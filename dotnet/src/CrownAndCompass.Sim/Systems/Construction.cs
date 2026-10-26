namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// How buildings go up, after Foundation: the townsfolk's own builds on painted land (homes, shops, workshops, farms) are
/// raised a few at a time by the town's builders, as many at once as it has crews (<see cref="ConstructionRules"/>), so a
/// zone fills in house by house rather than all at once. Every site needs its timber and stone carried onto it before the
/// walls can rise past their footings: a builder whose site has run out of materials spends the hour hauling them from the
/// lumber camp or quarry (<see cref="Activity.Hauling"/>), then builds with what is there.
/// </summary>
public static class Construction
{
    public static ConstructionRules Rules(Town town) => town.Content.City.Construction;

    /// <summary>A site the townsfolk raise on painted land, which waits its turn for a crew (the council's works and bastions go up on their own).</summary>
    public static bool Zoned(Town town, Building b) =>
        Planning.Paintable.Contains(b.Def.Zone) && !b.Def.Bastion && !b.Def.IsGrand && !Planning.IsGovernment(town, b.Def);

    /// <summary>How many of the townsfolk's builds the town's crews can have under way at once: the hall's own, and each builders' yard's.</summary>
    public static int Crews(Town town)
    {
        var r = Rules(town);
        int n = r.Crews;
        foreach (var b in town.Buildings)
            if (b.Complete && b.Def.Crews > 0) n += b.Def.Crews;
        return Math.Min(r.Most, n);
    }

    /// <summary>The crews a town of this size keeps busy: below it, builds queue for want of builders and the council wants another yard.</summary>
    public static int Wanted(Town town)
    {
        var r = Rules(town);
        return Math.Min(r.Most, r.Crews + town.Population / Math.Max(1, r.Residents));
    }

    /// <summary>Where a builders' yard's builder goes this hour: a site put first, else the nearest one with work to do.</summary>
    public static Building? SiteFor(Town town, Building yard)
    {
        var at = town.Lot(yard.LotId);
        Building? best = null;
        double bestD = double.MaxValue;
        foreach (var b in town.Buildings)
        {
            if (!b.IsSite || b.Def.Bastion || Stalled(town, b) || (b.Def.IsGrand && GrandWorks.Waiting(b))) continue;
            var l = town.Lot(b.LotId);
            double d = (l.X - at.X) * (l.X - at.X) + (l.Y - at.Y) * (l.Y - at.Y) - (b.Priority ? 1e6 : 0);
            if (d < bestD)
            {
                bestD = d;
                best = b;
            }
        }
        return best;
    }

    /// <summary>The townsfolk's builds under way.</summary>
    public static int UnderWay(Town town)
    {
        int n = 0;
        foreach (var b in town.Buildings)
            if (b.IsSite && Zoned(town, b)) n++;
        return n;
    }

    /// <summary>Whether a crew is free to break ground on another of the townsfolk's builds.</summary>
    public static bool Room(Town town) => UnderWay(town) < Crews(town);

    /// <summary>Loads of timber and stone a building takes.</summary>
    public static double Materials(BuildingDef d) => Math.Max(0, d.Timber) + Math.Max(0, d.Stone);

    /// <summary>The share of the materials on site a building starts with: all of them when it needs none, or is raised whole (grand works bring theirs stage by stage).</summary>
    public static double Start(BuildingDef d, bool complete) => complete || d.IsGrand || Materials(d) <= 0 ? 1 : 0;

    /// <summary>Builder-hours a building takes: raising it, and an average builder's hauling of its materials (what its labour budget pays for).</summary>
    public static double Hours(Town town, BuildingDef d) => d.BuildHours + (d.IsGrand ? 0 : Materials(d) / Rules(town).Load);

    /// <summary>Builder-hours left on a site, hauling included.</summary>
    public static double HoursLeft(Town town, Building b) => Math.Max(0, b.Def.BuildHours - b.BuildProgress) + (1 - b.Hauled) * Materials(b.Def) / Rules(town).Load;

