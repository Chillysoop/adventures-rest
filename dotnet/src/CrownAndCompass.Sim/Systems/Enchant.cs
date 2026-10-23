namespace CrownAndCompass.Sim.Systems;

/// <summary>A kind of damage a power deals or a ward turns: fire, cold, lightning... (enchantments.json "elements").</summary>
public sealed record ElementDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Words in a creature's or a power's name that mean it is of this element: a fire elemental shrugs off fire.</summary>
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>Hex colour the client draws it in.</summary>
    public string Color { get; init; } = "ffe0a0";
}

/// <summary>What a kind of power is worth on a hero's sheet and how the client names it (enchantments.json "kinds").</summary>
public sealed record PowerKindDef
{
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>Points the power adds to an item's worth when a hero weighs it against what they wear.</summary>
    public double Worth { get; init; } = 1;

    /// <summary>One line on what it does, for hovers: {dice}, {element}, {targets}, {uses}, {vs}, {amount}, {rounds}.</summary>
    public string Text { get; init; } = "";
}

/// <summary>
/// One power of a magic item (enchantments.json "items"); an item can have several. <see cref="Kind"/> is what it does:
/// strike (more dice on every hit), bane (more against the creature types in <see cref="Vs"/>), blast (an area of
/// <see cref="Targets"/> foes), chain (a bolt that leaps from foe to foe), crit (on a natural 20: hold, slay or triple),
/// hold (pins the strongest foe for <see cref="Rounds"/>), rout (the weakest foes flee), drain (heals by a share of the
/// blow), regen (heals each round), heal (mends the worst hurt), ac, resist (halves a foe's power of its element),
/// reflect (turns a foe's power back on it), guard (halves a blow), free (no snare or hold takes them), first (never
/// ambushed), haste (another blow a round), might (attack and damage), sure (a shot that cannot miss), sending (a
/// sending stone's word home) and scry (scries for the town for free).
/// </summary>
public sealed record ItemPowerDef
{
    public string Ref { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Element { get; init; } = "";
    public string Dice { get; init; } = "";
    public int Targets { get; init; } = 1;

    /// <summary>Times a delve (blast, chain, heal, hold, rout, sure) or a fight (bane, guard, reflect); 0 is every time.</summary>
    public int Uses { get; init; }

    /// <summary>Creature types (or words in their names) a bane bites.</summary>
    public IReadOnlyList<string> Vs { get; init; } = [];

    /// <summary>ac, might: points; drain: share of the blow.</summary>
    public double Amount { get; init; }

    /// <summary>crit: hold, slay or triple.</summary>
    public string Effect { get; init; } = "";
    public int Rounds { get; init; } = 1;

    /// <summary>The item's own bonus, over the +N in its name or its tier.</summary>
    public int? Bonus { get; init; }

    /// <summary>Points it adds to the item's worth, over its kind's.</summary>
    public double? Worth { get; init; }

    /// <summary>Only these classes can use it (empty: anyone); only spellcasters.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];
    public bool Caster { get; init; }

    /// <summary>The Ledger's line when it goes off: {hero}, {item}, {foe}, {n}, {dmg}.</summary>
    public string Text { get; init; } = "";

    public Dice? Roll => Sim.Dice.Parse(Dice);
}

/// <summary>Plain arms the forge makes and the watch and the army can carry (enchantments.json "basic").</summary>
public sealed record BasicArmsRules
{
    /// <summary>Library ids of the +1/+2/+3 weapons, armour, shields and war-mage wands.</summary>
    public IReadOnlyList<string> Items { get; init; } = [];

    /// <summary>How often a hoard holds a plain piece over a magic one, by its bonus (index 1 = +1).</summary>
    public double[] Weight { get; init; } = [0, 1, 0.5, 0.2];
}

/// <summary>Magic items' powers (content/game/enchantments.json).</summary>
public sealed record EnchantRules
{
    public IReadOnlyList<ElementDef> Elements { get; init; } = [];
    public IReadOnlyDictionary<string, PowerKindDef> Kinds { get; init; } = new Dictionary<string, PowerKindDef>();

    /// <summary>Elements a site's habitats favour in its hoard (arctic: cold), and how much likelier a matching piece is.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Habitats { get; init; } = new Dictionary<string, IReadOnlyList<string>>();
    public double Affinity { get; init; } = 4;
    public BasicArmsRules Basic { get; init; } = new();
    public KitRules Arms { get; init; } = new();

    /// <summary>Most magic charms a hero can be attuned to at once.</summary>
    public int Attune { get; init; } = 3;
    public IReadOnlyList<ItemPowerDef> Items { get; init; } = [];

    private Dictionary<string, List<ItemPowerDef>>? _of;
    private HashSet<string>? _basic;

    public IReadOnlyList<ItemPowerDef> Of(string itemId)
    {
        _of ??= Items.GroupBy(p => p.Ref).ToDictionary(g => g.Key, g => g.ToList());
        return _of.TryGetValue(itemId, out var l) ? l : [];
    }

    public bool IsBasic(string itemId) => (_basic ??= [.. Basic.Items]).Contains(itemId);

