using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AllyCallsTests
{
    /// <summary>A grown village with every settlement it knows made an ally in good standing.</summary>
    private static Simulation Town(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false, Enemy = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        t.Treasury = 50000;
        foreach (var s in t.Settlements)
        {
            s.Allied = true;
            s.Razed = false;
            s.Standing = 60;
            s.Shadow = null;
        }
        return sim;
    }

    private static void Hours(Simulation sim, int n)
    {
        for (int h = 0; h < n; h++) sim.Step();
    }

    [Fact]
    public void AlliesAnswerWithinTheDayAndTheirHostsMarchTheRoad()
    {
        var sim = Town();
        var t = sim.Town;
        var allies = AllyCalls.Callable(t);
        Assert.NotEmpty(allies);
        double before = Threat.Defense(t);
        var sent = sim.CallAllies(allies.Select(s => s.Id));
        Assert.Equal(allies.Count, sent.Count);
        Assert.All(allies, s => Assert.Equal("answer on the way", AllyCalls.CallBlocked(t, s)));
        Hours(sim, 17);
        Assert.DoesNotContain(t.Army.Calls, c => c.State == CallState.Asked);

        // Holdouts are paid or let go; whoever marches gets here when the road says so, not before.
        foreach (var c in t.Army.Calls.Where(c => c.State == CallState.Holdout).ToList()) Assert.True(sim.BribeAlly(c.Id));
        var marching = t.Army.Calls.Where(c => c.State == CallState.Marching).ToList();
        Assert.NotEmpty(marching);
        var first = marching.OrderBy(c => c.ArriveTick).First();
        Assert.True(first.ArriveTick - t.Tick >= 0);
        Assert.Equal(before, Threat.Defense(t), 3);
        while (t.Tick < first.ArriveTick) sim.Step();
        sim.Step();
        Assert.Equal(CallState.Arrived, first.State);
        Assert.True(Threat.Defense(t) > before);

        // With no foe in sight they go home when their days are up.
        sim.RunDays(t.Content.Army.Allies.Stay + marching.Max(c => (c.ArriveTick - t.Tick) / SimTime.HoursPerDay) + 2);
        Assert.DoesNotContain(t.Army.Calls, c => c.State == CallState.Arrived);
    }

    [Fact]
    public void HoldoutsCanBeBoughtThreatenedOrLetGo()
    {
        var sim = Town();
        var t = sim.Town;
        var s = AllyCalls.Callable(t)[0];
        // A bad friend: far below in standing, nearly sure to hold out.
        s.Standing = 0;
        AllyCall? c = null;
        for (int tries = 0; tries < 40 && c?.State != CallState.Holdout; tries++)
        {
            t.Army.Calls.Clear();
            s.Allied = true;
            s.Standing = 0;
            c = sim.CallAllies([s.Id])[0];
            Hours(sim, 17);
        }
        Assert.Equal(CallState.Holdout, c!.State);
        Assert.True(c.Price > 0);
        Assert.Equal("holding out", AllyCalls.CallBlocked(t, s));

        double gold = t.Treasury;
        double standing = s.Standing;
        Assert.True(sim.ThreatenAlly(c.Id));
        Assert.True(s.Standing < standing);
        Assert.Equal(gold, t.Treasury);
        Assert.Contains(c.State, new[] { CallState.Marching, CallState.Refused });
        Assert.Equal(c.State == CallState.Marching, t.Stories.LastCheck != null && Sheet.Succeeded(t.Stories.LastCheck));

        // Paying works when there is gold; letting go just ends it.
        c.State = CallState.Holdout;
        t.Treasury = 0;
        Assert.Equal("not enough gold", AllyCalls.BribeBlocked(t, c));
        Assert.False(sim.BribeAlly(c.Id));
        Assert.True(sim.LetAllyGo(c.Id));
        Assert.Equal(CallState.Refused, c.State);
        s.Allied = true;
        Assert.Equal("called too lately", AllyCalls.CallBlocked(t, s));
    }

    [Fact]
    public void APuppetAllyMarchesAndGoesOverAtTheWalls()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        var s = t.Settlements[0];
        s.Allied = true;
        s.Standing = 60;
        var c = new AllyCall { Id = t.Army.NextCall++, SettlementId = s.Id, State = CallState.Arrived, AskedTick = t.Tick, Strength = 80, Turncoat = true, LeaveDay = t.Day + 10 };
        t.Army.Calls.Add(c);
        var lines = new List<string>();
        double went = AllyCalls.Betray(t, "the warband", lines);
        Assert.Equal(80, went);
        Assert.Equal(CallState.Turned, c.State);
        Assert.False(s.Allied);
        Assert.Single(lines);
        Assert.Equal(0, AllyCalls.Defense(t));
    }

    [Fact]
    public void AllyCallsReloadAndPlayOnTheSame()
    {
        var sim = Town();
        var t = sim.Town;
        sim.CallAllies(AllyCalls.Callable(t).Select(s => s.Id));
        Hours(sim, 6);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        sim.RunDays(8);
        loaded.RunDays(8);
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
    }
}
