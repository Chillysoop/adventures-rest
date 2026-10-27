namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Stand-in for the player while the sim runs headless: zones land as it fills, raises a granary
/// and a watch house as the town grows, and commissions charter houses when the homeless pile up.
/// The real game replaces the planning with player input (<see cref="Planning"/>).
/// </summary>
public static class Mayor
{
    /// <param name="plan">Zone land and raise government buildings; off when a player plans the town.</param>
    /// <param name="defend">Raise the watch house too; off when a player sees to the defenses.</param>
    /// <param name="paint">Zone new land too; off for a surveyor the player named, who builds only on the land the player painted.</param>
    public static void Daily(Town town, bool plan = true, bool defend = true, bool paint = true)
    {
        if (plan)
        {
            int scale = 1 + town.Population / 150;
            if (paint)
            {
                KeepZoned(town, Zone.Residential, 2 + scale, false);
                KeepZoned(town, Zone.Commercial, 1 + scale, false);
                KeepZoned(town, Zone.Industry, 1 + scale, true);
                KeepFields(town, 1 + scale);
            }
            RaiseYards(town);
            RaiseWorks(town, defend);
            RaiseInstitutions(town);
            RaiseServices(town);
            PutIdleToWork(town);
            Decor.StandIn(town, defend);
            Seasons.StandIn(town);
        }
        CommissionHousing(town);
    }

    /// <summary>Keep a few lots of each zone vacant: near the hall (or far out, for fields and workshops), homes clear of dear ground and smoke.</summary>
    /// <param name="crop">Fields only: the crop they are sown with (<see cref="Farming"/>).</param>
    /// <param name="extra">Patches to paint even when the zone has its open plots.</param>
    private static void KeepZoned(Town town, Zone zone, int minVacant, bool edge, byte crop = 0, int extra = 0)
    {
        if (Planning.VacantZoned(town, zone) >= minVacant && extra == 0) return;
        double reach = town.Lots.Count == 0 ? 0 : town.Lots.Max(Town.DistanceFromCenter);
        // A round patch at a time, as a player would paint it, until the zone has the open plots it wants:
        // fields and workshops out at the edge of the built town, homes and shops close in; and alongside a street, so it fills
        // from the street. Homes keep off dear ground, where working folk can't pay the rent, and away from the workshops' smoke.
        float[] field = [];
        double? Key(double x, double y, double d)
        {
            int i = (int)(y * Land.Res) * Land.Size + (int)(x * Land.Res);
            return (edge ? Math.Abs(d - (reach + 1)) : d)
                + (zone == Zone.Residential ? Math.Max(0, Neighbourhood.RentAt(town, i) - 1) * 20 + Math.Max(0, -Neighbourhood.MoodAt(town, i)) * 2 : 0)
                + Math.Abs(field[i] / Land.Res - 2) * 0.7;
        }
        for (int i = 0; i < 12 && (Planning.VacantZoned(town, zone) < minVacant || extra-- > 0); i++)
        {
            field = Streets.Field(town.Plan);
            if (!Land.PaintPatch(town, zone, zone == Zone.Farmland ? 2.4 : 2.0, Key, crop)) break;
        }
    }

    /// <summary>
    /// Fields as farms are wanted: a field for each farm the town wants that has none open (two a day at most, and not
    /// while plenty of fields lie open), then open fields kept each sown with the crop furthest below its share of the town's farms (farming.json).
    /// </summary>
    private static void KeepFields(Town town, int minVacant)
    {
        int sown = 0;
        foreach (var crop in Farming.Crops(town).Where(c => Farming.Blocked(town, c) == null).OrderByDescending(c => Property.Demand(town, c.Building)).ThenBy(c => c.Id, StringComparer.Ordinal))
        {
            if (sown >= 2 || Property.Demand(town, crop.Building) < 0.95 || Planning.VacantZoned(town, Zone.Farmland) > minVacant * 3 + 3) break;
            if (Land.Find(town, town.Content.Building(crop.Building), Zone.Farmland) != null) continue;
            KeepZoned(town, Zone.Farmland, minVacant, true, Farming.Code(town, crop.Id), 1);
            sown++;
        }
        KeepZoned(town, Zone.Farmland, minVacant, true, Farming.Code(town, Farming.Next(town)?.Id ?? "wheat"));
    }

    /// <summary>Another builders' yard when the town has outgrown its crews, so homes and shops don't queue for builders.</summary>
    private static void RaiseYards(Town town)
    {
        if (Construction.Crews(town) >= Construction.Wanted(town)) return;
        if (town.Buildings.Any(b => b.IsSite && b.Def.Crews > 0)) return;
        var d = town.Content.Building("builders_yard");
        if (Planning.Blocked(town, d) != null || town.Treasury < Planning.Cost(town, d) + 60) return;
        var lot = Planning.CivicLot(town, d);
        if (lot != null) Planning.Place(town, d.Id, lot);
    }

    /// <summary>A granary once the town is a village, a watch house once it keeps a watch, and an adventurers' hall once heroes come and go.</summary>
    private static void RaiseWorks(Town town, bool defend)
    {
        if (town.Buildings.Any(b => b.IsSite && Planning.IsGovernment(town, b.Def))) return;
        foreach (var (id, pop, watch, spare) in new[] { ("granary", 25, 0, 150.0), ("watch_house", 30, 2, 200.0), ("adventurers_hall", 60, 0, 300.0) })
        {
            var d = town.Content.Building(id);
            if ((id == "watch_house" && !defend) || town.Buildings.Any(b => b.Def.Id == id) || town.Population < pop || town.Watch < watch) continue;
            if (Planning.Blocked(town, d) != null || town.Treasury < Planning.Cost(town, d) + spare) continue;
            var lot = Planning.CivicLot(town, d);
            if (lot != null) Planning.Place(town, id, lot);
            return;
        }
    }

