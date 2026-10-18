using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ClockworkTests
{
    [Fact]
    public void AWardenNeedsACityAndAWorkshopThenGuardsTheWallsButUnsettlesTheTown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoDefense = false });
        var t = sim.Town;
        Assert.NotNull(Clockwork.BuildBlocked(t));
        Assert.False(sim.BuildWarden());
        var r = t.Content.Clockwork;
        for (int d = 0; d < 900 && Clockwork.BuildBlocked(t) is { } why && why != "not enough gold"; d++) sim.RunDays(1);
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
