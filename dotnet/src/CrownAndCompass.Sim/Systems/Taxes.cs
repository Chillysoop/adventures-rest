namespace CrownAndCompass.Sim.Systems;

/// <summary>The tax rate the mayor sets, and the unrest of a town whose services cost more than it takes in (content/game/taxes.json).</summary>
public sealed record TaxRules
{
    /// <summary>The lowest and highest rate the mayor may set, and the step of the client's buttons.</summary>
    public double Min { get; init; } = 0.04;
    public double Max { get; init; } = 0.25;
    public double Step { get; init; } = 0.01;

    /// <summary>Mood every resident gains (below the usual rate) or loses (above it) per point of tax.</summary>
    public double MoodPerPoint { get; init; } = 0.6;

    /// <summary>Days of books the town weighs its services against its income over.</summary>
    public int Days { get; init; } = 7;

    /// <summary>Accounts that are the town's steady income: taxes, rents, trade, wonders. Hauls and windfalls aren't.</summary>
    public IReadOnlyList<string> Income { get; init; } = [];

    /// <summary>Accounts that are the town's services: upkeep, the watch, the institutions' patronage.</summary>
    public IReadOnlyList<string> Services { get; init; } = [];

    /// <summary>Unrest the town settles toward per share its services are short (0.4 short is Scale * 0.4), and how fast it climbs and eases a day.</summary>
    public double Scale { get; init; } = 250;
    public double Rise { get; init; } = 3;
    public double Ease { get; init; } = 2;

    /// <summary>Services are only weighed once they cost this much a day: a hamlet's hall is not a grievance.</summary>
    public double Grace { get; init; } = 40;

    /// <summary>At full unrest: mood lost by every resident, Mandate lost a day, settlers' attraction lost.</summary>
    public double Mood { get; init; } = 20;
    public double Mandate { get; init; } = 1;
    public double Attraction { get; init; } = 0.4;

    /// <summary>From this unrest, a share of workers down tools each day, up to <see cref="StrikeShare"/> at full unrest.</summary>
    public double StrikeAt { get; init; } = 60;
    public double StrikeShare { get; init; } = 0.3;

    /// <summary>Unrest at which the town grumbles, protests and strikes, for the Ledger.</summary>
    public IReadOnlyList<double> Stages { get; init; } = [25, 50, 75];
}

public sealed class TaxState
{
    /// <summary>Steady income and services' cost on each recent day, oldest first.</summary>
    public List<double> Income { get; } = [];
    public List<double> Services { get; } = [];

    /// <summary>Upkeep the treasury couldn't pay today: services that went without still cost the town.</summary>
    public double Unpaid { get; set; }

    /// <summary>0 to 100: how angry the town is that its services go short.</summary>
    public double Unrest { get; set; }

    /// <summary>The last stage of unrest the Ledger reported (0 = calm).</summary>
    public int Stage { get; set; }
}

/// <summary>
/// The mayor sets the tax rate. Every point above the usual rate sours the townsfolk a little and puts settlers
/// off; every point below pleases them. As the town grows its services (upkeep, the watch, the institutions)
/// cost more, and when they cost more than taxes, rents, trade and wonders bring in, the townsfolk grow restless:
/// unrest lowers their mood and the mayor's Mandate, turns settlers away and, at its worst, sends workers out
/// of their shops to protest. Raise taxes or find another income and it eases.
/// </summary>
public static class Taxes
{
    public static TaxRules Rules(Town town) => town.Content.Taxes;

    /// <summary>The player's lever: set the tax rate (a share, e.g. 0.12), within the town's limits.</summary>
    public static bool Set(Town town, double rate)
    {
        var r = Rules(town);
        rate = Math.Clamp(Js.Round(rate * 100) / 100, r.Min, r.Max);
        if (Math.Abs(rate - town.TaxRate) < 1e-9) return false;
        bool up = rate > town.TaxRate;
        town.TaxRate = rate;
        town.Log(LedgerCategory.Council, Importance.Routine, up
            ? $"The mayor raises the town's taxes to {Pct(rate)}. The criers read it out in the square to a sour crowd."
            : $"The mayor lowers the town's taxes to {Pct(rate)}. The taverns drink to it.");
        return true;
    }

    private static string Pct(double r) => $"{Js.Str(Js.Round(r * 100))}%";

