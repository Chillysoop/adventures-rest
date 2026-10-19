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

    /// <summary>Most a brush reaches, in tiles from the dab.</summary>
    public const double MaxBrush = 3;

    /// <summary>Paint a brush stroke (Zone.None clears land; civic land is made only by placing government buildings). Returns cells changed.</summary>
    /// <param name="crop">Farmland: the crop its fields are sown with (<see cref="Farming"/>); null or unknown sows wheat, the first.</param>
    public static int Paint(Town town, IEnumerable<(double X, double Y)> dabs, double radius, Zone zone, int density = 0, string? crop = null)
    {
        if (zone != Zone.None && !Paintable.Contains(zone)) return 0;
        if (!Density.Takes(zone)) density = Density.Low;
        if (Density.Blocked(town, density) != null) return 0;
        CropDef? sown = null;
        if (zone == Zone.Farmland)
        {
            sown = Farming.Crop(town, crop ?? "") ?? Farming.Crops(town).FirstOrDefault();
            if (sown != null && Farming.Blocked(town, sown) != null) return 0;
        }
        // A zone paints open ground only (and the density or crop of its own land), so a stroke never eats another zone: the eraser clears land to repaint it.
        int n = Land.Brush(town, dabs, Math.Clamp(radius, 0.25, MaxBrush), zone, overwrite: zone == Zone.None, density: density, crop: sown == null ? (byte)0 : Farming.Code(town, sown.Id));
        if (n > 0)
            town.Log(
                LedgerCategory.Property,
                Importance.Routine,
                zone == Zone.None
                    ? $"The council takes {Acres(n)} off the plan."
                    : sown != null
                        ? $"The council lays out {Acres(n)} of fields for {sown.Name.ToLowerInvariant()}."
                        : $"The council zones {Acres(n)} as {(density > Density.Low ? Density.Name(town, density).ToLowerInvariant() + "-density " : "")}{ZoneName(zone)} land.");
        return n;
    }

    public static string ZoneName(Zone zone) => zone.ToString().ToLowerInvariant();

    /// <summary>Painted area in words: a plan cell is a sixteenth of a tile, and a tile is about an acre.</summary>
    private static string Acres(int cells)
    {
        double acres = cells / (double)(Land.Res * Land.Res);
        return acres < 0.75 ? "a scrap of land" : acres < 1.5 ? "about an acre" : $"about {Js.ToFixed(acres, 0)} acres";
    }

    /// <summary>A buyer found no land of this zone for sale: tell the player, at most once a week per zone.</summary>
    public static void NoLand(Town town, Zone zone, Agent a)
    {
        if (town.NoLandDay.TryGetValue(zone, out int day) && town.Day - day < 7) return;
        town.NoLandDay[zone] = town.Day;
        town.Log(LedgerCategory.Property, Importance.Notable, $"{a.Name} has gold for {ZoneName(zone)} land, but no open {ZoneName(zone)} land is painted on the plan.", [a.Id]);
    }

    /// <summary>A would-be farmer found fields, but none sown with their crop: tell the player, at most once a week.</summary>
    public static void NoField(Town town, CropDef crop, Agent a)
    {
        if (town.NoLandDay.TryGetValue(Zone.Farmland, out int day) && town.Day - day < 7) return;
        town.NoLandDay[Zone.Farmland] = town.Day;
        town.Log(LedgerCategory.Property, Importance.Notable, $"{a.Name} would farm {crop.Name.ToLowerInvariant()}, but no open field on the plan is sown with it.", [a.Id]);
    }

    /// <summary>About how many more buildings the zone's open painted land holds.</summary>
    public static int VacantZoned(Town town, Zone zone) => Land.OpenPlots(town, zone);

    /// <summary>What the timber and stone for a building cost at today's market, caravans covering any shortfall.</summary>
    public static double Materials(Town town, BuildingDef d) => town.Market.Quote(Good.Timber, d.Timber) + town.Market.Quote(Good.Stone, d.Stone);

    /// <summary>Buy a building's timber and stone; returns what they cost.</summary>
    public static double BuyMaterials(Town town, BuildingDef d) => town.Market.Buy(Good.Timber, d.Timber) + town.Market.Buy(Good.Stone, d.Stone);

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
        if (d.IsGrand) return GrandWorks.StageCost(town, d, 0);
        return Js.Round(d.Cost + Materials(town, d) + (d.IsBusiness ? StartingCash : 0));
    }

    /// <summary>Working capital the treasury puts in a new town-owned business.</summary>
    public const double StartingCash = 30;

    /// <summary>Why the town can't raise this building anywhere yet, or null when it can.</summary>
    public static string? Blocked(Town town, BuildingDef d)
    {
        if (d.Id == town.Content.Shrines.Temple) return Shrines.TempleBlocked(town);
        if (SightWork(town, d.Id) is { } w) return Watch.WorkBlocked(town, w);
        if (!Placeable(town).Contains(d)) return "not the town's to build";
        if (City.Locked(town, d) is { } locked) return locked;
        int count = town.Buildings.Count(b => b.Def.Id == d.Id);
        if (d.Max > 0 && count >= d.Max) return $"the town has {d.Max}";
        if (town.Population < d.Population) return $"needs {d.Population} residents";
        if (town.Treasury < Cost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>Why the building can't stand at this point and heading (null faces the nearest lane), or null when it can (<see cref="Spot"/> may move it a little).</summary>
    public static string? SpotBlocked(Town town, BuildingDef d, double x, double y, double? yaw = null)
    {
        var ward = City.WardOf(town, x, y);
        const string wall = "the town wall runs there: its line is kept clear for the stone, towers and moat it will grow into";
        // The wall line sits just past a ring's edge, so a click on it would otherwise read as unopened land.
        if (City.OnWallStrip(town, x, y) && Spot(town, d, x, y, yaw) == null) return wall;
        if (!City.IsOpen(town, x, y)) return $"{City.The(ward)} has not been laid out";
        if (!City.AllowsWard(town, d, x, y)) return $"it can't stand in {City.The(ward)}";
        if (!Density.Allows(town, d, x, y)) return $"it needs land zoned {Density.Name(town, d.Density).ToLowerInvariant()} density";
        if (Spot(town, d, x, y, yaw) != null) return null;
        // Nothing near fits: say what's in the way at the spot itself.
        var (w, dd) = Land.Footprint(d);
        double a = yaw ?? Streets.Facing(town, x, y);
        var plan = town.Plan;
        var cells = Land.Cells(x, y, w, dd, a).ToList();
        if (cells.Any(i => i < 0)) return "too near the edge of the town's land";
        if (Land.Cells(x, y, w, dd, a, 1).Any(i => i >= 0 && Land.OnWallStrip(town, i))) return wall;
        if (Decor.Cells(town) is { } decor && cells.Any(i => decor[i])) return "decorations stand there: clear them first";
        if (cells.Any(i => plan.Taken[i] != 0)) return "another building stands there";
        if (cells.Any(i => plan.Paved[i] != 0)) return "that's a street";
        if (Land.Cells(x, y, w, dd, a, 1).Any(i => i >= 0 && plan.Taken[i] != 0)) return "too close to the next building";
        return Hills.Span(town, x, y, w, dd, a) > Hills.MaxSpan ? "the ground is too steep" : "no room here";
    }

    /// <summary>Where a building placed by hand at this point would actually go: there, or the nearest spot near it that fits (<see cref="Land.Near"/>).</summary>
    public static Lot? Spot(Town town, BuildingDef d, double x, double y, double? yaw = null) => Land.Near(town, d, x, y, yaw);

    /// <summary>Place a building by hand at a point and heading (null faces the nearest lane), paid from the treasury.</summary>
    public static Building? Place(Town town, string buildingId, double x, double y, double? yaw = null)
    {
        if (!town.Content.Buildings.Any(b => b.Id == buildingId)) return null;
        var lot = Spot(town, town.Content.Building(buildingId), x, y, yaw);
        return lot == null ? null : Place(town, buildingId, lot);
    }

    /// <summary>Place a building on a lot found for it, paid from the treasury; builders put it up like any other site.</summary>
    public static Building? Place(Town town, string buildingId, Lot lot)
    {
        var d = town.Content.Building(buildingId);
        if (Blocked(town, d) != null || lot.Id != town.Lots.Count) return null;
        if (d.Id == town.Content.Shrines.Temple) return Shrines.BuildTemple(town, lot);
        if (SightWork(town, d.Id) != null) return Watch.BuildWork(town, d.Id, lot);

        double cost = Cost(town, d);
        lot.Zone = ZoneFor(town, d);
        string name = d.Civic ? CivicName(town, d) : Property.NameFor(town, d, null);
        if (d.IsGrand)
        {
            var work = town.AddBuilding(d, lot, null, name, false);
            GrandWorks.Fund(town, work, TreasuryAccount.GrandWorks);
            town.Log(LedgerCategory.Town, Importance.Major, $"The council breaks ground on a grand work, {work.Name}, beginning with {d.Stages[0].Name}. It will rise in {d.Stages.Count} stages, each paid for as the last is done.");
            return work;
        }
        town.Book(d.Id == "house" ? TreasuryAccount.CharterHouses : TreasuryAccount.Works, -cost);
        BuyMaterials(town, d);
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

    /// <summary>A spot near the hall for a government work: on any unbuilt ground the players haven't zoned for the townsfolk.</summary>
    public static Lot? CivicLot(Town town, BuildingDef d)
    {
        var lot = Land.Find(town, d, null);
        if (lot != null) lot.Zone = Zone.Civic;
        return lot;
    }

    // What government buildings do -------------------------------------------------------------

    private static IEnumerable<Building> Standing(Town town) => town.Buildings.Where(b => b.Complete && b.Def.Civic && b.OwnerId == null);

    /// <summary>How much each watchman counts, from the watch houses.</summary>
    public static double WatchFactor(Town town)
    {
        double f = 1 + Decor.Watch(town);
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

    /// <summary>Mandate the townsfolk lend the mayor for the courthouse, the cathedral and the other great works.</summary>
    public static double Mandate(Town town)
    {
        double f = 0;
        foreach (var b in Standing(town)) f += b.Def.Mandate;
        return f;
    }

    /// <summary>How much stronger the walls are for the barracks and the keep.</summary>
    public static double Defense(Town town)
    {
        double f = 0;
        foreach (var b in Standing(town)) f += b.Def.Defense;
        return f;
    }

    /// <summary>Extra share of sales tax from the market halls.</summary>
    public static double SalesBoost(Town town)
    {
        double f = 0;
        foreach (var b in Standing(town)) f += b.Def.SalesBoost;
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
