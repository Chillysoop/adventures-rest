using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Floating isles, rope bridges over the gorges and ruins near town the council can restore.</summary>
public class LandmarksTests
{
    [Fact]
    public void FloatingIslesNeedWingsOrTheAirship()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(20);
        var t = sim.Town;
        var isle = t.Sites.First(s => s.Def.Aloft);
        Assert.InRange(t.Regions[isle.RegionId!.Value].Ring, 3, 4);
        var walkers = t.Heroes.Take(3).ToList();
        foreach (var h in walkers)
        {
            h.Hero!.Mount = "";
            foreach (var k in h.Hero.Magic.Select(e => e.Key).ToList()) h.Hero.Magic.Remove(k);
        }
        if (Passage.AirshipsFree(t) == 0) Assert.False(Passage.CanReach(t, walkers, isle));
        foreach (var h in walkers) h.Hero!.Mount = "griffon";
        Assert.True(Passage.CanReach(t, walkers, isle));
    }

    [Fact]
    public void GorgesHaveRopeBridgesRaidersCutAndMasonsRebuildInStone()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoMayor = false });
        var t = sim.Town;
        var gorges = t.Terrain.Where(f => f.Kind.Id == "gorge").ToList();
        Assert.All(gorges, f => Assert.Equal(Bridges.Rope, f.Bridge));
        if (gorges.Count == 0) return;
        var g = gorges[0];
        Assert.Equal(Ways.None, Bridges.Needs(g));
        g.Bridge = Bridges.Cut;
        Assert.Equal(Ways.Sheer, Bridges.Needs(g));
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"bridge:{g.Id}");
        t.Book(TreasuryAccount.Stories, 1000);
        Assert.True(sim.RebuildBridge(g.Id));
        Assert.Equal(Bridges.Stone, g.Bridge);
        Assert.False(sim.RebuildBridge(g.Id));
    }

    [Fact]
    public void AClearedRuinCanBeRestoredAndServesTheTown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        var ruins = t.Sites.Where(s => s.Def.Ruin != "").ToList();
        Assert.Equal(t.Content.World.Ruins.Count, ruins.Count);
        var r = ruins.First(s => Ruins.Of(t, s)!.Defence > 0 || Ruins.Of(t, s)!.SkyWatch > 0);
        Assert.False(sim.RestoreRuin(r.Def.Id));
        r.Cleared = true;
        t.Book(TreasuryAccount.Stories, 1000);
        double def = Ruins.Defence(t);
        int sky = Institutions.SkyWatch(t);
        Assert.True(sim.RestoreRuin(r.Def.Id));
        Assert.True(Ruins.Defence(t) > def || Institutions.SkyWatch(t) > sky);
        Assert.False(sim.RestoreRuin(r.Def.Id));
    }
}
