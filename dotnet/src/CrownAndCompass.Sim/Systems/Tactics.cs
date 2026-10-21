namespace CrownAndCompass.Sim.Systems;

/// <summary>How one kind of foe fights (content/game/tactics.json).</summary>
public sealed record TacticDef
{
    public string Id { get; init; } = "";

    /// <summary>Words in a creature's id that pick it ("goblin" picks goblin_boss and goblin-warrior).</summary>
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>Library creature types it covers ("undead").</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>Below this share of its hp a foe breaks and runs; 0 never.</summary>
    public double Flee { get; init; }
    public string Fled { get; init; } = "";

    /// <summary>Extra AC from cover in the first rounds.</summary>
    public int Cover { get; init; }
    public int CoverRounds { get; init; }

    /// <summary>Up to this much to hit from other packmates still standing.</summary>
    public int Pack { get; init; }

    /// <summary>A boss's attack on every standing hero in round 2: {boss}.</summary>
    public string Signature { get; init; } = "";

    /// <summary>A boss calls more of this creature in round 3: {boss}, {n}.</summary>
    public string Minions { get; init; } = "";
    public int[] MinionCount { get; init; } = [2, 3];
    public string Called { get; init; } = "";

    /// <summary>A proud boss a hero with Charisma this high can goad into talking: {hero}, {boss}.</summary>
    public int Pride { get; init; }
    public string Goaded { get; init; } = "";
}

public sealed record TacticRules
{
    public IReadOnlyList<TacticDef> Tactics { get; init; } = [];
}

/// <summary>
/// Each kind of foe fights its own way: goblins shoot from cover and scatter when hurt, wolves hunt as a
/// pack, outlaws run once the fight turns. Bosses fight like bosses: a signature attack on the whole
/// party, minions called in, and a dragon's pride that a silver-tongued hero can use against it.
/// </summary>
public static class Tactics
{
    private static readonly char[] Split = ['-', '_', '@', ' '];

    /// <summary>The tactic for a creature, if any: its id's words first, then its type.</summary>
    public static TacticDef? Of(Content content, MonsterDef m)
    {
        var words = m.Id.Split(Split, StringSplitOptions.RemoveEmptyEntries);
        var list = content.Tactics.Tactics;
        return list.FirstOrDefault(t => t.Words.Any(words.Contains)) ?? list.FirstOrDefault(t => m.Type != "" && t.Types.Contains(m.Type));
    }
}
