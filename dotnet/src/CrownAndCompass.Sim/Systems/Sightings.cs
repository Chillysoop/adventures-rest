namespace CrownAndCompass.Sim.Systems;

/// <summary>An enemy force someone saw and ran to tell the town about: where it was, how many, and where it was heading.</summary>
public sealed class Sighting
{
    public int Id { get; init; }

    /// <summary>The force it follows ("site:goblin-den", "menace:3", "warband"): a new report on it replaces the old one.</summary>
    public string Key { get; init; } = "";

    /// <summary>band (raiders on their way to a farm), dead (the dead walking), host (an army marching on the walls), flight (raiders going home).</summary>
    public string Kind { get; init; } = "";
    public string Foe { get; set; } = "";

    /// <summary>About how many, as the witness counted them; 0 for a host too big to count.</summary>
    public int Count { get; set; }

    /// <summary>Who brought the news ("Brannoc from Hale Farm", "riders from Ironhold", "Wren, out scouting").</summary>
    public string Reporter { get; set; } = "";

    /// <summary>When they were seen, and when the report is too old to matter.</summary>
    public int Tick { get; set; }
    public int UntilTick { get; set; }

    /// <summary>Where they came from (a site) and where they were going: a building, or a section of the wall.</summary>
    public string? FromSite { get; init; }
    public int? TargetId { get; set; }
    public int? Section { get; set; }

    /// <summary>How far along the road they were when seen, 0 at the site and 1 at the target (the other way for a flight).</summary>
    public double Progress { get; set; }

    /// <summary>When a host set out, so later reports can say how far it has come.</summary>
    public int StartTick { get; init; }
}

/// <summary>
/// Sightings: the town learns of enemies on the move the way a real town would, from the people who see them. A
/// shepherd at an outlying farm sees a band coming down from the hills, a scout sees a host on the march, riders
/// from an allied town bring word. Each report is a Ledger line and a mark on the map where the force was seen,
/// heading where it was heading, fading as the news goes stale. Nothing is shown live: fog of war holds, and the
/// map shows what the town has been told. A band seen on its way gives the town a night to plant a flag over its
/// target (<see cref="Lairs"/>).
/// </summary>
public static class Sightings
{
    public const string Band = "band";
    public const string Dead = "dead";
    public const string Host = "host";
    public const string Flight = "flight";

    public static Sighting? Of(Town town, string key) => town.Sightings.FirstOrDefault(s => s.Key == key);

    public static void Clear(Town town, string key) => town.Sightings.RemoveAll(s => s.Key == key);

    /// <summary>Record a sighting (replacing any older one of the same force) and put the witness's words in the Ledger.</summary>
    public static Sighting Report(Town town, string key, string kind, string foe, int count, string? fromSite, int? targetId, int? section, double progress, int hours, string text, Importance importance = Importance.Notable, string? reporter = null)
    {
        var old = Of(town, key);
        Clear(town, key);
        var s = new Sighting
        {
            Id = ++town.SightingCount,
            Key = key,
            Kind = kind,
            Foe = foe,
            Count = count,
            Reporter = reporter ?? Witness(town, kind, targetId),
            Tick = town.Tick,
            UntilTick = town.Tick + hours,
            FromSite = fromSite,
            TargetId = targetId,
            Section = section,
            Progress = Math.Clamp(progress, 0, 1),
            StartTick = old?.StartTick ?? town.Tick,
        };
        town.Sightings.Add(s);
        if (key == Discovery.Warband || key.StartsWith("menace:", StringComparison.Ordinal))
        {
            if (key == Discovery.Warband) Discovery.Meet(town, key, Discovery.Sighted);
            else if (int.TryParse(key.AsSpan(7), out int mid) && town.Saga.Menaces.FirstOrDefault(m => m.Id == mid) is { } m) Discovery.Meet(town, m, Discovery.Sighted);
        }
        if (text != "") town.Log(LedgerCategory.War, importance, $"{Saga.Cap(s.Reporter)} {(s.Reporter.StartsWith("riders ", StringComparison.Ordinal) ? Plural(text) : text)}");
        return s;
    }

    /// <summary>
    /// Who saw them: for an army, a scout out on the roads or riders from a friendly town, else someone working the
    /// fields; for a band, whoever works the farm it is heading for, or the nearest outlying one.
    /// </summary>
    public static string Witness(Town town, string kind, int? targetId)
    {
        if (kind == Host && Institutions.SkyWatch(town) > 0) return SkyRider(town);
        if (kind == Host)
        {
            var scout = town.Scouts.Where(s => s.Out).OrderBy(s => s.Id).FirstOrDefault();
            if (scout != null && town.Agent(scout.HeroId) is { Dead: false } sa) return $"{sa.Name}, out scouting,";
            var friend = town.Settlements.Where(x => !x.Razed && x.Met && (x.Allied || x.Standing >= 40)).OrderByDescending(x => x.Standing).ThenBy(x => x.Id).FirstOrDefault();
            if (friend != null) return $"riders from {friend.Name}";
        }
        var target = targetId is int id ? town.Buildings.FirstOrDefault(b => b.Id == id) : null;
        var edge = target != null ? [target] : town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp"))
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .Take(3)
            .ToList();
        foreach (var b in edge)
        {
            var hand = b.Workers.Select(town.Agent).Where(a => !a.Dead && !a.Departed).OrderBy(a => a.Id).FirstOrDefault();
            if (hand != null) return $"{hand.GivenName} from {b.Name}";
        }
        return kind == Host ? "a frightened carter" : "a woodcutter";
    }

