using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PavingTests
{
    private static (Simulation Sim, Town T) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = false, AutoBounties = false, Enemy = false });
        return (sim, sim.Town);
    }

    [Fact]
    public void AHamletPavesNothing()
    {
        var (_, t) = Town();
        t.Treasury = 5000;
        t.Market.Seed(Good.Stone, 200);
        Paving.Daily(t);
        Assert.All(t.Plan.Streets, s => Assert.Equal(0, s.Cobbled));
    }

    [Fact]
    public void ATownsMasonsPaveFromTheCentreOutwardWithTheMarketsStone()
    {
        var (_, t) = Town();
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 5000;
        t.Market.Seed(Good.Stone, 400);
        double stone = t.Market.Stock(Good.Stone);
        var first = Paving.Next(t)!;
        Assert.NotNull(first);
        double near = first.Points.Select((v, i) => (v, i)).Where(p => p.i % 2 == 0)
            .Min(p => Js.Hypot(p.v - CrownAndCompass.Sim.Town.Center, first.Points[p.i + 1] - CrownAndCompass.Sim.Town.Center));
        Assert.All(t.Plan.Streets.Where(s => s.Points.Length >= 4), s =>
            Assert.True(s.Points.Where((_, i) => i % 2 == 0).Select((x, k) => Js.Hypot(x - CrownAndCompass.Sim.Town.Center, s.Points[k * 2 + 1] - CrownAndCompass.Sim.Town.Center)).Min() >= near - 1e-9));
        for (int d = 0; d < 200 && !Paving.Done(first); d++) Paving.Daily(t);
        Assert.True(Paving.Done(first));
        Assert.True(t.Market.Stock(Good.Stone) < stone);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("begin paving", StringComparison.Ordinal));
        // A house beside the finished cobbles is happier for them.
        var house = t.Buildings.FirstOrDefault(b => City.Housing(t, b.Def.Id) != null && Paving.Comfort(t, b) > 0);
        Assert.NotNull(house);
    }

    [Fact]
    public void NoStoneNoPaving()
    {
        var (_, t) = Town();
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 5000;
        t.Market.Take(Good.Stone, 1e9);
        Paving.Daily(t);
        Assert.All(t.Plan.Streets, s => Assert.Equal(0, s.Cobbled));
    }
}
