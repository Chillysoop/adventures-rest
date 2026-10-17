namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// The JavaScript number semantics the parity with the TypeScript sim depends on. Expected values
/// were printed by Node 22 (x.toFixed(0), x.toFixed(2), String(x), Math.round(x), Math.hypot).
/// </summary>
public class JsTests
{
    [Theory]
    [InlineData(0.0, "0", "0.00", "0", 0.0)]
    [InlineData(0.5, "1", "0.50", "0.5", 1.0)]
    [InlineData(1.5, "2", "1.50", "1.5", 2.0)]
    [InlineData(2.5, "3", "2.50", "2.5", 3.0)]
    [InlineData(-0.5, "-1", "-0.50", "-0.5", 0.0)]
    [InlineData(-2.5, "-3", "-2.50", "-2.5", -2.0)]
    [InlineData(0.125, "0", "0.13", "0.125", 0.0)]
    [InlineData(0.375, "0", "0.38", "0.375", 0.0)]
    [InlineData(1.005, "1", "1.00", "1.005", 1.0)]
    [InlineData(2.675, "3", "2.67", "2.675", 3.0)]
    [InlineData(1.45, "1", "1.45", "1.45", 1.0)]
    [InlineData(12.345, "12", "12.35", "12.345", 12.0)]
    [InlineData(-0.4, "-0", "-0.40", "-0.4", 0.0)]
    [InlineData(-0.004, "-0", "-0.00", "-0.004", 0.0)]
    [InlineData(99.995, "100", "100.00", "99.995", 100.0)]
    [InlineData(1e-07, "0", "0.00", "1e-7", 0.0)]
    [InlineData(123456789.125, "123456789", "123456789.13", "123456789.125", 123456789.0)]
    [InlineData(0.30000000000000004, "0", "0.30", "0.30000000000000004", 0.0)]
    [InlineData(0.3333333333333333, "0", "0.33", "0.3333333333333333", 0.0)]
    [InlineData(0.6666666666666666, "1", "0.67", "0.6666666666666666", 1.0)]
    [InlineData(1234.5678, "1235", "1234.57", "1234.5678", 1235.0)]
    [InlineData(5e-324, "0", "0.00", "5e-324", 0.0)]
    [InlineData(1e+21, "1e+21", "1e+21", "1e+21", 1e+21)]
    [InlineData(1.7976931348623157e+308, "1.7976931348623157e+308", "1.7976931348623157e+308", "1.7976931348623157e+308", 1.7976931348623157e+308)]
    [InlineData(1e-06, "0", "0.00", "0.000001", 0.0)]
    [InlineData(1.234e-07, "0", "0.00", "1.234e-7", 0.0)]
    [InlineData(4.35, "4", "4.35", "4.35", 4.0)]
    [InlineData(8.345, "8", "8.35", "8.345", 8.0)]
    [InlineData(1.0000000000000002, "1", "1.00", "1.0000000000000002", 1.0)]
    [InlineData(72.5, "73", "72.50", "72.5", 73.0)]
    [InlineData(245.49999999999997, "245", "245.50", "245.49999999999997", 245.0)]
    [InlineData(0.49999999999999994, "0", "0.50", "0.49999999999999994", 0.0)]
    public void FormatsAndRoundsLikeJavaScript(double x, string fixed0, string fixed2, string str, double round)
    {
        Assert.Equal(fixed0, Js.ToFixed(x, 0));
        Assert.Equal(fixed2, Js.ToFixed(x, 2));
        Assert.Equal(str, Js.Str(x));
        Assert.Equal(round, Js.Round(x));
    }

    [Fact]
    public void PrintsNegativeZeroAsZero()
    {
        Assert.Equal("0", Js.ToFixed(-0.0, 0));
        Assert.Equal("0", Js.Str(-0.0));
        Assert.Equal("-0", Js.ToFixed(-0.4, 0));
    }

    // Lot distances where V8's Math.hypot differs in the last bit from Math.Sqrt(a * a + b * b).
    [Theory]
    [InlineData(-6.5, -6.5, "9.19238815542512")]
    [InlineData(-7.5, -5.5, "9.300537618869136")]
    [InlineData(-5.5, -5.5, "7.778174593052023")]
    [InlineData(-5.5, -4.5, "7.1063352017759485")]
    [InlineData(-4.5, -4.5, "6.3639610306789285")]
    [InlineData(-7.5, -2.5, "7.905694150420949")]
    [InlineData(-5.5, -2.5, "6.041522986797285")]
    [InlineData(-2.5, -1.5, "2.91547594742265")]
    [InlineData(-1.5, -1.5, "2.121320343559643")]
    [InlineData(-7.5, -0.5, "7.516648189186455")]
    [InlineData(-2.5, -0.5, "2.5495097567963927")]
    public void MatchesV8Hypot(double a, double b, string expected)
    {
        double want = double.Parse(expected, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(want, Js.Hypot(a, b));
        Assert.Equal(want, Js.Hypot(b, a));
        Assert.Equal(want, Js.Hypot(-a, b));
        Assert.NotEqual(want, Math.Sqrt(a * a + b * b));
    }
}
