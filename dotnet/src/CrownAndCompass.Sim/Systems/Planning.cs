namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// The player's hand on the town plan, after Foundation: zones are painted on vacant land and the
/// townsfolk and newcomers buy those lots and build what the zone allows (<see cref="Property"/>);
/// government buildings and town-owned businesses are placed directly and paid from the treasury.
/// </summary>
public static class Planning
{
    /// <summary>The zones a player paints. Civic land is made by placing a government building.</summary>
    public static readonly IReadOnlyList<Zone> Paintable = [Zone.Residential, Zone.Commercial, Zone.Industry, Zone.Farmland];

    // Zoning ----------------------------------------------------------------------------------

    /// <summary>A lot can be zoned (or cleared, with Zone.None) while nothing stands on it and nobody owns it.</summary>
    public static bool CanZone(Lot lot, Zone zone) =>
        lot.IsVacant && lot.OwnerId == null && lot.Zone != zone && (zone == Zone.None || Paintable.Contains(zone));

    /// <summary>Zone a stroke of lots. Returns how many changed.</summary>
    public static int Paint(Town town, IEnumerable<int> lotIds, Zone zone)
    {
        int n = 0;
        foreach (int id in lotIds.Distinct().Order())
        {
            if (id < 0 || id >= town.Lots.Count) continue;
            var lot = town.Lot(id);
            if (!CanZone(lot, zone)) continue;
            lot.Zone = zone;
            n++;
        }
        if (n > 0)
            town.Log(
                LedgerCategory.Property,
                Importance.Routine,
                zone == Zone.None
                    ? $"The council takes {Lots(n)} off the plan."
                    : $"The council zones {Lots(n)} as {ZoneName(zone)} land.");
        return n;
    }

    public static string ZoneName(Zone zone) => zone.ToString().ToLowerInvariant();

    private static string Lots(int n) => n == 1 ? "a lot" : $"{n} lots";

    /// <summary>A buyer found no land of this zone for sale: tell the player, at most once a week per zone.</summary>
    public static void NoLand(Town town, Zone zone, Agent a)
    {
        if (town.NoLandDay.TryGetValue(zone, out int day) && town.Day - day < 7) return;
        town.NoLandDay[zone] = town.Day;
        town.Log(LedgerCategory.Property, Importance.Notable, $"{a.Name} has gold for {ZoneName(zone)} land, but no {ZoneName(zone)} lot is zoned and for sale.", [a.Id]);
    }

    public static int VacantZoned(Town town, Zone zone) => town.Lots.Count(l => l.Zone == zone && l.IsVacant && l.OwnerId == null);

    // Government building --------------------------------------------------------------------

    /// <summary>What the player can place on a lot: government works, charter houses, and town-owned businesses.</summary>
    public static IEnumerable<BuildingDef> Placeable(Town town) =>
        town.Content.Buildings.Where(d => IsGovernment(town, d) || d.Id == "house" || (d.Buildable && d.IsBusiness));

    /// <summary>Built and run by the town, never by its townsfolk.</summary>
    public static bool IsGovernment(Town town, BuildingDef d) => d.Civic || d.Id == town.Content.Shrines.Temple || SightWork(town, d.Id) != null;

    private static SightWorkDef? SightWork(Town town, string id) => town.Content.Knowledge.Works.FirstOrDefault(w => w.Building == id);

    /// <summary>The zone a placed building takes: civic for government works, else its own.</summary>
    public static Zone ZoneFor(Town town, BuildingDef d) => IsGovernment(town, d) ? Zone.Civic : d.Zone;

    public static double Cost(Town town, BuildingDef d)
    {
        if (d.Id == town.Content.Shrines.Temple) return Shrines.TempleCost(town);
        if (SightWork(town, d.Id) is { } w) return Watch.WorkCost(town, w);
        return Js.Round(d.Cost + town.Market.Quote(Good.Timber, d.Timber) + (d.IsBusiness ? StartingCash : 0));
    }

    /// <summary>Working capital the treasury puts in a new town-owned business.</summary>
    public const double StartingCash = 30;

