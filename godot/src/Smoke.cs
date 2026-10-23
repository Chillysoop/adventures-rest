using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// With --smoke: the merge gate's play-through of a build (<c>scripts/gate.ts</c>). From the main menu (and its choice of storytellers) it
/// rolls and rerolls a mayor, starts a new town, plays on at top speed, opens every window, the build bar, the campaign map, a wall and a guard flag and the
/// game menu, saves and loads, starts the tutorial, opens the History, the Settings and the glossary with a pinned keyword card, steps into a story event, answers a party's sending, opens the tourney card with a bet on it, opens the recap and a tavern tale, every faction's first-meeting picture, a few frames apart so each one draws. It prints
/// <c>SMOKE OK</c> and quits with 0, or <c>SMOKE FAIL</c> and 1; the gate also fails on any engine error.
/// </summary>
public partial class Main
{
    private int _smokeStage, _smokeWait;

    private void Smoke()
    {
        if (!_opts.Smoke || --_smokeWait > 0) return;
        try
        {
            _smokeWait = SmokeStage(_smokeStage++);
        }
        catch (Exception e)
        {
            GD.PrintErr($"SMOKE FAIL at stage {_smokeStage - 1}: {e}");
            GetTree().Quit(1);
            _opts.Smoke = false;
        }
    }

    /// <summary>Draw every page of a paged window once, then leave it on its first.</summary>
    private void SmokePages(Tab t)
    {
        if (t == Tab.Inspect) return;
        var pane = _panes[t];
        RenderWindow();
        var pages = pane.Pages;
        foreach (var page in pages)
        {
            pane.Selected = page;
            RenderWindow();
        }
        if (pages.Count > 0) pane.Selected = pages[0];
    }

