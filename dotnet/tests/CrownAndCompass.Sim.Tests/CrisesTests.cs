using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CrisesTests
{
    private static (Simulation Sim, Menace M) Seeded(int seed = 3, bool auto = true)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = auto, AutoDefense = auto });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        return (sim, m!);
    }

    [Fact]
    public void ACleverStrongEnemyHasAWorseCrisisThanADullWeakOne()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Cunning = "crafty";
        m.Level = 3;
        m.Power = 150;
        double dull = Crises.Score(t, m);
        m.Cunning = "mastermind";
        m.Level = 12;
        m.Power = 700;
        double vampire = Crises.Score(t, m);
        Assert.True(vampire > dull * 3, $"{vampire} vs {dull}");
        var sv = t.Content.Crises.Severities;
        Assert.Equal("lesser", sv.Last(s => dull >= s.Min).Id);
        Assert.Equal("dire", sv.Last(s => vampire >= s.Min).Id);
        m.Cunning = "mindless";
        Assert.Equal(0, Crises.Score(t, m));
    }

    [Fact]
    public void APowerLeftAloneBeginsACrisisThatStrikesAndMarchesOnTheWalls()
    {
        var (sim, m) = Seeded(auto: false);
        var t = sim.Town;
        t.Saga.Act = 2;
        m.Cunning = "cunning";
        // A would-be overlord could take the town's oath instead, and a power the town serves begins no crisis.
        m.Ambition = "conquest";
        m.Power = t.Content.Saga.Menaces.Stages.Power + 10;
        // No herald to kneel to: a town under its yoke owes it tribute, not a crisis.
        m.Refused = true;
        // Most of the way ripe already, so the council's bounties don't get to its seat first.
        t.Crises.Ripeness[m.Id] = t.Content.Crises.RipenDays - 5;
        var crisis = (Crisis?)null;
        for (int d = 0; d < 200 && t.Outcome == null && m.Alive; d++)
        {
            sim.RunDays(1);
            crisis ??= Crises.Of(t, m);
            if (crisis is { Active: false }) break;
        }
        Assert.NotNull(crisis);
        Assert.True(crisis!.BlowsLanded + crisis.BlowsStopped > 0 || !m.Alive);
        if (m.Alive && t.Outcome == null) Assert.False(crisis.Active);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(Crises.Title(t, crisis), StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CrisesUnderWayRaiseDread()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        Assert.Equal(0, Crises.Dread(t));
        t.Crises.All.Add(new Crisis { Id = 1, MenaceId = m.Id, Severity = "dire", StartDay = t.Day, Phase = CrisisPhase.Onslaught, NextPhaseDay = t.Day + 30, NextBlowDay = t.Day + 30 });
        Assert.True(Crises.Dread(t) > 0);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "doom:1" && a.Kind == Alerts.Doom);
    }

    [Fact]
    public void RaidersStayAwayFromATownTheyCannotBeat()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(20);
        var t = sim.Town;
        // A night with someone home to meet them: with every hero out and no watch, any band is worth it.
        for (int d = 0; d < 60 && Threat.LocalDefense(t, roll: false) <= 0; d++) sim.RunDays(1);
        var farm = t.Buildings.First(b => b.Complete && Threat.Exposed(t, b));
        Assert.True(Threat.WorthRaiding(t, farm, 10_000));
        Assert.False(Threat.WorthRaiding(t, farm, 0));
        Assert.False(Threat.WorthRaiding(t, null, 10_000));
    }

    [Fact]
    public void AnswerToAClashIsTheTownsOnlyOnce()
    {
        var (sim, a) = Seeded();
        var t = sim.Town;
        var b = Menaces.Seed(t, [2, 3])!;
        t.Crises.Clashes.Add(new Clash { Id = 1, A = a.Id, B = b.Id, Where = "the hills", Day = t.Day + 5 });
        Assert.Contains(Alerts.Standing(t), x => x.Key == "clash:1");
        Assert.False(sim.JoinClash(1, 999));
        Assert.True(sim.JoinClash(1, b.Id));
        Assert.False(sim.JoinClash(1, 0));
        Assert.DoesNotContain(Alerts.Standing(t), x => x.Key == "clash:1");
    }

    [Fact]
    public void AWrongedEnemySwearsVengeanceAndComesForTheHeroItMarked()
    {
        var (sim, m) = Seeded();
        var t = sim.Town;
        m.Cunning = "mastermind";
        for (int d = 0; d < 120 && !t.Heroes.Any(h => !h.IsAway); d++) sim.RunDays(1);
        var hero = t.Heroes.Where(h => !h.IsAway).OrderBy(h => h.Id).First();
        Vengeance.Wrong(t, Vengeance.Key(m), new Culprit(m.Name, m.Leader, m.SeatName), "broken", 1.6, [hero.Id]);
        var g = Vengeance.Of(t, Vengeance.Key(m));
        Assert.NotNull(g);
        Assert.Contains(hero.Id, g!.Marked);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("sworn", StringComparison.Ordinal));
        int entries = t.Ledger.Entries.Count;
        for (int d = 0; d < 30 && g.Heat >= t.Content.Crises.Vengeance.StrikeAt; d++) sim.RunDays(1);
        Assert.True(g.Heat < t.Content.Crises.Vengeance.StrikeAt);
        // It struck where the town could see it, or (the marked hero out of town) quietly turned a councillor or sent an agent in to strike.
        Assert.True(t.Ledger.Entries.Skip(entries).Any(e => e.Text.Contains(m.Name, StringComparison.Ordinal) || e.Text.Contains(m.Leader, StringComparison.Ordinal) ||
            e.Text.Contains(m.SeatName, StringComparison.Ordinal)) || t.Moles.Any(x => x.Foe == g.Foe) || t.Lurkers.Any(l => l.Leader == m.Leader || l.By == m.Name));
    }

    private static (Simulation Sim, Grudge G) Plotting(string kind = "undead")
    {
        // A council of three at least: the traitor, the one they frame, and one more to frame next.
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        sim.RunDays(60);
        for (int seed = 5; sim.Town.Seats.Count(s => s.HolderId != null) < 3 && seed < 12; seed++)
        {
            sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(60);
        }
        var t = sim.Town;
        var m = Menaces.Seed(t, [2, 3])!;
        // Not one seated where a company is already at work: it can fall the next day, and its servants with it.
        for (int i = 0; i < 8 && t.Parties.Any(p => p.SiteId == m.SeatId); i++) m = Menaces.Seed(t, [2, 3])!;
        var g = new Grudge { Foe = Vengeance.Key(m), Name = m.Name, Leader = m.Leader, Seat = m.SeatName, Kind = kind, Wits = "mastermind", Cunning = 1.6 };
        t.Grudges.Add(g);
        t.Watch = 0;
        Assert.True(t.Seats.Count(s => s.HolderId != null) >= 2);
        return (sim, g);
    }

    [Fact]
    public void AVampireWinsOverACouncillorWhoWorksAgainstTheTownUntilTheTavernsNameNames()
    {
        var (sim, g) = Plotting();
        var t = sim.Town;
        for (int i = 0; i < 50 && t.Moles.Count == 0; i++) Infiltration.Try(t, g);
        var mole = Assert.Single(t.Moles);
        Assert.True(Infiltration.Try(t, g) == false || t.Moles.Count == 1);
        var motion = new Motion { Id = 999, Kind = MotionKind.HireWatch, Proposer = "x", Text = "hire" };
        Assert.False(Infiltration.Vote(t, mole.AgentId, motion));
        Assert.Null(Infiltration.Vote(t, t.Seats.First(s => s.HolderId != null && s.HolderId != mole.AgentId).HolderId!.Value, motion));
        Assert.Empty(Infiltration.Whispered(t));
        mole.Suspicion = t.Content.Crises.Infiltration.Whisper;
        sim.RunDays(1);
        var (_, a, b) = Assert.Single(Infiltration.Whispered(t));
        Assert.Contains(mole.AgentId, new[] { a.Id, b.Id });
        Assert.Contains(Alerts.Standing(t), x => x.Key == $"traitor:{mole.Id}");
    }

    [Fact]
    public void NamingTheTraitorDrivesThemOutAndNamingTheWrongOneCostsTheMayor()
    {
        var (sim, g) = Plotting("rivals");
        var t = sim.Town;
        for (int i = 0; i < 50 && t.Moles.Count == 0; i++) Infiltration.Try(t, g);
        var mole = t.Moles[0];
        mole.Suspicion = t.Content.Crises.Infiltration.Whisper;
        sim.RunDays(1);
        int decoy = mole.DecoyId!.Value;
        double mandate = t.Mandate;
        Assert.True(sim.AccuseCouncillor(decoy));
        Assert.True(t.Mandate < mandate);
        Assert.DoesNotContain(t.Seats, s => s.HolderId == decoy);
        Assert.Null(mole.DecoyId);
        Assert.Single(t.Moles);
        mole.Suspicion = t.Content.Crises.Infiltration.Whisper;
        // Only the plot's own day: a council election in a full day can unseat the traitor, who is then of no use to anyone.
        Infiltration.Daily(t);
        Assert.NotNull(mole.DecoyId);
        Assert.True(sim.AccuseCouncillor(mole.AgentId));
        Assert.Empty(t.Moles);
        Assert.True(t.Agent(mole.AgentId).Departed);
        Assert.False(sim.AccuseCouncillor(mole.AgentId));
    }

    [Fact]
    public void LeftAloneTheWatchFindsTheProof()
    {
        var (sim, g) = Plotting("rivals");
        var t = sim.Town;
        for (int i = 0; i < 50 && t.Moles.Count == 0; i++) Infiltration.Try(t, g);
        var mole = t.Moles[0];
        mole.Suspicion = t.Content.Crises.Infiltration.Proof - 0.01;
        sim.RunDays(1);
        Assert.Empty(t.Moles);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("the proof", StringComparison.Ordinal));
    }

    [Fact]
    public void ACharmedCouncillorIsFreedAndKeepsTheSeat()
    {
        var (sim, g) = Plotting("undead");
        var t = sim.Town;
        for (int i = 0; i < 50 && t.Moles.Count == 0; i++) Infiltration.Try(t, g);
        var mole = Assert.Single(t.Moles);
        Assert.True(mole.Charmed);
        var seat = t.Seats.First(s => s.HolderId == mole.AgentId);
        mole.Suspicion = t.Content.Crises.Infiltration.Proof - 0.01;
        Infiltration.Daily(t);
        Assert.Empty(t.Moles);
        Assert.Equal(mole.AgentId, seat.HolderId);
        Assert.False(t.Agent(mole.AgentId).Departed);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("charm on", StringComparison.Ordinal));
    }

    [Fact]
    public void AWardedChamberTurnsMostCharmsAway()
    {
        int open = 0, warded = 0;
        for (int round = 0; round < 2; round++)
        {
            var (sim, g) = Plotting("undead");
            var t = sim.Town;
            if (round == 1) t.Stories.Decrees["warded-chamber"] = t.Day;
            Assert.Equal(round == 1, Infiltration.Warded(t));
            for (int i = 0; i < 40; i++)
            {
                t.Moles.Clear();
                Infiltration.Try(t, g);
                if (t.Moles.Count > 0) { if (round == 0) open++; else warded++; }
            }
        }
        Assert.True(warded < open, $"warded {warded} vs open {open}");
    }
}
