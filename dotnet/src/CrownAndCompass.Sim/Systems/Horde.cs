namespace CrownAndCompass.Sim.Systems;

/// <summary>How the big bad's host gathers at its camps before it marches (content/game/saga.json "horde").</summary>
public sealed record HordeRules
{
    /// <summary>Days a full host takes to gather from the seat and Holdings holdings; fewer camps gather slower, more faster.</summary>
    public double GatherDays { get; init; } = 40;
    public double SeatWeight { get; init; } = 2;
    public double Holdings { get; init; } = 2;

    /// <summary>Share of a camp a party kills each time it fights there and comes home without clearing it.</summary>
    public double Struck { get; init; } = 0.35;

    /// <summary>Days the bands take to come together at the seat once the host is full, and to march from it to the walls.</summary>
    public int MusterDays { get; init; } = 3;
    public int MarchDays { get; init; } = 6;

    /// <summary>Days past due it waits for a full host before it marches with what it has, and the least share of one it will march with.</summary>
    public int Patience { get; init; } = 20;
    public double MinShare { get; init; } = 0.6;

    /// <summary>With no eyes on its camps or roads, the town hears of a host on the road only this many days out.</summary>
    public int BlindDays { get; init; } = 2;

    /// <summary>Survivors of a beaten host come home to the seat's camp, up to this share of the next full host.</summary>
    public double Survivors { get; init; } = 0.4;

    /// <summary>The big bad's crisis coming to a head fills its camps up to this share of the host a crisis brings (<see cref="Crises"/>).</summary>
    public double Crisis { get; init; } = 0.7;

    /// <summary>Share of the treasury the host carries off when it first breaks into the lower town.</summary>
    public double Loot { get; init; } = 0.3;

    /// <summary>Ledger lines: {name}, {leader}, {seat}, {town}, {camp}, {spotter}.</summary>
    public string Gather { get; init; } = "";
    public string Half { get; init; } = "";
    public string HalfRumour { get; init; } = "";
    public string Massing { get; init; } = "";
    public string Thinned { get; init; } = "";
    public string Scattered { get; init; } = "";
    public string Thin { get; init; } = "";
    public string March { get; init; } = "";
    public string Breach { get; init; } = "";
}

public enum HordeStage
{
    None,
    Gathering,
    Mustering,
    Marching,
}

/// <summary>Warriors gathering at one of the big bad's sites.</summary>
public sealed class HordeCamp
{
    public string SiteId { get; init; } = "";
    public double Strength { get; set; }

    /// <summary>Parties that fought here and came home, as last counted: each new one thins the camp.</summary>
    public int SeenAttempts { get; set; }
}

public sealed class HordeState
{
    public HordeStage Stage { get; set; }
    public List<HordeCamp> Camps { get; } = [];
    public int GatherDay { get; set; }
    public int MusterTick { get; set; }

    /// <summary>How far the town has been warned this gathering: 1 gathering, 2 half gathered, 3 massing.</summary>
    public int Told { get; set; }
    public bool ToldThin { get; set; }

    /// <summary>The host has broken the walls once and burned the lower town: the next break falls the town.</summary>
    public bool Breached { get; set; }
}

/// <summary>
/// The final horde. The big bad does not conjure its host on the day it marches: its bands gather at its seat and
/// holdings, a little each day, until the host is full; then they come together at the seat and march. The town
/// hears that a host is gathering, but how big it is, when it moves and which way it comes only with eyes on its
/// camps (a tower or patrol beside them, a scrying, a scout) or on the road. With none, the first word of the march
/// comes from a frightened carter a day or two out. Heroes who fight at a camp thin it; clearing a holding scatters
/// its camp and slows the rest; an army can meet the host on the road (<see cref="Field"/>). Struck often enough,
/// the host is too thin to march at all. No countdown is ever shown: only stages.
/// </summary>
public static class Horde
{
    public static HordeRules Rules(Town town) => town.Content.Saga.Horde;

    public static HordeState State(Town town) => town.Saga.Horde;

