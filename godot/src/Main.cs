using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The game client: runs the simulation in real time with pause, shows the town map with its fog
/// of war, and the Ledger, War, Council, Bounties, Chronicles and Inspect panes beside it.
/// </summary>
public partial class Main : Control
{
    /// <summary>In-game hours per real second for each speed button; index 0 is paused.</summary>
    private static readonly int[] Speeds = [0, 1, 4, 12, 36];
    private const int LedgerLimit = 250;

    private enum Tab
    {
        Ledger,
        War,
        Council,
        Bounties,
        Chronicles,
        Inspect,
    }

    private Simulation _sim = null!;
    private int _seed = 1;
    private MapView _map = null!;
    private int _speed = 1;
    private int _lastSpeed = 1;
    private double _acc;
    private bool _dirty = true;
    private Tab _tab = Tab.Ledger;
    private Importance _ledgerFilter = Importance.Notable;
    private bool _endShown;
    private readonly HashSet<int> _openChronicles = [];

    private Label _townName = null!, _clock = null!, _seedLabel = null!, _threat = null!;
    private PanelContainer _threatPill = null!;
    private readonly Button[] _speedButtons = new Button[5];
    private readonly Dictionary<Tab, Button> _tabButtons = [];
    private readonly Dictionary<Tab, ScrollContainer> _scrolls = [];
    private readonly Dictionary<Tab, Pane> _panes = [];
    private HBoxContainer _stats = null!;
    private ColorRect _motionDot = null!;
    private PanelContainer _toast = null!;
    private RichTextLabel _toastText = null!;
    private double _toastLeft;
    private Control _endOverlay = null!;
    private RichTextLabel _ledgerText = null!;
    private int _ledgerShown = -1;
    private Label _pausedPill = null!;

    private readonly Options _opts = Options.Parse(OS.GetCmdlineUserArgs());
    private int _frames;

    public override void _Ready()
    {
        Theme = Ui.BuildTheme();
        Ui.Links = OnLink;
        BuildLayout();
        NewTown(_opts.Seed ?? 1, _opts.Autoplay);
        if (_opts.Day > 0) _sim.RunDays(_opts.Day);
        if (_opts.Hour > 0) for (int i = 0; i < _opts.Hour; i++) _sim.Step();
        _opts.ApplyScript(_sim);
        SetSpeed(_opts.Speed ?? 1);
        if (_opts.Tab is string t && Enum.TryParse<Tab>(t, true, out var tab)) SetTab(tab);
        if (_opts.Site is string site) Select(Selection.Site(site));
        // News from a fast-forward is in the Ledger; don't greet the player with a stale toast.
        _toast.Visible = false;
    }

    // ---------- Layout ----------

