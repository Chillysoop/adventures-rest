using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

public sealed class Sprite
{
    public Vector2 P;
    public float Yaw;
    public bool Walking;
    public bool Visible;
}

/// <summary>
/// Townsfolk and heroes in town, walking at a steady pace toward wherever the simulation says they are.
/// The pace grows with the game speed (gently, so 36x is a brisk walk, not a blur), stops when paused,
/// and only hurries for someone who has fallen far behind.
/// </summary>
public sealed class AgentPositions(MapView map)
{
    public readonly Dictionary<int, Sprite> Sprites = [];

    public void Reset() => Sprites.Clear();

    private Vector2? Target(Town town, Agent a)
    {
        if (a.Departed || a.Dead || a.IsAway || a.Activity == Activity.Sleeping) return null;
        float j1 = Geo.Hash(a.Id * 13 + 1), j2 = Geo.Hash(a.Id * 13 + 2);
        if (a.LocationId is int loc)
        {
            var lot = town.Lot(town.Building(loc).LotId);
            // Gather in the lane in front of the building, whichever way it faces.
            var f = Geo.LotFront(lot);
            float half = lot.W / (float)Sim.Systems.Land.Res / 2;
            return Geo.LotPos(lot) + f * (lot.D / (float)Sim.Systems.Land.Res / 2 + 0.12f + (j2 - 0.5f) * 0.08f) + new Vector2(f.Y, -f.X) * ((j1 - 0.5f) * 2 * half);
        }
        // Wanderers drift around the square, picking a new spot every few hours.
        int k = town.Tick / 4;
        float ang = Geo.Hash(a.Id * 31 + k) * Mathf.Tau;
        float r = 1 + Geo.Hash(a.Id * 17 + k) * 4.5f;
        return Geo.Center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
    }

    private const float Pace = 1.1f; // tiles per real second at 1x
    private const float TurnRate = 9f; // radians per second

    public void Advance(float dt, float timeScale)
    {
        var town = map.Sim.Town;
        float pace = timeScale <= 0 ? 0 : Pace * Mathf.Sqrt(timeScale);
        foreach (var a in town.Agents)
        {
            var tgt = Target(town, a);
            Sprites.TryGetValue(a.Id, out var s);
            if (tgt == null)
            {
                if (s != null) s.Visible = false;
                continue;
            }
            if (s == null) Sprites[a.Id] = s = new Sprite { P = Geo.Center + new Vector2(Geo.Hash(a.Id) - 0.5f, Geo.Hash(a.Id + 1) - 0.5f) * 3, Yaw = Geo.Hash(a.Id) * Mathf.Tau };
            // Someone who reappears (back from the road, awake again) starts where they are meant to be.
            if (!s.Visible) s.P = tgt.Value;
            s.Visible = true;
            var to = tgt.Value - s.P;
            float dist = to.Length();
            float step = Mathf.Max(pace, pace > 0 ? dist / 2.5f : 0) * dt;
            s.Walking = dist > 0.02f && step > 0;
            if (!s.Walking) continue;
            if (dist > 30 || step >= dist) s.P = tgt.Value;
            else s.P += to / dist * step;
            float want = Mathf.Atan2(to.X, to.Y);
            s.Yaw += Mathf.Clamp(Mathf.Wrap(want - s.Yaw, -Mathf.Pi, Mathf.Pi), -TurnRate * dt, TurnRate * dt);
        }
    }
}

/// <summary>
/// Everyone on the map as instanced figures: townsfolk and heroes in town, companies on the road,
/// scouts at their posts and the warband on the march. One draw call per kind of figure, whatever
/// the head count; figures under fog are simply not drawn.
/// </summary>
public partial class Crowd(MapView map) : Node3D
{
    public readonly AgentPositions People = new(map);
    private readonly MultiMeshInstance3D _folk = Make(), _heroes = Make(), _orcs = Make(), _banners = Make(), _guards = Make();
    private readonly List<float> _buf = [];