    /// <summary>Run one stage of the play-through and say how many frames to wait before the next.</summary>
    private int SmokeStage(int stage)
    {
        // Every window gets its stage: the stages after the windows move along by one for each window past the first eleven.
        int windows = Enum.GetValues<Tab>().Length - 1, extra = windows - 11;
        if (stage >= 3 && stage < 3 + windows)
        {
            OpenWindow(Enum.GetValues<Tab>()[stage - 3]);
            SmokePages(Enum.GetValues<Tab>()[stage - 3]);
            return 5;
        }
        if (stage >= 3 + windows) stage -= extra;
        switch (stage)
        {
            case 0:
                if (!_title.Visible) throw new InvalidOperationException("the main menu is not showing");
                // New game asks for a storyteller first.
                PickStoryteller(false);
                if (!_tellers.Visible || _titleMenu.Visible) throw new InvalidOperationException("the storytellers are not showing");
                // Then where to found the town.
                PickSite("rising", _opts.Seed ?? 1);
                if (!_sites.Visible || _sites.GetChildCount() < 4) throw new InvalidOperationException("the founding sites are not showing");
                // Then the mayor's rolled sheet: a reroll and another background.
                PickMayor(Sim.Content.Default().Storytellers.Default);
                var rolled = _mayorRoll;
                RerollMayor();
                PickMayorBackground(Sim.Content.Default().Stories.Backgrounds[^1].Ref);
                if (!_mayorSetup.Visible || _tellers.Visible || _mayorRoll.Background == rolled.Background && _mayorRoll.Scores.SequenceEqual(rolled.Scores)) throw new InvalidOperationException("the mayor's sheet did not reroll");
                return 10;
            case 1:
                // What the main menu's New game does, with the stand-in mayor playing so later systems come into play.
                StartTown(tutorial: false, _opts.Seed ?? 1, autoplay: true, mayor: Stories.RollMayor(_sim.Town.Content, _opts.Seed ?? 1, _mayorRoll.Background));
                // A new town shows its charter goals (gone by the time the smoke run has played on).
                SyncGoals();
                if (Goals.Open(_sim.Town).Count == 0 || _goalsRows.GetChildCount() == 0) throw new InvalidOperationException("the charter goals card is empty in a new town");
                _sim.RunDays(_opts.SmokeDays);
                SetSpeed(Speeds.Length - 1);
                return 90;
            case 2:
                foreach (var tab in Enum.GetValues<Tab>()) SetTab(tab);
                // Each group on the rail opens its window on one of its own tabs.
                for (int g = 0; g < Groups.Length; g++)
                {
                    CloseWindow();
                    OpenGroup(g);
                    if (_windowTab is not Tab open || GroupOf(open) != g) throw new InvalidOperationException($"the {Groups[g].Name} group did not open");
                }
                return 1;
            case >= 3 and <= 13:
                // One window open at a time, each given a few frames to draw (Inspect, the last, opens by clicking).
                OpenWindow(Enum.GetValues<Tab>()[stage - 3]);
                return 5;
            case 14:
                CloseWindow();
                ToggleBuildBar();
                // The info lens: every view worked out once, the last left on to draw until the next stage.
                foreach (var m in CrownAndCompass.Client.Map.Lens.Modes)
                {
                    _map.Lens.Show(true, m);
                    if (_map.Lens.Summary == "") throw new InvalidOperationException($"the {m} lens says nothing");
                }
                return 5;
            case 15:
                ToggleBuildBar();
                ToggleLens();
                ToggleStrategic();
                return 10;
            case 16:
                ToggleStrategic();
                // The town guard: a stretch of wall in the inspector, and a flag planted by the map click.
                if (_map.TownView.Walk != null) ShowWall(1);
                ToggleFlag();
                OnFlagDropped(CrownAndCompass.Client.Map.Geo.Center + new Vector2(3, 2));
                // The quick orders over the corner map: man the walls, the Strike list, and a spot marked for a company.
                QuickDefend();
                ShowStrike(true);
                ToggleBrush(Wilds.Explore);
                OnWildsPainted(Wilds.Explore, [(2, -2), (3, -2)], false);
                ToggleBrush(Wilds.Patrol);
                OnWildsPainted(Wilds.Patrol, [(2, 1), (3, 1)], false);
                OnWildsPainted(Wilds.Patrol, [(3, 1)], true);
                ToggleBrush(Wilds.Patrol);
                return 5;
            case 17:
                ShowStrike(false);
                CloseWindow();
                OpenMenu();
                return 5;
            case 18:
            {
                // Save and load through the same calls the game menu uses, to a scratch file.
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"crown-smoke-{System.Environment.ProcessId}.save");
                int day = _sim.Town.Day, hour = _sim.Town.Hour;
                SaveGame.Capture(_sim, ClientData()).WriteFile(path);
                if (!LoadFile(path, "the smoke save")) throw new InvalidOperationException("the save did not load");
                System.IO.File.Delete(path);
                if (_sim.Town.Day != day || _sim.Town.Hour != hour) throw new InvalidOperationException($"loaded day {_sim.Town.Day} hour {_sim.Town.Hour}, saved day {day} hour {hour}");
                SetSpeed(Speeds.Length - 1);
                return 60;
            }
            case 19:
                StartTutorial();
                return 10;
            case 20:
                OpenHistory(null);
                // The Settings window over it, every clock-stop toggle drawn.
                // Every tab drawn, then the clock tab: a heading, every clock-stop toggle and a footnote.
                foreach (var (tab, _, _) in SettingsTabs) OpenSettings(tab);
                OpenSettings("clock");
                if (_settingsRows.GetChildCount() != ClockStops.All.Length + 2) throw new InvalidOperationException("the settings did not draw every toggle");
                return 10;
            case 21:
                CloseSettings();
                CloseHistory();
                // Each tray drawer, then the glossary with a keyword's card pinned over it.
                foreach (var d in Trays)
                {
                    OpenTray(d.Id);
                    RenderTray();
                }
                CloseTray();
                OpenGlossary("mandate");
                Pin("warband");
                if (_kwPinned.Count == 0) throw new InvalidOperationException("the keyword card did not pin");
                return 10;
            case 22:
            {
                CloseGlossary();
                UnpinAll();
                // A story event's card, as one pops up: open it and take the first option, which throws the die.
                var story = Stories.Open(_sim.Town, "shrine-omen") ?? throw new InvalidOperationException("no story would open");
                OpenStory(story.Id);
                Choose(story.Id, 0);
                return 70;
            }
            case 23:
                if (_beat == null || _beatResult is not { Visible: true }) throw new InvalidOperationException("the roll did not land");
                CloseStory();
                // A level up on the Mayor tab: one skill-level choice and one improvement.
                _sim.Town.Stories.Mayor.Level = Math.Max(_sim.Town.Stories.Mayor.Level, 4);
                SetTab(Tab.Mayor);
                return 10;
            case 24:
                for (int i = 0; i < 6 && Sheet.Pending(_sim.Town) > 0; i++)
                {
                    var (_, improvement) = Sheet.NextAdvance(_sim.Town);
                    string adv = improvement ? "feat:lucky" : _sim.Town.Content.Stories.Skills.Select(s => "skill:" + s.Id).First(a => Sheet.Refuse(_sim.Town, a) == null);
                    if (!_sim.AdvanceMayor(adv)) throw new InvalidOperationException($"the mayor could not take {adv}");
                    _dirty = true;
                }
                // The full character sheet behind its button, and a piece of regalia commissioned and worn.
                _mayorSheet = true;
                _sim.Town.Treasury = Math.Max(_sim.Town.Treasury, 5000);
                if (Regalia.Commissions(_sim.Town).FirstOrDefault(c => c.Refused == null) is { Item: var piece } && _sim.CommissionRegalia(piece.Id))
                    _sim.WearRegalia(_sim.Town.Armory.Stock.First(x => x.Item.Id == piece.Id).Id);
                return 10;
            case 25:
            {
                _mayorSheet = false;
                // A party's sending: the card with the party's and the hall's help on each roll, answered with the first choice.
                SetTab(Tab.Mayor);
                if (OpenScripted("bridge-toll") is not { } sending) return 1;
                OpenStory(sending.Id);
                Choose(sending.Id, 0);
                return 70;
            }
            case 26:
                if (_story.Visible && (_beat == null || _beatResult is not { Visible: true })) throw new InvalidOperationException("the sending's roll did not land");
                CloseStory();
                // The mayor out in the streets, then dead: the fallen card with the temple and the council's candidates.
                if (!_sim.SetMayorStance("town")) throw new InvalidOperationException("the mayor would not walk the streets");
                Mayoralty.Wound(_sim.Town, 9999, "a smoke test");
                if (!Mayoralty.Dead(_sim.Town)) throw new InvalidOperationException("the mayor did not die");
                _dirty = true;
                return 10;
            case 27:
                if (Mayoralty.Dead(_sim.Town) && !_sim.NameSuccessor(0)) throw new InvalidOperationException("no successor was named");
                // An explorers' report card, as one pops up, with the company's latest roll if it has one.
                if (_sim.Town.Finds.LastOrDefault() is { } find) OpenFind(find.Id, find.Beat);
                return 70;
            case 28:
                CloseFind();
                // "Last time in...", as a loaded game opens with it.
                OpenRecap(Math.Max(1, _sim.Town.Day - 15), "Welcome back");
                return 10;
            case 29:
                if (!_recap.Visible) throw new InvalidOperationException("the recap did not open");
                _recap.Visible = false;
                // A delve told at the tavern, skipped to its big moment so its roll and map draw.
                if (_sim.Town.Chronicles.LastOrDefault(c => c.Beats.Count > 0) is { } tale)
                {
                    OpenTale(tale.Id);
                    SkipTaleTo(null);
                }
                return 40;
            case 30:
                CloseTale();
                return 5;
            case 31:
            {
                // The tourney card: a grand tourney put on today, a stake on the first event, and its first heats ridden.
                // Staged the hour before the heats start, so they are ridden whatever hour the play-through got here at.
                for (int h = 0; h < 24 && _sim.Town.Hour != Tournaments.Rules(_sim.Town).StartHour - 1; h++) _sim.Step();
                if (!Tournaments.Stage(_sim.Town, 1)) throw new InvalidOperationException("the tourney would not stage");
                // A stake on the first event not yet under way (today's may have begun, by the hour), from a treasury that can spare it.
                _sim.Town.Treasury = Math.Max(_sim.Town.Treasury, 5000);
                int ev = _sim.Town.Tourney.Events.FindIndex(e => Tournaments.BetBlocked(_sim.Town, e) == null);
                if (ev < 0) throw new InvalidOperationException("no tourney event takes bets");
                var first = _sim.Town.Tourney.Events[ev];
                // On the favourite: a rider whose chance has fallen to nothing (hurt, gone) takes no stake.
                int pick = Tournaments.Chances(_sim.Town, first).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key;
                if (!_sim.PlaceBet(ev, pick, Tournaments.Rules(_sim.Town).Betting.Min)) throw new InvalidOperationException("the bet was refused");
                OpenTourney();
                // Until a heat is ridden: today's may be over by the hour, so tomorrow's event rides first.
                for (int h = 0; h < 36 && (h < 14 || _sim.Town.Tourney.Events.All(e => e.Heats.Count == 0)); h++) _sim.Step();
                _dirty = true;
                return 40;
            }
            case 32:
                // Staged late in the day, the first event's hours may have passed: any event's heats will do.
                if (!_tourney.Visible || _sim.Town.Tourney.Events.All(e => e.Heats.Count == 0)) throw new InvalidOperationException("the tourney's heats did not show");
                CloseTourney();
                return 5;
            case 33:
                // A first meeting's picture: every scene staged once, the last left up to draw.
                foreach (var d in _sim.Town.Content.Intros.Intros) OpenIntro(d, null);
                return 40;
            case 34:
                if (!_intro.Visible) throw new InvalidOperationException("the first-meeting card did not open");
                CloseIntro();
                return 5;
            default:
                var t = _sim.Town;
                GD.Print($"SMOKE OK seed={_sim.Options.Seed} day={t.Day} population={t.Population} buildings={t.Buildings.Count} outcome={t.Outcome?.ToString() ?? "none"}");
                GetTree().Quit(0);
                _opts.Smoke = false;
                return 0;
        }
    }
}
