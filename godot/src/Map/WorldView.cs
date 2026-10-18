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
    public static readonly ShaderMaterial[] All = [Terrain, Water, Props, People, Foliage];

    private static ShaderMaterial Make(string name) => new() { Shader = GD.Load<Shader>($"res://assets/shaders/{name}.gdshader") };

    static Mats() => People.SetShaderParameter("hide_in_strategic", 1.0);

    public static void SetAll(string param, Variant v)
    {
        foreach (var m in All) m.SetShaderParameter(param, v);
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
        for (int cy = 0; cy < WorldGen.Chunks; cy++)
            for (int cx = 0; cx < WorldGen.Chunks; cx++)
            {
                BuildChunk(cx, cy);
                BuildTrees(cx, cy);
                BuildScatter(cx, cy);
            }
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
        // A tuft of grass: a few thin blades leaning out from a point.
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
