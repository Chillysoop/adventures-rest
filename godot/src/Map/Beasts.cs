using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Every creature on the map, each kind its own model (<see cref="BeastKit"/>), made the first time one is seen and kept.
/// One instanced batch per kind, whatever the head count, animated in the vertex shader (beastrig.gdshaderinc): put each
/// creature every frame with <see cref="Put"/>, then <see cref="Flush"/>.
/// </summary>
public partial class Beasts : Node3D
{
    /// <summary>What a creature is doing. People and golems swing their weapon as the heroes do.</summary>
    public enum Act { Idle, Move, Attack }

    private sealed class Kind(MultiMeshInstance3D mmi, Anatomy a)
    {
        public readonly MultiMeshInstance3D Mmi = mmi;
        public readonly Anatomy A = a;
        public readonly List<float> Buf = [];
        public int Count;
        public readonly float Code = BeastKit.GaitCode(a.Gait) * (BeastKit.OnHeroRig(a) ? 0 : 1) + 16 * BeastKit.StrikeCode(a.Strike) + 256 * Mathf.Clamp(Mathf.RoundToInt((float)a.Scale * 10), 1, 60);
        public readonly float Size = BeastKit.OnHeroRig(a) ? (float)a.Scale : 1;
        public readonly Heroes.Act Swing = a.Strike switch { "shoot" => Heroes.Act.Shoot, "cast" => Heroes.Act.Cast, _ when a.Weapon is "Spear" or "Rapier" or "Dagger" => Heroes.Act.Thrust, _ when a.Weapon is "Greatsword" or "Greataxe" or "Maul" or "Glaive" || a.Scale > 1.3 => Heroes.Act.Swing2, _ => Heroes.Act.Swing };
    }

    private readonly Dictionary<string, Kind> _kinds = [];
    private readonly List<Kind> _list = [];
    private readonly MeshKit _kit = new();
    private Content? _content;
    private ShaderMaterial _material = Mats.Beast;

    /// <summary>Creatures drawn in a world of their own (a panel): a copy of the material with no fog and the painted look.</summary>
    public void OwnMaterial(Texture2D noise)
    {
        var clear = ImageTexture.CreateFromImage(Image.CreateEmpty(1, 1, false, Image.Format.Rgba8));
        _material = (ShaderMaterial)Mats.Beast.Duplicate();
        _material.SetShaderParameter("fog_tex", clear);
        _material.SetShaderParameter("noise_tex", noise);
        _material.SetShaderParameter("night", 0.0);
        _material.SetShaderParameter("strategic", 0.0);
        _material.SetShaderParameter("toon", 1.0);
    }

    public void Bind(Content content) => _content = content;

    /// <summary>How a creature is built (null before <see cref="Bind"/>).</summary>
    public Anatomy? Of(string id) => _content == null ? null : Bestiary.Of(_content, id);

    private Kind KindOf(string id)
    {
        if (_kinds.TryGetValue(id, out var had)) return had;
        var a = Bestiary.Of(_content!, id);
        // Game ids, bosses and library ids that come out the same creature share one batch.
        if (_kinds.TryGetValue(a.Id, out var same))
        {
            _kinds[id] = same;
            return same;
        }
        BeastKit.Build(_kit, a);
        var mmi = new MultiMeshInstance3D
        {
            Multimesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, UseColors = true, UseCustomData = true, Mesh = _kit.Build() },
            MaterialOverride = _material,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
        };
        AddChild(mmi);
        var k = new Kind(mmi, a);
        _kinds[id] = k;
        _kinds[a.Id] = k;
        _list.Add(k);
        Triangles += _kit.Triangles;
        return k;
    }

    /// <summary>Triangles in every model made so far (for --bench).</summary>
    public int Triangles { get; private set; }

    /// <summary>Kinds made so far.</summary>
    public int Made => _list.Count;

    /// <summary>
    /// A creature at xf (yaw and the map's figure scale; its own size is the model's), doing act at phase u of its
    /// cycle. host is the colour of the host it marches with (its cloth and war paint); chief glows; flash is a hit landing.
    /// </summary>
    public void Put(string id, Transform3D xf, Act act, float u, Color? host = null, bool chief = false, float flash = 0)
    {
        if (_content == null) return;
        var k = KindOf(id);
        var a = k.A;
        if (k.Size != 1) xf = xf with { Basis = xf.Basis.Scaled(Vector3.One * k.Size) };
        if (chief) xf = xf with { Basis = xf.Basis.Scaled(Vector3.One * 1.25f) };
        float anim;
        if (BeastKit.OnHeroRig(a))
        {
            var hact = act switch { Act.Move => Heroes.Act.Walk, Act.Attack => k.Swing, _ => Heroes.Act.Idle };
            anim = (int)hact + Mathf.Clamp(u - Mathf.Floor(u), 0, 0.998f);
        }
        else anim = (int)act + Mathf.Clamp(u - Mathf.Floor(u), 0, 0.998f);
        var c = host ?? new Color("#" + a.Accent).Darkened(0.4f);
        float packed = Mathf.RoundToInt(c.R * 255) * 65536 + Mathf.RoundToInt(c.G * 255) * 256 + Mathf.RoundToInt(c.B * 255);
        float g = (chief ? 1 : 0) + Mathf.Clamp(flash, 0, 1) * 0.95f;
        var x = xf.Basis;
        var p = xf.Origin;
        k.Buf.AddRange([x.X.X, x.Y.X, x.Z.X, p.X, x.X.Y, x.Y.Y, x.Z.Y, p.Y, x.X.Z, x.Y.Z, x.Z.Z, p.Z, 1, 1, 1, 1, packed, g, k.Code, anim]);
        k.Count++;
    }

    /// <summary>A creature fallen on its side (dead or dying).</summary>
    public void Fallen(string id, Vector3 p, float yaw, float scale, Color? host = null, bool chief = false)
    {
        var basis = Basis.FromEuler(new Vector3(0, yaw, 0)) * Basis.FromEuler(new Vector3(0, 0, Mathf.Pi / 2));
        Put(id, new Transform3D(basis.Scaled(Vector3.One * scale), p + new Vector3(0, 0.03f, 0)), Act.Idle, 0, host, chief);
    }

    /// <summary>How high above the ground a creature goes: fliers up in the air, swimmers at the surface, the rest on their feet.</summary>
    public float Lift(string id, bool moving)
    {
        var a = Of(id);
        if (a == null) return 0;
        return a.Gait switch { "fly" => (moving ? 1.2f : 0.9f) * Mathf.Sqrt((float)a.Scale), "hover" => 0.12f, _ => 0 };
    }

    /// <summary>Sends this frame's creatures to the GPU and starts the next frame empty.</summary>
    public void Flush()
    {
        foreach (var k in _list)
        {
            var mm = k.Mmi.Multimesh;
            if (mm.InstanceCount != k.Count) mm.InstanceCount = k.Count;
            if (k.Count > 0) mm.Buffer = k.Buf.ToArray();
            k.Mmi.Visible = k.Count > 0;
            k.Buf.Clear();
            k.Count = 0;
        }
    }
}
