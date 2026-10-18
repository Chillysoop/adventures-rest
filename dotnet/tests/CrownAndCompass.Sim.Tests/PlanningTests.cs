using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PlanningTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    [Fact]
    public void WithoutTheStandInTheCharterZonesOnlyItsOwnPatches()
    {
        var t = Planned().Town;
        Assert.Contains(t.Buildings, b => b.Def.Id == "lumber_camp" && t.Lot(b.LotId).Zone == Zone.Industry);
        Assert.Equal(0, Planning.VacantZoned(t, Zone.Farmland));
    }

    /// <summary>An open point about (dx, dy) tiles from the hall where the building can stand: off the lanes, on level enough ground.</summary>
    private static (double X, double Y) Open(Town t, double dx, double dy, string id)
    {
        var d = t.Content.Building(id);
        for (double r = 0; r < 4; r += 0.25)
            for (int k = 0; k < 16; k++)
            {
                double x = Town.Center + dx + r * Math.Cos(k * Math.PI / 8), y = Town.Center + dy + r * Math.Sin(k * Math.PI / 8);
                if (Planning.SpotBlocked(t, d, x, y, 1.0) == null) return (x, y);
            }
        throw new InvalidOperationException("no open spot");
    }

    [Fact]
    public void ABrushPaintsRoundPatchesAndLeavesBuiltGroundAlone()
    {
        var sim = Planned();
        var t = sim.Town;
        var hall = t.Lot(t.Buildings[0].LotId);
        var at = Open(t, 6, 6, "house");
        int n = sim.PaintZone([at, (hall.X, hall.Y)], 1, Zone.Commercial);
        // A circle of radius one tile is about pi tiles, each Res² cells; the hall keeps its civic ground.
        Assert.InRange(n, 40, 70 + 60);
        Assert.Equal(Zone.Civic, Land.ZoneAt(t, (int)(hall.X * Land.Res), (int)(hall.Y * Land.Res)));
        Assert.Equal(Zone.Commercial, Land.ZoneAt(t, (int)(at.X * Land.Res), (int)(at.Y * Land.Res)));
        Assert.Equal(0, sim.PaintZone([at], 1, Zone.Civic));
        Assert.True(sim.PaintZone([at], 2, Zone.None) >= 40);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("as commercial land"));
    }

    [Fact]
    public void BuildingsTurnAnyWayAndNeverOverlap()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(90);
        var t = sim.Town;
        // Not just the four compass headings: buildings face the hall from wherever they stand.
        Assert.True(t.Lots.Select(l => Math.Round(l.Yaw, 2)).Distinct().Count() > 8);
        var seen = new HashSet<int>();
        foreach (var l in t.Lots)
            foreach (int i in Land.Cells(l.X, l.Y, l.W, l.D, l.Yaw))
            {
                Assert.True(seen.Add(i), $"lot {l.Id} overlaps another");
                Assert.Equal(l.Id + 1, t.Plan.Taken[i]);
            }
    }

    [Fact]
    public void LanesWearInAndHomesFrontThemOnLevelEnoughGround()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(120);
        var t = sim.Town;
        // More than the square: lanes have worn in, and none runs through a building.
        Assert.True(t.Plan.Roads.Count > 5);
        for (int i = 0; i < t.Plan.Paved.Length; i++) Assert.False(t.Plan.Paved[i] != 0 && t.Plan.Taken[i] != 0);
        foreach (var b in t.Buildings.Where(b => b.OwnerId != null))
        {
            var l = t.Lot(b.LotId);
            Assert.True(Terrain.Span(t, l.X, l.Y, l.W, l.D, l.Yaw) <= Terrain.MaxSpan + 1e-9);
            // Its door looks onto a lane close by.
            double fx = l.X - Math.Sin(l.Yaw) * (l.D / (2.0 * Land.Res) + Roads.Verge + Roads.Width / 2 + 0.7), fy = l.Y + Math.Cos(l.Yaw) * (l.D / (2.0 * Land.Res) + Roads.Verge + Roads.Width / 2 + 0.7);
            Assert.True(Roads.Nearest(t, fx, fy)!.Value.D < 0.9, $"{b.Name} faces no lane");
        }
    }

    [Fact]
    public void TownsfolkBuildOnlyWhereThePlayerPaints()
    {
        var sim = Planned();
        sim.RunDays(20);
        var t = sim.Town;
        int before = t.Buildings.Count;
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("is painted on the plan"));
        sim.PaintZone([Open(t, -4, 0, "house"), (Town.Center - 4, 1.5 + Town.Center)], 1.4, Zone.Residential);
        sim.PaintZone([Open(t, 0, 5.5, "house")], 1.6, Zone.Farmland);
        sim.PaintZone([Open(t, 4, -3, "house")], 1.2, Zone.Commercial);
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
        var (x, y) = Open(t, 5, 0, "granary");
        var granary = sim.Place("granary", x, y, 1.0);
        Assert.NotNull(granary);
        var lot = t.Lot(granary!.LotId);
        Assert.Equal((x, y, 1.0), (lot.X, lot.Y, lot.Yaw));
        Assert.Equal(Zone.Civic, lot.Zone);
        Assert.Null(granary.OwnerId);
        Assert.Null(sim.Place("granary", x, y));
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
        var (x, y) = Open(t, -5, 3, "forge");
        var forge = sim.Place("forge", x, y);
        Assert.NotNull(forge);
        Assert.Null(forge!.OwnerId);
        Assert.Equal(Zone.Industry, t.Lot(forge.LotId).Zone);
        Assert.Equal(Planning.StartingCash, forge.Cash);
        var (hx, hy) = Open(t, -5, -5, "town_hall");
        Assert.Null(sim.Place("town_hall", hx, hy));
    }

    [Fact]
    public void ThePlayerPicksTheTemplesSpot()
    {
        var sim = Planned();
        var t = sim.Town;
        var (x, y) = Open(t, 0, -6, "temple");
        Assert.Null(sim.Place("temple", x, y));
        // A big enough town with gold to spare.
        for (int i = 0; i < t.Content.Shrines.TemplePopulation; i++) Population.CreateAgent(t);
        t.Treasury = 5000;
        var temple = sim.Place("temple", x, y);
        Assert.NotNull(temple);
        Assert.Equal((x, y), (t.Lot(temple!.LotId).X, t.Lot(temple.LotId).Y));
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
