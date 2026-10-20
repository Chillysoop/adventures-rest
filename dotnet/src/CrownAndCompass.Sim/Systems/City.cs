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
/// A ward of the town (GDD rule C3): the old town at the heart, four wards inside the city wall, and four outer wards beyond
/// it. Wards are not laid out ahead: the land round the old town is split into a ring of quarters inside the wall and one
/// outside, and a quarter becomes a ward once enough is built in it, the ward of its ring that best suits what was built
/// there first (<see cref="Draws"/>, <see cref="City.Form"/>). Until then it is unsettled ground (city.json "unsettled").
/// </summary>
public sealed record WardDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>0 is the old town, 1 the ring inside the city wall, 2 the outer ring.</summary>
    public int Ring { get; init; }

    /// <summary>The quarter (north, east, south or west of the hall) the ward stood in when wards were laid out by hand: where saves from then put it.</summary>
    public string Side { get; init; } = "";

    /// <summary>The tier the town must reach before a quarter can grow into this ward.</summary>
    public string Tier { get; init; } = "";

    /// <summary>
    /// What draws a quarter to become this ward: a weight per finished building there, by its zone (Residential, Commercial,
    /// Industry, Farmland), by its building id, "Holy" for temples and shrines, or "Fine" for houses grown past the cottage.
    /// </summary>
    public IReadOnlyDictionary<string, double> Draws { get; init; } = new Dictionary<string, double>();

    /// <summary>Unsettled ground: a quarter that has not yet become a ward.</summary>
    public bool Unsettled { get; init; }
    public double Rent { get; init; } = 1;
    public double Land { get; init; } = 1;

    /// <summary>Mood its residents carry every hour, for better or worse.</summary>
    public double Mood { get; init; }

    /// <summary>The zone it lends itself to, which the stand-in mayor paints there first.</summary>
    public Zone Favours { get; init; } = Zone.None;

    /// <summary>Outside every wall: cheap, rowdy, and raided first (GDD rule C4).</summary>
    public bool Outer { get; init; }

    /// <summary>The densest the stand-in mayor zones it (the lords of Castle Hill keep their gardens, and the outer wards their sprawl).</summary>
    public int Densest { get; init; } = 2;
    public string Blurb { get; init; } = "";
}

/// <summary>
/// How tightly a zone may be built (city.json "densities"): low land keeps cottages with gardens between,
/// medium lets houses rise to stone townhouses, high packs tall houses wall to wall as in an old walled core.
/// </summary>
public sealed record DensityDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>The charter that unlocks it.</summary>
    public string Tier { get; init; } = "";

    /// <summary>Cells of open ground kept round each building: 0 builds wall to wall.</summary>
    public int Lane { get; init; } = 1;

    /// <summary>Land price on it, against low land: what the right to build high is worth.</summary>
    public double Land { get; init; } = 1;

    /// <summary>Mood its residents carry for the crowding.</summary>
    public double Mood { get; init; }
    public string Blurb { get; init; } = "";
}

/// <summary>
/// How far from the hall the stand-in mayor zones a density (city.json "sprawl"): out to <see cref="Reach"/> tiles
/// for every square root of a resident, so the dense core spreads outward as the town grows.
/// </summary>
public sealed record SprawlStep
{
    public int Density { get; init; }
    public double Reach { get; init; }
}

/// <summary>What the families of one kind of house want, and what it grows into.</summary>
public sealed record HousingDef
{
    public required string Id { get; init; }

    /// <summary>The house it grows into once its families are content and the tier allows.</summary>
    public string Next { get; init; } = "";

    /// <summary>The house it grows into instead on high-density land, when that is a different one.</summary>
    public string Dense { get; init; } = "";

    /// <summary>Goods each resident buys a day.</summary>
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Goods { get; init; } = [];

    /// <summary>Services the town must offer enough of (tavern, shrine, market, bath, theatre, learning).</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>Hearth tax each resident pays the treasury a day at the town's usual tax rate; grander houses pay more.</summary>
    public double Tax { get; init; }

