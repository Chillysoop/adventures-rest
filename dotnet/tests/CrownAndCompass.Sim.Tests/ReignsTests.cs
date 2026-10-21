using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ReignsTests
{
    [Fact]
    public void RulersHaveATemperamentAnAgeAndAReign()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(60);
        var t = sim.Town;
        Assert.All(t.Settlements.Where(s => !s.Razed), s =>
        {
            Assert.NotNull(s.Reign);
            Assert.Contains(t.Content.Neighbours.Temperaments, x => x.Id == s.Reign!.Temperament);
            Assert.InRange(Reigns.AgeOf(t, s), t.Content.Neighbours.Reigns.Age[0], 120);
            Assert.True(s.Reign!.EndDay > s.Reign.SinceDay);
        });
    }

    [Fact]
    public void ARulerWhoseYearsRunOutIsSucceededByAnotherTemperamentAndTheNewsIsBroughtBySomeone()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        sim.RunDays(2);
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed);
        t.Regions[s.RegionId].Known = true;
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        string old = s.Leader;
        string temper = s.Reign!.Temperament;
        int before = t.Ledger.Entries.Count;
        s.Reign.EndDay = t.Day + 1;
        s.Reign.EndCause = "age";
        sim.RunDays(2);

        Assert.NotEqual(old, s.Leader);
        Assert.NotEqual(temper, s.Reign.Temperament);
        Assert.Equal(old, s.Reign.Before);
        Assert.Equal("age", s.Reign.BeforeFate);
        Assert.Contains(Neighbours.CastOf(t, s), c => c.Role == "leader" && c.Name == s.Leader);
        var news = t.Ledger.Entries.Skip(before).Where(e => e.Text.Contains(old) && e.Text.Contains(s.Leader)).ToList();
        Assert.Single(news);
        Assert.Matches("brings word from|sends word from|drovers back from", news[0].Text);
    }

    /// <summary>A town with a cunning risen menace whose seat is in reach of a settlement the town knows.</summary>
    private static (Simulation Sim, Menace M, Settlement S) Shadowing(Func<Menace, bool>? fits = null)
    {
        for (int seed = 3; seed < 30; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoMayor = false });
            sim.RunDays(3);
            var t = sim.Town;
            var m = Menaces.Seed(t, [1, 2, 3, 4]);
            if (m == null || fits?.Invoke(m) == false) continue;
            var s = t.Settlements.FirstOrDefault(x => !x.Razed && Shadows.Near(t, m, x));
            if (s == null) continue;
            m.Stage = MenaceStage.Rising;
            m.Peak = MenaceStage.Rising;
            m.Cunning = "mastermind";
            m.Named = true;
            t.Regions[s.RegionId].Known = true;
            Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
            return (sim, m, s);
        }
        throw new InvalidOperationException("no seed puts a menace near a settlement");
    }

    [Fact]
    public void ARisenMenacesShadowDeepensUntilTheyPayTributeAndStopTrading()
    {
        var (sim, m, s) = Shadowing();
        var t = sim.Town;
        s.Shadow = new SettlementShadow { MenaceId = m.Id, Depth = t.Content.Neighbours.Shadow.Tribute - 0.1, Form = "dread" };
        sim.RunDays(2);
        Assert.True(Shadows.Paying(s));
        Assert.Equal(m.Id, Shadows.Over(t, s)!.Id);
        Assert.Equal("they pay tribute to an enemy and dare not trade", Trade.Refuse(t, s));
        Assert.Equal(t.Content.Neighbours.Shadow.TributeLoad, Shadows.TradeShare(t, s));
        Assert.NotNull(s.Shadow!.SourceId);
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"shadow:{s.Id}");
    }

    [Fact]
    public void ClearingTheMenacesSiteNearestThemLoosensItsGrip()
    {
        var (sim, m, s) = Shadowing();
        var t = sim.Town;
        s.Shadow = new SettlementShadow { MenaceId = m.Id, Depth = 60, Form = "tribute", Told = "tribute" };
        sim.RunDays(1);
        var source = Quests.Site(t, s.Shadow!.SourceId!)!;
        double depth = s.Shadow.Depth;
        source.Cleared = true;
        sim.RunDays(1);
        Assert.True(s.Shadow == null || s.Shadow.Depth <= depth - t.Content.Neighbours.Shadow.Cleared + 2);
    }

    [Fact]
    public void AHeroSentToRallyThemSpeaksThereAndComesHome()
    {
        var (sim, m, s) = Shadowing();
        var t = sim.Town;
        s.Shadow = new SettlementShadow { MenaceId = m.Id, Depth = 60, Form = "tribute", Told = "tribute" };
        Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "bard"), 3);
        Assert.Null(Shadows.RallyRefused(t, s));
        var hero = Shadows.Rallier(t)!;
        Assert.True(sim.RallySettlement(s.Id));
        Assert.True(hero.IsAway);
        Assert.Equal(hero.Id, s.Shadow.RallyHeroId);
        Assert.NotNull(Shadows.RallyRefused(t, s));
        int days = 2 * t.Regions[s.RegionId].Days + t.Content.Neighbours.Shadow.Rally.Days + 2;
        sim.RunDays(days);
        Assert.False(hero.IsAway);
        Assert.True(s.Shadow == null || s.Shadow.RallyHeroId == null);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains(s.Name) && e.Text.Contains("Persuasion"));
    }

    [Fact]
    public void ADeepEnoughShadowPutsAPuppetOnTheirSeatAndLiftingItThrowsThePuppetDown()
    {
        // A cult or the undead take a settlement as a cult, not with a puppet.
        var (sim, m, s) = Shadowing(x => x.Kind is not ("cult" or "undead"));
        var t = sim.Town;
        if ( Menaces.AmbitionOf(t, m).Id is "plague" or "intrigue") m.Ambition = "conquest";
        string old = s.Leader;
        s.Shadow = new SettlementShadow { MenaceId = m.Id, Depth = t.Content.Neighbours.Shadow.Last + 1, Form = "tribute", Told = "tribute" };
        sim.RunDays(1);
        Assert.Equal("puppet", s.Shadow!.Form);
        Assert.True(Reigns.Puppet(s));
        Assert.NotEqual(old, s.Leader);
        Assert.False(s.Allied);

        m.EndedDay = t.Day;
        s.Shadow.Depth = 1;
        sim.RunDays(3);
        Assert.False(Reigns.Puppet(s));
        Assert.Equal("freed", s.Reign!.BeforeFate);
        Assert.False(Shadows.Shadowed(s));
    }
}
