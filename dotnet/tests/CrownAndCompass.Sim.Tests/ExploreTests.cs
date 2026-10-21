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
        // A step takes the days its chapter says, not hours.
        var ch = Explore.Def(sim.Town, waiting).Chapters[chapter];
        int started = sim.Town.Tick;
        for (int h = 0; h < 24 * 8 && waiting.State == FindState.Busy; h++) sim.Step();
        Assert.NotEqual(FindState.Busy, waiting.State);
        Assert.True(sim.Town.Tick - started >= ch.Span);
        Assert.True(waiting.Chapter > chapter || waiting.Log.Count > 1);
    }

    /// <summary>Every chapter of every find is timed in days in explore.json, and the deep ones take longer than the quick ones.</summary>
    [Fact]
    public void ChaptersTakeDaysThatFitTheFind()
    {
        var rules = Content.Default().Explore;
        Assert.All(rules.Kinds.SelectMany(k => k.Chapters), ch => Assert.True(ch.Days > 0, $"{ch.Title} has no days"));
        double Total(string kind) => rules.Kind(kind)!.Chapters.Sum(c => c.Days);
        Assert.True(Total("ruin") > Total("company"));
        Assert.True(Total("lair") > Total("caravan"));
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

    /// <summary>A spot the mayor marks is walked to and looked round, then the company comes home; one too near the town is refused.</summary>
    [Fact]
    public void CompanyLooksAtAMarkedSpot()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoBounties = true, AutoCouncil = true, AutoDefense = true, AutoPlanning = true, AutoExplore = false });
        sim.RunDays(40);
        var t = sim.Town;
        t.Book(TreasuryAccount.Exploring, 500);
        Assert.Null(sim.MarkSpot(0.1, 0.1));
        var region = t.Regions.First(r => r.Ring == 0);
        double a = region.Bearing * Math.PI / 180;
        var (x, y) = (region.Days * Math.Sin(a), -region.Days * Math.Cos(a));
        Assert.Equal(region.Id, Explore.RegionAt(t, x, y)!.Id);
        var c = sim.MarkSpot(x, y);
        Assert.NotNull(c);
        Assert.NotNull(Explore.SpotBlocked(t, x + 0.05, y));
        Survey? s = null;
        for (int h = 0; h < 24 * 60 && (s == null || Explore.Out(s)); h++)
        {
            sim.Step();
            s ??= t.Surveys.FirstOrDefault(v => v.CommissionId == c!.Id);
            if (s != null && Explore.Waiting(t).FirstOrDefault(f => f.SurveyId == s.Id) is { } f) sim.OrderExplorers(f.Id, "home");
        }
        Assert.NotNull(s);
        Assert.Equal(x, s!.SpotX);
        Assert.NotEqual(SurveyPhase.Outbound, s.Phase);
        Assert.Contains(s.Journal, j => j.Contains("spot the council marked", StringComparison.Ordinal));
    }
}
