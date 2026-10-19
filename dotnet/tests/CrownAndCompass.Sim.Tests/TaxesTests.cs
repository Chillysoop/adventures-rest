using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TaxesTests
{
    /// <summary>A day's books where the town's services cost <paramref name="services"/> and it takes in <paramref name="income"/>.</summary>
    private static void Books(Town t, double income, double services)
    {
        t.AccountsToday = new();
        t.AccountsToday[TreasuryAccount.SalesTax] = income;
        t.AccountsToday[TreasuryAccount.Upkeep] = -services;
    }

    [Fact]
    public void TheMayorSetsTheRateWithinItsLimits()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoCouncil = false });
        var t = sim.Town;
        double calm = Taxes.Mood(t);
        Assert.True(sim.SetTax(0.15));
        Assert.Equal(0.15, t.TaxRate, 6);
        Assert.True(Taxes.Mood(t) < calm);
        Assert.True(sim.SetTax(0.9));
        Assert.Equal(Taxes.Rules(t).Max, t.TaxRate, 6);
        Assert.False(sim.SetTax(0.9));
        Assert.True(sim.SetTax(0));
        Assert.Equal(Taxes.Rules(t).Min, t.TaxRate, 6);
        Assert.True(Taxes.Mood(t) > calm);
    }

    [Fact]
    public void ServicesThatOutrunIncomeStirUnrestUntilTheyArePaidFor()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(2);
        double mandate = t.Mandate;
        for (int d = 0; d < 30; d++)
        {
            Books(t, 100, 300);
            Taxes.Close(t);
        }
        Assert.True(Taxes.Funding(t) < 0.5);
        Assert.True(t.Taxes.Unrest >= 60, $"unrest {t.Taxes.Unrest}");
        Assert.True(t.Mandate < mandate);
        Assert.True(Taxes.Mood(t) < -10);
        Assert.True(Taxes.Attraction(t) < 0);
        Assert.True(Taxes.StrikeShare(t) > 0);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Underfunded);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("down tools"));

        for (int d = 0; d < 60; d++)
        {
            Books(t, 400, 300);
            Taxes.Close(t);
        }
        Assert.Equal(0, t.Taxes.Unrest);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Underfunded);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("protests over the town's neglect die down"));
    }

    [Fact]
    public void AHamletsHallIsNoGrievance()
    {
        var t = new Simulation(new SimulationOptions { Seed = 3 }).Town;
        for (int d = 0; d < 10; d++)
        {
            Books(t, 0, Taxes.Rules(t).Grace - 1);
            Taxes.Close(t);
        }
        Assert.Equal(0, t.Taxes.Unrest);
    }

    [Fact]
    public void ACouncilWithAMayorAtTheTableLeavesTaxesToTheMayor()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoCouncil = false, StartingTreasury = 0 });
        sim.RunDays(120);
        Assert.DoesNotContain(sim.Town.Motions, m => m.Kind is MotionKind.RaiseTax or MotionKind.LowerTax);
        Assert.Equal(sim.Town.Content.Rules.TaxRate, sim.Town.TaxRate, 6);
    }
}
