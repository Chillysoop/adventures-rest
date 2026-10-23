using System.Runtime.CompilerServices;

namespace CrownAndCompass.Sim.Systems;

/// <summary>Ground nobody crosses on foot, and what lets a party over it anyway (content/game/passage.json).</summary>
[Flags]
public enum Ways
{
    None = 0,

    /// <summary>The river, anywhere along it: swimming horses, a ring of swimming.</summary>
    River = 1,

    /// <summary>Open water: lakes, and the river with them (walking on it, flying over it).</summary>
    Water = 2,

    /// <summary>Sheer heights: climbing them like a spider, or flying.</summary>
    Sheer = 4,

    /// <summary>The open sky: only wings, a flying spell or an airship reach the isles that float above the wilds.</summary>
    Air = 8,
}

/// <summary>
/// A barrier in town (walls, the moat): a closed loop in tiles from the town's corner that feet cross only at its openings.
/// <see cref="Edge"/> is its outer edge (the moat's far bank), <see cref="Outer"/> the line a walker going round it keeps to;
/// all three loops share their corners' bearings, so their edges are numbered alike.
/// </summary>
public sealed record Barrier(string Kind, (double X, double Y)[] Loop, (double X, double Y)[] Edge, (double X, double Y)[] Outer, IReadOnlyList<Opening> Openings, double Area)
{
    /// <summary>How far past its line, outward, the barrier reaches (the wall's foot, or the far bank of its moat), in tiles.</summary>
    public double Reach { get; init; }

    /// <summary>How far inside its line the wall's foot stands, in tiles.</summary>
    public double Foot { get; init; }

    /// <summary>Clearance past <see cref="Edge"/> a walker keeps going round it (<see cref="Outer"/>).</summary>
    public double Round { get; init; }
}

/// <summary>A way through a barrier: a gate, a bridge over the moat. <see cref="In"/> lies inside the loop, <see cref="Out"/> clear of it outside.</summary>
public sealed record Opening(string Kind, (double X, double Y) In, (double X, double Y) Out);

/// <summary>A place to cross the river in the wilds, in day units from the town.</summary>
public sealed record Crossing(string Kind, string Name, double X, double Y);

public sealed record CrossSpellDef
{
    public required string Spell { get; init; }
    public IReadOnlyList<string> Ways { get; init; } = [];

    /// <summary>Spell slot that carries the whole party across (more targets when cast higher); 0 when it never can.</summary>
    public int PartySlot { get; init; }
}

public sealed record CrossItemDef
{
    public required string Item { get; init; }
    public IReadOnlyList<string> Ways { get; init; } = [];

    /// <summary>Whether it carries the whole party (a flying carpet), not just whoever wears it.</summary>
    public bool Party { get; init; }
}

public sealed record MountDef
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public IReadOnlyList<string> Ways { get; init; } = [];

    /// <summary>What it costs, and how much gold a hero holds before they spend that on it.</summary>
    public double Price { get; init; }
    public double BuyAt { get; init; }
    public int Level { get; init; }

    /// <summary>An institution that must stand for it to be sold (the Griffon Roost), or "".</summary>
    public string Institution { get; init; } = "";

    /// <summary>A spell that calls it for free (a paladin's steed), or "".</summary>
    public string Spell { get; init; } = "";
    public IReadOnlyList<string> Text { get; init; } = [];

    /// <summary>Classes it is sold to (an owl for those who work by night), or every class when empty.</summary>
    public IReadOnlyList<string> Classes { get; init; } = [];

    /// <summary>
    /// A beast that chooses its rider and is never sold (a giant eagle): the daily chance one comes to the town, and the
    /// renown a hero needs before it will look at them twice. It takes the most renowned, never one more feared than famed.
    /// </summary>
    public double Chooses { get; init; }
    public double Renown { get; init; }

    /// <summary>A wild beast only won by taming it out where it lives (<see cref="TameDef"/>), never bought.</summary>
    public TameDef? Tame { get; init; }

    /// <summary>Kinds of hard country it carries a rider through at an easy pace (a snow bear on the ice), and how much of the slowness it takes off.</summary>
    public IReadOnlyList<string> Eases { get; init; } = [];
    public double Ease { get; init; }

    /// <summary>Hit points it adds to its rider's side in a fight, as a beast that fights beside them.</summary>
    public int Fights { get; init; }
}

/// <summary>How a wild mount is won: met crossing its country, and calmed with an Animal Handling check or a spell.</summary>
public sealed record TameDef
{
    public IReadOnlyList<string> Terrain { get; init; } = [];

    /// <summary>Chance a crossing meets one, the check's DC, and the spells that win one over without a roll.</summary>
    public double Meet { get; init; } = 0.35;
    public int Dc { get; init; } = 15;
    public IReadOnlyList<string> Spells { get; init; } = [];
    public IReadOnlyList<string> Won { get; init; } = [];
    public IReadOnlyList<string> Lost { get; init; } = [];
}

public sealed record RiverRules
{
    public string Name { get; init; } = "the river";

    /// <summary>Days east of the hall it runs past the town, and how far up and down it stays straight before it wanders.</summary>
    public double Offset { get; init; } = 0.15;
    public double Straight { get; init; } = 0.25;
    public double Ease { get; init; } = 5.5;

    /// <summary>Its meanders: amplitude (days) and frequency (per day) of each wave.</summary>
    public double[][] Waves { get; init; } = [[1.05, 0.48], [0.27, 1.45]];

    /// <summary>How far up and down from the town it is traced, in days.</summary>
    public double Length { get; init; } = 12;

    /// <summary>Days a site's spot keeps from the water: the river bends round it.</summary>
    public double SiteClear { get; init; } = 0.12;

    /// <summary>Days between crossings along it, and the share that are fords (the rest are bridges).</summary>
    public double[] Spacing { get; init; } = [1.2, 2.2];
    public double Fords { get; init; } = 0.6;
    public string TownCrossing { get; init; } = "the town bridge";
    public IReadOnlyList<string> FordNames { get; init; } = [];
    public IReadOnlyList<string> BridgeNames { get; init; } = [];
    public IReadOnlyList<string> Words { get; init; } = [];
}

/// <summary>The town's walls and moat, how wide they stand and where their openings lie, in tiles.</summary>
public sealed record WallPassRules
{
    /// <summary>Half the width of the wall's foot, and the reach of the moat past the wall line.</summary>
    public double Wall { get; init; } = 0.35;
    public double Moat { get; init; } = 1.6;

    /// <summary>How far in and out of the gate a walker steps before turning for where they are going.</summary>
    public double GateIn { get; init; } = 0.8;
    public double GateOut { get; init; } = 0.8;

    /// <summary>Clearance a walker keeps when going round the outside of a wall.</summary>
    public double Round { get; init; } = 0.4;
}

