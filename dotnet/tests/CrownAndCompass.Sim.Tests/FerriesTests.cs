using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>The great river: it rises in the mountains and runs to the sea, and the town's ferries cross it for everyone.</summary>
public class FerriesTests
{
    [Fact]
    public void TheRiverRisesInMountainsOrRunsToTheSea()
    {
        int sourced = 0;
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            var line = Passage.River(t);
            Assert.True(line[0].Y < 0 && line[^1].Y > 0);
            // Past the town it runs straight by the ford it was founded on.
            var home = line.OrderBy(p => Math.Abs(p.Y)).First();
            Assert.InRange(home.X, 0, 0.5);
            bool Ends((double X, double Y) p) => t.Terrain.Any(f => f.Kind.Id is "ocean" or "range" or "peaks" && Roads.Contains(f, p.X, p.Y, 0.6));
            if (Ends(line[0]) || Ends(line[^1])) sourced++;
            Assert.NotEmpty(Passage.Crossings(t));
        }
        Assert.True(sourced >= 3, $"only {sourced} of 8 rivers rise in mountains or reach the sea");
    }

    [Fact]
    public void AFerryCrossesALongReachForEveryoneUntilItIsCut()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoMayor = false });
        var t = sim.Town;
        t.Treasury = 5000;
        var spots = Ferries.Spots(t);
        Assert.NotEmpty(spots);
        int before = Passage.Crossings(t).Count;
        Assert.True(sim.BuildFerry(0));
        var f = Assert.Single(t.Ferries);
        Assert.Equal(before + 1, Passage.Crossings(t).Count);
        Assert.Contains(Passage.Crossings(t), c => c.Kind == "ferry" && c.Name == f.Name);
        // A way straight over the river right by the ferry goes by it.
        var route = Passage.Ford(t, (f.X - 0.6, f.Y), [new Waypoint { X = f.X + 0.6, Y = f.Y }]);
        Assert.Contains(route, w => w.Crossing == f.Name);
        // Cut, nobody uses it; put back, it runs again.
        Assert.True(sim.CutFerry(f.Id));
        Assert.DoesNotContain(Passage.Crossings(t), c => c.Kind == "ferry");
        Assert.True(sim.RebuildFerry(f.Id));
        Assert.False(f.Cut);
        // No second ferry on the same reach.
        Assert.True(Ferries.Spots(t).Count < spots.Count);
    }

    [Fact]
    public void ARiverTownKeepsRiverBoatsThatTradeAlongTheWater()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("river_harbour");
        Assert.Null(City.Locked(t, d) is "a town on the sea builds a harbour" ? "x" : null);
        var lot = Land.Find(t, d, null)!;
        lot.Zone = Zone.Civic;
        t.AddBuilding(d, lot, null, "the River Harbour", true);
        foreach (var g in t.Regions) g.Known = true;
        t.Treasury = 20000;
        t.City.Tier = City.TierIndex(t, "town");
        sim.RunDays(1);
        Assert.True(Fleet.OnRiver(t));
        Assert.Single(Fleet.AtSea(t));
        Assert.Equal("too big for the river", Fleet.BuildBlocked(t, "carrack"));
        Assert.NotNull(Fleet.Build(t, "longship"));
        // Its ports lie along the water; the open sea only where the river runs to it.
        var line = Passage.River(t);
        foreach (var s in Fleet.Ports(t))
        {
            var g = t.Regions[s.RegionId];
            double b = g.Bearing * Math.PI / 180, x = g.Days * Math.Sin(b), y = -g.Days * Math.Cos(b);
            bool near = line.Min(p => Math.Abs(p.X - x) + Math.Abs(p.Y - y)) <= t.Content.Sea.River.Reach;
            Assert.True(near || Passage.Mouth(t));
        }
        Assert.Equal(Passage.Mouth(t), Fleet.ToSea(t));
        // A sea town builds a harbour, not a river harbour.
        var cove = new Simulation(new SimulationOptions { Seed = 3, Founding = "sea-cove" }).Town;
        cove.City.Tier = City.TierIndex(cove, "city");
        Assert.Equal("a town on the sea builds a harbour", City.Locked(cove, d));
    }
}
