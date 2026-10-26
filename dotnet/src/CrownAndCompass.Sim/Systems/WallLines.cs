namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Where the town's walls run, drawn by the player: the wall round the old town (ring 0, the palisade and then the stone wall
/// in its place) and the city wall (ring 1). A drawn line must go right round the hall, so it is kept as its distance from the
/// hall at <see cref="N"/> headings, and <see cref="City.Wall"/> reads it: the strip kept clear of buildings, the guard's
/// sections, the moat and the map all follow it. A line not drawn stands where the town's own lie of the land puts it. Every
/// wall needs at least one gate, placed by hand and snapped to the line (<see cref="PlaceGate"/>); the works can't begin
/// without one, and a gate is where feet cross it (<see cref="Passage.Barriers"/>). The walls rise as the market has their
/// timber and stone (<see cref="Threat"/>).
/// </summary>
public static class WallLines
{
    /// <summary>Headings a drawn line is kept at, from the south round as the map draws it.</summary>
    public const int N = 96;

    public static WallRules Rules(Town town) => town.Content.City.WallLines;

    public static double Heading(int i) => Math.PI / 2 + i * Math.PI * 2 / N;

    public static WallPlan? Plan(Town town, int ring) => town.City.WallPlans.FirstOrDefault(p => p.Ring == ring);

    private static WallPlan Own(Town town, int ring)
    {
        var p = Plan(town, ring);
        if (p == null) town.City.WallPlans.Add(p = new WallPlan { Ring = ring });
        return p;
    }

    /// <summary>The drawn line's distance from the hall at a heading, between its kept headings.</summary>
    public static double RadiusAt(WallPlan p, double angle)
    {
        double f = (angle - Math.PI / 2) / (Math.PI * 2) * N;
        f = ((f % N) + N) % N;
        int i = (int)Math.Floor(f);
        double t = f - i;
        return p.Radii[i % N] * (1 - t) + p.Radii[(i + 1) % N] * t;
    }

    /// <summary>The ring a wall work stands on: the palisade and the stone wall round the old town, the city wall round the wards.</summary>
    public static int RingOf(DefenseWork w) => w == DefenseWork.CityWall ? 1 : 0;

    public static string WallName(Town town, int ring) =>
        ring == 1 ? "the city wall" : town.Defenses.StoneWall != PalisadeState.None ? "the stone wall" : "the palisade";

    /// <summary>Whether the wall on a ring is going up or stands, so its line is fixed.</summary>
    public static bool Begun(Town town, int ring)
    {
        var d = town.Defenses;
        return ring == 1 ? d.CityWall != PalisadeState.None : d.Palisade != PalisadeState.None || d.StoneWall != PalisadeState.None;
    }

    public static bool Standing(Town town, int ring)
    {
        var d = town.Defenses;
        return ring == 1 ? d.CityWall == PalisadeState.Built : d.Palisade == PalisadeState.Built || d.StoneWall == PalisadeState.Built;
    }

    /// <summary>Whether a point lies outside the stone the town has: beyond the city wall, or the stone wall, or anywhere with neither.</summary>
    public static bool Outside(Town town, double x, double y)
    {
        var d = town.Defenses;
        int ring = d.CityWall == PalisadeState.Built ? 1 : d.StoneWall == PalisadeState.Built ? 0 : -1;
        if (ring < 0) return true;
        double dx = x - Town.Center, dy = y - Town.Center;
        return Math.Sqrt(dx * dx + dy * dy) >= City.Wall(town, ring, Math.Atan2(dy, dx));
    }

    /// <summary>The line's point at a heading.</summary>
    public static (double X, double Y) Point(Town town, int ring, double angle)
    {
        double r = City.Wall(town, ring, angle);
        return (Town.Center + Math.Cos(angle) * r, Town.Center + Math.Sin(angle) * r);
    }

