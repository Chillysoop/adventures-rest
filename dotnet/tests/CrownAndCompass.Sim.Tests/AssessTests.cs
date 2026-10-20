using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AssessTests
{
    private static Town NewTown() => new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false }).Town;

    [Fact]
    public void LaddersClimbInOrder()
    {
        var r = NewTown().Content.Assess;
        foreach (var ladder in new[] { r.Odds, r.Threat, r.Danger, r.Impact })
        {
            Assert.True(ladder.Count >= 3);
            Assert.Equal(0, ladder[0].At);
            for (int i = 1; i < ladder.Count; i++) Assert.True(ladder[i].At > ladder[i - 1].At, ladder[i].Id);
        }
    }

    [Fact]
    public void OddsFallInTheirBand()
    {
        var t = NewTown();
        Assert.Equal("longshot", Assess.Odds(t, 0.1).Id);
        Assert.Equal("even", Assess.Odds(t, 0.55).Id);
        Assert.Equal("sure", Assess.Odds(t, 1).Id);
        Assert.Equal(4, Assess.Odds(t, 0.95).Step);
    }

    /// <summary>Every effect a story can name weighs on its bars, so a new effect key cannot go unshown.</summary>
    [Fact]
    public void EveryStoryEffectHasAWeight()
    {
        var t = NewTown();
        var missing = Stories.EffectKeys.Where(k => Assess.Weight(t, k) == null).ToList();
        foreach (var d in t.Content.Stories.Stories)
            foreach (var st in d.Stages)
                foreach (var o in st.Options)
                    foreach (var e in new[] { o.Success, o.Failure, o.Critical, o.Fumble, o.Partial }.OfType<StoryOutcome>())
                        missing.AddRange(e.Effects.Keys.Where(k => Assess.Weight(t, k) == null));
        Assert.Empty(missing.Distinct());
    }

    [Fact]
    public void ImpactWeighsRewardAgainstRisk()
    {
        var t = NewTown();
        var bold = new StoryOption
        {
            Skill = "persuasion",
            Dc = 14,
            Success = new StoryOutcome { Effects = new() { ["gold"] = 150, ["mandate"] = 4 } },
            Failure = new StoryOutcome { Effects = new() { ["dread"] = 1 } },
        };
        var v = Assess.Impact(t, bold);
        Assert.Equal("high", v.Reward.Id);
        Assert.Equal("low", v.Risk.Id);
        Assert.Equal(["gold", "mandate", "dread"], v.Touches.Select(w => w.Icon));

        var sure = new StoryOption { Cost = 100, Success = new StoryOutcome { Effects = new() { ["mandate"] = 1 } } };
        var s = Assess.Impact(t, sure);
        Assert.Equal("some", s.Risk.Id);
        Assert.Contains(s.Touches, w => w.Icon == "gold");
    }

    [Fact]
    public void ThreatGrowsWithPowerAndTheBigBadIsNeverSlight()
    {
        var t = NewTown();
        var m = Menaces.Seed(t, [2, 3])!;
        double def = Threat.Defense(t, allies: false);
        m.Power = def * 0.05;
        Assert.Equal("slight", Assess.Threat(t, m).Id);
        m.Power = def * 0.9;
        Assert.Equal("deadly", Assess.Threat(t, m).Id);
        m.Power = def * 0.05;
        m.BigBad = true;
        Assert.Equal("serious", Assess.Threat(t, m).Id);
    }

    [Fact]
    public void SitesLookHarderUntilHeroesComeToTown()
    {
        var t = NewTown();
        var site = t.Sites.OrderByDescending(s => s.Def.Tier).First();
        site.BelievedTier = 3;
        foreach (var h in t.Heroes) h.Departed = true;
        Assert.Equal("deadly", Assess.Danger(t, site).Id);
        var fighter = t.Content.Classes.First(c => c.Id == "fighter");
        for (int i = 0; i < 4; i++) Quests.CreateHero(t, fighter, 8);
        Assert.Equal("slight", Assess.Danger(t, site).Id);
    }
}
