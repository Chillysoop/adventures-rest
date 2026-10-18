using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim.Systems;

/// <summary>A step on the town's road from thorp to great city (GDD rule C1).</summary>
public sealed record TierDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>Residents the town needs before it can claim the charter.</summary>
    public int Residents { get; init; }

    /// <summary>Institutions (building ids) that must stand, finished, before the charter is granted.</summary>
    public IReadOnlyList<string> Requires { get; init; } = [];

    /// <summary>Walls that must stand first: StoneWall or CityWall, empty for none.</summary>
    public string Walls { get; init; } = "";

    /// <summary>Heroes the town's taverns can keep at once: a bigger name draws more sellswords.</summary>
    public int Heroes { get; init; } = 12;

    /// <summary>Gold the charter costs the treasury: fees to the crown, seals, a feast.</summary>
    public double Cost { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>
/// A ward of the town grid (GDD rule C3): the old town at the heart, four wards inside the city
/// wall, and four outer wards beyond it. Rings and sides place it; the rest gives it a character.
/// </summary>
public sealed record WardDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>0 is the old town, 1 the ring inside the city wall, 2 the outer ring.</summary>
    public int Ring { get; init; }

    /// <summary>north, east, south or west of the hall; empty for the old town.</summary>
    public string Side { get; init; } = "";

    /// <summary>The tier the town must reach before the ward can be laid out.</summary>
    public string Tier { get; init; } = "";

    /// <summary>What surveying it and laying its streets costs the treasury.</summary>
    public double Cost { get; init; }
    public double Rent { get; init; } = 1;
    public double Land { get; init; } = 1;

    /// <summary>Mood its residents carry every hour, for better or worse.</summary>
    public double Mood { get; init; }

    /// <summary>The zone it lends itself to, which the stand-in mayor paints there first.</summary>
    public Zone Favours { get; init; } = Zone.None;

    /// <summary>Outside every wall: cheap, rowdy, and raided first (GDD rule C4).</summary>
    public bool Outer { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>What the families of one kind of house want, and what it grows into.</summary>
public sealed record HousingDef
{
    public required string Id { get; init; }

    /// <summary>The house it grows into once its families are content and the tier allows.</summary>
    public string Next { get; init; } = "";

    /// <summary>Goods each resident buys a day.</summary>
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Goods { get; init; } = [];

    /// <summary>Services the town must offer enough of (tavern, shrine, market, bath, theatre, learning).</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>Hearth tax each resident pays the treasury a day at the town's usual tax rate; grander houses pay more.</summary>
    public double Tax { get; init; }
}

/// <summary>Desire paths (city.json "paths"): wear per walk, how much a day's walks can wear, what's left of wear by the next day, and the wear that makes a lane.</summary>
public sealed record PathRules
{
    public double WearPerTrip { get; init; } = 0.008;
    public int MostTrips { get; init; } = 20;
    public double Fade { get; init; } = 0.97;
    public double Lay { get; init; } = 1.2;
}

/// <summary>How the town grows (content/game/city.json).</summary>
public sealed record CityRules
{
    /// <summary>Souls each resident stands for (their household), for the size the townsfolk boast of.</summary>
    public double SoulsPerResident { get; init; } = 5;

    /// <summary>Base market price of each good.</summary>
    public IReadOnlyDictionary<string, double> Goods { get; init; } = new Dictionary<string, double>();

    /// <summary>Share of base price traders pay for the town's surplus, so a glut sinks prices only so far.</summary>
    public double ExportShare { get; init; } = 0.75;
    public IReadOnlyList<TierDef> Tiers { get; init; } = [];
    public IReadOnlyList<WardDef> Wards { get; init; } = [];
    public IReadOnlyList<HousingDef> Housing { get; init; } = [];

    /// <summary>Each service id as a sentence names it ("a bathhouse").</summary>
    public IReadOnlyDictionary<string, string> Services { get; init; } = new Dictionary<string, string>();

    /// <summary>How feet wear paths to buildings off the streets.</summary>
    public PathRules Paths { get; init; } = new();

    /// <summary>Days a house must be content (at UpgradeAt or better) before it grows.</summary>
    public int UpgradeDays { get; init; } = 6;

    /// <summary>Days a house can go wanting (below DowngradeBelow) before it falls back a step.</summary>
    public int DowngradeDays { get; init; } = 10;
    public double UpgradeAt { get; init; } = 0.85;
    public double DowngradeBelow { get; init; } = 0.4;

    /// <summary>Mood a fully content household carries; a wanting one loses as much.</summary>
    public double NeedsMood { get; init; } = 6;

    public double BasePrice(Good g) => Goods.TryGetValue(g.ToString(), out double p) ? p : 1;
}

/// <summary>The town's standing as a settlement: its tier, the wards laid out, and what its people want.</summary>
public sealed class CityState
{
    /// <summary>Index into the tiers in city.json.</summary>
    public int Tier { get; set; }

    /// <summary>Wards laid out so far, in the order they opened.</summary>
    public List<string> OpenWards { get; } = ["old_town"];

    /// <summary>Share of each service the town covers (1 = enough for everyone), as of the last day.</summary>
    public OrderedMap<string, double> Coverage { get; set; } = new();

    /// <summary>Goods the households wanted yesterday, and the share of it they got.</summary>
    public OrderedMap<Good, double> Wanted { get; set; } = new();
    public OrderedMap<Good, double> Supplied { get; set; } = new();

    /// <summary>Day the town last heard the charter could be claimed, so the notice comes once.</summary>
    public int ReadyNoticeTier { get; set; } = -1;

    /// <summary>Demand for each building, worked out once per hour and set of buildings (see Property.Demand).</summary>
    [NotSaved]
    internal readonly Dictionary<string, double> DemandCache = [];
    [NotSaved]
    internal long DemandStamp = -1;
}

/// <summary>
/// The growing city (GDD rules C1, C3, C4): tiers claimed by charter once the town is big enough and
/// its institutions stand; wards laid out as the tiers allow; houses that grow a storey when their
/// families are content and fall back when they go wanting; and the goods and services they want.
/// </summary>
public static class City
{
    // Tiers ------------------------------------------------------------------------------------

    public static TierDef Tier(Town town) => town.Content.City.Tiers[town.City.Tier];

    public static TierDef? NextTier(Town town) =>
        town.City.Tier + 1 < town.Content.City.Tiers.Count ? town.Content.City.Tiers[town.City.Tier + 1] : null;

    public static int TierIndex(Town town, string id)
    {
        if (id == "") return 0;
        var tiers = town.Content.City.Tiers;
        for (int i = 0; i < tiers.Count; i++) if (tiers[i].Id == id) return i;
        throw new KeyNotFoundException($"Unknown town tier {id}");
    }

    public static string TierName(Town town, string id) => town.Content.City.Tiers[TierIndex(town, id)].Name;

    /// <summary>Whether the town has reached the tier a building or ward asks for.</summary>
    public static bool Reached(Town town, string tierId) => TierIndex(town, tierId) <= town.City.Tier;

    /// <summary>The most heroes who will make their home here at this tier.</summary>
    public static int HeroCap(Town town) => Tier(town).Heroes;

    /// <summary>The size the townsfolk boast of: every resident's household.</summary>
    public static int Souls(Town town) => (int)Js.Round(town.Population * town.Content.City.SoulsPerResident);

    /// <summary>What still stands between the town and its next charter, one line each; empty when it can be claimed.</summary>
    public static List<string> Missing(Town town)
    {
        var next = NextTier(town);
        if (next == null) return [];
        var lines = new List<string>();
        if (town.Population < next.Residents) lines.Add($"{next.Residents} residents (now {town.Population})");
        foreach (string id in next.Requires)
            if (!Has(town, id)) lines.Add($"a {town.Content.Building(id).Name.ToLowerInvariant()}");
        if (next.Walls != "" && !WallsStand(town, next.Walls)) lines.Add(next.Walls == "CityWall" ? "the city wall" : "stone walls");
        if (town.Treasury < next.Cost) lines.Add($"{Js.Str(next.Cost)} gold for the charter");
        return lines;
    }

    public static bool Has(Town town, string buildingId) => town.Buildings.Any(b => b.Def.Id == buildingId && b.Complete && !b.ForSale);

    public static bool WallsStand(Town town, string walls) =>
        walls == "CityWall" ? town.Defenses.CityWall == PalisadeState.Built : town.Defenses.StoneWall == PalisadeState.Built || town.Defenses.CityWall == PalisadeState.Built;

    /// <summary>Claim the next charter: the town becomes a hamlet, a village, a town and so on.</summary>
    public static bool RaiseCharter(Town town)
    {
        var next = NextTier(town);
        if (next == null || Missing(town).Count > 0) return false;
        town.Book(TreasuryAccount.Charters, -next.Cost);
        town.City.Tier++;
        town.Log(LedgerCategory.Town, Importance.Major, $"{town.Name} is granted the charter of a {next.Name.ToLowerInvariant()}. {next.Blurb}");
        town.Mandate = Math.Min(100, town.Mandate + 5);
        town.FestivalDays = Math.Max(town.FestivalDays, 1);
        return true;
    }

    // Wards ------------------------------------------------------------------------------------

    public static WardDef Ward(Town town, string id) =>
        town.Content.City.Wards.FirstOrDefault(w => w.Id == id) ?? throw new KeyNotFoundException($"Unknown ward {id}");

    /// <summary>The ward a lot lies in: the ring by distance from the hall, the side by direction.</summary>
    public static WardDef WardOf(Town town, Lot lot) => WardOf(town, lot.X, lot.Y);

    /// <summary>The ward a point of the town (tiles from its corner) lies in.</summary>
    public static WardDef WardOf(Town town, double x, double y)
    {
        var wards = town.Content.City.Wards;
        int ring = Ring(x, y);
        if (ring == 0) return wards.First(w => w.Ring == 0);
        string side = Side(x, y);
        return wards.FirstOrDefault(w => w.Ring == ring && w.Side == side) ?? wards.First(w => w.Ring == 0);
    }

    /// <summary>Tiles per ring band: the old town is the middle 16 by 16, each ring adds 8 on every side.</summary>
    public const int Band = 8;

    public static int Ring(Lot lot) => Ring(lot.X, lot.Y);

    public static int Ring(double x, double y)
    {
        double d = Math.Max(Math.Abs(x - Town.Center), Math.Abs(y - Town.Center));
        return d < Band ? 0 : d < 2 * Band ? 1 : 2;
    }

    public static string Side(Lot lot) => Side(lot.X, lot.Y);

    public static string Side(double x, double y)
    {
        double dx = x - Town.Center, dy = y - Town.Center;
        if (Math.Abs(dx) >= Math.Abs(dy)) return dx > 0 ? "east" : "west";
        return dy > 0 ? "south" : "north";
    }

    public static bool IsOpen(Town town, Lot lot) => IsOpen(town, lot.X, lot.Y);

    public static bool IsOpen(Town town, double x, double y) => town.City.OpenWards.Contains(WardOf(town, x, y).Id);

    /// <summary>Why the ward can't be laid out now, or null when it can.</summary>
    public static string? WardBlocked(Town town, string wardId)
    {
        var w = Ward(town, wardId);
        if (town.City.OpenWards.Contains(w.Id)) return "already laid out";
        if (!Reached(town, w.Tier)) return $"needs the town to be a {TierName(town, w.Tier).ToLowerInvariant()}";
        if (town.Treasury < w.Cost) return "not enough gold";
        return null;
    }

    /// <summary>Survey a ward and lay out its streets, opening its lots to zoning.</summary>
    public static bool OpenWard(Town town, string wardId)
    {
        if (WardBlocked(town, wardId) != null) return false;
        var w = Ward(town, wardId);
        town.Book(TreasuryAccount.Wards, -w.Cost);
        town.City.OpenWards.Add(w.Id);
        // New ground to paint and build on: searches of the old plan no longer hold.
        town.Plan.Changed();
        town.Log(LedgerCategory.Town, Importance.Major, $"Surveyors stake out the {w.Name} and the first streets are cobbled. {w.Blurb}");
        return true;
    }

    /// <summary>Whether a building may stand on this lot: its ward is open and the building allows it.</summary>
    public static bool Allows(Town town, BuildingDef d, Lot lot) => Allows(town, d, lot.X, lot.Y);

    public static bool Allows(Town town, BuildingDef d, double x, double y)
    {
        var w = WardOf(town, x, y);
        if (!town.City.OpenWards.Contains(w.Id)) return false;
        return d.Wards.Count == 0 || d.Wards.Contains(w.Id);
    }

    /// <summary>Whether the town could build this at all now: tier reached and a ward it may stand in is open.</summary>
    public static bool Unlocked(Town town, BuildingDef d) =>
        Reached(town, d.Tier) && (d.Wards.Count == 0 || d.Wards.Any(town.City.OpenWards.Contains));

    /// <summary>Why the town's tier or wards keep it from this building, or null.</summary>
    public static string? Locked(Town town, BuildingDef d)
    {
        if (!Reached(town, d.Tier)) return $"needs the town to be a {TierName(town, d.Tier).ToLowerInvariant()}";
        if (d.Wards.Count > 0 && !d.Wards.Any(town.City.OpenWards.Contains))
            return $"needs the {string.Join(" or the ", d.Wards.Select(w => Ward(town, w).Name))}";
        return null;
    }

    // Houses and their wants -------------------------------------------------------------------

    public static HousingDef? Housing(Town town, string buildingId) => town.Content.City.Housing.FirstOrDefault(h => h.Id == buildingId);

    /// <summary>The house a level down, the one this kind grew from.</summary>
    public static HousingDef? Previous(Town town, string buildingId) => town.Content.City.Housing.FirstOrDefault(h => h.Next == buildingId);

    /// <summary>Mood a resident carries from home: the ward's character, and how content the household is.</summary>
    public static double HomeMood(Town town, Agent a)
    {
        if (a.HomeId == null) return 0;
        var home = town.Building(a.HomeId.Value);
        double mood = WardOf(town, town.Lot(home.LotId)).Mood;
        if (Housing(town, home.Def.Id) is { } h && (h.Goods.Count > 0 || h.Services.Count > 0))
            mood += (home.Contentment - 0.5) * 2 * town.Content.City.NeedsMood;
        return mood;
    }

    public static string ServiceName(Town town, string id) => town.Content.City.Services.TryGetValue(id, out var n) ? n : id;

    /// <summary>Residents the town's buildings of each service can take, over the people who need it.</summary>
    private static void UpdateCoverage(Town town)
    {
        var cap = new OrderedMap<string, double>();
        foreach (string s in town.Content.City.Services.Keys.Order(StringComparer.Ordinal)) cap[s] = 0;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Closed || b.ForSale || b.Def.Serves.Count == 0) continue;
            foreach (string s in b.Def.Serves.Keys.Order(StringComparer.Ordinal)) cap[s] = cap.Get(s, 0) + b.Def.Serves[s];
        }
        int people = Math.Max(1, town.Workers.Count);
        var cov = new OrderedMap<string, double>();
        foreach (var (s, c) in cap) cov[s] = Math.Min(1, c / people);
        town.City.Coverage = cov;
    }

    public static double CoverageOf(Town town, string service) => town.City.Coverage.Get(service, 0);

    /// <summary>
    /// Daily: count the services, let every household buy what it wants, settle the service
    /// businesses' takings, grow the houses whose families are content and shrink the ones that
    /// went wanting, and let the stand-in mayor claim charters and lay out wards.
    /// </summary>
    public static void Daily(Town town, bool autoMayor)
    {
        UpdateCoverage(town);
        HearthTax(town);
        Households(town);
        Takings(town);
        Grow(town);
        ReadyNotice(town);
        if (autoMayor) Steward(town);
    }

    /// <summary>Every household pays its hearth tax, scaled by the tax rate the council has set.</summary>
    private static void HearthTax(Town town)
    {
        double scale = town.Content.Rules.TaxRate > 0 ? town.TaxRate / town.Content.Rules.TaxRate : 1;
        double total = 0;
        foreach (var house in town.Buildings)
        {
            if (!house.Complete || house.Def.Housing == 0 || Housing(town, house.Def.Id) is not { } h || h.Tax <= 0) continue;
            foreach (int id in house.Residents)
            {
                var a = town.Agent(id);
                double due = Math.Min(Math.Max(0, a.Gold), h.Tax * scale);
                a.Gold -= due;
                total += due;
            }
        }
        if (total > 0) town.Book(TreasuryAccount.HearthTax, total);
    }

    private static void Households(Town town)
    {
        var rules = town.Content.City;
        var wanted = new OrderedMap<Good, double>();
        var supplied = new OrderedMap<Good, double>();
        foreach (var house in town.Buildings)
        {
            if (!house.Complete || house.Def.Housing == 0) continue;
            var h = Housing(town, house.Def.Id);
            int n = house.Residents.Count;
            if (h == null || n == 0)
            {
                house.Contentment = 1;
                continue;
            }
            double sum = 0;
            int needs = 0;
            foreach (var (g, rate) in h.Goods)
            {
                double want = rate * n;
                double got = Buy(town, house, g, want);
                wanted[g] = wanted.Get(g, 0) + want;
                supplied[g] = supplied.Get(g, 0) + got;
                sum += want > 0 ? got / want : 1;
                needs++;
            }
            foreach (string s in h.Services)
            {
                sum += CoverageOf(town, s);
                needs++;
            }
            house.Contentment = Math.Clamp((needs == 0 ? 1 : sum / needs) + Decor.Comfort(town, house), 0, 1);
        }
        town.City.Wanted = wanted;
        town.City.Supplied = supplied;
    }

    /// <summary>A household buys what the market has of a good, split over the residents' purses; never by caravan.</summary>
    private static double Buy(Town town, Building house, Good g, double want)
    {
        double qty = Math.Min(want, town.Market.Stock(g));
        if (qty <= 0) return 0;
        double price = town.Market.Price(g);
        double purse = 0;
        foreach (int id in house.Residents) purse += Math.Max(0, town.Agent(id).Gold);
        qty = Math.Min(qty, purse / price);
        if (qty <= 0.0001) return 0;
        double cost = town.Market.Buy(g, qty);
        foreach (int id in house.Residents)
        {
            var a = town.Agent(id);
            a.Gold -= cost * Math.Max(0, a.Gold) / purse;
        }
        return qty;
    }

    /// <summary>Service businesses (bathhouses, theatres) are paid by the residents they serve, split by what each can take.</summary>
    private static void Takings(Town town)
    {
        var groups = town.Buildings
            .Where(b => b.Complete && !b.Closed && !b.ForSale && b.Def.Fee > 0 && b.Def.Serves.Count > 0)
            .GroupBy(b => b.Def.Serves.Keys.Order(StringComparer.Ordinal).First())
            .OrderBy(g => g.Key, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var list = group.OrderBy(b => b.Id).ToList();
            double capacity = list.Sum(b => b.Def.Serves[group.Key]);
            // Residents whose houses want this service, and who can pay, use it.
            int users = 0;
            foreach (var house in town.Buildings)
                if (house.Complete && Housing(town, house.Def.Id) is { } h && h.Services.Contains(group.Key)) users += house.Residents.Count;
            // The rest of the town goes now and then.
            users += (int)(town.Workers.Count * 0.15);
            users = (int)Math.Min(users, capacity);
            foreach (var b in list)
            {
                double share = users * b.Def.Serves[group.Key] / capacity;
                double revenue = share * b.Def.Fee;
                double tax = revenue * town.TaxRate;
                town.Book(TreasuryAccount.SalesTax, tax);
                b.Cash += revenue - tax;
                b.RevenueToday += revenue;
            }
        }
    }

    /// <summary>Houses grow a storey when their families are content and can pay, and fall back when they go wanting.</summary>
    private static void Grow(Town town)
    {
        var rules = town.Content.City;
        foreach (var house in town.Buildings.ToList())
        {
            if (!house.Complete || house.Def.Housing == 0) continue;
            if (house.Contentment < rules.DowngradeBelow)
            {
                house.UpDays = 0;
                if (++house.DownDays >= rules.DowngradeDays) Shrink(town, house);
                continue;
            }
            house.DownDays = 0;
            house.UpDays = house.Contentment >= rules.UpgradeAt ? house.UpDays + 1 : 0;
            if (house.UpDays >= rules.UpgradeDays) TryUpgrade(town, house);
        }
    }

    /// <summary>Would the families be content one step up? Goods the market holds enough of, services the town covers.</summary>
    public static double Prospect(Town town, Building house, HousingDef next)
    {
        int n = Math.Max(1, house.Residents.Count);
        double sum = 0;
        int needs = 0;
        foreach (var (g, rate) in next.Goods)
        {
            double share = town.City.Wanted.Get(g, 0) > 0 ? town.City.Supplied.Get(g, 0) / town.City.Wanted.Get(g, 0) : 1;
            // A good no one buys yet counts by what the market holds for a week of it.
            if (town.City.Wanted.Get(g, 0) <= 0) share = Math.Min(1, town.Market.Stock(g) / (rate * n * 7));
            sum += share;
            needs++;
        }
        foreach (string s in next.Services)
        {
            sum += CoverageOf(town, s);
            needs++;
        }
        return Math.Clamp((needs == 0 ? 1 : sum / needs) + Decor.Comfort(town, house), 0, 1);
    }

    /// <summary>Why this house can't grow now, or null when it can.</summary>
    public static string? UpgradeBlocked(Town town, Building house)
    {
        var h = Housing(town, house.Def.Id);
        if (h == null || h.Next == "") return "it is as grand as houses get";
        var next = town.Content.Building(h.Next);
        if (Locked(town, next) is { } why) return why;
        if (!Allows(town, next, town.Lot(house.LotId))) return $"a {next.Name.ToLowerInvariant()} can't stand in the {WardOf(town, town.Lot(house.LotId)).Name}";
        // The town's own houses are let to those who can't build: they grow roomier, never grand.
        if (house.OwnerId == null && next.Wards.Count > 0) return "the town lets its houses to working folk, not as manors";
        var nh = Housing(town, next.Id);
        if (nh != null && Prospect(town, house, nh) < town.Content.City.UpgradeAt) return "the town can't yet give a bigger house what it wants";
        return null;
    }

    public static double UpgradeCost(Town town, BuildingDef next) =>
        Js.Round(next.Cost * 0.6 + town.Market.Quote(Good.Timber, next.Timber) + town.Market.Quote(Good.Stone, next.Stone));

    private static void TryUpgrade(Town town, Building house)
    {
        if (UpgradeBlocked(town, house) != null) return;
        var next = town.Content.Building(Housing(town, house.Def.Id)!.Next);
        double cost = UpgradeCost(town, next);
        double labor = next.BuildHours * 0.5 * Brain.ConstructionWage;
        if (house.OwnerId is int oid)
        {
            var owner = town.Agent(oid);
            if (owner.Gold < cost + labor + 20) return;
            owner.Gold -= cost + labor;
        }
        else
        {
            // The town grows its own charter houses when the treasury can spare it.
            if (town.Treasury < cost + labor + 300) return;
            town.Book(TreasuryAccount.CharterHouses, -(cost + labor));
        }
        town.Market.Buy(Good.Timber, next.Timber);
        town.Market.Buy(Good.Stone, next.Stone);
        string old = house.Def.Name.ToLowerInvariant();
        house.Def = next;
        house.BuildProgress = next.BuildHours * 0.5;
        house.Opened = false;
        house.LaborBudget = labor;
        house.UpDays = 0;
        house.Contentment = 1;
        town.Log(
            LedgerCategory.Construction,
            Importance.Routine,
            $"{Cap(town.OwnerName(house.OwnerId))} raises {house.Name} from a {old} to a {next.Name.ToLowerInvariant()}.",
            house.OwnerId is int o ? [o] : []);
    }

    private static void Shrink(Town town, Building house)
    {
        var prev = Previous(town, house.Def.Id);
        house.DownDays = 0;
        if (prev == null) return;
        var def = town.Content.Building(prev.Id);
        var was = Housing(town, house.Def.Id)!;
        house.Def = def;
        // Families beyond the beds left must find somewhere else.
        var out_ = new List<int>();
        while (house.Residents.Count > def.Housing)
        {
            int id = house.Residents[^1];
            house.Residents.RemoveAt(house.Residents.Count - 1);
            town.Agent(id).HomeId = null;
            out_.Add(id);
        }
        string wants = Wants(town, was);
        town.Log(
            LedgerCategory.Hardship,
            Importance.Notable,
            $"{Cap(house.Name)} falls back to a {def.Name.ToLowerInvariant()}{(wants != "" ? $" for want of {wants}" : "")}{(out_.Count > 0 ? $", and {out_.Count} {(out_.Count == 1 ? "lodger has" : "lodgers have")} to find other beds" : "")}.",
            out_);
    }

    /// <summary>What the town fails to give a kind of house, in a few words.</summary>
    public static string Wants(Town town, HousingDef h)
    {
        var missing = new List<string>();
        foreach (var (g, _) in h.Goods)
        {
            double w = town.City.Wanted.Get(g, 0);
            if (w > 0 && town.City.Supplied.Get(g, 0) / w < 0.6) missing.Add(g.ToString().ToLowerInvariant());
        }
        foreach (string s in h.Services)
            if (CoverageOf(town, s) < 0.6) missing.Add(ServiceName(town, s));
        return Expedition.ListNames(missing);
    }

    private static void ReadyNotice(Town town)
    {
        var next = NextTier(town);
        if (next == null || town.City.ReadyNoticeTier == town.City.Tier + 1) return;
        if (Missing(town).Count > 0) return;
        town.City.ReadyNoticeTier = town.City.Tier + 1;
        town.Log(LedgerCategory.Town, Importance.Notable, $"{town.Name} has all it needs to be chartered a {next.Name.ToLowerInvariant()}. The council awaits the mayor's seal.");
    }

    // The stand-in mayor -----------------------------------------------------------------------

    /// <summary>Claim charters as soon as they are earned, and lay out the next ward when land runs short.</summary>
    private static void Steward(Town town)
    {
        var next = NextTier(town);
        if (next != null && Missing(town).Count == 0 && town.Treasury >= next.Cost + 150) RaiseCharter(town);

        // Lay out the next ward when land runs short, or when the treasury is flush and a ward is ready.
        int vacant = Land.OpenGround(town);
        bool flush = town.Treasury > 3000;
        if (vacant >= 24 * Land.Res * Land.Res && !flush) return;
        var ward = town.Content.City.Wards
            .Where(w => WardBlocked(town, w.Id) == null && town.Treasury >= w.Cost + (flush ? 2500 : 200))
            .OrderBy(w => w.Ring)
            .ThenBy(w => w.Cost)
            .ThenBy(w => w.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ward != null) OpenWard(town, ward.Id);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
