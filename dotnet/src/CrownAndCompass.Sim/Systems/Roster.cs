namespace CrownAndCompass.Sim.Systems;

/// <summary>How a company leans on a job offered to it, from sure to take it to sure to refuse.</summary>
public enum Leaning
{
    Eager,
    Willing,
    Unsure,
    Reluctant,
    Unwilling,
}

/// <summary>A company as the roster shows it: who rides with it, how strong and loyal they are, and its record.</summary>
public sealed record RosterEntry(
    string Name,
    List<Agent> Members,
    int Ready,
    int Power,
    double Loyalty,
    double Relation,
    int Missions,
    int Ventures,
    int GoodDeeds,
    int Renown,
    int Infamy,
    Party? Road)
{
    /// <summary>Where the company is: on the road, ready for work, resting its wounds, or out of town.</summary>
    public string State => Road != null ? "On the road" : Ready > 0 ? "Ready" : Members.All(m => m.IsAway) ? "Away" : "Resting";

    /// <summary>"Heroes of the realm": the tier its best member has reached (<see cref="Roster.TierOf"/>).</summary>
    public string Tier => Roster.TierOf(Members);

    public bool Has(string classOrRole) => Members.Any(m => m.Hero!.Cls.Id == classOrRole || m.Hero.Cls.Role == classOrRole);
}

/// <summary>What a company makes of a job before the council offers it: who would ride, why they would or wouldn't, and how they lean.</summary>
public sealed record JobView(
    string Company,
    SiteState? Site,
    TermDef Terms,
    double Reward,
    bool LootToTown,
    Bounty? Posted,
    List<Agent> Riders,
    List<(string Why, double Points)> Reasons,
    double Score,
    Leaning Leaning,
    string? Blocked);

/// <summary>
/// A band of heroes that could ride today: a company with the free heroes who would make it up to three, or (with no
/// name) the free heroes on their own. <see cref="Offerable"/> is whether the company will hear a job by name.
/// </summary>
public sealed record Crew(string Name, List<Agent> Riders, bool Offerable);

/// <summary>A company's answer to a job offered by name.</summary>
public sealed record JobAnswer(bool Accepted, string Why, Party? Party);

/// <summary>
/// The roster of adventuring companies, and jobs offered to one of them by name. A posted bounty waits on the
/// board for whoever wants it; an offer goes to the company the mayor picks, which answers on the spot. Its
/// riders weigh it as they weigh any bounty (their share of the pay, their traits, whether the terms suit their
/// temper), and the company adds what it thinks of the council, the flattery of being asked, and the danger
/// against its strength (content/game/bounties.json "offers"). A company that takes the job posts its bounty and
/// marches at once, on the council's coin, so its report comes home to the town hall.
/// </summary>
public static class Roster
{
    private static OfferRules Rules(Town town) => town.Content.Bounties.Offers;

    /// <summary>Every company with a member still riding, friendliest first.</summary>
    public static List<RosterEntry> Companies(Town town) =>
        Intel.MembersByCompany(town).Select(x => Entry(town, x.Name, x.Members)).ToList();

    public static RosterEntry Entry(Town town, string name, List<Agent> members)
    {
        var c = Intel.Find(town, name);
        var trips = town.Chronicles.Where(ch => ch.PartyName == name).ToList();
        return new RosterEntry(
            name,
            members,
            members.Count(Quests.Available),
            members.Sum(Power),
            members.Count > 0 ? Js.Sum(members, m => m.Hero!.Loyalty) / members.Count : 0,
            Intel.Relation(town, name, members),
            trips.Count(ch => ch.Outcome == ExpeditionOutcome.Cleared || ch.GoalMet),
            trips.Count,
            c?.GoodDeeds ?? 0,
            c?.Renown ?? 0,
            c?.Infamy ?? 0,
            town.Parties.FirstOrDefault(p => !p.Returned && p.Name == name));
    }

    /// <summary>
    /// The tier a company has reached by its best member's level: local heroes (to 4), heroes of the realm (to 10),
    /// masters of the realm (to 16), masters of the world. Many companies work at once across the tiers; the best of
    /// them are the town's champions, who ride against each act's climax (<see cref="Saga"/>).
    /// </summary>
    public static string TierOf(IReadOnlyList<Agent> members)
    {
        int best = members.Count == 0 ? 0 : members.Max(m => m.Hero!.Level);
        return best <= 4 ? "Local heroes" : best <= 10 ? "Heroes of the realm" : best <= 16 ? "Masters of the realm" : "Masters of the world";
    }

