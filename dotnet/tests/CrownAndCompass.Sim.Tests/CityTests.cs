using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CityTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    /// <summary>The middle of the first unbuilt tile in the ward.</summary>
    private static (double X, double Y) PointIn(Town t, string ward)
    {
        for (int y = 0; y < Town.Grid; y++)
            for (int x = 0; x < Town.Grid; x++)
                if (City.WardOf(t, x + 0.5, y + 0.5).Id == ward && Land.BuildingAt(t, x + 0.5, y + 0.5) == null) return (x + 0.5, y + 0.5);
        throw new InvalidOperationException(ward);
    }

    [Fact]
    public void ANewTownIsAThorpInsideTheOldTown()
    {
        var sim = Planned();
        var t = sim.Town;
        Assert.Equal("thorp", City.Tier(t).Id);
        Assert.Equal(["old_town"], t.City.OpenWards);
        Assert.All(t.Buildings, b => Assert.Equal(0, City.Ring(t, t.Lot(b.LotId))));
        var market = PointIn(t, "market");
        Assert.False(City.IsOpen(t, market.X, market.Y));
        Assert.Equal(0, sim.PaintZone([market], 1, Zone.Commercial));
    }

    [Fact]
    public void WardsLieInRingsAroundTheHall()
    {
        var t = Planned().Town;
        int c = Town.Grid / 2;
        Assert.Equal("old_town", City.WardOf(t, c + 0.5, c + 0.5).Id);
        Assert.Equal("market", City.WardOf(t, c + City.Band + 2 + 0.5, c + 0.5).Id);
        Assert.Equal("hill", City.WardOf(t, c + 0.5, c + City.Band + 2 + 0.5).Id);
        Assert.Equal("riverside", City.WardOf(t, Town.Grid - 1 + 0.5, c + 0.5).Id);
        Assert.Equal("field", City.WardOf(t, c + 0.5, 0 + 0.5).Id);
    }

    [Fact]
    public void AWardOpensOnlyOnceTheTownHasGrownIntoIt()
    {
        var sim = Planned();
        var t = sim.Town;
        t.Treasury = 5000;
        Assert.Equal("needs the town to be a village", City.WardBlocked(t, "market"));
        Assert.False(sim.OpenWard("market"));
        t.City.Tier = City.TierIndex(t, "village");
        Assert.True(sim.OpenWard("market"));
        Assert.Equal("already laid out", City.WardBlocked(t, "market"));
        Assert.True(sim.PaintZone([PointIn(t, "market")], 1, Zone.Commercial) > 0);
        Assert.True(t.Accounts.Get(TreasuryAccount.Wards, 0) < 0);
    }

    [Fact]
    public void TheCharterAsksForResidentsInstitutionsAndGold()
    {
        var sim = Planned();
        var t = sim.Town;
        Assert.Contains(City.Missing(t), m => m.Contains("residents"));
        Assert.False(sim.RaiseCharter());
        while (t.Population < City.NextTier(t)!.Residents) Population.CreateAgent(t);
        t.Treasury = 1000;
        if (!City.Has(t, "tavern")) Assert.Contains("a tavern", City.Missing(t));
        else
        {
            Assert.Empty(City.Missing(t));
            Assert.True(sim.RaiseCharter());
            Assert.Equal("hamlet", City.Tier(t).Id);
        }
    }

    [Fact]
    public void ManorsRiseOnlyOnCastleHillAndNeedACity()
    {
        var t = Planned().Town;
        var manor = t.Content.Building("manor");
        foreach (var w in t.Content.City.Wards) t.City.OpenWards.Add(w.Id);
        Assert.False(City.AllowsWard(t, manor, PointIn(t, "market").X, PointIn(t, "market").Y));
        var hill = PointIn(t, "hill");
        Assert.True(City.AllowsWard(t, manor, hill.X, hill.Y));
        // A manor wants its land zoned medium density at least.
        Assert.False(City.Allows(t, manor, hill.X, hill.Y));
        Assert.NotNull(City.Locked(t, manor));
        t.City.Tier = City.TierIndex(t, "city");
        Assert.Null(City.Locked(t, manor));
    }

    [Fact]
    public void ATownLeftToItselfGrowsIntoNewWardsAndBetterHouses()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(300);
        var t = sim.Town;
        Assert.True(t.City.Tier >= City.TierIndex(t, "village"), $"still a {City.Tier(t).Name}");
        Assert.True(t.City.OpenWards.Count > 1);
        Assert.Contains(t.Buildings, b => b.Def.Id == "timber_house" || b.Def.Id == "townhouse");
        Assert.True(t.Accounts.Get(TreasuryAccount.HearthTax, 0) > 0);
        Assert.True(t.Accounts.Get(TreasuryAccount.Charters, 0) < 0);
    }
}
