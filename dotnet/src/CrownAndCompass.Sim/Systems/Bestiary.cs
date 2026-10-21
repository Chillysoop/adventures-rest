namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// How one creature is built, moves and strikes, read off its own stat block (<see cref="Bestiary.Of"/>): the map
/// makes its model from this and plays its gait and attack, and the fights read its gait and roam. Every creature in
/// the library comes out different (BestiaryTests); kin share a plan, a rig and a gait but not a look.
/// </summary>
public sealed record Anatomy
{
    public string Id { get; init; } = "";

    /// <summary>The body plan: humanoid, golem, elemental, quadruped, ape, theropod, bird, dragon, serpent, worm, insect,
    /// spider, crab, ooze, wraith, floater, fish, cephalopod, plant, taur, swarm or object.</summary>
    public string Plan { get; init; } = "";

    /// <summary>The head the plan wears (wolf, bull, beak, skull, flame, chest...); "" for the plan's own.</summary>
    public string Head { get; init; } = "";

    /// <summary>How it moves: walk, lumber, trot, prowl, skitter, slither, fly, hover, ooze, swim, root, hop, swarm.</summary>
    public string Gait { get; init; } = "";

    /// <summary>Its attack: weapon, shoot, cast, bite, claw, slam, sting, gore, lash, spit, engulf, talons, touch, trample.</summary>
    public string Strike { get; init; } = "";

    /// <summary>How it roams its country: pack, herd, lone, circle, lurk, burrow, drift, school, march, haunt, rooted, swarm.</summary>
    public string Roam { get; init; } = "";

    /// <summary>Height against a man (1), from its size, and its build: bulk, leg length, neck and tail.</summary>
    public double Scale { get; init; } = 1;
    public double Bulk { get; init; } = 1;
    public double Leg { get; init; } = 1;
    public double Neck { get; init; }
    public double Tail { get; init; }

    public int Heads { get; init; } = 1;
    public int Legs { get; init; } = 2;
    public int Arms { get; init; } = 2;
    public int Eyes { get; init; } = 2;

    /// <summary>feather, bat, insect, flame, cloud, or "".</summary>
    public string Wings { get; init; } = "";

    /// <summary>Features: horns, antlers, tusks, mane, spikes, shell, fins, crest, beard, crown, hooves, tentacles, pincers, stinger, club...</summary>
    public IReadOnlyList<string> Marks { get; init; } = [];

    /// <summary>What it is made of: flesh, fur, scale, feather, chitin, bone, stone, metal, wood, fungus, slime, fire, water, air, shadow, ghost, crystal, ice.</summary>
    public string Material { get; init; } = "";

    /// <summary>Colours as hex: its hide, its underside, and an accent (eyes, horns, flames).</summary>
    public string Hide { get; init; } = "";
    public string Belly { get; init; } = "";
    public string Accent { get; init; } = "";

    /// <summary>People and golems are built from the heroes' kit: its body, head, weapon and off hand (HeroKit names).</summary>
    public string Frame { get; init; } = "";
    public string Helm { get; init; } = "";
    public string Weapon { get; init; } = "";
    public string Off { get; init; } = "";

    public bool Flies => Gait is "fly" or "hover";

    /// <summary>Everything that shapes its model but its colours, as one string: no two creatures share one, so none is a recolour of another.</summary>
    public string Shape => string.Join("|", Plan, Head, Gait, Strike, Js.ToFixed(Scale, 2), Js.ToFixed(Bulk, 2), Js.ToFixed(Leg, 2), Js.ToFixed(Neck, 2),
        Js.ToFixed(Tail, 2), Heads, Legs, Arms, Eyes, Wings, string.Join(",", Marks), Material, Frame, Helm, Weapon, Off);

    /// <summary>Its shape and colours.</summary>
    public string Signature => string.Join("|", Shape, Hide, Belly, Accent);
}

/// <summary>Hand-set parts of a creature's anatomy (content/game/bestiary.json "creatures"); anything left out is read off its stat block.</summary>
public sealed record AnatomyOverride
{
    public string? Plan { get; init; }
    public string? Head { get; init; }
    public string? Gait { get; init; }
    public string? Strike { get; init; }
    public string? Roam { get; init; }
    public string? Wings { get; init; }
    public string? Material { get; init; }
    public string? Hide { get; init; }
    public string? Belly { get; init; }
    public string? Accent { get; init; }
    public string? Frame { get; init; }
    public string? Helm { get; init; }
    public string? Weapon { get; init; }
    public string? Off { get; init; }
    public int? Heads { get; init; }
    public int? Legs { get; init; }
    public int? Arms { get; init; }
    public int? Eyes { get; init; }
    public double? Neck { get; init; }
    public double? Tail { get; init; }
    public double? Bulk { get; init; }
    public double? Leg { get; init; }
    public double? Scale { get; init; }

    /// <summary>Marks added to (or, with a leading "-", taken from) those read off the stat block.</summary>
    public IReadOnlyList<string> Marks { get; init; } = [];
}

public sealed record BestiaryRules
{
    /// <summary>Height against a man for each size.</summary>
    public Dictionary<string, double> Sizes { get; init; } = new();

    /// <summary>Colour words in a creature's id and the hide they give (hex).</summary>
    public Dictionary<string, string> Colours { get; init; } = new();

    /// <summary>Hides each creature type draws from when its name gives no colour, nudged per creature.</summary>
    public Dictionary<string, string[]> TypeHides { get; init; } = new();

    public Dictionary<string, AnatomyOverride> Creatures { get; init; } = new();
}

/// <summary>
/// Reads every creature's anatomy off its stat block: its type, size, speeds, the words of its name, its traits and
/// the names of its attacks. Pure and cached: the same creature always comes out the same.
/// </summary>
public static class Bestiary
{
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Content, Dictionary<string, Anatomy>> Cache = new();

    /// <summary>The anatomy of a creature by its library or game id ("goblin", "wolf@boss" and "goblin-warrior" all work).</summary>
    public static Anatomy Of(Content content, string id)
    {
        var cache = Cache.GetOrCreateValue(content);
        lock (cache)
        {
            if (cache.TryGetValue(id, out var had)) return had;
            string key = id.EndsWith("@boss", StringComparison.Ordinal) ? id[..^5] : id;
            var lm = content.Library.Monster(key);
            if (lm == null && TryGame(content, key) is { } gm) lm = content.Library.Monster(gm.Ref);
            var a = lm != null ? Read(content.Bestiary, lm) : new Anatomy { Id = key, Plan = "humanoid", Gait = "walk", Strike = "weapon", Roam = "march", Frame = "Brute", Helm = "Tusks", Weapon = "Axe", Hide = "6f8a4a" };
            cache[id] = a;
            return a;
        }
    }