    /// <summary>How far the builders can take a site with the materials on it, in build hours: its footings, then as far as the timber and stone go.</summary>
    public static double Ready(Town town, Building b) =>
        b.Def.BuildHours * Math.Min(1, Rules(town).Footings + b.Hauled * (1 - Rules(town).Footings));

    /// <summary>Whether the site's builders have built up to what has been carried onto it, so the next hour goes on hauling.</summary>
    public static bool Short(Town town, Building b) => b.IsSite && b.Hauled < 1 && b.BuildProgress >= Ready(town, b) - 1e-9;

    /// <summary>
    /// An hour's hauling onto a site by someone of the given strength modifier (day labourers count as average). A site
    /// that <see cref="Building.Draws"/> its materials takes them off the market's stock, its builder paying for each load as
    /// it goes; when the market has none (or a site put first is waiting on them) the haulers stand idle and the site stalls.
    /// Returns whether anything was carried.
    /// </summary>
    public static bool Haul(Town town, Building b, int strength = 0)
    {
        double loads = Materials(b.Def);
        if (loads <= 0)
        {
            b.Hauled = 1;
            return true;
        }
        double carry = Math.Min(Rules(town).Load * Math.Max(0.5, 1 + 0.1 * strength), (1 - b.Hauled) * loads);
        if (b.Draws)
        {
            carry = Draw(town, b, carry, loads);
            if (carry <= 0)
            {
                if (b.StalledSince < 0) b.StalledSince = town.Tick;
                return false;
            }
            b.StalledSince = -1;
        }
        b.Hauled = Math.Min(1, b.Hauled + carry / loads);
        return true;
    }

    /// <summary>
    /// Loads a drawing site gets off the market this hour, its timber and stone in proportion, paid by whoever builds it: the
    /// treasury for the council's sites, the owner's purse for a townsman's (who takes only what they can pay for).
    /// </summary>
    private static double Draw(Town town, Building b, double carry, double loads)
    {
        if (!b.Priority && Waiting(town, b)) return 0;
        var owner = b.OwnerId is int o ? town.Agent(o) : null;
        if (owner != null)
        {
            double price = 0;
            foreach (var (g, need) in Needs(b))
                price += Math.Min(carry * need / loads, town.Market.Stock(g)) * town.Market.Price(g);
            if (price > owner.Gold) carry *= Math.Max(0, owner.Gold) / price;
        }
        double got = 0, cost = 0;
        foreach (var (g, need) in Needs(b))
        {
            var (qty, paid) = town.Market.Take(g, carry * need / loads);
            got += qty;
            cost += paid;
        }
        if (owner != null) owner.Gold -= cost;
        else if (cost > 0) town.Book(TreasuryAccount.Works, -cost);
        b.Paid += cost;
        return got;
    }

    private static IEnumerable<(Good Good, double Need)> Needs(Building b)
    {
        if (b.Def.Timber > 0) yield return (Good.Timber, b.Def.Timber);
        if (b.Def.Stone > 0) yield return (Good.Stone, b.Def.Stone);
    }

    /// <summary>Whether a site put first is short of materials, so the others leave the market's stock to it.</summary>
    private static bool Waiting(Town town, Building except)
    {
        foreach (var o in town.Buildings)
            if (o.Priority && o.Draws && o.IsSite && o.Hauled < 1 && o.Id != except.Id) return true;
        return false;
    }

    /// <summary>A site that has stood waiting on timber or stone for a few hours.</summary>
    public static bool Stalled(Town town, Building b) => b.IsSite && b.Draws && b.StalledSince >= 0 && town.Tick - b.StalledSince >= Rules(town).StallHours;

    /// <summary>The timber and stone a site still needs carried onto it.</summary>
    public static (double Timber, double Stone) Wanting(Building b) =>
        (Math.Max(0, b.Def.Timber) * (1 - b.Hauled), Math.Max(0, b.Def.Stone) * (1 - b.Hauled));

    /// <summary>What a caravan would charge to bring a stalled site everything it still needs, at the import premium.</summary>
    public static double CaravanCost(Town town, Building b)
    {
        var (timber, stone) = Wanting(b);
        var m = town.Market;
        double premium = m.ImportPremium > 0 ? m.ImportPremium : 3;
        return Js.Round(timber * m.BasePrice(Good.Timber) * premium + stone * m.BasePrice(Good.Stone) * premium);
    }

