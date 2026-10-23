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
    public void TheHeartlandPinsASiteOfEveryKindWhereItsCountrySuitsIt()
    {
        var r = Content.Default().Founding;
        for (int seed = 1; seed <= 12; seed++)
        {
            var t = Founded(seed, "");
            var spots = Founding.Spots(t);
            Assert.Equal(r.Kinds.Select(k => k.Id).Order(StringComparer.Ordinal), spots.Select(p => p.Kind).Order(StringComparer.Ordinal));
            var ford = spots.Single(p => p.Kind == r.Default);
            Assert.Equal((0.0, 0.0), (ford.X, ford.Y));
            Assert.Equal("town", ford.Holder);
            foreach (var p in spots.Where(p => p != ford))
            {
                Assert.InRange(Js.Hypot(p.X, p.Y), 0.5, r.Heartland + 0.01);
                Assert.Null(Founding.Blocked(t, p.X, p.Y));
                Assert.Contains(Founding.KindsAt(t, p.X, p.Y), k => k.Id == p.Kind);
                Assert.All(spots.Where(o => o != p), o => Assert.True(Js.Hypot(o.X - p.X, o.Y - p.Y) >= r.Apart - 0.02, $"seed {seed}: {p.Kind} and {o.Kind} share ground"));
            }
            // The coast's sites lie on the real shore, where the world has an ocean.
            if (t.OceanArc > 0)
                foreach (var p in spots.Where(p => Founding.Kind(t.Content, p.Kind)!.Shore))
                    Assert.Contains(t.Terrain, f => f.Kind.Id == "ocean" && Roads.Contains(f, p.X, p.Y, r.Shore));
        }
    }

    [Fact]
    public void TheSitesPassedOverAreHeldOrLieEmptyAndAreTakenLater()
    {
        int people = 0, foes = 0, empty = 0;
        for (int seed = 1; seed <= 12; seed++)
        {
            var t = Founded(seed, "");
            foreach (var p in t.FoundingSpots.Where(p => p.Holder != "town"))
            {
                if (p.Holder == "people") people++;
                else if (p.Holder == "foes") foes++;
                else empty++;
                Assert.False(string.IsNullOrEmpty(Founding.Describe(t, p)));
            }
            Assert.All(t.Regions.Where(g => g.Site != null), g => Assert.False(string.IsNullOrEmpty(Founding.Describe(t, g))));
        }
        Assert.True(people > 0 && foes > 0 && empty > 0, $"{people} held by people, {foes} by foes, {empty} empty");

        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        int before = sim.Town.FoundingSpots.Count(p => p.Holder == "");
        sim.RunDays(400);
        Assert.True(sim.Town.FoundingSpots.Count(p => p.Holder == "") < before);
        Assert.Contains(sim.Town.FoundingSpots, p => p.Day > 0);
    }

    [Fact]
    public void AChosenKindIsFoundedOnItsPinAwayFromTheHeart()
    {
        var t = Founded(2, "isle");
        Assert.Equal("isle", t.Founding);
        var pin = t.FoundingSpots.Single(p => p.Kind == "isle");
        Assert.Equal("town", pin.Holder);
        Assert.Equal(-pin.X, t.HeartX, 6);
        Assert.Equal(-pin.Y, t.HeartY, 6);
        Assert.DoesNotContain(t.Regions, g => g.Site == "isle");
    }

    [Fact]
    public void TheWildsStayWhereTheyLieAndTheTownWalksToThemFromItsOwnGround()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var heart = Founded(seed, "");
            var pin = heart.FoundingSpots.First(p => p.Kind == "crag");
            var t = Founded(seed, "crag");
            Assert.Equal(heart.Sites.Count, t.Sites.Count);
            foreach (var s in t.Sites.Where(s => s.Def.Distance > 0))
            {
                var h = heart.Sites.Single(x => x.Def.Id == s.Def.Id);
                // The same place in the world, at the same danger...
                var (x, y) = World.Spot(t, s);
                var (hx, hy) = World.Spot(heart, h);
                Assert.Equal(hx - pin.X, x, 6);
                Assert.Equal(hy - pin.Y, y, 6);
                Assert.Equal(h.Def.Tier, s.Def.Tier);
                // ...and as many days from the town as it lies.
                Assert.Equal(Math.Max(1, (int)Math.Round(Js.Hypot(x, y))), s.Def.Distance);
            }
            // The country nearest the town is the country it knows.
            var known = t.Regions.Where(g => g.Known).ToList();
            Assert.NotEmpty(known);
            Assert.True(known.Max(g => g.Days) <= t.Regions.Where(g => !g.Known).Min(g => g.Days));
            // Hard country keeps clear of the town's own ground.
            Assert.DoesNotContain(t.Terrain, f => f.Kind.Id != "ocean" && Roads.Contains(f, 0, 0, 0.2));
        }
    }

    [Fact]
    public void ATownFoundedFarOutHasHarderCountryCloser()
    {
        var heart = Founded(4, "");
        // The farthest open spot on land that takes a river ford.
        var (x, y) = Enumerable.Range(0, 72).Select(i => (B: i * 5.0, R: 3.4))
            .Select(p => (X: p.R * Math.Sin(p.B * Math.PI / 180), Y: -p.R * Math.Cos(p.B * Math.PI / 180)))
            .First(p => Founding.KindsAt(heart, p.X, p.Y).Any(k => k.Id == "river-ford"));
        var far = new Simulation(new SimulationOptions { Seed = 4, HomeX = x, HomeY = y }).Town;
        Assert.Equal(-x, far.HeartX, 6);
        double Nearest(Town t, int level) => t.Sites.Where(s => s.Def.Distance > 0 && s.Def.Tier >= level).Min(s => Js.Hypot(World.Spot(t, s).X, World.Spot(t, s).Y));
        Assert.True(Nearest(far, 5) < Nearest(heart, 5));
        // Too deep in the wilds, or out at sea, the kind's own pin is taken instead.
        var deep = new Simulation(new SimulationOptions { Seed = 4, HomeX = 30, HomeY = 0 }).Town;
        Assert.Equal(0, deep.HeartX);
        Assert.NotNull(Founding.Blocked(heart, 30, 0));
    }

    [Fact]
    public void ATownFoundedAwayFromTheHeartSurvivesASave()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 6, Founding = "pass-keep" });
        sim.RunDays(30);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(sim.Town.HeartX, loaded.Town.HeartX);
        Assert.Equal(sim.Town.Terrain.Select(f => (f.Ox, f.Oy)), loaded.Town.Terrain.Select(f => (f.Ox, f.Oy)));
        sim.RunDays(30);
        loaded.RunDays(30);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }

    [Fact]
    public void ACoveHasTheSeaAndTheTownsBoatsCrossIt()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = Founded(seed, "sea-cove");
            var sea = Assert.Single(t.Terrain, f => f.Kind.Id == "sea");
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

    [Fact]
    public void ThePlayerNamesTheTownAndTheNameIsKept()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, TownName = "  Kestrel   [b]Rise[/b] " });
        Assert.Equal("Kestrel bRise/b", sim.Town.Name);
        Assert.Equal("Briarford", new Simulation(new SimulationOptions { Seed = 3, TownName = "   " }).Town.Name);
        Assert.True(Founding.CleanName(sim.Town.Content, new string('x', 99))!.Length <= sim.Town.Content.Founding.NameMost);
        Assert.NotEmpty(sim.Town.Content.Founding.Names);
        sim.RunDays(3);
        Assert.Equal(sim.Town.Name, SaveGame.Read(SaveGame.Write(sim)).Town.Name);
    }
}
