using System.Globalization;
using System.Text.Json;
using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Saving and loading: a game menu (Esc) with five save slots, three rotating autosaves and a
/// quicksave (F5, F9 to load it). The town autosaves every few days and on quit, and the game
/// opens on the newest save when started without arguments. Saves live in the user data folder,
/// so a new build keeps them.
/// </summary>
public partial class Main
{
    private const int ManualSlots = 5, AutoSlots = 3, AutosaveDays = 3;

    private Control _menu = null!;
    private VBoxContainer _slotList = null!;
    private Label _menuTitle = null!, _menuStatus = null!, _toast = null!;
    private HBoxContainer _menuActions = null!;
    private int _speedBeforeMenu = 1;
    private int _lastAutosaveDay;
    private System.Threading.Tasks.Task? _writing;

    private static string SaveDir => ProjectSettings.GlobalizePath("user://saves");

    private static string SlotPath(string slot) => Path.Combine(SaveDir, $"{slot}.save");

    private static string SlotTitle(string slot) => slot switch
    {
        "quick" => "Quicksave",
        _ when slot.StartsWith("auto-") => $"Autosave {slot[5..]}",
        _ => $"Slot {slot[5..]}",
    };

    private void BuildMenu()
    {
        _menu = new ColorRect { Color = new Color(0.05f, 0.04f, 0.03f, 0.6f), Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_menu);
        _menu.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        _menu.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(680, 0) };
        card.AddThemeStyleboxOverride("panel", Castle.Oak(26, 22));
        center.AddChild(card);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        card.AddChild(v);

        _menuTitle = Ui.Label("", Ui.Ink, Ui.Display, 22);
        v.AddChild(_menuTitle);
        var actions = new HBoxContainer();
        actions.AddThemeConstantOverride("separation", 8);
        var resume = new Button { Text = "Resume", ThemeTypeVariation = "Post", TooltipText = "Back to the town (Esc)" };
        resume.Pressed += CloseMenu;
        var quick = new Button { Text = "Quicksave", TooltipText = "Save to the quicksave (F5); F9 loads it" };
        quick.Pressed += () => Save("quick");
        var fresh = new Button { Text = "New town", TooltipText = "Start a fresh town with a random seed (save first: this one is not kept)" };
        fresh.Pressed += () =>
        {
            CloseMenu();
            NewTown((int)(GD.Randi() % 9999) + 1);
        };
        var quit = new Button { Text = "Save and quit", TooltipText = "Autosave, then close the game" };
        quit.Pressed += () =>
        {
            Autosave(now: true);
            GetTree().Quit();
        };
        var tutorial = new Button { Text = "Tutorial", TooltipText = "Run the tutorial again on this town" };
        tutorial.Pressed += () =>
        {
            StartTutorial();
            CloseMenu();
        };
        var title = new Button { Text = "Main menu", TooltipText = "Autosave, then go back to the main menu" };
        title.Pressed += () =>
        {
            Autosave(now: true);
            ShowTitle();
        };
        var history = new Button { Text = "History", TooltipText = "The ages, empires and peoples before your town" };
        history.Pressed += () => OpenHistory();
        var glossary = new Button { Text = "Glossary", TooltipText = "Every keyword in the game, explained (F1)" };
        glossary.Pressed += () => OpenGlossary();
        foreach (var b in new[] { resume, quick, fresh, tutorial, history, glossary, title, quit }) actions.AddChild(b);
        v.AddChild(actions);
        _menuActions = actions;
        v.AddChild(new HSeparator());

        _slotList = new VBoxContainer();
        _slotList.AddThemeConstantOverride("separation", 4);
        v.AddChild(_slotList);
        _menuStatus = Ui.Label("", Ui.Ink2, Ui.Medium, 12);
        _menuStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(_menuStatus);

