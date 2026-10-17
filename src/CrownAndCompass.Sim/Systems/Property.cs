using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Settling, buying and building: agents with ambitions and savings buy lots from the town,
/// break ground, and staff what they build. The mayor only decides which lots are zoned.
/// </summary>
public static class Property
{
    private static readonly string[] TavernAdjectives =
        { "Prancing", "Drunken", "Gilded", "Rusty", "Sleeping", "Laughing", "Crooked", "Wandering", "Golden", "Howling" };
    private static readonly string[] TavernNouns =
        { "Griffon", "Mimic", "Owlbear", "Flagon", "Dragon", "Lantern", "Kobold", "Boar", "Wyvern", "Compass" };

    public static void Daily(Town town)
    {
        foreach (var a in town.Residents.ToList())
        {
            switch (a.Ambition)
            {
                case Ambition.OpenBusiness: TryOpenBusiness(town, a); break;
                case Ambition.OwnHome: TryBuildHome(town, a); break;
                case Ambition.Landlord: TryBuildRental(town, a); break;
                case Ambition.None:
                    if (a.Has(Trait.Ambitious) && a.Gold > 180) a.Ambition = Ambition.OpenBusiness;
                    else if (a.Has(Trait.Greedy) && a.Gold > 120) a.Ambition = Ambition.Landlord;
                    break;
            }
        }
    }

    /// <summary>How much the town wants another building of this kind (1.0 = balanced).</summary>
    public static double Demand(Town town, string buildingId)
    {
        var m = town.Market;
        // Sites already rising will add supply soon; each one dampens the signal.
        double pending = 1 - 0.15 * town.Buildings.Count(b => b.IsSite && b.Def.Id == buildingId);
        // Idle hands are cheap labor: a pool of unemployed makes any workshop look attractive.
        int idle = town.Residents.Count(a => a.JobId == null);
        double labor = Math.Min(0.4, idle * 0.025);
        return pending * buildingId switch
        {
            "farm" => m.Price(Good.Food) / Market.BasePrice[Good.Food] + labor,
            "lumber_camp" => m.Price(Good.Timber) / Market.BasePrice[Good.Timber] + labor,
            "forge" => m.Price(Good.Tools) / Market.BasePrice[Good.Tools] + labor,
            "brewery" => m.Price(Good.Ale) / Market.BasePrice[Good.Ale] + labor,
            "tavern" => TavernDemand(town),
            _ => 0,
        };
    }

    private static double TavernDemand(Town town)
    {
        int taverns = town.Buildings.Count(b => b.Def.Venue && !b.ForSale);
        if (taverns == 0) return town.Population >= 12 ? 3.0 : 0.5;
        return town.Population / (taverns * 40.0);
    }

    private static void TryOpenBusiness(Town town, Agent a)
    {
        // A bargain first: a failed business going cheap.
        var forSale = town.Buildings
            .Where(b => b.ForSale && b.Complete && Demand(town, b.Def.Id) > 0.8)
            .OrderBy(b => Economy.SalePrice(town, b))
            .FirstOrDefault();
        if (forSale != null && a.Gold >= Economy.SalePrice(town, forSale) + 20)
        {
            double price = Economy.SalePrice(town, forSale);
            a.Gold -= price;
            town.Treasury += price;
            string old = forSale.Name;
            forSale.OwnerId = a.Id;
            forSale.ForSale = false;
            forSale.Closed = false;
            forSale.Cash = 20;
            a.Gold -= 20;
            town.Lot(forSale.LotId).OwnerId = a.Id;
            forSale.Name = NameFor(town, forSale.Def, a);
            town.Log(LedgerCategory.Property, Importance.Notable,
                $"{a.Name} buys the shuttered {old} for {price:0} gold and reopens it as {forSale.Name}.", a.Id);
            AfterOpening(a);
            return;
        }

        var candidates = town.Content.Buildings.Values.Where(d => d.Buildable && d.IsBusiness).ToList();
        BuildingDef? pick = null;
        if (a.DreamBusiness != null && Demand(town, a.DreamBusiness) >= 0.85)
            pick = town.Content.Building(a.DreamBusiness);
        else
        {
            var best = candidates.OrderByDescending(d => Demand(town, d.Id)).ThenBy(d => d.Id).First();
            if (Demand(town, best.Id) >= 1.05) pick = best;
        }
        if (pick == null) return;

        if (TryBuild(town, a, pick, rental: false)) AfterOpening(a);
    }

    private static void AfterOpening(Agent a)
    {
        a.Ambition = a.Has(Trait.Greedy) ? Ambition.Landlord : Ambition.OwnHome;
        a.DreamBusiness = null;
    }

