using System.Text.Json.Nodes;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The Settings window, from the game menu and the main menu, in tabs: Clock (which events stop the clock, one
/// toggle for each kind in <see cref="ClockStops.All"/>), Game (autosave, pause when away, fullscreen, interface
/// size), Camera (screen-edge pan, pan speed) and Keys (every rebindable key, <see cref="Keys"/>). All of it is
/// kept in the profile's "settings" section (user://profile.json, <see cref="ReadProfile"/>) so it holds for
/// every town; the sim never reads it. <see cref="StopClock"/> is the one call every pause site goes through.
/// </summary>
public partial class Main
{
    /// <summary>The player's toggles, by clock-stop id; a kind not here takes its default.</summary>
    private readonly Dictionary<string, bool> _clockStops = [];
    private Control _settings = null!;
    private VBoxContainer _settingsRows = null!;

    /// <summary>What the clock watch saw last, so only something new stops it.</summary>
    private int _clockTick = -1, _deadHeroes = -1, _crisisMark = -1, _motionSeen = -1, _mayorDown = -1;

    /// <summary>Whether events of this kind (a <see cref="ClockStops.All"/> id) stop the clock: the player's toggle, else the table's default.</summary>
    public bool ShouldStop(string id) => _clockStops.TryGetValue(id, out bool on) ? on : ClockStops.Of(id).Default;

    /// <summary>An event of this kind happened: stop the clock if the player wants it stopped for one. Every pause for an event goes through here.</summary>
    public void StopClock(string id)
    {
        if (_speed > 0 && !_opts.NoPause && !_opts.Smoke && ShouldStop(id)) SetSpeed(0);
    }

    /// <summary>Pause when the game window loses focus, and take the speed up again on return (Settings, on unless turned off).</summary>
    private bool _pauseAway = true;
    private float _uiScale = 1;

    private string _settingsTab = "clock";
    private HBoxContainer _settingsTabs = null!;

    /// <summary>The key being rebound on the Keys tab: the next key pressed goes to it.</summary>
    private string? _rebinding;

    private static readonly (string Id, string Icon, string Label)[] SettingsTabs =
    [
        ("clock", "order-defend", "Clock"),
        ("game", "books", "Game"),
        ("camera", "compass", "Camera"),
        ("keys", "tab-build", "Keys"),
    ];

    private void LoadSettings()
    {
        _clockStops.Clear();
        var section = ReadProfile()["settings"] as JsonObject;
        if (section?["clock"] is JsonObject clock)
            foreach (var c in ClockStops.All)
                if (clock[c.Id] is JsonValue v && v.TryGetValue(out bool on)) _clockStops[c.Id] = on;
        var game = section?["game"] as JsonObject;
        _autosaveDays = game?["autosaveDays"] is JsonValue ad && ad.TryGetValue(out int days) ? days : 7;
        _pauseAway = game?["pauseAway"] is not JsonValue pa || !pa.TryGetValue(out bool away) || away;
        var cam = section?["camera"] as JsonObject;
        _map.EdgePan = cam?["edgePan"] is not JsonValue ep || !ep.TryGetValue(out bool edge) || edge;
        _map.PanSpeed = cam?["panSpeed"] is JsonValue ps && ps.TryGetValue(out float speed) ? speed : 1;
        var display = section?["display"] as JsonObject;
        _uiScale = display?["uiScale"] is JsonValue us && us.TryGetValue(out float scale) ? scale : 1;
        GetTree().Root.ContentScaleFactor = _uiScale;
        // Fullscreen only for the game proper: screenshots and the smoke test keep their window.
        if (display?["fullscreen"] is JsonValue fs && fs.TryGetValue(out bool full) && full && _opts.Screenshot == null && !_opts.Smoke && !_opts.Bench)
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        Keys.Apply(section?["keys"] as JsonObject);
        Pane.Folded.Clear();
        if (section?["folds"] is JsonArray folds)
            foreach (var f in folds)
                if (f is JsonValue fv && fv.TryGetValue(out string? key) && key != null) Pane.Folded.Add(key);
    }

    private void SaveSettings()
    {
        var clock = new JsonObject();
        foreach (var c in ClockStops.All) clock[c.Id] = ShouldStop(c.Id);
        var section = ReadProfile()["settings"] as JsonObject ?? [];
        section["clock"] = clock;
        section["game"] = new JsonObject { ["autosaveDays"] = _autosaveDays, ["pauseAway"] = _pauseAway };
        section["camera"] = new JsonObject { ["edgePan"] = _map.EdgePan, ["panSpeed"] = _map.PanSpeed };
        section["display"] = new JsonObject { ["uiScale"] = _uiScale, ["fullscreen"] = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen };
        section["keys"] = Keys.Write();
        section["folds"] = new JsonArray(Pane.Folded.Order(StringComparer.Ordinal).Select(f => (JsonNode)JsonValue.Create(f)).ToArray());
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
        titles.AddChild(Ui.Label("For every town", Ui.Ink2, Ui.Medium, 12));
        var close = new Button { Text = "Close", TooltipText = "Back (Esc)", SizeFlagsVertical = SizeFlags.ShrinkCenter, FocusMode = FocusModeEnum.None };
        close.Pressed += CloseSettings;
        head.AddChild(close);

        _settingsTabs = new HBoxContainer();
        _settingsTabs.AddThemeConstantOverride("separation", 6);
        foreach (var (id, icon, label) in SettingsTabs)
        {
            var b = new Button { Text = label, Icon = Ui.Icon(icon), ExpandIcon = false, ToggleMode = true, ThemeTypeVariation = "Tab", FocusMode = FocusModeEnum.None };
            b.Pressed += () =>
            {
                _settingsTab = id;
                _rebinding = null;
                RenderSettings();
            };
            b.SetMeta("tab", id);
            _settingsTabs.AddChild(b);
        }
        v.AddChild(_settingsTabs);
        var rule = new ColorRect { Color = Ui.Gold, CustomMinimumSize = new Vector2(120, 2), SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        v.AddChild(rule);
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, 470) };
        _settingsRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _settingsRows.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_settingsRows);
        v.AddChild(scroll);
    }

    private void RenderSettings()
    {
        foreach (var b in _settingsTabs.GetChildren().OfType<Button>()) b.ButtonPressed = (string)b.GetMeta("tab") == _settingsTab;
        Ui.Clear(_settingsRows);
        switch (_settingsTab)
        {
            case "clock":
                SettingsHeading("STOP THE CLOCK FOR");
                foreach (var c in ClockStops.All)
                {
                    string key = c.Id;
                    SettingRow(c.Label, c.Tip, Toggle(ShouldStop(key), c.Default ? "On unless you turn it off" : "Off unless you turn it on", on => _clockStops[key] = on));
                }
                SettingsFoot("Menus always pause · Space resumes");
                break;
            case "game":
                SettingsHeading("SAVING");
                SettingRow("Autosave", "On quit too", Choice([(1, "1 day"), (3, "3 days"), (7, "7 days"), (0, "Never")], _autosaveDays, d => _autosaveDays = d));
                SettingsHeading("CLOCK");
                SettingRow("Pause when away", "Switching to another window pauses · back resumes", Toggle(_pauseAway, "On unless you turn it off", on => _pauseAway = on));
                SettingsHeading("DISPLAY");
                bool full = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
                SettingRow("Fullscreen", $"{Keys.Text("fullscreen")}", Toggle(full, "", _ => ToggleFullscreen()));
                SettingRow("Interface size", "Menus, windows and text", Choice([(85, "S"), (100, "M"), (115, "L"), (130, "XL")], (int)Math.Round(_uiScale * 100), pct =>
                {
                    _uiScale = pct / 100f;
                    GetTree().Root.ContentScaleFactor = _uiScale;
                }));
                break;
            case "camera":
                SettingsHeading("CAMERA");
                SettingRow("Screen-edge pan", "Rest the cursor on the edge of the screen", Toggle(_map.EdgePan, "On unless you turn it off", on => _map.EdgePan = on));
                SettingRow("Pan speed", $"Keys: {Keys.Text("pan_up")} · {Keys.Text("pan_left")} · {Keys.Text("pan_down")} · {Keys.Text("pan_right")}", Choice([(50, "Slow"), (100, "Normal"), (160, "Fast"), (240, "Fastest")], (int)Math.Round(_map.PanSpeed * 100), pct => _map.PanSpeed = pct / 100f));
                SettingsFoot("Ctrl + 1 – 0 pins what's selected · 1 – 0 goes back to it");
                break;
            case "keys":
                RenderKeys();
                break;
        }
    }

    private void RenderKeys()
    {
        SettingsHeading("KEYS · CLICK ONE, THEN PRESS A NEW KEY");
        foreach (var b in Keys.All)
        {
            string id = b.Id;
            bool waiting = _rebinding == id;
            var key = new Button { Text = waiting ? "Press a key…" : Keys.Text(id), ThemeTypeVariation = waiting ? "Post" : "", CustomMinimumSize = new Vector2(150, 0), FocusMode = FocusModeEnum.None, TooltipText = waiting ? "Esc cancels" : "Click to change" };
            key.Pressed += () =>
            {
                _rebinding = waiting ? null : id;
                RenderSettings();
            };
            SettingRow(b.Label, "", key, b.Icon);
        }
        SettingsHeading("ALWAYS");
        foreach (var (keys, label) in Keys.Fixed)
            SettingRow(label, "", Ui.Label(keys, Ui.Ink2, Ui.Bold, 13));
        var reset = new Button { Text = "Reset keys", FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        reset.Pressed += () =>
        {
            Keys.Reset();
            _rebinding = null;
            SaveSettings();
            RenderSettings();
        };
        _settingsRows.AddChild(reset);
    }

    /// <summary>The Keys tab is waiting for a key: take this one (Esc cancels).</summary>
    private bool RebindKey(InputEventKey k)
    {
        if (_rebinding is not string id) return false;
        if (k.Keycode is Key.Ctrl or Key.Shift or Key.Alt) return true;
        if (k.Keycode != Key.Escape)
        {
            Keys.Rebind(id, k.GetKeycodeWithModifiers());
            SaveSettings();
        }
        _rebinding = null;
        RenderSettings();
        return true;
    }

    private void SettingsHeading(string text) => _settingsRows.AddChild(Ui.Label(text, Ui.Gold, Ui.Bold, 12));

    private void SettingsFoot(string text)
    {
        var foot = Ui.Label(text, Ui.Ink2, Ui.Body, 12);
        foot.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _settingsRows.AddChild(foot);
    }

    /// <summary>One setting: its name and a short line under it on the left, its control on the right.</summary>
    private void SettingRow(string label, string tip, Control control, string? icon = null)
    {
        var row = new PanelContainer();
        row.AddThemeStyleboxOverride("panel", Castle.Ledge(12, 6));
        var line = new HBoxContainer();
        line.AddThemeConstantOverride("separation", 10);
        row.AddChild(line);
        if (icon != null) line.AddChild(new TextureRect { Texture = Ui.Icon(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, CustomMinimumSize = new Vector2(18, 18), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, SizeFlagsVertical = SizeFlags.ShrinkCenter });
        var words = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        words.AddThemeConstantOverride("separation", 0);
        line.AddChild(words);
        words.AddChild(Ui.Label(label, Ui.Ink, Ui.Bold, 15));
        if (tip != "")
        {
            var t = Ui.Label(tip, Ui.Ink2, Ui.Body, 12);
            t.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            t.CustomMinimumSize = new Vector2(320, 0);
            words.AddChild(t);
        }
        control.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        line.AddChild(control);
        _settingsRows.AddChild(row);
    }

    private CheckButton Toggle(bool on, string tip, Action<bool> set)
    {
        var box = new CheckButton { ButtonPressed = on, FocusMode = FocusModeEnum.None, TooltipText = tip };
        box.Toggled += v =>
        {
            set(v);
            SaveSettings();
        };
        return box;
    }

    /// <summary>A row of buttons, one lit: autosave days, interface size, pan speed.</summary>
    private HBoxContainer Choice((int Value, string Label)[] options, int now, Action<int> set)
    {
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 4);
        foreach (var (value, label) in options)
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonPressed = value == now, ThemeTypeVariation = "Speed", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(44, 0) };
            b.Pressed += () =>
            {
                set(value);
                SaveSettings();
                RenderSettings();
            };
            h.AddChild(b);
        }
        return h;
    }

    private void OpenSettings(string? tab = null)
    {
        HideHover();
        if (tab != null) _settingsTab = tab;
        _rebinding = null;
        RenderSettings();
        _settings.Visible = true;
        _settings.MoveToFront();
    }

    private void CloseSettings()
    {
        _rebinding = null;
        _settings.Visible = false;
    }

    /// <summary>Start the clock watch afresh on a new or loaded town, so what it already holds stops nothing.</summary>
    private void ResetClockWatch()
    {
        _clockTick = _deadHeroes = _crisisMark = _mayorDown = _rideSeen = _marchSeen = -1;
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
        int down = Mayoralty.Dead(t) ? 1 : 0;
        if (_mayorDown == 0 && down == 1) StopClock("mayor");
        _mayorDown = down;
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
