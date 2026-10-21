namespace CrownAndCompass.Sim.Systems;

/// <summary>How well a campaign was won: by the day it ended, and the boon rank it earns.</summary>
public sealed record GradeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Won on or before this day.</summary>
    public int By { get; init; }
    public int Rank { get; init; } = 1;
}

/// <summary>One boon a won campaign leaves for the next (content/game/boons.json).</summary>
public sealed record CabinetBoonDef
{
    public string Id { get; init; } = "";

    /// <summary>The road whose win earns it; empty for any road won at <see cref="Grade"/> or better.</summary>
    public string Road { get; init; } = "";
    public string Grade { get; init; } = "";

    /// <summary>military, economy, tech, adventuring or diplomacy.</summary>
    public string Kind { get; init; } = "";
    public string Icon { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public double PerRank { get; init; } = 1;
}

public sealed record BoonRules
{
    public List<GradeDef> Grades { get; init; } = [];
    public List<CabinetBoonDef> Boons { get; init; } = [];
}

/// <summary>
/// The cabinet: boons a player earned by winning earlier campaigns, which they may switch on when founding a
/// town. The client keeps the cabinet in the profile and passes the chosen ones in <see cref="SimulationOptions.Boons"/>;
/// the town keeps what it was founded with. Each does a little at the founding (a knight levels higher, more
/// watchmen, gold, lore, standing, charted regions) or lasts (trade routes).
/// </summary>
public static class Boons
{
    public const int MaxRank = 3;

    public static BoonRules Rules(Content content) => content.Boons;

    public static CabinetBoonDef? Def(Content content, string id) => Rules(content).Boons.FirstOrDefault(b => b.Id == id);

    /// <summary>The rank this town was founded with, 0 if none.</summary>
    public static int Rank(Town town, string id) => town.Boons.Get(id, 0);

    /// <summary>What the boon gives at the town's rank.</summary>
    public static double Value(Town town, string id) => Def(town.Content, id) is { } d ? d.PerRank * Rank(town, id) : 0;

    /// <summary>The boon's line, at a rank ("+300 gold").</summary>
    public static string Text(CabinetBoonDef d, int rank) => d.Text.Replace("{n}", Js.Str(d.PerRank * rank));

    /// <summary>The grade a win on this day earns.</summary>
    public static GradeDef Grade(Content content, int day) =>
        Rules(content).Grades.OrderBy(g => g.By).FirstOrDefault(g => day <= g.By) ?? new GradeDef { Id = "bronze", Name = "Bronze", Rank = 1 };

    /// <summary>The boons a won campaign earns, with their ranks; empty unless it was won.</summary>
    public static List<(CabinetBoonDef Boon, int Rank)> Earned(Town town)
    {
        if (Victory.Won(town) is not { } road) return [];
        var g = Grade(town.Content, town.Day);
        var gradeRank = Rules(town.Content).Grades.ToDictionary(x => x.Id, x => x.Rank);
        return Rules(town.Content).Boons
            .Where(b => b.Road == road.Id || (b.Road == "" && gradeRank.GetValueOrDefault(b.Grade, 99) <= g.Rank))
            .Select(b => (b, g.Rank))
            .ToList();
    }

    /// <summary>Keep the boons the town is founded with ("id:rank"), each once, at most at the top rank.</summary>
    public static void Choose(Town town, IEnumerable<string> picks)
    {
        foreach (var p in picks.OrderBy(p => p, StringComparer.Ordinal))
        {
            var parts = p.Split(':');
            if (Def(town.Content, parts[0]) is null) continue;
            int rank = parts.Length > 1 && int.TryParse(parts[1], out int r) ? Math.Clamp(r, 1, MaxRank) : 1;
            town.Boons[parts[0]] = Math.Max(rank, town.Boons.Get(parts[0], 0));
        }
    }

    /// <summary>At the founding, after the charter: what each chosen boon gives the new town.</summary>
    public static void Found(Town town)
    {
        if (town.Boons.Count == 0) return;
        town.Watch += (int)Value(town, "old-banners");
        town.Treasury += Value(town, "golden-coffers");
        town.Research.Notes += Value(town, "old-lore");
        if (town.Heroes.FirstOrDefault(h => h.Hero!.Sworn) is { } knight) knight.Hero!.Renown += (int)Value(town, "famous-name");
        foreach (var s in town.Settlements) s.Standing = Math.Clamp(s.Standing + Value(town, "old-friends"), -100, 100);
        foreach (var r in town.Regions.Where(r => !r.Known).OrderBy(r => r.Ring).ThenBy(r => r.Id).Take((int)Value(town, "guild-maps"))) r.Known = true;
        var names = town.Boons.Select(kv => Def(town.Content, kv.Key)!.Name).ToList();
        town.Log(LedgerCategory.Town, Importance.Notable, $"The founders bring what earlier towns left them: {Expedition.ListNames(names)}.");
    }

    /// <summary>Trade routes the merchant's charter adds to the charter's.</summary>
    public static int Routes(Town town) => (int)Value(town, "merchant-charter");

    /// <summary>Levels the veteran knight starts above the charter's.</summary>
    public static int KnightLevels(Town town) => (int)Value(town, "veterans");
}
