using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town's decorations: every piece drawn where the player set it, turned to its own angle,
/// trees scaled by how far they have grown. Pieces of one kind share an instanced batch, so the
/// draw calls grow with the kinds of decoration, not their number. Also draws the ghost of the
/// piece in hand while placing.
/// </summary>
public partial class DecorView(MapView map) : Node3D
{
    private readonly Dictionary<string, Mesh> _meshes = [];
    private readonly Dictionary<string, MultiMeshInstance3D> _batches = [];
    private readonly MeshInstance3D _ghost = new() { CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
    private readonly StandardMaterial3D _ghostOk = Ghost(new Color(1, 1, 1, 0.6f));
    private readonly StandardMaterial3D _ghostBad = Ghost(new Color(1, 0.35f, 0.3f, 0.55f));
    private string _key = "";

    private static StandardMaterial3D Ghost(Color tint) => new()
    {
        VertexColorUseAsAlbedo = true,
        VertexColorIsSrgb = true,
        AlbedoColor = tint,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        NoDepthTest = false,
    };

    public override void _Ready() => AddChild(_ghost);

    public void Reset()
    {
        foreach (var b in _batches.Values) b.QueueFree();
        _batches.Clear();
        _key = "";
    }

    private static float Y => WorldGen.TownHeight;

    /// <summary>World position of a point in town lots.</summary>
    public static Vector3 World(double x, double y) => new(Geo.Margin + (float)x, Y, Geo.Margin + (float)y);

    /// <summary>Trees that sway with the foliage; everything else is a prop.</summary>
    public static bool IsTree(DecorDef d) => d.Group == "Greenery" && d.GrowDays > 0;

    public void Refresh()
    {
        var town = map.Sim.Town;
        var s = town.Decor;
        string key = $"{s.Version}:{town.Day}";
        if (key == _key) return;
        _key = key;
        var groups = new SortedDictionary<string, List<Transform3D>>(StringComparer.Ordinal);
        foreach (var d in s.Pieces)
        {
            string mesh = MeshKey(d.Def.Id, d.Id);
            if (!groups.TryGetValue(mesh, out var list)) groups[mesh] = list = [];
            float scale = (float)Decor.Growth(town, d) * (0.9f + Geo.Hash(d.Id * 13 + 5) * 0.2f);
            list.Add(new Transform3D(Basis.FromEuler(new Vector3(0, (float)d.Angle, 0)).Scaled(Vector3.One * scale), World(d.X, d.Y)));
        }
        foreach (var (k, mmi) in _batches)
            if (!groups.ContainsKey(k)) mmi.Multimesh.InstanceCount = 0;
        foreach (var (k, list) in groups)
        {
            if (!_batches.TryGetValue(k, out var mmi))
            {
                bool tree = IsTree(town.Content.Decor.Piece(k.Split(':')[0]));
                mmi = new MultiMeshInstance3D
                {
                    Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = tree, Mesh = MeshFor(k) },
                    MaterialOverride = tree ? Mats.Foliage : Mats.Props,
                };
                AddChild(mmi);
                _batches[k] = mmi;
            }
            mmi.Multimesh.InstanceCount = list.Count;
            for (int i = 0; i < list.Count; i++)
            {
                mmi.Multimesh.SetInstanceTransform(i, list[i]);
                if (mmi.Multimesh.UseColors) mmi.Multimesh.SetInstanceColor(i, Colors.White);
            }
        }
    }

