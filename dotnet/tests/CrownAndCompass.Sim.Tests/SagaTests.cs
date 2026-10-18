using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>One long campaign (it ends by day 1000 on every seed swept), played once and shared by the tests that read it.</summary>
public sealed class LongCampaign
{
    public Simulation Sim { get; }

    public LongCampaign()
    {
        Sim = new Simulation(new SimulationOptions { Seed = 1 });
        for (int d = 0; d < 1000 && Sim.Town.Outcome == null; d++) Sim.RunDays(1);
    }
}

public class SagaTests(LongCampaign campaign) : IClassFixture<LongCampaign>
{
    [Fact]
    public void ActsOpenInOrderAndTheCampaignEnds()
    {
        var t = campaign.Sim.Town;
        Assert.NotNull(t.Outcome);
        Assert.Equal(t.Content.Saga.Acts.Count - 1, t.Saga.Act);
        Assert.Equal(t.Content.Saga.Acts.Count, t.Saga.ActDays.Count);
        for (int i = 1; i < t.Saga.ActDays.Count; i++)
        {
            var act = t.Content.Saga.Acts[i];
            Assert.True(t.Saga.ActDays[i] > t.Saga.ActDays[i - 1]);
            Assert.InRange(t.Saga.ActDays[i], act.EarliestDay, act.LatestDay);
        }
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Act IV: The Long Night.", StringComparison.Ordinal));
        Assert.NotEqual("", t.Saga.EndText);
    }

    [Fact]
    public void EachActBringsMenacesBuiltForItsLevels()
    {
        var t = campaign.Sim.Town;
        var seeded = t.Saga.Menaces.Where(m => m.Origin == "act").ToList();
        Assert.NotEmpty(seeded);
        var bands = t.Content.Saga.Acts.SelectMany(a => a.Seeds).ToList();
        foreach (var m in seeded) Assert.Contains(bands, b => m.BornLevel >= b[0] && m.BornLevel <= b[^1]);
    }

    [Fact]
    public void TheBigBadIsTheStrongestMenaceOfTheLastAct()
    {
        var t = campaign.Sim.Town;
        var big = Saga.BigBad(t);
        Assert.NotNull(big);
        Assert.True(big.BigBad);
        Assert.Equal(MenaceStage.Overlord, big.Peak);
        Assert.InRange(big.Level, t.Content.Saga.Finale.OverlordLevel[0], t.Content.Saga.Finale.OverlordLevel[^1]);
        // It was crowned when the last act opened, out of what play had grown.
        Assert.Equal(t.Saga.ActDays[^1], t.Saga.CrownDay);
        Assert.True(big.BornDay < t.Saga.CrownDay || big.Origin == "awakened");
    }

