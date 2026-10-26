using System.Text;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class NeighboursTests
{
    private static Settlement Known(Town t, Func<Settlement, bool>? where = null)
    {
        var s = t.Settlements.First(s => !s.Razed && (where == null || where(s)));
        t.Regions[s.RegionId].Known = true;
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        return s;
    }

    [Fact]
    public void SettlementsGetLeadersTroublesAndBandsOfTheirOwn()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(200);
        var t = sim.Town;
        Assert.All(t.Settlements.Where(s => !s.Razed), s =>
        {
            Assert.NotEqual("", s.Leader);
            Assert.InRange(s.Prosperity, 0, 100);
            Assert.InRange(s.Security, 0, 100);
            Assert.NotNull(s.Band);
        });
        // Troubles come and go, and the people the town deals with are remembered.
        Assert.Contains(t.Settlements, s => s.Trouble != null || s.NextTroubleDay > t.Day);
        Assert.NotEmpty(t.Neighbours.Cast);
        Assert.All(t.Neighbours.Cast, c => Assert.NotEqual("", c.Name));
    }

    [Fact]
    public void AidEndsANeedAndTheSettlementRemembersWhoSentIt()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        sim.RunDays(1);
        var t = sim.Town;
        var s = Known(t);
        var blight = t.Content.Neighbours.Trouble("blight")!;
        s.Trouble = new SettlementTrouble { Def = "blight", SinceDay = t.Day, Severity = 50 };
        int id = t.Settlements.IndexOf(s);

        t.Treasury = 0;
        Assert.NotNull(Neighbours.AidRefused(t, s));
        Assert.False(sim.AidSettlement(id));

        t.Treasury = 1000;
        t.Market.Seed(blight.Aid!.Good, blight.Aid.Qty);
        double standing = s.Standing;
        Assert.True(sim.AidSettlement(id));
        Assert.Null(s.Trouble);
        Assert.True(s.Standing > standing);
        Assert.Equal(1000 - blight.Aid.Gold, t.Treasury);
        Assert.Contains(Neighbours.CastOf(t, s), c => c.Role == blight.Contact && c.Memories.Count > 0);
    }

    [Fact]
    public void AFundedProjectIsBuiltAfterItsDaysAndChangesTheSettlement()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, AutoMayor = false });
        sim.RunDays(1);
        var t = sim.Town;
        var s = Known(t);
        var tower = t.Content.Neighbours.Project("watchtower")!;
        int id = t.Settlements.IndexOf(s);
        s.Standing = 0;
        t.Treasury = 5000;
        Assert.False(sim.FundProject(id, "watchtower"));

        s.Standing = 40;
        double security = Neighbours.SecurityTarget(t, s);
        Assert.True(sim.FundProject(id, "watchtower"));
        Assert.Equal("watchtower", s.Works);
        Assert.False(sim.FundProject(id, "patrol"));
        sim.RunDays(tower.Days + 1);
        Assert.Contains("watchtower", s.Projects);
        Assert.Null(s.Works);
        Assert.True(Neighbours.SecurityTarget(t, s) > security);
        Assert.False(sim.FundProject(id, "watchtower"));
    }

    [Fact]
    public void VillainsHoldAssetsAndHatchPlans()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        bool planned = false, held = false;
        for (int d = 0; d < 400; d += 10)
        {
            sim.RunDays(10);
            planned |= sim.Town.Saga.Menaces.Any(m => m.Plan != null);
            // Watched as the campaign goes: by the end the heroes may have put every villain down, assets and all.
            held |= sim.Town.Saga.Menaces.Any(m => m.Assets.Count > 0);
        }
        Assert.True(planned);
        Assert.True(held);
        Assert.All(sim.Town.Saga.Menaces.Where(m => m.Plan != null), m => Assert.NotNull(Plots.PlanOf(sim.Town, m)));
    }

    [Fact]
    public void DragonsAreAMenaceOfTheirOwnWithPlansForTribute()
    {
        var c = new Simulation(new SimulationOptions { Seed = 1 }).Town.Content;
        Assert.True(c.Saga.Menaces.Kinds.ContainsKey("dragon"));
        Assert.Contains(c.Saga.Menaces.Plots.Plans, p => p.Id == "tribute" && p.Kinds.Contains("dragon"));
    }

    [Fact]
    public void ReloadedCampaignKeepsItsNeighbours()
    {
        var opts = new SimulationOptions { Seed = 9 };
        var straight = new Simulation(opts);
        straight.RunDays(150);
        var loaded = SaveGame.Read(SaveGame.Write(straight));
        straight.RunDays(60);
        loaded.RunDays(60);
        Assert.Equal(Encoding.UTF8.GetString(SaveGame.StateJson(straight)), Encoding.UTF8.GetString(SaveGame.StateJson(loaded)));
    }
}
