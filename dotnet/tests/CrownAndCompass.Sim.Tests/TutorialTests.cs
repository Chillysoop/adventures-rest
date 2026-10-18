using System.Text.RegularExpressions;

namespace CrownAndCompass.Sim.Tests;

/// <summary>
/// The tutorial keeps up with the game: a new player command on <see cref="Simulation"/>, a new dock
/// window or a new hotkey in the Godot client fails here until content/game/tutorial.json teaches it.
/// </summary>
public class TutorialTests
{
    private static string Repo([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", ".."));

    private static string ClientSource() =>
        string.Join("\n", Directory.GetFiles(Path.Combine(Repo(), "godot", "src"), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(File.ReadAllText));

    /// <summary>The dock's windows: the client's Tab enum, less Inspect (the inspector opens by clicking).</summary>
    public static List<string> ClientTabs()
    {
        var m = Regex.Match(File.ReadAllText(Path.Combine(Repo(), "godot", "src", "Main.cs")), @"enum Tab\s*\{([^}]*)\}");
        return m.Groups[1].Value.Split(',').Select(s => s.Trim()).Where(s => s is not ("" or "Inspect")).ToList();
    }

    /// <summary>Every key the client reacts to (Key.Key0 only appears as arithmetic for the speed keys).</summary>
    public static List<string> ClientKeys() =>
        Regex.Matches(ClientSource(), @"(?<![\w.])Key\.(\w+)").Select(m => m.Groups[1].Value).Where(k => k != "Key0").Distinct().Order(StringComparer.Ordinal).ToList();

    [Fact]
    public void TeachesEveryPlayerFacingFeature()
    {
        var problems = Tutorial.Problems(Tutorial.Default(), ClientTabs(), ClientKeys());
        Assert.True(problems.Count == 0, "The tutorial is out of step with the game:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void FindsTheClientsFeatures()
    {
        Assert.Contains("Ledger", ClientTabs());
        Assert.Contains("Space", ClientKeys());
        Assert.Contains("PostBounty", Tutorial.Commands());
        Assert.DoesNotContain("Step", Tutorial.Commands());
    }

    [Fact]
    public void CatchesAnUntaughtFeature()
    {
        var book = Tutorial.Default() with { Steps = Tutorial.Default().Steps.Where(s => s.Id != "war").ToList() };
        var problems = Tutorial.Problems(book, ClientTabs(), ClientKeys());
        Assert.Contains(problems, p => p.StartsWith("tab:War "));
        Assert.Contains(problems, p => p.StartsWith("command:BuildDefense "));
    }

    [Fact]
    public void MeasuresAnyTownList()
    {
        var t = new Simulation(new SimulationOptions { Seed = 1 }).Town;
        Assert.Equal(0, Tutorial.Measure(t, "count:Bounties"));
        Assert.Equal(t.Agents.Count, Tutorial.Measure(t, "count:Agents"));
        Assert.Null(Tutorial.Measure(t, "count:Nonsense"));
        Assert.Null(Tutorial.Measure(t, "count:Name"));
    }
}
