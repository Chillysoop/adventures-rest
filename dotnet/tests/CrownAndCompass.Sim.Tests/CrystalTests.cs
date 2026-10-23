using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Lift-crystal: the store charges burn, the traders' dear monthly crystal, shards and seams from the sky, mines and discoveries.</summary>
public class CrystalTests
{
    private static (Simulation Sim, Town T, Ship Ship) Docked(int seed = 1)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("sky_dock");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        t.Treasury += 20000;
        return (sim, t, Sky.Ships(t).Single());
    }

    [Fact]
    public void ChargesBurnTheStoreAndAnEmptyStoreGroundsTheFleet()
    {
        var (sim, t, ship) = Docked();
        var r = t.Content.Sky.Crystal;
        Assert.Equal(r.Start, Crystal.Stock(t), 6);
        ship.Lift = 0;
        Assert.True(sim.ChargeShip(ship.Id));
        double used = Fleet.Def(t, ship).Lift;
        Assert.Equal(r.Start - used, Crystal.Stock(t), 6);
        // With the store empty, gold alone charges nothing.
        t.SkyFleet.Crystal = 0;
        ship.Lift = 0;
        Assert.False(sim.ChargeShip(ship.Id));
        // A discovery makes every charge go further.
        t.SkyFleet.Crystal = 10;
        t.Research.Known.Add("crystal-tapping");
        Assert.True(sim.ChargeShip(ship.Id));
        Assert.Equal(10 - ship.Lift * (1 - Research.Crystal(t)), Crystal.Stock(t), 6);
    }

    [Fact]
    public void TradersSellALittleEachMonthAtAPrice()
    {
        var (sim, t, _) = Docked();
        var r = t.Content.Sky.Crystal;
        double gold = t.Treasury, stock = Crystal.Stock(t);
        Assert.True(sim.BuyCrystal(5));
        Assert.Equal(stock + 5, Crystal.Stock(t), 6);
        Assert.Equal(gold - Crystal.Price(t, 5), t.Treasury, 6);
        Assert.Equal(r.BuyMost - 5, Crystal.ForSale(t), 6);
        Assert.False(sim.BuyCrystal((int)r.BuyMost));
        sim.RunDays(r.BuyDays);
        Assert.Equal(r.BuyMost, Crystal.ForSale(t), 6);
    }

    [Fact]
    public void TheSkyGivesShardsAndSeamsAndAMineFillsTheStoreWhileItIsClear()
    {
        var (sim, t, _) = Docked(2);
        var high = t.Sites.First(s => s.Def.Band == "high");
        var low = t.Sites.First(s => s.Def.Band == "low");
        var company = new Party { Name = "Lanterns", Members = [], SiteId = high.Def.Id };
        double stock = Crystal.Stock(t);
        Crystal.Home(t, company, low);
        Assert.True(Crystal.Stock(t) > stock);
        Assert.False(low.Seam);
        Crystal.Home(t, company, high);
        Assert.True(high.Seam);
        Assert.Equal("the site isn't clear", Crystal.MineRefused(t, high));
        high.Cleared = true;
        Assert.True(sim.MineSeam(high.Def.Id));
        Assert.False(sim.MineSeam(high.Def.Id));
        stock = Crystal.Stock(t);
        Crystal.Daily(t, false);
        Assert.Equal(stock + Crystal.Yield(t, high), Crystal.Stock(t), 6);
        // Retaken, it yields nothing until cleared again; the miners' lore makes every mine yield more.
        high.Cleared = false;
        stock = Crystal.Stock(t);
        Crystal.Daily(t, false);
        Assert.Equal(stock, Crystal.Stock(t), 6);
        double plain = Crystal.Yield(t, high);
        t.Research.Known.Add("seam-sounding");
        Assert.True(Crystal.Yield(t, high) > plain);
    }
}
