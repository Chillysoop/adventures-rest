namespace CrownAndCompass.Sim.Systems;

/// <summary>A quality an ingredient lends a brew: mending, vigour, ember, rime, gloom, purity.</summary>
public sealed record AlchemyPropertyDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";

    /// <summary>Hex colour the client draws it in.</summary>
    public string Color { get; init; } = "efe6d2";
}

/// <summary>Something an alchemist can put in the pot: a herb from the town's beds, or a part cut from a monster.</summary>
public sealed record AlchemyIngredientDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>Grown in the herb beds: each measure takes <see cref="AlchemyRules.Herbs"/> of the market's herbs.</summary>
    public bool Herb { get; init; }

    /// <summary>Library creature types whose bodies yield it, carried home by the company that cleared their lair.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];

    /// <summary>What it lends a brew, by property id.</summary>
    public IReadOnlyDictionary<string, int> Props { get; init; } = new Dictionary<string, int>();
}

/// <summary>A brew the ingredients can make: the least of each property it needs, and what it is good for.</summary>
public sealed record AlchemyRecipeDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string Blurb { get; init; } = "";
    public IReadOnlyDictionary<string, int> Needs { get; init; } = new Dictionary<string, int>();

    /// <summary>healing (heroes setting out carry more draughts), ward (harder to hit at sites of the kinds in <see cref="Against"/>), cure (saves a townsperson from a plague-bringer) or sell.</summary>
    public string Use { get; init; } = "sell";
    public IReadOnlyList<string> Against { get; init; } = [];

    /// <summary>Doses a batch makes, and the gold each fetches sold.</summary>
    public int Doses { get; init; } = 2;
    public double Value { get; init; } = 10;

    public int Strength => Needs.Values.Sum();
}

/// <summary>The alchemy bench (content/game/alchemy.json).</summary>
public sealed record AlchemyRules
{
    /// <summary>Buildings that hold a bench, and the batches each brews a day.</summary>
    public IReadOnlyList<string> Benches { get; init; } = [];
    public int PerBench { get; init; } = 1;

    /// <summary>Most ingredients in one batch (the least is two).</summary>
    public int Most { get; init; } = 3;

    /// <summary>Herbs from the market one measure of a herb ingredient takes.</summary>
    public double Herbs { get; init; } = 2;

    /// <summary>Measures of a part a cleared lair yields, and more for each tier; the most of one part kept.</summary>
    public int Parts { get; init; } = 2;
    public double PartsPerTier { get; init; } = 0.34;
    public int PartCap { get; init; } = 12;

    /// <summary>Healing draughts a hero setting out takes from the town's shelf over what they bought.</summary>
    public int Carry { get; init; } = 1;

    /// <summary>Armour class a ward draught adds at a site of its kind.</summary>
    public int WardAc { get; init; } = 1;

    /// <summary>The stand-in mayor brews a wanted potion while the shelf holds fewer doses than this, and sells past the second number.</summary>
    public int Keep { get; init; } = 6;
    public int SellOver { get; init; } = 12;

    /// <summary>How a new brew is named, in turn: {town}, {brewer}, {potion}.</summary>
    public IReadOnlyList<string> Names { get; init; } = ["{town} {potion}"];

    public IReadOnlyList<AlchemyPropertyDef> Properties { get; init; } = [];
    public IReadOnlyList<AlchemyIngredientDef> Ingredients { get; init; } = [];
    public IReadOnlyList<AlchemyRecipeDef> Recipes { get; init; } = [];

    public AlchemyIngredientDef? Ingredient(string id) => Ingredients.FirstOrDefault(i => i.Id == id);

    public AlchemyRecipeDef? Recipe(string id) => Recipes.FirstOrDefault(r => r.Id == id);
}

/// <summary>A page of the town's recipe book: a brew it has made, the name it gave it, and how.</summary>
public sealed class RecipeEntry
{
    public required string RecipeId { get; init; }
    public required string Name { get; init; }
    public int Day { get; init; }
    public string Brewer { get; init; } = "";

    /// <summary>The ingredients of the first batch, by id.</summary>
    public List<string> Ingredients { get; init; } = [];
    public int Batches { get; set; }
}

public sealed class AlchemyState
{
    /// <summary>Monster parts in the alchemists' stores, by ingredient id.</summary>
    public OrderedMap<string, int> Parts { get; } = new();

    /// <summary>Doses on the town's shelf, by recipe id.</summary>
    public OrderedMap<string, int> Shelf { get; } = new();

