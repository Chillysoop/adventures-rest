using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Client;

/// <summary>
/// For screenshots and clips (--beautify D): plays the part of a player who cares how the town
/// looks. On day D it keeps a square and a garden open by the hall, planting their edges so
/// nobody builds there; on the last day it dresses them (a fountain ringed with benches and
/// lamps, a statue garden) and the land round the town (orchards, fenced fields, a drill yard).
/// Everything goes through the same calls the decoration tool uses.
/// </summary>
public static class Showcase
{
    /// <summary>Open ground (unpainted, unbuilt, off the lanes) over the whole w by h tiles from (x, y).</summary>
    public static bool Open(Town t, int x, int y, int w, int h)
    {
        var plan = t.Plan;
        for (int cy = y * Land.Res; cy < (y + h) * Land.Res; cy++)
            for (int cx = x * Land.Res; cx < (x + w) * Land.Res; cx++)
            {
                if (cx < 0 || cy < 0 || cx >= Land.Size || cy >= Land.Size) return false;
                int i = cy * Land.Size + cx;
                if (plan.Zones[i] != (byte)Zone.None || plan.Taken[i] != 0 || plan.Paved[i] != 0) return false;
            }
        return City.IsOpen(t, x + w / 2.0, y + h / 2.0);
    }

    /// <summary>Ground set aside on the first pass: the square (2 by 2 tiles) and the garden (2 by 1), by their corners.</summary>
    private static ((int X, int Y) Square, (int X, int Y)? Garden)? Find(Town t)
    {
        double c = Town.Center;
        var spots = new List<(int X, int Y)>();
        for (int y = 0; y < Town.Grid - 1; y++)
            for (int x = 0; x < Town.Grid - 1; x++)
                if (Open(t, x, y, 2, 2)) spots.Add((x, y));
        if (spots.Count == 0) return null;
        var (sx, sy) = spots.OrderBy(p => Math.Abs(p.X + 1 - c) + Math.Abs(p.Y + 1 - c)).ThenBy(p => p.Y).ThenBy(p => p.X).First();
        var garden = new List<(int X, int Y)>();
        for (int y = 0; y < Town.Grid; y++)
            for (int x = 0; x < Town.Grid - 1; x++)
                if (Open(t, x, y, 2, 1) && (x > sx + 2 || x < sx - 2 || y > sy + 2 || y < sy - 1)) garden.Add((x, y));
        if (garden.Count == 0) return ((sx, sy), null);
        return ((sx, sy), garden.OrderBy(p => Math.Abs(p.X + 1 - c) + Math.Abs(p.Y + 0.5 - c)).ThenBy(p => p.Y).ThenBy(p => p.X).First());
    }

    private static (int X, int Y)? _square, _garden;

    /// <summary>First pass: keep the land open (painted for the council's own use, so no one else builds there) and plant its edges.</summary>
    public static void Reserve(Simulation sim)
    {
        var t = sim.Town;
        t.Treasury += 3000;
        if (Find(t) is not var (square, garden)) return;
        _square = square;
        _garden = garden;
        Land.Brush(t, [(square.X + 1, square.Y + 1)], 1.3, Zone.Civic, overwrite: false);
        if (garden is var (gx0, gy0)) Land.Brush(t, [(gx0 + 0.6, gy0 + 0.5), (gx0 + 1.4, gy0 + 0.5)], 0.7, Zone.Civic, overwrite: false);
        double x0 = square.X, y0 = square.Y;
        foreach (var (x, y) in new[] { (0.22, 0.22), (1.78, 0.22), (0.22, 1.78), (1.78, 1.78) })
            sim.Decorate("flowers", x0 + x, y0 + y, 0);
        if (garden is var (gx, gy))
        {
            for (int i = 0; i < 4; i++) sim.Decorate("hedge", gx + 0.25 + i * 0.5, gy + 0.08, 0);
            for (int i = 0; i < 4; i++) sim.Decorate("hedge", gx + 0.25 + i * 0.5, gy + 0.92, 0);
        }
    }

