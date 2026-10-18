using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CityTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    private static Lot LotIn(Town t, string ward) =>
        t.Lots.First(l => l.IsVacant && City.WardOf(t, l).Id == ward);

    [Fact]
    public void ANewTownIsAThorpInsideTheOldTown()
    {
        var sim = Planned();
        var t = sim.Town;
        Assert.Equal("thorp", City.Tier(t).Id);
        Assert.Equal(["old_town"], t.City.OpenWards);
        Assert.All(t.Buildings, b => Assert.Equal(0, City.Ring(t.Lot(b.LotId))));
        var market = LotIn(t, "market");
        Assert.False(Planning.CanZone(t, market, Zone.Commercial));
        Assert.Equal(0, sim.ZoneLots([market.Id], Zone.Commercial));
    }

    [Fact]
    public void WardsLieInRingsAroundTheHall()
    {
        var t = Planned().Town;
        int c = Town.Grid / 2;
        Assert.Equal("old_town", City.WardOf(t, t.Lots.First(l => l.X == c && l.Y == c)).Id);
        Assert.Equal("market", City.WardOf(t, t.Lots.First(l => l.X == c + City.Band + 2 && l.Y == c)).Id);
        Assert.Equal("hill", City.WardOf(t, t.Lots.First(l => l.X == c && l.Y == c + City.Band + 2)).Id);
        Assert.Equal("riverside", City.WardOf(t, t.Lots.First(l => l.X == Town.Grid - 1 && l.Y == c)).Id);
        Assert.Equal("field", City.WardOf(t, t.Lots.First(l => l.X == c && l.Y == 0)).Id);
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
        Assert.True(Planning.CanZone(t, LotIn(t, "market"), Zone.Commercial));
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
        Assert.False(City.Allows(t, manor, LotIn(t, "market")));
        Assert.True(City.Allows(t, manor, LotIn(t, "hill")));
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
