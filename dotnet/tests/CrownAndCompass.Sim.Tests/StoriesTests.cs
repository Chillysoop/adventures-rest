using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class StoriesTests
{
    private static Simulation Player(int seed = 2) => new(new SimulationOptions { Seed = seed, AutoStories = false });

    [Fact]
    public void EveryStoryIsWellFormed()
    {
        var c = Content.Default();
        var r = c.Stories;
        Assert.NotEmpty(r.Stories);
        Assert.Equal(18, r.Skills.Count);
        var abilities = new[] { Ability.Str, Ability.Dex, Ability.Con, Ability.Int, Ability.Wis, Ability.Cha };
        Assert.All(r.Skills, s => Assert.Contains(s.Ability, abilities));
        foreach (var b in r.Backgrounds)
        {
            Assert.NotNull(c.Library.Background(b.Ref));
            Assert.Equal(abilities.Order(), b.Abilities.Order());
            Assert.All(b.Skills, s => Assert.NotNull(r.Skill(s)));
        }
        Assert.Equal(r.Stories.Count, r.Stories.Select(s => s.Id).Distinct().Count());
        foreach (var d in r.Stories)
        {
            Assert.Contains(d.Stakes, r.Stakes.Select(s => s.Id));
            Assert.All(d.Needs, n => Assert.True(Stories.KnownNeed(c, n), $"{d.Id} needs {n}"));
            var stages = d.Stages.Select(s => s.Id).ToHashSet();
            foreach (var stage in d.Stages)
            {
                Assert.NotEmpty(stage.Options);
                foreach (var o in stage.Options)
                {
                    if (o.Skill != "") Assert.NotNull(r.Skill(o.Skill));
                    if (o.Ability != "") Assert.Contains(o.Ability, abilities);
                    Assert.True(!o.Rolls || o.Dc > 0, $"{d.Id}: {o.Label} rolls without a DC");
                    Assert.All(o.Needs, n => Assert.True(Stories.KnownNeed(c, n), $"{d.Id} option needs {n}"));
                    foreach (var outcome in o.Rolls ? new[] { o.Success, o.Failure, o.Critical, o.Partial, o.Fumble } : [o.Success])
                    {
                        if (outcome == null) continue;
                        Assert.NotEqual("", outcome.Text);
                        Assert.All(outcome.Effects.Keys, k => Assert.True(Stories.KnownEffect(c, k), $"{d.Id} effect {k}"));
                        Assert.True(outcome.Next == "" || stages.Contains(outcome.Next), $"{d.Id} leads to a stage it lacks: {outcome.Next}");
                    }
                }
            }
        }
    }

    [Fact]
    public void TheSheetFollowsTheBackgroundAndLevel()
    {
        var t = Player().Town;
        Assert.True(t.Stories.Mayor.Name.Length > 3);
        Assert.True(t.Stories.Mayor.Chosen == false);
        Assert.True(Stories.ChooseBackground(t, "soldier"));
        var a = Stories.Scores(t);
        Assert.Equal(17, a.Str);
        Assert.Equal(15, a.Con);
        Assert.Equal(8, a.Int);
        Assert.Equal((3, 2, Ability.Str), Stories.Bonus(t, "athletics"));
        Assert.Equal((-1, 0, Ability.Int), Stories.Bonus(t, "arcana"));
        t.Stories.Mayor.Level = 4;
        Assert.Equal(19, Stories.Scores(t).Str);
        t.Stories.Mayor.Level = 8;
        Assert.Equal(20, Stories.Scores(t).Str);
        Assert.Equal(16, Stories.Scores(t).Con);
        Assert.Equal(3, Stories.ProficiencyBonus(5));
        Assert.False(Stories.ChooseBackground(t, "no-such-background"));
    }

    [Fact]
    public void AChoiceRollsTheMayorsD20AndEndsTheStory()
    {
        var sim = Player();
        sim.RunDays(3);
        var t = sim.Town;
        var s = Stories.Open(t, "shrine-omen");
        Assert.NotNull(s);
        Assert.Contains(Alerts.Standing(t), a => a.StoryId == s.Id && a.Kind == Alerts.Story);
        var o = Stories.StageOf(t, s).Options[0];
        double chance = Stories.Chance(t, s, o);
        Assert.InRange(chance, 0.05, 0.95);
        var beat = sim.ResolveStory(s.Id, 0);
        Assert.NotNull(beat);
        Assert.InRange(beat.Die, 1, 20);
        Assert.Equal(beat.Die + beat.Modifier + beat.Proficiency, beat.Total);
        Assert.Equal(12, beat.Dc);
        Assert.True(s.Done);
        Assert.Equal(30, t.Stories.Mayor.Xp);
        Assert.Null(sim.ResolveStory(s.Id, 0));
        Assert.DoesNotContain(Alerts.Standing(t), a => a.StoryId == s.Id);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Story && e.Text.Contains("against DC 12"));
        // Having stepped into a story, the mayor's past is settled.
        Assert.False(sim.ChooseBackground("sage"));
    }

    [Fact]
    public void ASureThingCostsItsGoldAndAStoryCanGoOnToAnotherStage()
    {
        var sim = Player(4);
        sim.RunDays(30);
        var t = sim.Town;
        var s = Stories.Open(t, "tavern-brawl") ?? throw new Xunit.Sdk.XunitException("no tavern");
        double gold = t.Treasury;
        var beat = sim.ResolveStory(s.Id, 2);
        Assert.Equal(StoryResult.Certain, beat!.Result);
        Assert.Equal(gold - 25, t.Treasury, 6);

        var fever = Stories.Open(t, "fever")!;
        // A fumble or a miss on tending the sick spreads the fever, which is a second stage.
        for (int i = 0; i < 50 && fever.Stage == "start"; i++)
        {
            var b = Stories.Resolve(t, fever.Id, 0);
            if (fever.Done) fever = Stories.Open(t, "fever")!;
            Assert.NotNull(b);
        }
        Assert.Equal("spread", fever.Stage);
        Assert.False(fever.Done);
    }

    [Fact]
    public void AStoryLeftAloneRunsItsCourse()
    {
        var sim = Player();
        sim.RunDays(3);
        var t = sim.Town;
        var s = Stories.Open(t, "shrine-omen")!;
        double mandate = t.Mandate;
        sim.RunDays(5);
        Assert.True(s.Done);
        Assert.Equal(StoryResult.Ignored, s.Beats[^1].Result);
        Assert.Equal(0, t.Stories.Mayor.Xp);
        Assert.True(Stories.CanChooseBackground(t));
    }

    [Fact]
    public void AdvantageImprovesTheOddsAndSpendsInspiration()
    {
        var sim = Player();
        sim.RunDays(3);
        var t = sim.Town;
        Assert.True(sim.ChooseBackground("soldier"));
        var s = Stories.Open(t, "shrine-omen")!;
        var o = Stories.StageOf(t, s).Options[0];
        double plain = Stories.Chance(t, s, o);
        Assert.Equal(plain, Stories.Chance(t, s, o, inspired: true));
        t.Stories.Mayor.Inspiration = 1;
        Assert.Equal(1 - (1 - plain) * (1 - plain), Stories.Chance(t, s, o, inspired: true), 9);
        var beat = sim.ResolveStory(s.Id, 0, inspired: true)!;
        Assert.Equal("inspiration", beat.Advantage);
        Assert.NotNull(beat.Other);
        Assert.True(beat.Die >= beat.Other);
        Assert.Equal(beat.Die == 20 ? 1 : 0, t.Stories.Mayor.Inspiration);
    }

    [Fact]
    public void TheStandInSeesStoriesThroughAndTheMayorRises()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(200);
        var t = sim.Town;
        Assert.True(t.Stories.All.Count >= 10, $"only {t.Stories.All.Count} stories in 200 days");
        Assert.True(t.Stories.All.Count(s => s.Done) >= t.Stories.All.Count - 2);
        Assert.True(t.Stories.Mayor.Level >= 2);
        Assert.True(t.Stories.All.Select(s => s.Def).Distinct().Count() >= 6);
    }
}
