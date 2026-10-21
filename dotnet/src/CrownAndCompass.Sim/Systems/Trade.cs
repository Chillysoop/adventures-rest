namespace CrownAndCompass.Sim.Systems;

/// <summary>What a people buys from the town and what it sells (world.json peoples, "trade").</summary>
public sealed record TradeGoodsDef
{
    public IReadOnlyList<Good> Wants { get; init; } = [];
    public IReadOnlyList<Good> Offers { get; init; } = [];
}

/// <summary>Trade routes to the other settlements of the world (content/game/trade.json).</summary>
public sealed record TradeRules
{
    /// <summary>Standing a people must hold before it will trade with the town.</summary>
    public double MinStanding { get; init; } = 10;

    /// <summary>Below this the route closes: the people won't deal with the town any more.</summary>
    public double CloseBelow { get; init; } = -20;

    /// <summary>Gold to fit out the first caravan: a base, and more for each day of road.</summary>
    public double OpenCost { get; init; } = 80;
    public double OpenPerDay { get; init; } = 15;

    /// <summary>Routes the town can run at each charter tier.</summary>
    public Dictionary<string, int> Routes { get; init; } = [];

    /// <summary>Days a caravan spends at either end, on top of the road there and back.</summary>
    public int TurnaroundDays { get; init; } = 2;

    /// <summary>Goods a caravan carries each way: a base, and more for every hundred people in town.</summary>
    public double Load { get; init; } = 10;
    public double LoadPerHundred { get; init; } = 3;

    /// <summary>An allied people sends bigger caravans.</summary>
    public double AllyLoad { get; init; } = 1.3;

    /// <summary>The town keeps this share of each good's market target back from the caravans.</summary>
    public double KeepShare { get; init; } = 0.6;

    /// <summary>What a caravan's export fetches (times base price), more where the people want the good.</summary>
    public double ExportPrice { get; init; } = 1.0;
    public double WantedPrice { get; init; } = 1.25;

    /// <summary>What its import costs (times base price): cheaper than the open market's import premium.</summary>
    public double ImportPrice { get; init; } = 0.9;

    /// <summary>Goods are brought in only while the market holds less than this share of its target.</summary>
    public double ImportBelow { get; init; } = 1.0;

    /// <summary>Standing each caravan that gets through earns with the people at the other end.</summary>
    public double StandingPerCaravan { get; init; } = 0.8;

    /// <summary>Chance a caravan is waylaid: a base, more for each living menace in or next to the route's region.</summary>
    public double Raid { get; init; } = 0.03;
    public double RaidPerMenace { get; init; } = 0.06;

    /// <summary>Share of its power a menace gains from each caravan it takes once it holds the road.</summary>
    public double WaylayFeeds { get; init; } = 0;

    /// <summary>Wagons a caravan can grow to; each one past the first carries WagonLoad more of a load.</summary>
    public int Wagons { get; init; } = 4;
    public double WagonLoad { get; init; } = 0.75;

    /// <summary>Gold to add the second, third and fourth wagons.</summary>
    public double[] WagonCost { get; init; } = [200, 450, 900];

    /// <summary>The sky dock's airship on trade runs (<see cref="Skyship"/>).</summary>
    public SkyRules Sky { get; init; } = new();
}

/// <summary>A trade route the town runs to a settlement: what it sends, what it brings back, and how it has done.</summary>
public sealed class TradeRoute
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public Good Export { get; init; }
    public Good Import { get; init; }
    public int OpenedDay { get; init; }
    public int NextDay { get; set; }
    public bool Closed { get; set; }
    public int Caravans { get; set; }
    public int Raided { get; set; }

    /// <summary>Caravans lost in a row: the road's raiders get bolder, and get a name.</summary>
    public int LostInRow { get; set; }

    /// <summary>How many wagons its caravan runs; more carry more of the surplus out.</summary>
    public int Wagons { get; set; } = 1;

    /// <summary>Gold the exports fetched and the imports cost, over the route's life.</summary>
    public double Earned { get; set; }
    public double Spent { get; set; }
}

public sealed class TradeState
{
    public List<TradeRoute> Routes { get; } = [];

    /// <summary>The airship's trade runs (<see cref="Skyship"/>).</summary>
    public SkyState Sky { get; } = new();
}

