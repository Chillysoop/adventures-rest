using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class SecretsTests
{
    /// <summary>A player's town with a motion before a full enough council.</summary>
    private static (Simulation Sim, Town T, Motion M) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoCouncil = false, AutoHonours = false, Enemy = false });
        var t = sim.Town;
        for (int d = 0; d < 200 && (Council.PendingMotion(t) == null || t.Seats.Count(s => s.HolderId != null) < 2); d++) sim.RunDays(1);
        var m = Council.PendingMotion(t);
        Assert.NotNull(m);
        return (sim, t, m!);
    }

    /// <summary>Dig into a councillor until the mayor's people come back with something, giving each a secret to find.</summary>
    private static Seat Dug(Simulation sim, Town t, Func<Seat, bool> which)
    {
        var seat = t.Seats.First(s => s.HolderId != null && which(s));
        int id = seat.HolderId!.Value;
        t.Secrets.RemoveAll(x => x.AgentId == id);
        t.Secrets.Add(new Secret { AgentId = id, Kind = "debts" });
        for (int i = 0; i < 40 && Secrets.Held(t, id) == null; i++)
        {
            t.Treasury = 1000;
            Secrets.Of(t, id)!.DugDay = -1000;
            Assert.True(sim.DigIntoCouncillor(id));
        }
        Assert.NotNull(Secrets.Held(t, id));
        return seat;
    }

    [Fact]
    public void ASecretPressesAVoteOnce()
    {
        var (sim, t, m) = Town();
        var seat = Dug(sim, t, s => s.Def.Id != m.Proposer);
        int id = seat.HolderId!.Value;
        bool before = m.Votes.First(v => v.Seat == seat.Def.Id).Yes;
        double loyalty = seat.Loyalty;
        Assert.True(sim.PressCouncillor(id, !before));
        Assert.Equal(!before, m.Votes.First(v => v.Seat == seat.Def.Id).Yes);
        Assert.True(seat.Loyalty < loyalty);
        // Spent: it can't be used again.
        Assert.Null(Secrets.Held(t, id));
        Assert.False(sim.PressCouncillor(id, before));
        Assert.False(sim.ExposeCouncillor(id));
    }

    [Fact]
    public void ExposingACouncillorCostsThemTheSeat()
    {
        var (sim, t, _) = Town();
        var seat = Dug(sim, t, _ => true);
        int id = seat.HolderId!.Value;
        Assert.True(sim.ExposeCouncillor(id));
        Assert.Null(seat.HolderId);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("gives up the seat"));
    }

    [Fact]
    public void ACouncillorIsLookedIntoOnlyAfterAWait()
    {
        var (sim, t, _) = Town();
        var seat = t.Seats.First(s => s.HolderId != null);
        int id = seat.HolderId!.Value;
        t.Secrets.Clear();
        t.Treasury = 1000;
        Assert.True(sim.DigIntoCouncillor(id));
        if (Secrets.Of(t, id) is { Known: false }) Assert.False(sim.DigIntoCouncillor(id));
        else Assert.NotNull(Secrets.DigBlocked(t, id));
    }

    [Fact]
    public void TheStandInMayorRollsNoDiceForSecrets()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, Enemy = false });
        sim.RunDays(60);
        Assert.Empty(sim.Town.Secrets);
    }

    [Fact]
    public void HonoursComeToThePlayerAsChoices()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoHonours = false, Enemy = false });
        var t = sim.Town;
        for (int d = 0; d < 200 && !t.Heroes.Any(a => Loyalty.ChampionBlocked(t, a) == null && a.Hero!.Loyalty >= 50); d++) sim.RunDays(1);
        Assert.DoesNotContain(t.Heroes, a => a.Hero!.Champion);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Honours);
    }
}
