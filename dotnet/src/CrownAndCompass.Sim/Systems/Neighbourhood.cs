namespace CrownAndCompass.Sim.Systems;

/// <summary>What one kind of neighbour does to the ground round it: its land price, rent and the mood of those living by it.</summary>
public sealed record NeighbourLean
{
    public double Land { get; init; }
    public double Rent { get; init; }
    public double Mood { get; init; }
}

/// <summary>
/// Shops as neighbours (city.json "neighbourhood.shops"): a few close by are a blessing, a whole market at the door is noise,
/// and none within walking reach is a long trudge for every loaf.
/// </summary>
public sealed record ShopRules
{
    /// <summary>Tiles a shop's bustle reaches, fading out.</summary>
    public double Near { get; init; } = 3.5;

    /// <summary>Shops close by (weighted) up to which each one helps; past it each one is noise.</summary>
    public double Good { get; init; } = 2;
    public double Land { get; init; }
    public double Rent { get; init; }
    public double Mood { get; init; }

    /// <summary>Mood lost per shop past <see cref="Good"/>.</summary>
    public double Noise { get; init; }

    /// <summary>Tiles a household will walk to a shop; past it, once the town has shops, the <see cref="Far"/> lean.</summary>
    public double Reach { get; init; } = 10;
    public NeighbourLean Far { get; init; } = new();
}

/// <summary>Arcane works standing together grow a scholars' quarter that adds to the library's lore (city.json "neighbourhood.scholars").</summary>
public sealed record ScholarRules
{
    public IReadOnlyList<string> Ids { get; init; } = [];

    /// <summary>Tiles apart they may stand and still count as one quarter.</summary>
    public double Reach { get; init; } = 6;

    /// <summary>Works it takes, and the lore a day the quarter adds.</summary>
    public int Least { get; init; } = 3;
    public double Lore { get; init; } = 1.5;
    public string Name { get; init; } = "the scholars' quarter";
}

/// <summary>How neighbourhoods form and take their names (city.json "neighbourhood.quarters").</summary>
public sealed record QuarterRules
{
    /// <summary>Tiles from its heart a neighbourhood takes in buildings (each goes to the nearest heart).</summary>
    public double Radius { get; init; } = 9;

    /// <summary>Tiles round an unclaimed building that count toward a new neighbourhood, and the buildings it takes.</summary>
    public double Spacing { get; init; } = 8;
    public int Founding { get; init; } = 5;

    /// <summary>The first neighbourhood's name.</summary>
    public string Heart { get; init; } = "Old Town";

    /// <summary>Land price (multiplier) over which homes make a rich neighbourhood, and under which a poor one.</summary>
    public double Rich { get; init; } = 1.25;
    public double Poor { get; init; } = 0.85;

    /// <summary>Names by character: market, workshops, farms, holy, rich, poor, homes, scholars, river.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Names { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
}

/// <summary>How the buildings round a spot set its character (city.json "neighbourhood").</summary>
public sealed record NeighbourhoodRules
{
    /// <summary>Tiles a building's lean reaches, fading to nothing at the edge.</summary>
    public double Reach { get; init; } = 3;

    /// <summary>
    /// The lean of each kind of neighbour at full strength: by zone ("Industry", "Farmland"), "Holy" for temples and
    /// shrines, "Fine" for houses grown past the cottage and "Poor" for tenements. Shops have their own rules.
    /// </summary>
    public IReadOnlyDictionary<string, NeighbourLean> Kinds { get; init; } = new Dictionary<string, NeighbourLean>();
    public ShopRules Shops { get; init; } = new();

    /// <summary>The least and most land price and rent can be by their neighbours (multipliers), and mood (added).</summary>
    public double[] Land { get; init; } = [0.5, 1.8];
    public double[] Rent { get; init; } = [0.6, 1.6];
    public double[] Mood { get; init; } = [-5, 4];

    /// <summary>The densest the stand-in mayor zones land past the ring the city wall closes.</summary>
    public int OuterDensest { get; init; } = 1;

    /// <summary>Tiles from the river a building that needs water may stand (a town on the sea has water everywhere).</summary>
    public double Water { get; init; } = 30;

    public ScholarRules Scholars { get; init; } = new();
    public QuarterRules Quarters { get; init; } = new();
}

