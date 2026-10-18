namespace CrownAndCompass.Sim.Systems;

/// <summary>Something a choice can stand for (mercy, a hard hand, profit...), which tempers like or dislike.</summary>
public sealed record RegardTag
{
    public string Id { get; init; } = "";

    /// <summary>How the hero inspector says it: "likes mercy and an open purse".</summary>
    public string Name { get; init; } = "";
}

/// <summary>What a hero cares about in a mayor: the tags they like and dislike, and who tends to have it.</summary>
public sealed record TemperDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public List<string> Likes { get; init; } = [];
    public List<string> Dislikes { get; init; } = [];

    /// <summary>Weight by class id; every temper also has a weight of 1 for anyone.</summary>
    public Dictionary<string, double> Classes { get; init; } = [];

    /// <summary>Weight by trait name (Greedy, Generous...).</summary>
    public Dictionary<string, double> Traits { get; init; } = [];
}

/// <summary>How heroes judge the mayor's choices (content/game/regard.json).</summary>
public sealed record RegardRules
{
    /// <summary>Regard a choice moves per tag the hero likes, or loses per tag they dislike.</summary>
    public double Step { get; init; } = 6;

    /// <summary>Share of regard kept each day: old choices fade.</summary>
    public double Decay { get; init; } = 0.985;

    /// <summary>Loyalty points per point of regard, up to MaxLoyalty either way.</summary>
    public double Loyalty { get; init; } = 0.3;
    public double MaxLoyalty { get; init; } = 15;

    /// <summary>Regard at which a hero trusts the mayor with their personal quest, and at which they come with a grievance.</summary>
    public double Admire { get; init; } = 30;
    public double Aggrieved { get; init; } = -30;
    public int PersonalLevel { get; init; } = 3;

    /// <summary>Added to a party's check when the hero making it has a friend beside them, or a rival.</summary>
    public int Friend { get; init; } = 2;
    public int Rival { get; init; } = -2;

    /// <summary>How far the stand-in mayor leans toward choices the heroes would like (the share pleased less the share angered, times this).</summary>
    public double StandIn { get; init; } = 0.4;
    public List<RegardTag> Tags { get; init; } = [];
    public List<TemperDef> Tempers { get; init; } = [];

    public TemperDef? Temper(string id) => Tempers.FirstOrDefault(t => t.Id == id);
    public RegardTag? Tag(string id) => Tags.FirstOrDefault(t => t.Id == id);
}

/// <summary>
/// Heroes judge the mayor. Each hero has a temper (merciful, ruthless, pious, mercenary, lawful,
/// free-spirited, bold) settled by their class and traits. Story choices, and later the terms of bounties
/// and the law's verdicts, carry tags; every hero in town likes or dislikes what a choice stands for, and
/// their regard moves. Regard feeds loyalty, so the heroes a mayor keeps disgusting are the ones who
/// desert or turn. A hero who thinks well of the mayor trusts them with a personal quest (a story of their
/// own); one pushed too far comes to the mayor with a grievance.
/// </summary>
public static class Regard
{
    private static RegardRules Rules(Town town) => town.Content.Regard;

    public static TemperDef? TemperOf(Town town, Agent a) => a.Hero is { } s ? Rules(town).Temper(s.Temper) : null;

    /// <summary>Settle a hero's temper on arrival (and for heroes from a save older than tempers).</summary>
    public static void Arrive(Town town, Agent a)
    {
        var r = Rules(town);
        var s = a.Hero!;
        if (s.Temper != "" || r.Tempers.Count == 0) return;
        var weights = r.Tempers.Select(t => 1 + t.Classes.GetValueOrDefault(s.Cls.Id) + Js.Sum(a.Traits.OrderBy(x => x), x => t.Traits.GetValueOrDefault(x.ToString()))).ToList();
        // Who they are settles it, not a fresh roll: the hero's id and the town's seed pick from the weights.
        uint h = unchecked((uint)(a.Id * 2654435761u ^ (uint)town.Seed * 40503u));
        h ^= h >> 15;
        h = unchecked(h * 2246822519u);
        h ^= h >> 13;
        double roll = h / (double)uint.MaxValue * Js.Sum(weights, w => w);
        int pick = r.Tempers.Count - 1;
        for (int i = 0; i < weights.Count; i++)
        {
            roll -= weights[i];
            if (roll < 0)
            {
                pick = i;
                break;
            }
        }
        s.Temper = r.Tempers[pick].Id;
    }

    /// <summary>How much a choice with these tags moves this hero: + for each tag liked, − for each disliked.</summary>
    public static double Moves(Town town, Agent a, IReadOnlyCollection<string> tags)
    {
        if (tags.Count == 0 || TemperOf(town, a) is not { } t) return 0;
        return Rules(town).Step * (tags.Count(t.Likes.Contains) - tags.Count(t.Dislikes.Contains));
    }

    /// <summary>The heroes in town a choice with these tags would please and anger, in id order.</summary>
    public static (List<Agent> Pleased, List<Agent> Angered) Reactions(Town town, IReadOnlyCollection<string> tags)
    {
        var pleased = new List<Agent>();
        var angered = new List<Agent>();
        if (tags.Count == 0) return (pleased, angered);
        foreach (var a in town.Heroes.OrderBy(a => a.Id))
        {
            double v = Moves(town, a, tags);
            if (v > 0) pleased.Add(a);
            else if (v < 0) angered.Add(a);
        }
        return (pleased, angered);
    }

