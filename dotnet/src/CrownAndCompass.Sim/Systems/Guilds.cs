namespace CrownAndCompass.Sim.Systems;

/// <summary>One thing a guild can ask of the town hall (guilds.json "wants").</summary>
public sealed record GuildWant
{
    /// <summary>festival, tax (cut by Amount), watch (Amount more), part (of a monument) or zone (room for Amount more of the zone's buildings).</summary>
    public string Kind { get; init; } = "";
    public double Amount { get; init; }
    public Zone Zone { get; init; }

    /// <summary>"the guild wants {text}": {target}, {amount}, {monument}.</summary>
    public string Text { get; init; } = "";
}

/// <summary>One trade guild that can form in town (guilds.json "guilds").</summary>
public sealed record GuildDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>Who belongs, for the Ledger: "brewers, hop-growers, vintners and innkeepers".</summary>
    public string Trade { get; init; } = "";

    /// <summary>The humblest member, for the strike line.</summary>
    public string Member { get; init; } = "";

    /// <summary>Buildings whose workers belong.</summary>
    public IReadOnlyList<string> Trades { get; init; } = [];

    /// <summary>The council seat the guild leans on, if it is filled; otherwise the first seat that is.</summary>
    public string Seat { get; init; } = "";

    /// <summary>Workers in its trades before the guild forms.</summary>
    public int Members { get; init; } = 4;

    /// <summary>How it feels about each kind of motion: +1 for, -1 against.</summary>
    public IReadOnlyDictionary<string, double> Motions { get; init; } = new Dictionary<string, double>();

    /// <summary>What the strike looks like, after the strike line.</summary>
    public string Strike { get; init; } = "";
    public IReadOnlyList<GuildWant> Wants { get; init; } = [];
}

/// <summary>Ledger lines: {name}, {master}, {hall}, {trade}, {town}, {councillor}, {seat}, {want}, {days}, {member}, {gold}, {ruling}.</summary>
public sealed record GuildLines
{
    public string Founded { get; init; } = "";
    public string Backing { get; init; } = "";
    public string Master { get; init; } = "";
    public string Backs { get; init; } = "";
    public string Demand { get; init; } = "";
    public string Met { get; init; } = "";
    public string Lapsed { get; init; } = "";
    public string Crossed { get; init; } = "";
    public string Strike { get; init; } = "";
    public string Ends { get; init; } = "";
    public string Paid { get; init; } = "";
    public string Broken { get; init; } = "";
}

/// <summary>Trade guilds and their strikes (content/game/guilds.json).</summary>
public sealed record GuildRules
{
    /// <summary>Households before any guild forms.</summary>
    public int Population { get; init; } = 45;

    /// <summary>Standing a new guild starts at, the one it eases back toward and the share of the gap it closes a day.</summary>
    public double Start { get; init; } = 60;
    public double Rest { get; init; } = 55;
    public double Drift { get; init; } = 0.015;

    /// <summary>Gold each member pays the guild a day, from purses of at least DuesFloor.</summary>
    public double Dues { get; init; } = 0.25;
    public double DuesFloor { get; init; } = 10;

    /// <summary>How hard a guild leans on its councillor's vote, per point of its feeling about the motion.</summary>
    public double Backing { get; init; } = 30;

    /// <summary>At or above Pleased the backed councillor warms to the mayor by Loyalty a day; below Angry they cool.</summary>
    public double Pleased { get; init; } = 70;
    public double Angry { get; init; } = 30;
    public double Loyalty { get; init; } = 0.4;

    /// <summary>Days after founding before the first demand, between demands, and that a demand gives the mayor.</summary>
    public int FirstDemand { get; init; } = 6;

    /// <summary>Days later each further guild brings its first demand, so the guilds of a town take turns at the town hall.</summary>
    public int Stagger { get; init; } = 5;
    public int DemandGap { get; init; } = 24;
    public int DemandDays { get; init; } = 8;

    /// <summary>Standing for a demand met or left to lapse, and per point of feeling for a ruling on its side or against it.</summary>
    public double Met { get; init; } = 25;
    public double Lapsed { get; init; } = 25;
    public double Sided { get; init; } = 5;
    public double Crossed { get; init; } = 10;

    /// <summary>The share of a demand's cost the guild pays from its own purse, as far as the purse goes.</summary>
    public double ChipIn { get; init; } = 0.5;

