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

    /// <summary>Where every menace kind, people, faction and hero background comes from (LoreTests checks each has one).</summary>
    public IReadOnlyList<HistoryLineage> Lineages { get; init; } = [];
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

/// <summary>
/// The lore check: who something descends from in the history, and what that makes it want and be.
/// A menace takes its kind's lineage for the region its seat is in (the one with no themes elsewhere);
/// a hero takes a background whose lineage lists their class, and a trait from it and from their people.
/// </summary>
public sealed record HistoryLineage
{
    public string Id { get; init; } = "";

    /// <summary>The history power it descends from.</summary>
    public string Power { get; init; } = "";

    /// <summary>What it is the lineage of: "menace:undead", "people:dwarves", "faction:quiet-hand", "background:sage".</summary>
    public IReadOnlyList<string> Subjects { get; init; } = [];

    /// <summary>Menaces only: the region themes it holds; none for its kind's lineage everywhere else.</summary>
    public IReadOnlyList<string> Themes { get; init; } = [];
    public string Title { get; init; } = "";

    /// <summary>What it wants, and why ({leader}, {seat}, {region}, {town} for menaces).</summary>
    public string Motive { get; init; } = "";

    /// <summary>Habits it leaves on a person (Trait names).</summary>
    public IReadOnlyList<string> Traits { get; init; } = [];

    /// <summary>Backgrounds only: the classes that come up through it.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>Menaces only: how much likelier each ambition is for its heirs.</summary>
    public Dictionary<string, double> Ambitions { get; init; } = [];
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

    /// <summary>The lineage of a people, faction or background ("people:dwarves"), if the history gives one.</summary>
    public static HistoryLineage? LineageOf(HistoryRules h, string subject) =>
        h.Lineages.FirstOrDefault(l => l.Subjects.Contains(subject));

    public static HistoryLineage? Lineage(HistoryRules h, string id) => h.Lineages.FirstOrDefault(l => l.Id == id);

    /// <summary>A menace's lineage: its kind's heirs of the region its seat lies in, else its kind's own.</summary>
    public static HistoryLineage? ForMenace(HistoryRules h, string kind, string? theme)
    {
        var heirs = h.Lineages.Where(l => l.Subjects.Contains("menace:" + kind)).ToList();
        return heirs.FirstOrDefault(l => theme != null && l.Themes.Contains(theme)) ?? heirs.FirstOrDefault(l => l.Themes.Count == 0);
    }

    /// <summary>The motive a menace's lineage gives it, told with its names.</summary>
    public static string Motive(Town town, Menace m) =>
        Lineage(town.Content.History, m.Lineage) is HistoryLineage l && l.Motive != "" ? Saga.Fill(town, l.Motive, m) : "";

    /// <summary>
    /// A new hero's past: a background whose lineage lists their class, and a trait from it and from the
    /// people who sent them. Chosen by the hero's id, not dice, so arrivals elsewhere replay unchanged.
    /// </summary>
    public static void Raise(Town town, Agent a, HeroSheet sheet, string? people)
    {
        var h = town.Content.History;
        var backgrounds = town.Content.Library.Backgrounds
            .Where(b => LineageOf(h, "background:" + b.Id) is HistoryLineage l && l.Classes.Contains(sheet.Cls.Id))
            .ToList();
        if (backgrounds.Count > 0)
        {
            var b = backgrounds[a.Id % backgrounds.Count];
            sheet.Background = b.Id;
            Shape(a, LineageOf(h, "background:" + b.Id)!.Traits, a.Id);
        }
        if (people != null && LineageOf(h, "people:" + people) is HistoryLineage kin) Shape(a, kin.Traits, a.Id / 2);
    }

    private static void Shape(Agent a, IReadOnlyList<string> traits, int pick)
    {
        if (traits.Count == 0 || !Enum.TryParse<Trait>(traits[pick % traits.Count], out var t)) return;
        if (a.Traits.Contains(t) || Population.Opposed(a.Traits, t)) return;
        a.Traits = new HashSet<Trait>(a.Traits) { t };
    }

    /// <summary>"Once a caravan guard: rode guard on the Oasis road, ..." for arrivals and the hero sheet.</summary>
    public static string Past(Town town, Agent a)
    {
        if (a.Hero is HeroSheet { Past: not "" } turned) return turned.Past;
        if (a.Hero is not HeroSheet hs || hs.Background == "" || town.Content.Library.Background(hs.Background) is not LibBackground b) return "";
        var l = LineageOf(town.Content.History, "background:" + b.Id);
        return l == null || l.Motive == "" ? $"Once {Agent.Article(b.Name)} {b.Name.ToLowerInvariant()}." : $"Once {Agent.Article(b.Name)} {b.Name.ToLowerInvariant()}: {l.Motive}.";
    }
}
