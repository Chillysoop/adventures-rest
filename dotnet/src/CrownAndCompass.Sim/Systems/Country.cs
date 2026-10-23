namespace CrownAndCompass.Sim.Systems;

/// <summary>The lie of the land (roads.json "country"): hard country's outlines, how parties pick their way, and the trails they wear.</summary>
public sealed record CountryRules
{
    /// <summary>The land grid's cell, and how far out from the town it reaches, in days.</summary>
    public double Cell { get; init; } = 0.08;
    public double Reach { get; init; } = 24;

    /// <summary>Share of each region's ground under hard country, least and most.</summary>
    public double[] Cover { get; init; } = [0.25, 0.4];

    /// <summary>Tries at a feature per region before it settles for what it has.</summary>
    public int Tries { get; init; } = 80;

    /// <summary>Days kept clear round the town, round each site, and between two features (a way through always runs between them).</summary>
    public double TownClear { get; init; } = 0.5;
    public double SiteClear { get; init; } = 0.2;
    public double Gap { get; init; } = 0.18;

    /// <summary>Far country is bigger: features grow by this share per day out, up to <see cref="GrowMost"/> times.</summary>
    public double Grow { get; init; } = 0.12;
    public double GrowMost { get; init; } = 3;

    public ChainShape Chain { get; init; } = new();
    public BlobShape Blob { get; init; } = new();

    /// <summary>How far a mountain wall's line wanders in and out (days), and how much its depth swells and thins (share).</summary>
    public double WallWander { get; init; } = 0.3;
    public double WallSwell { get; init; } = 0.3;

    /// <summary>Open ground's bumps (dells, copses, a hill to go round): up to this much slower, about this many days across.</summary>
    public double Wander { get; init; } = 1.4;
    public double WanderSize { get; init; } = 0.6;

    /// <summary>The longest straight stretch a road keeps on open ground, in days: past that it follows the land's small bends.</summary>
    public double Straightest { get; init; } = 0.5;

    /// <summary>A straight stretch is kept when it costs no more than this share over the cells' own path (which zigzags on the grid).</summary>
    public double Slack { get; init; } = 0.06;

    /// <summary>Times a road's corners are cut round (Chaikin): each pass makes its bends rounder.</summary>
    public int Round { get; init; } = 2;

    /// <summary>Days into a feature its danger is spread over: deep country is feared for every day of it.</summary>
    public double DangerDepth { get; init; } = 0.4;

    /// <summary>How skilled leaders cut through hard country more easily (<see cref="Skill"/>).</summary>
    public CutRules Cut { get; init; } = new();

    /// <summary>Explorers keep to the land's lines: open ground this near a feature's edge or the river costs them this much.</summary>
    public double Follow { get; init; } = 0.8;
    public double FollowNear { get; init; } = 0.2;

    /// <summary>Chance an explorer's next spot is a landmark (a feature's edge, a strange place) rather than open country.</summary>
    public double Landmark { get; init; } = 0.35;

    public TrailRules Trail { get; init; } = new();
    public AvoidRules Avoid { get; init; } = new();
}

/// <summary>A long wandering band: a ridge, a gorge, a mountain chain.</summary>
public sealed record ChainShape
{
    public double[] Length { get; init; } = [0.8, 2.2];
    public double[] Width { get; init; } = [0.26, 0.45];

    /// <summary>Radians its line may turn each step.</summary>
    public double Bend { get; init; } = 0.35;
    public double Step { get; init; } = 0.15;
}

/// <summary>A patch of country: a mire, a tangled wood, a waste.</summary>
public sealed record BlobShape
{
    public double[] Radius { get; init; } = [0.3, 0.7];

    /// <summary>How round it is (1) or drawn out (less), least and most.</summary>
    public double[] Squash { get; init; } = [0.45, 1];

    /// <summary>How ragged its edge, as a share of its radius.</summary>
    public double Rag { get; init; } = 0.3;
}

public sealed record CutRules
{
    /// <summary>Ease per level the party stands above the country's own, and per point of the navigator's Survival.</summary>
    public double PerLevel { get; init; } = 0.05;
    public double PerNav { get; init; } = 0.03;
    public double Most { get; init; } = 0.6;
}

/// <summary>Trails: ground walked often wears into a track that is quicker going.</summary>
public sealed record TrailRules
{
    /// <summary>Wear one party's walk adds to each cell it crosses; what's left of it each day after.</summary>
    public double Wear { get; init; } = 1;
    public double Keep { get; init; } = 0.985;

    /// <summary>Wear at which a track is fully worn, and how much quicker the going is on one.</summary>
    public double Worn { get; init; } = 4;
    public double Quick { get; init; } = 0.2;

    /// <summary>How much cheaper a fully worn track looks to a party picking its way: they keep to it, so tracks draw together.</summary>
    public double Pull { get; init; } = 0.45;

    /// <summary>Wear below this is forgotten.</summary>
    public double Least { get; init; } = 0.1;

    /// <summary>Wear at which the map shows a track.</summary>
    public double Shown { get; init; } = 1.5;
}

/// <summary>Where a party was set on, it keeps away from on the way home.</summary>
public sealed record AvoidRules
{
    public double Radius { get; init; } = 0.5;
    public double Cost { get; init; } = 2.5;
}

/// <summary>
/// The lie of the land between the town and its sites. Hard country has real outlines (<see cref="Feature.Shape"/>):
/// wandering ridges and gorges, mires and woods in patches, mountain walls that bend, covering about a third of
/// the wilds. A coarse grid of it (<see cref="Land"/>, worked out once and never saved) lets a party pick its way:
/// round a hill, along a valley, through a pass, to a ford, or straight through when the leader is bold, in a hurry
/// or skilled enough. The ground they walk often wears into trails (<see cref="Town.Trails"/>).
/// </summary>
public static class Country
{
    public static CountryRules Rules(Town town) => town.Content.Roads.Country;

    // ---- Outlines ----

    /// <summary>A feature's outline: its shape, or the old ring sector traced as one.</summary>
    public static IReadOnlyList<(double X, double Y)> Outline(Feature f)
    {
        if (f.Shape.Count >= 3) return f.Shape;
        var pts = new List<(double X, double Y)>();
        int n = Math.Max(4, (int)Math.Ceiling(f.Arc / 4));
        for (int i = 0; i <= n; i++) pts.Add(Polar(f.Bearing - f.Arc / 2 + f.Arc * i / n, f.Outer));
        for (int i = n; i >= 0; i--) pts.Add(Polar(f.Bearing - f.Arc / 2 + f.Arc * i / n, Math.Max(0.02, f.Inner)));
        return pts;
    }

    private static (double X, double Y) Polar(double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return (r * Math.Sin(a), -r * Math.Cos(a));
    }

    private static double[] Box(Feature f)
    {
        if (f.Box != null) return f.Box;
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (var (x, y) in Outline(f))
        {
            x0 = Math.Min(x0, x);
            y0 = Math.Min(y0, y);
            x1 = Math.Max(x1, x);
            y1 = Math.Max(y1, y);
        }
        return f.Box = [x0, y0, x1, y1];
    }

    /// <summary>Whether a point lies inside a shaped feature, or within <paramref name="margin"/> days of its edge.</summary>
    public static bool Inside(Feature f, double x, double y, double margin = 0)
    {
        var b = Box(f);
        if (x < b[0] - margin || x > b[2] + margin || y < b[1] - margin || y > b[3] + margin) return false;
        var s = Outline(f);
        if (InPolygon(s, x, y)) return true;
        return margin > 0 && EdgeDistance(s, x, y) <= margin;
    }

