using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Dragons told as done, doing and doom; a lich that can't die while its phylactery is hidden.</summary>
public class LivingFoesTests
{
    private static (Simulation Sim, Menace M) Rise(string kind, int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        sim.RunDays(3);
        var m = Menaces.Rise(sim.Town, kind, "Korrathax", 6, "act")!;
        Assert.NotNull(m);
        return (sim, m);
    }

    [Fact]
    public void ADragonIsAnnouncedWithWhatItHasDone()
    {
        var (sim, m) = Rise("dragon");
        var t = sim.Town;
        int before = t.Ledger.Entries.Count;
        Menaces.Announce(t, m, Quests.Site(t, m.SeatId)!);
        var done = Menaces.Done(t, m);
        Assert.NotNull(done);
        Assert.Contains(t.Ledger.Entries.Skip(before), e => e.Text.Contains(done!));
    }

    [Fact]
    public void ADragonsPlansAreWatchedFromTheStartAndSendPortentsHalfway()
    {
        var (sim, m) = Rise("dragon");
        var t = sim.Town;
        m.Named = true;
        m.Stage = MenaceStage.Rising;
        m.Plan = null;
        m.NextPlanDay = t.Day;
        Plots.Daily(t);
        Assert.NotNull(m.Plan);
        Assert.True(m.Plan!.Known, "a named dragon's plan should be known as it begins");
        int before = t.Ledger.Entries.Count;
        m.Plan.StartDay = t.Day - 20;
        m.Plan.DueDay = t.Day + 10;
        Plots.Daily(t);
        Assert.True(m.Plan.Portent);
        Assert.Contains(t.Ledger.Entries.Skip(before), e => e.Text.Contains(m.Leader) && e.Text.Contains("days"));
    }

    [Fact]
    public void ALichCannotDieWhileItsPhylacteryIsHidden()
    {
        var (sim, m) = Rise("undead");
        var t = sim.Town;
        m.Cunning = "mastermind";
        m.Named = true;
        m.Holdings.Clear();
        m.Assets.Clear();
        var seat = Quests.Site(t, m.SeatId)!;
        var far = t.Sites.First(s => s.Generated && !s.Cleared && s.RegionId != seat.RegionId && Menaces.At(t, s.Def.Id) == null);
        m.Assets.Add(new MenaceAsset { Def = "phylactery", SiteId = far.Def.Id });

        // Its seat falls: it reforms where the phylactery lies.
        seat.Cleared = true;
        Menaces.Daily(t, false);
        Assert.True(m.Alive, "the lich died with its phylactery still hidden");
        Assert.Equal(far.Def.Id, m.SeatId);
        Assert.NotEqual(SiteKnowledge.Unheard, far.Known);
        Assert.True(t.Regions[far.RegionId!.Value].Known);

        // Clear the phylactery's hiding place too, and it ends.
        far.Cleared = true;
        Menaces.Daily(t, false);
        Assert.False(m.Alive);
    }

    [Fact]
    public void OnlyAMastermindKeepsAPhylacteryAndHidesItFarAway()
    {
        var phyl = Content.Default().Saga.Menaces.Plots.Asset("phylactery")!;
        Assert.True(phyl.Undying && phyl.Far);
        Assert.Equal("mastermind", phyl.MinCunning);
        for (int seed = 1; seed <= 6; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(150);
            foreach (var m in sim.Town.Saga.Menaces)
                foreach (var a in m.Assets.Where(a => a.Def == "phylactery"))
                {
                    Assert.Equal("undead", m.Kind);
                    Assert.Equal("mastermind", m.Cunning);
                }
        }
    }
}
