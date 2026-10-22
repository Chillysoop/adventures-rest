namespace CrownAndCompass.Sim.Systems;

/// <summary>A kind of trap laid before the wall (content/game/traps.json).</summary>
public sealed record TrapDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";
    public double Cost { get; init; }
    public double Timber { get; init; }
    public double Stone { get; init; }

    /// <summary>Share of a wave's ground troops it takes.</summary>
    public double Cut { get; init; }

    /// <summary>Spent when it goes off, until refilled for <see cref="Refill"/> gold.</summary>
    public bool Once { get; init; }
    public double Refill { get; init; }

    /// <summary>Takes only the first wave at its section in a siege.</summary>
    public bool First { get; init; }

    /// <summary>Needs the gatehouse, and goes only at the gate.</summary>
    public bool Gatehouse { get; init; }

    /// <summary>The hover, and the chronicle's line as it goes off ({place}).</summary>
    public string Tip { get; init; } = "";
    public string Text { get; init; } = "";
}

public sealed record TrapRules
{
    public List<TrapDef> Kinds { get; init; } = [];
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>A trap laid before a section of the wall (0 is the gate).</summary>
public sealed class Trap
{
    public int Id { get; set; }
    public string Kind { get; set; } = "";
    public int Section { get; set; }

    /// <summary>A once-only trap that went off and wants refilling.</summary>
    public bool Spent { get; set; }

    /// <summary>The siege it last went off in (<see cref="Battle.Id"/>), for traps that take only the first wave.</summary>
    public int LastBattle { get; set; }
}

public sealed class TrapState
{
    public List<Trap> Laid { get; } = [];
    public int NextId { get; set; } = 1;

    /// <summary>Foes the traps have taken, all told.</summary>
    public double Taken { get; set; }
}

/// <summary>
/// Wall traps, after Stronghold's killing grounds: pitch ditches, caltrops, wolf pits and murder holes laid before a
/// section of the drawn wall. In a siege each wave that comes at a section loses a share of its ground troops to every
/// ready trap there (fliers pass over), and the chronicle and the map's playback show them going off. Pitch burns once
/// and wants refilling. The stand-in mayor never lays traps, so they are the player's own edge. No dice: a trap that is
/// ready always goes off.
/// </summary>
public static class Traps
{
    public static TrapRules Rules(Town town) => town.Content.Traps;

    public static TrapDef? Def(Town town, string id) => Rules(town).Kinds.FirstOrDefault(k => k.Id == id);

    private static string Text(Town town, string key) => Rules(town).Texts.TryGetValue(key, out var t) ? t : "";

    public static List<Trap> At(Town town, int section) => town.Traps.Laid.Where(t => t.Section == section).ToList();

    public static List<Trap> Spent(Town town) => town.Traps.Laid.Where(t => t.Spent).ToList();

    /// <summary>Gold, timber and stone at market prices, to lay one.</summary>
    public static double Cost(Town town, TrapDef d) =>
        Js.Round(d.Cost + town.Market.Quote(Good.Timber, d.Timber) + town.Market.Quote(Good.Stone, d.Stone));

    /// <summary>Why this trap can't be laid at this section now, or null when it can.</summary>
    public static string? Blocked(Town town, string kind, int section)
    {
        if (Def(town, kind) is not { } d) return "no such trap";
        if (section < 0 || section >= Garrison.Sections(town)) return "no such wall";
        if (!Garrison.Walled(town)) return "needs a wall";
        if (d.Gatehouse && town.Defenses.Gatehouse != PalisadeState.Built) return "needs the gatehouse";
        if (d.Gatehouse && section != 0) return "only at the gate";
        if (town.Traps.Laid.Any(t => t.Section == section && t.Kind == kind)) return "laid already";
        if (town.Treasury < Cost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>Lay a trap before a section of the wall. Returns why not, or null when laid.</summary>
    public static string? Lay(Town town, string kind, int section)
    {
        if (Blocked(town, kind, section) is { } why) return why;
        var d = Def(town, kind)!;
        town.Book(TreasuryAccount.Defenses, -Cost(town, d));
        if (d.Timber > 0) town.Market.Buy(Good.Timber, d.Timber);
        if (d.Stone > 0) town.Market.Buy(Good.Stone, d.Stone);
        town.Traps.Laid.Add(new Trap { Id = town.Traps.NextId++, Kind = kind, Section = section });
        town.Log(LedgerCategory.War, Importance.Routine, Fill(Text(town, "laid"), town, d, section));
        return null;
    }

    /// <summary>Take a trap up again (nothing back). Returns false when there is no such trap.</summary>
    public static bool Remove(Town town, int id)
    {
        var t = town.Traps.Laid.FirstOrDefault(x => x.Id == id);
        if (t == null || Def(town, t.Kind) is not { } d) return false;
        town.Traps.Laid.Remove(t);
        town.Log(LedgerCategory.War, Importance.Routine, Fill(Text(town, "removed"), town, d, t.Section));
        return true;
    }

    /// <summary>What refilling every spent trap costs.</summary>
    public static double RefillCost(Town town) => Js.Round(Spent(town).Sum(t => Def(town, t.Kind)?.Refill ?? 0));

    /// <summary>Refill every spent trap. Returns why not, or null when done.</summary>
    public static string? Refill(Town town)
    {
        var spent = Spent(town);
        if (spent.Count == 0) return "nothing spent";
        double cost = RefillCost(town);
        if (town.Treasury < cost) return "not enough gold";
        town.Book(TreasuryAccount.Defenses, -cost);
        foreach (var t in spent) t.Spent = false;
        town.Log(LedgerCategory.War, Importance.Routine, Text(town, "refilled").Replace("{gold}", Js.Str(cost)));
        return null;
    }

    /// <summary>
    /// A siege wave comes at a section: every ready trap there takes its share of the wave's ground troops. Returns what
    /// is left of the wave. The first time each goes off in a siege the chronicle says so.
    /// </summary>
    public static double Spring(Town town, string menaceKind, double wave, int section, List<string> lines, Battle? record)
    {
        if (town.Traps.Laid.Count == 0 || wave <= 0) return wave;
        double ground = 1 - Passage.Fliers(town, menaceKind);
        if (ground <= 0) return wave;
        int battle = record?.Id ?? -town.Tick - 1;
        foreach (var t in town.Traps.Laid.Where(t => t.Section == section).OrderBy(t => t.Id).ToList())
        {
            if (t.Spent || Def(town, t.Kind) is not { } d) continue;
            if (d.First && t.LastBattle == battle) continue;
            double taken = wave * ground * d.Cut;
            wave -= taken;
            town.Traps.Taken += taken;
            if (t.LastBattle != battle) lines.Add(Saga.Cap(d.Text.Replace("{place}", Garrison.Place(town, section))));
            t.LastBattle = battle;
            if (d.Once) t.Spent = true;
            if (record != null && !record.Traps.Contains(t.Id)) record.Traps.Add(t.Id);
        }
        return Math.Max(0, wave);
    }

    private static string Fill(string text, Town town, TrapDef d, int section) =>
        text.Replace("{trap}", d.Name).Replace("{place}", Garrison.Place(town, section));
}