    /// <summary>The gates on a ring's wall, as headings from the hall, the main gate first.</summary>
    public static IReadOnlyList<double> Gates(Town town, int ring) => Plan(town, ring)?.Gates ?? [];

    /// <summary>The headings of the drawn loop, or null and why it can't be the wall.</summary>
    public static (double[]? Radii, string? Why) Trace(Town town, int ring, IReadOnlyList<(double X, double Y)> pts)
    {
        var r = Rules(town);
        if (ring is < 0 or >= City.WallRings) return (null, "there is no such wall");
        if (pts.Count < 3) return (null, "a wall needs at least three corners");
        var radii = new double[N];
        for (int i = 0; i < N; i++)
        {
            double a = Heading(i), dx = Math.Cos(a), dy = Math.Sin(a), best = -1;
            for (int k = 0; k < pts.Count; k++)
            {
                var (ax, ay) = pts[k];
                var (bx, by) = pts[(k + 1) % pts.Count];
                ax -= Town.Center; ay -= Town.Center; bx -= Town.Center; by -= Town.Center;
                // Ray from the hall along (dx, dy) against the edge a..b.
                double ex = bx - ax, ey = by - ay, den = dx * ey - dy * ex;
                if (Math.Abs(den) < 1e-12) continue;
                double t = (ax * ey - ay * ex) / den, u = (ax * dy - ay * dx) / den;
                if (t > 0 && u >= 0 && u <= 1) best = Math.Max(best, t);
            }
            if (best < 0) return (null, "a wall must go right round the town hall");
            if (best < r.Least) return (null, "it runs too close to the town hall");
            if (best > Town.Grid / 2.0 - r.Margin) return (null, "it runs off the edge of the town's land");
            radii[i] = best;
        }
        // The two walls keep apart all the way round, once both are drawn (an undrawn line makes way, see City.Wall).
        int other = 1 - ring;
        if (Plan(town, other) is not { Radii.Length: > 0 }) return (radii, null);
        for (int i = 0; i < N; i++)
        {
            double o = City.Wall(town, other, Heading(i));
            if (ring == 0 ? o - radii[i] < r.Apart : radii[i] - o < r.Apart)
                return (null, ring == 0 ? "it comes too close to the city wall" : "it must stand well outside the old town's wall");
        }
        return (radii, null);
    }

    /// <summary>Why the line can't be drawn there, or null.</summary>
    public static string? DrawBlocked(Town town, int ring, IReadOnlyList<(double X, double Y)> pts)
    {
        if (Begun(town, ring)) return $"{WallName(town, ring)} is already going up";
        var (radii, why) = Trace(town, ring, pts);
        if (why != null) return why;
        var r = Rules(town);
        foreach (var b in town.Buildings.Where(b => !b.Razed).OrderBy(b => b.Id))
        {
            var l = town.Lot(b.LotId);
            double dx = l.X - Town.Center, dy = l.Y - Town.Center, a = Math.Atan2(dy, dx), dist = Math.Sqrt(dx * dx + dy * dy);
            double half = Math.Max(l.W, l.D) / (double)Land.Res / 2 + r.Clear;
            if (Math.Abs(dist - Radius(radii!, a)) < half) return $"it runs through {Named(b)}";
        }
        return null;
    }

    private static double Radius(double[] radii, double angle) => RadiusAt(new WallPlan { Radii = radii }, angle);

    /// <summary>Lays the wall's line where the player drew it (a closed loop of points in tiles), before its works begin.</summary>
    public static string? Draw(Town town, int ring, IReadOnlyList<(double X, double Y)> pts)
    {
        if (DrawBlocked(town, ring, pts) is { } why) return why;
        var p = Own(town, ring);
        p.Radii = Trace(town, ring, pts).Radii!;
        Changed(town);
        Unzone(town);
        town.Log(LedgerCategory.War, Importance.Routine, $"The council marks out where {WallName(town, ring)} will run{(p.Gates.Count == 0 ? ". It needs a gate before the masons can start" : "")}.");
        return null;
    }

