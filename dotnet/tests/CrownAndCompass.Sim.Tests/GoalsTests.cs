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
        // Every goal with a button points at one that is on screen, and its icon exists.
        var tabs = TutorialTests.ClientTabs();
        var icons = Path.Combine(Repo(), "godot", "assets", "icons");
        foreach (var g in rules.Goals)
        {
            Assert.True(PointKnown(t, g.Point, tabs), $"goal {g.Id} points at \"{g.Point}\", which is not on screen");
            // A click on any goal takes the player where to act on it.
            Assert.True(g.Goes != "" && PointKnown(t, g.Goes, tabs), $"goal {g.Id} opens \"{g.Goes}\" when clicked, which is not on screen");
            Assert.True(g.When == "" || Goals.Measure(t, g.When) != null, $"goal {g.Id} waits on \"{g.When}\", which means nothing");
            Assert.True(g.Icon == "" || File.Exists(Path.Combine(icons, g.Icon + ".svg")), $"goal {g.Id} shows icon \"{g.Icon}\", which is not in godot/assets/icons");
        }
        Assert.Equal(rules.Goals[0].Id, Goals.Next(t)?.Id);
        // A new town starts with nothing met: the first goals are open and not yet done.
        Assert.Equal(rules.Goals.Take(rules.Shown).Select(g => g.Id), Goals.Open(t).Select(g => g.Id));
        Assert.All(Goals.Open(t), g => Assert.True(Goals.Progress(t, g).Now < g.Target, $"goal {g.Id} is met before the town has done anything"));
    }

    private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));

    internal static bool PointKnown(Town t, string point, List<string> tabs) =>
        point == "" || Tutorial.Anchors.Contains(point) || point is "brush:explore" or "brush:patrol"
        || (point.StartsWith("tab:", StringComparison.Ordinal) && tabs.Contains(point[4..].Split('>')[0].Split(':')[0]) && PageKnown(point[4..].Split('>')[0]))
        || (point.StartsWith("zone:", StringComparison.Ordinal) && Enum.TryParse<Zone>(point[5..], out var z) && z != Zone.None)
        || (point.StartsWith("place:", StringComparison.Ordinal) && t.Content.Buildings.Any(b => b.Id == point[6..] && b.Civic));

    /// <summary>A window page named after the colon ("City:Growth") is one its pane draws (<c>p.Page("Growth", ...)</c> in godot/src).</summary>
    private static bool PageKnown(string spec)
    {
        int colon = spec.IndexOf(':');
        if (colon < 0) return true;
        string page = $"p.Page(\"{spec[(colon + 1)..]}\"";
        return Directory.EnumerateFiles(Path.Combine(Repo(), "godot", "src"), "*.cs", SearchOption.AllDirectories).Any(f => File.ReadAllText(f).Contains(page));
    }

    [Fact]
    public void EveryLineOfTheWhatNextCardOpensSomewhere()
    {
        var sim = Player();
        var t = sim.Town;
        var tabs = TutorialTests.ClientTabs();
        // The charter list done: the card turns to the next charter's terms.
        t.Goals.Closed = true;
        var (_, aims) = Ahead.Now(t);
        Assert.NotEmpty(aims);
        Assert.All(aims, a => Assert.True(a.Goes != "" && PointKnown(t, a.Goes, tabs), $"\"{a.Text}\" opens \"{a.Goes}\" when clicked, which is not on screen"));
    }

    [Fact]
    public void EveryAlertActNamesAPageAndAControlTheClientDraws()
    {
        var src = string.Concat(Directory.EnumerateFiles(Path.Combine(Repo(), "godot", "src"), "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
        foreach (var k in Alerts.Kinds.Where(k => k.Act != ""))
        {
            int gt = k.Act.IndexOf('>');
            string page = gt < 0 ? "" : k.Act[..gt], item = gt < 0 ? k.Act : k.Act[(gt + 1)..];
            Assert.True(page == "" || src.Contains($"p.Page(\"{page}\""), $"alert {k.Id} opens page \"{page}\", which no window draws");
            Assert.True(item == "" || src.Contains($"\"{item}"), $"alert {k.Id} lights \"{item}\", which no window draws");
        }
    }

    [Fact]
    public void AGoalIsMetOncePaysOnceAndShowsTheNext()
    {
        var sim = Player();
        var t = sim.Town;
        // The painting goals before it, as if met.
        foreach (var g in t.Content.Goals.Goals.TakeWhile(g => g.Id != "first-bounty"))
        {
            t.Goals.Done.Add(g.Id);
            t.Goals.DoneDay.Add(0);
        }
        Goals.Hourly(t);
        Assert.DoesNotContain("first-bounty", t.Goals.Done);
        Assert.NotNull(sim.PostBounty("wolfwood", 40));
        double mandate = t.Mandate;
        Goals.Hourly(t);
        Goals.Hourly(t);
        Assert.Contains("first-bounty", t.Goals.Done);
        var def = t.Content.Goals.Goals.First(g => g.Id == "first-bounty");
        Assert.Equal(Math.Min(100, mandate + def.Mandate), t.Mandate);
        Assert.Single(t.Ledger.Entries, e => e.Text.StartsWith("Charter goal met: Post a bounty", StringComparison.Ordinal));
        Assert.DoesNotContain(Goals.Open(t), g => g.Id == "first-bounty");
        Assert.Equal(t.Content.Goals.Shown, Goals.Open(t).Count);
        Assert.Equal(t.Content.Goals.Goals.Where(g => !t.Goals.Done.Contains(g.Id)).Take(t.Content.Goals.Shown), Goals.Open(t));

        // Gold rewards go on the books under their own account, once.
        double gold = t.Treasury;
        var tavern = t.Content.Goals.Goals.First(g => g.Id == "tavern");
        foreach (var g in t.Content.Goals.Goals.TakeWhile(g => g != tavern).Where(g => !t.Goals.Done.Contains(g.Id)))
        {
            t.Goals.Done.Add(g.Id);
            t.Goals.DoneDay.Add(0);
        }
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

    [Fact]
    public void AfterTheListTheCardShowsTheNextCharter()
    {
        var sim = Player();
        var t = sim.Town;
        Assert.Empty(Ahead.Now(t).Aims);
        t.Goals.Closed = true;
        var (title, aims) = Ahead.Now(t);
        Assert.Contains(City.NextTier(t)!.Name, title);
        Assert.InRange(aims.Count, 1, Ahead.Most);
        Assert.All(aims, a => Assert.InRange(a.Share, 0, 1));
    }
}
