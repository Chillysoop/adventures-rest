using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ClockworkTests
{
    [Fact]
    public void AWardenNeedsACityAndAWorkshopThenGuardsTheWallsButUnsettlesTheTown()
    {
        // An unguarded town can fall before it is a city; which seeds do shifts with every other system, so take the first that grows.
        Simulation sim = null!;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            sim = new Simulation(new SimulationOptions { Seed = seed, AutoDefense = false });
            Assert.NotNull(Clockwork.BuildBlocked(sim.Town));
            Assert.False(sim.BuildWarden());
            for (int d = 0; d < 900 && Clockwork.BuildBlocked(sim.Town) is { } why && why != "not enough gold"; d++) sim.RunDays(1);
            if (Clockwork.BuildBlocked(sim.Town) is null or "not enough gold") break;
        }
        var t = sim.Town;
        var r = t.Content.Clockwork;
        t.Treasury += r.Cost * 2;
        Assert.Null(Clockwork.BuildBlocked(t));
        double def = Clockwork.Defense(t), caught = Council.WatchStrength(t);
        Assert.True(sim.BuildWarden());
        Assert.Equal(1, t.Clockwork.Wardens);
        Assert.Equal(def + r.Strength, Clockwork.Defense(t));
        Assert.True(Council.WatchStrength(t) > caught || caught >= 0.85);
        Assert.True(Clockwork.Mood(t) < 0);
        Assert.True(sim.ScrapWarden());
        Assert.False(sim.ScrapWarden());
    }

    [Fact]
    public void AnUnpaidWardenRunsDown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoDefense = false });
        var t = sim.Town;
        t.Clockwork.Wardens = 2;
        t.Treasury = 0;
        Clockwork.Daily(t, false);
        Assert.Equal(1, t.Clockwork.Wardens);
    }
}
