using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>The shared materials every world mesh uses; their uniforms are set once per frame.</summary>
public static class Mats
{
    public static readonly ShaderMaterial Terrain = Make("terrain");
    public static readonly ShaderMaterial Water = Make("water");
    public static readonly ShaderMaterial Props = Make("props");
    public static readonly ShaderMaterial People = Make("props");
    public static readonly ShaderMaterial Foliage = Make("foliage");

    /// <summary>The heroes (<see cref="Heroes"/>), their enchanted weapons' auras, and combat effects (<see cref="Fx"/>).</summary>
    public static readonly ShaderMaterial Hero = Make("hero");
    public static readonly ShaderMaterial Aura = Make("aura");
    public static readonly ShaderMaterial Effects = Make("fx");

    /// <summary>Mist banks and falling water (<see cref="TownView"/> scenery): soft sheets, one shader, two modes.</summary>
    public static readonly ShaderMaterial Mist = Make("veil");
    public static readonly ShaderMaterial Falls = Make("veil");

    /// <summary>Every creature, one mesh per kind (<see cref="Beasts"/>).</summary>
    public static readonly ShaderMaterial Beast = Make("beast");
    public static readonly ShaderMaterial[] All = [Terrain, Water, Props, People, Foliage, Hero, Aura, Effects, Mist, Falls, Beast];

    private static ShaderMaterial Make(string name) => new() { Shader = GD.Load<Shader>($"res://assets/shaders/{name}.gdshader") };

    static Mats()
    {
        People.SetShaderParameter("hide_in_strategic", 1.0);
        Falls.SetShaderParameter("mode", 1.0);
    }

    public static void SetAll(string param, Variant v)
    {
        foreach (var m in All)
        {
            m.SetShaderParameter(param, v);
            if (m.NextPass is ShaderMaterial pass) pass.SetShaderParameter(param, v);
        }
    }

    /// <summary>A seamless RGBA noise texture, each channel a different scale, for painterly variation.</summary>
    public static ImageTexture NoiseTexture()
    {
        const int S = 256;
        var img = Image.CreateEmpty(S, S, false, Image.Format.Rgba8);
        var layers = new Image[4];
        for (int c = 0; c < 4; c++)
        {
            var n = new FastNoiseLite { Seed = 17 + c * 31, Frequency = 0.012f + c * 0.006f, NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin, FractalOctaves = 4 };
            layers[c] = n.GetSeamlessImage(S, S);
        }
        var data = new byte[S * S * 4];
        for (int c = 0; c < 4; c++)
        {
            var src = layers[c].GetData();
            int stride = src.Length / (S * S);
            for (int i = 0; i < S * S; i++) data[i * 4 + c] = src[i * stride];
        }
        img.SetData(S, S, false, Image.Format.Rgba8, data);
        img.GenerateMipmaps();
        return ImageTexture.CreateFromImage(img);
    }
}

/// <summary>
/// The land: terrain chunks with three levels of detail, water where the ground dips below the
/// surface, and the forests as instanced trees per chunk. Godot's visibility ranges pick the level
/// of detail and drop distant trees; from far away the terrain shader paints the forest instead.
/// </summary>
public partial class WorldView : Node3D
{
    public WorldGen Gen { get; private set; } = null!;
    public ImageTexture CellTex { get; private set; } = null!;
    public ImageTexture StateTex { get; private set; } = null!;
    public ImageTexture DataTex { get; private set; } = null!;
    private readonly byte[] _state = new byte[WorldGen.Cells * WorldGen.Cells * 4];
    private Image _stateImg = null!;
    public int TreeInstances { get; private set; }

    /// <summary>Distances (from the camera) at which terrain and trees change detail.</summary>
    private static readonly float[] TerrainLod = [150, 420];
    private const float TreeNear = 120, TreeFar = 520;

