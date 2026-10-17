namespace CrownAndCompass.Sim.Core;

/// <summary>
/// Deterministic, platform-independent random source (xoshiro128**).
/// Every random decision in the simulation goes through one instance so a seed replays a campaign exactly.
/// </summary>
public sealed class Rng
{
    private uint _s0, _s1, _s2, _s3;

    public Rng(ulong seed)
    {
        // SplitMix64 to spread the seed over the state.
        ulong x = seed;
        ulong Next()
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }
        ulong a = Next(), b = Next();
        _s0 = (uint)a; _s1 = (uint)(a >> 32); _s2 = (uint)b; _s3 = (uint)(b >> 32);
        if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
    }

    public uint NextUInt()
    {
        uint result = RotL(_s1 * 5, 7) * 9;
        uint t = _s1 << 9;
        _s2 ^= _s0; _s3 ^= _s1; _s1 ^= _s2; _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotL(_s3, 11);
        return result;
    }

    private static uint RotL(uint x, int k) => (x << k) | (x >> (32 - k));

    /// <summary>Integer in [min, max).</summary>
    public int Range(int min, int max)
    {
        if (max <= min) return min;
        return min + (int)(NextUInt() % (uint)(max - min));
    }

    /// <summary>Double in [0, 1).</summary>
    public double NextDouble() => (NextUInt() >> 8) * (1.0 / (1 << 24));

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];

    /// <summary>Roll NdS, e.g. Roll(3, 6) for 3d6.</summary>
    public int Roll(int count, int sides)
    {
        int total = 0;
        for (int i = 0; i < count; i++) total += Range(1, sides + 1);
        return total;
    }

    public int D20() => Range(1, 21);

    /// <summary>Classic 4d6-drop-lowest ability score.</summary>
    public int AbilityScore()
    {
        int a = Range(1, 7), b = Range(1, 7), c = Range(1, 7), d = Range(1, 7);
        return a + b + c + d - Math.Min(Math.Min(a, b), Math.Min(c, d));
    }
}

public static class Dice
{
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);
}
