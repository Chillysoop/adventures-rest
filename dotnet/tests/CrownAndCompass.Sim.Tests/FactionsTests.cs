using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FactionsTests
{
    [Fact]
    public void EveryFactionInPlayHasLoreAndStartsDormant()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        Assert.Equal(t.Content.Factions.Factions.Count, t.Factions.Count);
        Assert.All(t.Factions, f => Assert.False(f.Active));
        Assert.Contains(t.Factions, f => f.Secret && !f.Known);
        Assert.Contains(t.Factions, f => !f.Secret && f.Known);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void FactionsTakeRootAsTheTownGrows(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(120);
        var t = sim.Town;
        if (t.Population < 45) return;
        Assert.True(t.Factions.Count(f => f.FoundedTick != null) >= 3);
        foreach (var f in t.Factions.Where(f => f.Active))
        {
            Assert.NotEmpty(f.Members);
            Assert.All(f.Members, id => Assert.False(t.Agent(id).Departed));
        }
        // Nobody belongs to more than the rules allow, nor to two rivals.
        foreach (var a in t.Residents)
        {
            var mine = Factions.Of(t, a);
            Assert.True(mine.Count <= t.Content.Factions.MaxPerAgent);
            Assert.DoesNotContain(mine, m => mine.Any(o => o.Lore.Rivals.Contains(m.Id)));
        }
    }

    [Fact]
    public void ASecretFactionStaysHiddenUntilItsHeatIsHighEnough()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(1);
        var hand = Factions.Find(t, "quiet-hand")!;
        hand.Active = true;
        hand.Members.AddRange(t.Residents.Take(3).Select(a => a.Id));
        var lawgiver = t.Content.Library.Deity("even-scale")!;
        Assert.Same(hand, Factions.Denounce(t, lawgiver, 10));
        Assert.False(hand.Known);
        Factions.Denounce(t, lawgiver, t.Content.Factions.ExposeAt);
        Assert.True(hand.Known);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Faction && e.Text.Contains("names at last", StringComparison.Ordinal));
    }

    [Fact]
    public void ACrackdownRunsTheLeaderOutAndSeizesTheCoffers()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(1);
        var hand = Factions.Find(t, "quiet-hand")!;
        Assert.False(sim.CrackDown("quiet-hand"));
        hand.Active = true;
        hand.Known = true;
        hand.Purse = 50;
        var members = t.Residents.Take(4).ToList();
        hand.Members.AddRange(members.Select(a => a.Id));
        double treasury = t.Treasury;
        Assert.True(sim.CrackDown("quiet-hand"));
        Assert.True(members[0].Departed);
        Assert.All(members.Skip(1), a => Assert.False(a.Departed));
        Assert.Equal(0, hand.Purse);
        Assert.True(t.Treasury > treasury - t.Content.Factions.CrackdownCost);
        Assert.False(hand.Known);
        Assert.True(hand.Members.Count < 4);
    }

    [Fact]
    public void TheCouncilCanOnlyBackAFactionItKnows()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(1);
        var scale = Factions.Find(t, "gilded-scale")!;
        Assert.False(sim.SupportFaction("gilded-scale", 50));
        scale.Active = true;
        scale.Members.Add(t.Residents[0].Id);
        Assert.True(sim.SupportFaction("gilded-scale", 50));
        Assert.Equal(50, Factions.Coffers(t, scale));
        Assert.True(scale.Standing > 0);
        Assert.False(sim.SupportFaction("quiet-hand", 50));
    }

    [Fact]
    public void KnightsStrengthenTheWallsAndAPaidOffCaptainWeakensTheWatch()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        var t = sim.Town;
        sim.RunDays(1);
        double before = Threat.Defense(t);
        var wardens = Factions.Find(t, "dawn-wardens")!;
        wardens.Active = true;
        wardens.Members.AddRange(t.Residents.Take(2).Select(a => a.Id));
        Assert.True(Threat.Defense(t) > before);
        Assert.True(Factions.WatchFactor(t) > 1);

        var hand = Factions.Find(t, "quiet-hand")!;
        var seat = Council.FindSeat(t, SeatId.Captain);
        if (seat == null) return;
        seat.HolderId = t.Residents[3].Id;
        hand.Active = true;
        hand.Members.Add(seat.HolderId.Value);
        Assert.True(Factions.WatchFactor(t) < 1 + t.Content.Factions.WardenWatch);
    }

    [Fact]
    public void TheHollowKingWakesACryptThatHeroesCanClear()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        sim.RunDays(1);
        var choir = Factions.Find(t, "hollow-choir")!;
        choir.Active = true;
        choir.Members.AddRange(t.Residents.Take(2).Select(a => a.Id));
        Faith.Act(t, t.Content.Library.Deity("hollow-king")!);
        var crypt = Quests.Site(t, t.Content.Factions.CryptSite!.Id)!;
        Assert.Equal(SiteKnowledge.Rumoured, crypt.Known);
        Assert.Equal("undead", crypt.Def.Kind);
        crypt.Cleared = true;
        sim.RunDays(1);
        Assert.True(choir.CryptBroken);
        Assert.True(choir.Known || !choir.Active);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void TheSimulationStaysDeterministic(int seed)
    {
        string Run()
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            sim.RunDays(100);
            return string.Join("\n", sim.Town.Ledger.Entries.Select(e => e.Format()));
        }
        Assert.Equal(Run(), Run());
    }
}
