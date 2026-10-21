using System.Text.Json;
using System.Text.Json.Serialization;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim;

public enum Good
{
    Food,
    Timber,
    Tools,
    Ale,
    Stone,
    Wool,
    Cloth,
    Furniture,
    Herbs,
    Potions,
    Wine,
    Books,
}

public enum Zone
{
    None,
    Civic,
    Residential,
    Farmland,
    Commercial,
    Industry,
}

public static class Goods
{
    public static readonly IReadOnlyList<Good> All = Enum.GetValues<Good>();
}

public sealed record BuildingDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Zone Zone { get; init; } = Zone.None;
    public bool Buildable { get; init; } = true;
    public double Cost { get; init; }
    public double Timber { get; init; }

    /// <summary>Stone the builders need, bought at the market (or by caravan at a premium).</summary>
    public double Stone { get; init; }
    public double BuildHours { get; init; }

    /// <summary>The town tier (content/game/city.json) that unlocks it; empty for the charter's own buildings.</summary>
    public string Tier { get; init; } = "";

    /// <summary>The only wards it can stand in; empty for any open ward.</summary>
    public IReadOnlyList<string> Wards { get; init; } = [];

    /// <summary>The least density (city.json "densities": 0 low, 1 medium, 2 high) the land under it must be zoned for.</summary>
    public int Density { get; init; }

    /// <summary>Services it gives (tavern, shrine, market, bath, theatre, learning, spectacle) and how many residents each covers.</summary>
    public IReadOnlyDictionary<string, int> Serves { get; init; } = new Dictionary<string, int>();

    /// <summary>A service business: what each resident it serves pays it a day.</summary>
    public double Fee { get; init; }

    /// <summary>Market hall: extra share of sales tax the treasury takes.</summary>
    public double SalesBoost { get; init; }

    /// <summary>Mandate the townsfolk lend the mayor while it stands.</summary>
    public double Mandate { get; init; }

    /// <summary>Barracks and keep: how much stronger the walls are while it stands.</summary>
    public double Defense { get; init; }
    public int Jobs { get; init; }
    public double Wage { get; init; }
    public int Housing { get; init; }
    public double Rent { get; init; }
    public bool Venue { get; init; }

    /// <summary>Townsfolk it holds at once on an outing (a tavern's drinkers, a shrine's worshippers, a bathhouse's bathers); 0 = it takes no visitors (<see cref="Systems.Crowds"/>).</summary>
    public int Capacity { get; init; }
    public string? Profession { get; init; }

    /// <summary>A house of the gods: offerings and tithes fill its coffers, which the treasury never sweeps.</summary>
    public bool Holy { get; init; }

    /// <summary>A government building the player places directly, paid from the treasury (<see cref="Planning"/>).</summary>
    public bool Civic { get; init; }

    /// <summary>Daily gold the treasury pays to keep a finished government building running.</summary>
    public double Upkeep { get; init; }

    /// <summary>Residents the town needs before it can raise one.</summary>
    public int Population { get; init; }

    /// <summary>Most the town can have at once (0 = no limit).</summary>
    public int Max { get; init; }

    /// <summary>A training hall: soldiers of the town's army it can train and quarter (<see cref="Systems.Army"/>).</summary>
    public int Trains { get; init; }

    /// <summary>Watch house: each one makes every watchman count this much more.</summary>
    public double WatchBoost { get; init; }

    /// <summary>Granary: food the caravans leave in town on top of the usual stock.</summary>
    public double FoodStore { get; init; }

    /// <summary>Footprint in plan cells (width, depth), when not the usual for its zone (<see cref="Systems.Land.Footprint"/>).</summary>
    public int[]? Size { get; init; }

    /// <summary>One line for the build bar.</summary>
    public string Blurb { get; init; } = "";

    /// <summary>Goods made per worker-hour, in the order the content lists them.</summary>
    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Produces { get; init; } = [];

    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Consumes { get; init; } = [];

    /// <summary>Part of an adventuring company's bastion: the company pays its hirelings and keeps what it earns (<see cref="Systems.Bastions"/>).</summary>
    public bool Bastion { get; init; }

    /// <summary>A grand work: added to every house's contentment while it stands.</summary>
    public double Comfort { get; init; }

    /// <summary>A grand work: added to the town's draw on settlers while it stands.</summary>
    public double Attraction { get; init; }

    /// <summary>A grand work: more heroes the town can keep while it stands.</summary>
    public int Heroes { get; init; }

    /// <summary>A grand work's stages, funded one after another; its build hours are theirs summed (<see cref="Systems.GrandWorks"/>).</summary>
    public IReadOnlyList<WorkStage> Stages { get; init; } = [];

    /// <summary>A builders' yard: building crews it adds, so more of the townsfolk's builds can be under way at once (<see cref="Systems.Construction.Crews"/>).</summary>
    public int Crews { get; init; }

    /// <summary>A monument's part: the building it grows from (temple, keep, town hall), raised beside it (<see cref="Systems.Monuments"/>).</summary>
    public string PartOf { get; init; } = "";

    /// <summary>A part the monument must already have, standing or rising, before this one can be added.</summary>
    public string After { get; init; } = "";

    /// <summary>
    /// What a standing part does each day for the town's standing: "god" with the monument's god (the temple's own, else
    /// the town's), "faction:id" with a faction. Negative for a rival.
    /// </summary>
    public IReadOnlyDictionary<string, double> Favour { get; init; } = new Dictionary<string, double>();

    public bool IsBusiness => Jobs > 0;
    public bool IsGrand => Stages.Count > 0;
    public bool IsPart => PartOf != "";
}

/// <summary>One stage of a grand work: the gold and goods it draws when it starts, and the builders' hours it takes.</summary>
public sealed record WorkStage
{
    public string Name { get; init; } = "";
    public double Gold { get; init; }

    [JsonConverter(typeof(GoodsListConverter))]
    public IReadOnlyList<KeyValuePair<Good, double>> Goods { get; init; } = [];
    public double Hours { get; init; }
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

    public static readonly string[] All = [Str, Dex, Con, Int, Wis, Cha];
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

    /// <summary>The die of the class's attack spell, for classes that fight with magic (<see cref="Spell"/>).</summary>
    public int DamageDie { get; init; }

    /// <summary>The kit a hero of the class arrives with: a weapon (library equipment id), then the better ones they may buy.</summary>
    public string Weapon { get; init; } = "";
    public IReadOnlyList<string> Better { get; init; } = [];

    /// <summary>Armours the class wears, the first one arriving on their back; empty for none.</summary>
    public IReadOnlyList<string> Wears { get; init; } = [];
    public bool Shield { get; init; }

    /// <summary>The ability added to 10 + Dex when the class fights unarmoured (barbarians, monks).</summary>
    public string Unarmored { get; init; } = "";

    /// <summary>Fights with an attack spell (the damage die and the casting ability), not the weapon.</summary>
    public bool Spell { get; init; }
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();
    public int Weight { get; init; }
    public bool Sneak { get; init; }

    /// <summary>What a fumbling hero nearly drops: "the weapon", "the spellbook".</summary>
    public string Fumble { get; init; } = "the weapon";

