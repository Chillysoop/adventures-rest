using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class HedgeMagesTests
{
    [Fact]
    public void EveryHedgeTradeIsARealWorkplaceWithAnEffect()
    {
        var c = Content.Default();
        var h = c.Research.Hedge;
        Assert.NotEmpty(h.Trades);
        foreach (var tr in h.Trades)
        {
            Assert.Contains(c.Buildings, b => b.Id == tr.Building && b.Jobs > 0);
            Assert.True(tr.Yield + tr.Build + tr.Watch > 0, tr.Building);
        }
    }

    [Fact]
    public void TheSharpestMindInATradeLearnsItsSpellOnceTheLibraryIsStrong()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, Enemy = false });
        var t = sim.Town;
        sim.RunDays(30);
        var r = HedgeMages.Rules(t);
        Assert.Empty(HedgeMages.All(t));
        for (int i = 0; i < 4; i++) Research.Add(t, 3, "a test");
        Assert.True(Research.Power(t) >= r.Power);
        // One hedge mage per PerPeople townsfolk: run until the town is big enough and it's the day.
        while (t.Day % r.Every != 0 || HedgeMages.Cap(t) == 0) sim.RunDays(1);
        // Picked on the day itself: a worker may change jobs while the days run.
        var worker = t.Residents.First(a => a.Hero == null && a.JobId is int j && r.Trade(t.Building(j).Def.Id) != null);
        worker.Abilities.Int = 20;
        HedgeMages.Daily(t);
        Assert.Contains(HedgeMages.All(t), a => a.HedgeSpell != "");
        var mage = HedgeMages.All(t)[0];
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(mage.Name));
        if (HedgeMages.Working(t, mage) is { Yield: > 0 } tr) Assert.Equal(tr.Yield, HedgeMages.Yield(t, t.Building(mage.JobId!.Value)), 6);
        Assert.Contains(Research.Sources(t), s => s.Why.Contains("hedge"));
    }
}
