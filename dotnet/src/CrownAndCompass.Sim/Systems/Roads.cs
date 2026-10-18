namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The town's streets, after Foundation: never drawn by the player, they wear in where people need
/// to go. The charter leaves an open square before the hall. Whenever painted land lies too far from
/// any street, a lane is worn from the middle of that land to the nearest street by the easiest way
/// on foot (round hills, between buildings, with a little wander), and on to a second street when that
/// saves a long way round, so the lanes knit into a web rather than spokes.
/// Buildings front onto the streets (<see cref="Land"/>).
/// </summary>
public static class Roads
{
    /// <summary>Width of a lane, in tiles.</summary>
    public const double Width = 0.5;

    /// <summary>Open ground between a street's edge and the front of a building, in tiles.</summary>
    public const double Verge = 0.15;

    /// <summary>Distance from a street's middle to the middle of a building of depth <paramref name="d"/> cells fronting on it.</summary>
    public static double Setback(int d) => Width / 2 + Verge + d / (2.0 * Land.Res);

    /// <summary>The open square before the hall: a rough round of trodden ground, its rim a street buildings front onto.</summary>
    public static void Square(Town town, Lot hall)
    {
        double r = Math.Sqrt(hall.W * hall.W + hall.D * hall.D) / (2.0 * Land.Res) + 0.9;
        const int n = 24;
        var pts = new double[(n + 1) * 2];
        for (int i = 0; i <= n; i++)
        {
            double a = Math.PI * 2 * (i % n) / n;
            // Not a circle: wider toward the hall's front, and a little uneven.
            double rr = r * (1 + 0.22 * Math.Max(0, Math.Sin(a)) + (Land.Jitter(i % n + 7) - 0.5) * 0.16);
            pts[i * 2] = hall.X + Math.Cos(a) * rr;
            pts[i * 2 + 1] = hall.Y + Math.Sin(a) * rr;
        }
        var plan = town.Plan;
        var road = new Road { Id = plan.Roads.Count, Points = pts, Square = true };
        plan.Roads.Add(road);
        // Everything inside the rim is trodden open ground.
        for (int i = 0; i < plan.Paved.Length; i++)
        {
            double x = (i % Land.Size + 0.5) / Land.Res, y = (i / Land.Size + 0.5) / Land.Res;
            if (Inside(pts, x, y) && plan.Taken[i] == 0) plan.Paved[i] = 1;
        }
        for (int s = 0; s + 3 < pts.Length; s += 2) Stamp(plan, pts[s], pts[s + 1], pts[s + 2], pts[s + 3]);
        plan.Changed();
    }

    private static bool Inside(double[] pts, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = pts.Length / 2 - 1; i < pts.Length / 2; j = i++)
        {
            double xi = pts[i * 2], yi = pts[i * 2 + 1], xj = pts[j * 2], yj = pts[j * 2 + 1];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    /// <summary>Put a street on the plan.</summary>
    public static Road Lay(Town town, double[] points)
    {
        var plan = town.Plan;
        var road = new Road { Id = plan.Roads.Count, Points = points };
        plan.Roads.Add(road);
        for (int s = 0; s + 3 < points.Length; s += 2) Stamp(plan, points[s], points[s + 1], points[s + 2], points[s + 3]);
        plan.Changed();
        return road;
    }

    /// <summary>Mark the cells a stretch of street covers.</summary>
    private static void Stamp(LandPlan plan, double ax, double ay, double bx, double by)
    {
        double h = Width / 2 * Land.Res;
        ax *= Land.Res; ay *= Land.Res; bx *= Land.Res; by *= Land.Res;
        int x0 = Math.Max(0, (int)Math.Floor(Math.Min(ax, bx) - h)), x1 = Math.Min(Land.Size - 1, (int)Math.Ceiling(Math.Max(ax, bx) + h));
        int y0 = Math.Max(0, (int)Math.Floor(Math.Min(ay, by) - h)), y1 = Math.Min(Land.Size - 1, (int)Math.Ceiling(Math.Max(ay, by) + h));
        for (int cy = y0; cy <= y1; cy++)
            for (int cx = x0; cx <= x1; cx++)
                // Round a building's corner, never through it.
                if (plan.Taken[cy * Land.Size + cx] == 0 && SegmentDistance(cx + 0.5, cy + 0.5, ax, ay, bx, by).D <= h) plan.Paved[cy * Land.Size + cx] = 1;
    }

    /// <summary>Distance from a point to a segment, and the nearest point on it.</summary>
    private static (double D, double X, double Y) SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax, dy = by - ay, len2 = dx * dx + dy * dy;
        double t = len2 < 1e-12 ? 0 : Math.Clamp(((px - ax) * dx + (py - ay) * dy) / len2, 0, 1);
        double x = ax + dx * t, y = ay + dy * t;
        return (Math.Sqrt((px - x) * (px - x) + (py - y) * (py - y)), x, y);
    }

