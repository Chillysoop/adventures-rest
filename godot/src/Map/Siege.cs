using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// A siege played back on the walls over the hours after it was fought, from the town's record of it
/// (<see cref="Battles"/>): the host marches up from the side the scouts named, a ram pounds at the gate,
/// ladders go up where each wave hits and the archers on the wall walk and the towers loose into it. A wave that
/// breaks leaves its dead under the wall; one that breaches spills into the streets, and roofs by the breach
/// burn (only there, and only from the enemy). Then the heroes sally out to meet the leader, and the host
/// breaks, draws off, or raises its banner over the town hall. Everything is figures in the crowd's batches
/// and five more instanced meshes, laid out once per siege and placed each frame without allocating.
/// </summary>
public partial class Crowd
{
    private readonly MultiMeshInstance3D _ram = Make(), _ladders = Make(), _arrows = Make(), _flames = Make(), _smoke = Make();

    /// <summary>The siege playing now, if any, and where it is: set at the top of each frame.</summary>
    private Battle? _siege;
    private BattlePhase _phase;
    private int _wave;
    private float _u;

    /// <summary>The siege's lay of the land, worked out once when it starts playing (or the wall is redrawn).</summary>
    private sealed class SiegeLook
    {
        public int Id;
        public WallWalk? Walk;
        public float Height, TowerTop;
        public Vector2 Gate, GateOut, Out, Camp, From, Duel, Hall;
        public Vector2[] WaveAt = [], WaveOut = [];
        public Vector2[] Sally = [];
        public Vector2[] Fires = [];
        public int[] FireWave = [];
        public int Host;
        public Color Body, Skin, Chief;
    }

    private SiegeLook? _look;

    /// <summary>A vertex alpha that is no flag of the shader's and below its snow threshold: plain colour, never snowed on.</summary>
    private const float NoSnow = 0.9f;

    /// <summary>Torches the host carries this frame (filled by the host, drawn with the flames; kept to avoid allocating).</summary>
    private readonly List<Vector3> _torches = [];
    private bool _dark;
    private bool _siegeShown = true;

    private static void BuildSiege(MeshKit kit, MultiMeshInstance3D ram, MultiMeshInstance3D ladders, MultiMeshInstance3D arrows, MultiMeshInstance3D flames, MultiMeshInstance3D smoke)
    {
        // A ram under its hide-roofed shed on four wheels, the iron-capped log along +Z.
        kit.Clear();
        kit.Box(new Vector3(0, 0.08f, 0), new Vector3(0.42f, 0.05f, 0.95f), Palette.DarkTimber);
        foreach (float x in new[] { -0.23f, 0.23f })
            foreach (float z in new[] { -0.3f, 0.3f })
                kit.Box(new Vector3(x, 0, z), new Vector3(0.04f, 0.16f, 0.16f), new Color("3a2c22"));
        foreach (float x in new[] { -0.19f, 0.19f })
            foreach (float z in new[] { -0.4f, 0.4f })
                kit.Box(new Vector3(x, 0.13f, z), new Vector3(0.04f, 0.3f, 0.04f), Palette.DarkTimber);
        // Vertex alpha under the snow flag: wet hides and a moving shed carry no snow.
        kit.Push(MeshKit.At(0, 0.43f, 0, Mathf.Pi / 2));
        kit.Gable(0, 0.95f, 0.46f, 0.2f, new Color(new Color("a08058"), NoSnow), new Color(Palette.DarkTimber, NoSnow));
        kit.Pop();
        kit.Box(new Vector3(0, 0.22f, 0.12f), new Vector3(0.09f, 0.09f, 1.15f), Palette.Timber);
        kit.Box(new Vector3(0, 0.21f, 0.7f), new Vector3(0.12f, 0.11f, 0.08f), new Color("5a5a60"));
        ram.Multimesh.Mesh = kit.Build();

        // A siege ladder standing up the +Y axis, a unit long.
        kit.Clear();
        foreach (float x in new[] { -0.07f, 0.07f }) kit.Box(new Vector3(x, 0, 0), new Vector3(0.022f, 1, 0.022f), new Color("8a6239"));
        for (int i = 1; i < 8; i++) kit.Box(new Vector3(0, i / 8f, 0), new Vector3(0.14f, 0.016f, 0.016f), new Color("6a4a30"));
        ladders.Multimesh.Mesh = kit.Build();

        // An arrow in flight, a pale streak along Z so it reads against the ground at night too.
        kit.Clear();
        kit.Box(new Vector3(0, -0.006f, 0), new Vector3(0.012f, 0.012f, 0.24f), new Color("c8a878"));
        kit.Box(new Vector3(0, -0.009f, -0.1f), new Vector3(0.034f, 0.018f, 0.04f), new Color("f4f0e8"));
        arrows.Multimesh.Mesh = kit.Build();

        // Flames on a roof: glowing tongues, deep orange outside and yellow at the heart.
        kit.Clear();
        var ember = new Color("ff7a24", MeshKit.Flag.Ember);
        var heart = new Color("ffd25a", MeshKit.Flag.Ember);
        kit.Frustum(new Vector3(0, 0, 0), 0.2f, 0, 0.5f, 6, ember);
        kit.Frustum(new Vector3(0.13f, 0, 0.05f), 0.11f, 0, 0.34f, 5, ember);
        kit.Frustum(new Vector3(-0.1f, 0, -0.09f), 0.12f, 0, 0.38f, 5, ember);
        kit.Frustum(new Vector3(0, 0, 0), 0.1f, 0, 0.3f, 5, heart);
        flames.Multimesh.Mesh = kit.Build();

        // A puff of smoke.
        kit.Clear();
        kit.Blob(Vector3.Zero, new Vector3(0.2f, 0.17f, 0.2f), new Color(1, 1, 1, NoSnow), 0.15f, 3);
        smoke.Multimesh.Mesh = kit.Build();
        foreach (var m in new[] { ram, ladders, arrows, flames, smoke }) m.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        ram.CastShadow = GeometryInstance3D.ShadowCastingSetting.On;
    }

