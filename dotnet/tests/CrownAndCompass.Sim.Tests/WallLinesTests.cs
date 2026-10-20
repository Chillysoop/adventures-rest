using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class WallLinesTests
{
    private static (Simulation Sim, Town T) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = false, AutoBounties = false, Enemy = false });
        return (sim, sim.Town);
    }

    private static (double X, double Y)[] Square(double half)
    {
        double c = CrownAndCompass.Sim.Town.Center;
        return [(c - half, c - half), (c + half, c - half), (c + half, c + half), (c - half, c + half)];
    }

    [Fact]
    public void TheWallRunsWhereThePlayerDrewIt()
    {
        var (sim, t) = Town();
        double c = CrownAndCompass.Sim.Town.Center;
        Assert.Null(sim.DrawWall(0, Square(10)));
        // Due east the line stands 10 tiles out, and its corner further.
        Assert.Equal(10, City.Wall(t, 0, 0), 3);
        Assert.True(City.Wall(t, 0, Math.PI / 4) > 13.5);
        Assert.True(City.OnWallStrip(t, c + 10, c));
        Assert.False(City.OnWallStrip(t, c + 7, c));
    }

    [Fact]
    public void AWallMustRingTheHallAndMissEveryBuilding()
    {
        var (sim, t) = Town();
        double c = CrownAndCompass.Sim.Town.Center;
        Assert.Equal("a wall must go right round the town hall", sim.DrawWall(0, [(c + 6, c + 6), (c + 12, c + 6), (c + 12, c + 12)]));
        Assert.Equal("it runs too close to the town hall", sim.DrawWall(0, Square(3)));
        // A square whose side runs straight through a standing building.
        t.AddBuilding(t.Content.Building("shrine"), new Lot(t.Lots.Count, c + 9, c + 1) { W = 3, D = 3, Zone = Zone.Civic }, null, "the Shrine", true);
        Assert.Equal("it runs through the Shrine", sim.DrawWall(0, Square(9)));
        Assert.Null(sim.DrawWall(0, Square(11)));
    }

    [Fact]
    public void NoGateNoWall()
    {
        var (sim, t) = Town();
        t.Treasury = 5000;
        Assert.Null(sim.DrawWall(0, Square(10)));
        Assert.Equal("needs a gate: place one on the wall's line", Threat.BuildBlocked(t, DefenseWork.Palisade));
        Assert.False(sim.BuildDefense(DefenseWork.Palisade));
        double c = CrownAndCompass.Sim.Town.Center;
        Assert.Equal("a gate goes on the wall's line", sim.PlaceGate(0, c + 4, c));
        Assert.Null(sim.PlaceGate(0, c + 9.6, c + 0.3));
        Assert.Equal("it's too close to another gate", sim.PlaceGate(0, c + 10, c + 1.5));
        Assert.True(sim.BuildDefense(DefenseWork.Palisade));
        Assert.Equal("every wall needs a gate", sim.RemoveGate(0, 0));
    }

    [Fact]
    public void ABuiltWallOpensAtEveryGateItsBuilderPut()
    {
        var (sim, t) = Town();
        t.Treasury = 5000;
        double c = CrownAndCompass.Sim.Town.Center;
        sim.DrawWall(0, Square(10));
        sim.PlaceGate(0, c + 10, c);
        sim.PlaceGate(0, c - 10, c);
        t.Market.Seed(Good.Timber, 500);
        Assert.True(sim.BuildDefense(DefenseWork.Palisade));
        for (int d = 0; d < 60 && t.Defenses.Palisade != PalisadeState.Built; d++) sim.RunDays(1);
        Assert.Equal(PalisadeState.Built, t.Defenses.Palisade);
        var wall = Assert.Single(Passage.Barriers(t));
        Assert.Equal(2, wall.Openings.Count);
        Assert.Empty(Passage.Sealed(t));
        Assert.Contains(wall.Openings, o => Math.Abs(o.Out.Y - c) < 1 && o.Out.X > c + 10);
    }

    [Fact]
    public void AWallWaitsWhenTheMarketHasNoTimber()
    {
        var (sim, t) = Town();
        t.Treasury = 5000;
        double c = CrownAndCompass.Sim.Town.Center;
        sim.DrawWall(0, Square(10));
        sim.PlaceGate(0, c, c + 10);
        Assert.True(sim.BuildDefense(DefenseWork.Palisade));
        int ready = t.Defenses.PalisadeReadyTick!.Value;
        t.Market.Take(Good.Timber, 1e9);
        Threat.Daily(t, 1, autoMayor: false, enemy: false);
        Assert.True(t.Defenses.PalisadeReadyTick > ready);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "stalled:walls");
    }

    [Fact]
    public void TheStandInMayorKeepsTheSouthGate()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(200);
        var t = sim.Town;
        if (WallLines.Begun(t, 0)) Assert.NotEmpty(WallLines.Gates(t, 0));
    }
}
