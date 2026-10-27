using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// Manners (content/game/manners.json): every faction gets a personality from its own data, a first meeting composed
/// from it, rolls against it moved by it, and overtures that win it its own way.
/// </summary>
public class MannersTests
{
    private static readonly Content C = Content.Default();

    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    [Fact]
    public void EveryPeoplePowerAndEnemyLineageHasManners()
    {
        var r = C.Manners;
        foreach (var p in C.World.Peoples) Assert.True(Manners.OfPeople(r, C.History, p).Count > 0, $"people {p.Id} has no manners: give its lineage traits");
        foreach (var d in C.Powers.Powers) Assert.True(Manners.OfPower(r, C.History, d).Count > 0, $"power {d.Id} has no manners: its likes map to none");
        foreach (var l in C.History.Lineages.Where(l => l.Subjects.Any(s => s.StartsWith("menace:") || s.StartsWith("foe:"))))
            Assert.True(Manners.OfLineage(r, l).Count > 0, $"lineage {l.Id} has no manners: give it ambitions or traits");
    }

    [Fact]
    public void EveryMannerIsWholeAndItsIconsExist()
    {
        var icons = Path.Combine(Repo(), "godot", "assets", "icons");
        foreach (var m in C.Manners.Manners)
        {
            Assert.True(m.Greet != null && m.Terms != null && m.Parley != null && m.Overture != null, $"manner {m.Id} needs greet, terms, parley and overture");
            Assert.True(m.Tip != "" && m.Arrive != "", $"manner {m.Id} needs a tip and an arrival");
            foreach (var icon in new[] { m.Icon, m.Overture!.Icon })
                Assert.True(File.Exists(Path.Combine(icons, icon + ".svg")), $"manner {m.Id}: no icon {icon}");
            foreach (var skill in m.Dc.Keys.Append(m.Overture.Skill).Where(s => s != ""))
                Assert.True(C.Stories.Skill(skill) != null, $"manner {m.Id} names no skill {skill}");
        }
        foreach (var v in C.Manners.Traits.Values.Concat(C.Manners.Tempers.Values).Concat(C.Manners.MenaceTempers.Values).Concat(C.Manners.Ambitions.Values).Concat(C.Manners.Likes.Values))
            Assert.True(C.Manners.Manner(v) != null, $"manners.json maps to {v}, which is no manner");
        foreach (var t in C.Neighbours.Temperaments) Assert.True(C.Manners.Tempers.ContainsKey(t.Id), $"temperament {t.Id} maps to no manner");
        foreach (var t in C.History.Tempers) Assert.True(C.Manners.MenaceTempers.ContainsKey(t.Id), $"menace temper {t.Id} maps to no manner");
    }

    [Fact]
    public void EveryPeopleAndEnemyLineageHasItsOwnFirstMeeting()
    {
        foreach (var p in C.World.Peoples) Assert.NotNull(C.Stories.Story(Manners.PeoplePrefix + p.Id));
        foreach (var l in C.History.Lineages.Where(l => l.Subjects.Any(s => s.StartsWith("menace:") || s.StartsWith("foe:"))))
        {
            Assert.NotNull(C.Stories.Story(Manners.MenacePrefix + l.Id));
            Assert.NotNull(C.Stories.Story(Manners.SignsPrefix + l.Id));
        }
        var composed = C.Stories.Stories.Where(d => d.Id.StartsWith(Manners.PeoplePrefix) || d.Id.StartsWith(Manners.MenacePrefix) || d.Id.StartsWith(Manners.SignsPrefix)).ToList();
        foreach (var d in composed)
            foreach (var st in d.Stages)
                foreach (var o in st.Options)
                    foreach (var e in new[] { o.Success, o.Failure }.SelectMany(x => x.Effects.Keys))
                        Assert.True(Stories.KnownEffect(C, e), $"{d.Id} option {o.Label} has effect {e}");
        // No two peoples open the same way.
        var openings = C.World.Peoples.Select(p => C.Stories.Story(Manners.PeoplePrefix + p.Id)!.Stages[0].Text).ToList();
        Assert.Equal(openings.Count, openings.Distinct().Count());
    }

