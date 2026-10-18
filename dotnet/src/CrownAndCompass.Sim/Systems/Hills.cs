namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The lie of the land the town stands on: a few gentle hills and ridges from the town's seed, flat
/// round the hall where the charter set it. Streets wind round the slopes and buildings step up them
/// (the client props the low side on stilts or a plinth); ground too steep stays open.
/// Pure function of the seed: nothing is saved, and it draws nothing from the town's rng.
/// </summary>
public static class Hills
{
    /// <summary>Most a footprint's ground may rise from its lowest corner to its highest, in tiles of height.</summary>
    public const double MaxSpan = 0.45;

    private sealed record Hill(double X, double Y, double H, double Rx, double Ry, double Angle);

    private static int _seed = int.MinValue;
    private static Hill[] _hills = [];
    private static readonly object Gate = new();

    private static Hill[] Of(int seed)
    {
        lock (Gate)
        {
            if (seed == _seed) return _hills;
            var hills = new List<Hill>();
            uint s = (uint)seed * 2654435761u + 0x9e3779b9u;
            double Next()
            {
                s ^= s << 13;
                s ^= s >> 17;
                s ^= s << 5;
                return (s & 0xffffff) / (double)0x1000000;
            }
            for (int i = 0; i < 40 && hills.Count < 9; i++)
            {
                double a = Next() * Math.PI * 2, d = 10 + Next() * 16;
                double x = Town.Center + Math.Cos(a) * d, y = Town.Center + Math.Sin(a) * d;
                if (hills.Any(h => Math.Abs(h.X - x) + Math.Abs(h.Y - y) < 5)) continue;
                // Some round knolls, some long ridges.
                double r = 2.6 + Next() * 3.6, stretch = Next() < 0.4 ? 1.8 + Next() : 1;
                hills.Add(new Hill(x, y, 0.7 + Next() * 1.4, r * stretch, r, Next() * Math.PI));
            }
            _hills = [.. hills];
            _seed = seed;
            return _hills;
        }
    }

    /// <summary>Ground height above the town's level at (x, y) in tiles.</summary>
    public static double Height(Town town, double x, double y) => Height(town.Seed, x, y);

    public static double Height(int seed, double x, double y)
    {
        double h = 0;
        foreach (var k in Of(seed))
        {
            double dx = x - k.X, dy = y - k.Y, c = Math.Cos(k.Angle), s = Math.Sin(k.Angle);
            double u = (dx * c + dy * s) / k.Rx, v = (-dx * s + dy * c) / k.Ry;
            double q = u * u + v * v;
            if (q < 9) h += k.H * Math.Exp(-q);
        }
        double dc = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
        // The square is level.
        return h * Smooth(4, 10, dc);
    }

    private static double Smooth(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Height at the middle of every plan cell, row by row (worked out once per town).</summary>
    public static float[] Grid(Town town)
    {
        var plan = town.Plan;
        if (plan.Heights is { } g) return g;
        g = new float[Land.Size * Land.Size];
        for (int i = 0; i < g.Length; i++) g[i] = (float)Height(town, (i % Land.Size + 0.5) / Land.Res, (i / Land.Size + 0.5) / Land.Res);
        return plan.Heights = g;
    }

    /// <summary>How far the ground rises across a footprint centred at (x, y), from its lowest corner to its highest.</summary>
    public static double Span(Town town, double x, double y, int w, int d, double yaw)
    {
        double hw = w / (2.0 * Land.Res), hd = d / (2.0 * Land.Res), s = Math.Sin(yaw), c = Math.Cos(yaw);
        double lo = double.MaxValue, hi = double.MinValue;
        for (int i = 0; i < 9; i++)
        {
            double u = (i % 3 - 1) * hw, v = (i / 3 - 1) * hd;
            double h = Height(town, x + u * c - v * s, y + u * s + v * c);
            lo = Math.Min(lo, h);
            hi = Math.Max(hi, h);
        }
        return hi - lo;
    }
}