    /// <summary>Services a house must have within <see cref="CityRules.NearReach"/> tiles before it can grow into this one.</summary>
    public IReadOnlyList<string> Near { get; init; } = [];

    /// <summary>Kinds of food the town must grow or catch (farms, orchards, fisheries...) before a house grows into this one.</summary>
    public int Foods { get; init; }

    /// <summary>The least appeal (decorations, gardens) a house's street must have before it grows into this one.</summary>
    public double Beauty { get; init; }
}

/// <summary>Desire paths (city.json "paths"): wear per walk, how much a day's walks can wear, what's left of wear by the next day, and the wear that makes a lane.</summary>
public sealed record PathRules
{
    public double WearPerTrip { get; init; } = 0.008;
    public int MostTrips { get; init; } = 20;
    public double Fade { get; init; } = 0.97;
    public double Lay { get; init; } = 1.2;
}

/// <summary>
/// How painted zones fill in (city.json "growth"), as scores where lower is built first: <see cref="Reach"/> per tile a spot
/// lies past the built town's edge, <see cref="OffStreet"/> for a spot not fronting a street, <see cref="Yard"/> per tile
/// set back from one, <see cref="Shuffle"/> of chance, and <see cref="Hall"/> per tile from the hall. <see cref="Turn"/> is
/// how far (radians) a grown building may stand off square to what it faces.
/// </summary>
public sealed record GrowthRules
{
    public double Reach { get; init; } = 1;
    public double OffStreet { get; init; } = 0.5;
    public double Yard { get; init; } = 1;
    public double Shuffle { get; init; } = 0.8;
    public double Hall { get; init; } = 0.05;
    public double Turn { get; init; } = 0.3;
}

/// <summary>
/// The strip along each wall line (city.json "wallStrip") that nothing is built on, in tiles inside and outside the line:
/// room for the widest the wall grows (stone, towers, the barracks behind it, the moat and its banks), so a palisade can
/// be raised and upgraded in place without a building in its way.
/// </summary>
public sealed record WallStripRules
{
    public double Inside { get; init; } = 0.9;
    public double Outside { get; init; } = 1.6;
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

    /// <summary>The unsettled ground of each ring past the old town (ring 1 first): a quarter until it becomes a ward.</summary>
    public IReadOnlyList<WardDef> Unsettled { get; init; } = [];

    /// <summary>The tier that opens each ring of the town to zoning, the old town first.</summary>
    public IReadOnlyList<string> Rings { get; init; } = [];

    /// <summary>Finished buildings a quarter needs before it becomes a ward.</summary>
    public int Founding { get; init; } = 4;

    /// <summary>How well, per building, a quarter's buildings must draw a ward for the quarter to become it (<see cref="WardDef.Draws"/>).</summary>
    public double Fit { get; init; } = 0.2;
    public IReadOnlyList<HousingDef> Housing { get; init; } = [];
    public IReadOnlyList<DensityDef> Densities { get; init; } = [];
    public IReadOnlyList<SprawlStep> Sprawl { get; init; } = [];

    /// <summary>Each service id as a sentence names it ("a bathhouse").</summary>
    public IReadOnlyDictionary<string, string> Services { get; init; } = new Dictionary<string, string>();

    /// <summary>How feet wear paths to buildings off the streets.</summary>
    public PathRules Paths { get; init; } = new();

    /// <summary>How painted zones fill in from the built town outward.</summary>
    public GrowthRules Growth { get; init; } = new();

    /// <summary>How many builds go up at once, and how materials reach them.</summary>
    public ConstructionRules Construction { get; init; } = new();

    /// <summary>The ground kept clear along each wall line for the wall's widest works.</summary>
    public WallStripRules WallStrip { get; init; } = new();

    /// <summary>Days a house must be content (at UpgradeAt or better) before it grows.</summary>
    public int UpgradeDays { get; init; } = 6;

    /// <summary>Days a house can go wanting (below DowngradeBelow) before it falls back a step.</summary>
    public int DowngradeDays { get; init; } = 10;
    public double UpgradeAt { get; init; } = 0.85;

