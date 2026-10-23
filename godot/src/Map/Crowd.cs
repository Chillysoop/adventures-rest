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

    /// <summary>Where they head next on the way to <see cref="For"/>: a gate, the moat's bridge, round a wall (<see cref="Passage.Next"/>).</summary>
    public Vector2? Via;
    public Vector2 For;

    /// <summary>Running for their life: straight through the moat and over the wall (<see cref="Passage.Fleeing"/>).</summary>
    public bool Fleeing;
}

/// <summary>
/// Townsfolk and heroes in town, walking at a steady pace toward wherever the simulation says they are.
/// The pace grows with the game speed (gently, so 36x is a brisk walk, not a blur), stops when paused,
/// and only hurries for someone who has fallen far behind.
/// </summary>
public sealed class AgentPositions(MapView map)
{
    public readonly Dictionary<int, Sprite> Sprites = [];

    public void Reset()
    {
        Sprites.Clear();
        Labourers.Clear();
        _yards.Clear();
    }

    private Vector2? Target(Town town, Agent a)
    {
        // Raiders at the door: run for the hall, whatever the hour.
        if (Passage.Fleeing(town, a)) return Geo.Center + new Vector2(Geo.Hash(a.Id * 13 + 1) - 0.5f, Geo.Hash(a.Id * 13 + 2) - 0.5f) * 3;
        if (a.Departed || a.Dead || a.IsAway || a.Activity == Activity.Sleeping) return null;
        float j1 = Geo.Hash(a.Id * 13 + 1), j2 = Geo.Hash(a.Id * 13 + 2);
        // Turned away at a full door: they walk up to it first, then go on to wherever they thought of instead.
        if (a.TurnedFrom is int full && map.HourFraction < 0.4f)
            return Front(town.Lot(town.Building(full).LotId), j1, j2) + new Vector2(j1 - 0.5f, j2 - 0.5f) * 0.6f;
        if (a.RegimentId != null && Soldiering(town, a) is Vector2 post) return post;
        if (a.LocationId is int loc)
        {
            // A hauler walks to the lumber camp or quarry (or the market at the square) and back with a load, twice an hour.
            if (a.Activity == Activity.Hauling && Loaded(town, a) is false) return Yard(town, town.Building(loc)) + new Vector2(j1 - 0.5f, j2 - 0.5f) * 0.4f;
            // Gather in the lane in front of the building, whichever way it faces.
            return Front(town.Lot(town.Building(loc).LotId), j1, j2);
        }
        // Wanderers drift around the square, picking a new spot every few hours.
        int k = town.Tick / 4;
        float ang = Geo.Hash(a.Id * 31 + k) * Mathf.Tau;
        float r = 1 + Geo.Hash(a.Id * 17 + k) * 4.5f;
        return Geo.Center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
    }

    /// <summary>A hauler's half of the trip: carrying a load to the site (true) or walking back for the next (false); null for anyone else.</summary>
    public bool? Loaded(Town town, Agent a) =>
        a.Activity != Activity.Hauling ? null : Mathf.PosMod(map.HourFraction * 2 + Geo.Hash(a.Id * 13 + 5), 1) >= 0.5f;

    private readonly Dictionary<int, (int Tick, Vector2 At)> _yards = [];

    /// <summary>Where a site's materials are fetched from (<see cref="Construction.Yard"/>), worked out once an hour per site.</summary>
    private Vector2 Yard(Town town, Building site)
    {
        if (_yards.TryGetValue(site.Id, out var y) && y.Tick == town.Tick) return y.At;
        Vector2 at;
        if (Construction.Yard(town, site) is { } yard)
        {
            var l = town.Lot(yard.LotId);
            at = Geo.LotPos(l) + Geo.LotFront(l) * (l.D / (float)Sim.Systems.Land.Res / 2 + 0.15f);
        }
        else at = Geo.Center + new Vector2(0.4f, 1.6f);
        _yards[site.Id] = (town.Tick, at);
        return at;
    }

    private const float Pace = 1.1f; // tiles per real second at 1x
    private const float TurnRate = 9f; // radians per second

