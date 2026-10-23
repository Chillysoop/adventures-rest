using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class RoadsTests
{
    [Fact]
    public void HardCountryLiesBetweenTheSitesNeverOnThem()
    {
        for (int seed = 1; seed <= 8; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            Assert.True(t.Terrain.Count >= t.Regions.Count / 2, $"seed {seed}: only {t.Terrain.Count} features");
            foreach (var s in t.Sites)
            {
                var (x, y) = World.Spot(t, s);
                // The ocean's sites are its islands, out in the water.
                Assert.DoesNotContain(t.Terrain, f => f.Kind.Id != "ocean" && Roads.Contains(f, x, y));
            }
        }
    }

    [Fact]
    public void TheMarchRunsDawnToDuskWithANoonRest()
    {
        var c = Content.Default();
        // Setting out at midnight, ten hours of march: 6 to 11 and 12 to 17.
        Assert.Equal(17, Roads.Finish(c, 0, 10), 6);
        Assert.Equal(24 + 7, Roads.Finish(c, 0, 11), 6);
        Assert.Equal(10, Roads.Marched(c, 0, 24), 6);
        Assert.Equal(24 + 6, Roads.WakeAfterRest(c, 20), 6);
    }

    /// <summary>How far a point inside a feature's outline is from its edge.</summary>
    private static double Inside(Feature f, double x, double y)
    {
        var s = Country.Outline(f);
        double best = double.MaxValue;
        for (int i = 0, j = s.Count - 1; i < s.Count; j = i++)
        {
            double dx = s[i].X - s[j].X, dy = s[i].Y - s[j].Y, len = dx * dx + dy * dy;
            double u = len == 0 ? 0 : Math.Clamp(((x - s[j].X) * dx + (y - s[j].Y) * dy) / len, 0, 1);
            best = Math.Min(best, Math.Sqrt(Math.Pow(s[j].X + u * dx - x, 2) + Math.Pow(s[j].Y + u * dy - y, 2)));
        }
        return best;
    }

    [Fact]
    public void ADetourWalksRoundTheCountryAndCrossingGoesThroughIt()
    {
        for (int seed = 1; seed <= 20; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            var t = sim.Town;
            for (int day = 0; day < 120; day++)
            {
                sim.RunDays(1);
                foreach (var p in t.Parties.Where(p => p.DepartTick == t.Tick && p.Out.Any(w => w.Way == "skirt")))
                {
                    // Every stretch of a road around a feature stays out of it.
                    var cur = (X: 0.0, Y: 0.0);
                    bool fromCrossing = false;
                    foreach (var w in p.Out)
                    {
                        // A detour bent back over the river takes the nearest crossing first; the river decides that step, not the feature.
                        if (w.Way == "skirt" && !fromCrossing)
                            for (int k = 1; k < 10; k++)
                            {
                                double x = cur.X + (w.X - cur.X) * k / 10, y = cur.Y + (w.Y - cur.Y) * k / 10;
                                // The land is a grid of cells owned by their centres, so a road beside country may clip its outline by half a cell.
                                Assert.False(Roads.Contains(w.Feature!, x, y) && Inside(w.Feature!, x, y) > t.Content.Roads.Country.Cell / 2, $"seed {seed}: the {p.Name} walk into {w.Feature!.Name}");
                            }
                        cur = (w.X, w.Y);
                        fromCrossing = w.Crossing != "";
                    }
                    Assert.NotEmpty(p.Said);
                    return;
                }
            }
        }
        Assert.Fail("No party went the long way round in 20 seeds");
    }

    [Fact]
    public void APartyIsSettledWhenItReachesTheSiteAndHomeLater()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        Party? party = null;
        // The town may call a party home before it gets there: follow the first that goes all the way.
        for (int day = 0; day < 200 && party == null; day++)
        {
            sim.RunDays(1);
            var leaving = t.Parties.FirstOrDefault(p => !p.Resolved && p.RecalledTick == null && p.ArriveTick > t.Tick);
            if (leaving == null) continue;
            while (t.Tick < leaving.ArriveTick && leaving.RecalledTick == null) sim.Step();
            if (leaving.RecalledTick == null) party = leaving;
        }
        Assert.NotNull(party);
        sim.Step();
        Assert.True(party!.Resolved);
        Assert.NotNull(party.Result);
        Assert.True(party.ReturnTick >= party.LeaveTick && party.LeaveTick >= party.ArriveTick);
        // The town hears nothing until they are back.
        Assert.DoesNotContain(t.Chronicles, c => c.PartyName == party.Name && c.DepartDay == SimTime.DayOf(party.DepartTick));
        while (!party.Returned) sim.Step();
        Assert.True(t.Tick >= party.ReturnTick);
        Assert.Contains(t.Chronicles, c => c.PartyName == party.Name && c.DepartDay == SimTime.DayOf(party.DepartTick));
    }
}
