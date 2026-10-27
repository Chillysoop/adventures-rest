using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Council votes as dice, swaying a councillor, trials, saving throws, conditions and negotiating.</summary>
public class TabletopRulesTests
{
    private static Simulation Player(int seed = 1, int days = 40)
    {
        var sim = new Simulation(new SimulationOptions { Seed = seed, AutoCouncil = false, AutoHonours = false, Enemy = false, AutoStories = false });
        sim.RunDays(days);
        return sim;
    }

    private static Motion Pending(Simulation sim)
    {
        var t = sim.Town;
        for (int d = 0; d < 120 && (Council.PendingMotion(t) is not { } m || m.Votes.Count < 2); d++) sim.RunDays(1);
        return Council.PendingMotion(t)!;
    }

    [Fact]
    public void EachVoteIsADieAndALean()
    {
        var sim = Player();
        var m = Pending(sim);
        Assert.NotNull(m);
        foreach (var v in m.Votes.Where(v => v.Seat != m.Proposer))
        {
            Assert.InRange(v.Die, 1, 20);
            Assert.Equal(v.Die == 20 || (v.Die != 1 && v.Die + v.Lean >= Council.VoteDc), v.Yes);
        }
    }

    [Fact]
    public void TheMayorSwaysOneVoteAMotion()
    {
        var sim = Player();
        var t = sim.Town;
        var m = Pending(sim);
        var target = m.Votes.First(v => v.Seat != m.Proposer);
        var beat = sim.SwayCouncillor(m.Id, target.Seat, "persuade");
        Assert.NotNull(beat);
        var after = m.Votes.First(v => v.Seat == target.Seat);
        Assert.Equal(Sheet.Succeeded(beat!), after.Yes != target.Yes);
        Assert.Equal(Sheet.Succeeded(beat!), after.Swayed);
        Assert.Equal("one try a motion", Council.SwayBlocked(t, m, m.Votes.Last().Seat));
        Assert.Null(sim.SwayCouncillor(m.Id, m.Votes.Last().Seat, "deceive"));
    }