    private static bool InPolygon(IReadOnlyList<(double X, double Y)> s, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = s.Count - 1; i < s.Count; j = i++)
        {
            var (xi, yi) = s[i];
            var (xj, yj) = s[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    private static double EdgeDistance(IReadOnlyList<(double X, double Y)> s, double x, double y)
    {
        double best = double.MaxValue;
        for (int i = 0, j = s.Count - 1; i < s.Count; j = i++)
        {
            var (ax, ay) = s[j];
            var (bx, by) = s[i];
            double dx = bx - ax, dy = by - ay, l2 = dx * dx + dy * dy;
            double t = l2 < 1e-12 ? 0 : Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
            best = Math.Min(best, Js.Hypot(x - ax - dx * t, y - ay - dy * t));
        }
        return best;
    }

    private static double Area(IReadOnlyList<(double X, double Y)> s)
    {
        double a = 0;
        for (int i = 0, j = s.Count - 1; i < s.Count; j = i++) a += s[j].X * s[i].Y - s[i].X * s[j].Y;
        return Math.Abs(a) / 2;
    }

    /// <summary>The middle of a feature's outline, in days: where its name goes on the map.</summary>
    public static (double X, double Y) Middle(Feature f)
    {
        if (f.Shape.Count < 3) return Polar(f.Bearing, (f.Inner + f.Outer) / 2);
        // The outline point nearest the average: a bent ridge's middle stays on the ridge.
        double mx = f.Shape.Average(p => p.X), my = f.Shape.Average(p => p.Y);
        if (InPolygon(f.Shape, mx, my)) return (mx, my);
        return DeepPoint(f) ?? (mx, my);
    }

    /// <summary>For a bent outline whose average falls outside it: the deepest point of a few across it.</summary>
    private static (double X, double Y)? DeepPoint(Feature f)
    {
        var s = f.Shape;
        (double X, double Y)? best = null;
        double bestD = -1;
        int n = s.Count;
        for (int i = 0; i < n; i++)
        {
            // Midpoints across the outline, from each vertex to the one opposite it.
            var a = s[i];
            var b = s[(i + n / 2) % n];
            var m = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            if (!InPolygon(s, m.Item1, m.Item2)) continue;
            double d = EdgeDistance(s, m.Item1, m.Item2);
            if (d > bestD)
            {
                bestD = d;
                best = m;
            }
        }
        return best;
    }

    /// <summary>The sector round an outline: what the old ring fields describe for code that still reads them.</summary>
    private static (double Bearing, double Arc, double Inner, double Outer) Envelope(List<(double X, double Y)> s)
    {
        double inner = double.MaxValue, outer = 0;
        double cx = s.Average(p => p.X), cy = s.Average(p => p.Y);
        double mid = Math.Atan2(cx, -cy) * 180 / Math.PI;
        double lo = 0, hi = 0;
        foreach (var (x, y) in s)
        {
            double r = Js.Hypot(x, y);
            inner = Math.Min(inner, r);
            outer = Math.Max(outer, r);
            double d = Roads.AngleDiff(Math.Atan2(x, -y) * 180 / Math.PI, mid);
            lo = Math.Min(lo, d);
            hi = Math.Max(hi, d);
        }
        double bearing = ((mid + (lo + hi) / 2) % 360 + 360) % 360;
        return (Js.Round(bearing * 10) / 10.0, Js.Round((hi - lo) * 10) / 10.0, Js.Round(inner * 100) / 100.0, Js.Round(outer * 100) / 100.0);
    }

    // ---- Laying the country ----

    /// <summary>A feature with an outline, its ring fields the sector round it.</summary>
    internal static Feature Shaped(Town town, int id, string name, TerrainKindDef kind, int regionId, List<(double X, double Y)> shape)
    {
        var (bearing, arc, inner, outer) = Envelope(shape);
        double mx = shape.Average(p => p.X), my = shape.Average(p => p.Y);
        return new Feature
        {
            Id = id,
            Name = name,
            Kind = kind,
            RegionId = regionId,
            Tier = Roads.TierAt(town, Js.Hypot(mx, my)),
            Bearing = bearing,
            Arc = arc,
            Inner = inner,
            Outer = outer,
            Shape = shape,
        };
    }

    private static double Between(Rng rng, double[] range) => range[0] + rng.Next() * (range[^1] - range[0]);

    private static (double X, double Y) Round2((double X, double Y) p) => (Js.Round(p.X * 1000) / 1000.0, Js.Round(p.Y * 1000) / 1000.0);

    /// <summary>A long wandering band about a middle: a ridge, a gorge.</summary>
    internal static List<(double X, double Y)> Chain(CountryRules r, Rng rng, double cx, double cy, double scale)
    {
        var c = r.Chain;
        double len = Between(rng, c.Length) * scale, width = Between(rng, c.Width) * Math.Sqrt(scale);
        int steps = Math.Max(4, (int)Math.Ceiling(len / c.Step));
        double heading = rng.Next() * 2 * Math.PI;
        var spine = new List<(double X, double Y)> { (0, 0) };
        var headings = new List<double>();
        for (int i = 0; i < steps; i++)
        {
            heading += (rng.Next() - 0.5) * 2 * c.Bend;
            headings.Add(heading);
            var (x, y) = spine[^1];
            spine.Add((x + Math.Cos(heading) * len / steps, y + Math.Sin(heading) * len / steps));
        }
        double ox = cx - spine.Average(p => p.X), oy = cy - spine.Average(p => p.Y);
        double p1 = rng.Next() * 6.28, p2 = rng.Next() * 6.28;
        var left = new List<(double X, double Y)>();
        var right = new List<(double X, double Y)>();
        for (int i = 0; i <= steps; i++)
        {
            double t = (double)i / steps;
            double h = (headings[Math.Max(0, i - 1)] + headings[Math.Min(steps - 1, i)]) / 2;
            double half = width / 2 * (0.3 + 0.7 * Math.Sin(Math.PI * Math.Clamp(t, 0.04, 0.96))) * (1 + 0.25 * Math.Sin(9 * t + p1) + 0.15 * Math.Sin(17 * t + p2));
            double nx = -Math.Sin(h), ny = Math.Cos(h);
            var (x, y) = spine[i];
            left.Add(Round2((x + ox + nx * half, y + oy + ny * half)));
            right.Add(Round2((x + ox - nx * half, y + oy - ny * half)));
        }
        right.Reverse();
        left.AddRange(right);
        return left;
    }

    /// <summary>A ragged patch about a middle: a mire, a wood, a lake.</summary>
    internal static List<(double X, double Y)> Blob(CountryRules r, Rng rng, double cx, double cy, double scale)
    {
        var b = r.Blob;
        double rad = Between(rng, b.Radius) * scale, squash = Between(rng, b.Squash), turn = rng.Next() * Math.PI;
        double p1 = rng.Next() * 6.28, p2 = rng.Next() * 6.28, p3 = rng.Next() * 6.28;
        int m = Math.Max(24, (int)Math.Ceiling(2 * Math.PI * rad / 0.12));
        var pts = new List<(double X, double Y)>(m);
        double cos = Math.Cos(turn), sin = Math.Sin(turn);
        for (int k = 0; k < m; k++)
        {
            double a = 2 * Math.PI * k / m;
            double rr = rad * (1 + b.Rag * (0.5 * Math.Sin(2 * a + p1) + 0.3 * Math.Sin(3 * a + p2) + 0.2 * Math.Sin(5 * a + p3)));
            double x = rr * Math.Cos(a), y = rr * squash * Math.Sin(a);
            pts.Add(Round2((cx + x * cos - y * sin, cy + x * sin + y * cos)));
        }
        return pts;
    }

    /// <summary>A mountain wall along a ring, bending in and out and swelling and thinning, tapered at its ends.</summary>
    internal static List<(double X, double Y)> Wall(CountryRules r, Rng rng, double bearing, double arc, double inner, double outer)
    {
        double mid = (inner + outer) / 2, half = (outer - inner) / 2;
        double p1 = rng.Next() * 6.28, p2 = rng.Next() * 6.28, p3 = rng.Next() * 6.28;
        int n = Math.Max(4, (int)Math.Ceiling(arc * Math.PI / 180 * mid / 0.15));
        var o = new List<(double X, double Y)>();
        var i = new List<(double X, double Y)>();
        for (int k = 0; k <= n; k++)
        {
            double t = (double)k / n;
            double b = bearing - arc / 2 + arc * t;
            double line = mid + r.WallWander * (0.6 * Math.Sin(t * 5.1 + p1) + 0.4 * Math.Sin(t * 11.3 + p2));
            double depth = half * (1 + r.WallSwell * Math.Sin(t * 7.7 + p3)) * (0.35 + 0.65 * Math.Min(1, Math.Min(t, 1 - t) / 0.15));
            o.Add(Round2(Polar(b, line + depth)));
            i.Add(Round2(Polar(b, Math.Max(0.05, line - depth))));
        }
        i.Reverse();
        o.AddRange(i);
        return o;
    }

    /// <summary>Whether two features come within <paramref name="gap"/> days of each other.</summary>
    private static bool Crowds(Feature a, Feature b, double gap)
    {
        var ba = Box(a);
        var bb = Box(b);
        if (ba[0] - gap > bb[2] || bb[0] - gap > ba[2] || ba[1] - gap > bb[3] || bb[1] - gap > ba[3]) return false;
        foreach (var (x, y) in Outline(a))
            if (Roads.Contains(b, x, y, gap)) return true;
        foreach (var (x, y) in Outline(b))
            if (Roads.Contains(a, x, y, gap)) return true;
        return false;
    }

    /// <summary>
    /// Hard country through each region, in the shapes of its kinds, until it covers its share of the region's ground:
    /// never on the town, a site or other country, so a way always runs between.
    /// </summary>
    internal static void Lay(Town town, Rng rng, List<(double X, double Y)> spots)
    {
        var rules = town.Content.Roads;
        var r = rules.Country;
        var rings = town.Content.World.Rings;
        foreach (var region in town.Regions)
        {
            if (!rules.Themes.TryGetValue(region.Theme.Id, out var ids) || ids.Count == 0) continue;
            int ring = region.Ring;
            double inner = ring == 0 ? r.TownClear : (rings[ring - 1].Days + rings[ring].Days) / 2;
            double outer = ring + 1 < rings.Count ? (rings[ring].Days + rings[ring + 1].Days) / 2 : rings[ring].Days + 2.5;
            double area = region.Arc * Math.PI / 180 / 2 * (outer * outer - inner * inner);
            double target = area * Between(rng, r.Cover);
            double scale = Math.Clamp(1 + (region.Days - 1) * r.Grow, 1, r.GrowMost);
            double covered = 0;
            for (int tries = 0; tries < r.Tries && covered < target; tries++)
            {
                var kind = rules.Kinds.Where(k => !k.Boats).FirstOrDefault(k => k.Id == rng.Pick(ids));
                if (kind == null) continue;
                double rr = inner + rng.Next() * (outer - inner);
                double bearing = region.Bearing + (rng.Next() - 0.5) * region.Arc;
                var (cx, cy) = Polar(bearing, rr);
                double size = scale * Math.Sqrt(kind.Wide);
                string name = Roads.Name(rng, kind);
                // Where it would cover a site or crowd other country, a smaller one may still fit.
                for (int shrink = 0; shrink < 3; shrink++, size *= 0.65)
                {
                    var shape = kind.Shape == "chain" ? Chain(r, rng, cx, cy, size) : Blob(r, rng, cx, cy, size);
                    var f = Shaped(town, town.Terrain.Count, name, kind, region.Id, shape);
                    if (shape.Any(p => Js.Hypot(p.X, p.Y) < r.TownClear)) continue;
                    if (spots.Any(p => Roads.Contains(f, p.X, p.Y, r.SiteClear))) continue;
                    if (town.Terrain.Any(o => Crowds(o, f, r.Gap))) continue;
                    town.Terrain.Add(f);
                    covered += Area(shape);
                    break;
                }
            }
        }
    }

    // ---- Old towns ----

    /// <summary>Daily: a town from before the land had shapes gets them, and old trails fade.</summary>
    public static void Daily(Town town)
    {
        if (!town.Shaped) Reshape(town);
        var t = Rules(town).Trail;
        if (town.Trails.Count == 0) return;
        foreach (var key in town.Trails.Keys.ToList())
        {
            double w = town.Trails[key] * t.Keep;
            if (w < t.Least) town.Trails.Remove(key);
            else town.Trails[key] = w;
        }
    }

    /// <summary>
    /// The old ring sectors become country of real shape where they lay (names, bridges and all), and more is laid
    /// between, on the town's own dice so nothing else shifts. Strange places and the sea keep their sectors.
    /// </summary>
    private static void Reshape(Town town)
    {
        town.Shaped = true;
        if (town.Terrain.Any(f => f.Shape.Count > 0) || town.Regions.Count == 0) return;
        var r = Rules(town);
        var rng = new Rng(town.Seed * 7919 + 60013);
        var spots = town.Sites.Select(s => World.Spot(town, s)).Concat(town.Gateways.SelectMany(g => new (double X, double Y)[] { (g.Ax, g.Ay), (g.Bx, g.By) })).ToList();
        var range = town.Content.Roads.Ranges.Kind;
        var kept = new List<Feature>();
        foreach (var f in town.Terrain)
        {
            if (f.Kind.Wonder != null || f.Kind.Boats || f.Inner < r.TownClear)
            {
                kept.Add(f);
                continue;
            }
            List<(double X, double Y)> shape;
            if (f.Kind.Id == range) shape = Wall(r, rng, f.Bearing, f.Arc, f.Inner, f.Outer);
            else
            {
                var (cx, cy) = Polar(f.Bearing, (f.Inner + f.Outer) / 2);
                double scale = Math.Clamp(1 + ((f.Inner + f.Outer) / 2 - 1) * r.Grow, 1, r.GrowMost) * Math.Sqrt(f.Kind.Wide);
                shape = f.Kind.Shape == "chain" ? Chain(r, rng, cx, cy, scale) : Blob(r, rng, cx, cy, scale);
            }
            var g = Shaped(town, f.Id, f.Name, f.Kind, f.RegionId, shape);
            g.Bridge = f.Bridge;
            g.BridgeName = f.BridgeName;
            if (shape.Any(p => Js.Hypot(p.X, p.Y) < r.TownClear) || spots.Any(p => Roads.Contains(g, p.X, p.Y, r.SiteClear))) continue;
            if (kept.Any(o => Crowds(o, g, r.Gap))) continue;
            kept.Add(g);
        }
        town.Terrain.Clear();
        town.Terrain.AddRange(kept);
        int next = town.Terrain.Count == 0 ? 0 : town.Terrain.Max(f => f.Id) + 1;
        int start = town.Terrain.Count;
        Lay(town, rng, spots);
        // Fresh ids past the old ones, so nothing that remembers a feature by id finds another.
        for (int i = start; i < town.Terrain.Count; i++)
        {
            var f = town.Terrain[i];
            town.Terrain[i] = new Feature { Id = next++, Name = f.Name, Kind = f.Kind, RegionId = f.RegionId, Tier = f.Tier, Bearing = f.Bearing, Arc = f.Arc, Inner = f.Inner, Outer = f.Outer, Shape = f.Shape };
        }
    }

    // ---- The land grid ----

    /// <summary>
    /// The country on a grid of cells in days from the town: whose ground each cell is, how deep inside it (or how far
    /// outside any, negative), and the river and its crossings. Worked out from the town once and kept until the
    /// country changes; never saved.
    /// </summary>
    public sealed class Land
    {
        public readonly double Cell, Reach;
        public readonly int W;

        /// <summary>Index into the town's terrain plus one, 0 for open ground.</summary>
        public readonly short[] Owner;

        /// <summary>Days inside its feature (positive), or out from the nearest (negative, down to -1).</summary>
        public readonly float[] Depth;

        /// <summary>1 river (no wading), 2 a crossing over it, 4 near the river's bank.</summary>
        public readonly byte[] Water;

        /// <summary>The deepest any cell of each feature lies.</summary>
        public readonly float[] Deepest;

        internal readonly int Stamp;
        internal readonly Feature[] Features;

        internal Land(Town town, int stamp)
        {
            var r = Rules(town);
            Cell = r.Cell;
            Reach = r.Reach;
            W = (int)Math.Ceiling(2 * Reach / Cell);
            Stamp = stamp;
            Features = town.Terrain.ToArray();
            Owner = new short[W * W];
            Depth = new float[W * W];
            Water = new byte[W * W];
            Deepest = new float[Features.Length];
            for (int i = 0; i < Features.Length; i++) Fill(i);
            Distances();
            for (int c = 0; c < Owner.Length; c++)
                if (Owner[c] > 0) Deepest[Owner[c] - 1] = Math.Max(Deepest[Owner[c] - 1], Depth[c]);
            Rivers(town);
        }

        public int Index(double x, double y)
        {
            int ix = (int)Math.Floor((x + Reach) / Cell), iy = (int)Math.Floor((y + Reach) / Cell);
            return ix < 0 || iy < 0 || ix >= W || iy >= W ? -1 : iy * W + ix;
        }

        public (double X, double Y) Centre(int c) => ((c % W + 0.5) * Cell - Reach, (c / W + 0.5) * Cell - Reach);

        /// <summary>The feature a point lies in (null on open ground), and its depth there in days, smoothed between cells.</summary>
        public (Feature? F, double Depth, double Deepest) Lie(double x, double y)
        {
            int c = Index(x, y);
            if (c < 0) return (null, -1, 0);
            double fx = (x + Reach) / Cell - 0.5, fy = (y + Reach) / Cell - 0.5;
            int ix = Math.Clamp((int)Math.Floor(fx), 0, W - 2), iy = Math.Clamp((int)Math.Floor(fy), 0, W - 2);
            double tx = Math.Clamp(fx - ix, 0, 1), ty = Math.Clamp(fy - iy, 0, 1);
            int i = iy * W + ix;
            double d = (Depth[i] * (1 - tx) + Depth[i + 1] * tx) * (1 - ty) + (Depth[i + W] * (1 - tx) + Depth[i + W + 1] * tx) * ty;
            int o = Owner[c];
            if (o == 0)
            {
                // The nearest owned neighbour, for the ragged edge just outside.
                for (int k = 0; k < 4 && o == 0; k++) o = Owner[k switch { 0 => i, 1 => i + 1, 2 => i + W, _ => i + W + 1 }];
            }
            return o == 0 ? (null, d, 0) : (Features[o - 1], d, Deepest[o - 1]);
        }

        /// <summary>Scanline fill of a feature's outline into the cells it covers (the first feature keeps a shared cell).</summary>
        private void Fill(int index)
        {
            var s = Outline(Features[index]);
            var b = Box(Features[index]);
            int y0 = Math.Max(0, (int)Math.Floor((b[1] + Reach) / Cell)), y1 = Math.Min(W - 1, (int)Math.Floor((b[3] + Reach) / Cell));
            var xs = new List<double>();
            for (int iy = y0; iy <= y1; iy++)
            {
                double y = (iy + 0.5) * Cell - Reach;
                xs.Clear();
                for (int i = 0, j = s.Count - 1; i < s.Count; j = i++)
                {
                    var (xi, yi) = s[i];
                    var (xj, yj) = s[j];
                    if ((yi > y) != (yj > y)) xs.Add((xj - xi) * (y - yi) / (yj - yi) + xi);
                }
                xs.Sort();
                for (int k = 0; k + 1 < xs.Count; k += 2)
                {
                    int a = Math.Max(0, (int)Math.Ceiling((xs[k] + Reach) / Cell - 0.5)), e = Math.Min(W - 1, (int)Math.Floor((xs[k + 1] + Reach) / Cell - 0.5));
                    for (int ix = a; ix <= e; ix++)
                        if (Owner[iy * W + ix] == 0) Owner[iy * W + ix] = (short)(index + 1);
                }
            }
        }

        /// <summary>Chamfer distances: inside, to the nearest cell not of the same feature; outside, to the nearest feature (capped at a day).</summary>
        private void Distances()
        {
            int n = W * W;
            var din = new float[n];
            var dout = new float[n];
            const float Big = 1e6f;
            for (int c = 0; c < n; c++)
            {
                din[c] = Owner[c] > 0 ? Big : 0;
                dout[c] = Owner[c] > 0 ? 0 : Big;
            }
            (int X, int Y, float W)[] ahead = [(-1, 0, 1), (-1, -1, 1.4142f), (0, -1, 1), (1, -1, 1.4142f)];
            for (int pass = 0; pass < 2; pass++)
            {
                int sgn = pass == 0 ? 1 : -1;
                for (int k = 0; k < n; k++)
                {
                    int c = pass == 0 ? k : n - 1 - k;
                    int ix = c % W, iy = c / W;
                    foreach (var (ox, oy, w) in ahead)
                    {
                        int sx = ix + ox * sgn, sy = iy + oy * sgn;
                        if (sx < 0 || sy < 0 || sx >= W || sy >= W) continue;
                        int s = sy * W + sx;
                        if (Owner[c] > 0)
                        {
                            float v = Owner[s] == Owner[c] ? din[s] + w : w * 0.5f;
                            if (v < din[c]) din[c] = v;
                        }
                        else if (dout[s] + w < dout[c]) dout[c] = dout[s] + w;
                    }
                }
            }
            for (int c = 0; c < n; c++)
                Depth[c] = Owner[c] > 0 ? (float)(din[c] * Cell) : -(float)Math.Min(1, dout[c] * Cell);
        }

        /// <summary>The river as cells nobody wades, but at its crossings; and the bank either side.</summary>
        private void Rivers(Town town)
        {
            var river = Passage.River(town);
            if (river.Count < 2) return;
            void Stamp(double ax, double ay, double bx, double by, double reach, byte flag)
            {
                int x0 = (int)Math.Floor((Math.Min(ax, bx) - reach + Reach) / Cell), x1 = (int)Math.Floor((Math.Max(ax, bx) + reach + Reach) / Cell);
                int y0 = (int)Math.Floor((Math.Min(ay, by) - reach + Reach) / Cell), y1 = (int)Math.Floor((Math.Max(ay, by) + reach + Reach) / Cell);
                double dx = bx - ax, dy = by - ay, l2 = Math.Max(1e-12, dx * dx + dy * dy);
                for (int iy = Math.Max(0, y0); iy <= Math.Min(W - 1, y1); iy++)
                    for (int ix = Math.Max(0, x0); ix <= Math.Min(W - 1, x1); ix++)
                    {
                        var (x, y) = Centre(iy * W + ix);
                        double t = Math.Clamp(((x - ax) * dx + (y - ay) * dy) / l2, 0, 1);
                        if (Js.Hypot(x - ax - dx * t, y - ay - dy * t) <= reach) Water[iy * W + ix] |= flag;
                    }
            }
            for (int k = 1; k < river.Count; k++)
            {
                Stamp(river[k - 1].X, river[k - 1].Y, river[k].X, river[k].Y, Cell * 0.8, 1);
                Stamp(river[k - 1].X, river[k - 1].Y, river[k].X, river[k].Y, Rules(town).FollowNear, 4);
            }
            foreach (var c in Passage.Crossings(town)) Stamp(c.X, c.Y, c.X, c.Y, Cell * 1.6, 2);
        }
    }

    private sealed class Cache
    {
        public Land? Land;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Town, Cache> Caches = new();

    private static int StampOf(Town town)
    {
        int h = town.Terrain.Count * 7919 + Passage.Crossings(town).Count * 31;
        foreach (var f in town.Terrain) h = unchecked(h * 31 + f.Id * 17 + f.Shape.Count);
        return h;
    }

    /// <summary>The town's land grid, worked out again only when its country or crossings change.</summary>
    public static Land LandOf(Town town)
    {
        var c = Caches.GetValue(town, _ => new Cache());
        int stamp = StampOf(town);
        if (c.Land == null || c.Land.Stamp != stamp) c.Land = new Land(town, stamp);
        return c.Land;
    }

    // ---- Trails ----

    /// <summary>The cells along a road, each once.</summary>
    private static IEnumerable<int> Cells(Land land, (double X, double Y) from, IReadOnlyList<Waypoint> route)
    {
        var seen = new HashSet<int>();
        var cells = new List<int>();
        var cur = from;
        foreach (var w in route)
        {
            if (w.Way is "portal" or "fly" or "sail" or "ferry")
            {
                cur = (w.X, w.Y);
                continue;
            }
            double len = Js.Hypot(w.X - cur.X, w.Y - cur.Y);
            int n = Math.Max(1, (int)Math.Ceiling(len / (land.Cell * 0.5)));
            for (int i = 0; i <= n; i++)
            {
                int c = land.Index(cur.X + (w.X - cur.X) * i / n, cur.Y + (w.Y - cur.Y) * i / n);
                if (c >= 0 && seen.Add(c)) cells.Add(c);
            }
            cur = (w.X, w.Y);
        }
        return cells;
    }

    /// <summary>A road walked wears the ground it crossed.</summary>
    public static void Wear(Town town, (double X, double Y) from, IReadOnlyList<Waypoint> route)
    {
        var t = Rules(town).Trail;
        var land = LandOf(town);
        foreach (int c in Cells(land, from, route))
            town.Trails[c] = town.Trails.GetValueOrDefault(c) + t.Wear;
    }

    /// <summary>How much quicker the going is in a cell for the trail worn there (0 to <see cref="TrailRules.Quick"/>).</summary>
    public static double Quick(Town town, int cell) => Rules(town).Trail.Quick * Worn(town, cell);

    /// <summary>How worn a cell's track is, 0 to 1 (fully worn).</summary>
    public static double Worn(Town town, int cell) =>
        town.Trails.TryGetValue(cell, out double w) ? Math.Min(1, w / Rules(town).Trail.Worn) : 0;

    /// <summary>The worn tracks the map shows: each cell's middle in days and how worn it is (1 fully).</summary>
    public static IEnumerable<(double X, double Y, double Worn)> Tracks(Town town)
    {
        var t = Rules(town).Trail;
        var land = LandOf(town);
        foreach (var (c, w) in town.Trails.OrderBy(kv => kv.Key))
            if (w >= t.Shown)
            {
                var (x, y) = land.Centre(c);
                yield return (x, y, Math.Min(1, w / t.Worn));
            }
    }

    // ---- Finding the way ----

    /// <summary>How much easier a party finds a feature for its level over the country's and its navigator's Survival.</summary>
    public static double Skill(Town town, RouteMind mind, Feature f)
    {
        var c = Rules(town).Cut;
        return Math.Clamp(c.PerLevel * (mind.Level - f.Tier) + c.PerNav * mind.NavBonus, 0, c.Most);
    }

    /// <summary>Share of a feature's slowness taken off: mounts and guides, then the party's own skill.</summary>
    private static double Ease(Town town, RouteMind mind, Feature f) =>
        1 - (1 - mind.Eases.GetValueOrDefault(f.Kind.Id)) * (1 - Skill(town, mind, f));

    /// <summary>Smooth bumps of open ground for one road, 0 to 1: two layers turned off the grid, so no valley runs along it.</summary>
    private static double Bumps(int seed, double x, double y, double size)
    {
        double a = (seed & 1023) / 1023.0 * Math.PI;
        double c = Math.Cos(a), s = Math.Sin(a);
        double big = Noise(seed, (x * c - y * s) / size, (x * s + y * c) / size);
        double small = Noise(seed ^ 0x5bd1, (x * s + y * c) * 2.3 / size, (y * s - x * c) * 2.3 / size);
        return big * 0.7 + small * 0.3;
    }

    private static double Noise(int seed, double fx, double fy)
    {
        int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
        double tx = fx - ix, ty = fy - iy;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);
        double H(int a, int b) => (Mix(seed, a, b) & 0xffff) / 65535.0;
        return (H(ix, iy) * (1 - tx) + H(ix + 1, iy) * tx) * (1 - ty) + (H(ix, iy + 1) * (1 - tx) + H(ix + 1, iy + 1) * tx) * ty;
    }

    private const double Never = double.PositiveInfinity;

    /// <summary>A quick hash of three numbers, for the road's own bumps and moods (no allocation: it runs for every cell searched).</summary>
    private static uint Mix(int a, int b, int c)
    {
        unchecked
        {
            uint h = (uint)a * 0x9E3779B1u ^ (uint)b * 0x85EBCA77u ^ (uint)c * 0xC2B2AE3Du;
            h ^= h >> 15;
            h *= 0x2C1B3C6Du;
            h ^= h >> 12;
            h *= 0x297A2D39u;
            return h ^ (h >> 15);
        }
    }

    /// <summary>What one road costs its walkers: per feature, per day inside it, and how they cross it.</summary>
    private sealed class Costs
    {
        public required double[] Feature;
        public required string[] Way;
        public required double[] Ease;
        public required int Seed;
        /// <summary>Hours at sea for each hour of march aboard the town's ship (<see cref="RouteMind.Sail"/>).</summary>
        public double Sail;
    }

    private static Costs Weigh(Town town, Land land, RouteMind mind, int seed)
    {
        var rules = town.Content.Roads;
        var r = rules.Country;
        int n = land.Features.Length;
        var cost = new double[n];
        var way = new string[n];
        var ease = new double[n];
        for (int i = 0; i < n; i++)
        {
            var f = land.Features[i];
            var needs = Bridges.Needs(f);
            if (needs != Ways.None)
            {
                bool flies = (mind.Ways & needs) == needs;
                // Aboard the harbour's ship they sail even where they could fly, at the ship's pace (Voyages.Embark).
                if (f.Kind.Ship && mind.Sail > 0) (cost[i], way[i]) = (mind.Sail, "sail");
                else if (flies) (cost[i], way[i]) = (1, "fly");
                else if (f.Kind.Boats) (cost[i], way[i]) = (f.Kind.Slow, "sail");
                else if (f.Kind.Wonder?.Kind == "ferry") (cost[i], way[i]) = (1.3, "ferry");
                else (cost[i], way[i]) = (Never, "");
                continue;
            }
            double e = Ease(town, mind, f);
            double slow = 1 + (f.Kind.Slow - 1) * (1 - e);
            double danger = f.Kind.Danger * (1 - e * 0.5) * Math.Clamp((f.Tier + 1.0) / (mind.Level + 1.0), 0.4, 2.5);
            // The party's mood about this country today, on the road's own dice.
            double mood = ((Mix(seed, f.Id, 7919) & 0xffff) / 65535.0 - 0.5) * rules.Choice.Noise;
            cost[i] = slow + Math.Max(0, danger * mind.RiskWeight + mood) / (3 * mind.TimeWeight * r.DangerDepth);
            way[i] = "cross";
            ease[i] = e;
        }
        return new Costs { Feature = cost, Way = way, Ease = ease, Seed = seed, Sail = mind.Sail };
    }

    /// <summary>A cell's cost per day walked through it, or <see cref="Never"/>.</summary>
    private static double CellCost(Town town, Land land, Costs k, RouteMind mind, int c)
    {
        var r = Rules(town);
        var (x, y) = land.Centre(c);
        int o = land.Owner[c];
        double cost;
        if (o > 0) cost = k.Feature[o - 1];
        else
        {
            byte w = land.Water[c];
            if ((w & 1) != 0 && (w & 2) == 0 && (mind.Ways & Ways.River) == 0) return Never;
            // A worn track has no bumps to pick round: it is the way.
            cost = 1 + r.Wander * Bumps(k.Seed, x, y, r.WanderSize) * (1 - Worn(town, c));
            if (mind.Explorer && ((w & 4) != 0 || land.Depth[c] > -r.FollowNear)) cost *= r.Follow;
        }
        if (double.IsInfinity(cost)) return cost;
        cost *= 1 - r.Trail.Pull * Worn(town, c);
        foreach (var (ax, ay) in mind.Avoid)
        {
            double d = Js.Hypot(x - ax, y - ay);
            if (d < r.Avoid.Radius) cost += r.Avoid.Cost * (1 - d / r.Avoid.Radius);
        }
        return cost;
    }

    /// <summary>
    /// The cheapest way over the land from one cell to another inside a box of cells, as cells from the start, with
    /// the cost walked to each; null when there is none.
    /// </summary>
    private static readonly (int X, int Y)[] Steps =
    [
        (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1),
        (2, 1), (2, -1), (-2, 1), (-2, -1), (1, 2), (1, -2), (-1, 2), (-1, -2),
    ];

    private static (List<int> Cells, List<double> Spent)? Search(Town town, Land land, Costs k, RouteMind mind, int start, int goal, int x0, int y0, int x1, int y1)
    {
        int bw = x1 - x0 + 1, bh = y1 - y0 + 1;
        int Local(int c) => (c / land.W - y0) * bw + (c % land.W - x0);
        int Global(int l) => (l / bw + y0) * land.W + l % bw + x0;
        var g = new double[bw * bh];
        var cost = new double[bw * bh];
        var from = new int[bw * bh];
        var shut = new bool[bw * bh];
        Array.Fill(g, double.MaxValue);
        Array.Fill(cost, -1);
        int gl = Local(goal);
        var (gx, gy) = land.Centre(goal);
        double hmin = 0.7 * (1 - Rules(town).Trail.Pull);
        var open = new PriorityQueue<int, double>();
        int sl = Local(start);
        g[sl] = 0;
        from[sl] = -1;
        open.Enqueue(sl, 0);
        double Cost(int l)
        {
            if (cost[l] < 0)
            {
                int c = Global(l);
                // Where they start and where they're going are always open to them.
                cost[l] = c == start || c == goal ? Math.Min(3, CellCost(town, land, k, mind, c)) : CellCost(town, land, k, mind, c);
                if (double.IsNaN(cost[l])) cost[l] = Never;
            }
            return cost[l];
        }
        while (open.TryDequeue(out int u, out _))
        {
            if (shut[u]) continue;
            if (u == gl) break;
            shut[u] = true;
            int ux = u % bw, uy = u / bw;
            double cu = Cost(u);
            // Sixteen ways out of a cell (the knight's moves too), so a road can run at any angle, not just the grid's eight.
            foreach (var (dx, dy) in Steps)
            {
                int vx = ux + dx, vy = uy + dy;
                if (vx < 0 || vy < 0 || vx >= bw || vy >= bh) continue;
                int v = vy * bw + vx;
                if (shut[v]) continue;
                double cv = Cost(v);
                if (double.IsInfinity(cv)) continue;
                double step;
                if (Math.Abs(dx) + Math.Abs(dy) == 3)
                {
                    // A knight's move crosses the two cells between; no slipping through either.
                    int m1 = Math.Abs(dx) == 2 ? uy * bw + ux + dx / 2 : (uy + dy / 2) * bw + ux;
                    int m2 = Math.Abs(dx) == 2 ? vy * bw + ux + dx / 2 : (uy + dy / 2) * bw + vx;
                    double c1 = Cost(m1), c2 = Cost(m2);
                    if (double.IsInfinity(c1) || double.IsInfinity(c2)) continue;
                    // Nor hopping over a sliver of other ground: the road must say what it crossed.
                    int ou = land.Owner[Global(u)], ov = land.Owner[Global(v)], o1 = land.Owner[Global(m1)], o2 = land.Owner[Global(m2)];
                    if (o1 != ou && o1 != ov || o2 != ou && o2 != ov) continue;
                    step = 2.23606798 * land.Cell * (cu + c1 + c2 + cv) / 4;
                }
                else
                {
                    // No slipping diagonally between two cells nobody can cross.
                    if (dx != 0 && dy != 0 && (double.IsInfinity(Cost(uy * bw + vx)) || double.IsInfinity(Cost(vy * bw + ux)))) continue;
                    step = (dx != 0 && dy != 0 ? 1.41421356 : 1) * land.Cell * (cu + cv) / 2;
                }
                double alt = g[u] + step;
                if (alt >= g[v]) continue;
                g[v] = alt;
                from[v] = u;
                var (vxw, vyw) = land.Centre(Global(v));
                open.Enqueue(v, alt + hmin * Js.Hypot(gx - vxw, gy - vyw));
            }
        }
        if (g[gl] == double.MaxValue) return null;
        var cells = new List<int>();
        var spent = new List<double>();
        for (int l = gl; l != -1; l = from[l])
        {
            cells.Add(Global(l));
            spent.Add(g[l]);
        }
        cells.Reverse();
        spent.Reverse();
        return (cells, spent);
    }

    /// <summary>The cost of walking straight from one spot to another over the cells between, and whether it keeps to one feature (or open ground).</summary>
    private static double LineCost(Town town, Land land, Costs k, RouteMind mind, (double X, double Y) a, (double X, double Y) b, int owner)
    {
        double len = Js.Hypot(b.X - a.X, b.Y - a.Y);
        int n = Math.Max(1, (int)Math.Ceiling(len / (land.Cell / 3)));
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            double t = (i + 0.5) / n;
            int c = land.Index(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
            if (c < 0 || land.Owner[c] != owner) return Never;
            double cc = CellCost(town, land, k, mind, c);
            if (double.IsInfinity(cc)) return Never;
            sum += cc;
        }
        return sum / n * len;
    }

    /// <summary>A stretch of the road through one kind of ground: open (0) or one feature (its index plus one).</summary>
    private sealed record Run(int Owner, List<(double X, double Y)> Points, List<double> Spent);

    /// <summary>
    /// One stretch of road picked over the land, from one spot to the next with no ford between (<see cref="Passage.Ford"/>):
    /// round hills and woods, along valleys, through passes, or straight through hard country when the party would rather.
    /// What they say about it goes into <paramref name="said"/>. Null when the land has no way (the caller falls back).
    /// </summary>
    internal static List<Waypoint>? Stretch(Town town, (double X, double Y) from, (double X, double Y) to, RouteMind mind, Rng rng, List<string> said)
    {
        var land = LandOf(town);
        int start = land.Index(from.X, from.Y), goal = land.Index(to.X, to.Y);
        if (start < 0 || goal < 0) return null;
        int seed = (int)(rng.NextUint() & 0x7fffffff);
        if (start == goal) return [new Waypoint { X = to.X, Y = to.Y }];
        var k = Weigh(town, land, mind, seed);
        int sx = start % land.W, sy = start / land.W, gx = goal % land.W, gy = goal / land.W;
        double dist = Js.Hypot(to.X - from.X, to.Y - from.Y);
        int pad = (int)Math.Ceiling(Math.Max(1.2, dist * 0.45) / land.Cell);
        var found = Search(town, land, k, mind, start, goal,
            Math.Max(0, Math.Min(sx, gx) - pad), Math.Max(0, Math.Min(sy, gy) - pad),
            Math.Min(land.W - 1, Math.Max(sx, gx) + pad), Math.Min(land.W - 1, Math.Max(sy, gy) + pad));
        found ??= Search(town, land, k, mind, start, goal, 0, 0, land.W - 1, land.W - 1);
        if (found is not var (cells, spent)) return null;

        // Cut the cells into runs of one kind of ground; each run's ends meet the next's halfway between their cells.
        var runs = new List<Run>();
        for (int i = 0; i < cells.Count; i++)
        {
            int o = land.Owner[cells[i]];
            var p = i == 0 ? from : i == cells.Count - 1 ? to : land.Centre(cells[i]);
            if (runs.Count == 0 || runs[^1].Owner != o)
            {
                if (runs.Count > 0)
                {
                    var a = land.Centre(cells[i - 1]);
                    var b = land.Centre(cells[i]);
                    var edge = ((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                    double half = (spent[i - 1] + spent[i]) / 2;
                    runs[^1].Points.Add(edge);
                    runs[^1].Spent.Add(half);
                    runs.Add(new Run(o, [edge], [half]));
                }
                else runs.Add(new Run(o, [], []));
            }
            runs[^1].Points.Add(p);
            runs[^1].Spent.Add(spent[i]);
        }

        // Each run pulled straight where straight is no dearer, no longer than a short march, so the road keeps the land's bends.
        var r = Rules(town);
        var path = new List<Waypoint>();
        var crossed = new List<(Feature F, (double X, double Y) In, (double X, double Y) Out, int At)>();
        foreach (var run in runs)
        {
            var pts = run.Points;
            var keep = new List<int> { 0 };
            int i = 0;
            while (i < pts.Count - 1)
            {
                int best = i + 1;
                for (int j = i + 2; j < pts.Count; j++)
                {
                    if (Js.Hypot(pts[j].X - pts[i].X, pts[j].Y - pts[i].Y) > r.Straightest) break;
                    if (LineCost(town, land, k, mind, pts[i], pts[j], run.Owner) <= (run.Spent[j] - run.Spent[i]) * (1 + r.Slack) + 1e-9) best = j;
                }
                keep.Add(best);
                i = best;
            }
            var f = run.Owner > 0 ? land.Features[run.Owner - 1] : null;
            string way = f == null ? "road" : k.Way[run.Owner - 1];
            int first = path.Count;
            var line = keep.Select(q => pts[q]).ToList();
            for (int pass = 0; pass < r.Round; pass++) line = Soften(line);
            for (int q = 1; q < line.Count; q++)
            {
                var a = line[q - 1];
                var b = line[q];
                path.Add(new Waypoint
                {
                    X = b.X,
                    Y = b.Y,
                    Way = way,
                    Feature = f,
                    Ease = f == null ? 0 : k.Ease[run.Owner - 1],
                    Sail = way == "sail" && f!.Kind.Ship ? k.Sail : 0,
                    Quick = way is "road" or "cross" ? Js.Round(Along(town, land, a, b) * 1000) / 1000.0 : 0,
                });
            }
            if (f != null && path.Count > first) crossed.Add((f, pts[0], pts[^1], path.Count - 1));
        }
        if (path.Count == 0 || Js.Hypot(path[^1].X - to.X, path[^1].Y - to.Y) > 1e-6) path.Add(new Waypoint { X = to.X, Y = to.Y });
        Tell(town, land, k, mind, rng, from, to, path, crossed, said);
        return path;
    }

    /// <summary>
    /// The way a lone rider (a scout) would likely take from one spot to another, for the map to draw: picked on its own dice
    /// from <paramref name="seed"/>, never the town's. Starts at <paramref name="from"/> and ends at <paramref name="to"/>.
    /// </summary>
    public static List<(double X, double Y)> Sketch(Town town, (double X, double Y) from, (double X, double Y) to, int seed)
    {
        var path = Stretch(town, from, to, new RouteMind(1, 1, 1, "", null, false, false), new Rng(seed), []);
        var pts = new List<(double X, double Y)> { from };
        if (path == null) pts.Add(to);
        else pts.AddRange(path.Select(w => (w.X, w.Y)));
        return pts;
    }

    /// <summary>Corners cut (Chaikin), the ends kept where they are: a walked line rounds its turns.</summary>
    private static List<(double X, double Y)> Soften(List<(double X, double Y)> pts)
    {
        if (pts.Count < 3) return pts;
        var o = new List<(double X, double Y)> { pts[0] };
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            var (ax, ay) = pts[i];
            var (bx, by) = pts[i + 1];
            if (i > 0) o.Add(Round2((ax * 0.75 + bx * 0.25, ay * 0.75 + by * 0.25)));
            if (i + 2 < pts.Count) o.Add(Round2((ax * 0.25 + bx * 0.75, ay * 0.25 + by * 0.75)));
        }
        o.Add(pts[^1]);
        return o;
    }

    /// <summary>The trail's quickness along a stretch, averaged.</summary>
    private static double Along(Town town, Land land, (double X, double Y) a, (double X, double Y) b)
    {
        if (town.Trails.Count == 0) return 0;
        double len = Js.Hypot(b.X - a.X, b.Y - a.Y);
        int n = Math.Max(1, (int)Math.Ceiling(len / (land.Cell / 2)));
        double sum = 0;
        for (int i = 0; i < n; i++)
        {
            int c = land.Index(a.X + (b.X - a.X) * (i + 0.5) / n, a.Y + (b.Y - a.Y) * (i + 0.5) / n);
            if (c >= 0) sum += Quick(town, c);
        }
        return sum / n;
    }

    private static double PathLength((double X, double Y) from, IEnumerable<Waypoint> path)
    {
        double len = 0;
        var cur = from;
        foreach (var w in path)
        {
            len += Js.Hypot(w.X - cur.X, w.Y - cur.Y);
            cur = (w.X, w.Y);
        }
        return len;
    }

    /// <summary>
    /// What they say about the road they chose, at each country they went through or round, as the old roads did; the
    /// navigator's roll at each; and the stretches near country they gave a wide berth marked as going round it.
    /// </summary>
    private static void Tell(Town town, Land land, Costs k, RouteMind mind, Rng rng, (double X, double Y) from, (double X, double Y) to, List<Waypoint> path,
        List<(Feature F, (double X, double Y) In, (double X, double Y) Out, int At)> crossed, List<string> said)
    {
        var rules = town.Content.Roads;
        string Fill(string line, Feature f, double days) => Cap(line
            .Replace("{leader}", mind.Leader, StringComparison.Ordinal)
            .Replace("{bold}", mind.Bold ?? mind.Leader, StringComparison.Ordinal)
            .Replace("{carrier}", mind.Carrier, StringComparison.Ordinal)
            .Replace("{name}", f.Name, StringComparison.Ordinal)
            .Replace("{through}", f.Kind.Through, StringComparison.Ordinal)
            .Replace("{days}", DaysText(Math.Max(0, days), rules.MarchHours), StringComparison.Ordinal));
        // Country the straight way would have gone through that the road keeps out of.
        var on = new HashSet<int>(crossed.Select(c => c.F.Id));
        var skirted = new List<Feature>();
        double len = Js.Hypot(to.X - from.X, to.Y - from.Y);
        int n = Math.Max(1, (int)Math.Ceiling(len / land.Cell));
        for (int i = 0; i <= n; i++)
        {
            int c = land.Index(from.X + (to.X - from.X) * i / n, from.Y + (to.Y - from.Y) * i / n);
            if (c < 0 || land.Owner[c] == 0) continue;
            var f = land.Features[land.Owner[c] - 1];
            if (!on.Contains(f.Id) && !skirted.Contains(f)) skirted.Add(f);
        }
        double extra = Math.Max(0, PathLength(from, path) - len);
        int told = 0;
        const int Most = 3;
        foreach (var (f, a, b, at) in crossed)
        {
            string way = path[at].Way;
            if (way == "cross")
            {
                double through = Js.Hypot(b.X - a.X, b.Y - a.Y);
                // How far round it would have been: half its outline's span past the way through, about.
                var box = Box(f);
                double round = Math.Max(0, Math.Min(box[2] - box[0], box[3] - box[1]) * 0.8 - through);
                string key = mind.Bold == mind.Leader && mind.Bold != null ? "crossBold" : mind.Hurry ? "crossHurry" : "cross";
                if (told < Most && f.Kind.Wonder == null && rules.Texts.TryGetValue(key, out var lines) && lines.Count > 0)
                {
                    said.Add(Fill(rng.Pick(lines), f, round));
                    told++;
                }
                double lost = Wayfaring.Navigate(town.Content, mind, f, through * (1 + (f.Kind.Slow - 1) * (1 - path[at].Ease)) * rules.MarchHours, said);
                if (lost > 0) path[at] = path[at].Lost(lost);
            }
            else if (way == "fly" && told < Most && Passage.Rules(town).Texts.TryGetValue("over", out var over) && over.Count > 0)
            {
                said.Add(Fill(rng.Pick(over), f, 0));
                told++;
            }
            else if (way == "sail") said.Add($"The town's boats take them over {f.Name}.");
        }
        foreach (var f in skirted.Take(2))
        {
            for (int i = 0; i < path.Count; i++)
                if (path[i].Way == "road" && Roads.Contains(f, path[i].X, path[i].Y, 0.35))
                    path[i] = new Waypoint { X = path[i].X, Y = path[i].Y, Way = "skirt", Feature = f, Quick = path[i].Quick, Delay = path[i].Delay };
            if (told >= Most) continue;
            double days = extra / Math.Max(1, skirted.Count);
            if (f.Kind.Barrier != "")
            {
                if (Passage.Rules(town).Texts.TryGetValue("round", out var round) && round.Count > 0) said.Add(Fill(rng.Pick(round), f, days));
            }
            else
            {
                string key = mind.Wounded ? "skirtWounded" : mind.Bold != null && mind.Bold != mind.Leader ? "skirtOverruled" : "skirt";
                if (rules.Texts.TryGetValue(key, out var lines) && lines.Count > 0) said.Add(Fill(rng.Pick(lines), f, days));
                int last = path.FindLastIndex(w => w.Way == "skirt" && w.Feature == f);
                double lost = Wayfaring.Navigate(town.Content, mind, f, days * rules.MarchHours, said);
                if (last >= 0 && lost > 0) path[last] = path[last].Lost(lost);
            }
            told++;
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string DaysText(double days, int marchHours)
    {
        double h = days * marchHours;
        if (h < 3) return "a few hours";
        if (h < 7) return "half a day";
        if (h < 14) return "a day";
        return $"{Js.Round(days)} days";
    }

    // ---- Explorers ----

    /// <summary>
    /// A landmark in a region for explorers to make for: the edge of a stretch of hard country, a strange place, or a bend
    /// of the river; null when the region has none.
    /// </summary>
    public static (double X, double Y)? Landmark(Town town, Region region, (double X, double Y) near, Rng rng)
    {
        var marks = new List<(double X, double Y)>();
        foreach (var f in town.Terrain)
        {
            if (f.RegionId != region.Id || f.Kind.Boats) continue;
            if (f.Kind.Wonder != null)
            {
                marks.Add(Middle(f));
                continue;
            }
            // A spot just outside its edge, on the side nearest them.
            var s = Outline(f);
            var best = s.OrderBy(p => Js.Hypot(p.X - near.X, p.Y - near.Y)).First();
            var m = Middle(f);
            double d = Js.Hypot(best.X - m.X, best.Y - m.Y);
            if (d > 1e-6) marks.Add((best.X + (best.X - m.X) / d * 0.1, best.Y + (best.Y - m.Y) / d * 0.1));
        }
        if (marks.Count == 0) return null;
        // The nearer ones first: they walk the country in front of them.
        var near3 = marks.OrderBy(p => Js.Hypot(p.X - near.X, p.Y - near.Y)).ThenBy(p => p.X).Take(3).ToList();
        return rng.Pick(near3);
    }
}
