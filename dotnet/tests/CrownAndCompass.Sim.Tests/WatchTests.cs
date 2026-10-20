using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class WatchTests
{
    private static Simulation Town(int seed, Func<Simulation, bool> until, int days = 150)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = false });
        for (int d = 0; d < days; d++)
        {
            if (until(sim)) return sim;
            sim.RunDays(1);
        }
        throw new InvalidOperationException("Condition never met");
    }

    private static Simulation WithScout() =>
        Enumerable.Range(1, 20).Select(seed =>
        {
            try { return Town(seed, s => Watch.Watchers(s.Town).Count > 0, 60); }
            catch (InvalidOperationException) { return null; }
        }).First(s => s != null)!;

    [Fact]
    public void AScoutShowsTheSiteLiveOnlyWhileWatching()
    {
        var sim = WithScout();
        var t = sim.Town;
        var site = t.Sites.First(s => !s.Cleared);
        sim.Town.Treasury += 500;
        var scout = sim.PostScout(site.Def.Id);
        Assert.NotNull(scout);
        Assert.Null(sim.PostScout(site.Def.Id));
        Assert.True(t.Agent(scout!.HeroId).IsAway);
        Assert.Null(Watch.LiveOf(t, site));
        while (t.Tick < scout.ArriveTick) sim.Step();
        // Unless it was seen off the moment it got there.
        if (scout.Out && t.Tick < scout.LeaveTick) Assert.Equal(LiveSource.Scout, Watch.LiveOf(t, site));
        while (scout.Out) sim.Step();
        Assert.True(scout.Lost || !t.Agent(scout.HeroId).IsAway);
        Assert.NotEqual(LiveSource.Scout, Watch.LiveOf(t, site));
    }

    [Fact]
    public void ARecalledScoutWalksBackTheWayItCame()
    {
        var sim = WithScout();
        var t = sim.Town;
        var scout = sim.PostScout(t.Sites.First(s => !s.Cleared).Def.Id)!;
        for (int i = 0; i < 5; i++) sim.Step();
        Assert.True(sim.RecallScout(scout.Id));
        Assert.Equal(t.Tick + 5, scout.ReturnTick);
        Assert.Equal(t.Tick + 5, t.Agent(scout.HeroId).Hero!.AwayUntil);
    }

    [Fact]
    public void AMirrorGivesALastingLiveView()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            Simulation sim;
            try { sim = Town(seed, s => Watch.MirrorCasters(s.Town).Count > 0, 150); }
            catch (InvalidOperationException) { continue; }
            var t = sim.Town;
            t.Treasury += 1000;
            var site = t.Sites.First(s => !s.Cleared && s.Known != SiteKnowledge.Unheard);
            var errand = sim.PlantMirror(site.Def.Id);
            Assert.NotNull(errand);
            Assert.Null(sim.PlantMirror(site.Def.Id));
            while (!errand!.Arrived && !errand.Lost) sim.RunDays(1);
            Assert.Contains(t.Mirrors, m => m.SiteId == site.Def.Id);
            // The lair may find it the very first night; try another town if so.
            if (Watch.MirrorAt(t, site.Def.Id) == null) continue;
            Assert.Equal(LiveSource.Mirror, Watch.LiveOf(t, site));
            return;
        }
        Assert.Fail("No mirror caster in 30 seeds");
    }

    [Fact]
    public void TheScryingPoolScriesForFreeOnceADay()
    {
        var sim = Town(1, s => s.Town.Population >= 30);
        var t = sim.Town;
        t.Treasury += 3000;
        var pool = sim.BuildSightWork("scrying_pool");
        Assert.NotNull(pool);
        Assert.Null(sim.BuildSightWork("scrying_pool"));
        while (!pool!.Complete) sim.RunDays(1);
        sim.RunDays(1);
        Assert.Equal(1, Watch.FreeScryingsLeft(t));
        double before = t.Treasury;
        var sites = t.Sites.Where(s => Watch.LiveOf(t, s) == null).ToList();
        Assert.NotNull(sim.CommissionScrying(sites[0].Def.Id));
        Assert.Equal(before, t.Treasury);
        Assert.Equal(0, Watch.FreeScryingsLeft(t));
    }

    [Fact]
    public void TheSpireShowsNearSitesAndARelicCountsAsItsBuilding()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        Assert.Empty(Watch.LiveSites(t));
        Watch.ReceiveRelic(t, t.Content.Library.Item("eye-of-the-sleepless-spire")!, "Test Company");
        var live = Watch.LiveSites(t);
        Assert.Equal(t.Sites.Where(s => s.Def.Distance <= 2).Select(s => s.Def.Id), live.Select(l => l.Site.Def.Id));
        Assert.All(live, l => Assert.Equal(LiveSource.Spire, l.Source));
    }
}
