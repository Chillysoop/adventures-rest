namespace CrownAndCompass.Sim.Systems;

public sealed record FoundingSeaDef
{
    public double Arc { get; init; } = 120;
    public double Inner { get; init; } = 0.24;
    public double Outer { get; init; } = 0.75;
}

/// <summary>A kind of place to found the town: a river ford, a sea cove, an island, a crag, a pass.</summary>
public sealed record FoundingDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>What the client raises round the town: meadow, coast, isle, crag, plateau, desert, ledge or pass.</summary>
    public string Look { get; init; } = "meadow";

    /// <summary>Founded only within <see cref="FoundingRules.Shore"/> days of the ocean's shore.</summary>
    public bool Shore { get; init; }

    /// <summary>
    /// The country it needs: a region whose habitats include one of <see cref="Ground"/>, or hard country of one of the
    /// <see cref="Rock"/> kinds close by. Both empty for anywhere on land.
    /// </summary>
    public IReadOnlyList<string> Ground { get; init; } = [];
    public IReadOnlyList<string> Rock { get; init; } = [];

    /// <summary>Compass bearing the sea or the drop lies on, before the seed turns it.</summary>
    public double Bearing { get; init; }
    public FoundingSeaDef? Sea { get; init; }

    /// <summary>High peaks either side of the road out.</summary>
    public bool Flank { get; init; }

    public double Defence { get; init; }
    public double Trade { get; init; } = 1;
    public IReadOnlyDictionary<string, double> Yields { get; init; } = new Dictionary<string, double>();
    public int Watch { get; init; }

    /// <summary>Tiles farther every watchtower sees from a town on high ground.</summary>
    public double Sight { get; init; }

    /// <summary>"" none, "garrison" a manned keep at the charter, "ruin" an old keep to win back, "either" by the seed.</summary>
    public string Keep { get; init; } = "";

    /// <summary>The site when a neighbour holds it ({name} the settlement), or when foes do.</summary>
    public string Held { get; init; } = "";
    public string Foes { get; init; } = "";
}

/// <summary>What becomes of the town sites the player passed over (founding.json <c>hold</c>).</summary>
public sealed record FoundingHoldRules
{
    /// <summary>Chance a site in a people's country starts as theirs, and that one starts in foes' hands; the rest lie empty.</summary>
    public double People { get; init; } = 0.45;
    public double Foes { get; init; } = 0.25;

    /// <summary>Chance a day that an empty site is taken, from <see cref="From"/>: by the people of its country, else by foes.</summary>
    public double Later { get; init; } = 0.005;
    public int From { get; init; } = 15;
}

public sealed record FoundingRules
{
    public string Default { get; init; } = "river-ford";

    /// <summary>Days from the world's heart within which New game pins a site of every kind.</summary>
    public double Heartland { get; init; } = 2.2;

    /// <summary>Farthest from the world's heart a town may be founded, in days.</summary>
    public double Reach { get; init; } = 3.6;

    /// <summary>Days from the ocean a coastal site may lie, and how far apart the pinned sites stand.</summary>
    public double Shore { get; init; } = 0.6;
    public double Apart { get; init; } = 0.7;

    /// <summary>Days kept clear of hard country round a town founded away from the heart.</summary>
    public double Clear { get; init; } = 0.3;
    public FoundingHoldRules Hold { get; init; } = new();

    /// <summary>The roads.json terrain kind of a coastal town's sea.</summary>
    public string Sea { get; init; } = "sea";

    /// <summary>The old keep a crag or a pass may start with in ruins (<see cref="Ruins"/>).</summary>
    public RuinDef? Ruin { get; init; }
    public IReadOnlyList<FoundingDef> Kinds { get; init; } = [];

    /// <summary>Names New game offers when the player rolls one for the town, and the longest a name may be.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    public int NameMost { get; init; } = 24;
}

