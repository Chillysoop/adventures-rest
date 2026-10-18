namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// A stretch of country that bends the roads: a ridge, a mire, a tangled wood. It is an annular sector
/// around the town, <see cref="Inner"/> to <see cref="Outer"/> days out across <see cref="Arc"/>
/// degrees centred on <see cref="Bearing"/>, and the client raises it out of the ground.
/// </summary>
public sealed class Feature
{
    public int Id { get; init; }
    public required string Name { get; init; }
    public required TerrainKindDef Kind { get; init; }
    public int RegionId { get; init; }

    /// <summary>Party level the country's dangers are built for.</summary>
    public int Tier { get; init; }
    public double Bearing { get; init; }
    public double Arc { get; init; }
    public double Inner { get; init; }
    public double Outer { get; init; }
}

/// <summary>A turn in a party's road, in day units from the town (x east, y south); <see cref="Way"/> is how they walked the stretch that ends here.</summary>
public sealed class Waypoint
{
    public double X { get; init; }
    public double Y { get; init; }

    /// <summary>"road" on open ground, "cross" through <see cref="Feature"/>, "skirt" around it.</summary>
    public string Way { get; init; } = "road";
    public Feature? Feature { get; init; }
}

/// <summary>How a party weighs time against danger when the road forks.</summary>
public sealed record RouteMind(double TimeWeight, double RiskWeight, double Level, string Leader, string? Bold, bool Hurry, bool Wounded);

public enum Pace
{
    Marching,
    Resting,
    Camped,
    Delving,
}

/// <summary>Where a party is at a moment, in day units from the town.</summary>
public readonly record struct Whereabouts(double X, double Y, Pace Pace);

/// <summary>
/// Roads through the wilds. Parties plan a route at the gate, choosing at each stretch of hard country
/// between the short way through it and the long way round; they march from dawn to dusk with a rest at
/// noon and camp at night. When they reach the site the venture is settled (the road out, the delve,
/// the road home) and the town hears of it when they walk back in.
/// </summary>
public static class Roads
{
    // ---- Generation ----

    /// <summary>Put hard country in front of each region's sites, never on a site or another feature.</summary>
    public static void Generate(Town town, Rng rng)
    {
        var rules = town.Content.Roads;
        var w = town.Content.World;
        var spots = town.Sites.Select(s => World.Spot(town, s)).ToList();
        foreach (var region in town.Regions)
        {
            if (!rules.Themes.TryGetValue(region.Theme.Id, out var ids) || ids.Count == 0) continue;
            int n = rng.Range(rules.FeaturesPerRegion[0], rules.FeaturesPerRegion[^1] + 1);
            for (int i = 0; i < n; i++)
            {
                var kind = rules.Kinds.FirstOrDefault(k => k.Id == rng.Pick(ids));
                if (kind == null) continue;
                for (int tries = 0; tries < 10; tries++)
                {
                    double arc = region.Arc * (rules.Arc[0] + rng.Next() * (rules.Arc[^1] - rules.Arc[0]));
                    double bearing = (region.Bearing + (rng.Next() - 0.5) * 0.8 * region.Arc + 360) % 360;
                    // The open ground along this arc between the sites that lie on it: hard country fills a gap.
                    var reaches = spots
                        .Where(p => Math.Abs(AngleDiff(Math.Atan2(p.X, -p.Y) * 180 / Math.PI, bearing)) < arc / 2 + 12)
                        .Select(p => Math.Sqrt(p.X * p.X + p.Y * p.Y))
                        .Append(0.15)
                        .Append(region.Days + 2.5)
                        .OrderBy(r => r)
                        .ToList();
                    var gaps = new List<(double A, double B)>();
                    for (int g = 1; g < reaches.Count; g++)
                        if (reaches[g] - reaches[g - 1] >= 0.62) gaps.Add((reaches[g - 1] + 0.22, reaches[g] - 0.22));
                    // The first lies in front of the region's sites; another may lie anywhere open.
                    var fits = i == 0 ? gaps.Where(g => g.B <= region.Days + 0.3).ToList() : gaps;
                    if (fits.Count == 0) continue;
                    var gap = i == 0 ? fits[^1] : rng.Pick(fits);
                    double depth = Math.Min(gap.B - gap.A, 0.3 + rng.Next() * 0.35);
                    double outer = gap.B - rng.Next() * (gap.B - gap.A - depth);
                    var f = new Feature
                    {
                        Id = town.Terrain.Count,
                        Name = Name(rng, kind),
                        Kind = kind,
                        RegionId = region.Id,
                        Tier = TierAt(town, outer),
                        Bearing = Js.Round(bearing * 10) / 10.0,
                        Arc = Js.Round(arc * 10) / 10.0,
                        Inner = Js.Round(Math.Max(0.4, outer - depth) * 100) / 100.0,
                        Outer = Js.Round(outer * 100) / 100.0,
                    };
                    if (f.Outer - f.Inner < 0.2 || spots.Any(p => Contains(f, p.X, p.Y, 0.18)) || town.Terrain.Any(o => Overlaps(o, f))) continue;
                    town.Terrain.Add(f);
                    break;
                }
            }
        }
    }

