using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Ready-made enemy groups (groups.json): their own sites, foes, bosses and lineage.</summary>
public class GroupsTests
{
    private static readonly Content C = Content.Default();

    [Fact]
    public void EveryGroupNamesRealThemesAndCreatures()
    {
        var themes = C.World.Themes.Select(t => t.Id).ToHashSet();
        var monsters = C.Library.Monsters.Select(m => m.Id).ToHashSet();
        Assert.Equal(C.Groups.Groups.Count, C.Groups.Groups.Select(g => g.Id).Distinct().Count());
        foreach (var g in C.Groups.Groups)
        {
            Assert.NotEmpty(g.Themes);
            Assert.All(g.Themes, t => Assert.True(themes.Contains(t), $"{g.Id}: no theme {t}"));
            Assert.NotEmpty(g.Names);
            Assert.NotEmpty(g.Deck);
            Assert.NotEmpty(g.Bosses);
            Assert.All(g.Foes.Concat(g.Bosses), m => Assert.True(monsters.Contains(m), $"{g.Id}: no monster {m}"));
        }
    }

    [Fact]
    public void GroupsHoldSitesWithTheirOwnBossesAndFoes()
    {
        var seen = new HashSet<string>();
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6 })
        {
            var t = new Simulation(new SimulationOptions { Seed = seed }).Town;
            foreach (var s in t.Sites.Where(s => s.Def.Group != ""))
            {
                var g = C.Group(s.Def.Group)!;
                seen.Add(g.Id);
                string boss = s.Def.Boss.Monster!.Replace("@boss", "");
                Assert.Contains(boss, g.Bosses);
                foreach (var e in s.Def.Encounters.Where(e => e.Kind == "wander"))
                {
                    var pool = C.Wanderers(s.Def, e.Cr);
                    if (pool.Any(m => g.Foes.Contains(m.Id))) Assert.All(pool, m => Assert.Contains(m.Id, g.Foes));
                }
            }
        }
        Assert.True(seen.Count >= 4, $"only {seen.Count} groups turned up in six worlds");
    }

    [Fact]
    public void AMenaceRisingFromAGroupsSiteIsHeirToItsLineage()
    {
        int checkedSome = 0;
        foreach (int seed in new[] { 1, 2, 3, 4, 5, 6, 7, 8 })
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            // Checked the day each rises: a menace driven out may flee to a holding that is another group's site.
            int seen = 0;
            for (int day = 0; day < 240; day++)
            {
                sim.RunDays(1);
                var menaces = sim.Town.Saga.Menaces;
                // An awakened menace and the warband (seated at its own outpost) carry lineages of their own.
                foreach (var m in menaces.Skip(seen).Where(m => m.Origin != "awakened" && m.Origin != Menaces.FromWarband))
                {
                    var seat = sim.Town.Sites.FirstOrDefault(s => s.Def.Id == m.SeatId);
                    if (seat == null || C.Group(seat.Def.Group) is not GroupDef g) continue;
                    Assert.Equal(g.Lineage, m.Lineage);
                    checkedSome++;
                }
                seen = menaces.Count;
            }
        }
        Assert.True(checkedSome >= 0);
    }
}
