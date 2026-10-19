namespace CrownAndCompass.Sim.Systems;

/// <summary>A standing order a story puts in force for a while: tolls lifted, a curfew, a levy, a guild's monopoly.</summary>
public record DecreeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>What it does, for the Mayor tab.</summary>
    public string Text { get; init; } = "";

    /// <summary>How long it lasts when a story enacts it without saying.</summary>
    public int Days { get; init; } = 60;

    /// <summary>Added to every townsperson's mood.</summary>
    public double Mood { get; init; }

    /// <summary>Added to the town's draw on settlers.</summary>
    public double Arrivals { get; init; }

    /// <summary>Added to build speed (-0.1 is a tenth slower).</summary>
    public double Build { get; init; }

    /// <summary>Gold to (or from) the treasury each day.</summary>
    public double Gold { get; init; }

    /// <summary>Added to the town's tension each day.</summary>
    public double Tension { get; init; }

    /// <summary>Multiplies what each good's makers produce.</summary>
    public Dictionary<string, double> Yields { get; init; } = [];
}

/// <summary>
/// Decrees: the lasting half of a hard choice. A petition granted or a dilemma settled can put a decree in
/// force for a span of days; while it stands it moves mood, settlers, build speed, yields, the treasury or
/// tension every day, and the Mayor tab lists it with the days it has left.
/// </summary>
public static class Decrees
{
    public static IEnumerable<DecreeDef> Active(Town town) =>
        town.Stories.Decrees.Keys.Select(id => town.Content.Stories.Decree(id)).OfType<DecreeDef>();

    public static bool InForce(Town town, string id) => town.Stories.Decrees.ContainsKey(id);

    public static int DaysLeft(Town town, string id) => Math.Max(0, town.Stories.Decrees.Get(id, town.Day) - town.Day);

    /// <summary>Decrees in force, the callings heroes hold and the town's discoveries: all move mood, settlers, build speed and yields.</summary>
    private static IEnumerable<DecreeDef> InEffect(Town town) => Active(town).Concat(Callings.Held(town)).Concat(Research.Known(town));

    public static double Mood(Town town) => Js.Sum(InEffect(town), d => d.Mood);

    public static double Arrivals(Town town) => Js.Sum(InEffect(town), d => d.Arrivals);

    public static double Build(Town town)
    {
        double f = 1;
        foreach (var d in InEffect(town)) f *= 1 + d.Build;
        return Math.Max(0.25, f);
    }

    public static double Yield(Town town, Good good)
    {
        if (town.Stories.Decrees.Count == 0 && town.Stories.Callings.Count == 0 && town.Research.Known.Count == 0) return 1;
        double f = 1;
        string key = good.ToString();
        foreach (var d in InEffect(town)) f *= d.Yields.TryGetValue(key, out double y) ? y : 1;
        return f;
    }

    /// <summary>Put a decree in force for days (0: its own span), or extend one already standing. Returns a note for the Ledger.</summary>
    public static string Enact(Town town, string id, int days)
    {
        var d = town.Content.Stories.Decree(id) ?? throw new InvalidDataException($"Unknown decree {id}");
        int until = town.Day + (days > 0 ? days : d.Days);
        town.Stories.Decrees[id] = Math.Max(until, town.Stories.Decrees.Get(id, 0));
        return $"{d.Name} for {Ui(town.Stories.Decrees[id] - town.Day)}";
    }

    public static void Repeal(Town town, string id) => town.Stories.Decrees.Remove(id);

    /// <summary>Daily: each standing decree pays or costs its gold and moves tension, and lapses when its days are up.</summary>
    public static void Daily(Town town)
    {
        var st = town.Stories;
        foreach (var id in st.Decrees.Keys.ToList())
        {
            var d = town.Content.Stories.Decree(id);
            if (d == null || town.Day >= st.Decrees[id])
            {
                st.Decrees.Remove(id);
                if (d != null) town.Log(LedgerCategory.Story, Importance.Routine, $"The decree of {Lower(d.Name)} lapses in {town.Name}.");
                continue;
            }
            if (d.Gold != 0) town.Book(TreasuryAccount.Decrees, d.Gold);
            if (d.Tension != 0) town.Tension = Math.Max(0, town.Tension + d.Tension);
        }
    }

    private static string Ui(int days) => days == 1 ? "1 day" : $"{days} days";

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