    /// <summary>The town's recipe book, in the order it found them.</summary>
    public List<RecipeEntry> Book { get; } = [];

    public int Day { get; set; } = -1;
    public int BrewedToday { get; set; }

    public int Batches { get; set; }
    public int Spoiled { get; set; }
    public int Warded { get; set; }
    public int Cured { get; set; }
    public double Sold { get; set; }
}

/// <summary>
/// The alchemy bench: the town's apothecaries (and the Alchemists' Quarter, and the mage tower) brew
/// potions from two or three ingredients, herbs from the beds and the parts companies cut from what they
/// slew. Each ingredient lends a brew a little of a property (mending, vigour, ember, rime, gloom, purity);
/// a mix that reaches what a recipe needs makes it (the most demanding recipe it reaches), and the first
/// batch of a recipe gets a name in the town's book. Doses go on the town's shelf: healing draughts go out
/// with heroes setting out, a ward makes them harder to hit at a site of its kind, a fever-breaker pulls a
/// townsperson back from a plague-bringer's night visit, and any of it sells.
/// </summary>
public static class Alchemy
{
    private static AlchemyRules Rules(Town town) => town.Content.Alchemy;

    /// <summary>The buildings that hold a bench, finished and open, by id.</summary>
    public static List<Building> Benches(Town town)
    {
        var r = Rules(town);
        return town.Buildings.Where(b => r.Benches.Contains(b.Def.Id) && b.Complete && !b.Closed).OrderBy(b => b.Id).ToList();
    }

    /// <summary>Batches the benches can still brew today.</summary>
    public static int Left(Town town) => Benches(town).Count * Rules(town).PerBench - (town.Alchemy.Day == town.Day ? town.Alchemy.BrewedToday : 0);

    /// <summary>Measures of an ingredient to hand: herbs from the market's stock, parts from the alchemists' stores.</summary>
    public static int Stock(Town town, AlchemyIngredientDef i) =>
        i.Herb ? (int)Math.Floor(town.Market.Stock(Good.Herbs) / Rules(town).Herbs) : town.Alchemy.Parts.Get(i.Id, 0);

    /// <summary>What a mix lends a brew, in the properties' order.</summary>
    public static OrderedMap<string, int> Mix(Town town, IReadOnlyList<string> ids)
    {
        var r = Rules(town);
        var sum = new OrderedMap<string, int>();
        foreach (var p in r.Properties)
        {
            int n = ids.Sum(id => r.Ingredient(id)?.Props.GetValueOrDefault(p.Id, 0) ?? 0);
            if (n > 0) sum[p.Id] = n;
        }
        return sum;
    }

    /// <summary>The recipe a mix makes: the most demanding one whose needs it meets (the earlier on a tie), or null for a spoiled batch.</summary>
    public static AlchemyRecipeDef? Match(Town town, IReadOnlyList<string> ids)
    {
        var mix = Mix(town, ids);
        AlchemyRecipeDef? best = null;
        foreach (var rec in Rules(town).Recipes)
        {
            if (!rec.Needs.All(n => mix.Get(n.Key, 0) >= n.Value)) continue;
            if (best == null || rec.Strength > best.Strength) best = rec;
        }
        return best;
    }

    /// <summary>The herbs a mix takes from the market, and what they cost.</summary>
    public static (double Qty, double Cost) Herbs(Town town, IReadOnlyList<string> ids)
    {
        double qty = ids.Count(id => Rules(town).Ingredient(id)?.Herb == true) * Rules(town).Herbs;
        return (qty, Math.Ceiling(qty * town.Market.Price(Good.Herbs)));
    }

    /// <summary>The town's name for a recipe it has brewed, or null.</summary>
    public static RecipeEntry? Known(Town town, string recipeId) => town.Alchemy.Book.FirstOrDefault(e => e.RecipeId == recipeId);

    public static string NameOf(Town town, AlchemyRecipeDef rec) => Known(town, rec.Id)?.Name ?? rec.Name;

    /// <summary>"the Briarford Draught of Mending", "the Briarford Huntsman's Musk", but just "Sabeth's Gravelight Tonic".</summary>
    public static string The(Town town, string name) => name.StartsWith(town.Name + " ", StringComparison.Ordinal) || !name.Split(' ')[0].EndsWith("'s", StringComparison.Ordinal) ? $"the {name}" : name;

