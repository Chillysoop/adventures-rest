namespace CrownAndCompass.Sim.Systems;

/// <summary>A score's names and the bonuses one people or kind brings to it.</summary>
public sealed record CharacterKindDef
{
    /// <summary>Score slots renamed for this people or kind ("lore": "Craft").</summary>
    public Dictionary<string, string> Names { get; init; } = [];
    public Dictionary<string, int> Bonus { get; init; } = [];
}

/// <summary>A size a settlement can be, and what it adds to its scores.</summary>
public sealed record SettlementSizeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Weight { get; init; } = 1;
    public Dictionary<string, int> Scores { get; init; } = [];
}

/// <summary>A custom a settlement keeps (a mirror by every door): a line, score bonuses, and menace kinds it wards against.</summary>
public sealed record CustomDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public Dictionary<string, int> Scores { get; init; } = [];

    /// <summary>Menace kinds whose shadow deepens slower over a settlement that keeps it.</summary>
    public IReadOnlyList<string> Wards { get; init; } = [];
}

/// <summary>What a settlement is there for, from forestry to guarding a sealed relic.</summary>
public sealed record PurposeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public Dictionary<string, int> Scores { get; init; } = [];

    /// <summary>Peoples who would live by it; empty for any.</summary>
    public IReadOnlyList<string> Peoples { get; init; } = [];

    /// <summary>The good it lives by, which its people always offer along a trade route.</summary>
    public Good? Good { get; init; }
}

/// <summary>Something a settlement keeps from strangers, told once the town is a friend.</summary>
public sealed record SecretDef
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";

    /// <summary>Only a settlement with this purpose keeps it; empty for any.</summary>
    public string Purpose { get; init; } = "";
}

public sealed record SettlementCharacterRules
{
    public Dictionary<string, string> Names { get; init; } = [];
    public Dictionary<string, CharacterKindDef> Peoples { get; init; } = [];

    /// <summary>What a ruler's temperament adds to the scores.</summary>
    public Dictionary<string, Dictionary<string, int>> Tempers { get; init; } = [];

    /// <summary>Per point of Diplomacy from the middle: more or less standing from every good turn.</summary>
    public double Diplomacy { get; init; } = 0.04;

    /// <summary>Per point of Public Health below the middle: how much faster an illness trouble runs.</summary>
    public double Health { get; init; } = 0.06;
    public IReadOnlyList<string> Illness { get; init; } = [];

    /// <summary>Per point of Faith below the middle: how much faster an enemy's shadow deepens.</summary>
    public double Faith { get; init; } = 0.04;

    /// <summary>Per point of Education from the middle: the band's odds.</summary>
    public double Lore { get; init; } = 0.01;

    /// <summary>A custom that wards against the kind leaves this share of the shadow's pace.</summary>
    public double Warded { get; init; } = 0.5;
    public IReadOnlyList<SettlementSizeDef> Sizes { get; init; } = [];
    public IReadOnlyList<CustomDef> Customs { get; init; } = [];
    public IReadOnlyList<PurposeDef> Purposes { get; init; } = [];
    public IReadOnlyList<SecretDef> Secrets { get; init; } = [];
    public Dictionary<string, IReadOnlyList<string>> Proverbs { get; init; } = [];

    /// <summary>{S}, {proverb}.</summary>
    public string Proverb { get; init; } = "";
}

public sealed record MenaceCharacterRules
{
    public Dictionary<string, string> Names { get; init; } = [];
    public Dictionary<string, CharacterKindDef> Kinds { get; init; } = [];

    /// <summary>Cunning by its wits tier.</summary>
    public Dictionary<string, int> Wits { get; init; } = [];

    /// <summary>Per point of Discipline from the middle: the days between its strikes shrink.</summary>
    public double Will { get; init; } = 0.03;
}

/// <summary>The character sheets of the settlements and organised enemies (content/game/character.json).</summary>
public sealed record CharacterRules
{
    public SettlementCharacterRules Settlement { get; init; } = new();
    public MenaceCharacterRules Menace { get; init; } = new();
}

/// <summary>One line of a character sheet: the slot, the name it goes by for this people or kind, and the score.</summary>
public readonly record struct Score(string Slot, string Name, int Value);

/// <summary>
/// Character sheets for the settlements of the wilds and the organised enemies: six scores from 3 to 18
/// like a hero's, named to fit what they are (a dwarf hold has Craft where a village has Education, a
/// vampire court Patience where an orc warband has Discipline). The scores grow out of the numbers the
/// game already keeps (a settlement's Military is its security, a menace's Might its level), and the rest
/// are rolled per campaign from the seed, never the town's dice. Each settlement also has a size, a
/// purpose, two customs and a secret; each menace kind a temper for the campaign, so orcs can be warlike
/// in one game and sensible in the next. The scores drive what they do: Diplomacy moves standing,
/// Public Health how hard illness hits, Faith how fast a shadow falls, Discipline how often a foe strikes.
/// </summary>
public static class Character
{
    public static readonly string[] SettlementSlots = ["might", "wealth", "diplomacy", "health", "lore", "faith"];
    public static readonly string[] MenaceSlots = ["might", "wits", "reach", "dread", "will", "wealth"];

