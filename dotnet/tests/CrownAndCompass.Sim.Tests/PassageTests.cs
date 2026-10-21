using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Xunit;

namespace CrownAndCompass.Sim.Tests;

public class PassageTests
{
    private static Town Walled(int seed, bool moat = true, bool city = false)
    {
        var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
        t.Defenses.StoneWall = PalisadeState.Built;
        if (moat) t.Defenses.Moat = PalisadeState.Built;
        if (city) t.Defenses.CityWall = PalisadeState.Built;
        return t;
    }

    /// <summary>Walk from one point to another the way <see cref="Passage.Next"/> leads, in short steps; every step taken.</summary>
    private static List<(double X, double Y)> Walk(Town t, (double X, double Y) from, (double X, double Y) to, bool desperate = false)
    {
        var steps = new List<(double X, double Y)> { from };
        var cur = from;
        for (int i = 0; i < 4000 && Math.Abs(cur.X - to.X) + Math.Abs(cur.Y - to.Y) > 1e-6; i++)
        {
            var aim = Passage.Next(t, cur, to, desperate);
            double dx = aim.X - cur.X, dy = aim.Y - cur.Y, len = Math.Sqrt(dx * dx + dy * dy);
            cur = len <= 0.05 ? aim : (cur.X + dx / len * 0.05, cur.Y + dy / len * 0.05);
            steps.Add(cur);
        }
        return steps;
    }

    /// <summary>Whether a step stands on a wall or in its moat anywhere but its gate and bridge.</summary>
    private static bool Trespass(Town t, (double X, double Y) p) =>
        Passage.Barriers(t).Any(b => Passage.OnBarrier(b, p) && !b.Openings.Any(o => Dist(p, o.In, o.Out) < 0.4));

