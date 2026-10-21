using CrownAndCompass.Sim.Systems;
using Godot;
using Alert = CrownAndCompass.Sim.Systems.Alert;

namespace CrownAndCompass.Client;

/// <summary>
/// Notices that slide out of the rail beside a window's button when something new lands there: the kind's icon and
/// title in its level's colour. Click one to answer it, right-click to tuck it away; otherwise it tucks back into the
/// badge after a few seconds. They read the same alerts as the strip and the badges, so they add no news of their
/// own: only an urgent or decision alert's arrival pops (minor ones just bump the badge), each tab holds one tile
/// (a second arrival there counts on it), at most <see cref="NoticeMost"/> show with the rest waiting as "+N", and
/// none slide out while a window is open. An alert that flickers off and on within <see cref="NoticeForget"/>
/// seconds does not pop again.
/// </summary>
public partial class Main
{
    private const int NoticeMost = 3;
    private const double NoticeSeconds = 5, NoticeForget = 90;

    private sealed class Notice
    {
        public required Tab Tab;
        public required Alert Alert;
        public required PanelContainer Tile;
        public required Label Count;
        public int Arrived = 1;
        public double Left = NoticeSeconds;
        public bool Hovered, Leaving;
    }

    private readonly List<Notice> _notices = [];
    private readonly List<(Tab Tab, Alert Alert)> _noticeQueue = [];
    /// <summary>When each alert was last on the strip, by key, so one that blinks off and back does not pop twice.</summary>
    private readonly Dictionary<string, double> _noticeSeen = [];
    private double _noticeFrom = -1;
    private Label? _noticeMore;

    /// <summary>Queue the alerts that just arrived, then slide out, age and tuck away the tiles.</summary>
    private void SyncNotices(List<Alert> shown, double delta)
    {
        // What stands when a town opens is not news: only what arrives after it pops.
        if (_noticeFrom < 0) _noticeFrom = _clockReal;
        bool primed = _clockReal - _noticeFrom > 2;
        if (_opts.Notices is int at && _frames >= at)
        {
            _opts.Notices = null;
            _noticeSeen.Clear();
        }
        foreach (var a in shown)
        {
            bool fresh = !_noticeSeen.ContainsKey(a.Key);
            _noticeSeen[a.Key] = _clockReal;
            if (fresh && primed && a.Kind.Level != AlertLevel.Minor && TabOf(a.Kind.Window) is Tab t)
                _noticeQueue.Add((t, a));
        }
        foreach (var k in _noticeSeen.Where(p => _clockReal - p.Value > NoticeForget).Select(p => p.Key).ToList()) _noticeSeen.Remove(k);
        var live = shown.Select(a => a.Key).ToHashSet();
        _noticeQueue.RemoveAll(q => !live.Contains(q.Alert.Key) || _window.Visible && _windowTab == q.Tab);

        bool held = _window.Visible || _title.Visible;
        foreach (var n in _notices.Where(n => !n.Leaving).ToList())
        {
            if (held || !live.Contains(n.Alert.Key)) TuckNotice(n);
            else if (!n.Hovered && (n.Left -= delta) <= 0) TuckNotice(n, true);
        }
        if (!held)
            while (_noticeQueue.Count > 0)
            {
                var (tab, alert) = _noticeQueue[0];
                if (_notices.FirstOrDefault(n => n.Tab == tab && !n.Leaving) is { } same)
                {
                    // A second arrival in the same window counts on its tile and keeps it out a while longer.
                    same.Arrived++;
                    same.Count.Text = $"×{same.Arrived}";
                    same.Count.Visible = true;
                    same.Left = NoticeSeconds;
                    if (alert.Kind.Level >= same.Alert.Kind.Level) same.Alert = alert;
                }
                else if (_notices.Count(n => !n.Leaving) < NoticeMost) _notices.Add(SlideNotice(tab, alert));
                else break;
                _noticeQueue.RemoveAt(0);
            }

        // What waits behind the full stack, beside its lowest tile.
        int more = _noticeQueue.Select(q => q.Tab).Distinct().Count();
        var last = _notices.Where(n => !n.Leaving).MaxBy(n => n.Tile.Position.Y);
        _noticeMore ??= MoreLabel();
        _noticeMore.Visible = more > 0 && last != null;
        if (_noticeMore.Visible)
        {
            _noticeMore.Text = $"+{more}";
            _noticeMore.Position = last!.Tile.Position + new Vector2(last.Tile.Size.X + 6, (last.Tile.Size.Y - _noticeMore.Size.Y) / 2);
        }
    }