    /// <summary>Why the town can't raise this building anywhere yet, or null when it can.</summary>
    public static string? Blocked(Town town, BuildingDef d)
    {
        if (d.Id == town.Content.Shrines.Temple) return Shrines.TempleBlocked(town);
        if (SightWork(town, d.Id) is { } w) return Watch.WorkBlocked(town, w);
        if (!Placeable(town).Contains(d)) return "not the town's to build";
        int count = town.Buildings.Count(b => b.Def.Id == d.Id);
        if (d.Max > 0 && count >= d.Max) return $"the town has {d.Max}";
        if (town.Population < d.Population) return $"needs {d.Population} residents";
        if (town.Treasury < Cost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>Why this lot can't take the building, or null when it can.</summary>
    public static string? LotBlocked(Town town, Lot lot)
    {
        if (!lot.IsVacant) return "the lot is built on";
        if (lot.OwnerId != null) return "the lot is privately owned";
        return null;
    }

    /// <summary>Place a building on a lot, paid from the treasury; builders put it up like any other site.</summary>
    public static Building? Place(Town town, string buildingId, int lotId)
    {
        if (lotId < 0 || lotId >= town.Lots.Count) return null;
        var d = town.Content.Building(buildingId);
        var lot = town.Lot(lotId);
        if (Blocked(town, d) != null || LotBlocked(town, lot) != null) return null;
        if (d.Id == town.Content.Shrines.Temple) return Shrines.BuildTemple(town, lot);
        if (SightWork(town, d.Id) != null) return Watch.BuildWork(town, d.Id, lot);

        double cost = Cost(town, d);
        town.Book(d.Id == "house" ? TreasuryAccount.CharterHouses : TreasuryAccount.Works, -cost);
        town.Market.Buy(Good.Timber, d.Timber);
        lot.Zone = ZoneFor(town, d);
        string name = d.Civic ? CivicName(town, d) : Property.NameFor(town, d, null);
        var site = town.AddBuilding(d, lot, null, name, false);
        site.LaborBudget = d.BuildHours * Brain.ConstructionWage;
        if (d.IsBusiness) site.Cash = StartingCash;
        town.Log(
            LedgerCategory.Town,
            d.Civic || d.IsBusiness ? Importance.Notable : Importance.Routine,
            d.IsBusiness
                ? $"The council breaks ground on {site.Name}, to be run by the town for {Js.ToFixed(cost, 0)} gold."
                : $"The council breaks ground on {site.Name} for {Js.ToFixed(cost, 0)} gold.");
        return site;
    }

    private static string CivicName(Town town, BuildingDef d)
    {
        int n = town.Buildings.Count(b => b.Def.Id == d.Id);
        return n == 0 ? $"the {d.Name}" : $"the {d.Name} No. {n + 1}";
    }

    /// <summary>A vacant lot near the hall for a government work: civic land first, then any open lot.</summary>
    public static Lot? CivicLot(Town town)
    {
        var lot = Property.FindLot(town, Zone.Civic) ?? Property.FindLot(town, Zone.None);
        if (lot != null) lot.Zone = Zone.Civic;
        return lot;
    }

    // What government buildings do -------------------------------------------------------------

    private static IEnumerable<Building> Standing(Town town) => town.Buildings.Where(b => b.Complete && b.Def.Civic && b.OwnerId == null);

    /// <summary>How much each watchman counts, from the watch houses.</summary>
    public static double WatchFactor(Town town)
    {
        double f = 1;
        foreach (var b in Standing(town)) f += b.Def.WatchBoost;
        return f;
    }

    /// <summary>Extra food the granaries keep in town.</summary>
    public static double FoodStore(Town town)
    {
        double f = 0;
        foreach (var b in Standing(town)) f += b.Def.FoodStore;
        return f;
    }

    /// <summary>Daily running cost of the government buildings.</summary>
    public static double Upkeep(Town town)
    {
        double f = 0;
        foreach (var b in Standing(town)) f += b.Def.Upkeep;
        return f;
    }
}
