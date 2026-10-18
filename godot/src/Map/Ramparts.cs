using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The wall the watch walks, as drawn: its line, the height of its walk, the stretch of it in each of the sim's
/// sections, its towers and its gate. The sim only knows sections by bearing (<see cref="Garrison"/>); this is
/// where they land on whatever line the wall follows.
/// </summary>
public sealed record WallWalk(Vector2[] Loop, float Height, Vector2[][] Sections, Vector2[] Towers, float TowerTop, Vector2 Gate, bool Stone);

public partial class TownView
{
    /// <summary>The outer wall as the watch walks it, or null with no finished wall.</summary>
    public WallWalk? Walk { get; private set; }

    /// <summary>Where a ray from the town's heart along a bearing last crosses a closed loop (the loop's farthest point that way).</summary>
    public static Vector2 RayHit(Vector2[] loop, Vector2 dir)
    {
        var o = Geo.Center;
        float best = -1;
        for (int i = 0; i < loop.Length; i++)
        {
            var a = loop[i] - o;
            var b = loop[(i + 1) % loop.Length] - o;
            var e = b - a;
            float den = dir.Cross(e);
            if (Mathf.Abs(den) < 1e-6f) continue;
            float t = a.Cross(e) / den, u = a.Cross(dir) / den;
            if (t > 0 && u >= 0 && u <= 1) best = Mathf.Max(best, t);
        }
        return best > 0 ? o + dir * best : o + dir * 4;
    }

    /// <summary>A section's bearing on the map, as the sim reckons it.</summary>
    public static Vector2 Bearing(Town town, int section)
    {
        var (x, y) = Garrison.Bearing(town, section);
        return new Vector2((float)x, (float)y);
    }

    /// <summary>Which section a point of the map lies in.</summary>
    public static int SectionOf(Town town, Vector2 p) => Garrison.SectionAt(town, p.X - Geo.Margin, p.Y - Geo.Margin);