    /// <summary>The big bad's host gathers this way; the warlord crowned keeps the warband's own clock (<see cref="Threat"/>).</summary>
    public static bool On(Town town, Menace m) => m.BigBad && m.Alive && m.Origin != Menaces.FromWarband;

    /// <summary>The host it marches with next, once full: its share of the town's defence, larger every march and with every dark quest done.</summary>
    public static double Full(Town town, Menace m)
    {
        var f = town.Content.Saga.Finale;
        var dq = town.Saga.DarkQuests;
        double more = (f.MarchGrowth + dq.Growth) * m.Marches + dq.Host;
        return Math.Max(1, Threat.Defense(town, allies: false) * (f.MarchArmy[^1] + more) * Storyteller.HordeSize(town));
    }

    public static double Gathered(Town town) => State(town).Camps.Sum(c => c.Strength);

    /// <summary>Whether the town has eyes on a site: a tower, outpost, scrying, mirror, scout or spire, or a patrol on its ground.</summary>
    public static bool Eyes(Town town, SiteState site)
    {
        if (Watch.LiveOf(town, site) != null) return true;
        var (x, y) = World.Spot(town, site);
        return Wilds.CoverAt(town, x, y) > 0;
    }

    /// <summary>The camps the town can see, in camp order.</summary>
    public static List<SiteState> Watched(Town town) =>
        State(town).Camps.Select(c => Quests.Site(town, c.SiteId)).Where(s => s != null && Eyes(town, s)).Select(s => s!).ToList();

    /// <summary>Whether the town would see the host leave its seat: eyes on the seat, a patrol on its road, scouts out, or the sky watch.</summary>
    public static bool RoadWatched(Town town, Menace m)
    {
        var seat = Quests.Site(town, m.SeatId);
        if (seat != null && (Eyes(town, seat) || Wilds.Warns(town, World.Spot(town, seat)) > 0)) return true;
        return Institutions.SkyWatch(town) > 0 || town.Scouts.Any(s => s.Out);
    }

    /// <summary>Whether the town knows the host is on the road (<see cref="Sightings"/> waits for eyes, or for it to come close).</summary>
    public static bool Known(Town town, Menace m) => !On(town, m) || m.ArrivalTick is not int at || Sightings.Of(town, Discovery.Key(m)) != null
        || RoadWatched(town, m) || at - town.Tick <= Rules(town).BlindDays * SimTime.HoursPerDay;

    /// <summary>One word on where the host stands, for the client: never a count of days.</summary>
    public static string Word(Town town, Menace m)
    {
        if (m.ArrivalTick is int at) return at - town.Tick <= SimTime.HoursPerDay ? "at the gates" : "on the march";
        return State(town).Stage == HordeStage.Mustering && Watched(town).Count > 0 ? "massing" : "gathering";
    }

    /// <summary>How full the host looks to the town's eyes: null when no camp is watched.</summary>
    public static double? Seen(Town town, Menace m)
    {
        if (!On(town, m) || m.ArrivalTick != null || Watched(town).Count == 0) return null;
        return Math.Clamp(Gathered(town) / Full(town, m), 0, 1);
    }

