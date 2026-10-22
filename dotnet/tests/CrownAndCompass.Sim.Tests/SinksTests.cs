using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>What a rich town can spend on: re-arming its regiments, buying out businesses, charters and oaths, great airships.</summary>
public class SinksTests
{
    private static Simulation Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, Enemy = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 200000;
        return sim;
    }

    private static Building Add(Town t, string id)
    {
        var d = t.Content.Building(id);
        var b = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false);
        b.BuildProgress = d.BuildHours;
        return b;
    }

    [Fact]
    public void EveryGradeOfArmsIsWholeAndStronger()
    {
        var a = Content.Default().Army.Arms;
        Assert.Equal(3, a.Grades.Count);
        Assert.NotEmpty(a.Smiths);
        for (int i = 0; i < a.Grades.Count; i++)
        {
            Assert.NotEqual("", a.Grades[i].Name);
            if (i > 0) Assert.True(a.Grades[i].Strength > a.Grades[i - 1].Strength && a.Grades[i].Kit > a.Grades[i - 1].Kit);
        }
    }

    [Fact]
    public void TheSmithsReArmARegimentSoldierBySoldierAndItGrowsStronger()
    {
        var sim = Town();
        var t = sim.Town;
        Add(t, "drill_yard");
        var r = sim.RaiseRegiment("swordsmen", 10)!;
        Army.Fill(t, r);
        r.Drill = 1;
        var forges = t.Buildings.Where(b => t.Content.Army.Arms.Smiths.Contains(b.Def.Id)).ToList();
        foreach (var f in forges) f.Closed = true;
        Assert.Equal("no smith in town is at a forge", Army.RefitBlocked(t, r));
        foreach (var f in forges) f.Closed = false;
        if (Army.Smiths(t) == 0)
        {
            var forge = Add(t, "forge");
            foreach (var a in t.Workers.Where(a => a.JobId == null && a.RegimentId == null).Take(2))
            {
                a.JobId = forge.Id;
                forge.Workers.Add(a.Id);
            }
        }
        double before = Army.Strength(t, r), gold = t.Treasury;
        Assert.Null(Army.RefitBlocked(t, r));
        Assert.True(sim.RefitRegiment(r.Id));
        Assert.False(sim.RefitRegiment(r.Id));
        for (int d = 0; d < 20 && r.Refitting; d++) sim.RunDays(1);
        Assert.False(r.Refitting);
        Assert.Equal(1, r.Arms);
        Assert.Equal("Steel", Army.Grade(t, r.Arms)!.Name);
        Assert.True(t.Treasury < gold);
        Assert.True(Army.Strength(t, r) > before * 1.1, $"{Army.Strength(t, r)} vs {before}");
        Assert.Equal("Masterwork", Army.NextGrade(t, r)!.Name);
    }

    [Fact]
    public void ACouncilTheOwnerTrustsBuysTheirBusinessAndTakesItsProfits()
    {
        var sim = Town();
        var t = sim.Town;
        var b = t.Buildings.Where(x => x.Complete && x.OwnerId != null && x.Def.IsBusiness && !x.Def.Civic && !x.ForSale).OrderBy(x => x.Id).First();
        var owner = t.Agent(b.OwnerId!.Value);
        b.DaysInDebt = 0;
        t.Mandate = 10;
        Assert.Contains(owner.Has(Trait.Ambitious) ? "won't sell" : "trust", Property.BuyBlocked(t, b));
        Assert.False(sim.BuyBuilding(b.Id));
        t.Mandate = 90;
        double price = Property.BuyPrice(t, b), purse = owner.Gold;
        Assert.True(price > b.Def.Cost);
        Assert.True(sim.BuyBuilding(b.Id));
        Assert.Null(b.OwnerId);
        Assert.Equal(purse + price, owner.Gold, 3);
        Assert.Equal("the town owns it already", Property.BuyBlocked(t, b));
    }

    [Fact]
    public void APeopleThatThinksWellOfTheTownSellsItsCharterAndCountsSworn()
    {
        var sim = Town();
        var t = sim.Town;
        var s = t.Settlements.OrderBy(x => x.Id).First();
        s.Met = true;
        s.Standing = 10;
        Assert.Contains("think so little", Conquest.BuyBlocked(t, s));
        s.Standing = 60;
        double price = Conquest.CharterPrice(t, s), gold = t.Treasury;
        Assert.True(price >= t.Content.Army.Conquest.Charter.Base);
        Assert.True(sim.BuySettlement(s.Id));
        var o = Conquest.Held(t, s)!;
        Assert.True(o.Bought);
        Assert.Equal(gold - price, t.Treasury, 3);
        sim.RunDays(20);
        Assert.Equal(0, o.Resistance);
        Assert.Contains(t.Army.Held, x => x.SettlementId == s.Id);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Army, 0) > 0);
        Assert.Equal("the town holds it already", Conquest.BuyBlocked(t, s));
    }

    [Fact]
    public void AnAllysOathIsBoughtForGoodNoTitheAndNoEnemyCanTurnIt()
    {
        var sim = Town();
        var t = sim.Town;
        var p = t.Powers.OrderBy(x => x.Id, StringComparer.Ordinal).First();
        Powers.Introduce(t, p, "test");
        Assert.Equal("it must be an ally first", Powers.SwearBlocked(t, p.Id));
        p.Side = PowerSide.Town;
        p.Standing = 80;
        Assert.True(sim.SwearPower(p.Id));
        Assert.True(p.Sworn);
        p.Standing = -50;
        t.Treasury = 0;
        sim.RunDays(10);
        Assert.Equal(PowerSide.Town, p.Side);
        Assert.Equal(0, p.Unpaid);
    }

    [Fact]
    public void TheGreatAirshipsCostTensOfThousandsAndOutgunTheGalleon()
    {
        var c = Content.Default();
        var ships = c.Sky.Ships.ToDictionary(s => s.Id);
        Assert.True(ships["dreadnought"].Cost >= 10000 && ships["leviathan"].Cost > ships["dreadnought"].Cost);
        Assert.True(ships["dreadnought"].Guns > ships["galleon"].Guns && ships["leviathan"].Hull > ships["dreadnought"].Hull);
    }
}