    /// <summary>
    /// The outer wall the watch stands on (the city wall, the stone walls, or the palisade) cut into the sim's sections,
    /// and the parts the wall works add to it: towers, the gate, a timber walk behind the stakes, the moat and the wall barracks.
    /// </summary>
    private void Ramparts(MeshKit k, Town town, Vector2[]? palisade)
    {
        var d = town.Defenses;
        Vector2[]? loop = null;
        float height = 0, towerTop = 0;
        bool stone = d.StoneWall == PalisadeState.Built;
        if (d.CityWall == PalisadeState.Built)
        {
            loop = RingLine(town, 1);
            height = 1.0f;
            towerTop = 3.03f * 0.5f;
        }
        else if (stone)
        {
            loop = RingLine(town, 0);
            height = 0.76f;
            towerTop = 3.03f * 0.42f;
        }
        else if (d.Palisade == PalisadeState.Built && palisade != null)
        {
            loop = palisade;
            height = 0.2f;
            towerTop = 0.86f;
        }
        if (loop == null)
        {
            Walk = null;
            return;
        }

        // Each section's stretch of the wall, walked in order, finely enough that a guard pacing it follows the curve.
        int n = Garrison.Sections(town);
        var arcs = Enumerable.Range(0, n).Select(_ => new List<Vector2>()).ToArray();
        float perim = Perimeter(loop);
        int steps = Math.Max(n * 4, (int)(perim / 0.2f));
        // Start halfway into section 0 (the gate) so no stretch wraps round the loop's start.
        var gate = RayHit(loop, Bearing(town, 0));
        float start = 0, bestD = float.MaxValue;
        for (int i = 0; i < steps; i++)
        {
            float dd = Along(loop, perim * i / steps).DistanceSquaredTo(gate);
            if (dd < bestD)
            {
                bestD = dd;
                start = perim * i / steps;
            }
        }
        // Sampling starts at the gate, so section 0 comes first and last: put its last stretch first, to walk it through the gate.
        var lead = new List<Vector2>();
        bool leading = true;
        for (int i = 0; i < steps; i++)
        {
            var p = Along(loop, (start + perim * i / steps) % perim);
            int s = SectionOf(town, p);
            if (s != 0) leading = false;
            if (s == 0 && leading) lead.Add(p);
            else arcs[s].Add(p);
        }
        arcs[0].AddRange(lead);

        var towers = new Vector2[d.Towers];
        for (int i = 0; i < d.Towers; i++) towers[i] = RayHit(loop, Bearing(town, Garrison.TowerSection(town, i)));
        Walk = new WallWalk(loop, height, [.. arcs.Select(a => a.Count > 0 ? a.ToArray() : [RayHit(loop, Bearing(town, Array.IndexOf(arcs, a)))])], towers, towerTop, gate, stone);

        if (!stone)
        {
            // A plank walk behind the stakes for the watch, and a timber gate on the south road.
            Ribbon(k, loop, -0.14f, 0.12f, height, Palette.DarkTimber, false);
            TimberGate(k, gate, (gate - Geo.Center).Normalized());
            return;
        }
        // The towers the town built, taller than the wall's own and flying its banner, with lit windows where the watch sleeps.
        var tint = WallStone(d.CityWall == PalisadeState.Built ? new Color("9b3d2a") : Slate, 0.8f);
        // Open fighting tops, so the watch can be seen up there when a tower is called to arms.
        string[] stack = ["tower-square-base", "tower-square-mid-windows", "tower-square-mid", "tower-square-top"];
        foreach (var c in towers)
        {
            k.Push(MeshKit.At(c.X, Ground(c) - 0.05f, c.Y));
            TowerStack(k, 0, 0, towerTop / 3.03f, stack, tint);
            k.Pop();
            k.Add("castle/flag-pennant", MeshKit.At(c.X, Ground(c) + towerTop + 0.1f, c.Y, 0, 0.3f), Palette.Kenney(new Color("2f5f9a")));
        }
        if (d.Moat == PalisadeState.Built)
        {
            Ribbon(k, loop, 0.95f, 0.9f, 0.07f, new Color("3d6f7a"), true);
            Ribbon(k, loop, 0.47f, 0.12f, 0.075f, new Color("6b5a3e"), true);
            Ribbon(k, loop, 1.43f, 0.12f, 0.075f, new Color("6b5a3e"), true);
            // A timber bridge carries the south road over the water to the gate.
            var outward = (gate - Geo.Center).Normalized();
            var mid = gate + outward * 0.95f;
            k.Push(MeshKit.At(mid.X, Mathf.Max(Ground(gate + outward * 0.4f), Ground(gate + outward * 1.5f)) + 0.06f, mid.Y, Mathf.Atan2(outward.X, outward.Y)));
            k.Box(Vector3.Zero, new Vector3(0.62f, 0.07f, 1.3f), Palette.Timber, new Color("8a6239"));
            foreach (float x in new[] { -0.3f, 0.3f }) k.Box(new Vector3(x, 0.07f, 0), new Vector3(0.04f, 0.1f, 1.3f), Palette.DarkTimber);
            k.Pop();
        }
        // Wall barracks: long stone blocks against the inside of the wall, west and east.
        for (int b = 0; b < d.Barracks; b++)
        {
            var at = RayHit(loop, Bearing(town, b == 0 ? 2 : 6));
            var inward = (Geo.Center - at).Normalized();
            var p = at + inward * 0.55f;
            float yaw = Mathf.Atan2(inward.X, inward.Y);
            k.Push(MeshKit.At(p.X, Ground(p) - 0.04f, p.Y, yaw));
            k.Box(Vector3.Zero, new Vector3(1.6f, height * 0.75f, 0.6f), new Color("8f877a"), new Color("6e665a"));
            k.Push(MeshKit.At(0, height * 0.75f, 0));
            k.Gable(0, 1.6f, 0.6f, 0.28f, new Color("5a4a3a"), new Color("8f877a"));
            k.Pop();
            for (int w = -2; w <= 2; w++)
                k.Box(new Vector3(w * 0.3f, height * 0.32f, 0.301f), new Vector3(0.08f, 0.1f, 0.01f), new Color(new Color("f2c766"), MeshKit.Flag.Window));
            k.Pop();
        }
    }