    /// <summary>Why the bench can't brew this mix now, or null.</summary>
    public static string? Refuse(Town town, IReadOnlyList<string> ids)
    {
        var r = Rules(town);
        if (Benches(town).Count == 0) return "the town has no apothecary";
        if (Left(town) <= 0) return "the benches have brewed all they can today";
        if (ids.Count < 2 || ids.Count > r.Most) return $"a batch takes two to {r.Most} ingredients";
        foreach (var g in ids.GroupBy(id => id).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var i = r.Ingredient(g.Key);
            if (i == null) return "no such ingredient";
            if (Stock(town, i) < g.Count()) return $"not enough {i.Name}";
        }
        var (herbs, cost) = Herbs(town, ids);
        // The herb ingredients all come out of the market's one stock of herbs.
        if (herbs > town.Market.Stock(Good.Herbs)) return "not enough herbs in the market";
        if (cost > town.Treasury) return "the treasury can't pay for the herbs";
        return null;
    }

    /// <summary>The alchemist who works the bench: an institution's keeper, or the longest-serving worker at the first bench with one.</summary>
    private static (string Name, string Given, int? Id)? Brewer(Town town)
    {
        foreach (var b in Benches(town))
        {
            if (Institutions.At(town, b) is { } s) return (Institutions.KeeperName(town, s), Institutions.KeeperGiven(town, s), null);
            if (b.Workers.OrderBy(id => id).FirstOrDefault() is int id and > 0) return (town.Agent(id).Name, town.Agent(id).GivenName, id);
        }
        return null;
    }

    private static string Listed(IReadOnlyList<string> words) =>
        words.Count <= 1 ? string.Join("", words) : $"{string.Join(", ", words.Take(words.Count - 1))} and {words[^1]}";

    private static readonly string[] Counts = ["", "", "two", "three"];

    /// <summary>"hearthmint and grave dust", "two measures of hearthmint".</summary>
    public static string What(Town town, IReadOnlyList<string> ids) =>
        Listed(ids.GroupBy(id => id).Select(g => (Name: Rules(town).Ingredient(g.Key)?.Name ?? g.Key, N: g.Count()))
            .Select(x => x.N == 1 ? x.Name : $"{(x.N < Counts.Length ? Counts[x.N] : Js.Str(x.N))} measures of {x.Name}").ToList());

    /// <summary>Brew a batch from two or three ingredients. A mix that makes no recipe spoils, and what went in is lost.</summary>
    public static bool Brew(Town town, IReadOnlyList<string> ids)
    {
        if (Refuse(town, ids) != null) return false;
        var r = Rules(town);
        var a = town.Alchemy;
        if (a.Day != town.Day)
        {
            a.Day = town.Day;
            a.BrewedToday = 0;
        }
        a.BrewedToday++;
        var (herbs, cost) = Herbs(town, ids);
        if (herbs > 0)
        {
            town.Market.Take(Good.Herbs, herbs);
            town.Book(TreasuryAccount.Alchemy, -cost);
        }
        foreach (var id in ids)
            if (r.Ingredient(id) is { Herb: false }) a.Parts[id] = a.Parts.Get(id, 0) - 1;
        var brewer = Brewer(town);
        string who = brewer?.Name ?? "The town's alchemist";
        IReadOnlyList<int>? tag = brewer?.Id is int bid ? [bid] : null;
        string what = What(town, ids);
        var rec = Match(town, ids);
        if (rec == null)
        {
            a.Spoiled++;
            town.Log(LedgerCategory.Town, Importance.Routine, $"{who} boils {what} together, and gets a grey sludge that has to be poured away.", tag);
            return true;
        }
        a.Batches++;
        a.Shelf[rec.Id] = a.Shelf.Get(rec.Id, 0) + rec.Doses;
        if (Known(town, rec.Id) is { } known)
        {
            known.Batches++;
            town.Log(LedgerCategory.Town, Importance.Routine, $"{who} brews {rec.Doses} doses of {The(town, known.Name)}.", tag);
            return true;
        }
        string pattern = r.Names.Count == 0 ? "{town} {potion}" : r.Names[a.Book.Count % r.Names.Count];
        if (brewer == null && pattern.Contains("{brewer}")) pattern = "{town} {potion}";
        string name = pattern.Replace("{town}", town.Name).Replace("{brewer}", brewer?.Given ?? "").Replace("{potion}", rec.Name);
        a.Book.Add(new RecipeEntry { RecipeId = rec.Id, Name = name, Day = town.Day, Brewer = brewer?.Name ?? "", Ingredients = ids.ToList(), Batches = 1 });
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{who} steeps {what} and the pot turns clear: something new. The town writes it in its book as {The(town, name)}. {rec.Blurb}", tag);
        return true;
    }

