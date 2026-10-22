using System.Globalization;
using System.Text.Json;
using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Saving and loading: a game menu (Esc) with named saves (as many as the player likes, each can be saved
/// over or deleted), three rotating autosaves and a quicksave (F5, F9 to load it). The town autosaves every
/// few days (Settings: 1, 3 or 7, or never) and on quit. Loading over play that isn't saved asks first, and a
/// loaded town opens paused. Saves live in the user data folder, so a new build keeps them; the old numbered
/// slots (slot-1...) show as saves named "Slot 1"...
/// </summary>
public partial class Main
{
    private const int AutoSlots = 3;

    /// <summary>Days between autosaves (Settings), 0 for never.</summary>
    private int _autosaveDays = 7;

    /// <summary>The tick the town was last saved, loaded or begun at: what a load would throw away is play since.</summary>
    private int _savedTick;

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
        _ when slot.StartsWith("slot-") => $"Slot {slot[5..]}",
        _ => slot,
    };

    private static bool IsAuto(string slot) => slot == "quick" || slot.StartsWith("auto-");

    /// <summary>The player's own saves, newest first, by file name.</summary>
    private static List<string> NamedSaves() =>
        !Directory.Exists(SaveDir) ? [] : new DirectoryInfo(SaveDir).GetFiles("*.save")
            .Where(f => !IsAuto(Path.GetFileNameWithoutExtension(f.Name)))
            .OrderByDescending(f => f.LastWriteTimeUtc).ThenBy(f => f.Name, StringComparer.Ordinal)
            .Select(f => Path.GetFileNameWithoutExtension(f.Name)).ToList();

    /// <summary>A save name the file system takes: no path characters, not a reserved slot.</summary>
    private static string CleanName(string name)
    {
        var bad = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        string clean = new string(name.Trim().Select(c => bad.Contains(c) ? ' ' : c).ToArray()).Trim().TrimEnd('.');
        if (clean.Length > 60) clean = clean[..60].Trim();
        return clean == "" || IsAuto(clean) ? "" : clean;
    }

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
        var fresh = new Button { Text = "New town", TooltipText = "Start a fresh town with a random seed" };
        fresh.Pressed += () =>
        {
            void Fresh()
            {
                CloseMenu();
                NewTown((int)(GD.Randi() % 9999) + 1);
                SetSpeed(0);
            }
            if (Unsaved() is string lose) Ask("Start a new town?", $"• Unsaved play is lost: {lose}", "New town", Fresh);
            else Fresh();
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
        var settings = new Button { Text = "Settings", TooltipText = "Clock stops, autosave, camera, display" };
        settings.Pressed += () => OpenSettings();
        var keys = new Button { Text = "Keys", TooltipText = "Every key, and changing them" };
        keys.Pressed += () => OpenSettings("keys");
        foreach (var b in new[] { resume, quick, fresh, tutorial, history, glossary, settings, keys, title, quit }) actions.AddChild(b);
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

    private LineEdit? _saveName;

    private void RenderSlots()
    {
        var t = _sim.Town;
        _menuTitle.Text = _menuLoadOnly ? "Load a town" : $"{t.Name} · Day {t.Day} · seed {_seed}";
        Ui.Clear(_slotList);
        if (!_menuLoadOnly)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            _saveName = new LineEdit { Text = $"{t.Name}, day {t.Day}", SizeFlagsHorizontal = SizeFlags.ExpandFill, PlaceholderText = "Name this save", MaxLength = 60 };
            _saveName.TextSubmitted += _ => SaveNamed();
            row.AddChild(_saveName);
            var save = new Button { Text = "Save", ThemeTypeVariation = "Post", TooltipText = "Save the town under this name" };
            save.Pressed += SaveNamed;
            row.AddChild(save);
            _slotList.AddChild(row);
        }
        var named = NamedSaves();
        Section("Saved games");
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 4);
        foreach (var slot in named) SlotRow(list, slot, canSave: !_menuLoadOnly, canDelete: true);
        if (named.Count == 0) list.AddChild(Ui.Label("None yet", Ui.Ink2, Ui.Body, 12));
        // Many saves scroll inside the menu rather than push it off the screen.
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, CustomMinimumSize = new Vector2(0, Math.Min(260, Math.Max(1, named.Count) * 34)) };
        list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        scroll.AddChild(list);
        _slotList.AddChild(scroll);
        Section("Autosaves");
        SlotRow(_slotList, "quick", canSave: false, canDelete: false);
        for (int i = 1; i <= AutoSlots; i++) SlotRow(_slotList, $"auto-{i}", canSave: false, canDelete: false);
    }

    private void SaveNamed()
    {
        string name = CleanName(_saveName?.Text ?? "");
        if (name == "")
        {
            _menuStatus.Text = "Give the save a name.";
            return;
        }
        if (Header(name) is { } h)
            Ask($"Save over {name}?", Describe(h), "Save over", () => Save(name));
        else Save(name);
    }

    private void Section(string title)
    {
        var l = Ui.Label(title.ToUpperInvariant(), Ui.Ink2, Ui.Bold, 10);
        l.CustomMinimumSize = new Vector2(0, 20);
        l.VerticalAlignment = VerticalAlignment.Bottom;
        _slotList.AddChild(l);
    }

    private void SlotRow(VBoxContainer into, string slot, bool canSave, bool canDelete)
    {
        var header = Header(slot);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var name = Ui.Label(SlotTitle(slot), Ui.Ink, Ui.Bold, 13);
        name.CustomMinimumSize = new Vector2(150, 0);
        name.ClipText = true;
        row.AddChild(name);
        var about = Ui.Label(header == null ? "Empty" : Describe(header), header == null ? Ui.Ink2 : Ui.Ink, Ui.Body, 12);
        about.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        about.ClipText = true;
        row.AddChild(about);
        if (canSave)
        {
            var save = new Button { Text = "Save over", TooltipText = "Save the town over this one" };
            save.Pressed += () => Ask($"Save over {SlotTitle(slot)}?", header == null ? "" : Describe(header), "Save over", () => Save(slot));
            row.AddChild(save);
        }
        var load = new Button { Text = "Load", Disabled = header == null, ThemeTypeVariation = header == null ? "" : "Post" };
        load.Pressed += () => AskLoad(slot);
        row.AddChild(load);
        if (canDelete)
        {
            var del = new Button { Text = "✕", ThemeTypeVariation = "Speed", TooltipText = "Delete this save", FocusMode = FocusModeEnum.None };
            del.Pressed += () => Ask($"Delete {SlotTitle(slot)}?", header == null ? "" : Describe(header), "Delete", () =>
            {
                _writing?.Wait();
                try
                {
                    File.Delete(SlotPath(slot));
                }
                catch (IOException e)
                {
                    GD.PushWarning($"Could not delete {slot}: {e.Message}");
                }
                RenderSlots();
            });
            row.AddChild(del);
        }
        into.AddChild(row);
    }

    private static string Describe(SaveHeader h)
    {
        string when = DateTime.TryParse(h.Saved, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var d)
            ? d.ToLocalTime().ToString("d MMM HH:mm", CultureInfo.InvariantCulture)
            : "";
        return $"{h.Town}, day {h.Day} · {h.Population} households · {Math.Floor(h.Treasury)} gold · {when}";
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
        _savedTick = _sim.Town.Tick;
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

    private bool Load(string slot) => LoadFile(SlotPath(slot), SlotTitle(slot));

    /// <summary>Play since the last save that a load or a new town would throw away, as words, or null if none would be lost.</summary>
    private string? Unsaved()
    {
        if (_title.Visible || _sim.Options.AutoBounties || _sim.Town.Outcome != null) return null;
        int hours = _sim.Town.Tick - _savedTick;
        if (hours < 2) return null;
        return hours < 24 ? $"{hours} hours" : hours < 48 ? "a day" : $"{hours / 24} days";
    }

    /// <summary>Load, asking first if that would throw away play since the last save.</summary>
    private void AskLoad(string slot)
    {
        if (Header(slot) == null)
        {
            Toast($"No {SlotTitle(slot).ToLowerInvariant()} yet");
            return;
        }
        if (Unsaved() is string lose) Ask($"Load {SlotTitle(slot)}?", $"• Unsaved play is lost: {lose}", "Load", () => Load(slot));
        else Load(slot);
    }

    private Control _ask = null!;
    private Label _askTitle = null!, _askBody = null!;
    private Button _askYes = null!;
    private Action? _askThen;

    private void BuildAsk()
    {
        _ask = new ColorRect { Color = new Color(0.05f, 0.04f, 0.03f, 0.55f), Visible = false, MouseFilter = MouseFilterEnum.Stop };
        AddChild(_ask);
        _ask.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var center = new CenterContainer();
        _ask.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(380, 0) };
        card.AddThemeStyleboxOverride("panel", Castle.Oak(22, 18));
        center.AddChild(card);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        card.AddChild(v);
        _askTitle = Ui.Label("", Ui.Ink, Ui.Display, 20);
        v.AddChild(_askTitle);
        _askBody = Ui.Label("", Ui.Ink2, Ui.Body, 13);
        _askBody.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        v.AddChild(_askBody);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 8);
        var no = new Button { Text = "Cancel", TooltipText = "Esc" };
        no.Pressed += () => _ask.Visible = false;
        _askYes = new Button { ThemeTypeVariation = "Post", TooltipText = "Enter" };
        _askYes.Pressed += () =>
        {
            _ask.Visible = false;
            var then = _askThen;
            _askThen = null;
            then?.Invoke();
        };
        row.AddChild(no);
        row.AddChild(_askYes);
        v.AddChild(row);
    }

    /// <summary>A yes-or-cancel card in front of everything, for a step that throws something away.</summary>
    private void Ask(string title, string body, string yes, Action then)
    {
        _askTitle.Text = title;
        _askBody.Text = body;
        _askBody.Visible = body != "";
        _askYes.Text = yes;
        _askThen = then;
        _ask.Visible = true;
        _ask.MoveToFront();
    }

    private bool LoadFile(string path, string what)
    {
        _writing?.Wait();
        try
        {
            var sim = SaveGame.ReadFile(path, out var client);
            Adopt(sim);
            RestoreClientData(client);
            // A loaded town opens paused: Space (or a speed) picks up where it left off.
            _speedBeforeMenu = 0;
            if (_menu.Visible) CloseMenu();
            HideTitle();
            SetSpeed(0);
            Toast($"Loaded {what}: {sim.Town.Name}, day {sim.Town.Day}");
            // A session opens with "last time in...".
            if (!_opts.Smoke && _opts.Screenshot == null) OpenRecap(Math.Max(1, sim.Town.Day - 15), "Welcome back");
            return true;
        }
        catch (Exception e) when (e is SaveException or IOException)
        {
            if (_menu.Visible) _menuStatus.Text = $"Could not load {what}. {e.Message}";
            else Toast($"Could not load {what.ToLowerInvariant()}");
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
        if (!now && (_autosaveDays <= 0 || day / _autosaveDays == _lastAutosaveDay / _autosaveDays)) return;
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
        return newest != null && LoadFile(newest.FullName, SlotTitle(Path.GetFileNameWithoutExtension(newest.Name)));
    }

    public override void _Notification(int what)
    {
        // On the main menu the town behind it is either a showcase or already autosaved on the way out.
        if (what == NotificationWMCloseRequest && _sim != null && _opts.Screenshot == null && !_opts.Bench && !_title.Visible)
        {
            Autosave(now: true);
            GetTree().Quit();
        }
        else if (what == NotificationApplicationFocusOut) FocusChanged(false);
        else if (what == NotificationApplicationFocusIn) FocusChanged(true);
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

    private sealed record SavedClient(List<SavedMarker> Markers, List<SavedPin>? Pins);

    /// <summary>The client's own part of a save: the player's map markers and camera pins.</summary>
    private string ClientData() =>
        JsonSerializer.Serialize(new SavedClient(_map.Markers.Select(m => new SavedMarker(m.Kind.ToString(), m.P.X, m.P.Y, m.Day, m.Label)).ToList(), SavedPins()));

    private void RestoreClientData(string? json)
    {
        RestorePins(null);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            // Older saves hold the markers alone, as a list.
            var saved = json.TrimStart().StartsWith('[')
                ? new SavedClient(JsonSerializer.Deserialize<List<SavedMarker>>(json) ?? [], null)
                : JsonSerializer.Deserialize<SavedClient>(json);
            foreach (var m in saved?.Markers ?? [])
                if (Enum.TryParse<MarkerKind>(m.Kind, out var kind)) _map.Markers.Add(new Marker(kind, new Vector2(m.X, m.Y), m.Day, m.Label ?? ""));
            RestorePins(saved?.Pins);
        }
        catch (JsonException)
        {
            // Markers and pins are a convenience; a save without readable ones still loads.
        }
    }
}