    /// <summary>From the library: frontline, skirmisher, support, caster, scout or specialist.</summary>
    public string Role { get; init; } = "";

    /// <summary>What a hero card says the class is for in a party: tank, striker, healer, scout or face.</summary>
    public string Card { get; init; } = "";

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

    /// <summary>Intelligence score, from the library unless set here; 0 reads as 10.</summary>
    public int Int { get; init; }

    /// <summary>Damage dice of its best attack, as "1d6+2".</summary>
    public string Damage { get; init; } = "";

    /// <summary>Attacks a round (multiattack).</summary>
    public int Attacks { get; init; }
    public int Xp { get; init; }
    public bool Boss { get; init; }
    public bool Undead { get; init; }

    /// <summary>The library creature type (beast, undead, dragon...), for how it fights (<see cref="Systems.Tactics"/>).</summary>
    public string Type { get; init; } = "";

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

    /// <summary>The signature foe (foes.json) that holds it, or empty.</summary>
    public string Foe { get; init; } = "";

    /// <summary>A floating isle: only a company that can fly, or rides the sky dock's airship, gets there.</summary>
    public bool Aloft { get; init; }

    /// <summary>The old work it is the ruin of (world.json "ruins"), which the town can restore once it is cleared.</summary>
    public string Ruin { get; init; } = "";

    /// <summary>The ready-made enemy group holding it (groups.json), or "": its foes, boss and lineage come from the group.</summary>
    public string Group { get; init; } = "";
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

    /// <summary>Government buildings' upkeep grows by its own amount again for every this many residents they serve.</summary>
    public double ServiceGrowth { get; init; }

    /// <summary>A young town draws settlers looking for cheap land: this much attraction at the founding, fading to none at <see cref="FrontierSize"/> residents.</summary>
    public double FrontierDraw { get; init; }

    /// <summary>Residents at which a town stops drawing settlers just for being new (<see cref="FrontierDraw"/>).</summary>
    public double FrontierSize { get; init; } = 1;

    /// <summary>Open ground, in tiles, kept between any two buildings' footprints however they are turned.</summary>
    public double BuildingGap { get; init; }

    /// <summary>The knight sworn to the charter, who rides with the first settlers (<see cref="Simulation"/> Charter); class id and level.</summary>
    public string CharterKnight { get; init; } = "";
    public int CharterKnightLevel { get; init; } = 1;

    /// <summary>Daily cost of keeping the town hall and council running.</summary>
    public double CivicUpkeep { get; init; }

    /// <summary>A great treasury costs keeping: TreasuryKeep of the gold above TreasuryFree each day, in clerks, guards and tithes.</summary>
    public double TreasuryFree { get; init; } = 15000;
    public double TreasuryKeep { get; init; }

    /// <summary>Share of the per-resident upkeep paid out as wages to road crews hired from the idle.</summary>
    public double CrewShare { get; init; } = 0.5;

    /// <summary>A road crewman's hourly wage, below any trade's so crews are the last resort.</summary>
    public double CrewWage { get; init; } = 0.5;
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
    public const string Knight = "knight";
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

    /// <summary>Days between festivals, and the Mandate a festival the mayor calls on their own word earns.</summary>
    public int FestivalGapDays { get; init; } = 20;
    public double FestivalMandate { get; init; }
    public int MotionDays { get; init; }
    public int MotionGapDays { get; init; }
    public double DecreeMandate { get; init; }
    public double StartingMandate { get; init; }

    /// <summary>Where Mandate settles with nothing done to earn more, and the share of the gap to it closed each day.</summary>
    public double MandateRest { get; init; } = 50;
    public double MandateEase { get; init; }

    /// <summary>Watchmen the charter pays for from the first day.</summary>
    public int StartingWatch { get; init; }

    /// <summary>Councillors' secrets: digging for them, and spending them (<see cref="Systems.Secrets"/>).</summary>
    public Systems.SecretRules Secrets { get; init; } = new();
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

    /// <summary>Strength multiplier of stone walls round the old town, and of the city wall, in place of the palisade's.</summary>
    public double StoneWall { get; init; } = 2.0;
    public double CityWall { get; init; } = 2.6;
    public double PerTower { get; init; }

    /// <summary>Raiders come only when they expect to win: their numbers against this share of the defenders they expect.</summary>
    public double RaidNerve { get; init; } = 0.8;

    /// <summary>Chances a raid that gets through kills someone, and (for a very hostile foe) burns what it hit.</summary>
    public double RaidKill { get; init; } = 0.15;
    public double RaidBurn { get; init; } = 0.25;

    /// <summary>How much more the watch can do against a raid a full two-day sky watch saw coming (<see cref="Systems.Institutions.SkyWatch"/>).</summary>
    public double SkyWatch { get; init; } = 0.5;
}

public sealed record WorkSpec
{
    public double Cost { get; init; }
    public double Timber { get; init; }
    public double Stone { get; init; }
    public int Days { get; init; }
    public int Max { get; init; }

    /// <summary>The town tier that unlocks it; empty for any.</summary>
    public string Tier { get; init; } = "";
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

    /// <summary>Past this day a warband that has not marched grows restless: the confidence it needs wanes by this much a day, down to the floor.</summary>
    public int MarchWaneAfter { get; init; }
    public double MarchWanePerDay { get; init; }
    public double MarchWaneFloor { get; init; } = 1;

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
    public WorkSpec StoneWall { get; init; } = new() { Cost = 900, Timber = 20, Stone = 120, Days = 12, Tier = "town" };
    public WorkSpec CityWall { get; init; } = new() { Cost = 2400, Timber = 40, Stone = 320, Days = 20, Tier = "city" };
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

    /// <summary>Days a caster rests after a scrying before casting another: the more scryers in town, the more scryings a day.</summary>
    public int ScryRestDays { get; init; } = 2;

    /// <summary>What a caster asks to scry, by their loyalty: free at or above Free, gold at or above Favour, a favour below it.</summary>
    public ScryPriceRules ScryPrice { get; init; } = new();
    public WatchtowerRules Watchtowers { get; init; } = new();
}

public sealed record ScryPriceRules
{
    public double Free { get; init; } = 70;
    public double Favour { get; init; } = 40;

    /// <summary>Loyalty a caster scrying as a favour loses: they will not forget being asked.</summary>
    public double FavourLoyalty { get; init; } = 6;
}

/// <summary>Watchtowers: each sees a circle of the wilds live around itself, wider as the town grows.</summary>
public sealed record WatchtowerRules
{
    public string Building { get; init; } = "watchtower";

    /// <summary>Tiles a tower sees around itself, plus more for every hundred residents, up to the most.</summary>
    public double Sight { get; init; } = 30;
    public double SightPerHundred { get; init; } = 4;
    public double MostSight { get; init; } = 70;

    /// <summary>The town may raise one tower for every this many residents (always one).</summary>
    public int ResidentsEach { get; init; } = 100;
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

/// <summary>Spell scrolls as the scriptorium sells them: one area spell, read once by any hero who casts.</summary>
public sealed record ScrollRules
{
    public string Item { get; init; } = "spell-scroll-1st-level";
    public double Price { get; init; } = 50;
    public double Wholesale { get; init; } = 20;
    public int Carry { get; init; } = 1;
    public string Dice { get; init; } = "3d6";
    public int Targets { get; init; } = 3;

