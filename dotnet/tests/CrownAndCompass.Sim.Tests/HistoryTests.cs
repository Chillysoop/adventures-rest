using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class HistoryTests
{
    private static readonly Content C = Content.Default();
    private static HistoryRules H => C.History;

    [Fact]
    public void EveryRegionThemeAndPeopleHasAPlaceInHistory()
    {
        foreach (var theme in C.World.Themes)
        {
            Assert.True(theme.History.Count > 0, $"{theme.Id} has no history");
            Assert.Single(H.Lanes, l => l.Themes.Contains(theme.Id));
        }
        foreach (var people in C.World.Peoples) Assert.False(people.History == "", $"{people.Id} has no history");
        foreach (var lane in H.Lanes)
        {
            Assert.All(lane.Themes, id => Assert.Contains(C.World.Themes, t => t.Id == id));
            Assert.NotEmpty(Annals.InLane(H, lane.Id));
        }
    }

    [Fact]
    public void TheAgesRunUnbrokenToTheCharter()
    {
        for (int i = 1; i < H.Ages.Count; i++) Assert.Equal(H.Ages[i - 1].To, H.Ages[i].From);
        Assert.Equal(H.Year, H.Ages[^1].To);
        Assert.All(H.Events, e => Assert.InRange(e.Year, H.Ages[0].From, H.Year));
    }

    [Fact]
    public void PowersSpanNeighbouringLanesAndNeverOverlapInOne()
    {
        var order = H.Lanes.Select(l => l.Id).ToList();
        foreach (var p in H.Powers)
        {
            Assert.True(p.From < p.To, p.Id);
            Assert.InRange(p.From, H.Ages[0].From, H.Year);
            Assert.InRange(p.To, H.Ages[0].From, H.Year);
            var idx = p.Lanes.Select(l => order.IndexOf(l)).ToList();
            Assert.DoesNotContain(-1, idx);
            Assert.Equal(Enumerable.Range(idx[0], idx.Count), idx);
        }
        foreach (var lane in H.Lanes)
        {
            var held = Annals.InLane(H, lane.Id).ToList();
            for (int i = 1; i < held.Count; i++) Assert.True(held[i - 1].To <= held[i].From, $"{held[i - 1].Id} overlaps {held[i].Id}");
        }
    }

    [Fact]
    public void YearsReadBeforeAndAfterTheLamp()
    {
        Assert.Equal("1180 BL", Annals.Year(H, -1180));
        Assert.Equal("724 AL", Annals.Year(H, 724));
        Assert.Equal("The Age of Crowns", Annals.AgeOf(H, -140)!.Name);
    }

    [Fact]
    public void TheCharterRemembersTheFordKing()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1, AutoMayor = false }).Town;
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("Aldren the Ford-King") && e.Text.Contains($"{H.Year} AL"));
    }

    [Fact]
    public void AFirstVisitAndAnAllianceTellTheOldStories()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        var region = t.Regions.First(r => !r.Visited);
        var party = new Party { Name = "Grey Company", Members = [], SiteId = region.SiteIds[0] };
        World.Describe(t, party, region);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"old story of {region.Name}") && e.Text.Contains(Annals.Story(region)!));
        World.Describe(t, party, region);
        Assert.Single(t.Ledger.Entries, e => e.Text.Contains($"old story of {region.Name}"));

        var s = t.Settlements.First();
        World.Shift(t, s, 100);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("remember the old days") && e.Text.Contains(s.Kin.History));
    }

    [Fact]
    public void TheOldPowersThatWakeAreTiedToTheHistory()
    {
        Assert.All(C.Saga.Finale.Fallback, f => Assert.False(f.Text == "", f.Leader));
        // The goddess of knowledge and the lich queen no longer share a name.
        Assert.DoesNotContain(C.Saga.Finale.Fallback, f => f.Leader.Contains("Ysolde"));
    }
}
