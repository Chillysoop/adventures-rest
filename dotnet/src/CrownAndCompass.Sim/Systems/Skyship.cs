namespace CrownAndCompass.Sim.Systems;

/// <summary>The sky dock's trade runs (trade.json "sky", <see cref="Skyship"/>).</summary>
public sealed record SkyRules
{
    /// <summary>Standing a people needs before they'll let the airship moor: far below a caravan's, they only must not be hostile.</summary>
    public double MinStanding { get; init; } = -10;

    /// <summary>Goods the airship's hold takes: a base and more for every hundred people in town, up to HoldMax.</summary>
    public double Hold { get; init; } = 30;
    public double HoldPerHundred { get; init; } = 3;
    public double HoldMax { get; init; } = 90;

    /// <summary>The least cargo worth flying.</summary>
    public double MinCargo { get; init; } = 6;

    /// <summary>Days of flight for each day of road, and days moored at the far end.</summary>
    public double Speed { get; init; } = 0.5;
    public int Turnaround { get; init; } = 1;

    /// <summary>Gold to fill the gasbag and pay the riggers: a base, and more for each day aloft.</summary>
    public double FitOut { get; init; } = 40;
    public double FitOutPerDay { get; init; } = 8;

    /// <summary>The town keeps this share of each good's market target back from the hold.</summary>
    public double KeepShare { get; init; } = 0.6;

    /// <summary>What cargo fetches (times base price), more where the people want it, more again for each ring out.</summary>
    public double SellPrice { get; init; } = 1.1;
    public double WantedPrice { get; init; } = 1.5;
    public double FarPremium { get; init; } = 0.12;

    /// <summary>How far each people's prices sit from the rest, either way (a fixed share per people and good).</summary>
    public double Spread { get; init; } = 0.25;

    /// <summary>Goods sold into one market that halve its price, and the share of the glut that clears each day.</summary>
    public double Glut { get; init; } = 45;
    public double GlutFade { get; init; } = 0.04;

    /// <summary>What the partner's own goods cost (times base price), bought for the trip home while the town holds less than BringBelow of its target.</summary>
    public double BuyPrice { get; init; } = 0.65;
    public double BringBelow { get; init; } = 1.2;

    /// <summary>Standing each run that gets there earns with the people.</summary>
    public double StandingPerRun { get; init; } = 0.5;

    /// <summary>Chance by season id that storms hold the ship back, and the days they cost.</summary>
    public Dictionary<string, double> Storm { get; init; } = [];
    public int[] StormDays { get; init; } = [1, 3];

    /// <summary>Chance of being set upon for each whole flying host (passage.json "fliers") near the far end, at most AmbushMax; the cargo share cut loose to get away.</summary>
    public double Ambush { get; init; } = 0.12;
    public double AmbushMax { get; init; } = 0.35;
    public double AmbushLoss { get; init; } = 0.5;

    /// <summary>The stand-in mayor sends a run at most this often, when it clears StandInProfit gold.</summary>
    public int StandInEvery { get; init; } = 12;
    public double StandInProfit { get; init; } = 60;
}

/// <summary>The airship out on a trade run: where, with what, and when it is due home.</summary>
public sealed class SkyRun
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public Good Cargo { get; init; }
    public double Qty { get; init; }
    public int SetOutTick { get; init; }
    public int BackDay { get; set; }

    /// <summary>Storms have already held it back once.</summary>
    public bool Stormed { get; set; }
}

/// <summary>How much of a good the airship has sold into a people's market lately: the more, the less it fetches there.</summary>
public sealed class SkyGlut
{
    public int SettlementId { get; init; }
    public Good Good { get; init; }
    public double Qty { get; set; }
}

public sealed class SkyState
{
    public SkyRun? Out { get; set; }
    public List<SkyGlut> Gluts { get; } = [];
    public int Runs { get; set; }
    public double Earned { get; set; }
    public double Spent { get; set; }

    /// <summary>When the airship last moored home from a run, for the client to fly it in.</summary>
    public int LandedTick { get; set; } = -999;
    public int LastPort { get; set; } = -1;
    public string LastLine { get; set; } = "";
    public int NextStandInDay { get; set; }
}