    /// <summary>Gold per member to buy a guild off, and the standing it buys.</summary>
    public double PayPerMember { get; init; } = 8;
    public double PaidOff { get; init; } = 15;

    /// <summary>At or below StrikeAt the guild strikes for StrikeDays, and comes back at AfterStrike.</summary>
    public double StrikeAt { get; init; } = 10;
    public int StrikeDays { get; init; } = 5;
    public double AfterStrike { get; init; } = 40;

    /// <summary>Breaking a strike with the watch: the watchmen it takes, the Mandate it costs, the standing left and the loyalty the backed councillor loses.</summary>
    public int BreakWatch { get; init; } = 4;
    public double BreakMandate { get; init; } = 6;
    public double Broken { get; init; } = 25;
    public double BrokenLoyalty { get; init; } = 10;

    public GuildLines Lines { get; init; } = new();

    /// <summary>The guilds banded together against the mages (<see cref="Systems.Union"/>).</summary>
    public UnionRules Union { get; init; } = new();
    public IReadOnlyList<GuildDef> Guilds { get; init; } = [];
}

/// <summary>A trade guild that has formed in town.</summary>
public sealed class Guild
{
    public string Id { get; init; } = "";
    public int FoundedDay { get; init; }
    public int? MasterId { get; set; }

    /// <summary>The building it meets at: its master's workplace.</summary>
    public int? HallId { get; set; }

    /// <summary>The council seat it leans on.</summary>
    public string Seat { get; set; } = "";

    /// <summary>0 to 100: how the guild feels about the town hall.</summary>
    public double Standing { get; set; }
    public double Purse { get; set; }

    /// <summary>The kind of want it has put to the mayor ("" for none), the mark it must reach and the day it lapses.</summary>
    public string Want { get; set; } = "";
    public double Target { get; set; }
    public int Due { get; set; }
    public int NextDemand { get; set; }

    /// <summary>Day the strike ends; striking while the town's day is before it.</summary>
    public int StrikeUntil { get; set; }
}

/// <summary>
/// Trade guilds, after The Guild: once enough workers share a trade (builders and quarriers, smiths, brewers and
/// innkeepers, farmers, shopkeepers) they form a guild with a master, a hall at the master's workplace and dues. Each
/// guild leans on one council seat, pushing its councillor's vote its way and warming or souring them toward the mayor
/// as the guild is pleased or not. Now and then the master brings a demand (a festival, lower taxes, more watchmen, a
/// new part on a monument, land zoned for the trade) with a deadline. Meet it and the guild is pleased; let it lapse,
/// raise its taxes or veto what it wants and its standing falls, and at the bottom it strikes: its workers down tools
/// for days (the builders stop hauling, the forges go cold, the taverns shut). The mayor can meet the demand, pay the
/// guild off or send the watch to break the strike at a cost in Mandate.
/// Guilds only form while a mayor sits at the table: the stand-in mayor's towns keep the peace with their trades, so
/// they roll no dice and pay no dues for them.
/// </summary>
public static class Guilds
{
    public static GuildRules Rules(Town town) => town.Content.Guilds;

    public static GuildDef Def(Town town, Guild g) => Rules(town).Guilds.First(d => d.Id == g.Id);

    public static Guild? Find(Town town, string id) => town.Guilds.FirstOrDefault(g => g.Id == id);

    public static string Name(Town town, Guild g) => Def(town, g).Name;

    /// <summary>The workers of the guild's trades, in id order.</summary>
    public static List<Agent> Members(Town town, Guild g) => Members(town, Def(town, g));

    private static List<Agent> Members(Town town, GuildDef d) =>
        town.Agents.Where(a => !a.Departed && a.Hero == null && a.JobId is int j && Trade(town, j) == d.Id).OrderBy(a => a.Id).ToList();

    /// <summary>Whether a townsperson works one of the guilds' trades (formed or not).</summary>
    public static bool InTrade(Town town, Agent a) => a.JobId is int j && Trade(town, j) != null;

    /// <summary>The guild whose trade a building is, or null.</summary>
    private static string? Trade(Town town, int buildingId)
    {
        var b = town.Building(buildingId);
        if (b.IsSite) return null;
        foreach (var d in Rules(town).Guilds) if (d.Trades.Contains(b.Def.Id)) return d.Id;
        return null;
    }

