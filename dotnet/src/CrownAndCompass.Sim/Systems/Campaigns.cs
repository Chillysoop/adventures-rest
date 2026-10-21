namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Free companies running their own campaigns. Heroes no bounty has moved band together with people
/// near their level and pick a site themselves: one they have heard of, built for parties about their
/// level, not too far, better if it pays. Like players in an online game they outgrow the country near
/// town and move outward as they level. The mayor's bounties and the settlements' pleas only add pay
/// to a site, steering companies rather than commanding them.
/// </summary>
public static class Campaigns
{
    public static void Daily(Town town)
    {
        var rules = town.Content.World.Companies;
        if (town.Regions.Count == 0) return;
        var pool = town.Heroes.Where(Quests.Available).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).ToList();
        while (pool.Count >= rules.MinMembers)
        {
            var leader = pool[0];
            pool.RemoveAt(0);
            // Most days a company is happy to spend in the taverns.
            if (!town.Rng.Chance(rules.SetOutChance)) continue;
            var members = Recruit(leader, pool, rules);
            if (members.Count < rules.MinMembers) continue;
            var target = Choose(town, members, rules);
            if (target == null) continue;
            foreach (var m in members) pool.Remove(m);
            Launch(town, members, target);
        }
    }

    /// <summary>Old companions first, then friends, all within a few levels of the leader.</summary>
    private static List<Agent> Recruit(Agent leader, List<Agent> pool, CompanyRules rules)
    {
        var members = new List<Agent> { leader };
        string? company = leader.Hero!.PartyName;
        var recruits = pool
            .Where(h => Math.Abs(h.Hero!.Level - leader.Hero.Level) <= rules.LevelSpread)
            .Where(h => leader.OpinionOf(h.Id) >= -10 && h.OpinionOf(leader.Id) >= -10)
            .OrderByDescending(h => company != null && h.Hero!.PartyName == company ? 1 : 0)
            .ThenByDescending(h => leader.OpinionOf(h.Id))
            .ThenByDescending(h => h.Hero!.Level)
            .ThenBy(h => h.Id);
        foreach (var r in recruits)
        {
            if (members.Count >= rules.MaxMembers) break;
            members.Add(r);
        }
        return members;
    }

    /// <summary>What a company would earn at a site beyond the hoard: the bounty and any plea.</summary>
    public static double Pay(Town town, SiteState site)
    {
        double pay = 0;
        foreach (var b in town.Bounties)
            if (b.SiteId == site.Def.Id && b.Status == BountyStatus.Open) pay += b.Reward;
        foreach (var p in town.Pleas)
            if (p.SiteId == site.Def.Id && p.Status == PleaStatus.Open) pay += p.Reward;
        return pay;
    }

    /// <summary>The company's pick of the sites it has heard of, or null if nothing appeals today.</summary>
    public static SiteState? Choose(Town town, IReadOnlyList<Agent> members, CompanyRules rules)
    {
        var leader = members[0];
        // Well-armed companies reckon themselves a little stronger than their levels.
        double avg = members.Average(m => m.Hero!.Level) + Gear.Levels(town.Content, members);
        // Bold leaders reach a step higher, but only with a full company behind them.
        bool bold = (leader.Has(Trait.Ambitious) || leader.Has(Trait.HotTempered)) && members.Count >= rules.MaxMembers;
        int cap = (int)Math.Floor(avg) + (bold ? rules.BoldStep : 0);
        // Greenhorns with no veteran to lead them start somewhere gentler.
        if (members.Any(m => m.Hero!.Expeditions == 0) && !members.Any(m => m.Hero!.Expeditions >= rules.Veteran))
            cap = Math.Max(1, cap - rules.GreenStep);
        // When war looms, heroes keep close to home; with the warband marching, nobody leaves.
        var stage = town.Enemy.Stage;
        bool war = Threat.IsActive(town) && stage >= ThreatStage.Raids;
        if (Threat.IsActive(town) && stage == ThreatStage.Marching) return null;
        string camp = town.Content.Enemy.Outpost.Id;
        var busy = town.Parties.Where(p => !p.Returned).Select(p => p.SiteId).ToHashSet();
        SiteState? best = null;
        double bestScore = rules.MinScore;
        foreach (var site in town.Sites)
        {
            // Companies go on what the town believes: a place it has heard of and thinks still held.
            if (site.Known == SiteKnowledge.Unheard || site.ThoughtCleared || busy.Contains(site.Def.Id)) continue;
            if (!Passage.CanReach(town, members, site)) continue;
            // Nobody goes down under the town for glory until it stirs.
            if (Undercity.IsUnder(town, site) && !Undercity.Stirring(town, site)) continue;
            int level = Math.Max(1, site.BelievedTier);
            if (level > cap || (war && site.Def.Distance > rules.WarDays)) continue;
            var region = World.RegionOf(town, site);
            double fit = 1 - rules.Outleveled * Math.Max(0, avg - level);
            double pay = Pay(town, site) / (members.Count * level * 40.0);
            double score =
                fit +
                pay * (leader.Has(Trait.Greedy) ? 1.5 : 1) +
                (region != null && !region.Visited ? 0.15 : 0) +
                (site.Def.Id == camp ? rules.CampGlory : 0) -
                rules.Travel * site.Def.Distance -
                (leader.Has(Trait.Lazy) ? 0.1 * site.Def.Distance : 0) +
                town.Rng.Next() * 0.2;
            if (score > bestScore)
            {
                best = site;
                bestScore = score;
            }
        }
        return best;
    }

    private static void Launch(Town town, List<Agent> members, SiteState site)
    {
        var bounty = town.Bounties.FirstOrDefault(b => b.SiteId == site.Def.Id && b.Status == BountyStatus.Open);
        var party = Quests.Launch(town, members, site, bounty);
        var region = World.RegionOf(town, site);
        string where = region != null ? $"{site.Def.Name} in {region.Name}" : site.Def.Name;
        var plea = party.PleaId is int pid ? town.Pleas[pid - 1] : null;
        string why = bounty != null ? ", taking the bounty on it as they go"
            : plea != null ? $", answering the plea from {town.Settlements[plea.SettlementId].Name}"
            : ", chasing glory on their own account";
        town.Log(
            LedgerCategory.Quest,
            Importance.Notable,
            $"The {party.Name} ({string.Join(", ", members.Select(m => $"{m.GivenName} the {m.Hero!.Cls.Name} {m.Hero.Level}"))}) set out for {where}{why}{Roads.Summary(party)}.",
            members.Select(m => m.Id).ToList());
    }
}
