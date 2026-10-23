using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>The sky's heights: bands only high enough ships reach, the dragon roosts, and companies stranded up there.</summary>
public class ReachesTests
{
    private static (Simulation Sim, Town T) Docked(int seed = 1)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("sky_dock");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        t.Treasury += 200000;
        t.City.Tier = City.TierIndex(t, "great_city");
        return (sim, t);
    }

    private static SiteState InBand(Town t, string band) =>
        t.Sites.Where(s => s.Def.Band == band).OrderBy(s => t.Regions[s.RegionId!.Value].Days).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();

    private static List<Agent> Fliers(Town t)
    {
        var heroes = t.Heroes.Take(2).ToList();
        foreach (var h in heroes) h.Hero!.Mount = "griffon";
        return heroes;
    }

    [Fact]
    public void EveryWorldHasItsHeightsAndTheRoostsHoldAncientDragons()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            foreach (var band in t.Content.World.Isles.Bands)
            {
                var sites = t.Sites.Where(s => s.Def.Band == band.Id).ToList();
                Assert.NotEmpty(sites);
                Assert.All(sites, s => Assert.True(s.Def.Aloft));
                Assert.All(sites, s => Assert.InRange(t.Regions[s.RegionId!.Value].Ring, band.Rings[0], band.Rings[^1]));
            }
            var roosts = t.Sites.Where(s => s.Def.Band == "roost").ToList();
            Assert.All(roosts, s => Assert.Equal("elder-wyrms", s.Def.Foe));
            Assert.All(roosts, s => Assert.StartsWith("ancient-", s.Def.Boss.Monster));
            // Nobody else's foe stands in the roosts, and no roost is in the sea.
            Assert.All(roosts, s => Assert.False(t.Regions[s.RegionId!.Value].Theme.Sea));
        }
    }

    [Fact]
    public void WingsReachTheIslesButOnlyTheGreatHullsClimbToTheCloudSeaAndTheRoosts()
    {
        var (_, t) = Docked();
        var fliers = Fliers(t);
        Assert.True(Reaches.CanReach(t, fliers, InBand(t, "low")));
        Assert.True(Reaches.CanReach(t, fliers, InBand(t, "high")));
        // The dock's skiff and its lent airship climb only so high.
        var roost = InBand(t, "roost");
        var cloud = InBand(t, "cloud");
        Assert.False(Reaches.CanReach(t, fliers, roost));
        Assert.False(Reaches.CanReach(t, fliers, cloud));
        Assert.NotNull(Fleet.Build(t, "dreadnought"));
        Assert.True(Reaches.CanReach(t, fliers, cloud));
        Assert.False(Reaches.CanReach(t, fliers, roost));
        var leviathan = Fleet.Build(t, "leviathan")!;
        Assert.True(Reaches.CanReach(t, fliers, roost));
        Assert.Equal(leviathan.Id, Reaches.Free(t, roost).First().Id);
        // The least ship that climbs high enough goes, so the leviathan stays free for the roosts.
        Assert.NotEqual(leviathan.Id, Reaches.Free(t, cloud).First().Id);
    }

    [Fact]
    public void ACompanyBoundForTheRoostsGoesAboardAndIsStrandedWhenItsShipIsLost()
    {
        var (sim, t) = Docked(2);
        var leviathan = Fleet.Build(t, "leviathan")!;
        var roost = InBand(t, "roost");
        var heroes = t.Heroes.Take(2).ToList();
        var company = new Party { Id = 900, Name = "Lanterns", Members = heroes.Select(h => h.Id).ToList(), SiteId = roost.Def.Id, DepartTick = t.Tick, ReturnTick = t.Tick + 400 };
        t.Parties.Add(company);
        var mind = Reaches.Embark(t, company, heroes, new RouteMind(1, 1, 3, "Ada", null, false, false), roost);
        Assert.Equal(leviathan.Id, company.ShipId);
        Assert.True((mind.Ways & Ways.Air) != 0);
        Assert.False(Sky.Moored(t, leviathan));
        Assert.True(company.Edge >= t.Content.Sky.Expedition.Edge);
        // The ship is lost while they are below: they are stranded where they went.
        company.Expected = t.Tick;
        company.ShipLost = true;
        Assert.True(Reaches.Wait(t, company, roost));
        Reaches.Strand(t, company, roost);
        Assert.Null(company.ShipId);
        Assert.Contains(company, Reaches.Stranded(t));
        Assert.Equal("no airship free that climbs so high", Reaches.RescueRefused(t, company));
        // The town only sees them overdue; a new leviathan goes to look.
        t.Tick += (t.Content.Knowledge.OverdueDays + 1) * SimTime.HoursPerDay;
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"overdue:{company.Id}" && !a.Tip.Contains("airship"));
        var rescuer = Fleet.Build(t, "leviathan")!;
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"overdue:{company.Id}" && a.Tip.Contains("airship"));
        Assert.True(sim.RescueParty(company.Id));
        Assert.Equal(rescuer.Id, company.ShipId);
        Assert.Null(company.StrandedTick);
        Assert.True(company.ReturnTick > t.Tick && company.ReturnTick < t.Tick + 200 * SimTime.HoursPerDay);
        Assert.False(sim.RescueParty(company.Id));
    }

    [Fact]
    public void AStrandedCompanyNobodyComesForClimbsDownHurt()
    {
        var (_, t) = Docked(3);
        var roost = InBand(t, "roost");
        var hero = t.Heroes.First();
        var company = new Party { Id = 901, Name = "Forgotten", Members = [hero.Id], SiteId = roost.Def.Id, DepartTick = t.Tick };
        t.Parties.Add(company);
        Reaches.Strand(t, company, roost);
        int hp = hero.Hero!.Hp;
        t.Tick += (t.Content.Sky.Expedition.StrandMost + 1) * SimTime.HoursPerDay;
        Reaches.Daily(t);
        Assert.Null(company.StrandedTick);
        Assert.True(company.ReturnTick < int.MaxValue / 4);
        Assert.True(hero.Hero.Hp < hp || hero.Hero.Hp == 1);
    }

    [Fact]
    public void TheSkyDockStandsOnlyOnHighGround()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false }).Town;
        var dock = t.Content.Building("sky_dock");
        var h = t.Content.Sky.Heights;
        // The square is level: never there.
        Assert.False(Sky.SitsHigh(t, dock, Town.Center, Town.Center));
        var grid = Hills.Grid(t);
        int top = Enumerable.Range(0, grid.Length).OrderByDescending(i => grid[i]).First();
        double x = (top % Land.Size + 0.5) / Land.Res, y = (top / Land.Size + 0.5) / Land.Res;
        Assert.True(grid[top] >= h.MinHeight);
        Assert.True(Sky.SitsHigh(t, dock, x, y));
        // Other buildings don't care.
        Assert.True(Sky.SitsHigh(t, t.Content.Building("house"), Town.Center, Town.Center));
        // Wherever a lot is found for it, it is high.
        var lot = Land.Find(t, dock, null);
        if (lot != null) Assert.True(Sky.SitsHigh(t, dock, lot.X, lot.Y));
        else Assert.NotNull(Sky.HeightBlocked(t, dock));
    }
}