    public static bool OnStrike(Town town, Guild g) => town.Day < g.StrikeUntil;

    /// <summary>Whether this worker has downed tools with their guild today.</summary>
    public static bool Striking(Town town, Agent a)
    {
        if (a.JobId is not int j || !town.Guilds.Any(g => OnStrike(town, g))) return false;
        return Trade(town, j) is string id && Find(town, id) is { } g && OnStrike(town, g);
    }

    /// <summary>The guilds leaning on a seat.</summary>
    public static IEnumerable<Guild> Backing(Town town, string seat) => town.Guilds.Where(g => g.Seat == seat);

    /// <summary>How far the guilds behind a seat push its councillor's vote on a kind of motion.</summary>
    public static double Lean(Town town, string seat, string kind) =>
        Js.Sum(Backing(town, seat).ToList(), g => Def(town, g).Motions.TryGetValue(kind, out var w) ? w * Rules(town).Backing : 0);

    // ---------- the mayor's levers ----------

    /// <summary>Gold the treasury pays to meet the guild's demand (after the guild's own share), or null when it can't be bought.</summary>
    public static double? MeetCost(Town town, Guild g)
    {
        if (Want(town, g) is not { } w) return null;
        double full = w.Kind switch
        {
            "festival" => FestivalCost(town),
            "part" => NextPart(town) is (_, var part) ? Monuments.Cost(town, part) : -1,
            "tax" or "watch" => 0,
            _ => -1,
        };
        if (full < 0) return null;
        return Js.Round(full - Math.Min(g.Purse, full * Rules(town).ChipIn));
    }

    /// <summary>Why the mayor can't meet the demand now, or null.</summary>
    public static string? MeetBlocked(Town town, Guild g)
    {
        if (Want(town, g) is not { } w) return "no demand";
        if (w.Kind == "zone") return $"paint {Zones(w.Zone)} for it";
        if (w.Kind == "festival" && town.FestivalDays > 0) return "a festival is on";
        double? cost = MeetCost(town, g);
        if (cost == null) return w.Kind == "part" ? "no part can be added" : "not now";
        if (town.Treasury < cost) return $"needs {Js.Str(cost.Value)} gold";
        return null;
    }

    private static string Zones(Zone z) => z switch
    {
        Zone.Farmland => "fields",
        Zone.Residential => "homes",
        Zone.Industry => "workshops",
        Zone.Commercial => "shops",
        _ => "land",
    };

    /// <summary>The player's lever: give the guild what it asked for.</summary>
    public static bool Meet(Town town, string id)
    {
        if (Find(town, id) is not { } g || MeetBlocked(town, g) != null) return false;
        var w = Want(town, g)!;
        double cost = MeetCost(town, g)!.Value;
        double share = w.Kind is "festival" or "part" ? Share(town, g, w) : 0;
        g.Purse -= share;
        var c = town.Content.Council;
        switch (w.Kind)
        {
            case "festival":
                town.Book(TreasuryAccount.Guilds, -cost);
                town.FestivalDays = c.FestivalDays;
                town.Mandate = Math.Clamp(town.Mandate + 5, 0, 100);
                town.Log(LedgerCategory.Town, Importance.Major, $"Lanterns go up across {town.Name}: the town holds a festival for {Name(town, g)}, and the ale flows on the council's coin.");
                break;
            case "part":
                var (core, part) = NextPart(town)!.Value;
                town.Book(TreasuryAccount.Guilds, share);
                Monuments.Add(town, core, part.Id);
                break;
            case "tax":
                Taxes.Set(town, g.Target);
                break;
            case "watch":
                town.Watch = Math.Max(town.Watch, (int)g.Target);
                town.Log(LedgerCategory.Council, Importance.Routine, $"The mayor takes on more of the watch: {town.Watch} watchmen now walk the streets.");
                break;
        }
        Settle(town, g);
        return true;
    }

    /// <summary>What the guild puts in from its own purse toward a demand that costs gold.</summary>
    private static double Share(Town town, Guild g, GuildWant w)
    {
        double full = w.Kind == "festival" ? FestivalCost(town) : NextPart(town) is (_, var part) ? Monuments.Cost(town, part) : 0;
        return Math.Min(g.Purse, full * Rules(town).ChipIn);
    }