    /// <summary>Buildings that sell them.</summary>
    public IReadOnlyList<string> Shops { get; init; } = ["scriptorium", "mage_tower"];

    /// <summary>Chance a hoard holds a scroll for a caster in the party.</summary>
    public double Found { get; init; } = 0.2;

    /// <summary>The class that copies a found scroll into its spellbook instead of carrying it, the damage each copied spell adds to its area spell, and the most it copies.</summary>
    public string Copier { get; init; } = "wizard";
    public int CopyBonus { get; init; } = 1;
    public int MaxCopied { get; init; } = 4;
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
    public ScrollRules Scroll { get; init; } = new();
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

    /// <summary>How a raiding lair picks its night, by weight, rolled when it first acts; the dead keep their own.</summary>
    public IReadOnlyDictionary<string, LairHabit> Habits { get; init; } = new Dictionary<string, LairHabit>();
    public string UndeadHabit { get; init; } = "";

    /// <summary>Days in a moon, and the dark days at its end when the moon-bound come.</summary>
    public int MoonDays { get; init; } = 28;
    public int DarkDays { get; init; } = 3;

    /// <summary>No lair raids within this many days of another's raid: they keep no common calendar.</summary>
    public int StaggerDays { get; init; } = 3;

    /// <summary>Hours between a band being seen on its way and falling on its mark (<see cref="Systems.Sightings"/>).</summary>
    public int BandHours { get; init; } = 24;

    /// <summary>A bold tribe's bands are this much bigger.</summary>
    public double BoldBand { get; init; } = 1.3;

    /// <summary>Each time the town turns a band back, the next is this much bigger, up to so many times.</summary>
    public double RebuffBand { get; init; } = 0.25;
    public int MaxRebuffs { get; init; } = 4;

    /// <summary>A threat that comes to nothing this many times running comes back worse, with this much more strength.</summary>
    public int EscalateAt { get; init; } = 3;
    public double EscalateStrength { get; init; } = 25;
}

/// <summary>A lair's way of choosing its night, and what its card says about it.</summary>
public sealed record LairHabit
{
    public double Weight { get; init; } = 1;
    public string Text { get; init; } = "";
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

/// <summary>One ring of country around the town: how far out it lies and what level of party it suits.</summary>
public sealed record RingDef
{
    /// <summary>Days on the road from town.</summary>
    public int Days { get; init; } = 1;

    /// <summary>Lowest and highest party level its sites are built for.</summary>
    public int[] Levels { get; init; } = [1, 1];
    public int Regions { get; init; }
}

/// <summary>
/// A ready-made enemy group (groups.json): a site of its own in the themes it names, held by its own
/// creatures and bosses rather than whatever the country breeds, and heir to its own lineage in history.json.
/// </summary>
public sealed record GroupDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>The history.json lineage its menaces carry (its own, or an existing enemy it is a version of).</summary>
    public string Lineage { get; init; } = "";

    /// <summary>Region themes it may hold a site in.</summary>
    public IReadOnlyList<string> Themes { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Habitats { get; init; } = [];
    public IReadOnlyList<string> Deck { get; init; } = [];

    /// <summary>Library monster ids that wander its site and escort its boss.</summary>
    public IReadOnlyList<string> Foes { get; init; } = [];

    /// <summary>Library monster ids that may lead it.</summary>
    public IReadOnlyList<string> Bosses { get; init; } = [];

    /// <summary>Encounter lines by deck kind ("wander", "boss"...), used before the theme's own.</summary>
    public Dictionary<string, List<string>> Texts { get; init; } = [];
}

public sealed record GroupRules
{
    /// <summary>Chance a generated site in a theme with groups goes to one of them instead, when one fits its level.</summary>
    public double Chance { get; init; }
    public IReadOnlyList<GroupDef> Groups { get; init; } = [];
}

/// <summary>A kind of adventure site a region theme can hold: how it is named and what its deck is made of.</summary>
/// <summary>A signature foe: one site in fitting country given over to this master (content/game/foes.json).</summary>
public sealed record FoeDef
{
    public string Id { get; init; } = "";
    public string Monster { get; init; } = "";
    public string Escort { get; init; } = "";
    public int[] EscortCount { get; init; } = [1, 2];

    /// <summary>The lair kind it behaves as (tribe raids, beasts hunt, undead walk, rivals toll).</summary>
    public string Kind { get; init; } = "tribe";

    /// <summary>Nearest and farthest ring it may hold (0 near), and the country it likes, best first.</summary>
    public int[] Rings { get; init; } = [0, 4];
    public IReadOnlyList<string> Themes { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public string Text { get; init; } = "";

    /// <summary>Days of road its lair's raids reach the town from (ordinary lairs: 1).</summary>
    public int Reach { get; init; } = 1;

    /// <summary>It calls up the raiders who fell at the town to walk again in its next walk.</summary>
    public bool Raises { get; init; }

    /// <summary>Its seat is lifted into the sky as a floating isle (<see cref="SiteDef.Aloft"/>).</summary>
    public bool Aloft { get; init; }
}

public sealed record FoeRules
{
    public IReadOnlyList<FoeDef> Foes { get; init; } = [];

    /// <summary>Lair strength a raising foe gains for each raider who fell at the town, and the most at once.</summary>
    public double Raise { get; init; } = 0.5;
    public double RaiseMax { get; init; } = 25;
    public string RaiseText { get; init; } = "";

    /// <summary>The rival dark ranger who takes the town's bounties (<see cref="Systems.Poaching"/>).</summary>
    public Systems.PoachingRules Poaching { get; init; } = new();

    public FoeDef? Of(string id) => id == "" ? null : Foes.FirstOrDefault(f => f.Id == id);
}

public sealed record SiteKindDef
{
    /// <summary>Name patterns; {A} takes one of the theme's words and {B} one of its names.</summary>
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Habitats { get; init; } = [];

    /// <summary>Encounter kinds in the site's deck, before the boss.</summary>
    public IReadOnlyList<string> Deck { get; init; } = [];
}

/// <summary>A region archetype: mountains, deep forest, caverns, castle ruins...</summary>
public sealed record ThemeDef
{
    public required string Id { get; init; }

    /// <summary>What the region is, in a word or two ("mountains").</summary>
    public string Name { get; init; } = "";

    /// <summary>Region name patterns, with {A} and {B} as for sites.</summary>
    public IReadOnlyList<string> Regions { get; init; } = [];
    public IReadOnlyList<string> Words { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Habitats { get; init; } = [];
    public int Weight { get; init; } = 1;

    /// <summary>How hot (1) or cold (-1), and wet (1) or dry (-1), the country is; null fits any climate (caverns, the fey).</summary>
    public double? Heat { get; init; }
    public double? Wet { get; init; }
    public int MinRing { get; init; }
    public int MaxRing { get; init; } = 99;

    /// <summary>Peoples who may have a settlement in the region; the first is the likeliest.</summary>
    public IReadOnlyList<string> Peoples { get; init; } = [];

    /// <summary>Chance the region holds a settlement; null uses the world's default.</summary>
    public double? SettlementChance { get; init; }

    /// <summary>The terrain as a returning party describes it.</summary>
    public string Text { get; init; } = "";

    /// <summary>Old stories of the fallen powers that held it (content/game/history.json), one heard on the first visit.</summary>
    public IReadOnlyList<string> History { get; init; } = [];
    public IReadOnlyList<SiteKindDef> Sites { get; init; } = [];

    /// <summary>Scene lines by encounter kind (wander, trap, hazard, puzzle, parley, treasure, boss).</summary>
    public IReadOnlyDictionary<string, List<string>> Texts { get; init; } = new Dictionary<string, List<string>>();
}

/// <summary>What an allied people sends the town.</summary>
public sealed record BoonDef
{
    public Good? Good { get; init; }

