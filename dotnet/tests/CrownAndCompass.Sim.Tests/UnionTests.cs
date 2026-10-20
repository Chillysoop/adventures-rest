using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class UnionTests
{
    /// <summary>A player's town grown until the union has formed.</summary>
    private static (Simulation Sim, Town T) Town(int seed = 1)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoCouncil = false, AutoHonours = false, Enemy = false });
        var t = sim.Town;
        for (int d = 0; d < 300 && !t.Union.Formed; d++) sim.RunDays(1);
        Assert.True(t.Union.Formed);
        return (sim, t);
    }

    [Fact]
    public void TheMovementFormsFromWorkersInAndOutOfTheGuildsAndTheStandInNeverSeesIt()
    {
        var (_, t) = Town();
        var members = Union.Members(t);
        Assert.True(members.Count >= Union.Rules(t).Founding);
        // Not only the guilds: workers outside their trades join too.
        Assert.Contains(members, a => !Guilds.InTrade(t, a));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(Union.Rules(t).Name, StringComparison.Ordinal));
        var auto = new Simulation(new SimulationOptions { Seed = 1, Enemy = false });
        auto.RunDays(150);
        Assert.False(auto.Town.Union.Formed);
    }

    [Fact]
    public void FundingResearchPullsTheBarTowardTheMagesAndTheGuildsSour()
    {
        var (sim, t) = Town();
        double before = Union.Target(t);
        t.Research.Funding = 4;
        Assert.True(Union.Target(t) > before);
        t.Union.Bar = 80;
        t.Union.Heard = 60;
        var g = t.Guilds[0];
        g.Standing = 60;
        g.StrikeUntil = 0;
        t.Union.NextBreak = t.Day + 100;
        sim.RunDays(1);
        Assert.True(g.Standing < 60);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "union");
        // Past breaking, the union marches and every guild walks out.
        t.Union.NextBreak = t.Day;
        t.Union.Bar = 95;
        sim.RunDays(1);
        Assert.Contains(t.Guilds, x => Guilds.OnStrike(t, x));
    }

    [Fact]
    public void LeaningToTheGuildsSlowsTheLibraryAndConcordSpeedsIt()
    {
        var (_, t) = Town();
        var r = Union.Rules(t);
        t.Union.Bar = 0;
        Assert.Equal(1 + r.ConcordLore, Union.LoreFactor(t), 6);
        t.Union.Bar = -40;
        Assert.Equal(1 - r.Sulk, Union.LoreFactor(t), 6);
    }

    [Fact]
    public void HearingASideOutMovesTheBar()
    {
        var (sim, t) = Town();
        t.Book(TreasuryAccount.Stories, 1000);
        double before = Union.Target(t);
        Assert.True(sim.HearUnion("mages"));
        Assert.True(Union.Target(t) > before);
        Assert.False(sim.HearUnion("nobody"));
    }

    [Fact]
    public void SidingWithTheMovementBansHedgeMagicOutsideItAndLeavesAGrudge()
    {
        var (sim, t) = Town();
        var outside = t.Residents.First(a => a.Hero == null && a.JobId != null && !Union.Joined(t, a, asMage: true));
        var inside = t.Residents.First(a => a.Hero == null && a.JobId != null && Union.Joined(t, a, asMage: true));
        outside.HedgeSpell = "farm";
        inside.HedgeSpell = "farm";
        // A hedge mage in the movement widens the middle.
        Assert.True(Union.Concord(t) > Union.Rules(t).Concord);
        Assert.True(sim.TakeSide("union"));
        Assert.Equal("", outside.HedgeSpell);
        // Magic users can be in the movement too: they keep their spell.
        Assert.Equal("farm", inside.HedgeSpell);
        Assert.True(Union.Banned(t));
        Assert.Equal(Union.Rules(t).GuildsLore, Union.LoreFactor(t), 6);
        Assert.False(sim.TakeSide("mages"));
        // The season passes; the mages keep their grudge.
        t.Union.SideUntil = t.Day;
        sim.RunDays(1);
        Assert.False(Union.Sided(t));
        Assert.True(t.Union.Grudge < 0);
    }

    [Fact]
    public void SidingWithTheMagesSoursEveryGuild()
    {
        var (sim, t) = Town();
        var before = t.Guilds.Select(g => g.Standing).ToList();
        Assert.True(sim.TakeSide("mages"));
        for (int i = 0; i < t.Guilds.Count; i++) Assert.True(t.Guilds[i].Standing < before[i] || t.Guilds[i].Standing == 0);
        Assert.Equal(Union.Rules(t).MagesLore, Union.LoreFactor(t), 6);
    }
}
