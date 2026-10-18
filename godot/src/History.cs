using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The world's history as one timeline (content/game/history.json): a column for each stretch of the
/// wilds, the ages down the side, and a block for every power that held the land, drawn the way a world
/// history poster stacks its empires. Opened from the main menu and the game menu; click a block for its story.
/// </summary>
public partial class Main
{
    private Control _history = null!;
    private HistoryChart _historyChart = null!;
    private RichTextLabel _historyStory = null!;

    private void BuildHistory()
    {
        var h = CrownAndCompass.Sim.Content.Default().History;
        _history = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _history.AddThemeStyleboxOverride("panel", Ui.Flat(Ui.Bg, 28, 20));
        AddChild(_history);
        _history.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        _history.AddChild(col);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        col.AddChild(head);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 2);
        head.AddChild(titles);
        titles.AddChild(Ui.Label("A History of the World", Ui.Ink, Ui.Display, 32));
        titles.AddChild(Ui.Label($"From the Age of Making to the charter of your town in {Annals.Year(h, h.Year)}, the years counted {h.EraName.ToLowerInvariant()} that ended the first Long Night", Ui.Ink2, Ui.Medium, 14));
        var close = new Button { Text = "Close", TooltipText = "Back (Esc)", SizeFlagsVertical = SizeFlags.ShrinkCenter };
        close.Pressed += CloseHistory;
        head.AddChild(close);

        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 18);
        col.AddChild(body);
        _historyChart = new HistoryChart(h) { SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ExpandFill };
        _historyChart.Picked += ShowPower;
        body.AddChild(_historyChart);

        var side = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        side.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line, 10, 1, 18, 16));
        body.AddChild(side);
        _historyStory = Ui.Rich("", 14);
        _historyStory.ScrollActive = true;
        _historyStory.FitContent = false;
        _historyStory.SizeFlagsVertical = SizeFlags.ExpandFill;
        side.AddChild(_historyStory);
        ShowPower(null);
    }

    private void OpenHistory(string? power = null)
    {
        _history.Visible = true;
        _history.MoveToFront();
        _historyChart.Select(power);
        ShowPower(power);
    }

    private void CloseHistory() => _history.Visible = false;

    /// <summary>The side panel: a power's story, or the milestones when nothing is picked.</summary>
    private void ShowPower(string? id)
    {
        var h = _historyChart.History;
        var p = h.Powers.FirstOrDefault(x => x.Id == id);
        if (p == null)
        {
            var lines = new List<string> { $"[font_size=18][b]Milestones[/b][/font_size]", Ui.C(Ui.Ink2, "Click a block on the timeline for its story."), "" };
            for (int i = 0; i < h.Events.Count; i++)
                lines.Add($"{Ui.C(Ui.Gold, $"[b]{i + 1}[/b]")}  {Ui.Esc(h.Events[i].Text)}  {Ui.C(Ui.Ink2, $"[font_size=12]{Annals.Year(h, h.Events[i].Year)}[/font_size]")}");
            lines.Add("");
            lines.AddRange(LegendLines());
            foreach (var age in h.Ages)
                lines.Add($"[b]{Ui.Esc(age.Name)}[/b]  {Ui.C(Ui.Ink2, $"[font_size=12]{Annals.Year(h, age.From)} to {Annals.Year(h, age.To)}[/font_size]")}\n{Ui.C(Ui.Ink2, Ui.Esc(age.Text))}\n");
            _historyStory.Text = string.Join("\n", lines);
            return;
        }
        string lanes = string.Join(", ", p.Lanes.Select(l => h.Lanes.First(x => x.Id == l).Name.ToLowerInvariant()));
        string until = p.To >= h.Year ? "to this day" : $"to {Annals.Year(h, p.To)}";
        _historyStory.Text =
            $"[font_size=20][b]{Ui.Esc(p.Name)}[/b][/font_size]\n" +
            $"{Ui.C(HistoryChart.Colour(p.Kind).Lightened(0.25f), $"{Annals.Year(h, p.From)} {until}")} · {Ui.C(Ui.Ink2, Ui.Esc(lanes))}\n\n" +
            $"{Ui.Esc(p.Text)}\n\n" +
            (p.Heirs != "" ? $"{Ui.C(Ui.Gold, "[b]What is left of it[/b]")}\n{Ui.Esc(p.Heirs)}\n\n" : "") +
            Ui.C(Ui.Ink2, "Click it again, or an empty stretch, for the milestones.");
    }
}

