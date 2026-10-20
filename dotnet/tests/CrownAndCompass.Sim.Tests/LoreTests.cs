using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// The lore check: every menace kind, people, faction and hero background descends from a power in the
/// world's history (content/game/history.json), and that lineage decides what it wants and what it is.
/// A new one without a lineage fails here.
/// </summary>
public class LoreTests
{
    private static readonly Content C = Content.Default();
    private static HistoryRules H => C.History;

    private static void HasLineage(string subject)
    {
        var l = Annals.LineageOf(H, subject);
        Assert.True(l != null, $"{subject} has no lineage in content/game/history.json");
        Assert.True(l!.Motive != "", $"{subject}'s lineage {l.Id} says nothing of what it wants");
    }

    [Fact]
    public void EveryEnemyAllyFactionAndBackgroundHasALineage()
    {
        foreach (var kind in C.Saga.Menaces.Kinds.Keys) HasLineage("menace:" + kind);
        foreach (var people in C.World.Peoples) HasLineage("people:" + people.Id);
        foreach (var f in C.Library.Factions) HasLineage("faction:" + f.Id);
        foreach (var b in C.Library.Backgrounds.Where(b => b.Source == "crown-and-compass")) HasLineage("background:" + b.Id);
        foreach (var m in C.Mercenaries.Companies) HasLineage("mercenary:" + m.Id);
        foreach (var i in C.Institutions.Institutions) HasLineage("institution:" + i.Id);
        foreach (var g in C.Guilds.Guilds) HasLineage("guild:" + g.Id);
        foreach (var f in C.Foes.Foes) HasLineage("foe:" + f.Id);
    }

    [Fact]
    public void LineagesNameRealPowersTraitsClassesAndAmbitions()
    {
        var themes = C.World.Themes.Select(t => t.Id).ToHashSet();
        var ambitions = C.Saga.Menaces.Ambitions;
        Assert.Equal(H.Lineages.Count, H.Lineages.Select(l => l.Id).Distinct().Count());
        foreach (var l in H.Lineages)
        {
            Assert.True(H.Powers.Any(p => p.Id == l.Power), $"{l.Id}: no power {l.Power}");
            Assert.NotEmpty(l.Subjects);
            Assert.All(l.Traits, t => Assert.True(Enum.TryParse<Trait>(t, out _), $"{l.Id}: no trait {t}"));
            Assert.All(l.Classes, c => Assert.True(C.Classes.Any(x => x.Id == c), $"{l.Id}: no class {c}"));
            Assert.All(l.Themes, t => Assert.True(themes.Contains(t), $"{l.Id}: no theme {t}"));
            foreach (var s in l.Subjects)
            {
                string[] parts = s.Split(':');
                Assert.True(parts.Length == 2, $"{l.Id}: subject {s}");
                bool real = parts[0] switch
                {
                    "menace" => C.Saga.Menaces.Kinds.ContainsKey(parts[1]),
                    "people" => C.World.Peoples.Any(p => p.Id == parts[1]),
                    "faction" => C.Library.Faction(parts[1]) != null,
                    "background" => C.Library.Background(parts[1]) != null,
                    "mercenary" => C.Mercenaries.Companies.Any(m => m.Id == parts[1]),
                    "institution" => C.Institutions.Def(parts[1]) != null,
                    "guild" => C.Guilds.Guilds.Any(g => g.Id == parts[1]),
                    "foe" => C.Foes.Foes.Any(f => f.Id == parts[1]),
                    _ => false,
                };
                Assert.True(real, $"{l.Id}: {s} is not in the game");
                if (parts[0] == "background") Assert.True(l.Classes.Count > 0, $"{l.Id}: no class comes up through it");
                if (parts[0] != "menace" && parts[0] != "foe") Assert.True(l.Themes.Count == 0 && l.Ambitions.Count == 0, $"{l.Id}: themes and ambitions are for menaces");
                // A lineage only makes an ambition likelier where the kind can want it at all.
                foreach (var a in l.Ambitions.Keys)
                {
                    var def = ambitions.FirstOrDefault(x => x.Id == a);
                    Assert.True(def != null, $"{l.Id}: no ambition {a}");
                    if (parts[0] == "menace") Assert.True(def!.Kinds.Count == 0 || def.Kinds.Contains(parts[1]), $"{l.Id}: {parts[1]} can't want {a}");
                    string? foeKind = parts[0] == "foe" ? C.Foes.Foes.First(f => f.Id == parts[1]).Kind : null;
                    if (foeKind != null) Assert.True(def!.Kinds.Count == 0 || def.Kinds.Contains(foeKind), $"{l.Id}: a {foeKind} foe can't want {a}");
                }
            }
        }
    }

    [Fact]
    public void EachMenaceKindHasOneLineageForEverywhereAndOnePerTheme()
    {
        foreach (var kind in C.Saga.Menaces.Kinds.Keys)
        {
            var heirs = H.Lineages.Where(l => l.Subjects.Contains("menace:" + kind)).ToList();
            Assert.Single(heirs, l => l.Themes.Count == 0);
            foreach (var theme in heirs.SelectMany(l => l.Themes).GroupBy(t => t))
                Assert.True(theme.Count() == 1, $"{kind} has two lineages in {theme.Key}");
        }
        foreach (var foe in C.Saga.Finale.Fallback)
            Assert.True(Annals.Lineage(H, foe.Lineage)?.Subjects.Contains("menace:" + foe.Kind) == true, $"{foe.Leader} has no {foe.Kind} lineage");
    }

    [Fact]
    public void EveryClassCanComeUpThroughABackground()
    {
        foreach (var cls in C.Classes)
            Assert.True(H.Lineages.Any(l => l.Subjects.Any(s => s.StartsWith("background:")) && l.Classes.Contains(cls.Id)), $"no background raises a {cls.Id}");
    }

    [Fact]
    public void HeroesAndMenacesTakeTheirLineage()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 5 });
        for (int d = 0; d < 400 && sim.Town.Saga.Menaces.Count(m => m.Named) < 2; d++) sim.RunDays(1);
        var t = sim.Town;
        Assert.NotEmpty(t.Heroes);
        // Turncoats (Turning.Join) come with a past instead of a background.
        foreach (var h in t.Heroes.Where(h => h.Hero!.Background != ""))
        {
            // A foe who came over to the town keeps the lineage of what it was, not a background.
            if (t.Saga.Menaces.FirstOrDefault(m => m.TurnedHeroId == h.Id) is { } was)
            {
                Assert.NotNull(Annals.Lineage(H, was.Lineage));
                continue;
            }
            var bg = Annals.LineageOf(H, "background:" + h.Hero!.Background);
            Assert.True(bg != null, $"{h.Name} has no background");
            Assert.Contains(h.Hero.Cls.Id, bg!.Classes);
        }
        Assert.NotEmpty(t.Saga.Menaces);
        foreach (var m in t.Saga.Menaces)
        {
            var l = Annals.Lineage(H, m.Lineage);
            Assert.True(l != null, $"{m.Name} has no lineage");
            // A signature foe's menace keeps its own lineage (foes.json), whatever kind it raids as.
            Assert.True(l!.Subjects.Contains("menace:" + m.Kind) || l.Subjects.Any(x => x.StartsWith("foe:")), $"{m.Name}: {l.Id} is not a {m.Kind} lineage");
            Assert.NotEqual("", Annals.Motive(t, m));
        }
    }
}
