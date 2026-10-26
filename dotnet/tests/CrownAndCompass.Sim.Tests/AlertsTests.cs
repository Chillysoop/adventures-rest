using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class AlertsTests
{
    [Fact]
    public void StandingAlertsHaveOneKeyEachAndAKnownKind()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        for (int d = 0; d < 150; d++)
        {
            sim.RunDays(1);
            var list = Alerts.Standing(sim.Town);
            Assert.Equal(list.Count, list.Select(a => a.Key).Distinct().Count());
            Assert.All(list, a => Assert.Contains(a.Kind, Alerts.Kinds));
        }
    }

    [Fact]
    public void AnAlertClearsOnceItIsResolved()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        sim.RunDays(5);
        var t = sim.Town;
        double gold = t.Treasury;
        t.Treasury = 10;
        Assert.Contains(Alerts.Standing(t), a => a.Key == "treasury" && a.Kind.Level == AlertLevel.Urgent);
        t.Treasury = gold + 100;
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Key == "treasury");
    }

    [Fact]
    public void HeavyTaxesRaiseTheSettlersAlertAndNameThemAsTheCause()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(30);
        var t = sim.Town;
        var draw = Population.Appeal(t);
        Assert.Equal(Math.Max(0.05, Math.Min(1.6, draw.Parts.Sum(p => p.Value))), draw.Total, 9);
        // Steep enough to drown whatever else draws settlers on this day, however the campaign went.
        t.TaxRate = 0.6;
        var a = Assert.Single(Alerts.Standing(t), a => a.Key == "settlers");
        Assert.Contains("taxes at 60%", a.Tip);
        Assert.Equal(AlertWindow.City, a.Kind.Window);
    }

    [Fact]
    public void ReadingAlertsLeavesTheTownUntouched()
    {
        var quiet = new Simulation(new SimulationOptions { Seed = 4 });
        var watched = new Simulation(new SimulationOptions { Seed = 4 });
        for (int h = 0; h < 24 * 80; h++)
        {
            quiet.Step();
            watched.Step();
            Alerts.Standing(watched.Town);
        }
        Assert.Equal(quiet.Town.Ledger.Entries.Select(e => e.Format()), watched.Town.Ledger.Entries.Select(e => e.Format()));
    }

    [Fact]
    public void ARareFindRaisesAnAlertNamingTheCompanyOrTheHeroAndFadesAfterItsDays()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(3);
        var t = sim.Town;
        var hero = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 5);
        var items = t.Content.LootItems.Where(i => i.Category is "weapon" or "armor" or "wondrous-item").OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
        var common = items.First(i => i.Rarity == "uncommon");
        var rare = items.First(i => i.Rarity == "rare");
        var legend = items.First(i => i.Rarity == "legendary");
        var potion = t.Content.Library.Items.First(i => i.Category == "potion" && Spoils.Rank(i) >= 3);
        Spoils.Found(t, common, hero.GivenName, false, hero.Id, null, [hero]);
        Spoils.Found(t, potion, hero.GivenName, false, hero.Id, null, [hero]);
        Assert.Empty(t.Spoils.Finds);

        // A hero who carries it home: the alert looks at them.
        Gear.Claim(t, hero, rare);
        bool worn = hero.Hero!.Magic.Values.Contains(rare);
        Spoils.Found(t, rare, hero.GivenName, false, hero.Id, null, [hero]);
        // A company's find nobody wears waits in the armory.
        Armory.Add(t, legend, "the Iron Owls' spoils");
        Spoils.Found(t, legend, "the Iron Owls", true, null, null, []);
        // A second legendary for a company that already carries one only badges the rail.
        var veteran = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 12);
        var other = items.Last(i => i.Rarity == "legendary");
        veteran.Hero!.Magic["trinket"] = other;
        Spoils.Found(t, other, "the Old Hands", true, veteran.Id, null, [veteran]);
        Assert.False(t.Spoils.Finds[^1].Great);
        var finds = Alerts.Standing(t).Where(a => a.Item != null).ToList();
        var lone = finds.SingleOrDefault(a => a.Item == rare);
        if (worn)
        {
            Assert.NotNull(lone);
            Assert.Equal($"{hero.GivenName} finds the {rare.Name}", lone!.Text);
            Assert.Equal(hero.Id, lone.AgentId);
            Assert.Equal(Alerts.RareFind, lone.Kind);
        }
        var party = Assert.Single(finds, a => a.Item == legend && a.Kind == Alerts.Treasure);
        Assert.Equal($"The Iron Owls find the {legend.Name}", party.Text);
        Assert.Contains("armory", party.Tip);
        Assert.Equal(Alerts.Treasure, party.Kind);

        sim.RunDays(t.Content.Spoils.FindDays + 1);
        Assert.DoesNotContain(Alerts.Standing(t), a => a.Item == rare || a.Item == legend || a.Item == other);
    }
}
