namespace CrownAndCompass.Sim.Systems;

public sealed record ShipDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public double Cost { get; init; }
    public double Timber { get; init; }
    public int Crew { get; init; }
    public double Hold { get; init; }

    /// <summary>Days at sea for each day out the port's region lies.</summary>
    public double Speed { get; init; } = 1;
    public double Hull { get; init; }
    public double Wear { get; init; }
    public double Repair { get; init; } = 1;
    public double Fish { get; init; }
    public double Fight { get; init; }

    /// <summary>The town tier it needs, if any.</summary>
    public string Tier { get; init; } = "";
}

public sealed record CorsairRules
{
    public string Name { get; init; } = "the corsairs";
    public double Chance { get; init; } = 0.12;
    public double PerRing { get; init; } = 0.05;
    public double Max { get; init; } = 0.45;
    public double Strength { get; init; } = 3;
    public double StrengthPerRing { get; init; } = 2;
    public double Loot { get; init; } = 0.6;
    public double Hurt { get; init; } = 25;
    public double Prize { get; init; } = 30;
    public int RaidEvery { get; init; } = 20;
    public double RaidChance { get; init; } = 0.35;
    public double RaidStrength { get; init; } = 5;
    public double RaidGold { get; init; } = 60;
    public double RaidFood { get; init; } = 30;

    /// <summary>What each watchman adds to the harbour's fight.</summary>
    public double WatchFight { get; init; } = 0.5;
    public IReadOnlyList<string> Win { get; init; } = [];
    public IReadOnlyList<string> Lose { get; init; } = [];
    public IReadOnlyList<string> Sunk { get; init; } = [];
    public IReadOnlyList<string> Repelled { get; init; } = [];
    public IReadOnlyList<string> Raided { get; init; } = [];
}

/// <summary>content/game/sea.json.</summary>
public sealed record FleetRules
{
    public double Ration { get; init; } = 0.25;
    public double MinStanding { get; init; } = -10;
    public double Reach { get; init; } = 100;
    public IReadOnlyList<string> CoastThemes { get; init; } = [];
    public int Turnaround { get; init; } = 1;
    public double Learn { get; init; } = 0.25;
    public double ProwessFish { get; init; } = 0.1;
    public double ProwessFight { get; init; } = 1;
    public double WinterFish { get; init; } = 0.5;
    public double PatchBelow { get; init; } = 0.5;
    public int StandInEvery { get; init; } = 10;
    public double StandInProfit { get; init; } = 40;
    public IReadOnlyList<ShipDef> Ships { get; init; } = [];
    public IReadOnlyList<string> Names { get; init; } = [];
    public IReadOnlyList<string> Captains { get; init; } = [];
    public IReadOnlyList<string> Prowess { get; init; } = [];
    public CorsairRules Corsairs { get; init; } = new();

    public ShipDef? Ship(string id) => Ships.FirstOrDefault(s => s.Id == id);
}

/// <summary>A trade run at sea: where, with what, and when it is due home.</summary>
public sealed class Voyage
{
    public int SettlementId { get; init; }
    public Good Cargo { get; init; }
    public double Qty { get; init; }
    public int Days { get; init; }
    public int BackDay { get; init; }
}

/// <summary>One of the town's ships: its kind, its captain and how good they are, and its hull.</summary>
public sealed class Ship
{
    public int Id { get; init; }
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string Captain { get; set; } = "";

    /// <summary>1 green to 5 legendary.</summary>
    public double Prowess { get; set; } = 1;
    public double Hull { get; set; }
    public Voyage? Out { get; set; }
    public int Voyages { get; set; }
    public double Earned { get; set; }
    public double Fished { get; set; }
    public string LastLine { get; set; } = "";
}

public sealed class FleetState
{
    public int NextId { get; set; }
    public int NextRaidDay { get; set; }
    public int NextStandInDay { get; set; }
    public bool Granted { get; set; }
}

