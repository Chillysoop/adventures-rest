using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Each kind of foe fights its own way, and bosses fight like bosses.</summary>
public class TacticsTests
{
    private static readonly Content C = Content.Default();

    [Fact]
    public void FoesTakeTheirKindsTactics()
    {
        Assert.Equal("skulkers", Tactics.Of(C, C.Monster("goblin"))?.Id);
        Assert.Equal("skulkers", Tactics.Of(C, C.Monster("goblin_boss"))?.Id);
        Assert.Equal("pack", Tactics.Of(C, C.Monster("wolf"))?.Id);
        Assert.Equal("outlaws", Tactics.Of(C, C.Monster("bandit_captain"))?.Id);
        Assert.Equal("dead", Tactics.Of(C, C.Monster("skeleton"))?.Id);
        Assert.Equal("crystal", Tactics.Of(C, C.Monster("crystalback"))?.Id);
        var dragon = C.Library.Monsters.First(m => m.Type == "dragon" && m.Damage != null);
        Assert.Equal("wyrm", Tactics.Of(C, C.Monster(dragon.Id))?.Id);
    }

    [Fact]
    public void EveryTacticsMinionsAreRealCreatures()
    {
        foreach (var t in C.Tactics.Tactics)
        {
            Assert.True(t.Words.Count > 0 || t.Types.Count > 0, $"{t.Id} picks nothing");
            if (t.Minions != "") Assert.NotNull(C.Monster(t.Minions));
            if (t.Minions != "") Assert.Contains("{n}", t.Called);
            if (t.Flee > 0) Assert.Contains("{foes}", t.Fled);
            if (t.Pride > 0) Assert.Contains("{hero}", t.Goaded);
        }
    }

    [Fact]
    public void TheNewCreaturesAreInTheLibrary()
    {
        foreach (var id in new[] { "crystalback", "hollow-sentinel", "hollow-castellan" })
        {
            var m = C.Library.Monster(id);
            Assert.NotNull(m);
            Assert.NotNull(m!.Damage);
        }
        Assert.Equal("construct", C.Monster("hollow-sentinel").Type);
    }

    [Fact]
    public void DelvesStillEndAndBossesUseTheirMoves()
    {
        // Run many delves against goblin warrens and dragon lairs: they finish, and the tactics show in the logs.
        var sim = new Simulation(new SimulationOptions { Seed = 7 });
        sim.RunDays(40);
        var heroes = sim.Town.Heroes.Take(4).ToList();
        Assert.NotEmpty(heroes);
        var lair = C.Sites.First(s => s.Boss.Monster == "goblin_boss");
        bool scattered = false, called = false;
        for (int seed = 1; seed <= 40; seed++)
        {
            var result = Expedition.Run(new Rng(seed), C, lair, heroes);
            string log = string.Join("\n", result.Lines);
            scattered |= log.Contains("scatter");
            called |= log.Contains("side tunnels");
        }
        Assert.True(scattered || called, "no goblin ever ran or called for help in 40 delves");
    }
}
