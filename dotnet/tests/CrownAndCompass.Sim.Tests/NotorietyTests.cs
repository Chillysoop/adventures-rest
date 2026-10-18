using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class NotorietyTests
{
    private static (Simulation Sim, Town Town) NewTown(int seed = 4)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
        return (sim, sim.Town);
    }

    private static Agent Hero(Town t, int level = 4) => Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), level);

    [Fact]
    public void DeedsEarnAnEpithetPastTheTier()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        Notoriety.Deed(t, a, "clear", 5);
        Assert.Equal("", a.Hero!.Epithet);
        Notoriety.Deed(t, a, "clear", 20);
        Assert.Contains(a.Hero.Epithet, t.Content.Notoriety.Deeds["clear"].Epithets);
        Assert.StartsWith(a.Name + " ", Loyalty.Styled(t, a));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("The bards have taken to calling"));
    }

    [Fact]
    public void InfamyStainsTheCompanyAndBlocksKnighthood()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        a.Hero!.Renown = 20;
        a.Hero.PartyName = "Iron Owls";
        Assert.Null(Loyalty.KnightBlocked(t, a));
        Notoriety.Deed(t, a, "betray", 0, 16);
        Assert.Equal("too notorious for the council to knight", Loyalty.KnightBlocked(t, a));
        Assert.Equal(8, Intel.Find(t, "Iron Owls")!.Infamy);
        Assert.Contains(Loyalty.Factors(t, a), f => f.Why == "scorned in the streets");
    }

    [Fact]
    public void FamousHeroesAreCourtedUnlessHonoured()
    {
        var (_, t) = NewTown();
        var a = Hero(t, 5);
        a.Hero!.Renown = 50;
        Assert.Contains(Loyalty.Factors(t, a), f => f.Why == "other towns court them");
        a.Hero.Champion = true;
        Assert.DoesNotContain(Loyalty.Factors(t, a), f => f.Why == "other towns court them");
        Assert.True(Notoriety.Defense(t) > 0);
        Assert.True(Notoriety.FeeFactor(t, a) > 1);
    }

    [Fact]
    public void ClearingASiteMakesTheCompanyFamous()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        a.Hero!.PartyName = "Lucky Lanterns";
        var party = new Party { Id = 1, Name = "Lucky Lanterns", Members = [a.Id], SiteId = t.Sites[0].Def.Id };
        Notoriety.Cleared(t, party, [a], 3, plea: false);
        Assert.Equal(6, a.Hero.Renown);
        Assert.Equal(6, Intel.Find(t, "Lucky Lanterns")!.Renown);
        Assert.Equal(1, Notoriety.TownFame(t));
    }

    [Fact]
    public void InfamyFadesAndTroubleMakersGetCaught()
    {
        var (sim, t) = NewTown();
        var a = Hero(t);
        a.Hero!.Infamy = 40;
        t.Watch = 50;
        int days = 0;
        while (days++ < 400 && !t.Ledger.Entries.Any(e => e.Agents.Contains(a.Id) && (e.Category == LedgerCategory.Brawl || e.Category == LedgerCategory.Hardship)))
            Notoriety.Daily(t);
        Assert.True(days < 400, "an infamous hero made no trouble in 400 days");
        int before = a.Hero.Infamy;
        t.Tick = t.Content.Notoriety.DecayDays * SimTime.HoursPerDay;
        Notoriety.Daily(t);
        Assert.True(a.Hero.Infamy < before + t.Content.Notoriety.InfamyDeeds.Theft);
        _ = sim;
    }

    [Fact]
    public void StoriesFindFamousAndInfamousHeroes()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        Assert.Null(Stories.Open(t, "old-warrant"));
        a.Hero!.Infamy = 6;
        var s = Stories.Open(t, "old-warrant");
        Assert.NotNull(s);
        Assert.Equal(a.Id, s!.HeroId);
        // Handing them over sends them away.
        var beat = Stories.Resolve(t, s.Id, 0);
        Assert.NotNull(beat);
        Assert.True(a.Departed);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"chose to hand {a.GivenName} over"));
    }

    [Fact]
    public void LegendsRememberTheNameTheBardsGave()
    {
        var (_, t) = NewTown();
        var a = Hero(t);
        a.Hero!.Epithet = "the Delver";
        a.Hero.Renown = 30;
        var l = Legends.Record(t, 4).First(x => x.Kind == Legend.Hero);
        Assert.Equal(a.Name, l.Name);
        Assert.Equal("The Delver", l.Title);
    }
}
