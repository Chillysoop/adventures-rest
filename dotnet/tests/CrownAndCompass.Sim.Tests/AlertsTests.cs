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

    [Fact]
    public void OnlyBigNewsBecomesAnAlert()
    {
        var major = new LedgerEntry(7, 30, LedgerCategory.Quest, Importance.Major, "The party returns from the Old Mill with a dragon's hoard and tales to tell", []);
        var a = Alerts.ForEntry(major);
        Assert.NotNull(a);
        Assert.Equal(Alerts.QuestNews, a!.Kind);
        Assert.Equal(AlertLevel.Minor, a.Kind.Level);
        Assert.Equal(7, a.EntryId);
        Assert.True(a.Text.Length <= 34);
        Assert.Null(Alerts.ForEntry(major with { Importance = Importance.Notable }));
        Assert.NotNull(Alerts.ForEntry(major with { Category = LedgerCategory.War, Importance = Importance.Notable }));
    }
}
