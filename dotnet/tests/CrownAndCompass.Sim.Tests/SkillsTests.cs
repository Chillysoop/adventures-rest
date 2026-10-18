using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class SkillsTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Folk(Town t, string profession)
    {
        var a = Population.CreateAgent(t, new NewAgentOptions { Profession = profession });
        a.Abilities = new Abilities();
        return a;
    }

    [Fact]
    public void EverySkillNamedInContentExists()
    {
        var (_, t) = NewTown();
        var r = t.Content.Skills;
        var ids = Skills.All(t).Select(s => s.Id).ToHashSet();
        Assert.Equal(18, ids.Count);
        var named = r.Trades.Values.SelectMany(x => x)
            .Concat(r.Classes.Values.SelectMany(c => c.Skills.Concat(c.Expertise)))
            .Concat(r.Backgrounds.Values.SelectMany(x => x))
            .Concat(r.Watchman.Keys)
            .Concat(r.Checks.Values.Select(c => c.Skill).Where(s => s != ""));
        Assert.All(named, s => Assert.Contains(s, ids));
        Assert.All(t.Content.Classes, c => Assert.True(r.Classes.ContainsKey(c.Id), $"{c.Id} has no skills"));
        Assert.All(r.Classes, kv => Assert.All(kv.Value.Expertise, s => Assert.Contains(s, kv.Value.Skills)));
    }

    [Fact]
    public void TownsfolkAreTrainedAtTheirTradeAndGrowWithPractice()
    {
        var (_, t) = NewTown();
        var smith = Folk(t, "smith");
        Assert.Equal(2, Skills.Bonus(t, smith, "athletics"));
        Assert.True(Skills.Trained(t, smith, "athletics"));
        Assert.Equal(0, Skills.Bonus(t, smith, "arcana") - (Skills.Knack(t, smith) == "arcana" ? 2 : 0));
        smith.Practice = t.Content.Skills.Townsfolk.PracticeStep * 9;
        Assert.Equal(2 + t.Content.Skills.Townsfolk.PracticeMax, Skills.Bonus(t, smith, "athletics"));
    }

    [Fact]
    public void HeroesOutclassTownsfolkAtMostSkills()
    {
        var (_, t) = NewTown();
        foreach (var cls in t.Content.Classes)
        {
            var hero = Quests.CreateHero(t, cls, 1);
            hero.Abilities = new Abilities();
            foreach (var trade in t.Content.Skills.Trades.Keys)
            {
                var folk = Folk(t, trade);
                int better = Skills.All(t).Count(s => Skills.Bonus(t, hero, s.Id) > Skills.Bonus(t, folk, s.Id));
                int worse = Skills.All(t).Count(s => Skills.Bonus(t, hero, s.Id) < Skills.Bonus(t, folk, s.Id));
                Assert.True(better > worse && better >= 9, $"a level 1 {cls.Id} only beats a {trade} at {better} skills");
            }
        }
    }

    [Fact]
    public void ARogueIsExpertAtStealth()
    {
        var (_, t) = NewTown();
        var rogue = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "rogue"), 5);
        rogue.Abilities = new Abilities();
        Assert.Equal(6, Skills.Bonus(t, rogue, "stealth"));
        Assert.Equal(1, Skills.Bonus(t, rogue, "arcana") - (Skills.HeroTrained(t, rogue.Hero!).Contains("arcana") ? 2 : 0));
    }

    [Fact]
    public void ChecksAreKeptForTheDiceAndRingOver()
    {
        var (_, t) = NewTown();
        var a = Folk(t, "player");
        for (int i = 0; i < t.Content.Skills.Ring + 10; i++) Skills.Roll(t, a, "performance", 12, "perform");
        Assert.Equal(t.Content.Skills.Ring, t.Checks.Count);
        Assert.Equal(t.CheckSerial, t.Checks[^1].Serial);
        Assert.All(t.Checks, c => Assert.Equal(c.Die == 20 || (c.Die != 1 && c.Total >= 12), c.Success));
        Assert.Equal(a.Id, Skills.Recent(t, a.Id).First().AgentId);
    }

    [Fact]
    public void ACampaignRollsEverydayChecks()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var kinds = new HashSet<string>();
        for (int h = 0; h < 40 * SimTime.HoursPerDay; h++)
        {
            int before = sim.Town.CheckSerial;
            sim.Step();
            foreach (var c in sim.Town.Checks.Where(c => c.Serial > before)) kinds.Add(c.Kind);
        }
        Assert.Contains("talk", kinds);
        Assert.Contains("craft", kinds);
        Assert.Contains("haggle", kinds);
        Assert.Contains("perform", kinds);
    }
}
