using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// Magic items' powers, the parts and drops foes leave, the forge's recipes and plain arms for the watch: the content
/// is whole (every creature has a part, every enemy something to drop, every ref an item) and the commands work.
/// </summary>
public class SpoilsTests
{
    private static readonly Content C = Content.Default();

    [Fact]
    public void EveryCreatureLeavesAPart()
    {
        var missing = C.Library.Monsters.Where(m => C.Spoils.For(m.Id, m.Name, m.Type, m.Cr) == null).Select(m => $"{m.Id} ({m.Type})").ToList();
        Assert.True(missing.Count == 0, "no part in spoils.json for: " + string.Join(", ", missing.Take(20)));
        // Words come first, and whole: a frost giant gives its heart, a pirate is no rat.
        Assert.Equal("rime-hearts", C.Spoils.For("t1", "Frost Giant", "giant", 8)!.Id);
        Assert.Equal("wolf-fangs", C.Spoils.For("t2", "Dire Wolf", "beast", 1)!.Id);
        Assert.NotEqual("rat-tails", C.Spoils.For("t3", "Pirate", "humanoid", 1)!.Id);
    }

    [Fact]
    public void EveryEnemyHasSomethingToDrop()
    {
        var d = C.Spoils.Drops;
        foreach (var g in C.Groups.Groups) Assert.True(d.Any(x => x.Groups.Contains(g.Id)), $"group {g.Id} drops nothing");
        foreach (var f in C.Foes.Foes) Assert.True(d.Any(x => x.Foes.Contains(f.Id)), $"foe {f.Id} drops nothing");
        foreach (var k in C.Saga.Menaces.Kinds.Keys) Assert.True(d.Any(x => x.Kinds.Contains(k)), $"menace kind {k} drops nothing");
        foreach (var p in C.World.Peoples) Assert.True(d.Any(x => x.Peoples.Contains(p.Id)), $"people {p.Id} drops nothing");
        foreach (var l in C.History.Lineages.Where(l => l.Subjects.Any(s => s.StartsWith("menace:", StringComparison.Ordinal))))
            Assert.True(d.Any(x => x.Lineages.Contains(l.Id)), $"menace line {l.Id} drops nothing");
    }

    [Fact]
    public void EveryRefIsAnItemAndEveryPowerIsWhole()
    {
        foreach (var x in C.Spoils.Drops) Assert.True(C.Library.Item(x.Ref) != null, $"drop {x.Ref} is no item");
        foreach (var r in C.Spoils.Recipes)
        {
            Assert.True(C.Library.Item(r.Ref) != null, $"recipe {r.Id} makes no item");
            Assert.NotEmpty(r.At);
            foreach (var b in r.At) Assert.True(C.Buildings.Any(x => x.Id == b), $"recipe {r.Id}: no building {b}");
            foreach (var n in r.Needs.Keys) Assert.True(n.StartsWith("grade:", StringComparison.Ordinal) || C.Spoils.Trophy(n) != null, $"recipe {r.Id}: no part {n}");
        }
        var kinds = C.Enchant.Kinds.Keys.ToHashSet();
        foreach (var p in C.Enchant.Items)
        {
            Assert.True(C.Library.Item(p.Ref) != null, $"power on {p.Ref}, which is no item");
            Assert.True(kinds.Contains(p.Kind), $"{p.Ref}: no kind {p.Kind}");
            if (p.Kind is "blast" or "chain" or "heal" or "hold" or "rout" or "sure") Assert.True(p.Uses >= 1, $"{p.Ref}'s {p.Kind} needs uses");
            if (p.Kind is "strike" or "bane" or "blast" or "chain" or "heal" or "regen") Assert.True(p.Roll != null, $"{p.Ref}'s {p.Kind} needs dice");
            if (p.Element != "") Assert.True(C.Enchant.Element(p.Element) != null, $"{p.Ref}: no element {p.Element}");
            foreach (var cls in p.Classes) Assert.True(C.Classes.Any(c => c.Id == cls), $"{p.Ref}: no class {cls}");
        }
        foreach (var b in C.Enchant.Basic.Items) Assert.True(C.Library.Item(b) != null, $"basic {b} is no item");
        // Drops only come from their foes: never from a hoard or a caravan.
        Assert.DoesNotContain(C.LootItems, i => i.Id == "rimegrip-gauntlets");
    }

