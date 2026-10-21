using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class TavernsTests
{
    private static Agent Hero(Town t, string cls, int level = 3) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);

    private static (Simulation Sim, Town T) Town(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, Enemy = false });
        for (int d = 0; d < 60 && sim.Town.Taverns.Count == 0; d++) sim.RunDays(1);
        Assert.NotEmpty(sim.Town.Taverns);
        return (sim, sim.Town);
    }

    [Fact]
    public void EveryKeeperHasAPastWithARequestAndEveryTavernAQuirk()
    {
        var c = Content.Default();
        var perks = new[] { "heroes", "calm", "ale", "perform", "watch", "warm" };
        foreach (var p in c.Taverns.Pasts)
        {
            Assert.Contains(p.Perk, perks);
            Assert.Contains(c.Stories.Stories, s => s.Needs.Contains("keeper:" + p.Id));
        }
        foreach (var q in c.Taverns.Quirks)
            Assert.All(q.Traits, x => Assert.True(Enum.TryParse<Trait>(x, out _), x));
        var (_, t) = Town();
        var tavern = t.Taverns[0];
        Assert.NotNull(Taverns.Quirk(t, tavern));
        if (Taverns.Keeper(t, tavern) != null) Assert.NotNull(Taverns.Past(t, tavern));
    }

    [Fact]
    public void AQuirkDrawsItsOwnCrowdAndPersonasShowAfterThreeAles()
    {
        var (_, t) = Town();
        var tavern = t.Taverns[0];
        var q = Taverns.Quirk(t, tavern)!;
        var h = Hero(t, "fighter");
        var drawn = t.Residents.FirstOrDefault(a => a.Hero == null && q.Traits.Any(x => a.Has(Enum.Parse<Trait>(x))));
        if (q.Heroes) Assert.Equal(q.Draw, Taverns.Draw(t, h, tavern));
        if (drawn != null) Assert.Equal(q.Draw, Taverns.Draw(t, drawn, tavern));
        var p = t.Residents.First(a => a.Hero == null);
        p.Drinks = 0;
        Assert.Null(Taverns.Drunk(t, p));
        p.Drinks = 3;
        Assert.Equal(Taverns.Persona(t, p), Taverns.Drunk(t, p));
    }

    [Fact]
    public void SlangColoursTalkButNeverASiteName()
    {
        var (_, t) = Town();
        string watch = Taverns.Say(t, "the watch");
        Assert.NotEqual("the watch", watch);
        Assert.Equal(watch, Taverns.Say(t, "the watch"));
        Assert.Contains(watch, Taverns.Slang(t, "Nobody trusts the watch."));
        Assert.StartsWith(char.ToUpperInvariant(watch[0]) + watch[1..], Taverns.Slang(t, "The watch drinks free."));
    }

    [Fact]
    public void ADrunkHerosKillingBlowLeavesKinWhoWantBloodMoney()
    {
        var (_, t) = Town();
        var tavern = t.Taverns[0];
        var h = Hero(t, "fighter");
        h.Drinks = 5;
        Agent? victim = null;
        // Not every natural 20 kills: try townsfolk until one does.
        for (int tick = 0; tick < 200 && victim == null; tick++, t.Tick++)
            foreach (var a in t.Residents.Where(a => a.Hero == null).Take(3).ToList())
                if (Taverns.Bloodshed(t, tavern, h, a, 20)) { victim = a; break; }
        Assert.NotNull(victim);
        Assert.True(victim!.Dead);
        Assert.NotNull(Taverns.Owing(t, h));
        Assert.False(Taverns.Bloodshed(t, tavern, h, t.Residents.First(a => a.Hero == null), 19));
        var s = Stories.Open(t, "blood-money");
        Assert.NotNull(s);
        Assert.Equal(h.Id, s!.HeroId);
        h.Gold = 100;
        // The hero pays from their own purse.
        Stories.Resolve(t, s.Id, 1);
        Assert.Null(Taverns.Owing(t, h));
        Assert.Equal("paid", t.BloodDebts[^1].Settled);
        Assert.True(h.Gold < 100);
    }

    [Fact]
    public void CastersAskByTheirStandingFreeGoldOrAFavour()
    {
        var (_, t) = Town();
        var w = Hero(t, "wizard", 5);
        // Only this caster is in town to ask.
        foreach (var o in Knowledge.Scryers(t).Where(o => o.Id != w.Id)) o.Hero!.AwayUntil = t.Tick + 1000;
        var site = t.Sites.First(s => s.Known != SiteKnowledge.Unheard).Def;
        var k = t.Content.Knowledge;
        w.Hero!.Loyalty = 60;
        Assert.Equal("gold", Knowledge.ScryOffer(t, site)!.Kind);
        w.Hero.Loyalty = k.ScryFreeLoyalty;
        Assert.Equal("free", Knowledge.ScryOffer(t, site)!.Kind);
        w.Hero.Loyalty = k.ScryFavourLoyalty - 5;
        var ask = Knowledge.ScryOffer(t, site)!;
        Assert.Equal("favour", ask.Kind);
        Assert.Equal(0, ask.Cost);
        double before = t.Treasury;
        while (Watch.FreeScryingsLeft(t) > 0) t.FreeScryingsUsed++;
        Assert.NotNull(Knowledge.CommissionScrying(t, site.Id));
        Assert.Equal(before, t.Treasury);
        Assert.Equal(1, w.Hero.Owed);
        // The favour is called in as a story.
        var s = Stories.Open(t, "scryer-favour");
        Assert.NotNull(s);
        Stories.Resolve(t, s!.Id, 0);
        Assert.Equal(0, w.Hero.Owed);
        // Owed too much, a sour caster stops scrying on credit.
        w.Hero.Owed = k.ScryMaxOwed;
        w.Hero.Loyalty = k.ScryFavourLoyalty - 5;
        Assert.Null(Knowledge.ScryOffer(t, site));
    }
}
