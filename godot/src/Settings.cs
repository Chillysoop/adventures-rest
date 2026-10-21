using System.Text.Json.Nodes;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The Settings window, from the game menu and the main menu: which events stop the clock (real time with
/// pause), one toggle for each kind in <see cref="ClockStops.All"/>, kept in the profile's "settings" section
/// (user://profile.json, <see cref="ReadProfile"/>) so it holds for every town; the sim never reads it.
/// <see cref="StopClock"/> is the one call every pause site goes through.
/// </summary>
public partial class Main
{
    /// <summary>The player's toggles, by clock-stop id; a kind not here takes its default.</summary>
    private readonly Dictionary<string, bool> _clockStops = [];
    private Control _settings = null!;
    private VBoxContainer _settingsRows = null!;

    /// <summary>What the clock watch saw last, so only something new stops it.</summary>
    private int _clockTick = -1, _deadHeroes = -1, _crisisMark = -1, _motionSeen = -1;

    /// <summary>Whether events of this kind (a <see cref="ClockStops.All"/> id) stop the clock: the player's toggle, else the table's default.</summary>
    public bool ShouldStop(string id) => _clockStops.TryGetValue(id, out bool on) ? on : ClockStops.Of(id).Default;

    /// <summary>An event of this kind happened: stop the clock if the player wants it stopped for one. Every pause for an event goes through here.</summary>
    public void StopClock(string id)
    {
        if (_speed > 0 && !_opts.NoPause && !_opts.Smoke && ShouldStop(id)) SetSpeed(0);
    }

    private void LoadSettings()
    {
        _clockStops.Clear();
        if (ReadProfile()["settings"]?["clock"] is not JsonObject clock) return;
        foreach (var c in ClockStops.All)
            if (clock[c.Id] is JsonValue v && v.TryGetValue(out bool on)) _clockStops[c.Id] = on;
    }

    private void SaveSettings()
    {
        var clock = new JsonObject();
        foreach (var c in ClockStops.All) clock[c.Id] = ShouldStop(c.Id);
        var section = ReadProfile()["settings"] as JsonObject ?? [];
        section["clock"] = clock;
        WriteProfileSection("settings", section.DeepClone());
    }

    private void BuildSettings()
    {
        LoadSettings();
        _settings = new ColorRect { Color = new Color(0.05f, 0.04f, 0.03f, 0.6f), Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_settings);
        _settings.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        _settings.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(620, 0) };
        card.AddThemeStyleboxOverride("panel", Castle.Oak(30, 24));
        center.AddChild(card);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 12);
        card.AddChild(v);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 12);
        v.AddChild(head);
        var titles = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        titles.AddThemeConstantOverride("separation", 2);
        head.AddChild(titles);
        titles.AddChild(Ui.Label("Settings", Ui.Ink, Ui.Display, 28));
        titles.AddChild(Ui.Label("Kept for every town you play", Ui.Ink2, Ui.Medium, 12));
        var close = new Button { Text = "Close", TooltipText = "Back (Esc)", SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        close.Pressed += CloseSettings;
        head.AddChild(close);

        var rule = new ColorRect { Color = Ui.Gold, CustomMinimumSize = new Vector2(120, 2), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        v.AddChild(rule);
        v.AddChild(Ui.Label("STOP THE CLOCK FOR", Ui.Gold, Ui.Bold, 12));
        _settingsRows = new VBoxContainer();
        _settingsRows.AddThemeConstantOverride("separation", 6);
        v.AddChild(_settingsRows);
        var foot = Ui.Label("The game menu, the main menu and the end of a campaign always stop it. Space starts it again.", Ui.Ink2, Ui.Body, 12);
        foot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(foot);
    }

    private void RenderSettings()
    {
        Ui.Clear(_settingsRows);
        foreach (var c in ClockStops.All)
        {
            string key = c.Id;
            var row = new PanelContainer();
            row.AddThemeStyleboxOverride("panel", Castle.Ledge(12, 6));
            var line = new HBoxContainer();
            line.AddThemeConstantOverride("separation", 10);
            row.AddChild(line);
            var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            words.AddThemeConstantOverride("separation", 0);
            line.AddChild(words);
            words.AddChild(Ui.Label(c.Label, Ui.Ink, Ui.Bold, 15));
            var tip = Ui.Label(c.Tip, Ui.Ink2, Ui.Body, 12);
            tip.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            tip.CustomMinimumSize = new Vector2(440, 0);
            words.AddChild(tip);
            var box = new CheckButton { ButtonPressed = ShouldStop(key), FocusMode = FocusModeEnum.None, SizeFlagsVertical = SizeFlags.ShrinkCenter, TooltipText = c.Default ? "On unless you turn it off" : "Off unless you turn it on" };
            box.Toggled += on =>
            {
                _clockStops[key] = on;
                SaveSettings();
            };
            line.AddChild(box);
            _settingsRows.AddChild(row);
        }
    }

    private void OpenSettings()
    {
        HideHover();
        RenderSettings();
        _settings.Visible = true;
        _settings.MoveToFront();
    }

    private void CloseSettings() => _settings.Visible = false;

    /// <summary>Start the clock watch afresh on a new or loaded town, so what it already holds stops nothing.</summary>
    private void ResetClockWatch()
    {
        _clockTick = _deadHeroes = _crisisMark = -1;
        _motionSeen = _sim.Town.Motions.Count == 0 ? -1 : _sim.Town.Motions.Max(m => m.Id);
    }

    /// <summary>Once an hour of the sim: a hero dead, a crisis turned, a motion put: each stops the clock if the player wants.</summary>
    private void SyncClockStops()
    {
        var t = _sim.Town;
        if (t.Tick == _clockTick) return;
        _clockTick = t.Tick;
        int dead = t.Agents.Count(a => a.Hero != null && a.Dead);
        if (_deadHeroes >= 0 && dead > _deadHeroes) StopClock("deaths");
        _deadHeroes = dead;
        // Phases only move on and crises only end, so the sum rises at every turn of one.
        int mark = t.Crises.All.Sum(c => 1 + (int)c.Phase + (c.EndedDay != null ? 1 : 0));
        if (_crisisMark >= 0 && mark > _crisisMark) StopClock("crises");
        _crisisMark = mark;
        if (Council.PendingMotion(t) is { } m && m.Id > _motionSeen)
        {
            _motionSeen = m.Id;
            StopClock("motions");
        }
    }

    /// <summary>A major war entry about a foe in crisis, or the big bad, is crisis news; the rest is war and raids.</summary>
    private bool CrisisNews(LedgerEntry e)
    {
        var t = _sim.Town;
        foreach (var m in t.Saga.Menaces)
        {
            if (!m.BigBad && !t.Crises.All.Any(c => c.MenaceId == m.Id)) continue;
            if (e.Text.Contains(m.Name, StringComparison.Ordinal) || (m.Leader != "" && e.Text.Contains(m.Leader, StringComparison.Ordinal))) return true;
        }
        return false;
    }
}
