using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public partial class Crowd
{
    /// <summary>
    /// A den out in the wilds: where one kind of creature lives and roams. Every creature in the library has one, in
    /// country of its own habitat at the distance its danger belongs (harmless beasts by the town, ancient dragons
    /// at the edge of the world), so each can be come across. Presentation only, from the seed (<see cref="Geo.Hash"/>).
    /// </summary>
    private readonly record struct Den(string Id, Anatomy A, Vector2 At, Vector2 To, int Count, float Seed);

    private List<Den>? _dens;
    private int _densFor = int.MinValue;

    /// <summary>How far from the camera's spot dens are drawn, in tiles.</summary>
    private const float WildReach = 140;

    /// <summary>Dens drawn this frame (for --bench).</summary>
    public int WildDrawn { get; private set; }

    /// <summary>The town's dens, laid out once per world.</summary>
    public IReadOnlyList<(string Id, Vector2 At)> Dens(Town town) => DensOf(town).Select(d => (d.Id, d.At)).ToList();

    private List<Den> DensOf(Town town)
    {
        if (_dens != null && _densFor == town.Seed) return _dens;
        _densFor = town.Seed;
        _dens = [];
        var gen = map.Gen;
        var rings = town.Content.World.Rings;
        float edge = Geo.World / 2f - 40;
        int n = 0;
        foreach (var m in Bestiary.All(town.Content))
        {
            n++;
            var a = Bestiary.Of(town.Content, m.Id);
            int h = (int)(Bestiary.Hash(m.Id) & 0x7fffffff);
            float H(int k) => Geo.Hash(h + k * 7919 + town.Seed * 31);
            // How far out: the ring whose sites are built for a party its match, the harmless just past the fields.
            double level = Math.Max(0.5, m.Cr);
            double inner = 0, outer = rings.Count > 0 ? rings[^1].Days : 18;
            for (int r = 0; r < rings.Count; r++)
                if (rings[r].Levels[^1] >= level)
                {
                    inner = r == 0 ? 0.35 : rings[r - 1].Days;
                    outer = rings[r].Days;
                    break;
                }
            if (rings.Count > 0 && level > rings[^1].Levels[^1])
            {
                inner = rings[^1].Days;
                outer = (edge - Geo.Reach0) / Geo.TilesPerDay;
            }
            float radius = Mathf.Min(edge, Mathf.Max(Geo.Clearing * 1.15f, Geo.DayRadius(inner + (outer - inner) * (0.15 + 0.8 * H(1)))));
            // Which way: the first bearing round from its own whose country suits it.
            var want = m.Habitats.Select(WorldGen.HabitatBiome).OfType<Biome>().ToHashSet();
            bool wet = a.Gait == "swim";
            float b0 = H(2) * Mathf.Tau;
            Vector2 at = Geo.Center + new Vector2(Mathf.Sin(b0), -Mathf.Cos(b0)) * radius;
            for (int k = 0; k < 48; k++)
            {
                float b = b0 + (k % 2 == 0 ? 1 : -1) * (k + 1) / 2 * 0.13f;
                var p = Geo.Center + new Vector2(Mathf.Sin(b), -Mathf.Cos(b)) * (radius + (H(3 + k) - 0.5f) * 12);
                float ground = gen.HeightAt(p.X, p.Y);
                bool fits = wet ? ground < -0.4f : ground > 0.3f && ground < 30;
                if (!wet && want.Count > 0) fits &= want.Contains(gen.Biomes[gen.TerritoryAt(p)]);
                if (!fits) continue;
                at = p;
                break;
            }
            int count = a.Roam switch
            {
                "pack" => 3 + (int)(H(60) * 3),
                "herd" => 4 + (int)(H(60) * 5),
                "march" => 3 + (int)(H(60) * 3),
                "school" => 4 + (int)(H(60) * 3),
                "circle" => 1 + (H(60) < 0.3f ? 1 : 0),
                _ => 1,
            };
            if (a.Scale >= 2.4) count = Math.Min(count, 2);
            // A marching band patrols out to a second spot and back.
            float tb = H(61) * Mathf.Tau;
            var to = at + new Vector2(Mathf.Cos(tb), Mathf.Sin(tb)) * (8 + H(62) * 8);
            _dens.Add(new Den(m.Id, a, at, to, count, H(63) * 100));
        }
        return _dens;
    }

    /// <summary>
    /// Every den the camera is near and the town can see: each kind about its own business, by its roam (Anatomy.Roam):
    /// packs lope in a loop and rest, herds graze and drift, fliers circle overhead, lurkers keep still, burrowers come
    /// up and go under, bands march out and back, schools turn in the water, the rooted sway, drifters wander slowly.
    /// </summary>
    private void Wildlife(Town town, float now)
    {
        WildDrawn = 0;
        if (Gallery.Count > 0) return;
        var gen = map.Gen;
        var centre = map.Target;
        foreach (var d in DensOf(town))
        {
            if (d.At.DistanceSquaredTo(centre) > WildReach * WildReach) continue;
            if (!map.Fog.IsLive(d.At)) continue;
            WildDrawn++;
            float size = (float)d.A.Scale;
            float pace = 0.5f + 0.35f * Mathf.Sqrt(size);
            for (int j = 0; j < d.Count; j++)
            {
                float s = d.Seed + j * 1.618f;
                var spread = new Vector2(Mathf.Cos(s * 2.4f), Mathf.Sin(s * 2.4f)) * (0.5f + 0.4f * j) * Mathf.Max(0.6f, size);
                Vector2 p;
                float yaw;
                var act = Beasts.Act.Idle;
                float u = now * 0.3f + s;
                float sink = 0;
                switch (d.A.Roam)
                {
                    case "circle":
                    {
                        // On the wing round its den, wide and slow for the big ones.
                        float r = 4 + 2 * size + j * 1.5f, w = pace / r * (j % 2 == 0 ? 1 : -1);
                        float ang = now * w + s;
                        p = d.At + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                        var v = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)) * w;
                        yaw = Mathf.Atan2(v.X, v.Y);
                        act = Beasts.Act.Move;
                        u = now * 1.2f + s;
                        break;
                    }
                    case "march":
                    {
                        // Out along the trail in file, a halt at each end, and back.
                        float period = 2 * d.At.DistanceTo(d.To) / pace + 24;
                        float f = Mathf.PosMod(now / period + d.Seed, 1);
                        float go = f < 0.4f ? f / 0.4f : f < 0.5f ? 1 : f < 0.9f ? 1 - (f - 0.5f) / 0.4f : 0;
                        bool moving = f < 0.4f || (f >= 0.5f && f < 0.9f);
                        var dir = (d.To - d.At).Normalized();
                        bool back = f >= 0.5f;
                        p = d.At.Lerp(d.To, go) - (back ? -dir : dir) * j * 0.7f + dir.Orthogonal() * ((j % 2) - 0.5f) * 0.4f;
                        var face = back ? -dir : dir;
                        yaw = moving ? Mathf.Atan2(face.X, face.Y) : s;
                        act = moving ? Beasts.Act.Move : Beasts.Act.Idle;
                        u = moving ? now * 1.6f + j * 0.3f : now * 0.3f + s;
                        break;
                    }
                    default:
                    {
                        // A wander about its den: Lissajous loops, with rests (the herd's long, the drifter's none).
                        var (reach, rest, slow) = d.A.Roam switch
                        {
                            "pack" => (6f, 0.45f, 1f),
                            "herd" => (5f, 0.75f, 0.45f),
                            "lurk" => (2f, 0.85f, 0.7f),
                            "burrow" => (4f, 0.5f, 0.8f),
                            "drift" or "haunt" => (4f, 0f, 0.3f),
                            "school" => (5f, 0f, 0.8f),
                            "swarm" => (3f, 0f, 0.6f),
                            "rooted" => (0f, 1f, 0f),
                            _ => (5f, 0.55f, 0.8f),
                        };
                        reach *= Mathf.Max(1, Mathf.Sqrt(size));
                        float wa = pace * slow / Mathf.Max(1, reach), wb = wa * 1.37f;
                        float period = 40;
                        float f = Mathf.PosMod(now / period + d.Seed * 0.13f, 1);
                        bool moving = f > rest && reach > 0;
                        float tau = (Mathf.Floor(now / period + d.Seed * 0.13f) * (1 - rest) + Mathf.Max(0, f - rest)) * period;
                        // The herd and the school keep together; the pack runs on its leader's heels.
                        float lag = d.A.Roam == "pack" ? j * 0.9f / Mathf.Max(0.01f, pace) : 0;
                        float t = tau - lag;
                        var centre2 = d.At + (d.A.Roam is "pack" ? Vector2.Zero : spread);
                        p = centre2 + new Vector2(Mathf.Sin(wa * t + s * 0.1f), Mathf.Sin(wb * t)) * reach;
                        var v = new Vector2(wa * Mathf.Cos(wa * t + s * 0.1f), wb * Mathf.Cos(wb * t));
                        if (d.A.Roam == "pack") p += spread * 0.3f;
                        yaw = moving ? Mathf.Atan2(v.X, v.Y) : Mathf.Atan2(v.X, v.Y) + 0.6f * Mathf.Sin(now * 0.07f + s);
                        act = moving ? Beasts.Act.Move : Beasts.Act.Idle;
                        u = moving ? now * 1.5f * Mathf.Max(0.5f, slow) + s : now * 0.3f + s;
                        // Burrowers go under while they rest and come up to roam.
                        if (d.A.Roam == "burrow")
                        {
                            float up = Mathf.Clamp((f - rest + 0.04f) / 0.04f, 0, 1) * Mathf.Clamp((1 - f) / 0.04f, 0, 1);
                            if (up <= 0) continue;
                            sink = (1 - up) * 0.8f * size;
                        }
                        break;
                    }
                }
                var at = gen.At(p) + new Vector3(0, Beasts.Lift(d.Id, act == Beasts.Act.Move) - sink, 0);
                Beasts.Put(d.Id, new Transform3D(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(Vector3.One * 0.95f), at), act, u);
            }
        }
    }
}