    /// <summary>Goods a day into the market.</summary>
    public double Qty { get; init; }

    /// <summary>Daily chance one of their heroes comes to the town's taverns.</summary>
    public double HeroChance { get; init; }
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>Warriors pledged to the town's walls, counted in its defence like watchmen are.</summary>
    public double Troops { get; init; }

    /// <summary>They share their maps: every region next to theirs becomes known.</summary>
    public bool Maps { get; init; }
    public string Text { get; init; } = "";

    /// <summary>Hard country (by kind) an ally's guides take the town's companies through faster and safer: share of its slowness taken off.</summary>
    public IReadOnlyDictionary<string, double> Guides { get; init; } = new Dictionary<string, double>();
}

/// <summary>A people with settlements out in the wilds: dwarves, elves, villagers...</summary>
public sealed record PeopleDef
{
    public required string Id { get; init; }

    /// <summary>"the dwarves of {S}", {S} being the settlement.</summary>
    public string Name { get; init; } = "";
    public string People { get; init; } = "";

    /// <summary>What their settlements are: hold, enclave, village...</summary>
    public string Kind { get; init; } = "";

    /// <summary>The race (names.json) of heroes they send; empty sends none.</summary>
    public string Race { get; init; } = "";

    /// <summary>Starting standing with the town, -100 to 100.</summary>
    public double Start { get; init; }

    /// <summary>Where they come from in the world's history, told when they ally.</summary>
    public string History { get; init; } = "";
    public IReadOnlyList<string> Settlements { get; init; } = [];
    public BoonDef Boon { get; init; } = new();

    /// <summary>What they buy from the town and sell to it along a trade route (<see cref="Systems.Trade"/>).</summary>
    public Systems.TradeGoodsDef Trade { get; init; } = new();
}

public sealed record StandingRules
{
    /// <summary>Standing at which a people allies with the town.</summary>
    public double Ally { get; init; } = 60;
    public double EnvoyCost { get; init; } = 60;
    public double Envoy { get; init; } = 10;
    public int EnvoyEveryDays { get; init; } = 8;

    /// <summary>From this day, every meetEvery days, the nearest people the town has heard of but not met sends riders.</summary>
    public int MeetFrom { get; init; } = 3;
    public int MeetEvery { get; init; } = 5;

    /// <summary>Standing for clearing a site in a people's region.</summary>
    public double Cleared { get; init; } = 8;

    /// <summary>Extra standing when the site was one they pleaded for help with.</summary>
    public double Plea { get; init; } = 14;
    public double PleaIgnored { get; init; } = -4;
    public int[] PleaEveryDays { get; init; } = [5, 10];
    public int PleaDays { get; init; } = 14;
    public double PleaRewardPerLevel { get; init; } = 30;

    /// <summary>How far the town trusts what a plea, a visit or an ally's maps say about a site.</summary>
    public int PleaTrust { get; init; } = 80;
    public int VisitTrust { get; init; } = 60;
    public int MapTrust { get; init; } = 70;

    /// <summary>Pleas open across all settlements at once.</summary>
    public int MaxOpen { get; init; } = 3;

    /// <summary>Daily drift of standing back toward where the people started.</summary>
    public double Drift { get; init; } = 0.5;
}

/// <summary>How free companies of heroes choose where to go.</summary>
public sealed record CompanyRules
{
    public int MinMembers { get; init; } = 3;
    public int MaxMembers { get; init; } = 4;

    /// <summary>Heroes only band together within this many levels of their leader.</summary>
    public int LevelSpread { get; init; } = 2;

    /// <summary>Daily chance a ready company sets out rather than drinking another day away.</summary>
    public double SetOutChance { get; init; } = 0.6;

    /// <summary>Appeal lost per level a site is below the company: they have outgrown it.</summary>
    public double Outleveled { get; init; } = 0.35;

    /// <summary>Appeal lost per day of travel.</summary>
    public double Travel { get; init; } = 0.08;
    public double MinScore { get; init; } = 0.3;

    /// <summary>Levels above their own that bold heroes will try.</summary>
    public int BoldStep { get; init; } = 1;

    /// <summary>Once the warband raids, companies go no farther than this many days from town.</summary>
    public int WarDays { get; init; } = 2;

    /// <summary>Extra appeal of burning the warband's camp.</summary>
    public double CampGlory { get; init; } = 0.4;

    /// <summary>Expeditions after which a hero is a veteran who can lead greenhorns.</summary>
    public int Veteran { get; init; } = 3;

    /// <summary>Levels a company of greenhorns with no veteran to lead them steps down when picking a site.</summary>
    public int GreenStep { get; init; } = 1;
}

/// <summary>When a party in a delve gives up, and how it tends its fallen.</summary>
public sealed record DelveRules
{
    /// <summary>Share of the party's hit points left at which they break off a fight (a boss fight, which they press harder).</summary>
    public double BreakOff { get; init; } = 0.2;
    public double BossBreakOff { get; init; } = 0.15;

    /// <summary>They also break off once this share of the living party is down.</summary>
    public double DownedShare { get; init; } = 1;

    /// <summary>Share of hit points below which they turn back between encounters.</summary>
    public double TurnBack { get; init; } = 0.3;

    /// <summary>DC of the Wisdom (Medicine) check a companion makes to stabilize the fallen; 0 means no first aid.</summary>
    public int FirstAidDc { get; init; }
}

/// <summary>A kind of country that bends the roads: a ridge, a mire, a tangled wood (content/game/roads.json).</summary>
public sealed record TerrainKindDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>"over" the ridge, "through" the mire, "across" the gorge.</summary>
    public string Through { get; init; } = "through";
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Words { get; init; } = [];

    /// <summary>How much slower the going is inside it than on open ground.</summary>
    public double Slow { get; init; } = 1.5;

    /// <summary>How much a party fears crossing it, before weighing its own strength.</summary>
    public double Danger { get; init; } = 1;
    public TerrainHazardDef Hazard { get; init; } = new();

    /// <summary>Chance something attacks a party crossing it.</summary>
    public double Ambush { get; init; }
    public IReadOnlyList<string> Habitats { get; init; } = [];
    public IReadOnlyList<string> AmbushText { get; init; } = [];

    /// <summary>"water" (a lake) or "sheer" (cliffs and peaks): nobody walks across, only flies, swims or climbs (<see cref="Systems.Passage"/>); "" for country anyone can.</summary>
    public string Barrier { get; init; } = "";

    /// <summary>Nature as a monster: now and then the crossing is a fight with the country itself (<see cref="Systems.Wayfaring"/>).</summary>
    public Systems.PerilDef? Peril { get; init; }