/// <summary>The timeline itself: lanes across, ages down (each as tall as its weight), powers as blocks.</summary>
public partial class HistoryChart : Control
{
    public HistoryRules History { get; }
    public event Action<string?>? Picked;
    private string? _selected;
    private string? _hover;
    private readonly List<(Rect2 Rect, string Id)> _blocks = [];

    private const float Axis = 168, Header = 34, Gap = 4;

    public HistoryChart(HistoryRules history)
    {
        History = history;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = true;
    }

    public void Select(string? id)
    {
        _selected = id;
        QueueRedraw();
    }

    public static Color Colour(string kind) => kind switch
    {
        "elder" => new Color("7d8aa3"),
        "elves" => new Color("6fae6a"),
        "dwarves" => new Color("c08a4a"),
        "serpent" => new Color("4fa39a"),
        "empire" => new Color("c0574c"),
        "folk" => new Color("c9b26b"),
        "free" => new Color("5b8fc9"),
        "league" => new Color("e8b93a"),
        "dark" => new Color("8e2f2f"),
        "frost" => new Color("8fb6c9"),
        "gnomes" => new Color("9a7fb8"),
        "fen" => new Color("7a9e8c"),
        "caravan" => new Color("d9a066"),
        "warrens" => new Color("8c6f5a"),
        _ => new Color("5a534a"),
    };

    /// <summary>Where a year sits down the chart: each age gets a share of the height by its weight.</summary>
    private float Y(int year)
    {
        var ages = History.Ages;
        double total = ages.Sum(a => a.Weight), top = Header, height = Size.Y - Header;
        foreach (var a in ages)
        {
            double h = height * a.Weight / total;
            if (year < a.To || a == ages[^1])
                return (float)(top + h * Math.Clamp((year - a.From) / (double)(a.To - a.From), 0, 1));
            top += h;
        }
        return Size.Y;
    }

    private float LaneWidth => (Size.X - Axis) / History.Lanes.Count;

    private float LaneX(string lane)
    {
        for (int i = 0; i < History.Lanes.Count; i++)
            if (History.Lanes[i].Id == lane) return Axis + i * LaneWidth;
        return Axis;
    }

