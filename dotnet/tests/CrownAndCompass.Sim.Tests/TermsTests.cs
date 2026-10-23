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
        // Hands enough for two raids: a town nobody zones keeps only the wagon's settlers (travellers move on).
        Population.Settle(t, 12);
        sim.RunDays(30);
        var site = Quests.Site(t, "goblin_warrens")!;
        site.Known = SiteKnowledge.Rumoured;
        // The goblins hold their warrens again, whatever the heroes did this month.
        site.Cleared = false;
        var farm = t.Buildings.First(b => b.Complete && b.Workers.Any(id => t.Agent(id).Hero == null && t.Seats.All(x => x.HolderId != id)) && b.Workers.All(id => t.Buildings.All(o => o.OwnerId != id)));
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
        // Nobody goes for them: the clock runs out.
        second.DueTick = t.Tick;
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
        // The goblins hold their warrens again, whatever the heroes did this month.
        site.Cleared = false;
        site.ThoughtCleared = false;
        t.Treasury += 1000;
        // A story, or the heroes themselves, may already have one going there.
        t.Bounties.RemoveAll(x => x.SiteId == "goblin_warrens" && x.Status is BountyStatus.Open or BountyStatus.Taken);
        var b = sim.PostBounty("goblin_warrens", 100, "fetch")!;
        b.Errand = "ward";
        t.Saga.Dread = 50;
        Errands.Keep(t, b);
        Assert.True(t.Saga.Dread < 50);
        Assert.Equal("", b.Errand);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("keeps its word"));
    }

    [Fact]
    public void AWipedCompanysGearLiesWhereItFellUntilAPartyBringsItHome()
    {
        var sim = Player();
        var t = sim.Town;
        var goblins = Quests.Site(t, "goblin_warrens")!;
        goblins.Known = SiteKnowledge.Rumoured;
        var recover = t.Content.Bounties.Term("recover");
        Assert.True(recover.Fallen);
        Assert.NotNull(Terms.Blocked(t, goblins, recover));

        var heroes = Party(t, 3);
        var charm = t.Content.Library.Items.First(x => x.Rarity == "uncommon");
        heroes[0].Hero!.Magic["trinket"] = charm;
        var party = new Party { Id = 77, Name = "Lost Lanterns", Members = heroes.Select(a => a.Id).ToList(), SiteId = goblins.Def.Id };
        var lost = Remains.Lie(t, party, goblins, heroes);
        Assert.Same(lost, Remains.At(t, goblins.Def.Id));
        Assert.Contains(charm, lost.Gear);

        // The card tells of it, and its first answer posts the recovery bounty.
        t.Treasury = 5000;
        var card = Stories.Lost(t, lost, party);
        Assert.NotNull(card);
        Assert.Null(Stories.Lost(t, lost, party));
        Assert.Null(Terms.Blocked(t, goblins, recover));
        Assert.NotNull(Stories.Resolve(t, card!.Id, 0));
        var bounty = t.Bounties.Single(b => b.SiteId == goblins.Def.Id);
        Assert.Equal("recover", bounty.Terms);

        // On those terms a party goes only as far as the fallen.
        var run = Expedition.Run(new Rng(3), t.Content, goblins.Def, Party(t, 6), terms: recover);
        if (run.GoalMet) Assert.Contains(run.Lines, l => l.Contains("fallen"));

        int stock = t.Armory.Stock.Count;
        Remains.Recover(t, lost, party);
        Assert.Null(Remains.At(t, goblins.Def.Id));
        Assert.Equal(stock + 1, t.Armory.Stock.Count);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("bring the Lost Lanterns home"));
    }

    [Fact]
    public void AHeistSneaksPastRoomsAndAnAlarmSharedWithTheVaultCanSlamItShut()
    {
        var sim = Player();
        var t = sim.Town;
        var site = Quests.Site(t, "goblin_warrens")!.Def;
        bool slipped = false, raised = false, vault = false;
        for (int seed = 1; seed < 60 && !(slipped && raised && vault); seed++)
        {
            var r = Expedition.Run(new Rng(seed), t.Content, site, Party(t, 4), terms: t.Content.Bounties.Term("fetch"));
            slipped |= r.Lines.Any(l => l.Contains("past unseen"));
            raised |= r.Lines.Any(l => l.Contains("The alarm rises") || l.Contains("the alarm goes up"));
            vault |= r.Lines.Any(l => l.Contains("on the vault"));
        }
        Assert.True(slipped);
        Assert.True(raised);
        Assert.True(vault);
    }

    [Fact]
    public void ACasterSolvesItTheirOwnWayAndAJobIsToldAsATale()
    {
        var sim = Player();
        var t = sim.Town;
        var site = Quests.Site(t, "goblin_warrens")!.Def;
        var party = Party(t, 8);
        party.Add(Quests.CreateHero(t, t.Content.Classes.First(x => x.Id == "wizard"), 8));
        bool way = false;
        for (int seed = 1; seed < 20 && !way; seed++)
            way = Expedition.Run(new Rng(seed), t.Content, site, party).Lines.Any(l => l.Contains("no roll needed") || l.Contains("charms the guards") || l.Contains("unseen servant") || l.Contains("divination"));
        Assert.True(way);
        var goblins = Quests.Site(t, "goblin_warrens")!;
        goblins.Known = SiteKnowledge.Rumoured;
        var b = sim.PostBounty("goblin_warrens", 100, "fetch");
        Assert.NotNull(b);
        Assert.Equal("vault", b!.Plot);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("the Vault Job"));
    }
}
