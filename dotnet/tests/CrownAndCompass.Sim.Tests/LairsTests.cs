using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LairsTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LairsGrowAndActByTheirKind(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(60);
        var t = sim.Town;
        Assert.Contains(t.Sites, s => !s.Cleared && s.Strength > t.Content.Lairs.StartStrength);
        var names = t.Sites.Select(s => s.Def.Name).ToList();
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.War && names.Any(n => e.Text.Contains(n, StringComparison.Ordinal)));
    }

    [Fact]
    public void ACrowdedLairPutsMoreInEveryFightButTheBoss()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var s = t.Sites.First(x => x.Def.Id != t.Content.Enemy.Outpost.Id);
        s.Strength = t.Content.Lairs.MaxStrength;
        Assert.Equal(t.Content.Lairs.MaxSwell, Lairs.Swell(t, s));
        s.Cleared = true;
        Assert.Equal(0, Lairs.Swell(t, s));
    }

    [Fact]
    public void ACleanedSiteIsResettledButTheTownStillThinksItEmpty()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        var t = sim.Town;
        sim.RunDays(1);
        var s = t.Sites.First(x => x.Known != SiteKnowledge.Unheard);
        s.Cleared = s.ThoughtCleared = true;
        s.ClearedTick = s.ResettleTick = t.Tick;
        sim.RunDays(1);
        Assert.False(s.Cleared);
        Assert.True(s.ThoughtCleared);
        Assert.Equal(t.Content.Lairs.StartStrength, s.Strength, 0);
        Assert.Contains(s.Def.Kind, t.Content.Lairs.Kinds.Keys);
        // Whatever moved in can be fought and named.
        Assert.False(string.IsNullOrEmpty(Rumors.Occupants(t.Content, s.Def)));
        Assert.True(t.Content.Monster(s.Def.Boss.Monster).Boss);
    }

    [Fact]
    public void AHeldSiteStaysEmptyAndInSight()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoMayor = false });
        var t = sim.Town;
        var s = t.Sites.First(x => x.Known != SiteKnowledge.Unheard);
        Assert.False(sim.HoldSite(s.Def.Id));
        s.Cleared = s.ThoughtCleared = true;
        s.ResettleTick = t.Tick + 24;
        t.Treasury = 1000;
        Assert.True(sim.HoldSite(s.Def.Id));
        Assert.Equal(1000 - t.Content.Lairs.Outpost.Cost, t.Treasury, 6);
        sim.RunDays(5);
        Assert.True(s.Cleared);
        Assert.Equal(LiveSource.Outpost, Watch.LiveOf(t, s));
    }

    [Fact]
    public void RaidersWaitWhileTheHeroesAreHomeAndComeWhenTheyLeave()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        while (t.Heroes.Count == 0 && t.Day < 200) sim.RunDays(1);
        Assert.NotEmpty(t.Heroes);
        foreach (var h in t.Heroes) h.Hero!.AwayUntil = null;
        var en = t.Enemy;
        en.Stage = ThreatStage.Raids;
        en.NextRaidTick = t.Tick;
        en.RaidWaited = 0;
        Assert.False(Threat.RaidTonight(t));
        Assert.Equal(1, en.RaidWaited);
        // Most of the heroes march out: the raiders come, even ahead of their day.
        en.NextRaidTick = t.Tick + 24;
        foreach (var h in t.Heroes) h.Hero!.AwayUntil = t.Tick + 100;
        Assert.True(Threat.RaidTonight(t));
        // And they never wait more than a few nights.
        foreach (var h in t.Heroes) h.Hero!.AwayUntil = null;
        en.NextRaidTick = t.Tick;
        en.RaidWaited = t.Content.Lairs.MaxWaitDays;
        Assert.True(Threat.RaidTonight(t));
    }
}
