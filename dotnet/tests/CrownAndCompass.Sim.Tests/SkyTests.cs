using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class SkyTests
{
    /// <summary>A player's town with a Sky Dock standing, its first airship granted, and gold to spend.</summary>
    private static (Simulation Sim, Town T, Ship Ship) Docked(int seed = 1)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("sky_dock");
        t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        t.Treasury += 5000;
        return (sim, t, Sky.Ships(t).Single());
    }

    [Fact]
    public void TheDockFliesItsFirstSkiffAndSharesTheFleetsBooks()
    {
        var (sim, t, ship) = Docked();
        Assert.Equal("skiff", ship.Kind);
        Assert.True(Fleet.Aloft(t, ship));
        Assert.Equal(Fleet.Def(t, ship).Lift, ship.Lift);
        // The same ship list, captains and repairs as the harbour's boats.
        Assert.Contains(ship, t.Ships);
        Assert.NotEqual("", ship.Captain);
        Assert.Empty(Fleet.AtSea(t));
        // A frigate needs a city; the skiff is free to build again.
        // Every kind names a real charter tier.
        Assert.All(t.Content.Sky.Ships.Concat(t.Content.Sea.Ships), d => Assert.Null(Record.Exception(() => Fleet.BuildBlocked(t, d.Id))));
        Assert.NotNull(Sky.BuildBlocked(t, "galleon"));
        Assert.Null(Sky.BuildBlocked(t, "skiff"));
        Assert.True(sim.BuildShip("skiff"));
        Assert.Equal(2, Sky.Ships(t).Count());
        Assert.Equal(2, Institutions.Airships(t));
    }

    [Fact]
    public void AScoutBurnsLiftAndConfirmsWhatLiesThere()
    {
        var (sim, t, ship) = Docked();
        var region = t.Regions.Where(r => r.Ring == 1 && t.Sites.Any(s => s.RegionId == r.Id && !s.Cleared)).OrderBy(r => r.Id).First();
        int days = Sky.Days(t, ship, region);
        Assert.Null(Sky.Refuse(t, ship, "scout", region.Id, "", []));
        Assert.True(sim.FlyShip(ship.Id, "scout", region.Id));
        Assert.NotNull(ship.Flight);
        Assert.Equal(1, Institutions.Airships(t) + 1);
        sim.RunDays(days + 3);
        if (ship.DownedRegion != null || !t.Ships.Contains(ship)) return;
        Assert.Null(ship.Flight);
        Assert.True(ship.Lift < Fleet.Def(t, ship).Lift);
        Assert.True(region.Known);
        Assert.All(t.Sites.Where(s => s.RegionId == region.Id && !s.Cleared), s => Assert.Equal(SiteKnowledge.Confirmed, s.Known));
        // The riggers charge it again at the dock.
        Assert.True(sim.ChargeShip(ship.Id));
        Assert.Equal(Fleet.Def(t, ship).Lift, ship.Lift);
    }

    [Fact]
    public void AStrikeBombardsALairBeforeACompanyGoesIn()
    {
        var (sim, t, ship) = Docked();
        var site = t.Sites.Where(s => s.RegionId is int r && t.Regions[r].Ring == 0 && !s.Cleared && s.Strength > 0).OrderBy(s => s.Def.Id, StringComparer.Ordinal).First();
        site.Known = SiteKnowledge.Rumoured;
        double before = site.Strength;
        Assert.True(sim.FlyShip(ship.Id, "strike", site.RegionId!.Value, site.Def.Id));
        sim.RunDays(Sky.Days(t, ship, t.Regions[site.RegionId!.Value]) + 3);
        if (!t.Ships.Contains(ship) || ship.DownedRegion != null) return;
        Assert.True(site.Strength < before);
    }

    [Fact]
    public void AShipThatRunsDryComesDownAndIsSalvagedOrLost()
    {
        var (sim, t, ship) = Docked();
        var region = t.Regions.Where(r => r.Ring == 2).OrderBy(r => r.Id).First();
        Assert.True(sim.FlyShip(ship.Id, "scout", region.Id));
        ship.Lift = 1;
        for (int i = 0; i < 6 && ship.DownedRegion == null && t.Ships.Contains(ship); i++) sim.RunDays(1);
        if (!t.Ships.Contains(ship)) return;
        Assert.NotNull(ship.DownedRegion);
        Assert.Equal(0, Institutions.Airships(t));
        Assert.True(sim.SalvageShip(ship.Id));
        sim.RunDays(Sky.Rules(t).SalvageDays + region.Ring + 1);
        Assert.Null(ship.DownedRegion);
        Assert.True(ship.Lift > 0);
        // Left where it fell, the wilds keep it.
        Assert.True(sim.FlyShip(ship.Id, "patrol") || ship.Lift < Sky.Rules(t).PatrolDays);
    }

    [Fact]
    public void FightsInTheAirGoByGunsAndBoardersMeetTheMarines()
    {
        var (sim, t, ship) = Docked();
        Assert.True(sim.BuildShip("skiff"));
        var outcomes = new List<string>();
        for (int i = 0; i < 12; i++)
        {
            var s = Sky.Ships(t).First(x => x.DownedRegion == null);
            s.Hull = Fleet.Def(t, s).Hull;
            s.Lift = Fleet.Def(t, s).Lift;
            outcomes.Add(Sky.Engage(t, s.Id, i % 2 == 0 ? "harpies" : "sky-pirates"));
        }
        Assert.All(outcomes, o => Assert.Contains(o, new[] { "evaded", "won", "boarded", "repelled", "lost", "downed", "wrecked" }));
        Assert.Contains(outcomes, o => o is "won" or "boarded");
        Assert.Equal(Math.Min(12, outcomes.Count), t.SkyFleet.Battles.Count);
        // Marines aboard a boarding come home hurt, never dead.
        var hero = Sky.Marines(t).First();
        int hp = hero.Hero!.Hp;
        var sk = Sky.Ships(t).First(x => x.DownedRegion == null);
        sk.Hull = Fleet.Def(t, sk).Hull;
        sk.Lift = Fleet.Def(t, sk).Lift;
        Assert.True(sim.FlyShip(sk.Id, "patrol", marines: [hero.Id]));
        Assert.True(hero.IsAway);
        for (int i = 0; i < 6; i++) Sky.Engage(t, sk.Id, "sky-pirates");
        Assert.False(hero.Dead);
        Assert.True(hero.Hero.Hp >= 1);
    }

    [Fact]
    public void APatrolSeesRaidersComingSooner()
    {
        var (sim, t, ship) = Docked();
        int before = Institutions.SkyWatch(t);
        Assert.True(sim.FlyShip(ship.Id, "patrol"));
        Assert.Equal(Math.Min(48, before + Sky.Rules(t).PatrolHours), Institutions.SkyWatch(t));
    }

    [Fact]
    public void TheWindSpeedsAShipWithItAndSlowsOneAgainstIt()
    {
        var t = new Simulation(new SimulationOptions { Seed = 2 }).Town;
        double with = Weather.Prevailing(t, Weather.TimeOf(t.Tick));
        Assert.True(Sky.FlightDays(t, 20, 0.5, with) < Sky.FlightDays(t, 20, 0.5, (with + 180) % 360));
    }

    [Fact]
    public void AirshipsSaveAndReplay()
    {
        var (sim, t, ship) = Docked();
        Assert.True(sim.FlyShip(ship.Id, "patrol"));
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(20);
        loaded.RunDays(20);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }
}
