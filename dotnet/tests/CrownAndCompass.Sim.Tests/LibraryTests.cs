using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LibraryTests
{
    private static readonly Content C = Content.Default();

    [Fact]
    public void TheWholeLibraryLoads()
    {
        var lib = C.Library;
        Assert.True(lib.Monsters.Count > 450);
        Assert.True(lib.Classes.Count >= 16);
        Assert.True(lib.Spells.Count > 600);
        Assert.True(lib.Items.Count > 300);
        Assert.Equal(lib.Monsters.Select(m => m.Id).OrderBy(i => i, StringComparer.Ordinal), lib.Monsters.Select(m => m.Id));
    }

    [Fact]
    public void GameMonstersTakeTheirStatsFromTheLibrary()
    {
        var goblin = C.Monster("goblin");
        Assert.Equal("goblin", goblin.Name);
        Assert.Equal(15, goblin.Ac);
        Assert.Equal(10, goblin.Hp);
        Assert.Equal(new Dice(1, 6, 2), goblin.Dice);
        Assert.Equal(2, C.Monster("ghoul").Attacks);
        Assert.True(C.Monster("skeleton").Undead);
        Assert.Equal("wolves", C.Monster("wolf").Plural);
        // Overrides in content/game win: the Last Stand's warlord is scaled down from the library's.
        var grukk = C.Monster("orc_warlord");
        Assert.Equal(75, grukk.Hp);
        Assert.Equal(17, grukk.Ac);
        Assert.True(grukk.Named);
    }

    [Fact]
    public void ClassesGetTheirSpellsFromTheLibrary()
    {
        var cleric = C.Classes.First(c => c.Id == "cleric");
        Assert.Equal("cure-wounds", cleric.HealSpell?.Id);
        Assert.Equal("wis", cleric.Casting);
        var wizard = C.Classes.First(c => c.Id == "wizard");
        Assert.Equal("burning-hands", wizard.AreaSpell?.Id);
        Assert.Equal(6, wizard.HitDie);
        Assert.False(C.Classes.First(c => c.Id == "fighter").Healer);
        Assert.Equal("scout", C.Classes.First(c => c.Id == "cartographer").Role);
        Assert.Equal(0, C.Classes.First(c => c.Id == "paladin").HealsAt(1));
    }

    [Fact]
    public void WanderersComeFromTheSitesHabitats()
    {
        var wood = C.Sites.First(s => s.Id == "wolfwood");
        var pool = C.Wanderers(wood, [0.125, 0.5]);
        Assert.NotEmpty(pool);
        Assert.All(pool, m => Assert.True(m.Xp <= 100 && m.Hp > 0 && m.Attack > 0));
    }

    [Fact]
    public void PluralsReadAsEnglish()
    {
        Assert.Equal("swarms of bats", Library.Plural("swarm of bats"));
        Assert.Equal("dire wolves", Library.Plural("dire wolf"));
        Assert.Equal("giant flies", Library.Plural("giant fly"));
        Assert.Equal("lizardfolk", Library.Plural("lizardfolk"));
    }

    [Fact]
    public void LootComesFromTheLibrary()
    {
        var names = new HashSet<string>();
        for (int seed = 1; seed <= 6; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(120);
            foreach (var h in sim.Town.Agents.Where(a => a.Hero != null)) names.UnionWith(h.Hero!.Items);
        }
        Assert.NotEmpty(names);
        Assert.All(names, n => Assert.Contains(C.Library.Items, i => i.Name == n));
    }
}
