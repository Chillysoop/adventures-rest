using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class WallStripTests
{
    /// <summary>Points over a lot's footprint, corners included, in tiles.</summary>
    private static IEnumerable<(double X, double Y)> Over(Lot l)
    {
        double hw = l.W / 2.0 / Land.Res, hd = l.D / 2.0 / Land.Res, c = Math.Cos(l.Yaw), s = Math.Sin(l.Yaw);
        for (int i = 0; i <= 4; i++)
            for (int j = 0; j <= 4; j++)
            {
                double u = -hw + 2 * hw * i / 4, v = -hd + 2 * hd * j / 4;
                yield return (l.X + u * c - v * s, l.Y + u * s + v * c);
            }
    }

    /// <summary>How far inside the strip a point is, in tiles (0 or less: off it).</summary>
    private static double Into(Town t, double x, double y)
    {
        var strip = t.Content.City.WallStrip;
        double dx = x - Town.Center, dy = y - Town.Center, dist = Math.Sqrt(dx * dx + dy * dy), a = Math.Atan2(dy, dx), most = 0;
        for (int ring = 0; ring < City.WallRings; ring++)
        {
            double off = dist - City.Wall(t, ring, a);
            most = Math.Max(most, Math.Min(off + strip.Inside, strip.Outside - off));
        }
        return most;
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void NothingIsBuiltWhereTheWallsRunOrWillGrow(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(450);
        var t = sim.Town;
        Assert.True(t.Buildings.Count > 100);
        // A footprint may graze the strip's edge by less than a cell, never stand in it.
        var on = t.Buildings.Where(b => Over(t.Lot(b.LotId)).Any(p => Into(t, p.X, p.Y) > 1.0 / Land.Res)).Select(b => $"{b.Def.Id} #{b.Id}").ToList();
        Assert.Empty(on);
    }

    [Fact]
    public void AClickOnTheWallLineSaysWhyAndTheWatchWalksTheSameLine()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        t.Treasury = 10000;
        // Due east, on the old town's wall line: nothing fits there, and a click either snaps off the strip or says why.
        double x = Town.Center + City.Wall(t, 0, 0), y = Town.Center;
        Assert.True(City.OnWallStrip(t, x, y));
        Assert.False(Land.Fits(t, x, y, 3, 3, 0, null));
        var granary = t.Content.Building("granary");
        if (Planning.Spot(t, granary, x, y) is { } lot) Assert.DoesNotContain(Over(lot), p => Into(t, p.X, p.Y) > 1.0 / Land.Res);
        else Assert.Contains("wall", Planning.SpotBlocked(t, granary, x, y) ?? "");
        // The palisade and the stone wall stand on one line, so an upgrade never moves the wall.
        for (int s = 0; s < Garrison.Sections(t); s++)
            Assert.Equal(City.Wall(t, 0, Math.PI / 2 + s * 2 * Math.PI / Garrison.Sections(t)), Garrison.Radius(t, s), 6);
        t.Defenses.CityWall = PalisadeState.Built;
        Assert.Equal(City.Wall(t, 1, Math.PI / 2), Garrison.Radius(t, 0), 6);
    }
}