    /// <summary>How near, in tiles, the services a grander house needs must stand (<see cref="HousingDef.Near"/>).</summary>
    public double NearReach { get; init; } = 8;
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

    /// <summary>Wards formed so far, in the order they formed.</summary>
    public List<string> OpenWards { get; } = ["old_town"];

    /// <summary>The ward each quarter has become ("ring:side", e.g. "1:west"), once it has (<see cref="City.Form"/>).</summary>
    public OrderedMap<string, string> Sectors { get; set; } = new();

    /// <summary>How each ward feels about the mayor, -100 to 100 (<see cref="Wards"/>).</summary>
    public OrderedMap<string, double> Feeling { get; set; } = new();

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
/// its institutions stand; wards that grow out of what is built round the old town; houses that grow a storey when their
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
    public static int HeroCap(Town town) => Tier(town).Heroes + GrandWorks.Heroes(town) + Institutions.Heroes(town) + Research.Heroes(town);

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
            if (!Has(town, id)) lines.Add($"{Agent.Article(town.Content.Building(id).Name)} {town.Content.Building(id).Name.ToLowerInvariant()}");
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
        town.Content.City.Wards.FirstOrDefault(w => w.Id == id) ?? town.Content.City.Unsettled.FirstOrDefault(w => w.Id == id)
        ?? throw new KeyNotFoundException($"Unknown ward {id}");

    /// <summary>The ward a lot lies in: the ring by distance from the hall, the side by direction.</summary>
    public static WardDef WardOf(Town town, Lot lot) => WardOf(town, lot.X, lot.Y);

    /// <summary>The ward a point of the town (tiles from its corner) lies in: its quarter's ward, or the ring's unsettled ground.</summary>
    public static WardDef WardOf(Town town, double x, double y)
    {
        var c = town.Content.City;
        int ring = Ring(town, x, y);
        if (ring == 0) return c.Wards.First(w => w.Ring == 0);
        if (town.City.Sectors.TryGetValue(Sector(ring, Side(town, x, y)), out var id)) return Ward(town, id);
        return c.Unsettled.FirstOrDefault(w => w.Ring == ring) ?? c.Wards.First(w => w.Ring == 0);
    }

    /// <summary>A quarter's key: its ring and its side of the hall.</summary>
    public static string Sector(int ring, string side) => $"{ring}:{side}";

    /// <summary>The quarter a ward has formed in, or null (the old town's is the middle).</summary>
    public static (int Ring, string Side)? SectorOf(Town town, string wardId)
    {
        foreach (var (key, id) in town.City.Sectors)
            if (id == wardId) return (int.Parse(key[..key.IndexOf(':')]), key[(key.IndexOf(':') + 1)..]);
        return null;
    }

    /// <summary>A ward's name as it reads in a sentence: "the Trades Ward", but "Castle Hill".</summary>
    public static string The(WardDef w) => w.Name.EndsWith("Ward", StringComparison.Ordinal) ? $"the {w.Name}" : w.Name;

    /// <summary>Tiles per ring band: the old town reaches about 8 tiles from the hall, each ring about 8 more.</summary>
    public const int Band = 8;

    /// <summary>
    /// How far from the hall ring <paramref name="ring"/> ends at a heading (radians from east, south positive), in tiles:
    /// a square-shouldered, round-cornered loop with a slow wobble from the town's seed, so no border runs ruled-straight.
    /// </summary>
    public static double Edge(Town town, int ring, double angle) => Edge(town.Seed, ring, angle);

    /// <summary>
    /// How far from the hall the wall round ring <paramref name="ring"/> runs at a heading: the palisade and the stone wall round
    /// the old town (ring 0), the city wall (ring 1). It stands just outside the ring, far enough that the strip kept clear
    /// for it (<see cref="OnWallStrip"/>) begins at the ring's edge and takes none of the land inside.
    /// </summary>
    public static double Wall(Town town, int ring, double angle) => Edge(town, ring, angle) + town.Content.City.WallStrip.Inside;

    /// <summary>The rings walls are raised round, innermost first.</summary>
    public const int WallRings = 2;

