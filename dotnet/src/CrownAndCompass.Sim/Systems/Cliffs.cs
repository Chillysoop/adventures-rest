namespace CrownAndCompass.Sim.Systems;

public sealed record CliffRules
{
    /// <summary>Plateaus tried for round the town; fewer stand where the river, a site or the south road leaves no room.</summary>
    public int Count { get; init; } = 4;

    /// <summary>Tiles from the hall to a plateau's middle, least and most.</summary>
    public double[] Distance { get; init; } = [66, 90];

    /// <summary>A plateau's radius in tiles, least and most.</summary>
    public double[] Radius { get; init; } = [5, 9];

    /// <summary>How high its cliffs stand, in tiles, least and most.</summary>
    public double[] Height { get; init; } = [3.2, 5];

    /// <summary>Tiles over which a cliff face rises from its foot to its top.</summary>
    public double Face { get; init; } = 0.8;

    /// <summary>A ramp's half width and how far it runs out from the cliff's edge, in tiles.</summary>
    public double RampWidth { get; init; } = 1.4;

    public double RampLength { get; init; } = 6;

    /// <summary>A plateau with a radius at least this has a second ramp on its far side.</summary>
    public double TwoRamps { get; init; } = 7;

    /// <summary>Clearance kept between a plateau and the plan's square, the river and sites (days), and another plateau (tiles).</summary>
    public double PlanClear { get; init; } = 1.5;

    public double RiverClear { get; init; } = 0.04;
    public double SiteClear { get; init; } = 0.04;
    public double Apart { get; init; } = 6;

    /// <summary>Radians either side of due south (the road out of the gate) kept free of cliffs.</summary>
    public double RoadArc { get; init; } = 0.45;
}

/// <summary>A plateau: its middle in tiles from the town's corner, its outline's radius by bearing, its height, and the ramps up it (bearings).</summary>
public sealed record Plateau(int Id, double X, double Y, double R, double H, double[] Shape, double[] Ramps)
{
    /// <summary>The cliff's edge radius at a bearing (radians, x east, y south).</summary>
    public double EdgeAt(double a) => R * (1 + Shape[0] * Math.Sin(2 * a + Shape[1]) + Shape[2] * Math.Sin(3 * a + Shape[3]));
}

/// <summary>
/// Terraced plateaus with sheer cliffs round the town, Northgard-style: a few in the near wilds past the town's plan, each with a
/// ramp or two up it. Feet go round them or up the ramps (<see cref="Passage"/> treats each as a barrier whose openings are its
/// ramps); the client raises them out of the ground (<see cref="Height"/>). A pure function of the seed and the land's fixed
/// layout (the river, sites): nothing is saved and nothing is drawn from the town's rng.
/// </summary>
public static class Cliffs
{
    private static CliffRules Rules(Town town) => town.Content.Passage.Cliffs;

    private sealed class Cache
    {
        public List<Plateau>? Plateaus;
        public List<Barrier>? Barriers;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Town, Cache> Caches = new();

    /// <summary>The plateaus round the town, sited once per town.</summary>
    public static IReadOnlyList<Plateau> Of(Town town)
    {
        var c = Caches.GetValue(town, _ => new Cache());
        if (c.Plateaus != null) return c.Plateaus;
        var r = Rules(town);
        var list = new List<Plateau>();
        var river = Passage.River(town);
        var sites = town.Sites.Where(s => s.Def.Distance > 0).Select(s => World.Spot(town, s)).ToList();
        for (int i = 0; i < 80 && list.Count < r.Count; i++)
        {
            double a = Hash(town.Seed, i * 9) * Math.PI * 2;
            if (Math.Abs(Diff(a, Math.PI / 2)) < r.RoadArc) continue;
            double d = Lerp(r.Distance, Hash(town.Seed, i * 9 + 1)), rad = Lerp(r.Radius, Hash(town.Seed, i * 9 + 2));
            double x = Town.Center + Math.Cos(a) * d, y = Town.Center + Math.Sin(a) * d;
            double[] shape = [0.06 + Hash(town.Seed, i * 9 + 3) * 0.12, Hash(town.Seed, i * 9 + 4) * 6.28, 0.04 + Hash(town.Seed, i * 9 + 5) * 0.08, Hash(town.Seed, i * 9 + 6) * 6.28];
            // The first ramp faces the town, give or take; a big plateau has a second on its far side.
            double toward = Math.Atan2(Town.Center - y, Town.Center - x) + (Hash(town.Seed, i * 9 + 7) - 0.5) * 1.4;
            double[] ramps = rad >= r.TwoRamps ? [toward, toward + Math.PI + (Hash(town.Seed, i * 9 + 8) - 0.5)] : [toward];
            var p = new Plateau(list.Count, x, y, rad, Lerp(r.Height, Hash(town.Seed, i * 9 + 8)), shape, ramps);
            if (Fits(town, r, p, list, river, sites)) list.Add(p);
        }
        return c.Plateaus = list;
    }

