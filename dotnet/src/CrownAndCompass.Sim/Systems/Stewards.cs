namespace CrownAndCompass.Sim.Systems;

/// <summary>Who runs one area of the town's business: the player, a steward, or a steward who asks first.</summary>
public enum StewardMode
{
    Player,
    Steward,
    Ask,
}

/// <summary>One area a steward can take on, and the stand-in mayor's lever it stands for (<see cref="SimulationOptions"/>).</summary>
public sealed record StewardArea(string Id, string Name, string Icon, string Blurb, bool CanAsk);

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

    /// <summary>The area whose steward is deciding right now and asks instead of acting; set only inside <see cref="Stewards.For"/>.</summary>
    [NotSaved]
    internal string? Asking;
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
        new("bounties", "Bounties and scouting", "tab-bounties", "Posts bounties on known trouble, surveys, buys reports, sends scouts", true),
        new("armory", "Armory", "tab-armory", "Buys loot, gifts it to heroes, sells what sits unused", true),
        new("honours", "Honours", "tab-mayor", "Purses for restless heroes, fiefs for knights, a champion", true),
        new("council", "Council motions", "tab-council", "Signs or vetoes what the council moves", true),
        new("defence", "Defence", "defence", "Walls, towers, sellswords, wardens, outposts, guard posts", true),
        new("stories", "Story events", "lore", "Answers story events, sendings and tourneys", false),
        new("planning", "Town planning", "tab-build", "Zones land and raises civic buildings", false),
    ];

    public static StewardArea Area(string id) => Areas.First(a => a.Id == id);

    internal static string Lower(string pascal) => string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()));

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Days a no keeps a steward from asking the same again.</summary>
    public const int DeclineDays = 10;

    /// <summary>Days an unanswered ask stands before the steward lets it go.</summary>
    public const int AskDays = 3;

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
            "council" => o.AutoCouncil,
            "defence" => o.AutoDefense,
            "stories" => o.AutoStories,
            "planning" => o.AutoPlanning,
            _ => false,
        };
        return auto ? StewardMode.Steward : StewardMode.Player;
    }

    /// <summary>Whether the stand-in decides for this area (a steward acts or asks).</summary>
    public static bool Hand(Town town, SimulationOptions o, string area) => o.AutoMayor && Mode(town, o, area) != StewardMode.Player;

    /// <summary>A lever under an area (exploring, reports): on with the area once the player has handed it over themselves.</summary>
    public static bool Sub(Town town, SimulationOptions o, string area, bool option) => Hand(town, o, area) && (town.Stewards.Modes.ContainsKey(area) || option);

    public static bool Set(Town town, string area, StewardMode mode)
    {
        if (Areas.All(a => a.Id != area) || (mode == StewardMode.Ask && !Area(area).CanAsk)) return false;
        town.Stewards.Modes[area] = mode;
        if (mode != StewardMode.Ask) town.Stewards.Asks.RemoveAll(a => a.Area == area);
        return true;
    }

    /// <summary>While the stand-in decides for an area whose steward asks first, its spending calls ask instead (<see cref="Intercept"/>).</summary>
    public static Scope For(Town town, SimulationOptions o, string area) =>
        new(town, Mode(town, o, area) == StewardMode.Ask ? area : null);

    public readonly struct Scope : IDisposable
    {
        private readonly Town _town;
        private readonly string? _was;

        public Scope(Town town, string? asking)
        {
            _town = town;
            _was = town.Stewards.Asking;
            town.Stewards.Asking = asking;
        }

        public void Dispose() => _town.Stewards.Asking = _was;
    }

    /// <summary>
    /// Called at the top of each lever a steward pulls: when that area's steward asks first, the call becomes an ask
    /// (one per area at a time, none the player said no to lately) and the lever does nothing. Returns whether it asked.
    /// </summary>
    public static bool Intercept(Town town, string area, StewardAsk ask)
    {
        var st = town.Stewards;
        if (st.Asking != area) return false;
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
            Day = town.Day,
        });
        return true;
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
        string? was = st.Asking;
        st.Asking = null;
        try
        {
            return Do(town, ask);
        }
        finally
        {
            st.Asking = was;
        }
    }

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
        "motion" => Council.Decide(town, a.I1, a.I2 == 1),
        "defense" => Threat.Build(town, (DefenseWork)a.I1),
        "hire" => Mercenaries.Hire(town, a.S1),
        "warden" => Clockwork.Build(town),
        "clash" => Crises.Join(town, a.I1, a.I2),
        "hold" => Lairs.Hold(town, a.S1),
        "reinforce" => Lairs.Reinforce(town, a.S1),
        _ => false,
    };

    /// <summary>Daily: asks the player let stand too long lapse, old noes are forgotten, and a council steward who asks puts the pending motion.</summary>
    public static void Daily(Town town, SimulationOptions o)
    {
        var st = town.Stewards;
        st.Asks.RemoveAll(a => town.Day - a.Day >= AskDays || Mode(town, o, a.Area) != StewardMode.Ask);
        foreach (var k in st.Declined.Where(kv => town.Day - kv.Value >= DeclineDays).Select(kv => kv.Key).Order(StringComparer.Ordinal).ToList()) st.Declined.Remove(k);
        if (o.AutoMayor && Mode(town, o, "council") == StewardMode.Ask && Council.PendingMotion(town) is { } m && Council.Majority(m) is bool sign)
        {
            using var _ = new Scope(town, "council");
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
