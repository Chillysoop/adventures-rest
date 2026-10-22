using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The town guard and the walls: the Defence tab's orders and wall works, the inspector for a stretch of wall, a tower or a
/// guard flag, and the flag in hand (G). The player never steers a guard: they sound a call to arms on the wall or plant
/// a flag, and the nearest of the watch go.
/// </summary>
public partial class Main
{
    /// <summary>Guards a new flag asks for.</summary>
    private int _flagGuards = 3;

    private void ToggleFlag()
    {
        _map.Flagging = !_map.Flagging;
        if (_map.Flagging)
        {
            _map.Brush = null;
            _map.Planner.Pick(null);
            _map.Decorator.Pick(null);
        }
        _dirty = true;
    }

    private void OnFlagDropped(Vector2 w)
    {
        var at = Planner.ToTown(w);
        var post = _sim.PlantFlag(at.X, at.Y, _flagGuards);
        _map.Flagging = false;
        GuideEvent("flag");
        if (post != null) Select(Selection.Flag(post.Id));
        _dirty = true;
    }

    /// <summary>Look at a stretch of wall and open it in the inspector, where its call to arms is.</summary>
    private void ShowWall(int section)
    {
        if (_map.TownView.Walk is { } walk) Look(walk.Sections[section][walk.Sections[section].Length / 2], 18);
        Select(Selection.Wall(section));
    }

    private static string WallName(Town t) =>
        t.Defenses.CityWall == PalisadeState.Built ? "City wall" : t.Defenses.StoneWall == PalisadeState.Built ? "Stone walls" : "Palisade";

    private static string SectionTitle(Town t, int section) =>
        section == 0 ? (t.Defenses.Gatehouse == PalisadeState.Built ? "The gatehouse" : "The south gate") : $"The {t.Content.Garrison.Sections[section]} wall";

    // ---------- Defence tab ----------

    private static string WorkBlurb(Town t, DefenseWork w)
    {
        var g = t.Content.Garrison;
        var e = t.Content.Enemy.Defense;
        return w switch
        {
            DefenseWork.Palisade => $"Stakes round the town. Multiplies your defence by {e.Palisade}.",
            DefenseWork.Tower => $"Each adds {Math.Round(e.PerTower * 100)}% to your defence, and archers when it is called to arms. {g.TowerHouses} of the watch live in it, quick to the horn.",
            DefenseWork.StoneWall => $"Round the old town, in place of the stakes. Multiplies your defence by {e.StoneWall}, and carries {g.StoneTowers} towers.",
            DefenseWork.Gatehouse => $"A portcullis over the south road: the gate holds ×{g.Gatehouse} when a siege tries it.",
            DefenseWork.Moat => $"Filled from the river: every wave wades it, and the walls count ×{g.Moat}.",
            DefenseWork.Barracks => $"Bunks in the walls for {g.BarracksHouses} of the watch: half their wage, quick to the horn, and stronger against raiders by night.",
            _ => $"Round the four inner wards. Multiplies your defence by {e.CityWall}, and carries {g.CityTowers} towers.",
        };
    }

    private static string WorkLabel(Town t, DefenseWork w) => w switch
    {
        DefenseWork.Tower => t.Defenses.StoneWall == PalisadeState.Built ? "Stone towers" : "Watchtowers",
        DefenseWork.StoneWall => "Stone walls",
        DefenseWork.CityWall => "City wall",
        DefenseWork.Barracks => "Wall barracks",
        _ => w.ToString(),
    };

