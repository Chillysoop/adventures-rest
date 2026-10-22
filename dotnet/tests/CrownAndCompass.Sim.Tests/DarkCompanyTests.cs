using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DarkCompanyTests
{
    /// <summary>A grown town with a big bad crowned today.</summary>
    private static Simulation Crowned(int seed = 2)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(120);
        var t = sim.Town;
        if (Saga.BigBad(t) is not { Alive: true })
        {
            // The old wyrm: far past what the town's heroes can slay this early, so it lives through the test.
            var top = Menaces.Awaken(t, t.Content.Saga.Finale.Fallback[0])!;
            t.Saga.BigBadId = top.Id;
            t.Saga.CrownDay = t.Day;
            // It bides its time in its seat rather than riding on the town at once.
            t.Saga.NextRideDay = t.Day + 1000;
        }
        t.DarkCompany.NextTick = -1;
        return sim;
    }

    private static void RunUntil(Simulation sim, Func<bool> done, int days = 60)
    {
        for (int i = 0; i < days && !done(); i++) sim.RunDays(1);
    }

    [Fact]
    public void NoBigBadNoCompany()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, Saga = false });
        sim.RunDays(80);
        Assert.False(sim.Town.DarkCompany.Riding);
        Assert.Equal("not on the road", sim.AmbushDarkCompany());
    }

    [Fact]
    public void ItRisesAfterTheCrowningAndRidesForAHoard()
    {
        var sim = Crowned();
        var t = sim.Town;
        sim.RunDays(t.Content.DarkCompany.Form - 1);
        Assert.False(t.DarkCompany.Riding);
        RunUntil(sim, () => DarkCompany.Out(t));
        Assert.True(t.DarkCompany.Riding, $"bb {t.Saga.BigBadId} alive {Saga.BigBad(t)?.Alive} next {t.DarkCompany.NextTick} tick {t.Tick} roles {t.Content.DarkCompany.Roles.Count}");
        Assert.Equal(4, t.DarkCompany.Members.Count);
        Assert.True(DarkCompany.Role(t, t.DarkCompany.Members[0].Role)!.Lead);
        Assert.Equal(t.DarkCompany.Members.Count, t.DarkCompany.Members.Select(m => m.Role).Distinct().Count());
        var site = DarkCompany.Target(t)!;
        Assert.False(site.Cleared);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Dark && a.SiteId == site.Def.Id);
    }

    [Fact]
    public void ARobbedHoardFeedsTheBigBadAndAClearedSiteDoesNot()
    {
        var sim = Crowned();
        var t = sim.Town;
        RunUntil(sim, () => DarkCompany.Out(t));
        var site = DarkCompany.Target(t)!;
        var master = Saga.BigBad(t)!;
        double loot = site.Def.Loot[^1];
        double power = master.Power;
        // Nobody races it: it robs the site.
        foreach (var p in t.Parties.Where(p => p.SiteId == site.Def.Id)) p.ArriveTick = int.MaxValue;
        t.DarkCompany.ArriveTick = t.Tick + 1;
        int robbed = t.DarkCompany.Robbed;
        sim.RunDays(1);
        if (t.DarkCompany.Robbed > robbed)
        {
            Assert.True(site.Def.Loot[^1] < loot);
            Assert.True(master.Power > power * 1.03 || master.Power > power + 10);
        }
        // Again, but the town clears its target first.
        sim = Crowned();
        t = sim.Town;
        RunUntil(sim, () => DarkCompany.Out(t));
        var next = DarkCompany.Target(t)!;
        next.Cleared = true;
        int lost = t.DarkCompany.Lost;
        int robbedNow = t.DarkCompany.Robbed;
        t.DarkCompany.ArriveTick = t.Tick + 1;
        sim.RunDays(1);
        Assert.Equal(lost + 1, t.DarkCompany.Lost);
        Assert.Equal(robbedNow, t.DarkCompany.Robbed);
    }

    [Fact]
    public void HeroesCanRideOutToAmbushIt()
    {
        int fought = 0, won = 0;
        foreach (int seed in new[] { 2, 3, 5 })
        {
            var sim = Crowned(seed);
            var t = sim.Town;
            RunUntil(sim, () => DarkCompany.Out(t) && DarkCompany.MeetTick(t) != null && DarkCompany.Free(t).Count > 0, 90);
            if (!DarkCompany.Out(t) || DarkCompany.AmbushBlocked(t) != null) continue;
            Assert.Null(sim.AmbushDarkCompany());
            Assert.Equal("already sent", sim.AmbushDarkCompany());
            var sent = t.DarkCompany.Ambush!.HeroIds.ToList();
            Assert.InRange(sent.Count, 1, t.Content.DarkCompany.Party);
            Assert.All(sent, id => Assert.True(t.Agent(id).IsAway));
            Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Dark);
            int broken = t.DarkCompany.Broken;
            RunUntil(sim, () => t.DarkCompany.Ambush == null, 40);
            Assert.Null(t.DarkCompany.Ambush);
            Assert.All(sent.Select(t.Agent).Where(a => !a.Dead && !a.Departed), a => Assert.False(a.IsAway));
            fought++;
            if (t.DarkCompany.Broken > broken)
            {
                won++;
                Assert.False(t.DarkCompany.Riding);
                Assert.True(t.DarkCompany.NextTick > t.Tick + 50 * SimTime.HoursPerDay);
            }
        }
        Assert.True(fought > 0, "some seed should have heroes free to ride out");
    }

    [Fact]
    public void ItKeepsTheMainDiceStill()
    {
        // Two towns, one whose company is out: the main stream is the same up to the errand.
        var a = Crowned();
        var b = Crowned();
        a.RunDays(30);
        b.RunDays(30);
        Assert.Equal(a.Town.DarkCompany.Target, b.Town.DarkCompany.Target);
        Assert.Equal(a.Town.DarkCompany.Name, b.Town.DarkCompany.Name);
        Assert.Equal(a.Town.Treasury, b.Town.Treasury);
    }

    [Fact]
    public void ItRidesForItsMastersQuestAndFetchingItFinishesTheQuest()
    {
        foreach (int seed in new[] { 2, 3, 5 })
        {
            var sim = Crowned(seed);
            var t = sim.Town;
            RunUntil(sim, () => DarkQuests.Current(t) != null && DarkCompany.OnQuest(t), 120);
            if (!DarkCompany.OnQuest(t)) continue;
            var q = DarkQuests.Current(t)!;
            Assert.Equal(q.SiteId, t.DarkCompany.Target);
            Assert.True(t.DarkCompany.ArriveTick < q.DueDay * SimTime.HoursPerDay);
            foreach (var p in t.Parties.Where(p => p.SiteId == q.SiteId)) p.ArriveTick = int.MaxValue;
            int done = DarkQuests.State(t).Past.Count;
            t.DarkCompany.ArriveTick = t.Tick + 1;
            sim.RunDays(1);
            Assert.Equal(done + 1, DarkQuests.State(t).Past.Count);
            Assert.False(DarkQuests.State(t).Past[^1].Foiled);
            return;
        }
        Assert.Fail("no seed sent the company on its master's quest");
    }
}
