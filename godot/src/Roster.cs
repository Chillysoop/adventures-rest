using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The roster at the top of the companies window: every adventuring company (or every hero) in a table to sort
/// and filter, and for the company picked, its record and the jobs the council could offer it by name, each with
/// how the company leans and why.
/// </summary>
public partial class Main
{
    private bool _rosterHeroes;
    private int _rosterSort = 2, _heroSort = 1;
    private bool _rosterAsc, _heroAsc;
    private string _rosterShow = "all", _rosterClass = "";
    private string? _rosterPick;
    private string _offerTerms = "slay";
    private double _offerPay = 1;
    private (string Company, string Site, JobAnswer Answer)? _offerAnswer;

    private static readonly (string Label, double Pay)[] OfferPays = [("¾ of the asking", 0.75), ("The asking", 1), ("Half again", 1.5), ("Double", 2)];

    /// <summary>The class filter's choices: any, then each role, then each class.</summary>
    private static List<(string Key, string Label)> ClassChoices(Town t) =>
        new List<(string, string)> { ("", "Any class") }
            .Concat(t.Content.Classes.Select(c => c.Role).Where(r => r != "").Distinct().Order(StringComparer.Ordinal).Select(r => (r, $"Any {r}")))
            .Concat(t.Content.Classes.Select(c => (c.Id, Ui.Cap(c.Name))))
            .ToList();

    private static (string Word, Color Color) LeaningWord(Leaning l) => l switch
    {
        Leaning.Eager => ("Eager", Ui.Good),
        Leaning.Willing => ("Willing", Ui.Good),
        Leaning.Unsure => ("Unsure", Ui.Gold),
        Leaning.Reluctant => ("Reluctant", Ui.Bad),
        _ => ("Unwilling", Ui.Bad),
    };

    private void RenderRoster(Pane p, Town t)
    {
        p.Heading("Roster");
        p.Buttons(
            ("Companies", true, !_rosterHeroes, () => Act(() => _rosterHeroes = false)),
            ("Heroes", true, _rosterHeroes, () => Act(() => _rosterHeroes = true)));
        p.Buttons(
            ("Everyone", true, _rosterShow == "all", () => Act(() => _rosterShow = "all")),
            ("Ready to ride", true, _rosterShow == "ready", () => Act(() => _rosterShow = "ready")),
            ("In town", true, _rosterShow == "town", () => Act(() => _rosterShow = "town")));
        var classes = ClassChoices(t);
        int ci = Math.Max(0, classes.FindIndex(c => c.Key == _rosterClass));
        p.Choice("With a", classes.Select(c => c.Label).ToList(), ci, i => Act(() => _rosterClass = classes[i].Key));
        if (_rosterHeroes) RenderHeroTable(p, t);
        else RenderCompanyTable(p, t);
    }

