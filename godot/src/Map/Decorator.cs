using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Setting decorations down, after Ostriv and Foundation: no grid anywhere. The piece in hand
/// follows the cursor and turns freely: click to set it down, or press and drag to aim it before
/// letting go; Shift+wheel or R turns it in steps. Fences and hedges run in a row along a drag;
/// trees, flowers and crates can be painted with a drag, each turned its own way. The eraser
/// clears what it is dragged over. While a piece is in hand every building shows how pleasant its
/// corner of town is, and a ring shows how far the piece's effect reaches.
/// </summary>
public partial class Decorator(MapView map) : Control
{
    /// <summary>A decoration to place by content id, or the eraser (null).</summary>
    public sealed record DecorTool(string? Piece);

    public DecorTool? Tool { get; private set; }

    /// <summary>A piece was placed or cleared, or the tool changed.</summary>
    public event Action? Changed;

    public bool Active => Tool != null;

    /// <summary>The facing of the next piece, in radians.</summary>
    public double Angle { get; private set; }

    private Vector2? _hover, _press;
    private bool _dragged;
    private Vector2? _lastPaint;
    private int _painted;

    private const float Step = Mathf.Pi / 12;
    private const float EraseRadius = 0.45f;

    private Town Town => map.Sim.Town;
    private DecorDef? Def => Tool?.Piece is string id ? Town.Content.Decor.Piece(id) : null;

    public void Pick(DecorTool? tool)
    {
        Tool = tool;
        _press = null;
        _dragged = false;
        if (tool == null) map.Decor.ShowGhost(null, 0, 0, 0, false);
        QueueRedraw();
        Changed?.Invoke();
    }

    /// <summary>Turn the piece in hand by steps of 15 degrees.</summary>
    public void Turn(int steps)
    {
        Angle = Decor.NormalAngle(Angle + steps * Step);
        Ghost();
    }

    /// <summary>World tile to town lots.</summary>
    private static Vector2 Lots(Vector2 w) => new(w.X - Geo.Margin, w.Y - Geo.Margin);

    // ---------- Input, routed here by the map while a piece is in hand ----------

    public void Hover(Vector2 w)
    {
        _hover = Lots(w);
        Ghost();
        QueueRedraw();
    }

    public void Press(Vector2 w)
    {
        _press = Lots(w);
        _hover = _press;
        _dragged = false;
        _lastPaint = null;
        _painted = 0;
    }

    /// <summary>The cursor moved with the button down.</summary>
    public void Drag(Vector2 w)
    {
        _hover = Lots(w);
        if (_press is not Vector2 p) return;
        if (!_dragged && p.DistanceTo(_hover.Value) < 0.15f) return;
        _dragged = true;
        var def = Def;
        if (Tool is { Piece: null }) Erase(_hover.Value, EraseRadius);
        else if (def is { Scatter: true, Line: 0 }) Paint(def, _hover.Value);
        else if (def is { Line: 0 }) Angle = Decor.NormalAngle(Math.Atan2(_hover.Value.X - p.X, _hover.Value.Y - p.Y));
        Ghost();
        QueueRedraw();
    }

    public void Release(Vector2 w)
    {
        _hover = Lots(w);
        var def = Def;
        bool placed = false;
        if (_press is Vector2 p)
        {
            if (Tool is { Piece: null })
            {
                if (!_dragged) Erase(p, 0.3f, true);
            }
            else if (def != null && def.Line > 0 && _dragged)
            {
                int n = 0;
                foreach (var (x, y, a) in Row(def, p, _hover.Value))
                    if (map.Sim.Decorate(def.Id, x, y, a) != null) n++;
                placed = n > 0;
            }
            else if (def != null && def.Scatter && def.Line == 0 && _dragged)
            {
                placed = _painted > 0;
            }
            else if (def != null)
            {
                placed = Place(def, p.X, p.Y, Angle) != null;
            }
        }
        // Once it is down the piece leaves the hand and the build bar comes back; Shift keeps it for more.
        if (placed && Tool != null && !Input.IsKeyPressed(Key.Shift)) Pick(null);
        else if (placed) Changed?.Invoke();
        _press = null;
        _dragged = false;
        Ghost();
        QueueRedraw();
    }