    private static MonsterDef? TryGame(Content c, string id)
    {
        try { return c.Monster(id); }
        catch (KeyNotFoundException) { return null; }
    }

    /// <summary>Every creature in the library that can be fought (it has a band), in id order.</summary>
    public static IEnumerable<LibMonster> All(Content content) => content.Library.Monsters.Where(m => m.Band != "");

    /// <summary>
    /// The creatures that hold a site, its master first: the boss, its escort, then whoever its fights name. Reads only:
    /// the map draws a site's fights and raids with them.
    /// </summary>
    public static List<string> SiteFoes(SiteDef site)
    {
        var list = new List<string>();
        void Add(string? id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (id.EndsWith("@boss", StringComparison.Ordinal)) id = id[..^5];
            if (!list.Contains(id)) list.Add(id);
        }
        Add(site.Boss.Monster);
        Add(site.Boss.Escort);
        foreach (var e in site.Encounters)
            if (e.Kind == "combat")
            {
                Add(e.Monster);
                Add(e.Escort);
            }
        return list;
    }

    /// <summary>
    /// Who marches in a host: the warband's outpost, or a menace's seat and holdings. Its leader's creature first.
    /// "" ids are left out; an empty list means the map's old orc figures.
    /// </summary>
    public static List<string> Host(Town town, string foe)
    {
        var list = new List<string>();
        if (foe == "warband") list.AddRange(SiteFoes(town.Content.Enemy.Outpost));
        else if (foe.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(foe[7..], out int mid) && town.Saga.Menaces.FirstOrDefault(x => x.Id == mid) is { } m)
            foreach (var sid in new[] { m.SeatId }.Concat(m.Holdings))
                if (Quests.Site(town, sid) is { } s)
                    foreach (var id in SiteFoes(s.Def))
                        if (!list.Contains(id)) list.Add(id);
        return list;
    }

    private static readonly char[] Split = ['-', '_', ' ', '\'', '(', ')', ','];

    private static HashSet<string> Words(string s) => s.ToLowerInvariant().Split(Split, StringSplitOptions.RemoveEmptyEntries).ToHashSet();

