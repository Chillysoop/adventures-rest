using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class SimulationTests
{
    private static Simulation Run(ulong seed, int days)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void SameSeedReplaysIdentically()
    {
        var a = Run(42, 40).Town.Ledger.Entries.Select(e => e.ToString()).ToList();
        var b = Run(42, 40).Town.Ledger.Entries.Select(e => e.ToString()).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void DifferentSeedsTellDifferentStories()
    {
        var a = Run(1, 20).Town.Ledger.Entries.Select(e => e.Text);
        var b = Run(2, 20).Town.Ledger.Entries.Select(e => e.Text);
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(7UL)]
    public void TownGrowsAndBuilds(ulong seed)
    {
        var sim = Run(seed, 60);
        var town = sim.Town;
        Assert.True(town.Population > sim.Options.Settlers, $"population {town.Population}");
        Assert.True(town.Buildings.Count(b => b.Complete) > 5);
        Assert.Contains(town.Buildings, b => b.Def.Venue && b.Complete);
        Assert.Contains(town.Lots, l => l.OwnerId != null);
    }

    [Fact]
    public void TownsfolkAreFedAndMostlyHoused()
    {
        var town = Run(3, 60).Town;
        var res = town.Residents.ToList();
        Assert.True(res.Average(a => a.Hunger) < 60);
        Assert.True(res.Count(a => a.HomeId == null) <= res.Count / 4 + 1);
    }

    [Fact]
    public void LedgerCarriesSocialStories()
    {
        var town = Run(1, 120).Town;
        var cats = town.Ledger.Entries.Select(e => e.Category).ToHashSet();
        Assert.Contains(LedgerCategory.Social, cats);
        Assert.Contains(LedgerCategory.Property, cats);
        Assert.Contains(LedgerCategory.Arrival, cats);
        Assert.True(cats.Contains(LedgerCategory.Brawl) || cats.Contains(LedgerCategory.Romance));
    }

    [Theory]
    [InlineData(1UL)]
    [InlineData(2UL)]
    [InlineData(5UL)]
    public void HomesJobsAndOwnershipStayConsistent(ulong seed)
    {
        var town = Run(seed, 90).Town;
        foreach (var a in town.Agents)
        {
            if (a.Departed)
            {
                Assert.Null(a.JobId);
                Assert.Null(a.HomeId);
                Assert.DoesNotContain(town.Buildings, b => b.OwnerId == a.Id);
                continue;
            }
            if (a.JobId is int j) Assert.Contains(a.Id, town.Building(j).Workers);
            if (a.HomeId is int h) Assert.Contains(a.Id, town.Building(h).Residents);
            if (a.SpouseId is int s) Assert.Equal(a.Id, town.Agent(s).SpouseId);
        }
        foreach (var b in town.Buildings)
        {
            Assert.True(b.Residents.Count <= Math.Max(b.Def.Housing, 0));
            Assert.All(b.Workers, w => Assert.Equal(b.Id, town.Agent(w).JobId));
            Assert.Equal(b.Id, town.Lot(b.LotId).BuildingId);
        }
    }
}

public class DiceAndMarketTests
{
    [Fact]
    public void DiceStayInRange()
    {
        var rng = new Rng(9);
        for (int i = 0; i < 10_000; i++)
        {
            Assert.InRange(rng.D20(), 1, 20);
            Assert.InRange(rng.AbilityScore(), 3, 18);
            Assert.InRange(rng.Roll(3, 6), 3, 18);
        }
    }

    [Fact]
    public void D20IsRoughlyUniform()
    {
        var rng = new Rng(11);
        var counts = new int[21];
        for (int i = 0; i < 40_000; i++) counts[rng.D20()]++;
        for (int face = 1; face <= 20; face++) Assert.InRange(counts[face], 1700, 2300);
    }

    [Fact]
    public void ModifiersFollowTheTable()
    {
        Assert.Equal(-1, Dice.Modifier(8));
        Assert.Equal(0, Dice.Modifier(10));
        Assert.Equal(0, Dice.Modifier(11));
        Assert.Equal(3, Dice.Modifier(16));
        Assert.Equal(-4, Dice.Modifier(3));
    }

    [Fact]
    public void ScarcityRaisesPricesAndShortfallsImportDearly()
    {
        var m = new Market();
        m.SetTarget(Good.Food, 100);
        m.Seed(Good.Food, 100);
        double healthy = m.Price(Good.Food);
        m.Buy(Good.Food, 90);
        Assert.True(m.Price(Good.Food) > healthy);
        double quote = m.Quote(Good.Food, 20);
        Assert.True(quote > 20 * Market.BasePrice[Good.Food] * 2, "caravan imports should be expensive");
    }
}
