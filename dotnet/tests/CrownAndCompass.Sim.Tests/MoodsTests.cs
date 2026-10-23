using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class MoodsTests
{
    private static (Simulation Sim, Town T) NewTown(string storyteller = "rising", int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false, Storyteller = storyteller });
        return (sim, sim.Town);
    }

    /// <summary>An adult townsperson, put to work at the given building.</summary>
    private static Agent Worker(Town t, Building? at = null)
    {
        var a = t.Workers.Where(x => x.Age >= 18).OrderBy(x => x.Id).First();
        if (at != null) a.JobId = at.Id;
        return a;
    }

    [Fact]
    public void AnInspiredSmithForgesANamedBladeThatAHeroKeepsTheNameOf()
    {
        var (sim, t) = NewTown();
        var a = Worker(t, t.Buildings[0]);
        Assert.True(Moods.Inspire(t, a, "forge"));
        var blade = t.Armory.Stock.Last();
        Assert.NotEqual("", blade.Name);
        Assert.StartsWith("weapon-", blade.Item.Id);
        Assert.Contains(a.Name, blade.From);
        Assert.Equal("forge", a.Episode);
        Assert.Equal("forge", Moods.Episode(t, a)!.Id);
        Assert.Contains(blade.Name, Assert.Single(a.Moments));
        Assert.Contains(t.Ledger.About(a.Id), e => e.Text.Contains(blade.Name));

        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        Assert.True(sim.Gift(blade.Id, hero.Id));
        Assert.StartsWith(blade.Name, Gear.Describe(hero.Hero!));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"gives {hero.Name} {blade.Name} ("));
        // A better weapon takes the slot, and the name goes back to the armory with the blade.
        var better = Armory.Add(t, t.Content.Library.Item("weapon-3")!, "a test");
        Assert.True(sim.Gift(better.Id, hero.Id));
        Assert.DoesNotContain(blade.Name, Gear.Describe(hero.Hero!));
        Assert.Contains(t.Armory.Stock, s => s.Name == blade.Name);
    }

    [Fact]
    public void AFeastCheersItsWardAndAHarvestGoesToMarket()
    {
        var (_, t) = NewTown();
        var a = Worker(t, t.Buildings[0]);
        Assert.True(Moods.Inspire(t, a, "feast"));
        string ward = City.WardOf(t, t.Lot(t.Building(a.HomeId ?? a.JobId!.Value).LotId)).Id;
        Assert.True(Moods.Feast(t, ward) > 0);
        t.Tick += 4 * SimTime.HoursPerDay;
        Assert.Equal(0, Moods.Feast(t, ward));

        var b = t.Workers.Where(x => x.Age >= 18 && x.Id != a.Id).OrderBy(x => x.Id).First();
        b.JobId = t.Buildings[0].Id;
        double food = t.Market.Stock(Good.Food);
        Assert.True(Moods.Inspire(t, b, "harvest"));
        Assert.True(t.Market.Stock(Good.Food) > food);
    }

    [Fact]
    public void ABenderKeepsThemAtTheTavernAndOutOfWork()
    {
        var (sim, t) = NewTown();
        sim.RunDays(2);
        if (t.Taverns.Count == 0) return;
        var a = Worker(t);
        Assert.True(Moods.Break(t, a, "bender"));
        Assert.Equal(Activity.Drinking, Moods.Holds(t, a));
        sim.RunDays(1);
        // Through the working hours of a whole day they never once went to work.
        for (int h = 0; h < 10; h++)
        {
            sim.Step();
            if (SimTime.IsWorkHours(t.Hour)) Assert.NotEqual(Activity.Working, a.Activity);
        }
        t.Tick = a.EpisodeUntil;
        Assert.Null(Moods.Holds(t, a));
    }

    [Fact]
    public void AfterALongLowSomeoneWalksOutForGood()
    {
        var (_, t) = NewTown();
        var a = Worker(t);
        a.LowDays = 2;
        Assert.False(Moods.Break(t, a, "walkout"));
        a.LowDays = 10;
        Assert.True(Moods.Break(t, a, "walkout"));
        Assert.True(a.Departed);
        Assert.Contains(t.Ledger.About(a.Id), e => e.Category == LedgerCategory.Departure && e.Importance == Importance.Notable);
    }

    [Fact]
    public void AWellRunTownSeesMoreInspirationsThanBreaks()
    {
        // Over 16 seeds inspirations outnumber breaks about three to one; a single seed swings, so use a typical one.
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        sim.RunDays(200);
        Assert.True(t.Moods.Inspirations > 0);
        Assert.True(t.Moods.Inspirations > t.Moods.Breaks, $"{t.Moods.Inspirations} inspirations, {t.Moods.Breaks} breaks");
        // News is spaced out: a second inspiration the same day goes to the full Ledger only.
        var x = t.Workers.Where(w => w.Age >= 18 && w.Episode == "").OrderBy(w => w.Id).ToList();
        x[0].JobId = x[1].JobId = t.Buildings[0].Id;
        t.Moods.NotedInspired = -99;
        t.Moods.NotedKind = "";
        Assert.True(Moods.Inspire(t, x[0], "forge"));
        Assert.Equal(Importance.Notable, t.Ledger.About(x[0].Id).Last().Importance);
        Assert.True(Moods.Inspire(t, x[1], "feast"));
        Assert.Equal(Importance.Routine, t.Ledger.About(x[1].Id).Last().Importance);
    }

    [Fact]
    public void TheStorytellerIsChosenAtTheStartAndSavedWithTheTown()
    {
        var (sim, t) = NewTown("chaotic");
        Assert.Equal("chaotic", Storyteller.Of(t).Id);
        sim.RunDays(3);
        var back = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal("chaotic", Storyteller.Of(back.Town).Id);
        Assert.Equal("chaotic", back.Options.Storyteller);
        // An unknown one, or a save from before storytellers, is told by the default.
        Assert.Equal("rising", Storyteller.Of(new Simulation(new SimulationOptions { Seed = 1, Storyteller = "nobody" }).Town).Id);
        back.Town.Storyteller = null;
        Assert.Equal("rising", Storyteller.Of(back.Town).Id);
    }

    [Fact]
    public void EveryStorytellerHasANameACharacterAndRisingChangesNothing()
    {
        var c = Content.Default();
        Assert.Equal(["steady", "rising", "chaotic", "relentless"], c.Storytellers.Storytellers.Select(s => s.Id));
        Assert.All(c.Storytellers.Storytellers, s => Assert.True(s.Name != "" && s.Character != "" && s.Text != "" && s.Title != ""));
        var rising = Storyteller.Def(c, "rising")!;
        Assert.Equal(new StorytellerDef { Id = rising.Id, Title = rising.Title, Name = rising.Name, Character = rising.Character, Text = rising.Text }, rising);
    }
}
