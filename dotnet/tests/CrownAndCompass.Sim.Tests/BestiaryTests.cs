using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Every creature has its own body, and moves and strikes as its stat block says.</summary>
public class BestiaryTests
{
    private static readonly Content C = Content.Default();

    private static readonly string[] Plans =
        ["humanoid", "golem", "elemental", "quadruped", "ape", "theropod", "bird", "dragon", "serpent", "worm", "insect", "spider", "crab", "ooze", "wraith", "floater", "fish", "cephalopod", "plant", "taur", "swarm", "object"];

    [Fact]
    public void EveryCreatureLooksLikeNoOther()
    {
        var all = Bestiary.All(C).Select(m => Bestiary.Of(C, m.Id)).ToList();
        if (Environment.GetEnvironmentVariable("BESTIARY_DUMP") is string path)
            File.WriteAllLines(path, all.Select(a => $"{a.Id} | {a.Signature} | {a.Roam}"));
        var twins = all.GroupBy(a => a.Shape).Where(g => g.Count() > 1).Select(g => string.Join(" = ", g.Select(a => a.Id))).ToList();
        Assert.True(twins.Count == 0, "Look alike: " + string.Join("; ", twins));
    }

    [Fact]
    public void EveryCreatureHasAKnownPlanGaitAndStrike()
    {
        foreach (var m in Bestiary.All(C))
        {
            var a = Bestiary.Of(C, m.Id);
            Assert.Contains(a.Plan, Plans);
            Assert.NotEqual("", a.Gait);
            Assert.NotEqual("", a.Strike);
            Assert.NotEqual("", a.Roam);
            Assert.Matches("^[0-9a-f]{6}$", a.Hide);
        }
    }

    [Fact]
    public void KinShareAPlanButNotALook()
    {
        var wolf = Bestiary.Of(C, "wolf");
        var dire = Bestiary.Of(C, "dire-wolf");
        Assert.Equal(wolf.Plan, dire.Plan);
        Assert.Equal("wolf", dire.Head);
        Assert.True(dire.Scale > wolf.Scale);
        Assert.NotEqual(wolf.Signature, dire.Signature);
        Assert.Equal("dragon", Bestiary.Of(C, "adult-red-dragon").Plan);
        Assert.Equal("fly", Bestiary.Of(C, "adult-red-dragon").Gait);
        Assert.Equal("ooze", Bestiary.Of(C, "gelatinous-cube").Plan);
        Assert.Equal("pack", Bestiary.Of(C, "wolf").Roam);
        // Game ids and bosses read through to their library creature.
        Assert.Equal(Bestiary.Of(C, "goblin-warrior").Signature, Bestiary.Of(C, "goblin").Signature);
        Assert.Equal(Bestiary.Of(C, "wolf").Signature, Bestiary.Of(C, "wolf@boss").Signature);
    }

    [Fact]
    public void EveryFoeHasCountryToBeMetIn()
    {
        // The country the world's regions are made of; a creature the books give no home gets one by its type.
        var country = C.World.Themes.SelectMany(t => t.Habitats.Concat(t.Sites.SelectMany(k => k.Habitats))).ToHashSet();
        var homeless = C.Library.Monsters
            .Where(m => m.Damage != null && m.Cr >= 0.125 && !m.Alignment.Contains("good") && !m.Habitats.Any(country.Contains))
            .Select(m => m.Id)
            .ToList();
        Assert.True(homeless.Count == 0, "Nowhere to meet: " + string.Join(", ", homeless));
        Assert.All(C.Library.Monsters, m => Assert.NotEmpty(m.Habitats));
    }
}