    /// <summary>Gold to buy the guild off: its demand dropped, or its strike called off.</summary>
    public static double PayCost(Town town, Guild g) => Js.Round(Rules(town).PayPerMember * Math.Max(1, Members(town, g).Count));

    public static string? PayBlocked(Town town, Guild g)
    {
        if (g.Want == "" && !OnStrike(town, g)) return "nothing to settle";
        double cost = PayCost(town, g);
        return town.Treasury < cost ? $"needs {Js.Str(cost)} gold" : null;
    }

    /// <summary>The player's lever: pay the guild to drop its demand or call off its strike.</summary>
    public static bool PayOff(Town town, string id)
    {
        var r = Rules(town);
        if (Find(town, id) is not { } g || PayBlocked(town, g) != null) return false;
        double cost = PayCost(town, g);
        town.Book(TreasuryAccount.Guilds, -cost);
        g.Purse += cost;
        Log(town, g, r.Lines.Paid, Importance.Notable, ("gold", Js.Str(cost)));
        bool striking = OnStrike(town, g);
        g.Want = "";
        g.NextDemand = town.Day + r.DemandGap;
        g.StrikeUntil = 0;
        g.Standing = Math.Clamp(Math.Max(g.Standing, striking ? r.AfterStrike : 0) + r.PaidOff, 0, 100);
        return true;
    }

    public static string? BreakBlocked(Town town, Guild g)
    {
        var r = Rules(town);
        if (!OnStrike(town, g)) return "not on strike";
        if (town.Watch < r.BreakWatch) return $"needs {r.BreakWatch} watchmen";
        return null;
    }

    /// <summary>The player's lever: send the watch to break the strike. It works, but the town and the guild's councillor remember it.</summary>
    public static bool Break(Town town, string id)
    {
        var r = Rules(town);
        if (Find(town, id) is not { } g || BreakBlocked(town, g) != null) return false;
        g.StrikeUntil = 0;
        g.Want = "";
        g.NextDemand = town.Day + r.DemandGap;
        g.Standing = r.Broken;
        town.Mandate = Math.Clamp(town.Mandate - r.BreakMandate, 0, 100);
        if (Council.FindSeat(town, g.Seat) is { HolderId: not null } seat) seat.Loyalty = Math.Max(0, seat.Loyalty - r.BrokenLoyalty);
        Log(town, g, r.Lines.Broken, Importance.Major);
        return true;
    }

    /// <summary>The mayor ruled on something the guild cares about: signed or vetoed a motion, or set taxes.</summary>
    public static void Ruled(Town town, string kind, bool yes, string ruling)
    {
        var r = Rules(town);
        foreach (var g in town.Guilds)
        {
            if (!Def(town, g).Motions.TryGetValue(kind, out var w) || w == 0) continue;
            if (yes == w > 0) g.Standing = Math.Min(100, g.Standing + r.Sided * Math.Abs(w));
            else
            {
                g.Standing = Math.Max(0, g.Standing - r.Crossed * Math.Abs(w));
                Log(town, g, r.Lines.Crossed, Importance.Routine, ("ruling", ruling));
            }
        }
    }

    // ---------- the day ----------

    /// <summary>Daily: guilds form, pay dues, ease toward their usual temper, press their demands and strike when crossed too far.</summary>
    public static void Daily(Town town, bool auto)
    {
        if (auto) return;
        var r = Rules(town);
        if (town.Population >= r.Population) Form(town);
        foreach (var g in town.Guilds)
        {
            var members = Members(town, g);
            Lead(town, g, members);
            Lean(town, g);
            foreach (var a in members)
            {
                if (a.Gold < r.DuesFloor) continue;
                a.Gold -= r.Dues;
                g.Purse += r.Dues;
            }
            g.Standing += (r.Rest - g.Standing) * r.Drift;
            if (Council.FindSeat(town, g.Seat) is { HolderId: not null } seat)
            {
                if (g.Standing >= r.Pleased) seat.Loyalty = Math.Min(100, seat.Loyalty + r.Loyalty);
                else if (g.Standing < r.Angry) seat.Loyalty = Math.Max(0, seat.Loyalty - r.Loyalty);
            }
            if (OnStrike(town, g)) continue;
            if (g.StrikeUntil > 0 && town.Day >= g.StrikeUntil)
            {
                g.StrikeUntil = 0;
                g.Standing = r.AfterStrike;
                g.NextDemand = town.Day + r.DemandGap;
                Log(town, g, r.Lines.Ends, Importance.Notable);
                continue;
            }
            Press(town, g, members.Count);
            if (g.Standing <= r.StrikeAt && members.Count > 0) Strike(town, g);
        }
    }