    /// <summary>A flat band along a closed loop, offset outward (positive) or inward from it.</summary>
    private void Ribbon(MeshKit k, Vector2[] loop, float offset, float width, float lift, Color col, bool ground)
    {
        float perim = Perimeter(loop);
        int n = Math.Max(16, (int)(perim / 0.35f));
        Vector2 Off(float t, float o)
        {
            var p = Along(loop, t);
            var q = Along(loop, (t + 0.05f) % perim);
            var tan = (q - p).Normalized();
            var normal = new Vector2(tan.Y, -tan.X);
            // Keep "out" pointing away from the town's heart whichever way the loop runs.
            if (normal.Dot(p - Geo.Center) < 0) normal = -normal;
            return p + normal * o;
        }
        for (int i = 0; i < n; i++)
        {
            float t0 = perim * i / n, t1 = perim * (i + 1) / n;
            var a = Off(t0, offset - width / 2);
            var b = Off(t0, offset + width / 2);
            var c = Off(t1, offset + width / 2);
            var e = Off(t1, offset - width / 2);
            float ya = Ground(a) + lift, yb = Ground(b) + lift, yc = Ground(c) + lift, ye = Ground(e) + lift;
            if (!ground) ya = yb = yc = ye = Ground((a + c) / 2) + lift;
            k.Quad(new Vector3(a.X, ya, a.Y), new Vector3(e.X, ye, e.Y), new Vector3(c.X, yc, c.Y), new Vector3(b.X, yb, b.Y), col);
            k.Quad(new Vector3(b.X, yb, b.Y), new Vector3(c.X, yc, c.Y), new Vector3(e.X, ye, e.Y), new Vector3(a.X, ya, a.Y), col);
        }
    }

    /// <summary>Two stout posts and a lintel where the south road goes through the stakes.</summary>
    private void TimberGate(MeshKit k, Vector2 at, Vector2 outward)
    {
        var side = new Vector2(outward.Y, -outward.X);
        float g = Ground(at) - 0.05f;
        foreach (float s in new[] { -1f, 1f })
        {
            var p = at + side * s * 0.32f;
            k.Frustum(new Vector3(p.X, g, p.Y), 0.07f, 0.07f, 0.62f, 6, Palette.DarkTimber);
        }
        float yaw = Mathf.Atan2(side.X, side.Y);
        k.Push(MeshKit.At(at.X, g + 0.56f, at.Y, yaw));
        k.Box(Vector3.Zero, new Vector3(0.05f, 0.07f, 0.8f), Palette.Timber);
        k.Pop();
        k.Add("castle/flag-pennant", MeshKit.At(at.X + side.X * 0.32f, g + 0.62f, at.Y + side.Y * 0.32f, 0, 0.22f), Palette.Kenney(new Color("b8322a")));
    }

    /// <summary>The wall section, tower or gate nearest a point of the map, if the point is on the wall.</summary>
    public (string Kind, int Index)? WallAt(Town town, Vector2 w)
    {
        if (Walk is not { } walk) return null;
        for (int i = 0; i < walk.Towers.Length; i++)
            if (walk.Towers[i].DistanceTo(w) < 0.55f) return ("tower", i);
        float best = 0.5f;
        int section = -1;
        foreach (var arc in walk.Sections)
            foreach (var p in arc)
            {
                float dd = p.DistanceTo(w);
                if (dd < best)
                {
                    best = dd;
                    section = SectionOf(town, p);
                }
            }
        return section >= 0 ? ("wall", section) : null;
    }
}
