using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class NotablesTests
{
    private static Simulation Run(int seed, int days)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed });
        sim.RunDays(days);
        return sim;
    }

    [Fact]
    public void EveryKindNamesApproachesNewsAndBoonsTheContentHas()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var r = t.Content.Notables;
        Assert.True(r.Kinds.Count >= 7);
        Assert.Equal(r.Bands.Count + 1, r.BandNames.Count);
        Assert.Equal(r.BandNames.Count, r.TipDays.Count);
        foreach (var d in r.Kinds)
        {
            Assert.True(d.Approaches.Count >= 3, $"{d.Id} needs three approaches");
            foreach (var a in d.Approaches)
            {
                var ap = r.Approach(a);
                Assert.NotNull(ap);
                Assert.NotNull(t.Content.Stories.Skill(ap!.Skill));
            }
            Assert.True(r.Hears.ContainsKey(d.Hears), $"{d.Id} hears {d.Hears}");
            if (d.Boon != "") Assert.NotNull(t.Content.Library.Item(d.Boon));
            Assert.True(d.Outsider ? d.Names.Count > 0 : d.Trades.Count > 0 || d.Id == r.GuildMaster, $"{d.Id} needs trades or names");
            Assert.NotEmpty(d.Asks);
            foreach (var ask in d.Asks)
                Assert.True(ask.Gold > 0 || t.Content.Stories.Skill(ask.Skill) != null, $"{d.Id} ask {ask.Id} needs gold or a skill");
        }
    }

    [Fact]
    public void NotablesRiseOnTheirOwnAndNeverSourWithoutTheMayor()
    {
        foreach (int seed in new[] { 1, 2, 3 })
        {
            var t = Run(seed, 240).Town;
            var active = Notables.Active(t);
            Assert.True(active.Count >= 4, $"seed {seed}: {active.Count} notables");
            Assert.Equal(active.Count, active.Select(n => n.Kind).Distinct().Count());
            Assert.Contains(active, n => n.AgentId == null);
            Assert.All(active, n => Assert.True(Notables.Band(t, n) > Notables.Bitter && Notables.Band(t, n) < Notables.Warm, $"seed {seed}: {Notables.NameOf(t, n)} at {n.Regard}"));
            Assert.DoesNotContain(t.Ledger.Entries, e => e.Text.Contains("nothing more to do with the mayor"));
        }
    }

    [Fact]
    public void VisitsRollTheMayorsSkillAndGiftsCostGold()
    {
        var sim = Run(4, 30);
        var t = sim.Town;
        var n = Notables.Active(t)[0];
        var d = Notables.DefOf(t, n);
        double before = n.Regard;
        int checks = t.Stories.Checks;
        var beat = sim.VisitNotable(n.Id, d.Approaches[0]);
        Assert.NotNull(beat);
        Assert.Equal(checks + 1, t.Stories.Checks);
        Assert.NotEqual(before, n.Regard);
        Assert.NotNull(Notables.VisitBlocked(t, n, "gift"));
        Assert.Null(sim.VisitNotable(n.Id, d.Approaches[1]));

        n.NextVisit = t.Day;
        t.Treasury = 500;
        double r0 = n.Regard;
        sim.VisitNotable(n.Id, "gift");
        Assert.Equal(500 - t.Content.Notables.Gift, t.Treasury);
        Assert.Equal(Math.Min(100, r0 + t.Content.Notables.GiftGain), n.Regard);
        n.NextVisit = t.Day;
        Assert.Equal("not their way", Notables.VisitBlocked(t, n, "nonsense"));
    }

    [Fact]
    public void AFriendGivesABoonAndPassesOnWhatTheyHear()
    {
        var sim = Run(2, 60);
        var t = sim.Town;
        var wizard = Notables.Active(t).First(n => n.Kind == "recluse");
        int unheard = t.Sites.Count(s => s.Known == SiteKnowledge.Unheard);
        int rumours = t.Rumors.Count;
        wizard.Regard = 100;
        sim.RunDays(12);
        Assert.True(wizard.BoonGiven);
        // The stand-in mayor may have handed it on already: the Ledger has the gift.
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains($"{wizard.Name} counts the mayor a friend"));
        Assert.True(wizard.Tips > 0);
        Assert.True(t.Rumors.Count > rumours);
        Assert.True(t.Sites.Count(s => s.Known == SiteKnowledge.Unheard) < unheard);
        Assert.Contains(t.Rumors, r => r.Source == wizard.Name && r.True);
    }

    [Fact]
    public void FavoursWarmAndRefusalsCool()
    {
        var sim = Run(3, 40);
        var t = sim.Town;
        var n = Notables.Active(t)[0];
        var d = Notables.DefOf(t, n);
        var gold = d.Asks.First(a => a.Gold > 0);
        n.Ask = gold.Id;
        n.AskUntil = t.Day + 3;
        t.Treasury = 500;
        double r0 = n.Regard;
        sim.AnswerNotable(n.Id, true);
        Assert.Equal("", n.Ask);
        Assert.Equal(500 - gold.Gold, t.Treasury);
        Assert.Equal(r0 + gold.Gain, n.Regard, 6);
        Assert.Equal(1, n.Favours);

        n.Ask = gold.Id;
        double r1 = n.Regard;
        sim.AnswerNotable(n.Id, false);
        Assert.Equal(r1 - t.Content.Notables.Refuse, n.Regard, 6);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == $"notable:{n.Id}");
    }

    [Fact]
    public void ABitterNotableWalksAway()
    {
        var sim = Run(1, 30);
        var t = sim.Town;
        var n = Notables.Active(t)[0];
        n.Regard = -100;
        n.Ask = "";
        n.NextAsk = t.Day + 999;
        sim.RunDays(1);
        Assert.Contains(Alerts.Standing(t), a => a.Key == $"bitter:{n.Id}");
        sim.RunDays(t.Content.Notables.LeaveDays);
        Assert.True(n.Gone);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("nothing more to do with the mayor"));
    }
}
