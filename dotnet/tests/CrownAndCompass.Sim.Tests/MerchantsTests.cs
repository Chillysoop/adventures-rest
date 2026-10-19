using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class MerchantsTests
{
    private static LibItem Item(Town t) => t.Content.Library.Item("weapon-1")!;

    private static Building Shop(Town t) =>
        t.Buildings.First(b => t.Content.Merchants.Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale && b.OwnerId != null);

    [Fact]
    public void AShopBuysLootWithItsOwnPurseAndSellsItOn()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(80);
        var t = sim.Town;
        var shop = Shop(t);
        var owner = t.Agent(shop.OwnerId!.Value);
        var seller = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        seller.Hero!.Temper = "lawful";
        foreach (var b in t.Buildings) if (b != shop && t.Content.Merchants.Shops.Contains(b.Def.Id)) b.Closed = true;
        t.Wares.Clear();

        // A shop whose keeper has no savings can't buy.
        owner.Gold = 0;
        shop.Cash = 0;
        Assert.False(Merchants.Sell(t, seller, Item(t)));

        owner.Gold = 1000;
        double gold = seller.Gold;
        Assert.True(Merchants.Sell(t, seller, Item(t)));
        Assert.True(seller.Gold > gold);
        Assert.True(owner.Gold < 1000);
        Assert.Single(t.Wares);

        // A friend of the shopkeeper pays less for it.
        var friend = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        var stranger = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        owner.ChangeOpinion(friend.Id, 80);
        double toFriend = Merchants.Asking(t, shop, friend, Item(t));
        double toStranger = Merchants.Asking(t, shop, stranger, Item(t));
        Assert.True(toFriend < toStranger);

        // Another hero buys it off the shelf, and the town takes its tax.
        stranger.Gold = 5000;
        double tax = t.Accounts.Get(TreasuryAccount.SalesTax, 0);
        Gear.Shop(t);
        Assert.Empty(t.Wares);
        Assert.True(t.Accounts.Get(TreasuryAccount.SalesTax, 0) > tax);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("second-hand"));
    }

    [Fact]
    public void AGreedyHeroSellsToTheThievesFence()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        var guild = Factions.Find(t, t.Content.Merchants.Fence.Faction)!;
        for (int d = 0; d < 300 && !(guild.Active && guild.Members.Count > 0); d++) sim.RunDays(1);
        Assert.True(guild.Active);
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "rogue"), 3);
        hero.Hero!.Temper = "greedy";
        double heat = guild.Heat, coffers = Factions.Coffers(t, guild);
        Assert.True(Merchants.Sell(t, hero, Item(t)));
        Assert.True(guild.Heat > heat || guild.Known);
        Assert.True(Factions.Coffers(t, guild) > coffers);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("to a fence"));
    }
    [Fact]
    public void APricelessRelicFencedMintsNoFortune()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        var guild = Factions.Find(t, t.Content.Merchants.Fence.Faction)!;
        for (int d = 0; d < 300 && !(guild.Active && guild.Members.Count > 0); d++) sim.RunDays(1);
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "rogue"), 3);
        hero.Hero!.Temper = "greedy";
        var relic = t.Content.Library.Items.OrderByDescending(i => i.ValueGp).First();
        double gold = hero.Gold, coffers = Factions.Coffers(t, guild);
        Assert.True(Merchants.Sell(t, hero, relic));
        double paid = hero.Gold - gold, cut = Factions.Coffers(t, guild) - coffers;
        Assert.True(paid < relic.ValueGp * 0.1);
        Assert.True(cut <= paid, $"the fence kept {cut} for an item it paid {paid} for");
    }
}
