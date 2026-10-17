using System.Diagnostics;

namespace CrownAndCompass.Sim.Tests;

public class SimulationTests
{
    private static Simulation Run(int seed, int days)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void ReplaysIdenticallyFromTheSameSeed()
    {
        var a = Run(42, 40).Town.Ledger.Entries.Select(e => e.Format()).ToList();
        var b = Run(42, 40).Town.Ledger.Entries.Select(e => e.Format()).ToList();
        Assert.Equal(a, b);
    }

    [Fact]
    public void TellsDifferentStoriesFromDifferentSeeds()
    {
        var a = Run(1, 20).Town.Ledger.Entries.Select(e => e.Text).ToList();
        var b = Run(2, 20).Town.Ledger.Entries.Select(e => e.Text).ToList();
        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    public void GrowsAndBuilds(int seed)
    {
        var sim = Run(seed, 60);
        var town = sim.Town;
        Assert.True(town.Population > sim.Options.Settlers);
        Assert.True(town.Buildings.Count(b => b.Complete) > 5);
        Assert.Contains(town.Buildings, b => b.Def.Venue && b.Complete);
        Assert.Contains(town.Lots, l => l.OwnerId != null);
    }

    [Fact]
    public void KeepsTownsfolkFedAndMostlyHoused()
    {
        var res = Run(3, 60).Town.Residents;
        Assert.True(Js.Sum(res, a => a.Hunger) / res.Count < 60);
        Assert.True(res.Count(a => a.HomeId == null) <= res.Count / 4 + 1);
    }

    [Fact]
    public void WritesSocialStoriesIntoTheLedger()
    {
        var cats = Run(1, 120).Town.Ledger.Entries.Select(e => e.Category).ToHashSet();
        foreach (var c in new[] { LedgerCategory.Social, LedgerCategory.Property, LedgerCategory.Arrival }) Assert.Contains(c, cats);
        Assert.True(cats.Contains(LedgerCategory.Brawl) || cats.Contains(LedgerCategory.Romance));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public void KeepsHomesJobsAndOwnershipConsistent(int seed)
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
            if (a.JobId != null) Assert.Contains(a.Id, town.Building(a.JobId.Value).Workers);
            if (a.HomeId != null) Assert.Contains(a.Id, town.Building(a.HomeId.Value).Residents);
            if (a.SpouseId != null) Assert.Equal(a.Id, town.Agent(a.SpouseId.Value).SpouseId);
        }
        foreach (var b in town.Buildings)
        {
            Assert.True(b.Residents.Count <= b.Def.Housing);
            foreach (int w in b.Workers) Assert.Equal(b.Id, town.Agent(w).JobId);
            Assert.Equal(b.Id, town.Lot(b.LotId).BuildingId);
        }
    }

    [Fact]
    public void RunsA120DayTownQuickly()
    {
        var watch = Stopwatch.StartNew();
        Run(1, 120);
        Assert.True(watch.ElapsedMilliseconds < 5000);
    }
}

public class DiceAndMarketTests
{
    [Fact]
    public void KeepsDiceInRange()
    {
        var rng = new Rng(9);
        for (int i = 0; i < 10_000; i++)
        {
            int d = rng.D20();
            Assert.InRange(d, 1, 20);
            int s = rng.AbilityScore();
            Assert.InRange(s, 3, 18);
        }
    }

    [Fact]
    public void RollsARoughlyUniformD20()
    {
        var rng = new Rng(11);
        var counts = new int[21];
        for (int i = 0; i < 40_000; i++) counts[rng.D20()]++;
        for (int face = 1; face <= 20; face++)
        {
            Assert.True(counts[face] > 1700);
            Assert.True(counts[face] < 2300);
        }
    }

    [Fact]
    public void FollowsTheModifierTable()
    {
        Assert.Equal([-1, 0, 0, 3, -4], new[] { 8, 10, 11, 16, 3 }.Select(Rng.Modifier).ToArray());
    }

    [Fact]
    public void RaisesPricesOnScarcityAndImportsShortfallsDearly()
    {
        var m = new Market();
        m.SetTarget(Good.Food, 100);
        m.Seed(Good.Food, 100);
        double healthy = m.Price(Good.Food);
        m.Buy(Good.Food, 90);
        Assert.True(m.Price(Good.Food) > healthy);
        Assert.True(m.Quote(Good.Food, 20) > 20 * Market.BasePrice(Good.Food) * 2);
    }

    [Fact]
    public void MatchesTheTypeScriptGeneratorBitForBit()
    {
        // First draws of new Rng(1) and new Rng(123456) in the TypeScript sim.
        var a = new Rng(1);
        Assert.Equal(Expected1, Enumerable.Range(0, Expected1.Length).Select(_ => a.NextUint()).ToArray());
        var b = new Rng(123456);
        Assert.Equal(Expected123456, Enumerable.Range(0, Expected123456.Length).Select(_ => b.NextUint()).ToArray());
    }

    private static readonly uint[] Expected1 = [2442144158, 3238099751, 3819917871, 2104621829, 2021136066, 4223536128, 1515984730, 2298887649];
    private static readonly uint[] Expected123456 = [3968961998, 3276381896, 4259053971, 1021512644, 3062622669, 2034986438, 3250480258, 2658268022];
}
