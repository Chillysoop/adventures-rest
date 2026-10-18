using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrownAndCompass.Sim;

/// <summary>Dice like "2d8+4": Count dice of Die sides plus Bonus.</summary>
public readonly record struct Dice(int Count, int Die, int Bonus)
{
    private static readonly Regex Pattern = new(@"^\s*(\d+)d(\d+)\s*(?:([+-])\s*(\d+))?", RegexOptions.CultureInvariant);

    public static Dice? Parse(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        var m = Pattern.Match(s);
        if (!m.Success) return null;
        int bonus = m.Groups[4].Success ? int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        if (m.Groups[3].Value == "-") bonus = -bonus;
        return new Dice(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), bonus);
    }

    public int Roll(Rng rng, bool crit = false) => rng.Roll(crit ? Count * 2 : Count, Die) + Bonus;

    public override string ToString() => Bonus == 0 ? $"{Count}d{Die}" : $"{Count}d{Die}{(Bonus > 0 ? "+" : "-")}{Math.Abs(Bonus)}";
}

/// <summary>A creature from the library, boiled down to what a delve needs.</summary>
public sealed record LibMonster
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string Type { get; init; } = "";
    public string Alignment { get; init; } = "";
    public double Cr { get; init; }
    public int Ac { get; init; }
    public int Hp { get; init; }

    /// <summary>The strongest attack's to-hit, damage and name, and how many it makes a round.</summary>
    public int ToHit { get; init; }
    public Dice? Damage { get; init; }
    public string Weapon { get; init; } = "";
    public int Attacks { get; init; } = 1;

    public IReadOnlyList<string> Habitats { get; init; } = [];
    public string Band { get; init; } = "";
    public bool Boss { get; init; }
}

public sealed record LibClass
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public int HitDie { get; init; }
    public IReadOnlyList<string> Primary { get; init; } = [];
    public string Role { get; init; } = "";

    /// <summary>full, half, pact, or empty for classes that cast nothing.</summary>
    public string Magic { get; init; } = "";
}

public sealed record LibSpell
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public int Level { get; init; }
    public IReadOnlyList<string> Classes { get; init; } = [];
    public Dice? Dice { get; init; }
    public string Targets { get; init; } = "";
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string? Fog { get; init; }
}

public sealed record LibItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string Category { get; init; } = "";
    public string Rarity { get; init; } = "";
    public string Slot { get; init; } = "";
    public int Tier { get; init; }
    public double ValueGp { get; init; }
    public string? Fog { get; init; }
    public string Text { get; init; } = "";
}

/// <summary>Mundane arms and armour from the equipment lists, boiled down to what a fight needs.</summary>
public sealed record LibGear
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }

    /// <summary>weapon, armor or shield.</summary>
    public string Slot { get; init; } = "";
    public double CostGp { get; init; }

    /// <summary>A weapon's damage dice.</summary>
    public Dice? Damage { get; init; }
    public IReadOnlyList<string> Properties { get; init; } = [];

    /// <summary>Armour class it gives (a shield's +2).</summary>
    public int Ac { get; init; }
    public bool AddDex { get; init; }

    /// <summary>Most Dexterity a medium armour lets through; null for none (light armour).</summary>
    public int? DexCap { get; init; }

    /// <summary>Strength the heavy armours ask for.</summary>
    public int Strength { get; init; }

    public bool TwoHanded => Properties.Any(p => p.StartsWith("Two-Handed", StringComparison.Ordinal));
}

/// <summary>A god of the pantheon (content/original/deities.json), boiled down to what the faith system needs.</summary>
public sealed record LibDeity
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string Alignment { get; init; } = "";
    public IReadOnlyList<string> Domains { get; init; } = [];
    public string Portfolio { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string HolyDay { get; init; } = "";

    /// <summary>Peoples of the wider world (content/game/world.json) who favour this god.</summary>
    public IReadOnlyList<string> FavouredBy { get; init; } = [];

    /// <summary>Race ids, professions and class ids whose folk lean to this god.</summary>
    public IReadOnlyList<string> Races { get; init; } = [];
    public IReadOnlyList<string> Professions { get; init; } = [];
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>Trait names the god favours and frowns on.</summary>
    public IReadOnlyList<string> Virtues { get; init; } = [];
    public IReadOnlyList<string> Vices { get; init; } = [];

    /// <summary>What a miracle of this god does: heal, forge, harvest, valor, justice, ward, lore, luck, trade, storm, ...</summary>
    public string Boon { get; init; } = "";
    public IReadOnlyList<string> Rivals { get; init; } = [];

    /// <summary>Site kinds (undead, schemers, tribe...) whose dens the god's temple pays to see cleansed.</summary>
    public IReadOnlyList<string> Hates { get; init; } = [];

    /// <summary>Evil gods are never worshipped openly; only cults convert to them.</summary>
    public bool Evil { get; init; }
}