/// <summary>
/// What a building needs of the ground round it (buildings.json "site"): dear land for a manor, cheap for a tenement,
/// water for a fishery, holy ground for a cathedral.
/// </summary>
public sealed record SiteRule
{
    /// <summary>Land price (multiplier, <see cref="Neighbourhood.Land(Town, double, double)"/>) it needs at least, or at most (0 for none).</summary>
    public double LandAtLeast { get; init; }
    public double LandAtMost { get; init; }

    /// <summary>Stands only near the river or on the sea (<see cref="NeighbourhoodRules.Water"/>).</summary>
    public bool Water { get; init; }

    /// <summary>Neighbours it needs within <see cref="NearReach"/> tiles, by kind (as <see cref="Neighbourhood.KindsOf"/> names them, or a building id).</summary>
    public IReadOnlyDictionary<string, int> Near { get; init; } = new Dictionary<string, int>();
    public double NearReach { get; init; } = 8;
}

/// <summary>A neighbourhood of the town: where it formed, what it was named, and when.</summary>
public sealed class Quarter
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>What it was when it formed (market, workshops, homes...), which named it.</summary>
    public string Character { get; set; } = "";
    public int Day { get; set; }
}

/// <summary>A neighbourhood's figures for the map card and the City window.</summary>
public sealed record QuarterStats(Quarter Quarter, string Character, int Buildings, int Homes, int Residents, int Shops, int Workshops, double Rent, double Land, double Mood);

/// <summary>
/// Neighbourhoods. No districts are drawn ahead or dictate what may be built: buildings go wherever their zone is painted,
/// and what stands round a spot gives it its character, after the way city builders price land. Homes with a few shops
/// close by pay more and are cheerier, a whole market at the door is noise, and a town whose shops are all far away
/// leaves the rest trudging; nobody wants the workshops' smoke next door; temples and fine houses make a street dear,
/// tenements make it cheap. So rich and poor streets and clusters of like with like grow on their own.
/// Where buildings cluster a neighbourhood forms and takes a name from what it is (<see cref="Quarter"/>).
/// The field is a pure function of the buildings standing, kept per plan cell and drawn again when they change.
/// </summary>
public static class Neighbourhood
{
    public static NeighbourhoodRules Rules(Town town) => town.Content.City.Neighbourhood;

    /// <summary>The neighbour kinds a building counts as.</summary>
    public static IEnumerable<string> KindsOf(BuildingDef d)
    {
        if (d.Zone is Zone.Commercial or Zone.Industry or Zone.Farmland) yield return d.Zone.ToString();
        if (d.Holy) yield return "Holy";
        if (d.Housing > 0 && d.Id is not ("house" or "tenement")) yield return "Fine";
        if (d.Id == "tenement") yield return "Poor";
    }

    /// <summary>Land price at a point as a multiplier, by what stands round it.</summary>
    public static double Land(Town town, double x, double y) => Clamp(1 + Field(town).Land![Cell(x, y)], Rules(town).Land);

    /// <summary>Rent at a point as a multiplier.</summary>
    public static double Rent(Town town, double x, double y) => Clamp(1 + Field(town).Rent![Cell(x, y)], Rules(town).Rent);

    /// <summary>Mood those living at a point carry every hour.</summary>
    public static double Mood(Town town, double x, double y) => Clamp(Field(town).Mood![Cell(x, y)], Rules(town).Mood);

    public static double Land(Town town, Lot lot) => Land(town, lot.X, lot.Y);
    public static double Rent(Town town, Lot lot) => Rent(town, lot.X, lot.Y);
    public static double Mood(Town town, Lot lot) => Mood(town, lot.X, lot.Y);

    /// <summary>Rent and mood at a plan cell, for searches that walk the plan.</summary>
    public static double RentAt(Town town, int cell) => Clamp(1 + Field(town).Rent![cell], Rules(town).Rent);
    public static double MoodAt(Town town, int cell) => Clamp(Field(town).Mood![cell], Rules(town).Mood);

    /// <summary>Whether a household at a point has a shop within walking reach (always, while the town has none).</summary>
    public static bool ShopsNear(Town town, double x, double y)
    {
        var f = Field(town);
        return !f.AnyShops || f.Access![Cell(x, y)] > 0;
    }

