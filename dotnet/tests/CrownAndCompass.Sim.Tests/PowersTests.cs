using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PowersTests
{
    private static (Simulation Sim, Menace M) Seeded(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        return (sim, m!);
    }

    private static void Courted(Town t, PowerState p, Menace m, double offer)
    {
        p.CourtedBy = m.Id;
        p.Offer = offer;
        p.DecideDay = t.Day + 2;
    }

    [Fact]
    public void EveryPowerIsALibraryFactionTheTownStartsOutKnowing()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        Assert.Equal(t.Content.Powers.Powers.Count, t.Powers.Count);
        foreach (var d in t.Content.Powers.Powers)
        {
            Assert.NotNull(t.Content.Library.Faction(d.Ref));
            Assert.True(d.Ally.Text != "" && d.Goal != "" && d.Witness != "", $"{d.Id} needs its ally text, goal and witness");
            if (d.Enemy.Strike != "") Assert.Contains(d.Enemy.Strike, new[] { Blows.Burn, Blows.Assassinate, Blows.Walls, Blows.Treasury, Blows.Stores, Blows.Discord, Blows.Dead, Blows.Terror });
        }
    }

    [Fact]
    public void APowerAnEnemyOutbidsTheTownForJoinsItAndArmsItAndGoesFreeWhenItDies()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        var p = Powers.Find(t, "black-ledger")!;
        Courted(t, p, m, 90);
        sim.RunDays(3);
        Assert.Equal(PowerSide.Enemy, p.Side);
        Assert.Equal(m.Id, p.MenaceId);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("took") && e.Text.Contains(m.Name));
        double before = m.Power;
        sim.RunDays(1);
        Assert.True(m.Power > before || !m.Alive);
        var lines = new List<string>();
        Assert.True(Powers.JoinHost(t, m, lines) > 0);
        Assert.Single(lines);
        m.EndedDay = t.Day;
        sim.RunDays(1);
        Assert.Equal(PowerSide.Neutral, p.Side);
        Assert.Null(p.MenaceId);
    }

    [Fact]
    public void GiftsOutbidAnEnemysEnvoys()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        t.Treasury = 5000;
        var p = Powers.Find(t, "mire-grandmothers")!;
        Courted(t, p, m, p.Standing + 15);
        Assert.True(sim.CourtPower(p.Id));
        Assert.True(sim.CourtPower(p.Id));
        sim.RunDays(3);
        Assert.Equal(PowerSide.Neutral, p.Side);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("away empty-handed"));
    }

    [Fact]
    public void TheGoodPowersNeverServeAnEnemy()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        foreach (var id in new[] { "lanternbearers", "crown-league" })
            Assert.False(Powers.Buyable(t, Powers.Find(t, id)!, m));
        Assert.True(Powers.Buyable(t, Powers.Find(t, "silent-knife")!, m));
    }

    [Fact]
    public void AnAllyStandsOnTheWallsForItsTitheAndItsRivalsTakeItBadly()
    {
        var (sim, _) = Seeded();
        var t = sim.Town;
        t.Treasury = 5000;
        var league = Powers.Find(t, "crown-league")!;
        var ledger = Powers.Find(t, "black-ledger")!;
        league.Favour = ledger.Favour = 80;
        league.Standing = ledger.Standing = 80;
        double before = Threat.Defense(t);
        Assert.True(sim.AllyWithPower("crown-league"));
        Assert.True(Threat.Defense(t) > before);
        double leagueBefore = Powers.Target(t, league);
        Assert.True(sim.AllyWithPower("black-ledger"));
        Assert.True(Powers.Target(t, league) < leagueBefore, "the League hates the Ledger and dark allies");
        Assert.True(sim.BreakWithPower("black-ledger"));
        Assert.Equal(PowerSide.Neutral, ledger.Side);
        double gold = t.Treasury;
        sim.RunDays(1);
        Assert.True(t.Treasury < gold + 200);
    }

    [Fact]
    public void TheCourtedAlertAsksForGiftsOnlyWhileTheEnemyIsAhead()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        var p = Powers.Find(t, "silent-knife")!;
        Courted(t, p, m, p.Standing + 30);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "courted:silent-knife");
        p.Favour += 60;
        p.Standing += 60;
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == "courted:silent-knife");
    }
}
