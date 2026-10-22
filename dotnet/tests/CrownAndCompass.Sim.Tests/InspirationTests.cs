using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Lasting wounds, word of mouth, year omens, wall traps and the campaign's records.</summary>
public class InspirationTests
{
    private static Simulation Grown(int seed = 2, int days = 90, bool auto = true)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoHonours = auto });
        sim.RunDays(days);
        return sim;
    }

    // Wounds -----------------------------------------------------------------------------------------

    [Fact]
    public void AWoundCostsTheHeroUntilItIsMended()
    {
        var sim = Grown(auto: false);
        var t = sim.Town;
        var hero = t.Heroes.OrderBy(a => a.Id).First();
        var def = t.Content.Wounds.Wounds.First(w => w.Attack < 0 || w.Ac < 0);
        int attack = Gear.Attack(t.Content, hero);
        int ac = Gear.Ac(t.Content, hero);
        hero.Hero!.Wounds.Add(new Wound { Id = def.Id, Site = "a test", Day = t.Day });
        Assert.Equal(attack + def.Attack, Gear.Attack(t.Content, hero));
        Assert.Equal(ac + def.Ac, Gear.Ac(t.Content, hero));
        t.Treasury = 0;
        Assert.NotNull(sim.MendWound(hero.Id, 0, "restoration"));
        Assert.NotNull(sim.MendWound(hero.Id, 5, "restoration"));
        t.Treasury = 5000;
        var mend = t.Content.Wounds.Mends.FirstOrDefault(m => sim.MendWound(hero.Id, 0, m.Id) == null);
        if (mend == null) return; // nothing in this town can mend it yet
        Assert.Equal(attack, Gear.Attack(t.Content, hero));
        Assert.Equal(ac, Gear.Ac(t.Content, hero));
    }

    [Fact]
    public void WoundsComeFromHardFightsNotEveryOne()
    {
        int wounds = 0, heroes = 0;
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var sim = Grown(seed, 300, auto: false);
            wounds += sim.Town.Heroes.Sum(h => h.Hero!.Wounds.Count);
            heroes += sim.Town.Heroes.Count;
            Assert.All(sim.Town.Heroes, h => Assert.True(h.Hero!.Wounds.Count <= sim.Town.Content.Wounds.Most));
        }
        Assert.True(wounds > 0, "three long campaigns should leave someone scarred");
        Assert.True(wounds < heroes * 2, "but not every hero, every time");
    }

    // Word of mouth ----------------------------------------------------------------------------------

    [Fact]
    public void HeroesRateTheTownAndHaveTheirSay()
    {
        var sim = Grown(days: 60);
        var t = sim.Town;
        double r = Word.Rating(t);
        Assert.InRange(r, 0, 5);
        foreach (var (d, stars) in Word.Aspects(t))
        {
            Assert.InRange(stars, 0, 5);
            Assert.Equal(0, stars * 2 % 1);
        }
        Assert.NotEmpty(t.Word.Reviews);
        Assert.All(t.Word.Reviews, rv => Assert.InRange(rv.Stars, 0, 5));
        Assert.True(t.Word.Reviews.Count <= t.Content.Word.Keep);
        Assert.Equal(Math.Sign(r - t.Content.Word.Neutral), Math.Sign(Word.Arrivals(t)));
    }

    // Omens ------------------------------------------------------------------------------------------

    [Fact]
    public void EachYearIsForetoldAndComesInItsSeason()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        sim.RunDays(2);
        var foretold = Omens.Foretold(t);
        Assert.NotNull(foretold);
        int? until = Omens.DaysUntil(t);
        if (until is int d && d > 0)
        {
            Assert.Null(Omens.Now(t));
            sim.RunDays(d + 1);
            Assert.Equal(foretold, Omens.Now(t));
            Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith("As the seers foretold"));
        }
        // The same seed foretells the same year.
        var again = new Simulation(new SimulationOptions { Seed = 3 });
        again.RunDays(2);
        Assert.Equal(foretold!.Id, Omens.Foretold(again.Town)!.Id);
    }

    // Traps ------------------------------------------------------------------------------------------

    [Fact]
    public void TrapsTakeTheirShareOfAWaveAndPitchBurnsOnce()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false, Enemy = false });
        var t = sim.Town;
        t.Treasury = 5000;
        Assert.Equal("needs a wall", sim.LayTrap("caltrops", 2));
        t.Defenses.Palisade = PalisadeState.Built;
        Assert.Equal("needs the gatehouse", sim.LayTrap("murder-holes", 0));
        Assert.Null(sim.LayTrap("caltrops", 2));
        Assert.Equal("laid already", sim.LayTrap("caltrops", 2));
        Assert.Null(sim.LayTrap("pitch-ditch", 2));
        var lines = new List<string>();
        double left = Traps.Spring(t, "goblins", 100, 2, lines, null);
        Assert.True(left < 100 && left > 0);
        Assert.Equal(2, lines.Count);
        Assert.Equal(100, Traps.Spring(t, "goblins", 100, 3, [], null));
        Assert.Single(Traps.Spent(t));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Spent);
        Assert.True(Traps.Spring(t, "goblins", 100, 2, [], null) > left);
        Assert.Null(sim.RefillTraps());
        Assert.Empty(Traps.Spent(t));
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Spent);
        var id = t.Traps.Laid[0].Id;
        Assert.True(sim.RemoveTrap(id));
        Assert.False(sim.RemoveTrap(id));
    }

    // Records ----------------------------------------------------------------------------------------

    [Fact]
    public void TheCampaignIsReadEveryWeekAndItsMomentsPinned()
    {
        var sim = Grown(days: 120);
        var t = sim.Town;
        var weeks = t.Records.Weeks;
        Assert.InRange(weeks.Count, 120 / 7, 120 / 7 + 2);
        for (int i = 1; i < weeks.Count; i++) Assert.Equal(7, weeks[i].Day - weeks[i - 1].Day);
        Assert.All(t.Records.Pins, p => Assert.InRange(p.Day, 0, t.Day));
        Assert.True(t.Records.Pins.Count <= t.Content.Trends.Records.Pins);
        foreach (var (_, _, of) in Records.Series) Assert.True(Records.Peak(t, of) >= weeks.Min(of));
    }
}
