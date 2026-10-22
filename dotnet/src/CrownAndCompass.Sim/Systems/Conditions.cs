namespace CrownAndCompass.Sim.Systems;

/// <summary>One condition: its SRD name, icon, and the advantage or disadvantage it puts on which rolls.</summary>
public sealed record ConditionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Srd { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>1 for advantage, -1 for disadvantage.</summary>
    public int Edge { get; init; }

    /// <summary>"checks", "saves".</summary>
    public List<string> On { get; init; } = [];
    public string Text { get; init; } = "";
}

/// <summary>Numbers for conditions (content/game/conditions.json).</summary>
public sealed record ConditionRules
{
    public double Fatigue { get; init; } = 85;
    public double Hunger { get; init; } = 90;

    /// <summary>Breaks (moods.json) that count as fear.</summary>
    public List<string> Fear { get; init; } = [];
    public List<ConditionDef> Conditions { get; init; } = [];
}

/// <summary>
/// Conditions after the SRD's, read off what someone is already going through (fatigue, hunger, raiders next door, a
/// break, a foe's charm, an inspiration), so they cost nothing to keep and nothing to save. Each puts advantage or
/// disadvantage on their checks or saves (<see cref="Skills"/>); the inspector shows them as icons.
/// </summary>
public static class Conditions
{
    public const string Checks = "checks";
    public const string Saves = "saves";

    public static ConditionRules Rules(Town town) => town.Content.Conditions;

    public static ConditionDef? Def(Town town, string id) => Rules(town).Conditions.FirstOrDefault(d => d.Id == id);

    /// <summary>Whether someone is under a condition now.</summary>
    public static bool Has(Town town, Agent a, string id)
    {
        if (a.Dead || a.Departed) return false;
        var r = Rules(town);
        return id switch
        {
            "exhausted" => a.Fatigue >= r.Fatigue || a.Hunger >= r.Hunger,
            "frightened" => Moods.Episode(town, a) is { } e && r.Fear.Contains(e.Effect) || Passage.Fleeing(town, a),
            "charmed" => Infiltration.Of(town, a.Id) is { Charmed: true },
            "inspired" => Moods.Episode(town, a) is { } e2 && Moods.IsInspiration(town, e2),
            _ => false,
        };
    }

    /// <summary>The conditions someone is under, in content order.</summary>
    public static List<ConditionDef> Of(Town town, Agent a) => Rules(town).Conditions.Where(d => Has(town, a, d.Id)).ToList();

    /// <summary>
    /// Advantage (1), disadvantage (-1) or neither on a roll of theirs ("checks" or "saves"), and the condition that gives it.
    /// One of each cancels out.
    /// </summary>
    public static (int Edge, string Why) Edge(Town town, Agent? a, string roll)
    {
        if (a == null) return (0, "");
        ConditionDef? up = null, down = null;
        foreach (var d in Rules(town).Conditions)
        {
            if (d.Edge == 0 || !d.On.Contains(roll) || (d.Edge > 0 ? up : down) != null || !Has(town, a, d.Id)) continue;
            if (d.Edge > 0) up = d;
            else down = d;
        }
        if (up != null && down != null) return (0, "");
        return up != null ? (1, up.Name) : down != null ? (-1, down.Name) : (0, "");
    }
}