    /// <summary>How each kind of host looks: tunic, skin, and its leader's colours.</summary>
    private static (Color Body, Color Skin, Color Chief) HostColours(string kind) => kind switch
    {
        "undead" => (new Color("3a3a44"), new Color("cfc9b4"), new Color("1e1e26")),
        "cult" => (new Color("4a2a4a"), Palette.RaceColor("human"), new Color("2a102a")),
        "rivals" => (new Color("4a5a7a"), Palette.RaceColor("human"), new Color("2a3550")),
        "schemers" => (new Color("2e2e2e"), Palette.RaceColor("human"), new Color("161616")),
        "beasts" => (new Color("6a5038"), new Color("7a6040"), new Color("3a2818")),
        "dragon" => (new Color("6a2a1a"), new Color("8a4a2a"), new Color("8a1a10")),
        _ => (new Color("9a4430"), new Color("6b7a4e"), new Color("3a1610")),
    };

    /// <summary>A point a share of the way along a section of the outer wall (or where it would stand, with no wall yet).</summary>
    private Vector2 OnWall(Town town, WallWalk? walk, int section, float u)
    {
        if (walk != null) return AlongArc(walk.Sections[Math.Clamp(section, 0, walk.Sections.Length - 1)], u);
        // No wall yet: the edge of what is built.
        int n = Garrison.Sections(town);
        double a = Math.PI / 2 + (section + (u - 0.5) * 0.8) * 2 * Math.PI / n;
        float r = Mathf.Min((float)City.Wall(town, 0, a), map.Geo.TownRadius + 1);
        return Geo.Center + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * r;
    }

    private static Vector2 Outward(Vector2 p)
    {
        var o = p - Geo.Center;
        return o.LengthSquared() > 1e-6f ? o.Normalized() : Vector2.Down;
    }

    /// <summary>Lay the siege out on the map: where the host camps, where each wave strikes, the heroes' way out and which roofs burn.</summary>
    private SiegeLook Lay(Town town, Battle b, WallWalk? walk)
    {
        var l = new SiegeLook { Id = b.Id, Walk = walk, Height = walk?.Height ?? 0, TowerTop = walk?.TowerTop ?? 0 };
        (l.Body, l.Skin, l.Chief) = HostColours(b.Kind);
        l.Gate = walk?.Gate ?? OnWall(town, null, 0, 0.5f);
        l.GateOut = Outward(l.Gate);
        var front = OnWall(town, walk, b.Approach, 0.5f);
        l.Out = Outward(front);
        l.Camp = front + l.Out * 6.5f;
        l.From = front + l.Out * 24f;
        l.Duel = front + l.Out * 2.6f;
        l.Host = Math.Clamp(b.Strength / 3, 14, 150);
        l.WaveAt = new Vector2[b.Waves.Count];
        l.WaveOut = new Vector2[b.Waves.Count];
        for (int w = 0; w < b.Waves.Count; w++)
        {
            l.WaveAt[w] = b.Waves[w].Section == 0 ? l.Gate : OnWall(town, walk, b.Waves[w].Section, 0.5f);
            l.WaveOut[w] = Outward(l.WaveAt[w]);
        }
        var hall = town.Buildings.Where(x => x.Def.Id == "town_hall" && x.Complete).OrderBy(x => x.Id).FirstOrDefault();
        l.Hall = hall != null ? Geo.LotPos(town.Lot(hall.LotId)) : Geo.Center;

        // The heroes' way out: through the gate, then round outside the wall to the leader.
        var path = new List<Vector2> { l.Gate - l.GateOut * 1.6f, l.Gate + l.GateOut * 1.4f };
        float a0 = Mathf.Atan2(l.GateOut.Y, l.GateOut.X), a1 = Mathf.Atan2(l.Out.Y, l.Out.X);
        float da = Mathf.Wrap(a1 - a0, -Mathf.Pi, Mathf.Pi);
        int steps = (int)Mathf.Ceil(Mathf.Abs(da) / 0.2f);
        for (int i = 1; i <= steps; i++)
        {
            float a = a0 + da * i / steps;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var rim = walk != null ? TownView.RayHit(walk.Loop, dir) : Geo.Center + dir * Mathf.Min((float)City.Wall(town, 0, a), map.Geo.TownRadius + 1);
            path.Add(rim + dir * 2.2f);
        }
        path.Add(l.Duel + l.Out.Orthogonal() * 0.01f);
        l.Sally = [.. path];

        // Roofs that catch near each breach (the nearest few inside the wall), and round the hall if the town falls.
        var fires = new List<Vector2>();
        var waves = new List<int>();
        var lit = new HashSet<int>();
        void Burn(Vector2 at, int count, int wave)
        {
            foreach (var x in town.Buildings
                .Where(x => x.Complete && !lit.Contains(x.Id) && x.Def.Id != "town_hall")
                .Select(x => (B: x, P: Geo.LotPos(town.Lot(x.LotId))))
                .Where(x => x.P.DistanceTo(Geo.Center) < at.DistanceTo(Geo.Center) - 0.6f)
                .OrderBy(x => x.P.DistanceSquaredTo(at)).ThenBy(x => x.B.Id)
                .Take(count))
            {
                lit.Add(x.B.Id);
                var lot = town.Lot(x.B.LotId);
                fires.Add(x.P + new Vector2(Geo.Hash(x.B.Id * 3) - 0.5f, Geo.Hash(x.B.Id * 3 + 1) - 0.5f) * (float)(Math.Min(lot.W, lot.D) / Land.Res) * 0.4f);
                waves.Add(wave);
            }
        }
        for (int w = 0; w < b.Waves.Count; w++) if (b.Waves[w].Breached) Burn(l.WaveAt[w], 3, w);
        if (b.Lost) Burn(l.Hall, 5, -1);
        l.Fires = [.. fires];
        l.FireWave = [.. waves];
        return l;
    }