/// <summary>
/// Trade with the other settlements of the world: the dwarf holds, elf enclaves, free ports and oasis
/// cities the town has heard of. A people that thinks well enough of the town will take a trade route: a
/// caravan that runs there and back on the road, selling the town's surplus of a good they want and
/// bringing back one they make, cheaper than the open market's import premium. Each caravan that gets
/// through warms the people to the town; menaces along the road waylay some, and winter slows them all.
/// A bigger town can run more routes (by charter tier) and loads bigger caravans.
/// </summary>
public static class Trade
{
    private static TradeRules Rules(Town town) => town.Content.Trade;

    public static IEnumerable<TradeRoute> Open(Town town) => town.Trade.Routes.Where(r => !r.Closed);

    /// <summary>Routes the town's charter lets it run.</summary>
    public static int Cap(Town town) => Rules(town).Routes.GetValueOrDefault(City.Tier(town).Id, 0) + Institutions.Routes(town);

    public static Settlement SettlementOf(Town town, TradeRoute r) => town.Settlements[r.SettlementId];

    /// <summary>Days for a caravan to go there and back, with its turnaround and the season's mud or snow.</summary>
    public static int TripDays(Town town, Settlement s) => Math.Max(2, 2 * town.Regions[s.RegionId].Days + Rules(town).TurnaroundDays + 2 * Seasons.MarchDays(town) - Neighbours.TripDays(town, s));

    public static double OpenCost(Town town, Settlement s) => Rules(town).OpenCost + Rules(town).OpenPerDay * town.Regions[s.RegionId].Days;

    public static double Load(Town town, Settlement s, TradeRoute? route = null) =>
        (Rules(town).Load + Rules(town).LoadPerHundred * town.Population / 100.0) * (s.Allied ? Rules(town).AllyLoad : 1)
        * (1 + Rules(town).WagonLoad * ((route?.Wagons ?? 1) - 1)) * Reigns.Trade(town, s) * Shadows.TradeShare(town, s);

    /// <summary>Gold to add the next wagon to a route's caravan, or null when it runs all it can.</summary>
    public static double? WagonCost(Town town, TradeRoute route) =>
        route.Wagons >= Rules(town).Wagons || route.Wagons - 1 >= Rules(town).WagonCost.Length ? null : Rules(town).WagonCost[route.Wagons - 1];

