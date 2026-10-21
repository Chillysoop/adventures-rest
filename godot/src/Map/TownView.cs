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

    private ImageTexture? _zoneTex;
    private int _zoneVersion = -1;
    private bool _planning;
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

    /// <summary>The ground under a point of the map (world tiles).</summary>
    private float Ground(Vector2 p) => map.Gen.HeightAt(p.X, p.Y);

    /// <summary>
    /// A lot's floor: level with the highest of its corners, so on a slope the low side stands proud
    /// of the ground and is propped up (<see cref="Footing"/>).
    /// </summary>
    private float Floor(Lot l)
    {
        float h = float.MinValue;
        foreach (var c in Geo.LotCorners(l, 0)) h = Mathf.Max(h, Ground(c));
        return Mathf.Max(h, Ground(Geo.LotPos(l)));
    }

    // ---------- Buildings ----------

    private static string MeshKey(Building b)
    {
        // A grand work rises in plain sight: its own shape, taller with every stage, in scaffold.
        if (b.IsSite && b.Def.IsGrand) return $"grand:{Math.Min(7, (int)(b.BuildProgress / b.Def.BuildHours * 8))}:{b.Def.Id}";
        // Any other site comes together in six steps: staked out, footings, then its own walls rising inside the scaffold,
        // beside a pile of the timber and stone carried onto it and not yet built in.
        if (b.IsSite)
        {
            double done = b.BuildProgress / b.Def.BuildHours;
            double left = b.Hauled - Math.Max(0, (done - 0.1) / 0.9);
            int pile = b.Hauled >= 1 && done >= 0.98 ? 0 : left > 0.3 ? 2 : left > 0.04 ? 1 : 0;
            return $"rise:{Math.Min(5, (int)(done * 6))}:{pile}{(b.Def.Stone > b.Def.Timber ? "s" : "t")}:{b.Def.Id}";
        }
        return $"{b.Def.Id}:{b.Id % 4}";
    }

    private void Buildings(Town town)
    {
        var groups = new SortedDictionary<string, List<(Vector3 P, float Yaw, float Scale)>>(StringComparer.Ordinal);
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            var lot = town.Lot(b.LotId);
            var c = Geo.LotPos(lot);
            var p = new Vector3(c.X, Floor(lot), c.Y);
            string key = MeshKey(b);
            // The mast stands empty while the airship is off (or flying out or in: see Sky).
            if (key.StartsWith("sky_dock:", StringComparison.Ordinal) && !SkyMoored(town)) key = "sky_dock_away:" + key[9..];
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
                signs.Add((new Vector3(at.X, Ground(at), at.Y), yaw + 0.3f, 1));
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
        if (parts[0] == "grand") Rising(kit, parts[2], v);
        else if (parts[0] == "rise") Site(kit, parts[3], v, parts[2][0] - '0', parts[2][1] == 's');
        else Draw(kit, parts[0], v);
        return _meshes[key] = kit.Build();
    }

    private static void Draw(MeshKit kit, string id, int v)
    {
        switch (id)
        {
            case "sign": kit.Add("nature/sign", MeshKit.At(0, 0, 0, 0, 0.7f)); break;
            case "tavern": Tavern(kit, v); break;
            case "town_hall": Hall(kit, v); break;
            case "farm": Farm(kit, v); break;
            case "root_farm": RootFarm(kit, v); break;
            case "hop_yard": HopYard(kit, v); break;
            case "orchard": Orchard(kit, v); break;
            case "lumber_camp": Lumber(kit, v); break;
            case "forge": Forge(kit, v); break;
            case "brewery": Brewery(kit, v); break;
            case "scrying_pool": Pool(kit); break;
            case "sleepless_spire": Spire(kit); break;
            case "shrine": Shrine(kit); break;
            case "temple": Temple(kit, v); break;
            case "general_store": Store(kit, v); break;
            case "granary": Granary(kit, v); break;
            case "builders_yard": BuildersYard(kit, v); break;
            case "watch_house": WatchHouse(kit, v); break;
            case "watchtower": Watchtower(kit); break;
            case "timber_house" or "townhouse" or "manor" or "tenement" or "tall_house": Crafted(kit, id, v); break;
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
            // A company's bastion: its own keep under its banner, and facilities after the town's own kinds.
            case "bastion_keep":
                // Smaller than the city's keep: a company's hall, not a lord's castle.
                kit.Push(MeshKit.At(0, 0, 0, 0, 0.72f));
                Keep(kit, Dyes[v % Dyes.Length]);
                kit.Pop();
                break;
            case "bastion_garden": HerbGarden(kit, v); break;
            case "bastion_yard": Barracks(kit, v); break;
            case "bastion_smithy": Forge(kit, v); break;
            case "bastion_maproom": Library(kit, v); break;
            case "bastion_sanctum": Temple(kit, v); break;
            case "bastion_counting": Guildhall(kit, v); break;
            case "bastion_proving": Arena(kit, v); break;
            case "bastion_banners": Courthouse(kit, v); break;
            case "bastion_vault": WatchHouse(kit, v); break;
            case "clock_tower": ClockTower(kit); break;
            case "aqueduct": Aqueduct(kit); break;
            case "great_gatehouse": Gatehouse(kit); break;
            case "mage_tower": MageTower(kit); break;
            case "royal_gardens": Gardens(kit); break;
            case "colossus": Colossus(kit); break;
            case "great_cathedral": GreatCathedral(kit); break;
            case "royal_palace": Palace(kit); break;
            case "sky_citadel": Citadel(kit); break;
            case "adventurers_hall": AdventurersHall(kit, v); break;
            case "druid_grove": DruidGrove(kit, v); break;
            case "grand_bazaar": Bazaar(kit, v); break;
            case "chapterhouse": Chapterhouse(kit, v); break;
            case "alchemists_quarter": AlchemistsQuarter(kit, v); break;
            case "artificers_foundry": ArtificersFoundry(kit, v); break;
            case "undercity_gate": UndercityGate(kit, v); break;
            case "griffon_roost": GriffonRoost(kit, v); break;
            case "sky_dock": SkyDock(kit, v); break;
            case "sky_dock_away": SkyDock(kit, v, false); break;
            case "fey_market": FeyMarket(kit, v); break;
            case "monster_sanctuary": MonsterSanctuary(kit, v); break;
            case "auction_house": AuctionHouse(kit, v); break;
            case "observatory": Observatory(kit, v); break;
            case "dungeon_entrance": DungeonEntrance(kit, v); break;
            case "embassy_quarter": EmbassyQuarter(kit, v); break;
            case "planar_gate": PlanarGate(kit, v); break;
            case "hall_of_heroes": HallOfHeroes(kit, v); break;
            case "dragon_eyrie": DragonEyrie(kit, v); break;
            case "temple_nave": TempleNave(kit, v); break;
            case "temple_bell_tower": TempleBellTower(kit); break;
            case "temple_almshouse": Almshouse(kit, v); break;
            case "temple_crypt": Crypt(kit); break;
            case "temple_cloister": Cloister(kit); break;
            case "temple_spire": Spire(kit, Slate); break;
            case "keep_curtain_wall": CurtainWall(kit); break;
            case "keep_chapel": CastleChapel(kit, v); break;
            case "keep_gatehouse": CastleGatehouse(kit); break;
            case "keep_great_hall": GreatHall(kit, v); break;
            case "keep_barracks_wing": Barracks(kit, v); break;
            case "keep_high_tower": HighTower(kit); break;
            case "hall_bell_tower": HallBellTower(kit); break;
            case "hall_council_chamber": Courthouse(kit, v); break;
            case "hall_counting_house": Guildhall(kit, v); break;
            case "hall_archive": Library(kit, v); break;
            case "hall_banner_court": BannerCourt(kit, v); break;
            default: House(kit, v); break;
        }
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

    /// <summary>How far standing crops bend in the wind, per unit of their height (MeshKit.Sway).</summary>
    private const float CropSway = 0.7f;

    /// <summary>Kenney's crops come untextured (white wheat, teal corn): ripe gold and leaf green, keeping their shading.</summary>
    private static readonly Func<Color, Color> Wheat = c => new Color("d4ad4a") * (0.8f + 0.2f * c.Luminance);
    private static readonly Func<Color, Color> Corn = c => new Color("6c9a36") * (0.8f + 0.2f * c.Luminance);

    private static void Chimney(MeshKit k, float x, float y, float z, bool smoke = false)
    {
        k.Box(new Vector3(x, y, z), new Vector3(0.22f, 0.9f, 0.22f), new Color("8a8070"), new Color("3a3430"));
        if (!smoke) return;
        k.Box(new Vector3(x, y + 0.9f, z), new Vector3(0.12f, 0.02f, 0.12f), new Color("ff8a3a", MeshKit.Flag.Ember));
        Smoke(k, new Vector3(x, y + 0.98f, z), 0.1f, x + z);
    }

    private static readonly Color SmokeGrey = new("cfcac2", 0.9f);

    /// <summary>Three puffs of smoke on a loop out of a chimney top (MeshKit.Puff), each a third of the way behind the last.</summary>
    private static void Smoke(MeshKit k, Vector3 top, float r, float seed)
    {
        float start = Geo.Hash((int)(seed * 977));
        for (int i = 0; i < 3; i++) k.Puff(top, r * (1 - i * 0.12f), start + i / 3f, SmokeGrey.Darkened(i * 0.05f));
    }

    /// <summary>Where the crafted homes' chimneys stand (x, z in model space; tools/blender_houses.py), topped by smoke at their highest point.</summary>
    private static readonly Dictionary<string, Vector2[]> CraftedChimneys = new()
    {
        ["house"] = [new(0.23f, -0.1f)],
        ["timber_house"] = [new(-0.27f, -0.12f)],
        ["townhouse"] = [new(-0.121f, -0.21f), new(0.279f, -0.21f)],
        ["tall_house"] = [new(-0.236f, -0.258f), new(0.064f, -0.258f), new(0.364f, -0.258f)],
        ["tenement"] = [new(-0.26f, -0.15f), new(0.26f, -0.2f)],
        ["manor"] = [new(-0.36f, -0.26f), new(0.36f, -0.26f)],
    };

    /// <summary>The top of a crafted chimney: the highest point of the model within a chimney's width of it.</summary>
    private static Vector3 ChimneyTop(Model m, Vector2 at)
    {
        float top = 0;
        foreach (var p in m.Verts)
            if (new Vector2(p.X, p.Z).DistanceSquaredTo(at) < 0.0049f) top = Mathf.Max(top, p.Y);
        return new Vector3(at.X, top + 0.03f, at.Y);
    }

    private static Color RoofOf(int v) => Palette.Roofs[v % Palette.Roofs.Length];

    /// <summary>
    /// A home made in Blender (tools/blender_houses.py, models/crafted.bin): its roof painted in the house's roof colour, and every
    /// other house turned end for end so a street of them doesn't repeat.
    /// </summary>
    private static void Crafted(MeshKit k, string id, int v)
    {
        var roof = id == "manor" ? (v % 2 == 0 ? Slate : new Color("6e4a3a")) : RoofOf(v + (id == "house" ? 0 : 1));
        var model = Models.Get("cc/" + id);
        k.Add(model, MeshKit.At(0, 0, 0, 0, 1), RoofPaint(roof));
        if (CraftedChimneys.TryGetValue(id, out var chimneys))
            for (int i = 0; i < chimneys.Length; i++) Smoke(k, ChimneyTop(model, chimneys[i]), 0.055f, v + i * 0.37f);
    }

    /// <summary>Repaints a crafted model's roof faces, baked as (1, 0, shade), in a roof colour at that shade.</summary>
    public static Func<Color, Color> RoofPaint(Color roof) => c =>
        c.R > 0.99f && c.G < 0.01f ? new Color(roof.R * (0.4f + 0.62f * c.B), roof.G * (0.4f + 0.62f * c.B), roof.B * (0.4f + 0.62f * c.B)) : c;

    private static void House(MeshKit k, int v) => Crafted(k, "house", v);

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

    private static void BuildersYard(MeshKit k, int v)
    {
        // An open-fronted workshop, a stack of sawn timber, dressed stone and a hoisting frame.
        k.Box(new Vector3(0, 0, 0), new Vector3(0.96f, 0.03f, 0.96f), new Color("7d6a50"), new Color("948063"));
        k.Push(MeshKit.At(-0.22f, 0.03f, -0.22f, 0, Fit(2, 1, 0.5f)));
        Walls(k, 2, 1, 0, false, v + 97, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.6f, new Color("7a5232"), Palette.Plaster, 0.1f);
        k.Pop();
        k.Pop();
        k.Add("nature/log_stackLarge", MeshKit.At(0.3f, 0.03f, -0.25f, 0.1f, 0.7f));
        for (int i = 0; i < 3; i++)
            k.Box(new Vector3(-0.3f + i * 0.16f, 0.03f, 0.28f), new Vector3(0.07f, 0.07f, 0.07f), Palette.Stone);
        k.Box(new Vector3(-0.22f, 0.1f, 0.28f), new Vector3(0.07f, 0.07f, 0.07f), Palette.Stone);
        // The hoist: two posts and a beam with a hanging block.
        var wood = new Color("6a4a2e");
        k.Box(new Vector3(0.22f, 0.03f, 0.3f), new Vector3(0.025f, 0.42f, 0.025f), wood);
        k.Box(new Vector3(0.42f, 0.03f, 0.3f), new Vector3(0.025f, 0.42f, 0.025f), wood);
        k.Box(new Vector3(0.32f, 0.44f, 0.3f), new Vector3(0.23f, 0.02f, 0.03f), wood);
        k.Box(new Vector3(0.32f, 0.22f, 0.3f), new Vector3(0.05f, 0.05f, 0.05f), Palette.Stone);
        k.Add("town/cart", MeshKit.At(0.05f, 0.03f, 0.05f, 0.8f, 0.25f));
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

    private static void Watchtower(MeshKit k)
    {
        // A tall timber lookout on four splayed posts: braced, with a railed platform, a steep shingle roof and a beacon.
        var post = new Color("6b4a2e");
        var plank = new Color("8c6a44");
        var roof = new Color("5a3a2a");
        const float top = 1.55f, foot = 0.34f, head = 0.22f;
        foreach (var (sx, sz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var a = new Vector3(sx * foot, 0, sz * foot);
            var b = new Vector3(sx * head, top, sz * head);
            var mid = (a + b) / 2;
            var dir = (b - a).Normalized();
            k.Push(new Transform3D(new Basis(new Quaternion(Vector3.Up, dir)), mid));
            k.Box(new Vector3(0, -(b - a).Length() / 2, 0), new Vector3(0.07f, (b - a).Length(), 0.07f), post);
            k.Pop();
        }
        // Cross braces on every face, at two heights.
        foreach (float y in new[] { 0.45f, 1.05f })
        {
            float w = Mathf.Lerp(foot, head, y / top);
            k.Box(new Vector3(0, y, -w), new Vector3(w * 2, 0.05f, 0.04f), post);
            k.Box(new Vector3(0, y, w), new Vector3(w * 2, 0.05f, 0.04f), post);
            k.Box(new Vector3(-w, y, 0), new Vector3(0.04f, 0.05f, w * 2), post);
            k.Box(new Vector3(w, y, 0), new Vector3(0.04f, 0.05f, w * 2), post);
        }
        // The platform, wider than the posts, its railing and corner uprights.
        k.Box(new Vector3(0, top, 0), new Vector3(0.66f, 0.06f, 0.66f), plank);
        foreach (var (x, z, w, d) in new[] { (0f, -0.31f, 0.66f, 0.04f), (0f, 0.31f, 0.66f, 0.04f), (-0.31f, 0f, 0.04f, 0.66f), (0.31f, 0f, 0.04f, 0.66f) })
            k.Box(new Vector3(x, top + 0.2f, z), new Vector3(w, 0.05f, d), plank);
        foreach (var (x, z) in new[] { (-0.3f, -0.3f), (0.3f, -0.3f), (-0.3f, 0.3f), (0.3f, 0.3f) })
            k.Box(new Vector3(x, top, z), new Vector3(0.05f, 0.5f, 0.05f), post);
        k.Push(MeshKit.At(0, top + 0.5f, 0));
        k.Pyramid(0, 0.8f, 0.8f, 0.42f, roof, 0.05f);
        k.Pop();
        // The beacon brazier, lit, and a ladder up the front.
        k.Box(new Vector3(0, top + 0.06f, 0), new Vector3(0.16f, 0.1f, 0.16f), new Color("4a443e"), new Color("ff8a3a", MeshKit.Flag.Ember));
        k.Box(new Vector3(-0.08f, 0, foot + 0.05f), new Vector3(0.03f, top, 0.03f), plank);
        k.Box(new Vector3(0.08f, 0, foot + 0.05f), new Vector3(0.03f, top, 0.03f), plank);
        for (float y = 0.15f; y < top; y += 0.18f) k.Box(new Vector3(0, y, foot + 0.05f), new Vector3(0.16f, 0.025f, 0.03f), plank);
        k.Add("castle/flag-pennant", MeshKit.At(0, top + 0.86f, 0, 0, 0.3f), Palette.Kenney(new Color("b8322a")));
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
        k.Sway = CropSway;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 2; c++)
                k.Add(v % 2 == 0 ? "nature/crops_wheatStageB" : "nature/crops_cornStageC", MeshKit.At(-0.3f + c * 0.3f, 0, -0.32f + r * 0.32f, 0, v % 2 == 0 ? 0.55f : 0.35f), v % 2 == 0 ? Wheat : Corn);
        k.Sway = 0;
        k.Push(MeshKit.At(0.3f, 0, 0.12f, Mathf.Pi / 2, K * 0.75f));
        Walls(k, 2, 1, 0, false, v + 41, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.8f, new Color("9b3d2a"), new Color("7a5232"), 0.1f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 4; i++) k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, i * Mathf.Pi / 2, 1));
    }

    private static void RootFarm(MeshKit k, int v)
    {
        // Ridged rows of turnips and onions, a pumpkin patch and a low clamp barn.
        k.Box(new Vector3(0, 0.005f, 0), new Vector3(0.96f, 0.02f, 0.96f), new Color("6a4a2c"));
        for (int r = 0; r < 4; r++) k.Add("nature/crops_dirtDoubleRow", MeshKit.At(-0.36f + r * 0.2f, 0, -0.08f, 0, 0.5f));
        var leaf = new Color("5f8a34");
        for (int r = 0; r < 4; r++)
            for (int i = 0; i < 5; i++)
                k.Blob(new Vector3(-0.36f + r * 0.2f + (i % 2 == 0 ? -0.03f : 0.03f), 0.04f, -0.42f + i * 0.17f), new Vector3(0.035f, 0.03f, 0.035f), leaf.Darkened(Geo.Hash(v * 13 + r * 5 + i) * 0.2f), 0.3f, r * 5 + i + v);
        k.Add("nature/crop_pumpkin", MeshKit.At(0.36f, 0, 0.36f, v, 0.45f));
        k.Add("nature/crop_pumpkin", MeshKit.At(0.24f, 0, 0.4f, v + 2, 0.38f));
        k.Push(MeshKit.At(0.32f, 0, -0.2f, Mathf.Pi / 2, K * 0.62f));
        Walls(k, 2, 1, 0, false, v + 43, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 2, 1, 0.7f, new Color("6a5a3a"), new Color("7a5232"), 0.1f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 4; i++) k.Add("nature/fence_simple", MeshKit.At(0, 0, 0, i * Mathf.Pi / 2, 1));
    }

    private static void HopYard(MeshKit k, int v)
    {
        // Tall hop poles strung with bines over a strip of barley, and an oast house with its cowled kiln.
        k.Box(new Vector3(0, 0.005f, 0), new Vector3(0.96f, 0.02f, 0.96f), new Color("8a7040"));
        k.Sway = CropSway;
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 2; c++)
                k.Add("nature/crops_wheatStageB", MeshKit.At(-0.32f + c * 0.26f, 0, 0.18f + r * 0.24f, 0, 0.45f), Wheat);
        k.Sway = 0;
        var bine = new Color("5a8a2e");
        for (int r = 0; r < 3; r++)
        {
            float x = -0.38f + r * 0.18f;
            k.Box(new Vector3(x, 0.36f, -0.2f), new Vector3(0.006f, 0.006f, 0.42f), new Color("8a8478"));
            for (int i = 0; i < 3; i++)
            {
                float z = -0.4f + i * 0.2f;
                k.Box(new Vector3(x, 0, z), new Vector3(0.014f, 0.38f, 0.014f), Palette.Timber);
                k.Blob(new Vector3(x, 0.22f, z), new Vector3(0.035f, 0.13f, 0.035f), bine.Darkened(Geo.Hash(v * 7 + r * 3 + i) * 0.2f), 0.3f, r * 3 + i + v, bine.Darkened(0.3f));
            }
        }
        k.Push(MeshKit.At(0.3f, 0, -0.18f, 0, K * 0.6f));
        Walls(k, 1, 2, 0, false, v + 47, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 1, 2, 0.7f, new Color("8a4a2e"), Palette.Plaster, 0.1f);
        k.Pop();
        k.Pop();
        // The oast's round kiln with its white cowl.
        k.Frustum(new Vector3(0.3f, 0, 0.22f), 0.11f, 0.11f, 0.32f, 10, new Color("9a5a3a"), new Color("8a4a2e"));
        k.Frustum(new Vector3(0.3f, 0.32f, 0.22f), 0.11f, 0.02f, 0.16f, 10, new Color("f2eee2"), new Color("d8d2c2"));
    }

    private static void Orchard(MeshKit k, int v)
    {
        // Rows of fruit trees on grass, a few apples in the boughs, and a cider shed with its barrels.
        k.Box(new Vector3(0, 0.004f, 0), new Vector3(0.98f, 0.016f, 0.98f), new Color("6f8a42"));
        var fruit = v % 2 == 0 ? new Color("c83a2a") : new Color("d8c040");
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                if (r == 0 && c == 2) continue;
                float x = -0.34f + c * 0.3f + (r % 2) * 0.08f, z = -0.34f + r * 0.3f;
                k.Add(Geo.Hash(v * 11 + r * 3 + c) < 0.5f ? "nature/tree_oak" : "nature/tree_fat", MeshKit.At(x, 0, z, Geo.Hash(v + r * 3 + c) * Mathf.Tau, 0.3f), WorldView.Foliage);
                k.Blob(new Vector3(x + 0.04f, 0.16f, z + 0.05f), new Vector3(0.02f, 0.02f, 0.02f), fruit);
            }
        k.Push(MeshKit.At(0.34f, 0, -0.3f, 0, K * 0.5f));
        Walls(k, 1, 1, 0, false, v + 49, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Gable(0, 1, 1, 0.6f, new Color("7a5a3a"), Palette.Timber, 0.12f);
        k.Pop();
        k.Pop();
        for (int i = 0; i < 2; i++) k.Frustum(new Vector3(0.2f + i * 0.09f, 0, -0.42f), 0.04f, 0.045f, 0.09f, 8, new Color("8a5a2b"), new Color("6a4422"));
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

    /// <summary>
    /// A building going up (<see cref="MeshKey"/>): staked out with string (stage 0), stone footings and corner posts (1), then
    /// its own shape rising inside a timber scaffold from stage 2 on, with the materials carried in but not yet used piled
    /// at the front corner (none, some, plenty; stone or timber).
    /// </summary>
    private static void Site(MeshKit k, string id, int stage, int pile, bool stone)
    {
        k.Box(Vector3.Zero, new Vector3(0.9f, 0.015f, 0.9f), Palette.Dirt.Darkened(0.1f));
        const float hx = 0.43f, hz = 0.43f;
        (float X, float Z)[] corners = [(-hx, -hz), (hx, -hz), (hx, hz), (-hx, hz)];
        if (stage == 0)
        {
            foreach (var (x, z) in corners) k.Box(new Vector3(x, 0, z), new Vector3(0.022f, 0.12f, 0.022f), Palette.Timber);
            for (int i = 0; i < 4; i++)
            {
                var (ax, az) = corners[i];
                var (bx, bz) = corners[(i + 1) % 4];
                k.Box(new Vector3((ax + bx) / 2, 0.09f, (az + bz) / 2), new Vector3(Math.Abs(bx - ax) + 0.006f, 0.006f, Math.Abs(bz - az) + 0.006f), new Color("efe6cc"));
            }
        }
        else if (stage == 1)
        {
            k.Box(new Vector3(0, 0, 0), new Vector3(hx * 2, 0.05f, hz * 2), Palette.Stone, Palette.Stone.Lightened(0.15f));
            foreach (var (x, z) in corners) k.Box(new Vector3(x * 0.9f, 0.05f, z * 0.9f), new Vector3(0.035f, 0.22f, 0.035f), Palette.Timber);
        }
        else
        {
            float f = (stage - 1) / 4.5f;
            k.Push(new Transform3D(Basis.FromScale(new Vector3(1, f, 1)), Vector3.Zero));
            Draw(k, id, 0);
            k.Pop();
            // Scaffold poles a little above the walls, with a plank walk at each storey.
            float h = 0.2f + 0.75f * f;
            for (int i = 0; i <= 2; i++)
                foreach (float z in new[] { -hz - 0.03f, hz + 0.03f })
                    k.Box(new Vector3(-hx + i * hx, 0, z), new Vector3(0.016f, h, 0.016f), Palette.Timber);
            for (float y = 0.15f; y < h; y += 0.2f)
                foreach (float z in new[] { -hz - 0.03f, hz + 0.03f }) k.Box(new Vector3(0, y, z), new Vector3(hx * 2, 0.01f, 0.045f), new Color("c8ab7e"));
        }
        // The materials pile, at the front corner where the haulers drop their loads.
        for (int i = 0; i < pile; i++)
        {
            if (stone) k.Box(new Vector3(hx - 0.06f - i * 0.11f, 0, hz + 0.1f), new Vector3(0.1f, 0.05f + 0.02f * i, 0.08f), Palette.Stone, Palette.Stone.Lightened(0.15f));
            else k.Add(i == 0 ? "town/planks" : "nature/log_stack", MeshKit.At(hx - 0.08f - i * 0.18f, 0, hz + 0.12f, Mathf.Pi / 2, i == 0 ? 0.25f : 0.45f));
        }
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
    private static void Keep(MeshKit k, Color? flag = null)
    {
        var castle = WallStone(new Color("4f5f8a"), 0.85f);
        TowerStack(k, 0, -0.04f, 0.5f, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-top"], castle);
        k.Add("castle/flag-wide", MeshKit.At(0, 1.66f, -0.04f, Mathf.Pi / 2, 0.55f), Palette.Kenney(flag ?? new Color("b8322a")));
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

    // ---------- Grand works ----------

    /// <summary>
    /// A grand work under way, in eighths: its own shape rising out of the ground stage by stage, wrapped in
    /// scaffold poles and walkways, with stone and timber stacked by the footings.
    /// </summary>
    private static void Rising(MeshKit k, string id, int eighth)
    {
        float f = 0.12f + 0.88f * eighth / 8f;
        k.Box(Vector3.Zero, new Vector3(0.98f, 0.02f, 0.98f), Palette.Dirt.Darkened(0.1f));
        k.Push(new Transform3D(Basis.FromScale(new Vector3(1, f, 1)), Vector3.Zero));
        Draw(k, id, 0);
        k.Pop();
        float hx = 0.47f, hz = 0.47f, h = 0.15f + 0.9f * f;
        if (id == "aqueduct") hz = 0.16f;
        else if (id == "royal_gardens") { hz = 0.36f; h = 0.12f + 0.2f * f; }
        else if (id == "great_gatehouse") hz = 0.32f;
        for (int i = 0; i <= 4; i++)
        {
            float x = -hx + i * hx / 2;
            foreach (float z in new[] { -hz, hz })
                k.Box(new Vector3(x, 0, z), new Vector3(0.018f, h, 0.018f), Palette.Timber);
        }
        foreach (float x in new[] { -hx, hx })
            for (int j = 1; j < 4; j++) k.Box(new Vector3(x, 0, -hz + j * hz / 2), new Vector3(0.018f, h, 0.018f), Palette.Timber);
        for (float y = 0.16f; y < h; y += 0.22f)
        {
            foreach (float z in new[] { -hz, hz }) k.Box(new Vector3(0, y, z), new Vector3(hx * 2, 0.012f, 0.05f), new Color("c8ab7e"));
            foreach (float x in new[] { -hx, hx }) k.Box(new Vector3(x, y, 0), new Vector3(0.05f, 0.012f, hz * 2), new Color("c8ab7e"));
        }
        k.Box(new Vector3(hx - 0.08f, 0, hz + 0.1f), new Vector3(0.14f, 0.06f, 0.08f), Palette.Stone, Palette.Stone.Lightened(0.15f));
        k.Add("nature/log_stack", MeshKit.At(-hx + 0.1f, 0, hz + 0.12f, Mathf.Pi / 2, 0.5f));
    }

    /// <summary>The clock tower: a tall square stone tower on a plinth, a clock face on every side, an open belfry and a gilded spire.</summary>
    private static void ClockTower(MeshKit k)
    {
        var castle = WallStone(new Color("6a5a4a"), 0.95f);
        var roof = new Color("4a5a6a");
        k.Box(Vector3.Zero, new Vector3(0.62f, 0.05f, 0.62f), new Color("9a9080"), new Color("b0a898"));
        const float S = 0.32f;
        TowerStack(k, 0, 0, S, ["tower-square-base", "tower-square-mid", "tower-square-mid-windows", "tower-square-mid", "tower-square-mid"], castle);
        float top = 5 * 1.01f * S;
        float fy = top - 0.45f * S;
        foreach (var n in new[] { Vector3.Back, Vector3.Forward, Vector3.Left, Vector3.Right })
        {
            var c = new Vector3(0, fy, 0) + n * (S * 0.53f);
            Pane(k, c, n, 0.1f, 0.1f, new Color("efe6cc"));
            Pane(k, c + n * 0.004f + new Vector3(0, 0.035f, 0), n, 0.007f, 0.04f, new Color("2a2420"));
            Pane(k, c + n * 0.004f + new Vector3(n.Z, 0, -n.X) * 0.025f, n, 0.03f, 0.006f, new Color("2a2420"));
        }
        // The belfry: four piers round a gilded bell, under a slate spire.
        float b = S * 0.42f;
        foreach (var (x, z) in new[] { (-b, -b), (b, -b), (-b, b), (b, b) })
            k.Box(new Vector3(x, top, z), new Vector3(0.05f, 0.22f, 0.05f), new Color("b0a898"));
        k.Frustum(new Vector3(0, top + 0.04f, 0), 0.07f, 0.03f, 0.12f, 8, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Push(MeshKit.At(0, top + 0.22f, 0));
        k.Pyramid(0, S * 1.05f, S * 1.05f, 0.42f, roof);
        k.Pop();
        k.Frustum(new Vector3(0, top + 0.62f, 0), 0.025f, 0, 0.16f, 4, new Color(Palette.Gold, MeshKit.Flag.Magic));
    }

    /// <summary>The aqueduct: two tiers of stone arches carrying a water channel the length of the lot, a spring house at one end and a fountain at the other.</summary>
    private static void Aqueduct(MeshKit k)
    {
        var stone = new Color("b8ae98");
        var dark = stone.Darkened(0.12f);
        const float L = 0.4f;
        // Lower arcade: piers with lintels between them.
        for (int i = 0; i <= 8; i++)
            k.Box(new Vector3(-L + i * L / 4, 0, 0), new Vector3(0.045f, 0.3f, 0.12f), dark, stone);
        k.Box(new Vector3(0, 0.26f, 0), new Vector3(2 * L + 0.045f, 0.06f, 0.12f), stone);
        // Upper arcade, twice as many and slimmer.
        for (int i = 0; i <= 16; i++)
            k.Box(new Vector3(-L + i * L / 8, 0.32f, 0), new Vector3(0.028f, 0.14f, 0.09f), dark, stone);
        k.Box(new Vector3(0, 0.44f, 0), new Vector3(2 * L + 0.03f, 0.06f, 0.1f), stone, stone.Lightened(0.1f));
        k.Box(new Vector3(0, 0.5f, 0.04f), new Vector3(2 * L, 0.02f, 0.015f), stone);
        k.Box(new Vector3(0, 0.5f, -0.04f), new Vector3(2 * L, 0.02f, 0.015f), stone);
        k.Box(new Vector3(0, 0.5f, 0), new Vector3(2 * L, 0.01f, 0.065f), new Color("3f6f95"));
        // The spring house at the hill end, the fountain at the town end.
        k.Push(MeshKit.At(L + 0.06f, 0, 0, 0, 0.13f));
        Walls(k, 1, 1, 0, true, 5, true);
        k.Push(MeshKit.At(0, 1, 0));
        k.Pyramid(0, 1.1f, 1.1f, 0.6f, new Color("58626f"));
        k.Pop();
        k.Pop();
        k.Add("town/fountain-round", MeshKit.At(-L - 0.04f, 0, 0, 0, 0.17f), Palette.Kenney(new Color("6a8aa8")));
    }

    /// <summary>The great gatehouse: twin square towers either side of a deep gate passage, a portcullis, battlements and banners.</summary>
    private static void Gatehouse(MeshKit k)
    {
        var castle = WallStone(new Color("5a4f6a"), 0.9f);
        const float S = 0.3f;
        foreach (float x in new[] { -0.3f, 0.3f })
        {
            TowerStack(k, x, 0, S, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-top-roof"], castle);
            k.Add("castle/flag-banner-long", MeshKit.At(x, 3 * 1.01f * S, S * 0.5f, 0, 0.35f), Palette.Kenney(new Color("b8322a")));
        }
        // The gate block between them, with the arch and its portcullis on the road side.
        var wall = new Color("9a9286");
        k.Box(new Vector3(0, 0, 0), new Vector3(0.32f, 0.62f, 0.3f), wall, wall.Lightened(0.08f));
        k.Add("castle/gate", new Transform3D(Basis.FromEuler(new Vector3(0, Mathf.Pi / 2, 0)) * Basis.FromScale(new Vector3(1, 0.5f, 0.32f)), new Vector3(0, 0, 0.15f)), castle);
        Pane(k, new Vector3(0, 0.15f, 0.152f), Vector3.Back, 0.07f, 0.15f, new Color("2a2622"));
        for (int i = 0; i < 6; i++)
        {
            float x = -0.15f + i * 0.06f;
            k.Box(new Vector3(x, 0.62f, 0.13f), new Vector3(0.035f, 0.05f, 0.04f), wall);
            k.Box(new Vector3(x, 0.62f, -0.13f), new Vector3(0.035f, 0.05f, 0.04f), wall);
        }
        // Wall stubs running off to meet the town wall.
        k.Box(new Vector3(-0.47f, 0, 0), new Vector3(0.06f, 0.36f, 0.12f), wall);
        k.Box(new Vector3(0.47f, 0, 0), new Vector3(0.06f, 0.36f, 0.12f), wall);
    }

    /// <summary>The mage tower: a tall hexagonal tower of warded stone with a little sister tower, crystals turning round its crown.</summary>
    private static void MageTower(MeshKit k)
    {
        var castle = Palette.Kenney(new Color("5a3f8a"));
        k.Box(Vector3.Zero, new Vector3(0.7f, 0.03f, 0.7f), new Color("6a6478"));
        k.Push(MeshKit.At(0, 0, 0, 0, 0.5f));
        k.Add("castle/tower-hexagon-base", MeshKit.At(0, 0, 0), castle);
        for (int i = 0; i < 6; i++) k.Add("castle/tower-hexagon-mid", MeshKit.At(0, 1.31f + i * 0.46f, 0), castle);
        k.Add("castle/tower-hexagon-top", MeshKit.At(0, 4.07f, 0), castle);
        k.Add("castle/tower-hexagon-roof", MeshKit.At(0, 4.2f, 0), castle);
        k.Frustum(new Vector3(0, 5.0f, 0), 0.08f, 0, 0.6f, 6, new Color(Palette.Gold, MeshKit.Flag.Magic));
        // Crystals turning round the crown, and a glow in the topmost windows.
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.Tau / 5;
            k.Blob(new Vector3(Mathf.Cos(a) * 0.75f, 3.9f + (i % 2) * 0.35f, Mathf.Sin(a) * 0.75f), new Vector3(0.08f, 0.16f, 0.08f), new Color("b48cff", MeshKit.Flag.Magic));
        }
        k.Pop();
        k.Push(MeshKit.At(0.3f, 0, 0.22f, 0.4f, 0.24f));
        k.Add("castle/tower-hexagon-base", MeshKit.At(0, 0, 0), castle);
        k.Add("castle/tower-hexagon-mid", MeshKit.At(0, 1.31f, 0), castle);
        k.Add("castle/tower-hexagon-top", MeshKit.At(0, 1.77f, 0), castle);
        k.Add("castle/tower-hexagon-roof", MeshKit.At(0, 1.9f, 0), castle);
        k.Pop();
    }

    /// <summary>The royal gardens: lawns inside clipped hedges, gravel walks, a great fountain, statues, trees and a glass orangery.</summary>
    private static void Gardens(MeshKit k)
    {
        const float X = 0.48f, Z = 0.35f;
        k.Box(Vector3.Zero, new Vector3(X * 2, 0.02f, Z * 2), Lawn);
        var gravel = new Color("d8ccae");
        k.Box(new Vector3(0, 0.02f, 0), new Vector3(X * 2, 0.004f, 0.07f), gravel);
        k.Box(new Vector3(0, 0.02f, 0), new Vector3(0.07f, 0.004f, Z * 2), gravel);
        for (int i = 0; i < 8; i++)
        {
            float x = -X + 0.06f + i * (2 * X - 0.12f) / 7;
            if (Math.Abs(x) < 0.08f) continue;
            k.Box(new Vector3(x, 0.02f, Z - 0.02f), new Vector3(0.1f, 0.05f, 0.04f), Hedge);
            k.Box(new Vector3(x, 0.02f, -Z + 0.02f), new Vector3(0.1f, 0.05f, 0.04f), Hedge);
        }
        foreach (float x in new[] { -X + 0.02f, X - 0.02f }) k.Box(new Vector3(x, 0.02f, 0), new Vector3(0.04f, 0.05f, Z * 2 - 0.04f), Hedge);
        k.Add("town/fountain-square-detail", MeshKit.At(0, 0.02f, 0, 0, 0.2f), Palette.Kenney(new Color("6a8aa8")));
        foreach (var (x, z) in new[] { (-0.2f, -0.18f), (0.2f, -0.18f), (-0.2f, 0.18f), (0.2f, 0.18f) })
            k.Add("nature/statue_column", MeshKit.At(x, 0.02f, z, 0, 0.18f));
        foreach (var (x, z, tree) in new[] { (-0.38f, -0.24f, "nature/tree_cone"), (-0.38f, 0.24f, "nature/tree_cone"), (-0.3f, 0, "nature/tree_oak"), (0.12f, 0.25f, "nature/tree_cone"), (-0.12f, 0.25f, "nature/tree_cone"), (0.12f, -0.25f, "nature/plant_bush"), (-0.12f, -0.25f, "nature/plant_bush") })
            k.Add(tree, MeshKit.At(x, 0.02f, z, x * 7, 0.22f));
        // The orangery: a glasshouse along the east end.
        var glass = new Color("a8c8d0", MeshKit.Flag.Window);
        k.Box(new Vector3(0.34f, 0.02f, 0), new Vector3(0.16f, 0.15f, 0.4f), glass, glass);
        k.Push(MeshKit.At(0.34f, 0.17f, 0, Mathf.Pi / 2));
        k.Gable(0, 0.4f, 0.16f, 0.08f, new Color("d8e4e8", MeshKit.Flag.Window), new Color("e8e0d0"), 0.01f);
        k.Pop();
        k.Box(new Vector3(0.34f, 0.02f, 0.2f), new Vector3(0.17f, 0.17f, 0.01f), new Color("e8e0d0"));
        k.Box(new Vector3(0.34f, 0.02f, -0.2f), new Vector3(0.17f, 0.17f, 0.01f), new Color("e8e0d0"));
    }

    /// <summary>The colossus: the town's founder in bronze on a stepped plinth, cloaked, a shield at the side and a lantern raised high, crowned in gold.</summary>
    private static void Colossus(MeshKit k)
    {
        var bronze = new Color("9a7440");
        var light = new Color("c09a5a");
        var stone = new Color("b0a898");
        k.Box(Vector3.Zero, new Vector3(0.8f, 0.06f, 0.8f), stone.Darkened(0.1f), stone);
        k.Box(new Vector3(0, 0.06f, 0), new Vector3(0.6f, 0.06f, 0.6f), stone.Darkened(0.05f), stone);
        k.Box(new Vector3(0, 0.12f, 0), new Vector3(0.4f, 0.22f, 0.4f), stone, stone.Lightened(0.1f));
        float y = 0.34f;
        // Legs, the cloak behind, the body tapering to the shoulders.
        k.Box(new Vector3(-0.07f, y, 0), new Vector3(0.08f, 0.5f, 0.1f), bronze, light);
        k.Box(new Vector3(0.07f, y, 0.02f), new Vector3(0.08f, 0.5f, 0.1f), bronze, light);
        k.Frustum(new Vector3(0, y + 0.42f, 0), 0.17f, 0.13f, 0.48f, 4, bronze, light, Mathf.Pi / 4);
        k.Frustum(new Vector3(0, y + 0.1f, -0.06f), 0.16f, 0.12f, 0.75f, 6, new Color("6a4a2a"), null, 0);
        float sh = y + 0.9f;
        // The lantern arm, raised.
        k.Box(new Vector3(0.15f, sh - 0.05f, 0), new Vector3(0.06f, 0.42f, 0.06f), bronze, light);
        k.Blob(new Vector3(0.15f, sh + 0.42f, 0), new Vector3(0.06f, 0.08f, 0.06f), new Color("ffb84a", MeshKit.Flag.Ember));
        // The shield arm, lowered.
        k.Box(new Vector3(-0.16f, sh - 0.35f, 0.02f), new Vector3(0.06f, 0.35f, 0.06f), bronze, light);
        k.Frustum(new Vector3(-0.21f, sh - 0.42f, 0.04f), 0.02f, 0.02f, 0.01f, 8, bronze);
        k.Push(new Transform3D(Basis.FromEuler(new Vector3(0, 0, Mathf.Pi / 2)), new Vector3(-0.2f, sh - 0.3f, 0.04f)));
        k.Frustum(Vector3.Zero, 0.13f, 0.13f, 0.03f, 10, light, new Color(Palette.Gold, MeshKit.Flag.Magic));
        k.Pop();
        // The head, and the crown.
        k.Blob(new Vector3(0, sh + 0.1f, 0), new Vector3(0.08f, 0.1f, 0.08f), bronze);
        k.Frustum(new Vector3(0, sh + 0.17f, 0), 0.07f, 0.08f, 0.06f, 8, new Color(Palette.Gold, MeshKit.Flag.Magic));
    }

    /// <summary>The great cathedral: a long nave with buttresses down both flanks, a rose window over the west door and a needle spire.</summary>
    private static void GreatCathedral(MeshKit k)
    {
        var stone = new Color("c8bea8");
        var dark = stone.Darkened(0.15f);
        var roof = new Color("4f5866");
        k.Box(Vector3.Zero, new Vector3(0.9f, 0.04f, 0.76f), dark, stone);
        k.Box(new Vector3(0, 0.04f, 0), new Vector3(0.34f, 0.42f, 0.66f), dark, stone);
        k.Push(MeshKit.At(0, 0.46f, 0, Mathf.Pi / 2));
        k.Gable(0, 0.66f, 0.34f, 0.2f, roof, stone, 0.02f);
        k.Pop();
        // Transept across the middle.
        k.Box(new Vector3(0, 0.04f, -0.05f), new Vector3(0.7f, 0.34f, 0.18f), dark, stone);
        k.Push(MeshKit.At(0, 0.38f, -0.05f));
        k.Gable(0, 0.7f, 0.18f, 0.14f, roof, stone, 0.02f);
        k.Pop();
        // Flying buttresses down both flanks.
        for (int i = 0; i < 4; i++)
        {
            float z = -0.28f + i * 0.17f;
            if (Math.Abs(z + 0.05f) < 0.1f) continue;
            foreach (float x in new[] { -0.26f, 0.26f })
                k.Box(new Vector3(x, 0.04f, z), new Vector3(0.05f, 0.3f, 0.04f), dark, stone);
        }
        // The west front: towers either side of the door, the rose window between, and the great spire behind.
        foreach (float x in new[] { -0.15f, 0.15f })
        {
            k.Box(new Vector3(x, 0.04f, 0.33f), new Vector3(0.1f, 0.62f, 0.1f), dark, stone);
            k.Frustum(new Vector3(x, 0.66f, 0.33f), 0.06f, 0, 0.18f, 4, roof);
        }
        Pane(k, new Vector3(0, 0.3f, 0.335f), Vector3.Back, 0.07f, 0.07f, new Color("c87ad8", MeshKit.Flag.Window));
        Pane(k, new Vector3(0, 0.1f, 0.335f), Vector3.Back, 0.05f, 0.08f, new Color("2a2420"));
        k.Box(new Vector3(0, 0.46f, -0.05f), new Vector3(0.12f, 0.24f, 0.12f), dark, stone);
        k.Frustum(new Vector3(0, 0.7f, -0.05f), 0.08f, 0, 0.62f, 8, roof);
        k.Frustum(new Vector3(0, 1.3f, -0.05f), 0.015f, 0, 0.08f, 4, new Color(Palette.Gold, MeshKit.Flag.Magic));
    }

    /// <summary>The royal palace: a raised terrace carrying a throne hall under a gilded dome, two long wings and corner pavilions.</summary>
    private static void Palace(MeshKit k)
    {
        var stone = new Color("e0d6c0");
        var dark = stone.Darkened(0.12f);
        var roof = new Color("3f5a78");
        var gold = new Color(Palette.Gold, MeshKit.Flag.Magic);
        k.Box(Vector3.Zero, new Vector3(0.94f, 0.08f, 0.78f), dark, stone);
        k.Box(new Vector3(0, 0.0f, 0.4f), new Vector3(0.3f, 0.04f, 0.06f), dark, stone);
        // The throne hall and its dome.
        k.Box(new Vector3(0, 0.08f, -0.05f), new Vector3(0.32f, 0.32f, 0.36f), dark, stone);
        k.Frustum(new Vector3(0, 0.4f, -0.05f), 0.15f, 0.13f, 0.06f, 12, stone);
        k.Blob(new Vector3(0, 0.5f, -0.05f), new Vector3(0.13f, 0.12f, 0.13f), gold);
        k.Frustum(new Vector3(0, 0.6f, -0.05f), 0.02f, 0, 0.1f, 6, gold);
        // The wings.
        foreach (float x in new[] { -0.3f, 0.3f })
        {
            k.Box(new Vector3(x, 0.08f, 0), new Vector3(0.28f, 0.22f, 0.22f), dark, stone);
            k.Push(MeshKit.At(x, 0.3f, 0));
            k.Gable(0, 0.28f, 0.22f, 0.08f, roof, stone, 0.01f);
            k.Pop();
            foreach (float z in new[] { -0.14f, 0.14f })
            {
                float px = x + Math.Sign(x) * 0.12f;
                k.Box(new Vector3(px, 0.08f, z), new Vector3(0.09f, 0.3f, 0.09f), dark, stone);
                k.Blob(new Vector3(px, 0.42f, z), new Vector3(0.05f, 0.06f, 0.05f), gold);
            }
        }
        // A colonnade along the front.
        for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.15f + i * 0.06f, 0.08f, 0.16f), new Vector3(0.018f, 0.24f, 0.018f), stone);
        k.Add("castle/flag-banner-long", MeshKit.At(0, 0.6f, 0.13f, 0, 0.25f), Palette.Kenney(new Color("6a2a8a")));
    }

    /// <summary>The skyward citadel: a ring of round towers about a tall keep on a rocky mound, the beacon burning on top.</summary>
    private static void Citadel(MeshKit k)
    {
        var castle = WallStone(new Color("4f5a66"), 0.9f);
        var rock = new Color("7a7468");
        k.Frustum(Vector3.Zero, 0.46f, 0.34f, 0.12f, 9, rock.Darkened(0.1f), rock);
        const float S = 0.2f;
        foreach (var (x, z) in new[] { (-0.24f, -0.24f), (0.24f, -0.24f), (-0.24f, 0.24f), (0.24f, 0.24f) })
        {
            k.Push(MeshKit.At(x, 0.12f, z));
            TowerStack(k, 0, 0, S, ["tower-square-base", "tower-square-mid", "tower-square-top-roof"], castle);
            k.Pop();
        }
        var wall = new Color("8a8478");
        k.Box(new Vector3(0, 0.12f, -0.24f), new Vector3(0.44f, 0.2f, 0.05f), wall);
        k.Box(new Vector3(0, 0.12f, 0.24f), new Vector3(0.44f, 0.2f, 0.05f), wall);
        k.Box(new Vector3(-0.24f, 0.12f, 0), new Vector3(0.05f, 0.2f, 0.44f), wall);
        k.Box(new Vector3(0.24f, 0.12f, 0), new Vector3(0.05f, 0.2f, 0.44f), wall);
        k.Push(MeshKit.At(0, 0.12f, 0));
        TowerStack(k, 0, 0, 0.26f, ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-mid-windows", "tower-square-top"], castle);
        k.Pop();
        float top = 0.12f + 5 * 1.01f * 0.26f;
        k.Frustum(new Vector3(0, top, 0), 0.06f, 0.08f, 0.06f, 8, new Color("5a4a3a"));
        k.Blob(new Vector3(0, top + 0.1f, 0), new Vector3(0.07f, 0.1f, 0.07f), new Color("ffb84a", MeshKit.Flag.Ember));
        k.Add("castle/flag-banner-long", MeshKit.At(0.24f, 0.12f + 3 * 1.01f * S, 0.24f, 0, 0.3f), Palette.Kenney(new Color("2a4a8a")));
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

    /// <summary>The footings that prop buildings up on a slope; the lanes and yards are painted on the ground (<see cref="Ground(Town)"/>).</summary>
    private void Lots(Town town)
    {
        // Paths wear in and grow back day by day.
        string key = $"{town.Lots.Count}|{town.Plan.Streets.Count}|{town.Day}";
        if (key == _groundKey) return;
        _groundKey = key;
        var k = new MeshKit();
        foreach (var l in town.Lots) Footing(k, l);
        k.Build(_ground.Mesh as ArrayMesh ?? (ArrayMesh)(_ground.Mesh = new ArrayMesh()));
        Ground(town);
    }

    /// <summary>
    /// What a building stands on where the ground falls away under it: a low stone plinth on a gentle
    /// slope; timber stilts under a plank deck on a steeper one; and steps down from the door.
    /// </summary>
    private void Footing(MeshKit k, Lot l)
    {
        float floor = Floor(l);
        var corners = Geo.LotCorners(l, 0);
        float low = corners.Min(Ground);
        if (floor - low < 0.03f) return;
        var at = Geo.LotPos(l);
        float w = l.W / (float)Land.Res, d = l.D / (float)Land.Res, yaw = Geo.LotYaw(l);
        var stone = Palette.Stone.Darkened(0.05f + Geo.Hash(l.Id * 13) * 0.08f);
        bool stilts = floor - low > 0.16f && l.Zone != Zone.Civic;
        k.Push(MeshKit.At(at.X, 0, at.Y, yaw));
        if (!stilts) k.Box(new Vector3(0, low - 0.05f, 0), new Vector3(w * 0.97f, floor - low + 0.06f, d * 0.97f), stone, stone.Lightened(0.08f));
        else
        {
            // A plank deck under the floor, on posts down to the ground, braced where they're tall.
            k.Box(new Vector3(0, floor - 0.05f, 0), new Vector3(w * 1.02f, 0.05f, d * 1.02f), Palette.DarkTimber);
            int nx = Math.Max(2, (int)Mathf.Ceil(w / 0.32f)), nz = Math.Max(2, (int)Mathf.Ceil(d / 0.32f));
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    if (ix != 0 && ix != nx - 1 && iz != 0 && iz != nz - 1) continue;
                    float u = (ix / (float)(nx - 1) - 0.5f) * w * 0.94f, v = (iz / (float)(nz - 1) - 0.5f) * d * 0.94f;
                    var world = at + new Vector2(u, v).Rotated(-yaw);
                    float g = Ground(world) - 0.04f;
                    if (floor - 0.05f - g < 0.02f) continue;
                    k.Box(new Vector3(u, g, v), new Vector3(0.045f, floor - 0.05f - g, 0.045f), Palette.Timber);
                    if (floor - g > 0.3f && (ix == 0 || ix == nx - 1) && iz < nz - 1)
                        k.Box(new Vector3(u, g + (floor - g) * 0.45f, v + d * 0.94f / (nz - 1) / 2), new Vector3(0.03f, 0.025f, d * 0.94f / (nz - 1)), Palette.DarkTimber);
                }
        }
        // Steps from the door down to the ground in front.
        var front = at + Geo.LotFront(l) * (d / 2 + 0.02f);
        float drop = floor - Ground(front);
        if (drop > 0.06f)
        {
            int n = Math.Min(10, (int)Mathf.Ceil(drop / 0.06f));
            float rise = drop / n;
            for (int i = 0; i < n; i++)
                k.Box(new Vector3(0, floor - (i + 1) * rise - 0.02f, d / 2 + 0.05f + i * 0.07f), new Vector3(0.26f, rise + 0.02f, 0.08f), stilts ? Palette.Timber : stone.Lightened(0.05f));
        }
        k.Pop();
    }

    /// <summary>Lane and yard texels to a plan cell.</summary>
    private const int GroundUp = 2;

    private ImageTexture? _groundTex;

    /// <summary>
    /// The lanes, the square and the trodden yards round buildings, painted into a texture the terrain
    /// shader blends in as worn earth with ragged edges (r: lane, g: yard, b: cobbles the masons have laid), so they lie on the hills.
    /// </summary>
    private void Ground(Town town)
    {
        int n = Land.Size * GroundUp;
        float px = 1f / (Land.Res * GroundUp);
        var lane = new float[n * n];
        var yard = new float[n * n];
        var cobble = new float[n * n];
        void Stroke(float[] into, Vector2 a, Vector2 b, float half, float soft)
        {
            float reach = half + soft;
            int x0 = Math.Max(0, (int)((Mathf.Min(a.X, b.X) - reach) / px)), x1 = Math.Min(n - 1, (int)((Mathf.Max(a.X, b.X) + reach) / px));
            int y0 = Math.Max(0, (int)((Mathf.Min(a.Y, b.Y) - reach) / px)), y1 = Math.Min(n - 1, (int)((Mathf.Max(a.Y, b.Y) + reach) / px));
            var ab = b - a;
            float len2 = Mathf.Max(ab.LengthSquared(), 1e-6f);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2((x + 0.5f) * px, (y + 0.5f) * px);
                    float t = Mathf.Clamp((q - a).Dot(ab) / len2, 0, 1);
                    float v = 1 - Mathf.SmoothStep(half - soft, half + soft, q.DistanceTo(a + ab * t));
                    int i = y * n + x;
                    into[i] = Mathf.Max(into[i], v);
                }
        }
        foreach (var r in town.Plan.Streets)
            for (int s = 0; s + 3 < r.Points.Length; s += 2)
                Stroke(lane, new Vector2((float)r.Points[s], (float)r.Points[s + 1]), new Vector2((float)r.Points[s + 2], (float)r.Points[s + 3]), (float)Streets.Width * 0.36f, 0.09f);
        // Cobbles, from the end the masons began at, as far as they have got.
        foreach (var r in town.Plan.Streets)
        {
            if (r.Cobbled <= 0) continue;
            double left = r.Cobbled;
            int m = r.Points.Length;
            for (int k = 0; k + 3 < m && left > 0; k += 2)
            {
                int i = r.FromEnd ? m - 2 - k : k, j = r.FromEnd ? i - 2 : i + 2;
                var a = new Vector2((float)r.Points[i], (float)r.Points[i + 1]);
                var b = new Vector2((float)r.Points[j], (float)r.Points[j + 1]);
                float seg = a.DistanceTo(b);
                if (seg <= 1e-5f) continue;
                var end = left >= seg ? b : a + (b - a) * (float)(left / seg);
                Stroke(cobble, a, end, (float)Streets.Width * (r.Square ? 0.5f : 0.4f), 0.05f);
                left -= seg;
            }
        }
        // The square's trodden middle.
        for (int i = 0; i < n * n; i++)
            if (town.Plan.Paved[i / n / GroundUp * Land.Size + i % n / GroundUp] != 0) lane[i] = Mathf.Max(lane[i], 0.8f);
        // Paths half worn in by feet going to buildings off the lanes: fainter than a lane until they become one.
        var worn = town.Plan.Worn;
        float lay = (float)town.Content.City.Paths.Lay;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float cx = (x + 0.5f) / GroundUp - 0.5f, cy = (y + 0.5f) / GroundUp - 0.5f;
                int x0 = Math.Clamp((int)cx, 0, Land.Size - 2), y0 = Math.Clamp((int)cy, 0, Land.Size - 2);
                float fx = Mathf.Clamp(cx - x0, 0, 1), fy = Mathf.Clamp(cy - y0, 0, 1);
                int c = y0 * Land.Size + x0;
                float w = Mathf.Lerp(Mathf.Lerp(worn[c], worn[c + 1], fx), Mathf.Lerp(worn[c + Land.Size], worn[c + Land.Size + 1], fx), fy);
                if (w > 0.03f) lane[y * n + x] = Mathf.Max(lane[y * n + x], Mathf.SmoothStep(0.05f, lay, w) * 0.6f);
            }
        // Yards: worn ground round each building, and a footpath from its door to the lane.
        foreach (var l in town.Lots)
        {
            if (l.Zone == Zone.Farmland) continue;
            var c = new Vector2((float)l.X, (float)l.Y);
            float yaw = (float)l.Yaw;
            var f = new Vector2(-Mathf.Sin(yaw), Mathf.Cos(yaw));
            var side = new Vector2(f.Y, -f.X);
            float hw = l.W / (float)Land.Res / 2, hd = l.D / (float)Land.Res / 2;
            Stroke(yard, c - side * hw * 0.6f, c + side * hw * 0.6f, hd + 0.18f, 0.15f);
            var door = c + f * hd;
            if (Streets.Nearest(town, door.X, door.Y) is { } near && near.D < 1.6)
                Stroke(lane, door, new Vector2((float)near.X, (float)near.Y), 0.07f, 0.06f);
        }
        var bytes = new byte[n * n * 4];
        for (int i = 0; i < n * n; i++)
        {
            bytes[i * 4] = (byte)(Mathf.Clamp(lane[i], 0, 1) * 255);
            bytes[i * 4 + 1] = (byte)(Mathf.Clamp(yard[i], 0, 1) * 255);
            bytes[i * 4 + 2] = (byte)(Mathf.Clamp(cobble[i], 0, 1) * 255);
            bytes[i * 4 + 3] = 255;
        }
        var img = Image.CreateFromData(n, n, false, Image.Format.Rgba8, bytes);
        if (_groundTex == null)
        {
            _groundTex = ImageTexture.CreateFromImage(img);
            Mats.Terrain.SetShaderParameter("town_tex", _groundTex);
            Mats.Terrain.SetShaderParameter("town_origin", new Vector2(Geo.Margin, Geo.Margin));
            Mats.Terrain.SetShaderParameter("town_size", (float)Geo.Grid);
        }
        else _groundTex.Update(img);
    }

    /// <summary>Show the painted zones strongly while the player plans, faintly otherwise.</summary>
    public void Planning(bool on)
    {
        _planning = on;
        Mats.Terrain.SetShaderParameter("zone_strength", on ? 0.5f : 0.1f);
    }

    /// <summary>Zone paint texels to a plan cell: the plan is drawn finer than it is kept, then softened, so painted edges come out round.</summary>
    private const int ZoneUp = 4;

    private readonly float[] _zoneBuf = new float[Land.Size * ZoneUp * Land.Size * ZoneUp * 4], _zoneTmp = new float[Land.Size * ZoneUp * Land.Size * ZoneUp * 4];

    /// <summary>Refresh the zone paint texture when the plan has changed: open painted ground only, built ground is shown by its yards.</summary>
    private void Zones(Town town)
    {
        var plan = town.Plan;
        int version = $"{town.Lots.Count}|{plan.Streets.Count}|{plan.Zones.Aggregate(17, (h, z) => h * 31 + z)}|{plan.Density.Aggregate(17, (h, z) => h * 31 + z)}|{plan.Crops.Aggregate(17, (h, z) => h * 31 + z)}".GetHashCode();
        if (version == _zoneVersion) return;
        _zoneVersion = version;
        int n = Land.Size * ZoneUp;
        // Premultiplied colour, so softening across a zone's edge fades it out instead of darkening it.
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y / ZoneUp * Land.Size + x / ZoneUp, o = (y * n + x) * 4;
                var z = (Zone)plan.Zones[i];
                bool show = z is not (Zone.None or Zone.Civic) && plan.Taken[i] == 0 && plan.Paved[i] == 0;
                // Fields show their crop.
                var c = !show ? new Color(0, 0, 0, 0) : z == Zone.Farmland ? Planner.CropColor(Farming.Crop(town, plan.Crops[i])) : Planner.ZoneColor(z, plan.Density[i]);
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
            Mats.Terrain.SetShaderParameter("zones_tex", _zoneTex);
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

    private void Palisade(Town town)
    {
        var d = town.Defenses;
        var e = town.Content.Enemy;
        var line = WallLine(town);
        float done = Progress(town, d.Palisade, d.PalisadeReadyTick, e.Palisade.Days);
        float stone = Progress(town, d.StoneWall, d.StoneWallReadyTick, e.StoneWall.Days);
        float city = Progress(town, d.CityWall, d.CityWallReadyTick, e.CityWall.Days);
        // Once the stone walls stand, the timber palisade and its towers come down.
        bool timber = d.StoneWall != PalisadeState.Built;
        string key = $"{town.City.WallVersion}|{d.Palisade}|{Math.Round(done, 2)}|{d.Towers}|{(line == null ? "" : string.Join(';', line))}|{timber}|{Math.Round(stone, 2)}|{Math.Round(city, 2)}|{d.Gatehouse}|{d.Moat}|{d.Barracks}|{d.CityWall}";
        if (key == _wallKey) return;
        _wallKey = key;
        var k = new MeshKit();
        if (timber && line != null)
        {
            float perim = Perimeter(line);
            if (d.Palisade != PalisadeState.None)
            {
                // Sharpened stakes all round, raised in order so a rising wall reads as progress.
                int stakes = (int)(perim / 0.13f), shown = (int)(stakes * Mathf.Clamp(done, 0, 1));
                var gates = GatePoints(town, 0);
                for (int i = 0; i < shown; i++)
                {
                    var p = Along(line, (float)i / stakes * perim);
                    // The stakes leave a way through at each gate.
                    if (gates.Any(g => g.DistanceTo(p) < 0.36f)) continue;
                    float sh = 0.32f + Geo.Hash(i) * 0.06f;
                    var col = i % 2 == 0 ? Palette.Timber : new Color("8a6239");
                    float g = Ground(p) - 0.05f;
                    k.Frustum(new Vector3(p.X, g, p.Y), 0.055f, 0.055f, sh + 0.05f, 5, col, col, i);
                    k.Frustum(new Vector3(p.X, g + sh + 0.05f, p.Y), 0.055f, 0, 0.1f, 5, col, null, i);
                }
            }
            var castle = Palette.Kenney(new Color("9b3d2a"));
            for (int i = 0; i < d.Towers; i++)
            {
                // Each tower on the section the sim puts it.
                var c = RayHit(line, Bearing(town, Garrison.TowerSection(town, i)));
                float g = Ground(c);
                k.Add("castle/tower-square-base", MeshKit.At(c.X, g, c.Y, 0, 0.42f), castle);
                k.Add("castle/tower-square-mid", MeshKit.At(c.X, g + 0.42f, c.Y, 0, 0.42f), castle);
                // An open fighting top, where the watch stand when the tower is called to arms.
                k.Add("castle/tower-square-top", MeshKit.At(c.X, g + 0.84f, c.Y, 0, 0.42f), castle);
                k.Add("castle/flag-pennant", MeshKit.At(c.X, g + 0.97f, c.Y, 0, 0.3f), Palette.Kenney(new Color("b8322a")));
            }
        }
        bool gatehouse = d.Gatehouse == PalisadeState.Built;
        if (stone > 0) StoneRing(k, RingLine(town, 0), stone, 0.24f, 0.8f, 0.5f, 1, 12, WallStone(Slate, 0.82f), gatehouse || d.CityWall == PalisadeState.Built, GatePoints(town, 0).Skip(1).ToArray());
        if (city > 0) StoneRing(k, RingLine(town, 1), city, 0.3f, 1.05f, 0.62f, 2, 20, WallStone(new Color("9b3d2a"), 0.72f), gatehouse, GatePoints(town, 1).Skip(1).ToArray());
        Ramparts(k, town, line);
        k.Build(_walls.Mesh as ArrayMesh ?? (ArrayMesh)(_walls.Mesh = new ArrayMesh()));
    }

    private static float Perimeter(Vector2[] loop)
    {
        float perim = 0;
        for (int i = 0; i < loop.Length; i++) perim += loop[i].DistanceTo(loop[(i + 1) % loop.Length]);
        return perim;
    }

    /// <summary>A point a distance along a closed loop, going round from its first point.</summary>
    private static Vector2 Along(Vector2[] loop, float t)
    {
        for (int i = 0; i < loop.Length; i++)
        {
            var a = loop[i];
            var b = loop[(i + 1) % loop.Length];
            float len = a.DistanceTo(b);
            if (t <= len) return a.Lerp(b, len > 0 ? t / len : 0);
            t -= len;
        }
        return loop[0];
    }

    /// <summary>
    /// The line a stone ring follows round the wards it encloses (ring 0 the old town, ring 1 the wards inside the city wall):
    /// just outside the ward border (<see cref="City.Edge(Town, int, double)"/>), so the wall bends with it, starting at its south gate.
    /// </summary>
    public static Vector2[] RingLine(Town town, int ring)
    {
        var c = new Vector2(Geo.Margin + (float)Town.Center, Geo.Margin + (float)Town.Center);
        const int N = 96;
        var loop = new Vector2[N];
        // From the main gate (the south road, until the player puts one elsewhere), then round clockwise as seen from above.
        var gates = WallLines.Gates(town, ring);
        double a0 = gates.Count > 0 ? gates[0] : Math.PI / 2;
        for (int i = 0; i < N; i++)
        {
            double a = a0 + i * Math.PI * 2 / N;
            float r = (float)City.Wall(town, ring, a);
            loop[i] = c + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * r;
        }
        return loop;
    }

    /// <summary>Where a ring's gates stand on the map, the main gate first.</summary>
    public static Vector2[] GatePoints(Town town, int ring) =>
        [.. WallLines.Gates(town, ring).Select(g => WallLines.Point(town, ring, g)).Select(p => new Vector2(Geo.Margin + (float)p.X, Geo.Margin + (float)p.Y))];

    /// <summary>
    /// A stone curtain wall along a closed loop that starts at its main gate, towers spaced round it,
    /// and a gatehouse where the road leaves; timber gates in the gaps left at the other gates. Raised round the loop as the work goes on.
    /// </summary>
    private void StoneRing(MeshKit k, Vector2[] loop, float done, float thick, float high, float tower, int mids, int towers, Func<Color, Color> tint, bool gatehouse = true, Vector2[]? others = null)
    {
        others ??= [];
        float perim = Perimeter(loop), reach = done * perim + 0.01f, gap = tower * 0.75f;
        // Wall sections, leaving the gateway open at the loop's start.
        const float Seg = 0.5f;
        int n = (int)Mathf.Round(perim / Seg);
        float step = perim / n;
        for (int i = 0; i < n; i++)
        {
            float t = (i + 0.5f) * step;
            if (t > reach) break;
            if (t < gap || t > perim - gap) continue;
            var a = Along(loop, i * step);
            var b = Along(loop, (i + 1) * step);
            var p = (a + b) / 2;
            if (others.Any(o => o.DistanceTo(p) < gap)) continue;
            var dir = b - a;
            float yaw = Mathf.Atan2(dir.X, dir.Y);
            var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromScale(new Vector3(thick / 0.92f, high / 1.31f, dir.Length() * 1.04f));
            k.Add("castle/wall", new Transform3D(basis, new Vector3(p.X, Ground(p) - 0.05f, p.Y)), tint);
        }
        string[] stack = [.. Enumerable.Repeat("tower-square-base", 1), .. Enumerable.Repeat("tower-square-mid", mids), "tower-square-top-roof"];
        for (int j = 1; j < towers; j++)
        {
            float t = perim * j / towers;
            if (t > reach) break;
            if (t < 1.2f || t > perim - 1.2f) continue;
            var c = Along(loop, t);
            k.Push(MeshKit.At(c.X, Ground(c), c.Y));
            TowerStack(k, 0, 0, tower, stack, tint);
            k.Pop();
        }
        // The gatehouse on the main road: two taller towers, a portcullis and the arch over it; until it is built, timber gates.
        if (reach < perim) return;
        // The other gates: timber gates between a pair of towers.
        foreach (var o in others)
        {
            var outward = (o - Geo.Center).Normalized();
            TimberGate(k, o, outward);
            foreach (float s in new[] { -1f, 1f })
            {
                var at = o + new Vector2(outward.Y, -outward.X) * s * (gap + tower * 0.35f);
                k.Push(MeshKit.At(at.X, Ground(at), at.Y));
                TowerStack(k, 0, 0, tower * 0.85f, stack, tint);
                k.Pop();
            }
        }
        if (!gatehouse)
        {
            TimberGate(k, loop[0], (loop[0] - Geo.Center).Normalized());
            return;
        }
        var gate = loop[0];
        var side = (loop[1] - loop[^1]).Normalized();
        float gy = Ground(gate);
        string[] gateStack = [.. stack[..^1], "tower-square-mid-windows", "tower-square-top-roof"];
        foreach (float s in new[] { -1f, 1f })
        {
            var at = gate + side * s * (gap + tower * 0.35f);
            k.Push(MeshKit.At(at.X, gy, at.Y));
            TowerStack(k, 0, 0, tower * 0.9f, gateStack, tint);
            k.Pop();
            k.Add("castle/flag-pennant", MeshKit.At(at.X, gy + tower * 0.9f * gateStack.Length, at.Y, 0, tower * 0.7f), Palette.Kenney(new Color("b8322a")));
        }
        float gyaw = Mathf.Atan2(side.X, side.Y);
        k.Add("castle/gate", new Transform3D(Basis.FromEuler(new Vector3(0, gyaw + Mathf.Pi / 2, 0)) * Basis.FromScale(new Vector3(1.4f, high * 0.75f / 0.91f, gap * 2 / 0.66f)), new Vector3(gate.X, gy, gate.Y)), tint);
        k.Box(new Vector3(gate.X, gy + high * 0.75f, gate.Y), new Vector3(gap * 2 + 0.05f, high * 0.35f, thick * 1.1f), new Color("9a9488"), new Color("8a847a"));
    }

    /// <summary>
    /// Where the palisade stands: the line round the old town that its stone wall will replace in place, kept clear of
    /// buildings from the founding (<see cref="City.OnWallStrip"/>).
    /// </summary>
    public static Vector2[] WallLine(Town town) => RingLine(town, 0);

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
        // The river's bridges, where roads cross it (its fords are shallows in the water itself).
        var gen = map.Gen;
        for (int i = 0; i < gen.Crossings.Count; i++)
        {
            var (c, at, flow) = gen.Crossings[i];
            string id = $"crossing:{i}";
            if (c.Kind != "bridge" || _landmarks.ContainsKey(id)) continue;
            var across = new Vector2(-flow.Y, flow.X);
            float deck = Mathf.Max(gen.SurfaceAt(at.X + across.X * 4.6f, at.Y + across.Y * 4.6f), gen.SurfaceAt(at.X - across.X * 4.6f, at.Y - across.Y * 4.6f)) + 0.1f;
            var mi = new MeshInstance3D { Mesh = Bridge(deck, i == 0), MaterialOverride = Mats.Props };
            AddChild(mi);
            _landmarks[id] = mi;
            mi.Position = new Vector3(at.X, 0, at.Y);
            mi.Rotation = new Vector3(0, Mathf.Atan2(across.X, across.Y), 0);
        }
    }

    /// <summary>A bridge over the river, its deck at <paramref name="deck"/>: the town's of timber on stone piers, the old ones all stone.</summary>
    private static ArrayMesh Bridge(float deck, bool timber)
    {
        var k = new MeshKit();
        var stone = new Color("8a8478");
        var wood = Palette.Timber;
        k.Box(new Vector3(0, deck - 0.16f, 0), new Vector3(1.15f, 0.16f, 9.6f), timber ? wood : stone, timber ? new Color("8a6239") : new Color("9a9488"));
        foreach (float x in new[] { -0.55f, 0.55f }) k.Box(new Vector3(x, deck, 0), new Vector3(0.08f, timber ? 0.14f : 0.2f, 9.6f), timber ? Palette.DarkTimber : stone);
        foreach (float z in new[] { -2.4f, -0.8f, 0.8f, 2.4f }) k.Box(new Vector3(0, -1.4f, z), new Vector3(1.0f, deck + 1.25f, 0.5f), stone, stone);
        // Abutments down into each bank, so the deck meets the ground instead of floating over it.
        foreach (float z in new[] { -4.2f, 4.2f }) k.Box(new Vector3(0, -1.4f, z), new Vector3(1.3f, deck + 1.25f, 1.6f), stone, stone);
        return k.Build();
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