    private static string Name(Rng rng, TerrainKindDef kind)
    {
        string pattern = kind.Names.Count > 0 ? rng.Pick(kind.Names) : $"the {{A}} {kind.Name}";
        string word = kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Wild";
        return pattern.Replace("{A}", word, StringComparison.Ordinal);
    }

    private static double AngleDiff(double a, double b)
    {
        double d = (a - b) % 360;
        if (d > 180) d -= 360;
        if (d < -180) d += 360;
        return d;
    }

    private static bool Overlaps(Feature a, Feature b) =>
        a.Inner - 0.2 < b.Outer && b.Inner - 0.2 < a.Outer &&
        Math.Abs(AngleDiff(a.Bearing, b.Bearing)) < (a.Arc + b.Arc) / 2 + 12 / Math.Max(0.5, Math.Min(a.Outer, b.Outer));

    /// <summary>Whether a point lies inside the feature, grown by a margin in days.</summary>
    public static bool Contains(Feature f, double x, double y, double margin = 0)
    {
        double r = Math.Sqrt(x * x + y * y);
        if (r < 1e-6 || r < f.Inner - margin || r > f.Outer + margin) return false;
        double b = Math.Atan2(x, -y) * 180 / Math.PI;
        return Math.Abs(AngleDiff(b, f.Bearing)) <= f.Arc / 2 + margin / r * 180 / Math.PI;
    }

    // ---- Geometry ----

    private readonly record struct P(double X, double Y)
    {
        public static P operator +(P a, P b) => new(a.X + b.X, a.Y + b.Y);
        public static P operator -(P a, P b) => new(a.X - b.X, a.Y - b.Y);
        public static P operator *(P a, double k) => new(a.X * k, a.Y * k);
        public double Length => Math.Sqrt(X * X + Y * Y);
        public static double Cross(P a, P b) => a.X * b.Y - a.Y * b.X;
    }

    private static P Polar(double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return new P(r * Math.Sin(a), -r * Math.Cos(a));
    }

    /// <summary>The convex hull of the feature grown by a margin: what a detour has to walk around.</summary>
    private static List<P> Hull(Feature f, double margin)
    {
        var pts = new List<P>();
        foreach (double r in new[] { Math.Max(0.05, f.Inner - margin), f.Outer + margin })
        {
            double half = f.Arc / 2 + margin / r * 180 / Math.PI;
            for (int i = 0; i <= 8; i++) pts.Add(Polar(f.Bearing - half + 2 * half * i / 8, r));
        }
        pts = pts.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
        var hull = new List<P>();
        for (int pass = 0; pass < 2; pass++)
        {
            int start = hull.Count;
            foreach (var p in pass == 0 ? pts : Enumerable.Reverse(pts))
            {
                while (hull.Count >= start + 2 && P.Cross(hull[^1] - hull[^2], p - hull[^2]) <= 0) hull.RemoveAt(hull.Count - 1);
                hull.Add(p);
            }
            hull.RemoveAt(hull.Count - 1);
        }
        return hull;
    }

