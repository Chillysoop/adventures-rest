namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// A part cut from a creature (spoils.json "trophies"): wolf fangs, troll blood, a drake's scale. A creature gives the
/// first whose words are in its name, else the first for its type, inside the challenge range. Grade is how fine it
/// is for the forge's recipes (1 common, 2 rare, 3 legendary).
/// </summary>
public sealed record TrophyDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public IReadOnlyList<string> Words { get; init; } = [];
    public IReadOnlyList<string> Types { get; init; } = [];
    public double[] Cr { get; init; } = [0, 99];

    /// <summary>Gold one is worth to the town's traders.</summary>
    public double Value { get; init; } = 2;
    public int Grade { get; init; } = 1;
    public bool Fits(double cr) => cr >= Cr[0] && cr <= Cr[^1];
}

/// <summary>
/// A magic item a foe can drop (spoils.json "drops"): from creatures with these words in their names or of these types
/// (a frost giant's gauntlets), or from a cleared site of these groups, signature foes, menace lineages or kinds, or a
/// people's hall taken by the army, or any cleared site of a theme or habitat (sea or sky loot). Chance is for one creature of challenge 0; more for stronger ones and bosses.
/// </summary>
public sealed record DropDef
{
    public string Ref { get; init; } = "";
    public IReadOnlyList<string> Words { get; init; } = [];
    public IReadOnlyList<string> Types { get; init; } = [];
    public IReadOnlyList<string> Groups { get; init; } = [];
    public IReadOnlyList<string> Foes { get; init; } = [];
    public IReadOnlyList<string> Lineages { get; init; } = [];
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public IReadOnlyList<string> Peoples { get; init; } = [];

    /// <summary>Or a cleared site in a region of these themes (a sea reach, a sky isle), or with one of these habitats.</summary>
    public IReadOnlyList<string> Themes { get; init; } = [];
    public IReadOnlyList<string> Habitats { get; init; } = [];

    /// <summary>Or a cleared isle in one of these heights of the sky (world.json isles bands).</summary>
    public IReadOnlyList<string> Bands { get; init; } = [];
    public double Chance { get; init; } = 0.05;

    /// <summary>Only the master of a place carries it.</summary>
    public bool Boss { get; init; }
}

/// <summary>Something the town's craftsmen can make (spoils.json "recipes"): at which building, for what gold, parts and days.</summary>
public sealed record RecipeDef
{
    public string Id { get; init; } = "";
    public string Ref { get; init; } = "";
    public IReadOnlyList<string> At { get; init; } = [];
    public double Gold { get; init; }
    public int Days { get; init; } = 3;

    /// <summary>Parts it takes, by trophy id or "grade:N" (any parts of that grade or finer).</summary>
    public IReadOnlyDictionary<string, int> Needs { get; init; } = new Dictionary<string, int>();

    /// <summary>The charter it needs (index into city.json tiers), and a faction chapter in town ("" for none).</summary>
    public int Tier { get; init; }
    public string Faction { get; init; } = "";
}

/// <summary>What foes leave behind and what the town makes of it (content/game/spoils.json).</summary>
public sealed record SpoilsRules
{
    public IReadOnlyList<TrophyDef> Trophies { get; init; } = [];
    public IReadOnlyList<DropDef> Drops { get; init; } = [];
    public IReadOnlyList<RecipeDef> Recipes { get; init; } = [];

    /// <summary>Chance a kind of creature beaten yields its part, and more for each point of challenge (bosses always do).</summary>
    public double TrophyChance { get; init; } = 0.35;
    public double PerCr { get; init; } = 0.06;

    /// <summary>A part's worth grows by this share for each point of the creature's challenge.</summary>
    public double ValuePerCr { get; init; } = 0.25;

    /// <summary>Share of the parts' worth the town's traders pay the company; share the treasury gets selling them on.</summary>
    public double HeroShare { get; init; } = 0.6;
    public double SellShare { get; init; } = 0.5;