    /// <summary>The player's lever: add a wagon to a route's caravan so it carries more of the surplus out (and more back).</summary>
    public static bool AddWagon(Town town, int routeId)
    {
        if (town.Trade.Routes.FirstOrDefault(r => r.Id == routeId) is not { Closed: false } route) return false;
        if (WagonCost(town, route) is not double cost || town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Trade, -cost);
        route.Wagons++;
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"The council buys another wagon for the caravan to {SettlementOf(town, route).Name} ({Js.Str(cost)} gold). It now runs {route.Wagons} wagons.");
        return true;
    }

    /// <summary>Why the town can't open a route to this settlement now, or null if it can.</summary>
    public static string? Refuse(Town town, Settlement s)
    {
        var r = Rules(town);
        if (s.Razed) return "they are gone";
        if (!town.Regions[s.RegionId].Known) return "the town hasn't heard of them";
        if (s.Kin.Trade.Offers.Count == 0) return "they have nothing to sell";
        if (Shadows.Paying(s)) return "they pay tribute to an enemy and dare not trade";
        if (Open(town).Any(x => x.SettlementId == s.Id)) return "a route already runs there";
        if (s.Standing < r.MinStanding) return $"they won't trade below {Js.Str(r.MinStanding)} standing";
        if (Open(town).Count() >= Cap(town)) return Cap(town) == 0 ? "the town needs a hamlet's charter first" : "the town runs all the routes its charter allows";
        if (town.Treasury < OpenCost(town, s)) return $"needs {Js.Str(OpenCost(town, s))} gold";
        return null;
    }

    /// <summary>The goods a route there would carry: the surplus they want most, and what they make that the town is shortest of.</summary>
    public static (Good Export, Good Import) Suggest(Town town, Settlement s)
    {
        var m = town.Market;
        var goods = Enum.GetValues<Good>();
        var wants = s.Kin.Trade.Wants.Count > 0 ? s.Kin.Trade.Wants : goods;
        var offers = s.Kin.Trade.Offers.Count > 0 ? s.Kin.Trade.Offers : goods;
        var import = offers.OrderBy(g => m.Stock(g) / Math.Max(1, m.Target(g))).ThenBy(g => g).First();
        var export = wants.Where(g => g != import).OrderByDescending(g => m.Stock(g) / Math.Max(1, m.Target(g))).ThenBy(g => g)
            .Cast<Good?>().FirstOrDefault() ?? goods.First(g => g != import);
        return (export, import);
    }

    /// <summary>The player's lever: fit out a caravan and open a route to a settlement, carrying export out and import back.</summary>
    public static TradeRoute? OpenRoute(Town town, int settlementId, Good export, Good import)
    {
        if (settlementId < 0 || settlementId >= town.Settlements.Count || export == import) return null;
        var s = town.Settlements[settlementId];
        if (Refuse(town, s) != null || !s.Kin.Trade.Offers.Contains(import)) return null;
        double cost = OpenCost(town, s);
        town.Book(TreasuryAccount.Trade, -cost);
        var route = new TradeRoute
        {
            Id = town.Trade.Routes.Count + 1,
            SettlementId = s.Id,
            Export = export,
            Import = import,
            OpenedDay = town.Day,
            NextDay = town.Day + TripDays(town, s),
        };
        town.Trade.Routes.Add(route);
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"The council fits out a caravan for {s.Name} ({Js.Str(cost)} gold): {Lower(export)} out to {s.People}, {Lower(import)} back. It will run there and back every {TripDays(town, s)} days or so.");
        return route;
    }

    public static bool CloseRoute(Town town, int routeId)
    {
        if (town.Trade.Routes.FirstOrDefault(r => r.Id == routeId) is not { Closed: false } route) return false;
        route.Closed = true;
        var s = SettlementOf(town, route);
        town.Log(LedgerCategory.Town, Importance.Routine, $"The council winds up the caravan to {s.Name} after {route.Caravans} {(route.Caravans == 1 ? "run" : "runs")}.");
        return true;
    }

    /// <summary>Daily: each route's caravan comes home when its trip is done, sells and buys, and sets out again.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        foreach (var route in Open(town).OrderBy(x => x.Id).ToList())
        {
            var s = SettlementOf(town, route);
            if (s.Razed || s.Standing < r.CloseBelow)
            {
                route.Closed = true;
                town.Log(LedgerCategory.Town, Importance.Notable,
                    s.Razed ? $"No caravan comes back from {s.Name}: there is nothing left there to trade with." : $"{Cap(s.People)} turn the town's caravan away at their gate. The route to {s.Name} is closed.");
                continue;
            }
            if (town.Day < route.NextDay) continue;
            Caravan(town, route, s);
            route.NextDay = town.Day + TripDays(town, s);
        }
        if (autoMayor) StandIn(town);
        Skyship.Daily(town, autoMayor);
    }

    private static void Caravan(Town town, TradeRoute route, Settlement s)
    {
        var r = Rules(town);
        var m = town.Market;
        double load = Load(town, s, route);
        double sent = Math.Max(0, Math.Min(load, m.Stock(route.Export) - m.Target(route.Export) * r.KeepShare));
        route.Caravans++;
        if (town.Rng.Chance(RaidChance(town, s)))
        {
            route.Raided++;
            route.LostInRow++;
            m.Seed(route.Export, -sent);
            var culprit = Waylayer(town, s);
            int esc = town.Content.Lairs.EscalateAt;
            if (culprit != null && route.LostInRow >= esc)
            {
                // The third caravan running: the road belongs to them now, and they grow fat on it.
                culprit.Power += Math.Max(1, culprit.Power * r.WaylayFeeds);
                Menaces.Remember(culprit, $"{route.LostInRow} caravans taken on the road to {s.Name}");
                Saga.Scar(town, 1);
                town.Log(LedgerCategory.War, Importance.Major, $"Another caravan to {s.Name} lost, the {Ordinal(route.LostInRow)} running. {Cap(culprit.Name)} holds that road now and grows fat on {town.Name}'s wagons. The drovers want an escort, or that road closed.");
            }
            else if (culprit != null && route.LostInRow == esc - 1)
                town.Log(LedgerCategory.War, Importance.Notable, $"The caravan to {s.Name} is waylaid again. This time the drovers saw whose banners: {culprit.Name}. The {Lower(route.Export)} is lost.");
            else
                town.Log(LedgerCategory.War, Importance.Notable, $"The caravan to {s.Name} is waylaid on the road. The {Lower(route.Export)} is lost, and the drovers limp home with nothing.");
            return;
        }
        route.LostInRow = 0;
        double earned = sent * m.BasePrice(route.Export) * (s.Kin.Trade.Wants.Contains(route.Export) ? r.WantedPrice : r.ExportPrice) * Founding.Trade(town);
        m.Seed(route.Export, -sent);
        if (earned > 0) town.Book(TreasuryAccount.Trade, earned);
        route.Earned += earned;

        double want = Math.Max(0, m.Target(route.Import) * r.ImportBelow - m.Stock(route.Import));
        double price = m.BasePrice(route.Import) * r.ImportPrice;
        double bought = Math.Min(Math.Min(load, want), price > 0 ? Math.Max(0, town.Treasury) / price : 0);
        if (bought > 0)
        {
            m.Seed(route.Import, bought);
            town.Book(TreasuryAccount.Trade, -bought * price);
            route.Spent += bought * price;
        }
        World.Shift(town, s, r.StandingPerCaravan);
        Armory.Caravan(town, s);
        town.Log(LedgerCategory.Town, Importance.Routine,
            $"The caravan comes home from {s.Name}: {(sent > 0 ? $"{Js.Str(Js.Round(sent))} {Lower(route.Export)} sold for {Js.Str(Js.Round(earned))} gold" : $"no {Lower(route.Export)} to spare this time")}" +
            $"{(bought > 0 ? $", {Js.Str(Js.Round(bought))} {Lower(route.Import)} bought for {Js.Str(Js.Round(bought * price))}" : "")}.");
    }

    /// <summary>Menaces holding ground in or beside the route's region make the road dangerous.</summary>
    public static double RaidChance(Town town, Settlement s)
    {
        var r = Rules(town);
        var region = town.Regions[s.RegionId];
        int near = town.Saga.Menaces.Count(m => m.Alive && Quests.Site(town, m.SeatId) is { RegionId: int rid } && rid >= 0 && rid < town.Regions.Count
            && Math.Abs(town.Regions[rid].Ring - region.Ring) <= 1 && Near(town.Regions[rid], region));
        // A bridge or patrol makes the road safer, and so do escorts; a menace holding it, far worse.
        return Math.Min(0.6, (r.Raid + r.RaidPerMenace * near) * Neighbours.RaidShare(town, s) + Plots.TollRaid(town, s)) * (1 - Institutions.Escort(town));
    }

    /// <summary>The strongest living menace on the road to a settlement, if any: who the drovers will name.</summary>
    private static Menace? Waylayer(Town town, Settlement s)
    {
        var region = town.Regions[s.RegionId];
        return town.Saga.Menaces
            .Where(m => m.Alive && m.Named && Quests.Site(town, m.SeatId) is { RegionId: int rid } && rid >= 0 && rid < town.Regions.Count
                && Math.Abs(town.Regions[rid].Ring - region.Ring) <= 1 && Near(town.Regions[rid], region))
            .OrderByDescending(m => m.Power).ThenBy(m => m.Id)
            .FirstOrDefault();
    }

    private static string Ordinal(int n) => n switch { 3 => "third", 4 => "fourth", 5 => "fifth", 6 => "sixth", _ => $"{n}th" };

    internal static bool Near(Region a, Region b)
    {
        double d = Math.Abs(a.Bearing - b.Bearing) % 360;
        return Math.Min(d, 360 - d) <= (a.Arc + b.Arc) / 2 + 1;
    }

    /// <summary>The stand-in mayor opens a route when it can afford it comfortably, to the friendliest people that will trade.</summary>
    private static void StandIn(Town town)
    {
        // A rich town puts more wagons on the road it already runs, the cheapest first.
        var grow = Open(town).Where(x => WagonCost(town, x) is double c && town.Treasury >= c * 3 + 500)
            .OrderBy(x => WagonCost(town, x)).ThenBy(x => x.Id).FirstOrDefault();
        if (grow != null) AddWagon(town, grow.Id);
        if (Open(town).Count() >= Cap(town)) return;
        // A careful mayor fits out no caravans while a warband camps at an unwalled town's door.
        if (Threat.IsActive(town) && town.Defenses.Palisade != PalisadeState.Built) return;
        var s = town.Settlements
            .Where(x => Refuse(town, x) == null && town.Treasury >= OpenCost(town, x) * 2 + 300)
            .OrderByDescending(x => x.Standing)
            .ThenBy(x => x.Id)
            .FirstOrDefault();
        if (s == null) return;
        var (export, import) = Suggest(town, s);
        OpenRoute(town, s.Id, export, import);
    }

    private static string Lower(Good g) => g.ToString().ToLowerInvariant();

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
