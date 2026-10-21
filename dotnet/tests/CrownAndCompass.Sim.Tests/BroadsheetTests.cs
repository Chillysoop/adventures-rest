using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BroadsheetTests
{
    private static Simulation Player() => new(new SimulationOptions { Seed = 3, AutoStories = false });

    [Fact]
    public void AWeeklyIssueCarriesTheWeeksNews()
    {
        var sim = Player();
        var t = sim.Town;
        var tags = t.Content.Regard.Tags.Select(x => x.Id).ToHashSet();
        Assert.All(t.Content.Broadsheet.Slants, s => Assert.All(s.Tags, x => Assert.Contains(x, tags)));
        sim.RunDays(120);
        Assert.True(Broadsheet.Printing(t));
        var issues = t.Press.Issues;
        Assert.True(issues.Count >= 5);
        Assert.All(issues, i => Assert.Equal(0, i.Day % t.Content.Broadsheet.Every));
        Assert.Contains(issues, i => i.Stories.Count > 0);
        Assert.All(issues, i => Assert.True(i.Stories.Select(s => s.Section).Distinct().Count() == i.Stories.Count));
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("A printer sets up shop"));
    }

    [Fact]
    public void APaidFrontPageWorksAndCanBeFoundOut()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(60);
        t.Treasury += 5000;
        // A hero at home if there is one: one out on the road may not come back before the paper prints.
        var hero = t.Heroes.Where(h => !h.Dead && !h.Departed).OrderBy(h => h.Hero!.AwayUntil == null ? 0 : 1).ThenByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).First();
        int renown = hero.Hero!.Renown;
        double regard = hero.Hero.Regard;
        Assert.NotNull(sim.SlantBroadsheet("hero"));
        Assert.Null(sim.SlantBroadsheet("hero", hero.Id));
        Assert.NotNull(sim.SlantBroadsheet("praise"));
        sim.RunDays(Broadsheet.NextDay(t) - t.Day + 1);
        var issue = Broadsheet.Latest(t)!;
        Assert.Equal("hero", issue.Slant);
        Assert.Contains(hero.Name, issue.Headline);
        Assert.True(hero.Hero.Renown >= renown + 3);
        Assert.True(hero.Hero.Regard > regard);

        // Flattery wins approval, but run it every week and it is found out.
        bool exposed = false;
        for (int i = 0; i < 12 && !exposed; i++)
        {
            double before = t.Mandate = 40;
            Assert.Null(sim.SlantBroadsheet("praise"));
            sim.RunDays(Broadsheet.NextDay(t) - t.Day + 1);
            exposed = Broadsheet.Latest(t)!.Exposed;
            if (!exposed && i == 0) Assert.True(t.Mandate != before);
        }
        Assert.True(exposed);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("lets slip who paid"));
    }
}
