using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class CirclesTests
{
    [Fact]
    public void AStoneCircleIsAFindWhoseLastChapterWakesIt()
    {
        var c = Content.Default();
        var kind = c.Explore.Kind("circle");
        Assert.NotNull(kind);
        Assert.Equal("circle", kind!.Chapters[^1].Reward);
        Assert.True(c.Explore.Weights.GetValueOrDefault("circle") > 0);
        Assert.NotEmpty(c.Explore.Circles.Names);
    }

    [Fact]
    public void AWokenCircleFeedsTheLibraryAndTheCultsUnlessWatched()
    {
        var t = new Simulation(new SimulationOptions { Seed = 3, Enemy = false }).Town;
        Assert.Equal(1, Circles.RiteFactor(t));
        double lore = Research.Lore(t);
        t.Circles.Add(new StoneCircle { Name = "the Nine Maidens" });
        Assert.True(Research.Lore(t) > lore);
        t.Research.Funding = 0;
        Assert.True(Circles.RiteFactor(t) > 1);
        t.Research.Funding = t.Content.Explore.Circles.WardFunding;
        Assert.Equal(1, Circles.RiteFactor(t));
    }
}