/// <summary>
/// The town's ships, for a town founded on the sea (Q8, Q53, Q79): once the Harbour stands, boats fish while moored,
/// sail trade runs to the ports along the coast (selling like the airship, the same prices and gluts), wear at sea and are
/// patched at the harbour. Captains grow from green to legendary. Corsairs meet runs on the way home and now and then
/// raid the harbour. Towns away from the sea have no fleet and nothing here touches their dice.
/// </summary>
public static class Fleet
{
    public static FleetRules Rules(Town town) => town.Content.Sea;

    public static ShipDef Def(Town town, Ship s) => Rules(town).Ship(s.Kind) ?? throw new KeyNotFoundException($"Unknown ship {s.Kind}");

    /// <summary>The town's harbour, standing: needed for any ship.</summary>
    public static Building? Harbour(Town town) => town.Buildings.FirstOrDefault(b => b.Def.Id == "harbour" && b.Complete && !b.Closed);

    public static bool Coastal(Town town) => town.Founding != null && Founding.Of(town).Sea != null;

    public static string ProwessName(Town town, Ship s)
    {
        var p = Rules(town).Prowess;
        return p.Count == 0 ? "" : p[Math.Clamp((int)Math.Floor(s.Prowess) - 1, 0, p.Count - 1)];
    }

    // ---- Building ships ----

    public static string? BuildBlocked(Town town, string kind)
    {
        if (!Coastal(town)) return "the town isn't on the sea";
        if (Harbour(town) == null) return "needs a harbour";
        if (Rules(town).Ship(kind) is not { } d) return "no such ship";
        if (d.Tier != "" && !City.Reached(town, d.Tier)) return $"needs the town to be a {City.TierName(town, d.Tier).ToLowerInvariant()}";
        if (town.Treasury < Cost(town, d)) return "not enough gold";
        return null;
    }

    /// <summary>Gold for a new ship and its timber at today's market.</summary>
    public static double Cost(Town town, ShipDef d) => Js.Round(d.Cost + town.Market.Quote(Good.Timber, d.Timber));

    public static Ship? Build(Town town, string kind, bool free = false)
    {
        if (!free && BuildBlocked(town, kind) != null) return null;
        var r = Rules(town);
        var d = r.Ship(kind)!;
        if (!free)
        {
            double cost = d.Cost + town.Market.Buy(Good.Timber, d.Timber);
            town.Book(TreasuryAccount.Works, -cost);
        }
        var st = town.FleetState;
        int id = st.NextId++;
        var used = town.Ships.Select(s => s.Name).ToHashSet();
        var names = r.Names.Where(n => !used.Contains(n)).ToList();
        string name = names.Count > 0 ? names[(int)(World.Hash(town.Seed * 131 + id * 17 + 3) * names.Count)] : $"the {d.Name} {id + 1}";
        string captain = r.Captains.Count > 0 ? r.Captains[(int)(World.Hash(town.Seed * 137 + id * 19 + 5) * r.Captains.Count)] : "the mate";
        var ship = new Ship { Id = id, Kind = d.Id, Name = name, Captain = captain, Hull = d.Hull };
        town.Ships.Add(ship);
        town.Log(LedgerCategory.Town, Importance.Notable,
            free ? $"The harbour's first boat, {name}, is pushed down the slip. {captain} will skipper her."
                 : $"A new {d.Name}, {name}, slides down the harbour slip, with {captain} for her captain.");
        return ship;
    }

    // ---- Repairs ----

    public static double RepairCost(Town town, Ship s) => Js.Round((Def(town, s).Hull - s.Hull) * Def(town, s).Repair);

