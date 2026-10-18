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
        var names = t.Heroes.Where(h => !h.Departed && !h.Dead && h.Hero!.PartyName != null).Select(h => h.Hero!.PartyName!)
            .Concat(t.Companies.Select(c => c.Name))
            .Distinct()
            .Select(n => (Name: n, Members: Intel.Members(t, n)))
            .Where(x => x.Members.Count > 0)
            .OrderByDescending(x => Intel.Relation(t, x.Name, x.Members))
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .ToList();
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
            string sulk = c?.SulkUntil is int until && t.Tick < until ? "  " + Tag("Sore", Ui.Bad) : "";
            string held = t.Parties.Count(x => x.Name == name && worth.Contains(x)) is int n && n > 0 ? "  " + Tag($"{n} {(n == 1 ? "report" : "reports")} to sell", Ui.Gold) : "";
            p.Row($"[b]The {Ui.Esc(name)}[/b]  {Tag(word, color)}{sulk}{held}\n{Ui.C(Ui.Ink2, $"[font_size=12]{where}[/font_size]")}\n[font_size=13]{roster}[/font_size]" +
                  $"\n{Ui.C(Ui.Ink2, $"[font_size=12]Members' loyalty {Math.Round(loyalty)} · dealings with the council {(c == null || Math.Abs(c.Dealings) < 0.5 ? "none yet" : (c.Dealings > 0 ? "+" : "−") + Math.Round(Math.Abs(c.Dealings)))}{(record.Count > 0 ? " · " + string.Join(" · ", record) : "")}[/font_size]")}");
            p.Meter("Relation", rel, Js.Str(Math.Round(rel)), rel < r.RefuseBelow);
        }
        p.Note("Relation is the members' loyalty plus how the council has dealt with the company: council bounties and fair prices raise it, haggling and insulting offers lower it. Friends sell their reports cheap.");
        p.Commit();
    }
}
