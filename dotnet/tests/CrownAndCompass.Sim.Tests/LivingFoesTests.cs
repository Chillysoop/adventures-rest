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

    [Fact]
    public void TheHorrorUnderTheTownChangesFromGameToGameAndIsSometimesNotThere()
    {
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 40; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            string v = Stories.Variant(t, "horror");
            Assert.Equal(v, Stories.Variant(t, "horror"));
            seen.Add(v);
        }
        Assert.Contains("none", seen);
        Assert.True(seen.Count >= 3, string.Join(",", seen));
    }

    [Fact]
    public void SomeLateCampaignsOpenABurningDoorwayAndItsImpsMakeMischief()
    {
        int opened = 0, never = 0;
        for (int seed = 1; seed <= 20; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false }).Town;
            while (t.Saga.ActDays.Count < 3) t.Saga.ActDays.Add(t.Day);
            if (Menaces.DoorwayDay(t) == null) never++;
            else opened++;
        }
        Assert.True(opened > 0 && never > 0, $"opened {opened}, never {never}");

        for (int seed = 1; ; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
            var t = sim.Town;
            sim.RunDays(2);
            t.Saga.Act = 2;
            while (t.Saga.ActDays.Count < 3) t.Saga.ActDays.Add(t.Day);
            if (Menaces.DoorwayDay(t) is not int day) continue;
            t.Saga.ActDays[2] -= day - t.Day;
            Menaces.Daily(t, false);
            var m = Assert.Single(t.Saga.Menaces, x => x.Origin == Menaces.FromDoorway);
            Assert.Equal("fiends", m.Kind);
            Assert.True(m.Named);
            Menaces.Daily(t, false);
            Assert.Single(t.Saga.Menaces, x => x.Origin == Menaces.FromDoorway);
            break;
        }
    }

    [Fact]
    public void EveryLeaderHasACaseAndTheTruthComesOutWhenAHoldingFalls()
    {
        var (sim, m) = Rise("tribe");
        var t = sim.Town;
        m.Named = true;
        Assert.NotNull(Menaces.Case(t, m));
        Assert.Null(Menaces.Truth(t, m));
        var holding = Menaces.TakeHolding(t, m);
        Assert.NotNull(holding);
        holding!.Cleared = true;
        Menaces.Daily(t, false);
        Assert.NotNull(Menaces.Truth(t, m));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(Menaces.Truth(t, m)!));
        foreach (var kind in t.Content.Saga.Menaces.Kinds.Keys)
            Assert.True(t.Content.Saga.Menaces.Cases.Kinds.ContainsKey(kind), $"{kind} has no case");
    }

    [Fact]
    public void ARisingPowerPushesTheWeakerFoeNearItAndRefugeesCome()
    {
        var (sim, big) = Rise("dragon");
        var t = sim.Town;
        var small = Menaces.Rise(t, "beasts", "Old Tusk", 2, "act")!;
        small.Stage = MenaceStage.Rising;
        small.NextStrikeTick = t.Tick + 1000;
        big.Power = small.Power + 500;
        int folk = t.Agents.Count(a => !a.Dead && !a.Departed);
        int before = t.Ledger.Entries.Count;
        typeof(Menaces).GetMethod("Chain", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [t, big]);
        Assert.True(small.NextStrikeTick <= t.Tick);
        Assert.Contains(t.Ledger.Entries.Skip(before), e => e.Text.Contains("Driven from"));
        Assert.True(t.Agents.Count(a => !a.Dead && !a.Departed) > folk);
    }
}