    [Fact]
    public void TheBigBadDoesNotRiseToMeetStrongChampions()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, FreeCompanies = false });
        var t = sim.Town;
        sim.RunDays(1);
        var m = Menaces.Seed(t, [9, 9])!;
        foreach (var h in t.Heroes) h.Hero!.Level = 12;
        m.BigBad = true;
        Menaces.Ascend(t, m);
        var band = t.Content.Saga.Finale.OverlordLevel;
        Assert.Equal(Math.Clamp(9, band[0], band[^1]), m.Level);
    }

    [Fact]
    public void TheBigBadEscapesItsFirstDefeatsAndAYoungLongNight()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, FreeCompanies = false });
        var t = sim.Town;
        sim.RunDays(1);
        var f = t.Content.Saga.Finale;
        var m = Menaces.Seed(t, [9, 9])!;
        m.BigBad = true;
        Menaces.Ascend(t, m);
        t.Saga.CrownDay = t.Day;
        for (int i = 0; i < f.Escapes + 1; i++)
        {
            Quests.Site(t, m.SeatId)!.Cleared = true;
            Menaces.Daily(t, false);
            Assert.True(m.Alive);
        }
        Assert.Equal(f.Escapes + 1, t.Saga.BigBadEscapes);
        Assert.Null(t.Outcome);

        // Out of escapes and the Long Night is old: this time it dies.
        t.Saga.CrownDay = t.Day - f.MinDays;
        Quests.Site(t, m.SeatId)!.Cleared = true;
        Menaces.Daily(t, false);
        Assert.False(m.Alive);
    }

    [Fact]
    public void AKnownLairLeftAloneFestersAndRises()
    {
        // Nobody goes out: every lair the town hears of is left alone.
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoBounties = false, FreeCompanies = false });
        sim.RunDays(240);
        var t = sim.Town;
        var festered = t.Saga.Menaces.Where(m => m.Origin == "lair").ToList();
        Assert.NotEmpty(festered);
        var m = festered.OrderByDescending(x => x.Power).First();
        Assert.True(m.Power > t.Content.Saga.Menaces.Stages.Rising);
        Assert.True(m.Named);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.War && e.Text.Contains(m.Leader, StringComparison.Ordinal));
    }

    [Fact]
    public void ClearingTheSeatBreaksTheMenaceOrEndsIt()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, FreeCompanies = false });
        var t = sim.Town;
        sim.RunDays(1);
        var m = Menaces.Seed(t, [2, 4])!;
        Assert.NotNull(m);
        var seat = Quests.Site(t, m.SeatId)!;
        var holding = t.Sites.First(s => !s.Cleared && s.Def.Id != seat.Def.Id && s.Def.Id != t.Content.Enemy.Outpost.Id && Menaces.At(t, s.Def.Id) == null);
        m.Holdings.Add(holding.Def.Id);
        double power = m.Power;
        seat.Cleared = true;
        sim.RunDays(1);
        // Broken: the leader (or an heir) holds on at the holding, weaker.
        Assert.True(m.Alive);
        Assert.Equal(holding.Def.Id, m.SeatId);
        Assert.True(m.Power < power);
        holding.Cleared = true;
        sim.RunDays(1);
        Assert.False(m.Alive);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.War && e.Importance == Importance.Major && e.Text.Contains(m.Leader, StringComparison.Ordinal));
    }

    [Fact]
    public void ATownIsSackedOnceAndLivesUnderTheYoke()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4 });
        var t = sim.Town;
        sim.RunDays(1);
        t.Treasury = 500;
        var band = Menaces.Warband(t);
        var lines = new List<string>();
        Assert.True(Saga.Sack(t, band, lines));
        Assert.Equal(band.Id, t.Saga.YokeId);
        Assert.Equal(0, t.Treasury, 0);
        Assert.True(Saga.Restrained(t));
        Assert.Contains(lines, l => l.Contains("under the yoke", StringComparison.Ordinal));
        // The second time the walls break, there is no yoke left to offer: the town falls.
        Assert.False(Saga.Sack(t, band, lines));
        // Tribute is taken every so often while the town pays.
        t.Treasury = 300;
        sim.RunDays(t.Content.Saga.Yoke.EveryDays + 1);
        Assert.True(t.Accounts.Get(TreasuryAccount.Tribute, 0) < -500);
    }

    [Fact]
    public void DreadDarkensTheToneAndFamiliesFlee()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        var t = sim.Town;
        sim.RunDays(30);
        t.Saga.Scars = 400;
        sim.RunDays(60);
        Assert.Equal(Tone.Dark, t.Saga.Tone);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Darkness gathers over", StringComparison.Ordinal));
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Departure && e.Text.Contains("take the south road", StringComparison.Ordinal));
    }

    [Fact]
    public void WithoutTheSagaTheLastStandStillEndsTheGame()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1, Saga = false });
        for (int d = 0; d < 260 && sim.Town.Outcome == null; d++) sim.RunDays(1);
        Assert.NotNull(sim.Town.Outcome);
        Assert.Empty(sim.Town.Saga.Menaces);
        Assert.Equal(0, sim.Town.Saga.Act);
    }

    [Fact]
    public void TheSameSeedTellsTheSameSaga()
    {
        var a = new Simulation(new SimulationOptions { Seed = 7 });
        var b = new Simulation(new SimulationOptions { Seed = 7 });
        a.RunDays(250);
        b.RunDays(250);
        Assert.Equal(Saga.Summary(a.Town), Saga.Summary(b.Town));
        Assert.Equal(a.Town.Ledger.Entries.Count, b.Town.Ledger.Entries.Count);
    }
}
