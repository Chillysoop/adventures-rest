using System.Collections;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrownAndCompass.Sim;

/// <summary>What a save slot shows without loading the whole campaign.</summary>
public sealed record SaveHeader(int Version, string Town, int Seed, int Day, int Hour, int Population, double Treasury, string Saved);

/// <summary>Leaves a field out of saves (listeners, caches); on load it gets its initial value.</summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotSavedAttribute : Attribute;

/// <summary>A save that cannot be read: damaged, from a newer build, or naming content that is gone.</summary>
public sealed class SaveException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Saves and loads a whole campaign. A save holds every field of the simulation, so a campaign
/// reloaded mid-game plays on exactly as if it had never stopped (rng state included).
/// <para>
/// The file is a magic line, a one-line JSON header for save slots, then gzipped JSON of the state.
/// The state is written by reflection over fields, so new sim state is saved without touching this
/// file. Content (buildings, classes, monsters, library entries) is not copied: references to it
/// are written as keys (<c>ClassDef:fighter</c>) and resolved against the loaded content. Objects
/// referenced from more than one place keep their identity. A field that must not be saved (a
/// listener, a cache) is marked <c>[NotSaved]</c>; a delegate anywhere else is an error.
/// </para>
/// </summary>
public static class SaveGame
{
    /// <summary>Bump when a change makes older saves unreadable; older versions are refused with a message.</summary>
    public const int Version = 3;

    private static readonly byte[] Magic = "CROWN&COMPASS SAVE\n"u8.ToArray();

    /// <summary>
    /// Take the campaign's state now. This is the only step that reads the sim, so it runs between
    /// steps; compressing and writing the snapshot can happen on another thread while play goes on.
    /// <paramref name="client"/> is the client's own data (map markers and the like), kept as given.
    /// </summary>
    public static SaveSnapshot Capture(Simulation sim, string? client = null, DateTime? saved = null)
    {
        var t = sim.Town;
        var header = new SaveHeader(Version, t.Name, t.Seed, t.Day, t.Hour, t.Population, t.Treasury, (saved ?? DateTime.UtcNow).ToString("O"));
        var ms = new MemoryStream();
        new StateWriter(ContentIndex.Of(t.Content)).Write(ms, sim, client);
        return new SaveSnapshot(header, ms.ToArray());
    }

    public static byte[] Write(Simulation sim, DateTime? saved = null) => Capture(sim, null, saved).ToBytes();

    public static void WriteFile(string path, Simulation sim, string? client = null) => Capture(sim, client).WriteFile(path);

    public static SaveHeader ReadHeader(Stream stream)
    {
        var magic = new byte[Magic.Length];
        if (stream.ReadAtLeast(magic, magic.Length, throwOnEndOfStream: false) < magic.Length || !magic.AsSpan().SequenceEqual(Magic))
            throw new SaveException("This is not a Crown & Compass save.");
        var line = new MemoryStream();
        for (int b = stream.ReadByte(); b != '\n'; b = stream.ReadByte())
        {
            if (b < 0) throw new SaveException("The save is cut short.");
            line.WriteByte((byte)b);
        }
        return JsonSerializer.Deserialize<SaveHeader>(line.ToArray()) ?? throw new SaveException("The save has no header.");
    }

    public static SaveHeader ReadHeader(string path)
    {
        using var fs = File.OpenRead(path);
        return ReadHeader(fs);
    }

    public static Simulation Read(Stream stream, Content? content = null) => Read(stream, out _, content);

    public static Simulation Read(Stream stream, out string? client, Content? content = null)
    {
        var header = ReadHeader(stream);
        if (header.Version > Version) throw new SaveException("This save was made by a newer version of the game.");
        if (header.Version < Version) throw new SaveException("This save was made by an older version of the game and can no longer be read.");
        try
        {
            using var gz = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
            using var doc = JsonDocument.Parse(gz);
            var root = doc.RootElement;
            client = root.TryGetProperty("client", out var c) ? c.GetString() : null;
            return (Simulation)new StateReader(ContentIndex.Of(content ?? Content.Default()), root).Read(typeof(Simulation));
        }
        catch (Exception e) when (e is not SaveException)
        {
            throw new SaveException($"The save is damaged ({e.Message}).", e);
        }
    }

    public static Simulation Read(byte[] data, Content? content = null) => Read(new MemoryStream(data), content);

    public static Simulation ReadFile(string path, out string? client, Content? content = null)
    {
        using var fs = File.OpenRead(path);
        return Read(fs, out client, content);
    }

