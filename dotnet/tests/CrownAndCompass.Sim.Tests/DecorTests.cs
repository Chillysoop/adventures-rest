using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class DecorTests
{
    private static Simulation Planned(int seed = 1, int days = 30)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoPlanning = false });
        sim.RunDays(days);
        sim.Town.Treasury = 5000;
        return sim;
    }

    /// <summary>Put a piece down in the first open spot by a building.</summary>
    private static Decoration Near(Simulation sim, string id, Building b, double angle = 0, double? within = null)
    {
        var t = sim.Town;
        var (x, y) = Decor.PosOf(t, b);
        var spot = Decor.Spot(t, Decor.Rules(t).Piece(id), x, y, within: within ?? Decor.Rules(t).Piece(id).Radius * 0.7);
        Assert.NotNull(spot);
        var d = sim.Decorate(id, spot!.Value.X, spot.Value.Y, angle);
        Assert.NotNull(d);
        return d!;
    }

    [Fact]
    public void EveryPieceDoesSomethingBesidesLookingNice()
    {
        var rules = Content.Default().Decor;
        Assert.True(rules.Pieces.Count >= 25);
        foreach (var p in rules.Pieces)
        {
            double[] effects = [p.Rest, p.Light, p.Trade, p.Harvest, p.Talk, p.Food, p.Favor, p.Honor, p.Draw, p.Mandate, p.Drill, p.Deter];
            Assert.True(effects.Any(e => e != 0), $"{p.Id} only looks nice");
            Assert.False(string.IsNullOrWhiteSpace(p.Blurb), $"{p.Id} has no blurb");
        }
    }

    [Fact]
    public void PiecesGoAnywhereAtAnyAngleButNotOnBuildingsOrEachOther()
    {
        var sim = Planned();
        var t = sim.Town;
        var hall = t.Buildings[0];
        var (hx, hy) = Decor.PosOf(t, hall);
        Assert.Null(sim.Decorate("bench", hx, hy, 0));
        var spot = Decor.Spot(t, Decor.Rules(t).Piece("bench"), hx, hy)!.Value;
        double before = t.Treasury;
        var bench = sim.Decorate("bench", spot.X, spot.Y, 1.234);
        Assert.NotNull(bench);
        Assert.Equal(1.234, bench!.Angle, 6);
        Assert.True(t.Treasury < before);
        Assert.Null(sim.Decorate("lamp", spot.X + 0.05, spot.Y, 0));
        Assert.Equal("too far from town", Decor.SpotBlocked(t, Decor.Rules(t).Piece("lamp"), Town.Center + 31, Town.Center));
        Assert.Equal(5.5, Decor.NormalAngle(5.5 - 4 * Math.PI), 6);
        Assert.True(sim.ClearDecoration(bench.Id));
        Assert.Empty(t.Decor.Pieces);
    }

    [Fact]
    public void ABenchAndATreeLiftTheMoodOfTheHouseNextDoor()
    {
        var sim = Planned();
        var t = sim.Town;
        var house = t.Buildings.First(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0);
        var resident = t.Agent(house.Residents[0]);
        resident.Activity = Activity.Relaxing;
        Assert.Equal(0, Decor.Mood(t, resident));
        Near(sim, "bench", house);
        Near(sim, "flowers", house);
        Assert.True(Decor.AppealOf(t, house) > 0);
        Assert.True(Decor.Mood(t, resident) > 0);
        Assert.True(Decor.Mood(t, resident) <= Decor.Rules(t).MoodCap + Decor.Rules(t).RestCap * Decor.Rules(t).RestMood);
    }

    [Fact]
    public void TreesGrowIntoTheirEffect()
    {
        var sim = Planned();
        var t = sim.Town;
        var house = t.Buildings.First(b => b.Complete && b.Def.Housing > 0);
        var oak = Near(sim, "oak", house);
        double young = Decor.AppealOf(t, house);
        Assert.Equal(0.3, Decor.Growth(t, oak), 6);
        sim.RunDays(oak.Def.GrowDays + 1);
        Assert.Equal(1, Decor.Growth(t, oak));
        Assert.True(Decor.AppealOf(t, house) > young * 3);
    }

    [Fact]
    public void LampsHelpTheWatchAndStocksDeterThieves()
    {
        var sim = Planned();
        var t = sim.Town;
        double before = Planning.WatchFactor(t);
        foreach (var b in t.Buildings.Where(b => b.Complete).ToList()) Near(sim, "lamp", b);
        Near(sim, "stocks", t.Buildings[0]);
        Assert.True(Planning.WatchFactor(t) > before + 0.15);
        Assert.True(Planning.WatchFactor(t) <= before + Decor.Rules(t).LightWatch + Decor.Rules(t).DeterCap + 1e-9);
    }

    [Fact]
    public void ScarecrowsHelpFarmsAndStallsHelpWorkshops()
    {
        var sim = Planned();
        var t = sim.Town;
        var farm = t.Buildings.First(b => b.Def.Id == "farm");
        var camp = t.Buildings.First(b => b.Def.Id == "lumber_camp");
        Assert.Equal(1, Decor.Yield(t, farm));
        Near(sim, "scarecrow", farm);
        Near(sim, "market_stall", camp);
        Assert.True(Decor.Yield(t, farm) > 1);
        Assert.True(Decor.Yield(t, camp) > 1);
        Assert.True(Decor.Yield(t, farm) <= 1 + Decor.Rules(t).HarvestCap);
    }

    [Fact]
    public void AStatueToAGodWinsItFavourAndOneToAHeroWinsTheirLoyalty()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(80);
        var t = sim.Town;
        t.Treasury += 2000;
        var god = Decor.DefaultGod(t);
        var shrine = Near(sim, "wayside_shrine", t.Buildings[0], within: 8);
        Assert.Equal(god, shrine.Deity);
        Assert.True(Decor.Favor(t, god) > 0);
        Assert.Equal(0, Decor.Favor(t, god == "x" ? "y" : god + "-other"));

        var hero = t.Agents.Where(a => a.Hero != null && !a.Departed).OrderBy(a => Loyalty.Target(t, a)).ThenBy(a => a.Id).First();
        double target = Js.Sum(Loyalty.Factors(t, hero), f => f.Points);
        var statue = Near(sim, "hero_statue", t.Buildings[0], within: 8);
        statue.HeroId = hero.Id;
        Assert.Contains(Decor.Loyalty(t, hero), f => f.Why == "their statue stands in town");
        Assert.True(Js.Sum(Loyalty.Factors(t, hero), f => f.Points) > target);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The town raises a statue of"));
    }

    [Fact]
    public void OrchardsSellTheirFruitAndLampsBurnOil()
    {
        var sim = Planned();
        var t = sim.Town;
        Near(sim, "fruit_tree", t.Buildings[0]);
        Near(sim, "lamp", t.Buildings[1]);
        sim.RunDays(3);
        Assert.True(t.Accounts.Get(TreasuryAccount.Orchards, 0) > 0);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Decorations, 0) < 0);
    }

    [Fact]
    public void DecoratedOpenGroundIsAGardenAndBuildersAndLanesGoRoundDecorations()
    {
        var sim = Planned();
        var t = sim.Town;
        t.Treasury = 2000;
        // Open, unpainted ground near the hall, off the lanes.
        (double X, double Y) OpenGround(double dx, double dy)
        {
            for (double r = 0; r < 6; r += 0.25)
                for (int k = 0; k < 16; k++)
                {
                    double x = Town.Center + dx + r * Math.Cos(k * Math.PI / 8), y = Town.Center + dy + r * Math.Sin(k * Math.PI / 8);
                    int i = (int)(y * Land.Res) * Land.Size + (int)(x * Land.Res);
                    if (t.Plan.Zones[i] == (byte)Zone.None && Planning.SpotBlocked(t, t.Content.Building("granary"), x, y, 0) == null) return (x, y);
                }
            throw new InvalidOperationException("no open ground");
        }
        var (fx, fy) = OpenGround(-5, 0);
        Assert.NotNull(sim.Decorate("flowers", fx, fy, 0));
        Assert.True(Decor.Garden(t, fx, fy));
        sim.PaintZone([(fx, fy)], 1.5, Zone.Residential);
        Assert.Equal((byte)Zone.None, t.Plan.Zones[(int)(fy * Land.Res) * Land.Size + (int)(fx * Land.Res)]);

        // A bench where the granary would go: the builders won't knock it down, and say why.
        var (bx, by) = OpenGround(5, 0);
        var granary = t.Content.Building("granary");
        var (w, d) = Land.Footprint(granary);
        Assert.True(Land.Fits(t, bx, by, w, d, 0, null));
        var bench = sim.Decorate("bench", bx, by, 0)!;
        Assert.NotNull(bench);
        Assert.False(Land.Fits(t, bx, by, w, d, 0, null));
        Assert.Null(Land.At(t, granary, bx, by, 0));
        // Placed there by hand, it settles beside the bench instead.
        int benchCell = (int)(by * Land.Res) * Land.Size + (int)(bx * Land.Res);
        Assert.True(Decor.Cells(t)![benchCell]);
        if (Planning.Spot(t, granary, bx, by, 0) is { } beside) Assert.DoesNotContain(benchCell, Land.Cells(beside.X, beside.Y, w, d, beside.Yaw));
        else Assert.Contains("decorations", Planning.SpotBlocked(t, granary, bx, by, 0));
        Assert.Contains(t.Decor.Pieces, p => p.Id == bench.Id);
        // Cleared away, the ground is free again.
        Assert.True(sim.ClearDecoration(bench.Id));
        Assert.True(Land.Fits(t, bx, by, w, d, 0, null));
        // Nothing goes on a street.
        var lane = t.Plan.Streets.First();
        Assert.Equal("that's the street", Decor.SpotBlocked(t, Decor.Rules(t).Piece("lamp"), lane.Points[0], lane.Points[1]));
    }

    [Fact]
    public void PleasantStreetsHelpHousesGrowAndMakeAWardDearer()
    {
        var sim = Planned(days: 60);
        var t = sim.Town;
        var house = t.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && City.Housing(t, b.Def.Id)?.Next is { Length: > 0 }).OrderBy(b => b.Id).First();
        var next = City.Housing(t, City.Housing(t, house.Def.Id)!.Next)!;
        var ward = City.WardOf(t, t.Lot(house.LotId));
        double prospect = City.Prospect(t, house, next), land = t.LotPrice(t.Lot(house.LotId));
        Assert.Equal(1, Decor.Prestige(t, ward.Id));
        foreach (var id in new[] { "flowers", "bench", "oak", "fountain", "flowers", "column" })
            if (Decor.Rules(t).Piece(id).Population <= t.Population) Near(sim, id, house, within: 1.6);
        Assert.True(Decor.Comfort(t, house) > 0);
        Assert.True(Decor.Comfort(t, house) <= Decor.Rules(t).ContentCap);
        Assert.True(City.Prospect(t, house, next) > prospect || prospect >= 1);
        Assert.True(Decor.Prestige(t, ward.Id) > 1);
        Assert.True(Decor.Prestige(t, ward.Id) <= 1 + Decor.Rules(t).WardCap);
        Assert.True(t.LotPrice(t.Lot(house.LotId)) > land);
        // The stocks next door make a household less content.
        var stocks = Near(sim, "stocks", house, within: 1.6);
        foreach (var p in t.Decor.Pieces.Where(p => p.Id != stocks.Id).ToList()) sim.ClearDecoration(p.Id);
        Assert.True(Decor.Comfort(t, house) < 0);
        Assert.True(Decor.Comfort(t, house) >= Decor.Rules(t).ContentFloor);
    }

    [Fact]
    public void DecorationsSurviveASaveAndReplayTheSame()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        sim.RunDays(60);
        sim.Town.Treasury += 2000;
        var house = sim.Town.Buildings.First(b => b.Complete && b.Def.Housing > 0);
        Near(sim, "oak", house, 0.7, 8);
        Near(sim, "hero_statue", sim.Town.Buildings[0], 2.1, 8);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(sim.Town.Decor.Pieces.Count, loaded.Town.Decor.Pieces.Count);
        Assert.All(loaded.Town.Decor.Pieces, p => Assert.Contains(p.Def, loaded.Town.Content.Decor.Pieces));
        sim.RunDays(20);
        loaded.RunDays(20);
        Assert.Equal(sim.Town.Ledger.Entries.Select(e => e.Format()), loaded.Town.Ledger.Entries.Select(e => e.Format()));
    }

    [Fact]
    public void TheStandInDressesTheLandRoundTheTown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        // The stand-in only dresses from spare gold, so give it some rather than hope this seed is rich on the day.
        sim.RunDays(140);
        sim.Town.Treasury += 3000;
        sim.RunDays(10);
        var t = sim.Town;
        Assert.NotEmpty(t.Decor.Pieces);
        // It keeps off the wards laid out, so headless towns lose no building land to it.
        Assert.All(t.Decor.Pieces, p => Assert.True(p.X < 0 || p.Y < 0 || p.X >= Town.Grid || p.Y >= Town.Grid
            || t.Plan.Zones[(int)(p.Y * Land.Res) * Land.Size + (int)(p.X * Land.Res)] == (byte)Zone.None));
    }
}
