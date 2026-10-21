using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Where the town is founded: the sites New game offers, what each does, and the ones left to others.</summary>
public class FoundingTests
{
    private static Town Founded(int seed, string founding) => new Simulation(new SimulationOptions { Seed = seed, Founding = founding }).Town;

    [Fact]
    public void TheRiverFordChangesNothing()
    {
        var a = new Simulation(new SimulationOptions { Seed = 4 });
        var b = new Simulation(new SimulationOptions { Seed = 4, Founding = "river-ford" });
        a.RunDays(20);
        b.RunDays(20);
        Assert.Null(b.Town.Founding);
        Assert.Equal(a.Town.Ledger.Entries.Select(e => e.Format()), b.Town.Ledger.Entries.Select(e => e.Format()));
    }

    [Fact]
    public void NewGameOffersTheFordAndSitesTheCountryAllows()
    {
        for (int seed = 1; seed <= 12; seed++)
        {
            var t = Founded(seed, "");
            var offered = Founding.Offered(t);
            Assert.Equal(t.Content.Founding.Default, offered[0].Def.Id);
            Assert.InRange(offered.Count, 2, t.Content.Founding.Offered);
            Assert.Equal(offered.Count, offered.Select(o => o.Def.Id).Distinct().Count());
            foreach (var o in offered.Where(o => o.Def.Near.Count > 0))
                Assert.Contains(t.Regions, g => g.Ring <= o.Def.Within && o.Def.Near.Contains(g.Theme.Id));
            // The ones not chosen stand on the map, held by neighbours or foes.
            Assert.Equal(offered.Count - 1, t.Regions.Count(g => g.Site != null));
            Assert.All(t.Regions.Where(g => g.Site != null), g => Assert.False(string.IsNullOrEmpty(Founding.Describe(t, g))));
        }
    }

    [Fact]
    public void AChosenSiteIsAmongTheOnesOffered()
    {
        var t = Founded(2, "isle");
        Assert.Equal("isle", t.Founding);
        Assert.Contains(Founding.Offered(t), o => o.Def.Id == "isle");
        Assert.DoesNotContain(t.Regions, g => g.Site == "isle");
    }

    [Fact]
    public void ACoveHasTheSeaAndTheTownsBoatsCrossIt()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = Founded(seed, "sea-cove");
            var sea = Assert.Single(t.Terrain, f => f.Kind.Boats);
            foreach (var s in t.Sites)
            {
                var (x, y) = World.Spot(t, s);
                Assert.False(Roads.Contains(sea, x, y), $"seed {seed}: {s.Def.Name} in the sea");
            }
            double a = sea.Bearing * Math.PI / 180, r = sea.Outer + 0.3;
            var mind = new RouteMind(1, 1, 1, "Ada", null, false, false);
            var road = Roads.Plan(t, (0, 0), (r * Math.Sin(a), -r * Math.Cos(a)), mind, new Rng(1), []);
            Assert.Contains(road, w => w.Way == "sail");
        }
    }

    [Fact]
    public void ASiteChangesTheTownsNumbers()
    {
        var ford = Founded(3, "");
        var crag = Founded(3, "crag");
        Assert.True(Threat.Fortification(crag) > Threat.Fortification(ford));
        Assert.True(Founding.Yield(crag, Good.Food) < 1);
        Assert.Equal(1, Founding.Yield(ford, Good.Food));
        Assert.True(Founding.Trade(Founded(3, "sea-cove")) > 1);
        Assert.Contains(crag.Ledger.Entries, e => e.Text.Contains("crag castle", StringComparison.Ordinal));
    }

    [Fact]
    public void AKeepStartsMannedOrInRuins()
    {
        bool manned = false, ruined = false;
        for (int seed = 1; seed <= 12; seed++)
        {
            var t = Founded(seed, "crag");
            Assert.Contains(t.Sites, s => s.Def.Ruin == "old-keep");
            var pass = Founded(seed, "pass-keep");
            if (Founding.Keep(pass) == "garrison")
            {
                manned = true;
                Assert.Contains(pass.Buildings, b => b.Def.Id == "keep");
            }
            else
            {
                ruined = true;
                Assert.Contains(pass.Sites, s => s.Def.Ruin == "old-keep" && Ruins.Of(pass, s) != null);
            }
        }
        Assert.True(manned && ruined);
    }

    [Fact]
    public void FoundingSurvivesASave()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, Founding = "plateau" });
        sim.RunDays(3);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal("plateau", loaded.Town.Founding);
        sim.RunDays(10);
        loaded.RunDays(10);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }
}
