using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TermsTests
{
    private static Simulation Player() => new(new SimulationOptions { Seed = 4, AutoMayor = false });

    private static List<Agent> Party(Town t, int level) =>
        new[] { "fighter", "cleric", "bard", "rogue" }.Select(c => Quests.CreateHero(t, t.Content.Classes.First(x => x.Id == c), level)).ToList();

    [Fact]
    public void EveryTermIsKnownAndTalkingNeedsAFoeThatBargains()
    {
        var sim = Player();
        var t = sim.Town;
        var b = t.Content.Bounties;
        Assert.Contains(b.Terms, x => x.Id == "slay");
        var tags = t.Content.Regard.Tags.Select(x => x.Id).ToHashSet();
        Assert.All(b.Terms, x => Assert.All(x.Tags, tag => Assert.Contains(tag, tags)));
        var wolves = Quests.Site(t, "wolfwood")!;
        var goblins = Quests.Site(t, "goblin_warrens")!;
        Assert.NotNull(Terms.Blocked(t, wolves, b.Term("parley")));
        Assert.Null(Terms.Blocked(t, goblins, b.Term("parley")));
        // A rescue needs captives held there.
        Assert.NotNull(Terms.Blocked(t, goblins, b.Term("rescue")));
        goblins.Known = SiteKnowledge.Rumoured;
        Assert.Null(sim.PostBounty("goblin_warrens", 100, "rescue"));
        var bounty = sim.PostBounty("goblin_warrens", 100, "drive");
        Assert.NotNull(bounty);
        Assert.Equal("drive", bounty!.Terms);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("to drive the occupants out of"));
    }

    [Fact]
    public void DrivingThemOffEndsHalfwayAndTalkingCanEndItWithoutAFight()
    {
        var sim = Player();
        var t = sim.Town;
        var site = Quests.Site(t, "goblin_warrens")!.Def;
        var drive = Expedition.Run(new Rng(7), t.Content, site, Party(t, 5), terms: t.Content.Bounties.Term("drive"));
        Assert.Equal(ExpeditionOutcome.Cleared, drive.Outcome);
        Assert.True(drive.Driven);
        Assert.Contains(drive.Lines, l => l.Contains("breaks and runs"));
        Assert.DoesNotContain(drive.Lines, l => l.Contains("heart of the place"));

        // Over enough tries a silver tongue talks the goblins out without a blow.
        bool talked = false;
        for (int seed = 1; seed < 30 && !talked; seed++)
        {
            var r = Expedition.Run(new Rng(seed), t.Content, site, Party(t, 5), terms: t.Content.Bounties.Term("parley"));
            if (r.Talked)
            {
                talked = true;
                Assert.Equal(ExpeditionOutcome.Cleared, r.Outcome);
                Assert.DoesNotContain(r.Kills.Values, k => k > 0);
            }
        }
        Assert.True(talked);
    }

    [Fact]
    public void CaptivesComeHomeIfRescuedAndAreLostIfNot()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(30);
        var site = Quests.Site(t, "goblin_warrens")!;
        site.Known = SiteKnowledge.Rumoured;
        var farm = t.Buildings.First(b => b.Complete && b.Workers.Count > 0 && b.Workers.All(id => t.Buildings.All(o => o.OwnerId != id)));
        // Take until a raid carries someone off (it doesn't every time).
        string said = "";
        for (int i = 0; i < 20 && said == ""; i++) said = Captives.Take(t, site, farm);
        Assert.NotEqual("", said);
        var held = Captives.At(t, site.Def.Id)!;
        Assert.All(held.Captives, id => Assert.True(t.Agent(id).Departed));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Captives);
        Assert.Null(Terms.Blocked(t, site, t.Content.Bounties.Term("rescue")));

        Captives.Free(t, held, null);
        Assert.All(held.Captives, id => Assert.False(t.Agent(id).Departed));
        Assert.True(t.Stories.Precedents.ContainsKey("captives-freed"));

        string again = "";
        for (int i = 0; i < 20 && again == ""; i++) again = Captives.Take(t, site, farm.Workers.Count > 0 ? farm : t.Buildings.First(b => b.Complete && b.Workers.Count > 0 && b.Workers.All(id => t.Buildings.All(o => o.OwnerId != id))));
        Assert.NotEqual("", again);
        var second = Captives.At(t, site.Def.Id)!;
        while (t.Tick < second.DueTick) sim.Step();
        Captives.Daily(t);
        Assert.True(second.Done && !second.Freed);
        Assert.All(second.Captives, id => Assert.True(t.Agent(id).Dead));
        Assert.True(t.Stories.Precedents.ContainsKey("captives-lost"));
    }

    [Fact]
    public void AMagesErrandPaysTheTownInASpell()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(30);
        var site = Quests.Site(t, "goblin_warrens")!;
        site.Known = SiteKnowledge.Rumoured;
        t.Treasury += 1000;
        var b = sim.PostBounty("goblin_warrens", 100, "fetch")!;
        b.Errand = "ward";
        t.Saga.Dread = 50;
        Errands.Keep(t, b);
        Assert.True(t.Saga.Dread < 50);
        Assert.Equal("", b.Errand);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("keeps its word"));
    }
}