    /// <summary>Show the piece in hand at a point and angle, tinted red where it can't go; null hides it.</summary>
    public void ShowGhost(string? defId, double x, double y, double angle, bool ok, int variant = 0)
    {
        _ghost.Visible = defId != null;
        if (defId == null) return;
        _ghost.Mesh = MeshFor(MeshKey(defId, variant));
        _ghost.MaterialOverride = ok ? _ghostOk : _ghostBad;
        _ghost.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, (float)angle, 0)), World(x, y) + new Vector3(0, 0.01f, 0));
    }

    /// <summary>Kinds with looks to choose from pick one by the piece's id.</summary>
    private static string MeshKey(string id, int n) => id switch
    {
        "oak" or "pine" or "birch" or "flowers" or "bush" or "crates" or "market_stall" or "banner" => $"{id}:{n % 3}",
        _ => id,
    };

    public Mesh MeshFor(string key)
    {
        if (_meshes.TryGetValue(key, out var m)) return m;
        var kit = new MeshKit();
        var parts = key.Split(':');
        int v = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        Build(kit, parts[0], v);
        return _meshes[key] = kit.Build();
    }

    // ---------- The pieces ----------

    private static readonly Color Iron = new("3a3632"), Wood = new("7a5232"), DarkWood = new("4e3522"), Straw = new("d8b45a"),
        StoneC = new("a8a296"), Bronze = new("a8743a"), Marble = new("dcd6c8");

    private static Func<Color, Color> Stone => Palette.Kenney(Palette.Stone);

    private static void Build(MeshKit k, string id, int v)
    {
        switch (id)
        {
            case "oak":
                k.Add(v == 1 ? "nature/tree_detailed" : v == 2 ? "nature/tree_fat" : "nature/tree_oak", MeshKit.At(0, 0, 0, 0, 1.45f), WorldView.Foliage);
                break;
            case "pine":
                k.Add(v == 1 ? "nature/tree_pineRoundB" : v == 2 ? "nature/tree_pineTallA_detailed" : "nature/tree_pineDefaultA", MeshKit.At(0, 0, 0, 0, 1.3f), WorldView.Foliage);
                break;
            case "birch":
                k.Add(v == 1 ? "nature/tree_tall" : v == 2 ? "nature/tree_simple" : "nature/tree_thin", MeshKit.At(0, 0, 0, 0, 1.25f), Birch);
                break;
            case "fruit_tree":
                k.Add("nature/tree_default", MeshKit.At(0, 0, 0, 0, 0.8f), WorldView.Foliage);
                for (int i = 0; i < 9; i++)
                {
                    float a = i * 2.4f, r = 0.16f + Geo.Hash(i * 7) * 0.12f;
                    k.Blob(new Vector3(Mathf.Cos(a) * r, 0.62f + Geo.Hash(i * 11) * 0.42f, Mathf.Sin(a) * r), new Vector3(0.035f, 0.035f, 0.035f), new Color(i % 3 == 0 ? "e0b030" : "c8322a"));
                }
                break;
            case "bush":
                k.Add(v == 1 ? "nature/plant_bushDetailed" : "nature/plant_bushLarge", MeshKit.At(0, 0, 0, 0, v == 1 ? 0.8f : 1.2f), WorldView.Foliage);
                if (v == 2) k.Add("nature/plant_bush", MeshKit.At(0.15f, 0, 0.08f, 0.6f, 1f), WorldView.Foliage);
                break;
            case "flowers":
                // A low bed of earth edged in stone, planted in one colour or mixed.
                k.Frustum(Vector3.Zero, 0.2f, 0.19f, 0.04f, 10, new Color("8f8a80"), new Color("5e4630"));
                string[] blooms = v == 0 ? ["flower_redA", "flower_redB"] : v == 1 ? ["flower_yellowA", "flower_yellowB"] : ["flower_purpleA", "flower_redA", "flower_yellowA"];
                for (int i = 0; i < 7; i++)
                {
                    float a = i * 2.4f, r = i == 0 ? 0 : 0.11f;
                    k.Add("nature/" + blooms[i % blooms.Length], MeshKit.At(Mathf.Cos(a) * r, 0.04f, Mathf.Sin(a) * r, a, 0.7f), Stems);
                }
                break;
            case "beehive":
                for (int i = 0; i < 2; i++)
                {
                    float x = i == 0 ? -0.1f : 0.1f;
                    k.Box(new Vector3(x, 0, 0), new Vector3(0.05f, 0.06f, 0.05f), DarkWood);
                    k.Frustum(new Vector3(x, 0.06f, 0), 0.075f, 0.08f, 0.05f, 8, Straw);
                    k.Frustum(new Vector3(x, 0.11f, 0), 0.08f, 0.065f, 0.05f, 8, new Color("c8a04a"));
                    k.Frustum(new Vector3(x, 0.16f, 0), 0.065f, 0f, 0.06f, 8, Straw);
                    k.Box(new Vector3(x, 0.075f, 0.07f), new Vector3(0.025f, 0.015f, 0.02f), new Color("2a2018"));
                }
                break;
            case "hedge":
                k.Add("town/hedge-large", MeshKit.At(0, 0, 0, Mathf.Pi / 2, 0.5f), c => Stone(WorldView.Foliage(c)));
                break;

            case "bench":
                k.Add("town/stall-bench", MeshKit.At(0, 0, 0, Mathf.Pi / 2, 0.34f), _ => Wood);
                k.Box(new Vector3(0, 0.08f, -0.035f), new Vector3(0.3f, 0.06f, 0.015f), DarkWood);
                break;
            case "lamp":
                k.Frustum(Vector3.Zero, 0.03f, 0.02f, 0.5f, 6, Iron);
                k.Box(new Vector3(0, 0.5f, 0), new Vector3(0.08f, 0.09f, 0.08f), new Color("ffd890", MeshKit.Flag.Window));
                k.Pyramid(0.59f, 0.1f, 0.1f, 0.05f, Iron);
                break;
            case "brazier":
                k.Frustum(Vector3.Zero, 0.06f, 0.02f, 0.18f, 3, Iron, phase: 0.3f);
                k.Frustum(new Vector3(0, 0.18f, 0), 0.04f, 0.09f, 0.07f, 8, Iron, new Color("ff7a2a", MeshKit.Flag.Ember));
                k.Blob(new Vector3(0, 0.27f, 0), new Vector3(0.05f, 0.06f, 0.05f), new Color("ffb040", MeshKit.Flag.Ember));
                break;
            case "well":
                k.Frustum(Vector3.Zero, 0.22f, 0.22f, 0.16f, 10, StoneC, new Color("3a4a5a"));
                k.Box(new Vector3(-0.19f, 0.16f, 0), new Vector3(0.03f, 0.3f, 0.03f), Wood);
                k.Box(new Vector3(0.19f, 0.16f, 0), new Vector3(0.03f, 0.3f, 0.03f), Wood);
                k.Log(new Vector3(0, 0.38f, 0), 0.38f, 0.02f, Wood, DarkWood);
                k.Gable(0.46f, 0.5f, 0.3f, 0.14f, new Color("9a4a36"), Wood, 0.03f);
                k.Box(new Vector3(0, 0.2f, 0), new Vector3(0.06f, 0.07f, 0.06f), DarkWood);
                break;
            case "fountain":
                k.Add("town/fountain-square-detail", MeshKit.At(0, 0, 0, 0, 0.48f), Water);
                k.Frustum(new Vector3(0, 0.05f, 0), 0.52f, 0.52f, 0.13f, 4, new Color("2f5f8f"), new Color("4a86b8"), Mathf.Pi / 4);
                k.Frustum(new Vector3(0, 0.1f, 0), 0.05f, 0.04f, 0.32f, 8, StoneC);
                k.Frustum(new Vector3(0, 0.42f, 0), 0.04f, 0.14f, 0.04f, 10, StoneC, new Color("6a9ac8"));
                k.Blob(new Vector3(0, 0.52f, 0), new Vector3(0.03f, 0.08f, 0.03f), new Color("bfe0f8", MeshKit.Flag.Magic));
                break;
            case "notice_board":
                k.Box(new Vector3(-0.13f, 0, 0), new Vector3(0.025f, 0.42f, 0.025f), DarkWood);
                k.Box(new Vector3(0.13f, 0, 0), new Vector3(0.025f, 0.42f, 0.025f), DarkWood);
                k.Box(new Vector3(0, 0.16f, 0), new Vector3(0.3f, 0.2f, 0.02f), Wood);
                k.Gable(0.4f, 0.36f, 0.08f, 0.06f, new Color("6e5238"), Wood, 0.02f);
                foreach (var (x, y, c) in new[] { (-0.07f, 0.28f, "efe6d0"), (0.06f, 0.25f, "e8d8a8"), (-0.02f, 0.2f, "f0e0c0"), (0.08f, 0.19f, "d8c8a0") })
                    k.Box(new Vector3(x, y - 0.03f, 0.012f), new Vector3(0.07f, 0.06f, 0.004f), new Color(c));
                break;
            case "banner":
                {
                    string[] cloth = ["b8322a", "4f5f8a", "2e7d4a"];
                    k.Frustum(Vector3.Zero, 0.02f, 0.015f, 0.85f, 6, Wood);
                    k.Add("castle/flag-banner-long", MeshKit.At(0, 0.15f, 0.02f, Mathf.Pi / 2, 0.32f), Palette.Kenney(new Color(cloth[v])));
                    k.Blob(new Vector3(0, 0.86f, 0), new Vector3(0.025f, 0.025f, 0.025f), Palette.Gold);
                    break;
                }
            case "bandstand":
                k.Frustum(Vector3.Zero, 0.44f, 0.42f, 0.08f, 8, StoneC, new Color("8a6a4a"), Mathf.Pi / 8);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Pi / 4 + Mathf.Pi / 8;
                    k.Frustum(new Vector3(Mathf.Cos(a) * 0.38f, 0.08f, Mathf.Sin(a) * 0.38f), 0.018f, 0.018f, 0.36f, 5, Marble);
                }
                k.Frustum(new Vector3(0, 0.44f, 0), 0.47f, 0, 0.26f, 8, new Color("9a4a36"), phase: Mathf.Pi / 8);
                k.Frustum(new Vector3(0, 0.7f, 0), 0.015f, 0.015f, 0.12f, 4, Iron);
                k.Add("castle/flag-pennant", MeshKit.At(0, 0.74f, 0, 0, 0.12f), Palette.Kenney(new Color("b8322a")));
                break;

            case "market_stall":
                k.Add(v == 0 ? "town/stall-red" : v == 1 ? "town/stall-green" : "town/stall", MeshKit.At(0, 0, 0, 0, 0.42f), v == 2 ? null : Palette.Kenney(new Color("c8a040")));
                for (int i = 0; i < 3; i++)
                    k.Blob(new Vector3(-0.1f + i * 0.1f, 0.2f, 0.08f), new Vector3(0.035f, 0.025f, 0.035f), new Color(i == 0 ? "c8322a" : i == 1 ? "e0b030" : "6a9a3a"));
                break;
            case "cart":
                k.Add("town/cart", MeshKit.At(0, 0, 0, 0, 0.36f));
                k.Box(new Vector3(0, 0.14f, 0.04f), new Vector3(0.14f, 0.07f, 0.12f), new Color("c8a060"));
                break;
            case "crates":
                for (int i = 0; i <= v; i++)
                {
                    float x = (i - v * 0.5f) * 0.11f;
                    k.Box(new Vector3(x, 0, 0), new Vector3(0.1f, 0.1f, 0.1f), Wood, new Color("9a7048"));
                    k.Frustum(new Vector3(x + 0.02f, 0, 0.12f), 0.045f, 0.045f, 0.12f, 8, new Color("6e4a2c"), new Color("8a5e38"));
                }
                if (v > 0) k.Box(new Vector3(0, 0.1f, 0), new Vector3(0.09f, 0.08f, 0.09f), new Color("9a7048"));
                break;

            case "scarecrow":
                k.Box(Vector3.Zero, new Vector3(0.025f, 0.5f, 0.025f), Wood);
                k.Box(new Vector3(0, 0.33f, 0), new Vector3(0.34f, 0.025f, 0.025f), Wood);
                k.Frustum(new Vector3(0, 0.2f, 0), 0.07f, 0.05f, 0.16f, 6, new Color("6a5a8a"));
                k.Blob(new Vector3(0, 0.44f, 0), new Vector3(0.05f, 0.05f, 0.05f), Straw);
                k.Frustum(new Vector3(0, 0.47f, 0), 0.09f, 0.03f, 0.06f, 8, new Color("5a4a30"));
                k.Blob(new Vector3(-0.17f, 0.33f, 0), new Vector3(0.03f, 0.02f, 0.02f), Straw);
                k.Blob(new Vector3(0.17f, 0.33f, 0), new Vector3(0.03f, 0.02f, 0.02f), Straw);
                break;
            case "haystack":
                k.Box(new Vector3(-0.08f, 0, 0), new Vector3(0.15f, 0.1f, 0.24f), Straw, new Color("e8c870"));
                k.Box(new Vector3(0.08f, 0, 0), new Vector3(0.15f, 0.1f, 0.24f), new Color("ccaa50"), new Color("e0c068"));
                k.Box(new Vector3(0, 0.1f, 0), new Vector3(0.15f, 0.1f, 0.24f), Straw, new Color("ecd078"));
                break;
            case "fence":
                k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, 0, 0.5f));
                break;

            case "god_statue":
                k.Push(MeshKit.At(0, 0, 0, 0, 1.3f));
                Plinth(k, 0.3f, 0.22f);
                Robed(k, 0.22f, Marble, 1.1f);
                k.Blob(new Vector3(0, 0.22f + 0.5f * 1.1f, 0), new Vector3(0.06f, 0.06f, 0.06f), new Color(Palette.Gold, MeshKit.Flag.Magic));
                k.Pop();
                break;
            case "wayside_shrine":
                k.Box(Vector3.Zero, new Vector3(0.16f, 0.32f, 0.12f), StoneC);
                k.Box(new Vector3(0, 0.12f, 0.045f), new Vector3(0.09f, 0.11f, 0.04f), new Color("2a2420"));
                k.Frustum(new Vector3(0, 0.13f, 0.05f), 0.012f, 0.012f, 0.03f, 4, new Color("ffe0a0", MeshKit.Flag.Ember));
                k.Gable(0.32f, 0.22f, 0.18f, 0.08f, new Color("7e4430"), StoneC, 0.02f);
                break;
            case "obelisk":
                k.Add("nature/statue_obelisk", MeshKit.At(0, 0, 0, 0, 1.1f), Stone);
                break;

            case "hero_statue":
                k.Push(MeshKit.At(0, 0, 0, 0, 1.3f));
                Plinth(k, 0.32f, 0.26f);
                Hero(k, 0.26f, Bronze, 1.3f);
                k.Pop();
                break;
            case "founder_statue":
                k.Push(MeshKit.At(0, 0, 0, 0, 1.3f));
                Plinth(k, 0.38f, 0.32f);
                k.Box(new Vector3(0, 0.32f, 0), new Vector3(0.26f, 0.06f, 0.26f), Marble);
                Robed(k, 0.38f, Bronze, 1.4f);
                k.Box(new Vector3(0.09f, 0.38f + 0.3f, 0.06f), new Vector3(0.02f, 0.02f, 0.14f), Bronze);
                k.Pop();
                break;
            case "column":
                k.Add("nature/statue_column", MeshKit.At(0, 0, 0, 0, 0.75f), Stone);
                break;

            case "training_dummy":
                k.Box(Vector3.Zero, new Vector3(0.03f, 0.36f, 0.03f), Wood);
                k.Frustum(new Vector3(0, 0.14f, 0), 0.07f, 0.06f, 0.16f, 6, Straw);
                k.Blob(new Vector3(0, 0.36f, 0), new Vector3(0.05f, 0.05f, 0.05f), new Color("c8a060"));
                k.Box(new Vector3(0, 0.25f, 0), new Vector3(0.26f, 0.025f, 0.025f), Wood);
                k.Box(new Vector3(0.06f, 0.2f, 0.06f), new Vector3(0.08f, 0.1f, 0.02f), new Color("8a3a2a"));
                break;
            case "archery_butts":
                k.Box(new Vector3(-0.12f, 0, -0.04f), new Vector3(0.025f, 0.32f, 0.025f), Wood);
                k.Box(new Vector3(0.12f, 0, -0.04f), new Vector3(0.025f, 0.32f, 0.025f), Wood);
                k.Push(new Transform3D(Basis.FromEuler(new Vector3(Mathf.Pi / 2 - 0.2f, 0, 0)), new Vector3(0, 0.2f, 0)));
                k.Frustum(Vector3.Zero, 0.16f, 0.16f, 0.06f, 12, Straw, new Color("efe6d0"));
                k.Frustum(new Vector3(0, 0.06f, 0), 0.11f, 0.11f, 0.004f, 12, new Color("c8322a"));
                k.Frustum(new Vector3(0, 0.064f, 0), 0.06f, 0.06f, 0.004f, 12, new Color("efe6d0"));
                k.Frustum(new Vector3(0, 0.068f, 0), 0.025f, 0.025f, 0.004f, 10, new Color("e0b030"));
                k.Pop();
                break;
            case "stocks":
                k.Box(new Vector3(0, 0, 0), new Vector3(0.36f, 0.03f, 0.14f), DarkWood);
                k.Box(new Vector3(-0.15f, 0.03f, 0), new Vector3(0.03f, 0.22f, 0.04f), Wood);
                k.Box(new Vector3(0.15f, 0.03f, 0), new Vector3(0.03f, 0.22f, 0.04f), Wood);
                k.Box(new Vector3(0, 0.15f, 0), new Vector3(0.34f, 0.08f, 0.04f), Wood, new Color("8a6040"));
                foreach (float x in new[] { -0.09f, 0f, 0.09f }) k.Box(new Vector3(x, 0.18f, 0.021f), new Vector3(0.035f, 0.025f, 0.002f), new Color("2a2018"));
                break;
            default:
                k.Blob(new Vector3(0, 0.1f, 0), new Vector3(0.1f, 0.1f, 0.1f), Palette.Gold);
                break;
        }
    }

    /// <summary>Kenney's lavender stone becomes warm grey; its blue water stays water.</summary>
    private static Color Water(Color c)
    {
        c.ToHsv(out float h, out float s, out float v);
        if (h > 0.45f && h < 0.7f && s > 0.3f) return Color.FromHsv(0.57f, 0.55f, v * 0.75f);
        if (h > 0.55f && h < 0.8f) return Color.FromHsv(0.09f, 0.12f, v * 0.92f);
        return c;
    }

    /// <summary>Kenney's teal flower stems turned green; the blooms keep their colour.</summary>
    private static Color Stems(Color c)
    {
        c.ToHsv(out float h, out float s, out float v);
        return h > 0.4f && h < 0.6f && s > 0.25f ? Color.FromHsv(0.28f, s * 0.8f, v * 0.7f) : c;
    }

    /// <summary>Kenney's leaves turned to pale gold-green, the trunk to white bark.</summary>
    private static Color Birch(Color c)
    {
        c.ToHsv(out float h, out float s, out float v);
        if (h > 0.2f && h < 0.6f && s > 0.25f) return Color.FromHsv(0.2f, 0.55f, v * 0.85f);
        if (h < 0.12f && s > 0.3f) return new Color("e8e2d4");
        return c;
    }

    private static void Plinth(MeshKit k, float w, float h)
    {
        k.Box(Vector3.Zero, new Vector3(w * 1.15f, h * 0.2f, w * 1.15f), StoneC);
        k.Box(new Vector3(0, h * 0.2f, 0), new Vector3(w, h * 0.8f, w), new Color("bab3a4"), new Color("cfc8b8"));
    }

    /// <summary>A robed figure: a god, a founder, a saint.</summary>
    private static void Robed(MeshKit k, float y, Color c, float s)
    {
        k.Push(MeshKit.At(0, y, 0, 0, s));
        k.Frustum(Vector3.Zero, 0.09f, 0.055f, 0.3f, 8, c);
        k.Frustum(new Vector3(0, 0.3f, 0), 0.055f, 0.06f, 0.06f, 8, c);
        k.Blob(new Vector3(0, 0.41f, 0), new Vector3(0.048f, 0.055f, 0.048f), c);
        k.Box(new Vector3(-0.075f, 0.18f, 0.03f), new Vector3(0.03f, 0.14f, 0.03f), c);
        k.Box(new Vector3(0.075f, 0.24f, 0.05f), new Vector3(0.03f, 0.03f, 0.12f), c);
        k.Pop();
    }

    /// <summary>A hero striding forward with sword raised and a shield on the arm.</summary>
    private static void Hero(MeshKit k, float y, Color c, float s)
    {
        k.Push(MeshKit.At(0, y, 0, 0, s));
        k.Frustum(new Vector3(-0.03f, 0, -0.02f), 0.022f, 0.022f, 0.12f, 5, c);
        k.Frustum(new Vector3(0.03f, 0, 0.03f), 0.022f, 0.022f, 0.12f, 5, c);
        k.Frustum(new Vector3(0, 0.11f, 0), 0.06f, 0.07f, 0.16f, 6, c);
        k.Blob(new Vector3(0, 0.33f, 0), new Vector3(0.045f, 0.05f, 0.045f), c);
        k.Quad(new(-0.06f, 0.27f, -0.04f), new(0.06f, 0.27f, -0.04f), new(0.08f, 0.06f, -0.09f), new(-0.08f, 0.06f, -0.09f), c.Darkened(0.15f));
        k.Box(new Vector3(0.08f, 0.24f, 0), new Vector3(0.025f, 0.1f, 0.025f), c);
        k.Box(new Vector3(0.08f, 0.33f, 0), new Vector3(0.016f, 0.2f, 0.016f), c.Lightened(0.25f));
        k.Frustum(new Vector3(-0.085f, 0.14f, 0.02f), 0.06f, 0.06f, 0.015f, 8, c.Darkened(0.1f), phase: 0);
        k.Pop();
    }
}
