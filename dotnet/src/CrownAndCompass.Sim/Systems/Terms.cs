namespace CrownAndCompass.Sim.Systems;

/// <summary>How a bounty is to be done: slay, drive off, talk down, rescue or fetch (content/game/bounties.json).</summary>
public sealed record TermDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>"drive the occupants out of", for "a bounty to drive the occupants out of the Goblin Warrens".</summary>
    public string Verb { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>The suggested reward, as a share of a slaying's.</summary>
    public double Pay { get; init; } = 1;

    /// <summary>What heroes make of the choice (<see cref="Regard"/>).</summary>
    public List<string> Tags { get; init; } = [];

    /// <summary>Share of a site's rooms the party fights through before the job is done (0: all of them and the boss).</summary>
    public double Rooms { get; init; }

    /// <summary>Share of the hoard's gold they bring back when the job ends short of the heart of the place.</summary>
    public double Loot { get; init; }

    /// <summary>How soon something moves back in, as a share of the usual wait.</summary>
    public double Resettle { get; init; } = 1;

    /// <summary>Talking down: the boss's Intelligence it needs, and the DC of the party's best voice.</summary>
    public int MinInt { get; init; }
    public int Dc { get; init; }
    public int DcPerTier { get; init; }

    /// <summary>Only where townsfolk are held captive.</summary>
    public bool Captives { get; init; }

    /// <summary>The party brings back the best thing in the hoard for certain.</summary>
    public bool Item { get; init; }
}

public sealed record CaptiveRules
{
    /// <summary>No captives are taken before this day; a raid that gets through carries some off at this chance.</summary>
    public int FirstDay { get; init; } = 25;
    public double Chance { get; init; } = 0.6;
    public int Min { get; init; } = 1;
    public int Max { get; init; } = 3;

    /// <summary>Days before the captives are lost, and days more for each tier of the place they are held.</summary>
    public int Days { get; init; } = 10;
    public int DaysPerTier { get; init; } = 2;
    public double FreedMandate { get; init; } = 5;
    public int FreedRenown { get; init; } = 2;
    public double LostMandate { get; init; } = 6;
    public double LostTension { get; init; } = 12;

    /// <summary>What the captors gain from captives they keep: thralls, ransom, recruits.</summary>
    public double LostStrength { get; init; } = 6;
}

public sealed record BountyRules
{
    /// <summary>A hero's interest in a bounty, more or less, when its terms suit their temper or go against it.</summary>
    public double Temper { get; init; } = 0.25;
    public List<TermDef> Terms { get; init; } = [];
    public CaptiveRules Captives { get; init; } = new();

    public TermDef Term(string id) => Terms.FirstOrDefault(t => t.Id == id) ?? Terms.FirstOrDefault() ?? new TermDef { Id = "slay", Name = "Slay", Verb = "clear" };
}

/// <summary>Townsfolk carried off in a raid, held at a site until rescued or lost.</summary>
public sealed class Captivity
{
    public int Id { get; set; }
    public string SiteId { get; set; } = "";
    public List<int> Captives { get; set; } = [];
    public int TakenTick { get; set; }
    public int DueTick { get; set; }
    public bool Done { get; set; }
    public bool Freed { get; set; }
}

/// <summary>
/// Bounty terms: the mayor says how a job is to be done, not only where. Slaying clears a site for good;
/// driving the occupants off is quicker and cheaper but they come back sooner; talking them down costs no
/// blood when it works; a rescue or a fetch goes in only as far as it must. Heroes favour terms that suit
/// their temper, and every hero judges the terms the mayor posts.
/// </summary>
public static class Terms
{
    private static BountyRules Rules(Town town) => town.Content.Bounties;

    public static TermDef Of(Town town, Bounty b) => Rules(town).Term(b.Terms);

    /// <summary>Why these terms can't be posted on this site, or null.</summary>
    public static string? Blocked(Town town, SiteState s, TermDef term)
    {
        if (term.MinInt > 0)
        {
            var boss = town.Content.Monster(s.Def.Boss.Monster);
            // The town only knows what it believes lives there; the bargain is made with the real occupant.
            if (boss.Int < term.MinInt) return $"what lives at {s.Def.Name} won't bargain";
        }
        if (term.Captives && Captives.At(town, s.Def.Id) == null) return "nobody is held captive there";
        return null;
    }

    /// <summary>The reward a bounty on these terms should carry, after the suggested reward for the believed tier.</summary>
    public static double Suggested(Town town, SiteState s, TermDef term) => Js.Round(Quests.SuggestedReward(Math.Max(1, s.BelievedTier)) * term.Pay);

    /// <summary>A hero's interest in the terms: more when they suit the hero's temper, less when they go against it.</summary>
    public static double Interest(Town town, Agent a, Bounty b)
    {
        double v = Regard.Moves(town, a, Of(town, b).Tags);
        return v > 0 ? Rules(town).Temper : v < 0 ? -Rules(town).Temper : 0;
    }
}

/// <summary>
/// Rescues against the clock. A raid that gets through can carry townsfolk off to the raiders' camp. They
/// are held there for a few days: a party that clears the place, or goes in on a rescue, brings them home;
/// if the days run out they are lost, and the town remembers it.
/// </summary>
public static class Captives
{
    private static CaptiveRules Rules(Town town) => town.Content.Bounties.Captives;

    public static Captivity? At(Town town, string siteId) => town.Captivities.FirstOrDefault(c => !c.Done && c.SiteId == siteId);

    public static IEnumerable<Captivity> Held(Town town) => town.Captivities.Where(c => !c.Done);

