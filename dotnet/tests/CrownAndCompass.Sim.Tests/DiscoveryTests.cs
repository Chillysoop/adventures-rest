using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DiscoveryTests
{
    [Fact]
    public void ANewTownKnowsNoFoe()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(1);
        Assert.False(Discovery.AnyFoe(sim.Town));
        Assert.False(Discovery.Known(sim.Town, Discovery.Warband));
    }

    [Fact]
    public void TheWarbandIsMetWhenItsScoutsAreSeenNotAtItsOmens()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        for (int d = 0; d < 120 && t.Enemy.Stage < ThreatStage.Probing; d++)
        {
            sim.RunDays(1);
            if (t.Enemy.Stage == ThreatStage.Omens) Assert.False(Discovery.Known(t, Discovery.Warband));
        }
        Assert.True(t.Enemy.Stage >= ThreatStage.Probing);
        Assert.True(Discovery.Known(t, Discovery.Warband));
        Assert.True(Discovery.AnyFoe(t));
    }

    [Fact]
    public void MeetingCountsOnlyTheFirstTimeAndNamedMenacesAreKnown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        var t = sim.Town;
        Assert.True(Discovery.Meet(t, "power:test", Discovery.Met));
        Assert.False(Discovery.Meet(t, "power:test", Discovery.Scried));
        Assert.Equal(Discovery.Met, Discovery.Of(t, "power:test")!.How);
        Assert.Contains(Discovery.MetToday(t), m => m.Key == "power:test");
        sim.RunDays(200);
        foreach (var m in t.Saga.Menaces.Where(m => m.Named && m.Origin != Menaces.FromWarband))
        {
            Assert.True(Discovery.Known(t, m));
            Assert.NotNull(Discovery.Of(t, Discovery.Key(m)));
        }
    }
}
