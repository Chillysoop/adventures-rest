using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TurningTests
{
    private static Simulation RunUntilNamed(int seed, int count, SimulationOptions? options = null)
    {
        var sim = new Simulation(options ?? new SimulationOptions { Seed = seed });
        for (int d = 0; d < 500 && sim.Town.Saga.Menaces.Count(m => m.Named && m.Alive && !m.BigBad && m.Origin != Menaces.FromWarband) < count; d++) sim.RunDays(1);
        return sim;
    }

    [Fact]
    public void AMenaceLeaderWhoTurnsJoinsTheTownAsAHeroAndTheMenaceEnds()
    {
        var sim = RunUntilNamed(5, 1);
        var t = sim.Town;
        var m = t.Saga.Menaces.First(x => x.Named && x.Alive && !x.BigBad && x.Origin != Menaces.FromWarband);
        var hero = Turning.Redeem(t, m);
        Assert.NotNull(hero);
        Assert.False(m.Alive);
        Assert.True(m.Redeemed);
        Assert.Equal(m.Leader, hero!.Name);
        Assert.Contains(hero, t.Heroes);
        Assert.InRange(hero.Hero!.Level, 1, t.Content.Saga.Turning.MaxLevel);
        Assert.Contains(t.Content.Saga.Turning.Kinds[m.Kind].Classes, c => c == hero.Hero.Cls.Id);
        Assert.NotEqual("", Annals.Past(t, hero));
    }

    [Fact]
    public void AFallenHeroLeavesTownAndLeadsANewMenace()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(10);
        for (int d = 0; d < 100 && !sim.Town.Heroes.Any(h => !h.IsAway); d++) sim.RunDays(1);
        var t = sim.Town;
        var a = t.Heroes.Where(h => !h.IsAway).OrderBy(h => h.Id).First();
        var m = Turning.FallFrom(t, a);
        Assert.NotNull(m);
        Assert.True(m!.Alive && m.Named);
        Assert.Equal(a.Name, m.Leader);
        Assert.Equal(Turning.FromFallen, m.Origin);
        Assert.True(a.Departed);
        Assert.Equal(m.Id, a.Hero!.TurnedTo);
        Assert.DoesNotContain(a, t.Heroes);
    }

    [Fact]
    public void EveryClassCanFallAndEveryTurnedKindHasClasses()
    {
        var r = Content.Default().Saga.Turning;
        foreach (var cls in Content.Default().Classes) Assert.True(r.FallKinds.ContainsKey(cls.Id), $"{cls.Id} has no fall");
        Assert.All(r.FallKinds.Values, k => Assert.Contains(k, Content.Default().Saga.Menaces.Kinds.Keys));
        foreach (var (kind, def) in r.Kinds)
        {
            Assert.Contains(kind, Content.Default().Saga.Menaces.Kinds.Keys);
            Assert.NotEmpty(def.Classes);
            Assert.All(def.Classes, c => Assert.Contains(Content.Default().Classes, x => x.Id == c));
        }
    }

    [Fact]
    public void LegendsOfOneCampaignComeBackInAnother()
    {
        // A campaign whose great foe rose far enough to be remembered (most do; some are won first).
        List<Legend> legends = [];
        foreach (int seed in new[] { 3, 5, 7, 9, 11 })
        {
            var first = new Simulation(new SimulationOptions { Seed = seed });
            for (int d = 0; d < 1000 && first.Town.Outcome == null && !first.Town.Saga.Menaces.Any(m => m.Named && (m.BigBad || m.Peak >= MenaceStage.Power) && m.Origin != Menaces.FromWarband); d++) first.RunDays(1);
            legends = Legends.Record(first.Town, seed);
            if (legends.Any(l => l.Kind == Legend.Foe)) break;
        }
        Assert.Contains(legends, l => l.Kind == Legend.Foe);
        Assert.Contains(legends, l => l.Kind == Legend.Hero);
        var kept = Legends.Read(Legends.Write(Legends.Merge([], legends)));
        Assert.Equal(legends.Count, kept.Count);

        // The next campaign's town takes the foe in, named as it was, and remembers that it has.
        var next = new Simulation(new SimulationOptions { Seed = 6, Legends = kept });
        next.RunDays(20);
        var foe = kept.First(l => l.Kind == Legend.Foe);
        var hero = Turning.Return(next.Town, foe);
        Assert.NotNull(hero);
        Assert.Equal(foe.Name, hero!.Name);
        Assert.True(next.Town.Saga.LegendJoined);
        Assert.Contains(foe.Name, next.Town.Saga.LegendsUsed);

        // A campaign never meets its own legends.
        var same = new Simulation(new SimulationOptions { Seed = legends[0].Seed, Legends = kept });
        Assert.Empty(same.Town.Saga.Legends);
    }

    [Fact]
    public void AProfileKeepsTheNewestLegendsAndReplacesACampaignsOlderOnes()
    {
        var old = Enumerable.Range(1, Legends.Kept).Select(i => new Legend { Name = $"old {i}", Seed = i, Town = "Briarford" }).ToList();
        var fresh = new List<Legend> { new() { Name = "new", Seed = 3, Town = "Briarford" } };
        var merged = Legends.Merge(old, fresh);
        Assert.Equal(Legends.Kept, merged.Count);
        Assert.DoesNotContain(merged, l => l.Name == "old 3");
        Assert.Equal("new", merged[^1].Name);
    }
}
