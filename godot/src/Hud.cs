using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The screen layout, after Foundation and its kin: the map fills the screen; a resource bar runs
/// along the top with the clock and speeds; the management windows open from a dock at the bottom
/// and float over the map; news slides in on the right; the inspector sits bottom left; the corner
/// map, the campaign-map toggle and the marker palette sit bottom right.
/// </summary>
public partial class Main
{
    private const int TopBar = 58, DockHeight = 58, Gap = 12;

    private PanelContainer _window = null!, _inspector = null!;
    private Label _windowTitle = null!;
    private Tab? _windowTab;
    private VBoxContainer _feed = null!;
    private double _clockReal;
    private Button _strategicButton = null!;
    private Label _help = null!;

    private static readonly Dictionary<Tab, string> Titles = new()
    {
        [Tab.Ledger] = "Ledger",
        [Tab.War] = "War",
        [Tab.Council] = "Council",
        [Tab.Bounties] = "Bounties and the road",
        [Tab.Companies] = "Adventuring companies",
        [Tab.Faith] = "Faith and factions",
        [Tab.Campaign] = "The campaign",
        [Tab.Chronicles] = "Chronicles",
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

        AddChild(BuildTopBar());
        BuildPaused();
        BuildWindow();
        BuildInspector();
        BuildFeed();
        BuildDock();
        BuildBuildBar();
        BuildCorner();

        _endOverlay = new ColorRect { Color = new Color(0.08f, 0.055f, 0.03f, 0.55f), Visible = false };
        AddChild(_endOverlay);
        _endOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        BuildLedgerPane();
    }

    // ---------- Top bar: name, resources, clock, threat, speeds ----------

    private Control BuildTopBar()
    {
        var panel = new PanelContainer();
        var style = Ui.Flat(new Color(0.07f, 0.06f, 0.045f, 0.92f), 16, 6);
        style.BorderColor = Ui.Line;
        style.BorderWidthBottom = 1;
        style.ShadowColor = new Color(0, 0, 0, 0.3f);
        style.ShadowSize = 6;
        panel.AddThemeStyleboxOverride("panel", style);
        panel.SetAnchorsPreset(LayoutPreset.TopWide);
        panel.OffsetBottom = TopBar;

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 16);
        panel.AddChild(bar);
        var title = new VBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        title.AddThemeConstantOverride("separation", -4);
        _townName = Ui.Label("Briarford", Ui.Ink, Ui.Display, 20);
        title.AddChild(_townName);
        title.AddChild(Ui.Label("Crown & Compass", Ui.Ink2, Ui.Medium, 10));
        bar.AddChild(title);
        bar.AddChild(new VSeparator());

        _stats = new HBoxContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _stats.AddThemeConstantOverride("separation", 18);
        bar.AddChild(_stats);
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _clock = Ui.Label("Day 1 · 00:00", Ui.Ink, Ui.Display, 16);
        _clock.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _clock.CustomMinimumSize = new Vector2(124, 0);
        bar.AddChild(_clock);
        _threatPill = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        _threat = Ui.Label("QUIET", Ui.Ink, Ui.Bold, 11);
        _threatPill.AddChild(_threat);
        bar.AddChild(_threatPill);

