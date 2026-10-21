using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class UndercityTests
{
    private static Simulation Player(int seed = 2, bool auto = false) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = auto, AutoStories = false, FreeCompanies = false });

    private static void Charter(Town t, string tier) => t.City.Tier = City.TierIndex(t, tier);

    [Fact]
    public void EachCharterOpensALevelWithItsSiteUnderTheTown()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Assert.Empty(t.Undercity);
        Charter(t, "village");
        sim.RunDays(1);
        var l = Assert.Single(t.Undercity);
        Assert.Equal("cellars", l.Id);
        var site = Undercity.Site(t, l);
        Assert.Equal(0, site.Def.Distance);
        Assert.Equal(SiteKnowledge.Rumoured, site.Known);
        Assert.True(Undercity.IsUnder(t, site));
        Charter(t, "town");
        sim.RunDays(1);
        Assert.Equal(["cellars", "sewers"], t.Undercity.Select(x => x.Id));
    }

    [Fact]
    public void LeftAloneItStirsWarnsAndRisesKillingTownsfolk()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Charter(t, "village");
        sim.RunDays(1);
        var l = t.Undercity[0];
        var r = t.Content.Undercity;
        Assert.True(Undercity.Rate(t, l) > 0);
        l.Stir = r.WarnAt;
        Assert.Contains(Alerts.Standing(t), a => a.Key == "below:cellars");
        var folk = t.Residents.Where(a => a.Hero == null).Select(a => a.Id).ToList();
        double mandate = t.Mandate;
        l.Stir = r.RiseAt - 0.01;
        sim.RunDays(1);
        Assert.Equal(1, l.Rose);
        Assert.Equal(r.RiseAfter, l.Stir);
        // Some of the townsfolk who were there are gone (newcomers that day can make up the count).
        Assert.Contains(folk, id => t.Agent(id).Dead);
        Assert.True(t.Mandate < mandate);
        Assert.Contains(t.Ledger.Entries, e => e.Importance == Importance.Major && e.Text.Contains("the Old Cellars"));
    }

    [Fact]
    public void APatrolSlowsTheStirAtADailyCost()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Charter(t, "village");
        sim.RunDays(1);
        var l = t.Undercity[0];
        double rate = Undercity.Rate(t, l);
        Assert.True(sim.PatrolUndercity("cellars", true));
        Assert.False(sim.PatrolUndercity("cellars", true));
        Assert.Equal(rate * (1 - t.Content.Undercity.PatrolCut), Undercity.Rate(t, l), 6);
        l.Stir = t.Content.Undercity.WarnAt;
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == "below:cellars");
        Assert.True(sim.PatrolUndercity("cellars", false));
        Assert.False(sim.PatrolUndercity("nowhere", true));
    }

    [Fact]
    public void ClearingALevelQuietsItUntilTheDarkFillsAgain()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Charter(t, "village");
        sim.RunDays(1);
        var l = t.Undercity[0];
        var site = Undercity.Site(t, l);
        l.Stir = 80;
        double mandate = t.Mandate;
        site.Cleared = true;
        sim.RunDays(1);
        Assert.Equal(1, l.Cleared);
        Assert.Equal(0, l.Stir);
        Assert.Equal("quiet", Undercity.Mood(t, l));
        Assert.True(t.Mandate > mandate - 1);
        sim.RunDays(t.Content.Undercity.RefillDays);
        Assert.False(site.Cleared);
        Assert.Null(l.RefillDay);
        Assert.True(l.Stir < t.Content.Undercity.WarnAt);
    }

    [Fact]
    public void HeroesDelveItWithNoRoadToWalk()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Charter(t, "village");
        sim.RunDays(1);
        var l = t.Undercity[0];
        t.Treasury += 2000;
        for (int i = 0; i < 3; i++) Quests.CreateHero(t, t.Content.Classes[i], 4);
        string site = Undercity.Def(t, l).Site.Id;
        Assert.NotNull(sim.PostBounty(site, 300));
        for (int i = 0; i < 90 && l.Cleared == 0; i++)
        {
            // A party that limps back empty-handed may try again on a fresh bounty.
            if (!t.Bounties.Any(b => b.SiteId == site && b.Status is BountyStatus.Open or BountyStatus.Taken)) sim.PostBounty(site, 300);
            sim.RunDays(1);
        }
        Assert.Equal(1, l.Cleared);
    }

    [Fact]
    public void TheStandInSendsTheWatchDownWhenItStirs()
    {
        var sim = Player(auto: true);
        var t = sim.Town;
        sim.RunDays(1);
        Charter(t, "village");
        sim.RunDays(1);
        var l = t.Undercity[0];
        t.Treasury += 2000;
        l.Stir = t.Content.Undercity.StandInAt;
        sim.RunDays(1);
        Assert.True(l.Patrol || Undercity.Site(t, l).Cleared);
    }
}