    /// <summary>The state alone, uncompressed: what the round-trip tests compare.</summary>
    public static byte[] StateJson(Simulation sim) => Capture(sim).State;

    internal static void WriteTo(Stream stream, SaveHeader header, byte[] state)
    {
        stream.Write(Magic);
        stream.Write(JsonSerializer.SerializeToUtf8Bytes(header));
        stream.WriteByte((byte)'\n');
        using var gz = new GZipStream(stream, CompressionLevel.Fastest, leaveOpen: true);
        gz.Write(state);
    }
}

/// <summary>A campaign's state taken at one moment, ready to compress and write from any thread.</summary>
public sealed class SaveSnapshot(SaveHeader header, byte[] state)
{
    public SaveHeader Header { get; } = header;

    /// <summary>The state as uncompressed JSON.</summary>
    public byte[] State { get; } = state;

    public void WriteTo(Stream stream) => SaveGame.WriteTo(stream, Header, State);

    public byte[] ToBytes()
    {
        var ms = new MemoryStream();
        WriteTo(ms);
        return ms.ToArray();
    }

    /// <summary>Write through a temporary file, so a crash mid-save never ruins the slot.</summary>
    public void WriteFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        string tmp = path + ".tmp";
        using (var fs = File.Create(tmp)) WriteTo(fs);
        File.Move(tmp, path, overwrite: true);
    }
}

/// <summary>How the save layer sees a type: which shape to write it in, and which fields an object has.</summary>
internal enum Shape
{
    Scalar,
    Enum,
    Nullable,
    Array,
    List,
    Set,
    Map,
    OrderedMap,

    /// <summary>Any other collection: saved as a list, so only behind an interface like IReadOnlyList.</summary>
    Sequence,
    Object,
}

internal sealed class TypeInfo
{
    public required Type Type { get; init; }
    public required Shape Shape { get; init; }

    /// <summary>Element type for sequences, key type for maps, underlying type for nullables.</summary>
    public Type? Arg { get; init; }

    /// <summary>Value type for maps.</summary>
    public Type? Arg2 { get; init; }

    public Dictionary<string, FieldInfo> Fields { get; init; } = [];
    public FieldInfo[] Ordered { get; init; } = [];

    /// <summary>Fields left out of saves, filled fresh on load.</summary>
    public FieldInfo[] Skipped { get; init; } = [];

    private static readonly ConcurrentDictionary<Type, TypeInfo> Cache = new();

    public static TypeInfo Of(Type t) => Cache.GetOrAdd(t, Build);

    public static bool IsScalar(Type t) =>
        t.IsPrimitive || t == typeof(string) || t == typeof(decimal);

    /// <summary>The concrete type a field of declared type d is read back as.</summary>
    public static Type Natural(Type d)
    {
        if (!d.IsInterface) return d;
        if (!d.IsGenericType) return d;
        var g = d.GetGenericTypeDefinition();
        var a = d.GetGenericArguments();
        if (g == typeof(IReadOnlySet<>) || g == typeof(ISet<>)) return typeof(HashSet<>).MakeGenericType(a);
        if (g == typeof(IReadOnlyDictionary<,>) || g == typeof(IDictionary<,>)) return typeof(Dictionary<,>).MakeGenericType(a);
        if (g == typeof(IReadOnlyList<>) || g == typeof(IList<>) || g == typeof(IEnumerable<>) || g == typeof(IReadOnlyCollection<>) || g == typeof(ICollection<>))
            return typeof(List<>).MakeGenericType(a);
        return d;
    }

    public static bool IsSequenceInterface(Type d) => d.IsInterface && Natural(d) != d;

