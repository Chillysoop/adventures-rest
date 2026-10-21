namespace CrownAndCompass.Sim.Systems;

/// <summary>The first time the town came across someone: who (a <see cref="Discovery"/> key), on what day, and how.</summary>
public sealed class Meeting
{
    /// <summary>"warband", "menace:3", "power:the-grandmothers", "people:2": the same keys <see cref="Sightings"/> uses.</summary>
    public string Key { get; set; } = "";
    public int Day { get; set; }

    /// <summary>How the town learned of them: one of the <c>Discovery</c> ways (sighted, reported, scouted, scried, met, attacked, heard).</summary>
    public string How { get; set; } = "";
}

/// <summary>
/// Who the town knows is out there. A game starts knowing no enemy and no power: each one is met once, when someone
/// sees it, a witness or messenger reports it, a scout or a scrying finds it, a party runs into it, or it strikes. The
/// War tab, the alerts and the bounty board show only what is known, and a first meeting is the moment a first-contact
/// scene can hang on (<see cref="MetToday"/>). Reading this never rolls dice, so it changes nothing the sim decides.
/// </summary>
public static class Discovery
{
    public const string Warband = "warband";

    public const string Sighted = "sighted";
    public const string Reported = "reported";
    public const string Scouted = "scouted";
    public const string Scried = "scried";
    public const string Met = "met";
    public const string Attacked = "attacked";
    public const string Heard = "heard";

    public static string Key(Menace m) => m.Origin == Menaces.FromWarband ? Warband : $"menace:{m.Id}";

    public static Meeting? Of(Town town, string key) => town.Discovered.FirstOrDefault(c => c.Key == key);

    public static bool Known(Town town, string key) => Of(town, key) != null;

    /// <summary>A menace is known once the town has its name; the warband once it has been seen.</summary>
    public static bool Known(Town town, Menace m) => m.Origin == Menaces.FromWarband ? Known(town, Warband) : m.Named || Known(town, Key(m));

    /// <summary>The living menaces the town knows of, in id order.</summary>
    public static List<Menace> KnownMenaces(Town town) => town.Saga.Menaces.Where(m => m.Alive && Known(town, m)).OrderBy(m => m.Id).ToList();

    /// <summary>Does the town know of any foe at all: the warband or a menace.</summary>
    public static bool AnyFoe(Town town) => (Threat.IsActive(town) && Known(town, Warband)) || town.Saga.Menaces.Any(m => m.Alive && Known(town, m));

    /// <summary>The town comes across someone. Only the first meeting counts; returns whether this was it.</summary>
    public static bool Meet(Town town, string key, string how)
    {
        if (Known(town, key)) return false;
        town.Discovered.Add(new Meeting { Key = key, Day = town.Day, How = how });
        return true;
    }

    /// <summary>A menace met: seen or struck by, though the town may not know its leader's name yet (<see cref="Menace.Named"/>).</summary>
    public static bool Meet(Town town, Menace m, string how) => Meet(town, Key(m), how);

    /// <summary>Someone of the town's saw a site with their own eyes (a party, a scout, a scrying): whoever holds it is met.</summary>
    public static void AtSite(Town town, string siteId, string how)
    {
        if (siteId == town.Content.Enemy.Outpost.Id && Threat.IsActive(town)) Meet(town, Warband, how);
        foreach (var m in town.Saga.Menaces.Where(m => m.Alive && (m.SeatId == siteId || m.Holdings.Contains(siteId))).OrderBy(m => m.Id)) Meet(town, m, how);
    }

    /// <summary>Who the town met today, oldest first: what a first-contact scene picks up.</summary>
    public static IEnumerable<Meeting> MetToday(Town town) => town.Discovered.Where(c => c.Day == town.Day);

    /// <summary>
    /// Daily, and on loading a save: the warband is known once its scouts have been seen watching the town, and a menace
    /// the town learned the name of by any road is recorded as met that day.
    /// </summary>
    public static void Daily(Town town)
    {
        if (StageSeen(town.Enemy.Stage)) Meet(town, Warband, Reported);
        foreach (var m in town.Saga.Menaces.Where(m => m.Named && m.Origin != Menaces.FromWarband).OrderBy(m => m.Id)) Meet(town, Key(m), Heard);
    }

    /// <summary>From probing on, the warband's scouts and war camp have been seen; before that there are only omens.</summary>
    private static bool StageSeen(ThreatStage s) => s is ThreatStage.Probing or ThreatStage.Raids or ThreatStage.Marching or ThreatStage.Defeated or ThreatStage.Victorious;
}
