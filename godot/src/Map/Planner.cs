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
    /// <summary>A zone to paint (Zone.None clears land) at a density (homes and shops only), or a building to place, by content id.</summary>
    public sealed record PlanTool(Zone? Zone, string? Building, int Density = 0);

    public PlanTool? Tool { get; private set; }

    /// <summary>The plan changed (land zoned, a building placed, or the tool put down).</summary>
    public event Action? Changed;

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
            if (map.Sim.PaintZone(_stroke.Select(p => ((double)p.X, (double)p.Y)), Radius, z, Tool.Density) > 0) Changed?.Invoke();
            _stroke.Clear();
        }
        else if (Tool is { Building: string id } && _press is Vector2 at)
        {
            _press = null;
            if (map.Sim.Place(id, at.X, at.Y, _yaw) != null)
            {
                // One commission per pick: the next costs more, and the bar shows why if it can't be had.
                Pick(null);
                return;
            }
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
            var c = z == Zone.None ? new Color(0.95f, 0.9f, 0.8f) : ZoneColor(z, Tool.Density);
            // The stroke so far, as the brush will leave it, then the brush ring at the cursor.
            foreach (var p in _stroke) Disc(p, Radius, new Color(c, 0.18f));
            Ring(h, Radius, new Color(c, 0.95f), 2.5f);
            Ring(h, Radius, new Color(0, 0, 0, 0.45f), 1f, 1.04f);
        }
        else if (Tool.Building is string id)
        {
            var def = town.Content.Building(id);
            var at = _press ?? h;
            double yaw = _yaw ?? Streets.Facing(town, at.X, at.Y);
            string? why = Planning.Blocked(town, def) ?? Planning.SpotBlocked(town, def, at.X, at.Y, yaw);
            var (w, d) = Land.Footprint(def);
            var c = why == null ? ZoneColor(Planning.ZoneFor(town, def)) : new Color(0.9f, 0.3f, 0.25f);
            Footprint(at, w / (float)Land.Res, d / (float)Land.Res, (float)yaw, new Color(c, 0.55f), c);
            // An arrow from the middle to the front shows which way it will face.
            var front = at + new Vector2(-Mathf.Sin((float)yaw), Mathf.Cos((float)yaw)) * (d / (float)Land.Res * 0.5f + 0.25f);
            DrawLine(Screen(at), Screen(front), new Color("f6eedb"), 2.5f, true);
            var label = why == null ? def.Name : Ui.Cap(why);
            var s = map.ToScreen(FromTown(at.X, at.Y), 0.7f);
            var lw = Ui.Bold.GetStringSize(label, HorizontalAlignment.Left, -1, 13).X;
            DrawStringOutline(Ui.Bold, s - new Vector2(lw / 2, 0), label, HorizontalAlignment.Left, -1, 13, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
            DrawString(Ui.Bold, s - new Vector2(lw / 2, 0), label, HorizontalAlignment.Left, -1, 13, new Color("f6eedb"));
        }
    }

    /// <summary>Ward borders, closed wards shaded, and each ward's name at its middle: grey with the charter it waits for until it is laid out.</summary>
    private void Wards(Town town)
    {
        var border = new Color("f6eedb", 0.55f);
        var shade = new Color(0.05f, 0.04f, 0.03f, 0.38f);
        var sums = new Dictionary<string, (float X, float Y, int N)>();
        var wards = new WardDef[Geo.Grid * Geo.Grid];
        for (int y = 0; y < Geo.Grid; y++)
            for (int x = 0; x < Geo.Grid; x++)
                wards[y * Geo.Grid + x] = City.WardOf(town, x + 0.5, y + 0.5);
        for (int y = 0; y < Geo.Grid; y++)
        {
            int run = -1;
            for (int x = 0; x <= Geo.Grid; x++)
            {
                // Closed land shaded a row run at a time.
                bool closed = x < Geo.Grid && !town.City.OpenWards.Contains(wards[y * Geo.Grid + x].Id);
                if (closed && run < 0) run = x;
                if (!closed && run >= 0)
                {
                    DrawColoredPolygon([Screen(new(run, y)), Screen(new(x, y)), Screen(new(x, y + 1)), Screen(new(run, y + 1))], shade);
                    run = -1;
                }
                if (x == Geo.Grid) break;
                var w = wards[y * Geo.Grid + x];
                var (sx, sy, n) = sums.GetValueOrDefault(w.Id);
                sums[w.Id] = (sx + x + 0.5f, sy + y + 0.5f, n + 1);
                if (x + 1 < Geo.Grid && !ReferenceEquals(wards[y * Geo.Grid + x + 1], w)) DrawLine(Screen(new(x + 1, y)), Screen(new(x + 1, y + 1)), border, 2, true);
                if (y + 1 < Geo.Grid && !ReferenceEquals(wards[(y + 1) * Geo.Grid + x], w)) DrawLine(Screen(new(x, y + 1)), Screen(new(x + 1, y + 1)), border, 2, true);
            }
        }
        foreach (var w in town.Content.City.Wards)
        {
            if (!sums.TryGetValue(w.Id, out var c) || c.N == 0) continue;
            bool open = town.City.OpenWards.Contains(w.Id);
            string sub = open ? "" : City.Reached(town, w.Tier) ? $"ready to lay out · {w.Cost:N0}g" : $"needs a {City.TierName(town, w.Tier).ToLowerInvariant()}";
            var at = map.ToScreen(FromTown(c.X / c.N, c.Y / c.N), 0.6f);
            Label(at, w.Name.ToUpperInvariant(), 13, open ? new Color("f6eedb") : new Color("cfc6b4", 0.8f));
            if (sub != "") Label(at + new Vector2(0, 16), sub, 11, new Color("cfc6b4", 0.8f));
        }
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

    private void Footprint(Vector2 c, float w, float d, float yaw, Color fill, Color edge)
    {
        float s = Mathf.Sin(yaw), co = Mathf.Cos(yaw);
        // Width runs along (cos, sin), depth along the front (−sin, cos).
        var u = new Vector2(co, s) * (w / 2);
        var v = new Vector2(-s, co) * (d / 2);
        Vector2[] p = [Screen(c - u - v), Screen(c + u - v), Screen(c + u + v), Screen(c - u + v)];
        DrawColoredPolygon(p, fill);
        DrawPolyline([.. p, p[0]], edge, 2, true);
    }
}