    private static double Dist((double X, double Y) p, (double X, double Y) a, (double X, double Y) b)
    {
        double dx = b.X - a.X, dy = b.Y - a.Y, t = Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / (dx * dx + dy * dy), 0, 1);
        return Math.Sqrt(Math.Pow(a.X + dx * t - p.X, 2) + Math.Pow(a.Y + dy * t - p.Y, 2));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(5, true)]
    public void WalkersGoByTheGateAndTheBridgeNeverOverTheWallOrThroughTheMoat(int seed, bool city)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(240);
        var t = sim.Town;
        t.Defenses.StoneWall = PalisadeState.Built;
        t.Defenses.Moat = PalisadeState.Built;
        if (city) t.Defenses.CityWall = PalisadeState.Built;
        // Doorsteps in the old town, between the walls and outside them all, every way round.
        var doors = t.Buildings.Where(b => b.Complete).OrderBy(b => b.Id).Select(b => Streets.Door(t.Lot(b.LotId), 0.4)).ToList();
        Assert.True(doors.Count > 40);
        var spots = Enumerable.Range(0, 12).Select(i => Passage.Clear(t, doors[i * doors.Count / 12])).ToList();
        foreach (var a in spots)
            foreach (var b in spots)
            {
                if (a == b) continue;
                var walk = Walk(t, a, Passage.Clear(t, b));
                Assert.True(walk[^1] == Passage.Clear(t, b), $"stuck at ({walk[^1].X:0.00}, {walk[^1].Y:0.00}) on the way from ({a.X:0.00}, {a.Y:0.00}) to ({b.X:0.00}, {b.Y:0.00})");
                var bad = walk.FirstOrDefault(p => Trespass(t, p));
                Assert.True(bad == default, $"({bad.X:0.00}, {bad.Y:0.00}) is in the wall or the moat, from ({a.X:0.00}, {a.Y:0.00}) to ({b.X:0.00}, {b.Y:0.00})");
            }
    }

    [Fact]
    public void OnlySomeoneFleeingForTheirLifeSwimsTheMoatAndClimbsTheWall()
    {
        var t = Walled(3);
        double c = Town.Center;
        var walk = Walk(t, (c, c - 20), (c, c), desperate: true);
        Assert.Contains(walk, p => Trespass(t, p));
        Assert.True(walk.Count < 450, "fleeing goes the straight way");
    }

    [Fact]
    public void RaidersAtTheDoorSendTheNearestRunning()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(60);
        var t = sim.Town;
        t.Defenses.StoneWall = PalisadeState.Built;
        double c = Town.Center;
        var outside = t.Buildings.Where(b => b.Complete && !Passage.Inside(Passage.Barriers(t)[0].Loop, t.Lot(b.LotId).X, t.Lot(b.LotId).Y)).OrderBy(b => b.Id).FirstOrDefault();
        if (outside == null) return;
        var near = t.Agents.FirstOrDefault(a => !a.Dead && !a.Departed && !a.IsAway && a.HomeId == outside.Id);
        Assert.DoesNotContain(t.Agents, a => Passage.Fleeing(t, a));
        Passage.Alarm(t, outside);
        if (near != null) Assert.True(Passage.Fleeing(t, near));
        // Nobody inside the walls runs: they are safe where they are.
        Assert.DoesNotContain(t.Agents, a => Passage.Fleeing(t, a) && (a.LocationId ?? a.HomeId) is int h
            && Passage.Inside(Passage.Barriers(t)[0].Loop, t.Lot(t.Building(h).LotId).X, t.Lot(t.Building(h).LotId).Y));
        t.Tick += t.Content.Passage.Flee.Hours;
        Assert.DoesNotContain(t.Agents, a => Passage.Fleeing(t, a));
        Assert.True(c > 0);
    }

    [Fact]
    public void EveryWalledPartOfTheTownKeepsAWayOut()
    {
        foreach (var (moat, city) in new[] { (false, false), (true, false), (true, true), (false, true) })
        {
            var t = Walled(4, moat, city);
            Assert.NotEmpty(Passage.Barriers(t));
            Assert.Empty(Passage.Sealed(t));
        }
        var p = new Simulation(new SimulationOptions { Seed = 4 }).Town;
        p.Defenses.Palisade = PalisadeState.Built;
        Assert.Single(Passage.Barriers(p));
        Assert.Empty(Passage.Sealed(p));
    }

    [Fact]
    public void LanesStopAtTheWallButForItsGate()
    {
        var t = Walled(1);
        var mask = Passage.Mask(t)!;
        var b = Passage.Barriers(t)[0];
        int Cell((double X, double Y) p) => (int)(p.Y * Land.Res) * Land.Size + (int)(p.X * Land.Res);
        Assert.True(mask[Cell(b.Loop[b.Loop.Length / 4])]);
        Assert.True(mask[Cell(b.Loop[b.Loop.Length / 2])]);
        Assert.False(mask[Cell(b.Loop[0])]);
        Assert.Null(Passage.Mask(new Simulation(new SimulationOptions { Seed = 1 }).Town));
    }

    [Fact]
    public void PartiesLeaveAndComeHomeByTheGate()
    {
        var t = Walled(2);
        var gate = Passage.Gate(t)!.Value;
        var mind = new RouteMind(1, 1, 3, "Ada", null, false, false);
        var route = Roads.Plan(t, (0, 0), (1.5, -2), mind, new Rng(1), []);
        Assert.Equal("gate", route[0].Way);
        Assert.Equal(gate.X, route[0].X, 6);
        var home = Roads.Plan(t, (1.5, -2), (0, 0), mind, new Rng(1), []);
        Assert.Equal((0.0, 0.0), (home[^1].X, home[^1].Y));
        Assert.Equal(gate.X, home[^2].X, 6);
    }

    [Fact]
    public void TheRiverIsCrossedAtItsFordsAndBridgesUnlessTheyCanSwimOrFly()
    {
        for (int seed = 1; seed <= 6; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            var river = Passage.River(t);
            Assert.True(river.Count > 100);
            Assert.True(Passage.Crossings(t).Count >= 5);
            // No site stands in the water.
            foreach (var s in t.Sites.Where(s => s.Def.Distance > 0))
            {
                var (x, y) = World.Spot(t, s);
                Assert.True(river.Min(p => Math.Sqrt((p.X - x) * (p.X - x) + (p.Y - y) * (p.Y - y))) > 0.05, $"seed {seed}: {s.Def.Name} stands in the river");
            }
            var east = (3.0, -2.5);
            var walk = new RouteMind(1, 1, 3, "Ada", null, false, false);
            var route = Roads.Plan(t, (0, 0), east, walk, new Rng(1), []);
            Assert.Contains(route, w => w.Way is "ford" or "bridge");
            var cur = (X: 0.0, Y: 0.0);
            foreach (var w in route)
            {
                var at = Passage.MeetsRiver(t, cur, (w.X, w.Y));
                Assert.True(at == null, $"seed {seed}: a stretch to ({w.X:0.00}, {w.Y:0.00}) wades the river");
                cur = (w.X, w.Y);
            }
            var flying = Roads.Plan(t, (0, 0), east, walk with { Ways = Ways.Water | Ways.River | Ways.Sheer }, new Rng(1), []);
            Assert.DoesNotContain(flying, w => w.Way is "ford" or "bridge");
        }
    }

    [Fact]
    public void LakesAndPeaksAreGoneRoundOnFootAndFlownOver()
    {
        int seen = 0;
        for (int seed = 1; seed <= 30 && seen < 3; seed++)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            // The bone ferryman's water is the one anyone crosses (WayfaringTests).
            foreach (var f in t.Terrain.Where(f => f.Kind.Barrier != "" && f.Kind.Wonder == null))
            {
                // Straight through the middle of it, from just short of it to just past it.
                double mid = (f.Inner + f.Outer) / 2, a = f.Bearing * Math.PI / 180;
                (double, double) At(double r) => (Math.Sin(a) * r, -Math.Cos(a) * r);
                // From well short of it (where a road from the town comes), and from in the hollow right before it.
                var walk = new RouteMind(1, 1, 3, "Ada", null, false, false);
                var said = new List<string>();
                var route = Roads.Plan(t, At(f.Inner - 0.35), At(f.Outer + 0.35), walk, new Rng(1), said);
                if (route.Count < 2 || route.All(w => w.Feature != f)) continue;
                Assert.DoesNotContain(route, w => w.Feature == f && w.Way is "cross" or "fly");
                Assert.Contains(route, w => w.Feature == f && w.Way == "skirt");
                var need = Passage.Needs(f.Kind);
                var over = Roads.Plan(t, At(f.Inner - 0.35), At(f.Outer + 0.35), walk with { Ways = need | Ways.River, Carrier = "their griffons" }, new Rng(1), []);
                Assert.Contains(over, w => w.Feature == f && w.Way == "fly");
                seen++;
            }
        }
        Assert.True(seen >= 3, "too few lakes and peaks in the first seeds");
    }

    [Fact]
    public void SpellsItemsAndMountsSayWhoCrossesWhat()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(30);
        var t = sim.Town;
        var heroes = t.Heroes.Take(3).ToList();
        Assert.Equal(3, heroes.Count);
        // On foot and empty-handed: no flying carpet or winged boots of their own.
        foreach (var h in heroes)
        {
            h.Hero!.Mount = "";
            foreach (var k in h.Hero.Magic.Select(e => e.Key).ToList()) h.Hero.Magic.Remove(k);
        }
        Assert.Equal(Ways.None, Passage.Abilities(t, heroes) & Ways.Sheer);
        // One on a griffon is not enough; all of them on griffons go anywhere.
        heroes[0].Hero!.Mount = "griffon";
        Assert.Equal(Ways.None, Passage.Abilities(t, heroes) & Ways.Sheer);
        foreach (var h in heroes) h.Hero!.Mount = "griffon";
        Assert.Equal(Ways.River | Ways.Water | Ways.Sheer | Ways.Air, Passage.Abilities(t, heroes));
        // Horses swim the river but no lake.
        foreach (var h in heroes) h.Hero!.Mount = "horse";
        Assert.Equal(Ways.River, Passage.Abilities(t, heroes) & (Ways.River | Ways.Water));
        // A flying carpet carries everyone.
        foreach (var h in heroes) h.Hero!.Mount = "";
        heroes[1].Hero!.Magic["trinket"] = t.Content.Library.Item("carpet-of-flying")!;
        Assert.Equal(Ways.Water | Ways.River | Ways.Sheer, Passage.Abilities(t, heroes) & (Ways.Water | Ways.River | Ways.Sheer));
        Assert.Contains("carpet", Passage.Carrier(t, heroes));
        // A cleric able to cast water walk takes the whole party over a lake.
        Assert.Equal(5, Passage.SlotLevel("full", 3));
        Assert.Equal(9, Passage.SlotLevel("half", 3));
        Assert.Equal(11, Passage.SlotLevel("full", 6));
    }

    [Fact]
    public void FliersPayTheMoatNoMind()
    {
        var t = Walled(1);
        Assert.Equal(100, Garrison.Wade(t, 100, 1), 6);
        Assert.True(Garrison.Wade(t, 100) < 100);
        Assert.True(Passage.Fliers(t, "dragon") >= 1);
        Assert.Equal(0, Passage.Fliers(t, "tribe"));
    }

    [Fact]
    public void HeroesGetMountsTheirWayAndPayForThem()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(40);
        var t = sim.Town;
        var h = t.Heroes.First(a => a.Hero!.Cls.Id != "paladin");
        h.Hero!.AwayUntil = null;
        h.Hero!.Mount = "";
        h.Gold = 1000;
        Passage.Daily(t);
        Assert.True(h.Hero.Mount == "horse", $"{h.Hero.Cls.Id} L{h.Hero.Level} {h.Gold} '{h.Hero.Mount}' mounts {t.Content.Passage.Mounts.Count}");
        Assert.Equal(1000 - t.Content.Passage.Mount("horse")!.Price, h.Gold, 6);
        // No roost, no griffon, however rich.
        h.Gold = 10000;
        h.Hero.Level = 12;
        Passage.Daily(t);
        Assert.Equal("horse", h.Hero.Mount);
    }

    [Fact]
    public void TheSkyDocksAirshipCarriesOneCompanyAtATimeOverTheRiver()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        var east = (3.0, -2.5);
        Party Company(string name) => new() { Name = name, Members = [], SiteId = t.Sites[0].Def.Id };
        var walk = new RouteMind(1, 1, 3, "Ada", null, false, false);
        Assert.Equal(walk, Passage.Board(t, Company("the Lanterns"), walk, (0, 0), east));
        var d = t.Content.Building("sky_dock");
        var dock = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, "the Sky Dock", true);
        sim.RunDays(1);
        Assert.NotNull(Institutions.Find(t, "sky-dock"));
        Assert.Equal(1, Passage.AirshipsFree(t));
        // A road that meets the river takes the ship, and the ship crosses where it likes.
        var first = Company("the Lanterns");
        var mind = Passage.Board(t, first, walk, (0, 0), east);
        Assert.True(first.Airship);
        Assert.Equal(Ways.River | Ways.Water | Ways.Sheer, mind.Ways & (Ways.River | Ways.Water | Ways.Sheer));
        Assert.DoesNotContain(Roads.Plan(t, (0, 0), east, mind, new Rng(1), []), w => w.Way is "ford" or "bridge");
        t.Parties.Add(first);
        // It's out, so the next company walks; once the first is home it's free again.
        var second = Company("the Thorns");
        Assert.Equal(walk, Passage.Board(t, second, walk, (0, 0), east));
        Assert.False(second.Airship);
        first.Returned = true;
        Assert.Equal(1, Passage.AirshipsFree(t));
        Assert.Contains("airship", string.Join(" ", Institutions.Describe(t, t.Content.Institutions.Def("sky-dock")!.Service)));
    }
}