    /// <summary>The guild walks out: its demand is dropped and its workers down tools for the strike's days.</summary>
    public static void Strike(Town town, Guild g)
    {
        var r = Rules(town);
        g.StrikeUntil = town.Day + r.StrikeDays;
        g.Want = "";
        g.Standing = Math.Min(g.Standing, r.StrikeAt);
        Log(town, g, r.Lines.Strike + " " + Def(town, g).Strike, Importance.Major);
    }

    private static void Form(Town town)
    {
        var r = Rules(town);
        foreach (var d in r.Guilds)
        {
            if (Find(town, d.Id) != null) continue;
            var members = Members(town, d);
            if (members.Count < d.Members) continue;
            var g = new Guild { Id = d.Id, FoundedDay = town.Day, Standing = r.Start, NextDemand = town.Day + r.FirstDemand + r.Stagger * town.Guilds.Count % (r.DemandGap + r.DemandDays) };
            town.Guilds.Add(g);
            var master = Master(town, members);
            g.MasterId = master.Id;
            g.HallId = master.JobId;
            g.Seat = SeatFor(town, d);
            string backing = Council.Holder(town, g.Seat) != null ? Fill(r.Lines.Backing, town, g, null) : "";
            Log(town, g, r.Lines.Founded.Replace("{backing}", backing), Importance.Notable, ("trade", d.Trade), ("town", town.Name));
        }
    }

    /// <summary>The guild's master: the member with the most presence and stake in the trade.</summary>
    private static Agent Master(Town town, List<Agent> members) =>
        members.OrderByDescending(a => a.Abilities.Cha + (a.Has(Trait.Ambitious) ? 2 : 0) + Math.Min(3, a.Gold / 100) + (a.JobId is int j && town.Building(j).OwnerId == a.Id ? 3 : 0)).ThenBy(a => a.Id).First();

    /// <summary>A master who left the town or the trade is replaced by the next in line.</summary>
    private static void Lead(Town town, Guild g, List<Agent> members)
    {
        if (g.MasterId is int m && members.Any(a => a.Id == m)) return;
        if (members.Count == 0)
        {
            g.MasterId = null;
            return;
        }
        var next = Master(town, members);
        g.MasterId = next.Id;
        g.HallId = next.JobId;
        Log(town, g, Rules(town).Lines.Master, Importance.Routine);
    }

    /// <summary>The seat the guild wants, once it is filled; until then the first filled seat.</summary>
    private static string SeatFor(Town town, GuildDef d)
    {
        if (Council.Holder(town, d.Seat) != null) return d.Seat;
        return town.Seats.FirstOrDefault(s => s.HolderId != null)?.Def.Id ?? d.Seat;
    }

    private static void Lean(Town town, Guild g)
    {
        string seat = SeatFor(town, Def(town, g));
        if (seat == g.Seat) return;
        g.Seat = seat;
        if (Council.Holder(town, seat) != null) Log(town, g, Rules(town).Lines.Backs, Importance.Routine);
    }

    /// <summary>Settle the guild's demand if it is met or has run out, or bring a new one when its turn comes.</summary>
    private static void Press(Town town, Guild g, int members)
    {
        var r = Rules(town);
        if (Want(town, g) is { } w)
        {
            if (Satisfied(town, g, w)) Settle(town, g);
            else if (town.Day >= g.Due)
            {
                Log(town, g, r.Lines.Lapsed, Importance.Notable, ("want", WantText(town, g, w)));
                g.Want = "";
                g.NextDemand = town.Day + r.DemandGap;
                g.Standing = Math.Max(0, g.Standing - r.Lapsed);
            }
            return;
        }
        if (town.Day < g.NextDemand || members == 0) return;
        var open = Def(town, g).Wants.Where(x => Possible(town, x)).ToList();
        if (open.Count == 0)
        {
            g.NextDemand = town.Day + r.DemandGap;
            return;
        }
        var pick = town.Rng.Pick(open);
        g.Want = pick.Kind;
        g.Target = pick.Kind switch
        {
            "tax" => Js.Round((town.TaxRate - pick.Amount) * 100) / 100,
            "watch" => town.Watch + pick.Amount,
            "part" => Parts(town) + 1,
            "zone" => Land.OpenPlots(town, pick.Zone) + pick.Amount,
            _ => 0,
        };
        g.Due = town.Day + r.DemandDays;
        Log(town, g, r.Lines.Demand, Importance.Notable, ("want", WantText(town, g, pick)), ("days", r.DemandDays.ToString()));
    }

