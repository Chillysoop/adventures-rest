using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ConquestTests
{
    /// <summary>A town with a strong drilled host, every settlement met, the weakest the target and two allies.</summary>
    private static (Simulation Sim, Settlement Target) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, Enemy = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 50000;
        var d = t.Content.Building("barracks");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false).BuildProgress = d.BuildHours;
        var r = sim.RaiseRegiment("swordsmen", 40, ArmyPost.Walls)!;
        Army.Fill(t, r);
        r.Drill = 1;
        r.Marches = true;
        foreach (var s in t.Settlements) s.Met = true;
        foreach (var s in t.Settlements) s.Prosperity = 0;
        var target = t.Settlements.OrderBy(s => Conquest.Defenders(t, s)).ThenBy(s => t.Regions[s.RegionId].Days).ThenBy(s => s.Id).First();
        foreach (var s in t.Settlements.Where(s => s.Id != target.Id).Take(2))
        {
            s.Allied = true;
            s.Standing = 70;
        }
        return (sim, target);
    }

    private static void Win(Simulation sim, Settlement target)
    {
        var t = sim.Town;
        var s = sim.Attack(target.Id, "advance")!;
        Assert.NotNull(s);
        while (t.Army.Sortie != null) sim.Step();
        Assert.True(t.Army.Pending.Contains(target.Id), string.Join("\n", t.Army.Fields[^1].Lines));
    }

    [Fact]
    public void MarchingOnASettlementWinsItAndEveryPeopleSeesIt()
    {
        var (sim, target) = Town();
        var t = sim.Town;
        Assert.Contains(target, Conquest.Targets(t));
        var others = t.Settlements.Where(s => s.Id != target.Id).ToDictionary(s => s.Id, s => s.Standing);
        var s0 = sim.Attack(target.Id, "advance")!;
        // The march takes the road's days, not hours.
        Assert.True(s0.MeetTick - t.Tick >= SimTime.HoursPerDay);
        Assert.True(target.Standing <= -40);
        Assert.Contains(t.Settlements, s => s.Allied && s.Standing < others[s.Id]);
        while (t.Army.Sortie != null) sim.Step();
        Assert.Contains(target.Id, t.Army.Pending);
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"conquered:{target.Id}");
        Assert.Equal("waiting on your word", Conquest.AttackBlocked(t, target));
    }

    [Fact]
    public void PillageBurnsItAndBringsGold()
    {
        var (sim, target) = Town();
        var t = sim.Town;
        Win(sim, target);
        double gold = t.Treasury;
        Assert.True(sim.SettleConquest(target.Id, ConquestChoice.Pillage));
        Assert.True(target.Razed);
        Assert.True(t.Treasury > gold);
        Assert.DoesNotContain(target, Conquest.Targets(t));
    }

    [Fact]
    public void AnOccupiedTownPaysTributeResistsAndRisesWithoutAGarrison()
    {
        var (sim, target) = Town();
        var t = sim.Town;
        Win(sim, target);
        Assert.True(sim.SettleConquest(target.Id, ConquestChoice.Occupy));
        var o = Conquest.Held(t, target)!;
        var reg = t.Army.Regiments[0];
        Assert.True(sim.GarrisonSettlement(target.Id, reg.Id));
        Assert.True(Field.Away(t, reg));
        double r0 = o.Resistance;
        sim.RunDays(5);
        Assert.True(o.Tribute > 0);
        // Held down, the resistance grows slowly at most; left alone it rises and takes the place back.
        Assert.True(sim.GarrisonSettlement(target.Id, null));
        sim.RunDays(60);
        Assert.Null(Conquest.Held(t, target));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("takes itself back"));
    }

    [Fact]
    public void AConquestReloadsAndPlaysOnTheSame()
    {
        var (sim0, target) = Town();
        Win(sim0, target);
        sim0.SettleConquest(target.Id, ConquestChoice.Occupy);
        sim0.GarrisonSettlement(target.Id, sim0.Town.Army.Regiments[0].Id);
        var sim = SaveGame.Read(SaveGame.Write(sim0));
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        sim.RunDays(10);
        loaded.RunDays(10);
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
    }
}
