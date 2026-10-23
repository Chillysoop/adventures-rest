using Godot;
using Alert = CrownAndCompass.Sim.Systems.Alert;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The rail down the left edge: six buttons, one a group of management windows, each with its own colour and name.
/// A group opens as one window with its windows as tabs along the top. Every button and tab carries a badge counting
/// the alerts it answers (red when one is urgent, gold for decisions, a grey dot for minor ones), so the menu itself
/// says where you are wanted. Shift and 1 to 6 opens a group. Build sits on its own at the bottom of the screen.
/// </summary>
public partial class Main
{
    private sealed record DockGroup(string Name, string Icon, Color Hue, string Tip, Tab[] Tabs);

    private const int RailWidth = 66;

    /// <summary>The six groups on the rail, each the windows it opens as tabs. A new window is a tab in one of them.</summary>
    private static readonly DockGroup[] Groups =
    [
        new("Town", "tab-city", new Color("8cc084"), "The city, the mayor, its notables and the Ledger", [Tab.City, Tab.Mayor, Tab.Notables, Tab.Ledger]),
        new("Council", "tab-council", new Color("e8b93a"), "The council, institutions and faiths", [Tab.Council, Tab.Institutions, Tab.Faith]),
        new("Heroes", "tab-companies", new Color("74aee6"), "Bounties, companies and the armory", [Tab.Bounties, Tab.Companies, Tab.Armory]),
        new("War", "tab-war", new Color("ef7f6f"), "Foes, the army and the town's defence", [Tab.War, Tab.Army, Tab.Defence]),
        new("Realm", "tab-campaign", new Color("5ec2b0"), "The campaign, diplomacy and trade", [Tab.Campaign, Tab.Diplomacy]),
        new("Lore", "tab-library", new Color("b996e6"), "Chronicles and the library", [Tab.Chronicles, Tab.Library]),
    ];

    /// <summary>Each window's name on its tab.</summary>
    private static readonly Dictionary<Tab, string> TabNames = new()
    {
        [Tab.City] = "City",
        [Tab.Mayor] = "Mayor",
        [Tab.Notables] = "Notables",
        [Tab.Ledger] = "Ledger",
        [Tab.Council] = "Council",
        [Tab.Institutions] = "Institutions",
        [Tab.Faith] = "Faith",
        [Tab.Bounties] = "Bounties",
        [Tab.Companies] = "Roster",
        [Tab.Armory] = "Armory",
        [Tab.War] = "Foes",
        [Tab.Army] = "Army",
        [Tab.Defence] = "Defence",
        [Tab.Campaign] = "Campaign",
        [Tab.Diplomacy] = "Diplomacy",
        [Tab.Chronicles] = "Chronicles",
        [Tab.Library] = "Library",
    };

    /// <summary>A line on each window, shown when its tab is hovered.</summary>
    private static readonly Dictionary<Tab, string> Blurbs = new()
    {
        [Tab.City] = "Charter, wards, housing and growth",
        [Tab.Mayor] = "Your mayor: what needs you, regalia, sheet (C)",
        [Tab.Ledger] = "Everything that happened, day by day",
        [Tab.Notables] = "People worth knowing: win them over, hear their news",
        [Tab.Council] = "Motions, members, taxes and the town's laws",
        [Tab.Institutions] = "Guilds, colleges and their keepers",
        [Tab.Faith] = "Gods, shrines and the factions",
        [Tab.Bounties] = "Post jobs and follow parties on the road",
        [Tab.Companies] = "Roster of companies: sort, compare, offer jobs",
        [Tab.Armory] = "Gifts, loot and pacts with heroes",
        [Tab.War] = "Foes the town has met",
        [Tab.Army] = "Regiments, the levy, sellswords and commanders",
        [Tab.Defence] = "Guard, walls, towers and wardens",
        [Tab.Diplomacy] = "Peoples, envoys and trade routes",
        [Tab.Campaign] = "The region, its menaces and the big bad",
        [Tab.Chronicles] = "Heroes' legends and the town's story",
        [Tab.Library] = "Classes, monsters, spells and items",
    };

