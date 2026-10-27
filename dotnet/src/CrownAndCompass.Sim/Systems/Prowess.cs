namespace CrownAndCompass.Sim.Systems;

/// <summary>How a fighting spell plays in a delve (content/game/prowess.json); the library gives its dice, type and save.</summary>
public sealed record SpellPlay
{
    public string Id { get; init; } = "";

    /// <summary>cantrip, bolt, blast, heal, buff, hold, mark or aura.</summary>
    public string Kind { get; init; } = "";
    public int Targets { get; init; } = 1;

    /// <summary>Rays or darts, each its own roll (scorching ray, magic missile); for a cantrip, rays that grow with level instead of dice.</summary>
    public int Beams { get; init; }

    /// <summary>Never misses (magic missile).</summary>
    public bool Auto { get; init; }
    public int Rounds { get; init; }
    public int Hit { get; init; }
    public int Ac { get; init; }
    public int Hp { get; init; }
    public int Attacks { get; init; }

    /// <summary>held (no turns) or hindered (easier to hit, worse at hitting).</summary>
    public string Effect { get; init; } = "held";

    /// <summary>Creature types it works on (hold person: humanoids); empty for any.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>The condition it lays, which creatures immune to it ignore.</summary>
    public string Condition { get; init; } = "";

    /// <summary>Hit points above which a foe shrugs it off (sleep), 0 for none.</summary>
    public int Most { get; init; }

    /// <summary>Dice added for each slot level above the spell's own; -1 for the usual one die.</summary>
    public int Upcast { get; init; } = -1;

    /// <summary>Dice when the library entry has none (a poultice).</summary>
    public string Dice { get; init; } = "";
}

/// <summary>A beast a druid can become (prowess.json wild-shape).</summary>
public sealed record ShapeDef
{
    public int Level { get; init; }
    public string Ref { get; init; } = "";
}

/// <summary>A class's signature power (prowess.json powers): when it opens, how often, and its numbers.</summary>
public sealed record ClassPower
{
    public string Id { get; init; } = "";
    public string Class { get; init; } = "";
    public int Level { get; init; }

    /// <summary>[level, uses] steps; empty for a power that is always on.</summary>
    public IReadOnlyList<int[]> Uses { get; init; } = [];

    /// <summary>long, short, or slots (spends the hero's spell slots).</summary>
    public string Rest { get; init; } = "";

    /// <summary>Uses come back on a short rest from this level (bardic inspiration).</summary>
    public int ShortFrom { get; init; }
    public IReadOnlyList<int[]> Bonus { get; init; } = [];
    public IReadOnlyList<int[]> Die { get; init; } = [];
    public int Rises { get; init; }
    public string Dice { get; init; } = "";
    public string Type { get; init; } = "";

    /// <summary>Hit points a level in the pool (lay on hands).</summary>
    public int Pool { get; init; }
    public string Save { get; init; } = "";
    public int Rounds { get; init; }
    public int Sear { get; init; }
    public int Hit { get; init; }
    public int Weight { get; init; }
    public string Spell { get; init; } = "";
    public IReadOnlyList<ShapeDef> Shapes { get; init; } = [];
    public string Text { get; init; } = "";
    public string Rose { get; init; } = "";
    public string Used { get; init; } = "";
    public string Ended { get; init; } = "";
}

public sealed record HarmRules
{
    public double Resist { get; init; } = 0.5;
    public double Immune { get; init; }
    public double Vulnerable { get; init; } = 2;
    public double SteelVsImmune { get; init; } = 0.5;
}

public sealed record AimRules
{
    public int BeastFront { get; init; } = 3;
    public int DimFront { get; init; } = 2;
    public int CleverSoft { get; init; } = 3;
    public int CleverBloodied { get; init; } = 2;
    public int CleverAc { get; init; } = 14;
    public int Clever { get; init; } = 8;
    public int Dim { get; init; } = 4;
}

public sealed record ProwessRules
{
    /// <summary>The least DC of the Constitution save to keep a spell held by concentration when hurt.</summary>
    public int ConcentrationDc { get; init; } = 10;