/// <summary>A faction's lore (content/original/factions.json); how it behaves lives in content/game/factions.json.</summary>
public sealed record LibFaction
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Source { get; init; }
    public string Kind { get; init; } = "";
    public string Alignment { get; init; } = "";

    /// <summary>The deity id the faction holds as its patron.</summary>
    public string Patron { get; init; } = "";
    public string Motto { get; init; } = "";
    public string Symbol { get; init; } = "";
    public bool Secret { get; init; }
    public IReadOnlyList<string> Rivals { get; init; } = [];
    public IReadOnlyList<string> Allies { get; init; } = [];

    /// <summary>Peoples of the wider world (content/game/world.json) the faction has ties with.</summary>
    public IReadOnlyList<string> Peoples { get; init; } = [];
}

/// <summary>
/// The content library (content/srd, content/open/*, content/original): every monster, class, spell
/// and magic item the game can draw on. Ids are unique per kind across folders, so lookups are by id.
/// Lists are sorted by id so any pick from them through the town's Rng replays exactly.
/// </summary>
public sealed class Library
{
    public IReadOnlyList<LibMonster> Monsters { get; }
    public IReadOnlyList<LibClass> Classes { get; }
    public IReadOnlyList<LibSpell> Spells { get; }
    public IReadOnlyList<LibItem> Items { get; }
    public IReadOnlyList<LibDeity> Deities { get; }
    public IReadOnlyList<LibFaction> Factions { get; }

    /// <summary>Weapons, armour and shields a hero can buy at a forge or a store.</summary>
    public IReadOnlyList<LibGear> Gear { get; }

    private readonly Dictionary<string, LibMonster> _monsters;
    private readonly Dictionary<string, LibClass> _classes;
    private readonly Dictionary<string, LibSpell> _spells;
    private readonly Dictionary<string, LibItem> _items;
    private readonly Dictionary<string, LibDeity> _deities;
    private readonly Dictionary<string, LibFaction> _factions;
    private readonly Dictionary<string, LibGear> _gear;

    public Library(
        IEnumerable<LibMonster> monsters,
        IEnumerable<LibClass> classes,
        IEnumerable<LibSpell> spells,
        IEnumerable<LibItem> items,
        IEnumerable<LibDeity>? deities = null,
        IEnumerable<LibFaction>? factions = null,
        IEnumerable<LibGear>? gear = null)
    {
        Monsters = monsters.OrderBy(m => m.Id, StringComparer.Ordinal).ToList();
        Classes = classes.OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
        Spells = spells.OrderBy(s => s.Id, StringComparer.Ordinal).ToList();
        Items = items.OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        _monsters = Monsters.ToDictionary(m => m.Id);
        _classes = Classes.ToDictionary(c => c.Id);
        _spells = Spells.ToDictionary(s => s.Id);
        _items = Items.ToDictionary(i => i.Id);
        Deities = (deities ?? []).OrderBy(d => d.Id, StringComparer.Ordinal).ToList();
        Factions = (factions ?? []).OrderBy(f => f.Id, StringComparer.Ordinal).ToList();
        _deities = Deities.ToDictionary(d => d.Id);
        _factions = Factions.ToDictionary(f => f.Id);
        Gear = (gear ?? []).OrderBy(g => g.Id, StringComparer.Ordinal).ToList();
        _gear = Gear.ToDictionary(g => g.Id);
    }