    private readonly Button[] _railButtons = new Button[Groups.Length];
    private readonly (Label Count, PanelContainer Badge)[] _railBadges = new (Label, PanelContainer)[Groups.Length];
    private readonly Dictionary<Tab, (Label Count, PanelContainer Badge)> _tabBadges = [];
    /// <summary>The tab each group opens on: the one last looked at.</summary>
    private readonly Tab[] _groupTab = Groups.Select(g => g.Tabs[0]).ToArray();
    private PanelContainer _rail = null!;
    private TextureRect _windowIcon = null!;

    private static int GroupOf(Tab t) => Array.FindIndex(Groups, g => g.Tabs.Contains(t));

    private static string QuickKey(int g) => $"Shift+{g + 1}";

    private static string GroupTip(int g) => $"{Groups[g].Name}  ({QuickKey(g)})\n{Groups[g].Tip}";

    /// <summary>The rail button that opens a window: its group's.</summary>
    private Button RailButtonOf(Tab t) => _railButtons[GroupOf(t)];

    /// <summary>The window that answers an alert, if it has one.</summary>
    private static Tab? TabOf(AlertWindow w) => w switch
    {
        AlertWindow.Ledger => Tab.Ledger,
        AlertWindow.War => Tab.War,
        AlertWindow.Defence => Tab.Defence,
        AlertWindow.Council => Tab.Council,
        AlertWindow.Bounties => Tab.Bounties,
        AlertWindow.Faith => Tab.Faith,
        AlertWindow.Campaign => Tab.Campaign,
        AlertWindow.City => Tab.City,
        AlertWindow.Mayor => Tab.Mayor,
        AlertWindow.Diplomacy => Tab.Diplomacy,
        AlertWindow.Library => Tab.Library,
        AlertWindow.Companies => Tab.Companies,
        AlertWindow.Armory => Tab.Armory,
        AlertWindow.Institutions => Tab.Institutions,
        AlertWindow.Army => Tab.Army,
        AlertWindow.Notables => Tab.Notables,
        _ => null,
    };

    /// <summary>
    /// The window an alert's badge sits on: the window that answers it, or for the few answered on the map or a
    /// card, the window nearest them. Null leaves it to the Events tray (stories and crises).
    /// </summary>
    private static Tab? RailOf(Alert a) => TabOf(a.Kind.Window) ?? a.Kind.Id switch
    {
        "heroes" or "honours" => Tab.Companies,
        "crowded" or "stalled" or "tourney" or "wager" => Tab.City,
        "outpost" or "tower" or "patrol" or "scry" => Tab.Bounties,
        "lurker" => Tab.Defence,
        _ => null,
    };

    /// <summary>The group a rail alert badges.</summary>
    private static int? RailGroupOf(Alert a) => RailOf(a) is Tab t ? GroupOf(t) : null;

    /// <summary>
    /// Open a group's window, or close it if it is the one open. It opens on the tab last looked at, unless another
    /// of its tabs has something more pressing waiting.
    /// </summary>
    private void OpenGroup(int g)
    {
        if (_window.Visible && _windowTab is Tab open && GroupOf(open) == g)
        {
            CloseWindow();
            return;
        }
        var tab = _groupTab[g];
        var top = ShownAlerts.Where(a => RailOf(a) is Tab t && GroupOf(t) == g).OrderByDescending(a => a.Kind.Level).FirstOrDefault();
        if (top != null && top.Kind.Level >= AlertLevel.Decision && !ShownAlerts.Any(a => RailOf(a) == tab && a.Kind.Level >= top.Kind.Level))
            tab = RailOf(top)!.Value;
        OpenWindow(tab);
    }