    private Decoration? Place(DecorDef def, double x, double y, double angle)
    {
        var d = map.Sim.Decorate(def.Id, Math.Round(x, 3), Math.Round(y, 3), angle);
        // Statues are dedicated one at a time; put the tool down so the player sees who it honours.
        if (d != null && def.Honors != "") Pick(null);
        return d;
    }

    /// <summary>Plant a piece wherever the brush has moved far enough, each turned its own way.</summary>
    private void Paint(DecorDef def, Vector2 at)
    {
        float spacing = (float)def.Size * 2.6f;
        if (_lastPaint is Vector2 last && last.DistanceTo(at) < spacing) return;
        int seed = Mathf.RoundToInt(at.X * 97) * 31 + Mathf.RoundToInt(at.Y * 89);
        var jitter = new Vector2(Geo.Hash(seed) - 0.5f, Geo.Hash(seed + 7) - 0.5f) * spacing * 0.5f;
        var p = at + jitter;
        if (map.Sim.Decorate(def.Id, Math.Round(p.X, 3), Math.Round(p.Y, 3), Geo.Hash(seed + 3) * Mathf.Tau) != null)
        {
            _painted++;
            _lastPaint = at;
            Changed?.Invoke();
        }
    }

    /// <summary>Pieces along a drag from a to b, evenly spaced, each turned along the row.</summary>
    private static IEnumerable<(double X, double Y, double A)> Row(DecorDef def, Vector2 a, Vector2 b)
    {
        float len = a.DistanceTo(b);
        int n = Math.Max(1, Mathf.RoundToInt(len / (float)def.Line) + 1);
        double angle = Decor.NormalAngle(Math.Atan2(b.X - a.X, b.Y - a.Y) + Math.PI / 2);
        for (int i = 0; i < n; i++)
        {
            var p = n == 1 ? a : a.Lerp(b, i / (float)(n - 1));
            yield return (Math.Round(p.X, 3), Math.Round(p.Y, 3), angle);
        }
    }

    private void Erase(Vector2 at, float radius, bool nearestOnly = false)
    {
        var hits = Town.Decor.Pieces
            .Select(d => (d, dist: at.DistanceTo(new Vector2((float)d.X, (float)d.Y))))
            .Where(x => x.dist < radius + x.d.Def.Size)
            .OrderBy(x => x.dist)
            .ThenBy(x => x.d.Id)
            .Select(x => x.d.Id)
            .ToList();
        if (nearestOnly) hits = hits.Take(1).ToList();
        int n = hits.Count(id => map.Sim.ClearDecoration(id));
        if (n > 0) Changed?.Invoke();
    }

    private void Ghost()
    {
        var def = Def;
        if (def == null || _hover is not Vector2 h)
        {
            map.Decor.ShowGhost(null, 0, 0, 0, false);
            return;
        }
        var at = _press is Vector2 p && def.Line == 0 && !(def.Scatter && _dragged) ? p : h;
        map.Decor.ShowGhost(def.Id, at.X, at.Y, Angle, Why(def, at) == null, Town.Decor.NextId);
    }

    private string? Why(DecorDef def, Vector2 at) => Decor.Blocked(Town, def) ?? Decor.SpotBlocked(Town, def, at.X, at.Y);

    // ---------- Drawing ----------