    [Fact]
    public void ClassLockedItemsSuitOnlyTheirClass()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        var monk = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "monk"), 5);
        var wizard = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "wizard"), 5);
        var wraps = t.Content.Library.Item("monks-falling-star-wraps")!;
        Assert.True(Gear.Gain(t.Content, monk, wraps) > 0);
        Assert.Equal(0, Gear.Gain(t.Content, wizard, wraps));
    }

    [Fact]
    public void PowersBeatPlainBonusesForHeroes()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 6);
        a.Hero!.Curse = "";
        Gear.Claim(t, a, t.Content.Library.Item("weapon-1")!);
        a.Hero.Curse = "";
        // A +1 axe that freezes on a crit is worth more to a hero than the plain +1 it replaces.
        Assert.True(Gear.Gain(t.Content, a, t.Content.Library.Item("rimecleaver")!) > 0);
    }

    [Fact]
    public void PartsSellAndTheForgeMakesArms()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        var forgeDef = t.Content.Building("forge");
        t.AddBuilding(forgeDef, Land.Find(t, forgeDef, null)!, null, "the Forge", true);
        t.Treasury = 5000;
        t.Spoils.Trophies["wolf-fangs"] = 6;
        t.Spoils.Trophies["rat-tails"] = 3;
        double before = t.Treasury;
        Assert.True(sim.SellParts("rat-tails", 3) > 0);
        Assert.True(t.Treasury > before);
        Assert.False(t.Spoils.Trophies.ContainsKey("rat-tails"));

        // Wolfsbane charms need an apothecary; plain +1 arms a forge and a charter.
        Assert.NotNull(Spoils.Refuse(t, "wolfsbane-charm"));
        var plain = t.Content.Spoils.Recipe("plain-weapon-1")!;
        t.City.Tier = Math.Max(t.City.Tier, plain.Tier);
        Assert.Null(Spoils.Refuse(t, plain.Id));
        Assert.True(sim.Craft(plain.Id));
        Assert.Single(t.Spoils.Orders);
        int held = t.Armory.Stock.Count;
        sim.RunDays(plain.Days + 1);
        Assert.Empty(t.Spoils.Orders);
        Assert.Contains(t.Armory.Stock, s => s.Item.Id == "weapon-1");
        Assert.True(t.Armory.Stock.Count > held);
    }

    [Fact]
    public void PlainArmsMakeTheWatchStronger()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        t.Watch = Math.Max(t.Watch, 2);
        Assert.Equal(1, Kit.WatchFactor(t));
        var magic = Armory.Add(t, t.Content.Library.Item("rimecleaver")!, "a test");
        Assert.False(sim.IssueArms(magic.Id, Kit.Watch));
        Armory.Add(t, t.Content.Library.Item("weapon-2")!, "a test");
        Armory.Add(t, t.Content.Library.Item("armor-1")!, "a test");
        Assert.Equal(2, sim.ArmUnit(Kit.Watch));
        Assert.True(Kit.WatchFactor(t) > 1);
        var issued = t.Armory.Issued.First();
        Assert.True(sim.RecallArms(issued.Id));
        Assert.Contains(t.Armory.Stock, s => s.Id == issued.Id);
    }

    [Fact]
    public void MayorRegaliaWardsAgainstKnivesAndCharms()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        Assert.Equal(0, Regalia.Ward(t));
        Assert.False(Regalia.Charm(t));
        var signet = Armory.Add(t, t.Content.Library.Item("vampire-lords-signet")!, "a test");
        var circlet = Armory.Add(t, t.Content.Library.Item("circlet-of-clear-sight")!, "a test");
        Assert.True(Regalia.Equip(t, signet.Id));
        Assert.True(Regalia.Equip(t, circlet.Id));
        Assert.True(Regalia.Ward(t) > 0);
        Assert.True(Regalia.Charm(t));
        Assert.True(Regalia.Truth(t) > 0);
        // Heroes can't wear the mayor's pieces.
        var a = Quests.CreateHero(t, t.Content.Classes.First(c => c.Id == "fighter"), 5);
        Assert.Equal(0, Gear.Gain(t.Content, a, t.Content.Library.Item("ninefold-seal")!));
    }

    [Fact]
    public void HallRelicsDoWhatTheySay()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 4, AutoMayor = false });
        var t = sim.Town;
        double escort = Institutions.Escort(t), truth = Regalia.Truth(t);
        int defence = (int)Institutions.Defence(t);
        Watch.ReceiveRelic(t, t.Content.Library.Item("caravan-bell")!, "Test Company");
        Watch.ReceiveRelic(t, t.Content.Library.Item("broken-warlord-tusk")!, "Test Company");
        Watch.ReceiveRelic(t, t.Content.Library.Item("unveiling-mirror")!, "Test Company");
        Assert.True(Institutions.Escort(t) > escort);
        Assert.True(Institutions.Defence(t) > defence);
        Assert.True(Regalia.Truth(t) > truth);
        // Kept in the hall, not worn: the mayor can't put one on.
        Assert.False(Regalia.Is(t, t.Content.Library.Item("caravan-bell")!));
        Assert.DoesNotContain(t.Armory.Stock, s => s.Item.Id == "caravan-bell");
        // The loupe makes the forge cheaper.
        var rec = t.Content.Spoils.Recipe("plain-weapon-1")!;
        double cost = Spoils.Cost(t, rec);
        Watch.ReceiveRelic(t, t.Content.Library.Item("enchanters-ley-loupe")!, "Test Company");
        Assert.True(Spoils.Cost(t, rec) < cost);
    }

    [Fact]
    public void EveryLootItemDoesSomething()
    {
        // A piece heroes or the town can find does more than its +N: a power, a way across, a hall effect or plain arms.
        var passage = C.Passage.Items.Select(i => i.Item).ToHashSet();
        var hall = C.Regalia.Items.Select(i => i.Ref).ToHashSet();
        var watched = C.Knowledge.Works.Select(w => w.Relic).ToHashSet();
        var plain = C.LootItems.Where(i => Enchant.Of(C, i).Count == 0 && !Enchant.Basic(C, i) && !passage.Contains(i.Id) && !hall.Contains(i.Id) && !watched.Contains(i.Id)
            && !C.Armory.Start.Contains(i.Id) && C.Armory.Stone != i.Id && !System.Text.RegularExpressions.Regex.IsMatch(i.Name, @"\+\d")).Select(i => i.Id).ToList();
        Assert.True(plain.Count <= Allowed.Count, "items that only give a bonus: " + string.Join(", ", plain.Except(Allowed)));
    }

    /// <summary>Bonus-only pieces Conner is weighing whether to keep (asked 2026-10-23); shrink as they get effects or go.</summary>
    private static readonly HashSet<string> Allowed =
    [
        "amulet-of-the-planes",
        "armor-of-vulnerability",
        "bag-of-beans",
        "bag-of-devouring",
        "cubic-gate",
        "decanter-of-endless-water",
        "deck-of-many-things",
        "dragon-orb",
        "dust-of-dryness",
        "feather-token",
        "feather-token-anchor",
        "feather-token-fan",
        "feather-token-tree",
        "manual-of-golems",
        "marvelous-pigments",
        "mysterious-deck",
        "potion-of-fire-giant-strength",
        "shield-of-missile-attraction",
        "sovereign-glue",
        "talisman-of-the-sphere",
        "universal-solvent",
        "well-of-many-worlds",
    ];
}