    public static Anatomy Read(BestiaryRules rules, LibMonster m)
    {
        var id = Words(m.Id);
        var strikes = m.Strikes.SelectMany(Words).ToHashSet();
        var traits = string.Join(" ", m.Traits).ToLowerInvariant();
        bool Id(params string[] w) => w.Any(id.Contains);
        bool Hit(params string[] w) => w.Any(strikes.Contains);
        bool Trait(params string[] w) => w.Any(traits.Contains);
        string type = m.Type;
        bool flies = m.Fly > 0, swims = m.Swim > 0, burrows = m.Burrow > 0;
        bool swimOnly = swims && m.Swim >= 30 && !Id("frog", "toad", "crocodile", "hippopotamus", "bear", "snake", "lizardfolk", "frogoblin", "hag", "naga") && type is "beast" or "monstrosity" or "elemental" or "aberration" && !flies && m.Burrow == 0;

        // The plan ---------------------------------------------------------------------------------------------------
        string plan, head = "";
        int legs = 4, arms = 0, heads = 1, eyes = 2;
        string wings = "";
        var marks = new List<string>();

        if (Id("swarm", "flock", "brood") || m.Id == "plaguefly-cloud")
        {
            plan = "swarm";
            head = Id("rat", "rats") ? "rat" : Id("bat", "bats") ? "bat" : Id("raven", "ravens", "rotfeather") ? "bird" : Id("snake", "snakes", "snakes") ? "snake"
                : Id("piranha", "piranhas", "quipper", "quippers") ? "fish" : Id("claws", "crawling") ? "hand" : "insect";
        }
        else if (Id("mimic", "door", "rug", "catapult", "cart") || m.Id is "animated-flying-sword" or "candle-skull" or "gilded-skull")
        {
            plan = "object";
            head = Id("mimic") ? "chest" : Id("door") ? "door" : Id("rug") ? "rug" : Id("catapult") ? "catapult" : Id("cart") ? "cart" : Id("sword") ? "sword" : "skull";
        }
        else if (Id("centaur", "drider", "lamia", "rider", "saltstrider", "nuckelavee", "dullahan", "huntsman", "salamander", "marilith", "merfolk", "merrow", "sila", "djinni", "efreeti", "hinn"))
        {
            plan = "taur";
            head = Id("drider") ? "spider" : Id("lamia") ? "lion" : Id("rider") ? "boar" : Id("saltstrider") ? "scorpion"
                : Id("salamander", "marilith") ? "serpent" : Id("merfolk", "merrow") ? "fish" : Id("sila", "djinni", "efreeti", "hinn") ? "wisp" : "horse";
            arms = Id("marilith") ? 6 : 2;
            legs = head switch { "spider" => 8, "scorpion" => 6, "serpent" or "fish" or "wisp" => 0, _ => 4 };
        }
        else if (type == "dragon" && Id("kobold", "half"))
        {
            plan = "humanoid";
            head = "lizard";
        }
        else if (Id("needle"))
        {
            plan = "insect";
            head = "dragonfly";
            legs = 6;
        }
        else if (type == "dragon" && Id("turtle"))
        {
            plan = "quadruped";
            head = "dragon";
            marks.Add("shell");
        }
        else if (type == "dragon" && (flies || Id("drake", "drakeling", "wyvern")))
        {
            plan = "dragon";
            head = Id("wyvern") ? "wyvern" : Id("pseudodragon") ? "pseudo" : "dragon";
            legs = Id("wyvern", "zmey") ? 2 : 4;
            heads = Id("zmey") ? 3 : 1;
        }
        else if (type == "ooze" || Id("jelly", "pudding", "ooze", "cube", "blot", "mouther", "slime", "lemure"))
        {
            plan = Id("crimson") ? "floater" : "ooze";
            head = Id("cube") ? "cube" : Id("mouther") ? "mouths" : Id("blot") ? "blot" : Id("crimson") ? "jellyfish" : Id("pudding") ? "pudding" : Id("lemure") ? "lemure" : "";
            legs = 0;
        }
        else if (Id("spider", "jorogumo"))
        {
            plan = "spider";
            legs = 8;
            head = Id("phase") ? "phase" : Id("wolf") ? "hunter" : Id("mistweaver") ? "weaver" : Id("dancing") ? "dancer" : Id("jorogumo") ? "woman" : "";
        }
        else if (Id("crab", "chuul", "wardsalt"))
        {
            plan = "crab";
            legs = 6;
            head = Id("chuul") ? "tentacles" : "";
            marks.Add("pincers");
        }
        else if (Id("beetle", "wasp", "centipede", "scorpion", "fly", "ankheg", "chitterkin", "crawler", "hivebound", "moth", "remorhaz", "rust", "mordovermis", "adze", "crystalback", "seeker", "stirge"))
        {
            plan = "insect";
            legs = Id("centipede", "remorhaz") ? 12 : 6;
            head = Id("wasp", "fly", "hivebound", "mordovermis", "adze") ? "wasp" : Id("moth") ? "moth" : Id("scorpion") ? "scorpion" : Id("centipede", "remorhaz") ? "centipede"
                : Id("ankheg", "chitterkin") ? "ankheg" : Id("rust") ? "rust" : Id("stirge") ? "stirge" : Id("seeker") ? "engine" : "beetle";
            if (Id("scorpion")) marks.AddRange(["pincers", "stinger"]);
            if (head is "wasp" or "moth" or "stirge" || flies) wings = "insect";
        }
        else if (Id("worm", "gnawer", "grub", "urugotha", "leech", "starving", "grick"))
        {
            plan = "worm";
            legs = 0;
            head = Id("grick") ? "grick" : Id("leech") ? "leech" : Id("starving", "gnawer") ? "maw" : Id("grub") ? "grub" : "";
        }
        else if (Id("octopus", "kraken", "aboleth", "roper", "otyugh", "fogmother", "envoy", "umibozu", "grindylow"))
        {
            plan = "cephalopod";
            legs = 0;
            head = Id("aboleth") ? "aboleth" : Id("roper") ? "roper" : Id("otyugh") ? "otyugh" : Id("envoy") ? "envoy" : Id("umibozu") ? "monk" : Id("grindylow") ? "imp" : Id("fogmother") ? "mist" : "";
            marks.Add("tentacles");
        }
        else if (Id("snake", "serpent", "naga", "couatl", "behir", "lindworm", "knucker", "wurm", "tatzelwurm", "tsuchinoko", "blightwyrm", "bloodwyrm", "wadjwer", "leviathan", "constrictor")
            || type == "dragon")
        {
            plan = "serpent";
            legs = Id("behir") ? 12 : Id("lindworm", "tatzelwurm") ? 2 : type == "dragon" && !Id("wurm") ? 4 : 0;
            head = Id("naga") ? "naga" : Id("couatl") ? "couatl" : type == "dragon" || Id("behir", "lindworm", "tatzelwurm", "leviathan") ? "dragon" : Id("ossuary") ? "skull" : "snake";
            if (Id("couatl")) wings = "feather";
        }
        else if (Id("ghost", "specter", "wraith", "shade", "shadow", "banshee", "gloomfeeder", "choir", "saint", "herald", "hollow", "eclipse", "phantom", "bedshade", "shepherd", "husk", "ringer", "witherglance")
            && type is "undead" or "fey" or "fiend" or "aberration" && !(type == "undead" && Id("knight", "marshal", "castellan", "sentinel")) || type == "undead" && m.Hover)
        {
            plan = Id("eclipse") ? "floater" : "wraith";
            head = Id("eclipse") ? "eclipse" : Id("hollow") ? "crown" : Id("banner") ? "banner" : Id("ringer") ? "bell" : Id("herald", "pestilence") ? "scythe" : Id("shepherd") ? "crook" : Id("shadow", "bedshade") ? "shadow"
                : Id("choir", "queen") ? "veil" : Id("saint") ? "halo" : Id("husk") ? "husk" : Id("ash") ? "ash" : "";
            legs = 0;
        }
        else if (Id("wisp", "star", "fractal", "rune", "malspell", "eye", "darkmantle", "cloaker", "mirror", "drone", "dihedrone", "octahedrone", "icosahedrone", "tetrahedrone", "cudrone", "hemicudrone", "trapezohedrone", "dodecatron", "stalker", "melodi")
            && !Id("crow"))
        {
            plan = "floater";
            legs = 0;
            head = Id("wisp") ? "wisp" : Id("star") ? "star" : Id("fractal") ? "fractal" : Id("rune") ? "rune" : Id("darkmantle", "cloaker") ? "manta" : Id("mirror") ? "mirror"
                : Id("tetrahedrone") ? "tetra" : Id("octahedrone") ? "octa" : Id("icosahedrone") ? "icosa" : Id("dodecatron") ? "dodeca" : Id("dihedrone") ? "di" : Id("hemicudrone", "cudrone") ? "cube"
                : Id("trapezohedrone") ? "trapezo" : Id("drone") ? "drone" : Id("stalker") ? "whirl" : Id("melodi") ? "notes" : "eye";
        }
        else if (swimOnly && !Id("hydra", "hippocampus", "bunyip", "mokele", "wodyanoi", "ahuizotl", "kappa", "brine", "kelpie"))
        {
            plan = "fish";
            legs = 0;
            head = Id("shark") ? "shark" : Id("orca", "whale") ? "whale" : Id("seahorse") ? "seahorse" : Id("piranha", "quipper") ? "piranha" : Id("plesiosaurus") ? "plesio" : Id("archelon") ? "turtle"
                : Id("hagfish") ? "eel" : Id("snow") ? "drifter" : Id("caeloni") ? "angler" : "fish";
        }
        else if (type == "plant" || Id("tree", "treant", "ironwood", "shrub", "leshy", "mound", "fungus", "bolete", "sporeling", "bloom", "blossom", "rose", "dandelion", "cherry", "thornling", "emberroot", "myconid", "mycolid"))
        {
            plan = Id("mycolid", "myconid", "herald") ? "humanoid" : "plant";
            head = Id("tree", "treant", "ironwood", "leshy") ? "tree" : Id("shrub") ? "shrub" : Id("mound") ? "mound" : Id("fungus", "bolete", "sporeling", "shrieker", "mycolid") ? "mushroom"
                : Id("emberroot") ? "roots" : "flower";
            legs = head == "tree" ? 2 : 0;
        }
        else if (Id("eagle", "hawk", "owl", "raven", "crow", "vulture", "roc", "pteranodon", "bat", "ahool", "blazewing", "malocophony", "rotfeather", "harpy") && !Id("owlbear") || type == "beast" && flies && !Id("pegasus"))
        {
            plan = Id("harpy") ? "humanoid" : "bird";
            legs = 2;
            head = Id("owl") ? "owl" : Id("vulture") ? "vulture" : Id("raven", "crow") ? "crow" : Id("bat", "ahool") ? "bat" : Id("pteranodon") ? "pteranodon" : Id("blazewing") ? "phoenix" : Id("roc") ? "roc" : "hawk";
            wings = head is "bat" ? "bat" : head == "pteranodon" ? "bat" : Id("blazewing", "malocophony") ? "flame" : "feather";
        }
        else if (Id("tyrannosaurus", "allosaurus", "velociraptor", "axe", "cockatrice", "sau", "tarrasque"))
        {
            plan = "theropod";
            legs = 2;
            head = Id("axe", "cockatrice", "sau") ? "beak" : Id("tarrasque") ? "tarrasque" : Id("velociraptor") ? "raptor" : "rex";
            if (Id("tarrasque")) marks.AddRange(["horns", "spikes", "shell"]);
            if (Id("cockatrice")) wings = "bat";
        }
        else if (Id("ape", "baboon", "yeti", "sasabonsam", "longreach", "likho", "aswang", "fachan") && type is not "humanoid")
        {
            plan = "ape";
            legs = 2;
            arms = 2;
            head = Id("yeti") ? "yeti" : Id("baboon") ? "baboon" : Id("sasabonsam") ? "bat" : "ape";
            if (Id("sasabonsam") || flies) wings = "bat";
        }
        else if (type == "construct" && !Id("gorgon", "balehound", "snail") || type == "elemental" && !Id("mephit", "gargoyle", "azer", "newt", "triune", "aurha", "yukanen", "xorn", "magmin") || Id("golem"))
        {
            plan = type == "construct" || Id("golem") ? "golem" : "elemental";
            head = plan == "golem" ? "" : Id("fire", "ember", "sovereign") ? "flame" : Id("water", "guardian") ? "wave" : Id("air", "storm") ? "whirl" : Id("earth") ? "rock" : "rock";
            legs = head is "flame" or "wave" or "whirl" ? 0 : 2;
            arms = 2;
        }
        else if (type is "humanoid" or "giant" || Id("goblin", "hobgoblin", "bugbear", "kobold", "gnoll", "sahuagin", "lizardfolk", "frogoblin", "orc", "ogre", "troll", "ettin", "hag", "satyr", "dryad", "sprite", "redcap", "boggart",
            "domovoi", "bannik", "lampblack", "kikimora", "poludnitsa", "vodyanoy", "kappa", "devil", "imp", "quasit", "dretch", "erinyes", "succubus", "incubus", "rakshasa", "glabrezu", "hezrou", "vrock", "nalfeshnee",
            "balor", "oni", "deva", "planetar", "solar", "vampire", "ghoul", "ghast", "zombie", "skeleton", "mummy", "wight", "lich", "revenant", "knight", "were", "minotaur", "medusa", "doppelganger",
            "mephit", "gargoyle", "azer", "magmin", "gnome", "duergar", "drow", "grimlock", "ettercap", "flickerling", "reveller", "knocker", "sackman", "prophet", "druid", "ringmaster", "matron", "herald",
            "fiend", "ape", "lodge", "lord", "chancellor", "broker", "myling", "nachzehrer", "wormhost", "levy", "marshal", "yukanen", "tikbalang", "unska", "golmana", "ahauit", "revok", "rainsnail", "rollsnail",
            "satarre", "werebear", "wereboar", "wererat", "weretiger", "werewolf", "hushed", "ghoul", "wyrdling", "godling", "heqai", "whim", "nurturer", "gleaner", "people", "bones", "guiding", "old")
            || m.Id.EndsWith("hag", StringComparison.Ordinal) || type is "celestial" && !Id("sphinx", "unicorn", "pegasus", "eagle", "owl", "elk", "couatl", "naga") || type == "fiend" && m.Size == "medium" && !Id("hound", "shuck", "cadejo", "nightmare"))
        {
            plan = "humanoid";
        }
        else
        {
            plan = "quadruped";
            head = Id("wolf", "worg", "hound", "dog", "mastiff", "jackal", "shuck", "cadejo", "hyena", "trisakh", "silkhound", "fog") ? "wolf"
                : Id("bear", "owlbear") ? (Id("owlbear") ? "owlbear" : "bear")
                : Id("lion", "tiger", "panther", "cat", "lynx", "leucrota", "manticore", "sphinx", "chimera", "tatzelwurm") ? "cat"
                : Id("horse", "pony", "mule", "donkey", "pegasus", "unicorn", "nightmare", "kelpie", "hippocampus", "warhorse", "tikbalang") ? "horse"
                : Id("boar", "narhistrix") ? "boar"
                : Id("deer", "elk", "goat", "ram", "sheep", "flinthorn", "karkadann", "camel", "wolpertinger", "hare", "bonnacon") ? "deer"
                : Id("bison", "gorgon", "rhinoceros", "triceratops", "ankylosaurus", "mammoth", "elephant", "hippopotamus", "mokele", "stormhorn", "tortoise", "archelon", "bunyip", "wodyanoi") ? "ox"
                : Id("rat", "weasel", "badger", "mole", "dazzlemole", "neep", "ahuizotl", "kamaitachi", "the") ? "rat"
                : Id("lizard", "crocodile", "basilisk", "newt", "salamandrach", "bulette", "frog", "toad", "kappa", "snail", "ankylosaurus", "gorak", "aurha", "xorn", "magmin") ? "lizard"
                : Id("griffon", "hippogriff") ? "griffon"
                : Id("hydra") ? "hydra"
                : "beast";
            if (Id("frog", "toad")) head = "frog";
            if (Id("snail")) head = "snail";
            if (Id("crocodile")) head = "croc";
            if (Id("griffon", "hippogriff", "pegasus", "sphinx", "chimera", "wolpertinger", "nightmare") || (flies && !Id("manticore", "nightmare", "aurha", "yukanen") && type != "fiend"))
                wings = Id("chimera") ? "bat" : "feather";
            if (Id("manticore")) wings = "bat";
        }

        if (plan is "humanoid" or "golem" or "elemental" && legs == 4) legs = 2;
        if (plan == "humanoid" && flies && !m.Hover && wings == "")
            wings = type == "celestial" || Id("erinyes", "vrock", "harpy", "deva", "planetar", "solar") ? "feather" : Id("sprite", "pixie", "flickerling") ? "insect" : "bat";

        // Heads for people: by their kind, then their trade ---------------------------------------------------------
        if (plan is "humanoid" or "ape" && head == "")
            head = Id("minotaur", "gorgon") ? "bull" : Id("werewolf", "gnoll", "cadejo") ? "wolf" : Id("wereboar") ? "boar" : Id("wererat", "rat") ? "rat"
                : Id("weretiger", "rakshasa") ? "tiger" : Id("werebear") ? "bear" : Id("frogoblin", "hezrou", "vodyanoy") ? "frog" : Id("vrock") ? "vulture" : Id("glabrezu") ? "dog" : Id("nalfeshnee") ? "boar"
                : Id("sahuagin", "glimmerkin", "brine", "kappa") ? "fish" : Id("coilborn", "medusa") ? "snake" : Id("lizardfolk", "kobold", "muskscale", "half") ? "lizard" : Id("satyr", "golmana", "tikbalang") ? "goat"
                : Id("skeleton", "lich", "bones", "grey", "levy", "rattlebone", "marshal", "virtuoso", "death") && type == "undead" ? "skull" : Id("orc", "bloodtusk", "ogre", "troll", "ettin", "hill", "fachan") ? "tusks"
                : Id("ettercap") ? "spider" : Id("grimlock", "hushed") ? "blind" : Id("myling", "little") ? "child" : Id("mycolid", "myconid") ? "cap" : "";

        // Size and build ---------------------------------------------------------------------------------------------
        string size = m.Size;
        // The SRD prints "Medium or Small" people as small: only the small folk are.
        if (type == "humanoid" && size == "small" && !Id("goblin", "kobold", "gnome", "halfling", "deep", "gloamdelver", "mycolid", "myling", "little", "redcap", "boggart", "sprite", "bannik", "kikimora", "domovoi", "lampblack", "knocker", "flickerling"))
            size = "medium";
        if (Id("goblin", "kobold") && size == "medium") size = "small";
        double scale = rules.Sizes.GetValueOrDefault(size, 1);
        double bulk = Math.Clamp(0.75 + (m.Str - 10) * 0.03 - (m.Dex - 12) * 0.012, 0.6, 1.5);
        double leg = Math.Clamp(0.85 + (m.Dex - 10) * 0.025, 0.6, 1.4);
        double neck = plan switch { "dragon" => 0.8, "serpent" => 0, "bird" => 0.4, "quadruped" => head is "horse" or "deer" ? 0.6 : head == "ox" && Id("mokele") ? 1.8 : 0.3, "theropod" => 0.5, _ => 0 };
        if (Id("plesiosaurus", "mokele", "bronze", "brass") && plan != "humanoid") neck += 0.5;
        double tail = plan switch { "dragon" => 1.2, "quadruped" => head is "cat" or "wolf" or "lizard" or "croc" ? 0.8 : head == "rat" ? 1.2 : 0.4, "theropod" => 1.1, "serpent" => 1, "bird" => 0.3, _ => 0 };
        if (Id("ankylosaurus")) marks.Add("club");
        if (head == "hydra") neck = 1.2;
        // Each dragon kind its own horns and frills, and the old ones grander.
        if (plan == "dragon" && head == "dragon" || type == "dragon" && plan == "serpent")
        {
            foreach (var (word, mk) in DragonMarks)
                if (Id(word)) marks.AddRange(mk);
            if (Id("ancient")) marks.Add("spikes");
            if (Id("wyrmling")) bulk *= 0.85;
        }

        // Heads, eyes and arms that the name and the attacks give ---------------------------------------------------
        if (Id("ettin", "death", "dog") && Id("ettin", "dog") && plan != "humanoid" || Id("ettin")) heads = 2;
        if (Id("chimera", "trisakh", "triune")) heads = 3;
        if (Id("hydra")) heads = 5;
        if (Id("zmey")) heads = 3;
        if (Id("cyclops", "fachan", "eyeless") || Id("one")) eyes = Id("eyeless") ? 0 : 1;
        if (Id("blind", "grimlock") || Trait("blind senses", "eyeless")) eyes = 0;
        if (Id("spider") || plan == "spider") eyes = 8;
        if (Id("marilith")) arms = 6;
        if (Id("glabrezu", "silkhound")) arms = 4;
        if (Id("xorn")) { arms = 3; eyes = 3; }
        if (plan is "humanoid" or "golem" or "elemental" or "ape") arms = Math.Max(arms, 2);

        // Marks -------------------------------------------------------------------------------------------------------
        void Mark(bool when, string mark)
        {
            if (when && !marks.Contains(mark)) marks.Add(mark);
        }
        Mark(Id("horned", "devil", "minotaur", "goat", "ram", "balor", "gorgon", "bison", "triceratops", "oni", "glabrezu", "satyr", "tikbalang", "golmana", "chimera", "flinthorn") || Hit("horn", "gore") && plan != "humanoid" && head is not "boar", "horns");
        Mark(Id("unicorn", "karkadann", "rhinoceros", "narhistrix"), "horn");
        Mark(Id("elk", "deer", "wolpertinger", "leshy", "poludnitsa", "stag"), "antlers");
        Mark(Id("boar", "mammoth", "elephant", "walrus", "wodyanoi", "bunyip") || Hit("tusk", "tusks"), "tusks");
        Mark(Id("lion", "manticore", "sphinx", "chimera", "yeti", "worg", "griffon", "hippogriff", "lamia"), "mane");
        Mark(Id("barbed", "spiked", "spiny", "thorn", "thorns", "crystalback", "remorhaz", "ankylosaurus", "bulette", "knucker", "manticore") || Hit("spike", "spines", "barbs"), "spikes");
        Mark(Id("turtle", "tortoise", "archelon", "snail", "kappa", "ankheg", "beetle", "rampart"), "shell");
        Mark(Id("shark", "orca", "whale", "sahuagin", "merrow", "bulette", "piranha", "quipper", "fish", "hippocampus", "kelpie") || swimOnly, "fins");
        Mark(Id("cockatrice", "basilisk", "couatl", "phoenix", "behir", "blazewing", "ahuizotl"), "crest");
        Mark(Hit("beard") || Id("duergar", "azer", "dwarf", "bearded"), "beard");
        Mark(Id("king", "queen", "lord", "princess", "sovereign", "ruler", "warlord", "captain", "chancellor", "ringmaster", "hollow", "ancient", "matron", "baron", "general") || m.Boss, "crown");
        Mark(Hit("hooves", "hoof") || Id("satyr", "tikbalang", "golmana") || head is "horse" or "deer" or "ox" && plan == "quadruped", "hooves");
        Mark(Hit("sting", "stinger", "spike") && plan is not "insect" || Id("wyvern", "manticore", "bone"), "stinger");
        Mark(Hit("tentacle", "tentacles", "tendril", "tendrils") && plan != "cephalopod", "tentacles");
        Mark(Hit("pincer", "pincers") && plan != "crab", "pincers");
        Mark(Id("medusa"), "snakehair");
        Mark(Trait("pack tactics"), "pack");
        Mark(Id("vampire", "lich", "noble", "lord", "king", "queen", "chancellor", "baron", "death", "witchfinder") && plan == "humanoid", "cape");
        if (marks.Contains("horn")) marks.Remove("horns");
        Mark(Id("gargoyle", "imp", "quasit", "balor", "pit", "horned", "bone", "erinyes", "succubus") && plan == "humanoid" && type is "fiend" or "elemental", "horns");

        // What it is made of ----------------------------------------------------------------------------------------
        string material =
            Id("fire", "flame", "ember", "magma", "cinder", "hell", "blazewing", "efreeti", "azer", "magmin", "salamander", "emberroot", "brimstone", "scorch", "sovereign") || Hit("burn") ? "fire"
            : Id("ice", "frost", "winter", "rime", "polar", "white", "cold", "yeti") && plan != "humanoid" || Id("ice", "frost") ? "ice"
            : Id("crystal", "ley", "glass", "prism") ? "crystal"
            : Id("water", "wave", "tide", "brine", "sila") && type == "elemental" ? "water"
            : Id("air", "storm", "whirl", "stalker", "invisible", "djinni") && type == "elemental" ? "air"
            : Id("iron", "brass", "clockwork", "engine", "dreadnought", "juggernaut", "mechanist", "armor", "tally", "collector", "balehound", "gorgon") && type == "construct" || Id("iron", "dreadnought") ? "metal"
            : Id("stone", "earth", "gargoyle", "rock", "clay", "colossus", "gravelback", "xorn", "mountain") ? "stone"
            : Id("skeleton", "bone", "bones", "ossuary", "lich", "skull", "rattlebone", "grey") || type == "undead" && head == "skull" ? "bone"
            : plan is "wraith" || Id("ghost", "specter", "phantom", "spectral", "shade", "spirit") ? "ghost"
            : Id("shadow", "gloom", "dusk", "night", "eclipse", "umbral", "black", "shuck") && type is "undead" or "fiend" or "fey" ? "shadow"
            : plan == "ooze" ? "slime"
            : plan == "plant" && head is "mushroom" ? "fungus"
            : plan == "plant" || Id("wood", "leshy", "treant", "dryad") ? "wood"
            : plan is "insect" or "spider" or "crab" ? "chitin"
            : plan is "bird" && wings == "feather" || wings == "feather" && plan != "humanoid" ? "feather"
            : plan is "dragon" or "serpent" or "fish" or "theropod" || head is "lizard" or "croc" or "snake" || type == "dragon" ? "scale"
            : type == "beast" || plan is "quadruped" or "ape" ? "fur"
            : "flesh";
        if (type == "undead" && material is "flesh" or "fur") material = "rot";

        // Gait and roam -----------------------------------------------------------------------------------------------
        string gait = plan switch
        {
            "swarm" => "swarm",
            "ooze" => "ooze",
            "wraith" => "hover",
            "floater" => "hover",
            "fish" => "swim",
            "cephalopod" => swimOnly || swims ? "swim" : "ooze",
            "serpent" or "worm" => "slither",
            "spider" or "insect" or "crab" => wings != "" && flies ? "fly" : "skitter",
            "plant" => head == "tree" ? "lumber" : "root",
            "bird" => "fly",
            "dragon" => "fly",
            "object" => head is "sword" or "skull" or "rug" ? "hover" : head is "cart" or "catapult" ? "trot" : "hop",
            "taur" => head is "serpent" or "fish" ? "slither" : head == "wisp" ? "hover" : head is "spider" or "scorpion" ? "skitter" : "trot",
            "quadruped" => head is "frog" ? "hop" : head is "lizard" or "croc" or "snail" ? "prowl" : head is "cat" or "wolf" ? "prowl" : wings != "" && flies ? "fly" : "trot",
            "ape" or "golem" => "lumber",
            "theropod" => "walk",
            "elemental" => head is "flame" or "wave" or "whirl" ? "hover" : "lumber",
            _ => scale >= 1.5 ? "lumber" : flies && wings != "" ? "fly" : "walk",
        };
        if (plan == "humanoid" && m.Hover) gait = "hover";
        string strike = StrikeOf(m, plan);
        string roam =
            plan == "swarm" ? "swarm"
            : plan == "plant" && head != "tree" ? "rooted"
            : Trait("pack tactics") || Id("wolf", "jackal", "hyena", "raptor", "velociraptor", "rat", "worg") && plan != "humanoid" ? "pack"
            : head is "horse" or "deer" or "ox" && type == "beast" ? "herd"
            : plan is "fish" or "cephalopod" && swims ? "school"
            : burrows && plan is not "humanoid" ? "burrow"
            : Trait("false appearance", "ambush", "mimicry", "shapechanger", "web walker", "spider climb") && plan is not "humanoid" ? "lurk"
            : plan is "wraith" || type == "undead" && m.Hover ? "haunt"
            : gait == "fly" ? "circle"
            : gait == "hover" ? "drift"
            : plan is "humanoid" or "golem" || type is "humanoid" ? "march"
            : "lone";

        // Gear for people and golems ----------------------------------------------------------------------------------
        string frame = "", helm = "", weapon = "", off = "";
        if (plan is "humanoid" or "golem" or "taur")
        {
            (frame, helm, weapon, off) = Gear(m, plan, type, head, Id, Hit);
            if (strike is not ("weapon" or "shoot" or "cast")) weapon = "";
            if (strike is "shoot" && weapon == "") weapon = "Bow";
        }

        // Colour ------------------------------------------------------------------------------------------------------
        var (hide, belly, accent) = Colours(rules, m, material, type, Id);

        var a = new Anatomy
        {
            Id = m.Id, Plan = plan, Head = head, Gait = gait, Strike = strike, Roam = roam,
            Scale = scale, Bulk = Js.Round(bulk * 100) / 100, Leg = Js.Round(leg * 100) / 100, Neck = neck, Tail = tail,
            Heads = heads, Legs = legs, Arms = arms, Eyes = eyes, Wings = wings, Marks = marks, Material = material,
            Hide = hide, Belly = belly, Accent = accent, Frame = frame, Helm = helm, Weapon = weapon, Off = off,
        };
        if (rules.Creatures.TryGetValue(m.Id, out var o)) a = Apply(a, o);
        return a;
    }

