using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town plan, drawn over the map while a build tool is picked: every lot tinted by its zone,
/// and a preview of the stroke or the lot under the cursor. Zone tools paint a rectangle you drag
/// over open land; a building tool places on the lot you click. Right-click puts the tool down.
/// The wards are outlined and named; land in a ward not yet laid out is shaded and can't be zoned.
/// </summary>
public partial class Planner(MapView map) : Control
{
    /// <summary>A zone to paint (Zone.None clears land), or a building to place, by content id.</summary>
    public sealed record PlanTool(Zone? Zone, string? Building);

    public PlanTool? Tool { get; private set; }

    /// <summary>The plan changed (land zoned, a building placed, or the tool put down).</summary>
    public event Action? Changed;

    private Vector2I? _from, _hover;

    public bool Active => Tool != null;

    public void Pick(PlanTool? tool)
    {
        Tool = tool;
        _from = null;
        QueueRedraw();
        Changed?.Invoke();
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

    private static Vector2I? LotAt(Vector2 w)
    {
        int gx = Mathf.FloorToInt(w.X - Geo.Margin), gy = Mathf.FloorToInt(w.Y - Geo.Margin);
        return gx >= 0 && gy >= 0 && gx < Geo.Grid && gy < Geo.Grid ? new Vector2I(gx, gy) : null;
    }

    // ---------- Input, routed here by the map while a tool is picked ----------

    public void Hover(Vector2 w)
    {
        _hover = LotAt(w);
        QueueRedraw();
    }

    public void Press(Vector2 w)
    {
        _from = LotAt(w);
        _hover = _from;
    }

    public void Release(Vector2 w)
    {
        var to = LotAt(w) ?? _hover;
        var town = map.Sim.Town;
        if (Tool is { Zone: Zone z } && _from is Vector2I a && to is Vector2I b)
        {
            if (map.Sim.ZoneLots(Stroke(a, b).Select(p => town.LotAt(p.X, p.Y).Id), z) > 0) Changed?.Invoke();
        }
        else if (Tool is { Building: string id } && to is Vector2I at)
        {
            if (map.Sim.Place(id, town.LotAt(at.X, at.Y).Id) != null)
            {
                // One commission per pick: the next costs more, and the bar shows why if it can't be had.
                Pick(null);
                return;
            }
        }
        _from = null;
        QueueRedraw();
    }

    private static IEnumerable<Vector2I> Stroke(Vector2I a, Vector2I b)
    {
        for (int y = Math.Min(a.Y, b.Y); y <= Math.Max(a.Y, b.Y); y++)
            for (int x = Math.Min(a.X, b.X); x <= Math.Max(a.X, b.X); x++)
                yield return new Vector2I(x, y);
    }

    // ---------- Drawing ----------

    public override void _Draw()
    {
        if (Tool == null || map.Sim == null) return;
        var town = map.Sim.Town;
        foreach (var l in town.Lots)
        {
            if (!City.IsOpen(town, l)) Shade(l.X, l.Y, new Color(0.05f, 0.04f, 0.03f, 0.38f));
            else if (l.Zone == Zone.None) Outline(l.X, l.Y, new Color(1, 1, 1, 0.12f));
            else Fill(l.X, l.Y, new Color(ZoneColor(l.Zone), l.IsVacant ? 0.32f : 0.16f));
        }
        Wards(town);
        if (Tool.Zone is Zone z)
        {
            var b = _hover;
            var a = _from ?? b;
            if (a is Vector2I pa && b is Vector2I pb)
                foreach (var p in Stroke(pa, pb))
                {
                    bool ok = Planning.CanZone(town, town.LotAt(p.X, p.Y), z);
                    Fill(p.X, p.Y, ok ? new Color(ZoneColor(z), 0.7f) : new Color(0.9f, 0.3f, 0.25f, 0.35f));
                }
        }
        else if (Tool.Building is string id && _hover is Vector2I h)
        {
            var lot = town.LotAt(h.X, h.Y);
            string? why = Planning.LotBlocked(town, lot) ?? Planning.Blocked(town, town.Content.Building(id));
            var c = why == null ? new Color(ZoneColor(Planning.ZoneFor(town, town.Content.Building(id))), 0.75f) : new Color(0.9f, 0.3f, 0.25f, 0.6f);
            Fill(h.X, h.Y, c);
            var label = why == null ? town.Content.Building(id).Name : Ui.Cap(why);
            var at = map.ToScreen(new Vector2(Geo.Margin + h.X + 0.5f, Geo.Margin + h.Y + 0.5f), 0.6f);
            var w = Ui.Bold.GetStringSize(label, HorizontalAlignment.Left, -1, 13).X;
            DrawStringOutline(Ui.Bold, at - new Vector2(w / 2, 0), label, HorizontalAlignment.Left, -1, 13, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
            DrawString(Ui.Bold, at - new Vector2(w / 2, 0), label, HorizontalAlignment.Left, -1, 13, new Color("f6eedb"));
        }
    }

    /// <summary>Ward borders, and each ward's name at its middle: grey with the charter it waits for until it is laid out.</summary>
    private void Wards(Town town)
    {
        var border = new Color("f6eedb", 0.55f);
        var sums = new Dictionary<string, (float X, float Y, int N)>();
        foreach (var l in town.Lots)
        {
            var w = City.WardOf(town, l);
            var (sx, sy, n) = sums.GetValueOrDefault(w.Id);
            sums[w.Id] = (sx + l.X, sy + l.Y, n + 1);
            if (l.X + 1 < Geo.Grid && City.WardOf(town, town.LotAt(l.X + 1, l.Y)) != w) Edge(l.X + 1, l.Y, l.X + 1, l.Y + 1, border);
            if (l.Y + 1 < Geo.Grid && City.WardOf(town, town.LotAt(l.X, l.Y + 1)) != w) Edge(l.X, l.Y + 1, l.X + 1, l.Y + 1, border);
        }
        foreach (var w in town.Content.City.Wards)
        {
            if (!sums.TryGetValue(w.Id, out var c) || c.N == 0) continue;
            bool open = town.City.OpenWards.Contains(w.Id);
            string sub = open ? "" : City.Reached(town, w.Tier) ? $"ready to lay out · {w.Cost:N0}g" : $"needs a {City.TierName(town, w.Tier).ToLowerInvariant()}";
            var at = map.ToScreen(new Vector2(Geo.Margin + c.X / c.N + 0.5f, Geo.Margin + c.Y / c.N + 0.5f), 0.6f);
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

    private void Edge(int x0, int y0, int x1, int y1, Color c) =>
        DrawLine(map.ToScreen(new(Geo.Margin + x0, Geo.Margin + y0), 0.1f), map.ToScreen(new(Geo.Margin + x1, Geo.Margin + y1), 0.1f), c, 2, true);

    private void Shade(int x, int y, Color c) => DrawColoredPolygon(Corners(x, y, 0), c);

    private Vector2[] Corners(int x, int y, float inset)
    {
        float x0 = Geo.Margin + x + inset, y0 = Geo.Margin + y + inset, x1 = Geo.Margin + x + 1 - inset, y1 = Geo.Margin + y + 1 - inset;
        return [map.ToScreen(new(x0, y0), 0.08f), map.ToScreen(new(x1, y0), 0.08f), map.ToScreen(new(x1, y1), 0.08f), map.ToScreen(new(x0, y1), 0.08f)];
    }

    private void Fill(int x, int y, Color c)
    {
        var p = Corners(x, y, 0.04f);
        DrawColoredPolygon(p, c);
        DrawPolyline([.. p, p[0]], new Color(c, Mathf.Min(1, c.A + 0.3f)), 1.2f, true);
    }

    private void Outline(int x, int y, Color c)
    {
        var p = Corners(x, y, 0.04f);
        DrawPolyline([.. p, p[0]], c, 1, true);
    }
}
