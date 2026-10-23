using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;
using Alert = CrownAndCompass.Sim.Systems.Alert;

namespace CrownAndCompass.Client;

/// <summary>
/// The alerts. They live on the left rail: each window's button carries a badge counting what waits there (red
/// urgent, gold a decision, a grey dot for minor), hovering it lists them (click one to go where it is answered,
/// right-click to dismiss), and a new urgent or decision alert slides a tile out of the button (RailNotices.cs).
/// Stories and crises badge the Events tray. The old icon strip under the top bar is kept as the alerts' model
/// but no longer shown. What the alerts are comes from
/// the sim (<see cref="Alerts"/>), and each is one thing the player should act on (news stays in the feed):
/// an alert lasts while its condition does and clears once it is resolved. Minor alerts also time out while
/// the clock runs; decisions and urgent ones stay until resolved or dismissed. A dismissed or faded alert
/// returns only after its condition clears and comes back.
/// </summary>
public partial class Main
{
    private const double MinorSeconds = 60;
    private const double FadeSeconds = 6;
    /// <summary>The most kinds of alert a rail button lists on hover; the rest wait in its window.</summary>
    private const int RailRows = 5;

    private HBoxContainer _alertRow = null!;
    private PanelContainer _alertTip = null!, _alertList = null!;
    private VBoxContainer _alertTipBox = null!, _alertListBox = null!;

    /// <summary>Seconds of running (unpaused) game, so minor alerts wait while the player reads.</summary>
    private double _alertClock;
    private List<Alert> _standing = [];
    private readonly Dictionary<string, double> _alertBorn = [];
    private readonly HashSet<string> _dismissed = [];
    private string _alertSig = "";
    private string? _openKind;

    /// <summary>A rail list opened by --alert-hover for a shot stays open without the mouse.</summary>
    private bool _railPinned;
    private Control? _tipAnchor, _listAnchor;
    private string? _tipKind;
    private readonly List<(AlertKind Kind, List<Alert> Items, Control Icon)> _alertGroups = [];

    /// <summary>Every alert on the strip now, for the tutorial's "alert:" conditions.</summary>
    private IEnumerable<Alert> ShownAlerts => _alertGroups.SelectMany(g => g.Items);

    /// <summary>The top-centre column: the alert strip, then the paused pill under it.</summary>
    private VBoxContainer BuildAlerts()
    {
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        col.OffsetTop = TopBar + 10;
        // Between the management window on the left and the news feed on the right.
        col.OffsetLeft = RailWidth + 480;
        col.OffsetRight = -(TrayWidth + 360);
        _alertRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, Visible = false };
        _alertRow.AddThemeConstantOverride("separation", 6);
        col.AddChild(_alertRow);