    /// <summary>Find the siege playing now and where it is; lay it out if it is new. Hero sprites in the sally stand down for it.</summary>
    private void SiegeNow(Town town)
    {
        double at = town.Tick + map.HourFraction;
        _siege = Battles.Playing(town, at);
        if (_siege == null)
        {
            _look = null;
            return;
        }
        (_phase, _wave, double u) = Battles.At(town, _siege, at);
        _u = (float)u;
        var walk = map.TownView.Walk;
        if (_look == null || _look.Id != _siege.Id || _look.Walk != walk) _look = Lay(town, _siege, walk);
    }

    /// <summary>Where to look at a siege now: the stretch under attack, the heroes' fight with the leader, or the breach and the square.</summary>
    public (Vector2 At, Vector2 Side) SiegeFocus(Town town, Battle b)
    {
        double at = town.Tick + map.HourFraction;
        var look = _look != null && _look.Id == b.Id ? _look : Lay(town, b, map.TownView.Walk);
        var (phase, wave, _) = Battles.At(town, b, at);
        if (Battles.Playing(town, at) != b || b.Waves.Count == 0) return (look.Duel, look.Out);
        return phase switch
        {
            BattlePhase.March => (look.Camp.Lerp(look.Duel, 0.6f), look.Out),
            BattlePhase.Waves => (look.WaveAt[wave] + look.WaveOut[wave] * 2.2f, look.WaveOut[wave]),
            _ when b.Lost => (look.WaveAt[^1].Lerp(look.Hall, 0.4f), look.WaveOut[^1]),
            _ => (look.Duel - look.Out * 0.6f, look.Out),
        };
    }

    /// <summary>Whether a hero's own sprite gives way to their figure in the sally.</summary>
    private bool Sallying(int id) => _siege != null && _phase is BattlePhase.Sally or BattlePhase.End && !_siege.Lost && _siege.Champions.Contains(id);

    private static float Ease(float x) => x * x * (3 - 2 * x);

    private static float Seg(float u, float a, float b) => Mathf.Clamp((u - a) / (b - a), 0, 1);

    /// <summary>One figure lying where it fell: the standing figure tipped over on its back.</summary>
    private void PutFallen(Vector3 p, float yaw, Color body, Color skin, float scale = 1)
    {
        var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromEuler(new Vector3(-Mathf.Pi / 2, 0, 0));
        PutXf(new Transform3D(basis.Scaled(new Vector3(scale, scale, scale)), p + new Vector3(0, 0.03f, 0)), body, skin);
    }

    private void PutXf(Transform3D xf, Color body, Color skin)
    {
        var b = xf.Basis;
        var p = xf.Origin;
        _buf.Add(b.X.X); _buf.Add(b.Y.X); _buf.Add(b.Z.X); _buf.Add(p.X);
        _buf.Add(b.X.Y); _buf.Add(b.Y.Y); _buf.Add(b.Z.Y); _buf.Add(p.Y);
        _buf.Add(b.X.Z); _buf.Add(b.Y.Z); _buf.Add(b.Z.Z); _buf.Add(p.Z);
        _buf.Add(body.R); _buf.Add(body.G); _buf.Add(body.B); _buf.Add(1);
        _buf.Add(skin.R); _buf.Add(skin.G); _buf.Add(skin.B); _buf.Add(1);
        _count++;
    }

