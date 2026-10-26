using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>The lesser council: stewards as titled townsfolk, their budgets and levers, the quiet they keep, and the land beyond the wall.</summary>
public class LesserCouncilTests
{
    /// <summary>A player's town: every area starts on "You", as a New game does.</summary>
    private static Simulation Player(int seed = 1) => new(new SimulationOptions
    {
        Seed = seed, AutoBounties = false, AutoArmory = false, AutoIntel = false, AutoCouncil = false, AutoHonours = false,
        AutoDefense = false, AutoPlanning = false, AutoStories = false, AutoExplore = false, AutoHelpers = false,
    });

    [Fact]
    public void ANewGameStartsWithEveryAreaYours()
    {
        var sim = Player();
        Assert.All(Stewards.Areas, a => Assert.Equal(StewardMode.Player, Stewards.Mode(sim.Town, sim.Options, a.Id)));
        Assert.All(Stewards.Areas, a => Assert.False(string.IsNullOrEmpty(a.Title)));
        Assert.Equal(Stewards.Areas.Length, Stewards.Areas.Select(a => a.Title).Distinct().Count());
    }

    [Fact]
    public void AHandedAreaGetsATitledLesserCouncillor()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(2);
        Assert.True(sim.SetSteward("research", StewardMode.Steward));
        sim.RunDays(1);
        var holder = Stewards.Holder(t, "research");
        Assert.NotNull(holder);
        Assert.False(holder!.IsHero);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("Loremaster"));
        // Taken back, the seat empties.
        sim.SetSteward("research", StewardMode.Player);
        sim.RunDays(1);
        Assert.Null(Stewards.Holder(t, "research"));
    }

    [Fact]
    public void ADelegatedAreaRaisesNoAlertsButOneThatAsksStillDoes()
    {
        var sim = Player();
        var t = sim.Town;
        string kind = Stewards.Area("bounties").Alerts[0];
        Assert.False(Stewards.Quiet(t, sim.Options, kind));
        sim.SetSteward("bounties", StewardMode.Ask);
        Assert.False(Stewards.Quiet(t, sim.Options, kind));
        sim.SetSteward("bounties", StewardMode.Steward);
        Assert.True(Stewards.Quiet(t, sim.Options, kind));
        sim.RunDays(20);
        Assert.DoesNotContain(Alerts.Shown(t, sim.Options), a => Stewards.Area("bounties").Alerts.Contains(a.Kind.Id));
    }

    [Fact]
    public void AStewardKeepsToItsMonthlyCapAndWritesWhatItDid()
    {
        var sim = Player(3);
        var t = sim.Town;
        sim.SetAllStewards(StewardMode.Steward);
        Assert.True(sim.SetStewardCap("armory", 1));
        sim.RunDays(60);
        Assert.True(Stewards.SpentThisMonth(t, "armory") <= 1);
        Assert.NotEmpty(t.Stewards.Report);
        Assert.All(t.Stewards.Report, d => Assert.Contains(Stewards.Areas, a => a.Id == d.Area));
        Assert.True(sim.SetStewardCap("armory", 0));
        Assert.False(t.Stewards.Caps.ContainsKey("armory"));
    }

    [Fact]
    public void AnArmoryLeverSwitchedOffAsksNothingOfThatKind()
    {
        var sim = Player(1);
        var t = sim.Town;
        sim.SetSteward("armory", StewardMode.Ask);
        foreach (string k in Stewards.Area("armory").Kinds) Assert.True(sim.SetStewardLever("armory", k, false));
        Assert.False(sim.SetStewardLever("armory", "nonsense", false));
        for (int d = 0; d < 60; d++)
        {
            sim.RunDays(1);
            Assert.DoesNotContain(t.Stewards.Asks, a => a.Area == "armory" && Stewards.Area("armory").Kinds.Contains(a.Kind));
        }
    }

    [Fact]
    public void YesToAllAnswersEveryAsk()
    {
        var sim = Player(3);
        var t = sim.Town;
        sim.SetAllStewards(StewardMode.Ask);
        for (int d = 0; d < 80 && t.Stewards.Asks.Count < 2; d++) sim.RunDays(1);
        Assert.True(t.Stewards.Asks.Count >= 2);
        sim.AnswerAllStewards(false);
        Assert.Empty(t.Stewards.Asks);
    }

    [Fact]
    public void BeyondTheOuterWallBuildsSlowerAndHomesComeCheaper()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoDefense = false, Enemy = false });
        sim.RunDays(10);
        var t = sim.Town;
        var b = t.Buildings.First(x => x.Def.Housing > 0);
        Assert.Equal(1, Frontier.Build(t, b));
        Assert.Equal(0, Frontier.Comfort(t, b));
        t.Defenses.Palisade = PalisadeState.Built;
        sim.RunDays(1);
        Assert.True(Frontier.Walled(t));
        foreach (var x in t.Buildings.Where(x => !x.Razed))
        {
            bool outside = !Frontier.Within(t, x);
            Assert.Equal(outside ? t.Content.Frontier.OutsideBuild : 1, Frontier.Build(t, x));
            if (outside) Assert.True(t.Content.Frontier.OutsideBuild < 1);
        }
        Assert.True(t.Content.Frontier.OutsideComfort < 0);
    }
}
