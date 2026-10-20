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
        var edge = PointIn(t, "edge");
        Assert.False(City.IsOpen(t, edge.X, edge.Y));
        Assert.Equal(0, sim.PaintZone([edge], 1, Zone.Commercial));
    }

    [Fact]
    public void TheGroundRoundTheOldTownIsUnsettledUntilBuiltOn()
    {
        var t = Planned().Town;
        int c = Town.Grid / 2;
        Assert.Equal("old_town", City.WardOf(t, c + 0.5, c + 0.5).Id);
        Assert.Equal("edge", City.WardOf(t, c + City.Band + 2 + 0.5, c + 0.5).Id);
        Assert.Equal("edge", City.WardOf(t, c + 0.5, c + City.Band + 2 + 0.5).Id);
        Assert.Equal("outskirts", City.WardOf(t, Town.Grid - 1 + 0.5, c + 0.5).Id);
    }

    [Fact]
    public void AQuarterGrowsIntoTheWardItsFirstBuildingsSuit()
    {
        var sim = Planned();
        var t = sim.Town;
        int c = Town.Grid / 2;
        var east = (X: c + City.Band + 3 + 0.5, Y: c + 0.5);
        Assert.Equal(0, sim.PaintZone([east], 3, Zone.Commercial));
        t.City.Tier = City.TierIndex(t, "village");
        Assert.True(sim.PaintZone([east], 3, Zone.Commercial) > 0);
        var store = t.Content.Building("general_store");
        string side = City.Side(t, east.X, east.Y);
        for (int i = 0; i < t.Content.City.Founding; i++)
        {
            City.Form(t);
            Assert.Equal("edge", City.WardOf(t, east.X, east.Y).Id);
            var lot = Land.Find(t, store, Zone.Commercial)!;
            Assert.Equal(side, City.Side(t, lot.X, lot.Y));
            t.AddBuilding(store, lot, null, $"Shop {i}", true);
        }
        City.Form(t);
        Assert.Equal("market", City.WardOf(t, east.X, east.Y).Id);
        Assert.Equal("market", t.City.Sectors[City.Sector(1, side)]);
        Assert.Contains("market", t.City.OpenWards);
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
        t.City.Tier = City.TierIndex(t, "village");
        t.City.Sectors[City.Sector(1, "south")] = "hill";
        t.City.OpenWards.Add("hill");
        t.Plan.Wards = null;
        Assert.False(City.AllowsWard(t, manor, PointIn(t, "edge").X, PointIn(t, "edge").Y));
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
