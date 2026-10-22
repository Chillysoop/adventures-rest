using System.Text.Json.Nodes;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>One thing a key does: its input action, its line on the Keys card, and its keys out of the box.</summary>
public sealed record KeyBinding(string Id, string Icon, string Label, Key[] Defaults);

/// <summary>
/// Every rebindable key, as Godot input actions (the map polls the pan keys, Main matches the rest). The Keys
/// tab of Settings lists them and rebinds them; the player's keys live in the profile's "settings.keys" section,
/// one list of key names per action, and an action not there keeps its defaults. <see cref="Fixed"/> are the
/// keys that go with the mouse or the number row, listed on the card but not rebindable.
/// </summary>
public static class Keys
{
    private static Key Ctrl(Key k) => (Key)((long)k | (long)KeyModifierMask.MaskCtrl);
    private static Key Alt(Key k) => (Key)((long)k | (long)KeyModifierMask.MaskAlt);

    public static readonly KeyBinding[] All =
    [
        new("pan_up", "compass", "Pan up", [Key.W, Key.Up]),
        new("pan_down", "compass", "Pan down", [Key.S, Key.Down]),
        new("pan_left", "compass", "Pan left", [Key.A, Key.Left]),
        new("pan_right", "compass", "Pan right", [Key.D, Key.Right]),
        new("rotate_left", "compass", "Turn left", [Key.Q]),
        new("rotate_right", "compass", "Turn right", [Key.E]),
        new("home", "tab-city", "Back to town", [Key.Home]),
        new("follow", "heroes", "Follow selected", [Key.F]),
        new("map", "tab-campaign", "Campaign map", [Key.M]),
        new("lens", "lore", "Lens", [Key.L]),
        new("pause", "order-defend", "Pause", [Key.Space]),
        new("faster", "order-strike", "Faster", [Key.Equal, Key.KpAdd]),
        new("slower", "order-guard", "Slower", [Key.Minus, Key.KpSubtract]),
        new("build", "tab-build", "Build", [Key.B]),
        new("undo", "tab-build", "Undo zone stroke", [Ctrl(Key.Z)]),
        new("brush_smaller", "tab-build", "Brush smaller", [Key.Bracketleft]),
        new("brush_bigger", "tab-build", "Brush bigger", [Key.Bracketright]),
        new("turn", "tab-build", "Turn decoration", [Key.R]),
        new("flag", "order-guard", "Guard flag", [Key.G]),
        new("mayor", "tab-mayor", "Mayor", [Key.C]),
        new("back", "tab-ledger", "Back", [Alt(Key.Left)]),
        new("glossary", "glossary", "Glossary", [Key.F1]),
        new("quicksave", "books", "Quicksave", [Key.F5]),
        new("quickload", "books", "Quickload", [Key.F9]),
        new("fullscreen", "face-all-seeing-eye", "Fullscreen", [Key.F11, Alt(Key.Enter)]),
    ];

    /// <summary>Keys that go with the mouse or the number row: on the card, not rebindable.</summary>
    public static readonly (string Keys, string Label)[] Fixed =
    [
        ("1 – 0", "Jump to a pin"),
        ("Ctrl + 1 – 0", "Pin what's selected"),
        ("Shift + 1 – 0", "Open a window"),
        ("Esc", "Close · menu"),
        ("Shift + click", "Place another"),
        ("Shift + wheel", "Brush size · turn"),
        ("Right-click", "Put the tool down"),
        ("Mouse back", "Back"),
    ];

    public static KeyBinding Of(string id) =>
        All.FirstOrDefault(b => b.Id == id) ?? throw new KeyNotFoundException($"No key binding \"{id}\": add it to Keys.All");

    /// <summary>Set up every action with the player's keys (from the profile), or its defaults.</summary>
    public static void Apply(JsonObject? custom)
    {
        foreach (var b in All)
        {
            if (!InputMap.HasAction(b.Id)) InputMap.AddAction(b.Id, 0.2f);
            InputMap.ActionEraseEvents(b.Id);
            var keys = custom?[b.Id] is JsonArray list ? list.Select(n => Parse(n?.GetValue<string>() ?? "")).Where(k => k != 0).ToArray() : b.Defaults;
            foreach (var k in keys) InputMap.ActionAddEvent(b.Id, Event(k));
        }
    }

    /// <summary>The player's keys as a profile section: only the actions moved off their defaults.</summary>
    public static JsonObject Write()
    {
        var o = new JsonObject();
        foreach (var b in All)
        {
            var now = Bound(b.Id);
            if (now.SequenceEqual(b.Defaults)) continue;
            o[b.Id] = new JsonArray(now.Select(k => (JsonNode)OS.GetKeycodeString(k)).ToArray());
        }
        return o;
    }

    /// <summary>The keys an action has now, modifiers included.</summary>
    public static List<Key> Bound(string id) =>
        InputMap.ActionGetEvents(id).OfType<InputEventKey>().Select(e => e.GetKeycodeWithModifiers()).ToList();

    /// <summary>The keys an action has now, as the player reads them ("W / Up").</summary>
    public static string Text(string id) => string.Join(" / ", Bound(id).Select(Name));

    public static string Name(Key k) => OS.GetKeycodeString(k).Replace("Equal", "+").Replace("Minus", "−").Replace("Kp Add", "Num +").Replace("Kp Subtract", "Num −");

    /// <summary>Give an action one key in place of its first (a second default, like the arrows, stays).</summary>
    public static void Rebind(string id, Key k)
    {
        // A key does one thing: take it off whatever had it.
        foreach (var b in All)
        {
            var had = Bound(b.Id);
            if (!had.Remove(k)) continue;
            Set(b.Id, had);
        }
        var keys = Bound(id);
        if (keys.Count == 0) keys.Add(k);
        else keys[0] = k;
        Set(id, keys);
    }

    public static void Reset()
    {
        foreach (var b in All) Set(b.Id, b.Defaults.ToList());
    }

    private static void Set(string id, List<Key> keys)
    {
        InputMap.ActionEraseEvents(id);
        foreach (var k in keys.Distinct()) InputMap.ActionAddEvent(id, Event(k));
    }

    private static Key Parse(string s) => s == "" ? 0 : OS.FindKeycodeFromString(s);

    private static InputEventKey Event(Key k)
    {
        long m = (long)k;
        return new InputEventKey
        {
            Keycode = (Key)(m & (long)KeyModifierMask.CodeMask),
            CtrlPressed = (m & (long)KeyModifierMask.MaskCtrl) != 0,
            AltPressed = (m & (long)KeyModifierMask.MaskAlt) != 0,
            ShiftPressed = (m & (long)KeyModifierMask.MaskShift) != 0,
        };
    }

    /// <summary>Whether a key press is this action's, modifiers and all.</summary>
    public static bool Is(InputEventKey k, string id) => k.IsAction(id, exactMatch: true);
}