/// <summary>
/// The Merchant of the Skies: the sky dock's airship, between carrying companies, flies the town's surplus to any people
/// that isn't hostile, straight over water and mountains in half the days a caravan takes and without a trade route. Each
/// people pays its own price for each good, more far out and for what they want, and less the more the town has sold them
/// lately; the ship comes home with the gold and a hold of their cheap goods the town is short of. Storms hold it back and
/// flying hosts near the far end can set on it, but only modestly. One airship: on a run it carries no company, and out
/// with a company it flies no run.
/// </summary>
public static class Skyship
{
    private static SkyRules Rules(Town town) => town.Content.Trade.Sky;

    private static SkyState State(Town town) => town.Trade.Sky;

    public static bool Away(Town town) => State(town).Out != null;

    public static double Hold(Town town) => Math.Min(Rules(town).HoldMax, Rules(town).Hold + Rules(town).HoldPerHundred * town.Population / 100.0);

    /// <summary>Days there and back by air: half the road and none of its mud or snow.</summary>
    public static int FlightDays(Town town, Settlement s) =>
        Math.Max(2, 2 * (int)Math.Ceiling(town.Regions[s.RegionId].Days * Rules(town).Speed - 1e-9) + Rules(town).Turnaround);

    public static double FitOut(Town town, Settlement s) => Rules(town).FitOut + Rules(town).FitOutPerDay * FlightDays(town, s);

    /// <summary>What the town can spare of a good for the hold.</summary>
    public static double Spare(Town town, Good g)
    {
        var m = town.Market;
        return Math.Max(0, Math.Min(Hold(town), m.Stock(g) - Math.Max(m.Target(g) * Rules(town).KeepShare, m.Held(g))));
    }

    /// <summary>A people's own lean on a good's price, the same every time: some pay well for wine, some don't.</summary>
    private static double Lean(Town town, Settlement s, Good g) => 1 + Rules(town).Spread * (2 * World.Hash(town.Seed * 31 + s.Id * 977 + (int)g * 131 + 7) - 1);

    private static SkyGlut? GlutOf(Town town, Settlement s, Good g) => State(town).Gluts.FirstOrDefault(x => x.SettlementId == s.Id && x.Good == g);

    /// <summary>What the first unit of a good fetches there now, before the glut the sale itself makes.</summary>
    public static double Price(Town town, Settlement s, Good g)
    {
        var r = Rules(town);
        double p = town.Market.BasePrice(g) * (s.Kin.Trade.Wants.Contains(g) ? r.WantedPrice : r.SellPrice)
            * (1 + r.FarPremium * town.Regions[s.RegionId].Ring) * Lean(town, s, g);
        return p / (1 + (GlutOf(town, s, g)?.Qty ?? 0) / r.Glut);
    }

    /// <summary>Gold for selling qty of a good there: each unit sinks the price under the next.</summary>
    public static double Quote(Town town, Settlement s, Good g, double qty)
    {
        var r = Rules(town);
        double g0 = GlutOf(town, s, g)?.Qty ?? 0;
        double first = Price(town, s, g) * (1 + g0 / r.Glut);
        return qty <= 0 ? 0 : first * r.Glut * Math.Log((r.Glut + g0 + qty) / (r.Glut + g0));
    }

    /// <summary>What they sell that the town is shortest of, for the trip home, and what it costs a unit there.</summary>
    public static (Good Good, double Price)? Bring(Town town, Settlement s, Good cargo)
    {
        var m = town.Market;
        var offers = s.Kin.Trade.Offers.Where(g => g != cargo).ToList();
        if (offers.Count == 0) return null;
        var g = offers.OrderBy(x => m.Stock(x) / Math.Max(1, m.Target(x))).ThenBy(x => x).First();
        return (g, m.BasePrice(g) * Rules(town).BuyPrice * Lean(town, s, g));
    }

    /// <summary>Peoples the airship can fly to: known, standing, and not hostile.</summary>
    public static IEnumerable<Settlement> Ports(Town town) =>
        town.Settlements.Where(s => !s.Razed && Neighbours.Known(town, s) && s.Standing >= Rules(town).MinStanding).OrderBy(s => s.Id);

