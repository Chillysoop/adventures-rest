namespace CrownAndCompass.Sim.Systems;

/// <summary>One charter goal of the first days (content/game/goals.json): what to do, how it is measured and its small reward.</summary>
public sealed record GoalDef
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>What the progress counts, for the card ("Households 12/20").</summary>
    public string Label { get; init; } = "";

    /// <summary>A <see cref="Goals.Measure"/> token.</summary>
    public string Measure { get; init; } = "";
    public int Target { get; init; } = 1;

    /// <summary>Count only what is gained after the goal shows (the land painted since), not what the town began with.</summary>
    public bool Gain { get; init; }
    public double Gold { get; init; }
    public double Mandate { get; init; }

    /// <summary>Days of festival cheer the town gets.</summary>
    public int Festival { get; init; }

    /// <summary>
    /// The button the card points the player at while this is the goal to work on: a tutorial anchor
    /// (<see cref="Tutorial.Anchors"/>), "tab:&lt;Tab&gt;", "zone:&lt;Zone&gt;" (that brush on the build bar) or "place:&lt;building&gt;".
    /// </summary>
    public string Point { get; init; } = "";

    /// <summary>A measure that must be above 0 before the card points at the button (a motion to answer), or "".</summary>
    public string When { get; init; } = "";

    /// <summary>The icon the card shows beside the goal (godot/assets/icons).</summary>
    public string Icon { get; init; } = "";
}

public sealed record GoalRules
{
    /// <summary>Goals open at a time.</summary>
    public int Shown { get; init; } = 3;

    /// <summary>The day after which an unfinished list closes quietly (and old saves never see it).</summary>
    public int Lapse { get; init; } = 30;
    public List<GoalDef> Goals { get; init; } = [];
}

/// <summary>Which goals the town has met, in the order it met them; Closed once the list is finished or lapsed.</summary>
public sealed class GoalsState
{
    public List<string> Done { get; } = [];

    /// <summary>The day each goal in <see cref="Done"/> was met.</summary>
    public List<int> DoneDay { get; } = [];
    public bool Closed { get; set; }

    /// <summary>For goals that count a gain: the measure when each one showed.</summary>
    public OrderedMap<string, int> Base { get; } = new();
}

/// <summary>
/// The charter goals that give a new town's first days a direction: a short ordered list, a few open at a time,
/// each met when a measure of the town reaches its target. Rules only read the town; meeting a goal pays its reward
/// and writes a Ledger line, and the next goal shows.
/// </summary>
public static class Goals
{
    public const string Account = "Charter goals";

    /// <summary>
    /// A number read off the town: "households", "tier", "built:&lt;building&gt;", "painted:&lt;Zone&gt;" (whole tiles of it), "watch",
    /// "decided" (motions the mayor signed or vetoed), "motions" (waiting on the mayor),
    /// or any tutorial measure (<see cref="Tutorial.Measure"/>). Null for a token that means nothing.
    /// </summary>
    public static int? Measure(Town t, string token)
    {
        if (token == "households") return t.Population;
        if (token == "tier") return t.City.Tier;
        if (token == "watch") return t.Watch + Built(t, "watch_house");
        if (token == "motions") return t.Motions.Count(m => m.Status == MotionStatus.Pending);
        if (token == "decided") return t.Motions.Count(m => m.DecidedBy == DecidedBy.Mayor);
        if (token.StartsWith("built:", StringComparison.Ordinal))
            return t.Content.Buildings.Any(b => b.Id == token[6..]) ? Built(t, token[6..]) : null;
        if (token.StartsWith("painted:", StringComparison.Ordinal))
        {
            if (!Enum.TryParse<Zone>(token[8..], out var zone)) return null;
            var zones = t.Plan.Zones;
            int n = 0;
            for (int i = 0; i < zones.Length; i++) if (zones[i] == (byte)zone) n++;
            return n / (Land.Res * Land.Res);
        }
        return Tutorial.Measure(t, token);
    }

    private static int Built(Town t, string id) => t.Buildings.Count(b => b.Def.Id == id && b.Complete && !b.ForSale);

    /// <summary>The goals showing now, in list order: the first few not yet met. Empty once the list is closed.</summary>
    public static List<GoalDef> Open(Town t)
    {
        var st = t.Goals;
        if (st.Closed) return [];
        var rules = t.Content.Goals;
        return rules.Goals.Where(g => !st.Done.Contains(g.Id)).Take(rules.Shown).ToList();
    }

    /// <summary>How far a goal has come: the measure (less its base for a gain, capped at the target) and the target.</summary>
    public static (int Now, int Target) Progress(Town t, GoalDef g) =>
        (Math.Min(g.Target, Math.Max(0, Value(t, g))), g.Target);

    private static int Value(Town t, GoalDef g)
    {
        int now = Measure(t, g.Measure) ?? 0;
        return g.Gain ? now - t.Goals.Base.Get(g.Id, now) : now;
    }

    /// <summary>The goal the card points at: the first open one with a button to press.</summary>
    public static GoalDef? Next(Town t) => Open(t).FirstOrDefault(g => g.Point != "" && (g.When == "" || Measure(t, g.When) > 0));

    /// <summary>The reward in words: "15 gold", "+3 mandate", "a festival day".</summary>
    public static string Reward(GoalDef g)
    {
        var parts = new List<string>();
        if (g.Gold > 0) parts.Add($"{Js.Str(g.Gold)} gold");
        if (g.Mandate > 0) parts.Add($"+{Js.Str(g.Mandate)} mandate");
        if (g.Festival > 0) parts.Add(g.Festival == 1 ? "a festival day" : $"{g.Festival} festival days");
        return string.Join(", ", parts);
    }

