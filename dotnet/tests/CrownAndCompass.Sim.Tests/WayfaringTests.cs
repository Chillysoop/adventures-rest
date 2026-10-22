using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Jobs on the road, nature as a monster, strange places, old gateways and lands that follow the climate.</summary>
public class WayfaringTests
{
    /// <summary>A town a few weeks on, with heroes in its taverns.</summary>
    private static Town Grown(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(30);
        return sim.Town;
    }

    [Fact]
    public void EachHeroTakesADayJobAndTheBestTakeTheNightOnes()
    {
        var t = Grown(1);
        var party = t.Heroes.Take(3).ToList();
        Assert.Equal(3, party.Count);
        var jobs = Wayfaring.Assign(t, party);
        var day = jobs.Where(j => Wayfaring.Rules(t.Content).Order.Contains(j.Job)).ToList();
        Assert.Equal(Math.Min(party.Count, Wayfaring.Rules(t.Content).Order.Count), day.Count);
        Assert.Equal(day.Count, day.Select(j => j.AgentId).Distinct().Count());
        var watch = jobs.Single(j => j.Job == "watch");
        Assert.Equal(party.Max(a => Skills.Bonus(t, a, "perception")), watch.Bonus);
        Assert.True(Wayfaring.Describe(t, watch).Contains("(Perception"), Wayfaring.Describe(t, watch));
    }

    [Fact]
    public void RoutineJobsNeverFail()
    {
        Assert.True(Wayfaring.Routine(2, 12));
        Assert.False(Wayfaring.Routine(1, 12));
    }

    [Fact]
    public void ALostNavigatorCostsHoursInHardCountry()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var mire = t.Content.Roads.Kinds.First(k => k.Id == "mire");
        var f = new Feature { Id = 99, Name = "the Test Mire", Kind = mire, Tier = 6, Bearing = 0, Arc = 20, Inner = 1, Outer = 2 };
        int lostOnes = 0;
        for (int i = 0; i < 40; i++)
        {
            var said = new List<string>();
            var mind = new RouteMind(1, 1, 1, "Ada", null, false, false) { Navigator = "Ada", NavBonus = -1, Dice = new Rng(i) };
            double lost = Wayfaring.Navigate(t.Content, mind, f, 10, said);
            if (lost > 0)
            {
                lostOnes++;
                Assert.Contains(said, s => s.Contains("Ada"));
            }
        }
        Assert.InRange(lostOnes, 10, 39);
        // A ranger at home in the wild never gets lost there.
        var sure = new RouteMind(1, 1, 1, "Bo", null, false, false) { Navigator = "Bo", NavBonus = 12, Dice = new Rng(1) };
        Assert.Equal(0, Wayfaring.Navigate(t.Content, sure, f, 10, []));
        var w = new Waypoint { X = 1, Y = 0 }.Lost(4);
        Assert.Equal(4 + 10, Roads.Hours(t, (0, 0), [w]), 6);
    }

    [Fact]
    public void NatureFightsLikeAnEncounter()
    {
        var t = Grown(2);
        var ridge = t.Content.Roads.Kinds.First(k => k.Id == "ridge");
        Assert.NotNull(ridge.Peril);
        var always = ridge with { Peril = ridge.Peril! with { Chance = 1 }, Ambush = 0 };
        var heroes = t.Heroes.Take(3).ToList();
        var v = new Expedition.Venture(new Rng(5), t.Content, heroes);
        v.TakeJobs(Wayfaring.Assign(t, heroes), Wayfaring.Bonuses(t, heroes), new Rng(9));
        v.Cross(always, "the Test Ridge", 2);
        Assert.True(v.Lines.Any(l => l.Contains(ridge.Peril!.Win) || l.Contains(ridge.Peril!.Lose)), string.Join("\n", v.Lines) + $" heroes {heroes.Count}");
        Assert.Contains(v.Lines, l => l.Contains(ridge.Peril!.Win) || l.Contains(ridge.Peril!.Lose));
    }

    [Fact]
    public void StrangePlacesOasesAndAGatewayAreOnEveryMap()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            Assert.Contains(t.Terrain, f => f.Kind.Wonder is { Kind: not ("oasis" or "mirage") });
            Assert.NotEmpty(t.Gateways);
            foreach (var s in t.Sites)
            {
                var (x, y) = World.Spot(t, s);
                Assert.DoesNotContain(t.Terrain, f => f.Kind.Wonder != null && Roads.Contains(f, x, y));
            }
            if (t.Regions.Any(r => r.Theme.Id == "duneglass"))
                Assert.Contains(t.Terrain, f => f.Kind.Wonder?.Kind is "oasis" or "mirage");
        }
    }

    [Fact]
    public void AnOpenGatewayShortensAFarRoad()
    {
        var t = Grown(3);
        var g = t.Gateways[0];
        g.Known = true;
        g.Open = true;
        var heroes = t.Heroes.Take(3).ToList();
        var mind = Roads.Mind(t, heroes, false, false);
        // From the near arch's side of town to the far arch.
        var to = (g.Bx * 1.05, g.By * 1.05);
        var said = new List<string>();
        var road = Roads.Plan(t, (0, 0), to, mind, new Rng(1), said);
        Assert.Contains(road, w => w.Way == "portal");
        Assert.True(Roads.Hours(t, (0, 0), road) < Js.Hypot(to.Item1, to.Item2) * t.Content.Roads.MarchHours);
        Assert.Contains(said, s => s.Contains(g.Name));
    }

    [Fact]
    public void AnAllyTellsTheWordThatOpensAGateway()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var s = t.Settlements[0];
        var g = new Gateway { Id = 7, Name = "the Test Gate", RegionA = s.RegionId, RegionB = s.RegionId, Lock = "password" };
        t.Gateways.Add(g);
        Wayfaring.Daily(t);
        Assert.False(g.Open);
        s.Allied = true;
        Wayfaring.Daily(t);
        Assert.True(g.Open && g.Known);
    }

    [Fact]
    public void LandsFollowTheClimate()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            foreach (var ring in t.Regions.GroupBy(r => r.Ring))
            {
                var list = ring.OrderBy(r => r.Bearing).ToList();
                for (int i = 0; i < list.Count; i++)
                {
                    var a = list[i].Theme;
                    var b = list[(i + 1) % list.Count].Theme;
                    if (a.Heat is double ha && b.Heat is double hb)
                        Assert.True(Math.Abs(ha - hb) < 2, $"seed {seed}: {a.Id} beside {b.Id}");
                }
            }
        }
    }
}
