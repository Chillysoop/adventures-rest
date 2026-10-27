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

    /// <summary>Where its <see cref="Bearing"/> and rings are measured from, in day units from the town: the world's heart for the wilds' own country, (0, 0) for the town's.</summary>
    public double Ox { get; set; }
    public double Oy { get; set; }

    /// <summary>A gorge's rope bridge: "" none, "rope", "cut" by raiders, or rebuilt in "stone" (<see cref="Bridges"/>).</summary>
    public string Bridge { get; set; } = "";
    public string BridgeName { get; set; } = "";

    /// <summary>
    /// Its outline in days from the town (<see cref="Country"/>); empty for the old ring sectors, which <see cref="Bearing"/>,
    /// <see cref="Arc"/>, <see cref="Inner"/> and <see cref="Outer"/> describe. A shaped feature keeps those as the sector round it.
    /// </summary>
    public List<(double X, double Y)> Shape { get; init; } = [];

    /// <summary>Its outline's box (min x, min y, max x, max y), worked out once.</summary>
    [NotSaved] internal double[]? Box;
}

/// <summary>A turn in a party's road, in day units from the town (x east, y south); <see cref="Way"/> is how they walked the stretch that ends here.</summary>
public sealed class Waypoint
{
    public double X { get; init; }
    public double Y { get; init; }

    /// <summary>"road" on open ground, "cross" through <see cref="Feature"/>, "skirt" around it.</summary>
    public string Way { get; init; } = "road";
    public Feature? Feature { get; init; }

    /// <summary>The ford or bridge the stretch ends at, for "ford" and "bridge" (<see cref="Passage.Crossings"/>).</summary>
    public string Crossing { get; init; } = "";

    /// <summary>Share of the hard country's slowness the party takes off on a "cross": mounts (snow bears on the ice), guides, a skilled leader.</summary>
    public double Ease { get; init; }

    /// <summary>Share of the time a worn trail saves on the stretch (<see cref="Country.Quick"/>).</summary>
    public double Quick { get; init; }

    /// <summary>Hours lost on the stretch when the navigator lost the way (<see cref="Wayfaring.Navigate"/>).</summary>
    public double Delay { get; init; }

    /// <summary>The old gateway a "portal" step goes through (<see cref="Wayfaring.Shortcut"/>).</summary>
    public string Gate { get; init; } = "";

    /// <summary>On a "sail" stretch aboard a ship: hours at sea for each hour of march (<see cref="RouteMind.Sail"/>); 0 for the open water's own pace.</summary>
    public double Sail { get; init; }

    public Waypoint Lost(double hours) => new() { X = X, Y = Y, Way = Way, Feature = Feature, Crossing = Crossing, Ease = Ease, Quick = Quick, Sail = Sail, Delay = Delay + hours, Gate = Gate };
}

/// <summary>How a party weighs time against danger when the road forks.</summary>
public sealed record RouteMind(double TimeWeight, double RiskWeight, double Level, string Leader, string? Bold, bool Hurry, bool Wounded)
{
    /// <summary>What the party can cross that nobody walks: the river anywhere, open water, sheer heights (<see cref="Passage.Abilities"/>).</summary>
    public Ways Ways { get; init; }

    /// <summary>Who carries them over it, for the telling: "Mira's griffon", "on Oswin's carpet".</summary>
    public string Carrier { get; init; } = "";

    /// <summary>Hard country (by kind) their mounts carry them through at an easier pace, and how much of its slowness that takes off.</summary>
    public IReadOnlyDictionary<string, double> Eases { get; init; } = new Dictionary<string, double>();

    /// <summary>Who finds the way through hard country, and their Survival (<see cref="Wayfaring"/>); "" for nobody.</summary>
    public string Navigator { get; init; } = "";
    public int NavBonus { get; init; }

    /// <summary>Hours at sea for each hour of march over open sea, aboard the harbour's ship (<see cref="Voyages.SailPace"/>); 0 for none aboard.</summary>
    public double Sail { get; init; }

    /// <summary>Who tries an old gateway's riddle, and their Investigation.</summary>
    public string Puzzler { get; init; } = "";
    public int PuzzleBonus { get; init; }

    /// <summary>The planning's own dice, off the town's stream; null plans with no rolls.</summary>
    public Rng? Dice { get; init; }

    /// <summary>Navigation DC the sky adds as they set off: fog, a storm (<see cref="Weather.Murk"/>).</summary>
    public int Murk { get; init; }

    /// <summary>Out exploring: they keep to the land's lines, along the river and the edges of hard country (<see cref="Country"/>).</summary>
    public bool Explorer { get; init; }

