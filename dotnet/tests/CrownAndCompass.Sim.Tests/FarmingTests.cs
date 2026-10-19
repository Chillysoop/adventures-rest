using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FarmingTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    /// <summary>The middle of the first open, unpainted tile of the town at least this far from the hall.</summary>
    private static (double X, double Y) OpenSpot(Town t, double from = 5)
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
    public void EveryCropHasAFarmOnFarmland()
    {
        var c = Content.Default();
        Assert.True(c.Farming.Crops.Count >= 7);
        Assert.Equal("wheat", c.Farming.Crops[0].Id);
        foreach (var crop in c.Farming.Crops)
        {
            var d = c.Building(crop.Building);
            Assert.Equal(Zone.Farmland, d.Zone);
            Assert.NotEmpty(d.Produces);
            Assert.All(crop.Seasons.Keys, s => Assert.Contains(c.Seasons.Seasons, x => x.Id == s));
        }
        // Every farm grows exactly one crop, so a field always says what goes up on it.
        Assert.All(c.Buildings.Where(d => d.Zone == Zone.Farmland && d.Buildable), d => Assert.Single(c.Farming.Crops, x => x.Building == d.Id));
    }

    [Fact]
    public void AFieldIsSownWithOneCropAndOnlyItsFarmGoesUpThere()
    {
        var sim = Planned();
        var t = sim.Town;
        // The charter's field is wheat: resow the open part of it with root crops.
        var open = Land.Find(t, t.Content.Building("farm"), Zone.Farmland)!;
        (double X, double Y) at = (open.X, open.Y);
        Assert.Equal("wheat", Farming.At(t, at.X, at.Y)?.Id);
        Assert.True(sim.PaintZone([at], 3, Zone.Farmland, crop: "roots") > 0);
        Assert.Equal("roots", Farming.At(t, at.X, at.Y)?.Id);
        var wheat = Land.Find(t, t.Content.Building("farm"), Zone.Farmland);
        Assert.True(wheat == null || Farming.At(t, wheat.X, wheat.Y)?.Id != "roots", "a wheat farm would go up in the root field");
        var lot = Land.Find(t, t.Content.Building("root_farm"), Zone.Farmland);
        Assert.NotNull(lot);
        Assert.Equal("roots", Farming.At(t, lot!.X, lot.Y)?.Id);

        // Resown as wheat, the field takes a wheat farm.
        Assert.True(sim.PaintZone([at], 2, Zone.Farmland, crop: "wheat") > 0);
        Assert.Equal("wheat", Farming.At(t, at.X, at.Y)?.Id);
    }

    [Fact]
    public void CropsUnlockWithTheirFarmsCharter()
    {
        var sim = Planned();
        var t = sim.Town;
        var at = OpenSpot(t);
        var orchard = Farming.Crop(t, "orchard")!;
        Assert.NotNull(Farming.Blocked(t, orchard));
        Assert.Equal(0, sim.PaintZone([at], 1, Zone.Farmland, crop: "orchard"));
        t.City.Tier = City.TierIndex(t, "village");
        Assert.Null(Farming.Blocked(t, orchard));
        Assert.True(sim.PaintZone([at], 1, Zone.Farmland, crop: "orchard") > 0);
    }

    [Fact]
    public void CropsKeepTheirOwnSeasons()
    {
        var sim = Planned();
        var t = sim.Town;
        double Yield(string building, string season) =>
            Farming.Yield(t, t.Content.Building(building), Good.Food, t.Content.Seasons.Seasons.First(s => s.Id == season));
        // Orchards bear at harvest; root crops carry the town through winter, when wheat fields lie bare.
        Assert.True(Yield("orchard", "autumn") > 1.5 * Yield("orchard", "spring"));
        Assert.True(Yield("root_farm", "winter") > Yield("farm", "winter"));
        Assert.True(Yield("farm", "summer") > Yield("root_farm", "summer"));
    }

    [Fact]
    public void TheStandInMayorSowsMoreThanWheat()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(300);
        var t = sim.Town;
        var crops = new HashSet<byte>();
        for (int i = 0; i < t.Plan.Crops.Length; i++)
            if (t.Plan.Zones[i] == (byte)Zone.Farmland && t.Plan.Crops[i] != 0) crops.Add(t.Plan.Crops[i]);
        Assert.True(crops.Count >= 2, $"crops sown: {crops.Count}");
    }
}