    /// <summary>
    /// Hands hired off the road for sites nobody in town works (<see cref="Economy.Hourly"/>): one per such site in working hours,
    /// hammering at its front or, while it is short of materials, hauling them in. Their sprites are keyed by the site.
    /// </summary>
    public readonly Dictionary<int, Sprite> Labourers = [];

    /// <summary>Whether a site is worked by a hired hand this hour, and whether they are hauling (<see cref="Construction.Short"/>).</summary>
    public static (bool Working, bool Hauling) Hand(Town town, Building b) =>
        b.IsSite && b.Workers.Count == 0 && !GrandWorks.Waiting(b) && SimTime.IsWorkHours(town.Hour)
            && (b.LaborBudget >= Brain.ConstructionWage || Construction.HoursLeft(town, b) <= 4)
            ? (true, Construction.Short(town, b)) : (false, false);

    /// <summary>A hired hand's half of a haul: carrying a load in (true) or walking back for the next.</summary>
    public bool Carrying(Building site) => Mathf.PosMod(map.HourFraction * 2 + Geo.Hash(site.Id * 7 + 3), 1) >= 0.5f;

    /// <summary>
    /// Where a soldier stands at their post: in ranks in their hall's yard at drill, spread along the wall walk on the walls,
    /// two ranks at the hall's door as the mayor's guard. Null for the guard's round, which walks building to building.
    /// </summary>
    private Vector2? Soldiering(Town town, Agent a)
    {
        if (Army.Of(town, a) is not { } r || a.Activity is not (Activity.Drilling or Activity.Guarding) || a.LocationId is not int loc) return null;
        int rank = r.Soldiers.IndexOf(a.Id);
        if (rank < 0) return null;
        if (r.Post == ArmyPost.Walls && map.TownView.Walk is { } walk)
        {
            // Every stretch gets its share, the gate first.
            var arc = walk.Sections[rank % walk.Sections.Length];
            int along = rank / walk.Sections.Length, per = Math.Max(1, (r.Soldiers.Count + walk.Sections.Length - 1) / walk.Sections.Length);
            return Crowd.AlongArc(arc, (along + 0.5f) / per * 0.8f + 0.1f);
        }
        var lot = town.Lot(town.Building(loc).LotId);
        var f = Geo.LotFront(lot);
        var side = new Vector2(f.Y, -f.X);
        float w = lot.W / (float)Sim.Systems.Land.Res, d = lot.D / (float)Sim.Systems.Land.Res;
        if (a.Activity == Activity.Drilling)
        {
            // Earlier regiments of the hall take the front ranks.
            foreach (var other in town.Army.Regiments)
            {
                if (other == r) break;
                if (other.HallId == r.HallId && other.Post == ArmyPost.Reserve) rank += other.Soldiers.Count;
            }
            int cols = Math.Max(3, (int)(w * 0.8f / 0.2f));
            int row = rank / cols, col = rank % cols;
            return Geo.LotPos(lot) + f * (d / 2 - 0.15f - row * 0.22f) + side * ((col - (cols - 1) / 2f) * 0.2f);
        }
        // The mayor's guard: two ranks either side of the door.
        int half = (rank / 2) + 1;
        return Geo.LotPos(lot) + f * (d / 2 + 0.25f + (rank % 4 >= 2 ? 0.25f : 0)) + side * ((rank % 2 == 0 ? 1 : -1) * (0.3f + 0.22f * (half / 2)));
    }

    private Vector2 Front(Lot lot, float j1, float j2)
    {
        var f = Geo.LotFront(lot);
        float half = lot.W / (float)Sim.Systems.Land.Res / 2;
        return Geo.LotPos(lot) + f * (lot.D / (float)Sim.Systems.Land.Res / 2 + 0.12f + (j2 - 0.5f) * 0.08f) + new Vector2(f.Y, -f.X) * ((j1 - 0.5f) * 2 * half);
    }

