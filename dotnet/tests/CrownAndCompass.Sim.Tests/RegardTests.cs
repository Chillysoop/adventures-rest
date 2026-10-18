using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class RegardTests
{
    private static Simulation Player(int seed = 2) => new(new SimulationOptions { Seed = seed, AutoStories = false });

    private static Agent Hero(Town t, string cls) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), 3);

    private static Agent WithTemper(Town t, string temper)
    {
        var a = Hero(t, "fighter");
        a.Hero!.Temper = temper;
        return a;
    }

    [Fact]
    public void EveryHeroHasATemperAndEveryTagIsKnown()
    {
        var c = Content.Default();
        var r = c.Regard;
        var tags = r.Tags.Select(x => x.Id).ToHashSet();
        foreach (var t in r.Tempers)
        {
            Assert.True(t.Name != "" && t.Text != "", t.Id);
            Assert.All(t.Likes.Concat(t.Dislikes), x => Assert.Contains(x, tags));
            Assert.All(t.Classes.Keys, cls => Assert.Contains(c.Classes, x => x.Id == cls));
        }
        foreach (var d in c.Stories.Stories)
            foreach (var o in d.Stages.SelectMany(s => s.Options))
                Assert.All(o.Tags, x => Assert.True(tags.Contains(x), $"{d.Id}: {o.Label} tag {x}"));
        var sim = Player();
        sim.RunDays(30);
        Assert.All(sim.Town.Heroes, h => Assert.NotNull(r.Temper(h.Hero!.Temper)));
    }

    [Fact]
    public void HeroesJudgeAChoiceByTheirTemper()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(5);
        var kind = WithTemper(t, "free");
        var hard = WithTemper(t, "lawful");
        var s = Stories.Open(t, "hero-wager");
        Assert.NotNull(s);
        var start = Stories.StageOf(t, s!);
        int shut = start.Options.FindIndex(o => o.Tags.Contains("order"));
        var (pleased, angered) = Regard.Reactions(t, start.Options[shut].Tags);
        Assert.Contains(hard, pleased);
        Assert.Contains(kind, angered);
        Assert.NotNull(Stories.Resolve(t, s!.Id, shut));
        Assert.True(kind.Hero!.Regard < 0);
        Assert.True(hard.Hero!.Regard > 0);
        Assert.Contains("put a stop to it", kind.Hero.RegardWhy);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("word gets round") && e.Text.Contains(kind.GivenName));
        // It sways their loyalty, and fades with time.
        Assert.Contains(Loyalty.Factors(t, kind), f => f.Why == "disapproves of the mayor" && f.Points < 0);
        double before = kind.Hero.Regard;
        sim.RunDays(10);
        Assert.True(kind.Hero.Regard > before);
    }

    [Fact]
    public void AnAdmirerAsksForHelpAndAGrievanceCanEndInDismissal()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(5);
        var fan = WithTemper(t, "merciful");
        fan.Hero!.Regard = 60;
        var quest = Stories.Open(t, "personal-heirloom");
        Assert.NotNull(quest);
        Assert.Equal(fan.Id, quest!.HeroId);
        int buy = Stories.StageOf(t, quest).Options.FindIndex(o => o.Cost > 0);
        t.Treasury += 500;
        int level = fan.Hero.Level;
        int xp = fan.Hero.Xp;
        Stories.Resolve(t, quest.Id, buy);
        Assert.Equal("done", fan.Hero.Personal);
        Assert.True(fan.Hero.Xp > xp || fan.Hero.Level > level);
        // Seen through once, it is not asked again.
        Assert.False(Regard.Admirer(t, fan));

        var sour = WithTemper(t, "lawful");
        sour.Hero!.Regard = -60;
        sour.Hero.RegardWhy = "chose to take a cut and say nothing";
        var g = Stories.Open(t, "grievance");
        Assert.NotNull(g);
        Assert.Equal(sour.Id, g!.HeroId);
        Assert.Contains("take a cut and say nothing", Stories.Fill(t, g, Stories.StageOf(t, g).Text));
        int dismiss = Stories.StageOf(t, g).Options.FindIndex(o => o.Success.Effects.ContainsKey("dismiss"));
        Stories.Resolve(t, g.Id, dismiss);
        Assert.True(sour.Departed);
    }

    [Fact]
    public void AFriendInThePartySteadiesAHeroAndARivalSpoilsIt()
    {
        var sim = Player();
        var t = sim.Town;
        var a = Hero(t, "fighter");
        var b = Hero(t, "rogue");
        var r = t.Content.Regard;
        Assert.Equal((0, ""), Regard.Chemistry(r, a, [a, b]));
        a.ChangeOpinion(b.Id, 50);
        b.ChangeOpinion(a.Id, 50);
        Assert.Equal(r.Friend, Regard.Chemistry(r, a, [a, b]).Bonus);
        a.ChangeOpinion(b.Id, -100);
        Assert.Equal(r.Rival, Regard.Chemistry(r, a, [a, b]).Bonus);
    }

    [Fact]
    public void AHeroOnTheWrongSideOfTheLawCanBeFinedOrJailed()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(5);
        foreach (var h in t.Heroes) h.Hero!.Temper = "lawful";
        var rogue = WithTemper(t, "greedy");
        rogue.Gold = 100;
        Assert.True(Regard.Lawless(t, rogue));
        var s = Stories.Open(t, "hero-pilfer");
        Assert.NotNull(s);
        Assert.Equal(rogue.Id, s!.HeroId);
        int fine = Stories.StageOf(t, s).Options.FindIndex(o => o.Success.Effects.ContainsKey("fine") && o.Skill == "");
        double treasury = t.Treasury;
        Stories.Resolve(t, s.Id, fine);
        Assert.True(rogue.Gold < 100);
        Assert.True(t.Treasury > treasury);

        var jail = Stories.Open(t, "hero-pilfer");
        Assert.NotNull(jail);
        int cells = Stories.StageOf(t, jail!).Options.FindIndex(o => o.Success.Effects.ContainsKey("jail"));
        Stories.Resolve(t, jail!.Id, cells);
        Assert.NotNull(rogue.Hero!.JailedUntil);
        Assert.False(Quests.Available(rogue));
        sim.RunDays(11);
        Assert.Null(rogue.Hero.JailedUntil);
    }
}