    // Meadow, woodland, hills, crags, marsh, heath: a base and an alternate tone each, in sRGB.
    private static readonly Vector3[] BiomeA = [V("6a8646"), V("4f6e3a"), V("8f9a55"), V("7d7a6e"), V("5a6c44"), V("857650")];
    private static readonly Vector3[] BiomeB = [V("8a9852"), V("5f7a42"), V("a3a260"), V("918c7e"), V("4b6040"), V("7a6664")];

    private static Vector3 V(string hex)
    {
        var c = new Color(hex);
        return new Vector3(c.R, c.G, c.B);
    }

    public void Build(WorldGen gen, float treeScale = 1)
    {
        foreach (var c in GetChildren()) c.QueueFree();
        Gen = gen;
        BuildTextures();
        var noise = Mats.NoiseTexture();
        Mats.SetAll("noise_tex", noise);
        Mats.SetAll("world_size", (float)WorldGen.N);
        var t = Mats.Terrain;
        t.SetShaderParameter("cell_tex", CellTex);
        t.SetShaderParameter("state_tex", StateTex);
        t.SetShaderParameter("data_tex", DataTex);
        t.SetShaderParameter("cells", (float)WorldGen.Cells);
        t.SetShaderParameter("cell_size", (float)WorldGen.Cell);
        t.SetShaderParameter("biome_a", BiomeA);
        t.SetShaderParameter("biome_b", BiomeB);
        Mats.Water.SetShaderParameter("data_tex", DataTex);
        _trailImg = Image.CreateEmpty(TrailRes, TrailRes, false, Image.Format.L8);
        TrailTex = ImageTexture.CreateFromImage(_trailImg);
        t.SetShaderParameter("trail_tex", TrailTex);
        for (int cy = 0; cy < WorldGen.Chunks; cy++)
            for (int cx = 0; cx < WorldGen.Chunks; cx++)
            {
                BuildChunk(cx, cy);
                BuildTrees(cx, cy);
                BuildScatter(cx, cy);
            }
        BuildCliffs();
        BuildSiteCliff();
    }

    /// <summary>
    /// Rock faces where the town's founding site drops away (<see cref="Sim.Systems.Founding"/>): round a crag, a plateau or a
    /// ledge, wherever the ground falls steeply out from the town, and none down a ramp or a gentle slope.
    /// </summary>
    /// <summary>A stretch of the founding site's cliff: its top and foot on the map, the way out, and how far it drops.</summary>
    public readonly record struct Face(Vector2 Top, Vector2 Foot, Vector2 Dir, float High);

    /// <summary>The founding site's cliff faces, found once (<see cref="BuildSiteCliff"/>); waterfalls and lift baskets go on them.</summary>
    public List<Face> SiteFaces { get; } = [];

    private void BuildSiteCliff()
    {
        SiteFaces.Clear();
        if (Gen.Town.Founding == null) return;
        var kit = new MeshKit();
        Color[] stones = [new("8c8172"), new("a39785"), new("74695d"), new("968a77")];
        float from = Geo.Clearing - 6, to = Geo.Clearing + 60;
        int n = (int)(Mathf.Tau * (Geo.Clearing + 14) / 0.9f);
        for (int k = 0; k < n; k++)
        {
            float a = k * Mathf.Tau / n;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            // The steepest fall along this line out from the town.
            float best = 0, at = 0;
            for (float r = from; r < to; r += 1)
            {
                var p = Geo.Center + dir * r;
                float fall = Gen.HeightAt(p.X, p.Y) - Gen.HeightAt(p.X + dir.X * 3, p.Y + dir.Y * 3);
                if (fall > best)
                {
                    best = fall;
                    at = r;
                }
            }
            if (best < 2.5f) continue;
            var top = Geo.Center + dir * at;
            var foot = Geo.Center + dir * (at + 3);
            float baseY = Gen.HeightAt(foot.X, foot.Y) - 0.3f, high = Gen.HeightAt(top.X, top.Y) - baseY;
            SiteFaces.Add(new Face(top, foot, dir, high));
            var mid = Geo.Center + dir * (at + 1.6f);
            int seed = 9001 + k;
            var col = stones[(int)(Geo.Hash(seed * 7 + 2) * stones.Length) % stones.Length];
            float w = 0.6f + Geo.Hash(seed * 7 + 1) * 0.5f;
            kit.Blob(new Vector3(mid.X, baseY + high * 0.3f, mid.Y), new Vector3(w * 1.2f, high * 0.36f, w), col, 0.3f, seed, col.Darkened(0.2f));
            var up = mid - dir * 0.5f;
            kit.Blob(new Vector3(up.X, baseY + high * 0.7f, up.Y), new Vector3(w, high * 0.27f, w * 0.85f), col.Lightened(0.08f), 0.3f, seed + 1, col.Darkened(0.15f));
        }
        if (kit.Triangles == 0) return;
        AddChild(new MeshInstance3D { Mesh = kit.Build(), MaterialOverride = Mats.Props });
    }

