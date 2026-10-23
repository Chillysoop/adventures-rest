using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// New game's map of the world round its heart: the country of each ring painted by its kind, the ocean, lakes and
/// mountains, the peoples' homes, a pin for every kind of town site (founding.json) and the bands of danger growing
/// outward. Hovering a spot tells what could be founded there; clicking picks it. Drawn from a town generated for the
/// seed (<see cref="Founding.Spots"/>), never the sim's dice. Lairs stay hidden, as they are when the game begins.
/// </summary>
public partial class FoundingMap : Control
{
    /// <summary>Days from the heart to the map's edge.</summary>
    private const double View = 4.4;

    private Town? _town;
    private (double X, double Y)? _hover, _picked;

    /// <summary>A spot the player clicked, in days from the heart.</summary>
    public event Action<double, double>? Picked;

    public FoundingMap()
    {
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Cross;
    }

    /// <summary>Show a world, with a spot picked and (for screenshots) one the pointer is over.</summary>
    public void Show(Town town, (double X, double Y)? picked, (double X, double Y)? hover = null)
    {
        _town = town;
        _picked = picked;
        _hover = hover;
        QueueRedraw();
    }

    public void Pick((double X, double Y)? at)
    {
        _picked = at;
        QueueRedraw();
    }

    private float PxPerDay => Mathf.Min(Size.X, Size.Y) / 2f / (float)View;

    private Vector2 Px(double x, double y) => Size / 2 + new Vector2((float)x, (float)y) * PxPerDay;

    private (double X, double Y) Days(Vector2 p) => ((p.X - Size.X / 2) / PxPerDay, (p.Y - Size.Y / 2) / PxPerDay);

    /// <summary>The pin under a point, if any: a click near one picks it exactly.</summary>
    private FoundingSpot? PinAt(Vector2 p) =>
        _town == null ? null : Founding.Spots(_town).Where(s => Px(s.X, s.Y).DistanceTo(p) < 16).OrderBy(s => Px(s.X, s.Y).DistanceTo(p)).FirstOrDefault();

    public override void _GuiInput(InputEvent e)
    {
        if (_town == null) return;
        if (e is InputEventMouseMotion m)
        {
            var at = PinAt(m.Position) is { } pin ? (pin.X, pin.Y) : Days(m.Position);
            if (_hover != at)
            {
                _hover = at;
                QueueRedraw();
            }
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b)
        {
            var at = PinAt(b.Position) is { } pin ? (pin.X, pin.Y) : Days(b.Position);
            _picked = at;
            Picked?.Invoke(at.Item1, at.Item2);
            QueueRedraw();
            AcceptEvent();
        }
    }

    /// <summary>The colour a kind of country is painted in.</summary>
    private static Color Land(ThemeDef t) => t.Id switch
    {
        "greenmarch" => new Color("8fa66a"),
        "thornwood" => new Color("5e7c4a"),
        "highpeaks" => new Color("9a948a"),
        "deeproads" => new Color("7d6a58"),
        "crownruins" => new Color("b0a07a"),
        "ashenbarrows" => new Color("8d8a66"),
        "mirefen" => new Color("6d806a"),
        "saltreach" => new Color("c8b98c"),
        "rimefell" => new Color("cfd8dc"),
        "duneglass" => new Color("d9bd7a"),
        "cinderwastes" => new Color("a66a4a"),
        "feyfringe" => new Color("8f7ea8"),
        _ when t.Sea => new Color("5d7f96"),
        _ => new Color("a99a7a"),
    };

    private static Color Hard(string kind) => kind switch
    {
        "ocean" or "sea" or "lake" or "ferry" => new Color("4f7590"),
        "peaks" or "range" => new Color("6c655c"),
        "ridge" or "icefield" => new Color("8d877c"),
        "mire" => new Color("4d6550"),
        "tangle" or "beastland" => new Color("3f5a36"),
        "waste" or "barrows" or "monsterland" => new Color("7a6448"),
        _ => new Color("6a5a44"),
    };