    /// <summary>The heroes going out to meet the leader, and fighting round them (into the heroes' batch).</summary>
    private void SiegeHeroes(Town town, float now)
    {
        if (_siege is not { } b || _look is not { } l || b.Lost || _phase is not (BattlePhase.Sally or BattlePhase.End)) return;
        var gen = map.Gen;
        int n = b.Champions.Count;
        for (int k = 0; k < n; k++)
        {
            int id = b.Champions[k];
            if (id < 1 || id > town.Agents.Count) continue;
            var a = town.Agent(id);
            var body = Palette.HeroColor(town, a);
            var skin = Palette.RaceColor(a.Race);
            float ang = k * Mathf.Tau / Math.Max(1, n) + 0.5f;
            var ring = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            if (_phase == BattlePhase.Sally && _u < 0.45f)
            {
                // Out through the gate and round the wall, two by two.
                float s = Ease(_u / 0.45f) - k / 2 * 0.03f;
                var (pos, dir) = Along(l.Sally, Mathf.Max(0, s));
                var side = new Vector2(dir.Y, -dir.X) * ((k % 2) - 0.5f) * 0.3f;
                Put(gen.At(pos + side, Mathf.Abs(Mathf.Sin(now * 9 + k)) * 0.03f), Mathf.Atan2(dir.X, dir.Y), body, skin, 1.15f);
                continue;
            }
            bool fell = b.ChampionsFell.Contains(id);
            float r = 0.62f - 0.18f * Mathf.Max(0, Mathf.Sin(now * 5 + k * 1.9f));
            var spot = l.Duel + ring * (fell && _phase == BattlePhase.End ? 0.7f : r);
            float face = Mathf.Atan2(-ring.X, -ring.Y);
            if (fell && _phase == BattlePhase.End) PutFallen(gen.At(spot), face, body, skin, 1.15f);
            else Put(gen.At(spot, _phase == BattlePhase.Sally ? Mathf.Max(0, Mathf.Sin(now * 7 + k)) * 0.04f : 0), face, body, skin, 1.15f);
        }
    }

    /// <summary>The townsfolk of the militia on the stretch under attack, beside the watch (into the guards' batch).</summary>
    private void SiegeDefenders()
    {
        if (_siege is not { } b || _look is not { } l || l.Walk == null || _phase == BattlePhase.End) return;
        var town = map.Sim.Town;
        int section = _phase == BattlePhase.Waves ? b.Waves[_wave].Section : b.Approach;
        if (_phase == BattlePhase.Sally && b.Lost) return;
        int m = Math.Clamp(b.Defenders / 10, 4, 14);
        var militia = new Color("6a5a3a");
        for (int k = 0; k < m; k++)
        {
            var p = OnWall(town, l.Walk, section, (k + 0.5f) / m * 0.84f + 0.08f);
            Put(map.Gen.At(p, l.Height), Mathf.Atan2(Outward(p).X, Outward(p).Y), militia, Palette.RaceColor(k % 3 == 0 ? "dwarf" : "human"), 1.05f);
        }
    }

    /// <summary>The host's war banner over its camp, or over the town hall once the town has fallen.</summary>
    private void SiegeBanners(List<(Vector3 P, float Yaw, Color Color)> banners)
    {
        if (_siege is not { } b || _look is not { } l) return;
        if (b.Lost && _phase == BattlePhase.End) banners.Add((map.Gen.At(l.Hall, 1.4f), 0, new Color("b8221a")));
        else if (!(_phase == BattlePhase.End && b.End == BattleEnd.Slain && _u > 0.2f))
            banners.Add((map.Gen.At(l.Camp - l.Out * 0.8f + l.Out.Orthogonal() * 1.2f), Mathf.Atan2(l.Out.X, l.Out.Y), l.Chief.Lightened(0.15f)));
    }