    /// <summary>Daily, from <see cref="Menaces"/>, while the big bad lives and no host of its is on the road.</summary>
    public static void Daily(Town town, Menace m)
    {
        var h = State(town);
        var r = Rules(town);
        if (h.Stage is HordeStage.None or HordeStage.Marching) Begin(town, m, h.Stage == HordeStage.Marching);
        Camps(town, m);
        double full = Full(town, m);
        if (h.Stage == HordeStage.Gathering)
        {
            double perDay = full / Math.Max(1, r.GatherDays) / Math.Max(1, r.SeatWeight + r.Holdings) * Storyteller.HordeGrowth(town) * Seasons.Activity(town, m.Kind);
            foreach (var c in h.Camps)
                if (Gathered(town) < full) c.Strength += perDay * (c.SiteId == m.SeatId ? r.SeatWeight : 1);
            Warn(town, m, full);
            var s = town.Saga;
            if (s.YokeId == m.Id && !s.Defied) return;
            bool claimed = town.Victory.Claimed != "";
            if (!claimed && (m.NextAssaultTick is not int next || town.Tick < next)) return;
            double share = Gathered(town) / full;
            int late = m.NextAssaultTick is int due ? (town.Tick - due) / SimTime.HoursPerDay : 0;
            if (!claimed && share < 0.999 && (late < r.Patience || share < r.MinShare))
            {
                if (late >= r.Patience && !h.ToldThin)
                {
                    h.ToldThin = true;
                    Tell(town, m, r.Thin, Importance.Major);
                }
                return;
            }
            h.Stage = HordeStage.Mustering;
            h.MusterTick = town.Tick + r.MusterDays * SimTime.HoursPerDay;
            if (Watched(town).Count > 0)
            {
                h.Told = 3;
                Tell(town, m, r.Massing, Importance.Major);
            }
            return;
        }
        if (h.Stage == HordeStage.Mustering && town.Tick >= h.MusterTick) March(town, m);
    }

    /// <summary>A new gathering: at the crowning, after a host has spent itself at the walls, or on a save from before the horde.</summary>
    private static void Begin(Town town, Menace m, bool after)
    {
        var h = State(town);
        var r = Rules(town);
        h.Stage = HordeStage.Gathering;
        h.GatherDay = town.Day;
        h.Told = 1;
        h.ToldThin = false;
        h.Camps.Clear();
        Camps(town, m);
        // What came home from the walls is the start of the next host.
        if (after && h.Camps.FirstOrDefault(c => c.SiteId == m.SeatId) is { } seat) seat.Strength = Math.Min(m.Power, Full(town, m) * r.Survivors);
        Tell(town, m, r.Gather, Importance.Major);
    }

    /// <summary>One camp at the seat and at each holding standing; a camp whose ground was cleared is scattered, and one a party fought at thins.</summary>
    private static void Camps(Town town, Menace m)
    {
        var h = State(town);
        var r = Rules(town);
        var sites = new List<string> { m.SeatId };
        sites.AddRange(m.Holdings.Where(id => id != m.SeatId && Quests.Site(town, id) is { Cleared: false }).Order(StringComparer.Ordinal));
        foreach (var c in h.Camps.ToList())
        {
            if (sites.Contains(c.SiteId)) continue;
            h.Camps.Remove(c);
            if (c.Strength >= 1 && Quests.Site(town, c.SiteId) is { } gone) Tell(town, m, r.Scattered, Importance.Notable, gone);
        }
        foreach (var id in sites)
        {
            if (Quests.Site(town, id) is not { } site || h.Camps.Any(c => c.SiteId == id)) continue;
            h.Camps.Add(new HordeCamp { SiteId = id, SeenAttempts = site.Attempts });
        }
        foreach (var c in h.Camps)
        {
            var site = Quests.Site(town, c.SiteId)!;
            if (site.Attempts <= c.SeenAttempts) continue;
            int fights = site.Attempts - c.SeenAttempts;
            c.SeenAttempts = site.Attempts;
            double before = c.Strength;
            c.Strength *= Math.Pow(1 - r.Struck, fights);
            if (before >= 1) Tell(town, m, r.Thinned, Importance.Notable, site);
        }
    }

    /// <summary>Its bands are coming together at the seat to march.</summary>
    public static bool Mustering(Town town, Menace m) => On(town, m) && m.ArrivalTick == null && State(town).Stage == HordeStage.Mustering;

