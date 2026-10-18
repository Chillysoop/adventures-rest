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
        Faith,
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
        _map.TreeScale = _opts.Trees;
        NewTown(_opts.Seed ?? 1, _opts.Autoplay);
        if (_opts.Day > 0) _sim.RunDays(_opts.Day);
        if (_opts.Hour > 0) for (int i = 0; i < _opts.Hour; i++) _sim.Step();
        _opts.ApplyScript(_sim);
        SetSpeed(_opts.Speed ?? 1);
        if (_opts.Tab is string t && Enum.TryParse<Tab>(t, true, out var tab)) SetTab(tab);
        if (_opts.Site is string site) Select(Selection.Site(site));
        if (_opts.Cam is float cam)
            _map.LookAt(_opts.Look ?? (_opts.Site is string at && _map.Geo.Pins.TryGetValue(at, out var pin) ? pin : Map.Geo.Center), cam);
        if (_opts.StrategicView) ToggleStrategic();
        if (_opts.Plan is string plan) OpenPlan(plan, _opts.Tool, _opts.Hover);
        if (_opts.Yaw != 0) _map.Rotate(_opts.Yaw);
        // --hero picks the town's most storied hero in town, for a look at their sheet.
        if (_opts.Hero && _sim.Town.Heroes.Where(a => !a.IsAway).OrderByDescending(a => a.Hero!.Items.Count).ThenByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault() is Agent hero)
            Select(Selection.Agent(hero.Id));
        // News from a fast-forward is in the Ledger; don't greet the player with a stale feed.
        ClearFeed();
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
        _sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = autoplay, AutoCouncil = autoplay, AutoDefense = autoplay, AutoPlanning = autoplay });
        _map.Planner.Pick(null);
        _map.Bind(_sim);
        _sim.Town.Ledger.OnWrite(OnLedger);
        _townName.Text = _sim.Town.Name;
        _seedLabel.Text = $"seed {seed}";
        _endShown = false;
        _endOverlay.Visible = false;
        ClearFeed();
        _openChronicles.Clear();
        _ledgerShown = -1;
        foreach (var p in _panes.Values) p.Invalidate();
        _dirty = true;
        CloseWindow();
        Select(null);
    }

    private void OnLedger(LedgerEntry e)
    {
        if (Newsworthy(e)) Feed(e);
        AlertFor(e);
        // Real time with pause: the war's turning points stop the clock so the player can react.
        if (e.Category == LedgerCategory.War && e.Importance == Importance.Major && _speed > 0 && !_opts.NoPause) SetSpeed(0);
        if (_sim.Town.Outcome != null && !_endShown) CallDeferred(MethodName.ShowEnd);
    }

    private void SetSpeed(int i)
    {
        if (i > 0) _lastSpeed = i;
        _speed = i;
        for (int k = 0; k < _speedButtons.Length; k++) _speedButtons[k].ButtonPressed = k == i;
        _pausedPill.GetParent<Control>().Visible = i == 0;
    }

    /// <summary>Open a management window, or close it if it is the one already open (the dock's toggle).</summary>
    private void SetTab(Tab t)
    {
        if (t == Tab.Inspect) return;
        if (_window.Visible && _windowTab == t) CloseWindow();
        else OpenWindow(t);
    }

    private void Select(Selection? sel)
    {
        _map.Selection = sel;
        _inspector.Visible = sel != null;
        PlaceInspector();
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
        else if (k.Keycode == Key.B) ToggleBuildBar();
        else if (k.Keycode == Key.Escape && _map.Planner.Active) _map.Planner.Pick(null);
        else if (k.Keycode == Key.M) ToggleStrategic();
        else if (k.Keycode == Key.Escape && _inspector.Visible) Select(null);
        else if (k.Keycode == Key.Escape && _window.Visible) CloseWindow();
        else if (k.Keycode == Key.Q) _map.Rotate(-1);
        else if (k.Keycode == Key.E) _map.Rotate(1);
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
        _map.TimeScale = Speeds[_speed];

        AgeFeed(delta);
        SyncAlerts();
        ScriptedAlert();
        SyncMarkers();
        if (_dirty)
        {
            RenderHeader();
            if (_window.Visible)
                switch (_windowTab)
                {
                    case Tab.Ledger: RenderLedger(); break;
                    case Tab.War: RenderWar(); break;
                    case Tab.Council: RenderCouncil(); break;
                    case Tab.Bounties: RenderBounties(); break;
                    case Tab.Faith: RenderFaith(); break;
                    case Tab.Chronicles: RenderChronicles(); break;
                }
            if (_inspector.Visible) RenderInspect();
            RenderBuild();
            _dirty = false;
        }
        if (_opts.Bench) Bench(delta);
        if (_opts.Flyout) _map.Drift(1 + (float)delta * 0.16f, (float)delta * 0.05f);
        _opts.Capture(this, ref _frames);
    }

    private int _benchFrames;
    private double _benchTime, _benchProcess;
    private (int Draws, int Primitives, int Objects) _benchPeak;

    /// <summary>With --bench: after the map settles, average 120 frames of frame time and render counters, print them and quit.</summary>
    private void Bench(double delta)
    {
        if (++_benchFrames <= 60)
        {
            MapView.CpuMs = 0;
            return;
        }
        _benchTime += delta;
        _benchProcess += MapView.CpuMs / 1000;
        var st = _map.Stats();
        _benchPeak = (Math.Max(_benchPeak.Draws, st.Draws), Math.Max(_benchPeak.Primitives, st.Primitives), Math.Max(_benchPeak.Objects, st.Objects));
        MapView.CpuMs = 0;
        if (_benchFrames < 180) return;
        int n = _benchFrames - 60;
        GD.Print($"BENCH trees={_map.TreeInstances} dist={_map.Distance:F0} frame_ms={_benchTime / n * 1000:F2} map_cpu_ms={_benchProcess / n * 1000:F2} draws={_benchPeak.Draws} primitives={_benchPeak.Primitives} objects={_benchPeak.Objects}");
        GetTree().Quit();
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
        _standing = StandingAlerts();
        var stage = Stages[t.Enemy.Stage];
        _threat.Text = (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at ? $"Siege in {Math.Ceiling((at - t.Tick) / 24.0)}d" : stage.Label).ToUpperInvariant();
        var pillColor = stage.Level == 2 ? Ui.Bad : stage.Level == 1 ? Ui.Gold : Ui.Line;
        _threatPill.AddThemeStyleboxOverride("panel", Ui.Box(stage.Level == 2 ? Ui.Bad : Ui.Panel, pillColor, 99, 1, 10, 3));
        _threat.AddThemeColorOverride("font_color", stage.Level == 2 ? Colors.White : stage.Level == 1 ? Ui.Gold : Ui.Ink);

        (string, string, string, string)[] stats =
        [
            ("Population", "population", s.Population.ToString(), "People living in town"),
            ("Heroes", "heroes", t.Heroes.Count.ToString(), "Adventurers in town or on the road"),
            ("Treasury", "gold", $"{Math.Floor(t.Treasury)}", "Gold in the town's coffers"),
            ("Bread", "bread", $"{s.FoodPrice:0.00}", "The price of a loaf, in gold: it climbs when food runs short"),
            ("Mandate", "mandate", $"{Math.Round(t.Mandate)}", "The townsfolk's approval of you, out of 100"),
            ("Homeless", "homeless", s.Homeless.ToString(), "Townsfolk with no roof"),
        ];
        if (_statCards.Count == 0)
            foreach (var st in stats)
            {
                var card = new HBoxContainer { TooltipText = st.Item4, MouseFilter = MouseFilterEnum.Pass };
                card.AddThemeConstantOverride("separation", 6);
                card.AddChild(new TextureRect { Texture = Ui.Icon(st.Item2), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(24, 24), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
                var v = new VBoxContainer();
                v.AddThemeConstantOverride("separation", -3);
                var k = Ui.Label("", Ui.Ink2, Ui.Medium, 10);
                var val = Ui.Label("", Ui.Ink, Ui.Bold, 16);
                v.AddChild(k);
                v.AddChild(val);
                card.AddChild(v);
                _stats.AddChild(card);
                _statCards.Add((k, val));
            }
        for (int i = 0; i < stats.Length; i++)
        {
            _statCards[i].K.Text = stats[i].Item1.ToUpperInvariant();
            _statCards[i].V.Text = stats[i].Item3;
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
        _ledgerText.Text = Ui.Coined(sb.ToString());
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
        var h = Ui.Label(Saga.EndTitle(t), Ui.Ink, Ui.Display, 22);
        h.HorizontalAlignment = HorizontalAlignment.Center;
        v.AddChild(h);
        var body = Ui.Rich($"[center]{Ui.Esc(t.Saga.EndText)} The full account is in the Chronicles.[/center]");
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
