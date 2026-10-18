namespace CrownAndCompass.Sim.Systems;

/// <summary>How an alert behaves on screen: minor ones fade on their own, decisions and urgent ones stay until resolved.</summary>
public enum AlertLevel
{
    Minor,
    Decision,
    Urgent,
}

/// <summary>The window that answers an alert.</summary>
public enum AlertWindow
{
    None,
    Ledger,
    War,
    Council,
    Bounties,
    Faith,
    Campaign,
    City,
    Story,
    Mayor,
    Diplomacy,
    Companies,
}

/// <summary>A group of alerts: one icon on the strip, with a count and a hover list of what is in it.</summary>
public sealed record AlertKind(string Id, string Title, string Icon, AlertLevel Level, AlertWindow Window);

/// <summary>
/// One thing the player should act on. The key stays the same while it is the same problem, so a dismissed
/// alert stays dismissed until it is resolved and comes back. Site, agent or town aim the camera.
/// </summary>
public sealed record Alert(string Key, AlertKind Kind, string Text, string Tip, string? SiteId = null, int? AgentId = null, bool AtTown = false, int? StoryId = null, int? Wall = null, int? FindId = null);

/// <summary>
/// The player's to-do list, in the manner of Paradox games. Standing alerts are read off the town each
/// time (nothing here is saved): an alert lasts while its rule names it, so resolving the problem clears
/// it. Each alert is an action for the player, Paradox style: news belongs in the Ledger and the feed, not
/// here. To raise a new alert, add a kind below and a
/// rule to <see cref="Rules"/> (or a line in an existing rule); the client groups, orders and aims it.
/// </summary>
public static class Alerts
{
    public static readonly AlertKind Siege = new("siege", "Under attack", "warband", AlertLevel.Urgent, AlertWindow.War);
    public static readonly AlertKind Hunger = new("hunger", "Hunger", "bread", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Coffers = new("coffers", "Empty coffers", "gold", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Unrest = new("unrest", "Unrest", "mandate", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Hostile = new("hostile", "Hostile factions", "suspicion", AlertLevel.Urgent, AlertWindow.Faith);
    public static readonly AlertKind Motion = new("motion", "Council motions", "mandate", AlertLevel.Decision, AlertWindow.Council);
    public static readonly AlertKind Stores = new("stores", "Winter stores", "bread", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Fealty = new("fealty", "Demands for fealty", "warband", AlertLevel.Decision, AlertWindow.Campaign);
    public static readonly AlertKind Below = new("below", "Stirring below", "warband", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Trade = new("trade", "Trade routes", "gold", AlertLevel.Minor, AlertWindow.Diplomacy);
    public static readonly AlertKind Charter = new("charter", "Charters", "favour", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind GrandWork = new("grandwork", "Grand works", "gold", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Crisis = new("crisis", "A crisis", "d20", AlertLevel.Urgent, AlertWindow.Story);
    public static readonly AlertKind Story = new("story", "Events", "d20", AlertLevel.Decision, AlertWindow.Story);
    public static readonly AlertKind Explorers = new("explorers", "Explorers' reports", "compass", AlertLevel.Decision, AlertWindow.Bounties);
    public static readonly AlertKind LevelUp = new("levelup", "Level up", "d20", AlertLevel.Decision, AlertWindow.Mayor);
    public static readonly AlertKind Bastion = new("bastion", "Bastions", "heroes", AlertLevel.Decision, AlertWindow.Companies);
    public static readonly AlertKind Heroes = new("heroes", "Restless heroes", "heroes", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Housing = new("housing", "Housing", "homeless", AlertLevel.Minor, AlertWindow.Council);
    public static readonly AlertKind Work = new("work", "Idle hands", "population", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Settlers = new("settlers", "No settlers", "population", AlertLevel.Decision, AlertWindow.City);


    /// <summary>Every kind, in the order their icons sit on the strip within a level.</summary>
    public static readonly IReadOnlyList<AlertKind> Kinds =
        [Siege, Crisis, Hunger, Coffers, Unrest, Hostile, Motion, Fealty, Stores, Story, Explorers, LevelUp, Charter, GrandWork, Bastion, Heroes, Work, Settlers, Below, Trade, Housing];

    /// <summary>The standing rules, each naming what needs the player now. No rng and no writes: the client calls them often.</summary>
    public static readonly IReadOnlyList<Func<Town, IEnumerable<Alert>>> Rules = [War, Events, Food, Money, Council, Hostility, Winter, Growth, Routes, Undercity, Loyalty, Homes];

    public static List<Alert> Standing(Town town) => Rules.SelectMany(r => r(town)).ToList();

    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    private static IEnumerable<Alert> War(Town t)
    {
        if (t.Enemy.Stage == ThreatStage.Marching && t.Enemy.ArrivalTick is int at)
        {
            int days = Math.Max(1, (int)Math.Ceiling((at - t.Tick) / 24.0));
            yield return new("march", Siege, $"Siege in {Days(days)}", $"The warband is marching on the {Garrison.Place(t, t.Enemy.Approach)}. Call the watch to arms there and keep heroes home.", AtTown: true, Wall: t.Enemy.Approach);
        }
        else if (t.Enemy.Stage == ThreatStage.Raids)
            yield return new("raids", Siege, "Raiders by night", "The warband raids the town when its heroes are away. Build walls, hire the watch, clear the war camp.");
        foreach (var m in t.Saga.Menaces.Where(m => m.Alive && m.Named && m.ArrivalTick is int).OrderBy(m => m.Id))
        {
            int days = Math.Max(1, (int)Math.Ceiling((m.ArrivalTick!.Value - t.Tick) / 24.0));
            yield return new($"menace:{m.Id}", Siege, $"{Cap(m.Name)} in {Days(days)}", $"The host of {m.Name} marches from {m.SeatName} on the {Garrison.Place(t, m.Approach)}. Call the watch to arms there and the heroes home.", AtTown: true, Wall: m.Approach);
        }
        if (Menaces.PendingDemand(t) is { } dm)
            yield return new($"demand:{dm.Id}", Fealty, "A demand for fealty", $"{Cap(dm.Name)} wants the town as its vassal. Bend the knee or refuse in the Campaign window; silence is a refusal.");
    }

    /// <summary>Each story waiting on the mayor; high stakes ones are a crisis.</summary>
    private static IEnumerable<Alert> Events(Town t)
    {
        foreach (var s in Stories.Pending(t))
        {
            var d = Stories.DefOf(t, s);
            int days = Math.Max(1, (int)Math.Ceiling((s.DueTick - t.Tick) / 24.0));
            yield return new($"story:{s.Id}:{s.Stage}", d.Stakes == "high" ? Crisis : Story, Stories.Fill(t, s, d.Title),
                $"{Cap(Stories.StakesOf(t, s).Name)} stakes. Step in and roll, or in {Days(days)} it runs its course without you.", StoryId: s.Id, AgentId: s.HeroId);
        }
        // Each find explorers have sent word of and are waiting on.
        foreach (var f in Explore.Waiting(t))
        {
            var s = Explore.Survey(t, f.SurveyId)!;
            int hours = Math.Max(1, f.DueTick - t.Tick);
            yield return new($"find:{f.Id}:{f.Log.Count}", Explorers, Explore.Title(t, f),
                $"The {s.Name} found {Explore.What(t, f)} and wait for word. In {(hours >= 24 ? Days((int)Math.Ceiling(hours / 24.0)) : $"{hours} hours")} their leader decides.", FindId: f.Id);
        }
        if (Sheet.Pending(t) > 0)
        {
            var (level, improvement) = Sheet.NextAdvance(t);
            yield return new($"levelup:{level}", LevelUp, $"Mayor {t.Stories.Mayor.Name} reached level {level}",
                $"Choose how the mayor grows on the Mayor tab: {(improvement ? "raise two abilities or take a feat" : "learn a new skill, or become expert in one")}.");
        }
    }

    private static IEnumerable<Alert> Food(Town t)
    {
        double price = t.Market.Price(Good.Food);
        int starving = t.Workers.Count(a => a.Hunger >= 85);
        if (price >= t.Market.BasePrice(Good.Food) * 2.2 || starving >= Math.Max(3, t.Population / 8))
            yield return new("hunger", Hunger, starving > 0 ? $"{starving} going hungry" : "Bread is scarce",
                $"A loaf costs {price:0.00}g and the stores are thin. Zone more farmland, or bless the harvest.");
    }

    private static IEnumerable<Alert> Money(Town t)
    {
        if (t.Treasury < 25)
            yield return new("treasury", Coffers, "The coffers are empty", $"{Math.Floor(t.Treasury)}g left. Raise taxes or hold back on bounties.");
    }

    private static IEnumerable<Alert> Council(Town t)
    {
        if (Systems.Council.PendingMotion(t) is { } m)
            yield return new($"motion:{m.Id}", Motion, $"Council votes in {Days(Systems.Council.DaysLeft(t, m))}",
                $"The council moves that {m.Text}. Sign or veto it in the Council window, or the council's vote settles it.");
        if (t.Mandate < 30)
            yield return new("mandate", Unrest, "The town is turning on you", $"Mandate {Math.Round(t.Mandate)}. Below this the council stops listening.");
    }

    private static IEnumerable<Alert> Hostility(Town t)
    {
        foreach (var f in t.Factions.Where(f => f.Active && f.Known && f.Standing <= -30).OrderBy(f => f.Id, StringComparer.Ordinal))
            yield return new($"faction:{f.Id}", Hostile, $"{Cap(f.Name)} turns hostile", "A faction in town is working against you. Crack down, or win it back.");
    }

    private static IEnumerable<Alert> Winter(Town t)
    {
        var s = Seasons.Current(t);
        bool late = s.Id == "winter" || (Seasons.Next(t).Id == "winter" && Seasons.DaysLeft(t) <= 10);
        if (late && Seasons.StoresFull(t) < 0.5 && Seasons.LayInQuote(t).Food > 0)
            yield return new("stores", Stores, "The winter stores are short",
                $"The cellars and granaries hold {Math.Round(Seasons.StoresFull(t) * 100)}% of what the town needs to see out the winter. Lay in grain from the traders in the City window, or build a granary.");
    }

    private static IEnumerable<Alert> Growth(Town t)
    {
        if (City.NextTier(t) is { } charter && City.Missing(t).Count == 0)
            yield return new($"charter:{charter.Id}", Charter, $"Claim the charter of a {charter.Name.ToLowerInvariant()}",
                "The town has all the charter asks for. Seal it in the City window to open new wards and buildings.");
        // Gold piling up with a grand work to spend it on: begin one (only while none is rising).
        if (t.Buildings.Any(b => b.Def.IsGrand && b.IsSite)) yield break;
        var work = GrandWorks.All(t)
            .Where(d => Planning.Blocked(t, d) == null && t.Treasury >= 3 * GrandWorks.StageCost(t, d, 0) + 1000)
            .OrderBy(d => d.Stages.Sum(s => s.Gold))
            .ThenBy(d => d.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (work != null)
            yield return new($"grandwork:{work.Id}", GrandWork, $"Gold to spare: raise the {work.Name.ToLowerInvariant()}",
                $"The treasury could begin a grand work. {GrandWorks.Bonus(work)} once it stands. Begin it from the City window.");
    }

    private static IEnumerable<Alert> Routes(Town t)
    {
        if (Systems.Trade.Open(t).Count() >= Systems.Trade.Cap(t)) yield break;
        var s = t.Settlements.Where(x => Systems.Trade.Refuse(t, x) == null).OrderByDescending(x => x.Standing).ThenBy(x => x.Id).FirstOrDefault();
        if (s != null)
            yield return new("trade", Trade, $"Open a trade route to {s.Name}",
                $"{Cap(s.People)} will trade, and the charter allows another route. Open one in the Diplomacy window: a caravan sells the town's surplus there and brings back what the town is short of.");
    }

    private static IEnumerable<Alert> Undercity(Town t)
    {
        var r = t.Content.Undercity;
        foreach (var l in t.Undercity)
        {
            var d = Systems.Undercity.Def(t, l);
            var site = Systems.Undercity.Site(t, l);
            if (site.Cleared || l.Patrol || l.Stir < r.WarnAt) continue;
            if (t.Bounties.Any(b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) continue;
            yield return new($"below:{l.Id}", Below, $"Something stirs in {d.Name}",
                $"Stir {Math.Round(l.Stir)} of {Js.Str(r.RiseAt)}. Post a bounty on it in Bounties, or send the watch down on patrol from the City window, before it rises into the streets.");
        }
    }

    private static IEnumerable<Alert> Loyalty(Town t)
    {
        foreach (var r in Bastions.Pending(t))
            yield return new($"bastion:{r.Id}", Bastion, $"The {r.Company} ask for a bastion",
                $"They want ground on the edge of town for a compound, and would hire townsfolk to staff it. Grant it, give the land free or refuse in the Companies window; in {Days(Bastions.DaysLeft(t, r))} they take silence as leave.");
        foreach (var a in t.Heroes.Where(a => a.Hero!.Restless && !a.IsAway).OrderBy(a => a.Id))
            yield return new($"restless:{a.Id}", Heroes, $"{a.Name} is restless",
                $"Loyalty {Math.Round(a.Hero!.Loyalty)}. A purse, a knighthood or a fief may win them back before they leave.", AgentId: a.Id);
    }

    private static IEnumerable<Alert> Homes(Town t)
    {
        var workers = t.Workers;
        int homeless = workers.Count(a => a.HomeId == null);
        if (homeless > 0)
            yield return new("homeless", Housing, $"{homeless} homeless", "Townsfolk with no roof lose heart and leave. More homes are needed.", AtTown: true);
        int idle = workers.Count(a => a.JobId == null);
        if (idle >= Math.Max(4, workers.Count / 5))
            yield return new("idle", Work, $"{idle} out of work", "Idle townsfolk drift away, and every one of them puts settlers off. Zone land for shops, workshops and farms.", AtTown: true);
        var draw = Population.Appeal(t);
        if (draw.Total < StalledAppeal)
        {
            var worst = draw.Parts.Where(p => p.Value < 0).OrderBy(p => p.Value).ThenBy(p => p.Label, StringComparer.Ordinal).Take(2)
                .Select(p => p.Label.ToLowerInvariant()).ToList();
            string why = worst.Count == 0 ? "" : $" Most of all: {string.Join(" and ", worst)}.";
            yield return new("settlers", Settlers, "Settlers stay away", $"Few newcomers will come while things stand as they are.{why} The City window shows every reason.");
        }
    }

    /// <summary>Below this appeal a small town gains a settler or two a week at most: a stall the player should hear about.</summary>
    private const double StalledAppeal = 0.15;

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
