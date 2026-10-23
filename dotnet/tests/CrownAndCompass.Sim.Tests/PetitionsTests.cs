using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class PetitionsTests
{
    private static Simulation Player(int seed = 2) => new(new SimulationOptions { Seed = seed, AutoStories = false });

    private static IEnumerable<StoryOutcome> Outcomes(StoryDef d) =>
        d.Stages.SelectMany(st => st.Options.SelectMany(o => new[] { o.Success, o.Failure, o.Critical, o.Partial, o.Fumble }).Append(st.Ignored)).Append(d.Ignored).OfType<StoryOutcome>();

    [Fact]
    public void EveryDecreeAndPrecedentIsUsed()
    {
        var r = Content.Default().Stories;
        var effects = r.Stories.SelectMany(Outcomes).SelectMany(o => o.Effects.Keys).ToHashSet();
        Assert.All(r.Decrees, d => Assert.Contains($"decree:{d.Id}", effects));
        Assert.All(r.Precedents, p => Assert.Contains($"precedent:{p.Id}", effects));
        Assert.All(r.Decrees, d => Assert.True(d.Days > 0 && d.Name != "" && d.Text != ""));
    }

    [Fact]
    public void APetitionBindsATownspersonAndADecreeRunsItsCourse()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(30);
        var s = Stories.Open(t, "market-tolls");
        Assert.NotNull(s);
        Assert.NotNull(s!.PetitionerId);
        var who = t.Agent(s.PetitionerId!.Value);
        Assert.Contains(who.Name, Stories.Fill(t, s, Stories.StageOf(t, s).Text));

        var beat = Stories.Resolve(t, s.Id, 0);
        Assert.Equal(StoryResult.Certain, beat!.Result);
        Assert.True(Decrees.InForce(t, "toll-free"));
        Assert.True(Decrees.Arrivals(t) > 0);
        double before = t.Accounts.Get(TreasuryAccount.Decrees, 0);
        sim.RunDays(5);
        Assert.True(t.Accounts.Get(TreasuryAccount.Decrees, 0) < before);

        sim.RunDays(t.Content.Stories.Decree("toll-free")!.Days);
        Assert.False(Decrees.InForce(t, "toll-free"));
        Assert.Equal(0, Decrees.Arrivals(t));
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("The decree of market tolls lifted lapses", StringComparison.Ordinal));
    }

    [Fact]
    public void AHardChoiceSetsAPrecedentThatLaterStoriesTurnOn()
    {
        var sim = Player(3);
        var t = sim.Town;
        sim.RunDays(30);
        Assert.Null(Stories.Open(t, "refugee-quarter"));
        Menaces.Seed(t, [3, 4]);
        var s = Stories.Open(t, "refugees");
        Assert.NotNull(s);
        int people = t.Workers.Count;
        Stories.Resolve(t, s!.Id, 0);
        Assert.True(t.Stories.Precedents.ContainsKey("open-gates"));
        Assert.Equal(people + 10, t.Workers.Count);
        // The gates stay open: the refugees do not come begging again, and their quarter can come up.
        Assert.Null(Stories.Open(t, "refugees"));
        Assert.NotNull(Stories.Open(t, "refugee-quarter"));
    }

    [Fact]
    public void AnUnheardPetitionerCanLeaveTown()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(30);
        t.Watch = Math.Max(1, t.Watch);
        var s = Stories.Open(t, "watch-widow");
        Assert.NotNull(s);
        var who = t.Agent(s!.PetitionerId!.Value);
        sim.RunDays(t.Content.Stories.StakesOf("low").DueDays + 1);
        Assert.True(s.Done);
        Assert.True(who.Departed);
    }

    [Fact]
    public void SomePetitionsWaitForTheirSeason()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(30);
        Assert.NotEqual("autumn", Seasons.Current(t).Id);
        Assert.Null(Stories.Open(t, "harvest-tithe"));
        while (Seasons.Current(t).Id != "autumn") sim.RunDays(1);
        Assert.NotNull(Stories.Open(t, "harvest-tithe"));
        Assert.Null(Stories.Open(t, "winter-hoard"));
    }

    [Fact]
    public void ATownHearsPetitionsAllCampaignLong()
    {
        // Most towns hear petitions both early and late (the story pool is wide, so one seed can miss a stretch, and a few
        // seeds can miss together: twelve of them keep the check from hanging on which ones). Over 48 seeds about three towns
        // in four hear both, so half of twelve is the floor, and the total keeps the pool from thinning unseen.
        var petitions = Content.Default().Stories.Stories.Where(d => d.Needs.Contains("petitioner") || d.Needs.Contains("ward")).Select(d => d.Id).ToHashSet();
        int both = 0, total = 0;
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            var t = sim.Town;
            sim.RunDays(200);
            int early = t.Stories.All.Count(s => petitions.Contains(s.Def));
            sim.RunDays(200);
            int late = t.Stories.All.Count(s => petitions.Contains(s.Def)) - early;
            if (early > 0 && late > 0) both++;
            total += early + late;
        }
        Assert.True(both >= 6, $"only {both} of 12 towns heard petitions both early and late");
        Assert.True(total >= 30, $"only {total} petitions across 12 towns in 400 days");
    }
}
