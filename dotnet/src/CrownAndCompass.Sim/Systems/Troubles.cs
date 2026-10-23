namespace CrownAndCompass.Sim.Systems;

/// <summary>What draws a company to a site that troubles the town (world.json companies.troubles).</summary>
public sealed record TroubleRules
{
    /// <summary>Added to a company's liking for a site: a camp of the final host, a lair that raided lately, a foe that hit a settlement lately.</summary>
    public double Host { get; init; } = 0.35;
    public double Raider { get; init; } = 0.25;
    public double Burner { get; init; } = 0.3;

    /// <summary>Days a raid or a blow on a settlement stays fresh in the taverns.</summary>
    public int RaidDays { get; init; } = 30;
    public int HarmDays { get; init; } = 60;

    /// <summary>Renown for the company and each member who fought there and came home; more if they cleared it.</summary>
    public int Fame { get; init; } = 4;
    public int ClearedFame { get; init; } = 4;

    /// <summary>Ledger lines by kind: {company}, {site}, {foe}, {settlement}.</summary>
    public string HostLine { get; init; } = "";
    public string RaiderLine { get; init; } = "";
    public string BurnerLine { get; init; } = "";
}

/// <summary>
/// The town's troubles draw free companies without a bounty: the final host's camps, a lair that has been raiding
/// the roads, the foe who fell on a neighbour. Heroes go where the taverns are angry, and the town honours them for
/// it when they come home. Reads never roll: the pull adds to <see cref="Campaigns.Choose"/>'s score.
/// </summary>
public static class Troubles
{
    public static TroubleRules Rules(Town town) => town.Content.World.Companies.Troubles;

    /// <summary>The trouble a site is to the town today, if any: "host", "raider" or "burner".</summary>
    public static string? Of(Town town, SiteState s)
    {
        var r = Rules(town);
        if (s.Cleared) return null;
        if (Horde.State(town).Camps.Any(c => c.SiteId == s.Def.Id && c.Strength > 0)) return "host";
        if (Menaces.At(town, s.Def.Id) is { HarmDay: int h } && town.Day - h <= r.HarmDays) return "burner";
        if (s.LastRaidDay is int d && town.Day - d <= r.RaidDays) return "raider";
        return null;
    }

    /// <summary>How much more a company likes a site for the trouble it is.</summary>
    public static double Pull(Town town, SiteState s) => Of(town, s) switch
    {
        "host" => Rules(town).Host,
        "burner" => Rules(town).Burner,
        "raider" => Rules(town).Raider,
        _ => 0,
    };

    /// <summary>A company home from a trouble it rode against: renown for having fought it, more for ending it.</summary>
    public static void Home(Town town, Party party, SiteState s, ExpeditionResult result, IReadOnlyList<Agent> survivors)
    {
        if (party.Trouble is not string kind || survivors.Count == 0 || result.Outcome == ExpeditionOutcome.Wiped || result.Fought.Count == 0) return;
        var r = Rules(town);
        bool cleared = result.Outcome == ExpeditionOutcome.Cleared;
        int fame = r.Fame + (cleared ? r.ClearedFame : 0);
        var menace = Menaces.At(town, s.Def.Id) ?? town.Saga.Menaces.FirstOrDefault(m => m.SeatId == s.Def.Id || m.Holdings.Contains(s.Def.Id));
        string foe = (kind == "host" ? Host(town) ?? menace : menace)?.Leader ?? "the raiders";
        string line = kind switch { "host" => r.HostLine, "burner" => r.BurnerLine, _ => r.RaiderLine };
        line = line.Replace("{company}", party.Name).Replace("{site}", s.Def.Name).Replace("{foe}", foe)
            .Replace("{settlement}", menace?.HarmedName is { Length: > 0 } h ? h : "a neighbour");
        foreach (var m in survivors) Notoriety.Deed(town, m, "trouble", fame);
        Notoriety.Record(town, party.Name, kind switch
        {
            "host" => $"struck {foe}'s host at {s.Def.Name}",
            "burner" => $"rode against {foe} at {s.Def.Name}",
            _ => $"went after the raiders at {s.Def.Name}",
        });
        Notoriety.CompanyDeed(town, party.Name, fame);
        if (line != "") town.Log(LedgerCategory.Quest, Importance.Notable, Saga.Cap(line) + $" (+{fame} renown)", survivors.Select(m => m.Id).ToList());
    }

    private static Menace? Host(Town town) => town.Saga.Menaces.FirstOrDefault(m => Horde.On(town, m));

    /// <summary>A menace has fallen on a settlement or the town: the taverns want it answered.</summary>
    public static void Harmed(Town town, Menace m, string where)
    {
        m.HarmDay = town.Day;
        m.HarmedName = where;
    }
}