    /// <summary>
    /// Rock along every plateau's cliff (the sim's <see cref="Cliffs"/>): stacked faceted columns standing in the face from its foot
    /// to its top, and fallen stones at the foot, left open where a ramp comes down. The heightmap gives the rise; this gives it a face.
    /// </summary>
    private void BuildCliffs()
    {
        var town = Gen.Town;
        var rules = town.Content.Passage.Cliffs;
        var kit = new MeshKit();
        Color[] stones = [new("8c8172"), new("a39785"), new("74695d"), new("968a77")];
        int seed = 0;
        foreach (var (p, b) in Cliffs.Of(town).Zip(Cliffs.Barriers(town)))
        {
            var loop = b.Loop;
            float len = 0;
            for (int i = 0; i < loop.Length; i++) len += (float)Math.Sqrt(Math.Pow(loop[(i + 1) % loop.Length].X - loop[i].X, 2) + Math.Pow(loop[(i + 1) % loop.Length].Y - loop[i].Y, 2));
            int n = (int)(len / 0.95f);
            for (int k = 0; k < n; k++)
            {
                seed++;
                double a = k * Math.PI * 2 / n;
                double cx = Math.Cos(a), cy = Math.Sin(a), e = p.EdgeAt(a);
                // Open where a ramp comes down.
                bool ramp = p.Ramps.Any(r => Math.Abs(Math.Sin(a - r)) * e < rules.RampWidth + 0.5 && Math.Cos(a - r) > 0);
                if (ramp) continue;
                var at = new Vector2((float)(p.X + cx * (e + 0.1)) + Geo.Margin, (float)(p.Y + cy * (e + 0.1)) + Geo.Margin);
                var foot = new Vector2((float)(p.X + cx * (e + rules.Face)) + Geo.Margin, (float)(p.Y + cy * (e + rules.Face)) + Geo.Margin);
                float baseY = Gen.HeightAt(foot.X, foot.Y) - 0.3f, top = Gen.HeightAt((float)(p.X + cx * (e - 1)) + Geo.Margin, (float)(p.Y + cy * (e - 1)) + Geo.Margin);
                // Never taller than the plateau stands: a column at the foot of other high ground keeps to its own cliff.
                float high = Mathf.Clamp(top - baseY, 0.6f, (float)p.H + 1.5f), j = Geo.Hash(seed * 7 + 1);
                var col = stones[(int)(Geo.Hash(seed * 7 + 2) * stones.Length) % stones.Length];
                // A column in two stacked blocks, the upper set back a little, its crown just under the grass of the top.
                float r = 0.5f + j * 0.5f;
                kit.Blob(new Vector3(at.X, baseY + high * 0.3f, at.Y), new Vector3(r * 1.15f, high * 0.36f, r), col, 0.3f, seed, col.Darkened(0.2f));
                var up = at - new Vector2((float)cx, (float)cy) * 0.22f;
                kit.Blob(new Vector3(up.X, baseY + high * 0.68f, up.Y), new Vector3(r * 0.95f, high * 0.26f, r * 0.85f), col.Lightened(0.08f), 0.3f, seed + 1, col.Darkened(0.15f));
                if (Geo.Hash(seed * 7 + 3) < 0.45f)
                {
                    var fall = foot + new Vector2((float)cx, (float)cy) * (0.3f + Geo.Hash(seed * 7 + 4) * 0.8f) + new Vector2((float)-cy, (float)cx) * (Geo.Hash(seed * 7 + 5) - 0.5f);
                    float s = 0.18f + Geo.Hash(seed * 7 + 6) * 0.25f;
                    kit.Blob(new Vector3(fall.X, Gen.HeightAt(fall.X, fall.Y) + s * 0.5f, fall.Y), new Vector3(s, s * 0.8f, s), col.Darkened(0.05f), 0.4f, seed + 2);
                }
            }
        }
        if (kit.Triangles == 0) return;
        AddChild(new MeshInstance3D { Mesh = kit.Build(), MaterialOverride = Mats.Props });
    }