    /// <summary>A strange place that bends the rules: a wishing well, dead-magic ground, a bone ferryman (<see cref="Systems.Wayfaring"/>).</summary>
    public Systems.WonderDef? Wonder { get; init; }

    /// <summary>How much wider than other hard country it spreads across its region: wide country is costly to go round.</summary>
    public double Wide { get; init; } = 1;
}

public sealed record TerrainHazardDef
{
    /// <summary>The check each member makes (a <see cref="SkillKind"/>).</summary>
    public string Skill { get; init; } = SkillKind.Hazard;

    /// <summary>DC before the country's level is added.</summary>
    public int Dc { get; init; } = 11;
    public int Die { get; init; } = 6;
    public IReadOnlyList<string> Text { get; init; } = [];
}

/// <summary>How a party weighs a short, risky road against a long, safe one.</summary>
public sealed record RouteChoiceRules
{
    /// <summary>Cost of a day's extra march.</summary>
    public double TimeWeight { get; init; } = 1;

    /// <summary>Cost of a unit of danger for a party of the country's level.</summary>
    public double RiskWeight { get; init; } = 1.4;

    /// <summary>Danger multipliers: a bold leader, green heroes with no veteran, a party coming home hurt.</summary>
    public double Bold { get; init; } = 0.55;
    public double Green { get; init; } = 1.5;
    public double Wounded { get; init; } = 2.2;

    /// <summary>Time multipliers: a lazy leader, people waiting on them (a plea, the warband raiding).</summary>
    public double Lazy { get; init; } = 1.6;
    public double Urgent { get; init; } = 1.6;

    /// <summary>Spread of the party's mood on the day, in units of cost.</summary>
    public double Noise { get; init; } = 0.5;
}

public sealed record ShortRestRules
{
    /// <summary>They rest when the party is below this share of its hit points.</summary>
    public double Below { get; init; } = 0.75;
    public int PerLongRest { get; init; } = 2;

    /// <summary>Chance a short rest inside a site draws a wandering band.</summary>
    public double WanderChance { get; init; } = 0.12;
}

public sealed record LongRestRules
{
    /// <summary>Share of their hit dice heroes get back from a night's sleep.</summary>
    public double HitDice { get; init; } = 0.5;
}

public sealed record RetreatRules
{
    /// <summary>Times a beaten party falls back, sleeps and goes in again.</summary>
    public int Retries { get; init; } = 1;

    /// <summary>They only try again with at least this many still on their feet.</summary>
    public int MinStanding { get; init; } = 2;

    /// <summary>A party below this share of hit points that has a retry left falls back to rest rather than going home.</summary>
    public double TurnBack { get; init; } = 0.5;

    /// <summary>After being driven off, they only come back if the foes were this badly hurt or worse (share of their hit points left).</summary>
    public double FoeShare { get; init; } = 0.5;

    /// <summary>Extra creatures waiting for them when they come back.</summary>
    public int Alerted { get; init; } = 1;

    /// <summary>How much sooner (share of hit points) they break off a fight on a second try, and in a fight on the road.</summary>
    public double Caution { get; init; } = 0.15;
    public double RoadCaution { get; init; } = 0.25;

    /// <summary>Whether greenhorns with no veteran among them try again; without one to steady them, they go home.</summary>
    public bool GreenRetry { get; init; } = true;

    /// <summary>How much sooner greenhorns with no veteran among them break off a fight.</summary>
    public double GreenCaution { get; init; }
}

public sealed record CampRules
{
    /// <summary>Nightly chance of trouble at a camp, plus this much per level of the country.</summary>
    public double Ambush { get; init; } = 0.05;
    public double PerTier { get; init; } = 0.006;

    /// <summary>The watch's Wisdom (Perception) DC to see it coming.</summary>
    public int WatchDc { get; init; } = 12;

    /// <summary>Reaching a site after this hour, a party camps and goes in at dawn.</summary>
    public int LateHour { get; init; } = 14;
}

/// <summary>Roads, rests and the country between the town and its sites (content/game/roads.json).</summary>
/// <summary>
/// Long mountain walls laid between the rings of the wilds. Roads must go through their passes, which open wherever a site
/// stands in the wall's line and at a few spots besides: the choke points of the far country.
/// </summary>
public sealed record RangeRules
{
    /// <summary>The terrain kind the walls are made of (a sheer barrier).</summary>
    public string Kind { get; init; } = "";

    /// <summary>The ring a wall first stands beyond: none nearer the town than the outside of this ring.</summary>
    public int FromRing { get; init; } = 3;

    /// <summary>Walls laid in each gap between rings, least and most.</summary>
    public int[] PerGap { get; init; } = [0, 0];

    /// <summary>Degrees of the town's horizon a wall spans, least and most.</summary>
    public double[] Arc { get; init; } = [40, 90];

    /// <summary>Its depth in days, least and most.</summary>
    public double[] Depth { get; init; } = [0.3, 0.45];

    /// <summary>Passes cut besides those at sites in the wall's line, least and most.</summary>
    public int[] Passes { get; init; } = [1, 2];

    /// <summary>Half a pass's width, in days.</summary>
    public double PassHalf { get; init; } = 0.3;

    /// <summary>The shortest stretch of wall worth standing, in days along it.</summary>
    public double Least { get; init; } = 0.8;
}

public sealed record RoadRules
{
    /// <summary>Rope bridges over gorges, cut by raiders and rebuilt in stone (<see cref="Systems.Bridges"/>).</summary>
    public Systems.BridgeRules Bridges { get; init; } = new();

    /// <summary>Jobs on the road, strange places and old gateways (<see cref="Systems.Wayfaring"/>).</summary>
    public Systems.WayRules Wayfaring { get; init; } = new();

    /// <summary>Hours a party marches a day; a day's march is one day of distance on open ground.</summary>
    public int MarchHours { get; init; } = 10;
    public int Dawn { get; init; } = 6;

    /// <summary>When the midday short rest starts.</summary>
    public int Noon { get; init; } = 11;
    public ShortRestRules ShortRest { get; init; } = new();
    public LongRestRules LongRest { get; init; } = new();
    public RetreatRules Retreat { get; init; } = new();
    public CampRules Camp { get; init; } = new();
    public int[] FeaturesPerRegion { get; init; } = [1, 2];

    /// <summary>Share of its region's arc a feature spans.</summary>
    public double[] Arc { get; init; } = [0.35, 0.7];

    /// <summary>Days a detour keeps clear of a feature's edge.</summary>
    public double Margin { get; init; } = 0.12;
    public RouteChoiceRules Choice { get; init; } = new();
    public IReadOnlyDictionary<string, List<string>> Texts { get; init; } = new Dictionary<string, List<string>>();
    public IReadOnlyList<TerrainKindDef> Kinds { get; init; } = [];

    /// <summary>The kinds of country each region theme puts in front of its sites (a kind twice is twice as likely).</summary>
    public IReadOnlyDictionary<string, List<string>> Themes { get; init; } = new Dictionary<string, List<string>>();

