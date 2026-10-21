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

    /// <summary>Offered only when one of these themes lies within <see cref="Within"/> rings; empty for anywhere.</summary>
    public IReadOnlyList<string> Near { get; init; } = [];
    public int Within { get; init; } = 1;

    /// <summary>Compass bearing the sea or the drop lies on, before the seed turns it.</summary>
    public double Bearing { get; init; }
    public FoundingSeaDef? Sea { get; init; }

    /// <summary>High peaks either side of the road out.</summary>
    public bool Flank { get; init; }

    public double Defence { get; init; }
    public double Trade { get; init; } = 1;
    public IReadOnlyDictionary<string, double> Yields { get; init; } = new Dictionary<string, double>();
    public int Watch { get; init; }

    /// <summary>"" none, "garrison" a manned keep at the charter, "ruin" an old keep to win back, "either" by the seed.</summary>
    public string Keep { get; init; } = "";

    /// <summary>The site when a neighbour holds it ({name} the settlement), or when foes do.</summary>
    public string Held { get; init; } = "";
    public string Foes { get; init; } = "";
}

public sealed record FoundingRules
{
    public string Default { get; init; } = "river-ford";
    public int Offered { get; init; } = 4;

    /// <summary>The roads.json terrain kind of a coastal town's sea.</summary>
    public string Sea { get; init; } = "sea";

    /// <summary>The old keep a crag or a pass may start with in ruins (<see cref="Ruins"/>).</summary>
    public RuinDef? Ruin { get; init; }
    public IReadOnlyList<FoundingDef> Kinds { get; init; } = [];
}

/// <summary>A site offered on New game: its kind and where it lies from the river ford.</summary>
public sealed record FoundingSite(FoundingDef Def, double Bearing);

/// <summary>
/// Where the town is founded (Q7, Q29): New game offers a few sites the seed's country allows, the chosen one shapes the
/// land round the town and its numbers, and the ones not chosen stand on the same map, held by neighbours or by foes.
/// The river ford (the default) changes nothing. Everything here is a pure function of the seed and the choice, on its
/// own dice, so nothing else the seed makes shifts.
/// </summary>
public static class Founding
{
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
    /// The sites New game offers for this world: the default first, then others the country allows, picked by the seed,
    /// each with the bearing it lies on. A town already founded somewhere other than the ford offers the ford as one of them.
    /// </summary>
    public static IReadOnlyList<FoundingSite> Offered(Town town)
    {
        var r = Rules(town.Content);
        var fit = r.Kinds.Where(k => k.Id != r.Default && Allowed(town, k)).ToList();
        var picked = new List<FoundingDef>();
        if (Kind(town.Content, r.Default) is { } first) picked.Add(first);
        for (int i = 0; picked.Count < r.Offered && fit.Count > 0; i++)
        {
            var k = fit[(int)(Hash(town.Seed, 11 + i) * fit.Count)];
            fit.Remove(k);
            picked.Add(k);
        }
        // The chosen site is always among them, so the rest are the ones not taken.
        if (town.Founding is { } own && picked.All(p => p.Id != own) && Kind(town.Content, own) is { } chosen)
            picked[^1] = chosen;
        // Spread round the compass from a turn the seed gives, so no two sites share a road.
        double turn = Hash(town.Seed, 7) * 360;
        return picked.Select((k, i) => new FoundingSite(k, Js.Round((turn + i * 360.0 / picked.Count) % 360 * 10) / 10.0)).ToList();
    }

    private static bool Allowed(Town town, FoundingDef k) =>
        k.Near.Count == 0 || town.Regions.Any(g => g.Ring < k.Within + 1 && k.Near.Contains(g.Theme.Id));

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
    /// At generation, after the rest of the world: the sea and the flanking peaks round the town, and the sites not chosen
    /// given to the neighbours or foes nearest their bearing.
    /// </summary>
    internal static void Place(Town town)
    {
        var def = Of(town);
        if (town.Founding != null)
        {
            if (def.Sea != null) Sea(town, def);
            if (def.Flank) Flank(town, def);
            if (Keep(town) == "ruin") RuinedKeep(town);
        }
        Hold(town);
    }

    /// <summary>The sites not chosen: each goes to the settlement nearest its bearing in the first two rings, or to foes.</summary>
    private static void Hold(Town town)
    {
        foreach (var site in Offered(town).Where(s => s.Def.Id != Of(town).Id))
        {
            var region = town.Regions.Where(g => g.Ring <= 1 && g.Site == null)
                .OrderBy(g => g.SettlementId == null ? 1 : 0)
                .ThenBy(g => Gap(g.Bearing, site.Bearing))
                .ThenBy(g => g.Id)
                .FirstOrDefault();
            if (region != null) region.Site = site.Def.Id;
        }
    }

    private static double Gap(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);

    /// <summary>
    /// The way the site faces, in compass degrees: where its sea lies, the road out of its pass, the open side of a cliff
    /// city or a ledge (the rock rises behind). The kind's bearing turned by the seed.
    /// </summary>
    public static double Facing(Town town) =>
        Of(town).Sea != null ? (Of(town).Bearing + (Hash(town.Seed, 41) - 0.5) * 60 + 360) % 360 : Hash(town.Seed, 43) * 360;

    /// <summary>The site a region holds and who holds it, for its card: null when it holds none.</summary>
    public static string? Describe(Town town, Region region)
    {
        if (region.Site == null || Kind(town.Content, region.Site) is not { } k) return null;
        if (region.SettlementId is int id && town.Settlements[id] is { Razed: false } s)
            return k.Held.Replace("{name}", s.Name, StringComparison.Ordinal);
        return k.Foes;
    }

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
        town.Terrain.RemoveAll(f => f.Inner < outer && f.Outer > sea.Inner && Gap(f.Bearing, bearing) < (f.Arc + sea.Arc) / 2);
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
            town.Terrain.RemoveAll(f => f.Inner < outer && f.Outer > 0.24 && Gap(f.Bearing, bearing) < (f.Arc + 80) / 2);
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
            .Where(s => s.Generated && s.Def.Foe == "" && !s.Def.Aloft && s.Def.Ruin == "" && s.RegionId is int rid && town.Regions[rid].Ring == 0)
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
