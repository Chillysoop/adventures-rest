using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class MayoraltyTests
{
    private static (Simulation Sim, Town Town) NewTown(bool hardcore = false, bool auto = false)
    {
        var sim = new Simulation(new SimulationOptions { Seed = 6, AutoMayor = auto, Hardcore = hardcore });
        return (sim, sim.Town);
    }

    [Fact]
    public void TheMayorStartsSafeInTheHallAndWalkingTheStreetsEarnsMandate()
    {
        var (sim, t) = NewTown();
        Assert.Equal("keep", Mayoralty.Stance(t).Id);
        Assert.Equal(Mayoralty.MaxHp(t), Mayoralty.Hp(t));
        Assert.Equal(0, Mayoralty.Mandate(t));
        Assert.False(Mayoralty.Exposed(t));
        Assert.True(sim.SetMayorStance("town"));
        Assert.True(Mayoralty.Exposed(t));
        Assert.True(Mayoralty.Mandate(t) > 0);
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        Assert.Contains(Loyalty.Factors(t, hero), f => f.Why == "the mayor walks among us");
        Assert.False(sim.SetMayorStance("nowhere"));
    }

    [Fact]
    public void WoundsHealAndADeadMayorIsSucceededByACandidate()
    {
        var (sim, t) = NewTown();
        t.Stories.Mayor.Level = 4;
        t.Stories.Mayor.Xp = 750;
        int level = t.Stories.Mayor.Level;
        Assert.True(level > 1);
        string name = t.Stories.Mayor.Name;
        Assert.False(Mayoralty.Wound(t, 2, "a fall"));
        Mayoralty.Daily(t, false);
        Assert.Equal(Mayoralty.MaxHp(t), Mayoralty.Hp(t));

        Assert.True(Mayoralty.Wound(t, 999, "a fall"));
        Assert.True(Mayoralty.Dead(t));
        Assert.Null(t.Outcome);
        Assert.False(sim.SetMayorStance("town"));
        Assert.Equal("needs a temple", Mayoralty.RaiseRefused(t));
        var options = Mayoralty.Candidates(t);
        Assert.Equal(3, options.Count);
        Assert.Equal(options.Select(o => o.Name), Mayoralty.Candidates(t).Select(o => o.Name));
        Assert.True(sim.NameSuccessor(1));
        Assert.False(Mayoralty.Dead(t));
        Assert.Equal(options[1].Name, t.Stories.Mayor.Name);
        Assert.NotEqual(name, t.Stories.Mayor.Name);
        Assert.True(t.Stories.Mayor.Level < level);
        var past = Assert.Single(t.Mayoralty.Past);
        Assert.Equal(name, past.Name);
        Assert.Equal(level, past.Level);
    }

    [Fact]
    public void TheTempleCanRaiseAMayorWhoseBodyCameHome()
    {
        var (sim, t) = NewTown();
        var lot = Planning.CivicLot(t, t.Content.Building("temple"))!;
        t.AddBuilding(t.Content.Building("temple"), lot, null, "the Temple", true);
        t.Treasury = 10000;
        Mayoralty.Wound(t, 999, "a fall");
        Assert.Null(Mayoralty.RaiseRefused(t));
        double before = t.Treasury;
        Assert.True(sim.RaiseMayor());
        Assert.False(Mayoralty.Dead(t));
        Assert.Equal(before - Mayoralty.RaiseCost(t), t.Treasury, 3);
        Assert.Equal(1, Mayoralty.Hp(t));

        Mayoralty.Wound(t, 999, "lost at sea", lost: true);
        Assert.Equal("no body", Mayoralty.RaiseRefused(t));
    }

    [Fact]
    public void InHardcoreTheMayorsDeathEndsTheCampaign()
    {
        var (sim, t) = NewTown(hardcore: true);
        Assert.True(t.Mayoralty.Hardcore);
        Mayoralty.Wound(t, 999, "a knife");
        Assert.Equal(Outcome.Defeat, t.Outcome);
        Assert.False(sim.NameSuccessor(0));
    }

    [Fact]
    public void AMayorWhoRidesOutGivesTheCompanyAnEdgeAndComesHome()
    {
        var (sim, t) = NewTown();
        sim.SetMayorStance("ride");
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 3);
        var party = new Party { Id = t.Parties.Count + 1, Name = "Mayor's Company", Members = [hero.Id], SiteId = t.Sites[0].Def.Id, DepartTick = t.Tick };
        t.Parties.Add(party);
        Mayoralty.Daily(t, false);
        Assert.Equal(party.Id, t.Mayoralty.PartyId);
        Assert.Equal(1, party.Edge);
        Assert.False(Mayoralty.Exposed(t));
        party.Returned = true;
        Mayoralty.Daily(t, false);
        Assert.Null(t.Mayoralty.PartyId);
        Assert.False(Mayoralty.Dead(t));
    }

    [Fact]
    public void TheStandInNamesASuccessorAtOnce()
    {
        var (_, t) = NewTown(auto: true);
        Mayoralty.Wound(t, 999, "a fall");
        Mayoralty.Daily(t, true);
        Assert.False(Mayoralty.Dead(t));
        Assert.Single(t.Mayoralty.Past);
    }
}
