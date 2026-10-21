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
    public void ASourCastersFavourIsCalledInAsAStory()
    {
        var (_, t) = Town();
        var w = Hero(t, "wizard", 5);
        foreach (var o in Knowledge.Scryers(t).Where(o => o.Id != w.Id)) o.Hero!.AwayUntil = t.Tick + 1000;
        var site = t.Sites.First(s => s.Known != SiteKnowledge.Unheard).Def;
        w.Hero!.Loyalty = t.Content.Knowledge.ScryPrice.Favour - 5;
        while (Watch.FreeScryingsLeft(t) > 0) t.FreeScryingsUsed++;
        Assert.Equal(ScryPrice.Favour, Knowledge.Offers(t, site).Single(o => o.Caster.Id == w.Id).Price);
        Assert.NotNull(Knowledge.CommissionScrying(t, site.Id, w.Id));
        Assert.Equal(1, w.Hero.Owed);
        var s = Stories.Open(t, "scryer-favour");
        Assert.NotNull(s);
        Stories.Resolve(t, s!.Id, 0);
        Assert.Equal(0, w.Hero.Owed);
    }
}

public class CursesAndDowntimeTests
{
    private static Agent Hero(Town t, string cls, int level = 3) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == cls), level);

    private static Town NewTown() => new Simulation(new SimulationOptions { Seed = 4, Enemy = false }).Town;

    private static LibItem Weapon(Town t) =>
        t.Content.Library.Items.Where(i => Gear.SlotOf(i) == "weapon" && i.Tier >= 1).OrderBy(i => i.Id, StringComparer.Ordinal).First();

    [Fact]
    public void ACursedWeaponGivesAndTakesAndWillNotBePutDown()
    {
        var t = NewTown();
        var h = Hero(t, "fighter");
        var it = Weapon(t);
        // Not every weapon is cursed: take them up on different days until one is.
        for (int tick = 0; tick < 400 && h.Hero!.Curse == ""; tick++, t.Tick++)
        {
            h.Hero.Magic.Clear();
            Gear.Claim(t, h, it);
        }
        var s = h.Hero!;
        var curse = Curses.Of(t.Content, s);
        Assert.NotNull(curse);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(curse!.Name, StringComparison.Ordinal));
        // It will not make room for another weapon, nor be passed on.
        Assert.Equal(0, Gear.Gain(t.Content, h, it));
        var other = Hero(t, "fighter");
        Assert.Null(Gear.Pass(t, h, other) is { } passed && Gear.SlotOf(passed) == "weapon" ? passed : null);
        // The temple lifts it; the blade stays.
        var story = Stories.Open(t, "cursed-blade");
        Assert.NotNull(story);
        Curses.Lift(t, h, breakIt: false);
        Assert.Equal("", s.Curse);
        Assert.True(s.Magic.ContainsKey("weapon"));
    }

    [Fact]
    public void HeroesBuyLandTrainAndTakeAStakeBetweenJobs()
    {
        var t = NewTown();
        var h = Hero(t, "fighter");
        var s = h.Hero!;
        s.Loyalty = 70;
        h.Gold = 2000;
        double treasury = t.Treasury;
        for (int d = 0; d < 7; d++) { Downtime.Daily(t); t.Tick += SimTime.HoursPerDay; }
        Assert.NotEqual("", s.Holding);
        Assert.True(t.Treasury > treasury);
        int xp = s.Xp;
        for (int d = 0; d < 7; d++) { Downtime.Daily(t); t.Tick += SimTime.HoursPerDay; }
        Assert.True(s.Xp > xp || s.Level > 3);
    }
}

public class LawsAndCardsTests
{
    [Fact]
    public void TheLawBookSetsTheSentenceAndEachMovesTheTown()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4, Enemy = false }).Town;
        var h = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        Assert.Equal("fine", Laws.For(t, "theft").Id);
        Assert.Equal(1, Laws.Deter(t, "theft"));
        Assert.True(Laws.Set(t, "theft", "flogging"));
        Assert.False(Laws.Set(t, "theft", "flogging"));
        Assert.False(Laws.Set(t, "arson", "fine"));
        Assert.True(Laws.Deter(t, "theft") < 1);
        t.Taxes.Unrest = 10;
        double loyalty = h.Hero!.Loyalty;
        h.Abilities.Cha = 1; // a poor liar, so the court convicts
        string said = Laws.Sentence(t, "theft", h, 20, witnessed: true);
        Assert.Contains("lashes", said);
        Assert.True(t.Taxes.Unrest < 10);
        Assert.True(h.Hero.Loyalty < loyalty);
        // Every punishment says how a sentence reads.
        Assert.All(t.Content.Laws.Punishments, p => Assert.NotEqual("", p.Sentenced));
    }

    [Fact]
    public void EveryClassHasAHeroCardRoleAndKeyPowers()
    {
        var t = new Simulation(new SimulationOptions { Seed = 4, Enemy = false }).Town;
        string[] cards = ["tank", "striker", "healer", "scout", "face"];
        foreach (var c in t.Content.Classes)
        {
            Assert.Contains(c.Card, cards);
            var h = Quests.CreateHero(t, c, 5);
            Assert.InRange(Roster.KeyPowers(t, h).Count, 0, 3);
        }
        Assert.All(cards, k => Assert.Contains(t.Content.Classes, c => c.Card == k));
    }
}
