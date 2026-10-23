using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CompaniesTests
{
    private static Simulation Run(int seed, int days)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void HeroesRideWithTheirOwnCompanyAndNoTwoCompaniesShareAWord()
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var t = Run(seed, 200).Town;
            // Each hero's trips went out under one or two names (a lone survivor may join another company).
            var banners = new Dictionary<int, HashSet<string>>();
            foreach (var p in t.Parties)
                foreach (int id in p.Members)
                    (banners.TryGetValue(id, out var set) ? set : banners[id] = []).Add(p.Name);
            Assert.True(banners.Count > 0, $"seed {seed}: nobody rode out");
            Assert.All(banners, kv => Assert.True(kv.Value.Count <= 2, $"seed {seed}: hero {kv.Key} rode as {string.Join(", ", kv.Value)}"));
            var riding = Intel.MembersByCompany(t);
            var words = riding.SelectMany(x => x.Name.Split(' ')).ToList();
            Assert.Equal(words.Count, words.Distinct().Count());
            // A company with a bastion takes in guests who live there; the rest keep a party's roll.
            Assert.All(riding.Where(x => !t.Bastions.Any(b => b.Company == x.Name)),
                x => Assert.True(x.Members.Count <= t.Content.World.Companies.MaxMembers, $"seed {seed}: the {x.Name} have {x.Members.Count}"));
        }
    }

    [Fact]
    public void ANewcomerJoinsACompanyShortOfAFullRoll()
    {
        var t = Run(7, 120).Town;
        var shortOf = Intel.MembersByCompany(t).FirstOrDefault(x => x.Members.Count is > 1 and < 4);
        var full = Intel.MembersByCompany(t).First();
        string name = shortOf.Name ?? full.Name;
        // Make room if every company is full.
        if (shortOf.Name == null) full.Members[^1].Hero!.PartyName = null;
        var best = Intel.Members(t, name)[0];
        var newcomer = t.Heroes.First(h => h.Hero!.PartyName != name && h.Hero.Duty == HeroDuty.None);
        newcomer.Hero!.PartyName = null;
        newcomer.Hero.AwayUntil = null;
        newcomer.Hero.Level = best.Hero!.Level;
        foreach (var m in Intel.Members(t, name))
        {
            m.Opinions[newcomer.Id] = 0;
            newcomer.Opinions[m.Id] = 0;
        }
        Muster.Daily(t);
        Assert.NotNull(newcomer.Hero.PartyName);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"{newcomer.Name} joins the "));
    }

    [Fact]
    public void ACompanyShortOfASitesRenownMeetsMoreFoes()
    {
        var t = Run(3, 60).Town;
        var site = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard).OrderByDescending(s => s.BelievedTier).First();
        int tier = Math.Max(1, site.BelievedTier);
        Assert.True(tier >= 3, $"wants a site of tier 3 or more, got {tier}");
        var c = Intel.Company(t, "Test Lanterns");
        c.Renown = 0;
        Assert.True(Notoriety.Swell(t, c.Name, site) > 0);
        c.Renown = Notoriety.Suggested(t, tier);
        Assert.Equal(0, Notoriety.Short(t, c.Name, site));
        Assert.Equal(0, Notoriety.Swell(t, c.Name, site));
        // The danger bars read it too.
        var heroes = t.Heroes.Take(4).ToList();
        c.Renown = 0;
        var unknown = Assess.DangerFor(t, site, heroes, c.Name);
        c.Renown = 10_000;
        var famed = Assess.DangerFor(t, site, heroes, c.Name);
        Assert.True(unknown.Step >= famed.Step);
    }

    [Fact]
    public void ACompanysNewStepOfFameLiftsTheMandate()
    {
        var t = Run(5, 60).Town;
        var (name, _) = Intel.MembersByCompany(t).First();
        var c = Intel.Company(t, name);
        c.Renown = 0;
        t.Mandate = 50;
        Notoriety.CompanyDeed(t, name, t.Content.Notoriety.Fame[1].At);
        Assert.Equal(50 + t.Content.Notoriety.Renown.TownMandate, t.Mandate);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"The {name} are known in town now"));
    }

    [Fact]
    public void ANotablesQuestGoesOnTheBoardOnTheirGoldAndPaysRegardWhenDone()
    {
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            var sim = Run(seed, 90);
            var t = sim.Town;
            var n = Notables.Active(t).FirstOrDefault(x => x.AgentId != null && Notables.DefOf(t, x).Asks.Any(a => a.Quest != ""));
            if (n == null) continue;
            var ask = Notables.DefOf(t, n).Asks.First(a => a.Quest != "");
            var site = t.Sites.Where(s => s.Known != SiteKnowledge.Unheard && !s.ThoughtCleared && !t.Bounties.Any(b => b.SiteId == s.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken))
                .Where(s => Systems.Terms.Blocked(t, s, t.Content.Bounties.Term(ask.Quest)) == null)
                // A sealed level of the undercity takes no job until it stirs (Notables.Open).
                .Where(s => !Systems.Undercity.IsUnder(t, s) || Systems.Undercity.Stirring(t, s))
                .OrderBy(s => s.BelievedTier).FirstOrDefault();
            if (site == null) continue;
            var patron = t.Agent(n.AgentId!.Value);
            patron.Gold = 10_000;
            n.Ask = ask.Id;
            n.AskSite = site.Def.Id;
            n.AskUntil = t.Day + 5;
            double treasury = t.Treasury;
            sim.AnswerNotable(n.Id, true);
            var b = t.Bounties.Last();
            Assert.Equal(n.Id, b.NotableId);
            Assert.Equal(patron.Id, b.PatronId);
            Assert.Equal(10_000 - b.Reward, patron.Gold);
            Assert.Equal(treasury, t.Treasury);
            Assert.Contains(site.Def.Name, Notables.AskText(t, n, ask));
            // Done: the notable thinks the better of the mayor and the company earns renown.
            double regard = n.Regard;
            var party = new Party { Id = 999, Name = "Test Lanterns", Members = [], SiteId = site.Def.Id };
            b.Status = BountyStatus.Completed;
            Notables.QuestHome(t, b, party, true);
            Assert.Equal(Math.Min(100, regard + ask.Gain), n.Regard);
            Assert.Equal(t.Content.Notables.QuestFame, Intel.Find(t, "Test Lanterns")!.Renown);
            return;
        }
        Assert.Fail("no seed had a notable with a quest to give");
    }
}