    /// <summary>One wall work: what it does, how far along it is, and the button to order it.</summary>
    private void WorkRow(Pane p, Town t, DefenseWork w)
    {
        var d = t.Defenses;
        string? why = Threat.BuildBlocked(t, w);
        double cost = Threat.BuildCost(t, w);
        // The work's name carries what it does on hover; the row keeps only its state and count.
        string head = Ui.Tip($"{Ui.Img(w is DefenseWork.Palisade ? "timber" : w is DefenseWork.Moat ? "ui-shield" : "stone", 16)} [b]{WorkLabel(t, w)}[/b] {Ui.Img("ui-info", 12)}", Ui.Plain(WorkBlurb(t, w)));
        string count = w switch
        {
            DefenseWork.Tower => $"  {Tag($"{d.Towers}{(d.TowersBuilding.Count > 0 ? $" (+{d.TowersBuilding.Count} rising)" : "")} of {Garrison.MaxTowers(t)}", d.Towers >= Garrison.MaxTowers(t) ? Ui.Good : Ui.Ink2)}",
            DefenseWork.Barracks => $"  {Tag($"{d.Barracks}{(d.BarracksBuilding.Count > 0 ? $" (+{d.BarracksBuilding.Count} rising)" : "")} of {t.Content.Garrison.BarracksWork.Max}")}",
            _ => "",
        };
        if (why is "built" or "dug") p.Row($"{head}  {Tag(Ui.Cap(why), Ui.Good)}");
        else if (why == "under way") p.Row($"{head}  {Tag("Rising", Ui.Gold)}");
        else if (why != null && why != "not enough gold") p.Row($"{head}{count}  {Tag(Ui.Cap(why))}");
        else p.Row($"{head}{count}", ($"Build {Math.Round(cost)}g", why == null, true, () => Act(() => _sim.BuildDefense(w))));
    }

    /// <summary>The town's own strength: the defence meter, the watch's orders, the walls and the wardens.</summary>
    private void RenderDefence()
    {
        var p = _panes[Tab.Defence];
        var t = _sim.Town;
        double def = Threat.Defense(t);
        p.Begin();
        double? est = Discovery.Known(t, Discovery.Warband) && Threat.IsActive(t) ? Threat.EstimatedStrength(t) : null;
        double scale = Math.Max(Math.Max(def, est ?? 0), 1);
        p.Meter("Your defence", 100 * def / scale, $"{Math.Round(def)}");
        if (est is double e) p.Meter("Warband", 100 * e / scale, $"~{Math.Round(e)}", true);
        // What the defence is made of, before the walls multiply it.
        var dd = t.Content.Enemy.Defense;
        double fort = Threat.Fortification(t);
        double watch = t.Watch * dd.Watchman * Planning.WatchFactor(t);
        double heroes = t.Heroes.Where(a => !a.IsAway).Sum(a => a.Hero!.Level) * dd.HeroLevel;
        double militia = t.Workers.Count * dd.MilitiaPerWorker;
        double rest = Math.Max(0, def / Math.Max(fort, 1e-9) - watch - heroes - militia);
        p.Text(Ui.Chips(
            Ui.Chip("defence", $"{Math.Round(watch)}", Ui.Ink, "The watch"),
            Ui.Chip("heroes", $"{Math.Round(heroes)}", Ui.Ink, "Heroes at home"),
            Ui.Chip("population", $"{Math.Round(militia)}", Ui.Ink, "A militia of townsfolk"),
            rest >= 1 ? Ui.Chip("ui-shield", $"{Math.Round(rest)}", Ui.Ink, "Everything else: soldiers, allies, sellswords, the gods, factions, institutions") : "",
            Ui.Chip("stone", $"×{fort:0.00}", Ui.Gold, $"Walls and towers multiply it{(Threat.IsActive(t) ? ". The warband grows while dungeons go uncleared" : "")}")), 12);
        RenderGuard(p, t);
        RenderWorks(p, t);
        RenderTraps(p, t);
        RenderClockwork(p, t);
        p.Commit();
    }

    /// <summary>The wall works in the order the town climbs them: stakes, towers, stone, the gate and moat, barracks, the city wall.</summary>
    private void RenderWorks(Pane p, Town t)
    {
        p.Heading("Walls");
        foreach (var w in new[] { DefenseWork.Palisade, DefenseWork.Tower, DefenseWork.StoneWall, DefenseWork.Gatehouse, DefenseWork.Moat, DefenseWork.Barracks, DefenseWork.CityWall })
        {
            // The stakes come down once the stone stands; stone works wait until it does.
            if (w == DefenseWork.Palisade && t.Defenses.StoneWall == PalisadeState.Built) continue;
            if (w is DefenseWork.Gatehouse or DefenseWork.Moat or DefenseWork.Barracks or DefenseWork.CityWall && t.Defenses.StoneWall == PalisadeState.None) continue;
            WorkRow(p, t, w);
        }
    }