    /// <summary>How much the stand-in mayor leans toward a choice for what the heroes in town would make of it: the share pleased less the share angered, weighted.</summary>
    public static double Sway(Town town, IReadOnlyCollection<string> tags)
    {
        if (tags.Count == 0) return 0;
        var (pleased, angered) = Reactions(town, tags);
        int heroes = town.Heroes.Count;
        return heroes == 0 ? 0 : Rules(town).StandIn * (pleased.Count - angered.Count) / heroes;
    }

    /// <summary>
    /// Every hero of the town hears what the mayor chose (<paramref name="what"/>: "chose to keep the gates
    /// shut", over <paramref name="about"/>: "Refugees at the gate") and judges it by their temper. One Ledger
    /// line says who approved and who did not.
    /// </summary>
    public static void Judge(Town town, IReadOnlyCollection<string> tags, string what, string about)
    {
        var (pleased, angered) = Reactions(town, tags);
        foreach (var a in pleased.Concat(angered))
        {
            var s = a.Hero!;
            double v = Moves(town, a, tags);
            s.Regard = Math.Clamp(s.Regard + v, -100, 100);
            s.RegardWhy = what;
            s.RegardPleased = v > 0;
        }
        if (pleased.Count + angered.Count == 0) return;
        var parts = new List<string>();
        if (pleased.Count > 0) parts.Add($"{Names(pleased)} {(pleased.Count == 1 ? "approves" : "approve")}");
        if (angered.Count > 0) parts.Add($"{Names(angered)} {(angered.Count == 1 ? "disapproves" : "disapprove")}");
        town.Log(LedgerCategory.Quest, Importance.Routine, $"{about}: word gets round that the mayor {what}. {Stories.Cap(string.Join("; ", parts))}.", pleased.Concat(angered).Select(a => a.Id).ToList());
    }

    private static string Names(List<Agent> heroes)
    {
        var names = heroes.Take(3).Select(a => a.GivenName).ToList();
        if (heroes.Count > 3) names.Add($"{heroes.Count - 3} more");
        return names.Count == 1 ? names[0] : $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }

    /// <summary>Change one hero's regard directly (a story about them).</summary>
    public static void Esteem(Agent a, double v)
    {
        if (a.Hero is { } s) s.Regard = Math.Clamp(s.Regard + v, -100, 100);
    }

    /// <summary>The loyalty a hero's regard adds or takes away, with why.</summary>
    public static (string Why, double Points) Loyalty(Town town, Agent a)
    {
        var r = Rules(town);
        double v = a.Hero!.Regard;
        double pts = Math.Clamp(v * r.Loyalty, -r.MaxLoyalty, r.MaxLoyalty);
        return (v >= 0 ? "approves of the mayor" : "disapproves of the mayor", pts);
    }

    /// <summary>Old choices fade, and heroes without a temper (from an older save) settle on one.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var a in town.Heroes)
        {
            Arrive(town, a);
            a.Hero!.Regard *= r.Decay;
        }
    }

    /// <summary>Whether a hero thinks well enough of the mayor to ask for help with their own trouble.</summary>
    public static bool Admirer(Town town, Agent a) =>
        a.Hero is { } s && !a.IsAway && s.Personal == "" && s.Regard >= Rules(town).Admire && s.Level >= Rules(town).PersonalLevel
        && town.Stories.Open.All(x => x.HeroId != a.Id);

    /// <summary>Whether a hero has had enough of the mayor's choices to say so to their face.</summary>
    public static bool Aggrieved(Town town, Agent a) =>
        a.Hero is { } s && !a.IsAway && s.Regard <= Rules(town).Aggrieved && s.RegardWhy != "" && !s.RegardPleased
        && town.Stories.Open.All(x => x.HeroId != a.Id);

    /// <summary>How the inspector sums a hero up: "merciful (likes mercy and an open purse; dislikes a hard hand)".</summary>
    public static string Describe(Town town, TemperDef t)
    {
        var r = Rules(town);
        string Say(List<string> ids) => And(ids.Select(id => r.Tag(id)?.Name ?? id).ToList());
        string likes = t.Likes.Count > 0 ? $"likes {Say(t.Likes)}" : "";
        string dislikes = t.Dislikes.Count > 0 ? $"dislikes {Say(t.Dislikes)}" : "";
        return string.Join("; ", new[] { likes, dislikes }.Where(x => x != ""));
    }

    private static string And(List<string> items) =>
        items.Count <= 1 ? string.Join("", items) : $"{string.Join(", ", items.Take(items.Count - 1))} and {items[^1]}";

    // Party chemistry ------------------------------------------------------------------------------

    /// <summary>
    /// What a hero's companions on the road add to a check they make: a friend beside them steadies the
    /// hand, a rival in the party spoils it. Returns the bonus and who it is owed to, or (0, "").
    /// </summary>
    public static (int Bonus, string With) Chemistry(RegardRules r, Agent who, IEnumerable<Agent> party)
    {
        Agent? friend = null, rival = null;
        foreach (var o in party.Where(o => o.Id != who.Id).OrderBy(o => o.Id))
        {
            if (friend == null && who.OpinionOf(o.Id) >= Social.FriendThreshold && o.OpinionOf(who.Id) >= Social.FriendThreshold) friend = o;
            if (rival == null && who.OpinionOf(o.Id) <= Social.RivalThreshold) rival = o;
        }
        if (friend != null) return (r.Friend, $"with {friend.GivenName} at their side");
        if (rival != null) return (r.Rival, $"with {rival.GivenName} sniping at them");
        return (0, "");
    }
}
