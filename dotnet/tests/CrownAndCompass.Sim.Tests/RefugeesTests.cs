using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class RefugeesTests
{
    private static Simulation Town(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoStories = false });
        sim.RunDays(2);
        return sim;
    }

    private static Settlement Somewhere(Town t) => t.Settlements.Where(s => !s.Razed).OrderBy(s => s.Id).First();

    [Fact]
    public void ASmallGroupPitchesTentsAndWaitsLongerThanTravellers()
    {
        var sim = Town();
        var t = sim.Town;
        var from = Somewhere(t);
        double mandate = t.Mandate;
        int n = Refugees.Flee(t, from, null, [3, 3]);
        Assert.InRange(n, 1, Refugees.Rules(t).CardAt - 1);
        var camp = Refugees.Camped(t).ToList();
        Assert.Equal(n, camp.Count);
        Assert.All(camp, a =>
        {
            Assert.True(a.Traveller);
            Assert.Equal(from.Id, a.RefugeeFrom);
            Assert.Equal(a.ArrivedTick / SimTime.HoursPerDay + Refugees.Rules(t).Patience.Least, a.StayUntil);
            Assert.False(a.Homeless);
        });
        Assert.True(t.Mandate > mandate, "taking them in earns Mandate");
        Assert.Equal(n, t.Refuge.Sheltered);
        Assert.True(t.Content.Refugees.Patience.Least > t.Content.Lodging.StayMost);
    }

    [Fact]
    public void RefugeesCampFreeEvenWithRoomsToLet()
    {
        var sim = Town();
        var t = sim.Town;
        Refugees.Flee(t, Somewhere(t), null, [3, 3]);
        sim.RunDays(1);
        Assert.All(Refugees.Camped(t), a => Assert.Null(a.InnId));
    }

    [Fact]
    public void ABigColumnAsksTheMayorAndCanBeTurnedAway()
    {
        var sim = Town();
        var t = sim.Town;
        var from = Somewhere(t);
        int before = t.Agents.Count;
        Assert.Equal(0, Refugees.Flee(t, from, null, [20, 20]));
        Assert.True(Refugees.Waiting(t));
        Assert.Contains(t.Stories.Open, s => s.Def == Refugees.StoryId);
        int count = t.Refuge.Column!.Count;
        Assert.Equal(before, t.Agents.Count);
        double mandate = t.Mandate, standing = from.Standing;
        Refugees.Decide(t, 3);
        Assert.False(Refugees.Waiting(t));
        Assert.Equal(before, t.Agents.Count);
        Assert.Equal(count, t.Refuge.TurnedAway);
        Assert.True(t.Mandate < mandate);
        Assert.True(from.Standing < standing);
    }

    [Fact]
    public void TakingSomeLetsInHalf()
    {
        var sim = Town();
        var t = sim.Town;
        Refugees.Flee(t, Somewhere(t), null, [20, 20]);
        int count = t.Refuge.Column!.Count;
        Refugees.Decide(t, 2);
        Assert.Equal((int)Math.Ceiling(count * t.Content.Refugees.Some), Refugees.Camping(t));
        Assert.Equal(count - Refugees.Camping(t), t.Refuge.TurnedAway);
    }

    [Fact]
    public void TheyWaitLongerWhileTheTownBuildsButNoLongerThanTheMost()
    {
        var sim = Town();
        var t = sim.Town;
        Refugees.Flee(t, Somewhere(t), null, [2, 2]);
        var a = Refugees.Camped(t).First();
        int first = a.StayUntil;
        bool building = t.Buildings.Any(b => b.IsSite && (b.Def.Housing > 0 || b.Def.Jobs > 0));
        Refugees.Daily(t);
        if (building) Assert.Equal(first + 1, a.StayUntil);
        for (int i = 0; i < 200; i++) Refugees.Daily(t);
        Assert.True(a.StayUntil <= a.ArrivedTick / SimTime.HoursPerDay + t.Content.Refugees.Patience.Most);
    }

    [Fact]
    public void WhenTheirFoeFallsTheyGoHomeAndRebuild()
    {
        var sim = Town();
        var t = sim.Town;
        var m = Menaces.Seed(t, [2, 3]);
        Assert.NotNull(m);
        var from = Somewhere(t);
        from.Razed = true;
        from.Standing = -100;
        Refugees.Flee(t, from, m, [3, 3]);
        var camp = Refugees.Camped(t).ToList();
        Assert.NotEmpty(camp);
        // One finds a home: settled refugees stay for good.
        var stayer = camp[0];
        stayer.Traveller = false;
        m!.EndedDay = t.Day;
        Refugees.Daily(t);
        Assert.False(stayer.Departed);
        Assert.All(camp.Skip(1), a => Assert.True(a.Departed));
        Assert.False(from.Razed);
        Assert.True(from.Allied, "the rebuilt village becomes the town's friend");
        Assert.Equal(camp.Count - 1, t.Refuge.WentHome);
    }

    [Fact]
    public void TheyNameWhatTookTheirHomeAndGoBackWhenACompanyRetakesIt()
    {
        var sim = Town();
        var t = sim.Town;
        var from = Somewhere(t);
        var site = t.Sites.Where(x => !x.Cleared && x.RegionId != null).OrderBy(x => x.Def.Id, StringComparer.Ordinal).First();
        from.Razed = true;
        Refugees.Flee(t, from, null, [4, 4], siteId: site.Def.Id);
        var lost = Refugees.LostOf(t, from.Id);
        Assert.NotNull(lost);
        Assert.Equal(site.Def.Id, lost!.SiteId);
        Assert.Contains(t.Pleas, p => p.SiteId == site.Def.Id && p.SettlementId == from.Id && p.Status == PleaStatus.Open);
        var camp = Refugees.Camped(t).ToList();
        Assert.True(camp.Count >= 2, $"{camp.Count} came");
        var stayer = camp[0];
        stayer.Traveller = false;
        site.Cleared = true;
        Refugees.Daily(t);
        Assert.True(lost.Retaken);
        Assert.False(stayer.Departed);
        Assert.All(camp.Skip(1), a => Assert.True(a.Departed));
        Assert.False(from.Razed);
        Assert.True(from.Allied);
    }

    [Fact]
    public void ASettledRefugeeWithAHomeOrWorkNeverLeavesForMisery()
    {
        var sim = Town();
        var t = sim.Town;
        Refugees.Flee(t, Somewhere(t), null, [3, 3]);
        var a = t.Agents.Last(x => x.Refugee);
        a.Traveller = false;
        a.JobId = t.Buildings.First(b => b.Def.Jobs > 0).Id;
        a.MiserableDays = 30;
        Population.DailyDepartures(t);
        Assert.False(a.Departed);
    }

    [Fact]
    public void ShelteringEnoughEarnsAHavenName()
    {
        var sim = Town();
        var t = sim.Town;
        var first = t.Content.Refugees.Havens[0];
        Assert.Equal("", Refugees.Title(t));
        double draw = Refugees.Draw(t);
        while (t.Refuge.Sheltered < first.At)
        {
            Refugees.Flee(t, Somewhere(t), null, [20, 20]);
            Refugees.Decide(t, 1);
        }
        Assert.Equal(first.Title, Refugees.Title(t));
        Assert.True(Refugees.Draw(t) >= draw || Refugees.Draw(t) == t.Content.Refugees.Draw.Most);
        Assert.Contains(Population.Appeal(t).Parts, p => p.Label.Contains(first.Title));
    }

    [Fact]
    public void EveryPeoplesTradesAreJobsTheTownHires()
    {
        var c = Content.Default();
        var hired = c.Buildings.Select(b => b.Profession).OfType<string>().ToHashSet();
        foreach (var (people, trades) in c.Refugees.Trades)
        {
            Assert.Contains(c.World.Peoples, p => p.Id == people);
            Assert.All(trades, tr => Assert.Contains(tr, hired));
        }
    }
}
