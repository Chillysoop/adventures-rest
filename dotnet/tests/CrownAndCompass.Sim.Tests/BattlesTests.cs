using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BattlesTests
{
    /// <summary>Seed 2 reaches its Last Stand around day 160; step hour by hour until the siege is fought.</summary>
    private static Simulation ToSiege(int seed = 2)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        for (int h = 0; h < 260 * SimTime.HoursPerDay && sim.Town.Battles.Count == 0; h++) sim.Step();
        Assert.NotEmpty(sim.Town.Battles);
        return sim;
    }

    [Fact]
    public void TheLastStandIsRecordedWaveByWaveAsTheChronicleTellsIt()
    {
        var sim = ToSiege();
        var t = sim.Town;
        var b = t.Battles[^1];
        Assert.Equal("warband", b.Foe);
        Assert.Equal(t.Tick, b.Tick);
        Assert.Equal(t.Content.Enemy.Leader, b.Leader);
        Assert.Equal(t.Enemy.Approach, b.Approach);
        Assert.True(b.Strength > 0 && b.Defenders > 0);
        // One record per wave the chronicle names, breaching where it says the wave got in.
        var siege = t.Chronicles.Last(c => c.Lines.Any(l => l.StartsWith("Wave ", StringComparison.Ordinal)));
        var lines = siege.Lines.Where(l => l.StartsWith("Wave ", StringComparison.Ordinal)).ToList();
        Assert.Equal(lines.Count, b.Waves.Count);
        for (int i = 0; i < lines.Count; i++)
        {
            Assert.Equal(lines[i].Contains(" breach "), b.Waves[i].Breached);
            Assert.Contains(Garrison.Place(t, b.Waves[i].Section), lines[i]);
        }
        Assert.Equal(b.End == BattleEnd.Slain, t.Enemy.Stage == ThreatStage.Defeated);
        Assert.All(b.ChampionsFell, id => Assert.Contains(id, b.Champions));
    }

    [Fact]
    public void PlaybackRunsThroughItsPhasesOverTheHoursAfterTheSiege()
    {
        var sim = ToSiege();
        var t = sim.Town;
        var b = t.Battles[^1];
        double hours = t.Content.Garrison.Battle.Hours;
        // Fought at midnight, the host camps till dawn and the assault plays by daylight.
        int dawn = Battles.Assault(t, b);
        Assert.Equal(t.Content.Garrison.Battle.Dawn, SimTime.HourOf(dawn));
        Assert.Same(b, Battles.Playing(t, b.Tick + 0.5));
        Assert.Same(b, Battles.Playing(t, dawn + hours - 0.1));
        Assert.Null(Battles.Playing(t, dawn + hours + 0.1));
        Assert.Null(Battles.Playing(t, b.Tick - 0.5));
        Assert.Equal(BattlePhase.March, Battles.At(t, b, dawn - 0.5).Phase);
        var phases = Enumerable.Range(0, 60).Select(i => Battles.At(t, b, dawn + hours * i / 60.0).Phase).ToList();
        Assert.Equal([BattlePhase.Waves, BattlePhase.Sally, BattlePhase.End], phases.Distinct());
        // Every wave gets its turn, in order.
        var waves = Enumerable.Range(0, 60).Select(i => Battles.At(t, b, dawn + hours * i / 60.0)).Where(p => p.Phase == BattlePhase.Waves).Select(p => p.Wave).Distinct().ToList();
        Assert.Equal(Enumerable.Range(0, b.Waves.Count), waves);
        Assert.Contains("wave 1 of", Battles.Headline(t, b, dawn + 0.1));
    }

    [Fact]
    public void AReloadedGameMidSiegePlaysOnTheRest()
    {
        var sim = ToSiege();
        sim.Step();
        sim.Step();
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        var t = loaded.Town;
        var b = Battles.Playing(t, t.Tick + 0.5);
        Assert.NotNull(b);
        var was = sim.Town.Battles[^1];
        Assert.Equal(was.Waves.Select(w => (w.Section, w.Size, w.Breached)), b!.Waves.Select(w => (w.Section, w.Size, w.Breached)));
        Assert.Equal(was.End, b.End);
        Assert.Equal(Battles.Headline(sim.Town, was, sim.Town.Tick + 0.5), Battles.Headline(t, b, t.Tick + 0.5));
    }
}
