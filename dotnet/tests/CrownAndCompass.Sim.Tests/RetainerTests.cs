using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Companies kept on the town's retainer: priced by levels and renown, leaning to council jobs, paid daily, and quitting when unpaid.</summary>
public class RetainerTests
{
    private static (Simulation Sim, string Company) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoBounties = false, Enemy = false });
        sim.RunDays(150);
        var t = sim.Town;
        var name = Intel.MembersByCompany(t).Select(x => x.Name).First();
        Intel.Deal(t, name, 50);
        return (sim, name);
    }

    [Fact]
    public void ACompanyIsDearerWithMoreLevelsAndRenown()
    {
        var (sim, name) = Town();
        var t = sim.Town;
        double before = Retainers.Price(t, name);
        Assert.True(before >= t.Content.Bounties.Offers.Retainer.Base);
        Intel.Company(t, name).Renown += 100;
        Assert.True(Retainers.Price(t, name) > before);
        double renowned = Retainers.Price(t, name);
        Intel.Members(t, name)[0].Hero!.Level += 5;
        Assert.True(Retainers.Price(t, name) > renowned);
    }

    [Fact]
    public void ARetainedCompanyLeansToTheCouncilsJobsIsPaidDailyAndQuitsWhenUnpaid()
    {
        var (sim, name) = Town();
        var t = sim.Town;
        t.Treasury = 0;
        Assert.StartsWith("the treasury", Retainers.Blocked(t, name));
        t.Treasury = 100000;
        double price = Retainers.Price(t, name);
        Assert.True(sim.RetainCompany(name));
        Assert.False(sim.RetainCompany(name));
        Assert.Equal(100000 - price, t.Treasury, 3);
        var c = Intel.Find(t, name)!;
        Assert.True(c.Retained && c.Wage > 0);
        Assert.All(Intel.Members(t, name), m => Assert.Contains(Loyalty.Factors(t, m), f => f.Why == "on the town's retainer"));

        sim.RunDays(1);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Retainers, 0) < 0);

        // A wage no treasury could pay: they walk after a few days.
        c.Wage = 1e9;
        sim.RunDays(t.Content.Bounties.Offers.Retainer.LapseDays + 1);
        Assert.False(c.Retained);

        t.Treasury = 100000;
        Intel.Deal(t, name, 50);
        Assert.True(sim.RetainCompany(name));
        Assert.True(sim.ReleaseCompany(name));
        Assert.False(c.Retained);
        Assert.False(sim.ReleaseCompany(name));
    }
}
