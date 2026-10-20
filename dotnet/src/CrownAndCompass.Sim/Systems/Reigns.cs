namespace CrownAndCompass.Sim.Systems;

/// <summary>A ruler's temperament: how they treat the town (relations, caravans, relief, pleas) and how hard they are to cow.</summary>
public sealed record TemperamentDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>False for a temperament no people chooses (a menace's puppet).</summary>
    public bool Chosen { get; init; } = true;

    /// <summary>Where the people's feelings for the town settle, above or below where their kind starts.</summary>
    public double Standing { get; init; }

    /// <summary>A caravan's load there, the relief they send, and the reward their pleas offer, times these.</summary>
    public double Trade { get; init; } = 1;
    public double Relief { get; init; } = 1;
    public double Plea { get; init; } = 1;

    /// <summary>How hard an enemy's shadow is to lay over them: its spread is divided by this.</summary>
    public double Resolve { get; init; } = 1;
    public string Text { get; init; } = "";
}

/// <summary>How the rulers of the wilds age, die, fall and are replaced (neighbours.json "reigns").</summary>
public sealed record ReignRules
{
    /// <summary>A new ruler's age, in years.</summary>
    public int[] Age { get; init; } = [26, 70];
    public int YearDays { get; init; } = 120;

    /// <summary>The chance a ruler dies in a year, and how much it climbs each year past DeathFrom.</summary>
    public double DeathPerYear { get; init; } = 0.03;
    public int DeathFrom { get; init; } = 55;
    public double DeathPerYearOver { get; init; } = 0.015;

    /// <summary>Past this age a death is old age; before it, illness.</summary>
    public int OldAge { get; init; } = 64;
    public int CheckEveryDays { get; init; } = 30;

    /// <summary>At each check: a people this poor may drive their ruler out; a shadowed one's ruler may meet the enemy's knife.</summary>
    public double OverthrowBelow { get; init; } = 20;
    public double OverthrowChance { get; init; } = 0.2;
    public double KnifeChance { get; init; } = 0.08;

    /// <summary>A menace falling on them may kill their ruler; so may their band being broken.</summary>
    public double BattleChance { get; init; } = 0.3;
    public double BrokenChance { get; init; } = 0.25;

    /// <summary>The share of the difference between the old and new ruler's temperament that moves relations at once.</summary>
    public double Shift { get; init; } = 0.5;
    public IReadOnlyList<string> Messengers { get; init; } = [];
    public string News { get; init; } = "";
    public string RouteNews { get; init; } = "";
    public string ContactNews { get; init; } = "";

    /// <summary>How the old ruler went, by cause: age, illness, knife, battle, broken, overthrown, stepdown, puppet, freed.</summary>
    public Dictionary<string, string> Deaths { get; init; } = [];
    public string Heir { get; init; } = "";
    public string Warmer { get; init; } = "";
    public string Colder { get; init; } = "";
    public string PuppetHeir { get; init; } = "";
}

/// <summary>A settlement's ruler as a person: their temperament, age and reign, and how the last one went.</summary>
public sealed class Reign
{
    public string Temperament { get; set; } = "";

    /// <summary>Their age when they took the seat, and the day they did.</summary>
    public int Age { get; set; }
    public int SinceDay { get; set; }

    /// <summary>The day their years run out, and how (age or illness), rolled when they take the seat.</summary>
    public int EndDay { get; set; }
    public string EndCause { get; set; } = "age";
    public int NextCheckDay { get; set; }

    /// <summary>Rulers the settlement has had since the town first looked at it.</summary>
    public int Count { get; set; } = 1;
    public string Before { get; set; } = "";
    public string BeforeFate { get; set; } = "";

    /// <summary>The menace whose creature rules, for a puppet.</summary>
    public int? PuppetOf { get; set; }
}

/// <summary>
/// The rulers of the wilds. Each settlement's leader has a temperament (generous, shrewd, wary, proud,
/// devout, timid) that colours how the town is treated: where relations settle, how much its caravans
/// carry, the relief it sends and what its pleas pay. Rulers age and die (old age, illness), fall to an
/// enemy's knife or in a lost fight, or are driven out by a people grown poor, and an heir with another
/// temperament takes the seat, which shifts relations. A menace whose shadow lies deep enough on a people
/// puts its own creature in the seat (<see cref="Shadows"/>). The town learns of it only as news someone
/// brings: drovers on its trade route, a contact there, or a passing herald.
/// </summary>
public static class Reigns
{
    private static ReignRules Rules(Town town) => town.Content.Neighbours.Reigns;

    private static readonly TemperamentDef Plain = new() { Id = "", Name = "steady" };

    public static TemperamentDef TemperOf(Town town, Settlement s) =>
        s.Reign is { } g ? town.Content.Neighbours.Temperaments.FirstOrDefault(t => t.Id == g.Temperament) ?? Plain : Plain;

    /// <summary>The ruler's age now, in years.</summary>
    public static int AgeOf(Town town, Settlement s) => s.Reign is { } g ? g.Age + Math.Max(0, town.Day - g.SinceDay) / Math.Max(1, Rules(town).YearDays) : 0;

