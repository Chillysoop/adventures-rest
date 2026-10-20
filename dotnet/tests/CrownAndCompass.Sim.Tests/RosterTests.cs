using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class RosterTests
{
    /// <summary>
    /// A town where the player posts nothing, run until some company has three heroes fit to ride and a known,
    /// uncleared place with no bounty on it. A few seeds are tried: when companies form drifts with the layout.
    /// </summary>
    private static (Simulation Sim, string Company, SiteState Site) Ready()
    {
        foreach (int seed in new[] { 3, 2, 4, 5, 6 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoBounties = false, Enemy = false });
            var t = sim.Town;
            for (int d = 0; d < 250; d++)
            {
                sim.RunDays(1);
                var site = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && !t.Bounties.Any(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken))
                    .OrderBy(s => s.BelievedTier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
                if (site == null) continue;
                var company = Roster.Companies(t).FirstOrDefault(c => c.Ready >= 3 && c.Relation >= 40);
                if (company != null) return (sim, company.Name, site);
            }
        }
        throw new Exception("no company was ready to ride");
    }

    [Fact]
    public void TheRosterCountsEachCompanysStrengthAndRecord()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(200);
        var t = sim.Town;
        var roster = Roster.Companies(t);
        Assert.NotEmpty(roster);
        foreach (var e in roster)
        {
            Assert.Equal(e.Members.Sum(m => m.Hero!.Level + m.Hero.Magic.Count), e.Power);
            Assert.Equal(t.Chronicles.Count(c => c.PartyName == e.Name), e.Ventures);
            Assert.True(e.Missions <= e.Ventures);
            Assert.InRange(e.Relation, 0, 100);
        }
        // Ruins cleared go on the clearing company's record as good deeds.
        Assert.Contains(t.Chronicles, c => c.Outcome == ExpeditionOutcome.Cleared);
        Assert.Contains(t.Companies, c => c.GoodDeeds > 0 && c.Deeds.Any(d => d.What.StartsWith("cleared ") || d.What.StartsWith("answered ")));
        Assert.All(t.Companies, c => Assert.True(c.Deeds.Count <= Math.Min(12, c.GoodDeeds)));
    }

    [Fact]
    public void ACompanyAskedByNameTakesAGoodJobAndMarchesOnTheCouncilsCoin()
    {
        var (sim, name, site) = Ready();
        var t = sim.Town;
        t.Treasury = 20000;
        double reward = 3 * Terms.Suggested(t, site, t.Content.Bounties.Term("slay"));
        var view = Roster.Weigh(t, name, site.Def.Id, reward);
        Assert.Null(view.Blocked);
        Assert.True(view.Riders.Count >= 3);
        Assert.Contains(view.Reasons, r => r.Why == "the council asked them by name");
        Assert.Equal(Leaning.Eager, view.Leaning);
        double before = t.Treasury;

        var answer = sim.OfferJob(name, site.Def.Id, reward);
        Assert.NotNull(answer);
        Assert.True(answer!.Accepted);
        var party = answer.Party!;
        Assert.Equal(name, party.Name);
        Assert.Equal(site.Def.Id, party.SiteId);
        Assert.False(party.Returned);
        Assert.True(Intel.Commissioned(t, party));
        var bounty = t.Bounties[party.BountyId!.Value - 1];
        Assert.Equal(BountyStatus.Taken, bounty.Status);
        Assert.Equal(reward, bounty.Reward);
        Assert.True(t.Treasury < before - reward / 2);
        Assert.Equal(1, Intel.Find(t, name)!.Offers);
        // It can't be offered twice.
        Assert.NotNull(Roster.Weigh(t, name, site.Def.Id, reward).Blocked);
        Assert.Null(sim.OfferJob(name, site.Def.Id, reward));
    }

    [Fact]
    public void AMeanOfferIsTurnedDownAndCantBeRepeatedUntilThePayRises()
    {
        var (sim, name, site) = Ready();
        var t = sim.Town;
        t.Treasury = 20000;
        double mean = Math.Max(1, Js.Round(0.1 * Terms.Suggested(t, site, t.Content.Bounties.Term("slay"))));
        var view = Roster.Weigh(t, name, site.Def.Id, mean);
        Assert.Null(view.Blocked);
        Assert.Equal(Leaning.Unwilling, view.Leaning);
        double dealings = Intel.Find(t, name)?.Dealings ?? 0, before = t.Treasury;

        var answer = sim.OfferJob(name, site.Def.Id, mean);
        Assert.NotNull(answer);
        Assert.False(answer!.Accepted);
        Assert.Contains("is an insult", answer.Why);
        var c = Intel.Find(t, name)!;
        Assert.Equal(1, c.Declined);
        Assert.True(c.Dealings < dealings);
        Assert.Equal(before, t.Treasury);
        Assert.DoesNotContain(t.Bounties, b => b.SiteId == site.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken);
        // The same offer the same day gets no hearing; a better one does.
        Assert.NotNull(Roster.Weigh(t, name, site.Def.Id, mean).Blocked);
        Assert.Null(Roster.Weigh(t, name, site.Def.Id, mean + 50).Blocked);
    }

    [Fact]
    public void DangerBeyondTheirStrengthCountsAgainstAJob()
    {
        var (sim, name, site) = Ready();
        var t = sim.Town;
        t.Treasury = 20000;
        double reward = Terms.Suggested(t, site, t.Content.Bounties.Term("slay"));
        var easy = Roster.Weigh(t, name, site.Def.Id, reward);
        int tier = site.BelievedTier;
        site.BelievedTier = 6;
        var hard = Roster.Weigh(t, name, site.Def.Id, reward);
        site.BelievedTier = tier;
        Assert.Contains(hard.Reasons, r => r.Why.Contains("too much for them") && r.Points < 0);
        Assert.True(hard.Score < easy.Score);
    }
}
