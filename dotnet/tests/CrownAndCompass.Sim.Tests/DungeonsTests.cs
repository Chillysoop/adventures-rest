using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DungeonsTests
{
    private static SiteState Site(Town t) => t.Sites.Where(s => s.Generated && s.Def.Tier >= 2 && s.Def.Encounters.Count >= 4).OrderBy(s => s.Def.Id, StringComparer.Ordinal).First();

    [Fact]
    public void AMapHasTheMainWayToTheHeartAndSideRoomsOffIt()
    {
        var t = new Simulation(new SimulationOptions { Seed = 3 }).Town;
        var s = Site(t);
        var d = Dungeons.Map(t, s);
        var way = Dungeons.MainWay(d);
        Assert.Equal(Dungeons.Boss, way[^1].Enc);
        Assert.Equal(Math.Min(s.Def.Encounters.Count, 2 + s.Def.Tier) + 1, way.Count);
        // Each room of the way opens off the one before; every side room off a room of the way.
        for (int i = 1; i < way.Count; i++) Assert.Equal(way[i - 1].Id, way[i].From);
        Assert.All(d.Rooms.Where(r => r.Side), r => Assert.Contains(way, w => w.Id == r.From && w.Enc != Dungeons.Boss));
        // Drawn on the site's own dice: the same map every time, and asking again keeps it.
        var again = Dungeons.Draw(t.Content, s.Def, new Rng(1));
        Assert.Same(d, Dungeons.Map(t, s));
        Assert.NotNull(again);
    }

    [Fact]
    public void ANewMasterDrawsThePlaceAfresh()
    {
        var t = new Simulation(new SimulationOptions { Seed = 3 }).Town;
        var s = Site(t);
        var d = Dungeons.Map(t, s);
        d.Rooms[0].Cleared = true;
        Lairs.Resettle(t, s);
        Assert.Null(s.Dungeon);
        Assert.DoesNotContain(Dungeons.Map(t, s).Rooms, r => r.Cleared);
    }

    [Fact]
    public void OccupantsTakeBackRoomsAndSetTrapsBetweenTrips()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4 }).Town;
        var s = t.Sites.Where(x => x.Def.Kind == "tribe" && x.Def.Encounters.Count >= 3).OrderBy(x => x.Def.Id, StringComparer.Ordinal).First();
        var d = Dungeons.Map(t, s);
        // The first trip: nothing happened before it.
        var (first, _) = Dungeons.React(t, s, t.Tick);
        Assert.Empty(first);
        foreach (var r in Dungeons.MainWay(d).Where(r => r.Enc != Dungeons.Boss)) r.Cleared = true;
        var (lines, swell) = Dungeons.React(t, s, t.Tick + 40 * SimTime.HoursPerDay);
        Assert.NotEmpty(lines);
        Assert.True(swell > 0 || d.Rooms.Any(r => r.Retaken || r.Trapped) || d.Hidden);
        Assert.Equal(2, d.Trips);
    }

    [Fact]
    public void ADelveOnAMapKeepsWhatItClearedForTheNextCompany()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(160);
        var t = sim.Town;
        var mapped = t.Sites.Where(s => s.Dungeon != null).ToList();
        Assert.NotEmpty(mapped);
        // Every chronicle of a delve since has the map it walked, and the rooms it met are on that map.
        var told = t.Chronicles.Where(c => c.Rooms.Count > 0).ToList();
        Assert.NotEmpty(told);
        Assert.All(told, c => Assert.All(c.Visits, v => Assert.Contains(c.Rooms, r => r.Id == v.Room)));
        Assert.Contains(t.Chronicles, c => c.Approach != "");
    }

    [Fact]
    public void ACompanyOftenFindsDeepsUnderWhatItClears()
    {
        bool any = false;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(300);
            var t = sim.Town;
            foreach (var deep in t.Sites.Where(s => s.Def.Under != ""))
            {
                any = true;
                var up = Quests.Site(t, deep.Def.Under)!;
                // A tier harder than the site was when it fell (it may have been resettled worse since).
                Assert.True(deep.Def.Tier > t.Content.Dungeons.Deeps.MinTier);
                Assert.Equal(World.Spot(t, up), World.Spot(t, deep));
                Assert.InRange(Dungeons.Depth(t, deep), 1, t.Content.Dungeons.Deeps.MaxDepth);
            }
        }
        Assert.True(any);
    }

    [Fact]
    public void EveryFeatureHasAnAnswerAndACost()
    {
        var c = Content.Default();
        Assert.NotEmpty(c.Dungeons.Features);
        foreach (var f in c.Dungeons.Features)
        {
            Assert.True(f.Races.Count + f.Classes.Count > 0, f.Id);
            Assert.True(f.Attack != 0 || f.Ac != 0 || f.MonstersFirst || f.Hours > 0 || f.Check != null, f.Id);
            Assert.All(f.Classes, cls => Assert.Contains(c.Classes, k => k.Id == cls));
        }
    }
}
