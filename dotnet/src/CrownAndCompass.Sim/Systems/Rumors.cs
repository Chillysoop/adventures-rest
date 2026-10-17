namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Something said over ale about a site. It names who said it and how far they can be trusted, and
/// may be wrong: True records whether it matched the world when it was said, which the town only
/// learns (Proven) when a party reports or someone sees the place.
/// </summary>
public sealed class Rumor
{
    public int Id { get; init; }
    public int Tick { get; init; }
    public required string SiteId { get; init; }
    public required string Source { get; init; }

    /// <summary>The townsperson or hero who told it, or null for a traveller passing through.</summary>
    public int? SourceId { get; init; }
    /// <summary>The tavern it was heard in, or -1 when planted elsewhere.</summary>
    public int TavernId { get; init; }
    public required string Text { get; init; }

    /// <summary>How far the teller can be trusted, 0 to 100; the chance the tale is true.</summary>
    public int Reliability { get; init; }
    public bool True { get; init; }

    /// <summary>What the claim says lives there, if it says.</summary>
    public string Who { get; init; } = "";

    /// <summary>Null until a report or a sighting settles it.</summary>
    public bool? Proven { get; set; }
}

/// <summary>
/// The tavern as the town's exchange (GDD rules W3 and C9). Sites start unheard of, past the local
/// menace the settlers know. Each evening's talk at each tavern can turn up a rumour: a place, who
/// holds it, how dangerous it is, or that a cleared ruin has been taken again. A rumour carries its
/// teller and how reliable they are, and can be false; the town acts on what it believes (bounties,
/// heroes sizing up a job) until a party comes home or a scrying, scout or mirror shows the truth.
/// </summary>
public static class Rumors
{
    private static RumorRules Rules(Town town) => town.Content.Rumors;

    /// <summary>Who lives at a site, as a tavern would put it: "goblins", "giant spiders", "an ogre".</summary>
    public static string Occupants(Content content, SiteDef site)
    {
        var boss = site.Boss;
        if (!string.IsNullOrEmpty(boss.Escort)) return content.Monster(boss.Escort).Plural;
        var def = content.Monster(boss.Monster);
        if (boss.Count[^1] > 1) return def.Plural;
        return def.Named ? def.Name : $"{Agent.Article(def.Name)} {def.Name}";
    }

    public static string Days(int distance) => distance switch
    {
        1 => "a day",
        2 => "two days",
        3 => "three days",
        4 => "four days",
        _ => $"{distance} days",
    };

    public static string Trust(int reliability) =>
        reliability >= 70 ? "a reliable sort" : reliability >= 40 ? "half believed" : "most likely ale talk";

    /// <summary>The town hears of a site, or hears something new about it.</summary>
    public static void Hear(Town town, SiteState site, string who, int tier, int trust = 100)
    {
        site.Trust = trust;
        if (site.Known == SiteKnowledge.Unheard) site.Known = SiteKnowledge.Rumoured;
        site.Believed = who;
        site.BelievedTier = tier;
    }

    public static void Daily(Town town)
    {
        foreach (var tavern in town.Taverns)
        {
            var r = Rules(town);
            double chance = Math.Min(r.MaxChance, r.BaseChance + r.ChancePerPatron * tavern.Talk);
            tavern.Talk = 0;
            if (town.Rng.Chance(chance)) Spread(town, tavern);
        }
        // Whatever the town can see right now, it knows for certain.
        foreach (var (site, _) in Watch.LiveSites(town))
            if (site.Known != SiteKnowledge.Confirmed || site.ThoughtCleared != site.Cleared || site.Believed != Occupants(town.Content, site.Def))
                Confirm(town, site, "Eyes on the place");
    }

