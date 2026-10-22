using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class SicknessTests
{
    private static Simulation Grown(int seed = 2, int days = 90)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void ItOnlyComesWhenSomethingBringsIt()
    {
        // Nothing near town brings it and no caravan runs: the town stays well however long it goes.
        var sim = new Simulation(new SimulationOptions { Seed = 1, Enemy = false });
        foreach (var s in sim.Town.Sites) s.Cleared = true;
        sim.RunDays(120);
        Assert.Equal(0, sim.Town.Sickness.Houses);
    }

    [Fact]
    public void ASickHouseStaysInAndGetsBetter()
    {
        var sim = Grown();
        var t = sim.Town;
        var house = t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0).OrderBy(b => b.Id).First();
        Assert.True(Sickness.Seed(t, house, "a test"));
        Assert.True(Sickness.IsSick(t, house));
        var who = t.Agent(house.Residents[0]);
        Assert.True(Sickness.Holds(t, who));
        Assert.True(Sickness.Mood(t, who) < 0);
        Assert.False(Sickness.Seed(t, house, "again"));
        sim.RunDays(t.Content.Sickness.Days[^1] + 2);
        Assert.False(Sickness.IsSick(t, house));
        // Just over it, it can't catch it again so soon.
        Assert.False(Sickness.Seed(t, house, "too soon"));
    }

    [Fact]
    public void ItSpreadsToTheHousesRoundAndTheHealersCanEndIt()
    {
        int spread = 0;
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            var sim = Grown(seed, 120);
            var t = sim.Town;
            foreach (var h in t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0).OrderBy(b => b.Id).Take(6)) Sickness.Seed(t, h, "a test");
            int before = Sickness.Sick(t).Count;
            sim.RunDays(4);
            spread += Math.Max(0, t.Sickness.Houses - before);
            if (Sickness.Sick(t).Count == 0) continue;
            t.Treasury += 1000;
            var tended = Sickness.Sick(t);
            Assert.Null(Sickness.Tend(t));
            sim.RunDays(t.Content.Sickness.Tend.Days + 1);
            Assert.All(tended, b => Assert.False(Sickness.IsSick(t, b)));
        }
        Assert.True(spread > 0, "a crowded town's sickness should reach some neighbour within days");
    }

    [Fact]
    public void AnOutbreakEndsAndTheRoadsRestAfter()
    {
        var sim = Grown();
        var t = sim.Town;
        var house = t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0).OrderBy(b => b.Id).First();
        Sickness.Seed(t, house, "a test");
        int start = t.Day;
        // It burns out within a season (another may start later on its own, so stop at the first end).
        while (Sickness.Outbreak(t) && t.Day - start < 60) sim.RunDays(1);
        Assert.False(Sickness.Outbreak(t));
        Assert.True(t.Sickness.LastOver >= start);
    }

    [Fact]
    public void QuarantineNeedsAnOutbreak()
    {
        var sim = Grown();
        var t = sim.Town;
        Assert.False(Sickness.SetQuarantine(t, true));
        var house = t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0).OrderBy(b => b.Id).First();
        Sickness.Seed(t, house, "a test");
        Assert.True(Sickness.SetQuarantine(t, true));
        Assert.True(Sickness.Mood(t, t.Workers.First(a => a.HomeId != house.Id)) < 0);
    }
}
