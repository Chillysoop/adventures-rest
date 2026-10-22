namespace CrownAndCompass.Sim.Systems;

/// <summary>A step on one of the assessment ladders: where it starts and what the bar says.</summary>
public sealed record AssessBand
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double At { get; init; }
}

/// <summary>How one story effect weighs on a choice's reward or risk, and the icon it shows as.</summary>
public sealed record ImpactWeight
{
    public double Unit { get; init; } = 1;
    public int Sign { get; init; }
    public string Icon { get; init; } = "";
    public string Name { get; init; } = "";
}

/// <summary>The ladders the client draws as bars (content/game/assess.json).</summary>
public sealed record AssessRules
{
    public List<AssessBand> Odds { get; init; } = [];
    public List<AssessBand> Threat { get; init; } = [];
    public List<AssessBand> Danger { get; init; } = [];
    public List<AssessBand> Impact { get; init; } = [];
    public int PartyLevels { get; init; } = 3;
    public int PartyEdge { get; init; } = 2;

    /// <summary>Hero levels a party wants per foe that stands and fights a company of too little renown (<see cref="Notoriety.Swell(Town, string?, SiteState)"/>).</summary>
    public int RenownLevels { get; init; } = 2;

    /// <summary>The most points one effect adds to a reward or a risk, so one big number does not drown the rest.</summary>
    public double PerEffect { get; init; } = 3;

    /// <summary>By effect key; decree:, precedent:, calling:, fixture: and project: effects go by their prefix.</summary>
    public Dictionary<string, ImpactWeight> Effects { get; init; } = [];
}

/// <summary>Where a value falls on a ladder: its band, its step (0 for the lowest) and how many steps there are.</summary>
public readonly record struct Banded(AssessBand Band, int Step, int Steps)
{
    public string Id => Band.Id;
    public string Name => Band.Name;
}

/// <summary>What a choice could win and cost, as bands, and what it touches (icons, in the order the effects come).</summary>
public sealed record ImpactView(Banded Reward, Banded Risk, List<ImpactWeight> Touches);

/// <summary>
/// Reads the town for the player at a glance: odds, how dangerous an enemy or a site is, and what a choice puts
/// at stake, each as a band on a short ladder rather than a number, so the bars guide without giving the game
/// away. It reads the town and never writes it or rolls dice.
/// </summary>
public static class Assess
{
    public static AssessRules Rules(Town town) => town.Content.Assess;

    /// <summary>The band a value reaches on a ladder (the last whose start it reaches).</summary>
    public static Banded On(List<AssessBand> ladder, double value)
    {
        int step = 0;
        for (int i = 0; i < ladder.Count; i++)
            if (value >= ladder[i].At) step = i;
        return new Banded(ladder[step], step, ladder.Count);
    }

    /// <summary>How likely a check is to succeed.</summary>
    public static Banded Odds(Town town, double chance) => On(Rules(town).Odds, chance);

    /// <summary>A menace's strength against the town's defence, allies left out (they may not come).</summary>
    public static double ThreatRatio(Town town, Menace m) => m.Power / Math.Max(1, Systems.Threat.Defense(town, allies: false));

    /// <summary>How dangerous a menace is to the town now. The big bad is never less than serious.</summary>
    public static Banded Threat(Town town, Menace m)
    {
        var ladder = Rules(town).Threat;
        var b = On(ladder, ThreatRatio(town, m));
        int floor = Math.Min(2, ladder.Count - 1);
        return m.BigBad && b.Step < floor ? new Banded(ladder[floor], floor, ladder.Count) : b;
    }

    /// <summary>The warband's threat as the scouts guess it, or null while nobody has seen it.</summary>
    public static Banded? WarbandThreat(Town town)
    {
        if (!Systems.Threat.IsActive(town) || Systems.Threat.EstimatedStrength(town) is not double est) return null;
        return On(Rules(town).Threat, est / Math.Max(1, Systems.Threat.Defense(town, allies: false)));
    }