    /// <summary>What a gate costs: free while the wall is only a line, dearer once there is wall to cut through.</summary>
    public static double GateCost(Town town, int ring) => Begun(town, ring) ? Rules(town).GateCost : 0;

    public static string? GateBlocked(Town town, int ring, double x, double y)
    {
        var r = Rules(town);
        if (ring is < 0 or >= City.WallRings) return "there is no such wall";
        double a = Math.Atan2(y - Town.Center, x - Town.Center);
        var (px, py) = Point(town, ring, a);
        if (Js.Hypot(px - x, py - y) > r.Snap) return "a gate goes on the wall's line";
        foreach (double g in Gates(town, ring))
        {
            var (gx, gy) = Point(town, ring, g);
            if (Js.Hypot(gx - px, gy - py) < r.GateApart) return "it's too close to another gate";
        }
        if (Gates(town, ring).Count >= r.Most) return $"a wall has at most {r.Most} gates";
        if (town.Treasury < GateCost(town, ring)) return "not enough gold";
        // The way out of the gate must be open ground, not a building.
        foreach (var b in town.Buildings)
        {
            if (b.Razed) continue;
            var l = town.Lot(b.LotId);
            if (Js.Hypot(l.X - px, l.Y - py) < Math.Max(l.W, l.D) / (double)Land.Res / 2 + 0.6) return $"{Named(b)} stands in the way";
        }
        return null;
    }

    /// <summary>A gate on a ring's wall, snapped to the line nearest the point. Returns why not, or null.</summary>
    public static string? PlaceGate(Town town, int ring, double x, double y)
    {
        if (GateBlocked(town, ring, x, y) is { } why) return why;
        double cost = GateCost(town, ring);
        if (cost > 0) town.Book(TreasuryAccount.Defenses, -cost);
        double a = Math.Atan2(y - Town.Center, x - Town.Center);
        Own(town, ring).Gates.Add(a);
        Changed(town);
        town.Log(LedgerCategory.War, Importance.Routine, $"A gate is marked on the {Garrison.Rules(town).Sections[Garrison.SectionAt(town, x, y)]} side of {WallName(town, ring)}.");
        return null;
    }

    /// <summary>Takes a gate off the plan; the last gate of a wall going up or standing stays, since every wall needs one.</summary>
    public static string? RemoveGate(Town town, int ring, int index)
    {
        var p = Plan(town, ring);
        if (p == null || index < 0 || index >= p.Gates.Count) return "there is no such gate";
        if (p.Gates.Count == 1 && Begun(town, ring)) return "every wall needs a gate";
        p.Gates.RemoveAt(index);
        Changed(town);
        return null;
    }

    /// <summary>The stand-in mayor's line and gate: where the wall would have run by default, clear of what already stands.</summary>
    public static void StandIn(Town town, int ring)
    {
        if (Plan(town, ring) is not { Radii.Length: > 0 })
        {
            Own(town, ring).Radii = Enumerable.Range(0, N).Select(i => City.Wall(town, ring, Heading(i))).ToArray();
            Changed(town);
        }
        EnsureGate(town, ring);
    }

    /// <summary>A gate on the south road for a wall that has none: the stand-in mayor's way, and old saves' walls.</summary>
    public static void EnsureGate(Town town, int ring)
    {
        if (Gates(town, ring).Count > 0) return;
        Own(town, ring).Gates.Add(Math.PI / 2);
        Changed(town);
    }

    /// <summary>Takes the paint off open ground under the drawn walls: nothing is zoned under a wall.</summary>
    private static void Unzone(Town town)
    {
        var plan = town.Plan;
        bool any = false;
        for (int i = 0; i < plan.Zones.Length; i++)
        {
            if (plan.Zones[i] == (byte)Zone.None || plan.Taken[i] != 0) continue;
            if (!Land.UnderWall(town, i)) continue;
            plan.Zones[i] = (byte)Zone.None;
            plan.Density[i] = 0;
            plan.Crops[i] = 0;
            any = true;
        }
        if (any) plan.Changed();
    }

