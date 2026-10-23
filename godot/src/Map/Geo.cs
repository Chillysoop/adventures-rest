using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Where things sit on the map, in world tiles (one tile is one town lot): a 2048-tile square of
/// wilderness with the town grid (96 lots a side, in rings of wards) at its heart, a river past the ford the town was founded
/// on, and the adventure sites a few days' march out. Decoration is hashed from fixed numbers so
/// it never touches the simulation's Rng.
/// </summary>
public sealed class Geo
{
    public const int Grid = Town.Grid;
    public const int World = 2048;
    public const int Margin = (World - Grid) / 2;
    public static readonly Vector2 Center = new(World / 2f, World / 2f);

    /// <summary>Tiles a party covers in a day on the road; sites sit this far out per day of travel.</summary>
    public const float TilesPerDay = (float)Passage.TilesPerDay;

    /// <summary>World tiles from the town's heart to where the wilds begin: a site or road point zero days out sits here.</summary>
    public const float Reach0 = (float)Passage.Reach0;

    /// <summary>Everything within this radius of the town stays clear of forest and flat enough to build on: the whole grid, every ward.</summary>
    public const float Clearing = Grid * 0.75f;

    public readonly Dictionary<string, Vector2> Pins = [];
    public float TownRadius { get; private set; } = 3;

