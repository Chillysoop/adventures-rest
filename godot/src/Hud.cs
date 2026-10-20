using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The screen layout, after Foundation and its kin: the map fills the screen; a resource bar runs
/// along the top with the clock and speeds; the management windows open from a rail down the left
/// and float over the map, and the charter goals of the first days sit beside the rail under the top bar;
/// the action tray and the news sit on the right; the inspector sits bottom left; the corner
/// map, the campaign-map toggle and the marker palette sit bottom right.
/// </summary>
public partial class Main
{
    private const int TopBar = 58, DockHeight = 48, Gap = 12;

    private PanelContainer _window = null!, _inspector = null!;
    private Label _windowTitle = null!;
    private Tab? _windowTab;
    private VBoxContainer _feed = null!;
    private double _clockReal;
    private Button _strategicButton = null!;
    private Label _help = null!;

    private static readonly Dictionary<Tab, string> Titles = new()
    {
        [Tab.City] = "The city",
        [Tab.Ledger] = "Ledger",
        [Tab.War] = "War",
        [Tab.Council] = "Council",
        [Tab.Bounties] = "Bounties and the road",
        [Tab.Companies] = "Adventurers' roster",
        [Tab.Armory] = "The town armory",
        [Tab.Diplomacy] = "Diplomacy and trade",
        [Tab.Institutions] = "The town's institutions",
        [Tab.Faith] = "Faith and factions",
        [Tab.Campaign] = "The campaign",
        [Tab.Chronicles] = "Chronicles",
        [Tab.Mayor] = "The mayor",
        [Tab.Library] = "The library",
        [Tab.Inspect] = "Inspect",
    };

    private static StyleBoxFlat Glass(int radius = 12, int padX = 0, int padY = 0)
    {
        var s = Ui.Box(new Color(0.09f, 0.075f, 0.055f, 0.96f), new Color(Ui.Line, 0.9f), radius, 1, padX, padY);
        s.ShadowColor = new Color(0, 0, 0, 0.35f);
        s.ShadowSize = 8;
        return s;
    }

    private void BuildLayout()
    {
        _map = new MapView();
        AddChild(_map);
        _map.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _map.Picked += sel => Select(sel);
        _map.FlagDropped += OnFlagDropped;

        AddChild(BuildTopBar());
        BuildPaused();
        BuildWindow();
        BuildInspector();
        BuildFeed();
        BuildDock();
        BuildGoals();
        BuildTray();
        BuildBuildBar();
        BuildCorner();
        BuildLensBar();

        _endOverlay = new ColorRect { Color = new Color(0.08f, 0.055f, 0.03f, 0.55f), Visible = false };
        AddChild(_endOverlay);
        _endOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLedgerPane();
    }

    // ---------- Top bar: name, resources, clock, threat, speeds ----------

