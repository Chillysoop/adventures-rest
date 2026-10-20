using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TrendsTests
{
    [Fact]
    public void KeepsAMonthOfBooks()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(45);
        var t = sim.Town;
        Assert.Equal(30, Trends.Span(t));
        Assert.Equal(t.Day - 1, t.Trends.Days[^1].Day);
        // The month's net is its books summed.
        double net = Js.Sum(t.Trends.Days, d => Js.Sum(d.Accounts.Values, v => v));
        Assert.Equal(Js.Round(net), Trends.Net(t));
        var books = Trends.Books(t);
        Assert.InRange(Js.Sum(books, b => b.Amount), net - books.Count, net + books.Count);
        Assert.True(Trends.Eaten(t) > 0);
    }

    [Fact]
    public void RunwayOnlyWhileLosingMoney()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(20);
        var t = sim.Town;
        if (Trends.Net(t) >= 0) Assert.Null(Trends.Runway(t));
        else Assert.NotNull(Trends.Runway(t));
        t.Treasury = 10;
        Assert.True(Trends.MoneyTrouble(t));
    }
}