    /// <summary>Second pass: dress the square, the garden and the land round the town.</summary>
    public static void Dress(Simulation sim)
    {
        var t = sim.Town;
        t.Treasury += 6000;
        if (_square is var (sqx, sqy))
        {
            double cx = sqx + 1, cy = sqy + 1;
            sim.Decorate("fountain", cx, cy, 0);
            for (int i = 0; i < 6; i++)
            {
                double a = i * Math.PI / 3 + Math.PI / 6;
                double bx = cx + Math.Sin(a) * 0.78, by = cy + Math.Cos(a) * 0.78;
                sim.Decorate("bench", bx, by, Decor.NormalAngle(a + Math.PI));
            }
            foreach (double a in new[] { 0, Math.PI / 2, Math.PI, 3 * Math.PI / 2 })
                sim.Decorate("lamp", cx + Math.Sin(a) * 0.82, cy + Math.Cos(a) * 0.82, 0);
            sim.Decorate("banner", cx - 0.55, cy - 0.55, 0.8);
            sim.Decorate("banner", cx + 0.55, cy + 0.55, 0.8);
        }
        if (_garden is var (gx, gy))
        {
            Try(sim, "god_statue", gx + 1, gy + 0.5, Math.PI);
            Try(sim, "wayside_shrine", gx + 0.3, gy + 0.5, Math.PI / 2);
            Try(sim, "hero_statue", gx + 1.7, gy + 0.5, 3 * Math.PI / 2);
            sim.Decorate("bench", gx + 0.62, gy + 0.32, 0);
            sim.Decorate("bench", gx + 1.38, gy + 0.32, 0);
        }

        // Before the hall, in the open middle of its square: a fountain ringed with benches and lamps.
        var hall = t.Lot(t.Buildings[0].LotId);
        double fx = -Math.Sin(hall.Yaw), fy = Math.Cos(hall.Yaw), out0 = hall.D / (2.0 * Land.Res) + 0.85;
        double hx = hall.X + fx * out0, hy = hall.Y + fy * out0;
        // Lanes cross the square from the hall's door: the fountain takes the nearest clear ground.
        if (Decor.Spot(t, t.Content.Decor.Piece("fountain"), hx, hy, within: 2.5) is { } fs) (hx, hy) = fs;
        sim.Decorate("fountain", hx, hy, hall.Yaw);
        for (int i = 0; i < 4; i++)
        {
            double a = hall.Yaw + Math.PI / 4 + i * Math.PI / 2;
            Try(sim, "bench", hx + Math.Sin(a) * 0.72, hy + Math.Cos(a) * 0.72, Decor.NormalAngle(a + Math.PI));
            Try(sim, "lamp", hx + Math.Sin(a + Math.PI / 4) * 0.8, hy + Math.Cos(a + Math.PI / 4) * 0.8, 0);
        }

        // Round the town, just past the last houses: orchards and hives, fenced; a drill yard; a market by the road; a ring of trees.
        double c = Town.Center;
        double reach = t.Buildings.Max(b => Math.Sqrt((t.Lot(b.LotId).X - c) * (t.Lot(b.LotId).X - c) + (t.Lot(b.LotId).Y - c) * (t.Lot(b.LotId).Y - c)));
        (double X, double Y) At(double a, double extra) => (c + Math.Cos(a) * (reach + extra), c + Math.Sin(a) * (reach + extra));
        // Along and across the ring at an angle: u outward, v around.
        (double X, double Y) Off((double X, double Y) p, double a, double u, double v) => (p.X + Math.Cos(a) * u - Math.Sin(a) * v, p.Y + Math.Sin(a) * u + Math.Cos(a) * v);

        double ao = 0;
        var orchard = At(ao, 1.6);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 4; j++)
            {
                var p = Off(orchard, ao, i * 0.6, (j - 1.5) * 0.6);
                Try(sim, "fruit_tree", p.X, p.Y, i * 1.3 + j);
            }
        for (int j = 0; j < 7; j++)
        {
            var p = Off(orchard, ao, -0.5, (j - 3) * 0.5);
            Try(sim, "fence", p.X, p.Y, Decor.NormalAngle(-ao - Math.PI / 2));
        }
        foreach (var (u, v, id) in new[] { (1.8, -1.0, "beehive"), (1.8, -0.4, "beehive"), (1.9, 0.6, "scarecrow"), (1.7, 1.3, "haystack") })
        {
            var p = Off(orchard, ao, u, v);
            Try(sim, id, p.X, p.Y, u + v);
        }

        double ay = -Math.PI / 2;
        var yard = At(ay, 1.4);
        foreach (var (u, v, id) in new[] { (0.0, -1.0, "training_dummy"), (0.1, -0.5, "training_dummy"), (0.5, 0.4, "archery_butts"), (0.5, 1.1, "archery_butts"), (-0.3, -1.5, "brazier"), (-0.2, 1.8, "stocks") })
        {
            var p = Off(yard, ay, u, v);
            Try(sim, id, p.X, p.Y, Decor.NormalAngle(Math.PI / 2 - ay));
        }

        double am = Math.PI / 2;
        var market = At(am, 1.0);
        foreach (var (u, v, id) in new[] { (0.0, -1.2, "notice_board"), (0.0, -0.4, "market_stall"), (0.0, 0.5, "market_stall"), (0.3, 1.3, "cart"), (-0.3, 1.2, "crates"), (0.5, -1.4, "well") })
        {
            var p = Off(market, am, u, v);
            Try(sim, id, p.X, p.Y, Decor.NormalAngle(Math.PI / 2 - am + Math.PI));
        }

        var square = At(Math.PI, 1.3);
        Try(sim, "founder_statue", square.X, square.Y, Math.PI / 2);
        foreach (var (u, v, id) in new[] { (0.0, -1.0, "column"), (0.0, 1.0, "column"), (0.9, 0.0, "obelisk"), (-0.2, 2.0, "bandstand") })
        {
            var p = Off(square, Math.PI, u, v);
            Try(sim, id, p.X, p.Y, Math.PI / 2);
        }

        for (int i = 0; i < 40; i++)
        {
            double a = i * Math.PI * 2 / 40 + 0.04;
            var p = At(a, 3.4 + Geo(i) * 0.6);
            Try(sim, i % 4 == 0 ? "birch" : i % 3 == 0 ? "pine" : "oak", p.X, p.Y, i * 0.9);
            if (i % 2 == 0)
            {
                var q = At(a + 0.05, 2.8);
                Try(sim, "bush", q.X, q.Y, i);
            }
        }
    }

    private static double Geo(int i) => Map.Geo.Hash(i * 13 + 5);

    private static void Try(Simulation sim, string id, double x, double y, double angle)
    {
        if (sim.Decorate(id, x, y, angle) != null) return;
        var t = sim.Town;
        if (Decor.Spot(t, t.Content.Decor.Piece(id), x, y, within: 1.2) is { } s) sim.Decorate(id, s.X, s.Y, angle);
    }
}
