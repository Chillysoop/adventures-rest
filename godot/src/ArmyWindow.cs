using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The army window: the town's forces at a glance (soldiers, strength at the walls, the guard's eye, wages), each
/// regiment as a card in its unit's colours with its post, its commander and its numbers, the units the town can
/// raise as a grid (locked ones dimmed, with what they need on hover), the levy under its two laws, and the sellswords.
/// The walls and works themselves stay in Defence; the foes in War.
/// </summary>
public partial class Main
{
    private string? _armyPick;

    private static readonly (ArmyPost Post, string Icon, string Name, string Tip)[] ArmyPosts =
    [
        (ArmyPost.Walls, "post-walls", "Walls", "On the walls and gates: all of it counts in a siege"),
        (ArmyPost.Reserve, "post-reserve", "Reserve", "At the hall: drills half again as fast"),
        (ArmyPost.Patrol, "post-patrol", "Patrol", "The town guard: night raids, thieves, spies, fire-raisers"),
        (ArmyPost.Guard, "post-guard", "Guard", "The mayor's guard: stops knives in the night at the council's doors"),
    ];

    private static Color UnitColor(UnitDef u) => new(u.Colour);

    private static string Pct(double share) => $"{Math.Round(share * 100)}%";

    private void RenderArmy()
    {
        var p = _panes[Tab.Army];
        var t = _sim.Town;
        p.Begin();
        var regiments = t.Army.Regiments.OrderBy(r => Army.Unit(t, r).IsLevy ? 1 : 0).ThenBy(r => r.Id).ToList();
        p.Card($"{Ui.Img("tab-army", 16)} [b]{Army.Soldiers(t)}[/b]    {Ui.Img("post-walls", 16)} [b]+{Math.Round(Army.Defense(t))}[/b]    " +
               $"{Ui.Img("post-patrol", 16)} [b]+{Pct(Army.StreetWatch(t))}[/b]    {Ui.Img("post-guard", 16)} [b]{Pct(Army.Bodyguard(t))}[/b]    {Ui.Img("gold", 16)} [b]{Math.Round(Army.Wages(t))}[/b]/day\n" +
               Ui.C(Ui.Ink2, "[font_size=12]Soldiers · at the walls · guard's eye · mayor's guard · wages[/font_size]"));

        p.Page("Troops", "tab-army");
        RenderField(p, t);

        p.Heading("Regiments");
        foreach (var r in regiments) RegimentRow(p, t, r);

        RenderLevy(p, t);

        p.Page("Raise", "army-drill");
        p.Heading("Raise");
        var units = t.Content.Army.Units.Where(u => !u.IsLevy).ToList();
        // Open ones first, then the ones a hall or an ally away, then the rest.
        var cards = units.Select(u => (u, Why: Army.Locked(t, u))).OrderBy(x => x.Why == null ? 0 : 1).ToList();
        _armyPick ??= cards.FirstOrDefault().u?.Id;
        p.Add($"ug{_armyPick}|{string.Join(";", cards.Select(c => $"{c.u.Id}{c.Why}"))}", () => UnitGrid(t, cards));
        if (units.FirstOrDefault(u => u.Id == _armyPick) is { } pick) PickedUnit(p, t, pick);

        RenderSellswords(p, t);

        p.Page("Allies", "standing");
        RenderAllies(p, t);
        RenderCampaigns(p, t);
        p.Commit();
    }

