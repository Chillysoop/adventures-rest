using System.Text.Json;
using System.Text.Json.Serialization;

namespace CrownAndCompass.Sim;

public enum Good
{
    Food,
    Timber,
    Tools,
    Ale,
}

public enum Zone
{
    None,
    Civic,
    Residential,
    Farmland,
    Commercial,
}

public static class Goods
{
    public static readonly IReadOnlyList<Good> All = [Good.Food, Good.Timber, Good.Tools, Good.Ale];
}

public sealed record BuildingDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Zone Zone { get; init; } = Zone.None;
    public bool Buildable { get; init; } = true;
    public double Cost { get; init; }
    public double Timber { get; init; }
    public double BuildHours { get; init; }
    public int Jobs { get; init; }
    public double Wage { get; init; }
    public int Housing { get; init; }
    public double Rent { get; init; }
    public bool Venue { get; init; }
    public int Capacity { get; init; }
    public string? Profession { get; init; }

    /// <summary>A house of the gods: offerings and tithes fill its coffers, which the treasury never sweeps.</summary>
    public bool Holy { get; init; }

    /// <summary>Goods made per worker-hour, in the order the content lists them.</summary>
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Produces { get; init; } = [];

    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Consumes { get; init; } = [];

    public bool IsBusiness => Jobs > 0;
}

public sealed record RaceDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Weight { get; init; }
    public IReadOnlyList<string> Given { get; init; } = [];
    public IReadOnlyList<string> Family { get; init; } = [];
}

/// <summary>Ability keys as the content names them: str, dex, con, int, wis, cha.</summary>
public static class Ability
{
    public const string Str = "str";
    public const string Dex = "dex";
    public const string Con = "con";
    public const string Int = "int";
    public const string Wis = "wis";
    public const string Cha = "cha";
}

/// <summary>Skill check kinds: trap, hazard, puzzle, parley, perception.</summary>
public static class SkillKind
{
    public const string Trap = "trap";
    public const string Hazard = "hazard";
    public const string Puzzle = "puzzle";
    public const string Parley = "parley";
    public const string Perception = "perception";
}

/// <summary>
/// A hero class. Its name, hit die, key ability and spells come from the content library (content/srd,
/// content/original); content/game/classes.json adds how it plays here (armour, weapon, skills, how
/// often heroes of the class turn up).
/// </summary>
public sealed record ClassDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Primary { get; init; } = "";

    /// <summary>The spellcasting ability for classes that cast (int, wis or cha).</summary>
    public string Casting { get; init; } = "";
    public int HitDie { get; init; }
    public int Armor { get; init; }
    public int DamageDie { get; init; }
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();
    public int Weight { get; init; }
    public bool Sneak { get; init; }

    /// <summary>What a fumbling hero nearly drops: "the weapon", "the spellbook".</summary>
    public string Fumble { get; init; } = "the weapon";

    /// <summary>From the library: frontline, skirmisher, support, caster, scout or specialist.</summary>
    public string Role { get; init; } = "";

    /// <summary>From the library: full, half, pact, or empty for classes that cast nothing.</summary>
    public string Magic { get; init; } = "";

    /// <summary>The class's first healing spell, if it has one (cure wounds, bloodmoss poultice).</summary>
    public LibSpell? HealSpell { get; init; }

    /// <summary>The class's first area damage spell, if it has one (burning hands, thunderwave).</summary>
    public LibSpell? AreaSpell { get; init; }

    public bool Healer => HealSpell != null;
    public bool Area => AreaSpell != null;

    /// <summary>Heals a hero of this class carries into a delve: full casters more than half casters.</summary>
    public int HealsAt(int level) => HealSpell == null ? 0 : Magic == "full" ? 1 + level / 2 : level / 2;
}

/// <summary>
/// A creature as a delve fights it. Entries in content/game/monsters.json name a library creature with
/// <c>ref</c> and may override any number (the Last Stand's warlord is scaled down); any library creature
/// can also be fought by its library id.
/// </summary>
public sealed record MonsterDef
{
    public required string Id { get; init; }
    public string Ref { get; init; } = "";
    public string Name { get; init; } = "";
    public string Plural { get; init; } = "";
    public int Hp { get; init; }
    public int Ac { get; init; }
    public int Attack { get; init; }

