namespace CrownAndCompass.Sim.Systems;

/// <summary>How an enemy's fire spreads in town (crises.json "fire").</summary>
public sealed record FireRules
{
    /// <summary>Timber buildings within this many tiles of the blaze are at risk, the nearest first, at most so many of them.</summary>
    public double Reach { get; init; } = 2.4;
    public int Neighbours { get; init; } = 5;

    /// <summary>Chance each one catches, before the bucket chain; and at most this many catch from one fire.</summary>
    public double Catch { get; init; } = 0.3;
    public int MaxSpread { get; init; } = 3;

    /// <summary>How much the watch's strength counts toward the bucket chain, beside wells and fountains (<see cref="Decor.Douse"/>).</summary>
    public double WatchDouse { get; init; } = 0.2;

    /// <summary>Share of its work a building that catches loses (less than the blaze it caught from).</summary>
    public double[] Wreck { get; init; } = [0.2, 0.4];

    /// <summary>The Ledger: {from}, {list}; held is when the bucket chain kept it to one building.</summary>
    public string Spread { get; init; } = "";
    public string Held { get; init; } = "";
}

/// <summary>
/// Fire only ever comes from an enemy here (an arsonist, raiders' torches): there is no chance of a house simply
/// catching. But a fire an enemy sets can leap to the timber buildings round it, after Kingdoms and Castles, unless a
/// bucket chain from a well or fountain nearby and the watch beat it down. Stone never burns, and slate and lime
/// (<see cref="Research.Fireproof"/>) save some.
/// </summary>
public static class Fires
{
    private static FireRules Rules(Town town) => town.Content.Crises.Fire;

    /// <summary>A building an enemy has just burned: the fire may leap to its neighbours. Returns those that caught.</summary>
    public static List<Building> Spread(Town town, Building origin)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var o = town.Lot(origin.LotId);
        double Dist(Building b)
        {
            var l = town.Lot(b.LotId);
            return Math.Sqrt((l.X - o.X) * (l.X - o.X) + (l.Y - o.Y) * (l.Y - o.Y));
        }
        var near = town.Buildings
            .Where(b => b != origin && b.Complete && !b.Def.IsGrand && Blows.Burns(b) && Dist(b) <= r.Reach)
            .OrderBy(Dist)
            .ThenBy(b => b.Id)
            .Take(r.Neighbours)
            .ToList();
        var caught = new List<Building>();
        if (near.Count == 0) return caught;
        double douse = Math.Min(0.9, Decor.Douse(town, o.X, o.Y) + Council.WatchStrength(town) * r.WatchDouse);
        double chance = r.Catch * (1 - douse) * (1 - Research.Fireproof(town));
        foreach (var b in near)
        {
            if (caught.Count >= r.MaxSpread) break;
            if (!rng.Chance(chance)) continue;
            Blows.Wreck(town, b, r.Wreck[0] + rng.Next() * (r.Wreck[^1] - r.Wreck[0]));
            caught.Add(b);
        }
        if (caught.Count > 0)
        {
            town.Log(LedgerCategory.War, Importance.Major, r.Spread.Replace("{from}", origin.Name).Replace("{list}", Expedition.ListNames(caught.Select(b => b.Name).ToList())), caught.Where(b => b.OwnerId != null).Select(b => b.OwnerId!.Value).Distinct().OrderBy(id => id).ToList());
            town.Tension += 5 * caught.Count;
            Saga.Scar(town, 0.5 * caught.Count);
        }
        else if (douse > 0.25 && r.Held != "") town.Log(LedgerCategory.War, Importance.Notable, r.Held.Replace("{from}", origin.Name));
        return caught;
    }
}
