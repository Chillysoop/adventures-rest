using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class MonumentTests
{
    private static (Simulation Sim, Town T, Building Hall) Town(bool auto = false)
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = auto, AutoBounties = false, Enemy = false });
        var t = sim.Town;
        return (sim, t, t.Buildings.First(b => b.Def.Id == "town_hall"));
    }

    /// <summary>A finished temple near the hall, dedicated to the town's choice of god.</summary>
    private static Building Temple(Town t)
    {
        var def = t.Content.Building("temple");
        var b = t.AddBuilding(def, Planning.CivicLot(t, def)!, null, "", true);
        Faith.Dedicate(t, b, Faith.TempleChoice(t));
        return b;
    }

    [Fact]
    public void APartGoesUpBesideItsMonumentAndLendsTheTownItsGood()
    {
        var (sim, t, hall) = Town();
        hall.Grow = false;
        t.Treasury = 5000;
        double before = Planning.Mandate(t);
        var tower = sim.AddPart(hall.Id, "hall_bell_tower");
        Assert.NotNull(tower);
        Assert.Equal(hall.Id, tower!.ParentId);
        Assert.True(tower.Draws);
        Assert.Equal("it has one", Monuments.Blocked(t, hall, t.Content.Building("hall_bell_tower")));
        var core = t.Lot(hall.LotId);
        var at = t.Lot(tower.LotId);
        Assert.True(Math.Sqrt((at.X - core.X) * (at.X - core.X) + (at.Y - core.Y) * (at.Y - core.Y)) < 2 + Monuments.Room);
        for (int d = 0; d < 30 && tower.IsSite; d++) sim.RunDays(1);
        Assert.True(tower.Complete);
        Assert.Equal(before + 1, Planning.Mandate(t));
    }

    [Fact]
    public void APartNeedsItsCharterAndTheOnesBeforeIt()
    {
        var (_, t, hall) = Town();
        t.Treasury = 5000;
        Assert.Contains("town", Monuments.Blocked(t, hall, t.Content.Building("hall_archive")));
        Assert.Contains("council chamber first", Monuments.Blocked(t, hall, t.Content.Building("hall_banner_court")));
        Assert.Null(Monuments.Blocked(t, hall, t.Content.Building("hall_bell_tower")));
        t.Treasury = 10;
        Assert.Equal("not enough gold", Monuments.Blocked(t, hall, t.Content.Building("hall_bell_tower")));
    }

    [Fact]
    public void BuildingBigAtOnceEmptiesTheMarketAndTheSitesStallUntilACaravanComes()
    {
        var (sim, t, hall) = Town();
        t.Treasury = 5000;
        var temple = Temple(t);
        t.City.Tier = City.TierIndex(t, "village");
        int n = sim.AddAllParts(temple.Id);
        Assert.True(n >= 2);
        Assert.True(t.Treasury < 5000 - 200);
        // No timber or stone left in town.
        t.Market.Take(Good.Timber, 1e9);
        t.Market.Take(Good.Stone, 1e9);
        var part = Monuments.Parts(t, temple).First();
        for (int h = 0; h < 72 && !Construction.Stalled(t, part); h++)
        {
            t.Market.Take(Good.Timber, 1e9);
            t.Market.Take(Good.Stone, 1e9);
            sim.Step();
        }
        Assert.True(Construction.Stalled(t, part));
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"stalled:{part.Id}" && a.BuildingId == part.Id);
        t.Treasury += 5000;
        Assert.True(sim.SendCaravan(part.Id));
        Assert.Equal(1, part.Hauled);
        Assert.False(Construction.Stalled(t, part));
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"stalled:{part.Id}");
    }

    [Fact]
    public void ASitePutFirstTakesTheMarketsTimberAheadOfTheOthers()
    {
        var (sim, t, hall) = Town();
        t.Treasury = 5000;
        t.City.Tier = City.TierIndex(t, "village");
        var a = sim.AddPart(hall.Id, "hall_bell_tower")!;
        var b = sim.AddPart(hall.Id, "hall_council_chamber")!;
        Assert.True(sim.PrioritiseSite(b.Id, true));
        Assert.Equal(b.Id, Construction.Sites(t).First().Id);
        t.Market.Take(Good.Timber, 1e9);
        t.Market.Take(Good.Stone, 1e9);
        t.Market.Seed(Good.Timber, 2);
        double hauled = a.Hauled;
        Construction.Haul(t, a);
        Assert.Equal(hauled, a.Hauled);
        Construction.Haul(t, b);
        Assert.True(b.Hauled > 0);
    }

    [Fact]
    public void ALeftToGrowMonumentAddsItsNextPartFromSpareGold()
    {
        var (sim, t, hall) = Town();
        t.Treasury = 100;
        sim.RunDays(1);
        Assert.Empty(Monuments.Parts(t, hall));
        t.Treasury = 5000;
        sim.RunDays(1);
        Assert.Single(Monuments.Parts(t, hall));
        Assert.True(sim.GrowMonument(hall.Id, false));
        var parts = Monuments.Parts(t, hall);
        foreach (var p in parts) p.BuildProgress = p.Def.BuildHours;
        t.Treasury = 5000;
        sim.RunDays(1);
        Assert.Single(Monuments.Parts(t, hall));
    }

    [Fact]
    public void APartWinsItsGodsFavourAndCostsARival()
    {
        var (sim, t, _) = Town();
        t.Treasury = 5000;
        t.City.Tier = City.TierIndex(t, "village");
        var temple = Temple(t);
        temple.Grow = false;
        var god = Faith.Of(t, temple)!;
        var tower = sim.AddPart(temple.Id, "temple_bell_tower")!;
        tower.BuildProgress = tower.Def.BuildHours;
        var choir = Factions.Find(t, "hollow-choir")!;
        choir.Active = true;
        double standing = choir.Standing, favour = t.Faiths.Favor.Get(god.Id, 0);
        Monuments.Daily(t);
        Assert.Equal(standing - 0.1, choir.Standing, 6);
        Assert.Equal(favour + 0.6, t.Faiths.Favor.Get(god.Id, 0), 6);
        Assert.Contains("angers", Monuments.Gives(t, tower.Def));
    }
}
