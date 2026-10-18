namespace CrownAndCompass.Sim.Systems;

/// <summary>Production, wages, rent, the housing and job markets, and bankruptcy.</summary>
public static class Economy
{
    private const double ToolUsePerHour = 0.02;
    private const double ToolBonus = 0.3;
    private const double CashReserve = 40;

    public static void Hourly(Town town)
    {
        bool workHours = SimTime.IsWorkHours(town.Hour);
        double? salesTax = null;
        foreach (var b in town.Buildings)
        {
            // A site nobody in town is working on hires a day labourer off the road, paid from its budget.
            // The last few hours of a site get finished on credit if its budget has run dry.
            if (workHours && b.IsSite && b.Workers.Count == 0 && (b.LaborBudget >= Brain.ConstructionWage || b.Def.BuildHours - b.BuildProgress <= 4))
            {
                b.Raise(Seasons.Build(town) * Decrees.Build(town));
                b.LaborBudget = Math.Max(0, b.LaborBudget - Brain.ConstructionWage);
                continue;
            }
            if (!b.Complete || b.Closed || b.ForSale || b.Def.Produces.Count == 0) continue;
            foreach (int wid in b.Workers)
            {
                var w = town.Agent(wid);
                if (w.Activity == Activity.Working && w.LocationId == b.Id) Produce(town, b, salesTax ??= town.TaxRate + Planning.SalesBoost(town));
            }
        }
    }

    private static void Produce(Town town, Building b, double salesTax)
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

