using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class KnowledgeTests
{
    /// <summary>Run a town until a party with a caster has just marched out.</summary>
    private static (Simulation Sim, Party Party) PartyWithCaster()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            for (int day = 0; day < 120; day++)
            {
                sim.RunDays(1);
                var t = sim.Town;
                var p = t.Parties.FirstOrDefault(x => !x.Returned && x.DepartTick == t.Tick && Knowledge.Slots(t, x) > 0);
                if (p != null) return (sim, p);
            }
        }
        throw new InvalidOperationException("No party with a caster in 30 seeds");
    }

    [Fact]
    public void ASendingCostsASlotAndAnswersWithWhereThePartyIs()
    {
        var (sim, party) = PartyWithCaster();
        int slots = Knowledge.SlotsLeft(sim.Town, party);
        string? answer = sim.SendWord(party.Id);
        Assert.NotNull(answer);
        Assert.Contains("On the road", answer);
        Assert.Equal(slots - 1, Knowledge.SlotsLeft(sim.Town, party));
        while (Knowledge.SlotsLeft(sim.Town, party) > 0) sim.SendWord(party.Id);
        Assert.Null(sim.SendWord(party.Id));
    }

    [Fact]
    public void ARecalledPartyComesHomeWithoutAChronicleAndTheBountyReopens()
    {
        var (sim, party) = PartyWithCaster();
        var t = sim.Town;
        var site = Quests.Site(t, party.SiteId)!;
        int attempts = site.Attempts;
        for (int i = 0; i < 9; i++) sim.Step();
        // Three hours on the road since dawn: they march the same three hours back.
        double walked = Roads.Marched(t.Content, party.DepartTick, t.Tick);
        Assert.True(walked > 0);
        Assert.NotNull(sim.SendWord(party.Id, recall: true));
        Assert.Equal((int)Math.Ceiling(Roads.Finish(t.Content, t.Tick, walked)), party.ReturnTick);
        Assert.Null(sim.SendWord(party.Id, recall: true));
        sim.RunDays(1);
        Assert.True(party.Returned);
        Assert.DoesNotContain(t.Chronicles, c => c.PartyName == party.Name && c.DepartDay == SimTime.DayOf(party.DepartTick));
        // The recalled trip does not count as an attempt; the reopened bounty may be taken again at once.
        int retakes = t.Parties.Count(p => p.SiteId == party.SiteId && p.DepartTick > party.DepartTick);
        Assert.Equal(attempts - 1 + retakes, site.Attempts);
        // Home unless back on the road with another party or out scouting.
        Assert.All(party.Members, id => Assert.Equal(t.Parties.Any(p => !p.Returned && p.Members.Contains(id)) || t.Scouts.Any(s => s.Out && s.HeroId == id), t.Agent(id).IsAway));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("called home from the road"));
    }

    [Fact]
    public void ScryingPaysTheCasterAndLastsItsHours()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            var t = sim.Town;
            for (int day = 0; day < 150 && Knowledge.Scryers(t).Count == 0; day++) sim.RunDays(1);
            // A town with the Scrying Pool scries for free; this checks the paid kind.
            if (Knowledge.Scryers(t).Count == 0 || Watch.FreeScryingsLeft(t) > 0) continue;
            t.Treasury += 500;
            var caster = Knowledge.Scryers(t)[0];
            double gold = caster.Gold;
            double treasury = t.Treasury;
            // A site the town has heard of and is not already watching.
            var site = t.Sites.First(s => s.Known != SiteKnowledge.Unheard && Knowledge.ActiveScrying(t, s.Def.Id) == null);
            double cost = Knowledge.ScryCost(t, site.Def);
            var scry = sim.CommissionScrying(site.Def.Id);
            Assert.NotNull(scry);
            Assert.Equal(treasury - cost, t.Treasury, 6);
            Assert.Equal(gold + cost, caster.Gold, 6);
            Assert.Null(sim.CommissionScrying(site.Def.Id));
            Assert.NotNull(Knowledge.ActiveScrying(t, site.Def.Id));
            for (int i = 0; i < t.Content.Knowledge.ScryHours; i++) sim.Step();
            Assert.Null(Knowledge.ActiveScrying(t, site.Def.Id));
            return;
        }
        Assert.Fail("No town grew a caster able to scry in 30 seeds");
    }
}