    public override void _Draw()
    {
        if (Tool == null || map.Sim == null) return;
        var town = Town;
        Appeal(town);
        if (_hover is not Vector2 h) return;
        var def = Def;
        if (def == null)
        {
            Ring(h, EraseRadius, new Color(0.95f, 0.4f, 0.3f, 0.9f), 2);
            foreach (var d in town.Decor.Pieces)
                if (h.DistanceTo(new Vector2((float)d.X, (float)d.Y)) < EraseRadius + d.Def.Size)
                    Ring(new Vector2((float)d.X, (float)d.Y), (float)d.Def.Size, new Color(0.95f, 0.35f, 0.3f, 0.9f), 2);
            Label(h, "Clear (half the price back)", true);
            return;
        }
        var at = _press is Vector2 p && def.Line == 0 && !(def.Scatter && _dragged) ? p : h;
        if (def.Line > 0 && _press is Vector2 from && _dragged)
        {
            foreach (var (x, y, _) in Row(def, from, h))
            {
                var q = new Vector2((float)x, (float)y);
                bool ok = Why(def, q) == null;
                Ring(q, (float)def.Size, ok ? new Color(0.6f, 0.95f, 0.5f, 0.9f) : new Color(0.95f, 0.35f, 0.3f, 0.9f), 2);
            }
        }
        string? why = Why(def, at);
        Ring(at, (float)def.Radius, new Color(0.95f, 0.85f, 0.45f, 0.55f), 1.5f);
        Ring(at, (float)def.Size, why == null ? new Color(0.6f, 0.95f, 0.5f, 0.95f) : new Color(0.95f, 0.35f, 0.3f, 0.95f), 2);
        if (_press is Vector2 && def.Line == 0 && !def.Scatter && _dragged)
        {
            var tip = at + new Vector2(Mathf.Sin((float)Angle), Mathf.Cos((float)Angle)) * 0.8f;
            DrawLine(Screen(at), Screen(tip), new Color(0.95f, 0.85f, 0.45f, 0.9f), 2, true);
        }
        Label(at, why == null ? $"{def.Name} · {Decor.Cost(town, def)}g" : Ui.Cap(why), why != null);
    }

    /// <summary>Every building's appeal, Pharaoh-style: green where it is pleasant, red where it is grim.</summary>
    private void Appeal(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (b.Razed) continue;
            double a = Decor.AppealOf(town, b);
            if (Math.Abs(a) < 0.15) continue;
            var (x, y) = Decor.PosOf(town, b);
            var c = a > 0 ? new Color(0.45f, 0.9f, 0.4f) : new Color(0.95f, 0.35f, 0.25f);
            float r = Mathf.Clamp((float)Math.Abs(a) / 10, 0.08f, 0.42f);
            var pts = Circle(new Vector2((float)x, (float)y), r, 18);
            DrawColoredPolygon(pts, new Color(c, 0.32f));
            DrawPolyline([.. pts, pts[0]], new Color(c, 0.75f), 1.2f, true);
        }
    }

    private Vector2 Screen(Vector2 lots, float lift = 0.04f) => map.ToScreen(new Vector2(Geo.Margin + lots.X, Geo.Margin + lots.Y), lift);

    private Vector2[] Circle(Vector2 c, float r, int n)
    {
        var pts = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n;
            pts[i] = Screen(c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        return pts;
    }

    private void Ring(Vector2 c, float r, Color col, float width)
    {
        var pts = Circle(c, r, Math.Clamp((int)(r * 40), 16, 64));
        DrawPolyline([.. pts, pts[0]], col, width, true);
    }

    private void Label(Vector2 at, string text, bool bad)
    {
        var s = Screen(at, 0.7f);
        float w = Ui.Bold.GetStringSize(text, HorizontalAlignment.Left, -1, 13).X;
        var pos = s - new Vector2(w / 2, 14);
        DrawStringOutline(Ui.Bold, pos, text, HorizontalAlignment.Left, -1, 13, 4, new Color(0.08f, 0.06f, 0.04f, 0.85f));
        DrawString(Ui.Bold, pos, text, HorizontalAlignment.Left, -1, 13, bad ? new Color("f0a090") : new Color("f6eedb"));
    }
}
