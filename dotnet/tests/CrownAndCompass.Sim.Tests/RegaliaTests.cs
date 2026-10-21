using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class RegaliaTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4, MayorRoll? mayor = null)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false, Mayor = mayor });
        return (sim, sim.Town);
    }

    [Fact]
    public void EveryPieceIsALibraryItemForAKnownSlot()
    {
        var c = Content.Default();
        var r = c.Regalia;
        Assert.Equal(5, r.Slots.Count);
        foreach (var d in r.Items)
        {
            var it = c.Library.Item(d.Ref);
            Assert.True(it != null, $"{d.Ref} is not in the library");
            Assert.True(it!.Slot is "regalia" or "town", $"{d.Ref} is a {it.Slot} item");
            Assert.True(r.Slot(d.Slot) != null, $"{d.Ref} has no slot {d.Slot}");
            foreach (var skill in d.Skills.Keys) Assert.True(c.Stories.Skill(skill) != null, $"{d.Ref} names no skill {skill}");
        }
        foreach (var id in r.Start) Assert.NotNull(r.Item(id));
        // Every library regalia item is wearable.
        foreach (var it in c.Library.Items.Where(i => i.Slot == "regalia")) Assert.True(r.Item(it.Id) != null, $"{it.Id} has no regalia entry");
    }

    [Fact]
    public void TheMayorStartsWearingTheCharterChain()
    {
        var (_, t) = NewTown();
        var chain = Assert.Single(t.Regalia.Worn);
        Assert.Equal("chain-of-the-first-charter", chain.Item.Id);
        var (_, prof, _) = Stories.Bonus(t, "persuasion");
        Regalia.Unequip(t, "chain");
        var (_, without, _) = Stories.Bonus(t, "persuasion");
        Assert.Equal(1, prof - without);
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "chain-of-the-first-charter");
    }

    [Fact]
    public void ACommissionNeedsTheCharterAndTheGoldAndSwapsWhatItReplaces()
    {
        var (sim, t) = NewTown();
        t.Treasury = 5000;
        Assert.Equal("needs a hamlet", Regalia.Refuse(t, "burghers-seven-link-chain"));
        Assert.Equal("can't be made", Regalia.Refuse(t, "chain-of-the-first-charter"));
        t.City.Tier = 1;
        double before = t.Treasury;
        Assert.True(sim.CommissionRegalia("burghers-seven-link-chain"));
        Assert.True(t.Treasury < before);
        Assert.Equal("already held", Regalia.Refuse(t, "burghers-seven-link-chain"));
        var made = t.Armory.Stock.Single(s => s.Item.Id == "burghers-seven-link-chain");
        Assert.True(sim.WearRegalia(made.Id));
        Assert.Equal("burghers-seven-link-chain", Regalia.In(t, "chain")!.Item.Id);
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "chain-of-the-first-charter");
        // Heroes have no use for the mayor's regalia.
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        Assert.NotNull(Armory.Refuse(t, hero, made.Item));
    }

    [Fact]
    public void ThePiecesHelpWhereTheyAreWorn()
    {
        var (sim, t) = NewTown();
        t.Treasury = 50000;
        t.City.Tier = 4;
        int insp = Sheet.InspirationMax(t);
        foreach (var id in new[] { "mantle-of-the-lamplit-hall", "signet-of-the-open-hand", "town-pennant" })
        {
            Assert.True(sim.CommissionRegalia(id), id);
            Assert.True(sim.WearRegalia(t.Armory.Stock.Single(s => s.Item.Id == id).Id));
        }
        Assert.Equal(insp + 1, Sheet.InspirationMax(t));
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        Assert.Contains(Loyalty.Factors(t, hero), f => f.Why.Contains("Signet of the Open Hand"));

        // A company marching under the pennant fights the better for it, until it comes home.
        int bonus = hero.Hero!.WeaponBonus;
        var party = new Party { Id = t.Parties.Count + 1, Name = "Pennant Company", Members = [hero.Id], SiteId = t.Sites[0].Def.Id, DepartTick = t.Tick };
        t.Parties.Add(party);
        Regalia.Daily(t, false);
        Assert.Equal(bonus + 1, hero.Hero.WeaponBonus);
        Regalia.Daily(t, false);
        Assert.Equal(bonus + 1, hero.Hero.WeaponBonus);
        party.Returned = true;
        Regalia.Daily(t, false);
        Assert.Equal(bonus, hero.Hero.WeaponBonus);

        // Taking the mantle off takes its inspiration with it.
        t.Stories.Mayor.Inspiration = Sheet.InspirationMax(t);
        Assert.True(sim.TakeOffRegalia("mantle"));
        Assert.Equal(insp, t.Stories.Mayor.Inspiration);
    }

    [Fact]
    public void TheStandInWearsTheFinestPieceInTheArmory()
    {
        var (_, t) = NewTown();
        t.Treasury = 50000;
        t.City.Tier = 4;
        Regalia.Commission(t, "oathbound-chain");
        Regalia.Daily(t, true);
        Assert.Equal("oathbound-chain", Regalia.In(t, "chain")!.Item.Id);
    }

    [Fact]
    public void ARolledMayorIsTheTownsAndRerollsDiffer()
    {
        var c = Content.Default();
        var a = Stories.RollMayor(c, 11, "soldier");
        Assert.Equal(a.Scores, Stories.RollMayor(c, 11, "soldier").Scores);
        Assert.Equal(6, a.Scores.Count);
        Assert.All(a.Scores, s => Assert.InRange(s, 3, 18));
        Assert.Equal(a.Scores.OrderByDescending(s => s), a.Scores);
        Assert.Contains(Enumerable.Range(12, 20), seed => !Stories.RollMayor(c, seed).Scores.SequenceEqual(a.Scores));

        var (_, t) = NewTown(mayor: a);
        var m = t.Stories.Mayor;
        Assert.Equal(a.Name, m.Name);
        Assert.Equal("soldier", m.Background);
        Assert.True(m.Chosen);
        // The soldier's best ability takes the highest roll, +2.
        Assert.Equal(a.Scores[0] + 2, Stories.Scores(t).Str);
    }
}
