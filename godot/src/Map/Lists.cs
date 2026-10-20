using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// The tourney grounds on tourney days (<see cref="Tournaments"/>): a fenced field with a tilt barrier down the
/// middle, two striped pavilions and a stand, or just the barrier on the arena's sand; banners all round; a crowd
/// along the fence that grows with the visitors; and the riders of the heat being ridden: two lances charging
/// down the tilt, archers loosing at the butts, two casters facing off in a ring of wardstones, or runners going
/// round. The others in the draw wait by the pavilions. Drawn only by day, in the batches the townsfolk use.
/// </summary>
public partial class Crowd
{
    private MeshInstance3D? _lists;
    private bool _listsArena;

    /// <summary>The jousters' horses, in a batch of their own (tinted per instance).</summary>
    private MultiMeshInstance3D? _horses;
    private readonly List<(Vector3 P, float Yaw, Color Coat)> _mounts = [];

    /// <summary>A horse, nose to +z: barrel, legs, neck, head and tail, its coat from the instance colour.</summary>
    private static void Horse(MeshKit k)
    {
        var coat = Colors.White;
        var dark = new Color("2a1e14");
        k.Box(new Vector3(0, 0.075f, 0), new Vector3(0.07f, 0.065f, 0.2f), coat);
        foreach (float x in new[] { -0.022f, 0.022f })
            foreach (float z in new[] { -0.075f, 0.075f })
                k.Box(new Vector3(x, 0, z), new Vector3(0.018f, 0.08f, 0.018f), coat);
        k.Box(new Vector3(0, 0.12f, 0.1f), new Vector3(0.04f, 0.075f, 0.045f), coat);
        k.Box(new Vector3(0, 0.17f, 0.125f), new Vector3(0.035f, 0.035f, 0.075f), coat);
        k.Box(new Vector3(0, 0.15f, 0.085f), new Vector3(0.012f, 0.06f, 0.03f), dark);
        k.Box(new Vector3(0, 0.07f, -0.11f), new Vector3(0.016f, 0.06f, 0.025f), dark);
        // A caparison in the rider's colours would be grander; a saddle cloth will do.
        k.Box(new Vector3(0, 0.105f, -0.01f), new Vector3(0.076f, 0.04f, 0.08f), new Color("b8322a"));
    }