    /// <summary>Damage dice of its best attack, as "1d6+2".</summary>
    public string Damage { get; init; } = "";

    /// <summary>Attacks a round (multiattack).</summary>
    public int Attacks { get; init; }
    public int Xp { get; init; }
    public bool Boss { get; init; }
    public bool Undead { get; init; }

    /// <summary>A named individual: no article before the name.</summary>
    public bool Named { get; init; }

    public Dice Dice => Sim.Dice.Parse(Damage) ?? new Dice(1, 4, 0);
}

/// <summary>
/// One encounter in a site's deck. <see cref="Kind"/> is combat, trap, hazard, puzzle, parley or
/// treasure; each kind reads its own fields.
/// </summary>
public sealed record EncounterDef
{
    public required string Kind { get; init; }
    public string Text { get; init; } = "";
    // combat
    public string Monster { get; init; } = "";
    public int[] Count { get; init; } = [1, 1];
    public string? Escort { get; init; }
    public int[]? EscortCount { get; init; }
    // trap, hazard, puzzle, parley
    public int Dc { get; init; }
    public int DamageDie { get; init; }
    // puzzle, parley, treasure
    public int[] Gold { get; init; } = [0, 0];
    public double MimicChance { get; init; }
    // wander: a creature drawn from the library by the site's habitats and this challenge-rating range
    public double[] Cr { get; init; } = [0, 0];

    /// <summary>Experience the wanderers are worth between them; sets how many come.</summary>
    public int Budget { get; init; }
}

public sealed record SiteDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int Tier { get; init; }
    public int Distance { get; init; }
    public IReadOnlyList<EncounterDef> Encounters { get; init; } = [];
    public required EncounterDef Boss { get; init; }
    public int[] Loot { get; init; } = [0, 0];

    /// <summary>Library habitats whose creatures wander here (forest, caves, ruins...).</summary>
    public IReadOnlyList<string> Habitats { get; init; } = [];

    /// <summary>What lives there, from the GDD's enemy table: beasts, tribe, undead, schemers or rivals.</summary>
    public string Kind { get; init; } = "";

    /// <summary>Known to the town from the charter (the local menace); every other site starts as tavern talk at best.</summary>
    public bool Known { get; init; }
}

/// <summary>Town-wide economic rules.</summary>
public sealed record TownRules
{
    /// <summary>Share of every sale and every drink that goes to the treasury.</summary>
    public double TaxRate { get; init; }

    /// <summary>Daily cost per resident of the watch, roads and wells.</summary>
    public double UpkeepPerResident { get; init; }

    /// <summary>Per-resident upkeep doubles at this many residents: bigger towns cost more per head.</summary>
    public double UpkeepGrowth { get; init; }

    /// <summary>Daily cost of keeping the town hall and council running.</summary>
    public double CivicUpkeep { get; init; }
}

/// <summary>Council seat ids as the content names them.</summary>
public static class SeatId
{
    public const string Steward = "steward";
    public const string Speaker = "speaker";
    public const string Captain = "captain";
}

/// <summary>Motion kinds as the content names them.</summary>
public static class MotionKind
{
    public const string RaiseTax = "raise_tax";
    public const string LowerTax = "lower_tax";
    public const string HireWatch = "hire_watch";
    public const string DismissWatch = "dismiss_watch";
    public const string Festival = "festival";
    public const string BuildHomes = "build_homes";
}

public sealed record SeatDef
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Ability { get; init; }
    public int UnlockPopulation { get; init; }
    public string Portfolio { get; init; } = "";
    public string Agenda { get; init; } = "";
}

/// <summary>The council: its seats, how each seat feels about each kind of motion, and the motions' numbers.</summary>
public sealed record CouncilRules
{
    public IReadOnlyList<SeatDef> Seats { get; init; } = [];

    /// <summary>-1 (against) to 1 (for), by motion kind and seat.</summary>
    public IReadOnlyDictionary<string, Dictionary<string, double>> Alignment { get; init; } = new Dictionary<string, Dictionary<string, double>>();

