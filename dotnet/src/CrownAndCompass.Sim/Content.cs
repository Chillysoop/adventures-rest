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

public sealed record ClassDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Primary { get; init; }
    public int HitDie { get; init; }
    public int Armor { get; init; }
    public int DamageDie { get; init; }
    public IReadOnlyDictionary<string, int> Skills { get; init; } = new Dictionary<string, int>();
    public int Weight { get; init; }
    public bool Sneak { get; init; }
    public bool Area { get; init; }
    public bool Healer { get; init; }
}

public sealed record MonsterDef
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Plural { get; init; }
    public int Hp { get; init; }
    public int Ac { get; init; }
    public int Attack { get; init; }
    public int DamageDie { get; init; }
    public int DamageBonus { get; init; }
    public int Xp { get; init; }
    public bool Boss { get; init; }
    public bool Undead { get; init; }

    /// <summary>A named individual: no article before the name.</summary>
    public bool Named { get; init; }
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

    public Content(
        IReadOnlyList<BuildingDef> buildings,
        IReadOnlyList<RaceDef> races,
        IReadOnlyList<ClassDef> classes,
        IReadOnlyList<MonsterDef> monsters,
        IReadOnlyList<SiteDef> sites,
        TownRules rules,
        CouncilRules council,
        EnemyDef enemy)
    {
        Buildings = buildings;
        _buildings = buildings.ToDictionary(b => b.Id);
        Races = races;
        Classes = classes;
        Monsters = monsters;
        _monsters = monsters.ToDictionary(m => m.Id);
        Sites = sites;
        Rules = rules;
        Council = council;
        Enemy = enemy;
    }

    public MonsterDef Monster(string id) =>
        _monsters.TryGetValue(id, out var m) ? m : throw new KeyNotFoundException($"Unknown monster {id}");

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
            Parse<EnemyDef>("enemy.json"));
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
