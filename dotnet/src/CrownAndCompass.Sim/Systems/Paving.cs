namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Once the town is chartered a town, its masons pave the streets with stone on their own, the square first and then
/// street by street outward from the centre, as the market has stone and the treasury can spare the gold. The player lays
/// no paving: a cobbled street is something a growing town earns. Houses beside cobbles are a little happier
/// (<see cref="Comfort"/>), and the client draws the stones (<see cref="Street.Cobbled"/>).
/// </summary>
public static class Paving
{
    public static PavingRules Rules(Town town) => town.Content.City.Paving;

    /// <summary>Whether the town is grand enough for its masons to pave.</summary>
    public static bool Open(Town town) => town.City.Tier >= City.TierIndex(town, Rules(town).Tier);

    public static double Length(Street s)
    {
        double len = 0;
        for (int i = 0; i + 3 < s.Points.Length; i += 2) len += Js.Hypot(s.Points[i + 2] - s.Points[i], s.Points[i + 3] - s.Points[i + 1]);
        return len;
    }

    public static bool Done(Street s) => s.Cobbled >= Length(s) - 1e-6;

    /// <summary>How far the street's nearest point lies from the town's centre: the masons work outward.</summary>
    private static double Reach(Street s)
    {
        double best = double.MaxValue;
        for (int i = 0; i + 1 < s.Points.Length; i += 2)
            best = Math.Min(best, Js.Hypot(s.Points[i] - Town.Center, s.Points[i + 1] - Town.Center));
        return best;
    }

    /// <summary>The street the masons work on next: one already begun, else the nearest the centre.</summary>
    public static Street? Next(Town town) =>
        town.Plan.Streets.Where(s => s.Points.Length >= 4 && !Done(s))
            .OrderByDescending(s => s.Cobbled > 0).ThenBy(s => Reach(s)).ThenBy(s => s.Id).FirstOrDefault();

    public static void Daily(Town town)
    {
        if (!Open(town)) return;
        var r = Rules(town);
        double budget = r.PerDay;
        while (budget > 1e-6 && town.Treasury > r.Reserve && Next(town) is { } s)
        {
            double len = Length(s);
            if (s.Cobbled <= 0)
            {
                // Lay from whichever end lies nearer the centre.
                int last = s.Points.Length - 2;
                s.FromEnd = Js.Hypot(s.Points[last] - Town.Center, s.Points[last + 1] - Town.Center) < Js.Hypot(s.Points[0] - Town.Center, s.Points[1] - Town.Center);
                if (!town.Plan.Streets.Any(o => o.Cobbled > 0))
                    town.Log(LedgerCategory.Town, Importance.Notable, $"The masons begin paving {town.Name}'s streets with stone, from the square outward.");
            }
            double want = Math.Min(budget, len - s.Cobbled);
            var (stone, paid) = town.Market.Take(Good.Stone, want * r.Stone);
            if (stone <= 1e-9) return;
            double laid = stone / r.Stone;
            double cost = paid + laid * r.Gold;
            town.Book(TreasuryAccount.Works, -cost);
            s.Cobbled = Math.Min(len, s.Cobbled + laid);
            budget -= laid;
            if (Done(s))
            {
                s.Cobbled = len;
                town.Log(LedgerCategory.Town, Importance.Routine, $"The masons finish cobbling {Name(town, s)}.");
            }
        }
    }

    /// <summary>"the square", or "the street past the Laughing Wyvern".</summary>
    public static string Name(Town town, Street s)
    {
        if (s.Square) return "the market square";
        int mid = (s.Points.Length / 2) & ~1;
        double mx = s.Points[mid], my = s.Points[mid + 1];
        Building? best = null;
        double bd = double.MaxValue;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Zone == Zone.Farmland) continue;
            var l = town.Lot(b.LotId);
            double d = (l.X - mx) * (l.X - mx) + (l.Y - my) * (l.Y - my);
            if (d < bd) (bd, best) = (d, b);
        }
        if (best == null) return "a street";
        string name = best.Name.StartsWith("The ", StringComparison.Ordinal) ? "the " + best.Name[4..] : best.Name;
        return $"the street past {name}";
    }

    /// <summary>Contentment a household takes from cobbles at its door: dry feet, no mud, a street to be proud of.</summary>
    public static double Comfort(Town town, Building house)
    {
        var r = Rules(town);
        if (r.Comfort <= 0) return 0;
        var l = town.Lot(house.LotId);
        double reach2 = r.Reach * r.Reach;
        foreach (var s in town.Plan.Streets)
        {
            if (s.Cobbled <= 0) continue;
            bool done = Done(s);
            double walked = 0;
            int n = s.Points.Length;
            for (int k = 0; k + 3 < n; k += 2)
            {
                // Segments in laying order, from the end the masons began at.
                int i = s.FromEnd ? n - 2 - k : k, j = s.FromEnd ? i - 2 : i + 2;
                double seg = Js.Hypot(s.Points[j] - s.Points[i], s.Points[j + 1] - s.Points[i + 1]);
                if (!done && walked >= s.Cobbled) break;
                var hit = Streets.SegmentDistance(l.X, l.Y, s.Points[i], s.Points[i + 1], s.Points[j], s.Points[j + 1]);
                if (hit.D * hit.D <= reach2) return r.Comfort;
                walked += seg;
            }
        }
        return 0;
    }
}

public sealed record PavingRules
{
    /// <summary>The charter at which the masons start paving.</summary>
    public string Tier { get; init; } = "town";

    /// <summary>Tiles of street paved a day at most.</summary>
    public double PerDay { get; init; } = 0.6;

    /// <summary>Stone a tile of street takes, off the market's stock (none is imported for paving).</summary>
    public double Stone { get; init; } = 2;

    /// <summary>Masons' gold a tile, on top of the stone.</summary>
    public double Gold { get; init; } = 6;

    /// <summary>The treasury the masons leave untouched.</summary>
    public double Reserve { get; init; } = 500;

    /// <summary>Contentment for a house within <see cref="Reach"/> tiles of cobbles.</summary>
    public double Comfort { get; init; } = 0.03;

    public double Reach { get; init; } = 2;
}