    /// <summary>Most of one part the town keeps; the rest go on the road.</summary>
    public int Cap { get; init; } = 24;

    /// <summary>A drop's chance grows by this share per point of challenge, times this for a boss; at most this many drops come home a trip.</summary>
    public double DropPerCr { get; init; } = 0.12;
    public double BossDrop { get; init; } = 2;
    public int MostDrops { get; init; } = 2;

    /// <summary>Drops a people's hall yields when the army takes it.</summary>
    public int Conquest { get; init; } = 2;

    /// <summary>The stand-in mayor sells parts over this many of a kind.</summary>
    public int AutoKeep { get; init; } = 12;

    /// <summary>Each open trade road adds this share to what the caravans pay for parts, up to <see cref="RouteMost"/> roads.</summary>
    public double RouteShare { get; init; } = 0.1;
    public int RouteMost { get; init; } = 5;

    /// <summary>
    /// A magic item of this rarity or finer (<see cref="Spoils.Rank"/>: 2 rare, 3 very rare, 4 legendary, 5 artifact)
    /// found by heroes raises an alert for this many days, one of <see cref="GreatRank"/> or finer that beats all the
    /// finders already carry a louder one. Kinds
    /// in <see cref="FindSkip"/> (potions, scrolls) are used up, so they make no alert.
    /// </summary>
    public int FindRank { get; init; } = 2;
    public int GreatRank { get; init; } = 3;
    public int FindDays { get; init; } = 3;
    public IReadOnlyList<string> FindSkip { get; init; } = [];

    private HashSet<string>? _signature;

    /// <summary>Whether the item only drops from the foes its drop names.</summary>
    public bool Signature(string itemId) => (_signature ??= [.. Drops.Select(d => d.Ref)]).Contains(itemId);

    private Dictionary<string, TrophyDef?>? _for;
    private Dictionary<string, TrophyDef>? _byId;

    public TrophyDef? Trophy(string id) => (_byId ??= Trophies.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First())).GetValueOrDefault(id);
    public RecipeDef? Recipe(string id) => Recipes.FirstOrDefault(r => r.Id == id);

    /// <summary>The part a creature gives: by a word in its name, else by its type.</summary>
    public TrophyDef? For(string id, string name, string type, double cr)
    {
        _for ??= [];
        if (_for.TryGetValue(id, out var t)) return t;
        string n = Spoils.Plain($"{name} {id}");
        string ty = type.ToLowerInvariant();
        t = Trophies.FirstOrDefault(x => x.Words.Count > 0 && x.Fits(cr) && x.Words.Any(w => Spoils.Named(n, w)))
            ?? Trophies.FirstOrDefault(x => x.Words.Count == 0 && x.Fits(cr) && x.Types.Any(y => ty.StartsWith(y, StringComparison.Ordinal)));
        _for[id] = t;
        return t;
    }
}

/// <summary>An order on the forge's or the workshop's book.</summary>
public sealed class CraftOrder
{
    public int Id { get; init; }
    public string RecipeId { get; init; } = "";
    public int BuildingId { get; init; }
    public int DueTick { get; init; }
    public int Day { get; init; }

    /// <summary>Gold paid for it, given back if it's called off (0 on orders saved before it was kept: the recipe's price).</summary>
    public double Paid { get; init; }

    /// <summary>The hero who ordered it with their own gold; it goes to them, not the armory.</summary>
    public int? ForId { get; init; }
}

public sealed class SpoilsState
{
    /// <summary>Parts the town holds, by trophy id.</summary>
    public OrderedMap<string, int> Trophies { get; } = new();
    public List<CraftOrder> Orders { get; } = [];
    public int NextOrder { get; set; } = 1;
    public int Crafted { get; set; }
    public int Dropped { get; set; }
    public double Sold { get; set; }

    /// <summary>Rare and finer magic items heroes brought home lately, for the alert (<see cref="Spoils.Found"/>).</summary>
    public List<RareFind> Finds { get; } = [];
    public int NextFind { get; set; } = 1;
}

