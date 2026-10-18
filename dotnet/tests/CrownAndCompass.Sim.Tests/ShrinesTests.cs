using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ShrinesTests
{
    [Fact]
    public void TheCharterBringsAShrineAndAGeneralStore()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        Assert.NotNull(Shrines.Shrine(t));
        Assert.Contains(t.Buildings, b => b.Def.Id == "general_store" && b.Complete && b.OwnerId != null);
        Assert.Null(Shrines.Temple(t));
        Assert.Equal($"needs {t.Content.Shrines.TemplePopulation} residents", Shrines.TempleBlocked(t));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TownsfolkPrayTakeVowsAndTheTownRaisesATemple(int seed)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(120);
        var t = sim.Town;
        Assert.Contains(t.Residents, a => a.Devout);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Faith);
        if (t.Population >= t.Content.Shrines.TemplePopulation + 10)
            Assert.Contains(t.Buildings, b => b.Def.Id == "temple");
    }

    [Fact]
    public void TheTemplePaysItsOwnBountiesAndTakesBackWhatGoesUnearned()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        var lot = Planning.CivicLot(t, t.Content.Building("temple"))!;
        var temple = t.AddBuilding(t.Content.Building("temple"), lot, null, "the Temple", true);
        temple.Cash = 500;
        var site = t.Sites.First(s => s.Known != SiteKnowledge.Unheard);
        double treasury = t.Treasury;
        var b = Quests.PostBounty(t, site.Def.Id, 140, temple)!;
        Assert.Equal(360, temple.Cash);
        Assert.Equal(treasury, t.Treasury);
        Assert.Equal(temple.Id, b.FunderId);
        // Nobody takes it in time: the coffers get it back.
        double before = temple.Cash;
        int posted = t.Bounties.Count;
        while (b.Status == BountyStatus.Open)
        {
            before = temple.Cash;
            posted = t.Bounties.Count;
            sim.RunDays(1);
        }
        // The same morning the temple may already put the money on its next crusade.
        double reposted = t.Bounties.Skip(posted).Where(x => x.FunderId == temple.Id).Sum(x => x.Reward);
        if (b.Status == BountyStatus.Expired) Assert.True(temple.Cash + reposted - before > 100);
    }

    [Fact]
    public void HeroesCarryPotionsFromTheStore()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(60);
        var t = sim.Town;
        var store = t.Buildings.First(b => b.Def.Id == "general_store");
        Assert.True(t.Parties.Count > 0);
        Assert.Contains(t.Chronicles.SelectMany(c => c.Lines), l => l.Contains("potion of healing"));
    }
}
