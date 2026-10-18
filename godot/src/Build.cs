using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The build bar, after Foundation: paint zones and the townsfolk and newcomers buy that land and
/// build what the town's economy calls for; place government buildings and commission town-run
/// businesses directly, paid from the treasury. It opens above the dock (B).
/// </summary>
public partial class Main
{
    private enum BuildGroup
    {
        Zones,
        Government,
        Commission,
    }

    /// <summary>One button on the bar: a zone to paint, a building to place, or a defense to order.</summary>
    private sealed record BuildItem(string Label, Zone? Zone = null, string? Building = null, DefenseWork? Defense = null);

    private PanelContainer _buildBar = null!;
    private HBoxContainer _buildItems = null!;
    private Label _buildHint = null!;
    private Button _buildToggle = null!;
    private BuildGroup _buildGroup = BuildGroup.Zones;
    private readonly Dictionary<BuildGroup, Button> _groupButtons = [];
    private readonly List<(BuildItem Item, Button Button)> _buildButtons = [];

    private static readonly Dictionary<BuildGroup, BuildItem[]> BuildItems = new()
    {
        [BuildGroup.Zones] =
        [
            new("Residential", Zone.Residential), new("Commercial", Zone.Commercial), new("Industry", Zone.Industry),
            new("Farmland", Zone.Farmland), new("Clear zone", Zone.None),
        ],
        [BuildGroup.Government] =
        [
            new("Charter House", Building: "house"), new("Granary", Building: "granary"), new("Watch House", Building: "watch_house"),
            new("Temple", Building: "temple"), new("Scrying Pool", Building: "scrying_pool"), new("Sleepless Spire", Building: "sleepless_spire"),
            new("Palisade", Defense: DefenseWork.Palisade), new("Watchtower", Defense: DefenseWork.Tower),
        ],
        [BuildGroup.Commission] =
        [
            new("Farm", Building: "farm"), new("Lumber Camp", Building: "lumber_camp"), new("Forge", Building: "forge"),
            new("Brewery", Building: "brewery"), new("Tavern", Building: "tavern"), new("General Store", Building: "general_store"),
        ],
    };

    private static readonly Dictionary<BuildGroup, string> GroupHints = new()
    {
        [BuildGroup.Zones] = "Zone open land. Townsfolk and newcomers buy zoned lots and build what the town's economy needs.",
        [BuildGroup.Government] = "Government works, paid from the treasury and built by the town's builders.",
        [BuildGroup.Commission] = "Commission a business the town owns and runs. Its profits go to the treasury.",
    };

    private void BuildBuildBar()
    {
        _buildBar = new PanelContainer { Visible = false };
        _buildBar.AddThemeStyleboxOverride("panel", Glass(14, 12, 10));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        _buildBar.AddChild(col);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 4);
        foreach (var g in Enum.GetValues<BuildGroup>())
        {
            var b = new Button { Text = g.ToString(), ThemeTypeVariation = "Tab", ToggleMode = true, FocusMode = FocusModeEnum.None };
            b.Pressed += () => ShowBuildGroup(g);
            head.AddChild(b);
            _groupButtons[g] = b;
        }
        head.AddChild(new VSeparator());
        _buildHint = Ui.Label("", Ui.Ink2, null, 12);
        _buildHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _buildHint.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _buildHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _buildHint.CustomMinimumSize = new Vector2(420, 0);
        head.AddChild(_buildHint);
        col.AddChild(head);

        _buildItems = new HBoxContainer();
        _buildItems.AddThemeConstantOverride("separation", 6);
        col.AddChild(_buildItems);