    /// <summary>Why the council can't send for a site's materials, or null when it can.</summary>
    public static string? CaravanBlocked(Town town, Building b)
    {
        if (!b.IsSite || !b.Draws || b.Hauled >= 1) return "it has its materials";
        if (b.ImportDue >= 0) return "its own carts are on the road";
        if (town.Treasury < CaravanCost(town, b)) return "not enough gold";
        return null;
    }

    /// <summary>Send a caravan for everything the site still needs: dear, but it comes straight to the site and the work goes on.</summary>
    public static bool SendCaravan(Town town, Building b)
    {
        if (CaravanBlocked(town, b) != null) return false;
        double cost = CaravanCost(town, b);
        town.Book(TreasuryAccount.Works, -cost);
        if (b.Draws) b.Paid += cost;
        b.Hauled = 1;
        b.StalledSince = -1;
        b.ImportDue = -1;
        town.Log(LedgerCategory.Construction, Importance.Notable, $"A caravan brings timber and stone for {b.Name}, for {Js.Str(cost)} gold, and the builders go back to work.");
        return true;
    }

    /// <summary>The stand-in mayor sends for the materials of a council site that has stalled a day, if the treasury can spare it.</summary>
    public static void Daily(Town town)
    {
        foreach (var b in town.Buildings)
            if (b.OwnerId == null && Stalled(town, b) && town.Tick - b.StalledSince >= 24 && town.Treasury > CaravanCost(town, b) + 300) SendCaravan(town, b);
    }

    /// <summary>What a townsman pays a carter to bring the rest of a stalled site's materials from afar: dearer than the council's caravan.</summary>
    public static double ImportCost(Town town, Building b) => Js.Round(CaravanCost(town, b) * Rules(town).ImportPremium);

    /// <summary>
    /// Daily: a townsman whose site has stood stalled <see cref="ConstructionRules.ImportWait"/> days sends for the rest from
    /// afar if they can pay, and it comes <see cref="ConstructionRules.ImportDays"/> days later; one who can't waits on the market.
    /// </summary>
    public static void Imports(Town town)
    {
        var r = Rules(town);
        foreach (var b in town.Buildings)
        {
            if (b.OwnerId is not int o || !b.IsSite || !b.Draws) continue;
            if (b.ImportDue >= 0)
            {
                if (b.Hauled >= 1) b.ImportDue = -1;
                else if (town.Tick >= b.ImportDue)
                {
                    b.Hauled = 1;
                    b.ImportDue = -1;
                    b.StalledSince = -1;
                    town.Log(LedgerCategory.Construction, Importance.Routine, $"Carts reach {b.Name} with its timber and stone, and the builders go back to work.");
                }
                continue;
            }
            if (!Stalled(town, b) || town.Tick - b.StalledSince < r.ImportWait * SimTime.HoursPerDay) continue;
            var owner = town.Agent(o);
            double cost = ImportCost(town, b);
            if (owner.Gold < cost + r.ImportReserve) continue;
            owner.Gold -= cost;
            b.Paid += cost;
            b.ImportDue = town.Tick + r.ImportDays * SimTime.HoursPerDay;
            town.Log(LedgerCategory.Construction, Importance.Routine, $"{owner.Name} gives up waiting on the market and sends away for timber and stone for {b.Name}, for {Js.Str(cost)} gold. The carts are {r.ImportDays} days out.", [owner.Id]);
        }
    }

    /// <summary>The ways the town mends what storms, fires and enemies wreck: "crews" (builders as they come to it), "first" (put first), "hire" (the council pays hands).</summary>
    public static readonly string[] RepairWays = ["crews", "first", "hire"];

