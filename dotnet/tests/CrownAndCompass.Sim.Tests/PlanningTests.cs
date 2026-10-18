using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PlanningTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    [Fact]
    public void WithoutTheStandInTheCharterZonesOnlyItsOwnLots()
    {
        var t = Planned().Town;
        Assert.Contains(t.Buildings, b => b.Def.Id == "lumber_camp" && t.Lot(b.LotId).Zone == Zone.Industry);
        Assert.Equal(0, Planning.VacantZoned(t, Zone.Farmland));
    }

    [Fact]
    public void PaintedLotsAreZonedAndBuiltLotsAreLeftAlone()
    {
        var sim = Planned();
        var t = sim.Town;
        var built = t.Lots.First(l => !l.IsVacant && l.Zone == Zone.Residential);
        var open = t.Lots.Where(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l)).Take(3).Select(l => l.Id).ToList();
        int n = sim.ZoneLots([.. open, built.Id], Zone.Commercial);
        Assert.Equal(3, n);
        Assert.All(open, id => Assert.Equal(Zone.Commercial, t.Lot(id).Zone));
        Assert.NotEqual(Zone.Commercial, built.Zone);
        Assert.Equal(0, sim.ZoneLots(open, Zone.Civic));
        Assert.Equal(3, sim.ZoneLots(open, Zone.None));
    }

    [Fact]
    public void TownsfolkBuildOnlyWhereThePlayerZones()
    {
        var sim = Planned();
        sim.RunDays(20);
        var t = sim.Town;
        int before = t.Buildings.Count;
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("lot is zoned and for sale"));
        var near = t.Lots.Where(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l)).OrderBy(Town.DistanceFromCenter).ThenBy(l => l.Id).ToList();
        sim.ZoneLots(near.Take(4).Select(l => l.Id), Zone.Residential);
        sim.ZoneLots(near.Skip(4).Take(3).Select(l => l.Id), Zone.Farmland);
        sim.ZoneLots(near.Skip(7).Take(3).Select(l => l.Id), Zone.Commercial);
        sim.RunDays(30);
        Assert.True(t.Buildings.Count > before);
        Assert.All(t.Buildings.Where(b => b.OwnerId != null), b => Assert.Equal(b.Def.Zone, t.Lot(b.LotId).Zone));
    }

    [Fact]
    public void TheCouncilCommissionsAGranaryAndAWatchHouseWherePlaced()
    {
        var sim = Planned();
        var t = sim.Town;
        t.Treasury = 2000;
        var lot = t.Lots.First(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l) && Town.DistanceFromCenter(l) > 4);
        var granary = sim.Place("granary", lot.Id);
        Assert.NotNull(granary);
        Assert.Equal(lot.Id, granary!.LotId);
        Assert.Equal(Zone.Civic, lot.Zone);
        Assert.Null(granary.OwnerId);
        Assert.Null(sim.Place("granary", lot.Id));
        Assert.Equal("needs 20 residents", Planning.Blocked(t, t.Content.Building("watch_house")));
        Assert.True(t.Accounts.Get(TreasuryAccount.Works, 0) < 0);

        sim.RunDays(10);
        Assert.True(granary.Complete);
        Assert.Equal(60, Planning.FoodStore(t));
        Assert.Equal(1, Planning.WatchFactor(t));
    }

    [Fact]
    public void ACommissionedBusinessIsRunByTheTown()
    {
        var sim = Planned();
        var t = sim.Town;
        t.Treasury = 1000;
        var lot = t.Lots.First(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l));
        var forge = sim.Place("forge", lot.Id);
        Assert.NotNull(forge);
        Assert.Null(forge!.OwnerId);
        Assert.Equal(Zone.Industry, lot.Zone);
        Assert.Equal(Planning.StartingCash, forge.Cash);
        Assert.Null(sim.Place("town_hall", t.Lots.First(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l)).Id));
    }

    [Fact]
    public void ThePlayerPicksTheTemplesLot()
    {
        var sim = Planned();
        var t = sim.Town;
        Assert.Null(sim.Place("temple", t.Lots.First(l => l.IsVacant && l.OwnerId == null).Id));
        var lot = t.Lots.Last(l => l.IsVacant && l.Zone == Zone.None && City.IsOpen(t, l));
        // A big enough town with gold to spare.
        for (int i = 0; i < t.Content.Shrines.TemplePopulation; i++) Population.CreateAgent(t);
        t.Treasury = 5000;
        var temple = sim.Place("temple", lot.Id);
        Assert.NotNull(temple);
        Assert.Equal(lot.Id, temple!.LotId);
    }

    [Fact]
    public void TheStandInPutsIdleHandsToWorkInATownNoOneInvestsIn()
    {
        // Seed 30 used to stall with a dozen idle, run the treasury dry and starve by day 90.
        var sim = new Simulation(new SimulationOptions { Seed = 30 });
        sim.RunDays(120);
        var t = sim.Town;
        Assert.Contains(t.Buildings, b => b.Def.IsBusiness && b.OwnerId == null && b.Def.Produces.Count > 0);
        Assert.True(t.Population >= 20);
        Assert.True(t.Market.Price(Good.Food) < 2);
    }
}