    private static TypeInfo Build(Type t)
    {
        if (IsScalar(t)) return new TypeInfo { Type = t, Shape = Shape.Scalar };
        if (t.IsEnum) return new TypeInfo { Type = t, Shape = Shape.Enum };
        if (Nullable.GetUnderlyingType(t) is Type u) return new TypeInfo { Type = t, Shape = Shape.Nullable, Arg = u };
        if (t.IsArray) return new TypeInfo { Type = t, Shape = Shape.Array, Arg = t.GetElementType() };
        if (typeof(Delegate).IsAssignableFrom(t))
            throw new InvalidOperationException($"A {t.Name} cannot be saved: mark the field holding it [NotSaved].");
        if (t.IsGenericType)
        {
            var g = t.GetGenericTypeDefinition();
            var a = t.GetGenericArguments();
            if (g == typeof(List<>)) return new TypeInfo { Type = t, Shape = Shape.List, Arg = a[0] };
            if (g == typeof(HashSet<>)) return new TypeInfo { Type = t, Shape = Shape.Set, Arg = a[0] };
            if (g == typeof(Dictionary<,>)) return new TypeInfo { Type = t, Shape = Shape.Map, Arg = a[0], Arg2 = a[1] };
            if (g == typeof(OrderedMap<,>)) return new TypeInfo { Type = t, Shape = Shape.OrderedMap, Arg = a[0], Arg2 = a[1] };
        }
        if (t.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>)) is Type seq)
            return new TypeInfo { Type = t, Shape = Shape.Sequence, Arg = seq.GetGenericArguments()[0] };

        var fields = new List<FieldInfo>();
        var skipped = new List<FieldInfo>();
        for (var c = t; c != null && c != typeof(object) && c != typeof(ValueType); c = c.BaseType)
            foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                (f.IsDefined(typeof(NotSavedAttribute)) ? skipped : fields).Add(f);
        return new TypeInfo
        {
            Type = t,
            Shape = Shape.Object,
            Ordered = [.. fields],
            Fields = fields.ToDictionary(Name),
            Skipped = [.. skipped],
        };
    }

    /// <summary>Saved names drop the compiler's decoration: <c>&lt;Hp&gt;k__BackingField</c> is saved as <c>Hp</c>.</summary>
    public static string Name(FieldInfo f)
    {
        string n = f.Name;
        if (n.StartsWith('<'))
        {
            int end = n.IndexOf('>');
            if (end > 1) return n[1..end];
        }
        return n;
    }
}

/// <summary>
/// Every object reachable from a Content, keyed so a save can name it. Objects with an Id are keyed
/// by type and id (<c>BuildingDef:farm</c>); the rest by their path from the nearest keyed object.
/// </summary>
internal sealed class ContentIndex
{
    private static readonly ConditionalWeakTable<Content, ContentIndex> Cache = [];

    public Dictionary<object, string> Keys { get; } = new(ReferenceEqualityComparer.Instance);
    public Dictionary<string, object> Objects { get; } = [];

    public static ContentIndex Of(Content content) => Cache.GetValue(content, c => new ContentIndex(c));

    private ContentIndex(Content content)
    {
        var queue = new Queue<(object Obj, string Key)>();
        Add(content, "Content", queue);
        while (queue.Count > 0)
        {
            var (obj, key) = queue.Dequeue();
            Visit(obj, key, queue);
        }
    }

    private void Add(object obj, string pathKey, Queue<(object, string)> queue)
    {
        if (Keys.ContainsKey(obj)) return;
        string key = IdKey(obj) is string k && !Objects.ContainsKey(k) ? k : pathKey;
        if (Objects.ContainsKey(key)) return;
        Keys[obj] = key;
        Objects[key] = obj;
        queue.Enqueue((obj, key));
    }

    private static string? IdKey(object obj)
    {
        var p = obj.GetType().GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
        if (p == null || p.GetIndexParameters().Length > 0) return null;
        var v = p.GetValue(obj);
        return v is string or int ? $"{obj.GetType().Name}:{v}" : null;
    }

    private void Visit(object obj, string key, Queue<(object, string)> queue)
    {
        var t = obj.GetType();
        if (obj is string || t.IsPrimitive || t.IsEnum || typeof(Delegate).IsAssignableFrom(t)) return;
        if (obj is IDictionary dict)
        {
            foreach (DictionaryEntry e in dict) Child(e.Value, $"{key}[{e.Key}]", queue);
            return;
        }
        if (obj is IEnumerable seq)
        {
            int i = 0;
            foreach (var e in seq) Child(e, $"{key}[{i++}]", queue);
            return;
        }
        for (var c = t; c != null && c != typeof(object); c = c.BaseType)
            foreach (var f in c.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                Child(f.GetValue(obj), $"{key}.{TypeInfo.Name(f)}", queue);
    }

    private void Child(object? v, string key, Queue<(object, string)> queue)
    {
        if (v == null) return;
        var t = v.GetType();
        if (v is string || t.IsValueType || typeof(Delegate).IsAssignableFrom(t)) return;
        Add(v, key, queue);
    }
}

/// <summary>Writes the object graph as JSON. A first pass finds objects referenced twice, so only they carry ids.</summary>
internal sealed class StateWriter(ContentIndex content)
{
    private readonly Dictionary<object, int> _seen = new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<object> _shared = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, int> _ids = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<Type, int> _typeIds = [];
    private readonly List<Type> _types = [];

