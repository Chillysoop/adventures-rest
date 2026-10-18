using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GiftsTests
{
    [Fact]
    public void ATaintedGiftFallsDueOnlyAfterItsDays()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(70);
        var gift = Stories.Open(t, "gift-contract");
        Assert.NotNull(gift);
        int sign = Stories.StageOf(t, gift!).Options.FindIndex(o => o.Success.Effects.ContainsKey("precedent:signed-clause"));
        double gold = t.Treasury;
        Stories.Resolve(t, gift!.Id, sign);
        Assert.True(t.Treasury > gold);
        Assert.Null(Stories.Open(t, "gift-contract-due"));
        sim.RunDays(t.Content.Stories.Precedent("signed-clause")!.After + 1);
        foreach (var o in t.Stories.Open.ToList()) t.Stories.All.Remove(o);
        var due = Stories.Open(t, "gift-contract-due");
        Assert.NotNull(due);
        int buy = Stories.StageOf(t, due!).Options.FindIndex(o => o.Cost > 0);
        t.Treasury += 1000;
        Stories.Resolve(t, due!.Id, buy);
        Assert.False(t.Stories.Precedents.ContainsKey("signed-clause"));
    }

    [Fact]
    public void ADarkGiftMakesAHeroStrongerAndLikelierToFall()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(80);
        Story? gift = null;
        for (int d = 0; d < 400 && (gift = Stories.Open(t, "dark-gift")) == null; d++) sim.RunDays(1);
        Assert.NotNull(gift);
        var hero = t.Agent(gift!.HeroId!.Value);
        int level = hero.Hero!.Level, xp = hero.Hero.Xp;
        int take = Stories.StageOf(t, gift).Options.FindIndex(o => o.Success.Effects.ContainsKey("taint") && o.Skill == "");
        Stories.Resolve(t, gift.Id, take);
        Assert.Equal(1, hero.Hero.Taint);
        Assert.True(hero.Hero.Level > level || hero.Hero.Xp > xp);

        var again = Stories.Open(t, "dark-gift");
        if (again != null)
        {
            int forbid = Stories.StageOf(t, again).Options.FindIndex(o => o.Success.Effects.ContainsKey("precedent:gifts-forbidden"));
            Stories.Resolve(t, again.Id, forbid);
            Assert.Null(Stories.Open(t, "dark-gift"));
        }
    }
}
