using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CouncilTests
{
    private static Motion UntilMotion(Simulation sim, int maxDays = 120)
    {
        for (int d = 0; d < maxDays; d++)
        {
            var m = Council.PendingMotion(sim.Town);
            if (m != null) return m;
            sim.RunDays(1);
        }
        throw new InvalidOperationException("no motion was proposed");
    }

    [Fact]
    public void SeatsAStewardEarlyAndOpensMoreSeatsAsTheTownGrows()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(5);
        var steward = sim.Town.Seats.First(s => s.Def.Id == SeatId.Steward);
        Assert.NotNull(steward.HolderId);
        sim.RunDays(115);
        foreach (var s in sim.Town.Seats) if (sim.Town.Population >= s.Def.UnlockPopulation + 10) Assert.NotNull(s.HolderId);
        var ids = sim.Town.Seats.Select(s => s.HolderId).Where(x => x != null).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LetsTheMayorSignOrVetoAndChargesMandateForOverrulingTheMajority(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoCouncil = false });
        var m = UntilMotion(sim);
        int yes = m.Votes.Count(v => v.Yes);
        bool? majority = yes * 2 == m.Votes.Count ? null : yes * 2 > m.Votes.Count;
        double before = sim.Town.Mandate;
        bool sign = majority == null || !majority.Value;
        Assert.True(sim.DecideMotion(m.Id, sign));
        Assert.Equal(DecidedBy.Mayor, m.DecidedBy);
        Assert.Equal(majority != null, m.Decree);
        if (m.Decree) Assert.True(sim.Town.Mandate < before);
        Assert.False(sim.DecideMotion(m.Id, true));
    }

    [Fact]
    public void NamesTheMotionThatLapsesOnTheVeryNextStep()
    {
        // The client pauses on this, so it must warn exactly once: the step before the council settles the motion.
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoCouncil = false });
        var m = UntilMotion(sim);
        int warned = 0;
        while (m.Status == MotionStatus.Pending)
        {
            bool lapsing = Council.Lapsing(sim.Town) == m;
            if (lapsing) warned++;
            sim.Step();
            Assert.Equal(lapsing, m.Status != MotionStatus.Pending);
        }
        Assert.Equal(1, warned);
        Assert.Equal(DecidedBy.Council, m.DecidedBy);
    }

    [Fact]
    public void LetsTheCouncilVoteStandWhenTheMayorSaysNothing()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoCouncil = false });
        var m = UntilMotion(sim);
        sim.RunDays(sim.Town.Content.Council.MotionDays + 1);
        Assert.NotEqual(MotionStatus.Pending, m.Status);
        Assert.Equal(DecidedBy.Council, m.DecidedBy);
    }

    [Fact]
    public void ReplaysIdenticallyFromTheSameSeed()
    {
        static string Run()
        {
            var sim = new Simulation(new SimulationOptions { Seed = 7 });
            sim.RunDays(90);
            return string.Join(",", sim.Town.Motions.Select(m => $"{m.Kind}:{m.Status}")) + $"|{Js.ToFixed(sim.Town.Mandate, 3)}";
        }
        Assert.Equal(Run(), Run());
    }
}
