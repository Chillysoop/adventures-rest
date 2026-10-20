using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TableFeelTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Hero(Town t, string cls = "fighter", int level = 4) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);

    [Fact]
    public void AFriendSpeaksAtTheGraveAndSwearsAVowTheyCanKeep()
    {
        var (_, t) = NewTown();
        var dead = Hero(t);
        var friend = Hero(t);
        friend.Opinions[dead.Id] = 60;
        var site = t.Sites.First(s => !s.Cleared);
        dead.Dead = true;
        Voices.Funeral(t, [dead], [friend], site.Def.Id, site.Def.Name, true);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"buries {dead.Name}") && e.Text.Contains(friend.GivenName));
        Assert.Equal(site.Def.Id, friend.Hero!.Vow);
        Assert.Equal(dead.Name, friend.Hero.VowFor);

        int renown = friend.Hero.Renown;
        site.Cleared = true;
        Voices.Cleared(t, site.Def.Id, site.Def.Name, [friend]);
        Assert.Null(friend.Hero.Vow);
        Assert.True(friend.Hero.Renown > renown);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The vow is kept"));
    }

    [Fact]
    public void AWakeWhenThereIsNoBodyAndNoVowFromAStranger()
    {
        var (_, t) = NewTown();
        var dead = Hero(t);
        var stranger = Hero(t);
        var site = t.Sites.First(s => !s.Cleared);
        dead.Dead = true;
        Voices.Funeral(t, [dead], [stranger], site.Def.Id, site.Def.Name, false);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("There is nobody to bury") && e.Text.Contains(dead.Name));
        Assert.Null(stranger.Hero!.Vow);
    }

    [Fact]
    public void AHeroWhoKeepsFumblingEarnsANickname()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        Voices.Jokes(t, "Iron Owls", [new Gag(a.Id, "fumble")], [a]);
        Assert.Equal("", a.Hero!.Nickname);
        Voices.Jokes(t, "Iron Owls", [new Gag(a.Id, "fumble")], [a]);
        Assert.Contains(a.Hero.Nickname, t.Content.Voices.Jokes["fumble"].Names);
        Assert.Equal("fumble", a.Hero.NicknameFor);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"\"{a.Hero.Nickname}\""));
    }

    [Fact]
    public void ACasterReachingANewCircleGetsACardNamingASpell()
    {
        var (_, t) = NewTown();
        var a = Hero(t, "wizard", 2);
        Moments.Leveled(t, a, 3, a.Hero!.MaxHp);
        var m = Assert.Single(t.Moments);
        Assert.Equal(Moments.Level, m.Kind);
        Assert.Contains(m.Gains, g => g.StartsWith("Spells of the second circle: learns "));
    }

    [Fact]
    public void HeroesOnTheRoadTalkAndTheirTalesAreKept()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        var t = sim.Town;
        sim.RunDays(150);
        var told = t.Chronicles.Where(c => c.Beats.Count > 0).ToList();
        Assert.NotEmpty(told);
        Assert.All(told, c => Assert.NotEmpty(c.MemberIds));
        Assert.Contains(told.SelectMany(c => c.Beats), b => b.Kind == "setout");
        Assert.Contains(told.SelectMany(c => c.Beats), b => b.Kind == "say");
        Assert.All(told.SelectMany(c => c.Beats.Select(b => (c, b))), x => Assert.InRange(x.b.Line, 0, x.c.Lines.Count - 1));

        var heroes = t.Heroes.Where(a => !a.Dead && !a.Departed).ToList();
        Assert.Equal(heroes.Count, heroes.Select(a => a.GivenName).Distinct().Count());

        var recap = Recap.Build(t, t.Day - 30);
        Assert.Equal($"Last time in {t.Name}...", recap.Title);
        Assert.NotEmpty(recap.Moments);
        Assert.DoesNotContain(recap.Moments, m => m.Contains("(Chronicle "));
    }
}