    public void Advance(float dt, float timeScale)
    {
        var town = map.Sim.Town;
        float pace = timeScale <= 0 ? 0 : Pace * Mathf.Sqrt(timeScale);
        foreach (var a in town.Agents)
        {
            var tgt = Target(town, a);
            // Woken by raiders: they come running out of their own door.
            if (tgt != null && Passage.Fleeing(town, a) && (!Sprites.TryGetValue(a.Id, out var was) || !was.Visible) && (a.LocationId ?? a.HomeId) is int door)
                Sprites[a.Id] = new Sprite { P = Front(town.Lot(town.Building(door).LotId), 0.5f, 0.5f), Visible = true, Fleeing = true };
            if (Sprites.TryGetValue(a.Id, out var s)) s.Fleeing = tgt != null && Passage.Fleeing(town, a);
            Move(town, Sprites, a.Id, tgt, pace, dt);
        }
        foreach (var b in town.Buildings)
        {
            if (!b.IsSite && !Labourers.ContainsKey(b.Id)) continue;
            var (working, hauling) = Hand(town, b);
            Vector2? tgt = !working ? null : hauling && !Carrying(b) ? Yard(town, b) : Front(town.Lot(b.LotId), Geo.Hash(b.Id * 7 + 1), Geo.Hash(b.Id * 7 + 2));
            Move(town, Labourers, b.Id, tgt, pace, dt);
        }
    }

    /// <summary>The next point on the way to a target: by the gates and bridges, never through a wall or the moat but to flee.</summary>
    private static Vector2 Way(Town town, Sprite s, Vector2 goal)
    {
        if (s.Via is Vector2 via && s.For.DistanceSquaredTo(goal) < 1e-4f && s.P.DistanceSquaredTo(via) > 0.0025f) return via;
        var m = new Vector2(Geo.Margin, Geo.Margin);
        var (x, y) = Passage.Next(town, (s.P.X - m.X, s.P.Y - m.Y), (goal.X - m.X, goal.Y - m.Y), s.Fleeing);
        s.For = goal;
        s.Via = new Vector2((float)x, (float)y) + m;
        return s.Via.Value;
    }