    /// <summary>A rider of the town's sky watch, by name when a hero in town flies, for the reporter of a sighting from the air.</summary>
    public static string SkyRider(Town town)
    {
        var flier = town.Heroes
            .Where(a => !a.IsAway && !a.Dead && !a.Departed && a.Hero!.Mount != "" && town.Content.Passage.Mount(a.Hero.Mount) is { } m && m.Ways.Contains("air"))
            .OrderBy(a => a.Id)
            .FirstOrDefault();
        return flier != null ? $"{flier.Name}, flying with the sky watch," : "a rider of the sky watch";
    }

    /// <summary>Daily: stale reports fade, and armies on the march are seen again as they come on.</summary>
    public static void Daily(Town town)
    {
        town.Sightings.RemoveAll(s => town.Tick >= s.UntilTick);
        foreach (var m in town.Saga.Menaces.Where(m => m.Alive && m.ArrivalTick != null).OrderBy(m => m.Id))
            Marching(town, $"menace:{m.Id}", m.Name, m.SeatId, m.Approach, m.ArrivalTick!.Value);
        if (town.Enemy.Stage == ThreatStage.Marching && town.Enemy.ArrivalTick is int at)
            Marching(town, "warband", town.Content.Enemy.Name, town.Content.Enemy.Outpost.Id, town.Enemy.Approach, at);
        else Clear(town, "warband");
        foreach (var m in town.Saga.Menaces.Where(m => m.ArrivalTick == null || !m.Alive)) Clear(town, $"menace:{m.Id}");
    }

    /// <summary>A host on the road: seen when it sets out, again each day as it comes on, and the Ledger told when it is a day out.</summary>
    private static void Marching(Town town, string key, string foe, string seat, int section, int arrival)
    {
        var old = Of(town, key);
        int start = old?.StartTick ?? town.Tick;
        double progress = arrival <= start ? 1 : (double)(town.Tick - start) / (arrival - start);
        int daysOut = Math.Max(0, (int)Math.Ceiling((arrival - town.Tick) / (double)SimTime.HoursPerDay));
        string where = Garrison.Place(town, section);
        string text = old == null
            ? $"brings word: {foe} has set out, heading for the {where}."
            : daysOut == 1 && old.Progress < 1 - 1e-9 && (arrival - old.Tick) > SimTime.HoursPerDay
                ? $"sees {foe} a day from the {where}, close enough to count the banners."
                : "";
        var s = Report(town, key, Host, foe, 0, seat, null, section, progress, 2 * SimTime.HoursPerDay, text, Importance.Major);
        if (old == null) return;
        // Quiet updates keep the old witness so the mark doesn't change hands every day.
        if (text == "") s.Reporter = old.Reporter;
    }

    /// <summary>The report's verb for several witnesses: "runs in" becomes "run in".</summary>
    private static string Plural(string text)
    {
        int sp = text.IndexOf(' ');
        string verb = sp < 0 ? text : text[..sp];
        return verb.EndsWith("es", StringComparison.Ordinal) && verb != "sees" ? verb[..^2] + text[verb.Length..] : verb.EndsWith('s') ? verb[..^1] + text[verb.Length..] : text;
    }

    /// <summary>How long ago, for the map label: "an hour ago", "5 hours ago", "yesterday".</summary>
    public static string Ago(Town town, Sighting s)
    {
        int h = Math.Max(0, town.Tick - s.Tick);
        return h < 1 ? "just now" : h == 1 ? "an hour ago" : h < SimTime.HoursPerDay ? $"{h} hours ago" : h < 2 * SimTime.HoursPerDay ? "yesterday" : $"{h / SimTime.HoursPerDay} days ago";
    }

    /// <summary>What the map label says: "about 12 goblins" or "the host of Gorrak".</summary>
    public static string What(Sighting s) => s.Kind switch
    {
        Host => s.Foe,
        Dead => "the walking dead",
        Flight => s.Count > 0 ? $"about {s.Count} {s.Foe}, heading home" : $"{s.Foe}, heading home",
        _ => s.Count > 0 ? $"about {s.Count} {s.Foe}" : s.Foe,
    };
}