    private static void TryBuildHome(Town town, Agent a)
    {
        if (a.HomeId is int h && (town.Building(h).OwnerId == a.Id || (a.SpouseId is int s && town.Building(h).OwnerId == s)))
        {
            a.Ambition = Ambition.None;
            return;
        }
        if (TryBuild(town, a, town.Content.Building("house"), rental: false))
            a.Ambition = Ambition.None;
    }

    private static void TryBuildRental(Town town, Agent a)
    {
        int homeless = town.Residents.Count(x => x.HomeId == null);
        int openBeds = town.Buildings.Sum(b => b.OpenBeds) + town.Buildings.Where(b => b.IsSite).Sum(b => b.Def.Housing);
        if (homeless <= openBeds) return;
        var house = town.Content.Building("house");
        if (a.Gold < TotalCost(town, house, town.Lots.First()) * 1.5) return;
        if (TryBuild(town, a, house, rental: true) && !a.Has(Trait.Greedy)) a.Ambition = Ambition.None;
    }

    public static double TotalCost(Town town, BuildingDef def, Lot lot) =>
        town.LotPrice(lot) + def.Cost + town.Market.Quote(Good.Timber, def.Timber);

    private static bool TryBuild(Town town, Agent a, BuildingDef def, bool rental)
    {
        var lot = FindLot(town, def.Zone);
        if (lot == null) return false;
        double lotPrice = town.LotPrice(lot);
        double total = TotalCost(town, def, lot);
        if (a.Gold < total + 5) return false;

        a.Gold -= lotPrice;
        town.Treasury += lotPrice;
        a.Gold -= town.Market.Buy(Good.Timber, def.Timber);
        a.Gold -= def.Cost;

        var site = town.AddBuilding(def, lot, a.Id, NameFor(town, def, a), complete: false);
        site.LaborBudget = def.BuildHours * Brain.ConstructionWage;

        string what = def.Id == "house"
            ? (rental ? "a house to let" : "a home for the family")
            : $"{site.Name}";
        town.Log(LedgerCategory.Property, def.IsBusiness ? Importance.Notable : Importance.Routine,
            $"{a.Name} buys a {def.Zone.ToString().ToLowerInvariant()} lot for {lotPrice:0} gold and breaks ground on {what}.", a.Id);
        return true;
    }

    private static Lot? FindLot(Town town, Zone zone) =>
        town.Lots
            .Where(l => l.Zone == zone && l.IsVacant && l.OwnerId == null)
            .OrderBy(Town.DistanceFromCenter)
            .ThenBy(l => l.Id)
            .FirstOrDefault();

    public static string NameFor(Town town, BuildingDef def, Agent? owner)
    {
        string family = owner?.FamilyName ?? "Charter";
        string name = def.Id switch
        {
            "house" => owner == null ? $"Charter House {town.Buildings.Count(b => b.Def.Id == "house") + 1}" : $"the {family} house",
            "farm" => $"{family} Farm",
            "lumber_camp" => $"{family} Lumber Camp",
            "forge" => $"{family}'s Forge",
            "brewery" => $"{owner?.GivenName ?? "Charter"}'s Brewery",
            "tavern" => UniqueTavernName(town),
            _ => def.Name,
        };
        // A tycoon's second forge is "Nailo's Forge No. 2", not a twin.
        int same = town.Buildings.Count(b => b.Name == name || b.Name.StartsWith(name + " No. "));
        return same == 0 || def.Id == "tavern" ? name : $"{name} No. {same + 1}";
    }

    private static string UniqueTavernName(Town town)
    {
        for (int i = 0; i < 20; i++)
        {
            string name = $"The {town.Rng.Pick(TavernAdjectives)} {town.Rng.Pick(TavernNouns)}";
            if (town.Buildings.All(b => b.Name != name)) return name;
        }
        return $"Tavern No. {town.Buildings.Count}";
    }

    /// <summary>Hourly: finish construction sites whose labor is done.</summary>
    public static void Hourly(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (b.Opened || !b.Complete) continue;
            b.Opened = true;
            Complete(town, b);
        }
    }

    private static void Complete(Town town, Building b)
    {
        Economy.ReleaseWorkers(town, b);
        if (b.OwnerId is int oid)
        {
            var owner = town.Agent(oid);
            owner.Gold += b.LaborBudget;
            b.LaborBudget = 0;
            if (b.Def.Housing > 0 && (owner.HomeId == null || town.Building(owner.HomeId.Value).OwnerId != owner.Id))
            {
                Economy.MoveIn(town, owner, b);
                if (owner.SpouseId is int s) Economy.MoveIn(town, town.Agent(s), b);
            }
        }
        b.LaborBudget = 0;
        town.Log(LedgerCategory.Construction, b.Def.IsBusiness ? Importance.Notable : Importance.Routine,
            $"{Capitalize(b.Name)} is finished{(b.OwnerId != null ? $" for {town.OwnerName(b.OwnerId)}" : "")}.",
            b.OwnerId is int o ? new[] { o } : Array.Empty<int>());
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
