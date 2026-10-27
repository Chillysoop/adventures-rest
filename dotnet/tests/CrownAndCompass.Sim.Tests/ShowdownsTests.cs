using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ShowdownsTests
{
    private static Simulation Town(int seed, int days)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void AStagedShowdownPlaysItselfToAnEnd()
    {
        var sim = Town(1, 120);
        var live = Showdowns.Stage(sim.Town, null, "fight")!;
        Assert.NotNull(live);
        var f = live.Fight;
        Assert.Equal("fight", f.Phase);
        Assert.Contains(f.Units(), u => u.Hero);
        Assert.Contains(f.Units(), u => u.Boss);
        f.Auto();
        Assert.Equal("over", f.Phase);
        Assert.True(f.Lines.Count > 3);
        // Nobody stands inside a wall or on another.
        var units = f.Units().Where(u => !u.Down).ToList();
        Assert.Equal(units.Count, units.Select(u => (u.X, u.Y)).Distinct().Count());
        Assert.All(units, u => Assert.NotEqual("pillar", f.Ground[u.Y * f.W + u.X]));
    }

    [Fact]
    public void ACommandedPartyFightsTurnByTurn()
    {
        var sim = Town(2, 120);
        var f = Showdowns.Stage(sim.Town, null, "fight")!.Fight;
        int steps = 0;
        while (f.Phase == "fight" && steps++ < 2000)
        {
            int hero = f.Waiting;
            Assert.NotEqual(0, hero);
            var me = f.Units().First(u => u.AgentId == hero);
            var foes = f.Units().Where(u => !u.Hero && !u.Down).OrderBy(u => Math.Max(Math.Abs(u.X - me.X), Math.Abs(u.Y - me.Y))).ToList();
            var acts = f.Actions();
            Assert.Contains(acts, a => a.Id == "end");
            var attack = acts.First(a => a.Id == "attack");
            var near = foes.First();
            int dist = Math.Max(Math.Abs(near.X - me.X), Math.Abs(near.Y - me.Y));
            if (attack.Ready && dist <= attack.Range)
            {
                Assert.True(f.Act("attack", near.X, near.Y));
                if (f.Phase == "fight" && f.Waiting == hero) f.Act("end");
                continue;
            }
            var moves = f.Moves();
            if (f.Turn.Move > 0 && moves.Count > 0)
            {
                var to = moves.OrderBy(m => Math.Max(Math.Abs(m.X - near.X), Math.Abs(m.Y - near.Y))).ThenBy(m => m.Y).ThenBy(m => m.X).First();
                if (Math.Max(Math.Abs(to.X - near.X), Math.Abs(to.Y - near.Y)) < dist)
                {
                    Assert.True(f.Move(to.X, to.Y));
                    continue;
                }
            }
            f.Act("end");
        }
        Assert.Equal("over", f.Phase);
    }

    [Fact]
    public void TalkingCanSettleItOrComeToBlades()
    {
        var sim = Town(3, 120);
        var f = Showdowns.Stage(sim.Town)!.Fight;
        if (f.Phase == "talk")
        {
            var ways = f.TalkOptions();
            Assert.Equal(3, ways.Count);
            for (int i = 0; i < 10 && f.Phase == "talk"; i++) f.Talk("persuade");
            Assert.NotEqual("talk", f.Phase);
            // Either the master was talked round, or the fight began.
            Assert.True(f.Talked || f.Phase == "fight");
        }
        else Assert.Equal("fight", f.Phase);
    }

    [Fact]
    public void ACompanyWaitsAtTheDoorAndGoesHomeAfter()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoStories = false, Showdowns = true });
        Party? waiting = null;
        for (int d = 0; d < 400 && waiting == null; d++)
        {
            sim.RunDays(1);
            waiting = sim.Town.Parties.FirstOrDefault(p => p.AtTheDoor);
        }
        Assert.NotNull(waiting);
        Assert.NotNull(sim.Showdown);
        Assert.Equal(int.MaxValue, waiting!.ReturnTick);
        sim.ShowdownAuto();
        Assert.Null(sim.Showdown);
        Assert.False(waiting.AtTheDoor);
        Assert.True(waiting.ReturnTick < int.MaxValue);
        Assert.NotNull(waiting.Result);
        sim.RunDays(30);
        Assert.True(waiting.Returned);
    }

    [Fact]
    public void AShowdownLostToALoadGoesInOffTheGrid()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoStories = false, Showdowns = true });
        Party? waiting = null;
        for (int d = 0; d < 400 && waiting == null; d++)
        {
            sim.RunDays(1);
            waiting = sim.Town.Parties.FirstOrDefault(p => p.AtTheDoor);
        }
        Assert.NotNull(waiting);
        // What a load sees: the party at the door, nothing live.
        var save = SaveGame.Write(sim);
        var back = SaveGame.Read(save);
        var p = back.Town.Parties.First(x => x.Id == waiting!.Id);
        Assert.True(p.AtTheDoor);
        back.RunDays(1);
        Assert.False(p.AtTheDoor);
        Assert.True(p.Dueled);
        Assert.NotNull(p.Result);
    }

    [Fact]
    public void TheStandInNeverStopsForOne()
    {
        var sim = Town(1, 200);
        Assert.DoesNotContain(sim.Town.Parties, p => p.Dueled);
    }
}
