namespace CrownAndCompass.Sim.Systems;

/// <summary>"Last time in Briarford...": the big moments since a day, who is hurt or out on the road, and the open leads.</summary>
public sealed record RecapView(string Title, IReadOnlyList<string> Moments, IReadOnlyList<string> Hurt, IReadOnlyList<string> Leads);

/// <summary>
/// The recap a game master opens a session with, read off the town: the Ledger's major news since a day,
/// the heroes nursing wounds, on the road or newly fallen, and what is waiting to be done (captives on a
/// clock, a crisis brewing, sendings unanswered, bounties nobody has taken). It reads the town and changes nothing.
/// </summary>
public static class Recap
{
    private const int MaxMoments = 5;
    private const int MaxLines = 4;

    public static RecapView Build(Town town, int sinceDay)
    {
        int since = Math.Max(0, (sinceDay - 1) * SimTime.HoursPerDay);
        var moments = town.Ledger.Entries
            .Where(e => e.Tick >= since && e.Importance == Importance.Major && e.Category is not (LedgerCategory.Construction or LedgerCategory.Property))
            .Select(e => Trim(e.Text))
            .Distinct()
            .TakeLast(MaxMoments)
            .ToList();

        var hurt = new List<string>();
        foreach (var l in town.Legacies.Where(l => !l.Retired && l.Day >= sinceDay).TakeLast(2))
            hurt.Add($"{l.Name} fell on day {l.Day}.");
        foreach (var p in town.Parties.Where(p => !p.Returned).OrderBy(p => p.ReturnTick).ThenBy(p => p.Id).Take(2))
            hurt.Add($"The {p.Name} are out at {Quests.Site(town, p.SiteId)?.Def.Name ?? "a site in the wilds"}, due home around day {SimTime.DayOf(p.ReturnTick)}.");
        foreach (var a in town.Heroes.Where(a => !a.IsAway && a.Hero!.Hp < a.Hero.MaxHp * 0.6).OrderBy(a => (double)a.Hero!.Hp / Math.Max(1, a.Hero.MaxHp)).ThenBy(a => a.Id).Take(2))
            hurt.Add($"{a.Name} is still nursing wounds ({a.Hero!.Hp} of {a.Hero.MaxHp} hit points).");
        if (hurt.Count > MaxLines) hurt.RemoveRange(MaxLines, hurt.Count - MaxLines);

        var leads = new List<string>();
        foreach (var c in Captives.Held(town).OrderBy(c => c.DueTick).Take(1))
        {
            int days = Math.Max(0, (int)Math.Ceiling((c.DueTick - town.Tick) / (double)SimTime.HoursPerDay));
            leads.Add($"{c.Captives.Count} townsfolk are held at {Quests.Site(town, c.SiteId)?.Def.Name ?? "a lair"}, {days} {(days == 1 ? "day" : "days")} left to bring them home.");
        }
        if (town.Enemy.Stage == ThreatStage.Marching && town.Enemy.ArrivalTick is int at)
            leads.Add($"The warband is marching on the town and arrives around day {SimTime.DayOf(at)}.");
        foreach (var c in Crises.Active(town).OrderBy(c => c.Id).Take(1))
            if (town.Saga.Menaces.FirstOrDefault(m => m.Id == c.MenaceId) is { } m)
                leads.Add($"{m.Name} is brewing trouble: the next blow could fall around day {c.NextBlowDay}.");
        int sendings = Stories.Pending(town).Count(s => Sendings.Is(town, s));
        if (sendings > 0) leads.Add(sendings == 1 ? "A party is waiting on the sending stone for your word." : $"{sendings} parties are waiting on the sending stone for your word.");
        var open = town.Bounties.Where(b => b.Status == BountyStatus.Open).ToList();
        if (open.Count > 0)
        {
            var best = open.OrderByDescending(b => b.Reward).ThenBy(b => b.Id).First();
            leads.Add($"{(open.Count == 1 ? "A bounty waits" : $"{open.Count} bounties wait")} on the board, the richest {Js.Str(Js.Round(best.Reward))} gold for {Quests.Site(town, best.SiteId)?.Def.Name ?? "a site"}.");
        }
        int pleas = town.Pleas.Count(p => p.Status == PleaStatus.Open);
        if (pleas > 0) leads.Add(pleas == 1 ? "A neighbouring village is still begging for help." : $"{pleas} neighbouring villages are still begging for help.");
        if (leads.Count > MaxLines) leads.RemoveRange(MaxLines, leads.Count - MaxLines);

        return new RecapView($"Last time in {town.Name}...", moments, hurt, leads);
    }

    /// <summary>A Ledger line without its pointer into the Chronicles.</summary>
    private static string Trim(string text)
    {
        int i = text.LastIndexOf(" (Chronicle ", StringComparison.Ordinal);
        return i > 0 && text.EndsWith(')') ? text[..i] : text;
    }
}
