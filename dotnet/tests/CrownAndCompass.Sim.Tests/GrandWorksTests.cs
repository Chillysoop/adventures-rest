using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GrandWorksTests
{
    [Fact]
    public void AGreatCityHasWondersAndAGreatTreasuryCostsKeeping()
    {
        var c = Content.Default();
        var wonders = c.Buildings.Where(d => d.IsGrand && d.Tier == "great_city").Select(d => d.Id).ToList();
        Assert.Contains("great_cathedral", wonders);
        Assert.Contains("royal_palace", wonders);
        Assert.Contains("sky_citadel", wonders);
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        t.Treasury = c.Rules.TreasuryFree;
        double bill = Economy.UpkeepBill(t);
        Assert.Equal(0, Economy.TreasuryUpkeep(t));
        t.Treasury = c.Rules.TreasuryFree + 100_000;
        Assert.Equal(Js.Round(100_000 * c.Rules.TreasuryKeep), Economy.TreasuryUpkeep(t));
        Assert.Equal(bill + Economy.TreasuryUpkeep(t), Economy.UpkeepBill(t));
    }
}