    /// <summary>Where a segment runs inside a convex polygon, as fractions along it, or null if it stays out.</summary>
    private static (double In, double Out)? Clip(List<P> poly, P a, P b)
    {
        var d = b - a;
        double lo = 0, hi = 1;
        for (int i = 0; i < poly.Count; i++)
        {
            var e = poly[(i + 1) % poly.Count] - poly[i];
            double num = P.Cross(e, a - poly[i]);
            double den = P.Cross(e, d);
            if (Math.Abs(den) < 1e-12)
            {
                if (num < 0) return null;
                continue;
            }
            double t = -num / den;
            if (den > 0) lo = Math.Max(lo, t);
            else hi = Math.Min(hi, t);
            if (lo > hi) return null;
        }
        return hi - lo > 1e-9 ? (lo, hi) : null;
    }

    private static List<P> Shrink(List<P> poly, double k)
    {
        var c = new P(poly.Average(p => p.X), poly.Average(p => p.Y));
        return poly.Select(p => c + (p - c) * k).ToList();
    }

    private static bool Blocked(List<P> inner, P a, P b) => Clip(inner, a, b) is { } c && (c.Out - c.In) * (b - a).Length > 1e-6;

    /// <summary>The shortest way from a to b around the hull (its corners in order), by a small visibility graph.</summary>
    private static List<P> Around(List<P> hull, P a, P b)
    {
        var inner = Shrink(hull, 0.985);
        var nodes = new List<P> { a, b };
        nodes.AddRange(hull);
        int n = nodes.Count;
        var dist = Enumerable.Repeat(double.MaxValue, n).ToArray();
        var from = Enumerable.Repeat(-1, n).ToArray();
        var done = new bool[n];
        dist[0] = 0;
        for (int step = 0; step < n; step++)
        {
            int u = -1;
            for (int i = 0; i < n; i++)
                if (!done[i] && dist[i] < double.MaxValue && (u < 0 || dist[i] < dist[u])) u = i;
            if (u < 0 || u == 1) break;
            done[u] = true;
            for (int v = 0; v < n; v++)
            {
                if (done[v] || v == u) continue;
                double alt = dist[u] + (nodes[v] - nodes[u]).Length;
                if (alt >= dist[v] || Blocked(inner, nodes[u], nodes[v])) continue;
                dist[v] = alt;
                from[v] = u;
            }
        }
        var path = new List<P>();
        if (from[1] < 0) return path;
        for (int v = from[1]; v > 0; v = from[v]) path.Add(nodes[v]);
        path.Reverse();
        return path;
    }

    private static double PathLength(P start, IEnumerable<P> via, P end)
    {
        double len = 0;
        var cur = start;
        foreach (var p in via.Append(end))
        {
            len += (p - cur).Length;
            cur = p;
        }
        return len;
    }

    // ---- Planning ----

    /// <summary>What drives a party's choices on the road: who leads, how strong they are, how urgent and how hurt.</summary>
    public static RouteMind Mind(Town town, IReadOnlyList<Agent> members, bool hurry, bool wounded)
    {
        var c = town.Content.Roads.Choice;
        var leader = members[0];
        bool BoldOne(Agent a) => a.Has(Trait.Ambitious) || a.Has(Trait.HotTempered);
        var bold = members.FirstOrDefault(BoldOne);
        bool green = members.Any(m => m.Hero!.Expeditions == 0) && !members.Any(m => m.Hero!.Expeditions >= town.Content.World.Companies.Veteran);
        double risk = c.RiskWeight * (BoldOne(leader) ? c.Bold : 1) * (green ? c.Green : 1) * (wounded ? c.Wounded : 1);
        double time = c.TimeWeight * (leader.Has(Trait.Lazy) ? c.Lazy : 1) * (hurry ? c.Urgent : 1);
        double level = members.Average(m => m.Hero!.Level) + Gear.Levels(town.Content, members);
        return new RouteMind(time, risk, level, leader.GivenName, bold?.GivenName, hurry, wounded);
    }

    /// <summary>Whether a party's errand is pressing: a settlement waiting on them, or the warband raiding.</summary>
    public static bool Hurry(Town town, Bounty? bounty, SiteState site) =>
        town.Pleas.Any(p => p.SiteId == site.Def.Id && p.Status == PleaStatus.Open) ||
        (Threat.IsActive(town) && town.Enemy.Stage >= ThreatStage.Raids) ||
        site.Def.Id == town.Content.Enemy.Outpost.Id;