        (_alertTip, _alertTipBox) = AlertPopup(MouseFilterEnum.Ignore);
        (_alertList, _alertListBox) = AlertPopup(MouseFilterEnum.Stop);
        return col;
    }

    private (PanelContainer, VBoxContainer) AlertPopup(MouseFilterEnum filter)
    {
        var panel = new PanelContainer { Visible = false, MouseFilter = filter, ZIndex = 50, CustomMinimumSize = new Vector2(300, 0) };
        panel.AddThemeStyleboxOverride("panel", Castle.Oak(16, 14));
        var box = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        box.AddThemeConstantOverride("separation", 6);
        panel.AddChild(box);
        AddChild(panel);
        return (panel, box);
    }

    private static Color LevelColor(AlertLevel l) => l switch
    {
        AlertLevel.Urgent => Ui.Bad,
        AlertLevel.Decision => Ui.Gold,
        _ => Ui.Ink2,
    };

    private List<Alert> StandingAlerts() => Alerts.Standing(_sim.Town);

    /// <summary>Answer an alert: its window, and the site, person or town it is about.</summary>
    private void Go(Alert a)
    {
        CloseAlertList();
        GuideEvent("alert");
        var t = _sim.Town;
        Tab? tab = TabOf(a.Kind.Window);
        if (a.Kind.Window == AlertWindow.Tourney)
        {
            OpenTourney();
            return;
        }
        if (a.StoryId is int story)
        {
            OpenStory(story);
            return;
        }
        if (a.FindId is int find)
        {
            OpenFind(find);
            return;
        }
        if (a.BuildingId is int bid && t.Buildings.FirstOrDefault(b => b.Id == bid) is { } mark)
        {
            Look(Geo.LotPos(t.Lot(mark.LotId)), 30);
            Select(Selection.Building(bid));
            return;
        }
        if (tab is Tab w)
        {
            OpenWindow(w);
            // The control the alert asks for glows in its window (AlertKind.Act), the trail's last step.
            if (a.Kind.Act != "")
            {
                // "Page>Item", "Item" or "Page>": the page opens now and the item is scrolled to and lit.
                var (page, item) = a.Kind.Act.Contains('>') ? (a.Kind.Act[..a.Kind.Act.IndexOf('>')], a.Kind.Act[(a.Kind.Act.IndexOf('>') + 1)..]) : ("", a.Kind.Act);
                if (page != "")
                {
                    _panes[w].Selected = page;
                    _dirty = true;
                }
                _trail = $"tab:{w}{(page == "" ? "" : ":" + page)}{(item == "" ? "" : ">" + item)}";
                _trailKey = $"alert:{a.Key}";
                _trailTab = w;
                _goalTarget = null;
            }
        }
        // A faction turning hostile is answered under Factions, a long scroll below the gods.
        if (a.Wall is int section && Garrison.Walled(t))
        {
            ShowWall(section);
            return;
        }
        if (a.SiteId is string site && _map.Geo.Pins.TryGetValue(site, out var pin))
        {
            Look(pin, 45);
            Select(Selection.Site(site));
        }
        else if (a.AgentId is int agent)
        {
            if (_map.Crowd.People.Sprites.TryGetValue(agent, out var sp) && sp.Visible) Look(sp.P, 22);
            Select(Selection.Agent(agent));
        }
        else if (a.AtTown) Look(Geo.Center, 40);
    }

    /// <summary>Take the player to what an entry is about: its window, and the site or person it names.</summary>
    private void GoTo(LedgerEntry e)
    {
        var t = _sim.Town;
        // A siege's news looks at the walls, where the map plays it out.
        if (e.Importance == Importance.Major && SiegeOf(e) is { } siege)
        {
            ShowSiege(siege);
            return;
        }
        Tab? tab = e.Category switch
        {
            LedgerCategory.War => Tab.War,
            LedgerCategory.Faith or LedgerCategory.Faction => Tab.Faith,
            LedgerCategory.Council => Tab.Council,
            LedgerCategory.Quest => Tab.Bounties,
            _ => null,
        };
        // The longest site name the entry mentions, so "the Old Mill" beats "the Mill".
        var site = t.Sites
            .Where(s => _map.Geo.Pins.ContainsKey(s.Def.Id) && e.Text.Contains(s.Def.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.Def.Name.Length).ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        var agent = e.Agents.Where(id => id >= 1 && id <= t.Agents.Count && _map.Crowd.People.Sprites.TryGetValue(id, out var sp) && sp.Visible).Cast<int?>().FirstOrDefault();
        if (tab is Tab w) OpenWindow(w);
        else if (site == null && agent == null) OpenWindow(Tab.Ledger);
        if (site != null)
        {
            Look(_map.Geo.Pins[site.Def.Id], 45);
            Select(Selection.Site(site.Def.Id));
        }
        else if (agent is int id)
        {
            Look(_map.Crowd.People.Sprites[id].P, 22);
            Select(Selection.Agent(id));
        }
    }

    private void Look(Vector2 p, float dist)
    {
        if (_map.Strategic) ToggleStrategic();
        _map.LookAt(p, dist);
    }

    /// <summary>Seconds a minor alert has left, or null for one that stays until resolved.</summary>
    private double? AlertLeft(Alert a)
    {
        if (a.Kind.Level != AlertLevel.Minor) return null;
        return _alertBorn.GetValueOrDefault(a.Key, _alertClock) + MinorSeconds - _alertClock;
    }

    /// <summary>Age the alerts, rebuild the strip when what it shows changes, and fade what is about to time out.</summary>
    private void SyncAlerts(double delta)
    {
        if (Speeds[_speed] > 0) _alertClock += delta;
        var standing = _standing;
        // A dismissed standing alert comes back only once its condition has cleared and returned.
        _dismissed.RemoveWhere(k => standing.All(a => a.Key != k));
        var all = standing.Where(a => !_dismissed.Contains(a.Key)).ToList();
        foreach (var a in all) _alertBorn.TryAdd(a.Key, _alertClock);
        foreach (var a in all)
            if (AlertLeft(a) <= 0) _dismissed.Add(a.Key);
        foreach (var k in _alertBorn.Keys.Where(k => all.All(a => a.Key != k)).ToList()) _alertBorn.Remove(k);

        var shown = all.Where(a => !_dismissed.Contains(a.Key)).ToList();
        string sig = string.Join("|", shown.Select(a => a.Key + a.Text));
        if (sig != _alertSig)
        {
            _alertSig = sig;
            RebuildAlerts(shown);
        }
        SyncNotices(shown, delta);

        // Popups follow their icon as the strip re-centres.
        if (_alertTip.Visible && IsInstanceValid(_tipAnchor)) PlaceAlertPopup(_alertTip, _tipAnchor!);
        if (_alertList.Visible && IsInstanceValid(_listAnchor)) PlaceAlertPopup(_alertList, _listAnchor!, beside: _openKind?.StartsWith("rail:", StringComparison.Ordinal) == true);
        SyncRailAlerts();

        // Minor groups fade out over their last seconds; urgent ones breathe.
        float pulse = 0.82f + 0.18f * Mathf.Sin((float)_clockReal * 4);
        foreach (var (kind, items, icon) in _alertGroups)
        {
            double left = items.Max(a => AlertLeft(a) ?? double.MaxValue);
            float alpha = left < FadeSeconds ? (float)Math.Max(0.15, left / FadeSeconds) : 1;
            icon.Modulate = new Color(1, 1, 1, kind.Level == AlertLevel.Urgent ? pulse : alpha);
            // A council motion over its last day flashes a red outline: the clock no longer stops for it.
            if (kind == Alerts.Motion && icon.GetThemeStylebox("panel") is StyleBoxFlat box)
            {
                bool closing = Council.PendingMotion(_sim.Town) is { } m && Council.Closing(_sim.Town, m);
                float flash = 0.5f + 0.5f * Mathf.Sin((float)_clockReal * 7);
                box.BorderColor = closing ? new Color(Ui.Bad, 0.35f + 0.65f * flash) : LevelColor(kind.Level);
                box.SetBorderWidthAll(closing ? 3 : 2);
            }
        }
    }

    private void RebuildAlerts(List<Alert> shown)
    {
        Ui.Clear(_alertRow);
        _alertGroups.Clear();
        string? tipKind = _alertTip.Visible ? _tipKind : null;
        _alertTip.Visible = false;
        var order = Alerts.Kinds.Select((k, i) => (k.Id, i)).ToDictionary(p => p.Id, p => p.i);
        var groups = shown.GroupBy(a => a.Kind.Id)
            .Select(g => (Kind: g.First().Kind, Items: g.ToList()))
            .OrderByDescending(g => g.Kind.Level).ThenBy(g => order.GetValueOrDefault(g.Kind.Id, 99)).ThenBy(g => g.Kind.Id, StringComparer.Ordinal);
        foreach (var (kind, items) in groups)
        {
            var icon = AlertIcon(kind, items);
            _alertRow.AddChild(icon);
            _alertGroups.Add((kind, items, icon));
        }
        // A summary or list that was open stays open over its kind's new icon.
        if (tipKind != null && _alertGroups.FirstOrDefault(g => g.Kind.Id == tipKind) is { Icon: not null } hovered)
            ShowAlertTip(hovered.Kind, hovered.Items, hovered.Icon);
        if (_openKind != null && _openKind.StartsWith("rail:", StringComparison.Ordinal))
        {
            if (int.TryParse(_openKind[5..], out int railGroup) && railGroup >= 0 && railGroup < Groups.Length) ShowRailAlerts(railGroup, _railButtons[railGroup]);
        }
        else if (_openKind != null)
        {
            var open = _alertGroups.FirstOrDefault(g => g.Kind.Id == _openKind);
            if (open.Icon == null || open.Items.Count < 2) CloseAlertList();
            else ShowAlertList(open.Kind, open.Items, open.Icon);
        }
        UpdateBadges();
    }

    private Control AlertIcon(AlertKind kind, List<Alert> items)
    {
        var color = LevelColor(kind.Level);
        var chip = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, CustomMinimumSize = new Vector2(40, 40) };
        var style = Glass(9);
        style.BorderColor = color;
        style.SetBorderWidthAll(kind.Level == AlertLevel.Minor ? 1 : 2);
        if (kind.Level == AlertLevel.Urgent) style.BgColor = new Color(0.32f, 0.09f, 0.07f, 0.96f);
        chip.AddThemeStyleboxOverride("panel", style);
        chip.AddChild(new TextureRect
        {
            Texture = Ui.Icon(kind.Icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(24, 24),
            SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        if (items.Count > 1)
        {
            // The count sits on the top-right corner, outside the container's layout.
            var badge = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            var minor = kind.Level == AlertLevel.Minor;
            badge.AddThemeStyleboxOverride("panel", Ui.Box(minor ? Ui.Panel2 : color, minor ? Ui.Ink2 : new Color(0, 0, 0, 0.6f), 99, 1, 6, 0));
            badge.AddChild(Ui.Label(items.Count.ToString(), minor ? Ui.Ink : Colors.White, Ui.Bold, 12));
            var holder = new Control { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsHorizontal = SizeFlags.ShrinkEnd, SizeFlagsVertical = SizeFlags.ShrinkBegin };
            holder.AddChild(badge);
            badge.Position = new Vector2(-6, -9);
            chip.AddChild(holder);
        }
        chip.MouseEntered += () => ShowAlertTip(kind, items, chip);
        chip.MouseExited += () => _alertTip.Visible = false;
        chip.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left) PressAlert(kind, items, chip);
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                foreach (var a in items) _dismissed.Add(a.Key);
                if (_openKind == kind.Id) CloseAlertList();
            }
            else return;
            chip.AcceptEvent();
        };
        return chip;
    }

    /// <summary>A single alert is answered at once; a kind with several opens the list to pick from.</summary>
    private void PressAlert(AlertKind kind, List<Alert> items, Control chip)
    {
        _alertTip.Visible = false;
        if (items.Count == 1) Go(items[0]);
        else if (_openKind == kind.Id) CloseAlertList();
        else ShowAlertList(kind, items, chip);
    }

    private void ShowAlertTip(AlertKind kind, List<Alert> items, Control chip)
    {
        if (_openKind == kind.Id) return;
        _tipKind = kind.Id;
        Ui.Clear(_alertTipBox);
        _alertTipBox.AddChild(AlertHeading(kind, items.Count));
        foreach (var a in items.Take(5))
        {
            var item = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            item.AddThemeConstantOverride("separation", 1);
            item.AddChild(Ui.Label(a.Text, Ui.Ink, Ui.Medium, 13));
            // The tip a sentence a line, so it can be skimmed.
            var tip = Ui.Rich(Ui.C(Ui.Ink2, Ui.Points(Ui.Esc(a.Tip))), 12);
            tip.MouseFilter = MouseFilterEnum.Ignore;
            tip.CustomMinimumSize = new Vector2(300, 0);
            item.AddChild(tip);
            if (AlertFace(a, 40) is { } face)
            {
                var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                line.AddThemeConstantOverride("separation", 10);
                line.AddChild(face);
                line.AddChild(item);
                _alertTipBox.AddChild(line);
            }
            else _alertTipBox.AddChild(item);
        }
        if (items.Count > 5) _alertTipBox.AddChild(Ui.Label($"…and {items.Count - 5} more", Ui.Ink2, Ui.Body, 12));
        string hint = items.Count == 1
            ? kind.Level == AlertLevel.Minor ? "Click: go there · Right-click: dismiss · fades" : "Click: go there · Right-click: dismiss"
            : "Click: list · Right-click: dismiss all";
        _alertTipBox.AddChild(Ui.Label(hint, new Color(Ui.Ink2, 0.75f), Ui.Body, 11));
        _tipAnchor = chip;
        PlaceAlertPopup(_alertTip, chip);
    }

    private void ShowAlertList(AlertKind kind, List<Alert> items, Control chip)
    {
        _openKind = kind.Id;
        Ui.Clear(_alertListBox);
        _alertListBox.AddChild(AlertHeading(kind, items.Count));
        foreach (var a in items) _alertListBox.AddChild(AlertRow(a, 28));
        _alertListBox.AddChild(Ui.Label("Click to go there · right-click to dismiss", new Color(Ui.Ink2, 0.75f), Ui.Body, 11));
        _listAnchor = chip;
        PlaceAlertPopup(_alertList, chip);
    }

    /// <summary>
    /// Hovering a rail button lists the alerts waiting at its window, most pressing first: each with its portrait or
    /// icon, its line in the colour of its level and the reason under it. Click one to go there, right-click to dismiss.
    /// The list stays while the mouse is on the button or the list.
    /// </summary>
    private void ShowRailAlerts(int g, Control button)
    {
        var items = ShownAlerts.Where(a => RailGroupOf(a) == g)
            .OrderByDescending(a => a.Kind.Level).ThenBy(a => a.Key, StringComparer.Ordinal).ToList();
        if (items.Count == 0)
        {
            if (_openKind == $"rail:{g}") CloseAlertList();
            return;
        }
        _alertTip.Visible = false;
        _openKind = $"rail:{g}";
        Ui.Clear(_alertListBox);
        var top = items.Max(a => a.Kind.Level);
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 8);
        head.AddChild(new TextureRect { Texture = Ui.Icon(Groups[g].Icon), Modulate = Groups[g].Hue, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        head.AddChild(Ui.Label(Groups[g].Name, LevelColor(top), Ui.Bold, 14));
        _alertListBox.AddChild(head);
        // One row per kind of alert, the most pressing first, and no more than RailRows of them.
        var families = Families(items);
        foreach (var f in families.Take(RailRows)) _alertListBox.AddChild(AlertRow(f.First(), 28, withTip: true, family: f));
        int more = families.Skip(RailRows).Sum(f => f.Count);
        if (more > 0) _alertListBox.AddChild(Ui.Label($"…and {more} more in the window", Ui.Ink2, Ui.Body, 12));
        _alertListBox.AddChild(Ui.Label("Click: go there · Right-click: dismiss", new Color(Ui.Ink2, 0.75f), Ui.Body, 11));
        _listAnchor = button;
        PlaceAlertPopup(_alertList, button, beside: true);
    }

    /// <summary>Close the rail's list once the mouse has left both the button and the list.</summary>
    private void SyncRailAlerts()
    {
        if (_railPinned || _openKind == null || !_openKind.StartsWith("rail:", StringComparison.Ordinal) || !IsInstanceValid(_listAnchor)) return;
        var m = GetViewport().GetMousePosition();
        if (_listAnchor!.GetGlobalRect().Grow(6).HasPoint(m) || _alertList.GetGlobalRect().Grow(10).HasPoint(m)) return;
        // The gap between the button and the list counts as on it, so the mouse can cross over.
        var a = _listAnchor.GetGlobalRect();
        var bridge = new Rect2(a.End.X, a.Position.Y - 8, Math.Max(0, _alertList.GlobalPosition.X - a.End.X), a.Size.Y + 16);
        if (bridge.HasPoint(m)) return;
        CloseAlertList();
    }

    /// <summary>One alert in a list: its face or icon, its line, optionally its reason; click to go, right-click to dismiss.</summary>
    private Control AlertRow(Alert a, float faceSize, bool withTip = false, List<Alert>? family = null)
    {
        if (family is { Count: > 1 }) a = a with { Text = $"{a.Text}  ·  {family.Count}" };
        var kind = a.Kind;
        {
            var row = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, TooltipText = withTip ? "" : $"{a.Tip}\nClick to go there · right-click to dismiss" };
            var normal = Ui.Box(Ui.Panel, Ui.Line, 6, 1, 8, 5);
            var hover = Ui.Box(Ui.Panel2, LevelColor(kind.Level), 6, 1, 8, 5);
            row.AddThemeStyleboxOverride("panel", normal);
            row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hover);
            row.MouseExited += () => row.AddThemeStyleboxOverride("panel", normal);
            Control label;
            if (withTip)
            {
                var words = Ui.Rich($"{Ui.C(kind.Level == AlertLevel.Minor ? Ui.Ink : LevelColor(kind.Level), $"[b]{Ui.Esc(a.Text)}[/b]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Points(Ui.Esc(a.Tip))}[/font_size]")}", 13);
                words.CustomMinimumSize = new Vector2(300, 0);
                label = words;
            }
            else label = Ui.Label(a.Text, Ui.Ink, Ui.Medium, 13);
            label.MouseFilter = MouseFilterEnum.Ignore;
            Control? face = AlertFace(a, faceSize);
            if (face == null && withTip)
                face = new TextureRect { Texture = Ui.Icon(kind.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(faceSize, faceSize), SizeFlagsVertical = SizeFlags.ShrinkBegin, Modulate = LevelColor(kind.Level), MouseFilter = MouseFilterEnum.Ignore };
            if (face != null)
            {
                var line = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
                line.AddThemeConstantOverride("separation", 8);
                line.AddChild(face);
                if (!withTip) label.SizeFlagsVertical = SizeFlags.ShrinkCenter;
                line.AddChild(label);
                row.AddChild(line);
            }
            else row.AddChild(label);
            row.GuiInput += ev =>
            {
                if (ev is not InputEventMouseButton { Pressed: true } mb) return;
                if (mb.ButtonIndex == MouseButton.Left) Go(a);
                else if (mb.ButtonIndex == MouseButton.Right) _dismissed.UnionWith((family ?? [a]).Select(x => x.Key));
                else return;
                row.AcceptEvent();
            };
            return row;
        }
    }

    /// <summary>Alerts gathered by kind, the most pressing kind first and each kind's own alerts in key order.</summary>
    internal static List<List<Alert>> Families(IEnumerable<Alert> alerts) =>
        alerts.GroupBy(a => a.Kind.Id)
            .Select(g => g.OrderBy(a => a.Key, StringComparer.Ordinal).ToList())
            .OrderByDescending(f => f[0].Kind.Level).ThenBy(f => f[0].Kind.Id, StringComparer.Ordinal).ToList();

    /// <summary>The portrait of the enemy or the site an alert is about, if it is about one.</summary>
    private Portrait? AlertFace(Alert a, float size)
    {
        var t = _sim.Town;
        if (a.StoryId is int sid && Stories.Find(t, sid) is { } st)
        {
            if (st.MenaceId is int mid && mid >= 1 && mid <= t.Saga.Menaces.Count) return Foes.Of(t, t.Saga.Menaces[mid - 1], size);
            if (st.Warband) return Foes.Warband(t, size);
            return null;
        }
        if (a.Kind.Id is "siege" or "sighted" && a.SiteId == null) return Foes.Warband(t, size);
        return a.SiteId is string site && Quests.Site(t, site) is { Known: not SiteKnowledge.Unheard } s ? Foes.Of(t, s, size) : null;
    }

    private static Control AlertHeading(AlertKind kind, int count)
    {
        var head = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        head.AddThemeConstantOverride("separation", 8);
        head.AddChild(new TextureRect { Texture = Ui.Icon(kind.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        head.AddChild(Ui.Label(count > 1 ? $"{kind.Title} ({count})" : kind.Title, LevelColor(kind.Level), Ui.Bold, 14));
        string level = kind.Level switch { AlertLevel.Urgent => "URGENT", AlertLevel.Decision => "NEEDS YOU", _ => "" };
        if (level != "")
        {
            var tag = Ui.Label(level, new Color(LevelColor(kind.Level), 0.8f), Ui.Bold, 10);
            tag.SizeFlagsHorizontal = SizeFlags.ExpandFill | SizeFlags.ShrinkEnd;
            tag.HorizontalAlignment = HorizontalAlignment.Right;
            head.AddChild(tag);
        }
        return head;
    }

    /// <summary>Under the icon, kept on screen.</summary>
    private void PlaceAlertPopup(PanelContainer popup, Control chip, bool beside = false)
    {
        popup.Visible = true;
        var size = popup.GetCombinedMinimumSize();
        if (popup.Size != size) popup.Size = size;
        var view = GetViewportRect().Size;
        if (beside)
        {
            // To the right of a rail button, kept on screen top and bottom.
            var by = chip.GlobalPosition + new Vector2(chip.Size.X + 8, -6);
            popup.Position = new Vector2(by.X, Math.Clamp(by.Y, 8, Math.Max(8, view.Y - size.Y - 8)));
        }
        else
        {
            var at = chip.GlobalPosition + new Vector2(chip.Size.X / 2 - size.X / 2, chip.Size.Y + 8);
            popup.Position = new Vector2(Math.Clamp(at.X, 8, view.X - size.X - 8), at.Y);
        }
        if (popup.GetIndex() != GetChildCount() - 1) MoveChild(popup, GetChildCount() - 1);
    }

    private void CloseAlertList()
    {
        _openKind = null;
        _alertList.Visible = false;
    }

    /// <summary>A click anywhere outside the open list (or the build bar) closes it.</summary>
    public override void _Input(InputEvent e)
    {
        if (e is not InputEventMouseButton { Pressed: true } mb) return;
        ClickOffBuildBar(mb);
        if (_openKind == null) return;
        var p = mb.Position;
        if (_alertList.GetGlobalRect().HasPoint(p) || _alertRow.GetGlobalRect().HasPoint(p)) return;
        CloseAlertList();
    }

    /// <summary>
    /// --alert N presses the Nth icon as a player would, and --alert-hover N shows its summary: as soon as there is one,
    /// or with --frames over 200, in the last 90 frames before the shot so it catches the strip as it then stands.
    /// </summary>
    private void ScriptedAlert()
    {
        if (_opts.Frames > 200 && _frames < _opts.Frames - 90) return;
        if (_opts.Alert is int i && i < _alertGroups.Count)
        {
            _opts.Alert = null;
            Go(_alertGroups[i].Items[0]);
        }
        // --alert-hover N: the list of the Nth rail button that has alerts, top to bottom.
        var railed = Enumerable.Range(0, Groups.Length).Where(g => ShownAlerts.Any(a => RailGroupOf(a) == g)).ToList();
        if (_opts.AlertHover is int h && h < railed.Count && _railButtons[railed[h]].Size.X > 0)
        {
            ShowRailAlerts(railed[h], _railButtons[railed[h]]);
            _railPinned = true;
            _opts.AlertHover = null;
        }
    }

    private void ClearAlerts()
    {
        _standing = [];
        _dismissed.Clear();
        _alertBorn.Clear();
        _alertSig = "";
        CloseAlertList();
        ClearNotices();
    }
}
