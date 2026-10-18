using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GarrisonTests
{
    /// <summary>A town with a finished palisade and some of the watch, ready for orders.</summary>
    private static Simulation Walled(int watch = 8, int seed = 2)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoDefense = false, Enemy = false });
        sim.RunDays(10);
        var t = sim.Town;
        t.Defenses.Palisade = PalisadeState.Built;
        t.Watch = watch;
        Garrison.Sync(t);
        sim.RunDays(1);
        return sim;
    }

    [Fact]
    public void KeepsAGuardForEveryWatchmanWalkingTheirBeat()
    {
        var sim = Walled(watch: 9);
        var t = sim.Town;
        Assert.Equal(9, t.Garrison.Guards.Count);
        Assert.All(t.Garrison.Guards, g => Assert.True(Garrison.Arrived(t, g)));
        // Spread round the eight sections of wall.
        Assert.Equal(8, t.Garrison.Guards.Select(g => g.Beat).Distinct().Count());
        t.Watch = 5;
        Garrison.Sync(t);
        Assert.Equal(5, t.Garrison.Guards.Count);
    }

    [Fact]
    public void CallToArmsSendsTheNearestGuardsAndTheyArriveWithinHours()
    {
        var sim = Walled();
        var t = sim.Town;
        int north = 4;
        var post = sim.CallToArms(PostKind.Wall, north)!;
        Assert.NotNull(post);
        Assert.Equal(t.Content.Garrison.WallPost, Garrison.Sent(t, post));
        // The guard whose beat is the north wall is already there and goes first; the others come from next door.
        var sent = t.Garrison.Guards.Where(g => g.PostId == post.Id).Select(g => g.Beat).ToList();
        Assert.Contains(north, sent);
        Assert.DoesNotContain(0, sent);
        int before = Garrison.Manning(t, north);
        for (int h = 0; h < 6; h++) sim.Step();
        Assert.Equal(t.Content.Garrison.WallPost, Garrison.At(t, post));
        Assert.True(Garrison.Manning(t, north) > before);
        Assert.True(Garrison.SectionFactor(t, north) > Garrison.SectionFactor(t, 0));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The horn sounds on the north wall"));

        // Standing down sends them back to their beats.
        Assert.True(sim.StandDown(post.Id));
        Assert.Empty(t.Garrison.Posts);
        Assert.All(t.Garrison.Guards, g => Assert.Null(g.PostId));
    }

    [Fact]
    public void NoWallNoWatchNoCall()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoDefense = false, Enemy = false });
        Assert.Null(sim.CallToArms(PostKind.Wall, 3));
        sim.Town.Watch = 4;
        Assert.Equal("the town has no wall yet", Garrison.CallBlocked(sim.Town, PostKind.Wall, 3));
        Assert.Equal(1.0, Garrison.SectionFactor(sim.Town, 3));
        sim.Town.Defenses.Palisade = PalisadeState.Built;
        Assert.Equal("no such tower", Garrison.CallBlocked(sim.Town, PostKind.Tower, 0));
        Assert.NotNull(sim.CallToArms(PostKind.Gate, 0));
    }

    [Fact]
    public void AFlagDrawsRaidersOffWhatItCovers()
    {
        var sim = Walled(watch: 6);
        var t = sim.Town;
        var farm = t.Buildings.First(b => b.Def.Zone == Zone.Farmland);
        var lot = t.Lot(farm.LotId);
        var flag = sim.PlantFlag(lot.X, lot.Y, 3)!;
        Assert.NotNull(flag);
        for (int h = 0; h < 6; h++) sim.Step();
        Assert.Equal(3, Garrison.At(t, flag));
        Assert.True(Garrison.Covered(t, farm));
        Assert.True(Garrison.FlagFactor(t, farm) > 1.3);
        var marks = t.Buildings.Where(b => b.Complete && b.Def.Zone == Zone.Farmland).ToList();
        if (marks.Any(b => !Garrison.Covered(t, b))) Assert.DoesNotContain(farm, Garrison.Unguarded(t, marks));
        Assert.Equal([farm], Garrison.Unguarded(t, [farm]));
        // The watch holds only so many flags.
        for (int i = 0; i < 10; i++) sim.PlantFlag(Town.Center, Town.Center, 1);
        Assert.Equal(t.Content.Garrison.MaxFlags, t.Garrison.Posts.Count(p => p.Kind == PostKind.Flag));
    }

    [Fact]
    public void StoneWallsTakeAGatehouseMoatTowersAndBarracksThatHouseTheWatch()
    {
        var sim = Walled(watch: 12);
        var t = sim.Town;
        var d = t.Defenses;
        t.Treasury = 100000;
        Assert.Equal("needs the stone walls", Threat.BuildBlocked(t, DefenseWork.Moat));
        d.StoneWall = PalisadeState.Built;
        t.City.Tier = City.TierIndex(t, "town");
        double fort = Threat.Fortification(t);
        double wage = Garrison.Wages(t);
        foreach (var w in new[] { DefenseWork.Gatehouse, DefenseWork.Moat, DefenseWork.Barracks, DefenseWork.Tower, DefenseWork.Tower })
            Assert.True(sim.BuildDefense(w), Threat.BuildBlocked(t, w));
        sim.RunDays(12);
        Assert.Equal(PalisadeState.Built, d.Gatehouse);
        Assert.Equal(PalisadeState.Built, d.Moat);
        Assert.Equal(1, d.Barracks);
        Assert.Equal(2, d.Towers);
        Assert.True(Threat.Fortification(t) > fort);
        // All twelve live in the walls: eight in the barracks, two in each tower.
        Assert.Equal(12, Garrison.Housed(t));
        Assert.Equal(8, Garrison.Barracked(t));
        Assert.True(Garrison.Wages(t) < wage);
        Assert.True(Garrison.NightWatch(t) > t.Watch);
        Assert.True(Garrison.SectionFactor(t, 0) > 1.2);
        Assert.Equal(t.Content.Garrison.StoneTowers, Garrison.MaxTowers(t));
    }

    [Fact]
    public void ASiegeComesFromANamedSideAndTheStandInMayorMansIt()
    {
        foreach (int seed in new[] { 5, 3, 1, 2 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = false, AutoStories = false });
            var t = sim.Town;
            for (int d = 0; d < 400 && t.Enemy.Stage != ThreatStage.Marching && t.Outcome == null; d++) sim.RunDays(1);
            if (t.Enemy.Stage != ThreatStage.Marching || !Garrison.Walled(t) || t.Watch == 0) continue;
            var march = t.Ledger.Entries.Last(e => e.Text.Contains("is on the march"));
            Assert.Contains($"come at the {Garrison.Place(t, t.Enemy.Approach)}", march.Text);
            sim.RunDays(1);
            Assert.Contains(t.Garrison.Posts, p => p.Auto && (p.Index == t.Enemy.Approach || p.Kind == PostKind.Gate));
            Assert.Contains(Alerts.Standing(t), a => a.Wall == t.Enemy.Approach);
            return;
        }
        Assert.Fail("No seed marched on a walled town with a watch.");
    }
}
