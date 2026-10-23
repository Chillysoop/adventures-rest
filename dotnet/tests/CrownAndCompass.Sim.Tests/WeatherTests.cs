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
        for (int seed = 1; seed <= 12; seed++)
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

    [Fact]
    public void SnowOnTheRoadSlowsWalkersButNotAnAirship()
    {
        var t = Fresh(3);
        int day = Enumerable.Range(60, 80).First(d => Weather.Ground(t, 2, 0, d).Snow >= 0.4 && Weather.Worst(t, 2, 0, d).Step < Weather.Storm);
        double start = (day - 1) * 24 + 6;
        List<Waypoint> road = [new Waypoint { X = 4, Y = 0 }];
        var said = new List<string>();
        var foot = Roads.Weathered(t, (0, 0), road, start, false, said);
        var air = Roads.Weathered(t, (0, 0), road, start, true, []);
        double plain = Roads.Hours(t, (0, 0), road);
        Assert.True(Roads.Hours(t, (0, 0), foot) > plain * 1.15, $"{Roads.Hours(t, (0, 0), foot)} vs {plain}");
        Assert.True(Roads.Hours(t, (0, 0), air) < Roads.Hours(t, (0, 0), foot));
        Assert.Single(said);
        // A summer's day of fair weather costs nothing.
        int fair = Enumerable.Range(150, 60).First(d => Enumerable.Range(0, 4).All(i => Weather.Worst(t, 2, 0, d + i).Step <= Weather.Grey) && Weather.Ground(t, 2, 0, d).Wet < 0.5);
        Assert.Equal(plain, Roads.Hours(t, (0, 0), Roads.Weathered(t, (0, 0), road, (fair - 1) * 24 + 6, false, [])), 3);
    }

    [Fact]
    public void AFordInSpateIsPassedForABridge()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = Fresh(seed);
            foreach (var ford in Passage.Crossings(t).Where(c => c.Kind == "ford"))
            {
                int? wet = null, dry = null;
                for (int day = 2; day < 360 && (wet == null || dry == null); day++)
                {
                    t.Tick = (day - 1) * 24;
                    bool flooded = Weather.Flooded(t, ford.X, ford.Y);
                    if (flooded) wet ??= day;
                    else dry ??= day;
                }
                if (wet == null || dry == null) continue;
                List<Waypoint> Over(int day)
                {
                    t.Tick = (day - 1) * 24;
                    return Passage.Ford(t, (ford.X - 1.5, ford.Y), [new Waypoint { X = ford.X + 1.5, Y = ford.Y }], []);
                }
                var dryWay = Over(dry.Value);
                if (!dryWay.Any(w => w.Crossing == ford.Name)) continue;
                var wetWay = Over(wet.Value);
                Assert.DoesNotContain(wetWay, w => w.Crossing == ford.Name && Passage.Crossings(t).Any(c => c.Kind == "bridge"));
                return;
            }
        }
        Assert.Fail("no ford ever ran in spate");
    }

    [Fact]
    public void AKillingNightCallsForSavesAndShelterSoftensIt()
    {
        var t = Fresh(2);
        var heroes = t.Heroes.Take(3).ToList();
        var r = Weather.Rules(t).Road;
        double whole = new Expedition.Venture(new Rng(0), t.Content, heroes).HpFraction(), hurtBare = 0, hurtSheltered = 0;
        for (int i = 0; i < 60; i++)
        {
            var bare = new Expedition.Venture(new Rng(i), t.Content, heroes);
            bare.Exposed(r with { ShelterDc = 99 }, "stormNight", "blizzard", 5, new Rng(1000 + i));
            hurtBare += whole - bare.HpFraction();
            var snug = new Expedition.Venture(new Rng(i), t.Content, heroes);
            snug.Exposed(r with { ShelterDc = 0 }, "stormNight", "blizzard", 5, new Rng(1000 + i));
            hurtSheltered += whole - snug.HpFraction();
            Assert.Contains(snug.Lines, l => l.Contains("blizzard"));
        }
        Assert.True(hurtBare > 0);
        Assert.True(hurtSheltered < hurtBare, $"{hurtSheltered} vs {hurtBare}");
        // The town's dice never roll for it.
        Assert.Equal(Fresh(2).Rng.Next(), t.Rng.Next());
    }

    [Fact]
    public void TheWeatherLeansOnTheHarvestButNeverDecidesIt()
    {
        var t = Fresh(3);
        var r = Weather.Rules(t).Harvest;
        var seen = new HashSet<string>();
        for (int day = 1; day <= 360; day++)
        {
            t.Tick = (day - 1) * 24 + 8;
            double y = Weather.Harvest(t);
            Assert.InRange(y, r.Least, r.Most);
            seen.Add(Weather.Fields(t).Why);
        }
        // Over a year the fields see more than one kind of season.
        Assert.True(seen.Count >= 2, string.Join(",", seen));
    }

    [Fact]
    public void RepairsMendDamageTheWayTheTownChose()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(20);
        var t = sim.Town;
        var b = t.Buildings.First(x => x.Complete && !x.Def.IsGrand && Blows.Burns(x) && x.Def.BuildHours >= 20);
        Blows.Wreck(t, b, 0.5);
        Assert.True(b.Damaged);
        Assert.False(sim.SetRepairs("nonsense"));
        Assert.True(sim.SetRepairs("first"));
        Construction.Mend(t);
        Assert.True(b.Priority);
        Assert.True(sim.SetRepairs("hire"));
        t.Treasury += 5000;
        double before = b.BuildProgress, gold = t.Treasury;
        Construction.Mend(t);
        Assert.True(b.BuildProgress > before);
        Assert.True(t.Treasury < gold);
        while (b.Damaged) Construction.Mend(t);
        Construction.Mend(t);
        Assert.False(b.Wrecked);
    }

    [Fact]
    public void StormsTearAtTimberAndNeverStone()
    {
        int hit = 0;
        for (int seed = 1; seed <= 4 && hit == 0; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(300);
            var lines = sim.Town.Ledger.Entries.Select(e => e.Text).ToList();
            var r = Weather.Rules(sim.Town).Damage;
            hit += lines.Count(l => r.Texts.Values.SelectMany(x => x).Any(p => l.Contains(p[..Math.Min(14, p.IndexOf('{') is int i and > 0 ? i : 14)])));
            Assert.All(sim.Town.Buildings.Where(b => b.Wrecked), b => Assert.True(Blows.Burns(b) || b.Damaged));
        }
        Assert.True(hit > 0, "no storm damaged the town in four years of seeds");
    }

    [Fact]
    public void ALordWorksTheSkyRoundItsLairAndTheStormsItCalls()
    {
        var t = Fresh(5);
        int tick = 40 * 24 + 12;
        var calm = Weather.At(t, 5, 0, tick);
        t.Weather.Lords.Add(new WeatherLord { SiteId = "test", Monster = "storm-giant", Name = "the storm giant", Kind = "storm", X = 5, Y = 0, Power = 1.2, Radius = 1.5 });
        t.Weather.Version++;
        var worked = Weather.At(t, 5, 0, tick);
        Assert.True(worked.Storm > calm.Storm + 1);
        Assert.Equal("the storm giant", worked.By);
        Assert.Equal(Weather.At(t, -9, 0, tick).Storm, Weather.At(Fresh(5), -9, 0, tick).Storm, 6);

        var before = Weather.Here(t, tick + 12);
        t.Weather.Called.Add(new CalledStorm { SiteId = "test", Name = "the storm giant", X0 = 5, Y0 = 0, X1 = 0, Y1 = 0, From = tick, To = tick + 36, Power = 1.5, Radius = 2.5, AtTown = true });
        t.Weather.Version++;
        var after = Weather.Here(t, tick + 12);
        Assert.True(after.Step >= Weather.Storm && after.Step > before.Step, $"{before.Step} -> {after.Step}");
        // Nothing of it touches the town's dice.
        Assert.Equal(Fresh(5).Rng.Next(), t.Rng.Next());
    }

    [Fact]
    public void TheWindWardensTurnACalledStormFromTheTown()
    {
        var t = Fresh(5);
        t.Tick = 70 * 24;
        t.Weather.Called.Add(new CalledStorm { SiteId = "test", Name = "the storm giant", X0 = 5, Y0 = 0, From = t.Tick - 2, To = t.Tick + 30, Power = 1.5, Radius = 2.5, AtTown = true });
        t.Research.Known.Add(Weather.Rules(t).Lords.Wardens);
        Weather.Daily(t);
        Assert.Empty(t.Weather.Called);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("storm giant") && (e.Text.Contains("Vane") || e.Text.Contains("vane")));
    }

    [Fact]
    public void StormGiantsAndBlueDragonsKeepTheirLairsStormy()
    {
        var r = Weather.Rules(Fresh(1)).Lords;
        for (int seed = 1; seed <= 40; seed++)
        {
            var t = Fresh(seed);
            t.Tick = (r.From + 1) * 24;
            Weather.Daily(t);
            if (t.Weather.Lords.Count == 0) continue;
            var l = t.Weather.Lords[0];
            Assert.Contains(r.List, d => d.Monster == l.Monster);
            Assert.Contains(t.Sites, s => s.Def.Id == l.SiteId && !s.Cleared);
            return;
        }
        Assert.Fail("no seed has a weather-working lair");
    }
}
