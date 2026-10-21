namespace CrownAndCompass.Sim.Systems;

/// <summary>A crop a field can be sown with: the farm that grows it and, when it keeps its own calendar, its yield by season.</summary>
public sealed record CropDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The farm building that grows it (its goods are that building's).</summary>
    public string Building { get; init; } = "";

    /// <summary>The field's paint on the plan, as hex.</summary>
    public string Color { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>The part of the town's farms the stand-in mayor sows with it.</summary>
    public double Share { get; init; }

    /// <summary>Yield by season id, in place of the season's yield for the good; empty follows the season (seasons.json).</summary>
    public IReadOnlyDictionary<string, double> Seasons { get; init; } = new Dictionary<string, double>();
}

public sealed record FarmingRules
{
    /// <summary>Saved on the plan by place in this list (1 for the first, 0 for land that takes any crop): only ever add at the end.</summary>
    public IReadOnlyList<CropDef> Crops { get; init; } = [];
}

/// <summary>
/// Farmland is sown crop by crop (content/game/farming.json): the player picks a crop for each field with the zone
/// brush (<see cref="LandPlan.Crops"/>), and only that crop's farm goes up on it. Crops with their own calendar
/// replace the season's yield for their farms, and the town wants a crop more in the season it does better than the rest.
/// </summary>
public static class Farming
{
    public static IReadOnlyList<CropDef> Crops(Town town) => town.Content.Farming.Crops;

    /// <summary>A crop's number on the plan (0 for none or unknown).</summary>
    public static byte Code(Town town, string? id)
    {
        var crops = Crops(town);
        for (int i = 0; i < crops.Count; i++)
            if (crops[i].Id == id) return (byte)(i + 1);
        return 0;
    }

    /// <summary>The crop a plan number stands for.</summary>
    public static CropDef? Crop(Town town, byte code) => code > 0 && code <= Crops(town).Count ? Crops(town)[code - 1] : null;

    public static CropDef? Crop(Town town, string id) => Crop(town, Code(town, id));

    /// <summary>The crop a farm building grows, by plan number (0 when it grows none).</summary>
    public static byte CropOf(Town town, BuildingDef d)
    {
        if (d.Zone != Zone.Farmland) return 0;
        var crops = Crops(town);
        for (int i = 0; i < crops.Count; i++)
            if (crops[i].Building == d.Id) return (byte)(i + 1);
        return 0;
    }

    /// <summary>The crop sown on the cell under a point, or null when it isn't farmland sown with one.</summary>
    public static CropDef? At(Town town, double x, double y)
    {
        int cx = (int)Math.Floor(x * Land.Res), cy = (int)Math.Floor(y * Land.Res);
        if (cx < 0 || cy < 0 || cx >= Land.Size || cy >= Land.Size) return null;
        int i = cy * Land.Size + cx;
        return town.Plan.Zones[i] == (byte)Zone.Farmland ? Crop(town, town.Plan.Crops[i]) : null;
    }

    /// <summary>Why the town can't sow this crop yet (its farm's charter), or null when it can.</summary>
    public static string? Blocked(Town town, CropDef crop) => City.Locked(town, town.Content.Building(crop.Building));

    /// <summary>What a farm brings in of a good this season: its crop's own calendar, or the season's yield for the good.</summary>
    public static double Yield(Town town, Building b, Good good) => Yield(town, b.Def, good, Systems.Seasons.Current(town));

    public static double Yield(Town town, BuildingDef d, Good good, SeasonDef season)
    {
        if (d.Zone == Zone.Farmland && Crop(town, CropOf(town, d)) is { Seasons.Count: > 0 } crop && crop.Seasons.TryGetValue(season.Id, out double y)) return y;
        return season.YieldOf(good);
    }

    /// <summary>
    /// How much more a farm is worth sowing this season than the season's yield for its first good gives: root crops
    /// before the snow, orchards at harvest. 1 for crops that follow the season.
    /// </summary>
    public static double Signal(Town town, BuildingDef d)
    {
        if (d.Zone != Zone.Farmland || d.Produces.Count == 0 || Crop(town, CropOf(town, d)) is not { Seasons.Count: > 0 } crop) return 1;
        if (!crop.Seasons.TryGetValue(Systems.Seasons.Current(town).Id, out double own)) return 1;
        double season = Systems.Seasons.Yield(town, d.Produces[0].Key);
        return season <= 0 ? 1 : Math.Clamp(own / season, 0.8, 1.25);
    }

    /// <summary>The stand-in mayor's next field: the crop it can sow furthest below its share of the town's farms.</summary>
    public static CropDef? Next(Town town)
    {
        var farms = new Dictionary<string, int>();
        foreach (var b in town.Buildings)
            if (b.Def.Zone == Zone.Farmland && !b.Razed) farms[b.Def.Id] = farms.GetValueOrDefault(b.Def.Id) + 1;
        CropDef? best = null;
        double low = double.MaxValue;
        foreach (var crop in Crops(town))
        {
            if (crop.Share <= 0 || Blocked(town, crop) != null) continue;
            double k = (farms.GetValueOrDefault(crop.Building) + 1) / crop.Share;
            if (k < low - 1e-9)
            {
                low = k;
                best = crop;
            }
        }
        return best;
    }

    /// <summary>The crop the town wants sown most (its farm's demand), among those it can sow; null when it wants none.</summary>
    public static CropDef? Wanted(Town town)
    {
        CropDef? best = null;
        double top = 0;
        foreach (var crop in Crops(town))
        {
            if (Blocked(town, crop) != null) continue;
            double want = Property.Demand(town, crop.Building);
            if (want > top + 1e-9)
            {
                top = want;
                best = crop;
            }
        }
        return best;
    }
}