/// <summary>A rare or finer magic item a hero or a company found, kept a few days for the alert.</summary>
public sealed class RareFind
{
    public int Id { get; init; }
    public int Day { get; init; }
    public required LibItem Item { get; init; }

    /// <summary>"Bryn" or "the Iron Wolves", and whether that is a whole company.</summary>
    public string Who { get; init; } = "";
    public bool Company { get; init; }

    /// <summary>The hero who carries it home, if one does; the site it came from.</summary>
    public int? HeroId { get; init; }
    public string? SiteId { get; init; }

    /// <summary>Finer than anything the finders carried: it slides a tile out of the rail, not only a badge.</summary>
    public bool Great { get; init; }
}

/// <summary>
/// The spoils of the wilds. Every creature a company beats may leave a part (wolf fangs, troll blood, a drake's scale):
/// the company sells it to the town's traders on the way in, and the town keeps it to sell on or to craft with. Strong
/// foes carry magic of their own: frost giants their gauntlets, a vampire lord its signet, a group or a menace's line
/// the arms they are known by. The forge and the workshops make plain +1/+2/+3 arms and finer pieces from the parts.
/// Rolls use the trip's own dice (seeded from the seed, tick and company), never <see cref="Town.Rng"/>.
/// </summary>
public static class Spoils
{
    public static SpoilsRules Rules(Town town) => town.Content.Spoils;

    /// <summary>A name made plain for matching words: " frost giant " (lower case, spaced, no punctuation).</summary>
    public static string Plain(string name)
    {
        var sb = new System.Text.StringBuilder(" ");
        foreach (char ch in name.ToLowerInvariant()) sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        return sb.Append(' ').ToString();
    }

    /// <summary>Whether a plain name holds the word or phrase whole ("rat" is in "giant rat", not in "pirate"), or its plural.</summary>
    public static bool Named(string plain, string word) =>
        plain.Contains($" {word} ", StringComparison.Ordinal) || plain.Contains($" {word}s ", StringComparison.Ordinal) || plain.Contains($" {word}es ", StringComparison.Ordinal);

    private static Rng Dice(Town town, int salt) => new(unchecked((int)Voices.Hash("spoils", town.Seed, town.Tick, salt)));

    /// <summary>The library creature behind a fight's id, and its challenge.</summary>
    public static (string Name, string Type, double Cr, bool Boss) Creature(Content c, string id)
    {
        MonsterDef? md = null;
        try { md = c.Monster(id); } catch (KeyNotFoundException) { }
        var lib = c.Library.Monster(md?.Ref is { Length: > 0 } r ? r : id) ?? c.Library.Monster(id);
        string name = lib?.Name ?? md?.Name ?? id;
        string type = lib?.Type ?? md?.Type ?? "";
        double cr = lib?.Cr ?? Math.Max(0, (md?.Xp ?? 0) / 200.0);
        return (name, type, cr, md?.Boss == true || lib?.Boss == true);
    }

    public static TrophyDef? TrophyOf(Content c, string id)
    {
        var (name, type, cr, _) = Creature(c, id);
        return c.Spoils.For(id, name, type, cr);
    }

    /// <summary>What one part is worth.</summary>
    public static double Value(Town town, TrophyDef t) => t.Value;

    // Homecoming ----------------------------------------------------------------------------------

