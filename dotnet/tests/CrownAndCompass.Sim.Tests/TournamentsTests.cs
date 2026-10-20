using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TournamentsTests
{
    private static (Simulation Sim, Town T) Town(bool auto = false)
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoStories = auto, Enemy = false });
        return (sim, sim.Town);
    }

    /// <summary>Run to the morning the council proposes the summer tourney.</summary>
    private static void ToProposal(Simulation sim)
    {
        var t = sim.Town;
        t.City.Tier = Math.Max(t.City.Tier, City.TierIndex(t, "village"));
        for (int d = 0; d < 400 && t.Tourney.Stage != TourneyStage.Proposed; d++)
        {
            sim.RunDays(1);
            t.City.Tier = Math.Max(t.City.Tier, City.TierIndex(t, "village"));
        }
        Assert.Equal(TourneyStage.Proposed, t.Tourney.Stage);
    }

    [Fact]
    public void TheCouncilProposesItInSummerAndTheMayorPaysForIt()
    {
        var (sim, t) = Town();
        ToProposal(sim);
        Assert.Equal(Tournaments.Rules(t).Season, Seasons.Current(t).Id);
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Tourney);
        t.Treasury = 20000;
        double cost = Tournaments.Cost(t, Tournaments.Rules(t).Levels[1]);
        Assert.True(sim.FundTourney(1));
        Assert.Equal(20000 - cost, t.Treasury, 6);
        Assert.Equal(TourneyStage.Funded, t.Tourney.Stage);
        Assert.False(sim.FundTourney(2));
        // Every event has a full draw, and each hero rides in one event at most.
        Assert.All(t.Tourney.Events, e => Assert.Equal(Tournaments.Rules(t).Levels[1].Entrants, e.Draw.Count));
        var heroes = t.Tourney.Entrants.Where(e => e.AgentId != null).Select(e => e.AgentId).ToList();
        Assert.Equal(heroes.Count, heroes.Distinct().Count());
        Assert.Contains(t.Tourney.Entrants, e => e.SettlementId != null);
    }

    [Fact]
    public void SkippingItCostsMandate()
    {
        var (sim, t) = Town();
        ToProposal(sim);
        double mandate = t.Mandate;
        Assert.True(sim.SkipTourney());
        Assert.Equal(TourneyStage.Skipped, t.Tourney.Stage);
        Assert.True(t.Mandate < mandate);
        Assert.True(Tournaments.Mood(t) < 0);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Tourney);
    }

    [Fact]
    public void AnUnansweredProposalBecomesAModestTourney()
    {
        var (sim, t) = Town();
        ToProposal(sim);
        t.Treasury = 5000;
        sim.RunDays(t.Tourney.OpenDay - t.Day + 1);
        Assert.Equal(0, t.Tourney.Level);
        Assert.Equal(TourneyStage.Running, t.Tourney.Stage);
    }

    [Fact]
    public void TheEventsAreRiddenHeatByHeatAndTheWinnersAreCrowned()
    {
        var (sim, t) = Town();
        sim.RunDays(30);
        Assert.True(Tournaments.Stage(t, 1));
        Assert.Equal(TourneyStage.Running, t.Tourney.Stage);
        sim.RunDays(t.Tourney.Events.Count + 1);
        Assert.Equal(TourneyStage.Over, t.Tourney.Stage);
        Assert.All(t.Tourney.Events, e =>
        {
            Assert.NotNull(e.Winner);
            Assert.Equal(e.Draw.Count - 1, e.Heats.Count);
            Assert.All(e.Heats.Where(h => !h.Walkover), h => Assert.True(h.Passes.Count >= Tournaments.DefOf(t, e).Rounds.Count));
        });
        Assert.Equal(t.Tourney.Events.Count, t.Tourney.Honours.Count);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("in the final of", StringComparison.Ordinal));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The tourney ends", StringComparison.Ordinal));
        Assert.True(t.Tourney.GateGold > 0);
    }

    [Fact]
    public void FairOddsAddUpAndABetIsSettled()
    {
        var (sim, t) = Town();
        sim.RunDays(30);
        Assert.True(Tournaments.Stage(t, 0));
        var e = t.Tourney.Events[1];
        var chances = Tournaments.Chances(t, e);
        Assert.Equal(1, chances.Values.Sum(), 6);
        var d = Tournaments.DefOf(t, e);
        var a = Tournaments.Rider(t, e.Draw[0]);
        var b = Tournaments.Rider(t, e.Draw[1]);
        Assert.Equal(1, Tournaments.HeatChance(t, d, a, b) + Tournaments.HeatChance(t, d, b, a), 6);
        t.Treasury = 1000;
        int pick = e.Draw.OrderByDescending(id => chances[id]).First();
        Assert.False(sim.PlaceBet(1, pick, 5000));
        Assert.True(sim.PlaceBet(1, pick, 100));
        Assert.Equal(900, t.Treasury, 6);
        Assert.False(sim.PlaceBet(1, pick, 100));
        Assert.True(e.Bet!.Odds * chances[pick] < 1);
        sim.RunDays(3);
        Assert.True(e.Bet.Settled);
        Assert.Equal(e.Winner == pick ? Js.Round(100 * e.Bet.Odds) : 0, e.Bet.Paid, 6);
        // No stakes once the heats have begun.
        Assert.NotNull(Tournaments.BetBlocked(t, e));
    }

    [Fact]
    public void TheStandInFundsWhatTheTreasuryCanBear()
    {
        var (sim, t) = Town(auto: true);
        for (int d = 0; d < 400 && t.Tourney.Stage == TourneyStage.None; d++)
        {
            t.City.Tier = Math.Max(t.City.Tier, City.TierIndex(t, "village"));
            t.Treasury = 4000;
            sim.RunDays(1);
        }
        Assert.Equal(TourneyStage.Funded, t.Tourney.Stage);
        Assert.Equal(1, t.Tourney.Level);
    }

    [Fact]
    public void AMidTourneySaveRidesOnTheSame()
    {
        var (sim, t) = Town();
        sim.RunDays(30);
        Tournaments.Stage(t, 1);
        for (int h = 0; h < 12; h++) sim.Step();
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        sim.RunDays(5);
        loaded.RunDays(5);
        Assert.Equal(SaveGame.StateJson(sim), SaveGame.StateJson(loaded));
    }
}
