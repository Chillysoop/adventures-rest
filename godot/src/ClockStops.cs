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
        new("discoveries", "Discoveries", "An exploring company's first report of a find, its last chapter, and a step gone wrong. Routine steps wait on the alert strip.", true),
        new("sendings", "Sendings from parties", "A party out on a venture sends word and waits on your answer (early on, and high stakes later).", true),
        new("stories", "Story events", "A high-stakes story event pops up.", true),
        new("motions", "Council motions", "The council puts a motion to the vote. Off: it waits on the alert strip.", false),
        new("war", "War and raids", "Raids, battles, enemy hosts on the march and the war's other turning points.", true),
        new("deaths", "Deaths of heroes", "One of the town's heroes dies, at home or abroad.", true),
        new("crises", "Crises and the big bad", "A crisis begins, turns or ends, and every move of the campaign's great enemy.", true),
    ];

    /// <summary>The kind with this id; an id missing from the table is a bug at the call site.</summary>
    public static ClockStop Of(string id) =>
        All.FirstOrDefault(c => c.Id == id) ?? throw new KeyNotFoundException($"No clock stop \"{id}\": add it to ClockStops.All");
}