    /// <summary>
    /// The land's toll on a wall: its line's cost over flat ground, stretch by stretch, so a loop over hills and ridges costs
    /// far more than one that keeps to the low ground. 1 on the flat.
    /// </summary>
    public static double Terrain(Town town, int ring) => Terrain(town, Enumerable.Range(0, N).Select(i => City.Wall(town, ring, Heading(i))).ToArray());

    public static double Terrain(Town town, double[] radii)
    {
        if (radii.Length == 0) return 1;
        double cost = 0, length = 0;
        for (int i = 0; i < radii.Length; i++)
        {
            cost += Stretch(town, radii, i, out double len) * len;
            length += len;
        }
        return length > 0 ? cost / length : 1;
    }

    /// <summary>One stretch's cost over flat ground (from heading i to the next), and its length.</summary>
    public static double Stretch(Town town, double[] radii, int i, out double length)
    {
        var r = Rules(town);
        int n = radii.Length, j = (i + 1) % n;
        double a = Heading(i), b = Heading(j);
        double ax = Town.Center + Math.Cos(a) * radii[i], ay = Town.Center + Math.Sin(a) * radii[i];
        double bx = Town.Center + Math.Cos(b) * radii[j], by = Town.Center + Math.Sin(b) * radii[j];
        length = Js.Hypot(bx - ax, by - ay);
        double ha = Hills.Height(town, ax, ay), hb = Hills.Height(town, bx, by);
        // The slope across the line as well as along it: a wall cut into a hillside is as dear as one up it.
        double ox = Math.Cos(a) * 0.5, oy = Math.Sin(a) * 0.5;
        double across = Math.Abs(Hills.Height(town, ax + ox, ay + oy) - Hills.Height(town, ax - ox, ay - oy));
        double slope = Math.Max(Math.Abs(hb - ha) / Math.Max(0.1, length), across);
        return 1 + r.High * (ha + hb) / 2 + r.Steep * slope;
    }

    private static string Named(Building b) => b.Name.StartsWith("The ", StringComparison.Ordinal) ? "the " + b.Name[4..] : b.Name;

    private static void Changed(Town town)
    {
        town.City.WallVersion++;
        town.Plan.WallStrip = null;
        town.Plan.UnderWall = null;
        town.Plan.Version++;
    }
}

/// <summary>A wall's line and gates as the player laid them out.</summary>
public sealed class WallPlan
{
    public int Ring { get; set; }

    /// <summary>Distance from the hall at each of <see cref="WallLines.N"/> headings; empty for the line the land suggests.</summary>
    public double[] Radii { get; set; } = [];

    /// <summary>Gate headings (radians from east, south positive), the main gate first.</summary>
    public List<double> Gates { get; set; } = [];
}

public sealed record WallRules
{
    /// <summary>Nearest a wall may run to the hall, and how far from the town's land's edge it keeps, in tiles.</summary>
    public double Least { get; init; } = 5;
    public double Margin { get; init; } = 2;

    /// <summary>How far apart the old town's wall and the city wall keep, all the way round.</summary>
    public double Apart { get; init; } = 3;

    /// <summary>Room kept between a wall and a building it would run past.</summary>
    public double Clear { get; init; } = 0.4;

    /// <summary>How near the line a click snaps to it; how far apart gates stand; the most a wall has; a gate cut in a wall already going up.</summary>
    public double Snap { get; init; } = 2.5;
    public double GateApart { get; init; } = 4;
    public int Most { get; init; } = 4;
    public double GateCost { get; init; } = 60;

    /// <summary>What a stretch of wall costs over flat ground, more for each tile of height it climbs to and each unit of slope it runs across.</summary>
    public double High { get; init; }
    public double Steep { get; init; }
}
