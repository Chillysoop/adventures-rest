namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The Director, a pacing hand modelled on a good Dungeon Master. It never decides a roll. It reads
/// recent drama (tension) and sets the tempo: after a hard blow it slows the warband and sends
/// help; after a long quiet stretch it lets the warband gather faster and sends a stranger with a
/// commission. It also makes sure the campaign reaches its climax in a reasonable time.
/// </summary>
public static class Director
{
    /// <summary>The Director's multiplier on the warband's growth today.</summary>
    public static double Daily(Town town)
    {
        town.Tension *= 0.9;
        var entries = town.Ledger.Entries;
        for (int i = Math.Max(0, entries.Count - 80); i < entries.Count; i++)
        {
            var e = entries[i];
            if (town.Tick - e.Tick >= SimTime.HoursPerDay) continue;
            if (e.Category == LedgerCategory.Brawl) town.Tension += 2;
            if (e.Category == LedgerCategory.Quest && e.Importance == Importance.Major) town.Tension += 4;
        }
        if (!Threat.IsActive(town)) return 0;

        town.QuietDays = town.Tension < 10 ? town.QuietDays + 1 : 0;
        double pace = 1;
        if (town.Tension > 40) pace = 0.5;
        else if (town.QuietDays > 10) pace = 1.5;
        // Every campaign reaches its climax: past day 120 the warband gathers in earnest.
        if (town.Day > 120) pace = Math.Max(pace, 1 + (town.Day - 120) / 30.0);

        if (town.Tension > 45 && town.Rng.Chance(0.25)) SendHelp(town);
        else if (town.QuietDays > 12 && town.Rng.Chance(0.2)) SendStranger(town);
        return pace;
    }

    private static void SendHelp(Town town)
    {
        if (town.Taverns.Count == 0) return;
        var cls = town.Content.Classes[town.Rng.Range(0, town.Content.Classes.Count)];
        var a = Quests.CreateHero(town, cls, town.Rng.Chance(0.5) ? 3 : 2);
        town.Tension -= 15;
        town.Log(
            LedgerCategory.Arrival,
            Importance.Notable,
            $"Word of {town.Name}'s troubles has travelled. {a.Describe()} rides in, saying they owe the old mayor a debt.",
            [a.Id]);
    }

    /// <summary>A hooded stranger pays for a bounty on the warband's camp, or on whatever threat is nearest.</summary>
    private static void SendStranger(Town town)
    {
        var camp = town.Sites.FirstOrDefault(s => s.Def.Id == town.Content.Enemy.Outpost.Id && !s.Cleared);
        var target = camp ?? town.Sites.Where(s => !s.Cleared).OrderBy(s => s.Def.Tier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
        if (target == null) return;
        if (town.Bounties.Any(b => b.SiteId == target.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) return;
        double reward = Quests.SuggestedReward(target.Def.Tier) + 40;
        town.Book(TreasuryAccount.Patron, reward);
        if (Quests.PostBounty(town, target.Def.Id, reward) == null)
        {
            town.Book(TreasuryAccount.Patron, -reward);
            return;
        }
        town.QuietDays = 0;
        town.Log(LedgerCategory.Town, Importance.Major, $"A hooded stranger leaves a purse of {Js.Str(reward)} gold at the town hall, for whoever clears {target.Def.Name}. No name is given.");
    }
}