    /// <summary>Daily: damaged buildings mended as the town's repairs say; mended ones are whole again.</summary>
    public static void Mend(Town town)
    {
        var r = Rules(town);
        foreach (var b in town.Buildings.OrderBy(b => b.Id))
        {
            if (!b.Wrecked) continue;
            if (!b.IsSite)
            {
                b.Wrecked = false;
                b.Priority = false;
                continue;
            }
            if (town.Repairs == "first") b.Priority = true;
            else if (town.Repairs == "hire")
            {
                double hours = Math.Min(r.HireHours, Math.Max(0, b.Def.BuildHours - b.BuildProgress));
                double cost = Js.Round(hours * r.Hire);
                if (hours <= 0 || town.Treasury < cost + r.HireReserve) continue;
                town.Book(TreasuryAccount.Repairs, -cost);
                b.Raise(town, hours);
            }
        }
    }

    /// <summary>Gold a day the hired hands would cost at today's damage.</summary>
    public static double RepairCost(Town town)
    {
        var r = Rules(town);
        return town.Buildings.Where(b => b.Damaged).Sum(b => Js.Round(Math.Min(r.HireHours, Math.Max(0, b.Def.BuildHours - b.BuildProgress)) * r.Hire));
    }

    /// <summary>Choose how the town mends its damage.</summary>
    public static bool SetRepairs(Town town, string way)
    {
        if (!RepairWays.Contains(way)) return false;
        town.Repairs = way;
        return true;
    }

    /// <summary>Put a site first, or back in line.</summary>
    public static bool Prioritise(Town town, Building b, bool first)
    {
        if (!b.IsSite) return false;
        b.Priority = first;
        return true;
    }

    /// <summary>Sites in the order builders look for work at them: those put first, then by id.</summary>
    public static IEnumerable<Building> Sites(Town town) => town.Buildings.Where(b => b.IsSite).OrderByDescending(b => b.Priority).ThenBy(b => b.Id);

    /// <summary>
    /// Where a site's materials come from: the nearest lumber camp or quarry that stands (stone first for a building that is
    /// mostly stone), or null for the market at the square. The client walks its haulers between there and the site.
    /// </summary>
    public static Building? Yard(Town town, Building site)
    {
        string want = site.Def.Stone > site.Def.Timber ? "Stone" : "Timber";
        var lot = town.Lot(site.LotId);
        Building? best = null;
        double bestD = double.MaxValue;
        foreach (var b in town.Buildings)
        {
            if (!b.Complete || b.Def.Produces.Count == 0 || !b.Def.Produces.Any(p => p.Key.ToString() == want)) continue;
            var l = town.Lot(b.LotId);
            double d = (l.X - lot.X) * (l.X - lot.X) + (l.Y - lot.Y) * (l.Y - lot.Y);
            if (d < bestD)
            {
                bestD = d;
                best = b;
            }
        }
        return best;
    }
}

/// <summary>
/// Building rules (city.json "construction"): <see cref="Crews"/> crews the hall keeps, more from each builders' yard, up to
/// <see cref="Most"/>, and the council wants one crew more per <see cref="Residents"/> residents; the share of a build its footings take before any materials are needed
/// (<see cref="Footings"/>); and the loads of timber or stone a builder carries onto a site in an hour (<see cref="Load"/>).
/// </summary>
public sealed record ConstructionRules
{
    public int Crews { get; init; } = 2;
    public int Residents { get; init; } = 25;
    public int Most { get; init; } = 40;
    public double Footings { get; init; } = 0.1;
    public double Load { get; init; } = 3;

    /// <summary>Hours a site may wait on timber or stone before it counts as stalled.</summary>
    public int StallHours { get; init; } = 6;

    /// <summary>
    /// A townsman's stalled site: days its owner waits on the market before sending for the rest by caravan, the days the
    /// caravan takes on the road, its price over the council's caravan, and the gold the owner keeps back.
    /// </summary>
    public int ImportWait { get; init; } = 8;
    public int ImportDays { get; init; } = 10;
    public double ImportPremium { get; init; } = 2;
    public double ImportReserve { get; init; } = 40;

    /// <summary>Hired hands mending damage ("hire" repairs): gold an hour of work, hours a day at each building, and the treasury kept back.</summary>
    public double Hire { get; init; } = 1.5;
    public double HireHours { get; init; } = 10;
    public double HireReserve { get; init; } = 150;
}