    private static readonly (string Word, string[] Marks)[] DragonMarks =
    [
        ("red", ["horns", "frill"]), ("blue", ["horn", "frill"]), ("green", ["crest"]), ("black", ["horns", "skull"]), ("white", ["spikes", "crest"]),
        ("gold", ["whiskers", "frill"]), ("silver", ["crest", "frill", "whiskers"]), ("brass", ["plate"]), ("bronze", ["fins", "horns"]), ("copper", ["horns", "plate"]),
    ];

    private static string StrikeOf(LibMonster m, string plan)
    {
        if (m.Strikes.Count == 0) return plan is "ooze" ? "engulf" : plan is "plant" ? "lash" : "touch";
        var first = Words(m.Strikes[0]);
        bool F(params string[] w) => w.Any(first.Contains);
        if (F("bow", "longbow", "shortbow", "crossbow", "pistol", "sling", "dart", "javelin") && plan == "humanoid") return "shoot";
        if (F("burst", "bolt", "ray", "flame", "blast", "wrath", "smite", "beam", "drain", "flourish", "concoction", "spheres", "song", "dirge", "knell", "edict", "clause", "calling") && plan is "humanoid" or "wraith" or "floater" or "elemental" or "taur") return "cast";
        if (plan is "humanoid" or "golem" or "taur" && F("sword", "greatsword", "longsword", "shortsword", "scimitar", "rapier", "dagger", "axe", "greataxe", "battleaxe", "handaxe", "mace", "club", "greatclub", "hammer", "warhammer", "maul",
                "spear", "pike", "glaive", "lance", "pick", "flail", "morningstar", "staff", "quarterstaff", "sickle", "scythe", "knife", "cleaver", "hatchet", "blade", "fork", "whip", "lash", "cane", "jambiya", "saber", "stiletto", "rod", "scepter", "ladle", "trident", "dident", "nightblades", "crook", "hook"))
            return "weapon";
        if (F("bite", "bites", "fangs", "maw", "jaw", "mandibles", "mandible", "beak", "beaks", "gouge", "rasp", "glyphbite", "proboscis")) return plan is "bird" && F("beak", "gouge") ? "talons" : "bite";
        if (F("claw", "claws", "rend", "scratch", "rake", "swipe", "talons", "forepaw", "slash", "hands")) return plan == "bird" ? "talons" : "claw";
        if (F("sting", "stinger", "spike", "needle")) return "sting";
        if (F("gore", "horn", "ram", "butt", "tusk", "tusks", "shove")) return "gore";
        if (F("hooves", "hoof", "stomp", "trample", "wheels", "kick")) return "trample";
        if (F("tentacle", "tentacles", "tendril", "lash", "whip", "vine", "branch", "briar", "tongue", "tail", "constrict", "coil", "chain", "grasp", "grip", "grab")) return "lash";
        if (F("pseudopod", "engulf", "smother", "dissolve", "swallowing", "attach", "crush")) return "engulf";
        if (F("spit", "spray", "hurl", "lob", "rock", "boulder", "shot", "launch")) return "spit";
        if (F("touch", "drain", "shock", "jolt")) return "touch";
        if (F("slam", "fist", "fists", "pummel", "punch", "bash", "arm", "smash")) return "slam";
        return plan == "humanoid" ? "weapon" : "slam";
    }

