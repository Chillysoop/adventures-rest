using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class HordeTests
{
    private static (Simulation Sim, Menace Big) Crowned(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        m!.BigBad = true;
        sim.Town.Saga.BigBadId = m.Id;
        sim.Town.Saga.CrownDay = sim.Town.Day;
        Menaces.Ascend(sim.Town, m);
        return (sim, m);
    }

    private static HordeCamp Seat(Town t, Menace m) => Horde.State(t).Camps.First(c => c.SiteId == m.SeatId);

    [Fact]
    public void TheHostGathersAtItsCampsBeforeItMarches()
    {
        var (sim, m) = Crowned();
        var t = sim.Town;
        Horde.Daily(t, m);
        var h = Horde.State(t);
        Assert.Equal(HordeStage.Gathering, h.Stage);
        Assert.Contains(h.Camps, c => c.SiteId == m.SeatId);
        double first = Horde.Gathered(t);
        Assert.True(first > 0);
        Horde.Daily(t, m);
        Assert.True(Horde.Gathered(t) > first);
        Assert.Null(m.ArrivalTick);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "horde");

        // Full and due: the bands come together at the seat, then the whole of what gathered marches.
        Seat(t, m).Strength = Horde.Full(t, m) * 1.01;
        m.NextAssaultTick = t.Tick;
        Horde.Daily(t, m);
        Assert.Equal(HordeStage.Mustering, h.Stage);
        double host = Horde.Gathered(t);
        h.MusterTick = t.Tick;
        Horde.Daily(t, m);
        Assert.Equal(HordeStage.Marching, h.Stage);
        Assert.NotNull(m.ArrivalTick);
        Assert.Equal(host, m.Power, 6);
        Assert.Equal(0, Horde.Gathered(t));
    }

    [Fact]
    public void FightsAtACampThinItAndAThinHostStillMarches()
    {
        var (sim, m) = Crowned();
        var t = sim.Town;
        Horde.Daily(t, m);
        var seat = Quests.Site(t, m.SeatId)!;
        Seat(t, m).Strength = 100;
        seat.Attempts++;
        Horde.Daily(t, m);
        Assert.Equal(100 * (1 - Horde.Rules(t).Struck), Seat(t, m).Strength, 6);

        // Short of its due, thin: it waits.
        foreach (var c in Horde.State(t).Camps) c.Strength = 0;
        m.NextAssaultTick = t.Tick - (Horde.Rules(t).Patience - 2) * SimTime.HoursPerDay;
        Horde.Daily(t, m);
        Assert.Equal(HordeStage.Gathering, Horde.State(t).Stage);
        // Long past due with too little gathered: it musters anyway, and the town hears it comes thin.
        m.NextAssaultTick = t.Tick - (Horde.Rules(t).Patience + 1) * SimTime.HoursPerDay;
        Horde.Daily(t, m);
        Assert.Equal(HordeStage.Mustering, Horde.State(t).Stage);
        Assert.True(Horde.State(t).ToldThin);
    }

    [Fact]
    public void WithoutEyesTheMarchIsHeardOfOnlyWhenItIsClose()
    {
        var (sim, m) = Crowned();
        var t = sim.Town;
        Horde.Daily(t, m);
        t.Scouts.Clear();
        foreach (var w in t.Wilds.ToList()) t.Wilds.Remove(w);
        Seat(t, m).Strength = Horde.Full(t, m) * 1.01;
        m.NextAssaultTick = t.Tick;
        Horde.Daily(t, m);
        Horde.State(t).MusterTick = t.Tick;
        Horde.Daily(t, m);
        Assert.NotNull(m.ArrivalTick);
        Assert.False(Horde.RoadWatched(t, m));
        Assert.False(Horde.Known(t, m));
        Assert.DoesNotContain(Field.Foes(t), f => f.Key == Discovery.Key(m));
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"menace:{m.Id}");

        // A tower over its seat sees it go; and close to the walls everyone knows.
        Quests.Site(t, m.SeatId)!.Held = true;
        Assert.True(Horde.Known(t, m));
        Quests.Site(t, m.SeatId)!.Held = false;
        m.ArrivalTick = t.Tick + SimTime.HoursPerDay;
        Assert.True(Horde.Known(t, m));
        var alert = Alerts.Standing(t).First(a => a.Key == $"menace:{m.Id}");
        Assert.DoesNotContain("day", alert.Text);
    }

    [Fact]
    public void TheFirstBreachBurnsTheLowerTownAndTheNextFallsIt()
    {
        var (sim, m) = Crowned();
        var t = sim.Town;
        t.Treasury = 1000;
        double power = m.Power;
        var lines = new List<string>();
        Assert.True(Horde.Reprieve(t, m, lines));
        Assert.Equal(700, t.Treasury, 6);
        Assert.True(m.Power < power);
        Assert.Contains("lower town", lines[^1]);
        Assert.False(Horde.Reprieve(t, m, lines));
    }

    [Fact]
    public void TheRelentlessStorytellerIsHarderOnEveryLever()
    {
        var c = Content.Default();
        var rising = Storyteller.Def(c, "rising")!;
        var hard = Storyteller.Def(c, "relentless")!;
        Assert.True(hard.Pace > rising.Pace && hard.RaidStrength > rising.RaidStrength && hard.MenaceGrowth > rising.MenaceGrowth);
        Assert.True(hard.RaidEvery < rising.RaidEvery && hard.StrikeEvery < rising.StrikeEvery && hard.Lull < rising.Lull && hard.Ripen < rising.Ripen);
        Assert.True(hard.HordeGrowth > 1 && hard.HordeSize > 1 && hard.Acts < 1);
        Assert.Equal(0, hard.Swing);
    }
}
