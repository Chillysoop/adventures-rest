using Godot;
using Alert = CrownAndCompass.Sim.Systems.Alert;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// The rail down the left edge: every management window one click away, in five groups split by thin lines. Each
/// button carries a badge counting the alerts that window answers (red when one is urgent, gold for decisions, a
/// grey dot for minor ones), so the menu itself says where you are wanted. Shift and a number opens the ten most
/// used. Build sits on its own at the bottom of the screen.
/// </summary>
public partial class Main
{
    private sealed record DockGroup(string Name, string Icon, string Tip, Tab[] Tabs);

    private const int RailWidth = 58;

    /// <summary>Every management window, in the group it belongs to on the rail.</summary>
    private static readonly DockGroup[] Groups =
    [
        new("Town", "tab-city", "Your town: the city, the mayor, its notables and the Ledger", [Tab.City, Tab.Mayor, Tab.Notables, Tab.Ledger]),
        new("Council", "tab-council", "The council, the town's institutions and its faiths", [Tab.Council, Tab.Institutions, Tab.Faith]),
        new("Heroes", "tab-companies", "Bounties, adventuring companies and the armory", [Tab.Bounties, Tab.Companies, Tab.Armory]),
        new("Realm", "tab-campaign", "War, diplomacy and the campaign beyond the walls", [Tab.War, Tab.Diplomacy, Tab.Campaign]),
        new("Lore", "tab-library", "The town's chronicles and its library", [Tab.Chronicles, Tab.Library]),
    ];

    /// <summary>A line on each window, shown when the rail button is hovered.</summary>
    private static readonly Dictionary<Tab, string> Blurbs = new()
    {
        [Tab.City] = "Charter, wards, housing and growth",
        [Tab.Mayor] = "Your character sheet and level-ups (C)",
        [Tab.Ledger] = "Everything that happened, day by day",
        [Tab.Notables] = "People worth knowing: win them over, hear their news",
        [Tab.Council] = "Motions, members, taxes and the town's laws",
        [Tab.Institutions] = "Guilds, colleges and their keepers",
        [Tab.Faith] = "Gods, shrines and the factions",
        [Tab.Bounties] = "Post jobs and follow parties on the road",
        [Tab.Companies] = "Roster of companies: sort, compare, offer jobs",
        [Tab.Armory] = "Gifts, loot and pacts with heroes",
        [Tab.War] = "The enemy, your defence, the guard and walls",
        [Tab.Diplomacy] = "Peoples, envoys and trade routes",
        [Tab.Campaign] = "The region, its menaces and the big bad",
        [Tab.Chronicles] = "Heroes' legends and the town's story",
        [Tab.Library] = "Classes, monsters, spells and items",
    };

    /// <summary>Shift+1 to Shift+0: the windows a mayor opens most, in that order.</summary>
    private static readonly Tab[] QuickTabs = [Tab.Bounties, Tab.Council, Tab.War, Tab.Campaign, Tab.City, Tab.Companies, Tab.Armory, Tab.Diplomacy, Tab.Institutions, Tab.Ledger];

    private readonly Dictionary<Tab, (Label Count, PanelContainer Badge)> _badges = [];
    private PanelContainer _rail = null!;

    private static string QuickKey(Tab t) => Array.IndexOf(QuickTabs, t) is int i and >= 0 ? $"Shift+{(i + 1) % 10}" : "";

    /// <summary>The window that answers an alert, if it has one.</summary>
    private static Tab? TabOf(AlertWindow w) => w switch
    {
        AlertWindow.Ledger => Tab.Ledger,
        AlertWindow.War => Tab.War,
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
        AlertWindow.Notables => Tab.Notables,
        _ => null,
    };

    private void BuildDock()
    {
        _rail = new PanelContainer();
        _rail.AddThemeStyleboxOverride("panel", Castle.Oak(5, 8));
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 2);
        _rail.AddChild(col);
        for (int g = 0; g < Groups.Length; g++)
        {
            if (g > 0)
            {
                var line = new ColorRect { Color = new Color(Ui.Line, 0.8f), CustomMinimumSize = new Vector2(0, 1), MouseFilter = MouseFilterEnum.Ignore };
                var pad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
                foreach (var s in new[] { "left", "right" }) pad.AddThemeConstantOverride($"margin_{s}", 6);
                foreach (var s in new[] { "top", "bottom" }) pad.AddThemeConstantOverride($"margin_{s}", 3);
                pad.AddChild(line);
                col.AddChild(pad);
            }
            foreach (var t in Groups[g].Tabs) col.AddChild(RailButton(t));
        }
        AddChild(_rail);
        _rail.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
        _rail.OffsetTop = TopBar + Gap;
        _rail.OffsetLeft = 0;

