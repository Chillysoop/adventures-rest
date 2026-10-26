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
    Defence,
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
    Army,
    Notables,
}

/// <summary>A group of alerts: one icon on the strip, with a count and a hover list of what is in it.</summary>
/// <summary>
/// A kind of alert. <paramref name="Act"/> is where in its window it is answered, which the client opens, scrolls to and
/// lights in one click: a page and the start of a button's words or hover or of a heading ("Hall>Sign"), either alone
/// ("Taxes", "Growth&gt;"); "" opens only the window.
/// </summary>
public sealed record AlertKind(string Id, string Title, string Icon, AlertLevel Level, AlertWindow Window, string Act = "");

/// <summary>
/// One thing the player should act on. The key stays the same while it is the same problem, so a dismissed
/// alert stays dismissed until it is resolved and comes back. Site, agent or town aim the camera.
/// </summary>
public sealed record Alert(string Key, AlertKind Kind, string Text, string Tip, string? SiteId = null, int? AgentId = null, bool AtTown = false, int? StoryId = null, int? Wall = null, int? FindId = null, int? BuildingId = null, LibItem? Item = null);

/// <summary>
/// The player's to-do list, in the manner of Paradox games. Standing alerts are read off the town each
/// time (nothing here is saved): an alert lasts while its rule names it, so resolving the problem clears
/// it. Each alert is an action for the player, Paradox style: news belongs in the Ledger and the feed, not
/// here. To raise a new alert, add a kind below and a
/// rule to <see cref="Rules"/> (or a line in an existing rule); the client groups, orders and aims it.
/// </summary>
public static class Alerts
{
    public static readonly AlertKind Siege = new("siege", "Under attack", "warband", AlertLevel.Urgent, AlertWindow.Defence, "Walls");
    public static readonly AlertKind Sighted = new("sighted", "Raiders sighted", "warband", AlertLevel.Urgent, AlertWindow.Defence, "Town guard");
    public static readonly AlertKind Overdue = new("overdue", "Overdue", "compass", AlertLevel.Decision, AlertWindow.Bounties);
    public static readonly AlertKind Captives = new("captives", "Townsfolk taken", "heroes", AlertLevel.Urgent, AlertWindow.Bounties);
    public static readonly AlertKind Hunger = new("hunger", "Hunger", "bread", AlertLevel.Urgent, AlertWindow.City);
    public static readonly AlertKind Coffers = new("coffers", "Empty coffers", "gold", AlertLevel.Urgent, AlertWindow.Council, "Hall>Taxes");
    public static readonly AlertKind Underfunded = new("underfunded", "Services go short", "gold", AlertLevel.Urgent, AlertWindow.Council, "Hall>Taxes");
    public static readonly AlertKind Fear = new("fear", "Fear", "warband", AlertLevel.Urgent, AlertWindow.Campaign, "Foes>Dread");
    public static readonly AlertKind Unrest = new("unrest", "Unrest", "mandate", AlertLevel.Urgent, AlertWindow.Council, "Hall>Hold a festival");
    public static readonly AlertKind Hostile = new("hostile", "Hostile factions", "suspicion", AlertLevel.Urgent, AlertWindow.Faith, "Factions");
    public static readonly AlertKind Motion = new("motion", "Council motions", "mandate", AlertLevel.Decision, AlertWindow.Council, "Sign");
    public static readonly AlertKind Stores = new("stores", "Winter stores", "bread", AlertLevel.Decision, AlertWindow.City, "Growth>");
    public static readonly AlertKind Fealty = new("fealty", "Demands for fealty", "warband", AlertLevel.Decision, AlertWindow.Campaign, "Foes>Threats in the land");
    public static readonly AlertKind Doom = new("doom", "End-game crises", "warband", AlertLevel.Urgent, AlertWindow.Campaign, "Foes>Crises");
    public static readonly AlertKind Gathering = new("horde", "The host gathers", "warband", AlertLevel.Urgent, AlertWindow.Campaign, "Foes>Threats in the land");
    public static readonly AlertKind DarkQuest = new("darkquest", "The big bad's quest", "warband", AlertLevel.Urgent, AlertWindow.Campaign, "Foes>Threats in the land");
    public static readonly AlertKind Road = new("road", "A road to victory", "favour", AlertLevel.Decision, AlertWindow.Campaign, "Victory>Roads to victory");
    public static readonly AlertKind Clash = new("clash", "Enemies clash", "warband", AlertLevel.Decision, AlertWindow.Campaign, "Foes>Threats in the land");
    public static readonly AlertKind Traitor = new("traitor", "A traitor on the council", "mandate", AlertLevel.Decision, AlertWindow.Council, "Hall>Before the council");
    public static readonly AlertKind Scholars = new("scholars", "Idle scholars", "d20", AlertLevel.Minor, AlertWindow.Library, "Study>The scholars");
    public static readonly AlertKind Below = new("below", "Stirring below", "warband", AlertLevel.Decision, AlertWindow.City, "Homes>Under the streets");
    public static readonly AlertKind Trade = new("trade", "Trade routes", "gold", AlertLevel.Minor, AlertWindow.Diplomacy, "Trade routes");
    public static readonly AlertKind Courted = new("courted", "Powers courted", "rivals", AlertLevel.Decision, AlertWindow.Diplomacy, "Peoples of the wilds");
    public static readonly AlertKind Envoy = new("envoy", "Envoys waiting", "standing", AlertLevel.Decision, AlertWindow.Diplomacy, "Peoples of the wilds");
    public static readonly AlertKind Neighbour = new("neighbour", "Neighbours in trouble", "favour", AlertLevel.Decision, AlertWindow.Diplomacy, "Peoples of the wilds");
    public static readonly AlertKind Shadow = new("shadow", "Villages in shadow", "warband", AlertLevel.Decision, AlertWindow.Diplomacy, "Peoples of the wilds");
    public static readonly AlertKind Plot = new("plot", "Villains' plans", "warband", AlertLevel.Decision, AlertWindow.Campaign, "Foes>Threats in the land");
    public static readonly AlertKind Charter = new("charter", "Charters", "favour", AlertLevel.Decision, AlertWindow.City, "Claim");
    public static readonly AlertKind GrandWork = new("grandwork", "Grand works", "gold", AlertLevel.Decision, AlertWindow.City, "Growth>Grand works");
    public static readonly AlertKind Crisis = new("crisis", "A crisis", "d20", AlertLevel.Urgent, AlertWindow.Story);
    public static readonly AlertKind Story = new("story", "Events", "d20", AlertLevel.Decision, AlertWindow.Story);
    public static readonly AlertKind Explorers = new("explorers", "Explorers' reports", "compass", AlertLevel.Decision, AlertWindow.Bounties, "Exploring");
    public static readonly AlertKind Fallen = new("fallen", "The mayor is dead", "face-death-skull", AlertLevel.Urgent, AlertWindow.Mayor);
    public static readonly AlertKind LevelUp = new("levelup", "Level up", "d20", AlertLevel.Decision, AlertWindow.Mayor);
    public static readonly AlertKind Bastion = new("bastion", "Bastions", "heroes", AlertLevel.Decision, AlertWindow.Companies, "Bastions");
    public static readonly AlertKind Institution = new("institution", "Institutions", "favour", AlertLevel.Decision, AlertWindow.Institutions);
    public static readonly AlertKind Heroes = new("heroes", "Restless heroes", "heroes", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Honours = new("honours", "Honours to give", "favour", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Housing = new("housing", "Housing", "homeless", AlertLevel.Minor, AlertWindow.City, "Homes>Houses");
    public static readonly AlertKind Rooms = new("rooms", "No rooms", "heroes", AlertLevel.Minor, AlertWindow.City);
    public static readonly AlertKind Camp = new("camp", "Refugees", "homeless", AlertLevel.Minor, AlertWindow.City);
    public static readonly AlertKind Crowded = new("crowded", "Full to the doors", "population", AlertLevel.Minor, AlertWindow.None);
    public static readonly AlertKind Work = new("work", "Idle hands", "population", AlertLevel.Decision, AlertWindow.City, "Growth>Why settlers");
    public static readonly AlertKind Settlers = new("settlers", "No settlers", "population", AlertLevel.Decision, AlertWindow.City, "Growth>Why settlers");
    public static readonly AlertKind Loot = new("loot", "Loot for sale", "gold", AlertLevel.Decision, AlertWindow.Armory, "Offered to the town");
    public static readonly AlertKind Treasure = new("treasure", "A great find", "item-ring", AlertLevel.Decision, AlertWindow.Armory, "Items>In the armory");
    public static readonly AlertKind RareFind = new("rarefind", "Rare find", "item-ring", AlertLevel.Minor, AlertWindow.Armory, "Items>In the armory");
    public static readonly AlertKind Gifts = new("gifts", "Arms to give", "heroes", AlertLevel.Minor, AlertWindow.Armory, "In the armory");
    public static readonly AlertKind Outpost = new("outpost", "Land to hold", "warband", AlertLevel.Minor, AlertWindow.None);
    public static readonly AlertKind Tower = new("tower", "Tower tested", "warband", AlertLevel.Urgent, AlertWindow.None);
    public static readonly AlertKind Patrol = new("patrol", "Keep it clear", "order-guard", AlertLevel.Minor, AlertWindow.None);
    public static readonly AlertKind Conquered = new("conquered", "A town to settle", "gold", AlertLevel.Decision, AlertWindow.Army, "Allies");
    public static readonly AlertKind WarAsk = new("warask", "An ally asks for war", "warband", AlertLevel.Minor, AlertWindow.Army, "Allies");
    public static readonly AlertKind Holdout = new("holdout", "An ally holds out", "gold", AlertLevel.Decision, AlertWindow.Army, "Allies");
    public static readonly AlertKind Stalled = new("stalled", "Building stalled", "gold", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Bridge = new("bridge", "Bridge cut", "gold", AlertLevel.Decision, AlertWindow.Campaign, "Roads>Roads and ruins");
    public static readonly AlertKind Ruin = new("ruin", "Ruin to restore", "gold", AlertLevel.Decision, AlertWindow.Campaign, "Roads>Roads and ruins");
    public static readonly AlertKind Lurker = new("lurker", "A stranger watching", "suspicion", AlertLevel.Urgent, AlertWindow.None);
    public static readonly AlertKind Tourney = new("tourney", "The tourney", "favour", AlertLevel.Decision, AlertWindow.Tourney);
    public static readonly AlertKind Strike = new("strike", "A guild on strike", "population", AlertLevel.Urgent, AlertWindow.Council, "Guilds>Guilds");
    public static readonly AlertKind Guild = new("guild", "Guild demands", "favour", AlertLevel.Decision, AlertWindow.Council, "Guilds>Guilds");
    public static readonly AlertKind Balance = new("union", "Guilds and mages", "favour", AlertLevel.Decision, AlertWindow.Council, "Guilds>Guilds");
    public static readonly AlertKind Wager = new("wager", "Tourney bets", "gold", AlertLevel.Minor, AlertWindow.Tourney);
    public static readonly AlertKind Sick = new("sick", "Sickness", "sickness", AlertLevel.Urgent, AlertWindow.City, "Growth>Sickness");
    public static readonly AlertKind Spent = new("spent", "Traps spent", "trap-pitch", AlertLevel.Decision, AlertWindow.Defence, "Traps");
    public static readonly AlertKind Dark = new("dark", "The dark company rides", "dark-company", AlertLevel.Decision, AlertWindow.Campaign, "Foes>");
    public static readonly AlertKind Scry = new("scry", "Scry the fight", "compass", AlertLevel.Decision, AlertWindow.None);
    public static readonly AlertKind Ride = new("ride", "The last ride", "ui-renown", AlertLevel.Decision, AlertWindow.War, "Ride");
    public static readonly AlertKind Fields = new("fields", "Fields stripped", "food", AlertLevel.Decision, AlertWindow.Defence, "Gate");
    public static readonly AlertKind Unmanned = new("unmanned", "Walls unmanned", "defence", AlertLevel.Decision, AlertWindow.Defence, "Gate");
    public static readonly AlertKind Strangers = new("strangers", "Strangers at work", "suspicion", AlertLevel.Decision, AlertWindow.Defence, "Gate");
    public static readonly AlertKind Notable = new("notable", "Notables", "np-shaking-hands", AlertLevel.Decision, AlertWindow.Notables);


    /// <summary>Every kind, in the order their icons sit on the strip within a level.</summary>
    public static readonly IReadOnlyList<AlertKind> Kinds =
        [Siege, Sighted, Lurker, Captives, Overdue, Ride, Doom, Crisis, Sick, Fear, Hunger, Coffers, Underfunded, Unrest, Strike, Hostile, Motion, Guild, Balance, Fealty, Gathering, DarkQuest, Road, Clash, Traitor, Plot, Dark, Spent, Fields, Unmanned, Strangers, Stores, Story, Scry, Explorers, Fallen, LevelUp, Charter, GrandWork, Tourney, Bastion, Institution, Notable, Heroes, Honours, Courted, Envoy, Neighbour, Shadow, Treasure, Loot, Stalled, Bridge, Ruin, Work, Settlers, Below, Scholars, Trade, Wager, RareFind, Gifts, Tower, Patrol, Outpost, Housing, Rooms, Camp, Crowded, WarAsk];

    /// <summary>The standing rules, each naming what needs the player now. No rng and no writes: the client calls them often.</summary>
    public static readonly IReadOnlyList<Func<Town, IEnumerable<Alert>>> Rules = [War, Events, Food, Money, Council, Trades, Hostility, Winter, Growth, Routes, Region, Courting, Undercity, Study, Loyalty, Armoury, Homes, Sites, Allies, Word, Crowds, Keepers, Notables, Outposts, Lists, Brought, Missing, Frontiers, Treasures];

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
            yield return new("march", Siege, $"Siege in {Days(days)}", $"The warband is marching on the {Garrison.Place(t, t.Enemy.Approach)}. Call the watch to arms there and keep heroes home, or march the army out to meet it.", AtTown: true, Wall: t.Enemy.Approach);
        }
        if (t.SkyFleet.AirRaid is { } air && t.Content.Sky.AirRaids.Attackers.FirstOrDefault(a => a.Id == air.Attacker) is { } attacker)
        {
            int days = Math.Max(1, air.Day - t.Day);
            yield return new("airraid", Siege, $"{Cap(attacker.Name)} in {Days(days)}",
                "An attack from the air: walls are no help. Keep the airships moored, the watch on the towers and the heroes home, or strike its isle first.", SiteId: air.SiteId, AtTown: true);
        }
        if (t.Enemy.Stage == ThreatStage.Raids)
            yield return new("raids", Siege, "Raiders by night", "The warband raids the town when its heroes are away. Build walls, hire the watch, clear the war camp.");
        foreach (var m in t.Saga.Menaces.Where(m => m.Alive && Discovery.Known(t, m) && m.ArrivalTick is int).OrderBy(m => m.Id))
        {
            if (Horde.On(t, m))
            {
                // The big bad's host: no count of days, only what the town's eyes have seen.
                if (Horde.Known(t, m))
                    yield return new($"menace:{m.Id}", Siege, $"{Cap(m.Name)} {Horde.Word(t, m)}", $"Its host comes for the {Garrison.Place(t, m.Approach)}. Call the watch to arms there and the heroes home, or march the army out to meet it.", AtTown: true, Wall: m.Approach);
                continue;
            }
            int days = Math.Max(1, (int)Math.Ceiling((m.ArrivalTick!.Value - t.Tick) / 24.0));
            yield return new($"menace:{m.Id}", Siege, $"{Cap(m.Name)} in {Days(days)}", $"The host of {m.Name} marches from {m.SeatName} on the {Garrison.Place(t, m.Approach)}. Call the watch to arms there and the heroes home.", AtTown: true, Wall: m.Approach);
        }
        // The big bad's host gathering at its camps: thin it at its camps, and put eyes on them.
        if (Saga.BigBad(t) is { } big && Horde.On(t, big) && big.ArrivalTick == null && Horde.State(t).Stage is HordeStage.Gathering or HordeStage.Mustering)
        {
            var seat = Quests.Site(t, big.SeatId);
            string eyes = Horde.Seen(t, big) is double share
                ? $"Your eyes on its camps count it {(share >= 0.95 ? "full" : share >= 0.5 ? "more than half gathered" : "under half gathered")}."
                : "Nobody watches its camps: a tower, patrol, scout or scrying by them would show how big it grows and when it moves.";
            yield return new("horde", Gathering, $"{Cap(big.Name)} {Horde.Word(t, big)}", $"Raid {seat?.Def.Name ?? "its seat"} and its holdings to thin the host before it marches. {eyes}", SiteId: seat?.Def.Id);
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
            // The big bad's crisis has no count of days: its host marches when its camps are full or the crisis comes to a head.
            yield return new($"doom:{c.Id}", Doom, Horde.On(t, m) ? Cap(Crises.Title(t, c)) : $"{Cap(Crises.Title(t, c))} in {Days(Crises.DaysLeft(t, c))}",
                $"{Cap(m.Name)}'s {Crises.SeverityOf(t, c).Name} crisis strikes at the town until its host marches on the walls. Break {where} first: post a bounty on it from Bounties.", SiteId: seat?.Def.Id);
        }
        foreach (var x in Crises.Pending(t).OrderBy(x => x.Id))
            yield return new($"clash:{x.Id}", Clash, $"Battle at {x.Where} in {Days(Math.Max(1, x.Day - t.Day))}",
                $"{Cap(t.Saga.Menaces[x.A - 1].Name)} and {t.Saga.Menaces[x.B - 1].Name} are about to fight. Ride out against one of them or keep behind the walls, in the Campaign window.");
        foreach (var (mole, a, b) in Infiltration.Whispered(t))
            yield return new($"traitor:{mole.Id}", Traitor, $"{a.GivenName} or {b.GivenName}?",
                $"The taverns say {a.Name} or {b.Name} serves {mole.By}. Name one before the council in the Council window, or wait for the watch to find proof while the traitor works.");
        if (DarkQuests.Current(t) is { } dq && Quests.Site(t, dq.SiteId) is { } dqs)
            yield return new($"darkquest:{dq.Def}", DarkQuest, $"Stop {Saga.BigBad(t)!.Leader}'s quest",
                $"{DarkQuests.Describe(t, dq)} Clear {dqs.Def.Name} within {Days(DarkQuests.DaysLeft(t))} and it will not have it: post a bounty there.", SiteId: dqs.Def.Id);
        if (Saga.BigBad(t) is { Alive: true } bb)
            foreach (var d in Victory.Roads(t).Where(d => Victory.ClaimBlocked(t, d.Id) == null))
                yield return new($"road:{d.Id}", Road, $"Claim {d.Name}",
                    $"Every term of {d.Name} holds. Claim it in the Campaign window: {bb.Name} marches at once, and the terms must hold {Days(d.Hold)} more to win.");
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
        if (Mayoralty.Dead(t) && !t.Mayoralty.Hardcore)
            yield return new("fallen", Fallen, $"Mayor {t.Stories.Mayor.Name} is dead",
                $"{Cap(t.Mayoralty.Cause)}. Raise them at the temple, or name a new mayor, on the Mayor tab.");
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
                $"A loaf costs {price:0.00}g and the stores are thin. Paint more farmland from the Build bar (B). In autumn and winter, lay in grain from the City window.");
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
        // The Common Cause and the mages out of balance: hear the slighted side out before it acts.
        var ur = Union.Rules(t);
        if (t.Union.Formed && !Union.Sided(t) && Math.Abs(t.Union.Bar) >= ur.Lean)
        {
            bool mages = t.Union.Bar > 0;
            string cause = Stories.Cap(ur.Name);
            yield return new("union", Balance, mages ? $"{cause} feels slighted by the mages" : $"The mages feel slighted by {ur.Name}",
                $"{Union.Mood(t)}. In the Council window hear {(mages ? "the movement" : "the mages")} out ({Js.Str(Union.HearCost(t))}g), or ease off what pulls it {(mages ? "toward the mages (research funding, hedge mages outside the movement)" : "toward the movement")}.");
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
        yield return new("scholars", Scholars, "The scholars are idle",
            $"{open.Count} {(open.Count == 1 ? "discovery is" : "discoveries are")} ready to study, {open[0].Name.ToLowerInvariant()} the quickest. Set the scholars to work in the Library window.");
    }

    /// <summary>A friend in the wilds whose need the town can meet now, and a villain's plan close to done.</summary>
    private static IEnumerable<Alert> Region(Town t)
    {
        foreach (var s in t.Settlements.Where(x => x.Standing >= 10 && x.Trouble is { Severity: >= 40 } && Neighbours.AidRefused(t, x) == null).OrderBy(x => x.Id))
            yield return new($"neighbour:{s.Id}", Neighbour, $"{s.Name} needs help",
                $"{Cap(s.People)} face {Neighbours.TroubleOf(t, s)!.Name}. Send aid from the Diplomacy window before it does its worst; they will remember who helped.");
        // A people paying an enemy tribute (or ruled by its creature): one to win back.
        foreach (var s in t.Settlements.Where(x => Shadows.Paying(x) && Neighbours.Known(t, x)).OrderBy(x => x.Id))
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
                $"{Plots.Describe(t, m)} It comes to a head in {Days(days)}. Clear its seat or one of its holdings to set it back; post a bounty on it from Bounties.", SiteId: seat?.Def.Id);
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
        int homeless = workers.Count(a => a.Homeless);
        if (homeless > 0)
        {
            // Townsfolk build their own homes, but only on residential land: say whether there is any left to build on.
            string room = Planning.VacantZoned(t, Zone.Residential) == 0
                ? "Every residential plot is taken: paint more residential land from the Build bar (B) and they will build homes on it."
                : "Homes are going up on the residential land; painting more lets more go up at once.";
            yield return new("homeless", Housing, $"{homeless} homeless", $"Townsfolk with no roof lose heart and leave. {room}", AtTown: true);
        }
        int camping = t.Heroes.Count(a => a.Hero!.AwayUntil == null && !a.Hero.Lodging && a.Gold >= t.Content.Lodging.Price);
        if (camping > 0)
            yield return new("rooms", Rooms, $"{camping} {(camping == 1 ? "hero" : "heroes")} camping",
                $"No free room at the inns. Heroes who camp {t.Content.Lodging.CampNights} nights ride on, and travellers move on sooner. Commission an inn from the Build bar (B), or paint shops for townsfolk to open one.", AtTown: true);
        // Refugees about to give up on the camp: homes or work going up keeps them waiting.
        int day = t.Day, leaving = Refugees.Camped(t).Count(a => a.StayUntil - day <= 3);
        if (leaving > 0)
            yield return new("camp", Camp, $"{leaving} {(leaving == 1 ? "refugee" : "refugees")} leaving",
                "Refugees wait while homes and workplaces are going up. Paint homes and shops from the Build bar (B) and they settle.", AtTown: true);
        int idle = workers.Count(a => a.JobId == null && !a.Traveller);
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

    /// <summary>A council site standing idle for want of timber or stone the market hasn't got; a rope bridge raiders cut.</summary>
    private static IEnumerable<Alert> Sites(Town t)
    {
        foreach (var s in Ruins.Restorable(t))
            if (Ruins.Of(t, s) is { } k && t.Treasury >= k.Cost)
                yield return new($"ruin:{s.Def.Id}", Ruin, $"{Cap(s.Def.Name)} can be restored",
                    $"The company cleared {s.Def.Name}. Restore it for {Js.Str(k.Cost)}g and it {k.Gives}. Restore it from its card on the map or the Campaign window.", SiteId: s.Def.Id);
        foreach (var f in Bridges.CutBridges(t).OrderBy(f => f.Id))
            yield return new($"bridge:{f.Id}", Bridge, $"{Cap(Bridges.Name(f))} is cut",
                $"Raiders cut the rope bridge over {f.Name}, and companies must go the long way round. Rebuild it in stone from the Campaign window ({Js.Str(t.Content.Roads.Bridges.Stone)}g) and it can never be cut again.");
        if (t.Defenses.WallsShort >= t.Day - 1)
            yield return new("stalled:walls", Stalled, "The walls wait for timber and stone",
                "The masons on the walls stand idle half the day: the market is short of timber and stone. Zone industry for a quarry and a lumber camp, or wait for the market to fill.");
        foreach (var b in t.Buildings)
        {
            // Townsfolk's sites wait on the market or send away for their own; the build bar says why zones sit idle.
            if (b.OwnerId != null || !Construction.Stalled(t, b)) continue;
            var (timber, stone) = Construction.Wanting(b);
            string what = timber > 0 && stone > 0 ? "timber and stone" : stone > 0 ? "stone" : "timber";
            string from = stone > 0 && !t.Buildings.Any(q => q.Complete && q.Def.Id == "quarry") ? "There is no quarry: zone industry for one, or" :
                timber > 0 && !t.Buildings.Any(q => q.Complete && q.Def.Id == "lumber_camp") ? "There is no lumber camp: zone industry for one, or" : "Wait for the market to fill, or";
            yield return new($"stalled:{b.Id}", Stalled, $"{Cap(b.Name)} waits for {what}",
                $"The builders stand idle: the market has no {what} left. {from} send for it by caravan from the site ({Js.Str(Construction.CaravanCost(t, b))}g).", BuildingId: b.Id);
        }
    }

    /// <summary>The frontier: fields stripped beyond the walls, a wall with too few on it, strangers robbing an open town.</summary>
    private static IEnumerable<Alert> Frontiers(Town t)
    {
        if (Saga.Champions(t, out var foe, out var seat) is { } champions)
            yield return new("ride", Ride, $"Ride for {seat!.Def.Name}",
                $"{Expedition.ListNames(champions.Select(h => h.GivenName).ToList())} are strong enough to face {foe!.Leader} at {seat.Def.Name}. Send them from the War window: the killing blow is yours to order.", SiteId: seat.Def.Id);
        var f = t.Frontier;
        if (f.FieldsLost > 0)
            yield return new("fields", Fields, $"{f.FieldsLost} {(f.FieldsLost == 1 ? "field" : "fields")} stripped",
                $"Raiders and beasts took {Js.Str(Js.Round(f.FoodLost))} food from fields beyond the walls last night. Plant a guard flag over the fields (G on the map): a field with a guard by it is left alone.");
        if (Frontier.Walled(t) && Frontier.Manned(t) < t.Content.Frontier.WantManned)
            yield return new("unmanned", Unmanned, $"Walls {Js.Str(Js.Round(Frontier.Manned(t) * 100))}% manned",
                $"The wall wants {Frontier.Wanted(t)} watchmen at night and has {Js.Str(Js.Round(Frontier.OnWalls(t)))}. An unmanned wall gives a third of its strength. Hire the watch in the council.");
        if (f.Strangers > 0)
            yield return new("strangers", Strangers, "Strangers robbing shops",
                Frontier.Walled(t)
                    ? "Footpads come and go through the open gate. Set the gate to Watched and the watch asks every stranger their business."
                    : "With no wall, anyone walks in, footpads too. A wall with a gate lets the watch turn them away.");
    }

    /// <summary>Companies long past the day the town looked for them home, with no word: where they went is all the town knows.</summary>
    private static IEnumerable<Alert> Missing(Town t)
    {
        foreach (var p in Knowledge.Overdue(t))
        {
            var site = Quests.Site(t, p.SiteId);
            int quiet = (t.Tick - p.HeardTick) / SimTime.HoursPerDay;
            string bound = site != null ? site.Def.Name : "the wilds";
            string ask = site is { Def.Aloft: true } && Reaches.RescueRefused(t, p) == null ? "Send an airship to look for them from Bounties"
                : Knowledge.SlotsLeft(t, p) > 0 ? "Send a stone to them" : "Scry where they went, or post a bounty to find them";
            yield return new($"overdue:{p.Id}", Overdue, $"The {p.Name} are overdue",
                $"This company has been gone a while. No word for {quiet} days; they set out for {bound}, looked for home by day {SimTime.DayOf(p.Expected)}. {ask}.",
                SiteId: site?.Def.Id);
        }
    }

    /// <summary>Sickness in the houses; traps spent before a siege; the big bad's company on the road to a hoard.</summary>
    private static IEnumerable<Alert> Brought(Town t)
    {
        if (Sickness.Outbreak(t))
        {
            int n = Sickness.Sick(t).Count;
            yield return new("sick", Sick, n == 1 ? "Sickness in a house" : $"Sickness in {n} houses",
                $"The sick stay home and spread it to the houses round. Send the healers door to door ({Js.Str(Sickness.TendCost(t))}g) or close the sick lanes from the City window.");
        }
        var spent = Traps.Spent(t);
        if (spent.Count > 0)
            yield return new("spent", Spent, spent.Count == 1 ? "A trap is spent" : $"{spent.Count} traps spent",
                $"Refill them from the Defence window ({Js.Str(Traps.RefillCost(t))}g) before the next siege.");
        if (DarkCompany.Target(t) is { } site && t.DarkCompany.Ambush == null && DarkCompany.MeetTick(t) != null)
            yield return new("dark", Dark, $"{Cap(t.DarkCompany.Name)} ride for {site.Def.Name}",
                $"They mean to rob its hoard for {Saga.BigBad(t)?.Leader}. Send a company there first, or ambush them on the road from the Campaign window.", SiteId: site.Def.Id);
    }

    /// <summary>
    /// A rare or finer magic item heroes brought home in the last few days (spoils.json findDays): one look, at the hero
    /// who carries it, or at the armory or the hall where it waits. Clicking it is the look, so the client clears it then.
    /// </summary>
    private static IEnumerable<Alert> Treasures(Town t)
    {
        var r = t.Content.Spoils;
        foreach (var f in t.Spoils.Finds.Where(f => t.Day - f.Day <= r.FindDays).OrderBy(f => f.Id))
        {
            var it = f.Item;
            // Finer than all the finders carried slides a tile out of the rail; the rest only badge it.
            var found = f.Great ? Treasure : RareFind;
            string key = $"treasure:{f.Id}";
            string kind = Cap($"{it.Rarity} {it.Category}".Trim().Replace('-', ' '));
            string from = f.SiteId != null && Quests.Site(t, f.SiteId) is { } site ? $"\n• From {site.Def.Name}" : "";
            string text = $"{Cap(f.Who)} find{(f.Company ? "" : "s")} the {it.Name}";
            var holder = t.Agents.Where(a => !a.Dead && !a.Departed && a.Hero != null && a.Hero.Magic.Values.Contains(it)).OrderBy(a => a.Id != f.HeroId).ThenBy(a => a.Id).FirstOrDefault();
            if (holder != null)
                yield return new(key, found, text, $"• {kind}{from}\n• {holder.GivenName} carries it", AgentId: holder.Id, Item: it);
            else if (t.Armory.Stock.Any(s => s.Item == it))
                yield return new(key, found, text, $"• {kind}{from}\n• In the armory: give it to a hero", Item: it);
            else if (t.Relics.Contains(it.Id))
                yield return new(key, found, text, $"• {kind}{from}\n• Kept in the town hall", AtTown: true, Item: it);
        }
    }

    /// <summary>An ally called to arms that will come only for gold or a harder word.</summary>
    private static IEnumerable<Alert> Allies(Town t)
    {
        foreach (var c in t.Army.Calls)
        {
            if (c.State != CallState.Holdout) continue;
            var s = AllyCalls.Of(t, c);
            yield return new($"holdout:{c.Id}", Holdout, $"{Cap(s.People)} hold out",
                $"They will send {Js.Str(c.Strength)} for {Js.Str(c.Price)}g. Pay, talk them round or let them be.");
        }
        foreach (int id in t.Army.Pending)
        {
            var s = t.Settlements.First(x => x.Id == id);
            yield return new($"conquered:{id}", Conquered, $"{s.Name} is yours",
                "Occupy it for tribute, pillage and burn it, cripple it, or spare it.");
        }
        foreach (var ask in t.Army.Asks)
        {
            var ally = t.Settlements.First(x => x.Id == ask.AllyId);
            var foe = t.Settlements.First(x => x.Id == ask.TargetId);
            yield return new($"warask:{ask.AllyId}:{ask.TargetId}", WarAsk, $"{Cap(ally.People)} want {foe.Name} struck",
                $"March on {foe.People} by day {ask.UntilDay} and {ally.People} will remember it. Every other people will too.");
        }
    }

    /// <summary>A party that sent word home is in the fight now, out of sight: a scrying would show it.</summary>
    private static IEnumerable<Alert> Word(Town t)
    {
        int ready = Knowledge.Ready(t);
        if (ready == 0) yield break;
        foreach (var p in t.Parties.Where(p => !p.Returned && p.Asked > 0 && p.RecalledTick == null).OrderBy(p => p.Id))
        {
            if (Knowledge.LegOf(t, p) != Leg.AtSite || Quests.Site(t, p.SiteId) is not { } site || Watch.LiveOf(t, site) != null) continue;
            yield return new($"scry:{p.Id}", Scry, $"The {p.Name} go into {site.Def.Name}",
                $"Fighting out of sight. Scry to watch · {ready} ready", SiteId: site.Def.Id);
        }
    }

    /// <summary>A cleared site the town can afford to garrison before something moves back in. The watch waits for the word.</summary>
    private static IEnumerable<Alert> Outposts(Town t)
    {
        // A town tower that something is coming to test: one action, reinforce it.
        foreach (var x in t.Sites)
        {
            if (x.ThreatTick is not int at || !x.Held || x.Reinforced || Systems.Loyalty.Lord(t, x.Def.Id) != null) continue;
            int days = Math.Max(1, (int)Math.Ceiling((at - t.Tick) / (double)SimTime.HoursPerDay));
            yield return new($"tower:{x.Def.Id}", Tower, $"{x.Def.Name}: tower tested in {days}d",
                $"• {Cap(x.ThreatBy)} are coming\n• Holds {Math.Round(Lairs.HoldOdds(t, x) * 100)}% now\n• Reinforce: {Js.Str(t.Content.Lairs.Outpost.Reinforce)}g", SiteId: x.Def.Id);
        }
        // Cleared by a bounty, then kept clear by a patrol, then held for good by a tower.
        if (Lairs.WorthPatrolling(t) is { } p)
        {
            var pr = t.Content.Lairs.Patrol;
            yield return new($"patrol:{p.Def.Id}", Patrol, $"Keep {p.Def.Name} clear?",
                $"• Cleared, but something will move back in\n• A patrol: {Js.Str(Lairs.PatrolWage(t, p))}g a day\n• Heroes ride out and drive newcomers off", SiteId: p.Def.Id);
            yield break;
        }
        if (Lairs.WorthHolding(t) is not { } s) yield break;
        var o = t.Content.Lairs.Outpost;
        yield return new($"outpost:{s.Def.Id}", Outpost, $"Hold {s.Def.Name}?",
            $"• Cleared, but something will move back in\n• A tower: {Js.Str(o.Cost)}g, {o.BuildDays} days to build, {Js.Str(o.Upkeep)}g a day", SiteId: s.Def.Id);
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

    /// <summary>A notable asking a favour, or one about to turn their back on the town.</summary>
    private static IEnumerable<Alert> Notables(Town t)
    {
        foreach (var n in Systems.Notables.Active(t))
        {
            string who = Systems.Notables.NameOf(t, n);
            if (Systems.Notables.AskOf(t, n) is { } ask)
                yield return new($"notable:{n.Id}", Notable, $"{who} asks a favour",
                    $"{Cap(who)} {Systems.Notables.AskText(t, n, ask)}. Answer in the Notables window within {Days(Math.Max(1, n.AskUntil - t.Day))}.", AgentId: n.AgentId);
            else if (Systems.Notables.Band(t, n) == Systems.Notables.Bitter)
                yield return new($"bitter:{n.Id}", Notable, $"{who} is bitter",
                    $"{Cap(who)} will walk away from the town in {Days(Math.Max(1, t.Content.Notables.LeaveDays - n.BitterDays))}. Visit them in the Notables window.", AgentId: n.AgentId);
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
