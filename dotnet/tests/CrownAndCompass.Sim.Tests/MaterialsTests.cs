using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Townsfolk's builds take their timber and stone off the market as hauled, stall without it, and send away for it slowly and dearly.</summary>
public class MaterialsTests
{
    private static void Drain(Town t)
    {
        t.Market.Take(Good.Timber, 1e9);
        t.Market.Take(Good.Stone, 1e9);
    }

    [Fact]
    public void ASettlersSiteStallsWithoutTimberThenSendsAwayForItAndWaitsForTheCarts()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = false, AutoBounties = false, Enemy = false });
        var t = sim.Town;
        var def = t.Content.Building("house");
        var owner = t.Workers.First();
        owner.Gold = 5000;
        var site = t.AddBuilding(def, Land.Find(t, def, null)!, owner.Id, "the Test House", false);
        site.Draws = true;
        Drain(t);
        for (int h = 0; h < 48 && !Construction.Stalled(t, site); h++)
        {
            Drain(t);
            Construction.Haul(t, site);
            sim.Step();
        }
        Assert.True(Construction.Stalled(t, site));
        // The town's alerts are for what the mayor can do: a settler's stall says so on the build bar instead.
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"stalled:{site.Id}");
        var r = Construction.Rules(t);
        double cost = Construction.ImportCost(t, site);
        Assert.True(cost > Construction.CaravanCost(t, site));
        int sent = -1;
        for (int h = 0; h < (r.ImportWait + 2) * SimTime.HoursPerDay && sent < 0; h++)
        {
            Drain(t);
            sim.Step();
            if (site.ImportDue >= 0) sent = t.Tick;
        }
        Assert.True(sent >= 0);
        Assert.True(site.Hauled < 1);
        for (int h = 0; h < (r.ImportDays + 1) * SimTime.HoursPerDay && site.Hauled < 1; h++)
        {
            Drain(t);
            sim.Step();
        }
        Assert.Equal(1, site.Hauled);
        Assert.True(t.Tick - sent >= (r.ImportDays - 1) * SimTime.HoursPerDay);
    }

    [Fact]
    public void ASettlerPaysForEachLoadFromTheirOwnPurse()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = false, AutoBounties = false, Enemy = false });
        var t = sim.Town;
        var def = t.Content.Building("house");
        var owner = t.Workers.First();
        owner.Gold = 500;
        t.Market.Seed(Good.Timber, 100);
        var site = t.AddBuilding(def, Land.Find(t, def, null)!, owner.Id, "the Test House", false);
        site.Draws = true;
        double treasury = t.Treasury, stock = t.Market.Stock(Good.Timber);
        Assert.True(Construction.Haul(t, site));
        Assert.True(owner.Gold < 500);
        Assert.Equal(treasury, t.Treasury);
        Assert.True(t.Market.Stock(Good.Timber) < stock);
    }
}
