using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>Ground types; each territory has one, as in Northgard, and the terrain shader colours by it.</summary>
public enum Biome
{
    Meadow,
    Woodland,
    Hills,
    Crags,
    Marsh,
    Heath,
}

public readonly record struct TreeSpot(float X, float Y, float H, float Scale, byte Kind, float Yaw, float Tint);

/// <summary>
/// The land itself, generated once per town from its seed: a heightmap with hills, mountain
/// chains, lakes and the river; Northgard-style territories (a warped jittered grid of cells, so
/// one seed per cell and the same lookup in C# and in the terrain shader); and forests.
/// Pure presentation: it reads the town and never changes the simulation.
/// </summary>
public sealed class WorldGen
{
    public const int N = Geo.World;
    public const int Cell = 40;
    public const int Cells = (N + Cell - 1) / Cell;
    public const int ChunkTiles = 64;
    public const int Chunks = N / ChunkTiles;
    public const float WaterLevel = 0;
    public const float TownHeight = 2;

    public readonly int Seed;
    public readonly float[] Height = new float[(N + 1) * (N + 1)];
    public readonly float[] Forest = new float[N * N];
    public readonly Vector2[] Jitter = new Vector2[Cells * Cells];
    public readonly Biome[] Biomes = new Biome[Cells * Cells];
    public readonly List<TreeSpot>[] ChunkTrees = new List<TreeSpot>[Chunks * Chunks];

    /// <summary>Ground clutter per chunk: rocks, bushes, grass tufts and flowers (kinds listed in <see cref="Scatter"/>).</summary>
    public readonly List<TreeSpot>[] ChunkScatter = new List<TreeSpot>[Chunks * Chunks];

    public static class Scatter
    {
        public const byte Rock = 0, Boulder = 1, Bush = 2, Tuft = 3, Flowers = 4;
        public const int Kinds = 5;
    }
    public int TownCell { get; private set; }
    public int TreeCount { get; private set; }

    private readonly Noise _n1, _n2, _n3;

    /// <summary>The river's course on the map (the sim's, <see cref="Passage.River"/>), and each vertex's distance from it in tiles.</summary>
    public readonly Vector2[] River;
    private readonly float[] _riverDist = new float[(N + 1) * (N + 1)];

    /// <summary>Its fords and bridges on the map, and which way the water runs there.</summary>
    public readonly List<(Crossing Crossing, Vector2 At, Vector2 Flow)> Crossings = [];

    /// <summary>The sim's hard country on its land grid (<see cref="Country"/>), raised out of the ground here so the roads bend round what you see.</summary>
    private readonly Country.Land _land;

    /// <summary>Islands in the ocean: a centre and a radius in tiles.</summary>
    private readonly List<(Vector2 At, float R)> _isles = [];
    private readonly Town _town;

    public Town Town => _town;

    public WorldGen(int seed, Geo geo, Town town, float treeScale = 1)
    {
        Seed = seed;
        _n1 = new Noise(seed * 31 + 7);
        _n2 = new Noise(seed * 57 + 11);
        _n3 = new Noise(seed * 91 + 23);
        _town = town;
        _land = Country.LandOf(town);
        // The ocean's sites and towns stand on islands: a great one for a city, a small one for a wreck or a cave.
        foreach (var site in town.Sites)
            if (site.RegionId is int rid && town.Regions[rid].Theme.Sea && geo.Pins.TryGetValue(site.Def.Id, out var pin))
                _isles.Add((pin, 9 + Geo.Hash(site.Def.Id.Aggregate(17, (a, c) => a * 31 + c)) * 7));
        foreach (var st in town.Settlements.Where(st => town.Regions[st.RegionId].Theme.Sea))
            _isles.Add((Geo.SettlementPin(town, st), st.Kin.Grows > 0 ? 34 : 16));
        River = Passage.River(town).Select(Geo.FromDays).ToArray();
        TraceRiver();
        foreach (var c in Passage.Crossings(town))
        {
            var at = Geo.FromDays(c.X, c.Y);
            int i = 0;
            for (int k = 1; k < River.Length; k++)
                if (River[k].DistanceSquaredTo(at) < River[i].DistanceSquaredTo(at)) i = k;
            var flow = (River[Math.Min(River.Length - 1, i + 1)] - River[Math.Max(0, i - 1)]).Normalized();
            Crossings.Add((c, at, flow));
        }
        BuildTerritories(geo, town);
        BuildHeight(town.Seed);
        BuildForest(geo, treeScale);
    }