    /// <summary>A hero card's key powers, at most three: sneak attacks, their healing and area spells, their best skill
    /// and the best magic they wear.</summary>
    public static List<string> KeyPowers(Town town, Agent a)
    {
        var s = a.Hero!;
        var cls = s.Cls;
        var powers = new List<string>();
        if (cls.Sneak) powers.Add("sneak attack");
        if (cls.HealSpell != null) powers.Add(cls.HealSpell.Name);
        if (cls.AreaSpell != null) powers.Add(cls.AreaSpell.Name);
        if (cls.Skills.Count > 0 && cls.Skills.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal).First() is { Value: > 1 } best)
            powers.Add($"{best.Key} +{best.Value}");
        if (s.Magic.Values.OrderByDescending(i => i.Tier).ThenBy(i => i.Id, StringComparer.Ordinal).FirstOrDefault() is { } item)
            powers.Add(Gear.Label(s, item));
        return powers.Take(3).ToList();
    }

    /// <summary>
    /// Who could ride on a job today: each company in town with someone fit to ride (made up to three with the
    /// strongest free heroes, as an offer would), then the free heroes on their own if three of them could go.
    /// Reads the town only.
    /// </summary>
    public static List<Crew> Crews(Town town)
    {
        var r = Rules(town);
        var free = town.Heroes
            .Where(h => h.Hero!.PartyName == null && Quests.Available(h))
            .OrderByDescending(h => h.Hero!.Level)
            .ThenBy(h => h.Id)
            .ToList();
        var crews = new List<Crew>();
        foreach (var (name, members) in Intel.MembersByCompany(town))
        {
            if (town.Parties.Any(p => !p.Returned && p.Name == name)) continue;
            var riders = members.Where(Quests.Available).Take(r.MaxRiders).ToList();
            if (riders.Count == 0) continue;
            // Only newcomers the company has room for make up the numbers.
            int room = town.Content.World.Companies.MaxMembers - members.Count;
            riders.AddRange(free.Take(Math.Max(0, Math.Min(room, 3 - riders.Count))));
            if (riders.Count >= 3) crews.Add(new Crew(name, riders, Unavailable(town, name) == null));
        }
        if (free.Count >= 3) crews.Add(new Crew("", free.Take(r.MaxRiders).ToList(), false));
        return crews;
    }

    /// <summary>A hero's weight in a fight, as the roster counts it: their level and each piece of magic gear they wear.</summary>
    public static int Power(Agent a) => a.Hero!.Level + a.Hero.Magic.Count;

    /// <summary>The heroes who would ride on a job: the company's fit members, strongest first, and free heroes in town to make up three.</summary>
    private static List<Agent> Riders(Town town, string company, Bounty job, int tier)
    {
        var r = Rules(town);
        var riders = Intel.Members(town, company).Where(Quests.Available).Take(r.MaxRiders).ToList();
        if (riders.Count == 0) return riders;
        var leader = riders[0];
        var free = town.Heroes
            .Where(h => h.Hero!.PartyName == null && Quests.Available(h))
            .OrderByDescending(h => leader.OpinionOf(h.Id))
            .ThenByDescending(h => h.Hero!.Level)
            .ThenBy(h => h.Id)
            .ToList();
        int room = town.Content.World.Companies.MaxMembers - Intel.Members(town, company).Count;
        foreach (var h in free)
        {
            if (room <= 0) break;
            int levels = riders.Sum(m => m.Hero!.Level);
            if (riders.Count >= 3 && (levels >= 3 * tier || riders.Count >= r.MaxRiders)) break;
            if (leader.OpinionOf(h.Id) < -10 || h.OpinionOf(leader.Id) < -10) continue;
            if (Quests.Motive(town, h, job, riders.Count + 1) < 0.5) continue;
            riders.Add(h);
            room--;
        }
        return riders;
    }

    /// <summary>Why the company won't hear any job today (sore, hostile, nobody fit to ride), or null.</summary>
    public static string? Unavailable(Town town, string company)
    {
        var members = Intel.Members(town, company);
        if (members.Count == 0) return "no one rides under that name any more";
        if (Intel.Find(town, company)?.SulkUntil is int until && town.Tick < until) return "they are still sore at the council and won't hear it";
        if (Intel.Relation(town, company, members) < town.Content.World.Intel.RefuseBelow) return "they won't work for this council";
        if (town.Parties.Any(p => !p.Returned && p.Name == company)) return "they are out on the road";
        if (members.Count(Quests.Available) == 0)
            return town.Parties.Any(p => !p.Returned && p.Name == company) ? "they are out on the road" : members.All(m => m.IsAway) ? "none of them are in town" : "none of them are fit to ride yet";
        return null;
    }

    /// <summary>What the company makes of this job: riders, reasons for and against, its leaning, or why it can't be offered at all.</summary>
    public static JobView Weigh(Town town, string company, string siteId, double reward, string terms = "slay", bool lootToTown = false)
    {
        var r = Rules(town);
        var s = Quests.Site(town, siteId);
        var live = s == null ? null : town.Bounties.FirstOrDefault(b => b.SiteId == siteId && Quests.IsActive(b));
        var posted = live?.Status == BountyStatus.Open ? live : null;
        var term = town.Content.Bounties.Term(posted?.Terms ?? terms);
        if (posted != null)
        {
            reward = posted.Reward;
            lootToTown = posted.LootToTown;
        }
        JobView No(string why) => new(company, s, term, reward, lootToTown, posted, [], [], 0, Leaning.Unwilling, why);

        if (s == null || s.Known == SiteKnowledge.Unheard) return No("the town knows of no such place");
        if (s.ThoughtCleared) return No($"the town thinks {s.Def.Name} cleared");
        if (live is { Status: BountyStatus.Taken, PartyId: int pid }) return No($"the {town.Parties[pid - 1].Name} are already on that job");
        if (posted == null && term.Id != terms) return No("no such terms");
        if (posted == null && Systems.Terms.Blocked(town, s, term) is string blocked) return No(blocked);
        if (Unavailable(town, company) is string busy) return No(busy);
        var c = Intel.Find(town, company);
        if (c != null && c.DeclinedDay == town.Day && c.DeclinedSite == siteId && reward <= c.DeclinedReward)
            return No("they turned this down today; offer more to ask again");
        if (posted == null && (reward <= 0 || town.Treasury < reward)) return No($"the treasury can't cover {Js.Str(reward)}g");

        int tier = Math.Max(1, s.BelievedTier);
        var job = posted ?? new Bounty { SiteId = siteId, Reward = reward, Terms = term.Id, LootToTown = lootToTown };
        var riders = Riders(town, company, job, tier);
        if (riders.Count < 3)
            return No($"only {riders.Count} of them can ride, and no free hero in town will make up three");
        if (!Passage.CanReach(town, riders, s)) return No($"{s.Def.Name} floats above the clouds, and none of them can fly there");

        int n = riders.Count;
        var reasons = new List<(string Why, double Points)>();
        double pay = Js.Sum(riders, h => Quests.PayRatio(town, h, job, n)) / n;
        string payWord = pay >= 1.2 ? "generous" : pay >= 0.85 ? "good" : pay >= r.Accept ? "fair" : pay >= 0.4 ? "thin" : "an insult";
        reasons.Add(($"{Js.Str(Js.Round(reward / n))}g each for {n} riders is {payWord}", pay - r.Accept));

        void Mood(Trait t, double points, string what)
        {
            var who = riders.Where(h => h.Has(t)).Select(h => h.GivenName).ToList();
            if (who.Count > 0) reasons.Add(($"{Expedition.ListNames(who)} {what}", points * who.Count / n));
        }
        Mood(Trait.Greedy, 0.2, "want the gold");
        Mood(Trait.HotTempered, 0.15, "are spoiling for a fight");
        Mood(Trait.Ambitious, 0.1, "want a name");
        Mood(Trait.Lazy, -0.3, "would rather stay at the tavern");

        var liked = riders.Where(h => Systems.Terms.Interest(town, h, job) > 0).Select(h => h.GivenName).ToList();
        var disliked = riders.Where(h => Systems.Terms.Interest(town, h, job) < 0).Select(h => h.GivenName).ToList();
        string terming = term.Id == "slay" ? "the job" : $"the terms ({term.Name.ToLowerInvariant()})";
        if (liked.Count > 0) reasons.Add(($"{Expedition.ListNames(liked)} like {terming}", town.Content.Bounties.Temper * liked.Count / n));
        if (disliked.Count > 0) reasons.Add(($"{Expedition.ListNames(disliked)} dislike {terming}", -town.Content.Bounties.Temper * disliked.Count / n));
        double cut = Js.Sum(riders, h => Armory.TermsCut(town, h, job)) / n;
        if (cut > 0) reasons.Add(("they'd hand the hoard's magic to the town", -cut));

        reasons.Add(("the council asked them by name", r.Asked));
        if (Retainers.Held(town, company)) reasons.Add(("they are on the town's retainer", Retainers.Bonus(town, company)));
        double rel = Intel.Relation(town, company, Intel.Members(town, company));
        if (Math.Abs(rel - 50) >= 5)
            reasons.Add((rel >= 50 ? "they think well of the council" : "they think little of the council", (rel - 50) * r.Relation));

        int levels = riders.Sum(h => h.Hero!.Level), need = 3 * tier;
        if (levels < need) reasons.Add(($"{s.Def.Name} looks too much for them (levels {levels}, it wants {need})", -r.Danger * (1 - (double)levels / need)));
        int shortBy = Notoriety.Short(town, company, s);
        if (shortBy > 0)
            reasons.Add(($"nobody at {s.Def.Name} has heard of them (renown {Notoriety.RenownOf(town, company)}, it suggests {Notoriety.Suggested(town, s)})", -town.Content.Notoriety.Renown.Unproven * shortBy));
        if (c != null && Notoriety.FameTier(town, c) >= r.PettyFame && tier <= r.PettyTier) reasons.Add(("it's beneath a company of their name", -r.Petty));

        double score = r.Accept + Js.Sum(reasons, x => x.Points);
        var leaning = score >= r.Eager ? Leaning.Eager : score >= r.Willing ? Leaning.Willing : score >= r.Unsure ? Leaning.Unsure : score >= r.Reluctant ? Leaning.Reluctant : Leaning.Unwilling;
        return new JobView(company, s, term, reward, lootToTown, posted, riders, reasons.OrderByDescending(x => Math.Abs(x.Points)).ToList(), score, leaning, null);
    }

    /// <summary>
    /// Offer the company the job: they take it and march at once, or turn it down and say why. Null when it can't
    /// be offered at all (<see cref="JobView.Blocked"/>).
    /// </summary>
    public static JobAnswer? Offer(Town town, string company, string siteId, double reward, string terms = "slay", bool lootToTown = false)
    {
        var r = Rules(town);
        var v = Weigh(town, company, siteId, reward, terms, lootToTown);
        if (v.Blocked != null) return null;
        var s = v.Site!;
        var c = Intel.Company(town, company);
        bool yes = v.Score + (town.Rng.Next() - 0.5) * r.Spread >= r.Accept;
        if (!yes)
        {
            c.Declined++;
            c.DeclinedSite = siteId;
            c.DeclinedReward = v.Reward;
            c.DeclinedDay = town.Day;
            // An offer far below what heroes ask for the place is taken as an insult.
            if (v.Posted == null && v.Reward < r.Insult * Systems.Terms.Suggested(town, s, v.Terms)) Intel.Deal(town, company, r.InsultDealings);
            var worst = v.Reasons.Where(x => x.Points < 0).OrderBy(x => x.Points).Select(x => x.Why).FirstOrDefault() ?? "they don't like the look of it";
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The {company} turn down the council's offer on {s.Def.Name}: {worst}.", Intel.Members(town, company).Select(m => m.Id).ToList());
            return new JobAnswer(false, worst, null);
        }
        var bounty = v.Posted ?? Quests.PostBounty(town, siteId, v.Reward, null, v.Terms.Id, v.LootToTown);
        if (bounty == null) return null;
        var party = Quests.Launch(town, v.Riders, s, bounty);
        c = Intel.Company(town, party.Name);
        c.Offers++;
        Intel.Deal(town, party.Name, r.AcceptDealings);
        var best = v.Reasons.Where(x => x.Points > 0).OrderByDescending(x => x.Points).Select(x => x.Why).FirstOrDefault() ?? "the council asked them by name";
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"Asked by name, the {party.Name} ({string.Join(", ", v.Riders.Select(m => $"{m.GivenName} the {m.Hero!.Cls.Name}"))}) take the council's job on {s.Def.Name} and {(s.Def.Distance == 0 ? "go down with lanterns" : "march out")}{Roads.Summary(party)}.",
            v.Riders.Select(m => m.Id).ToList());
        return new JobAnswer(true, best, party);
    }
}