/// <summary>When fear lets townsfolk through what they never cross otherwise: swim the moat, scramble over the wall.</summary>
public sealed record FleeRules
{
    /// <summary>Hours the alarm lasts after raiders strike, and how near the attack (tiles) is certain death.</summary>
    public int Hours { get; init; } = 3;
    public double Radius { get; init; } = 6;
}

/// <summary>The sky dock's airship: what it carries a company over, and how the telling goes.</summary>
public sealed record AirshipRules
{
    public string Name { get; init; } = "the town's airship";
    public IReadOnlyList<string> Ways { get; init; } = [];
    public IReadOnlyList<string> Text { get; init; } = [];
}

public sealed record PassageRules
{
    public WallPassRules Walls { get; init; } = new();
    public FleeRules Flee { get; init; } = new();
    public RiverRules River { get; init; } = new();
    public CliffRules Cliffs { get; init; } = new();
    public IReadOnlyList<CrossSpellDef> Spells { get; init; } = [];
    public IReadOnlyList<CrossItemDef> Items { get; init; } = [];
    public IReadOnlyList<MountDef> Mounts { get; init; } = [];
    public AirshipRules Airship { get; init; } = new();

    /// <summary>Share of a menace kind's host that flies or swims and pays the moat no mind.</summary>
    public IReadOnlyDictionary<string, double> Fliers { get; init; } = new Dictionary<string, double>();
    public IReadOnlyDictionary<string, List<string>> Texts { get; init; } = new Dictionary<string, List<string>>();

    public MountDef? Mount(string id) => Mounts.FirstOrDefault(m => m.Id == id);
}

/// <summary>
/// Where feet can go. Walls and the moat close the town but for their gates and bridges; in the wilds the river is crossed
/// at fords and bridges, and lakes and sheer peaks are gone round. Only flying, water-walking or climbing magic, mounts that
/// swim or fly, and creatures born to it go straight over. Every walker, lane and road asks here; nothing else decides it.
/// Townsfolk break the rule only to escape certain death (<see cref="Fleeing"/>), never to save themselves a walk.
/// </summary>
public static class Passage
{
    public static PassageRules Rules(Town town) => town.Content.Passage;

    // ---- Distances: the town's tiles and the wilds' days ----

    /// <summary>Tiles of map per day's march, and the town's own reach, over which a road eases into days (the map's scale).</summary>
    public const double TilesPerDay = 44;
    public const double Reach0 = Town.Grid * 0.75 + 7;

    /// <summary>Tiles from the hall for a spot this many days out, eased in near the town.</summary>
    public static double DayRadius(double days) => days * TilesPerDay + Reach0 * Math.Min(1, days / 0.3);

    /// <summary>Days out for a spot this many tiles from the hall: the inverse of <see cref="DayRadius"/>.</summary>
    public static double DaysOut(double tiles)
    {
        double ease = DayRadius(0.3);
        return tiles >= ease ? 0.3 + (tiles - ease) / TilesPerDay : tiles / (TilesPerDay + Reach0 / 0.3);
    }

    /// <summary>A point of the town (tiles from its corner) in the wilds' day units from the hall.</summary>
    public static (double X, double Y) ToDays(double x, double y)
    {
        double dx = x - Town.Center, dy = y - Town.Center, r = Math.Sqrt(dx * dx + dy * dy);
        if (r < 1e-9) return (0, 0);
        double d = DaysOut(r);
        return (dx / r * d, dy / r * d);
    }

    // ---- The town: walls, the moat and their gates ----

    private sealed class Cache
    {
        public string Key = "";
        public List<Barrier> Barriers = [];
        public List<Barrier>? Obstacles;
        public string? ObstaclesKey;
        public bool[]? Mask;
        public List<(double X, double Y)>? River;
        public List<Crossing>? Crossings;
    }

    private static readonly ConditionalWeakTable<Town, Cache> Caches = new();

    private static Cache Store(Town town) => Caches.GetValue(town, _ => new Cache());

    private static string Key(Town town)
    {
        var d = town.Defenses;
        return $"{d.Palisade}|{d.StoneWall}|{d.CityWall}|{d.Moat}|{d.Gatehouse}|{town.City.WallVersion}";
    }

    /// <summary>
    /// The barriers standing in town, innermost first: the palisade or stone walls round the old town (with the moat, once dug)
    /// and the city wall. A wall half built is no barrier yet: its gaps are open ground. Every one has an opening.
    /// </summary>
    public static IReadOnlyList<Barrier> Barriers(Town town)
    {
        var c = Store(town);
        string key = Key(town);
        if (c.Key == key) return c.Barriers;
        c.Key = key;
        c.Mask = null;
        c.Barriers = [];
        var d = town.Defenses;
        var w = Rules(town).Walls;
        bool stone = d.StoneWall == PalisadeState.Built;
        if (stone || d.Palisade == PalisadeState.Built)
            c.Barriers.Add(Ring(town, 0, stone ? "stone" : "palisade", stone && d.Moat == PalisadeState.Built ? w.Moat : w.Wall));
        if (d.CityWall == PalisadeState.Built) c.Barriers.Add(Ring(town, 1, "city", w.Wall));
        return c.Barriers;
    }

    /// <summary>Everything a walker must go round or through by its openings: the town's barriers, then the cliffs (<see cref="Cliffs"/>).</summary>
    public static IReadOnlyList<Barrier> Obstacles(Town town)
    {
        var walls = Barriers(town);
        var cliffs = Cliffs.Barriers(town);
        if (cliffs.Count == 0) return walls;
        var c = Store(town);
        if (c.ObstaclesKey != c.Key || c.Obstacles == null)
        {
            c.Obstacles = [.. walls, .. cliffs];
            c.ObstaclesKey = c.Key;
        }
        return c.Obstacles;
    }

    /// <summary>A wall round a ring, along the line its stones follow (<see cref="City.Wall"/>), its gate on the south road.</summary>
    private static Barrier Ring(Town town, int ring, string kind, double reach)
    {
        const int N = 96;
        var w = Rules(town).Walls;
        var loop = new (double X, double Y)[N];
        double area = 0;
        for (int i = 0; i < N; i++)
        {
            // South (+y) first, then round, as the map draws it: the gate stands at the loop's start.
            double a = Math.PI / 2 + i * Math.PI * 2 / N, r = City.Wall(town, ring, a);
            double cx = Math.Cos(a), cy = Math.Sin(a);
            loop[i] = (Town.Center + cx * r, Town.Center + cy * r);
        }
        var edge = Offset(loop, reach);
        var outer = Offset(loop, reach + w.Round);
        for (int i = 0; i < N; i++)
        {
            var (ax, ay) = loop[i];
            var (bx, by) = loop[(i + 1) % N];
            area += ax * by - bx * ay;
        }
        // Its gates, where the player put them on the line (the main gate first, which the gatehouse stands over).
        var heads = WallLines.Gates(town, ring);
        if (heads.Count == 0) heads = [Math.PI / 2];
        var openings = new List<Opening>();
        for (int g = 0; g < heads.Count; g++)
        {
            var (gx, gy) = WallLines.Point(town, ring, heads[g]);
            double ox = gx - Town.Center, oy = gy - Town.Center, len = Math.Sqrt(ox * ox + oy * oy);
            (ox, oy) = (ox / len, oy / len);
            string gate = kind == "palisade" ? "gate" : g == 0 && town.Defenses.Gatehouse == PalisadeState.Built && ring == 0 ? "gatehouse" : "gate";
            openings.Add(new Opening(reach > w.Wall ? "bridge" : gate, (gx - ox * w.GateIn, gy - oy * w.GateIn), (gx + ox * (reach + w.GateOut), gy + oy * (reach + w.GateOut))));
        }
        return new Barrier(kind, loop, edge, outer, openings, Math.Abs(area) / 2) { Reach = reach, Foot = w.Wall, Round = w.Round };
    }

