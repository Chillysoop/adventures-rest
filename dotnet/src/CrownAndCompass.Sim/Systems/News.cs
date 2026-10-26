namespace CrownAndCompass.Sim.Systems;

/// <summary>The news diet (content/game/news.json).</summary>
public sealed record NewsRules
{
    /// <summary>Notable lines a day from the busy kinds of news; the rest of the day's are kept as routine and counted for the digest.</summary>
    public int Budget { get; init; } = 2;

    /// <summary>Kinds of news that always reach the Notable Ledger (war, the mayor's stories, the council, hardship).</summary>
    public IReadOnlyList<string> Always { get; init; } = [];

    /// <summary>The digest is written only when at least this many lines were held back.</summary>
    public int DigestMin { get; init; } = 3;

    /// <summary>How the digest names each kind of news: "from the roads", "at the shrines".</summary>
    public Dictionary<string, string> Phrases { get; init; } = [];
}

public sealed class NewsState
{
    /// <summary>The day the counts below are for.</summary>
    public int Day { get; set; }

    /// <summary>Busy-kind lines let through as Notable today.</summary>
    public int Admitted { get; set; }

    /// <summary>Lines held back today, by kind of news.</summary>
    public Dictionary<string, int> Held { get; } = [];
}

/// <summary>
/// The news diet: a great town makes a dozen notable things happen a day, too many to read at speed. War, the
/// mayor's own stories, the council and hardship always reach the Notable Ledger; of the rest only the day's first
/// few do, and the others stay in the full Ledger as routine, counted into one digest line the next morning.
/// Major news is never touched. No dice, so it never moves the main stream.
/// </summary>
public static class News
{
    public static NewsRules Rules(Town town) => town.Content.News;

    /// <summary>The importance a Notable line is written at: Notable while today's budget lasts, else Routine.</summary>
    public static Importance Admit(Town town, LedgerCategory cat)
    {
        var r = Rules(town);
        var s = town.News;
        if (s.Day != town.Day) Turn(town);
        string kind = cat.ToString();
        if (r.Always.Contains(kind)) return Importance.Notable;
        if (s.Admitted < r.Budget)
        {
            s.Admitted++;
            return Importance.Notable;
        }
        s.Held[kind] = s.Held.GetValueOrDefault(kind) + 1;
        return Importance.Routine;
    }

    /// <summary>A new day: yesterday's held-back news goes into one digest line, and the budget starts again.</summary>
    private static void Turn(Town town)
    {
        var r = Rules(town);
        var s = town.News;
        int held = s.Held.Values.Sum();
        if (held >= r.DigestMin && s.Day > 0)
        {
            var parts = s.Held
                .OrderByDescending(kv => kv.Value)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => $"{kv.Value} {r.Phrases.GetValueOrDefault(kv.Key, "more")}")
                .ToList();
            town.Ledger.Write(town.Tick, LedgerCategory.Town, Importance.Notable, $"Also on day {s.Day}: {Expedition.ListNames(parts)}. All of it is in the full Ledger.");
        }
        s.Day = town.Day;
        s.Admitted = 0;
        s.Held.Clear();
    }
}