    public double TaxStep { get; init; }
    public double MinTax { get; init; }
    public double MaxTax { get; init; }
    public int WatchPerMotion { get; init; }
    public double WatchWage { get; init; }
    public double FestivalBase { get; init; }
    public double FestivalPerResident { get; init; }
    public int FestivalDays { get; init; }
    public double FestivalMood { get; init; }
    public int MotionDays { get; init; }
    public int MotionGapDays { get; init; }
    public double DecreeMandate { get; init; }
    public double StartingMandate { get; init; }
}

public sealed record EnemyStages
{
    public double Omens { get; init; }
    public double Probing { get; init; }
    public double Raids { get; init; }
    public double March { get; init; }
}

public sealed record EnemyDefense
{
    public double Watchman { get; init; }
    public double HeroLevel { get; init; }
    public double MilitiaPerWorker { get; init; }
    public double Palisade { get; init; }
    public double PerTower { get; init; }
}

public sealed record WorkSpec
{
    public double Cost { get; init; }
    public double Timber { get; init; }
    public int Days { get; init; }
    public int Max { get; init; }
}

/// <summary>The slice's Tier 2 enemy: how it grows, escalates, raids and besieges the town.</summary>
public sealed record EnemyDef
{
    public required string Name { get; init; }
    public required string Leader { get; init; }
    public string LeaderMonster { get; init; } = "";
    public double StartStrength { get; init; }
    public double BaseGrowth { get; init; }
    public double GrowthPerDay { get; init; }
    public double GrowthPerSite { get; init; }
    public double GrowthPerTier3Site { get; init; }

    /// <summary>A richer town draws a bigger warband.</summary>
    public double GrowthPerResident { get; init; }

    /// <summary>The warlord marches only once the warband is at least this share of the town's defence.</summary>
    public double MarchConfidence { get; init; }

    /// <summary>Warband strength at which each escalation stage begins.</summary>
    public required EnemyStages Stages { get; init; }

    public int MarchDays { get; init; }
    public int[] RaidEveryDays { get; init; } = [5, 9];
    public double RaidShare { get; init; }
    public double OutpostGrowth { get; init; }
    public double OutpostClearedLoss { get; init; }
    public int SiegeWaves { get; init; }
    public int BreachesToFall { get; init; }
    public required EnemyDefense Defense { get; init; }
    public required WorkSpec Palisade { get; init; }
    public required WorkSpec Tower { get; init; }
    public required SiteDef Outpost { get; init; }
    public required EncounterDef WarlordBattle { get; init; }
    public IReadOnlyList<string> Omens { get; init; } = [];
    public IReadOnlyList<string> Probing { get; init; } = [];
}

/// <summary>Scouts posted to watch a site from cover (content/game/knowledge.json).</summary>
public sealed record ScoutRules
{
    /// <summary>Library class roles that can scout.</summary>
    public IReadOnlyList<string> Roles { get; init; } = ["scout", "skirmisher"];
    public double WagePerDay { get; init; } = 3;

    /// <summary>Days a scout can watch before the rations run out.</summary>
    public int WatchDays { get; init; } = 8;

    /// <summary>Daily chance per site tier that the site's monsters spot the scout.</summary>
    public double SpottedPerTier { get; init; } = 0.04;
    public int EscapeDc { get; init; } = 10;
    public int EscapeDcPerTier { get; init; } = 2;
}

/// <summary>A paired mirror hidden at a site: the library's Mirror Network spell.</summary>
public sealed record MirrorRules
{
    public string Spell { get; init; } = "mirror-network";
    public int MinLevel { get; init; } = 3;
    public double Cost { get; init; } = 400;

    /// <summary>Daily chance per site tier that the hidden mirror is found and smashed while the site is held.</summary>
    public double SmashPerTier { get; init; } = 0.015;
}

/// <summary>A building that gives the town sight of the wilds, housing a library relic.</summary>
public sealed record SightWorkDef
{
    public required string Building { get; init; }
    public required string Relic { get; init; }
    public int Population { get; init; }

    /// <summary>Free scryings a day.</summary>
    public int Scryings { get; init; }

    /// <summary>Sites this many days out or nearer are always in sight.</summary>
    public int Reach { get; init; }
}

/// <summary>Numbers for what the town can learn of the wilds (content/game/knowledge.json).</summary>
public sealed record KnowledgeRules
{
    /// <summary>Library spells: whoever can cast them can scry, or send word from a party.</summary>
    public string ScrySpell { get; init; } = "scry-commission";
    public string SendSpell { get; init; } = "spell-slot-message";

