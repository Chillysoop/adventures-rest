using System.Text.Json;
using System.Text.RegularExpressions;

namespace CrownAndCompass.Sim;

/// <summary>
/// One keyword of the glossary (content/game/glossary.json). The client links every mention of
/// <see cref="Term"/> or an alias in its rich text to this entry: hovering shows the text, a click pins it.
/// </summary>
public sealed record GlossaryEntry
{
    /// <summary>Stable id, used in the client's links ("kw:mandate").</summary>
    public string Id { get; init; } = "";
    public string Term { get; init; } = "";
    public string Category { get; init; } = "";

    /// <summary>An icon in godot/assets/icons (without .svg) shown with the term wherever it is linked.</summary>
    public string Icon { get; init; } = "";

    /// <summary>Other words that mean this entry, matched like the term (plurals, synonyms).</summary>
    public List<string> Aliases { get; init; } = [];

    /// <summary>The explanation; [b]BBCode[/b] works, and other keywords in it are linked in turn.</summary>
    public string Text { get; init; } = "";
}

public sealed record GlossaryCategory
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
}

public sealed record GlossaryBook
{
    public List<GlossaryCategory> Categories { get; init; } = [];
    public List<GlossaryEntry> Entries { get; init; } = [];
}

/// <summary>
/// The glossary of keywords the client explains wherever they appear, and the rules that keep it whole:
/// every good and every menace kind has an entry with an icon, which <c>GlossaryTests</c> checks.
/// </summary>
public static class Glossary
{
    private static readonly Lazy<GlossaryBook> _default = new(() => Parse(Content.ReadText("glossary.json")));

    public static GlossaryBook Default() => _default.Value;

    public static GlossaryBook Parse(string json) => JsonSerializer.Deserialize<GlossaryBook>(json, Content.JsonOptions) ?? new GlossaryBook();

    /// <summary>Every word that links to an entry, lower-cased, longest first so "stone walls" wins over "stone".</summary>
    public static List<(string Word, GlossaryEntry Entry)> Words(GlossaryBook book) =>
        book.Entries
            .SelectMany(e => new[] { e.Term }.Concat(e.Aliases).Select(w => w.ToLowerInvariant()).Distinct().Select(w => (Word: w, Entry: e)))
            .OrderByDescending(p => p.Word.Length)
            .ThenBy(p => p.Word, StringComparer.Ordinal)
            .ToList();

    /// <summary>A pattern matching any keyword as a whole word, case-insensitively.</summary>
    public static Regex Pattern(GlossaryBook book) =>
        new(@"(?<![\w'-])(" + string.Join("|", Words(book).Select(p => Regex.Escape(p.Word))) + @")(?![\w-])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>What is wrong with a glossary: duplicate ids or words, missing text, unknown categories, missing icons.</summary>
    public static List<string> Problems(GlossaryBook book, Func<string, bool> iconExists)
    {
        var problems = new List<string>();
        var cats = book.Categories.Select(c => c.Id).ToHashSet();
        var ids = new HashSet<string>();
        var words = new Dictionary<string, string>();
        foreach (var e in book.Entries)
        {
            if (e.Id == "" || !ids.Add(e.Id)) problems.Add($"entry \"{e.Id}\" needs a unique id");
            if (e.Term == "" || e.Text == "") problems.Add($"entry \"{e.Id}\" needs a term and text");
            if (!cats.Contains(e.Category)) problems.Add($"entry \"{e.Id}\" is in an unknown category \"{e.Category}\"");
            if (e.Icon != "" && !iconExists(e.Icon)) problems.Add($"entry \"{e.Id}\" names icon \"{e.Icon}\", which is not in godot/assets/icons");
            foreach (var w in new[] { e.Term }.Concat(e.Aliases).Select(w => w.ToLowerInvariant()).Distinct())
                if (words.TryGetValue(w, out var other)) problems.Add($"\"{w}\" means both \"{other}\" and \"{e.Id}\": a word links to one entry");
                else words[w] = e.Id;
        }
        return problems;
    }
}