    public static bool Repair(Town town, int shipId)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || s.Out != null || s.Hull >= Def(town, s).Hull || Harbour(town) == null) return false;
        double cost = RepairCost(town, s);
        if (town.Treasury < cost) return false;
        town.Book(TreasuryAccount.Works, -cost);
        s.Hull = Def(town, s).Hull;
        town.Log(LedgerCategory.Town, Importance.Routine, $"Shipwrights patch {s.Name}'s hull ({Js.Str(cost)} gold).");
        return true;
    }

    // ---- Trade runs ----

    /// <summary>Ports a ship can sail to: known, not hostile, and along the coast.</summary>
    public static IEnumerable<Settlement> Ports(Town town)
    {
        if (!Coastal(town)) return [];
        var r = Rules(town);
        double sea = Founding.Facing(town);
        return town.Settlements.Where(s => !s.Razed && town.Regions[s.RegionId].Known && s.Standing >= r.MinStanding
            && (r.CoastThemes.Contains(town.Regions[s.RegionId].Theme.Id) || Gap(town.Regions[s.RegionId].Bearing, sea) <= r.Reach)).OrderBy(s => s.Id);
    }

    private static double Gap(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);

    public static int Days(Town town, Ship s, Settlement port) =>
        Math.Max(2, 2 * (int)Math.Ceiling(town.Regions[port.RegionId].Days * Def(town, s).Speed - 1e-9) + Rules(town).Turnaround);

    /// <summary>Food the crew eats on a run there and back.</summary>
    public static double Rations(Town town, Ship s, Settlement port) => Js.Round(Def(town, s).Crew * Days(town, s, port) * Rules(town).Ration * 10) / 10;

    public static double Spare(Town town, Ship s, Good g)
    {
        var m = town.Market;
        double keep = Math.Max(m.Target(g) * town.Content.Trade.Sky.KeepShare, m.Held(g)) + (g == Good.Food ? 0 : 0);
        return Math.Max(0, Math.Min(Def(town, s).Hold, m.Stock(g) - keep));
    }

    public static string? Refuse(Town town, Ship s, Settlement port, Good cargo)
    {
        var r = Rules(town);
        if (Harbour(town) == null) return "needs a harbour";
        if (s.Out != null) return "at sea";
        if (Def(town, s).Hold <= 0) return "no hold";
        if (s.Hull < Def(town, s).Hull * 0.25) return "needs patching first";
        if (!Ports(town).Contains(port)) return "not a port the ship can reach";
        if (town.Market.Stock(Good.Food) < Rations(town, s, port) + (cargo == Good.Food ? Spare(town, s, cargo) : 0)) return "not enough food for the crew";
        if (Spare(town, s, cargo) < town.Content.Trade.Sky.MinCargo) return $"no {Lower(cargo)} to spare";
        return null;
    }

    /// <summary>Gold the run should clear, before corsairs.</summary>
    public static double Profit(Town town, Ship s, Settlement port, Good cargo) =>
        Skyship.Quote(town, port, cargo, Spare(town, s, cargo)) - Rations(town, s, port) * town.Market.BasePrice(Good.Food);

    public static bool Send(Town town, int shipId, int settlementId, Good cargo)
    {
        var s = town.Ships.FirstOrDefault(x => x.Id == shipId);
        if (s == null || settlementId < 0 || settlementId >= town.Settlements.Count) return false;
        var port = town.Settlements[settlementId];
        if (Refuse(town, s, port, cargo) != null) return false;
        double qty = Spare(town, s, cargo), food = Rations(town, s, port);
        town.Market.Seed(cargo, -qty);
        town.Market.Seed(Good.Food, -food);
        int days = Days(town, s, port);
        s.Out = new Voyage { SettlementId = port.Id, Cargo = cargo, Qty = qty, Days = days, BackDay = town.Day + days };
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{Cap(s.Name)} sails for {port.Name} with {Js.Str(Js.Round(qty))} {Lower(cargo)} in the hold and {Js.Str(Js.Round(food))} food for the crew. Home in {days} days.");
        return true;
    }

    /// <summary>Daily, from <see cref="Trade.Daily"/>: the harbour's first boat, fishing, ships home, corsairs, and the stand-in.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        if (!Coastal(town)) return;
        if (Harbour(town) == null)
        {
            // The stand-in mayor raises the harbour once the treasury can spare it.
            var d = town.Content.Building("harbour");
            if (autoMayor && !town.Buildings.Any(b => b.Def.Id == d.Id) && Planning.Blocked(town, d) == null && town.Treasury > Planning.Cost(town, d) + 300
                && Land.Find(town, d, null) is { } lot)
            {
                lot.Zone = Zone.Civic;
                Planning.Place(town, d.Id, lot);
            }
            return;
        }
        var r = Rules(town);
        var st = town.FleetState;
        if (!st.Granted)
        {
            st.Granted = true;
            st.NextRaidDay = town.Day + r.Corsairs.RaidEvery;
            if (r.Ships.Count > 0) Build(town, r.Ships[0].Id, free: true);
        }
        bool winter = Seasons.Current(town).Id == "winter";
        foreach (var s in town.Ships.OrderBy(s => s.Id).ToList())
        {
            if (s.Out is { } v)
            {
                if (town.Day >= v.BackDay) Home(town, s, v);
                continue;
            }
            if (s.Hull <= 0) continue;
            var d = Def(town, s);
            double catchToday = d.Fish * (1 + r.ProwessFish * (s.Prowess - 1)) * (winter ? r.WinterFish : 1);
            town.Market.Seed(Good.Food, catchToday);
            s.Fished += catchToday;
        }
        if (town.Day >= st.NextRaidDay) Raid(town);
        if (autoMayor) StandIn(town);
    }

    private static Rng Dice(Town town, int salt) => new(town.Seed * 7349 + town.Day * 31 + salt);

    private static void Home(Town town, Ship s, Voyage v)
    {
        var r = Rules(town);
        var c = r.Corsairs;
        var port = town.Settlements[v.SettlementId];
        var d = Def(town, s);
        s.Out = null;
        s.Voyages++;
        s.Hull = Math.Max(0, s.Hull - d.Wear * v.Days);
        double qty = v.Qty;
        var rng = Dice(town, s.Id);
        string fight = "";
        int ring = town.Regions[port.RegionId].Ring;
        if (rng.Chance(Math.Min(c.Max, c.Chance + c.PerRing * ring)))
        {
            double strength = c.Strength + c.StrengthPerRing * ring;
            int die = rng.D20();
            bool won = die != 1 && (die == 20 || die + d.Fight + r.ProwessFight * (s.Prowess - 1) >= 10 + strength);
            if (won)
            {
                s.Prowess = Math.Min(5, s.Prowess + r.Learn);
                town.Book(TreasuryAccount.Trade, c.Prize);
                fight = " " + Fill(rng, c.Win, s, port).Replace("{prize}", Js.Str(c.Prize), StringComparison.Ordinal);
            }
            else
            {
                double lost = Js.Round(qty * c.Loot);
                qty -= lost;
                s.Hull = Math.Max(0, s.Hull - c.Hurt);
                fight = " " + Fill(rng, c.Lose, s, port).Replace("{lost}", Js.Str(lost), StringComparison.Ordinal).Replace("{good}", Lower(v.Cargo), StringComparison.Ordinal);
                if (s.Hull <= 0)
                {
                    town.Ships.Remove(s);
                    town.Log(LedgerCategory.War, Importance.Major, Fill(rng, c.Sunk, s, port));
                    return;
                }
            }
        }
        s.Prowess = Math.Min(5, s.Prowess + r.Learn);
        if (port.Razed)
        {
            town.Market.Seed(v.Cargo, qty);
            s.LastLine = $"{fight.Trim()} {Cap(s.Name)} comes home with her hold still full: there is nothing left at {port.Name}.".Trim();
            town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
            return;
        }
        double earned = Skyship.Sell(town, port, v.Cargo, qty);
        s.Earned += earned;
        string brought = "";
        if (Skyship.Bring(town, port, v.Cargo) is { } bring && bring.Price > 0)
        {
            var (good, price) = bring;
            var mk = town.Market;
            double want = Math.Max(0, mk.Target(good) * town.Content.Trade.Sky.BringBelow - mk.Stock(good));
            double bought = Math.Min(Math.Min(d.Hold, want), Math.Max(0, town.Treasury) / price);
            if (bought >= 5)
            {
                mk.Seed(good, bought);
                town.Book(TreasuryAccount.Trade, -bought * price);
                brought = $", and {Js.Str(Js.Round(bought))} of their {Lower(good)} bought cheap for {Js.Str(Js.Round(bought * price))}";
            }
        }
        World.Shift(town, port, town.Content.Trade.Sky.StandingPerRun);
        s.LastLine = $"{fight.Trim()} {Cap(s.Name)} ties up home from {port.Name}: {Js.Str(Js.Round(qty))} {Lower(v.Cargo)} sold for {Js.Str(Js.Round(earned))} gold{brought}.".Trim();
        town.Log(LedgerCategory.Town, Importance.Notable, s.LastLine);
    }

    /// <summary>Corsairs come for the harbour: the moored ships and the watch fight them off, or they rob it and hole a ship.</summary>
    private static void Raid(Town town)
    {
        var r = Rules(town);
        var c = r.Corsairs;
        town.FleetState.NextRaidDay = town.Day + c.RaidEvery;
        var rng = Dice(town, 977);
        if (!rng.Chance(c.RaidChance)) return;
        var moored = town.Ships.Where(s => s.Out == null && s.Hull > 0).OrderBy(s => s.Id).ToList();
        double guard = town.Watch * c.WatchFight + Js.Sum(moored, s => Def(town, s).Fight + r.ProwessFight * (s.Prowess - 1));
        int die = rng.D20();
        if (die + guard >= 10 + c.RaidStrength + town.Day / 120.0)
        {
            town.Log(LedgerCategory.War, Importance.Notable, rng.Pick(c.Repelled));
            return;
        }
        double gold = Math.Min(Math.Max(0, town.Treasury), c.RaidGold), food = Math.Min(town.Market.Stock(Good.Food), c.RaidFood);
        town.Book(TreasuryAccount.Works, -gold);
        town.Market.Seed(Good.Food, -food);
        var holed = moored.Count > 0 ? moored[rng.Range(0, moored.Count)] : null;
        if (holed != null) holed.Hull = Math.Max(1, holed.Hull - c.Hurt);
        town.Log(LedgerCategory.War, Importance.Notable, rng.Pick(c.Raided)
            .Replace("{gold}", Js.Str(Js.Round(gold)), StringComparison.Ordinal)
            .Replace("{food}", Js.Str(Js.Round(food)), StringComparison.Ordinal)
            .Replace("{ship}", holed?.Name ?? "a fishing boat", StringComparison.Ordinal));
    }

    /// <summary>The stand-in mayor patches worn hulls, adds a longship when it can afford one, and sends the best run now and then.</summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var st = town.FleetState;
        foreach (var s in town.Ships.Where(s => s.Out == null && s.Hull < Def(town, s).Hull * r.PatchBelow).ToList())
            if (town.Treasury > RepairCost(town, s) + 200) Repair(town, s.Id);
        if (town.Ships.Count < 2 && r.Ships.Count > 1 && BuildBlocked(town, r.Ships[1].Id) == null && town.Treasury > Cost(town, r.Ships[1]) + 400)
            Build(town, r.Ships[1].Id);
        if (town.Day < st.NextStandInDay) return;
        st.NextStandInDay = town.Day + r.StandInEvery;
        var best = town.Ships.Where(s => s.Out == null).OrderBy(s => s.Id)
            .SelectMany(s => Ports(town).SelectMany(p => Goods.All.Where(g => Refuse(town, s, p, g) == null).Select(g => (S: s, P: p, G: g, Gain: Profit(town, s, p, g)))))
            .Where(x => x.Gain >= r.StandInProfit)
            .OrderByDescending(x => x.Gain / Days(town, x.S, x.P)).ThenBy(x => x.S.Id).ThenBy(x => x.P.Id).ThenBy(x => x.G)
            .FirstOrDefault();
        if (best.S != null) Send(town, best.S.Id, best.P.Id, best.G);
    }

    private static string Fill(Rng rng, IReadOnlyList<string> texts, Ship s, Settlement port) =>
        (texts.Count == 0 ? "" : rng.Pick(texts))
            .Replace("{captain}", s.Captain, StringComparison.Ordinal)
            .Replace("{ship}", s.Name, StringComparison.Ordinal)
            .Replace("{port}", port.Name, StringComparison.Ordinal);

    private static string Lower(Good g) => g.ToString().ToLowerInvariant();

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
