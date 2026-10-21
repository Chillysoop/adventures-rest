using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class WorldTests
{
    [Fact]
    public void GeneratesTheSameWildsFromTheSameSeed()
    {
        static string Map(int seed)
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            return string.Join("\n", t.Regions.Select(r => $"{r.Name}|{r.Theme.Id}|{r.Bearing}|{string.Join(",", r.SiteIds)}"))
                + string.Join("\n", t.Sites.Select(s => $"{s.Def.Id}|{s.Def.Tier}|{s.Def.Boss.Monster}"));
        }
        Assert.Equal(Map(5), Map(5));
        Assert.NotEqual(Map(5), Map(6));
    }

    [Fact]
    public void BuildsRingsOfThemedRegionsWithSitesForTheirLevels()
    {
        var t = new Simulation(new SimulationOptions { Seed = 3 }).Town;
        var w = t.Content.World;
        Assert.Equal(w.Rings.Sum(r => r.Regions), t.Regions.Count);
        Assert.All(t.Regions, r => Assert.NotEmpty(r.SiteIds));
        Assert.All(t.Regions, r => Assert.Equal(r.Ring == 0, r.Known));
        foreach (var s in t.Sites.Where(s => s.Generated))
        {
            var r = World.RegionOf(t, s)!;
            Assert.InRange(s.Def.Tier, r.Levels[0], r.Levels[^1]);
            Assert.Equal(r.Days, s.Def.Distance);
            // Every boss and wanderer resolves to a creature the delve can fight.
            Assert.True(t.Content.Monster(s.Def.Boss.Monster).Hp > 0);
            Assert.NotEmpty(t.Content.Wanderers(s.Def, s.Def.Encounters.First(e => e.Kind == "wander").Cr));
        }
        // The hand-written sites all found a home.
        Assert.All(t.Sites.Where(s => !s.Generated), s => Assert.NotNull(s.RegionId));
        Assert.Equal(t.Sites.Count, t.Sites.Select(s => s.Def.Name).Distinct().Count());
    }

    [Fact]
    public void PutsDwarvesInTheMountainsAndElvesInTheForests()
    {
        var themes = new HashSet<string>();
        foreach (int seed in new[] { 1, 2, 3, 4 })
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            foreach (var r in t.Regions.Where(r => r.Ring <= 1)) themes.Add(r.Theme.Id);
            foreach (var s in t.Settlements)
            {
                var theme = t.Regions[s.RegionId].Theme;
                Assert.Contains(s.Kin.Id, theme.Peoples);
                if (theme.Id == "highpeaks") Assert.Contains(s.Kin.Id, new[] { "dwarves", "gnomes" });
                if (theme.Id == "thornwood") Assert.Equal("elves", s.Kin.Id);
            }
        }
        // Mountains, forests, caverns, castle ruins and farmland villages are all near town.
        Assert.Superset(new HashSet<string> { "highpeaks", "thornwood", "deeproads", "crownruins", "greenmarch" }, themes);
    }

    [Fact]
    public void ALevelOneCompanyPicksALevelOneSiteItHasHeardOf()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 7, AutoMayor = false, Enemy = false });
        var t = sim.Town;
        var cls = t.Content.Classes[0];
        var members = Enumerable.Range(0, 3).Select(_ => Quests.CreateHero(t, cls, 1)).ToList();
        var pick = Campaigns.Choose(t, members, t.Content.World.Companies with { MinScore = -99 });
        Assert.NotNull(pick);
        Assert.Equal(1, pick.Def.Tier);
        Assert.NotEqual(SiteKnowledge.Unheard, pick.Known);

        // Seasoned heroes look past the country they have outgrown.
        foreach (var s in t.Sites) Rumors.Hear(t, s, "something", s.Def.Tier);
        var veterans = Enumerable.Range(0, 4).Select(_ => Quests.CreateHero(t, cls, 6)).ToList();
        var far = Campaigns.Choose(t, veterans, t.Content.World.Companies with { MinScore = -99 });
        Assert.NotNull(far);
        Assert.True(far.Def.Tier >= 4);
    }

    [Fact]
    public void GreenhornsWithoutAVeteranStartSomewhereGentler()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 7, AutoMayor = false, Enemy = false });
        var t = sim.Town;
        foreach (var s in t.Sites) Rumors.Hear(t, s, "something", s.Def.Tier);
        var rules = t.Content.World.Companies with { MinScore = -99 };
        var members = Enumerable.Range(0, 3).Select(_ => Quests.CreateHero(t, t.Content.Classes[0], 3)).ToList();
        for (int i = 0; i < 20; i++) Assert.True(Campaigns.Choose(t, members, rules)!.Def.Tier <= 2);
        // With an old hand along they will take on a site built for their level.
        members[0].Hero!.Expeditions = rules.Veteran;
        var picks = Enumerable.Range(0, 20).Select(_ => Campaigns.Choose(t, members, rules)!.Def.Tier).ToList();
        Assert.Contains(3, picks);
    }

    [Fact]
    public void FreeCompaniesGoOutWithoutBountiesAndLevelUp()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2, AutoBounties = false, Enemy = false });
        sim.RunDays(150);
        var t = sim.Town;
        Assert.Contains(t.Parties, p => p.BountyId == null);
        Assert.True(t.Agents.Where(a => a.Hero != null).Max(a => a.Hero!.Level) >= 3);
        // Nobody paid them, so the country they saw stays off the map until the council buys it.
        var told = t.Parties.Where(p => p.Returned && !p.Withheld).Select(p => Quests.Site(t, p.SiteId)!.RegionId).ToHashSet();
        Assert.All(t.Regions.Where(r => r.Visited), r => Assert.Contains(r.Id, told));
        Assert.NotEmpty(Intel.ForSale(t));
    }

    [Fact]
    public void ASettlementWonOverAlliesAndSendsWarriorsToTheWalls()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        var s = t.Settlements.First(x => t.Regions[x.RegionId].Known);
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        double before = Threat.Defense(t);
        t.Treasury = 10000;
        while (!s.Allied && s.Envoys < 100) Assert.True(sim.SendEnvoy(s.Id) || s.Allied);
        World.Shift(t, s, 100);
        Assert.True(s.Allied);
        Assert.Contains(t.Ledger.Entries, e => e.Text.Contains("pledge friendship"));
        if (s.Kin.Boon.Troops > 0) Assert.True(Threat.Defense(t) > before);
        Assert.False(sim.SendEnvoy(999));
    }
}