    /// <summary>Mountain walls between the rings, broken only at their passes (<see cref="Systems.Roads.Ranges"/>).</summary>
    public RangeRules Ranges { get; init; } = new();
}

/// <summary>
/// What the town pays companies for reports it never paid a bounty for, and how a company's dealings
/// with the town hall sway its price (<see cref="Systems.Intel"/>).
/// </summary>
public sealed record IntelRules
{
    /// <summary>Gold any first-hand account is worth, even one that tells the town nothing new.</summary>
    public double Base { get; init; } = 10;

    /// <summary>Gold for a region the town has never had described, plus this much per ring out.</summary>
    public double NewRegion { get; init; } = 30;
    public double PerRing { get; init; } = 15;

    /// <summary>Gold for setting the town right about a site, plus this much per level of the site.</summary>
    public double NewSite { get; init; } = 20;
    public double PerTier { get; init; } = 6;

    /// <summary>Days over which a report loses its worth, down to MinFresh of it; after ForgetDays it is not for sale.</summary>
    public int FreshDays { get; init; } = 45;
    public double MinFresh { get; init; } = 0.25;
    public int ForgetDays { get; init; } = 120;

    /// <summary>Asking price as a share of the worth from a company that loves the town (100) to one that loathes it (0).</summary>
    public double Friendly { get; init; } = 0.8;
    public double Hostile { get; init; } = 1.5;
    public double Greedy { get; init; } = 0.15;

    /// <summary>Below this relation a company will not sell to the town at all.</summary>
    public double RefuseBelow { get; init; } = 20;

    /// <summary>The council's haggled offer as a share of the asking price, and days a company sulks after turning it down.</summary>
    public double Haggle { get; init; } = 0.7;
    public int SulkDays { get; init; } = 6;

    /// <summary>Dealings points: paying the asking price, a deal haggled down, an offer turned down, a town bounty completed.</summary>
    public double Fair { get; init; } = 2;
    public double Grumble { get; init; } = -1;
    public double Insult { get; init; } = -4;
    public double Bounty { get; init; } = 3;
    public double MaxDealings { get; init; } = 30;

    /// <summary>Treasury the stand-in mayor keeps back before buying a report.</summary>
    public double Reserve { get; init; } = 150;
}

/// <summary>The wilds: rings of themed regions around the town, their sites and the peoples living there (content/game/world.json).</summary>
public sealed record ClimateRules
{
    /// <summary>How strongly a ring feels the climate, from the town's ring out; the last holds beyond.</summary>
    public double[] Amp { get; init; } = [0.25, 0.5, 0.75, 0.95, 1.0];

    /// <summary>How far from its climate a theme may be and still be likely.</summary>
    public double Spread { get; init; } = 0.35;

    /// <summary>How much wet-dry counts against hot-cold.</summary>
    public double WetShare { get; init; } = 0.6;

    /// <summary>A theme this much hotter or colder than the region is never picked while another fits.</summary>
    public double Hard { get; init; } = 1.1;

    /// <summary>Neighbouring regions never differ by this much heat: no jungle beside the tundra.</summary>
    public double Clash { get; init; } = 1.5;
}

public sealed record WorldRules
{
    public IReadOnlyList<RingDef> Rings { get; init; } = [];
    public int[] SitesPerRegion { get; init; } = [2, 3];

    /// <summary>How the lands follow hot-cold and wet-dry (<see cref="Systems.World.Climate"/>).</summary>
    public ClimateRules Climate { get; init; } = new();

    /// <summary>Experience a wandering band is worth, by party level (index = level - 1).</summary>
    public int[] Budget { get; init; } = [];

