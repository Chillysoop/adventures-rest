using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The info lens's controls: L (or the Lens button by the corner map) greys the town and colours one thing about it,
/// and a bar at the top picks which (Room, Work, Growth, Appeal, Danger) with a line on what the colours mean. Under
/// it sit the tools for what the lens shows (the homes brush on Room, decor on Appeal, the watch on Danger), so the
/// lens is a way of working, not only a picture.
/// </summary>
public partial class Main
{
    private Button _lensButton = null!;
    private PanelContainer _lensBar = null!;
    private Label _lensLine = null!;
    private HBoxContainer _lensTools = null!;
    private Lens.Mode? _lensToolsFor;
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
        _lensTools = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        _lensTools.AddThemeConstantOverride("separation", 6);
        v.AddChild(_lensTools);
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
        if (_lensToolsFor != lens.Showing || !lens.On) RenderLensTools();
    }

    /// <summary>The tools for what the lens shows: a press picks the brush or order straight from the lens.</summary>
    private void RenderLensTools()
    {
        var lens = _map.Lens;
        _lensToolsFor = lens.On ? lens.Showing : null;
        Ui.Clear(_lensTools);
        if (!lens.On || _sim == null) return;
        void Tool(string icon, string label, string tip, Action press)
        {
            var b = new Button { Text = label, Icon = Ui.Icon(icon), ExpandIcon = false, FocusMode = FocusModeEnum.None, TooltipText = tip };
            b.AddThemeConstantOverride("icon_max_width", 16);
            b.Pressed += press;
            _lensTools.AddChild(b);
        }
        switch (lens.Showing)
        {
            case Lens.Mode.Room:
                Tool("population", "Homes", "Paint homes where the lens shows room", () => LensBrush(Zone.Residential));
                break;
            case Lens.Mode.Work:
                Tool("gold", "Shops", "Paint shops", () => LensBrush(Zone.Commercial));
                Tool("tools", "Workshops", "Paint workshops", () => LensBrush(Zone.Industry));
                break;
            case Lens.Mode.Growth:
                Tool("population", "Homes", "Paint homes", () => LensBrush(Zone.Residential));
                Tool("gold", "Shops", "Paint shops", () => LensBrush(Zone.Commercial));
                Tool("bread", "Fields", "Sow fields", () => LensBrush(Zone.Farmland));
                break;
            case Lens.Mode.Appeal:
                Tool("ui-heart", "Decor", "Gardens, fountains and statues: each lifts its corner", LensDecor);
                break;
            case Lens.Mode.Danger:
                Tool("order-guard", "Guard", "Post the watch over a spot", QuickGuard);
                Tool("defence", "Watch house", "Place a watch house", () => LensPlace("watch_house"));
                break;
        }
    }

    /// <summary>The build bar opened on whichever group holds the plain brush for a zone, with that brush in hand.</summary>
    private void LensBrush(Zone zone)
    {
        foreach (var g in Enum.GetValues<BuildGroup>())
            if (BuildItems(g).FirstOrDefault(i => i.Zone == zone && i.Density == 0 && (zone != Zone.Farmland || i.Crop is null or "wheat")) is { } item)
            {
                if (!_buildBar.Visible) ToggleBuildBar();
                ShowBuildGroup(g);
                _map.Planner.Pick(new Planner.PlanTool(item.Zone, item.Building, item.Density, item.Crop));
                RenderBuild();
                return;
            }
    }

    private void LensPlace(string building)
    {
        foreach (var g in Enum.GetValues<BuildGroup>())
            if (BuildItems(g).FirstOrDefault(i => i.Building == building) is { } item)
            {
                if (!_buildBar.Visible) ToggleBuildBar();
                ShowBuildGroup(g);
                _map.Planner.Pick(new Planner.PlanTool(null, item.Building));
                RenderBuild();
                return;
            }
        Hint("No watch house to place yet");
    }

    private void LensDecor()
    {
        if (!_buildBar.Visible) ToggleBuildBar();
        ShowBuildGroup(BuildGroup.Decor);
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
