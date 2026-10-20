using CrownAndCompass.Client.Map;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The info lens's controls: L (or the Lens button by the corner map) greys the town and colours one thing about it,
/// and a bar at the top picks which (Room, Work, Growth, Appeal, Danger) with a line on what the colours mean.
/// </summary>
public partial class Main
{
    private Button _lensButton = null!;
    private PanelContainer _lensBar = null!;
    private Label _lensLine = null!;
    private readonly Dictionary<Lens.Mode, Button> _lensModes = [];

    private Button BuildLensButton()
    {
        _lensButton = new Button { Text = "Lens", ToggleMode = true, TooltipText = "Grey the town and show its room, work, growth, appeal or danger (L)", FocusMode = FocusModeEnum.None };
        _lensButton.Pressed += ToggleLens;
        return _lensButton;
    }

    private void BuildLensBar()
    {
        _lensBar = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _lensBar.AddThemeStyleboxOverride("panel", Castle.Oak(12, 8));
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 4);
        foreach (var m in Lens.Modes)
        {
            var b = new Button { Text = Lens.Name(m), ToggleMode = true, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(84, 0) };
            b.Pressed += () => _map.Lens.Show(true, m);
            _lensModes[m] = b;
            row.AddChild(b);
        }
        var close = new Button { Text = "×", TooltipText = "Close the lens (L or Esc)", FocusMode = FocusModeEnum.None };
        close.Pressed += () => _map.Lens.Show(false);
        row.AddChild(close);
        v.AddChild(row);
        _lensLine = Ui.Label("", Ui.Ink, Ui.Medium, 13);
        _lensLine.HorizontalAlignment = HorizontalAlignment.Center;
        _lensLine.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _lensLine.CustomMinimumSize = new Vector2(560, 0);
        v.AddChild(_lensLine);
        _lensBar.AddChild(v);
        AddChild(_lensBar);
        _lensBar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterTop, LayoutPresetMode.Minsize);
        _lensBar.GrowHorizontal = GrowDirection.Both;
        _lensBar.OffsetTop = 64;
        _map.Lens.Changed += RenderLens;
    }

    private void ToggleLens()
    {
        _map.Lens.Show(!_map.Lens.On);
        if (_map.Lens.On) GuideEvent("lens");
    }

    private void RenderLens()
    {
        var lens = _map.Lens;
        _lensBar.Visible = lens.On;
        _lensButton.SetPressedNoSignal(lens.On);
        foreach (var (m, b) in _lensModes) b.SetPressedNoSignal(lens.On && lens.Showing == m);
        _lensLine.Text = lens.Summary;
    }

    /// <summary>The summary follows the town as it changes, a few times a second at most.</summary>
    private double _lensLineAt;

    private void TickLens(double delta)
    {
        if (!_map.Lens.On) return;
        _lensLineAt -= delta;
        if (_lensLineAt > 0) return;
        _lensLineAt = 0.4;
        _lensLine.Text = _map.Lens.Summary;
    }
}