    /// <summary>A hoard holds about this much gold times level^1.5.</summary>
    public double HoardPerLevel { get; init; } = 60;
    public double SettlementChance { get; init; } = 0.75;
    public StandingRules Standing { get; init; } = new();
    public CompanyRules Companies { get; init; } = new();
    public DelveRules Delve { get; init; } = new();
    public IntelRules Intel { get; init; } = new();
    public IReadOnlyList<PeopleDef> Peoples { get; init; } = [];
    public IReadOnlyList<ThemeDef> Themes { get; init; } = [];
    public IsleRules Isles { get; init; } = new();
    public Systems.RuinRules Ruins { get; init; } = new();
}

/// <summary>Floating isles in the far wilds (world.json "isles"), reached only by air.</summary>
public sealed record IsleRules
{
    public int Count { get; init; }
    public int[] Rings { get; init; } = [3, 4];
    public IReadOnlyList<string> Themes { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
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

    /// <summary>The themed regions, sites and peoples of the wilds.</summary>
    public WorldRules World { get; init; } = new();

    /// <summary>Roads, rests and the country that bends them.</summary>
    public RoadRules Roads { get; init; } = new();

    /// <summary>How lairs grow, act and are resettled.</summary>
    public LairRules Lairs { get; init; } = new();
    public GroupRules Groups { get; init; } = new();

    /// <summary>How taverns trade in rumours.</summary>
    public RumorRules Rumors { get; init; } = new();

    /// <summary>The shrine, the temple and the general store's potions.</summary>
    public ShrineRules Shrines { get; init; } = new();

    /// <summary>Numbers for the pantheon in town (the gods themselves are in the library).</summary>
    public FaithRules Faith { get; init; } = new();

    /// <summary>The factions that can take root in town and how they behave.</summary>
    public FactionRules Factions { get; init; } = new();

    /// <summary>The campaign arc: acts, festering menaces, dread and the finale (content/game/saga.json).</summary>
    public SagaRules Saga { get; init; } = new();

    /// <summary>What heroes can buy, where, and what they get for loot they don't need (content/game/gear.json).</summary>
    public GearRules Gear { get; init; } = new();

    /// <summary>Town tiers, wards, house levels and what households want (content/game/city.json).</summary>
    public CityRules City { get; init; } = new();

    /// <summary>Heroes' loyalty, knighthoods, fiefs and the town's champion (content/game/loyalty.json).</summary>
    public LoyaltyRules Loyalty { get; init; } = new();

    /// <summary>Decorations and set dressing the player can place (content/game/decor.json).</summary>
    public DecorRules Decor { get; init; } = new();
    public SeasonRules Seasons { get; init; } = new();

    /// <summary>The crops a field can be sown with (content/game/farming.json).</summary>
    public FarmingRules Farming { get; init; } = new();
    public TradeRules Trade { get; init; } = new();

    /// <summary>The town armory: gifts, loot terms and pacts, buying loot (content/game/armory.json).</summary>
    public ArmoryRules Armory { get; init; } = new();

    /// <summary>The alchemy bench: ingredients, recipes and what potions do (content/game/alchemy.json).</summary>
    public AlchemyRules Alchemy { get; init; } = new();
    public NeighbourRules Neighbours { get; init; } = new();
    public LegacyRules Legacies { get; init; } = new();
    public UndercityRules Undercity { get; init; } = new();

    public ResearchRules Research { get; init; } = new();

    /// <summary>The world's history: its ages, the powers that rose and fell, and the calendar (content/game/history.json).</summary>
    public HistoryRules History { get; init; } = new();

    /// <summary>Story events and the mayor's character sheet (content/game/stories.json).</summary>
    public StoryRules Stories { get; init; } = new();

    /// <summary>Sending-stone quests from parties on the road (content/game/sendings.json); their stories are among <see cref="Stories"/> too.</summary>
    public SendingRules Sendings { get; init; } = new();

    /// <summary>Adventuring companies' bastions: who may raise one, their facilities and turns (content/game/bastions.json).</summary>
    public BastionRules Bastions { get; init; } = new();

    /// <summary>The city's institutions: their keepers, services, choices and requests (content/game/institutions.json).</summary>
    public InstitutionRules Institutions { get; init; } = new();

    /// <summary>Sellsword companies for hire and how their campaigns go (content/game/mercenaries.json).</summary>
    public MercRules Mercenaries { get; init; } = new();

    /// <summary>The town's own army: units, training, laws and posts (content/game/army.json).</summary>
    public Systems.ArmyRules Army { get; init; } = new();

    /// <summary>Minor powers the town and its enemies court (content/game/powers.json).</summary>
    public PowerRules Powers { get; init; } = new();

    /// <summary>Heroes' tempers and what they think of the mayor's choices (content/game/regard.json).</summary>
    public RegardRules Regard { get; init; } = new();

    /// <summary>What heroes say in the dark, at the table and over the graves of the fallen (content/game/voices.json).</summary>
    public VoiceRules Voices { get; init; } = new();
    /// <summary>Exploring companies and what they find (content/game/explore.json).</summary>
    public ExploreRules Explore { get; init; } = new();
    /// <summary>The town guard's posts and the wall works past the stone walls (content/game/garrison.json).</summary>
    public GarrisonRules Garrison { get; init; } = new();

    /// <summary>Bounty terms and townsfolk carried off in raids (content/game/bounties.json).</summary>
    public BountyRules Bounties { get; init; } = new();
    public BroadsheetRules Broadsheet { get; init; } = new();
    public ClockworkRules Clockwork { get; init; } = new();
    /// <summary>The tax rate and the unrest of underfunded services (content/game/taxes.json).</summary>
    public TaxRules Taxes { get; init; } = new();
    /// <summary>Trade guilds, their demands and their strikes (content/game/guilds.json).</summary>
    public GuildRules Guilds { get; init; } = new();

    /// <summary>Patrons beyond the world and their pacts with heroes (content/game/patrons.json).</summary>
    public PatronRules Patrons { get; init; } = new();
    public Systems.TavernRules Taverns { get; init; } = new();
    public Systems.CurseRules Curses { get; init; } = new();
    public Systems.DowntimeRules Downtime { get; init; } = new();
    public Systems.LawRules Laws { get; init; } = new();
    /// <summary>The top bar's month and when a trend counts as trouble (content/game/trends.json).</summary>
    public TrendRules Trends { get; init; } = new();
    public MerchantRules Merchants { get; init; } = new();
    public WardRules Wards { get; init; } = new();
    /// <summary>Fame and infamy for heroes and companies (content/game/notoriety.json).</summary>
    public NotorietyRules Notoriety { get; init; } = new();

    /// <summary>Signature foes placed in the wilds (content/game/foes.json).</summary>
    public FoeRules Foes { get; init; } = new();

    /// <summary>Tower cannons and war-wagons (content/game/cannons.json).</summary>
    public Systems.CannonRules Cannons { get; init; } = new();

    /// <summary>Dragon eggs, hatching and the town's own dragon (content/game/eggs.json).</summary>
    public EggRules Eggs { get; init; } = new();

    /// <summary>Companies' arms and how their colours wear (content/game/heraldry.json).</summary>
    public HeraldryRules Heraldry { get; init; } = new();

    /// <summary>Each faction's first-meeting picture and crest (content/game/intros.json).</summary>
    public Systems.IntroRules Intros { get; init; } = new();

    /// <summary>The charter goals of a new town's first days (content/game/goals.json).</summary>
    public Systems.GoalRules Goals { get; init; } = new();

    /// <summary>Walls, moats, the river and the ground nobody crosses on foot, and what crosses it anyway (content/game/passage.json).</summary>
    public PassageRules Passage { get; init; } = new();

    /// <summary>The ladders the client's odds, threat and stakes bars read (content/game/assess.json).</summary>
    public AssessRules Assess { get; init; } = new();

    /// <summary>Townsfolk and heroes' skills and everyday checks (content/game/skills.json).</summary>
    public SkillRules Skills { get; init; } = new();

    /// <summary>Enemy powers' end-game crises, their blows against the town and their clashes (content/game/crises.json).</summary>
    public CrisisRules Crises { get; init; } = new();

    /// <summary>The yearly tourney: its events, levels, champions and betting (content/game/tournaments.json).</summary>
    public TourneyRules Tourney { get; init; } = new();
    /// <summary>How many fit in a tavern, shrine or bathhouse at once, and what the townsfolk do when it is full (content/game/crowds.json).</summary>
    public CrowdRules Crowds { get; init; } = new();

    /// <summary>Townsfolk's inspirations and breaks (content/game/moods.json).</summary>
    public MoodRules Moods { get; init; } = new();

    /// <summary>The storytellers a town can be founded under, the game's difficulty (content/game/storytellers.json).</summary>
    public StorytellerRules Storytellers { get; init; } = new();
    public Systems.CharacterRules Character { get; init; } = new();
    public Systems.TacticRules Tactics { get; init; } = new();

    /// <summary>The roads to victory and what each needs (content/game/victory.json).</summary>
    public Systems.VictoryRules Victory { get; init; } = new();

    /// <summary>The big bad's own quests and what they give it (content/game/darkquests.json).</summary>
    public Systems.DarkQuestRules DarkQuests { get; init; } = new();

    /// <summary>Boons won campaigns leave for the next, and how a win is graded (content/game/boons.json).</summary>
    public Systems.BoonRules Boons { get; init; } = new();

    /// <summary>Endless mode: the heralds of each new era and the complications they bring (content/game/endless.json).</summary>
    public Systems.EndlessRules Endless { get; init; } = new();

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
            Int = m.Int > 0 ? m.Int : lm.Int,
            Attack = m.Attack > 0 ? m.Attack : lm.ToHit,
            Damage = m.Damage != "" ? m.Damage : lm.Damage?.ToString() ?? "1d4",
            Attacks = m.Attacks > 0 ? m.Attacks : lm.Attacks,
            Xp = m.Xp > 0 ? m.Xp : Library.XpForCr(lm.Cr),
            Undead = m.Undead || lm.Type == "undead",
            Type = m.Type != "" ? m.Type : lm.Type,
        };
    }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, MonsterDef> _bosses = new();

    /// <summary>A library creature leading a band: the same stats, fought as the boss ("id@boss").</summary>
    public MonsterDef BossOf(string id) => _bosses.GetOrAdd($"{id}@boss", key => Monster(id) with { Id = key, Boss = true });

    public MonsterDef Monster(string id) =>
        _monsters.TryGetValue(id, out var m) ? m
        : id.EndsWith("@boss", StringComparison.Ordinal) ? BossOf(id[..^5])
        : throw new KeyNotFoundException($"Unknown monster {id}");

