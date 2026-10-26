using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Timber and stone come from woods and rock: near the town, or down cart roads that raiders can cut.</summary>
public class WorkingsTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(11)]
    [InlineData(12)]
    public void TheDefaultStartHasWoodsAndRockCloseBy(int seed)
    {
        var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
        var near = Workings.Rules(t).Near;
        Assert.True(Workings.Nearest(t, Good.Timber) is { } wood && wood.Days <= near);
        Assert.True(Workings.Nearest(t, Good.Stone) is { } rock && rock.Days <= near);
        var cells = Workings.Cells(t);
        Assert.Contains(cells, c => c == Workings.Woods);
        Assert.Contains(cells, c => c == Workings.Rock);
    }

    [Fact]
    public void NothingIsBuiltOrZonedWhereTheWoodsReachIn()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, Enemy = false });
        var t = sim.Town;
        var cells = Workings.Cells(t);
        int i = Array.FindIndex(cells, c => c != 0);
        Assert.True(i >= 0);
        Assert.False(Land.Open(t, i));
        double x = (i % Land.Size + 0.5) / Land.Res, y = (i / Land.Size + 0.5) / Land.Res;
        Assert.False(Land.Fits(t, x, y, 4, 4, 0, null));
        sim.RunDays(60);
        foreach (var b in t.Buildings.Where(b => b.LotId < t.Lots.Count))
            foreach (int c in Land.Cells(t.Lot(b.LotId).X, t.Lot(b.LotId).Y, t.Lot(b.LotId).W, t.Lot(b.LotId).D, t.Lot(b.LotId).Yaw))
                if (c >= 0) Assert.Equal(0, cells[c]);
    }

    [Fact]
    public void AFarCampSendsItsLoadsHomeByCartAndPatrolsKeepTheRoad()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, Enemy = false });
        var t = sim.Town;
        var road = new Working(Good.Timber, 2, 0, 2);
        Assert.True(Workings.Afar(t, road));
        double open = Workings.RaidChance(t, road);
        Assert.True(open > 0);
        var cells = new List<(int X, int Y)>();
        for (double d = 0; d <= 2.2; d += 0.1)
            foreach (double off in new[] { -0.25, 0, 0.25 })
                cells.Add(Wilds.CellOf(t, d, off));
        t.Treasury = 100000;
        sim.PaintWilds(Wilds.Patrol, cells.Distinct().ToList());
        Assert.True(Workings.Patrolled(t, road) > 0.3);
        Assert.True(Workings.RaidChance(t, road) < open);
    }
}
