using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public static class Palette
{
    public static readonly Color Gold = new("e8b93a");
    public static readonly Color Timber = new("7a5232");
    public static readonly Color DarkTimber = new("4e3522");
    public static readonly Color Plaster = new("eadcc0");
    public static readonly Color Stone = new("a49a8a");
    public static readonly Color Dirt = new("a88a5e");
    public static readonly Color[] Roofs = [new("a8885a"), new("9a4a36"), new("6e5238"), new("58626f"), new("7e4430")];

    public static Color ClassColor(string cls) => cls switch
    {
        "fighter" => new("c0392b"),
        "barbarian" => new("d35400"),
        "rogue" => new("5b4a6e"),
        "ranger" => new("2e7d32"),
        "wizard" => new("3949ab"),
        "cleric" => new("f5f0e1"),
        "paladin" => new("e6c35c"),
        "warden" => new("7d6b4f"),
        "monk" => new("e08a3c"),
        "cartographer" => new("4fa3a5"),
        "sorcerer" => new("b03a7a"),
        "warlock" => new("4a2a5e"),
        "reliquarian" => new("a8a39a"),
        "druid" => new("6f9a3a"),
        "bard" => new("d46a9a"),
        "hedge-witch" => new("5e7d4a"),
        _ => new("dddddd"),
    };

    public static Color RaceColor(string race) => race switch
    {
        "human" => new("e9c3a0"),
        "dwarf" => new("d09a72"),
        "elf" => new("f0dcc0"),
        "halfling" => new("f0b98a"),
        "gnome" => new("e8c8b0"),
        "half-orc" => new("9fbf7a"),
        _ => new("e0c0a0"),
    };

    /// <summary>Any dyed cloth (not wood or stone) takes the given colour, keeping its light and shade.</summary>
    public static Func<Color, Color> Cloth(Color accent) => c =>
    {
        c.ToHsv(out float h, out float s, out float v);
        bool wood = h > 0.02f && h < 0.13f && s > 0.2f;
        if (s > 0.22f && !wood) return Color.FromHsv(accent.H, accent.S, Mathf.Clamp(accent.V * v / 0.7f, 0, 1));
        if (s <= 0.22f && h > 0.55f && h < 0.8f) return Color.FromHsv(0.09f, 0.12f, v * 0.92f);
        return c;
    };

    /// <summary>Kenney's lavender stone becomes warm grey; teal and blue roofs and cloth take the given colour.</summary>
    public static Func<Color, Color> Kenney(Color accent) => c =>
    {
        c.ToHsv(out float h, out float s, out float v);
        if (s > 0.3f && h > 0.38f && h < 0.72f) return Color.FromHsv(accent.H, accent.S, Mathf.Clamp(accent.V * v / 0.68f, 0, 1));
        if (h > 0.55f && h < 0.75f && s < 0.35f) return Color.FromHsv(0.09f, 0.12f, v * 0.92f);
        return c;
    };
}

/// <summary>
/// The town and the landmarks of the wild: buildings assembled from Kenney's town and castle kit
/// pieces and generated parts, lots and fields on the ground, the palisade, and a landmark at each
/// known site. Buildings that share a look share one instanced batch, so the draw calls grow with
/// the number of building designs, not the number of buildings.
/// </summary>
public partial class TownView(MapView map) : Node3D
{
    /// <summary>Kenney module (one wall section) to tiles.</summary>
    private const float K = 0.42f;

    /// <summary>Scale that fits a footprint of w by d sections into most of a lot.</summary>
    private static float Fit(int w, int d, float fill = 0.9f) => fill / Math.Max(w, d);