    /// <summary>Put the horses gathered this frame in their batch (the buffer is empty between batches).</summary>
    private void PutHorses()
    {
        if (_horses == null)
        {
            var k = new MeshKit();
            Horse(k);
            _horses = new MultiMeshInstance3D
            {
                Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = k.Build() },
                MaterialOverride = Mats.People,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            };
            AddChild(_horses);
        }
        foreach (var (p, yaw, coat) in _mounts) Put(p, yaw, coat, Colors.White, 1.6f);
        _mounts.Clear();
        Flush(_horses);
    }

    /// <summary>The lists are up and someone is about: the tourney is on today, between first light and dusk.</summary>
    private static bool ListsUp(Town town) => Tournaments.On(town) && town.Hour >= 7 && town.Hour <= 20;

    /// <summary>The field's centre, its long and short axes (unit vectors on the map) and half its length and width in tiles.</summary>
    private static (Vector2 C, Vector2 L, Vector2 S, float Hl, float Hs) Field(Town town)
    {
        var st = town.Tourney;
        var c = new Vector2(Geo.Margin + (float)st.X, Geo.Margin + (float)st.Y);
        var l = new Vector2(Mathf.Cos((float)st.Yaw), Mathf.Sin((float)st.Yaw));
        var s = new Vector2(-l.Y, l.X);
        var g = Tournaments.Rules(town).Grounds;
        float hl = g.W / (float)Land.Res / 2, hs = g.D / (float)Land.Res / 2;
        if (st.Arena && town.Buildings.FirstOrDefault(b => b.Def.Id == "arena" && b.Complete) is { } arena)
        {
            var lot = town.Lot(arena.LotId);
            hl = Mathf.Min(lot.W, lot.D) / (float)Land.Res * 0.27f;
            hs = hl * 0.5f;
        }
        return (c, l, s, hl, hs);
    }

    /// <summary>The fence, the tilt, the pavilions and the stand, built once a tourney and set down at the grounds.</summary>
    private void ListsMesh(Town town)
    {
        var (c, l, _, hl, hs) = Field(town);
        bool arena = town.Tourney.Arena;
        if (_lists == null || _listsArena != arena)
        {
            _lists?.QueueFree();
            var k = new MeshKit();
            var rail = new Color("8a6a44");
            var post = new Color("5a4028");
            // The tilt: a painted barrier down the middle, red and white.
            int bays = (int)Mathf.Max(4, hl * 4);
            for (int i = 0; i < bays; i++)
            {
                float x = -hl * 0.85f + (i + 0.5f) * hl * 1.7f / bays;
                k.Box(new Vector3(x, 0, 0), new Vector3(hl * 1.7f / bays * 0.98f, 0.1f, 0.025f), i % 2 == 0 ? new Color("b8322a") : new Color("eee4cc"));
            }
            if (!arena)
            {
                // Posts and a rail round the field, the near long side left open behind the crowd's rope.
                foreach (float side in new[] { -1f, 1f })
                {
                    k.Box(new Vector3(0, 0.07f, side * hs), new Vector3(hl * 2, 0.025f, 0.02f), rail);
                    for (float x = -hl; x <= hl + 0.01f; x += 0.4f) k.Box(new Vector3(x, 0, side * hs), new Vector3(0.03f, 0.1f, 0.03f), post);
                }
                foreach (float end in new[] { -1f, 1f })
                {
                    k.Box(new Vector3(end * hl, 0.07f, 0), new Vector3(0.02f, 0.025f, hs * 2), rail);
                    // A striped pavilion at each end, in the two sides' colours.
                    var cloth = end < 0 ? new Color("2f4f8a") : new Color("9a2a24");
                    var at = new Vector3(end * (hl + 0.38f), 0, -hs * 0.45f);
                    k.Box(at, new Vector3(0.36f, 0.16f, 0.36f), new Color("eee4cc"));
                    k.Box(at + new Vector3(0, 0, 0.181f), new Vector3(0.1f, 0.14f, 0.004f), cloth);
                    k.Push(MeshKit.At(at.X, at.Y, at.Z));
                    k.Pyramid(0.16f, 0.36f, 0.36f, 0.2f, cloth);
                    k.Pop();
                    k.Box(at + new Vector3(0, 0.34f, 0), new Vector3(0.012f, 0.12f, 0.012f), post);
                }
                // The stand for the gentry along the far side: three tiers under an awning.
                var stand = new Vector3(0, 0, -hs - 0.42f);
                for (int t = 0; t < 3; t++) k.Box(stand + new Vector3(0, 0, -t * 0.1f), new Vector3(hl * 1.1f, 0.05f + t * 0.06f, 0.12f), new Color("7a5a3a"), new Color("9a7a52"));
                k.Box(stand + new Vector3(0, 0.36f, -0.1f), new Vector3(hl * 1.15f, 0.02f, 0.42f), new Color("b8322a"), new Color("d8b84a"));
                foreach (float x in new[] { -hl * 0.55f, hl * 0.55f })
                    k.Box(stand + new Vector3(x, 0, 0.1f), new Vector3(0.025f, 0.36f, 0.025f), post);
            }
            _lists = new MeshInstance3D { Mesh = k.Build(), MaterialOverride = Mats.Props, CastShadow = GeometryInstance3D.ShadowCastingSetting.On };
            _listsArena = arena;
            AddChild(_lists);
        }
        var gen = map.Gen;
        float yaw = Mathf.Atan2(l.Y, l.X);
        _lists.Transform = new Transform3D(Basis.FromEuler(new Vector3(0, -yaw, 0)), gen.At(c, arena ? 0.02f : 0));
        _lists.Visible = true;
    }

    private static readonly Color[] Colours = [new("e8b93a"), new("b8322a"), new("2f4f8a"), new("eee4cc"), new("4f8a4a")];

    /// <summary>The crowd along the fence (or round the arena's floor), facing in and cheering when a pass is ridden.</summary>
    private void ListsCrowd(Town town, float now)
    {
        if (!ListsUp(town))
        {
            if (_lists != null) _lists.Visible = false;
            return;
        }
        ListsMesh(town);
        var gen = map.Gen;
        var (c, l, s, hl, hs) = Field(town);
        int n = Math.Clamp(24 + town.Seasons.Tourists / 3, 30, 160);
        for (int i = 0; i < n; i++)
        {
            float u = Geo.Hash(i * 3 + 11), v = Geo.Hash(i * 3 + 12);
            Vector2 at, face;
            if (town.Tourney.Arena)
            {
                // Round the floor, on the stands' rising tiers.
                float a = u * Mathf.Tau, rr = 1.25f + v * 0.35f;
                var dir = l * Mathf.Cos(a) * hl + s * Mathf.Sin(a) * hl;
                at = c + dir * rr;
                face = -dir;
                float jump = Mathf.Max(0, Mathf.Sin(now * 5 + i)) * 0.02f;
                Put(gen.At(at, 0.04f + (rr - 1.25f) * 0.5f + jump), Mathf.Atan2(face.X, face.Y), Tunics[i % Tunics.Length], new Color("d9b48f"));
                continue;
            }
            // Two rows along the open near side, and a few at each end.
            if (i % 5 == 4)
            {
                float side = i % 2 == 0 ? -1 : 1;
                at = c + l * side * (hl + 0.15f + v * 0.3f) + s * (u - 0.2f) * hs * 1.4f;
                face = -l * side;
            }
            else
            {
                at = c + l * (u * 2 - 1) * hl * 0.95f + s * (hs + 0.14f + (i % 2) * 0.16f + v * 0.05f);
                face = -s;
            }
            float hop = Mathf.Max(0, Mathf.Sin(now * 4 + i * 1.3f)) * 0.025f;
            Put(gen.At(at, hop), Mathf.Atan2(face.X, face.Y), Tunics[i % Tunics.Length], new Color("d9b48f"));
        }
        // The gentry in the stand, under the awning.
        if (!town.Tourney.Arena)
            for (int i = 0; i < 9; i++)
            {
                var at = c + l * ((i - 4) * hl * 0.11f) + s * (-hs - 0.42f - (i % 3) * 0.1f);
                Put(gen.At(at, 0.05f + (i % 3) * 0.06f), Mathf.Atan2(s.X, s.Y), Colours[i % Colours.Length].Darkened(0.2f), new Color("e9c3a0"));
            }
    }

    private static Color RiderColor(Town town, Entrant e) => e.AgentId != null ? Palette.ClassColor(e.Class) : Palette.ClassColor(e.Class).Darkened(0.15f);

    private static Color RiderSkin(Town town, Entrant e) =>
        e.AgentId is int id ? Palette.RaceColor(town.Agent(id).Race)
        : e.SettlementId is int sid && town.Settlements.FirstOrDefault(x => x.Id == sid) is { } s ? Palette.RaceColor(s.Kin.Race) : Palette.RaceColor("human");

    /// <summary>The heat being ridden, by the event's own manner, and the rest of the draw waiting by the pavilions.</summary>
    private void ListsRiders(Town town, float now)
    {
        if (!ListsUp(town)) return;
        var st = town.Tourney;
        var e = st.Events.FirstOrDefault(x => x.Day == town.Day);
        if (e == null) return;
        var gen = map.Gen;
        var (c, l, s, hl, hs) = Field(town);
        // The pair: the heat due next, or the last one ridden once it is decided.
        int a, b;
        if (Tournaments.NextPair(e) is var (na, nb, _)) (a, b) = (na, nb);
        else if (e.Heats.Count > 0) (a, b) = (e.Heats[^1].A, e.Heats[^1].B);
        else return;
        var ra = Tournaments.Rider(town, a);
        var rb = Tournaments.Rider(town, b);
        bool between = town.Hour >= Tournaments.Rules(town).StartHour && town.Hour <= Tournaments.Rules(town).EndHour && !e.Done;
        float face(Vector2 d) => Mathf.Atan2(d.X, d.Y);
        switch (e.Def)
        {
            case "joust":
            {
                // Down the tilt and back, one charge every few seconds, lances (pennons) lowered.
                float t = between ? Mathf.PosMod(now / 3.2f, 1) : 0;
                float run = Mathf.SmoothStep(0, 1, Mathf.Clamp(t * 1.4f, 0, 1));
                float x = Mathf.Lerp(-hl * 0.85f, hl * 0.85f, run);
                var pa = c + l * x + s * 0.14f;
                var pb = c - l * x - s * 0.14f;
                float gallop = between && run < 1 ? Mathf.Abs(Mathf.Sin(now * 14)) * 0.04f : 0;
                Put(gen.At(pa, 0.15f + gallop), face(l), RiderColor(town, ra), RiderSkin(town, ra), 1.1f);
                Put(gen.At(pb, 0.15f + gallop), face(-l), RiderColor(town, rb), RiderSkin(town, rb), 1.1f);
                // The horses: a dark body under each rider.
                _mounts.Add((gen.At(pa, gallop), face(l), new Color("5a3a24")));
                _mounts.Add((gen.At(pb, gallop), face(-l), new Color("d8d0c0")));
                _pennons.Add((gen.At(pa, 0.17f), face(l) + Mathf.Pi / 2, RiderColor(town, ra), 0.7f));
                _pennons.Add((gen.At(pb, 0.17f), face(-l) + Mathf.Pi / 2, RiderColor(town, rb), 0.7f));
                break;
            }
            case "archery":
            {
                // Both at the near end, loosing at two butts at the far end.
                var line = c - l * hl * 0.8f;
                float draw = between ? Mathf.Max(0, Mathf.Sin(now * 2.2f)) * 0.02f : 0;
                Put(gen.At(line + s * 0.3f, draw), face(l), RiderColor(town, ra), RiderSkin(town, ra), 1.2f);
                Put(gen.At(line - s * 0.3f, draw), face(l), RiderColor(town, rb), RiderSkin(town, rb), 1.2f);
                foreach (float side in new[] { 0.3f, -0.3f })
                    _pennons.Add((gen.At(c + l * hl * 0.85f + s * side), face(-l) + Mathf.Pi / 2, new Color("eee4cc"), 0.6f));
                break;
            }
            case "duel":
            {
                // Face to face in a ring of wardstones that flicker as the spells fly.
                var pa = c + l * 0.55f;
                var pb = c - l * 0.55f;
                float sway = between ? Mathf.Sin(now * 3) * 0.06f : 0;
                Put(gen.At(pa + s * sway), face(-l), RiderColor(town, ra), RiderSkin(town, ra), 1.25f);
                Put(gen.At(pb - s * sway), face(l), RiderColor(town, rb), RiderSkin(town, rb), 1.25f);
                for (int i = 0; i < 8; i++)
                {
                    float ang = i * Mathf.Tau / 8;
                    var at = c + (l * Mathf.Cos(ang) + s * Mathf.Sin(ang) * 0.7f) * 1.05f;
                    var glow = (i + (int)(now * 4)) % 4 == 0 ? new Color("8ab4ff") : new Color("c8c0f0");
                    _pennons.Add((gen.At(at), ang, glow, 0.45f));
                }
                break;
            }
            default:
            {
                // Round and round the field, one a little ahead of the other.
                float speed = between ? 0.35f : 0;
                for (int k = 0; k < 2; k++)
                {
                    var r = k == 0 ? ra : rb;
                    float ang = now * speed + k * 0.35f;
                    var at = c + l * Mathf.Cos(ang) * hl * 0.8f + s * Mathf.Sin(ang) * hs * 0.75f;
                    var dir = -l * Mathf.Sin(ang) * hl + s * Mathf.Cos(ang) * hs;
                    float bob = between ? Mathf.Abs(Mathf.Sin(now * 12 + k)) * 0.03f : 0;
                    Put(gen.At(at, bob), face(dir), RiderColor(town, r), RiderSkin(town, r), 1.2f);
                }
                break;
            }
        }
        // The rest of the draw still in it, by the pavilions.
        var lost = e.Heats.Select(h => h.Winner == h.A ? h.B : h.A).ToHashSet();
        int w = 0;
        foreach (int id in e.Draw)
        {
            if (id == a || id == b || lost.Contains(id)) continue;
            var r = Tournaments.Rider(town, id);
            float end = r.AgentId != null ? -1 : 1;
            var at = c + l * end * (hl + 0.2f) + s * (-hs * 0.1f + (w % 3) * 0.18f) + l * end * (w / 3) * 0.18f;
            Put(gen.At(at), face(-l * end), RiderColor(town, r), RiderSkin(town, r), 1.15f);
            w++;
        }
    }

    /// <summary>The tourney's own pennons, smaller than a company's banner: put in the banners' batch after those.</summary>
    private readonly List<(Vector3 P, float Yaw, Color Color, float Scale)> _pennons = [];

    private void PutPennons()
    {
        foreach (var (p, yaw, col, scale) in _pennons) Put(p, yaw, col, Colors.White, scale);
        _pennons.Clear();
    }

    /// <summary>Banners all round the field (or the arena's floor), in the tourney's colours.</summary>
    private void ListsBanners(Town town, float now)
    {
        if (!ListsUp(town)) return;
        var gen = map.Gen;
        var (c, l, s, hl, hs) = Field(town);
        int i = 0;
        if (town.Tourney.Arena) return;
        for (float x = -hl; x <= hl + 0.01f; x += hl / 2, i++)
            _pennons.Add((gen.At(c + l * x - s * (hs + 0.02f)), Mathf.Sin(now * 0.8f + i) * 0.3f + Mathf.Atan2(l.X, l.Y), Colours[i % Colours.Length], 0.9f));
        foreach (float end in new[] { -1f, 1f })
            _pennons.Add((gen.At(c + l * end * (hl + 0.38f) - s * hs * 0.45f, 0.36f), Mathf.Sin(now + end) * 0.3f, end < 0 ? new Color("2f4f8a") : new Color("9a2a24"), 0.6f));
    }
}
