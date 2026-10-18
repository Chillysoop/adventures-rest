using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace CrownAndCompass.Sim;

/// <summary>
/// One lesson of the tutorial (content/game/tutorial.json). The client shows its card, outlines what
/// <see cref="Point"/> names and moves on when the player does what <see cref="Until"/> asks.
/// </summary>
public sealed record TutorialStep
{
    /// <summary>Stable id: the client remembers which steps a player has seen by it.</summary>
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";

    /// <summary>The card's text; [b]bold[/b] and the client's other BBCode work.</summary>
    public string Text { get; init; } = "";

    /// <summary>What to outline on screen: a <see cref="Tutorial.Anchors"/> name or "tab:Ledger"; empty for nothing.</summary>
    public string Point { get; init; } = "";

    /// <summary>
    /// What finishes the step: "next" (the Next button), a client event (<see cref="Tutorial.Events"/>),
    /// "tab:Ledger" (that window opened), or a town measure (<see cref="Tutorial.Measure"/>) changing.
    /// </summary>
    public string Until { get; init; } = "next";

    /// <summary>
    /// Holds the step back until it is useful: "day:5", "alert:motion" (an alert whose key starts so is
    /// showing) or a town measure above zero. Empty shows it in order.
    /// </summary>
    public string When { get; init; } = "";

    /// <summary>The player-facing features this step teaches: "command:PostBounty", "tab:War", "key:Space".</summary>
    public List<string> Teaches { get; init; } = [];
}

public sealed record TutorialBook
{
    public List<TutorialStep> Steps { get; init; } = [];

    /// <summary>Player features deliberately left out of the tutorial, with why (for the coverage check).</summary>
    public Dictionary<string, string> Untaught { get; init; } = [];
}

/// <summary>
/// The tutorial's steps and the rules that keep it in step with the game: every player command, dock
/// window and hotkey must be taught by a step or listed as untaught, which <c>TutorialTests</c> checks.
/// </summary>
public static class Tutorial
{
    /// <summary>Things on screen a step can point at, besides "tab:&lt;Tab&gt;" for a dock button.</summary>
    public static readonly string[] Anchors = ["speeds", "topbar", "clock", "threat", "alerts", "feed", "build", "buildbar", "strategic", "minimap", "markers", "menu", "window", "inspector", "dock"];

    /// <summary>Things the player does in the client that can finish a step.</summary>
    public static readonly string[] Events = ["next", "speed", "pause", "rotate", "select", "select:agent", "select:building", "select:site", "build", "strategic", "menu", "save", "alert", "roll"];

    private static TutorialBook? _default;

    public static TutorialBook Default() => _default ??= Parse(Content.ReadText("tutorial.json"));

    public static TutorialBook Parse(string json) => JsonSerializer.Deserialize<TutorialBook>(json, Content.JsonOptions) ?? new TutorialBook();

    /// <summary>
    /// A number read off the town for steps to watch: "count:Bounties" is the size of that list on
    /// <see cref="Town"/> (any public list works, so a new system needs no code here); "zoned" is the painted
    /// ground not yet built on, "decorated" the decorations set down. Null for a name that means nothing.
    /// </summary>
    public static int? Measure(Town t, string token)
    {
        if (token == "zoned") return Systems.Land.PaintedCells(t);
        if (token == "decorated") return t.Decor.Pieces.Count;
        // The densest zoning the charter allows, and the cells zoned denser than low.
        if (token == "density") return Systems.Density.Unlocked(t);
        if (token == "dense") return t.Plan.Density.Count(d => d > 0);
        if (!token.StartsWith("count:", StringComparison.Ordinal)) return null;
        var prop = typeof(Town).GetProperty(token[6..], BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(t) is IEnumerable items and not string ? items.Cast<object>().Count() : null;
    }

    /// <summary>Whether a step's condition token is one the client understands.</summary>
    public static bool KnownUntil(string until, Town t, ICollection<string> tabs) =>
        Events.Contains(until) || (until.StartsWith("tab:", StringComparison.Ordinal) && tabs.Contains(until[4..])) || Measure(t, until) != null;

    public static bool KnownWhen(string when, Town t) =>
        when == "" || Measure(t, when) != null
        || (when.StartsWith("day:", StringComparison.Ordinal) && int.TryParse(when[4..], out _))
        || when.StartsWith("alert:", StringComparison.Ordinal);

    /// <summary>The player commands on <see cref="Simulation"/>: its public methods other than running the clock.</summary>
    public static IEnumerable<string> Commands() =>
        typeof(Simulation).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName && m.Name is not ("Step" or "RunDays" or "Snapshot"))
            .Select(m => m.Name)
            .Distinct()
            .OrderBy(n => n, StringComparer.Ordinal);

    /// <summary>
    /// Everything wrong with the tutorial against the game as built: features no step teaches, steps
    /// pointing at or waiting for things that don't exist. The client's dock tabs and hotkeys are passed in.
    /// </summary>
    public static List<string> Problems(TutorialBook book, ICollection<string> tabs, ICollection<string> keys)
    {
        var problems = new List<string>();
        var town = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        var taught = book.Steps.SelectMany(s => s.Teaches).Concat(book.Untaught.Keys).ToHashSet();
        var features = Commands().Select(c => $"command:{c}").Concat(tabs.Select(t => $"tab:{t}")).Concat(keys.Select(k => $"key:{k}")).ToList();
        foreach (var f in features.Where(f => !taught.Contains(f)))
            problems.Add($"{f} is a player-facing feature no tutorial step teaches: add a step to content/game/tutorial.json (or put it in a step's \"teaches\", or in \"untaught\" with a reason)");
        foreach (var f in taught.Where(f => !features.Contains(f)))
            problems.Add($"{f} is taught but no longer exists in the game: update content/game/tutorial.json");
        var ids = new HashSet<string>();
        foreach (var s in book.Steps)
        {
            if (s.Id == "" || !ids.Add(s.Id)) problems.Add($"step \"{s.Id}\" needs a unique id");
            if (s.Title == "" || s.Text == "") problems.Add($"step \"{s.Id}\" needs a title and text");
            if (s.Point != "" && !Anchors.Contains(s.Point) && !(s.Point.StartsWith("tab:", StringComparison.Ordinal) && tabs.Contains(s.Point[4..])))
                problems.Add($"step \"{s.Id}\" points at \"{s.Point}\", which is not on screen (anchors: {string.Join(", ", Anchors)}, or tab:<Tab>)");
            if (!KnownUntil(s.Until, town, tabs)) problems.Add($"step \"{s.Id}\" waits until \"{s.Until}\", which the client does not know");
            if (!KnownWhen(s.When, town)) problems.Add($"step \"{s.Id}\" has an unknown \"when\": \"{s.When}\"");
        }
        return problems;
    }
}
