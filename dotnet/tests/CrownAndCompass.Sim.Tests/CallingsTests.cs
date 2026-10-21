using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CallingsTests
{
    private static Simulation Player(int seed = 2) => new(new SimulationOptions { Seed = seed, AutoStories = false });

    private static IEnumerable<StoryOutcome> Outcomes(StoryDef d) =>
        d.Stages.SelectMany(st => st.Options.SelectMany(o => new[] { o.Success, o.Failure, o.Critical, o.Partial, o.Fumble }).Append(st.Ignored)).Append(d.Ignored).OfType<StoryOutcome>();

    private static Agent Hero(Town t, string cls) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), 3);

    [Fact]
    public void EveryClassHasAStoryAndEveryCallingIsOffered()
    {
        var c = Content.Default();
        var r = c.Stories;
        foreach (var cls in c.Classes)
            Assert.Contains(r.Stories, s => s.Needs.Contains($"class:{cls.Id}"));
        foreach (var calling in r.Callings)
        {
            Assert.True(calling.Name != "" && calling.Text != "", calling.Id);
            Assert.Contains(c.Classes, x => x.Id == calling.Class);
            // A calling is offered by a story of its own class.
            Assert.Contains(r.Stories, s => s.Needs.Contains($"class:{calling.Class}") && Outcomes(s).Any(o => o.Effects.GetValueOrDefault($"calling:{calling.Id}") > 0));
            if (calling.Faction != "") Assert.Contains(c.Factions.Factions, f => f.Ref == calling.Faction);
        }
    }

    [Fact]
    public void APaladinsOathBindsThemToTheTownUntilTheyLeave()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(10);
        Hero(t, "paladin");
        var s = Stories.Open(t, "paladin-oath");
        Assert.NotNull(s);
        Assert.Equal("paladin", t.Agent(s!.HeroId!.Value).Hero!.Cls.Id);
        int at = Stories.StageOf(t, s).Options.FindIndex(o => o.Label.StartsWith("Swear the Oath of the Shield", StringComparison.Ordinal));
        var hero = t.Agent(s.HeroId.Value);
        double defence = Callings.Defence(t);
        // The oath is a roll now, but sworn either way: a poor showing only costs loyalty.
        Assert.NotNull(Stories.Resolve(t, s.Id, at));
        Assert.Equal("oath-shield", Callings.Of(t, hero)?.Id);
        Assert.True(Callings.Defence(t) > defence);
        Assert.Contains(Loyalty.Factors(t, hero), f => f.Why == "oath of the shield");
        // A sworn paladin is not asked again; the oath outlives a save.
        Assert.NotEqual(hero.Id, Stories.Open(t, "paladin-oath")?.HeroId);
        var back = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(hero.Id, back.Town.Stories.Callings.Keys.Single());

        Population.Depart(t, hero);
        sim.RunDays(1);
        Assert.Empty(t.Stories.Callings);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith($"Oath of the Shield ends with {hero.Name}", StringComparison.Ordinal));
    }

    [Fact]
    public void AHeroWhoLosesHeartRenouncesTheirCalling()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(10);
        var bard = Hero(t, "bard");
        Callings.Take(t, bard, "towns-bard");
        Assert.True(Decrees.Mood(t) > 0);
        bard.Hero!.Loyalty = 5;
        sim.RunDays(1);
        Assert.Null(Callings.Of(t, bard));
        Assert.Equal(0, Decrees.Mood(t));
    }

    [Fact]
    public void ARogueInformerHeatsTheGuildAndAFollowUpTurnsOnTheirCalling()
    {
        var sim = Player(3);
        var t = sim.Town;
        sim.RunDays(10);
        var hand = Factions.Find(t, "quiet-hand")!;
        Assert.Null(Stories.Open(t, "rogue-guild"));
        hand.Active = true;
        hand.Members.Add(t.Workers.OrderBy(a => a.Id).First().Id);
        var rogue = Hero(t, "rogue");
        Assert.Null(Stories.Open(t, "guild-favour"));
        var s = Stories.Open(t, "rogue-guild");
        Assert.NotNull(s);
        Assert.Equal("quiet-hand", s!.FactionId);
        Assert.Equal(rogue.Id, s.HeroId);
        Stories.Resolve(t, s.Id, 0);
        Assert.Equal("guild-go-between", Callings.Of(t, rogue)?.Id);

        // The go-between keeps the guild cool and the envelope coming; the guild then calls in a favour.
        hand.Heat = 20;
        double gold = t.Accounts.Get(TreasuryAccount.Callings, 0);
        Callings.Daily(t);
        Assert.Equal(gold + 1.5, t.Accounts.Get(TreasuryAccount.Callings, 0), 6);
        Assert.Equal(19, hand.Heat, 6);
        var favour = Stories.Open(t, "guild-favour");
        Assert.NotNull(favour);
        Assert.Equal(rogue.Id, favour!.HeroId);
        Assert.Contains(rogue.Name, Stories.Fill(t, favour, Stories.StageOf(t, favour).Text));
        Assert.Contains(Stories.StageOf(t, favour).Options, o => Stories.Fill(t, favour, o.Label).Contains(rogue.GivenName, StringComparison.Ordinal));
    }
}
