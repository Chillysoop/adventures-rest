using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DarkQuestsTests
{
    private static (Simulation Sim, Menace Big) Crowned(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        m!.BigBad = true;
        sim.Town.Saga.BigBadId = m.Id;
        sim.Town.Saga.CrownDay = sim.Town.Day;
        return (sim, m);
    }

    /// <summary>Start a quest today and hand back the one it chose.</summary>
    private static DarkQuest Begun(Town t)
    {
        DarkQuests.State(t).NextDay = t.Day;
        DarkQuests.Daily(t);
        var q = DarkQuests.Current(t);
        Assert.NotNull(q);
        return q!;
    }

    [Fact]
    public void EveryQuestHasWordsAndGivesSomething()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var kinds = t.Content.Saga.Menaces.Kinds.Keys.ToHashSet();
        Assert.NotEmpty(t.Content.DarkQuests.Quests);
        foreach (var q in t.Content.DarkQuests.Quests)
        {
            Assert.NotEmpty(q.Things);
            Assert.All([q.Text, q.Done, q.Foiled], s => Assert.Contains("{", s));
            Assert.All(q.Kinds, k => Assert.Contains(k, kinds));
            var g = q.Gain;
            Assert.True(g.Host + g.Growth + g.Escapes + g.Sway + g.Pact + g.Dread > 0, q.Id);
        }
        // Every kind of big bad has quests enough to fill a campaign.
        foreach (var k in kinds)
            Assert.True(t.Content.DarkQuests.Quests.Count(q => q.Kinds.Count == 0 || q.Kinds.Contains(k)) >= t.Content.DarkQuests.Most, k);
    }

    [Fact]
    public void NothingHappensUntilTheBigBadIsCrowned()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(5);
        DarkQuests.State(sim.Town).NextDay = sim.Town.Day;
        DarkQuests.Daily(sim.Town);
        Assert.Null(DarkQuests.State(sim.Town).Current);
    }

    [Fact]
    public void TheTownHearsOfAQuestAndWhereItIs()
    {
        var (sim, _) = Crowned();
        var t = sim.Town;
        var q = Begun(t);
        var site = Quests.Site(t, q.SiteId)!;
        Assert.False(site.Cleared);
        Assert.NotEqual(SiteKnowledge.Unheard, site.Known);
        Assert.True(DarkQuests.Sought(t, site));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.DarkQuest && a.SiteId == site.Def.Id);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(site.Def.Name, StringComparison.Ordinal) && e.Importance == Importance.Major);
    }

    [Fact]
    public void ClearingTheSiteFirstFoilsIt()
    {
        var (sim, big) = Crowned();
        var t = sim.Town;
        var q = Begun(t);
        big.Power = 100;
        Quests.Site(t, q.SiteId)!.Cleared = true;
        DarkQuests.Daily(t);
        Assert.Null(DarkQuests.Current(t));
        Assert.True(Assert.Single(DarkQuests.State(t).Past).Foiled);
        Assert.Equal(85, big.Power, 3);
        Assert.Equal(0, DarkQuests.State(t).Host);
    }

    [Fact]
    public void AQuestLeftAloneMakesItsHostsGreater()
    {
        var (sim, _) = Crowned();
        var t = sim.Town;
        var q = Begun(t);
        q.Def = "relic";
        q.DueDay = t.Day;
        DarkQuests.Daily(t);
        Assert.False(Assert.Single(DarkQuests.State(t).Past).Foiled);
        Assert.Equal(DarkQuests.Def(t, "relic")!.Gain.Host, DarkQuests.State(t).Host);
    }

    [Fact]
    public void APactBindsAGreatPowerToIt()
    {
        var (sim, big) = Crowned();
        var t = sim.Town;
        foreach (var p in t.Powers) p.Side = PowerSide.Town;
        var q = Begun(t);
        q.Def = "pact";
        q.DueDay = t.Day;
        DarkQuests.Daily(t);
        Assert.Single(Powers.Backing(t, big));
    }

    [Fact]
    public void ItGoesOnNoMoreQuestsThanTheRulesAllow()
    {
        var (sim, _) = Crowned();
        var t = sim.Town;
        for (int i = 0; i < t.Content.DarkQuests.Most; i++)
        {
            var q = Begun(t);
            q.DueDay = t.Day;
            DarkQuests.Daily(t);
        }
        DarkQuests.State(t).NextDay = t.Day;
        DarkQuests.Daily(t);
        Assert.Null(DarkQuests.Current(t));
        Assert.Equal(t.Content.DarkQuests.Most, DarkQuests.State(t).Past.Count);
        Assert.Equal(DarkQuests.State(t).Past.Count, DarkQuests.State(t).Past.Select(x => x.Def).Distinct().Count());
    }
}
