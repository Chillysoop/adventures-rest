using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DensityTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    /// <summary>The middle of the first open, unpainted tile of the old town at least this far from the hall.</summary>
    private static (double X, double Y) OpenSpot(Town t, double from = 4)
    {
        for (int y = 0; y < Town.Grid; y++)
            for (int x = 0; x < Town.Grid; x++)
            {
                double px = x + 0.5, py = y + 0.5, d = Math.Max(Math.Abs(px - Town.Center), Math.Abs(py - Town.Center));
                int i = (int)(py * Land.Res) * Land.Size + (int)(px * Land.Res);
                if (d >= from && City.IsOpen(t, px, py) && t.Plan.Zones[i] == (byte)Zone.None && t.Plan.Taken[i] == 0) return (px, py);
            }
        throw new InvalidOperationException("no open ground");
    }

    [Fact]
    public void DenserZonesUnlockWithTheCharters()
    {
        var sim = Planned();
        var t = sim.Town;
        var at = OpenSpot(t);
        Assert.Equal(Density.Low, Density.Unlocked(t));
        Assert.Equal("medium density needs the town to be a town", Density.Blocked(t, Density.Medium));
        Assert.Equal(0, sim.PaintZone([at], 1, Zone.Residential, Density.Medium));
        t.City.Tier = City.TierIndex(t, "town");
        Assert.True(sim.PaintZone([at], 1, Zone.Residential, Density.Medium) > 0);
        Assert.Equal(Density.Medium, Density.At(t, at.X, at.Y));
        Assert.NotNull(Density.Blocked(t, Density.High));
        t.City.Tier = City.TierIndex(t, "large_town");
        Assert.Equal(Density.High, Density.Unlocked(t));
    }

    [Fact]
    public void FieldsAndWorkshopsAreNeverBuiltDense()
    {
        var sim = Planned();
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "large_town");
        var at = OpenSpot(t);
        Assert.True(sim.PaintZone([at], 1, Zone.Industry, Density.High) > 0);
        Assert.Equal(Density.Low, Density.At(t, at.X, at.Y));
    }

    [Fact]
    public void RepaintingAnOldStreetDenserLetsItsHousesGrowTaller()
    {
        var sim = Planned();
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "large_town");
        var house = t.Buildings.First(b => b.Def.Id == "house");
        var lot = t.Lot(house.LotId);
        house.Def = t.Content.Building("townhouse");
        var h = City.Housing(t, "townhouse")!;
        Assert.Equal("manor", City.NextFor(t, house, h));
        // Built land of the zone takes the new density.
        Assert.True(sim.PaintZone([(lot.X, lot.Y)], 0.5, Zone.Residential, Density.High) > 0);
        Assert.Equal(Density.High, Density.At(t, lot));
        Assert.Equal("tall_house", City.NextFor(t, house, h));
        Assert.Equal(house.Def.Id, City.Previous(t, "tall_house")!.Id);
    }

    [Fact]
    public void TimberHousesBecomeTownhousesOnAnyLand()
    {
        var sim = Planned();
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "town");
        var house = t.Buildings.First(b => b.Def.Id == "house");
        house.Def = t.Content.Building("timber_house");
        Assert.DoesNotContain("density", City.UpgradeBlocked(t, house) ?? "");
    }

    [Fact]
    public void HighDensityBuildsWallToWall()
    {
        var sim = Planned();
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "large_town");
        var tall = t.Content.Building("tall_house");
        var (w, d) = Land.Footprint(tall);
        // Find a spot in fresh paint, claim it, then try its neighbour one footprint over: only dense land takes it.
        // A few spots in turn, since one may sit against a lane or the edge of the ward.
        Lot? lot = null;
        bool dense = false;
        foreach (double from in new[] { 5.0, 4, 6, 3 })
        {
            var at = OpenSpot(t, from);
            // Zones paint open ground only, so clear whatever else is near first.
            sim.PaintZone([at], 2.5, Zone.None);
            sim.PaintZone([at], 2.5, Zone.Residential, Density.High);
            lot = Land.Find(t, tall, Zone.Residential);
            Assert.NotNull(lot);
            double ux = Math.Cos(lot!.Yaw), uy = Math.Sin(lot.Yaw), step = w / (double)Land.Res;
            Land.Claim(t, lot);
            dense = Land.Fits(t, lot.X + ux * step, lot.Y + uy * step, w, d, lot.Yaw, Zone.Residential)
                || Land.Fits(t, lot.X - ux * step, lot.Y - uy * step, w, d, lot.Yaw, Zone.Residential);
            if (dense) break;
        }
        Assert.True(dense, "a tall house fits right against its neighbour");
        Assert.True(Density.LandFactor(t, lot!) > 1);
        Assert.True(Density.Mood(t, t.AddBuilding(tall, lot!, null, "test", true)) < 0);
    }

    [Fact]
    public void LeftToItselfACityGrowsADenseCoreAndLowerSprawl()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        for (int d = 0; d < 1000 && sim.Town.Outcome == null && sim.Town.City.Tier < City.TierIndex(sim.Town, "city"); d++) sim.RunDays(1);
        sim.RunDays(30);
        var t = sim.Town;
        Assert.True(t.City.Tier >= City.TierIndex(t, "large_town"), $"still a {City.Tier(t).Name}");
        double Mean(int ring)
        {
            var houses = t.Buildings.Where(b => b.Def.Housing > 0 && b.Complete && City.Ring(t, t.Lot(b.LotId)) == ring).ToList();
            return houses.Count == 0 ? 0 : houses.Average(b => Density.At(t, t.Lot(b.LotId)));
        }
        Assert.True(Mean(0) > Mean(2), $"old town {Mean(0)} vs outer wards {Mean(2)}");
        Assert.Contains(t.Buildings, b => b.Def.Id == "tall_house");
    }
}