    /// <summary>Sell every dose of a brew on the shelf to the merchants.</summary>
    public static bool Sell(Town town, string recipeId)
    {
        int doses = town.Alchemy.Shelf.Get(recipeId, 0);
        if (doses <= 0 || Rules(town).Recipe(recipeId) is not { } rec) return false;
        double gold = doses * rec.Value;
        town.Alchemy.Shelf.Remove(recipeId);
        town.Alchemy.Sold += gold;
        town.Book(TreasuryAccount.Alchemy, gold);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The town hall sells {doses} doses of {The(town, NameOf(town, rec))} to the merchants for {Js.Str(gold)} gold.");
        return true;
    }

    // Parts from the wilds --------------------------------------------------------------------------

    /// <summary>The library creature type of a site's master ("undead", "beast"), or "".</summary>
    private static string BossType(Town town, SiteDef site)
    {
        if (site.Boss.Monster == "") return "";
        MonsterDef m;
        try { m = town.Content.Monster(site.Boss.Monster); }
        catch (KeyNotFoundException) { return ""; }
        string key = m.Ref != "" ? m.Ref : m.Id.EndsWith("@boss", StringComparison.Ordinal) ? m.Id[..^5] : m.Id;
        return town.Content.Library.Monster(key)?.Type ?? "";
    }

    /// <summary>The part a site's master yields, or null (people yield nothing for the pot).</summary>
    public static AlchemyIngredientDef? PartOf(Town town, SiteDef site)
    {
        string type = BossType(town, site);
        return type == "" ? null : Rules(town).Ingredients.FirstOrDefault(i => !i.Herb && i.Types.Contains(type));
    }

    /// <summary>A company home: from a cleared lair they bring the alchemists what they cut from its master, and the wards they drank are spent.</summary>
    public static void Homecoming(Town town, Party party, SiteDef site, ExpeditionOutcome outcome, IReadOnlyList<Agent> members)
    {
        foreach (var m in members) m.Hero!.Ward = null;
        if (outcome != ExpeditionOutcome.Cleared || !members.Any(m => !m.Dead) || Benches(town).Count == 0) return;
        if (PartOf(town, site) is not { } part) return;
        var r = Rules(town);
        int had = town.Alchemy.Parts.Get(part.Id, 0);
        int n = Math.Min(r.PartCap - had, r.Parts + (int)Math.Floor(site.Tier * r.PartsPerTier));
        if (n <= 0) return;
        town.Alchemy.Parts[part.Id] = had + n;
        town.Log(LedgerCategory.Town, Importance.Routine, $"The {party.Name} sell the town's alchemists {n} measures of {part.Name} from {site.Name}.");
    }

    // Potions at work -----------------------------------------------------------------------------

    private static bool Take(Town town, AlchemyRecipeDef rec)
    {
        int n = town.Alchemy.Shelf.Get(rec.Id, 0);
        if (n <= 0) return false;
        if (n == 1) town.Alchemy.Shelf.Remove(rec.Id);
        else town.Alchemy.Shelf[rec.Id] = n - 1;
        return true;
    }

    /// <summary>Heroes setting out for a site take healing draughts from the shelf, and a ward for its kind if there is one.</summary>
    public static void Outfit(Town town, IReadOnlyList<Agent> members, SiteDef site)
    {
        var r = Rules(town);
        int carry = town.Content.Shrines.Potion.Carry + r.Carry;
        int healed = 0;
        var heals = r.Recipes.Where(x => x.Use == "healing").ToList();
        foreach (var m in members.OrderBy(m => m.Id))
        {
            var s = m.Hero!;
            s.Ward = null;
            while (s.Potions < carry && heals.FirstOrDefault(x => town.Alchemy.Shelf.Get(x.Id, 0) > 0) is { } h && Take(town, h))
            {
                s.Potions++;
                healed++;
            }
        }
        var ward = site.Kind == "" ? null : r.Recipes.FirstOrDefault(x => x.Use == "ward" && x.Against.Contains(site.Kind) && town.Alchemy.Shelf.Get(x.Id, 0) > 0);
        var drank = new List<Agent>();
        if (ward != null)
            foreach (var m in members.OrderBy(m => m.Id))
                if (Take(town, ward))
                {
                    m.Hero!.Ward = site.Kind;
                    drank.Add(m);
                }
        town.Alchemy.Warded += drank.Count;
        if (drank.Count > 0)
            town.Log(LedgerCategory.Quest, Importance.Routine,
                $"{Listed(drank.Select(d => d.GivenName).ToList())} {(drank.Count == 1 ? "takes" : "take")} {The(town, NameOf(town, ward!))} from the town's shelf for {site.Name}{(healed > 0 ? $", with {healed} healing {(healed == 1 ? "draught" : "draughts")}" : "")}.",
                drank.Select(d => d.Id).ToList());
    }

