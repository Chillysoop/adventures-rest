using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town plan while a build tool is picked, after Foundation: zones are painted with a round brush
/// (drag to paint, [ and ] or Shift+wheel to size it), and the townsfolk site their own buildings inside
/// the paint. A building tool shows its footprint under the cursor: click to place it facing the nearest lane,
/// or press and drag to turn it to face any way (Shift+wheel turns it too). Right-click puts the tool down.
/// The wards are outlined and named; land in a ward not yet laid out is shaded and can't be painted.
/// The paint itself is drawn on the ground by <see cref="TownView"/>; this draws the wards, the brush and the ghost.
/// </summary>
public partial class Planner(MapView map) : Control
{
    /// <summary>A zone to paint (Zone.None clears land) at a density (homes and shops only) or sown with a crop (farmland), or a building to place, by content id.</summary>
    public sealed record PlanTool(Zone? Zone, string? Building, int Density = 0, string? Crop = null);

    public PlanTool? Tool { get; private set; }

    /// <summary>The plan changed (land zoned, a building placed, or the tool put down).</summary>
    public event Action? Changed;

    /// <summary>A placement went through or didn't, in a line for the player.</summary>
    public event Action<string>? Said;

    /// <summary>Brush radius in tiles; kept between picks.</summary>
    public float Radius { get; private set; } = 1.2f;

    /// <summary>The stroke being painted, in town tiles, not yet sent to the sim.</summary>
    private readonly List<Vector2> _stroke = [];

    private Vector2? _hover, _press;

    /// <summary>The heading a building ghost is turned to, or null to face the nearest lane.</summary>
    private float? _yaw;

    public bool Active => Tool != null;

    public void Pick(PlanTool? tool)
    {
        Tool = tool;
        _stroke.Clear();
        _press = null;
        _yaw = null;
        QueueRedraw();
        Changed?.Invoke();
    }

    /// <summary>A zone's paint, deeper and stronger the denser the land is zoned.</summary>
    public static Color ZoneColor(Zone z, int density)
    {
        var c = ZoneColor(z);
        if (density <= 0) return c;
        c.ToHsv(out float h, out float s, out float v);
        return Color.FromHsv(h, Mathf.Min(1, s * (1 + 0.28f * density)), v * (1 - 0.2f * density));
    }

    /// <summary>A field's paint: its crop's colour (content/game/farming.json), or farmland's for land sown with any.</summary>
    public static Color CropColor(CropDef? crop) => crop is { Color: not "" } ? new Color(crop.Color) : ZoneColor(Zone.Farmland);

    /// <summary>The paint of a tool: a crop's colour for fields.</summary>
    public static Color ToolColor(Town town, PlanTool t) =>
        t.Zone == Zone.Farmland && t.Crop != null ? CropColor(Farming.Crop(town, t.Crop)) : ZoneColor(t.Zone ?? Zone.None, t.Density);

    public static Color ZoneColor(Zone z) => z switch
    {
        Zone.Residential => new Color("7fbf5f"),
        Zone.Commercial => new Color("4f9fd8"),
        Zone.Industry => new Color("c8643c"),
        Zone.Farmland => new Color("c8b440"),
        Zone.Civic => new Color("c47adb"),
        _ => new Color("d8d0c0"),
    };

    /// <summary>A world point in town tiles (the sim's frame: tiles from the town's corner).</summary>
    public static Vector2 ToTown(Vector2 w) => w - new Vector2(Geo.Margin, Geo.Margin);

    public static Vector2 FromTown(double x, double y) => new(Geo.Margin + (float)x, Geo.Margin + (float)y);

    // ---------- Input, routed here by the map while a tool is picked ----------

    public void Hover(Vector2 w)
    {
        _hover = ToTown(w);
        if (Tool?.Zone != null && _stroke.Count > 0) Dab(_hover.Value);
        if (Tool?.Building != null && _press is Vector2 p && p.DistanceTo(_hover.Value) > 0.3f)
            _yaw = Mathf.Atan2(-(_hover.Value.X - p.X), _hover.Value.Y - p.Y);
        QueueRedraw();
    }

    public void Press(Vector2 w)
    {
        _hover = ToTown(w);
        if (Tool?.Zone != null)
        {
            _stroke.Clear();
            _stroke.Add(_hover.Value);
        }
        else if (Tool?.Building != null)
        {
            _press = _hover;
            _yaw = null;
        }
        QueueRedraw();
    }

    /// <summary>Dabs a quarter of the brush apart along the drag, so a fast stroke leaves no gaps.</summary>
    private void Dab(Vector2 p)
    {
        var last = _stroke[^1];
        float step = Mathf.Max(0.1f, Radius * 0.25f), d = last.DistanceTo(p);
        for (float t = step; t < d; t += step) _stroke.Add(last.Lerp(p, t / d));
        _stroke.Add(p);
    }

