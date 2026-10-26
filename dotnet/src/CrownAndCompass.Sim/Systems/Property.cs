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
        int idle = town.Workers.Count(x => x.JobId == null);
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
                    // A landlord with more gold than tenants puts it to work where hands are idle.
                    if (a.Gold > 320 && idle >= 3) TryOpenBusiness(town, a, keepAmbition: true);
                    break;
                case Ambition.None:
                    if (a.Has(Trait.Ambitious) && a.Gold > 180) a.Ambition = Ambition.OpenBusiness;
                    else if (a.Has(Trait.Greedy) && a.Gold > 120) a.Ambition = Ambition.Landlord;
                    // Savings sit idle only so long: anyone but the lazy buys into a trade the town wants.
                    else if (!a.Has(Trait.Lazy) && a.Gold > 240) a.Ambition = Ambition.OpenBusiness;
                    break;
            }
        }
        Invest(town, idle);
    }

    /// <summary>
    /// The town's richest put their savings to work: each day a few of them (more in a bigger town)
    /// open whatever business the town wants most, whatever their own ambitions, so money hoarded by
    /// a lucky few comes back as jobs.
    /// </summary>
    private static void Invest(Town town, int idle)
    {
        var workers = town.Workers;
        int slots = 1 + workers.Count / 150;
        foreach (var a in workers.Where(x => x.Gold > 300 && !x.Has(Trait.Lazy)).OrderByDescending(x => x.Gold).ThenBy(x => x.Id).Take(slots).ToList())
        {
            if (idle <= 0 && town.Workers.Count(x => x.Roofless) == 0) return;
            int before = town.Buildings.Count;
            TryOpenBusiness(town, a, keepAmbition: true);
            if (town.Buildings.Count > before) idle -= 2;
        }
    }

    /// <summary>How much the town wants another building of this kind (1.0 = balanced).</summary>
    public static double Demand(Town town, string buildingId)
    {
        // Every would-be owner asks the same question many times a day; answer it once per hour.
        var city = town.City;
        long stamp = (long)town.Tick * 100_000 + town.Buildings.Count;
        if (city.DemandStamp != stamp)
        {
            city.DemandCache.Clear();
            city.DemandStamp = stamp;
        }
        if (city.DemandCache.TryGetValue(buildingId, out double cached)) return cached;
        double demand = WorkOutDemand(town, buildingId);
        city.DemandCache[buildingId] = demand;
        return demand;
    }

    private static double WorkOutDemand(Town town, string buildingId)
    {
        var d = town.Content.Building(buildingId);
        if (!City.Unlocked(town, d)) return 0;
        var m = town.Market;
        // Sites already rising will add supply soon; each one dampens the signal.
        double pending = 1 - 0.15 * town.Buildings.Count(b => b.IsSite && b.Def.Id == buildingId);
        // Idle hands are cheap labor: a pool of unemployed makes any workshop look attractive.
        int idle = town.Workers.Count(a => a.JobId == null);
        double labor = Math.Min(0.4, idle * 0.04);
        double signal = buildingId switch
        {
            "tavern" => TavernDemand(town),
            "general_store" => StoreDemand(town),
            _ when d.Rooms > 0 && !d.Venue => RoomDemand(town),
            // A crop with its own calendar is worth more sown in the season it beats the rest (Farming.Signal).
            _ when d.Produces.Count > 0 => ProducerSignal(m.Realized(d.Produces[0].Key) / m.BasePrice(d.Produces[0].Key), labor) * Farming.Signal(town, d),
            _ when d.Fee > 0 && d.Serves.Count > 0 => ServiceDemand(town, d),
            _ => 0,
        };
        return pending * signal;
    }

    /// <summary>
    /// A workshop pays when its good is dear. Idle hands make it look better still: even a trade in glut
    /// can sell its surplus to the barges at the export price (a healthy market sits near 0.91 of base,
    /// the export floor at 0.75), so idle hands push the town into trades it can ship downriver.
    /// </summary>
    private static double ProducerSignal(double ratio, double labor) => ratio + labor * (ratio >= 0.88 ? 1 : 0.6);

    /// <summary>A bathhouse or theatre pays when the residents who want one outnumber the places in the ones there are.</summary>
    private static double ServiceDemand(Town town, BuildingDef d)
    {
        string service = d.Serves.Keys.Order(StringComparer.Ordinal).First();
        double capacity = 0;
        foreach (var b in town.Buildings)
            if (b.Def.Serves.TryGetValue(service, out int n) && !b.ForSale && !b.Razed) capacity += n;
        int users = 0;
        foreach (var house in town.Buildings)
            if (house.Complete && City.Housing(town, house.Def.Id) is { } h && (h.Services.Contains(service) || HousingWants(town, h, service))) users += house.Residents.Count;
        if (users < d.Serves[service] * 0.3) return 0;
        if (capacity == 0) return 2.0;
        return users / capacity + Crowds.Demand(town, service);
    }

    /// <summary>Families a step below want what the next house up needs, so the town builds ahead of them.</summary>
    private static bool HousingWants(Town town, HousingDef h, string service) =>
        h.Next != "" && City.Housing(town, h.Next) is { } next && next.Services.Contains(service) && City.Reached(town, town.Content.Building(h.Next).Tier);

    private static double TavernDemand(Town town)
    {
        int taverns = town.Buildings.Count(b => b.Def.Venue && !b.ForSale && !b.Razed);
        if (taverns == 0) return town.Population >= 12 ? 3.0 : 0.5;
        return town.Population / (taverns * 40.0) + Crowds.Demand(town, Crowds.Tavern);
    }

    /// <summary>An inn pays when guests camp outside for want of rooms (Lodging), counting the rooms already rising.</summary>
    private static double RoomDemand(Town town)
    {
        int rooms = Lodging.Rooms(town) + town.Buildings.Where(b => b.IsSite).Sum(b => b.Def.Rooms);
        int wanting = town.Lodging.Guests + town.Lodging.Camped;
        if (rooms == 0) return wanting >= 2 ? 2.0 : 0.3;
        return wanting / (double)rooms;
    }

    /// <summary>A general store lives on heroes buying potions before they march.</summary>
    private static double StoreDemand(Town town)
    {
        int stores = town.Buildings.Count(b => b.Def.Id == "general_store" && !b.ForSale && !b.Razed);
        int heroes = town.Heroes.Count;
        if (stores == 0) return heroes >= 3 ? 2.0 : 0.5;
        return heroes / (stores * 8.0);
    }

    private static void TryOpenBusiness(Town town, Agent a, bool keepAmbition = false)
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
            if (!keepAmbition) AfterOpening(a);
            return;
        }

        var candidates = town.Content.Buildings.Where(d => d.Buildable && d.IsBusiness && City.Unlocked(town, d)).ToList();
        BuildingDef? pick = null;
        if (candidates.Count == 0) return;
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
            foreach (var (d, s) in scored)
            {
                if (s < 1.05) break;
                // A farm stands only in a field sown with its crop: with other fields open, the next trade gets a look.
                if (d.Zone == Zone.Farmland && FindLot(town, d) == null && Land.OpenPlots(town, Zone.Farmland) > 0)
                {
                    if (pick == null && Farming.Crop(town, Farming.CropOf(town, d)) is { } crop) Planning.NoField(town, crop, a);
                    continue;
                }
                pick = d;
                break;
            }
        }
        if (pick == null) return;

        if (TryBuild(town, a, pick, false) && !keepAmbition) AfterOpening(a);
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
        int homeless = town.Workers.Count(x => x.Roofless);
        int openBeds = 0;
        foreach (var b in town.Buildings) openBeds += b.IsSite ? b.Def.Housing : b.OpenBeds;
        // Landlords build ahead of the town: a few empty beds draw settlers down the road.
        if (homeless <= openBeds && openBeds >= 2) return;
        var house = RentalKind(town);
        if (a.Gold < (town.LotPrice(Town.Grid) + house.Cost + Planning.Materials(town, house)) * 1.5) return;
        if (TryBuild(town, a, house, true) && !a.Has(Trait.Greedy)) a.Ambition = Ambition.None;
    }

    /// <summary>
    /// A landlord builds tall houses wall to wall on high-density land, tenements on open land outside the walls,
    /// and cottages until the town has either.
    /// </summary>
    private static BuildingDef RentalKind(Town town)
    {
        foreach (var d in town.Content.Buildings.Where(d => d.Zone == Zone.Residential && d.Buildable && d.Density > Density.Low).OrderByDescending(d => d.Density).ThenBy(d => d.Id, StringComparer.Ordinal))
            if (City.Unlocked(town, d) && FindLot(town, d) != null) return d;
        var tenement = town.Content.Building("tenement");
        return City.Unlocked(town, tenement) && FindLot(town, tenement) != null ? tenement : town.Content.Building("house");
    }

    /// <summary>
    /// Why painted land of a zone with room on it sits empty right now, in a few words for the build bar, or null when
    /// someone could build there. Reads the town only.
    /// </summary>
    public static string? Waiting(Town town, Zone zone)
    {
        if (zone is not (Zone.Residential or Zone.Commercial or Zone.Industry) || Land.OpenPlots(town, zone) == 0) return null;
        if (!Construction.Room(town))
        {
            var stalled = town.Buildings.Where(b => Construction.Zoned(town, b) && Construction.Stalled(town, b)).ToList();
            if (stalled.Count == 0) return "waits on builders: every crew is busy";
            bool stone = stalled.Any(b => Construction.Wanting(b).Stone > 0), timber = stalled.Any(b => Construction.Wanting(b).Timber > 0);
            return $"waits on {(timber && stone ? "timber and stone" : stone ? "stone" : "timber")}: building sites stand idle";
        }
        var workers = town.Workers;
        if (zone == Zone.Residential)
        {
            var house = town.Content.Building("house");
            double price = house.Cost + Planning.Materials(town, house);
            var wanting = workers.Where(a => a.Ambition is Ambition.OwnHome or Ambition.Landlord || a.HomeId == null).ToList();
            if (wanting.Count == 0) return "nobody needs a new home yet";
            return wanting.Any(a => a.Gold >= price) ? null : $"no settler can afford a house yet ({Js.Round(price)}g)";
        }
        var wanted = town.Content.Buildings
            .Where(d => d.Buildable && d.IsBusiness && d.Zone == zone && City.Unlocked(town, d))
            .Select(d => (d, s: Demand(town, d.Id)))
            .Where(x => x.s >= 1.05)
            .OrderByDescending(x => x.s).ThenBy(x => x.d.Id, StringComparer.Ordinal)
            .ToList();
        if (wanted.Count == 0) return zone == Zone.Commercial ? "no shop the town wants yet" : "no trade the town wants yet";
        double cheapest = wanted.Min(x => x.d.Cost + Planning.Materials(town, x.d));
        return workers.Any(a => a.Gold >= cheapest) ? null : $"nobody has the {Js.Round(cheapest)}g to open a {wanted[0].d.Name.ToLowerInvariant()}";
    }

    /// <summary>What the owner of a business asks the council for it: its worth today, with a premium, and the cash in its till.</summary>
    public static double BuyPrice(Town town, Building b) =>
        Js.Round(TotalCost(town, b.Def, town.Lot(b.LotId)) * (1 + town.Content.Rules.BuyoutPremium) + Math.Max(0, b.Cash));

    /// <summary>Why the council can't buy this building from its owner now (and the owner's own words when they won't sell), or null.</summary>
    public static string? BuyBlocked(Town town, Building b)
    {
        var rules = town.Content.Rules;
        if (!b.Def.IsBusiness || b.Def.Civic) return "not a business";
        if (b.OwnerId is not int ownerId) return "the town owns it already";
        if (!b.Complete || b.ForSale) return "it isn't trading";
        var owner = town.Agent(ownerId);
        bool proud = owner.Has(Trait.Ambitious);
        double need = proud ? rules.BuyoutProudMandate : rules.BuyoutMandate;
        if (b.DaysInDebt == 0 && town.Mandate < need)
            return proud ? $"{owner.GivenName} built it up and won't sell to a council with Mandate under {Js.Str(need)}" : $"{owner.GivenName} doesn't trust the council enough to sell (Mandate {Js.Str(need)})";
        if (town.Treasury < BuyPrice(town, b)) return "not enough gold";
        return null;
    }

    /// <summary>The council buys a townsman's business: the owner takes the gold, the town takes its profits (and its debts).</summary>
    public static bool Buy(Town town, Building b)
    {
        if (BuyBlocked(town, b) != null) return false;
        double price = BuyPrice(town, b);
        var owner = town.Agent(b.OwnerId!.Value);
        town.Book(TreasuryAccount.Buyouts, -price);
        owner.Gold += price;
        b.OwnerId = null;
        town.Lot(b.LotId).OwnerId = null;
        town.Log(LedgerCategory.Business, Importance.Notable, $"The council buys {b.Name} from {owner.GivenName} for {Js.Str(price)} gold. Its profits go to the treasury now.", [owner.Id]);
        return true;
    }

    public static double TotalCost(Town town, BuildingDef def, Lot lot) =>
        town.LotPrice(lot) + def.Cost + Planning.Materials(town, def);

    private static bool TryBuild(Town town, Agent a, BuildingDef def, bool rental)
    {
        // Short of the build itself, no lot is cheap enough: don't go looking.
        if (a.Gold < def.Cost + Planning.Materials(town, def)) return false;
        // Every crew busy: the family waits its turn.
        if (!Construction.Room(town)) return false;
        var lot = FindLot(town, def);
        if (lot == null)
        {
            if (a.Gold >= def.Cost) Planning.NoLand(town, def.Zone, a);
            return false;
        }
        double lotPrice = town.LotPrice(lot);
        if (a.Gold < TotalCost(town, def, lot) + 5) return false;

        a.Gold -= lotPrice;
        town.Book(TreasuryAccount.LandSales, lotPrice);
        a.Gold -= def.Cost;

        // The timber and stone come off the market as the haulers carry them, paid from the builder's purse: with none in
        // town the site stalls until the market fills or the owner sends away for it (Construction.Imports).
        var site = town.AddBuilding(def, lot, a.Id, NameFor(town, def, a), false);
        site.Draws = true;
        site.LaborBudget = Construction.Hours(town, def) * Brain.ConstructionWage;

        string what = def.Housing > 0 ? (rental ? $"a {def.Name.ToLowerInvariant()} to let" : "a home for the family") : site.Name;
        town.Log(
            LedgerCategory.Property,
            def.IsBusiness && !town.Buildings.Any(o => o != site && o.Def.Id == def.Id) ? Importance.Notable : Importance.Routine,
            $"{a.Name} buys {(def.Zone == Zone.Industry ? "an" : "a")} {Planning.ZoneName(def.Zone)} lot for {Js.ToFixed(lotPrice, 0)} gold and breaks ground on {what}.",
            [a.Id]);
        return true;
    }

    /// <summary>Where a building of this kind would go on its zone's painted land, nearest the hall, in a ward it may stand in (<see cref="Land.Find"/>).</summary>
    public static Lot? FindLot(Town town, BuildingDef def) => Land.Find(town, def, def.Zone);

    /// <summary>As <see cref="FindLot(Town, BuildingDef)"/>, on another zone's land.</summary>
    public static Lot? FindLot(Town town, Zone zone, BuildingDef def) => Land.Find(town, def, zone);

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
            case "inn":
                return UniqueTavernName(town) + " Inn";
            case "general_store":
                name = $"{family}'s General Store";
                break;
            case "tenement":
                name = owner == null ? "the Charter Tenement" : $"{family}'s Rents";
                break;
            default:
                name = def.IsBusiness && owner != null ? $"{family} {def.Name}" : def.Name;
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
            if (b.Def.IsGrand) GrandWorks.Finished(town, b);
        }
    }

    private static void Complete(Town town, Building b)
    {
        Economy.ReleaseWorkers(town, b);
        if (b.OwnerId != null)
        {
            var owner = town.Agent(b.OwnerId.Value);
            owner.Gold += b.LaborBudget;
            // Tenants who took its beds first keep them: the owner moves in only if the household fits.
            int household = owner.SpouseId != null && town.Agent(owner.SpouseId.Value) is { Departed: false, Hero: null } ? 2 : 1;
            if (b.Def.Housing - b.Residents.Count >= household && (owner.HomeId == null || town.Building(owner.HomeId.Value).OwnerId != owner.Id))
            {
                Economy.MoveIn(town, owner, b);
                if (owner.SpouseId != null) Economy.MoveIn(town, town.Agent(owner.SpouseId.Value), b);
            }
        }
        b.LaborBudget = 0;
        // The town's first of a trade is news; the tenth brewery is routine.
        bool firstOfKind = !town.Buildings.Any(o => o != b && o.Complete && o.Def.Id == b.Def.Id);
        town.Log(
            LedgerCategory.Construction,
            b.Def.IsBusiness && firstOfKind ? Importance.Notable : Importance.Routine,
            $"{Capitalize(b.Name)} is finished{(b.OwnerId != null ? $" for {town.OwnerName(b.OwnerId)}" : "")}.",
            b.OwnerId != null ? [b.OwnerId.Value] : []);
    }

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