        _toast = Ui.Label("", new Color("f6eedb"), Ui.Bold, 13);
        var pill = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Visible = false };
        pill.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0.08f, 0.06f, 0.04f, 0.85f), 14, 6, 99));
        pill.AddChild(_toast);
        AddChild(pill);
        pill.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize, TopBar + 44);
        pill.GrowHorizontal = GrowDirection.Both;
    }

    private void ToggleMenu()
    {
        if (_menu.Visible) CloseMenu();
        else OpenMenu();
    }

    private void OpenMenu(string? status = null)
    {
        if (!_menu.Visible) _speedBeforeMenu = _speed;
        SetSpeed(0);
        _menu.Visible = true;
        _menuActions.Visible = !_menuLoadOnly;
        _menuStatus.Text = status ?? "";
        RenderSlots();
        if (!_menuLoadOnly) GuideEvent("menu");
    }

    private void CloseMenu()
    {
        _menu.Visible = false;
        // Load from the main menu goes back to it, with the clock still stopped.
        if (_menuLoadOnly)
        {
            _menuLoadOnly = false;
            return;
        }
        SetSpeed(_speedBeforeMenu);
    }

    private void RenderSlots()
    {
        var t = _sim.Town;
        _menuTitle.Text = _menuLoadOnly ? "Load a town" : $"{t.Name} · Day {t.Day} · seed {_seed}";
        Ui.Clear(_slotList);
        Section("Saved games");
        for (int i = 1; i <= ManualSlots; i++) SlotRow($"slot-{i}", canSave: !_menuLoadOnly);
        Section("Autosaves");
        SlotRow("quick", canSave: false);
        for (int i = 1; i <= AutoSlots; i++) SlotRow($"auto-{i}", canSave: false);
    }

    private void Section(string title)
    {
        var l = Ui.Label(title.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 10);
        l.CustomMinimumSize = new Vector2(0, 20);
        l.VerticalAlignment = VerticalAlignment.Bottom;
        _slotList.AddChild(l);
    }

    private void SlotRow(string slot, bool canSave)
    {
        var header = Header(slot);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = Ui.Label(SlotTitle(slot), Ui.Ink, Ui.Bold, 13);
        name.CustomMinimumSize = new Vector2(96, 0);
        row.AddChild(name);
        var about = Ui.Label(header == null ? "Empty" : Describe(header), header == null ? Ui.Ink2 : Ui.Ink, Ui.Body, 12);
        about.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        about.ClipText = true;
        row.AddChild(about);
        if (canSave)
        {
            var save = new Button { Text = "Save", TooltipText = header == null ? "Save the town here" : "Save over this slot" };
            save.Pressed += () => Save(slot);
            row.AddChild(save);
        }
        var load = new Button { Text = "Load", Disabled = header == null, ThemeTypeVariation = header == null ? "" : "Post" };
        load.Pressed += () => Load(slot);
        row.AddChild(load);
        _slotList.AddChild(row);
    }

    private static string Describe(SaveHeader h)
    {
        string when = DateTime.TryParse(h.Saved, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture)
            : "";
        return $"{h.Town}, day {h.Day} · {h.Population} people · {Math.Floor(h.Treasury)} gold · {when}";
    }

    private static SaveHeader? Header(string slot)
    {
        try
        {
            return File.Exists(SlotPath(slot)) ? SaveGame.ReadHeader(SlotPath(slot)) : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Take the state now and write it in the background; the next save waits for this one.</summary>
    private void Save(string slot, bool wait = false)
    {
        var snap = SaveGame.Capture(_sim, ClientData());
        var prev = _writing;
        string path = SlotPath(slot);
        _writing = System.Threading.Tasks.Task.Run(() =>
        {
            prev?.Wait();
            snap.WriteFile(path);
        });
        if (wait) _writing.Wait();
        if (slot.StartsWith("auto-")) return;
        GuideEvent("save");
        Toast($"Saved to {SlotTitle(slot).ToLowerInvariant()}");
        if (_menu.Visible)
        {
            _writing.Wait();
            RenderSlots();
            _menuStatus.Text = $"Saved {_sim.Town.Name}, day {_sim.Town.Day}, to {SlotTitle(slot).ToLowerInvariant()}.";
        }
    }

    private bool Load(string slot) => LoadFile(SlotPath(slot), SlotTitle(slot).ToLowerInvariant());

    private bool LoadFile(string path, string what)
    {
        _writing?.Wait();
        try
        {
            var sim = SaveGame.ReadFile(path, out var client);
            Adopt(sim);
            RestoreClientData(client);
            if (_menu.Visible) CloseMenu();
            HideTitle();
            Toast($"Loaded {what}: {sim.Town.Name}, day {sim.Town.Day}");
            return true;
        }
        catch (Exception e) when (e is SaveException or IOException)
        {
            if (_menu.Visible) _menuStatus.Text = $"Could not load {what}. {e.Message}";
            else Toast($"Could not load {what}");
            GD.PrintErr($"Load {path}: {e}");
            return false;
        }
    }

    /// <summary>
    /// Once every few in-game days, into the oldest of the rotating autosaves. It goes on the first frame of a new
    /// stretch of days, not on midnight exactly: at high speed a frame runs several hours and can step over midnight.
    /// </summary>
    private void Autosave(bool now = false)
    {
        int day = _sim.Town.Day;
        if (!now && day / AutosaveDays == _lastAutosaveDay / AutosaveDays) return;
        _lastAutosaveDay = day;
        string slot = Enumerable.Range(1, AutoSlots)
            .Select(i => $"auto-{i}")
            .OrderBy(s => File.Exists(SlotPath(s)) ? File.GetLastWriteTimeUtc(SlotPath(s)) : DateTime.MinValue)
            .First();
        Save(slot, wait: now);
        RememberLegends();
    }

    /// <summary>Continue on the main menu: pick up where the player left off, from the newest save of any kind.</summary>
    private bool Continue()
    {
        if (!Directory.Exists(SaveDir)) return false;
        var newest = new DirectoryInfo(SaveDir).GetFiles("*.save").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
        return newest != null && LoadFile(newest.FullName, SlotTitle(Path.GetFileNameWithoutExtension(newest.Name)).ToLowerInvariant());
    }

    public override void _Notification(int what)
    {
        // On the main menu the town behind it is either a showcase or already autosaved on the way out.
        if (what == NotificationWMCloseRequest && _sim != null && _opts.Screenshot == null && !_opts.Bench && !_title.Visible)
        {
            Autosave(now: true);
            GetTree().Quit();
        }
    }

    private void Toast(string text)
    {
        _toast.Text = text;
        var pill = _toast.GetParent<Control>();
        pill.Visible = true;
        pill.Modulate = Colors.White;
        var tw = CreateTween();
        tw.TweenInterval(2.2);
        tw.TweenProperty(pill, "modulate:a", 0.0, 0.6);
        tw.TweenCallback(Callable.From(() => pill.Visible = false));
    }

    private sealed record SavedMarker(string Kind, float X, float Y, int Day, string? Label = null);

    /// <summary>The client's own part of a save: the player's map markers.</summary>
    private string ClientData() =>
        JsonSerializer.Serialize(_map.Markers.Select(m => new SavedMarker(m.Kind.ToString(), m.P.X, m.P.Y, m.Day, m.Label)).ToList());

    private void RestoreClientData(string? json)
    {
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            foreach (var m in JsonSerializer.Deserialize<List<SavedMarker>>(json) ?? [])
                if (Enum.TryParse<MarkerKind>(m.Kind, out var kind)) _map.Markers.Add(new Marker(kind, new Vector2(m.X, m.Y), m.Day, m.Label ?? ""));
        }
        catch (JsonException)
        {
            // Markers are a convenience; a save without readable ones still loads.
        }
    }
}