    public static float Hash(int n)
    {
        uint x = unchecked((uint)n * 0x45d9f3bu);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3bu);
        x = (x >> 16) ^ x;
        return x / 4294967296f;
    }

    public Geo(Town town) => PlaceSites(town, 1);

    /// <summary>The centre of a lot on the map.</summary>
    public static Vector2 LotPos(Lot l) => new(Margin + (float)l.X, Margin + (float)l.Y);

    /// <summary>A lot's facing on the map: the yaw (about +y) that turns a model's front (+z) the way the building faces.</summary>
    public static float LotYaw(Lot l) => -(float)l.Yaw;

    /// <summary>The way a lot's front faces, on the map.</summary>
    public static Vector2 LotFront(Lot l) => new(-Mathf.Sin((float)l.Yaw), Mathf.Cos((float)l.Yaw));

    /// <summary>A lot's four corners on the map (front two last), grown by <paramref name="grow"/> tiles on every side.</summary>
    public static Vector2[] LotCorners(Lot l, float grow = 0)
    {
        var c = LotPos(l);
        var f = LotFront(l);
        var u = new Vector2(f.Y, -f.X) * (l.W / (float)Sim.Systems.Land.Res / 2 + grow);
        var v = f * (l.D / (float)Sim.Systems.Land.Res / 2 + grow);
        return [c - u - v, c + u - v, c + u + v, c - u + v];
    }

    /// <summary>World tiles from the town's heart for a spot this many days out (eased in near the town, so a party walks out of it smoothly).</summary>
    public static float DayRadius(double days) => (float)(days * TilesPerDay + Reach0 * Math.Min(1, days / 0.3));

    /// <summary>A spot in the sim's day units (x east, y south of the town) on the map.</summary>
    public static Vector2 FromDays(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y);
        if (r < 1e-6) return Center;
        var p = Center + new Vector2((float)(x / r), (float)(y / r)) * DayRadius(r);
        // A town founded far from the world's heart has the farthest country past the map's edge on its far side: drawn at the edge.
        var inside = p.Clamp(new Vector2(Edge, Edge), new Vector2(World - Edge, World - Edge));
        return inside == p ? p : Center + (p - Center) * Mathf.Min(Mathf.Abs((World / 2f - Edge) / Mathf.Max(1e-3f, Mathf.Abs(p.X - Center.X))), Mathf.Abs((World / 2f - Edge) / Mathf.Max(1e-3f, Mathf.Abs(p.Y - Center.Y))));
    }

    /// <summary>Tiles kept between anything drawn and the map's edge.</summary>
    private const float Edge = 24;

    public static Vector2 FromDays((double X, double Y) p) => FromDays(p.X, p.Y);

    /// <summary>A spot on the map in the sim's day units: the inverse of <see cref="FromDays(double, double)"/>.</summary>
    public static (double X, double Y) ToDays(Vector2 w)
    {
        var d = w - Center;
        float r = d.Length();
        if (r < 1e-4f) return (0, 0);
        // DayRadius eases in over the first 0.3 days, then grows a day's march per TilesPerDay.
        float ease = DayRadius(0.3);
        double days = r >= ease ? 0.3 + (r - ease) / TilesPerDay : r / (TilesPerDay + Reach0 / 0.3f);
        return (d.X / r * days, d.Y / r * days);
    }

    /// <summary>
    /// The town's settled radius (for its lamplight), and every site's pin where the sim puts it: on its
    /// region's bearing, a day's march out per day of travel. Returns true when a pin moved enough to
    /// matter for cached drawing.
    /// </summary>
    public bool PlaceSites(Town town, float ease)
    {
        float r = 3;
        foreach (var l in town.Lots) r = Mathf.Max(r, LotPos(l).DistanceTo(Center) + 0.9f);
        TownRadius += (r - TownRadius) * ease;
        bool moved = false;
        foreach (var s in town.Sites)
        {
            // The undercity lies under the town: no pin out on the map.
            if (s.Def.Distance == 0) continue;
            var p = FromDays(Sim.Systems.World.Spot(town, s));
            if (!Pins.TryGetValue(s.Def.Id, out var old) || old.DistanceTo(p) > 0.25f) moved = true;
            Pins[s.Def.Id] = p;
        }
        return moved;
    }

    /// <summary>Where a party is at <paramref name="hours"/> past the current tick, and what it is doing; null once it is lost.</summary>
    public static (Vector2 Pos, Pace Pace)? PartyAt(Town town, Party p, float hours)
    {
        if (Roads.Where(town, p, town.Tick + hours) is not { } w) return null;
        return (FromDays(w.X, w.Y), w.Pace);
    }

    /// <summary>Where an exploring company is at <paramref name="hours"/> past the current tick, and whether it is on the move.</summary>
    public static (Vector2 Pos, bool Moving) SurveyAt(Town town, Survey s, float hours)
    {
        var (x, y) = Explore.Where(town, s, town.Tick + hours);
        return (FromDays(x, y), Explore.Marching(town, s, town.Tick + hours));
    }

    /// <summary>
    /// Where the town thinks a site it has only heard of lies: somewhere near the truth, further off the
    /// less the teller could be trusted. Once someone has seen it the pin is exact.
    /// </summary>
    public Vector2 Rough(SiteState s)
    {
        var pin = Pins[s.Def.Id];
        if (s.Found || s.Known == SiteKnowledge.Confirmed) return pin;
        int h = 0;
        foreach (char c in s.Def.Id) h = h * 31 + c;
        float off = TilesPerDay * 0.45f * Mathf.Clamp((100 - s.Trust) / 100f, 0.25f, 1);
        float a = Hash(h) * Mathf.Tau;
        return pin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * off * (0.5f + Hash(h + 7) * 0.5f);
    }

    /// <summary>A settlement's spot on the map.</summary>
    public static Vector2 SettlementPin(Town town, Settlement st)
    {
        var (x, y) = Explore.SettlementSpot(town, st);
        return FromDays(x, y);
    }

    /// <summary>0 by day, 1 at deep night, easing through dusk and dawn.</summary>
    public static float Nightness(Town town, float hours)
    {
        float h = (town.Tick % SimTime.HoursPerDay) + hours + 0.5f;
        if (h >= 24) h -= 24;
        if (h >= 7 && h <= 19) return 0;
        if (h > 19 && h < 22) return (h - 19) / 3;
        if (h >= 22 || h < 4) return 1;
        return 1 - (h - 4) / 3;
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Town, Dictionary<int, Vector2[]>> ScoutWays = new();

    /// <summary>The way a scout rides out to its site, round the hard country like any party (sketched once, on its own dice).</summary>
    public static Vector2[] ScoutWay(Town town, Scout s, Vector2 pin)
    {
        var ways = ScoutWays.GetOrCreateValue(town);
        if (!ways.TryGetValue(s.Id, out var line))
            ways[s.Id] = line = Country.Sketch(town, (0, 0), ToDays(pin), town.Seed * 7919 + s.Id).Select(FromDays).ToArray();
        return line;
    }

    /// <summary>The point a share of the way along a line, by length.</summary>
    public static Vector2 Along(Vector2[] line, float t)
    {
        if (line.Length == 0) return Center;
        float total = 0;
        for (int i = 1; i < line.Length; i++) total += line[i - 1].DistanceTo(line[i]);
        float want = Mathf.Clamp(t, 0, 1) * total;
        for (int i = 1; i < line.Length; i++)
        {
            float d = line[i - 1].DistanceTo(line[i]);
            if (want <= d && d > 0) return line[i - 1].Lerp(line[i], want / d);
            want -= d;
        }
        return line[^1];
    }

    /// <summary>The first share of a line, by length.</summary>
    public static Vector2[] Part(Vector2[] line, float t)
    {
        if (t >= 1 || line.Length < 2) return line;
        var end = Along(line, t);
        float total = 0;
        for (int i = 1; i < line.Length; i++) total += line[i - 1].DistanceTo(line[i]);
        float want = Mathf.Clamp(t, 0, 1) * total;
        var o = new List<Vector2> { line[0] };
        for (int i = 1; i < line.Length; i++)
        {
            float d = line[i - 1].DistanceTo(line[i]);
            if (want <= d) break;
            want -= d;
            o.Add(line[i]);
        }
        o.Add(end);
        return o.ToArray();
    }
}
