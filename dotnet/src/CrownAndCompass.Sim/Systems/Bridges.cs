namespace CrownAndCompass.Sim.Systems;

public sealed record BridgeRules
{
    /// <summary>The kinds of country (roads.json) a rope bridge spans, and its names ({A} a word of the country).</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];

    /// <summary>Daily chance raiders of a live lair in the same region cut a rope bridge, out to this ring.</summary>
    public double Cut { get; init; } = 0.01;
    public int Ring { get; init; } = 1;
    public IReadOnlyList<string> Cutters { get; init; } = ["tribe", "rivals"];

    /// <summary>Gold to rebuild a cut bridge in stone, and the treasury (times it) at which the stand-in mayor does.</summary>
    public double Stone { get; init; } = 240;
    public double AutoAt { get; init; } = 3;
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>
/// Rope bridges over the gorges. Every gorge on the roads has a named rope bridge across it, so companies cross it
/// where they would otherwise climb down and up. Raiders from a live lair in the same country cut them now and
/// then: until the town rebuilds it in stone (<see cref="Rebuild"/>), the gorge can only be gone round or flown.
/// Stone bridges are never cut. Rolls go by hash, not the town's dice.
/// </summary>
public static class Bridges
{
    public const string Rope = "rope", Cut = "cut", Stone = "stone";

    private static BridgeRules Rules(Town town) => town.Content.Roads.Bridges;

    /// <summary>At generation: a bridge and its name for each gorge.</summary>
    public static void Span(Town town)
    {
        var r = Rules(town);
        foreach (var f in town.Terrain.Where(f => r.Kinds.Contains(f.Kind.Id) && f.Bridge == ""))
        {
            f.Bridge = Rope;
            if (r.Names.Count == 0) continue;
            string word = f.Kind.Words.Count > 0 ? f.Kind.Words[(int)(Voices.Hash("bridge-word", town.Seed, f.Id) % (uint)f.Kind.Words.Count)] : "Old";
            f.BridgeName = r.Names[(int)(Voices.Hash("bridge", town.Seed, f.Id) % (uint)r.Names.Count)].Replace("{A}", word);
        }
    }

    /// <summary>What a party must be able to do to cross this country: a cut bridge leaves the gorge sheer.</summary>
    public static Ways Needs(Feature f) => f.Bridge == Cut ? Ways.Sheer : Passage.Needs(f.Kind);

    public static IEnumerable<Feature> CutBridges(Town town) => town.Terrain.Where(f => f.Bridge == Cut);

    public static string Name(Feature f) => f.BridgeName != "" ? f.BridgeName : $"the bridge over {f.Name}";

    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        // Towns from before the bridges get theirs now.
        if (town.Terrain.Any(f => f.Bridge == "" && r.Kinds.Contains(f.Kind.Id))) Span(town);
        foreach (var f in town.Terrain.Where(f => f.Bridge == Rope).ToList())
        {
            var region = town.Regions.FirstOrDefault(g => g.Id == f.RegionId);
            if (region == null || region.Ring > r.Ring) continue;
            var lair = town.Sites
                .Where(s => s.RegionId == region.Id && !s.Cleared && r.Cutters.Contains(s.Def.Kind))
                .OrderBy(s => s.Def.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (lair == null || Voices.Hash("cut", town.Seed, town.Day, f.Id) % 10000 >= r.Cut * 10000) continue;
            f.Bridge = Cut;
            town.Log(LedgerCategory.War, Importance.Notable, Text(r, "cut", f).Replace("{site}", lair.Def.Name));
        }
        if (!autoMayor) return;
        foreach (var f in CutBridges(town).OrderBy(f => f.Id).ToList())
            if (town.Treasury >= r.Stone * r.AutoAt) Rebuild(town, f.Id);
    }

    /// <summary>Masons rebuild a cut bridge in stone, for good. False if it isn't cut or the treasury is short.</summary>
    public static bool Rebuild(Town town, int featureId)
    {
        var r = Rules(town);
        var f = town.Terrain.FirstOrDefault(x => x.Id == featureId);
        if (f is not { Bridge: Cut } || town.Treasury < r.Stone) return false;
        town.Book(TreasuryAccount.Works, -r.Stone);
        f.Bridge = Stone;
        town.Log(LedgerCategory.Town, Importance.Notable, Text(r, "stone", f) + $" (-{Js.Str(r.Stone)} gold)");
        return true;
    }

    private static string Text(BridgeRules r, string key, Feature f) =>
        r.Texts.GetValueOrDefault(key, "").Replace("{bridge}", Name(f)).Replace("{gorge}", f.Name);
}
