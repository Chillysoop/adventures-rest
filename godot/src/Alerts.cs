using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The alert strip under the top bar: what needs the player now. Standing alerts (a motion waiting, the
/// warband on the march, people sleeping rough) last while the condition does; event alerts come from the
/// Ledger's big news and fade. A click takes the player where the alert can be answered: the window that
/// handles it and, when it happened somewhere, the place on the map. Right-click dismisses.
/// </summary>
public partial class Main
{
    private sealed record Alert(string Key, string Icon, string Text, string Tip, Color Color, Action Go);

    private const int MaxAlerts = 4;
    private const double EventAlertSeconds = 45;

    private HBoxContainer _alertRow = null!;
    private readonly List<(Alert Alert, double Until)> _eventAlerts = [];
    private readonly HashSet<string> _dismissed = [];
    private string _alertSig = "";
    private List<Alert> _standing = [];
    private readonly List<Alert> _shownAlerts = [];

    /// <summary>The top-centre column: the alert strip, then the paused pill under it.</summary>
    private VBoxContainer BuildAlerts()
    {
        var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        AddChild(col);
        col.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        col.OffsetTop = TopBar + 10;
        // Between the management window on the left and the news feed on the right.
        col.OffsetLeft = 480;
        col.OffsetRight = -360;
        _alertRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        _alertRow.AddThemeConstantOverride("separation", 8);
        col.AddChild(_alertRow);
        return col;
    }

    private List<Alert> StandingAlerts()
    {
        var t = _sim.Town;
        var s = _sim.Snapshot();
        var list = new List<Alert>();
        if (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at)
        {
            int days = (int)Math.Ceiling((at - t.Tick) / 24.0);
            list.Add(new("march", "warband", $"Siege in {Ui.Days(days)}", "The warband is marching on the town. Man the walls and keep heroes home.", Ui.Bad,
                () => { OpenWindow(Tab.War); Look(Geo.Center, 55); }));
        }
        else if (t.Enemy.Stage == ThreatStage.Raids)
            list.Add(new("raids", "warband", "Raiders by night", "The warband raids the town when its heroes are away. Build walls, hire the watch, clear the war camp.", Ui.Bad,
                () => OpenWindow(Tab.War)));
        if (Council.PendingMotion(t) is { } m)
            list.Add(new($"motion:{m.Id}", "mandate", "A motion awaits you", Ui.Cap(m.Text), Ui.Gold, () => OpenWindow(Tab.Council)));
        if (City.NextTier(t) is { } charter && City.Missing(t).Count == 0)
            list.Add(new($"charter:{charter.Id}", "favour", $"Claim the charter of a {charter.Name.ToLowerInvariant()}", "The town has all the charter asks for. Seal it in the City window to open new wards and buildings.", Ui.Good,
                () => OpenWindow(Tab.City)));
        if (s.Homeless > 0)
            list.Add(new("homeless", "homeless", $"{s.Homeless} homeless", "Townsfolk with no roof lose heart and leave. More homes are needed.", Ui.Gold,
                () => { OpenWindow(Tab.Council); Look(Geo.Center, 40); }));
        if (t.Mandate < 30)
            list.Add(new("mandate", "mandate", "The town is turning on you", $"Mandate {Math.Round(t.Mandate)}. Below this the council stops listening.", Ui.Bad, () => OpenWindow(Tab.Council)));
        if (t.Treasury < 25)
            list.Add(new("treasury", "gold", "The coffers are empty", $"{Math.Floor(t.Treasury)}g left. Raise taxes or hold back on bounties.", Ui.Bad, () => OpenWindow(Tab.Council)));
        foreach (var f in t.Factions.Where(f => f.Active && f.Known && f.Standing <= -30).OrderBy(f => f.Id, StringComparer.Ordinal))
            list.Add(new($"faction:{f.Id}", "suspicion", $"{Ui.Cap(f.Name)} turns hostile", "A faction in town is working against you. Crack down, or win it back.", Ui.Bad, () => OpenWindow(Tab.Faith)));
        return list;
    }

    /// <summary>Big news becomes an alert that fades on its own; the feed still carries everything newsworthy.</summary>
    private void AlertFor(LedgerEntry e)
    {
        if (e.Importance != Importance.Major && !(e.Category == LedgerCategory.War && e.Importance == Importance.Notable)) return;
        string icon = e.Category switch
        {
            LedgerCategory.War => "warband",
            LedgerCategory.Quest => "quest",
            LedgerCategory.Faith => "faith",
            LedgerCategory.Faction => "standing",
            LedgerCategory.Council => "mandate",
            _ => "alert",
        };
        string text = e.Text.Length > 34 ? e.Text[..32].TrimEnd(' ', ',', '.') + "…" : e.Text;
        var color = e.Category == LedgerCategory.War ? Ui.Bad : Ui.Gold;
        _eventAlerts.Add((new($"entry:{e.Id}", icon, Ui.Cap(text), e.Text, color, () => GoTo(e)), _clockReal + EventAlertSeconds));
        // At high speed news piles up; keep the freshest few, the Ledger keeps the rest.
        if (_eventAlerts.Count > 4) _eventAlerts.RemoveAt(0);
        _alertSig = "";
    }

    /// <summary>Take the player to what an entry is about: its window, and the site or person it names.</summary>
    private void GoTo(LedgerEntry e)
    {
        var t = _sim.Town;
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

    /// <summary>Rebuild the strip only when what it shows changes; ages event alerts every frame.</summary>
    private void SyncAlerts()
    {
        _eventAlerts.RemoveAll(a => a.Until <= _clockReal);
        var standing = _standing;
        _dismissed.RemoveWhere(k => !k.StartsWith("entry:") && standing.All(a => a.Key != k));
        var shown = standing.Concat(_eventAlerts.AsEnumerable().Reverse().Select(a => a.Alert))
            .Where(a => !_dismissed.Contains(a.Key)).ToList();
        string sig = string.Join("|", shown.Select(a => a.Key + a.Text));
        if (sig == _alertSig) return;
        _alertSig = sig;
        foreach (var c in _alertRow.GetChildren()) c.Free();
        _shownAlerts.Clear();
        // As many chips as fit between the window and the feed; the rest wait behind a count.
        float room = GetViewportRect().Size.X - 840 - 40;
        int n = 0;
        foreach (var a in shown.Take(MaxAlerts))
        {
            float w = Ui.Medium.GetStringSize(a.Text, HorizontalAlignment.Left, -1, 13).X + 18 + 6 + 23 + 8;
            if (w > room) break;
            room -= w;
            _alertRow.AddChild(AlertChip(a));
            _shownAlerts.Add(a);
            n++;
        }
        if (shown.Count > n)
        {
            var more = Ui.Label($"+{shown.Count - n}", Ui.Ink, Ui.Bold, 12);
            more.TooltipText = "More alerts: right-click one to dismiss it.";
            more.MouseFilter = MouseFilterEnum.Stop;
            _alertRow.AddChild(more);
        }
    }

    /// <summary>--alert N: press the Nth alert as soon as there is one, as a player would.</summary>
    private void ScriptedAlert()
    {
        if (_opts.Alert is int i && i < _shownAlerts.Count)
        {
            _opts.Alert = null;
            _shownAlerts[i].Go();
        }
    }

    private Control AlertChip(Alert a)
    {
        var chip = new PanelContainer { TooltipText = $"{a.Tip}\nClick to go there · right-click to dismiss", MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand };
        var style = Glass(8, 10, 5);
        style.BorderColor = a.Color;
        style.BorderWidthLeft = 3;
        chip.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 6);
        row.AddChild(new TextureRect { Texture = Ui.Icon(a.Icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(18, 18), SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore });
        var label = Ui.Label(a.Text, Ui.Ink, Ui.Medium, 13);
        label.MouseFilter = MouseFilterEnum.Ignore;
        row.AddChild(label);
        chip.AddChild(row);
        chip.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left)
            {
                a.Go();
                GuideEvent("alert");
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _dismissed.Add(a.Key);
                _alertSig = "";
            }
            else return;
            chip.AcceptEvent();
        };
        return chip;
    }

    private void ClearAlerts()
    {
        _eventAlerts.Clear();
        _standing = [];
        _dismissed.Clear();
        _alertSig = "";
    }
}
