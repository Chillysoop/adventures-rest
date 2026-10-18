namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The world's history (content/game/history.json): six ages from the gods' making of the world to the
/// town's charter, and the powers that rose and fell in each stretch of the wilds. The sim tells it in
/// passing (the charter, a region's first visit, a people's alliance); the client draws it as a timeline.
/// </summary>
public sealed record HistoryRules
{
    /// <summary>The calendar: years after the Lamp ("AL"), before it ("BL"), and the year of the charter.</summary>
    public string Era { get; init; } = "AL";
    public string Before { get; init; } = "BL";
    public string EraName { get; init; } = "";
    public int Year { get; init; } = 1190;

    /// <summary>Added to the charter: {year}, and {caldor} for the years before the Lamp the High Kingdom was founded.</summary>
    public string Founding { get; init; } = "";

    /// <summary>A region's story on the first visit: {party}, {region}, {history}.</summary>
    public string Visit { get; init; } = "";

    /// <summary>A people's story when they ally: {People}, {history}.</summary>
    public string Ally { get; init; } = "";
    public IReadOnlyList<HistoryLane> Lanes { get; init; } = [];
    public IReadOnlyList<HistoryAge> Ages { get; init; } = [];
    public IReadOnlyList<HistoryPower> Powers { get; init; } = [];
    public IReadOnlyList<HistoryEvent> Events { get; init; } = [];
}

/// <summary>A stretch of the world the timeline gives a column: the region themes that lie there now.</summary>
public sealed record HistoryLane
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Themes { get; init; } = [];
}

/// <summary>An age: its span in years (negative before the Lamp) and how tall the timeline draws it.</summary>
public sealed record HistoryAge
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public int From { get; init; }
    public int To { get; init; }
    public double Weight { get; init; } = 1;
    public string Text { get; init; } = "";
}

/// <summary>An empire, realm or people that held some lanes for a span of years.</summary>
public sealed record HistoryPower
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>elder, elves, dwarves, gnomes, serpent, empire, folk, free, frost, fen, caravan, warrens, league, dark or sleeping: how the timeline colours it.</summary>
    public string Kind { get; init; } = "";

    /// <summary>Neighbouring lanes it spans, west to east.</summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];
    public int From { get; init; }
    public int To { get; init; }
    public string Text { get; init; } = "";

    /// <summary>What of it is left in the game.</summary>
    public string Heirs { get; init; } = "";
}

public sealed record HistoryEvent
{
    public int Year { get; init; }
    public string Text { get; init; } = "";
}

public static class Annals
{
    /// <summary>"1180 BL", "724 AL".</summary>
    public static string Year(HistoryRules h, int year) => year < 1 ? $"{-year} {h.Before}" : $"{year} {h.Era}";

    /// <summary>The age a year falls in (the last one for years after the charter).</summary>
    public static HistoryAge? AgeOf(HistoryRules h, int year) =>
        h.Ages.FirstOrDefault(a => year >= a.From && year < a.To) ?? h.Ages.LastOrDefault();

    /// <summary>The powers that held a lane, oldest first.</summary>
    public static IEnumerable<HistoryPower> InLane(HistoryRules h, string lane) =>
        h.Powers.Where(p => p.Lanes.Contains(lane)).OrderBy(p => p.From).ThenBy(p => p.Id, StringComparer.Ordinal);

    /// <summary>The line the charter adds: the ford's story and the year.</summary>
    public static string Founding(Town town)
    {
        var h = town.Content.History;
        int caldor = h.Powers.FirstOrDefault(p => p.Id == "caldor")?.From ?? 0;
        return h.Founding.Replace("{year}", h.Year.ToString()).Replace("{caldor}", (-caldor).ToString());
    }

    /// <summary>The region's old story: one of its theme's, chosen by the region so that neighbours differ (no dice).</summary>
    public static string? Story(Region region) =>
        region.Theme.History.Count == 0 ? null : region.Theme.History[region.Id % region.Theme.History.Count];

    /// <summary>A party's first visit to a region: the old story of whoever held it.</summary>
    public static void Visited(Town town, Party party, Region region)
    {
        var h = town.Content.History;
        if (Story(region) is not string story || h.Visit == "") return;
        town.Log(LedgerCategory.Quest, Importance.Notable, h.Visit
            .Replace("{party}", party.Name)
            .Replace("{region}", region.Name)
            .Replace("{history}", story));
    }

    /// <summary>A people allies with the town, and tells where they come from.</summary>
    public static void Allied(Town town, Settlement s)
    {
        var h = town.Content.History;
        if (s.Kin.History == "" || h.Ally == "") return;
        string people = s.People;
        town.Log(LedgerCategory.Town, Importance.Notable, h.Ally
            .Replace("{People}", people.Length == 0 ? people : char.ToUpperInvariant(people[0]) + people[1..])
            .Replace("{history}", s.Kin.History.Replace("{S}", s.Name)));
    }
}
