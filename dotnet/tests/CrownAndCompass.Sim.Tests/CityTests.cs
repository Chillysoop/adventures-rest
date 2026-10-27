using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CityTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    /// <summary>The middle of the first unbuilt tile in a ring.</summary>
    private static (double X, double Y) PointIn(Town t, int ring)
    {
        for (int y = 0; y < Town.Grid; y++)
            for (int x = 0; x < Town.Grid; x++)
                if (City.Ring(t, x + 0.5, y + 0.5) == ring && Land.BuildingAt(t, x + 0.5, y + 0.5) == null) return (x + 0.5, y + 0.5);
        throw new InvalidOperationException($"ring {ring}");
    }

    /// <summary>Puts a finished building of this kind down at a point, whether or not it would fit there.</summary>
    private static Building Put(Town t, string id, double x, double y)
    {
        var def = t.Content.Building(id);
        var (w, d) = Land.Footprint(def);
        // The neighbourhood is read once an hour: move the clock on so the next read sees it.
        t.Tick++;
        return t.AddBuilding(def, new Lot(t.Lots.Count, x, y) { W = w, D = d, Zone = def.Zone }, null, def.Name, true);
    }

    [Fact]
    public void ANewTownIsAThorpWithAllItsLandOpen()
    {
        var sim = Planned();
        var t = sim.Town;
        Assert.Equal("thorp", City.Tier(t).Id);
        Assert.All(t.Buildings, b => Assert.Equal(0, City.Ring(t, t.Lot(b.LotId))));
        // Land is never bought or opened by charter: the town may zone anywhere from the first day.
        var edge = PointIn(t, 1);
        Assert.True(City.IsOpen(t, edge.X, edge.Y));
        Assert.True(sim.PaintZone([edge], 1, Zone.Commercial) > 0);
    }

    [Fact]
    public void NeighbourhoodsFormWhereBuildingsClusterAndNameThemselves()
    {
        var t = Planned().Town;
        Neighbourhood.Daily(t);
        Assert.Equal(Neighbourhood.Rules(t).Quarters.Heart, t.City.Quarters[0].Name);
        int c = Town.Grid / 2;
        // Shops far out east, clear of the old town: a market street of their own.
        for (int i = 0; i < Neighbourhood.Rules(t).Quarters.Founding; i++) Put(t, "general_store", c + 30 + (i % 3) * 3, c + (i / 3) * 4);
        Neighbourhood.Daily(t);
        var market = t.City.Quarters.Last();
        Assert.Equal("market", market.Character);
        Assert.Contains(market.Name, Neighbourhood.Rules(t).Quarters.Names["market"]);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(market.Name));
        var stats = Neighbourhood.Stats(t, market);
        Assert.Equal(Neighbourhood.Rules(t).Quarters.Founding, stats.Shops);
        // It keeps its name and place however the day's run goes.
        int count = t.City.Quarters.Count;
        Neighbourhood.Daily(t);
        Assert.Equal(count, t.City.Quarters.Count);
    }

    [Fact]
    public void WhatStandsRoundAHomeSetsItsRentAndMood()
    {
        var t = Planned().Town;
        int c = Town.Grid / 2;
        double x = c + 30, y = c;
        // Far from the town's shops: a long walk, cheaper and glummer.
        Assert.False(Neighbourhood.ShopsNear(t, x, y));
        Assert.True(Neighbourhood.Rent(t, x, y) < 1);
        Assert.True(Neighbourhood.Mood(t, x, y) < 0);
        Assert.Contains(Neighbourhood.Why(t, x, y), w => w.Contains("long walk"));
        // A shop or two near: dearer and cheerier.
        Put(t, "general_store", x + 2, y);
        double withShop = Neighbourhood.Mood(t, x, y);
        Assert.True(Neighbourhood.ShopsNear(t, x, y));
        Assert.True(Neighbourhood.Rent(t, x, y) > 1);
        Assert.True(withShop > 0);
        // A whole market at the door is noise.
        for (int i = 0; i < 6; i++) Put(t, "general_store", x + (i % 3) - 1, y + 1 + i / 3);
        Assert.True(Neighbourhood.Mood(t, x, y) < withShop);
        // The workshops' smoke makes a street cheap and gloomy.
        double far = Neighbourhood.Rent(t, c - 30, c);
        Put(t, "forge", c - 29, c);
        Assert.True(Neighbourhood.Rent(t, c - 30, c) < far);
        Assert.Contains(Neighbourhood.Why(t, c - 30, c), w => w.Contains("smoke"));
    }

    [Fact]
    public void BuildingsAskTheirGroundForWhatTheyNeed()
    {
        var t = Planned().Town;
        int c = Town.Grid / 2;
        double x = c - 25, y = c + 10;
        var manor = t.Content.Building("manor");
        var tenement = t.Content.Building("tenement");
        // Plain ground: no manor, but tenements go up.
        Assert.NotNull(Neighbourhood.Sited(t, manor, x, y));
        Assert.Null(Neighbourhood.Sited(t, tenement, x, y));
        // Fine houses and a shrine round it make it dear: a manor may rise, tenements may not.
        foreach (var (dx, dy) in new[] { (1.5, 0.0), (-1.5, 0.0), (0.0, 1.5), (0.0, -1.5) }) Put(t, "townhouse", x + dx, y + dy);
        Put(t, "shrine", x + 2, y + 2);
        Put(t, "general_store", x - 2, y + 2);
        Assert.Null(Neighbourhood.Sited(t, manor, x, y));
        Assert.NotNull(Neighbourhood.Sited(t, tenement, x, y));
        // The cathedral wants holy ground beside it; the fishery, the river.
        var cathedral = t.Content.Building("cathedral");
        Assert.Null(Neighbourhood.Sited(t, cathedral, x, y));
        Assert.NotNull(Neighbourhood.Sited(t, cathedral, c + 25, c - 25));
        var fishery = t.Content.Building("fishery");
        Assert.NotNull(Neighbourhood.Sited(t, fishery, x, y));
        Assert.Null(Neighbourhood.Sited(t, fishery, c + 40, c));
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
    public void ATownLeftToItselfGrowsNeighbourhoodsAndBetterHouses()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(300);
        var t = sim.Town;
        Assert.True(t.City.Tier >= City.TierIndex(t, "village"), $"still a {City.Tier(t).Name}");
        Assert.True(t.City.Quarters.Count > 1);
        Assert.Contains(t.Buildings, b => b.Def.Id == "timber_house" || b.Def.Id == "townhouse");
        Assert.True(t.Accounts.Get(TreasuryAccount.HearthTax, 0) > 0);
        Assert.True(t.Accounts.Get(TreasuryAccount.Charters, 0) < 0);
    }
}