    public LibMonster? Monster(string id) => _monsters.GetValueOrDefault(id);
    public LibClass? Class(string id) => _classes.GetValueOrDefault(id);
    public LibSpell? Spell(string id) => _spells.GetValueOrDefault(id);
    public LibItem? Item(string id) => _items.GetValueOrDefault(id);
    public LibDeity? Deity(string id) => _deities.GetValueOrDefault(id);
    public LibFaction? Faction(string id) => _factions.GetValueOrDefault(id);
    public LibGear? GearOf(string id) => _gear.GetValueOrDefault(id);

    /// <summary>Spells a class can cast, lowest level first, then by id.</summary>
    public IEnumerable<LibSpell> SpellsOf(string classId) =>
        Spells.Where(s => s.Classes.Contains(classId)).OrderBy(s => s.Level).ThenBy(s => s.Id, StringComparer.Ordinal);

    private static Library? _default;

    /// <summary>The bundled library, parsed once and shared.</summary>
    public static Library Default() => _default ??= Load(EmbeddedFiles());

    /// <summary>Load from the repo's content/ directory (everything but content/game).</summary>
    public static Library FromDirectory(string contentDir) =>
        Load(Directory.EnumerateFiles(contentDir, "*.json", SearchOption.AllDirectories)
            .Select(f => (Path.GetRelativePath(contentDir, f).Replace('\\', '/'), (Func<string>)(() => File.ReadAllText(f))))
            .Where(f => !f.Item1.StartsWith("game/", StringComparison.Ordinal)));

