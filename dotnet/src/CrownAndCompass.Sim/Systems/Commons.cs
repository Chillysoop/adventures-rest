namespace CrownAndCompass.Sim.Systems;

/// <summary>What each neighbourhood costs to keep (city.json <c>commons</c>).</summary>
public sealed record CommonsRules
{
    /// <summary>Gold a day for a neighbourhood's watchman, whatever its size.</summary>
    public double Watch { get; init; }

    /// <summary>Gold a day per resident for wells, drains and lamps, times the land's density (low, medium, high).</summary>
    public double PerResident { get; init; }
    public List<double> Density { get; init; } = [1];

    /// <summary>Tiles from the centre over which a neighbourhood's bill doubles: pipes and lamps run further out there.</summary>
    public double Sprawl { get; init; } = 1000;

    /// <summary>Unpaid days to full neglect, and the mood its homes lose then.</summary>
    public double Days { get; init; } = 10;
    public double Dark { get; init; }

    /// <summary>Daily chance at full neglect that sickness breaks out in one of its homes.</summary>
    public double Sick { get; init; }

    /// <summary>Neglect at which the Ledger says its lamps have gone dark.</summary>
    public double Report { get; init; } = 3;
}

/// <summary>
/// The commons: every neighbourhood wants a watchman, wells, drains and lamps, and the bill grows with its people, the
/// density of its land and how far it lies from the centre, so a sprawling or packed city costs more to run. The treasury
/// pays the dearest streets first. A neighbourhood left unpaid goes dark and dirty: its homes sour, day by day, sickness
/// takes hold there, and the anger runs into the town's unrest through its homes' mood (<see cref="Taxes.Homes"/>).
/// </summary>
public static class Commons
{
    public static CommonsRules Rules(Town town) => town.Content.City.Commons;

    /// <summary>Each neighbourhood's bill today, the dearest ground first.</summary>
    public static List<(Quarter Quarter, double Bill)> Bills(Town town)
    {
        var r = Rules(town);
        var people = new Dictionary<int, double>();
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Housing <= 0 || b.Residents.Count == 0 || Neighbourhood.Of(town, b) is not { } q) continue;
            int level = Density.At(town, town.Lot(b.LotId));
            people[q.Id] = people.GetValueOrDefault(q.Id) + b.Residents.Count * r.Density[Math.Clamp(level, 0, r.Density.Count - 1)];
        }
        return town.City.Quarters
            .Select(q => (Quarter: q, Bill: (r.Watch + r.PerResident * people.GetValueOrDefault(q.Id)) * (1 + Js.Hypot(q.X - Town.Center, q.Y - Town.Center) / r.Sprawl)))
            .OrderByDescending(x => Neighbourhood.Land(town, x.Quarter.X, x.Quarter.Y)).ThenBy(x => x.Quarter.Id)
            .ToList();
    }

    /// <summary>All the neighbourhoods' bills together, for the treasury's books.</summary>
    public static double Bill(Town town) => Js.Sum(Bills(town), x => x.Bill);

    /// <summary>Pay the day's bills while the treasury lasts; a neighbourhood that goes without grows neglected.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (r.PerResident <= 0 && r.Watch <= 0) return;
        foreach (var (q, bill) in Bills(town))
        {
            double paid = Math.Min(bill, Math.Max(0, town.Treasury));
            if (paid > 0) town.Book(TreasuryAccount.Commons, -paid);
            bool unpaid = paid < bill - 1e-9;
            if (unpaid) town.Taxes.Unpaid += bill - paid;
            double was = q.Neglect;
            q.Neglect = Math.Clamp(q.Neglect + (unpaid ? 1 : -2), 0, r.Days);
            if (was < r.Report && q.Neglect >= r.Report)
                town.Log(LedgerCategory.Hardship, Importance.Notable, $"The lamps go dark in {q.Name}: its watchman and the well-diggers have gone unpaid for days, and the drains are backing up.");
            else if (was >= r.Report && q.Neglect < r.Report)
                town.Log(LedgerCategory.Town, Importance.Routine, $"The lamps are lit again in {q.Name}, and the drains run clear.");
            if (q.Neglect <= 0 || r.Sick <= 0) continue;
            var dice = new Rng(unchecked((int)Voices.Hash("commons", town.Seed, town.Day, q.Id)));
            if (!dice.Chance(r.Sick * q.Neglect / r.Days)) continue;
            var homes = town.Buildings.Where(b => b.Complete && b.Def.Housing > 0 && b.Residents.Count > 0 && Neighbourhood.Of(town, b) == q).ToList();
            if (homes.Count > 0) Sickness.Seed(town, homes[dice.Range(0, homes.Count)], $"the foul wells of {q.Name}");
        }
    }

    /// <summary>Mood a home's folk carry for the state of their neighbourhood's commons.</summary>
    public static double Mood(Town town, Quarter? q) => q == null || q.Neglect <= 0 ? 0 : Rules(town).Dark * q.Neglect / Rules(town).Days;
}