    /// <summary>
    /// Its crisis comes to a head (<see cref="Crises"/>): the warriors it called join the seat's camp and the host musters
    /// now, full or not. A host already on the road takes them in.
    /// </summary>
    public static void Hasten(Town town, Menace m, double joined)
    {
        var h = State(town);
        if (m.ArrivalTick != null)
        {
            m.Power += joined;
            return;
        }
        if (h.Stage is HordeStage.None or HordeStage.Marching) Begin(town, m, h.Stage == HordeStage.Marching);
        Camps(town, m);
        if (h.Camps.FirstOrDefault(c => c.SiteId == m.SeatId) is { } seat) seat.Strength += joined;
        if (h.Stage == HordeStage.Mustering) return;
        h.Stage = HordeStage.Mustering;
        h.MusterTick = town.Tick + Rules(town).MusterDays * SimTime.HoursPerDay;
        if (Watched(town).Count == 0) return;
        h.Told = 3;
        Tell(town, m, Rules(town).Massing, Importance.Major);
    }

    /// <summary>The seat fell and the big bad got away: whatever gathered there is lost.</summary>
    internal static void Lost(Town town, string siteId)
    {
        if (State(town).Camps.FirstOrDefault(c => c.SiteId == siteId) is { } c) c.Strength = 0;
    }

    /// <summary>Word that it is half gathered: counted by the town's eyes at its camps, or a vague rumour without them.</summary>
    private static void Warn(Town town, Menace m, double full)
    {
        var h = State(town);
        var r = Rules(town);
        if (h.Told >= 2 || Gathered(town) < full * 0.5) return;
        h.Told = 2;
        var seen = Watched(town);
        if (seen.Count > 0) Tell(town, m, r.Half, Importance.Major, seen[0]);
        else Tell(town, m, r.HalfRumour, Importance.Notable);
    }

    /// <summary>The bands come together and set out: the host is all that gathered, and the town sees it go only with eyes on its road.</summary>
    private static void March(Town town, Menace m)
    {
        var h = State(town);
        var r = Rules(town);
        m.Power = Math.Max(1, Gathered(town));
        foreach (var c in h.Camps) c.Strength = 0;
        h.Stage = HordeStage.Marching;
        int days = r.MarchDays + Seasons.MarchDays(town);
        m.ArrivalTick = town.Tick + days * SimTime.HoursPerDay;
        m.Approach = Garrison.Approach(town, 1000 + m.Id * 31 + m.Marches);
        m.Marches++;
        if (RoadWatched(town, m)) Tell(town, m, r.March, Importance.Major);
    }

    /// <summary>
    /// The first time the big bad's host breaks the walls, the town is not taken in a day: the lower town burns and is
    /// looted, fear scars it, and the host draws off to gather again. False when that has happened already.
    /// </summary>
    public static bool Reprieve(Town town, Menace m, List<string> lines)
    {
        var h = State(town);
        if (!On(town, m) || h.Breached) return false;
        h.Breached = true;
        town.Book(TreasuryAccount.Tribute, -Math.Floor(Math.Max(0, town.Treasury) * Rules(town).Loot));
        Saga.Scar(town, town.Content.Saga.Dread.Sack);
        m.Power *= town.Content.Saga.Menaces.AssaultRetreat;
        lines.Add(Saga.Sentences(Saga.Fill(town, Rules(town).Breach, m)));
        return true;
    }

    private static void Tell(Town town, Menace m, string text, Importance importance, SiteState? camp = null)
    {
        if (text == "") return;
        string spotter = camp != null ? Spotter(town, camp) : "";
        string line = Saga.Fill(town, text, m)
            .Replace("{camp}", camp?.Def.Name ?? m.SeatName)
            .Replace("{spotter}", spotter)
            .Replace("{where}", Garrison.Place(town, m.Approach));
        town.Log(LedgerCategory.War, importance, Saga.Sentences(line));
    }

    /// <summary>Whose eyes saw the camp, in words for the Ledger.</summary>
    private static string Spotter(Town town, SiteState site) => Watch.LiveOf(town, site) switch
    {
        LiveSource.Outpost => $"the tower at {site.Def.Name}",
        LiveSource.Scrying or LiveSource.Mirror or LiveSource.Spire => "the town's scryers",
        LiveSource.Scout => "the town's scouts",
        LiveSource.Tower => "the watchtower",
        _ => "the town's patrols",
    };
}