    private readonly Dictionary<string, Mesh> _meshes = [];
    private readonly Dictionary<string, MultiMeshInstance3D> _batches = [];
    private readonly MeshInstance3D _ground = new() { MaterialOverride = Mats.Props, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    private readonly MeshInstance3D _walls = new() { MaterialOverride = Mats.Props };

    /// <summary>The painted zones, a plane over the town textured from the plan, one texel a cell.</summary>
    private readonly MeshInstance3D _zones = new()
    {
        Mesh = new PlaneMesh { Size = new Vector2(Geo.Grid, Geo.Grid) },
        MaterialOverride = Mats.Zones,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        Position = new Vector3(Geo.Center.X, WorldGen.TownHeight + 0.035f, Geo.Center.Y),
    };

    private ImageTexture? _zoneTex;
    private int _zoneVersion = -1;
    private bool _planning;
    private readonly Dictionary<string, MeshInstance3D> _landmarks = [];
    private string _groundKey = "", _wallKey = "", _buildKey = "";

    public override void _Ready()
    {
        AddChild(_ground);
        AddChild(_walls);
        AddChild(_zones);
    }

    public void Reset()
    {
        foreach (var b in _batches.Values) b.QueueFree();
        _batches.Clear();
        foreach (var l in _landmarks.Values) l.QueueFree();
        _landmarks.Clear();
        _groundKey = _wallKey = _buildKey = "";
        _zoneVersion = -1;
    }

    public void Refresh()
    {
        var town = map.Sim.Town;
        Lots(town);
        Zones(town);
        Palisade(town);
        Buildings(town);
        Landmarks(town);
    }

    private static float Y => WorldGen.TownHeight;

    // ---------- Buildings ----------

    private static string MeshKey(Building b)
    {
        if (b.IsSite) return $"site:{Math.Min(3, (int)(b.BuildProgress / b.Def.BuildHours * 4))}";
        return $"{b.Def.Id}:{b.Id % 4}";
    }

    private void Buildings(Town town)
    {
        var groups = new SortedDictionary<string, List<(Vector3 P, float Yaw, float Scale)>>(StringComparer.Ordinal);
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            var lot = town.Lot(b.LotId);
            var c = Geo.LotPos(lot);
            var p = new Vector3(c.X, Y, c.Y);
            string key = MeshKey(b);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            // Every building faces the way its lot was sited (toward the hall, or as the player turned it),
            // scaled from the one-tile models to its footprint.
            float yaw = Geo.LotYaw(lot), scale = Math.Max(lot.W, lot.D) / (float)Land.Res / 0.92f;
            list.Add((p, yaw, scale));
            if (b.Closed || b.ForSale)
            {
                if (!groups.TryGetValue("sign", out var signs)) groups["sign"] = signs = [];
                var f = Geo.LotFront(lot);
                var at = c + f * (lot.D / (float)Land.Res / 2 + 0.1f) + new Vector2(f.Y, -f.X) * 0.25f;
                signs.Add((new Vector3(at.X, Y, at.Y), yaw + 0.3f, 1));
            }
        }
        string all = string.Join('|', groups.Select(g => $"{g.Key}:{string.Join(',', g.Value.Select(v => $"{v.P.X},{v.P.Z},{v.Yaw}"))}"));
        if (all == _buildKey) return;
        _buildKey = all;
        foreach (var (key, mmi) in _batches)
            if (!groups.ContainsKey(key)) mmi.Multimesh.InstanceCount = 0;
        foreach (var (key, list) in groups)
        {
            if (!_batches.TryGetValue(key, out var mmi))
            {
                mmi = new MultiMeshInstance3D
                {
                    Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = MeshFor(key) },
                    MaterialOverride = Mats.Props,
                };
                AddChild(mmi);
                _batches[key] = mmi;
            }
            mmi.Multimesh.InstanceCount = list.Count;
            for (int i = 0; i < list.Count; i++)
                mmi.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.FromEuler(new Vector3(0, list[i].Yaw, 0)).Scaled(Vector3.One * list[i].Scale), list[i].P));
        }
    }

    private Mesh MeshFor(string key)
    {
        if (_meshes.TryGetValue(key, out var m)) return m;
        var kit = new MeshKit();
        var parts = key.Split(':');
        int v = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        switch (parts[0])
        {
            case "site": Construction(kit, v); break;
            case "sign": kit.Add("nature/sign", MeshKit.At(0, 0, 0, 0, 0.7f)); break;
            case "tavern": Tavern(kit, v); break;
            case "town_hall": Hall(kit, v); break;
            case "farm": Farm(kit, v); break;
            case "lumber_camp": Lumber(kit, v); break;
            case "forge": Forge(kit, v); break;
            case "brewery": Brewery(kit, v); break;
            case "scrying_pool": Pool(kit); break;
            case "sleepless_spire": Spire(kit); break;
            case "shrine": Shrine(kit); break;
            case "temple": Temple(kit, v); break;
            case "general_store": Store(kit, v); break;
            case "granary": Granary(kit, v); break;
            case "watch_house": WatchHouse(kit, v); break;
            default: House(kit, v); break;
        }
        return _meshes[key] = kit.Build();
    }

    /// <summary>
    /// Walls of Kenney town-kit sections around a w by d footprint (in sections), on the given floor,
    /// with a door on the street side; windows get a pane that lights up at night.
    /// </summary>
    private static void Walls(MeshKit k, int w, int d, int floor, bool stone, int seed, bool door)
    {
        string pre = stone ? "town/wall" : "town/wall-wood";
        var tint = Palette.Kenney(Palette.Stone);
        for (int i = 0; i < w; i++)
            for (int j = 0; j < d; j++)
            {
                float cx = i - w / 2f + 0.5f, cz = j - d / 2f + 0.5f;
                // Each edge of the footprint that faces outward gets a section.
                (bool Edge, float Yaw, bool Front)[] sides =
                [
                    (i == w - 1, 0, false), (i == 0, Mathf.Pi, false), (j == d - 1, -Mathf.Pi / 2, true), (j == 0, Mathf.Pi / 2, false),
                ];
                foreach (var (edge, yaw, front) in sides)
                {
                    if (!edge) continue;
                    float r = Geo.Hash(seed * 97 + i * 13 + j * 7 + (int)(yaw * 10) + floor * 3);
                    string piece = front && door && floor == 0 && i == w / 2 ? "-door"
                        : r < 0.45f ? "-window-shutters" : r < 0.6f ? (stone ? "-window-stone" : "-window-small") : r < 0.8f && !stone ? "-detail-cross" : "";
                    k.Add(pre + piece, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)), new Vector3(cx, floor, cz)), tint);
                    if (piece.Contains("window"))
                    {
                        // The pane: a warm square just proud of the wall, lit at night.
                        var n = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
                        var c = new Vector3(cx, floor + 0.52f, cz) + n * 0.51f;
                        var t = new Vector3(n.Z, 0, -n.X) * 0.14f;
                        var u = new Vector3(0, 0.15f, 0);
                        k.Quad(c - t - u, c - t + u, c + t + u, c + t - u, new Color("3a3026", MeshKit.Flag.Window));
                    }
                }
            }
    }

    private static void Chimney(MeshKit k, float x, float y, float z, bool smoke = false)
    {
        k.Box(new Vector3(x, y, z), new Vector3(0.22f, 0.9f, 0.22f), new Color("8a8070"), new Color("3a3430"));
        if (smoke) k.Box(new Vector3(x, y + 0.9f, z), new Vector3(0.12f, 0.02f, 0.12f), new Color("ff8a3a", MeshKit.Flag.Ember));
    }

    private static Color RoofOf(int v) => Palette.Roofs[v % Palette.Roofs.Length];

    private static void House(MeshKit k, int v)
    {
        int w = 2, d = v == 2 ? 1 : 2;
        k.Push(MeshKit.At(0, 0, -0.04f, 0, Fit(w, d, d == 1 ? 0.76f : 0.8f)));
        Walls(k, w, d, 0, v == 1, v + 11, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, w, d, d * 0.62f + 0.25f, RoofOf(v), Palette.Plaster, 0.12f);
        k.Pop();
        if (v != 2) Chimney(k, w / 2f - 0.45f, 1, -0.2f, v == 0);
        k.Pop();
    }

    /// <summary>A wayside shrine: a stone plinth under a little pyramid roof on four pillars, a gold sun inside.</summary>
    private static void Shrine(MeshKit k)
    {
        k.Push(MeshKit.At(0, 0, 0, 0, 0.62f));
        k.Box(Vector3.Zero, new Vector3(1.1f, 0.18f, 1.1f), new Color("8f8a80"), new Color("a8a296"));
        foreach (var (x, z) in new[] { (-0.4f, -0.4f), (0.4f, -0.4f), (-0.4f, 0.4f), (0.4f, 0.4f) })
            k.Frustum(new Vector3(x, 0.18f, z), 0.07f, 0.06f, 0.9f, 6, new Color("b8b2a4"));
        k.Box(new Vector3(0, 0.18f, 0), new Vector3(0.36f, 0.3f, 0.36f), new Color("9a9488"));
        k.Blob(new Vector3(0, 0.62f, 0), new Vector3(0.14f, 0.14f, 0.14f), new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pyramid(1.08f, 1.0f, 1.0f, 0.55f, new Color("7e4430"), 0.08f);
        k.Pop();
    }

    /// <summary>A stone temple: two storeys of stone, a colonnade on the street side and a gilded finial.</summary>
    private static void Temple(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.1f, 0, Fit(3, 2, 0.95f)));
        Walls(k, 3, 2, 0, true, v + 41, true);
        Walls(k, 3, 2, 1, true, v + 43, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.1f, new Color("6c7480"), new Color("c8c0b0"), 0.14f);
        k.Pop();
        k.Box(new Vector3(0, 0, 1.3f), new Vector3(3.2f, 0.12f, 0.7f), new Color("a8a296"));
        foreach (float x in new[] { -1.3f, -0.45f, 0.45f, 1.3f })
            k.Add("town/pillar-stone", MeshKit.At(x, 0.12f, 1.45f, 0, 1), Palette.Kenney(Palette.Stone));
        k.Frustum(new Vector3(0, 3.1f, 0), 0.12f, 0, 0.5f, 6, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pop();
    }

    /// <summary>The general store: a timber house with a striped market stall and a cart at the door.</summary>
    private static void Store(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.25f, 0, Fit(2, 2, 0.72f)));
        Walls(k, 2, 2, 0, false, v + 51, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 1.05f, new Color("5f6a34"), Palette.Plaster, 0.12f);
        k.Pop();
        k.Add("town/stall-green", MeshKit.At(-0.5f, 0, 1.7f, 0, 0.8f));
        k.Add("town/cart", MeshKit.At(0.75f, 0, 1.6f, 0.4f, 0.7f));
        k.Pop();
    }

    private static void Granary(MeshKit k, int v)
    {
        // Two round grain bins on a stone footing beside a long store barn.
        k.Box(new Vector3(0, 0, 0), new Vector3(0.96f, 0.05f, 0.96f), new Color("8f8a80"), new Color("a8a296"));
        for (int i = 0; i < 2; i++)
        {
            float x = -0.24f + i * 0.46f;
            k.Frustum(new Vector3(x, 0.05f, -0.22f), 0.19f, 0.19f, 0.5f, 10, new Color("b89a62"), null, i);
            k.Frustum(new Vector3(x, 0.55f, -0.22f), 0.21f, 0.02f, 0.2f, 10, new Color("8a5232"), null, i);
        }
        k.Push(MeshKit.At(0, 0.05f, 0.24f, 0, Fit(2, 1, 0.82f)));
        Walls(k, 2, 1, 0, false, v + 81, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.7f, new Color("8a5232"), Palette.Plaster, 0.1f);
        k.Pop();
        k.Pop();
        k.Add("town/cart", MeshKit.At(0.4f, 0, 0.44f, 0.3f, 0.45f));
    }

    private static void WatchHouse(MeshKit k, int v)
    {
        // A stone guard post with a lookout tower and the watch's red banners.
        k.Push(MeshKit.At(-0.12f, 0, 0.02f, 0, Fit(2, 2, 0.7f)));
        Walls(k, 2, 2, 0, true, v + 91, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 0.9f, new Color("58626f"), Palette.Stone, 0.1f);
        k.Pop();
        k.Add("town/banner-red", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(-0.6f, 0.4f, 1.02f)));
        k.Add("town/lantern", MeshKit.At(0.6f, 0, 1.25f, 0, 0.8f));
        k.Pop();
        var castle = Palette.Kenney(new Color("6a6e78"));
        k.Add("castle/tower-square-base", MeshKit.At(0.33f, 0, -0.3f, 0, 0.3f), castle);
        k.Add("castle/tower-square-mid", MeshKit.At(0.33f, 0.3f, -0.3f, 0, 0.3f), castle);
        k.Add("castle/tower-square-top-roof", MeshKit.At(0.33f, 0.6f, -0.3f, 0, 0.3f), castle);
        k.Add("castle/flag-pennant", MeshKit.At(0.33f, 0.92f, -0.3f, 0, 0.3f), Palette.Kenney(new Color("b8322a")));
    }

    private static void Tavern(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.02f, 0, Fit(3, 2, 0.98f)));
        Walls(k, 3, 2, 0, true, v + 3, true);
        Walls(k, 3, 2, 1, false, v + 5, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.25f, new Color("8e3b2e"), Palette.Plaster, 0.12f);
        k.Pop();
        Chimney(k, -0.9f, 2, -0.3f, true);
        Chimney(k, 0.9f, 2, -0.3f, true);
        k.Add("town/banner-red", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(1, 0.6f, 0.62f)));
        k.Add("town/lantern", MeshKit.At(-1.3f, 0, 1.25f, 0, 0.8f));
        k.Box(new Vector3(-1.3f, 1.2f, 1.25f), new Vector3(0.1f, 0.1f, 0.1f), new Color("ffcf7a", MeshKit.Flag.Window));
        k.Add("town/stall-bench", MeshKit.At(0.9f, 0, 1.45f, Mathf.Pi / 2, 0.9f));
        k.Pop();
    }

    private static void Hall(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.08f, 0, 0, 0, Fit(3, 2, 0.84f)));
        Walls(k, 3, 2, 0, true, 21, true);
        Walls(k, 3, 2, 1, false, 23, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.6f, new Color("4f5f8a"), Palette.Plaster, 0.14f);
        k.Pop();
        // A bell tower at one end, flying the town's banner.
        var castle = Palette.Kenney(new Color("4f5f8a"));
        k.Add("castle/tower-square-base", MeshKit.At(1.95f, 0, -0.4f, 0, 0.85f), castle);
        k.Add("castle/tower-square-mid-windows", MeshKit.At(1.95f, 0.86f, -0.4f, 0, 0.85f), castle);
        k.Add("castle/tower-square-mid", MeshKit.At(1.95f, 1.72f, -0.4f, 0, 0.85f), castle);
        k.Add("castle/tower-square-top-roof", MeshKit.At(1.95f, 2.58f, -0.4f, 0, 0.85f), castle);
        k.Add("castle/flag-wide", MeshKit.At(1.95f, 3.42f, -0.4f, Mathf.Pi / 2, 0.9f), Palette.Kenney(new Color("b8322a")));
        k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(-1, 0.4f, 0.62f)), Palette.Kenney(new Color("4f5f8a")));
        k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(1, 0.4f, 0.62f)), Palette.Kenney(new Color("4f5f8a")));
        k.Pop();
    }

    private static void Farm(MeshKit k, int v)
    {
        // Rows of wheat and a small barn.
        k.Box(new Vector3(0, 0.005f, 0), new Vector3(0.96f, 0.02f, 0.96f), new Color("7a5e3a"));
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 2; c++)
                k.Add(v % 2 == 0 ? "nature/crops_wheatStageB" : "nature/crops_cornStageC", MeshKit.At(-0.3f + c * 0.3f, 0, -0.32f + r * 0.32f, 0, v % 2 == 0 ? 0.55f : 0.35f));
        k.Push(MeshKit.At(0.3f, 0, 0.12f, Mathf.Pi / 2, K * 0.75f));
        Walls(k, 2, 1, 0, false, v + 41, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.8f, new Color("9b3d2a"), new Color("7a5232"), 0.1f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 4; i++) k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, i * Mathf.Pi / 2, 1));
    }

    private static void Lumber(MeshKit k, int v)
    {
        k.Add("nature/log_stackLarge", MeshKit.At(-0.25f, 0, -0.2f, 0.3f, 0.9f));
        k.Add("nature/log_large", MeshKit.At(-0.15f, 0, 0.28f, 0, 0.5f));
        k.Add("nature/stump_old", MeshKit.At(0.05f, 0, 0.05f, 0, 0.9f));
        k.Push(MeshKit.At(0.25f, 0, -0.1f, Mathf.Pi / 2, K * 0.75f));
        Walls(k, 2, 1, 0, false, v + 51, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.75f, new Color("5a7a3e"), new Color("7a5232"), 0.1f);
        k.Pop();
        k.Pop();
    }

    private static void Forge(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.05f, 0, -0.08f, 0, Fit(2, 2, 0.78f)));
        Walls(k, 2, 2, 0, true, v + 61, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 1.0f, new Color("5b5550"), Palette.Stone, 0.1f);
        k.Pop();
        Chimney(k, 0.55f, 1, -0.45f, true);
        k.Pop();
        // The open hearth and anvil out front.
        k.Box(new Vector3(0.3f, 0, 0.36f), new Vector3(0.16f, 0.08f, 0.12f), new Color("6a625a"), new Color("ff7a2a", MeshKit.Flag.Ember));
        k.Box(new Vector3(-0.15f, 0, 0.38f), new Vector3(0.1f, 0.06f, 0.05f), new Color("4a4a4e"));
    }

    private static void Brewery(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.08f, 0, -0.05f, 0, Fit(2, 2, 0.76f)));
        Walls(k, 2, 2, 0, false, v + 71, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 1.1f, new Color("7a5a34"), Palette.Plaster, 0.1f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 3; i++) k.Frustum(new Vector3(0.36f, 0, -0.2f + i * 0.17f), 0.065f, 0.07f, 0.14f, 8, new Color("8a5a2b"), new Color("6a4422"));
        k.Frustum(new Vector3(0.36f, 0.14f, -0.03f), 0.06f, 0.065f, 0.13f, 8, new Color("8a5a2b"), new Color("6a4422"));
    }

    private static void Pool(MeshKit k)
    {
        // A round stone basin of still, faintly glowing water under a little shrine roof.
        var stone = Palette.Kenney(new Color("6a7a9a"));
        k.Add("town/fountain-round", MeshKit.At(0, 0, 0, 0, 0.42f), stone);
        k.Frustum(new Vector3(0, 0.08f, 0), 0.33f, 0.33f, 0.02f, 16, new Color("8a6ad8", MeshKit.Flag.Magic));
        foreach (var (x, z) in new[] { (-0.38f, -0.38f), (0.38f, -0.38f), (-0.38f, 0.38f), (0.38f, 0.38f) })
            k.Add("town/pillar-stone", MeshKit.At(x, 0, z, 0, 0.55f), stone);
        k.Push(MeshKit.At(0, 0.55f, 0));
        k.Pyramid(0, 0.86f, 0.86f, 0.32f, new Color("4f5f8a"), 0.04f);
        k.Pop();
    }

    private static void Spire(MeshKit k)
    {
        // A tall stone tower with a crystal eye that never closes.
        var castle = Palette.Kenney(new Color("4f5f8a"));
        k.Push(MeshKit.At(0, 0, 0, 0, 0.6f));
        k.Add("castle/tower-hexagon-base", MeshKit.At(0, 0, 0), castle);
        for (int i = 0; i < 4; i++) k.Add("castle/tower-hexagon-mid", MeshKit.At(0, 1.31f + i * 0.46f, 0), castle);
        k.Add("castle/tower-hexagon-top", MeshKit.At(0, 3.15f, 0), castle);
        k.Add("castle/tower-hexagon-roof", MeshKit.At(0, 3.28f, 0), castle);
        k.Blob(new Vector3(0, 4.35f, 0), new Vector3(0.2f, 0.28f, 0.2f), new Color("b48cff", MeshKit.Flag.Magic));
        k.Pop();
    }

    private static void Construction(MeshKit k, int stage)
    {
        // A footing, then a timber frame that rises with progress, wrapped in scaffold.
        k.Box(new Vector3(0, 0, 0), new Vector3(0.72f, 0.05f, 0.5f), Palette.Stone);
        float h = 0.12f + stage * 0.1f;
        foreach (var (x, z) in new[] { (-0.33f, -0.22f), (0.33f, -0.22f), (-0.33f, 0.22f), (0.33f, 0.22f), (0, -0.22f), (0, 0.22f) })
            k.Box(new Vector3(x, 0.05f, z), new Vector3(0.04f, h, 0.04f), Palette.Timber);
        k.Box(new Vector3(0, 0.05f + h, -0.22f), new Vector3(0.7f, 0.035f, 0.035f), Palette.Timber);
        k.Box(new Vector3(0, 0.05f + h, 0.22f), new Vector3(0.7f, 0.035f, 0.035f), Palette.Timber);
        if (stage >= 2)
            for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.2f + i * 0.2f, 0.05f, -0.21f), new Vector3(0.18f, h * 0.7f, 0.02f), new Color("c8ab7e"));
        k.Add("nature/log_stack", MeshKit.At(0.4f, 0, 0.36f, Mathf.Pi / 2, 0.6f));
        k.Add("town/planks", MeshKit.At(-0.35f, 0, 0.38f, 0.2f, 0.25f));
    }

    // ---------- Ground ----------

    /// <summary>A yard of trodden ground under every building, turned with it.</summary>
    private void Lots(Town town)
    {
        string key = town.Lots.Count.ToString();
        if (key == _groundKey) return;
        _groundKey = key;
        var k = new MeshKit();
        foreach (var l in town.Lots)
        {
            var c = l.Zone switch
            {
                Zone.Civic => new Color("b4aa94"),
                Zone.Commercial => new Color("a28c62"),
                Zone.Farmland => new Color("7a6040"),
                Zone.Industry => new Color("8a7a66"),
                _ => new Color("8f8a5c"),
            };
            var at = Geo.LotPos(l);
            float w = l.W / (float)Land.Res + 0.12f, d = l.D / (float)Land.Res + 0.12f;
            k.Push(MeshKit.At(at.X, Y, at.Y, Geo.LotYaw(l)));
            k.Box(new Vector3(0, -0.05f, 0), new Vector3(w, 0.065f, d), c.Darkened(Geo.Hash(l.Id * 31 + 7) * 0.08f));
            // A worn apron where the door opens onto the lane.
            if (l.Zone != Zone.Farmland) k.Box(new Vector3(0, -0.045f, d / 2), new Vector3(w * 0.4f, 0.065f, 0.16f), Palette.Dirt.Darkened(0.05f));
            k.Pop();
        }
        k.Build(_ground.Mesh as ArrayMesh ?? (ArrayMesh)(_ground.Mesh = new ArrayMesh()));
    }

    /// <summary>Show the painted zones strongly while the player plans, faintly otherwise.</summary>
    public void Planning(bool on)
    {
        _planning = on;
        Mats.Zones.SetShaderParameter("strength", on ? 0.5f : 0.1f);
    }

    /// <summary>Zone paint texels to a plan cell: the plan is drawn finer than it is kept, then softened, so painted edges come out round.</summary>
    private const int ZoneUp = 4;

    private readonly float[] _zoneBuf = new float[Land.Size * ZoneUp * Land.Size * ZoneUp * 4], _zoneTmp = new float[Land.Size * ZoneUp * Land.Size * ZoneUp * 4];

    /// <summary>Refresh the zone paint texture when the plan has changed: open painted ground only, built ground is shown by its yards.</summary>
    private void Zones(Town town)
    {
        var plan = town.Plan;
        int version = $"{town.Lots.Count}|{plan.Zones.Aggregate(17, (h, z) => h * 31 + z)}".GetHashCode();
        if (version == _zoneVersion) return;
        _zoneVersion = version;
        int n = Land.Size * ZoneUp;
        // Premultiplied colour, so softening across a zone's edge fades it out instead of darkening it.
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y / ZoneUp * Land.Size + x / ZoneUp, o = (y * n + x) * 4;
                var z = (Zone)plan.Zones[i];
                bool show = z is not (Zone.None or Zone.Civic) && plan.Taken[i] == 0;
                var c = show ? Planner.ZoneColor(z) : new Color(0, 0, 0, 0);
                _zoneBuf[o] = c.R * c.A;
                _zoneBuf[o + 1] = c.G * c.A;
                _zoneBuf[o + 2] = c.B * c.A;
                _zoneBuf[o + 3] = c.A;
            }
        for (int pass = 0; pass < 2; pass++)
        {
            Blur(_zoneBuf, _zoneTmp, n, 1, n, 3);
            Blur(_zoneTmp, _zoneBuf, n, n, 1, 3);
        }
        var bytes = new byte[n * n * 4];
        for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(_zoneBuf[i] * 255), 0, 255);
        var img = Image.CreateFromData(n, n, false, Image.Format.Rgba8, bytes);
        if (_zoneTex == null)
        {
            _zoneTex = ImageTexture.CreateFromImage(img);
            Mats.Zones.SetShaderParameter("zones_tex", _zoneTex);
        }
        else _zoneTex.Update(img);
    }

    /// <summary>A box blur of radius r along one axis (step between neighbours <paramref name="step"/>, between lines <paramref name="line"/>).</summary>
    private static void Blur(float[] src, float[] dst, int n, int step, int line, int r)
    {
        float k = 1f / (2 * r + 1);
        for (int l = 0; l < n; l++)
            for (int ch = 0; ch < 4; ch++)
            {
                float sum = 0;
                int Idx(int t) => (l * line + Math.Clamp(t, 0, n - 1) * step) * 4 + ch;
                for (int t = -r; t <= r; t++) sum += src[Idx(t)];
                for (int t = 0; t < n; t++)
                {
                    dst[Idx(t)] = sum * k;
                    sum += src[Idx(t + r + 1)] - src[Idx(t - r)];
                }
            }
    }

    private void Palisade(Town town)
    {
        var d = town.Defenses;
        var line = WallLine(town);
        float total = town.Content.Enemy.Palisade.Days * SimTime.HoursPerDay;
        float done = d.Palisade == PalisadeState.Building && d.PalisadeReadyTick is int ready ? 1 - (ready - town.Tick) / total : d.Palisade == PalisadeState.Built ? 1 : 0;
        string key = $"{d.Palisade}|{Math.Round(done, 2)}|{d.Towers}|{(line == null ? "" : string.Join(';', line))}";
        if (key == _wallKey) return;
        _wallKey = key;
        var k = new MeshKit();
        if (line != null)
        {
            float perim = 0;
            for (int i = 0; i < line.Length; i++) perim += line[i].DistanceTo(line[(i + 1) % line.Length]);
            // A point a distance along the wall, going round from the first corner.
            Vector2 Along(float t)
            {
                for (int i = 0; i < line.Length; i++)
                {
                    var a = line[i];
                    var b = line[(i + 1) % line.Length];
                    float len = a.DistanceTo(b);
                    if (t <= len) return a.Lerp(b, len > 0 ? t / len : 0);
                    t -= len;
                }
                return line[0];
            }
            if (d.Palisade != PalisadeState.None)
            {
                // Sharpened stakes all round, raised in order so a rising wall reads as progress.
                int stakes = (int)(perim / 0.13f), shown = (int)(stakes * Mathf.Clamp(done, 0, 1));
                for (int i = 0; i < shown; i++)
                {
                    var p = Along((float)i / stakes * perim);
                    float sh = 0.32f + Geo.Hash(i) * 0.06f;
                    var col = i % 2 == 0 ? Palette.Timber : new Color("8a6239");
                    k.Frustum(new Vector3(p.X, Y, p.Y), 0.055f, 0.055f, sh, 5, col, col, i);
                    k.Frustum(new Vector3(p.X, Y + sh, p.Y), 0.055f, 0, 0.1f, 5, col, null, i);
                }
            }
            var castle = Palette.Kenney(new Color("9b3d2a"));
            for (int i = 0; i < Math.Min(4, d.Towers); i++)
            {
                // Towers spread evenly round the wall.
                var c = Along(perim * i / 4f);
                k.Add("castle/tower-square-base", MeshKit.At(c.X, Y, c.Y, 0, 0.42f), castle);
                k.Add("castle/tower-square-mid", MeshKit.At(c.X, Y + 0.42f, c.Y, 0, 0.42f), castle);
                k.Add("castle/tower-square-top-roof", MeshKit.At(c.X, Y + 0.84f, c.Y, 0, 0.42f), castle);
            }
        }
        k.Build(_walls.Mesh as ArrayMesh ?? (ArrayMesh)(_walls.Mesh = new ArrayMesh()));
    }

    /// <summary>
    /// The walled town: the convex hull of every building but the farms (which lie outside the wall),
    /// pushed out a little so the stakes clear the yards. Null before there is anything to wall in.
    /// </summary>
    public static Vector2[]? WallLine(Town town)
    {
        var pts = new List<Vector2>();
        foreach (var l in town.Lots)
            if (l.Zone != Zone.Farmland) pts.AddRange(Geo.LotCorners(l, 0.35f));
        if (pts.Count < 3) return null;
        // Andrew's monotone chain, counter-clockwise from the lowest x.
        pts = [.. pts.OrderBy(p => p.X).ThenBy(p => p.Y)];
        var hull = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (var p in pass == 0 ? pts : Enumerable.Reverse(pts))
            {
                while (hull.Count >= start + 2 && (hull[^1] - hull[^2]).Cross(p - hull[^2]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return [.. hull];
    }

    // ---------- Landmarks ----------

    /// <summary>What a site looks like on the map, from its kind and habitat.</summary>
    public static string LandmarkKind(Town town, SiteDef def) =>
        def.Id == town.Content.Enemy.Outpost.Id ? "camp"
        : def.Habitats.FirstOrDefault() switch
        {
            "caves" or "underworld" => "cave",
            "forest" => "grove",
            "ruins" => "ruin",
            "swamp" or "temple" or "tomb" => "temple",
            "grassland" or "coast" or "hills" => "ford",
            _ => "ruin",
        };

    private void Landmarks(Town town)
    {
        foreach (var site in town.Sites)
        {
            if (!map.Geo.Pins.TryGetValue(site.Def.Id, out var pin)) continue;
            var sight = map.Fog.SightOf(site.Def.Id, pin);
            bool cleared = sight == Sight.Live ? site.Cleared : map.Fog.LastReport(site.Def.Id)?.Outcome == ExpeditionOutcome.Cleared;
            string kind = LandmarkKind(town, site.Def) + (cleared ? ":cleared" : "");
            string id = site.Def.Id;
            if (!_landmarks.TryGetValue(id, out var mi) || (string)mi.GetMeta("kind") != kind)
            {
                mi?.QueueFree();
                mi = new MeshInstance3D { Mesh = Landmark(kind, Geo.Hash(StableHash(id))), MaterialOverride = Mats.Props };
                mi.SetMeta("kind", kind);
                AddChild(mi);
                _landmarks[id] = mi;
            }
            // Never seen, never drawn: a rumour is a name, not a place.
            mi.Visible = sight != Sight.Rumoured;
            mi.Position = map.Gen.At(pin, -0.05f);
        }
    }

    /// <summary>A hash of a site id that is the same on every run (string.GetHashCode is not).</summary>
    private static int StableHash(string s)
    {
        uint h = 2166136261;
        foreach (char c in s) h = (h ^ c) * 16777619;
        return (int)(h & 0x7fffffff);
    }

    private static ArrayMesh Landmark(string kind, float r)
    {
        var k = new MeshKit();
        bool cleared = kind.EndsWith(":cleared");
        var stone = Palette.Kenney(new Color("6a6a7a"));
        switch (kind.Split(':')[0])
        {
            case "cave":
                // A broken hillside with cave mouths, a goblin fire and their ragged tents.
                k.Add("nature/cliff_cave_rock", MeshKit.At(0, 0, -1.4f, 0, 3.2f), stone);
                k.Add("nature/cliff_large_rock", MeshKit.At(-2.9f, 0, -1.2f, 0.4f, 3f), stone);
                k.Add("nature/cliff_large_rock", MeshKit.At(2.9f, 0, -1.2f, -0.4f, 3f), stone);
                k.Add("nature/rock_tallA", MeshKit.At(-3.2f, 0, 1, 0, 2.2f), stone);
                k.Add("nature/rock_largeB", MeshKit.At(2.6f, 0, 1.6f, 1, 2.4f), stone);
                k.Add("nature/tent_smallOpen", MeshKit.At(-1.4f, 0, 1.4f, 0.6f, 2.4f), Palette.Cloth(new Color("6a5a3a")));
                k.Add("nature/tent_smallOpen", MeshKit.At(1.5f, 0, 1.1f, -0.5f, 2.2f), Palette.Cloth(new Color("6a5a3a")));
                k.Add("nature/campfire_stones", MeshKit.At(0, 0, 1.6f, 0, 2.2f), stone);
                if (!cleared) k.Box(new Vector3(0, 0.02f, 1.6f), new Vector3(0.35f, 0.06f, 0.35f), new Color("ff7a2a", MeshKit.Flag.Ember));
                break;
            case "grove":
                // A ring of old standing stones under dark, crowding trees.
                for (int i = 0; i < 7; i++)
                {
                    float a = i * Mathf.Tau / 7;
                    k.Add("nature/statue_ring", MeshKit.At(Mathf.Cos(a) * 2.2f, 0, Mathf.Sin(a) * 2.2f, -a + Mathf.Pi / 2, 1.8f), stone);
                }
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8 + 0.3f;
                    k.Add(i % 2 == 0 ? "nature/tree_detailed_dark" : "nature/tree_oak_dark", MeshKit.At(Mathf.Cos(a) * 4f, 0, Mathf.Sin(a) * 4f, a, 2.6f), WorldView.Foliage);
                }
                k.Add("nature/mushroom_redGroup", MeshKit.At(0.4f, 0, 0.3f, 0, 2.5f));
                break;
            case "ruin":
                // The stump of an old tower and its fallen walls.
                k.Add("castle/tower-square-base", MeshKit.At(0, 0, 0, 0.2f, 2.2f), stone);
                k.Add("castle/tower-square-mid-windows", MeshKit.At(0, 2.2f, 0, 0.2f, 2.2f), stone);
                k.Add("castle/wall-half", MeshKit.At(2.3f, 0, 0.6f, 0.3f, 1.6f), stone);
                k.Add("castle/wall-corner", MeshKit.At(-2.2f, 0, 0.9f, -0.2f, 1.6f), stone);
                k.Add("castle/rocks-small", MeshKit.At(1.4f, 0, 2, 0, 1.8f), stone);
                k.Add("castle/rocks-large", MeshKit.At(-1.2f, 0, 2.2f, 0.7f, 1.6f), stone);
                break;
            case "temple":
                // A sunken temple: a cracked platform, broken columns and a great stone head.
                k.Add("nature/platform_stone", MeshKit.At(0, 0, 0, 0, 7f), stone);
                for (int i = 0; i < 6; i++)
                {
                    float x = -2.4f + (i % 3) * 2.4f, z = i < 3 ? -1.8f : 1.8f;
                    float h = i % 2 == 0 ? 1.6f : 0.9f;
                    k.Add("town/pillar-stone", new Transform3D(Basis.Identity.Scaled(new Vector3(2.2f, h * 2.2f, 2.2f)), new Vector3(x, 0, z)), stone);
                }
                k.Add("nature/statue_head", MeshKit.At(0, 0, -0.4f, 0, 2.4f), stone);
                k.Add("nature/lily_large", MeshKit.At(3.3f, 0, 2.5f, 0, 3f), WorldView.Foliage);
                break;
            case "ford":
                // Stepping stones and a great hut of hides where something big sleeps.
                k.Add("nature/tent_detailedClosed", MeshKit.At(0, 0, -0.6f, 0.3f, 4.2f), Palette.Cloth(new Color("7a5a3a")));
                k.Add("nature/rock_largeA", MeshKit.At(-2.8f, 0, 1.2f, 0, 2.5f), stone);
                k.Add("nature/rock_largeC", MeshKit.At(2.6f, 0, 1.7f, 0.5f, 2.2f), stone);
                k.Add("nature/log_large", MeshKit.At(1.8f, 0, -2.2f, 0.4f, 2f));
                k.Add("nature/campfire_logs", MeshKit.At(-0.8f, 0, 1.9f, 0, 2.6f));
                break;
            case "camp":
                // The war camp: hide tents, a stake ring, a ram and the warband's red banner.
                var hide = Palette.Cloth(new Color("6a2a1f"));
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.Tau / 5;
                    k.Add("nature/tent_detailedOpen", MeshKit.At(Mathf.Cos(a) * 2.4f, 0, Mathf.Sin(a) * 2.4f, -a - Mathf.Pi / 2, 2.6f), hide);
                }
                for (int i = 0; i < 36; i++)
                {
                    float a = i * Mathf.Tau / 36;
                    var p = new Vector3(Mathf.Cos(a) * 4.6f, 0, Mathf.Sin(a) * 4.6f);
                    k.Frustum(p, 0.12f, 0.12f, 0.7f, 5, Palette.DarkTimber);
                    k.Frustum(p + new Vector3(0, 0.7f, 0), 0.12f, 0, 0.25f, 5, Palette.DarkTimber);
                }
                k.Add("castle/siege-ram", MeshKit.At(0.6f, 0, 3.4f, 0.3f, 1.2f), Palette.Kenney(new Color("6a2a1f")));
                k.Add("castle/flag-banner-long", MeshKit.At(0, 0, 0, 0, 2f), Palette.Kenney(new Color("c0392b")));
                k.Add("nature/campfire_logs", MeshKit.At(0, 0, 0.8f, 0, 2.6f));
                if (!cleared) k.Box(new Vector3(0, 0.02f, 0.8f), new Vector3(0.4f, 0.08f, 0.4f), new Color("ff6a1a", MeshKit.Flag.Ember));
                break;
        }
        if (cleared)
            k.Add("castle/flag", MeshKit.At(1.2f, 0, 2.8f, 0, 2.4f), Palette.Kenney(Palette.Gold));
        return k.Build();
    }
}
