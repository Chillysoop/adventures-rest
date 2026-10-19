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
        // A town big enough with gold to spare has raised one; a poorer one waits for the money. There is always room.
        Assert.NotNull(Shrines.CivicLot(t, t.Content.Building("temple")));
        if (t.Population >= t.Content.Shrines.TemplePopulation + 10 && t.Treasury > Shrines.TempleCost(t) + 250)
            Assert.Contains(t.Buildings, b => b.Def.Id == "temple");
        Assert.True(t.Buildings.Any(b => b.Def.Id == "temple") || Shrines.TempleBlocked(t) != null || t.Treasury <= Shrines.TempleCost(t) + 250);
    }

    [Fact]
    public void CivicWorksFindRoomWhenTheZonesCoverTheOldTown()
    {
        // Seed 1 paints every open cell of the old town with homes and shops by day 60, long before a second ward opens:
        // the temple and the village's civic works still find room on plots nobody has built on yet.
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(70);
        var t = sim.Town;
        Assert.Single(t.City.OpenWards);
        // Raised already, or (when the treasury hasn't had the gold yet) still with room to go.
        Assert.True(t.Buildings.Any(b => b.Def.Id == "temple") || Planning.CivicLot(t, t.Content.Building("temple")) != null);
        foreach (var id in new[] { "watch_house", "granary", "market_hall" })
        {
            var lot = Planning.CivicLot(t, t.Content.Building(id));
            Assert.NotNull(lot);
            Assert.NotEqual(Zone.Farmland, lot!.Zone);
        }
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
