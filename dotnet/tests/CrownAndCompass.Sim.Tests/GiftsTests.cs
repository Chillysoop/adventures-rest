using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class GiftsTests
{
    [Fact]
    public void ATaintedGiftFallsDueOnlyAfterItsDays()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(70);
        var gift = Stories.Open(t, "gift-contract");
        Assert.NotNull(gift);
        int sign = Stories.StageOf(t, gift!).Options.FindIndex(o => o.Success.Effects.ContainsKey("precedent:signed-clause"));
        double gold = t.Treasury;
        Stories.Resolve(t, gift!.Id, sign);
        Assert.True(t.Treasury > gold);
        Assert.Null(Stories.Open(t, "gift-contract-due"));
        sim.RunDays(t.Content.Stories.Precedent("signed-clause")!.After + 1);
        foreach (var o in t.Stories.Open.ToList()) t.Stories.All.Remove(o);
        var due = Stories.Open(t, "gift-contract-due");
        Assert.NotNull(due);
        int buy = Stories.StageOf(t, due!).Options.FindIndex(o => o.Cost > 0);
        t.Treasury += 1000;
        Stories.Resolve(t, due!.Id, buy);
        Assert.False(t.Stories.Precedents.ContainsKey("signed-clause"));
    }
}
