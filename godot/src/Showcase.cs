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
    private static bool Open(Town t, int x, int y, int w, int h)
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

        // Round the town: orchards and hives by the fields, fenced; a drill yard by the gate; trees along the road.
        var hall = t.Buildings[0];
        double c = Town.Grid / 2.0;
        double edge = Town.Grid + 0.8;
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                Try(sim, "fruit_tree", edge + i * 0.55, c - 2 + j * 0.6, i * 1.3 + j);
        sim.Decorate("beehive", edge + 2.5, c - 2.1, 0.4);
        sim.Decorate("beehive", edge + 2.5, c - 1.4, 0.9);
        for (int i = 0; i < 8; i++) Try(sim, "fence", edge - 0.3 + i * 0.5, c - 2.6, 0);
        for (int i = 0; i < 8; i++) Try(sim, "fence", edge - 0.3 + i * 0.5, c + 0.2, 0);
        Try(sim, "scarecrow", edge + 3.2, c - 1, 0.5);
        Try(sim, "haystack", edge + 3.1, c - 0.2, 0.3);

        double yard = -1.2;
        Try(sim, "training_dummy", c - 1, yard, 0);
        Try(sim, "training_dummy", c - 0.5, yard - 0.1, 0.2);
        Try(sim, "archery_butts", c + 0.4, yard - 0.5, 0);
        Try(sim, "archery_butts", c + 1.1, yard - 0.5, 0);
        Try(sim, "brazier", c - 1.5, yard - 0.6, 0);
        Try(sim, "stocks", c + 2, yard, 0.1);
        Try(sim, "notice_board", c - 0.2, -0.45, 0);
        Try(sim, "market_stall", c + 0.9, -0.5, 0);
        Try(sim, "market_stall", c + 1.7, -0.5, 0);
        Try(sim, "cart", c + 2.6, -0.6, 0.3);
        Try(sim, "crates", c + 2.3, -0.25, 0.2);
        Try(sim, "well", c - 1.4, -0.55, 0);

        for (int i = 0; i < 14; i++)
        {
            Try(sim, i % 3 == 0 ? "birch" : "oak", -0.7, 1 + i * 1.05, i * 0.9);
            Try(sim, i % 2 == 0 ? "pine" : "oak", Town.Grid + 0.6, c + 2 + i * 0.5, i * 1.7);
        }
        for (int i = 0; i < 10; i++) Try(sim, "bush", -0.35, 1.5 + i * 1.3, i);
        Try(sim, "obelisk", -0.9, c, 0);
        Try(sim, "column", -0.9, c - 1.2, 0);
        Try(sim, "column", -0.9, c + 1.2, 0);
        Try(sim, "bandstand", c, Town.Grid + 1.1, Math.PI);
        Try(sim, "founder_statue", c - 1.6, Town.Grid + 1, Math.PI);
        for (int i = 0; i < 6; i++) Try(sim, "lamp", c - 3 + i * 1.2, Town.Grid + 0.35, 0);
    }

    private static void Try(Simulation sim, string id, double x, double y, double angle)
    {
        if (sim.Decorate(id, x, y, angle) != null) return;
        var t = sim.Town;
        if (Decor.Spot(t, t.Content.Decor.Piece(id), x, y, within: 0.8) is { } s) sim.Decorate(id, s.X, s.Y, angle);
    }
}
