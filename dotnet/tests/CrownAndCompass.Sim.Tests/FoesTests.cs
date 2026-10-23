using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

public class FoesTests
{
    [Fact]
    public void EverySignatureFoeHoldsASiteInItsRings()
    {
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            // Foes a height of the sky seats on several sites are ReachesTests'.
            foreach (var f in t.Content.Foes.Foes.Where(f => !t.Content.World.Isles.Bands.Any(b => b.Foe == f.Id)))
            {
                var site = t.Sites.SingleOrDefault(s => s.Def.Foe == f.Id);
                Assert.True(site != null, $"seed {seed}: no site for {f.Id}");
                int ring = t.Regions[site!.RegionId!.Value].Ring;
                Assert.InRange(ring, f.Rings[0], f.Rings[^1]);
                Assert.Equal(t.Content.BossOf(f.Monster).Id, site.Def.Boss.Monster);
            }
        }
    }

    [Fact]
    public void FoesTakeTheirLineagesLeaders()
    {
        var t = new Simulation(new SimulationOptions { Seed = 2 }).Town;
        var site = t.Sites.Single(s => s.Def.Foe == "cloud-lords");
        var create = typeof(Menaces).GetMethod("Create", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var m = (Menace)create.Invoke(null, [t, site, site.Def.Kind, "test", 50.0, null, null, null, null, null])!;
        Assert.Equal("cloud-lords", m.Lineage);
        Assert.Contains(m.Leader, Annals.Lineage(t.Content.History, "cloud-lords")!.Leaders);
    }

    [Fact]
    public void TheDarkRangerPoachesABountyAndCanBeHired()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false, Enemy = false });
        var t = sim.Town;
        var target = t.Sites.Where(s => !s.Cleared && s.Def.Foe == "" && World.LevelOf(s) <= 2).OrderBy(s => s.Def.Id, StringComparer.Ordinal).First();
        t.Bounties.Add(new Bounty { Id = 999, SiteId = target.Def.Id, Reward = 120, PostedTick = t.Tick, ExpiresTick = t.Tick + 9999 });
        t.Tick = t.Content.Foes.Poaching.After * SimTime.HoursPerDay;
        Poaching.Daily(t);
        Assert.True(target.Cleared);
        Assert.Equal(1, t.Ranger!.Poached);
        Assert.Contains(t.Stories.Open, s => s.Def == "dark-ranger");
        int heroes = t.Heroes.Count;
        Poaching.Decide(t, 1);
        Assert.Equal(heroes + 1, t.Heroes.Count);
        Assert.Null(Poaching.Camp(t));
    }

    [Fact]
    public void ASpiritCallerRaisesTheRaidersWhoFell()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var site = t.Sites.Single(s => s.Def.Foe == "spirit-caller");
        site.Strength = 10;
        t.FallenRaiders = 20;
        typeof(Lairs).GetMethod("Raise", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.Invoke(null, [t, site]);
        Assert.Equal(20, site.Strength);
        Assert.Equal(0, t.FallenRaiders);
    }
}
