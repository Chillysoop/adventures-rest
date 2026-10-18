using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GearTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Hero(Town t, string cls, int level = 3)
    {
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);
        a.Abilities.Str = 16;
        a.Abilities.Dex = 14;
        return a;
    }

    [Fact]
    public void HeroesArriveWithTheirClassKitFromTheLibrary()
    {
        var (_, t) = NewTown();
        var fighter = Hero(t, "fighter");
        Assert.Equal("longsword", fighter.Hero!.Weapon!.Id);
        Assert.Equal("chain-mail", fighter.Hero.Armor!.Id);
        Assert.Equal("shield", fighter.Hero.Shield!.Id);
        // Chain mail is heavy: no Dexterity. 16 + 2 for the shield.
        Assert.Equal(18, Gear.Ac(t.Content, fighter));
        Assert.Equal("longsword, chain mail, shield", Gear.Describe(fighter.Hero));

        var ranger = Hero(t, "ranger");
        Assert.Null(ranger.Hero!.Shield);
        Assert.True(ranger.Hero.Weapon!.TwoHanded);
        // Leather is light: 11 + all of Dex.
        Assert.Equal(13, Gear.Ac(t.Content, ranger));

        var monk = Hero(t, "monk");
        monk.Abilities.Wis = 16;
        Assert.Null(monk.Hero!.Armor);
        Assert.Equal(15, Gear.Ac(t.Content, monk));
    }

    [Fact]
    public void MagicGearAddsToArmourAttackAndDamage()
    {
        var (_, t) = NewTown();
        var a = Hero(t, "fighter");
        int ac = Gear.Ac(t.Content, a);
        int atk = Gear.Attack(t.Content, a);
        Gear.Claim(t, a, t.Content.Library.Item("armor-1")!);
        Gear.Claim(t, a, t.Content.Library.Item("weapon-2")!);
        Assert.Equal(ac + 1, Gear.Ac(t.Content, a));
        Assert.Equal(atk + 2, Gear.Attack(t.Content, a));
        Assert.Equal(["+2 longsword", "+1 chain mail"], a.Hero!.Items);
        Assert.Equal(3, Gear.Edge(t.Content, a));
    }

    [Fact]
    public void BetterLootReplacesWhatTheHeroWearsAndTheRestIsSold()
    {
        var (_, t) = NewTown();
        var a = Hero(t, "fighter");
        Gear.Claim(t, a, t.Content.Library.Item("weapon-2")!);
        double gold = a.Gold;
        // A +1 weapon is no use to someone with a +2: it goes to a merchant.
        Gear.Claim(t, a, t.Content.Library.Item("weapon-1")!);
        Assert.Equal("weapon-2", a.Hero!.Magic["weapon"].Id);
        Assert.True(a.Gold > gold);
        // A wizard can't wear armour magic and has no shield arm.
        var wizard = Hero(t, "wizard");
        Assert.True(Gear.Gain(t.Content, wizard, t.Content.Library.Item("armor-1")!) <= 0);
        Assert.True(Gear.Gain(t.Content, wizard, t.Content.Library.Item("shield-1")!) <= 0);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("sells the Weapon +1 to a passing merchant"));
    }

    [Fact]
    public void HeroesBuyUpgradesAtTheForgeAndTheTownTakesItsTax()
    {
        var (_, t) = NewTown();
        var forgeDef = t.Content.Building("forge");
        var forge = t.AddBuilding(forgeDef, Land.Find(t, forgeDef, null)!, null, "the Forge", true);
        var a = Hero(t, "fighter");
        a.Gold = 500;
        double treasury = t.Treasury;
        Gear.Shop(t);
        // Chain mail (16) to splint (17) is the only upgrade a small town's forge makes.
        Assert.Equal("splint-armor", a.Hero!.Armor!.Id);
        Assert.Equal(300, a.Gold);
        Assert.True(t.Treasury > treasury);
        Assert.True(forge.Cash > 0);
        // A bigger town's forge enchants: +1 armour, shield and longsword, one a day.
        for (int i = 0; i < 60; i++) Population.CreateAgent(t);
        Assert.Contains(Gear.Enchanted(t), it => it.Id == "weapon-1");
        for (int day = 0; day < 3; day++)
        {
            a.Gold = 400;
            Gear.Shop(t);
        }
        Assert.Equal(["+1 longsword", "+1 splint armor", "+1 shield"], a.Hero.Items);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("buys a +1 longsword at the Forge for 300 gold"));
    }

    [Fact]
    public void WithoutAForgeTheStoreSellsLightArmourDearer()
    {
        var (_, t) = NewTown();
        var rogue = Hero(t, "rogue");
        var fighter = Hero(t, "fighter");
        rogue.Gold = fighter.Gold = 200;
        Gear.Shop(t);
        // Studded leather is 45 at the forge; the store brings it in for half as much again.
        Assert.Equal("studded-leather-armor", rogue.Hero!.Armor!.Id);
        Assert.Equal(200 - 68, rogue.Gold);
        Assert.Equal("chain-mail", fighter.Hero!.Armor!.Id);
    }

    [Fact]
    public void GearSurvivesASaveAndLoad()
    {
        var (sim, t) = NewTown();
        var a = Hero(t, "fighter");
        Gear.Claim(t, a, t.Content.Library.Item("weapon-1")!);
        var back = SaveGame.Read(SaveGame.Write(sim));
        var b = back.Town.Agent(a.Id).Hero!;
        Assert.Same(a.Hero!.Weapon, b.Weapon);
        Assert.Same(a.Hero.Magic["weapon"], b.Magic["weapon"]);
        Assert.Equal(a.Hero.Items, b.Items);
    }
}
