using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ThreatTests
{
    private static void RunUntilOutcome(Simulation sim, int maxDays)
    {
        for (int d = 0; d < maxDays && sim.Town.Outcome == null; d++) sim.RunDays(1);
    }

    [Fact]
    public void EscalatesThroughForeshadowedStagesAndRaisesAWarCampHeroesCanRaid()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var seen = new HashSet<ThreatStage>();
        for (int d = 0; d < 90; d++)
        {
            sim.RunDays(1);
            seen.Add(sim.Town.Enemy.Stage);
        }
        Assert.Contains(ThreatStage.Omens, seen);
        Assert.Contains(ThreatStage.Probing, seen);
        Assert.Contains(sim.Town.Sites, s => s.Def.Id == "bloodtusk_camp");
        Assert.True(sim.Town.Ledger.Entries.Count(e => e.Category == LedgerCategory.War) > 3);
    }

    [Fact]
    public void AnnouncesTheMarchDaysBeforeTheSiege()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, AutoBounties = false });
        RunUntilOutcome(sim, 250);
        var march = sim.Town.Ledger.Entries.FirstOrDefault(e => e.Text.Contains("is on the march"));
        var siege = sim.Town.Chronicles.FirstOrDefault(c => c.PartyName.StartsWith("Defenders", StringComparison.Ordinal));
        Assert.NotNull(march);
        Assert.NotNull(siege);
        Assert.True(siege.ReturnDay * 24 - march.Tick >= 2 * 24);
    }

    [Fact]
    public void PunishesATownThatNeverFortifiesAndNeverFightsBack()
    {
        int fallen = 0;
        foreach (int seed in new[] { 1, 3, 4 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = false });
            sim.Town.Defenses.Palisade = PalisadeState.Building;
            sim.Town.Defenses.PalisadeReadyTick = int.MaxValue;
            RunUntilOutcome(sim, 250);
            if (sim.Town.Outcome == Outcome.Defeat) fallen++;
        }
        Assert.True(fallen >= 2);
    }

    [Fact]
    public void CanBeWonByATownThatFortifiesAndSendsHeroesOut()
    {
        int won = 0;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            RunUntilOutcome(sim, 260);
            if (sim.Town.Outcome == Outcome.Victory) won++;
            Assert.True(sim.Town.Enemy.Strength >= 0);
        }
        Assert.True(won >= 2);
    }

    [Fact]
    public void OnlyBuildsAPalisadeTheTreasuryCanPayFor()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, StartingTreasury = 0, Enemy = false });
        Assert.False(sim.BuildDefense(DefenseWork.Palisade));
        sim.Town.Treasury = 2000;
        Assert.True(sim.BuildDefense(DefenseWork.Palisade));
        Assert.False(sim.BuildDefense(DefenseWork.Tower));
        double before = Threat.Defense(sim.Town);
        sim.RunDays(7);
        Assert.Equal(PalisadeState.Built, sim.Town.Defenses.Palisade);
        Assert.True(Threat.Defense(sim.Town) > before);
    }
}
