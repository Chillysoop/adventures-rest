using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The companies window: every adventuring company in town, what it thinks of the town and why, and
/// the reports companies brought home from trips the council didn't pay for, to buy or haggle over.
/// </summary>
public partial class Main
{
    private static (string Word, Color Color) RelationWord(double rel, double refuse) =>
        rel >= 75 ? ("Devoted", Ui.Good)
        : rel >= 60 ? ("Friendly", Ui.Good)
        : rel >= 40 ? ("Cordial", Ui.Ink)
        : rel >= refuse ? ("Wary", Ui.Gold)
        : ("Hostile", Ui.Bad);

    private void RenderCompanies()
    {
        var p = _panes[Tab.Companies];
        var t = _sim.Town;
        var r = t.Content.World.Intel;
        p.Begin();
        p.Note("Only a party out on a council bounty reports to the town hall. A company out on its own account keeps what it saw: its road stays in the fog until the council buys the report.");

        RenderBastions(p, t);

        p.Heading("Reports for sale");
        var sale = Intel.ForSale(t);
        // Reports with new ground first; the ones that would tell the town nothing new fold into one line.
        var worth = sale.Where(x => Intel.NewGround(t, x) > 0).OrderByDescending(x => Intel.NewGround(t, x)).ThenByDescending(x => x.ReturnTick).ThenBy(x => x.Id).ToList();
        if (worth.Count == 0) p.Note("No company holds a report of ground the town doesn't know.");
        foreach (var party in worth)
        {
            var site = Quests.Site(t, party.SiteId)!;
            var region = World.RegionOf(t, site);
            var chron = t.Chronicles.FirstOrDefault(c => c.PartyName == party.Name && c.DepartDay == SimTime.DayOf(party.DepartTick));
            string trip = party.SawCleared && chron?.Outcome != ExpeditionOutcome.Cleared ? "found empty"
                : chron?.Outcome == ExpeditionOutcome.Cleared ? "cleared" : "came back beaten from";
            int age = (t.Tick - party.ReturnTick) / SimTime.HoursPerDay;
            var what = new List<string>();
            if (Intel.NewRegion(t, party)) what.Add($"{Ui.Esc(region!.Name)} never described");
            if (Intel.Corrects(t, party)) what.Add(site.Known == SiteKnowledge.Confirmed ? "the town has the site wrong" : "first-hand word of the site");
            string fresh = age <= 7 ? "fresh" : age <= r.FreshDays ? $"{Ui.Days(age)} old" : "old news";
            string head = $"[b]The {Ui.Esc(party.Name)}[/b] {trip} {SiteLink(site.Def)}{(region != null ? Ui.C(Ui.Ink2, $" in {Ui.Esc(region.Name)}") : "")}\n" +
                          $"{Ui.C(Ui.Ink2, $"[font_size=12]Back day {SimTime.DayOf(party.ReturnTick)}, {fresh} · {string.Join(" · ", what)}[/font_size]")}";
            string? no = Intel.WontSell(t, party);
            double ask = Intel.Ask(t, party), offer = Intel.HaggleOffer(t, party);
            int id = party.Id;
            if (no != null) p.Row($"{head}\n{Ui.C(Ui.Bad, $"[font_size=12]They {no}.[/font_size]")}");
            else
                p.Row(head,
                    ($"Pay {Js.Str(ask)}g", t.Treasury >= ask, true, () => Act(() => _sim.BuyIntel(id))),
                    ($"Offer {Js.Str(offer)}g", t.Treasury >= offer, false, () => Act(() => _sim.BuyIntel(id, haggle: true))));
        }
        int stale = sale.Count - worth.Count;
        if (stale > 0) p.Note($"{stale} more {(stale == 1 ? "report covers" : "reports cover")} ground the town already knows, so nothing in them is worth buying.");
        p.Note($"Haggling saves gold but sours the company, and one that turns an offer down won't talk for {Ui.Days(r.SulkDays)}. Companies below {Js.Str(r.RefuseBelow)} won't sell to the council at all.");

        p.Heading("Companies");
        var names = Intel.MembersByCompany(t);
        if (names.Count == 0) p.Note("No company has formed yet. Heroes band together when they first set out.");
        foreach (var (name, members) in names)
        {
            var c = Intel.Find(t, name);
            double rel = Intel.Relation(t, name, members);
            double loyalty = Js.Sum(members, m => m.Hero!.Loyalty) / members.Count;
            var (word, color) = RelationWord(rel, r.RefuseBelow);
            var road = t.Parties.FirstOrDefault(x => !x.Returned && x.Name == name);
            string where = road != null ? $"On the road to {SiteLink(Quests.Site(t, road.SiteId)!.Def)}{(road.BountyId != null && Intel.Commissioned(t, road) ? ", on the council's bounty" : ", on their own account")}"
                : members.All(m => m.IsAway) ? "Out of town" : "In town";
            string roster = string.Join(", ", members.Select(m => $"{AgentLink(m, m.GivenName)} {Ui.C(Ui.Ink2, $"{Ui.Esc(m.Hero!.Cls.Name)} {m.Hero.Level}")}"));
            var record = new List<string>();
            if (c != null)
            {
                if (c.Bounties > 0) record.Add($"{c.Bounties} council {(c.Bounties == 1 ? "bounty" : "bounties")} done");
                if (c.ReportsSold > 0) record.Add($"{c.ReportsSold} {(c.ReportsSold == 1 ? "report" : "reports")} sold for {Js.Str(Js.Round(c.GoldPaid))}g");
                if (c.Refusals > 0) record.Add($"{c.Refusals} {(c.Refusals == 1 ? "offer" : "offers")} turned down");
            }
            string fame = c == null ? "" : (c.Renown > 0 ? "  " + Tag(Notoriety.FameName(t, c.Renown), Notoriety.FameTier(t, c) >= 2 ? Ui.Gold : Ui.Ink2) : "")
                + (c.Infamy > 0 && Notoriety.InfamyTier(t, c) > 0 ? "  " + Tag(Notoriety.InfamyName(t, c.Infamy), Ui.Bad) : "");
            if (c != null && (c.Renown > 0 || c.Infamy > 0)) record.Insert(0, $"renown {c.Renown}{(c.Infamy > 0 ? $", infamy {c.Infamy}" : "")}");
            string sulk = c?.SulkUntil is int until && t.Tick < until ? "  " + Tag("Sore", Ui.Bad) : "";
            string held = t.Parties.Count(x => x.Name == name && worth.Contains(x)) is int n && n > 0 ? "  " + Tag($"{n} {(n == 1 ? "report" : "reports")} to sell", Ui.Gold) : "";
            p.Row($"[b]The {Ui.Esc(name)}[/b]  {Tag(word, color)}{fame}{sulk}{held}\n{Ui.C(Ui.Ink2, $"[font_size=12]{where}[/font_size]")}\n[font_size=13]{roster}[/font_size]" +
                  $"\n{Ui.C(Ui.Ink2, $"[font_size=12]Members' loyalty {Math.Round(loyalty)} · dealings with the council {(c == null || Math.Abs(c.Dealings) < 0.5 ? "none yet" : (c.Dealings > 0 ? "+" : "−") + Math.Round(Math.Abs(c.Dealings)))}{(record.Count > 0 ? " · " + string.Join(" · ", record) : "")}[/font_size]")}");
            p.Meter("Relation", rel, Js.Str(Math.Round(rel)), rel < r.RefuseBelow);
        }
        p.Note("Relation is the members' loyalty plus how the council has dealt with the company: council bounties and fair prices raise it, haggling and insulting offers lower it. Friends sell their reports cheap. A famous company asks more for its reports; an infamous one takes what it can get.");
        p.Commit();
    }

    /// <summary>Companies asking leave to raise a bastion, and the bastions that stand: who holds them, their chest, facilities and hirelings.</summary>
    private void RenderBastions(Pane p, Town t)
    {
        var r = t.Content.Bastions;
        var pending = Bastions.Pending(t);
        var standing = t.Bastions.Where(b => !b.Abandoned).ToList();
        var empty = t.Bastions.Where(b => b.Abandoned && b.TakenBy == null).ToList();
        if (pending.Count == 0 && standing.Count == 0 && empty.Count == 0) return;
        p.Heading("Bastions");
        foreach (var q in pending)
        {
            var members = Intel.Members(t, q.Company);
            string lead = members.Count > 0 ? $", led by {AgentLink(members[0])} {Ui.C(Ui.Ink2, $"{Ui.Esc(members[0].Hero!.Cls.Name)} {members[0].Hero!.Level}")}" : "";
            string what = q.TakeOver is int bid ? $"to take on the empty {Ui.Esc(t.Bastions[bid - 1].Name)} for {Js.Str(q.Land)}g" : $"for ground on the edge of town (the land is worth {Js.Str(q.Land)}g)";
            int id = q.Id;
            p.Row($"[b]The {Ui.Esc(q.Company)}[/b]{lead} ask {what}.\n{Ui.C(Ui.Ink2, $"[font_size=12]They would live there and hire townsfolk to staff it. Silence in {Ui.Days(Bastions.DaysLeft(t, q))} is taken as leave.[/font_size]")}",
                ("Grant", true, true, () => Act(() => _sim.AnswerBastion(id, BastionAnswer.Grant))),
                ("Give the land", true, false, () => Act(() => _sim.AnswerBastion(id, BastionAnswer.Gift))),
                ("Refuse", true, false, () => Act(() => _sim.AnswerBastion(id, BastionAnswer.Refuse))));
        }
        if (pending.Count > 0)
            p.Note($"Granting sells them the land; giving it free wins the company's goodwill. Refusing sours them and keeps them from asking again for {Ui.Days(r.Council.RefuseDays)}.");
        foreach (var b in standing)
        {
            var keep = Bastions.Keep(t, b);
            var members = Bastions.Members(t, b);
            var lord = b.LordId is int l ? t.Agent(l) : null;
            int level = Bastions.Level(t, b);
            string state = !keep.Complete ? Tag("Rising", Ui.Gold) : b.Idle ? Tag("Can't pay its hirelings", Ui.Bad) : Tag("Open", Ui.Good);
            string head = $"[b]{BuildingLink(keep)}[/b] of the {Ui.Esc(b.Company)}  {state}\n" +
                          Ui.C(Ui.Ink2, $"[font_size=12]Held by {(lord != null ? Ui.Esc(lord.Name) : "no one")} · {members.Count} living there · chest {Js.Str(Js.Round(b.Chest))}g · founded day {b.FoundedDay}[/font_size]");
            var rows = new List<string>();
            int slots = Bastions.Slots(t, level);
            foreach (int fid in b.Facilities)
            {
                var f = t.Building(fid);
                string staff = !f.Complete ? "being built" : $"{f.Workers.Count}/{f.Def.Jobs} hired";
                rows.Add($"{BuildingLink(f)} {Ui.C(Ui.Ink2, $"({staff})")}");
            }
            for (int i = b.Facilities.Count; i < slots; i++) rows.Add(Ui.C(Ui.Ink2, "an empty plot"));
            string keepStaff = keep.Complete ? $"{keep.Workers.Count}/{keep.Def.Jobs} hired at the keep" : "";
            string next = slots <= b.Facilities.Count && Bastions.Slots(t, 20) > slots
                ? $"More facilities as its best reaches level {r.Slots.Where(s => s.Facilities > slots).Select(s => s.Level).DefaultIfEmpty(0).Min()}."
                : "";
            string recent = b.Recent.Count > 0 ? $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(b.Recent[^1])}[/font_size]")}" : "";
            p.Row($"{head}\n[font_size=13]{string.Join(" · ", rows)}{(keepStaff != "" ? " · " + keepStaff : "")}[/font_size]{recent}" + (next != "" ? $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{next}[/font_size]")}" : ""));
        }
        foreach (var b in empty) p.Row($"[b]{BuildingLink(Bastions.Keep(t, b))}[/b]  {Tag("Empty", Ui.Ink2)}\n{Ui.C(Ui.Ink2, $"[font_size=12]Once the {Ui.Esc(b.Company)}'. Another company may take it on.[/font_size]")}");
        int sentries = Bastions.Sentries(t);
        p.Note($"Members live at their bastion instead of the tavern, which keeps them loyal. Each week its facilities do their work: draughts, drill, arms, word of new places, alms, trade, training and recruits." +
               (sentries > 0 ? $" {sentries} sentries from the drill yards stand on the walls in a siege." : ""));
    }
}
