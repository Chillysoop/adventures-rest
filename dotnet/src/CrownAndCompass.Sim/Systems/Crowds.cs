namespace CrownAndCompass.Sim.Systems;

public sealed record CrowdRules
{
    /// <summary>Hours townsfolk go out to a bathhouse, theatre, market hall, library or the arena (from, until).</summary>
    public int VisitFrom { get; init; } = 9;
    public int VisitUntil { get; init; } = 21;

    /// <summary>How much an agent wants to go out: a base, more for a service their house wants, for loneliness and for the gregarious.</summary>
    public double VisitBase { get; init; } = 0.15;
    public double VisitWanted { get; init; } = 0.25;
    public double VisitLonely { get; init; } = 0.4;
    public double VisitGregarious { get; init; } = 0.1;

    /// <summary>Loneliness an hour out among people takes off, and the mood it gives.</summary>
    public double VisitCompany { get; init; } = 5;
    public double VisitMood { get; init; } = 4;

    /// <summary>Mood lost in the hour someone walks to a full building and has to think again.</summary>
    public double TurnedAwayMood { get; init; } = 8;

    /// <summary>Tiles further someone walks to a tavern for each point of their opinion of its keeper.</summary>
    public double Keeper { get; init; } = 0.1;

    /// <summary>Tiles past a full building someone will walk on to the next place of its kind before giving the outing up.</summary>
    public double Detour { get; init; } = 10;

    /// <summary>Share of a service's coverage lost for each visitor turned away yesterday (as a share of all who came).</summary>
    public double CoverageLoss { get; init; } = 0.5;

    /// <summary>How much a day of turned-away visitors adds to the townsfolk's wish to open another tavern, bathhouse or theatre.</summary>
    public double Demand { get; init; } = 1.5;

    /// <summary>The alert: when this share of a kind's visitors, and at least this many, were turned away yesterday.</summary>
    public double AlertShare { get; init; } = 0.2;
    public int AlertTurnedAway { get; init; } = 8;

