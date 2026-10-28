using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AccordsTests
{
    private static Simulation Town(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false, AutoStories = false });
        sim.RunDays(20);
        sim.Town.Treasury = 50000;
        return sim;
    }

    private static void Meet(Town t, Settlement s)
    {
        t.Regions[s.RegionId].Known = true;
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Heard);
    }

    [Fact]
    public void WarmingAPeopleCoolsItsOldRivalButNeverIntoWar()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = Town(seed).Town;
            var pair = t.Settlements.SelectMany(a => t.Settlements.Where(b => b.Id > a.Id && Accords.Rivals(t, a.Kin.Id, b.Kin.Id)).Select(b => (a, b))).FirstOrDefault();
            if (pair.a == null) continue;
            var (friend, rival) = pair;
            Meet(t, friend);
            Meet(t, rival);
            friend.Standing = 0;
            friend.Allied = false;
            rival.Standing = 20;
            World.Shift(t, friend, 10 / Character.Diplomacy(t, friend));
            Assert.True(rival.Standing < 20);
            World.Shift(t, friend, 200);
            Assert.True(friend.Allied);
            friend.Standing = 0;
            World.Shift(t, friend, 100);
            Assert.Equal(t.Content.Accords.RivalFloor, rival.Standing);
            return;
        }
        Assert.Fail("no seed has two rival peoples");
    }

    [Fact]
    public void AnAllyAsksAndSilenceCostsAsMuchAsNo()
    {
        var sim = Town();
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed);
        Meet(t, s);
        s.Allied = true;
        s.Standing = 90;
        s.Shadow = null;
        for (int d = 0; d < 120 && !Accords.Waiting(t).Any(); d++) sim.RunDays(1);
        var a = Assert.Single(Accords.Waiting(t));
        Assert.Contains(Alerts.Standing(t), x => x.Key == $"allyask:{a.Id}");
        double before = s.Standing;
        Assert.True(sim.AnswerAllyAsk(a.Id, false));
        Assert.True(s.Standing < before);
        Assert.False(sim.AnswerAllyAsk(a.Id, true));
        Assert.DoesNotContain(Alerts.Standing(t), x => x.Key == $"allyask:{a.Id}");

        // An ally left below the threshold walks away.
        s.Standing = t.Content.Accords.LeaveBelow - 5;
        sim.RunDays(1);
        Assert.False(s.Allied);
    }

    [Fact]
    public void APromiseGivenAtAFirstMeetingIsBrokenByMarchingOnThem()
    {
        var sim = Town();
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed);
        var kin = t.Settlements.FirstOrDefault(x => x.Id != s.Id && !x.Razed && x.Kin.Id == s.Kin.Id);
        Assert.NotEqual("", Accords.Give(t, s, "riders"));
        Assert.Equal("", Accords.Give(t, s, "riders"));
        double kinBefore = kin?.Standing ?? 0;
        Conquest.Declare(t, s);
        Assert.Equal(PromiseState.Broken, Assert.Single(Accords.To(t, s)).State);
        if (kin != null) Assert.True(kin.Standing < kinBefore);
    }

    [Fact]
    public void AMenaceCanBeParleyedWith()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        Menace? m = null;
        for (int d = 0; d < 400 && m == null; d++)
        {
            sim.RunDays(1);
            m = t.Saga.Menaces.FirstOrDefault(x => x.Alive && !x.BigBad && x.Origin != Menaces.FromWarband && x.ArrivalTick == null && Discovery.Known(t, x));
        }
        Assert.NotNull(m);
        t.Treasury = 50000;
        Assert.Null(Accords.ParleyBlocked(t, m!, Accords.Truce));
        Assert.Equal("it holds none of the town's people", Accords.ParleyBlocked(t, m!, Accords.Ransom));
        var beat = sim.Parley(m!.Id, Accords.Truce);
        Assert.NotNull(beat);
        Assert.Equal(Sheet.Succeeded(beat!), Accords.InTruce(t, m));
        Assert.StartsWith("again in", Accords.ParleyBlocked(t, m, Accords.Truce));
    }

    [Fact]
    public void TheGrandAllianceClaimedEarlyDrawsTheBigBadOut()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoStories = false });
        var t = sim.Town;
        for (int d = 0; d < 400 && t.Saga.Act < 1; d++) sim.RunDays(1);
        Assert.True(t.Saga.Act >= 1);
        Assert.Null(Saga.BigBad(t));
        var road = Victory.Def(t, "alliance")!;
        Assert.True(road.Early);
        foreach (var s in t.Settlements.Where(s => !s.Razed))
        {
            s.Allied = true;
            s.Standing = 90;
        }
        foreach (var p in t.Powers) p.Side = PowerSide.Town;
        Assert.Null(Victory.ClaimBlocked(t, "alliance"));
        Assert.NotNull(Victory.ClaimBlocked(t, "golden"));
        Assert.True(sim.ClaimRoad("alliance"));
        Assert.NotNull(Saga.BigBad(t));
        Assert.True(Saga.Finale(t));
        Assert.Equal(road.EarlyHold, Victory.DaysLeft(t));
    }
}