    /// <summary>Why the airship can't fly this run now, or null if it can.</summary>
    public static string? Refuse(Town town, Settlement s, Good cargo)
    {
        var r = Rules(town);
        if (Institutions.Airships(town) == 0) return "the town has no airship";
        if (Away(town)) return "the airship is out on a run";
        if (Passage.AirshipsFree(town) <= 0) return "the airship is out with a company";
        if (s.Razed) return "they are gone";
        if (!Neighbours.Known(town, s)) return "the town hasn't met them";
        if (s.Standing < r.MinStanding) return "they would fire on it";
        if (Spare(town, cargo) < r.MinCargo) return $"the town has no {Lower(cargo)} to spare";
        if (town.Treasury < FitOut(town, s)) return $"needs {Js.Str(FitOut(town, s))} gold to fit out";
        return null;
    }

    /// <summary>Gold the run should clear, before storms and fliers: the cargo's sale, less the fit-out.</summary>
    public static double Profit(Town town, Settlement s, Good cargo) => Quote(town, s, cargo, Spare(town, cargo)) - FitOut(town, s);

    /// <summary>The player's lever: load the airship with a good and send it to a people to sell, to come home with gold and their goods.</summary>
    public static SkyRun? Send(Town town, int settlementId, Good cargo)
    {
        if (settlementId < 0 || settlementId >= town.Settlements.Count) return null;
        var s = town.Settlements[settlementId];
        if (Refuse(town, s, cargo) != null) return null;
        var st = State(town);
        double qty = Spare(town, cargo), cost = FitOut(town, s);
        town.Book(TreasuryAccount.Trade, -cost);
        st.Spent += cost;
        town.Market.Seed(cargo, -qty);
        int days = FlightDays(town, s);
        st.Out = new SkyRun { Id = st.Runs + 1, SettlementId = s.Id, Cargo = cargo, Qty = qty, SetOutTick = town.Tick, BackDay = town.Day + days };
        town.Log(LedgerCategory.Town, Importance.Notable,
            $"{Cap(Name(town))} casts off from the Sky Dock with {Js.Str(Js.Round(qty))} {Lower(cargo)} in the hold, bound over everything for {s.Name} ({Js.Str(cost)} gold to fit out). It should be home in {days} days.");
        return st.Out;
    }

