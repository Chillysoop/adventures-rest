using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// With --smoke: the merge gate's play-through of a build (<c>scripts/gate.ts</c>). From the main menu it
/// starts a new town, plays on at top speed, opens every window, the build bar, the campaign map and the
/// game menu, saves and loads, starts the tutorial and opens the History, a few frames apart so each one draws. It prints
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

    /// <summary>Run one stage of the play-through and say how many frames to wait before the next.</summary>
    private int SmokeStage(int stage)
    {
        switch (stage)
        {
            case 0:
                if (!_title.Visible) throw new InvalidOperationException("the main menu is not showing");
                return 10;
            case 1:
                // What the main menu's New game does, with the stand-in mayor playing so later systems come into play.
                StartTown(tutorial: false, _opts.Seed ?? 1, autoplay: true);
                _sim.RunDays(_opts.SmokeDays);
                SetSpeed(Speeds.Length - 1);
                return 90;
            case 2:
                foreach (var tab in Enum.GetValues<Tab>()) SetTab(tab);
                return 1;
            case 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 or 11:
                // One window open at a time, each given a few frames to draw.
                OpenWindow(Enum.GetValues<Tab>()[stage - 3]);
                return 5;
            case 12:
                CloseWindow();
                ToggleBuildBar();
                return 5;
            case 13:
                ToggleBuildBar();
                ToggleStrategic();
                return 10;
            case 14:
                ToggleStrategic();
                OpenMenu();
                return 5;
            case 15:
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
            case 16:
                StartTutorial();
                return 10;
            case 17:
                OpenHistory(null);
                return 10;
            case 18:
                CloseHistory();
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
