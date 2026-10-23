using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FieldTests
{
    /// <summary>A grown village with two drilled regiments and the warband on the road, two days out.</summary>
    private static Simulation Town(double foe, int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false, Enemy = false, AutoStories = false });
        sim.RunDays(30);
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "town");
        t.Treasury = 50000;
        foreach (var id in new[] { "drill_yard", "stables" })
        {
            var d = t.Content.Building(id);
            var b = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false);
            b.BuildProgress = d.BuildHours;
        }
        foreach (var (unit, size) in new[] { ("swordsmen", 30), ("outriders", 16) })
        {
            var r = sim.RaiseRegiment(unit, size, ArmyPost.Walls)!;
            Army.Fill(t, r);
            r.Drill = 1;
        }
        t.Enemy.Stage = ThreatStage.Marching;
        t.Enemy.Strength = foe;
        t.Enemy.ArrivalTick = t.Tick + 48;
        return sim;
    }

    private static void RunTo(Simulation sim, int tick)
    {
        while (sim.Town.Tick <= tick) sim.Step();
    }

    [Fact]
    public void OrdersAndGoalsAreWhole()
    {
        var r = Content.Default().Army.Field;
        Assert.Contains(r.Orders, o => o.Id == "hold");
        Assert.Contains(r.Goals, g => g.Id == "leader");
        Assert.All(r.Orders, o => Assert.NotEqual("", o.Blurb));
        Assert.Contains("warband", r.FoeRoles.Keys);
    }

    [Fact]
    public void TheHostMarchesOutBreaksAWeakFoeAndComesHome()
    {
        var sim = Town(25);
        var t = sim.Town;
        Assert.Single(Field.Foes(t));
        Assert.Equal("pick who marches", Field.MarchBlocked(t, "warband"));
        foreach (var r in t.Army.Regiments) Assert.True(sim.SetMarches(r.Id, true));
        double home = Threat.Defense(t);
        var s = sim.MarchOut("warband", "advance")!;
        Assert.NotNull(s);
        Assert.Equal("the host is out", Field.MarchBlocked(t, "warband"));
        // Gone from the walls while they're out.
        Assert.True(Threat.Defense(t) < home);
        sim.Step();
        Assert.All(t.Army.Regiments.SelectMany(r => r.Soldiers), id => Assert.Equal(Activity.Marching, t.Agent(id).Activity));

        RunTo(sim, s.MeetTick);
        // Fought; the host marches the road home and is away until then.
        Assert.NotNull(s.ReturnTick);
        Assert.True(Threat.Defense(t) < home);
        RunTo(sim, s.ReturnTick!.Value);
        Assert.Null(t.Army.Sortie);
        Assert.True(Threat.Defense(t) > Threat.Defense(t, false) - 1);
        var b = Assert.Single(t.Army.Fields);
        Assert.True(b.End == FieldEnd.Won, string.Join("\n", b.Lines));
        Assert.Null(t.Enemy.ArrivalTick);
        Assert.NotEqual(ThreatStage.Marching, t.Enemy.Stage);
        Assert.Equal(t.Content.Army.Field.Rounds >= b.Theirs.Left.Count ? b.Theirs.Left.Count : -1, b.Ours[0].Left.Count);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Victory in the field"));
    }

    [Fact]
    public void AStrongFoeBreaksTheHostAndMarchesOn()
    {
        var sim = Town(2000);
        var t = sim.Town;
        foreach (var r in t.Army.Regiments) sim.SetMarches(r.Id, true);
        int men = t.Army.Regiments.Sum(r => r.Soldiers.Count);
        var s = sim.MarchOut("warband", "charge")!;
        RunTo(sim, s.MeetTick);
        var b = Assert.Single(t.Army.Fields);
        Assert.Contains(b.End, new[] { FieldEnd.Lost, FieldEnd.Drawn });
        Assert.Equal(ThreatStage.Marching, t.Enemy.Stage);
        Assert.True(t.Enemy.Strength < 2000);
        Assert.True(t.Army.Regiments.Sum(r => r.Soldiers.Count) < men);
        Assert.NotEmpty(b.Fallen);
    }

    [Fact]
    public void HeroesTakeGoalsAndComeBackToTheirOwnLives()
    {
        // A foe the host can stand against past the first round, when the heroes go for their goals.
        var sim = Town(100);
        var t = sim.Town;
        foreach (var r in t.Army.Regiments) sim.SetMarches(r.Id, true);
        var hero = Field.FreeHeroes(t).First();
        Assert.True(sim.SetFieldGoal(hero.Id, "banner"));
        Assert.False(sim.SetFieldGoal(hero.Id, "nonsense"));
        var s = sim.MarchOut("warband", "hold")!;
        Assert.Equal(HeroDuty.Field, hero.Hero!.Duty);
        Assert.False(Quests.Available(hero));
        RunTo(sim, s.MeetTick);
        RunTo(sim, s.ReturnTick!.Value);
        var b = Assert.Single(t.Army.Fields);
        Assert.Contains(b.Goals, g => g.HeroId == hero.Id && g.Goal == "banner");
        Assert.Contains(b.Ours, u => u.Kind == "heroes");
        if (!hero.Dead) Assert.Equal(HeroDuty.None, hero.Hero.Duty);
    }

    [Fact]
    public void TooLateToMarchAndCalledBack()
    {
        var sim = Town(60);
        var t = sim.Town;
        foreach (var r in t.Army.Regiments) sim.SetMarches(r.Id, true);
        t.Enemy.ArrivalTick = t.Tick + 4;
        Assert.Equal("too close to the walls", Field.MarchBlocked(t, "warband"));
        t.Enemy.ArrivalTick = t.Tick + 48;
        Assert.NotNull(sim.MarchOut("warband", "flank"));
        Assert.True(sim.SetFieldOrder("hold"));
        Assert.Equal("hold", t.Army.Sortie!.Order);
        Assert.True(sim.RecallHost());
        Assert.Null(t.Army.Sortie);
        Assert.Empty(t.Army.Fields);
    }

    [Fact]
    public void AFieldReloadsAndPlaysOnTheSame()
    {
        // Loaded once first: a warband put on the road by hand is discovered on load.
        var sim = SaveGame.Read(SaveGame.Write(Town(120)));
        var t = sim.Town;
        foreach (var r in t.Army.Regiments) sim.SetMarches(r.Id, true);
        sim.SetFieldGoal(Field.FreeHeroes(t).First().Id, "flank");
        sim.MarchOut("warband", "advance");
        for (int h = 0; h < 5; h++) sim.Step();
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        sim.RunDays(3);
        loaded.RunDays(3);
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
        Assert.Single(loaded.Town.Army.Fields);
    }
}