    private static (string Text, int? Id, int Reliability) Teller(Town town, Building tavern)
    {
        var r = Rules(town);
        var rng = town.Rng;
        double roll = rng.Next();
        if (roll < r.HeroShare)
        {
            var heroes = town.Heroes.Where(a => !a.IsAway).ToList();
            if (heroes.Count > 0)
            {
                var h = heroes[rng.Range(0, heroes.Count)];
                int rel = rng.Range(r.HeroReliability[0], r.HeroReliability[1] + 1) + (Watch.CanScout(town, h) ? 10 : 0);
                return ($"{h.Name} the {h.Hero!.Cls.Name}", h.Id, Math.Min(95, rel));
            }
        }
        else if (roll < r.HeroShare + r.ResidentShare)
        {
            var locals = town.Workers;
            if (locals.Count > 0)
            {
                var a = locals[rng.Range(0, locals.Count)];
                int rel = rng.Range(r.ResidentReliability[0], r.ResidentReliability[1] + 1) + Rng.Modifier(a.Abilities.Wis) * 5;
                return (a.Name, a.Id, Math.Clamp(rel, 5, 95));
            }
        }
        var t = r.Travellers[rng.Range(0, r.Travellers.Count)];
        return (t.Text, null, rng.Range(t.Reliability[0], t.Reliability[1] + 1));
    }

    /// <summary>How much a tavern is likely to talk about each site: the unheard of and the near most.</summary>
    private static double Weight(Town town, SiteState s)
    {
        if (s.Known == SiteKnowledge.Unheard) return Rules(town).UnheardWeight / Math.Max(1, s.Def.Distance);
        // Talk of a cleared ruin taken again starts only once it has had time to be.
        if (s.ThoughtCleared) return town.Tick - (s.ClearedTick ?? 0) < Rules(town).QuietDays * SimTime.HoursPerDay ? 0 : s.Cleared ? 0.03 : 2;
        return s.Known == SiteKnowledge.Rumoured ? 0.4 : 0;
    }

    /// <summary>Someone the town has never seen there, in the same country: what a false tale names.</summary>
    private static string Mistaken(Town town, SiteState s, string truth)
    {
        var pool = town.Content.Wanderers(s.Def, [0.125, s.Def.Tier + 1]).Where(m => m.Plural != truth).ToList();
        return pool.Count == 0 ? truth : town.Rng.Pick(pool).Plural;
    }

    private static void Spread(Town town, Building tavern)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var sites = town.Sites.Where(s => Weight(town, s) > 0).ToList();
        if (sites.Count == 0) return;
        // Most nights the talk is of nothing much; news is likelier when there is news to have.
        double total = Js.Sum(sites, s => Weight(town, s));
        double pick = rng.Next() * (total + 1);
        if (pick >= total) return;
        var site = sites[^1];
        foreach (var s in sites)
        {
            pick -= Weight(town, s);
            if (pick < 0)
            {
                site = s;
                break;
            }
        }

        var (source, sourceId, reliability) = Teller(town, tavern);
        bool truth = rng.Chance(reliability / 100.0);
        string occupants = Occupants(town.Content, site.Def);
        string who = occupants;
        int tier = site.Def.Tier;
        string text;
        var importance = Importance.Routine;
        if (site.ThoughtCleared)
        {
            // Smoke over a ruin the town thought was done with: true if something really moved in.
            if (site.Cleared == truth) return;
            if (!truth) who = Mistaken(town, site, occupants);
            text = Fill(rng.Pick(r.Resettled), source, site, who, tier, r);
            site.ThoughtCleared = false;
            site.Known = SiteKnowledge.Rumoured;
            Hear(town, site, who, Math.Max(1, site.BelievedTier), reliability);
            importance = Importance.Notable;
        }
        else if (site.Known == SiteKnowledge.Unheard)
        {
            if (!truth)
            {
                who = Mistaken(town, site, occupants);
                tier = Math.Max(1, tier + (rng.Chance(0.5) ? -1 : 1));
                tier = Math.Min(3, tier);
            }
            text = Fill(rng.Pick(r.Place), source, site, who, tier, r);
            Hear(town, site, who, tier, reliability);
            importance = Importance.Notable;
        }
        else if (rng.Chance(0.5))
        {
            if (!truth) tier = Math.Clamp(tier + (rng.Chance(0.5) ? -1 : 1), 1, 3);
            text = Fill(rng.Pick(r.Strength), source, site, site.Believed, tier, r);
            who = "";
            Hear(town, site, site.Believed, tier, reliability);
        }
        else
        {
            if (!truth) who = Mistaken(town, site, occupants);
            text = Fill(rng.Pick(r.Occupant), source, site, who, tier, r);
            Hear(town, site, who, site.BelievedTier, reliability);
        }