    public int ScryMinLevel { get; init; } = 2;
    public double ScryBaseCost { get; init; } = 30;
    public double ScryCostPerDay { get; init; } = 15;
    public int ScryHours { get; init; } = 12;
    public int SlotsPerCaster { get; init; } = 1;
    public int SlotsPerTwoLevels { get; init; } = 1;
    public ScoutRules Scouts { get; init; } = new();
    public MirrorRules Mirror { get; init; } = new();
    public IReadOnlyList<SightWorkDef> Works { get; init; } = [];
}

/// <summary>Where a rumour comes from and how far to trust it.</summary>
public sealed record RumorSource
{
    public string Text { get; init; } = "";
    public int[] Reliability { get; init; } = [30, 70];
}

/// <summary>How taverns trade in rumours (content/game/rumors.json).</summary>
public sealed record RumorRules
{
    /// <summary>Daily chance a tavern turns up a rumour, plus a little per patron-hour of evening talk.</summary>
    public double BaseChance { get; init; } = 0.12;
    public double ChancePerPatron { get; init; } = 0.012;
    public double MaxChance { get; init; } = 0.8;

    /// <summary>How much likelier a rumour is about a place the town has never heard of.</summary>
    public double UnheardWeight { get; init; } = 3;

    /// <summary>Days after a clearing before anyone talks of the place being taken again.</summary>
    public int QuietDays { get; init; } = 20;
    public IReadOnlyList<RumorSource> Travellers { get; init; } = [];
    public int[] ResidentReliability { get; init; } = [25, 65];
    public int[] HeroReliability { get; init; } = [55, 90];
    public double HeroShare { get; init; } = 0.3;
    public double ResidentShare { get; init; } = 0.3;
    public IReadOnlyList<string> TierWords { get; init; } = [];
    public IReadOnlyList<string> Place { get; init; } = [];
    public IReadOnlyList<string> Strength { get; init; } = [];
    public IReadOnlyList<string> Occupant { get; init; } = [];
    public IReadOnlyList<string> Resettled { get; init; } = [];

    /// <summary>Each tavern past the first makes a seasoned hero likelier to walk in.</summary>
    public double BetterHeroesPerTavern { get; init; } = 0.08;
}

/// <summary>A healing potion as the general store sells it.</summary>
public sealed record PotionRules
{
    public string Item { get; init; } = "potion-of-healing";
    public double Price { get; init; } = 12;
    public double Wholesale { get; init; } = 5;

    /// <summary>Most potions a hero carries into a delve.</summary>
    public int Carry { get; init; } = 2;
    public string Dice { get; init; } = "2d4+2";
}

/// <summary>The shrine, the temple and the town's god (content/game/faith.json).</summary>
public sealed record ShrineRules
{
    public string Deity { get; init; } = "the Lantern Mother";

    /// <summary>What the faithful call themselves.</summary>
    public string Devout { get; init; } = "the faithful";
    public string Shrine { get; init; } = "shrine";
    public string Temple { get; init; } = "temple";
    public int TemplePopulation { get; init; } = 40;
    public int[] PrayHours { get; init; } = [6, 7, 18];
    public double Offering { get; init; } = 0.2;
    public double PrayMood { get; init; } = 10;
    public double FollowerChance { get; init; } = 0.05;
    public double Tithe { get; init; } = 0.03;
    public int TitheEveryDays { get; init; } = 7;

    /// <summary>Extra share of a hero's hit points a night's rest restores once the town has a shrine.</summary>
    public double ShrineRest { get; init; } = 0.25;
    public double TempleHealFee { get; init; } = 2;
    public double RaiseCost { get; init; } = 250;
    public int HolyDayEveryDays { get; init; } = 15;
    public double HolyDayCost { get; init; } = 30;
    public string HolyDayName { get; init; } = "the Feast of Lamps";

    /// <summary>Site kinds the temple pays to see destroyed.</summary>
    public IReadOnlyList<string> CrusadeKinds { get; init; } = [];
    public double CrusadeReward { get; init; } = 140;
    public double CrusadeReserve { get; init; } = 40;
    public PotionRules Potion { get; init; } = new();
}

