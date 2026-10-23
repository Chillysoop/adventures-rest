namespace CrownAndCompass.Sim.Systems;

/// <summary>A ferry the town keeps across the river: a crossing like any ford or bridge, for whoever comes to it, friend or foe.</summary>
public sealed class Ferry
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public double X { get; init; }
    public double Y { get; init; }
    public int Day { get; init; }

    /// <summary>The rope cut and the raft sunk: no crossing here until the town puts it back.</summary>
    public bool Cut { get; set; }
}

/// <summary>passage.json `ferries`.</summary>
public sealed record FerryRules
{
    public double Cost { get; init; } = 220;
    public double Rebuild { get; init; } = 120;

    /// <summary>A ferry may go where the fixed crossings lie at least `gap` days apart, no further than `reach` days up or down from the town.</summary>
    public double Gap { get; init; } = 1.6;
    public double Reach { get; init; } = 9;

    /// <summary>Daily chance raiders from a live lair of `cutters` kinds within `near` days cut it.</summary>
    public double CutChance { get; init; } = 0.01;
    public double Near { get; init; } = 1.5;
    public IReadOnlyList<string> Cutters { get; init; } = ["tribe", "rivals"];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Texts { get; init; } = [];
}

/// <summary>
/// Ferries over the great river (<see cref="Passage.River"/>). The player puts one across a long reach between fixed
/// crossings, and every road uses it: the town's companies and caravans, and the enemy's hosts. Raiders cut them now and
/// then; the town can cut its own to deny the river to a host, and put them back. Rolls go by hash, not the town's dice.
/// </summary>
public static class Ferries
{
    public static FerryRules Rules(Town town) => town.Content.Passage.Ferries;

    /// <summary>Where a ferry could go: the middle of each long reach between the river's fixed crossings, near enough to matter.</summary>
    public static List<(double X, double Y)> Spots(Town town)
    {
        var r = Rules(town);
        var line = Passage.River(town);
        var fixedYs = Passage.Fixed(town).Select(c => c.Y).Concat([line[0].Y, line[^1].Y]).Order().ToList();
        var spots = new List<(double X, double Y)>();
        for (int i = 0; i + 1 < fixedYs.Count; i++)
        {
            if (fixedYs[i + 1] - fixedYs[i] < r.Gap) continue;
            double y = (fixedYs[i] + fixedYs[i + 1]) / 2;
            if (Math.Abs(y) > r.Reach) continue;
            var p = line.OrderBy(q => Math.Abs(q.Y - y)).First();
            if (town.Ferries.Any(f => Math.Abs(f.Y - p.Y) < r.Gap / 2)) continue;
            spots.Add(p);
        }
        return spots;
    }

    public static string? BuildBlocked(Town town, int spot)
    {
        if (spot < 0 || spot >= Spots(town).Count) return "no reach of the river there wants a ferry";
        if (town.Treasury < Rules(town).Cost) return $"needs {Js.Str(Rules(town).Cost)} gold";
        return null;
    }

    public static Ferry? Build(Town town, int spot)
    {
        if (BuildBlocked(town, spot) != null) return null;
        var r = Rules(town);
        var p = Spots(town)[spot];
        town.Book(TreasuryAccount.Works, -r.Cost);
        int id = town.Ferries.Count;
        var words = town.Content.Passage.River.Words;
        string word = words.Count > 0 ? words[(int)(Voices.Hash("ferry-word", town.Seed, id) % (uint)words.Count)] : "Old";
        string name = r.Names.Count > 0 ? r.Names[(int)(Voices.Hash("ferry", town.Seed, id) % (uint)r.Names.Count)].Replace("{A}", word, StringComparison.Ordinal) : "the ferry";
        var f = new Ferry { Id = id, Name = name, X = p.X, Y = p.Y, Day = town.Day };
        town.Ferries.Add(f);
        town.Log(LedgerCategory.Town, Importance.Notable, Text(town, 0, f, ""));
        return f;
    }

    /// <summary>Cut the town's own ferry: the river stands between a host and the town again.</summary>
    public static bool CutOwn(Town town, int id)
    {
        if (town.Ferries.FirstOrDefault(f => f.Id == id) is not { Cut: false } f) return false;
        f.Cut = true;
        town.Log(LedgerCategory.War, Importance.Notable, Text(town, 1, f, ""));
        return true;
    }

    public static bool Rebuild(Town town, int id)
    {
        var r = Rules(town);
        if (town.Ferries.FirstOrDefault(f => f.Id == id) is not { Cut: true } f || town.Treasury < r.Rebuild) return false;
        town.Book(TreasuryAccount.Works, -r.Rebuild);
        f.Cut = false;
        town.Log(LedgerCategory.Town, Importance.Routine, Text(town, 3, f, ""));
        return true;
    }

    /// <summary>Raiders from a live lair near a ferry cut it now and then.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var f in town.Ferries.Where(f => !f.Cut).OrderBy(f => f.Id).ToList())
        {
            var lair = town.Sites
                .Where(s => !s.Cleared && r.Cutters.Contains(s.Def.Kind) && s.RegionId != null && Dist(World.Spot(town, s), (f.X, f.Y)) <= r.Near)
                .OrderBy(s => s.Def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (lair == null || Voices.Hash("ferry-cut", town.Seed, town.Day, f.Id) % 10000 >= r.CutChance * 10000) continue;
            f.Cut = true;
            town.Log(LedgerCategory.War, Importance.Notable, Text(town, 2, f, lair.Def.Name));
        }
    }

    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Texts: 0 built, 1 cut by the town, 2 cut by raiders, 3 put back.</summary>
    private static string Text(Town town, int i, Ferry f, string site) =>
        (i < Rules(town).Texts.Count ? Rules(town).Texts[i] : "{ferry}")
            .Replace("{ferry}", f.Name, StringComparison.Ordinal)
            .Replace("{river}", town.Content.Passage.River.Name, StringComparison.Ordinal)
            .Replace("{site}", site, StringComparison.Ordinal);
}