    /// <summary>
    /// How hard a site looks for the town's heroes, by what the town believes of it (a rumour can be wrong): the
    /// levels a party wants for its believed tier over the strongest four heroes on the books.
    /// </summary>
    public static Banded Danger(Town town, SiteState s) => DangerFor(town, s, town.Heroes.Where(h => !h.Departed && !h.Dead));

    /// <summary>
    /// How hard a site looks for these heroes (a company and whoever would ride with it): the same ladder, over their
    /// strongest four, and harder for a company whose renown falls short of the site's (more foes stand and fight).
    /// </summary>
    public static Banded DangerFor(Town town, SiteState s, IEnumerable<Agent> heroes, string? company = null)
    {
        var r = Rules(town);
        int have = heroes.Select(h => h.Hero!.Level).OrderByDescending(l => l).Take(4).Sum();
        int wants = Wants(town, s) + r.RenownLevels * Notoriety.Swell(town, company, s);
        return On(r.Danger, have == 0 ? double.MaxValue : (double)wants / have);
    }

    /// <summary>The hero levels a party wants for what the town believes is at a site.</summary>
    public static int Wants(Town town, SiteState s) => Rules(town).PartyLevels * Math.Max(1, s.BelievedTier) + Rules(town).PartyEdge;

    /// <summary>The weight of an effect key, by its prefix for decrees, precedents, callings, fixtures and projects.</summary>
    public static ImpactWeight? Weight(Town town, string key)
    {
        var fx = Rules(town).Effects;
        if (fx.TryGetValue(key, out var w)) return w;
        int colon = key.IndexOf(':');
        return colon > 0 && fx.TryGetValue(key[..colon], out var p) ? p : null;
    }

    /// <summary>Good (positive) and bad (positive) points an outcome's effects add up to.</summary>
    public static (double Good, double Bad) Points(Town town, Dictionary<string, double> effects)
    {
        double good = 0, bad = 0, cap = Rules(town).PerEffect;
        foreach (var (key, v) in effects.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (Weight(town, key) is not { Sign: not 0 } w || v == 0) continue;
            double pts = Math.Min(cap, Math.Abs(v) / Math.Max(1e-9, w.Unit));
            // Repealing a decree or dropping a precedent is neither here nor there; more of a bad thing is bad.
            if (v * w.Sign > 0) good += pts;
            else bad += pts;
        }
        return (good, bad);
    }

    /// <summary>
    /// What a story choice could win and what it could cost: the good of a success (or of a sure thing), and the bad
    /// of a failure plus any cost a success carries and the gold paid to try. A failure that is only "no gain" is
    /// no risk. The icons are everything the choice touches, either way, so the card names what is at stake without
    /// saying which way it goes.
    /// </summary>
    public static ImpactView Impact(Town town, StoryOption o)
    {
        var r = Rules(town);
        var (winGood, winBad) = Points(town, o.Success.Effects);
        var (_, loseBad) = o.Rolls ? Points(town, o.Failure.Effects) : (0, 0);
        double cost = o.Cost > 0 && Weight(town, "gold") is { } g ? Math.Min(r.PerEffect, o.Cost / g.Unit) : 0;
        var touches = new List<ImpactWeight>();
        var outcomes = o.Rolls ? new[] { o.Success, o.Failure } : [o.Success];
        foreach (var e in outcomes)
            foreach (var key in e.Effects.Keys.OrderBy(k => k, StringComparer.Ordinal))
                if (e.Effects[key] != 0 && Weight(town, key) is { } w && touches.All(x => x.Icon != w.Icon)) touches.Add(w);
        if (cost > 0 && Weight(town, "gold") is { } gw && touches.All(x => x.Icon != gw.Icon)) touches.Add(gw);
        return new ImpactView(On(r.Impact, winGood), On(r.Impact, loseBad + winBad + cost), touches);
    }
}