    /// <summary>A closed loop moved out by <paramref name="d"/> tiles, every edge parallel to its own (mitred corners).</summary>
    public static (double X, double Y)[] Offset((double X, double Y)[] loop, double d)
    {
        int n = loop.Length;
        double area = 0;
        for (int i = 0; i < n; i++) area += loop[i].X * loop[(i + 1) % n].Y - loop[(i + 1) % n].X * loop[i].Y;
        double sign = area > 0 ? 1 : -1;
        (double X, double Y) Normal(int i)
        {
            var a = loop[(i + n) % n];
            var b = loop[(i + 1 + n) % n];
            double ex = b.X - a.X, ey = b.Y - a.Y, l = Math.Sqrt(ex * ex + ey * ey);
            // Outward: to the right of an anticlockwise loop's direction (y down flips it, hence the sign).
            return (ey / l * sign, -ex / l * sign);
        }
        var outp = new (double X, double Y)[n];
        for (int i = 0; i < n; i++)
        {
            var n1 = Normal(i - 1);
            var n2 = Normal(i);
            double k = d / Math.Max(0.3, 1 + n1.X * n2.X + n1.Y * n2.Y);
            outp[i] = (loop[i].X + (n1.X + n2.X) * k, loop[i].Y + (n1.Y + n2.Y) * k);
        }
        return outp;
    }

    /// <summary>A target on a wall or in its moat (a doorstep facing it), moved just clear of it, on its own side.</summary>
    public static (double X, double Y) Clear(Town town, (double X, double Y) p)
    {
        foreach (var b in Obstacles(town))
        {
            if (!OnBarrier(b, p) || b.Openings.Any(o => SegDist(p, o.In, o.Out) < 0.35)) continue;
            bool inside = Inside(b.Loop, p.X, p.Y);
            p = Nearest(Offset(b.Loop, inside ? -(b.Foot + 0.05) : b.Reach + 0.05), p);
        }
        return p;
    }

    /// <summary>The nearest point on a closed loop's line.</summary>
    private static (double X, double Y) Nearest((double X, double Y)[] ring, (double X, double Y) p)
    {
        (double X, double Y) best = p;
        double bestD = double.MaxValue;
        for (int i = 0; i < ring.Length; i++)
        {
            var a = ring[i];
            var e = ring[(i + 1) % ring.Length];
            double dx = e.X - a.X, dy = e.Y - a.Y, l2 = dx * dx + dy * dy;
            double t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
            var q = (a.X + dx * t, a.Y + dy * t);
            double dd = Dist(p, q);
            if (dd < bestD)
            {
                bestD = dd;
                best = q;
            }
        }
        return best;
    }