    /// <summary>Note where the open goals that count a gain start from: at the charter, and as each one shows.</summary>
    public static void Begin(Town t)
    {
        foreach (var g in Open(t))
            if (g.Gain && !t.Goals.Base.ContainsKey(g.Id)) t.Goals.Base[g.Id] = Measure(t, g.Measure) ?? 0;
    }

    /// <summary>Every hour: meet the open goals whose measure has reached its target.</summary>
    public static void Hourly(Town t)
    {
        var st = t.Goals;
        if (st.Closed) return;
        var rules = t.Content.Goals;
        if (t.Day > rules.Lapse || rules.Goals.Count == 0)
        {
            st.Closed = true;
            return;
        }
        foreach (var g in Open(t))
            if (st.Base.ContainsKey(g.Id) || !g.Gain)
                if (Value(t, g) >= g.Target) Meet(t, g);
        Begin(t);
        if (rules.Goals.All(g => st.Done.Contains(g.Id)))
        {
            st.Closed = true;
            t.Log(LedgerCategory.Town, Importance.Notable, $"{t.Name} has met every goal of its first charter list.");
        }
    }

    private static void Meet(Town t, GoalDef g)
    {
        t.Goals.Done.Add(g.Id);
        t.Goals.DoneDay.Add(t.Day);
        if (g.Gold > 0) t.Book(Account, g.Gold);
        if (g.Mandate > 0) t.Mandate = Math.Min(100, t.Mandate + g.Mandate);
        if (g.Festival > 0) t.FestivalDays = Math.Max(t.FestivalDays, g.Festival);
        string reward = Reward(g);
        t.Log(LedgerCategory.Town, Importance.Notable, $"Charter goal met: {g.Text}.{(reward == "" ? "" : $" The town gains {reward}.")}");
    }
}

/// <summary>One line of the what-next card once the charter goals are done: an icon, a few words, how far along, and the button it points at.</summary>
public sealed record Aim(string Icon, string Text, string Figure, double Share, string Point = "");

/// <summary>
/// What the town works toward after its first charter list, read fresh off the town (nothing saved): the next
/// charter's missing terms, then, once the big bad shows, the closest road to victory, then the days a claimed road
/// still has to hold. Pure reads, so the card can never disagree with the City and Campaign windows.
/// </summary>
public static class Ahead
{
    /// <summary>Lines shown at once.</summary>
    public const int Most = 3;

    /// <summary>The card's heading and its lines; no lines once the charter goals still show or the campaign is over.</summary>
    public static (string Title, List<Aim> Aims) Now(Town t)
    {
        if (!t.Goals.Closed || t.Outcome != null) return ("", []);
        if (Victory.Claimed(t) is { } held)
            return ($"Hold {held.Name}", [new(held.Icon, $"Hold {held.Name}", $"{Victory.Held(t)}/{held.Hold} days", (double)Victory.Held(t) / Math.Max(1, held.Hold), "tab:Campaign")]);
        if (Victory.Ready(t) || City.NextTier(t) == null)
        {
            var road = Victory.Roads(t).OrderByDescending(d => Victory.Progress(t, d)).ThenBy(d => d.Id, StringComparer.Ordinal).FirstOrDefault();
            if (road == null) return ("", []);
            var terms = Victory.Terms(t, road).Where(x => !x.Met).Take(Most).ToList();
            if (terms.Count == 0)
                return (road.Name, [new(road.Icon, Victory.Ready(t) ? $"Claim {road.Name}" : $"{road.Name} holds", "", 1, Victory.Ready(t) ? "tab:Campaign" : "")]);
            return (road.Name, terms.Select(x => new Aim(road.Icon, x.Label, x.AtMost ? $"{Js.Str(x.Have)} (at most {Js.Str(x.Need)})" : $"{Js.Str(x.Have)}/{Js.Str(x.Need)}", x.Share)).ToList());
        }
        var next = City.NextTier(t)!;
        var aims = new List<Aim>();
        if (t.Population < next.Residents) aims.Add(new("population", "Households", $"{t.Population}/{next.Residents}", (double)t.Population / next.Residents));
        foreach (string id in next.Requires)
            if (!City.Has(t, id))
            {
                var b = t.Content.Building(id);
                bool rising = t.Buildings.Any(x => x.Def.Id == id && x.IsSite);
                aims.Add(new("tools", $"{Agent.Article(b.Name)} {b.Name.ToLowerInvariant()}", rising ? "rising" : "", rising ? 0.5 : 0));
            }
        if (next.Walls != "" && !City.WallsStand(t, next.Walls))
            aims.Add(new("defence", next.Walls == "CityWall" ? "The city wall" : "Stone walls", "", 0, "tab:Defence"));
        if (t.Treasury < next.Cost) aims.Add(new("gold", "Charter fee", $"{Js.Str(Math.Floor(t.Treasury))}/{Js.Str(next.Cost)}g", t.Treasury / next.Cost));
        if (aims.Count == 0) aims.Add(new("decree", $"Claim the {next.Name.ToLowerInvariant()} charter", "", 1, "tab:City"));
        return ($"Next charter: {next.Name}", aims.Take(Most).ToList());
    }
}