    /// <summary>What the alert calls each kind of place: "Taverns", "Houses of the gods".</summary>
    public IReadOnlyDictionary<string, string> Names { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Room in the buildings townsfolk go out to (GDD rule C11: the tavern is the town's living room). Each one
/// holds <see cref="BuildingDef.Capacity"/> at once. Someone heading out walks to the place they prefer (the
/// tavern whose keeper they like, else the nearest); if it is full they are turned away at the door, try the
/// next place of the kind if it is near, and otherwise think again about their hour, a little put out.
/// Turned-away visitors cut the service's coverage of the houses that want it, make the townsfolk keener to
/// open another, and raise the "crowded" alert. Homes (beds) and workshops (jobs) have their own limits.
/// </summary>
public static class Crowds
{
    public const string Tavern = "tavern";
    public const string Holy = "holy";

    private static CrowdRules Rules(Town town) => town.Content.Crowds;

    /// <summary>What kind of outing a building is for: a tavern, a place to pray (a house of the gods, a temple's nave, the cathedral), or the service it serves; null when it takes no visitors.</summary>
    public static string? Kind(BuildingDef d) =>
        d.Capacity <= 0 ? null
        : d.Venue ? Tavern
        : d.Holy || d.Serves.ContainsKey(Worship) ? Holy
        : d.Serves.Count > 0 ? d.Serves.Keys.Order(StringComparer.Ordinal).First() : null;

    /// <summary>The house service places of prayer give.</summary>
    public const string Worship = "shrine";

    public static bool Outing(Activity act) => act is Activity.Drinking or Activity.Praying or Activity.Visiting;

    /// <summary>Open buildings of each outing kind, in id order.</summary>
    public static OrderedMap<string, List<Building>> Places(Town town)
    {
        var map = new OrderedMap<string, List<Building>>();
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Closed || b.ForSale || Kind(b.Def) is not { } kind) continue;
            if (!map.TryGetValue(kind, out var list)) map[kind] = list = [];
            list.Add(b);
        }
        return map;
    }

    public static List<Building> PlacesOf(Town town, string kind) =>
        (town.HourPlaces ?? Places(town)).TryGetValue(kind, out var list) ? list : [];

    // The hour ----------------------------------------------------------------------------------

    /// <summary>Count who is out where once, before the townsfolk choose their hour; Brain keeps the count as each one moves.</summary>
    internal static void Begin(Town town)
    {
        var present = new Dictionary<int, int>();
        foreach (var x in town.Agents)
            if (!x.Departed && !x.IsAway && Outing(x.Activity) && x.LocationId is int at) present[at] = present.GetValueOrDefault(at) + 1;
        town.HourPlaces = Places(town);
        town.HourErrands = Errands(town.HourPlaces);
        town.Present = present;
    }

    internal static void End(Town town)
    {
        town.HourPlaces = null;
        town.HourErrands = null;
        town.Present = null;
        town.PresentWasAt = null;
    }

    /// <summary>Before an agent chooses: where they were out last hour, so the running count can leave them out.</summary>
    internal static void Leave(Town town, Agent a) => town.PresentWasAt = Outing(a.Activity) ? a.LocationId : null;

    /// <summary>After an agent chose: move them in the running count.</summary>
    internal static void Moved(Town town, Agent a)
    {
        if (town.Present == null) return;
        if (town.PresentWasAt is int w) town.Present[w]--;
        if (Outing(a.Activity) && a.LocationId is int now)
        {
            int n = town.Present[now] = town.Present.GetValueOrDefault(now) + 1;
            var b = town.Building(now);
            if (n > b.Peak) b.Peak = n;
        }
        town.PresentWasAt = null;
    }

    /// <summary>Visitors in a building now, for the inspector and the map.</summary>
    public static int Inside(Town town, Building b)
    {
        int n = 0;
        foreach (var x in town.Agents) if (!x.Departed && !x.IsAway && Outing(x.Activity) && x.LocationId == b.Id) n++;
        return n;
    }

    /// <summary>Whether the agent was already in on an outing (last hour, while the townsfolk choose theirs).</summary>
    private static bool Stayed(Town town, Agent a, Building b) =>
        town.Present != null ? town.PresentWasAt == b.Id : Outing(a.Activity) && a.LocationId == b.Id;

    /// <summary>Other visitors in the building as the agent comes to the door.</summary>
    private static int Others(Town town, Agent a, Building b) =>
        town.Present != null
            // The running count still holds the agent's last hour.
            ? town.Present.GetValueOrDefault(b.Id) - (town.PresentWasAt == b.Id ? 1 : 0)
            : Inside(town, b) - (Stayed(town, a, b) ? 1 : 0);

    /// <summary>Whether there is room for the agent (always, for someone already in on an outing).</summary>
    public static bool Room(Town town, Agent a, Building b) => b.Def.Capacity <= 0 || Stayed(town, a, b) || Others(town, a, b) < b.Def.Capacity;

    /// <summary>
    /// The agent heads for the first of the places, best liked first. A full one turns them away at the door:
    /// they go on to the next with room if it is within the detour of the full one, else give the outing up (null).
    /// </summary>
    public static Building? Enter(Town town, Agent a, IReadOnlyList<Building> ordered)
    {
        if (ordered.Count == 0) return null;
        var first = ordered[0];
        var go = Room(town, a, first) ? first : null;
        if (go == null)
        {
            first.TurnedAway++;
            a.TurnedFrom = first.Id;
            var lot = town.Lot(first.LotId);
            double reach = Rules(town).Detour;
            for (int i = 1; i < ordered.Count && go == null; i++)
            {
                var next = ordered[i];
                var at = town.Lot(next.LotId);
                if (Js.Hypot(at.X - lot.X, at.Y - lot.Y) <= reach && Room(town, a, next)) go = next;
            }
            if (go == null) return null;
        }
        go.Visits++;
        a.LocationId = go.Id;
        return go;
    }

    /// <summary>
    /// As above, over places in no order: the agent heads for the one that costs least (a walk in tiles, less what draws them),
    /// and only a full door sorts the rest. Ties go by id.
    /// </summary>
    public static Building? Enter(Town town, Agent a, List<Building> places, Func<Building, double> cost)
    {
        Building? best = null;
        double least = 0;
        foreach (var b in places)
        {
            double c = cost(b);
            if (best == null || c < least || (c == least && b.Id < best.Id))
            {
                best = b;
                least = c;
            }
        }
        if (best == null) return null;
        if (Room(town, a, best))
        {
            best.Visits++;
            a.LocationId = best.Id;
            return best;
        }
        return Enter(town, a, places.OrderBy(cost).ThenBy(b => b.Id).ToList());
    }

    /// <summary>Tiles from (x, y) to the building.</summary>
    public static double Distance(Town town, Building b, (double X, double Y) from)
    {
        var lot = town.Lot(b.LotId);
        return Js.Hypot(lot.X - from.X, lot.Y - from.Y);
    }

    /// <summary>Where the agent sets out from: where they are, else home, else the square.</summary>
    public static (double X, double Y) From(Town town, Agent a)
    {
        int? at = a.LocationId ?? a.HomeId;
        if (at is int id)
        {
            var lot = town.Lot(town.Building(id).LotId);
            return (lot.X, lot.Y);
        }
        return (Town.Center, Town.Center);
    }


    // Going out ---------------------------------------------------------------------------------

    /// <summary>The service the agent would go out for today: one their house wants, else any the town has.</summary>
    public static string? Errand(Town town, Agent a)
    {
        var kinds = town.HourErrands ?? Errands(Places(town));
        if (kinds.Count == 0) return null;
        var wants = Wants(town, a);
        int wanted = 0;
        foreach (string k in kinds) if (wants.Contains(k)) wanted++;
        int pick = (a.Id + town.Day) % (wanted > 0 ? wanted : kinds.Count);
        foreach (string k in kinds)
        {
            if (wanted > 0 && !wants.Contains(k)) continue;
            if (pick-- == 0) return k;
        }
        return null;
    }

    /// <summary>The services the town has places to go out to, in kind order (not taverns or houses of the gods).</summary>
    private static List<string> Errands(OrderedMap<string, List<Building>> places)
    {
        var kinds = new List<string>();
        foreach (var (kind, list) in places)
            if (kind != Tavern && kind != Holy && list.Count > 0) kinds.Add(kind);
        return kinds;
    }

    private static IReadOnlyList<string> Wants(Town town, Agent a) =>
        a.HomeId is int h && City.Housing(town, town.Building(h).Def.Id) is { } house ? house.Services : [];

    /// <summary>How much an agent wants an hour out at a bathhouse, theatre or the like (0 when there is none, or they have been today).</summary>
    public static double VisitScore(Town town, Agent a, int hour)
    {
        var r = Rules(town);
        // Those with a trade go out after work.
        if (a.VisitedToday || hour < r.VisitFrom || hour >= r.VisitUntil || (a.JobId != null && SimTime.IsWorkHours(hour)) || Errand(town, a) is not { } kind) return 0;
        return r.VisitBase + (Wants(town, a).Contains(kind) ? r.VisitWanted : 0) + a.Loneliness / 100 * r.VisitLonely + (a.Has(Trait.Gregarious) ? r.VisitGregarious : 0);
    }

    /// <summary>Go out on today's errand; false when every place near was full.</summary>
    public static bool Visit(Town town, Agent a)
    {
        if (Errand(town, a) is not { } kind) return false;
        var from = From(town, a);
        var place = Enter(town, a, PlacesOf(town, kind), b => Distance(town, b, from));
        if (place == null) return false;
        a.VisitedToday = true;
        a.Loneliness = Math.Max(0, a.Loneliness - Rules(town).VisitCompany);
        return true;
    }

    public static double Mood(Town town, Agent a) =>
        (a.Activity == Activity.Visiting ? Rules(town).VisitMood : 0) - (a.TurnedFrom != null ? Rules(town).TurnedAwayMood : 0);

    // The day -----------------------------------------------------------------------------------

    /// <summary>Close the day's count at every door.</summary>
    public static void Daily(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (b.Def.Capacity <= 0) continue;
            b.VisitsLast = b.Visits;
            b.TurnedAwayLast = b.TurnedAway;
            b.PeakLast = b.Peak;
            b.Visits = b.TurnedAway = b.Peak = 0;
        }
        foreach (var a in town.Agents) a.VisitedToday = false;
    }

