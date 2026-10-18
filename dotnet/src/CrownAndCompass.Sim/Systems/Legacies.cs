namespace CrownAndCompass.Sim.Systems;

/// <summary>Numbers for hero legacies (content/game/legacies.json).</summary>
public sealed record LegacyRules
{
    /// <summary>Heroes who fall at this level or above are kept in the hall of heroes.</summary>
    public int MinLevel { get; init; } = 3;

    /// <summary>A fallen hero this seasoned may leave an heir: a child, a sibling or a squire who takes up their arms.</summary>
    public int HeirLevel { get; init; } = 4;

    public double HeirChance { get; init; } = 0.3;
    public double HeirChancePerRenown { get; init; } = 0.01;
    public double HeirChanceMax { get; init; } = 0.75;

    /// <summary>Days after the death that the heir comes, low and high.</summary>
    public int[] HeirDays { get; init; } = [20, 60];

    /// <summary>The heir's level as a share of the fallen hero's (at least 1).</summary>
    public double HeirLevelShare { get; init; } = 0.5;

    /// <summary>Loyalty the heir starts with on top of the usual: they come because of what the town meant to the fallen.</summary>
    public double HeirLoyalty { get; init; } = 15;

    /// <summary>What a memorial costs: base plus so much per level of the hero it honours.</summary>
    public double MemorialCost { get; init; } = 80;
    public double MemorialCostPerLevel { get; init; } = 40;

    /// <summary>Loyalty every hero in town feels for each memorial the town has raised, up to the cap.</summary>
    public double MemorialLoyalty { get; init; } = 1.5;
    public double MemorialLoyaltyMax { get; init; } = 6;

    /// <summary>Mandate when a memorial is raised.</summary>
    public double MemorialMandate { get; init; } = 2;

    /// <summary>Old heroes who retire stay on as masters of arms; each gives new heroes this share of the XP to their next level, up to the cap.</summary>
    public double MentorXp { get; init; } = 0.25;
    public double MentorXpMax { get; init; } = 0.75;

    /// <summary>Heroes this seasoned may be asked about retiring (the "veteran" need of a story).</summary>
    public int RetireLevel { get; init; } = 8;
    public int RetireExpeditions { get; init; } = 8;

    /// <summary>The stand-in mayor raises a memorial when the treasury holds this many times its cost, plus the reserve.</summary>
    public double StandInTimes { get; init; } = 4;
    public double StandInReserve { get; init; } = 1500;

    /// <summary>Days the stand-in mayor leaves a death before raising a memorial (the town's own stories come first).</summary>
    public int StandInAfterDays { get; init; } = 30;

    /// <summary>The chance a memorial brings word to a fallen hero's kin, when none came at the death.</summary>
    public double MemorialHeirChance { get; init; } = 0.3;
}

/// <summary>One hero the town remembers: one who fell in its service or hung up their sword.</summary>
public sealed class Legacy
{
    public int HeroId { get; set; }
    public string Name { get; set; } = "";
    public string Class { get; set; } = "";
    public int Level { get; set; }
    public int Renown { get; set; }
    public int Kills { get; set; }
    public int Expeditions { get; set; }
    public int Day { get; set; }

    /// <summary>Retired as a master of arms rather than fallen.</summary>
    public bool Retired { get; set; }

    /// <summary>The town raised a memorial to them.</summary>
    public bool Memorial { get; set; }

    /// <summary>The day their heir is due, while one is on the way.</summary>
    public int? HeirDay { get; set; }

    public int? HeirId { get; set; }

    /// <summary>The magic item the heir carries in their name.</summary>
    public string Relic { get; set; } = "";
}

/// <summary>
/// Hero legacies: what a hero leaves behind. Heroes who fall in the town's service go into the hall of
/// heroes; the seasoned may leave an heir who comes to take up their arms (and their best magic item);
/// the council can raise memorials, which steady every hero's loyalty; and old heroes a story lets retire
/// stay on as masters of arms, so the heroes who come after arrive better trained.
/// </summary>
public static class Legacies
{
    private static LegacyRules Rules(Town town) => town.Content.Legacies;

    public static Legacy? Of(Town town, int heroId) => town.Legacies.FirstOrDefault(l => l.HeroId == heroId);

    public static IEnumerable<Legacy> Mentors(Town town) => town.Legacies.Where(l => l.Retired);

    public static double MemorialCost(Town town, Legacy l) => Rules(town).MemorialCost + Rules(town).MemorialCostPerLevel * l.Level;

    /// <summary>Loyalty every hero feels for the memorials the town has raised.</summary>
    public static double Loyalty(Town town)
    {
        var r = Rules(town);
        int n = town.Legacies.Count(l => l.Memorial);
        return n == 0 ? 0 : Math.Min(r.MemorialLoyaltyMax, n * r.MemorialLoyalty);
    }

    /// <summary>XP a new hero of this level arrives with, from the masters of arms who train them.</summary>
    public static int MentorXp(Town town, int level)
    {
        var r = Rules(town);
        int n = Mentors(town).Count();
        if (n == 0) return 0;
        return (int)Math.Floor(Math.Min(r.MentorXpMax, n * r.MentorXp) * Quests.XpToNext(level));
    }

    /// <summary>A seasoned hero who could be asked to retire.</summary>
    public static bool Veteran(Town town, Agent a) =>
        a.Hero is { } s && !a.IsAway && !s.Champion && s.Level >= Rules(town).RetireLevel && s.Expeditions >= Rules(town).RetireExpeditions;