    private void BuildTextures()
    {
        var g = Gen;
        const int C = WorldGen.Cells;
        var cells = new byte[C * C * 4];
        for (int i = 0; i < C * C; i++)
        {
            byte jx = (byte)Mathf.RoundToInt(g.Jitter[i].X * 255), jy = (byte)Mathf.RoundToInt(g.Jitter[i].Y * 255);
            // The shader reads these bytes back, so the CPU lookup uses the same rounded seeds.
            g.Jitter[i] = new Vector2(jx / 255f, jy / 255f);
            cells[i * 4] = jx;
            cells[i * 4 + 1] = jy;
            cells[i * 4 + 2] = (byte)Mathf.RoundToInt((int)g.Biomes[i] * 255f / 8);
            cells[i * 4 + 3] = 255;
        }
        CellTex = ImageTexture.CreateFromImage(Image.CreateFromData(C, C, false, Image.Format.Rgba8, cells));
        _stateImg = Image.CreateFromData(C, C, false, Image.Format.Rgba8, _state);
        StateTex = ImageTexture.CreateFromImage(_stateImg);
        const int N = WorldGen.N;
        var data = new byte[N * N * 4];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                int i = y * N + x;
                data[i * 4] = (byte)(g.Forest[i] * 255);
                data[i * 4 + 1] = (byte)(Mathf.Clamp(-g.HeightAt(x + 0.5f, y + 0.5f) / 2.5f, 0, 1) * 255);
                data[i * 4 + 3] = 255;
            }
        DataTex = ImageTexture.CreateFromImage(Image.CreateFromData(N, N, false, Image.Format.Rgba8, data));
    }

    private const int TrailRes = 1024;
    private Image _trailImg = null!;
    private readonly byte[] _trail = new byte[TrailRes * TrailRes];

    /// <summary>The wilds' worn trails (<see cref="Country.Tracks"/>) as a mask over the world, for the terrain shader.</summary>
    public ImageTexture TrailTex { get; private set; } = null!;

    /// <summary>
    /// Draw the worn trails: the roads the companies of the last months actually walked, wherever the ground under them is
    /// worn enough to show (<see cref="Country.Tracks"/>), as worn as it is there.
    /// </summary>
    public void SetTrails(CrownAndCompass.Sim.Town town)
    {
        Array.Clear(_trail);
        var land = Country.LandOf(town);
        var t = Country.Rules(town).Trail;
        float k = TrailRes / (float)WorldGen.N;
        // The last weeks' roads: older ones wandered before the tracks drew together.
        int since = town.Tick - 45 * CrownAndCompass.Sim.SimTime.HoursPerDay;
        var lines = town.Parties.Where(p => p.DepartTick >= since && p.Resolved).SelectMany(p => Roads.Walked(town, p))
            .Concat(town.Surveys.Where(s => s.DepartTick >= since).SelectMany(s => Explore.Walked(town, s)));
        foreach (var line in lines)
            for (int i = 1; i < line.Count; i++)
            {
                var (ax, ay) = line[i - 1];
                var (bx, by) = line[i];
                var a = Geo.FromDays(ax, ay) * k;
                var b = Geo.FromDays(bx, by) * k;
                int steps = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) * 2));
                for (int j = 0; j <= steps; j++)
                {
                    float f = j / (float)steps;
                    var p = a.Lerp(b, f);
                    // The town's own lanes are its own; out in the wilds only ground worn enough shows.
                    if ((p / k).DistanceTo(Geo.Center) < Geo.Clearing + 8) continue;
                    int c = land.Index(ax + (bx - ax) * f, ay + (by - ay) * f);
                    if (c < 0 || !town.Trails.TryGetValue(c, out double w) || w < t.Shown) continue;
                    int px = (int)p.X, py = (int)p.Y;
                    if (px < 0 || py < 0 || px >= TrailRes || py >= TrailRes) continue;
                    byte v = (byte)(Math.Min(1, w / t.Worn) * 255);
                    int at = py * TrailRes + px;
                    if (_trail[at] < v) _trail[at] = v;
                }
            }
        _trailImg.SetData(TrailRes, TrailRes, false, Image.Format.L8, _trail);
        TrailTex.Update(_trailImg);
    }

    /// <summary>Which territories the town holds, and how far the enemy's corruption has spread.</summary>
    public void SetStates(IReadOnlyDictionary<int, (float Held, float Corrupt)> states)
    {
        Array.Clear(_state);
        foreach (var (cell, s) in states)
        {
            _state[cell * 4] = (byte)(Mathf.Clamp(s.Held, 0, 1) * 255);
            _state[cell * 4 + 1] = (byte)(Mathf.Clamp(s.Corrupt, 0, 1) * 255);
        }
        _stateImg.SetData(WorldGen.Cells, WorldGen.Cells, false, Image.Format.Rgba8, _state);
        StateTex.Update(_stateImg);
    }

    private void BuildChunk(int cx, int cy)
    {
        var g = Gen;
        int x0 = cx * WorldGen.ChunkTiles, y0 = cy * WorldGen.ChunkTiles;
        int[] steps = [1, 2, 8];
        for (int lod = 0; lod < steps.Length; lod++)
        {
            var mesh = GridMesh(x0, y0, steps[lod], skirt: lod > 0);
            var mi = new MeshInstance3D
            {
                Mesh = mesh,
                MaterialOverride = Mats.Terrain,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeBegin = lod == 0 ? 0 : TerrainLod[lod - 1],
                VisibilityRangeEnd = lod < TerrainLod.Length ? TerrainLod[lod] : 0,
            };
            AddChild(mi);
        }
        bool wet = false;
        for (int y = y0; y <= y0 + WorldGen.ChunkTiles && !wet; y += 2)
            for (int x = x0; x <= x0 + WorldGen.ChunkTiles; x += 2)
                if (g.Height[y * (WorldGen.N + 1) + x] < WorldGen.WaterLevel + 0.05f)
                {
                    wet = true;
                    break;
                }
        if (!wet) return;
        float s = WorldGen.ChunkTiles;
        var plane = new PlaneMesh { Size = new Vector2(s, s), SubdivideWidth = 0, SubdivideDepth = 0 };
        AddChild(new MeshInstance3D
        {
            Mesh = plane,
            MaterialOverride = Mats.Water,
            Position = new Vector3(x0 + s / 2, WorldGen.WaterLevel, y0 + s / 2),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>A heightmap patch at the given vertex step; coarser patches hang a skirt to hide cracks.</summary>
    private ArrayMesh GridMesh(int x0, int y0, int step, bool skirt)
    {
        var g = Gen;
        int n = WorldGen.ChunkTiles / step + 1;
        int total = n * n + (skirt ? 4 * n : 0);
        var verts = new Vector3[total];
        var norms = new Vector3[total];
        var idx = new List<int>((n - 1) * (n - 1) * 6 + (skirt ? 24 * n : 0));
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int x = x0 + i * step, y = y0 + j * step;
                verts[j * n + i] = new Vector3(x, g.Height[y * (WorldGen.N + 1) + x], y);
                norms[j * n + i] = g.NormalAt(x, y);
            }
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                idx.AddRange([a, b, d, a, d, c]);
            }
        if (skirt)
        {
            // Each edge of the patch drops a curtain a few units down.
            int baseIdx = n * n;
            int[][] edges =
            [
                Enumerable.Range(0, n).ToArray(),
                Enumerable.Range(0, n).Select(i => (n - 1) * n + i).ToArray(),
                Enumerable.Range(0, n).Select(j => j * n).ToArray(),
                Enumerable.Range(0, n).Select(j => j * n + n - 1).ToArray(),
            ];
            for (int e = 0; e < 4; e++)
            {
                for (int k = 0; k < n; k++)
                {
                    int src = edges[e][k];
                    verts[baseIdx + e * n + k] = verts[src] - new Vector3(0, 3, 0);
                    norms[baseIdx + e * n + k] = norms[src];
                }
                for (int k = 0; k < n - 1; k++)
                {
                    int a = edges[e][k], b = edges[e][k + 1], c = baseIdx + e * n + k, d = c + 1;
                    // Both windings, so the curtain shows whichever side the camera is on.
                    idx.AddRange([a, b, d, a, d, c, a, d, b, a, c, d]);
                }
            }
        }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Normal] = norms;
        arrays[(int)Mesh.ArrayType.Index] = idx.ToArray();
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    // ---------- Trees ----------

    private static readonly string[][] TreeModels =
    [
        ["nature/tree_pineTallA_detailed", "nature/tree_pineDefaultA", "nature/tree_pineRoundA"],
        ["nature/tree_pineTallB_detailed", "nature/tree_cone"],
        ["nature/tree_oak", "nature/tree_detailed", "nature/tree_default"],
        ["nature/tree_fat_darkh", "nature/tree_oak_dark", "nature/tree_detailed_dark"],
    ];

    private static Mesh[]? _treeNear, _treeFar;

    /// <summary>Kenney's pines and broadleaves, recoloured to our palette; far trees are simple generated cones and blobs.</summary>
    private static void TreeMeshes()
    {
        if (_treeNear != null) return;
        _treeNear = new Mesh[4];
        _treeFar = new Mesh[4];
        var kit = new MeshKit();
        for (int k = 0; k < 4; k++)
        {
            kit.Clear();
            // Each kind is one mesh holding its variants side by side would cost more vertices; the first variant stands for the kind.
            kit.Add(TreeModels[k][0], MeshKit.At(0, 0, 0, 0, 0.95f), Foliage);
            _treeNear[k] = kit.Build();
            kit.Clear();
            var bark = new Color("6b4a2e");
            if (k < 2)
            {
                kit.Frustum(Vector3.Zero, 0.06f, 0.05f, 0.25f, 4, bark);
                kit.Frustum(new Vector3(0, 0.2f, 0), k == 0 ? 0.26f : 0.2f, 0, k == 0 ? 1.25f : 1.6f, 5, k == 0 ? new Color("3f6b3a") : new Color("3a5f45"));
            }
            else
            {
                kit.Frustum(Vector3.Zero, 0.06f, 0.05f, 0.45f, 4, bark);
                kit.Blob(new Vector3(0, 0.75f, 0), new Vector3(0.36f, 0.38f, 0.36f), k == 2 ? new Color("5f8f3e") : new Color("4b6a37"));
            }
            _treeFar[k] = kit.Build();
        }
    }

    /// <summary>Kenney's teal leaves become our greens; bark and stone stay as they are.</summary>
    public static Color Foliage(Color c)
    {
        c.ToHsv(out float h, out float s, out float v);
        if (h > 0.2f && h < 0.6f && s > 0.25f) return Color.FromHsv(0.25f + (h - 0.4f) * 0.25f, Mathf.Min(0.75f, s * 0.95f), v * 0.62f);
        if (h < 0.12f && s > 0.3f) return Color.FromHsv(h, s * 0.6f, v * 0.62f);
        return c;
    }

    private static Mesh[]? _scatter;

    private static void ScatterMeshes()
    {
        if (_scatter != null) return;
        var kit = new MeshKit();
        var meshes = new Mesh[WorldGen.Scatter.Kinds];
        var stone = Palette.Kenney(new Color("7a7468"));
        kit.Add("nature/rock_smallA", MeshKit.At(0, 0, 0, 0, 1.4f), stone);
        kit.Add("nature/rock_smallB", MeshKit.At(0.18f, 0, 0.1f, 1, 1.1f), stone);
        meshes[WorldGen.Scatter.Rock] = kit.Build();
        kit.Clear();
        kit.Add("nature/rock_largeB", MeshKit.At(0, 0, 0, 0, 1.3f), stone);
        meshes[WorldGen.Scatter.Boulder] = kit.Build();
        kit.Clear();
        kit.Add("nature/plant_bushLarge", MeshKit.At(0, 0, 0, 0, 1.5f), Foliage);
        kit.Add("nature/plant_bush", MeshKit.At(0.2f, 0, 0.12f, 0.5f, 1.2f), Foliage);
        meshes[WorldGen.Scatter.Bush] = kit.Build();
        kit.Clear();
        // A tuft of grass: a few thin blades leaning out from a point, bending in the gusts.
        kit.Sway = 1.1f;
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5 + 0.3f;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            var side = new Vector3(-dir.Z, 0, dir.X) * 0.035f;
            var tip = dir * 0.15f + new Vector3(0, 0.22f + (i % 2) * 0.08f, 0);
            var col = i % 2 == 0 ? new Color("4c6e30") : new Color("a9a85c");
            kit.Tri(-side, tip, side, col);
            kit.Tri(side, tip, -side, col);
        }
        meshes[WorldGen.Scatter.Tuft] = kit.Build();
        kit.Clear();
        kit.Sway = 0.9f;
        for (int i = 0; i < 6; i++)
        {
            var p = new Vector3(Mathf.Cos(i * 2.4f) * 0.12f * (i % 3), 0, Mathf.Sin(i * 2.4f) * 0.12f * (i % 3));
            kit.Frustum(p, 0.008f, 0.008f, 0.09f, 3, new Color("5f8a3e"));
            kit.Blob(p + new Vector3(0, 0.1f, 0), new Vector3(0.028f, 0.02f, 0.028f), i % 3 == 0 ? new Color("e8d27a") : i % 3 == 1 ? new Color("d88aa8") : new Color("f2f0e8"));
        }
        meshes[WorldGen.Scatter.Flowers] = kit.Build();
        _scatter = meshes;
    }

    /// <summary>How far from the camera (to the chunk centre, 64 tiles across) each kind of clutter is still drawn: grass only close up, boulders much further.</summary>
    private static readonly float[] ScatterRange = [150, 320, 180, 95, 95];

    private void BuildScatter(int cx, int cy)
    {
        ScatterMeshes();
        var list = Gen.ChunkScatter[cy * WorldGen.Chunks + cx];
        for (int kind = 0; kind < WorldGen.Scatter.Kinds; kind++)
        {
            var mine = list.Where(t => t.Kind == kind).ToList();
            if (mine.Count == 0) continue;
            var mm = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, Mesh = _scatter![kind], InstanceCount = mine.Count };
            mm.Buffer = Instances(mine, kind == WorldGen.Scatter.Tuft ? 0 : -1);
            AddChild(new MultiMeshInstance3D
            {
                Multimesh = mm,
                MaterialOverride = kind is WorldGen.Scatter.Bush or WorldGen.Scatter.Tuft or WorldGen.Scatter.Flowers ? Mats.Foliage : Mats.People,
                CastShadow = kind is WorldGen.Scatter.Boulder or WorldGen.Scatter.Bush ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                VisibilityRangeEnd = ScatterRange[kind],
            });
            TreeInstances += mine.Count;
        }
    }

    /// <summary>Instance buffer for a list of spots: transform plus a per-instance tint.</summary>
    private static float[] Instances(List<TreeSpot> spots, int kind)
    {
        var buf = new float[spots.Count * 16];
        for (int i = 0; i < spots.Count; i++)
        {
            var t = spots[i];
            float sx = t.Scale * (kind == 1 ? 0.85f : kind == 3 ? 1.15f : 1), sy = t.Scale * (kind == 1 ? 1.2f : kind == 3 ? 0.85f : 1);
            var basis = Basis.FromEuler(new Vector3(0, t.Yaw, 0)).Scaled(new Vector3(sx, sy, sx));
            int o = i * 16;
            buf[o] = basis.X.X;
            buf[o + 1] = basis.Y.X;
            buf[o + 2] = basis.Z.X;
            buf[o + 3] = t.X;
            buf[o + 4] = basis.X.Y;
            buf[o + 5] = basis.Y.Y;
            buf[o + 6] = basis.Z.Y;
            buf[o + 7] = t.H - 0.05f;
            buf[o + 8] = basis.X.Z;
            buf[o + 9] = basis.Y.Z;
            buf[o + 10] = basis.Z.Z;
            buf[o + 11] = t.Y;
            // A little variety in each tree's green, and a few early autumn crowns among the broadleaves.
            float v = 0.85f + t.Tint * 0.3f;
            var tint = kind >= 2 && t.Tint > 0.93f ? new Color(1.35f, 0.95f, 0.45f) : new Color(v * (0.95f + t.Tint * 0.1f), v, v * (1.02f - t.Tint * 0.08f));
            buf[o + 12] = tint.R;
            buf[o + 13] = tint.G;
            buf[o + 14] = tint.B;
            buf[o + 15] = 1;
        }
        return buf;
    }

    private void BuildTrees(int cx, int cy)
    {
        TreeMeshes();
        var list = Gen.ChunkTrees[cy * WorldGen.Chunks + cx];
        if (list.Count == 0) return;
        for (int kind = 0; kind < 4; kind++)
        {
            var mine = list.Where(t => t.Kind == kind).ToList();
            if (mine.Count == 0) continue;
            var buf = Instances(mine, kind);
            // Pines and firs are evergreen: half alpha tells the foliage shader to keep them green through autumn and winter.
            if (kind < 2) for (int i = 15; i < buf.Length; i += 16) buf[i] = 0.5f;
            TreeInstances += mine.Count;
            for (int lod = 0; lod < 2; lod++)
            {
                var mm = new MultiMesh
                {
                    TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
                    UseColors = true,
                    Mesh = lod == 0 ? _treeNear![kind] : _treeFar![kind],
                    InstanceCount = mine.Count,
                };
                mm.Buffer = buf;
                AddChild(new MultiMeshInstance3D
                {
                    Multimesh = mm,
                    MaterialOverride = Mats.Foliage,
                    CastShadow = lod == 0 ? GeometryInstance3D.ShadowCastingSetting.On : GeometryInstance3D.ShadowCastingSetting.Off,
                    VisibilityRangeBegin = lod == 0 ? 0 : TreeNear,
                    VisibilityRangeEnd = lod == 0 ? TreeNear : TreeFar,
                });
            }
        }
    }
}