    private static void Move(Town town, Dictionary<int, Sprite> sprites, int id, Vector2? tgt, float pace, float dt)
    {
        sprites.TryGetValue(id, out var s);
        if (tgt == null)
        {
            if (s != null) s.Visible = false;
            return;
        }
        if (s == null) sprites[id] = s = new Sprite { P = tgt.Value, Yaw = Geo.Hash(id) * Mathf.Tau };
        // Someone who reappears (back from the road, awake again) starts where they are meant to be.
        if (!s.Visible)
        {
            s.P = tgt.Value;
            s.Via = null;
        }
        s.Visible = true;
        var aim = Way(town, s, tgt.Value);
        var to = aim - s.P;
        float dist = to.Length();
        float step = Mathf.Max(pace, pace > 0 ? dist / 2.5f : 0) * dt;
        s.Walking = dist > 0.02f && step > 0;
        if (!s.Walking) return;
        if (dist > 30) s.P = tgt.Value;
        else if (step >= dist) s.P = aim;
        else s.P += to / dist * step;
        float want = Mathf.Atan2(to.X, to.Y);
        s.Yaw += Mathf.Clamp(Mathf.Wrap(want - s.Yaw, -Mathf.Pi, Mathf.Pi), -TurnRate * dt, TurnRate * dt);
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

    /// <summary>The heroes, in their own gear and moving with their own rig; and the sparks and spells of a fight.</summary>
    public readonly Heroes Heroes = new();

    /// <summary>Every creature, each kind its own model (<see cref="Map.Beasts"/>).</summary>
    public readonly Beasts Beasts = new();
    public readonly Fx Fx = new();
    private readonly MultiMeshInstance3D _folk = Make(), _heroes = Make(), _orcs = Make(), _banners = Make(), _guards = Make(), _loads = Make();
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
        kit.Clear();
        Load(kit);
        _loads.Multimesh.Mesh = kit.Build();
        BuildSiege(kit, _ram, _ladders, _arrows, _flames, _smoke);
        foreach (var m in new[] { _folk, _heroes, _orcs, _banners, _guards, _loads, _ram, _ladders, _arrows, _flames, _smoke }) AddChild(m);
        AddChild(Heroes);
        AddChild(Beasts);
        AddChild(Fx);
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

    /// <summary>A hauler's load, carried on the shoulder: a bundle of planks or a dressed stone (tinted per instance).</summary>
    private static void Load(MeshKit k)
    {
        k.Push(MeshKit.At(0, 0, 0, 0, 1.55f));
        for (int i = 0; i < 3; i++) k.Box(new Vector3(-0.045f + i * 0.03f, 0.235f, 0), new Vector3(0.026f, 0.022f, 0.2f), Colors.White);
        k.Box(new Vector3(0, 0.25f, 0.06f), new Vector3(0.1f, 0.012f, 0.012f), new Color("3a2c22"));
        k.Box(new Vector3(0, 0.25f, -0.06f), new Vector3(0.1f, 0.012f, 0.012f), new Color("3a2c22"));
        k.Pop();
    }

    /// <summary>A raider: hunched and broad, a spiked pauldron, tusks, a topknot and a heavy cleaver (the instance tints the jerkin; skin from instance data).</summary>
    private static void Orc(MeshKit k)
    {
        k.Push(MeshKit.At(0, 0, 0, 0, 1.6f));
        // The skin comes from the instance, so one figure serves every host (orc green by default).
        var skin = new Color(1, 1, 1, MeshKit.Flag.Skin);
        var iron = new Color("4a4644");
        k.Frustum(new Vector3(-0.034f, 0, 0), 0.028f, 0.026f, 0.075f, 5, new Color("3a2a20"));
        k.Frustum(new Vector3(0.034f, 0, 0), 0.028f, 0.026f, 0.075f, 5, new Color("3a2a20"));
        k.Frustum(new Vector3(0, 0.06f, 0), 0.07f, 0.088f, 0.12f, 6, Colors.White);
        k.Frustum(new Vector3(0, 0.07f, 0), 0.074f, 0.074f, 0.022f, 6, new Color("2e2018"));
        // Shoulders hunched forward, a bare green chest showing.
        k.Blob(new Vector3(0, 0.17f, 0.012f), new Vector3(0.09f, 0.04f, 0.07f), skin);
        k.Blob(new Vector3(0, 0.205f, 0.03f), new Vector3(0.046f, 0.044f, 0.048f), skin);
        // Tusks, a dark brow and a topknot.
        k.Frustum(new Vector3(-0.018f, 0.19f, 0.07f), 0.006f, 0, 0.022f, 3, new Color("f0e8d0"));
        k.Frustum(new Vector3(0.018f, 0.19f, 0.07f), 0.006f, 0, 0.022f, 3, new Color("f0e8d0"));
        k.Box(new Vector3(0, 0.215f, 0.068f), new Vector3(0.06f, 0.012f, 0.012f), new Color("2a2420"));
        k.Frustum(new Vector3(0, 0.24f, 0.02f), 0.014f, 0.006f, 0.05f, 4, new Color("1e1a18"));
        // One great spiked iron pauldron, one bare shoulder.
        k.Blob(new Vector3(0.085f, 0.18f, 0), new Vector3(0.042f, 0.032f, 0.042f), iron);
        k.Frustum(new Vector3(0.095f, 0.2f, 0), 0.012f, 0, 0.04f, 4, new Color("cfc8b8"));
        k.Blob(new Vector3(-0.08f, 0.165f, 0.01f), new Vector3(0.03f, 0.028f, 0.03f), skin);
        // Arms and a cleaver.
        k.Frustum(new Vector3(-0.088f, 0.07f, 0.03f), 0.022f, 0.026f, 0.1f, 5, skin);
        k.Frustum(new Vector3(0.09f, 0.07f, 0.02f), 0.022f, 0.026f, 0.1f, 5, skin);
        k.Box(new Vector3(-0.09f, 0.05f, 0.05f), new Vector3(0.018f, 0.2f, 0.018f), new Color("5a4030"));
        k.Box(new Vector3(-0.09f, 0.2f, 0.07f), new Vector3(0.016f, 0.08f, 0.06f), new Color("a8a8a0"));
        k.Pop();
    }

    public void Reset()
    {
        People.Reset();
        _sceneTick = -1;
    }

    private int _count;
    private float _clock;

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
        Beasts.Bind(town.Content);
        _content = town.Content;
        People.Advance(dt, map.TimeScale);
        Fx.Advance(dt);
        // The figures' own clock: frame time, so swings and strides keep pace in a recorded clip too.
        _clock = (_clock + dt) % 3600;
        float now = _clock;
        var gen = map.Gen;
        bool close = map.Distance < 260;
        Visible = close;
        if (!close) return;
        SiegeNow(town);
        SkirmishCast(town);

        // Townsfolk in one batch, heroes in another; haulers' loads in their own.
        var loads = new List<(Vector3 P, float Yaw, bool Stone)>();
        foreach (bool heroes in new[] { false, true })
        {
            foreach (var a in town.Agents)
            {
                if ((a.Hero != null) != heroes || !People.Sprites.TryGetValue(a.Id, out var s) || !s.Visible || (heroes && (Sallying(a.Id) || Skirmishing(a.Id))) || a.Activity == Activity.Marching || a.Hero?.Duty == HeroDuty.Field) continue;
                // Builders at a site rise and stoop to the hammer; everyone else bobs as they walk.
                float bob = s.Walking ? Mathf.Abs(Mathf.Sin(now * 8 + a.Id)) * 0.02f
                    : a.Activity == Activity.Building ? Mathf.Max(0, Mathf.Sin(now * 6 + a.Id)) * 0.025f : 0;
                var p = gen.At(s.P, bob);
                // Swimming the moat to get away: up to the shoulders in it.
                if (s.Fleeing && Swimming(town, s.P)) p.Y -= 0.14f;
                if (a.Hero != null)
                {
                    var (act, u) = Heroes.Moving(a.Id, s.Walking, now);
                    Heroes.Put(a, p - new Vector3(0, bob, 0), s.Yaw, act, u);
                }
                else if (a.RegimentId != null && Army.Of(town, a) is { } reg) _soldiers.Add((a, reg, s, p));
                else Put(p, s.Yaw, Tunics[a.Id % Tunics.Length], Palette.RaceColor(a.Race));
                if (People.Loaded(town, a) == true && a.LocationId is int site) loads.Add((p, s.Yaw, town.Building(site).Def.Stone > town.Building(site).Def.Timber));
            }
            // Heroes on the road and scouts join the heroes' batch below; street scenes join the townsfolk.
            if (!heroes)
            {
                foreach (var (id, s) in People.Labourers)
                {
                    if (!s.Visible) continue;
                    var site = town.Building(id);
                    float bob = s.Walking ? Mathf.Abs(Mathf.Sin(now * 8 + id)) * 0.02f : Mathf.Max(0, Mathf.Sin(now * 6 + id)) * 0.025f;
                    var p = gen.At(s.P, bob);
                    Put(p, s.Yaw, new Color("8a7a5a"), Palette.RaceColor("human"));
                    if (AgentPositions.Hand(town, site).Hauling && People.Carrying(site)) loads.Add((p, s.Yaw, site.Def.Stone > site.Def.Timber));
                }
                Street(town, now);
                ListsCrowd(town, now);
                Flush(_folk);
            }
        }
        foreach (var (lp, ly, stone) in loads) Put(lp, ly, stone ? Palette.Stone : new Color("c8a070"), Colors.White);
        Flush(_loads);

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
            if (StageDelve && p == town.Parties.First(x => !x.Returned)) (pos, pace) = (pin, Pace.Delving);
            if (pace == Pace.Delving)
            {
                DelveFight(town, p, pos, now);
                banners.Add((gen.At(pos + new Vector2(0, 1.6f)), 0, Palette.Gold));
                continue;
            }
            bool stopped = pace is Pace.Camped or Pace.Resting;
            for (int i = 0; i < p.Members.Count; i++)
            {
                var a = town.Agent(p.Members[i]);
                if (stopped)
                {
                    // Sitting round the fire, facing it.
                    float ang = i * Mathf.Tau / p.Members.Count + 0.4f;
                    var ring = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.55f;
                    Heroes.Put(a, gen.At(pos + ring), Mathf.Atan2(-ring.X, -ring.Y), Heroes.Act.Idle, now * 0.3f + a.Id * 0.17f);
                    continue;
                }
                var off = -dir * (0.35f + i / 2 * 0.32f) + side * ((i % 2) - 0.5f) * 0.3f;
                Heroes.Put(a, gen.At(pos + off), yaw, Heroes.Act.Walk, now * 1.7f + i * 0.31f);
            }
            banners.Add((gen.At(stopped ? pos + side * 0.9f : pos), yaw, Palette.Gold));
        }
        // Explorers, the same, under their green banner.
        foreach (var sv in town.Surveys)
        {
            if (!Explore.Out(sv)) continue;
            var (pos, moving) = Geo.SurveyAt(town, sv, map.HourFraction);
            if (map.Fog.AlphaAt(pos) >= Fog.SightAlpha) continue;
            var was = Geo.SurveyAt(town, sv, map.HourFraction - 0.5f).Pos;
            var dir = was.DistanceSquaredTo(pos) > 0.0004f ? (pos - was).Normalized() : (pos - Geo.Center).Normalized();
            float yaw = Mathf.Atan2(dir.X, dir.Y);
            var side = new Vector2(dir.Y, -dir.X);
            var members = sv.Members.Select(town.Agent).Where(a => !a.Dead).ToList();
            for (int i = 0; i < members.Count; i++)
            {
                var a = members[i];
                if (!moving)
                {
                    float ang = i * Mathf.Tau / members.Count + 0.4f;
                    var ring = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 0.55f;
                    Heroes.Put(a, gen.At(pos + ring), Mathf.Atan2(-ring.X, -ring.Y), Heroes.Act.Idle, now * 0.3f + a.Id * 0.17f);
                    continue;
                }
                var off = -dir * (0.35f + i / 2 * 0.32f) + side * ((i % 2) - 0.5f) * 0.3f;
                Heroes.Put(a, gen.At(pos + off), yaw, Heroes.Act.Walk, now * 1.7f + i * 0.31f);
            }
            banners.Add((gen.At(moving ? pos : pos + side * 0.9f), yaw, new Color("4f8a4a")));
        }
        foreach (var s in town.Scouts)
        {
            if (!s.Out || !map.Geo.Pins.TryGetValue(s.SiteId, out var pin)) continue;
            var pos = ScoutPos(town, s, pin);
            if (map.Fog.AlphaAt(pos) >= Fog.SightAlpha) continue;
            var a = town.Agent(s.HeroId);
            var dir = (pin - pos).Normalized();
            Heroes.Put(a, gen.At(pos), Mathf.Atan2(dir.X, dir.Y), Heroes.Act.Idle, now * 0.3f + a.Id * 0.17f);
        }
        ListsRiders(town, now);
        ListsBanners(town, now);
        SiegeHeroes(town, now);
        SkirmishHeroes(now);
        Flush(_heroes);
        Guards(town, now, banners);
        PutHorses();
        SiegeBanners(banners);
        foreach (var (bp, yaw, col) in banners) Put(bp, yaw + Mathf.Pi / 2, col, Colors.White, 2.2f);
        PutPennons();
        Flush(_banners);