    /// <summary>What sets the ground at a point, one line each, for the hover on a house or a plot.</summary>
    public static List<string> Why(Town town, double x, double y)
    {
        var lines = new List<string>();
        var r = Rules(town);
        var f = Field(town);
        int i = Cell(x, y);
        double shops = f.Shops![i];
        if (shops > 0.3) lines.Add(shops > r.Shops.Good ? "noisy with shops" : "shops close by");
        else if (f.AnyShops && f.Access![i] <= 0) lines.Add("a long walk to any shop");
        foreach (var b in Around(town, x, y, r.Reach))
            foreach (var k in KindsOf(b.Def))
            {
                string s = k switch { "Industry" => "workshops' smoke", "Holy" => "near a temple", "Fine" => "fine houses", "Poor" => "tenements", "Farmland" => "open fields", _ => "" };
                if (s != "" && !lines.Contains(s)) lines.Add(s);
            }
        return lines;
    }

    private static IEnumerable<Building> Around(Town town, double x, double y, double reach)
    {
        foreach (var b in town.Buildings)
        {
            if (!b.Complete) continue;
            var (bx, by) = Decor.PosOf(town, b);
            if ((bx - x) * (bx - x) + (by - y) * (by - y) < reach * reach) yield return b;
        }
    }

    private static double Clamp(double v, double[] range) => Math.Clamp(v, range[0], range[1]);

    private static int Cell(double x, double y)
    {
        int cx = Math.Clamp((int)Math.Floor(x * Systems.Land.Res), 0, Systems.Land.Size - 1);
        int cy = Math.Clamp((int)Math.Floor(y * Systems.Land.Res), 0, Systems.Land.Size - 1);
        return cy * Systems.Land.Size + cx;
    }