/// <summary>How one kind of enemy lives off-screen (GDD enemy table).</summary>
public sealed record LairKind
{
    public double Growth { get; init; } = 0.5;
    public string Verb { get; init; } = "";
}

public sealed record TributeRules
{
    /// <summary>Gold a tribe demands per point of strength.</summary>
    public double PerStrength { get; init; } = 0.8;

    /// <summary>Days a paid tribe stays quiet and stops growing.</summary>
    public int QuietDays { get; init; } = 12;

    /// <summary>The stand-in mayor pays when the treasury holds this many times the demand.</summary>
    public double PayIfTreasuryOver { get; init; } = 4;
}

public sealed record OutpostRules
{
    public double Cost { get; init; } = 150;
    public double Upkeep { get; init; } = 2;
    public int Max { get; init; } = 3;
}

/// <summary>Lairs: how sites grow, act by their kind, and are resettled (content/game/lairs.json).</summary>
public sealed record LairRules
{
    public double StartStrength { get; init; } = 20;
    public double MaxStrength { get; init; } = 100;

    /// <summary>Each this much strength puts one more creature in every fight there.</summary>
    public double StrengthPerSwell { get; init; } = 35;
    public int MaxSwell { get; init; } = 2;

    /// <summary>Strength at which a lair starts acting on the world.</summary>
    public double ActAt { get; init; } = 25;
    public int[] ActEveryDays { get; init; } = [5, 10];
    public IReadOnlyDictionary<string, LairKind> Kinds { get; init; } = new Dictionary<string, LairKind>();
    public double RetreatLossPerKill { get; init; } = 3;

    /// <summary>Strength a scattered tribe's survivors bring to the nearest tribe that takes them in.</summary>
    public double ScatterJoin { get; init; } = 15;
    public TributeRules Tribute { get; init; } = new();
    public double UndeadKillAt { get; init; } = 60;

    /// <summary>Chance the shrine's or temple's wards turn the walking dead from the fields.</summary>
    public double WardShrine { get; init; } = 0.5;
    public double WardTemple { get; init; } = 0.8;
    public int ThrallStealDays { get; init; } = 7;
    public int[] ThrallSteal { get; init; } = [8, 25];
    public int[] ResettleDays { get; init; } = [25, 45];

    /// <summary>Chance the new occupant of a cleared site is a tier worse.</summary>
    public double WorseChance { get; init; } = 0.4;

    /// <summary>Challenge-rating range of a new occupant's leader, by tier.</summary>
    public double[][] BossCr { get; init; } = [];
    public OutpostRules Outpost { get; init; } = new();

    /// <summary>Raiders wait for a day when at least this share of the heroes' levels is away.</summary>
    public double SoftShare { get; init; } = 0.5;
    public int MaxWaitDays { get; init; } = 3;
}

/// <summary>A magic item as loot: which library slots a delve's hoard can hold, and what each grants.</summary>
public sealed record LootRules
{
    /// <summary>Weapon bonus by item tier (index = tier); armour and shields give one point of AC.</summary>
    public int[] WeaponBonus { get; init; } = [0, 1, 1, 1, 2, 2, 3];
    public IReadOnlyList<string> Slots { get; init; } = ["weapon", "armor", "shield", "trinket", "town"];

    /// <summary>Chance per site tier that a cleared hoard holds a magic item.</summary>
    public double ItemChancePerTier { get; init; } = 0.15;
}

/// <summary>
/// Data-driven content (buildings, races, names, classes, monsters, sites, the council, the
/// enemy). Embedded from the repo's content/game/*.json by default so the simulation runs anywhere; a
/// mod can pass its own.
/// </summary>
public sealed class Content
{
    private readonly Dictionary<string, BuildingDef> _buildings;
    private readonly Dictionary<string, MonsterDef> _monsters;

    /// <summary>Buildings in content order.</summary>
    public IReadOnlyList<BuildingDef> Buildings { get; }
    public IReadOnlyList<RaceDef> Races { get; }
    public IReadOnlyList<ClassDef> Classes { get; }
    public IReadOnlyList<MonsterDef> Monsters { get; }
    public IReadOnlyList<SiteDef> Sites { get; }
    public TownRules Rules { get; }
    public CouncilRules Council { get; }
    public EnemyDef Enemy { get; }

