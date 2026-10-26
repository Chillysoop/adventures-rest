namespace CrownAndCompass.Sim.Systems;

/// <summary>What a kind of working cuts, the buildings that work it, and the country it needs (workings.json <c>goods</c>).</summary>
public sealed record WorkingDef
{
    public Good Good { get; init; }
    public IReadOnlyList<string> Buildings { get; init; } = [];

    /// <summary>Hard country it lies in or beside.</summary>
    public IReadOnlyList<string> Features { get; init; } = [];
}

/// <summary>How a cart road from a far working gets raided (workings.json <c>raid</c>), per cart.</summary>
public sealed record CartRaidRules
{
    /// <summary>Chance a cart is taken: a base, more for each day of road, the town's danger and each hostile lair near the road.</summary>
    public double Base { get; init; } = 0.03;
    public double PerDay { get; init; } = 0.03;
    public double Danger { get; init; } = 0.15;
    public double Lair { get; init; } = 0.05;

    /// <summary>Days from the road a lair's raiders reach it.</summary>
    public double LairReach { get; init; } = 0.4;

    /// <summary>Share of the chance patrols over the whole road take away.</summary>
    public double PatrolCut { get; init; } = 0.85;
    public double Most { get; init; } = 0.45;

    /// <summary>Days the alert stands after a cart is taken.</summary>
    public int AlertDays { get; init; } = 4;
}

public sealed record WorkingsRules
{
    public IReadOnlyList<WorkingDef> Goods { get; init; } = [];

    /// <summary>Days out a working counts as near (cut by the town's own hands, no road to keep), and the farthest one may lie.</summary>
    public double Near { get; init; } = 0.8;
    public double Far { get; init; } = 3;

    /// <summary>Days between the scan's rings, and its bearings.</summary>
    public double Step { get; init; } = 0.1;
    public int Bearings { get; init; } = 48;

    /// <summary>Hard country this many days off still counts as beside it.</summary>
    public double Margin { get; init; } = 0.15;

    /// <summary>A far working's yield: the share lost for each day of road past <see cref="Near"/> (time on the road), and the least kept.</summary>
    public double LossPerDay { get; init; } = 0.12;
    public double LeastYield { get; init; } = 0.5;

    /// <summary>Days between carts down a far working's road.</summary>
    public int CartDays { get; init; } = 2;

    public CartRaidRules Raid { get; init; } = new();

    public EdgeRules Edge { get; init; } = new();
}

/// <summary>
/// Woods and rock that reach into the town's own ground from hard country close by (workings.json <c>edge</c>): within
/// <see cref="Reach"/> days a feature pushes a lobe in from the plan's rim on its bearing, the nearer the deeper and wider,
/// with smaller ones beside it. Nothing is built or zoned there, so the town grows round them.
/// </summary>
public sealed record EdgeRules
{
    public double Reach { get; init; } = 0.7;

    /// <summary>Tiles from the hall a lobe's middle lies: at the rim for a feature at <see cref="Reach"/>, this much nearer for one at the town.</summary>
    public double Rim { get; init; } = 40;
    public double Depth { get; init; } = 16;

    /// <summary>A lobe's radius in tiles, least and more for a near feature; the satellites' share of it, and how many.</summary>
    public double Radius { get; init; } = 6;
    public double Grow { get; init; } = 8;
    public double Satellite { get; init; } = 0.55;
    public int Satellites { get; init; } = 2;

    /// <summary>Tiles round the hall kept clear whatever lies near.</summary>
    public double Keep { get; init; } = 16;
}

/// <summary>A working the town cuts from: the spot in days from the town, and how far it lies.</summary>
public readonly record struct Working(Good Good, double X, double Y, double Days);

/// <summary>Carts taken on a far working's road lately (the alert reads it).</summary>
public sealed class WorkingsState
{
    public int RaidedDay { get; set; } = -100;
    public Good RaidedGood { get; set; }
    public double RaidedLoad { get; set; }
}