    private static (string Frame, string Helm, string Weapon, string Off) Gear(LibMonster m, string plan, string type, string head, Func<string[], bool> id, Func<string[], bool> hit)
    {
        bool Id(params string[] w) => id(w);
        bool Hit(params string[] w) => hit(w);
        string armour = m.ArmorNote.ToLowerInvariant();
        string frame =
            plan == "golem" ? "Plate"
            : head == "skull" || Id("skeleton") ? "Bones"
            : armour.Contains("plate") || armour.Contains("splint") || Id("knight", "castellan", "sentinel", "marshal", "paladin", "armor") ? "Plate"
            : armour.Contains("chain") || armour.Contains("scale") || armour.Contains("ring") || armour.Contains("breastplate") || Id("guard", "warrior", "veteran", "hobgoblin", "captain", "soldier", "infantry", "ironhide") ? "Mail"
            : Id("orc", "ogre", "troll", "ettin", "giant", "bloodtusk", "gnoll", "berserker", "minotaur", "bugbear", "hezrou", "nalfeshnee", "glabrezu", "balor", "fachan", "skullcrusher", "rammer") || type == "giant" ? "Brute"
            : Id("mage", "priest", "cultist", "druid", "lich", "acolyte", "shaman", "seer", "witch", "warlock", "archmage", "archdruid", "prophet", "hag", "alchemist", "confessor", "chancellor", "notary", "whisperer", "hierophant", "caller", "wyrdling", "mummy") || type is "celestial" ? "Robe"
            : armour.Contains("leather") || armour.Contains("hide") || armour.Contains("studded") ? "Leather"
            : Id("vampire") ? "Leather"
            : Id("monk", "commoner", "satyr", "dryad", "sprite", "zombie", "ghoul", "ghast", "were", "imp", "quasit", "dretch", "succubus", "incubus") || type is "fiend" or "fey" or "undead" or "elemental" ? "Bare"
            : "Leather";
        string helm =
            head != "" ? ""
            : Id("cultist", "fanatic", "confessor") ? "Cowl"
            : Id("priest", "acolyte", "hierophant", "prophet", "saint") ? "Mitre"
            : Id("mage", "archmage", "apprentice", "alchemist", "witch") ? "WizardHat"
            : Id("druid", "archdruid", "shaman", "seer", "leshy") ? "Antlers"
            : Id("assassin", "spy", "scout", "bandit", "thug", "rogue", "poacher", "ranger", "whisperer", "velvet", "highway", "toll", "drow") ? "Hood"
            : Id("berserker", "wild", "raider", "outrunner") ? "HornedHelm"
            : Id("knight", "paladin", "castellan", "marshal") ? "GreatHelm"
            : Id("guard", "warrior", "infantry", "soldier", "veteran", "captain") ? "Sallet"
            : Id("noble", "chancellor", "notary", "bard", "ringmaster", "lord", "princess", "baron") ? "Feathered"
            : Id("pirate", "gladiator", "commoner", "tough", "mechanist", "artificer") ? "Coif"
            : type == "celestial" ? "Circlet"
            : Id("hag", "kikimora", "poludnitsa") ? "WitchHat"
            : frame == "Bones" ? "Skull"
            : frame == "Brute" ? "Tusks"
            : type != "humanoid" || plan == "golem" ? ""
            : new[] { "Topknot", "Coif", "Hood", "Sallet" }[(int)(Hash(m.Id) % 4)];
        string w = m.Strikes.Count > 0 ? m.Strikes[0].ToLowerInvariant() : "";
        bool W(params string[] k) => k.Any(w.Contains);
        string weapon =
            W("greatsword", "flame sword", "storm sword", "radiant sword", "flying sword", "black longsword", "grave sword") ? "Greatsword"
            : W("greataxe", "frost axe", "battleaxe", "tusk-axe") ? "Greataxe"
            : W("axe", "cleaver", "hatchet") ? "Axe"
            : W("maul", "greatclub", "tree club", "stone club", "spiked club", "iron-banded", "skull club") ? "Maul"
            : W("hammer", "mace", "morningstar", "club", "flail", "cane", "rod", "ladle") ? (W("hammer") ? "Hammer" : "Mace")
            : W("spear", "pike", "lance", "trident", "fork", "dident", "javelin") ? "Spear"
            : W("glaive", "halberd", "scythe", "pick") ? "Glaive"
            : W("rapier", "saber", "stiletto") ? "Rapier"
            : W("dagger", "knife", "sickle", "needle", "blade", "jambiya") ? (W("sickle") ? "Sickle" : "Dagger")
            : W("staff", "crook", "scepter") ? "Staff"
            : W("bow", "crossbow", "pistol") ? "Bow"
            : W("sword", "scimitar", "longsword", "shortsword") ? "Sword"
            : W("burst", "bolt", "ray", "blast", "flame", "wrath", "smite", "drain", "touch") && plan == "humanoid" ? "Staff"
            : "";
        string off =
            Hit("shield", "smash") || Id("guard", "knight", "hobgoblin", "ironhide", "warrior") && weapon is "Sword" or "Spear" or "Mace" ? "Kite"
            : Id("mage", "archmage", "lich", "wyrdling", "apprentice", "kobold") && weapon is "Staff" or "" ? "Orb"
            : Id("priest", "acolyte", "cultist", "prophet", "notary", "chancellor") ? "Book"
            : Id("bard", "chorister") ? "Lute"
            : weapon is "Rapier" or "Dagger" && Id("assassin", "spy", "rogue", "drow", "whisperer") ? "Dagger"
            : m.Strikes.Skip(1).Any(s => s.Contains("bow", StringComparison.OrdinalIgnoreCase) || s.Contains("crossbow", StringComparison.OrdinalIgnoreCase)) && weapon != "Bow" ? "Bow"
            : Id("pirate", "toll", "bandit", "outlaw") ? "Round"
            : Id("lantern", "lampblack", "sackman") ? "Lantern"
            : "";
        if (weapon is "Greatsword" or "Greataxe" or "Maul" or "Glaive" or "Bow" && off is "Kite" or "Round") off = "";
        return (frame, helm, weapon, off);
    }

