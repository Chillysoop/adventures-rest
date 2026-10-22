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
        // The sheet alone, without the charter chain's +1 on persuasion.
        Regalia.Unequip(t, "chain");
        Assert.True(Stories.ChooseBackground(t, "soldier"));
        var a = Stories.Scores(t);
        Assert.Equal(17, a.Str);
        Assert.Equal(15, a.Con);
        Assert.Equal(8, a.Int);
        Assert.Equal((3, 2, Ability.Str), Stories.Bonus(t, "athletics"));
        Assert.Equal((-1, 0, Ability.Int), Stories.Bonus(t, "arcana"));
        // Levels bring advances the player chooses: abilities or a feat at 4 and 8, skills at the others.
        t.Stories.Mayor.Level = 4;
        Assert.Equal(3, Sheet.Pending(t));
        Assert.NotNull(Sheet.Refuse(t, "ability:str,str"));
        Assert.True(Sheet.Advance(t, "skill:persuasion"));
        Assert.Equal((1, 2, Ability.Cha), Stories.Bonus(t, "persuasion"));
        Assert.True(Sheet.Advance(t, "expertise:athletics"));
        Assert.Equal((3, 4, Ability.Str), Stories.Bonus(t, "athletics"));
        Assert.Null(Sheet.Refuse(t, "feat:lucky"));
        Assert.NotNull(Sheet.Refuse(t, "ability:str,str,str"));
        Assert.NotNull(Sheet.Refuse(t, "skill:arcana"));
        Assert.True(Sheet.Advance(t, "ability:str,con"));
        Assert.Equal(18, Stories.Scores(t).Str);
        Assert.Equal(16, Stories.Scores(t).Con);
        Assert.Equal(0, Sheet.Pending(t));
        Assert.False(Sheet.Advance(t, "skill:arcana"));
        t.Stories.Mayor.Level = 8;
        Assert.True(Sheet.Advance(t, "skill:arcana"));
        Assert.True(Sheet.Advance(t, "skill:history"));
        Assert.True(Sheet.Advance(t, "skill:insight"));
        Assert.True(Sheet.Advance(t, "feat:silver-tongue"));
        Assert.Equal((1, 3 + 2, Ability.Cha), Stories.Bonus(t, "persuasion"));
        Assert.False(Sheet.Advance(t, "feat:silver-tongue"));
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
    public void InspirationRerollsAMissAndIsSpentOnlyThen()
    {
        var sim = Player();
        sim.RunDays(3);
        var t = sim.Town;
        Assert.True(sim.ChooseBackground("soldier"));
        var s = Stories.Open(t, "shrine-omen")!;
        var o = Stories.StageOf(t, s).Options[0];
        double plain = Stories.Chance(t, s, o);
        Assert.Equal(plain, Stories.Chance(t, s, o, inspired: true));
        t.Stories.Mayor.Inspiration = 3;
        Assert.Equal(1 - (1 - plain) * (1 - plain), Stories.Chance(t, s, o, inspired: true), 9);
        var beat = sim.ResolveStory(s.Id, 0, inspired: true)!;
        int spent = beat.Rerolled != null ? 1 : 0;
        Assert.Equal(Math.Min(3, 3 - spent + (beat.Die == 20 ? 1 : 0)), t.Stories.Mayor.Inspiration);
        // Over many stories, a reroll only ever follows a miss.
        for (int i = 0; i < 40; i++)
        {
            t.Stories.Mayor.Inspiration = 1;
            var st = Stories.Open(t, "shrine-omen") ?? Stories.Open(t, "old-heirloom");
            if (st == null) continue;
            var b = Stories.Resolve(t, st.Id, 0, inspired: true)!;
            if (b.Rerolled is int first) Assert.True(first == 1 || first + b.Modifier + b.Proficiency < b.Dc);
            else Assert.Equal(b.Die == 20 ? 2 : 1, t.Stories.Mayor.Inspiration);
        }
    }

    [Fact]
    public void FeatsChangeTheDice()
    {
        var t = Player().Town;
        Assert.True(Stories.ChooseBackground(t, "soldier"));
        t.Stories.Mayor.Level = 4;
        Assert.True(Sheet.Advance(t, "skill:persuasion"));
        Assert.True(Sheet.Advance(t, "skill:insight"));
        double before = Sheet.Chance(t, "athletics", "", 25, false, false);
        Assert.True(Sheet.Advance(t, "feat:practised-hand"));
        // Trained skills lose their natural 1s; an untrained one keeps them.
        Assert.Equal(before, Sheet.Chance(t, "athletics", "", 25, false, false));
        Assert.Equal(1.0, Sheet.Chance(t, "athletics", "", 2, false, false), 9);
        Assert.Equal(18 / 20.0, Sheet.Chance(t, "arcana", "", 2, false, false), 9);
        Assert.Equal(1.0, Sheet.Chance(t, "athletics", "", 9, false, false), 9);
        t.Stories.Mayor.Level = 8;
        Assert.True(Sheet.Advance(t, "skill:history"));
        Assert.True(Sheet.Advance(t, "skill:arcana"));
        Assert.True(Sheet.Advance(t, "skill:nature"));
        Assert.Equal(3, Sheet.InspirationMax(t));
        Assert.True(Sheet.Advance(t, "feat:inspiring-leader"));
        Assert.Equal(4, Sheet.InspirationMax(t));
        Assert.Equal(1, t.Stories.Mayor.Inspiration);
    }

    [Fact]
    public void RunningTheTownWellEarnsInspiration()
    {
        var sim = Player();
        sim.RunDays(2);
        var t = sim.Town;
        Assert.Equal(0, t.Stories.Mayor.Inspiration);
        Sheet.Repelled(t);
        Sheet.Repelled(t);
        sim.RunDays(1);
        Assert.Equal(1, t.Stories.Mayor.Inspiration);
        Assert.Contains(t.Ledger.Entries, e => e.Category == LedgerCategory.Story && e.Text.Contains("inspired by the town standing firm"));
        var content = t.Content.Stories.Deeds.First(d => d.Id == "content");
        Assert.Equal(content.Every, Sheet.Progress(t, content).Need);
        // It stops at what the mayor can hold.
        for (int i = 0; i < 20; i++) Sheet.Repelled(t);
        sim.RunDays(1);
        Assert.Equal(Sheet.InspirationMax(t), t.Stories.Mayor.Inspiration);
    }

    [Fact]
    public void EverydayDecisionsRollTheMayorsDice()
    {
        var sim = Player();
        var t = sim.Town;
        int checks = t.Stories.Checks;
        var b = Sheet.Decide(t, "motion")!;
        Assert.Equal(checks + 1, t.Stories.Checks);
        Assert.Same(b, t.Stories.LastCheck);
        Assert.Equal("persuasion", b.Skill);
        Assert.Contains(t.Ledger.Entries, e => e.Text.StartsWith($"Mayor {t.Stories.Mayor.Name} rolls Persuasion"));
        Assert.Equal(Sheet.Succeeded(b) ? (b.Result == StoryResult.Critical ? 2.0 : 1.0) : 0, Sheet.Gain(t, "motion", b));
        Assert.Null(Sheet.Decide(t, "no-such-decision"));
        Assert.Equal(11, Intel.HaggleDc(0.5));
        Assert.Equal(21, Intel.HaggleDc(0));
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
        Assert.Equal(0, Sheet.Pending(t));
        Assert.True(t.Stories.All.Select(s => s.Def).Distinct().Count() >= 6);
    }
}