/// <summary>
/// Timber and stone come from the land. A lumber camp needs woods and a quarry rock (a region whose habitats hold them,
/// or hard country of the right kind): near the town they are cut by its own hands; farther out, up to
/// <see cref="WorkingsRules.Far"/> days, the camp in town sends its hands out and the loads come home by cart down a road
/// that raiders and lairs beside it can cut, unless the town patrols it (<see cref="Wilds.Along"/>). Woods grow back as
/// fast as they are cut, so a working never runs out. Where the ground lies is a pure function of the seed and the land
/// (nothing saved); only the carts' loads (<see cref="Building.Cart"/>) and the last raid are.
/// </summary>
public static class Workings
{
    public static WorkingsRules Rules(Town town) => town.Content.Workings;

    private sealed class Cache
    {
        public readonly Dictionary<Good, Working?> Nearest = [];
        public byte[]? Cells;
        public (int, double, double) Stamp;
    }

    /// <summary>The town's cache, emptied whenever its country has changed under it (made, or moved by a founding).</summary>
    private static Cache CacheOf(Town town)
    {
        var c = Caches.GetValue(town, _ => new Cache());
        var stamp = (town.Terrain.Count, town.HeartX, town.HeartY);
        if (c.Stamp != stamp)
        {
            c.Nearest.Clear();
            c.Cells = null;
            c.Stamp = stamp;
        }
        return c;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Town, Cache> Caches = new();

    /// <summary>Drops the workings found for the town, once the world moves under it (<see cref="Founding.Settle"/>).</summary>
    internal static void Forget(Town town) => Caches.Remove(town);

    /// <summary>The kind of working a building cuts, or null.</summary>
    public static WorkingDef? Of(Town town, BuildingDef d)
    {
        foreach (var g in Rules(town).Goods)
            foreach (string id in g.Buildings)
                if (id == d.Id) return g;
        return null;
    }

    /// <summary>
    /// Whether the country at a spot (days from the town) holds this working: hard country of its kind (a tangled wood, a
    /// ridge, a range) under it or just beside it. The country is fixed once the world is made, so the answer never changes
    /// in a campaign (and a reloaded one finds the same workings).
    /// </summary>
    public static bool Holds(Town town, WorkingDef w, double x, double y)
    {
        double m = Rules(town).Margin;
        foreach (var f in town.Terrain)
            if (w.Features.Contains(f.Kind.Id) && Roads.Contains(f, x, y, m)) return true;
        return false;
    }

    /// <summary>The ground of the plan's cells (<see cref="Land.Size"/> squared): 0 open, 1 woods, 2 rock.</summary>
    public const byte Woods = 1, Rock = 2;

    /// <summary>
    /// Which plan cells woods or rock reach into (<see cref="EdgeRules"/>), worked out once from the country round the town.
    /// </summary>
    public static byte[] Cells(Town town)
    {
        if (town.WorkingCells is { } held && town.WorkingStamp == (town.Terrain.Count, town.HeartX, town.HeartY)) return held;
        var c = CacheOf(town);
        if (c.Cells != null)
        {
            town.WorkingStamp = c.Stamp;
            return town.WorkingCells = c.Cells;
        }
        var r = Rules(town);
        var e = r.Edge;
        int n = Land.Size;
        var cells = new byte[n * n];
        foreach (var f in town.Terrain.OrderBy(f => f.Id))
        {
            byte kind = 0;
            foreach (var w in r.Goods)
                if (w.Features.Contains(f.Kind.Id)) kind = w.Good == Good.Timber ? Woods : Rock;
            if (kind == 0) continue;
            // The feature's nearest edge to the town, on a coarse scan out to the reach.
            double best = double.MaxValue, at = 0;
            for (int i = 0; i < r.Bearings; i++)
            {
                double a = (i + 0.5) * 2 * Math.PI / r.Bearings;
                for (double d = r.Step / 2; d <= e.Reach + 1e-9 && d < best; d += r.Step / 2)
                    if (Roads.Contains(f, d * Math.Sin(a), -d * Math.Cos(a), r.Margin))
                    {
                        best = d;
                        at = a;
                        break;
                    }
            }
            if (best > e.Reach) continue;
            double k = 1 - best / e.Reach;
            double rc = e.Rim - e.Depth * k, rr = e.Radius + e.Grow * k;
            Lobe(cells, kind, at, rc, rr, f.Id * 7 + 1, e.Keep);
            for (int sIdx = 0; sIdx < e.Satellites; sIdx++)
            {
                double side = (sIdx % 2 == 0 ? 1 : -1) * (0.3 + 0.25 * Unit(f.Id * 13 + sIdx));
                Lobe(cells, kind, at + side, rc + 3 + 4 * Unit(f.Id * 17 + sIdx), rr * e.Satellite, f.Id * 7 + 3 + sIdx, e.Keep);
            }
        }
        town.WorkingStamp = c.Stamp;
        return town.WorkingCells = c.Cells = cells;
    }

    private static double Unit(int salt) => Voices.Hash("working-edge", salt) / 4294967296.0;

    /// <summary>Marks a ragged round lobe of cells, its middle <paramref name="rc"/> tiles from the hall on bearing <paramref name="a"/> (radians, x east, y south).</summary>
    private static void Lobe(byte[] cells, byte kind, double a, double rc, double rr, int salt, double keep)
    {
        double cx = Town.Center + rc * Math.Sin(a), cy = Town.Center - rc * Math.Cos(a);
        double p1 = Unit(salt) * 2 * Math.PI, p2 = Unit(salt + 1) * 2 * Math.PI;
        int n = Land.Size;
        double reach = rr * 1.45;
        for (int y = Math.Max(0, (int)((cy - reach) * Land.Res)); y <= Math.Min(n - 1, (int)((cy + reach) * Land.Res)); y++)
            for (int x = Math.Max(0, (int)((cx - reach) * Land.Res)); x <= Math.Min(n - 1, (int)((cx + reach) * Land.Res)); x++)
            {
                double px = (x + 0.5) / Land.Res, py = (y + 0.5) / Land.Res, dx = px - cx, dy = py - cy;
                double t = Math.Atan2(dy, dx), edge = rr * (1 + 0.22 * Math.Sin(3 * t + p1) + 0.12 * Math.Sin(5 * t + p2));
                if (dx * dx + dy * dy > edge * edge) continue;
                double hx = px - Town.Center, hy = py - Town.Center;
                if (hx * hx + hy * hy < keep * keep) continue;
                int i = y * n + x;
                if (cells[i] == 0) cells[i] = kind;
            }
    }

    /// <summary>The nearest spot of this working to the town, within <see cref="WorkingsRules.Far"/> days, or null.</summary>
    public static Working? Nearest(Town town, Good good)
    {
        var c = CacheOf(town);
        if (c.Nearest.TryGetValue(good, out var hit)) return hit;
        var r = Rules(town);
        var w = r.Goods.FirstOrDefault(g => g.Good == good);
        Working? best = null;
        if (w != null)
            for (double d = r.Step; d <= r.Far + 1e-9 && best == null; d += r.Step)
                for (int i = 0; i < r.Bearings; i++)
                {
                    double a = (i + 0.5) * 2 * Math.PI / r.Bearings;
                    double x = d * Math.Sin(a), y = -d * Math.Cos(a);
                    if (!Holds(town, w, x, y)) continue;
                    best = new Working(good, x, y, d);
                    break;
                }
        c.Nearest[good] = best;
        return best;
    }

    /// <summary>The working a camp or quarry cuts from, or null for a building that cuts none (or has nowhere to).</summary>
    public static Working? For(Town town, Building b) => Of(town, b.Def) is { } w ? Nearest(town, w.Good) : null;

    /// <summary>Whether a working lies far enough out that its loads come home by cart.</summary>
    public static bool Afar(Town town, Working w) => w.Days > Rules(town).Near;

    /// <summary>Why the town can't raise this building for want of the land to work, or null.</summary>
    public static string? Locked(Town town, BuildingDef d)
    {
        if (Of(town, d) is not { } w || Nearest(town, w.Good) != null) return null;
        string what = w.Good == Good.Timber ? "woods" : "rock to quarry";
        return $"no {what} within {Js.Str(Rules(town).Far)} days of the town";
    }

    /// <summary>A camp's yield from its working: whole near the town, less for each day of road beyond.</summary>
    public static double Yield(Town town, Building b)
    {
        if (For(town, b) is not { } w || !Afar(town, w)) return 1;
        var r = Rules(town);
        return Math.Max(r.LeastYield, 1 - r.LossPerDay * (w.Days - r.Near));
    }

    /// <summary>Whether a camp's goods go onto the cart for the road home rather than straight to market.</summary>
    public static bool Carted(Town town, Building b) => For(town, b) is { } w && Afar(town, w);

    /// <summary>How much of the road to a working the town's patrols keep, 0 to 1.</summary>
    public static double Patrolled(Town town, Working w)
    {
        double near = Math.Min(w.Days, town.Content.Wilds.Near + town.Content.Wilds.Cell * 0.5);
        return Wilds.Along(town, (w.X * near / w.Days, w.Y * near / w.Days), (w.X, w.Y));
    }

    /// <summary>Hostile lairs within reach of the road to a working.</summary>
    public static int LairsBy(Town town, Working w)
    {
        double reach = Rules(town).Raid.LairReach;
        int n = 0;
        foreach (var s in town.Sites)
        {
            if (s.Cleared || s.Def.Distance <= 0) continue;
            var (sx, sy) = World.Spot(town, s);
            if (Streets.SegmentDistance(sx, sy, 0, 0, w.X, w.Y).D <= reach) n++;
        }
        return n;
    }

    /// <summary>The chance a cart down this road is taken.</summary>
    public static double RaidChance(Town town, Working w)
    {
        var r = Rules(town).Raid;
        double p = r.Base + r.PerDay * w.Days + r.Danger * Frontier.Danger(town) + r.Lair * LairsBy(town, w);
        return Math.Clamp(p * (1 - r.PatrolCut * Patrolled(town, w)), 0, r.Most);
    }

    /// <summary>
    /// Daily: every <see cref="WorkingsRules.CartDays"/> days the carts of each far working set off home together with what
    /// its camps' hands cut. On the working's own dice they are taken on the road, or the loads reach the market and each
    /// camp is paid for its share.
    /// </summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (town.Day % Math.Max(1, r.CartDays) != 0) return;
        double salesTax = town.TaxRate + Planning.SalesBoost(town);
        foreach (var g in r.Goods)
        {
            var camps = town.Buildings.Where(b => b.Cart > 0 && g.Buildings.Contains(b.Def.Id)).ToList();
            if (camps.Count == 0 || Nearest(town, g.Good) is not { } w) continue;
            double load = camps.Sum(b => b.Cart);
            var dice = new Rng(unchecked((int)Voices.Hash("cart", town.Seed, (int)g.Good, town.Day)));
            bool taken = dice.Next() < RaidChance(town, w);
            foreach (var b in camps)
            {
                if (!taken)
                {
                    double revenue = town.Market.Sell(g.Good, b.Cart);
                    double tax = revenue * salesTax;
                    town.Book(TreasuryAccount.SalesTax, tax);
                    b.Cash += revenue - tax;
                    b.RevenueToday += revenue;
                }
                b.Cart = 0;
            }
            if (!taken) continue;
            town.Workings.RaidedDay = town.Day;
            town.Workings.RaidedGood = g.Good;
            town.Workings.RaidedLoad = load;
            string what = g.Good == Good.Timber ? "timber" : "stone";
            town.Log(LedgerCategory.War, Importance.Notable,
                $"Raiders fall on the {what} carts on the road from the {(g.Good == Good.Timber ? "woods" : "quarry")} {Js.Str(Js.Round(w.Days * 10) / 10)} days out, and {Js.Str(Js.Round(load))} loads never reach town.");
        }
    }
}