        double yield = Decor.Yield(town, b);
        foreach (var (good, qty) in b.Def.Produces)
        {
            double revenue = town.Market.Sell(good, qty * rate * yield * Seasons.Yield(town, good) * Decrees.Yield(town, good));
            double tax = revenue * salesTax;
            town.Book(TreasuryAccount.SalesTax, tax);
            b.Cash += revenue - tax;
            b.RevenueToday += revenue;
        }
    }

    /// <summary>Daily accounts; with jobsAndHousingOnly, just re-runs matching (e.g. after arrivals).</summary>
    public static void Daily(Town town, bool jobsAndHousingOnly = false)
    {
        if (!jobsAndHousingOnly)
        {
            UpdateMarket(town);
            Accounts(town);
            Rent(town);
            Upkeep(town);
        }
        HousingMarket(town);
        if (!jobsAndHousingOnly) Crews(town);
        JobMarket(town);
    }

    /// <summary>
    /// The cost of running a town: the watch, roads and wells grow with it, so growth isn't free money.
    /// Part of it pays road crews hired from the idle (<see cref="Crews"/>); their wages, already paid
    /// by the hour, come out of the bill rather than on top of it.
    /// </summary>
    private static void Upkeep(Town town)
    {
        double bill = UpkeepBill(town);
        var hall = Hall(town);
        if (hall != null && hall.Cash < 0)
        {
            bill = Math.Max(bill, -hall.Cash);
            hall.Cash = 0;
        }
        double paid = Math.Min(bill, Math.Max(0, town.Treasury));
        town.Book(TreasuryAccount.Upkeep, -paid);
        if (paid < bill && town.UnpaidDays++ == 0)
            town.Log(LedgerCategory.Hardship, Importance.Notable, "The treasury runs dry. The watch and the road crews go unpaid.");
        else if (paid >= bill) town.UnpaidDays = 0;
    }

    public static double UpkeepBill(Town town)
    {
        var rules = town.Content.Rules;
        int n = town.Workers.Count;
        return rules.CivicUpkeep + PerResidentUpkeep(town, n) + Planning.Upkeep(town);
    }

    private static double PerResidentUpkeep(Town town, int n)
    {
        var rules = town.Content.Rules;
        return rules.UpkeepPerResident * n * (1 + Math.Min(n, rules.UpkeepGrowth * 1.5) / rules.UpkeepGrowth);
    }

    public static Building? Hall(Town town) => town.Buildings.FirstOrDefault(b => b.Def.Id == "town_hall");

    /// <summary>
    /// The town hall hires idle hands onto road crews, as many as the roads-and-wells share of the
    /// upkeep pays for, so that money goes round the town instead of out of it. Crewmen leave for any
    /// trade that will have them.
    /// </summary>
    private static void Crews(Town town)
    {
        var hall = Hall(town);
        if (hall == null) return;
        var rules = town.Content.Rules;
        int idle = town.Workers.Count(a => a.JobId == null);
        int afford = (int)(PerResidentUpkeep(town, town.Workers.Count) * rules.CrewShare / (rules.CrewWage * 8));
        if (town.Treasury <= 0) afford = 0;
        hall.Crew = Math.Max(0, Math.Min(afford, hall.Workers.Count + idle));
        // Crews shrink by the last hired; the rest look for a trade.
        while (hall.Workers.Count > hall.Crew)
        {
            int id = hall.Workers[^1];
            hall.Workers.RemoveAt(hall.Workers.Count - 1);
            town.Agent(id).JobId = null;
        }
        foreach (int id in hall.Workers.ToList())
        {
            var job = town.Buildings
                .Where(b => b.Id != hall.Id && b.OpenJobs > 0)
                .OrderByDescending(b => b.Def.Wage)
                .ThenBy(b => b.Id)
                .FirstOrDefault()
                ?? town.Buildings.FirstOrDefault(b => b.IsSite && b.Workers.Count < 3 && b.LaborBudget >= Brain.ConstructionWage);
            if (job == null) break;
            Population.Remove(hall.Workers, id);
            town.Agent(id).JobId = job.Id;
            job.Workers.Add(id);
        }
    }

    private static void UpdateMarket(Town town)
    {
        int pop = town.Population;
        var m = town.Market;
        var sites = town.Buildings.Where(b => b.IsSite).ToList();
        m.SetTarget(Good.Food, pop * 3 + 40 + Planning.FoodStore(town));
        m.SetTarget(Good.Timber, 40 + sites.Count * 10);
        m.SetTarget(Good.Tools, 10 + town.BusinessCount());
        m.SetTarget(Good.Ale, pop * 0.6 + 10);
        // Everything else: a few days of what the households want and the workshops burn, and what the builders need.
        var use = new OrderedMap<Good, double>();
        foreach (var (g, q) in town.City.Wanted) use[g] = use.Get(g, 0) + q;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Closed || b.ForSale) continue;
            foreach (var (g, q) in b.Def.Consumes) use[g] = use.Get(g, 0) + q * b.Workers.Count * 8;
        }
        double stone = 0;
        foreach (var b in sites) stone += b.Def.Stone;
        foreach (var g in Goods.All)
        {
            if (g is Good.Food or Good.Timber or Good.Tools or Good.Ale) continue;
            m.SetTarget(g, 10 + use.Get(g, 0) * 3 + (g == Good.Stone ? 20 + stone : 0));
        }
        m.DailyCaravan();
    }

    private static int[] OwnerIds(Building b) => b.OwnerId != null ? [b.OwnerId.Value] : [];

    private static void Accounts(Town town)
    {
        foreach (var b in town.Buildings)
        {
            // A bastion's buildings settle with the company's chest (Bastions.Daily).
            if (!b.Complete || !b.Def.IsBusiness || b.ForSale || b.Def.Bastion)
            {
                b.RevenueToday = 0;
                continue;
            }
            if (b.Def.Holy)
            {
                // A house of the gods keeps its coffers; when they run dry the town pays its priests.
                if (b.Cash < 0)
                {
                    double cover = Math.Min(Math.Max(0, town.Treasury), -b.Cash);
                    town.Book(TreasuryAccount.Temple, -cover);
                    b.Cash += cover;
                }
                b.RevenueToday = 0;
                continue;
            }

            // Keep a working stock of tools.
            if (b.Def.Produces.Count > 0 && b.StockOf(Good.Tools) < 0.5 && b.Cash > 30)
            {
                b.Cash -= town.Market.Buy(Good.Tools, 1);
                b.AddStock(Good.Tools, 1);
            }

            if (b.Cash < 0 && b.OwnerId != null)
            {
                var owner = town.Agent(b.OwnerId.Value);
                double cover = Math.Min(owner.Gold, -b.Cash);
                owner.Gold -= cover;
                b.Cash += cover;
            }
            else if (b.Cash < 0 && b.OwnerId == null)
            {
                double cover = Math.Min(town.Treasury, -b.Cash);
                town.Book(TreasuryAccount.Bailouts, -cover);
                b.Cash += cover;
            }

            if (b.Cash < -0.01)
            {
                b.DaysInDebt++;
                if (b.DaysInDebt == 2)
                    town.Log(
                        LedgerCategory.Business,
                        Importance.Notable,
                        $"{b.Name} cannot meet its wages; {town.OwnerName(b.OwnerId)} is borrowing against next week.",
                        OwnerIds(b));
                if (b.DaysInDebt >= 4) Bankrupt(town, b);
            }
            else
            {
                b.DaysInDebt = 0;
                if (b.Cash > CashReserve)
                {
                    double profit = b.Cash - CashReserve;
                    b.Cash = CashReserve;
                    if (b.OwnerId != null) town.Agent(b.OwnerId.Value).Gold += profit;
                    else town.Book(TreasuryAccount.TownBusinesses, profit);
                }
            }
            b.RevenueToday = 0;
        }
    }

    private static void Bankrupt(Town town, Building b)
    {
        string owner = town.OwnerName(b.OwnerId);
        var ids = OwnerIds(b);
        if (b.OwnerId != null)
        {
            b.OwnerId = null;
            town.Lot(b.LotId).OwnerId = null;
        }
        b.Cash = 0;
        b.DaysInDebt = 0;
        PutUpForSale(town, b);
        town.Log(LedgerCategory.Business, Importance.Major, $"{b.Name} goes bankrupt. {owner} loses it to creditors and the town puts it up for sale.", ids);
    }

    public static void PutUpForSale(Town town, Building b)
    {
        if (!b.Complete) return;
        b.ForSale = true;
        ReleaseWorkers(town, b);
    }

    public static double SalePrice(Town town, Building b) => Js.Round(town.LotPrice(town.Lot(b.LotId)) + b.Def.Cost * 0.6);

    private static void Rent(Town town)
    {
        foreach (var house in town.Buildings)
        {
            if (!house.Complete || house.Def.Housing == 0) continue;
            foreach (int rid in house.Residents.ToList())
            {
                var r = town.Agent(rid);
                if (house.OwnerId == r.Id || (house.OwnerId != null && r.SpouseId == house.OwnerId)) continue;
                var landlord = house.OwnerId != null ? town.Agent(house.OwnerId.Value) : null;
                double rent = RentOf(town, house);
                if (r.Gold >= rent)
                {
                    r.RentArrears = 0;
                    r.Gold -= rent;
                    if (landlord != null) landlord.Gold += rent;
                    else town.Book(TreasuryAccount.Rent, rent);
                }
                else if (landlord != null && landlord.Has(Trait.Generous))
                {
                    r.ChangeOpinion(landlord.Id, 3);
                }
                else if (++r.RentArrears < 3)
                {
                    // A few days' grace before the bailiff comes.
                }
                else
                {
                    r.RentArrears = 0;
                    Population.Remove(house.Residents, r.Id);
                    r.HomeId = null;
                    if (landlord != null) r.ChangeOpinion(landlord.Id, -25);
                    town.Log(
                        LedgerCategory.Hardship,
                        Importance.Notable,
                        $"{r.Name} cannot pay rent and is turned out of {house.Name} by {town.OwnerName(house.OwnerId)}.",
                        landlord != null ? [r.Id, landlord.Id] : [r.Id]);
                }
            }
        }
    }

    /// <summary>A bed's daily rent: the house's own, dearer or cheaper by its ward.</summary>
    public static double RentOf(Town town, Building house) => Js.Round(house.Def.Rent * City.WardOf(town, town.Lot(house.LotId)).Rent * 10) / 10;

    /// <summary>Heroes lodge at the tavern between expeditions, even when married to a townsperson.</summary>
    public static void MoveIn(Town town, Agent a, Building home)
    {
        if (a.Hero != null || a.Departed) return;
        if (a.HomeId != null) Population.Remove(town.Building(a.HomeId.Value).Residents, a.Id);
        a.HomeId = home.Id;
        home.Residents.Add(a.Id);
    }

    private static void HousingMarket(Town town)
    {
        foreach (var a in town.Workers)
        {
            if (a.HomeId != null) continue;
            var spouse = a.SpouseId != null ? town.Agent(a.SpouseId.Value) : null;
            // A spouse who left town does not come back for the new house.
            if (spouse?.Departed == true) spouse = null;
            int beds = spouse != null && spouse.HomeId == null ? 2 : 1;
            if (spouse?.HomeId != null && town.Building(spouse.HomeId.Value).OpenBeds > 0)
            {
                MoveIn(town, a, town.Building(spouse.HomeId.Value));
                continue;
            }

            double purse = a.Gold + (spouse?.Gold ?? 0);
            int Score(Building b) => b.OwnerId == a.Id ? 1000 : b.OwnerId != null ? a.OpinionOf(b.OwnerId.Value) : 0;
            var home = town.Buildings
                .Where(b => b.Complete && b.OpenBeds >= beds && (b.OwnerId == a.Id || purse >= RentOf(town, b) * beds * 3))
                .OrderByDescending(Score)
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
        foreach (var site in town.Buildings)
        {
            if (!site.IsSite || site.LaborBudget >= Brain.ConstructionWage) continue;
            // A grand work waiting on its next stage lets its builders go; the treasury tops up only the stage under way.
            if (site.Def.IsGrand && GrandWorks.TopUp(town, site)) continue;
            // Weak laborers fall behind the estimate; an owner who can afford it pays for the last hours.
            double rest0 = Math.Ceiling(site.Def.BuildHours - site.BuildProgress) * Brain.ConstructionWage;
            if (site.OwnerId == null && town.Treasury >= rest0 + 20)
            {
                town.Book(TreasuryAccount.Works, -rest0);
                site.LaborBudget += rest0;
                continue;
            }
            if (site.OwnerId is int oid)
            {
                var owner = town.Agent(oid);
                double rest = Math.Ceiling(site.Def.BuildHours - site.BuildProgress) * Brain.ConstructionWage;
                if (owner.Gold >= rest + 20)
                {
                    owner.Gold -= rest;
                    site.LaborBudget += rest;
                    continue;
                }
            }
            foreach (int wid in site.Workers.Where(w => w != site.OwnerId).ToList())
            {
                town.Agent(wid).JobId = null;
                Population.Remove(site.Workers, wid);
            }
        }

        foreach (var a in town.Workers)
        {
            if (a.JobId != null) continue;
            // Owners staff their own businesses first.
            var job = town.Buildings.FirstOrDefault(b => b.OwnerId == a.Id && b.Complete && b.Def.IsBusiness && b.OpenJobs > 0);
            job ??= town.Buildings
                .Where(b => b.OpenJobs > 0 && b.Crew == 0)
                .OrderByDescending(b => b.Def.Profession == a.Profession ? 1 : 0)
                .ThenByDescending(b => b.Def.Wage)
                .ThenBy(b => b.Id)
                .FirstOrDefault();
            job ??= town.Buildings.FirstOrDefault(
                b => b.IsSite && b.Workers.Count < 3 && (b.LaborBudget >= Brain.ConstructionWage || b.OwnerId == a.Id));
            // The road crews take whoever is left.
            job ??= town.Buildings.FirstOrDefault(b => b.Crew > 0 && b.OpenJobs > 0);
            if (job == null) continue;

            a.JobId = job.Id;
            job.Workers.Add(a.Id);
            if (a.DaysUnemployed >= 3)
                town.Log(LedgerCategory.Business, Importance.Routine, $"After {a.DaysUnemployed} days idle, {a.Name} finds work at {job.Name}.", [a.Id]);
        }
    }

    public static void ReleaseWorkers(Town town, Building b)
    {
        foreach (int wid in b.Workers) town.Agent(wid).JobId = null;
        b.Workers.Clear();
    }
}
