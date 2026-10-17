using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The town plan, drawn over the map while a build tool is picked: every lot tinted by its zone,
/// and a preview of the stroke or the lot under the cursor. Zone tools paint a rectangle you drag
/// over open land; a building tool places on the lot you click. Right-click puts the tool down.
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
            if (l.Zone == Zone.None) Outline(l.X, l.Y, new Color(1, 1, 1, 0.12f));
            else Fill(l.X, l.Y, new Color(ZoneColor(l.Zone), l.IsVacant ? 0.32f : 0.16f));
        }
        if (Tool.Zone is Zone z)
        {
            var b = _hover;
            var a = _from ?? b;
            if (a is Vector2I pa && b is Vector2I pb)
                foreach (var p in Stroke(pa, pb))
                {
                    bool ok = Planning.CanZone(town.LotAt(p.X, p.Y), z);
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