    /// <summary>Library creatures that can wander into a site: any of its habitats, inside the challenge range, with an attack to fight with. A group's site draws from the group's own foes when any fit the range.</summary>
    public List<MonsterDef> Wanderers(SiteDef site, double[] cr)
    {
        if (Group(site.Group) is GroupDef g)
        {
            var own = Library.Monsters
                .Where(lm => g.Foes.Contains(lm.Id) && lm.Damage != null && lm.Cr >= cr[0] && lm.Cr <= cr[^1] && lm.Band != "")
                .Select(lm => _monsters[lm.Id])
                .ToList();
            if (own.Count > 0) return own;
        }
        return Library.Monsters
            .Where(lm => lm.Damage != null && lm.Cr >= cr[0] && lm.Cr <= cr[^1] && lm.Band != "" && !lm.Boss && lm.Habitats.Any(site.Habitats.Contains))
            .Select(lm => _monsters[lm.Id])
            .ToList();
    }

    /// <summary>The ready-made enemy group with this id, or null for "" and unknown ids.</summary>
    public GroupDef? Group(string id) => id == "" ? null : Groups.Groups.FirstOrDefault(g => g.Id == id);

    public BuildingDef Building(string id) =>
        _buildings.TryGetValue(id, out var b) ? b : throw new KeyNotFoundException($"Unknown building {id}");

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    // Lazy so tests starting in parallel share one instance: saves resolve content by reference.
    private static readonly Lazy<Content> _default = new(() => Load(ReadEmbedded));

    /// <summary>The bundled content, parsed once and shared (content is immutable).</summary>
    public static Content Default() => _default.Value;

    /// <summary>Load content from a directory of JSON files laid out like the repo's content/game/.</summary>
    public static Content FromDirectory(string dir) => Load(name => File.ReadAllText(Path.Combine(dir, name)));

    private static Content Load(Func<string, string> read)
    {
        T Parse<T>(string file) => JsonSerializer.Deserialize<T>(read(file), JsonOptions) ?? throw new InvalidDataException($"Empty content file {file}");
        // The institutions' requests are story events like any other, kept with the institutions.
        var institutions = Parse<InstitutionRules>("institutions.json");
        var stories = WithSendings(Parse<StoryRules>("stories.json"), Parse<SendingRules>("sendings.json"));
        // So are the powers' first-contact scenes, kept with the powers.
        var powers = Parse<PowerRules>("powers.json");
        stories = stories with { Stories = [.. stories.Stories, .. institutions.Requests, .. powers.Contacts], Precedents = [.. stories.Precedents, .. institutions.Precedents] };
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
            World = Parse<WorldRules>("world.json"),
            Roads = Parse<RoadRules>("roads.json"),
            Rumors = Parse<RumorRules>("rumors.json"),
            Lairs = Parse<LairRules>("lairs.json"),
            Groups = Parse<GroupRules>("groups.json"),
            Shrines = Parse<ShrineRules>("shrines.json"),
            Faith = Parse<FaithRules>("faith.json"),
            Factions = Parse<FactionRules>("factions.json"),
            Saga = Parse<SagaRules>("saga.json"),
            Gear = Parse<GearRules>("gear.json"),
            Loyalty = Parse<LoyaltyRules>("loyalty.json"),
            City = Parse<CityRules>("city.json"),
            Decor = Parse<DecorRules>("decor.json"),
            Seasons = Parse<SeasonRules>("seasons.json"),
            Farming = Parse<FarmingRules>("farming.json"),
            Trade = Parse<TradeRules>("trade.json"),
            Armory = Parse<ArmoryRules>("armory.json"),
            Alchemy = Parse<AlchemyRules>("alchemy.json"),
            Neighbours = Parse<NeighbourRules>("neighbours.json"),
            Legacies = Parse<LegacyRules>("legacies.json"),
            Undercity = Parse<UndercityRules>("undercity.json"),
            Research = Parse<ResearchRules>("research.json"),
            History = Parse<HistoryRules>("history.json"),
            Stories = stories,
            Institutions = institutions,
            Sendings = Parse<SendingRules>("sendings.json"),
            Bastions = Parse<BastionRules>("bastions.json"),
            Mercenaries = Parse<MercRules>("mercenaries.json"),
            Army = Parse<Systems.ArmyRules>("army.json"),
            Powers = powers,
            Regard = Parse<RegardRules>("regard.json"),
            Voices = Parse<VoiceRules>("voices.json"),
            Explore = Parse<ExploreRules>("explore.json"),
            Garrison = Parse<GarrisonRules>("garrison.json"),
            Bounties = Parse<BountyRules>("bounties.json"),
            Broadsheet = Parse<BroadsheetRules>("broadsheet.json"),
            Clockwork = Parse<ClockworkRules>("clockwork.json"),
            Taxes = Parse<TaxRules>("taxes.json"),
            Guilds = Parse<GuildRules>("guilds.json"),
            Patrons = Parse<PatronRules>("patrons.json"),
            Taverns = Parse<Systems.TavernRules>("taverns.json"),
            Curses = Parse<Systems.CurseRules>("curses.json"),
            Downtime = Parse<Systems.DowntimeRules>("downtime.json"),
            Laws = Parse<Systems.LawRules>("laws.json"),
            Trends = Parse<TrendRules>("trends.json"),
            Merchants = Parse<MerchantRules>("merchants.json"),
            Wards = Parse<WardRules>("wards.json"),
            Notoriety = Parse<NotorietyRules>("notoriety.json"),
            Goals = Parse<Systems.GoalRules>("goals.json"),
            Passage = Parse<PassageRules>("passage.json"),
            Heraldry = Parse<HeraldryRules>("heraldry.json"),
            Intros = Parse<Systems.IntroRules>("intros.json"),
            Eggs = Parse<EggRules>("eggs.json"),
            Foes = Parse<FoeRules>("foes.json"),
            Cannons = Parse<Systems.CannonRules>("cannons.json"),
            Assess = Parse<AssessRules>("assess.json"),
            Skills = Parse<SkillRules>("skills.json"),
            Crises = Parse<CrisisRules>("crises.json"),
            Tourney = Parse<TourneyRules>("tournaments.json"),
            Crowds = Parse<CrowdRules>("crowds.json"),
            Moods = Parse<MoodRules>("moods.json"),
            Storytellers = Parse<StorytellerRules>("storytellers.json"),
            Victory = Parse<Systems.VictoryRules>("victory.json"),
            DarkQuests = Parse<Systems.DarkQuestRules>("darkquests.json"),
            Boons = Parse<Systems.BoonRules>("boons.json"),
            Endless = Parse<Systems.EndlessRules>("endless.json"),
            Character = Parse<CharacterRules>("character.json"),
            Tactics = Parse<TacticRules>("tactics.json"),
        };
    }

    /// <summary>The sending quests are stories like any other, of their own source.</summary>
    private static StoryRules WithSendings(StoryRules stories, SendingRules sendings)
    {
        foreach (var d in sendings.Stories)
            if (d.Source != Systems.Sendings.Source) throw new InvalidDataException($"Sending {d.Id} must have source \"{Systems.Sendings.Source}\"");
        stories.Stories.AddRange(sendings.Stories);
        return stories;
    }

    /// <summary>A file of content/game/ as built into the library, for readers outside the sim (the tutorial).</summary>
    public static string ReadText(string name) => ReadEmbedded(name);

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