    public void Write(Stream stream, object root, string? client)
    {
        Count(root);
        using var w = new Utf8JsonWriter(stream);
        w.WriteStartObject();
        w.WritePropertyName("state");
        Value(w, root, root.GetType());
        w.WriteStartArray("types");
        foreach (var t in _types) w.WriteStringValue(TypeName(t));
        w.WriteEndArray();
        if (client != null) w.WriteString("client", client);
        w.WriteEndObject();
    }

    private static readonly Regex AssemblyDetail = new(@", (Version|Culture|PublicKeyToken)=[^,\]]+", RegexOptions.Compiled);

    private static string TypeName(Type t) => AssemblyDetail.Replace(t.AssemblyQualifiedName!, "");

    /// <summary>Objects whose identity a save keeps. Arrays and other read-only sequences are copied
    /// (the sim shares one empty array everywhere, and they come back as lists anyway).</summary>
    private static bool Tracked(object v) =>
        v is not string && !v.GetType().IsValueType && TypeInfo.Of(v.GetType()).Shape is not (Shape.Array or Shape.Sequence);

    /// <summary>First pass: count references to each object (content aside).</summary>
    private void Count(object root)
    {
        var stack = new Stack<object>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var obj = stack.Pop();
            foreach (var child in Children(obj))
            {
                if (child == null) continue;
                if (!Tracked(child))
                {
                    if (child is not string && TypeInfo.Of(child.GetType()).Shape is not (Shape.Scalar or Shape.Enum)) stack.Push(child);
                    continue;
                }
                if (content.Keys.ContainsKey(child)) continue;
                if (_seen.TryGetValue(child, out int n))
                {
                    _seen[child] = n + 1;
                    _shared.Add(child);
                    continue;
                }
                _seen[child] = 1;
                stack.Push(child);
            }
        }
    }

    private static IEnumerable<object?> Children(object obj)
    {
        var info = TypeInfo.Of(obj.GetType());
        switch (info.Shape)
        {
            case Shape.Scalar or Shape.Enum or Shape.Nullable:
                yield break;
            case Shape.Map:
                foreach (DictionaryEntry e in (IDictionary)obj)
                {
                    yield return e.Key;
                    yield return e.Value;
                }
                yield break;
            case Shape.OrderedMap:
                foreach (var kv in (IEnumerable)obj)
                {
                    var (k, v) = Pair(kv);
                    yield return k;
                    yield return v;
                }
                yield break;
            case Shape.Array or Shape.List or Shape.Set or Shape.Sequence:
                foreach (var e in (IEnumerable)obj) yield return e;
                yield break;
            default:
                foreach (var f in info.Ordered) yield return f.GetValue(obj);
                yield break;
        }
    }

    private static (object? Key, object? Value) Pair(object kv)
    {
        var t = kv.GetType();
        return (t.GetProperty("Key")!.GetValue(kv), t.GetProperty("Value")!.GetValue(kv));
    }

    /// <summary>Start an object, with its id if it is referenced again later and its type if the field's type does not say it.</summary>
    private void Open(Utf8JsonWriter w, object v, bool shared, bool typed)
    {
        w.WriteStartObject();
        if (shared)
        {
            _ids[v] = _ids.Count + 1;
            w.WriteNumber("$i", _ids[v]);
        }
        if (typed) w.WriteNumber("$t", TypeId(v.GetType()));
    }

    private static IEnumerable<DictionaryEntry> Entries(IDictionary d)
    {
        var e = d.GetEnumerator();
        while (e.MoveNext()) yield return e.Entry;
    }

    private static IEnumerable<T> Sorted<T>(IEnumerable<T> items, Func<T, object> key) =>
        items.OrderBy(key, Comparer<object>.Create((a, b) => a is string x && b is string y ? string.CompareOrdinal(x, y) : Comparer<object>.Default.Compare(a, b)));

    private int TypeId(Type t)
    {
        if (_typeIds.TryGetValue(t, out int id)) return id;
        _typeIds[t] = _types.Count;
        _types.Add(t);
        return _types.Count - 1;
    }