    /// <summary>A band round a centre between two radii and two bearings, as a polygon in pixels.</summary>
    private Vector2[] Sector(double ox, double oy, double bearing, double arc, double inner, double outer)
    {
        int n = Math.Max(4, (int)(arc / 6));
        var pts = new List<Vector2>(2 * n + 2);
        for (int i = 0; i <= n; i++) pts.Add(Polar(ox, oy, bearing - arc / 2 + arc * i / n, outer));
        for (int i = n; i >= 0; i--) pts.Add(Polar(ox, oy, bearing - arc / 2 + arc * i / n, Math.Max(0.02, inner)));
        return [.. pts];
    }

    private Vector2 Polar(double ox, double oy, double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return Px(ox + r * Math.Sin(a), oy - r * Math.Cos(a));
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color("2a251d"));
        if (_town is not { } t) return;
        var heart = World.Heart(t);
        // The country, ring by ring.
        foreach (var g in t.Regions)
        {
            var (inner, outer) = World.Band(t, g.Ring);
            if (inner > View * 1.5) continue;
            DrawColoredPolygon(Sector(heart.X, heart.Y, g.Bearing, g.Arc + 0.4, inner, outer), Land(g.Theme));
        }
        // Its hard country: the ocean, lakes, mountains and the rest, each its own colour.
        foreach (var f in t.Terrain.OrderBy(f => f.Kind.Id == "ocean" ? 0 : 1).ThenBy(f => f.Id))
        {
            if (f.Inner > View * 1.5 + Js.Hypot(f.Ox, f.Oy)) continue;
            var c = Hard(f.Kind.Id);
            DrawColoredPolygon(Sector(f.Ox, f.Oy, f.Bearing, f.Arc, f.Inner, f.Outer), f.Kind.Barrier == "" && f.Kind.Id != "ocean" ? c with { A = 0.55f } : c);
        }
        // The rings of danger: a faint line between each, and the levels its lairs are built for.
        var ink = new Color("1e1a14");
        for (int ring = 0; ring < t.Content.World.Rings.Count; ring++)
        {
            var (inner, outer) = World.Band(t, ring);
            if (inner > View) break;
            if (ring > 0) DrawArc(Px(heart.X, heart.Y), (float)inner * PxPerDay, 0, Mathf.Tau, 96, ink with { A = 0.35f }, 1.5f);
            var lv = t.Content.World.Rings[ring].Levels;
            var at = Px(heart.X, heart.Y - (inner + Math.Min(outer, View) - 0.1) / 2) - new Vector2(30, -5);
            DrawString(Ui.Bold, at, $"Lv {lv[0]}–{lv[^1]}", HorizontalAlignment.Center, 60, 13, ink with { A = 0.75f });
        }
        // Beyond the reach no town can be founded: the deep wilds, darkened.
        double reach = t.Content.Founding.Reach;
        float far = Mathf.Max(Size.X, Size.Y) * 2;
        var shade = new Color(0.08f, 0.06f, 0.04f, 0.55f);
        for (int i = 0; i < 72; i++)
        {
            Vector2 In(int k) => Px(heart.X + reach * Math.Sin(k * Math.Tau / 72), heart.Y - reach * Math.Cos(k * Math.Tau / 72));
            Vector2 Out(int k) => Px(heart.X, heart.Y) + new Vector2(Mathf.Sin(k * Mathf.Tau / 72), -Mathf.Cos(k * Mathf.Tau / 72)) * far;
            DrawColoredPolygon([In(i), Out(i), Out(i + 1), In(i + 1)], shade);
        }
        DrawArc(Px(heart.X, heart.Y), (float)reach * PxPerDay, 0, Mathf.Tau, 128, Ui.Gold with { A = 0.6f }, 2);
        // Region names, faint, for the country near enough to matter.
        foreach (var g in t.Regions.Where(g => g.Ring <= 2))
        {
            var (mx, my) = World.Middle(t, g);
            if (Js.Hypot(mx - heart.X, my - heart.Y) > View) continue;
            DrawString(Ui.Medium, Px(mx, my) - new Vector2(70, 0), g.Name, HorizontalAlignment.Center, 140, 11, new Color(0.1f, 0.08f, 0.06f, 0.6f));
        }
        // The peoples' homes.
        foreach (var st in t.Settlements)
        {
            var (sx, sy) = Explore.SettlementSpot(t, st);
            if (Js.Hypot(sx - heart.X, sy - heart.Y) > View) continue;
            var p = Px(sx, sy);
            DrawCircle(p, 6, new Color("efe6d2"));
            DrawCircle(p, 3.5f, new Color("7a3b2a"));
        }
        // A pin for each kind of site, the picked spot, and the pointer.
        foreach (var s in Founding.Spots(t))
        {
            var p = Px(s.X, s.Y);
            DrawCircle(p, 14, new Color(0.12f, 0.1f, 0.08f, 0.85f));
            DrawArc(p, 14, 0, Mathf.Tau, 24, Ui.Gold, 1.5f);
            DrawTextureRect(Ui.Icon($"found-{s.Kind}"), new Rect2(p - new Vector2(10, 10), new Vector2(20, 20)), false);
        }
        if (_picked is var (px, py))
        {
            var p = Px(px, py);
            DrawCircle(p, 20, Ui.Accent with { A = 0.25f });
            DrawArc(p, 20, 0, Mathf.Tau, 32, Ui.Accent, 2.5f);
            DrawTextureRect(Ui.Icon("found-here"), new Rect2(p - new Vector2(13, 28), new Vector2(26, 26)), false);
        }
        if (_hover is var (hx, hy)) Tag(t, hx, hy);
    }

    /// <summary>Beside the pointer: the kinds of town the spot takes, as their icons, and the level its lairs are built for; a cross where none can stand.</summary>
    private void Tag(Town t, double x, double y)
    {
        var p = Px(x, y);
        var kinds = Founding.KindsAt(t, x, y);
        string lv = Explore.RegionAt(t, x, y) is { } g ? $"Lv {g.Levels[0]}–{g.Levels[^1]}" : "";
        // Over a pin, its kind by name.
        if (Founding.Spots(t).FirstOrDefault(s => Js.Hypot(s.X - x, s.Y - y) < 0.05) is { } pin && Founding.Kind(t.Content, pin.Kind) is { } k)
        {
            kinds = [k];
            lv = $"{k.Name} · {lv}";
        }
        float w = Mathf.Max(1, kinds.Count) * 22 + Ui.Bold.GetStringSize(lv, HorizontalAlignment.Left, -1, 13).X + 16;
        var box = new Rect2(p + new Vector2(18, -34), new Vector2(w, 28));
        if (box.End.X > Size.X) box.Position -= new Vector2(w + 36, 0);
        DrawRect(box, new Color(0.1f, 0.08f, 0.06f, 0.9f));
        DrawRect(box, Ui.Gold with { A = 0.7f }, false, 1);
        if (kinds.Count == 0) DrawTextureRect(Ui.Icon("ui-no"), new Rect2(box.Position + new Vector2(5, 4), new Vector2(20, 20)), false);
        for (int i = 0; i < kinds.Count; i++)
            DrawTextureRect(Ui.Icon($"found-{kinds[i].Id}"), new Rect2(box.Position + new Vector2(5 + i * 22, 4), new Vector2(20, 20)), false);
        DrawString(Ui.Bold, box.Position + new Vector2(Mathf.Max(1, kinds.Count) * 22 + 8, 19), lv, HorizontalAlignment.Left, -1, 13, Ui.Ink);
        DrawArc(p, 18, 0, Mathf.Tau, 32, (kinds.Count == 0 ? Ui.Bad : Colors.White) with { A = 0.7f }, 1.5f);
    }
}