    /// <summary>The hero hangs up their sword and stays on as a master of arms. Returns a note for the Ledger.</summary>
    public static string Retire(Town town, Agent a)
    {
        var s = a.Hero!;
        Callings.End(town, a, "");
        Systems.Loyalty.Strip(a);
        Population.Depart(town, a);
        var l = Remember(town, a);
        l.Retired = true;
        return $"{a.GivenName} retires to teach";
    }

    /// <summary>Raise a memorial to a fallen hero. Word of it may reach a seasoned hero's kin, when none came at the death.</summary>
    public static bool RaiseMemorial(Town town, int heroId)
    {
        var l = Of(town, heroId);
        if (l == null || l.Memorial) return false;
        double cost = MemorialCost(town, l);
        if (town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Legacies, -cost);
        Honor(town, l);
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{town.Name} raises a memorial to {l.Name}, {(l.Retired ? "master of arms" : "who fell in its service")}, for {Js.Str(Js.Round(cost))} gold. Heroes stop to read the name on their way out of the gate.", [l.HeroId]);
        return true;
    }

    /// <summary>A memorial goes up, whoever pays for it.</summary>
    internal static void Honor(Town town, Legacy l)
    {
        var r = Rules(town);
        l.Memorial = true;
        town.Mandate = Math.Clamp(town.Mandate + r.MemorialMandate, 0, 100);
        if (!l.Retired && l.HeirDay == null && l.HeirId == null && l.Level >= r.HeirLevel && town.Rng.Chance(r.MemorialHeirChance))
            l.HeirDay = town.Day + town.Rng.Range(r.HeirDays[0], r.HeirDays[1] + 1);
    }

    /// <summary>Daily: the fallen enter the hall, heirs arrive, and the stand-in mayor raises memorials when there is gold to spare.</summary>
    public static void Daily(Town town, bool auto)
    {
        var r = Rules(town);
        foreach (var a in town.Agents)
        {
            if (a.Hero is not { } s) continue;
            var l = Of(town, a.Id);
            if (l != null)
            {
                // Raised from the dead: they are no longer in the hall.
                if (!a.Dead && !a.Departed && !l.Retired) town.Legacies.Remove(l);
                continue;
            }
            if (!a.Dead || s.Level < r.MinLevel || s.TurnedTo != null) continue;
            var fallen = Remember(town, a);
            if (s.Level >= r.HeirLevel && town.Rng.Chance(Math.Min(r.HeirChanceMax, r.HeirChance + s.Renown * r.HeirChancePerRenown)))
                fallen.HeirDay = town.Day + town.Rng.Range(r.HeirDays[0], r.HeirDays[1] + 1);
        }
        foreach (var l in town.Legacies.Where(l => l.HeirDay is int d && d <= town.Day).ToList())
        {
            if (town.Heroes.Count >= City.HeroCap(town))
            {
                l.HeirDay = town.Day + 10;
                continue;
            }
            Heir(town, l);
        }
        if (auto) StandIn(town);
    }

    private static Legacy Remember(Town town, Agent a)
    {
        var s = a.Hero!;
        var l = new Legacy
        {
            HeroId = a.Id,
            Name = a.Name,
            Class = s.Cls.Name,
            Level = s.Level,
            Renown = s.Renown,
            Kills = s.Kills,
            Expeditions = s.Expeditions,
            Day = town.Day,
        };
        town.Legacies.Add(l);
        return l;
    }

    /// <summary>The heir comes: of the fallen's family and class, with their best magic item.</summary>
    private static void Heir(Town town, Legacy l)
    {
        var r = Rules(town);
        var fallen = town.Agent(l.HeroId);
        string? race = town.Content.Races.FirstOrDefault(x => x.Name == fallen.Race)?.Id;
        int level = Math.Max(1, (int)Math.Floor(l.Level * r.HeirLevelShare));
        var heir = Quests.CreateHero(town, fallen.Hero!.Cls, level, race);
        heir.FamilyName = fallen.FamilyName;
        heir.Age = town.Rng.Range(17, 25);
        var hs = heir.Hero!;
        hs.Loyalty = Math.Min(100, hs.Loyalty + r.HeirLoyalty);
        var relic = Gear.Pass(town, fallen, heir);
        l.Relic = relic?.Name ?? "";
        l.HeirId = heir.Id;
        l.HeirDay = null;
        string kin = town.Rng.Pick(["child", "younger kin", "sister", "brother"]);
        hs.Past = $"The {kin} of {l.Name}, the {l.Class.ToLowerInvariant()} who fell in {town.Name}'s service.";
        town.Log(LedgerCategory.Arrival, Importance.Notable,
            $"{heir.Describe()}, the {kin} of {l.Name}, comes to {town.Name} to take up the fallen {l.Class.ToLowerInvariant()}'s arms" +
            (relic != null ? $", the {relic.Name} at their side." : ".") + $" ({heir.TraitList()})", [heir.Id, l.HeroId]);
    }

    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var l = town.Legacies.Where(x => !x.Memorial && town.Day - x.Day >= r.StandInAfterDays).OrderByDescending(x => x.Level).ThenBy(x => x.HeroId).FirstOrDefault();
        if (l == null) return;
        double cost = MemorialCost(town, l);
        if (town.Treasury >= cost * r.StandInTimes + r.StandInReserve) RaiseMemorial(town, l.HeroId);
    }
}
