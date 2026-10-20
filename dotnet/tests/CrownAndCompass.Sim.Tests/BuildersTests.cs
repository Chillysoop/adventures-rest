using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BuildersTests
{
    private static (Simulation Sim, Town T) Town()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, AutoPlanning = false, AutoBounties = false, Enemy = false });
        return (sim, sim.Town);
    }

    [Fact]
    public void AStandingYardPutsMoreCrewsToWork()
    {
        var (_, t) = Town();
        int before = Construction.Crews(t);
        var def = t.Content.Building("builders_yard");
        var yard = t.AddBuilding(def, Planning.CivicLot(t, def)!, null, "", true);
        Assert.True(yard.Complete);
        Assert.Equal(Math.Min(t.Content.City.Construction.Most, before + def.Crews), Construction.Crews(t));
    }

    [Fact]
    public void TheMayorRaisesAYardWhenTheTownOutgrowsItsCrews()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoBounties = false, Enemy = false });
        sim.RunDays(200);
        Assert.Contains(sim.Town.Buildings, b => b.Def.Id == "builders_yard");
    }

    [Fact]
    public void AHouseWaitsForWhatItsStreetLacks()
    {
        var (_, t) = Town();
        var house = t.Buildings.First(b => City.Housing(t, b.Def.Id) != null);
        var want = new HousingDef { Id = "test", Foods = 99 };
        Assert.Contains("kinds of food", City.NeedsBlocked(t, house, want));
        var near = new HousingDef { Id = "test", Near = ["nowhere"] };
        Assert.Contains("nowhere within", City.NeedsBlocked(t, house, near));
        Assert.Null(City.NeedsBlocked(t, house, new HousingDef { Id = "test" }));
    }
}
