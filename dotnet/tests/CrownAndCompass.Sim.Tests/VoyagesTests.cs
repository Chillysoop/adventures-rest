using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>The harbour's ships at sea: companies carried to islands, the navigator and the weather, jobs, guards and fights.</summary>
public class VoyagesTests
{
    /// <summary>A sea cove with its harbour standing, a longship beside the first boat, and every land heard of.</summary>
    private static Simulation Harboured(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, Founding = "sea-cove", AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("harbour");
        var lot = Land.Find(t, d, null)!;
        lot.Zone = Zone.Civic;
        t.AddBuilding(d, lot, null, "the Harbour", true);
        foreach (var g in t.Regions) g.Known = true;
        t.Treasury = 5000;
        sim.RunDays(2);
        t.City.Tier = City.TierIndex(t, "village");
        Assert.NotNull(Fleet.Build(t, "longship"));
        return sim;
    }

    private static SiteState Island(Town t) =>
        t.Sites.Where(s => Voyages.Offshore(t, s)).OrderBy(s => t.Regions[s.RegionId!.Value].Days).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();

    private static RouteMind Walk => new(1, 1, 3, "Ada", null, false, false);

    [Fact]
    public void AnIslandNeedsAShipOrWings()
    {
        var ford = new Simulation(new SimulationOptions { Seed = 3 });
        var t = ford.Town;
        var isle = Island(t);
        var walkers = t.Heroes.Take(2).ToList();
        foreach (var h in walkers)
        {
            h.Hero!.Mount = "";
            foreach (var k in h.Hero.Magic.Select(e => e.Key).ToList()) h.Hero.Magic.Remove(k);
        }
        Assert.False(Passage.CanReach(t, walkers, isle));
        foreach (var h in walkers) h.Hero!.Mount = "griffon";
        Assert.True(Passage.CanReach(t, walkers, isle));
        // A town on the sea with its harbour and a ship free gets there.
        var cove = Harboured().Town;
        Assert.True(Passage.CanReach(cove, [], Island(cove)));
    }

    [Fact]
    public void ACompanyBoardsAShipThatSailsItOutAndIsFreeOnceItIsHome()
    {
        var sim = Harboured();
        var t = sim.Town;
        var isle = Island(t);
        var spot = World.Spot(t, isle);
        int free = Voyages.Free(t).Count();
        var company = new Party { Name = "the Lanterns", Members = [], SiteId = isle.Def.Id };
        var mind = Voyages.Embark(t, company, [], Walk, (0, 0), spot);
        Assert.NotNull(company.ShipId);
        Assert.True(mind.Sail > 0);
        var road = Roads.Plan(t, (0, 0), spot, mind, new Rng(1), []);
        Assert.Contains(road, w => w.Way == "sail" && w.Feature?.Kind.Ship == true && w.Sail == mind.Sail);
        t.Parties.Add(company);
        if (t.Ships.Any(s => s.Id == company.ShipId))
        {
            var ship = t.Ships.First(s => s.Id == company.ShipId);
            Assert.False(Voyages.Moored(t, ship));
            Assert.Equal(free - 1, Voyages.Free(t).Count());
            Assert.NotNull(Fleet.Refuse(t, ship, t.Settlements[0], Good.Food));
        }
        company.Returned = true;
        Assert.True(Voyages.Free(t).Count() >= free - 1);
    }

    [Fact]
    public void TheNavigatorAndTheWeatherSetTheShipsPace()
    {
        var t = Harboured().Town;
        var ship = Fleet.AtSea(t).First(s => s.Kind == "longship");
        var speed = Fleet.Def(t, ship).Speed;
        var nav = t.Content.Sea.Voyages.Navigate;
        var (slow, _) = Voyages.Weather(t);
        for (int i = 0; i < 40; i++)
        {
            double pace = Voyages.SailPace(t, ship, 3, new Rng(i));
            Assert.InRange(pace, speed * nav[0] * slow - 0.01, speed * nav[3] * slow + 0.01);
        }
        // A good navigator makes the passage quicker on the whole.
        double Mean(int survival) => Enumerable.Range(0, 200).Average(i => Voyages.SailPace(t, ship, survival, new Rng(i)));
        Assert.True(Mean(8) < Mean(-2));
        // A storm slows a ship more than fair skies, and the wind behind it is quicker than the wind ahead.
        var steps = t.Content.Sky.StormStep;
        Assert.True(steps[^1] > steps[0]);
        double wind = Weather.Prevailing(t, Weather.TimeOf(t.Tick));
        Assert.True(Voyages.Tailwind(t, wind) < Voyages.Tailwind(t, wind + 180));
    }

