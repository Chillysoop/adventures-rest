using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FrontierTests
{
    private static Simulation Town(bool walled, int watch = 0)
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoDefense = false, Enemy = false });
        sim.RunDays(10);
        var t = sim.Town;
        if (walled) t.Defenses.Palisade = PalisadeState.Built;
        t.Watch = watch;
        Garrison.Sync(t);
        sim.RunDays(1);
        return sim;
    }

    [Fact]
    public void TheGateLawNeedsAWallAndTurnsAgentsAway()
    {
        var open = Town(walled: false).Town;
        Assert.NotNull(Frontier.GateBlocked(open, "watched"));
        Assert.False(Frontier.SetGate(open, "shut"));
        Assert.Equal(0, Frontier.Turned(open));

        var sim = Town(walled: true, watch: 6);
        var t = sim.Town;
        Assert.True(Frontier.Walled(t));
        Assert.True(sim.SetGate("shut"));
        Assert.Equal("shut", Frontier.Gate(t).Id);
        Assert.True(Frontier.Turned(t) > 0.5);
        Assert.True(Frontier.Draw(t) < 0);
    }

    [Fact]
    public void AWallIsOnlyAsStrongAsTheWatchOnIt()
    {
        var t = Town(walled: true, watch: 0).Town;
        Assert.True(Frontier.Wanted(t) > 0);
        double bare = Threat.Fortification(t);
        Assert.Equal(0, Frontier.Manned(t));
        t.Watch = Frontier.Wanted(t) * 2;
        Garrison.Sync(t);
        Assert.True(Frontier.Manned(t) > 0);
        Assert.True(Threat.Fortification(t) > bare);
    }

    [Fact]
    public void FieldsWithinTheWallsYieldLessAndWealthDrawsRaiders()
    {
        var t = Town(walled: true).Town;
        var farm = t.Buildings.FirstOrDefault(b => b.Def.Zone == Zone.Farmland);
        if (farm != null) Assert.Equal(Frontier.Within(t, farm) ? t.Content.Frontier.InsideFarm : 1, Frontier.FarmYield(t, farm));
        t.Treasury = 0;
        Assert.Equal(1, Frontier.Lure(t), 6);
        t.Treasury = 10000;
        Assert.True(Frontier.Lure(t) > 1);
    }

    [Fact]
    public void TheNewsDietSendsABusyDaysRestToTheDigest()
    {
        var t = Town(walled: false).Town;
        t.News.Day = t.Day;
        t.News.Admitted = 0;
        t.News.Held.Clear();
        int budget = t.Content.News.Budget;
        for (int i = 0; i < budget; i++) Assert.Equal(Importance.Notable, t.Log(LedgerCategory.Social, Importance.Notable, $"busy {i}").Importance);
        Assert.Equal(Importance.Routine, t.Log(LedgerCategory.Social, Importance.Notable, "one too many").Importance);
        Assert.Equal(Importance.Notable, t.Log(LedgerCategory.War, Importance.Notable, "war always reaches you").Importance);
        Assert.Equal(Importance.Major, t.Log(LedgerCategory.Social, Importance.Major, "major is never held").Importance);
        Assert.Equal(1, t.News.Held["Social"]);
    }

    [Fact]
    public void TheLastRideWaitsOnTheMayor()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        // No big bad yet: there is no one to ride for.
        Assert.False(sim.CallLastRide());
        Assert.Null(Saga.Champions(sim.Town, out _, out _));
    }
}
