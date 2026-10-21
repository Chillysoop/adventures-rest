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
        var stage = Stages[t.Enemy.Stage];
        double? est = Threat.EstimatedStrength(t);
        double def = Threat.Defense(t);
        double scale = Math.Max(Math.Max(def, est ?? 0), 1);
        var camp = t.Sites.FirstOrDefault(s => s.Def.Id == t.Content.Enemy.Outpost.Id);
        int home = t.Heroes.Count(a => !a.IsAway);

        p.Begin();
        string arrival = t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at
            ? "\n" + Ui.C(Ui.Bad, $"[b]They reach the {Garrison.Place(t, t.Enemy.Approach)} in {Ui.Days((int)Math.Ceiling((at - t.Tick) / 24.0))}. Call the watch to arms there; heroes in town will face the warlord.[/b]")
            : "";
        var threat = Assess.WarbandThreat(t);
        string card = $"{Ui.KindIcon("warband", 22)}[font_size=17][b]{Ui.Esc(Ui.Cap(t.Content.Enemy.Name))}[/b][/font_size]\n{stage.Text}{arrival}";
        p.Add($"warband{threat?.Id}{card}", () =>
        {
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", Ui.Box(Ui.Panel2, Ui.Panel2, 10, 0, 12, 10));
            var h = new HBoxContainer();
            h.AddThemeConstantOverride("separation", 12);
            h.AddChild(Foes.Warband(t, 60));
            var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            v.AddThemeConstantOverride("separation", 6);
            v.AddChild(Ui.Rich(card));
            if (threat is Banded b) v.AddChild(Foes.Bar("Threat", b, Foes.Worse(b), "The warband's strength as the scouts guess it, against the town's defence."));
            h.AddChild(v);
            panel.AddChild(h);
            return panel;
        });
        p.Heading("Strength");
        // A broken warband has no strength left to guess at.
        if (Threat.IsActive(t) && est is double e) p.Meter("Warband", 100 * e / scale, $"~{Math.Round(e)}", true);
        else if (Threat.IsActive(t)) p.Text($"{Ui.C(Ui.Ink2, "Warband")}    [i]Unknown. Nobody has seen it yet.[/i]", 13);
        p.Meter("Your defence", 100 * def / scale, $"{Math.Round(def)}");
        p.Note($"Defence counts the watch, heroes in town, a militia of townsfolk, and the walls (×{Threat.Fortification(t):0.00}).{(Threat.IsActive(t) ? " The warband grows while dungeons go uncleared." : "")}");
        RenderGuard(p, t);
        RenderWorks(p, t);
        p.Heading("Heroes");
        p.Row($"[b]Heroes in town[/b]  {Tag(home.ToString())}\n{Ui.C(Ui.Ink2, "[font_size=12]Only heroes can kill the warlord when he comes.[/font_size]")}");
        if (camp != null && !camp.Cleared) p.Note("The war camp feeds the warband. Post a bounty on it in the Bounties tab to strike first.");
        RenderSellswords(p, t);
        RenderClockwork(p, t);
        p.Commit();
    }

    /// <summary>Sellsword companies: hire one onto the walls, send it against a menace, or pay it off.</summary>
    private void RenderSellswords(Pane p, Town t)
    {
        p.Heading("Sellswords");
        var targets = Mercenaries.Targets(t);
        foreach (var c in Mercenaries.Rules(t).Companies)
        {
            var h = Mercenaries.Hired(t, c.Id);
            string? why = Mercenaries.HireBlocked(t, c.Id);
            var id = c.Id;
            string head = $"[b]{Ui.Esc(Ui.Cap(c.Name))}[/b]  {Tag($"strength {c.Strength:0}")}";
            if (h == null)
            {
                if (c.Tier != "" && !City.Reached(t, c.Tier))
                {
                    p.Row($"{head}  {Tag($"needs {City.TierName(t, c.Tier).ToLowerInvariant()}")}");
                    continue;
                }
                p.Row($"{head}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(c.Blurb)} {c.Fee:N0}g to sign, then {c.Wage:N0}g a day.[/font_size]")}",
                    ($"Hire · {c.Fee:N0}g", why == null, true, () => Act(() => _sim.HireMercenaries(id))));
                continue;
            }
            var foe = h.TargetId is int tid ? t.Saga.Menaces.FirstOrDefault(m => m.Id == tid) : null;
            string where = h.ArriveTick is int at ? Tag($"marching on {foe?.SeatName ?? "the foe"}, {Ui.Days(Math.Max(1, (int)Math.Ceiling((at - t.Tick) / 24.0)))}", Ui.Gold)
                : h.HomeTick is int home ? Tag($"coming home, {Ui.Days(Math.Max(1, (int)Math.Ceiling((home - t.Tick) / 24.0)))}", Ui.Gold)
                : Tag("on the walls", Ui.Good);
            p.Row($"{head}  {where}\n{Ui.C(Ui.Ink2, $"[font_size=12]{c.Wage:N0}g a day. Adds its strength to your defence while it is home.[/font_size]")}",
                ("Dismiss", !h.Away, false, () => Act(() => _sim.DismissMercenaries(id))));
            p.Meter("Fit to fight", 100 * h.Strength / c.Strength, $"{Math.Round(h.Strength)}", h.Strength < c.Strength * 0.5);
            if (h.Away) continue;
            double cost = Mercenaries.CampaignCost(t, c.Id);
            foreach (var m in targets.Take(4))
            {
                int mid = m.Id;
                string odds = h.Strength >= m.Power * t.Content.Mercenaries.StormRatio && !m.BigBad ? "they could storm its seat"
                    : h.Strength >= m.Power ? "they should beat it in the field"
                    : h.Strength >= m.Power * t.Content.Mercenaries.RoutBelow ? "a bloody, even fight" : "they would likely be routed";
                p.Row($"{Ui.C(Ui.Ink2, $"[font_size=12]March on [b]{Ui.Esc(Ui.Cap(m.Name))}[/b] at {Ui.Esc(m.SeatName)}, about {Math.Round(m.Power / 10) * 10:0} strong: {odds}.[/font_size]")}",
                    ($"March · {cost:N0}g", Mercenaries.MarchBlocked(t, c.Id, mid) == null, false, () => Act(() => _sim.MarchMercenaries(id, mid))));
            }
        }
        p.Note(targets.Count == 0
            ? "Hired companies stand on the walls with the watch. Once the town knows where a named menace sits, you can send them against it."
            : "A campaign costs a war chest of wages. Sellswords can break a menace's hold and its seat, but only heroes can end the greatest foe.");
    }

    /// <summary>Clockwork wardens: build one for the walls, or break one up.</summary>
    private void RenderClockwork(Pane p, Town t)
    {
        var r = Clockwork.Rules(t);
        var c = t.Clockwork;
        p.Heading("Clockwork wardens");
        if (r.Tier != "" && !City.Reached(t, r.Tier) && c.Wardens == 0)
        {
            p.Note($"Tireless brass guards that take no bribes. A {City.TierName(t, r.Tier).ToLowerInvariant()} with a mage tower or a guildhall can build them.");
            return;
        }
        string? why = Clockwork.BuildBlocked(t);
        p.Row($"[b]On the walls[/b]  {Tag($"{c.Wardens} of {r.Max}", c.Wardens > 0 ? Ui.Good : Ui.Ink2)}{(c.Seized > 0 ? "  " + Tag($"{c.Seized} stolen", Ui.Bad) : "")}\n{Ui.C(Ui.Ink2, $"[font_size=12]Each adds {r.Strength:0} strength and helps the watch catch a schemer's agents, however crooked its captain. {r.Upkeep:0}g a day to wind. Townsfolk don't trust them (mood {Clockwork.Mood(t):0.0}), and the Quiet Hand or a scheming foe may steal one; a mage tower keeps the keys safer.{(why != null ? $" Can't build: {Ui.Esc(why)}." : "")}[/font_size]")}",
            ($"Build · {r.Cost:N0}g", why == null, true, () => Act(() => _sim.BuildWarden())),
            ("Scrap", c.Wardens > 0, false, () => Act(() => _sim.ScrapWarden())));
    }

    /// <summary>The terms the bounty board posts on (<see cref="Terms"/>).</summary>
    private string _terms = "slay";

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
        p.Begin();
        string facts = $"{Tag($"Tax {Math.Round(t.TaxRate * 100)}%", Ui.Ink)}   {Tag($"Watch {t.Watch}", Ui.Ink)}{(t.FestivalDays > 0 ? "   " + Tag("Festival!", Ui.Gold) : "")}";
        p.Meter("Mandate", t.Mandate, $"{Math.Round(t.Mandate)}", t.Mandate < 30);
        p.Note("The townsfolk's approval of you. It follows their mood, fades back toward 50 unless you keep earning it, and falls when you rule by decree. A popular mayor draws settlers.");
        p.Text(facts, 12);
        RenderTaxes(p, t);
        var festive = Council.FestivalBlocked(t);
        p.Buttons(($"Hold a festival · {Js.Str(Council.FestivalCost(t))}g", festive == null, false, () => Act(() => _sim.HoldFestival())));
        p.Note(festive == null
            ? $"Mood up for {t.Content.Council.FestivalDays} days · Mandate +{Js.Str(t.Content.Council.FestivalMandate)}"
            : Ui.Cap(festive));
        RenderMotion(p, t);
        p.Heading("Seats");
        foreach (var s in t.Seats)
        {
            if (s.HolderId is not int holder)
            {
                p.Row($"{Ui.C(Ui.Ink2, $"[b]{Ui.Esc(s.Def.Title)}[/b]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{(t.Population < s.Def.UnlockPopulation ? $"Opens when {s.Def.UnlockPopulation} households live here" : "Vacant, to be filled tomorrow")}[/font_size]")}");
                continue;
            }
            var a = t.Agent(holder);
            string mood = s.Loyalty >= 60 ? "Loyal" : s.Loyalty >= 25 ? "Wary" : "Disloyal";
            int skill = Council.Competence(t, s);
            p.Row($"[b]{Ui.Esc(s.Def.Title)}[/b]  {Tag(mood, s.Loyalty < 25 ? Ui.Bad : s.Loyalty >= 60 ? Ui.Good : Ui.Ink2)}\n{AgentLink(a)} {Ui.C(Ui.Ink2, $"[font_size=12]· {a.Race} {a.Profession} · skill {(skill >= 0 ? "+" : "")}{skill} · loyalty {Math.Round(s.Loyalty)}\nMinds {Ui.Esc(s.Def.Portfolio)}. Wants {Ui.Esc(s.Def.Agenda)}.[/font_size]")}");
            RenderSecret(p, t, a);
        }
        RenderGuilds(p, t);
        var history = t.Motions.Where(m => m.Status != MotionStatus.Pending).TakeLast(8).Reverse().ToList();
        if (history.Count > 0)
        {
            p.Heading("Past motions");
            foreach (var m in history)
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {m.ProposedTick / 24 + 1}[/font_size]")}  {Ui.Esc(Ui.Cap(m.Text))}  {Tag(m.Status == MotionStatus.Passed ? $"passed{(m.Decree ? " by decree" : "")}" : $"failed{(m.Decree ? " by decree" : "")}", m.Status == MotionStatus.Passed ? Ui.Good : Ui.Bad)}", 13);
        }
        p.Commit();
    }

    /// <summary>The motion before the council, and any traitor the taverns whisper of: the council window's and the tray's.</summary>
    private void RenderMotion(Pane p, Town t)
    {
        var pending = Council.PendingMotion(t);
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
            // A secret in hand swings a councillor's vote, so the mayor can carry the hall without a decree.
            var levers = pending.Votes
                .Where(v => v.Seat != pending.Proposer && t.Seats.First(x => x.Def.Id == v.Seat).HolderId is int h && Secrets.Held(t, h) != null)
                .Select(v => (v, h: t.Seats.First(x => x.Def.Id == v.Seat).HolderId!.Value))
                .ToList();
            if (levers.Count > 0)
            {
                p.Buttons([.. levers.Select(l => ($"Press {t.Agent(l.h).GivenName} to vote {(l.v.Yes ? "nay" : "aye")}", true, false, (Action)(() => Act(() => _sim.PressCouncillor(l.h, !l.v.Yes)))))]);
                p.Note($"You know what they hide. Pressing them spends it: they vote your way, never forgive it, and {Math.Round(t.Content.Council.Secrets.Scandal * 100)}% of the time word gets out.");
            }
        }
        foreach (var (mole, a, b) in Infiltration.Whispered(t))
        {
            p.Card($"[b]A traitor on the council[/b]\n{Ui.C(Ui.Ink2, $"The taverns say {AgentLink(a)} or {AgentLink(b)} serves {Ui.Esc(mole.By)}. Name the traitor and they flee; name an honest councillor and they resign, and the council resents it. Wait, and the watch finds the proof while the traitor goes on working.")}", Ui.Bad);
            int ia = a.Id, ib = b.Id;
            p.Buttons(($"Name {a.GivenName}", true, false, () => Act(() => _sim.AccuseCouncillor(ia))), ($"Name {b.GivenName}", true, false, () => Act(() => _sim.AccuseCouncillor(ib))));
        }
    }

    /// <summary>What the mayor knows about a councillor, and the levers it gives: dig for a secret, then press them on the vote or expose them.</summary>
    private void RenderSecret(Pane p, Town t, Agent a)
    {
        var r = t.Content.Council.Secrets;
        int id = a.Id;
        var known = Secrets.Of(t, id);
        if (Secrets.Held(t, id) is { } held)
        {
            p.Text($"{Tag("Secret", Ui.Gold)} {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(Secrets.Describe(t, held)))}. Press them on a vote before the council, or expose them and they lose the seat.[/font_size]")}", 12);
            p.Buttons(("Expose them", true, false, () => Act(() => _sim.ExposeCouncillor(id))));
            return;
        }
        if (known is { Known: true })
        {
            p.Text(Ui.C(Ui.Ink2, known.Kind == "" ? "[font_size=12]Your people found nothing to hold over them.[/font_size]" : "[font_size=12]You have spent what you knew about them.[/font_size]"), 12);
            return;
        }
        string? why = Secrets.DigBlocked(t, id);
        int dc = Secrets.Dc(t);
        double odds = Sheet.Chance(t, "investigation", "", dc, false, false);
        p.Buttons(($"Dig into their past, {Js.Str(r.DigCost)}g · {Math.Round(odds * 100)}%", why == null, false, () => Act(() => _sim.DigIntoCouncillor(id))));
        if (why != null && why != "only a councillor") p.Note(Ui.Cap(why) + ".");
    }

    /// <summary>The tax rate the mayor sets, and what the town's services cost against what it takes in.</summary>
    private void RenderTaxes(Pane p, Town t)
    {
        var r = Taxes.Rules(t);
        double rate = t.TaxRate, usual = t.Content.Rules.TaxRate, unrest = t.Taxes.Unrest;
        int pct = (int)Math.Round(rate * 100);
        p.Heading("Taxes");
        double mood = Taxes.Mood(t) + r.Mood * unrest / 100;
        p.Row($"[b]Tax rate[/b]  {Tag($"{pct}%", rate > usual + 1e-9 ? Ui.Bad : rate < usual - 1e-9 ? Ui.Good : Ui.Ink)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Taken from every sale, drink and hearth. Above {Math.Round(usual * 100)}% each point sours the townsfolk and puts settlers off; below it pleases them (mood {(mood >= 0 ? "+" : "")}{mood:0.0} now).[/font_size]")}",
            ("−1%", rate > r.Min + 1e-9, false, () => Act(() => _sim.SetTax(rate - r.Step))),
            ("+1%", rate < r.Max - 1e-9, true, () => Act(() => _sim.SetTax(rate + r.Step))));
        double income = Taxes.Income(t), services = Taxes.Services(t), funding = Taxes.Funding(t);
        bool shortfall = funding < 1;
        p.Text($"Services {Tag($"{services:N0}g a day", shortfall ? Ui.Bad : Ui.Ink)}   Taxes, rents and trade {Tag($"{income:N0}g a day", shortfall ? Ui.Bad : Ui.Good)}", 12);
        p.Meter("Unrest", unrest, $"{Math.Round(unrest)}", unrest >= 25);
        p.Note(unrest >= r.StrikeAt
            ? $"The town's services are falling apart and workers are downing tools to protest. Raise taxes or find more income: trade routes, wonders, institutions that pay."
            : shortfall
                ? "The watch, the roads, the wells and the institutions cost more than the town takes in. Unrest is climbing: it sours every household, costs Mandate and turns settlers away, and at its worst workers walk out."
                : unrest > 0
                    ? "The town's services are paid for again, and the unrest is easing."
                    : "Services grow with the town: every new household, hall and institution costs upkeep. While taxes, rents and trade cover them, the townsfolk are content.");
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

        RenderRoad(p, t);

        RenderExplore(p, t);
        RenderEyes(p, t);

        p.Heading("Bounty board");
        // How the job is to be done: the terms every Post button below uses.
        var chosen = t.Content.Bounties.Term(_terms);
        p.Buttons(t.Content.Bounties.Terms.Select(term => (term.Name, true, term.Id == chosen.Id, (Action)(() => Act(() => _terms = term.Id)))).ToArray());
        var (pleased, angered) = Regard.Reactions(t, chosen.Tags);
        string Who(List<Agent> heroes) => string.Join(", ", heroes.Take(4).Select(h => h.GivenName)) + (heroes.Count > 4 ? $" +{heroes.Count - 4}" : "");
        var feel = new List<string>();
        if (pleased.Count > 0) feel.Add(Ui.C(Ui.Good, $"Pleases {Ui.Esc(Who(pleased))}"));
        if (angered.Count > 0) feel.Add(Ui.C(Ui.Bad, $"Angers {Ui.Esc(Who(angered))}"));
        p.Note($"[b]{Ui.Esc(chosen.Name)}:[/b] {Ui.Esc(chosen.Text)}{(feel.Count > 0 ? "\n" + string.Join("  ·  ", feel) : "")}");
        foreach (var c in Captives.Held(t).OrderBy(c => c.Id))
        {
            if (Quests.Site(t, c.SiteId) is not SiteState held) continue;
            int left = Math.Max(1, (int)Math.Ceiling((c.DueTick - t.Tick) / 24.0));
            string names = string.Join(", ", c.Captives.Select(id => t.Agent(id).Name));
            p.Card($"[b]Held at {SiteLink(held.Def)}[/b]  {Tag($"{left} {(left == 1 ? "day" : "days")} left", Ui.Bad)}\n[font_size=12]{Ui.Esc(names)}. Post a [b]Rescue[/b] on {Ui.Esc(held.Def.Name)} before the days run out, or clear it.[/font_size]", Ui.Bad);
        }
        bool freeScry = Watch.FreeScryingsLeft(t) > 0;
        bool canScout = Watch.Watchers(t).Count > 0;
        // Threats first: a site an alert names (a war camp, a crisis's seat) or the warband's camp, then the rest of the
        // open country, with cleared places last.
        var named = Alerts.Standing(t).Where(a => a.SiteId != null).Select(a => a.SiteId!).ToHashSet();
        int Rank(SiteState s) => s.ThoughtCleared ? 2 : named.Contains(s.Def.Id) || s.Def.Id == t.Content.Enemy.Outpost.Id ? 0 : 1;
        foreach (var s in t.Sites.OrderBy(Rank))
        {
            // The board only lists what the town has heard of.
            if (s.Known == SiteKnowledge.Unheard) continue;
            var live = t.Bounties.FirstOrDefault(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
            var party = live?.PartyId is int pid ? t.Parties.FirstOrDefault(x => x.Id == pid) : null;
            int reward = (int)Terms.Suggested(t, s, chosen);
            string status;
            var buttons = new List<(string, bool, bool, Action)>();
            if (s.ThoughtCleared) status = Tag("Cleared", Ui.Good);
            else if (party != null && !party.Returned) status = Tag($"{Ui.Esc(party.Name)} on the road", Ui.Gold);
            else if (live != null && live.Errand != "" && t.Content.Bounties.Errands.FirstOrDefault(x => x.Id == live.Errand) is { } boon)
                status = Tag($"Mages' errand: {Math.Round(live.Reward)}g and {boon.Name}", Ui.Gold);
            else if (live != null) status = Tag($"{(live.Terms == "slay" ? "Bounty" : Terms.Of(t, live).Name)} posted: {Math.Round(live.Reward)}g{(live.LootToTown ? ", loot to the town" : "")}", Ui.Gold);
            else
            {
                status = s.Attempts > 0 ? Tag($"{s.Attempts} failed {(s.Attempts == 1 ? "attempt" : "attempts")}", Ui.Bad) : Tag("Unexplored");
                string sid = s.Def.Id;
                string terms = chosen.Id;
                bool fits = Terms.Blocked(t, s, chosen) == null;
                if (fits) buttons.Add(($"{chosen.Name} · {reward}g", t.Treasury >= reward, true, () => Act(() => _sim.PostBounty(sid, reward, terms))));
                double withLoot = Sim.Systems.Armory.TermsReward(t, reward);
                if (fits && !chosen.Item) buttons.Add(($"{Js.Str(withLoot)}g, loot to us", t.Treasury >= withLoot, false, () => Act(() => _sim.PostBounty(sid, withLoot, terms, lootToTown: true))));
            }
            if (!s.ThoughtCleared && Watch.LiveOf(t, s) == null)
            {
                string sid = s.Def.Id;
                var offer = Knowledge.Offers(t, s.Def).FirstOrDefault();
                string label = freeScry || offer?.Price == ScryPrice.Free ? "Scry free" : offer?.Price == ScryPrice.Favour ? "Scry (a favour)" : $"Scry {Math.Round(offer?.Gold ?? Knowledge.ScryCost(t, s.Def))}g";
                buttons.Add((label, freeScry || offer != null && t.Treasury >= offer.Gold, false, () => Act(() => _sim.CommissionScrying(sid))));
                if (Watch.ScoutAt(t, sid) == null) buttons.Add(("Scout", canScout, false, () => Act(() => _sim.PostScout(sid))));
            }
            int bt = Math.Clamp(s.BelievedTier, 1, 3);
            string tier = new string('◆', bt) + new string('◇', 3 - bt);
            // A seat the Campaign window names reads as that, not as the rumour the town first heard of the place.
            var seated = s.ThoughtCleared ? null : t.Saga.Menaces.FirstOrDefault(m => m.Alive && m.SeatId == s.Def.Id);
            string word = seated != null
                ? Ui.C(Ui.Bad, $" [font_size=11]seat of {Ui.Esc(seated.Name)}{(seated.BigBad ? ", the big bad" : "")}{(s.Def.Tier > 0 ? $", tier {s.Def.Tier}" : "")}[/font_size]")
                : s.Known == SiteKnowledge.Rumoured && !s.ThoughtCleared
                ? Ui.C(Ui.Ink2, $" [font_size=11]rumour: {Ui.Esc(s.Believed)} ({Rumors.Trust(s.Trust)})[/font_size]")
                : "";
            // Its face and how hard it looks for the town's heroes, both from what the town believes is there.
            var danger = Assess.Danger(t, s);
            var site = s;
            string look = $"{Foes.CreatureFace(s.Believed, s.Def.Kind)}{s.Known}{s.ThoughtCleared}{danger.Id}";
            p.Row($"{SiteLink(s.Def)} {Ui.C(Ui.Gold, $"[font_size=11]{tier}[/font_size]")}{word}\n{status}",
                (look, () => Foes.Of(t, site, 44), s.ThoughtCleared ? null : () => Foes.Bar("Danger", danger, Foes.Worse(danger), "How hard it looks for the heroes on the town's books, from what the town believes is there. A rumour can be wrong.")),
                buttons.ToArray());
        }
        p.Note(freeScry
            ? $"Scrying: the Scrying Pool will show one site live for {t.Content.Knowledge.ScryHours} hours today, free."
            : Knowledge.Scryers(t).Count is var casters and > 0
                ? $"{Ui.Img("face-all-seeing-eye")} Scrying: {Knowledge.Rested(t).Count}/{casters} ready · {t.Content.Knowledge.ScryHours}h"
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

    /// <summary>Parties out on the road, with their sendings: the bounty board's and the tray's.</summary>
    private void RenderRoad(Pane p, Town t)
    {
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
                    ? Ui.C(Ui.Ink2, Knowledge.Slots(t, party) == 0 ? "No caster and no sending stone with them: no way to send word." : "No spell slots or stone sendings left.")
                    : Ui.C(Ui.Ink2, Sim.Systems.Armory.Carries(t, party)
                        ? $"They carry one of the town's sending stones: {slots} {(slots == 1 ? "sending" : "sendings")} left."
                        : $"{slots} spell {(slots == 1 ? "slot" : "slots")} for sendings. Each one costs a slot{(party.Members.Any(id => t.Agent(id).Hero!.Cls.Healer) ? ", and a healer's slot is a heal they won't have in the dungeon" : "")}.");
                int id = party.Id;
                p.Card($"[b]The {Ui.Esc(party.Name)}[/b]  {Tag($"due back day {due}", Ui.Gold)}\n{where} · {members}\n[font_size=12]{slotNote}[/font_size]");
                if (Knowledge.Slots(t, party) > 0)
                    p.Buttons(
                        ("Ask for word", slots > 0, false, () => Act(() => _sim.SendWord(id))),
                        ("Call them home", slots > 0 && Knowledge.CanRecall(t, party), false, () => Act(() => _sim.SendWord(id, recall: true))));
            }
        }
    }

    /// <summary>Companies out exploring, the council's open offers, and the country a company could be sent to.</summary>
    private void RenderExplore(Pane p, Town t)
    {
        p.Heading("Exploring");
        p.Note("Nothing goes on the map by itself. Travellers and allies tell of places; to find them for certain, pay a company to explore a region, or press [b]Scout[/b] over the corner map and mark a spot for them to look at. They send word of what they find and wait for yours.");
        foreach (var s in t.Surveys.Where(Explore.Out).OrderBy(x => x.Id))
        {
            var region = t.Regions[s.RegionId];
            var at = s.AtFind is int fid ? Explore.FindOf(t, fid) : null;
            string state = s.Phase switch
            {
                SurveyPhase.Outbound when s.SpotX is double sx && s.SpotY is double sy => $"On the road to the spot you marked {Explore.Bearing(sx, sy)}",
                SurveyPhase.Outbound => $"On the road to {Ui.Esc(region.Name)}",
                SurveyPhase.Exploring when s.SpotX != null && !s.Escorting => $"Looking round the spot you marked in {Ui.Esc(region.Name)}",
                SurveyPhase.Exploring => s.Escorting ? "Walking a caravan home" : $"Exploring {Ui.Esc(region.Name)}, {Ui.Days(Math.Max(1, (int)Math.Ceiling((s.ExploreUntil - t.Tick) / 24.0)))} left",
                SurveyPhase.Waiting when at != null => t.Tick >= at.ReportTick ? $"Waiting for word at {Ui.Esc(Explore.Title(t, at))}" : $"Stopped in {Ui.Esc(region.Name)}; a bird is on its way",
                SurveyPhase.Investigating when at != null && Explore.NextChapter(t, at) is { } ch => $"Investigating {Ui.Esc(Explore.Title(t, at))}: {Ui.Esc(ch.Title.ToLowerInvariant())}",
                _ => "Coming home",
            };
            string members = string.Join(", ", s.Members.Select(t.Agent).Where(a => !a.Dead).Select(a => AgentLink(a, a.GivenName)));
            int finds = s.Finds.Count(id => Explore.FindOf(t, id)!.Known);
            var buttons = new List<(string, bool, bool, Action)>();
            if (at is { State: FindState.Open } && t.Tick >= at.ReportTick)
            {
                int id = at.Id;
                buttons.Add(("Read their report", true, true, () => OpenFind(id)));
            }
            p.Row($"[b]The {Ui.Esc(s.Name)}[/b]  {Tag(finds == 1 ? "1 find" : $"{finds} finds", new Color("8cc084"))}\n{state} · {members}", buttons.ToArray());
        }
        foreach (var c in t.Commissions.Where(c => c.Status == BountyStatus.Open))
        {
            string what = c.SpotX is double sx && c.SpotY is double sy
                ? $"Spot marked {Explore.Bearing(sx, sy)}, {Js.Hypot(sx, sy):0.0} days out"
                : Ui.Esc(t.Regions[c.RegionId].Name);
            p.Row($"{what}  {Tag($"Offer posted: {Math.Round(c.Reward)}g", Ui.Gold)}\n{Ui.C(Ui.Ink2, "[font_size=12]Waiting for a company to take it.[/font_size]")}");
        }
        int strongest = t.Heroes.Select(h => h.Hero!.Level).DefaultIfEmpty(1).Max();
        foreach (var r in t.Regions.Where(r => r.Known && Explore.CanCommission(t, r.Id)).OrderBy(r => r.Ring).ThenBy(r => r.Id))
        {
            int heard = r.SiteIds.Count(id => Quests.Site(t, id)!.Known != SiteKnowledge.Unheard);
            int found = r.SiteIds.Count(id => Quests.Site(t, id)!.Found);
            int reward = (int)Math.Round(Explore.SuggestedReward(t, r));
            string what = !r.Visited ? "Never explored" : found == 0 ? "Explored, nothing found" : $"{found} {(found == 1 ? "place" : "places")} found";
            string danger = r.Levels[0] > strongest + 1 ? Ui.C(Ui.Bad, " · too dangerous for the town's heroes yet") : "";
            int rid = r.Id;
            p.Row($"{Ui.Esc(r.Name)} {Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(r.Theme.Name)}, {Ui.Days(r.Days)} out[/font_size]")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{what}{(heard > found ? $", {heard - found} more heard of" : "")}[/font_size]")}{danger}",
                ($"Explore {reward}g", t.Treasury >= reward, false, () => Act(() => _sim.PostSurvey(rid, reward))));
        }
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
        string thead = $"[b]Temple of {Ui.Esc(Faith.TempleGod(t) is { } tg ? Faith.Prose(tg) : t.Content.Shrines.Deity)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=12]Heals heroes, raises the fallen, keeps holy days and pays its own bounties. {Shrines.Followers(t).Count} {Ui.Esc(t.Content.Shrines.Devout)} in town.[/font_size]")}";
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

    // ---------- Faith and factions ----------

    private static string BuildingLink(Building b) => $"[url=building:{b.Id}]{Ui.C(Ui.Accent, Ui.Esc(b.Name))}[/url]";

    private void RenderFaith()
    {
        var p = _panes[Tab.Faith];
        var t = _sim.Town;
        var fs = t.Faiths;
        p.Begin();
        var boons = new List<string>();
        if (t.Day < fs.ValorUntil) boons.Add($"Valour on the walls for {Ui.Days(fs.ValorUntil - t.Day)}");
        if (t.Day < fs.JusticeUntil) boons.Add($"Justice with the watch for {Ui.Days(fs.JusticeUntil - t.Day)}");
        if (boons.Count > 0) p.Card(string.Join("\n", boons.Select(b => $"[b]{b}.[/b]")), Ui.Gold);

        p.Heading("The gods in town");
        var gods = Faith.Gods(t)
            .Select(g => (God: g, Followers: Faith.Followers(t, g.Id).Count, Places: Faith.Places(t, g.Id)))
            .Where(x => x.Followers > 0 || x.Places.Count > 0)
            .OrderByDescending(x => x.Followers)
            .ThenBy(x => x.God.Id, StringComparer.Ordinal)
            .ToList();
        if (gods.Count == 0) p.Note("Nobody in town keeps a god yet.");
        foreach (var (god, followers, places) in gods)
        {
            double favor = fs.Favor.Get(god.Id, 0), need = Faith.Threshold(t, god);
            string holy = Faith.IsHolyDay(t, god) ? "  " + Tag("Holy day", Ui.Gold) : "";
            string where = places.Count > 0 ? $"Kept at {string.Join(", ", places.Select(BuildingLink))}." : god.Evil ? "No house: only a cult would feed it." : "No house of its own yet.";
            double funds = fs.Funds.Get(god.Id, 0);
            string saving = places.Count == 0 && funds >= 1 ? $" The faithful have saved {Math.Round(funds)}g towards a shrine." : "";
            p.Row($"[b]{Ui.Esc(Faith.Short(god))}[/b]  {Tag($"{followers} {(followers == 1 ? "follower" : "followers")}", god.Evil ? Ui.Bad : Ui.Ink)}{holy}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(god.Portfolio))}{(god.Portfolio != "" ? ". " : "")}{where}{saving}[/font_size]")}");
            if (!god.Evil) p.Meter("Favour", 100 * Math.Min(1, favor / Math.Max(1, need)), favor >= need ? "ready" : $"{Math.Round(100 * favor / Math.Max(1, need))}%");
        }
        p.Note("Devotion builds a god's favour; when it brims over, the god acts in town.");
        var miracles = fs.Miracles.TakeLast(5).Reverse().ToList();
        if (miracles.Count > 0)
        {
            p.Heading("Miracles");
            foreach (var m in miracles)
                p.Text($"{Ui.C(Ui.Ink2, $"[font_size=11]Day {m.Tick / SimTime.HoursPerDay + 1}[/font_size]")}  {Ui.Esc(Faith.God(t, m.Deity) is { } g ? Faith.Short(g) : m.Deity)}: {Ui.Esc(m.Boon)}", 13);
        }

        p.Heading("Factions");
        var rules = t.Content.Factions;
        var known = t.Factions.Where(f => f.Active && f.Known).ToList();
        if (known.Count == 0) p.Note("No faction has a chapter in town that the town knows of. They take root as the town grows.");
        foreach (var f in known)
        {
            string leader = f.LeaderId is int lid ? $"Led by {AgentLink(t.Agent(lid))}. " : "";
            string stand = f.Standing >= 30 ? "Friendly" : f.Standing <= -30 ? "Hostile" : "Neutral";
            var standColor = f.Standing >= 30 ? Ui.Good : f.Standing <= -30 ? Ui.Bad : Ui.Ink2;
            string kind = f.Secret ? Tag("Exposed", Ui.Bad) : Tag(Ui.Cap(f.Lore.Kind == "" ? "open" : f.Lore.Kind));
            string motto = f.Lore.Motto != "" ? $"\n[i]{Ui.Esc(f.Lore.Motto)}[/i]" : "";
            if (Annals.LineageOf(t.Content.History, "faction:" + f.Id) is HistoryLineage heirs) motto += $"\nOf {Ui.Esc(heirs.Title)}. {Ui.Esc(heirs.Motive)}";
            string id = f.Id;
            double grant = rules.SupportPerStanding * 10;
            var actions = new List<(string, bool, bool, Action)>();
            // Crack down when it is turning on the town (a cult, a gang); support it when it serves.
            actions.Add(($"Crack down {Math.Round(rules.CrackdownCost)}g", t.Treasury >= rules.CrackdownCost, true, () => Act(() => _sim.CrackDown(id))));
            if (f.Lore.Kind != "cult")
                actions.Add(($"Support {Math.Round(grant)}g", t.Treasury >= grant, false, () => Act(() => _sim.SupportFaction(id, grant))));
            p.Row($"[b]{Ui.Esc(Ui.Cap(f.Name))}[/b]  {kind}  {Tag(stand, standColor)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{leader}{f.Members.Count} {(f.Members.Count == 1 ? "member" : "members")} · coffers {Math.Round(Factions.Coffers(t, f))}g · standing {Math.Round(f.Standing)}{motto}[/font_size]")}", actions.ToArray());
            p.Meter("Suspicion", f.Heat, $"{Math.Round(f.Heat)}", f.Heat >= rules.ExposeAt * 0.75);
        }
        if (t.Factions.Any(f => f.Active && !f.Known))
            p.Note("Not every faction shows its face. Secret ones are exposed as the watch's suspicion of them grows.");
        p.Commit();
    }

    // ---------- Chronicles ----------

    private void RenderChronicles()
    {
        var p = _panes[Tab.Chronicles];
        var t = _sim.Town;
        p.Begin();
        RenderHall(p, t);
        if (t.Legacies.Count > 0) p.Heading("Expeditions");
        if (t.Chronicles.Count == 0) p.Note("No expeditions yet. Post a bounty and wait for a party to form at the tavern. Their story comes back with them.");
        foreach (var c in Enumerable.Reverse(t.Chronicles))
        {
            var color = c.Outcome == ExpeditionOutcome.Cleared || c.GoalMet ? Ui.Good : c.Outcome == ExpeditionOutcome.Wiped ? Ui.Bad : Ui.Ink2;
            string tale = c.Beats.Count > 0 && c.MemberIds.Count > 0 ? $"  [url=tale:{c.Id}]{Ui.C(Ui.Gold, "▶ Hear the tale")}[/url]" : "";
            string head = $"[url=chron:{c.Id}][b]{Ui.Esc(c.PartyName)}[/b][/url] at {Ui.Esc(c.SiteName)}  {Tag(c.Verdict, color)}{tale}\n{Ui.C(Ui.Ink2, $"[font_size=12]Day {c.DepartDay}–{c.ReturnDay} · {Ui.Esc(string.Join(", ", c.Members))}[/font_size]")}";
            if (_openChronicles.Contains(c.Id))
                head += "\n" + string.Join("\n", c.Lines.Select(l => $"{Ui.C(color, "│")} [font_size=13]{Ui.Esc(l)}[/font_size]"));
            p.Row(head);
        }
        p.Commit();
    }

    /// <summary>The hall of heroes: the fallen and the retired, newest first, with memorials to raise.</summary>
    private void RenderHall(Pane p, Town t)
    {
        if (t.Legacies.Count == 0) return;
        p.Heading("Hall of heroes");
        int mentors = Legacies.Mentors(t).Count();
        double loyalty = Legacies.Loyalty(t);
        if (mentors > 0 || loyalty > 0)
            p.Note(string.Join(" ", new[]
            {
                mentors > 0 ? $"{mentors} {(mentors == 1 ? "master" : "masters")} of arms train new heroes, who arrive with a head start." : "",
                loyalty > 0 ? $"The town's memorials add {Js.Str(Math.Round(loyalty, 1))} to every hero's loyalty." : "",
            }.Where(x => x != "")));
        foreach (var l in Enumerable.Reverse(t.Legacies))
        {
            int id = l.HeroId;
            string fate = l.Retired ? $"retired on day {l.Day} to teach" : $"fell on day {l.Day}";
            string heir = l.HeirId is int h ? $"\n{Ui.C(Ui.Good, $"[font_size=12]Their heir {Ui.Esc(t.Agent(h).Name)} took up their arms{(l.Relic != "" ? $" and the {Ui.Esc(l.Relic)}" : "")}.[/font_size]")}"
                : l.HeirDay != null ? $"\n{Ui.C(Ui.Ink2, "[font_size=12]Word has gone to their kin.[/font_size]")}" : "";
            string head = $"[b]{Ui.Esc(l.Name)}[/b]  {(l.Memorial ? Tag("memorial", Ui.Gold) : "")}\n" +
                          Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(l.Class)}, level {l.Level} · {fate} · {l.Expeditions} {(l.Expeditions == 1 ? "expedition" : "expeditions")}, {l.Kills} foes slain, renown {l.Renown}[/font_size]") + heir;
            if (l.Memorial) p.Row(head);
            else
            {
                double cost = Legacies.MemorialCost(t, l);
                p.Row(head, ($"Raise a memorial · {Js.Str(Js.Round(cost))}g", t.Treasury >= cost, false, () => Act(() => _sim.RaiseMemorial(id))));
            }
        }
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
        else if (sel.Kind == "wall") InspectWall(p, t, sel.Id);
        else if (sel.Kind == "tower") InspectTower(p, t, sel.Id);
        else if (sel.Kind == "flag") InspectFlag(p, t, sel.Id);
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
        p.Text($"[font_size=19][b]{Ui.Esc(Loyalty.Styled(t, a))}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(describe))} · {Ui.Esc(a.TraitList())}")}");
        if (seat != null) p.Text($"{Tag(seat.Def.Title, Ui.Gold)} loyalty {Math.Round(seat.Loyalty)}");
        if (a.Hero is HeroSheet hs)
        {
            p.Text($"HP {hs.Hp}/{hs.MaxHp} · {hs.Xp} XP · {hs.Kills} kills · {hs.Expeditions} expeditions");
            if (Annals.Past(t, a) is string past && past != "") p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(past)}[/font_size]"));
            var cls = hs.Cls;
            var spells = new List<string>();
            if (cls.HealSpell != null) spells.Add($"{cls.HealSpell.Name} ({cls.HealsAt(hs.Level)} a delve)");
            if (cls.AreaSpell != null) spells.Add(cls.AreaSpell.Name);
            string role = cls.Role == "" ? "" : $"{Ui.Cap(cls.Role)}{(cls.Magic != "" ? $", {cls.Magic} caster" : "")}";
            p.Text(Ui.C(Ui.Ink2, $"{Ui.Esc(role)}{(spells.Count > 0 ? $" · casts {Ui.Esc(string.Join(", ", spells))}" : "")}"), 13);
            p.Text($"Fights with {Ui.Esc(Gear.Describe(hs))} · AC {Gear.Ac(t.Content, a)} · attack +{Gear.Attack(t.Content, a)}{(hs.Stone ? " · carries a town sending stone" : "")}", 13);
            var (alone, carries) = Passage.Of(t, a);
            string mount = hs.Mount != "" && t.Content.Passage.Mount(hs.Mount) is { } md ? $"Rides a {md.Name}" : "";
            string crosses = Passage.Describe(alone), party = Passage.Describe(carries & ~alone) is var more && more != "" ? $"; carries the whole party over {more}" : "";
            if (mount != "" || crosses != "" || party != "")
                p.Text(Ui.C(Ui.Ink2, Ui.Esc($"{mount}{(mount != "" && crosses != "" ? " · " : "")}{(crosses != "" ? $"can cross {crosses}" : "")}{party}")), 13);
            InspectNotoriety(p, t, a, hs);
            InspectLoyalty(p, t, a, hs);
        }
        p.Text($"{Ui.Esc(status)} · {Math.Floor(a.Gold)} gold · mood {Math.Round(a.LastDayMood)}");
        InspectMoods(p, t, a);
        string lives = a.HomeId == null && a.Hero?.Lodging != true ? "Sleeps rough" : $"Lives at {Ui.Esc(home)}";
        string works = a.Hero != null ? "lives by bounties" : a.JobId == null ? "out of work" : $"works at {Ui.Esc(job)}";
        p.Text($"{lives}; {works}.");
        if (a.SpouseId is int sp) p.Text($"Married to {AgentLink(t.Agent(sp))}");
        InspectSheet(p, t, a);
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

    /// <summary>The inspiration or break gripping them now, and what the last few came to.</summary>
    private static void InspectMoods(Pane p, Town t, Agent a)
    {
        if (Moods.Episode(t, a) is { } d)
        {
            bool inspired = Moods.IsInspiration(t, d);
            int hours = Math.Max(1, a.EpisodeUntil - t.Tick);
            string left = hours >= 24 ? $"{(hours + 12) / 24} more day{((hours + 12) / 24 == 1 ? "" : "s")}" : $"{hours} more hour{(hours == 1 ? "" : "s")}";
            p.Text($"{Tag(inspired ? "Inspired" : "Breaking", inspired ? Ui.Gold : Ui.Bad)} {Ui.C(inspired ? Ui.Gold : Ui.Bad, Ui.Esc(d.Name))} {Ui.C(Ui.Ink2, $"[font_size=12]· {left}[/font_size]")}", 13);
        }
        else if (!a.IsHero && !a.Departed)
        {
            var r = t.Content.Moods;
            string streak = a.HighDays > 0 ? $"in high spirits {a.HighDays} day{(a.HighDays == 1 ? "" : "s")} running: an inspiration may strike from day {r.HighDays}"
                : a.LowDays > 0 ? $"low {a.LowDays} day{(a.LowDays == 1 ? "" : "s")} running: past day {r.LowDays} they may break" : "";
            if (streak != "") p.Text(Ui.C(a.LowDays > 0 ? Ui.Bad : Ui.Ink2, $"[font_size=12]{Ui.Cap(streak)}[/font_size]"), 13);
        }
        if (a.Moments.Count > 0)
            p.Text(Ui.C(Ui.Ink2, $"[font_size=12]{string.Join("\n", a.Moments.AsEnumerable().Reverse().Select(m => Ui.Esc(m)))}[/font_size]"), 13);
    }

    /// <summary>Fame and infamy: what the world calls them, and what their name does for them.</summary>
    private void InspectNotoriety(Pane p, Town t, Agent a, HeroSheet hs)
    {
        if (hs.Renown <= 0 && hs.Infamy <= 0) return;
        var tags = new List<string>();
        if (hs.Renown > 0) tags.Add($"{Tag($"Renown {hs.Renown}", Notoriety.FameTier(t, hs) >= 2 ? Ui.Gold : Ui.Ink2)} {Ui.C(Ui.Ink2, Ui.Esc(Notoriety.FameName(t, hs.Renown)))}");
        if (hs.Infamy > 0) tags.Add($"{Tag($"Infamy {hs.Infamy}", Notoriety.InfamyTier(t, hs) >= 2 ? Ui.Bad : Ui.Ink2)} {Ui.C(Ui.Ink2, Ui.Esc(Notoriety.InfamyName(t, hs.Infamy)))}");
        var effects = Notoriety.Effects(t, a);
        p.Text(string.Join("  ", tags) + (effects.Count > 0 ? $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(string.Join(" · ", effects)))}[/font_size]")}" : ""), 13);
    }

    private void InspectLoyalty(Pane p, Town t, Agent a, HeroSheet hs)
    {
        if (a.Departed)
        {
            if (hs.TurnedTo is int m) p.Text(Ui.C(Ui.Bad, $"Went over to {Ui.Esc(t.Saga.Menaces[m - 1].Name)}."));
            else if (hs.DefectedTo != null && Factions.Find(t, hs.DefectedTo) is FactionState f) p.Text(Ui.C(Ui.Bad, $"Ran to {Ui.Esc(f.Known ? f.Name : "someone in the shadows")}."));
            return;
        }
        var honours = new List<string>();
        if (hs.Knight) honours.Add(Tag("Knight", Ui.Gold));
        if (hs.FiefId != null && Quests.Site(t, hs.FiefId) is SiteState land) honours.Add(Tag($"Holds {Ui.Esc(land.Def.Name)}", Ui.Gold));
        if (hs.Champion) honours.Add(Tag($"Champion of {t.Name}", Ui.Gold));
        if (Callings.Of(t, a) is CallingDef calling) honours.Add(Tag(calling.Name, Ui.Good));
        if (honours.Count > 0) p.Text(string.Join("  ", honours));
        var lr = t.Content.Loyalty;
        double loyalty = Math.Round(hs.Loyalty);
        var color = hs.Loyalty < lr.Desert ? Ui.Bad : hs.Loyalty < lr.Restless ? Ui.Gold : hs.Loyalty >= 60 ? Ui.Good : Ui.Ink2;
        string mood = hs.Loyalty < lr.Desert ? "ready to leave" : hs.Loyalty < lr.Restless ? "restless" : hs.Loyalty >= 75 ? "devoted" : hs.Loyalty >= 60 ? "loyal" : "content";
        var why = Loyalty.Factors(t, a).OrderByDescending(f => Math.Abs(f.Points)).Take(4).Select(f => $"{f.Why} {(f.Points >= 0 ? "+" : "−")}{Math.Round(Math.Abs(f.Points))}");
        p.Text($"{Tag($"Loyalty {loyalty}", color)} {Ui.C(color, mood)}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(string.Join(" · ", why))}[/font_size]")}", 13);
        if (hs.JailedUntil is int jail)
            p.Text(Ui.C(Ui.Bad, $"In the town cells until day {jail / SimTime.HoursPerDay + 1}. They take no bounties until then."), 13);
        if (hs.Potions + hs.Scrolls + hs.Copied > 0)
        {
            var kit = new List<string>();
            if (hs.Potions > 0) kit.Add(hs.Potions == 1 ? "1 healing potion" : $"{hs.Potions} healing potions");
            if (hs.Scrolls > 0) kit.Add(hs.Scrolls == 1 ? "1 spell scroll" : $"{hs.Scrolls} spell scrolls");
            if (hs.Copied > 0) kit.Add($"{hs.Copied} copied into the spellbook");
            p.Text(Ui.C(Ui.Ink2, "Carries " + string.Join(", ", kit) + "."), 13);
        }
        if (hs.Taint > 0)
            p.Text(Ui.C(Ui.Bad, hs.Taint == 1 ? "Took a gift from the dark. Their shadow is slow to follow them." : $"Took {hs.Taint} gifts from the dark. They are not sleeping well."), 13);
        if (Regard.TemperOf(t, a) is TemperDef temper)
        {
            // What they make of the mayor, and the choice that moved them last.
            double r = Math.Round(hs.Regard);
            var rc = r >= 15 ? Ui.Good : r <= -15 ? Ui.Bad : Ui.Ink2;
            string thinks = r >= t.Content.Regard.Admire ? "trusts you" : r >= 15 ? "approves of you" : r <= t.Content.Regard.Aggrieved ? "has a grievance" : r <= -15 ? "disapproves of you" : "undecided about you";
            string last = hs.RegardWhy != "" ? $" Last {(hs.RegardPleased ? "pleased" : "angered")} by: you {hs.RegardWhy}." : "";
            string quest = hs.Personal == "done" ? " You saw their personal quest through." : hs.Personal == "failed" ? " You let them down over their personal quest." : "";
            p.Text($"{Tag(temper.Name, Ui.Ink2)} {Ui.C(rc, $"Regard {(r >= 0 ? "+" : "−")}{Math.Abs(r)}, {thinks}")}\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(Ui.Cap(temper.Text))}: {Ui.Esc(Regard.Describe(t, temper))}.{Ui.Esc(last + quest)}[/font_size]")}", 13);
        }
        string? knight = Loyalty.KnightBlocked(t, a);
        string? champion = Loyalty.ChampionBlocked(t, a);
        double purse = Js.Round(lr.Reward.PerLevel * hs.Level);
        int id = a.Id;
        p.Buttons(
            (hs.Knight ? "Knighted" : $"Knight them, {Js.Str(lr.Knight.Cost)}g", knight == null, true, () => Act(() => _sim.Knight(id))),
            (hs.Champion ? "Champion" : "Name champion", champion == null, false, () => Act(() => _sim.NameChampion(id))),
            ($"Send a purse, {Js.Str(purse)}g", t.Treasury >= purse, false, () => Act(() => _sim.RewardHero(id, purse))));
        if (knight != null && !hs.Knight) p.Note($"Knighthood: {knight}.");
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
        if (b.Def.Capacity > 0 && b.Complete)
        {
            int inside = Crowds.Inside(t, b);
            string now = $"Visitors {inside}/{b.Def.Capacity}" + (inside >= b.Def.Capacity ? Ui.C(Ui.Bad, " · full") : "");
            string last = b.TurnedAwayLast > 0
                ? Ui.C(Ui.Bad, $"Yesterday it filled to {b.PeakLast} and turned townsfolk away {b.TurnedAwayLast} times; they went elsewhere or home. Another of its kind nearby would take the crowd.")
                : $"Busiest yesterday: {b.PeakLast}/{b.Def.Capacity}.";
            p.Text($"{now}\n{last}");
        }
        if (b.Def.Housing > 0 && Decor.Comfort(t, b) is double comfort && Math.Abs(comfort) >= 0.005)
            p.Text(comfort > 0
                ? Ui.C(Ui.Good, $"A pleasant street: the household is {Math.Round(comfort * 100)}% more content, so the house grows sooner.")
                : Ui.C(Ui.Bad, $"A grim street: the household is {Math.Round(-comfort * 100)}% less content."));
        if (b.Def.Housing > 0 && b.Complete && City.UpgradeBlocked(t, b) is { } stuck && !stuck.StartsWith("it is as grand"))
            p.Note($"To grow a storey: {stuck}.");
        if (t.Content.Merchants.Shops.Contains(b.Def.Id) && !b.IsSite)
        {
            var shelf = Merchants.Wares(t, b).Select(w => t.Content.Library.Item(w.ItemId)).OfType<LibItem>().ToList();
            p.Text($"Buys heroes' loot with up to {Math.Floor(Merchants.Purse(t, b))}g. " +
                (shelf.Count == 0 ? "Nothing second-hand on the shelf." : $"On the shelf: {string.Join(", ", shelf.Select(i => $"{Ui.Esc(i.Name)} ({Js.Str(Math.Ceiling(i.ValueGp * t.Content.Merchants.Resale))}g)"))}."));
        }
        if (here.Count > 0) p.Text($"Here now: {string.Join(", ", here.Select(a => AgentLink(a)))}");
        if (b.IsSite) SiteCard(p, t, b);
        if (b.ParentId is int core) p.Text(Ui.C(Ui.Ink2, $"Part of {Ui.Esc(t.Building(core).Name)}."));
        if (b.Def.Crews > 0 && b.Complete)
            p.Text(Ui.C(Ui.Ink2, $"Puts {b.Def.Crews} more crews to work. The town has {Construction.Crews(t)} crews; a town its size keeps {Construction.Wanted(t)} busy."));
        if (Monuments.IsMonument(t, b.Def)) MonumentCard(p, t, b);
    }

    /// <summary>A building site: what it waits on, and the player's say over it.</summary>
    private void SiteCard(Pane p, Town t, Building b)
    {
        int id = b.Id;
        if (Construction.Stalled(t, b))
        {
            var (timber, stone) = Construction.Wanting(b);
            string what = timber > 0 && stone > 0 ? "timber and stone" : stone > 0 ? "stone" : "timber";
            p.Text(Ui.C(Ui.Bad, $"Stalled: the builders wait for {what}, and the market has none."));
            double cost = Construction.CaravanCost(t, b);
            p.Buttons(($"Send for it by caravan, {Js.Str(cost)}g", Construction.CaravanBlocked(t, b) == null, true, () => Act(() => _sim.SendCaravan(id))));
        }
        else if (b.Hauled < 1 && b.BuildProgress >= Construction.Ready(t, b) - 1e-9)
            p.Text("The footings are in; builders are carrying timber and stone onto the site.");
        p.Buttons((b.Priority ? "Back in line" : "Put this site first", true, !b.Priority, () => Act(() => _sim.PrioritiseSite(id, !b.Priority))));
        p.Note(b.Priority ? "Builders and the market's timber and stone go here before any other site." : "Sends builders and materials here ahead of every other site.");
    }

    /// <summary>A monument's parts: what stands, what is rising, and what the council could add next.</summary>
    private void MonumentCard(Pane p, Town t, Building b)
    {
        int id = b.Id;
        p.Heading("Its parts");
        foreach (var part in Monuments.PartsOf(t, b.Def))
        {
            var built = Monuments.Part(t, b, part.Id);
            string gives = Monuments.Gives(t, part);
            if (built != null)
            {
                string state = built.Complete ? Ui.C(Ui.Good, "standing") : $"rising, {Math.Floor(100 * built.BuildProgress / part.BuildHours)}%";
                p.Text($"[b]{Ui.Esc(part.Name)}[/b] · {state}\n{Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(gives)))}");
                continue;
            }
            string? no = Monuments.Blocked(t, b, part);
            string pid = part.Id;
            p.Text($"[b]{Ui.Esc(part.Name)}[/b] · {Js.Str(Monuments.Cost(t, part))}g\n{Ui.C(Ui.Ink2, Ui.Esc(Ui.Cap(gives)))}");
            if (no == null) p.Buttons(($"Add the {part.Name.ToLowerInvariant()}", true, false, () => Act(() => _sim.AddPart(id, pid))));
            else p.Note(Ui.Cap(no) + ".");
        }
        p.Buttons(
            ("Build it all now", Monuments.PartsOf(t, b.Def).Any(d => Monuments.Blocked(t, b, d) == null), false, () => Act(() => _sim.AddAllParts(id))),
            (b.Grow ? "Stop growing it" : "Grow on its own", true, !b.Grow, () => Act(() => _sim.GrowMonument(id, !b.Grow))));
        p.Note(b.Grow
            ? "The council adds the next part on its own whenever it can spare the gold."
            : "Every part at once can empty the treasury, and the market's timber and stone with it.");
    }

    private void InspectSite(Pane p, Town t, SiteState s)
    {
        var pin = _map.Geo.Pins.GetValueOrDefault(s.Def.Id);
        var sight = _map.Fog.SightOf(s.Def.Id, pin);
        var report = _map.Fog.LastReport(s.Def.Id);
        var scry = Knowledge.ActiveScrying(t, s.Def.Id);
        var live = Watch.LiveOf(t, s);
        var scout = Watch.ScoutAt(t, s.Def.Id);
        p.Text($"[font_size=19][b]{Ui.Esc(Ui.Cap(s.Def.Name))}[/b][/font_size]\n{Ui.C(Ui.Ink2, $"Tier {s.Def.Tier} · {(s.Def.Distance == 0 ? "under the town" : $"{Ui.Days(s.Def.Distance)} away")}")}");
        if (s.Def.Aloft) p.Text(Ui.C(new Color("cfe3f0"), "[b]A floating isle[/b], drifting above the clouds. Only a company that can fly, or rides the sky dock's airship, can reach it."));
        if (Ruins.Of(t, s) is { } ruin && !s.Restored) p.Text(Ui.C(Ui.Gold, $"[b]Ruins[/b] of old works. Cleared, the town can restore it for {Js.Str(ruin.Cost)}g, and it {Ui.Esc(ruin.Gives)}."));
        string seen = sight switch
        {
            Sight.Live when scry != null => Ui.C(new Color("b48cff"), $"[b]Scrying[/b] {(scry.CasterId == 0 ? "in the Scrying Pool" : $"by {Ui.Esc(t.Agent(scry.CasterId).Name)}")}, {scry.EndTick - t.Tick} hours left. You see it as it is now."),
            Sight.Live when live == LiveSource.Mirror => Ui.C(new Color("cfe3f0"), "[b]Mirror[/b]: the hidden mirror shows it as it is now, until someone finds it."),
            Sight.Live when live == LiveSource.Scout && scout != null => Ui.C(Ui.Good, $"[b]Watched[/b] by {Ui.Esc(t.Agent(scout.HeroId).Name)} from cover, rations for {Ui.Days((int)Math.Ceiling((scout.LeaveTick - t.Tick) / 24.0))}."),
            Sight.Live when live == LiveSource.Outpost && Loyalty.Lord(t, s.Def.Id) is Agent lord => Ui.C(Ui.Good, $"[b]Held[/b] by the retainers of {Ui.Esc(Loyalty.Styled(t, lord))}. Nothing moves back in."),
            Sight.Live when live == LiveSource.Outpost => Ui.C(Ui.Good, $"[b]Held[/b] by a garrison of the watch, {Js.Str(t.Content.Lairs.Outpost.Upkeep)}g a day. Nothing moves back in."),
            Sight.Live when live == LiveSource.Spire => Ui.C(Ui.Gold, "[b]In the Spire's sight[/b], day and night."),
            Sight.Live when live == LiveSource.Tower && Watch.TowerOver(t, s) is { } tower => Ui.C(Ui.Gold, $"[b]In sight[/b] of {Ui.Esc(tower.Name)}, day and night."),
            Sight.Live => "In sight of the town.",
            Sight.Scouted when report != null => t.Day - report.Day > Fog.StaleDays
                ? $"Last seen on day {report.Day}, in a report {t.Day - report.Day} days old. Things may have changed."
                : $"Scouted on day {report.Day}. You know it as the party described it, not as it is now.",
            _ when t.Finds.LastOrDefault(f => f.SiteId == s.Def.Id && f.Known) is { } find => $"Found by the {Ui.Esc(Explore.Survey(t, find.SurveyId)!.Name)} on day {SimTime.DayOf(find.FoundTick)}, who sent word of it.",
            _ => s.HeardFrom != ""
                ? $"Only hearsay, from {Ui.Esc(s.HeardFrom)}. The map shows roughly where; nobody has seen it. Send explorers to find it."
                : "Only a name in tavern gossip. Nobody who went there has come back to describe it.",
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
            if ((s.Habit != "" || s.Def.Kind == "undead") && Lairs.HabitText(t, s) is { Length: > 0 } habit) p.Text($"{Tag("Habit", Ui.Gold)} {Ui.Esc(habit)}");
        }
        if (sight == Sight.Live && live != null)
        {
            var there = t.Parties.Where(x => !x.Returned && x.SiteId == s.Def.Id && Knowledge.LegOf(t, x) == Leg.AtSite).ToList();
            foreach (var party in there) p.Text($"You watch the {Ui.Esc(party.Name)} at the mouth of the site, readying to go in.");
        }
        if (sight == Sight.Live && !s.Cleared)
        {
            {
                int swell = Lairs.Swell(t, s);
                p.Text(swell == 0 ? "Few of them about; the lair is quiet." : swell == 1 ? Ui.C(Ui.Gold, "The lair is growing: more of them in every fight.") : Ui.C(Ui.Bad, "The lair is swollen and bold: many more in every fight."));
            }
        }
        var chrons = t.Chronicles.Count(c => c.SiteName == s.Def.Name);
        if (chrons > 0) p.Text($"{chrons} {(chrons == 1 ? "chronicle" : "chronicles")} written here. See the Chronicles tab.");
        string sid = s.Def.Id;
        if (s.Cleared)
        {
            if (Ruins.Of(t, s) is { } old)
            {
                if (s.Restored)
                {
                    p.Text(Ui.C(Ui.Good, $"Restored. It {Ui.Esc(old.Gives)}."));
                    return;
                }
                p.Heading("Restore it");
                p.Buttons(($"Restore it, {Js.Str(old.Cost)}g", t.Treasury >= old.Cost, true, () => Act(() => _sim.RestoreRuin(sid))));
                p.Note($"Masons put it back in order, and it {old.Gives}. Nothing moves back in.");
            }
            if (Loyalty.Lord(t, s.Def.Id) is Agent lord)
            {
                p.Text($"The land of {AgentLink(lord, Loyalty.Styled(t, lord))}, held by their own retainers.");
                return;
            }
            // A landless knight can be granted it, whether or not the watch garrisons it now.
            var knight = t.Heroes.Where(h => h.Hero!.Knight && h.Hero.FiefId == null).OrderByDescending(h => h.Hero!.Loyalty).ThenBy(h => h.Id).FirstOrDefault();
            if (knight != null)
            {
                string? no = Loyalty.FiefBlocked(t, knight, s);
                int kid = knight.Id;
                p.Heading("Grant it");
                p.Buttons(($"Grant it to Ser {knight.GivenName}, {Js.Str(t.Content.Loyalty.Fief.Cost)}g", no == null, true, () => Act(() => _sim.GrantFief(kid, sid))));
                p.Note(no == null ? $"Their retainers hold it at no cost to the town, and they take {Js.Str(t.Content.Loyalty.Fief.Rent)}g a day from the land." : Ui.Cap(no) + ".");
            }
            if (s.Held) return;
            // Something will move back in unless the town holds it.
            string? blocked = Lairs.HoldBlocked(t, s);
            p.Heading("Hold it");
            var o = t.Content.Lairs.Outpost;
            p.Buttons(($"Garrison an outpost, {Js.Str(o.Cost)}g", blocked == null, true, () => Act(() => _sim.HoldSite(sid))));
            p.Note(blocked == null ? $"{Js.Str(o.Upkeep)}g a day. Nothing resettles it, and you see it live." : Ui.Cap(blocked) + ".");
            return;
        }
        p.Heading("Eyes on it");
        if (live == null)
        {
            var k = t.Content.Knowledge;
            var buttons = new List<(string, bool, bool, Action)>();
            if (Watch.FreeScryingsLeft(t) > 0) buttons.Add(("Scry it free, from the pool", true, true, () => Act(() => _sim.CommissionScrying(sid))));
            // Every rested caster says what they want for it: the loyal nothing, others gold, the disaffected a favour.
            foreach (var o in Knowledge.Offers(t, s.Def).Take(4))
            {
                int id = o.Caster.Id;
                string terms = o.Price switch { ScryPrice.Free => "free", ScryPrice.Gold => $"{Math.Round(o.Gold)}g", _ => "as a favour" };
                buttons.Add(($"{o.Caster.GivenName}: {terms}", t.Treasury >= o.Gold, buttons.Count == 0, () => Act(() => _sim.CommissionScrying(sid, id))));
            }
            if (buttons.Count > 0) p.Buttons([.. buttons]);
            int casters = Knowledge.Scryers(t).Count, rested = Knowledge.Rested(t).Count;
            p.Note(casters == 0 && buttons.Count == 0 ? $"Needs a {ScryClasses(t)} of level {k.ScryMinLevel} or more in town."
                : $"{Ui.Img("face-all-seeing-eye")} {rested}/{casters} ready · {k.ScryHours}h · rest {Ui.Days(k.ScryRestDays)}\n{Ui.Img("standing")} loyal free · {Ui.Img("gold")} others gold · {Ui.Img("favour")} disaffected a favour");
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