/// <summary>
/// A place a town could be founded, pinned on New game's map: its kind and where it lies, in day units from the world's
/// heart (x east, y south). The ones the player passes over stay on the map, empty or held by a people or by foes.
/// </summary>
public sealed class FoundingSpot
{
    public required string Kind { get; init; }
    public double X { get; init; }
    public double Y { get; init; }

    /// <summary>"" while it lies empty, "people" once its country's settlement holds it, "foes" once foes do.</summary>
    public string Holder { get; set; } = "";

    /// <summary>The day it was taken; 0 for held from the start.</summary>
    public int Day { get; set; }
}

/// <summary>
/// Where the town is founded (Q7, Q29): New game offers a few sites the seed's country allows, the chosen one shapes the
/// land round the town and its numbers, and the ones not chosen stand on the same map, held by neighbours or by foes.
/// The river ford (the default) changes nothing. Everything here is a pure function of the seed and the choice, on its
/// own dice, so nothing else the seed makes shifts.
/// </summary>
public static class Founding
{
    /// <summary>A name the player typed, tidied: trimmed, single-spaced, no markup brackets, cut to the longest allowed; null when nothing is left.</summary>
    public static string? CleanName(Content content, string? typed)
    {
        if (string.IsNullOrWhiteSpace(typed)) return null;
        var kept = new string(typed.Where(c => !char.IsControl(c) && c is not ('[' or ']' or '{' or '}' or '<' or '>')).ToArray());
        string name = string.Join(' ', kept.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (name.Length > content.Founding.NameMost) name = name[..content.Founding.NameMost].TrimEnd();
        return name == "" ? null : name;
    }

    public static FoundingRules Rules(Content content) => content.Founding;

    public static FoundingDef? Kind(Content content, string? id) => Rules(content).Kinds.FirstOrDefault(k => k.Id == id);

    /// <summary>The site the town stands on (the default for an old save).</summary>
    public static FoundingDef Of(Town town) =>
        Kind(town.Content, town.Founding) ?? Kind(town.Content, Rules(town.Content).Default) ?? new FoundingDef { Id = "river-ford", Name = "River ford" };

    private static ulong Mix(int seed, int salt)
    {
        ulong h = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL ^ (ulong)(uint)salt * 0xC2B2AE3D27D4EB4FUL;
        h ^= h >> 31;
        h *= 0xBF58476D1CE4E5B9UL;
        return h ^ (h >> 29);
    }

    private static double Hash(int seed, int salt) => (Mix(seed, salt) >> 11) / (double)(1UL << 53);

    /// <summary>
    /// The town sites pinned on New game's map: one of every kind within the heartland, the default at the heart itself,
    /// each where its country suits it best (a cove on the shore, a pass under the mountains, a cliff city toward the
    /// desert). Where nothing near suits a kind, its pin lies on the side it would be found on and the site brings its own
    /// country. In day units from the world's heart; fixed by the seed, and kept by the founded town.
    /// </summary>
    public static IReadOnlyList<FoundingSpot> Spots(Town town)
    {
        if (town.FoundingSpots.Count > 0) return town.FoundingSpots;
        var r = Rules(town.Content);
        var spots = new List<FoundingSpot>();
        if (Kind(town.Content, r.Default) is { } ford) spots.Add(new FoundingSpot { Kind = ford.Id });
        for (int i = 0; i < r.Kinds.Count; i++)
        {
            if (r.Kinds[i].Id == r.Default) continue;
            var (x, y) = Pin(town, r.Kinds[i], i, spots);
            spots.Add(new FoundingSpot { Kind = r.Kinds[i].Id, X = Js.Round(x * 100) / 100.0, Y = Js.Round(y * 100) / 100.0 });
        }
        return spots;
    }

    /// <summary>Where a kind's pin goes: the open spot nearest the heart whose country suits it, else on the side it is found.</summary>
    private static (double X, double Y) Pin(Town town, FoundingDef k, int salt, List<FoundingSpot> taken)
    {
        var r = Rules(town.Content);
        // Well clear of the sites: their pins settle a little as the world fills in.
        var sites = town.Sites.Where(s => s.Def.Distance > 0).Select(s => World.Spot(town, s)).ToList();
        bool Open(double x, double y) => Blocked(town, x, y) == null && taken.All(t => Js.Hypot(t.X - x, t.Y - y) >= r.Apart);
        bool Free(double x, double y) => Open(x, y) && sites.All(p => Js.Hypot(p.X - x, p.Y - y) >= 0.45);
        (double X, double Y)? best = null;
        double score = double.MaxValue;
        for (int i = 0; i < 240; i++)
        {
            double reach = 0.55 + Math.Sqrt(Hash(town.Seed, 1000 + salt * 977 + i * 2)) * (r.Heartland - 0.55);
            var (x, y) = Polar(Hash(town.Seed, 1001 + salt * 977 + i * 2) * 360, reach);
            if (!Free(x, y) || !Suits(town, k, x, y)) continue;
            double sc = reach + Hash(town.Seed, 3001 + salt * 977 + i) * 0.4;
            if (sc < score) (best, score) = ((x, y), sc);
        }
        if (best is { } found) return found;
        // Nothing near suits it: on its side of the world, as near that side's middle as there is open ground.
        double side = Side(town, k);
        foreach (var fits in new Func<double, double, bool>[] { Free, Open })
            for (int i = 0; i <= 60; i++)
                foreach (double share in new[] { 0.75, 0.55, 0.95, 0.35 })
                {
                    var (x, y) = Polar(side + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * 6, r.Heartland * share);
                    if (fits(x, y)) return (x, y);
                }
        return Polar(side, r.Heartland * 0.6);
    }

    private static (double X, double Y) Polar(double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return (r * Math.Sin(a), -r * Math.Cos(a));
    }

    /// <summary>The side of the world a kind is found on when nothing near the heart suits it: the shore, the warm side, the mountains'.</summary>
    private static double Side(Town town, FoundingDef k)
    {
        if (k.Shore && town.OceanArc > 0) return town.OceanBearing;
        if (k.Ground.Contains("desert")) return World.Warm(town);
        if (k.Ground.Contains("mountain"))
            return town.Regions.Where(g => g.Ring <= 2 && !g.Theme.Sea && g.Theme.Habitats.Contains("mountain"))
                .OrderBy(g => g.Ring).ThenBy(g => g.Id).Select(g => (double?)g.Bearing).FirstOrDefault() ?? (World.Warm(town) + 180) % 360;
        return Hash(town.Seed, 61 + k.Id.Length) * 360;
    }

    /// <summary>Whether the country at a spot (day units from the heart) suits a kind of site: its shore, its ground, its rock.</summary>
    public static bool Suits(Town town, FoundingDef k, double x, double y)
    {
        double px = x + town.HeartX, py = y + town.HeartY;
        if (k.Shore && !town.Terrain.Any(f => f.Kind.Id == "ocean" && Roads.Contains(f, px, py, Rules(town.Content).Shore))) return false;
        if (k.Ground.Count == 0 && k.Rock.Count == 0) return true;
        if (Explore.RegionAt(town, px, py) is { } g && g.Theme.Habitats.Any(k.Ground.Contains)) return true;
        return town.Terrain.Any(f => k.Rock.Contains(f.Kind.Id) && Roads.Contains(f, px, py, 0.5));
    }

    /// <summary>Why no town can be founded at a spot (day units from the heart), or null when one can.</summary>
    public static string? Blocked(Town town, double x, double y)
    {
        var r = Rules(town.Content);
        double px = x + town.HeartX, py = y + town.HeartY;
        if (Js.Hypot(x, y) > r.Reach) return "too deep in the wilds";
        if (Explore.RegionAt(town, px, py) is { } g && g.Theme.Sea) return "open water";
        foreach (var f in town.Terrain)
        {
            if (f.Kind.Barrier == "water" && Roads.Contains(f, px, py)) return "open water";
            if (f.Kind.Barrier == "sheer" && Roads.Contains(f, px, py)) return "sheer peaks";
        }
        foreach (var s in town.Sites)
        {
            if (s.Def.Distance <= 0) continue;
            var (sx, sy) = World.Spot(town, s);
            if (Js.Hypot(sx - px, sy - py) < 0.3) return "something old already stands there";
        }
        foreach (var st in town.Settlements)
        {
            var (sx, sy) = Explore.SettlementSpot(town, st);
            if (Js.Hypot(sx - px, sy - py) < 0.4) return $"{st.Name} stands there";
        }
        return null;
    }

    /// <summary>The kinds of town that could be founded at a spot (day units from the heart): those its country suits, and any pinned near it.</summary>
    public static IReadOnlyList<FoundingDef> KindsAt(Town town, double x, double y)
    {
        if (Blocked(town, x, y) != null) return [];
        var spots = Spots(town);
        return Rules(town.Content).Kinds
            .Where(k => Suits(town, k, x, y) || spots.Any(p => p.Kind == k.Id && Js.Hypot(p.X - x, p.Y - y) < 0.4))
            .ToList();
    }

    /// <summary>Where the town is founded, from the heart: the spot asked for if it can take the kind, else the kind's own pin.</summary>
    public static (double X, double Y) Home(Town town, double? x, double? y)
    {
        var def = Of(town);
        if (x is double hx && y is double hy && KindsAt(town, hx, hy).Any(k => k.Id == def.Id)) return (hx, hy);
        return Spots(town).FirstOrDefault(p => p.Kind == def.Id) is { } pin ? (pin.X, pin.Y) : (0, 0);
    }

    /// <summary>
    /// The town stands away from the world's heart: everything the world measures from there is measured from the town
    /// instead. The rings of country, their hard country and the old gateways stay where they lie; the days to every site
    /// and region are walked from the town, and the country nearest it is the country it knows.
    /// </summary>
    internal static void Settle(Town town, double x, double y)
    {
        if (x == 0 && y == 0) return;
        town.HeartX = -x;
        town.HeartY = -y;
        foreach (var f in town.Terrain)
        {
            f.Ox += town.HeartX;
            f.Oy += town.HeartY;
        }
        foreach (var g in town.Gateways)
        {
            g.Ax = Js.Round((g.Ax + town.HeartX) * 100) / 100.0;
            g.Ay = Js.Round((g.Ay + town.HeartY) * 100) / 100.0;
            g.Bx = Js.Round((g.Bx + town.HeartX) * 100) / 100.0;
            g.By = Js.Round((g.By + town.HeartY) * 100) / 100.0;
        }
        // The town's own ground is clear of hard country; the ocean stays where it is.
        double clear = Rules(town.Content).Clear;
        town.Terrain.RemoveAll(f => f.Kind.Id != "ocean" && Roads.Contains(f, 0, 0, clear));
        foreach (var s in town.Sites.Where(s => s.Def.Distance > 0))
        {
            s.Out = s.Def.Distance;
            var (sx, sy) = World.Spot(town, s);
            s.Def = s.Def with { Distance = Math.Max(1, (int)Math.Round(Js.Hypot(sx, sy))) };
        }
        int known = town.Regions.Count(g => g.Known);
        foreach (var g in town.Regions)
        {
            g.Out = g.Days;
            var (mx, my) = World.Middle(town, g);
            g.Days = Math.Max(1, (int)Math.Round(Js.Hypot(mx, my)));
            g.Known = false;
        }
        foreach (var g in town.Regions.OrderBy(g => Js.Hypot(World.Middle(town, g).X, World.Middle(town, g).Y)).ThenBy(g => g.Id).Take(Math.Max(1, known)))
            g.Known = true;
        Passage.Forget(town);
        Cliffs.Forget(town);
    }

    /// <summary>What a site does, as short chips for New game: an icon key and a figure ("defence", "+30%").</summary>
    public static IReadOnlyList<(string Icon, string Text)> Effects(FoundingDef def)
    {
        static string Pct(double x) => (x >= 0 ? "+" : "") + Js.Str(Js.Round(x * 100)) + "%";
        var chips = new List<(string, string)>();
        if (def.Defence != 0) chips.Add(("defence", Pct(def.Defence)));
        if (def.Trade != 1) chips.Add(("gold", Pct(def.Trade - 1)));
        foreach (var (good, k) in def.Yields.OrderBy(y => y.Key, StringComparer.Ordinal))
            if (k != 1) chips.Add((good.ToLowerInvariant(), Pct(k - 1)));
        if (def.Sea != null) chips.Add(("compass", "boats"));
        if (def.Sight > 0) chips.Add(("act-scry", $"+{Js.Str(def.Sight)} sight"));
        if (def.Keep != "") chips.Add(("tab-war", def.Keep == "garrison" ? "keep" : def.Keep == "ruin" ? "ruined keep" : "keep?"));
        return chips;
    }

    /// <summary>What a site does in numbers, in words: "Walls +30%, food -15%, stone +25%".</summary>
    public static string Summary(FoundingDef def)
    {
        static string Pct(double x) => (x >= 0 ? "+" : "") + Js.Str(Js.Round(x * 100)) + "%";
        var parts = new List<string>();
        if (def.Defence != 0) parts.Add($"walls {Pct(def.Defence)}");
        if (def.Trade != 1) parts.Add($"caravans {Pct(def.Trade - 1)}");
        foreach (var (good, k) in def.Yields.OrderBy(y => y.Key, StringComparer.Ordinal))
            if (k != 1) parts.Add($"{good.ToLowerInvariant()} {Pct(k - 1)}");
        if (def.Sea != null) parts.Add("boats over the sea");
        string keep = def.Keep switch { "garrison" => "a manned keep", "ruin" => "a ruined keep to win back", "either" => "a keep, manned or in ruins", _ => "" };
        if (keep != "") parts.Add(keep);
        if (parts.Count == 0) return "No bonuses and no drawbacks.";
        string text = string.Join(", ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..] + ".";
    }

    /// <summary>Its share added to the walls' fortification.</summary>
    public static double Defence(Town town) => town.Founding == null ? 0 : Of(town).Defence;

    /// <summary>What caravans earn, as a multiple.</summary>
    /// <summary>Tiles farther the town's watchtowers see from its high ground.</summary>
    public static double Sight(Town town) => town.Founding == null ? 0 : Of(town).Sight;

    public static double Trade(Town town) => town.Founding == null ? 1 : Of(town).Trade;

    /// <summary>What the land gives of a good, as a multiple.</summary>
    public static double Yield(Town town, Good good) => town.Founding == null ? 1 : Of(town).Yields.GetValueOrDefault(good.ToString(), 1);

    /// <summary>Where the settlers' wagon stops, for the charter's line.</summary>
    public static string At(Town town) => town.Founding == null ? "the river ford" : $"the {Of(town).Name.ToLowerInvariant()}";

    /// <summary>A manned keep or a ruined one: "either" turns on the seed.</summary>
    public static string Keep(Town town)
    {
        var k = Of(town).Keep;
        return k == "either" ? (Hash(town.Seed, 31) < 0.5 ? "garrison" : "ruin") : k;
    }

    /// <summary>
    /// At generation, after the rest of the world: the sites pinned for New game kept, the town settled where it was
    /// founded, the sea and the flanking peaks round it, and the sites passed over left empty or given to their country's
    /// people or to foes.
    /// </summary>
    internal static void Place(Town town, double? x, double? y)
    {
        town.FoundingSpots.AddRange(Spots(town));
        var (hx, hy) = Home(town, x, y);
        Settle(town, hx, hy);
        var def = Of(town);
        if (town.Founding != null)
        {
            if (def.Sea != null) Sea(town, def);
            if (def.Flank) Flank(town, def);
            if (Keep(town) == "ruin") RuinedKeep(town);
        }
        Hold(town);
    }

    /// <summary>A pinned site's spot, in day units from the town.</summary>
    public static (double X, double Y) At(Town town, FoundingSpot p) => (p.X + town.HeartX, p.Y + town.HeartY);

    /// <summary>The region a pinned site lies in.</summary>
    public static Region? RegionOf(Town town, FoundingSpot p) => Explore.RegionAt(town, At(town, p).X, At(town, p).Y);

    /// <summary>The settlement that would take a site: the living one of its region, if that region holds no site yet.</summary>
    private static Settlement? Taker(Town town, FoundingSpot p) =>
        RegionOf(town, p) is { Site: null, SettlementId: int id } && town.Settlements[id] is { Razed: false } st ? st : null;

    /// <summary>
    /// The sites passed over, each on its own roll: its country's people may hold it, foes may, or it lies empty for
    /// somebody to take later (<see cref="Daily"/>). The site the town stands on is the town's.
    /// </summary>
    private static void Hold(Town town)
    {
        var h = Rules(town.Content).Hold;
        for (int i = 0; i < town.FoundingSpots.Count; i++)
        {
            var p = town.FoundingSpots[i];
            var (x, y) = At(town, p);
            if (Js.Hypot(x, y) < 0.4) { p.Holder = "town"; continue; }
            double roll = Hash(town.Seed, 700 + i);
            if (roll < h.People && Taker(town, p) is { } st) Take(town, p, "people", RegionOf(town, p)!);
            else if (roll >= 1 - h.Foes) Take(town, p, "foes", null);
        }
    }

    private static void Take(Town town, FoundingSpot p, string holder, Region? region)
    {
        p.Holder = holder;
        p.Day = town.Day;
        if (region != null) region.Site = p.Kind;
    }

    /// <summary>Daily: an empty site may be taken, by the people of its country or, where none can, by foes. On its own dice.</summary>
    internal static void Daily(Town town)
    {
        var h = Rules(town.Content).Hold;
        if (town.Day < h.From) return;
        for (int i = 0; i < town.FoundingSpots.Count; i++)
        {
            var p = town.FoundingSpots[i];
            if (p.Holder != "" || Hash(town.Seed * 31 + town.Day, 900 + i) >= h.Later || Kind(town.Content, p.Kind) is not { } k) continue;
            if (Taker(town, p) is { } st)
            {
                Take(town, p, "people", RegionOf(town, p)!);
                town.Log(LedgerCategory.Faction, Importance.Notable, $"{st.Name} has claimed the empty {k.Name.ToLowerInvariant()} in {RegionOf(town, p)!.Name}.");
            }
            else
            {
                Take(town, p, "foes", null);
                town.Log(LedgerCategory.War, Importance.Notable, $"Word comes from {RegionOf(town, p)?.Name ?? "the wilds"}. {k.Foes}");
            }
        }
    }

    /// <summary>A pinned site and who holds it, for its card.</summary>
    public static string Describe(Town town, FoundingSpot p)
    {
        if (Kind(town.Content, p.Kind) is not { } k) return "";
        return p.Holder switch
        {
            "people" when RegionOf(town, p) is { SettlementId: int id } => k.Held.Replace("{name}", town.Settlements[id].Name, StringComparison.Ordinal),
            "foes" => k.Foes,
            "town" => $"{town.Name} stands here.",
            _ => $"Empty for now: {k.Blurb}",
        };
    }

    private static double Gap(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);

    /// <summary>
    /// The way the site faces, in compass degrees: where its sea lies, the road out of its pass, the open side of a cliff
    /// city or a ledge (the rock rises behind). The kind's bearing turned by the seed.
    /// </summary>
    public static double Facing(Town town)
    {
        if (Of(town).Sea == null) return Hash(town.Seed, 43) * 360;
        double b = (Of(town).Bearing + (Hash(town.Seed, 41) - 0.5) * 60 + 360) % 360;
        if (town.OceanArc <= 0) return b;
        // Founded away from the heart, the shore faces the nearest of the ocean.
        if ((town.HeartX != 0 || town.HeartY != 0) && (town.Shoreward ??= Shoreward(town) ?? -1) is var shore and >= 0) return shore;
        // The shore faces the ocean: the founding's own bearing where that is out over it, else the ocean's nearest edge.
        double turn = ((b - town.OceanBearing) % 360 + 540) % 360 - 180, half = town.OceanArc / 2 * 0.8;
        return (town.OceanBearing + Math.Clamp(turn, -half, half) + 360) % 360;
    }

    /// <summary>The bearing from the town to the nearest open ocean, or null when none lies within a few days.</summary>
    private static double? Shoreward(Town town)
    {
        var ocean = town.Terrain.Where(f => f.Kind.Id == "ocean").ToList();
        for (double r = 0.1; r <= 4; r += 0.1)
        {
            double? best = null, off = double.MaxValue;
            for (int i = 0; i < 72; i++)
            {
                double b = i * 5, a = b * Math.PI / 180;
                if (!ocean.Any(f => Roads.Contains(f, r * Math.Sin(a), -r * Math.Cos(a)))) continue;
                // Of the bearings that reach the water first, the one nearest the ocean's own middle.
                double d = Gap(b, town.OceanBearing);
                if (d < off) (best, off) = (b, d);
            }
            if (best != null) return best;
        }
        return null;
    }

    /// <summary>
    /// Whether hard country stands where the town's own sea or peaks go (a sector round the town): the founding's country
    /// takes its place. The ocean stays.
    /// </summary>
    private static bool Covers(Feature f, double bearing, double arc, double inner, double outer)
    {
        if (f.Kind.Id == "ocean") return false;
        if (f.Ox == 0 && f.Oy == 0) return f.Inner < outer && f.Outer > inner && Gap(f.Bearing, bearing) < (f.Arc + arc) / 2;
        for (int i = 0; i <= 6; i++)
            for (int j = 0; j <= 4; j++)
            {
                double b = (bearing - arc / 2 + arc * i / 6) * Math.PI / 180, r = inner + (outer - inner) * j / 4;
                if (Roads.Contains(f, r * Math.Sin(b), -r * Math.Cos(b), 0.1)) return true;
            }
        return false;
    }

    /// <summary>The site a region holds and who holds it, for its card: null when it holds none.</summary>
    public static string? Describe(Town town, Region region)
    {
        if (region.Site == null || Kind(town.Content, region.Site) is not { } k) return null;
        if (region.SettlementId is int id && town.Settlements[id] is { Razed: false } s)
            return k.Held.Replace("{name}", s.Name, StringComparison.Ordinal);
        return k.Foes;
    }

    /// <summary>The bay before a town founded on the sea, or null; the ocean further out is separate water.</summary>
    public static Feature? Bay(Town town) => town.Terrain.FirstOrDefault(f => f.Kind.Id == Rules(town.Content).Sea);

    /// <summary>A sector of open water from the town's shore, kept clear of every site; the town's boats cross it.</summary>
    private static void Sea(Town town, FoundingDef def)
    {
        var kind = town.Content.Roads.Kinds.FirstOrDefault(k => k.Id == Rules(town.Content).Sea);
        if (kind == null) return;
        var sea = def.Sea!;
        double bearing = Facing(town);
        var spots = town.Sites.Select(s => World.Spot(town, s))
            .Concat(town.Gateways.SelectMany(g => new[] { (g.Ax, g.Ay), (g.Bx, g.By) }))
            .ToList();
        double outer = sea.Outer;
        foreach (var (x, y) in spots)
        {
            double r = Js.Hypot(x, y), b = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
            if (Gap(b, bearing) <= sea.Arc / 2 + 12) outer = Math.Min(outer, r - 0.2);
        }
        outer = Math.Max(sea.Inner + 0.12, outer);
        var region = town.Regions.Where(g => g.Ring == 0).OrderBy(g => Gap(g.Bearing, bearing)).ThenBy(g => g.Id).First();
        // The sea takes the place of whatever country lay there.
        town.Terrain.RemoveAll(f => Covers(f, bearing, sea.Arc, sea.Inner, outer));
        var rng = new Rng(town.Seed * 7919 + 4243);
        string word = kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Grey";
        string pattern = kind.Names.Count > 0 ? rng.Pick(kind.Names) : "the {A} Sea";
        town.Terrain.Add(new Feature
        {
            Id = town.Terrain.Count == 0 ? 0 : town.Terrain.Max(f => f.Id) + 1,
            Name = pattern.Replace("{A}", word, StringComparison.Ordinal),
            Kind = kind,
            RegionId = region.Id,
            Tier = region.Levels[0],
            Bearing = Js.Round(bearing * 10) / 10.0,
            Arc = sea.Arc,
            Inner = sea.Inner,
            Outer = Js.Round(outer * 100) / 100.0,
        });
    }

    /// <summary>High peaks either side of the road out of a pass or below a mountain ledge.</summary>
    private static void Flank(Town town, FoundingDef def)
    {
        var kind = town.Content.Roads.Kinds.FirstOrDefault(k => k.Id == "peaks");
        if (kind == null) return;
        double road = Facing(town);
        var spots = town.Sites.Select(s => World.Spot(town, s)).ToList();
        var region = town.Regions.Where(g => g.Ring == 0).OrderBy(g => Gap(g.Bearing, road)).ThenBy(g => g.Id).First();
        var rng = new Rng(town.Seed * 7919 + 4547);
        foreach (double side in new[] { 90.0, 270.0 })
        {
            double bearing = (road + side) % 360;
            double outer = 0.7;
            foreach (var (x, y) in spots)
            {
                double r = Js.Hypot(x, y), b = (Math.Atan2(x, -y) * 180 / Math.PI + 360) % 360;
                if (Gap(b, bearing) <= 50) outer = Math.Min(outer, r - 0.2);
            }
            if (outer < 0.4) continue;
            town.Terrain.RemoveAll(f => Covers(f, bearing, 80, 0.24, outer));
            string word = kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Grey";
            string pattern = kind.Names.Count > 0 ? rng.Pick(kind.Names) : "the {A} Peaks";
            town.Terrain.Add(new Feature
            {
                Id = town.Terrain.Count == 0 ? 0 : town.Terrain.Max(f => f.Id) + 1,
                Name = pattern.Replace("{A}", word, StringComparison.Ordinal),
                Kind = kind,
                RegionId = region.Id,
                Tier = region.Levels[0],
                Bearing = Js.Round(bearing * 10) / 10.0,
                Arc = 70,
                Inner = 0.26,
                Outer = Js.Round(outer * 100) / 100.0,
            });
        }
    }

    /// <summary>The nearest site that isn't a foe's own becomes the old keep: clear it, then restore it (<see cref="Ruins"/>).</summary>
    private static void RuinedKeep(Town town)
    {
        if (Rules(town.Content).Ruin is not { } keep) return;
        var site = town.Sites
            .Where(s => s.Generated && s.Def.Foe == "" && !s.Def.Aloft && s.Def.Ruin == "" && s.RegionId is int rid && town.Regions[rid].Ring <= (town.HeartX == 0 && town.HeartY == 0 ? 0 : 1))
            .OrderBy(s => Js.Hypot(World.Spot(town, s).X, World.Spot(town, s).Y))
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (site == null) return;
        string name = keep.Names.Count > 0 ? keep.Names[(int)(Hash(town.Seed, 53) * keep.Names.Count)] : "the Old Keep";
        site.Def = site.Def with { Ruin = keep.Id, Name = name };
    }

    /// <summary>At the charter: a manned keep beside the hall and the watch it brings.</summary>
    internal static void Charter(Town town)
    {
        if (town.Founding == null) return;
        var def = Of(town);
        town.Watch += def.Watch;
        if (Keep(town) != "garrison") return;
        var keep = town.Content.Building("keep");
        if (Land.Find(town, keep, null) is not { } lot) return;
        lot.Zone = Zone.Civic;
        town.AddBuilding(keep, lot, null, "the Old Keep", true);
        town.Watch += 2;
    }
}