    private Label MoreLabel()
    {
        var l = Ui.Label("", Ui.Gold, Ui.Bold, 13);
        l.AddThemeColorOverride("font_outline_color", new Color(0, 0, 0, 0.8f));
        l.AddThemeConstantOverride("outline_size", 4);
        l.MouseFilter = MouseFilterEnum.Ignore;
        AddChild(l);
        return l;
    }

    /// <summary>A tile out of the rail beside the tab's button: the kind's icon and title in its level's colour.</summary>
    private Notice SlideNotice(Tab tab, Alert a)
    {
        var color = LevelColor(a.Kind.Level);
        var tile = new PanelContainer { MouseFilter = MouseFilterEnum.Stop, MouseDefaultCursorShape = CursorShape.PointingHand, ZIndex = 20 };
        var style = Glass(8, 10, 6);
        style.BorderColor = color;
        style.SetBorderWidthAll(2);
        style.BorderWidthLeft = 4;
        if (a.Kind.Level == AlertLevel.Urgent) style.BgColor = new Color(0.32f, 0.09f, 0.07f, 0.96f);
        tile.AddThemeStyleboxOverride("panel", style);
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        row.AddChild(new TextureRect
        {
            Texture = Ui.Icon(a.Kind.Icon),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            CustomMinimumSize = new Vector2(20, 20),
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            MouseFilter = MouseFilterEnum.Ignore,
        });
        var title = Ui.Label(a.Kind.Title, a.Kind.Level == AlertLevel.Urgent ? Colors.White : Ui.Ink, Ui.Bold, 13);
        title.MouseFilter = MouseFilterEnum.Ignore;
        title.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        row.AddChild(title);
        var count = Ui.Label("", color, Ui.Bold, 13);
        count.MouseFilter = MouseFilterEnum.Ignore;
        count.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        count.Visible = false;
        row.AddChild(count);
        tile.AddChild(row);
        AddChild(tile);

        var n = new Notice { Tab = tab, Alert = a, Tile = tile, Count = count };
        tile.TooltipText = $"{Titles[tab]}\n• {a.Text}";
        tile.MouseEntered += () => n.Hovered = true;
        tile.MouseExited += () => n.Hovered = false;
        tile.GuiInput += ev =>
        {
            if (ev is not InputEventMouseButton { Pressed: true } mb) return;
            if (mb.ButtonIndex == MouseButton.Left)
            {
                TuckNotice(n);
                Go(n.Alert);
            }
            else if (mb.ButtonIndex == MouseButton.Right) TuckNotice(n, true);
            else return;
            tile.AcceptEvent();
        };

        // Out from behind the rail, level with the button.
        var button = _tabButtons[tab];
        tile.Size = tile.GetCombinedMinimumSize();
        float y = button.GlobalPosition.Y + (button.Size.Y - tile.Size.Y) / 2;
        tile.Position = new Vector2(RailWidth - tile.Size.X * 0.5f, y);
        tile.Modulate = new Color(1, 1, 1, 0);
        var tw = tile.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(tile, "position:x", RailWidth + 6f, 0.35);
        tw.TweenProperty(tile, "modulate:a", 1.0, 0.2);
        return n;
    }

    /// <summary>Slide a tile back into the rail; when it had its say unanswered, the badge it leaves behind gives a nudge.</summary>
    private void TuckNotice(Notice n, bool intoBadge = false)
    {
        if (n.Leaving) return;
        n.Leaving = true;
        n.Tile.MouseFilter = MouseFilterEnum.Ignore;
        var tw = n.Tile.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tw.TweenProperty(n.Tile, "position:x", RailWidth - n.Tile.Size.X * 0.5f, 0.25);
        tw.TweenProperty(n.Tile, "modulate:a", 0.0, 0.25);
        tw.Chain().TweenCallback(Callable.From(() =>
        {
            _notices.Remove(n);
            n.Tile.QueueFree();
            if (intoBadge && _badges.TryGetValue(n.Tab, out var b) && b.Badge.Visible)
            {
                b.Badge.PivotOffset = b.Badge.Size / 2;
                var pop = b.Badge.CreateTween();
                pop.TweenProperty(b.Badge, "scale", new Vector2(1.6f, 1.6f), 0.12);
                pop.TweenProperty(b.Badge, "scale", Vector2.One, 0.25);
            }
        }));
    }

    private void ClearNotices()
    {
        foreach (var n in _notices) n.Tile.QueueFree();
        _notices.Clear();
        _noticeQueue.Clear();
        _noticeSeen.Clear();
        _noticeFrom = -1;
        if (_noticeMore != null) _noticeMore.Visible = false;
    }
}
