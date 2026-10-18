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
    public static void Daily(Town town, bool plan = true, bool defend = true)
    {
        if (plan)
        {
            KeepZoned(town, Zone.Residential, 3, 7.5, 0);
            KeepZoned(town, Zone.Commercial, 2, 6.5, 0);
            KeepZoned(town, Zone.Industry, 2, 9.5, 5);
            KeepZoned(town, Zone.Farmland, 2, 99, 5.5);
            RaiseWorks(town, defend);
            PutIdleToWork(town);
        }
        CommissionHousing(town);
    }

    /// <param name="ring">Distance from the hall the zone prefers; 0 keeps it as near the hall as it can.</param>
    private static void KeepZoned(Town town, Zone zone, int minVacant, double maxDist, double ring)
    {
        int vacant = Planning.VacantZoned(town, zone);
        double Key(double d) => ring == 0 ? d : Math.Abs(d - ring);
        var picked = town.Lots
            .Where(l =>
            {
                double d = Town.DistanceFromCenter(l);
                return l.Zone == Zone.None && d <= maxDist && (ring == 0 || d >= ring - 0.5);
            })
            .OrderBy(l => Key(Town.DistanceFromCenter(l)))
            .ThenBy(l => l.Id)
            .Take(Math.Max(0, minVacant - vacant))
            .ToList();
        foreach (var l in picked) l.Zone = zone;
    }

    /// <summary>A granary once the town is a village, and a watch house once it keeps a watch.</summary>
    private static void RaiseWorks(Town town, bool defend)
    {
        if (town.Buildings.Any(b => b.IsSite && Planning.IsGovernment(town, b.Def))) return;
        foreach (var (id, pop, watch, spare) in new[] { ("granary", 25, 0, 150.0), ("watch_house", 30, 2, 200.0) })
        {
            var d = town.Content.Building(id);
            if ((id == "watch_house" && !defend) || town.Buildings.Any(b => b.Def.Id == id) || town.Population < pop || town.Watch < watch) continue;
            if (Planning.Blocked(town, d) != null || town.Treasury < Planning.Cost(town, d) + spare) continue;
            var lot = Planning.CivicLot(town);
            if (lot != null) Planning.Place(town, id, lot.Id);
            return;
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
        if (town.Day < 10 || idle < Math.Max(4, workers.Count / 4)) return;
        if (town.Buildings.Any(b => b.IsSite && b.Def.IsBusiness)) return;
        // A workshop, never a tavern: a town-run inn drains purses the townsfolk need for homes and
        // businesses of their own, and the town keeps to a few works so private trade can grow.
        int run = town.Buildings.Count(b => b.Def.IsBusiness && b.OwnerId == null && !b.ForSale);
        if (run >= 1 + workers.Count / 20) return;
        var best = town.Content.Buildings
            .Where(d => d.Buildable && d.IsBusiness && d.Produces.Count > 0)
            .Select(d => (d, s: Property.Demand(town, d.Id)))
            .OrderByDescending(x => x.s)
            .ThenBy(x => x.d.Id, StringComparer.Ordinal)
            .First();
        if (best.s < 1.0 || town.Treasury < Planning.Cost(town, best.d) + 60) return;
        var lot = Property.FindLot(town, best.d.Zone) ?? Property.FindLot(town, Zone.None);
        if (lot != null) Planning.Place(town, best.d.Id, lot.Id);
    }

    /// <summary>Build a charter house for the homeless, paid by the treasury. Returns whether one was started.</summary>
    public static bool CommissionHousing(Town town, int minHomeless = 5)
    {
        int homeless = town.Workers.Count(a => a.HomeId == null);
        bool building = town.Buildings.Any(b => b.IsSite && b.OwnerId == null && b.Def.Housing > 0);
        if (homeless < minHomeless || building) return false;

        var house = town.Content.Building("house");
        var lot = Property.FindLot(town, Zone.Residential);
        if (lot == null) return false;
        double cost = house.Cost + town.Market.Quote(Good.Timber, house.Timber);
        if (town.Treasury < cost + 50) return false;

        town.Book(TreasuryAccount.CharterHouses, -cost);
        town.Market.Buy(Good.Timber, house.Timber);
        var site = town.AddBuilding(house, lot, null, Property.NameFor(town, house, null), false);
        site.LaborBudget = house.BuildHours * Brain.ConstructionWage;
        town.Log(LedgerCategory.Town, Importance.Routine, $"With {homeless} people sleeping rough, the council commissions {site.Name}.");
        return true;
    }
}