    /// <summary>Whether a point lies inside a closed loop (even-odd).</summary>
    public static bool Inside((double X, double Y)[] loop, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = loop.Length - 1; i < loop.Length; j = i++)
        {
            var (xi, yi) = loop[i];
            var (xj, yj) = loop[j];
            if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi) inside = !inside;
        }
        return inside;
    }

    private static double SegDist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, l2 = dx * dx + dy * dy;
        double t = l2 < 1e-12 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / l2, 0, 1);
        double ex = a.X + dx * t - p.X, ey = a.Y + dy * t - p.Y;
        return Math.Sqrt(ex * ex + ey * ey);
    }

    private static double Dist((double X, double Y) a, (double X, double Y) b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

    /// <summary>Distance from a point to a closed loop's line.</summary>
    private static double LoopDist((double X, double Y)[] loop, (double X, double Y) p)
    {
        double best = double.MaxValue;
        for (int i = 0; i < loop.Length; i++) best = Math.Min(best, SegDist(p, loop[i], loop[(i + 1) % loop.Length]));
        return best;
    }

    /// <summary>Whether a point stands on a barrier itself: on the wall, or in the moat before it.</summary>
    public static bool OnBarrier(Barrier b, (double X, double Y) p)
    {
        double d = LoopDist(b.Loop, p);
        return Inside(b.Loop, p.X, p.Y) ? d < b.Foot : d < b.Reach;
    }

    /// <summary>Where segment a-b first crosses a closed loop: the share of the way along it, and the edge, or null.</summary>
    private static (double T, int Edge)? Hit((double X, double Y)[] loop, (double X, double Y) a, (double X, double Y) b, bool last = false)
    {
        (double T, int Edge)? best = null;
        double rx = b.X - a.X, ry = b.Y - a.Y;
        for (int i = 0; i < loop.Length; i++)
        {
            var p = loop[i];
            var q = loop[(i + 1) % loop.Length];
            double sx = q.X - p.X, sy = q.Y - p.Y, den = rx * sy - ry * sx;
            if (Math.Abs(den) < 1e-12) continue;
            double t = ((p.X - a.X) * sy - (p.Y - a.Y) * sx) / den, u = ((p.X - a.X) * ry - (p.Y - a.Y) * rx) / den;
            if (t < 1e-9 || t > 1 - 1e-9 || u < 0 || u > 1) continue;
            if (best == null || (last ? t > best.Value.T : t < best.Value.T)) best = (t, i);
        }
        return best;
    }

    /// <summary>
    /// Where someone walking from one point of the town to another heads next, in tiles: the gate (and over the moat's bridge)
    /// when a wall lies between, round the outside of a wall that stands in the way, else straight there. Only someone fleeing
    /// certain death (<paramref name="desperate"/>) goes straight through: into the moat and over the wall.
    /// </summary>
    public static (double X, double Y) Next(Town town, (double X, double Y) from, (double X, double Y) to, bool desperate = false)
    {
        var all = Obstacles(town);
        if (desperate || all.Count == 0) return to;
        to = Clear(town, to);
        // A doorstep on the bridge or in the gateway itself: reach that opening's end on this side, then walk along it.
        foreach (var b in all)
            foreach (var o in b.Openings)
            {
                if (!OnBarrier(b, to) || SegDist(to, o.In, o.Out) >= 0.35) continue;
                if (SegDist(from, o.In, o.Out) < 0.35) return to;
                var end = Inside(b.Loop, from.X, from.Y) ? o.In : o.Out;
                return Dist(from, end) < 0.05 ? to : Next(town, from, end);
            }
        Barrier? cross = null;
        bool outward = false;
        foreach (var b in all)
        {
            bool f = Inside(b.Loop, from.X, from.Y), t = Inside(b.Loop, to.X, to.Y);
            // Out of the innermost wall first; into the outermost first.
            if (f && !t && (cross == null || !outward || b.Area < cross.Area))
            {
                cross = b;
                outward = true;
            }
            else if (!f && t && !outward && (cross == null || b.Area > cross.Area)) cross = b;
        }
        // In the gateway or on the bridge, short of either end: on through it, to the end on the far side.
        foreach (var b in all)
        {
            bool on = OnBarrier(b, from);
            if (!on && !(Inside(b.Outer, from.X, from.Y) && !Inside(b.Loop, from.X, from.Y))) continue;
            // Off the bridge already, with a doorstep beside it in plain sight across the verge: straight there, not back to the end.
            if (!on && !Inside(b.Loop, to.X, to.Y) && Hit(b.Edge, from, to) == null) continue;
            foreach (var o in b.Openings)
            {
                if (SegDist(from, o.In, o.Out) > 0.35) continue;
                var end = Inside(b.Loop, to.X, to.Y) ? o.In : o.Out;
                if (Dist(from, end) > 0.05) return end;
            }
        }
        if (cross != null)
        {
            Opening? best = null;
            double bestLen = double.MaxValue;
            foreach (var o in cross.Openings)
            {
                var (near, far) = outward ? (o.In, o.Out) : (o.Out, o.In);
                double len = Dist(from, near) + Dist(far, to);
                if (len < bestLen)
                {
                    bestLen = len;
                    best = o;
                }
            }
            var (n, f) = outward ? (best!.In, best.Out) : (best!.Out, best.In);
            // At the near end, or already walking the gateway: on to the far end.
            if (Dist(from, n) < 0.1 || SegDist(from, n, f) < 0.25) return f;
            // To the gate round any wall that stands between, not across the moat.
            return Skirt(all, from, n);
        }
        return Skirt(all, from, to);
    }

    /// <summary>
    /// The way to a point on the same side of every wall: straight, or round the outside of a wall that stands across it.
    /// A wall inside another that both ends lie outside is gone round with it.
    /// </summary>
    private static (double X, double Y) Skirt(IReadOnlyList<Barrier> all, (double X, double Y) from, (double X, double Y) to)
    {
        foreach (var b in all.OrderByDescending(b => b.Area))
        {
            if (Inside(b.Loop, from.X, from.Y) || Inside(b.Loop, to.X, to.Y)) continue;
            if (all.Any(a => a.Area > b.Area && Inside(a.Loop, b.Loop[0].X, b.Loop[0].Y) && !Inside(a.Loop, from.X, from.Y) && !Inside(a.Loop, to.X, to.Y))) continue;
            if (Detour(b, from, to) is { } round) return round;
        }
        return to;
    }

    /// <summary>The next corner on the way round the outside of a barrier, when the straight way from outside it to a point outside it crosses it.</summary>
    private static (double X, double Y)? Detour(Barrier b, (double X, double Y) from, (double X, double Y) to)
    {
        if (Hit(b.Edge, from, to) is not { } first) return null;
        var lastHit = Hit(b.Edge, from, to, true)!.Value;
        return Round(b, from, first.Edge, lastHit.Edge);
    }

    /// <summary>The farthest corner of a loop (walking round it the shorter way from where the line meets it) still in plain sight.</summary>
    private static (double X, double Y) Round(Barrier b, (double X, double Y) from, int enter, int leave)
    {
        var loop = b.Outer;
        // Too close under the wall to see round it: out to the line walkers keep first.
        if (Inside(loop, from.X, from.Y)) return Nearest(Offset(b.Loop, b.Reach + b.Round + 0.05), from);
        int n = loop.Length;
        int fwd = ((leave - enter) % n + n) % n, back = n - fwd;
        int dir = fwd <= back ? 1 : -1, steps = Math.Max(1, Math.Min(fwd, back));
        int start = dir > 0 ? (enter + 1) % n : enter;
        var pick = loop[start];
        for (int k = 0; k <= steps; k++)
        {
            var v = loop[((start + dir * k) % n + n) % n];
            if (Hit(b.Edge, from, v) is { } h && h.T < 0.999) break;
            pick = v;
        }
        // Pushed a hair outward, so the next look from there sees past the corner.
        // Away from the loop's middle: the town's for its walls, the plateau's own for a cliff.
        double mx = Town.Center, my = Town.Center;
        if (b.Kind == "cliff") (mx, my) = (b.Loop.Average(v => v.X), b.Loop.Average(v => v.Y));
        double dx = pick.X - mx, dy = pick.Y - my, len = Math.Sqrt(dx * dx + dy * dy);
        return len < 1e-9 ? pick : (pick.X + dx / len * 0.05, pick.Y + dy / len * 0.05);
    }

    /// <summary>
    /// Cells of the town plan a lane can't cross (<see cref="Streets"/>): the foot of a standing wall and the moat before it,
    /// but for its gates and bridges.
    /// </summary>
    public static bool[]? Mask(Town town)
    {
        var all = Barriers(town);
        if (all.Count == 0) return null;
        var c = Store(town);
        if (c.Mask != null) return c.Mask;
        int n = Land.Size;
        var mask = new bool[n * n];
        foreach (var b in all)
        {
            double reach = b.Reach + 0.25, wall = Rules(town).Walls.Wall + 0.25;
            for (int i = 0; i < b.Loop.Length; i++)
            {
                var a = b.Loop[i];
                var e = b.Loop[(i + 1) % b.Loop.Length];
                int x0 = Math.Max(0, (int)((Math.Min(a.X, e.X) - reach) * Land.Res)), x1 = Math.Min(n - 1, (int)((Math.Max(a.X, e.X) + reach) * Land.Res));
                int y0 = Math.Max(0, (int)((Math.Min(a.Y, e.Y) - reach) * Land.Res)), y1 = Math.Min(n - 1, (int)((Math.Max(a.Y, e.Y) + reach) * Land.Res));
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        var p = ((x + 0.5) / Land.Res, (y + 0.5) / Land.Res);
                        double d = SegDist(p, a, e);
                        if (d < (Inside(b.Loop, p.Item1, p.Item2) ? wall : reach)) mask[y * n + x] = true;
                    }
            }
            foreach (var o in b.Openings)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                        if (mask[y * n + x] && SegDist(((x + 0.5) / Land.Res, (y + 0.5) / Land.Res), o.In, o.Out) < 0.6) mask[y * n + x] = false;
        }
        return c.Mask = mask;
    }

    /// <summary>
    /// Every closed-off part of the town keeps a way out to the wider map: each barrier has an opening that leads from inside
    /// its loop to outside it. The names of any that don't (a wall with no gate) are returned.
    /// </summary>
    public static List<string> Sealed(Town town) =>
        Barriers(town).Where(b => !b.Openings.Any(o => Inside(b.Loop, o.In.X, o.In.Y) && !Inside(b.Loop, o.Out.X, o.Out.Y) && !OnBarrier(b, o.Out)))
            .Select(b => b.Kind).ToList();

    // ---- Fleeing ----

    /// <summary>Raiders strike a building: for a few hours, whoever is near it and outside the walls runs for their life.</summary>
    public static void Alarm(Town town, Building b)
    {
        town.Defenses.AlarmBuilding = b.Id;
        town.Defenses.AlarmUntil = town.Tick + Rules(town).Flee.Hours;
    }

    /// <summary>
    /// Whether someone faces certain death: raiders at a building within reach of where they are, and no wall between them and
    /// it. They run for the hall the straight way, through the moat and over the wall if they must.
    /// </summary>
    public static bool Fleeing(Town town, Agent a)
    {
        var d = town.Defenses;
        if (d.AlarmBuilding is not int id || town.Tick >= d.AlarmUntil || a.Dead || a.Departed || a.IsAway) return false;
        if (town.Buildings.FirstOrDefault(b => b.Id == id) is not { } struck) return false;
        int? at = a.LocationId ?? a.HomeId;
        if (at is not int here) return false;
        var mine = town.Lot(town.Building(here).LotId);
        var hit = town.Lot(struck.LotId);
        if (Js.Hypot(mine.X - hit.X, mine.Y - hit.Y) > Rules(town).Flee.Radius) return false;
        // Behind the same wall as the raiders: nothing stands between, nothing to swim.
        return Barriers(town).Any(b => !Inside(b.Loop, mine.X, mine.Y));
    }

    // ---- The wilds: the river and its crossings ----

    private static double Hash(int seed, int i)
    {
        uint h = (uint)seed * 2246822519u ^ (uint)(i + 7) * 3266489917u;
        h ^= h >> 15;
        h *= 2654435761u;
        h ^= h >> 13;
        return (h & 0xffffff) / (double)0x1000000;
    }

    private static double Smooth(double a, double b, double x)
    {
        double t = Math.Clamp((x - a) / (b - a), 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>
    /// The river's course in day units, north to south: straight past the town's east side at the ford it was founded on,
    /// wandering further out, and bending round every site so none stands in the water. Fixed by the seed.
    /// </summary>
    public static IReadOnlyList<(double X, double Y)> River(Town town)
    {
        var c = Store(town);
        if (c.River != null) return c.River;
        var r = Rules(town).River;
        double phase = Hash(town.Seed, 1) * Math.PI * 2;
        var spots = town.Sites.Where(s => s.Def.Distance > 0).Select(s => World.Spot(town, s)).ToList();
        var line = new List<(double X, double Y)>();
        for (double y = -r.Length; y <= r.Length + 1e-9; y += 0.04)
        {
            double far = Smooth(r.Straight, r.Straight + r.Ease, Math.Abs(y)), wave = 0;
            for (int k = 0; k < r.Waves.Length; k++) wave += r.Waves[k][0] * Math.Sin(y * r.Waves[k][1] + phase * (k + 1));
            double x = r.Offset + far * wave;
            foreach (var (sx, sy) in spots)
            {
                double dy = sy - y;
                if (Math.Abs(dy) > r.SiteClear * 3 || Math.Abs(sx - x) > r.SiteClear * 1.5) continue;
                // Bend away from the site, the side the water already lies on.
                double push = r.SiteClear * 1.5 - Math.Abs(sx - x);
                x += (x >= sx ? 1 : -1) * push * Math.Exp(-dy * dy / (r.SiteClear * r.SiteClear));
            }
            line.Add((x, y));
        }
        return c.River = line;
    }

    /// <summary>The fords and bridges on the river: the town's own, and others every day or two up and down it.</summary>
    public static IReadOnlyList<Crossing> Crossings(Town town)
    {
        var c = Store(town);
        if (c.Crossings != null) return c.Crossings;
        var r = Rules(town).River;
        var line = River(town);
        var list = new List<Crossing>();
        (double X, double Y) At(double y)
        {
            int i = Math.Clamp((int)Math.Round((y + r.Length) / 0.04), 0, line.Count - 1);
            return line[i];
        }
        var home = At(0);
        list.Add(new Crossing("bridge", r.TownCrossing, home.X, home.Y));
        int k = 0;
        foreach (int dir in new[] { -1, 1 })
            for (double y = 0; ;)
            {
                k++;
                y += dir * (r.Spacing[0] + Hash(town.Seed, 100 + k) * (r.Spacing[^1] - r.Spacing[0]));
                // Never in the middle of a lake or a range of peaks the river runs through: on down the water past it.
                while (Math.Abs(y) <= r.Length - 0.5 && town.Terrain.Any(f => f.Kind.Barrier != "" && Roads.Contains(f, At(y).X, At(y).Y, town.Content.Roads.Margin * 1.5))) y += dir * 0.04;
                if (Math.Abs(y) > r.Length - 0.5) break;
                var p = At(y);
                bool ford = Hash(town.Seed, 200 + k) < r.Fords;
                var names = ford ? r.FordNames : r.BridgeNames;
                // No two crossings share a name while there are words left to tell them apart.
                string name = ford ? "a ford" : "a bridge";
                int w0 = (int)(Hash(town.Seed, 300 + k) * Math.Max(1, r.Words.Count)), n0 = (int)(Hash(town.Seed, 400 + k) * Math.Max(1, names.Count));
                for (int j = 0; j < r.Words.Count && names.Count > 0; j++)
                {
                    name = names[n0 % names.Count].Replace("{A}", r.Words[(w0 + j) % r.Words.Count], StringComparison.Ordinal);
                    if (!list.Any(x => x.Name == name)) break;
                }
                list.Add(new Crossing(ford ? "ford" : "bridge", name, p.X, p.Y));
            }
        return c.Crossings = list;
    }

    /// <summary>Where a straight way from a to b first meets the river, as a share of the way, or null when it never does.</summary>
    public static double? MeetsRiver(Town town, (double X, double Y) a, (double X, double Y) b)
    {
        var line = River(town);
        double? best = null;
        double minY = Math.Min(a.Y, b.Y) - 0.05, maxY = Math.Max(a.Y, b.Y) + 0.05;
        double rx = b.X - a.X, ry = b.Y - a.Y;
        for (int i = 0; i + 1 < line.Count; i++)
        {
            var p = line[i];
            var q = line[i + 1];
            if (q.Y < minY || p.Y > maxY) continue;
            double sx = q.X - p.X, sy = q.Y - p.Y, den = rx * sy - ry * sx;
            if (Math.Abs(den) < 1e-12) continue;
            double t = ((p.X - a.X) * sy - (p.Y - a.Y) * sx) / den, u = ((p.X - a.X) * ry - (p.Y - a.Y) * rx) / den;
            if (t < 1e-6 || t > 1 - 1e-6 || u < 0 || u > 1) continue;
            if (best == null || t < best) best = t;
        }
        return best;
    }

    /// <summary>
    /// A road with the river crossed where it can be: wherever a stretch meets the water it turns for the best ford or bridge,
    /// one reached without wading the river first. Words for the ones they use go into <paramref name="said"/>.
    /// </summary>
    public static List<Waypoint> Ford(Town town, (double X, double Y) from, List<Waypoint> route, List<string>? said = null)
    {
        var crossings = Crossings(town);
        var outp = new List<Waypoint>();
        var cur = from;
        foreach (var w in route)
        {
            var to = (w.X, w.Y);
            for (int guard = 0; guard < 6 && MeetsRiver(town, cur, to) != null; guard++)
            {
                // A ford in spate after rain is passed over for a bridge, if one can be reached (Weather.Flooded).
                Crossing? best = null, spate = null;
                double bestLen = double.MaxValue, spateLen = double.MaxValue;
                foreach (var c in crossings)
                {
                    var at = (c.X, c.Y);
                    if (Dist(cur, at) < 1e-6 || MeetsRiver(town, cur, at) is double m && m < 0.999) continue;
                    double len = Dist(cur, at) + Dist(at, to);
                    if (c.Kind == "ford" && Weather.Flooded(town, c.X, c.Y))
                    {
                        if (len < spateLen)
                        {
                            spateLen = len;
                            spate = c;
                        }
                        continue;
                    }
                    if (len < bestLen)
                    {
                        bestLen = len;
                        best = c;
                    }
                }
                if (best == null && spate != null)
                {
                    best = spate;
                    bestLen = spateLen;
                    spate = null;
                }
                if (best == null) break;
                outp.Add(new Waypoint { X = best.X, Y = best.Y, Way = best.Kind, Crossing = best.Name });
                if (said != null && spate != null && spateLen < bestLen && Weather.Rules(town).Road.Line(new Rng(unchecked((int)Voices.Hash("spate", town.Seed, town.Day, (int)Math.Round(spate.X * 100)))), "flood") is { Length: > 0 } flood)
                    said.Add(Saga.Cap(flood.Replace("{name}", spate.Name, StringComparison.Ordinal).Replace("{bridge}", best.Name, StringComparison.Ordinal)));
                else if (said != null && Line(town, best.Kind == "ford" ? "ford" : "bridge") is { } line)
                    said.Add(line.Replace("{name}", best.Name, StringComparison.Ordinal).Replace("{river}", Rules(town).River.Name, StringComparison.Ordinal));
                cur = (best.X, best.Y);
            }
            outp.Add(w);
            cur = to;
        }
        return outp;
    }

    /// <summary>The way from one spot of the wilds to another for someone who can't swim the river or climb: by its crossings, round the cliffs.</summary>
    public static List<(double X, double Y)> Road(Town town, (double X, double Y) from, (double X, double Y) to)
    {
        var way = new List<(double X, double Y)> { from };
        foreach (var w in Ford(town, from, [new Waypoint { X = to.X, Y = to.Y }]))
        {
            way.AddRange(RoundCliffs(town, way[^1], (w.X, w.Y)));
            way.Add((w.X, w.Y));
        }
        return way;
    }

    /// <summary>A spot in day units in tiles from the town's corner: the inverse of <see cref="ToDays"/>.</summary>
    public static (double X, double Y) FromDays(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y);
        if (r < 1e-9) return (Town.Center, Town.Center);
        double t = DayRadius(r);
        return (Town.Center + x / r * t, Town.Center + y / r * t);
    }

    /// <summary>
    /// The corners, in day units, of the way round any plateau (<see cref="Cliffs"/>) standing across the straight way from a to b,
    /// for those who can't climb; empty when nothing is in the way. A road goes round a plateau, not up and over it.
    /// </summary>
    public static List<(double X, double Y)> RoundCliffs(Town town, (double X, double Y) a, (double X, double Y) b)
    {
        var corners = new List<(double X, double Y)>();
        var cliffs = Cliffs.Barriers(town);
        if (cliffs.Count == 0) return corners;
        var cur = FromDays(a.X, a.Y);
        var end = FromDays(b.X, b.Y);
        for (int guard = 0; guard < 24; guard++)
        {
            var next = Skirt(cliffs, cur, end);
            if (Dist(next, end) < 1e-9 || Dist(next, cur) < 1e-9) break;
            corners.Add(ToDays(next.X, next.Y));
            cur = next;
        }
        return corners;
    }

    private static string? Line(Town town, string key) =>
        Rules(town).Texts.TryGetValue(key, out var lines) && lines.Count > 0 ? lines[(int)(Hash(town.Seed, town.Tick) * lines.Count) % lines.Count] : null;

    /// <summary>The town's gate on the road out, in day units: where a party leaves the walls and comes home through them, or null with no walls.</summary>
    public static (double X, double Y)? Gate(Town town)
    {
        var all = Barriers(town);
        if (all.Count == 0) return null;
        var o = all.OrderByDescending(b => b.Area).First().Openings[0];
        return ToDays(o.Out.X, o.Out.Y);
    }

    // ---- Who can cross ----

    public static Ways Parse(IEnumerable<string> ways)
    {
        var w = Ways.None;
        foreach (var s in ways)
            w |= s switch
            {
                "river" => Ways.River,
                "water" => Ways.Water | Ways.River,
                "sheer" => Ways.Sheer,
                "air" => Ways.Air,
                _ => Ways.None,
            };
        return w;
    }

    /// <summary>The lowest hero level with a spell slot of this level, for a class that casts this way.</summary>
    public static int SlotLevel(string magic, int slot) => magic switch
    {
        "full" => slot <= 9 ? slot * 2 - 1 : 99,
        "pact" => slot <= 5 ? slot * 2 - 1 : 99,
        "half" => slot <= 5 ? slot * 4 - 3 : 99,
        _ => 99,
    };

    /// <summary>What a hero can cross alone, and what they can carry the whole party over.</summary>
    public static (Ways Self, Ways Party) Of(Town town, Agent a)
    {
        var h = a.Hero;
        if (h == null) return (Ways.None, Ways.None);
        var r = Rules(town);
        Ways self = Ways.None, party = Ways.None;
        if (h.Mount != "" && r.Mount(h.Mount) is { } m) self |= Parse(m.Ways);
        foreach (var it in h.Magic.Values)
            foreach (var d in r.Items)
                if (d.Item == it.Id)
                {
                    self |= Parse(d.Ways);
                    if (d.Party) party |= Parse(d.Ways);
                }
        if (h.Cls.Magic != "")
            foreach (var s in r.Spells)
            {
                if (town.Content.Library.Spell(s.Spell) is not { } spell || !spell.Classes.Contains(h.Cls.Id)) continue;
                if (h.Level >= SlotLevel(h.Cls.Magic, Math.Max(1, spell.Level))) self |= Parse(s.Ways);
                if (s.PartySlot > 0 && h.Level >= SlotLevel(h.Cls.Magic, s.PartySlot)) party |= Parse(s.Ways);
            }
        return (self, party);
    }

    /// <summary>What a party can cross together: what any one of them can carry everyone over, and what every one can cross alone.</summary>
    public static Ways Abilities(Town town, IReadOnlyList<Agent> members)
    {
        if (members.Count == 0) return Ways.None;
        Ways all = Ways.River | Ways.Water | Ways.Sheer | Ways.Air, carried = Ways.None;
        foreach (var m in members)
        {
            var (self, party) = Of(town, m);
            all &= self;
            carried |= party;
        }
        return all | carried;
    }

    /// <summary>Who carries a party over what nobody walks, for the telling: "Mira's carpet of flying", "Oswin's spells", "their griffons".</summary>
    public static string Carrier(Town town, IReadOnlyList<Agent> members)
    {
        var r = Rules(town);
        foreach (var m in members)
        {
            var h = m.Hero;
            if (h == null) continue;
            foreach (var it in h.Magic.Values)
                if (r.Items.Any(d => d.Item == it.Id && d.Party)) return $"{m.GivenName}'s {it.Name.ToLowerInvariant()}";
            if (Of(town, m).Party != Ways.None) return $"{m.GivenName}'s spells";
        }
        var mounts = members.Select(a => a.Hero?.Mount ?? "").Where(id => id != "").Distinct().ToList();
        if (mounts.Count == 1 && r.Mount(mounts[0]) is { } mount && members.All(a => a.Hero?.Mount == mount.Id)) return $"their {Library.Plural(mount.Name)}";
        return "their mounts and magic";
    }

    /// <summary>What a hero can cross that nobody walks, in words ("rivers anywhere, lakes, sheer heights"), or "" for nothing.</summary>
    public static string Describe(Ways w)
    {
        var bits = new List<string>();
        if ((w & Ways.Water) != 0) bits.Add("rivers and lakes");
        else if ((w & Ways.River) != 0) bits.Add("rivers anywhere, not just at fords");
        if ((w & Ways.Sheer) != 0) bits.Add("sheer heights");
        if ((w & Ways.Air) != 0) bits.Add("the open sky");
        return string.Join(", ", bits);
    }

    /// <summary>A kind of hard country's barrier as the ways that cross it, or none for country anyone can walk.</summary>
    public static Ways Needs(TerrainKindDef kind) => kind.Barrier switch
    {
        "water" => Ways.Water,
        "sheer" => Ways.Sheer,
        "air" => Ways.Air,
        _ => Ways.None,
    };

    // ---- The sky dock ----

    /// <summary>Airships at the sky dock that aren't already out with a company or on a trade run (<see cref="Skyship"/>).</summary>
    public static int AirshipsFree(Town town) => Institutions.Airships(town) - town.Parties.Count(p => p.Airship && !p.Returned) - (Skyship.Away(town) ? 1 : 0);

    /// <summary>What a straight march from a to b meets that nobody walks: the river, a lake, sheer heights.</summary>
    public static Ways Meets(Town town, (double X, double Y) a, (double X, double Y) b)
    {
        var w = MeetsRiver(town, a, b) != null ? Ways.River : Ways.None;
        foreach (var f in town.Terrain)
        {
            var needs = Needs(f.Kind);
            if (needs == Ways.None || (w & needs) != 0) continue;
            for (int i = 0; i <= 40; i++)
                if (Roads.Contains(f, a.X + (b.X - a.X) * i / 40, a.Y + (b.Y - a.Y) * i / 40))
                {
                    w |= needs;
                    break;
                }
        }
        return w;
    }

    /// <summary>
    /// A company setting out whose road meets water or heights it can't cross on its own takes a free airship from the
    /// sky dock, out and home again; the mind it plans with then crosses what the ship does.
    /// </summary>
    public static RouteMind Board(Town town, Party party, RouteMind mind, (double X, double Y) from, (double X, double Y) to, bool aloft = false)
    {
        var r = Rules(town).Airship;
        var ship = Parse(r.Ways);
        var meets = Meets(town, from, to) | (aloft ? Ways.Air : Ways.None);
        if (ship == Ways.None || (meets & ~mind.Ways) == Ways.None || AirshipsFree(town) <= 0) return mind;
        party.Airship = true;
        if (r.Text.Count > 0)
        {
            string line = r.Text[town.Rng.Range(0, r.Text.Count)].Replace("{party}", party.Name, StringComparison.Ordinal).Replace("{ship}", r.Name, StringComparison.Ordinal);
            town.Log(LedgerCategory.Quest, Importance.Routine, line);
        }
        return Aboard(town, party, mind);
    }

    /// <summary>A floating isle needs every member to fly (a mount, a spell or an item), or a free airship to carry them.</summary>
    public static bool CanReach(Town town, IReadOnlyList<Agent> members, SiteState site) =>
        Voyages.CanReach(town, members, site) && Reaches.CanReach(town, members, site);

    /// <summary>The mind of a company with an airship under it: it crosses what the ship does.</summary>
    public static RouteMind Aboard(Town town, Party party, RouteMind mind)
    {
        var r = Rules(town).Airship;
        // The town's own airship that carried it up brings it home (Reaches).
        if (party.ShipId is int id && town.Ships.FirstOrDefault(s => s.Id == id) is { } ship && Fleet.Aloft(town, ship))
            return mind with { Ways = mind.Ways | Parse(r.Ways), Carrier = ship.Name };
        if (!party.Airship) return mind;
        return mind with { Ways = mind.Ways | Parse(r.Ways), Carrier = r.Name };
    }

    /// <summary>Share of a menace's host that flies or swims over the moat as if it weren't there.</summary>
    public static double Fliers(Town town, string menaceKind) => Rules(town).Fliers.TryGetValue(menaceKind, out var f) ? f : 0;

    // ---- Mounts ----

    /// <summary>
    /// Once a day: heroes in town get mounts. A paladin calls a steed when they can cast for one; a hero rich enough buys a
    /// riding horse, and a seasoned one a griffon (or an owl, for those who work by night) where the Griffon Roost sells them.
    /// Now and then a giant eagle comes to the roost and chooses the most renowned hero. A hero keeps the best they have had.
    /// </summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.Mounts.Count == 0) return;
        foreach (var a in town.Heroes.Where(h => !h.IsAway && !h.Dead && !h.Departed).OrderBy(h => h.Id).ToList())
        {
            var h = a.Hero!;
            var have = h.Mount == "" ? null : r.Mount(h.Mount);
            double haveRank = have == null ? -1 : Rank(have);
            foreach (var m in r.Mounts.OrderByDescending(Rank).ThenBy(m => m.Id, StringComparer.Ordinal))
            {
                if (Rank(m) <= haveRank) break;
                if (h.Level < m.Level || m.Chooses > 0 || m.Tame != null) continue;
                if (m.Classes.Count > 0 && !m.Classes.Contains(h.Cls.Id)) continue;
                if (m.Institution != "" && (Institutions.Find(town, m.Institution) is not { } inst || inst.Shut)) continue;
                bool called = false;
                if (m.Spell != "")
                {
                    if (h.Cls.Magic == "" || town.Content.Library.Spell(m.Spell) is not { } spell || !spell.Classes.Contains(h.Cls.Id)
                        || h.Level < SlotLevel(h.Cls.Magic, Math.Max(1, spell.Level))) continue;
                    called = true;
                }
                else if (m.BuyAt <= 0 || a.Gold < m.BuyAt) continue;
                if (!called)
                {
                    a.Gold -= m.Price;
                    if (m.Institution != "" && Institutions.Find(town, m.Institution) is { BuildingId: int bid }) town.Building(bid).Cash += m.Price;
                }
                Ride(town, a, m, called || m.Ways.Contains("sheer") ? Importance.Notable : Importance.Routine);
                break;
            }
        }
        foreach (var m in r.Mounts.Where(m => m.Chooses > 0).OrderBy(m => m.Id, StringComparer.Ordinal)) Choose(town, m);
    }

    /// <summary>A hero takes a mount: theirs from now on, told in the Ledger (and on a card when it is one worth telling).</summary>
    public static void Ride(Town town, Agent a, MountDef m, Importance importance, string? line = null)
    {
        a.Hero!.Mount = m.Id;
        line ??= m.Text.Count > 0 ? m.Text[(int)(Hash(town.Seed, a.Id * 31 + town.Day) * m.Text.Count) % m.Text.Count] : "";
        if (line == "") return;
        line = line.Replace("{hero}", a.Name, StringComparison.Ordinal).Replace("{given}", a.GivenName, StringComparison.Ordinal);
        town.Log(LedgerCategory.Quest, importance, line, [a.Id]);
        if (importance >= Importance.Notable && m.Ways.Contains("air")) Moments.Mounted(town, a, m, line);
    }

    /// <summary>
    /// A beast that picks its own rider (a giant eagle) comes to the roost on its day and looks the heroes over: it takes
    /// the most renowned one it would carry, and none at all if nobody in town is famous enough or all are more feared.
    /// </summary>
    private static void Choose(Town town, MountDef m)
    {
        if (m.Institution != "" && (Institutions.Find(town, m.Institution) is not { } inst || inst.Shut)) return;
        if (Hash(town.Seed, town.Day * 7919 + m.Id.Length * 104729) >= m.Chooses) return;
        var rank = Rank(m);
        var pick = town.Heroes
            .Where(a => !a.IsAway && !a.Dead && !a.Departed && a.Hero!.Level >= m.Level && a.Hero.Renown >= m.Renown && a.Hero.Infamy < a.Hero.Renown)
            .Where(a => a.Hero!.Mount == "" || Rules(town).Mount(a.Hero.Mount) is not { } had || Rank(had) < rank)
            .OrderByDescending(a => a.Hero!.Renown)
            .ThenBy(a => a.Id)
            .FirstOrDefault();
        if (pick != null) Ride(town, pick, m, Importance.Major);
    }

    /// <summary>
    /// A party crossing a wild mount's country may meet one (a snow bear on the ice): whoever has none as good tries to win
    /// it over, with a spell that charms beasts or an Animal Handling check. Rolls on the venture's dice. Returns the telling.
    /// </summary>
    public static List<string> Taming(Town town, Rng rng, IReadOnlyList<Agent> party, string terrain)
    {
        var said = new List<string>();
        var lib = town.Content.Library;
        foreach (var m in Rules(town).Mounts.Where(m => m.Tame != null && m.Tame.Terrain.Contains(terrain)).OrderBy(m => m.Id, StringComparer.Ordinal))
        {
            var t = m.Tame!;
            if (!rng.Chance(t.Meet)) continue;
            double rank = Rank(m);
            foreach (var a in party.OrderBy(a => a.Id))
            {
                var h = a.Hero;
                if (h == null || a.Dead || (h.Mount != "" && Rules(town).Mount(h.Mount) is { } had && Rank(had) >= rank)) continue;
                string? spell = h.Cls.Magic == "" ? null : t.Spells.FirstOrDefault(id => lib.Spell(id) is { } sp && sp.Classes.Contains(h.Cls.Id) && h.Level >= SlotLevel(h.Cls.Magic, Math.Max(1, sp.Level)));
                int roll = rng.D20();
                bool won = spell != null || roll + Skills.Bonus(town, a, "animal-handling") >= t.Dc;
                var lines = won ? t.Won : t.Lost;
                if (lines.Count > 0)
                    said.Add(rng.Pick(lines).Replace("{hero}", a.GivenName, StringComparison.Ordinal).Replace("{beast}", m.Name, StringComparison.Ordinal)
                        .Replace("{how}", spell != null ? $"with {Library.Prose(lib.Spell(spell)!.Name)}" : $"with a slow hand and a steady voice (Animal Handling {roll + Skills.Bonus(town, a, "animal-handling")})", StringComparison.Ordinal));
                if (won)
                {
                    h.Mount = m.Id;
                    break;
                }
            }
        }
        return said;
    }

    /// <summary>How much of a hard country's slowness a party's mounts take off: the least any one of them gets, so none is left behind.</summary>
    public static double Eased(Town town, IReadOnlyList<Agent> members, string terrain)
    {
        if (members.Count == 0) return 0;
        double least = 1;
        foreach (var a in members)
        {
            var m = a.Hero?.Mount is { Length: > 0 } id ? Rules(town).Mount(id) : null;
            least = Math.Min(least, m != null && m.Eases.Contains(terrain) ? m.Ease : 0);
        }
        return least;
    }

    /// <summary>Hit points a hero's mount brings to a fight beside them.</summary>
    public static int Fights(Town town, Agent a) => a.Hero?.Mount is { Length: > 0 } id && Rules(town).Mount(id) is { } m ? m.Fights : 0;

    /// <summary>How much a mount opens up: a flier above a swimmer above nothing.</summary>
    public static double Rank(MountDef m) => (m.Ways.Contains("sheer") ? 4 : 0) + (m.Ways.Contains("water") ? 2 : 0) + (m.Ways.Contains("river") ? 1 : 0)
        + (m.Spell != "" || m.Chooses > 0 ? 0.5 : 0) + (m.Eases.Count > 0 ? 0.25 : 0);
}
