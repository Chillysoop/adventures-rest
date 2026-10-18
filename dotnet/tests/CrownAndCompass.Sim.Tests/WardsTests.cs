using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class WardsTests
{
    [Fact]
    public void EveryWardHasAVoiceAndKnownTags()
    {
        var c = Content.Default();
        var tags = c.Regard.Tags.Select(x => x.Id).ToHashSet();
        Assert.All(c.City.Wards, w => Assert.NotNull(c.Wards.Of(w.Id)));
        Assert.All(c.Wards.Wards, w =>
        {
            Assert.Contains(c.City.Wards, x => x.Id == w.Id);
            Assert.NotEqual("", w.Voice);
            Assert.All(w.Likes.Concat(w.Dislikes), x => Assert.Contains(x, tags));
        });
    }

    [Fact]
    public void WardsJudgeChoicesAndItShowsInTheirHomes()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(40);
        var oldTown = t.Content.Wards.Of("old_town")!;
        var liked = new[] { oldTown.Likes[0] };
        Regard.Judge(t, liked, "chose to keep the peace", "Test");
        Assert.True(Wards.Feeling(t, "old_town") > 0);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("the first families of Old Town are pleased"));
        var house = t.Buildings.First(b => b.Def.Housing > 0 && b.Complete && City.WardOf(t, t.Lot(b.LotId)).Id == "old_town");
        Assert.True(Wards.Comfort(t, house) > 0);
        double before = Wards.Feeling(t, "old_town");
        sim.RunDays(10);
        Assert.True(Wards.Feeling(t, "old_town") < before);
        Regard.Judge(t, new[] { oldTown.Dislikes[0] }, "chose badly", "Test");
        Regard.Judge(t, new[] { oldTown.Dislikes[0] }, "chose badly", "Test");
        Assert.True(Wards.Comfort(t, house) < 0);
    }
}
