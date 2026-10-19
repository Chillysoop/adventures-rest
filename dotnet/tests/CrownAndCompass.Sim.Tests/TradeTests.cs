using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TradeTests
{
    private static Simulation Player(int seed = 2) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = false, FreeCompanies = false, AutoStories = false });

    /// <summary>A town with a charter for routes, a known people who will trade, and gold to spare.</summary>
    private static (Simulation Sim, Settlement S) Ready(int seed = 2)
    {
        var sim = Player(seed);
        var t = sim.Town;
        sim.RunDays(1);
        t.City.Tier = City.TierIndex(t, "town");
        var s = t.Settlements.First(x => x.Kin.Trade.Offers.Count > 0);
        t.Regions[s.RegionId].Known = true;
        s.Standing = 30;
        t.Treasury += 2000;
        return (sim, s);
    }

    [Fact]
    public void EveryPeopleTradesInRealGoods()
    {
        var c = Content.Default();
        Assert.All(c.World.Peoples, p =>
        {
            Assert.NotEmpty(p.Trade.Offers);
            Assert.NotEmpty(p.Trade.Wants);
            Assert.Empty(p.Trade.Wants.Intersect(p.Trade.Offers));
        });
        Assert.Equal(c.City.Tiers.Count, c.Trade.Routes.Count);
        Assert.All(c.City.Tiers, tier => Assert.True(c.Trade.Routes.ContainsKey(tier.Id), tier.Id));
    }

    [Fact]
    public void ARouteNeedsACharterStandingAndGold()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var s = t.Settlements.First(x => x.Kin.Trade.Offers.Count > 0);
        t.Regions[s.RegionId].Known = true;
        s.Standing = 30;
        Assert.Equal(0, Trade.Cap(t));
        Assert.NotNull(Trade.Refuse(t, s));
        t.City.Tier = City.TierIndex(t, "hamlet");
        t.Treasury += 1000;
        Assert.Null(Trade.Refuse(t, s));
        s.Standing = 0;
        Assert.NotNull(Trade.Refuse(t, s));
    }

    [Fact]
    public void ACaravanSellsTheSurplusBringsBackWhatIsShortAndWarmsThePeople()
    {
        var (sim, s) = Ready();
        var t = sim.Town;
        var (export, import) = Trade.Suggest(t, s);
        Assert.Contains(import, s.Kin.Trade.Offers);
        t.Market.Seed(export, t.Market.Target(export) * 3);
        t.Market.Seed(import, -t.Market.Stock(import));
        double treasury = t.Treasury, cost = Trade.OpenCost(t, s);
        Assert.True(sim.OpenTradeRoute(s.Id, export, import));
        Assert.Equal(treasury - cost, t.Treasury, 6);
        Assert.False(sim.OpenTradeRoute(s.Id, export, import));
        var route = Trade.Open(t).Single();
        double standing = s.Standing;
        sim.RunDays(route.NextDay - t.Day + 1);
        Assert.Equal(1, route.Caravans);
        if (route.Raided == 0)
        {
            Assert.True(route.Earned > 0);
            Assert.True(route.Spent > 0);
            Assert.True(s.Standing > standing - t.Content.World.Standing.Drift * 20);
        }
        Assert.True(sim.CloseTradeRoute(route.Id));
        Assert.Empty(Trade.Open(t));
    }

    [Fact]
    public void ARazedPeopleOrAnAngryOneEndsTheRoute()
    {
        var (sim, s) = Ready();
        var t = sim.Town;
        var (export, import) = Trade.Suggest(t, s);
        Assert.True(sim.OpenTradeRoute(s.Id, export, import));
        s.Standing = -50;
        sim.RunDays(1);
        Assert.Empty(Trade.Open(t));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("turn the town's caravan away", StringComparison.Ordinal));
    }

    [Fact]
    public void WinterSlowsTheCaravans()
    {
        var (sim, s) = Ready();
        var t = sim.Town;
        int summer = Trade.TripDays(t, s);
        while (Seasons.Current(t).Id != "winter") sim.RunDays(1);
        Assert.True(Trade.TripDays(t, s) > summer);
    }

    [Fact]
    public void TheStandInMayorRunsRoutesAsTheTownGrows()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(400);
        var t = sim.Town;
        Assert.NotEmpty(t.Trade.Routes);
        Assert.True(t.Trade.Routes.Sum(r => r.Caravans) > 10);
        Assert.True(t.Accounts.Get(TreasuryAccount.Trade, 0) != 0);
    }

    [Fact]
    public void DiplomaticStoriesBindASettlement()
    {
        var (sim, s) = Ready();
        var t = sim.Town;
        var (export, import) = Trade.Suggest(t, s);
        Assert.True(sim.OpenTradeRoute(s.Id, export, import));
        var story = Stories.Open(t, "weighers-dispute");
        Assert.NotNull(story);
        Assert.Equal(s.Id, story!.SettlementId);
        Assert.Contains(s.Name, Stories.Fill(t, story, Stories.StageOf(t, story).Text));
        double before = s.Standing;
        Stories.Resolve(t, story.Id, 2);
        Assert.True(s.Standing > before);
    }
    [Fact]
    public void MoreWagonsCarryMoreOfTheSurplusUpToFour()
    {
        var (sim, s) = Ready();
        var t = sim.Town;
        var (export, import) = Trade.Suggest(t, s);
        Assert.True(sim.OpenTradeRoute(s.Id, export, import));
        var route = Trade.Open(t).Single();
        double one = Trade.Load(t, s, route);
        t.Treasury += 5000;
        double treasury = t.Treasury;
        Assert.True(sim.AddWagon(route.Id));
        Assert.Equal(2, route.Wagons);
        Assert.True(Trade.Load(t, s, route) > one * 1.5);
        Assert.True(t.Treasury < treasury);
        while (Trade.WagonCost(t, route) != null) Assert.True(sim.AddWagon(route.Id));
        Assert.Equal(t.Content.Trade.Wagons, route.Wagons);
        Assert.False(sim.AddWagon(route.Id));
    }
}