    [Fact]
    public void AShipScoutsASeaLandAndPatrolsTurnCorsairsAway()
    {
        var sim = Harboured();
        var t = sim.Town;
        var ship = Fleet.AtSea(t).First(s => s.Kind == "longship");
        var sea = t.Regions.Where(r => World.Sea(r)).OrderBy(r => r.Days).ThenBy(r => r.Id).First();
        sea.Known = false;
        Assert.Equal("not a sea land", Voyages.Refuse(t, ship, "scout", t.Regions.First(r => !World.Sea(r) && r.Ring > 0).Id, "", []));
        Assert.True(sim.SailJob(ship.Id, "scout", sea.Id));
        Assert.False(Voyages.Moored(t, ship));
        for (int d = 0; d < 40 && t.Ships.Contains(ship) && ship.Flight != null; d++) sim.RunDays(1);
        if (t.Ships.Contains(ship))
        {
            Assert.True(sea.Known);
            Assert.All(t.Sites.Where(s => s.RegionId == sea.Id && !s.Cleared), s => Assert.True(s.Found));
            ship.Hull = Fleet.Def(t, ship).Hull;
            Assert.Equal(0, Voyages.RaidGuard(t));
            Assert.True(sim.SailJob(ship.Id, "patrol"));
            Assert.True(Voyages.RaidGuard(t) > 0);
        }
    }

    [Fact]
    public void TheWatchStandsAtTheRailAtTheTownsCostAndSellswordsForPay()
    {
        var sim = Harboured();
        var t = sim.Town;
        var ship = Fleet.AtSea(t).First(s => s.Kind == "longship");
        t.Watch = Math.Max(t.Watch, 10);
        double home = Threat.Defense(t);
        Assert.Equal(0, Voyages.Guards(t, ship, 0));
        Assert.True(sim.SetShipGuard(ship.Id, "watch"));
        Assert.True(Voyages.Guards(t, ship, 0) > 0);
        Assert.True(sim.SailJob(ship.Id, "patrol"));
        Assert.True(Threat.Defense(t) < home);
        // Hired marines cost gold a head a day instead.
        var boat = Fleet.AtSea(t).First(s => s.Kind == "rowboat");
        Assert.True(sim.SetShipGuard(boat.Id, "hired"));
        double gold = t.Treasury;
        Assert.True(sim.SailJob(boat.Id, "patrol"));
        Assert.True(t.Treasury < gold);
        Assert.False(sim.SetShipGuard(boat.Id, "pirates"));
    }

    [Fact]
    public void SeaAndSkyShipsFightTheSameWay()
    {
        var t = Harboured().Town;
        var ship = Fleet.AtSea(t).First(s => s.Kind == "longship");
        var outcomes = new HashSet<string>();
        for (int i = 0; i < 30; i++)
        {
            ship.Hull = Fleet.Def(t, ship).Hull;
            outcomes.Add(ShipFight.Resolve(t, ship, new ShipFoe("a corsair sloop", 18, 3, true), null, [], 0, false, new Rng(i), out _));
        }
        Assert.Subset(new HashSet<string> { "evaded", "won", "boarded", "repelled", "lost", "wrecked" }, outcomes);
        Assert.DoesNotContain("downed", outcomes);
        Assert.Contains(outcomes, o => o is "won" or "boarded");
        Assert.True(ship.Fights >= 30);
    }

    [Fact]
    public void ACampaignWithShipsAtSeaSavesAndPlaysOnIdentically()
    {
        var sim = Harboured();
        var t = sim.Town;
        var ship = Fleet.AtSea(t).First(s => s.Kind == "longship");
        sim.SetShipGuard(ship.Id, "hired");
        Assert.True(sim.SailJob(ship.Id, "patrol"));
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(15);
        loaded.RunDays(15);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }

    [Fact]
    public void ACompanyLongGoneWithoutWordIsCalledOverdueUntilItIsHeardFrom()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        var t = sim.Town;
        for (int d = 0; d < 60 && !t.Parties.Any(p => !p.Returned); d++) sim.RunDays(1);
        var party = t.Parties.First(p => !p.Returned);
        Assert.True(party.Expected > 0);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"overdue:{party.Id}");
        party.Expected = t.Tick - (t.Content.Knowledge.OverdueDays + 1) * SimTime.HoursPerDay;
        var alert = Assert.Single(Alerts.Standing(t), a => a.Key == $"overdue:{party.Id}");
        Assert.Equal(party.SiteId, alert.SiteId);
        party.ReturnTick = t.Tick + 48;
        Knowledge.Heard(t, party);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"overdue:{party.Id}");
    }
}