    private Control BuildTopBar()
    {
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Castle.Parapet());
        panel.SetAnchorsPreset(LayoutPreset.TopWide);
        panel.OffsetBottom = TopBar;

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 10);
        panel.AddChild(bar);
        var title = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        title.AddThemeConstantOverride("separation", -4);
        _townName = Ui.Label("Briarford", Ui.Ink, Ui.Display, 20);
        title.AddChild(_townName);
        _tierLabel = Ui.Label("Thorp", Ui.Ink2, Ui.Medium, 10);
        Ui.KeywordOn(_tierLabel, "charter", () => "Open the City window to see what the next charter asks for.");
        title.AddChild(_tierLabel);
        bar.AddChild(title);
        bar.AddChild(new VSeparator());

        _stats = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _stats.AddThemeConstantOverride("separation", 2);
        bar.AddChild(_stats);
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _clock = Ui.Label("Day 1 · 00:00", Ui.Ink, Ui.Display, 16);
        _clock.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _clock.CustomMinimumSize = new Vector2(176, 0);
        _clock.MouseFilter = MouseFilterEnum.Pass;
        bar.AddChild(_clock);
        _threatPill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        Ui.KeywordOn(_threatPill, "warband", () => Stages[_sim.Town.Enemy.Stage].Text);
        _threat = Ui.Label("QUIET", Ui.Ink, Ui.Bold, 11);
        _threatPill.AddChild(_threat);
        bar.AddChild(_threatPill);

        var speeds = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        speeds.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line, 10, 1, 3, 3));
        var sh = new HBoxContainer();
        sh.AddThemeConstantOverride("separation", 2);
        speeds.AddChild(sh);
        string[] labels = ["❚❚", "1×", "4×", "12×", "36×"];
        string[] tips = ["Pause (space)", "Normal (1): a day lasts a minute", "Fast (2): a day in 15 seconds", "Faster (3): a day in 5 seconds", "Fastest (4): a day in under 2 seconds"];
        for (int i = 0; i < 5; i++)
        {
            int k = i;
            var b = new Button { Text = labels[i], ThemeTypeVariation = "Speed", ToggleMode = true, TooltipText = tips[i], CustomMinimumSize = new Vector2(38, 0) };
            b.Pressed += () => PlayerSpeed(k);
            sh.AddChild(b);
            _speedButtons[i] = b;
        }
        bar.AddChild(speeds);
        _speedsBox = speeds;
        var book = new Button { Icon = Ui.Icon("glossary"), TooltipText = "Glossary: every keyword explained (F1)", SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None, ExpandIcon = false };
        book.AddThemeConstantOverride("icon_max_width", 20);
        book.Pressed += () => OpenGlossary();
        bar.AddChild(book);
        _glossaryButton = book;
        var menu = new Button { Text = "Menu", TooltipText = "Save, load or start a new town (Esc)", SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        menu.Pressed += () => ToggleMenu();
        bar.AddChild(menu);
        _menuButton = menu;
        return panel;
    }

    private void BuildPaused()
    {
        _pausedPill = Ui.Label("PAUSED", new Color("f6eedb"), Ui.Bold, 12);
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkCenter };
        pill.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0.08f, 0.06f, 0.04f, 0.8f), 12, 4, 99));
        pill.AddChild(_pausedPill);
        // Under the alert strip, centred, with the siege banner below it.
        var col = BuildAlerts();
        col.AddChild(pill);
        BuildSiegeBanner(col);
    }

    // ---------- Management windows ----------

    private ScrollContainer MakeScroll(Tab t)
    {
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false };
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var s in new[] { "left", "right" }) pad.AddThemeConstantOverride($"margin_{s}", 14);
        pad.AddThemeConstantOverride("margin_top", 6);
        pad.AddThemeConstantOverride("margin_bottom", 14);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        pad.AddChild(box);
        scroll.AddChild(pad);
        _scrolls[t] = scroll;
        _panes[t] = new Pane(box);
        return scroll;
    }

    /// <summary>A floating window with a title bar you can drag it by and a close button.</summary>
    private PanelContainer Floating(out VBoxContainer body, out Label title, Action close)
    {
        var win = new PanelContainer { Visible = false };
        win.AddThemeStyleboxOverride("panel", Castle.Oak(4, 4));
        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        win.AddChild(body);
        var head = new PanelContainer { MouseDefaultCursorShape = CursorShape.Move };
        head.AddThemeStyleboxOverride("panel", Castle.Beam(16, 8));
        var hb = new HBoxContainer();
        head.AddChild(hb);
        title = Ui.Label("", Ui.Ink, Ui.Display, 16);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        title.MouseFilter = MouseFilterEnum.Ignore;
        hb.AddChild(title);
        var x = new Button { Text = "✕", ThemeTypeVariation = "Speed", TooltipText = "Close (Esc)", FocusMode = FocusModeEnum.None };
        x.Pressed += close;
        hb.AddChild(x);
        head.GuiInput += e =>
        {
            if (e is InputEventMouseMotion mm && (mm.ButtonMask & MouseButtonMask.Left) != 0)
            {
                var vp = GetViewportRect().Size;
                var to = (win.Position + mm.Relative).Clamp(new Vector2(-win.Size.X + 80, TopBar), vp - new Vector2(80, 40));
                var d = to - win.Position;
                win.OffsetLeft += d.X;
                win.OffsetRight += d.X;
                win.OffsetTop += d.Y;
                win.OffsetBottom += d.Y;
            }
        };
        body.AddChild(head);
        return win;
    }

    private void BuildWindow()
    {
        _window = Floating(out var body, out _windowTitle, () => CloseWindow());
        foreach (var t in Enum.GetValues<Tab>())
            if (t != Tab.Inspect) body.AddChild(MakeScroll(t));
        AddChild(_window);
        _window.AnchorTop = 0;
        _window.AnchorBottom = 1;
        _window.OffsetLeft = RailWidth + 6;
        _window.OffsetRight = RailWidth + 6 + 460;
        _window.OffsetTop = TopBar + Gap;
        _window.OffsetBottom = -(DockHeight + Gap * 2 + 16);
    }

    private void BuildInspector()
    {
        _inspector = Floating(out var body, out var title, () => Select(null));
        title.Text = "Inspect";
        body.AddChild(MakeScroll(Tab.Inspect));
        _scrolls[Tab.Inspect].Visible = true;
        AddChild(_inspector);
        _inspector.AnchorTop = _inspector.AnchorBottom = 1;
        _inspector.OffsetBottom = -(DockHeight + Gap * 2 + 16);
        _inspector.OffsetTop = _inspector.OffsetBottom - 540;
        PlaceInspector();
    }

    /// <summary>Bottom left, or beside the management window when one is open there.</summary>
    private void PlaceInspector()
    {
        float left = _window.Visible && _window.OffsetLeft < 200 ? _window.OffsetRight + Gap : RailWidth + 6;
        _inspector.OffsetLeft = left;
        _inspector.OffsetRight = left + 390;
    }

    private void OpenWindow(Tab t)
    {
        _windowTab = t;
        _windowTitle.Text = Titles[t];
        // The roster is a table: its window opens wide.
        _window.OffsetRight = _window.OffsetLeft + (t == Tab.Companies ? 900 : 460);
        foreach (var (k, s) in _scrolls)
            if (k != Tab.Inspect) s.Visible = k == t;
        _window.Visible = true;
        SyncDock();
        _dirty = true;
        GuideEvent($"tab:{t}");
    }

    private void CloseWindow()
    {
        _window.Visible = false;
        _windowTab = null;
        SyncDock();
    }

    // ---------- News feed ----------

    private void BuildFeed()
    {
        _feed = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0), MouseFilter = MouseFilterEnum.Ignore };
        _feed.AddThemeConstantOverride("separation", 6);
        AddChild(_feed);
        _feed.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize, Gap);
        _feed.GrowHorizontal = GrowDirection.Begin;
        _feed.GrowVertical = GrowDirection.End;
        _feed.OffsetTop = TopBar + Gap;
        // Clear of the action tray on the right edge.
        _feed.OffsetLeft -= TrayWidth;
        _feed.OffsetRight -= TrayWidth;
    }

    /// <summary>The news worth looking up for: turning points, the war, quests, the council, faith and the factions.</summary>
    private static bool Newsworthy(LedgerEntry e) =>
        e.Importance == Importance.Major
        || (e.Importance == Importance.Notable && e.Category is LedgerCategory.War or LedgerCategory.Quest or LedgerCategory.Faith or LedgerCategory.Faction)
        || (e.Category == LedgerCategory.Council && e.Text.Contains(" moves that "));

    private void Feed(LedgerEntry e)
    {
        var card = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        var color = e.Importance == Importance.Major ? Ui.Gold : e.Category == LedgerCategory.War ? Ui.Bad : Ui.Ink2;
        var s = Glass(10, 12, 8);
        s.BorderColor = color;
        s.BorderWidthLeft = 3;
        card.AddThemeStyleboxOverride("panel", s);
        var text = Ui.Rich($"{Ui.C(color, $"[font_size=11][b]{e.Category.ToString().ToUpperInvariant()}[/b][/font_size]")}  {Ui.C(Ui.Ink2, $"[font_size=11]Day {e.Tick / SimTime.HoursPerDay + 1}[/font_size]")}\n[font_size=13]{LinkNames(e)}[/font_size]");
        card.AddChild(text);
        card.SetMeta("until", _clockReal + (e.Importance == Importance.Major ? 30 : 14));
        card.GuiInput += ev =>
        {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) GoTo(e);
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) card.QueueFree();
        };
        _feed.AddChild(card);
        _feed.MoveChild(card, 0);
        // Fewer while the tutorial card sits below the feed.
        while (_feed.GetChildCount() > (_guideCard.Visible ? 3 : 5))
        {
            var old = _feed.GetChild(_feed.GetChildCount() - 1);
            _feed.RemoveChild(old);
            old.QueueFree();
        }
    }

    private void AgeFeed(double delta)
    {
        _clockReal += delta;
        foreach (var c in _feed.GetChildren().OfType<Control>())
        {
            double left = (double)c.GetMeta("until") - _clockReal;
            if (left <= 0) c.QueueFree();
            else c.Modulate = new Color(1, 1, 1, (float)Math.Min(1, left / 1.5));
        }
    }

    private void ClearFeed()
    {
        Ui.Clear(_feed);
        ClearAlerts();
    }

    // ---------- Charter goals ----------

    private const float GoalsWidth = 292;
    private PanelContainer _goalsCard = null!;
    private VBoxContainer _goalsRows = null!;
    private Label _goalsCount = null!;
    private string _goalsSig = "";
    private Simulation? _goalsSim;
    private int _goalsDone;
    private readonly List<Control> _goalsFlourish = [];

    /// <summary>The first days' goals (Systems/Goals.cs): beside the rail under the top bar, clear of the alert strip and the news.</summary>
    private void BuildGoals()
    {
        _goalsCard = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(GoalsWidth, 0) };
        _goalsCard.AddThemeStyleboxOverride("panel", Castle.Oak(16, 12));
        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddThemeConstantOverride("separation", 6);
        _goalsCard.AddChild(v);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        var title = Ui.Label("CHARTER GOALS", Ui.Gold, Ui.Bold, 10);
        title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        head.AddChild(title);
        _goalsCount = Ui.Label("", Ui.Ink2, Ui.Medium, 10);
        head.AddChild(_goalsCount);
        v.AddChild(head);
        _goalsRows = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _goalsRows.AddThemeConstantOverride("separation", 8);
        v.AddChild(_goalsRows);
        AddChild(_goalsCard);
        _goalsCard.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft, LayoutPresetMode.Minsize);
        _goalsCard.OffsetLeft = RailWidth + Gap;
        _goalsCard.OffsetRight = RailWidth + Gap + GoalsWidth;
        _goalsCard.OffsetTop = _goalsCard.OffsetBottom = TopBar + Gap;
        _goalsCard.GrowVertical = GrowDirection.End;
    }

    /// <summary>Every frame: rebuild the card when the goals move on, flourish the ones just met, and stay out of the way.</summary>
    private void SyncGoals()
    {
        var t = _sim.Town;
        if (_goalsSim != _sim)
        {
            // A new town or a loaded one: what is already met is no news.
            _goalsSim = _sim;
            _goalsDone = t.Goals.Done.Count;
            _goalsSig = "";
            foreach (var c in _goalsFlourish) c.QueueFree();
            _goalsFlourish.Clear();
        }
        if (t.Goals.Done.Count > _goalsDone)
        {
            foreach (var id in t.Goals.Done.Skip(_goalsDone))
                if (t.Content.Goals.Goals.FirstOrDefault(g => g.Id == id) is GoalDef met) _goalsFlourish.Add(MetRow(met));
            _goalsDone = t.Goals.Done.Count;
            _goalsSig = "";
        }
        if (!_dirty && _goalsSig != "" && t.Goals.Closed && _goalsFlourish.Count == 0)
        {
            _goalsCard.Visible = false;
            return;
        }
        var open = Goals.Open(t);
        if (_dirty || _goalsSig == "")
        {
            string sig = string.Join("|", open.Select(g => $"{g.Id}:{Goals.Progress(t, g).Now}")) + $"#{_goalsFlourish.Count}";
            if (sig != _goalsSig)
            {
                _goalsSig = sig;
                foreach (var c in _goalsRows.GetChildren().OfType<Control>().Where(c => !_goalsFlourish.Contains(c)).ToList())
                {
                    _goalsRows.RemoveChild(c);
                    c.QueueFree();
                }
                foreach (var g in open) _goalsRows.AddChild(GoalRow(t, g));
                _goalsCount.Text = $"{t.Goals.Done.Count} OF {t.Content.Goals.Goals.Count}";
            }
        }
        bool any = open.Count > 0 || _goalsFlourish.Count > 0;
        bool covered = _window.Visible || _title.Visible || _menu.Visible || _endOverlay.Visible
            || (_inspector.Visible && _inspector.GetGlobalRect().Intersects(_goalsCard.GetGlobalRect()));
        _goalsCard.Visible = any && !covered;
        // Hug the rows: wrapped text first measures tall before it knows its width, a row leaves as a goal is
        // met, and a container only ever grows on its own.
        if (_goalsCard.Visible) _goalsCard.ResetSize();
    }

    private static Control GoalRow(Town t, GoalDef g)
    {
        var (now, target) = Goals.Progress(t, g);
        var row = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 3);
        var text = Ui.Label(g.Text, Ui.Ink, Ui.Medium, 13);
        text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        row.AddChild(text);
        var bar = new ProgressBar { MinValue = 0, MaxValue = target, Value = now, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 4), MouseFilter = MouseFilterEnum.Ignore };
        bar.AddThemeStyleboxOverride("background", Ui.Flat(new Color(Ui.Line, 0.9f), 0, 0, 2));
        bar.AddThemeStyleboxOverride("fill", Ui.Flat(Ui.Gold, 0, 0, 2));
        row.AddChild(bar);
        var info = Ui.Rich($"{Ui.C(Ui.Ink2, $"{Ui.Esc(g.Label)} {now}/{target}")}   {Ui.C(Ui.Gold, Ui.Esc(Goals.Reward(g)))}", 11);
        info.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(info);
        return row;
    }

    /// <summary>A met goal's last moment on the card: ticked and gilded, then gone, and the next goal in its place.</summary>
    private Control MetRow(GoalDef g)
    {
        var row = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeStyleboxOverride("panel", Ui.Box(new Color(Ui.Gold, 0.14f), Ui.Gold, 6, 1, 8, 4));
        var text = Ui.Rich($"{Ui.C(Ui.Good, "[b]✓[/b]")}  [s]{Ui.Esc(g.Text)}[/s]\n{Ui.C(Ui.Gold, $"[font_size=11]{Ui.Esc(Goals.Reward(g))}[/font_size]")}", 13);
        text.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(text);
        _goalsRows.AddChild(row);
        _goalsRows.MoveChild(row, 0);
        row.Modulate = new Color(1.6f, 1.4f, 0.8f, 0);
        var tw = row.CreateTween();
        tw.TweenProperty(row, "modulate", new Color(1.4f, 1.25f, 0.85f, 1), 0.25);
        tw.TweenProperty(row, "modulate", Colors.White, 0.5);
        tw.TweenInterval(2.2);
        tw.TweenProperty(row, "modulate:a", 0.0, 0.6);
        tw.TweenCallback(Callable.From(() =>
        {
            _goalsFlourish.Remove(row);
            row.QueueFree();
            _goalsSig = "";
        }));
        return row;
    }


    // ---------- Corner: minimap, campaign map, markers ----------

    private void BuildCorner()
    {
        var corner = new VBoxContainer();
        corner.AddThemeConstantOverride("separation", 6);

        corner.AddChild(BuildMarkerBar());

        var tools = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        tools.AddThemeConstantOverride("separation", 4);
        _strategicButton = new Button { Text = "Campaign map", ToggleMode = true, TooltipText = "The same land as an inked campaign map, for planning and markers (M)", FocusMode = FocusModeEnum.None };
        _strategicButton.Pressed += ToggleStrategic;
        tools.AddChild(_strategicButton);
        var home = new Button { Text = "Town", TooltipText = "Back to the town (Home, or double right-click)", FocusMode = FocusModeEnum.None };
        home.Pressed += () => _map.Recenter();
        tools.AddChild(home);
        tools.AddChild(BuildLensButton());
        corner.AddChild(tools);
        corner.AddChild(BuildOrders());

        var frame = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        frame.AddThemeStyleboxOverride("panel", Castle.Oak(8, 8));
        var mini = new Minimap(_map) { CustomMinimumSize = new Vector2(220, 220) };
        frame.AddChild(mini);
        _miniFrame = frame;
        corner.AddChild(frame);
        AddChild(corner);
        corner.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight, LayoutPresetMode.Minsize, Gap);
        corner.GrowHorizontal = GrowDirection.Begin;
        corner.GrowVertical = GrowDirection.Begin;
    }
}