    /// <summary>
    /// A company home from a site: the parts of what they beat (sold to the town's traders), and any magic the strongest
    /// of them carried or the place itself held for its masters.
    /// </summary>
    public static void Homecoming(Town town, Party party, SiteState site, ExpeditionResult result, IReadOnlyList<Agent> survivors, Bounty? bounty)
    {
        if (survivors.Count == 0 || result.Outcome == ExpeditionOutcome.Wiped || result.Fought.Count == 0) return;
        var r = Rules(town);
        var dice = Dice(town, party.Id);
        bool cleared = result.Outcome == ExpeditionOutcome.Cleared;
        // A company that ran left the last fight's dead where they fell.
        var beaten = cleared || result.GoalMet ? result.Fought.ToList() : result.Fought.Take(result.Fought.Count - 1).ToList();

        var parts = new OrderedMap<string, int>();
        double worth = 0;
        foreach (var id in beaten)
        {
            var (name, type, cr, boss) = Creature(town.Content, id);
            var t = r.For(id, name, type, cr);
            if (t == null || !(boss || dice.Chance(Math.Min(0.95, r.TrophyChance + r.PerCr * cr)))) continue;
            int n = 1 + Math.Min(3, (int)(cr / 4)) + (boss ? 1 : 0);
            parts[t.Id] = parts.Get(t.Id, 0) + n;
            worth += n * t.Value * (1 + r.ValuePerCr * cr);
        }
        if (parts.Count > 0)
        {
            foreach (var (id, n) in parts) town.Spoils.Trophies[id] = Math.Min(r.Cap, town.Spoils.Trophies.Get(id, 0) + n);
            double paid = Math.Floor(worth * r.HeroShare);
            foreach (var m in survivors) m.Gold += paid / survivors.Count;
            double tax = Math.Floor(paid * town.TaxRate);
            if (tax > 0) town.Book(TreasuryAccount.SalesTax, tax);
            string list = Expedition.ListNames(parts.Select(p => Count(r.Trophy(p.Key)!, p.Value)).ToList());
            town.Log(LedgerCategory.Quest, Importance.Routine, $"The {party.Name} bring home {list}, and the town's traders pay {Js.Str(paid)} gold for them.", survivors.Select(m => m.Id).ToList());
        }

        var drops = new List<LibItem>();
        void Roll(DropDef d, double cr, bool boss)
        {
            if (drops.Count >= r.MostDrops || d.Boss && !boss) return;
            if (town.Content.Library.Item(d.Ref) is not LibItem it || drops.Contains(it)) return;
            if (!dice.Chance(Math.Min(0.9, d.Chance * (1 + r.DropPerCr * cr) * (boss ? r.BossDrop : 1)))) return;
            drops.Add(it);
        }
        foreach (var id in beaten)
        {
            var (name, type, cr, boss) = Creature(town.Content, id);
            foreach (var d in ByCreature(town.Content, name, type)) Roll(d, cr, boss);
        }
        if (cleared)
        {
            double cr = Math.Max(1, site.Def.Tier);
            var menace = Menaces.At(town, site.Def.Id);
            string theme = site.RegionId is int rid && rid >= 0 && rid < town.Regions.Count ? town.Regions[rid].Theme.Id : "";
            foreach (var d in r.Drops)
            {
                bool mine = site.Def.Group != "" && d.Groups.Contains(site.Def.Group)
                    || site.Def.Foe != "" && d.Foes.Contains(site.Def.Foe)
                    || menace != null && (d.Lineages.Contains(menace.Lineage) || d.Kinds.Contains(menace.Kind))
                    || site.Def.Kind != "" && d.Kinds.Contains(site.Def.Kind)
                    || theme != "" && d.Themes.Contains(theme)
                    || d.Habitats.Count > 0 && site.Def.Habitats.Any(d.Habitats.Contains)
                    || site.Def.Band != "" && d.Bands.Contains(site.Def.Band);
                if (mine) Roll(d, cr, true);
            }
        }
        foreach (var it in drops) Bring(town, party, bounty, it, survivors, site);
    }

    /// <summary>Drops a creature of this name and type can carry.</summary>
    public static IEnumerable<DropDef> ByCreature(Content c, string name, string type)
    {
        string n = Plain(name), ty = type.ToLowerInvariant();
        return c.Spoils.Drops.Where(d => d.Words.Any(w => Named(n, w)) || d.Types.Any(t => ty.StartsWith(t, StringComparison.Ordinal)));
    }

