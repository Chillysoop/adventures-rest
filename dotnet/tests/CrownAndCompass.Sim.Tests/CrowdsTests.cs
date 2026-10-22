using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CrowdsTests
{
    [Fact]
    public void EveryBuildingThatServesTheTownsfolkHasRoomForSoMany()
    {
        var c = new Simulation(new SimulationOptions { Seed = 1 }).Town.Content;
        foreach (var d in c.Buildings.Where(d => d.Serves.Count > 0 || d.Venue || d.Holy))
        {
            Assert.True(d.Capacity > 0, $"{d.Id} serves the townsfolk but has no capacity");
            Assert.NotNull(Crowds.Kind(d));
        }
    }

    private static (Town Town, Building Tavern, List<Agent> Folk) FullTavern()
    {
        // A village of some fifty souls.
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(60);
        var t = sim.Town;
        var tavern = t.Taverns.OrderBy(b => b.Id).First();
        foreach (var a in t.Agents.Where(a => Crowds.Outing(a.Activity))) a.Activity = Activity.Relaxing;
        var folk = t.Workers.Where(a => !a.IsAway).OrderBy(a => a.Id).Take(tavern.Def.Capacity + 1).ToList();
        Assert.Equal(tavern.Def.Capacity + 1, folk.Count);
        foreach (var a in folk.Take(tavern.Def.Capacity))
        {
            a.Activity = Activity.Drinking;
            a.LocationId = tavern.Id;
        }
        var late = folk[^1];
        late.Activity = Activity.Relaxing;
        late.LocationId = late.HomeId;
        return (t, tavern, folk);
    }

    [Fact]
    public void AFullTavernTurnsTheNextDrinkerAwayAtTheDoor()
    {
        var (t, tavern, folk) = FullTavern();
        var late = folk[^1];
        Assert.Equal(tavern.Def.Capacity, Crowds.Inside(t, tavern));
        Assert.False(Crowds.Room(t, late, tavern));
        // Those already in keep their seats.
        Assert.True(Crowds.Room(t, folk[0], tavern));

        Assert.Null(Crowds.Enter(t, late, [tavern]));
        Assert.Equal(1, tavern.TurnedAway);
        Assert.Equal(tavern.Id, late.TurnedFrom);
        Assert.NotEqual(tavern.Id, late.LocationId);
    }

    [Fact]
    public void ATurnedAwayDrinkerGoesOnToAnotherTavernNearby()
    {
        var (t, tavern, folk) = FullTavern();
        var late = folk[^1];
        var def = t.Content.Building("tavern");
        var lot = Property.FindLot(t, def) ?? Planning.CivicLot(t, def)!;
        var other = t.AddBuilding(def, lot, null, "the Second Cup", true);
        var a = t.Lot(tavern.LotId);
        bool near = Js.Hypot(lot.X - a.X, lot.Y - a.Y) <= t.Content.Crowds.Detour;

        var went = Crowds.Enter(t, late, [tavern, other]);
        Assert.Equal(1, tavern.TurnedAway);
        if (near)
        {
            Assert.Same(other, went);
            Assert.Equal(other.Id, late.LocationId);
            Assert.Equal(1, other.Visits);
        }
        else Assert.Null(went);
    }

    [Fact]
    public void TurnedAwayVisitorsCutTheServiceAndRaiseTheAlert()
    {
        var (t, tavern, _) = FullTavern();
        tavern.TurnedAway = 40;
        tavern.Visits = 60;
        Crowds.Daily(t);
        Assert.Equal(0.4, Crowds.Shortfall(t, Crowds.Tavern), 6);
        Assert.Equal(1 - t.Content.Crowds.CoverageLoss * 0.4, Crowds.Coverage(t, "tavern"), 6);
        Assert.True(Crowds.Demand(t, Crowds.Tavern) > 0);
        var alert = Assert.Single(Alerts.Standing(t), x => x.Kind == Alerts.Crowded);
        Assert.Equal(tavern.Id, alert.BuildingId);
        Assert.Contains("commercial land", alert.Tip);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void NoPlaceEverHoldsMoreThanItHasRoomFor(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        bool visited = false, praying = false;
        int? venues = null;
        const int Hours = 24 * 150;
        for (int h = 0; h < Hours; h++)
        {
            sim.Step();
            var t = sim.Town;
            visited |= t.Agents.Any(a => a.Activity == Activity.Visiting);
            praying |= t.Agents.Any(a => a.Activity == Activity.Praying);
            if (h % 7 != 0) continue;
            foreach (var b in t.Buildings.Where(b => b.Def.Capacity > 0 && b.Complete))
                Assert.True(Crowds.Inside(t, b) <= b.Def.Capacity, $"{b.Name} holds {Crowds.Inside(t, b)} of {b.Def.Capacity}");
            if (venues == null && Crowds.Places(t).Keys.Any(k => k != Crowds.Tavern && k != Crowds.Holy)) venues = h;
        }
        // Townsfolk go out once there is a market hall, bathhouse or the like to go to (given a few days to find it).
        if (venues == null) Assert.False(visited);
        else if (venues < Hours - 24 * 5) Assert.True(visited);
        Assert.True(praying);
    }
}
