using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Stand-in for the player while the sim runs headless: zones land as it fills and commissions
/// charter houses when the homeless pile up. The real game replaces this with player input.
/// </summary>
public static class Mayor
{
    public static void Daily(Town town)
    {
        KeepZoned(town, Zone.Residential, minVacant: 3, maxDist: 6.5, preferNear: true);
        KeepZoned(town, Zone.Commercial, minVacant: 2, maxDist: 4.5, preferNear: true);
        KeepZoned(town, Zone.Farmland, minVacant: 2, maxDist: 99, preferNear: false);
        CommissionHousing(town);
    }

    private static void KeepZoned(Town town, Zone zone, int minVacant, double maxDist, bool preferNear)
    {
        int vacant = town.Lots.Count(l => l.Zone == zone && l.IsVacant && l.OwnerId == null);
        var candidates = town.Lots
            .Where(l => l.Zone == Zone.None && Town.DistanceFromCenter(l) <= maxDist && (preferNear || Town.DistanceFromCenter(l) >= 5))
            .OrderBy(l => preferNear ? Town.DistanceFromCenter(l) : Math.Abs(Town.DistanceFromCenter(l) - 5.5))
            .ThenBy(l => l.Id)
            .Take(Math.Max(0, minVacant - vacant))
            .ToList();
        foreach (var l in candidates) l.Zone = zone;
    }

    private static void CommissionHousing(Town town)
    {
        int homeless = town.Residents.Count(a => a.HomeId == null);
        bool building = town.Buildings.Any(b => b.IsSite && b.OwnerId == null && b.Def.Housing > 0);
        if (homeless < 5 || building) return;

        var house = town.Content.Building("house");
        var lot = town.Lots
            .Where(l => l.Zone == Zone.Residential && l.IsVacant && l.OwnerId == null)
            .OrderBy(Town.DistanceFromCenter).ThenBy(l => l.Id)
            .FirstOrDefault();
        if (lot == null) return;
        double cost = house.Cost + town.Market.Quote(Good.Timber, house.Timber);
        if (town.Treasury < cost + 50) return;

        town.Treasury -= cost;
        town.Market.Buy(Good.Timber, house.Timber);
        var site = town.AddBuilding(house, lot, null, Property.NameFor(town, house, null), complete: false);
        site.LaborBudget = house.BuildHours * Brain.ConstructionWage;
        town.Log(LedgerCategory.Town, Importance.Routine,
            $"With {homeless} people sleeping rough, the council commissions {site.Name}.");
    }
}
