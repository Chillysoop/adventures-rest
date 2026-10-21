using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GoalsTests
{
    private static Simulation Player(int seed = 4) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = false, AutoPlanning = false, AutoBounties = false });

    [Fact]
    public void EveryGoalMeasuresSomethingAndPaysSomething()
    {
        var t = Player().Town;
        var rules = t.Content.Goals;
        Assert.True(rules.Shown > 0 && rules.Goals.Count > rules.Shown);
        Assert.Equal(rules.Goals.Count, rules.Goals.Select(g => g.Id).Distinct().Count());
        foreach (var g in rules.Goals)
        {
            Assert.True(Goals.Measure(t, g.Measure) != null, $"goal {g.Id} measures \"{g.Measure}\", which means nothing");
            Assert.True(g.Text != "" && g.Label != "" && g.Target > 0, $"goal {g.Id} needs text, a label and a target");
            Assert.True(Goals.Reward(g) != "", $"goal {g.Id} has no reward");
            // Small: the stand-in mayor meets these too, and the sweep must not move.
            Assert.True(g.Gold <= 30 && g.Mandate <= 5 && g.Festival <= 2, $"goal {g.Id} pays too much");
        }
        // A new town starts with nothing met: the first goals are open and not yet done.
        Assert.Equal(rules.Goals.Take(rules.Shown).Select(g => g.Id), Goals.Open(t).Select(g => g.Id));
        Assert.All(Goals.Open(t), g => Assert.True(Goals.Progress(t, g).Now < g.Target, $"goal {g.Id} is met before the town has done anything"));
    }

    [Fact]
    public void AGoalIsMetOncePaysOnceAndShowsTheNext()
    {
        var sim = Player();
        var t = sim.Town;
        Goals.Hourly(t);
        Assert.DoesNotContain("first-bounty", t.Goals.Done);
        Assert.NotNull(sim.PostBounty("wolfwood", 40));
        double mandate = t.Mandate;
        Goals.Hourly(t);
        Goals.Hourly(t);
        Assert.Contains("first-bounty", t.Goals.Done);
        var def = t.Content.Goals.Goals.First(g => g.Id == "first-bounty");
        Assert.Equal(Math.Min(100, mandate + def.Mandate), t.Mandate);
        Assert.Single(t.Ledger.Entries, e => e.Text.StartsWith("Charter goal met: Post your first bounty", StringComparison.Ordinal));
        Assert.DoesNotContain(Goals.Open(t), g => g.Id == "first-bounty");
        Assert.Equal(t.Content.Goals.Shown, Goals.Open(t).Count);
        Assert.Contains(Goals.Open(t), g => g.Id == t.Content.Goals.Goals[t.Content.Goals.Shown].Id);

        // Gold rewards go on the books under their own account, once.
        double gold = t.Treasury;
        var tavern = t.Content.Goals.Goals.First(g => g.Id == "tavern");
        var lot = new Lot(t.Lots.Count, Town.Center + 8, Town.Center + 8) { W = 4, D = 3, Zone = Zone.Commercial };
        t.AddBuilding(t.Content.Building("tavern"), lot, null, "the Test Tavern", true);
        Goals.Hourly(t);
        Goals.Hourly(t);
        Assert.Equal(gold + tavern.Gold, t.Treasury);
        Assert.Equal(tavern.Gold, t.Accounts.Get(Goals.Account, 0));
    }

    [Fact]
    public void PaintingHomesCountsOnlyTheLandPaintedSinceTheCharter()
    {
        var sim = Player();
        var t = sim.Town;
        var goal = t.Content.Goals.Goals.First(g => g.Id == "paint-homes");
        Assert.True(goal.Gain);
        Assert.Equal(0, Goals.Progress(t, goal).Now);
        var dabs = Enumerable.Range(0, 9).Select(i => (Town.Center - 4 + i, Town.Center + 4.0)).ToList();
        Assert.True(sim.PaintZone(dabs, 2, Zone.Residential) > 0);
        Assert.True(Goals.Progress(t, goal).Now > 0);
        sim.PaintZone(Enumerable.Range(0, 9).Select(i => (Town.Center - 4 + i, Town.Center - 4.0)), 2, Zone.Residential);
        Goals.Hourly(t);
        Assert.Contains("paint-homes", t.Goals.Done);
    }

    [Fact]
    public void AnUnfinishedListLapsesQuietly()
    {
        var sim = Player();
        var t = sim.Town;
        t.Tick = (t.Content.Goals.Lapse + 1) * SimTime.HoursPerDay;
        double gold = t.Treasury;
        Goals.Hourly(t);
        Assert.True(t.Goals.Closed);
        Assert.Empty(Goals.Open(t));
        Assert.Equal(gold, t.Treasury);
    }

    [Fact]
    public void SaveAndLoadKeepTheGoals()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(8);
        Assert.NotEmpty(sim.Town.Goals.Done);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(sim.Town.Goals.Done, loaded.Town.Goals.Done);
        Assert.Equal(sim.Town.Goals.DoneDay, loaded.Town.Goals.DoneDay);
        Assert.Equal(sim.Town.Goals.Closed, loaded.Town.Goals.Closed);
        Assert.Equal(Goals.Open(sim.Town).Select(g => g.Id), Goals.Open(loaded.Town).Select(g => g.Id));
        sim.RunDays(10);
        loaded.RunDays(10);
        Assert.Equal(sim.Town.Goals.Done, loaded.Town.Goals.Done);
        Assert.Equal(sim.Town.Treasury, loaded.Town.Treasury);
    }
}
