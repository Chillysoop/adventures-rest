namespace CrownAndCompass.Sim.Systems;

/// <summary>What one ward cares about: who speaks for it and the choices it likes and dislikes (content/game/wards.json).</summary>
public sealed record WardTemper
{
    public string Id { get; init; } = "";

    /// <summary>"the stallholders": who speaks for the ward.</summary>
    public string Voice { get; init; } = "";
    public List<string> Likes { get; init; } = [];
    public List<string> Dislikes { get; init; } = [];
}

public sealed record WardRules
{
    /// <summary>Feeling a choice moves a ward by, per tag it likes less each it dislikes; and the share kept each day.</summary>
    public double Step { get; init; } = 6;
    public double Decay { get; init; } = 0.98;

    /// <summary>At full feeling (±100), the contentment its households gain or lose, and the mood its residents carry.</summary>
    public double Comfort { get; init; } = 0.1;
    public double Mood { get; init; } = 3;
    public List<WardTemper> Wards { get; init; } = [];

    public WardTemper? Of(string id) => Wards.FirstOrDefault(w => w.Id == id);
}

/// <summary>
/// Wards with a character. Each ward of the city has a voice (the stallholders, the temple wardens, the
/// wharf crews) and cares about some kinds of choice more than others, the way heroes do (<see cref="Regard"/>).
/// When the mayor decides, the open wards judge it too: a ward that likes the choice is warmer toward the
/// mayor, its households more content and its houses quicker to grow; one that doesn't sours. It fades.
/// </summary>
public static class Wards
{
    private static WardRules Rules(Town town) => town.Content.Wards;

    public static double Feeling(Town town, string wardId) => town.City.Feeling.Get(wardId, 0);

    /// <summary>How a choice with these tags moves a ward.</summary>
    public static double Moves(Town town, WardTemper w, IReadOnlyCollection<string> tags) =>
        Rules(town).Step * (tags.Count(w.Likes.Contains) - tags.Count(w.Dislikes.Contains));

    /// <summary>The open wards a choice pleases and angers, by name.</summary>
    public static (List<WardDef> Pleased, List<WardDef> Angered) Reactions(Town town, IReadOnlyCollection<string> tags)
    {
        var pleased = new List<WardDef>();
        var angered = new List<WardDef>();
        if (tags.Count == 0) return (pleased, angered);
        foreach (var id in town.City.OpenWards)
        {
            if (Rules(town).Of(id) is not { } w) continue;
            double v = Moves(town, w, tags);
            if (v > 0) pleased.Add(City.Ward(town, id));
            else if (v < 0) angered.Add(City.Ward(town, id));
        }
        return (pleased, angered);
    }

    /// <summary>The wards judge a choice; the Ledger says which spoke up.</summary>
    public static void Judge(Town town, IReadOnlyCollection<string> tags, string about)
    {
        var (pleased, angered) = Reactions(town, tags);
        if (pleased.Count + angered.Count == 0) return;
        foreach (var ward in pleased.Concat(angered))
        {
            double v = Moves(town, Rules(town).Of(ward.Id)!, tags);
            town.City.Feeling[ward.Id] = Math.Clamp(Feeling(town, ward.Id) + v, -100, 100);
        }
        var parts = new List<string>();
        if (pleased.Count > 0) parts.Add($"{Voices(town, pleased)} are pleased");
        if (angered.Count > 0) parts.Add($"{Voices(town, angered)} grumble");
        town.Log(LedgerCategory.Town, Importance.Routine, $"{about}: {string.Join("; ", parts)}.");
    }

    private static string Voices(Town town, List<WardDef> wards)
    {
        var names = wards.Select(w => $"{Rules(town).Of(w.Id)!.Voice} of {w.Name}").ToList();
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    /// <summary>Contentment a household draws from how its ward feels about the mayor.</summary>
    public static double Comfort(Town town, Building house) =>
        Feeling(town, City.WardOf(town, town.Lot(house.LotId)).Id) / 100.0 * Rules(town).Comfort;

    /// <summary>Mood a resident carries from how their ward feels.</summary>
    public static double Mood(Town town, string wardId) => Feeling(town, wardId) / 100.0 * Rules(town).Mood;

    /// <summary>Old choices fade.</summary>
    public static void Daily(Town town)
    {
        foreach (var id in town.City.Feeling.Keys.ToList())
        {
            double v = Feeling(town, id) * Rules(town).Decay;
            town.City.Feeling[id] = Math.Abs(v) < 0.5 ? 0 : v;
        }
    }

    /// <summary>"warm", "cool"... for the City window.</summary>
    public static string Describe(double feeling) =>
        feeling >= 40 ? "devoted to you" : feeling >= 15 ? "warm toward you" : feeling <= -40 ? "angry with you" : feeling <= -15 ? "cool toward you" : "making up its mind";
}