    /// <summary>A dropped piece comes home: whoever gains most wears it, else it goes to the armory (regalia always does).</summary>
    private static void Bring(Town town, Party party, Bounty? bounty, LibItem it, IReadOnlyList<Agent> survivors, SiteState site)
    {
        town.Spoils.Dropped++;
        var who = survivors.OrderByDescending(m => Gear.Gain(town.Content, m, it)).ThenBy(m => m.Id).First();
        bool useful = Gear.Gain(town.Content, who, it) > 0;
        Found(town, it, party, survivors, useful ? who.Id : null, site.Def.Id);
        if (Regalia.Is(town, it) || it.Slot == "town")
        {
            Watch.ReceiveRelic(town, it, party.Name);
            return;
        }
        if (!useful)
        {
            Armory.Add(town, it, $"the {party.Name}' spoils");
            town.Log(LedgerCategory.Quest, Importance.Notable, $"The {party.Name} carry the {it.Name} home from {site.Def.Name}. Nobody among them can use it, so it goes to the armory.", survivors.Select(m => m.Id).ToList());
            return;
        }
        town.Log(LedgerCategory.Quest, Importance.Notable, $"{who.Name} takes the {it.Name} from what they beat at {site.Def.Name}.", [who.Id]);
        if (Armory.HandIn(town, party, bounty, who, it)) return;
        Gear.Claim(town, who, it);
        Moments.Claimed(town, who, it, party.Name, site, survivors);
    }

    /// <summary>
    /// Heroes bring home a magic item: one of <see cref="SpoilsRules.FindTier"/> or finer is kept a few days for the
    /// alert (<see cref="Alerts"/>), named for the company when more than one rode, else for the hero.
    /// </summary>
    public static void Found(Town town, LibItem it, string who, bool company, int? heroId, string? siteId, IReadOnlyList<Agent> finders)
    {
        var r = Rules(town);
        if (Rank(it) < r.FindRank || r.FindSkip.Contains(it.Category)) return;
        var st = town.Spoils;
        st.Finds.RemoveAll(f => town.Day - f.Day > r.FindDays);
        // A company's first legendary is a moment; its fifth is a good day's work.
        int best = finders.SelectMany(m => m.Hero?.Magic.Values ?? []).Select(Rank).DefaultIfEmpty(-1).Max();
        bool great = Rank(it) >= r.GreatRank && Rank(it) > best;
        st.Finds.Add(new RareFind { Id = st.NextFind++, Day = town.Day, Item = it, Who = who, Company = company, HeroId = heroId, SiteId = siteId, Great = great });
    }

    private static readonly string[] Rarities = ["common", "uncommon", "rare", "very-rare", "legendary", "artifact"];

    /// <summary>An item's rarity as a rank: 0 common, 1 uncommon, 2 rare, 3 very rare, 4 legendary, 5 artifact (its tier when it names none).</summary>
    public static int Rank(LibItem it) => Array.IndexOf(Rarities, it.Rarity) is var i && i >= 0 ? i : Math.Clamp(it.Tier, 0, Rarities.Length - 1);

    public static void Found(Town town, LibItem it, Party party, IReadOnlyList<Agent> survivors, int? heroId, string? siteId)
    {
        bool company = survivors.Count > 1;
        string who = company ? $"the {party.Name}" : survivors.FirstOrDefault()?.GivenName ?? $"the {party.Name}";
        Found(town, it, who, company, heroId, siteId, survivors);
    }

    /// <summary>"3 wolf fangs", "a troll heart".</summary>
    public static string Count(TrophyDef t, int n) => n == 1 ? $"{Agent.Article(t.Name)} {Single(t.Name)}" : $"{n} {t.Name}";

    private static string Single(string name) => name.EndsWith("ves", StringComparison.Ordinal) ? name[..^3] + "f"
        : name.EndsWith("ies", StringComparison.Ordinal) ? name[..^3] + "y"
        : name.EndsWith("ss", StringComparison.Ordinal) || !name.EndsWith('s') ? name : name[..^1];

