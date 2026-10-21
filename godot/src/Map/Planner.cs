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
    public sealed record PlanTool(Zone? Zone, string? Building, int Density = 0, string? Crop = null)
    {
        /// <summary>A wall's line to draw corner by corner (its ring), or a gate to snap onto it; the work to start once it has a gate.</summary>
        public int? Wall { get; init; }
        public int? Gate { get; init; }
        public DefenseWork? Work { get; init; }
    }

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

    /// <summary>The corners of the wall being drawn, in town tiles.</summary>
    private readonly List<Vector2> _corners = [];

    /// <summary>The heading a building ghost is turned to, or null to face the nearest lane.</summary>
    private float? _yaw;

    public bool Active => Tool != null;

    public void Pick(PlanTool? tool)
    {
        Tool = tool;
        _stroke.Clear();
        _corners.Clear();
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
        if (Tool is { Wall: int ring })
        {
            var at = ToTown(w);
            // Back on the first corner closes the loop.
            if (_corners.Count >= 3 && at.DistanceTo(_corners[0]) < 0.9f) CloseWall(ring);
            else _corners.Add(at);
            QueueRedraw();
            return;
        }
        if (Tool is { Gate: int gr })
        {
            var at = ToTown(w);
            if (map.Sim.PlaceGate(gr, at.X, at.Y) is { } no) Said?.Invoke($"No gate there: {no}.");
            else if (Tool.Work is DefenseWork work && !WallLines.Begun(town, gr) && map.Sim.BuildDefense(work))
                Said?.Invoke($"The gate is marked and work begins on {WallLines.WallName(town, gr)}. Click the line again for more gates; right-click when done.");
            else Said?.Invoke("A gate is marked on the wall. Right-click when done.");
            Changed?.Invoke();
            QueueRedraw();
            return;
        }
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

    /// <summary>Lays the drawn loop as the wall's line, then hands over to placing its gates.</summary>
    private void CloseWall(int ring)
    {
        if (map.Sim.DrawWall(ring, _corners.Select(c => ((double)c.X, (double)c.Y))) is { } why)
        {
            Said?.Invoke($"The wall can't run there: {why}.");
            return;
        }
        Said?.Invoke("The line is marked. Now click on it to place a gate: every wall needs one.");
        GateNext(ring);
    }

    private void GateNext(int ring)
    {
        var work = Tool?.Work;
        Pick(new PlanTool(null, null) { Gate = ring, Work = work });
    }

    /// <summary>Enter closes the wall being drawn (or keeps the line already there and goes on to its gates); Backspace takes back a corner.</summary>
    public bool WallKey(Key k)
    {
        if (Tool is not { Wall: int ring }) return false;
        if (k is Godot.Key.Enter or Godot.Key.KpEnter)
        {
            if (_corners.Count >= 3) CloseWall(ring);
            else GateNext(ring);
            return true;
        }
        if (k == Godot.Key.Backspace && _corners.Count > 0)
        {
            _corners.RemoveAt(_corners.Count - 1);
            QueueRedraw();
            return true;
        }
        return false;
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
        if (Tool.Wall is int wr || Tool.Gate is int)
        {
            WallTool(town, h);
            return;
        }
        if (Tool.Zone is Zone z)
        {
            var c = z == Zone.None ? new Color(0.95f, 0.9f, 0.8f) : ToolColor(town, Tool);
            // Only the paint shows: buildings come as the town grows into it, never as outlines laid out ahead.
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

    /// <summary>The wall's line as it stands now (dashed), its gates, and the corners being drawn or the gate a click would place.</summary>
    private void WallTool(Town town, Vector2 h)
    {
        int ring = Tool!.Wall ?? Tool.Gate ?? 0;
        var stone = new Color("e8dcc0");
        const int Steps = 96;
        var line = new Vector2[Steps + 1];
        for (int i = 0; i <= Steps; i++)
        {
            var (x, y) = WallLines.Point(town, ring, WallLines.Heading(i % Steps));
            line[i] = Screen(new Vector2((float)x, (float)y));
        }
        var dim = new Color(stone, Tool.Wall != null ? 0.45f : 0.9f);
        for (int i = 0; i < Steps; i += 2) DrawLine(line[i], line[i + 1], dim, Tool.Wall != null ? 2 : 3, true);
        foreach (double g in WallLines.Gates(town, ring)) GateMark(town, ring, g, new Color("e8b04a"));
        if (Tool.Wall != null)
        {
            var red = new Color(0.9f, 0.3f, 0.25f);
            var pts = _corners.Select(Screen).ToList();
            pts.Add(Screen(h));
            var c = _corners.Count >= 3 && WallLines.DrawBlocked(town, ring, [.. _corners.Select(v => ((double)v.X, (double)v.Y)), (h.X, h.Y)]) != null ? red : stone;
            if (pts.Count >= 2) DrawPolyline([.. pts], c, 3, true);
            foreach (var p in _corners) DrawCircle(Screen(p), 5, c);
            if (_corners.Count > 0) DrawArc(Screen(_corners[0]), 10, 0, Mathf.Tau, 24, new Color("f6eedb"), 2, true);
            string hint = _corners.Count == 0 ? "Click round the town to draw the wall · Enter keeps the line shown"
                : _corners.Count < 3 ? "Keep clicking corners · Backspace takes one back"
                : "Click the first corner (or Enter) to close the wall";
            Label(map.ToScreen(FromTown(h.X, h.Y), 0.7f) + new Vector2(0, -16), hint, 12, new Color("f6eedb"));
            return;
        }
        double a = Math.Atan2(h.Y - Town.Center, h.X - Town.Center);
        string? why = WallLines.GateBlocked(town, ring, h.X, h.Y);
        GateMark(town, ring, a, why == null ? new Color(0.45f, 0.9f, 0.4f) : new Color(0.9f, 0.3f, 0.25f));
        double cost = WallLines.GateCost(town, ring);
        Label(map.ToScreen(FromTown(h.X, h.Y), 0.7f) + new Vector2(0, -16), why == null ? cost > 0 ? $"Gate · {Js.Str(cost)}g" : "Gate" : Ui.Cap(why), 12, new Color("f6eedb"));
    }

    private void GateMark(Town town, int ring, double angle, Color c)
    {
        var (x, y) = WallLines.Point(town, ring, angle);
        var p = new Vector2((float)x, (float)y);
        var out1 = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 0.7f;
        var side = new Vector2(-out1.Y, out1.X);
        Vector2[] q = [Screen(p - out1 - side), Screen(p - out1 + side), Screen(p + out1 + side), Screen(p + out1 - side)];
        DrawColoredPolygon(q, new Color(c, 0.55f));
        DrawPolyline([.. q, q[0]], c, 2, true);
    }

    /// <summary>Half a tile per sample: whether the building fits there facing the nearest lane.</summary>
    private const float Sample = 0.5f;

    private (string Id, int Version, int Wards, bool[] Fits)? _ground;

    /// <summary>The ground that takes the building, shaded green a row run at a time; worked out again only when the plan changes.</summary>
    private void Ground(Town town, BuildingDef def)
    {
        int n = (int)(Town.Grid / Sample);
        if (_ground is not { } g || g.Id != def.Id || g.Version != town.Plan.Version || g.Wards != town.City.Tier)
        {
            var fits = new bool[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    fits[y * n + x] = Land.At(town, def, (x + 0.5) * Sample, (y + 0.5) * Sample) != null;
            _ground = g = (def.Id, town.Plan.Version, town.City.Tier, fits);
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

    /// <summary>
    /// The wards as they have grown, not as a plan: each formed ward outlined round its own buildings with its name at their
    /// middle (unsettled ground shows nothing), and the line past which the charter doesn't yet let the town zone.
    /// </summary>
    private void Wards(Town town)
    {
        var border = new Color("f6eedb", 0.45f);
        foreach (var id in town.City.OpenWards)
        {
            var pts = new List<Vector2>();
            foreach (var lot in town.Lots)
                if (lot.BuildingId != null && City.WardOf(town, lot).Id == id)
                    foreach (var c in Geo.LotCorners(lot, 0.35f)) pts.Add(c - new Vector2(Geo.Margin, Geo.Margin));
            if (pts.Count < 3) continue;
            var hull = Hull(pts);
            var screen = hull.Select(Screen).ToArray();
            DrawPolyline([.. screen, screen[0]], border, 2, true);
            var mid = new Vector2(hull.Average(p => p.X), hull.Average(p => p.Y));
            Label(map.ToScreen(FromTown(mid.X, mid.Y), 0.6f), City.Ward(town, id).Name.ToUpperInvariant(), 13, new Color("f6eedb"));
        }
        // The charter's reach: zoning stops at the edge of the last ring it opens.
        int ring = 0;
        while (ring < City.WallRings && City.RingOpen(town, ring + 1)) ring++;
        if (ring >= City.WallRings) return;
        double c0 = Town.Center;
        const int Steps = 96;
        var line = new Vector2[Steps + 1];
        for (int i = 0; i <= Steps; i++)
        {
            double a = i * Math.PI * 2 / Steps, r = City.Edge(town, ring, a);
            line[i] = Screen(new Vector2((float)(c0 + Math.Cos(a) * r), (float)(c0 + Math.Sin(a) * r)));
        }
        var dim = new Color("cfc6b4", 0.5f);
        for (int i = 0; i < Steps; i += 2) DrawLine(line[i], line[i + 1], dim, 2, true);
        double la = Math.PI * 0.25, lr = City.Edge(town, ring, la) + 1.2;
        Label(map.ToScreen(FromTown(c0 + Math.Cos(la) * lr, c0 + Math.Sin(la) * lr), 0.6f),
            $"more land to zone once the town is a {City.TierName(town, City.RingTier(town, ring + 1)).ToLowerInvariant()}", 11, new Color("cfc6b4", 0.85f));
    }

    /// <summary>The convex hull of points, anticlockwise (Andrew's monotone chain).</summary>
    private static List<Vector2> Hull(List<Vector2> pts)
    {
        var p = pts.OrderBy(v => v.X).ThenBy(v => v.Y).ToList();
        var h = new List<Vector2>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = h.Count;
            foreach (var v in p)
            {
                while (h.Count >= start + 2 && (h[^1] - h[^2]).Cross(v - h[^2]) <= 0) h.RemoveAt(h.Count - 1);
                h.Add(v);
            }
            h.RemoveAt(h.Count - 1);
            p.Reverse();
        }
        return h;
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