    public void Release(Vector2 w)
    {
        var town = map.Sim.Town;
        if (Tool is { Zone: Zone z } && _stroke.Count > 0)
        {
            Dab(ToTown(w));
            if (map.Sim.PaintZone(_stroke.Select(p => ((double)p.X, (double)p.Y)), Radius, z, Tool.Density, Tool.Crop) > 0) Changed?.Invoke();
            _stroke.Clear();
        }
        else if (Tool is { Building: string id } && _press is Vector2 at)
        {
            _press = null;
            var def = town.Content.Building(id);
            string? why = Planning.Blocked(town, def) ?? Planning.SpotBlocked(town, def, at.X, at.Y, _yaw);
            if (map.Sim.Place(id, at.X, at.Y, _yaw) is { } site)
            {
                Said?.Invoke($"Builders are raising {site.Name}.");
                // One commission per pick: the next costs more, and the bar shows why if it can't be had.
                Pick(null);
                return;
            }
            Said?.Invoke($"The {def.Name.ToLowerInvariant()} can't go there: {why ?? "no room here"}.");
            _yaw = null;
        }
        QueueRedraw();
    }

    /// <summary>Shift+wheel sizes the brush or turns the ghost; [ and ] size the brush. Returns whether it was used.</summary>
    public bool Wheel(bool up)
    {
        if (Tool?.Zone != null) Resize(up ? 1.15f : 1 / 1.15f);
        else if (Tool?.Building != null && _hover is Vector2 h)
            _yaw = (_yaw ?? (float)Streets.Facing(map.Sim!.Town, h.X, h.Y)) + (up ? 1 : -1) * Mathf.Pi / 12;
        else return false;
        QueueRedraw();
        return true;
    }

    public void Resize(float by)
    {
        Radius = Mathf.Clamp(Radius * by, 0.3f, (float)Planning.MaxBrush);
        QueueRedraw();
    }

    // ---------- Drawing ----------