    /// <summary>What the town can learn of the wilds: scrying, sending stones, scouts, mirrors and sight towers.</summary>
    public KnowledgeRules Knowledge { get; init; } = new();

    /// <summary>What a cleared hoard can hold.</summary>
    public LootRules Loot { get; init; } = new();

    /// <summary>How lairs grow, act and are resettled.</summary>
    public LairRules Lairs { get; init; } = new();

    /// <summary>How taverns trade in rumours.</summary>
    public RumorRules Rumors { get; init; } = new();

    /// <summary>The shrine, the temple and the general store's potions.</summary>
    public ShrineRules Shrines { get; init; } = new();

    /// <summary>The content library every hero class, creature, spell and magic item comes from.</summary>
    public Library Library { get; }

    /// <summary>Library magic items a hoard can hold (the loot rules' slots), by id.</summary>
    public IReadOnlyList<LibItem> LootItems => _lootItems ??= Library.Items.Where(i => i.Tier > 0 && Loot.Slots.Contains(i.Slot)).ToList();
    private IReadOnlyList<LibItem>? _lootItems;

    public Content(
        IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<RaceDef> races,
        IReadOnlyList<ClassDef> classes,
        IReadOnlyList<MonsterDef> monsters,
        IReadOnlyList<SiteDef> sites,
        TownRules rules,
        CouncilRules council,
        EnemyDef enemy,
        Library? library = null)
    {
        Library = library ?? Library.Default();
        Buildings = buildings;
        _buildings = buildings.ToDictionary(b => b.Id);
        Races = races;
        Classes = classes.Select(c => ResolveClass(c, Library)).ToList();
        Monsters = monsters.Select(m => ResolveMonster(m, Library)).ToList();
        _monsters = Monsters.ToDictionary(m => m.Id);
        // Every library creature can be met by its own id too.
        foreach (var lm in Library.Monsters)
            if (lm.Damage != null && !_monsters.ContainsKey(lm.Id)) _monsters[lm.Id] = ResolveMonster(new MonsterDef { Id = lm.Id, Ref = lm.Id }, Library);
        Sites = sites;
        Rules = rules;
        Council = council;
        Enemy = enemy;
    }

    /// <summary>Fill a class from the library: name, hit die, abilities, magic and its signature spells.</summary>
    public static ClassDef ResolveClass(ClassDef c, Library lib)
    {
        var lc = lib.Class(c.Id) ?? throw new KeyNotFoundException($"Class {c.Id} is not in the content library");
        string[] mental = [Ability.Int, Ability.Wis, Ability.Cha];
        var spells = lib.SpellsOf(c.Id).Where(s => s.Level >= 1).ToList();
        return c with
        {
            Name = c.Name != "" ? c.Name : Library.Prose(lc.Name),
            Primary = c.Primary != "" ? c.Primary : lc.Primary.FirstOrDefault() ?? Ability.Str,
            Casting = lc.Magic == "" ? "" : lc.Primary.FirstOrDefault(mental.Contains) ?? Ability.Wis,
            HitDie = c.HitDie > 0 ? c.HitDie : lc.HitDie,
            Role = lc.Role,
            Magic = lc.Magic,
            HealSpell = lc.Magic == "" ? null : spells.FirstOrDefault(s => s.Roles.Contains("heal")),
            AreaSpell = lc.Magic == "" ? null : spells.FirstOrDefault(s => s.Level == 1 && s.Targets == "area" && s.Roles.Contains("damage") && s.Dice != null),
        };
    }