        var rumor = new Rumor
        {
            Id = town.Rumors.Count + 1,
            Tick = town.Tick,
            SiteId = site.Def.Id,
            Source = source,
            SourceId = sourceId,
            TavernId = tavern.Id,
            Text = text,
            Reliability = reliability,
            True = truth,
            Who = who,
        };
        town.Rumors.Add(rumor);
        town.Log(LedgerCategory.Rumor, importance, $"At {tavern.Name}: {text} ({Trust(reliability)})", sourceId != null ? [sourceId.Value] : []);
    }

    /// <summary>
    /// Plant a rumour from outside the tavern's own talk: a faction's agent, a cult's lie, a guild's
    /// tip. It goes in the Ledger and the rumour list like any other, at the busiest tavern (or none),
    /// and the town believes it until a report or a sighting settles it.
    /// </summary>
    public static Rumor Plant(Town town, SiteState site, string source, int? sourceId, string text, int reliability, bool truth, string who = "", int? tier = null)
    {
        var tavern = town.Taverns.FirstOrDefault();
        if (who != "" || tier != null) Hear(town, site, who != "" ? who : site.Believed, tier ?? site.BelievedTier, reliability);
        var rumor = new Rumor
        {
            Id = town.Rumors.Count + 1,
            Tick = town.Tick,
            SiteId = site.Def.Id,
            Source = source,
            SourceId = sourceId,
            TavernId = tavern?.Id ?? -1,
            Text = text,
            Reliability = reliability,
            True = truth,
            Who = who,
        };
        town.Rumors.Add(rumor);
        town.Log(LedgerCategory.Rumor, Importance.Notable, $"{(tavern != null ? $"At {tavern.Name}: " : "")}{text} ({Trust(reliability)})", sourceId != null ? [sourceId.Value] : []);
        return rumor;
    }

    private static string Fill(string template, string source, SiteState site, string who, int tier, RumorRules r)
    {
        string text = template
            .Replace("{source}", source, StringComparison.Ordinal)
            .Replace("{site}", site.Def.Name, StringComparison.Ordinal)
            .Replace("{who}", who, StringComparison.Ordinal)
            .Replace("{days}", Days(site.Def.Distance), StringComparison.Ordinal)
            .Replace("{tier}", r.TierWords.Count > tier ? r.TierWords[tier] : $"tier {tier}", StringComparison.Ordinal);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    /// <summary>
    /// The truth about a site reaches town: a party's report, or someone seeing it. Belief snaps to the
    /// world, open rumours about the place are settled, and the town hears which tale was wrong.
    /// </summary>
    public static void Confirm(Town town, SiteState site, string by)
    {
        site.Known = SiteKnowledge.Confirmed;
        site.Believed = Occupants(town.Content, site.Def);
        site.BelievedTier = site.Def.Tier;
        site.ThoughtCleared = site.Cleared;
        site.Trust = 100;
        Rumor? wrong = null;
        foreach (var r in town.Rumors)
        {
            if (r.SiteId != site.Def.Id || r.Proven != null) continue;
            r.Proven = r.True;
            if (!r.True) wrong ??= r;
        }
        if (wrong == null) return;
        string what = wrong.Who != "" ? $"talk of {wrong.Who} at {site.Def.Name}" : $"word on {site.Def.Name}";
        town.Log(
            LedgerCategory.Rumor,
            Importance.Routine,
            $"{Cap(by)} put paid to {wrong.Source}'s {what}: {(site.Cleared ? "the place is empty" : $"it is {site.Believed}")}.",
            wrong.SourceId != null ? [wrong.SourceId.Value] : []);
    }

    /// <summary>Rumours about a site, newest first.</summary>
    public static List<Rumor> About(Town town, string siteId) => town.Rumors.Where(r => r.SiteId == siteId).OrderByDescending(r => r.Id).ToList();

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
