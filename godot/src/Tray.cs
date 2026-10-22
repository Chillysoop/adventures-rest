using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using Alert = CrownAndCompass.Sim.Systems.Alert;

namespace CrownAndCompass.Client;

/// <summary>
/// The action tray on the right edge: the four things a mayor answers most (the bounty board, the council,
/// events and the war) pinned as buttons with live counts. Clicking one slides out a drawer over the map with
/// what waits there and the buttons to answer it, so most of the day's business never needs a window; the full
/// window is one click further.
/// </summary>
public partial class Main
{
    private const int TrayWidth = 56, DrawerWidth = 390;

    private sealed record TrayDef(string Id, string Name, string Icon, string Tip, Tab? Window);

    private static readonly TrayDef[] Trays =
    [
        new("Bounties", "Bounty board", "tab-bounties", "Jobs to post, parties on the road and townsfolk to rescue", Tab.Bounties),
        new("Council", "The council", "tab-council", "The motion before the council and the town's taxes", Tab.Council),
        new("Events", "Events", "d20", "Stories and crises waiting for the mayor's word", null),
        new("War", "The war", "tab-war", "The enemy, your defence and raiders at the gates", Tab.War),
    ];

    private readonly Dictionary<string, (Button Button, Label Count, PanelContainer Badge)> _trayButtons = [];
    private PanelContainer _drawer = null!;
    private Label _drawerTitle = null!;
    private Pane _trayPane = null!;
    private string? _trayOpen;

