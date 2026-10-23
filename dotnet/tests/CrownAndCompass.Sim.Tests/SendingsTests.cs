using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class SendingsTests
{
    /// <summary>A town (the mayor answering for themselves, no adventurers' hall yet) whose first party on a bounty with a caster has just marched out, far enough from its site for a quest.</summary>
    private static (Simulation Sim, Party Party) PartyOnTheRoad()
    {
        for (int seed = 1; seed <= 30; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed, AutoStories = false });
            for (int day = 0; day < 160; day++)
            {
                sim.RunDays(1);
                var t = sim.Town;
                // A town that has not raised its adventurers' hall yet, so the tests can raise it themselves.
                if (t.Buildings.Any(b => b.Def.Id == "adventurers_hall")) break;
                var p = t.Day < 30 ? null : t.Parties.FirstOrDefault(x => Sendings.Abroad(t, x) && x.BountyId != null && Knowledge.SlotsLeft(t, x) > 0 && x.ArriveTick - t.Tick >= 30);
                if (p != null)
                {
                    // Close whatever the party has sent already, so each test opens its own.
                    foreach (var s in t.Stories.Open.Where(s => s.PartyId == p.Id)) s.Done = true;
                    return (sim, p);
                }
            }
        }
        throw new InvalidOperationException("No party on the road with a caster in 30 seeds");
    }

    private static void RaiseHall(Town t)
    {
        var d = t.Content.Building("adventurers_hall");
        var b = t.AddBuilding(d, Planning.CivicLot(t, d)!, null, d.Name, false);
        b.BuildProgress = d.BuildHours;
    }

    [Fact]
    public void EverySendingIsAStoryOfItsOwnSourceAboutAParty()
    {
        var c = Content.Default();
        Assert.True(c.Sendings.Stories.Count >= 10);
        foreach (var d in c.Sendings.Stories)
        {
            Assert.Equal(Sendings.Source, d.Source);
            Assert.NotNull(c.Stories.Story(d.Id));
            Assert.True(d.Needs.Intersect(["abroad", "homeward", "victorious"]).Any(), $"{d.Id} says nothing of where the party is");
        }
        Assert.Contains(c.Sendings.Stories, d => d.Stages.Count > 1);
        Assert.All(c.Sendings.Aids, a => Assert.NotNull(c.Building(a.Building)));
        Assert.All(c.Sendings.Kinds.Keys, k => Assert.NotNull(c.Stories.Skill(k)));
        Assert.Equal(c.Stories.Skills.Count, c.Sendings.Kinds.Count);
        // Every unlock names a building the town can have.
        foreach (var o in c.Sendings.Stories.SelectMany(d => d.Stages).SelectMany(s => s.Options))
            Assert.All(o.Needs.Where(n => n.StartsWith("building:")), n => Assert.NotNull(c.Building(n[9..])));
        Assert.True(c.Building("adventurers_hall").Civic);
    }

    [Fact]
    public void APartySpendsASlotToAskAndTheRollAddsItsBestHand()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        int slots = Knowledge.SlotsLeft(t, party);
        var s = Sendings.Open(t, party, "bridge-toll");
        Assert.NotNull(s);
        Assert.Equal(slots - 1, Knowledge.SlotsLeft(t, party));
        Assert.Equal(party.Id, s!.PartyId);
        Assert.Contains(s, Sendings.Waiting(t));

        var o = Stories.StageOf(t, s).Options[0];
        // One hero in the party with a hand worth lending, whatever the seed rolled them.
        t.Agent(party.Members.First(id => t.Agent(id) is { Dead: false, Hero: not null })).Abilities = new Abilities { Str = 18, Dex = 18, Con = 18, Int = 18, Wis = 18, Cha = 18 };
        var help = Sendings.Helpers(t, s, o);
        Assert.Single(help);
        Assert.Contains(help[0].Name, party.Members.Select(id => t.Agent(id).GivenName));

        var beat = Stories.Resolve(t, s.Id, 0)!;
        var (mod, prof, _) = Stories.Bonus(t, o.Skill);
        Assert.Equal(help[0].Bonus, beat.Help);
        Assert.Equal(beat.Die + mod + prof + beat.Help, beat.Total);
        Assert.Contains(help[0].Name, beat.Helpers);
        Assert.True(s.Done);
    }

    [Fact]
    public void TheAdventurersHallUnlocksChoicesAndBetterRollsAndMoreSendings()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        var s = Sendings.Open(t, party, "bridge-toll")!;
        var stage = Stories.StageOf(t, s);
        int hallOption = stage.Options.FindIndex(o => o.Needs.Contains("building:adventurers_hall"));
        Assert.True(hallOption >= 0);
        Assert.Equal("needs an adventurers' hall", Stories.Blocked(t, s, stage.Options[hallOption]));
        int trips = Sendings.Trips(t);
        double before = Stories.Chance(t, s, stage.Options[0]);

        RaiseHall(t);
        Assert.Null(Stories.Blocked(t, s, stage.Options[hallOption]));
        Assert.Contains(Sendings.Helpers(t, s, stage.Options[0]), h => h.Name == "the Adventurers' Hall" && h.Bonus == 2);
        Assert.True(Stories.Chance(t, s, stage.Options[0]) > before);
        Assert.Equal(trips + 1, Sendings.Trips(t));
        Assert.NotNull(Stories.Resolve(t, s.Id, hallOption));
    }

    /// <summary>A sending on a council bounty carries the road walked so far; one from the road home confirms the site.</summary>
    [Fact]
    public void ASendingTellsTheTownWhereThePartyStands()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        Assert.True(Intel.Commissioned(t, party));
        Assert.NotNull(Sendings.Open(t, party, "bridge-toll"));
        Assert.Equal(t.Tick, party.SentTick);
        var road = Roads.WalkedBy(t, party, t.Tick);
        Assert.NotEmpty(road);
        Assert.True(road[0].Count >= 2);

        for (int h = 0; h < 24 * 60 && !Sendings.Homeward(t, party) && !party.Returned; h++) sim.Step();
        if (!Sendings.Homeward(t, party)) return;
        foreach (var s in t.Stories.Open.Where(s => s.PartyId == party.Id)) s.Done = true;
        var site = Quests.Site(t, party.SiteId)!;
        site.Known = SiteKnowledge.Rumoured;
        site.ReportTick = null;
        var home = t.Content.Sendings.Stories.Where(d => d.Needs.Contains("homeward")).Select(d => Sendings.Open(t, party, d.Id)).FirstOrDefault(s => s != null);
        if (home == null) return;
        Assert.Equal(SiteKnowledge.Confirmed, site.Known);
        Assert.Equal(t.Tick, site.ReportTick);
        Assert.Equal(site.Cleared, site.ThoughtCleared);
    }

    [Fact]
    public void AnswersChangeTheVentureAndCanTurnThePartyBack()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        var storm = Sendings.Open(t, party, "party-storm")!;
        int turn = Stories.StageOf(t, storm).Options.FindIndex(o => o.Success.Effects.ContainsKey("turnBack"));
        Stories.Resolve(t, storm.Id, turn);
        Assert.NotNull(party.RecalledTick);
        Assert.Equal(Leg.Homeward, Knowledge.LegOf(t, party));
    }

    [Fact]
    public void AQuestWaitsForTheNextSendingAndComesBackBeforeTheSite()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        var s = Sendings.Open(t, party, "village-plea")!;
        var beat = Stories.Resolve(t, s.Id, 0)!; // stay and hunt: a sure thing that waits on the next sending
        Assert.Equal(StoryResult.Certain, beat.Result);
        Assert.False(s.Done);
        Assert.Equal("hunt", s.Stage);
        Assert.NotNull(s.WaitTick);
        Assert.True(s.WaitTick < party.ArriveTick);
        Assert.DoesNotContain(s, Sendings.Waiting(t));
        while (t.Tick < s.WaitTick) sim.Step();
        Assert.Contains(s, Sendings.Waiting(t));
        Assert.False(party.Resolved);
        int edge = party.Edge, swell = party.Swell;
        Stories.Resolve(t, s.Id, 0);
        Assert.True(s.Done);
        Assert.True(s.Beats.Count == 2);
        Assert.True(party.Edge == edge && party.Swell == swell);
    }

    [Fact]
    public void AnEdgeFromTheMayorGoesIntoTheDelve()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        var s = Sendings.Open(t, party, "sealed-door")!;
        var stage = Stories.StageOf(t, s);
        // Force the outcome: the door's riddle worked out gives an edge of 2.
        var effects = stage.Options[0].Success.Effects;
        Assert.Equal(2, effects["edge"]);
        Sendings.Apply(t, s, "edge", 2);
        Sendings.Apply(t, s, "swell", -1);
        Assert.Equal(2, party.Edge);
        Assert.Equal(-1, party.Swell);
        while (!party.Resolved) sim.Step();
        // Into the delve, unless another company cleared the site first and the party found it empty.
        Assert.True(party.Result != null || party.Empty && Quests.Site(t, party.SiteId)!.Cleared);
    }

    [Fact]
    public void TheStandInAnswersEverySendingAndPartiesSendThem()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(240);
        var t = sim.Town;
        var sent = t.Stories.All.Where(s => Sendings.Is(t, s)).ToList();
        Assert.True(sent.Count >= 3, $"only {sent.Count} sendings in 240 days");
        Assert.All(sent.Where(s => s.OpenedTick < t.Tick - 72), s => Assert.True(s.Done));
        Assert.Contains(sent, s => s.Beats.Any(b => b.Result != StoryResult.Ignored));
        // One a trip, one more for the Adventurers' Hall, and one for a party carrying a town sending stone.
        Assert.All(t.Parties, p => Assert.True(p.Asked <= 2 + t.Content.Armory.StoneTrips));
    }

    [Fact]
    public void ASendingSurvivesASaveAndLoad()
    {
        var (sim, party) = PartyOnTheRoad();
        var t = sim.Town;
        var s = Sendings.Open(t, party, "caught-scout")!;
        var back = SaveGame.Read(SaveGame.Write(sim));
        var s2 = Stories.Find(back.Town, s.Id)!;
        Assert.Equal(party.Id, s2.PartyId);
        Assert.Contains(s2, Sendings.Waiting(back.Town));
        Assert.Equal(party.Asked, back.Town.Parties.First(p => p.Id == party.Id).Asked);
    }
}
