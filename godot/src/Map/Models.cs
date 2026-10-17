using Godot;

namespace CrownAndCompass.Client.Map;

/// <summary>One baked model: triangles and a colour (with a window flag) per triangle.</summary>
public sealed class Model(Vector3[] verts, Color[] colors)
{
    public readonly Vector3[] Verts = verts;
    public readonly Color[] Colors = colors;
    public int Triangles => Colors.Length;
}

/// <summary>
/// The Kenney CC0 kits (fantasy town, castle, nature), baked by tools/kenney_bake.py into
/// assets/models/kenney.bin: flat triangles with one colour each, so they merge freely with the
/// generated models into one vertex-coloured batch. Loaded once, on first use.
/// </summary>
public static class Models
{
    private static Dictionary<string, Model>? _all;

    public static Model Get(string name)
    {
        _all ??= Load();
        return _all.TryGetValue(name, out var m) ? m : throw new KeyNotFoundException($"No baked model {name}");
    }

    private static Dictionary<string, Model> Load()
    {
        var all = new Dictionary<string, Model>();
        using var f = Godot.FileAccess.Open("res://assets/models/kenney.bin", Godot.FileAccess.ModeFlags.Read);
        if (f == null) return all;
        var bytes = f.GetBuffer((long)f.GetLength());
        using var r = new BinaryReader(new MemoryStream(bytes));
        if (new string(r.ReadChars(4)) != "KNY1") return all;
        int count = (int)r.ReadUInt32();
        for (int i = 0; i < count; i++)
        {
            string name = System.Text.Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
            int tris = (int)r.ReadUInt32();
            var verts = new Vector3[tris * 3];
            for (int v = 0; v < verts.Length; v++) verts[v] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            var colors = new Color[tris];
            for (int t = 0; t < tris; t++)
            {
                byte cr = r.ReadByte(), cg = r.ReadByte(), cb = r.ReadByte(), flag = r.ReadByte();
                colors[t] = new Color(cr / 255f, cg / 255f, cb / 255f, flag == 1 ? MeshKit.Flag.Window : MeshKit.Flag.Plain);
            }
            all[name] = new Model(verts, colors);
        }
        return all;
    }
}