    /// <summary>Whole years on the seat.</summary>
    public static int YearsOf(Town town, Settlement s) => s.Reign is { } g ? Math.Max(0, town.Day - g.SinceDay) / Math.Max(1, Rules(town).YearDays) : 0;

    public static bool Puppet(Settlement s) => s.Reign?.PuppetOf != null;

    public static double Trade(Town town, Settlement s) => TemperOf(town, s).Trade;
    public static double Relief(Town town, Settlement s) => TemperOf(town, s).Relief;
    public static double Plea(Town town, Settlement s) => TemperOf(town, s).Plea;
    public static double Resolve(Town town, Settlement s) => TemperOf(town, s).Resolve;
    public static double Standing(Town town, Settlement s) => TemperOf(town, s).Standing;

    // ---- The daily tick (from Neighbours, for each standing settlement) ----

    internal static void Daily(Town town, Settlement s)
    {
        var r = Rules(town);
        if (s.Reign == null)
        {
            Crown(town, s);
            return;
        }
        var g = s.Reign;
        if (town.Day >= g.EndDay)
        {
            Fall(town, s, g.PuppetOf != null ? "illness" : g.EndCause, null);
            return;
        }
        if (town.Day < g.NextCheckDay) return;
        g.NextCheckDay = town.Day + r.CheckEveryDays;
        // A puppet falls when the shadow that set it there lifts, not to its own people.
        if (g.PuppetOf != null) return;
        // A people gives a new ruler a year before blaming them for hard times.
        if (s.Prosperity < r.OverthrowBelow && YearsOf(town, s) >= 1 && town.Rng.Chance(r.OverthrowChance))
        {
            Fall(town, s, "overthrown", null);
            return;
        }
        if (Shadows.Shadowed(s) && Shadows.Over(town, s) is { Alive: true } m && town.Rng.Chance(r.KnifeChance)) Fall(town, s, "knife", m);
    }

    /// <summary>The ruler the town finds there: from the settlement's own stream, so the town's dice don't move.</summary>
    private static void Crown(Town town, Settlement s)
    {
        var r = Rules(town);
        var rng = new Rng(town.Seed * 31 + s.Id * 977 + 50 * 7919 + 17);
        int age = rng.Range(r.Age[0], r.Age[1] + 1);
        int years = rng.Range(0, Math.Max(1, Math.Min(20, age - r.Age[0]) + 1));
        var g = NewReign(town, rng, "", age);
        g.Age = age - years;
        g.SinceDay = town.Day - years * r.YearDays;
        s.Reign = g;
    }

    private static Reign NewReign(Town town, Rng rng, string old, int age)
    {
        var r = Rules(town);
        var pool = town.Content.Neighbours.Temperaments.Where(t => t.Chosen && t.Id != old).ToList();
        string temper = pool.Count > 0 ? pool[rng.Range(0, pool.Count)].Id : "";
        var g = new Reign { Temperament = temper, Age = age, SinceDay = town.Day, NextCheckDay = town.Day + r.CheckEveryDays };
        for (int y = 0; y < 80; y++)
        {
            int a = age + y;
            if (!rng.Chance(r.DeathPerYear + Math.Max(0, a - r.DeathFrom) * r.DeathPerYearOver)) continue;
            g.EndDay = town.Day + y * r.YearDays + rng.Range(0, r.YearDays);
            g.EndCause = a >= r.OldAge ? "age" : "illness";
            return g;
        }
        g.EndDay = town.Day + 80 * r.YearDays;
        return g;
    }

    /// <summary>A menace fell on them: their ruler may have fallen in the fighting.</summary>
    internal static void Struck(Town town, Settlement s, Menace m)
    {
        if (s.Reign != null && !Puppet(s) && town.Rng.Chance(Rules(town).BattleChance)) Fall(town, s, "battle", m);
    }

    /// <summary>Their band was broken: their ruler may have gone down with it.</summary>
    internal static void Broken(Town town, Settlement s)
    {
        if (s.Reign != null && !Puppet(s) && town.Rng.Chance(Rules(town).BrokenChance)) Fall(town, s, "broken", null);
    }

