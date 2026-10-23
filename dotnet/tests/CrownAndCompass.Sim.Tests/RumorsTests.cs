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
            // Whatever a party has reported on, the town believes as it is, unless a menace nobody has named yet
            // has since grown in it: that change is secret until word of the menace spreads. A site someone else
            // cleared after the report may have new occupants the town has not seen.
            var hidden = sim.Town.Saga.Menaces.Where(m => !m.Named).Select(m => m.SeatId).ToHashSet();
            foreach (var s in sim.Town.Sites.Where(s => s.Known == SiteKnowledge.Confirmed && s.Trust == 100 && s.ThoughtCleared == s.Cleared && !hidden.Contains(s.Def.Id) && !(s.ClearedTick > s.ReportTick)))
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

    /// <summary>A report nobody renews goes back to hearsay: the town still knows where the place is, not that it stands empty.</summary>
    [Fact]
    public void AnOldReportGoesBackToHearsay()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        var t = sim.Town;
        sim.RunDays(2);
        var site = t.Sites.Where(s => !s.Held && s.TowerDue == null && Watch.LiveOf(t, s) == null).OrderByDescending(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();
        int limit = t.Content.Rumors.StaleDays * SimTime.HoursPerDay;
        Rumors.Confirm(t, site, "a test", cleared: true, asOf: t.Tick - limit + SimTime.HoursPerDay * 3);
        sim.RunDays(1);
        Assert.Equal(SiteKnowledge.Confirmed, site.Known);
        Assert.True(site.ThoughtCleared);
        sim.RunDays(3);
        Assert.Equal(SiteKnowledge.Rumoured, site.Known);
        Assert.False(site.ThoughtCleared);
        Assert.True(site.Found);
        Assert.True(site.Trust <= t.Content.Rumors.StaleTrust);
        Assert.Contains(t.Ledger.Entries, e => e.Format().Contains(site.Def.Name, StringComparison.Ordinal) && e.Format().Contains("half a year", StringComparison.Ordinal));
    }
}