    public override void _Draw()
    {
        var h = History;
        var font = Ui.Medium;
        var bold = Ui.Bold;
        _blocks.Clear();

        // Ages: bands down the left, with a rule across the chart at each boundary.
        for (int i = 0; i < h.Ages.Count; i++)
        {
            var a = h.Ages[i];
            float y0 = Y(a.From), y1 = Y(a.To);
            DrawRect(new Rect2(0, y0, Axis - 10, y1 - y0 - 2), new Color(Ui.Panel2, i % 2 == 0 ? 0.9f : 0.55f));
            string name = a.Name.Replace("The ", "");
            DrawMultilineString(bold, new Vector2(10, y0 + 20), name, HorizontalAlignment.Left, Axis - 36, 14, -1, Ui.Ink);
            float below = bold.GetMultilineStringSize(name, HorizontalAlignment.Left, Axis - 36, 14).Y;
            if (y1 - y0 > below + 34)
                DrawString(font, new Vector2(10, y0 + 18 + below), $"{Annals.Year(h, a.From)} to {Annals.Year(h, a.To)}", HorizontalAlignment.Left, Axis - 36, 11, Ui.Ink2);
            DrawLine(new Vector2(Axis, y0), new Vector2(Size.X, y0), new Color(Ui.Line, 0.8f), 1);
        }

        // Lane headers.
        foreach (var lane in h.Lanes)
            DrawString(bold, new Vector2(LaneX(lane.Id) + 4, 22), Ui.Cap(lane.Name.Replace("The ", "")), HorizontalAlignment.Center, LaneWidth - 8, 13, Ui.Ink2);

        // Powers.
        foreach (var p in h.Powers.OrderBy(p => p.From).ThenBy(p => p.Id, StringComparer.Ordinal))
        {
            float x = LaneX(p.Lanes[0]) + Gap / 2, w = LaneWidth * p.Lanes.Count - Gap;
            float y0 = Y(p.From), y1 = Math.Max(Y(p.To), y0 + 14);
            var rect = new Rect2(x, y0 + 1, w, y1 - y0 - 2);
            _blocks.Add((rect, p.Id));
            var c = Colour(p.Kind);
            bool on = p.Id == _selected, hot = p.Id == _hover;
            var fill = new StyleBoxFlat { BgColor = on ? c.Lightened(0.15f) : hot ? c.Lightened(0.08f) : c, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
            if (p.Kind == "sleeping") fill.BgColor = new Color(c, on || hot ? 0.95f : 0.75f);
            if (on)
            {
                fill.BorderColor = Ui.Ink;
                fill.SetBorderWidthAll(2);
            }
            DrawStyleBox(fill, rect);
            var ink = c.Luminance > 0.45f ? new Color("1d1912") : Ui.Ink;
            float hgt = rect.Size.Y;
            if (hgt >= 18)
            {
                int size = hgt > 60 && w > 150 ? 15 : 12, lines = hgt > 64 ? 3 : hgt > 44 ? 2 : 1;
                DrawMultilineString(bold, new Vector2(x + 7, y0 + 4 + size), p.Name, HorizontalAlignment.Left, w - 12, size, lines, ink);
                float used = bold.GetMultilineStringSize(p.Name, HorizontalAlignment.Left, w - 12, size, lines).Y;
                if (hgt > used + 36)
                    DrawMultilineString(font, new Vector2(x + 7, y0 + 18 + used), $"{Annals.Year(h, p.From)} to {(p.To >= h.Year ? "now" : Annals.Year(h, p.To))}", HorizontalAlignment.Left, w - 12, 11, 2, new Color(ink, 0.8f));
            }
        }

        // Milestones: numbered marks on the axis, as on a history poster.
        for (int i = 0; i < h.Events.Count; i++)
        {
            float y = Math.Min(Y(h.Events[i].Year), Size.Y - 9);
            var at = new Vector2(Axis - 22, y);
            DrawCircle(at, 9, Ui.Gold);
            DrawString(bold, at + new Vector2(-9, 4), (i + 1).ToString(), HorizontalAlignment.Center, 18, 11, new Color("1d1912"));
            DrawLine(at + new Vector2(9, 0), new Vector2(Axis, y), Ui.Gold, 1);
        }

        // Your town, at the foot of the heartland.
        var town = new Vector2(LaneX("heart") + LaneWidth / 2, Size.Y - 6);
        DrawColoredPolygon([town + new Vector2(0, -12), town + new Vector2(8, 0), town + new Vector2(0, 4), town + new Vector2(-8, 0)], Ui.Ink);
    }

    public override void _GuiInput(InputEvent e)
    {
        if (e is InputEventMouseMotion m)
        {
            string? hover = Hit(m.Position);
            if (hover != _hover)
            {
                _hover = hover;
                MouseDefaultCursorShape = hover != null ? CursorShape.PointingHand : CursorShape.Arrow;
                QueueRedraw();
            }
        }
        else if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } b)
        {
            string? id = Hit(b.Position);
            _selected = id == _selected ? null : id;
            QueueRedraw();
            Picked?.Invoke(_selected);
        }
    }

    private string? Hit(Vector2 at)
    {
        for (int i = _blocks.Count - 1; i >= 0; i--)
            if (_blocks[i].Rect.HasPoint(at)) return _blocks[i].Id;
        return null;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationResized) QueueRedraw();
    }
}
