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
                    case "found": Assert.Contains(p[1], new[] { "lair", "settlement", "company", "caravan", "font", "ruin" }); break;
                    case "slain": Assert.Contains(c.Library.Monsters, m => m.Type.Equals(p[1], StringComparison.OrdinalIgnoreCase)); break;
                    case "ally": Assert.Contains(c.Powers.Powers, x => x.Id == p[1]); break;
                    case "books" or "wizard" or "circle" or "routes" or "delves" or "cleared" or "heroes" or "hero": break;
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
        // The idea takes shape over days before it is revealed.
        Assert.DoesNotContain("hardy-flocks", t.Research.Revealed.Keys);
        Assert.Equal(1, t.Research.Leads["hardy-flocks"].Days);
        sim.RunDays(Research.BrewDays(t, Research.Def(t, "hardy-flocks")) - 1);
        Assert.Contains("hardy-flocks", t.Research.Revealed.Keys);
        Assert.DoesNotContain("hardy-flocks", t.Research.Leads.Keys);
        Assert.Contains("shepherds", t.Research.Revealed["hardy-flocks"]);
        Assert.Equal(Sparks.Newcomer, t.Research.RevealIcon["hardy-flocks"]);
        Assert.Contains(t.Research.Feed, n => n.Text.Contains("hardy flocks"));

        // Gold can't buy a study: setting the scholars to it costs nothing.
        t.Treasury += 5000;
        double before = t.Treasury;
        Assert.True(sim.StudyDiscovery("hardy-flocks"));
        Assert.False(sim.StudyDiscovery("hardy-flocks"));
        Assert.False(sim.StudyDiscovery("the-great-working"));
        Assert.Equal(before, t.Treasury, 3);
        Assert.True(sim.FundResearch(3));
        Assert.True(Research.Lore(t) > 0);
        double wool = Decrees.Yield(t, Good.Wool);
        // A bare town's clerks alone are slow; a scholar's finds carry the work.
        Research.Insight(t, Sparks.Book, "a test", Research.LoreCost(t, Research.Def(t, "hardy-flocks")) - 10);
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
        sim.RunDays(Research.BrewDays(t, Research.Def(t, "plant-growth")));
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

    [Fact]
    public void FindsInTheWildsMoveTheScholarsAndRevealIdeas()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        // Idle scholars bank the notes for their next study.
        Research.Insight(t, Sparks.Wilds, "a test", 30);
        Assert.Equal(30, t.Research.Notes, 6);
        // A company that brings down the undead sparks grave wards, which takes shape over days.
        t.Research.Slain["undead"] = 1;
        t.Research.Slayers["undead"] = "the Grey Lanterns at the Barrow";
        sim.RunDays(Research.BrewDays(t, Research.Def(t, "grave-wards")));
        Assert.Equal("the Grey Lanterns at the Barrow", t.Research.Revealed["grave-wards"]);
        Assert.Equal(Sparks.Slain, t.Research.RevealIcon["grave-wards"]);
        Assert.True(sim.StudyDiscovery("grave-wards"));
        Assert.Equal(0, t.Research.Notes);
        Assert.True(t.Research.Progress["grave-wards"] >= 30);
        // Insight goes straight into the study under way, and shows in the feed.
        double had = t.Research.Progress["grave-wards"];
        Research.Insight(t, Sparks.Book, "a book", 12);
        Assert.Equal(had + 12, t.Research.Progress["grave-wards"], 6);
        Assert.Equal(12, t.Research.Feed[^1].Lore);
    }

    [Fact]
    public void SlainSparksCountWhatCompaniesBroughtDown()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var site = t.Sites.First(s => Sparks.CreatureType(t, s.Def.Boss.Monster).Length > 0);
        string type = Sparks.CreatureType(t, site.Def.Boss.Monster);
        Assert.Equal(new Spark(false, $"a slain {type}", Sparks.Slain), Sparks.Check(t, $"slain:{type}"));
        t.Research.Slain[type] = 1;
        Assert.True(Sparks.Check(t, $"slain:{type}").Holds);
        Assert.False(Sparks.Check(t, $"slain:{type}:2").Holds);
    }
}
