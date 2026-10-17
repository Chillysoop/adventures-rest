using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Where things sit on the map, in world tiles: the 16x16 town grid in the middle of a ring of
/// wilderness, the river, the forest, and the adventure sites. Decoration is hashed from fixed
/// numbers so it never touches the simulation's Rng.
/// </summary>
public sealed class Geo
{
    public const int Grid = Town.Grid;
    public const int Margin = 6;
    public const int World = Grid + Margin * 2;
    public static readonly Vector2 Center = new(World / 2f, World / 2f);

    public readonly List<Vector2> River = [];
    public readonly List<(Vector2 P, float R)> Trees = [];
    public readonly Dictionary<string, Vector2> Pins = [];
    public float TownRadius { get; private set; } = 3;

    public static float Hash(int n)
    {
        uint x = unchecked((uint)n * 0x45d9f3bu);
        x = unchecked(((x >> 16) ^ x) * 0x45d9f3bu);
        x = (x >> 16) ^ x;
        return x / 4294967296f;
    }

    public Geo(Town town)
    {
        const float seed = 1.3f;
        // A river runs down the eastern wilds and bends south, past the ford the town was founded on.
        for (int i = 0; i <= 40; i++)
        {
            float t = i / 40f;
            River.Add(new Vector2(World - 2.2f + Mathf.Sin(t * 5 + seed) * 0.6f - t * t * 3, -8 + t * (World + 16)));
        }
        for (int i = 0; i < 1600; i++)
        {
            float x = -8 + Hash(i * 3 + 1) * (World + 16);
            float y = -8 + Hash(i * 3 + 2) * (World + 16);
            bool inTown = x > Margin - 0.3f && x < Margin + Grid + 0.3f && y > Margin - 0.3f && y < Margin + Grid + 0.3f;
            if (inTown) continue;
            if (River.Any(p => p.DistanceTo(new Vector2(x, y)) < 0.9f)) continue;
            // Denser forest farther from the ford.
            float d = new Vector2(x, y).DistanceTo(Center);
            if (Hash(i * 3 + 3) > (d - 7) / 9) continue;
            Trees.Add((new Vector2(x, y), 0.28f + Hash(i * 7) * 0.22f));
        }
        Trees.Sort((a, b) => a.P.Y.CompareTo(b.P.Y));
        PlaceSites(town, 1);
    }

    public static Vector2 LotPos(Lot l) => new(Margin + l.X, Margin + l.Y);

    /// <summary>
    /// The wilds keep their distance from the frontier: sites sit just beyond the edge of the
    /// settled area, farther for longer journeys, and drift outward as the town grows.
    /// Returns true when a pin moved enough to matter for cached drawing.
    /// </summary>
    public bool PlaceSites(Town town, float ease)
    {
        float r = 3;
        foreach (var l in town.Lots)
            if (l.Zone != Zone.None) r = Mathf.Max(r, new Vector2(Margin + l.X + 0.5f, Margin + l.Y + 0.5f).DistanceTo(Center) + 0.7f);
        TownRadius += (r - TownRadius) * ease;
        int baseCount = town.Content.Sites.Count;
        bool moved = false;
        for (int i = 0; i < town.Sites.Count; i++)
        {
            var s = town.Sites[i];
            float a;
            float rr = TownRadius + 1.3f + s.Def.Distance * 1.1f;
            if (Sim.Systems.World.RegionOf(town, s) is { } region)
            {
                // Spread a region's sites across its arc of the compass (bearing 0 is north, clockwise).
                int k = region.SiteIds.IndexOf(s.Def.Id);
                int n = region.SiteIds.Count;
                float bearing = (float)(region.Bearing + (k - (n - 1) / 2f) * region.Arc * 0.6f / Math.Max(1, n));
                a = -Mathf.Pi / 2 + Mathf.DegToRad(bearing);
                rr += (k % 2) * 0.5f;
            }
            else
            {
                // Content sites keep fixed slots; sites raised later (the war camp) sit between them.
                float slot = i < baseCount ? i : i - baseCount + 0.5f;
                a = -Mathf.Pi / 2 + slot / baseCount * Mathf.Tau + 0.35f;
            }
            var p = Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rr;
            if (!Pins.TryGetValue(s.Def.Id, out var old) || old.DistanceTo(p) > 0.05f) moved = true;
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