    /// <summary>Whether a point lies on the strip kept clear along a wall line (<see cref="WallStripRules"/>), walls built or not.</summary>
    public static bool OnWallStrip(Town town, double x, double y)
    {
        var strip = town.Content.City.WallStrip;
        double dx = x - Town.Center, dy = y - Town.Center, dist = Math.Sqrt(dx * dx + dy * dy), a = Math.Atan2(dy, dx);
        for (int ring = 0; ring < WallRings; ring++)
        {
            double off = dist - Wall(town, ring, a);
            if (off >= -strip.Inside && off <= strip.Outside) return true;
        }
        return false;
    }

    public static double Edge(int seed, int ring, double angle)
    {
        double c = Math.Cos(angle), s = Math.Sin(angle);
        double r = Band * (ring + 1) / Math.Pow(Math.Pow(Math.Abs(c), 4) + Math.Pow(Math.Abs(s), 4), 0.25);
        return r * (1 + Wobble(seed, ring, angle) * 0.07);
    }

    /// <summary>
    /// The heading of the border between two sides (0 between east and south, then round: south|west, west|north, north|east)
    /// at a distance from the hall: near the diagonals, leaning a little with the seed and curving gently outward.
    /// </summary>
    public static double Split(Town town, int k, double dist) => Split(town.Seed, k, dist);

    public static double Split(int seed, int k, double dist) =>
        Math.PI / 4 + k * Math.PI / 2 + (Hash(seed, 40 + k) - 0.5) * 0.3 + Math.Sin(dist / 5 + Hash(seed, 50 + k) * 6.3) * 0.08;

    /// <summary>A slow wave round a ring, from about -1 to 1.</summary>
    private static double Wobble(int seed, int ring, double angle)
    {
        double w = 0;
        foreach (var (k, a) in new[] { (2, 0.45), (3, 0.35), (5, 0.2) })
            w += a * Math.Sin(k * angle + Hash(seed, ring * 7 + k) * Math.PI * 2);
        return w;
    }

    private static double Hash(int seed, int i)
    {
        uint h = (uint)seed * 2654435761u ^ (uint)(i + 1) * 2246822519u;
        h ^= h >> 15;
        h *= 2654435761u;
        h ^= h >> 13;
        return (h & 0xffffff) / (double)0x1000000;
    }

    public static int Ring(Town town, Lot lot) => Ring(town, lot.X, lot.Y);

    public static int Ring(Town town, double x, double y)
    {
        double dx = x - Town.Center, dy = y - Town.Center, d = Math.Sqrt(dx * dx + dy * dy), a = Math.Atan2(dy, dx);
        return d < Edge(town, 0, a) ? 0 : d < Edge(town, 1, a) ? 1 : 2;
    }

    public static string Side(Town town, Lot lot) => Side(town, lot.X, lot.Y);

    private static readonly string[] Sides = ["south", "west", "north", "east"];

    public static string Side(Town town, double x, double y)
    {
        double dx = x - Town.Center, dy = y - Town.Center, d = Math.Sqrt(dx * dx + dy * dy);
        double a = Math.Atan2(dy, dx);
        // Round from the east|south border: the side is the one whose two borders the heading lies between.
        double from = Split(town, 0, d);
        double t = a - from;
        while (t < 0) t += Math.PI * 2;
        while (t >= Math.PI * 2) t -= Math.PI * 2;
        for (int k = 1; k < 4; k++)
        {
            double b = Split(town, k, d) - from;
            if (t < b) return Sides[k - 1];
        }
        return "east";
    }

    public static bool IsOpen(Town town, Lot lot) => IsOpen(town, lot.X, lot.Y);

    public static bool IsOpen(Town town, double x, double y) => RingOpen(town, Ring(town, x, y));

    /// <summary>Whether the town's charter reaches a ring, so its ground can be zoned and built on.</summary>
    public static bool RingOpen(Town town, int ring)
    {
        var rings = town.Content.City.Rings;
        return ring == 0 || (ring < rings.Count && Reached(town, rings[ring]));
    }

