namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The town's streets, after Foundation: never drawn by the player, they wear in where people need
/// to go. The charter leaves an open square before the hall. Painting lays no streets: houses grow in beside
/// what is built (<see cref="Growth"/>), and the feet going to them wear paths that become lanes (<see cref="Wear"/>),
/// by the easiest way on foot (round hills, between buildings, with a little wander).
/// Buildings front onto the streets where they can (<see cref="Land"/>).
/// </summary>
public static class Streets
{
    /// <summary>Width of a lane, in tiles.</summary>
    public const double Width = 0.4;

    /// <summary>Open ground between a street's edge and the front of a building, in tiles.</summary>
    public const double Verge = 0.1;

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
        var road = new Street { Id = plan.Streets.Count, Points = pts, Square = true };
        plan.Streets.Add(road);
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
    public static Street Lay(Town town, double[] points)
    {
        var plan = town.Plan;
        var road = new Street { Id = plan.Streets.Count, Points = points };
        plan.Streets.Add(road);
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
    internal static (double D, double X, double Y) SegmentDistance(double px, double py, double ax, double ay, double bx, double by)
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
        foreach (var r in town.Plan.Streets)
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
    private static readonly double[] Yards = [0, 0.3, 0.6, 0.9, 1.2];

    /// <summary>
    /// Where a building d cells deep could go in the zone, best first: spots fronting a street (<see cref="Frontage"/>, kept until
    /// a street is laid or paint changes) and, for a painted zone, spots off the streets beside what is already built
    /// (<see cref="Growth"/>), nearest the built town first. Kept until the plan changes.
    /// </summary>
    public static List<(double Score, double X, double Y, double Yaw)> Fronts(Town town, int d, Zone? zone)
    {
        var plan = town.Plan;
        var key = (plan.Streets.Count, plan.Painted);
        if (plan.FrontsKey != key)
        {
            plan.Fronts.Clear();
            plan.FrontsKey = key;
        }
        if (!plan.Fronts.TryGetValue((d, zone), out var list))
            plan.Fronts[(d, zone)] = list = [.. Frontage(town, d, zone).OrderBy(c => c.Score).ThenBy(c => c.X).ThenBy(c => c.Y)];
        if (zone is not Zone z || !Planning.Paintable.Contains(z)) return list;
        if (plan.GrowthSeen != plan.Version)
        {
            plan.Growth.Clear();
            plan.GrowthSeen = plan.Version;
        }
        if (!plan.Growth.TryGetValue((d, z), out var all))
        {
            // Both lists are sorted: merge them.
            var grow = Growth(town, d, z);
            all = new List<(double Score, double X, double Y, double Yaw)>(list.Count + grow.Count);
            int i = 0, j = 0;
            while (i < list.Count || j < grow.Count)
                all.Add(j >= grow.Count || (i < list.Count && list[i].Score <= grow[j].Score) ? list[i++] : grow[j++]);
            plan.Growth[(d, z)] = all;
        }
        return all;
    }

    /// <summary>
    /// Spots off the streets in the zone's open paint where a building d cells deep could stand, the way a town grows when nobody
    /// lays streets for it: nearest what is already built (or a street) first, so a zone fills in from the side the town is on
    /// and spreads on from each new building. A building faces the street when one is near, else the building it grows beside;
    /// the feet going to it wear the lane in later (<see cref="Wear"/>).
    /// </summary>
    public static List<(double Score, double X, double Y, double Yaw)> Growth(Town town, int d, Zone zone)
    {
        var plan = town.Plan;
        var rules = town.Content.City.Growth;
        int n = Land.Size;
        var (field, near) = Built(plan);
        var spots = new List<(double Score, double X, double Y, double Yaw)>();
        // A spot's middle sits this far (cells) from the nearest wall or street at the least: half the building and a lane.
        double least = d / 2.0 + 1;
        // Zoned cells all lie in the box round the town's ground; the walk keeps the full grid's stride so spots stay put.
        var (bx0, by0, bx1, by1) = Land.Bounds(plan, 0);
        for (int cy = Math.Max(1, by0 - 1); cy < Math.Min(n - 1, by1 + 2); cy++)
        {
            if (cy % 2 == 0) continue;
            int first = 1 + (cy / 2 % 2);
            int from = Math.Max(first, bx0 - 1 + Math.Abs((bx0 - 1 - first) % 2));
            for (int cx = from; cx < Math.Min(n - 1, bx1 + 2); cx += 2)
            {
                int i = cy * n + cx;
                if (plan.Zones[i] != (byte)zone || plan.Taken[i] != 0 || plan.Paved[i] != 0 || field[i] < least) continue;
                double x = (cx + 0.5 + (Land.Jitter(i * 5 + 1) - 0.5) * 0.9) / Land.Res, y = (cy + 0.5 + (Land.Jitter(i * 5 + 2) - 0.5) * 0.9) / Land.Res;
                // Facing what it grows beside: the street if that is what's nearest, else the neighbour's door side.
                int k = near[i];
                double yaw = plan.Paved[k] != 0 && Nearest(town, x, y) is { } st && st.D > 1e-6
                    ? Land.FaceToward(x, y, st.X, st.Y)
                    : Land.FaceToward(x, y, (k % n + 0.5) / Land.Res, (k / n + 0.5) / Land.Res);
                yaw += (Land.Jitter(i * 5 + 3) - 0.5) * rules.Turn;
                double dist = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
                double score = (field[i] - least) / Land.Res * rules.Reach + rules.OffStreet + Land.Jitter(i * 5 + 4) * rules.Shuffle + dist * rules.Hall
                    + (zone == Zone.Commercial ? town.Content.City.Markets.OffStreet : 0);
                spots.Add((score, x, y, yaw));
            }
        }
        return [.. spots.OrderBy(c => c.Score).ThenBy(c => c.X).ThenBy(c => c.Y)];
    }

    /// <summary>
    /// Distance in cells from every cell to the nearest built or paved one, and which cell that is (a two-pass chamfer), kept for the
    /// plan's <see cref="LandPlan.Version"/>. Only the box round the town's ground (<see cref="Land.Bounds"/>) is worked out: farther
    /// cells read as far from anything, which is what they are.
    /// </summary>
    private static (float[] Dist, int[] Near) Built(LandPlan plan)
    {
        if (plan.BuiltSeen == plan.Version && plan.BuiltDist is { } kept && plan.BuiltNear is { } keptNear) return (kept, keptNear);
        int n = Land.Size;
        var f = new float[n * n];
        var near = new int[n * n];
        Array.Fill(f, 1e6f);
        var (x0, y0, x1, y1) = Land.Bounds(plan, 8);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * n + x;
                if (plan.Paved[i] != 0 || plan.Taken[i] > 0)
                {
                    f[i] = 0;
                    near[i] = i;
                }
            }
        const float a = 1, b = 1.4142f;
        void Try(int i, int j, float step)
        {
            if (f[j] + step < f[i])
            {
                f[i] = f[j] + step;
                near[i] = near[j];
            }
        }
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * n + x;
                if (x > x0) Try(i, i - 1, a);
                if (y > y0)
                {
                    Try(i, i - n, a);
                    if (x > x0) Try(i, i - n - 1, b);
                    if (x < x1) Try(i, i - n + 1, b);
                }
            }
        for (int y = y1; y >= y0; y--)
            for (int x = x1; x >= x0; x--)
            {
                int i = y * n + x;
                if (x < x1) Try(i, i + 1, a);
                if (y < y1)
                {
                    Try(i, i + n, a);
                    if (x < x1) Try(i, i + n + 1, b);
                    if (x > x0) Try(i, i + n - 1, b);
                }
            }
        plan.BuiltDist = f;
        plan.BuiltNear = near;
        plan.BuiltSeen = plan.Version;
        return (f, near);
    }

    /// <summary>Spots along the streets where a building d cells deep could front on one: centre, heading, and a score (nearer the hall first).</summary>
    public static List<(double Score, double X, double Y, double Yaw)> Frontage(Town town, int d, Zone? zone)
    {
        var plan = town.Plan;
        var spots = new List<(double, double, double, double)>();
        const double step = 0.3;
        var rules = town.Content.City.Growth;
        bool grows = zone is Zone g && Planning.Paintable.Contains(g);
        var markets = town.Content.City.Markets;
        foreach (var r in plan.Streets)
            for (int s = 0; s + 3 < r.Points.Length; s += 2)
            {
                // Shops round a square stand shoulder to shoulder on its rim, each side's facing the same way.
                bool square = r.Square && zone == Zone.Commercial;
                double ax = r.Points[s], ay = r.Points[s + 1], bx = r.Points[s + 2], by = r.Points[s + 3];
                double len = Math.Sqrt((bx - ax) * (bx - ax) + (by - ay) * (by - ay));
                if (len < 1e-6) continue;
                double ux = (bx - ax) / len, uy = (by - ay) / len;
                for (int k = 0; k * step < len; k++)
                    for (int side = -1; side <= 1; side += 2)
                        for (int yard = 0; yard < (square ? 1 : Yards.Length); yard++)
                        {
                            int key = (((r.Id * 97 + s) * 211 + k) * 2 + (side + 1) / 2) * 8 + yard;
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
                            double yaw = Math.Atan2(nx, -ny) + (square ? 0 : (Land.Jitter(key * 3 + 2) - 0.5) * 0.14);
                            if (square) (x, y) = (ax + ux * k * step + nx * Setback(d), ay + uy * k * step + ny * Setback(d));
                            // Painted zones fill from the built town outward (see Growth); the council's works and bastions keep near the hall.
                            spots.Add(grows
                                ? (Land.Jitter(key) * rules.Shuffle + Yards[yard] * rules.Yard + dist * rules.Hall - (square ? markets.Pull : 0), x, y, yaw)
                                : (dist + Land.Jitter(key) * 1.2 + Yards[yard] * 2, x, y, yaw));
                        }
            }
        return spots;
    }

    // Market squares and side lanes -----------------------------------------------------------------

    /// <summary>The middle of a street's points (a square's centre).</summary>
    private static (double X, double Y) Middle(Street r)
    {
        double x = 0, y = 0;
        int m = r.Points.Length / 2;
        for (int i = 0; i < m; i++) (x, y) = (x + r.Points[i * 2], y + r.Points[i * 2 + 1]);
        return (x / m, y / m);
    }

    /// <summary>
    /// Once a day: where shops are painted with room and no square lies near, the market grows a square of its own, a crisp
    /// rectangle of trodden ground turned square to the nearest street and joined to it by a straight high street (unlike the
    /// lanes homes wear, which wander). Shops then front its rim, each side's all facing the same way (<see cref="Frontage"/>).
    /// </summary>
    public static void Markets(Town town)
    {
        var rules = town.Content.City.Markets;
        var plan = town.Plan;
        if (plan.Streets.Count == 0 || Land.OpenCells(town, Zone.Commercial) < rules.Least) return;
        // A new square only once the town keeps enough shops to line the ones it has.
        if (town.Buildings.Count(b => b.Def.Zone == Zone.Commercial) < rules.ShopsEach * (plan.Streets.Count(r => r.Market) + 1)) return;
        var squares = plan.Streets.Where(r => r.Square).Select(Middle).ToList();
        int n = Land.Size, w = (int)Math.Round(rules.Width * Land.Res), d = (int)Math.Round(rules.Depth * Land.Res);
        double best = double.MaxValue;
        (double X, double Y, double Yaw, double SX, double SY)? pick = null;
        for (int cy = 2; cy < n - 2; cy += 2)
            for (int cx = 2; cx < n - 2; cx += 2)
            {
                int i = cy * n + cx;
                if (plan.Zones[i] != (byte)Zone.Commercial || plan.Taken[i] != 0 || plan.Paved[i] != 0) continue;
                double x = (cx + 0.5) / Land.Res, y = (cy + 0.5) / Land.Res;
                if (squares.Any(q => (q.X - x) * (q.X - x) + (q.Y - y) * (q.Y - y) < rules.Spacing * rules.Spacing)) continue;
                if (Nearest(town, x, y) is not { } st || st.D > rules.Reach || st.D < rules.Depth / 2 + 0.6) continue;
                double dist = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center));
                double score = dist + st.D * 0.5 + Land.Jitter(i * 3 + 11) * 1.5;
                if (score >= best) continue;
                // Its depth runs toward the street, so the high street meets its middle.
                double yaw = Land.FaceToward(x, y, st.X, st.Y);
                if (!Plaza(town, x, y, w, d, yaw, st.X, st.Y)) continue;
                best = score;
                pick = (x, y, yaw, st.X, st.Y);
            }
        if (pick is not { } p) return;
        double fx = -Math.Sin(p.Yaw), fy = Math.Cos(p.Yaw), sx = Math.Cos(p.Yaw), sy = Math.Sin(p.Yaw);
        double hw = w / 2.0 / Land.Res, hd = d / 2.0 / Land.Res;
        (double U, double V)[] corners = [(-hw, -hd), (hw, -hd), (hw, hd), (-hw, hd), (-hw, -hd)];
        var pts = new double[corners.Length * 2];
        for (int k = 0; k < corners.Length; k++) (pts[k * 2], pts[k * 2 + 1]) = (p.X + sx * corners[k].U + fx * corners[k].V, p.Y + sy * corners[k].U + fy * corners[k].V);
        var road = new Street { Id = plan.Streets.Count, Points = pts, Square = true, Market = true };
        plan.Streets.Add(road);
        for (int i = 0; i < plan.Paved.Length; i++)
        {
            double x = (i % n + 0.5) / Land.Res, y = (i / n + 0.5) / Land.Res;
            if (Math.Abs(x - p.X) > hw + hd || Math.Abs(y - p.Y) > hw + hd) continue;
            if (Inside(pts, x, y) && plan.Taken[i] == 0) plan.Paved[i] = 1;
        }
        for (int k = 0; k + 3 < pts.Length; k += 2) Stamp(plan, pts[k], pts[k + 1], pts[k + 2], pts[k + 3]);
        // Its rim is the shops' to front: open unpainted ground a shop deep round it is painted for them.
        var decor = Decor.Cells(town);
        foreach (int i in Land.Cells(p.X, p.Y, w, d, p.Yaw, rules.Rim))
            if (i >= 0 && plan.Zones[i] == (byte)Zone.None && plan.Taken[i] == 0 && plan.Paved[i] == 0 && plan.Grounds[i] == 0
                && (decor == null || !decor[i]) && Land.Open(town, i))
                plan.Zones[i] = (byte)Zone.Commercial;
        plan.Painted++;
        plan.Changed();
        // The high street, ruler-straight from the square's front to the street.
        double mx = p.X + fx * hd, my = p.Y + fy * hd;
        if ((mx - p.SX) * (mx - p.SX) + (my - p.SY) * (my - p.SY) > 0.04) Lay(town, [mx, my, p.SX, p.SY]);
        town.Log(LedgerCategory.Town, Importance.Routine, "The shopkeepers have gathered round a new market square.");
    }

    /// <summary>
    /// Whether a square fits here: its ground and a margin round it open shop paint the charter reaches, off the wall strip and
    /// decorations and not too steep (open shop paint or unpainted ground), near half its ring of shops' ground painted for shops, and the way to the street clear of buildings.
    /// </summary>
    private static bool Plaza(Town town, double x, double y, int w, int d, double yaw, double tx, double ty)
    {
        var plan = town.Plan;
        var decor = Decor.Cells(town);
        foreach (int i in Land.Cells(x, y, w, d, yaw, 1))
            if (i < 0 || plan.Zones[i] is not ((byte)Zone.Commercial or (byte)Zone.None) || plan.Taken[i] != 0 || plan.Paved[i] != 0 || (decor != null && decor[i])
                || plan.Grounds[i] != 0 || Land.OnWallStrip(town, i) || !Land.Open(town, i))
                return false;
        int ring = 0, shop = 0;
        foreach (int i in Land.Cells(x, y, w, d, yaw, 4))
        {
            ring++;
            if (i >= 0 && plan.Zones[i] == (byte)Zone.Commercial && plan.Taken[i] == 0) shop++;
        }
        if (shop * 20 < ring * 9) return false;
        if (Hills.Span(town, x, y, w, d, yaw) > Hills.MaxSpan * 1.5) return false;
        double len = Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y));
        for (double t = 0; t < len; t += 0.2)
        {
            int cx = (int)((x + (tx - x) * t / len) * Land.Res), cy = (int)((y + (ty - y) * t / len) * Land.Res);
            if (cx < 1 || cy < 1 || cx >= Land.Size - 1 || cy >= Land.Size - 1) return false;
            if (plan.Taken[cy * Land.Size + cx] != 0 || (Passage.Mask(town) is { } walls && walls[cy * Land.Size + cx])) return false;
        }
        return true;
    }

    /// <summary>
    /// Once a day: the open home paint deepest from any street, near what's built, gets a side lane wandering to the nearest
    /// street, so homes grow along many lanes, close-packed, the way an old town's back streets run.
    /// </summary>
    public static void Closes(Town town)
    {
        var rules = town.Content.City.Closes;
        var plan = town.Plan;
        if (plan.Streets.Count == 0) return;
        int n = Land.Size;
        var walls = Passage.Mask(town);
        for (int k = 0; k < rules.PerDay; k++)
        {
            var field = Field(plan);
            var (built, _) = Built(plan);
            int start = -1;
            double best = double.MaxValue;
            for (int cy = 2; cy < n - 2; cy++)
                for (int cx = 2; cx < n - 2; cx++)
                {
                    int i = cy * n + cx;
                    if (plan.Zones[i] != (byte)Zone.Residential || plan.Taken[i] != 0 || plan.Paved[i] != 0) continue;
                    if (field[i] < rules.Deep * Land.Res || built[i] > rules.Near * Land.Res) continue;
                    if (Blocked(plan, cx, cy) || (walls != null && walls[i]) || Land.OnWallStrip(town, i) || !Land.Open(town, i)) continue;
                    double x = (cx + 0.5) / Land.Res, y = (cy + 0.5) / Land.Res;
                    double score = Math.Sqrt((x - Town.Center) * (x - Town.Center) + (y - Town.Center) * (y - Town.Center)) + Land.Jitter(i * 3 + 7) * 1.5;
                    if (score < best) (best, start) = (score, i);
                }
            if (start < 0 || Route(town, field, start, null, 12) is not { Count: > 1 } path) return;
            Lay(town, Smooth(town, path)).Close = true;
        }
    }

    // Desire paths -------------------------------------------------------------------------------

    /// <summary>Just outside a building's door (<paramref name="step"/> tiles out), in tiles.</summary>
    public static (double X, double Y) Door(Lot lot, double step = 0.25)
    {
        double reach = lot.D / (2.0 * Land.Res) + step;
        return (lot.X - Math.Sin(lot.Yaw) * reach, lot.Y + Math.Cos(lot.Yaw) * reach);
    }

    /// <summary>Whether a building's door is out of reach of every street, so the feet going to it wear a path.</summary>
    public static bool Offroad(Town town, Building b)
    {
        var plan = town.Plan;
        plan.Fresh();
        if (plan.Offroad.TryGetValue(b.Id, out bool off)) return off;
        var (x, y) = Door(town.Lot(b.LotId));
        off = plan.Streets.Count > 0;
        // Paved ground within reach of the doorstep.
        int n = Land.Size, r = (int)Math.Ceiling(Reach * Land.Res), cx = (int)(x * Land.Res), cy = (int)(y * Land.Res);
        double r2 = Reach * Land.Res * Reach * Land.Res;
        for (int yy = Math.Max(0, cy - r); off && yy <= Math.Min(n - 1, cy + r); yy++)
            for (int xx = Math.Max(0, cx - r); xx <= Math.Min(n - 1, cx + r); xx++)
                if (plan.Paved[yy * n + xx] != 0 && (xx + 0.5 - x * Land.Res) * (xx + 0.5 - x * Land.Res) + (yy + 0.5 - y * Land.Res) * (yy + 0.5 - y * Land.Res) <= r2)
                {
                    off = false;
                    break;
                }
        plan.Offroad[b.Id] = off;
        return off;
    }

    /// <summary>A doorstep this near paved ground (tiles) opens on it: past the deepest front yard, and a step.</summary>
    public const double Reach = 1.4;

    /// <summary>Someone walked from one building to another (null: from out of doors, the square's way). Counted at whichever end lies off the streets.</summary>
    public static void Trip(Town town, int? from, int? to)
    {
        if (from == to) return;
        Count(town, to, from);
        Count(town, from, to);
    }

    private static void Count(Town town, int? at, int? other)
    {
        if (at is not int id) return;
        var b = town.Building(id);
        if (!Offroad(town, b)) return;
        var (ox, oy) = other is int o ? Door(town.Lot(town.Building(o).LotId)) : (Town.Center + 0.5, Town.Center + 0.5);
        b.Trips++;
        b.TripX += ox;
        b.TripY += oy;
    }

    /// <summary>
    /// Once a day: the ground wears where feet went to buildings off the streets, along the way most of them took
    /// (the easiest way to the streets, bent toward where they came from, and easier where it is worn already).
    /// Old wear grows back. A path worn deep enough becomes a lane, which buildings can front like any other.
    /// </summary>
    public static void Wear(Town town)
    {
        var plan = town.Plan;
        Decor.Cells(town);
        var rules = town.Content.City.Paths;
        var worn = plan.Worn;
        for (int i = 0; i < worn.Length; i++)
            if (worn[i] != 0) worn[i] = worn[i] < 0.02f ? 0 : worn[i] * (float)rules.Fade;
        if (plan.Streets.Count == 0) return;
        int n = Land.Size;
        float[]? field = null;
        foreach (var b in town.Buildings)
        {
            if (b.Trips == 0) continue;
            int trips = b.Trips;
            double ox = b.TripX / trips, oy = b.TripY / trips;
            (b.Trips, b.TripX, b.TripY) = (0, 0, 0);
            if (!Offroad(town, b)) continue;
            // The path starts where the doorstep is clear of the walls.
            int start = -1;
            for (double step = 0.4; step < 1.2 && start < 0; step += 0.2)
            {
                var (x, y) = Door(town.Lot(b.LotId), step);
                int cx = (int)(x * Land.Res), cy = (int)(y * Land.Res);
                if (cx >= 1 && cy >= 1 && cx < n - 1 && cy < n - 1 && !Blocked(plan, cx, cy)) start = cy * n + cx;
            }
            if (start < 0 || Route(town, field ??= Field(plan), start, null, 24, worn, (ox * Land.Res, oy * Land.Res)) is not { Count: > 1 } path) continue;
            float add = (float)(Math.Min(trips, rules.MostTrips) * rules.WearPerTrip);
            double sum = 0;
            int count = 0;
            foreach (int c in path)
                if (plan.Paved[c] == 0)
                {
                    worn[c] = Math.Min(worn[c] + add, (float)rules.Lay * 2);
                    sum += worn[c];
                    count++;
                }
            if (count == 0 || sum / count < rules.Lay) continue;
            foreach (int c in path) worn[c] = 0;
            Lay(town, Smooth(town, path));
            field = null;
            town.Log(LedgerCategory.Town, Importance.Routine, $"Feet have worn a lane to {b.Name}.");
        }
    }

    /// <summary>Distance in cells from every cell to the nearest paved one (a two-pass chamfer), worked out in the box round the town's ground.</summary>
    public static float[] Field(LandPlan plan)
    {
        int n = Land.Size;
        var f = new float[n * n];
        Array.Fill(f, 1e6f);
        var (x0, y0, x1, y1) = Land.Bounds(plan, 8);
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (plan.Paved[y * n + x] != 0) f[y * n + x] = 0;
        const float a = 1, b = 1.4142f;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int i = y * n + x;
                float v = f[i];
                if (x > x0) v = Math.Min(v, f[i - 1] + a);
                if (y > y0)
                {
                    v = Math.Min(v, f[i - n] + a);
                    if (x > x0) v = Math.Min(v, f[i - n - 1] + b);
                    if (x < x1) v = Math.Min(v, f[i - n + 1] + b);
                }
                f[i] = v;
            }
        for (int y = y1; y >= y0; y--)
            for (int x = x1; x >= x0; x--)
            {
                int i = y * n + x;
                float v = f[i];
                if (x < x1) v = Math.Min(v, f[i + 1] + a);
                if (y < y1)
                {
                    v = Math.Min(v, f[i + n] + a);
                    if (x < x1) v = Math.Min(v, f[i + n + 1] + b);
                    if (x > x0) v = Math.Min(v, f[i + n - 1] + b);
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
    /// so lanes wander. Null when there's no way within <paramref name="most"/> tiles. Worn ground (<paramref name="worn"/>) is easier
    /// going, and with <paramref name="toward"/> (in cells) the street it meets is the one nearest that way, as a walker's would be.
    /// </summary>
    private static List<int>? Route(Town town, float[] field, int start, Func<int, bool>? goal, double most = 30,
        float[]? worn = null, (double X, double Y)? toward = null)
    {
        var plan = town.Plan;
        int n = Land.Size;
        var heights = Hills.Grid(town);
        // No lane through a standing wall or its moat: feet go by the gate.
        var walls = Passage.Mask(town);
        // Scratch kept per thread and put back as it was after each search, so a big plan costs no allocation per lane.
        if (_cost?.Length != n * n)
        {
            _cost = new double[n * n];
            Array.Fill(_cost, double.MaxValue);
            _from = new int[n * n];
        }
        var cost = _cost;
        var from = _from!;
        var touched = _touched ??= [];
        touched.Clear();
        try
        {
            var queue = new PriorityQueue<int, (double, int)>();
            cost[start] = 0;
            touched.Add(start);
            queue.Enqueue(start, (0, start));
            double limit = most * Land.Res * 2.2, bestTotal = double.MaxValue;
            int best = -1;
            while (queue.TryDequeue(out int i, out var pri))
            {
                if (pri.Item1 > cost[i]) continue;
                // Every street further along costs more than the best met so far, however it lies.
                if (pri.Item1 > limit || pri.Item1 > bestTotal) break;
                int x = i % n, y = i / n;
                if (plan.Paved[i] != 0 && i != start && (goal == null || goal(i)))
                {
                    double total = pri.Item1;
                    if (toward is { } t) total += Math.Sqrt((x + 0.5 - t.X) * (x + 0.5 - t.X) + (y + 0.5 - t.Y) * (y + 0.5 - t.Y)) * 0.6;
                    if (total < bestTotal)
                    {
                        bestTotal = total;
                        best = i;
                    }
                    if (toward == null) break;
                    continue;
                }
                foreach (var (dx, dy, len) in Steps)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 1 || ny < 1 || nx >= n - 1 || ny >= n - 1) continue;
                    int j = ny * n + nx;
                    if (Blocked(plan, nx, ny) || (walls != null && walls[j])) continue;
                    double grade = Math.Abs(heights[j] - heights[i]) * Land.Res / len;
                    double step = len * (1 + 30 * grade * grade + Land.Jitter(j) * 0.9);
                    // A lane keeps its distance from the street it would run beside, then meets it square on.
                    if (plan.Paved[j] == 0 && field[j] < 6) step *= 1 + (6 - field[j]) * 0.35;
                    if (!Land.Open(town, j)) step *= 3;
                    if (worn != null && worn[j] > 0) step *= 1 - 0.6 * Math.Min(1, worn[j] / town.Content.City.Paths.Lay);
                    double c = cost[i] + step;
                    if (c < cost[j])
                    {
                        if (cost[j] == double.MaxValue) touched.Add(j);
                        cost[j] = c;
                        from[j] = i;
                        queue.Enqueue(j, (c, j));
                    }
                }
            }
            if (best < 0) return null;
            var path = new List<int>();
            for (int c = best; c != start; c = from[c]) path.Add(c);
            path.Add(start);
            path.Reverse();
            return path;
        }
        finally
        {
            foreach (int c in touched) cost[c] = double.MaxValue;
        }
    }

    [ThreadStatic] private static double[]? _cost;
    [ThreadStatic] private static int[]? _from;
    [ThreadStatic] private static List<int>? _touched;

    /// <summary>Whether a lane can't pass through this cell: a building on it or right beside it, or a decoration on it.</summary>
    private static bool Blocked(LandPlan plan, int x, int y)
    {
        int n = Land.Size;
        if (plan.Decor is { } decor && decor[y * n + x]) return true;
        for (int yy = y - 1; yy <= y + 1; yy++)
            for (int xx = x - 1; xx <= x + 1; xx++)
                if (plan.Taken[yy * n + xx] != 0) return true;
        return false;
    }

    /// <summary>
    /// A path of cells as a smooth line: straightened where it zigzags on the raster, long straight runs given a gentle
    /// sway (no lane runs ruler-straight), then rounded at the bends.
    /// </summary>
    private static double[] Smooth(Town town, List<int> cells)
    {
        var pts = cells.Select(c => ((c % Land.Size + 0.5) / Land.Res, (c / Land.Size + 0.5) / Land.Res)).ToList();
        pts = Bend(town.Plan, Simplify(pts, 0.18));
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

    /// <summary>Longest straight run a lane keeps, in tiles; longer ones sway a little to one side and back.</summary>
    private const double Straight = 1.5;

    /// <summary>Sway each run longer than <see cref="Straight"/>: a point or two pushed off the line, to alternate sides, clear of walls.</summary>
    private static List<(double X, double Y)> Bend(LandPlan plan, List<(double X, double Y)> pts)
    {
        var bent = new List<(double X, double Y)> { pts[0] };
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var (ax, ay) = pts[i];
            var (bx, by) = pts[i + 1];
            double dx = bx - ax, dy = by - ay, len = Math.Sqrt(dx * dx + dy * dy);
            if (len > Straight)
            {
                int m = Math.Max(1, (int)(len / 3));
                double amp = Math.Min(0.1 * len / m, 0.6);
                int key = (int)(ax * 31 + ay * 17 + bx * 7 + by * 3);
                double side = Land.Jitter(key) > 0.5 ? 1 : -1;
                for (int k = 1; k <= m; k++)
                {
                    double t = k / (m + 1.0), off = amp * side * (k % 2 == 1 ? 1 : -1) * (0.7 + Land.Jitter(key + k) * 0.6);
                    double px = ax + dx * t - dy / len * off, py = ay + dy * t + dx / len * off;
                    int cx = (int)(px * Land.Res), cy = (int)(py * Land.Res);
                    if (cx < 1 || cy < 1 || cx >= Land.Size - 1 || cy >= Land.Size - 1 || Blocked(plan, cx, cy)) (px, py) = (ax + dx * t, ay + dy * t);
                    bent.Add((px, py));
                }
            }
            bent.Add((bx, by));
        }
        return bent;
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
public sealed class Street
{
    public int Id { get; set; }
    public double[] Points { get; set; } = [];
    public bool Square { get; set; }

    /// <summary>A side lane in homes' paint (<see cref="Streets.Closes"/>): it stays trodden earth, never paved.</summary>
    public bool Close { get; set; }

    /// <summary>A market square the shops grew round (<see cref="Streets.Markets"/>), flagged from the day it's laid.</summary>
    public bool Market { get; set; }

    /// <summary>Tiles of it the masons have paved with stone (<see cref="Paving"/>), from the start or, if <see cref="FromEnd"/>, the end.</summary>
    public double Cobbled { get; set; }

    public bool FromEnd { get; set; }
}
