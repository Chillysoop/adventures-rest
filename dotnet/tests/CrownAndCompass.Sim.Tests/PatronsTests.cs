using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PatronsTests
{
    private static Agent Hero(Town t, string cls, int level = 3) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);

    private static Town NewTown() => new Simulation(new SimulationOptions { Seed = 4, Enemy = false }).Town;

    [Fact]
    public void EveryPatronIsWholeAndHasALineage()
    {
        var c = Content.Default();
        var r = c.Patrons;
        Assert.True(r.Patrons.Count >= 4);
        foreach (var p in r.Patrons)
        {
            Assert.NotEmpty(p.Deeds);
            Assert.NotEmpty(p.Boon);
            Assert.All(p.Boon.Keys, k => Assert.Contains(k, new[] { "attack", "ac", "skill" }));
            Assert.All(p.Deeds.Keys, k => Assert.Contains(k, new[] { "kills", "delves", "renown", "infamy", "gold", "lore" }));
            Assert.NotEmpty(p.Familiars);
            Assert.NotEqual("", p.Price.Text);
            Assert.NotNull(Annals.LineageOf(c.History, "patron:" + p.Id));
            Assert.NotNull(c.Stories.Story(p.Id + "-design"));
        }
        Assert.NotNull(r.Patron(r.Bargain.Fiend));
    }

    [Fact]
    public void AWarlockIsSwornWithAFamiliarAndFavourLendsItsBoon()
    {
        var t = NewTown();
        var w = Hero(t, "warlock");
        Patrons.Daily(t);
        var s = w.Hero!;
        Assert.NotEqual("", s.Patron);
        Assert.NotEqual("", s.Familiar);
        Assert.Contains(Patrons.Of(t.Content, s)!.Familiars, f => f == s.Familiar);
        int baseAttack = Gear.Attack(t.Content, w) - Patrons.Boon(t.Content, s, "attack");
        s.Favour = 4;
        int up = Patrons.Boon(t.Content, s, "attack") + Patrons.Boon(t.Content, s, "ac") + Patrons.Boon(t.Content, s, "skill");
        s.Favour = -4;
        int down = Patrons.Boon(t.Content, s, "attack") + Patrons.Boon(t.Content, s, "ac") + Patrons.Boon(t.Content, s, "skill");
        Assert.True(up > down, "favour lends a boon that turns against them below zero");
        Assert.Equal(baseAttack + Patrons.Boon(t.Content, s, "attack"), Gear.Attack(t.Content, w));
    }

    [Fact]
    public void APatronJudgesItsHeroByItsDeeds()
    {
        var t = NewTown();
        var h = Hero(t, "fighter");
        Patrons.Swear(t, h, "hollow-prince");
        var s = h.Hero!;
        int start = s.Favour;
        var r = t.Content.Patrons;
        // Blood in its name pleases it.
        s.Kills += 6;
        s.PactJudged = t.Day - r.JudgeEvery;
        Patrons.Daily(t);
        Assert.Equal(start + 1, s.Favour);
        // Idleness does not.
        s.PactJudged = t.Day - r.JudgeEvery;
        Patrons.Daily(t);
        Assert.Equal(start, s.Favour);
    }

    [Fact]
    public void TheTopBringsAGiftAndTheBottomItsPrice()
    {
        var t = NewTown();
        var r = t.Content.Patrons;
        var h = Hero(t, "fighter", 4);
        Patrons.Swear(t, h, "hollow-prince");
        var s = h.Hero!;
        int xp = s.Xp;
        s.Favour = r.Max - 1;
        Patrons.Shift(t, h, 1);
        Assert.Equal(r.AfterGift, s.Favour);
        Assert.True(s.Xp > xp);
        s.Favour = r.Min + 1;
        int taint = s.Taint, hp = s.Hp;
        Patrons.Shift(t, h, -1);
        Assert.Equal(r.AfterPrice, s.Favour);
        Assert.True(s.Taint > taint);
        Assert.True(s.Hp < hp);
    }

    [Fact]
    public void BreakingAPactEndsItForGood()
    {
        var t = NewTown();
        var w = Hero(t, "warlock");
        Patrons.Daily(t);
        Patrons.Break(t, w);
        Patrons.Daily(t);
        Assert.Equal("", w.Hero!.Patron);
        Assert.Equal("", w.Hero.Familiar);
    }

    [Fact]
    public void APatronCourtsAHeroAndPressesItsDesign()
    {
        var t = NewTown();
        var h = Hero(t, "fighter");
        var court = Stories.Open(t, "patron-courts");
        Assert.NotNull(court);
        Assert.Equal(h.Id, court!.HeroId);
        Assert.NotNull(court.Patron);
        Stories.Resolve(t, court.Id, 0);
        Assert.Equal(court.Patron, h.Hero!.Patron);
        // Its design waits until its sworn have pleased it enough.
        string design = h.Hero.Patron + "-design";
        h.Hero.Favour = 1;
        Assert.Null(Stories.Open(t, design));
        h.Hero.Favour = t.Content.Patrons.DesignAt;
        var s = Stories.Open(t, design);
        Assert.NotNull(s);
        Assert.Contains(t.Content.Patrons.Patron(h.Hero.Patron)!.Name, t.Ledger.Entries[^1].Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ABargainInTheVeiledRoomSwearsTheHeroAndLeavesADarkGift()
    {
        var t = NewTown();
        var h = Hero(t, "fighter");
        var result = new ExpeditionResult(ExpeditionOutcome.Retreated, [], [], 0, 0, [], new()) { Bargains = new() { [h.Id] = "hollow-prince" } };
        Patrons.Home(t, result);
        Assert.Equal("hollow-prince", h.Hero!.Patron);
        Assert.Equal(t.Content.Patrons.Bargain.Favour, h.Hero.Favour);
        Assert.Equal(t.Content.Patrons.Bargain.Taint, h.Hero.Taint);
    }
}