    /// <summary>A fingerprint of everything the field is drawn from: each standing building's kinds and where it stands, and the neighbourhoods.</summary>
    private static long Stamp(Town town)
    {
        long h = town.Buildings.Count * 131 + town.City.Quarters.Count;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete) continue;
            h = h * 1_000_003 + b.Id * 31 + b.LotId * 7 + (int)b.Def.Zone * 3 + (b.Def.Holy ? 1 : 0) + b.Def.Housing * 17 + b.Def.Id.Length;
        }
        return h;
    }

    private static NeighbourhoodState Field(Town town)
    {
        var s = town.Neighbourhood;
        if (s.Tick == town.Tick && s.Land != null) return s;
        s.Tick = town.Tick;
        long stamp = Stamp(town);
        if (s.Land != null && s.Stamp == stamp) return s;
        s.Stamp = stamp;
        const int size = Systems.Land.Size;
        int n = size * size;
        float[] land = new float[n], rent = new float[n], mood = new float[n], shops = new float[n], access = new float[n];
        var r = Rules(town);
        bool anyShops = false;
        void Splat(double bx, double by, double reach, Action<int, float> add)
        {
            int cx = (int)Math.Floor(bx * Systems.Land.Res), cy = (int)Math.Floor(by * Systems.Land.Res), cells = (int)Math.Ceiling(reach * Systems.Land.Res);
            for (int y = Math.Max(0, cy - cells); y <= Math.Min(size - 1, cy + cells); y++)
                for (int x = Math.Max(0, cx - cells); x <= Math.Min(size - 1, cx + cells); x++)
                {
                    double dx = (x + 0.5) / Systems.Land.Res - bx, dy = (y + 0.5) / Systems.Land.Res - by;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    if (d < reach) add(y * size + x, (float)(1 - d / reach));
                }
        }
        foreach (var b in town.Buildings)
        {
            if (!b.Complete) continue;
            var (bx, by) = Decor.PosOf(town, b);
            if (b.Def.Zone == Zone.Commercial)
            {
                anyShops = true;
                Splat(bx, by, r.Shops.Near, (i, w) => shops[i] += w);
                Splat(bx, by, r.Shops.Reach, (i, _) => access[i] = 1);
            }
            double l = 0, rt = 0, m = 0;
            foreach (var k in KindsOf(b.Def))
                if (r.Kinds.TryGetValue(k, out var lean))
                {
                    l += lean.Land;
                    rt += lean.Rent;
                    m += lean.Mood;
                }
            if (l == 0 && rt == 0 && m == 0) continue;
            Splat(bx, by, r.Reach, (i, w) =>
            {
                land[i] += (float)l * w;
                rent[i] += (float)rt * w;
                mood[i] += (float)m * w;
            });
        }
        // Shops: the first few close by help, past them each is noise; none in reach is a long walk.
        var sh = r.Shops;
        for (int i = 0; i < n; i++)
        {
            double k = shops[i];
            if (k > 0)
            {
                double good = Math.Min(k, sh.Good);
                land[i] += (float)(good * sh.Land);
                rent[i] += (float)(good * sh.Rent);
                mood[i] += (float)(good * sh.Mood - Math.Max(0, k - sh.Good) * sh.Noise);
            }
            if (anyShops && access[i] <= 0)
            {
                land[i] += (float)sh.Far.Land;
                rent[i] += (float)sh.Far.Rent;
                mood[i] += (float)sh.Far.Mood;
            }
        }
        s.Land = land;
        s.Rent = rent;
        s.Mood = mood;
        s.Shops = shops;
        s.Access = access;
        s.AnyShops = anyShops;
        s.QuarterOf = Members(town);
        return s;
    }

    // What a building needs of its site -----------------------------------------------------------

    /// <summary>Why the ground at a point won't take this building, or null when it will.</summary>
    public static string? Sited(Town town, BuildingDef d, double x, double y)
    {
        if (d.Site is not { } s) return null;
        if (s.LandAtLeast > 0 && Land(town, x, y) < s.LandAtLeast) return "needs dearer ground: fine houses, a temple or a few shops round it";
        if (s.LandAtMost > 0 && Land(town, x, y) > s.LandAtMost) return "only goes up on cheap ground";
        if (s.Water && !Water(town, x, y)) return "needs to stand by the water";
        foreach (var (kind, count) in s.Near)
            if (Around(town, x, y, s.NearReach).Count(b => b.Def.Id == kind || KindsOf(b.Def).Contains(kind)) < count)
                return $"needs {Describe(town, kind)} near it";
        return null;
    }

    private static string Describe(Town town, string kind) => kind switch
    {
        "Holy" => "a temple or shrine",
        "Fine" => "fine houses",
        "Commercial" => "shops",
        "Industry" => "workshops",
        "Farmland" => "farms",
        "Poor" => "tenements",
        _ => town.Content.Buildings.FirstOrDefault(b => b.Id == kind) is { } b ? Agent.Article(b.Name) + " " + b.Name.ToLowerInvariant() : kind,
    };

    /// <summary>Whether a point lies by the river (or anywhere, for a town on the sea).</summary>
    public static bool Water(Town town, double x, double y)
    {
        if (Fleet.Coastal(town)) return true;
        var river = Passage.River(town);
        if (river.Count < 2) return false;
        var (px, py) = Passage.ToDays(x, y);
        double best = double.MaxValue;
        for (int i = 1; i < river.Count; i++)
        {
            var (ax, ay) = river[i - 1];
            var (bx, by) = river[i];
            double vx = bx - ax, vy = by - ay, len = vx * vx + vy * vy;
            double t = len < 1e-12 ? 0 : Math.Clamp(((px - ax) * vx + (py - ay) * vy) / len, 0, 1);
            double dx = px - (ax + t * vx), dy = py - (ay + t * vy);
            best = Math.Min(best, dx * dx + dy * dy);
        }
        return Math.Sqrt(best) <= Passage.DaysOut(Rules(town).Water);
    }

    // The scholars' quarter -----------------------------------------------------------------------

    /// <summary>
    /// The most arcane works standing within reach of one another (the library, the mage tower...): enough of them
    /// together make a scholars' quarter that adds to the library's lore.
    /// </summary>
    public static int Scholars(Town town)
    {
        var r = Rules(town).Scholars;
        if (r.Ids.Count == 0) return 0;
        var works = town.Buildings.Where(b => b.Complete && r.Ids.Contains(b.Def.Id)).Select(b => Decor.PosOf(town, b)).ToList();
        int best = 0;
        foreach (var (x, y) in works)
            best = Math.Max(best, works.Count(p => (p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y) <= r.Reach * r.Reach));
        return best;
    }

    /// <summary>Lore a day the scholars' quarter adds, once there are enough works together (0 before).</summary>
    public static double ScholarLore(Town town) => Scholars(town) >= Rules(town).Scholars.Least ? Rules(town).Scholars.Lore : 0;

    // Named neighbourhoods ------------------------------------------------------------------------

    /// <summary>Each building's neighbourhood by building id: the nearest heart within reach, or -1.</summary>
    private static int[] Members(Town town)
    {
        var q = town.City.Quarters;
        double reach = Rules(town).Quarters.Radius;
        var of = new int[town.Buildings.Count];
        for (int i = 0; i < of.Length; i++)
        {
            of[i] = -1;
            var b = town.Buildings[i];
            if (b.Razed || q.Count == 0) continue;
            var (x, y) = Decor.PosOf(town, b);
            double best = reach * reach;
            foreach (var n in q)
            {
                double d = (n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y);
                if (d < best)
                {
                    best = d;
                    of[i] = n.Id;
                }
            }
        }
        return of;
    }

    /// <summary>The neighbourhood a building stands in, or null.</summary>
    public static Quarter? Of(Town town, Building b)
    {
        var of = Field(town).QuarterOf!;
        int id = b.Id < of.Length ? of[b.Id] : -1;
        return id < 0 ? null : town.City.Quarters.FirstOrDefault(q => q.Id == id);
    }

    /// <summary>The neighbourhood a point lies in: the nearest heart within reach, or null.</summary>
    public static Quarter? At(Town town, double x, double y)
    {
        double best = Rules(town).Quarters.Radius * Rules(town).Quarters.Radius;
        Quarter? at = null;
        foreach (var n in town.City.Quarters)
        {
            double d = (n.X - x) * (n.X - x) + (n.Y - y) * (n.Y - y);
            if (d < best)
            {
                best = d;
                at = n;
            }
        }
        return at;
    }

    public static Quarter? Find(Town town, int id) => town.City.Quarters.FirstOrDefault(q => q.Id == id);

    /// <summary>The buildings standing in a neighbourhood, in id order.</summary>
    public static List<Building> Buildings(Town town, Quarter q)
    {
        var of = Field(town).QuarterOf!;
        var list = new List<Building>();
        for (int i = 0; i < of.Length; i++)
            if (of[i] == q.Id && town.Buildings[i].Complete) list.Add(town.Buildings[i]);
        return list;
    }

    /// <summary>What a set of buildings is, by what most of them are: market, workshops, farms, holy, scholars, river, rich, poor or homes.</summary>
    public static string Character(Town town, IReadOnlyList<Building> here)
    {
        if (here.Count == 0) return "homes";
        var r = Rules(town);
        var score = new Dictionary<string, double>();
        void Add(string k, double v) => score[k] = score.GetValueOrDefault(k) + v;
        foreach (var b in here)
        {
            if (r.Scholars.Ids.Contains(b.Def.Id)) Add("scholars", 3);
            if (b.Def.Holy) Add("holy", 3);
            if (b.Def.Profession == "fisher") Add("river", 3);
            switch (b.Def.Zone)
            {
                case Zone.Commercial: Add("market", 1.2); break;
                case Zone.Industry: Add("workshops", 1.2); break;
                case Zone.Farmland: Add("farms", 1); break;
                case Zone.Residential: Add("homes", 1); break;
            }
        }
        var top = score.OrderByDescending(x => x.Value).ThenBy(x => x.Key, StringComparer.Ordinal).Select(x => x.Key).FirstOrDefault() ?? "homes";
        if (top != "homes") return top;
        var homes = here.Where(b => b.Def.Housing > 0).ToList();
        double land = homes.Count == 0 ? 1 : homes.Average(b => Land(town, town.Lot(b.LotId)));
        if (land >= r.Quarters.Rich) return "rich";
        if (land <= r.Quarters.Poor || homes.Count(b => b.Def.Id == "tenement") * 3 >= homes.Count && homes.Count > 0) return "poor";
        return "homes";
    }

    /// <summary>A neighbourhood's figures now.</summary>
    public static QuarterStats Stats(Town town, Quarter q)
    {
        var here = Buildings(town, q);
        var homes = here.Where(b => b.Def.Housing > 0).ToList();
        var ids = homes.Select(b => b.Id).ToHashSet();
        int residents = town.Agents.Count(a => a.HomeId is int h && ids.Contains(h));
        double Avg(Func<Lot, double> f) => homes.Count == 0 ? (here.Count == 0 ? 1 : here.Average(b => f(town.Lot(b.LotId)))) : homes.Average(b => f(town.Lot(b.LotId)));
        return new QuarterStats(q, Character(town, here), here.Count, homes.Count, residents,
            here.Count(b => b.Def.Zone == Zone.Commercial), here.Count(b => b.Def.Zone == Zone.Industry),
            Avg(l => Rent(town, l)), Avg(l => Land(town, l)), homes.Count == 0 ? 0 : homes.Average(b => Mood(town, town.Lot(b.LotId))));
    }

    /// <summary>
    /// Daily: where enough finished buildings stand together outside every neighbourhood, a new one forms round them and
    /// takes a name from what it is. Its name stays; what it is may change as it grows.
    /// </summary>
    public static void Daily(Town town)
    {
        var r = Rules(town).Quarters;
        for (int pass = 0; pass < 3; pass++)
        {
            var of = Members(town);
            var loose = town.Buildings.Where(b => b.Complete && of[b.Id] < 0).ToList();
            if (loose.Count < r.Founding) return;
            var pos = loose.Select(b => Decor.PosOf(town, b)).ToList();
            int bestAt = -1, bestCount = 0;
            for (int i = 0; i < loose.Count; i++)
            {
                int c = 0;
                for (int j = 0; j < loose.Count; j++)
                    if ((pos[i].X - pos[j].X) * (pos[i].X - pos[j].X) + (pos[i].Y - pos[j].Y) * (pos[i].Y - pos[j].Y) <= r.Spacing * r.Spacing) c++;
                if (c > bestCount)
                {
                    bestCount = c;
                    bestAt = i;
                }
            }
            if (bestCount < r.Founding) return;
            var here = new List<Building>();
            double sx = 0, sy = 0;
            for (int j = 0; j < loose.Count; j++)
                if ((pos[bestAt].X - pos[j].X) * (pos[bestAt].X - pos[j].X) + (pos[bestAt].Y - pos[j].Y) * (pos[bestAt].Y - pos[j].Y) <= r.Spacing * r.Spacing)
                {
                    here.Add(loose[j]);
                    sx += pos[j].X;
                    sy += pos[j].Y;
                }
            Form(town, here, sx / here.Count, sy / here.Count);
        }
    }

    private static void Form(Town town, List<Building> here, double x, double y)
    {
        var r = Rules(town).Quarters;
        var city = town.City;
        string character = Character(town, here);
        string name;
        if (city.Quarters.Count == 0) name = r.Heart;
        else
        {
            var used = city.Quarters.Select(q => q.Name).ToHashSet();
            var names = r.Names.TryGetValue(character, out var list) && list.Count > 0 ? list : r.Names.GetValueOrDefault("homes") ?? ["New Lanes"];
            int start = (int)(Voices.Hash("quarter:" + character, town.Seed, city.Quarters.Count) % (uint)names.Count);
            name = Enumerable.Range(0, names.Count).Select(k => names[(start + k) % names.Count]).FirstOrDefault(nm => !used.Contains(nm))
                ?? $"New {names[start].Replace("the ", "")}";
            while (used.Contains(name)) name = "Upper " + name;
        }
        var q = new Quarter { Id = city.Quarters.Count == 0 ? 0 : city.Quarters.Max(n => n.Id) + 1, Name = name, X = x, Y = y, Character = character, Day = town.Day };
        city.Quarters.Add(q);
        town.Neighbourhood.Tick = -1;
        if (q.Id > 0)
            town.Log(LedgerCategory.Town, Importance.Notable, $"Folk have started calling the {Describe(character)} {(Ring(town, x, y) == 0 ? "by the hall" : "out past the old town")} {name}.");
    }

    private static int Ring(Town town, double x, double y) => City.Ring(town, x, y);

    /// <summary>A character as the news names it: "market streets", "workshops", "fine houses".</summary>
    public static string Describe(string character) => character switch
    {
        "market" => "market streets",
        "workshops" => "workshops and yards",
        "farms" => "farmsteads",
        "holy" => "streets round the temple",
        "rich" => "fine houses",
        "poor" => "crowded lanes",
        "scholars" => "scholars' streets",
        "river" => "wharves",
        _ => "new lanes",
    };
}

/// <summary>The neighbourhood field per plan cell; nothing here is saved, it is drawn again from the buildings.</summary>
public sealed class NeighbourhoodState
{
    [NotSaved]
    internal long Tick = -1;
    [NotSaved]
    internal long Stamp;
    [NotSaved]
    internal float[]? Land;
    [NotSaved]
    internal float[]? Rent;
    [NotSaved]
    internal float[]? Mood;
    [NotSaved]
    internal float[]? Shops;
    [NotSaved]
    internal float[]? Access;
    [NotSaved]
    internal bool AnyShops;
    [NotSaved]
    internal int[]? QuarterOf;
}
