namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// What makes a story likelier in this town (a story's <see cref="StoryDef.Odds"/>): each reading of the town,
/// times its weight, adds to or takes from the story's chance of being the one that opens. A town full of rogues
/// draws heists, an open gate and a foe with a grudge draw strangers in the night, a strong watch keeps crime down.
/// Readings only look at the town; they never write or roll.
/// </summary>
public static class Odds
{
    /// <summary>
    /// Every plain reading, each in about one unit of its own: tier (charter tier, 0 for a thorp), heroes (per five in
    /// town), notables, guilds, refugees (per ten camped or settled), agents (enemy agents in town), moles (servants on
    /// the council), grudge (the hottest grudge over the heat it strikes at), unrest (over the trouble line), wealth
    /// (per thousand gold), crowded (yesterday's turned-away share), sick (per five sick houses), watch (per five
    /// watchmen), famous and infamous (heroes in town with a name). Besides these: class:id (heroes of the class in
    /// town), faction:id (the faction's heat over a hundred, while it has a chapter), gate:id (1 while that gate law
    /// holds), season:id (1 in that season) and zone:Zone (per ten finished buildings on that zone's land).
    /// </summary>
    public static readonly string[] Readings =
        ["tier", "heroes", "notables", "guilds", "refugees", "agents", "moles", "grudge", "unrest", "wealth", "crowded", "sick", "watch", "famous", "infamous"];

    /// <summary>Whether content may name this reading.</summary>
    public static bool Known(Content c, string reading)
    {
        if (Readings.Contains(reading)) return true;
        int at = reading.IndexOf(':');
        if (at < 0) return false;
        string id = reading[(at + 1)..];
        return reading[..at] switch
        {
            "class" => c.Classes.Any(x => x.Id == id),
            "faction" => c.Factions.Factions.Any(x => x.Ref == id),
            "gate" => c.Frontier.Gates.Any(g => g.Id == id),
            "season" => c.Seasons.Seasons.Any(s => s.Id == id),
            "zone" => Enum.TryParse<Zone>(id, out var z) && z != Zone.None,
            _ => false,
        };
    }

    /// <summary>One reading of the town as it stands.</summary>
    public static double Read(Town town, string reading)
    {
        switch (reading)
        {
            case "tier": return town.City.Tier;
            case "heroes": return town.Heroes.Count(a => !a.IsAway) / 5.0;
            case "notables": return Notables.Active(town).Count;
            case "guilds": return town.Guilds.Count;
            case "refugees": return town.Agents.Count(a => !a.Departed && a.Refugee) / 10.0;
            case "agents": return town.Lurkers.Count;
            case "moles": return town.Moles.Count;
            case "grudge":
                return town.Grudges.Count == 0 ? 0 : town.Grudges.Max(g => g.Heat) / Math.Max(1, town.Content.Crises.Vengeance.StrikeAt);
            case "unrest": return town.Taxes.Unrest / Math.Max(1, town.Content.Trends.Unrest);
            case "wealth": return Math.Max(0, town.Treasury) / 1000.0;
            case "crowded":
            {
                int came = 0, turned = 0;
                foreach (var b in town.Buildings)
                {
                    came += b.VisitsLast;
                    turned += b.TurnedAwayLast;
                }
                return came + turned == 0 ? 0 : (double)turned / (came + turned);
            }
            case "sick": return Sickness.Sick(town).Count / 5.0;
            case "watch": return town.Watch / 5.0;
            case "famous": return town.Heroes.Count(a => !a.IsAway && Notoriety.FameTier(town, a.Hero!) >= town.Content.Notoriety.StoryFame);
            case "infamous": return town.Heroes.Count(a => !a.IsAway && Notoriety.InfamyTier(town, a.Hero!) >= town.Content.Notoriety.StoryInfamy);
        }
        int at = reading.IndexOf(':');
        string kind = at < 0 ? reading : reading[..at], id = at < 0 ? "" : reading[(at + 1)..];
        return kind switch
        {
            "class" => town.Heroes.Count(a => !a.IsAway && a.Hero!.Cls.Id == id),
            "faction" => Factions.Find(town, id) is { Active: true } f ? f.Heat / 100.0 : 0,
            "gate" => Frontier.Gate(town).Id == id ? 1 : 0,
            "season" => Seasons.Current(town).Id == id ? 1 : 0,
            "zone" => ZoneBuilt(town, Enum.Parse<Zone>(id)) / 10.0,
            _ => throw new InvalidDataException($"Unknown story odds reading {reading}"),
        };
    }

    /// <summary>Finished buildings standing on land painted for a zone.</summary>
    public static int ZoneBuilt(Town town, Zone zone)
    {
        int n = 0;
        foreach (var b in town.Buildings)
            if (b.Complete && !b.Razed && town.Lots[b.LotId].Zone == zone) n++;
        return n;
    }

    /// <summary>How much likelier the town makes a story: the product of 1 + weight × reading over its odds, each at least nothing, and at most the cap.</summary>
    public static double Factor(Town town, StoryDef d)
    {
        if (d.Odds.Count == 0) return 1;
        double f = 1;
        foreach (var (reading, weight) in d.Odds.OrderBy(o => o.Key, StringComparer.Ordinal))
            f *= Math.Max(0, 1 + weight * Read(town, reading));
        return Math.Min(f, town.Content.Stories.OddsCap);
    }

    /// <summary>Whether the town's charter tier lets the story open: from its first tier, and before it has outgrown it.</summary>
    public static bool InTier(Town town, StoryDef d) =>
        (d.FromTier == "" || City.Reached(town, d.FromTier)) && (d.UntilTier == "" || town.City.Tier < City.TierIndex(town, d.UntilTier));
}
