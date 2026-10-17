namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Settling, buying and building: agents with ambitions and savings buy lots from the town,
/// break ground, and staff what they build. The mayor only decides which lots are zoned.
/// </summary>
public static class Property
{
    private static readonly string[] TavernAdjectives = ["Prancing", "Drunken", "Gilded", "Rusty", "Sleeping", "Laughing", "Crooked", "Wandering", "Golden", "Howling"];
    private static readonly string[] TavernNouns = ["Griffon", "Mimic", "Owlbear", "Flagon", "Dragon", "Lantern", "Kobold", "Boar", "Wyvern", "Compass"];

    public static void Daily(Town town)
    {
        foreach (var a in town.Workers)
        {
            switch (a.Ambition)
            {
                case Ambition.OpenBusiness:
                    TryOpenBusiness(town, a);
                    break;
                case Ambition.OwnHome:
                    TryBuildHome(town, a);
                    break;
                case Ambition.Landlord:
                    TryBuildRental(town, a);
                    break;
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
        int idle = town.Workers.Count(a => a.JobId == null);
        double labor = Math.Min(0.4, idle * 0.025);
        double signal = buildingId switch
        {
            "farm" => m.Price(Good.Food) / Market.BasePrice(Good.Food) + labor,
            "lumber_camp" => m.Price(Good.Timber) / Market.BasePrice(Good.Timber) + labor,
            "forge" => m.Price(Good.Tools) / Market.BasePrice(Good.Tools) + labor,
            "brewery" => m.Price(Good.Ale) / Market.BasePrice(Good.Ale) + labor,
            "tavern" => TavernDemand(town),
            "general_store" => StoreDemand(town),
            _ => 0,
        };
        return pending * signal;
    }

    private static double TavernDemand(Town town)
    {
        int taverns = town.Buildings.Count(b => b.Def.Venue && !b.ForSale);
        if (taverns == 0) return town.Population >= 12 ? 3.0 : 0.5;
        return town.Population / (taverns * 40.0);
    }

    /// <summary>A general store lives on heroes buying potions before they march.</summary>
    private static double StoreDemand(Town town)
    {
        int stores = town.Buildings.Count(b => b.Def.Id == "general_store" && !b.ForSale);
        int heroes = town.Heroes.Count;
        if (stores == 0) return heroes >= 3 ? 2.0 : 0.5;
        return heroes / (stores * 8.0);
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
            town.Book(TreasuryAccount.LandSales, price);
            string old = forSale.Name;
            forSale.OwnerId = a.Id;
            forSale.ForSale = false;
            forSale.Closed = false;
            forSale.Cash = 20;
            a.Gold -= 20;
            town.Lot(forSale.LotId).OwnerId = a.Id;
            forSale.Name = NameFor(town, forSale.Def, a);
            town.Log(LedgerCategory.Property, Importance.Notable, $"{a.Name} buys the shuttered {old} for {Js.ToFixed(price, 0)} gold and reopens it as {forSale.Name}.", [a.Id]);
            AfterOpening(a);
            return;
        }

        var candidates = town.Content.Buildings.Where(d => d.Buildable && d.IsBusiness).ToList();
        BuildingDef? pick = null;
        if (a.DreamBusiness != null && Demand(town, a.DreamBusiness) >= 0.85)
        {
            pick = town.Content.Building(a.DreamBusiness);
        }
        else
        {
            var scored = candidates
                .Select(d => (d, s: Demand(town, d.Id)))
                .OrderByDescending(x => x.s)
                .ThenBy(x => x.d.Id, StringComparer.Ordinal)
                .ToList();
            if (scored[0].s >= 1.05) pick = scored[0].d;
        }
        if (pick == null) return;

        if (TryBuild(town, a, pick, false)) AfterOpening(a);
    }

    private static void AfterOpening(Agent a)
    {
        a.Ambition = a.Has(Trait.Greedy) ? Ambition.Landlord : Ambition.OwnHome;
        a.DreamBusiness = null;
    }

    private static void TryBuildHome(Town town, Agent a)
    {
        if (a.HomeId != null)
        {
            var home = town.Building(a.HomeId.Value);
            if (home.OwnerId == a.Id || (a.SpouseId != null && home.OwnerId == a.SpouseId))
            {
                a.Ambition = Ambition.None;
                return;
            }
        }
        if (TryBuild(town, a, town.Content.Building("house"), false)) a.Ambition = Ambition.None;
    }

    private static void TryBuildRental(Town town, Agent a)
    {
        int homeless = town.Workers.Count(x => x.HomeId == null);
        int openBeds = 0;
        foreach (var b in town.Buildings) openBeds += b.IsSite ? b.Def.Housing : b.OpenBeds;
        if (homeless <= openBeds) return;
        var house = town.Content.Building("house");
        if (a.Gold < TotalCost(town, house, town.Lots[0]) * 1.5) return;
        if (TryBuild(town, a, house, true) && !a.Has(Trait.Greedy)) a.Ambition = Ambition.None;
    }

    public static double TotalCost(Town town, BuildingDef def, Lot lot) =>
        town.LotPrice(lot) + def.Cost + town.Market.Quote(Good.Timber, def.Timber);

    private static bool TryBuild(Town town, Agent a, BuildingDef def, bool rental)
    {
        var lot = FindLot(town, def.Zone);
        if (lot == null)
        {
            if (a.Gold >= def.Cost) Planning.NoLand(town, def.Zone, a);
            return false;
        }
        double lotPrice = town.LotPrice(lot);
        if (a.Gold < TotalCost(town, def, lot) + 5) return false;

        a.Gold -= lotPrice;
        town.Book(TreasuryAccount.LandSales, lotPrice);
        a.Gold -= town.Market.Buy(Good.Timber, def.Timber);
        a.Gold -= def.Cost;

        var site = town.AddBuilding(def, lot, a.Id, NameFor(town, def, a), false);
        site.LaborBudget = def.BuildHours * Brain.ConstructionWage;

        string what = def.Id == "house" ? (rental ? "a house to let" : "a home for the family") : site.Name;
        town.Log(
            LedgerCategory.Property,
            def.IsBusiness ? Importance.Notable : Importance.Routine,
            $"{a.Name} buys {(def.Zone == Zone.Industry ? "an" : "a")} {Planning.ZoneName(def.Zone)} lot for {Js.ToFixed(lotPrice, 0)} gold and breaks ground on {what}.",
            [a.Id]);
        return true;
    }

    public static Lot? FindLot(Town town, Zone zone) =>
        town.Lots
            .Where(l => l.Zone == zone && l.IsVacant && l.OwnerId == null)
            .OrderBy(Town.DistanceFromCenter)
            .ThenBy(l => l.Id)
            .FirstOrDefault();

    public static string NameFor(Town town, BuildingDef def, Agent? owner)
    {
        string family = owner?.FamilyName ?? "Charter";
        string name;
        switch (def.Id)
        {
            case "house":
                name = owner == null ? $"Charter House {town.Buildings.Count(b => b.Def.Id == "house") + 1}" : $"the {family} house";
                break;
            case "farm":
                name = $"{family} Farm";
                break;
            case "lumber_camp":
                name = $"{family} Lumber Camp";
                break;
            case "forge":
                name = $"{family}'s Forge";
                break;
            case "brewery":
                name = $"{owner?.GivenName ?? "Charter"}'s Brewery";
                break;
            case "tavern":
                return UniqueTavernName(town);
            case "general_store":
                name = $"{family}'s General Store";
                break;
            default:
                name = def.Name;
                break;
        }
        // A tycoon's second forge is "Nailo's Forge No. 2", not a twin.
        int same = town.Buildings.Count(b => b.Name == name || b.Name.StartsWith($"{name} No. ", StringComparison.Ordinal));
        return same == 0 ? name : $"{name} No. {same + 1}";
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
        if (b.OwnerId != null)
        {
            var owner = town.Agent(b.OwnerId.Value);
            owner.Gold += b.LaborBudget;
            if (b.Def.Housing > 0 && (owner.HomeId == null || town.Building(owner.HomeId.Value).OwnerId != owner.Id))
            {
                Economy.MoveIn(town, owner, b);
                if (owner.SpouseId != null) Economy.MoveIn(town, town.Agent(owner.SpouseId.Value), b);
            }
        }
        b.LaborBudget = 0;
        town.Log(
            LedgerCategory.Construction,
            b.Def.IsBusiness ? Importance.Notable : Importance.Routine,
            $"{Capitalize(b.Name)} is finished{(b.OwnerId != null ? $" for {town.OwnerName(b.OwnerId)}" : "")}.",
            b.OwnerId != null ? [b.OwnerId.Value] : []);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
