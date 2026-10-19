namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// The glossary keeps up with the game: every entry is whole and unambiguous, and every good and every kind of
/// menace has an entry with an icon, so the client can show its icon wherever it is named.
/// </summary>
public class GlossaryTests
{
    private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));

    private static bool IconExists(string name) => File.Exists(Path.Combine(Repo(), "godot", "assets", "icons", name + ".svg"));

    [Fact]
    public void EveryEntryIsWhole()
    {
        var problems = Glossary.Problems(Glossary.Default(), IconExists);
        Assert.True(problems.Count == 0, "content/game/glossary.json has problems:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void EveryGoodHasAnIcon()
    {
        var book = Glossary.Default();
        foreach (var g in Goods.All)
        {
            var e = book.Entries.FirstOrDefault(x => x.Id == g.ToString().ToLowerInvariant());
            Assert.True(e is { Icon: not "" }, $"Good {g} needs a glossary entry \"{g.ToString().ToLowerInvariant()}\" with an icon (scripts/icons.ts)");
        }
    }

    [Fact]
    public void EveryMenaceKindHasAnIcon()
    {
        var book = Glossary.Default();
        foreach (var kind in Content.Default().Saga.Menaces.Kinds.Keys)
            Assert.True(book.Entries.Any(x => x.Id == kind && x.Icon != ""), $"Menace kind {kind} needs a glossary entry \"{kind}\" with an icon (scripts/icons.ts)");
    }

    [Fact]
    public void LinksWholeWordsLongestFirst()
    {
        var re = Glossary.Pattern(Glossary.Default());
        var hits = re.Matches("Raise stone walls with Stone; Mandate fell. Stonework is not a keyword.").Select(m => m.Value).ToList();
        Assert.Equal(["stone walls", "Stone", "Mandate"], hits);
    }
}
