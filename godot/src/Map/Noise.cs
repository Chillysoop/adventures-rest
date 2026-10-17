namespace CrownAndCompass.Client.Map;

/// <summary>
/// Seeded 2D gradient noise in plain C#, so terrain generation runs in managed code without a
/// native call per sample, and the same seed always draws the same land.
/// </summary>
public sealed class Noise
{
    private readonly int[] _perm = new int[512];
    private static readonly float[] Gx = new float[16], Gy = new float[16];

    static Noise()
    {
        for (int i = 0; i < 16; i++)
        {
            Gx[i] = MathF.Cos(i * MathF.Tau / 16);
            Gy[i] = MathF.Sin(i * MathF.Tau / 16);
        }
    }

    public Noise(int seed)
    {
        var p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        uint s = (uint)seed * 2654435761u + 1013904223u;
        for (int i = 255; i > 0; i--)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            int j = (int)(s % (uint)(i + 1));
            (p[i], p[j]) = (p[j], p[i]);
        }
        for (int i = 0; i < 512; i++) _perm[i] = p[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);

    private float Grad(int ix, int iy, float dx, float dy)
    {
        int h = _perm[_perm[ix & 255] + (iy & 255)] & 15;
        return Gx[h] * dx + Gy[h] * dy;
    }

    /// <summary>Gradient noise in roughly -0.7..0.7.</summary>
    public float Get(float x, float y)
    {
        int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
        float fx = x - ix, fy = y - iy;
        float u = Fade(fx), v = Fade(fy);
        float a = Grad(ix, iy, fx, fy), b = Grad(ix + 1, iy, fx - 1, fy);
        float c = Grad(ix, iy + 1, fx, fy - 1), d = Grad(ix + 1, iy + 1, fx - 1, fy - 1);
        return a + (b - a) * u + (c - a) * v + (a - b - c + d) * u * v;
    }

    /// <summary>Fractal sum, normalised to roughly -1..1.</summary>
    public float Fbm(float x, float y, int octaves, float lacunarity = 2, float gain = 0.5f)
    {
        float sum = 0, amp = 1, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            sum += Get(x, y) * amp;
            norm += amp;
            amp *= gain;
            x = x * lacunarity + 17.3f;
            y = y * lacunarity - 9.1f;
        }
        return sum / norm * 1.6f;
    }

    /// <summary>Sharp-crested ridges in 0..1, for mountain chains.</summary>
    public float Ridge(float x, float y, int octaves)
    {
        float sum = 0, amp = 0.5f, norm = 0;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1 - MathF.Abs(Get(x, y) * 1.4f);
            sum += n * n * amp;
            norm += amp;
            amp *= 0.5f;
            x = x * 2 + 5.7f;
            y = y * 2 + 3.3f;
        }
        return sum / norm;
    }
}
