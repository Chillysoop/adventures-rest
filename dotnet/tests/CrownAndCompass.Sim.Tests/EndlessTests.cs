using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class EndlessTests
{
    private static (Simulation Sim, Menace Big) Crowned(bool endless, int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, Endless = endless });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        m!.BigBad = true;
        sim.Town.Saga.BigBadId = m.Id;
        sim.Town.Saga.CrownDay = sim.Town.Day;
        return (sim, m);
    }

    [Fact]
    public void EveryHeraldAndComplicationHasWords()
    {
        var r = Content.Default().Endless;
        Assert.NotEmpty(r.Heralds);
        Assert.NotEmpty(r.Complications);
        Assert.All(r.Heralds, h => Assert.False(string.IsNullOrWhiteSpace(h.Leader + h.Text)));
        foreach (var c in r.Complications)
        {
            Assert.False(string.IsNullOrWhiteSpace(c.Text), c.Id);
            Assert.True(c.Dread + Math.Abs(c.Mandate) + c.Gold + c.Empower + c.Harvest > 0, c.Id);
        }
    }

    [Fact]
    public void AWinWithoutEndlessEndsTheCampaign()
    {
        var (sim, big) = Crowned(false);
        Victory.Win(sim.Town, Victory.Def(sim.Town, "golden")!);
        Assert.Equal(Outcome.Victory, sim.Town.Outcome);
        Assert.Empty(Endless.State(sim.Town).Eras);
    }

    [Fact]
    public void AWinInEndlessClosesAnEraAndAWorseFoeComes()
    {
        var (sim, big) = Crowned(true);
        var t = sim.Town;
        big.Level = 5;
        Victory.Win(t, Victory.Def(t, "golden")!);
        Assert.Null(t.Outcome);
        Assert.Null(t.Saga.BigBadId);
        Assert.False(big.Alive);
        var era = Assert.Single(Endless.State(t).Eras);
        Assert.Equal("golden", era.Road);
        Assert.Equal(2, Endless.EraNumber(t));
        Assert.NotNull(Endless.RestLeft(t));

        Endless.State(t).NextHeraldDay = t.Day;
        Endless.Daily(t);
        var next = Saga.BigBad(t);
        Assert.NotNull(next);
        Assert.NotEqual(big.Id, next!.Id);
        Assert.True(next.Level >= 5 + t.Content.Endless.LevelUp || next.Level == t.Content.Saga.Menaces.MaxLevel);
        Assert.Single(Endless.State(t).ComplicationsUsed);
        Assert.Null(Endless.RestLeft(t));
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Era 2.", StringComparison.Ordinal));
    }

    [Fact]
    public void SlayingTheBigBadInEndlessIsAnEraToo()
    {
        var (sim, big) = Crowned(true);
        Menaces.End(sim.Town, big);
        Assert.Null(sim.Town.Outcome);
        Assert.Equal(Victory.Slay, Assert.Single(Endless.State(sim.Town).Eras).Road);
    }

    [Fact]
    public void ABlightCutsTheHarvestForAWhile()
    {
        var (sim, _) = Crowned(true);
        var t = sim.Town;
        var st = Endless.State(t);
        st.HarvestCut = 0.3;
        st.HarvestUntil = t.Day + 2;
        Assert.Equal(0.7, Endless.Harvest(t), 3);
        sim.RunDays(2);
        Assert.Equal(1, Endless.Harvest(t), 3);
    }
}
