namespace CrownAndCompass.Sim.Tests;

public class TreasuryTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void BooksEveryGoldPieceInOrOutOfTheTreasuryUnderAnAccount(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, StartingTreasury = 250 });
        sim.RunDays(60);
        var t = sim.Town;
        double booked = Js.Sum(t.Accounts.Values, v => v);
        Assert.Equal(250 + booked, t.Treasury, 6);
        Assert.True(t.Accounts[TreasuryAccount.Upkeep] < 0);
    }

    [Fact]
    public void NeverLetsUpkeepDriveTheTreasuryBelowZero()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, StartingTreasury = 0, AutoBounties = false });
        for (int d = 0; d < 40; d++)
        {
            sim.RunDays(1);
            Assert.True(sim.Town.Treasury >= 0);
        }
    }
}