    public ElementDef? Element(string id) => Elements.FirstOrDefault(e => e.Id == id);
    public PowerKindDef? Kind(string id) => Kinds.TryGetValue(id, out var k) ? k : null;
}

/// <summary>
/// What magic items do beyond their +N: a flame that bites on every hit, a fireball once a delve, a bolt that leaps
/// from foe to foe, a bane against giants, a ward against breath, a whisper home without a caster, a glass that scries
/// for the town. The fight reads them in <see cref="Expedition.Venture"/>; a hero weighs them through
/// <see cref="Gear.Worth"/>, so a blade with a power beats a plain +2. Some are for one class, or for casters only.
/// </summary>
public static class Enchant
{
    public static EnchantRules Rules(Content c) => c.Enchant;

    public static IReadOnlyList<ItemPowerDef> Of(Content c, LibItem it) => Rules(c).Of(it.Id);

    public static bool Basic(Content c, LibItem it) => Rules(c).IsBasic(it.Id);

    /// <summary>The item's own bonus, if its powers name one.</summary>
    public static int? Bonus(Content c, LibItem it) => Of(c, it).Select(p => p.Bonus).FirstOrDefault(b => b != null);

    /// <summary>What its powers add to its worth.</summary>
    public static double Worth(Content c, LibItem it) =>
        Js.Sum(Of(c, it), p => p.Worth ?? Rules(c).Kind(p.Kind)?.Worth ?? 1);

    /// <summary>Whether this hero's class can use it at all.</summary>
    public static bool Usable(Content c, HeroSheet s, LibItem it)
    {
        foreach (var p in Of(c, it))
        {
            if (p.Classes.Count > 0 && !p.Classes.Contains(s.Cls.Id)) return false;
            if (p.Caster && !s.Cls.Spell) return false;
        }
        return true;
    }

    /// <summary>The classes that can use it, or empty for anyone.</summary>
    public static IReadOnlyList<string> Classes(Content c, LibItem it) =>
        Of(c, it).SelectMany(p => p.Classes).Distinct().ToList();

    /// <summary>The powers a hero carries into a fight: every worn item's, with the item.</summary>
    public static List<(ItemPowerDef Power, LibItem Item)> Worn(Content c, HeroSheet s) =>
        Gear.Slots.Where(s.Magic.ContainsKey).SelectMany(slot => Of(c, s.Magic[slot]).Select(p => (p, s.Magic[slot]))).ToList();

    public static bool Has(Content c, HeroSheet s, string kind) => Gear.Slots.Any(slot => s.Magic.TryGetValue(slot, out var it) && Of(c, it).Any(p => p.Kind == kind));

    /// <summary>Armour class from worn wards (a cloak or ring of protection, bracers).</summary>
    public static int Ac(Content c, HeroSheet s) => (int)Js.Sum(Worn(c, s).Where(w => w.Power.Kind == "ac"), w => w.Power.Amount);

    /// <summary>Attack and damage from worn might (a giant's belt, an ogre's gauntlets), the best one only.</summary>
    public static int Might(Content c, HeroSheet s) =>
        (int)Worn(c, s).Where(w => w.Power.Kind == "might").Select(w => w.Power.Amount).DefaultIfEmpty(0).Max();

    /// <summary>The element a creature or a power belongs to by its name ("frost giant": cold), or null.</summary>
    public static ElementDef? ElementOf(Content c, string name)
    {
        string n = Spoils.Plain(name);
        return Rules(c).Elements.FirstOrDefault(e => e.Words.Any(w => Spoils.Named(n, w)));
    }

    /// <summary>Elements a site's habitats favour.</summary>
    public static HashSet<string> Favoured(Content c, IEnumerable<string> habitats)
    {
        var set = new HashSet<string>();
        foreach (var h in habitats)
            if (Rules(c).Habitats.TryGetValue(h, out var els)) set.UnionWith(els);
        return set;
    }

    /// <summary>The elements of an item's powers.</summary>
    public static IEnumerable<string> Elements(Content c, LibItem it) => Of(c, it).Select(p => p.Element).Where(e => e != "").Distinct();

    /// <summary>One line on a power, for hovers: "Fireball: 8d6 fire on 4 foes, once a delve".</summary>
    public static string Describe(Content c, ItemPowerDef p)
    {
        var k = Rules(c).Kind(p.Kind);
        string text = k?.Text ?? p.Kind;
        string uses = p.Uses switch { 0 => "", 1 => ", once a delve", 2 => ", twice a delve", _ => $", {p.Uses}× a delve" };
        string effect = p.Effect switch { "hold" => "stuns it", "slay" => "slays it", "triple" => $"+{p.Dice}", var e => e };
        return text
            .Replace("{dice}", p.Dice)
            .Replace("{element}", Rules(c).Element(p.Element)?.Name ?? p.Element)
            .Replace("{targets}", p.Targets.ToString())
            .Replace("{uses}", uses)
            .Replace("{vs}", string.Join(", ", p.Vs))
            .Replace("{amount}", p.Kind == "drain" ? $"{Js.Round(p.Amount * 100)}%" : Js.Str(p.Amount))
            .Replace("{rounds}", p.Rounds == 1 ? "a round" : $"{p.Rounds} rounds")
            .Replace("{effect}", effect)
            .Replace("  ", " ")
            .Trim();
    }
}
