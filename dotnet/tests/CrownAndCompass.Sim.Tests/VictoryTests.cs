using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class VictoryTests(LongCampaign campaign) : IClassFixture<LongCampaign>
{
    /// <summary>A young town with a menace crowned the big bad by hand.</summary>
    private static (Simulation Sim, Menace Big) Crowned(int seed = 3)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(5);
        var m = Menaces.Seed(sim.Town, [2, 3]);
        Assert.NotNull(m);
        m!.BigBad = true;
        sim.Town.Saga.BigBadId = m.Id;
        sim.Town.Saga.CrownDay = sim.Town.Day;
        return (sim, m);
    }

    /// <summary>Make every term of the Grand Alliance hold: allied settlements and minor powers, nobody backing the big bad.</summary>
    private static void Ally(Town t)
    {
        var d = Victory.Def(t, "alliance")!;
        foreach (var s in t.Settlements.Take(d.Allies)) s.Allied = true;
        foreach (var p in t.Powers.Take(d.Powers)) { p.Side = PowerSide.Town; p.MenaceId = null; }
        foreach (var p in t.Powers.Skip(d.Powers)) p.Side = PowerSide.Neutral;
        foreach (var m in t.Saga.Menaces) m.Domain = 0;
    }

    [Fact]
    public void EveryRoadHasItsWordsAndANumberToHold()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var roads = t.Content.Victory.Roads;
        Assert.Equal(Victory.Slay, roads[0].Id);
        Assert.Equal(roads.Count, roads.Select(r => r.Id).Distinct().Count());
        foreach (var r in roads)
        {
            Assert.False(string.IsNullOrWhiteSpace(r.Name + r.Blurb + r.Title), r.Id);
            Assert.NotEmpty(Victory.Terms(t, r));
            if (r.Id == Victory.Slay) continue;
            Assert.True(r.Hold > 0, r.Id);
            Assert.Contains("{name}", r.Ending);
        }
    }

    [Fact]
    public void NoRoadCanBeClaimedBeforeTheBigBadShowsItself()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(5);
        Ally(sim.Town);
        Assert.True(Victory.Met(sim.Town, Victory.Def(sim.Town, "alliance")!));
        Assert.Equal("The big bad has not shown itself.", Victory.ClaimBlocked(sim.Town, "alliance"));
        Assert.False(sim.ClaimRoad("alliance"));
        Assert.DoesNotContain(Alerts.Standing(sim.Town), a => a.Kind == Alerts.Road);
    }

    [Fact]
    public void AClaimedRoadBringsTheBigBadAndWinsOnceItHolds()
    {
        var (sim, big) = Crowned();
        var t = sim.Town;
        Assert.Equal("Not every term holds yet.", Victory.ClaimBlocked(t, "alliance"));
        Ally(t);
        Assert.Contains(Alerts.Standing(t), a => a.Key == "road:alliance");
        Assert.True(sim.ClaimRoad("alliance"));
        Assert.Equal(t.Tick, big.NextAssaultTick);
        Assert.False(sim.ClaimRoad("alliance"));
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Road);

        var d = Victory.Def(t, "alliance")!;
        t.Victory.ClaimDay = t.Day - d.Hold;
        Victory.Daily(t);
        Assert.Equal(Outcome.Victory, t.Outcome);
        Assert.Equal("alliance", t.Victory.Road);
        Assert.Equal(d.Title, Saga.EndTitle(t));
        Assert.Contains(big.Leader, t.Saga.EndText);
    }

    [Fact]
    public void AClaimLapsesWhenItsTermsBreak()
    {
        var (sim, _) = Crowned();
        var t = sim.Town;
        Ally(t);
        Assert.True(sim.ClaimRoad("alliance"));
        foreach (var p in t.Powers) p.Side = PowerSide.Neutral;
        Victory.Daily(t);
        Assert.Null(Victory.Claimed(t));
        Assert.Null(t.Outcome);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("The road of Grand Alliance slips away", StringComparison.Ordinal));
    }

    [Fact]
    public void ARoadSwitchedOffCannotBeClaimed()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, RoadsOff = ["alliance", "slay"] });
        Assert.Equal(["alliance"], sim.Town.Victory.Off);
        Assert.DoesNotContain(Victory.Roads(sim.Town), d => d.Id == "alliance");
        Assert.Equal("That road is not open.", Victory.ClaimBlocked(sim.Town, "alliance"));
    }

    [Fact]
    public void TheStandInMayorNeverClaimsARoad()
    {
        var t = campaign.Sim.Town;
        Assert.Equal("", t.Victory.Claimed);
        if (t.Outcome == Outcome.Victory) Assert.Equal(Victory.Slay, t.Victory.Road);
        else Assert.Equal("", t.Victory.Road);
    }
}