        var speeds = new PanelContainer { SizeFlagsVertical = SizeFlags.ShrinkCenter };
        speeds.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line, 10, 1, 3, 3));
        var sh = new HBoxContainer();
        sh.AddThemeConstantOverride("separation", 2);
        speeds.AddChild(sh);
        string[] labels = ["❚❚", "1×", "4×", "12×", "36×"];
        string[] tips = ["Pause (space)", "Normal (1)", "Fast (2)", "Faster (3)", "Fastest (4)"];
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
        _seedLabel = Ui.Label("seed 1", Ui.Ink2, null, 11);
        _seedLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(_seedLabel);
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
        // Under the alert strip, centred.
        BuildAlerts().AddChild(pill);
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
        win.AddThemeStyleboxOverride("panel", Glass(12));
        body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 0);
        win.AddChild(body);
        var head = new PanelContainer { MouseDefaultCursorShape = CursorShape.Move };
        var hs = Ui.Flat(new Color(Ui.Panel2, 0.95f), 14, 8);
        hs.CornerRadiusTopLeft = hs.CornerRadiusTopRight = 12;
        hs.BorderColor = Ui.Line;
        hs.BorderWidthBottom = 1;
        head.AddThemeStyleboxOverride("panel", hs);
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
        _window.OffsetLeft = Gap;
        _window.OffsetRight = Gap + 460;
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
        _inspector.OffsetTop = _inspector.OffsetBottom - 380;
        PlaceInspector();
    }

    /// <summary>Bottom left, or beside the management window when one is open there.</summary>
    private void PlaceInspector()
    {
        float left = _window.Visible && _window.OffsetLeft < 200 ? _window.OffsetRight + Gap : Gap;
        _inspector.OffsetLeft = left;
        _inspector.OffsetRight = left + 390;
    }

    private void OpenWindow(Tab t)
    {
        _windowTab = t;
        _windowTitle.Text = Titles[t];
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

    private void SyncDock()
    {
        foreach (var (k, b) in _tabButtons) b.ButtonPressed = _window.Visible && _windowTab == k;
        PlaceInspector();
    }

    // ---------- The dock ----------

    private void BuildDock()
    {
        var dock = new PanelContainer();
        dock.AddThemeStyleboxOverride("panel", Glass(14, 8, 6));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        dock.AddChild(row);
        foreach (var t in Enum.GetValues<Tab>())
        {
            if (t == Tab.Inspect) continue;
            var b = new Button { Text = t.ToString(), ThemeTypeVariation = "Tab", ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(92, 0) };
            b.Pressed += () => SetTab(t);
            row.AddChild(b);
            _tabButtons[t] = b;
            if (t == Tab.Council)
            {
                _motionDot = new ColorRect { Color = Ui.Accent, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                b.AddChild(_motionDot);
                _motionDot.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
                _motionDot.OffsetLeft = -10;
                _motionDot.OffsetRight = -4;
                _motionDot.OffsetTop = 8;
                _motionDot.OffsetBottom = 14;
            }
        }
        row.AddChild(new VSeparator());
        _buildToggle = new Button { Text = "Build", ThemeTypeVariation = "Tab", ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = "Zone land and place government buildings (B)", CustomMinimumSize = new Vector2(92, 0) };
        _buildToggle.Pressed += ToggleBuildBar;
        row.AddChild(_buildToggle);
        AddChild(dock);
        _dock = dock;
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap);
        dock.GrowHorizontal = GrowDirection.Both;
        dock.GrowVertical = GrowDirection.Begin;

        _help = Ui.Label(MapHelp, new Color("f6eedb", 0.5f), Ui.Medium, 11);
        _help.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_help);
        _help.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap + DockHeight + 4);
        _help.GrowHorizontal = GrowDirection.Both;
        _help.GrowVertical = GrowDirection.Begin;
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
        var text = Ui.Rich($"{Ui.C(color, $"[font_size=10][b]{e.Category.ToString().ToUpperInvariant()}[/b][/font_size]")}  {Ui.C(Ui.Ink2, $"[font_size=10]Day {e.Tick / SimTime.HoursPerDay + 1}[/font_size]")}\n[font_size=13]{LinkNames(e)}[/font_size]");
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
        while (_feed.GetChildCount() > (_guideCard.Visible ? 3 : 5)) _feed.GetChild(_feed.GetChildCount() - 1).Free();
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
        foreach (var c in _feed.GetChildren()) c.Free();
        ClearAlerts();
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
        corner.AddChild(tools);

        var frame = new PanelContainer();
        frame.AddThemeStyleboxOverride("panel", Glass(10, 6, 6));
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