    private void RenderCompanyTable(Pane p, Town t)
    {
        var all = Roster.Companies(t);
        var shown = all
            .Where(e => _rosterShow switch { "ready" => e.Ready >= 1 && e.Road == null, "town" => e.Members.Any(m => !m.IsAway), _ => true })
            .Where(e => _rosterClass == "" || e.Has(_rosterClass))
            .ToList();
        // Each column sorts by its own figure, and ties fall back to the name so the order holds still.
        Func<RosterEntry, double>[] keys =
        [
            e => 0,
            e => e.Ready,
            e => e.Power,
            e => e.Loyalty,
            e => e.Relation,
            e => e.Missions * 1000 + e.Ventures,
            e => e.GoodDeeds,
            e => e.Renown - e.Infamy,
            e => e.Road != null ? 0 : e.Ready > 0 ? 2 : 1,
        ];
        var sorted = (_rosterSort == 0
            ? shown.OrderBy(e => e.Name, StringComparer.Ordinal)
            : _rosterAsc ? shown.OrderBy(keys[_rosterSort]).ThenBy(e => e.Name, StringComparer.Ordinal) : shown.OrderByDescending(keys[_rosterSort]).ThenBy(e => e.Name, StringComparer.Ordinal)).ToList();
        if (_rosterSort == 0 && !_rosterAsc) sorted.Reverse();
        if (all.Count == 0)
        {
            p.Note("No company has formed yet. Heroes band together when they first set out on a bounty.");
            return;
        }
        if (_rosterPick == null || all.All(e => e.Name != _rosterPick)) _rosterPick = sorted.FirstOrDefault()?.Name ?? all[0].Name;
        var r = t.Content.World.Intel;
        var rows = sorted.Select(e =>
        {
            string makeup = string.Join(" · ", e.Members.Select(m => $"{Ui.Esc(Ui.Cap(m.Hero!.Cls.Name))} {m.Hero.Level}"));
            string fame = e.Infamy > 0 && Notoriety.InfamyTier(t, Intel.Find(t, e.Name)!) > 0 ? Ui.C(Ui.Bad, Notoriety.InfamyName(t, e.Infamy))
                : e.Renown > 0 ? Ui.C(Notoriety.FameTier(t, Intel.Find(t, e.Name)!) >= 2 ? Ui.Gold : Ui.Ink, Notoriety.FameName(t, e.Renown)) : Ui.C(Ui.Ink2, "Unknown");
            var (_, rc) = RelationWord(e.Relation, r.RefuseBelow);
            string state = e.State switch
            {
                "Ready" => Ui.C(Ui.Good, "Ready"),
                "On the road" => Ui.C(Ui.Gold, "On the road"),
                var s => Ui.C(Ui.Ink2, s),
            };
            return new[]
            {
                $"[b]The {Ui.Esc(e.Name)}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=11]{makeup}[/font_size]")}",
                $"{e.Ready}/{e.Members.Count}",
                $"[b]{e.Power}[/b]",
                Ui.C(e.Loyalty < 35 ? Ui.Bad : Ui.Ink, Js.Str(Math.Round(e.Loyalty))),
                Ui.C(rc, Js.Str(Math.Round(e.Relation))),
                e.Ventures == 0 ? Ui.C(Ui.Ink2, "none") : $"{e.Missions}/{e.Ventures}",
                e.GoodDeeds > 0 ? Ui.C(Ui.Good, e.GoodDeeds.ToString()) : Ui.C(Ui.Ink2, "0"),
                fame,
                state,
            };
        }).ToList();
        p.Table(
            ["Company", "Ready", "Power", "Loyalty", "Relation", "Missions", "Deeds", "Fame", "Now"],
            [0, 52, 52, 60, 66, 70, 52, 86, 86],
            rows,
            _rosterSort,
            !_rosterAsc,
            sorted.FindIndex(e => e.Name == _rosterPick),
            col => Act(() =>
            {
                if (col == _rosterSort) _rosterAsc = !_rosterAsc;
                else
                {
                    _rosterSort = col;
                    _rosterAsc = col == 0;
                }
            }),
            row => Act(() => _rosterPick = sorted[row].Name));
        if (sorted.Count == 0) p.Note("No company fits the filter.");
        p.Note("Ready counts members fit to ride now. Power is their levels plus each piece of magic gear. Missions are jobs done of trips made; deeds are the good deeds to the company's name. Click a column to sort, a company to see its record and offer it a job.");

        if (all.FirstOrDefault(e => e.Name == _rosterPick) is { } pick) RenderPick(p, t, pick);
    }

    private void RenderHeroTable(Pane p, Town t)
    {
        var heroes = t.Heroes.Where(h => !h.Dead)
            .Where(h => _rosterShow switch { "ready" => Quests.Available(h), "town" => !h.IsAway, _ => true })
            .Where(h => _rosterClass == "" || h.Hero!.Cls.Id == _rosterClass || h.Hero.Cls.Role == _rosterClass)
            .ToList();
        Func<Agent, double>[] keys =
        [
            h => 0,
            h => h.Hero!.Level * 100 + h.Hero.Magic.Count,
            h => 0,
            h => h.Hero!.Loyalty,
            h => h.Hero!.Renown - h.Hero.Infamy,
            h => h.Hero!.Kills,
            h => h.Hero!.Expeditions,
            h => h.IsAway ? 0 : Quests.Available(h) ? 2 : 1,
        ];
        var sorted = (_heroSort switch
        {
            0 => heroes.OrderBy(h => h.Name, StringComparer.Ordinal),
            2 => heroes.OrderBy(h => h.Hero!.PartyName ?? "~", StringComparer.Ordinal).ThenBy(h => h.Name, StringComparer.Ordinal),
            _ => _heroAsc ? heroes.OrderBy(keys[_heroSort]).ThenBy(h => h.Id) : heroes.OrderByDescending(keys[_heroSort]).ThenBy(h => h.Id),
        }).ToList();
        if (_heroSort is 0 or 2 && !_heroAsc) sorted.Reverse();
        var rows = sorted.Select(h =>
        {
            var s = h.Hero!;
            string temper = Regard.TemperOf(t, h)?.Name is string tn ? $" · {Ui.Esc(tn)}" : "";
            string state = h.IsAway ? Ui.C(Ui.Gold, "Away") : s.Duty != HeroDuty.None ? DutyIcon(h, 13) + Ui.C(Ui.Good, s.Duty switch { HeroDuty.Walls => "Walls", HeroDuty.Streets => "Streets", _ => "Command" }) : Quests.Available(h) ? Ui.C(Ui.Good, "Ready") : s.JailedUntil != null ? Ui.C(Ui.Bad, "In jail") : Ui.C(Ui.Ink2, "Resting");
            return new[]
            {
                $"[b]{Ui.Esc(Notoriety.Known(h))}[/b]\n{Ui.C(Ui.Ink2, $"[font_size=11]{Ui.Esc(Ui.Cap(s.Cls.Name))}{temper}{(s.Magic.Count > 0 ? $" · {s.Magic.Count} magic" : "")}[/font_size]")}",
                $"[b]{s.Level}[/b]",
                s.PartyName != null ? Ui.Esc(s.PartyName) : Ui.C(Ui.Ink2, "free"),
                Ui.C(s.Loyalty < 35 ? Ui.Bad : Ui.Ink, Js.Str(Math.Round(s.Loyalty))),
                s.Infamy > s.Renown ? Ui.C(Ui.Bad, $"−{s.Infamy}") : s.Renown.ToString(),
                s.Kills.ToString(),
                s.Expeditions.ToString(),
                state,
            };
        }).ToList();
        p.Table(
            ["Hero", "Level", "Company", "Loyalty", "Renown", "Kills", "Trips", "Now"],
            [0, 52, 130, 60, 64, 52, 52, 72],
            rows,
            _heroSort,
            !_heroAsc,
            -1,
            col => Act(() =>
            {
                if (col == _heroSort) _heroAsc = !_heroAsc;
                else
                {
                    _heroSort = col;
                    _heroAsc = col is 0 or 2;
                }
            }),
            row => OnLink($"agent:{sorted[row].Id}"));
        if (sorted.Count == 0) p.Note(t.Heroes.Count == 0 ? "No heroes have come to town yet." : "No hero fits the filter.");
        p.Note("Click a hero to open their sheet. A free hero rides with no company yet, and joins one that is short of three.");
    }

    /// <summary>The picked company: who rides with it, its record and latest deeds, and the jobs the council could offer it.</summary>
    private void RenderPick(Pane p, Town t, RosterEntry e)
    {
        var r = t.Content.World.Intel;
        var c = Intel.Find(t, e.Name);
        var (word, color) = RelationWord(e.Relation, r.RefuseBelow);
        string sulk = c?.SulkUntil is int until && t.Tick < until ? "  " + Tag("Sore", Ui.Bad) : "";
        string where = e.Road != null
            ? $"On the road to {SiteLink(Quests.Site(t, e.Road.SiteId)!.Def)}{(e.Road.BountyId != null && Intel.Commissioned(t, e.Road) ? ", on the council's job" : ", on their own account")}, due back day {SimTime.DayOf(e.Road.ReturnTick)}"
            : e.State == "Ready" ? "In town and ready for work" : e.State == "Away" ? "Out of town" : "In town, resting their wounds";
        string members = string.Join("\n", e.Members.Select(m =>
        {
            var s = m.Hero!;
            string hp = s.Hp < s.MaxHp ? Ui.C(s.Hp < s.MaxHp * 0.7 ? Ui.Bad : Ui.Ink2, $" · {s.Hp}/{s.MaxHp} hp") : "";
            string temper = Regard.TemperOf(t, m)?.Name is string tn ? $" · {Ui.Esc(tn)}" : "";
            return $"{AgentLink(m, Notoriety.Known(m))} {Ui.C(Ui.Ink2, $"{Ui.Esc(Ui.Cap(s.Cls.Name))} {s.Level}{temper} · loyalty {Math.Round(s.Loyalty)}")}{hp}";
        }));
        var record = new List<string>();
        if (c != null)
        {
            if (c.Bounties > 0) record.Add($"{c.Bounties} council {(c.Bounties == 1 ? "bounty" : "bounties")} done");
            if (c.Offers > 0) record.Add($"{c.Offers} {(c.Offers == 1 ? "job" : "jobs")} taken when asked");
            if (c.Declined > 0) record.Add($"{c.Declined} turned down");
            if (c.ReportsSold > 0) record.Add($"{c.ReportsSold} {(c.ReportsSold == 1 ? "report" : "reports")} sold for {Js.Str(Js.Round(c.GoldPaid))}g");
            if (c.Renown > 0 || c.Infamy > 0) record.Add($"renown {c.Renown}{(c.Infamy > 0 ? $", infamy {c.Infamy}" : "")}");
        }
        string deeds = c == null || c.Deeds.Count == 0 ? "" :
            "\n" + Ui.C(Ui.Ink2, "[font_size=12]Latest good deeds:[/font_size]") + "\n" +
            string.Join("\n", c.Deeds.AsEnumerable().Reverse().Take(4).Select(d => Ui.C(Ui.Good, $"[font_size=12]Day {d.Day}: {Ui.Esc(d.What)}[/font_size]")));
        p.Card($"[b]The {Ui.Esc(e.Name)}[/b]  {Tag(word, color)}{sulk}\n{Ui.C(Ui.Ink2, $"[font_size=12]{where}[/font_size]")}\n[font_size=13]{members}[/font_size]" +
               (record.Count > 0 ? $"\n{Ui.C(Ui.Ink2, $"[font_size=12]{string.Join(" · ", record)}[/font_size]")}" : "") + deeds, Ui.Gold);
        p.Meter("Relation", e.Relation, Js.Str(Math.Round(e.Relation)), e.Relation < r.RefuseBelow);
        var home = e.Members.Where(m => !m.IsAway).ToList();
        if (home.Count > 0)
        {
            var ids = home.Select(m => m.Id).ToList();
            p.Buttons(("Arm from the armory", home.Any(m => Sim.Systems.Armory.BestFor(t, m) != null), false, () => Act(() => _sim.Arm(ids))));
        }

        p.Heading($"Offer the {e.Name} a job");
        if (_offerAnswer is { } last && last.Company == e.Name)
            p.Card(last.Answer.Accepted
                ? Ui.C(Ui.Good, $"[b]They take the job on {Ui.Esc(last.Site)}[/b]") + $"\n[font_size=12]{Ui.Esc(Ui.Cap(last.Answer.Why))}. They march out on the council's coin, and their report comes to the town hall.[/font_size]"
                : Ui.C(Ui.Bad, $"[b]They turn down the job on {Ui.Esc(last.Site)}[/b]") + $"\n[font_size=12]{Ui.Esc(Ui.Cap(last.Answer.Why))}.[/font_size]",
                last.Answer.Accepted ? Ui.Good : Ui.Bad);
        if (Roster.Unavailable(t, e.Name) is string busy)
        {
            p.Note($"Not now: {Ui.Esc(busy)}.");
            return;
        }
        var term = t.Content.Bounties.Term(_offerTerms);
        p.Buttons(t.Content.Bounties.Terms.Select(x => (x.Name, true, x.Id == term.Id, (Action)(() => Act(() => _offerTerms = x.Id)))).ToArray());
        p.Buttons(OfferPays.Select(x => (x.Label, true, x.Pay == _offerPay, (Action)(() => Act(() => _offerPay = x.Pay)))).ToArray());
        p.Note($"[b]{Ui.Esc(term.Name)}:[/b] {Ui.Esc(term.Text)}");

        // Threats first, as on the bounty board, then the places they lean toward most.
        var named = Alerts.Standing(t).Where(a => a.SiteId != null).Select(a => a.SiteId!).ToHashSet();
        var jobs = t.Sites
            .Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared)
            .Select(s =>
            {
                var posted = t.Bounties.FirstOrDefault(b => b.SiteId == s.Def.Id && b.Status == BountyStatus.Open);
                double reward = posted?.Reward ?? Js.Round(Terms.Suggested(t, s, term) * _offerPay);
                return (Site: s, View: Roster.Weigh(t, e.Name, s.Def.Id, reward, term.Id));
            })
            // Places where these terms can't be posted (nobody held captive there...) stay off this list.
            .Where(x => x.View.Posted != null || Terms.Blocked(t, x.Site, term) == null)
            .Where(x => x.View.Blocked == null || !x.View.Blocked.Contains("already on that job"))
            .OrderBy(x => named.Contains(x.Site.Def.Id) || x.Site.Def.Id == t.Content.Enemy.Outpost.Id ? 0 : 1)
            .ThenBy(x => x.View.Blocked != null)
            .ThenByDescending(x => x.View.Score)
            .ThenBy(x => x.Site.Def.Id, StringComparer.Ordinal)
            .Take(8)
            .ToList();
        if (jobs.Count == 0) p.Note("The town knows of no place to send them on these terms.");
        foreach (var (s, v) in jobs)
        {
            int bt = Math.Clamp(s.BelievedTier, 1, 3);
            string tier = new string('◆', bt) + new string('◇', 3 - bt);
            string head = $"{SiteLink(s.Def)} {Ui.C(Ui.Gold, $"[font_size=11]{tier}[/font_size]")}" +
                          (v.Posted != null ? "  " + Tag($"bounty posted, {Js.Str(v.Reward)}g", Ui.Gold) : "");
            string sid = s.Def.Id, name = e.Name, site = s.Def.Name, terms = v.Terms.Id;
            double reward = v.Reward;
            if (v.Blocked != null)
            {
                p.Row($"{head}\n{Ui.C(Ui.Bad, $"[font_size=12]{Ui.Esc(Ui.Cap(v.Blocked))}.[/font_size]")}");
                continue;
            }
            var (lw, lc) = LeaningWord(v.Leaning);
            string why = string.Join("  ·  ", v.Reasons.Where(x => Math.Abs(x.Points) >= 0.01).Take(4).Select(x => Ui.C(x.Points > 0 ? Ui.Good : Ui.Bad, $"{(x.Points > 0 ? "+" : "−")} {Ui.Esc(x.Why)}")));
            string riders = string.Join(", ", v.Riders.Select(m => $"{Ui.Esc(m.GivenName)}{(m.Hero!.PartyName != e.Name ? " (free)" : "")}"));
            p.Row($"{head}  {Tag(lw, lc)}\n[font_size=12]{why}[/font_size]\n{Ui.C(Ui.Ink2, $"[font_size=12]Would ride: {riders}[/font_size]")}",
                ($"Offer {Js.Str(reward)}g", v.Posted != null || t.Treasury >= reward, v.Leaning <= Leaning.Willing,
                    () => Act(() =>
                    {
                        if (_sim.OfferJob(name, sid, reward, terms) is { } answer) _offerAnswer = (name, site, answer);
                    })));
        }
        p.Note("They answer on the spot. Eager and willing companies take the job; an unsure one might; a reluctant one rarely does. A job they take is posted as a council bounty and they march at once. A mean offer turned down sours them, and they won't hear the same job again today unless the pay goes up.");
    }
}
