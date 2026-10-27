using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>A town on the sea: its harbour, boats that fish and trade, worn hulls and corsairs.</summary>
public class FleetTests
{
    /// <summary>A sea cove a few weeks on, its harbour standing and every region heard of.</summary>
    private static Simulation Harboured(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, Founding = "sea-cove", AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("harbour");
        var lot = Land.Find(t, d, null)!;
        lot.Zone = Zone.Civic;
        t.AddBuilding(d, lot, null, "the Harbour", true);
        foreach (var g in t.Regions) g.Known = true;
        t.Treasury = 3000;
        sim.RunDays(2);
        return sim;
    }

    [Fact]
    public void OnlyATownOnTheSeaRaisesAHarbour()
    {
        var ford = new Simulation(new SimulationOptions { Seed = 3 }).Town;
        Assert.NotNull(City.Locked(ford, ford.Content.Building("harbour")));
        var cove = new Simulation(new SimulationOptions { Seed = 3, Founding = "sea-cove" }).Town;
        Assert.Null(City.Locked(cove, cove.Content.Building("harbour")));
    }

    [Fact]
    public void ThePlayerBuildsTheHarbourAndTheSkyDockThemselves()
    {
        // Nothing raises either for a player: the build bar offers the harbour from the start on the sea, and the
        // Sky Dock once the town hears of it; no ships until they stand.
        var sim = new Simulation(new SimulationOptions { Seed = 3, Founding = "sea-cove", AutoMayor = false });
        var t = sim.Town;
        sim.RunDays(30);
        Assert.DoesNotContain(t.Buildings, b => b.Def.Id is "harbour" or "sky_dock");
        Assert.Empty(t.Ships);
        Assert.False(Institutions.Hidden(t, "harbour"));
        // Laid down, it's a site the builders raise like any other; the first boat comes when it opens.
        var d = t.Content.Building("harbour");
        var lot = Land.Find(t, d, null)!;
        lot.Zone = Zone.Civic;
        t.Treasury += 2000;
        Planning.Place(t, d.Id, lot);
        var site = Assert.Single(t.Buildings, b => b.Def.Id == "harbour");
        Assert.True(site.IsSite);
        sim.RunDays(1);
        Assert.Empty(t.Ships);
    }

    [Fact]
    public void TheHarbourGivesAFirstBoatThatFishes()
    {
        var sim = Harboured();
        var boat = Assert.Single(sim.Town.Ships);
        Assert.Equal("rowboat", boat.Kind);
        Assert.True(boat.Fished > 0);
    }

    [Fact]
    public void ShipsSailTradeRunsAndComeHomeWorn()
    {
        var sim = Harboured();
        var t = sim.Town;
        Assert.False(sim.BuildShip("carrack"));
        Assert.True(sim.BuildShip("rowboat"));
        Assert.Equal(2, t.Ships.Count);
        var ship = t.Ships[1];
        var port = Fleet.Ports(t).FirstOrDefault();
        Assert.NotNull(port);
        t.Market.Seed(Good.Timber, 200);
        t.Market.Seed(Good.Food, 200);
        Assert.True(sim.SendShip(ship.Id, port!.Id, Good.Timber));
        Assert.NotNull(ship.Out);
        // The weather and the navigator set the days: she is home the day after the run says.
        sim.RunDays(ship.Out!.Days + 1);
        if (t.Ships.Contains(ship))
        {
            Assert.Null(ship.Out);
            Assert.Equal(1, ship.Voyages);
            Assert.True(ship.Hull < Fleet.Def(t, ship).Hull, $"hull {ship.Hull} line {ship.LastLine}");
            Assert.True(sim.RepairShip(ship.Id));
            Assert.Equal(Fleet.Def(t, ship).Hull, ship.Hull);
        }
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(ship.Name, StringComparison.Ordinal));
    }

    [Fact]
    public void CorsairsMeetTheShipsAndRaidTheHarbour()
    {
        bool met = false, raided = false;
        for (int seed = 1; seed <= 8 && !(met && raided); seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, Founding = "sea-cove", AutoMayor = true });
            var t = sim.Town;
            var d = t.Content.Building("harbour");
            var lot = Land.Find(t, d, null)!;
            lot.Zone = Zone.Civic;
            t.AddBuilding(d, lot, null, "the Harbour", true);
            foreach (var g in t.Regions) g.Known = true;
            sim.RunDays(150);
            met |= t.Ledger.Entries.Any(e => e.Text.Contains("corsair", StringComparison.OrdinalIgnoreCase) && e.Category == LedgerCategory.Town);
            raided |= t.Ledger.Entries.Any(e => e.Category == LedgerCategory.War && e.Text.Contains("harbour", StringComparison.OrdinalIgnoreCase));
        }
        Assert.True(raided);
        Assert.True(met);
    }

    [Fact]
    public void AFleetSurvivesASave()
    {
        var sim = Harboured(5);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(sim.Town.Ships.Count, loaded.Town.Ships.Count);
        sim.RunDays(20);
        loaded.RunDays(20);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }
}
