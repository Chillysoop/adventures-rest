using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The land's mood and the neighbours' homes: the peoples the town has met drawn where they live (elves in tree towns,
/// gorge folk on ledges with lift baskets, the castles, harbours and cliff cities on the sites the town didn't found on),
/// stake fences round the farms outside the walls, mist lying on the sea and the river at dawn, waterfalls off the
/// plateaus, lift baskets up a cliff-top town's face, houses on the town's bridge once it is a town, and snow falling
/// in winter. All looks: the sim is untouched, and every random pick goes through <see cref="Geo.Hash"/>.
/// </summary>
public partial class TownView
{
    private readonly MeshInstance3D _farmFences = new() { MaterialOverride = Mats.Props };
    private readonly MeshInstance3D _mists = new() { MaterialOverride = Mats.Mist, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    private readonly MeshInstance3D _falls = new() { MaterialOverride = Mats.Falls, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
    private readonly List<(MeshInstance3D Basket, Vector3 Top, Vector3 Foot, float Phase)> _lifts = [];
    private CpuParticles3D? _snowfall;
    private string _farmKey = "";
    private bool _sceneryBuilt;

    /// <summary>The town's bridge grows houses on it from this charter on: its bridge ward.</summary>
    private const string BridgeWardTier = "town";

    private void Scenery(Town town)
    {
        if (!_sceneryBuilt)
        {
            _sceneryBuilt = true;
            foreach (var n in new Node[] { _farmFences, _mists, _falls })
                if (n.GetParent() == null) AddChild(n);
            Mists(town);
            Waterfalls(town);
            CliffLifts(town);
        }
        Settlements(town);
        FarmFences(town);
    }

    private void ResetScenery()
    {
        _lifts.Clear();
        _farmKey = "";
        _sceneryBuilt = false;
        _mists.Mesh = null;
        _falls.Mesh = null;
        _farmFences.Mesh = null;
    }

    /// <summary>Every frame: lift baskets ride their ropes, the mist thickens at dawn, and snow falls round the camera in winter.</summary>
    public void Weather(float dt)
    {
        var town = map.Sim.Town;
        float t = (float)Time.GetTicksMsec() / 1000f;
        foreach (var (basket, top, foot, phase) in _lifts)
        {
            // Up, a rest at the top, down, a rest at the foot: forty seconds a round.
            float u = Mathf.PosMod(t / 40f + phase, 1f);
            float y = u < 0.4f ? Mathf.SmoothStep(0, 1, u / 0.4f) : u < 0.5f ? 1 : u < 0.9f ? 1 - Mathf.SmoothStep(0, 1, (u - 0.5f) / 0.4f) : 0;
            basket.Position = foot.Lerp(top, y);
        }
        var look = Seasons.Look(town);
        float hour = (town.Tick % SimTime.HoursPerDay) + map.HourFraction;
        // Thickest an hour after dawn, burnt off by late morning, a little back at dusk; more in autumn and winter, and over the sea.
        float dawn = Mathf.Exp(-Mathf.Pow((hour - 6.5f) / 1.8f, 2)), dusk = 0.4f * Mathf.Exp(-Mathf.Pow((hour - 20f) / 1.5f, 2));
        float mist = Mathf.Clamp(0.1f + dawn * 0.6f + dusk + (float)look.Autumn * 0.2f + (float)look.Winter * 0.15f + (Fleet.Coastal(town) ? 0.15f : 0), 0, 1);
        Mats.Mist.SetShaderParameter("mist", mist);
        Snowfall((float)look.Winter);
    }

    // ---------- Neighbours ----------

    /// <summary>How a people's home looks: by the site it holds if the town might have founded there, else by its people and country.</summary>
    public static string SettlementLook(Town town, Settlement st)
    {
        var region = town.Regions[st.RegionId];
        switch (region.Site)
        {
            case "crag" or "pass-keep": return "castle";
            case "sea-cove" or "isle": return "harbour";
            case "plateau": return "mesa";
            case "desert-cliff": return "cliff-city";
            case "cloud-ledge": return "eyrie";
        }
        if (st.Kin.Race == "elf") return "trees";
        var (x, y) = Explore.SettlementSpot(town, st);
        double d = Math.Sqrt(x * x + y * y), b = Math.Atan2(x, -y) * 180 / Math.PI;
        foreach (var f in town.Terrain)
        {
            if (f.Kind.Id != "gorge") continue;
            double off = Math.Abs(((b - f.Bearing) % 360 + 540) % 360 - 180);
            if (d > f.Inner - 0.6 && d < f.Outer + 0.6 && off < f.Arc / 2 + 12) return "gorge";
        }
        if (st.Kin.Race == "dwarf") return "hall";
        return "hamlet";
    }

    /// <summary>Each people the town has met, drawn where it lives: burnt shells once razed.</summary>
    private void Settlements(Town town)
    {
        foreach (var st in town.Settlements)
        {
            if (!st.Met) continue;
            string id = $"settlement:{st.Id}";
            string look = SettlementLook(town, st) + (st.Razed ? ":razed" : "");
            if (_landmarks.TryGetValue(id, out var old) && (string)old.GetMeta("kind") == look) continue;
            old?.QueueFree();
            var pin = Geo.SettlementPin(town, st);
            if (pin.X < 4 || pin.Y < 4 || pin.X > WorldGen.N - 4 || pin.Y > WorldGen.N - 4) continue;
            var lifts = new List<(Vector3 Top, Vector3 Foot)>();
            var mi = new MeshInstance3D { Mesh = SettlementMesh(look, StableHash(st.Name), Math.Clamp((int)(st.Prosperity / 18), 2, 5), lifts), MaterialOverride = Mats.Props };
            mi.SetMeta("kind", look);
            AddChild(mi);
            _landmarks[id] = mi;
            mi.Position = map.Gen.At(pin, -0.05f);
            // Facing the town.
            mi.Rotation = new Vector3(0, Mathf.Atan2(Geo.Center.X - pin.X, Geo.Center.Y - pin.Y), 0);
            foreach (var (top, foot) in lifts) Lift(mi.Transform * top, mi.Transform * foot, id);
        }
    }

    private static readonly Color Bark = new("7a6650"), ElfWood = new("c9b48a"), ElfRoof = new("4f8a7a"), Leaf = new("5f8f4a");
    private static readonly Color Rock = new("8c8172"), Sandstone = new("c8a070"), Window = new("ffd890", MeshKit.Flag.Window);

    /// <summary>A people's home, facing +z (the town), about eight tiles across. <paramref name="lifts"/> collects its lift ropes (top, foot), local.</summary>
    private static ArrayMesh SettlementMesh(string look, int seed, int size, List<(Vector3 Top, Vector3 Foot)> lifts)
    {
        var k = new MeshKit();
        bool razed = look.EndsWith(":razed");
        float H(int i) => Geo.Hash(seed * 31 + i);
        switch (look.Split(':')[0])
        {
            case "trees":
                // Great trees in a ring, each with a platform and a round house up in it, rope walks between them and ladders down.
                var trunks = new List<Vector3>();
                int n = size + 2;
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.Tau / n + H(i) * 0.4f, r = 2.4f + H(i + 20) * 0.8f;
                    trunks.Add(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r));
                }
                for (int i = 0; i < n; i++)
                {
                    var b = trunks[i];
                    float tall = 6.5f + H(i + 40) * 2f, deck = 2.8f + H(i + 60) * 1.2f;
                    k.Frustum(b, 0.62f, 0.38f, tall, 8, Bark, null, i);
                    foreach (var (dx, dz) in new[] { (0.6f, 0.2f), (-0.5f, 0.4f), (0.1f, -0.6f) })
                        k.Frustum(b + new Vector3(dx, 0, dz), 0.22f, 0.05f, 0.7f, 5, Bark.Darkened(0.1f));
                    // A crown high above the homes, tiered so the platforms show between the boughs.
                    for (int c = 0; c < 3; c++)
                    {
                        float ca = c * 2.1f + H(i * 7 + c);
                        k.Blob(b + new Vector3(Mathf.Cos(ca) * 0.5f, tall - 0.4f + c * 0.45f, Mathf.Sin(ca) * 0.5f), new Vector3(1.25f - c * 0.25f, 0.75f, 1.25f - c * 0.25f), Leaf.Lightened(c * 0.06f), 0.25f, seed + i * 9 + c, Leaf.Darkened(0.2f));
                    }
                    if (razed) continue;
                    k.Frustum(b + new Vector3(0, deck, 0), 1.5f, 1.5f, 0.12f, 10, ElfWood, ElfWood.Lightened(0.08f));
                    // Out from the trunk, away from the town's centre so the ring of homes reads from above.
                    var outward = new Vector3(b.X, 0, b.Z).Normalized();
                    var hut = b + outward * 0.85f + new Vector3(0, deck + 0.12f, 0);
                    k.Frustum(hut, 0.55f, 0.55f, 0.6f, 8, ElfWood.Lightened(0.1f));
                    k.Frustum(hut + new Vector3(0, 0.6f, 0), 0.75f, 0f, 0.7f, 8, ElfRoof);
                    k.Box(hut + outward * 0.53f + new Vector3(0, 0.15f, 0), new Vector3(0.16f, 0.2f, 0.16f), Window);
                    // A second, smaller home higher up the bigger trees.
                    if (tall > 7.4f)
                    {
                        var hi = b - outward * 0.7f + new Vector3(0, deck + 2.0f, 0);
                        k.Frustum(hi - new Vector3(0, 0.1f, 0), 0.8f, 0.8f, 0.1f, 8, ElfWood);
                        k.Frustum(hi, 0.4f, 0.4f, 0.45f, 8, ElfWood.Lightened(0.1f));
                        k.Frustum(hi + new Vector3(0, 0.45f, 0), 0.55f, 0f, 0.5f, 8, ElfRoof.Lightened(0.08f));
                    }
                    // A ladder down the trunk.
                    k.Box(b + new Vector3(0, 0, 0.66f), new Vector3(0.3f, deck, 0.04f), ElfWood.Darkened(0.2f));
                }
                if (!razed)
                    for (int i = 0; i < n; i++) PlankWalk(k, trunks[i] + new Vector3(0, 3.3f, 0), trunks[(i + 1) % n] + new Vector3(0, 3.3f, 0), ElfWood);
                break;

            case "gorge":
                // Two rock walls with a cleft between; houses on ledges down both faces, a bridge over the top, a lift basket.
                // Each wall a flat-topped shelf of rock with its inner face stepped into ledges.
                for (int side = -1; side <= 1; side += 2)
                {
                    k.Box(new Vector3(side * 3.4f, 0, 0), new Vector3(2.4f, 4.6f, 8.0f), Rock, new Color("7a9a52"));
                    for (int i = 0; i < 5; i++)
                        k.Blob(new Vector3(side * 4.4f, 1.6f, -3.2f + i * 1.6f), new Vector3(1.1f, 2.4f, 1.0f), Rock.Lightened(H(i + side * 10) * 0.08f), 0.2f, seed + i + side * 50, Rock.Darkened(0.2f));
                    for (int step = 0; step < 3; step++)
                        k.Box(new Vector3(side * (2.0f - step * 0.0f), 0, 0), new Vector3(0.5f, 0.8f + step * 1.4f, 8.0f - step * 1.6f), Rock.Darkened(0.06f * step), Rock.Lightened(0.04f));
                }
                k.Box(new Vector3(0, -0.05f, 0), new Vector3(3.4f, 0.08f, 8.0f), new Color("6a8aa0"));
                // Pines along both rims.
                for (int i = 0; i < 10; i++)
                {
                    float x = (i % 2 == 0 ? -1 : 1) * (3.0f + H(i + 70) * 1.2f), z = -3.6f + (i / 2) * 1.8f + H(i + 80) * 0.6f;
                    k.Box(new Vector3(x, 4.6f, z), new Vector3(0.12f, 0.4f, 0.12f), Bark);
                    k.Frustum(new Vector3(x, 4.9f, z), 0.45f, 0, 1.1f, 6, new Color("3f6f3a"));
                }
                for (int i = 0; i < size + 4; i++)
                {
                    int side = i % 2 == 0 ? -1 : 1;
                    float z = -3.0f + (i / 2) * 1.5f + H(i) * 0.3f, y = 0.8f + ((i / 2) % 3) * 1.4f;
                    k.Box(new Vector3(side * 1.55f, y - 0.1f, z), new Vector3(0.9f, 0.1f, 1.1f), Palette.DarkTimber);
                    foreach (float dz in new[] { -0.45f, 0.45f }) k.Box(new Vector3(side * 1.25f, y - 0.6f, z + dz), new Vector3(0.06f, 0.5f, 0.06f), Palette.DarkTimber);
                    if (!razed) Cottage(k, side * 1.55f, y, z, side < 0 ? Mathf.Pi / 2 : -Mathf.Pi / 2, 0.7f, 0.6f, 0.5f, Palette.Plaster, Palette.Roofs[i % Palette.Roofs.Length]);
                }
                PlankWalk(k, new Vector3(-2.2f, 4.6f, 2.6f), new Vector3(2.2f, 4.6f, 2.6f), Palette.Timber);
                if (!razed)
                {
                    k.Push(MeshKit.At(2.4f, 4.6f, -1.4f, -Mathf.Pi / 2));
                    Winch(k, Vector3.Zero, 1);
                    k.Pop();
                    lifts.Add((new Vector3(1.3f, 4.6f, -1.4f), new Vector3(1.3f, 0.05f, -1.4f)));
                }
                break;

            case "castle":
                // A crag with a castle on it: curtain wall, round towers, a keep, and a road winding up.
                k.Blob(new Vector3(0, 0, 0), new Vector3(4.2f, 2.6f, 3.8f), Rock, 0.3f, seed, Rock.Darkened(0.2f));
                k.Blob(new Vector3(1.4f, 0, 1.2f), new Vector3(2.4f, 1.6f, 2.2f), Rock.Lightened(0.05f), 0.3f, seed + 1, Rock.Darkened(0.2f));
                var stone = razed ? new Color("5a5450") : new Color("a49a8a");
                float top = 2.3f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.Tau / 6;
                    var p = new Vector3(Mathf.Cos(a) * 2.2f, top, Mathf.Sin(a) * 2.0f);
                    k.Frustum(p, 0.42f, 0.38f, razed && i % 2 == 0 ? 0.6f : 1.6f, 8, stone, stone.Lightened(0.06f), i);
                    if (!razed) k.Frustum(p + new Vector3(0, 1.6f, 0), 0.5f, 0, 0.7f, 8, new Color("58626f"));
                    var q = new Vector3(Mathf.Cos(a + Mathf.Tau / 6) * 2.2f, top, Mathf.Sin(a + Mathf.Tau / 6) * 2.0f);
                    Wall(k, p, q, razed ? 0.5f : 1.1f, stone);
                }
                k.Box(new Vector3(0, top, 0), new Vector3(1.6f, razed ? 1.0f : 2.6f, 1.6f), stone, stone.Lightened(0.05f));
                if (!razed)
                {
                    k.Pyramid(top + 2.6f, 1.6f, 1.6f, 1.0f, new Color("58626f"));
                    k.Box(new Vector3(0, top + 3.6f, 0), new Vector3(0.05f, 0.8f, 0.05f), Palette.DarkTimber);
                    k.Box(new Vector3(0.25f, top + 4.1f, 0), new Vector3(0.45f, 0.3f, 0.03f), new Color("9a3a2a"));
                    for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.5f + i * 0.35f, top + 1.2f, 0.81f), new Vector3(0.14f, 0.24f, 0.03f), Window);
                }
                break;

            case "harbour":
                // Cottages along a quay, a jetty out into the water and boats at it.
                Cottages(k, size + 2, seed, -1.5f, razed, 3.2f);
                k.Box(new Vector3(0, 0, 1.6f), new Vector3(6.5f, 0.25f, 1.0f), new Color("9a9488"), new Color("a8a294"));
                k.Box(new Vector3(1.8f, 0.05f, 3.4f), new Vector3(0.7f, 0.15f, 3.0f), Palette.Timber);
                if (!razed)
                    foreach (float x in new[] { 1.0f, 2.6f })
                    {
                        k.Box(new Vector3(x, -0.15f, 3.6f), new Vector3(0.5f, 0.3f, 1.6f), new Color("6a4a2a"), Palette.DarkTimber);
                        k.Box(new Vector3(x, 0.15f, 3.6f), new Vector3(0.04f, 1.2f, 0.04f), Palette.DarkTimber);
                        k.Box(new Vector3(x, 0.55f, 3.6f), new Vector3(0.02f, 0.6f, 0.6f), new Color("e8dcc0"));
                    }
                break;

            case "mesa" or "cliff-city":
                // A flat-topped rock with homes on it; a cliff city cuts its homes into the face as well, and both have a lift.
                bool city = look.StartsWith("cliff-city");
                var rock = city ? Sandstone : Rock;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.Tau / 8;
                    k.Blob(new Vector3(Mathf.Cos(a) * 2.3f, 0, Mathf.Sin(a) * 2.1f), new Vector3(1.6f, 3.2f, 1.6f), rock.Lightened(H(i) * 0.08f), 0.2f, seed + i, rock.Darkened(0.2f));
                }
                k.Box(new Vector3(0, 0, 0), new Vector3(4.6f, 3.0f, 4.2f), rock, rock.Darkened(0.05f));
                k.Box(new Vector3(0, 3.0f, 0), new Vector3(4.4f, 0.08f, 4.0f), new Color("7a9a52"));
                if (!razed) Cottages(k, size + 1, seed, 0, false, 3.08f, 1.6f, city ? Sandstone.Lightened(0.15f) : Palette.Plaster);
                if (city)
                    // Homes cut into the face: doorways, lit windows and a balcony on each row, stairs between.
                    for (int i = 0; i < 8; i++)
                    {
                        float x = -1.65f + (i % 4) * 1.1f, y = 0.35f + (i / 4) * 1.4f;
                        k.Box(new Vector3(x, y, 2.1f), new Vector3(0.45f, 0.75f, 0.08f), razed ? new Color("2a2420") : new Color("3a2e26"));
                        k.Box(new Vector3(x, y + 0.75f, 2.1f), new Vector3(0.55f, 0.12f, 0.1f), Sandstone.Lightened(0.2f));
                        if (razed) continue;
                        k.Box(new Vector3(x + 0.38f, y + 0.35f, 2.12f), new Vector3(0.16f, 0.22f, 0.04f), Window);
                        k.Box(new Vector3(x, y - 0.06f, 2.3f), new Vector3(0.9f, 0.06f, 0.45f), Sandstone.Darkened(0.1f));
                    }
                if (!razed)
                {
                    Winch(k, new Vector3(1.4f, 3.08f, 2.0f), 1);
                    lifts.Add((new Vector3(1.4f, 2.6f, 2.75f), new Vector3(1.4f, 0.05f, 2.75f)));
                }
                break;

            case "eyrie":
                // A spire of rock over the clouds, homes on its ledges and top, a basket for the long way down.
                k.Frustum(new Vector3(0, 0, 0), 2.4f, 1.3f, 7.5f, 9, Rock, new Color("7a9a52"));
                k.Blob(new Vector3(0.6f, 2f, 0.4f), new Vector3(2.4f, 2.4f, 2.2f), Rock.Darkened(0.05f), 0.3f, seed, Rock.Darkened(0.2f));
                if (!razed)
                {
                    for (int i = 0; i < size; i++)
                    {
                        float a = i * Mathf.Tau / size;
                        Cottage(k, Mathf.Cos(a) * 0.7f, 7.5f, Mathf.Sin(a) * 0.7f, -a, 0.6f, 0.55f, 0.45f, Palette.Plaster, new Color("58626f"));
                    }
                    k.Box(new Vector3(1.7f, 4.5f, 0.8f), new Vector3(1.0f, 0.1f, 1.0f), Palette.DarkTimber);
                    Cottage(k, 1.7f, 4.6f, 0.8f, 0, 0.6f, 0.55f, 0.45f, Palette.Plaster, new Color("58626f"));
                    Winch(k, new Vector3(0.9f, 7.5f, 1.0f), 1);
                    lifts.Add((new Vector3(0.9f, 7.0f, 1.75f), new Vector3(0.9f, 0.05f, 1.75f)));
                }
                break;

            case "hall":
                // A hillside with a great stone door, chimneys and a few stone houses before it.
                k.Blob(new Vector3(0, 0, -1.6f), new Vector3(4.4f, 2.8f, 3.0f), new Color("7f8a5a"), 0.25f, seed, Rock);
                k.Box(new Vector3(0, 0, 0.9f), new Vector3(1.8f, 1.8f, 0.6f), new Color("8a8478"), new Color("9a9488"));
                k.Box(new Vector3(0, 0, 1.21f), new Vector3(1.0f, 1.3f, 0.04f), razed ? new Color("1a1614") : new Color("4a3a2a"));
                if (!razed)
                {
                    k.Box(new Vector3(0, 1.0f, 1.24f), new Vector3(0.5f, 0.2f, 0.03f), new Color("ffb060", MeshKit.Flag.Ember));
                    foreach (float x in new[] { -1.4f, 1.2f }) k.Frustum(new Vector3(x, 2.2f, -1.4f), 0.2f, 0.2f, 0.9f, 6, new Color("8a8478"));
                }
                Cottages(k, size, seed, 2.6f, razed, 0, 1.2f, new Color("a49a8a"));
                break;

            default:
                // A hamlet round a green, inside a ring of stakes.
                Cottages(k, size + 4, seed, 0, razed, 0, 2.4f);
                if (!razed)
                {
                    // Its hall on the green, a well, and kitchen gardens behind the homes.
                    Cottage(k, 0, 0, -0.3f, 0, 1.3f, 0.8f, 0.7f, new Color("d8c8a8"), new Color("6e5238"));
                    k.Frustum(new Vector3(0.9f, 0, 0.9f), 0.22f, 0.22f, 0.25f, 8, new Color("9a9488"));
                    k.Box(new Vector3(0.9f, 0.25f, 0.9f), new Vector3(0.04f, 0.4f, 0.5f), Palette.DarkTimber);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * Mathf.Tau / 5 + 0.6f;
                        k.Push(MeshKit.At(Mathf.Cos(a) * 3.0f, 0.01f, Mathf.Sin(a) * 3.0f, -a));
                        k.Box(Vector3.Zero, new Vector3(0.9f, 0.05f, 0.5f), new Color("7a5a3a"));
                        for (int r = 0; r < 3; r++) k.Box(new Vector3(-0.3f + r * 0.3f, 0.05f, 0), new Vector3(0.12f, 0.08f, 0.45f), new Color("6a9a4a"));
                        k.Pop();
                    }
                }
                for (int i = 0; i < 64; i++)
                {
                    float a = i * Mathf.Tau / 64;
                    if (Mathf.Abs(Mathf.AngleDifference(a, Mathf.Pi / 2)) < 0.18f) continue;
                    var col = i % 2 == 0 ? Palette.Timber : new Color("8a6239");
                    k.Frustum(new Vector3(Mathf.Cos(a) * 3.9f, 0, Mathf.Sin(a) * 3.9f), 0.07f, 0.04f, razed && i % 3 == 0 ? 0.15f : 0.55f, 5, col, col, i);
                }
                break;
        }
        return k.Build();
    }

    /// <summary>A ring of cottages <paramref name="ring"/> tiles round a spot (a row along x when 0), burnt shells when razed.</summary>
    private static void Cottages(MeshKit k, int count, int seed, float z0, bool razed, float y = 0, float ring = 0, Color? wall = null)
    {
        for (int i = 0; i < count; i++)
        {
            float a = i * Mathf.Tau / count + Geo.Hash(seed + i) * 0.3f;
            float x = ring > 0 ? Mathf.Cos(a) * ring : -((count - 1) * 0.6f) + i * 1.2f;
            float z = ring > 0 ? Mathf.Sin(a) * ring + z0 : z0 + Geo.Hash(seed + i * 3) * 0.4f;
            float yaw = ring > 0 ? -a - Mathf.Pi / 2 : 0;
            var roof = Palette.Roofs[(seed + i) % Palette.Roofs.Length];
            if (razed)
            {
                k.Push(MeshKit.At(x, y, z, yaw));
                k.Box(Vector3.Zero, new Vector3(0.8f, 0.3f + Geo.Hash(seed + i * 5) * 0.2f, 0.7f), new Color("3a3430"), new Color("2a2420"));
                k.Pop();
            }
            else Cottage(k, x, y, z, yaw, 0.8f, 0.7f, 0.55f, wall ?? Palette.Plaster, roof);
        }
    }

    /// <summary>A cottage on (x, y, z) turned by yaw: walls, a gabled roof and a lit window.</summary>
    private static void Cottage(MeshKit k, float x, float y, float z, float yaw, float w, float d, float h, Color wall, Color roof)
    {
        k.Push(MeshKit.At(x, y, z, yaw));
        k.Box(Vector3.Zero, new Vector3(w, h, d), wall);
        k.Gable(h, w, d, h * 0.75f, roof, wall);
        k.Box(new Vector3(w * 0.2f, h * 0.35f, d / 2 + 0.01f), new Vector3(0.14f, 0.16f, 0.02f), Window);
        k.Box(new Vector3(-w * 0.2f, 0, d / 2 + 0.01f), new Vector3(0.16f, h * 0.55f, 0.02f), Palette.DarkTimber);
        k.Pop();
    }

    /// <summary>A plank walk from a to b, sagging a little, with rope rails.</summary>
    private static void PlankWalk(MeshKit k, Vector3 a, Vector3 b, Color wood)
    {
        var flat = new Vector2(b.X - a.X, b.Z - a.Z);
        float len = flat.Length(), yaw = Mathf.Atan2(flat.X, flat.Y);
        int n = Math.Max(4, (int)(len / 0.3f));
        for (int i = 0; i < n; i++)
        {
            float u = (i + 0.5f) / n;
            var p = a.Lerp(b, u) - new Vector3(0, 0.5f * 4 * u * (1 - u), 0);
            k.Push(MeshKit.At(p.X, p.Y, p.Z, yaw));
            k.Box(Vector3.Zero, new Vector3(0.5f, 0.04f, len / n * 0.8f), wood);
            k.Box(new Vector3(0.24f, 0.35f, 0), new Vector3(0.025f, 0.025f, len / n), new Color("c8b48a"));
            k.Box(new Vector3(-0.24f, 0.35f, 0), new Vector3(0.025f, 0.025f, len / n), new Color("c8b48a"));
            k.Pop();
        }
    }

    /// <summary>A length of curtain wall between two towers.</summary>
    private static void Wall(MeshKit k, Vector3 a, Vector3 b, float high, Color stone)
    {
        var flat = new Vector2(b.X - a.X, b.Z - a.Z);
        var mid = (a + b) / 2;
        k.Push(MeshKit.At(mid.X, mid.Y, mid.Z, Mathf.Atan2(flat.X, flat.Y)));
        k.Box(Vector3.Zero, new Vector3(0.3f, high, flat.Length()), stone, stone.Lightened(0.06f));
        k.Pop();
    }

    /// <summary>A winch at a cliff's lip: two posts, a beam reaching out over the drop (+z turned by <paramref name="face"/>) and a drum.</summary>
    private static void Winch(MeshKit k, Vector3 at, float face)
    {
        k.Push(MeshKit.At(at.X, at.Y, at.Z, face > 0 ? 0 : Mathf.Pi));
        foreach (float x in new[] { -0.3f, 0.3f }) k.Box(new Vector3(x, 0, 0), new Vector3(0.08f, 1.0f, 0.08f), Palette.DarkTimber);
        k.Box(new Vector3(0, 0.95f, 0.35f), new Vector3(0.1f, 0.1f, 1.0f), Palette.Timber);
        k.Box(new Vector3(0, 0.4f, 0), new Vector3(0.6f, 0.18f, 0.18f), Palette.Timber);
        k.Pop();
    }

    /// <summary>A rope from top to foot and a basket riding it.</summary>
    private void Lift(Vector3 top, Vector3 foot, string owner)
    {
        var rope = new MeshKit();
        rope.Box(new Vector3(top.X, foot.Y, top.Z), new Vector3(0.03f, top.Y - foot.Y + 0.5f, 0.03f), new Color("c8b48a"));
        var ri = new MeshInstance3D { Mesh = rope.Build(), MaterialOverride = Mats.Props };
        AddChild(ri);
        _landmarks[$"{owner}:rope:{_lifts.Count}"] = ri;
        var b = new MeshKit();
        b.Box(new Vector3(0, -0.45f, 0), new Vector3(0.5f, 0.35f, 0.5f), new Color("a07a48"), new Color("6a4a2a"));
        b.Box(new Vector3(0, -0.1f, 0), new Vector3(0.03f, 0.1f, 0.03f), Palette.DarkTimber);
        var bi = new MeshInstance3D { Mesh = b.Build(), MaterialOverride = Mats.Props, Position = foot };
        AddChild(bi);
        _landmarks[$"{owner}:basket:{_lifts.Count}"] = bi;
        _lifts.Add((bi, top, foot + new Vector3(0, 0.45f, 0), Geo.Hash(_lifts.Count * 13 + 5)));
    }

    // ---------- The town's own ground ----------

    /// <summary>Stake fences round the farms that stand outside the walls (all of them, before any wall is drawn).</summary>
    private void FarmFences(Town town)
    {
        var line = WallLine(town);
        var farms = town.Buildings.Where(b => b.Def.Zone == Zone.Farmland && b.Complete && b.ParentId == null)
            .Where(b => line is not { Length: > 2 } || !InsideLine(line, Geo.LotPos(town.Lot(b.LotId)))).ToList();
        string key = $"{string.Join(',', farms.Select(b => b.Id))}|{town.City.WallVersion}";
        if (key == _farmKey) return;
        _farmKey = key;
        var k = new MeshKit();
        foreach (var b in farms)
        {
            var l = town.Lot(b.LotId);
            var c = Geo.LotCorners(l, 0.25f);
            var gate = (c[2] + c[3]) / 2;
            for (int e = 0; e < 4; e++)
            {
                var a = c[e];
                var z = c[(e + 1) % 4];
                int n = Math.Max(2, (int)(a.DistanceTo(z) / 0.16f));
                for (int i = 0; i < n; i++)
                {
                    var p = a.Lerp(z, (float)i / n);
                    // A gap at the front for the cart.
                    if (p.DistanceTo(gate) < 0.4f) continue;
                    var col = (i + e) % 2 == 0 ? Palette.Timber : new Color("8a6239");
                    k.Frustum(new Vector3(p.X, Ground(p) - 0.05f, p.Y), 0.04f, 0.025f, 0.28f + Geo.Hash(b.Id * 97 + e * 31 + i) * 0.05f, 5, col, col, i);
                }
            }
        }
        _farmFences.Mesh = k.Triangles > 0 ? k.Build() : null;
    }

    private static bool InsideLine(Vector2[] line, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = line.Length - 1; i < line.Length; j = i++)
            if ((line[i].Y > p.Y) != (line[j].Y > p.Y) && p.X < (line[j].X - line[i].X) * (p.Y - line[i].Y) / (line[j].Y - line[i].Y) + line[i].X)
                inside = !inside;
        return inside;
    }

    /// <summary>A flat soft sheet for the veil shader (UVs 0..1 across it).</summary>
    private static void Sheet(SurfaceTool st, Vector3 c, Vector3 u, Vector3 v, Color col)
    {
        Vector3[] p = [c - u - v, c + u - v, c + u + v, c - u + v];
        Vector2[] uv = [new(0, 0), new(1, 0), new(1, 1), new(0, 1)];
        foreach (int i in new[] { 0, 1, 2, 0, 2, 3 })
        {
            st.SetColor(col);
            st.SetUV(uv[i]);
            st.SetNormal(Vector3.Up);
            st.AddVertex(p[i]);
        }
    }

    /// <summary>Mist banks lying on the water: over the sea off a town on the coast, and along the river near town.</summary>
    private void Mists(Town town)
    {
        var gen = map.Gen;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var tint = new Color("dfe6ea");
        int sheets = 0;
        if (town.Terrain.FirstOrDefault(f => f.Kind.Boats) is { } sea)
        {
            double b0 = sea.Bearing * Math.PI / 180;
            for (int i = 0; i < 36; i++)
            {
                double b = b0 + (Geo.Hash(i * 3 + 1) - 0.5) * sea.Arc * Math.PI / 180 * 0.9;
                double d = sea.Inner + Geo.Hash(i * 3 + 2) * (sea.Outer - sea.Inner) * 0.7;
                var at = Geo.FromDays(d * Math.Sin(b), -d * Math.Cos(b));
                if (gen.HeightAt(at.X, at.Y) > WorldGen.WaterLevel + 0.4f) continue;
                float size = 10 + Geo.Hash(i * 3 + 3) * 14;
                Sheet(st, new Vector3(at.X, 0.4f + (i % 2) * 0.6f, at.Y), new Vector3(size, 0, 0), new Vector3(0, 0, size * 0.7f), tint);
                sheets++;
            }
        }
        var river = gen.River;
        for (int i = 0; i < river.Length; i += 6)
        {
            var p = river[i];
            if (p.DistanceTo(Geo.Center) > 220) continue;
            float size = 7 + Geo.Hash(i * 5 + 7) * 6;
            Sheet(st, new Vector3(p.X, gen.SurfaceAt(p.X, p.Y) + 0.35f + (i % 2) * 0.3f, p.Y), new Vector3(size, 0, 0), new Vector3(0, 0, size * 0.8f), new Color(tint, 0.8f));
            sheets++;
        }
        if (sheets > 0) _mists.Mesh = st.Commit();
    }

    /// <summary>
    /// Waterfalls: one off each plateau in the near wilds (<see cref="Cliffs"/>), where its stream finds the edge away from
    /// the ramps, and one down a cliff-top town's own face (<see cref="WorldView.SiteFaces"/>), with a white pool at the foot.
    /// </summary>
    private void Waterfalls(Town town)
    {
        var gen = map.Gen;
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        var pools = new MeshKit();
        var water = new Color("7fb0c8");
        void Fall(Vector2 lip, Vector2 foot, Vector2 dir)
        {
            float top = gen.HeightAt(lip.X, lip.Y), low = gen.SurfaceAt(foot.X, foot.Y);
            if (top - low < 1.5f) return;
            var side = new Vector3(-dir.Y, 0, dir.X) * 0.8f;
            var a = new Vector3(lip.X, top + 0.05f, lip.Y);
            var b = new Vector3(foot.X, low, foot.Y);
            // The falling sheet, the lip at UV.y 0; and the stream running to the lip over the top.
            Sheet(st, (a + b) / 2, side, (b - a) / 2, water);
            var back = lip - dir * 2.2f;
            Sheet(st, new Vector3((lip.X + back.X) / 2, top + 0.06f, (lip.Y + back.Y) / 2), side * 0.6f, new Vector3(lip.X - back.X, 0, lip.Y - back.Y) / 2, new Color(water, 0.8f));
            int s = (int)foot.X * 7 + (int)foot.Y;
            pools.Blob(new Vector3(foot.X, low - 0.12f, foot.Y), new Vector3(1.5f, 0.16f, 1.2f), new Color("9cc6d8"), 0.35f, s, new Color("7fb0c8"));
            pools.Blob(new Vector3(foot.X - dir.X * 0.2f, low - 0.02f, foot.Y - dir.Y * 0.2f), new Vector3(0.8f, 0.14f, 0.6f), new Color("eef6fa"), 0.4f, s + 1);
        }
        var rules = town.Content.Passage.Cliffs;
        foreach (var p in Cliffs.Of(town))
        {
            // Away from the ramps: the angle furthest from every ramp.
            double best = 0, bestGap = -1;
            for (int i = 0; i < 16; i++)
            {
                double a = i * Math.PI / 8 + p.Id * 0.37;
                double gap = p.Ramps.Length == 0 ? 1 : p.Ramps.Min(r => Math.Abs(Math.IEEERemainder(a - r, Math.PI * 2)));
                if (gap > bestGap) (best, bestGap) = (a, gap);
            }
            var dir = new Vector2((float)Math.Cos(best), (float)Math.Sin(best));
            double e = p.EdgeAt(best);
            var lip = new Vector2((float)(p.X + dir.X * (e - 0.4)) + Geo.Margin, (float)(p.Y + dir.Y * (e - 0.4)) + Geo.Margin);
            var foot = new Vector2((float)(p.X + dir.X * (e + rules.Face + 0.9)) + Geo.Margin, (float)(p.Y + dir.Y * (e + rules.Face + 0.9)) + Geo.Margin);
            Fall(lip, foot, dir);
        }
        // Down the town's own cliff: its tallest face, away from the river.
        if (map.Lands.SiteFaces.Where(f => gen.RiverDistance(f.Top) > 14).OrderByDescending(f => f.High).FirstOrDefault() is { High: > 0 } face)
            Fall(face.Top - face.Dir * 0.3f, face.Foot + face.Dir * 0.9f, face.Dir);
        _falls.Mesh = st.Commit();
        if (pools.Triangles > 0)
        {
            var mi = new MeshInstance3D { Mesh = pools.Build(), MaterialOverride = Mats.Props };
            AddChild(mi);
            _landmarks["falls:pools"] = mi;
        }
    }

    /// <summary>Lift baskets up a cliff-top town's face: up to three winches on its tallest faces, well apart.</summary>
    private void CliffLifts(Town town)
    {
        var picked = new List<WorldView.Face>();
        foreach (var f in map.Lands.SiteFaces.OrderByDescending(f => f.High))
        {
            if (picked.Count == 3) break;
            if (f.High < 2.5f || picked.Any(p => p.Dir.AngleTo(f.Dir) is var a && Mathf.Abs(a) < 0.9f)) continue;
            if (map.Gen.RiverDistance(f.Top) < 10) continue;
            picked.Add(f);
        }
        int i = 0;
        foreach (var f in picked)
        {
            var k = new MeshKit();
            float top = map.Gen.HeightAt(f.Top.X, f.Top.Y);
            var at = f.Top - f.Dir * 0.6f;
            Winch(k, Vector3.Zero, 1);
            var mi = new MeshInstance3D { Mesh = k.Build(), MaterialOverride = Mats.Props, Position = new Vector3(at.X, top, at.Y), Rotation = new Vector3(0, Mathf.Atan2(f.Dir.X, f.Dir.Y), 0) };
            AddChild(mi);
            _landmarks[$"lift:{i}"] = mi;
            var over = at + f.Dir * 0.75f;
            var foot = f.Foot + f.Dir * 0.3f;
            Lift(new Vector3(over.X, top + 0.5f, over.Y), new Vector3(over.X, map.Gen.SurfaceAt(foot.X, foot.Y) + 0.05f, over.Y), $"lift:{i}");
            i++;
        }
    }

    /// <summary>
    /// The town's bridge ward: once the town is chartered a town, narrow tall houses line its main bridge on both sides,
    /// shops below and homes above, leaving the carriageway between.
    /// </summary>
    private static void BridgeHouses(MeshKit k, float deck)
    {
        for (int i = 0; i < 6; i++)
            foreach (float side in new[] { -1f, 1f })
            {
                float z = -3.1f + i * 1.24f;
                var roof = Palette.Roofs[(i * 2 + (side > 0 ? 1 : 0)) % Palette.Roofs.Length];
                k.Push(MeshKit.At(side * 0.85f, deck, z, side > 0 ? -Mathf.Pi / 2 : Mathf.Pi / 2));
                // Jettied over the water: the upper storey reaches out past the deck.
                k.Box(Vector3.Zero, new Vector3(1.0f, 0.5f, 0.55f), Palette.Plaster);
                k.Box(new Vector3(0, 0.5f, -0.08f), new Vector3(1.06f, 0.55f, 0.75f), new Color("e4d4b4"));
                k.Box(new Vector3(0, 0.5f, -0.46f), new Vector3(1.06f, 0.06f, 0.04f), Palette.DarkTimber);
                k.Push(MeshKit.At(0, 0, -0.08f, Mathf.Pi / 2));
                k.Gable(1.05f, 0.75f, 1.06f, 0.5f, roof, new Color("e4d4b4"));
                k.Pop();
                k.Box(new Vector3(0.2f, 0.7f, 0.3f), new Vector3(0.14f, 0.16f, 0.02f), Window);
                k.Box(new Vector3(-0.2f, 0.05f, 0.29f), new Vector3(0.36f, 0.3f, 0.02f), new Color("6a4a2a"));
                k.Pop();
            }
    }

    /// <summary>Snow falling round where the camera looks, as thick as winter is deep.</summary>
    private void Snowfall(float winter)
    {
        if (_snowfall == null)
        {
            if (winter < 0.05f) return;
            var flake = new QuadMesh { Size = new Vector2(0.09f, 0.09f) };
            _snowfall = new CpuParticles3D
            {
                Amount = 1800,
                Lifetime = 9,
                Preprocess = 9,
                Mesh = flake,
                EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
                EmissionBoxExtents = new Vector3(40, 1, 40),
                Direction = new Vector3(0.15f, -1, 0.05f),
                Spread = 8,
                Gravity = Vector3.Zero,
                InitialVelocityMin = 1.6f,
                InitialVelocityMax = 2.4f,
                LocalCoords = false,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
                MaterialOverride = new StandardMaterial3D
                {
                    ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                    BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
                    AlbedoColor = new Color(1, 1, 1, 0.85f),
                    Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                },
            };
            AddChild(_snowfall);
        }
        _snowfall.Emitting = winter > 0.05f && !map.Strategic;
        ((StandardMaterial3D)_snowfall.MaterialOverride).AlbedoColor = new Color(1, 1, 1, 0.85f * Mathf.Clamp(winter, 0.2f, 1));
        var look = map.Target;
        _snowfall.GlobalPosition = new Vector3(look.X, map.Gen.SurfaceAt(look.X, look.Y) + 16, look.Y);
    }
}
