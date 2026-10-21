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
        // Home unless back on the road with another party, out scouting or off exploring.
        Assert.All(party.Members, id => Assert.Equal(t.Parties.Any(p => !p.Returned && p.Members.Contains(id)) || t.Scouts.Any(s => s.Out && s.HeroId == id)
            || t.Surveys.Any(s => Explore.Out(s) && s.Members.Contains(id)), t.Agent(id).IsAway));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("called home from the road"));
    }

    /// <summary>A town with a caster able to scry and no Scrying Pool, with gold to spare.</summary>
    private static (Simulation Sim, Agent Caster) TownWithScryer()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            var t = sim.Town;
            for (int day = 0; day < 150 && Knowledge.Rested(t).Count == 0; day++) sim.RunDays(1);
            // A town with the Scrying Pool scries for free; these check the casters.
            if (Knowledge.Rested(t).Count == 0 || Watch.FreeScryingsLeft(t) > 0) continue;
            t.Treasury += 500;
            return (sim, Knowledge.Rested(t)[0]);
        }
        throw new InvalidOperationException("No town grew a caster able to scry in 30 seeds");
    }

    private static List<SiteState> Unwatched(Town t) =>
        t.Sites.Where(s => s.Known != SiteKnowledge.Unheard && Knowledge.ActiveScrying(t, s.Def.Id) == null).ToList();

    [Fact]
    public void ScryingPaysTheCasterAndLastsItsHours()
    {
        var (sim, caster) = TownWithScryer();
        var t = sim.Town;
        caster.Hero!.Loyalty = 55;
        double gold = caster.Gold;
        double treasury = t.Treasury;
        var site = Unwatched(t)[0];
        double cost = Knowledge.ScryCost(t, site.Def);
        var scry = sim.CommissionScrying(site.Def.Id, caster.Id);
        Assert.NotNull(scry);
        Assert.Equal(treasury - cost, t.Treasury, 6);
        Assert.Equal(gold + cost, caster.Gold, 6);
        Assert.Null(sim.CommissionScrying(site.Def.Id));
        Assert.NotNull(Knowledge.ActiveScrying(t, site.Def.Id));
        for (int i = 0; i < t.Content.Knowledge.ScryHours; i++) sim.Step();
        Assert.Null(Knowledge.ActiveScrying(t, site.Def.Id));
    }

    [Fact]
    public void CastersAskByTheirLoyaltyAndRestBetweenScryings()
    {
        var (sim, caster) = TownWithScryer();
        var t = sim.Town;
        var k = t.Content.Knowledge;
        var site = Unwatched(t)[0];

        caster.Hero!.Loyalty = k.ScryPrice.Free;
        Assert.Equal(ScryPrice.Free, Knowledge.Offer(t, caster, site.Def).Price);
        caster.Hero.Loyalty = k.ScryPrice.Favour;
        Assert.Equal(ScryPrice.Gold, Knowledge.Offer(t, caster, site.Def).Price);
        caster.Hero.Loyalty = k.ScryPrice.Favour - 10;
        Assert.Equal(ScryPrice.Favour, Knowledge.Offer(t, caster, site.Def).Price);

        // A favour costs no gold, only the caster's goodwill.
        double treasury = t.Treasury;
        double loyalty = caster.Hero.Loyalty;
        Assert.NotNull(sim.CommissionScrying(site.Def.Id, caster.Id));
        Assert.Equal(treasury, t.Treasury, 6);
        Assert.Equal(loyalty - k.ScryPrice.FavourLoyalty, caster.Hero.Loyalty, 6);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("as a favour"));

        // Spent: the caster rests, and scries again once the rest is over.
        Assert.DoesNotContain(caster, Knowledge.Rested(t));
        var next = Unwatched(t).First(s => s.Def.Id != site.Def.Id);
        Assert.Null(sim.CommissionScrying(next.Def.Id, caster.Id));
        while (t.Tick < caster.Hero.ScryRestUntil) sim.Step();
        Assert.Contains(caster, Knowledge.Rested(t));
    }

    [Fact]
    public void AWatchtowerSeesTheSitesNearItLiveAndFartherAsTheTownGrows()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        sim.RunDays(30);
        t.Treasury += 500;
        var w = t.Content.Knowledge.Watchtowers;
        Assert.Equal(w.Sight, Watch.TowerSight(t) - w.SightPerHundred * t.Population / 100.0, 6);
        // Raise one at the town's edge facing the nearest site, then finish it.
        var site = t.Sites.Where(s => s.Def.Distance > 0).OrderBy(s => s.Def.Distance).ThenBy(s => s.Def.Id, StringComparer.Ordinal).First();
        var (sx, sy) = Watch.SiteTiles(t, site);
        double dx = sx - Town.Center, dy = sy - Town.Center, d = Js.Hypot(dx, dy);
        Building? tower = null;
        for (double r = 14; r > 2 && tower == null; r -= 1) tower = sim.Place(w.Building, Town.Center + dx / d * r, Town.Center + dy / d * r);
        Assert.NotNull(tower);
        Assert.Null(Watch.TowerOver(t, site));
        tower!.BuildProgress = tower.Def.BuildHours;
        var lot = t.Lot(tower.LotId);
        bool inSight = Js.Hypot(sx - lot.X, sy - lot.Y) <= Watch.TowerSight(t);
        Assert.Equal(inSight, Watch.LiveOf(t, site) == LiveSource.Tower);
        // A bigger town sees farther, up to the most.
        Assert.True(Watch.TowerSight(t) > w.Sight);
        Assert.True(Watch.TowerSight(t) <= w.MostSight);
        // One tower for every hundred residents.
        Assert.Equal(t.Population < 2 * w.ResidentsEach, Watch.TowerBlocked(t, tower.Def) != null);
    }
}
