using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BoonsTests
{
    [Fact]
    public void EveryBoonHasAKindAWayToEarnItAndANumber()
    {
        var c = Content.Default();
        var roads = c.Victory.Roads.Select(r => r.Id).ToHashSet();
        var grades = c.Boons.Grades.Select(g => g.Id).ToHashSet();
        string[] kinds = ["military", "economy", "tech", "adventuring", "diplomacy"];
        foreach (var b in c.Boons.Boons)
        {
            Assert.Contains(b.Kind, kinds);
            Assert.True(b.Road == "" ? grades.Contains(b.Grade) : roads.Contains(b.Road), b.Id);
            Assert.True(b.PerRank > 0, b.Id);
            Assert.Contains("{n}", b.Text);
        }
        Assert.All(kinds, k => Assert.Contains(c.Boons.Boons, b => b.Kind == k));
    }

    [Fact]
    public void AFasterWinEarnsAHigherGrade()
    {
        var c = Content.Default();
        Assert.Equal("gold", Boons.Grade(c, 300).Id);
        Assert.Equal("silver", Boons.Grade(c, 700).Id);
        Assert.Equal("bronze", Boons.Grade(c, 5000).Id);
    }

    [Fact]
    public void OnlyAWonCampaignEarnsBoons()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(2);
        Assert.Empty(Boons.Earned(sim.Town));
        sim.Town.Outcome = Outcome.Victory;
        sim.Town.Victory.Road = "golden";
        var earned = Boons.Earned(sim.Town);
        // A gold win: the road's own boon and the gold grade's.
        Assert.Equal(["golden-coffers", "old-lore"], earned.Select(e => e.Boon.Id).Order(StringComparer.Ordinal));
        Assert.All(earned, e => Assert.Equal(3, e.Rank));
    }

    [Fact]
    public void ATownIsFoundedWithTheBoonsChosen()
    {
        var plain = new Simulation(new SimulationOptions { Seed = 4 }).Town;
        var t = new Simulation(new SimulationOptions
        {
            Seed = 4,
            Boons = ["golden-coffers:2", "old-banners:1", "veterans:9", "old-lore", "merchant-charter:1", "old-friends:1", "nonsense:3"],
        }).Town;
        Assert.Equal(["golden-coffers", "merchant-charter", "old-banners", "old-friends", "old-lore", "veterans"], t.Boons.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Boons.MaxRank, Boons.Rank(t, "veterans"));
        Assert.Equal(1, Boons.Rank(t, "old-lore"));
        Assert.Equal(plain.Treasury + 300, t.Treasury, 3);
        Assert.Equal(plain.Watch + 1, t.Watch);
        Assert.Equal(plain.Research.Notes + 20, t.Research.Notes, 3);
        Assert.Equal(plain.Heroes.Max(h => h.Hero!.Level) + 3, t.Heroes.Max(h => h.Hero!.Level));
        Assert.Equal(1, Boons.Routes(t));
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("The founders bring", StringComparison.Ordinal));
    }

    [Fact]
    public void NoBoonsChangeNothing()
    {
        var a = new Simulation(new SimulationOptions { Seed = 5 });
        var b = new Simulation(new SimulationOptions { Seed = 5, Boons = [] });
        a.RunDays(20);
        b.RunDays(20);
        Assert.Equal(a.Town.Ledger.Entries.Select(e => e.Text), b.Town.Ledger.Entries.Select(e => e.Text));
    }
}