    private void BuildTray()
    {
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 6);
        AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.TopRight, LayoutPresetMode.Minsize);
        col.GrowHorizontal = GrowDirection.Begin;
        col.OffsetTop = TopBar + Gap + 12;
        col.OffsetRight = 0;
        foreach (var d in Trays)
        {
            var b = new Button
            {
                Icon = Ui.Icon(d.Icon),
                ToggleMode = true,
                FocusMode = FocusModeEnum.None,
                ExpandIcon = false,
                IconAlignment = HorizontalAlignment.Center,
                CustomMinimumSize = new Vector2(TrayWidth, 50),
                TooltipText = $"{d.Name}\n{d.Tip}",
            };
            b.AddThemeConstantOverride("icon_max_width", 26);
            foreach (var state in new[] { "normal", "hover", "pressed", "hover_pressed" })
            {
                var s = Castle.Oak(4, 4);
                if (state != "normal") s.ModulateColor = state == "hover" ? new Color(1.15f, 1.1f, 1.05f) : new Color(1.3f, 1.2f, 1.05f);
                b.AddThemeStyleboxOverride(state, s);
            }
            b.AddThemeColorOverride("icon_normal_color", Ui.Ink2);
            b.AddThemeColorOverride("icon_hover_color", Ui.Ink);
            b.AddThemeColorOverride("icon_pressed_color", Ui.Gold);
            b.AddThemeColorOverride("icon_hover_pressed_color", Ui.Gold);
            string id = d.Id;
            b.Pressed += () => ToggleTray(id);
            var badge = new PanelContainer { Visible = false, MouseFilter = MouseFilterEnum.Ignore };
            var count = Ui.Label("", Colors.White, Ui.Bold, 11);
            count.HorizontalAlignment = HorizontalAlignment.Center;
            count.MouseFilter = MouseFilterEnum.Ignore;
            badge.AddChild(count);
            b.AddChild(badge);
            col.AddChild(b);
            _trayButtons[id] = (b, count, badge);
        }
        _trayColumn = col;

        _drawer = Floating(out var body, out _drawerTitle, () => CloseTray());
        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var s in new[] { "left", "right" }) pad.AddThemeConstantOverride($"margin_{s}", 12);
        pad.AddThemeConstantOverride("margin_top", 4);
        pad.AddThemeConstantOverride("margin_bottom", 12);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 8);
        pad.AddChild(box);
        scroll.AddChild(pad);
        body.AddChild(scroll);
        _trayPane = new Pane(box);
        AddChild(_drawer);
        _drawer.AnchorLeft = _drawer.AnchorRight = 1;
        _drawer.AnchorTop = 0;
        _drawer.AnchorBottom = 1;
        _drawer.OffsetRight = -(TrayWidth + 6);
        _drawer.OffsetLeft = _drawer.OffsetRight - DrawerWidth;
        _drawer.OffsetTop = TopBar + Gap;
        _drawer.OffsetBottom = -(DockHeight + Gap * 2 + 16);
    }

    private Control _trayColumn = null!;

    private void ToggleTray(string id)
    {
        if (_trayOpen == id) CloseTray();
        else OpenTray(id);
    }

    private void OpenTray(string id)
    {
        _trayOpen = id;
        _drawerTitle.Text = Trays.First(d => d.Id == id).Name;
        _drawer.Visible = true;
        _drawer.MoveToFront();
        _trayPane.Invalidate();
        // The news cards sit where the drawer opens: they wait behind it.
        _feed.Visible = false;
        SyncTray();
        _dirty = true;
        GuideEvent($"tray:{id}");
    }

    private void CloseTray()
    {
        _trayOpen = null;
        _drawer.Visible = false;
        _feed.Visible = true;
        SyncTray();
    }

    private void SyncTray()
    {
        foreach (var (id, (b, _, _)) in _trayButtons) b.SetPressedNoSignal(id == _trayOpen);
    }

    /// <summary>The alerts a tray answers: its window's, or for Events every story waiting on the mayor.</summary>
    private List<Alert> TrayAlerts(TrayDef d) =>
        ShownAlerts.Where(a => d.Window is Tab w ? RailOf(a) == w : a.Kind.Window == AlertWindow.Story || a.StoryId != null && RailOf(a) == null).ToList();

    private void UpdateTrayBadges()
    {
        foreach (var d in Trays)
        {
            var (b, count, badge) = _trayButtons[d.Id];
            SetBadge(count, badge, TrayAlerts(d), b, $"{d.Name}\n{d.Tip}");
        }
    }

    private void RenderTray()
    {
        if (_trayOpen is not string id) return;
        var d = Trays.First(x => x.Id == id);
        var p = _trayPane;
        var t = _sim.Town;
        p.Begin();
        // The council's motion is shown in full below, with its buttons.
        var waiting = TrayAlerts(d).Where(a => !(id == "Council" && a.Kind == Alerts.Motion)).ToList();
        if (waiting.Count > 0)
        {
            p.Heading("Needs you");
            foreach (var a in waiting.OrderByDescending(a => a.Kind.Level).ThenBy(a => a.Key, StringComparer.Ordinal))
            {
                var alert = a;
                p.Row($"{Ui.C(LevelColor(a.Kind.Level), $"[b]{Ui.Esc(a.Text)}[/b]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(a.Tip)}[/font_size]")}",
                    (a.StoryId != null || a.FindId != null ? "Answer" : "Go", true, a.Kind.Level == AlertLevel.Urgent, () => Go(alert)));
            }
        }
        switch (id)
        {
            case "Bounties": TrayBounties(p, t); break;
            case "Council": RenderMotion(p, t); RenderTaxes(p, t); break;
            case "Events":
                if (waiting.Count == 0) p.Note("No story waits on the mayor. When one does, its card can be answered from here.");
                break;
            case "War": TrayWar(p, t); break;
        }
        if (d.Window is Tab w)
        {
            var win = w;
            p.Buttons(("Open the full window →", true, false, () => OpenWindow(win)));
        }
        p.Commit();
    }

    /// <summary>The war in short: the enemy's stage, its strength against yours and the heroes at home.</summary>
    private void TrayWar(Pane p, Town t)
    {
        double def = Threat.Defense(t);
        bool band = Threat.IsActive(t) && Discovery.Known(t, Discovery.Warband);
        double? est = band ? Threat.EstimatedStrength(t) : null;
        if (band)
        {
            var stage = Stages[t.Enemy.Stage];
            string arrival = t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at
                ? "\n" + Ui.C(Ui.Bad, $"[b]At the {Garrison.Place(t, t.Enemy.Approach)} in {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))}.[/b]")
                : "";
            p.Card($"{Ui.KindIcon("warband", 20)}[b]{Ui.Esc(Ui.Cap(t.Content.Enemy.Name))}[/b]  {Tag(stage.Label, stage.Level == 2 ? Ui.Bad : stage.Level == 1 ? Ui.Gold : Ui.Ink2)}\n[font_size=13]{stage.Text}[/font_size]{arrival}", stage.Level == 2 ? Ui.Bad : null);
        }
        var foes = Discovery.KnownMenaces(t).Where(m => m.Origin != Menaces.FromWarband).OrderByDescending(m => m.ArrivalTick != null).ThenByDescending(m => m.Power).ThenBy(m => m.Id).ToList();
        foreach (var m in foes.Take(3))
            p.Text($"{Ui.KindIcon(m.Kind, 16)}[b]{Ui.Esc(Ui.Cap(m.Name))}[/b]  {Tag(MenaceStages[m.Stage].Word, MenaceStages[m.Stage].Color)}{(m.ArrivalTick != null ? "  " + Tag("marching", Ui.Bad) : "")}", 13);
        if (!band && foes.Count == 0) p.Text($"{Ui.Img("foe-unseen", 18)} {Ui.C(Ui.Ink2, "No foes known")}", 13);
        double scale = Math.Max(Math.Max(def, est ?? 0), 1);
        if (est is double e) p.Meter("Warband", 100 * e / scale, $"~{Math.Round(e)}", true);
        p.Meter("Your defence", 100 * def / scale, $"{Math.Round(def)}");
        int home = t.Heroes.Count(a => !a.IsAway);
        p.Text($"Heroes in town {Tag(home.ToString(), home == 0 ? Ui.Bad : Ui.Ink)}   The watch {Tag(t.Watch.ToString(), Ui.Ink)}", 13);
    }
}
