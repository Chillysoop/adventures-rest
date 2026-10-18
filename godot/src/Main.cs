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
    /// <summary>In-game hours per real second for each speed button; index 0 is paused. At normal speed a day and
    /// night last a minute (1×, 4×, 12× and 36× of that).</summary>
    private static readonly double[] Speeds = [0, 0.4, 1.6, 4.8, 14.4];
    private const int LedgerLimit = 250;

    private enum Tab
    {
        City,
        Ledger,
        War,
        Council,
        Bounties,
        Companies,
        Armory,
        Diplomacy,
        Institutions,
        Faith,
        Campaign,
        Chronicles,
        Mayor,
        Inspect,
    }

    private Simulation _sim = null!;
    private int _seed = 1;
    private MapView _map = null!;
    private int _speed = 1;
    private int _warnedMotion = -1;
    private int _lastSpeed = 1;
    private double _acc;
    private bool _dirty = true;
    private Importance _ledgerFilter = Importance.Notable;
    private bool _endShown;
    private readonly HashSet<int> _openChronicles = [];

    private Label _tierLabel = null!;
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
        BuildMenu();
        BuildTitle();
        BuildHistory();
        BuildGuide();
        BuildStory();
        BuildFind();
        _map.TreeScale = _opts.Trees;
        Map.Fog.RevealAll = _opts.Reveal;
        // Started plainly, the game opens on the main menu over a town that has been growing a while;
        // arguments set up a moment instead.
        bool plain = OS.GetCmdlineUserArgs().Length == 0;
        bool loaded = _opts.Load is string file && LoadFile(file, "the save");
        if (!loaded && plain) Showcase();
        else if (!loaded) NewTown(_opts.Seed ?? 1, _opts.Autoplay);
        if (_opts.Beautify is int keep && keep < _opts.Day)
        {
            _sim.RunDays(keep);
            Client.Showcase.Reserve(_sim);
            _sim.RunDays(_opts.Day - keep);
            Client.Showcase.Dress(_sim);
        }
        else if (_opts.Day > 0) _sim.RunDays(_opts.Day);
        if (_opts.Hour > 0) for (int i = 0; i < _opts.Hour; i++) _sim.Step();
        _opts.ApplyScript(_sim);
        // Stories the script's hours brought are the stand-in mayor's, not cards to greet the shot with.
        if (_opts.Autoplay) _storySeen = _sim.Town.Stories.All.Count;
        SetSpeed(_opts.Speed ?? 1);
        if (_opts.Tab is string t && Enum.TryParse<Tab>(t, true, out var tab)) SetTab(tab);
        if (_opts.Site is string site) Select(Selection.Site(site));
        if (_opts.Wall is int wall) Select(Selection.Wall(wall));
        if (_opts.Cam is float cam)
            _map.LookAt(_opts.Look ?? (_opts.Site is string at && _map.Geo.Pins.TryGetValue(at, out var pin) ? pin : Map.Geo.Center), cam);
        // --at aims the camera at the town's first finished building of a kind.
        if (_opts.At is string atKind && _sim.Town.Buildings.Where(b => b.Def.Id == atKind && b.Complete).OrderBy(b => b.Id).FirstOrDefault() is Building at0)
            _map.LookAt(Map.Geo.LotPos(_sim.Town.Lot(at0.LotId)), _opts.Cam ?? 22);
        if (_opts.StrategicView) ToggleStrategic();
        foreach (var (kind, at, label) in _opts.Markers) _map.Markers.Add(new Marker(kind, at, _sim.Town.Day, label));
        if (_opts.OpenMarker is int open && open < _map.Markers.Count) CallDeferred(MethodName.OpenMarkerAt, open);
        if (_opts.Plan is string plan) OpenPlan(plan, _opts.Tool, _opts.Hover);
        if (_opts.DecorTool is string piece) OpenDecor(piece, _opts.DecorAt);
        if (_opts.Yaw != 0) _map.Rotate(_opts.Yaw);
        // --hero picks the town's most storied hero in town, for a look at their sheet.
        if (_opts.Hero && _sim.Town.Heroes.Where(a => !a.IsAway).OrderByDescending(a => a.Hero!.Epithet != "").ThenByDescending(a => a.Hero!.Items.Count).ThenByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault() is Agent hero)
            Select(Selection.Agent(hero.Id));
        if (_opts.House && _sim.Town.Buildings.Where(b => b.Complete && b.Def.Housing > 0).OrderByDescending(b => Decor.Comfort(_sim.Town, b)).ThenBy(b => b.Id).FirstOrDefault() is Building house)
        {
            Select(Selection.Building(house.Id));
            var lot = _sim.Town.Lot(house.LotId);
            _map.LookAt(new Vector2(Map.Geo.Margin + (float)lot.X, Map.Geo.Margin + (float)lot.Y), 7);
        }
        // News from a fast-forward is in the Ledger; don't greet the player with a stale feed.
        ClearFeed();
        if (_opts.Tutorial is string step) StartTutorial(step == "start" ? null : step);
        if (_opts.Background is string bg) _sim.ChooseBackground(bg);
        if (_opts.MayorLevel is int lv)
        {
            _sim.Town.Stories.Mayor.Level = lv;
            _sim.Town.Stories.Mayor.Xp = Math.Max(_sim.Town.Stories.Mayor.Xp, _sim.Town.Content.Stories.Levels[lv - 1] + 60);
        }
        foreach (var adv in _opts.Advances) _sim.AdvanceMayor(adv);
        if (_opts.Inspiration is int insp) _sim.Town.Stories.Mayor.Inspiration = insp;
        if (_opts.Story is string story && OpenScripted(story) is { } opened)
        {
            OpenStory(opened.Id);
            if (_opts.StoryPick is int pick) Choose(opened.Id, pick);
        }
        if (_opts.Find && Explore.Waiting(_sim.Town).FirstOrDefault() is { } waiting)
        {
            _findSeen[waiting.Id] = waiting.Log.Count;
            if (_opts.FindPick is string act) OrderFind(waiting.Id, act);
            else OpenFind(waiting.Id);
        }
        if (_opts.Menu) OpenMenu();
        if (plain || _opts.Title || _opts.Smoke) ShowTitle();
        if (_opts.History is string power) OpenHistory(power == "" ? null : power);
    }

    /// <summary>A story for a scripted shot; a sending comes from the first party out on a bounty that it fits.</summary>
    private Story? OpenScripted(string id)
    {
        var t = _sim.Town;
        if (t.Content.Stories.Story(id) is not { } d || !Sendings.Is(d)) return Stories.Open(t, id);
        // Wait (up to a few days) for a party on the road far enough from its site.
        for (int hour = 0; hour < 24 * 6; hour++)
        {
            foreach (var p in t.Parties.Where(p => !p.Returned && p.BountyId != null).OrderBy(p => p.Id))
            {
                p.Asked = 0;
                if (Sendings.Open(t, p, id) is { } s) return s;
            }
            _sim.Step();
        }
        return null;
    }

    /// <summary>The town behind the main menu: a random one, a month into the stand-in mayor's care, in the morning light.</summary>
    private void Showcase()
    {
        NewTown((int)(GD.Randi() % 9999) + 1, autoplay: true);
        _sim.RunDays(30);
        for (int i = 0; i < 10; i++) _sim.Step();
        ClearFeed();
    }

    private void OpenMarkerAt(int i) => OpenMarker(_map.Markers[i]);

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
                ShowPaper(false);
                _ledgerShown = -1;
                _dirty = true;
            };
            filters.AddChild(b);
        }
        var paper = new Button { Text = "Broadsheet", ToggleMode = true, TooltipText = "The town's weekly paper, and the front page you can pay for" };
        paper.AddThemeFontSizeOverride("font_size", 12);
        paper.Pressed += () =>
        {
            foreach (var c in filters.GetChildren().OfType<Button>()) c.ButtonPressed = c == paper;
            ShowPaper(true);
            _dirty = true;
        };
        filters.AddChild(paper);
        box.AddChild(filters);
        _ledgerText = Ui.Rich("");
        box.AddChild(_ledgerText);
        BuildPaper(box);
    }

    // ---------- Game ----------

    private void NewTown(int seed, bool autoplay = false)
    {
        RememberLegends();
        // With --autoplay the stand-in mayor keeps playing, for demos and screenshots; only a player's towns meet old legends.
        var legends = autoplay ? [] : ProfileLegends();
        Adopt(new Simulation(new SimulationOptions { Seed = seed, AutoBounties = autoplay, AutoArmory = autoplay && !_opts.KeepArmory, AutoIntel = autoplay && !_opts.KeepReports, AutoCouncil = autoplay, AutoDefense = autoplay, AutoPlanning = autoplay, AutoStories = autoplay, AutoExplore = autoplay && !_opts.Find, Legends = legends }));
    }

    /// <summary>Show a town: a new one, or one loaded from a save.</summary>
    private void Adopt(Simulation sim)
    {
        int seed = sim.Options.Seed;
        _seed = seed;
        _sim = sim;
        _lastAutosaveDay = sim.Town.Day;
        _warnedMotion = -1;
        _map.Planner.Pick(null);
        _map.Decorator.Pick(null);
        _map.Bind(_sim);
        ShowBuildGroup(_buildGroup);
        _sim.Town.Ledger.OnWrite(OnLedger);
        _townName.Text = _sim.Town.Name;
        _seedLabel.Text = $"seed {seed}";
        _endShown = sim.Town.Outcome != null;
        _endOverlay.Visible = false;
        ClearFeed();
        _openChronicles.Clear();
        _ledgerShown = -1;
        _step = null;
        CloseStory();
        _storySeen = sim.Town.Stories.All.Count;
        CloseFind();
        _findSeen.Clear();
        _findShownBeat.Clear();
        foreach (var p in _panes.Values) p.Invalidate();
        _dirty = true;
        CloseWindow();
        Select(null);
    }

    private void OnLedger(LedgerEntry e)
    {
        if (Newsworthy(e)) Feed(e);
        // Real time with pause: the war's turning points stop the clock so the player can react.
        if (e.Category == LedgerCategory.War && e.Importance == Importance.Major && _speed > 0 && !_opts.NoPause) SetSpeed(0);
        if (_sim.Town.Outcome != null && !_endShown) CallDeferred(MethodName.ShowEnd);
    }

    /// <summary>
    /// Real time with pause: a council motion the player has not answered stops the clock once, on the hour its days run
    /// out, and opens the Council, so it is never settled for them in a blink at top speed. Unpausing lets the vote stand.
    /// </summary>
    private bool MotionLapses()
    {
        if (_opts.NoPause || _opts.Smoke || _opts.Bench || _opts.Screenshot != null || _sim.Options.AutoCouncil) return false;
        if (Council.Lapsing(_sim.Town) is not { } m || m.Id == _warnedMotion) return false;
        _warnedMotion = m.Id;
        SetSpeed(0);
        _acc = 0;
        if (!(_window.Visible && _windowTab == Tab.Council)) OpenWindow(Tab.Council);
        return true;
    }

    private void SetSpeed(int i)
    {
        if (i > 0) _lastSpeed = i;
        _speed = i;
        for (int k = 0; k < _speedButtons.Length; k++) _speedButtons[k].ButtonPressed = k == i;
        _pausedPill.GetParent<Control>().Visible = i == 0;
    }

    /// <summary>The player changed the speed (a key or a button), which the tutorial watches for.</summary>
    private void PlayerSpeed(int i)
    {
        SetSpeed(i);
        GuideEvent(i == 0 ? "pause" : "speed");
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
        if (sel != null) GuideEvent($"select:{sel.Kind}");
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
            case "mayor":
                OpenWindow(Tab.Mayor);
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
        if (_find.Visible && k.Keycode == Key.Escape)
        {
            CloseFind();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_story.Visible && k.Keycode == Key.Escape)
        {
            // A sending can't be waved away: the party is waiting on the stone.
            if (_storyId is not int sid || Stories.Find(_sim.Town, sid) is not { } open || !Sendings.Is(_sim.Town, open) || open.Done || open.WaitTick > _sim.Town.Tick) CloseStory();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_history.Visible)
        {
            if (k.Keycode == Key.Escape) CloseHistory();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_menu.Visible)
        {
            if (k.Keycode == Key.Escape) CloseMenu();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_title.Visible)
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (MarkerKey(k.Keycode))
        {
            GetViewport().SetInputAsHandled();
            return;
        }
        if (k.Keycode == Key.Space) PlayerSpeed(_speed == 0 ? _lastSpeed : 0);
        else if (k.Keycode is >= Key.Key1 and <= Key.Key4) PlayerSpeed((int)(k.Keycode - Key.Key0));
        else if (k.Keycode == Key.Home) _map.Recenter();
        else if (k.Keycode == Key.B) ToggleBuildBar();
        else if (k.Keycode == Key.G) ToggleFlag();
        else if (k.Keycode == Key.Escape && _map.Flagging) ToggleFlag();
        else if (k.Keycode == Key.Escape && _map.Planner.Active) _map.Planner.Pick(null);
        else if (k.Keycode is Key.Bracketleft or Key.Bracketright && _map.Planner.Tool?.Zone != null) _map.Planner.Resize(k.Keycode == Key.Bracketright ? 1.15f : 1 / 1.15f);
        else if (k.Keycode == Key.Escape && _map.Decorator.Active) _map.Decorator.Pick(null);
        else if (k.Keycode == Key.R && _map.Decorator.Active) _map.Decorator.Turn(k.ShiftPressed ? -1 : 1);
        else if (k.Keycode == Key.M) ToggleStrategic();
        else if (k.Keycode == Key.C) SetTab(Tab.Mayor);
        else if (k.Keycode == Key.Escape && _inspector.Visible) Select(null);
        else if (k.Keycode == Key.Escape && _window.Visible) CloseWindow();
        else if (k.Keycode is Key.Q or Key.E)
        {
            _map.Rotate(k.Keycode == Key.Q ? -1 : 1);
            GuideEvent("rotate");
        }
        else if (k.Keycode == Key.Escape) OpenMenu();
        else if (k.Keycode == Key.F5) Save("quick");
        else if (k.Keycode == Key.F9) Load("quick");
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
            if (MotionLapses()) break;
            _sim.Step();
            _acc -= 1;
            steps++;
            _dirty = true;
        }
        if (steps == 8) _acc = 0;
        if (steps > 0 && _opts.Screenshot == null && !_opts.Bench && !_opts.Smoke) Autosave();
        _map.HourFraction = (float)_acc;
        // Townsfolk walk at the pace of the speed button, not of the hours: a slower day doesn't slow their stride.
        _map.TimeScale = (float)(Speeds[_speed] / Speeds[1]);

        AgeFeed(delta);
        SyncAlerts(delta);
        ScriptedAlert();
        SyncMarkers();
        SyncGuide();
        SyncStories(delta);
        SyncFinds();
        SyncChecks();
        if (_title.Visible) _map.Drift(1, (float)delta * 0.035f);
        if (_dirty)
        {
            RenderHeader();
            if (_window.Visible)
                switch (_windowTab)
                {
                    case Tab.City: RenderCity(); break;
                    case Tab.Ledger: RenderLedger(); break;
                    case Tab.War: RenderWar(); break;
                    case Tab.Council: RenderCouncil(); break;
                    case Tab.Bounties: RenderBounties(); break;
                    case Tab.Companies: RenderCompanies(); break;
                    case Tab.Armory: RenderArmory(); break;
                    case Tab.Faith: RenderFaith(); break;
                    case Tab.Campaign: RenderCampaign(); break;
                    case Tab.Chronicles: RenderChronicles(); break;
                    case Tab.Mayor: RenderMayor(); break;
                    case Tab.Diplomacy: RenderDiplomacy(); break;
                    case Tab.Institutions: RenderInstitutions(); break;
                }
            if (_inspector.Visible) RenderInspect();
            RenderBuild();
            _dirty = false;
        }
        if (_opts.Bench) Bench(delta);
        Smoke();
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
        var season = Seasons.Current(t);
        _clock.Text = $"Day {t.Day} · {season.Name} · {t.Hour:00}:00";
        _clock.TooltipText = $"{season.Name} of year {Seasons.Year(t)}, day {Seasons.DayOf(t)} of {Seasons.Rules(t).Days}. {Seasons.Next(t).Name} comes in {Ui.Days(Seasons.DaysLeft(t))}.\n{season.Blurb}";
        _tierLabel.Text = $"{City.Tier(t).Name} · about {City.Souls(t):N0} souls";
        _motionDot.Visible = Council.PendingMotion(t) != null;
        _standing = StandingAlerts();
        var stage = Stages[t.Enemy.Stage];
        _threat.Text = (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at ? $"Siege in {Math.Ceiling((at - t.Tick) / 24.0)}d" : stage.Label).ToUpperInvariant();
        var pillColor = stage.Level == 2 ? Ui.Bad : stage.Level == 1 ? Ui.Gold : Ui.Line;
        _threatPill.AddThemeStyleboxOverride("panel", Ui.Box(stage.Level == 2 ? Ui.Bad : Ui.Panel, pillColor, 99, 1, 10, 3));
        _threat.AddThemeColorOverride("font_color", stage.Level == 2 ? Colors.White : stage.Level == 1 ? Ui.Gold : Ui.Ink);

        (string, string, string, string)[] stats =
        [
            ("Households", "population", s.Population.ToString(), $"Households living in town, about {City.Souls(t):N0} souls"),
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
        if (_paperShown)
        {
            RenderPaper();
            return;
        }
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
        RememberLegends();
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
