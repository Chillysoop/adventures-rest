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
        Decor,
    }

    /// <summary>One button on the bar: a zone to paint, a building to place, or a defense to order.</summary>
    private sealed record BuildItem(string Label, Zone? Zone = null, string? Building = null, DefenseWork? Defense = null, int Density = 0, string? Crop = null);

    private PanelContainer _buildBar = null!;
    private HFlowContainer _buildItems = null!;
    private Label _buildHint = null!;
    private Button _buildToggle = null!;
    private BuildGroup _buildGroup = BuildGroup.Zones;
    private readonly Dictionary<BuildGroup, Button> _groupButtons = [];
    private readonly List<(BuildItem Item, Button Button)> _buildButtons = [];

    private static readonly BuildItem[] Zones =
    [
        // Homes and shops come in three densities, unlocked by the charters (Density in the sim).
        new("Residential", Zone.Residential), new("Res · Medium", Zone.Residential, Density: 1), new("Res · High", Zone.Residential, Density: 2),
        new("Commercial", Zone.Commercial), new("Comm · Medium", Zone.Commercial, Density: 1), new("Comm · High", Zone.Commercial, Density: 2),
        new("Industry", Zone.Industry),
    ];

    /// <summary>The zone brushes: homes, shops and workshops, a field for each crop (content/game/farming.json), and the eraser.</summary>
    private static BuildItem[] ZoneItems(Content content) =>
        [.. Zones, .. content.Farming.Crops.Select(c => new BuildItem(c.Name, Zone.Farmland, Crop: c.Id)), new("Clear zone", Zone.None)];

    private static readonly BuildItem[] Defenses =
    [
        new("Palisade", Defense: DefenseWork.Palisade), new("Watchtower", Defense: DefenseWork.Tower),
        new("Stone Walls", Defense: DefenseWork.StoneWall), new("Gatehouse", Defense: DefenseWork.Gatehouse),
        new("Moat", Defense: DefenseWork.Moat), new("Wall Barracks", Defense: DefenseWork.Barracks), new("City Wall", Defense: DefenseWork.CityWall),
    ];

    /// <summary>The bar's buttons, from the content: everything the town could ever place, in the order the charters open it.</summary>
    private BuildItem[] BuildItems(BuildGroup g)
    {
        // Before the first town is adopted there is nothing to build yet.
        if (_sim == null) return g == BuildGroup.Zones ? ZoneItems(Content.Default()) : [];
        var town = _sim.Town;
        int Order(BuildingDef d) => d.Tier == "" ? 0 : City.TierIndex(town, d.Tier);
        IEnumerable<BuildingDef> Sorted(Func<BuildingDef, bool> keep) =>
            town.Content.Buildings.Where(keep).OrderBy(Order).ThenBy(d => d.Cost).ThenBy(d => d.Id, StringComparer.Ordinal);
        return g switch
        {
            BuildGroup.Zones => ZoneItems(town.Content),
            BuildGroup.Government =>
            [
                new("Charter House", Building: "house"),
                .. Sorted(d => Planning.IsGovernment(town, d) && d.Id != "town_hall").Select(d => new BuildItem(d.Name, Building: d.Id)),
                .. Defenses,
            ],
            BuildGroup.Decor => [],
            _ => [.. Sorted(d => d.Buildable && d.IsBusiness && !Planning.IsGovernment(town, d)).Select(d => new BuildItem(d.Name, Building: d.Id))],
        };
    }

    private static readonly Dictionary<BuildGroup, string> GroupHints = new()
    {
        [BuildGroup.Zones] = "Paint zones on open land with the brush. Townsfolk and newcomers build what the town's economy needs in them, facing the lanes. Denser homes and shops unlock with the charters: paint them over built streets too, and the houses there grow taller. Fields are sown one crop at a time: wheat, root crops, barley and hops, orchards, pasture, herbs or vines.",
        [BuildGroup.Government] = "Government works, paid from the treasury and built by the town's builders.",
        [BuildGroup.Commission] = "Commission a business the town owns and runs. Its profits go to the treasury.",
        [BuildGroup.Decor] = "Set dressing, anywhere and at any angle. Every piece makes its corner pleasanter and does one small job for the town.",
    };

    private void BuildBuildBar()
    {
        _buildBar = new PanelContainer { Visible = false };
        _buildBar.AddThemeStyleboxOverride("panel", Castle.Oak(16, 14));
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

        // Rows that wrap: the lists grow with every charter.
        _buildItems = new HFlowContainer { CustomMinimumSize = new Vector2(1110, 0) };
        _buildItems.AddThemeConstantOverride("h_separation", 6);
        _buildItems.AddThemeConstantOverride("v_separation", 6);
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
        _map.Planner.Said += Toast;
        _map.Decorator.Changed += () =>
        {
            _dirty = true;
            SyncDecorButtons();
        };
        ShowBuildGroup(BuildGroup.Zones);
    }

    private void ToggleBuildBar()
    {
        _buildBar.Visible = !_buildBar.Visible;
        if (!_buildBar.Visible)
        {
            _map.Planner.Pick(null);
            _map.Decorator.Pick(null);
        }
        _buildToggle.SetPressedNoSignal(_buildBar.Visible);
        _dirty = true;
        if (_buildBar.Visible) GuideEvent("build");
    }

    private void ShowBuildGroup(BuildGroup g)
    {
        _buildGroup = g;
        foreach (var (k, b) in _groupButtons) b.SetPressedNoSignal(k == g);
        Ui.Clear(_buildItems);
        _buildButtons.Clear();
        _map.Planner.Pick(null);
        _map.Decorator.Pick(null);
        if (g == BuildGroup.Decor)
        {
            ShowDecor();
            return;
        }
        foreach (var item in BuildItems(g))
        {
            var b = new Button { ToggleMode = item.Defense == null, FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(112, 58), ClipText = true };
            b.AddThemeFontSizeOverride("font_size", 12);
            if (item.Zone is Zone z)
            {
                var c = item.Crop != null ? Planner.CropColor(Content.Default().Farming.Crops.FirstOrDefault(x => x.Id == item.Crop)) : Planner.ZoneColor(z, item.Density);
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
        _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(item.Zone, item.Building, item.Density, item.Crop) : null);
    }

    private void SyncBuildButtons()
    {
        var tool = _map.Planner.Tool;
        foreach (var (item, b) in _buildButtons)
            if (b.ToggleMode) b.SetPressedNoSignal(tool != null && tool.Zone == item.Zone && tool.Building == item.Building && tool.Density == item.Density && tool.Crop == item.Crop);
    }

    /// <summary>Costs, open land and what blocks each item, refreshed as the town changes.</summary>
    private void RenderBuild()
    {
        if (!_buildBar.Visible) return;
        if (_buildGroup == BuildGroup.Decor)
        {
            RenderDecor();
            return;
        }
        var town = _sim.Town;
        foreach (var (item, b) in _buildButtons)
        {
            string line;
            string? why = null;
            string tip;
            if (item.Zone is Zone z)
            {
                int open = z == Zone.None ? 0 : Planning.VacantZoned(town, z);
                line = z == Zone.None ? "eraser" : open >= 30 ? "plenty of room" : open == 0 ? "paint more" : $"room for {open}";
                tip = z == Zone.None
                    ? "Brush over painted land nobody has built on to take it off the plan."
                    : $"Paint {Planning.ZoneName(z)} land with a round brush; the townsfolk build their own {Planning.ZoneName(z)} buildings inside it, each turned to the square. [ and ] or Shift+wheel size the brush.";
                if (item.Crop != null && Farming.Crop(town, item.Crop) is { } crop)
                {
                    var farm = town.Content.Building(crop.Building);
                    why = Farming.Blocked(town, crop);
                    line = why?.Replace("needs the town to be ", "needs ") ?? $"farms {DemandWord(Property.Demand(town, farm.Id))}";
                    tip = $"Sow fields with {crop.Name.ToLowerInvariant()}: only {farm.Name.ToLowerInvariant()}s go up on them. {crop.Blurb} Painting over open fields of another crop resows them.";
                }
                else if (Density.Takes(z))
                {
                    var dd = Density.Def(town, item.Density);
                    why = Density.Blocked(town, item.Density);
                    if (why != null) line = why.Replace($"{dd.Name.ToLowerInvariant()} density ", "");
                    tip = $"{Ui.Cap(dd.Name.ToLowerInvariant())}-density {Planning.ZoneName(z)}: {dd.Blurb} Painting over built {Planning.ZoneName(z)} land changes its density too.";
                }
            }
            else if (item.Defense is DefenseWork d)
            {
                why = Threat.BuildBlocked(town, d);
                line = why ?? $"{Threat.BuildCost(town, d)}g";
                tip = d switch
                {
                    DefenseWork.Palisade => "A wall of stakes around the town. Raiders and the warband break on it.",
                    DefenseWork.Tower => "A tower on the walls. Each one adds to the town's strength at the walls, and two of the watch live in it. Stone walls carry more of them.",
                    DefenseWork.Gatehouse => "A stone gatehouse with a portcullis over the south road. The gate holds far better when a siege tries it.",
                    DefenseWork.Moat => "A moat round the stone walls, filled from the river. Every wave wades it, and the walls count for more.",
                    DefenseWork.Barracks => "Barracks in the thickness of the stone walls. The watch lives there: cheaper to keep, and quicker to the walls when the horn sounds.",
                    DefenseWork.StoneWall => "Stone walls around the old town and the inner wards, far stronger than stakes. A large town needs them for its charter. The outer wards stay outside.",
                    _ => "The great wall of a city, around every inner ward. A great city needs it for its charter.",
                };
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
            { Zone: Zone.None } => "Brush over painted land to clear it. [ ] or Shift+wheel sizes the brush. Right-click puts the tool down.",
            { Zone: Zone.Farmland, Crop: string c } => $"Sow fields with {(Farming.Crop(town, c)?.Name ?? c).ToLowerInvariant()}, out beyond the houses where the farmers live; painting over open fields of another crop resows them (other zones stay as they are: clear them first to repaint). Right-click puts the tool down.",
            { Zone: Zone z, Density: > 0 } t => $"Paint {Density.Name(town, t.Density).ToLowerInvariant()}-density {Planning.ZoneName(z)} land, on open ground or over built {Planning.ZoneName(z)} streets to let them grow taller (other zones stay as they are: clear them first to repaint). Faint outlines show where buildings will fit. [ ] or Shift+wheel sizes the brush. Right-click puts the tool down.",
            { Zone: Zone z } => $"Paint {Planning.ZoneName(z)} land on open ground (other zones stay as they are: clear them first to repaint). Faint outlines show where buildings will fit. [ ] or Shift+wheel sizes the brush. Right-click puts the tool down.",
            { Building: string id } => $"Click to place the {town.Content.Building(id).Name.ToLowerInvariant()} facing the nearest lane, or drag to turn it any way (Shift+wheel turns it too). Green ground takes it; a click near it settles on the nearest spot that fits. Right-click puts the tool down.",
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

    /// <summary>For screenshots: open the bar on a group, pick a tool, and preview a stroke (or a placement dragged to face a point) from tile (x, y) to (x2, y2) of the town.</summary>
    private void OpenPlan(string group, string? tool, int[]? hover)
    {
        if (!_buildBar.Visible) ToggleBuildBar();
        if (Enum.TryParse<BuildGroup>(group, true, out var g)) ShowBuildGroup(g);
        if (tool == null) return;
        // A zone may name its density after a colon (residential:2), and farmland its crop (farmland:orchard).
        var parts = tool.Split(':');
        int dens = parts.Length > 1 && int.TryParse(parts[1], out int dv) ? dv : 0;
        string? crop = parts.Length > 1 && !int.TryParse(parts[1], out _) ? parts[1] : null;
        var item = BuildItems(_buildGroup).FirstOrDefault(i => i.Building == tool
            || ((i.Zone?.ToString().Equals(parts[0], StringComparison.OrdinalIgnoreCase) ?? false) && i.Density == dens && (i.Crop == crop || (crop == null && i.Crop == "wheat"))));
        if (item == null) return;
        _map.Planner.Pick(new Planner.PlanTool(item.Zone, item.Building, item.Density, item.Crop));
        if (hover is { Length: >= 2 })
        {
            Vector2 At(int x, int y) => Planner.FromTown(x, y);
            if (hover.Length >= 4) _map.Planner.Press(At(hover[0], hover[1]));
            _map.Planner.Hover(At(hover[^2], hover[^1]));
        }
        RenderBuild();
    }
}