    /// <summary>The host itself (into the orcs' batch): the camp, the wave at the wall, the dead it left and the leader.</summary>
    private void SiegeHost(Town town, float now)
    {
        _torches.Clear();
        if (_siege is not { } b || _look is not { } l) return;
        _dark = Geo.Nightness(town, map.HourFraction) > 0.25f;
        var gen = map.Gen;
        var side = l.Out.Orthogonal();
        float faceIn = Mathf.Atan2(-l.Out.X, -l.Out.Y);
        bool retreat = _phase == BattlePhase.End && !b.Lost;
        bool broken = retreat && b.End == BattleEnd.Slain;
        int wavesDone = _phase == BattlePhase.Waves ? _wave : b.Waves.Count;
        int inWave = _phase == BattlePhase.Waves ? Math.Clamp(b.Waves[_wave].Size / 3, 6, 60) : 0;

        // The camp: those not at the wall, drawn up facing it.
        int reserve = Math.Max(6, l.Host - inWave);
        for (int i = 0; i < reserve; i++)
        {
            float h1 = Geo.Hash(i * 7 + 1), h2 = Geo.Hash(i * 7 + 2);
            // Drawn up in ranks of sixteen, a little ragged.
            int rank = i / 16, file = i % 16;
            var o = side * ((file - 7.5f) * 0.36f + (h1 - 0.5f) * 0.12f) + l.Out * (rank * 0.42f + (h2 - 0.5f) * 0.12f);
            var p = l.Camp + o;
            float yaw = faceIn, bob = 0;
            if (_phase == BattlePhase.March)
            {
                // Up to the walls in the first hours of the night, then camped till dawn.
                float march = Mathf.Clamp(_u * 3.2f - h2 * 0.3f, 0, 1);
                p = l.From.Lerp(l.Camp, Ease(march)) + o;
                if (march < 1) bob = Mathf.Abs(Mathf.Sin(now * 8 + i)) * 0.03f;
            }
            else if (retreat)
            {
                // Broken, they scatter every way from the walls; drawn off, they march back the way they came.
                var away = broken ? l.Out.Rotated((h1 - 0.5f) * 2.2f) : l.Out;
                float go = Ease(Seg(_u, h2 * 0.25f, 1)) * (broken ? 18 + h1 * 8 : 16);
                if (go > 0.01f) bob = Mathf.Abs(Mathf.Sin(now * (broken ? 12 : 8) + i)) * 0.03f;
                p += away * go;
                yaw = go > 0.01f ? Mathf.Atan2(away.X, away.Y) : faceIn;
                if (b.End == BattleEnd.Slain && _u > 0.85f + h1 * 0.15f) continue;
            }
            else if (b.Lost && _phase is BattlePhase.Sally or BattlePhase.End)
            {
                // The town is theirs: the camp pours in at the breach and up to the square.
                var breach = l.WaveAt[^1];
                float s = _phase == BattlePhase.End ? 1 : Ease(Seg(_u, h2 * 0.4f, 0.6f + h2 * 0.4f));
                var inside = l.Hall + new Vector2(h1 - 0.5f, h2 - 0.5f) * 7;
                p = s < 0.5f ? p.Lerp(breach, s * 2) : breach.Lerp(inside, (s - 0.5f) * 2);
                yaw = Mathf.Atan2((inside - breach).X, (inside - breach).Y);
                bob = s < 1 ? Mathf.Abs(Mathf.Sin(now * 9 + i)) * 0.03f : 0;
            }
            Put(gen.At(p, bob), yaw, i % 9 == 0 ? l.Chief : l.Body, l.Skin, 1.1f);
            if (i % 6 == 0 && _dark) _torches.Add(gen.At(p + new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * 0.1f, bob + 0.5f));
        }

        // The dead each wave that broke left under the wall, and those who got in milling in the streets.
        for (int w = 0; w < wavesDone; w++) WaveFigures(town, b, l, w, 1, now, retreat);
        if (_phase == BattlePhase.Waves) WaveFigures(town, b, l, _wave, _u, now, false);

        // The ram's crew, whatever the wave.
        if (l.Walk != null && RamAt(out var ram, out _, now))
            for (int c = 0; c < 4; c++)
            {
                var o = l.GateOut.Orthogonal() * ((c % 2) - 0.5f) * 0.8f + l.GateOut * (0.5f + c / 2 * 0.4f);
                Put(gen.At(ram + o, Mathf.Abs(Mathf.Sin(now * 3 + c)) * 0.02f), Mathf.Atan2(-l.GateOut.X, -l.GateOut.Y), l.Body, l.Skin, 1.1f);
            }

        // The leader: before the camp, then out to meet the heroes, then dead, carried off or drawing off.
        var chief = l.Camp - l.Out * 1.2f;
        float cyaw = faceIn;
        if (_phase == BattlePhase.March) chief = l.From.Lerp(l.Camp, Ease(Mathf.Clamp(_u * 3.2f, 0, 1))) - l.Out * 1.2f;
        else if (_phase == BattlePhase.Sally && !b.Lost && b.Champions.Count > 0)
        {
            chief = chief.Lerp(l.Duel, Ease(Seg(_u, 0, 0.4f)));
            if (_u > 0.45f) cyaw = faceIn + Mathf.Sin(now * 1.3f) * 1.4f;
        }
        else if (_phase == BattlePhase.End && !b.Lost)
        {
            if (b.Champions.Count > 0) chief = l.Duel;
            if (b.End is BattleEnd.Slain or BattleEnd.Carried)
            {
                if (b.End == BattleEnd.Carried) chief += l.Out * Ease(Seg(_u, 0.3f, 1)) * 16;
                PutFallen(gen.At(chief), cyaw, l.Chief, l.Skin, 1.7f);
                return;
            }
            chief += l.Out * Ease(Seg(_u, 0.1f, 1)) * 16;
            cyaw = Mathf.Atan2(l.Out.X, l.Out.Y);
        }
        else if (b.Lost && _phase is BattlePhase.Sally or BattlePhase.End)
        {
            float s = _phase == BattlePhase.End ? 1 : Ease(_u);
            chief = s < 0.5f ? chief.Lerp(l.WaveAt[^1], s * 2) : l.WaveAt[^1].Lerp(l.Hall + new Vector2(0.9f, 0.9f), (s - 0.5f) * 2);
        }
        Put(gen.At(chief), cyaw, l.Chief, l.Skin, 1.7f);
    }