    private static (string Hide, string Belly, string Accent) Colours(BestiaryRules rules, LibMonster m, string material, string type, Func<string[], bool> id)
    {
        bool Id(params string[] w) => id(w);
        string? named = null;
        foreach (var w in m.Id.Split('-'))
            if (rules.Colours.TryGetValue(w, out var hex)) named ??= hex;
        string baseHex = named ?? material switch
        {
            "fire" => "d4552a",
            "ice" => "cfe4f0",
            "crystal" => "8ad8e0",
            "water" => "3a8ac8",
            "air" => "c8dce8",
            "metal" => "8a8f98",
            "stone" => "8a8478",
            "bone" => "ddd3b8",
            "ghost" => "9ac8d8",
            "shadow" => "3a3448",
            "slime" => "7ac850",
            "fungus" => "b89a7a",
            "wood" => "6a5034",
            "rot" => "7a8a62",
            _ => "",
        };
        if (baseHex == "")
        {
            var pool = rules.TypeHides.TryGetValue(type, out var p) && p.Length > 0 ? p : ["7a6650"];
            baseHex = pool[(int)(Hash(m.Id) % (uint)pool.Length)];
        }
        // Every creature its own shade: a nudge of hue, saturation and value from its id, larger for those the name doesn't colour.
        uint h = Hash(m.Id + "#");
        double spread = named != null ? 0.03 : 0.07;
        var hide = Nudge(baseHex, ((h & 0xff) / 255.0 - 0.5) * spread, (((h >> 8) & 0xff) / 255.0 - 0.5) * 0.2, (((h >> 16) & 0xff) / 255.0 - 0.5) * 0.18);
        var belly = Nudge(hide, 0.02, -0.25, 0.22);
        string accent = material switch
        {
            "fire" => "ffb030",
            "ice" => "8ae0ff",
            "ghost" => "8ae0ff",
            "shadow" => "b65cff",
            "crystal" => "c8f8ff",
            _ => type switch
            {
                "fiend" => "ff4a2a",
                "undead" => "5ac8ff",
                "celestial" => "ffe08a",
                "fey" => "c8ff6a",
                "aberration" => "e05aff",
                "dragon" => Id("red", "gold", "brass", "copper") ? "ffd040" : "ffc030",
                "elemental" => "ffffff",
                "construct" => "6ac8ff",
                _ => "ffcc40",
            },
        };
        return (hide, belly, accent);
    }