    public IReadOnlyDictionary<string, int> Attacks { get; init; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int[]> ExtraAttacks { get; init; } = new Dictionary<string, int[]>();
    public IReadOnlyList<int> CantripSteps { get; init; } = [];
    public IReadOnlyDictionary<string, int[]> Slots { get; init; } = new Dictionary<string, int[]>();
    public int KeepForHeals { get; init; }
    public int SpendFoes { get; init; } = 3;

    /// <summary>Spells to steady the party (bless, haste) cast in one fight at most.</summary>
    public int Buffs { get; init; } = 2;
    public IReadOnlyList<SpellPlay> Spells { get; init; } = [];
    public IReadOnlyList<ClassPower> Powers { get; init; } = [];
    public IReadOnlyDictionary<string, string> Lines { get; init; } = new Dictionary<string, string>();
    public HarmRules Damage { get; init; } = new();
    public int RegenRises { get; init; }
    public AimRules Targeting { get; init; } = new();

    public ClassPower? Power(string id) => Powers.FirstOrDefault(p => p.Id == id);
}

/// <summary>
/// Heroes fight the way their class does at their level, D&amp;D style: more attacks a turn (Extra Attack), cantrips
/// that grow, spell slots spent on the spell the fight wants (a fireball into a crowd, hold monster on a giant,
/// bless before a boss), and each class's signature power (rage, smite, action surge, wild shape, inspiration).
/// Damage types matter both ways: resistances, immunities and weaknesses from the library, fire that stops a troll
/// knitting, silver or magic for a werewolf. <see cref="Expedition"/> plays it all out; this reads the rules.
/// </summary>
public static class Prowess
{
    public static ProwessRules Rules(Content c) => c.Prowess;

    /// <summary>The value of the last [level, value] step at or below the level; 0 below the first.</summary>
    public static int At(IReadOnlyList<int[]> steps, int level)
    {
        int v = 0;
        foreach (var s in steps)
            if (s.Length >= 2 && s[0] <= level) v = s[1];
        return v;
    }

    /// <summary>Weapon attacks a turn: the library's Extra Attack features, or the levels our own classes gain one.</summary>
    public static int Attacks(Content c, HeroSheet s)
    {
        var r = Rules(c);
        int n = 1;
        if (c.Library.Class(s.Cls.Id) is { } lc)
            foreach (var f in lc.Features)
                if (f.Level <= s.Level && r.Attacks.TryGetValue(f.Name, out int a)) n = Math.Max(n, a);
        if (r.ExtraAttacks.TryGetValue(s.Cls.Id, out var levels)) n = Math.Max(n, 1 + levels.Count(l => l <= s.Level));
        return n;
    }

    /// <summary>Dice a cantrip rolls at this level (two at 5th, three at 11th, four at 17th).</summary>
    public static int CantripDice(Content c, int level) => 1 + Rules(c).CantripSteps.Count(l => l <= level);

    /// <summary>Spell slots a long rest (pact magic: a short rest).</summary>
    public static int Casts(Content c, ClassDef cls, int level) =>
        cls.Magic != "" && Rules(c).Slots.TryGetValue(cls.Magic, out var t) && t.Length > 0 ? t[Math.Clamp(level, 1, t.Length) - 1] : 0;

    /// <summary>The highest slot a caster of this level has.</summary>
    public static int SlotTop(ClassDef cls, int level) => Moments.SpellLevel(cls.Magic, level);

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Content, Dictionary<string, List<(SpellPlay Play, LibSpell Spell)>>> Known = new();

    /// <summary>The fighting spells a class knows, cantrips first, then by level.</summary>
    public static IReadOnlyList<(SpellPlay Play, LibSpell Spell)> Spells(Content c, ClassDef cls)
    {
        var cache = Known.GetOrCreateValue(c);
        lock (cache)
        {
            if (cache.TryGetValue(cls.Id, out var had)) return had;
            var list = cls.Magic == "" && !cls.Spell ? [] : Rules(c).Spells
                .Select(p => (Play: p, Spell: c.Library.Spell(p.Id)))
                .Where(x => x.Spell != null && x.Spell.Classes.Contains(cls.Id))
                .Select(x => (x.Play, x.Spell!))
                .OrderBy(x => x.Item2.Level)
                .ThenBy(x => x.Play.Id, StringComparer.Ordinal)
                .ToList();
            cache[cls.Id] = list;
            return list;
        }
    }

    /// <summary>The fighting spells a hero can cast now: cantrips, and spells up to their highest slot.</summary>
    public static IEnumerable<(SpellPlay Play, LibSpell Spell)> Castable(Content c, HeroSheet s)
    {
        int top = SlotTop(s.Cls, s.Level);
        return Spells(c, s.Cls).Where(x => x.Spell.Level == 0 ? s.Cls.Spell || x.Play.Kind == "cantrip" : x.Spell.Level <= top && Casts(c, s.Cls, s.Level) > 0);
    }

