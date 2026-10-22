using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class LoyaltyTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Veteran(Town t, int level = 4)
    {
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), level);
        a.Hero!.Renown = 20;
        return a;
    }

    [Fact]
    public void KnightingNeedsRenownAndPaysAStipend()
    {
        var (sim, t) = NewTown();
        var green = Quests.CreateHero(t, t.Content.Classes[0], 1);
        Assert.Equal("needs level 3", Loyalty.KnightBlocked(t, green));
        Assert.False(sim.Knight(green.Id));

        var a = Veteran(t);
        double loyalty = a.Hero!.Loyalty;
        double treasury = t.Treasury;
        Assert.True(sim.Knight(a.Id));
        Assert.True(a.Hero.Knight);
        Assert.Equal($"Ser {a.Name}", Loyalty.Styled(t, a));
        Assert.True(a.Hero.Loyalty > loyalty);
        Assert.Equal(treasury - t.Content.Loyalty.Knight.Cost, t.Treasury);
        Assert.Equal("already a knight", Loyalty.KnightBlocked(t, a));
        // The stipend comes out of the treasury each day.
        sim.RunDays(1);
        Assert.True(t.Accounts.Get(TreasuryAccount.Honours, 0) < -t.Content.Loyalty.Knight.Cost);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"Rise, Ser {a.GivenName}"));
    }

    [Fact]
    public void AKnightsLandsAreTheirOwnToKeepAtNoCostToTheTown()
    {
        var (sim, t) = NewTown();
        var a = Veteran(t);
        sim.Knight(a.Id);
        var site = t.Sites.First(s => !s.Generated && s.Def.Id != t.Content.Enemy.Outpost.Id);
        Assert.Equal("only a cleared site can be granted", Loyalty.FiefBlocked(t, a, site));
        site.Cleared = site.ThoughtCleared = true;
        // A knight with a purse and no greed builds a tower from it.
        a.Gold = 1000;
        Assert.True(sim.GrantFief(a.Id, site.Def.Id));
        Assert.Equal(Loyalty.Choose(t, a), a.Hero!.FiefWay);
        if (a.Hero.FiefWay == "tower")
        {
            Assert.NotNull(site.TowerDue);
            Assert.Equal(1000 - t.Content.Lairs.Outpost.Cost, a.Gold);
        }
        // The town has no say on a knight's land.
        Assert.Equal("a knight's land", Lairs.HoldBlocked(t, site));
        Assert.Equal("a knight's land", Lairs.PatrolBlocked(t, site));
        Assert.Same(a, Loyalty.Lord(t, site.Def.Id));
        Assert.EndsWith($" of {site.Def.Name}", Loyalty.Styled(t, a));
        sim.RunDays(t.Content.Lairs.Outpost.BuildDays + 1);
        Assert.Equal(0, t.Accounts.Get(TreasuryAccount.Outposts, 0));
        Assert.Equal(site.Def.Id, a.Hero.FiefId);
        Assert.True(a.Hero.FiefWay != "tower" || site.Held);
    }

    [Fact]
    public void AGreedyKnightTakesTheRentsAndLosesTheLandWhenSomethingMovesIn()
    {
        var (sim, t) = NewTown();
        var a = Veteran(t);
        sim.Knight(a.Id);
        a.Traits = new HashSet<Trait>(a.Traits) { Trait.Greedy };
        var site = t.Sites.First(s => !s.Generated && s.Def.Id != t.Content.Enemy.Outpost.Id);
        site.Cleared = site.ThoughtCleared = true;
        Assert.True(sim.GrantFief(a.Id, site.Def.Id));
        Assert.Equal("rent", a.Hero!.FiefWay);
        Assert.False(site.Held);
        site.ResettleTick = t.Tick;
        sim.RunDays(2);
        Assert.False(site.Cleared);
        Assert.Null(a.Hero.FiefId);
    }

    [Fact]
    public void LoyaltyFollowsPayFaithAndTheTownsFortunes()
    {
        var (_, t) = NewTown();
        var a = Veteran(t);
        double before = Loyalty.Target(t, a);
        a.Hero!.Pay = 500;
        Assert.Contains(Loyalty.Factors(t, a), f => f.Why == "paid well by the town" && f.Points > 0);
        Assert.True(Loyalty.Target(t, a) > before);
        t.Mandate = 0;
        t.Saga.Tone = Tone.Dark;
        Assert.Contains(Loyalty.Factors(t, a), f => f.Why == "dark times" && f.Points < 0);
        Assert.True(Loyalty.Target(t, a) < before + 20);
    }

    [Fact]
    public void AHeroWhoHasGivenUpLeavesTown()
    {
        var (sim, t) = NewTown();
        var a = Veteran(t);
        t.Mandate = 0;
        for (int d = 0; d < 60 && !a.Departed; d++)
        {
            a.Hero!.Loyalty = 0;
            a.Gold = 0;
            // Disloyal, not destitute: an empty purse alone would send them off for want of a meal.
            a.MiserableDays = 0;
            sim.RunDays(1);
        }
        Assert.True(a.Departed);
        Assert.False(a.Dead);
        Assert.Contains(t.Ledger.About(a.Id), e => e.Importance == Importance.Major);
    }

    [Fact]
    public void AHeroSwornToASecretFactionRunsToItWithTreasuryGold()
    {
        var (sim, t) = NewTown();
        var a = Veteran(t);
        var hand = Factions.Find(t, "quiet-hand")!;
        hand.Active = true;
        hand.Members.Add(a.Id);
        double treasury = t.Treasury;
        for (int d = 0; d < 60 && !a.Departed; d++)
        {
            a.Hero!.Loyalty = 0;
            sim.RunDays(1);
        }
        Assert.True(a.Departed);
        Assert.Equal("quiet-hand", a.Hero!.DefectedTo);
        Assert.True(t.Accounts.Get(TreasuryAccount.Discrepancies, 0) < 0);
    }

    [Fact]
    public void TraitorsOpenTheGatesInASiege()
    {
        var (_, t) = NewTown();
        double lost = 0;
        var lines = new List<string>();
        var traitors = new List<Agent>();
        for (int i = 0; i < 20 && lost == 0; i++)
        {
            var a = Veteran(t);
            a.Hero!.Loyalty = 0;
            traitors.Add(a);
            lost = Loyalty.Betray(t, "the warband", null, lines);
        }
        Assert.True(lost > 0);
        Assert.Contains(lines, l => l.Contains("gate for the warband"));
        Assert.Contains(traitors, a => a.Departed);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("Treachery at the walls", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCouncilKnightsTheTownsBestOverACampaign()
    {
        bool knighted = false;
        for (int seed = 1; seed <= 3 && !knighted; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(200);
            knighted = sim.Town.Motions.Any(m => m.Kind == MotionKind.Knight && m.Status == MotionStatus.Passed);
        }
        Assert.True(knighted);
    }
}