        AddChild(_buildBar);
        _buildBar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap);
        _buildBar.GrowHorizontal = GrowDirection.Both;
        _buildBar.GrowVertical = GrowDirection.Begin;
        _buildBar.OffsetBottom = -(DockHeight + Gap * 2 + 20);
        _buildBar.OffsetTop = _buildBar.OffsetBottom;

        _map.Planner.Changed += () =>
        {
            _dirty = true;
            SyncBuildButtons();
        };
        ShowBuildGroup(BuildGroup.Zones);
    }

    private void ToggleBuildBar()
    {
        _buildBar.Visible = !_buildBar.Visible;
        if (!_buildBar.Visible) _map.Planner.Pick(null);
        _buildToggle.SetPressedNoSignal(_buildBar.Visible);
        _dirty = true;
        if (_buildBar.Visible) GuideEvent("build");
    }

    private void ShowBuildGroup(BuildGroup g)
    {
        _buildGroup = g;
        foreach (var (k, b) in _groupButtons) b.SetPressedNoSignal(k == g);
        foreach (var c in _buildItems.GetChildren()) c.Free();
        _buildButtons.Clear();
        _map.Planner.Pick(null);
        foreach (var item in BuildItems[g])
        {
            var b = new Button { ToggleMode = item.Defense == null, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(112, 58), ClipText = true };
            b.AddThemeFontSizeOverride("font_size", 12);
            if (item.Zone is Zone z)
            {
                var c = Planner.ZoneColor(z);
                b.AddThemeStyleboxOverride("normal", Ui.Box(c.Darkened(0.62f), c.Darkened(0.2f), 8, 1, 8, 6));
                b.AddThemeStyleboxOverride("hover", Ui.Box(c.Darkened(0.5f), c, 8, 1, 8, 6));
                b.AddThemeStyleboxOverride("pressed", Ui.Box(c.Darkened(0.3f), Ui.Ink, 8, 2, 8, 6));
            }
            b.Pressed += () => PressBuild(item, b);
            _buildItems.AddChild(b);
            _buildButtons.Add((item, b));
        }
        RenderBuild();
    }

    private void PressBuild(BuildItem item, Button b)
    {
        if (item.Defense is DefenseWork d)
        {
            _sim.BuildDefense(d);
            _dirty = true;
            return;
        }
        _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(item.Zone, item.Building) : null);
    }

    private void SyncBuildButtons()
    {
        var tool = _map.Planner.Tool;
        foreach (var (item, b) in _buildButtons)
            if (b.ToggleMode) b.SetPressedNoSignal(tool != null && tool.Zone == item.Zone && tool.Building == item.Building);
    }

    /// <summary>Costs, open land and what blocks each item, refreshed as the town changes.</summary>
    private void RenderBuild()
    {
        if (!_buildBar.Visible) return;
        var town = _sim.Town;
        foreach (var (item, b) in _buildButtons)
        {
            string line;
            string? why = null;
            string tip;
            if (item.Zone is Zone z)
            {
                line = z == Zone.None ? "free" : $"{Planning.VacantZoned(town, z)} open";
                tip = z == Zone.None ? "Drag over zoned land nobody has bought to take it off the plan." : $"Drag over open land to zone it {Planning.ZoneName(z)}.";
            }
            else if (item.Defense is DefenseWork d)
            {
                why = DefenseBlocked(town, d);
                line = why ?? $"{Threat.BuildCost(town, d)}g";
                tip = d == DefenseWork.Palisade ? "A wall of stakes around the town. Raiders and the warband break on it." : "A tower on the palisade. Each one adds to the town's strength at the walls.";
            }
            else
            {
                var def = town.Content.Building(item.Building!);
                why = Planning.Blocked(town, def);
                line = why ?? $"{Planning.Cost(town, def)}g";
                tip = def.Blurb != "" ? def.Blurb : BuildTip(town, def);
                if (why == null && def.IsBusiness) line += $" · {DemandWord(Property.Demand(town, def.Id))}";
            }
            b.Text = $"{item.Label}\n{line.Replace("not enough gold", "short of gold")}";
            Ui.GoldButton(b);
            b.Disabled = why != null && !b.ButtonPressed;
            b.TooltipText = why == null ? tip : $"{tip}\n({Ui.Cap(why)}.)";
        }
        SyncBuildButtons();
        _buildHint.Text = _map.Planner.Tool switch
        {
            { Zone: Zone.None } => "Drag over open land to clear its zone. Right-click to put the tool down.",
            { Zone: Zone z } => $"Drag over open land to zone it {Planning.ZoneName(z)}. Right-click to put the tool down.",
            { Building: string id } => $"Click an open lot for the {town.Content.Building(id).Name.ToLowerInvariant()}. Right-click to put the tool down.",
            _ => GroupHints[_buildGroup],
        };
    }

    private static string BuildTip(Town town, BuildingDef def) => def.Id switch
    {
        "house" => "A house the town owns and lets out, for families with nowhere to live.",
        "temple" => "A temple to the town's god. Blessings, miracles and heroes' vows.",
        "scrying_pool" => "Once a day it shows one site under fog, with no scryer to pay.",
        "sleepless_spire" => "From the top the near country is seen live, day and night.",
        _ => $"A {def.Name.ToLowerInvariant()} the town owns and runs. Townsfolk build these themselves on {Planning.ZoneName(def.Zone)} land when the town needs one.",
    };

    private static string DemandWord(double d) => d >= 1.05 ? "wanted" : d >= 0.8 ? "steady" : "enough";

    private static string? DefenseBlocked(Town town, DefenseWork d)
    {
        var def = town.Defenses;
        if (d == DefenseWork.Palisade && def.Palisade != PalisadeState.None) return def.Palisade == PalisadeState.Built ? "built" : "under way";
        if (d == DefenseWork.Tower)
        {
            if (def.Palisade != PalisadeState.Built) return "needs the palisade";
            if (def.Towers + def.TowersBuilding.Count >= town.Content.Enemy.Tower.Max) return "all built";
        }
        if (town.Treasury < Threat.BuildCost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>For screenshots: open the bar on a group, pick a tool, and preview a stroke from lot (x, y) to (x2, y2).</summary>
    private void OpenPlan(string group, string? tool, int[]? hover)
    {
        if (!_buildBar.Visible) ToggleBuildBar();
        if (Enum.TryParse<BuildGroup>(group, true, out var g)) ShowBuildGroup(g);
        if (tool == null) return;
        var item = BuildItems[_buildGroup].FirstOrDefault(i => i.Building == tool || (i.Zone?.ToString().Equals(tool, StringComparison.OrdinalIgnoreCase) ?? false));
        if (item == null) return;
        _map.Planner.Pick(new Planner.PlanTool(item.Zone, item.Building));
        if (hover is { Length: >= 2 })
        {
            Vector2 At(int x, int y) => new(Map.Geo.Margin + x + 0.5f, Map.Geo.Margin + y + 0.5f);
            if (hover.Length >= 4) _map.Planner.Press(At(hover[0], hover[1]));
            _map.Planner.Hover(At(hover[^2], hover[^1]));
        }
        RenderBuild();
    }
}