    /// <summary>The watch's orders: who is where, the flag in hand, and every call to arms or flag standing.</summary>
    private void RenderGuard(Pane p, Town t)
    {
        var g = t.Content.Garrison;
        p.Heading("Town guard");
        int housed = Garrison.Housed(t);
        p.Row($"[b]Watch[/b]  {Ui.Chips(Ui.Chip("defence", $"{t.Watch}", null, "Watchmen. Hired through the Captain's motions in the Council."), housed > 0 ? Ui.Chip("stone", $"{housed}", Ui.Good, "Live in the walls") : "", Ui.Chip("gold", $"−{Math.Round(Garrison.Wages(t))}/day", Ui.Bad, "Wages"))}");
        RenderHeroGuard(p, t);
        string? flagWhy = Garrison.FlagBlocked(t, Town.Center, Town.Center);
        p.Row($"{Ui.Tip($"{Ui.Img("order-guard", 16)} [b]Guard flag[/b] {Ui.Dim("(G)")}", "Plant it anywhere: the nearest guards go and stand there. Raiders shy away from what they cover, and what they cover holds better.")}  {Ui.Chip("defence", $"{_flagGuards}", null, "Guards")}",
            ("−", _flagGuards > 1, false, () => Act(() => _flagGuards--)),
            ("+", _flagGuards < Math.Max(1, t.Watch), false, () => Act(() => _flagGuards++)),
            (_map.Flagging ? "Cancel" : "Plant", flagWhy == null || _map.Flagging, true, ToggleFlag));
        foreach (var post in t.Garrison.Posts.OrderBy(x => x.Id))
        {
            int id = post.Id, at = Garrison.At(t, post), sent = Garrison.Sent(t, post);
            string what = post.Kind switch
            {
                PostKind.Flag => $"Flag {FlagPlace(t, post)}",
                PostKind.Tower => $"The {g.Sections[Garrison.TowerSection(t, post.Index)]} tower",
                _ => SectionTitle(t, post.Index),
            };
            string state = at == sent ? Tag($"{at} holding it", Ui.Good) : Tag($"{at} there, {sent - at} on the way", Ui.Gold);
            p.Row($"{Ui.Esc(what)}  {state}", ("Show", true, false, () => ShowPost(post)), ("Stand down", true, false, () => Act(() => _sim.StandDown(id))));
        }
        if (Garrison.Walled(t)) p.Note("Click a wall, tower or gate to call the watch to arms there. When a host marches, the scouts say which side it comes at.");
        else p.Note("Once the palisade stands, the watch walks it, and you can call them to arms on any stretch of it.");
    }

    /// <summary>Heroes posted to guard the town: who stands where, what they add, and a drop-down to post more.</summary>
    private void RenderHeroGuard(Pane p, Town t)
    {
        var walls = Garrison.OnDuty(t, HeroDuty.Walls);
        var streets = Garrison.OnDuty(t, HeroDuty.Streets);
        p.Row($"{Ui.Tip($"{Ui.Img("heroes", 16)} {Ui.Plainly("[b]Heroes on guard[/b]")}", "Heroes on guard stay home and take no bounties.")}  {Ui.Chips(
            Ui.Chip("order-defend", $"{walls.Count}", null, "On the walls"), Ui.Chip("defence", $"+{Js.Str(Math.Round(Garrison.DutyDefense(t)))}", Ui.Good, "Defence"),
            Ui.Chip("order-guard", $"{streets.Count}", null, "On the streets"), Ui.Chip("ui-eye", $"+{Js.Str(Math.Round(Garrison.StreetWatch(t) * 100))}%", Ui.Good, "Watch"),
            Ui.Chip("gold", $"−{Js.Str(Math.Round(Garrison.DutyWages(t)))}g", Ui.Bad, "Wages a day"))}");
        foreach (var h in Garrison.Defenders(t))
        {
            int hid = h.Id;
            var s = h.Hero!;
            var other = s.Duty == HeroDuty.Walls ? HeroDuty.Streets : HeroDuty.Walls;
            p.Row($"{DutyIcon(h, 15)}{AgentLink(h)} {Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(s.Cls.Name))} {s.Level}{(s.Magic.Count > 0 ? $" · {s.Magic.Count} magic" : "")}")}",
                (other == HeroDuty.Walls ? "To walls" : "To streets", true, false, () => Act(() => _sim.SetDuty(hid, other))),
                ("Stand down", true, false, () => Act(() => _sim.SetDuty(hid, HeroDuty.None))));
        }
        // Heroes in town not on guard, best first, for the two drop-downs.
        var free = t.Heroes.Where(h => h.Hero!.Duty == HeroDuty.None && Garrison.DutyBlocked(t, h, HeroDuty.Walls) == null)
            .OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
        var items = free.Select(h => ("", $"{h.GivenName} · {Ui.Cap(h.Hero!.Cls.Name)} {h.Hero.Level} · {Js.Str(Garrison.Wage(t, h))}g/day")).ToList();
        var ids = free.Select(h => h.Id).ToList();
        var guard = Garrison.Defenders(t);
        var guardIds = guard.Select(h => h.Id).ToList();
        p.Menus(
            ("Post to walls", "order-defend", items, i => Act(() =>
            {
                _sim.SetDuty(ids[i], HeroDuty.Walls);
                GuideEvent("duty");
            })),
            ("Post to streets", "order-guard", items, i => Act(() =>
            {
                _sim.SetDuty(ids[i], HeroDuty.Streets);
                GuideEvent("duty");
            })));
        if (guard.Count > 0)
            p.Buttons(($"Arm the guard from the armory", guard.Any(h => Sim.Systems.Armory.BestFor(t, h) != null), true, () => Act(() => _sim.Arm(guardIds))));
    }

    private void ShowPost(GuardPost post)
    {
        if (post.Kind == PostKind.Flag)
        {
            Look(new Vector2(Geo.Margin + (float)post.X, Geo.Margin + (float)post.Y), 18);
            Select(Selection.Flag(post.Id));
        }
        else if (post.Kind == PostKind.Tower) Select(Selection.Tower(post.Index));
        else ShowWall(post.Index);
    }

    private static string FlagPlace(Town t, GuardPost post)
    {
        var near = t.Buildings.Where(b => b.Complete)
            .OrderBy(b => Math.Pow(t.Lot(b.LotId).X - post.X, 2) + Math.Pow(t.Lot(b.LotId).Y - post.Y, 2)).ThenBy(b => b.Id).FirstOrDefault();
        return near != null && Math.Abs(t.Lot(near.LotId).X - post.X) + Math.Abs(t.Lot(near.LotId).Y - post.Y) < 8 ? $"by {near.Name}" : $"on the {t.Content.Garrison.Sections[Garrison.SectionAt(t, post.X, post.Y)]} side";
    }

    // ---------- Inspector ----------

    /// <summary>A threat marching on this section, for the inspector's warning.</summary>
    private static string? Threatening(Town t, int section)
    {
        if (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at && t.Enemy.Approach == section)
            return $"{Ui.Cap(t.Content.Enemy.Name)} comes at it in {Ui.Days(Math.Max(1, (int)Math.Ceiling((at - t.Tick) / 24.0)))}.";
        foreach (var m in t.Saga.Menaces.Where(m => m.Alive && m.ArrivalTick != null && m.Approach == section && Horde.Known(t, m)).OrderBy(m => m.Id))
            return Horde.On(t, m) ? $"{Ui.Cap(m.Name)} comes at it, {Horde.Word(t, m)}." : $"{Ui.Cap(m.Name)} comes at it in {Ui.Days(Math.Max(1, (int)Math.Ceiling((m.ArrivalTick!.Value - t.Tick) / 24.0)))}.";
        return null;
    }

    /// <summary>The order standing on a stretch of wall or a tower, with its stand-down, or the call to arms.</summary>
    private void CallRow(Pane p, Town t, PostKind kind, int index)
    {
        var post = t.Garrison.Posts.FirstOrDefault(x => x.Kind == kind && x.Index == index);
        if (post != null)
        {
            int id = post.Id, at = Garrison.At(t, post), sent = Garrison.Sent(t, post);
            p.Row($"[b]Called to arms[/b]  {(at == sent ? Tag($"{at} holding it", Ui.Good) : Tag($"{at} there, {sent - at} on the way", Ui.Gold))}",
                ("Stand down", true, false, () => Act(() => _sim.StandDown(id))));
            return;
        }
        string? why = Garrison.CallBlocked(t, kind, index);
        int want = kind switch { PostKind.Tower => t.Content.Garrison.TowerPost, PostKind.Gate => t.Content.Garrison.GatePost, _ => t.Content.Garrison.WallPost };
        p.Row($"{Ui.C(Ui.Ink2, $"[font_size=12]{(why != null ? Ui.Cap(why) + "." : $"Sends the nearest {Math.Min(want, t.Watch)} of the watch to hold it.")}[/font_size]")}",
            ("Call to arms", why == null, true, () => Act(() =>
            {
                _sim.CallToArms(kind, index);
                GuideEvent("call");
            })));
    }

    private void InspectWall(Pane p, Town t, int section)
    {
        var d = t.Defenses;
        p.Text($"[font_size=19][b]{Ui.Esc(SectionTitle(t, section))}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"{WallName(t)}{(section == 0 ? d.Gatehouse == PalisadeState.Built ? " · stone gatehouse" : " · timber gates" : "")}{(d.Moat == PalisadeState.Built ? " · moat" : "")}")}");
        if (Threatening(t, section) is string warn) p.Text(Ui.C(Ui.Bad, $"[b]{Ui.Esc(warn)}[/b]"));
        int men = Garrison.Manning(t, section);
        double f = Garrison.SectionFactor(t, section);
        p.Text($"{men} of the watch on it now. Against a siege it holds {Tag($"×{f:0.00}", f >= 1.05 ? Ui.Good : f < 0.97 ? Ui.Bad : Ui.Ink2)}.");
        CallRow(p, t, section == 0 ? PostKind.Gate : PostKind.Wall, section);
        for (int i = 0; i < d.Towers; i++)
            if (Garrison.TowerSection(t, i) == section)
            {
                int tower = i;
                p.Row($"A tower stands on this stretch.", ("Open", true, false, () => Select(Selection.Tower(tower))));
            }
        p.Heading("Works");
        if (section == 0) WorkRow(p, t, d.StoneWall == PalisadeState.Built ? DefenseWork.Gatehouse : DefenseWork.Palisade);
        if (!Garrison.TowerOn(t, section) && Garrison.TowerSection(t, d.Towers + d.TowersBuilding.Count) == section) WorkRow(p, t, DefenseWork.Tower);
        WorkRow(p, t, d.StoneWall == PalisadeState.Built ? (d.Moat == PalisadeState.Built ? d.Barracks < t.Content.Garrison.BarracksWork.Max ? DefenseWork.Barracks : DefenseWork.CityWall : DefenseWork.Moat) : DefenseWork.StoneWall);
        RenderTrapsAt(p, t, section);
        p.Note("Guards on their beats walk the wall in turns round the town. A stretch with more of the watch on it than its share holds better when a wave hits it; a bare one, worse.");
    }

    private void InspectTower(Pane p, Town t, int tower)
    {
        if (tower >= t.Defenses.Towers)
        {
            p.Note("The tower is gone.");
            return;
        }
        int section = Garrison.TowerSection(t, tower);
        var g = t.Content.Garrison;
        bool stone = t.Defenses.StoneWall == PalisadeState.Built;
        p.Text($"[font_size=19][b]The {g.Sections[section]} tower[/b][/font_size]\n{Ui.C(Ui.Ink2, stone ? "Stone tower" : "Watchtower")}");
        if (Threatening(t, section) is string warn) p.Text(Ui.C(Ui.Bad, $"[b]{Ui.Esc(warn)}[/b]"));
        p.Text($"Home to {g.TowerHouses} of the watch, who reach the walls twice as fast. Called to arms, its archers add {Math.Round(g.TowerManned * 100)}% to the {g.Sections[section]} wall in a siege.");
        CallRow(p, t, PostKind.Tower, tower);
        p.Row("The wall it stands on.", ("Open", true, false, () => ShowWall(section)));
    }

    private void InspectFlag(Pane p, Town t, int postId)
    {
        if (Garrison.Post(t, postId) is not { } post)
        {
            p.Note("The flag has been taken down.");
            return;
        }
        var g = t.Content.Garrison;
        int at = Garrison.At(t, post), sent = Garrison.Sent(t, post);
        var covered = t.Buildings.Where(b => b.Complete && Math.Sqrt(Math.Pow(t.Lot(b.LotId).X - post.X, 2) + Math.Pow(t.Lot(b.LotId).Y - post.Y, 2)) <= g.FlagRadius).ToList();
        p.Text($"[font_size=19][b]Guard flag[/b][/font_size]\n{Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(FlagPlace(t, post))))}");
        p.Text(at == sent ? $"{Tag($"{at} guards holding it", Ui.Good)}" : $"{Tag($"{at} there, {sent - at} on the way", Ui.Gold)}");
        p.Text($"Covers {covered.Count} {(covered.Count == 1 ? "building" : "buildings")}. Raiders pick something else when they can; if they come anyway, the defence here counts ×{1 + Math.Min(g.FlagCap, g.FlagPerGuard * at):0.00}.", 13);
        p.Buttons(("Stand down", true, false, () => Act(() =>
        {
            _sim.StandDown(postId);
            Select(null);
        })));
    }

    /// <summary>Every trap laid, wall by wall, and the refill for spent pitch. Laying is on each wall's card.</summary>
    private void RenderTraps(Pane p, Town t)
    {
        if (!Garrison.Walled(t)) return;
        p.Heading("Traps", "trap-caltrops");
        if (t.Traps.Laid.Count == 0)
        {
            p.Note("Click a stretch of wall on the map to lay pitch, caltrops or pits before it. Each takes a share of every wave that comes at that wall.");
            return;
        }
        foreach (var g in t.Traps.Laid.GroupBy(x => x.Section).OrderBy(g => g.Key))
        {
            int section = g.Key;
            var icons = g.OrderBy(x => x.Id).Select(x => Traps.Def(t, x.Kind) is { } d ? Ui.Tip(Ui.Img(d.Icon, 18) + (x.Spent ? Ui.C(Ui.Bad, "✕") : ""), d.Name + (x.Spent ? " (spent)" : "")) : "");
            p.Row($"[b]{Ui.Esc(Ui.Cap(Garrison.Place(t, section)))}[/b]  {string.Join(" ", icons)}", ("Open", true, false, () => ShowWall(section)));
        }
        p.Text(Ui.Chips(Ui.Chip("warband", $"{Math.Round(t.Traps.Taken)}", Ui.Good, "Foes the traps have taken")), 13);
        var spent = Traps.Spent(t);
        if (spent.Count > 0)
            p.Buttons(($"Refill · {Js.Str(Traps.RefillCost(t))}g", t.Treasury >= Traps.RefillCost(t), true, () => Act(() => _sim.RefillTraps())));
    }

    /// <summary>A wall's traps: the ones laid, to take up, and the kinds still to lay.</summary>
    private void RenderTrapsAt(Pane p, Town t, int section)
    {
        if (!Garrison.Walled(t)) return;
        p.Heading("Traps", "trap-caltrops");
        foreach (var trap in Traps.At(t, section).OrderBy(x => x.Id))
        {
            if (Traps.Def(t, trap.Kind) is not { } d) continue;
            int id = trap.Id;
            p.Row($"{Ui.Img(d.Icon, 18)} [b]{Ui.Esc(d.Name)}[/b]{(trap.Spent ? "  " + Tag("Spent", Ui.Bad) : "")}", ("Take up", true, false, () => Act(() => _sim.RemoveTrap(id))));
        }
        foreach (var d in t.Content.Traps.Kinds)
        {
            string? why = Traps.Blocked(t, d.Id, section);
            if (why is "laid already" or "only at the gate") continue;
            string kind = d.Id;
            string cut = $"−{Math.Round(d.Cut * 100)}%";
            string chips = Ui.Chips(Ui.Chip("warband", cut, Ui.Good, d.First ? "Of the first wave" : "Of every wave"), d.Once ? Ui.Chip("ui-time", "once", Ui.Ink2, $"Spent when lit; refill {Js.Str(d.Refill)}g") : "");
            string head = Ui.Tip($"{Ui.Img(d.Icon, 18)} {Ui.Esc(d.Name)}", d.Tip);
            if (why is not null and not "not enough gold") p.Row($"{head}  {Tag(Ui.Cap(why))}");
            else p.Row($"{head}  [font_size=12]{chips}[/font_size]", ($"Lay · {Js.Str(Traps.Cost(t, d))}g", why == null, true, () => Act(() =>
            {
                _sim.LayTrap(kind, section);
                GuideEvent("trap");
            })));
        }
    }
}