    private static bool Fits(Town town, CliffRules r, Plateau p, List<Plateau> others, IReadOnlyList<(double X, double Y)> river, List<(double X, double Y)> sites)
    {
        if (others.Any(o => Math.Sqrt((o.X - p.X) * (o.X - p.X) + (o.Y - p.Y) * (o.Y - p.Y)) < o.R * 1.3 + p.R * 1.3 + r.Apart)) return false;
        var pts = new List<(double X, double Y)>();
        for (int k = 0; k < 24; k++)
        {
            double a = k * Math.PI * 2 / 24, e = p.EdgeAt(a) + r.Face;
            pts.Add((p.X + Math.Cos(a) * e, p.Y + Math.Sin(a) * e));
        }
        foreach (double a in p.Ramps)
        {
            double e = p.EdgeAt(a) + r.RampLength + r.RampWidth;
            pts.Add((p.X + Math.Cos(a) * e, p.Y + Math.Sin(a) * e));
        }
        double lo = -r.PlanClear, hi = Town.Grid + r.PlanClear;
        foreach (var q in pts)
        {
            if (q.X > lo && q.X < hi && q.Y > lo && q.Y < hi) return false;
            var dq = Passage.ToDays(q.X, q.Y);
            if (river.Any(v => Dist(v, dq) < r.RiverClear)) return false;
            if (sites.Any(s => Dist(s, dq) < r.SiteClear)) return false;
        }
        return true;
    }

    /// <summary>The cliffs as barriers for walkers: each plateau's edge, open only at its ramps (In on top, Out at the ramp's foot).</summary>
    public static IReadOnlyList<Barrier> Barriers(Town town)
    {
        var c = Caches.GetValue(town, _ => new Cache());
        if (c.Barriers != null) return c.Barriers;
        var r = Rules(town);
        var w = town.Content.Passage.Walls;
        var list = new List<Barrier>();
        foreach (var p in Of(town))
        {
            const int N = 48;
            var loop = new (double X, double Y)[N];
            double area = 0;
            for (int i = 0; i < N; i++)
            {
                double a = i * Math.PI * 2 / N, e = p.EdgeAt(a);
                loop[i] = (p.X + Math.Cos(a) * e, p.Y + Math.Sin(a) * e);
            }
            for (int i = 0; i < N; i++) area += loop[i].X * loop[(i + 1) % N].Y - loop[(i + 1) % N].X * loop[i].Y;
            double reach = r.Face / 2 + 0.2;
            var openings = p.Ramps.Select(a =>
            {
                double e = p.EdgeAt(a), cx = Math.Cos(a), cy = Math.Sin(a);
                return new Opening("ramp", (p.X + cx * (e - 1), p.Y + cy * (e - 1)), (p.X + cx * (e + r.RampLength), p.Y + cy * (e + r.RampLength)));
            }).ToList();
            list.Add(new Barrier("cliff", loop, Passage.Offset(loop, reach), Passage.Offset(loop, reach + w.Round), openings, Math.Abs(area) / 2)
            {
                Reach = reach,
                Foot = reach,
                Round = w.Round,
            });
        }
        return c.Barriers = list;
    }

    /// <summary>How far the cliffs raise the ground at (x, y) in tiles from the town's corner: a plateau's height on top, falling sheer at its edge but for its ramps.</summary>
    public static double Height(Town town, double x, double y)
    {
        var r = Rules(town);
        double h = 0;
        foreach (var p in Of(town))
        {
            double dx = x - p.X, dy = y - p.Y, d2 = dx * dx + dy * dy, reach = p.R * 1.4 + r.RampLength + 2;
            if (d2 > reach * reach) continue;
            double d = Math.Sqrt(d2), a = Math.Atan2(dy, dx), edge = p.EdgeAt(a), s = edge - d;
            double v = p.H * Smooth(-r.Face / 2, r.Face / 2, s);
            foreach (double ra in p.Ramps)
            {
                // Along the ramp's line: how far out past the edge, and how far to one side.
                double cx = Math.Cos(ra), cy = Math.Sin(ra), along = dx * cx + dy * cy, side = Math.Abs(-dx * cy + dy * cx);
                double out_ = along - p.EdgeAt(ra);
                if (out_ < -1 || out_ > r.RampLength || side > r.RampWidth + 0.6) continue;
                double rise = p.H * Math.Clamp(1 - out_ / r.RampLength, 0, 1);
                v = Math.Max(v, rise * (1 - Smooth(r.RampWidth, r.RampWidth + 0.6, side)));
            }
            h = Math.Max(h, v);
        }
        return h;
    }

    private static double Lerp(double[] range, double t) => range[0] + (range[^1] - range[0]) * t;

    private static double Diff(double a, double b)
    {
        double d = (a - b) % (Math.PI * 2);
        if (d > Math.PI) d -= Math.PI * 2;
        if (d < -Math.PI) d += Math.PI * 2;
        return d;
    }

    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    private static double Smooth(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Hash(int seed, int i)
    {
        uint h = (uint)seed * 3266489917u ^ (uint)(i + 911) * 2246822519u;
        h ^= h >> 15;
        h *= 2654435761u;
        h ^= h >> 13;
        return (h & 0xffffff) / (double)0x1000000;
    }
}
