using System.Text.Json;
using System.Text.Json.Nodes;
using CrownAndCompass.Sim;
using CrownAndCompass.Sim.Systems;
using Godot;

namespace CrownAndCompass.Client;

/// <summary>
/// The player's profile (user://profile.json, next to the saves): what carries from one campaign to the
/// next. Each feature keeps its own section; "legends" holds the foes and heroes of earlier towns, who
/// may come back on the other side (<see cref="Turning"/>). Other sections are kept as they are.
/// </summary>
public partial class Main
{
    private static string ProfilePath => ProjectSettings.GlobalizePath("user://profile.json");

    private static JsonObject ReadProfile()
    {
        try
        {
            return File.Exists(ProfilePath) ? JsonNode.Parse(File.ReadAllText(ProfilePath)) as JsonObject ?? [] : [];
        }
        catch (Exception e) when (e is JsonException or IOException)
        {
            return [];
        }
    }

    private static List<Legend> ProfileLegends()
    {
        try
        {
            return ReadProfile()["legends"] is JsonNode n ? Legends.Read(n.ToJsonString()) : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Remember this campaign's legends, once it has gone on long enough to have any.</summary>
    private void RememberLegends()
    {
        // Only towns the player runs: the main menu's showcase and screenshot runs leave no legends.
        if (_sim == null || _sim.Options.AutoBounties || _opts.Screenshot != null) return;
        var t = _sim.Town;
        if (t.Outcome == null && t.Day < LegendDays) return;
        var fresh = Legends.Record(t, t.Seed);
        if (fresh.Count == 0) return;
        try
        {
            var profile = ReadProfile();
            profile["legends"] = JsonNode.Parse(Legends.Write(Legends.Merge(ProfileLegends(), fresh)));
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilePath)!);
            File.WriteAllText(ProfilePath, profile.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException e)
        {
            GD.PushWarning($"Could not write the profile: {e.Message}");
        }
    }

    /// <summary>A campaign is remembered from this day on, or when it ends.</summary>
    private const int LegendDays = 120;

    /// <summary>The history screen's list of the player's own legends.</summary>
    private static IEnumerable<string> LegendLines()
    {
        var legends = ProfileLegends();
        if (legends.Count == 0) yield break;
        yield return "[font_size=18][b]Legends of your other towns[/b][/font_size]";
        yield return Ui.C(Ui.Ink2, "Foes and heroes of your earlier campaigns. Now and then one comes back to a new town, on the other side.");
        yield return "";
        foreach (var l in Enumerable.Reverse(legends))
        {
            var color = l.Kind == Legend.Foe ? Ui.Bad : Ui.Gold;
            yield return $"{Ui.C(color, $"[b]{Ui.Esc(l.Name)}[/b]")}  {Ui.C(Ui.Ink2, $"[font_size=12]{Ui.Esc(l.Title)} · seed {l.Seed}, day {l.Day}[/font_size]")}\n{Ui.C(Ui.Ink2, Ui.Esc($"{l.Name} {l.Deed}."))}\n";
        }
    }
}