    /// <summary>A unit's icon on a tile in its colours.</summary>
    private static Control UnitBadge(UnitDef u, int size = 44, bool dim = false)
    {
        var col = UnitColor(u);
        var tile = new PanelContainer { CustomMinimumSize = new Vector2(size, size), SizeFlagsVertical = Control.SizeFlags.ShrinkBegin, Modulate = dim ? new Color(1, 1, 1, 0.4f) : Colors.White };
        tile.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, col, 6, 2, 4, 4));
        tile.AddChild(new TextureRect { Texture = Ui.Icon($"unit-{u.Id}"), Modulate = col.Lightened(0.25f), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
        return tile;
    }

    /// <summary>Ten pips of drill, filled to how well drilled the regiment is.</summary>
    private static string DrillPips(double drill)
    {
        int full = (int)Math.Round(drill * 10);
        return Ui.C(drill >= 0.999 ? Ui.Good : Ui.Gold, new string('■', full)) + Ui.C(Ui.Line.Lightened(0.2f), new string('■', 10 - full));
    }

    private void RegimentRow(Pane p, Town t, Regiment r)
    {
        var u = Army.Unit(t, r);
        int id = r.Id;
        var boss = Army.Commander(t, r);
        string head = $"[b]{Ui.Esc(r.Name)}[/b]  {Tag($"{r.Soldiers.Count}/{r.Wanted}", r.Soldiers.Count >= r.Wanted ? Ui.Ink : Ui.Gold)}  {Ui.Img("post-walls", 13)} {Ui.C(Ui.Good, $"+{Math.Round(Army.AtWalls(t, r))}")}" +
            $"{(r.Fallen > 0 ? $"  {Ui.C(Ui.Bad, $"✝{r.Fallen}")}" : "")}\n" +
            $"{Ui.Img("army-drill", 13)} [font_size=11]{DrillPips(r.Drill)}[/font_size]" +
            (boss != null ? $"   {Ui.Img("army-command", 13)} {AgentLink(boss, boss.GivenName)} {Ui.C(Ui.Good, $"+{Pct(Army.Command(t, r))}")}" : "") +
            ArmsLine(t, r) +
            (r.Short != "" && r.Soldiers.Count < r.Wanted ? $"\n{Ui.C(Ui.Gold, $"[font_size=12]{Ui.Cap(r.Short)}[/font_size]")}" : "");
        string sig = $"{u.Id}{r.Post}{r.CommanderId}{string.Join(",", CommandChoices(t, r).Select(c => c.Id))}";
        int step = u.IsLevy ? 5 : 10;
        p.Row(head, (sig, () => UnitBadge(u), () => RegimentControls(t, r)),
            ("−", r.Wanted > step, false, () => Act(() => _sim.ResizeRegiment(id, r.Wanted - step))),
            ("+", r.Wanted + step <= t.Content.Army.MaxSize && (!u.IsLevy || Army.LevyRoom(t) >= step), false, () => Act(() => _sim.ResizeRegiment(id, r.Wanted + step))),
            (u.IsLevy ? "Stand down" : "Disband", true, false, () => Act(() => _sim.DisbandRegiment(id))));
        if (!u.IsLevy && !r.Refitting && Army.NextGrade(t, r) is { } next)
            p.Buttons(($"{next.Name} arms · {Js.Str(Army.RefitCost(t, r))}g", Army.RefitBlocked(t, r) == null, false, () => Act(() => _sim.RefitRegiment(id))));
    }

    /// <summary>The regiment's arms: its grade, and the smiths' progress on the next.</summary>
    private static string ArmsLine(Town t, Regiment r)
    {
        var now = Army.Grade(t, r.Arms);
        if (now == null && !r.Refitting) return "";
        string grade = now != null ? $"{Ui.Img("army-arms", 13)} {Ui.C(Ui.Good, $"{now.Name} +{Pct(now.Strength)}")}" : Ui.Img("army-arms", 13);
        if (r.Refitting && Army.NextGrade(t, r) is { } next)
            grade += $"  {Ui.C(Ui.Gold, $"→ {next.Name} {r.Refitted}/{r.Soldiers.Count}")}";
        return "   " + grade;
    }

    /// <summary>Heroes who could take command, best leaders first.</summary>
    private static List<Agent> CommandChoices(Town t, Regiment r) =>
        t.Heroes.Where(h => Army.CommandBlocked(t, r, h) == null).OrderByDescending(h => Army.Leadership(t, h)).ThenBy(h => h.Id).ToList();

    /// <summary>A regiment's post as four icon toggles, and the drop-down of heroes to lead it.</summary>
    private Control RegimentControls(Town t, Regiment r)
    {
        var h = new HFlowContainer();
        h.AddThemeConstantOverride("h_separation", 4);
        h.AddThemeConstantOverride("v_separation", 4);
        int id = r.Id;
        foreach (var (post, icon, name, tip) in ArmyPosts)
        {
            var b = new Button { Icon = Ui.Icon(icon), ExpandIcon = false, ToggleMode = true, ButtonPressed = r.Post == post, TooltipText = $"{name}\n{tip}", FocusMode = Control.FocusModeEnum.None };
            b.AddThemeConstantOverride("icon_max_width", 18);
            var to = post;
            b.Pressed += () => Act(() => _sim.PostRegiment(id, to));
            h.AddChild(b);
        }
        var heroes = CommandChoices(t, r);
        var items = heroes.Select(x => ("", $"{x.GivenName} · {Ui.Cap(x.Hero!.Cls.Name)} {x.Hero.Level} · +{Pct(Army.Leadership(t, x))} · leads {Army.CanLead(t, x)}")).ToList();
        var ids = heroes.Select(x => x.Id).ToList();
        if (r.CommanderId != null)
        {
            items.Add(("", "No commander"));
            ids.Add(0);
        }
        if (items.Count > 0)
            h.AddChild(Ui.Menu(r.CommanderId == null ? "Commander" : "Change", "army-command", items, i => Act(() => _sim.CommandRegiment(id, ids[i] == 0 ? null : ids[i]))));
        return h;
    }

    /// <summary>Every unit as a card: open ones bright in their colours, locked ones dimmed with what they need on hover.</summary>
    private Control UnitGrid(Town t, List<(UnitDef U, string? Why)> cards)
    {
        var grid = new HFlowContainer();
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var (u, why) in cards)
        {
            bool picked = u.Id == _armyPick;
            var col = UnitColor(u);
            var card = new Button { CustomMinimumSize = new Vector2(98, 92), FocusMode = Control.FocusModeEnum.None, TooltipText = $"{u.Name}\n{u.Blurb}{(why != null ? $"\n🔒 {Ui.Cap(why)}" : "")}", ClipContents = true };
            var look = Ui.Box(picked ? Ui.Panel2.Lightened(0.08f) : Ui.Panel2, picked ? Ui.Gold : why == null ? col : Ui.Line, 8, picked ? 2 : 1);
            foreach (var state in new[] { "normal", "hover", "pressed", "focus" }) card.AddThemeStyleboxOverride(state, state == "hover" ? Ui.Box(Ui.Panel2.Lightened(0.12f), col, 8, 2) : look);
            var v = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = why == null ? Colors.White : new Color(1, 1, 1, 0.4f) };
            v.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect, Control.LayoutPresetMode.KeepSize, 5);
            v.AddThemeConstantOverride("separation", 2);
            v.AddChild(new TextureRect { Texture = Ui.Icon($"unit-{u.Id}"), Modulate = col.Lightened(0.25f), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, CustomMinimumSize = new Vector2(0, 38), MouseFilter = Control.MouseFilterEnum.Ignore });
            var name = Ui.Label(u.Name, Ui.Ink, null, 12);
            name.HorizontalAlignment = HorizontalAlignment.Center;
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            name.MaxLinesVisible = 2;
            name.MouseFilter = Control.MouseFilterEnum.Ignore;
            v.AddChild(name);
            card.AddChild(v);
            string uid = u.Id;
            card.Pressed += () => Act(() => _armyPick = uid);
            grid.AddChild(card);
        }
        return grid;
    }

    /// <summary>The picked unit: how it fights where, what it costs, and raise buttons by size.</summary>
    private void PickedUnit(Pane p, Town t, UnitDef u)
    {
        string? locked = Army.Locked(t, u);
        string Mul(string icon, double x) => $"{Ui.Img(icon, 14)} {Ui.C(x >= 1.25 ? Ui.Good : x < 0.9 ? Ui.Bad : Ui.Ink, $"×{x:0.#}")}";
        string beats = u.Beats.Count > 0 ? $"   ⚔ {Ui.C(Ui.Good, string.Join(", ", u.Beats))}" : "";
        string bane = u.Bane.Count > 0 ? $"   ✦ {Ui.C(Ui.Gold, string.Join(", ", u.Bane))}" : "";
        string mend = u.Mend > 0 ? $"   ✚ {Ui.C(Ui.Good, Pct(u.Mend))}" : "";
        string text = $"[b]{Ui.Esc(u.Name)}[/b]  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(u.Blurb)}[/font_size]")}\n" +
            $"{Ui.Img("tab-war", 14)} [b]{Js.Str(u.Strength)}[/b]   {Mul("post-walls", u.Walls)}   {Mul("tab-campaign", u.Field)}   {Mul("post-patrol", u.Patrol)}   {Mul("post-guard", u.Guard)}\n" +
            $"{Ui.Img("gold", 14)} {Math.Round(Army.RecruitCost(t, u))} each, {Js.Str(u.Wage)}/day   {Ui.Img("army-drill", 14)} {u.Drill:0} days{(u.Speed > 1 ? $"   » ×{u.Speed:0.#}" : "")}{beats}{bane}{mend}" +
            (locked != null ? $"\n{Ui.C(Ui.Bad, $"🔒 {Ui.Cap(locked)}")}" : "");
        var buttons = t.Content.Army.Sizes.Select(n =>
        {
            string? why = Army.RaiseBlocked(t, u.Id, n);
            return ($"Raise {n} · {Math.Round(Army.RecruitCost(t, u) * n)}g", why == null, n == t.Content.Army.Sizes[0], (Action)(() => Act(() => _sim.RaiseRegiment(u.Id, n))));
        }).ToArray();
        p.Row(text, ($"pu{u.Id}{locked}", () => UnitBadge(u, 52, locked != null), null), buttons);
        if (locked == null && Army.HallFor(t, u, t.Content.Army.Sizes[0]) is { } hall)
            p.Note($"Trains at the {hall.Name}: room for {Army.Room(t, hall)} more.");
    }

    /// <summary>The levy: its two laws, who would answer, and the call.</summary>
    private void RenderLevy(Pane p, Town t)
    {
        var l = t.Content.Army.Levy;
        p.Heading("Levy");
        p.Choice("Service", ["Volunteers", "Conscription"], t.Army.Service == ServiceLaw.Volunteers ? 0 : 1, i => Act(() => _sim.SetServiceLaw(i == 0 ? ServiceLaw.Volunteers : ServiceLaw.Conscription)));
        p.Choice("Pay", [$"Paid · {Js.Str(l.Wage)}/day", "Unpaid"], t.Army.LevyPaid ? 0 : 1, i => Act(() => _sim.SetLevyPay(i == 0)));
        int room = Army.LevyRoom(t);
        var levy = Army.Levy(t);
        bool conscripts = t.Army.Service == ServiceLaw.Conscription;
        string laws = (conscripts ? $"{Ui.C(Ui.Bad, $"−{Js.Str(-l.ConscriptMandate)} mandate to pass, unhappy conscripts")}" : Ui.C(Ui.Ink2, "Only the content volunteer")) +
            (t.Army.LevyPaid ? "" : $" · {Ui.C(Ui.Bad, "unpaid: unhappier still")}");
        p.Row($"{Ui.Img("unit-levy", 15)} [b]{room}[/b] would answer  ·  [b]{levy?.Soldiers.Count ?? 0}[/b] called  ·  cap {Army.LevyCap(t)}\n[font_size=12]{laws}\n{Ui.C(Ui.Ink2, "Called townsfolk leave their work while they serve.")}[/font_size]",
            ("Call 10", Army.LevyBlocked(t, Math.Min(10, Math.Max(1, room))) == null, true, () => Act(() => _sim.RaiseLevy(10))),
            ($"Call all ({room})", room > 0 && Army.LevyBlocked(t, room) == null, false, () => Act(() => _sim.RaiseLevy(room))));
    }

    /// <summary>The town's allies: what they pledge at the walls, and a call to arms for one or all of them.</summary>
    private void RenderAllies(Pane p, Town t)
    {
        var allies = AllyCalls.Callable(t);
        if (allies.Count == 0) return;
        p.Heading("Allies");
        double pledged = World.AllyTroops(t);
        double camped = AllyCalls.Defense(t);
        var open = allies.Where(s => AllyCalls.CallBlocked(t, s) == null).Select(s => s.Id).ToList();
        p.Row($"{Ui.Img("act-offer", 15)} Pledged  {Tag($"+{Math.Round(pledged)}", Ui.Good)}    {Ui.Img("tab-army", 15)} Camped  {Tag($"+{Math.Round(camped)}", camped > 0 ? Ui.Good : Ui.Ink2)}",
            ($"Call all ({open.Count})", open.Count > 0, true, () => Act(() => _sim.CallAllies(open))));
        foreach (var s in allies)
        {
            var c = AllyCalls.Live(t, s);
            string head = $"[b]{Ui.Esc(Ui.Cap(s.People))}[/b]  {Ui.Img("standing", 13)}{Tag(Js.Str(Js.Round(s.Standing)), s.Standing >= 30 ? Ui.Good : s.Standing < 0 ? Ui.Bad : Ui.Ink2)}";
            string host = $"{Ui.Img("unit-swordsmen", 13)} {Js.Str(AllyCalls.Host(t, s))}  ·  {Ui.Img("compass", 13)} {AllyCalls.MarchDays(t, s)}d";
            switch (c?.State)
            {
                case CallState.Asked:
                    p.Row($"{head}\n[font_size=12]{host}  ·  {Ui.C(Ui.Ink2, "riders out")}[/font_size]");
                    break;
                case CallState.Holdout:
                    var call = c;
                    var temper = AllyCalls.Ruler(t, s);
                    p.Row($"{head}\n[font_size=12]{Ui.C(Ui.Gold, "Holding out")}  ·  {Ui.Img("gold", 13)} {Js.Str(c.Price)}  ·  {Ui.C(Ui.Ink2, $"ruler {temper.Name}")}[/font_size]",
                        ($"Pay {Js.Str(c.Price)}", AllyCalls.BribeBlocked(t, c) == null, true, () => Act(() => _sim.BribeAlly(call.Id))),
                        ("Let go", true, false, () => Act(() => _sim.LetAllyGo(call.Id))));
                    var items = Odds.Approaches(t, a => AllyCalls.NegotiateDc(t, call, a),
                        a => $"their {temper.Name} ruler's {(a.Against == "wis" ? "Wisdom" : "Insight")}, standing {Js.Str(Js.Round(s.Standing))}");
                    p.RollMenus(("Negotiate", "talk-persuade", "Talk them round: they march, or keep their host. Choose how.", items,
                        i => Act(() => _sim.NegotiateAlly(call.Id, Negotiation.Rules(t).Approaches[i].Id))));
                    break;
                case CallState.Marching:
                    int days = (int)Math.Ceiling((c.ArriveTick - t.Tick) / (double)SimTime.HoursPerDay);
                    p.Row($"{head}\n[font_size=12]{Ui.C(Ui.Good, $"Marching · {Js.Str(c.Strength)}")}  ·  {Ui.Img("compass", 13)} {days}d to go[/font_size]");
                    break;
                case CallState.Arrived:
                    p.Row($"{head}\n[font_size=12]{Ui.C(Ui.Good, $"Camped at the gate · +{Js.Str(c.Strength)}")}  ·  home in {Math.Max(0, c.LeaveDay - t.Day)}d[/font_size]");
                    break;
                default:
                    string? why = AllyCalls.CallBlocked(t, s);
                    int id = s.Id;
                    p.Row($"{head}\n[font_size=12]{host}  ·  {Ui.C(Ui.Ink2, $"{Pct(AllyCalls.ComeChance(t, s))} come")}{(why == null ? "" : $"  ·  {Ui.C(Ui.Ink2, why)}")}[/font_size]",
                        ("Call", why == null, false, () => Act(() => _sim.CallAllies([id]))));
                    break;
            }
        }
    }
}