    [Fact]
    public void AnIntimidatedCouncillorResentsItEvenWhenItWorks()
    {
        var sim = Player();
        var t = sim.Town;
        var m = Pending(sim);
        var target = m.Votes.First(v => v.Seat != m.Proposer);
        var seat = Council.FindSeat(t, target.Seat)!;
        seat.Loyalty = 50;
        int dc = Council.SwayDc(t, seat, Negotiation.Approach(t, "intimidate")!);
        var beat = sim.SwayCouncillor(m.Id, target.Seat, "intimidate")!;
        Assert.Equal(dc, beat.Dc);
        Assert.True(seat.Loyalty < 50 || beat.Result == StoryResult.Critical && Negotiation.Approach(t, "intimidate")!.Crit + Negotiation.Approach(t, "intimidate")!.Win >= 0);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("Intimidate:"));
    }

    [Fact]
    public void ALoyalTrainedCouncillorLendsTheMayorAHand()
    {
        var sim = Player();
        var t = sim.Town;
        var seat = t.Seats.First(s => s.HolderId != null);
        var a = t.Agent(seat.HolderId!.Value);
        string skill = Skills.All(t).Select(s => s.Id).First(s => Skills.Trained(t, a, s));
        foreach (var s in t.Seats) s.Loyalty = 0;
        Assert.Null(Sheet.Helper(t, skill));
        seat.Loyalty = 80;
        Assert.Equal(a, Sheet.Helper(t, skill));
        Assert.Contains(a.GivenName, Sheet.HelpText(t, skill));
    }

    [Fact]
    public void ANotableFriendLendsTheMayorAHand()
    {
        var sim = Player();
        var t = sim.Town;
        foreach (var s in t.Seats) s.Loyalty = 0;
        var a = t.Residents.First(x => x.Hero == null && !t.Seats.Any(s => s.HolderId == x.Id) && Skills.All(t).Any(s => Skills.Trained(t, x, s.Id)));
        string skill = Skills.All(t).Select(s => s.Id).First(s => Skills.Trained(t, a, s));
        var n = new Notable { Id = 999, Kind = t.Content.Notables.Kinds[0].Id, AgentId = a.Id, Regard = 0 };
        t.Notables.Add(n);
        Assert.Null(Sheet.Helper(t, skill));
        n.Regard = 100;
        Assert.Equal(a, Sheet.Helper(t, skill));
        Assert.Contains(a.GivenName, Sheet.HelpText(t, skill));
    }

    [Fact]
    public void TrialsWeighTheCaseAgainstTheTaleWithoutDice()
    {
        var sim = Player();
        var t = sim.Town;
        var hero = t.Heroes.First();
        int evidence = Laws.Evidence(t, true, null), lies = Laws.Lies(t, hero);
        var trial = Laws.Try(t, "brawling", hero, true);
        Assert.Equal(evidence, trial.Evidence);
        Assert.Equal(lies, trial.Lies);
        Assert.Equal(evidence >= lies, trial.Guilty);
        Assert.Same(trial, t.Trials[^1]);
        // The same case comes out the same way every time.
        Assert.Equal(trial.Guilty, Laws.Try(t, "brawling", hero, true).Guilty);
    }

    [Fact]
    public void UnderZoneOfTruthNoLieHolds()
    {
        var sim = Player();
        var t = sim.Town;
        var zone = Laws.Rules(t).Trial.Of("zone-of-truth")!;
        t.City.Tier = 0;
        Assert.NotNull(Laws.TruthBlocked(t, zone));
        Assert.False(sim.SetTrialMagic("zone-of-truth"));
        t.City.Tier = t.Content.City.Tiers.Count - 1;
        var cleric = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "cleric"), 5);
        Assert.Contains(cleric, Laws.Casters(t, "zone-of-truth"));
        Assert.True(sim.SetTrialMagic("zone-of-truth"));
        t.Treasury = 1000;
        var liar = t.Heroes.First(h => h != cleric);
        liar.Abilities.Cha = 20;
        double purse = cleric.Gold;
        var trial = Laws.Try(t, "theft", liar, false);
        Assert.True(trial.Guilty);
        Assert.Equal("zone-of-truth", trial.Magic);
        Assert.Equal(purse + zone.Fee, cleric.Gold);
    }

    [Fact]
    public void HeroesAddProficiencyToTheirClassSaves()
    {
        var sim = Player();
        var t = sim.Town;
        var fighter = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 5);
        Assert.Contains(Ability.Str, fighter.Hero!.Cls.Saves);
        Assert.Equal(Rng.Modifier(fighter.Abilities.Str) + Skills.HeroProficiency(5), Skills.SaveBonus(t, fighter, Ability.Str));
        var save = Skills.Save(t, fighter, Ability.Wis, 12, "tempted");
        Assert.Equal("save:wis", save.Skill);
        Assert.Equal("Wisdom save", Skills.Name(t, save.Skill));
        Assert.Contains(save, t.Checks);
    }

    [Fact]
    public void ConditionsGiveAdvantageOrDisadvantageAndCancel()
    {
        var sim = Player();
        var t = sim.Town;
        var a = t.Residents.First(x => x.Hero == null);
        a.Fatigue = 0;
        a.Hunger = 0;
        a.Episode = "";
        if (a.HomeId is int home) t.Building(home).SickUntil = 0;
        Assert.Equal(0, Conditions.Edge(t, a, Conditions.Checks).Edge);
        a.Fatigue = 99;
        Assert.True(Conditions.Has(t, a, "exhausted"));
        Assert.Equal((-1, "Exhausted"), Conditions.Edge(t, a, Conditions.Checks));
        a.Episode = t.Content.Moods.Inspirations[0].Id;
        a.EpisodeUntil = t.Tick + 48;
        Assert.True(Conditions.Has(t, a, "inspired"));
        Assert.Equal(0, Conditions.Edge(t, a, Conditions.Checks).Edge);
        a.Fatigue = 0;
        Assert.Equal(1, Conditions.Edge(t, a, Conditions.Checks).Edge);
        var check = Skills.Roll(t, a, "persuasion", 10, "talk");
        Assert.NotNull(check.Other);
        Assert.True(check.Die >= check.Other);
        Assert.Equal("Inspired", check.Edge);
    }

    [Fact]
    public void SicknessAndLastingWoundsAreConditions()
    {
        var sim = Player();
        var t = sim.Town;
        var a = t.Residents.First(x => x.Hero == null && x.HomeId != null);
        t.Building(a.HomeId!.Value).SickUntil = 0;
        Assert.False(Conditions.Has(t, a, "sick"));
        t.Building(a.HomeId!.Value).SickUntil = t.Tick + 24;
        Assert.True(Conditions.Has(t, a, "sick"));
        var hero = t.Heroes.First();
        hero.Hero!.Wounds.Clear();
        Assert.False(Conditions.Has(t, hero, "wounded"));
        hero.Hero!.Wounds.Add(new Wound { Id = t.Content.Wounds.Wounds[0].Id, Day = t.Day });
        Assert.True(Conditions.Has(t, hero, "wounded"));
        Assert.Equal(-1, Conditions.Edge(t, hero, Conditions.Saves).Edge);
    }

    [Fact]
    public void AGuildDemandIsTalkedOverOnce()
    {
        var sim = Player();
        var t = sim.Town;
        var def = t.Content.Guilds.Guilds[0];
        t.Guilds.Clear();
        var g = new Guild { Id = def.Id, Standing = 50, Want = def.Wants[0].Kind, Due = t.Day + 6 };
        t.Guilds.Add(g);
        Assert.Null(Guilds.TalkBlocked(t, g));
        var beat = sim.NegotiateGuild(g.Id, "persuade");
        Assert.NotNull(beat);
        Assert.Equal(Sheet.Succeeded(beat!), g.Want == "");
        if (g.Want != "") Assert.Equal("already talked it over", Guilds.TalkBlocked(t, g));
        Assert.Null(sim.NegotiateGuild(g.Id, "persuade"));
    }

    [Fact]
    public void ApproachesWeighTheirCostsAsWritten()
    {
        var sim = Player(days: 1);
        var t = sim.Town;
        var lie = Negotiation.Approach(t, "deceive")!;
        Assert.True(lie.Lose < Negotiation.Approach(t, "persuade")!.Lose);
        Assert.True(Negotiation.Approach(t, "intimidate")!.Win < 0);
        Assert.Equal(15, Negotiation.Dc(Negotiation.Approach(t, "persuade")!, 15, 0, 0));
        Assert.Equal(10 + 3 - 1, Negotiation.Dc(Negotiation.Approach(t, "intimidate")!, 15, 3, 0));
    }

    [Fact]
    public void ResearchGoesInStepsEachARoll()
    {
        var sim = Player();
        var t = sim.Town;
        var d = t.Content.Research.Discoveries.Where(x => x.Requires.Count == 0).OrderBy(x => x.Rank).First();
        t.Research.Revealed[d.Id] = "test";
        // The scholars may already be on it of their own accord.
        Assert.True(t.Research.Study == d.Id || sim.StudyDiscovery(d.Id));
        int notes = t.Research.Feed.Count(n => n.Icon == "d20");
        for (int i = 0; i < 60 && !Research.IsKnown(t, d.Id); i++)
        {
            t.Research.Progress[d.Id] = Research.LoreCost(t, d) * 2;
            sim.RunDays(1);
        }
        Assert.True(Research.IsKnown(t, d.Id));
        Assert.True(t.Research.Feed.Count(n => n.Icon == "d20") - notes >= t.Content.Research.Stages.Count);
    }
}