    /// <summary>A hex colour shifted in hue (turns), saturation and value.</summary>
    private static string Nudge(string hex, double dh, double ds, double dv)
    {
        int v = Convert.ToInt32(hex, 16);
        double r = (v >> 16 & 0xff) / 255.0, g = (v >> 8 & 0xff) / 255.0, b = (v & 0xff) / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b)), d = max - min;
        double hue = d == 0 ? 0 : max == r ? ((g - b) / d % 6 + 6) % 6 / 6 : max == g ? ((b - r) / d + 2) / 6 : ((r - g) / d + 4) / 6;
        double sat = max == 0 ? 0 : d / max, val = max;
        hue = ((hue + dh) % 1 + 1) % 1;
        sat = Math.Clamp(sat + ds, 0, 1);
        val = Math.Clamp(val + dv, 0.08, 1);
        double c = val * sat, x = c * (1 - Math.Abs(hue * 6 % 2 - 1)), mm = val - c;
        var (rr, gg, bb) = (int)(hue * 6) switch { 0 => (c, x, 0.0), 1 => (x, c, 0.0), 2 => (0.0, c, x), 3 => (0.0, x, c), 4 => (x, 0.0, c), _ => (c, 0.0, x) };
        int R = (int)Math.Round((rr + mm) * 255), G = (int)Math.Round((gg + mm) * 255), B = (int)Math.Round((bb + mm) * 255);
        return $"{R:x2}{G:x2}{B:x2}";
    }

    /// <summary>FNV-1a: a stable number per id, the same on every machine.</summary>
    public static uint Hash(string s)
    {
        uint h = 2166136261;
        foreach (char ch in s)
        {
            h ^= ch;
            h *= 16777619;
        }
        return h;
    }

    private static Anatomy Apply(Anatomy a, AnatomyOverride o)
    {
        var marks = a.Marks.ToList();
        foreach (var mk in o.Marks)
        {
            if (mk.StartsWith('-')) marks.Remove(mk[1..]);
            else if (!marks.Contains(mk)) marks.Add(mk);
        }
        return a with
        {
            Plan = o.Plan ?? a.Plan, Head = o.Head ?? a.Head, Gait = o.Gait ?? a.Gait, Strike = o.Strike ?? a.Strike, Roam = o.Roam ?? a.Roam,
            Wings = o.Wings ?? a.Wings, Material = o.Material ?? a.Material, Hide = o.Hide ?? a.Hide, Belly = o.Belly ?? a.Belly, Accent = o.Accent ?? a.Accent,
            Frame = o.Frame ?? a.Frame, Helm = o.Helm ?? a.Helm, Weapon = o.Weapon ?? a.Weapon, Off = o.Off ?? a.Off,
            Heads = o.Heads ?? a.Heads, Legs = o.Legs ?? a.Legs, Arms = o.Arms ?? a.Arms, Eyes = o.Eyes ?? a.Eyes,
            Neck = o.Neck ?? a.Neck, Tail = o.Tail ?? a.Tail, Bulk = o.Bulk ?? a.Bulk, Leg = o.Leg ?? a.Leg, Scale = o.Scale ?? a.Scale,
            Marks = marks,
        };
    }
}