        // The warband on the march, seen only once it is inside the light.
        var en = town.Enemy;
        if (en.Stage == ThreatStage.Marching && en.ArrivalTick is int arrival)
        {
            float total = town.Content.Enemy.MarchDays * SimTime.HoursPerDay;
            float t = Mathf.Clamp(1 - (arrival - town.Tick - map.HourFraction) / total, 0, 1);
            var from = map.Geo.Pins.TryGetValue(town.Content.Enemy.Outpost.Id, out var camp) ? camp : Geo.Center + new Vector2(60, -60);
            // They march by the fords and bridges like anyone else on foot.
            var road = Passage.Road(town, Geo.ToDays(from), (0, 0)).Select(Geo.FromDays).ToArray();
            var (pos, dir) = Along(road, t * 0.85f);
            int n = Math.Min(160, (int)Math.Round(en.Strength / 3));
            var host = Sim.Systems.Bestiary.Host(town, "warband");
            for (int i = 0; i < n; i++)
            {
                var o = new Vector2((Geo.Hash(i * 5 + 1) - 0.5f) * 7, (Geo.Hash(i * 5 + 2) - 0.5f) * 5);
                if (map.Fog.AlphaAt(pos + o) >= Fog.SightAlpha) continue;
                float bob = Mathf.Abs(Mathf.Sin(now * 8 + i)) * 0.03f;
                Foe("", gen.At(pos + o), Mathf.Atan2(dir.X, dir.Y), i % 7 == 0 ? new Color("4a2018") : new Color("8a3a2a"), OrcSkin, 1.1f, Heroes.Act.Walk, now * 1.7f + i * 0.37f, creature: Ranks(host, i, i == 0));
            }
        }
        SiegeHost(town, now);
        SkirmishFoes(now);
        GalleryPut(now);
        Wildlife(town, now);
        Beasts.Flush();
        Flush(_orcs);
        SiegeProps(town, now);
        Heroes.Flush(now);
    }

    /// <summary>Where each guard stands on the map this frame, for picking them out with the mouse.</summary>
    public readonly Dictionary<int, Vector2> GuardSpots = [];

    private static readonly Color Tabard = new("8a2a24"), OrcSkin = new("6b7a4e");

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
        Regiments(town, now);
        Hosts(town, now, banners);
        FieldPlay(town, now, banners);
        SiegeDefenders();
        Flush(_guards);
        foreach (var post in town.Garrison.Posts)
        {
            if (post.Kind != PostKind.Flag) continue;
            var at = new Vector2(Geo.Margin + (float)post.X, Geo.Margin + (float)post.Y);
            banners.Add((gen.At(at), Mathf.Sin(now * 0.7f + post.Id) * 0.4f, new Color("b8322a")));
        }
    }

    /// <summary>The town's soldiers seen this frame, drawn with the watch in their units' colours.</summary>
    private readonly List<(Agent A, Regiment R, Sprite S, Vector3 P)> _soldiers = [];

    private static readonly Dictionary<string, Color> UnitColours = [];

    private static Color UnitColour(UnitDef u) =>
        UnitColours.TryGetValue(u.Id, out var c) ? c : UnitColours[u.Id] = new Color(u.Colour);

    /// <summary>
    /// The town's regiments: soldiers in their unit's tabard, at drill lunging in their ranks, up on the wall walk, at the
    /// hall's door or on their round; horsemen in the saddle (and the Roost's riders on their griffons, coated gold).
    /// </summary>
    private void Regiments(Town town, float now)
    {
        var walk = map.TownView.Walk;
        foreach (var (a, r, s, p0) in _soldiers)
        {
            var u = Army.Unit(town, r);
            var p = p0;
            float yaw = s.Yaw;
            if (!s.Walking)
            {
                if (a.Activity == Activity.Drilling)
                {
                    // Facing the front of the yard, thrusting on the drillmaster's count.
                    var lot = town.Lot(town.Building(a.LocationId!.Value).LotId);
                    yaw = Geo.LotYaw(lot);
                    var f = Geo.LotFront(lot);
                    float lunge = Mathf.Max(0, Mathf.Sin(now * 3 + (a.Id % 3) * 0.2f)) * 0.05f;
                    p += new Vector3(f.X, 0, f.Y) * lunge;
                }
                else if (r.Post == ArmyPost.Walls && walk != null)
                {
                    p.Y += walk.Height;
                    var o = s.P - Geo.Center;
                    yaw = Mathf.Atan2(o.X, o.Y);
                }
            }
            bool mounted = u.Role is "horse" or "flier" or "beast";
            if (mounted)
            {
                _mounts.Add((p, yaw, u.Role == "flier" ? new Color("c89a3a") : u.Role == "beast" ? new Color("5a4a3a") : new Color(a.Id % 3 == 0 ? "d8d0c0" : a.Id % 3 == 1 ? "5a3a24" : "2a1e14")));
                p.Y += 0.15f;
            }
            Put(p, yaw, UnitColour(u), Palette.RaceColor(a.Race), 1.05f);
        }
        _soldiers.Clear();
    }

    /// <summary>
    /// Allied hosts camped before the main gate: ranks in their people's colours under a banner, side by side when more
    /// than one has come. A host that went over to the foe is gone from here with the treachery.
    /// </summary>
    private void Hosts(Town town, float now, List<(Vector3 P, float Yaw, Color C)> banners)
    {
        int n = 0;
        foreach (var c in town.Army.Calls)
        {
            if (c.State != CallState.Arrived) continue;
            var s = AllyCalls.Of(town, c);
            var walk = map.TownView.Walk;
            var gate = walk?.Gate ?? Geo.Center + Vector2.Down * (map.Geo.TownRadius + 1);
            var o = gate - Geo.Center;
            var outward = o.LengthSquared() > 1e-6f ? o.Normalized() : Vector2.Down;
            var side = outward.Orthogonal();
            // Hosts take turns either side of the road out.
            float lane = (n % 2 == 0 ? 1 : -1) * (2.2f + n / 2 * 3.4f);
            var at = gate + outward * 4.5f + side * lane;
            float yaw = Mathf.Atan2(-outward.X, -outward.Y);
            int count = (int)Math.Clamp(c.Strength / 3, 8, 48);
            int cols = 8;
            var body = HostColour(s);
            var skin = Palette.RaceColor(s.Kin.Race);
            for (int i = 0; i < count; i++)
            {
                int row = i / cols, col = i % cols;
                var p = at + side * ((col - (cols - 1) / 2f) * 0.32f) + outward * (row * 0.34f);
                p += new Vector2(Geo.Hash(c.Id * 131 + i) - 0.5f, Geo.Hash(c.Id * 71 + i) - 0.5f) * 0.08f;
                float sway = Mathf.Sin(now * 1.3f + i) * 0.04f;
                Put(map.Gen.At(p), yaw + sway, body, skin, 1.0f);
            }
            banners.Add((map.Gen.At(at - outward * 0.5f + side * 1.6f), Mathf.Sin(now * 0.7f + c.Id) * 0.4f, body.Lightened(0.15f)));
            n++;
        }
    }

    private static readonly Dictionary<string, Color> AllyColours = [];

    /// <summary>A people's colours, from its kind of place, the same each time.</summary>
    private static Color HostColour(Settlement s) =>
        AllyColours.TryGetValue(s.Kin.Id, out var c) ? c : AllyColours[s.Kin.Id] = s.Kin.Kind switch
        {
            "hold" => new Color("5a6a7a"),
            "enclave" => new Color("3f6b45"),
            "shire" => new Color("8a7a3a"),
            "burrow" => new Color("6a5a8a"),
            "hall" => new Color("9a5a3a"),
            "monastery" => new Color("c8c0a8"),
            _ => Color.FromHsv(Geo.Hash(s.Kin.Id.Sum(ch => (int)ch)), 0.45f, 0.55f),
        };

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

    /// <summary>Whether a point is in the moat's water (outside a wall, short of its far bank).</summary>
    private static bool Swimming(Town town, Vector2 p)
    {
        var q = (p.X - Geo.Margin, (double)p.Y - Geo.Margin);
        foreach (var b in Passage.Barriers(town))
            if (b.Reach > b.Foot && !Passage.Inside(b.Loop, q.Item1, q.Item2) && Passage.OnBarrier(b, q)) return true;
        return false;
    }

    /// <summary>A point a share of the way along a road, and which way it runs there.</summary>
    private static (Vector2 Pos, Vector2 Dir) Along(Vector2[] road, float share)
    {
        float total = 0;
        for (int i = 1; i < road.Length; i++) total += road[i].DistanceTo(road[i - 1]);
        float want = total * Mathf.Clamp(share, 0, 1);
        for (int i = 1; i < road.Length; i++)
        {
            float len = road[i].DistanceTo(road[i - 1]);
            if (want <= len || i == road.Length - 1)
                return (road[i - 1] + (road[i] - road[i - 1]) * (len > 0 ? Mathf.Min(1, want / len) : 0), (road[i] - road[i - 1]).Normalized());
            want -= len;
        }
        return (road[0], Vector2.Down);
    }

    /// <summary>A point a share of the way along a polyline.</summary>
    internal static Vector2 AlongArc(Vector2[] arc, float u)
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
