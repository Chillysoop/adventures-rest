using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Where things sit on the map, in world tiles (one tile is one town lot): a 1024-tile square of
/// wilderness with the 16x16 town grid at its heart, a river past the ford the town was founded
/// on, and the adventure sites a few days' march out. Decoration is hashed from fixed numbers so
/// it never touches the simulation's Rng.
/// </summary>
public sealed class Geo
{
    public const int Grid = Town.Grid;
    public const int World = 1024;
    public const int Margin = (World - Grid) / 2;
    public static readonly Vector2 Center = new(World / 2f, World / 2f);

    /// <summary>Tiles a party covers in a day on the road; sites sit this far out per day of travel.</summary>
    public const float TilesPerDay = 44;

    /// <summary>World tiles from the town's heart to where the wilds begin: a site or road point zero days out sits here.</summary>
    public const float Reach0 = 22;

    /// <summary>Everything within this radius of the town stays clear of forest and flat enough to build on.</summary>
    public const float Clearing = 15;

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

    public static Vector2 LotPos(Lot l) => new(Margin + l.X, Margin + l.Y);

    /// <summary>World tiles from the town's heart for a spot this many days out (eased in near the town, so a party walks out of it smoothly).</summary>
    public static float DayRadius(double days) => (float)(days * TilesPerDay + Reach0 * Math.Min(1, days / 0.3));

    /// <summary>A spot in the sim's day units (x east, y south of the town) on the map.</summary>
    public static Vector2 FromDays(double x, double y)
    {
        double r = Math.Sqrt(x * x + y * y);
        if (r < 1e-6) return Center;
        return Center + new Vector2((float)(x / r), (float)(y / r)) * DayRadius(r);
    }

    public static Vector2 FromDays((double X, double Y) p) => FromDays(p.X, p.Y);

    /// <summary>
    /// The town's settled radius (for its lamplight), and every site's pin where the sim puts it: on its
    /// region's bearing, a day's march out per day of travel. Returns true when a pin moved enough to
    /// matter for cached drawing.
    /// </summary>
    public bool PlaceSites(Town town, float ease)
    {
        float r = 3;
        foreach (var l in town.Lots)
            if (l.Zone != Zone.None) r = Mathf.Max(r, new Vector2(Margin + l.X + 0.5f, Margin + l.Y + 0.5f).DistanceTo(Center) + 0.7f);
        TownRadius += (r - TownRadius) * ease;
        bool moved = false;
        foreach (var s in town.Sites)
        {
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
}