    private static SettlementCharacterRules Villages(Town town) => town.Content.Character.Settlement;

    private static MenaceCharacterRules Foes(Town town) => town.Content.Character.Menace;

    /// <summary>The settlement's own stream for its sheet: never the town's dice.</summary>
    private static Rng Dice(Town town, int id, int salt) => new(town.Seed * 131 + id * 1931 + salt * 7877 + 41);

    private static int ThreeD6(Rng r) => r.Range(1, 7) + r.Range(1, 7) + r.Range(1, 7);

    private static int Clamp(int v) => Math.Clamp(v, 3, 18);

    /// <summary>A fixed string hash for salts, the same on every machine.</summary>
    private static int Salt(string s)
    {
        unchecked
        {
            int h = 17;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }

    private static T? Pick<T>(Rng r, IReadOnlyList<T> list, Func<T, double> weight) where T : class
    {
        double total = list.Sum(weight);
        if (list.Count == 0 || total <= 0) return null;
        double roll = r.Next() * total;
        foreach (var x in list)
        {
            roll -= weight(x);
            if (roll < 0) return x;
        }
        return list[^1];
    }

    // ---- Settlements ----

    public static SettlementSizeDef? SizeOf(Town town, Settlement s) => Pick(Dice(town, s.Id, 1), Villages(town).Sizes, z => z.Weight);

    public static PurposeDef? PurposeOf(Town town, Settlement s)
    {
        var fit = Villages(town).Purposes.Where(p => p.Peoples.Count == 0 || p.Peoples.Contains(s.Kin.Id)).ToList();
        return Pick(Dice(town, s.Id, 2), fit, _ => 1);
    }

    /// <summary>Its two customs, never the same one twice.</summary>
    public static List<CustomDef> CustomsOf(Town town, Settlement s)
    {
        var pool = Villages(town).Customs.ToList();
        var r = Dice(town, s.Id, 3);
        var got = new List<CustomDef>();
        for (int i = 0; i < 2 && pool.Count > 0; i++)
        {
            var c = pool[r.Range(0, pool.Count)];
            got.Add(c);
            pool.Remove(c);
        }
        return got;
    }

    public static SecretDef? SecretOf(Town town, Settlement s)
    {
        string purpose = PurposeOf(town, s)?.Id ?? "";
        var fit = Villages(town).Secrets.Where(x => x.Purpose == "" || x.Purpose == purpose).ToList();
        // A settlement that guards something always keeps the secret of what it guards.
        var own = fit.Where(x => x.Purpose != "").ToList();
        return own.Count > 0 ? own[0] : Pick(Dice(town, s.Id, 4), fit, _ => 1);
    }

    /// <summary>Strangers aren't told: an ally, or a friend among its people, hears it.</summary>
    public static bool SecretKnown(Town town, Settlement s) =>
        s.Allied || Neighbours.CastOf(town, s).Any(c => c.Regard >= town.Content.Neighbours.Cast.Regard);

    /// <summary>A new ally's leader confides what the settlement keeps from strangers, with one of its sayings.</summary>
    public static void Allied(Town town, Settlement s)
    {
        if (SecretOf(town, s) is not { } secret) return;
        string saying = Proverb(town, s, s.Id) is string p ? " " + p : "";
        town.Log(LedgerCategory.Town, Importance.Notable, $"Among friends now, {s.Leader} of {s.Name} confides that {Fill(s, secret.Text)}.{saying}");
    }

    public static string Fill(Settlement s, string text) => text.Replace("{S}", s.Name);

    /// <summary>One of its people's sayings, the nth (wrapping), or null when they have none.</summary>
    public static string? Proverb(Town town, Settlement s, int n)
    {
        var r = Villages(town);
        if (!r.Proverbs.TryGetValue(s.Kin.Id, out var list) || list.Count == 0 || r.Proverb == "") return null;
        return Fill(s, r.Proverb).Replace("{proverb}", list[((n % list.Count) + list.Count) % list.Count]);
    }

    private static string SettlementName(Town town, Settlement s, string slot) =>
        Villages(town).Peoples.GetValueOrDefault(s.Kin.Id)?.Names.GetValueOrDefault(slot)
        ?? Villages(town).Names.GetValueOrDefault(slot) ?? slot;

    public static int Score(Town town, Settlement s, string slot)
    {
        var r = Villages(town);
        int v = slot switch
        {
            // Military and Wealth are what the settlement's security and prosperity are now.
            "might" => 3 + (int)Math.Round(s.Security * 0.15),
            "wealth" => 3 + (int)Math.Round(s.Prosperity * 0.15),
            _ => ThreeD6(Dice(town, s.Id, 10 + Array.IndexOf(SettlementSlots, slot))),
        };
        if (r.Peoples.GetValueOrDefault(s.Kin.Id) is { } kin) v += kin.Bonus.GetValueOrDefault(slot);
        if (s.Reign != null && r.Tempers.GetValueOrDefault(Reigns.TemperOf(town, s).Id) is { } t) v += t.GetValueOrDefault(slot);
        v += SizeOf(town, s)?.Scores.GetValueOrDefault(slot) ?? 0;
        v += PurposeOf(town, s)?.Scores.GetValueOrDefault(slot) ?? 0;
        foreach (var c in CustomsOf(town, s)) v += c.Scores.GetValueOrDefault(slot);
        return Clamp(v);
    }

    public static List<Score> Sheet(Town town, Settlement s) =>
        SettlementSlots.Select(x => new Score(x, SettlementName(town, s, x), Score(town, s, x))).ToList();

    /// <summary>Good turns move a diplomatic people further, a closed one less.</summary>
    public static double Diplomacy(Town town, Settlement s) =>
        Math.Max(0.2, 1 + (Score(town, s, "diplomacy") - 10.5) * Villages(town).Diplomacy);

    /// <summary>How fast a trouble runs there: illness runs faster where Public Health is poor.</summary>
    public static double Trouble(Town town, Settlement s, string trouble) =>
        Villages(town).Illness.Contains(trouble) ? Math.Max(0.3, 1 + (10.5 - Score(town, s, "health")) * Villages(town).Health) : 1;

    /// <summary>How fast a menace's shadow deepens there: faith and customs against its kind hold it back.</summary>
    public static double Shadow(Town town, Settlement s, string kind)
    {
        var r = Villages(town);
        double k = Math.Max(0.3, 1 + (10.5 - Score(town, s, "faith")) * r.Faith);
        if (CustomsOf(town, s).Any(c => c.Wards.Contains(kind))) k *= r.Warded;
        return k;
    }

    /// <summary>Its band's odds, nudged by how much its people know.</summary>
    public static double Band(Town town, Settlement s) => (Score(town, s, "lore") - 10.5) * Villages(town).Lore;

    // ---- Menaces ----

    /// <summary>The kind's temper this campaign: a pure function of the seed, so it never moves the town's dice.</summary>
    public static HistoryTemper? TemperOf(Town town, string kind)
    {
        string subject = "menace:" + kind;
        var fit = town.Content.History.Tempers.Where(t => t.Subjects.Contains(subject)).ToList();
        return Pick(new Rng(town.Seed * 197 + Salt(kind)), fit, t => t.Weight);
    }

    /// <summary>How much likelier its kind's temper makes an ambition.</summary>
    public static double Ambition(Town town, string kind, string ambition) =>
        TemperOf(town, kind)?.Ambitions.GetValueOrDefault(ambition, 1) ?? 1;

    private static string MenaceName(Town town, Menace m, string slot) =>
        Foes(town).Kinds.GetValueOrDefault(m.Kind)?.Names.GetValueOrDefault(slot)
        ?? Foes(town).Names.GetValueOrDefault(slot) ?? slot;

    public static int Score(Town town, Menace m, string slot)
    {
        var r = Foes(town);
        int v = slot switch
        {
            "might" => 2 + m.Level,
            "wits" => r.Wits.GetValueOrDefault(m.Cunning, 10) + Dice(town, m.Id, 50).Range(-1, 2),
            "reach" => 3 + 2 * m.Holdings.Count + 3 * m.Domain + (int)m.Stage,
            "dread" => 4 + 3 * (int)m.Stage + Math.Min(3, m.Memories.Count),
            "will" => ThreeD6(Dice(town, m.Id, 51)) + (TemperOf(town, m.Kind)?.Will ?? 0),
            "wealth" => ThreeD6(Dice(town, m.Id, 52)) / 2 + 2 * m.Assets.Count + m.Holdings.Count,
            _ => 10,
        };
        v += r.Kinds.GetValueOrDefault(m.Kind)?.Bonus.GetValueOrDefault(slot) ?? 0;
        return Clamp(v);
    }

    public static List<Score> Sheet(Town town, Menace m) =>
        MenaceSlots.Select(x => new Score(x, MenaceName(town, m, x), Score(town, m, x))).ToList();

    /// <summary>Times the days between its strikes: its temper, and its Discipline (a disciplined host strikes more often).</summary>
    public static double Strike(Town town, Menace m) =>
        (TemperOf(town, m.Kind)?.Strike ?? 1) * Math.Max(0.5, 1 + (10.5 - Score(town, m, "will")) * Foes(town).Will);
}