    /// <summary>Places they keep away from: where they were set on along the way out.</summary>
    public IReadOnlyList<(double X, double Y)> Avoid { get; init; } = [];
}

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

    /// <summary>Hard country through the wilds in real shapes (<see cref="Country.Lay"/>), after the mountain walls between the far rings.</summary>
    public static void Generate(Town town, Rng rng)
    {
        var spots = town.Sites.Select(s => World.Spot(town, s)).ToList();
        Ranges(town, rng, spots);
        Country.Lay(town, rng, spots);
        Country.Home(town, spots);
        town.Shaped = true;
    }

    /// <summary>
    /// Mountain walls in the gaps between the far rings (<see cref="RangeRules"/>), each cut into stretches by its passes:
    /// one wherever a site stands in its line, and a few more. A party on foot goes through a pass or the long way round.
    /// </summary>
    public static void Ranges(Town town, Rng rng, List<(double X, double Y)> spots)
    {
        var rr = town.Content.Roads.Ranges;
        var kind = town.Content.Roads.Kinds.FirstOrDefault(k => k.Id == rr.Kind);
        var rings = town.Content.World.Rings;
        if (kind == null) return;
        for (int ring = rr.FromRing; ring + 1 < rings.Count; ring++)
        {
            // The wall runs between the sites of this ring and the next.
            double mid = (rings[ring].Days * 1.12 + 0.14 + rings[ring + 1].Days * 0.82) / 2;
            int walls = rng.Range(rr.PerGap[0], rr.PerGap[^1] + 1);
            for (int n = 0; n < walls; n++)
            {
                double span = rr.Arc[0] + rng.Next() * (rr.Arc[^1] - rr.Arc[0]);
                double centre = rng.Next() * 360;
                double depth = rr.Depth[0] + rng.Next() * (rr.Depth[^1] - rr.Depth[0]);
                double inner = Js.Round((mid - depth / 2) * 100) / 100.0, outer = Js.Round((mid + depth / 2) * 100) / 100.0;
                double half = rr.PassHalf / mid * 180 / Math.PI;
                // Passes, as offsets in degrees from the wall's west end: at the sites in its line, then a few anywhere inside it.
                var cuts = new List<double>();
                foreach (var p in spots)
                {
                    double r = Math.Sqrt(p.X * p.X + p.Y * p.Y);
                    if (r < inner - rr.PassHalf || r > outer + rr.PassHalf) continue;
                    double off = AngleDiff(Math.Atan2(p.X, -p.Y) * 180 / Math.PI, centre) + span / 2;
                    if (off > -half && off < span + half) cuts.Add(off);
                }
                int extra = rng.Range(rr.Passes[0], rr.Passes[^1] + 1);
                for (int k = 0; k < extra; k++) cuts.Add(span * (0.15 + rng.Next() * 0.7));
                cuts.Sort();
                string name = Name(rng, kind);
                double from = 0, least = rr.Least / mid * 180 / Math.PI;
                var stretches = new List<Feature>();
                foreach (double c in cuts.Append(span + half))
                {
                    double to = Math.Min(span, c - half);
                    if (to - from >= least)
                    {
                        double bearing = (centre - span / 2 + (from + to) / 2 + 720) % 360;
                        var region = town.Regions.Where(g => g.Ring == ring).OrderBy(g => Math.Abs(AngleDiff(g.Bearing, bearing))).ThenBy(g => g.Id).FirstOrDefault();
                        // The wall bends in and out along its line and swells and thins (Country.Wall).
                        var shape = Country.Wall(town.Content.Roads.Country, rng, bearing, to - from, inner, outer);
                        stretches.Add(Country.Shaped(town, town.Terrain.Count + stretches.Count, name, kind, region?.Id ?? 0, shape));
                    }
                    from = Math.Max(from, c + half);
                }
                // A wall needs a pass to be a choke point, and must not stand on a site or across other hard country.
                if (stretches.Count < 2 || stretches.Any(f => spots.Any(p => Contains(f, p.X, p.Y, 0.18)) || town.Terrain.Any(o => Overlaps(o, f)))) continue;
                town.Terrain.AddRange(stretches);
            }
        }
    }

    internal static string Name(Rng rng, TerrainKindDef kind)
    {
        string pattern = kind.Names.Count > 0 ? rng.Pick(kind.Names) : $"the {{A}} {kind.Name}";
        string word = kind.Words.Count > 0 ? rng.Pick(kind.Words) : "Wild";
        return pattern.Replace("{A}", word, StringComparison.Ordinal);
    }

    internal static double AngleDiff(double a, double b)
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
        if (f.Shape.Count >= 3) return Country.Inside(f, x, y, margin);
        x -= f.Ox;
        y -= f.Oy;
        double r = Math.Sqrt(x * x + y * y);
        if (r < 1e-6 || r < f.Inner - margin || r > f.Outer + margin) return false;
        double b = Math.Atan2(x, -y) * 180 / Math.PI;
        return Math.Abs(AngleDiff(b, f.Bearing)) <= f.Arc / 2 + margin / r * 180 / Math.PI;
    }

    /// <summary>A spot in day units from the town as the feature measures it: days out from its middle and the bearing from there.</summary>
    public static (double R, double Bearing) Local(Feature f, double x, double y)
    {
        double dx = x - f.Ox, dy = y - f.Oy;
        return (Math.Sqrt(dx * dx + dy * dy), (Math.Atan2(dx, -dy) * 180 / Math.PI + 360) % 360);
    }

    /// <summary>The spot on a bearing this many days out from where the feature is measured, in day units from the town.</summary>
    public static (double X, double Y) At(Feature f, double bearing, double r)
    {
        double a = bearing * Math.PI / 180;
        return (f.Ox + r * Math.Sin(a), f.Oy - r * Math.Cos(a));
    }

    /// <summary>The middle of the feature's band, in day units from the town.</summary>
    public static (double X, double Y) Middle(Feature f) => Country.Middle(f);

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
            for (int i = 0; i <= 8; i++) pts.Add(Polar(f.Bearing - half + 2 * half * i / 8, r) + new P(f.Ox, f.Oy));
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

    /// <summary>A point inside a hull moved back toward the town until it is clear of it (itself when it is outside).</summary>
    private static P Clear(List<P> inner, P p)
    {
        for (int i = 0; i < 40 && Clip(inner, p, p * 1.0001) != null; i++) p *= 0.97;
        return p;
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

    /// <summary>Whether a way from <paramref name="start"/> by <paramref name="via"/> to <paramref name="end"/> meets the river anywhere.</summary>
    private static bool Wades(Town town, P start, IEnumerable<P> via, P end)
    {
        var cur = start;
        foreach (var p in via.Append(end))
        {
            if (Passage.MeetsRiver(town, (cur.X, cur.Y), (p.X, p.Y)) != null) return true;
            cur = p;
        }
        return false;
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

    /// <summary>How much of a hard country's slowness the guides of an ally take off (the elves' paths through the deep wood).</summary>
    public static double Guided(Town town, string kind)
    {
        double best = 0;
        foreach (var s in town.Settlements)
            if (s.Allied && !s.Razed && s.Kin.Boon.Guides.TryGetValue(kind, out var g)) best = Math.Max(best, g);
        return best;
    }

    /// <summary>What drives a party's choices on the road: who leads, how strong they are, how urgent and how hurt.</summary>
    public static RouteMind Mind(Town town, IReadOnlyList<Agent> members, bool hurry, bool wounded)
    {
        var c = town.Content.Roads.Choice;
        // A company with nobody left standing still has a road home to draw, if no one to choose it.
        if (members.Count == 0) return new RouteMind(c.TimeWeight, c.RiskWeight, 1, "", null, hurry, wounded);
        var leader = members[0];
        bool BoldOne(Agent a) => a.Has(Trait.Ambitious) || a.Has(Trait.HotTempered);
        var bold = members.FirstOrDefault(BoldOne);
        bool green = members.Any(m => m.Hero!.Expeditions == 0) && !members.Any(m => m.Hero!.Expeditions >= town.Content.World.Companies.Veteran);
        double risk = c.RiskWeight * (BoldOne(leader) ? c.Bold : 1) * (green ? c.Green : 1) * (wounded ? c.Wounded : 1);
        double time = c.TimeWeight * (leader.Has(Trait.Lazy) ? c.Lazy : 1) * (hurry ? c.Urgent : 1);
        double level = members.Average(m => m.Hero!.Level) + Gear.Levels(town.Content, members);
        var nav = Wayfaring.Assign(town, members).FirstOrDefault(j => j.Job == "navigate");
        var puzzler = members.OrderByDescending(m => Skills.Bonus(town, m, "investigation")).ThenBy(m => m.Id).First();
        return new RouteMind(time, risk, level, leader.GivenName, bold?.GivenName, hurry, wounded)
        {
            Navigator = nav?.Name ?? "",
            NavBonus = nav?.Bonus ?? 0,
            Puzzler = puzzler.GivenName,
            PuzzleBonus = Skills.Bonus(town, puzzler, "investigation"),
            Dice = Wayfaring.Dice(town, leader.Id),
            Ways = Passage.Abilities(town, members),
            Carrier = Passage.Carrier(town, members),
            Eases = town.Content.Roads.Kinds.Select(k => (k.Id, E: Math.Max(Passage.Eased(town, members, k.Id), Guided(town, k.Id)))).Where(k => k.E > 0).ToDictionary(k => k.Id, k => k.E),
        };
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
        var path = new List<Waypoint>();
        var cur = from;
        var goal = to;
        // Through the town gate, never over the walls: out of it at the start, in by it at the end.
        var gate = Passage.Gate(town);
        bool home = gate != null && Js.Hypot(goal.X, goal.Y) < 1e-6;
        if (gate is { } g && Js.Hypot(cur.X, cur.Y) < 1e-6)
        {
            path.Add(new Waypoint { X = g.X, Y = g.Y, Way = "gate" });
            cur = g;
        }
        if (home) goal = gate!.Value;
        // A known old gateway near the way can save them days: walk to the near arch and step out of the far one.
        var hops = new List<((double X, double Y) To, string Gate)>();
        if (Wayfaring.Shortcut(town, cur, goal, mind, said) is { } cut) hops.AddRange([(cut.In, ""), (cut.Out, cut.Gate.Name)]);
        hops.Add((goal, ""));
        foreach (var (hop, through) in hops)
        {
            if (through != "")
            {
                path.Add(new Waypoint { X = hop.X, Y = hop.Y, Way = "portal", Gate = through });
                cur = hop;
                continue;
            }
            // The river is crossed at its fords and bridges, unless they can swim or fly it anywhere; hard country is met on each stretch.
            var stretches = (mind.Ways & Ways.River) != 0 ? [new Waypoint { X = hop.X, Y = hop.Y }] : Passage.Ford(town, cur, [new Waypoint { X = hop.X, Y = hop.Y }], said);
            foreach (var w in stretches)
            {
                // Picked over the land, the road only meets the river at its crossings; the old straight way may need one found.
                var picked = Country.Stretch(town, cur, (w.X, w.Y), mind, rng, said);
                var leg = picked ?? Stretch(town, cur, (w.X, w.Y), mind, rng, said);
                leg[^1] = leg[^1].Delay > 0 ? w.Lost(leg[^1].Delay) : w;
                // Going round hard country can bend the road back over the river: those legs take a crossing too.
                path.AddRange((mind.Ways & Ways.River) != 0 || picked != null ? leg : Passage.Ford(town, cur, leg, said));
                cur = (w.X, w.Y);
            }
        }
        if (home) path.Add(new Waypoint { X = 0, Y = 0, Way = "gate" });
        return (mind.Ways & Ways.Sheer) != 0 ? path : RoundCliffs(town, from, path);
    }

    /// <summary>The road bent round any plateau near the town (<see cref="Cliffs"/>) a stretch would climb over.</summary>
    private static List<Waypoint> RoundCliffs(Town town, (double X, double Y) from, List<Waypoint> path)
    {
        var outp = new List<Waypoint>(path.Count);
        var cur = from;
        foreach (var w in path)
        {
            if (w.Way != "portal")
                foreach (var (x, y) in Passage.RoundCliffs(town, cur, (w.X, w.Y))) outp.Add(new Waypoint { X = x, Y = y });
            outp.Add(w);
            cur = (w.X, w.Y);
        }
        return outp;
    }

    /// <summary>One stretch of a road, from one spot to the next with no river between: round or through the hard country on the way.</summary>
    private static List<Waypoint> Stretch(Town town, (double X, double Y) from, (double X, double Y) to, RouteMind mind, Rng rng, List<string> said)
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
            // A lake or sheer heights: nobody walks over. Those who can fly, walk on water or climb go straight across; the rest go round.
            var needs = Bridges.Needs(next);
            if (needs != Ways.None)
            {
                // In the hollow before it (inside its hull, not in it): back off toward the town first, then round.
                var back = cur;
                var near = goal;
                if (around.Count == 0 && (mind.Ways & needs) != needs && !Contains(next, cur.X, cur.Y) && !Contains(next, goal.X, goal.Y))
                {
                    back = Clear(inner, cur);
                    near = Clear(inner, goal);
                    around = Around(hull, back, near);
                    if (around.Count > 0 && (back - cur).Length > 1e-9) path.Add(new Waypoint { X = back.X, Y = back.Y });
                }
                bool flies = (mind.Ways & needs) == needs;
                // The bone ferryman takes anyone over his water, for a toll; the town's own boats take them over its sea.
                // Aboard the harbour's ship they sail even where they could fly; the open sea takes nobody else but fliers.
                bool boats = next.Kind.Boats && (!flies || next.Kind.Ship && mind.Sail > 0);
                bool ferry = !flies && (boats || next.Kind.Wonder?.Kind == "ferry");
                bool over = flies || ferry || around.Count == 0;
                if (!ferry && Passage.Rules(town).Texts.TryGetValue(over ? "over" : "round", out var told) && told.Count > 0)
                    said.Add(Cap(rng.Pick(told)
                        .Replace("{leader}", mind.Leader, StringComparison.Ordinal)
                        .Replace("{carrier}", mind.Carrier, StringComparison.Ordinal)
                        .Replace("{name}", next.Name, StringComparison.Ordinal)
                        .Replace("{days}", DaysText(Math.Max(0, PathLength(cur, around, goal) - d.Length), rules.MarchHours), StringComparison.Ordinal)));
                if (over)
                {
                    path.Add(new Waypoint { X = entry.X, Y = entry.Y });
                    path.Add(new Waypoint { X = exit.X, Y = exit.Y, Way = boats ? "sail" : ferry ? "ferry" : "fly", Feature = next, Sail = boats && next.Kind.Ship ? mind.Sail : 0 });
                    if (boats) said.Add(next.Kind.Ship && mind.Carrier != "" ? $"{Cap(mind.Carrier)} carries them over {next.Name}." : $"The town's boats take them over {next.Name}.");
                    cur = exit;
                }
                else
                {
                    foreach (var p in around) path.Add(new Waypoint { X = p.X, Y = p.Y, Way = "skirt", Feature = next });
                    if ((near - goal).Length > 1e-9) path.Add(new Waypoint { X = near.X, Y = near.Y, Way = "skirt", Feature = next });
                    cur = new P(path[^1].X, path[^1].Y);
                }
                continue;
            }
            // The way round would wade the river this stretch keeps to its own side of: through it is the only way.
            if (!forced && around.Count > 0 && (mind.Ways & Ways.River) == 0 && Wades(town, cur, around, goal)) around = [];
            if (forced || around.Count == 0)
            {
                path.Add(new Waypoint { X = entry.X, Y = entry.Y });
                path.Add(new Waypoint { X = exit.X, Y = exit.Y, Way = "cross", Feature = next, Ease = mind.Eases.GetValueOrDefault(next.Kind.Id) });
                Lose(path, Wayfaring.Navigate(town.Content, mind, next, through * next.Kind.Slow * rules.MarchHours, said));
                cur = exit;
                continue;
            }
            double ease = mind.Eases.GetValueOrDefault(next.Kind.Id);
            crossLen -= through * (next.Kind.Slow - 1) * ease;
            double extra = PathLength(cur, around, goal) - crossLen;
            double danger = next.Kind.Danger * (1 - ease * 0.5) * Math.Clamp((next.Tier + 1.0) / (mind.Level + 1.0), 0.4, 2.5);
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
                path.Add(new Waypoint { X = exit.X, Y = exit.Y, Way = "cross", Feature = next, Ease = ease });
                Lose(path, Wayfaring.Navigate(town.Content, mind, next, through * next.Kind.Slow * rules.MarchHours, said));
                cur = exit;
            }
            else
            {
                foreach (var p in around) path.Add(new Waypoint { X = p.X, Y = p.Y, Way = "skirt", Feature = next });
                Lose(path, Wayfaring.Navigate(town.Content, mind, next, PathLength(cur, around.Take(around.Count - 1), around[^1]) * rules.MarchHours, said));
                cur = new P(path[^1].X, path[^1].Y);
            }
        }
        path.Add(new Waypoint { X = goal.X, Y = goal.Y });
        return path;
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>The hours a lost navigator cost, put on the last turn of the stretch.</summary>
    private static void Lose(List<Waypoint> path, double hours)
    {
        if (hours > 0 && path.Count > 0) path[^1] = path[^1].Lost(hours);
    }

    /// <summary>Hours of marching a stretch takes: its length in days, slower through hard country.</summary>
    private static double LegHours(RoadRules rules, (double X, double Y) from, Waypoint to)
    {
        // Stepping through an old gateway takes no time at all.
        if (to.Way == "portal") return 0;
        double len = Math.Sqrt((to.X - from.X) * (to.X - from.X) + (to.Y - from.Y) * (to.Y - from.Y));
        return len * rules.MarchHours * (to.Way == "cross" && to.Feature != null ? 1 + (to.Feature.Kind.Slow - 1) * (1 - to.Ease) : to.Way == "sail" && to.Feature != null ? (to.Sail > 0 ? to.Sail : to.Feature.Kind.Slow) : 1) * (1 - to.Quick) + to.Delay;
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
            back.Add(new Waypoint { X = x, Y = y, Way = route[i].Way, Feature = route[i].Feature, Crossing = route[i].Crossing, Ease = route[i].Ease, Quick = route[i].Quick, Sail = route[i].Sail });
        }
        return back;
    }

    /// <summary>
    /// A planned road with the weather on it: each stretch slowed by the sky over it when they walk it (snow, mud, storms;
    /// only the wind aloft), sampled a day's march at a time. The worst of it goes into <paramref name="said"/>.
    /// </summary>
    public static List<Waypoint> Weathered(Town town, (double X, double Y) from, List<Waypoint> route, double start, bool aloft, List<string> said)
    {
        var content = town.Content;
        var r = content.Roads;
        var road = Weather.Rules(town).Road;
        var outp = new List<Waypoint>(route.Count);
        double h = 0, lost = 0, worst = 0;
        string why = "", place = "";
        WeatherNow seen = default;
        var cur = from;
        foreach (var w in route)
        {
            double leg = LegHours(r, cur, w);
            // A ship's pace at sea already reads the weather (Voyages.SailPace).
            if (leg <= 0 || w.Way == "sail" && w.Sail > 0)
            {
                outp.Add(w);
                cur = (w.X, w.Y);
                continue;
            }
            int n = Math.Max(1, (int)Math.Ceiling(leg / Math.Max(1, r.MarchHours)));
            double share = 0;
            for (int i = 0; i < n; i++)
            {
                double k = (i + 0.5) / n;
                var (x, y) = (cur.X + (w.X - cur.X) * k, cur.Y + (w.Y - cur.Y) * k);
                var (extra, cause, now) = Weather.Slow(town, x, y, (int)Finish(content, start, h + leg * k), aloft);
                share += extra / n;
                if (extra * leg / n > worst && cause != "")
                {
                    worst = extra * leg / n;
                    why = cause;
                    seen = now;
                    place = w.Feature?.Name ?? (w.Crossing != "" ? w.Crossing : Weather.RegionAt(town, x, y)?.Name ?? "the wilds");
                }
            }
            double add = Js.Round(leg * share * 10) / 10.0;
            outp.Add(add > 0 ? w.Lost(add) : w);
            lost += add;
            h += leg + add;
            cur = (w.X, w.Y);
        }
        if (lost >= 4 && why != "")
        {
            string line = road.Line(new Rng(unchecked((int)Voices.Hash("weather-road", town.Seed, (int)start, route.Count))), why);
            if (line != "")
                said.Add(Cap(line.Replace("{place}", place, StringComparison.Ordinal)
                    .Replace("{days}", DaysText(lost / r.MarchHours, r.MarchHours), StringComparison.Ordinal)
                    .Replace("{kind}", Weather.Kind(town, seen.Kind).Name.ToLowerInvariant(), StringComparison.Ordinal)));
        }
        return outp;
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
        var spot = World.Spot(town, site);
        var mind = Reaches.Embark(town, party, members, Mind(town, members, Hurry(town, bounty, site), false), site);
        mind = Passage.Board(town, party, mind, party.Start, spot, site.Def.Aloft && party.ShipId == null && Reaches.DockReaches(town, site));
        mind = Voyages.Embark(town, party, members, mind, party.Start, spot);
        mind = mind with { Murk = Weather.Murk(town, party.Start.X, party.Start.Y, town.Tick) };
        party.Out = Weathered(town, party.Start, Plan(town, party.Start, spot, mind, town.Rng, party.Said), party.DepartTick, party.Airship, party.Said);
        double outHours = Hours(town, party.Start, party.Out);
        double arrive = Finish(town.Content, party.DepartTick, outHours);
        party.ArriveTick = (int)Math.Ceiling(arrive);
        // A first guess at the homecoming, made good when they reach the site.
        party.ReturnTick = (int)Math.Ceiling(Finish(town.Content, WakeAfterRest(town.Content, arrive), outHours));
        party.Expected = party.ReturnTick;
        party.HeardTick = party.DepartTick;
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

    /// <summary>The level of the wilds at a distance from the world's heart: the band of the ring that far out.</summary>
    internal static int TierAt(Town town, double reach)
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
        var (reach, bearing) = World.FromHeart(town, p.X, p.Y);
        var region = town.Regions
            .OrderBy(g => Math.Abs(AngleDiff(bearing, g.Bearing)) / Math.Max(1, g.Arc) + Math.Abs(World.Out(g) - reach))
            .ThenBy(g => g.Id)
            .FirstOrDefault();
        return region?.Theme.Habitats ?? ["grassland", "forest", "hills"];
    }

    /// <summary>
    /// Walk a road: hard country crossed, the noon rest, and the nights in camp, in the order they come. Returns where on it
    /// they were set on.
    /// </summary>
    private static List<(double X, double Y)> Walk(Town town, Expedition.Venture v, Party party, (double X, double Y) from, IReadOnlyList<Waypoint> route, double start)
    {
        var troubled = new List<(double X, double Y)>();
        var content = town.Content;
        var r = content.Roads;
        double total = Hours(town, from, route);
        double end = Finish(content, start, total);
        var events = new List<(double At, int Order, Action Act)>();
        double h = 0;
        var cur = from;
        Waypoint? prev = null;
        foreach (var w in route)
        {
            double leg = LegHours(r, cur, w);
            // A road picked over the land crosses a stretch of country in several steps: it is met once, on the first.
            bool again = prev != null && prev.Way == w.Way && prev.Feature != null && prev.Feature == w.Feature;
            prev = w;
            if (!again && w.Way == "cross" && w.Feature is { } f)
                events.Add((h + leg / 2, 0, () =>
                {
                    v.Cross(f.Kind, f.Name, f.Tier);
                    if (v.Wiped) return;
                    foreach (var line in Passage.Taming(town, town.Rng, v.Party.Where(c => !c.Down).Select(c => c.Agent).ToList(), f.Kind.Id)) v.Log(line);
                }));
            else if (!again && w.Way == "ferry" && w.Feature is { } water)
                events.Add((h + leg / 2, 0, () => v.Ferry(water.Kind, water.Name)));
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
                int tier = TierAt(town, World.FromHeart(town, spot.X, spot.Y).Reach);
                var habitats = HabitatsAt(town, spot);
                // A night in killing weather, on its own dice; an airship's crew sleeps aboard.
                var exposed = party.Airship ? null : Weather.Exposure(town, spot.X, spot.Y, dusk);
                int night = (int)dusk;
                events.Add((at, 2, () =>
                {
                    if (exposed is { } e) v.Exposed(Weather.Rules(town).Road, e.Kind, Weather.Kind(town, e.W.Kind).Name.ToLowerInvariant(), tier, new Rng(unchecked((int)Voices.Hash("weather-camp", town.Seed, night, party.Id))));
                    if (!v.Wiped) v.Camp(tier, habitats);
                }));
            }
        }
        foreach (var (at, _, act) in events.OrderBy(e => e.At).ThenBy(e => e.Order))
        {
            if (v.Wiped) break;
            v.Clock = Finish(content, start, at);
            int fights = v.RoadFights;
            act();
            if (v.RoadFights > fights) troubled.Add(Along(town, from, route, at));
        }
        if (!v.Wiped) v.Clock = end;
        return troubled;
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
        v.TakeJobs(Wayfaring.Assign(town, members), Wayfaring.Bonuses(town, members), Wayfaring.Dice(town, party.Id + 7919));
        if (party.Edge != 0) v.Hearten(party.Edge);
        if (party.BountyId is int tb && town.Bounties[tb - 1].Terms != "slay") v.Terms = Terms.Of(town, town.Bounties[tb - 1]);
        v.Log($"{Expedition.ListNames(members.Select(m => m.GivenName).ToList())} set out for {site.Def.Name}.");
        v.Mark("setout");
        v.Speak("setout");
        v.TellJobs();
        foreach (var line in party.Said) v.Log(line);
        var troubled = Walk(town, v, party, party.Start, party.Out, party.DepartTick);
        Country.Wear(town, party.Start, party.Out);

        var outcome = ExpeditionOutcome.Wiped;
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
                v.Track(site.Def);
                // The place as they find it: its map, and what its occupants did since the last company came.
                v.Map = Dungeons.Map(town, site);
                var (found, more) = Dungeons.React(town, site, (int)Math.Floor(v.Clock));
                v.Swell += more;
                foreach (var line in found)
                {
                    v.Log(line);
                    v.Mark("react");
                }
                bool parley = Menaces.At(town, site.Def.Id) == null && site.Def.Id != content.Enemy.Outpost.Id && Captives.At(town, site.Def.Id) == null;
                v.Choose(site.Def, v.Map.Mapped || v.Tracked, parley);
                v.Duel = Showdowns.Due(town, party, site, v);
                if (!v.Wiped) outcome = v.Delve(site.Def, content.Roads.Retreat.Retries);
                if (v.AtDoor)
                {
                    // At the master's door: the trip waits on the showdown.
                    Showdowns.Begin(town, party, site, v, new Arrival(troubled, spot, members));
                    return;
                }
            }
        }
        Leave(town, party, v, outcome, new Arrival(troubled, spot, members));
    }

    /// <summary>What the road home needs to know of the road out.</summary>
    internal sealed record Arrival(List<(double X, double Y)> Troubled, (double X, double Y) Spot, List<Agent> Members);

    /// <summary>The delve is over: the road home, the homecoming, the result.</summary>
    internal static void Leave(Town town, Party party, Expedition.Venture v, ExpeditionOutcome outcome, Arrival arrival)
    {
        var content = town.Content;
        var site = Quests.Site(town, party.SiteId)!;
        var (troubled, spot, members) = arrival;
        double leave = v.Clock;
        party.Halts.AddRange(v.Halts);
        party.LeaveTick = (int)Math.Ceiling(leave);

        if (outcome != ExpeditionOutcome.Wiped)
        {
            // Home again, choosing the roads afresh: a hurt party takes no chances.
            bool hurt = v.HpFraction() < 0.6 || v.Fallen > 0;
            var said = new List<string>();
            // They keep clear of where they were set on, and go home by whatever way looks best now.
            var mind = Passage.Aboard(town, party, Mind(town, members, false, hurt)) with { Avoid = troubled };
            mind = mind with { Murk = Weather.Murk(town, spot.X, spot.Y, party.LeaveTick) };
            party.Home = Weathered(town, spot, Plan(town, spot, (0, 0), mind, town.Rng, said), party.LeaveTick, party.Airship, said);
            foreach (var line in said) v.Log(line);
            Walk(town, v, party, spot, party.Home, party.LeaveTick);
            Country.Wear(town, spot, party.Home);
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
        // Its airship lost, a company in the sky waits there for a rescue.
        if (outcome != ExpeditionOutcome.Wiped && Reaches.Wait(town, party, site)) Reaches.Strand(town, party, site);
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
        if (party.StrandedTick != null && t >= party.LeaveTick) return new Whereabouts(spot.X, spot.Y, Pace.Camped);
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
        if (party.RecalledTick is int rt) return WalkedOut(town, party, rt);
        var lines = new List<List<(double X, double Y)>>();
        var spot = World.Spot(town, Quests.Site(town, party.SiteId)!);
        Split(lines, [party.Start], party.Out.Count > 0 ? party.Out : [new Waypoint { X = spot.X, Y = spot.Y }]);
        if (party.Home.Count > 0 && party.GoneTick == null) Split(lines, [spot], party.Home);
        return lines;
    }

    /// <summary>
    /// What a party still out could have described by <paramref name="tick"/> (when it sent word home): the road out as
    /// far as it had got, and, once it had reached the site, the whole road out.
    /// </summary>
    public static List<List<(double X, double Y)>> WalkedBy(Town town, Party party, int tick)
    {
        if (tick < party.ArriveTick || party.RecalledTick != null) return WalkedOut(town, party, Math.Min(tick, party.RecalledTick ?? tick));
        var lines = new List<List<(double X, double Y)>>();
        var spot = World.Spot(town, Quests.Site(town, party.SiteId)!);
        Split(lines, [party.Start], party.Out.Count > 0 ? party.Out : [new Waypoint { X = spot.X, Y = spot.Y }]);
        return lines;
    }

    /// <summary>The road out as far as the party had marched by <paramref name="until"/>.</summary>
    private static List<List<(double X, double Y)>> WalkedOut(Town town, Party party, int until)
    {
        var lines = new List<List<(double X, double Y)>>();
        var outLine = new List<(double X, double Y)> { party.Start };
        double walked = Marched(town.Content, party.DepartTick, until);
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
            if (w.Way == "portal")
            {
                lines.Add(outLine);
                outLine = [];
            }
            outLine.Add((w.X, w.Y));
            h += leg;
            cur = (w.X, w.Y);
        }
        lines.Add(outLine);
        return lines;
    }

    /// <summary>A road as lines of spots, broken where it steps through an old gateway (nobody walks the miles between the arches).</summary>
    private static void Split(List<List<(double X, double Y)>> lines, List<(double X, double Y)> line, IReadOnlyList<Waypoint> route)
    {
        foreach (var w in route)
        {
            if (w.Way == "portal")
            {
                lines.Add(line);
                line = [];
            }
            line.Add((w.X, w.Y));
        }
        lines.Add(line);
    }

    /// <summary>A few words for the town's news on the way a party chose: ", the long way round the Black Mire".</summary>
    public static string Summary(Party party)
    {
        var skirted = party.Out.Where(w => w.Way == "skirt" && w.Feature != null).Select(w => w.Feature!).Distinct().ToList();
        var crossed = party.Out.Where(w => w.Way == "cross" && w.Feature != null).Select(w => w.Feature!).Distinct().ToList();
        var flown = party.Out.Where(w => w.Way == "fly" && w.Feature != null).Select(w => w.Feature!).Distinct().ToList();
        var gate = party.Out.FirstOrDefault(w => w.Way == "portal");
        var bits = new List<string>();
        if (gate != null) bits.Add($"the old way through {gate.Gate}");
        if (flown.Count > 0) bits.Add($"straight over {flown[0].Name}");
        if (crossed.Count > 0) bits.Add($"straight {crossed[0].Kind.Through} {crossed[0].Name}");
        if (skirted.Count > 0) bits.Add($"the long way round {skirted[0].Name}");
        return bits.Count == 0 ? "" : $", taking {string.Join(" and ", bits)}";
    }
}