    private static MultiMeshInstance3D Make() => new()
    {
        Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true },
        MaterialOverride = Mats.People,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
    };

    public override void _Ready()
    {
        var kit = new MeshKit();
        Figure(kit, false);
        _folk.Multimesh.Mesh = kit.Build();
        kit.Clear();
        Figure(kit, true);
        _heroes.Multimesh.Mesh = kit.Build();
        kit.Clear();
        Orc(kit);
        _orcs.Multimesh.Mesh = kit.Build();
        kit.Clear();
        kit.Frustum(Vector3.Zero, 0.012f, 0.01f, 0.62f, 4, new Color("4a3424"));
        Vector3 a = new(0.012f, 0.6f, 0), b = new(0.232f, 0.56f, 0), c = new(0.232f, 0.44f, 0), d = new(0.012f, 0.44f, 0);
        kit.Quad(a, b, c, d, Colors.White);
        kit.Quad(d, c, b, a, Colors.White);
        _banners.Multimesh.Mesh = kit.Build();
        kit.Clear();
        Soldier(kit);
        _guards.Multimesh.Mesh = kit.Build();
        foreach (var m in new[] { _folk, _heroes, _orcs, _banners, _guards }) AddChild(m);
    }

    /// <summary>A little person: tunic (tinted per instance), head and hands (skin from instance data), a hood or hat.</summary>
    private static void Figure(MeshKit k, bool hero)
    {
        var skin = new Color(1, 1, 1, MeshKit.Flag.Skin);
        float s = hero ? 1.75f : 1.55f;
        k.Push(MeshKit.At(0, 0, 0, 0, s));
        k.Frustum(new Vector3(-0.025f, 0, 0), 0.018f, 0.018f, 0.07f, 4, new Color("3a2c22"));
        k.Frustum(new Vector3(0.025f, 0, 0), 0.018f, 0.018f, 0.07f, 4, new Color("3a2c22"));
        k.Frustum(new Vector3(0, 0.06f, 0), 0.062f, 0.045f, 0.11f, 6, Colors.White);
        k.Blob(new Vector3(0, 0.205f, 0), new Vector3(0.042f, 0.045f, 0.042f), skin);
        k.Blob(new Vector3(-0.06f, 0.1f, 0.01f), new Vector3(0.018f, 0.018f, 0.018f), skin);
        k.Blob(new Vector3(0.06f, 0.1f, 0.01f), new Vector3(0.018f, 0.018f, 0.018f), skin);
        if (hero)
        {
            // A cloak behind and a blade at the hip mark the heroes out from the townsfolk.
            k.Quad(new(-0.05f, 0.17f, -0.04f), new(0.05f, 0.17f, -0.04f), new(0.065f, 0.03f, -0.07f), new(-0.065f, 0.03f, -0.07f), new Color("e8b93a"));
            k.Box(new Vector3(0.075f, 0.02f, 0.02f), new Vector3(0.012f, 0.14f, 0.012f), new Color("c8ccd0"));
            k.Frustum(new Vector3(0, 0.235f, 0), 0.048f, 0.03f, 0.03f, 6, new Color("8a7a66"));
        }
        else k.Frustum(new Vector3(0, 0.235f, 0), 0.046f, 0, 0.05f, 6, new Color("7a6448"));
        k.Pop();
    }

    /// <summary>One of the town watch: a tabard in the instance colour over mail, a kettle helm, a spear and a round shield.</summary>
    private static void Soldier(MeshKit k)
    {
        var skin = new Color(1, 1, 1, MeshKit.Flag.Skin);
        var steel = new Color("9aa0a6");
        k.Push(MeshKit.At(0, 0, 0, 0, 1.6f));
        k.Frustum(new Vector3(-0.025f, 0, 0), 0.019f, 0.019f, 0.07f, 4, new Color("3a2c22"));
        k.Frustum(new Vector3(0.025f, 0, 0), 0.019f, 0.019f, 0.07f, 4, new Color("3a2c22"));
        k.Frustum(new Vector3(0, 0.06f, 0), 0.064f, 0.05f, 0.11f, 6, Colors.White);
        k.Box(new Vector3(0, 0.12f, 0), new Vector3(0.1f, 0.05f, 0.09f), steel);
        k.Blob(new Vector3(0, 0.205f, 0), new Vector3(0.04f, 0.043f, 0.04f), skin);
        k.Frustum(new Vector3(0, 0.215f, 0), 0.06f, 0.04f, 0.035f, 8, steel);
        k.Blob(new Vector3(0.06f, 0.1f, 0.01f), new Vector3(0.018f, 0.018f, 0.018f), skin);
        k.Box(new Vector3(0.065f, 0.0f, 0.02f), new Vector3(0.012f, 0.36f, 0.012f), new Color("6a4a30"));
        k.Frustum(new Vector3(0.065f, 0.36f, 0.02f), 0.014f, 0, 0.05f, 4, steel);
        k.Push(MeshKit.At(-0.07f, 0.1f, 0.01f, Mathf.Pi / 2));
        k.Frustum(Vector3.Zero, 0.05f, 0.05f, 0.012f, 8, new Color("7a2a22"), steel);
        k.Pop();
        k.Pop();
    }

    private static void Orc(MeshKit k)
    {
        k.Push(MeshKit.At(0, 0, 0, 0, 1.6f));
        var skin = new Color("6f8f4a");
        k.Frustum(new Vector3(-0.03f, 0, 0), 0.024f, 0.024f, 0.07f, 4, new Color("3a2a20"));
        k.Frustum(new Vector3(0.03f, 0, 0), 0.024f, 0.024f, 0.07f, 4, new Color("3a2a20"));
        k.Frustum(new Vector3(0, 0.06f, 0), 0.075f, 0.065f, 0.12f, 6, Colors.White);
        k.Blob(new Vector3(0, 0.215f, 0.01f), new Vector3(0.05f, 0.048f, 0.05f), skin);
        k.Blob(new Vector3(-0.08f, 0.15f, 0), new Vector3(0.03f, 0.025f, 0.03f), new Color("5a4a3a"));
        k.Blob(new Vector3(0.08f, 0.15f, 0), new Vector3(0.03f, 0.025f, 0.03f), new Color("5a4a3a"));
        k.Box(new Vector3(0.1f, 0.05f, 0.03f), new Vector3(0.02f, 0.2f, 0.02f), new Color("5a4030"));
        k.Box(new Vector3(0.1f, 0.22f, 0.03f), new Vector3(0.07f, 0.05f, 0.02f), new Color("9a9a9a"));
        k.Pop();
    }

    public void Reset() => People.Reset();

    private int _count;

    private void Put(Vector3 p, float yaw, Color body, Color skin, float scale = 1)
    {
        var b = Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scale, scale, scale));
        _buf.AddRange([b.X.X, b.Y.X, b.Z.X, p.X, b.X.Y, b.Y.Y, b.Z.Y, p.Y, b.X.Z, b.Y.Z, b.Z.Z, p.Z, body.R, body.G, body.B, 1, skin.R, skin.G, skin.B, 1]);
        _count++;
    }

    private void Flush(MultiMeshInstance3D mmi)
    {
        var mm = mmi.Multimesh;
        if (mm.InstanceCount != _count) mm.InstanceCount = _count;
        if (_count > 0) mm.Buffer = _buf.ToArray();
        _buf.Clear();
        _count = 0;
    }

    private static readonly Color[] Tunics = [new("7a5a3a"), new("5f6f4a"), new("8a6a4a"), new("4f5f6f"), new("9a7a52"), new("6a4a4a"), new("a08a62"), new("566048")];

    public void Advance(float dt)
    {
        var town = map.Sim.Town;
        People.Advance(dt, map.TimeScale);
        float now = (float)Time.GetTicksMsec() / 1000f;
        var gen = map.Gen;
        bool close = map.Distance < 260;
        Visible = close;
        if (!close) return;

        // Townsfolk in one batch, heroes in another.
        foreach (bool heroes in new[] { false, true })
        {
            foreach (var a in town.Agents)
            {
                if ((a.Hero != null) != heroes || !People.Sprites.TryGetValue(a.Id, out var s) || !s.Visible) continue;
                float bob = s.Walking ? Mathf.Abs(Mathf.Sin(now * 8 + a.Id)) * 0.02f : 0;
                var p = gen.At(s.P, bob);
                var body = a.Hero != null ? Palette.ClassColor(a.Hero.Cls.Id) : Tunics[a.Id % Tunics.Length];
                Put(p, s.Yaw, body, Palette.RaceColor(a.Race));
            }
            // Heroes on the road and scouts join the heroes' batch below.
            if (!heroes) Flush(_folk);
        }

        // Companies on the road, under their banner; the fog swallows them past the town's light.
        var banners = new List<(Vector3 P, float Yaw, Color Color)>();
        foreach (var p in town.Parties)
        {
            if (p.Returned || !map.Geo.Pins.TryGetValue(p.SiteId, out var pin)) continue;
            if (Geo.PartyAt(town, p, map.HourFraction) is not var (pos, pace)) continue;
            if (map.Fog.AlphaAt(pos) >= Fog.SightAlpha) continue;
            // Facing the way they are walking (or were, when they stopped).
            var dir = (pin - Geo.Center).Normalized();
            for (float back = 0.25f; back <= 4; back *= 2)
                if (Geo.PartyAt(town, p, map.HourFraction - back) is var (was, _) && was.DistanceSquaredTo(pos) > 0.0004f)
                {
                    dir = (pos - was).Normalized();
                    break;
                }
            float yaw = Mathf.Atan2(dir.X, dir.Y);
            var side = new Vector2(dir.Y, -dir.X);
            bool stopped = pace is Pace.Camped or Pace.Resting;
            for (int i = 0; i < p.Members.Count; i++)
            {
                var a = town.Agent(p.Members[i]);
                if (stopped)
                {
                    // Sitting round the fire, facing it.
                    float ang = i * Mathf.Tau / p.Members.Count + 0.4f;
                    var ring = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.55f;
                    Put(gen.At(pos + ring), Mathf.Atan2(-ring.X, -ring.Y), Palette.ClassColor(a.Hero?.Cls.Id ?? ""), Palette.RaceColor(a.Race), 1.1f);
                    continue;
                }
                var off = -dir * (0.35f + i / 2 * 0.32f) + side * ((i % 2) - 0.5f) * 0.3f;
                float bob = Mathf.Abs(Mathf.Sin(now * 9 + i * 1.7f)) * 0.03f;
                Put(gen.At(pos + off, bob), yaw, Palette.ClassColor(a.Hero?.Cls.Id ?? ""), Palette.RaceColor(a.Race), 1.1f);
            }
            banners.Add((gen.At(stopped ? pos + side * 0.9f : pos), yaw, Palette.Gold));
        }
        foreach (var s in town.Scouts)
        {
            if (!s.Out || !map.Geo.Pins.TryGetValue(s.SiteId, out var pin)) continue;
            var pos = ScoutPos(town, s, pin);
            if (map.Fog.AlphaAt(pos) >= Fog.SightAlpha) continue;
            var a = town.Agent(s.HeroId);
            var dir = (pin - pos).Normalized();
            Put(gen.At(pos), Mathf.Atan2(dir.X, dir.Y), new Color("3f5f3a"), Palette.RaceColor(a.Race), 1.1f);
        }
        Flush(_heroes);
        Guards(town, now, banners);
        foreach (var (bp, yaw, col) in banners) Put(bp, yaw + Mathf.Pi / 2, col, Colors.White, 2.2f);
        Flush(_banners);

        // The warband on the march, seen only once it is inside the light.
        var en = town.Enemy;
        if (en.Stage == ThreatStage.Marching && en.ArrivalTick is int arrival)
        {
            float total = town.Content.Enemy.MarchDays * SimTime.HoursPerDay;
            float t = Mathf.Clamp(1 - (arrival - town.Tick - map.HourFraction) / total, 0, 1);
            var from = map.Geo.Pins.TryGetValue(town.Content.Enemy.Outpost.Id, out var camp) ? camp : Geo.Center + new Vector2(60, -60);
            var pos = from + (Geo.Center - from) * t * 0.85f;
            var dir = (Geo.Center - from).Normalized();
            int n = Math.Min(160, (int)Math.Round(en.Strength / 3));
            for (int i = 0; i < n; i++)
            {
                var o = new Vector2((Geo.Hash(i * 5 + 1) - 0.5f) * 7, (Geo.Hash(i * 5 + 2) - 0.5f) * 5);
                if (map.Fog.AlphaAt(pos + o) >= Fog.SightAlpha) continue;
                float bob = Mathf.Abs(Mathf.Sin(now * 8 + i)) * 0.03f;
                Put(gen.At(pos + o, bob), Mathf.Atan2(dir.X, dir.Y), i % 7 == 0 ? new Color("4a2018") : new Color("6a3a2a"), Colors.White, 1.1f);
            }
        }
        Flush(_orcs);
    }

    /// <summary>Where each guard stands on the map this frame, for picking them out with the mouse.</summary>
    public readonly Dictionary<int, Vector2> GuardSpots = [];

    private static readonly Color Tabard = new("8a2a24");

    /// <summary>
    /// The town watch: pacing their beat along the wall walk, standing their posts on a called stretch, a tower top or
    /// round a flag, and crossing town between them when an order sends them. Flags fly the watch's red.
    /// </summary>
    private void Guards(Town town, float now, List<(Vector3 P, float Yaw, Color Color)> banners)
    {
        GuardSpots.Clear();
        var gen = map.Gen;
        var walk = map.TownView.Walk;
        var ranks = new Dictionary<int, int>();
        var counts = new Dictionary<int, int>();
        foreach (var g in town.Garrison.Guards)
            if (g.PostId is int pid)
            {
                ranks[g.Id] = counts.GetValueOrDefault(pid);
                counts[pid] = ranks[g.Id] + 1;
            }
        foreach (var g in town.Garrison.Guards)
        {
            var post = Garrison.Post(town, g.PostId);
            int rank = ranks.GetValueOrDefault(g.Id), of = post != null ? counts.GetValueOrDefault(post.Id, 1) : 1;
            var (spot, lift, face) = Stand(town, walk, g, post, rank, of, now);
            float t = town.Tick + map.HourFraction;
            Vector2 p = spot;
            float yaw = face;
            if (t < g.ArriveTick && g.ArriveTick > g.DepartTick)
            {
                // On the way: straight across town, climbing to the wall walk at the end.
                var from = new Vector2(Geo.Margin + (float)g.FromX, Geo.Margin + (float)g.FromY);
                float f = Mathf.Clamp((t - g.DepartTick) / (g.ArriveTick - g.DepartTick), 0, 1);
                p = from.Lerp(spot, f);
                lift *= Mathf.Pow(f, 6);
                var dir = spot - from;
                if (dir.LengthSquared() > 1e-4f) yaw = Mathf.Atan2(dir.X, dir.Y);
                lift += Mathf.Abs(Mathf.Sin(now * 9 + g.Id)) * 0.025f;
            }
            GuardSpots[g.Id] = p;
            Put(gen.At(p, lift), yaw, Tabard, Palette.RaceColor("human"), 1.05f);
        }
        Flush(_guards);
        foreach (var post in town.Garrison.Posts)
        {
            if (post.Kind != PostKind.Flag) continue;
            var at = new Vector2(Geo.Margin + (float)post.X, Geo.Margin + (float)post.Y);
            banners.Add((gen.At(at), Mathf.Sin(now * 0.7f + post.Id) * 0.4f, new Color("b8322a")));
        }
    }

    /// <summary>Where a guard stands (or paces) once there, how high above the ground, and which way they face.</summary>
    private static (Vector2 Spot, float Lift, float Yaw) Stand(Town town, WallWalk? walk, Guard g, GuardPost? post, int rank, int of, float now)
    {
        float Out(Vector2 p)
        {
            var o = p - Geo.Center;
            return Mathf.Atan2(o.X, o.Y);
        }
        if (post?.Kind == PostKind.Flag)
        {
            var at = new Vector2(Geo.Margin + (float)post.X, Geo.Margin + (float)post.Y);
            float a = rank * Mathf.Tau / Math.Max(1, of) + post.Id;
            var p = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.45f;
            return (p, 0, Mathf.Atan2(p.X - at.X, p.Y - at.Y));
        }
        if (walk == null) return (Geo.Center + new Vector2(Mathf.Cos(g.Id * 2.4f), Mathf.Sin(g.Id * 2.4f)) * (1.5f + g.Id % 3), 0, g.Id);
        if (post?.Kind == PostKind.Tower && post.Index < walk.Towers.Length)
        {
            var c = walk.Towers[post.Index];
            float a = rank * Mathf.Pi + 0.6f;
            var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.1f;
            return (p, walk.TowerTop, Out(c));
        }
        int section = post == null ? g.Beat : post.Kind == PostKind.Tower ? Garrison.TowerSection(town, post.Index) : post.Index;
        var arc = walk.Sections[Math.Clamp(section, 0, walk.Sections.Length - 1)];
        if (post != null)
        {
            // Spread along the called stretch, facing out over the wall.
            var p = AlongArc(arc, (rank + 0.5f) / of * 0.8f + 0.1f);
            return (p, walk.Height, Out(p));
        }
        // On their beat: up and down the stretch, a little out of step with the others.
        float speed = 0.05f + Geo.Hash(g.Id) * 0.03f;
        float u = 0.5f + 0.42f * Mathf.Sin(now * speed * Mathf.Tau + g.Id * 1.7f);
        var at2 = AlongArc(arc, u);
        var ahead = AlongArc(arc, Mathf.Clamp(u + (Mathf.Cos(now * speed * Mathf.Tau + g.Id * 1.7f) >= 0 ? 0.02f : -0.02f), 0, 1));
        var d = ahead - at2;
        return (at2, walk.Height, d.LengthSquared() > 1e-6f ? Mathf.Atan2(d.X, d.Y) : Out(at2));
    }

    /// <summary>A point a share of the way along a polyline.</summary>
    private static Vector2 AlongArc(Vector2[] arc, float u)
    {
        if (arc.Length == 1) return arc[0];
        float f = Mathf.Clamp(u, 0, 1) * (arc.Length - 1);
        int i = Math.Min(arc.Length - 2, (int)f);
        return arc[i].Lerp(arc[i + 1], f - i);
    }

    public Vector2? PartyPos(Town town, Party p) => Geo.PartyAt(town, p, map.HourFraction)?.Pos;

    public Vector2 ScoutPos(Town town, Scout s, Vector2 pin)
    {
        float leg = (float)Watch.ScoutLeg(town, s, map.HourFraction);
        var dir = (pin - Geo.Center).Normalized();
        return leg >= 1 ? pin - dir * 6 + new Vector2(dir.Y, -dir.X) * 2 : Geo.Center + (pin - Geo.Center) * leg;
    }
}
