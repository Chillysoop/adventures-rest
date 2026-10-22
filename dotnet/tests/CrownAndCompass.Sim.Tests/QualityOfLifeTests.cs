using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class QualityOfLifeTests
{
    private static Simulation Planned(int seed = 1) => new(new SimulationOptions { Seed = seed, AutoPlanning = false });

    private static (double X, double Y) Open(Town t, double dx, double dy, string id)
    {
        var d = t.Content.Building(id);
        for (double r = 0; r < 4; r += 0.25)
            for (int k = 0; k < 16; k++)
            {
                double x = Town.Center + dx + r * Math.Cos(k * Math.PI / 8), y = Town.Center + dy + r * Math.Sin(k * Math.PI / 8);
                if (Planning.SpotBlocked(t, d, x, y, 1.0) == null) return (x, y);
            }
        throw new InvalidOperationException("no open spot");
    }

    [Fact]
    public void ACalledOffSiteGivesItsGoldBackAndFreesItsGround()
    {
        var sim = Planned();
        var t = sim.Town;
        t.Treasury = 2000;
        var (x, y) = Open(t, 7, 0, "tavern");
        var site = sim.Place("tavern", x, y, 1.0)!;
        double paid = 2000 - t.Treasury;
        Assert.True(paid > 0);
        Assert.Equal(Math.Floor(paid), Planning.Refund(t, site));
        Assert.Null(Planning.RazeBlocked(t, site));
        Assert.True(sim.Raze(site.Id));
        Assert.True(site.Razed);
        Assert.False(site.IsSite);
        Assert.False(site.Complete);
        Assert.Equal(2000 - paid + Math.Floor(paid), t.Treasury, 6);
        Assert.Null(Planning.SpotBlocked(t, t.Content.Building("tavern"), x, y, 1.0));
        Assert.False(sim.Raze(site.Id));
        // The town plays on with the gap.
        sim.RunDays(3);
    }

    [Fact]
    public void TheTownHallCannotBeTornDown()
    {
        var sim = Planned();
        Assert.NotNull(Planning.RazeBlocked(sim.Town, sim.Town.Buildings[0]));
        Assert.False(sim.Raze(sim.Town.Buildings[0].Id));
    }

    [Fact]
    public void UndoTakesBackTheLastStroke()
    {
        var sim = Planned();
        var t = sim.Town;
        var at = Open(t, 6, 6, "house");
        var was = Land.ZoneAt(t, (int)(at.X * Land.Res), (int)(at.Y * Land.Res));
        Assert.True(sim.PaintZone([at], 1, Zone.Industry) > 0);
        Assert.Equal(Zone.Industry, Land.ZoneAt(t, (int)(at.X * Land.Res), (int)(at.Y * Land.Res)));
        Assert.True(sim.UndoPaint() > 0);
        Assert.Equal(was, Land.ZoneAt(t, (int)(at.X * Land.Res), (int)(at.Y * Land.Res)));
        Assert.Equal(0, sim.UndoPaint());
    }

    [Fact]
    public void AStewardWhoAsksWaitsForAYes()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        foreach (var a in Stewards.Areas.Where(a => a.CanAsk)) Assert.True(sim.SetSteward(a.Id, StewardMode.Ask));
        Assert.False(sim.SetSteward("stories", StewardMode.Ask));
        StewardAsk? ask = null;
        for (int d = 0; d < 60 && ask == null; d++)
        {
            sim.RunDays(1);
            ask = t.Stewards.Asks.FirstOrDefault();
        }
        Assert.NotNull(ask);
        // One ask per area at a time.
        Assert.Equal(t.Stewards.Asks.Count, t.Stewards.Asks.Select(a => a.Area).Distinct().Count());
        Assert.True(sim.AnswerSteward(ask!.Id, false));
        Assert.DoesNotContain(t.Stewards.Asks, a => a.Id == ask.Id);
        Assert.True(t.Stewards.Declined.ContainsKey(ask.Key));
        Assert.False(sim.AnswerSteward(ask.Id, true));
        // Taking the area back clears what it asked.
        sim.RunDays(5);
        foreach (var a in Stewards.Areas) sim.SetSteward(a.Id, StewardMode.Player);
        Assert.Empty(t.Stewards.Asks);
    }

    [Fact]
    public void AnAskedBountyIsPostedOnYes()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        sim.SetSteward("bounties", StewardMode.Ask);
        StewardAsk? ask = null;
        for (int d = 0; d < 80 && ask == null; d++)
        {
            sim.RunDays(1);
            ask = t.Stewards.Asks.FirstOrDefault(a => a.Kind == "bounty");
        }
        Assert.NotNull(ask);
        int before = t.Bounties.Count;
        t.Treasury += 1000;
        Assert.True(sim.AnswerSteward(ask!.Id, true));
        Assert.Equal(before + 1, t.Bounties.Count);
    }

    [Fact]
    public void APatrolKeepsAClearedSiteClearOrFallsBackToClearing()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoMayor = false });
        var t = sim.Town;
        var s = t.Sites.First(x => x.Known != SiteKnowledge.Unheard);
        Assert.False(sim.Patrol(s.Def.Id, true));
        s.Cleared = s.ThoughtCleared = true;
        s.ResettleTick = t.Tick + 30;
        t.Treasury = 1000;
        Assert.True(sim.Patrol(s.Def.Id, true));
        Assert.False(sim.Patrol(s.Def.Id, true));
        sim.RunDays(1);
        Assert.True(t.Treasury < 1000);
        sim.RunDays(1);
        // The day something moved in, the patrol met it: still clear and watched, or lost and wanting clearing again.
        Assert.True((s.Cleared && s.Patrolled && s.ResettleTick > t.Tick) || (!s.Cleared && !s.Patrolled));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("patrol"));
        if (s.Patrolled)
        {
            Assert.True(sim.Patrol(s.Def.Id, false));
            Assert.False(s.Patrolled);
        }
    }
}