    /// <summary>One wave's figures at a share <paramref name="u"/> of its stretch: running in, at the ladders, then back or over.</summary>
    private void WaveFigures(Town town, Battle b, SiegeLook l, int w, float u, float now, bool retreat)
    {
        var gen = map.Gen;
        var wave = b.Waves[w];
        int n = Math.Clamp(wave.Size / 3, 6, 60);
        var outw = l.WaveOut[w];
        bool gate = wave.Section == 0;
        for (int i = 0; i < n; i++)
        {
            float h1 = Geo.Hash(w * 977 + i * 13 + 5), h2 = Geo.Hash(w * 977 + i * 13 + 6), h3 = Geo.Hash(w * 977 + i * 13 + 7);
            // At the gate they crowd either side of the ram, leaving it room to swing.
            var foot = gate
                ? l.Gate + l.GateOut * (0.5f + h2 * 1.8f) + l.GateOut.Orthogonal() * (h1 < 0.5f ? -1 : 1) * (0.45f + Mathf.Abs(h1 - 0.5f) * 2.4f)
                : OnWall(town, l.Walk, wave.Section, 0.1f + 0.8f * h1) + outw * (0.35f + h2 * 0.7f);
            var start = l.Camp + l.Out.Orthogonal() * (h1 - 0.5f) * 8 + l.Out * h2 * 2;
            bool dies = i % 3 == 0;
            bool climber = !gate && i % 5 == 1 && l.Walk != null;
            float bob = 0, lift = 0;
            Vector2 p;
            float yaw = Mathf.Atan2(-outw.X, -outw.Y);
            float run = Ease(Seg(u, h3 * 0.08f, 0.3f + h3 * 0.06f));
            p = start.Lerp(foot, run);
            if (run < 1) bob = Mathf.Abs(Mathf.Sin(now * 10 + i)) * 0.03f;
            if (wave.Breached)
            {
                // Over the wall or through the gate, and on into the streets.
                float over = Ease(Seg(u, 0.5f + h3 * 0.15f, 0.85f + h3 * 0.15f));
                var inside = (gate ? l.Gate : OnWall(town, l.Walk, wave.Section, 0.1f + 0.8f * h1)) - outw * (0.8f + h2 * 3.2f);
                if (over > 0)
                {
                    p = foot.Lerp(inside, over);
                    lift = gate ? 0 : l.Height * 1.2f * Mathf.Sin(Mathf.Pi * Mathf.Clamp(over * 1.6f, 0, 1));
                    bob = over < 1 ? Mathf.Abs(Mathf.Sin(now * 10 + i)) * 0.03f : Mathf.Max(0, Mathf.Sin(now * 6 + i)) * 0.03f;
                }
                if (retreat && over >= 1)
                {
                    // Cut off inside when the host draws off: they go back out the way they came.
                    float back = Ease(Seg(_u, 0, 0.6f));
                    p = inside.Lerp(foot + outw * 6, back);
                    yaw = Mathf.Atan2(outw.X, outw.Y);
                    if (_u > 0.65f + h1 * 0.3f) continue;
                }
            }
            else
            {
                if (climber && run >= 1) lift = l.Height * Ease(Seg(u, 0.35f, 0.62f)) * (u < 0.7f ? 1 : 0);
                if (u > 0.68f + h3 * 0.06f)
                {
                    if (dies)
                    {
                        PutFallen(gen.At(foot), yaw + h1 * 2, l.Body, l.Skin, 1.1f);
                        continue;
                    }
                    if (retreat || w < _wave || _phase != BattlePhase.Waves) continue;
                    float back = Ease(Seg(u, 0.7f, 1));
                    p = foot.Lerp(start, back);
                    yaw = Mathf.Atan2(outw.X, outw.Y);
                    bob = Mathf.Abs(Mathf.Sin(now * 10 + i)) * 0.03f;
                }
                else if (run >= 1 && !climber) bob = Mathf.Max(0, Mathf.Sin(now * 7 + i)) * 0.04f;
            }
            Put(gen.At(p, lift + bob), yaw, i % 9 == 0 ? l.Chief : l.Body, l.Skin, 1.1f);
            if (i % 7 == 2 && _dark) _torches.Add(gen.At(p + new Vector2(Mathf.Cos(yaw), -Mathf.Sin(yaw)) * 0.1f, lift + bob + 0.5f));
        }
    }

