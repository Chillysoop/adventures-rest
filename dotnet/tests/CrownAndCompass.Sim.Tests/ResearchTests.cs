using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ResearchTests
{
    private static Simulation Player(int seed = 2, bool auto = false) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = auto, AutoStories = false, FreeCompanies = false });

    private static Agent Wizard(Town t, int level) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "wizard"), level);

    [Fact]
    public void EveryDiscoveryIsWellFormed()
    {
        var c = Content.Default();
        var r = c.Research;
        Assert.Equal(r.Discoveries.Count, r.Discoveries.Select(d => d.Id).Distinct().Count());
        var sim = Player();
        foreach (var d in r.Discoveries)
        {
            Assert.InRange(d.Rank, 1, 5);
            Assert.NotEmpty(d.Sparks);
            Assert.Contains(d.Field, new[] { "arcana", "crafts", "husbandry", "healing", "trade", "war", "lore" });
            // Every token names something the content knows (an unknown token never holds and reads as itself).
            foreach (string token in d.Sparks.Concat(d.Requires))
            {
                var p = token.Split(':');
                switch (p[0])
                {
                    case "craft": Assert.Contains(c.Buildings, b => b.Profession == p[1]); break;
                    case "folk": Assert.Contains(c.Races, x => x.Id == p[1]); break;
                    case "partner": Assert.Contains(c.World.Peoples, x => x.Id == p[1]); break;
                    case "building": Assert.Contains(c.Buildings, b => b.Id == p[1]); break;
                    case "book": Assert.Contains(p[1], r.Schools); break;
                    case "known": Assert.NotNull(r.Discovery(p[1])); break;
                    case "tier": Assert.Contains(c.City.Tiers, x => x.Id == p[1]); break;
                    case "books" or "wizard" or "circle": break;
                    default: Assert.Fail($"{d.Id}: unknown token {token}"); break;
                }
            }
            if (d.Field == "arcana") Assert.Contains(d.Requires, x => x.StartsWith("circle:", StringComparison.Ordinal));
        }
    }

    [Fact]
    public void WhoLivesInTownRevealsTheTreeAndTheScholarsStudyIt()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Assert.DoesNotContain("hardy-flocks", t.Research.Revealed.Keys);
        // Three shepherds spark hardy flocks.
        foreach (var a in t.Residents.Where(a => a.Hero == null).OrderBy(a => a.Id).Take(3)) a.Profession = "shepherd";
        sim.RunDays(1);
        Assert.Contains("hardy-flocks", t.Research.Revealed.Keys);
        Assert.Contains("shepherds", t.Research.Revealed["hardy-flocks"]);

        t.Treasury += 5000;
        double before = t.Treasury;
        Assert.True(sim.StudyDiscovery("hardy-flocks"));
        Assert.False(sim.StudyDiscovery("hardy-flocks"));
        Assert.False(sim.StudyDiscovery("the-great-working"));
        Assert.Equal(before - Research.Commission(t, Research.Def(t, "hardy-flocks")), t.Treasury, 3);
        Assert.True(sim.FundResearch(3));
        Assert.True(Research.Lore(t) > 0);
        double wool = Decrees.Yield(t, Good.Wool);
        for (int d = 0; d < 600 && !Research.IsKnown(t, "hardy-flocks"); d++)
        {
            // The council spends freely; keep the scholars paid.
            t.Treasury = Math.Max(t.Treasury, 2000);
            t.Research.Funding = 3;
            sim.RunDays(1);
        }
        Assert.True(Research.IsKnown(t, "hardy-flocks"));
        Assert.Null(t.Research.Study);
        Assert.True(Decrees.Yield(t, Good.Wool) > wool);
    }

    [Fact]
    public void FundingCostsGoldEachDayAndLapsesWhenTheTreasuryRunsDry()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Assert.True(sim.FundResearch(2));
        Assert.False(sim.FundResearch(2));
        Assert.False(sim.FundResearch(9));
        double spent = t.Accounts.Get(TreasuryAccount.Research, 0);
        sim.RunDays(3);
        Assert.Equal(spent - 3 * Research.FundingCost(t, 2), t.Accounts.Get(TreasuryAccount.Research, 0), 3);
        t.Treasury = 0;
        sim.RunDays(1);
        Assert.Equal(0, t.Research.Funding);
    }

    [Fact]
    public void BooksAndWizardsRaiseTheLibraryAndItsCircle()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Assert.Equal(0, Research.Circle(t));
        Research.Add(t, 3, "a test", "transmutation");
        // A book with no wizard to read it casts nothing.
        Assert.Equal(0, Research.Circle(t));
        var w = Wizard(t, 5);
        Assert.Equal(3, Research.Circle(t));
        Assert.Equal(3 + 5, Research.Power(t));
        // Plant growth needs a book of transmutation and the third circle; it is revealed by the book.
        sim.RunDays(1);
        Assert.Contains("plant-growth", t.Research.Revealed.Keys);
        Assert.True(Research.Available(t, Research.Def(t, "plant-growth")));
        w.Hero!.Level = 3;
        Assert.Equal(2, Research.Circle(t));
        Assert.Contains(Research.Missing(t, Research.Def(t, "plant-growth")), m => m.Contains("third circle"));
    }

    [Fact]
    public void AStrongLibraryDrawsAStrongerWizard()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        for (int i = 0; i < 8; i++) Research.Add(t, 5, "a test");
        int before = t.Heroes.Where(a => a.Hero!.Cls.Id == "wizard").Select(a => a.Hero!.Level).DefaultIfEmpty(0).Max();
        sim.RunDays(1);
        var drawn = Assert.Single(t.Research.Drawn);
        var w = t.Agent(drawn);
        Assert.Equal("wizard", w.Hero!.Cls.Id);
        Assert.True(w.Hero.Level > before);
        Assert.True(w.Hero.Level > 1);
        sim.RunDays(1);
        Assert.Single(t.Research.Drawn);
    }

    [Fact]
    public void KnownDiscoveriesReachTheirSystems()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        int cap = City.HeroCap(t);
        double defence = Threat.Defense(t);
        t.Research.Known.Add("field-surgery");
        t.Research.Known.Add("guards-and-wards");
        Assert.Equal(cap + 1, City.HeroCap(t));
        Assert.True(Threat.Defense(t) > defence);
        Assert.Equal(0.5 + 0.0, Research.Ward(t), 6);
    }

    [Fact]
    public void TheStandInFundsAndPicksWhatToStudy()
    {
        var sim = Player(auto: true);
        var t = sim.Town;
        t.Treasury = 5000;
        for (int d = 0; d < 60 && t.Research.Study == null && t.Research.Known.Count == 0; d++) sim.RunDays(1);
        Assert.True(t.Research.Funding > 0);
        Assert.True(t.Research.Study != null || t.Research.Known.Count > 0);
    }
}
