namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Grand works: great civic projects (a clock tower, an aqueduct, a gatehouse, a mage tower, royal gardens,
/// a colossus, and at the great city's charter three wonders: a cathedral, a palace and a citadel) that the charters unlock and that soak up a rich town's gold. Each is a building in
/// buildings.json with stages. Placing one pays for the first stage; when the builders finish a stage the
/// next draws its gold and goods from the treasury and the market, waiting while the treasury can't spare
/// them, so a work rises over seasons in plain sight. Once it stands it lends the town a lasting bonus
/// through its building (comfort, settlers, heroes, defense, sales, mandate) and costs upkeep.
/// </summary>
public static class GrandWorks
{
    /// <summary>Gold the treasury keeps back when a work draws its next stage on its own.</summary>
    public const double Reserve = 200;

    public static IEnumerable<BuildingDef> All(Town town) => town.Content.Buildings.Where(d => d.IsGrand);

    /// <summary>The town's grand work of this kind, rising or standing, if any.</summary>
    public static Building? Of(Town town, string id) => town.Buildings.FirstOrDefault(b => b.Def.Id == id);

    /// <summary>What a stage costs today: its gold, and its goods at the market (caravans bringing any shortfall).</summary>
    public static double StageCost(Town town, BuildingDef d, int stage)
    {
        var s = d.Stages[stage];
        double goods = 0;
        foreach (var (g, qty) in s.Goods) goods += town.Market.Quote(g, qty);
        return Js.Round(s.Gold + goods + s.Hours * Brain.ConstructionWage);
    }

    /// <summary>What the whole work would cost at today's prices.</summary>
    public static double TotalCost(Town town, BuildingDef d) => Enumerable.Range(0, d.Stages.Count).Sum(i => StageCost(town, d, i));

    /// <summary>Pay for a stage: the gold, the goods off the market, and the builders' wages into the site's budget.</summary>
    public static void Fund(Town town, Building site, string account)
    {
        var s = site.Def.Stages[site.Stage];
        double cost = s.Gold;
        foreach (var (g, qty) in s.Goods) cost += town.Market.Buy(g, qty);
        double labor = s.Hours * Brain.ConstructionWage;
        town.Book(account, -(cost + labor));
        site.LaborBudget += labor;
        site.Stage++;
    }

    /// <summary>The stage under way (or the one waiting for gold), 0-based.</summary>
    public static int Current(Building site) => Math.Min(site.Stage, site.Def.Stages.Count) - (Waiting(site) ? 0 : 1);

    /// <summary>The builders have finished every stage paid for, and the next waits on the treasury.</summary>
    public static bool Waiting(Building site) => site.IsSite && site.Def.IsGrand && site.BuildProgress >= site.Funded && site.Stage < site.Def.Stages.Count;

    /// <summary>
    /// The job market's question for a grand work whose budget ran short: waiting on its next stage, its
    /// builders go home; mid-stage, the treasury pays for the hours left in the stage. True when handled.
    /// </summary>
    public static bool TopUp(Town town, Building site)
    {
        if (site.BuildProgress >= site.Funded)
        {
            Economy.ReleaseWorkers(town, site);
            return true;
        }
        double rest = Math.Ceiling(site.Funded - site.BuildProgress) * Brain.ConstructionWage;
        if (town.Treasury < rest + 20) return false;
        town.Book(TreasuryAccount.GrandWorks, -rest);
        site.LaborBudget += rest;
        return true;
    }

    /// <summary>Daily: works whose builders reached the end of a stage draw the next one when the treasury can spare it.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        foreach (var site in town.Buildings.Where(Waiting).ToList())
        {
            // Wages left over from the last stage go back to the treasury.
            if (site.LaborBudget > 0)
            {
                town.Book(TreasuryAccount.GrandWorks, site.LaborBudget);
                site.LaborBudget = 0;
            }
            var stage = site.Def.Stages[site.Stage];
            if (town.Treasury < StageCost(town, site.Def, site.Stage) + Reserve) continue;
            Fund(town, site, TreasuryAccount.GrandWorks);
            town.Log(
                LedgerCategory.Construction,
                Importance.Notable,
                $"Work begins on {stage.Name} of {site.Name}, stage {site.Stage} of {site.Def.Stages.Count}.");
        }
        if (autoMayor) Steward(town);
    }

    /// <summary>A grand work stands: the town celebrates and the bonus begins.</summary>
    public static void Finished(Town town, Building b)
    {
        town.Log(LedgerCategory.Town, Importance.Major, $"{Cap(b.Name)} stands complete, and the whole town turns out to see it. {Bonus(b.Def)}.");
        town.FestivalDays = Math.Max(town.FestivalDays, 2);
        if (b.Def.Lofted) Lofted.Risen(town, b);
    }

    /// <summary>What the finished work does for the town, in a sentence.</summary>
    public static string Bonus(BuildingDef d)
    {
        var parts = new List<string>();
        if (d.Comfort > 0) parts.Add($"every household {Js.Round(d.Comfort * 100)}% more content");
        if (d.Attraction > 0) parts.Add($"+{Js.ToFixed(d.Attraction, 2)} draw on settlers");
        if (d.Heroes > 0) parts.Add($"room for {d.Heroes} more heroes");
        if (d.Defense > 0) parts.Add($"walls {Js.Round(d.Defense * 100)}% stronger");
        if (d.WatchBoost > 0) parts.Add($"each watchman counts {Js.Round(d.WatchBoost * 100)}% more");
        if (d.SalesBoost > 0) parts.Add($"+{Js.Round(d.SalesBoost * 100)}% of sales to the treasury");
        if (d.Mandate > 0) parts.Add($"+{Js.Str(d.Mandate)} mandate");
        return Cap(Expedition.ListNames(parts));
    }

    // What standing works do ------------------------------------------------------------------

    /// <summary>Standing grand works, and monuments' parts, which lend their comfort, draw and hero beds the same way.</summary>
    private static IEnumerable<Building> Standing(Town town) => town.Buildings.Where(b => (b.Def.IsGrand || b.Def.IsPart) && b.Complete);

    /// <summary>Added to every house's contentment.</summary>
    public static double Comfort(Town town) => Standing(town).Sum(b => b.Def.Comfort);

    /// <summary>Added to the town's draw on settlers.</summary>
    public static double Draw(Town town) => Standing(town).Sum(b => b.Def.Attraction);

    /// <summary>More heroes the taverns can keep.</summary>
    public static int Heroes(Town town) => Standing(town).Sum(b => b.Def.Heroes);

    // The stand-in mayor ----------------------------------------------------------------------

    /// <summary>A rich town raises the grand works its charter allows, one at a time, cheapest first, keeping a deep reserve.</summary>
    private static void Steward(Town town)
    {
        if (town.Buildings.Any(b => b.Def.IsGrand && b.IsSite)) return;
        foreach (var d in All(town).Where(d => !d.Lofted).OrderBy(d => d.Stages.Sum(s => s.Gold)).ThenBy(d => d.Id, StringComparer.Ordinal))
        {
            if (Planning.Blocked(town, d) != null) continue;
            // Only gold the town plainly doesn't need: several times the first stage, and well past the next charter.
            double reserve = 1500 + 3 * StageCost(town, d, 0) + (City.NextTier(town)?.Cost ?? 0);
            if (town.Treasury < reserve) return;
            var lot = Planning.CivicLot(town, d);
            if (lot != null && Planning.Place(town, d.Id, lot) != null) return;
        }
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
