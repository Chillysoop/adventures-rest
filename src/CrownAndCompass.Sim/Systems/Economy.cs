using CrownAndCompass.Sim.Agents;
using CrownAndCompass.Sim.Core;
using CrownAndCompass.Sim.World;

namespace CrownAndCompass.Sim.Systems;

/// <summary>Production, wages, rent, the housing and job markets, and bankruptcy.</summary>
public static class Economy
{
    private const double ToolUsePerHour = 0.02;
    private const double ToolBonus = 0.3;
    private const double CashReserve = 40;

    public static void Hourly(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Closed || b.ForSale || b.Def.Produces.Count == 0) continue;
            foreach (var wid in b.Workers)
            {
                var w = town.Agent(wid);
                if (w.Activity == Activity.Working && w.LocationId == b.Id) Produce(town, b);
            }
        }
    }

    private static void Produce(Town town, Building b)
    {
        double rate = 1.0;
        if (b.StockOf(Good.Tools) >= ToolUsePerHour)
        {
            b.AddStock(Good.Tools, -ToolUsePerHour);
            rate += ToolBonus;
        }

        foreach (var (good, qty) in b.Def.Consumes)
        {
            double need = qty * rate;
            if (b.StockOf(good) < need)
            {
                double buy = Math.Ceiling(need * 10);
                b.Cash -= town.Market.Buy(good, buy);
                b.AddStock(good, buy);
            }
            b.AddStock(good, -need);
        }

        foreach (var (good, qty) in b.Def.Produces)
        {
            double revenue = town.Market.Sell(good, qty * rate);
            double tax = revenue * town.TaxRate;
            town.Treasury += tax;
            b.Cash += revenue - tax;
            b.RevenueToday += revenue;
        }
    }

    /// <summary>Daily accounts; with jobsAndHousingOnly, just re-runs matching (e.g. after arrivals).</summary>
    public static void Daily(Town town, bool jobsAndHousingOnly = false)
    {
        if (jobsAndHousingOnly)
        {
            HousingMarket(town);
            JobMarket(town);
            return;
        }
        UpdateMarket(town);
        Accounts(town);
        Rent(town);
        HousingMarket(town);
        JobMarket(town);
    }

    private static void UpdateMarket(Town town)
    {
        int pop = town.Population;
        var m = town.Market;
        m.SetTarget(Good.Food, pop * 3 + 40);
        m.SetTarget(Good.Timber, 40 + town.Buildings.Count(b => b.IsSite) * 10);
        m.SetTarget(Good.Tools, 10 + town.Buildings.Count(b => b.Def.IsBusiness));
        m.SetTarget(Good.Ale, pop * 0.6 + 10);
        m.DailyCaravan();
    }

    private static void Accounts(Town town)
    {
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || !b.Def.IsBusiness || b.ForSale) { b.RevenueToday = 0; continue; }

            // Keep a working stock of tools.
            if (b.Def.Produces.Count > 0 && b.StockOf(Good.Tools) < 0.5 && b.Cash > 30)
            {
                b.Cash -= town.Market.Buy(Good.Tools, 1);
                b.AddStock(Good.Tools, 1);
            }

            if (b.Cash < 0 && b.OwnerId is int oid)
            {
                var owner = town.Agent(oid);
                double cover = Math.Min(owner.Gold, -b.Cash);
                owner.Gold -= cover;
                b.Cash += cover;
            }
            else if (b.Cash < 0 && b.OwnerId == null)
            {
                double cover = Math.Min(town.Treasury, -b.Cash);
                town.Treasury -= cover;
                b.Cash += cover;
            }

            if (b.Cash < -0.01)
            {
                b.DaysInDebt++;
                if (b.DaysInDebt == 2)
                    town.Log(LedgerCategory.Business, Importance.Notable,
                        $"{b.Name} cannot meet its wages; {town.OwnerName(b.OwnerId)} is borrowing against next week.", OwnerIds(b));
                if (b.DaysInDebt >= 4) Bankrupt(town, b);
            }
            else
            {
                b.DaysInDebt = 0;
                if (b.Cash > CashReserve)
                {
                    double profit = b.Cash - CashReserve;
                    b.Cash = CashReserve;
                    if (b.OwnerId is int o) town.Agent(o).Gold += profit;
                    else town.Treasury += profit;
                }
            }
            b.RevenueToday = 0;
        }
    }

    private static int[] OwnerIds(Building b) => b.OwnerId is int o ? new[] { o } : Array.Empty<int>();

    private static void Bankrupt(Town town, Building b)
    {
        string owner = town.OwnerName(b.OwnerId);
        int[] ids = OwnerIds(b);
        if (b.OwnerId is int oid)
        {
            b.OwnerId = null;
            town.Lot(b.LotId).OwnerId = null;
        }
        b.Cash = 0;
        b.DaysInDebt = 0;
        PutUpForSale(town, b);
        town.Log(LedgerCategory.Business, Importance.Major,
            $"{b.Name} goes bankrupt. {owner} loses it to creditors and the town puts it up for sale.", ids);
    }

    public static void PutUpForSale(Town town, Building b)
    {
        if (!b.Complete) return;
        b.ForSale = true;
        foreach (var wid in b.Workers) town.Agent(wid).JobId = null;
        b.Workers.Clear();
    }

    public static double SalePrice(Town town, Building b) =>
        Math.Round(town.LotPrice(town.Lot(b.LotId)) + b.Def.Cost * 0.6, 0);

    private static void Rent(Town town)
    {
        foreach (var house in town.Buildings.Where(b => b.Complete && b.Def.Housing > 0))
        {
            foreach (var rid in house.Residents.ToList())
            {
                var r = town.Agent(rid);
                if (house.OwnerId == r.Id || (house.OwnerId is int o && r.SpouseId == o)) continue;
                if (r.Gold >= house.Def.Rent)
                {
                    r.RentArrears = 0;
                    r.Gold -= house.Def.Rent;
                    if (house.OwnerId is int landlord) town.Agent(landlord).Gold += house.Def.Rent;
                    else town.Treasury += house.Def.Rent;
                }
                else if (house.OwnerId is int landlord && town.Agent(landlord).Has(Trait.Generous))
                {
                    r.ChangeOpinion(landlord, 3);
                }
                else if (++r.RentArrears < 3)
                {
                    // A few days' grace before the bailiff comes.
                }
                else
                {
                    r.RentArrears = 0;
                    house.Residents.Remove(r.Id);
                    r.HomeId = null;
                    if (house.OwnerId is int ll) r.ChangeOpinion(ll, -25);
                    town.Log(LedgerCategory.Hardship, Importance.Notable,
                        $"{r.Name} cannot pay rent and is turned out of {house.Name} by {town.OwnerName(house.OwnerId)}.",
                        house.OwnerId is int l2 ? new[] { r.Id, l2 } : new[] { r.Id });
                }
            }
        }
    }

    public static void MoveIn(Town town, Agent a, Building home)
    {
        if (a.HomeId is int old) town.Building(old).Residents.Remove(a.Id);
        a.HomeId = home.Id;
        home.Residents.Add(a.Id);
    }

    private static void HousingMarket(Town town)
    {
        foreach (var a in town.Residents.Where(a => a.HomeId == null).ToList())
        {
            if (a.HomeId != null) continue;
            var spouse = a.SpouseId is int s ? town.Agent(s) : null;
            int beds = spouse != null && spouse.HomeId == null ? 2 : 1;
            if (spouse?.HomeId is int sh && town.Building(sh).OpenBeds > 0)
            {
                MoveIn(town, a, town.Building(sh));
                continue;
            }

            double purse = a.Gold + (spouse?.Gold ?? 0);
            var home = town.Buildings
                .Where(b => b.Complete && b.OpenBeds >= beds && (b.OwnerId == a.Id || purse >= b.Def.Rent * beds * 3))
                .OrderByDescending(b => b.OwnerId == a.Id ? 1000 : b.OwnerId is int o ? a.OpinionOf(o) : 0)
                .ThenBy(b => b.Id)
                .FirstOrDefault();
            if (home == null) continue;
            MoveIn(town, a, home);
            if (beds == 2) MoveIn(town, spouse!, home);
        }
    }

    private static void JobMarket(Town town)
    {
        // Laborers walk off sites that can no longer pay them.
        foreach (var site in town.Buildings.Where(b => b.IsSite && b.LaborBudget < Brain.ConstructionWage))
        {
            foreach (var wid in site.Workers.Where(w => w != site.OwnerId).ToList())
            {
                town.Agent(wid).JobId = null;
                site.Workers.Remove(wid);
            }
        }

        foreach (var a in town.Residents.Where(a => a.JobId == null))
        {
            // Owners staff their own businesses first.
            var own = town.Buildings.FirstOrDefault(b => b.OwnerId == a.Id && b.Complete && b.Def.IsBusiness && b.OpenJobs > 0);
            var job = own ?? town.Buildings
                .Where(b => b.OpenJobs > 0)
                .OrderByDescending(b => b.Def.Profession == a.Profession ? 1 : 0)
                .ThenByDescending(b => b.Def.Wage)
                .ThenBy(b => b.Id)
                .FirstOrDefault();

            job ??= town.Buildings
                .Where(b => b.IsSite && b.Workers.Count < 3 && (b.LaborBudget >= Brain.ConstructionWage || b.OwnerId == a.Id))
                .OrderBy(b => b.Id)
                .FirstOrDefault();
            if (job == null) continue;

            a.JobId = job.Id;
            job.Workers.Add(a.Id);
            if (a.DaysUnemployed >= 3)
                town.Log(LedgerCategory.Business, Importance.Routine,
                    $"After {a.DaysUnemployed} days idle, {a.Name} finds work at {job.Name}.", a.Id);
        }
    }

    public static void ReleaseWorkers(Town town, Building b)
    {
        foreach (var wid in b.Workers) town.Agent(wid).JobId = null;
        b.Workers.Clear();
    }
}
