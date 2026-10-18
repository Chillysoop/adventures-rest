using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class SeasonsTests
{
    [Fact]
    public void TheYearTurnsThroughFourSeasons()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        var r = Seasons.Rules(t);
        Assert.Equal("spring", Seasons.Current(t).Id);
        Assert.Equal(r.StartDay + 1, Seasons.DayOf(t));
        var seen = new List<string>();
        for (int d = 0; d < r.Days * 4; d++)
        {
            sim.RunDays(1);
            if (seen.Count == 0 || seen[^1] != Seasons.Current(t).Id) seen.Add(Seasons.Current(t).Id);
        }
        Assert.Equal(["spring", "summer", "autumn", "winter", "spring"], seen);
        Assert.Equal(2, Seasons.Year(t));
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Winter closes on", StringComparison.Ordinal));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("Harvest Home", StringComparison.Ordinal));
    }

    [Fact]
    public void TheLookBlendsAcrossTheTurns()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, FreeCompanies = false });
        var t = sim.Town;
        for (int d = 0; d < 120; d++)
        {
            sim.RunDays(1);
            var (sp, su, au, wi) = Seasons.Look(t);
            Assert.Equal(1, sp + su + au + wi, 6);
            var s = Seasons.Current(t).Id;
            double own = s == "spring" ? sp : s == "summer" ? su : s == "autumn" ? au : wi;
            Assert.True(own >= 0.49, $"day {t.Day}: {s} weighs {own}");
        }
    }

    [Fact]
    public void WinterFieldsYieldLessAndTheStoresHoldTheHarvest()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        Assert.True(Seasons.Yield(t, Good.Food) < 1);
        while (Seasons.Current(t).Id != "autumn") sim.RunDays(1);
        sim.RunDays(15);
        Assert.Equal(Seasons.Stores(t), t.Market.Held(Good.Food));
        Assert.True(Seasons.Yield(t, Good.Food) > 1);
        while (Seasons.Current(t).Id != "winter") sim.RunDays(1);
        Assert.True(Seasons.Yield(t, Good.Food) < 0.6);
        Assert.True(Seasons.MarchDays(t) > 0);
        Assert.True(Seasons.Activity(t, "undead") > Seasons.Activity(t, "tribe"));
        Assert.True(t.Market.ImportPremium > 3);
    }

    [Fact]
    public void TheCouncilLaysInStoresOnlyWhenTheCellarsHoldThem()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false, FreeCompanies = false });
        var t = sim.Town;
        t.Treasury += 5000;
        Assert.False(sim.LayInStores());
        while (Seasons.Current(t).Id != "winter") sim.RunDays(1);
        t.Market.Seed(Good.Food, -t.Market.Stock(Good.Food));
        var (food, cost) = Seasons.LayInQuote(t);
        Assert.True(food > 0);
        double before = t.Treasury;
        Assert.True(sim.LayInStores());
        Assert.Equal(before - cost, t.Treasury, 6);
        Assert.Equal(food, t.Market.Stock(Good.Food), 6);
    }

    [Fact]
    public void VisitorsSpendMoreInSummerThanWinter()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(140);
        while (Seasons.Current(t).Id != "summer" || Seasons.Festival(t)) sim.RunDays(1);
        int summer = t.Seasons.Tourists;
        Assert.True(t.Seasons.TouristGold > 0);
        while (Seasons.Current(t).Id != "winter" || Seasons.Festival(t)) sim.RunDays(1);
        Assert.True(t.Seasons.Tourists < summer);
    }
}
