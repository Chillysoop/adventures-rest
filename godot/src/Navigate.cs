using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// Getting about: camera pins on the number row (Ctrl+1–0 pins what is selected, or the view if nothing is; 1–0
/// goes back to it and rides along with a person), following what is selected (F), Back (Alt+Left or the mouse's
/// back button) to the window or card you came from, flying to whatever a link names, fullscreen, and pausing when
/// the game loses focus. Pins are kept in the save's client data.
/// </summary>
public partial class Main
{
    /// <summary>A camera pin: a thing to go to (and follow, if it moves), or a spot and zoom.</summary>
    private sealed record CameraPin(Selection? Thing, Vector2 Spot, float Dist, string Label);

    private readonly CameraPin?[] _pins = new CameraPin?[10];
    private HBoxContainer _pinRow = null!;

    /// <summary>Where the player has been: the window and card open before each jump, newest last.</summary>
    private readonly List<(Tab? Window, Selection? Card)> _back = [];
    private bool _goingBack;
    private const int BackMost = 30;

    private static string PinKey(int slot) => ((slot + 1) % 10).ToString();

    private void SetPin(int slot)
    {
        var sel = _map.Selection;
        string label = sel != null ? PinLabel(sel) : $"View, day {_sim.Town.Day}";
        // A wall section or a flag has no place to fly to: pin the view on it instead.
        if (sel != null && _map.Where(sel) == null) sel = null;
        _pins[slot] = new CameraPin(sel, _map.Target, _map.Distance, label);
        Toast($"Pin {PinKey(slot)}: {label}");
        RenderPins();
        GuideEvent("pin");
    }

    private void GoToPin(int slot)
    {
        if (_pins[slot] is not { } pin)
        {
            Toast($"Ctrl+{PinKey(slot)} pins what's selected");
            return;
        }
        if (_map.Strategic) ToggleStrategic();
        if (pin.Thing is { } thing && _map.Where(thing) is Vector2 at)
        {
            _map.FlyTo(at, Math.Min(_map.Distance, 30));
            Select(thing);
            // People and parties move: the camera stays with them.
            if (thing.Kind == "agent") _map.Follow = thing;
        }
        else _map.FlyTo(pin.Spot, pin.Dist);
    }

    private void ClearPin(int slot)
    {
        _pins[slot] = null;
        RenderPins();
    }

    private string PinLabel(Selection s)
    {
        var t = _sim.Town;
        return s.Kind switch
        {
            "agent" when s.Id >= 1 && s.Id <= t.Agents.Count => t.Agent(s.Id).Name,
            "building" when s.Id >= 1 && s.Id <= t.Buildings.Count => t.Building(s.Id).Name,
            "site" => Sim.Systems.Quests.Site(t, s.SiteId)?.Def.Name ?? s.SiteId,
            _ => "A spot on the walls",
        };
    }

    private Control BuildPins()
    {
        _pinRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End, Visible = false };
        _pinRow.AddThemeConstantOverride("separation", 3);
        return _pinRow;
    }

    /// <summary>One small numbered chip per pin above the minimap: click to go, right-click to clear.</summary>
    private void RenderPins()
    {
        Ui.Clear(_pinRow);
        for (int i = 0; i < _pins.Length; i++)
        {
            if (_pins[i] is not { } pin) continue;
            int slot = i;
            var b = new Button { Text = PinKey(i), ThemeTypeVariation = "Speed", FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 0), TooltipText = $"{pin.Label}\n• {PinKey(i)}: go\n• Right-click: clear" };
            b.Pressed += () => GoToPin(slot);
            b.GuiInput += e =>
            {
                if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right }) ClearPin(slot);
            };
            _pinRow.AddChild(b);
        }
        _pinRow.Visible = _pinRow.GetChildCount() > 0;
    }

    private void ToggleFollow()
    {
        if (_map.Follow != null && (_map.Follow == _map.Selection || _map.Selection == null))
        {
            _map.Follow = null;
            _dirty = true;
            return;
        }
        if (_map.Selection is not { } sel || _map.Where(sel) == null)
        {
            Toast("Select someone to follow");
            return;
        }
        if (_map.Strategic) ToggleStrategic();
        _map.Follow = sel;
        _dirty = true;
        GuideEvent("follow");
    }

    /// <summary>Remember where the player was before a window or card opens, for Back.</summary>
    private void Remember()
    {
        if (_goingBack) return;
        var here = (_window.Visible ? _windowTab : null, _inspector.Visible ? _map.Selection : null);
        if (here.Item1 == null && here.Item2 == null) return;
        if (_back.Count > 0 && _back[^1] == here) return;
        _back.Add(here);
        if (_back.Count > BackMost) _back.RemoveAt(0);
    }

    private void GoBack()
    {
        if (_back.Count == 0) return;
        var (window, card) = _back[^1];
        _back.RemoveAt(_back.Count - 1);
        _goingBack = true;
        try
        {
            if (window is Tab w) OpenWindow(w);
            else CloseWindow();
            if (card != null && _map.Where(card) is Vector2 at) _map.FlyTo(at);
            Select(card);
        }
        finally
        {
            _goingBack = false;
        }
    }

    /// <summary>Follow a link to someone or something: open their card and take the camera there.</summary>
    private void FlyToCard(Selection sel)
    {
        Remember();
        Select(sel);
        if (_map.Where(sel) is Vector2 at)
        {
            if (_map.Strategic) ToggleStrategic();
            _map.FlyTo(at);
        }
    }

    private static void ToggleFullscreen()
    {
        bool full = DisplayServer.WindowGetMode() is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
        DisplayServer.WindowSetMode(full ? DisplayServer.WindowMode.Windowed : DisplayServer.WindowMode.Fullscreen);
    }

    /// <summary>The speed the game ran at before it lost focus, to take up again when the player comes back.</summary>
    private int? _speedBeforeAway;

    private void FocusChanged(bool focused)
    {
        if (_opts.Screenshot != null || _opts.Smoke || _opts.Bench || !_pauseAway || _title.Visible) return;
        if (!focused && _speed > 0)
        {
            _speedBeforeAway = _speed;
            SetSpeed(0);
        }
        else if (focused && _speedBeforeAway is int was)
        {
            _speedBeforeAway = null;
            if (_speed == 0 && !_menu.Visible) SetSpeed(was);
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Xbutton1 })
        {
            GoBack();
            GetViewport().SetInputAsHandled();
        }
    }

    private sealed record SavedPin(int Slot, string? Kind, int Id, string? SiteId, float X, float Y, float Dist, string Label);

    private List<SavedPin> SavedPins() =>
        _pins.Select((p, i) => p == null ? null : new SavedPin(i, p.Thing?.Kind, p.Thing?.Id ?? 0, p.Thing?.SiteId, p.Spot.X, p.Spot.Y, p.Dist, p.Label))
            .OfType<SavedPin>().ToList();

    private void RestorePins(IEnumerable<SavedPin>? pins)
    {
        Array.Clear(_pins);
        foreach (var p in pins ?? [])
            if (p.Slot is >= 0 and < 10)
                _pins[p.Slot] = new CameraPin(p.Kind == null ? null : new Selection(p.Kind, p.Id, p.SiteId ?? ""), new Vector2(p.X, p.Y), p.Dist, p.Label);
        RenderPins();
    }
}