    /// <summary>The class's signature powers open at the hero's level.</summary>
    public static IEnumerable<ClassPower> Powers(Content c, HeroSheet s) => Rules(c).Powers.Where(p => p.Class == s.Cls.Id && p.Level <= s.Level);

    public static ClassPower? Power(Content c, HeroSheet s, string id) => Rules(c).Power(id) is { } p && p.Class == s.Cls.Id && p.Level <= s.Level ? p : null;

    /// <summary>The slot a spell is cast at: midway between its own level and the caster's highest.</summary>
    public static int CastAt(LibSpell sp, int top) => Math.Max(sp.Level, (sp.Level + top + 1) / 2);

    /// <summary>A spell's dice cast at a slot: one more die (or the play's upcast) for each level above its own.</summary>
    public static Dice? DiceAt(SpellPlay p, LibSpell sp, int slot)
    {
        var d = Sim.Dice.Parse(p.Dice) ?? sp.Dice;
        if (d is not { } x) return null;
        int up = p.Upcast < 0 ? 1 : p.Upcast;
        return x with { Count = x.Count + Math.Max(0, slot - sp.Level) * up };
    }

    public static double Avg(Dice? d) => d is { } x ? x.Count * (x.Die + 1) / 2.0 + x.Bonus : 0;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Content, Dictionary<string, LibMonster?>> Libs = new();

    /// <summary>The library creature a fighting creature is, for its defences, saves and wits; null for one of our own without an entry.</summary>
    public static LibMonster? Lib(Content c, MonsterDef m)
    {
        var cache = Libs.GetOrCreateValue(c);
        lock (cache)
        {
            if (cache.TryGetValue(m.Id, out var had)) return had;
            string id = m.Ref != "" ? m.Ref : m.Id.EndsWith("@boss", StringComparison.Ordinal) ? m.Id[..^5] : m.Id;
            var lm = c.Library.Monster(id);
            cache[m.Id] = lm;
            return lm;
        }
    }

    private static readonly string[] Physical = ["bludgeoning", "piercing", "slashing"];

    public static bool IsPhysical(string type) => Physical.Contains(type);

    /// <summary>
    /// What a creature's defences do to a blow of this type: half for a resistance, nothing for an immunity, double for
    /// a weakness. Silver or magic gets past a hide that turns plain blades; plain steel on one only does steelVsImmune.
    /// </summary>
    public static (int Dmg, string How) Harm(Content c, LibMonster? lm, int dmg, string type, bool magic, bool silver)
    {
        if (lm == null || type == "" || dmg <= 0) return (dmg, "");
        var r = Rules(c).Damage;
        bool phys = IsPhysical(type);
        if (lm.Immune.Contains(type))
        {
            if (phys && (magic || silver)) return (dmg, "");
            if (phys) return ((int)Math.Round(dmg * r.SteelVsImmune), "steel");
            return ((int)Math.Round(dmg * r.Immune), "immune");
        }
        if (lm.Resist.Contains(type) && !(phys && (magic || silver))) return (Math.Max(1, (int)Math.Round(dmg * r.Resist)), "resist");
        if (lm.Vulnerable.Contains(type)) return ((int)Math.Round(dmg * r.Vulnerable), "vulnerable");
        return (dmg, "");
    }

    /// <summary>The share of a blow of this type that gets through, for choosing what to hit with.</summary>
    public static double Share(Content c, LibMonster? lm, string type, bool magic, bool silver) => Harm(c, lm, 100, type, magic, silver).Dmg / 100.0;

    /// <summary>A creature's saving throw bonus: its ability modifier, and its proficiency if it is a boss.</summary>
    public static int SaveBonus(LibMonster? lm, MonsterDef m, string ability)
    {
        int score = lm == null ? 10 : ability switch
        {
            "str" => lm.Str,
            "dex" => lm.Dex,
            "con" => lm.Con,
            "int" => lm.Int,
            "wis" => lm.Wis,
            "cha" => lm.Cha,
            _ => 10,
        };
        int prof = m.Boss ? 2 + (int)Math.Max(0, (lm?.Cr ?? 1) - 1) / 4 : 0;
        return Rng.Modifier(score) + prof;
    }