    /// <summary>A demand met, by the mayor's lever or on its own.</summary>
    private static void Settle(Town town, Guild g)
    {
        var r = Rules(town);
        Log(town, g, r.Lines.Met, Importance.Notable);
        g.Want = "";
        g.NextDemand = town.Day + r.DemandGap;
        g.Standing = Math.Min(100, g.Standing + r.Met);
    }

    /// <summary>The want the guild is pressing, or null.</summary>
    public static GuildWant? Want(Town town, Guild g) => g.Want == "" ? null : Def(town, g).Wants.FirstOrDefault(w => w.Kind == g.Want);

    /// <summary>Whether the town can be asked for this at all now.</summary>
    private static bool Possible(Town town, GuildWant w) => w.Kind switch
    {
        "festival" => town.FestivalDays == 0,
        "tax" => town.TaxRate - w.Amount >= Taxes.Rules(town).Min - 1e-9,
        "part" => NextPart(town) != null,
        "zone" or "watch" => true,
        _ => false,
    };

    private static bool Satisfied(Town town, Guild g, GuildWant w) => w.Kind switch
    {
        "festival" => town.FestivalDays > 0,
        "tax" => town.TaxRate <= g.Target + 1e-9,
        "watch" => town.Watch >= g.Target,
        "part" => Parts(town) >= g.Target,
        "zone" => Land.OpenPlots(town, w.Zone) >= g.Target,
        _ => false,
    };

    private static int Parts(Town town) => town.Buildings.Count(b => b.ParentId != null);

    /// <summary>The monument part the town would add next: the cheapest open one of the first monument that has one.</summary>
    private static (Building Core, BuildingDef Part)? NextPart(Town town)
    {
        foreach (var core in Monuments.All(town).Where(b => b.Complete).OrderBy(b => b.Id))
            if (Monuments.Next(town, core) is { } part) return (core, part);
        return null;
    }

    private static double FestivalCost(Town town)
    {
        var c = town.Content.Council;
        return Js.Round(c.FestivalBase + c.FestivalPerResident * town.Population);
    }

    /// <summary>What the guild wants, in words: "taxes cut to 8%".</summary>
    public static string WantText(Town town, Guild g, GuildWant w) => w.Text
        .Replace("{target}", w.Kind == "tax" ? $"{Js.Str(Js.Round(g.Target * 100))}%" : Js.Str(g.Target))
        .Replace("{amount}", Js.Str(w.Amount))
        .Replace("{monument}", NextPart(town) is (var core, _) ? core.Name : "the town's monuments");

    private static string Fill(string text, Town town, Guild g, (string Key, string Value)[]? extra)
    {
        var d = Def(town, g);
        string master = g.MasterId is int m ? town.Agent(m).Name : "nobody";
        string hall = g.HallId is int h ? town.Building(h).Name : "the taverns";
        var holder = Council.Holder(town, g.Seat);
        text = text
            .Replace("{name}", d.Name)
            .Replace("{master}", master)
            .Replace("{hall}", hall)
            .Replace("{member}", d.Member)
            .Replace("{councillor}", holder?.Name ?? "nobody")
            .Replace("{seat}", Council.FindSeat(town, g.Seat)?.Def.Title ?? "council");
        foreach (var (k, v) in extra ?? []) text = text.Replace("{" + k + "}", v);
        return text;
    }

    private static void Log(Town town, Guild g, string line, Importance importance, params (string Key, string Value)[] extra)
    {
        if (line == "") return;
        var ids = new List<int>();
        if (g.MasterId is int m) ids.Add(m);
        town.Log(LedgerCategory.Council, importance, Saga.Sentences(Fill(line, town, g, extra)), ids);
    }
}
