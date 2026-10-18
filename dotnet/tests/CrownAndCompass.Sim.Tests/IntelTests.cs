using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class IntelTests
{
    /// <summary>A campaign where companies roam unpaid, with a withheld report that shows new ground.</summary>
    private static (Simulation Sim, Party Party) Withheld()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoBounties = false, Enemy = false });
        var t = sim.Town;
        for (int d = 0; d < 200; d++)
        {
            sim.RunDays(1);
            var p = Intel.ForSale(t).FirstOrDefault(x => Intel.NewGround(t, x) > 0 && World.RegionOf(t, Quests.Site(t, x.SiteId)!) is { Visited: false });
            if (p != null) return (sim, p);
        }
        throw new Exception("no company came home with new ground");
    }

    [Fact]
    public void OnlyCouncilBountiesPutWhatAPartySawOnTheMap()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(150);
        var t = sim.Town;
        var home = t.Parties.Where(p => p.Returned && p.RecalledTick == null).ToList();
        Assert.Contains(home, p => p.Withheld || p.SoldTick != null);
        foreach (var p in home)
        {
            bool council = Intel.Commissioned(t, p);
            if (council) Assert.False(p.Withheld);
            if (p.SoldTick != null) Assert.False(council);
        }
    }

    [Fact]
    public void BuyingAReportDescribesTheRegionAndPaysTheTellers()
    {
        var (sim, party) = Withheld();
        var t = sim.Town;
        t.Treasury = 5000;
        var region = World.RegionOf(t, Quests.Site(t, party.SiteId)!)!;
        var tellers = Intel.Tellers(t, party);
        double gold = tellers.Sum(m => m.Gold), ask = Intel.Ask(t, party);
        Assert.Null(Intel.WontSell(t, party));

        Assert.Equal(IntelDeal.Bought, sim.BuyIntel(party.Id));
        Assert.True(region.Visited);
        Assert.False(party.Withheld);
        Assert.Equal(ask, party.SoldFor);
        Assert.Equal(gold + ask, tellers.Sum(m => m.Gold), 6);
        Assert.Equal(1, Intel.Find(t, party.Name)!.ReportsSold);
        Assert.Null(sim.BuyIntel(party.Id));
    }

    [Fact]
    public void ACompanyThatDislikesTheTownAsksMoreAndMayRefuse()
    {
        var (sim, party) = Withheld();
        var t = sim.Town;
        var tellers = Intel.Tellers(t, party);
        foreach (var m in tellers) m.Hero!.Loyalty = 90;
        double friendly = Intel.Ask(t, party);
        foreach (var m in tellers) m.Hero!.Loyalty = 30;
        double sour = Intel.Ask(t, party);
        Assert.True(sour > friendly);

        foreach (var m in tellers) m.Hero!.Loyalty = 5;
        Assert.NotNull(Intel.WontSell(t, party));
        t.Treasury = 5000;
        Assert.Null(sim.BuyIntel(party.Id));
    }

    [Fact]
    public void ARefusedHaggleSoursTheCompanyAndItSulks()
    {
        var (sim, party) = Withheld();
        var t = sim.Town;
        t.Treasury = 5000;
        // Barely willing to deal: a haggled offer is almost sure to be turned down.
        foreach (var m in Intel.Tellers(t, party)) m.Hero!.Loyalty = 21;
        var deal = sim.BuyIntel(party.Id, haggle: true);
        if (deal != IntelDeal.Refused) return;
        var c = Intel.Find(t, party.Name)!;
        Assert.True(c.Dealings < 0);
        Assert.Equal(1, c.Refusals);
        Assert.NotNull(Intel.WontSell(t, party));
        Assert.True(party.Withheld);
    }

    [Fact]
    public void AnOldReportDoesNotOverruleNewerWord()
    {
        var (sim, party) = Withheld();
        var t = sim.Town;
        t.Treasury = 5000;
        var site = Quests.Site(t, party.SiteId)!;
        // A council party went later and found it otherwise.
        Rumors.Confirm(t, site, "a later party", !party.SawCleared, party.ReturnTick + 1);
        sim.BuyIntel(party.Id);
        Assert.Equal(!party.SawCleared, site.ThoughtCleared);
    }

    [Fact]
    public void ReportsLoseWorthWithAge()
    {
        var (sim, party) = Withheld();
        var t = sim.Town;
        double fresh = Intel.Worth(t, party);
        int before = t.Tick;
        t.Tick += 30 * SimTime.HoursPerDay;
        double old = Intel.Worth(t, party);
        t.Tick = before;
        Assert.True(old < fresh);
    }
}