    /// <summary>The nearest point on any street, in tiles, or null before the town has one.</summary>
    public static (double D, double X, double Y)? Nearest(Town town, double x, double y)
    {
        (double D, double X, double Y)? best = null;
        foreach (var r in town.Plan.Roads)
            for (int s = 0; s + 3 < r.Points.Length; s += 2)
            {
                var hit = SegmentDistance(x, y, r.Points[s], r.Points[s + 1], r.Points[s + 2], r.Points[s + 3]);
                if (best == null || hit.D < best.Value.D) best = hit;
            }
        return best;
    }

    /// <summary>The heading that turns a building's front to the nearest street, or to the hall when no street is near.</summary>
    public static double Facing(Town town, double x, double y)
    {
        if (Nearest(town, x, y) is { } n && n.D < 4 && n.D > 1e-6) return Land.FaceToward(x, y, n.X, n.Y);
        return Land.FaceHall(x, y);
    }

    /// <summary>How far back from the verge a building may stand, in tiles.</summary>
    private static readonly double[] Yards = [0, 0.3, 0.6];

    /// <summary>Spots along the streets where a building d cells deep could front on one: centre, heading, and a score (nearer the hall first).</summary>
    public static List<(double Score, double X, double Y, double Yaw)> Frontage(Town town, int d, Zone? zone)
    {
        var plan = town.Plan;
        var spots = new List<(double, double, double, double)>();
        const double step = 0.3;
        foreach (var r in plan.Roads)
            for (int s = 0; s + 3 < r.Points.Length; s += 2)
            {
                double ax = r.Points[s], ay = r.Points[s + 1], bx = r.Points[s + 2], by = r.Points[s + 3];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (len < 1e-6) continue;
                double ux = (bx - ax) / len, uy = (by - ay) / len;
                for (int k = 0; k * step < len; k++)
                    for (int side = -1; side <= 1; side += 2)
                        for (int yard = 0; yard < Yards.Length; yard++)
                        {
                            int key = (((r.Id * 97 + s) * 211 + k) * 2 + (side + 1) / 2) * 4 + yard;
                            // Out from the street to the building's middle, behind a front yard if the verge is taken;
                            // its front looks back at the street, give or take, as houses on a lane do.
                            double nx = -uy * side, ny = ux * side;
                            double off = Setback(d) + Yards[yard] + (Land.Jitter(key * 3 + 1) - 0.5) * 0.1;
                            double x = ax + ux * k * step + nx * off, y = ay + uy * k * step + ny * off;
                            int cx = (int)Math.Floor(x * Land.Res), cy = (int)Math.Floor(y * Land.Res);
                            if (cx < 0 || cy < 0 || cx >= Land.Size || cy >= Land.Size) continue;
                            int i = cy * Land.Size + cx;
                            if (plan.Taken[i] != 0 || plan.Paved[i] != 0) continue;
                            if (zone is Zone z ? plan.Zones[i] != (byte)z : plan.Zones[i] is not ((byte)Zone.None or (byte)Zone.Civic)) continue;
                            double dist = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
                            double yaw = Math.Atan2(nx, -ny) + (Land.Jitter(key * 3 + 2) - 0.5) * 0.14;
                            spots.Add((dist + Land.Jitter(key) * 1.2 + Yards[yard] * 3, x, y, yaw));
                        }
            }
        return spots;
    }

    // Wearing in new lanes -------------------------------------------------------------------------