    /// <summary>After a raid gets through at <paramref name="target"/>: perhaps its workers are carried off to <paramref name="site"/>. Returns the line for the raid's Ledger entry.</summary>
    public static string Take(Town town, SiteState? site, Building? target)
    {
        var r = Rules(town);
        if (site == null || site.Cleared || target == null || town.Day < r.FirstDay || At(town, site.Def.Id) != null || !town.Rng.Chance(r.Chance)) return "";
        // Hands without property or a council seat: the ones working the outlying fields at night.
        var seated = town.Seats.Where(x => x.HolderId != null).Select(x => x.HolderId!.Value).ToHashSet();
        var owners = town.Buildings.Where(b => b.OwnerId != null).Select(b => b.OwnerId!.Value).ToHashSet();
        var pool = target.Workers.Select(town.Agent).Where(a => !a.Departed && a.Hero == null && !seated.Contains(a.Id) && !owners.Contains(a.Id)).OrderBy(a => a.Id).ToList();
        if (pool.Count == 0) return "";
        int n = Math.Min(pool.Count, town.Rng.Range(r.Min, r.Max + 1));
        var taken = new List<Agent>();
        for (int i = 0; i < n; i++)
        {
            var a = pool[town.Rng.Range(0, pool.Count)];
            pool.Remove(a);
            taken.Add(a);
        }
        foreach (var a in taken) Population.Depart(town, a);
        int days = r.Days + r.DaysPerTier * Math.Max(1, site.Def.Tier);
        town.Captivities.Add(new Captivity
        {
            Id = town.Captivities.Count + 1,
            SiteId = site.Def.Id,
            Captives = taken.Select(a => a.Id).ToList(),
            TakenTick = town.Tick,
            DueTick = town.Tick + days * SimTime.HoursPerDay,
        });
        if (site.Known == SiteKnowledge.Unheard) Rumors.Hear(town, site, Rumors.Occupants(town.Content, site.Def), site.Def.Tier, 60);
        return $". They drag {Names(taken)} off toward {site.Def.Name}; whoever goes after them has {days} days";
    }

    private static string Names(List<Agent> people) =>
        people.Count == 1 ? people[0].Name : $"{string.Join(", ", people.Take(people.Count - 1).Select(a => a.Name))} and {people[^1].Name}";

    /// <summary>Captives whose days have run out are lost.</summary>
    public static void Daily(Town town)
    {
        foreach (var c in Held(town).ToList())
        {
            var s = Quests.Site(town, c.SiteId);
            if (s == null || s.Cleared) Free(town, c, null);
            else if (town.Tick >= c.DueTick) Lose(town, c);
        }
    }

    /// <summary>The captives come home: by a party's hand, or because their captors are gone.</summary>
    public static void Free(Town town, Captivity c, Party? by)
    {
        var r = Rules(town);
        c.Done = true;
        c.Freed = true;
        var people = c.Captives.Select(town.Agent).Where(a => !a.Dead).ToList();
        foreach (var a in people)
        {
            // They come home with nothing: no bed, no job, until they find them again.
            a.Departed = false;
            a.Hunger = Math.Max(a.Hunger, 60);
        }
        town.Mandate = Math.Min(100, town.Mandate + r.FreedMandate);
        Stories.Remember(town, "captives-freed");
        if (by != null)
            foreach (var m in by.Members.Select(town.Agent).Where(m => !m.Departed && m.Hero != null))
                m.Hero!.Renown += r.FreedRenown;
        string site = Quests.Site(town, c.SiteId)?.Def.Name ?? "the raiders' camp";
        town.Log(LedgerCategory.Quest, Importance.Major,
            by != null
                ? $"The {by.Name} bring {Names(people)} home from {site}. The whole street turns out for them."
                : $"With {site} empty, {Names(people)} find their own way home.",
            people.Select(a => a.Id).ToList());
    }

    private static void Lose(Town town, Captivity c)
    {
        var r = Rules(town);
        c.Done = true;
        var people = c.Captives.Select(town.Agent).ToList();
        foreach (var a in people)
        {
            a.Dead = true;
            foreach (var other in town.Residents) if (other.OpinionOf(a.Id) >= 40) other.Loneliness = Math.Min(100, other.Loneliness + 30);
        }
        town.Mandate = Math.Max(0, town.Mandate - r.LostMandate);
        town.Tension += r.LostTension;
        Stories.Remember(town, "captives-lost");
        var s = Quests.Site(town, c.SiteId);
        if (s != null) s.Strength = Math.Min(town.Content.Lairs.MaxStrength, s.Strength + r.LostStrength * people.Count);
        town.Log(LedgerCategory.Hardship, Importance.Major,
            $"Nobody came for {Names(people)}. Word comes back from {s?.Def.Name ?? "the raiders' camp"} that they are gone, and their families wear grey.",
            people.Select(a => a.Id).ToList());
    }

    /// <summary>Stand-in mayor: a rescue bounty for every camp holding townsfolk, before anything else.</summary>
    public static void AutoPost(Town town)
    {
        var rescue = town.Content.Bounties.Terms.FirstOrDefault(t => t.Captives);
        if (rescue == null) return;
        foreach (var c in Held(town).OrderBy(c => c.Id))
        {
            if (town.Bounties.Any(b => b.SiteId == c.SiteId && Quests.IsActive(b))) continue;
            if (Quests.Site(town, c.SiteId) is not { } s) continue;
            double reward = Terms.Suggested(town, s, rescue);
            if (town.Treasury < reward + 50) continue;
            Quests.PostBounty(town, s.Def.Id, reward, null, rescue.Id);
        }
    }
}
