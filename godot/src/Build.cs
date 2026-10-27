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
    /// <summary>The build bar's buckets: what a thing is for, whether it is painted, commissioned or placed.</summary>
    private enum BuildGroup
    {
        Homes,
        Farming,
        Industry,
        Trade,
        Leisure,
        Faith,
        Civic,
        Defence,
        Wonders,
        Decor,
    }

    /// <summary>Each bucket's icon, colour and one-line hint (the rest of what it does sits on each button's hover).</summary>
    private static readonly Dictionary<BuildGroup, (string Icon, Color Hue, string Hint)> GroupLook = new()
    {
        [BuildGroup.Homes] = ("ui-house", new Color("8cc084"), "Paint homes · settlers build them"),
        [BuildGroup.Farming] = ("food", new Color("d9c46a"), "Sow fields one crop at a time"),
        [BuildGroup.Industry] = ("tools", new Color("c9925e"), "Paint workshops · or commission one"),
        [BuildGroup.Trade] = ("gold", new Color("e8b93a"), "Paint shops · or commission one"),
        [BuildGroup.Leisure] = ("ale", new Color("e08aa8"), "Taverns, baths, stages: mood and heroes"),
        [BuildGroup.Faith] = ("faith", new Color("b996e6"), "Shrines and temples for the gods"),
        [BuildGroup.Civic] = ("tab-council", new Color("74aee6"), "The town's own halls and institutions"),
        [BuildGroup.Defence] = ("defence", new Color("ef7f6f"), "Walls, gates, towers and the watch"),
        [BuildGroup.Wonders] = ("ui-star", new Color("f0d070"), "Grand works that take seasons"),
        [BuildGroup.Decor] = ("face-oak-leaf", new Color("5ec2b0"), "Dress the streets · each piece helps a little"),
    };

    /// <summary>Civic buildings whose bucket isn't Civic.</summary>
    private static readonly Dictionary<string, BuildGroup> CivicBuckets = new()
    {
        ["granary"] = BuildGroup.Farming,
        ["builders_yard"] = BuildGroup.Industry, ["artificers_foundry"] = BuildGroup.Industry, ["alchemists_quarter"] = BuildGroup.Industry,
        ["market_hall"] = BuildGroup.Trade, ["guildhall"] = BuildGroup.Trade, ["auction_house"] = BuildGroup.Trade, ["grand_bazaar"] = BuildGroup.Trade,
        ["fey_market"] = BuildGroup.Trade, ["harbour"] = BuildGroup.Trade, ["sky_dock"] = BuildGroup.Trade, ["embassy_quarter"] = BuildGroup.Trade,
        ["arena"] = BuildGroup.Leisure, ["library"] = BuildGroup.Leisure,
        ["shrine"] = BuildGroup.Faith, ["temple"] = BuildGroup.Faith, ["cathedral"] = BuildGroup.Faith, ["monastery"] = BuildGroup.Faith,
        ["druid_grove"] = BuildGroup.Faith, ["chapterhouse"] = BuildGroup.Faith,
        ["watch_house"] = BuildGroup.Defence, ["drill_yard"] = BuildGroup.Defence, ["archery_range"] = BuildGroup.Defence, ["stables"] = BuildGroup.Defence,
        ["watchtower"] = BuildGroup.Defence, ["barracks"] = BuildGroup.Defence, ["keep"] = BuildGroup.Defence, ["griffon_roost"] = BuildGroup.Defence,
        ["dragon_eyrie"] = BuildGroup.Defence,
    };

    /// <summary>Townsfolk businesses that are leisure, not trade.</summary>
    private static readonly HashSet<string> Venues = ["tavern", "bathhouse", "theatre"];

    /// <summary>The bucket a building sits in on the bar.</summary>
    private static BuildGroup BucketOf(BuildingDef d)
    {
        if (d.IsGrand) return BuildGroup.Wonders;
        if (CivicBuckets.TryGetValue(d.Id, out var g)) return g;
        if (Venues.Contains(d.Id)) return BuildGroup.Leisure;
        return d.Zone switch
        {
            Zone.Residential => BuildGroup.Homes,
            Zone.Farmland => BuildGroup.Farming,
            Zone.Industry => BuildGroup.Industry,
            Zone.Commercial => BuildGroup.Trade,
            _ => BuildGroup.Civic,
        };
    }

    /// <summary>One button on the bar: a zone to paint, a building to place, or a defense to order.</summary>
    private sealed record BuildItem(string Label, Zone? Zone = null, string? Building = null, DefenseWork? Defense = null, int Density = 0, string? Crop = null, bool Gate = false);

    private PanelContainer _buildBar = null!;
    private HBoxContainer _buildTabs = null!;
    private HFlowContainer _buildItems = null!;
    private Label _buildHint = null!;
    private Button _buildToggle = null!;
    private BuildGroup _buildGroup = BuildGroup.Homes;
    private readonly Dictionary<BuildGroup, Button> _groupButtons = [];
    private readonly List<(BuildItem Item, Button Button)> _buildButtons = [];

    private static readonly BuildItem ClearZone = new("Clear zone", Zone.None);

    /// <summary>A bucket's zone brushes: homes and shops in three densities (unlocked by the charters), workshops, a field for each crop.</summary>
    private static BuildItem[] ZoneItems(Content content, BuildGroup g) => g switch
    {
        BuildGroup.Homes => [new("Homes", Zone.Residential), new("Homes · Medium", Zone.Residential, Density: 1), new("Homes · High", Zone.Residential, Density: 2), ClearZone],
        BuildGroup.Trade => [new("Shops", Zone.Commercial), new("Shops · Medium", Zone.Commercial, Density: 1), new("Shops · High", Zone.Commercial, Density: 2), ClearZone],
        BuildGroup.Industry => [new("Workshops", Zone.Industry), ClearZone],
        BuildGroup.Farming => [.. content.Farming.Crops.Select(c => new BuildItem(c.Name, Zone.Farmland, Crop: c.Id)), ClearZone],
        _ => [],
    };

    private static readonly BuildItem[] Defenses =
    [
        new("Palisade", Defense: DefenseWork.Palisade), new("Watchtower", Defense: DefenseWork.Tower),
        new("Stone Walls", Defense: DefenseWork.StoneWall), new("Gatehouse", Defense: DefenseWork.Gatehouse),
        new("Moat", Defense: DefenseWork.Moat), new("Wall Barracks", Defense: DefenseWork.Barracks), new("City Wall", Defense: DefenseWork.CityWall),
        new("Wall Gate", Gate: true),
    ];

    /// <summary>The wall a gate goes on now: the city wall once it is going up, else the old town's.</summary>
    private int GateRing() => WallLines.Begun(_sim.Town, 1) ? 1 : 0;

    /// <summary>A wall not yet begun is drawn first (then given a gate, which starts the work); other works are ordered at once.</summary>
    private bool DrawsFirst(DefenseWork d) => Threat.IsWall(d) && !WallLines.Begun(_sim.Town, WallLines.RingOf(d))
        && (d != DefenseWork.StoneWall || _sim.Town.Defenses.Palisade == PalisadeState.None);

    /// <summary>The bar's buttons, from the content: everything the town could ever place, in the order the charters open it.</summary>
    private BuildItem[] BuildItems(BuildGroup g)
    {
        // Before the first town is adopted there is nothing to build yet.
        if (_sim == null) return ZoneItems(Content.Default(), g);
        var town = _sim.Town;
        int Order(BuildingDef d) => d.Tier == "" ? 0 : City.TierIndex(town, d.Tier);
        IEnumerable<BuildingDef> Sorted(Func<BuildingDef, bool> keep) =>
            town.Content.Buildings.Where(d => keep(d) && BucketOf(d) == g).OrderBy(Order).ThenBy(d => d.Cost).ThenBy(d => d.Id, StringComparer.Ordinal);
        // Painted first, then what the town commissions and runs, then its own works.
        return
        [
            .. ZoneItems(town.Content, g),
            .. g == BuildGroup.Homes ? [new BuildItem("Charter House", Building: "house")] : Array.Empty<BuildItem>(),
            .. Sorted(d => d.Buildable && d.IsBusiness && !Planning.IsGovernment(town, d)).Select(d => new BuildItem(d.Name, Building: d.Id)),
            .. Sorted(d => Planning.IsGovernment(town, d) && d.Id != "town_hall").Select(d => new BuildItem(d.Name, Building: d.Id)),
            .. g == BuildGroup.Defence ? Defenses : [],
        ];
    }

    /// <summary>
    /// Whether the bar shows an item yet: zone brushes and walls always, a building once the town's charter is at most
    /// one short of it (it shows dimmed until then), so each bucket grows with the town.
    /// </summary>
    private bool Revealed(BuildItem item)
    {
        if (item.Building is not string id || _sim == null) return true;
        var d = _sim.Town.Content.Building(id);
        if (Institutions.Hidden(_sim.Town, d.Id)) return false;
        return d.Tier == "" || City.TierIndex(_sim.Town, d.Tier) <= _sim.Town.City.Tier + 1;
    }

    /// <summary>A bucket shows once it has anything to offer.</summary>
    private bool BucketShown(BuildGroup g) => g == BuildGroup.Decor || BuildItems(g).Any(Revealed);

    private void BuildBuildBar()
    {
        _buildBar = new PanelContainer { Visible = false };
        _buildBar.AddThemeStyleboxOverride("panel", Castle.Oak(16, 14));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 8);
        _buildBar.AddChild(col);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 4);
        _buildTabs = new HBoxContainer();
        _buildTabs.AddThemeConstantOverride("separation", 4);
        head.AddChild(_buildTabs);
        foreach (var g in Enum.GetValues<BuildGroup>())
        {
            var (icon, hue, hint) = GroupLook[g];
            var b = new Button { Text = g.ToString(), Icon = Ui.Icon(icon), ExpandIcon = false, ThemeTypeVariation = "Tab", ToggleMode = true, FocusMode = FocusModeEnum.None, TooltipText = hint };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeConstantOverride("h_separation", 5);
            b.AddThemeFontSizeOverride("font_size", 13);
            var on = Ui.Flat(new Color(hue, 0.2f), 4, 11, 5);
            on.BorderColor = hue;
            on.BorderWidthBottom = 3;
            b.AddThemeStyleboxOverride("pressed", on);
            b.AddThemeStyleboxOverride("hover_pressed", on);
            b.AddThemeColorOverride("font_pressed_color", hue.Lightened(0.25f));
            b.AddThemeColorOverride("font_hover_pressed_color", hue.Lightened(0.25f));
            b.Pressed += () => ShowBuildGroup(g);
            _buildTabs.AddChild(b);
            _groupButtons[g] = b;
        }
        _buildTabs.AddChild(new VSeparator());
        _buildHint = Ui.Label("", Ui.Ink2, null, 12);
        _buildHint.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        _buildHint.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        _buildHint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _buildHint.CustomMinimumSize = new Vector2(300, 0);
        col.AddChild(head);
        // The hint gets its own line under the buckets, so the buckets keep the whole width.
        col.AddChild(_buildHint);

        // Rows that wrap: the lists grow with every charter.
        _buildItems = new HFlowContainer { CustomMinimumSize = new Vector2(1110, 0) };
        _buildItems.AddThemeConstantOverride("h_separation", 6);
        _buildItems.AddThemeConstantOverride("v_separation", 6);
        col.AddChild(_buildItems);

        AddChild(_buildBar);
        _buildBar.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap);
        _buildBar.GrowHorizontal = GrowDirection.Both;
        _buildBar.GrowVertical = GrowDirection.Begin;
        FitBuildBar();

        _map.Planner.Changed += () =>
        {
            _dirty = true;
            SyncBuildButtons();
            TuckBuildBar();
        };
        _map.Planner.Said += Toast;
        _map.Decorator.Changed += () =>
        {
            _dirty = true;
            SyncDecorButtons();
            TuckBuildBar();
        };
        ShowBuildGroup(BuildGroup.Homes);
    }

    /// <summary>Ctrl+Z: take back the last stroke of the zoning brush.</summary>
    private void UndoPaint()
    {
        int n = _sim.UndoPaint();
        if (n == 0)
        {
            Toast("Nothing to undo");
            return;
        }
        _map.Planner.Touch();
        Toast("Last stroke undone");
        GuideEvent("undo");
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

    /// <summary>
    /// With a tool in hand the bar folds down to its hint line so the map is clear to place on, and opens again
    /// once the tool is put down (placed, right-click or Esc).
    /// </summary>
    private void TuckBuildBar()
    {
        bool tuck = _map.Planner.Active || _map.Decorator.Active;
        if (_buildItems.Visible != tuck) return;
        _buildTabs.Visible = !tuck;
        _buildItems.Visible = !tuck;
        Callable.From(FitBuildBar).CallDeferred();
    }

    /// <summary>Shrink the bar to what it holds, centred above the dock.</summary>
    private void FitBuildBar()
    {
        _buildBar.OffsetLeft = _buildBar.OffsetRight = 0;
        _buildBar.OffsetBottom = -(DockHeight + Gap * 2 + 20);
        _buildBar.OffsetTop = _buildBar.OffsetBottom;
        _buildBar.ResetSize();
    }

    /// <summary>A click anywhere off the open bar closes it; on the map it does nothing else.</summary>
    private void ClickOffBuildBar(InputEventMouseButton mb)
    {
        _map.Swallow = false;
        if (!_buildBar.Visible || !_buildItems.Visible || mb.ButtonIndex is not (MouseButton.Left or MouseButton.Right)) return;
        if (_buildBar.GetGlobalRect().HasPoint(mb.Position) || _buildToggle.GetGlobalRect().HasPoint(mb.Position)) return;
        ToggleBuildBar();
        _map.Swallow = true;
    }

    /// <summary>
    /// Where to click next for a build-bar item ("zone:Residential", "place:watch_house"): the Build button while the bar is
    /// shut, the item's group while another shows, then the item's own button. Null when the town can't build it yet.
    /// </summary>
    private Control? BuildTarget(string point)
    {
        bool zone = point.StartsWith("zone:", StringComparison.Ordinal);
        string id = zone ? point[5..] : point[6..];
        bool Is(BuildItem i) => zone ? i.Zone?.ToString() == id && i.Density == 0 : i.Building == id;
        if (!_buildBar.Visible) return _buildToggle;
        foreach (var (item, b) in _buildButtons)
            if (Is(item)) return b;
        foreach (var g in Enum.GetValues<BuildGroup>())
            if (g != _buildGroup && BuildItems(g).Any(i => Is(i) && Revealed(i))) return _groupButtons[g];
        return null;
    }

    private void ShowBuildGroup(BuildGroup g)
    {
        if (!BucketShown(g)) g = BuildGroup.Homes;
        _buildGroup = g;
        foreach (var (k, b) in _groupButtons)
        {
            b.SetPressedNoSignal(k == g);
            b.Visible = BucketShown(k);
        }
        Ui.Clear(_buildItems);
        _buildButtons.Clear();
        // The decor buttons went with the items: left listed, putting the decor tool down would touch freed buttons.
        _decorButtons.Clear();
        _map.Planner.Pick(null);
        _map.Decorator.Pick(null);
        if (g == BuildGroup.Decor)
        {
            ShowDecor();
            return;
        }
        BuildItem? last = null;
        foreach (var item in BuildItems(g).Where(Revealed))
        {
            // A thin rule between what is painted and what is placed.
            if (last != null && (last.Zone != null) != (item.Zone != null)) _buildItems.AddChild(new VSeparator());
            last = item;
            var b = new Button { ToggleMode = item.Defense == null || Threat.IsWall(item.Defense.Value), FocusMode = FocusModeEnum.None, CustomMinimumSize = new Vector2(112, 58) };
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
        if (item.Gate)
        {
            _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(null, null) { Gate = GateRing() } : null);
            return;
        }
        if (item.Defense is DefenseWork d)
        {
            if (d is DefenseWork.Tower or DefenseWork.Barracks)
            {
                _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(null, null) { OnWall = d } : null);
                return;
            }
            if (DrawsFirst(d))
            {
                _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(null, null) { Wall = WallLines.RingOf(d), Work = d } : null);
                return;
            }
            b.SetPressedNoSignal(false);
            if (!_sim.BuildDefense(d) && Threat.BuildBlocked(_sim.Town, d) is { } why && why.StartsWith("needs a gate"))
                _map.Planner.Pick(new Planner.PlanTool(null, null) { Gate = WallLines.RingOf(d), Work = d });
            _dirty = true;
            return;
        }
        _map.Planner.Pick(b.ButtonPressed ? new Planner.PlanTool(item.Zone, item.Building, item.Density, item.Crop) : null);
    }

    private void SyncBuildButtons()
    {
        var tool = _map.Planner.Tool;
        foreach (var (item, b) in _buildButtons)
            if (b.ToggleMode) b.SetPressedNoSignal(tool != null && (item.Gate ? tool.Gate != null && tool.Work == null
                : item.Defense is DefenseWork dw ? tool.Work == dw
                : tool.Wall == null && tool.Gate == null && tool.Zone == item.Zone && tool.Building == item.Building && tool.Density == item.Density && tool.Crop == item.Crop));
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
                // Room on painted land nobody is building on: say what it waits for.
                if (why == null && item.Crop == null && Property.Waiting(town, z) is { } wait)
                {
                    line = wait;
                    tip += $" Not filling now: {wait}.";
                }
            }
            else if (item.Gate)
            {
                int ring = GateRing();
                why = WallLines.Gates(town, ring).Count >= town.Content.City.WallLines.Most ? $"a wall has at most {town.Content.City.WallLines.Most} gates" : null;
                line = why ?? (WallLines.GateCost(town, ring) is > 0 and var gc ? $"{Js.Str(gc)}g" : "mark on the line");
                tip = "A gate in the wall, snapped onto its line where you click. Every wall needs at least one, and up to four: the townsfolk and the watch go in and out by them, and raiders try them first.";
            }
            else if (item.Defense is DefenseWork d)
            {
                why = Threat.BuildBlocked(town, d);
                // A wall waiting only for its line and gate is drawn, not blocked.
                if (why != null && why.StartsWith("needs a gate")) why = null;
                line = why ?? (DrawsFirst(d) ? $"draw · {Threat.BuildCost(town, d)}g" : $"{Threat.BuildCost(town, d)}g");
                tip = d switch
                {
                    DefenseWork.Palisade => "A wall of stakes around the town. Raiders and the warband break on it.",
                    DefenseWork.Tower => "A tower on the walls. Each one adds to the town's strength at the walls, and two of the watch live in it. Stone walls carry more of them.",
                    DefenseWork.Gatehouse => "A stone gatehouse with a portcullis over the south road. The gate holds far better when a siege tries it.",
                    DefenseWork.Moat => "A moat round the stone walls, filled from the river. Every wave wades it, and the walls count for more.",
                    DefenseWork.Barracks => "Barracks in the thickness of the stone walls. The watch lives there: cheaper to keep, and quicker to the walls when the horn sounds.",
                    DefenseWork.StoneWall => "Stone walls around the old town, far stronger than stakes. A large town needs them for its charter.",
                    _ => "The great wall of a city, around the old town and the streets about it. A great city needs it for its charter.",
                };
            }
            else
            {
                var def = town.Content.Building(item.Building!);
                why = Planning.Blocked(town, def);
                line = why ?? $"{Planning.Cost(town, def)}g";
                tip = def.Blurb != "" ? def.Blurb : BuildTip(town, def);
                // Arcane works standing together make a scholars' quarter: say so where the player picks them.
                var scholars = Neighbourhood.Rules(town).Scholars;
                if (scholars.Ids.Contains(def.Id))
                    tip += $"\nScholars' quarter: {scholars.Least} arcane works within a few streets of each other add {Js.Str(scholars.Lore)} lore a day ({Neighbourhood.Scholars(town)} together now).";
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
            { Zone: Zone.None } => "Brush to clear unbuilt paint · [ ] size · Right-click: done",
            { Zone: Zone.Farmland, Crop: string c } => $"Sow {(Farming.Crop(town, c)?.Name ?? c).ToLowerInvariant()} beyond the houses · [ ] size · Right-click: done",
            { Zone: Zone z, Density: > 0 } t => $"Paint {Density.Name(town, t.Density).ToLowerInvariant()} {Planning.ZoneName(z)}, also over built streets · [ ] size · Right-click: done",
            { Zone: Zone z } => $"Paint {Planning.ZoneName(z)} on open ground · [ ] size · Right-click: done",
            { Wall: int } => "Drag for curves, click for corners · back to the first point to close · Backspace: undo · Right-click: done",
            { OnWall: DefenseWork.Tower } => "Click a stretch of wall for the tower · Shift+click: another · Right-click: done",
            { OnWall: DefenseWork } => "Click a stretch of wall for the barracks · Right-click: done",
            { Gate: int } => "Click on the wall's line for a gate (1 to 4) · Right-click: done",
            { Building: string id } => $"Click to place the {town.Content.Building(id).Name.ToLowerInvariant()} · drag to turn · Shift+click: another · Right-click: done",
            _ => GroupLook[_buildGroup].Hint,
        };
        _buildHint.AddThemeColorOverride("font_color", _map.Planner.Tool == null ? GroupLook[_buildGroup].Hue : Ui.Ink2);
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
        // Old names from before the buckets still work in shot scripts.
        var named = group.ToLowerInvariant() switch { "zones" => "Homes", "government" => "Civic", "commission" => "Trade", var x => x };
        if (Enum.TryParse<BuildGroup>(named, true, out var g)) ShowBuildGroup(g);
        // A tool from another bucket opens that bucket.
        if (tool != null && !BuildItems(_buildGroup).Any(i => i.Building == tool || (i.Zone?.ToString().Equals(tool.Split(':')[0], StringComparison.OrdinalIgnoreCase) ?? false)))
            foreach (var other in Enum.GetValues<BuildGroup>())
                if (BuildItems(other).Any(i => i.Building == tool || (i.Zone?.ToString().Equals(tool.Split(':')[0], StringComparison.OrdinalIgnoreCase) ?? false)))
                {
                    ShowBuildGroup(other);
                    break;
                }
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
