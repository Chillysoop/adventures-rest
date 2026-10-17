namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Stand-in for the player while the sim runs headless: zones land as it fills and commissions
/// charter houses when the homeless pile up. The real game replaces this with player input.
/// </summary>
public static class Mayor
{
    public static void Daily(Town town)
    {
        KeepZoned(town, Zone.Residential, 3, 7.5, true);
        KeepZoned(town, Zone.Commercial, 2, 6.5, true);
        KeepZoned(town, Zone.Farmland, 2, 99, false);
        CommissionHousing(town);
    }

    private static void KeepZoned(Town town, Zone zone, int minVacant, double maxDist, bool preferNear)
    {
        int vacant = town.Lots.Count(l => l.Zone == zone && l.IsVacant && l.OwnerId == null);
        double Key(double d) => preferNear ? d : Math.Abs(d - 5.5);
        var picked = town.Lots
            .Where(l =>
            {
                double d = Town.DistanceFromCenter(l);
                return l.Zone == Zone.None && d <= maxDist && (preferNear || d >= 5);
            })
            .OrderBy(l => Key(Town.DistanceFromCenter(l)))
            .ThenBy(l => l.Id)
            .Take(Math.Max(0, minVacant - vacant))
            .ToList();
        foreach (var l in picked) l.Zone = zone;
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
