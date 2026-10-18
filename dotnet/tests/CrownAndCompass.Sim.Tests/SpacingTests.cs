using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Xunit;
using Xunit.Abstractions;

namespace CrownAndCompass.Sim.Tests;

public class SpacingTests(ITestOutputHelper output)
{
    /// <summary>Pairs of buildings whose footprints, each grown by <paramref name="gap"/> tiles on every side, overlap.</summary>
    public static List<(Building A, Building B)> Crowded(Town t, double gap)
    {
        var boxes = t.Buildings.Select(b => (B: b, L: t.Lots[b.LotId])).ToList();
        var hits = new List<(Building, Building)>();
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                // Dense land builds wall to wall: there the footprints may touch but never cross.
                double g = Dense(t, boxes[i].L) || Dense(t, boxes[j].L) ? 0 : gap;
                if (Land.Overlap(boxes[i].L.X, boxes[i].L.Y, boxes[i].L.W, boxes[i].L.D, boxes[i].L.Yaw, boxes[j].L.X, boxes[j].L.Y, boxes[j].L.W, boxes[j].L.D, boxes[j].L.Yaw, g)) hits.Add((boxes[i].B, boxes[j].B));
            }
        return hits;
    }

    private static bool Dense(Town t, Lot l) => Density.Lane(t, t.Plan.Density[(int)(l.Y * Land.Res) * Land.Size + (int)(l.X * Land.Res)]) == 0;

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void NoTwoBuildingsStandCloserThanTheGapAcrossALongCampaign(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(450);
        var t = sim.Town;
        // A hair under the gap, for rounding.
        var crowded = Crowded(t, t.Content.Rules.BuildingGap - 0.01);
        foreach (var (a, b) in crowded.Take(10))
            output.WriteLine($"{a.Def.Id} #{a.Id} and {b.Def.Id} #{b.Id} stand too close");
        Assert.Empty(crowded);
        Assert.True(t.Content.Rules.BuildingGap > 0);
    }

    [Fact]
    public void TwoTurnedFootprintsThatCrossOnlyAtTheCornersStillCountAsTouching()
    {
        // Side by side a tile apart, the second turned an eighth: the corners reach into each other.
        Assert.True(Land.Overlap(10, 10, 4, 4, 0, 11.05, 10, 4, 4, Math.PI / 4, 0));
        Assert.False(Land.Overlap(10, 10, 4, 4, 0, 11.5, 10, 4, 4, 0, 0.25));
        Assert.True(Land.Overlap(10, 10, 4, 4, 0, 11.2, 10, 4, 4, 0, 0.25));
    }
}