    private void BuildLayout()
    {
        var bg = new Panel();
        AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer();
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var edge in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{edge}", 12);
        var app = new HBoxContainer();
        app.AddThemeConstantOverride("separation", 12);
        margin.AddChild(app);

        var stage = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        stage.AddThemeConstantOverride("separation", 10);
        app.AddChild(stage);
        stage.AddChild(BuildHeader());
        _stats = new HBoxContainer();
        _stats.AddThemeConstantOverride("separation", 6);
        stage.AddChild(_stats);
        stage.AddChild(BuildMap());

        var aside = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
        aside.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line, 14));
        app.AddChild(aside);
        var side = new VBoxContainer();
        side.AddThemeConstantOverride("separation", 0);
        aside.AddChild(side);
        var tabsBg = new PanelContainer();
        var tabsBox = Ui.Flat(Ui.Panel2, 6, 0);
        tabsBox.CornerRadiusTopLeft = tabsBox.CornerRadiusTopRight = 14;
        tabsBox.BorderColor = Ui.Line;
        tabsBox.BorderWidthBottom = 1;
        tabsBg.AddThemeStyleboxOverride("panel", tabsBox);
        var tabs = new HBoxContainer();
        tabs.AddThemeConstantOverride("separation", 0);
        tabsBg.AddChild(tabs);
        side.AddChild(tabsBg);
        foreach (var t in Enum.GetValues<Tab>())
        {
            var b = new Button { Text = t.ToString(), ThemeTypeVariation = "Tab", ToggleMode = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.Pressed += () => SetTab(t);
            tabs.AddChild(b);
            _tabButtons[t] = b;
            if (t == Tab.Council)
            {
                _motionDot = new ColorRect { Color = Ui.Accent, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                b.AddChild(_motionDot);
                _motionDot.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
                _motionDot.OffsetLeft = -9;
                _motionDot.OffsetRight = -3;
                _motionDot.OffsetTop = 9;
                _motionDot.OffsetBottom = 15;
            }

            var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, Visible = false };
            var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            foreach (var s in new[] { "left", "right" }) pad.AddThemeConstantOverride($"margin_{s}", 14);
            pad.AddThemeConstantOverride("margin_top", 10);
            pad.AddThemeConstantOverride("margin_bottom", 14);
            var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            box.AddThemeConstantOverride("separation", 8);
            pad.AddChild(box);
            scroll.AddChild(pad);
            side.AddChild(scroll);
            _scrolls[t] = scroll;
            _panes[t] = new Pane(box);
        }
        BuildLedgerPane();
    }

    private Control BuildHeader()
    {
        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 14);
        var title = new HBoxContainer();
        title.AddThemeConstantOverride("separation", 6);
        _townName = Ui.Label("Briarford", Ui.Ink, Ui.Display, 24);
        title.AddChild(_townName);
        var sub = Ui.Label("Crown & Compass", Ui.Ink2, Ui.Medium, 12);
        sub.SizeFlagsVertical = SizeFlags.ShrinkEnd;
        title.AddChild(sub);
        bar.AddChild(title);
        _clock = Ui.Label("Day 1 · 00:00", Ui.Ink, Ui.Display, 16);
        _clock.CustomMinimumSize = new Vector2(130, 0);
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
            var b = new Button { Text = labels[i], ThemeTypeVariation = "Speed", ToggleMode = true, TooltipText = tips[i], CustomMinimumSize = new Vector2(40, 0) };
            b.Pressed += () => SetSpeed(k);
            sh.AddChild(b);
            _speedButtons[i] = b;
        }
        bar.AddChild(speeds);
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        _seedLabel = Ui.Label("seed 1", Ui.Ink2, null, 12);
        _seedLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        bar.AddChild(_seedLabel);
        var newTown = new Button { Text = "New town", TooltipText = "Start a fresh town with a random seed" };
        newTown.Pressed += () => NewTown((int)(GD.Randi() % 9999) + 1);
        bar.AddChild(newTown);
        return bar;
    }

    private Control BuildMap()
    {
        var frame = new PanelContainer { SizeFlagsVertical = SizeFlags.ExpandFill, ClipChildren = ClipChildrenMode.AndDraw };
        frame.AddThemeStyleboxOverride("panel", Ui.Box(new Color("3c5c34"), Ui.Line, 14, 0));
        _map = new MapView { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _map.Picked += sel => Select(sel);
        frame.AddChild(_map);

        // Overlays share the map's rectangle.
        var over = new Control { MouseFilter = MouseFilterEnum.Ignore };
        frame.AddChild(over);
        _pausedPill = Ui.Label("PAUSED", new Color("f6eedb"), Ui.Bold, 12);
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        pill.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0.08f, 0.06f, 0.04f, 0.7f), 12, 4, 99));
        pill.AddChild(_pausedPill);
        over.AddChild(pill);
        pill.SetAnchorsPreset(LayoutPreset.CenterTop);
        pill.Position = new Vector2(-34, 12);
        pill.GrowHorizontal = GrowDirection.Both;

        var help = Ui.Label("Scroll to zoom · drag to pan · double right-click to recenter", new Color("f6eedb", 0.55f), Ui.Medium, 11);
        over.AddChild(help);
        help.SetAnchorsPreset(LayoutPreset.TopRight);
        help.Position = new Vector2(-370, 12);

        _toast = new PanelContainer { Visible = false };
        _toast.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0.11f, 0.086f, 0.055f, 0.9f), 14, 10, 10));
        _toastText = Ui.Rich("");
        _toastText.AddThemeColorOverride("default_color", new Color("f6eedb"));
        _toast.AddChild(_toastText);
        over.AddChild(_toast);
        _toast.SetAnchorsPreset(LayoutPreset.BottomWide);
        _toast.OffsetLeft = 12;
        _toast.OffsetRight = -12;
        _toast.OffsetBottom = -12;
        _toast.GrowVertical = GrowDirection.Begin;
        _toast.GuiInput += e =>
        {
            if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) _toast.Visible = false;
        };

        _endOverlay = new ColorRect { Color = new Color(0.08f, 0.055f, 0.03f, 0.55f), Visible = false };
        over.AddChild(_endOverlay);
        _endOverlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        over.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        return frame;
    }

    private void BuildLedgerPane()
    {
        var box = _panes[Tab.Ledger].Box;
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 6);
        foreach (var (label, imp) in new[] { ("All", Importance.Routine), ("Notable", Importance.Notable), ("Major", Importance.Major) })
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonPressed = imp == _ledgerFilter };
            b.AddThemeFontSizeOverride("font_size", 12);
            b.Pressed += () =>
            {
                _ledgerFilter = imp;
                foreach (var c in filters.GetChildren().OfType<Button>()) c.ButtonPressed = c == b;
                _ledgerShown = -1;
                _dirty = true;
            };
            filters.AddChild(b);
        }
        box.AddChild(filters);
        _ledgerText = Ui.Rich("");
        box.AddChild(_ledgerText);
    }

    // ---------- Game ----------

    private void NewTown(int seed, bool autoplay = false)
    {
        _seed = seed;
        // With --autoplay the stand-in mayor keeps playing, for demos and screenshots.
        _sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = autoplay, AutoCouncil = autoplay, AutoDefense = autoplay });
        _map.Bind(_sim);
        _sim.Town.Ledger.OnWrite(OnLedger);
        _townName.Text = _sim.Town.Name;
        _seedLabel.Text = $"seed {seed}";
        _endShown = false;
        _endOverlay.Visible = false;
        _toast.Visible = false;
        _openChronicles.Clear();
        _ledgerShown = -1;
        foreach (var p in _panes.Values) p.Invalidate();
        _dirty = true;
        SetTab(Tab.Ledger);
    }

    private void OnLedger(LedgerEntry e)
    {
        if (e.Importance == Importance.Major || (e.Category == LedgerCategory.Council && e.Text.Contains(" moves that "))) Toast(e);
        // Real time with pause: the war's turning points stop the clock so the player can react.
        if (e.Category == LedgerCategory.War && e.Importance == Importance.Major && _speed > 0) SetSpeed(0);
        if (_sim.Town.Outcome != null && !_endShown) CallDeferred(MethodName.ShowEnd);
    }

    private void Toast(LedgerEntry e)
    {
        _toastText.Text = $"{Ui.C(Ui.Gold, $"[b]{e.Category.ToString().ToUpperInvariant()}[/b]")}  {LinkNames(e)}";
        _toast.Visible = true;
        _toastLeft = 6;
    }

    private void SetSpeed(int i)
    {
        if (i > 0) _lastSpeed = i;
        _speed = i;
        for (int k = 0; k < _speedButtons.Length; k++) _speedButtons[k].ButtonPressed = k == i;
        _pausedPill.GetParent<Control>().Visible = i == 0;
    }

    private void SetTab(Tab t)
    {
        _tab = t;
        foreach (var (k, b) in _tabButtons) b.ButtonPressed = k == t;
        foreach (var (k, s) in _scrolls) s.Visible = k == t;
        _dirty = true;
    }

    private void Select(Selection? sel)
    {
        _map.Selection = sel;
        if (sel != null) SetTab(Tab.Inspect);
        _dirty = true;
    }

    private void OnLink(string meta)
    {
        var parts = meta.Split(':', 2);
        switch (parts[0])
        {
            case "agent":
                Select(Selection.Agent(int.Parse(parts[1])));
                break;
            case "site":
                Select(Selection.Site(parts[1]));
                break;
            case "building":
                Select(Selection.Building(int.Parse(parts[1])));
                break;
            case "chron":
                int id = int.Parse(parts[1]);
                if (!_openChronicles.Add(id)) _openChronicles.Remove(id);
                _dirty = true;
                break;
        }
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } k) return;
        if (k.Keycode == Key.Space) SetSpeed(_speed == 0 ? _lastSpeed : 0);
        else if (k.Keycode is >= Key.Key1 and <= Key.Key4) SetSpeed((int)(k.Keycode - Key.Key0));
        else if (k.Keycode == Key.Home) _map.Recenter();
        else return;
        GetViewport().SetInputAsHandled();
    }

    public override void _Process(double delta)
    {
        double dt = Math.Min(0.1, delta);
        _acc += dt * Speeds[_speed];
        int steps = 0;
        while (_acc >= 1 && steps < 8)
        {
            _sim.Step();
            _acc -= 1;
            steps++;
            _dirty = true;
        }
        if (steps == 8) _acc = 0;
        _map.HourFraction = (float)_acc;

        if (_toast.Visible && (_toastLeft -= delta) <= 0) _toast.Visible = false;
        if (_dirty)
        {
            RenderHeader();
            switch (_tab)
            {
                case Tab.Ledger: RenderLedger(); break;
                case Tab.War: RenderWar(); break;
                case Tab.Council: RenderCouncil(); break;
                case Tab.Bounties: RenderBounties(); break;
                case Tab.Chronicles: RenderChronicles(); break;
                case Tab.Inspect: RenderInspect(); break;
            }
            _dirty = false;
        }
        _opts.Capture(this, ref _frames);
    }

    // ---------- Header ----------

    private static readonly Dictionary<ThreatStage, (string Label, int Level, string Text)> Stages = new()
    {
        [ThreatStage.Quiet] = ("Quiet", 0, "The hills are quiet. For now, the only danger is in the dungeons."),
        [ThreatStage.Omens] = ("Omens", 1, "Something is stirring in the hills. The signs are in the Ledger."),
        [ThreatStage.Probing] = ("Scouts", 1, "Orc scouts are watching the town, and a war camp has been raised. Burning it slows the warband."),
        [ThreatStage.Raids] = ("Raids", 2, "The warband raids the outlying farms. Every raid it gets away with, it grows bolder."),
        [ThreatStage.Marching] = ("Siege", 2, "The warband is marching on the town. Man the walls."),
        [ThreatStage.Defeated] = ("Victory", 0, "The warlord is dead and the warband broken. The Last Stand is won."),
        [ThreatStage.Victorious] = ("Fallen", 2, "The town has fallen to the warband."),
    };

    private readonly List<(Label K, Label V)> _statCards = [];

    private void RenderHeader()
    {
        var t = _sim.Town;
        var s = _sim.Snapshot();
        _clock.Text = $"Day {t.Day} · {t.Hour:00}:00";
        _motionDot.Visible = Council.PendingMotion(t) != null;
        var stage = Stages[t.Enemy.Stage];
        _threat.Text = (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at ? $"Siege in {Math.Ceiling((at - t.Tick) / 24.0)}d" : stage.Label).ToUpperInvariant();
        var pillColor = stage.Level == 2 ? Ui.Bad : stage.Level == 1 ? Ui.Gold : Ui.Line;
        _threatPill.AddThemeStyleboxOverride("panel", Ui.Box(stage.Level == 2 ? Ui.Bad : Ui.Panel, pillColor, 99, 1, 10, 3));
        _threat.AddThemeColorOverride("font_color", stage.Level == 2 ? Colors.White : stage.Level == 1 ? Ui.Gold : Ui.Ink);

        (string, string)[] stats =
        [
            ("Population", s.Population.ToString()),
            ("Heroes", t.Heroes.Count.ToString()),
            ("Treasury", $"{Math.Floor(t.Treasury)}g"),
            ("Bread", $"{s.FoodPrice:0.00}g"),
            ("Mandate", $"{Math.Round(t.Mandate)}"),
            ("Homeless", s.Homeless.ToString()),
        ];
        if (_statCards.Count == 0)
            foreach (var _ in stats)
            {
                var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                card.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel, Ui.Line, 10, 1, 10, 6));
                var v = new VBoxContainer();
                v.AddThemeConstantOverride("separation", 0);
                var k = Ui.Label("", Ui.Ink2, Ui.Medium, 11);
                var val = Ui.Label("", Ui.Ink, Ui.Bold, 17);
                v.AddChild(k);
                v.AddChild(val);
                card.AddChild(v);
                _stats.AddChild(card);
                _statCards.Add((k, val));
            }
        for (int i = 0; i < stats.Length; i++)
        {
            _statCards[i].K.Text = stats[i].Item1.ToUpperInvariant();
            _statCards[i].V.Text = stats[i].Item2;
        }
    }

    // ---------- Ledger ----------

    private void RenderLedger()
    {
        var entries = _sim.Town.Ledger.Entries;
        if (entries.Count == _ledgerShown) return;
        _ledgerShown = entries.Count;
        var sb = new System.Text.StringBuilder();
        int n = 0;
        for (int i = entries.Count - 1; i >= 0 && n < LedgerLimit; i--)
        {
            var e = entries[i];
            if (e.Importance < _ledgerFilter) continue;
            n++;
            int day = e.Tick / SimTime.HoursPerDay + 1;
            var catColor = e.Category switch
            {
                LedgerCategory.Quest => Ui.Gold,
                LedgerCategory.Brawl or LedgerCategory.Hardship or LedgerCategory.War => Ui.Bad,
                LedgerCategory.Romance => new Color("e07aa0"),
                LedgerCategory.Construction or LedgerCategory.Property => Ui.Good,
                _ => Ui.Ink2,
            };
            sb.Append(Ui.C(Ui.Ink2, $"[font_size=11]Day {day} · {e.Tick % 24:00}:00[/font_size]"))
                .Append("  ")
                .Append(Ui.C(catColor, $"[font_size=10][b]{e.Category.ToString().ToUpperInvariant()}[/b][/font_size]"))
                .Append('\n');
            string text = LinkNames(e);
            sb.Append(e.Importance == Importance.Major ? $"{Ui.C(Ui.Gold, "◆")} [b]{text}[/b]" : text).Append("\n[font_size=6] [/font_size]\n");
        }
        _ledgerText.Text = sb.ToString();
    }

    /// <summary>Turn the names of agents an entry mentions into links that open the inspector.</summary>
    private string LinkNames(LedgerEntry e)
    {
        string text = Ui.Esc(e.Text);
        foreach (int id in e.Agents)
        {
            if (id < 1 || id > _sim.Town.Agents.Count) continue;
            var a = _sim.Town.Agent(id);
            foreach (var n in new[] { a.Name, a.GivenName })
            {
                int i = text.IndexOf(n, StringComparison.Ordinal);
                if (i < 0) continue;
                text = $"{text[..i]}{AgentLink(a, n)}{text[(i + n.Length)..]}";
                break;
            }
        }
        return text;
    }

    private static string AgentLink(Agent a, string? label = null) => $"[url=agent:{a.Id}]{Ui.C(Ui.Accent, Ui.Esc(label ?? a.Name))}[/url]";

    private static string SiteLink(SiteDef s) => $"[url=site:{s.Id}]{Ui.C(Ui.Accent, Ui.Esc(Ui.Cap(s.Name)))}[/url]";

    private void ShowEnd()
    {
        var t = _sim.Town;
        _endShown = true;
        SetSpeed(0);
        bool won = t.Outcome == Outcome.Victory;
        foreach (var c in _endOverlay.GetChildren()) c.QueueFree();
        var center = new CenterContainer();
        _endOverlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(420, 0) };
        var box = Ui.Box(Ui.Panel, won ? Ui.Gold : Ui.Bad, 14, 0, 22, 20);
        box.BorderWidthTop = 4;
        card.AddThemeStyleboxOverride("panel", box);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        var h = Ui.Label(won ? "The Last Stand is won" : $"{t.Name} has fallen", Ui.Ink, Ui.Display, 22);
        h.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(h);
        string leader = t.Content.Enemy.Leader;
        var body = Ui.Rich("[center]" + (won
            ? $"{Ui.Esc(leader)} lies dead before your walls on day {t.Day}. The Marchlands will sing of {Ui.Esc(t.Name)}. The full account is in the Chronicles."
            : $"{Ui.Esc(leader)}'s banner flies over the town hall on day {t.Day}. The full account is in the Chronicles.") + "[/center]");
        v.AddChild(body);
        var actions = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        actions.AddThemeConstantOverride("separation", 8);
        var again = new Button { Text = "New town", ThemeTypeVariation = "Post" };
        again.Pressed += () => NewTown((int)(GD.Randi() % 9999) + 1);
        var stay = new Button { Text = "Keep watching" };
        stay.Pressed += () => _endOverlay.Visible = false;
        actions.AddChild(again);
        actions.AddChild(stay);
        v.AddChild(actions);
        card.AddChild(v);
        center.AddChild(card);
        _endOverlay.Visible = true;
    }
}