    private static string DaysText(double days, int marchHours)
    {
        double h = days * marchHours;
        if (h < 3) return "a few hours";
        if (h < 7) return "half a day";
        if (h < 14) return "a day";
        return $"{Js.Round(days)} days";
    }

    /// <summary>
    /// Plan a road from one spot to another. At each stretch of hard country in the way, the party weighs
    /// the extra days of going around against the danger of going through (for a party of its level), and
    /// says so; their words go into <paramref name="said"/>.
    /// </summary>
    public static List<Waypoint> Plan(Town town, (double X, double Y) from, (double X, double Y) to, RouteMind mind, Rng rng, List<string> said)
    {
        var rules = town.Content.Roads;
        var start = new P(from.X, from.Y);
        var goal = new P(to.X, to.Y);
        var path = new List<Waypoint>();
        var handled = new HashSet<int>();
        var cur = start;
        for (int guard = 0; guard < 16; guard++)
        {
            // The first stretch of hard country on the straight way on.
            Feature? next = null;
            (double In, double Out) span = default;
            foreach (var f in town.Terrain)
            {
                if (handled.Contains(f.Id)) continue;
                if (Clip(Hull(f, 0), cur, goal) is not { } c) continue;
                if (next == null || c.In < span.In)
                {
                    next = f;
                    span = c;
                }
            }
            if (next == null) break;
            handled.Add(next.Id);

            var d = goal - cur;
            var entry = cur + d * span.In;
            var exit = cur + d * span.Out;
            double through = (exit - entry).Length;
            double crossLen = d.Length + through * (next.Kind.Slow - 1);
            var hull = Hull(next, rules.Margin);
            var inner = Shrink(hull, 0.985);
            bool forced = Clip(inner, cur, cur) != null || Contains(next, cur.X, cur.Y, rules.Margin) || Contains(next, goal.X, goal.Y, rules.Margin);
            var around = forced ? [] : Around(hull, cur, goal);
            if (forced || around.Count == 0)
            {
                path.Add(new Waypoint { X = entry.X, Y = entry.Y });
                path.Add(new Waypoint { X = exit.X, Y = exit.Y, Way = "cross", Feature = next });
                cur = exit;
                continue;
            }
            double extra = PathLength(cur, around, goal) - crossLen;
            double danger = next.Kind.Danger * Math.Clamp((next.Tier + 1.0) / (mind.Level + 1.0), 0.4, 2.5);
            double noise = (rng.Next() - 0.5) * rules.Choice.Noise;
            bool cross = extra > 0 && extra * mind.TimeWeight * 3 > danger * mind.RiskWeight + noise;

            string key = cross
                ? (mind.Bold == mind.Leader ? "crossBold" : mind.Hurry ? "crossHurry" : "cross")
                : (mind.Wounded ? "skirtWounded" : mind.Bold != null && mind.Bold != mind.Leader ? "skirtOverruled" : "skirt");
            if (rules.Texts.TryGetValue(key, out var lines) && lines.Count > 0)
            {
                said.Add(Cap(rng.Pick(lines)
                    .Replace("{leader}", mind.Leader, StringComparison.Ordinal)
                    .Replace("{bold}", mind.Bold ?? mind.Leader, StringComparison.Ordinal)
                    .Replace("{name}", next.Name, StringComparison.Ordinal)
                    .Replace("{through}", next.Kind.Through, StringComparison.Ordinal)
                    .Replace("{days}", DaysText(Math.Abs(extra), rules.MarchHours), StringComparison.Ordinal)));
            }
            if (cross)
            {
                path.Add(new Waypoint { X = entry.X, Y = entry.Y });
                path.Add(new Waypoint { X = exit.X, Y = exit.Y, Way = "cross", Feature = next });
                cur = exit;
            }
            else
            {
                foreach (var p in around) path.Add(new Waypoint { X = p.X, Y = p.Y, Way = "skirt", Feature = next });
                cur = new P(path[^1].X, path[^1].Y);
            }
        }
        path.Add(new Waypoint { X = goal.X, Y = goal.Y });
        return path;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Hours of marching a stretch takes: its length in days, slower through hard country.</summary>
    private static double LegHours(RoadRules rules, (double X, double Y) from, Waypoint to)
    {
        double len = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
        return len * rules.MarchHours * (to.Way == "cross" && to.Feature != null ? to.Feature.Kind.Slow : 1);
    }

    /// <summary>Hours of marching the whole road takes, from <paramref name="from"/>.</summary>
    public static double Hours(Town town, (double X, double Y) from, IReadOnlyList<Waypoint> route)
    {
        double h = 0;
        var cur = from;
        foreach (var w in route)
        {
            h += LegHours(town.Content.Roads, cur, w);
            cur = (w.X, w.Y);
        }
        return h;
    }

    /// <summary>The spot <paramref name="hours"/> of marching along the road.</summary>
    public static (double X, double Y) Along(Town town, (double X, double Y) from, IReadOnlyList<Waypoint> route, double hours)
    {
        var cur = from;
        foreach (var w in route)
        {
            double h = LegHours(town.Content.Roads, cur, w);
            if (hours <= h && h > 0)
            {
                double k = hours / h;
                return (cur.X + (w.X - cur.X) * k, cur.Y + (w.Y - cur.Y) * k);
            }
            hours -= h;
            cur = (w.X, w.Y);
        }
        return cur;
    }

    /// <summary>A road walked backwards, ending at <paramref name="end"/>.</summary>
    public static List<Waypoint> Reverse(IReadOnlyList<Waypoint> route, (double X, double Y) end)
    {
        var back = new List<Waypoint>();
        for (int i = route.Count - 1; i >= 0; i--)
        {
            var (x, y) = i > 0 ? (route[i - 1].X, route[i - 1].Y) : end;
            back.Add(new Waypoint { X = x, Y = y, Way = route[i].Way, Feature = route[i].Feature });
        }
        return back;
    }

    // ---- The march schedule ----

    /// <summary>The two marches of a day: dawn to noon, and an hour after noon until the day's march is done.</summary>
    private static (double A, double B)[] Windows(RoadRules r, int day)
    {
        double d = day * SimTime.HoursPerDay;
        int morning = Math.Clamp(r.Noon - r.Dawn, 0, r.MarchHours);
        return [(d + r.Dawn, d + r.Dawn + morning), (d + r.Noon + 1, d + r.Noon + 1 + r.MarchHours - morning)];
    }

    /// <summary>When a party that sets off at <paramref name="start"/> has marched <paramref name="hours"/>.</summary>
    public static double Finish(Content content, double start, double hours)
    {
        var r = content.Roads;
        if (hours <= 0) return start;
        for (int day = (int)Math.Floor(start / SimTime.HoursPerDay); ; day++)
        {
            foreach (var (a, b) in Windows(r, day))
            {
                if (b <= start) continue;
                double s = Math.Max(a, start);
                double take = Math.Min(b - s, hours);
                hours -= take;
                if (hours <= 1e-9) return s + take;
            }
        }
    }

    /// <summary>Hours marched between two moments.</summary>
    public static double Marched(Content content, double start, double t)
    {
        var r = content.Roads;
        double sum = 0;
        for (int day = (int)Math.Floor(start / SimTime.HoursPerDay); day * SimTime.HoursPerDay < t; day++)
            foreach (var (a, b) in Windows(r, day))
                sum += Math.Max(0, Math.Min(b, t) - Math.Max(a, start));
        return sum;
    }

    /// <summary>When a party that lies down at <paramref name="t"/> gets up rested: the first dawn after eight hours' sleep.</summary>
    public static double WakeAfterRest(Content content, double t)
    {
        double up = t + 8;
        double day = Math.Floor(up / SimTime.HoursPerDay) * SimTime.HoursPerDay + content.Roads.Dawn;
        return day >= up ? day : day + SimTime.HoursPerDay;
    }

    private static Pace PaceAt(Content content, double t)
    {
        var r = content.Roads;
        double h = t % SimTime.HoursPerDay;
        int day = (int)Math.Floor(t / SimTime.HoursPerDay);
        foreach (var (a, b) in Windows(r, day))
            if (t >= a && t < b) return Pace.Marching;
        return h >= r.Noon && h < r.Noon + 1 ? Pace.Resting : Pace.Camped;
    }

    // ---- Parties on the road ----

    /// <summary>Plan the road out for a party leaving town now, and when it will reach the site.</summary>
    internal static void Depart(Town town, Party party, IReadOnlyList<Agent> members, SiteState site, Bounty? bounty)
    {
        var mind = Mind(town, members, Hurry(town, bounty, site), false);
        var spot = World.Spot(town, site);
        party.Out = Plan(town, party.Start, spot, mind, town.Rng, party.Said);
        double outHours = Hours(town, party.Start, party.Out);
        double arrive = Finish(town.Content, party.DepartTick, outHours);
        party.ArriveTick = (int)Math.Ceiling(arrive);
        // A first guess at the homecoming, made good when they reach the site.
        party.ReturnTick = (int)Math.Ceiling(Finish(town.Content, WakeAfterRest(town.Content, arrive), outHours));
    }

    /// <summary>Hourly: parties reaching their sites go in.</summary>
    public static void Hourly(Town town)
    {
        foreach (var party in town.Parties)
        {
            if (party.Returned || party.Resolved || party.RecalledTick != null || town.Tick < party.ArriveTick) continue;
            Arrive(town, party);
        }
    }

    /// <summary>The level of the wilds at a distance from town: the band of the ring that far out.</summary>
    private static int TierAt(Town town, double reach)
    {
        var rings = town.Content.World.Rings;
        foreach (var r in rings)
            if (reach <= r.Days + 0.5) return r.Levels[0];
        return rings.Count > 0 ? rings[^1].Levels[0] : 1;
    }

    private static IReadOnlyList<string> HabitatsAt(Town town, (double X, double Y) p)
    {
        var f = town.Terrain.FirstOrDefault(t => Contains(t, p.X, p.Y, 0.1));
        if (f != null) return f.Kind.Habitats;
        double reach = Math.Sqrt(p.X * p.X + p.Y * p.Y);
        var region = town.Regions
            .OrderBy(g => Math.Abs(AngleDiff(Math.Atan2(p.X, -p.Y) * 180 / Math.PI, g.Bearing)) / Math.Max(1, g.Arc) + Math.Abs(g.Days - reach))
            .ThenBy(g => g.Id)
            .FirstOrDefault();
        return region?.Theme.Habitats ?? ["grassland", "forest", "hills"];
    }

    /// <summary>Walk a road: hard country crossed, the noon rest, and the nights in camp, in the order they come.</summary>
    private static void Walk(Town town, Expedition.Venture v, (double X, double Y) from, IReadOnlyList<Waypoint> route, double start)
    {
        var content = town.Content;
        var r = content.Roads;
        double total = Hours(town, from, route);
        double end = Finish(content, start, total);
        var events = new List<(double At, int Order, Action Act)>();
        double h = 0;
        var cur = from;
        foreach (var w in route)
        {
            double leg = LegHours(r, cur, w);
            if (w.Way == "cross" && w.Feature is { } f) events.Add((h + leg / 2, 0, () => v.Cross(f.Kind, f.Name, f.Tier)));
            h += leg;
            cur = (w.X, w.Y);
        }
        for (int day = (int)Math.Floor(start / SimTime.HoursPerDay); day * SimTime.HoursPerDay < end; day++)
        {
            double d = day * SimTime.HoursPerDay;
            double noon = d + r.Noon, dusk = Windows(r, day)[1].B;
            if (noon > start && noon < end) events.Add((Marched(content, start, noon), 1, () => v.ShortRest(false)));
            if (dusk > start && dusk < end)
            {
                double at = Marched(content, start, dusk);
                var spot = Along(town, from, route, at);
                int tier = TierAt(town, Math.Sqrt(spot.X * spot.X + spot.Y * spot.Y));
                var habitats = HabitatsAt(town, spot);
                events.Add((at, 2, () => v.Camp(tier, habitats)));
            }
        }
        foreach (var (at, _, act) in events.OrderBy(e => e.At).ThenBy(e => e.Order))
        {
            if (v.Wiped) break;
            v.Clock = Finish(content, start, at);
            act();
        }
        if (!v.Wiped) v.Clock = end;
    }

    /// <summary>
    /// A party reaches its site: the road behind it, a night's camp if it is late, the delve (with rests,
    /// a fall-back and a second try), and the road home, chosen anew by how hurt they are.
    /// </summary>
    internal static void Arrive(Town town, Party party)
    {
        party.Resolved = true;
        var content = town.Content;
        var site = Quests.Site(town, party.SiteId)!;
        var spot = World.Spot(town, site);
        if (party.Out.Count == 0) party.Out = [new Waypoint { X = spot.X, Y = spot.Y }];
        var members = party.Members.Select(town.Agent).ToList();
        double arrive = Math.Max(town.Tick, Finish(content, party.DepartTick, Hours(town, party.Start, party.Out)));
        if (site.Cleared)
        {
            // Nobody there: they turn straight round.
            party.Empty = true;
            party.LeaveTick = town.Tick;
            party.Home = party.Start == (0, 0) ? Reverse(party.Out, (0, 0)) : [new Waypoint { X = 0, Y = 0 }];
            party.ReturnTick = (int)Math.Ceiling(Finish(content, town.Tick, Hours(town, spot, party.Home)));
            foreach (var m in members) m.Hero!.AwayUntil = party.ReturnTick;
            return;
        }

        var v = new Expedition.Venture(town.Rng, content, members, site.Def) { Swell = Lairs.Swell(town, site) + party.Swell, Clock = party.DepartTick };
        if (party.Edge != 0) v.Hearten(party.Edge);
        v.Log($"{Expedition.ListNames(members.Select(m => m.GivenName).ToList())} set out for {site.Def.Name}.");
        foreach (var line in party.Said) v.Log(line);
        Walk(town, v, party.Start, party.Out, party.DepartTick);

        var outcome = ExpeditionOutcome.Wiped;
        double leave = v.Clock;
        if (!v.Wiped)
        {
            v.Clock = arrive;
            double hour = arrive % SimTime.HoursPerDay;
            if (hour >= content.Roads.Camp.LateHour || hour < content.Roads.Dawn)
            {
                // Late in the day: camp out of sight and go in at first light.
                double up = WakeAfterRest(content, arrive);
                v.Camp(site.Def.Tier, site.Def.Habitats, Cap(Text(town, "campBefore").Replace("{site}", site.Def.Name, StringComparison.Ordinal)));
                v.Halts.Add(new Halt(arrive, up, "camp"));
                v.Clock = up;
            }
            if (!v.Wiped)
            {
                v.SpendSlots(party.Sendings);
                outcome = v.Delve(site.Def, content.Roads.Retreat.Retries);
            }
            leave = v.Clock;
        }
        party.Halts.AddRange(v.Halts);
        party.LeaveTick = (int)Math.Ceiling(leave);

        if (outcome != ExpeditionOutcome.Wiped)
        {
            // Home again, choosing the roads afresh: a hurt party takes no chances.
            bool hurt = v.HpFraction() < 0.6 || v.Fallen > 0;
            var said = new List<string>();
            party.Home = Plan(town, spot, (0, 0), Mind(town, members, false, hurt), town.Rng, said);
            foreach (var line in said) v.Log(line);
            Walk(town, v, spot, party.Home, party.LeaveTick);
            if (v.Wiped) outcome = ExpeditionOutcome.Wiped;
        }
        if (outcome == ExpeditionOutcome.Wiped)
        {
            party.GoneTick = (int)Math.Ceiling(v.LostAt ?? leave);
            // The town waits for them as long as the walk home would have taken.
            if (party.Home.Count == 0) party.Home = Reverse(party.Out, party.Start);
        }
        party.ReturnTick = Math.Max(party.LeaveTick, (int)Math.Ceiling(Finish(content, party.LeaveTick, Hours(town, spot, party.Home))));
        foreach (var m in members) m.Hero!.AwayUntil = party.ReturnTick;
        party.Result = v.Result(outcome, site.Def.Name);
    }

    private static string Text(Town town, string key) =>
        town.Content.Roads.Texts.TryGetValue(key, out var lines) && lines.Count > 0 ? town.Rng.Pick(lines) : "";

    /// <summary>A party called home turns round where it stands and walks back the way it came.</summary>
    internal static void Recall(Town town, Party party)
    {
        double walked = Marched(town.Content, party.DepartTick, town.Tick);
        // A party that set out from the wilds (explorers who chose to fight) still has the road from there to town.
        walked += Hours(town, party.Start, [new Waypoint { X = 0, Y = 0 }]);
        party.ReturnTick = (int)Math.Ceiling(Finish(town.Content, town.Tick, walked));
    }

    /// <summary>Where a party is at moment <paramref name="t"/> (ticks, fractional), or null once it is lost.</summary>
    public static Whereabouts? Where(Town town, Party party, double t)
    {
        var content = town.Content;
        var site = Quests.Site(town, party.SiteId)!;
        var spot = World.Spot(town, site);
        if (t <= party.DepartTick) return new Whereabouts(party.Start.X, party.Start.Y, Pace.Camped);
        if (party.GoneTick is int gone && t >= gone) return null;
        if (party.RecalledTick is int rt)
        {
            double m0 = Marched(content, party.DepartTick, Math.Min(t, rt));
            double back = t > rt ? Marched(content, rt, t) : 0;
            var p = Along(town, party.Start, party.Out, Math.Max(0, m0 - back));
            return new Whereabouts(p.X, p.Y, PaceAt(content, t));
        }
        if (t < party.ArriveTick || !party.Resolved)
        {
            if (t >= party.ArriveTick) return new Whereabouts(spot.X, spot.Y, Pace.Camped);
            var p = Along(town, party.Start, party.Out, Marched(content, party.DepartTick, t));
            return new Whereabouts(p.X, p.Y, PaceAt(content, t));
        }
        if (t < party.LeaveTick)
        {
            var halt = party.Halts.LastOrDefault(h => t >= h.From && t < h.To);
            if (halt != null && halt.Kind != "delve")
            {
                // Camped a little way back down the road.
                var back = Along(town, spot, party.Home.Count > 0 ? party.Home : Reverse(party.Out, party.Start), 0.8);
                return new Whereabouts(back.X, back.Y, Pace.Camped);
            }
            return new Whereabouts(spot.X, spot.Y, Pace.Delving);
        }
        var q = Along(town, spot, party.Home, Marched(content, party.LeaveTick, t));
        return new Whereabouts(q.X, q.Y, t >= party.ReturnTick ? Pace.Camped : PaceAt(content, t));
    }

    /// <summary>The roads a party walked and could describe: out (as far as it got) and home. Each is a line of spots.</summary>
    public static List<List<(double X, double Y)>> Walked(Town town, Party party)
    {
        var lines = new List<List<(double X, double Y)>>();
        var site = Quests.Site(town, party.SiteId)!;
        var spot = World.Spot(town, site);
        var outLine = new List<(double X, double Y)> { party.Start };
        if (party.RecalledTick is int rt)
        {
            double walked = Marched(town.Content, party.DepartTick, rt);
            var cur = party.Start;
            double h = 0;
            foreach (var w in party.Out)
            {
                double leg = LegHours(town.Content.Roads, cur, w);
                if (h + leg >= walked)
                {
                    outLine.Add(Along(town, party.Start, party.Out, walked));
                    break;
                }
                outLine.Add((w.X, w.Y));
                h += leg;
                cur = (w.X, w.Y);
            }
            lines.Add(outLine);
            return lines;
        }
        outLine.AddRange(party.Out.Count > 0 ? party.Out.Select(w => (w.X, w.Y)) : [spot]);
        lines.Add(outLine);
        if (party.Home.Count > 0 && party.GoneTick == null) lines.Add([spot, .. party.Home.Select(w => (w.X, w.Y))]);
        return lines;
    }

    /// <summary>A few words for the town's news on the way a party chose: ", the long way round the Black Mire".</summary>
    public static string Summary(Party party)
    {
        var skirted = party.Out.Where(w => w.Way == "skirt" && w.Feature != null).Select(w => w.Feature!).Distinct().ToList();
        var crossed = party.Out.Where(w => w.Way == "cross" && w.Feature != null).Select(w => w.Feature!).Distinct().ToList();
        var bits = new List<string>();
        if (crossed.Count > 0) bits.Add($"straight {crossed[0].Kind.Through} {crossed[0].Name}");
        if (skirted.Count > 0) bits.Add($"the long way round {skirted[0].Name}");
        return bits.Count == 0 ? "" : $", taking {string.Join(" and ", bits)}";
    }
}
