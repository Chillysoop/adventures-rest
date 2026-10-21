using System.Text;
using CrownAndCompass.Sim.Systems;

namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// First meetings (content/game/intros.json): every lineage the town can meet (a menace kind's, a foe's, an enemy
/// group's, a people's or a power's) has an intro, and every intro's scene, crest and props exist.
/// </summary>
public class IntrosTests
{
    private static readonly Content C = Content.Default();

    private static string Repo()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "CLAUDE.md"))) dir = dir.Parent;
        return dir!.FullName;
    }

    /// <summary>The baked model names (godot/src/Map/Models.cs reads the same layout).</summary>
    private static HashSet<string> Models()
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in new[] { "kenney.bin", "crafted.bin" })
        {
            using var r = new BinaryReader(File.OpenRead(Path.Combine(Repo(), "godot", "assets", "models", file)));
            r.ReadBytes(4);
            uint count = r.ReadUInt32();
            for (int i = 0; i < count; i++)
            {
                names.Add(Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte())));
                uint tris = r.ReadUInt32();
                r.BaseStream.Seek(tris * 40L, SeekOrigin.Current);
            }
        }
        return names;
    }

    [Fact]
    public void EveryLineageTheTownCanMeetHasAnIntro()
    {
        var h = C.History;
        var met = h.Lineages.Where(l => l.Subjects.Any(s => s.Split(':')[0] is "menace" or "foe" or "group" or "people" or "faction"));
        foreach (var l in met) Assert.True(Intros.Def(TestTown(), l.Id) != null, $"lineage {l.Id} has no intro in content/game/intros.json");
    }

    [Fact]
    public void EveryIntroIsWhole()
    {
        var models = Models();
        var icons = Path.Combine(Repo(), "godot", "assets", "icons");
        Assert.Equal(C.Intros.Intros.Count, C.Intros.Intros.Select(i => i.Lineage).Distinct().Count());
        foreach (var i in C.Intros.Intros)
        {
            Assert.True(Annals.Lineage(C.History, i.Lineage) != null, $"intro {i.Lineage} names no lineage in history.json");
            Assert.True(C.Intros.Scenes.Any(s => s.Id == i.Scene), $"intro {i.Lineage} has no scene {i.Scene}");
            Assert.True(File.Exists(Path.Combine(icons, $"crest-{i.Lineage}.svg")), $"no crest-{i.Lineage}.svg: run node scripts/icons.ts");
            Assert.True(i.Line != "" && i.Line.Length <= 70, $"intro {i.Lineage}'s line should be one short breath");
        }
        foreach (var s in C.Intros.Scenes)
            foreach (var p in s.Props)
                Assert.True(models.Contains(p.Model), $"scene {s.Id} names no baked model {p.Model}");
    }

    [Fact]
    public void MeetingsBringOneIntroALineage()
    {
        var sim = new Simulation(new SimulationOptions { Seed = 3 });
        var t = sim.Town;
        var menaces = t.Saga.Menaces.Where(m => m.Lineage != "").OrderBy(m => m.Id).ToList();
        if (menaces.Count == 0) return;
        int from = t.Discovered.Count;
        foreach (var m in menaces) Discovery.Meet(t, m, Discovery.Sighted);
        var due = Intros.Due(t, from);
        Assert.Equal(due.Select(d => d.Intro.Lineage).Distinct().Count(), due.Count);
        Assert.All(due, d => Assert.Equal(d.Intro.Lineage, Intros.LineageOf(t, d.Meeting.Key)));
        // Nothing new is due the second time round.
        Assert.Empty(Intros.Due(t, t.Discovered.Count));
    }

    private static Town TestTown() => new Simulation(new SimulationOptions { Seed = 1 }).Town;
}
