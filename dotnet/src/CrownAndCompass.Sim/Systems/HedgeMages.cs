namespace CrownAndCompass.Sim.Systems;

/// <summary>One trade's spell, learned by a hedge mage who works it (research.json "hedge").</summary>
public sealed record HedgeTrade
{
    /// <summary>The workplace the spell belongs to.</summary>
    public string Building { get; init; } = "";

    /// <summary>"builder-mage": what the townsfolk call them.</summary>
    public string Name { get; init; } = "";

    /// <summary>"sets stone with a word".</summary>
    public string Spell { get; init; } = "";

    /// <summary>Share more work a day at their workplace, faster raising at building sites, stronger watch.</summary>
    public double Yield { get; init; }
    public double Build { get; init; }
    public double Watch { get; init; }
}

public sealed record HedgeRules
{
    /// <summary>The library's power before any townsperson learns a trade spell, and the days between awakenings.</summary>
    public double Power { get; init; } = 4;
    public int Every { get; init; } = 12;

    /// <summary>At most one hedge mage per this many residents, plus one per arcane discovery the town knows.</summary>
    public int PerPeople { get; init; } = 40;

    /// <summary>The Intelligence or Wisdom it takes to teach yourself a spell, and the lore each hedge mage adds a day.</summary>
    public int MinScore { get; init; } = 12;
    public double Lore { get; init; } = 0.3;
    public List<HedgeTrade> Trades { get; init; } = [];

    /// <summary>Ledger lines: {name}, {trade}, {workplace}, {spell}, {mage} ({a mage} with its article), {town}; the first for the town's first.</summary>
    public string Learned { get; init; } = "";
    public string LearnedMore { get; init; } = "";

    public HedgeTrade? Trade(string building) => Trades.FirstOrDefault(t => t.Building == building);
}

/// <summary>
/// Hedge mages, the bottom rung of the town's magic (below the working wizards of the library and the archmage of
/// the Mage Tower): once the library is strong enough, now and then the sharpest townsperson in a trade that has a
/// spell (builders, field hands, fishers, smiths, woodcutters, brewers, weavers, apothecaries, and the temple's
/// grave-speakers who help the watch) teaches themselves a little magic for their work. They are still townsfolk:
/// they keep their trade and make it better, add a little lore, and the movement resents those outside it, though some join it (<see cref="Union"/>).
/// No dice: who learns is the sharpest mind, ties by id.
/// </summary>
public static class HedgeMages
{
    public static HedgeRules Rules(Town town) => town.Content.Research.Hedge;

    public static List<Agent> All(Town town) => town.Agents.Where(a => a.HedgeSpell != "" && !a.Departed).OrderBy(a => a.Id).ToList();

    /// <summary>The spell a hedge mage works at their own workplace, or null.</summary>
    public static HedgeTrade? Working(Town town, Agent a) =>
        a.HedgeSpell != "" && a.JobId is int j && town.Building(j).Def.Id == a.HedgeSpell ? Rules(town).Trade(a.HedgeSpell) : null;

    /// <summary>How many hedge mages the town can hold now.</summary>
    public static int Cap(Town town) =>
        town.Population / Math.Max(1, Rules(town).PerPeople) + Research.Known(town).Count(d => d.Field == "arcana");

    /// <summary>Extra share of work a building's hedge mages put into its day (added to its craft check).</summary>
    public static double Yield(Town town, Building b)
    {
        if (b.Workers.Count == 0) return 0;
        double y = 0;
        foreach (int id in b.Workers)
            if (Working(town, town.Agent(id)) is { } t) y += t.Yield;
        return y;
    }

    /// <summary>The multiplier a builder-mage puts on the sites they raise.</summary>
    public static double Build(Town town, Agent a) => 1 + (Working(town, a)?.Build ?? 0);

    /// <summary>What the grave-speakers add to the watch's strength.</summary>
    public static double Watch(Town town) => Js.Sum(All(town), a => Working(town, a)?.Watch ?? 0);

    /// <summary>Daily: every few days, once the library is strong enough and the town has room, one more townsperson learns a trade spell.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Trades.Count == 0 || r.Every <= 0 || town.Day % r.Every != 0 || Research.Power(town) < r.Power) return;
        if (All(town).Count >= Cap(town)) return;
        Agent? pick = null;
        int best = r.MinScore - 1;
        foreach (var a in town.Residents)
        {
            if (a.Hero != null || a.HedgeSpell != "" || a.JobId is not int j || r.Trade(town.Building(j).Def.Id) == null) continue;
            // While the movement has hedge magic banned, only its own members may take it up.
            if (Union.Banned(town) && !Union.Joined(town, a, asMage: true)) continue;
            int mind = Math.Max(a.Abilities.Int, a.Abilities.Wis);
            if (mind > best) (pick, best) = (a, mind);
        }
        if (pick == null) return;
        var b = town.Building(pick.JobId!.Value);
        var t = r.Trade(b.Def.Id)!;
        bool first = All(town).All(x => x.HedgeSpell != t.Building);
        pick.HedgeSpell = t.Building;
        string line = (first ? r.Learned : r.LearnedMore)
            .Replace("{name}", pick.Name)
            .Replace("{trade}", Agent.Article(pick.Profession) + " " + pick.Profession)
            .Replace("{workplace}", b.Name)
            .Replace("{spell}", t.Spell)
            .Replace("{a mage}", Agent.Article(t.Name) + " " + t.Name)
            .Replace("{mage}", t.Name)
            .Replace("{town}", town.Name);
        town.Log(LedgerCategory.Social, Importance.Notable, Saga.Sentences(line), [pick.Id]);
    }

    /// <summary>Every hedge mage outside the movement puts their spell away (the movement had it banned).</summary>
    public static int Forget(Town town)
    {
        var all = All(town).Where(a => !Union.Joined(town, a)).ToList();
        foreach (var a in all) a.HedgeSpell = "";
        return all.Count;
    }
}
