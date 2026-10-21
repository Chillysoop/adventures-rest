namespace CrownAndCompass.Sim.Systems;

/// <summary>Rune-lit stone circles (explore.json "circles").</summary>
public sealed record CircleRules
{
    /// <summary>Lore a day each circle the town holds gives the library.</summary>
    public double Lore { get; init; } = 1.5;

    /// <summary>Extra share of favour a cult's rite brings its god while any circle stands unwatched.</summary>
    public double Rite { get; init; } = 0.5;

    /// <summary>Research funding that keeps the mages watching the stones.</summary>
    public int WardFunding { get; init; } = 1;

    /// <summary>Names for circles, picked by the find's id: "the Nine Maidens".</summary>
    public List<string> Names { get; init; } = [];

    /// <summary>The Ledger line when one is woken: {circle}, {town}.</summary>
    public string Claimed { get; init; } = "";
}

/// <summary>A stone circle a company found and woke for the town.</summary>
public sealed class StoneCircle
{
    public string Name { get; init; } = "";
    public int RegionId { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public int Day { get; init; }
}

/// <summary>
/// Rune-lit stone circles out in the wilds, found by explorers like any other find: the company reads the runes and
/// wakes them, and the town holds the circle from then on. The library's mages draw lore from every circle the town
/// holds; the cults in town use the same stones for their rites, which bring their gods more favour unless the
/// council funds the mages to keep watch on them. No dice: names come from the find.
/// </summary>
public static class Circles
{
    public static CircleRules Rules(Town town) => town.Content.Explore.Circles;

    /// <summary>The circles' lore for the library, with the words for it.</summary>
    public static (string Why, double Lore) Lore(Town town)
    {
        int n = town.Circles.Count;
        return n == 0 ? ("", 0) : (n == 1 ? town.Circles[0].Name : $"{n} stone circles", n * Rules(town).Lore);
    }

    /// <summary>The mages keep watch on the stones while the council funds their research.</summary>
    public static bool Warded(Town town) => town.Research.Funding >= Rules(town).WardFunding;

    /// <summary>What a cult's rite is worth at the stones: more while any circle stands unwatched.</summary>
    public static double RiteFactor(Town town) => town.Circles.Count > 0 && !Warded(town) ? 1 + Rules(town).Rite : 1;

    /// <summary>The company woke a circle for the town.</summary>
    public static string Claim(Town town, Find f)
    {
        var r = Rules(town);
        string name = r.Names.Count == 0 ? "a stone circle" : r.Names[(f.Id + town.Circles.Count) % r.Names.Count];
        if (town.Circles.Any(c => c.Name == name)) name = $"{name} of {town.Regions[f.RegionId].Name}";
        town.Circles.Add(new StoneCircle { Name = name, RegionId = f.RegionId, X = f.X, Y = f.Y, Day = town.Day });
        return " " + r.Claimed.Replace("{circle}", name).Replace("{town}", town.Name);
    }

    /// <summary>A rite held at the stones, for the faction's line.</summary>
    public static string? RiteAt(Town town) => town.Circles.Count > 0 && !Warded(town) ? town.Circles[0].Name : null;
}