    /// <summary>Steady income a day over the recent books.</summary>
    public static double Income(Town town) => Average(town.Taxes.Income);

    /// <summary>What the town's services cost a day over the recent books.</summary>
    public static double Services(Town town) => Average(town.Taxes.Services);

    private static double Average(List<double> days) => days.Count > 0 ? Js.Sum(days, v => v) / days.Count : 0;

    /// <summary>Income over services' cost: below 1 the services go short. Infinite while services cost next to nothing.</summary>
    public static double Funding(Town town)
    {
        double s = Services(town);
        return s < Rules(town).Grace ? double.PositiveInfinity : Income(town) / s;
    }

    /// <summary>Mood every resident feels from the tax rate and the unrest.</summary>
    public static double Mood(Town town)
    {
        var r = Rules(town);
        double burden = (town.Content.Rules.TaxRate - town.TaxRate) * 100 * r.MoodPerPoint;
        return burden - r.Mood * town.Taxes.Unrest / 100;
    }

    /// <summary>Settlers' attraction lost to unrest.</summary>
    public static double Attraction(Town town) => -Rules(town).Attraction * town.Taxes.Unrest / 100;

    /// <summary>Share of workers out protesting today.</summary>
    public static double StrikeShare(Town town)
    {
        var r = Rules(town);
        double u = town.Taxes.Unrest;
        return u < r.StrikeAt ? 0 : r.StrikeShare * (u - r.StrikeAt) / (100 - r.StrikeAt);
    }

    /// <summary>Whether this worker has downed tools today; the same people walk out all day, a different few each day.</summary>
    public static bool Striking(Town town, Agent a)
    {
        double share = StrikeShare(town);
        return share > 0 && ((a.Id * 37 + town.Day * 11) % 100) < share * 100;
    }

    /// <summary>End of day: weigh the day's books, move unrest toward what the town's services deserve, and let it bite.</summary>
    public static void Close(Town town)
    {
        var r = Rules(town);
        var s = town.Taxes;
        double income = 0, services = 0;
        foreach (string acc in r.Income) income += Math.Max(0, town.AccountsToday.Get(acc, 0));
        foreach (string acc in r.Services) services += Math.Max(0, -town.AccountsToday.Get(acc, 0));
        services += s.Unpaid;
        s.Unpaid = 0;
        s.Income.Add(Js.Round(income));
        s.Services.Add(Js.Round(services));
        if (s.Income.Count > r.Days) s.Income.RemoveAt(0);
        if (s.Services.Count > r.Days) s.Services.RemoveAt(0);

        double funding = Funding(town);
        double target = funding >= 1 ? 0 : Math.Min(100, (1 - funding) * r.Scale);
        s.Unrest = s.Unrest < target ? Math.Min(target, s.Unrest + r.Rise) : Math.Max(target, s.Unrest - r.Ease);
        if (s.Unrest > 0) town.Mandate = Math.Clamp(town.Mandate - r.Mandate * s.Unrest / 100, 0, 100);
        Report(town, funding);
    }

    private static void Report(Town town, double funding)
    {
        var r = Rules(town);
        var s = town.Taxes;
        int stage = 0;
        for (int i = 0; i < r.Stages.Count; i++) if (s.Unrest >= r.Stages[i]) stage = i + 1;
        // Climbing reports each new stage; easing only reports once the town is calm again.
        if (stage > s.Stage)
        {
            string shortfall = $"The town's services cost {Js.Str(Js.Round(Services(town)))}g a day and taxes, rents and trade bring in {Js.Str(Js.Round(Income(town)))}g.";
            town.Log(LedgerCategory.Hardship, stage >= 2 ? Importance.Major : Importance.Notable, stage switch
            {
                1 => $"Grumbling in the market: the wells go uncleaned, the lamps unlit, the roads unmended. {shortfall}",
                2 => $"A crowd gathers outside the town hall, banging pots, to protest the town's neglected streets and wells. {shortfall}",
                _ => $"Workers down tools across {town.Name} and march on the town hall: the town's services are falling apart. {shortfall}",
            });
            s.Stage = stage;
        }
        else if (stage == 0 && s.Stage > 0)
        {
            town.Log(LedgerCategory.Town, Importance.Notable, "The lamps are lit again and the wells run clean. The protests over the town's neglect die down.");
            s.Stage = 0;
        }
        else if (stage < s.Stage - 1) s.Stage = stage + 1;
    }
}