    public override void _Draw()
    {
        if (Tool == null || map.Sim == null || _hover is not Vector2 h) return;
        var town = map.Sim.Town;
        Wards(town);
        if (Tool.Zone is Zone z)
        {
            var c = z == Zone.None ? new Color(0.95f, 0.9f, 0.8f) : ToolColor(town, Tool);
            // Where the zone's buildings will go on the land already painted, faintly, so a stroke's worth shows at once.
            if (z != Zone.None)
                foreach (var l in PlotsOf(town, z))
                    Footprint(new Vector2((float)l.X, (float)l.Y), l.W / (float)Land.Res, l.D / (float)Land.Res, (float)l.Yaw, new Color(c, 0.12f), new Color(c, 0.6f), 1);
            // The stroke so far, as the brush will leave it, then the brush ring at the cursor.
            foreach (var p in _stroke) Disc(p, Radius, new Color(c, 0.18f));
            Ring(h, Radius, new Color(c, 0.95f), 2.5f);
            Ring(h, Radius, new Color(0, 0, 0, 0.45f), 1f, 1.04f);
        }
        else if (Tool.Building is string id)
        {
            var def = town.Content.Building(id);
            Ground(town, def);
            var at = _press ?? h;
            string? why = Planning.Blocked(town, def);
            // The ghost settles where a click would put it: the spot itself, or the nearest that fits.
            var lot = why == null ? Planning.Spot(town, def, at.X, at.Y, _yaw) : null;
            if (why == null && lot == null) why = Planning.SpotBlocked(town, def, at.X, at.Y, _yaw);
            var spot = lot != null ? new Vector2((float)lot.X, (float)lot.Y) : at;
            double yaw = lot?.Yaw ?? _yaw ?? Streets.Facing(town, at.X, at.Y);
            var (w, d) = Land.Footprint(def);
            var c = why == null ? ZoneColor(Planning.ZoneFor(town, def)) : new Color(0.9f, 0.3f, 0.25f);
            if (spot.DistanceTo(at) > 0.05f) DrawLine(Screen(at), Screen(spot), new Color(c, 0.8f), 1.5f, true);
            Footprint(spot, w / (float)Land.Res, d / (float)Land.Res, (float)yaw, new Color(c, 0.55f), c);
            // An arrow from the middle to the front shows which way it will face.
            var front = spot + new Vector2(-Mathf.Sin((float)yaw), Mathf.Cos((float)yaw)) * (d / (float)Land.Res * 0.5f + 0.25f);
            DrawLine(Screen(spot), Screen(front), new Color("f6eedb"), 2.5f, true);
            var label = why == null ? def.Name : Ui.Cap(why);
            var s = map.ToScreen(FromTown(spot.X, spot.Y), 0.7f);
            var lw = Ui.Bold.GetStringSize(label, HorizontalAlignment.Left, -1, 13).X;
            DrawStringOutline(Ui.Bold, s - new Vector2(lw / 2, 0), label, HorizontalAlignment.Left, -1, 13, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
            DrawString(Ui.Bold, s - new Vector2(lw / 2, 0), label, HorizontalAlignment.Left, -1, 13, new Color("f6eedb"));
        }
    }

    private (Zone Zone, int Version, List<Lot> Lots)? _plots;

    /// <summary>Where a zone's buildings would go, worked out again only when the plan changes.</summary>
    private List<Lot> PlotsOf(Town town, Zone z)
    {
        if (_plots is { } p && p.Zone == z && p.Version == town.Plan.Version) return p.Lots;
        var lots = Land.Plots(town, z, 120);
        _plots = (z, town.Plan.Version, lots);
        return lots;
    }

    /// <summary>Half a tile per sample: whether the building fits there facing the nearest lane.</summary>
    private const float Sample = 0.5f;

    private (string Id, int Version, int Wards, bool[] Fits)? _ground;

    /// <summary>The ground that takes the building, shaded green a row run at a time; worked out again only when the plan changes.</summary>
    private void Ground(Town town, BuildingDef def)
    {
        int n = (int)(Town.Grid / Sample);
        if (_ground is not { } g || g.Id != def.Id || g.Version != town.Plan.Version || g.Wards != town.City.OpenWards.Count)
        {
            var fits = new bool[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    fits[y * n + x] = Land.At(town, def, (x + 0.5) * Sample, (y + 0.5) * Sample) != null;
            _ground = g = (def.Id, town.Plan.Version, town.City.OpenWards.Count, fits);
        }
        var green = new Color(0.45f, 0.9f, 0.4f, 0.13f);
        for (int y = 0; y < n; y++)
        {
            int run = -1;
            for (int x = 0; x <= n; x++)
            {
                bool ok = x < n && g.Fits[y * n + x];
                if (ok && run < 0) run = x;
                if (!ok && run >= 0)
                {
                    DrawColoredPolygon([Screen(new(run * Sample, y * Sample)), Screen(new(x * Sample, y * Sample)), Screen(new(x * Sample, (y + 1) * Sample)), Screen(new(run * Sample, (y + 1) * Sample))], green);
                    run = -1;
                }
            }
        }
    }

    /// <summary>Ward borders, closed wards shaded, and each ward's name at its middle: grey with the charter it waits for until it is laid out.</summary>
    private void Wards(Town town)
    {
        var border = new Color("f6eedb", 0.55f);
        var shade = new Color(0.05f, 0.04f, 0.03f, 0.38f);
        double c = Town.Center;
        // The borders are curves (City.Edge, City.Split), drawn from them rather than from cells so they never stair-step.
        Vector2 At(double a, double r) => new((float)(c + Math.Cos(a) * r), (float)(c + Math.Sin(a) * r));
        // The town's square edge along a heading: where the outer ring ends.
        double Rim(double a) => c / Math.Max(Math.Abs(Math.Cos(a)), Math.Abs(Math.Sin(a)));
        double Inner(int ring, double a) => ring == 0 ? 0 : City.Edge(town, ring - 1, a);
        double Outer(int ring, double a) => ring == 2 ? Rim(a) : City.Edge(town, ring, a);
        const int Steps = 24;
        foreach (var w in town.Content.City.Wards)
        {
            bool open = town.City.OpenWards.Contains(w.Id);
            var poly = new List<Vector2>();
            Vector2 mid;
            if (w.Ring == 0)
            {
                for (int i = 0; i < Steps * 4; i++) poly.Add(At(i * Math.PI * 2 / (Steps * 4), City.Edge(town, 0, i * Math.PI * 2 / (Steps * 4))));
                mid = new Vector2((float)c, (float)c);
            }
            else
            {
                int j = Array.IndexOf(Sides, w.Side);
                if (j < 0) continue;
                // A side lies between border j and border j + 1, round from east|south (City.Side); east wraps past a full turn.
                double SplitAt(int i, double r) => City.Split(town, i % 4, r) + (i >= 4 ? Math.PI * 2 : 0);
                // Where a border crosses a ring's edge (the edge's own wobble moves it a little: twice round settles it).
                double Cross(int i, Func<double, double> edge) => SplitAt(i, edge(SplitAt(i, edge(SplitAt(i, 0)))));
                Func<double, double> inner = a => Inner(w.Ring, a), outer = a => Outer(w.Ring, a);
                double a0 = Cross(j, inner), a1 = Cross(j + 1, inner), b0 = Cross(j, outer), b1 = Cross(j + 1, outer);
                for (int i = 0; i <= Steps; i++)
                {
                    double a = a0 + (a1 - a0) * i / Steps;
                    poly.Add(At(a, inner(a)));
                }
                // Out along the far border, back round the outer edge, and in along the near one.
                for (int i = 1; i < 6; i++)
                {
                    double r = inner(a1) + (outer(b1) - inner(a1)) * i / 6;
                    poly.Add(At(SplitAt(j + 1, r), r));
                }
                for (int i = Steps; i >= 0; i--)
                {
                    double a = b0 + (b1 - b0) * i / Steps;
                    poly.Add(At(a, outer(a)));
                }
                for (int i = 5; i >= 1; i--)
                {
                    double r = inner(a0) + (outer(b0) - inner(a0)) * i / 6;
                    poly.Add(At(SplitAt(j, r), r));
                }
                double am = (a0 + a1) / 2, rm = (Inner(w.Ring, am) + Outer(w.Ring, am)) / 2;
                mid = At(am, rm);
            }
            var screen = poly.Select(Screen).ToArray();
            // Closed land shaded a strip of small quads at a time: a whole curved ward seen in perspective can defeat triangulation.
            if (!open && w.Ring > 0)
                for (int i = 0; i < Steps; i++)
                {
                    // Inner points run 0..Steps; the outer edge runs back from Steps + 6 (see the order above).
                    int o = Steps + 6 + Steps - i;
                    Vector2[] quad = [screen[i], screen[i + 1], screen[o - 1], screen[o]];
                    // Past the horizon the projection folds a quad over or squeezes it flat: nothing to shade there, and Godot can't triangulate it.
                    if (Convex(quad)) DrawColoredPolygon(quad, shade);
                }
            DrawPolyline([.. screen, screen[0]], border, 2, true);
            string sub = open ? "" : City.Reached(town, w.Tier) ? $"ready to lay out · {w.Cost:N0}g" : $"needs a {City.TierName(town, w.Tier).ToLowerInvariant()}";
            var at = map.ToScreen(FromTown(mid.X, mid.Y), 0.6f);
            Label(at, w.Name.ToUpperInvariant(), 13, open ? new Color("f6eedb") : new Color("cfc6b4", 0.8f));
            if (sub != "") Label(at + new Vector2(0, 16), sub, 11, new Color("cfc6b4", 0.8f));
        }
    }

    private static readonly string[] Sides = ["south", "west", "north", "east"];

    /// <summary>Whether a quad on screen turns the same way at every corner, with some area to it.</summary>
    private static bool Convex(Vector2[] q)
    {
        int sign = 0;
        for (int i = 0; i < q.Length; i++)
        {
            float cross = (q[(i + 1) % q.Length] - q[i]).Cross(q[(i + 2) % q.Length] - q[(i + 1) % q.Length]);
            if (!float.IsFinite(cross) || Mathf.Abs(cross) < 0.5f) return false;
            int s = Math.Sign(cross);
            if (sign != 0 && s != sign) return false;
            sign = s;
        }
        return true;
    }

    private void Label(Vector2 at, string text, int size, Color c)
    {
        var w = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;
        DrawStringOutline(Ui.Bold, at - new Vector2(w / 2, 0), text, HorizontalAlignment.Left, -1, size, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
        DrawString(Ui.Bold, at - new Vector2(w / 2, 0), text, HorizontalAlignment.Left, -1, size, c);
    }

    private Vector2 Screen(Vector2 town) => map.ToScreen(FromTown(town.X, town.Y), 0.06f);

    private Vector2[] Circle(Vector2 c, float r, int n = 40)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            pts[i] = Screen(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        return pts;
    }

    private void Disc(Vector2 c, float r, Color col) => DrawColoredPolygon(Circle(c, r, 24), col);

    private void Ring(Vector2 c, float r, Color col, float width, float scale = 1)
    {
        var p = Circle(c, r * scale);
        DrawPolyline([.. p, p[0]], col, width, true);
    }

    private void Footprint(Vector2 c, float w, float d, float yaw, Color fill, Color edge, float width = 2)
    {
        float s = Mathf.Sin(yaw), co = Mathf.Cos(yaw);
        // Width runs along (cos, sin), depth along the front (−sin, cos).
        var u = new Vector2(co, s) * (w / 2);
        var v = new Vector2(-s, co) * (d / 2);
        Vector2[] p = [Screen(c - u - v), Screen(c + u - v), Screen(c + u + v), Screen(c - u + v)];
        DrawColoredPolygon(p, fill);
        DrawPolyline([.. p, p[0]], edge, width, true);
    }
}
