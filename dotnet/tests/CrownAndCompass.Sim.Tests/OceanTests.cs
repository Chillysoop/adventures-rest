using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>The ocean on the campaign map: sea lands and their islands, the open water over them and the great island city.</summary>
public class OceanTests
{
    private static Town World(int seed) => new Simulation(new SimulationOptions { Seed = seed }).Town;

    [Fact]
    public void EveryWorldHasAnOceanOfIslandsPastTheTownsOwnLands()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = World(seed);
            var seas = t.Regions.Where(r => r.Theme.Sea).ToList();
            Assert.NotEmpty(seas);
            Assert.All(seas, r => Assert.True(r.Ring >= t.Content.World.Ocean.FromRing, $"seed {seed}: {r.Name} is sea in ring {r.Ring}"));
            Assert.Contains(t.Regions, r => !r.Theme.Sea && r.Ring >= 1);
            // Every sea land has its islands, wrecks and caves to find, under the same fog as the land.
            Assert.All(seas, r => Assert.NotEmpty(r.SiteIds));
            Assert.All(seas, r => Assert.False(r.Known));
            var water = t.Terrain.Where(f => f.Kind.Id == "ocean").ToList();
            Assert.NotEmpty(water);
            foreach (var s in t.Sites.Where(s => s.RegionId is int rid && t.Regions[rid].Theme.Sea))
            {
                var (x, y) = Systems.World.Spot(t, s);
                Assert.Contains(water, f => Roads.Contains(f, x, y));
            }
        }
    }

    [Fact]
    public void NoHardCountryOrWonderStandsInTheOpenSea()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = World(seed);
            var water = t.Terrain.Where(f => f.Kind.Id == "ocean").ToList();
            foreach (var f in t.Terrain.Where(f => f.Kind.Id != "ocean" && f.Kind.Id != "sea"))
            {
                double mid = (f.Inner + f.Outer) / 2, a = f.Bearing * Math.PI / 180;
                Assert.DoesNotContain(water, w => Roads.Contains(w, mid * Math.Sin(a), -mid * Math.Cos(a)));
            }
        }
    }

    [Fact]
    public void TheBoatsTakeACompanyOutToAnIsland()
    {
        var t = World(3);
        var isle = t.Sites.Where(s => s.RegionId is int rid && t.Regions[rid].Theme.Sea).OrderBy(s => s.Def.Tier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();
        var mind = new RouteMind(1, 1, 1, "Ada", null, false, false);
        var road = Roads.Plan(t, (0, 0), Systems.World.Spot(t, isle), mind, new Rng(1), []);
        Assert.Contains(road, w => w.Way == "sail" && w.Feature?.Kind.Id == "ocean");
    }

    [Fact]
    public void TheGreatIslandCityStandsInTheSeaAndGrows()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = World(seed);
            var city = Assert.Single(t.Settlements, s => s.Kin.Id == t.Content.World.Ocean.City.People);
            var region = t.Regions[city.RegionId];
            Assert.True(region.Theme.Sea);
            Assert.InRange(region.Ring, t.Content.World.Ocean.City.Rings[0], t.Content.World.Ocean.City.Rings[^1]);
            Assert.Equal(region.SettlementId, city.Id);
            Assert.Equal(t.Content.World.Ocean.City.Prosperity, city.Prosperity);
        }
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var c = sim.Town.Settlements.Single(s => s.Kin.Grows > 0);
        double early = Neighbours.ProsperityTarget(sim.Town, c);
        sim.RunDays(200);
        Assert.True(Neighbours.ProsperityTarget(sim.Town, c) > early + 10);
    }

    [Fact]
    public void ACoveFacesTheOceanAndItsPortsAreTheSeaLands()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed, Founding = "sea-cove" }).Town;
            Assert.True(Math.Abs(((Founding.Facing(t) - t.OceanBearing) % 360 + 540) % 360 - 180) <= t.OceanArc / 2, $"seed {seed}");
            foreach (var r in t.Regions.Where(r => r.Theme.Sea)) r.Known = true;
            Assert.Contains(Fleet.Ports(t), s => t.Regions[s.RegionId].Theme.Sea);
        }
    }
}
