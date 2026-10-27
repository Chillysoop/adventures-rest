using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The cause-and-effect lens at the head of the Council window's Blocs page, after Democracy 4: each bloc an icon in the
/// middle, sized by its numbers and ringed by how it feels about the mayor, with the laws in force on the left and the
/// situations and trust on the right. A green line is a push up, a red one a push down, thicker for more. Hover any
/// node to light its lines and see why.
/// </summary>
public partial class BlocLens : Control
{
    private sealed record Node(string Id, string Icon, string Label, Vector2 At, float Radius, Color Ring, string Tip);

    private sealed record Edge(string From, string To, double Points);

    private readonly List<Node> _nodes = [];
    private readonly List<Edge> _edges = [];
    private string _hover = "";
    private string _sig = "";

    public BlocLens()
    {
        CustomMinimumSize = new Vector2(0, 300);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseFilter = MouseFilterEnum.Stop;
        SetMeta("keep", true);
        TooltipText = " ";
        Resized += () => _sig = "";
    }

    /// <summary>Read the town; lays out again only when what it shows has changed.</summary>
    public void Show(Town t)
    {
        var blocs = Politics.Blocs(t);
        var pushes = blocs.Select(b => (Bloc: b, Pushes: Politics.Pushes(t, b))).ToList();
        string sig = $"{Size.X:0}|" + string.Join(";", pushes.Select(x => $"{x.Bloc.Id}:{Math.Round(Politics.Feeling(t, x.Bloc.Id))}:{string.Join(",", x.Pushes.Select(p => $"{p.Law}{Math.Round(p.Points)}"))}"));
        if (sig == _sig) return;
        _sig = sig;
        _nodes.Clear();
        _edges.Clear();
        float w = Math.Max(360, Size.X), h = CustomMinimumSize.Y;

        // Sources down the sides: laws on the left, situations and trust on the right.
        var sources = pushes.SelectMany(x => x.Pushes.Select(p => (p.Law, p.What))).DistinctBy(p => p.Law).ToList();
        var left = sources.Where(s => !s.Law.StartsWith("situation:", StringComparison.Ordinal) && s.Law != "trust").ToList();
        var right = sources.Except(left).ToList();
        void Column(List<(string Law, string What)> list, float x)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var (law, what) = list[i];
                float y = h * (i + 1) / (list.Count + 1);
                var hits = pushes.Select(p => (p.Bloc, Points: p.Pushes.Where(q => q.Law == law).Sum(q => q.Points))).Where(p => Math.Abs(p.Points) >= 0.5).ToList();
                string tip = $"{what}\n" + string.Join("\n", hits.OrderByDescending(p => p.Points).Select(p => $"{(p.Points > 0 ? "▲" : "▼")} {Ui.Cap(p.Bloc.Voice)} {(p.Points > 0 ? "+" : "")}{Math.Round(p.Points)}"));
                _nodes.Add(new Node(law, Main.LawIcon(t, law), what, new Vector2(x, y), 9, Ui.Ink2, tip));
            }
        }
        Column(left, 150);
        Column(right, w - 150);

        // The blocs round an oval in the middle.
        double most = Math.Max(1, blocs.Select(b => Politics.Size(t, b)).DefaultIfEmpty(1).Max());
        for (int i = 0; i < blocs.Count; i++)
        {
            var b = blocs[i];
            double a = -Math.PI / 2 + Math.PI * 2 * i / Math.Max(1, blocs.Count);
            var at = new Vector2(w / 2 + (float)Math.Cos(a) * Math.Max(60, w / 2 - 220), h / 2 + (float)Math.Sin(a) * (h / 2 - 34));
            double f = Politics.Feeling(t, b.Id), size = Politics.Size(t, b);
            var cell = Politics.CellOf(t, b.Id);
            string why = string.Join("\n", pushes[i].Pushes.OrderByDescending(p => Math.Abs(p.Points)).Take(6).Select(p => $"{(p.Points > 0 ? "▲" : "▼")} {p.What} {(p.Points > 0 ? "+" : "")}{Math.Round(p.Points)}"));
            double choices = Blocs.Feeling(t, b.Id);
            string tip = $"{Ui.Cap(b.Voice)}: {Math.Round(f)}\n{Math.Round(size)} townsfolk\n" + (why != "" ? why + "\n" : "")
                + (Math.Abs(choices) >= 1 ? $"{(choices > 0 ? "▲" : "▼")} Your choices {(choices > 0 ? "+" : "")}{Math.Round(choices)}\n" : "")
                + $"Likes {string.Join(", ", b.Likes)}; dislikes {string.Join(", ", b.Dislikes)}" + (cell != null ? "\nRadicals at work!" : "");
            _nodes.Add(new Node("bloc:" + b.Id, b.Icon, b.Voice, at, 14 + (float)(10 * Math.Sqrt(size / most)), Feel(f), tip));
            foreach (var p in pushes[i].Pushes) _edges.Add(new Edge(p.Law, "bloc:" + b.Id, p.Points));
        }
        QueueRedraw();
    }

    private static Color Feel(double f) => f >= 15 ? Ui.Good : f <= -15 ? Ui.Bad : Ui.Gold;

    private Node? At(Vector2 p) => _nodes.LastOrDefault(n => n.At.DistanceTo(p) <= n.Radius + 6);

    public override void _GuiInput(InputEvent e)
    {
        if (e is not InputEventMouseMotion m) return;
        string id = At(m.Position)?.Id ?? "";
        if (id == _hover) return;
        _hover = id;
        QueueRedraw();
    }

    public override string _GetTooltip(Vector2 atPosition) => At(atPosition)?.Tip ?? "";

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(Ui.Panel2, 0.6f));
        var byId = _nodes.ToDictionary(n => n.Id);
        foreach (var e in _edges.OrderBy(e => Lit(e) ? 1 : 0))
        {
            if (!byId.TryGetValue(e.From, out var a) || !byId.TryGetValue(e.To, out var b)) continue;
            bool lit = Lit(e);
            var c = e.Points > 0 ? Ui.Good : Ui.Bad;
            float alpha = _hover == "" ? 0.45f : lit ? 0.95f : 0.08f;
            float width = Math.Clamp((float)Math.Abs(e.Points) / 4f, 1, 5);
            var mid = (a.At + b.At) / 2 + new Vector2(0, (b.At.Y - a.At.Y) * -0.15f);
            DrawPolyline([a.At, mid, b.At], new Color(c, alpha), width, true);
        }
        foreach (var n in _nodes)
        {
            bool dim = _hover != "" && n.Id != _hover && !_edges.Any(e => Lit(e) && (e.From == n.Id || e.To == n.Id));
            float alpha = dim ? 0.35f : 1;
            DrawCircle(n.At, n.Radius + 3, new Color(n.Ring, alpha));
            DrawCircle(n.At, n.Radius + 1, new Color(Ui.Bg, alpha));
            var icon = Ui.Icon(n.Icon);
            float s = n.Radius * 1.3f;
            DrawTextureRect(icon, new Rect2(n.At - new Vector2(s, s) / 2, new Vector2(s, s)), false, new Color(1, 1, 1, alpha));
            if (!n.Id.StartsWith("bloc:", StringComparison.Ordinal))
            {
                // Laws read outward from their dot: to the left on the left, to the right on the right.
                bool onLeft = n.At.X < Size.X / 2;
                string label = n.Label.Length > 24 ? n.Label[..23] + "…" : n.Label;
                float tw = Ui.Body.GetStringSize(label, HorizontalAlignment.Left, -1, 11).X;
                float x = onLeft ? n.At.X - n.Radius - 6 - tw : n.At.X + n.Radius + 6;
                DrawString(Ui.Body, new Vector2(x, n.At.Y + 4), label, HorizontalAlignment.Left, -1, 11, new Color(Ui.Ink2, alpha));
            }
            else
            {
                string label = n.Label.StartsWith("the ", StringComparison.Ordinal) ? n.Label[4..] : n.Label;
                var font = Ui.Body;
                float tw = font.GetStringSize(label, HorizontalAlignment.Left, -1, 11).X;
                DrawString(font, new Vector2(n.At.X - tw / 2, n.At.Y + n.Radius + 15), label, HorizontalAlignment.Left, -1, 11, new Color(Ui.Ink, alpha));
            }
        }
    }

    private bool Lit(Edge e) => _hover != "" && (e.From == _hover || e.To == _hover);
}
