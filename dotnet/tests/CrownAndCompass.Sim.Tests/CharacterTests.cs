using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>Character sheets for settlements and organised enemies, and each campaign's enemy tempers.</summary>
public class CharacterTests
{
    private static readonly Content C = Content.Default();

    [Fact]
    public void EverySettlementHasSixScoresASizeAPurposeTwoCustomsAndASecret()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(10);
        var t = sim.Town;
        Assert.NotEmpty(t.Settlements);
        foreach (var s in t.Settlements)
        {
            var sheet = Character.Sheet(t, s);
            Assert.Equal(6, sheet.Count);
            Assert.All(sheet, x => Assert.InRange(x.Value, 3, 18));
            Assert.NotNull(Character.SizeOf(t, s));
            var purpose = Character.PurposeOf(t, s);
            Assert.NotNull(purpose);
            Assert.True(purpose!.Peoples.Count == 0 || purpose.Peoples.Contains(s.Kin.Id), $"{s.Name} lives by {purpose.Id}");
            var customs = Character.CustomsOf(t, s);
            Assert.Equal(2, customs.Count);
            Assert.NotEqual(customs[0].Id, customs[1].Id);
            Assert.NotNull(Character.SecretOf(t, s));
            // The same seed always gives the same sheet.
            Assert.Equal(sheet.Select(x => x.Value), Character.Sheet(t, s).Select(x => x.Value));
        }
    }

    [Fact]
    public void EachPeopleNamesItsScoresItsOwnWay()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(2);
        var t = sim.Town;
        var dwarves = t.Settlements.FirstOrDefault(s => s.Kin.Id == "dwarves");
        if (dwarves != null) Assert.Contains(Character.Sheet(t, dwarves), x => x.Name == "Craft");
        var folk = t.Settlements.FirstOrDefault(s => s.Kin.Id == "villagers");
        if (folk != null) Assert.Contains(Character.Sheet(t, folk), x => x.Name == "Education");
    }

    [Fact]
    public void EveryPeopleHasSayingsAndContentNamesRealThings()
    {
        var r = C.Character.Settlement;
        foreach (var p in C.World.Peoples)
            Assert.True(r.Proverbs.TryGetValue(p.Id, out var list) && list.Count > 0, $"{p.Id} has no proverbs");
        var kinds = C.Saga.Menaces.Kinds.Keys.ToHashSet();
        Assert.All(r.Customs.SelectMany(c => c.Wards), k => Assert.Contains(k, kinds));
        Assert.All(r.Purposes.SelectMany(p => p.Peoples), id => Assert.Contains(C.World.Peoples, x => x.Id == id));
        Assert.All(r.Secrets.Where(s => s.Purpose != ""), s => Assert.Contains(r.Purposes, p => p.Id == s.Purpose));
        Assert.All(r.Tempers.Keys, k => Assert.Contains(C.Neighbours.Temperaments, x => x.Id == k));
        string[] slots = [.. Character.SettlementSlots, .. Character.MenaceSlots];
        var bonuses = r.Customs.SelectMany(c => c.Scores.Keys).Concat(r.Purposes.SelectMany(p => p.Scores.Keys)).Concat(r.Peoples.Values.SelectMany(p => p.Bonus.Keys.Concat(p.Names.Keys)));
        Assert.All(bonuses, k => Assert.Contains(k, Character.SettlementSlots));
        Assert.All(C.Character.Menace.Kinds.Values.SelectMany(k => k.Names.Keys), k => Assert.Contains(k, Character.MenaceSlots));
    }

    [Fact]
    public void EveryMenaceKindHasTempersThatItCanActOn()
    {
        var ambitions = C.Saga.Menaces.Ambitions;
        foreach (var kind in C.Saga.Menaces.Kinds.Keys)
        {
            var tempers = C.History.Tempers.Where(t => t.Subjects.Contains("menace:" + kind)).ToList();
            Assert.True(tempers.Count >= 2, $"{kind} needs at least two tempers so campaigns differ");
            foreach (var t in tempers)
            {
                Assert.NotEqual("", t.Text);
                foreach (var a in t.Ambitions.Keys)
                {
                    var def = ambitions.FirstOrDefault(x => x.Id == a);
                    Assert.True(def != null && (def.Kinds.Count == 0 || def.Kinds.Contains(kind)), $"{t.Id}: {kind} can't want {a}");
                }
            }
        }
        Assert.Equal(C.History.Tempers.Count, C.History.Tempers.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void TheSameFoesHaveDifferentTempersInDifferentCampaigns()
    {
        var seen = new HashSet<string>();
        for (int seed = 1; seed <= 12; seed++)
        {
            var sim = new Simulation(new SimulationOptions { Seed = seed });
            var temper = Character.TemperOf(sim.Town, "tribe");
            Assert.NotNull(temper);
            Assert.Equal(temper, Character.TemperOf(sim.Town, "tribe"));
            seen.Add(temper!.Id);
        }
        Assert.True(seen.Count >= 2, "every campaign rolled the same temper for the orcs");
    }

    [Fact]
    public void MenacesHaveSixScoresNamedForTheirKind()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        sim.RunDays(3);
        var t = sim.Town;
        var m = Menaces.Seed(t, [1, 2, 3, 4]);
        Assert.NotNull(m);
        var sheet = Character.Sheet(t, m!);
        Assert.Equal(6, sheet.Count);
        Assert.All(sheet, x => Assert.InRange(x.Value, 3, 18));
        if (m!.Kind == "dragon") Assert.Contains(sheet, x => x.Name == "Hoard");
        Assert.InRange(Character.Strike(t, m), 0.3, 2.5);
    }

    [Fact]
    public void PoorHealthLetsAFeverRunFasterAndFaithSlowsAShadow()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        sim.RunDays(2);
        var t = sim.Town;
        foreach (var s in t.Settlements)
        {
            int health = Character.Score(t, s, "health");
            double fever = Character.Trouble(t, s, "fever");
            Assert.True(health < 10 ? fever > 1 : health > 11 ? fever < 1 : true, $"{s.Name}: health {health}, fever pace {fever}");
            Assert.Equal(1, Character.Trouble(t, s, "missing-hunters"));
            int faith = Character.Score(t, s, "faith");
            bool warded = Character.CustomsOf(t, s).Any(c => c.Wards.Contains("undead"));
            if (faith > 11 || warded) Assert.True(Character.Shadow(t, s, "undead") < 1);
        }
    }

    [Fact]
    public void ADiplomaticPeopleWarmsFasterToTheTown()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, AutoMayor = false });
        sim.RunDays(2);
        var t = sim.Town;
        foreach (var s in t.Settlements.Where(s => !s.Razed && !s.Allied))
        {
            double before = s.Standing;
            World.Shift(t, s, 1);
            Assert.Equal(Character.Diplomacy(t, s), s.Standing - before, 6);
        }
    }

    [Fact]
    public void ANewAllyConfidesItsSecret()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5, AutoMayor = false });
        sim.RunDays(2);
        var t = sim.Town;
        var s = t.Settlements.First(x => !x.Razed && !x.Allied);
        Assert.False(Character.SecretKnown(t, s) && !s.Allied && !Neighbours.CastOf(t, s).Any());
        int before = t.Ledger.Entries.Count;
        World.Shift(t, s, 200);
        Assert.True(s.Allied);
        Assert.True(Character.SecretKnown(t, s));
        Assert.Contains(t.Ledger.Entries.Skip(before), e => e.Text.Contains("confides"));
    }
}
