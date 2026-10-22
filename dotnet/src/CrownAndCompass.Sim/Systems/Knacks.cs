namespace CrownAndCompass.Sim.Systems;

/// <summary>Something a creature does in a fight because its stat block says so (content/game/knacks.json).</summary>
public sealed record KnackDef
{
    public string Id { get; init; } = "";

    /// <summary>Trait or action names that give it ("Pack Tactics"), and ones that look alike but don't ("Web Walker").</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Not { get; init; } = [];

    public int Hit { get; init; }
    public int Ac { get; init; }
    public double Share { get; init; }
    public int Rounds { get; init; }
    public int Dc { get; init; }

    /// <summary>The most foes in one fight that use it (power).</summary>
    public int Most { get; init; }

    public string Text { get; init; } = "";
    public string Laid { get; init; } = "";
    public string Held { get; init; } = "";
    public string Shaken { get; init; } = "";
    public string Freed { get; init; } = "";
    public string Spotted { get; init; } = "";
}

public sealed record KnackRules
{
    public IReadOnlyList<KnackDef> Knacks { get; init; } = [];
}

/// <summary>A creature's knacks, and the power it saves against if it has one.</summary>
public sealed class KnackSet
{
    public static readonly KnackSet None = new([], null);

    private readonly Dictionary<string, KnackDef> _by;

    public KnackSet(IReadOnlyList<KnackDef> knacks, LibPower? power)
    {
        All = knacks;
        Power = power;
        _by = knacks.ToDictionary(k => k.Id);
    }

    public IReadOnlyList<KnackDef> All { get; }

    /// <summary>Its strongest power (a breath, a gaze), with the power knack's numbers.</summary>
    public LibPower? Power { get; }

    public KnackDef? this[string id] => _by.GetValueOrDefault(id);

    public bool Has(string id) => _by.ContainsKey(id);

    public int Ac => All.Sum(k => k.Id is "flyby" or "slippery" ? k.Ac : 0);
}

/// <summary>
/// Each creature fights its own way, read off its stat block: wolves with pack tactics, a dragon's breath, a
/// basilisk's gaze, a purple worm's gullet, a troll that knits back together, a ghost blades pass through. Kinds of
/// foe add their habits on top (<see cref="Tactics"/>); <see cref="Expedition"/> plays both out in a delve.
/// </summary>
public static class Knacks
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Content, Dictionary<string, KnackSet>> Cache = new();

    /// <summary>A fighting creature's knacks: from its library entry's trait and action names.</summary>
    public static KnackSet Of(Content content, MonsterDef m)
    {
        var cache = Cache.GetOrCreateValue(content);
        lock (cache)
        {
            if (cache.TryGetValue(m.Id, out var had)) return had;
            string id = m.Ref != "" ? m.Ref : m.Id.EndsWith("@boss", StringComparison.Ordinal) ? m.Id[..^5] : m.Id;
            var lm = content.Library.Monster(id);
            var set = lm == null ? KnackSet.None : Read(content.Knacks, lm);
            cache[m.Id] = set;
            return set;
        }
    }

    public static KnackSet Read(KnackRules rules, LibMonster lm)
    {
        var names = lm.Traits.Concat(lm.Abilities).ToList();
        bool Named(string n) => names.Any(x => x.Contains(n, StringComparison.OrdinalIgnoreCase));
        var list = new List<KnackDef>();
        LibPower? power = null;
        foreach (var k in rules.Knacks)
        {
            if (k.Id == "power")
            {
                power = lm.Powers.FirstOrDefault();
                if (power != null) list.Add(k);
                continue;
            }
            // "Swarm" the trait, never the "Swarm of Bats" it would match in an action name.
            bool has = k.Id == "swarm" ? lm.Traits.Any(t => t.StartsWith("Swarm", StringComparison.Ordinal) && !k.Not.Any(t.StartsWith)) : k.Names.Any(Named);
            if (has && k.Not.Count > 0 && names.Where(x => k.Names.Any(n => x.Contains(n, StringComparison.OrdinalIgnoreCase))).All(x => k.Not.Any(n => x.Contains(n, StringComparison.OrdinalIgnoreCase)))) has = false;
            if (has) list.Add(k);
        }
        return new KnackSet(list, power);
    }

    /// <summary>A knack's line with its names filled in.</summary>
    public static string Fill(string text, string foe, string hero = "", string power = "") =>
        text.Replace("{foe}", foe).Replace("{hero}", hero).Replace("{power}", power);
}