    /// <summary>The hero's weapon gets past a hide that turns plain blades: a magic weapon, or a silvered one.</summary>
    public static bool Magic(HeroSheet s) => s.WeaponBonus > 0 || s.Magic.ContainsKey("weapon");

    /// <summary>A line from prowess.json with its blanks filled.</summary>
    public static string Line(Content c, string key, params (string Key, string Value)[] fill)
    {
        string s = Rules(c).Lines.GetValueOrDefault(key, "");
        foreach (var (k, v) in fill) s = s.Replace("{" + k + "}", v);
        return s;
    }

    /// <summary>A power's line with its blanks filled.</summary>
    public static string Fill(string text, params (string Key, string Value)[] fill)
    {
        foreach (var (k, v) in fill) text = text.Replace("{" + k + "}", v);
        return text;
    }

    /// <summary>
    /// What the hero card says they fight with, a line each: attacks a turn, cantrip, spell slots, the fighting spells they
    /// know at their best, and their class powers with how often. (icon, short label, hover text).
    /// </summary>
    public static List<(string Icon, string Label, string Tip)> Sheet(Content c, HeroSheet s)
    {
        var list = new List<(string, string, string)>();
        int attacks = Attacks(c, s);
        if (attacks > 1) list.Add(("item-weapon", $"{attacks} attacks a turn", "Extra Attack: more weapon blows each turn as they level"));
        if (s.Silvered) list.Add(("item-weapon", "Silvered weapon", "Bites creatures that shrug off plain steel, like werewolves"));
        var known = Castable(c, s).ToList();
        if (known.Where(k => k.Play.Kind == "cantrip").OrderByDescending(k => Avg(k.Spell.Dice)).ThenBy(k => k.Play.Id, StringComparer.Ordinal).FirstOrDefault() is { Spell: not null } cantrip)
        {
            int dice = CantripDice(c, s.Level);
            list.Add(("sheet-spell", Library.Prose(cantrip.Spell.Name) + (dice > 1 ? $" ×{dice}" : ""), $"Cantrip: cast at will, {dice} {(dice == 1 ? "die" : "dice")} at this level"));
        }
        int casts = Casts(c, s.Cls, s.Level);
        if (casts > 0)
        {
            int top = SlotTop(s.Cls, s.Level);
            list.Add(("sheet-spell", $"{casts} spell slots · up to {Ordinal(top)}", s.Cls.Magic == "pact" ? "Pact slots come back on a short rest" : "Spent on the spell each fight needs; back after a night's sleep"));
            var best = known.Where(k => k.Spell.Level > 0).GroupBy(k => k.Play.Kind)
                .Select(g => g.OrderByDescending(k => k.Spell.Level).ThenBy(k => k.Play.Id, StringComparer.Ordinal).First())
                .OrderByDescending(k => k.Spell.Level).ThenBy(k => k.Play.Id, StringComparer.Ordinal).Take(3);
            foreach (var k in best) list.Add((k.Play.Kind == "heal" ? "ui-heal" : "sheet-spell", Library.Prose(k.Spell.Name), KindTip(k.Play.Kind)));
        }
        foreach (var p in Powers(c, s))
        {
            if (p.Text == "" && p.Dice == "") continue;
            int uses = At(p.Uses, s.Level);
            string label = Title(p.Id) + (uses > 0 ? $" ×{uses}" : p.Pool > 0 ? $" {p.Pool * s.Level} hp" : "");
            string rest = p.Rest switch { "short" => "a short rest", "long" => p.ShortFrom > 0 && s.Level >= p.ShortFrom ? "a short rest" : "a long rest", "slots" => "spends spell slots", _ => "" };
            list.Add(("ui-star", label, rest == "" ? "Always on" : p.Rest == "slots" ? "Spends a spell slot" : $"Comes back after {rest}"));
        }
        return list;
    }

    private static string KindTip(string kind) => kind switch
    {
        "blast" => "Area damage for a crowd",
        "bolt" => "Heavy damage on one foe",
        "heal" => "Healing",
        "buff" => "Strengthens the party before a hard fight",
        "hold" => "Stops foes acting",
        "mark" => "Extra damage on every hit on one foe",
        "aura" => "Hurts the foes around them each round",
        _ => "",
    };

    private static string Title(string id) => string.Join(' ', id.Split('-').Select((w, i) => i == 0 ? char.ToUpperInvariant(w[0]) + w[1..] : w));

    public static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };
}
