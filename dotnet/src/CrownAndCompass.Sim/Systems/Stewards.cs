namespace CrownAndCompass.Sim.Systems;

/// <summary>Who runs one area of the town's business: the player, a steward, or a steward who asks first.</summary>
public enum StewardMode
{
    Player,
    Steward,
    Ask,
}

/// <summary>
/// One area a steward can take on, and who runs it when it is handed over: a lesser councillor with a title, chosen
/// from the townsfolk by <see cref="Ability"/>. <see cref="Alerts"/> are the alert kinds its steward answers, so they
/// stay off the rail while the steward runs it; <see cref="Kinds"/> are the levers the player can switch off one by one.
/// </summary>
public sealed record StewardArea(string Id, string Name, string Title, string Icon, string Blurb, bool CanAsk, string Ability, string[] Alerts, string[] Kinds);

/// <summary>Something a steward who asks first wants to do: one yes or no for the player.</summary>
public sealed class StewardAsk
{
    public int Id { get; init; }
    public required string Area { get; init; }
    public required string Kind { get; init; }
    /// <summary>What it would do, in a few words ("Post a bounty · the Wolf Den · 120g").</summary>
    public required string Title { get; init; }
    /// <summary>Why the steward wants it, for the hover and the card opened up.</summary>
    public string Why { get; init; } = "";
    public string S1 { get; init; } = "";
    public string S2 { get; init; } = "";
    public int I1 { get; init; }
    public int I2 { get; init; }
    public double D1 { get; init; }
    public double D2 { get; init; }
    /// <summary>What it spends at once, against the area's monthly budget (<see cref="StewardState.Caps"/>).</summary>
    public double Cost { get; init; }
    public int Day { get; init; }

    public string Key => $"{Kind}:{S1}:{S2}:{I1}:{I2}";
}

public sealed class StewardState
{
    /// <summary>Each area the player has handed over or taken back; an area not here follows the game's start (<see cref="SimulationOptions"/>).</summary>
    public Dictionary<string, StewardMode> Modes { get; } = [];

    /// <summary>What stewards who ask first are waiting on, oldest first.</summary>
    public List<StewardAsk> Asks { get; } = [];

    /// <summary>What the player said no to, by <see cref="StewardAsk.Key"/>, and the day: not asked again for a while.</summary>
    public Dictionary<string, int> Declined { get; } = [];

    public int NextId { get; set; } = 1;

    /// <summary>The lesser councillor running each area handed over, by area: an agent id.</summary>
    public Dictionary<string, int> Holders { get; } = [];

    /// <summary>The most gold a month the player lets each area's steward spend; an area not here has no cap.</summary>
    public Dictionary<string, double> Caps { get; } = [];

    /// <summary>What each area's steward has spent this month (<see cref="Month"/>).</summary>
    public Dictionary<string, double> Spent { get; } = [];

    public int Month { get; set; }

    /// <summary>Levers the player has switched off within an area, as "area:kind" (the armory's buy, gift and sell).</summary>
    public Dictionary<string, bool> Off { get; } = [];

    /// <summary>What the stewards did, newest last, for the player to look over (<see cref="Stewards.ReportKept"/>).</summary>
    public List<StewardDeed> Report { get; } = [];

    /// <summary>The area whose steward is deciding right now and asks instead of acting; set only inside <see cref="Stewards.For"/>.</summary>
    [NotSaved]
    internal string? Asking;

    /// <summary>The area whose steward is deciding right now, asking or acting; set only inside <see cref="Stewards.For"/>.</summary>
    [NotSaved]
    internal string? Acting;
}

