using System.Collections;

namespace CrownAndCompass.Sim;

/// <summary>
/// A map that iterates in insertion order, like a JS Map or a plain object with string keys.
/// The sim sums and prints some maps in that order, so it has to be guaranteed, not incidental.
/// </summary>
public sealed class OrderedMap<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>> where TKey : notnull
{
    private readonly List<TKey> _keys = [];
    private readonly Dictionary<TKey, TValue> _values = [];

    public int Count => _keys.Count;
    public IReadOnlyList<TKey> Keys => _keys;
    public IEnumerable<TValue> Values => _keys.Select(k => _values[k]);

    public bool ContainsKey(TKey key) => _values.ContainsKey(key);
    public bool TryGetValue(TKey key, out TValue value) => _values.TryGetValue(key, out value!);
    public bool Remove(TKey key)
    {
        if (!_values.Remove(key)) return false;
        _keys.Remove(key);
        return true;
    }

    public void Clear()
    {
        _keys.Clear();
        _values.Clear();
    }

    public TValue Get(TKey key, TValue fallback) => _values.TryGetValue(key, out var v) ? v : fallback;

    public TValue this[TKey key]
    {
        get => _values[key];
        set
        {
            if (!_values.ContainsKey(key)) _keys.Add(key);
            _values[key] = value;
        }
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        foreach (var k in _keys) yield return new KeyValuePair<TKey, TValue>(k, _values[k]);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
