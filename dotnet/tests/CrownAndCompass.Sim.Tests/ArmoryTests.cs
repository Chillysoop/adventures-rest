using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ArmoryTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Hero(Town t, string cls, int level = 3, string? company = null)
    {
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);
        a.Abilities.Str = 16;
        a.Abilities.Dex = 14;
        a.Hero!.PartyName = company;
        return a;
    }

    private static LibItem Item(Town t, string id) => t.Content.Library.Item(id)!;

    private static Party PartyOf(Town t, string name, params Agent[] members) =>
        new() { Id = t.Parties.Count + 1, Name = name, Members = members.Select(m => m.Id).ToList(), SiteId = t.Sites[0].Def.Id };

    [Fact]
    public void TheTownHallStartsWithASendingStone()
    {
        var (_, t) = NewTown();
        var stock = Assert.Single(t.Armory.Stock);
        Assert.True(Armory.IsStone(t, stock.Item));
    }

    [Fact]
    public void AGiftIsWornAndWhatItReplacesComesBackToTheArmory()
    {
        var (sim, t) = NewTown();
        var a = Hero(t, "fighter");
        Gear.Claim(t, a, Item(t, "weapon-1"));
        // A blade taken up can come cursed, and a cursed one won't make room (CursesAndDowntimeTests): not this one.
        a.Hero!.Curse = "";
        var gift = Armory.Add(t, Item(t, "weapon-2"), "a test");
        double loyalty = a.Hero!.Loyalty, pay = a.Hero.Pay;
        Assert.True(sim.Gift(gift.Id, a.Id));
        Assert.Equal("weapon-2", a.Hero.Magic["weapon"].Id);
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "weapon-1");
        Assert.DoesNotContain(t.Armory.Stock, s => s.Id == gift.Id);
        Assert.True(a.Hero.Loyalty > loyalty);
        Assert.True(a.Hero.Pay > pay);
        // The +1 back in the armory is no use to them now, and a hero away can't be given anything.
        var back = t.Armory.Stock.First(s => s.Item.Id == "weapon-1");
        Assert.False(sim.Gift(back.Id, a.Id));
        var b = Hero(t, "fighter");
        b.Hero!.AwayUntil = t.Tick + 48;
        Assert.False(sim.Gift(back.Id, b.Id));
        b.Hero.AwayUntil = null;
        Assert.Equal(b.Id, Armory.Takers(t, back.Item)[0].Hero.Id);
    }

    [Fact]
    public void ASendingStoneLetsACompanyWithNoCasterSendWord()
    {
        var (sim, t) = NewTown();
        var fighters = new[] { Hero(t, "fighter", company: "Iron Wolves"), Hero(t, "fighter", company: "Iron Wolves"), Hero(t, "barbarian", company: "Iron Wolves") };
        var party = PartyOf(t, "Iron Wolves", fighters);
        Assert.Equal(0, Knowledge.Slots(t, party));
        var stone = t.Armory.Stock.Single(s => Armory.IsStone(t, s.Item));
        // The stone goes to a company that can't send word already.
        Assert.Contains(Armory.Takers(t, stone.Item), x => x.Hero.Id == fighters[2].Id);
        Assert.True(sim.Gift(stone.Id, fighters[2].Id));
        Assert.True(fighters[2].Hero!.Stone);
        Assert.Equal(t.Content.Armory.StoneSlots, Knowledge.Slots(t, party));
        Assert.Equal(1, Armory.StonesOut(t));
        // A second stone is no use to the same hero; a dead carrier takes theirs to the grave.
        Assert.Equal(0, Armory.Use(t, fighters[2], stone.Item));
        fighters[2].Dead = true;
        Assert.Equal(0, Knowledge.Slots(t, party));
    }

    [Fact]
    public void LootTermsSendTheHoardsMagicToTheArmory()
    {
        var (_, t) = NewTown();
        var site = t.Sites.First(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared);
        t.Treasury = 1000;
        var bounty = Quests.PostBounty(t, site.Def.Id, 100, lootToTown: true)!;
        Assert.True(bounty.LootToTown);
        var a = Hero(t, "fighter", company: "Iron Wolves");
        var party = PartyOf(t, "Iron Wolves", a);
        Assert.True(Armory.HandIn(t, party, bounty, a, Item(t, "weapon-1")));
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "weapon-1");
        Assert.Empty(a.Hero!.Magic);
        // Without terms or a pact the finder keeps it.
        Assert.False(Armory.HandIn(t, party, null, a, Item(t, "armor-1")));
        Assert.True(Armory.TermsCut(t, a, bounty) > 0);
    }

    [Fact]
    public void AFriendlyCompanySwearsALootPactAndAWaryOneRefusesAndSulks()
    {
        var (sim, t) = NewTown();
        var friend = Hero(t, "fighter", company: "Iron Wolves");
        friend.Hero!.Loyalty = 80;
        var wary = Hero(t, "rogue", company: "Grey Hands");
        wary.Hero!.Loyalty = 20;

        Assert.Null(Armory.PactRefusal(t, "Iron Wolves"));
        Assert.True(sim.OfferPact("Iron Wolves"));
        var c = Intel.Find(t, "Iron Wolves")!;
        Assert.True(c.Pact);
        Assert.True(c.PactFee < t.Content.Armory.Pact.Fee);

        t.Treasury = 1000;
        var item = Item(t, "weapon-1");
        double fee = Math.Round(Gear.SellValue(t, item) * c.PactFee), gold = friend.Gold;
        Assert.True(Armory.HandIn(t, PartyOf(t, "Iron Wolves", friend), null, friend, item));
        Assert.Equal(gold + fee, friend.Gold);
        Assert.Equal(1, c.Handed);

        Assert.NotNull(Armory.PactRefusal(t, "Grey Hands"));
        Assert.False(sim.OfferPact("Grey Hands"));
        var g = Intel.Find(t, "Grey Hands")!;
        Assert.False(g.Pact);
        Assert.True(g.Dealings < 0);
        Assert.Equal("still sore about the last offer", Armory.PactRefusal(t, "Grey Hands"));

        Assert.True(sim.EndPact("Iron Wolves"));
        Assert.False(c.Pact);
    }

    [Fact]
    public void TheTownBuysWhatAHeroOffersAndTheyArePaid()
    {
        var (sim, t) = NewTown();
        var a = Hero(t, "fighter");
        Gear.Claim(t, a, Item(t, "weapon-2"));
        Gear.Claim(t, a, Item(t, "weapon-1"));
        var offer = Assert.Single(t.Armory.Offers);
        t.Treasury = 1000;
        double gold = a.Gold;
        Assert.True(sim.BuyLoot(offer.Id));
        Assert.Equal(gold + offer.Price, a.Gold);
        Assert.Equal(1000 - offer.Price, t.Treasury);
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "weapon-1");
        // Selling it on gets the merchants' price.
        var s = t.Armory.Stock.First(x => x.Item.Id == "weapon-1");
        Assert.True(sim.SellStock(s.Id));
        Assert.Equal(1000 - offer.Price + Gear.SellValue(t, s.Item), t.Treasury);
    }

    [Fact]
    public void TheStandInMayorKeepsTheArmoryAndCampaignsStillRunOn()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(300);
        var t = sim.Town;
        Assert.True(t.Armory.Gifts > 0, "the stand-in gave nothing");
        Assert.True(Armory.StonesOut(t) > 0 || t.Heroes.Count == 0);
    }

    [Fact]
    public void TheArmoryAndPactsSurviveASaveAndLoad()
    {
        var (sim, t) = NewTown();
        var a = Hero(t, "fighter", company: "Iron Wolves");
        a.Hero!.Loyalty = 80;
        sim.OfferPact("Iron Wolves");
        Armory.Add(t, Item(t, "weapon-1"), "a test");
        var copy = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(t.Armory.Stock.Select(s => s.Item.Id), copy.Town.Armory.Stock.Select(s => s.Item.Id));
        Assert.True(Intel.Find(copy.Town, "Iron Wolves")!.Pact);
        Assert.Equal(t.Armory.NextId, copy.Town.Armory.NextId);
    }
}