/// <summary>One thing a steward did on its own, for the Automate tab's report.</summary>
public sealed class StewardDeed
{
    public int Day { get; init; }
    public string Area { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>
/// Stewards: the stand-in mayor's levers handed to the player area by area, mid-game. Each area is the player's,
/// a steward's (the stand-in acts, as in a demo), or a steward's who asks first: the stand-in decides as usual, but
/// each of the few calls that spend gold or bind the town (<see cref="Intercept"/>) becomes a yes-or-no for the player
/// instead, one per area at a time. The player's yes runs the same call (<see cref="Answer"/>). Asking never rolls
/// dice, so a steward who asks leaves the main stream as it would be with the player in charge.
/// </summary>
public static class Stewards
{
    public static readonly StewardArea[] Areas =
    [
        new("planning", "Town planning", "Surveyor", "tab-build", "Fills the land you painted, raises yards, services and the temple", false, Ability.Int,
            ["housing", "settlers", "work", "charter", "grandwork", "stalled", "rooms", "stores", "crowded", "camp"], []),
        new("research", "Research", "Loremaster", "tab-library", "Picks what the scholars study and how well they are paid", true, Ability.Int, ["scholars"], []),
        new("realm", "Diplomacy and trade", "Envoy", "tab-diplomacy", "Envoys, trade routes and wagons, aid and works for neighbours, road patrols, bridges and ruins", true, Ability.Cha,
            ["envoy", "courted", "neighbour", "allyask", "shadow", "trade", "bridge", "ruin"], ["envoy", "route", "wagon", "aid", "works", "ally", "roadpatrol", "bridge", "ruin"]),
        new("council", "Council motions", "Clerk of the Council", "tab-council", "Signs or vetoes what the council moves", true, Ability.Cha, ["motion"], []),
        new("guilds", "Guilds and union", "Guildreeve", "tools", "Meets or buys off guild demands, hears out the guilds and the mages", true, Ability.Cha, ["guild", "union"], []),
        new("order", "Order in town", "Sheriff", "suspicion", "Cracks down on cults and thieves' rings, patrols the undercity", true, Ability.Wis, ["below", "hostile"], []),
        new("bounties", "Bounties and scouting", "Bountymaster", "tab-bounties", "Posts bounties on known trouble, surveys, buys reports, sends scouts", true, Ability.Wis,
            ["overdue", "explorers", "captives"], []),
        new("armory", "Armory", "Quartermaster", "tab-armory", "Buys loot, gifts it to heroes, sells what sits unused", true, Ability.Int, ["loot", "gifts"], ["buy", "gift", "sell"]),
        new("honours", "Honours", "Herald", "ui-renown", "Purses, fiefs, the champion, bastions and memorials to the fallen", true, Ability.Cha, ["heroes", "honours", "bastion"], []),
        new("defence", "Defence", "Castellan", "tab-defence", "Walls, towers, sellswords, wardens; answers demands and tribute", true, Ability.Str, ["fealty", "spent"], []),
        new("frontier", "Fields and gate", "Warden of the Marches", "order-guard", "Guard flags over the fields beyond the walls, the gate law", true, Ability.Wis,
            ["fields", "unmanned", "strangers"], []),
        new("fleet", "Fleet", "Harbourmaster", "inst-harbour", "Builds and patches ships, keeps the sea runs going, patrols when corsairs are about; the airships too", true, Ability.Wis,
            ["ship", "idle", "corsairs"], ["ship", "patch", "run", "patrol"]),
        new("lairs", "Cleared lairs", "Marshal of Holds", "post-patrol", "Towers or patrols on cleared lairs, reinforcing them", true, Ability.Str, ["patrol", "outpost", "tower"], []),
        new("stories", "Story events", "Chronicler", "lore", "Answers story events, sendings and tourneys", false, Ability.Cha, ["story", "tourney", "wager", "levelup"], []),
    ];

    public static StewardArea Area(string id) => Areas.First(a => a.Id == id);

    internal static string Lower(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Days a no keeps a steward from asking the same again.</summary>
    public const int DeclineDays = 10;

    /// <summary>Days an unanswered ask stands before the steward lets it go.</summary>
    public const int AskDays = 3;

    /// <summary>Lines kept in the stewards' report.</summary>
    public const int ReportKept = 80;

    /// <summary>Days in a steward's budget month.</summary>
    public const int MonthDays = 30;

    /// <summary>Who runs an area now: the player's choice, else the game's start.</summary>
    public static StewardMode Mode(Town town, SimulationOptions o, string area)
    {
        if (town.Stewards.Modes.TryGetValue(area, out var m)) return m;
        // Council motions settle on their own with the lever alone; the rest need the stand-in mayor.
        bool auto = (area == "council" || o.AutoMayor) && area switch
        {
            "bounties" => o.AutoBounties,
            "armory" => o.AutoArmory,
            "honours" => o.AutoHonours,
            "council" or "guilds" => o.AutoCouncil,
            "defence" or "frontier" or "lairs" => o.AutoDefense,
            "stories" => o.AutoStories,
            "planning" => o.AutoPlanning,
            "research" or "realm" or "order" or "fleet" => o.AutoHelpers,
            _ => false,
        };
        return auto ? StewardMode.Steward : StewardMode.Player;
    }

    /// <summary>Whether the stand-in decides for this area (a steward acts or asks).</summary>
    public static bool Hand(Town town, SimulationOptions o, string area) => o.AutoMayor && Mode(town, o, area) != StewardMode.Player;

    /// <summary>Whether a steward runs this area outright, asking nothing.</summary>
    public static bool Runs(Town town, SimulationOptions o, string area) => o.AutoMayor && Mode(town, o, area) == StewardMode.Steward;

    /// <summary>Whether the player handed this area over themselves (rather than the game starting it with the stand-in).</summary>
    public static bool Handed(Town town, SimulationOptions o, string area) => Hand(town, o, area) && town.Stewards.Modes.ContainsKey(area);

    /// <summary>A lever under an area (exploring, reports): on with the area once the player has handed it over themselves.</summary>
    public static bool Sub(Town town, SimulationOptions o, string area, bool option) => Hand(town, o, area) && (town.Stewards.Modes.ContainsKey(area) || option);

    public static bool Set(Town town, string area, StewardMode mode)
    {
        if (Areas.All(a => a.Id != area) || (mode == StewardMode.Ask && !Area(area).CanAsk)) return false;
        town.Stewards.Modes[area] = mode;
        if (mode != StewardMode.Ask) town.Stewards.Asks.RemoveAll(a => a.Area == area);
        return true;
    }

    /// <summary>Hand every area over at once, or take them all back: Ask falls back to a steward where an area can't ask.</summary>
    public static void SetAll(Town town, StewardMode mode)
    {
        foreach (var a in Areas) Set(town, a.Id, mode == StewardMode.Ask && !a.CanAsk ? StewardMode.Steward : mode);
    }

    /// <summary>The most a month an area's steward may spend; zero or less lifts the cap.</summary>
    public static bool SetCap(Town town, string area, double gold)
    {
        if (Areas.All(a => a.Id != area)) return false;
        if (gold <= 0) town.Stewards.Caps.Remove(area);
        else town.Stewards.Caps[area] = Js.Round(gold);
        return true;
    }

    /// <summary>Switch one of an area's levers off or back on (<see cref="StewardArea.Kinds"/>).</summary>
    public static bool SetLever(Town town, string area, string kind, bool on)
    {
        if (Areas.FirstOrDefault(a => a.Id == area) is not { } def || !def.Kinds.Contains(kind)) return false;
        if (on) town.Stewards.Off.Remove($"{area}:{kind}");
        else town.Stewards.Off[$"{area}:{kind}"] = true;
        return true;
    }

    public static bool LeverOn(Town town, string area, string kind) => !town.Stewards.Off.ContainsKey($"{area}:{kind}");

    /// <summary>What an area's steward has spent this month.</summary>
    public static double SpentThisMonth(Town town, string area) => town.Stewards.Month == town.Day / MonthDays ? town.Stewards.Spent.GetValueOrDefault(area) : 0;

    /// <summary>The area's lesser councillor, if it has one.</summary>
    public static Agent? Holder(Town town, string area) =>
        town.Stewards.Holders.TryGetValue(area, out int id) && id >= 1 && id <= town.Agents.Count ? town.Agent(id) : null;

    /// <summary>Whether an alert kind is one a steward running its area answers, so the rail leaves it out.</summary>
    public static bool Quiet(Town town, SimulationOptions o, string alertKind) =>
        Areas.Any(a => a.Alerts.Contains(alertKind) && Runs(town, o, a.Id));

    /// <summary>While the stand-in decides for an area its steward runs or asks about, its spending goes through <see cref="Intercept"/>.</summary>
    public static Scope For(Town town, SimulationOptions o, string area)
    {
        var mode = o.AutoMayor ? Mode(town, o, area) : StewardMode.Player;
        return new(town, mode == StewardMode.Ask ? area : null, mode == StewardMode.Player ? null : area);
    }

    public readonly struct Scope : IDisposable
    {
        private readonly Town _town;
        private readonly string? _was, _wasActing;

        public Scope(Town town, string? asking, string? acting)
        {
            _town = town;
            _was = town.Stewards.Asking;
            _wasActing = town.Stewards.Acting;
            town.Stewards.Asking = asking;
            town.Stewards.Acting = acting;
        }

        public void Dispose()
        {
            _town.Stewards.Asking = _was;
            _town.Stewards.Acting = _wasActing;
        }
    }

    /// <summary>
    /// Called at the top of each lever a steward pulls. The player's own calls pass. A steward's call is held back when
    /// the player switched that lever off or it would break the area's monthly budget; a steward who asks first turns it
    /// into an ask (one per area at a time, none the player said no to lately); a steward who runs the area goes ahead,
    /// and it goes in the report. Returns whether the lever should do nothing.
    /// </summary>
    public static bool Intercept(Town town, string area, StewardAsk ask)
    {
        var st = town.Stewards;
        if (st.Acting != area) return false;
        if (!LeverOn(town, area, ask.Kind)) return true;
        if (st.Caps.TryGetValue(area, out double cap) && SpentThisMonth(town, area) + ask.Cost > cap) return true;
        if (st.Asking != area)
        {
            Spend(town, area, ask.Cost);
            Note(town, area, ask.Title);
            return false;
        }
        if (st.Asks.Any(a => a.Area == area)) return true;
        if (st.Declined.TryGetValue(ask.Key, out int day) && town.Day - day < DeclineDays) return true;
        st.Asks.Add(new StewardAsk
        {
            Id = st.NextId++,
            Area = area,
            Kind = ask.Kind,
            Title = ask.Title,
            Why = ask.Why,
            S1 = ask.S1,
            S2 = ask.S2,
            I1 = ask.I1,
            I2 = ask.I2,
            D1 = ask.D1,
            D2 = ask.D2,
            Cost = ask.Cost,
            Day = town.Day,
        });
        return true;
    }

    private static void Spend(Town town, string area, double gold)
    {
        var st = town.Stewards;
        if (st.Month != town.Day / MonthDays)
        {
            st.Month = town.Day / MonthDays;
            st.Spent.Clear();
        }
        if (gold > 0) st.Spent[area] = st.Spent.GetValueOrDefault(area) + gold;
    }

    /// <summary>A line in the stewards' report.</summary>
    public static void Note(Town town, string area, string text)
    {
        var r = town.Stewards.Report;
        r.Add(new StewardDeed { Day = town.Day, Area = area, Text = text });
        if (r.Count > ReportKept) r.RemoveRange(0, r.Count - ReportKept);
    }

    /// <summary>The player's yes (the steward's call goes ahead, if it still can) or no (not asked again for a while).</summary>
    public static bool Answer(Town town, int askId, bool yes)
    {
        var st = town.Stewards;
        var ask = st.Asks.FirstOrDefault(a => a.Id == askId);
        if (ask == null) return false;
        st.Asks.Remove(ask);
        if (!yes)
        {
            st.Declined[ask.Key] = town.Day;
            return true;
        }
        using var _ = new Scope(town, null, null);
        return Do(town, ask);
    }

    /// <summary>Yes or no to every ask waiting: how many went ahead.</summary>
    public static int AnswerAll(Town town, bool yes) => town.Stewards.Asks.Select(a => a.Id).ToList().Count(id => Answer(town, id, yes) && yes);

    private static bool Do(Town town, StewardAsk a) => a.Kind switch
    {
        "bounty" => Quests.PostBounty(town, a.S1, a.D1, null, a.S2 == "" ? "slay" : a.S2, a.I1 == 1) != null,
        "survey" => Explore.Post(town, a.I1, a.D1 > 0 ? a.D1 : null) != null,
        "find" => Explore.Order(town, a.I1, a.S1),
        "intel" => Intel.Buy(town, a.I1, false) != null,
        "scout" => Watch.PostScout(town, a.S1) != null,
        "buy" => Armory.Buy(town, a.I1),
        "gift" => Armory.Gift(town, a.I1, a.I2),
        "sell" => Armory.Sell(town, a.I1),
        "reward" => a.I1 >= 1 && a.I1 <= town.Agents.Count && Loyalty.Reward(town, town.Agent(a.I1), a.D1),
        "fief" => a.I1 >= 1 && a.I1 <= town.Agents.Count && Loyalty.Grant(town, town.Agent(a.I1), a.S1),
        "champion" => a.I1 >= 1 && a.I1 <= town.Agents.Count && Loyalty.NameChampion(town, town.Agent(a.I1)),
        "bastion" => Bastions.Answer(town, a.I1, BastionAnswer.Grant),
        "memorial" => Legacies.RaiseMemorial(town, a.I1),
        "motion" => Council.Decide(town, a.I1, a.I2 == 1),
        "ally" => Accords.Answer(town, a.I1, true),
        "defense" => Threat.Build(town, (DefenseWork)a.I1),
        "hire" => Mercenaries.Hire(town, a.S1),
        "warden" => Clockwork.Build(town),
        "clash" => Crises.Join(town, a.I1, a.I2),
        "hold" => Lairs.Hold(town, a.S1),
        "reinforce" => Lairs.Reinforce(town, a.S1),
        "study" => Research.Study(town, a.S1),
        "fund" => Research.Fund(town, a.I1),
        "envoy" => World.SendEnvoy(town, a.I1),
        "route" => Enum.TryParse<Good>(a.S1, out var ex) && Enum.TryParse<Good>(a.S2, out var im) && Trade.OpenRoute(town, a.I1, ex, im) != null,
        "wagon" => Trade.AddWagon(town, a.I1),
        "ship" => Fleet.Build(town, a.S1) != null,
        "patch" => Fleet.Repair(town, a.I1),
        "run" => Enum.TryParse<Good>(a.S1, out var cargo) && Fleet.Send(town, a.I1, a.I2, cargo),
        "patrol" => a.S1 == "sky" ? Sky.Send(town, a.I1, "patrol") : Voyages.Send(town, a.I1, "patrol"),
        "aid" => Neighbours.Aid(town, a.I1),
        "works" => Neighbours.Fund(town, a.I1, a.S1),
        "roadpatrol" => a.I1 >= 0 && a.I1 < town.Settlements.Count && Wilds.PatrolRoad(town, town.Settlements[a.I1]) != null,
        "bridge" => Bridges.Rebuild(town, a.I1),
        "ruin" => Ruins.Restore(town, a.S1),
        "crackdown" => Factions.CrackDown(town, a.S1),
        "undercity" => Undercity.SetPatrol(town, a.S1, a.I1 == 1),
        "gate" => Frontier.SetGate(town, a.S1),
        "flag" => Garrison.FlagBlocked(town, a.D1, a.D2) == null && Garrison.PlantFlag(town, a.D1, a.D2) != null,
        "meet" => Guilds.Meet(town, a.S1),
        "payoff" => Guilds.PayOff(town, a.S1),
        "hear" => Union.Hear(town, a.S1),
        _ => false,
    };

    /// <summary>
    /// Each area handed over has a lesser councillor: the townsperson best suited by the area's ability who holds no
    /// other post, named once and replaced if they die or leave. No dice: the choice is by ability, then id.
    /// </summary>
    private static void Appoint(Town town, SimulationOptions o)
    {
        var st = town.Stewards;
        var seated = new HashSet<int>(town.Seats.Where(s => s.HolderId != null).Select(s => s.HolderId!.Value));
        List<Agent>? pool = null;
        foreach (var area in Areas)
        {
            bool handed = Hand(town, o, area.Id);
            var holder = Holder(town, area.Id);
            if (holder != null && (!handed || holder.Dead || holder.Departed || holder.Hero != null || seated.Contains(holder.Id)))
            {
                st.Holders.Remove(area.Id);
                if (handed) town.Log(LedgerCategory.Council, Importance.Routine, $"{holder.Name} is no longer {town.Name}'s {area.Title}.", [holder.Id]);
                holder = null;
            }
            if (!handed || holder != null) continue;
            var held = new HashSet<int>(st.Holders.Values);
            pool ??= town.Workers;
            var pick = pool
                .Where(a => !a.Dead && !a.Traveller && a.Age >= 18 && !seated.Contains(a.Id) && !held.Contains(a.Id))
                .OrderByDescending(a => a.Abilities[area.Ability])
                .ThenBy(a => a.Id)
                .FirstOrDefault();
            if (pick == null) continue;
            st.Holders[area.Id] = pick.Id;
            town.Log(LedgerCategory.Council, Importance.Routine, $"{pick.Name} takes a lesser seat on the council as {area.Title}: {Lowered(area.Name)} is theirs to run.", [pick.Id]);
        }
    }

    private static string Lowered(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>Daily: asks the player let stand too long lapse, old noes are forgotten, and a council steward who asks puts the pending motion.</summary>
    public static void Daily(Town town, SimulationOptions o)
    {
        var st = town.Stewards;
        st.Asks.RemoveAll(a => town.Day - a.Day >= AskDays || Mode(town, o, a.Area) != StewardMode.Ask);
        Spend(town, "", 0);
        Appoint(town, o);
        foreach (var k in st.Declined.Where(kv => town.Day - kv.Value >= DeclineDays).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList()) st.Declined.Remove(k);
        if (o.AutoMayor && Mode(town, o, "council") == StewardMode.Ask && Council.PendingMotion(town) is { } m && Council.Majority(m) is bool sign)
        {
            using var _ = new Scope(town, "council", "council");
            Intercept(town, "council", new StewardAsk
            {
                Area = "council",
                Kind = "motion",
                Title = $"{(sign ? "Sign" : "Veto")} · {m.Text}",
                Why = $"• The council leans {(sign ? "aye" : "nay")}, {m.Votes.Count(v => v.Yes)} to {m.Votes.Count(v => !v.Yes)}\n• Going with the hall keeps the seats sweet",
                I1 = m.Id,
                I2 = sign ? 1 : 0,
            });
        }
    }
}