    private void Value(Utf8JsonWriter w, object? v, Type declared)
    {
        if (v == null)
        {
            w.WriteNullValue();
            return;
        }
        var t = v.GetType();
        switch (v)
        {
            case string s: w.WriteStringValue(s); return;
            case bool b: w.WriteBooleanValue(b); return;
            case int i: w.WriteNumberValue(i); return;
            case double d:
                if (double.IsFinite(d)) w.WriteNumberValue(d);
                else w.WriteStringValue(d.ToString(System.Globalization.CultureInfo.InvariantCulture));
                return;
            case Enum e: w.WriteStringValue(e.ToString()); return;
        }
        if (TypeInfo.IsScalar(t))
        {
            switch (v)
            {
                case long l: w.WriteNumberValue(l); return;
                case uint u: w.WriteNumberValue(u); return;
                case ulong ul: w.WriteNumberValue(ul); return;
                case float f: w.WriteNumberValue(f); return;
                case char c: w.WriteStringValue(c.ToString()); return;
                default: w.WriteRawValue(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!); return;
            }
        }

        if (content.Keys.TryGetValue(v, out var key))
        {
            w.WriteStartObject();
            w.WriteString("$c", key);
            w.WriteEndObject();
            return;
        }
        if (_ids.TryGetValue(v, out int refId))
        {
            w.WriteStartObject();
            w.WriteNumber("$r", refId);
            w.WriteEndObject();
            return;
        }

        var info = TypeInfo.Of(t);
        var natural = TypeInfo.Natural(Nullable.GetUnderlyingType(declared) ?? declared);
        bool sequence = info.Shape is Shape.Array or Shape.List or Shape.Set or Shape.Sequence;
        // Behind an interface any sequence (a collection expression's hidden type, an array) is read back as the natural list.
        bool typed = t != natural && !(sequence && TypeInfo.IsSequenceInterface(declared));
        if (typed && info.Shape == Shape.Sequence)
            throw new InvalidOperationException($"The save layer does not know how to save a {t.Name}; use a List, HashSet, Dictionary or OrderedMap.");
        bool shared = _shared.Contains(v);
        bool wrapped = shared || typed;

        if (info.Shape == Shape.Object)
        {
            Open(w, v, shared, typed);
            foreach (var f in info.Ordered)
            {
                w.WritePropertyName(TypeInfo.Name(f));
                Value(w, f.GetValue(v), f.FieldType);
            }
            w.WriteEndObject();
            return;
        }

        if (wrapped)
        {
            Open(w, v, shared, typed);
            w.WritePropertyName("$v");
        }
        w.WriteStartArray();
        switch (info.Shape)
        {
            case Shape.Map:
                // Dictionaries and sets are written in key order: the sim only looks things up in them
                // (order that matters lives in an OrderedMap), and a canonical save can be compared byte for byte.
                foreach (var e in Sorted(Entries((IDictionary)v), e => e.Key))
                {
                    w.WriteStartArray();
                    Value(w, e.Key, info.Arg!);
                    Value(w, e.Value, info.Arg2!);
                    w.WriteEndArray();
                }
                break;
            case Shape.OrderedMap:
                foreach (var kv in (IEnumerable)v)
                {
                    var (k, val) = Pair(kv);
                    w.WriteStartArray();
                    Value(w, k, info.Arg!);
                    Value(w, val, info.Arg2!);
                    w.WriteEndArray();
                }
                break;
            case Shape.Set:
                foreach (var e in Sorted(((IEnumerable)v).Cast<object>(), e => e)) Value(w, e, info.Arg!);
                break;
            default:
                foreach (var e in (IEnumerable)v) Value(w, e, info.Arg!);
                break;
        }
        w.WriteEndArray();
        if (wrapped) w.WriteEndObject();
    }
}

/// <summary>Reads the graph back, rebuilding shared references and resolving content keys.</summary>
internal sealed class StateReader(ContentIndex content, JsonElement root)
{
    private readonly Dictionary<int, object> _ids = [];
    private readonly Type[] _types = root.GetProperty("types").EnumerateArray().Select(e => ResolveType(e.GetString()!)).ToArray();

    private static Type ResolveType(string name) =>
        Type.GetType(name) ?? throw new SaveException($"The save names a kind of thing this version of the game does not have ({name.Split(',')[0]}).");

    public object Read(Type type) => Value(root.GetProperty("state"), type)!;

    private object? Value(JsonElement e, Type declared)
    {
        if (e.ValueKind == JsonValueKind.Null) return null;
        var info = TypeInfo.Of(declared);
        switch (info.Shape)
        {
            case Shape.Nullable:
                return Value(e, info.Arg!);
            case Shape.Enum:
                return Enum.Parse(declared, e.GetString()!);
            case Shape.Scalar:
                return Scalar(e, declared);
        }

