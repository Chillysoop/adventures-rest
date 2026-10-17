using System.Globalization;
using System.Numerics;

namespace CrownAndCompass.Sim;

/// <summary>
/// The JavaScript number semantics the TypeScript reference sim relies on, so that both runtimes
/// make the same decisions and print byte-identical Ledgers: Math.round, Math.hypot,
/// Number.prototype.toFixed and Number-to-string.
/// </summary>
public static class Js
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>Math.round: to the nearest integer, halves toward +infinity (C# rounds halves to even).</summary>
    public static double Round(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x)) return x;
        double r = Math.Floor(x);
        return x - r >= 0.5 ? r + 1 : r;
    }

    /// <summary>Math.round for values used as counts.</summary>
    public static int RoundInt(double x) => (int)Round(x);

    /// <summary>V8's Math.hypot: normalised by the largest argument, with Kahan summation.</summary>
    public static double Hypot(double a, double b)
    {
        if (double.IsInfinity(a) || double.IsInfinity(b)) return double.PositiveInfinity;
        if (double.IsNaN(a) || double.IsNaN(b)) return double.NaN;
        a = Math.Abs(a);
        b = Math.Abs(b);
        double max = Math.Max(a, b);
        if (max == 0) return 0;
        double sum = 0;
        double compensation = 0;
        foreach (double v in (ReadOnlySpan<double>)[a, b])
        {
            double n = v / max;
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
        }
        return Math.Sqrt(sum) * max;
    }

    /// <summary>
    /// Number.prototype.toFixed: rounds the exact binary value, ties to the larger magnitude, and keeps
    /// the "-" of a negative that rounds to zero. (.NET's "F" format rounds exact ties to even.)
    /// </summary>
    public static string ToFixed(double x, int digits)
    {
        if (double.IsNaN(x)) return "NaN";
        if (Math.Abs(x) >= 1e21) return Str(x);
        bool negative = x < 0;
        // |x| = mantissa * 2^exponent, exactly.
        long bits = BitConverter.DoubleToInt64Bits(Math.Abs(x));
        int rawExponent = (int)((bits >> 52) & 0x7FF);
        long mantissa = bits & 0xFFFFFFFFFFFFFL;
        if (rawExponent == 0) rawExponent = 1;
        else mantissa |= 1L << 52;
        int exponent = rawExponent - 1075;

        BigInteger scaled = new BigInteger(mantissa) * BigInteger.Pow(10, digits);
        BigInteger n;
        if (exponent >= 0) n = scaled << exponent;
        else
        {
            BigInteger den = BigInteger.One << -exponent;
            n = BigInteger.DivRem(scaled, den, out var rem);
            if (rem * 2 >= den) n += 1;
        }

        string s = n.ToString(Inv);
        if (digits > 0)
        {
            if (s.Length <= digits) s = new string('0', digits - s.Length + 1) + s;
            s = s[..^digits] + "." + s[^digits..];
        }
        return negative ? "-" + s : s;
    }

    /// <summary>String(x) for a JS number: integers without a decimal point, otherwise the shortest round-trip form.</summary>
    public static string Str(double x)
    {
        if (double.IsNaN(x)) return "NaN";
        if (double.IsPositiveInfinity(x)) return "Infinity";
        if (double.IsNegativeInfinity(x)) return "-Infinity";
        if (x == 0) return "0";
        if (x == Math.Floor(x) && Math.Abs(x) < 9007199254740992.0) return ((long)x).ToString(Inv);

        // Shortest round-trip digits and decimal exponent, then laid out the way ECMAScript does.
        string shortest = Math.Abs(x).ToString("R", Inv);
        int e = shortest.IndexOfAny(['E', 'e']);
        string mantissa = e >= 0 ? shortest[..e] : shortest;
        int exp10 = e >= 0 ? int.Parse(shortest[(e + 1)..], Inv) : 0;
        int dot = mantissa.IndexOf('.');
        string intPart = dot >= 0 ? mantissa[..dot] : mantissa;
        string all = intPart + (dot >= 0 ? mantissa[(dot + 1)..] : "");
        string trimmed = all.TrimStart('0');
        // n: where the decimal point sits relative to the first significant digit.
        int n = intPart.Length + exp10 - (all.Length - trimmed.Length);
        string digits = trimmed.TrimEnd('0');
        int k = digits.Length;
        string body;
        if (k <= n && n <= 21) body = digits + new string('0', n - k);
        else if (0 < n && n <= 21) body = digits[..n] + "." + digits[n..];
        else if (-6 < n && n <= 0) body = "0." + new string('0', -n) + digits;
        else
        {
            int ee = n - 1;
            string sign = ee >= 0 ? "+" : "-";
            body = (k == 1 ? digits : digits[..1] + "." + digits[1..]) + "e" + sign + Math.Abs(ee).ToString(Inv);
        }
        return x < 0 ? "-" + body : body;
    }

    /// <summary>A sequential left-to-right sum, as Array.prototype.reduce adds (LINQ may reorder doubles).</summary>
    public static double Sum<T>(IEnumerable<T> items, Func<T, double> f)
    {
        double s = 0;
        foreach (var item in items) s += f(item);
        return s;
    }
}