    private void BuildDock()
    {
        _rail = new PanelContainer();
        _rail.AddThemeStyleboxOverride("panel", Castle.Oak(5, 8));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        _rail.AddChild(col);
        for (int g = 0; g < Groups.Length; g++) col.AddChild(RailButton(g));
        AddChild(_rail);
        _rail.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _rail.OffsetTop = TopBar + Gap;
        _rail.OffsetLeft = 0;

        // Build and Next at the bottom.
        var dock = new PanelContainer();
        dock.AddThemeStyleboxOverride("panel", Castle.Ledge(12, 4));
        var dockRow = new HBoxContainer();
        dockRow.AddThemeConstantOverride("separation", 6);
        dock.AddChild(dockRow);
        _buildToggle = DockButton("Build", "tab-build", "Zone land and place government buildings (B)");
        _buildToggle.Pressed += ToggleBuildBar;
        dockRow.AddChild(_buildToggle);
        dockRow.AddChild(BuildNextButton());
        AddChild(dock);
        _dock = dock;
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap);
        dock.GrowHorizontal = GrowDirection.Both;
        dock.GrowVertical = GrowDirection.Begin;

        _help = Ui.Label(MapHelp, new Color("f6eedb", 0.72f), Ui.Medium, 12);
        _help.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        _help.AddThemeConstantOverride("outline_size", 4);
        _help.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_help);
        _help.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap + DockHeight + 14);
        _help.GrowHorizontal = GrowDirection.Both;
        _help.GrowVertical = GrowDirection.Begin;
    }

    /// <summary>A group's button on the rail: its icon in the group's colour over its name, with a badge in the corner for what waits there.</summary>
    private Button RailButton(int g)
    {
        var grp = Groups[g];
        var b = new Button
        {
            Text = grp.Name,
            Icon = Ui.Icon(grp.Icon),
            ThemeTypeVariation = "Tab",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            ExpandIcon = false,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Top,
            CustomMinimumSize = new Vector2(RailWidth - 10, 54),
            TooltipText = GroupTip(g),
        };
        b.AddThemeConstantOverride("icon_max_width", 24);
        b.AddThemeConstantOverride("h_separation", 2);
        b.AddThemeFontSizeOverride("font_size", 11);
        StyleDockButton(b, 2, 5);
        var dim = new Color(grp.Hue, 0.85f);
        b.AddThemeColorOverride("icon_normal_color", dim);
        b.AddThemeColorOverride("icon_hover_color", grp.Hue.Lightened(0.2f));
        b.AddThemeColorOverride("icon_pressed_color", grp.Hue.Lightened(0.2f));
        b.AddThemeColorOverride("icon_hover_pressed_color", grp.Hue.Lightened(0.2f));
        b.AddThemeColorOverride("font_color", Ui.Ink2);
        b.AddThemeColorOverride("font_hover_color", Ui.Ink);
        b.AddThemeColorOverride("font_pressed_color", grp.Hue.Lightened(0.2f));
        b.AddThemeColorOverride("font_hover_pressed_color", grp.Hue.Lightened(0.2f));
        var on = Ui.Flat(new Color(grp.Hue, 0.18f), 5, 2, 5);
        on.BorderColor = grp.Hue;
        on.BorderWidthLeft = 3;
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        // A thin bar of the group's colour down the left, so the groups read apart at a glance.
        var off = Ui.Flat(Colors.Transparent, 5, 2, 5);
        off.BorderColor = new Color(grp.Hue, 0.45f);
        off.BorderWidthLeft = 2;
        b.AddThemeStyleboxOverride("normal", off);
        var hover = Ui.Flat(new Color(grp.Hue, 0.1f), 5, 2, 5);
        hover.BorderColor = grp.Hue;
        hover.BorderWidthLeft = 2;
        b.AddThemeStyleboxOverride("hover", hover);
        int group = g;
        b.Pressed += () => { OpenGroup(group); GuideEvent("rail"); };
        b.MouseEntered += () => ShowRailAlerts(group, b);
        _railBadges[g] = Badge(b);
        _railButtons[g] = b;
        return b;
    }

    /// <summary>A hidden badge in a button's corner, for <see cref="SetBadge"/>.</summary>
    private static (Label, PanelContainer) Badge(Button b)
    {
        var badge = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var count = Ui.Label("", Colors.White, Ui.Bold, 11);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.MouseFilter = MouseFilterEnum.Ignore;
        badge.AddChild(count);
        b.AddChild(badge);
        return (count, badge);
    }

    /// <summary>
    /// The tabs along the top of the window: every window of the open group, its icon and name in the group's
    /// colour when picked, each with its own badge.
    /// </summary>
    private Control BuildWindowTabs()
    {
        var strip = new PanelContainer();
        strip.AddThemeStyleboxOverride("panel", Ui.Flat(new Color(0, 0, 0, 0.22f), 0, 8, 4));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 2);
        strip.AddChild(row);
        foreach (var t in Groups.SelectMany(g => g.Tabs))
        {
            var hue = Groups[GroupOf(t)].Hue;
            var b = new Button
            {
                Text = TabNames[t],
                Icon = Ui.Icon($"tab-{t.ToString().ToLowerInvariant()}"),
                ThemeTypeVariation = "Tab",
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
                ExpandIcon = false,
                Alignment = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 34),
                TooltipText = Blurbs.GetValueOrDefault(t, ""),
                Visible = false,
            };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeConstantOverride("h_separation", 6);
            b.AddThemeFontSizeOverride("font_size", 13);
            // Room on the right for the badge.
            StyleDockButton(b, 10, 5);
            var on = Ui.Flat(new Color(hue, 0.16f), 5, 10, 5);
            on.BorderColor = hue;
            on.BorderWidthBottom = 3;
            b.AddThemeStyleboxOverride("pressed", on);
            b.AddThemeStyleboxOverride("hover_pressed", on);
            foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed" })
                if (b.GetThemeStylebox(st) is StyleBox box) box.ContentMarginRight = 20;
            b.AddThemeColorOverride("icon_pressed_color", hue);
            b.AddThemeColorOverride("icon_hover_pressed_color", hue);
            b.AddThemeColorOverride("font_color", Ui.Ink2);
            b.AddThemeColorOverride("font_pressed_color", hue.Lightened(0.25f));
            b.AddThemeColorOverride("font_hover_pressed_color", hue.Lightened(0.25f));
            var tab = t;
            b.Pressed += () =>
            {
                if (_windowTab != tab) OpenWindow(tab);
                else b.SetPressedNoSignal(true);
            };
            _tabBadges[t] = Badge(b);
            _tabButtons[t] = b;
            row.AddChild(b);
        }
        return strip;
    }

    /// <summary>Count the alerts by the window that answers them, onto the rail's badges, the window's tabs and the tray's.</summary>
    private void UpdateBadges()
    {
        var by = new Dictionary<Tab, List<Alert>>();
        foreach (var a in ShownAlerts)
            if (RailOf(a) is Tab t)
            {
                if (!by.TryGetValue(t, out var list)) by[t] = list = [];
                list.Add(a);
            }
        for (int g = 0; g < Groups.Length; g++)
        {
            var list = Groups[g].Tabs.SelectMany(t => by.GetValueOrDefault(t) ?? []).ToList();
            var (count, badge) = _railBadges[g];
            SetBadge(count, badge, list, _railButtons[g], GroupTip(g), listOnHover: true);
        }
        foreach (var (t, (count, badge)) in _tabBadges)
            SetBadge(count, badge, by.GetValueOrDefault(t) ?? [], _tabButtons[t], Blurbs.GetValueOrDefault(t, ""));
        UpdateTrayBadges();
    }

    /// <summary>A badge for a list of alerts: how many, red if any is urgent, gold for decisions, a grey dot for minor ones.</summary>
    private static void SetBadge(Label count, PanelContainer badge, List<Alert> alerts, Control owner, string tip, bool listOnHover = false)
    {
        badge.Visible = alerts.Count > 0;
        if (alerts.Count == 0)
        {
            owner.TooltipText = tip;
            return;
        }
        var top = alerts.Max(a => a.Kind.Level);
        bool dot = top == AlertLevel.Minor;
        // The number counts the kinds that need a choice, not every alert of them; minor ones only make the dot.
        count.Text = dot ? "" : Families(alerts.Where(a => a.Kind.Level > AlertLevel.Minor)).Count.ToString();
        count.Visible = !dot;
        var fill = top == AlertLevel.Urgent ? new Color("c94b3b") : top == AlertLevel.Decision ? Ui.Gold : Ui.Ink2;
        var s = Ui.Flat(fill, dot ? 4 : 5, dot ? 4 : 0, 99);
        s.BorderColor = new Color("1b140d");
        s.SetBorderWidthAll(1);
        badge.AddThemeStyleboxOverride("panel", s);
        count.AddThemeColorOverride("font_color", top == AlertLevel.Urgent ? Colors.White : new Color("2a1d0c"));
        badge.CustomMinimumSize = dot ? new Vector2(9, 9) : new Vector2(18, 17);
        // A rail button lists its alerts in a popup on hover (Alerts.cs ShowRailAlerts), so no tooltip on top of it.
        owner.TooltipText = listOnHover ? "" : tip + "\n" + string.Join("\n", alerts.Take(4).Select(a => $"• {a.Text}"));
        // In the owner's top-right corner (a button lays out no children of its own).
        badge.Size = badge.GetCombinedMinimumSize();
        badge.Position = new Vector2(Math.Max(owner.Size.X, owner.CustomMinimumSize.X) - badge.Size.X - 2, 2);
    }

    private void SyncDock()
    {
        int open = _window.Visible && _windowTab is Tab wt ? GroupOf(wt) : -1;
        for (int g = 0; g < Groups.Length; g++) _railButtons[g].SetPressedNoSignal(g == open);
        foreach (var (t, b) in _tabButtons)
        {
            b.Visible = open >= 0 && GroupOf(t) == open;
            b.SetPressedNoSignal(_window.Visible && _windowTab == t);
        }
        PlaceInspector();
    }

    /// <summary>
    /// Where the tutorial points for a window: nowhere when it is open, its tab when its group's window is, else its
    /// group's rail button.
    /// </summary>
    private Control? TabAnchor(Tab tab)
    {
        if (_window.Visible && _windowTab == tab) return null;
        if (_window.Visible && _windowTab is Tab wt && GroupOf(wt) == GroupOf(tab)) return _tabButtons[tab];
        return RailButtonOf(tab);
    }

    /// <summary>A dock button: its icon beside a short name.</summary>
    private static Button DockButton(string text, string icon, string tip)
    {
        var b = new Button
        {
            Text = text,
            Icon = Ui.Icon(icon),
            ThemeTypeVariation = "Tab",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            TooltipText = tip,
            ExpandIcon = false,
            CustomMinimumSize = new Vector2(112, 38),
        };
        b.AddThemeConstantOverride("icon_max_width", 22);
        b.AddThemeConstantOverride("h_separation", 8);
        b.AddThemeFontSizeOverride("font_size", 15);
        StyleDockButton(b, 14, 6);
        return b;
    }

    private static void StyleDockButton(Button b, int padX, int padY)
    {
        foreach (var state in new[] { "normal", "hover" }) b.AddThemeStyleboxOverride(state, Ui.Flat(state == "hover" ? new Color(Ui.Gold, 0.08f) : Colors.Transparent, 5, padX, padY));
        var on = Ui.Flat(new Color(Ui.Gold, 0.14f), 5, padX, padY);
        on.BorderColor = Ui.Accent;
        on.BorderWidthBottom = 2;
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        b.AddThemeColorOverride("icon_normal_color", Ui.Ink2);
        b.AddThemeColorOverride("icon_hover_color", Ui.Ink);
        b.AddThemeColorOverride("icon_pressed_color", Ui.Gold);
        b.AddThemeColorOverride("icon_hover_pressed_color", Ui.Gold);
    }
}
