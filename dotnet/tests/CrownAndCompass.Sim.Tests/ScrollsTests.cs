using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class ScrollsTests
{
    [Fact]
    public void CastersBuyScrollsAtTheScriptoriumAndOthersDoNot()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 1 });
        var t = sim.Town;
        var sr = t.Content.Shrines.Scroll;
        for (int d = 0; d < 600 && !t.Buildings.Any(b => sr.Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale); d++) sim.RunDays(1);
        var shop = t.Buildings.First(b => sr.Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale);
        var heroes = t.Agents.Where(a => a.Hero != null).OrderBy(a => a.Id).ToList();
        var caster = heroes.First(a => Scrolls.Reads(a.Hero!.Cls));
        var fighter = heroes.FirstOrDefault(a => !Scrolls.Reads(a.Hero!.Cls));
        foreach (var h in new[] { caster, fighter }.Where(h => h != null)) { h!.Gold = 500; h.Hero!.Scrolls = 0; }
        // The caster may buy at any of the town's scroll shops (a scriptorium, a mage tower).
        var shops = t.Buildings.Where(b => sr.Shops.Contains(b.Def.Id) && b.Complete && !b.Closed && !b.ForSale).ToList();
        double cash = shops.Sum(b => b.Cash);
        Scrolls.Outfit(t, new[] { caster, fighter }.Where(h => h != null).ToList()!);
        Assert.Equal(sr.Carry, caster.Hero!.Scrolls);
        Assert.True(shops.Sum(b => b.Cash) > cash);
        if (fighter != null) Assert.Equal(0, fighter.Hero!.Scrolls);
    }

    [Fact]
    public void HeroesReadScrollsAndWizardsCopyThemOnDelves()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 2 });
        sim.RunDays(400);
        var heroes = sim.Town.Agents.Where(a => a.Hero != null).ToList();
        Assert.Contains(heroes, a => a.Hero!.Copied > 0 || a.Hero.Scrolls > 0);
    }
}
