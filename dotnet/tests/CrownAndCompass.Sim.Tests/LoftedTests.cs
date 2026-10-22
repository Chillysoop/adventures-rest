using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LoftedTests
{
    private static Simulation Player(int seed = 3) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = false, AutoStories = false, FreeCompanies = false });

    [Fact]
    public void TheRelicsLieAtDistinctFarSitesAndComeHomeWhenOneIsCleared()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var rules = t.Content.Lofted;
        Assert.Equal(rules.Relics.Count, t.Lofted.Relics.Count);
        Assert.Equal(t.Lofted.Relics.Count, t.Lofted.Relics.Select(r => r.SiteId).Distinct().Count());
        foreach (var rs in t.Lofted.Relics) Assert.True(World.RegionOf(t, Quests.Site(t, rs.SiteId)!)!.Ring >= rules.MinRing);

        var first = t.Lofted.Relics[0];
        Assert.False(Sparks.Check(t, $"relic:{first.Id}").Holds);
        Quests.Site(t, first.SiteId)!.Cleared = true;
        sim.RunDays(1);
        Assert.Equal("town", first.Holder);
        Assert.True(Sparks.Check(t, $"relic:{first.Id}").Holds);
        Assert.True(Sparks.Check(t, "relics:1").Holds);
        Assert.Contains(t.Research.Feed, n => n.Icon == Sparks.Relic && n.Lore > 0);
    }

    [Fact]
    public void TheCityIsLockedUntilLoftingAndTheTownRaisesOnlyOneSize()
    {
        var sim = Player();
        var t = sim.Town;
        var sizes = t.Content.Buildings.Where(d => d.Lofted).ToList();
        Assert.Equal(3, sizes.Count);
        Assert.All(sizes, d => Assert.True(d.IsGrand));
        Assert.All(sizes, d => Assert.NotNull(Lofted.Locked(t, d)));
        t.Research.Known.Add(t.Content.Lofted.Unlock);
        Assert.All(sizes, d => Assert.Null(Lofted.Locked(t, d)));
        t.AddBuilding(sizes[0], Planning.CivicLot(t, sizes[0])!, null, "the Lofted Spire", false);
        Assert.Null(Lofted.Locked(t, sizes[0]));
        Assert.NotNull(Lofted.Locked(t, sizes[1]));
        Assert.NotNull(Lofted.Locked(t, sizes[2]));
        // Machinery first, then the city, and the last stage is the lofting itself.
        Assert.All(sizes, d => Assert.Equal("the lofting", d.Stages[^1].Name));
    }

    [Fact]
    public void AFoeRacesForTheRelicsAndBreakingItGivesThemBack()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var kind = t.Content.Saga.Menaces.Kinds.Keys.OrderBy(k => k, StringComparer.Ordinal).First();
        var m = Menaces.Rise(t, kind, "Vessra", 12, "test")!;
        m.Ambition = "conquest";
        m.Power = 1e6;
        t.Research.Known.Add(t.Content.Lofted.Reveal);
        sim.RunDays(1);
        Assert.Equal(m.Id, t.Lofted.RivalId);
        // The town hears where they lie.
        Assert.All(t.Lofted.Relics, rs => Assert.NotEqual(SiteKnowledge.Unheard, Quests.Site(t, rs.SiteId)!.Known));

        var race = t.Content.Lofted.Race;
        sim.RunDays(race.FirstAfter + 1);
        var taken = t.Lofted.Relics.Where(r => r.Holder == $"menace:{m.Id}").ToList();
        Assert.Single(taken);
        Assert.Equal(m.Name, Lofted.HolderName(t, taken[0]));

        Menaces.End(t, m);
        sim.RunDays(1);
        Assert.Equal("town", taken[0].Holder);
    }

    [Fact]
    public void AFinishedCitySailsAndCompaniesSetOutFromUnderIt()
    {
        var sim = Player();
        var t = sim.Town;
        t.Research.Known.Add(t.Content.Lofted.Unlock);
        var d = t.Content.Building("lofted_spire");
        var city = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Lofted Spire", true);
        Assert.True(Lofted.Aloft(t));
        Assert.False(Lofted.Away(t));
        Assert.Contains(Research.Sources(t), s => s.Why.Contains("Lofted", StringComparison.Ordinal));

        var g = t.Regions.Where(r => r.Ring >= 3).OrderBy(r => r.Id).First();
        Assert.NotNull(Lofted.SailBlocked(t, g.Id));
        g.Known = true;
        t.Treasury = 10_000;
        Assert.True(sim.SailLofted(g.Id));
        Assert.True(t.Treasury < 10_000);
        Assert.True(Lofted.Sailing(t));
        Assert.False(sim.SailLofted(-1));
        int days = (t.Lofted.ArriveTick - t.Tick) / SimTime.HoursPerDay;
        sim.RunDays(days + 1);
        Assert.False(Lofted.Sailing(t));
        Assert.True(g.Visited);
        Assert.All(g.SiteIds, id => Assert.Equal(SiteKnowledge.Confirmed, Quests.Site(t, id)!.Known));

        var site = Quests.Site(t, g.SiteIds[0])!;
        var start = Lofted.Start(t, World.Spot(t, site));
        Assert.NotEqual((0.0, 0.0), start);
        // A site by the town still starts from the town.
        var near = t.Sites.Where(s => s.Def.Distance > 0).OrderBy(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();
        Assert.Equal((0.0, 0.0), Lofted.Start(t, World.Spot(t, near)));

        Assert.True(sim.SailLofted(-1));
        sim.RunDays((t.Lofted.ArriveTick - t.Tick) / SimTime.HoursPerDay + 1);
        Assert.False(Lofted.Away(t));
        Assert.Equal(city.Id, Lofted.City(t)!.Id);
    }
}