    /// <summary>Fill a creature from the library entry it names; numbers set in content/game win.</summary>
    public static MonsterDef ResolveMonster(MonsterDef m, Library lib)
    {
        if (m.Ref == "") return m;
        var lm = lib.Monster(m.Ref) ?? throw new KeyNotFoundException($"Monster {m.Id} refers to {m.Ref}, which is not in the content library");
        string name = m.Name != "" ? m.Name : m.Named ? lm.Name : Library.Prose(lm.Name);
        return m with
        {
            Name = name,
            Plural = m.Plural != "" ? m.Plural : Library.Plural(name),
            Hp = m.Hp > 0 ? m.Hp : lm.Hp,
            Ac = m.Ac > 0 ? m.Ac : lm.Ac,
            Attack = m.Attack > 0 ? m.Attack : lm.ToHit,
            Damage = m.Damage != "" ? m.Damage : lm.Damage?.ToString() ?? "1d4",
            Attacks = m.Attacks > 0 ? m.Attacks : lm.Attacks,
            Xp = m.Xp > 0 ? m.Xp : Library.XpForCr(lm.Cr),
            Undead = m.Undead || lm.Type == "undead",
        };
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MonsterDef> _bosses = new();

    /// <summary>A library creature leading a band: the same stats, fought as the boss ("id@boss").</summary>
    public MonsterDef BossOf(string id) => _bosses.GetOrAdd($"{id}@boss", key => Monster(id) with { Id = key, Boss = true });

    public MonsterDef Monster(string id) =>
        _monsters.TryGetValue(id, out var m) ? m
        : id.EndsWith("@boss", StringComparison.Ordinal) ? BossOf(id[..^5])
        : throw new KeyNotFoundException($"Unknown monster {id}");

    /// <summary>Library creatures that can wander into a site: any of its habitats, inside the challenge range, with an attack to fight with.</summary>
    public List<MonsterDef> Wanderers(SiteDef site, double[] cr) =>
        Library.Monsters
            .Where(lm => lm.Damage != null && lm.Cr >= cr[0] && lm.Cr <= cr[^1] && lm.Band != "" && !lm.Boss && lm.Habitats.Any(site.Habitats.Contains))
            .Select(lm => _monsters[lm.Id])
            .ToList();

    public BuildingDef Building(string id) =>
        _buildings.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException($"Unknown building {id}");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private static Content? _default;

    /// <summary>The bundled content, parsed once and shared (content is immutable).</summary>
    public static Content Default() => _default ??= Load(ReadEmbedded);

    /// <summary>Load content from a directory of JSON files laid out like the repo's content/game/.</summary>
    public static Content FromDirectory(string dir) => Load(name => File.ReadAllText(Path.Combine(dir, name)));

    private static Content Load(Func<string, string> read)
    {
        T Parse<T>(string file) => JsonSerializer.Deserialize<T>(read(file), JsonOptions) ?? throw new InvalidDataException($"Empty content file {file}");
        return new Content(
            Parse<List<BuildingDef>>("buildings.json"),
            Parse<NamesFile>("names.json").Races,
            Parse<List<ClassDef>>("classes.json"),
            Parse<List<MonsterDef>>("monsters.json"),
            Parse<List<SiteDef>>("sites.json"),
            Parse<TownRules>("town.json"),
            Parse<CouncilRules>("council.json"),
            Parse<EnemyDef>("enemy.json"))
        {
            Knowledge = Parse<KnowledgeRules>("knowledge.json"),
            Loot = Parse<LootRules>("loot.json"),
            Rumors = Parse<RumorRules>("rumors.json"),
            Lairs = Parse<LairRules>("lairs.json"),
            Shrines = Parse<ShrineRules>("shrines.json"),
        };
    }

    private static string ReadEmbedded(string name)
    {
        var asm = typeof(Content).Assembly;
        using var stream = asm.GetManifestResourceStream("content." + name) ?? throw new FileNotFoundException($"Missing embedded content {name}");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private sealed record NamesFile
    {
        public List<RaceDef> Races { get; init; } = [];
    }
}

/// <summary>Reads a JSON object of good to quantity as a list, keeping the file's order.</summary>
internal sealed class GoodsListConverter : JsonConverter<IReadOnlyList<KeyValuePair<Good, double>>>
{
    public override IReadOnlyList<KeyValuePair<Good, double>> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var list = new List<KeyValuePair<Good, double>>();
        foreach (var p in doc.RootElement.EnumerateObject()) list.Add(new(Enum.Parse<Good>(p.Name), p.Value.GetDouble()));
        return list;
    }

    public override void Write(Utf8JsonWriter writer, IReadOnlyList<KeyValuePair<Good, double>> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        foreach (var (g, q) in value) writer.WriteNumber(g.ToString(), q);
        writer.WriteEndObject();
    }
}