    /// <summary>Daily, from <see cref="Trade.Daily"/>: gluts clear, the ship comes home when its run is done, and the stand-in sends one now and then.</summary>
    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var st = State(town);
        foreach (var g in st.Gluts) g.Qty *= 1 - r.GlutFade;
        st.Gluts.RemoveAll(g => g.Qty < 0.5);
        if (st.Out is { } run && town.Day >= run.BackDay) Home(town, run);
        if (autoMayor) StandIn(town);
    }

    private static void Home(Town town, SkyRun run)
    {
        var r = Rules(town);
        var st = State(town);
        var s = town.Settlements[run.SettlementId];
        // Storms hold the ship back once a run at most: a day or three lost, nothing more.
        if (!run.Stormed && town.Rng.Chance(r.Storm.GetValueOrDefault(Seasons.Current(town).Id)))
        {
            run.Stormed = true;
            int late = town.Rng.Range(r.StormDays[0], r.StormDays[^1] + 1);
            run.BackDay = town.Day + late;
            town.Log(LedgerCategory.Town, Importance.Routine, $"A bird from {s.Name}: storms keep {Name(town)} moored there {late} more {(late == 1 ? "day" : "days")}.");
            return;
        }
        st.Out = null;
        st.Runs++;
        st.LandedTick = town.Tick;
        st.LastPort = s.Id;
        double qty = run.Qty;
        string set = "";
        if (Flier(town, s) is { } m && town.Rng.Chance(AmbushChance(town, s)))
        {
            double lost = qty * r.AmbushLoss;
            qty -= lost;
            set = $" Over {town.Regions[s.RegionId].Name} {m.Name} came at it out of the clouds; the riggers cut {Js.Str(Js.Round(lost))} {Lower(run.Cargo)} loose to get away.";
        }
        if (s.Razed)
        {
            st.LastLine = $"{Cap(Name(town))} comes home with its hold still full: there is nothing left at {s.Name} to sell to.{set}";
            town.Market.Seed(run.Cargo, qty);
            town.Log(LedgerCategory.Town, Importance.Notable, st.LastLine);
            return;
        }
        double earned = Quote(town, s, run.Cargo, qty);
        var glut = GlutOf(town, s, run.Cargo);
        if (glut == null) st.Gluts.Add(glut = new SkyGlut { SettlementId = s.Id, Good = run.Cargo });
        glut.Qty += qty;
        if (earned > 0) town.Book(TreasuryAccount.Trade, earned);
        st.Earned += earned;
        string brought = "";
        if (Bring(town, s, run.Cargo) is { } bring && bring.Price > 0)
        {
            var (good, price) = bring;
            var mk = town.Market;
            double want = Math.Max(0, mk.Target(good) * r.BringBelow - mk.Stock(good));
            double bought = Math.Min(Math.Min(Hold(town), want), Math.Max(0, town.Treasury) / price);
            if (bought >= 5)
            {
                mk.Seed(good, bought);
                town.Book(TreasuryAccount.Trade, -bought * price);
                st.Spent += bought * price;
                brought = $", and {Js.Str(Js.Round(bought))} of their {Lower(good)} bought cheap for {Js.Str(Js.Round(bought * price))}";
            }
        }
        World.Shift(town, s, r.StandingPerRun);
        st.LastLine = $"{Cap(Name(town))} moors at the Sky Dock home from {s.Name}: {Js.Str(Js.Round(qty))} {Lower(run.Cargo)} sold for {Js.Str(Js.Round(earned))} gold{brought}.{set}";
        town.Log(LedgerCategory.Town, Importance.Notable, st.LastLine);
    }

    /// <summary>The flying host likeliest to set on the ship near a people, if any: the strongest living one near their region.</summary>
    public static Menace? Flier(Town town, Settlement s)
    {
        var region = town.Regions[s.RegionId];
        return Fliers(town, region).OrderByDescending(m => Passage.Fliers(town, m.Kind)).ThenByDescending(m => m.Power).ThenBy(m => m.Id).FirstOrDefault();
    }

    private static IEnumerable<Menace> Fliers(Town town, Region region) =>
        town.Saga.Menaces.Where(m => m.Alive && Passage.Fliers(town, m.Kind) > 0 && Quests.Site(town, m.SeatId) is { RegionId: int rid } && rid >= 0 && rid < town.Regions.Count
            && Math.Abs(town.Regions[rid].Ring - region.Ring) <= 1 && Trade.Near(town.Regions[rid], region));

    /// <summary>Chance a flying host sets on the ship over a people's region.</summary>
    public static double AmbushChance(Town town, Settlement s)
    {
        var r = Rules(town);
        return Math.Min(r.AmbushMax, r.Ambush * Js.Sum(Fliers(town, town.Regions[s.RegionId]), m => Passage.Fliers(town, m.Kind)));
    }

    /// <summary>The stand-in mayor flies the town's richest surplus to whoever pays best for it, now and then, if it clears a profit.</summary>
    private static void StandIn(Town town)
    {
        var r = Rules(town);
        var st = State(town);
        if (town.Day < st.NextStandInDay || Away(town) || Passage.AirshipsFree(town) <= 0) return;
        st.NextStandInDay = town.Day + r.StandInEvery;
        var best = Ports(town)
            .SelectMany(s => Goods.All.Where(g => Refuse(town, s, g) == null && town.Treasury >= FitOut(town, s) * 3 + 300)
                .Select(g => (S: s, G: g, Per: Profit(town, s, g) / FlightDays(town, s))))
            .Where(x => x.Per * FlightDays(town, x.S) >= r.StandInProfit)
            .OrderByDescending(x => x.Per).ThenBy(x => x.S.Id).ThenBy(x => x.G)
            .FirstOrDefault();
        if (best.S != null) Send(town, best.S.Id, best.G);
    }

    private static string Name(Town town) => town.Content.Passage.Airship.Name;

    private static string Lower(Good g) => g.ToString().ToLowerInvariant();

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];
}
