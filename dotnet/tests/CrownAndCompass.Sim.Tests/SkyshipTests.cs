using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class SkyshipTests
{
    [Fact]
    public void TheAirshipFliesARunHomeWithGoldAndFloodsTheMarketItSellsInto()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("sky_dock");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        var s = t.Settlements[0];
        t.Regions[s.RegionId].Known = true;
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        s.Standing = 0;
        t.Market.Seed(Good.Tools, 200);
        t.Treasury += 1000;
        Assert.Null(Skyship.Refuse(t, s, Good.Tools));
        double before = Skyship.Price(t, s, Good.Tools);
        Assert.True(sim.SendSkyship(s.Id, Good.Tools));
        // Out trading, it carries no company and flies no second run.
        Assert.Equal(0, Passage.AirshipsFree(t));
        Assert.NotNull(Skyship.Refuse(t, s, Good.Tools));
        Assert.False(sim.SendSkyship(s.Id, Good.Tools));
        sim.RunDays(Skyship.FlightDays(t, s) + 4);
        Assert.Null(t.Trade.Sky.Out);
        Assert.Equal(1, t.Trade.Sky.Runs);
        Assert.True(t.Trade.Sky.Earned > 0);
        Assert.False(Skyship.Away(t));
        // Selling there sinks what that good fetches there next time.
        Assert.True(Skyship.Price(t, s, Good.Tools) < before);
    }

    [Fact]
    public void ACompanyAboardKeepsTheAirshipFromTrading()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        var d = t.Content.Building("sky_dock");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        var s = t.Settlements[0];
        t.Regions[s.RegionId].Known = true;
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        s.Standing = 0;
        t.Market.Seed(Good.Tools, 200);
        t.Treasury += 1000;
        t.Parties.Add(new Party { Name = "the Lanterns", Members = [], SiteId = t.Sites[0].Def.Id, Airship = true });
        Assert.Equal("the airship is out with a company", Skyship.Refuse(t, s, Good.Tools));
    }
}
