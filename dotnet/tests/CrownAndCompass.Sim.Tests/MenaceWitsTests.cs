using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class MenaceWitsTests(LongCampaign campaign) : IClassFixture<LongCampaign>
{
    private static (Simulation Sim, Menace M) Seeded(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        return (sim, m!);
    }

    [Fact]
    public void ALeadersIntelligenceSetsHowFarItsMenaceCanGrow()
    {
        var t = campaign.Sim.Town;
        Assert.NotEmpty(t.Saga.Menaces);
        foreach (var m in t.Saga.Menaces.Where(m => !m.BigBad))
            Assert.True(m.Peak <= Menaces.CunningOf(t, m).MaxStage, $"{m.Name} ({m.Cunning}) reached {m.Peak}");
        var tiers = t.Content.Saga.Menaces.Cunning;
        Assert.Equal("mindless", tiers.First(c => 3 <= c.MaxInt).Id);
        Assert.Equal("crafty", tiers.First(c => 8 <= c.MaxInt).Id);
        Assert.Equal("mastermind", tiers.First(c => 17 <= c.MaxInt).Id);
    }

    [Fact]
    public void OnlyAMindThatCanRuleIsCrownedTheBigBad()
    {
        var t = campaign.Sim.Town;
        var big = Saga.BigBad(t);
        Assert.NotNull(big);
        Assert.True(Menaces.CanCrown(t, big!));
    }

    [Fact]
    public void EveryMenaceWantsSomethingItsKindAndWitsAllow()
    {
        var t = campaign.Sim.Town;
        var ids = t.Content.Saga.Menaces.Ambitions.Select(a => a.Id).ToHashSet();
        Assert.All(t.Saga.Menaces, m => Assert.Contains(m.Ambition, ids));
        Assert.All(t.Saga.Menaces.Where(m => Menaces.CunningOf(t, m).Id == "mindless"), m => Assert.DoesNotContain(m.Ambition, new[] { "vassal", "plague", "intrigue" }));
    }

    [Fact]
    public void BendingTheKneePutsTheTownUnderTheYokeWithoutASack()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Ambition = "vassal";
        m.DemandDueTick = t.Tick + 240;
        Assert.Same(m, Menaces.PendingDemand(t));
        double treasury = t.Treasury;
        Assert.True(sim.AnswerDemand(m.Id, true));
        Assert.Equal(m.Id, t.Saga.YokeId);
        Assert.Equal(0, t.Saga.Sacks);
        Assert.Equal(treasury, t.Treasury);
        Assert.Null(Menaces.PendingDemand(t));
        Assert.False(sim.AnswerDemand(m.Id, false));
    }

    [Fact]
    public void RefusingADemandSendsTheHost()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Ambition = "vassal";
        m.DemandDueTick = t.Tick + 240;
        Assert.True(sim.AnswerDemand(m.Id, false));
        Assert.True(m.Refused);
        Assert.True(m.NextStrikeTick <= t.Tick + 48);
        Assert.Null(t.Saga.YokeId);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("banner in the mud", StringComparison.Ordinal));
    }
}
