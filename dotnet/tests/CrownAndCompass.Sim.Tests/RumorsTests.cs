using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class RumorsTests
{
    [Fact]
    public void TheTownStartsKnowingOnlyTheLocalMenace()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var known = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard).Select(s => s.Def.Id).ToList();
        Assert.Equal(t.Content.Sites.Where(d => d.Known).Select(d => d.Id), known);
        var far = t.Sites.First(s => s.Known == SiteKnowledge.Unheard);
        Assert.Null(Quests.PostBounty(t, far.Def.Id, 500));
        Assert.Null(Knowledge.CommissionScrying(t, far.Def.Id));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TavernsSpreadRumoursWithATellerAndAReliability(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(90);
        var t = sim.Town;
        Assert.NotEmpty(t.Rumors);
        Assert.All(t.Rumors, r =>
        {
            Assert.InRange(r.Reliability, 0, 100);
            Assert.False(string.IsNullOrEmpty(r.Source));
            Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Rumor && e.Text.Contains(r.Text));
        });
        // Rumours named places the town had never heard of.
        Assert.True(t.Sites.Count(s => s.Known != SiteKnowledge.Unheard) > t.Content.Sites.Count(d => d.Known));
    }

    [Fact]
    public void SomeRumoursAreFalseAndAReportSettlesThem()
    {
        var rumors = new List<Rumor>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(120);
            rumors.AddRange(sim.Town.Rumors);
            // Whatever a party has reported on, the town believes as it is.
            foreach (var s in sim.Town.Sites.Where(s => s.Known == SiteKnowledge.Confirmed && s.Trust == 100 && s.ThoughtCleared == s.Cleared))
                Assert.Equal(Rumors.Occupants(sim.Town.Content, s.Def), s.Believed);
        }
        Assert.Contains(rumors, r => !r.True);
        Assert.Contains(rumors, r => r.True);
        Assert.Contains(rumors, r => r.Proven == false);
    }

    [Fact]
    public void APlantedRumourIsBelievedUntilSomeoneLooks()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        var t = sim.Town;
        var site = t.Sites.First(s => s.Known == SiteKnowledge.Unheard);
        var r = Rumors.Plant(t, site, "a cloaked man", null, "A cloaked man swears dragons nest there.", 80, false, "dragons", 3);
        Assert.Equal(SiteKnowledge.Rumoured, site.Known);
        Assert.Equal("dragons", site.Believed);
        Assert.Equal(3, site.BelievedTier);
        Rumors.Confirm(t, site, "a scout");
        Assert.False(r.Proven);
        Assert.Equal(site.Def.Tier, site.BelievedTier);
    }
}
