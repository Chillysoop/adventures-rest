using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class WildsTests
{
    private static List<string> Ledger(Simulation sim) => sim.Town.Ledger.Entries.Select(e => e.Format()).ToList();

    private static Simulation Player(int seed) =>
        new(new SimulationOptions { Seed = seed, AutoBounties = true, AutoCouncil = true, AutoDefense = true, AutoPlanning = true, AutoExplore = false });

    /// <summary>A block of cells round a spot (days east and south of the town).</summary>
    private static List<(int X, int Y)> Block(Town t, double x, double y, int w, int h)
    {
        var (cx, cy) = Wilds.CellOf(t, x, y);
        var cells = new List<(int X, int Y)>();
        for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++) cells.Add((cx + i, cy + j));
        return cells;
    }

    /// <summary>Strokes that touch join one zone, the town itself can't be painted, and rubbing out every cell removes the zone.</summary>
    [Fact]
    public void PaintJoinsAndErases()
    {
        var sim = Player(4);
        var t = sim.Town;
        var a = sim.PaintWilds(Wilds.Patrol, Block(t, 2, 0, 2, 1));
        Assert.NotNull(a);
        var b = sim.PaintWilds(Wilds.Patrol, Block(t, 3, 0, 2, 1));
        Assert.Same(a, b);
        Assert.Single(Wilds.OfKind(t, Wilds.Patrol));
        // Explore paint can lie over patrol paint: they are separate brushes.
        Assert.NotNull(sim.PaintWilds(Wilds.Explore, Block(t, 2, 0, 1, 1)));
        // The town's own ground takes no paint.
        Assert.Null(sim.PaintWilds(Wilds.Patrol, [(0, 0), (-1, -1)]));
        Assert.Equal(a!.Cells.Count, sim.EraseWilds(Wilds.Patrol, a.Cells.Select(c => (c.X, c.Y)).ToList()));
        Assert.Empty(Wilds.OfKind(t, Wilds.Patrol));
        Assert.Single(Wilds.OfKind(t, Wilds.Explore));
    }

    /// <summary>An explore zone posts a commission, the company walks it, and it is posted again until every cell is seen; then the paint comes off.</summary>
    [Fact]
    public void ExploreZoneIsWalkedUntilSeen()
    {
        var sim = Player(5);
        sim.RunDays(40);
        var t = sim.Town;
        t.Book(TreasuryAccount.Exploring, 3000);
        var zone = sim.PaintWilds(Wilds.Explore, Block(t, 1.2, -0.4, 3, 2));
        Assert.NotNull(zone);
        int id = zone!.Id;
        int cells = zone.Cells.Count;
        bool posted = false, walked = false, pending = false;
        for (int d = 0; d < 300 && Wilds.Of(t, id) != null; d++)
        {
            sim.RunDays(1);
            // Walked ground stays the company's own until it is home to tell of it (out, or held up by a meeting on the way).
            if (Wilds.Of(t, id) is { } z && Wilds.Untold(z) > 0)
            {
                pending = true;
                Assert.Contains(t.Surveys, s => s.Saw.Count > 0 && (Explore.Out(s) || s.Phase == SurveyPhase.Engaged));
            }
            if (t.Commissions.Any(c => c.ZoneId == id)) posted = true;
            if (t.Surveys.Any(s => s.ZoneId == id && s.Phase == SurveyPhase.Home)) walked = true;
            t.Book(TreasuryAccount.Exploring, Math.Max(0, 500 - t.Treasury));
        }
        Assert.True(posted);
        Assert.True(walked);
        Assert.True(pending);
        Assert.Null(Wilds.Of(t, id));
        Assert.Contains(Ledger(sim), l => l.Contains("has been walked and told", StringComparison.Ordinal));
        Assert.True(cells > 0);
    }

    /// <summary>A patrol over a settlement's road is paid daily, cuts its raid chance and warns of raiders; unpaid, it lapses.</summary>
    [Fact]
    public void PatrolKeepsTheRoad()
    {
        var sim = Player(6);
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed);
        double before = Neighbours.RaidShare(t, s);
        var zone = Wilds.PatrolRoad(t, s);
        Assert.NotNull(zone);
        Assert.True(Wilds.Road(t, s) > 0.8);
        Assert.True(Neighbours.RaidShare(t, s) < before);
        Assert.True(Neighbours.SecurityTarget(t, s) > 0);
        Assert.True(Wilds.Warns(t, Explore.SettlementSpot(t, s)) > 0);
        Assert.True(Wilds.Wage(t, zone!) > 0);
        sim.RunDays(1);
        Assert.False(zone!.Unpaid);
        // Broke, the riders go home and the road is bare again.
        t.Book(TreasuryAccount.Patrols, -t.Treasury - 5000);
        sim.RunDays(1);
        Assert.True(zone!.Unpaid);
        Assert.Equal(0, Wilds.Road(t, s));
    }

    /// <summary>A caravan patrol bought before patrols were painted rides on as a painted patrol over the same road.</summary>
    [Fact]
    public void OldCaravanPatrolsBecomePaintedPatrols()
    {
        var sim = Player(6);
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed);
        s.Projects.Add("patrol");
        sim.RunDays(1);
        Assert.DoesNotContain("patrol", s.Projects);
        Assert.Single(Wilds.OfKind(t, Wilds.Patrol));
        Assert.True(Wilds.Road(t, s) > 0.8);
        Assert.DoesNotContain(Neighbours.ProjectsFor(t, s), p => p.Id == "patrol");
    }

    /// <summary>Only horse rides the wilds; a regiment out on patrol is away from the walls and saves the hired riders' wage.</summary>
    [Fact]
    public void HorseRidesAPatrol()
    {
        var sim = Player(6);
        var t = sim.Town;
        var zone = sim.PaintWilds(Wilds.Patrol, Block(t, 2, 1, 3, 2))!;
        var horse = new Regiment { Id = t.Army.NextId++, Unit = "outriders", Name = "1st Outriders" };
        var foot = new Regiment { Id = t.Army.NextId++, Unit = "spearmen", Name = "1st Spears" };
        var a = t.Agents.First(x => !x.IsHero && !x.Dead);
        horse.Soldiers.Add(a.Id);
        foot.Soldiers.Add(a.Id);
        t.Army.Regiments.Add(horse);
        t.Army.Regiments.Add(foot);
        double wage = Wilds.Wage(t, zone);
        Assert.False(sim.SendRiders(zone.Id, foot.Id));
        Assert.True(sim.SendRiders(zone.Id, horse.Id));
        Assert.True(Field.Away(t, horse));
        Assert.True(Wilds.Wage(t, zone) < wage);
        Assert.True(sim.SendRiders(zone.Id, null));
        Assert.False(Field.Away(t, horse));
    }

    /// <summary>A campaign saved with painted zones and a company out walking one plays on exactly as the run that never stopped.</summary>
    [Fact]
    public void SavesWithZones()
    {
        var sim = Player(5);
        sim.RunDays(40);
        var t = sim.Town;
        t.Book(TreasuryAccount.Exploring, 3000);
        sim.PaintWilds(Wilds.Explore, Block(t, -1.5, 1, 3, 3));
        sim.PaintWilds(Wilds.Patrol, Block(t, 1.5, 1, 3, 2));
        for (int h = 0; h < 24 * 60 && !t.Surveys.Any(s => s.ZoneId != null && Explore.Out(s)); h++) sim.Step();
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(30);
        loaded.RunDays(30);
        Assert.Equal(Ledger(sim), Ledger(loaded));
    }
}