    /// <summary>The charter that opens a ring to zoning.</summary>
    public static string RingTier(Town town, int ring)
    {
        var rings = town.Content.City.Rings;
        return ring < rings.Count ? rings[ring] : rings[^1];
    }

    private static readonly string[] Quarters = ["south", "west", "north", "east"];

    /// <summary>
    /// Daily: a quarter with enough finished buildings in it becomes a ward, the one of its ring (not yet formed elsewhere, its
    /// tier reached) that what was built there draws most (<see cref="WardDef.Draws"/>). A quarter whose buildings draw none of
    /// the wards open to it waits, unsettled, until one is.
    /// </summary>
    public static void Form(Town town)
    {
        var c = town.Content.City;
        int need = c.Founding;
        Dictionary<string, List<Building>>? bySector = null;
        for (int ring = 1; ring <= WallRings && RingOpen(town, ring); ring++)
            foreach (string side in Quarters)
            {
                string key = Sector(ring, side);
                if (town.City.Sectors.ContainsKey(key)) continue;
                bySector ??= Built(town);
                if (!bySector.TryGetValue(key, out var here) || here.Count < need) continue;
                var best = c.Wards
                    .Where(w => w.Ring == ring && !town.City.OpenWards.Contains(w.Id) && Reached(town, w.Tier))
                    .Select(w => (w, score: here.Sum(b => Draw(town, w, b))))
                    .Where(x => x.score >= c.Fit * here.Count)
                    .OrderByDescending(x => x.score)
                    .ThenBy(x => x.w.Id, StringComparer.Ordinal)
                    .Select(x => x.w)
                    .FirstOrDefault();
                if (best == null) continue;
                town.City.Sectors[key] = best.Id;
                town.City.OpenWards.Add(best.Id);
                // The plan's ward per cell, and searches of the old plan, no longer hold.
                town.Plan.Wards = null;
                town.Plan.Changed();
                town.Log(LedgerCategory.Town, Importance.Major, $"The {Describe(town, best, here)} {side} of the {(ring == 1 ? "old town" : "walls")} have grown into a quarter of their own: {The(best)}. {best.Blurb}");
            }
    }