    /// <summary>The army takes a people's hall: what its lords kept goes to the armory.</summary>
    public static void Conquered(Town town, string people, string hall)
    {
        var r = Rules(town);
        var dice = Dice(town, people.Length * 7919 + hall.Length);
        var pool = r.Drops.Where(d => d.Peoples.Contains(people)).ToList();
        for (int i = 0; i < r.Conquest && pool.Count > 0; i++)
        {
            var d = pool[dice.Range(0, pool.Count)];
            pool.Remove(d);
            if (town.Content.Library.Item(d.Ref) is not LibItem it) continue;
            town.Spoils.Dropped++;
            Armory.Add(town, it, $"taken at {hall}");
            town.Log(LedgerCategory.War, Importance.Notable, $"In the treasury of {hall} the army finds the {it.Name}. It goes to the armory.");
        }
    }

    // Selling and crafting ------------------------------------------------------------------------

    /// <summary>Sell parts on to the caravans: the treasury gets its share of their worth.</summary>
    public static double Sell(Town town, string trophyId, int n)
    {
        var t = Rules(town).Trophy(trophyId);
        int have = town.Spoils.Trophies.Get(trophyId, 0);
        n = Math.Min(n, have);
        if (t == null || n <= 0) return 0;
        double gold = Math.Floor(n * Price(town, t));
        Take(town, trophyId, n);
        town.Book(TreasuryAccount.Armory, gold);
        town.Spoils.Sold += gold;
        int roads = Math.Min(Rules(town).RouteMost, Trade.Open(town).Count());
        town.Log(LedgerCategory.Town, Importance.Routine, $"The town sells {Count(t, n)} to the caravans for {Js.Str(gold)} gold{(roads > 0 ? $", carried down {roads} trade {(roads == 1 ? "road" : "roads")}" : "")}.");
        return gold;
    }

    /// <summary>What one part fetches from the caravans: its share of the worth, more for each trade road it can travel.</summary>
    public static double Price(Town town, TrophyDef t)
    {
        var r = Rules(town);
        return t.Value * r.SellShare * (1 + r.RouteShare * Math.Min(r.RouteMost, Trade.Open(town).Count()));
    }

    /// <summary>Parts taken from the stores for another use (the apothecary's pot, a notable's ask).</summary>
    public static void Use(Town town, string trophyId, int n) => Take(town, trophyId, Math.Min(n, town.Spoils.Trophies.Get(trophyId, 0)));

    private static void Take(Town town, string trophyId, int n)
    {
        int left = town.Spoils.Trophies.Get(trophyId, 0) - n;
        if (left > 0) town.Spoils.Trophies[trophyId] = left;
        else town.Spoils.Trophies.Remove(trophyId);
    }

    /// <summary>The parts a recipe would use, by trophy id: named ones first, then the cheapest of a grade.</summary>
    public static OrderedMap<string, int>? Parts(Town town, RecipeDef rec)
    {
        var r = Rules(town);
        var have = new OrderedMap<string, int>();
        foreach (var (k, v) in town.Spoils.Trophies) have[k] = v;
        var use = new OrderedMap<string, int>();
        foreach (var (need, n) in rec.Needs.OrderBy(x => x.Key.StartsWith("grade:", StringComparison.Ordinal)).ThenBy(x => x.Key, StringComparer.Ordinal))
        {
            int left = n;
            IEnumerable<string> ids = need.StartsWith("grade:", StringComparison.Ordinal) && int.TryParse(need[6..], out int g)
                ? have.Keys.Where(k => (r.Trophy(k)?.Grade ?? 0) >= g).OrderBy(k => r.Trophy(k)!.Grade).ThenBy(k => r.Trophy(k)!.Value).ThenBy(k => k, StringComparer.Ordinal).ToList()
                : [need];
            foreach (var id in ids)
            {
                int take = Math.Min(left, have.Get(id, 0));
                if (take <= 0) continue;
                have[id] = have.Get(id, 0) - take;
                use[id] = use.Get(id, 0) + take;
                left -= take;
                if (left == 0) break;
            }
            if (left > 0) return null;
        }
        return use;
    }

