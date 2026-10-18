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
    private readonly Dictionary<string, MeshInstance3D> _landmarks = [];
    private string _groundKey = "", _wallKey = "", _buildKey = "";

    public override void _Ready()
    {
        AddChild(_ground);
        AddChild(_walls);
    }

    public void Reset()
    {
        foreach (var b in _batches.Values) b.QueueFree();
        _batches.Clear();
        foreach (var l in _landmarks.Values) l.QueueFree();
        _landmarks.Clear();
        _groundKey = _wallKey = _buildKey = "";
    }

    public void Refresh()
    {
        var town = map.Sim.Town;
        Lots(town);
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
        var groups = new SortedDictionary<string, List<(Vector3 P, float Yaw, bool Sign)>>(StringComparer.Ordinal);
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            var lot = town.Lot(b.LotId);
            var p = new Vector3(Geo.Margin + lot.X + 0.5f, Y, Geo.Margin + lot.Y + 0.5f);
            string key = MeshKey(b);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = [];
            // Most buildings face the street to the south; a few turn their backs for variety.
            float yaw = b.Def.Id is "farm" or "lumber_camp" ? (b.Id % 2) * Mathf.Pi / 2
                : b.Def.Id == "house" && b.Id % 3 == 1 ? Mathf.Pi / 2 : 0;
            list.Add((p, yaw, b.Closed || b.ForSale));
            if (b.Closed || b.ForSale)
            {
                if (!groups.TryGetValue("sign", out var signs)) groups["sign"] = signs = [];
                signs.Add((p + new Vector3(-0.32f, 0, 0.38f), 0.3f, false));
            }
        }
        string all = string.Join('|', groups.Select(g => $"{g.Key}:{string.Join(',', g.Value.Select(v => $"{v.P.X},{v.P.Z}"))}"));
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
                mmi.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.FromEuler(new Vector3(0, list[i].Yaw, 0)), list[i].P));
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
            case "timber_house": TimberHouse(kit, v); break;
            case "townhouse": Townhouse(kit, v); break;
            case "manor": Manor(kit, v); break;
            case "tenement": Tenement(kit, v); break;
            case "quarry": Quarry(kit, v); break;
            case "sheepfold": Sheepfold(kit, v); break;
            case "weaver": Weaver(kit, v); break;
            case "carpenter": Carpenter(kit, v); break;
            case "herb_garden": HerbGarden(kit, v); break;
            case "apothecary": Apothecary(kit, v); break;
            case "fishery": Fishery(kit, v); break;
            case "vineyard": Vineyard(kit, v); break;
            case "scriptorium": Scriptorium(kit, v); break;
            case "bathhouse": Bathhouse(kit, v); break;
            case "theatre": Theatre(kit, v); break;
            case "market_hall": MarketHall(kit, v); break;
            case "courthouse": Courthouse(kit, v); break;
            case "library": Library(kit, v); break;
            case "barracks": Barracks(kit, v); break;
            case "cathedral": Cathedral(kit); break;
            case "arena": Arena(kit, v); break;
            case "keep": Keep(kit); break;
            case "guildhall": Guildhall(kit, v); break;
            default: House(kit, v); break;
        }
        return _meshes[key] = kit.Build();
    }

    /// <summary>
    /// Walls of Kenney town-kit sections around a w by d footprint (in sections), on the given floor,
    /// with a door on the street side; windows get a pane that lights up at night. Skip leaves out
    /// sides hidden against a neighbour (1 east, 2 west, 4 south, 8 north).
    /// </summary>
    private static void Walls(MeshKit k, int w, int d, int floor, bool stone, int seed, bool door, int skip = 0)
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
                for (int s = 0; s < 4; s++)
                {
                    var (edge, yaw, front) = sides[s];
                    if (!edge || (skip & (1 << s)) != 0) continue;
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

    // ---------- City buildings ----------

    private static readonly Func<Color, Color> StoneTint = Palette.Kenney(Palette.Stone);
    private static readonly Color Slate = new("58626f");
    private static readonly Color Lawn = new("6f8f45");
    private static readonly Color Hedge = new("3f6a32");
    private static readonly Color[] Dyes = [new("b8322a"), new("3a5fa8"), new("d8a83a"), new("4f8a3a"), new("8a4a9a"), new("e07a3a")];

    /// <summary>A window pane on an outward face with horizontal normal n, lit at night.</summary>
    private static void Pane(MeshKit k, Vector3 c, Vector3 n, float hw, float hh, Color? col = null)
    {
        var t = new Vector3(n.Z, 0, -n.X) * hw;
        var u = new Vector3(0, hh, 0);
        k.Quad(c - t - u, c - t + u, c + t + u, c + t - u, col ?? new Color("3a3026", MeshKit.Flag.Window));
    }

    /// <summary>A quad seen from both sides: cloth, banners, washing.</summary>
    private static void Sheet(MeshKit k, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        k.Quad(a, b, c, d, col);
        k.Quad(d, c, b, a, col);
    }

    /// <summary>A pitched roof whose ridge runs front to back (gable to the street) over a w (x) by d (z) footprint.</summary>
    private static void GableZ(MeshKit k, float y, float w, float d, float h, Color roof, Color gable, float over = 0.1f)
    {
        k.Push(MeshKit.At(0, y, 0, Mathf.Pi / 2));
        k.Gable(0, d, w, h, roof, gable, over);
        k.Pop();
    }

    /// <summary>Plain stone walls of several storeys with tall windows running up them, for halls of learning and worship.</summary>
    private static void TallWalls(MeshKit k, int w, int d, int floors, bool door, bool frontPanes = true)
    {
        for (int i = 0; i < w; i++)
            for (int j = 0; j < d; j++)
            {
                float cx = i - w / 2f + 0.5f, cz = j - d / 2f + 0.5f;
                (bool Edge, float Yaw, bool Front)[] sides =
                [
                    (i == w - 1, 0, false), (i == 0, Mathf.Pi, false), (j == d - 1, -Mathf.Pi / 2, true), (j == 0, Mathf.Pi / 2, false),
                ];
                foreach (var (edge, yaw, front) in sides)
                {
                    if (!edge) continue;
                    bool isDoor = front && door && i == w / 2;
                    for (int f = 0; f < floors; f++)
                        k.Add(isDoor && f == 0 ? "town/wall-door" : "town/wall", new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)), new Vector3(cx, f, cz)), StoneTint);
                    if (isDoor || (front && !frontPanes)) continue;
                    var n = new Vector3(Mathf.Cos(yaw), 0, -Mathf.Sin(yaw));
                    Pane(k, new Vector3(cx, floors * 0.5f + 0.05f, cz) + n * 0.51f, n, 0.12f, floors * 0.36f);
                }
            }
    }

    private static void TowerStack(MeshKit k, float x, float z, float s, string[] pieces, Func<Color, Color> tint)
    {
        float y = 0;
        foreach (var p in pieces)
        {
            k.Add("castle/" + p, MeshKit.At(x, y, z, 0, s), tint);
            y += p switch { "tower-square-top" => 0.3f, "tower-square-top-roof" => 1f, "tower-square-roof" => 2f, _ => 1.01f } * s;
        }
    }

    /// <summary>A cottage grown a storey: a plastered ground floor under a jettied timber upper floor.</summary>
    private static void TimberHouse(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.03f, 0, Fit(2, 2, 0.78f)));
        Walls(k, 2, 2, 0, false, v + 111, true);
        k.Box(new Vector3(0, 0.96f, 0), new Vector3(2.16f, 0.08f, 2.16f), Palette.DarkTimber);
        k.Push(new Transform3D(Basis.FromScale(new Vector3(1.07f, 1, 1.07f)), Vector3.Zero));
        Walls(k, 2, 2, 1, false, v + 113, false);
        k.Pop();
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 2.14f, 2.14f, 1.15f, RoofOf(v + 1), Palette.Plaster, 0.12f);
        k.Pop();
        Chimney(k, -0.55f, 2, -0.3f, v % 2 == 0);
        k.Pop();
    }

    /// <summary>A pair of narrow gable-fronted townhouses: stone ground floor, timber storeys above.</summary>
    private static void Townhouse(MeshKit k, int v)
    {
        for (int h = 0; h < 2; h++)
        {
            int floors = h == 0 || v < 2 ? 3 : 2;
            k.Push(MeshKit.At(h == 0 ? -0.235f : 0.235f, 0, -0.02f, 0, 0.44f));
            // The party wall between the pair is never seen.
            int party = h == 0 ? 1 : 2;
            Walls(k, 1, 2, 0, true, v * 7 + h * 3 + 121, true, party);
            for (int f = 1; f < floors; f++) Walls(k, 1, 2, f, false, v * 7 + h * 3 + 121 + f, false, party);
            k.Box(new Vector3(0, 0.97f, 0), new Vector3(1.06f, 0.06f, 2.06f), Palette.DarkTimber);
            GableZ(k, floors, 1, 2, 0.9f, RoofOf(v + h * 2 + 1), Palette.Plaster, 0.08f);
            Chimney(k, 0.24f, floors, -0.65f, (v + h) % 3 == 0);
            k.Pop();
        }
    }

    /// <summary>A manor: a broad two-storey stone house with a porch tower, behind a hedged garden.</summary>
    private static void Manor(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), Lawn);
        k.Box(new Vector3(-0.46f, 0, 0), new Vector3(0.06f, 0.12f, 0.96f), Hedge, Hedge.Lightened(0.1f));
        k.Box(new Vector3(0.46f, 0, 0), new Vector3(0.06f, 0.12f, 0.96f), Hedge, Hedge.Lightened(0.1f));
        k.Box(new Vector3(-0.28f, 0, 0.46f), new Vector3(0.36f, 0.12f, 0.06f), Hedge, Hedge.Lightened(0.1f));
        k.Box(new Vector3(0.28f, 0, 0.46f), new Vector3(0.36f, 0.12f, 0.06f), Hedge, Hedge.Lightened(0.1f));
        foreach (float x in new[] { -0.09f, 0.09f })
            k.Box(new Vector3(x, 0, 0.46f), new Vector3(0.05f, 0.17f, 0.05f), new Color("b8b0a0"));
        k.Box(new Vector3(0, 0.012f, 0.36f), new Vector3(0.12f, 0.01f, 0.22f), new Color("c8bca0"));
        foreach (float x in new[] { -0.3f, 0.3f })
            k.Blob(new Vector3(x, 0.08f, 0.3f), new Vector3(0.07f, 0.08f, 0.07f), new Color("4f7a3a"), 0.2f, (int)(x * 10) + v, Hedge);
        var roof = v % 2 == 0 ? Slate : new Color("6e4a3a");
        k.Push(MeshKit.At(0, 0, -0.17f, 0, 0.28f));
        Walls(k, 3, 2, 0, true, v + 131, false);
        Walls(k, 3, 2, 1, true, v + 133, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.1f, roof, Palette.Stone, 0.12f);
        k.Pop();
        Chimney(k, -1.15f, 2, -0.3f, true);
        Chimney(k, 1.15f, 2, -0.3f);
        // The porch tower, a storey above the roofs, gilded at the top.
        k.Push(MeshKit.At(0, 0, 1.25f));
        for (int f = 0; f < 3; f++) Walls(k, 1, 1, f, true, v + 135 + f, f == 0);
        k.Pyramid(3, 1, 1, 0.9f, roof, 0.08f);
        k.Pop();
        k.Frustum(new Vector3(0, 3.85f, 1.25f), 0.08f, 0, 0.45f, 6, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pop();
    }

    /// <summary>A tenement: a tall, crowded three-storey timber block with washing strung across its front.</summary>
    private static void Tenement(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.04f, 0, Fit(2, 2, 0.9f)));
        for (int f = 0; f < 3; f++) Walls(k, 2, 2, f, false, v * 5 + 141 + f, f == 0);
        k.Push(MeshKit.At(0, 3, 0));
        k.Gable(0, 2, 2, 0.75f, v % 2 == 0 ? new Color("4f4a46") : new Color("6a5442"), new Color("b8a888"), 0.1f);
        k.Pop();
        Chimney(k, -0.6f, 3, -0.4f, true);
        Chimney(k, 0.1f, 3, -0.45f);
        Chimney(k, 0.7f, 3, 0.4f, v % 2 == 1);
        // A washing line from the first-floor windows, hung with the household's linen.
        k.Box(new Vector3(0, 1.62f, 1.25f), new Vector3(2.1f, 0.03f, 0.03f), Palette.DarkTimber);
        for (int i = 0; i < 4; i++)
        {
            float x = -0.8f + i * 0.5f + (v % 2) * 0.1f;
            var c = i % 2 == 0 ? new Color("e8e0cc") : Dyes[(v + i) % Dyes.Length].Lightened(0.2f);
            Sheet(k, new Vector3(x, 1.62f, 1.25f), new Vector3(x, 1.3f, 1.25f), new Vector3(x + 0.3f, 1.3f, 1.25f), new Vector3(x + 0.3f, 1.62f, 1.25f), c);
        }
        k.Pop();
    }

    /// <summary>A quarry: a stepped rock face, cut blocks stacked for the carts, and a timber hoist.</summary>
    private static void Quarry(MeshKit k, int v)
    {
        var rock = new Color("9a948a");
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("8e877c"));
        k.Box(new Vector3(0, 0, -0.36f), new Vector3(0.96f, 0.42f, 0.26f), rock.Darkened(0.12f), rock);
        k.Box(new Vector3(-0.08f, 0, -0.17f), new Vector3(0.8f, 0.24f, 0.14f), rock.Darkened(0.06f), rock.Lightened(0.06f));
        k.Box(new Vector3(-0.2f, 0, -0.05f), new Vector3(0.5f, 0.1f, 0.12f), rock, rock.Lightened(0.1f));
        var stone = Palette.Kenney(new Color("8a8478"));
        k.Add("castle/rocks-large", MeshKit.At(0.36f, 0, -0.12f, v, 0.28f), stone);
        k.Add("castle/rocks-small", MeshKit.At(0.12f, 0, 0.3f, v * 0.7f, 0.18f), stone);
        // Cut blocks, squared and stacked.
        var cut = new Color("cfc6b2");
        int n = 3 + v % 2;
        for (int i = 0; i < n; i++)
            k.Box(new Vector3(-0.36f + (i % 3) * 0.12f, (i / 3) * 0.08f, 0.3f), new Vector3(0.1f, 0.08f, 0.1f), cut.Darkened(i * 0.04f), cut.Lightened(0.05f));
        // The hoist: a mast and jib with a block on the rope.
        k.Box(new Vector3(0.3f, 0, 0.08f), new Vector3(0.05f, 0.72f, 0.05f), Palette.Timber);
        k.Box(new Vector3(0.18f, 0.68f, 0.08f), new Vector3(0.36f, 0.04f, 0.04f), Palette.Timber);
        k.Box(new Vector3(0.03f, 0.3f, 0.08f), new Vector3(0.012f, 0.38f, 0.012f), new Color("c8b48a"));
        k.Box(new Vector3(0.03f, 0.22f, 0.08f), new Vector3(0.1f, 0.08f, 0.1f), cut);
        k.Box(new Vector3(0.3f, 0, 0.08f), new Vector3(0.14f, 0.04f, 0.14f), Palette.DarkTimber);
    }

    /// <summary>A sheepfold: a fenced pasture, a few white sheep and a small shed.</summary>
    private static void Sheepfold(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("7f9a4e"));
        for (int i = 0; i < 4; i++) k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, i * Mathf.Pi / 2, 0.98f));
        k.Push(MeshKit.At(-0.22f, 0, -0.27f, 0, 0.24f));
        Walls(k, 2, 1, 0, false, v + 151, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.75f, new Color("7a5a3a"), Palette.Timber, 0.12f);
        k.Pop();
        k.Pop();
        k.Frustum(new Vector3(0.3f, 0, -0.3f), 0.09f, 0.05f, 0.12f, 8, new Color("d8b45a"), new Color("e8c870"));
        var wool = new Color("f2eee2");
        var shade = new Color("c8c2b2");
        var face = new Color("3a3430");
        for (int i = 0; i < 5; i++)
        {
            float x = -0.3f + Geo.Hash(v * 17 + i * 3) * 0.6f, z = -0.02f + Geo.Hash(v * 17 + i * 3 + 1) * 0.36f;
            float a = Geo.Hash(v * 17 + i * 3 + 2) * Mathf.Tau;
            var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
            k.Blob(new Vector3(x, 0.075f, z), new Vector3(0.075f, 0.055f, 0.075f), wool, 0.2f, i + v * 5, shade);
            k.Blob(new Vector3(x, 0.09f, z) + dir * 0.08f, new Vector3(0.03f, 0.03f, 0.03f), face);
            k.Box(new Vector3(x, 0, z), new Vector3(0.08f, 0.04f, 0.04f), face);
        }
    }

    /// <summary>A weaver: a timber house with dyed cloth drying on lines and a banner by the door.</summary>
    private static void Weaver(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.13f, 0, -0.16f, 0, Fit(2, 2, 0.66f)));
        Walls(k, 2, 2, 0, false, v + 161, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 1.05f, RoofOf(v + 2), Palette.Plaster, 0.12f);
        k.Pop();
        k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(0.6f, 0.35f, 0.62f)), Palette.Kenney(Dyes[(v + 4) % Dyes.Length]));
        k.Pop();
        // Two drying lines, front and side, hung with lengths of dyed cloth.
        foreach (var (a, b) in new[] { (new Vector3(-0.42f, 0, 0.36f), new Vector3(0.42f, 0, 0.36f)), (new Vector3(0.36f, 0, -0.4f), new Vector3(0.36f, 0, 0.2f)) })
        {
            k.Box(a, new Vector3(0.03f, 0.42f, 0.03f), Palette.Timber);
            k.Box(b, new Vector3(0.03f, 0.42f, 0.03f), Palette.Timber);
            var dir = b - a;
            int n = (int)(dir.Length() / 0.2f);
            for (int i = 0; i < n; i++)
            {
                var p0 = a + dir * ((i + 0.15f) / n) + new Vector3(0, 0.4f, 0);
                var p1 = a + dir * ((i + 0.85f) / n) + new Vector3(0, 0.4f, 0);
                float drop = 0.2f + Geo.Hash(v * 13 + i) * 0.1f;
                Sheet(k, p0, p0 - new Vector3(0, drop, 0), p1 - new Vector3(0, drop, 0), p1, Dyes[(v + i * 2 + (int)a.X) % Dyes.Length]);
            }
        }
    }

    private static void Table(MeshKit k, Vector3 p, float w, float d, float h, Color col)
    {
        k.Box(p + new Vector3(0, h - 0.015f, 0), new Vector3(w, 0.015f, d), col);
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
            k.Box(p + new Vector3(sx * (w / 2 - 0.01f), 0, sz * (d / 2 - 0.01f)), new Vector3(0.012f, h - 0.015f, 0.012f), col.Darkened(0.2f));
    }

    /// <summary>A carpenter: a workshop with planks stacked and new furniture set out front.</summary>
    private static void Carpenter(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.22f, 0, 0.42f));
        Walls(k, 2, 1, 0, false, v + 171, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.8f, RoofOf(v), Palette.Timber, 0.12f);
        k.Pop();
        k.Pop();
        // A lean-to over the workbench.
        k.Box(new Vector3(0.3f, 0.32f, 0.08f), new Vector3(0.34f, 0.025f, 0.24f), Palette.DarkTimber);
        k.Box(new Vector3(0.43f, 0, 0.18f), new Vector3(0.025f, 0.32f, 0.025f), Palette.Timber);
        k.Box(new Vector3(0.17f, 0, 0.18f), new Vector3(0.025f, 0.32f, 0.025f), Palette.Timber);
        Table(k, new Vector3(0.3f, 0, 0.06f), 0.24f, 0.1f, 0.1f, new Color("a07a4a"));
        // Planks, stacked crosswise.
        for (int i = 0; i < 4; i++)
            k.Box(new Vector3(-0.28f, i * 0.025f, 0.22f), i % 2 == 0 ? new Vector3(0.34f, 0.025f, 0.12f) : new Vector3(0.3f, 0.025f, 0.1f), new Color("c89a62").Darkened(i * 0.05f));
        // Furniture for sale: a table and two chairs, a chest.
        var wood = v % 2 == 0 ? new Color("9a6a3a") : new Color("b0824a");
        Table(k, new Vector3(0.02f, 0, 0.32f), 0.16f, 0.1f, 0.08f, wood);
        foreach (float x in new[] { -0.1f, 0.14f })
        {
            k.Box(new Vector3(x, 0.045f, 0.32f), new Vector3(0.05f, 0.012f, 0.05f), wood);
            k.Box(new Vector3(x + (x < 0 ? -0.025f : 0.025f), 0, 0.32f), new Vector3(0.01f, 0.1f, 0.05f), wood.Darkened(0.15f));
        }
        k.Box(new Vector3(-0.3f, 0, 0.4f), new Vector3(0.14f, 0.08f, 0.08f), wood.Darkened(0.1f), new Color("6a4422"));
        k.Add("nature/log", MeshKit.At(0.32f, 0, 0.36f, Mathf.Pi / 2, 0.7f));
    }

    /// <summary>A herb garden: raised beds of green and purple herbs and a little potting hut.</summary>
    private static void HerbGarden(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("7a6a48"));
        Color[] herbs = [new("4f8a3a"), new("8a5ab0"), new("6fa04a"), new("d8c040"), new("3f7a4a")];
        for (int b = 0; b < 3; b++)
        {
            float x = -0.36f + b * 0.24f;
            k.Box(new Vector3(x, 0, 0.02f), new Vector3(0.18f, 0.06f, 0.86f), Palette.Timber, new Color("4e3a28"));
            for (int i = 0; i < 6; i++)
            {
                var c = herbs[(b * 2 + i + v) % herbs.Length];
                k.Blob(new Vector3(x + (i % 2 == 0 ? -0.03f : 0.03f), 0.09f, -0.34f + i * 0.14f), new Vector3(0.06f, 0.05f, 0.06f), c, 0.3f, b * 7 + i + v, c.Darkened(0.25f));
            }
        }
        k.Push(MeshKit.At(0.33f, 0, -0.25f, 0, 0.3f));
        Walls(k, 1, 1, 0, false, v + 181, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 1, 1, 0.6f, new Color("5a7a3e"), Palette.Timber, 0.12f);
        k.Pop();
        k.Pop();
        k.Add("nature/pot_small", MeshKit.At(0.26f, 0, 0.12f, 0, 0.45f));
        k.Add("nature/pot_small", MeshKit.At(0.4f, 0, 0.2f, 1, 0.4f));
        k.Blob(new Vector3(0.26f, 0.12f, 0.12f), new Vector3(0.05f, 0.05f, 0.05f), herbs[(v + 1) % herbs.Length]);
        k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, Mathf.Pi, 0.98f));
    }

    /// <summary>An apothecary: a narrow tall shop with green-glass bottles glowing on a shelf and a green sign.</summary>
    private static void Apothecary(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.2f, 0, -0.02f, 0, 0.44f));
        Walls(k, 1, 2, 0, true, v + 191, true);
        Walls(k, 1, 2, 1, false, v + 193, false);
        GableZ(k, 2, 1, 2, 0.95f, new Color("3f5e46"), Palette.Plaster, 0.08f);
        Chimney(k, 0.25f, 2, -0.6f, true);
        k.Pop();
        // The hanging sign, a glowing flask.
        k.Box(new Vector3(0.04f, 0.6f, 0.38f), new Vector3(0.14f, 0.02f, 0.02f), Palette.DarkTimber);
        k.Box(new Vector3(0.09f, 0.44f, 0.38f), new Vector3(0.1f, 0.14f, 0.02f), new Color("4fd88a", MeshKit.Flag.Magic));
        // A shelf of bottles out front.
        k.Box(new Vector3(0.27f, 0, 0.22f), new Vector3(0.36f, 0.16f, 0.12f), Palette.Timber, new Color("6a4a2a"));
        Color[] glass = [new("4fd88a"), new("3ab8a0"), new("8ae04a"), new("b06ae0")];
        for (int i = 0; i < 5; i++)
        {
            var c = new Color(glass[(i + v) % glass.Length], MeshKit.Flag.Magic);
            var p = new Vector3(0.13f + i * 0.07f, 0.16f, 0.22f);
            k.Frustum(p, 0.026f, 0.022f, 0.06f, 6, c);
            k.Frustum(p + new Vector3(0, 0.06f, 0), 0.01f, 0.008f, 0.03f, 4, c.Darkened(0.2f));
        }
        k.Add("nature/pot_small", MeshKit.At(0.34f, 0, -0.25f, v, 0.5f));
        k.Blob(new Vector3(0.34f, 0.13f, -0.25f), new Vector3(0.06f, 0.06f, 0.06f), new Color("6fa04a"));
    }

    /// <summary>A fishery: a fish shed, racks of fish drying, a beached boat and barrels of salt.</summary>
    private static void Fishery(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.02f, 0.98f), new Color("8a6a48"), new Color("9a7a52"));
        k.Push(MeshKit.At(-0.2f, 0, -0.26f, 0, 0.3f));
        Walls(k, 2, 1, 0, false, v + 201, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.8f, new Color("5a6a6a"), Palette.Timber, 0.14f);
        k.Pop();
        k.Pop();
        // Drying racks: two rails on posts, hung with silver fish.
        var fish = new Color("b8c4cc");
        for (int r = 0; r < 2; r++)
        {
            float z = 0.08f + r * 0.18f;
            k.Box(new Vector3(-0.42f, 0, z), new Vector3(0.025f, 0.3f, 0.025f), Palette.Timber);
            k.Box(new Vector3(0.02f, 0, z), new Vector3(0.025f, 0.3f, 0.025f), Palette.Timber);
            k.Box(new Vector3(-0.2f, 0.28f, z), new Vector3(0.46f, 0.02f, 0.02f), Palette.DarkTimber);
            for (int i = 0; i < 6; i++)
                k.Box(new Vector3(-0.38f + i * 0.07f, 0.17f, z), new Vector3(0.03f, 0.11f, 0.012f), fish.Darkened(Geo.Hash(v + i * 7 + r) * 0.2f));
        }
        // A rowing boat drawn up, keel to the sky as often as not.
        k.Push(MeshKit.At(0.32f, 0, 0.1f, v % 2 == 0 ? 0 : 0.25f));
        k.Box(new Vector3(0, 0, 0), new Vector3(0.16f, 0.07f, 0.46f), new Color("6a4a2a"), new Color("3e2a18"));
        k.Box(new Vector3(0, 0, 0.26f), new Vector3(0.08f, 0.06f, 0.08f), new Color("6a4a2a"));
        k.Box(new Vector3(0, 0, -0.26f), new Vector3(0.08f, 0.06f, 0.08f), new Color("6a4a2a"));
        k.Box(new Vector3(0, 0.05f, 0), new Vector3(0.14f, 0.015f, 0.04f), new Color("8a6a42"));
        k.Pop();
        for (int i = 0; i < 2; i++)
            k.Frustum(new Vector3(0.36f - i * 0.11f, 0, -0.33f), 0.05f, 0.055f, 0.11f, 8, new Color("8a5a2b"), new Color("d8d0c0"));
    }

    /// <summary>A vineyard: rows of vines trained on stakes and wire, and a small stone press house.</summary>
    private static void Vineyard(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("8a6a44"));
        var leaf = new Color("4f7f32");
        var grape = new Color("5a2a6a");
        for (int r = 0; r < 3; r++)
        {
            float x = -0.4f + r * 0.2f;
            k.Box(new Vector3(x, 0.17f, 0), new Vector3(0.008f, 0.008f, 0.86f), new Color("8a8478"));
            for (int i = 0; i < 5; i++)
            {
                float z = -0.38f + i * 0.19f;
                k.Box(new Vector3(x, 0, z), new Vector3(0.016f, 0.2f, 0.016f), Palette.Timber);
                k.Blob(new Vector3(x, 0.14f, z + 0.09f), new Vector3(0.05f, 0.06f, 0.08f), leaf.Darkened(Geo.Hash(r * 5 + i + v) * 0.15f), 0.3f, r * 5 + i + v, leaf.Darkened(0.3f));
                if ((i + r + v) % 2 == 0) k.Blob(new Vector3(x + 0.04f, 0.1f, z + 0.06f), new Vector3(0.022f, 0.03f, 0.022f), grape);
            }
        }
        k.Push(MeshKit.At(0.3f, 0, -0.22f, 0, 0.32f));
        Walls(k, 1, 1, 0, true, v + 211, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 1, 1, 0.6f, new Color("9a4a36"), Palette.Stone, 0.12f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 2; i++)
        {
            var p = new Vector3(0.24f + i * 0.13f, 0, 0.18f);
            k.Frustum(p, 0.05f, 0.055f, 0.12f, 8, new Color("8a5a2b"), new Color("5a2a3a"));
        }
        k.Frustum(new Vector3(0.3f, 0, 0.36f), 0.06f, 0.06f, 0.05f, 8, new Color("6a4a2a"), grape);
    }

    /// <summary>A scriptorium: a stone house of tall windows with a little bell-cote on the ridge.</summary>
    private static void Scriptorium(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.04f, 0, Fit(2, 2, 0.78f)));
        TallWalls(k, 2, 2, 2, true);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 2, 2, 1.1f, v % 2 == 0 ? Slate : new Color("7e4430"), new Color("c8c0b0"), 0.12f);
        k.Pop();
        // A round window in each gable.
        Pane(k, new Vector3(1.01f, 2.35f, 0), Vector3.Right, 0.16f, 0.16f);
        Pane(k, new Vector3(-1.01f, 2.35f, 0), Vector3.Left, 0.16f, 0.16f);
        // The bell-cote: an open stone frame on the ridge with a bronze bell.
        var stone = new Color("b8b0a0");
        k.Box(new Vector3(0.6f, 3.0f, -0.18f), new Vector3(0.06f, 0.5f, 0.06f), stone);
        k.Box(new Vector3(0.6f, 3.0f, 0.18f), new Vector3(0.06f, 0.5f, 0.06f), stone);
        k.Blob(new Vector3(0.6f, 3.28f, 0), new Vector3(0.09f, 0.11f, 0.09f), new Color("c8963a"));
        k.Push(MeshKit.At(0.6f, 3.5f, 0));
        k.Pyramid(0, 0.24f, 0.5f, 0.3f, Slate, 0.04f);
        k.Pop();
        k.Pop();
    }

    /// <summary>A bathhouse: a stone hall under a green copper dome, steam rising from it.</summary>
    private static void Bathhouse(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.04f, 0, Fit(2, 2, 0.82f)));
        Walls(k, 2, 2, 0, true, v + 221, true);
        k.Box(new Vector3(0, 0.98f, 0), new Vector3(2.12f, 0.14f, 2.12f), new Color("a8a296"), new Color("b8b2a6"));
        k.Frustum(new Vector3(0, 1.12f, 0), 0.82f, 0.82f, 0.18f, 12, new Color("b8b2a6"));
        k.Blob(new Vector3(0, 1.3f, 0), new Vector3(0.8f, 0.62f, 0.8f), new Color("6a9a88"), 0, 0, new Color("5a8a78"));
        k.Frustum(new Vector3(0, 1.88f, 0), 0.07f, 0, 0.3f, 6, new Color(Palette.Gold, MeshKit.Flag.Magic));
        var steam = new Color("eef0f0");
        k.Blob(new Vector3(0.6f, 2.0f, -0.5f), new Vector3(0.16f, 0.12f, 0.16f), steam, 0.3f, v);
        k.Blob(new Vector3(0.75f, 2.35f, -0.6f), new Vector3(0.12f, 0.09f, 0.12f), steam, 0.3f, v + 1);
        k.Blob(new Vector3(-0.7f, 1.75f, 0.4f), new Vector3(0.12f, 0.09f, 0.12f), steam, 0.3f, v + 2);
        foreach (float x in new[] { -0.5f, 0.5f })
            k.Add("town/pillar-stone", MeshKit.At(x, 0, 1.12f, 0, 1), StoneTint);
        k.Pop();
    }

    /// <summary>A theatre: an open half-round of stepped seats facing a stage with a painted backdrop and banners.</summary>
    private static void Theatre(MeshKit k, int v)
    {
        var seat = new Color("b8ae98");
        var dye = Dyes[v % Dyes.Length];
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("a89a7e"));
        // The stage at the back, with a backdrop and a canopy on wooden posts.
        k.Box(new Vector3(0, 0, -0.3f), new Vector3(0.62f, 0.09f, 0.26f), new Color("8a6a42"), new Color("a07a4a"));
        k.Box(new Vector3(0, 0, -0.44f), new Vector3(0.7f, 0.5f, 0.04f), new Color("8f8a80"), new Color("a8a296"));
        k.Box(new Vector3(0, 0.12f, -0.418f), new Vector3(0.5f, 0.3f, 0.01f), dye);
        foreach (float x in new[] { -0.29f, 0.29f })
        {
            k.Box(new Vector3(x, 0.09f, -0.19f), new Vector3(0.03f, 0.36f, 0.03f), Palette.Timber);
            k.Add("castle/flag-banner-long", MeshKit.At(x * 1.45f, 0, -0.4f, 0, 0.3f), Palette.Kenney(Dyes[(v + 2) % Dyes.Length]));
        }
        k.Push(MeshKit.At(0, 0.45f, -0.31f));
        k.Gable(0, 0.66f, 0.26f, 0.12f, dye.Darkened(0.2f), dye, 0.03f);
        k.Pop();
        // Three tiers of seats in a half round.
        var c = new Vector3(0, 0, -0.18f);
        const int Segs = 8;
        for (int s = 0; s < Segs; s++)
        {
            float a = (s + 0.5f) / Segs * Mathf.Pi;
            for (int t = 0; t < 3; t++)
            {
                float r = 0.3f + t * 0.085f;
                k.Push(new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2 - a, 0)), c + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)));
                k.Box(Vector3.Zero, new Vector3(r * Mathf.Pi / Segs * 1.08f, 0.05f + t * 0.06f, 0.09f), seat.Darkened(t * 0.05f), seat.Lightened(0.06f));
                k.Pop();
            }
        }
        foreach (float a in new[] { 0.06f, Mathf.Pi - 0.06f })
            k.Add("castle/flag-pennant", MeshKit.At(Mathf.Cos(a) * 0.47f, 0.17f, -0.18f + Mathf.Sin(a) * 0.47f, 0, 0.35f), Palette.Kenney(dye));
    }

    /// <summary>A market hall: a timber upper floor carried on stone pillars over an open arcade of stalls.</summary>
    private static void MarketHall(MeshKit k, int v)
    {
        k.Box(new Vector3(0, 0, -0.02f), new Vector3(0.9f, 0.03f, 0.86f), new Color("a8a296"), new Color("b8b2a4"));
        foreach (float x in new[] { -0.37f, 0, 0.37f })
            foreach (float z in new[] { -0.39f, 0.35f })
                k.Add("town/pillar-stone", new Transform3D(Basis.FromScale(new Vector3(0.6f, 0.4f, 0.6f)), new Vector3(x, 0.03f, z)), StoneTint);
        k.Box(new Vector3(0, 0.42f, -0.02f), new Vector3(0.82f, 0.04f, 0.82f), Palette.DarkTimber);
        k.Push(MeshKit.At(0, 0.46f, -0.02f, 0, 0.38f));
        Walls(k, 2, 2, 0, false, v + 231, false);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 2, 1.05f, v % 2 == 0 ? new Color("9a4a36") : new Color("7e4430"), Palette.Plaster, 0.14f);
        k.Pop();
        k.Pop();
        k.Add("town/stall-red", MeshKit.At(-0.18f, 0.03f, -0.02f, 0, 0.3f));
        k.Add("town/stall-green", MeshKit.At(0.18f, 0.03f, -0.02f, 0, 0.3f));
        k.Box(new Vector3(-0.3f, 0.03f, 0.3f), new Vector3(0.08f, 0.06f, 0.08f), new Color("a07a4a"));
        k.Box(new Vector3(0.32f, 0.03f, 0.28f), new Vector3(0.07f, 0.05f, 0.07f), new Color("8a6a42"), new Color("d8a83a"));
    }

    /// <summary>The courthouse: a stone hall behind a columned portico with a pediment bearing the gilded scales.</summary>
    private static void Courthouse(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.17f, 0, 0.28f));
        Walls(k, 3, 2, 0, true, v + 241, true);
        Walls(k, 3, 2, 1, true, v + 243, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.0f, Slate, new Color("c8c0b0"), 0.12f);
        k.Pop();
        // The portico: steps, four tall columns and a pediment.
        k.Box(new Vector3(0, 0, 1.3f), new Vector3(3.0f, 0.12f, 0.6f), new Color("b8b2a4"));
        k.Box(new Vector3(0, 0, 1.2f), new Vector3(3.0f, 0.24f, 0.4f), new Color("a8a296"));
        foreach (float x in new[] { -1.2f, -0.4f, 0.4f, 1.2f })
            k.Add("town/pillar-stone", new Transform3D(Basis.FromScale(new Vector3(1.3f, 1.75f, 1.3f)), new Vector3(x, 0.24f, 1.35f)), StoneTint);
        k.Box(new Vector3(0, 1.98f, 1.2f), new Vector3(3.0f, 0.12f, 0.5f), new Color("b8b2a4"));
        k.Tri(new Vector3(-1.5f, 2.1f, 1.46f), new Vector3(0, 2.6f, 1.46f), new Vector3(1.5f, 2.1f, 1.46f), new Color("c8c0b0"));
        k.Tri(new Vector3(1.5f, 2.1f, 0.94f), new Vector3(0, 2.6f, 0.94f), new Vector3(-1.5f, 2.1f, 0.94f), new Color("c8c0b0"));
        k.Quad(new Vector3(-1.56f, 2.08f, 1.5f), new Vector3(-1.56f, 2.08f, 0.94f), new Vector3(0, 2.62f, 0.94f), new Vector3(0, 2.62f, 1.5f), Slate);
        k.Quad(new Vector3(1.56f, 2.08f, 0.94f), new Vector3(1.56f, 2.08f, 1.5f), new Vector3(0, 2.62f, 1.5f), new Vector3(0, 2.62f, 0.94f), Slate.Darkened(0.1f));
        // The scales of justice in gold on the pediment.
        var gold = Palette.Gold;
        k.Box(new Vector3(0, 2.14f, 1.48f), new Vector3(0.04f, 0.36f, 0.03f), gold);
        k.Box(new Vector3(0, 2.44f, 1.48f), new Vector3(0.5f, 0.04f, 0.03f), gold);
        k.Frustum(new Vector3(-0.24f, 2.24f, 1.5f), 0.04f, 0.09f, 0.06f, 6, gold);
        k.Frustum(new Vector3(0.24f, 2.24f, 1.5f), 0.04f, 0.09f, 0.06f, 6, gold);
        foreach (float x in new[] { -1.55f, 1.55f })
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(x * 0.75f, 0.9f, 1.02f)), Palette.Kenney(new Color("3a4f8a")));
        k.Pop();
    }

    /// <summary>A library: a stone hall of tall windows beside a hexagonal reading tower.</summary>
    private static void Library(MeshKit k, int v)
    {
        k.Push(MeshKit.At(-0.1f, 0, -0.06f, 0, 0.26f));
        TallWalls(k, 3, 2, 2, true);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.0f, new Color("4f5f7a"), new Color("c8c0b0"), 0.12f);
        k.Pop();
        k.Pop();
        var castle = WallStone(new Color("4f5f7a"), 0.95f);
        k.Push(MeshKit.At(0.32f, 0, -0.2f, 0.3f, 0.3f));
        k.Add("castle/tower-hexagon-base", MeshKit.At(0, 0, 0), castle);
        for (int i = 0; i < 3; i++) k.Add("castle/tower-hexagon-mid", MeshKit.At(0, 1.31f + i * 0.46f, 0), castle);
        k.Add("castle/tower-hexagon-top", MeshKit.At(0, 2.69f, 0), castle);
        k.Add("castle/tower-hexagon-roof", MeshKit.At(0, 2.82f, 0), castle);
        k.Frustum(new Vector3(0, 3.62f, 0), 0.06f, 0, 0.4f, 6, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pop();
    }

    /// <summary>The barracks: a long stone block flying red banners over a training yard with dummies and a weapon rack.</summary>
    private static void Barracks(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.29f, 0, 0.3f));
        Walls(k, 3, 1, 0, true, v + 251, true);
        Walls(k, 3, 1, 1, true, v + 253, false);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 1, 0.7f, new Color("7e4430"), Palette.Stone, 0.12f);
        k.Pop();
        var red = Palette.Kenney(new Color("b8322a"));
        foreach (float x in new[] { -1, 1 })
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(x, 0.9f, 0.12f)), red);
        k.Pop();
        k.Box(new Vector3(0, 0.004f, 0.17f), new Vector3(0.96f, 0.014f, 0.6f), new Color("a08a62"));
        var wall = new Color("9a9488");
        k.Box(new Vector3(-0.46f, 0, 0.17f), new Vector3(0.05f, 0.09f, 0.6f), wall, wall.Lightened(0.1f));
        k.Box(new Vector3(0.46f, 0, 0.17f), new Vector3(0.05f, 0.09f, 0.6f), wall, wall.Lightened(0.1f));
        k.Box(new Vector3(-0.3f, 0, 0.46f), new Vector3(0.36f, 0.09f, 0.05f), wall, wall.Lightened(0.1f));
        k.Box(new Vector3(0.3f, 0, 0.46f), new Vector3(0.36f, 0.09f, 0.05f), wall, wall.Lightened(0.1f));
        var straw = new Color("d8b45a");
        for (int i = 0; i < 3; i++)
        {
            var p = new Vector3(-0.28f + i * 0.2f, 0, 0.12f + (i % 2) * 0.12f);
            k.Box(p, new Vector3(0.022f, 0.2f, 0.022f), Palette.Timber);
            k.Box(p + new Vector3(0, 0.14f, 0), new Vector3(0.14f, 0.022f, 0.022f), Palette.Timber);
            k.Blob(p + new Vector3(0, 0.12f, 0), new Vector3(0.04f, 0.06f, 0.035f), straw);
            k.Blob(p + new Vector3(0, 0.22f, 0), new Vector3(0.03f, 0.03f, 0.03f), straw.Darkened(0.1f));
        }
        // A weapon rack of spears.
        k.Box(new Vector3(0.32f, 0.16f, 0.22f), new Vector3(0.022f, 0.022f, 0.24f), Palette.DarkTimber);
        for (int i = 0; i < 4; i++)
            k.Box(new Vector3(0.32f, 0, 0.12f + i * 0.065f), new Vector3(0.012f, 0.26f, 0.012f), i % 2 == 0 ? Palette.Timber : new Color("8a8a90"));
        k.Add("castle/flag-wide", MeshKit.At(0.38f, 0.6f, -0.4f, Mathf.Pi / 2, 0.4f), red);
    }

    /// <summary>The cathedral: a tall nave and transept with an apse, twin spired towers at the west front and a spire at the crossing, all gilded at the top.</summary>
    private static void Cathedral(MeshKit k)
    {
        var stone = new Color("c8c0b0");
        var roof = new Color("5a6474");
        var gold = new Color(Palette.Gold, MeshKit.Flag.Magic);
        k.Push(MeshKit.At(0, 0, 0.015f, 0, 0.19f));
        // Nave: two sections wide, four long, five storeys tall.
        k.Push(MeshKit.At(0, 0, 0.3f));
        TallWalls(k, 2, 4, 5, true, false);
        GableZ(k, 5, 2, 4, 1.5f, roof, stone, 0.1f);
        k.Pop();
        // Transept across it.
        k.Push(MeshKit.At(0, 0, -0.7f));
        TallWalls(k, 4, 1, 4, false);
        k.Push(MeshKit.At(0, 4, 0));
        k.Gable(0, 4, 1, 1.0f, roof, stone, 0.1f);
        k.Pop();
        k.Pop();
        // The apse, a half-round at the east end.
        k.Frustum(new Vector3(0, 0, -1.7f), 0.95f, 0.95f, 4.0f, 10, new Color("b8b0a0"));
        k.Frustum(new Vector3(0, 4.0f, -1.7f), 1.0f, 0, 1.1f, 10, roof);
        // The crossing spire.
        k.Box(new Vector3(0, 5.6f, -0.7f), new Vector3(0.8f, 1.2f, 0.8f), new Color("b8b0a0"));
        k.Frustum(new Vector3(0, 6.8f, -0.7f), 0.52f, 0, 5.5f, 8, roof, null, Mathf.Pi / 8);
        k.Frustum(new Vector3(0, 12.2f, -0.7f), 0.1f, 0, 1.0f, 4, gold);
        // Twin west towers, with spires and gilded finials.
        var castle = WallStone(roof, 0.9f);
        foreach (float x in new[] { -1.45f, 1.45f })
        {
            TowerStack(k, x, 2.0f, 1.0f, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-mid-windows", "tower-square-mid", "tower-square-mid-windows", "tower-square-mid", "tower-square-roof"], castle);
            k.Frustum(new Vector3(x, 9.0f, 2.0f), 0.1f, 0, 1.0f, 4, gold);
        }
        // A great rose window over the west door.
        Pane(k, new Vector3(0, 3.2f, 2.32f), Vector3.Back, 0.45f, 0.45f, new Color("4a3a6a", MeshKit.Flag.Window));
        k.Pop();
    }

    /// <summary>The arena: an oval ring of arched stone walls around tiers of stands and a sand floor, pennants on the rim.</summary>
    private static void Arena(MeshKit k, int v)
    {
        var stone = new Color("b8ae98");
        k.Push(new Transform3D(Basis.FromScale(new Vector3(1, 1, 0.9f)), Vector3.Zero));
        k.Frustum(Vector3.Zero, 0.3f, 0.3f, 0.02f, 16, new Color("d8c08a"));
        const int Segs = 16;
        for (int s = 0; s < Segs; s++)
        {
            float a = (s + 0.5f) / Segs * Mathf.Tau;
            for (int t = 0; t < 3; t++)
            {
                float r = 0.3f + t * 0.055f;
                k.Push(new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2 - a, 0)), new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)));
                k.Box(Vector3.Zero, new Vector3(r * Mathf.Tau / Segs * 1.06f, 0.07f + t * 0.06f, 0.06f), stone.Darkened(0.08f - t * 0.03f), stone.Lightened(0.05f));
                k.Pop();
            }
            float ro = 0.46f;
            k.Push(new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2 - a, 0)), new Vector3(Mathf.Cos(a) * ro, 0, Mathf.Sin(a) * ro)));
            float w = ro * Mathf.Tau / Segs * 1.06f;
            k.Box(Vector3.Zero, new Vector3(w, 0.34f, 0.04f), new Color("a8a08c"), new Color("c0b8a4"));
            // Two tiers of dark arches on the outer face.
            for (int row = 0; row < 2; row++)
                Pane(k, new Vector3(0, 0.08f + row * 0.15f, 0.021f), Vector3.Back, w * 0.25f, 0.05f, new Color("3a3026"));
            k.Pop();
            if (s % 4 == 1)
                k.Add("castle/flag-pennant", MeshKit.At(Mathf.Cos(a) * ro, 0.34f, Mathf.Sin(a) * ro, 0, 0.3f), Palette.Kenney(Dyes[(s / 4 + v) % Dyes.Length]));
        }
        k.Pop();
    }

    /// <summary>The keep: a great square tower inside four corner towers and curtain walls, the lord's flag on top.</summary>
    private static void Keep(MeshKit k)
    {
        var castle = WallStone(new Color("4f5f8a"), 0.85f);
        TowerStack(k, 0, -0.04f, 0.5f, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-top"], castle);
        k.Add("castle/flag-wide", MeshKit.At(0, 1.66f, -0.04f, Mathf.Pi / 2, 0.55f), Palette.Kenney(new Color("b8322a")));
        float c = 0.38f;
        foreach (var (x, z) in new[] { (-c, -c), (c, -c), (-c, c), (c, c) })
            TowerStack(k, x, z, 0.22f, ["tower-square-base", "tower-square-mid", "tower-square-top-roof"], castle);
        Vector3[] corners = [new(-c, 0, -c), new(c, 0, -c), new(c, 0, c), new(-c, 0, c)];
        for (int i = 0; i < 4; i++)
        {
            var a = corners[i];
            var b = corners[(i + 1) % 4];
            if (i == 2)
            {
                // The gate in the front wall.
                var g = (a + b) / 2;
                CurtainRun(k, a, g + new Vector3(0.08f, 0, 0), 0.1f, 0.34f, 0.3f, castle);
                CurtainRun(k, g - new Vector3(0.08f, 0, 0), b, 0.1f, 0.34f, 0.3f, castle);
                k.Add("castle/gate", new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2, 0)) * Basis.FromScale(new Vector3(1, 0.3f, 0.26f)), g), castle);
                k.Box(g + new Vector3(0, 0.26f, 0), new Vector3(0.18f, 0.08f, 0.1f), new Color("8f8a80"));
            }
            else CurtainRun(k, a, b, 0.1f, 0.34f, 0.3f, castle);
        }
    }

    /// <summary>Castle-kit wall sections from a to b (on the ground), thick by high, about seg long each.</summary>
    private static void CurtainRun(MeshKit k, Vector3 a, Vector3 b, float thick, float high, float seg, Func<Color, Color> tint)
    {
        var dir = b - a;
        float len = dir.Length();
        int n = Math.Max(1, (int)Mathf.Round(len / seg));
        float yaw = Mathf.Atan2(dir.X, dir.Z);
        var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromScale(new Vector3(thick / 0.92f, high / 1.31f, len / n));
        for (int i = 0; i < n; i++)
            k.Add("castle/wall", new Transform3D(basis, a + dir * ((i + 0.5f) / n)), tint);
    }

    /// <summary>A guildhall: a broad hall, stone below and timber above, gold banners and a cupola on the ridge.</summary>
    private static void Guildhall(MeshKit k, int v)
    {
        k.Push(MeshKit.At(0, 0, -0.05f, 0, Fit(3, 2, 0.9f)));
        Walls(k, 3, 2, 0, true, v + 261, true);
        Walls(k, 3, 2, 1, false, v + 263, false);
        k.Box(new Vector3(0, 0.96f, 0), new Vector3(3.1f, 0.08f, 2.1f), Palette.DarkTimber);
        k.Push(MeshKit.At(0, 2, 0));
        k.Gable(0, 3, 2, 1.3f, new Color("3a4f6a"), Palette.Plaster, 0.12f);
        k.Pop();
        var gold = Palette.Kenney(Palette.Gold);
        foreach (float x in new[] { -1, 1 })
            k.Add("town/banner-green", new Transform3D(Basis.FromEuler(new Vector3(0, -Mathf.Pi / 2, 0)), new Vector3(x, 0.9f, 0.62f)), gold);
        k.Box(new Vector3(0, 3.0f, 0), new Vector3(0.4f, 0.4f, 0.4f), Palette.Plaster);
        k.Push(MeshKit.At(0, 3.4f, 0));
        k.Pyramid(0, 0.4f, 0.4f, 0.4f, new Color("5a8a7a"), 0.06f);
        k.Pop();
        k.Frustum(new Vector3(0, 3.8f, 0), 0.05f, 0, 0.3f, 4, new Color(Palette.Gold, MeshKit.Flag.Magic));
        Chimney(k, 1.2f, 2, -0.4f, true);
        k.Pop();
    }

    // ---------- Ground ----------

    private void Lots(Town town)
    {
        string key = string.Join(',', town.Lots.Select(l => (int)l.Zone));
        if (key == _groundKey) return;
        _groundKey = key;
        var k = new MeshKit();
        foreach (var l in town.Lots)
        {
            if (l.Zone == Zone.None) continue;
            float x = Geo.Margin + l.X + 0.5f, z = Geo.Margin + l.Y + 0.5f;
            var c = l.Zone switch
            {
                Zone.Civic => new Color("b4aa94"),
                Zone.Commercial => new Color("a28c62"),
                Zone.Farmland => new Color("7a6040"),
                Zone.Industry => new Color("8a7a66"),
                _ => new Color("8f8a5c"),
            };
            float s = l.Zone == Zone.Farmland ? 0.98f : 1.02f;
            k.Box(new Vector3(x, Y - 0.05f, z), new Vector3(s, 0.065f, s), c.Darkened(Geo.Hash(l.X * 31 + l.Y) * 0.08f));
            if (l.Zone == Zone.Civic)
                for (int i = 0; i < 4; i++)
                    k.Box(new Vector3(x - 0.375f + i * 0.25f, Y + 0.012f, z), new Vector3(0.22f, 0.01f, 0.95f), new Color("c4b8a0"));
        }
        k.Build(_ground.Mesh as ArrayMesh ?? (ArrayMesh)(_ground.Mesh = new ArrayMesh()));
    }

    /// <summary>Castle-kit stone in a grey darker than the houses' so the walls read; roofs and flags take the accent.</summary>
    private static Func<Color, Color> WallStone(Color accent, float shade)
    {
        var roof = Palette.Kenney(accent);
        return c =>
        {
            c.ToHsv(out float h, out float s, out float v);
            if (s > 0.3f && h > 0.38f && h < 0.72f) return roof(c);
            if (h < 0.12f && s < 0.6f) return Color.FromHsv(0.08f, 0.1f, Mathf.Min(1, v * shade));
            return roof(c);
        };
    }

    private static float Progress(Town town, PalisadeState state, int? ready, int days) =>
        state == PalisadeState.Built ? 1
        : state == PalisadeState.Building && ready is int r ? Mathf.Clamp(1 - (r - town.Tick) / (float)(days * SimTime.HoursPerDay), 0, 1) : 0;

    /// <summary>Lots a stone ring encloses: ring 0 is the old town, ring 1 adds the wards inside the city wall (see City.Ring).</summary>
    public static Rect2 RingBounds(int ring)
    {
        int lo = Town.Grid / 2 - City.Band * (ring + 1), n = 2 * City.Band * (ring + 1);
        return new Rect2(Geo.Margin + lo, Geo.Margin + lo, n, n);
    }

    private void Palisade(Town town)
    {
        var d = town.Defenses;
        var e = town.Content.Enemy;
        var bounds = WallBounds(town);
        float done = Progress(town, d.Palisade, d.PalisadeReadyTick, e.Palisade.Days);
        float stone = Progress(town, d.StoneWall, d.StoneWallReadyTick, e.StoneWall.Days);
        float city = Progress(town, d.CityWall, d.CityWallReadyTick, e.CityWall.Days);
        // Once the stone walls stand, the timber palisade and its towers come down.
        bool timber = d.StoneWall != PalisadeState.Built;
        string key = $"{d.Palisade}|{Math.Round(done, 2)}|{d.Towers}|{bounds}|{timber}|{Math.Round(stone, 2)}|{Math.Round(city, 2)}";
        if (key == _wallKey) return;
        _wallKey = key;
        var k = new MeshKit();
        if (timber && bounds is Rect2 b && d.Palisade != PalisadeState.None)
        {
            // Sharpened stakes along the perimeter, raised clockwise so a rising wall reads as progress.
            float w = b.Size.X, h = b.Size.Y, perim = 2 * (w + h);
            int stakes = (int)(perim / 0.13f), shown = (int)(stakes * Mathf.Clamp(done, 0, 1));
            for (int i = 0; i < shown; i++)
            {
                var (x, y) = Around(b, (float)i / stakes * perim);
                float sh = 0.32f + Geo.Hash(i) * 0.06f;
                var col = i % 2 == 0 ? Palette.Timber : new Color("8a6239");
                k.Frustum(new Vector3(x, Y, y), 0.055f, 0.055f, sh, 5, col, col, i);
                k.Frustum(new Vector3(x, Y + sh, y), 0.055f, 0, 0.1f, 5, col, null, i);
            }
        }
        if (timber && bounds is Rect2 bb)
        {
            Vector2[] corners = [bb.Position, new(bb.End.X, bb.Position.Y), bb.End, new(bb.Position.X, bb.End.Y)];
            var castle = Palette.Kenney(new Color("9b3d2a"));
            for (int i = 0; i < Math.Min(4, d.Towers); i++)
            {
                var c = corners[i];
                k.Add("castle/tower-square-base", MeshKit.At(c.X, Y, c.Y, 0, 0.42f), castle);
                k.Add("castle/tower-square-mid", MeshKit.At(c.X, Y + 0.42f, c.Y, 0, 0.42f), castle);
                k.Add("castle/tower-square-top-roof", MeshKit.At(c.X, Y + 0.84f, c.Y, 0, 0.42f), castle);
            }
        }
        if (stone > 0) StoneRing(k, RingBounds(0), stone, 0.24f, 0.8f, 0.5f, 1, 2, WallStone(Slate, 0.82f));
        if (city > 0) StoneRing(k, RingBounds(1), city, 0.3f, 1.05f, 0.62f, 2, 4, WallStone(new Color("9b3d2a"), 0.72f));
        k.Build(_walls.Mesh as ArrayMesh ?? (ArrayMesh)(_walls.Mesh = new ArrayMesh()));
    }

    /// <summary>The point t along a rectangle's perimeter, clockwise from its north-west corner (north is -y).</summary>
    private static (float X, float Y) Around(Rect2 b, float t)
    {
        float w = b.Size.X, h = b.Size.Y;
        if (t < w) return (b.Position.X + t, b.Position.Y);
        if ((t -= w) < h) return (b.End.X, b.Position.Y + t);
        if ((t -= h) < w) return (b.End.X - t, b.End.Y);
        return (b.Position.X, b.End.Y - (t - w));
    }

    /// <summary>
    /// A stone curtain wall along the edges of the lots in b, towers at the corners and along the sides,
    /// and a gatehouse where the south road leaves. Raised clockwise as the work goes on.
    /// </summary>
    private static void StoneRing(MeshKit k, Rect2 b, float done, float thick, float high, float tower, int mids, int perSide, Func<Color, Color> tint)
    {
        float w = b.Size.X, h = b.Size.Y, perim = 2 * (w + h), reach = done * perim + 0.01f;
        float gateX = b.Position.X + w / 2, gap = tower * 0.75f;
        // Wall sections, edge by edge, leaving the gateway open.
        (Vector2 A, Vector2 B, float T0)[] edges =
        [
            (b.Position, new(b.End.X, b.Position.Y), 0), (new(b.End.X, b.Position.Y), b.End, w),
            (b.End, new(b.Position.X, b.End.Y), w + h), (new(b.Position.X, b.End.Y), b.Position, 2 * w + h),
        ];
        const float Seg = 0.5f;
        for (int e = 0; e < 4; e++)
        {
            var (a, bEnd, t0) = edges[e];
            float len = a.DistanceTo(bEnd);
            int n = (int)Mathf.Round(len / Seg);
            var dir = (bEnd - a) / n;
            float yaw = Mathf.Atan2(dir.X, dir.Y);
            var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromScale(new Vector3(thick / 0.92f, high / 1.31f, len / n));
            for (int i = 0; i < n; i++)
            {
                if (t0 + (i + 0.5f) * len / n > reach) break;
                var p = a + dir * (i + 0.5f);
                if (e == 2 && Mathf.Abs(p.X - gateX) < gap) continue;
                k.Add("castle/wall", new Transform3D(basis, new Vector3(p.X, Y, p.Y)), tint);
            }
        }
        // Towers: the four corners and perSide along each edge, the south ones shifted clear of the gate.
        string[] stack = [.. Enumerable.Repeat("tower-square-base", 1), .. Enumerable.Repeat("tower-square-mid", mids), "tower-square-top-roof"];
        for (int e = 0; e < 4; e++)
        {
            float len = e % 2 == 0 ? w : h;
            for (int j = 0; j <= perSide; j++)
            {
                float t = edges[e].T0 + len * j / (perSide + 1);
                if (e == 2 && j > 0)
                {
                    var (gx, _) = Around(b, t);
                    if (Mathf.Abs(gx - gateX) < 1.2f) continue;
                }
                if (t > reach) continue;
                var (x, y) = Around(b, t);
                k.Push(MeshKit.At(x, Y, y));
                TowerStack(k, 0, 0, tower, stack, tint);
                k.Pop();
            }
        }
        // The gatehouse on the south road: two taller towers, a portcullis and the arch over it.
        float tg = w + h + (b.End.X - gateX);
        if (tg > reach) return;
        string[] gateStack = [.. stack[..^1], "tower-square-mid-windows", "tower-square-top-roof"];
        foreach (float s in new[] { -1f, 1f })
        {
            k.Push(MeshKit.At(gateX + s * (gap + tower * 0.35f), Y, b.End.Y));
            TowerStack(k, 0, 0, tower * 0.9f, gateStack, tint);
            k.Pop();
            k.Add("castle/flag-pennant", MeshKit.At(gateX + s * (gap + tower * 0.35f), Y + tower * 0.9f * (gateStack.Length + 0.0f), b.End.Y, 0, tower * 0.7f), Palette.Kenney(new Color("b8322a")));
        }
        k.Add("castle/gate", new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2, 0)) * Basis.FromScale(new Vector3(1.4f, high * 0.75f / 0.91f, gap * 2 / 0.66f)), new Vector3(gateX, Y, b.End.Y)), tint);
        k.Box(new Vector3(gateX, Y + high * 0.75f, b.End.Y), new Vector3(gap * 2 + 0.05f, high * 0.35f, thick * 1.1f), new Color("9a9488"), new Color("8a847a"));
    }

    /// <summary>Bounds of the walled town: every zoned lot except the farms, which lie outside the wall.</summary>
    public static Rect2? WallBounds(Town town)
    {
        float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
        foreach (var l in town.Lots)
        {
            if (l.Zone is Zone.None or Zone.Farmland) continue;
            x0 = Mathf.Min(x0, l.X);
            y0 = Mathf.Min(y0, l.Y);
            x1 = Mathf.Max(x1, l.X + 1);
            y1 = Mathf.Max(y1, l.Y + 1);
        }
        if (x0 == float.MaxValue) return null;
        return new Rect2(Geo.Margin + x0 - 0.3f, Geo.Margin + y0 - 0.3f, x1 - x0 + 0.6f, y1 - y0 + 0.6f);
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
