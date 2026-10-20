using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class CliffsTests
{
    private static Town Fresh(int seed) => new Simulation(new SimulationOptions { Seed = seed }).Town;

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9323)]
    public void PlateausStandRoundTheTownOutsideItsPlanEachWithARamp(int seed)
    {
        var t = Fresh(seed);
        var all = Cliffs.Of(t);
        Assert.True(all.Count >= 2, $"only {all.Count} plateaus");
        foreach (var b in Cliffs.Barriers(t))
        {
            Assert.NotEmpty(b.Openings);
            Assert.All(b.Loop, p => Assert.True(p.X < 0 || p.Y < 0 || p.X > Town.Grid || p.Y > Town.Grid, $"({p.X:0.0}, {p.Y:0.0}) is in the plan"));
        }
        // Sheer at the edge, level on top, the ground at its foot untouched.
        var top = all[0];
        Assert.Equal(top.H, Cliffs.Height(t, top.X, top.Y), 6);
        double a = top.Ramps[0] + Math.PI / 2, e = top.EdgeAt(a);
        Assert.Equal(0, Cliffs.Height(t, top.X + Math.Cos(a) * (e + 1), top.Y + Math.Sin(a) * (e + 1)), 6);
    }

    [Fact]
    public void NothingIsEverBuiltOnOrUnderACliff()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 9323 });
        sim.RunDays(400);
        var t = sim.Town;
        foreach (var b in t.Buildings)
        {
            var lot = t.Lot(b.LotId);
            Assert.All(Cliffs.Barriers(t), c => Assert.True(!Passage.Inside(c.Edge, lot.X, lot.Y), $"building {b.Id} stands on plateau"));
            Assert.Equal(0, Cliffs.Height(t, lot.X, lot.Y), 9);
        }
    }

    [Fact]
    public void ARampClimbsSteadilyFromItsFootToTheTop()
    {
        var t = Fresh(2);
        var p = Cliffs.Of(t)[0];
        var o = Cliffs.Barriers(t)[0].Openings[0];
        double last = -1;
        for (int i = 0; i <= 10; i++)
        {
            double x = o.Out.X + (o.In.X - o.Out.X) * i / 10, y = o.Out.Y + (o.In.Y - o.Out.Y) * i / 10, h = Cliffs.Height(t, x, y);
            Assert.True(h >= last - 1e-9, $"the ramp drops at step {i}");
            last = h;
        }
        Assert.Equal(p.H, last, 6);
    }

    private static List<(double X, double Y)> Walk(Town t, (double X, double Y) from, (double X, double Y) to)
    {
        var steps = new List<(double X, double Y)> { from };
        var cur = from;
        for (int i = 0; i < 8000 && Math.Abs(cur.X - to.X) + Math.Abs(cur.Y - to.Y) > 1e-6; i++)
        {
            var aim = Passage.Next(t, cur, to);
            double dx = aim.X - cur.X, dy = aim.Y - cur.Y, len = Math.Sqrt(dx * dx + dy * dy);
            cur = len <= 0.05 ? aim : (cur.X + dx / len * 0.05, cur.Y + dy / len * 0.05);
            steps.Add(cur);
        }
        return steps;
    }

    private static double SegDist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, s = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(a.X + dx * s - p.X, 2) + Math.Pow(a.Y + dy * s - p.Y, 2));
    }

    /// <summary>Whether a step is on a cliff face anywhere but a ramp.</summary>
    private static bool OnFace(Town t, (double X, double Y) p) =>
        Cliffs.Barriers(t).Any(b => Passage.OnBarrier(b, p) && !b.Openings.Any(o => SegDist(p, o.In, o.Out) < 1.6));

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void WalkersGoUpByTheRampsAndRoundThePlateausNeverOverTheCliffs(int seed)
    {
        var t = Fresh(seed);
        double c = Town.Center;
        foreach (var p in Cliffs.Of(t))
        {
            // Up on top from the town, and from the town to the far side of the plateau.
            double a = Math.Atan2(p.Y - c, p.X - c), far = Math.Sqrt((p.X - c) * (p.X - c) + (p.Y - c) * (p.Y - c)) + p.R * 1.6 + 3;
            var start = (c + Math.Cos(a) * 26, c + Math.Sin(a) * 26);
            foreach (var goal in new[] { (p.X, p.Y), (c + Math.Cos(a) * far, c + Math.Sin(a) * far) })
            {
                var walk = Walk(t, start, goal);
                Assert.True(Math.Abs(walk[^1].X - goal.Item1) + Math.Abs(walk[^1].Y - goal.Item2) < 1e-6, $"stuck at ({walk[^1].X:0.00}, {walk[^1].Y:0.00}) on the way to ({goal.Item1:0.0}, {goal.Item2:0.0})");
                var bad = walk.FirstOrDefault(q => OnFace(t, q));
                Assert.True(bad == default, $"({bad.X:0.00}, {bad.Y:0.00}) is on a cliff face, on the way to ({goal.Item1:0.0}, {goal.Item2:0.0})");
            }
        }
    }

    [Fact]
    public void RoadsBendRoundThePlateausForThoseWhoCannotClimb()
    {
        var t = Fresh(1);
        double c = Town.Center;
        foreach (var p in Cliffs.Of(t))
        {
            var b = Cliffs.Barriers(t)[p.Id];
            double a = Math.Atan2(p.Y - c, p.X - c);
            var road = Passage.Road(t, (0, 0), (Math.Cos(a) * 0.6, Math.Sin(a) * 0.6));
            var tiles = road.Select(w => Passage.FromDays(w.X, w.Y)).ToList();
            for (int i = 1; i < tiles.Count; i++)
                for (int k = 0; k <= 40; k++)
                {
                    var q = (tiles[i - 1].X + (tiles[i].X - tiles[i - 1].X) * k / 40, tiles[i - 1].Y + (tiles[i].Y - tiles[i - 1].Y) * k / 40);
                    Assert.False(Passage.Inside(b.Loop, q.Item1, q.Item2), $"the road crosses plateau {p.Id} at ({q.Item1:0.0}, {q.Item2:0.0})");
                }
        }
    }
}