    /// <summary>Buildings in town that can make it.</summary>
    public static List<Building> Shops(Town town, RecipeDef rec) =>
        town.Buildings.Where(b => rec.At.Contains(b.Def.Id) && b.Complete && !b.Closed).OrderBy(b => b.Id).ToList();

    /// <summary>Why the town can't order it now, or null.</summary>
    public static string? Refuse(Town town, string recipeId)
    {
        var rec = Rules(town).Recipe(recipeId);
        if (rec == null || town.Content.Library.Item(rec.Ref) == null) return "no such work";
        if (Shops(town, rec).Count == 0) return $"needs a {Names(town, rec)}";
        if (town.City.Tier < rec.Tier) return $"needs a {town.Content.City.Tiers[Math.Min(rec.Tier, town.Content.City.Tiers.Count - 1)].Name.ToLowerInvariant()}";
        if (rec.Faction != "" && Factions.Find(town, rec.Faction) is not { Active: true }) return "needs a chapter in town";
        if (Parts(town, rec) == null) return "not enough parts";
        if (town.Treasury < Cost(town, rec)) return "not enough gold";
        return null;
    }

    /// <summary>What the work costs now: the recipe's gold, less what a relic in the hall saves.</summary>
    public static double Cost(Town town, RecipeDef rec) => Math.Ceiling(rec.Gold * (1 - Regalia.Craft(town)));

    /// <summary>"forge", "forge or mage tower".</summary>
    public static string Names(Town town, RecipeDef rec) =>
        string.Join(" or ", rec.At.Select(id => town.Content.Buildings.FirstOrDefault(b => b.Id == id)?.Name.ToLowerInvariant() ?? id));

    /// <summary>Order a piece: the gold and parts are paid now, and it comes to the armory when the work is done.</summary>
    public static CraftOrder? Craft(Town town, string recipeId)
    {
        if (Refuse(town, recipeId) != null) return null;
        var rec = Rules(town).Recipe(recipeId)!;
        var it = town.Content.Library.Item(rec.Ref)!;
        foreach (var (id, n) in Parts(town, rec)!) Take(town, id, n);
        double gold = Cost(town, rec);
        town.Book(TreasuryAccount.Armory, -gold);
        town.Armory.Spent += gold;
        // The shop with the shortest book takes it.
        var shop = Shops(town, rec).OrderBy(b => town.Spoils.Orders.Count(o => o.BuildingId == b.Id)).ThenBy(b => b.Id).First();
        int queued = town.Spoils.Orders.Where(o => o.BuildingId == shop.Id).Select(o => o.DueTick).DefaultIfEmpty(town.Tick).Max();
        var order = new CraftOrder { Id = town.Spoils.NextOrder++, RecipeId = rec.Id, BuildingId = shop.Id, DueTick = Math.Max(queued, town.Tick) + rec.Days * SimTime.HoursPerDay, Day = town.Day, Paid = gold };
        town.Spoils.Orders.Add(order);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The {shop.Name} takes an order for {Agent.Article(Gear.Plainly(it))} {Gear.Plainly(it)}, for {Js.Str(gold)} gold; it will be ready in {Math.Max(1, (order.DueTick - town.Tick) / SimTime.HoursPerDay)} days.");
        return order;
    }

