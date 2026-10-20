using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AlchemyTests
{
    private static (Simulation Sim, Town Town) WithBench(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        var t = sim.Town;
        var d = t.Content.Building("apothecary");
        var b = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false);
        b.BuildProgress = d.BuildHours;
        t.Market.Seed(Good.Herbs, 40);
        t.Treasury = 500;
        return (sim, t);
    }

    [Fact]
    public void EveryIngredientAndRecipeIsWhole()
    {
        var r = Content.Default().Alchemy;
        var props = r.Properties.Select(p => p.Id).ToHashSet();
        Assert.All(r.Ingredients, i =>
        {
            Assert.NotEmpty(i.Props);
            Assert.All(i.Props.Keys, k => Assert.Contains(k, props));
            Assert.True(i.Herb || i.Types.Count > 0, $"{i.Id} comes from nowhere");
        });
        Assert.All(r.Recipes, rec =>
        {
            Assert.All(rec.Needs.Keys, k => Assert.Contains(k, props));
            Assert.Contains(rec.Use, new[] { "healing", "ward", "cure", "sell" });
            if (rec.Use == "ward") Assert.NotEmpty(rec.Against);
        });
        // Every recipe can be made from something.
        var (_, t) = WithBench();
        foreach (var i in r.Ingredients.Where(i => !i.Herb)) t.Alchemy.Parts[i.Id] = 3;
        Assert.All(r.Recipes, rec => Assert.NotNull(Alchemy.Find(t, rec)));
    }

    [Fact]
    public void TheFirstBatchOfARecipeIsNamedAndGoesOnTheShelf()
    {
        var (sim, t) = WithBench();
        double herbs = t.Market.Stock(Good.Herbs);
        Assert.False(sim.Brew(["hearthmint"]));
        Assert.True(sim.Brew(["hearthmint", "hearthmint"]));
        var page = Assert.Single(t.Alchemy.Book);
        Assert.Equal("mending", page.RecipeId);
        Assert.StartsWith(t.Name, page.Name);
        Assert.Equal(2, t.Alchemy.Shelf.Get("mending", 0));
        Assert.True(t.Market.Stock(Good.Herbs) < herbs);
        // One bench brews one batch a day.
        Assert.False(sim.Brew(["hearthmint", "hearthmint"]));
        Assert.NotNull(Alchemy.Refuse(t, ["hearthmint", "hearthmint"]));
    }

    [Fact]
    public void AMixThatReachesNoRecipeSpoils()
    {
        var (sim, t) = WithBench();
        t.Alchemy.Parts["grave-dust"] = 1;
        Assert.Null(Alchemy.Match(t, ["emberroot", "grave-dust"]));
        Assert.True(sim.Brew(["emberroot", "grave-dust"]));
        Assert.Empty(t.Alchemy.Book);
        Assert.Equal(0, t.Alchemy.Parts.Get("grave-dust", 0));
        Assert.Equal(1, t.Alchemy.Spoiled);
    }

    [Fact]
    public void HeroesTakeHealingAndAWardForWhereTheyAreBound()
    {
        var (_, t) = WithBench();
        var site = t.Sites.First(s => s.Def.Kind != "").Def;
        var ward = t.Content.Alchemy.Recipes.First(x => x.Use == "ward" && x.Against.Contains(site.Kind));
        t.Alchemy.Shelf["mending"] = 4;
        t.Alchemy.Shelf[ward.Id] = 3;
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 2);
        a.Hero!.Potions = t.Content.Shrines.Potion.Carry;
        int ac = Gear.Ac(t.Content, a);
        Alchemy.Outfit(t, [a], site);
        Assert.Equal(t.Content.Shrines.Potion.Carry + t.Content.Alchemy.Carry, a.Hero.Potions);
        Assert.Equal(site.Kind, a.Hero.Ward);
        Assert.Equal(2, t.Alchemy.Shelf.Get(ward.Id, 0));
        Assert.Equal(t.Content.Alchemy.WardAc, Alchemy.WardAc(t.Content, a.Hero, site));
        Assert.True(ac > 0);
    }

    [Fact]
    public void AFeverBreakerSavesAPlagueVictimOnce()
    {
        var (_, t) = WithBench();
        Assert.Null(Alchemy.Cure(t));
        t.Alchemy.Shelf["fever-breaker"] = 1;
        Assert.NotNull(Alchemy.Cure(t));
        Assert.Null(Alchemy.Cure(t));
        Assert.Equal(1, t.Alchemy.Cured);
    }

    [Fact]
    public void ThePotionsSellForGold()
    {
        var (sim, t) = WithBench();
        t.Alchemy.Shelf["liquid-ember"] = 2;
        double gold = t.Treasury;
        Assert.True(sim.SellPotions("liquid-ember"));
        Assert.Equal(gold + 2 * t.Content.Alchemy.Recipe("liquid-ember")!.Value, t.Treasury);
        Assert.False(sim.SellPotions("liquid-ember"));
    }

    [Fact]
    public void TheStandInMayorBrewsAndTheBookSurvivesASave()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(220);
        var t = sim.Town;
        Assert.NotEmpty(t.Alchemy.Book);
        var loaded = SaveGame.Read(SaveGame.Write(sim));
        Assert.Equal(t.Alchemy.Book.Select(e => e.Name), loaded.Town.Alchemy.Book.Select(e => e.Name));
        Assert.Equal(t.Alchemy.Shelf.Keys, loaded.Town.Alchemy.Shelf.Keys);
    }
}
