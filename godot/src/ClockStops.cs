namespace CrownAndCompass.Client;

/// <summary>One kind of event that can stop the clock: its id, its line in the Settings window, and whether it stops it unless the player says otherwise.</summary>
public sealed record ClockStop(string Id, string Label, string Tip, bool Default);

/// <summary>
/// Every kind of event that stops the clock (real time with pause), in the order the Settings window lists
/// them. This is the one table: a new kind is one line here plus <c>StopClock("id")</c> where it happens
/// (<see cref="Main.StopClock"/>). The player's toggle in Settings (profile "settings.clock") overrides the
/// default; the menus, the title and the end screen stop the clock on their own and are not listed.
/// </summary>
public static class ClockStops
{
    public static readonly ClockStop[] All =
    [
        new("discoveries", "Discoveries", "First report, last step, or a step gone wrong", true),
        new("first-contact", "First meetings", "A faction or foe met for the first time", true),
        new("sendings", "Sendings from parties", "A party asks what to do", true),
        new("stories", "Story events", "High-stakes stories", true),
        new("motions", "Council motions", "Off: they wait on the Council badge", false),
        new("war", "War and raids", "Raids, battles, hosts on the march", true),
        new("deaths", "Deaths of heroes", "At home or abroad", true),
        new("crises", "Crises and the big bad", "Crisis turns and the big bad's moves", true),
        new("mayor", "Your mayor falls", "Killed by a knife, a breach or the road", true),
        new("storm", "Storms", "A storm breaks over the town", false),
    ];

    /// <summary>The kind with this id; an id missing from the table is a bug at the call site.</summary>
    public static ClockStop Of(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No clock stop \"{id}\": add it to ClockStops.All");
}
