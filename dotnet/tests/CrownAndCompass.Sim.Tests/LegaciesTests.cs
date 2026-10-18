using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LegaciesTests
{
    private static Simulation Player(int seed = 2) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = false, AutoStories = false, FreeCompanies = false });

    /// <summary>A seasoned hero in town, wearing a magic item, who then falls.</summary>
    private static Agent Fall(Simulation sim, int level = 6)
    {
        var t = sim.Town;
        var hero = Quests.CreateHero(t, t.Content.Classes[0], level);
        var item = t.Content.Library.Items.Where(i => Gear.SlotOf(i) == "trinket").OrderBy(i => i.Tier).ThenBy(i => i.Name, StringComparer.Ordinal).First();
        hero.Hero!.Magic["trinket"] = item;
        hero.Dead = true;
        Population.Depart(t, hero);
        return hero;
    }

    [Fact]
    public void AFallenHeroEntersTheHallAndTheirHeirTakesUpTheirArms()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var fallen = Fall(sim);
        string relic = fallen.Hero!.Magic["trinket"].Name;
        sim.RunDays(1);
        var l = Assert.Single(t.Legacies);
        Assert.Equal(fallen.Id, l.HeroId);
        // Whether kin come is a roll; send word now.
        l.HeirDay = t.Day + 2;
        sim.RunDays(3);
        Assert.NotNull(l.HeirId);
        var heir = t.Agent(l.HeirId!.Value);
        Assert.Equal(fallen.FamilyName, heir.FamilyName);
        Assert.Equal(fallen.Hero.Cls.Id, heir.Hero!.Cls.Id);
        Assert.Equal(2, heir.Hero.Level);
        Assert.Equal(relic, l.Relic);
        Assert.Contains(heir.Hero.Magic.Values, i => i.Name == relic);
        Assert.DoesNotContain(fallen.Hero.Magic.Values, i => i.Name == relic);
        Assert.Contains(fallen.Name, heir.Hero.Past);
    }

    [Fact]
    public void AMemorialCostsGoldAndSteadiesEveryHero()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var fallen = Fall(sim);
        sim.RunDays(1);
        var hero = Quests.CreateHero(t, t.Content.Classes[0], 2);
        double before = Loyalty.Target(t, hero);
        t.Treasury += 1000;
        double treasury = t.Treasury, cost = Legacies.MemorialCost(t, Legacies.Of(t, fallen.Id)!);
        Assert.True(sim.RaiseMemorial(fallen.Id));
        Assert.False(sim.RaiseMemorial(fallen.Id));
        Assert.Equal(treasury - cost, t.Treasury, 6);
        Assert.True(Loyalty.Target(t, hero) > before);
        Assert.Contains(Loyalty.Factors(t, hero), f => f.Why == "memorials to the fallen");
    }

    [Fact]
    public void ARetiredVeteranTrainsTheHeroesWhoComeAfter()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var vet = Quests.CreateHero(t, t.Content.Classes[0], 9);
        vet.Hero!.Expeditions = 10;
        Assert.True(Legacies.Veteran(t, vet));
        var green = Quests.CreateHero(t, t.Content.Classes[0], 1);
        Assert.Equal(0, green.Hero!.Xp);

        var s = Stories.Open(t, "old-blade");
        Assert.NotNull(s);
        s!.HeroId = vet.Id;
        Stories.Resolve(t, s.Id, 0);
        Assert.True(vet.Departed);
        Assert.False(vet.Dead);
        Assert.True(Legacies.Of(t, vet.Id)!.Retired);
        Assert.DoesNotContain(vet, t.Heroes);

        var trained = Quests.CreateHero(t, t.Content.Classes[0], 1);
        Assert.Equal(Legacies.MentorXp(t, 1), trained.Hero!.Xp);
        Assert.True(trained.Hero.Xp > 0);
    }

    [Fact]
    public void AHeroRaisedFromTheDeadLeavesTheHall()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var fallen = Fall(sim);
        sim.RunDays(1);
        Assert.NotNull(Legacies.Of(t, fallen.Id));
        fallen.Dead = false;
        fallen.Departed = false;
        sim.RunDays(1);
        Assert.Null(Legacies.Of(t, fallen.Id));
    }

    [Fact]
    public void AFuneralStoryCanRaiseTheMemorial()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var fallen = Fall(sim);
        sim.RunDays(1);
        t.Treasury += 500;
        var s = Stories.Open(t, "hero-funeral");
        Assert.NotNull(s);
        Assert.Equal(fallen.Id, s!.FallenId);
        Assert.Contains(fallen.Name, Stories.Fill(t, s, Stories.StageOf(t, s).Text));
        Stories.Resolve(t, s.Id, 0);
        Assert.True(Legacies.Of(t, fallen.Id)!.Memorial);
    }

    [Fact]
    public void TheStandInHonoursTheFallenOverACampaign()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(500);
        var t = sim.Town;
        Assert.NotEmpty(t.Legacies);
        Assert.Contains(t.Legacies, l => l.Memorial);
        Assert.True(t.Accounts.Get(TreasuryAccount.Legacies, 0) < 0);
    }
}
