using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ExploreTests
{
    private static List<string> Ledger(Simulation sim) => sim.Town.Ledger.Entries.Select(e => e.Format()).ToList();

    /// <summary>The stand-in council sends explorers out, and what they find is reported before anyone acts on it.</summary>
    [Fact]
    public void ExplorersFindThingsAndReportThem()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoBounties = true, AutoCouncil = true, AutoDefense = true, AutoPlanning = true });
        sim.RunDays(300);
        var t = sim.Town;
        Assert.NotEmpty(t.Surveys);
        Assert.NotEmpty(t.Finds);
        Assert.Contains(t.Surveys, s => s.Phase == SurveyPhase.Home);
        Assert.All(t.Finds, f => Assert.True(f.ReportTick >= f.FoundTick));
        Assert.All(t.Finds.Where(f => f.SiteId != null), f => Assert.True(t.Sites.First(s => s.Def.Id == f.SiteId).Found));
    }

    /// <summary>A player's own report waits for an order; digging in starts the next chapter.</summary>
    [Fact]
    public void PlayerOrdersTheNextChapter()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoBounties = true, AutoCouncil = true, AutoDefense = true, AutoPlanning = true, AutoExplore = false });
        Find? waiting = null;
        for (int h = 0; h < 24 * 300 && waiting == null; h++)
        {
            sim.Step();
            waiting = Explore.Waiting(sim.Town).FirstOrDefault(f => Explore.Choices(sim.Town, f).Any(c => c.Act == "dig" && c.Blocked == null));
        }
        Assert.NotNull(waiting);
        int chapter = waiting!.Chapter;
        Assert.True(sim.OrderExplorers(waiting.Id, "dig"));
        Assert.Equal(FindState.Busy, waiting.State);
        for (int h = 0; h < 24 * 4 && waiting.State == FindState.Busy; h++) sim.Step();
        Assert.True(waiting.Chapter > chapter || waiting.Log.Count > 1);
    }

    /// <summary>Saving with explorers in the field plays on exactly as the run that never stopped.</summary>
    [Fact]
    public void SavesWithExplorersOut()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoBounties = true, AutoCouncil = true, AutoDefense = true, AutoPlanning = true });
        for (int h = 0; h < 24 * 300 && !sim.Town.Surveys.Any(Explore.Out); h++) sim.Step();
        Assert.Contains(sim.Town.Surveys, Explore.Out);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(30);
        loaded.RunDays(30);
        Assert.Equal(Ledger(sim), Ledger(loaded));
    }
}
