namespace CrownAndCompass.Sim.Systems;

/// <summary>How far back the top bar looks and when a trend counts as trouble (content/game/trends.json).</summary>
public sealed record TrendRules
{
    /// <summary>Days of books the top bar sums: a month, the length of a season.</summary>
    public int Days { get; init; } = 30;

    /// <summary>The treasury is in trouble when, at the month's rate, it runs dry within this many days.</summary>
    public double Runway { get; init; } = 60;

    /// <summary>...or once it holds less than this: the bills can't be paid.</summary>
    public double Floor { get; init; } = 50;

    /// <summary>Food is in trouble once the caravans bring in this share of what the town eats: its farms can't feed it.</summary>
    public double Imports { get; init; } = 0.25;

    /// <summary>Unrest and Mandate in trouble: unrest from this, or Mandate under this while it falls.</summary>
    public double Unrest { get; init; } = 25;
    public double Mandate { get; init; } = 40;

    /// <summary>The whole campaign's graphs (<see cref="Records"/>).</summary>
    public RecordRules Records { get; init; } = new();
}

/// <summary>One day's close of the town's key numbers, for the top bar's month.</summary>
public sealed class TrendDay
{
    public int Day { get; set; }
    public double Treasury { get; set; }
    public OrderedMap<string, double> Accounts { get; set; } = new();
    public int Households { get; set; }
    public int Homeless { get; set; }
    public double Mandate { get; set; }
    public double Unrest { get; set; }
    public double Food { get; set; }
    public double Eaten { get; set; }
    public double Imported { get; set; }
}

public sealed class TrendState
{
    /// <summary>The last month of days, oldest first.</summary>
    public List<TrendDay> Days { get; } = [];
}

/// <summary>
/// The month at a glance: each day closes with the treasury's books, the households, food, Mandate and unrest,
/// and the top bar reads where each is heading over the last month (30 days) and when the money or bread runs out.
/// It only watches: nothing here changes the town.
/// </summary>
public static class Trends
{
    public static TrendRules Rules(Town town) => town.Content.Trends;

    /// <summary>End of day, after the books close: keep the day and drop the one a month back.</summary>
    public static void Close(Town town)
    {
        var days = town.Trends.Days;
        days.Add(new TrendDay
        {
            Day = town.Day - 1,
            Treasury = town.Treasury,
            Accounts = Copy(town.AccountsToday),
            Households = town.Population,
            Homeless = town.Workers.Count(a => a.Homeless),
            Mandate = town.Mandate,
            Unrest = town.Taxes.Unrest,
            Food = town.Market.Stock(Good.Food),
            Eaten = town.Market.BoughtYesterday(Good.Food),
            Imported = town.Market.ImportedYesterday(Good.Food),
        });
        while (days.Count > Math.Max(1, Rules(town).Days)) days.RemoveAt(0);
    }

    private static OrderedMap<string, double> Copy(OrderedMap<string, double> from)
    {
        var to = new OrderedMap<string, double>();
        foreach (var (k, v) in from)
            if (Math.Abs(v) >= 0.005) to[k] = v;
        return to;
    }

    /// <summary>How many days the month has so far: the first month of a town has fewer.</summary>
    public static int Span(Town town) => town.Trends.Days.Count;

    /// <summary>A day's average of a number scaled up to the month, so a young town reads the same way.</summary>
    private static double Monthly(Town town, double total) => Span(town) == 0 ? 0 : total / Span(town) * Rules(town).Days;

    /// <summary>Gold in or out over the month, by account, biggest income first and biggest cost last.</summary>
    public static List<(string Account, double Amount)> Books(Town town)
    {
        var sums = new OrderedMap<string, double>();
        foreach (var d in town.Trends.Days)
            foreach (var (k, v) in d.Accounts) sums[k] = sums.Get(k, 0) + v;
        return sums.Select(kv => (kv.Key, Js.Round(Monthly(town, kv.Value))))
            .Where(x => x.Item2 != 0)
            .OrderByDescending(x => x.Item2).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>The treasury's net over the month.</summary>
    public static double Net(Town town) => Js.Round(Monthly(town, Js.Sum(town.Trends.Days, d => Js.Sum(d.Accounts.Values, v => v))));

    /// <summary>Days until the treasury is empty at the month's rate, or null while it holds or grows.</summary>
    public static double? Runway(Town town)
    {
        double perDay = Net(town) / Rules(town).Days;
        return perDay >= 0 ? null : Math.Max(0, Math.Floor(town.Treasury / -perDay));
    }

    /// <summary>How a number moved over the month: today's against the oldest day kept, scaled to a month.</summary>
    public static double Change(Town town, Func<TrendDay, double> of)
    {
        var days = town.Trends.Days;
        if (days.Count < 2) return 0;
        return (of(days[^1]) - of(days[0])) / (days.Count - 1) * Rules(town).Days;
    }

    /// <summary>The treasury day by day over the month, for the hover card's line.</summary>
    public static List<double> Treasury(Town town) => town.Trends.Days.Select(d => d.Treasury).ToList();

    /// <summary>What the townsfolk eat a day, over the last week kept.</summary>
    public static double Eaten(Town town)
    {
        var week = town.Trends.Days.TakeLast(7).ToList();
        return week.Count == 0 ? 0 : Js.Sum(week, d => d.Eaten) / week.Count;
    }

    /// <summary>Loads of bread the caravans brought in a day over the last week: the stores could not feed the town.</summary>
    public static double Imported(Town town)
    {
        var week = town.Trends.Days.TakeLast(7).ToList();
        return week.Count == 0 ? 0 : Js.Sum(week, d => d.Imported) / week.Count;
    }

    /// <summary>Days of bread in the market at the town's rate of eating; null before anyone has eaten.</summary>
    public static double? FoodDays(Town town)
    {
        double eaten = Eaten(town);
        return eaten < 0.5 ? null : Math.Floor(town.Market.Stock(Good.Food) / eaten);
    }

    public static bool MoneyTrouble(Town town) => town.Treasury < Rules(town).Floor || Runway(town) is double d && d < Rules(town).Runway;

    public static bool FoodTrouble(Town town) => Imported(town) >= Math.Max(1, Eaten(town) * Rules(town).Imports);

    public static bool UnrestTrouble(Town town) => town.Taxes.Unrest >= Rules(town).Unrest;

    public static bool MandateTrouble(Town town) => town.Mandate < Rules(town).Mandate && Change(town, d => d.Mandate) <= 0;
}
