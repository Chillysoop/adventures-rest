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
    Library,
    Armory,
    Institutions,
    Tourney,
}

/// <summary>A group of alerts: one icon on the strip, with a count and a hover list of what is in it.</summary>
public sealed record AlertKind(string Id, string Title, string Icon, AlertLevel Level, AlertWindow Window);

/// <summary>
/// One thing the player should act on. The key stays the same while it is the same problem, so a dismissed
/// alert stays dismissed until it is resolved and comes back. Site, agent or town aim the camera.
/// </summary>
public sealed record Alert(string Key, AlertKind Kind, string Text, string Tip, string? SiteId = null, int? AgentId = null, bool AtTown = false, int? StoryId = null, int? Wall = null, int? FindId = null, int? BuildingId = null);

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
    public static readonly AlertKind Sighted = new("sighted", "Raiders sighted", "warband", AlertLevel.Urgent, AlertWindow.War);
    public static readonly AlertKind Captives = new("captives", "Townsfolk taken", "heroes", AlertLevel.Urgent, AlertWindow.Bounties);
    public static readonly AlertKind Hunger = new("hunger", "Hunger", "bread", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Coffers = new("coffers", "Empty coffers", "gold", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Underfunded = new("underfunded", "Services go short", "gold", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Fear = new("fear", "Fear", "warband", AlertLevel.Urgent, AlertWindow.Campaign);
    public static readonly AlertKind Unrest = new("unrest", "Unrest", "mandate", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Hostile = new("hostile", "Hostile factions", "suspicion", AlertLevel.Urgent, AlertWindow.Faith);
    public static readonly AlertKind Motion = new("motion", "Council motions", "mandate", AlertLevel.Decision, AlertWindow.Council);
    public static readonly AlertKind Stores = new("stores", "Winter stores", "bread", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Fealty = new("fealty", "Demands for fealty", "warband", AlertLevel.Decision, AlertWindow.Campaign);
    public static readonly AlertKind Doom = new("doom", "End-game crises", "warband", AlertLevel.Urgent, AlertWindow.Campaign);
    public static readonly AlertKind Clash = new("clash", "Enemies clash", "warband", AlertLevel.Decision, AlertWindow.Campaign);
    public static readonly AlertKind Traitor = new("traitor", "A traitor on the council", "mandate", AlertLevel.Decision, AlertWindow.Council);
    public static readonly AlertKind Scholars = new("scholars", "Idle scholars", "d20", AlertLevel.Minor, AlertWindow.Library);
    public static readonly AlertKind Below = new("below", "Stirring below", "warband", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Trade = new("trade", "Trade routes", "gold", AlertLevel.Minor, AlertWindow.Diplomacy);
    public static readonly AlertKind Courted = new("courted", "Powers courted", "rivals", AlertLevel.Decision, AlertWindow.Diplomacy);
    public static readonly AlertKind Envoy = new("envoy", "Envoys waiting", "standing", AlertLevel.Decision, AlertWindow.Diplomacy);
    public static readonly AlertKind Neighbour = new("neighbour", "Neighbours in trouble", "favour", AlertLevel.Decision, AlertWindow.Diplomacy);
    public static readonly AlertKind Shadow = new("shadow", "Villages in shadow", "warband", AlertLevel.Decision, AlertWindow.Diplomacy);
    public static readonly AlertKind Plot = new("plot", "Villains' plans", "warband", AlertLevel.Decision, AlertWindow.Campaign);
    public static readonly AlertKind Charter = new("charter", "Charters", "favour", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind GrandWork = new("grandwork", "Grand works", "gold", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Crisis = new("crisis", "A crisis", "d20", AlertLevel.Urgent, AlertWindow.Story);
    public static readonly AlertKind Story = new("story", "Events", "d20", AlertLevel.Decision, AlertWindow.Story);
    public static readonly AlertKind Explorers = new("explorers", "Explorers' reports", "compass", AlertLevel.Decision, AlertWindow.Bounties);
    public static readonly AlertKind LevelUp = new("levelup", "Level up", "d20", AlertLevel.Decision, AlertWindow.Mayor);
    public static readonly AlertKind Bastion = new("bastion", "Bastions", "heroes", AlertLevel.Decision, AlertWindow.Companies);
    public static readonly AlertKind Institution = new("institution", "Institutions", "favour", AlertLevel.Decision, AlertWindow.Institutions);
    public static readonly AlertKind Heroes = new("heroes", "Restless heroes", "heroes", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Honours = new("honours", "Honours to give", "favour", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Housing = new("housing", "Housing", "homeless", AlertLevel.Minor, AlertWindow.City);
    public static readonly AlertKind Crowded = new("crowded", "Full to the doors", "population", AlertLevel.Minor, AlertWindow.None);
    public static readonly AlertKind Work = new("work", "Idle hands", "population", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Settlers = new("settlers", "No settlers", "population", AlertLevel.Decision, AlertWindow.City);
    public static readonly AlertKind Loot = new("loot", "Loot for sale", "gold", AlertLevel.Decision, AlertWindow.Armory);
    public static readonly AlertKind Gifts = new("gifts", "Arms to give", "heroes", AlertLevel.Minor, AlertWindow.Armory);
    public static readonly AlertKind Outpost = new("outpost", "Land to hold", "warband", AlertLevel.Minor, AlertWindow.None);
    public static readonly AlertKind Stalled = new("stalled", "Building stalled", "gold", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Lurker = new("lurker", "A stranger watching", "suspicion", AlertLevel.Urgent, AlertWindow.None);
    public static readonly AlertKind Tourney = new("tourney", "The tourney", "favour", AlertLevel.Decision, AlertWindow.Tourney);
    public static readonly AlertKind Strike = new("strike", "A guild on strike", "population", AlertLevel.Urgent, AlertWindow.Council);
    public static readonly AlertKind Guild = new("guild", "Guild demands", "favour", AlertLevel.Decision, AlertWindow.Council);
    public static readonly AlertKind Balance = new("union", "Guilds and mages", "favour", AlertLevel.Decision, AlertWindow.Council);
    public static readonly AlertKind Wager = new("wager", "Tourney bets", "gold", AlertLevel.Minor, AlertWindow.Tourney);


    /// <summary>Every kind, in the order their icons sit on the strip within a level.</summary>
    public static readonly IReadOnlyList<AlertKind> Kinds =
        [Siege, Sighted, Lurker, Captives, Doom, Crisis, Fear, Hunger, Coffers, Underfunded, Unrest, Strike, Hostile, Motion, Guild, Balance, Fealty, Clash, Traitor, Plot, Stores, Story, Explorers, LevelUp, Charter, GrandWork, Tourney, Bastion, Institution, Heroes, Honours, Courted, Envoy, Neighbour, Shadow, Loot, Stalled, Work, Settlers, Below, Scholars, Trade, Wager, Gifts, Outpost, Housing, Crowded];

    /// <summary>The standing rules, each naming what needs the player now. No rng and no writes: the client calls them often.</summary>
    public static readonly IReadOnlyList<Func<Town, IEnumerable<Alert>>> Rules = [War, Events, Food, Money, Council, Trades, Hostility, Winter, Growth, Routes, Region, Courting, Undercity, Study, Loyalty, Armoury, Homes, Sites, Crowds, Keepers, Outposts, Lists];

    public static List<Alert> Standing(Town town) => Rules.SelectMany(r => r(town)).ToList();

    private static string Days(int n) => n == 1 ? "1 day" : $"{n} days";

    /// <summary>An enemy's envoys at a minor power's door: outbid them with gifts before it answers.</summary>
    private static IEnumerable<Alert> Courting(Town t)
    {
        foreach (var p in t.Powers.Where(p => p.Envoy != "").OrderBy(p => p.Id, StringComparer.Ordinal))
            yield return new($"envoy:{p.Id}", Envoy, $"Envoy from {Powers.Name(t, p)}",
                $"{Cap(Powers.Name(t, p))} {Powers.EnvoyText(t, p)}. Answer in the Diplomacy window within {Days(Math.Max(1, p.EnvoyUntil - t.Day))}.");
        foreach (var p in t.Powers.Where(p => p.CourtedBy != null).OrderBy(p => p.Id, StringComparer.Ordinal))
        {
            var m = Powers.Suitor(t, p);
            if (m == null || Powers.Resolve(t, p) >= p.Offer) continue;
            int days = Math.Max(1, p.DecideDay - t.Day);
            yield return new($"courted:{p.Id}", Courted, $"{Cap(m.Name)} courts {Powers.Name(t, p)}",
                $"{Cap(Powers.Name(t, p))} answer in {Days(days)}. Their standing is {Js.Str(Js.Round(Powers.Resolve(t, p)))} against {m.Name}'s offer of {Js.Str(p.Offer)}: send gifts from the Diplomacy window to outbid it, or they join your enemy.");
        }
    }

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
        // A band someone saw on its way: a night to put guards over its mark.
        foreach (var g in t.Sightings.Where(g => g.Kind == Sightings.Band && g.TargetId != null).OrderBy(g => g.Id))
        {
            if (t.Buildings.FirstOrDefault(b => b.Id == g.TargetId) is not { } mark) continue;
            bool guarded = Garrison.Covered(t, mark);
            yield return new($"sighted:{g.Id}", Sighted, $"{Cap(g.Foe)} heading for {mark.Name}",
                guarded
                    ? $"{Cap(g.Reporter)} saw about {g.Count} {g.Foe} coming. The watch's flag covers {mark.Name}; keep it there tonight."
                    : t.Watch == 0
                        // No watch yet means no flag to plant: don't send the player to a button that can't work.
                        ? $"{Cap(g.Reporter)} saw about {g.Count} {g.Foe} coming for {mark.Name}. They strike tonight. The town has no watch yet, so keep heroes home tonight."
                        : $"{Cap(g.Reporter)} saw about {g.Count} {g.Foe} coming for {mark.Name}. They strike tonight. Plant a guard flag over it (G on the map) or keep heroes home.",
                BuildingId: mark.Id);
        }
        // An enemy's agent loitering in town until dark: find them in the crowd and have them seized.
        foreach (var l in t.Lurkers.OrderBy(l => l.Id))
        {
            int hours = Math.Max(1, l.StrikeTick - t.Tick);
            yield return new($"lurker:{l.Id}", Lurker, $"A stranger by {Lurkers.Where(t, l)}",
                $"Folk point out {l.Disguise} near {Lurkers.Where(t, l)}, watching. Find them in the crowd there (the hooded figure) and click them to have the watch seize them, before they act after dark in {hours} {(hours == 1 ? "hour" : "hours")}.",
                BuildingId: l.BuildingId, Wall: l.BuildingId == null ? 0 : null);
        }
        foreach (var c in Systems.Captives.Held(t).OrderBy(c => c.Id))
        {
            if (Quests.Site(t, c.SiteId) is not { } s) continue;
            int days = Math.Max(1, (int)Math.Ceiling((c.DueTick - t.Tick) / 24.0));
            bool posted = t.Bounties.Any(b => b.SiteId == c.SiteId && Quests.IsActive(b));
            yield return new($"captives:{c.Id}", Captives, $"{c.Captives.Count} held at {s.Def.Name}, {Days(days)}",
                posted
                    ? $"A rescue is posted. If no party brings them home in {Days(days)}, they are lost."
                    : $"Raiders carried {(c.Captives.Count == 1 ? "a townsperson" : $"{c.Captives.Count} townsfolk")} off. Post a rescue bounty on {s.Def.Name} in the Bounties window before {Days(days)} run out.",
                SiteId: s.Def.Id);
        }
        // A crisis the heroes can still avert by breaking its seat; a host already marching is a siege.
        foreach (var c in Crises.Active(t).Where(c => c.Phase != CrisisPhase.Marching).OrderBy(c => c.Id))
        {
            var m = Crises.MenaceOf(t, c);
            var seat = Quests.Site(t, m.SeatId);
            string where = seat == null || seat.Known == SiteKnowledge.Unheard ? $"its seat in {m.Region}" : seat.Def.Name;
            yield return new($"doom:{c.Id}", Doom, $"{Cap(Crises.Title(t, c))} in {Days(Crises.DaysLeft(t, c))}",
                $"{Cap(m.Name)}'s {Crises.SeverityOf(t, c).Name} crisis strikes at the town until its host marches on the walls. Break {where} first: post a bounty in the Campaign window.", SiteId: seat?.Def.Id);
        }
        foreach (var x in Crises.Pending(t).OrderBy(x => x.Id))
            yield return new($"clash:{x.Id}", Clash, $"Battle at {x.Where} in {Days(Math.Max(1, x.Day - t.Day))}",
                $"{Cap(t.Saga.Menaces[x.A - 1].Name)} and {t.Saga.Menaces[x.B - 1].Name} are about to fight. Ride out against one of them or keep behind the walls, in the Campaign window.");
        foreach (var (mole, a, b) in Infiltration.Whispered(t))
            yield return new($"traitor:{mole.Id}", Traitor, $"{a.GivenName} or {b.GivenName}?",
                $"The taverns say {a.Name} or {b.Name} serves {mole.By}. Name one before the council in the Council window, or wait for the watch to find proof while the traitor works.");
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
        // Services that cost more than the town takes in: unrest is coming, or already here.
        if (Taxes.Funding(t) < 1)
            yield return new("underfunded", Underfunded, t.Taxes.Unrest >= 1 ? $"Unrest {Math.Round(t.Taxes.Unrest)}" : "Services cost more than taxes bring",
                $"The town's services cost {Math.Round(Taxes.Services(t))}g a day; taxes, rents and trade bring {Math.Round(Taxes.Income(t))}g. Raise taxes in the Council window, or find more income, before the townsfolk take to the streets.");
    }

    private static IEnumerable<Alert> Council(Town t)
    {
        if (Systems.Council.PendingMotion(t) is { } m)
            yield return new($"motion:{m.Id}", Motion, $"Council votes in {Days(Systems.Council.DaysLeft(t, m))}",
                $"The council moves that {m.Text}. Sign or veto it in the Council window, or the council's vote settles it.");
        var d = t.Content.Saga.Dread;
        if (t.Saga.Dread >= d.WarnAt && t.Outcome == null)
        {
            string worst = Saga.WorstFear(t) is { } wm ? $" {Cap(wm.Name)} frightens them most." : "";
            yield return Saga.AbandonIn(t) is int left
                ? new("fear", Fear, $"Town gives up in {Days(left)}", $"Fear is at {Math.Round(t.Saga.Dread)}. If it stays this high for {Days(left)} more, the townsfolk abandon {t.Name} and the campaign is lost.{worst} Break a menace from the Campaign window, win a fight, or fund the temples to lift spirits.", AtTown: true)
                : new("fear", Fear, $"Fear {Math.Round(t.Saga.Dread)} of {Math.Round(d.AbandonAt)}", $"The town is losing heart. If fear reaches {Math.Round(d.AbandonAt)} and stays there for {Days(d.AbandonDays)}, the townsfolk abandon {t.Name}.{worst} Break a menace from the Campaign window, win a fight, or fund the temples to lift spirits.", AtTown: true);
        }
        if (t.Mandate < 30)
            yield return new("mandate", Unrest, "The town is turning on you", $"Mandate {Math.Round(t.Mandate)}. Below this the council stops listening.");
    }

    /// <summary>A trade guild on strike, or pressing a demand with a deadline.</summary>
    private static IEnumerable<Alert> Trades(Town t)
    {
        var r = Guilds.Rules(t);
        foreach (var g in t.Guilds)
        {
            string name = Cap(Guilds.Name(t, g));
            if (Guilds.OnStrike(t, g))
                yield return new($"strike:{g.Id}", Strike, $"{name} on strike for {Days(g.StrikeUntil - t.Day)}",
                    $"Its workers have downed tools. In the Council window pay it off ({Js.Str(Guilds.PayCost(t, g))}g) or send the watch to break the strike (costs {Js.Str(r.BreakMandate)} Mandate).",
                    AgentId: g.MasterId);
            else if (Guilds.Want(t, g) is { } w)
                yield return new($"guild:{g.Id}:{g.Due}", Guild, $"{name} wants {Guilds.WantText(t, g, w)}",
                    $"The guild gives you {Days(Math.Max(1, g.Due - t.Day))}. Meet it from the Council window or it takes it ill{(g.Standing - r.Lapsed <= r.StrikeAt ? " and strikes" : "")}.",
                    AgentId: g.MasterId);
        }
        // The guilds and the mages out of balance: hear the slighted side out before it acts.
        var ur = Union.Rules(t);
        if (t.Union.Formed && !Union.Sided(t) && Math.Abs(t.Union.Bar) >= ur.Lean)
        {
            bool mages = t.Union.Bar > 0;
            yield return new("union", Balance, mages ? "The guilds feel slighted by the mages" : "The mages feel slighted by the guilds",
                $"{Union.Mood(t)}. In the Council window hear the {(mages ? "guilds" : "mages")} out ({Js.Str(Union.HearCost(t))}g), or ease off what pulls it {(mages ? "toward the mages (research funding, hedge mages)" : "toward the guilds")}.");
        }
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

    private static IEnumerable<Alert> Study(Town t)
    {
        if (t.Research.Study != null) yield break;
        var open = Research.Open(t);
        if (open.Count == 0) yield break;
        // Only once the town can pay for one: a study it can't afford isn't an action yet.
        if (!open.Any(d => (t.Research.Progress.ContainsKey(d.Id) ? 0 : Research.Commission(t, d)) <= t.Treasury)) yield break;
        yield return new("scholars", Scholars, "The scholars are idle",
            $"{open.Count} {(open.Count == 1 ? "discovery is" : "discoveries are")} ready to study, {open[0].Name.ToLowerInvariant()} the cheapest. Set the scholars to work in the Library window.");
    }

    /// <summary>A friend in the wilds whose need the town can meet now, and a villain's plan close to done.</summary>
    private static IEnumerable<Alert> Region(Town t)
    {
        foreach (var s in t.Settlements.Where(x => x.Standing >= 10 && x.Trouble is { Severity: >= 40 } && Neighbours.AidRefused(t, x) == null).OrderBy(x => x.Id))
            yield return new($"neighbour:{s.Id}", Neighbour, $"{s.Name} needs help",
                $"{Cap(s.People)} face {Neighbours.TroubleOf(t, s)!.Name}. Send aid from the Diplomacy window before it does its worst; they will remember who helped.");
        // A people paying an enemy tribute (or ruled by its creature): one to win back.
        foreach (var s in t.Settlements.Where(x => Shadows.Paying(x) && t.Regions[x.RegionId].Known).OrderBy(x => x.Id))
        {
            if (Shadows.Over(t, s) is not { } m) continue;
            string source = s.Shadow!.SourceId is string sid && Quests.Site(t, sid) is { } site ? $" or clear {site.Def.Name}" : "";
            yield return new($"shadow:{s.Id}", Shadow, $"{s.Name} pays {m.Name}",
                $"{Cap(s.People)} send tribute to {m.Name} and no longer trade with the town. Send a hero to rally them or an envoy from the Diplomacy window{source} to win them back.",
                SiteId: s.Shadow.SourceId);
        }
        foreach (var m in Plots.Looming(t))
        {
            var p = m.Plan!;
            int days = Math.Max(0, p.DueDay - t.Day);
            var seat = Quests.Site(t, m.SeatId);
            yield return new($"plot:{m.Id}:{p.Def}", Plot, $"Stop {m.Leader}'s plan",
                $"{Plots.Describe(t, m)} It comes to a head in {Days(days)}. Clear its seat or one of its holdings to set it back; post a bounty in the Campaign window.", SiteId: seat?.Def.Id);
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
        foreach (var o in Systems.Loyalty.Honours(t))
            yield return new($"honours:{o.Key}", Honours, o.Title, o.Tip, SiteId: o.SiteId, AgentId: o.AgentId);
    }

    /// <summary>Loot offered that a hero in town would use, and armory stock a hero in town would.</summary>
    private static IEnumerable<Alert> Armoury(Town t)
    {
        foreach (var o in t.Armory.Offers.Where(o => t.Treasury >= o.Price).OrderBy(o => o.Id))
        {
            var taker = Armory.Takers(t, o.Item).FirstOrDefault(x => x.Hero.Id != o.HeroId).Hero;
            if (taker == null) continue;
            int days = Math.Max(1, o.Until - t.Day);
            yield return new($"loot:{o.Id}", Loot, $"{Cap(o.Seller)} offers the {Gear.Plainly(o.Item)}",
                $"{Js.Str(o.Price)}g for the town armory; {taker.GivenName} could use it. Buy it in the Armory window; in {Days(days)} it is sold elsewhere.", AgentId: o.HeroId);
        }
        foreach (var s in t.Armory.Stock.OrderBy(s => s.Id))
            if (Armory.Takers(t, s.Item).FirstOrDefault().Hero is { } a)
                yield return new($"gift:{s.Id}:{a.Id}", Gifts, $"Give the {Gear.Plainly(s.Item)} to {a.GivenName}",
                    Armory.IsStone(t, s.Item) ? $"With a sending stone their company can send word home without a caster's spell slot. Give it in the Armory window." : $"It would serve {a.Name} better than what they carry. Give it in the Armory window.", AgentId: a.Id);
    }

    /// <summary>Taverns, shrines, bathhouses and the like so full yesterday that townsfolk were turned away at the door, while the player can add room.</summary>
    private static IEnumerable<Alert> Crowds(Town t)
    {
        foreach (var (kind, away, busiest) in Systems.Crowds.Crowded(t))
        {
            string what = t.Content.Crowds.Names.TryGetValue(kind, out var n) ? n : kind;
            string service = busiest.Def.Serves.Keys.Order(StringComparer.Ordinal).FirstOrDefault() ?? kind;
            string? fix;
            var aim = busiest;
            if (busiest.Def.IsBusiness && !busiest.Def.Civic && !busiest.Def.Holy)
                fix = $"Townsfolk open another {busiest.Def.Name.ToLowerInvariant()} where there is commercial land: paint some from the Build bar (B) near the crowd.";
            else if (kind == Systems.Crowds.Holy)
            {
                // Houses of the gods grow into the temple, its nave, then the cathedral.
                var temple = Shrines.Temple(t);
                var nave = t.Content.Buildings.FirstOrDefault(d => d.PartOf == t.Content.Shrines.Temple && d.Capacity > 0);
                var cathedral = t.Content.Buildings.FirstOrDefault(d => d.Id == "cathedral");
                if (temple == null)
                    fix = Shrines.TempleBlocked(t) is null or "not enough gold" ? "Build the temple from the Faith window: it holds many more." : null;
                else if (nave != null && Monuments.Blocked(t, temple, nave) is null or "not enough gold")
                {
                    fix = $"Add the {nave.Name.ToLowerInvariant()} to {temple.Name}: select the temple to build onto it.";
                    aim = temple;
                }
                else fix = cathedral != null && Planning.Blocked(t, cathedral) is null or "not enough gold" ? "Raise the cathedral from the Build bar (B)." : null;
            }
            else
                fix = Planning.Blocked(t, busiest.Def) is null or "not enough gold"
                    ? $"Raise another {busiest.Def.Name.ToLowerInvariant()} from the Build bar (B)."
                    : null;
            if (fix == null) continue;
            yield return new($"crowded:{kind}", Crowded, $"{what} full",
                $"{away} townsfolk found {what.ToLowerInvariant()} full yesterday and had to think again, and the houses that want {City.ServiceName(t, service)} grow slower. {fix}",
                BuildingId: aim.Id);
        }
    }

    private static IEnumerable<Alert> Homes(Town t)
    {
        var workers = t.Workers;
        int homeless = workers.Count(a => a.HomeId == null);
        if (homeless > 0)
        {
            // Townsfolk build their own homes, but only on residential land: say whether there is any left to build on.
            string room = Planning.VacantZoned(t, Zone.Residential) == 0
                ? "Every residential plot is taken: paint more residential land from the Build bar (B) and they will build homes on it."
                : "Homes are going up on the residential land; painting more lets more go up at once.";
            yield return new("homeless", Housing, $"{homeless} homeless", $"Townsfolk with no roof lose heart and leave. {room}", AtTown: true);
        }
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

    /// <summary>A council site standing idle for want of timber or stone the market hasn't got.</summary>
    private static IEnumerable<Alert> Sites(Town t)
    {
        if (t.Defenses.WallsShort >= t.Day - 1)
            yield return new("stalled:walls", Stalled, "The walls wait for timber and stone",
                "The masons on the walls stand idle half the day: the market is short of timber and stone. Zone industry for a quarry and a lumber camp, or wait for the market to fill.");
        foreach (var b in t.Buildings)
        {
            if (!Construction.Stalled(t, b)) continue;
            var (timber, stone) = Construction.Wanting(b);
            string what = timber > 0 && stone > 0 ? "timber and stone" : stone > 0 ? "stone" : "timber";
            string from = stone > 0 && !t.Buildings.Any(q => q.Complete && q.Def.Id == "quarry") ? "There is no quarry: zone industry for one, or" :
                timber > 0 && !t.Buildings.Any(q => q.Complete && q.Def.Id == "lumber_camp") ? "There is no lumber camp: zone industry for one, or" : "Wait for the market to fill, or";
            yield return new($"stalled:{b.Id}", Stalled, $"{Cap(b.Name)} waits for {what}",
                $"The builders stand idle: the market has no {what} left. {from} send for it by caravan from the site ({Js.Str(Construction.CaravanCost(t, b))}g).", BuildingId: b.Id);
        }
    }

    /// <summary>A cleared site the town can afford to garrison before something moves back in. The watch waits for the word.</summary>
    private static IEnumerable<Alert> Outposts(Town t)
    {
        if (Lairs.WorthHolding(t) is not { } s) yield break;
        var o = t.Content.Lairs.Outpost;
        yield return new($"outpost:{s.Def.Id}", Outpost, $"Hold {s.Def.Name}?",
            $"{s.Def.Name} is cleared, and something will move back in unless the town holds it. A garrison of the watch costs {Js.Str(o.Cost)} gold and {Js.Str(o.Upkeep)} a day; send it from the site's panel.", SiteId: s.Def.Id);
    }

    /// <summary>An institution waiting on the council's choice, or a keeper whose patience has run out.</summary>
    private static IEnumerable<Alert> Keepers(Town t)
    {
        foreach (var s in t.Institutions)
        {
            var d = Institutions.DefOf(t, s);
            if (s.Shut) continue;
            if (s.Choice == "" && d.Choice != null)
                yield return new($"institution:{d.Id}", Institution, $"Decide {d.Choice.Label}",
                    $"{Cap(d.Name)} is open and {Institutions.KeeperName(t, s)} is waiting on the council. {d.Choice.Text} Choose in the Institutions window.");
            else if (s.Regard <= -40)
                yield return new($"keeper:{d.Id}", Institution, $"{Institutions.KeeperName(t, s)} is bitter",
                    $"{Cap(d.Name)} gives the town little while its keeper feels like this. Fund it more generously in the Institutions window, or answer its next request.");
        }
    }

    /// <summary>Below this appeal a small town gains a settler or two a week at most: a stall the player should hear about.</summary>
    private const double StalledAppeal = 0.15;

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>The council's tourney waits on the mayor's purse; once it is on, the next event still open to a stake.</summary>
    private static IEnumerable<Alert> Lists(Town t)
    {
        var st = t.Tourney;
        if (st.Stage == TourneyStage.Proposed)
        {
            int days = Math.Max(1, st.OpenDay - t.Day);
            yield return new("tourney:fund", Tourney, $"Fund the tourney ({Days(days)})",
                $"The council has proposed the year's tourney. Pay for it modest, grand or legendary, or let it go and disappoint the town. Left unanswered, the council holds a modest one if the treasury can bear it.");
        }
        else if (st.Stage == TourneyStage.Running && st.Events.Select((e, i) => (e, i)).FirstOrDefault(x => !x.e.Started && x.e.Day <= t.Day + 1 && Tournaments.BetBlocked(t, x.e) == null) is ({ } next, int index))
            yield return new($"tourney:bet:{index}", Wager, $"Bet on {Tournaments.DefOf(t, next).Name}",
                $"{Cap(Tournaments.DefOf(t, next).Name)} is ridden {(next.Day == t.Day ? "today" : "tomorrow")}. Open the tourney card to see the draw and the odds, and stake the treasury's gold on a rider if you like.");
    }
}