    /// <summary>The institutions the next charter asks for, and the walls, raised as the treasury allows (one at a time).</summary>
    private static void RaiseInstitutions(Town town)
    {
        var next = City.NextTier(town);
        if (next == null || town.Population < next.Residents * 0.7) return;
        if (next.Walls != "")
        {
            var work = next.Walls == "CityWall" ? DefenseWork.CityWall : DefenseWork.StoneWall;
            if (work == DefenseWork.CityWall && town.Defenses.StoneWall == PalisadeState.None) work = DefenseWork.StoneWall;
            if (town.Treasury >= Threat.BuildCost(town, work) + 250) WallLines.StandIn(town, WallLines.RingOf(work));
            if (Threat.BuildBlocked(town, work) == null && town.Treasury >= Threat.BuildCost(town, work) + 250) Threat.Build(town, work);
        }
        if (town.Buildings.Any(b => b.IsSite && (Planning.IsGovernment(town, b.Def) || (b.OwnerId == null && b.Def.IsBusiness)))) return;
        foreach (string id in next.Requires)
        {
            if (town.Buildings.Any(b => b.Def.Id == id && !b.ForSale)) continue;
            var d = town.Content.Building(id);
            if (id == town.Content.Shrines.Temple || id == "general_store" || id == "tavern") continue;
            if (Planning.Blocked(town, d) != null || town.Treasury < Planning.Cost(town, d) + 200) continue;
            var lot = d.IsBusiness && !d.Civic ? Property.FindLot(town, d) ?? Planning.CivicLot(town, d) : Planning.CivicLot(town, d);
            if (lot != null && Planning.Place(town, id, lot) != null) return;
        }
    }

    /// <summary>Another market hall or library when the town has outgrown the ones it has.</summary>
    private static void RaiseServices(Town town)
    {
        if (town.Buildings.Any(b => b.IsSite && Planning.IsGovernment(town, b.Def))) return;
        foreach (var d in town.Content.Buildings.Where(d => d.Civic && d.Serves.Count > 0).OrderBy(d => d.Cost).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            string service = d.Serves.Keys.Order(StringComparer.Ordinal).First();
            if (!town.Buildings.Any(b => b.Def.Id == d.Id) || City.CoverageOf(town, service) >= 0.9) continue;
            if (Planning.Blocked(town, d) != null || town.Treasury < Planning.Cost(town, d) + 500) continue;
            var lot = Planning.CivicLot(town, d);
            if (lot != null && Planning.Place(town, d.Id, lot) != null) return;
        }
    }

    /// <summary>
    /// When idle hands pile up and no one has the purse to open a business, the council opens one
    /// itself: the business the town most wants, run by the town. Keeps a poor town from starving
    /// with half its people out of work.
    /// </summary>
    private static void PutIdleToWork(Town town)
    {
        var workers = town.Workers;
        int idle = workers.Count(a => a.JobId == null);
        if (town.Day < 10 || idle < Math.Max(4, workers.Count / 10)) return;
        if (town.Buildings.Any(b => b.IsSite && b.Def.IsBusiness)) return;
        // A workshop, never a tavern: a town-run inn drains purses the townsfolk need for homes and
        // businesses of their own, and the town keeps to a few works so private trade can grow.
        int run = town.Buildings.Count(b => b.Def.IsBusiness && b.OwnerId == null && !b.ForSale);
        if (run >= 1 + workers.Count / 15) return;
        var best = town.Content.Buildings
            .Where(d => d.Buildable && d.IsBusiness && d.Produces.Count > 0 && City.Unlocked(town, d))
            .Select(d => (d, s: Property.Demand(town, d.Id)))
            .OrderByDescending(x => x.s)
            .ThenBy(x => x.d.Id, StringComparer.Ordinal)
            .First();
        if (best.s < 1.0 || town.Treasury < Planning.Cost(town, best.d) + 60) return;
        var lot = Property.FindLot(town, best.d) ?? Land.Find(town, best.d, null);
        if (lot != null) Planning.Place(town, best.d.Id, lot);
    }

    /// <summary>Build a charter house for the homeless, paid by the treasury. Returns whether one was started.</summary>
    public static bool CommissionHousing(Town town, int minHomeless = 5)
    {
        int homeless = town.Workers.Count(a => a.Roofless);
        // One house at a time, more while many sleep rough.
        int building = town.Buildings.Count(b => b.IsSite && b.OwnerId == null && b.Def.Housing > 0);
        if (homeless < minHomeless || building > homeless / 12 || !Construction.Room(town)) return false;

        var house = town.Content.Building("house");
        var lot = Land.FindCheapest(town, house, house.Zone);
        if (lot == null) return false;
        double cost = house.Cost + Planning.Materials(town, house);
        if (town.Treasury < cost + 50) return false;

        // Its timber and stone come off the market as hauled (Construction.Haul), like any council site.
        town.Book(TreasuryAccount.CharterHouses, -house.Cost);
        var site = town.AddBuilding(house, lot, null, Property.NameFor(town, house, null), false);
        site.Paid = house.Cost;
        site.Draws = true;
        site.LaborBudget = Construction.Hours(town, house) * Brain.ConstructionWage;
        town.Log(LedgerCategory.Town, Importance.Routine, $"With {homeless} people sleeping rough, the council commissions {site.Name}.");
        return true;
    }
}