    private static IEnumerable<(string Path, Func<string> Read)> EmbeddedFiles()
    {
        var asm = typeof(Library).Assembly;
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.StartsWith("library/", StringComparison.Ordinal)) continue;
            yield return (name["library/".Length..].Replace('\\', '/'), () =>
            {
                using var stream = asm.GetManifestResourceStream(name)!;
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            });
        }
    }

    private static Library Load(IEnumerable<(string Path, Func<string> Read)> files)
    {
        var monsters = new Dictionary<string, LibMonster>();
        var classes = new Dictionary<string, LibClass>();
        var spells = new Dictionary<string, LibSpell>();
        var items = new Dictionary<string, LibItem>();
        var deities = new Dictionary<string, LibDeity>();
        var factions = new Dictionary<string, LibFaction>();
        var gear = new Dictionary<string, LibGear>();
        // Folder order only matters if an id repeats; the README promises it doesn't, and the first wins.
        foreach (var (path, read) in files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            string kind = System.IO.Path.GetFileNameWithoutExtension(path);
            if (kind is not ("monsters" or "classes" or "spells" or "magic-items" or "deities" or "factions" or "equipment")) continue;
            using var doc = JsonDocument.Parse(read());
            foreach (var e in doc.RootElement.EnumerateArray())
            {
                string id = Str(e, "id");
                switch (kind)
                {
                    case "monsters": monsters.TryAdd(id, ParseMonster(e)); break;
                    case "classes": classes.TryAdd(id, ParseClass(e)); break;
                    case "spells": spells.TryAdd(id, ParseSpell(e)); break;
                    case "magic-items": items.TryAdd(id, ParseItem(e)); break;
                    case "deities": deities.TryAdd(id, ParseDeity(e)); break;
                    case "factions": factions.TryAdd(id, ParseFaction(e)); break;
                    case "equipment":
                        if (ParseGear(e) is LibGear g) gear.TryAdd(id, g);
                        break;
                }
            }
        }
        return new Library(monsters.Values, classes.Values, spells.Values, items.Values, deities.Values, factions.Values, gear.Values);
    }

    private static string Str(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";

    private static double Num(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    private static bool Flag(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static List<string> Strings(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()!).ToList()
            : [];

    private static JsonElement Rt(JsonElement e) => e.TryGetProperty("rt", out var rt) ? rt : default;

    private static string? Fog(JsonElement e)
    {
        var rt = Rt(e);
        string s = rt.ValueKind == JsonValueKind.Object ? Str(rt, "fog") : "";
        if (s == "") s = Str(e, "fog");
        return s == "" ? null : s;
    }

    private static readonly Dictionary<string, int> CountWords = new()
    {
        ["two"] = 2,
        ["three"] = 3,
        ["four"] = 4,
        ["five"] = 5,
    };

    private static readonly Regex Multiattack = new(@"makes (two|three|four|five)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static LibMonster ParseMonster(JsonElement e)
    {
        int toHit = 0;
        Dice? damage = null;
        string weapon = "";
        double best = -1;
        int attacks = 1;
        if (e.TryGetProperty("actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in actions.EnumerateArray())
            {
                if (Str(a, "name").StartsWith("Multiattack", StringComparison.Ordinal))
                {
                    var m = Multiattack.Match(Str(a, "text"));
                    if (m.Success) attacks = CountWords[m.Groups[1].Value.ToLowerInvariant()];
                }
                if (!a.TryGetProperty("attacks", out var list) || list.ValueKind != JsonValueKind.Array) continue;
                foreach (var atk in list.EnumerateArray())
                {
                    double avg = Num(atk, "avg");
                    var dice = Dice.Parse(Str(atk, "dice"));
                    if (dice == null || avg <= best) continue;
                    best = avg;
                    damage = dice;
                    toHit = (int)Num(atk, "toHit");
                    weapon = Str(a, "name");
                }
            }
        }
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibMonster
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            Type = Str(e, "type"),
            Alignment = Str(e, "alignment"),
            Cr = Num(e, "cr"),
            Ac = (int)Num(e, "ac"),
            Hp = (int)Num(e, "hp"),
            ToHit = toHit,
            Damage = damage,
            Weapon = weapon,
            Attacks = attacks,
            Habitats = Strings(e, "habitats"),
            Band = hasRt ? Str(rt, "band") : "",
            Boss = hasRt && Flag(rt, "boss"),
        };
    }

    private static LibClass ParseClass(JsonElement e)
    {
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibClass
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            HitDie = (int)Num(e, "hitDie"),
            Primary = Strings(e, "primaryAbilities"),
            Role = hasRt ? Str(rt, "role") : "",
            Magic = hasRt ? Str(rt, "magic") : "",
        };
    }

    private static LibSpell ParseSpell(JsonElement e)
    {
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibSpell
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            Level = (int)Num(e, "level"),
            Classes = Strings(e, "classes"),
            Dice = Dice.Parse(Str(e, "damage")),
            Targets = hasRt ? Str(rt, "targets") : "",
            Roles = hasRt ? Strings(rt, "roles") : [],
            Fog = Fog(e),
        };
    }

    private static LibItem ParseItem(JsonElement e)
    {
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibItem
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            Category = Str(e, "category"),
            Rarity = Str(e, "rarity"),
            Slot = hasRt ? Str(rt, "slot") : "",
            Tier = hasRt ? (int)Num(rt, "tier") : 0,
            ValueGp = hasRt ? Num(rt, "valueGp") : 0,
            Fog = Fog(e),
            Text = Str(e, "text"),
        };
    }

    /// <summary>Only weapons with damage dice, armour and shields; the rest of the gear lists isn't fought with.</summary>
    private static LibGear? ParseGear(JsonElement e)
    {
        string id = Str(e, "id");
        if (e.TryGetProperty("weapon", out var w) && w.ValueKind == JsonValueKind.Object && Dice.Parse(Str(w, "damage")) is Dice dmg)
            return new LibGear { Id = id, Name = Str(e, "name"), Source = Str(e, "source"), Slot = "weapon", CostGp = Num(e, "costGp"), Damage = dmg, Properties = Strings(w, "properties") };
        if (e.TryGetProperty("armor", out var a) && a.ValueKind == JsonValueKind.Object)
        {
            bool shield = Str(Rt(e), "slot") == "shield";
            return new LibGear
            {
                Id = id,
                Name = Str(e, "name"),
                Source = Str(e, "source"),
                Slot = shield ? "shield" : "armor",
                CostGp = Num(e, "costGp"),
                Ac = (int)Num(a, "ac"),
                AddDex = Flag(a, "addDex"),
                DexCap = a.TryGetProperty("dexCap", out var cap) && cap.ValueKind == JsonValueKind.Number ? cap.GetInt32() : null,
                Strength = (int)Num(a, "strength"),
            };
        }
        return null;
    }

    private static LibDeity ParseDeity(JsonElement e)
    {
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibDeity
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            Alignment = Str(e, "alignment"),
            Domains = Strings(e, "domains"),
            Portfolio = Str(e, "portfolio"),
            Symbol = Str(e, "symbol"),
            HolyDay = Str(e, "holyDay"),
            FavouredBy = Strings(e, "favouredBy"),
            Races = hasRt ? Strings(rt, "races") : [],
            Professions = hasRt ? Strings(rt, "professions") : [],
            Classes = hasRt ? Strings(rt, "classes") : [],
            Virtues = hasRt ? Strings(rt, "virtues") : [],
            Vices = hasRt ? Strings(rt, "vices") : [],
            Boon = hasRt ? Str(rt, "boon") : "",
            Rivals = hasRt ? Strings(rt, "rivals") : [],
            Hates = hasRt ? Strings(rt, "hates") : [],
            Evil = hasRt && Flag(rt, "evil"),
        };
    }

    private static LibFaction ParseFaction(JsonElement e)
    {
        var rt = Rt(e);
        bool hasRt = rt.ValueKind == JsonValueKind.Object;
        return new LibFaction
        {
            Id = Str(e, "id"),
            Name = Str(e, "name"),
            Source = Str(e, "source"),
            Kind = Str(e, "kind"),
            Alignment = Str(e, "alignment"),
            Patron = Str(e, "patron"),
            Motto = Str(e, "motto"),
            Symbol = Str(e, "symbol"),
            Secret = hasRt && Flag(rt, "secret"),
            Rivals = hasRt ? Strings(rt, "rivals") : [],
            Allies = hasRt ? Strings(rt, "allies") : [],
            Peoples = hasRt ? Strings(rt, "peoples") : [],
        };
    }

    /// <summary>Experience for defeating a creature of this challenge rating.</summary>
    public static int XpForCr(double cr) => cr switch
    {
        <= 0 => 10,
        <= 0.125 => 25,
        <= 0.25 => 50,
        <= 0.5 => 100,
        <= 1 => 200,
        <= 2 => 450,
        <= 3 => 700,
        <= 4 => 1100,
        <= 5 => 1800,
        <= 6 => 2300,
        <= 7 => 2900,
        <= 8 => 3900,
        <= 9 => 5000,
        <= 10 => 5900,
        _ => 7200 + (int)(cr - 11) * 1200,
    };

    /// <summary>"a Far-Seeing Basin", "an Eye of the Sleepless Spire".</summary>
    public static string A(string name) => ("aeiouAEIOU".Contains(name[0]) ? "an " : "a ") + name;

    /// <summary>"Goblin Warrior" reads as "goblin warrior" in prose; proper nouns keep their capitals.</summary>
    public static string Prose(string name) => name.ToLowerInvariant();

    /// <summary>English plural of a creature name: "swarm of bats" to "swarms of bats", "wolf" to "wolves".</summary>
    public static string Plural(string name)
    {
        int of = name.IndexOf(" of ", StringComparison.Ordinal);
        if (of > 0) return Plural(name[..of]) + name[of..];
        int sp = name.LastIndexOf(' ');
        string head = sp >= 0 ? name[..(sp + 1)] : "";
        string w = sp >= 0 ? name[(sp + 1)..] : name;
        string p = w switch
        {
            _ when w.EndsWith("man", StringComparison.Ordinal) => w[..^3] + "men",
            "mouse" => "mice",
            "ox" => "oxen",
            _ when w.EndsWith("folk", StringComparison.Ordinal) => w,
            "fish" or "sheep" or "deer" or "elk" or "kuo-toa" or "drow" => w,
            _ when w.EndsWith("lf", StringComparison.Ordinal) || w.EndsWith("rf", StringComparison.Ordinal) => w[..^1] + "ves",
            _ when w.EndsWith("y", StringComparison.Ordinal) && w.Length > 1 && !"aeiou".Contains(w[^2]) => w[..^1] + "ies",
            _ when w.EndsWith("s", StringComparison.Ordinal) || w.EndsWith("x", StringComparison.Ordinal) || w.EndsWith("ch", StringComparison.Ordinal) || w.EndsWith("sh", StringComparison.Ordinal) => w + "es",
            _ => w + "s",
        };
        return head + p;
    }
}
