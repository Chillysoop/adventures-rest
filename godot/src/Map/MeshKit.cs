using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>
/// Builds low-poly, flat-shaded meshes from boxes, prisms, cones and blobs with vertex colours.
/// Every model in the game is generated here at load time, so the download stays tiny and every
/// asset is our own. Vertex alpha carries a material flag the shaders read (see <see cref="Flag"/>).
/// </summary>
public sealed class MeshKit
{
    /// <summary>Vertex-alpha flags: plain, a window that lights up at night, embers that always glow, magic that pulses, skin taken from instance data.</summary>
    public static class Flag
    {
        public const float Plain = 1f;
        public const float Window = 0.6f;
        public const float Ember = 0.3f;
        public const float Magic = 0.8f;
        public const float Skin = 0.45f;
    }

    private readonly List<Vector3> _v = [];
    private readonly List<Vector3> _n = [];
    private readonly List<Color> _c = [];
    private readonly Stack<Transform3D> _stack = new();
    public Transform3D Xf = Transform3D.Identity;

    public int Triangles => _v.Count / 3;

    public void Push(Transform3D t)
    {
        _stack.Push(Xf);
        Xf = Xf * t;
    }

    public void Pop() => Xf = _stack.Pop();

    public static Transform3D At(float x, float y, float z, float yaw = 0, float scale = 1) =>
        new(Basis.FromEuler(new Vector3(0, yaw, 0)).Scaled(new Vector3(scale, scale, scale)), new Vector3(x, y, z));

    public void Tri(Vector3 a, Vector3 b, Vector3 c, Color col)
    {
        a = Xf * a;
        b = Xf * b;
        c = Xf * c;
        var n = (c - a).Cross(b - a).Normalized();
        _v.Add(a);
        _v.Add(b);
        _v.Add(c);
        for (int i = 0; i < 3; i++)
        {
            _n.Add(n);
            _c.Add(col);
        }
    }

    public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
    {
        Tri(a, b, c, col);
        Tri(a, c, d, col);
    }

    /// <summary>An axis-aligned box from its base centre; the bottom face is left off.</summary>
    public void Box(Vector3 baseCenter, Vector3 size, Color col, Color? top = null)
    {
        float x0 = baseCenter.X - size.X / 2, x1 = baseCenter.X + size.X / 2;
        float z0 = baseCenter.Z - size.Z / 2, z1 = baseCenter.Z + size.Z / 2;
        float y0 = baseCenter.Y, y1 = baseCenter.Y + size.Y;
        var t = top ?? col;
        Quad(new(x0, y1, z0), new(x1, y1, z0), new(x1, y1, z1), new(x0, y1, z1), t);
        Quad(new(x0, y0, z1), new(x0, y1, z1), new(x1, y1, z1), new(x1, y0, z1), col);
        Quad(new(x1, y0, z0), new(x1, y1, z0), new(x0, y1, z0), new(x0, y0, z0), col);
        Quad(new(x0, y0, z0), new(x0, y1, z0), new(x0, y1, z1), new(x0, y0, z1), col);
        Quad(new(x1, y0, z1), new(x1, y1, z1), new(x1, y1, z0), new(x1, y0, z0), col);
    }

    /// <summary>
    /// A pitched roof over a w (x) by d (z) footprint at height y, ridge along x, with overhang:
    /// courses of shingles in alternating shades, a dark eave and a ridge cap, so it reads as a
    /// roof and not a slab from above.
    /// </summary>
    public void Gable(float y, float w, float d, float h, Color roof, Color gable, float over = 0.05f)
    {
        float x0 = -w / 2 - over, x1 = w / 2 + over, z1 = d / 2 + over;
        float drop = over * h / (d / 2);
        const int Courses = 5;
        foreach (float side in new[] { 1f, -1f })
        {
            // The sunward slope is drawn a touch lighter than the shaded one, for a painted look.
            var baseCol = side > 0 ? roof : roof.Darkened(0.1f);
            for (int i = 0; i < Courses; i++)
            {
                float t0 = (float)i / Courses, t1 = (float)(i + 1) / Courses;
                float za = side * z1 * (1 - t0), zb = side * z1 * (1 - t1);
                float ya = y - drop + (h + drop) * t0, yb = y - drop + (h + drop) * t1;
                var col = i == 0 ? baseCol.Darkened(0.22f) : i % 2 == 0 ? baseCol : baseCol.Darkened(0.06f);
                if (side > 0) Quad(new(x0, ya, za), new(x0, yb, zb), new(x1, yb, zb), new(x1, ya, za), col);
                else Quad(new(x1, ya, za), new(x1, yb, zb), new(x0, yb, zb), new(x0, ya, za), col);
            }
        }
        Box(new Vector3(0, y + h - 0.04f, 0), new Vector3(w + over * 2 + 0.02f, 0.08f, 0.1f), roof.Darkened(0.3f));
        Tri(new(-w / 2, y, d / 2), new(-w / 2, y + h, 0), new(-w / 2, y, -d / 2), gable);
        Tri(new(w / 2, y, -d / 2), new(w / 2, y + h, 0), new(w / 2, y, d / 2), gable);
    }

