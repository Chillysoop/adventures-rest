namespace CrownAndCompass.Sim.Systems;

public sealed record RuinDef
{
    public string Id { get; init; } = "";
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Gold to restore it once cleared, and what it does for the town restored.</summary>
    public double Cost { get; init; } = 200;
    public double Defence { get; init; }
    public int SkyWatch { get; init; }
    public double Mood { get; init; }
    public string Gives { get; init; } = "";
    public string Text { get; init; } = "";
}

public sealed record RuinRules
{
    /// <summary>How many of the near sites are old works fallen to ruin, out to this ring.</summary>
    public int Count { get; init; } = 3;
    public int Ring { get; init; } = 1;
    public double AutoAt { get; init; } = 3;
    public IReadOnlyList<RuinDef> Kinds { get; init; } = [];
}

/// <summary>
/// Old works near the town fallen to ruin: an aqueduct, a gatehouse, a watchtower, a star fort. Each is a lair like any
/// other until a company clears it; then the town can restore it cheaply (<see cref="Restore"/>) and it serves for good:
/// defence, an early warning, or contentment. A restored ruin is never resettled.
/// </summary>
public static class Ruins
{
    private static RuinRules Rules(Town town) => town.Content.World.Ruins;

    public static RuinDef? Of(Town town, SiteState s) =>
        s.Def.Ruin == "" ? null : Rules(town).Kinds.FirstOrDefault(k => k.Id == s.Def.Ruin) ?? (town.Content.Founding.Ruin is { } keep && keep.Id == s.Def.Ruin ? keep : null);

    private static IEnumerable<(SiteState Site, RuinDef Def)> Restored(Town town) =>
        town.Sites.Where(s => s.Restored).Select(s => (s, Of(town, s))).Where(x => x.Item2 != null).Select(x => (x.s, x.Item2!));

    /// <summary>Cleared, not yet restored: the council can put it back to use.</summary>
    public static IEnumerable<SiteState> Restorable(Town town) =>
        town.Sites.Where(s => s.Def.Ruin != "" && s.Cleared && !s.Restored).OrderBy(s => s.Def.Id, StringComparer.Ordinal);

    public static double Defence(Town town) => Js.Sum(Restored(town), x => x.Def.Defence);
    public static int SkyWatch(Town town) => Restored(town).Sum(x => x.Def.SkyWatch);
    public static double Mood(Town town) => Js.Sum(Restored(town), x => x.Def.Mood);

    /// <summary>At generation: a few near sites are ruins of old works (on their own dice, so nothing else shifts).</summary>
    internal static void Place(Town town, HashSet<string> used, Func<Rng, ThemeDef, IReadOnlyList<string>, HashSet<string>, string> fill)
    {
        var r = Rules(town);
        if (r.Kinds.Count == 0) return;
        var rng = new Rng(town.Seed * 5303 + 977);
        var pool = town.Sites
            .Where(s => s.Generated && s.Def.Foe == "" && !s.Def.Aloft && s.RegionId is int rid && town.Regions[rid].Ring <= r.Ring && !town.Regions[rid].Theme.Sea)
            .OrderBy(s => s.Def.Id, StringComparer.Ordinal)
            .ToList();
        var kinds = r.Kinds.ToList();
        for (int i = 0; i < r.Count && pool.Count > 0 && kinds.Count > 0; i++)
        {
            var site = pool[rng.Range(0, pool.Count)];
            pool.Remove(site);
            var k = kinds[rng.Range(0, kinds.Count)];
            kinds.Remove(k);
            used.Remove(site.Def.Name);
            string name = k.Names.Count > 0 ? fill(rng, town.Regions[site.RegionId!.Value].Theme, k.Names, used) : site.Def.Name;
            site.Def = site.Def with { Ruin = k.Id, Name = name };
        }
    }

    /// <summary>Restore a cleared ruin: false if it isn't one, isn't cleared, or the treasury is short.</summary>
    public static bool Restore(Town town, string siteId)
    {
        var s = Quests.Site(town, siteId);
        if (s == null || !s.Cleared || s.Restored || Of(town, s) is not { } k || town.Treasury < k.Cost) return false;
        town.Book(TreasuryAccount.Works, -k.Cost);
        s.Restored = true;
        s.ResettleTick = null;
        town.Log(LedgerCategory.Town, Importance.Notable, k.Text.Replace("{site}", s.Def.Name) + $" (-{Js.Str(k.Cost)} gold)");
        return true;
    }

    public static void Daily(Town town, bool autoMayor)
    {
        if (!autoMayor) return;
        foreach (var s in Restorable(town).ToList())
            if (Of(town, s) is { } k && town.Treasury >= k.Cost * Rules(town).AutoAt) Restore(town, s.Def.Id);
    }
}
