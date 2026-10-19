using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CrisesTests
{
    private static (Simulation Sim, Menace M) Seeded(int seed = 3, bool auto = true)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = auto, AutoDefense = auto });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        return (sim, m!);
    }

    [Fact]
    public void ACleverStrongEnemyHasAWorseCrisisThanADullWeakOne()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Cunning = "crafty";
        m.Level = 3;
        m.Power = 150;
        double dull = Crises.Score(t, m);
        m.Cunning = "mastermind";
        m.Level = 12;
        m.Power = 700;
        double vampire = Crises.Score(t, m);
        Assert.True(vampire > dull * 3, $"{vampire} vs {dull}");
        var sv = t.Content.Crises.Severities;
        Assert.Equal("lesser", sv.Last(s => dull >= s.Min).Id);
        Assert.Equal("dire", sv.Last(s => vampire >= s.Min).Id);
        m.Cunning = "mindless";
        Assert.Equal(0, Crises.Score(t, m));
    }

    [Fact]
    public void APowerLeftAloneBeginsACrisisThatStrikesAndMarchesOnTheWalls()
    {
        var (sim, m) = Seeded(auto: false);
        var t = sim.Town;
        t.Saga.Act = 2;
        m.Cunning = "cunning";
        m.Power = t.Content.Saga.Menaces.Stages.Power + 10;
        var crisis = (Crisis?)null;
        for (int d = 0; d < 200 && t.Outcome == null && m.Alive; d++)
        {
            sim.RunDays(1);
            crisis ??= Crises.Of(t, m);
            if (crisis is { Active: false }) break;
        }
        Assert.NotNull(crisis);
        Assert.True(crisis!.BlowsLanded + crisis.BlowsStopped > 0 || !m.Alive);
        if (m.Alive && t.Outcome == null) Assert.False(crisis.Active);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(Crises.Title(t, crisis), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrisesUnderWayRaiseDread()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        Assert.Equal(0, Crises.Dread(t));
        t.Crises.All.Add(new Crisis { Id = 1, MenaceId = m.Id, Severity = "dire", StartDay = t.Day, Phase = CrisisPhase.Onslaught, NextPhaseDay = t.Day + 30, NextBlowDay = t.Day + 30 });
        Assert.True(Crises.Dread(t) > 0);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "doom:1" && a.Kind == Alerts.Doom);
    }

    [Fact]
    public void RaidersStayAwayFromATownTheyCannotBeat()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(20);
        var t = sim.Town;
        var farm = t.Buildings.First(b => b.Complete && Threat.Exposed(t, b));
        Assert.True(Threat.WorthRaiding(t, farm, 10_000));
        Assert.False(Threat.WorthRaiding(t, farm, 0));
        Assert.False(Threat.WorthRaiding(t, null, 10_000));
    }

    [Fact]
    public void AnswerToAClashIsTheTownsOnlyOnce()
    {
        var (sim, a) = Seeded();
        var t = sim.Town;
        var b = Menaces.Seed(t, [2, 3])!;
        t.Crises.Clashes.Add(new Clash { Id = 1, A = a.Id, B = b.Id, Where = "the hills", Day = t.Day + 5 });
        Assert.Contains(Alerts.Standing(t), x => x.Key == "clash:1");
        Assert.False(sim.JoinClash(1, 999));
        Assert.True(sim.JoinClash(1, b.Id));
        Assert.False(sim.JoinClash(1, 0));
        Assert.DoesNotContain(Alerts.Standing(t), x => x.Key == "clash:1");
    }

    [Fact]
    public void AWrongedEnemySwearsVengeanceAndComesForTheHeroItMarked()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Cunning = "mastermind";
        var hero = t.Heroes.OrderBy(h => h.Id).First();
        Vengeance.Wrong(t, Vengeance.Key(m), new Culprit(m.Name, m.Leader, m.SeatName), "broken", 1.6, [hero.Id]);
        var g = Vengeance.Of(t, Vengeance.Key(m));
        Assert.NotNull(g);
        Assert.Contains(hero.Id, g!.Marked);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("sworn", StringComparison.Ordinal));
        int entries = t.Ledger.Entries.Count;
        for (int d = 0; d < 30 && g.Heat >= t.Content.Crises.Vengeance.StrikeAt; d++) sim.RunDays(1);
        Assert.True(g.Heat < t.Content.Crises.Vengeance.StrikeAt);
        Assert.Contains(t.Ledger.Entries.Skip(entries), e => e.Text.Contains(m.Name, StringComparison.Ordinal) || e.Text.Contains(m.Leader, StringComparison.Ordinal));
    }
}
