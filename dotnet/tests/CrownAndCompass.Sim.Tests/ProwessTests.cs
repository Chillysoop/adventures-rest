using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Heroes fight like their class at their level; damage types, regeneration and silver matter; foes aim by their wits.</summary>
public class ProwessTests
{
    private static readonly Content C = Content.Default();

    private static Town NewTown(int seed = 3) => new Simulation(new SimulationOptions { Seed = seed }).Town;

    private static Agent Hero(Town t, string cls, int level) => Quests.CreateHero(t, t.Content.Classes.First(x => x.Id == cls), level);

    [Fact]
    public void TheLibraryKnowsWhatHurtsWhat()
    {
        var troll = C.Library.Monster("troll")!;
        Assert.Contains("fire", troll.RegenStops);
        Assert.Contains("acid", troll.RegenStops);
        Assert.True(troll.RegenRises);
        Assert.Equal("slashing", troll.DamageType);
        Assert.Contains("slashing", C.Library.Monster("werewolf")!.Immune);
        Assert.Contains("bludgeoning", C.Library.Monster("skeleton")!.Vulnerable);
        Assert.Contains("fire", C.Library.Monster("fire-elemental")!.Immune);
        Assert.Equal("fire", C.Library.Spell("fireball")!.DamageType);
        Assert.Equal("dex", C.Library.Spell("fireball")!.Save);
        Assert.Equal("slashing", C.Library.GearOf("longsword")!.DamageType);
    }

    [Fact]
    public void DamageGoesThroughDefences()
    {
        var wolf = C.Library.Monster("werewolf");
        Assert.Equal((10, "steel"), Prowess.Harm(C, wolf, 20, "slashing", false, false));
        Assert.Equal((20, ""), Prowess.Harm(C, wolf, 20, "slashing", false, true));
        Assert.Equal((20, ""), Prowess.Harm(C, wolf, 20, "slashing", true, false));
        Assert.Equal((0, "immune"), Prowess.Harm(C, C.Library.Monster("fire-elemental"), 20, "fire", true, false));
        Assert.Equal((40, "vulnerable"), Prowess.Harm(C, C.Library.Monster("skeleton"), 20, "bludgeoning", false, false));
        Assert.Equal((10, "resist"), Prowess.Harm(C, C.Library.Monster("vampire"), 20, "necrotic", true, false));
    }

    [Fact]
    public void HeroesGainAttacksCantripsAndSlotsAsTheyLevel()
    {
        var t = NewTown();
        Assert.Equal(1, Prowess.Attacks(C, Hero(t, "fighter", 4).Hero!));
        Assert.Equal(2, Prowess.Attacks(C, Hero(t, "fighter", 5).Hero!));
        Assert.Equal(3, Prowess.Attacks(C, Hero(t, "fighter", 11).Hero!));
        Assert.Equal(2, Prowess.Attacks(C, Hero(t, "warden", 5).Hero!));
        Assert.Equal(1, Prowess.Attacks(C, Hero(t, "wizard", 11).Hero!));
        Assert.Equal(1, Prowess.CantripDice(C, 4));
        Assert.Equal(2, Prowess.CantripDice(C, 5));
        Assert.Equal(4, Prowess.CantripDice(C, 17));
        var wizard = C.Classes.First(c => c.Id == "wizard");
        Assert.True(Prowess.Casts(C, wizard, 9) > Prowess.Casts(C, wizard, 1));
        var known = Prowess.Castable(C, Hero(t, "wizard", 5).Hero!).Select(k => k.Play.Id).ToList();
        Assert.Contains("fireball", known);
        Assert.Contains("fire-bolt", known);
        Assert.DoesNotContain("cone-of-cold", known);
    }

    [Fact]
    public void EveryFightingSpellAndShapeIsInTheLibrary()
    {
        foreach (var p in C.Prowess.Spells) Assert.True(C.Library.Spell(p.Id) != null, $"{p.Id} is not in the library");
        foreach (var p in C.Prowess.Powers)
        {
            Assert.Contains(C.Classes, c => c.Id == p.Class);
            foreach (var s in p.Shapes) Assert.NotNull(C.Library.Monster(s.Ref)?.Damage);
            if (p.Spell != "") Assert.NotNull(C.Library.Spell(p.Spell));
        }
        // Every class with magic has a way to fight with it.
        foreach (var cls in C.Classes.Where(c => c.Spell)) Assert.Contains(Prowess.Spells(C, cls), k => k.Play.Kind == "cantrip");
    }

    private static string Delves(Town t, string siteMonster, IReadOnlyList<Agent> party, int runs = 30)
    {
        var site = C.Sites.FirstOrDefault(s => s.Boss.Monster == siteMonster) ?? C.Sites[0];
        var log = new List<string>();
        for (int seed = 1; seed <= runs; seed++)
        {
            foreach (var h in party)
            {
                h.Hero!.Hp = h.Hero.MaxHp;
                h.Hero.AwayUntil = null;
            }
            var r = Expedition.Run(new Rng(seed), C, site, party);
            log.AddRange(r.Lines);
        }
        return string.Join("\n", log);
    }

    [Fact]
    public void ClassesUseTheirPowersAndCastersChooseSpells()
    {
        var t = NewTown();
        var party = new[] { ("barbarian", 6), ("wizard", 6), ("cleric", 6), ("bard", 6), ("fighter", 6) }.Select(x => Hero(t, x.Item1, x.Item2)).ToList();
        string log = Delves(t, "goblin_boss", party);
        Assert.Contains("rage", log);
        Assert.Contains("surges", log);
        Assert.Contains("fighting song", log);
        Assert.Contains("casts", log);
    }

    [Fact]
    public void FightsReplayExactlyFromTheSameSeed()
    {
        static List<string> Run()
        {
            var t = NewTown(9);
            var party = new[] { ("paladin", 7), ("druid", 7), ("monk", 7), ("ranger", 7) }.Select(x => Hero(t, x.Item1, x.Item2)).ToList();
            return Expedition.Run(new Rng(5), C, C.Sites[0], party).Lines.ToList();
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void ClevererFoesGoForTheCasters()
    {
        // Over many fights, a clever foe's blows fall on the wizard more than a beast's do.
        var t = NewTown();
        var party = new[] { ("fighter", 3), ("barbarian", 3), ("wizard", 3) }.Select(x => Hero(t, x.Item1, x.Item2)).ToList();
        int Hits(string monster)
        {
            int n = 0;
            for (int seed = 1; seed <= 60; seed++)
            {
                foreach (var h in party) h.Hero!.Hp = h.Hero.MaxHp;
                var run = new Expedition.Venture(new Rng(seed), C, party);
                run.Combat(new EncounterDef { Kind = "combat", Monster = monster, Count = [1, 1], Text = "a foe" }, true);
                n += run.Lines.Count(l => l.Contains(party[2].GivenName + " goes down"));
            }
            return n;
        }
        Assert.True(Hits("mage") >= Hits("wolf"), "a mage should drop the wizard at least as often as a wolf does");
    }
}