    /// <summary>Yesterday's visitors turned away from full places of a kind (tavern, holy, or a service), and how many came in all.</summary>
    public static (int TurnedAway, int Came) Yesterday(Town town, string kind)
    {
        int away = 0, came = 0;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || Kind(b.Def) != kind) continue;
            away += b.TurnedAwayLast;
            came += b.VisitsLast + b.TurnedAwayLast;
        }
        return (away, came);
    }

    /// <summary>Share of yesterday's visitors to a kind of place who found it full (0 to 1).</summary>
    public static double Shortfall(Town town, string kind)
    {
        var (away, came) = Yesterday(town, kind);
        return came == 0 ? 0 : (double)away / came;
    }

    /// <summary>Share of a house service lost to full buildings yesterday, across every place that serves it.</summary>
    public static double ServiceShortfall(Town town, string service)
    {
        int away = 0, came = 0;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Capacity <= 0 || !b.Def.Serves.ContainsKey(service)) continue;
            away += b.TurnedAwayLast;
            came += b.VisitsLast + b.TurnedAwayLast;
        }
        return came == 0 ? 0 : (double)away / came;
    }

    /// <summary>Coverage a service keeps after yesterday's turned-away visitors.</summary>
    public static double Coverage(Town town, string service) => 1 - Rules(town).CoverageLoss * ServiceShortfall(town, service);

    /// <summary>Extra wish to open another place of a kind, from yesterday's crowds at the door.</summary>
    public static double Demand(Town town, string kind) => Rules(town).Demand * Shortfall(town, kind);

    /// <summary>Kinds whose places turned away enough visitors yesterday to need another, with the count, in kind order.</summary>
    public static IEnumerable<(string Kind, int TurnedAway, Building Busiest)> Crowded(Town town)
    {
        var r = Rules(town);
        foreach (var (kind, list) in Places(town))
        {
            var (away, came) = Yesterday(town, kind);
            if (away < r.AlertTurnedAway || came == 0 || (double)away / came < r.AlertShare) continue;
            var busiest = list.OrderByDescending(b => b.TurnedAwayLast).ThenBy(b => b.Id).First();
            yield return (kind, away, busiest);
        }
    }
}
