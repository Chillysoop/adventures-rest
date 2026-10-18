using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Where things sit on the map, in world tiles (one tile is one town lot): a 1024-tile square of
/// wilderness with the town grid (48 lots a side, in rings of wards) at its heart, a river past the ford the town was founded
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

    /// <summary>
    /// The wilds keep their distance from the frontier: sites sit beyond the edge of the settled
    /// area, a day's march out per day of travel, and drift outward as the town grows.
    /// Returns true when a pin moved enough to matter for cached drawing.
    /// </summary>
    public bool PlaceSites(Town town, float ease)
    {
        float r = 3;
        foreach (var l in town.Lots) r = Mathf.Max(r, LotPos(l).DistanceTo(Center) + 0.9f);
        TownRadius += (r - TownRadius) * ease;
        int baseCount = town.Content.Sites.Count;
        bool moved = false;
        for (int i = 0; i < town.Sites.Count; i++)
        {
            var s = town.Sites[i];
            float a;
            float rr = TownRadius + 10 + s.Def.Distance * TilesPerDay * (0.82f + Hash(i * 7 + 3) * 0.3f);
            if (Sim.Systems.World.RegionOf(town, s) is { } region)
            {
                // Spread a region's sites across its arc of the compass (bearing 0 is north, clockwise).
                int k = region.SiteIds.IndexOf(s.Def.Id);
                int n = region.SiteIds.Count;
                float bearing = (float)(region.Bearing + (k - (n - 1) / 2f) * region.Arc * 0.6f / Math.Max(1, n));
                a = -Mathf.Pi / 2 + Mathf.DegToRad(bearing);
                rr += (k % 2) * 6;
            }
            else
            {
                // Content sites keep fixed slots; sites raised later (the war camp) sit between them.
                float slot = i < baseCount ? i : i - baseCount + 0.5f;
                a = -Mathf.Pi / 2 + slot / baseCount * Mathf.Tau + 0.35f;
            }
            var p = Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!Pins.TryGetValue(s.Def.Id, out var old) || old.DistanceTo(p) > 0.25f) moved = true;
            Pins[s.Def.Id] = p;
        }
        return moved;
    }

    /// <summary>Where a party is between town (0) and its site (1), counting fractional hours.</summary>
    public static float PartyLeg(Town town, Party p, float hours)
    {
        var site = Quests.Site(town, p.SiteId)!;
        float e = town.Tick + hours - p.DepartTick;
        float outbound = site.Def.Distance * SimTime.HoursPerDay;
        if (p.RecalledTick is int rt)
        {
            float turned = rt - p.DepartTick;
            float peak = turned / outbound;
            if (e < turned) return e / outbound;
            float back = Math.Max(1, p.ReturnTick - rt);
            return Mathf.Clamp(peak * (1 - (e - turned) / back), 0, 1);
        }
        if (e < outbound) return e / outbound;
        if (e < outbound + SimTime.HoursPerDay) return 1;
        return Mathf.Clamp(1 - (e - outbound - SimTime.HoursPerDay) / outbound, 0, 1);
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