    // ---------- Territories ----------

    /// <summary>The same domain warp as the terrain shader's, so borders wander like coastlines.</summary>
    public static Vector2 Warp(Vector2 p) => p + new Vector2(
        Mathf.Sin(p.Y * 0.045f + 1.7f) + 0.6f * Mathf.Sin(p.X * 0.11f + p.Y * 0.03f),
        Mathf.Sin(p.X * 0.05f + 0.4f) + 0.6f * Mathf.Sin(p.Y * 0.09f - p.X * 0.02f)) * 4.5f;

    public Vector2 SeedOf(int cell) => (new Vector2(cell % Cells, cell / Cells) + Jitter[cell]) * Cell;

    /// <summary>The territory (cell index) a world point lies in.</summary>
    public int TerritoryAt(Vector2 p)
    {
        var q = Warp(p);
        int cx = (int)Mathf.Floor(q.X / Cell), cy = (int)Mathf.Floor(q.Y / Cell);
        int best = 0;
        float bestD = float.MaxValue;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int x = cx + ox, y = cy + oy;
                if (x < 0 || y < 0 || x >= Cells || y >= Cells) continue;
                int c = y * Cells + x;
                float d = SeedOf(c).DistanceSquaredTo(q);
                if (d < bestD)
                {
                    bestD = d;
                    best = c;
                }
            }
        return best;
    }

    private void BuildTerritories(Geo geo, Town town)
    {
        for (int c = 0; c < Jitter.Length; c++)
            Jitter[c] = new Vector2(0.2f + Geo.Hash(Seed * 977 + c * 2) * 0.6f, 0.2f + Geo.Hash(Seed * 977 + c * 2 + 1) * 0.6f);
        // The town sits at the heart of its own territory.
        var q = Warp(Geo.Center);
        int tx = (int)(q.X / Cell), ty = (int)(q.Y / Cell);
        TownCell = ty * Cells + tx;
        Jitter[TownCell] = q / Cell - new Vector2(tx, ty);
        for (int c = 0; c < Biomes.Length; c++)
        {
            var s = SeedOf(c);
            float wet = _n2.Fbm(s.X / 260, s.Y / 260, 3);
            float high = _n3.Fbm(s.X / 320 + 40, s.Y / 320, 3);
            float pick = Geo.Hash(Seed * 31 + c);
            // Open country: meadow, heath, rolling hills and light woods. Crags, marsh and deep forest are the sim's hard country.
            Biomes[c] = high > 0.15f ? Biome.Hills
                : wet > 0.1f ? (pick < 0.75f ? Biome.Woodland : Biome.Meadow)
                : high > 0.15f ? Biome.Hills
                : wet < -0.35f ? Biome.Heath
                : pick < 0.3f ? Biome.Woodland : Biome.Meadow;
        }
        // A desert cliff city stands on bare ground, a ledge or a pass on rock.
        Biomes[TownCell] = town.Founding == null ? Biome.Meadow : Sim.Systems.Founding.Of(town).Look switch
        {
            "desert" => Biome.Heath,
            "ledge" or "pass" => Biome.Hills,
            _ => Biome.Meadow,
        };
        // A site's habitat decides its territory's ground.
        foreach (var site in town.Sites)
        {
            if (!geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            var biome = site.Def.Habitats.Select(HabitatBiome).FirstOrDefault(b => b != null);
            if (biome is Biome b) Biomes[TerritoryAt(pin)] = b;
        }
        // Hard country colours the territories it covers.
        for (int c = 0; c < Biomes.Length; c++)
        {
            if (c == TownCell || FeatureAt(SeedOf(c)) is not var (f, k) || k < 0.35f) continue;
            if (FeatureBiome(f.Kind.Id) is Biome b) Biomes[c] = b;
        }
    }

    // ---------- Hard country ----------

    public static Biome? FeatureBiome(string kind) => kind switch
    {
        "ridge" or "icefield" or "peaks" or "range" => Biome.Crags,
        "mire" => Biome.Marsh,
        "tangle" or "beastland" => Biome.Woodland,
        "monsterland" => Biome.Hills,
        "barrows" or "waste" => Biome.Heath,
        _ => null,
    };

    /// <summary>
    /// The feature a point lies in and how deep inside it (0 at a ragged edge to 1 a few tiles in), or
    /// null. Edges wander with noise so a ridge or a mire never reads as a ruled shape.
    /// </summary>
    public (Feature F, float K)? FeatureAt(Vector2 p) => FeatureAt(p, out _);

    /// <summary>As <see cref="FeatureAt(Vector2)"/>, with how far toward its deepest line the point lies (0 at the edge, 1 on the line).</summary>
    public (Feature F, float K)? FeatureAt(Vector2 p, out float core)
    {
        core = 0;
        var d = p - Geo.Center;
        float rw = d.Length();
        if (rw < Geo.Clearing) return null;
        var (x, y) = Geo.ToDays(p);
        var (f, depth, deepest) = _land.Lie(x, y);
        if (f == null) return null;
        double inside = depth * Geo.TilesPerDay + _n2.Fbm(p.X / 22 + f.Id * 7, p.Y / 22, 3) * 4;
        // The ocean's shore wanders far more: bays and headlands, never the ruled edge of a ring.
        if (f.Kind.Id == "ocean") inside += _n3.Fbm(p.X / 90, p.Y / 90, 3) * 30;
        if (inside <= -3) return null;
        core = deepest > 0 ? Mathf.Clamp((float)(depth / deepest), 0, 1) : 0;
        return (f, Mathf.SmoothStep(-3, 12, (float)inside));
    }

    /// <summary>How the hard country reshapes the ground at a point.</summary>
    private float FeatureHeight(Vector2 p, float h)
    {
        if (FeatureAt(p, out float spine) is not var (f, k) || k <= 0) return h;
        switch (f.Kind.Id)
        {
            case "ridge":
                // Highest along its spine, falling to its edges.
                return h + Mathf.SmoothStep(0, 1, k) * (6 + 14 * spine + 26 * Mathf.Pow(_n2.Ridge(p.X / 45 + f.Id, p.Y / 45, 4), 1.3f));
            case "icefield":
                return h + k * (4 + 6 * _n1.Fbm(p.X / 40, p.Y / 40, 3));
            case "mire":
                return Mathf.Lerp(h, 0.25f + _n1.Fbm(p.X / 14, p.Y / 14, 3) * 1.2f, k);
            case "gorge":
            {
                // A ravine down the middle of the band, water at the bottom.
                float core = Mathf.SmoothStep(0.45f, 0.85f, spine);
                return Mathf.Lerp(h, -1.4f, core * k) + k * (1 - core) * 3;
            }
            case "sea":
                // The open sea: deep, with a long shelving beach.
                return Mathf.Lerp(h, -2.4f + _n1.Fbm(p.X / 30, p.Y / 30, 2) * 0.5f, Mathf.SmoothStep(0, 0.45f, k));
            case "ocean":
            {
                // The ocean: deeper than any bay, broken by the islands its sites and towns stand on.
                float sea = Mathf.Lerp(h, -3.2f + _n1.Fbm(p.X / 40, p.Y / 40, 2) * 0.8f, Mathf.SmoothStep(0, 0.45f, k));
                float isle = IsleAt(p);
                return isle <= 0 ? sea : Mathf.Max(sea, Mathf.Lerp(-1.5f, 1.4f + _n2.Fbm(p.X / 12, p.Y / 12, 3) * 2.2f, isle));
            }
            case "lake":
                // Open water with a shelving shore: deep enough that nobody wades it.
                return Mathf.Lerp(h, -1.6f + _n1.Fbm(p.X / 18, p.Y / 18, 2) * 0.4f, Mathf.SmoothStep(0, 0.6f, k));
            case "peaks":
            case "range":
                // Sheer summits: cliffs no road climbs, highest along the chain's spine.
                return h + Mathf.SmoothStep(0, 1, k) * (20 + 18 * spine + 40 * Mathf.Pow(_n2.Ridge(p.X / 30 + f.Id, p.Y / 30, 4), 1.1f));
            case "barrows":
                return h + k * 1.2f * Mathf.Max(0, _n1.Get(p.X / 6, p.Y / 6));
            case "beastland":
                // Rolling wild country, long grass and thickets.
                return h + k * 1.6f * Mathf.Max(0, _n2.Get(p.X / 16, p.Y / 16));
            case "monsterland":
                // Broken ground full of hollows and dens.
                return h + k * (2.4f * Mathf.Abs(_n3.Get(p.X / 11, p.Y / 11)) - 0.8f);
            case "waste":
                return Mathf.Lerp(h, Mathf.Max(h, 1.5f), k) + k * Mathf.Abs(_n3.Get(p.X / 9, p.Y / 9)) * 2;
            default:
                return h;
        }
    }

    /// <summary>How far up an island a point is: 0 off every island, 1 at a centre; the shore wanders with noise.</summary>
    private float IsleAt(Vector2 p)
    {
        float best = 0;
        foreach (var (at, r) in _isles)
        {
            float reach = r * 1.4f;
            if (Mathf.Abs(p.X - at.X) > reach || Mathf.Abs(p.Y - at.Y) > reach) continue;
            float d = p.DistanceTo(at) / r + _n3.Fbm(p.X / 9, p.Y / 9, 2) * 0.35f;
            best = Mathf.Max(best, 1 - Mathf.SmoothStep(0.55f, 1.15f, d));
        }
        return best;
    }

    /// <summary>Forest bias the hard country adds: thick in a tangled wood, none on bare rock or waste.</summary>
    private float FeatureForest(Vector2 p, float bias)
    {
        if (FeatureAt(p) is not var (f, k) || k <= 0) return bias;
        return f.Kind.Id switch
        {
            "tangle" => bias + 0.75f * k,
            "ocean" => Mathf.Lerp(bias, -1, k * (1 - IsleAt(p))),
            "ridge" or "icefield" or "waste" or "peaks" or "range" or "lake" or "sea" => Mathf.Lerp(bias, -1, k),
            "beastland" => bias + 0.3f * k,
            "monsterland" => Mathf.Lerp(bias, -0.2f, k),
            "mire" => Mathf.Lerp(bias, -0.05f, k),
            "barrows" => Mathf.Lerp(bias, -0.5f, k),
            _ => bias,
        };
    }

    public static Biome? HabitatBiome(string habitat) => habitat switch
    {
        "forest" => Biome.Woodland,
        "swamp" or "coast" => Biome.Marsh,
        "hills" or "caves" => Biome.Hills,
        "mountain" or "mountains" or "underworld" => Biome.Crags,
        "grassland" => Biome.Meadow,
        "ruins" or "temple" or "tomb" => Biome.Heath,
        _ => null,
    };

    // ---------- Height ----------

    /// <summary>Each vertex's distance from the river, out to a few tiles (beyond that it is far enough not to matter).</summary>
    private void TraceRiver()
    {
        Array.Fill(_riverDist, 99f);
        const float Reach = 8;
        for (int k = 1; k < River.Length; k++)
        {
            Vector2 a = River[k - 1], b = River[k];
            int x0 = Math.Max(0, (int)(Mathf.Min(a.X, b.X) - Reach)), x1 = Math.Min(N, (int)(Mathf.Max(a.X, b.X) + Reach) + 1);
            int y0 = Math.Max(0, (int)(Mathf.Min(a.Y, b.Y) - Reach)), y1 = Math.Min(N, (int)(Mathf.Max(a.Y, b.Y) + Reach) + 1);
            var ab = b - a;
            float l2 = Mathf.Max(1e-6f, ab.LengthSquared());
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x, y);
                    float t = Mathf.Clamp((p - a).Dot(ab) / l2, 0, 1);
                    float d = p.DistanceTo(a + ab * t);
                    int i = y * (N + 1) + x;
                    if (d < _riverDist[i]) _riverDist[i] = d;
                }
        }
    }

    /// <summary>Distance from the river in tiles at a point (99 when far from it).</summary>
    public float RiverDistance(Vector2 p)
    {
        int x = Mathf.Clamp((int)Mathf.Round(p.X), 0, N), y = Mathf.Clamp((int)Mathf.Round(p.Y), 0, N);
        return _riverDist[y * (N + 1) + x];
    }

    /// <summary>How shallow the river runs at a point: 1 at a ford's stones, 0 in the deep channel.</summary>
    private float FordAt(Vector2 p)
    {
        float best = 0;
        foreach (var (c, at, _) in Crossings)
            if (c.Kind == "ford") best = Mathf.Max(best, 1 - Mathf.SmoothStep(1.5f, 3.5f, p.DistanceTo(at)));
        return best;
    }

    private float RawHeight(float x, float y)
    {
        // Rolling open country: every hill a party has to go round is the sim's (Country), so nothing else stands high.
        float h = 3.2f + _n1.Fbm(x / 170, y / 170, 5) * 4.5f;
        float swell = Mathf.SmoothStep(0.25f, 0.65f, _n3.Fbm(x / 320 + 40, y / 320, 3));
        if (swell > 0) h += swell * Mathf.Pow(_n2.Ridge(x / 150, y / 150, 4), 2) * 5;
        h += _n2.Get(x / 23, y / 23) * 0.8f;
        // No pond or lake the roads don't know of: open water is the sim's (its lakes and the river) or the sea at the world's end.
        h = Mathf.Max(h, 0.6f);
        // The land falls away near the map's edge so the world ends in sea, not a cliff.
        float edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(N - x, N - y));
        h = Mathf.Lerp(-3, h, Mathf.SmoothStep(0, 40, edge));
        return h;
    }

    private void BuildHeight(int townSeed)
    {
        for (int y = 0; y <= N; y++)
            for (int x = 0; x <= N; x++)
            {
                var p = new Vector2(x, y);
                float h = RawHeight(x, y);
                // The town was founded on dry ground by the ford: level round the hall, with the gentle hills
                // the town plan knows (Hills), so lanes and buildings sit on the same slopes the sim sited them on.
                float d = p.DistanceTo(Geo.Center);
                float flat = Mathf.SmoothStep(Geo.Clearing, Geo.Clearing + 30, d);
                float town = TownHeight + (float)Sim.Systems.Hills.Height(townSeed, x - Geo.Margin, y - Geo.Margin);
                h = Mathf.Lerp(town, Mathf.Max(h, d < Geo.Clearing + 30 ? 1.2f : h), flat);
                h = FeatureHeight(p, h);
                // Plateaus and their cliffs in the near wilds (the sim's Cliffs: walkers go round them or up the ramps).
                h += (float)Sim.Systems.Cliffs.Height(_town, x - Geo.Margin, y - Geo.Margin);
                h += SiteHeight(p, d);
                float dr = _riverDist[y * (N + 1) + x];
                // Shallow at the fords, stones showing through; deep in the channel everywhere else.
                if (dr < 6) h = Mathf.Min(h, Mathf.Lerp(Mathf.Lerp(-1.1f, -0.12f, FordAt(p)), h, Mathf.SmoothStep(0.6f, 5.5f, dr)));
                Height[y * (N + 1) + x] = h;
            }
    }

    /// <summary>
    /// What the town's founding site (<see cref="Sim.Systems.Founding"/>) does to the ground: a crag or a plateau lifts the
    /// town above the plain (the plateau with one ramp down on the side it faces), a ledge sits high with the mountain rising
    /// behind it, a desert city has its sandstone cliff at its back. The river still cuts down through it all.
    /// </summary>
    private float SiteHeight(Vector2 p, float d)
    {
        if (_town.Founding == null) return 0;
        var off = p - Geo.Center;
        float bearing = Mathf.RadToDeg(Mathf.Atan2(off.X, -off.Y));
        float face = (float)Sim.Systems.Founding.Facing(_town);
        // 0 on the side the site faces, 180 at its back.
        float gap = Mathf.Abs(Mathf.PosMod(bearing - face + 540, 360) - 180);
        float rag = _n2.Fbm(p.X / 26, p.Y / 26, 3) * 5;
        float rim = Geo.Clearing + rag;
        float behind = Mathf.SmoothStep(100, 150, gap);
        // The high ground stops at a cliff over the river instead of the river cutting through it.
        float bank = Mathf.SmoothStep(2.5f, 7, RiverDistance(p));
        switch (Sim.Systems.Founding.Of(_town).Look)
        {
            case "crag":
                return 9 * bank * (1 - Mathf.SmoothStep(rim + 4, rim + 11, d));
            case "plateau":
            {
                // Sheer all round but for the ramp on the side it faces.
                float ramp = 1 - Mathf.SmoothStep(8, 16, gap);
                return 14 * bank * (1 - Mathf.SmoothStep(rim + 10, rim + 13 + ramp * 36, d));
            }
            case "ledge":
            {
                float top = 20 * bank * (1 - Mathf.SmoothStep(rim + 6, rim + 9, d));
                float wall = behind * Mathf.SmoothStep(rim + 2, rim + 30, d) * (40 + 30 * _n2.Ridge(p.X / 40, p.Y / 40, 3));
                return top + wall;
            }
            case "desert":
                return behind * Mathf.SmoothStep(rim, rim + 8, d) * (22 + 6 * _n1.Fbm(p.X / 18, p.Y / 18, 2));
            default:
                return 0;
        }
    }

    public float HeightAt(float x, float y)
    {
        x = Mathf.Clamp(x, 0, N - 0.001f);
        y = Mathf.Clamp(y, 0, N - 0.001f);
        int ix = (int)x, iy = (int)y;
        float fx = x - ix, fy = y - iy;
        int i = iy * (N + 1) + ix;
        float a = Height[i], b = Height[i + 1], c = Height[i + N + 1], d = Height[i + N + 2];
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    /// <summary>Ground height, never below the water's surface: where things stand or float.</summary>
    public float SurfaceAt(float x, float y) => Mathf.Max(WaterLevel, HeightAt(x, y));

    public Vector3 At(Vector2 p, float lift = 0) => new(p.X, SurfaceAt(p.X, p.Y) + lift, p.Y);

    public Vector3 NormalAt(int x, int y)
    {
        float l = Height[y * (N + 1) + Math.Max(0, x - 1)], r = Height[y * (N + 1) + Math.Min(N, x + 1)];
        float u = Height[Math.Max(0, y - 1) * (N + 1) + x], d = Height[Math.Min(N, y + 1) * (N + 1) + x];
        return new Vector3(l - r, 2, u - d).Normalized();
    }

    // ---------- Forest ----------

    /// <summary>Rocks on the heights and screes, bushes at the forest's edge, grass tufts and flowers in the open.</summary>
    private void Clutter(Vector2 p, float h, float f, Biome biome, Vector3 nrm, Vector2[] pins)
    {
        if (h < 0.5f) return;
        foreach (var pin in pins)
            if (p.DistanceSquaredTo(pin) < 36) return;
        int seed = Seed * 104729 + (int)p.Y * N + (int)p.X;
        float r = Geo.Hash(seed);
        // On the town's grid the lots may be built on at any time: only sparse grass, which the buildings hide.
        if (Mathf.Abs(p.X - Geo.Center.X) < Geo.Grid / 2 + 1 && Mathf.Abs(p.Y - Geo.Center.Y) < Geo.Grid / 2 + 1)
        {
            if (r < 0.3f) ChunkAdd(p, seed, Scatter.Tuft);
            return;
        }
        float rocky = biome is Biome.Crags ? 0.12f : biome is Biome.Hills ? 0.03f : 0.008f;
        if (nrm.Y < 0.85f) rocky += 0.08f;
        byte kind;
        if (r < rocky) kind = Geo.Hash(seed + 1) < 0.2f ? Scatter.Boulder : Scatter.Rock;
        else if (r < rocky + (f > 0.05f && f < 0.6f ? 0.16f : 0.01f)) kind = Scatter.Bush;
        else if (f < 0.3f && r < rocky + 0.6f && biome is not Biome.Crags) kind = biome == Biome.Meadow && Geo.Hash(seed + 2) < 0.08f ? Scatter.Flowers : Scatter.Tuft;
        else return;
        ChunkAdd(p, seed, kind);
    }

    private void ChunkAdd(Vector2 p, int seed, byte kind)
    {
        float x = p.X - 0.35f + Geo.Hash(seed + 3) * 0.7f, y = p.Y - 0.35f + Geo.Hash(seed + 4) * 0.7f;
        int chunk = Math.Min(Chunks - 1, (int)y / ChunkTiles) * Chunks + Math.Min(Chunks - 1, (int)x / ChunkTiles);
        ChunkScatter[chunk].Add(new TreeSpot(x, y, HeightAt(x, y), 0.7f + Geo.Hash(seed + 5) * 0.6f, kind, Geo.Hash(seed + 6) * Mathf.Tau, Geo.Hash(seed + 7)));
    }

    private void BuildForest(Geo geo, float treeScale)
    {
        for (int i = 0; i < ChunkTrees.Length; i++)
        {
            ChunkTrees[i] = [];
            ChunkScatter[i] = [];
        }
        // Sites, and the strange places and gateway arches of the wilds (Wayfaring), stand in clearings of their own.
        var pins = geo.Pins.Values
            .Concat(_town.Terrain.Where(t => t.Kind.Wonder != null).Select(t => Geo.FromDays(Roads.Middle(t))))
            .Concat(_town.Gateways.SelectMany(g => new[] { Geo.FromDays(g.Ax, g.Ay), Geo.FromDays(g.Bx, g.By) }))
            .ToArray();
        int count = 0;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float h = HeightAt(p.X, p.Y);
                var biome = Biomes[TerritoryAt(p)];
                float bias = biome switch
                {
                    // Open woodland: copses and spinneys a party walks through; the deep woods are the sim's tangles.
                    Biome.Woodland => 0.12f,
                    Biome.Marsh => 0.05f,
                    Biome.Crags => 0.0f,
                    Biome.Hills => -0.12f,
                    Biome.Heath => -0.32f,
                    _ => -0.2f,
                };
                bias = FeatureForest(p, bias);
                float f = Mathf.SmoothStep(-0.05f, 0.4f, _n3.Fbm(p.X / 75, p.Y / 75, 4) + bias);
                // No forest on water, bare peaks, steep scree, or the town's own clearing.
                if (h < 0.4f || h > 36) f = 0;
                f *= Mathf.SmoothStep(Geo.Clearing, Geo.Clearing + 22, p.DistanceTo(Geo.Center));
                foreach (var pin in pins) f *= Mathf.SmoothStep(4, 9, p.DistanceTo(pin));
                var nrm = NormalAt(x, y);
                if (nrm.Y < 0.72f) f *= 0.3f;
                Forest[y * N + x] = f;
                Clutter(p, h, f, biome, nrm, pins);
                if (f <= 0.02f) continue;
                int hseed = Seed * 7919 + y * N + x;
                int trees = (Geo.Hash(hseed) < f * 0.9f * treeScale ? 1 : 0) + (f > 0.6f && Geo.Hash(hseed + 1) < (f - 0.6f) * 2 * treeScale ? 1 : 0)
                    + (treeScale > 1 && Geo.Hash(hseed + 9) < f * (treeScale - 1) ? (int)treeScale : 0);
                for (int t = 0; t < trees; t++)
                {
                    float tx = x + 0.15f + Geo.Hash(hseed * 3 + t * 5 + 2) * 0.7f, ty = y + 0.15f + Geo.Hash(hseed * 3 + t * 5 + 3) * 0.7f;
                    float th = HeightAt(tx, ty);
                    if (th < 0.3f) continue;
                    float r = Geo.Hash(hseed * 3 + t * 5 + 4);
                    // Pines on the heights and in the crags; broadleaf in the lowland woods and the marsh.
                    float pineOdds = biome switch { Biome.Crags => 0.95f, Biome.Hills => 0.7f, Biome.Woodland => 0.5f, Biome.Marsh => 0.1f, _ => 0.35f } + th / 60;
                    byte kind = r < pineOdds ? (byte)(Geo.Hash(hseed + t * 11) < 0.5f ? 0 : 1) : (byte)(biome == Biome.Marsh ? 3 : 2);
                    float scale = 0.75f + Geo.Hash(hseed * 5 + t) * 0.6f;
                    int chunk = Math.Min(Chunks - 1, (int)ty / ChunkTiles) * Chunks + Math.Min(Chunks - 1, (int)tx / ChunkTiles);
                    ChunkTrees[chunk].Add(new TreeSpot(tx, ty, th, scale, kind, Geo.Hash(hseed + t * 3 + 7) * Mathf.Tau, Geo.Hash(hseed + t * 3 + 8)));
                    count++;
                }
            }
        TreeCount = count;
        foreach (var list in ChunkScatter) list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
        // Back to front within each chunk, so instances draw in a stable order.
        foreach (var list in ChunkTrees) list.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
    }
}
