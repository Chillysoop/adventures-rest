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

    [Fact]
    public void EachPowerMarchingWithAnEnemyMakesItsNextOfferBigger()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Power = 400;
        m.Cunning = "cunning";
        double alone = Powers.OfferOf(t, m);
        var p = Powers.Find(t, "black-ledger")!;
        p.Side = PowerSide.Enemy;
        p.MenaceId = m.Id;
        Assert.True(Powers.OfferOf(t, m) > alone);
    }

    [Fact]
    public void PowersSendEnvoysAndAPactKeepsEnemiesFromBuyingThem()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        sim.RunDays(t.Content.Powers.Envoys.From + t.Content.Powers.Envoys.Every);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("An envoy of", StringComparison.Ordinal));

        var knife = Powers.Find(t, "silent-knife")!;
        Courted(t, knife, m, 500);
        knife.Envoy = "pact";
        knife.EnvoyGold = 10;
        knife.EnvoyPrice = "";
        knife.EnvoyUntil = t.Day + 5;
        t.Treasury = 1000;
        Assert.Contains(Alerts.Standing(t), a => a.Key == "envoy:silent-knife");
        Assert.True(sim.AnswerEnvoy("silent-knife", true));
        Assert.True(Powers.Pacted(t, knife));
        Assert.Null(knife.CourtedBy);
        Assert.False(Powers.Buyable(t, knife, m));
        sim.RunDays(3);
        Assert.NotEqual(PowerSide.Enemy, knife.Side);
    }

    [Fact]
    public void RefusingADemandCostsMoreFavourThanLettingAnOfferGo()
    {
        var (sim, _) = Seeded();
        var t = sim.Town;
        var ledger = Powers.Find(t, "black-ledger")!;
        ledger.Envoy = "tribute";
        ledger.EnvoyUntil = t.Day + 5;
        double before = ledger.Favour;
        Assert.True(sim.AnswerEnvoy("black-ledger", false));
        Assert.Equal(before - t.Content.Powers.Envoys.Spurned, ledger.Favour);
        Assert.Equal("", ledger.Envoy);
        Assert.False(sim.AnswerEnvoy("black-ledger", false));
    }

    [Fact]
    public void TheGrandmothersTakeAStrangePriceInsteadOfGold()
    {
        var (sim, _) = Seeded();
        var t = sim.Town;
        var hags = Powers.Find(t, "mire-grandmothers")!;
        hags.Envoy = "alliance";
        hags.EnvoyGold = 999999;
        hags.EnvoyPrice = "a-name";
        hags.EnvoyUntil = t.Day + 5;
        double gold = t.Treasury;
        int folk = t.Residents.Count;
        Assert.Null(Powers.AnswerBlocked(t, hags));
        Assert.True(sim.AnswerEnvoy("mire-grandmothers", true));
        Assert.Equal(PowerSide.Town, hags.Side);
        Assert.Equal(gold, t.Treasury);
        Assert.Equal(folk - 1, t.Residents.Count);

        hags.Side = PowerSide.Neutral;
        hags.Envoy = "pact";
        hags.EnvoyPrice = "a-good-year";
        hags.EnvoyUntil = t.Day + 5;
        Assert.True(sim.AnswerEnvoy("mire-grandmothers", true));
        Assert.Equal(0.75, Powers.Harvest(t), 3);
        Assert.True(t.Content.Powers.Powers.Single(d => d.Id == "mire-grandmothers").Strange.All(x => x.Text != "" && x.Paid != ""));
    }
}
