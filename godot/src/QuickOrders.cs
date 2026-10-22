using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The quick orders over the corner map, always on screen so the town can be mobilised in a click: Defend mans the gate
/// and every side a host marches from (or stands them down), Guard plants a flag over the farm raiders were seen heading
/// for or puts the flag in hand, Strike lists the foes the town knows with a bounty or a sellsword march for each, and
/// Explore and Patrol pick up the wilds brushes to paint ground for companies to walk or riders to keep. A button turns
/// red while it is needed.
/// </summary>
public partial class Main
{
    private Button _defendButton = null!, _guardButton = null!, _strikeButton = null!, _scoutButton = null!, _patrolButton = null!;
    private Control _ordersBar = null!;
    private PanelContainer _strikePanel = null!;
    private Pane _strikePane = null!;
    private Label _ordersHint = null!;
    private double _hintUntil;

    private static Button OrderButton(string icon, string text)
    {
        var b = new Button
        {
            Text = text,
            Icon = Ui.Icon(icon),
            ToggleMode = true,
            FocusMode = FocusModeEnum.None,
            ExpandIcon = false,
            IconAlignment = HorizontalAlignment.Center,
            VerticalIconAlignment = VerticalAlignment.Top,
            CustomMinimumSize = new Vector2(56, 54),
        };
        b.AddThemeConstantOverride("icon_max_width", 22);
        b.AddThemeFontSizeOverride("font_size", 12);
        return b;
    }

