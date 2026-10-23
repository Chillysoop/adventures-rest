namespace CrownAndCompass.Sim.Systems;

/// <summary>How the weather reaches the town's foes and its neighbours (weather.json "foes").</summary>
public sealed record WeatherFoes
{
    /// <summary>The step (2 foul, 3 storm) from which fliers keep to their roosts, and sky pirates stay off the Sky Dock.</summary>
    public int Grounded { get; init; } = 2;
    public int Pirates { get; init; } = 3;

    /// <summary>Hours a flying menace's strike waits out a storm over the town, a few at a time.</summary>
    public int FlierWait { get; init; } = 6;

    /// <summary>Share of the watch's strength that sees raiders coming out of fog.</summary>
    public double Fog { get; init; } = 0.7;

    /// <summary>Snow lying at a camp of the final host from which it stops gathering there, and the share of the camp lost a day.</summary>
    public double CampSnow { get; init; } = 0.45;
    public double CampLoss { get; init; } = 0.02;

    /// <summary>Ledger lines: {foe}, {camp}, {site}.</summary>
    public string FlierLine { get; init; } = "";
    public string PirateLine { get; init; } = "";
    public string CampLine { get; init; } = "";
    public string FogLine { get; init; } = "";
    public string MenaceFog { get; init; } = "";

    /// <summary>
    /// A neighbour's weather troubles come from the sky over it, never the dice: when a trouble is due, ground soaked past
    /// `wet` floods it, snow past `snow` shuts it in, and no rain (ground below `dry`) in heat past `dryHeat` parches it.
    /// </summary>
    public double Wet { get; init; } = 0.3;
    public double Snow { get; init; } = 0.9;
    public double Dry { get; init; } = 0.02;
    public double DryHeat { get; init; } = 28;
}

/// <summary>
/// The weather on the other side of the walls: storms ground fliers and keep sky pirates off, fog hides raiders from the
/// watch, deep snow thins the final host's camps, and the fronts bring floods, droughts and hard winters on the
/// neighbours. Reads only the weather (a pure function of the seed), so nothing here rolls.
/// </summary>
public static class Elements
{
    public static WeatherFoes Rules(Town town) => town.Content.Weather.Foes;

    /// <summary>Fliers stay on their roosts over a spot today.</summary>
    public static bool Grounded(Town town, double x, double y) => Weather.Worst(town, x, y, town.Day).Step >= Rules(town).Grounded;

    /// <summary>A flying menace whose strike on the town waits out the storm over it.</summary>
    public static bool Waits(Town town, Menace m)
    {
        if (Passage.Fliers(town, m.Kind) <= 0 || Weather.Here(town).Step < Weather.Storm) return false;
        m.ArrivalTick = town.Tick + Rules(town).FlierWait;
        if (m.WaitedDay != town.Day)
        {
            m.WaitedDay = town.Day;
            Log(town, Rules(town).FlierLine, ("foe", m.Leader), ("camp", ""), ("site", ""));
        }
        return true;
    }

    /// <summary>Sky pirates stay off the dock in a storm over the town.</summary>
    public static bool PiratesHeld(Town town)
    {
        if (Weather.Worst(town, 0, 0, town.Day).Step < Rules(town).Pirates) return false;
        Log(town, Rules(town).PirateLine, ("foe", ""), ("camp", ""), ("site", ""));
        return true;
    }

    /// <summary>The watch's strength against raiders in the night: less in fog.</summary>
    public static double Watch(Town town, out string why)
    {
        why = "";
        // They come in the small hours, before the dawn fog lifts.
        int dawn = (town.Day - 1) * SimTime.HoursPerDay + 5;
        if (!Weather.Here(town).Fog && !Weather.Here(town, dawn).Fog && !Weather.Here(town, dawn + 2).Fog) return 1;
        why = Rules(town).FogLine;
        return Rules(town).Fog;
    }

    /// <summary>A menace's raiders come for the town out of fog.</summary>
    public static void Fogged(Town town, Menace m, string place) => Log(town, Rules(town).MenaceFog, ("foe", m.Name), ("camp", place), ("site", place));

    /// <summary>Snow deep at a camp of the host: it stops gathering there and thins.</summary>
    public static bool Snowbound(Town town, Menace m, HordeCamp c)
    {
        var r = Rules(town);
        if (Quests.Site(town, c.SiteId) is not { } site) return false;
        var (x, y) = World.Spot(town, site);
        if (Weather.Ground(town, x, y, town.Day).Snow < r.CampSnow) return false;
        c.Strength *= 1 - r.CampLoss;
        if (c.ColdDay is not int d || town.Day - d > 30)
            Log(town, r.CampLine, ("foe", m.Leader), ("camp", site.Def.Name), ("site", site.Def.Name));
        c.ColdDay = town.Day;
        return true;
    }

    /// <summary>The weather trouble the sky brings on a settlement now, if its people suffer that kind: "wet", "snow" or "dry".</summary>
    public static string? Hardship(Town town, Settlement s)
    {
        var r = Rules(town);
        var (x, y) = Explore.SettlementSpot(town, s);
        var (snow, wet) = Weather.Ground(town, x, y, town.Day);
        if (snow >= r.Snow) return "snow";
        if (wet >= r.Wet) return "wet";
        if (wet <= r.Dry && Weather.At(town, x, y, (town.Day - 1) * SimTime.HoursPerDay + 14).Heat >= r.DryHeat) return "dry";
        return null;
    }

    private static void Log(Town town, string line, (string K, string V) a, (string K, string V) b, (string K, string V) c)
    {
        if (line == "") return;
        string text = line.Replace("{town}", town.Name);
        foreach (var (k, v) in new[] { a, b, c }) text = text.Replace("{" + k + "}", v);
        town.Log(LedgerCategory.War, Importance.Routine, Saga.Cap(text));
    }
}