    /// <summary>
    /// Wear lanes out to painted land that no street reaches yet, as many as it takes (up to <paramref name="max"/>).
    /// Returns how many were laid.
    /// </summary>
    public static int Serve(Town town, int max = 16)
    {
        var plan = town.Plan;
        if (plan.Roads.Count == 0) return 0;
        var skip = new HashSet<int>();
        var gap = new double[8];
        foreach (var z in Planning.Paintable) gap[(int)z] = Gap(town, z);
        int laid = 0, n = Land.Size;
        for (int tries = 0; tries < max * 2 && laid < max; tries++)
        {
            var field = Field(plan);
            // Painted ground far enough from every street to hold a lane with buildings along it, and how much of it lies around each cell.
            var sum = new int[(n + 1) * (n + 1)];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    int i = y * n + x;
                    byte z = plan.Zones[i];
                    int u = z is (byte)Zone.None or (byte)Zone.Civic || plan.Taken[i] != 0 || plan.Paved[i] != 0 || field[i] < gap[z] ? 0 : 1;
                    sum[(y + 1) * (n + 1) + x + 1] = u + sum[y * (n + 1) + x + 1] + sum[(y + 1) * (n + 1) + x] - sum[y * (n + 1) + x];
                }
            int Around(int x, int y, int r)
            {
                int x0 = Math.Max(0, x - r), y0 = Math.Max(0, y - r), x1 = Math.Min(n, x + r + 1), y1 = Math.Min(n, y + r + 1);
                return sum[y1 * (n + 1) + x1] - sum[y0 * (n + 1) + x1] - sum[y1 * (n + 1) + x0] + sum[y0 * (n + 1) + x0];
            }
            int target = -1;
            double best = double.MinValue;
            for (int i = 0; i < field.Length; i++)
            {
                int x = i % n, y = i / n;
                if (Around(x, y, 0) == 0 || skip.Contains(i) || Around(x, y, 2) < 10) continue;
                // The middle of the widest stretch first, so each lane splits what's left.
                double k = field[i] + Land.Jitter(i) * 4;
                if (k > best)
                {
                    best = k;
                    target = i;
                }
            }
            if (target < 0) break;
            var path = Route(town, field, target, null);
            if (path == null)
            {
                // No way there: leave this bit of paint be.
                for (int cy = target / n - Land.Res; cy <= target / n + Land.Res; cy++)
                    for (int cx = target % n - Land.Res; cx <= target % n + Land.Res; cx++)
                        if (cx >= 0 && cy >= 0 && cx < n && cy < n) skip.Add(cy * n + cx);
                continue;
            }
            var before = (byte[])plan.Paved.Clone();
            Lay(town, Smooth(path));
            laid++;
            // On through to another street, if that's a short way and saves going round: lanes knit into a web.
            int join = path[^1];
            double jx = (join % n + 0.5) / Land.Res, jy = (join / n + 0.5) / Land.Res;
            var on = Land.Jitter(target * 5 + 3) > 0.5 ? null : Route(town, Field(plan), target, c =>
            {
                double x = (c % n + 0.5) / Land.Res - jx, y = (c / n + 0.5) / Land.Res - jy;
                return before[c] != 0 && x * x + y * y > 16;
            }, 9);
            if (on != null && on.Count > 4)
            {
                Lay(town, Smooth(on));
                laid++;
            }
        }
        return laid;
    }

    /// <summary>
    /// How far (in cells, from a street's edge) paint must lie before it wants a lane: past the backs of
    /// the zone's deepest buildings, with room for half a lane and a lane's verge beyond.
    /// </summary>
    private static double Gap(Town town, Zone zone)
    {
        int d = 3;
        foreach (var b in town.Content.Buildings)
            if (b.Zone == zone && b.Buildable && b.Wards.Count == 0) d = Math.Max(d, Land.Footprint(b).D);
        return (Verge + d / (double)Land.Res + Width / 2) * Land.Res + 2.5;
    }

    /// <summary>Distance in cells from every cell to the nearest paved one (a two-pass chamfer).</summary>
    public static float[] Field(LandPlan plan)
    {
        int n = Land.Size;
        var f = new float[n * n];
        for (int i = 0; i < f.Length; i++) f[i] = plan.Paved[i] != 0 ? 0 : 1e6f;
        const float a = 1, b = 1.4142f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float v = f[i];
                if (x > 0) v = Math.Min(v, f[i - 1] + a);
                if (y > 0)
                {
                    v = Math.Min(v, f[i - n] + a);
                    if (x > 0) v = Math.Min(v, f[i - n - 1] + b);
                    if (x < n - 1) v = Math.Min(v, f[i - n + 1] + b);
                }
                f[i] = v;
            }
        for (int y = n - 1; y >= 0; y--)
            for (int x = n - 1; x >= 0; x--)
            {
                int i = y * n + x;
                float v = f[i];
                if (x < n - 1) v = Math.Min(v, f[i + 1] + a);
                if (y < n - 1)
                {
                    v = Math.Min(v, f[i + n] + a);
                    if (x < n - 1) v = Math.Min(v, f[i + n + 1] + b);
                    if (x > 0) v = Math.Min(v, f[i + n - 1] + b);
                }
                f[i] = v;
            }
        return f;
    }

    private static readonly (int Dx, int Dy, double Len)[] Steps =
        [(1, 0, 1), (-1, 0, 1), (0, 1, 1), (0, -1, 1), (1, 1, 1.4142), (1, -1, 1.4142), (-1, 1, 1.4142), (-1, -1, 1.4142)];

    /// <summary>
    /// The easiest way on foot from a cell to a street (a cell of one passing <paramref name="goal"/>, if given), as cells from start to street:
    /// climbing costs, as does crowding a street it runs beside or a building's walls, and ground differs a little cell to cell
    /// so lanes wander. Null when there's no way within <paramref name="most"/> tiles.
    /// </summary>
    private static List<int>? Route(Town town, float[] field, int start, Func<int, bool>? goal, double most = 30)
    {
        var plan = town.Plan;
        int n = Land.Size;
        var heights = Terrain.Grid(town);
        var cost = new double[n * n];
        Array.Fill(cost, double.MaxValue);
        var from = new int[n * n];
        var queue = new PriorityQueue<int, (double, int)>();
        cost[start] = 0;
        queue.Enqueue(start, (0, start));
        double limit = most * Land.Res * 2.2;
        while (queue.TryDequeue(out int i, out var pri))
        {
            if (pri.Item1 > cost[i]) continue;
            if (pri.Item1 > limit) return null;
            int x = i % n, y = i / n;
            if (plan.Paved[i] != 0 && i != start && (goal == null || goal(i)))
            {
                var path = new List<int>();
                for (int c = i; c != start; c = from[c]) path.Add(c);
                path.Add(start);
                path.Reverse();
                return path;
            }
            foreach (var (dx, dy, len) in Steps)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1) continue;
                int j = ny * n + nx;
                if (Blocked(plan, nx, ny)) continue;
                double grade = Math.Abs(heights[j] - heights[i]) * Land.Res / len;
                double step = len * (1 + 30 * grade * grade + Land.Jitter(j) * 0.9);
                // A lane keeps its distance from the street it would run beside, then meets it square on.
                if (plan.Paved[j] == 0 && field[j] < 6) step *= 1 + (6 - field[j]) * 0.35;
                if (!City.IsOpen(town, (nx + 0.5) / Land.Res, (ny + 0.5) / Land.Res)) step *= 3;
                double c = cost[i] + step;
                if (c < cost[j])
                {
                    cost[j] = c;
                    from[j] = i;
                    queue.Enqueue(j, (c, j));
                }
            }
        }
        return null;
    }

    /// <summary>Whether a lane can't pass through this cell: a building on it or right beside it.</summary>
    private static bool Blocked(LandPlan plan, int x, int y)
    {
        int n = Land.Size;
        for (int yy = y - 1; yy <= y + 1; yy++)
            for (int xx = x - 1; xx <= x + 1; xx++)
                if (plan.Taken[yy * n + xx] != 0) return true;
        return false;
    }

    /// <summary>A path of cells as a smooth line: straightened where it zigzags on the raster, then rounded at the bends.</summary>
    private static double[] Smooth(List<int> cells)
    {
        var pts = cells.Select(c => ((c % Land.Size + 0.5) / Land.Res, (c / Land.Size + 0.5) / Land.Res)).ToList();
        pts = Simplify(pts, 0.18);
        for (int pass = 0; pass < 2; pass++)
        {
            var next = new List<(double, double)> { pts[0] };
            for (int i = 0; i + 1 < pts.Count; i++)
            {
                var (ax, ay) = pts[i];
                var (bx, by) = pts[i + 1];
                next.Add((ax * 0.75 + bx * 0.25, ay * 0.75 + by * 0.25));
                next.Add((ax * 0.25 + bx * 0.75, ay * 0.25 + by * 0.75));
            }
            next.Add(pts[^1]);
            pts = next;
        }
        var flat = new double[pts.Count * 2];
        for (int i = 0; i < pts.Count; i++) (flat[i * 2], flat[i * 2 + 1]) = pts[i];
        return flat;
    }

    /// <summary>Douglas-Peucker: drop points within <paramref name="tolerance"/> tiles of the line through their neighbours.</summary>
    private static List<(double X, double Y)> Simplify(List<(double X, double Y)> pts, double tolerance)
    {
        if (pts.Count < 3) return pts;
        double worst = 0;
        int at = 0;
        var (ax, ay) = pts[0];
        var (bx, by) = pts[^1];
        for (int i = 1; i < pts.Count - 1; i++)
        {
            double d = SegmentDistance(pts[i].X, pts[i].Y, ax, ay, bx, by).D;
            if (d > worst)
            {
                worst = d;
                at = i;
            }
        }
        if (worst <= tolerance) return [pts[0], pts[^1]];
        var left = Simplify(pts[..(at + 1)], tolerance);
        var right = Simplify(pts[at..], tolerance);
        return [.. left[..^1], .. right];
    }
}

/// <summary>A street: a line through points in tiles, x then y. The square's is its rim, a closed loop.</summary>
public sealed class Road
{
    public int Id { get; set; }
    public double[] Points { get; set; } = [];
    public bool Square { get; set; }
}
