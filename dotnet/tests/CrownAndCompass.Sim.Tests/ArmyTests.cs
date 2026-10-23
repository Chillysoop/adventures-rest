using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ArmyTests
{
    private static readonly string[] Roles = ["foot", "pike", "ranged", "horse", "flier", "arcane", "beast", "construct"];

    /// <summary>A grown village with no stand-in mayor, a fat treasury and the halls of the kinds asked for.</summary>
    private static Simulation Town(params string[] halls)
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, Enemy = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 50000;
        foreach (var id in halls)
        {
            var d = t.Content.Building(id);
            var b = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false);
            b.BuildProgress = d.BuildHours;
        }
        return sim;
    }

    [Fact]
    public void EveryUnitIsWhole()
    {
        var c = Content.Default();
        var r = c.Army;
        Assert.Equal(r.Units.Count, r.Units.Select(u => u.Id).Distinct().Count());
        Assert.Single(r.Units, u => u.IsLevy);
        Assert.Equal(r.Levy.Unit, r.Units.Single(u => u.IsLevy).Id);
        Assert.True(r.Units.Count >= 20);
        foreach (var u in r.Units)
        {
            Assert.Contains(u.Role, Roles);
            Assert.All(u.Beats, b => Assert.Contains(b, Roles));
            Assert.True(u.Strength > 0 && u.Drill > 0, u.Id);
            Assert.NotEqual("", u.Blurb);
            Assert.All(u.Halls, h => Assert.True(c.Building(h).Trains > 0, $"{u.Id}: {h} trains nobody"));
            if (u.Tier != "") Assert.Contains(c.City.Tiers, t => t.Id == u.Tier);
            foreach (var need in u.Needs)
            {
                var (kind, id) = (need[..need.IndexOf(':')], need[(need.IndexOf(':') + 1)..]);
                Assert.True(kind switch
                {
                    "institution" => c.Institutions.Def(id) != null,
                    "ally" => c.World.Peoples.Any(p => p.Id == id),
                    "building" => c.Buildings.Any(b => b.Id == id),
                    _ => false,
                }, $"{u.Id}: {need}");
            }
        }
        // Enough beyond the basics: what institutions and allies teach.
        Assert.True(r.Units.Count(u => u.Needs.Any(n => n.StartsWith("institution:"))) >= 6);
        Assert.True(r.Units.Count(u => u.Needs.Any(n => n.StartsWith("ally:"))) >= 6);
    }

    [Fact]
    public void ARegimentTakesTownsfolkIntoItsHallDrillsAndHoldsTheWalls()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        double before = Threat.Defense(t);
        double gold = t.Treasury;
        var r = sim.RaiseRegiment("swordsmen", 10)!;
        Assert.NotNull(r);
        Assert.Equal("1st Swordsmen", r.Name);
        Assert.Equal(t.Content.Army.RecruitPerDay, r.Soldiers.Count);
        Assert.True(t.Treasury < gold);
        var hall = t.Building(r.HallId!.Value);
        Assert.All(r.Soldiers, id =>
        {
            Assert.Equal(hall.Id, t.Agent(id).JobId);
            Assert.Contains(id, hall.Workers);
            Assert.Equal(r.Id, t.Agent(id).RegimentId);
        });
        sim.RunDays(3);
        Assert.Equal(10, r.Soldiers.Count);
        Assert.True(r.Drill > 0);
        Assert.True(Threat.Defense(t) > before);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Army, 0) < 0);

        // In the working hours they drill at the hall.
        for (int h = 0; h < 10; h++) sim.Step();
        Assert.Contains(r.Soldiers, id => t.Agent(id).Activity == Activity.Drilling && t.Agent(id).LocationId == hall.Id);

        // Drill brings them up towards full strength (a raw recruit filling a gap dilutes it a little).
        double raw = Army.Strength(t, r);
        sim.RunDays(25);
        Assert.True(r.Drill > 0.7, $"drill {r.Drill}");
        Assert.True(Army.Strength(t, r) > raw * 1.3);

        // The walls are where they count most in a siege.
        double reserve = Army.AtWalls(t, r);
        sim.PostRegiment(r.Id, ArmyPost.Walls);
        Assert.True(Army.AtWalls(t, r) > reserve);

        // Stood down, they go home to look for a trade.
        var ids = r.Soldiers.ToList();
        Assert.True(sim.DisbandRegiment(r.Id));
        Assert.Empty(t.Army.Regiments);
        Assert.All(ids, id => Assert.Null(t.Agent(id).RegimentId));
        Assert.DoesNotContain(hall.Workers, ids.Contains);
    }

    [Fact]
    public void HallsAndAlliesAndInstitutionsGateTheUnits()
    {
        var sim = Town();
        var t = sim.Town;
        Assert.Equal("needs a Drill Yard", Army.RaiseBlocked(t, "swordsmen", 10));
        Assert.Null(sim.RaiseRegiment("swordsmen", 10));
        Assert.StartsWith("needs the Griffon Roost", Army.Locked(t, Army.Unit(t, "griffon-riders")) ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.Equal("needs an alliance with dwarves", Army.Locked(t, Army.Unit(t, "hold-shields")));
        Assert.Equal("call the levy instead", Army.RaiseBlocked(t, "levy", 10));

        var sim2 = Town("drill_yard");
        var t2 = sim2.Town;
        // A drill yard holds thirty.
        Assert.NotNull(sim2.RaiseRegiment("swordsmen", 20));
        Assert.Equal("no hall has room", Army.RaiseBlocked(t2, "spearmen", 20));
        Assert.NotNull(sim2.RaiseRegiment("spearmen", 10));
        Assert.Equal("2", t2.Army.Regiments.Count.ToString());
        Assert.Equal("needs a Archery Range", Army.RaiseBlocked(t2, "archers", 10));
    }

    [Fact]
    public void PatrolsWatchTheStreetsAndTheGuardKeepsTheCouncilAlive()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        var r = sim.RaiseRegiment("swordsmen", 20, ArmyPost.Patrol)!;
        sim.RunDays(6);
        double watch = Council.WatchStrength(t);
        double raid = Army.RaidDefense(t);
        Assert.True(Army.StreetWatch(t) > 0);
        Assert.Equal(0, Army.Bodyguard(t));
        for (int h = 0; h < 10; h++) sim.Step();
        Assert.Contains(r.Soldiers, id => t.Agent(id).Activity == Activity.Patrolling);

        sim.PostRegiment(r.Id, ArmyPost.Guard);
        Assert.True(Council.WatchStrength(t) < watch);
        Assert.True(Army.RaidDefense(t) < raid);
        Assert.True(Army.Bodyguard(t) > 0.3);
    }

    [Fact]
    public void TheTownIsFoundedWithTheMayorsKeepAndTheGuardStandsThere()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        var keep = Army.Keep(t);
        Assert.NotNull(keep);
        Assert.Single(t.Buildings, b => b.Def.Id == "keep");
        Assert.NotNull(Monuments.Next(t, keep!));
        var r = sim.RaiseRegiment("swordsmen", 12, ArmyPost.Guard)!;
        sim.RunDays(4);
        for (int h = 0; h < 10; h++) sim.Step();
        Assert.Contains(r.Soldiers, id => t.Agent(id).Activity == Activity.Guarding && t.Agent(id).LocationId == keep!.Id);
    }

    [Fact]
    public void AHeroLeadsARegimentAndStaysHome()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        var r = sim.RaiseRegiment("swordsmen", 12)!;
        sim.RunDays(3);
        // Companies may all be out on a trip: wait for a hero to come home.
        bool Free(Agent h) => !h.IsAway && Army.CommandBlocked(t, r, h) == null;
        for (int d = 0; d < 40 && !t.Heroes.Any(Free); d++) sim.RunDays(1);
        var hero = t.Heroes.Where(Free).OrderByDescending(h => h.Hero!.Level).First();
        double plain = Army.Strength(t, r);
        Assert.True(sim.CommandRegiment(r.Id, hero.Id));
        Assert.Equal(HeroDuty.Command, hero.Hero!.Duty);
        Assert.False(Quests.Available(hero));
        Assert.True(Army.Strength(t, r) > plain);
        Assert.Equal("already in command", Army.CommandBlocked(t, r, hero));
        Assert.True(sim.CommandRegiment(r.Id, null));
        Assert.Equal(HeroDuty.None, hero.Hero.Duty);
        Assert.Null(r.CommanderId);
    }

    [Fact]
    public void TheLevyAnswersTheLaw()
    {
        var sim = Town();
        var t = sim.Town;
        int volunteers = Army.LevyRoom(t);
        Assert.True(volunteers <= Army.LevyCap(t));
        Assert.Equal(ServiceLaw.Volunteers, t.Army.Service);

        // Conscription calls more, at a cost to the mayor's mandate.
        double mandate = t.Mandate;
        Assert.True(sim.SetServiceLaw(ServiceLaw.Conscription));
        Assert.True(t.Mandate < mandate || mandate == 0);
        Assert.True(Army.LevyRoom(t) >= volunteers);
        int room = Army.LevyRoom(t);
        Assert.True(room > 0);

        var levy = sim.RaiseLevy(room)!;
        Assert.NotNull(levy);
        Assert.Equal(room, levy.Soldiers.Count);
        Assert.Equal(0, Army.LevyRoom(t));
        // They keep their trades, but spend the working hours on the walls.
        var called = levy.Soldiers.Select(t.Agent).ToList();
        var jobs = called.ToDictionary(a => a.Id, a => a.JobId);
        for (int h = 0; h < 10; h++) sim.Step();
        Assert.All(called, a => Assert.NotEqual(Activity.Working, a.Activity));
        Assert.Contains(called, a => a.Activity == Activity.Guarding);
        Assert.All(called, a => Assert.Equal(jobs[a.Id], a.JobId));

        // Unpaid conscripts are the unhappiest townsfolk in town.
        sim.SetLevyPay(false);
        Assert.True(Army.Mood(t, called[0]) < 0);
        Assert.Equal(0, Army.Wage(t, levy));

        Assert.True(sim.DisbandRegiment(levy.Id));
        Assert.All(called, a => Assert.Null(a.RegimentId));
    }

    [Fact]
    public void UnpaidSoldiersGoHome()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        var r = sim.RaiseRegiment("swordsmen", 10)!;
        sim.RunDays(3);
        Assert.Equal(10, r.Soldiers.Count);
        t.Treasury = 0;
        sim.RunDays(1);
        Assert.True(r.Soldiers.Count < 10);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("hand back their arms"));
    }

    [Fact]
    public void SoldiersFallFirstInASiege()
    {
        var sim = Town("drill_yard");
        var t = sim.Town;
        var r = sim.RaiseRegiment("swordsmen", 30, ArmyPost.Walls)!;
        sim.RunDays(8);
        int n = r.Soldiers.Count;
        var dead = Army.Bloodied(t, Threat.Defense(t) * 0.6, Threat.Defense(t));
        Assert.NotEmpty(dead);
        Assert.Equal(n - dead.Count, r.Soldiers.Count);
        Assert.Equal(dead.Count, r.Fallen);
        Assert.All(dead, a => Assert.True(a.Dead));
    }

    [Fact]
    public void AnArmyReloadsAndPlaysOnTheSame()
    {
        var sim = Town("drill_yard", "archery_range");
        var t = sim.Town;
        sim.RaiseRegiment("swordsmen", 12, ArmyPost.Patrol);
        sim.RaiseRegiment("archers", 10, ArmyPost.Walls);
        sim.RaiseLevy(5);
        sim.RunDays(2);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        sim.RunDays(10);
        loaded.RunDays(10);
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        Assert.Equal(3, loaded.Town.Army.Regiments.Count);
    }
}