    /// <summary>
    /// The ruler is gone (by a cause in reigns "deaths") and another takes the seat: an heir of another
    /// temperament, or for "puppet" a creature of the menace. Relations shift with the new temperament.
    /// </summary>
    internal static void Fall(Town town, Settlement s, string cause, Menace? m, string trouble = "")
    {
        var r = Rules(town);
        var nb = town.Content.Neighbours;
        if (s.Reign == null) Crown(town, s);
        var g = s.Reign!;
        string old = s.Leader;
        int age = AgeOf(town, s);
        var before = TemperOf(town, s);
        bool puppet = cause == "puppet" && m != null;
        var menace = (cause == "freed" && g.PuppetOf is int was ? town.Saga.Menaces.FirstOrDefault(x => x.Id == was) : null) ?? m ?? (g.PuppetOf is int pid ? town.Saga.Menaces.FirstOrDefault(x => x.Id == pid) : null) ?? Shadows.Caster(town, s);

        string name = Heir(town, s, old);
        var next = NewReign(town, town.Rng, g.Temperament, town.Rng.Range(r.Age[0], r.Age[1] + 1));
        if (puppet)
        {
            next.Temperament = nb.Temperaments.FirstOrDefault(t => !t.Chosen)?.Id ?? next.Temperament;
            next.PuppetOf = m!.Id;
        }
        next.Count = g.Count + 1;
        next.Before = old;
        next.BeforeFate = cause;
        s.Reign = next;
        s.Leader = name;

        // The old leader no longer speaks for them; the new one starts from what the people think of the town.
        foreach (var c in Neighbours.CastOf(town, s).Where(c => c.Role == "leader").ToList()) c.Gone = true;
        town.Neighbours.Cast.Add(new Contact
        {
            Id = town.Neighbours.Cast.Count + 1, Name = name, Role = "leader", SettlementId = s.Id,
            Regard = puppet ? 0 : Math.Max(0, s.Standing + 10), MetDay = town.Day, NextFavourDay = town.Day + nb.Cast.FavourEveryDays[1],
        });

        var after = TemperOf(town, s);
        double delta = (after.Standing - before.Standing) * r.Shift;
        if (puppet)
        {
            s.Allied = false;
            s.Standing = Math.Min(s.Standing, s.Standing + delta);
        }
        else if (delta != 0) World.Shift(town, s, delta);

        string menaceName = menace?.Name ?? "the enemy";
        string death = r.Deaths.GetValueOrDefault(cause, r.Deaths.GetValueOrDefault("illness", "{old} is gone."))
            .Replace("{old}", old).Replace("{age}", age.ToString()).Replace("{menace}", menaceName).Replace("{trouble}", trouble);
        string heir = puppet
            ? r.PuppetHeir
            : r.Heir.Replace("{temper}", $"{Agent.Article(after.Name)} {after.Name} sort, {after.Text}") +
              (delta > 0.5 ? " " + r.Warmer : delta < -0.5 ? " " + r.Colder : "");
        News(town, s, death, heir.Replace("{menace}", menaceName));
    }

    /// <summary>A name for the new ruler: one of their people's leaders' names not in use, or a name of their race.</summary>
    private static string Heir(Town town, Settlement s, string old)
    {
        var lead = Neighbours.LeaderOf(town, s);
        var free = lead.Names.Where(n => n != old && !town.Neighbours.Cast.Any(c => c.Name == n && !c.Gone) && !town.Settlements.Any(x => x.Leader == n)).ToList();
        return free.Count > 0 ? free[town.Rng.Range(0, free.Count)] : Neighbours.NewName(town, s);
    }

    /// <summary>The town hears of it only from someone who came from there: its own drovers, a friend there, or a passer-by.</summary>
    private static void News(Town town, Settlement s, string death, string heir)
    {
        var r = Rules(town);
        if (!town.Regions[s.RegionId].Known) return;
        string text;
        if (Systems.Trade.Open(town).Any(x => x.SettlementId == s.Id)) text = r.RouteNews;
        else if (Neighbours.CastOf(town, s).Where(c => c.Role != "leader" && c.Memories.Count > 0).OrderByDescending(c => c.Regard).ThenBy(c => c.Id).FirstOrDefault() is { } c)
            text = r.ContactNews.Replace("{contact}", c.Name);
        else text = r.News.Replace("{messenger}", r.Messengers.Count > 0 ? r.Messengers[town.Rng.Range(0, r.Messengers.Count)] : "a traveller");
        text = text.Replace("{death}", death).Replace("{heir}", heir).Replace("the the ", "the ");
        town.Log(LedgerCategory.Town, Importance.Notable, Saga.Sentences(Neighbours.Fill(town, s, text)));
    }

    /// <summary>"Hollis Tamm, a wary reeve of 54, six years on the seat": for the Diplomacy window.</summary>
    public static string Describe(Town town, Settlement s)
    {
        var t = TemperOf(town, s);
        string title = Neighbours.LeaderOf(town, s).Title;
        int years = YearsOf(town, s);
        string reign = years == 0 ? "new to the seat" : years == 1 ? "a year on the seat" : $"{years} years on the seat";
        return Puppet(s) ? $"{s.Leader}, {t.Name} {t.Text}" : $"{s.Leader}, {Agent.Article(t.Name)} {t.Name} {title} of {AgeOf(town, s)}, {reign}";
    }

    /// <summary>"Succeeded Edric Fenn, who died of old age", or empty for the first ruler the town has known.</summary>
    public static string Succession(Town town, Settlement s)
    {
        if (s.Reign is not { Before: not "" } g) return "";
        string fate = g.BeforeFate switch
        {
            "age" => "who died of old age",
            "illness" => "who died of a fever",
            "knife" => "who was murdered",
            "battle" => "who fell in the fighting",
            "broken" => "who died with their band",
            "overthrown" => "whom the people drove out",
            "stepdown" => "who stepped down",
            "puppet" => "whom the enemy put aside",
            "freed" => "the enemy's creature, thrown down",
            _ => "",
        };
        return $"Succeeded {g.Before}{(fate != "" ? ", " + fate : "")}";
    }
}
