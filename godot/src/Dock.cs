using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The dock, after RimWorld's bottom bar: a few groups instead of a button per window. Clicking a group pops its
/// windows up in a short list above it, each with a line on what it is for; picking one opens the window, and the
/// window's shelf under its title switches between the windows of the same group. Build sits at the end on its own.
/// </summary>
public partial class Main
{
    private sealed record DockGroup(string Name, string Icon, string Tip, Tab[] Tabs);

    /// <summary>Every management window, in the group it belongs to. A window left out gets a group of its own.</summary>
    private static readonly DockGroup[] Groups =
    [
        new("Town", "tab-city", "Your town: the city, the mayor and the Ledger", [Tab.City, Tab.Mayor, Tab.Ledger]),
        new("Council", "tab-council", "The council, the town's institutions and its faiths", [Tab.Council, Tab.Institutions, Tab.Faith]),
        new("Heroes", "tab-companies", "Bounties, adventuring companies and the armory", [Tab.Bounties, Tab.Companies, Tab.Armory]),
        new("Realm", "tab-campaign", "War, diplomacy and the campaign beyond the walls", [Tab.War, Tab.Diplomacy, Tab.Campaign]),
        new("Lore", "tab-library", "The town's chronicles and its library", [Tab.Chronicles, Tab.Library]),
    ];

    /// <summary>A line on each window, shown in the group's list.</summary>
    private static readonly Dictionary<Tab, string> Blurbs = new()
    {
        [Tab.City] = "Charter, wards, housing and growth",
        [Tab.Mayor] = "Your character sheet and level-ups (C)",
        [Tab.Ledger] = "Everything that happened, day by day",
        [Tab.Council] = "Motions, members and the town's laws",
        [Tab.Institutions] = "Guilds, colleges and their keepers",
        [Tab.Faith] = "Gods, shrines and the factions",
        [Tab.Bounties] = "Post jobs and follow parties on the road",
        [Tab.Companies] = "Adventuring companies and their reports",
        [Tab.Armory] = "Gifts, loot and pacts with heroes",
        [Tab.War] = "The enemy, your defence, the guard and walls",
        [Tab.Diplomacy] = "Peoples, envoys and trade routes",
        [Tab.Campaign] = "The region, its menaces and the big bad",
        [Tab.Chronicles] = "Heroes' legends and the town's story",
        [Tab.Library] = "Classes, monsters, spells and items",
    };

    private readonly List<Button> _dockGroups = [];
    private readonly Dictionary<int, Tab> _groupLast = [];
    private readonly Dictionary<Tab, Button> _shelfButtons = [];
    private PanelContainer _picker = null!;
    private VBoxContainer _pickerList = null!;
    private int _pickerGroup = -1;
    private HBoxContainer _shelf = null!;

    private static int GroupOf(Tab t)
    {
        for (int i = 0; i < Groups.Length; i++)
            if (Groups[i].Tabs.Contains(t)) return i;
        return -1;
    }

    private static Tab[] TabsOf(int g) => g >= 0 ? Groups[g].Tabs : [];