    /// <summary>The finished buildings in each unsettled quarter, by its key.</summary>
    private static Dictionary<string, List<Building>> Built(Town town)
    {
        var map = new Dictionary<string, List<Building>>();
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Civic && !b.Def.Holy) continue;
            var lot = town.Lot(b.LotId);
            int ring = Ring(town, lot.X, lot.Y);
            if (ring == 0) continue;
            string key = Sector(ring, Side(town, lot.X, lot.Y));
            if (!map.TryGetValue(key, out var list)) map[key] = list = [];
            list.Add(b);
        }
        return map;
    }

    /// <summary>How much one building draws its quarter toward a ward.</summary>
    public static double Draw(Town town, WardDef w, Building b)
    {
        double d = w.Draws.TryGetValue(b.Def.Zone.ToString(), out double z) ? z : 0;
        if (w.Draws.TryGetValue(b.Def.Id, out double own)) d += own;
        if (b.Def.Holy && w.Draws.TryGetValue("Holy", out double holy)) d += holy;
        if (b.Def.Housing > 0 && b.Def.Id != "house" && w.Draws.TryGetValue("Fine", out double fine)) d += fine;
        return d;
    }

    /// <summary>What drew a quarter to its ward, for the news of it: "shops and homes", "workshops and farms".</summary>
    private static string Describe(Town town, WardDef w, List<Building> here)
    {
        var by = new Dictionary<string, double>();
        foreach (var b in here)
        {
            void Add(string what, string key)
            {
                if (w.Draws.TryGetValue(key, out double v)) by[what] = by.GetValueOrDefault(what) + v;
            }
            Add(b.Def.Zone switch { Zone.Commercial => "shops", Zone.Industry => "workshops", Zone.Farmland => "farms", Zone.Residential => "homes", _ => "halls" }, b.Def.Zone.ToString());
            Add(b.Def.Name.ToLowerInvariant() + "s", b.Def.Id);
            if (b.Def.Holy) Add("shrines", "Holy");
            if (b.Def.Housing > 0 && b.Def.Id != "house") Add("fine houses", "Fine");
        }
        var top = by.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Take(2).Select(x => x.Key).ToList();
        return string.Join(" and ", top);
    }

    /// <summary>Saves from before wards formed on their own: each ward laid out takes the quarter it stood in then.</summary>
    public static void Migrate(Town town)
    {
        if (town.City.Sectors.Count > 0) return;
        foreach (var id in town.City.OpenWards)
            if (town.Content.City.Wards.FirstOrDefault(w => w.Id == id) is { Ring: > 0, Side: not "" } w)
                town.City.Sectors[Sector(w.Ring, w.Side)] = w.Id;
        town.Plan.Wards = null;
    }

    /// <summary>Whether a building may stand on this lot: its ward is open and the building allows it.</summary>
    public static bool Allows(Town town, BuildingDef d, Lot lot) => Allows(town, d, lot.X, lot.Y);

    public static bool Allows(Town town, BuildingDef d, double x, double y) => AllowsWard(town, d, x, y) && Density.Allows(town, d, x, y);

    /// <summary>As <see cref="Allows(Town, BuildingDef, double, double)"/>, whatever the density the land is zoned for.</summary>
    public static bool AllowsWard(Town town, BuildingDef d, double x, double y)
    {
        var w = WardOf(town, x, y);
        if (!RingOpen(town, w.Ring)) return false;
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
            return $"needs the {string.Join(" or the ", d.Wards.Select(w => Ward(town, w).Name))} to grow up";
        return null;
    }

    // Houses and their wants -------------------------------------------------------------------

    public static HousingDef? Housing(Town town, string buildingId) => town.Content.City.Housing.FirstOrDefault(h => h.Id == buildingId);

    /// <summary>The house a level down, the one this kind grew from.</summary>
    public static HousingDef? Previous(Town town, string buildingId) => town.Content.City.Housing.FirstOrDefault(h => h.Next == buildingId || h.Dense == buildingId);

    /// <summary>The house this one grows into where it stands: the dense kind on land zoned dense enough for it.</summary>
    public static string NextFor(Town town, Building house, HousingDef h) =>
        h.Dense != "" && Density.At(town, town.Lot(house.LotId)) >= town.Content.Building(h.Dense).Density ? h.Dense : h.Next;

    /// <summary>Mood a resident carries from home: the ward's character, and how content the household is.</summary>
    public static double HomeMood(Town town, Agent a)
    {
        if (a.HomeId == null) return 0;
        var home = town.Building(a.HomeId.Value);
        var ward = WardOf(town, town.Lot(home.LotId));
        double mood = ward.Mood + Wards.Mood(town, ward.Id) + Clockwork.Mood(town) + Density.Mood(town, home);
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
    /// went wanting, let quarters grow into wards, and let the stand-in mayor claim charters.
    /// </summary>
    public static void Daily(Town town, bool autoMayor)
    {
        UpdateCoverage(town);
        HearthTax(town);
        Households(town);
        Takings(town);
        Grow(town);
        ReadyNotice(town);
        Form(town);
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
        double grand = GrandWorks.Comfort(town);
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
            house.Contentment = Math.Clamp((needs == 0 ? 1 : sum / needs) + Decor.Comfort(town, house) + Wards.Comfort(town, house) + grand, 0, 1);
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
        return Math.Clamp((needs == 0 ? 1 : sum / needs) + Decor.Comfort(town, house) + Wards.Comfort(town, house) + GrandWorks.Comfort(town), 0, 1);
    }

    /// <summary>Why this house can't grow now, or null when it can.</summary>
    public static string? UpgradeBlocked(Town town, Building house)
    {
        var h = Housing(town, house.Def.Id);
        string nextId = h == null ? "" : NextFor(town, house, h);
        if (h == null || nextId == "") return "it is as grand as houses get";
        var next = town.Content.Building(nextId);
        if (Locked(town, next) is { } why) return why;
        var lot = town.Lot(house.LotId);
        if (!AllowsWard(town, next, lot.X, lot.Y)) return $"{Agent.Article(next.Name)} {next.Name.ToLowerInvariant()} can't stand in the {WardOf(town, lot).Name}";
        if (!Density.Allows(town, next, lot.X, lot.Y)) return $"{Agent.Article(next.Name)} {next.Name.ToLowerInvariant()} needs land zoned {Density.Def(town, next.Density).Name.ToLowerInvariant()} density";
        // The town's own houses are let to those who can't build: they grow roomier, never grand.
        if (house.OwnerId == null && next.Wards.Count > 0) return "the town lets its houses to working folk, not as manors";
        var nh = Housing(town, next.Id);
        if (nh != null && NeedsBlocked(town, house, nh) is { } need) return need;
        if (nh != null && Prospect(town, house, nh) < town.Content.City.UpgradeAt) return "the town can't yet give a bigger house what it wants";
        return null;
    }

    /// <summary>What the house's own street lacks for the next storey: services near it, food variety in town, some beauty; or null.</summary>
    public static string? NeedsBlocked(Town town, Building house, HousingDef next)
    {
        double reach = town.Content.City.NearReach;
        var lot = town.Lot(house.LotId);
        foreach (string s in next.Near)
            if (!Near(town, lot, s, reach)) return $"needs {Agent.Article(ServiceName(s))} {ServiceName(s)} within {Js.Str(reach)} tiles";
        if (next.Foods > 0 && FoodKinds(town) is int kinds && kinds < next.Foods)
            return $"needs {next.Foods} kinds of food grown in town (there {(kinds == 1 ? "is" : "are")} {kinds}): farms, orchards, root fields, fisheries";
        if (next.Beauty > 0 && Decor.AppealOf(town, house) < next.Beauty) return "needs something pleasant on its street: trees, a fountain, a garden";
        return null;
    }

    /// <summary>Whether a standing building giving the service is within reach of the lot.</summary>
    public static bool Near(Town town, Lot lot, string service, double reach)
    {
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || !b.Def.Serves.ContainsKey(service)) continue;
            var l = town.Lot(b.LotId);
            if ((l.X - lot.X) * (l.X - lot.X) + (l.Y - lot.Y) * (l.Y - lot.Y) <= reach * reach) return true;
        }
        return false;
    }

    /// <summary>Kinds of food the town grows or catches: each sort of farm, orchard or fishery standing counts once.</summary>
    public static int FoodKinds(Town town)
    {
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var b in town.Buildings)
            if (b.Complete && b.Def.Produces.Any(p => p.Key == Good.Food)) kinds.Add(b.Def.Id);
        return kinds.Count;
    }

    private static string ServiceName(string service) => service switch
    {
        "shrine" => "shrine",
        "market" => "market",
        "tavern" => "tavern",
        "bath" => "bathhouse",
        "theatre" => "theatre",
        "learning" => "library",
        _ => service,
    };

    public static double UpgradeCost(Town town, BuildingDef next) =>
        Js.Round(next.Cost * 0.6 + town.Market.Quote(Good.Timber, next.Timber) + town.Market.Quote(Good.Stone, next.Stone));

    private static void TryUpgrade(Town town, Building house)
    {
        if (UpgradeBlocked(town, house) != null) return;
        var next = town.Content.Building(NextFor(town, house, Housing(town, house.Def.Id)!));
        double cost = UpgradeCost(town, next);
        double labor = Construction.Hours(town, next) * 0.5 * Brain.ConstructionWage;
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
        // The new storey's timber and stone are carried up before it rises.
        house.Hauled = Construction.Start(next, false) == 1 ? 1 : 0.5;
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

    /// <summary>Claim charters as soon as they are earned (wards form on their own, <see cref="Form"/>).</summary>
    private static void Steward(Town town)
    {
        var next = NextTier(town);
        if (next != null && Missing(town).Count == 0 && town.Treasury >= next.Cost + 150) RaiseCharter(town);
        Density.StandIn(town);
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