    [Fact]
    public void APeopleMetOpensItsOwnMeetingAndOnlyItsRulersOptionsShow()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        sim.RunDays(1);
        var s = t.Settlements.First(x => !x.Razed && t.Regions[x.RegionId].Known && !Discovery.Known(t, Neighbours.Key(x)) && x.Reign != null);
        Assert.True(Neighbours.Introduce(t, s, Discovery.Met));
        var story = t.Stories.Open.Last();
        Assert.Equal(Manners.PeoplePrefix + s.Kin.Id, story.Def);
        var stage = Stories.StageOf(t, story);
        var all = Manners.All(t, Neighbours.Key(s));
        foreach (var o in stage.Options)
        {
            var need = o.Needs.FirstOrDefault(n => n.StartsWith(Manners.Need));
            Assert.Equal(need != null && !all.Contains(need[Manners.Need.Length..]), Stories.Hidden(t, story, o));
        }
        Assert.Contains(stage.Options, o => !Stories.Hidden(t, story, o) && o.Rolls);
    }

    [Fact]
    public void MannersMoveTheDcOfRollsAgainstThem()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        foreach (var s in t.Settlements)
        {
            string key = Neighbours.Key(s);
            int expected = Manners.Of(t, key).Sum(m => m.Dc.GetValueOrDefault("history"));
            Assert.Equal(Math.Clamp(expected, -C.Manners.DcCap, C.Manners.DcCap), Manners.Lean(t, key, "history"));
        }
        // A proud people won't be threatened.
        var proud = t.Settlements.FirstOrDefault(s => Manners.Of(t, Neighbours.Key(s)).Any(m => m.Id == "proud"));
        if (proud != null) Assert.True(Manners.Lean(t, Neighbours.Key(proud), "intimidation", ruler: false) > Manners.Lean(t, Neighbours.Key(proud), "history", ruler: false));
    }

    [Fact]
    public void AnOvertureMovesStandingAndWaitsBeforeItCanBeMadeAgain()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        sim.RunDays(1);
        t.Treasury = 5000;
        var s = t.Settlements.First(x => !x.Razed && t.Regions[x.RegionId].Known);
        Discovery.Meet(t, Neighbours.Key(s), Discovery.Met);
        string key = Neighbours.Key(s);
        var (m, o) = Manners.Overtures(t, key).First();
        double before = s.Standing;
        var beat = sim.Overture(key, m.Id);
        Assert.NotNull(beat);
        Assert.NotEqual(before, s.Standing);
        Assert.Equal(t.Day + o.Every, Manners.Ready(t, key, m.Id));
        Assert.Null(sim.Overture(key, m.Id));
        Assert.StartsWith("again", Manners.Refuse(t, key, m.Id));
        Assert.Null(sim.Overture(key, "no-such-manner"));

        Powers.Init(t);
        var p = t.Powers.First();
        string pk = Powers.Key(p.Id);
        Assert.NotNull(Manners.Refuse(t, pk, Manners.Overtures(t, pk).First().Manner.Id));
        Discovery.Meet(t, pk, Discovery.Met);
        double was = p.Standing;
        Assert.NotNull(sim.Overture(pk, Manners.Overtures(t, pk).First().Manner.Id));
        Assert.NotEqual(was, p.Standing);
    }

    [Fact]
    public void AMenaceMetSendsItsHeraldOnce()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3, AutoMayor = false });
        var t = sim.Town;
        sim.RunDays(5);
        var m = Menaces.Seed(t, [2, 3])!;
        Assert.NotNull(m);
        int open = t.Stories.All.Count;
        Discovery.Meet(t, m, Discovery.Sighted);
        sim.RunDays(1);
        var heralds = t.Stories.All.Skip(open).Where(s => s.MenaceId == m.Id && Stories.DefOf(t, s).Source == Powers.ContactSource).ToList();
        Assert.Single(heralds);
        Assert.StartsWith(Manners.Mindless(m) ? Manners.SignsPrefix : Manners.MenacePrefix, heralds[0].Def);
        sim.RunDays(2);
        Assert.Single(t.Stories.All.Skip(open), s => s.MenaceId == m.Id && Stories.DefOf(t, s).Source == Powers.ContactSource);
    }
}
