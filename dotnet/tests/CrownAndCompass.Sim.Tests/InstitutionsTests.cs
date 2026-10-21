using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class InstitutionsTests
{
    private static readonly Content C = Content.Default();

    private static Simulation Player(int seed = 2) =>
        new(new SimulationOptions { Seed = seed, AutoMayor = false, AutoStories = false, FreeCompanies = false });

    /// <summary>A finished building of the kind, standing on a civic lot.</summary>
    private static Building Raise(Town t, string id)
    {
        var d = t.Content.Building(id);
        var lot = Planning.CivicLot(t, d)!;
        return t.AddBuilding(d, lot, null, $"the {d.Name}", true);
    }

    [Fact]
    public void EveryInstitutionHasABuildingKeepersAndSensibleChoices()
    {
        var r = C.Institutions;
        Assert.True(r.Patronage.Count >= 3);
        Assert.Equal(r.Institutions.Count, r.Institutions.Select(i => i.Id).Distinct().Count());
        foreach (var i in r.Institutions)
        {
            Assert.True(C.Buildings.Any(b => b.Id == i.Building), $"{i.Id}: no building {i.Building}");
            Assert.True(i.Keepers.Count >= 2, $"{i.Id}: keepers");
            Assert.NotEqual("", i.Title);
            if (i.Choice != null)
            {
                Assert.True(i.Choice.Options.Count >= 2, $"{i.Id}: a choice needs options");
                Assert.Equal(i.Choice.Options.Count, i.Choice.Options.Select(o => o.Id).Distinct().Count());
                foreach (var o in i.Choice.Options.Where(o => o.Deity != ""))
                    Assert.True(C.Library.Deity(o.Deity) != null, $"{i.Id}: no god {o.Deity}");
            }
            foreach (var p in new[] { i.Service }.Concat(i.Choice?.Options.Select(o => o.Service) ?? []))
            {
                if (p.Faction != "") Assert.Contains(C.Factions.Factions, f => f.Ref == p.Faction);
                foreach (var g in p.Yields.Keys) Assert.True(Enum.TryParse<Good>(g, out _), $"{i.Id}: no good {g}");
            }
            // Something must ask for it, or the keeper never speaks.
            Assert.True(r.Requests.Any(s => s.Needs.Any(n => n == "institution:" + i.Id || n.StartsWith("institution:" + i.Id + ":"))), $"{i.Id} makes no requests");
        }
        foreach (var f in r.Fixtures) Assert.True(r.Def(f.Institution) != null, $"{f.Id}: no institution {f.Institution}");
        foreach (var s in r.Requests) Assert.Contains(C.Stories.Stories, x => x.Id == s.Id);
    }

    [Fact]
    public void InstitutionsStartUndiscoveredAndComeToLightFromTheirSparks()
    {
        foreach (var i in C.Institutions.Institutions)
        {
            Assert.NotEmpty(i.Sparks);
            Assert.StartsWith("inst-", i.Icon);
        }
        var sim = Player();
        var t = sim.Town;
        Assert.Empty(t.InstitutionsSeen);
        sim.RunDays(1);
        Assert.True(t.InstitutionsSeen.Count() < C.Institutions.Institutions.Count / 3, $"{t.InstitutionsSeen.Count()} known on day one");
        Assert.False(Institutions.Seen(t, "resident-dragon"));
        Assert.True(Institutions.Hidden(t, "dragon_eyrie"));

        // Opening one makes it known, whatever its sparks.
        Raise(t, "griffon_roost");
        sim.RunDays(1);
        Assert.True(Institutions.Seen(t, "griffon-roost"));
        Assert.False(Institutions.Hidden(t, "griffon_roost"));
    }

    [Fact]
    public void AHallOpensWithAKeeperAndItsCallingServesTheTown()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "adventurers_hall");
        sim.RunDays(1);
        var s = Assert.Single(t.Institutions);
        Assert.Equal("adventurers-hall", s.Def);
        Assert.StartsWith("Hallmaster ", Institutions.KeeperName(t, s));
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("the Adventurers' Hall opens its doors", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(Alerts.Standing(t), a => a.Kind == Alerts.Institution);

        // Monster-hunters stand on the walls; unfunded, the hall gives far less.
        Assert.Equal(0, Institutions.Defence(t));
        Assert.True(sim.ChooseInstitution("adventurers-hall", "monster-hunters"));
        double funded = Institutions.Defence(t);
        Assert.True(funded > 0);
        Assert.True(sim.FundInstitution("adventurers-hall", 0));
        Assert.True(Institutions.Defence(t) < funded);
    }

    [Fact]
    public void PatronageCostsGoldAndWinsTheKeeperOver()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "druid_grove");
        sim.RunDays(1);
        var s = Institutions.Find(t, "druid-grove")!;
        t.Treasury += 5000;
        Assert.True(sim.FundInstitution("druid-grove", 2));
        Assert.False(sim.FundInstitution("druid-grove", 2));
        double regard = s.Regard;
        sim.RunDays(1);
        Assert.True(t.AccountsYesterday.Get(TreasuryAccount.Institutions, 0) < 0);
        Assert.True(s.Regard > regard);
        double food = Institutions.Yield(t, Good.Food);
        Assert.True(food > 1);
        Assert.True(sim.FundInstitution("druid-grove", 0));
        Assert.True(Institutions.Yield(t, Good.Food) < food);
    }

    [Fact]
    public void ChoosingIsFreeOnceAndChangingItCostsRegard()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "grand_bazaar");
        sim.RunDays(1);
        var s = Institutions.Find(t, "grand-bazaar")!;
        double regard = s.Regard;
        double tools = Institutions.Yield(t, Good.Tools);
        Assert.False(sim.ChooseInstitution("grand-bazaar", "nonsense"));
        Assert.True(sim.ChooseInstitution("grand-bazaar", "metals"));
        Assert.Equal(regard, s.Regard);
        Assert.True(Institutions.Yield(t, Good.Tools) > tools);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Kind == Alerts.Institution);
        // Not again so soon.
        Assert.False(sim.ChooseInstitution("grand-bazaar", "spices"));
        sim.RunDays(t.Content.Institutions.ChangeDays);
        Assert.True(sim.ChooseInstitution("grand-bazaar", "spices"));
        Assert.True(s.Regard < regard + t.Content.Institutions.ChangeDays * t.Content.Institutions.RegardDrift - t.Content.Institutions.ChangeRegard + 0.01);
        Assert.Equal(1, Trade.Cap(t) - t.Content.Trade.Routes.GetValueOrDefault(City.Tier(t).Id, 0));
    }

    [Fact]
    public void TheTempleAndTheOldestTavernBecomeInstitutions()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        sim.RunDays(160);
        var t = sim.Town;
        Assert.True(City.TierIndex(t, City.Tier(t).Id) >= City.TierIndex(t, "hamlet"));
        var tavern = Institutions.Find(t, "famous-tavern");
        Assert.NotNull(tavern);
        Assert.Equal(t.Taverns.OrderBy(b => b.Id).First().Id, tavern!.BuildingId);
        // The stand-in makes the choices.
        Assert.NotEqual("", tavern.Choice);
        if (Shrines.Temple(t) is { } temple)
            Assert.Equal(temple.Id, Institutions.Find(t, "temple-district")?.BuildingId);
    }

    [Fact]
    public void ARequestMovesItsKeepersRegard()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "druid_grove");
        sim.RunDays(1);
        var s = Institutions.Find(t, "druid-grove")!;
        var story = Stories.Open(t, "grove-bridge");
        Assert.NotNull(story);
        Assert.Equal(["druid-grove"], story!.Institutions);
        Assert.Contains("Grove Warden", Stories.Fill(t, story, "{keeper}"));
        double regard = s.Regard;
        Assert.NotNull(sim.ResolveStory(story.Id, 0));
        Assert.True(s.Regard > regard);
        Assert.Contains("living-bridge", s.Fixtures);
        Assert.True(t.Stories.Precedents.ContainsKey("clean-river"));
        Assert.Equal(1, s.Answered);
        // The bridge does something for the town.
        Assert.True(Institutions.Arrivals(t) > 0);
        // And the promise it was given on can now be tested.
        Assert.NotNull(Stories.Open(t, "grove-river-spill"));
    }

    [Fact]
    public void AStoryAcrossTwoInstitutionsBindsBoth()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "druid_grove");
        Raise(t, "temple");
        sim.RunDays(1);
        Assert.NotNull(Institutions.Find(t, "temple-district"));
        var story = Stories.Open(t, "grove-and-temple")!;
        Assert.Equal(["druid-grove", "temple-district"], story.Institutions);
        var grove = Institutions.Find(t, "druid-grove")!;
        var temple = Institutions.Find(t, "temple-district")!;
        double g = grove.Regard, p = temple.Regard;
        sim.ResolveStory(story.Id, 1);
        Assert.True(grove.Regard < g);
        Assert.True(temple.Regard > p);
    }

    [Fact]
    public void TheChapterhouseSwearsAnOathAndQuarrelsWithTheTemple()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "chapterhouse");
        Raise(t, "temple");
        sim.RunDays(1);
        var chapter = Institutions.Find(t, "paladin-chapterhouse")!;
        Assert.StartsWith("Knight-Commander ", Institutions.KeeperName(t, chapter));
        Assert.True(Institutions.Defence(t) > 0);
        Assert.True(sim.ChooseInstitution("paladin-chapterhouse", "crown"));
        var story = Stories.Open(t, "chapter-and-temple")!;
        Assert.Equal(["paladin-chapterhouse", "temple-district"], story.Institutions);
        Assert.Contains("oath of the crown", Stories.Fill(t, story, "{choice}"));
    }

    [Fact]
    public void TheUndercityGateHushesTheDarkBelowAndItsKnockingBindsALevel()
    {
        var sim = Player();
        var t = sim.Town;
        t.City.Tier = City.TierIndex(t, "village");
        sim.RunDays(1);
        var level = Assert.Single(t.Undercity);
        double before = Undercity.Rate(t, level);
        Raise(t, "undercity_gate");
        sim.RunDays(1);
        var gate = Institutions.Find(t, "undercity-gate")!;
        Assert.StartsWith("Gatewarden ", Institutions.KeeperName(t, gate));
        double hushed = Undercity.Rate(t, level);
        Assert.True(hushed < before);
        Assert.True(sim.ChooseInstitution("undercity-gate", "sealed"));
        Assert.True(Undercity.Rate(t, level) < hushed);
        Assert.Contains(Institutions.Describe(t, C.Institutions.Def("undercity-gate")!.Service), x => x.Contains("undercity stirs"));
        var story = Stories.Open(t, "gate-knocking")!;
        Assert.Equal(Undercity.Def(t, level).Site.Id, story.SiteId);
    }

    [Fact]
    public void TheRoostWatchesTheRoadsAndTheOracleSeesAPlaceNoOneHasHeardOf()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "griffon_roost");
        Raise(t, "observatory");
        sim.RunDays(1);
        Assert.StartsWith("Roostmaster ", Institutions.KeeperName(t, Institutions.Find(t, "griffon-roost")!));
        double escort = Institutions.Escort(t);
        Assert.True(escort > 0);
        Assert.True(sim.ChooseInstitution("griffon-roost", "roads"));
        Assert.True(Institutions.Escort(t) > escort);
        var story = Stories.Open(t, "oracle-vision")!;
        var site = Quests.Site(t, story.SiteId!)!;
        Assert.Equal(SiteKnowledge.Unheard, site.Known);
        Assert.Contains(site.Def.Name, Stories.Fill(t, story, "she sees {site}"));
    }

    [Fact]
    public void TheHallBringsTheFoundingBannerHomeAndTheDragonGuardsTheWalls()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        Raise(t, "hall_of_heroes");
        Raise(t, "dragon_eyrie");
        sim.RunDays(1);
        t.Treasury = 1000;
        var hero = Quests.CreateHero(t, t.Content.Classes[0], 5);
        var s = Stories.Open(t, "heroes-banner")!;
        s.HeroId = hero.Id;
        // A reward finds the banner; paying the robber-lord's price brings it home to the empty pedestal.
        Stories.Resolve(t, s.Id, 1);
        Assert.Equal("price", s.Stage);
        Stories.Resolve(t, s.Id, 0);
        Assert.Contains("founding-banner", Institutions.Find(t, "hall-of-heroes")!.Fixtures);

        Assert.StartsWith("Dragon ", Institutions.KeeperName(t, Institutions.Find(t, "resident-dragon")!));
        double defence = Institutions.Defence(t);
        Assert.True(sim.ChooseInstitution("resident-dragon", "protector"));
        Assert.True(Institutions.Defence(t) > defence);
    }

    [Fact]
    public void AnInstitutionShutsWhenItsBuildingCloses()
    {
        var sim = Player();
        var t = sim.Town;
        sim.RunDays(1);
        var b = Raise(t, "druid_grove");
        sim.RunDays(1);
        var s = Institutions.Find(t, "druid-grove")!;
        b.Closed = true;
        sim.RunDays(1);
        Assert.True(s.Shut);
        Assert.Equal(1, Institutions.Yield(t, Good.Food));
        b.Closed = false;
        sim.RunDays(1);
        Assert.False(s.Shut);
    }

    [Fact]
    public void TheStandInRaisesAndFundsInstitutions()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(300);
        var t = sim.Town;
        Assert.True(t.Institutions.Count >= 3, $"only {string.Join(", ", t.Institutions.Select(i => i.Def))}");
        Assert.All(t.Institutions, s => Assert.True(s.Choice != "" || Institutions.DefOf(t, s).Choice == null));
    }
}