    /// <summary>
    /// A rich hero orders a piece for themselves: the best the town's shops and parts can make that beats what they wear,
    /// within their purse. They pay the work and the parts' worth to the town; it goes to them when done.
    /// </summary>
    public static (string Item, double Cost, string Shop)? ForHero(Town town, Agent a, double purse)
    {
        if (town.Spoils.Orders.Any(o => o.ForId == a.Id)) return null;
        var pick = Rules(town).Recipes
            .Where(rec => rec.Faction == "" && town.City.Tier >= rec.Tier && Shops(town, rec).Count > 0 && town.Content.Library.Item(rec.Ref) is { } it && it.Tier <= town.Content.Loot.Ready(a.Hero!.Level) && Gear.Gain(town.Content, a, it) > 0)
            .Select(rec => (rec, parts: Parts(town, rec)))
            .Where(x => x.parts != null)
            .Select(x => (x.rec, x.parts, cost: Cost(town, x.rec) + x.parts!.Sum(p => Rules(town).Trophy(p.Key) is { } t ? Price(town, t) * p.Value : 0)))
            .Where(x => x.cost <= purse)
            .OrderByDescending(x => Gear.Gain(town.Content, a, town.Content.Library.Item(x.rec.Ref)!))
            .ThenBy(x => x.rec.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (pick.rec == null) return null;
        var item = town.Content.Library.Item(pick.rec.Ref)!;
        foreach (var (id, n) in pick.parts!) Take(town, id, n);
        double cost = Math.Ceiling(pick.cost);
        a.Gold -= cost;
        town.Book(TreasuryAccount.Patron, cost);
        var shop = Shops(town, pick.rec).OrderBy(b => town.Spoils.Orders.Count(o => o.BuildingId == b.Id)).ThenBy(b => b.Id).First();
        int queued = town.Spoils.Orders.Where(o => o.BuildingId == shop.Id).Select(o => o.DueTick).DefaultIfEmpty(town.Tick).Max();
        town.Spoils.Orders.Add(new CraftOrder { Id = town.Spoils.NextOrder++, RecipeId = pick.rec.Id, BuildingId = shop.Id, DueTick = Math.Max(queued, town.Tick) + pick.rec.Days * SimTime.HoursPerDay, Day = town.Day, ForId = a.Id });
        return (Gear.Plainly(item), cost, Shop(shop));
    }

    /// <summary>A shop's name to follow "the": "Artificers' Foundry" for "the Artificers' Foundry".</summary>
    private static string Shop(Building b) => b.Name.StartsWith("the ", StringComparison.OrdinalIgnoreCase) ? b.Name[4..] : b.Name;

    /// <summary>Call off an order: the gold comes back, the parts don't.</summary>
    public static bool Cancel(Town town, int orderId)
    {
        var o = town.Spoils.Orders.FirstOrDefault(x => x.Id == orderId);
        if (o == null || o.ForId != null || Rules(town).Recipe(o.RecipeId) is not { } rec) return false;
        town.Spoils.Orders.Remove(o);
        double paid = o.Paid > 0 ? o.Paid : rec.Gold;
        town.Book(TreasuryAccount.Armory, paid);
        town.Armory.Spent -= paid;
        return true;
    }

    public static void Daily(Town town, bool auto)
    {
        foreach (var o in town.Spoils.Orders.Where(o => o.DueTick <= town.Tick).ToList())
        {
            town.Spoils.Orders.Remove(o);
            if (Rules(town).Recipe(o.RecipeId) is not { } rec || town.Content.Library.Item(rec.Ref) is not LibItem it) continue;
            var shop = town.Buildings.FirstOrDefault(b => b.Id == o.BuildingId);
            if (o.ForId is int hid && town.Agent(hid) is { Dead: false, Hero: not null } hero)
            {
                town.Spoils.Crafted++;
                town.Log(LedgerCategory.Town, Importance.Routine, $"The {(shop != null ? Shop(shop) : "forge")} finishes {hero.Name}'s {Gear.Plainly(it)}.", [hero.Id]);
                Gear.Claim(town, hero, it);
                continue;
            }
            Armory.Add(town, it, $"made at the {shop?.Name ?? "forge"}");
            town.Spoils.Crafted++;
            town.Log(LedgerCategory.Town, Importance.Notable, $"The {shop?.Name ?? "forge"} finishes {Agent.Article(Gear.Plainly(it))} {Gear.Plainly(it)}. It waits in the armory.");
        }
        if (auto)
            foreach (var (id, n) in town.Spoils.Trophies.ToList())
                if (n > Rules(town).AutoKeep) Sell(town, id, n - Rules(town).AutoKeep);
    }
}
