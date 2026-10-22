namespace CrownAndCompass.Sim.Systems;

/// <summary>How plain arms help the watch and the army (enchantments.json "arms").</summary>
public sealed record KitRules
{
    /// <summary>Strength each point of bonus per man adds, as a share.</summary>
    public double PerPoint { get; init; } = 0.12;

    /// <summary>Pieces one man carries: a weapon and armour (or a shield).</summary>
    public int PerMan { get; init; } = 2;
}

/// <summary>
/// Plain +1/+2/+3 arms issued from the armory to the watch or a regiment. Each point of bonus per man (a man carries
/// at most a weapon and armour or a shield) makes the unit that much stronger: the watch at the walls and on the
/// streets, a regiment wherever it is posted. Recalled arms go back to the armory; a disbanded regiment hands its in.
/// </summary>
public static class Kit
{
    public const string Watch = "watch";

    public static string Regiment(int id) => $"regiment:{id}";

    private static KitRules Rules(Town town) => town.Content.Enchant.Arms;

    /// <summary>Whether the watch or a soldier can carry the piece: plain arms, a weapon, armour or a shield.</summary>
    public static bool Fits(Town town, LibItem it) => Enchant.Basic(town.Content, it) && Gear.SlotOf(it) is "weapon" or "armor" or "shield";

    public static IEnumerable<StockItem> Of(Town town, string unit) => town.Armory.Issued.Where(s => s.Unit == unit);

    /// <summary>Men in a unit: watchmen, or a regiment's soldiers.</summary>
    public static int Men(Town town, string unit) =>
        unit == Watch ? town.Watch
        : unit.StartsWith("regiment:", StringComparison.Ordinal) && int.TryParse(unit[9..], out int id) && Army.Find(town, id) is { } r ? r.Soldiers.Count : 0;

    /// <summary>Bonus points per man the unit's arms give, counting no more pieces than its men can carry.</summary>
    public static double PerMan(Town town, string unit)
    {
        int men = Men(town, unit);
        if (men <= 0) return 0;
        var pieces = Of(town, unit).Select(s => Gear.BonusOf(town.Content, s.Item)).OrderByDescending(b => b).Take(men * Rules(town).PerMan);
        return pieces.Sum() / (double)men;
    }

    /// <summary>How much stronger its arms make the unit.</summary>
    public static double Factor(Town town, string unit) => 1 + Rules(town).PerPoint * PerMan(town, unit);

    public static double WatchFactor(Town town) => town.Armory.Issued.Count == 0 ? 1 : Factor(town, Watch);

    public static double RegimentFactor(Town town, Regiment r) => town.Armory.Issued.Count == 0 ? 1 : Factor(town, Regiment(r.Id));

    /// <summary>Why a piece can't go to the unit, or null.</summary>
    public static string? Refuse(Town town, StockItem s, string unit)
    {
        if (!Fits(town, s.Item)) return "only plain arms";
        if (unit != Watch && Men(town, unit) == 0 && !(unit.StartsWith("regiment:", StringComparison.Ordinal) && int.TryParse(unit[9..], out int id) && Army.Find(town, id) != null)) return "no such unit";
        if (Men(town, unit) == 0) return "nobody to carry it";
        if (Of(town, unit).Count() >= Men(town, unit) * Rules(town).PerMan) return "all armed";
        return null;
    }

    /// <summary>Issue a piece from the armory to the watch or a regiment.</summary>
    public static bool Issue(Town town, int stockId, string unit)
    {
        var s = town.Armory.Stock.FirstOrDefault(x => x.Id == stockId);
        if (s == null || Refuse(town, s, unit) != null) return false;
        town.Armory.Stock.Remove(s);
        s.Unit = unit;
        town.Armory.Issued.Add(s);
        town.Log(LedgerCategory.War, Importance.Routine, $"The armory issues {Agent.Article(Gear.Plainly(s.Item))} {Gear.Plainly(s.Item)} to {Name(town, unit)}.");
        return true;
    }

    /// <summary>Issue every plain piece the armory holds to the unit, best first, until each man carries his share.</summary>
    public static int Equip(Town town, string unit)
    {
        int n = 0;
        foreach (var s in town.Armory.Stock.Where(x => Fits(town, x.Item)).OrderByDescending(x => Gear.BonusOf(town.Content, x.Item)).ThenBy(x => x.Id).ToList())
        {
            if (Refuse(town, s, unit) != null) break;
            if (Issue(town, s.Id, unit)) n++;
        }
        return n;
    }

    /// <summary>Take a piece back into the armory.</summary>
    public static bool Recall(Town town, int stockId)
    {
        var s = town.Armory.Issued.FirstOrDefault(x => x.Id == stockId);
        if (s == null) return false;
        town.Armory.Issued.Remove(s);
        s.Unit = "";
        town.Armory.Stock.Add(s);
        return true;
    }

    /// <summary>A regiment stood down hands its arms back.</summary>
    public static void Disbanded(Town town, int regimentId)
    {
        foreach (var s in Of(town, Regiment(regimentId)).ToList()) Recall(town, s.Id);
    }

    /// <summary>"the watch", "the Second Pikes".</summary>
    public static string Name(Town town, string unit) =>
        unit == Watch ? "the watch"
        : unit.StartsWith("regiment:", StringComparison.Ordinal) && int.TryParse(unit[9..], out int id) && Army.Find(town, id) is { } r ? $"the {r.Name}" : unit;
}
