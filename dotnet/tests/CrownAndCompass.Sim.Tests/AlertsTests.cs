using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AlertsTests
{
    [Fact]
    public void StandingAlertsHaveOneKeyEachAndAKnownKind()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        for (int d = 0; d < 150; d++)
        {
            sim.RunDays(1);
            var list = Alerts.Standing(sim.Town);
            Assert.Equal(list.Count, list.Select(a => a.Key).Distinct().Count());
            Assert.All(list, a => Assert.Contains(a.Kind, Alerts.Kinds));
        }
    }

    [Fact]
    public void AnAlertClearsOnceItIsResolved()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(5);
        var t = sim.Town;
        double gold = t.Treasury;
        t.Treasury = 10;
        Assert.Contains(Alerts.Standing(t), a => a.Key == "treasury" && a.Kind.Level == AlertLevel.Urgent);
        t.Treasury = gold + 100;
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == "treasury");
    }

    [Fact]
    public void HeavyTaxesRaiseTheSettlersAlertAndNameThemAsTheCause()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(30);
        var t = sim.Town;
        var draw = Population.Appeal(t);
        Assert.Equal(Math.Max(0.05, Math.Min(1.6, draw.Parts.Sum(p => p.Value))), draw.Total, 9);
        // Steep enough to drown whatever else draws settlers on this day, however the campaign went.
        t.TaxRate = 0.6;
        var a = Assert.Single(Alerts.Standing(t), a => a.Key == "settlers");
        Assert.Contains("taxes at 60%", a.Tip);
        Assert.Equal(AlertWindow.City, a.Kind.Window);
    }

    [Fact]
    public void ReadingAlertsLeavesTheTownUntouched()
    {
        var quiet = new Simulation(new SimulationOptions { Seed = 4 });
        var watched = new Simulation(new SimulationOptions { Seed = 4 });
        for (int h = 0; h < 24 * 80; h++)
        {
            quiet.Step();
            watched.Step();
            Alerts.Standing(watched.Town);
        }
        Assert.Equal(quiet.Town.Ledger.Entries.Select(e => e.Format()), watched.Town.Ledger.Entries.Select(e => e.Format()));
    }
}