        if (e.ValueKind == JsonValueKind.Object)
        {
            if (e.TryGetProperty("$c", out var c))
            {
                string key = c.GetString()!;
                return content.Objects.TryGetValue(key, out var obj)
                    ? obj
                    : throw new SaveException($"The save refers to {key}, which this version of the game no longer has.");
            }
            if (e.TryGetProperty("$r", out var r)) return _ids[r.GetInt32()];
        }

        var type = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("$t", out var tid) ? _types[tid.GetInt32()] : TypeInfo.Natural(declared);
        info = TypeInfo.Of(type);
        int? id = e.ValueKind == JsonValueKind.Object && e.TryGetProperty("$i", out var iid) ? iid.GetInt32() : null;

        if (info.Shape == Shape.Object) return Object(e, info, id);

        var items = e.ValueKind == JsonValueKind.Object ? e.GetProperty("$v") : e;
        int n = items.GetArrayLength();
        switch (info.Shape)
        {
            case Shape.Array:
            {
                var arr = Array.CreateInstance(info.Arg!, n);
                if (id is int a) _ids[a] = arr;
                int i = 0;
                foreach (var x in items.EnumerateArray()) arr.SetValue(Value(x, info.Arg!), i++);
                return arr;
            }
            case Shape.List:
            {
                var list = (IList)Activator.CreateInstance(type, n)!;
                if (id is int a) _ids[a] = list;
                foreach (var x in items.EnumerateArray()) list.Add(Value(x, info.Arg!));
                return list;
            }
            case Shape.Set:
            {
                var set = Activator.CreateInstance(type)!;
                if (id is int a) _ids[a] = set;
                var add = type.GetMethod("Add")!;
                foreach (var x in items.EnumerateArray()) add.Invoke(set, [Value(x, info.Arg!)]);
                return set;
            }
            default:
            {
                var map = Activator.CreateInstance(type)!;
                if (id is int a) _ids[a] = map;
                var indexer = type.GetProperty("Item")!;
                foreach (var x in items.EnumerateArray())
                {
                    var k = Value(x[0], info.Arg!)!;
                    indexer.SetValue(map, Value(x[1], info.Arg2!), [k]);
                }
                return map;
            }
        }
    }

    private static object Scalar(JsonElement e, Type t)
    {
        if (t == typeof(string)) return e.GetString()!;
        if (t == typeof(bool)) return e.GetBoolean();
        if (t == typeof(int)) return e.GetInt32();
        if (t == typeof(double))
            return e.ValueKind == JsonValueKind.String ? double.Parse(e.GetString()!, System.Globalization.CultureInfo.InvariantCulture) : e.GetDouble();
        if (t == typeof(long)) return e.GetInt64();
        if (t == typeof(uint)) return e.GetUInt32();
        if (t == typeof(ulong)) return e.GetUInt64();
        if (t == typeof(float)) return e.GetSingle();
        if (t == typeof(char)) return e.GetString()![0];
        return Convert.ChangeType(e.GetRawText(), t, System.Globalization.CultureInfo.InvariantCulture);
    }

    private object Object(JsonElement e, TypeInfo info, int? id)
    {
        var t = info.Type;
        // A parameterless constructor fills fields a save from before they existed leaves out.
        var ctor = t.IsValueType ? null : t.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, Type.EmptyTypes);
        object obj = ctor != null ? ctor.Invoke(null) : RuntimeHelpers.GetUninitializedObject(t);
        if (id is int i) _ids[i] = obj;
        foreach (var p in e.EnumerateObject())
        {
            if (p.Name.StartsWith('$') || !info.Fields.TryGetValue(p.Name, out var f)) continue;
            f.SetValue(obj, Value(p.Value, f.FieldType));
        }
        // Collections a save does not carry (skipped fields, or ones added since) start empty.
        foreach (var f in info.Ordered.Concat(info.Skipped))
        {
            if (f.FieldType.IsValueType || f.GetValue(obj) != null) continue;
            var natural = TypeInfo.Natural(f.FieldType);
            if (natural != typeof(string) && typeof(IEnumerable).IsAssignableFrom(natural) && !natural.IsArray && natural.GetConstructor(Type.EmptyTypes) != null)
                f.SetValue(obj, Activator.CreateInstance(natural));
        }
        return obj;
    }
}