    private void BuildDock()
    {
        var dock = new PanelContainer();
        dock.AddThemeStyleboxOverride("panel", Castle.Ledge(12, 4));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        dock.AddChild(row);
        for (int i = 0; i < Groups.Length; i++)
        {
            var g = Groups[i];
            int at = i;
            var b = DockButton(g.Name, g.Icon, g.Tip);
            b.Pressed += () => TogglePicker(at);
            row.AddChild(b);
            _dockGroups.Add(b);
            if (g.Tabs.Contains(Tab.Council))
            {
                _motionDot = new ColorRect { Color = Ui.Accent, Visible = false, MouseFilter = MouseFilterEnum.Ignore };
                b.AddChild(_motionDot);
                _motionDot.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight);
                _motionDot.OffsetLeft = -10;
                _motionDot.OffsetRight = -4;
                _motionDot.OffsetTop = 6;
                _motionDot.OffsetBottom = 12;
            }
        }
        row.AddChild(new VSeparator());
        _buildToggle = DockButton("Build", "tab-build", "Zone land and place government buildings (B)");
        _buildToggle.Pressed += ToggleBuildBar;
        row.AddChild(_buildToggle);
        AddChild(dock);
        _dock = dock;
        dock.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap);
        dock.GrowHorizontal = GrowDirection.Both;
        dock.GrowVertical = GrowDirection.Begin;

        BuildPicker();

        _help = Ui.Label(MapHelp, new Color("f6eedb", 0.72f), Ui.Medium, 12);
        _help.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.6f));
        _help.AddThemeConstantOverride("outline_size", 4);
        _help.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(_help);
        _help.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterBottom, LayoutPresetMode.Minsize, Gap + DockHeight + 14);
        _help.GrowHorizontal = GrowDirection.Both;
        _help.GrowVertical = GrowDirection.Begin;
    }

    /// <summary>The list a group pops up: one row per window, its icon, name and what it is for.</summary>
    private void BuildPicker()
    {
        _picker = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Stop };
        _picker.AddThemeStyleboxOverride("panel", Castle.Oak(10, 10));
        _pickerList = new VBoxContainer();
        _pickerList.AddThemeConstantOverride("separation", 2);
        _picker.AddChild(_pickerList);
        AddChild(_picker);
        foreach (var t in Enum.GetValues<Tab>())
        {
            if (t == Tab.Inspect) continue;
            var b = new Button
            {
                Text = Titles[t],
                Icon = Ui.Icon($"tab-{t.ToString().ToLowerInvariant()}"),
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.None,
                ExpandIcon = false,
                CustomMinimumSize = new Vector2(350, 48),
                Visible = false,
            };
            b.AddThemeConstantOverride("icon_max_width", 24);
            b.AddThemeConstantOverride("h_separation", 10);
            b.AddThemeFontSizeOverride("font_size", 15);
            b.AddThemeStyleboxOverride("normal", Ui.Flat(Colors.Transparent, 6, 10, 4));
            b.AddThemeStyleboxOverride("hover", Ui.Flat(new Color(Ui.Gold, 0.14f), 6, 10, 4));
            b.AddThemeStyleboxOverride("pressed", Ui.Flat(new Color(Ui.Gold, 0.2f), 6, 10, 4));
            b.AddThemeColorOverride("icon_normal_color", Ui.Ink2);
            b.AddThemeColorOverride("icon_hover_color", Ui.Gold);
            // The window's name on top, a line on what it is for under it.
            b.Text = "";
            var words = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            words.AddThemeConstantOverride("separation", -1);
            words.AddChild(Ui.Label(Titles[t], Ui.Ink, Ui.Bold, 15));
            words.AddChild(Ui.Label(Blurbs.GetValueOrDefault(t, ""), Ui.Ink2, Ui.Medium, 12));
            foreach (var l in words.GetChildren().OfType<Control>()) l.MouseFilter = MouseFilterEnum.Ignore;
            b.AddChild(words);
            words.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            words.OffsetLeft = 46;
            words.Alignment = BoxContainer.AlignmentMode.Center;
            var tab = t;
            b.Pressed += () => OpenWindow(tab);
            _pickerList.AddChild(b);
            _tabButtons[t] = b;
        }
    }

    private void TogglePicker(int g)
    {
        if (_picker.Visible && _pickerGroup == g) ClosePicker();
        else OpenPicker(g);
    }

    private void OpenPicker(int g)
    {
        _pickerGroup = g;
        foreach (var (t, b) in _tabButtons) b.Visible = GroupOf(t) == g;
        _picker.Visible = true;
        _picker.ResetSize();
        _picker.Size = _picker.GetCombinedMinimumSize();
        var from = _dockGroups[g].GetGlobalRect();
        var vp = GetViewportRect().Size;
        float x = Mathf.Clamp(from.Position.X + from.Size.X / 2 - _picker.Size.X / 2, Gap, vp.X - _picker.Size.X - Gap);
        _picker.Position = new Vector2(x, _dock.GetGlobalRect().Position.Y - _picker.Size.Y - 6);
        _picker.MoveToFront();
        SyncDock();
    }

    private void ClosePicker()
    {
        if (!_picker.Visible) return;
        _picker.Visible = false;
        _pickerGroup = -1;
        SyncDock();
    }

    /// <summary>A click outside the open list and the dock closes the list, as RimWorld's does.</summary>
    private void PickerInput(InputEvent e)
    {
        if (!_picker.Visible || e is not InputEventMouseButton { Pressed: true } mb) return;
        if (_picker.GetGlobalRect().HasPoint(mb.Position) || _dock.GetGlobalRect().HasPoint(mb.Position)) return;
        ClosePicker();
    }

    /// <summary>The strip under a window's title with the other windows of its group.</summary>
    private Control BuildShelf()
    {
        var pad = new MarginContainer();
        foreach (var s in new[] { "left", "right" }) pad.AddThemeConstantOverride($"margin_{s}", 10);
        pad.AddThemeConstantOverride("margin_top", 6);
        _shelf = new HBoxContainer();
        _shelf.AddThemeConstantOverride("separation", 4);
        pad.AddChild(_shelf);
        foreach (var t in Enum.GetValues<Tab>())
        {
            if (t == Tab.Inspect) continue;
            var b = new Button
            {
                Text = t.ToString(),
                Icon = Ui.Icon($"tab-{t.ToString().ToLowerInvariant()}"),
                ThemeTypeVariation = "Tab",
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
                ExpandIcon = false,
                TooltipText = Blurbs.GetValueOrDefault(t, ""),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            b.AddThemeConstantOverride("icon_max_width", 18);
            b.AddThemeFontSizeOverride("font_size", 13);
            StyleDockButton(b, 6, 5);
            var tab = t;
            b.Pressed += () => { if (_windowTab != tab) OpenWindow(tab); else b.SetPressedNoSignal(true); };
            _shelf.AddChild(b);
            _shelfButtons[t] = b;
        }
        return pad;
    }

    private void SyncDock()
    {
        int open = _window.Visible && _windowTab is Tab w ? GroupOf(w) : -1;
        for (int i = 0; i < _dockGroups.Count; i++) _dockGroups[i].SetPressedNoSignal(i == open || i == _pickerGroup);
        foreach (var (t, b) in _shelfButtons)
        {
            b.Visible = open >= 0 && GroupOf(t) == open && TabsOf(open).Length > 1;
            b.SetPressedNoSignal(_windowTab == t);
        }
        _shelf.GetParent<Control>().Visible = open >= 0 && TabsOf(open).Length > 1;
        PlaceInspector();
    }

    /// <summary>Where the tutorial points for a window: its row in the open list, its shelf button, else its group.</summary>
    private Control? TabAnchor(Tab tab, Button row)
    {
        if (_window.Visible && _windowTab == tab) return null;
        if (_picker.Visible && _pickerGroup == GroupOf(tab)) return row;
        if (_shelfButtons.TryGetValue(tab, out var s) && s.IsVisibleInTree()) return s;
        int g = GroupOf(tab);
        return g >= 0 ? _dockGroups[g] : null;
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
