using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class BlocsTests
{
    [Fact]
    public void EveryBlocHasAVoiceKnownTagsAndKnownMembers()
    {
        var c = Content.Default();
        var tags = c.Regard.Tags.Select(x => x.Id).ToHashSet();
        var houses = c.City.Housing.Select(h => h.Id).ToHashSet();
        Assert.NotEmpty(c.Blocs.Blocs);
        Assert.All(c.Blocs.Blocs, b =>
        {
            Assert.NotEqual("", b.Voice);
            Assert.NotEmpty(b.Members);
            Assert.All(b.Likes.Concat(b.Dislikes), x => Assert.Contains(x, tags));
            Assert.All(b.Members.Keys, k =>
            {
                if (k.StartsWith("home:")) Assert.Contains(k[5..], houses);
                else if (k.StartsWith("work:")) Assert.True(k == "work:holy" || Enum.TryParse<Zone>(k[5..], out _) || c.Buildings.Any(d => d.Id == k[5..]), k);
                else Assert.Contains(k, new[] { "devout", "hedge", "refugee" });
            });
        });
    }

    [Fact]
    public void ATownspersonBelongsToSeveralBlocsByDegree()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(20);
        var shop = t.Buildings.First(b => b.Complete && b.Def.Zone == Zone.Commercial);
        var a = t.Workers.First(x => x.Age >= 18);
        a.JobId = shop.Id;
        a.Devout = true;
        Assert.Equal(1, Blocs.Degree(t, Blocs.Def(t, "stallholders")!, a));
        Assert.Equal(0.6, Blocs.Degree(t, Blocs.Def(t, "temple")!, a), 3);
        Assert.Equal(0, Blocs.Degree(t, Blocs.Def(t, "guilds")!, a));
    }

    [Fact]
    public void BlocsJudgeChoicesAndItShowsInTheirHomes()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoStories = false });
        var t = sim.Town;
        sim.RunDays(40);
        var homes = Blocs.Def(t, "householders")!;
        Assert.Contains(homes, Blocs.Speaking(t));
        Regard.Judge(t, new[] { homes.Likes[0] }, "chose to keep the peace", "Test");
        Assert.True(Blocs.Feeling(t, "householders") > 0);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("the householders") && e.Text.Contains("are pleased"));
        var house = t.Buildings.First(b => b.Def.Id == "house" && b.Complete);
        Assert.True(Blocs.Comfort(t, house) > 0);
        double before = Blocs.Feeling(t, "householders");
        sim.RunDays(10);
        Assert.True(Blocs.Feeling(t, "householders") < before);
        Regard.Judge(t, new[] { homes.Dislikes[0] }, "chose badly", "Test");
        Regard.Judge(t, new[] { homes.Dislikes[0] }, "chose badly", "Test");
        Assert.True(Blocs.Comfort(t, house) < 0);
    }
}
