namespace CrownAndCompass.Sim;

/// <summary>
/// Deterministic random source (xoshiro128**). Every random decision in the simulation goes
/// through one instance so a seed replays a campaign exactly. Bit-for-bit the same generator as
/// the TypeScript sim's rng.ts (uint arithmetic stands in for JS's <c>&gt;&gt;&gt; 0</c> and Math.imul).
/// </summary>
public sealed class Rng
{
    private uint _s0;
    private uint _s1;
    private uint _s2;
    private uint _s3;

    public Rng(int seed)
    {
        unchecked
        {
            // SplitMix32 to spread the seed over the state.
            uint x = (uint)seed;
            uint Next()
            {
                x += 0x9e3779b9;
                uint z = x;
                z = (z ^ (z >> 16)) * 0x85ebca6b;
                z = (z ^ (z >> 13)) * 0xc2b2ae35;
                return z ^ (z >> 16);
            }
            _s0 = Next();
            _s1 = Next();
            _s2 = Next();
            _s3 = Next();
            if ((_s0 | _s1 | _s2 | _s3) == 0) _s0 = 1;
        }
    }

    public uint NextUint()
    {
        unchecked
        {
            uint result = RotL(_s1 * 5, 7) * 9;
            uint t = _s1 << 9;
            _s2 ^= _s0;
            _s3 ^= _s1;
            _s1 ^= _s2;
            _s0 ^= _s3;
            _s2 ^= t;
            _s3 = RotL(_s3, 11);
            return result;
        }
    }

    /// <summary>Integer in [min, max).</summary>
    public int Range(int min, int max)
    {
        if (max <= min) return min;
        return min + (int)(NextUint() % (uint)(max - min));
    }

    /// <summary>Float in [0, 1).</summary>
    public double Next() => (NextUint() >> 8) / 16777216.0;

    public bool Chance(double p) => Next() < p;

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

    private static uint RotL(uint x, int k) => (x << k) | (x >> (32 - k));

    /// <summary>D&amp;D ability modifier.</summary>
    public static int Modifier(int score) => (int)Math.Floor((score - 10) / 2.0);
}