    private Control BuildOrders()
    {
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        box.AddThemeConstantOverride("separation", 4);

        _strikePanel = new PanelContainer { Visible = false, CustomMinimumSize = new Vector2(380, 0), SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _strikePanel.AddThemeStyleboxOverride("panel", Castle.Oak(14, 12));
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        _strikePanel.AddChild(list);
        _strikePane = new Pane(list);
        box.AddChild(_strikePanel);

        _ordersHint = Ui.Label("", Ui.Gold, Ui.Bold, 13);
        _ordersHint.HorizontalAlignment = HorizontalAlignment.Right;
        _ordersHint.Visible = false;
        box.AddChild(_ordersHint);

        var bar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        bar.AddThemeConstantOverride("separation", 4);
        _defendButton = OrderButton("order-defend", "Defend");
        _defendButton.Pressed += QuickDefend;
        _guardButton = OrderButton("order-guard", "Guard");
        _guardButton.Pressed += QuickGuard;
        _strikeButton = OrderButton("order-strike", "Strike");
        _strikeButton.Pressed += () => ShowStrike(!_strikePanel.Visible);
        _scoutButton = OrderButton("act-explore", "Explore");
        _scoutButton.Pressed += () => ToggleBrush(Wilds.Explore);
        _patrolButton = OrderButton("order-patrol", "Patrol");
        _patrolButton.Pressed += () => ToggleBrush(Wilds.Patrol);
        foreach (var b in new[] { _defendButton, _guardButton, _strikeButton, _scoutButton, _patrolButton }) bar.AddChild(b);
        _ordersBar = bar;
        box.AddChild(bar);
        _map.WildsPainted += OnWildsPainted;
        return box;
    }

    // ---------- Defend ----------

    /// <summary>Calls to arms the player (not the stand-in mayor) has standing on the walls, towers and gate.</summary>
    private List<GuardPost> WallOrders() => _sim.Town.Garrison.Posts.Where(p => p.Kind != PostKind.Flag).OrderBy(p => p.Id).ToList();

    /// <summary>Why Defend can't do anything now, or null.</summary>
    private string? DefendBlocked() => WallOrders().Count > 0 ? null : Garrison.CallBlocked(_sim.Town, PostKind.Gate, 0);

    /// <summary>A host is on its way and a side it comes from (or the gate) has nobody called to it.</summary>
    private bool DefendNeeded() => Garrison.Threatened(_sim.Town).Count > 0 && DefendBlocked() == null && !Garrison.WallsManned(_sim.Town);

    private void QuickDefend()
    {
        var t = _sim.Town;
        if (Garrison.WallsManned(t) && WallOrders().Count > 0)
        {
            foreach (var p in WallOrders()) _sim.StandDown(p.Id);
            Hint("The watch stands down and goes back to its beats.");
        }
        else if (_sim.ManTheWalls() > 0)
        {
            var sides = Garrison.Threatened(t).Distinct().Where(s => s != 0).Order().Select(s => $"the {t.Content.Garrison.Sections[s]} wall").ToList();
            Hint(sides.Count == 0 ? "The horn sounds: the watch runs to the gate." : $"The horn sounds: the watch runs to the gate and {string.Join(" and ", sides)}.");
        }
        else Hint($"Can't sound the horn: {DefendBlocked() ?? "nobody answers"}.");
        GuideEvent("orders");
        _dirty = true;
    }

    // ---------- Guard ----------

    /// <summary>A building raiders were seen heading for that no flag covers yet: the first thing a flag should go over.</summary>
    private Building? Raided()
    {
        var t = _sim.Town;
        return t.Sightings
            .Where(s => s.Kind == Sightings.Band && s.UntilTick > t.Tick && s.TargetId != null)
            .OrderBy(s => s.Id)
            .Select(s => t.Buildings.FirstOrDefault(b => b.Id == s.TargetId))
            .FirstOrDefault(b => b != null && !Garrison.Covered(t, b));
    }

    private void QuickGuard()
    {
        var t = _sim.Town;
        if (!_map.Flagging && Raided() is { } b && Garrison.FlagBlocked(t, t.Lot(b.LotId).X, t.Lot(b.LotId).Y) == null)
        {
            // Raiders are on their way to a farm: the flag goes straight over it.
            var lot = t.Lot(b.LotId);
            var post = _sim.PlantFlag(lot.X, lot.Y, _flagGuards);
            if (_map.Strategic) ToggleStrategic();
            Look(Geo.LotPos(lot), 26);
            Hint($"The watch goes to stand over {b.Name}, where the raiders were heading.");
            if (post != null) Select(Selection.Flag(post.Id));
        }
        else
        {
            if (_map.Strategic) ToggleStrategic();
            ToggleFlag();
            if (_map.Flagging) Hint("Click where the watch should stand guard · Esc to cancel");
        }
        GuideEvent("orders");
        _dirty = true;
    }

    // ---------- Strike ----------

    private void ShowStrike(bool on)
    {
        _strikePanel.Visible = on;
        if (on)
        {
            _strikePane.Invalidate();
            RenderStrike();
            GuideEvent("orders");
        }
    }

    /// <summary>Foes to strike at: named menaces with a seat the town knows, then the dangers on the bounty board, threats first.</summary>
    private void RenderStrike()
    {
        var t = _sim.Town;
        var p = _strikePane;
        p.Begin();
        p.Heading("Strike at a foe");
        int shown = 0;
        foreach (var m in Mercenaries.Targets(t).Take(3))
        {
            int mid = m.Id;
            var free = t.Hires.Where(h => !h.Away).OrderByDescending(h => h.Strength).ThenBy(h => h.CompanyId, StringComparer.Ordinal).FirstOrDefault();
            string text = $"[b]{Ui.Esc(Ui.Cap(m.Name))}[/b] at {Ui.Esc(m.SeatName)}\n{Ui.C(Ui.Ink2, $"[font_size=12]About {Math.Round(m.Power / 10) * 10:0} strong.[/font_size]")}";
            if (free != null)
            {
                string id = free.CompanyId;
                double cost = Mercenaries.CampaignCost(t, id);
                p.Row(text, ($"Send {Ui.Cap(Mercenaries.Def(t, id).Name)} · {cost:N0}g", Mercenaries.MarchBlocked(t, id, mid) == null, true, () => Act(() => _sim.MarchMercenaries(id, mid))));
            }
            else p.Row(text, ("Hire sellswords", true, false, () => OpenWindow(Tab.Army)));
            shown++;
        }
        var slay = t.Content.Bounties.Term("slay");
        var named = Alerts.Standing(t).Where(a => a.SiteId != null).Select(a => a.SiteId!).ToHashSet();
        var sites = t.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && !t.Bounties.Any(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken))
            .OrderBy(s => named.Contains(s.Def.Id) || s.Def.Id == t.Content.Enemy.Outpost.Id ? 0 : 1)
            .ThenBy(s => s.Def.Tier)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .Take(5);
        foreach (var s in sites)
        {
            string sid = s.Def.Id;
            int reward = (int)Terms.Suggested(t, s, slay);
            bool hot = named.Contains(sid) || sid == t.Content.Enemy.Outpost.Id;
            string tried = s.Attempts > 0 ? $" {s.Attempts} {(s.Attempts == 1 ? "party has" : "parties have")} failed there." : "";
            p.Row($"{SiteLink(s.Def)}{(hot ? "  " + Tag("threat", Ui.Bad) : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]A level {s.Def.Tier} danger.{tried}[/font_size]")}",
                ($"Bounty · {reward}g", t.Treasury >= reward && Terms.Blocked(t, s, slay) == null, true, () => Act(() => _sim.PostBounty(sid, reward, slay.Id))));
            shown++;
        }
        if (shown == 0) p.Note("The town knows of no foe to strike at. Scout a spot on the map, or send explorers from the Bounties window, to find one.");
        p.Buttons(("War", true, false, () => OpenWindow(Tab.War)), ("Bounty board", true, false, () => OpenWindow(Tab.Bounties)), ("Close", true, false, () => ShowStrike(false)));
        p.Commit();
    }

    // ---------- Explore and Patrol ----------

    /// <summary>Pick up a wilds brush (or put it down), on the campaign map where the wilds are.</summary>
    private void ToggleBrush(string kind)
    {
        _map.Brush = _map.Brush == kind ? null : kind;
        if (_map.Brush != null)
        {
            _map.Flagging = false;
            _map.Placing = null;
            _map.Planner.Pick(null);
            _map.Decorator.Pick(null);
            if (!_map.Strategic) ToggleStrategic();
            Hint(kind == Wilds.Explore ? "Drag to paint ground to explore · right-drag rubs out · Esc done" : "Drag to paint a patrol · right-drag rubs out · Esc done", 30);
        }
        else Hint("");
        _dirty = true;
    }

    private void OnWildsPainted(string kind, List<(int X, int Y)> cells, bool erase)
    {
        var t = _sim.Town;
        if (erase)
        {
            int n = _sim.EraseWilds(kind, cells);
            if (n > 0) Hint($"Rubbed out {n} {(n == 1 ? "stretch" : "stretches")}.");
        }
        else if (_sim.PaintWilds(kind, cells) is { } z)
            Hint(kind == Wilds.Explore ? $"{Wilds.Unseen(z)} to walk · next trip {Wilds.TripReward(t, z):0}g" : $"Patrol · {Wilds.Wage(t, z):0.#}g a day");
        else if (cells.Select(c => Wilds.CellBlocked(t, c.X, c.Y)).FirstOrDefault(w => w != null) is string why) Hint($"Can't paint there: {why}.");
        GuideEvent("orders");
        _dirty = true;
    }

    // ---------- Every frame ----------

    private void Hint(string text, double seconds = 4)
    {
        _ordersHint.Text = text;
        _ordersHint.Visible = text != "";
        _hintUntil = _clockReal + seconds;
    }

    /// <summary>The buttons show what is in hand and turn red while they are needed; the hint fades.</summary>
    private void SyncOrders()
    {
        var t = _sim.Town;
        if (_ordersHint.Visible && _clockReal > _hintUntil && _map.Brush == null && !_map.Flagging) _ordersHint.Visible = false;

        string? defendWhy = DefendBlocked();
        bool manned = WallOrders().Count > 0 && Garrison.WallsManned(t);
        _defendButton.SetPressedNoSignal(manned);
        _defendButton.Disabled = defendWhy != null;
        _defendButton.ThemeTypeVariation = DefendNeeded() ? "Post" : "";
        _defendButton.TooltipText = defendWhy != null ? $"Defend: {defendWhy}."
            : manned ? "Defend: the watch holds the gate and every side a host comes from. Press to stand it down."
            : Garrison.Threatened(t).Count > 0 ? "Defend: a host is on the march. Sound the horn on the gate and the side it comes from."
            : "Defend: sound the horn on the gate. When a host marches, it mans the side it comes from too.";

        var raided = Raided();
        string? flagWhy = Garrison.FlagBlocked(t, Town.Center, Town.Center);
        _guardButton.SetPressedNoSignal(_map.Flagging);
        _guardButton.Disabled = flagWhy != null && !_map.Flagging;
        _guardButton.ThemeTypeVariation = raided != null && flagWhy == null ? "Post" : "";
        _guardButton.TooltipText = flagWhy != null && !_map.Flagging ? $"Guard: {flagWhy}."
            : raided != null ? $"Guard: raiders were seen heading for {raided.Name}. Plant a flag over it."
            : "Guard: pick up a guard flag and click where the watch should stand (G).";

        _strikeButton.SetPressedNoSignal(_strikePanel.Visible);
        _strikeButton.TooltipText = "Strike: the foes the town knows of, each with a bounty to post or sellswords to send.";

        int left = t.Wilds.Where(z => z.Kind == Wilds.Explore).Sum(Wilds.Unseen);
        _scoutButton.SetPressedNoSignal(_map.Brush == Wilds.Explore);
        _scoutButton.Text = left > 0 ? $"Explore {left}" : "Explore";
        _scoutButton.TooltipText = "Explore: paint ground for a company to walk\n• Paid again each trip until it's all seen\n• Right-drag rubs out";
        var patrols = t.Wilds.Where(z => z.Kind == Wilds.Patrol).ToList();
        _patrolButton.SetPressedNoSignal(_map.Brush == Wilds.Patrol);
        _patrolButton.ThemeTypeVariation = patrols.Any(z => z.Unpaid) ? "Post" : "";
        _patrolButton.TooltipText = $"Patrol: paint ground for riders to keep\n• {t.Content.Wilds.Patrol.PerCell:0.#}g a day a stretch\n• Fewer caravans lost, raiders seen sooner\n• Right-drag rubs out";
    }
}
