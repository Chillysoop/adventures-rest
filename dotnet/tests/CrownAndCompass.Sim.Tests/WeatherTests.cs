using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class WeatherTests
{
    private static Town Fresh(int seed) => new Simulation(new SimulationOptions { Seed = seed }).Town;

    [Fact]
    public void TheWeatherIsTheSeedsAndNeverTheTownsDice()
    {
        var a = Fresh(4);
        var b = Fresh(4);
        for (int tick = 0; tick < 24 * 60; tick += 7)
        {
            Assert.Equal(Weather.At(a, 3, -5, tick), Weather.At(b, 3, -5, tick));
            Assert.Equal(Weather.Here(a, tick), Weather.At(b, 0, 0, tick));
        }
        // Reading the sky draws nothing from the town's stream.
        Assert.Equal(Fresh(4).Rng.Next(), a.Rng.Next());
        // Another seed, another sky.
        Assert.NotEqual(Weather.Worst(Fresh(5), 0, 0, 30).Storm, Weather.Worst(a, 0, 0, 30).Storm);
    }

    [Fact]
    public void MostDaysAreFairOrGreyAndAStormIsAFewDaysASeason()
    {
        var steps = new int[5];
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = Fresh(seed);
            for (int day = 1; day <= 240; day++) steps[Weather.Worst(t, 0, 0, day).Step]++;
        }
        double days = steps.Sum();
        Assert.InRange((steps[Weather.Fair] + steps[Weather.Grey]) / days, 0.6, 0.92);
        Assert.InRange(steps[Weather.Storm] / days, 0.02, 0.14);
        Assert.InRange(steps[Weather.Tempest] / days, 0, 0.01);
    }

    [Fact]
    public void TheWeatherLumbersRatherThanFlickers()
    {
        var t = Fresh(2);
        int changes = 0;
        int last = Weather.Here(t, 0).Step;
        for (int tick = 1; tick < 24 * 120; tick++)
        {
            int s = Weather.Here(t, tick).Step;
            if (Math.Abs(s - last) >= 2) changes++;
            last = s;
        }
        // Jumps of two steps in an hour are rare: fronts come on over hours, not at once.
        Assert.True(changes < 24 * 120 / 50, $"{changes} jumps");
    }

    [Fact]
    public void WintersAreColdAndSnowLiesThen()
    {
        var t = Fresh(3);
        double Mean(int season) => Enumerable.Range(1, 30).Select(d => Weather.At(t, 0, 0, ((season * 30 + d - 10 + 120) % 120 + 120) * 24 + 12).Heat).Average();
        // The campaign opens on day 10 of spring: day 1 of each season is (season * 30 - 9).
        Assert.True(Mean(3) < Mean(1) - 10);
        double snowWinter = Enumerable.Range(0, 30).Select(d => Weather.Ground(t, 0, 0, 81 + 120 + d).Snow).Average();
        double snowSummer = Enumerable.Range(0, 30).Select(d => Weather.Ground(t, 0, 0, 21 + 120 + d).Snow).Average();
        Assert.True(snowWinter > snowSummer + 0.1, $"winter {snowWinter:F2} summer {snowSummer:F2}");
    }

    [Fact]
    public void FrontsDriftDownwind()
    {
        // What the sky upwind does today, the town tends to get a little later.
        double same = 0, cross = 0;
        for (int seed = 1; seed <= 4; seed++)
        {
            var t = Fresh(seed);
            for (int day = 5; day < 120; day++)
            {
                int tick = day * 24 + 12;
                double b = Weather.Prevailing(t, Weather.TimeOf(tick)) * Math.PI / 180;
                double ux = -Math.Sin(b) * 4, uy = Math.Cos(b) * 4;
                double here = Weather.At(t, 0, 0, tick + 48).Storm;
                same += Weather.At(t, ux, uy, tick).Storm * here;
                cross += Weather.At(t, -uy, ux, tick).Storm * here;
            }
        }
        Assert.True(same > cross, $"upwind {same:F1} vs across {cross:F1}");
    }

    [Fact]
    public void TheTownSeesTheSignsBeforeAStormBreaks()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(200);
        var lines = sim.Town.Ledger.Entries.Select(e => e.Text).ToList();
        var r = Weather.Rules(sim.Town);
        bool Starts(string line, IReadOnlyList<string> of) => of.Any(p => line.StartsWith(p[..Math.Min(18, p.IndexOf('{') is int i and > 0 ? i : 18)], StringComparison.Ordinal));
        Assert.Contains(lines, l => Starts(l, r.Signs));
        Assert.Contains(lines, l => Starts(l, r.Breaks) || Starts(l, r.Great2));
    }

    [Fact]
    public void ASnowyWinterSlowsTheMarch()
    {
        var t = Fresh(3);
        int most = 0;
        for (int day = 81; day < 111; day++)
        {
            t.Tick = (day - 1) * 24;
            most = Math.Max(most, Weather.MarchDays(t));
        }
        Assert.InRange(most, 1, Weather.Rules(t).MarchMost);
        t.Tick = 30 * 24;
        Assert.Equal(0, Weather.Ground(t, 0, 0, 31).Snow, 1);
    }

    [Fact]
    public void ADruidAtHomeReadsTheSkyADayFurther()
    {
        var t = Fresh(1);
        int lead = Weather.Lead(t);
        Assert.Equal(lead + 1, Weather.Forecast(t).Count);
        Assert.Equal(Weather.Rules(t).Lead, lead - (t.Heroes.Any(h => !h.IsAway && Weather.Rules(t).Readers.Contains(h.Hero!.Cls.Id)) ? Weather.Rules(t).LeadReader : 0));
    }
}