    /// <summary>A four-sided pyramid roof from a w by d base at height y.</summary>
    public void Pyramid(float y, float w, float d, float h, Color col, float over = 0.04f)
    {
        float x0 = -w / 2 - over, x1 = w / 2 + over, z0 = -d / 2 - over, z1 = d / 2 + over;
        var apex = new Vector3(0, y + h, 0);
        Tri(new(x0, y, z1), apex, new(x1, y, z1), col);
        Tri(new(x1, y, z1), apex, new(x1, y, z0), col.Darkened(0.06f));
        Tri(new(x1, y, z0), apex, new(x0, y, z0), col.Darkened(0.12f));
        Tri(new(x0, y, z0), apex, new(x0, y, z1), col.Darkened(0.04f));
    }

    /// <summary>A capped frustum (cylinder when r0 == r1, cone when r1 == 0) standing on y0.</summary>
    public void Frustum(Vector3 b, float r0, float r1, float h, int sides, Color col, Color? cap = null, float phase = 0)
    {
        for (int i = 0; i < sides; i++)
        {
            float a0 = phase + i * Mathf.Tau / sides, a1 = phase + (i + 1) * Mathf.Tau / sides;
            var p0 = b + new Vector3(Mathf.Cos(a0) * r0, 0, Mathf.Sin(a0) * r0);
            var p1 = b + new Vector3(Mathf.Cos(a1) * r0, 0, Mathf.Sin(a1) * r0);
            var q0 = b + new Vector3(Mathf.Cos(a0) * r1, h, Mathf.Sin(a0) * r1);
            var q1 = b + new Vector3(Mathf.Cos(a1) * r1, h, Mathf.Sin(a1) * r1);
            if (r1 <= 0.0001f) Tri(p1, p0, q0, col);
            else
            {
                Quad(p1, p0, q0, q1, col);
                Tri(b + new Vector3(0, h, 0), q1, q0, cap ?? col);
            }
        }
    }

    /// <summary>A log lying along x.</summary>
    public void Log(Vector3 c, float len, float r, Color bark, Color end)
    {
        Push(new Transform3D(Basis.FromEuler(new Vector3(0, 0, Mathf.Pi / 2)), c + new Vector3(len / 2, 0, 0)));
        Frustum(Vector3.Zero, r, r, len, 6, bark, end);
        Pop();
    }

    private static readonly Vector3[] Ico = MakeIco();
    private static readonly int[] IcoFaces =
    [
        0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
        3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
    ];

    private static Vector3[] MakeIco()
    {
        float t = (1 + Mathf.Sqrt(5)) / 2;
        Vector3[] v =
        [
            new(-1, t, 0), new(1, t, 0), new(-1, -t, 0), new(1, -t, 0), new(0, -1, t), new(0, 1, t),
            new(0, -1, -t), new(0, 1, -t), new(t, 0, -1), new(t, 0, 1), new(-t, 0, -1), new(-t, 0, 1),
        ];
        for (int i = 0; i < v.Length; i++) v[i] = v[i].Normalized();
        return v;
    }

    /// <summary>A faceted blob (icosahedron), squashed or stretched by scale; jitter roughens it.</summary>
    public void Blob(Vector3 c, Vector3 scale, Color col, float jitter = 0, int seed = 0, Color? shade = null)
    {
        var pts = new Vector3[Ico.Length];
        for (int i = 0; i < Ico.Length; i++)
        {
            float j = 1 + (Geo.Hash(seed * 31 + i) - 0.5f) * jitter;
            pts[i] = c + Ico[i] * scale * j;
        }
        for (int f = 0; f < IcoFaces.Length; f += 3)
        {
            var a = pts[IcoFaces[f]];
            var b = pts[IcoFaces[f + 1]];
            var d = pts[IcoFaces[f + 2]];
            // Faces on the underside take the shade colour, as if lit from above.
            bool under = (a.Y + b.Y + d.Y) / 3 < c.Y - scale.Y * 0.2f;
            Tri(a, d, b, under && shade is Color s ? s : col);
        }
    }

    /// <summary>A baked model under the current transform; recolour maps its palette onto ours.</summary>
    public void Add(Model m, Transform3D t, Func<Color, Color>? recolour = null)
    {
        Push(t);
        for (int i = 0; i < m.Triangles; i++)
        {
            var c = m.Colors[i];
            if (recolour != null) c = recolour(c) with { A = c.A };
            Tri(m.Verts[i * 3], m.Verts[i * 3 + 2], m.Verts[i * 3 + 1], c);
        }
        Pop();
    }

    public void Add(string model, Transform3D t, Func<Color, Color>? recolour = null) => Add(Models.Get(model), t, recolour);

    public ArrayMesh Build(ArrayMesh? into = null)
    {
        var mesh = into ?? new ArrayMesh();
        mesh.ClearSurfaces();
        if (_v.Count == 0) return mesh;
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = _v.ToArray();
        arrays[(int)Mesh.ArrayType.Normal] = _n.ToArray();
        arrays[(int)Mesh.ArrayType.Color] = _c.ToArray();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }

    public void Clear()
    {
        _v.Clear();
        _n.Clear();
        _c.Clear();
        Xf = Transform3D.Identity;
        _stack.Clear();
    }
}
