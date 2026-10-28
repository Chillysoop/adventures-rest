using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FaithTests
{
    [Fact]
    public void ThePantheonLoadsFromTheLibrary()
    {
        var lib = Content.Default().Library;
        Assert.True(lib.Deities.Count >= 12);
        Assert.Contains(lib.Deities, d => d.Evil);
        foreach (var d in lib.Deities)
        {
            Assert.NotEqual("", d.HolyDay);
            Assert.NotEqual("", d.Boon);
            foreach (string r in d.Rivals) Assert.NotNull(lib.Deity(r));
        }
        foreach (var f in lib.Factions) Assert.NotNull(lib.Deity(f.Patron));
    }

    [Fact]
    public void TheCharterShrineIsTheLanternMothers()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var shrine = Shrines.Shrine(t)!;
        Assert.Equal("lantern-mother", Faith.Of(t, shrine)!.Id);
        Assert.Equal("the shrine of the Lantern Mother", shrine.Name);
    }

    [Fact]
    public void ShortNamesReadWellOnBuildings()
    {
        var lib = Content.Default().Library;
        Assert.Equal("Haldor", Faith.Short(lib.Deity("anvil-father")!));
        Assert.Equal("Nim", Faith.Short(lib.Deity("veiled-coin")!));
        Assert.Equal("the Pale Ferryman", Faith.Short(lib.Deity("ferryman")!));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void TownsfolkKeepManyGodsAndTheirHolyDays(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(90);
        var t = sim.Town;
        // Faith is settled once a day: anyone who came to town later in today's tick chooses tomorrow.
        Assert.All(t.Residents.Where(a => a.ArrivedTick < t.Tick), a => Assert.NotNull(a.Faith));
        var kept = t.Residents.Where(a => a.Faith != "").Select(a => a.Faith).Distinct().ToList();
        Assert.True(kept.Count >= 4, $"only {kept.Count} gods kept");
        Assert.DoesNotContain(t.Residents, a => Faith.Of(t, a)?.Evil == true && !Factions.Of(t, a).Any());
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Faith && e.Text.Contains(" keep", StringComparison.Ordinal));
    }

    [Fact]
    public void ClericsAndPaladinsArriveSworn()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4 }).Town;
        var cleric = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "cleric"), 1);
        Faith.Choose(t, cleric);
        Assert.True(cleric.Devout);
        Assert.False(Faith.Of(t, cleric)!.Evil);
    }

    [Fact]
    public void FollowersRaiseShrinesToTheirOwnGods()
    {
        int towns = 0;
        foreach (int seed in new[] { 1, 2, 3, 5 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(120);
            var t = sim.Town;
            var gods = t.Faiths.Dedications.Values.Distinct().ToList();
            if (gods.Count > 1) towns++;
            // Every house of worship is dedicated, and to a god who isn't evil.
            foreach (var b in t.Buildings.Where(b => b.Def.Holy)) Assert.False(Faith.Of(t, b)!.Evil);
        }
        Assert.True(towns >= 2);
    }

    [Fact]
    public void AGodActsWhenItsFavourBrimsOverAndThenWantsMore()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        sim.RunDays(1);
        var mother = Faith.CharterGod(t);
        double first = Faith.Threshold(t, mother);
        foreach (var h in t.Agents) h.Hunger = 90;
        Faith.Act(t, mother);
        Assert.All(t.Residents, a => Assert.True(a.Hunger <= 30));
        Assert.Single(t.Faiths.Miracles);
        Assert.Equal(2 * first, Faith.Threshold(t, mother));
        Assert.Equal(0, t.Faiths.Favor.Get(mother.Id, -1));
    }

    [Fact]
    public void HeroesAreBlessedBeforeTheyMarchAndTheBlessingLiftsWhenHome()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        for (int d = 0; d < 150 && t.Faiths.Blessings.Count == 0; d++) sim.RunDays(1);
        Assert.NotEmpty(t.Faiths.Blessings);
        var b = t.Faiths.Blessings[0];
        var hero = t.Agent(b.AgentId);
        int blessed = hero.Hero!.WeaponBonus;
        var party = t.Parties[b.PartyId - 1];
        // The mayor's banner may already bless the same march; it lifts with the god's when they come home.
        int marched = t.Regalia.Boons.Where(x => x.AgentId == hero.Id && x.PartyId == party.Id).Sum(x => x.Bonus);
        while (!party.Returned) sim.RunDays(1);
        sim.RunDays(1);
        Assert.DoesNotContain(t.Faiths.Blessings, x => x.PartyId == party.Id);
        if (!hero.Dead && hero.Hero.WeaponBonus < blessed) Assert.Equal(blessed - b.Bonus - marched, hero.Hero.WeaponBonus);
    }

    [Fact]
    public void ATempleGoesToTheLargestCongregation()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        sim.RunDays(1);
        foreach (var a in t.Residents)
        {
            a.Faith = "anvil-father";
            a.Devout = true;
        }
        Assert.Equal("anvil-father", Faith.TempleChoice(t).Id);
    }
}
