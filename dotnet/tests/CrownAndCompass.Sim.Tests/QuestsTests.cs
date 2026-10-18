using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class QuestsTests
{
    [Fact]
    public void HoldsBountyRewardsInEscrowAndRefundsThemOnExpiry()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var town = sim.Town;
        double before = town.Treasury;
        var bounty = sim.PostBounty("wolfwood", 100);
        Assert.NotNull(bounty);
        Assert.Equal(before - 100, town.Treasury);
        Assert.Null(sim.PostBounty("wolfwood", 50));
        sim.RunDays(11);
        Assert.NotEqual(BountyStatus.Open, bounty.Status);
        if (bounty.Status == BountyStatus.Expired)
        {
            // The refund came back; everything else the treasury did is ordinary income.
            int refunds = town.Ledger.Entries.Count(e => e.Text.Contains("withdraws it"));
            Assert.Equal(1, refunds);
        }
    }

    [Fact]
    public void RefusesBountiesTheTownCannotPayFor()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        Assert.Null(sim.PostBounty("drowned_temple", sim.Town.Treasury + 1));
    }

    [Fact]
    public void ResolvesTheSameExpeditionIdenticallyFromTheSameSeed()
    {
        static ExpeditionResult Run()
        {
            var sim = new Simulation(new SimulationOptions { Seed = 9, AutoMayor = false });
            var t = sim.Town;
            var party = t.Content.Classes.Take(4).Select(c => Quests.CreateHero(t, c, 2)).ToList();
            return Expedition.Run(new Rng(77), t.Content, t.Content.Sites[0], party);
        }
        var a = Run();
        var b = Run();
        Assert.Equal(a.Lines, b.Lines);
        Assert.Equal(a.Outcome, b.Outcome);
    }

    [Fact]
    public void OutmatchedPartiesBreakOffAndCarryTheirFallenHome()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 9, AutoMayor = false });
        var t = sim.Town;
        var site = t.Content.Sites.First(s => s.Tier == 3);
        int wiped = 0;
        var lines = new List<string>();
        for (int i = 0; i < 40; i++)
        {
            var party = t.Content.Classes.Take(3).Select(c => Quests.CreateHero(t, c, 1)).ToList();
            var result = Expedition.Run(new Rng(100 + i), t.Content, site, party);
            if (result.Outcome == ExpeditionOutcome.Wiped) wiped++;
            lines.AddRange(result.Lines);
        }
        // Level 1 heroes in a level 3 site get badly beaten, but most of them live to run.
        Assert.True(wiped < 10, $"{wiped} of 40 parties wiped");
        Assert.Contains(lines, l => l.Contains("breaks off and runs, dragging"));
    }

    [Fact]
    public void SendsPartiesOutAndWritesChroniclesOverACampaign()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(120);
        var town = sim.Town;
        Assert.True(town.Heroes.Count + town.Agents.Count(a => a.Hero != null && a.Departed) > 0);
        Assert.NotEmpty(town.Chronicles);
        foreach (var c in town.Chronicles) Assert.True(c.Lines.Count > 1);
        Assert.Contains(town.Ledger.Entries, e => e.Category == LedgerCategory.Quest);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void KeepsHeroesOutOfTheJobAndHousingMarketsAndTheDeadOutOfTown(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(100);
        foreach (var a in sim.Town.Agents)
        {
            if (a.Hero != null)
            {
                Assert.Null(a.JobId);
                Assert.Null(a.HomeId);
            }
            if (a.Dead) Assert.True(a.Departed);
        }
        foreach (var b in sim.Town.Bounties)
        {
            if (b.Status == BountyStatus.Taken) Assert.Contains(sim.Town.Parties, p => p.BountyId == b.Id && !p.Returned);
        }
    }
}
