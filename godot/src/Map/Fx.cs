using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Combat effects: sparks where a blow lands, a caster's bolt flying to its mark and bursting, a ring of
/// light, motes rising from a healed hero. Short-lived glowing quads in one additive batch (fx.gdshader),
/// capped so a great battle costs no more than a small one. Purely for the eye: nothing here touches the sim,
/// and its randomness is <see cref="Geo.Hash"/>.
/// </summary>
public partial class Fx : Node3D
{
    public enum Shape { Orb, Spark, Ring, Streak }

    private struct Bit
    {
        public Vector3 P, V;
        public float Age, Life, Size, Grow, Fall;
        public Color C;
        public Shape Shape;

        // A bolt flies to Target and bursts there (an arrow just strikes sparks).
        public Vector3? Target;
        public bool Sparks;
    }

    private const int Cap = 1200;
    private readonly List<Bit> _bits = [];
    private readonly MultiMeshInstance3D _mmi = new()
    {
        Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = new QuadMesh { Size = Vector2.One } },
        MaterialOverride = Mats.Effects,
        CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        // Effects live round the heroes: never culled by a stale box.
        CustomAabb = new Aabb(new Vector3(-5000, -100, -5000), new Vector3(10000, 400, 10000)),
    };
    private readonly List<float> _buf = [];
    private int _seq;

    public override void _Ready() => AddChild(_mmi);

    private float Rand() => Geo.Hash(++_seq * 7919 + 13);

    private Vector3 RandDir(float up = 0.5f)
    {
        float a = Rand() * Mathf.Tau, y = Rand() * up + (1 - up) * 0.2f;
        return new Vector3(Mathf.Cos(a), y, Mathf.Sin(a)).Normalized();
    }

    private void Add(Bit b)
    {
        if (_bits.Count >= Cap) return;
        _bits.Add(b);
    }

    /// <summary>A blow landing: a white flash and a spray of sparks.</summary>
    public void Hit(Vector3 at, Color c, float size = 1)
    {
        Add(new Bit { P = at, Life = 0.16f, Size = 0.28f * size, Grow = 1.5f, C = Colors.White, Shape = Shape.Spark });
        for (int i = 0; i < 7; i++)
            Add(new Bit { P = at, V = RandDir(0.8f) * (0.9f + Rand() * 1.4f) * size, Life = 0.25f + Rand() * 0.2f, Size = 0.07f * size, C = c, Shape = Shape.Streak, Fall = 3 });
    }

    /// <summary>A spell let fly from one point to another; it bursts in its colour where it lands.</summary>
    public void Bolt(Vector3 from, Vector3 to, Color c, float speed = 4.5f)
    {
        float d = from.DistanceTo(to);
        Add(new Bit { P = from, V = (to - from) / Mathf.Max(0.05f, d) * speed, Life = d / speed, Size = 0.16f, C = c, Shape = Shape.Orb, Target = to });
        Add(new Bit { P = from, Life = 0.25f, Size = 0.2f, Grow = 1.2f, C = c, Shape = Shape.Spark });
    }

    /// <summary>A burst of magic: a flash, an expanding ring and embers.</summary>
    public void Burst(Vector3 at, Color c, float size = 1)
    {
        Add(new Bit { P = at, Life = 0.3f, Size = 0.35f * size, Grow = 1.2f, C = c.Lightened(0.4f), Shape = Shape.Orb });
        Add(new Bit { P = at, Life = 0.45f, Size = 0.2f * size, Grow = 2.4f, C = c, Shape = Shape.Ring });
        for (int i = 0; i < 9; i++)
            Add(new Bit { P = at, V = RandDir(0.9f) * (0.4f + Rand() * 0.9f) * size, Life = 0.4f + Rand() * 0.4f, Size = 0.06f * size, C = c, Shape = Shape.Spark, Fall = 1.2f });
    }

    /// <summary>Motes of light drifting up from someone being healed or blessed.</summary>
    public void Motes(Vector3 at, Color c, int n = 6)
    {
        for (int i = 0; i < n; i++)
        {
            var o = RandDir(0) * 0.12f;
            Add(new Bit { P = at + new Vector3(o.X, Rand() * 0.2f, o.Z), V = new Vector3(0, 0.35f + Rand() * 0.3f, 0), Life = 0.8f + Rand() * 0.5f, Size = 0.05f + Rand() * 0.04f, C = c, Shape = Shape.Spark });
        }
    }

    /// <summary>An arrow's flight: a quick pale streak.</summary>
    public void Arrow(Vector3 from, Vector3 to)
    {
        float d = from.DistanceTo(to);
        const float Speed = 9;
        Add(new Bit { P = from, V = (to - from) / Mathf.Max(0.05f, d) * Speed, Life = d / Speed, Size = 0.22f, C = new Color("fff2d0"), Shape = Shape.Streak, Target = to, Sparks = true });
    }

    public void Advance(float dt)
    {
        for (int i = _bits.Count - 1; i >= 0; i--)
        {
            var b = _bits[i];
            b.Age += dt;
            if (b.Age >= b.Life)
            {
                if (b.Target is Vector3 t)
                {
                    if (b.Sparks) Hit(t, new Color("ffd890"), 0.7f);
                    else Burst(t, b.C);
                }
                _bits.RemoveAt(i);
                continue;
            }
            b.P += b.V * dt;
            b.V.Y -= b.Fall * dt;
            b.V *= 1 - Mathf.Min(1, dt * 1.5f);
            _bits[i] = b;
        }
        foreach (var b in _bits)
        {
            float k = b.Age / b.Life;
            float size = b.Size * (1 + b.Grow * k);
            Basis x;
            if (b.Shape == Shape.Streak && b.V.LengthSquared() > 1e-6f)
            {
                var dir = b.V.Normalized();
                var side = dir.Cross(Vector3.Up).LengthSquared() > 1e-4f ? dir.Cross(Vector3.Up).Normalized() : Vector3.Right;
                x = new Basis(dir * size * 2.5f, side * size, dir.Cross(side) * size);
            }
            else x = Basis.Identity.Scaled(new Vector3(size, size, size));
            float fade = b.Target != null ? 1 : 1 - k * k;
            _buf.AddRange([x.X.X, x.Y.X, x.Z.X, b.P.X, x.X.Y, x.Y.Y, x.Z.Y, b.P.Y, x.X.Z, x.Y.Z, x.Z.Z, b.P.Z,
                b.C.R, b.C.G, b.C.B, fade, (int)b.Shape, b.Shape == Shape.Streak ? 0.18f : 1, 0, 0]);
        }
        var mm = _mmi.Multimesh;
        if (mm.InstanceCount != _bits.Count) mm.InstanceCount = _bits.Count;
        if (_bits.Count > 0) mm.Buffer = _buf.ToArray();
        _mmi.Visible = _bits.Count > 0;
        _buf.Clear();
    }

    public void Clear() => _bits.Clear();
}