    /// <summary>Armour class a hero's ward lends them at this site.</summary>
    public static int WardAc(Content content, HeroSheet s, SiteDef? site) => site != null && site.Kind != "" && s.Ward == site.Kind ? content.Alchemy.WardAc : 0;

    /// <summary>A plague-bringer's night visit: a fever-breaker from the shelf pulls the victim back. Returns the brew's name, or null.</summary>
    public static string? Cure(Town town)
    {
        var rec = Rules(town).Recipes.FirstOrDefault(x => x.Use == "cure" && town.Alchemy.Shelf.Get(x.Id, 0) > 0);
        if (rec == null || !Take(town, rec)) return null;
        town.Alchemy.Cured++;
        return The(town, NameOf(town, rec));
    }

    // The stand-in mayor --------------------------------------------------------------------------

    /// <summary>The cheapest mix to hand that makes this recipe: fewest herbs, then fewest ingredients, then the first found.</summary>
    public static List<string>? Find(Town town, AlchemyRecipeDef rec) =>
        Mixes(town).TryGetValue(rec.Id, out var mixes) ? mixes.FirstOrDefault(ids => Refuse(town, ids) == null)?.ToList() : null;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<AlchemyRules, Dictionary<string, List<string[]>>> _mixes = new();

    /// <summary>Every mix of two or three ingredients, by the recipe it makes, cheapest first (fewest herbs, then fewest ingredients); worked out once for the content.</summary>
    private static Dictionary<string, List<string[]>> Mixes(Town town) => _mixes.GetValue(Rules(town), r =>
    {
        var ids = r.Ingredients.Select(i => i.Id).ToList();
        var all = new List<string[]>();
        for (int i = 0; i < ids.Count; i++)
            for (int j = i; j < ids.Count; j++)
            {
                all.Add([ids[i], ids[j]]);
                if (r.Most < 3) continue;
                for (int k = j; k < ids.Count; k++) all.Add([ids[i], ids[j], ids[k]]);
            }
        return all
            .Select(m => (m, rec: Match(town, m)))
            .Where(x => x.rec != null)
            .OrderBy(x => x.m.Count(id => r.Ingredient(id)!.Herb))
            .ThenBy(x => x.m.Length)
            .GroupBy(x => x.rec!.Id)
            .ToDictionary(g => g.Key, g => g.Select(x => x.m).ToList());
    });

    /// <summary>What the stand-in mayor keeps on the shelf, most wanted first: healing, wards for the lairs the town knows, fever-breakers while a plague-bringer lives or sickness is in town.</summary>
    private static List<AlchemyRecipeDef> Wanted(Town town)
    {
        var r = Rules(town);
        var kinds = town.Sites.Where(s => !s.Cleared && s.Known != SiteKnowledge.Unheard).Select(s => s.Def.Kind).ToHashSet();
        bool plague = Menaces.Alive(town).Any(m => Menaces.AmbitionOf(town, m).Id == "plague") || Sickness.Outbreak(town);
        var list = r.Recipes.Where(x => x.Use == "healing").ToList();
        list.AddRange(r.Recipes.Where(x => x.Use == "ward" && x.Against.Any(kinds.Contains)));
        if (plague) list.AddRange(r.Recipes.Where(x => x.Use == "cure"));
        return list;
    }

    public static void Daily(Town town, bool auto)
    {
        var a = town.Alchemy;
        if (a.Day != town.Day)
        {
            a.Day = town.Day;
            a.BrewedToday = 0;
        }
        if (!auto) return;
        var r = Rules(town);
        foreach (var rec in Wanted(town))
        {
            if (Left(town) <= 0) break;
            if (a.Shelf.Get(rec.Id, 0) >= r.Keep) continue;
            if (Find(town, rec) is { } ids) Brew(town, ids);
        }
        foreach (var rec in r.Recipes.Where(x => a.Shelf.Get(x.Id, 0) > r.SellOver).ToList()) Sell(town, rec.Id);
    }
}