        // Build, on its own at the bottom.
        var dock = new PanelContainer();
        dock.AddThemeStyleboxOverride("panel", Castle.Ledge(12, 4));
        _buildToggle = DockButton("Build", "tab-build", "Zone land and place government buildings (B)");
        _buildToggle.Pressed += ToggleBuildBar;
        dock.AddChild(_buildToggle);
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

    /// <summary>A window's button on the rail: its icon, with a badge in the corner for what waits there.</summary>
    private Button RailButton(Tab t)
    {
        string key = QuickKey(t);
        var b = new Button
        {
            Icon = Ui.Icon($"tab-{t.ToString().ToLowerInvariant()}"),
            ThemeTypeVariation = "Tab",
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            ExpandIcon = false,
            IconAlignment = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(46, 40),
            TooltipText = $"{Titles[t]}{(key != "" ? $"  ({key})" : "")}\n{Blurbs.GetValueOrDefault(t, "")}",
        };
        b.AddThemeConstantOverride("icon_max_width", 24);
        StyleDockButton(b, 4, 4);
        var on = Ui.Flat(new Color(Ui.Gold, 0.16f), 5, 4, 4);
        on.BorderColor = Ui.Accent;
        on.BorderWidthLeft = 3;
        b.AddThemeStyleboxOverride("pressed", on);
        b.AddThemeStyleboxOverride("hover_pressed", on);
        var tab = t;
        b.Pressed += () => { SetTab(tab); GuideEvent("rail"); };

        var badge = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
        var count = Ui.Label("", Colors.White, Ui.Bold, 11);
        count.HorizontalAlignment = HorizontalAlignment.Center;
        count.MouseFilter = MouseFilterEnum.Ignore;
        badge.AddChild(count);
        b.AddChild(badge);
        _badges[t] = (count, badge);
        _tabButtons[t] = b;
        return b;
    }

    /// <summary>Count the alerts on the strip by the window that answers them, onto the rail's badges and the tray's.</summary>
    private void UpdateBadges()
    {
        var by = new Dictionary<Tab, List<Alert>>();
        foreach (var a in ShownAlerts)
            if (TabOf(a.Kind.Window) is Tab t)
            {
                if (!by.TryGetValue(t, out var list)) by[t] = list = [];
                list.Add(a);
            }
        foreach (var (t, (count, badge)) in _badges)
        {
            var list = by.GetValueOrDefault(t);
            SetBadge(count, badge, list ?? [], _tabButtons[t], $"{Titles[t]}{(QuickKey(t) is { Length: > 0 } k ? $"  ({k})" : "")}\n{Blurbs.GetValueOrDefault(t, "")}");
        }
        UpdateTrayBadges();
    }

    /// <summary>A badge for a list of alerts: how many, red if any is urgent, gold for decisions, a grey dot for minor ones.</summary>
    private static void SetBadge(Label count, PanelContainer badge, List<Alert> alerts, Control owner, string tip)
    {
        badge.Visible = alerts.Count > 0;
        if (alerts.Count == 0)
        {
            owner.TooltipText = tip;
            return;
        }
        var top = alerts.Max(a => a.Kind.Level);
        bool dot = top == AlertLevel.Minor;
        count.Text = dot ? "" : alerts.Count.ToString();
        count.Visible = !dot;
        var fill = top == AlertLevel.Urgent ? new Color("c94b3b") : top == AlertLevel.Decision ? Ui.Gold : Ui.Ink2;
        var s = Ui.Flat(fill, dot ? 4 : 5, dot ? 4 : 0, 99);
        s.BorderColor = new Color("1b140d");
        s.SetBorderWidthAll(1);
        badge.AddThemeStyleboxOverride("panel", s);
        count.AddThemeColorOverride("font_color", top == AlertLevel.Urgent ? Colors.White : new Color("2a1d0c"));
        badge.CustomMinimumSize = dot ? new Vector2(9, 9) : new Vector2(18, 17);
        owner.TooltipText = tip + "\n" + string.Join("\n", alerts.Take(4).Select(a => $"• {a.Text}"));
        // In the owner's top-right corner (a button lays out no children of its own).
        badge.Size = badge.GetCombinedMinimumSize();
        badge.Position = new Vector2(Math.Max(owner.Size.X, owner.CustomMinimumSize.X) - badge.Size.X - 2, 2);
    }

    private void SyncDock()
    {
        foreach (var (t, b) in _tabButtons) b.SetPressedNoSignal(_window.Visible && _windowTab == t);
        PlaceInspector();
    }

    /// <summary>Where the tutorial points for a window: its rail button, unless it is open already.</summary>
    private Control? TabAnchor(Tab tab, Button row) => _window.Visible && _windowTab == tab ? null : row;

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
