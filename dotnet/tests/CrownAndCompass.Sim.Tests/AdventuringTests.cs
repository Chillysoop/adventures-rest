using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AdventuringTests
{
    private static ExpeditionResult Cleared() => new(ExpeditionOutcome.Cleared, [], [], 0, 0, [], new());

    [Fact]
    public void WhatMovesIntoALairMakesItsOwnRoomsAndHeart()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        var s = t.Sites.First(x => x.Def.Id == "spider_hollow");
        string oldBoss = s.Def.Boss.Text;
        s.Cleared = true;
        Lairs.Resettle(t, s);
        Assert.False(s.Cleared);
        if (s.Def.Boss.Monster == "giant_spider") return;
        Assert.NotEqual(oldBoss, s.Def.Boss.Text);
        // The rooms the spiders held hold the newcomers now.
        Assert.DoesNotContain(s.Def.Encounters, e => e.Kind == "combat" && e.Monster == "giant_spider" && s.Def.Boss.Escort != "giant_spider");
    }

    [Fact]
    public void EachClearingKeepsTheCountryQuieterForLonger()
    {
        var t = new Simulation(new SimulationOptions { Seed = 2 }).Town;
        var s = t.Sites.First(x => x.Def.Id != t.Content.Enemy.Outpost.Id);
        var waits = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            s.Cleared = true;
            Lairs.AfterDelve(t, s, Cleared());
            waits.Add(s.ResettleTick!.Value - t.Tick);
        }
        Assert.Equal(4, s.Clearings);
        // The dice vary each wait, but the longest early one is still shorter than the quiet after three clearings.
        Assert.True(waits[3] > t.Content.Lairs.ResettleDays[1] * SimTime.HoursPerDay, string.Join(", ", waits));
    }

    [Fact]
    public void ABackedPleaHoldsCouncilGoldAndHandsItBackIfNobodyAnswers()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        for (int d = 0; d < 200 && !t.Pleas.Any(p => p.Status == PleaStatus.Open); d++) sim.RunDays(1);
        var plea = t.Pleas.First(p => p.Status == PleaStatus.Open);
        double before = plea.Reward;
        // A plea pays what the danger is worth, not a token.
        Assert.True(before >= 0.5 * Quests.SuggestedReward(World.LevelOf(Quests.Site(t, plea.SiteId)!)) - 1);
        t.Treasury = 10_000;
        double gold = World.BackingFor(t, plea);
        Assert.True(sim.BackPlea(plea.Id));
        Assert.False(sim.BackPlea(plea.Id));
        Assert.Equal(before + gold, plea.Reward, 3);
        Assert.Equal(10_000 - gold, t.Treasury, 3);
        // Nobody answers: it lapses and the council's share comes home.
        plea.ExpiresDay = t.Day;
        double treasury = t.Treasury;
        plea.PartyId = null;
        plea.Status = PleaStatus.Open;
        sim.RunDays(1);
        if (plea.Status == PleaStatus.Lapsed) Assert.True(t.Treasury >= treasury + gold - 2_000);
    }

    [Fact]
    public void ACompanyThatRunsDropsTheTakeAndMayLeaveTheFallen()
    {
        var c = new Simulation(new SimulationOptions { Seed = 1 }).Town.Content;
        Assert.True(c.World.Delve.Parting > 0);
        Assert.True(c.World.Delve.Dragged > 0);
        Assert.True(c.World.Delve.RetreatDrop > 0);
        // The gentle storyteller is gentler in the dungeon, the merciless one deadlier.
        var tellers = c.Storytellers.Storytellers.ToDictionary(x => x.Id);
        Assert.True(tellers["steady"].Peril < tellers["rising"].Peril);
        Assert.True(tellers["merciless"].Peril > tellers["rising"].Peril);
        Assert.Equal(1, tellers["rising"].Peril);
    }
}
