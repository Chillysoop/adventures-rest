using CrownAndCompass.Client.Map;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>The side panes, one method each, written like the web build's so the two stay easy to compare.</summary>
public partial class Main
{
    private static string Tag(string text, Color? color = null) => Ui.C(color ?? Ui.Ink2, $"[b]{text}[/b]");

    private static string Sign(double n) => n >= 0 ? $"+{Math.Abs(Math.Round(n))}g" : $"−{Math.Abs(Math.Round(n))}g";

    // ---------- War ----------

    private void RenderWar()
    {
        var p = _panes[Tab.War];
        var t = _sim.Town;
        var d = t.Defenses;
        var stage = Stages[t.Enemy.Stage];
        double? est = Threat.EstimatedStrength(t);
        double def = Threat.Defense(t);
        double scale = Math.Max(Math.Max(def, est ?? 0), 1);
        var camp = t.Sites.FirstOrDefault(s => s.Def.Id == t.Content.Enemy.Outpost.Id);
        double palCost = Threat.BuildCost(t, DefenseWork.Palisade);
        double towCost = Threat.BuildCost(t, DefenseWork.Tower);
        int maxTowers = t.Content.Enemy.Tower.Max;
        int home = t.Heroes.Count(a => !a.IsAway);

        p.Begin();
        string arrival = t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at
            ? "\n" + Ui.C(Ui.Bad, $"[b]They reach the walls in {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))}. Heroes in town will face the warlord.[/b]")
            : "";
        p.Card($"[font_size=17][b]{Ui.Esc(Ui.Cap(t.Content.Enemy.Name))}[/b][/font_size]\n{stage.Text}{arrival}");
        p.Heading("Strength");
        if (est is double e) p.Meter("Warband", 100 * e / scale, $"~{Math.Round(e)}", true);
        else p.Text($"{Ui.C(Ui.Ink2, "Warband")}    [i]Unknown. Nobody has seen it yet.[/i]", 13);
        p.Meter("Your defence", 100 * def / scale, $"{Math.Round(def)}");
        p.Note($"Defence counts the watch, heroes in town, a militia of townsfolk, and the walls (×{Threat.Fortification(t):0.00}). The warband grows while dungeons go uncleared.");
        p.Heading("Fortifications");
        if (d.Palisade == PalisadeState.Built) p.Row($"[b]Palisade[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Multiplies your defence by {t.Content.Enemy.Defense.Palisade}.[/font_size]")}  {Tag("Built", Ui.Good)}");
        else if (d.Palisade == PalisadeState.Building)
            p.Row($"[b]Palisade[/b]  {Tag($"Building, {Ui.Days((int)Math.Ceiling(((d.PalisadeReadyTick ?? t.Tick) - t.Tick) / 24.0))} left", Ui.Gold)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Multiplies your defence by {t.Content.Enemy.Defense.Palisade}.[/font_size]")}");
        else
            p.Row($"[b]Palisade[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Multiplies your defence by {t.Content.Enemy.Defense.Palisade}.[/font_size]")}",
                ($"Build {Math.Round(palCost)}g", t.Treasury >= palCost, true, () => Act(() => _sim.BuildDefense(DefenseWork.Palisade))));
        string towers = $"[b]Watchtowers[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Each adds {Math.Round(t.Content.Enemy.Defense.PerTower * 100)}% more, and the town sees farther into the fog.[/font_size]")}";
        if (d.Palisade != PalisadeState.Built) p.Row($"{towers}  {Tag("Needs the palisade")}");
        else if (d.Towers + d.TowersBuilding.Count >= maxTowers) p.Row($"{towers}  {Tag($"{d.Towers} of {maxTowers}", Ui.Good)}");
        else
            p.Row($"{towers}  {Tag($"{d.Towers}{(d.TowersBuilding.Count > 0 ? $" (+{d.TowersBuilding.Count} building)" : "")} of {maxTowers}")}",
                ($"Build {Math.Round(towCost)}g", t.Treasury >= towCost, true, () => Act(() => _sim.BuildDefense(DefenseWork.Tower))));
        p.Row($"[b]Watch[/b]  {Tag($"{t.Watch} watchmen")}\n{Ui.C(Ui.Ink2, "[font_size=12]Hired through the Captain's motions in the Council.[/font_size]")}");
        p.Row($"[b]Heroes in town[/b]  {Tag(home.ToString())}\n{Ui.C(Ui.Ink2, "[font_size=12]Only heroes can kill the warlord when he comes.[/font_size]")}");
        if (camp != null && !camp.Cleared) p.Note("The war camp feeds the warband. Post a bounty on it in the Bounties tab to strike first.");
        p.Commit();
    }

    private void Act(Action a)
    {
        a();
        _dirty = true;
    }

    // ---------- Council ----------

    private void RenderCouncil()
    {
        var p = _panes[Tab.Council];
        var t = _sim.Town;
        var pending = Council.PendingMotion(t);
        p.Begin();
        string facts = $"{Tag($"Tax {Math.Round(t.TaxRate * 100)}%", Ui.Ink)}   {Tag($"Watch {t.Watch}", Ui.Ink)}{(t.FestivalDays > 0 ? "   " + Tag("Festival!", Ui.Gold) : "")}";
        p.Meter("Mandate", t.Mandate, $"{Math.Round(t.Mandate)}", t.Mandate < 30);
        p.Note("The townsfolk's approval of you. It follows their mood and falls when you rule by decree. A popular mayor draws settlers.");
        p.Text(facts, 12);
        p.Heading("Before the council");
        if (pending == null) p.Note("No motion before the council. Councillors bring one when their portfolio needs it.");
        else
        {
            int yes = pending.Votes.Count(v => v.Yes), no = pending.Votes.Count - yes;
            bool? majority = yes == no ? null : yes > no;
            int hoursLeft = pending.DeadlineTick - t.Tick;
            var votes = string.Join("\n", pending.Votes.Select(v =>
            {
                var seat = t.Seats.First(x => x.Def.Id == v.Seat);
                return $"{Ui.Esc(seat.Def.Title)}   {Ui.C(v.Yes ? Ui.Good : Ui.Bad, v.Yes ? "[b]Aye[/b]" : "[b]Nay[/b]")}";
            }));
            string Decree(bool signIt) => majority != null && majority != signIt ? " (decree)" : "";
            int days = (int)Math.Ceiling(hoursLeft / 24.0);
            p.Card($"[b]{Ui.Esc(Ui.Cap(pending.Text))}.[/b]\n{Ui.C(Ui.Ink2, $"Moved by {AgentLink(t.Agent(pending.ProposerId))}. {(pending.Cost > 0 ? $"Costs {pending.Cost}g. " : "")}If you say nothing in {days} {(hoursLeft > 24 ? "days" : "day")}, the council's vote stands.")}\n{votes}", Ui.Gold);
            int id = pending.Id;
            p.Buttons(($"Sign{Decree(true)}", true, true, () => Act(() => _sim.DecideMotion(id, true))), ($"Veto{Decree(false)}", true, false, () => Act(() => _sim.DecideMotion(id, false))));
            p.Note("Going against the council's majority is a decree: it costs Mandate and angers the councillors you overrule.");
        }
        p.Heading("Seats");
        foreach (var s in t.Seats)
        {
            if (s.HolderId is not int holder)
            {
                p.Row($"{Ui.C(Ui.Ink2, $"[b]{Ui.Esc(s.Def.Title)}[/b]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{(t.Population < s.Def.UnlockPopulation ? $"Opens when {s.Def.UnlockPopulation} people live here" : "Vacant, to be filled tomorrow")}[/font_size]")}");
                continue;
            }
            var a = t.Agent(holder);
            string mood = s.Loyalty >= 60 ? "Loyal" : s.Loyalty >= 25 ? "Wary" : "Disloyal";
            int skill = Council.Competence(t, s);
            p.Row($"[b]{Ui.Esc(s.Def.Title)}[/b]  {Tag(mood, s.Loyalty < 25 ? Ui.Bad : s.Loyalty >= 60 ? Ui.Good : Ui.Ink2)}\n{AgentLink(a)} {Ui.C(Ui.Ink2, $"[font_size=12]· {a.Race} {a.Profession} · skill {(skill >= 0 ? "+" : "")}{skill} · loyalty {Math.Round(s.Loyalty)}\nMinds {Ui.Esc(s.Def.Portfolio)}. Wants {Ui.Esc(s.Def.Agenda)}.[/font_size]")}");
        }
        var history = t.Motions.Where(m => m.Status != MotionStatus.Pending).TakeLast(8).Reverse().ToList();
        if (history.Count > 0)
        {
            p.Heading("Past motions");
            foreach (var m in history)
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {m.ProposedTick / 24 + 1}[/font_size]")}  {Ui.Esc(Ui.Cap(m.Text))}  {Tag(m.Status == MotionStatus.Passed ? $"passed{(m.Decree ? " by decree" : "")}" : $"failed{(m.Decree ? " by decree" : "")}", m.Status == MotionStatus.Passed ? Ui.Good : Ui.Bad)}", 13);
        }
        p.Commit();
    }

    // ---------- Bounties and the road ----------

    private void RenderBounties()
    {
        var p = _panes[Tab.Bounties];
        var t = _sim.Town;
        p.Begin();
        p.Note("You are the mayor. Post bounties from the treasury; heroes at the tavern form parties and take them on. Once a party leaves the town's light you will not see it again until it comes home to report.");
        int idle = t.Heroes.Count(a => !a.IsAway);
        p.Note(t.Taverns.Count == 0
            ? "Heroes need a tavern before they will come to town."
            : $"{idle} {(idle == 1 ? "hero is" : "heroes are")} in town. Parties need at least three, and tougher sites want higher levels.");

        var road = t.Parties.Where(x => !x.Returned).ToList();
        if (road.Count > 0)
        {
            p.Heading("On the road");
            foreach (var party in road)
            {
                var site = Quests.Site(t, party.SiteId)!;
                int gone = (t.Tick - party.DepartTick) / 24;
                int due = SimTime.DayOf(party.ReturnTick);
                int slots = Knowledge.SlotsLeft(t, party);
                string members = string.Join(", ", party.Members.Select(id => AgentLink(t.Agent(id), t.Agent(id).GivenName)));
                string where = party.RecalledTick != null ? "Called home" : $"Gone {Ui.Days(gone)} to {SiteLink(site.Def)}";
                string slotNote = slots == 0
                    ? Ui.C(Ui.Ink2, Knowledge.Slots(t, party) == 0 ? "No caster with them: no sending stone." : "No spell slots left for a sending.")
                    : Ui.C(Ui.Ink2, $"{slots} spell {(slots == 1 ? "slot" : "slots")} for sendings. Each one costs a slot{(party.Members.Any(id => t.Agent(id).Hero!.Cls.Healer) ? ", and a healer's slot is a heal they won't have in the dungeon" : "")}.");
                int id = party.Id;
                p.Card($"[b]The {Ui.Esc(party.Name)}[/b]  {Tag($"due back day {due}", Ui.Gold)}\n{where} · {members}\n[font_size=12]{slotNote}[/font_size]");
                if (Knowledge.Slots(t, party) > 0)
                    p.Buttons(
                        ("Ask for word", slots > 0, false, () => Act(() => _sim.SendWord(id))),
                        ("Call them home", slots > 0 && Knowledge.CanRecall(t, party), false, () => Act(() => _sim.SendWord(id, recall: true))));
            }
        }

        RenderEyes(p, t);

        p.Heading("Bounty board");
        var scryer = Knowledge.Scryers(t).FirstOrDefault();
        bool freeScry = Watch.FreeScryingsLeft(t) > 0;
        bool canScout = Watch.Watchers(t).Count > 0;
        foreach (var s in t.Sites)
        {
            // The board only lists what the town has heard of.
            if (s.Known == SiteKnowledge.Unheard) continue;
            var live = t.Bounties.FirstOrDefault(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            var party = live?.PartyId is int pid ? t.Parties.FirstOrDefault(x => x.Id == pid) : null;
            int reward = Quests.SuggestedReward(Math.Max(1, s.BelievedTier));
            string status;
            var buttons = new List<(string, bool, bool, Action)>();
            if (s.ThoughtCleared) status = Tag("Cleared", Ui.Good);
            else if (party != null && !party.Returned) status = Tag($"{Ui.Esc(party.Name)} on the road", Ui.Gold);
            else if (live != null) status = Tag($"Bounty posted: {Math.Round(live.Reward)}g", Ui.Gold);
            else
            {
                status = s.Attempts > 0 ? Tag($"{s.Attempts} failed {(s.Attempts == 1 ? "attempt" : "attempts")}", Ui.Bad) : Tag("Unexplored");
                string sid = s.Def.Id;
                buttons.Add(($"Post {reward}g", t.Treasury >= reward, true, () => Act(() => _sim.PostBounty(sid, reward))));
            }
            if (!s.ThoughtCleared && Watch.LiveOf(t, s) == null)
            {
                double cost = Knowledge.ScryCost(t, s.Def);
                string sid = s.Def.Id;
                buttons.Add((freeScry ? "Scry free" : $"Scry {Math.Round(cost)}g", freeScry || scryer != null && t.Treasury >= cost, false, () => Act(() => _sim.CommissionScrying(sid))));
                if (Watch.ScoutAt(t, sid) == null) buttons.Add(("Scout", canScout, false, () => Act(() => _sim.PostScout(sid))));
            }
            int bt = Math.Clamp(s.BelievedTier, 1, 3);
            string tier = new string('◆', bt) + new string('◇', 3 - bt);
            string word = s.Known == SiteKnowledge.Rumoured && !s.ThoughtCleared
                ? Ui.C(Ui.Ink2, $" [font_size=11]rumour: {Ui.Esc(s.Believed)} ({Rumors.Trust(s.Trust)})[/font_size]")
                : "";
            p.Row($"{SiteLink(s.Def)} {Ui.C(Ui.Gold, $"[font_size=11]{tier}[/font_size]")}{word}\n{status}", buttons.ToArray());
        }
        p.Note(freeScry
            ? $"Scrying: the Scrying Pool will show one site live for {t.Content.Knowledge.ScryHours} hours today, free."
            : scryer != null
                ? $"Scrying: {scryer.Name} the {scryer.Hero!.Cls.Name} will show you a site live for {t.Content.Knowledge.ScryHours} hours."
                : $"Scrying needs a {ScryClasses(t)} of level {t.Content.Knowledge.ScryMinLevel} or more in town.");
        p.Note(canScout
            ? $"Scouting: {Watch.Watchers(t)[0].Name} the {Watch.Watchers(t)[0].Hero!.Cls.Name} will watch a site from cover for up to {Ui.Days(t.Content.Knowledge.Scouts.WatchDays)}, {t.Content.Knowledge.Scouts.WagePerDay}g a day."
            : "Scouting needs a ranger, rogue, monk or cartographer in town and rested.");

        var books = t.AccountsYesterday.Where(kv => Math.Abs(kv.Value) >= 0.5).OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (books.Count > 0)
        {
            double net = books.Sum(kv => kv.Value);
            p.Heading("Yesterday's books");
            p.Card(string.Join("\n", books.Select(kv => $"{Ui.Esc(kv.Key)}   {Ui.C(kv.Value >= 0 ? Ui.Good : Ui.Bad, $"[b]{Sign(kv.Value)}[/b]")}")) + $"\n[b]Net[/b]   {Ui.C(net >= 0 ? Ui.Good : Ui.Bad, $"[b]{Sign(net)}[/b]")}");
        }
        p.Commit();
    }

    private static string ScryClasses(Town t) =>
        string.Join(" or ", t.Content.Library.Spell(t.Content.Knowledge.ScrySpell)?.Classes ?? []);

    /// <summary>Scouts in the field, mirrors at sites, and the works that see for the town.</summary>
    private void RenderEyes(Pane p, Town t)
    {
        var scouts = t.Scouts.Where(x => x.Out).ToList();
        var mirrors = t.Mirrors.Where(m => m.Intact).ToList();
        p.Heading("Eyes in the wilds");
        foreach (var sc in scouts)
        {
            var hero = t.Agent(sc.HeroId);
            var site = Quests.Site(t, sc.SiteId)!;
            string state = sc.Mirror
                ? (t.Tick < sc.ArriveTick ? $"carrying a mirror to {SiteLink(site.Def)}" : $"back from hiding a mirror at {SiteLink(site.Def)}")
                : t.Tick < sc.ArriveTick ? $"on the way to {SiteLink(site.Def)}"
                : t.Tick < sc.LeaveTick ? $"watching {SiteLink(site.Def)}, rations for {Ui.Days((int)Math.Ceiling((sc.LeaveTick - t.Tick) / 24.0))}"
                : $"coming home from {SiteLink(site.Def)}";
            int id = sc.Id;
            bool canRecall = !sc.Mirror && t.Tick < sc.LeaveTick;
            p.Row($"{AgentLink(hero)} the {Ui.Esc(hero.Hero!.Cls.Name)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{state}[/font_size]")}",
                canRecall ? [("Call home", true, false, () => Act(() => _sim.RecallScout(id)))] : []);
        }
        foreach (var m in mirrors)
        {
            var site = Quests.Site(t, m.SiteId)!;
            p.Row($"Mirror at {SiteLink(site.Def)}  {Tag("live", Ui.Good)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Hidden on day {SimTime.DayOf(m.PlacedTick)}. It shows the site until someone there finds it.[/font_size]")}");
        }
        foreach (var w in t.Content.Knowledge.Works)
        {
            var def = t.Content.Building(w.Building);
            var relic = t.Content.Library.Item(w.Relic);
            var b = Watch.WorkBuilding(t, w);
            string what = w.Reach > 0
                ? $"Shows every site within {Ui.Days(w.Reach)} of town live, always."
                : $"{(w.Scryings == 1 ? "One free scrying" : $"{w.Scryings} free scryings")} a day.";
            string head = $"[b]{Ui.Esc(def.Name)}[/b]{(relic != null ? Ui.C(Ui.Ink2, $" · houses {Ui.Esc(Library.A(relic.Name))}") : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{what}[/font_size]")}";
            if (Watch.HasWork(t, w))
                p.Row($"{head}  {Tag(t.Relics.Contains(w.Relic) && b?.Complete != true ? "Relic in the hall" : "Built", Ui.Good)}");
            else if (b != null)
                p.Row($"{head}  {Tag($"Building, {Math.Floor(100 * b.BuildProgress / b.Def.BuildHours)}%", Ui.Gold)}");
            else if (t.Population < w.Population)
                p.Row($"{head}  {Tag($"Needs {w.Population} residents")}");
            else
            {
                double cost = Watch.WorkCost(t, w);
                string bid = w.Building;
                p.Row(head, ($"Build {Math.Round(cost)}g", t.Treasury >= cost, true, () => Act(() => _sim.BuildSightWork(bid))));
            }
        }
        // The town's first institution: a temple to its god, once there are people enough to fill it.
        var temple = t.Buildings.FirstOrDefault(b => b.Def.Id == t.Content.Shrines.Temple);
        string thead = $"[b]Temple of {Ui.Esc(t.Content.Shrines.Deity)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Heals heroes, raises the fallen, keeps holy days and pays its own bounties. {Shrines.Followers(t).Count} {Ui.Esc(t.Content.Shrines.Devout)} in town.[/font_size]")}";
        if (temple?.Complete == true) p.Row($"{thead}  {Tag($"Built · coffers {Math.Round(temple.Cash)}g", Ui.Good)}");
        else if (temple != null) p.Row($"{thead}  {Tag($"Building, {Math.Floor(100 * temple.BuildProgress / temple.Def.BuildHours)}%", Ui.Gold)}");
        else if (t.Population < t.Content.Shrines.TemplePopulation) p.Row($"{thead}  {Tag($"Needs {t.Content.Shrines.TemplePopulation} residents")}");
        else
        {
            double tcost = Shrines.TempleCost(t);
            p.Row(thead, ($"Build {Math.Round(tcost)}g", t.Treasury >= tcost, true, () => Act(() => _sim.BuildTemple())));
        }
        if (scouts.Count == 0 && mirrors.Count == 0)
            p.Note("Post a scout to watch a site from cover, or hide a mirror there (from the site's page) for a view that lasts.");
    }

    // ---------- Chronicles ----------

    private void RenderChronicles()
    {
        var p = _panes[Tab.Chronicles];
        var t = _sim.Town;
        p.Begin();
        if (t.Chronicles.Count == 0) p.Note("No expeditions yet. Post a bounty and wait for a party to form at the tavern. Their story comes back with them.");
        foreach (var c in Enumerable.Reverse(t.Chronicles))
        {
            var color = c.Outcome == ExpeditionOutcome.Cleared ? Ui.Good : c.Outcome == ExpeditionOutcome.Wiped ? Ui.Bad : Ui.Ink2;
            string head = $"[url=chron:{c.Id}][b]{Ui.Esc(c.PartyName)}[/b][/url] at {Ui.Esc(c.SiteName)}  {Tag(c.Outcome.ToString().ToLowerInvariant(), color)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Day {c.DepartDay}–{c.ReturnDay} · {Ui.Esc(string.Join(", ", c.Members))}[/font_size]")}";
            if (_openChronicles.Contains(c.Id))
                head += "\n" + string.Join("\n", c.Lines.Select(l => $"{Ui.C(color, "│")} [font_size=13]{Ui.Esc(l)}[/font_size]"));
            p.Row(head);
        }
        p.Commit();
    }

    // ---------- Inspect ----------

    private void RenderInspect()
    {
        var p = _panes[Tab.Inspect];
        var t = _sim.Town;
        var sel = _map.Selection;
        p.Begin();
        if (sel == null) p.Note("Click a building, a person or a site on the map.");
        else if (sel.Kind == "agent") InspectAgent(p, t, t.Agent(sel.Id));
        else if (sel.Kind == "building") InspectBuilding(p, t, t.Building(sel.Id));
        else if (Quests.Site(t, sel.SiteId) is SiteState s) InspectSite(p, t, s);
        p.Commit();
    }

    private void InspectAgent(Pane p, Town t, Agent a)
    {
        string home = a.HomeId is int h ? t.Building(h).Name : a.Hero?.Lodging == true ? "a room at the tavern" : "nowhere";
        string job = a.JobId is int j ? t.Building(j).Name : "out of work";
        var seat = t.Seats.FirstOrDefault(x => x.HolderId == a.Id);
        var scouting = t.Scouts.LastOrDefault(x => x.HeroId == a.Id && x.Out);
        string status = a.Departed ? (a.Dead ? "Dead" : "Left town")
            : scouting != null ? $"Out {(scouting.Mirror ? "hiding a mirror at" : "scouting")} {Quests.Site(t, scouting.SiteId)!.Def.Name}"
            : a.IsAway ? "Away on an expedition, somewhere in the fog" : a.Activity.ToString();
        string describe = a.Describe()[(a.Name.Length + 2)..];
        p.Text($"[font_size=19][b]{Ui.Esc(a.Name)}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(describe))} · {Ui.Esc(a.TraitList())}")}");
        if (seat != null) p.Text($"{Tag(seat.Def.Title, Ui.Gold)} loyalty {Math.Round(seat.Loyalty)}");
        if (a.Hero is HeroSheet hs)
        {
            p.Text($"HP {hs.Hp}/{hs.MaxHp} · {hs.Xp} XP · {hs.Kills} kills · {hs.Expeditions} expeditions");
            var cls = hs.Cls;
            var spells = new List<string>();
            if (cls.HealSpell != null) spells.Add($"{cls.HealSpell.Name} ({cls.HealsAt(hs.Level)} a delve)");
            if (cls.AreaSpell != null) spells.Add(cls.AreaSpell.Name);
            string role = cls.Role == "" ? "" : $"{Ui.Cap(cls.Role)}{(cls.Magic != "" ? $", {cls.Magic} caster" : "")}";
            p.Text(Ui.C(Ui.Ink2, $"{Ui.Esc(role)}{(spells.Count > 0 ? $" · casts {Ui.Esc(string.Join(", ", spells))}" : "")}"), 13);
            if (hs.Items.Count > 0) p.Text($"Carries {Ui.Esc(string.Join(", ", hs.Items))}{(hs.WeaponBonus > 0 ? $" · weapon +{hs.WeaponBonus}" : "")}{(hs.ArmorBonus > 0 ? $" · armour +{hs.ArmorBonus}" : "")}", 13);
        }
        p.Text($"{Ui.Esc(status)} · {Math.Floor(a.Gold)} gold · mood {Math.Round(a.LastDayMood)}");
        p.Text($"Lives at {Ui.Esc(home)}; {(a.Hero != null ? "lives by bounties" : $"works at {Ui.Esc(job)}")}.");
        if (a.SpouseId is int sp) p.Text($"Married to {AgentLink(t.Agent(sp))}");
        p.Text(Ui.C(Ui.Ink2, a.Abilities.ToString()), 12);
        var friends = a.Opinions.Where(kv => kv.Value >= 40).OrderBy(kv => kv.Key).Select(kv => AgentLink(t.Agent(kv.Key))).ToList();
        var foes = a.Opinions.Where(kv => kv.Value <= -30).OrderBy(kv => kv.Key).Select(kv => AgentLink(t.Agent(kv.Key))).ToList();
        if (friends.Count > 0) p.Text($"Friends: {string.Join(", ", friends)}");
        if (foes.Count > 0) p.Text($"Grudges: {string.Join(", ", foes)}");
        var history = t.Ledger.About(a.Id).Where(e => e.Id > 1).TakeLast(12).Reverse().ToList();
        if (history.Count > 0)
        {
            p.Heading("Story so far");
            foreach (var e in history) p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {e.Tick / 24 + 1}[/font_size]")}  {LinkNames(e)}", 13);
        }
    }

    private void InspectBuilding(Pane p, Town t, Building b)
    {
        string owner = b.OwnerId is int o ? AgentLink(t.Agent(o)) : "the town";
        string People(List<int> ids) => ids.Count == 0 ? "none" : string.Join(", ", ids.Select(id => AgentLink(t.Agent(id))));
        var here = t.Agents.Where(a => !a.Departed && a.LocationId == b.Id && a.Activity != Activity.Sleeping).ToList();
        string state = b.IsSite ? $"Under construction, {Math.Floor(100 * b.BuildProgress / b.Def.BuildHours)}% done." : b.Closed ? "Closed." : b.ForSale ? "For sale." : "Open.";
        p.Text($"[font_size=19][b]{Ui.Esc(Ui.Cap(b.Name))}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(b.Def.Name))} · owned by ")}{owner}");
        p.Text(state);
        if (b.Def.Jobs > 0) p.Text($"Workers ({b.Workers.Count}/{b.Def.Jobs}): {People(b.Workers)}\nCash {Math.Floor(b.Cash)}g");
        if (b.Def.Housing > 0) p.Text($"Residents ({b.Residents.Count}/{b.Def.Housing}): {People(b.Residents)}");
        if (here.Count > 0) p.Text($"Here now: {string.Join(", ", here.Select(a => AgentLink(a)))}");
    }

    private void InspectSite(Pane p, Town t, SiteState s)
    {
        var pin = _map.Geo.Pins.GetValueOrDefault(s.Def.Id);
        var sight = _map.Fog.SightOf(s.Def.Id, pin);
        var report = _map.Fog.LastReport(s.Def.Id);
        var scry = Knowledge.ActiveScrying(t, s.Def.Id);
        var live = Watch.LiveOf(t, s);
        var scout = Watch.ScoutAt(t, s.Def.Id);
        p.Text($"[font_size=19][b]{Ui.Esc(Ui.Cap(s.Def.Name))}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"Tier {s.Def.Tier} · {Ui.Days(s.Def.Distance)} away")}");
        string seen = sight switch
        {
            Sight.Live when scry != null => Ui.C(new Color("b48cff"), $"[b]Scrying[/b] {(scry.CasterId == 0 ? "in the Scrying Pool" : $"by {Ui.Esc(t.Agent(scry.CasterId).Name)}")}, {scry.EndTick - t.Tick} hours left. You see it as it is now."),
            Sight.Live when live == LiveSource.Mirror => Ui.C(new Color("cfe3f0"), "[b]Mirror[/b]: the hidden mirror shows it as it is now, until someone finds it."),
            Sight.Live when live == LiveSource.Scout && scout != null => Ui.C(Ui.Good, $"[b]Watched[/b] by {Ui.Esc(t.Agent(scout.HeroId).Name)} from cover, rations for {Ui.Days((int)Math.Ceiling((scout.LeaveTick - t.Tick) / 24.0))}."),
            Sight.Live when live == LiveSource.Spire => Ui.C(Ui.Gold, "[b]In the Spire's sight[/b], day and night."),
            Sight.Live => "In sight of the town.",
            Sight.Scouted when report != null => t.Day - report.Day > Fog.StaleDays
                ? $"Last seen on day {report.Day}, in a report {t.Day - report.Day} days old. Things may have changed."
                : $"Scouted on day {report.Day}. You know it as the party described it, not as it is now.",
            _ => "Only a name in tavern gossip. Nobody who went there has come back to describe it.",
        };
        p.Text(seen);
        // What the taverns say, newest first: a teller, how far to trust them, and whether it proved true.
        var talk = Rumors.About(t, s.Def.Id).Take(4).ToList();
        if (talk.Count > 0)
        {
            p.Heading("Tavern talk");
            foreach (var r in talk)
            {
                string verdict = r.Proven == true ? Tag("Proved true", Ui.Good) : r.Proven == false ? Tag("Proved wrong", Ui.Bad) : Tag(Ui.Cap(Rumors.Trust(r.Reliability)));
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {SimTime.DayOf(r.Tick)}[/font_size]")} {Ui.Esc(r.Text)} {verdict}");
            }
        }
        if (sight != Sight.Rumoured)
        {
            bool cleared = sight == Sight.Live ? s.Cleared : report?.Outcome == ExpeditionOutcome.Cleared;
            p.Text(cleared ? "Cleared. The road is safe." : $"Still dangerous. {s.Attempts} {(s.Attempts == 1 ? "party has" : "parties have")} tried.");
            if (s.Def.Boss.Kind == "combat") p.Text($"Its master: {Ui.Esc(s.Def.Boss.Text)}.");
        }
        if (sight == Sight.Live && live != null)
        {
            var there = t.Parties.Where(x => !x.Returned && x.SiteId == s.Def.Id && Knowledge.LegOf(t, x) == Leg.AtSite).ToList();
            foreach (var party in there) p.Text($"You watch the {Ui.Esc(party.Name)} at the mouth of the site, readying to go in.");
        }
        var chrons = t.Chronicles.Count(c => c.SiteName == s.Def.Name);
        if (chrons > 0) p.Text($"{chrons} {(chrons == 1 ? "chronicle" : "chronicles")} written here. See the Chronicles tab.");
        if (s.Cleared) return;
        string sid = s.Def.Id;
        p.Heading("Eyes on it");
        if (live == null)
        {
            bool free = Watch.FreeScryingsLeft(t) > 0;
            double cost = Knowledge.ScryCost(t, s.Def);
            var scryer = Knowledge.Scryers(t).FirstOrDefault();
            p.Buttons((free ? "Scry it free, from the pool" : $"Commission a scrying, {Math.Round(cost)}g", free || scryer != null && t.Treasury >= cost, true, () => Act(() => _sim.CommissionScrying(sid))));
            p.Note(free ? $"The Scrying Pool shows it for {t.Content.Knowledge.ScryHours} hours." : scryer != null ? $"{scryer.Name} would cast it, for {t.Content.Knowledge.ScryHours} hours." : $"Needs a {ScryClasses(t)} of level {t.Content.Knowledge.ScryMinLevel} or more in town.");
        }
        if (scout != null && live != LiveSource.Scout)
            p.Note($"{t.Agent(scout.HeroId).Name} is {(t.Tick < scout.ArriveTick ? "on the way to watch it" : "on the way home")}.");
        if (scout == null)
        {
            var watcher = Watch.Watchers(t).FirstOrDefault();
            var sr = t.Content.Knowledge.Scouts;
            p.Buttons(($"Post a scout, {sr.WagePerDay}g a day", watcher != null, false, () => Act(() => _sim.PostScout(sid))));
            p.Note(watcher != null
                ? $"{watcher.Name} the {watcher.Hero!.Cls.Name} would walk {Ui.Days(s.Def.Distance)} out and watch for up to {Ui.Days(sr.WatchDays)}. Tier {s.Def.Tier} sites spot watchers more often."
                : "Needs a ranger, rogue, monk or cartographer in town and rested.");
        }
        if (Watch.MirrorAt(t, sid) == null && !Watch.MirrorUnderway(t, sid))
        {
            var mr = t.Content.Knowledge.Mirror;
            var caster = Watch.MirrorCasters(t).FirstOrDefault();
            var spell = t.Content.Library.Spell(mr.Spell);
            p.Buttons(($"Hide a mirror here, {Math.Round(mr.Cost)}g", caster != null && t.Treasury >= mr.Cost, false, () => Act(() => _sim.PlantMirror(sid))));
            p.Note(caster != null
                ? $"{caster.Name} binds a pair with {spell?.Name ?? "the Mirror Network"}; a scout hides one here. You see the site live until someone finds it."
                : $"Needs a {string.Join(" or ", spell?.Classes ?? [])} of level {mr.MinLevel} or more in town to bind the mirrors.");
        }
    }
}