    /// <summary>Where the ram stands this frame (pulled up to the gate and swinging), and whether it is out at all.</summary>
    private bool RamAt(out Vector2 at, out float yaw, float now)
    {
        at = default;
        yaw = 0;
        if (_siege is not { } b || _look is not { } l || l.Walk == null) return false;
        if (_phase == BattlePhase.March && _u < 0.3f) return false;
        if (_phase == BattlePhase.End && !b.Lost && _u > 0.5f) return false;
        // Built in the camp in the night, and rolled up to the gate before dawn.
        float up = _phase == BattlePhase.March ? Ease(Seg(_u, 0.8f, 1)) : 1;
        var rest = l.Gate + l.GateOut * 0.8f;
        at = l.Camp.Lerp(rest, up);
        bool pounding = _phase == BattlePhase.Waves;
        if (pounding) at -= l.GateOut * 0.12f * Mathf.Max(0, Mathf.Sin(now * 3.2f));
        var face = up < 1 ? rest - l.Camp : -l.GateOut;
        yaw = Mathf.Atan2(face.X, face.Y);
        return true;
    }

    private static readonly Color[] Smokes = [new("5a5550"), new("78726a"), new("4a4642"), new("8a847c")];

    /// <summary>The siege's own meshes: the ram, the ladders, arrows in flight, and fire and smoke on roofs by a breach.</summary>
    private void SiegeProps(Town town, float now)
    {
        var gen = map.Gen;
        if (_siege is { } b && _look is { } l)
        {
            if (RamAt(out var ram, out float ryaw, now))
            {
                PutXf(new Transform3D(Basis.FromEuler(new Vector3(0, ryaw, 0)).Scaled(new Vector3(1.05f, 1.05f, 1.05f)), gen.At(ram)), Colors.White, Colors.White);
                // More war-wagons beside the first: those the guns or a sally stopped lie broken short of the gate.
                var side = l.GateOut.Orthogonal();
                for (int i = 1; i < b.Wagons; i++)
                {
                    bool through = i < b.WagonsThrough;
                    var at = ram + side * (i % 2 == 1 ? 0.9f : -0.9f) * ((i + 1) / 2) + (through ? Vector2.Zero : l.GateOut * (1.6f + i * 0.4f));
                    var tilt = through ? Vector3.Zero : new Vector3(0.25f, 0, 0.35f);
                    PutXf(new Transform3D(Basis.FromEuler(new Vector3(tilt.X, ryaw + (through ? 0 : 0.6f), tilt.Z)).Scaled(Vector3.One * 1.05f), gen.At(at)), Colors.White, Colors.White);
                }
            }
            Flush(_ram);

            // Ladders up each stretch a wave has come at, from the moment it reaches the wall.
            if (l.Walk != null && l.Height > 0.05f)
            {
                const float Tilt = 0.32f;
                float len = l.Height / Mathf.Cos(Tilt) + 0.12f;
                int last = _phase == BattlePhase.Waves ? _wave : b.Waves.Count - 1;
                for (int w = 0; w <= last; w++)
                {
                    if (b.Waves[w].Section == 0 || (_phase == BattlePhase.Waves && w == _wave && _u < 0.3f)) continue;
                    if (_phase == BattlePhase.End && !b.Lost && _u > 0.5f) break;
                    for (int k = 0; k < 3; k++)
                    {
                        var wp = OnWall(town, l.Walk, b.Waves[w].Section, 0.25f + k * 0.25f);
                        var o = Outward(wp);
                        var basis = Basis.FromEuler(new Vector3(0, Mathf.Atan2(o.X, o.Y), 0)) * Basis.FromEuler(new Vector3(-Tilt, 0, 0));
                        PutXf(new Transform3D(basis.Scaled(new Vector3(1, len, 1)), gen.At(wp + o * (l.Height * Mathf.Tan(Tilt) + 0.08f))), Colors.White, Colors.White);
                    }
                }
            }
            Flush(_ladders);

            // Arrows: from the wall walk and the towers into the wave, and a few back up at the wall.
            if (l.Walk != null && _phase is BattlePhase.Waves or BattlePhase.March && (_phase != BattlePhase.March || _u > 0.9f))
            {
                int section = _phase == BattlePhase.Waves ? b.Waves[_wave].Section : b.Approach;
                for (int k = 0; k < 44; k++)
                {
                    float h1 = Geo.Hash(k * 31 + 1), h2 = Geo.Hash(k * 31 + 2), h3 = Geo.Hash(k * 31 + 3);
                    var wallAt = OnWall(town, l.Walk, section, 0.08f + 0.84f * h1);
                    var o = Outward(wallAt);
                    var src = gen.At(wallAt, l.Height + 0.2f);
                    if (l.Walk != null && k % 5 == 0 && l.Walk.Towers.Length > 0)
                    {
                        // The nearest tower's archers.
                        var best = l.Walk.Towers[0];
                        foreach (var t in l.Walk.Towers) if (t.DistanceSquaredTo(wallAt) < best.DistanceSquaredTo(wallAt)) best = t;
                        if (best.DistanceTo(wallAt) < 7) src = gen.At(best, l.TowerTop + 0.25f);
                    }
                    var dst = gen.At(wallAt + o * (1.2f + h2 * 5) + o.Orthogonal() * (h3 - 0.5f) * 2, 0.05f);
                    if (k % 4 == 3) (src, dst) = (dst + Vector3.Up * 0.2f, gen.At(wallAt, l.Height + 0.1f));
                    float f = Mathf.PosMod(now * 0.85f + h3 * 7.3f + h1, 1);
                    float apex = src.DistanceTo(dst) * 0.22f;
                    var pos = src.Lerp(dst, f) + Vector3.Up * apex * 4 * f * (1 - f);
                    var vel = dst - src + Vector3.Up * apex * 4 * (1 - 2 * f);
                    if (vel.LengthSquared() < 1e-6f) continue;
                    PutXf(new Transform3D(Basis.LookingAt(vel.Normalized(), Mathf.Abs(vel.Normalized().Y) > 0.98f ? Vector3.Right : Vector3.Up), pos), Colors.White, Colors.White);
                }
            }
            Flush(_arrows);

            // The host's torches, then fire on the roofs by a breach, from the moment the wave gets over, and smoke rising off them.
            for (int i = 0; i < _torches.Count; i++)
            {
                float s = 0.2f + 0.03f * Mathf.Sin(now * 11 + i * 1.7f);
                PutXf(new Transform3D(Basis.FromEuler(new Vector3(0, i, 0)).Scaled(new Vector3(s, s * 1.3f, s)), _torches[i]), Colors.White, Colors.White);
            }
            _torches.Clear();
            // Campfires through the night before the walls.
            if (_phase == BattlePhase.March && _u > 0.3f)
                for (int i = 0; i < 5; i++)
                {
                    var at = l.Camp + l.Out.Orthogonal() * (i - 2) * 1.9f + l.Out * (1.4f + Geo.Hash(i * 5 + 3));
                    float s = 0.42f + 0.05f * Mathf.Sin(now * 8 + i * 2.3f);
                    PutXf(new Transform3D(Basis.FromEuler(new Vector3(0, now + i, 0)).Scaled(new Vector3(s, s, s)), gen.At(at)), Colors.White, Colors.White);
                }
            int lit = 0;
            for (int i = 0; i < l.Fires.Length; i++)
            {
                float since = FireSince(b, l.FireWave[i]);
                if (since <= 0) continue;
                lit++;
                float grow = Mathf.Clamp(since * 3, 0.2f, 1) * (0.85f + 0.2f * Mathf.Sin(now * 9 + i * 2.1f));
                PutXf(new Transform3D(Basis.FromEuler(new Vector3(0, now * 0.7f + i, 0)).Scaled(new Vector3(grow, grow * (1 + 0.15f * Mathf.Sin(now * 13 + i)), grow)), gen.At(l.Fires[i], 0.5f)), Colors.White, Colors.White);
            }
            Flush(_flames);
            for (int i = 0; i < l.Fires.Length; i++)
            {
                float since = FireSince(b, l.FireWave[i]);
                if (since <= 0) continue;
                for (int j = 0; j < 6; j++)
                {
                    float f = Mathf.PosMod(now * 0.16f + j / 6f + Geo.Hash(i * 11 + j) * 0.1f, 1);
                    var drift = new Vector2(0.6f, -0.35f) * f * f * 1.8f;
                    float s = (0.35f + f * 1.1f) * Mathf.Min(1, since * 2) * (1 - f * f * 0.6f);
                    var pos = gen.At(l.Fires[i] + drift, 0.8f + f * 2.6f);
                    PutXf(new Transform3D(Basis.FromEuler(new Vector3(0, j, 0)).Scaled(new Vector3(s, s, s)), pos), Smokes[(i + j) % Smokes.Length], Colors.White);
                }
            }
            Flush(_smoke);
            map.Firelight = lit > 0 ? Mathf.Min(1, 0.75f + lit / 12f) : 0.75f;
            _siegeShown = true;
            return;
        }
        if (!_siegeShown) return;
        _siegeShown = false;
        Flush(_ram);
        Flush(_ladders);
        Flush(_arrows);
        Flush(_flames);
        Flush(_smoke);
        map.Firelight = 0;
    }

    /// <summary>How long (in shares of a wave) a roof has burned: from halfway through its breaching wave, or from the town's fall.</summary>
    private float FireSince(Battle b, int wave)
    {
        if (wave < 0) return b.Lost && _phase is BattlePhase.Sally or BattlePhase.End ? (_phase == BattlePhase.End ? 1 : _u) : 0;
        if (_phase == BattlePhase.March) return 0;
        if (_phase == BattlePhase.Waves && _wave < wave) return 0;
        if (_phase == BattlePhase.Waves && _wave == wave) return _u - 0.55f;
        return 1;
    }
}
