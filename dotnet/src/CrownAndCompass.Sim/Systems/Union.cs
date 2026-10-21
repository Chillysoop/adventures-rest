namespace CrownAndCompass.Sim.Systems;

/// <summary>Ledger lines for the union: {town}, {guilds}, {cost}, {wizard}, {hall}.</summary>
public sealed record UnionLines
{
    public string Formed { get; init; } = "";
    public string Grumble { get; init; } = "";
    public string Sulk { get; init; } = "";
    public string Concord { get; init; } = "";
    public string March { get; init; } = "";
    public string Walkout { get; init; } = "";
    public string HearGuilds { get; init; } = "";
    public string HearMages { get; init; } = "";
    public string SideGuilds { get; init; } = "";
    public string SideMages { get; init; } = "";
    public string Lapsed { get; init; } = "";
}

/// <summary>The Common Cause and the mages, and the bar between them (guilds.json "union").</summary>
public sealed record UnionRules
{
    /// <summary>What the movement calls itself.</summary>
    public string Name { get; init; } = "the Common Cause";

    /// <summary>Members it takes to stand up as a movement.</summary>
    public int Founding { get; init; } = 12;

    /// <summary>Share of guild workers, of other working townsfolk and of hedge mages who join it (each by a stable hash of who they are).</summary>
    public double GuildShare { get; init; } = 0.6;
    public double WorkerShare { get; init; } = 0.2;
    public double HedgeShare { get; init; } = 0.35;

    /// <summary>Pull toward the movement per member.</summary>
    public double PerMember { get; init; } = 0.4;

    /// <summary>How much wider the concord band grows for each hedge mage in the movement, and at most.</summary>
    public double Bridge { get; init; } = 3;
    public double BridgeMax { get; init; } = 15;

    /// <summary>Share of the gap to where the town's doings pull it that the bar closes a day.</summary>
    public double Drift { get; init; } = 0.05;

    /// <summary>Past Lean the side out of favour grumbles; past Breaking it acts; within Concord both work together.</summary>
    public double Lean { get; init; } = 25;
    public double Breaking { get; init; } = 60;
    public double Concord { get; init; } = 15;

    /// <summary>Pull toward the mages per level of research funding, per hedge mage, per level of the wizards' library's patronage, for the Unshut Book's chapter, per wizard in town.</summary>
    public double Funding { get; init; } = 8;
    public double Hedge { get; init; } = 3;
    public double Library { get; init; } = 5;
    public double Order { get; init; } = 6;
    public double Wizard { get; init; } = 2;

    /// <summary>Pull toward the movement per guild formed, and per point of the guilds' mean standing above 50.</summary>
    public double PerGuild { get; init; } = 6;
    public double Standing { get; init; } = 0.3;

    /// <summary>Standing each guild loses a day while the mages are favoured past Lean (doubled past Breaking).</summary>
    public double Drain { get; init; } = 0.4;

    /// <summary>Share of the library's lore lost while the guilds are favoured past Lean (doubled past Breaking).</summary>
    public double Sulk { get; init; } = 0.2;

    /// <summary>In concord: extra lore, and standing each guild gains a day.</summary>
    public double ConcordLore { get; init; } = 0.1;
    public double ConcordStanding { get; init; } = 0.05;

    /// <summary>Days between the slighted side's acts while past Breaking, and the Mandate the union's march costs.</summary>
    public int BreakEvery { get; init; } = 30;
    public double MarchMandate { get; init; } = 5;

    /// <summary>Hearing one side out: gold (plus per resident), how far it moves the bar, and the share of that the town forgets a day.</summary>
    public double HearCost { get; init; } = 40;
    public double HearPerResident { get; init; } = 0.2;
    public double HearShift { get; init; } = 10;
    public double Forget { get; init; } = 0.01;

    /// <summary>Taking a side outright: how long it holds, the standing it gives or takes from every guild, the Mandate it costs, and the grudge the slighted side keeps after.</summary>
    public int SideDays { get; init; } = 120;
    public double SideStanding { get; init; } = 30;
    public double SideMandate { get; init; } = 4;
    public double Grudge { get; init; } = 20;
    public double GrudgeFades { get; init; } = 0.05;

    /// <summary>The library's lore while the town has sided with the mages, or with the guilds.</summary>
    public double MagesLore { get; init; } = 1.25;
    public double GuildsLore { get; init; } = 0.5;
    public UnionLines Lines { get; init; } = new();
}

/// <summary>Where the town stands between its guilds and its mages.</summary>
public sealed class UnionState
{
    public bool Formed { get; set; }

    /// <summary>-100 (the guilds have the town hall's ear) to +100 (the mages do).</summary>
    public double Bar { get; set; }

    /// <summary>What hearing one side out has added, fading as the town forgets.</summary>
    public double Heard { get; set; }

    /// <summary>"union" or "mages" while the town has taken a side, until SideUntil; then the slighted side's grudge, fading.</summary>
    public string Side { get; set; } = "";
    public int SideUntil { get; set; }
    public double Grudge { get; set; }

    /// <summary>The day the slighted side may next act, and which way the bar last leaned past Lean (for the Ledger).</summary>
    public int NextBreak { get; set; }
    public int Leaning { get; set; }
}

/// <summary>
/// The Common Cause against the mages: a movement of working townsfolk, many from the guilds and many not (a share of
/// each trade joins, by a stable hash of who they are), and some hedge mages too, since magic users work the trades
/// as well. Once it has enough members it stands up, and a bar between it and the town's mages (the Unshut Book, the
/// library's wizards and the hedge mages outside the movement) moves with what the town hall does. Funding research,
/// the wizards' library and hedge mages pull it toward the mages; the movement's numbers, the guilds and how pleased
/// they are pull it back. Every hedge mage inside the movement widens the middle where both can live together. Kept near the middle both sides work together (more lore, guilds
/// warming); let it lean and the slighted side grumbles (guilds sour toward a strike, or the library's lore dries up),
/// and past breaking it acts (the union marches and every guild walks out; or the mages walk out and a wizard leaves
/// town). The mayor can hear one side out for gold, or take a side outright for a season, with lasting consequences:
/// siding with the movement bans hedge magic outside it and halves the library's lore, siding with the mages sends
/// every guild toward a strike, and either way the slighted side keeps a grudge. It runs only while a mayor sits at the table,
/// as the guilds do. No dice.
/// </summary>
public static class Union
{
    public static UnionRules Rules(Town town) => town.Content.Guilds.Union;

    public static bool Sided(Town town) => town.Union.Side != "" && town.Day < town.Union.SideUntil;

    /// <summary>The movement had hedge magic outside it banned.</summary>
    public static bool Banned(Town town) => Sided(town) && town.Union.Side == "union";

    /// <summary>Whether a townsperson belongs to the movement: a share of each kind of worker, by a stable hash.</summary>
    /// <param name="asMage">Ask as if they were a hedge mage (who join at their own share).</param>
    public static bool Joined(Town town, Agent a, bool asMage = false)
    {
        if (a.Hero != null || a.Departed || a.JobId == null) return false;
        var r = Rules(town);
        double share = asMage || a.HedgeSpell != "" ? r.HedgeShare : Guilds.InTrade(town, a) ? r.GuildShare : r.WorkerShare;
        return Voices.Hash("common-cause", a.Id) % 1000 < share * 1000;
    }

    /// <summary>The movement's members, in id order.</summary>
    public static List<Agent> Members(Town town) => town.Residents.Where(a => Joined(town, a)).OrderBy(a => a.Id).ToList();

    /// <summary>The middle band where both sides live together, wider for every hedge mage in the movement.</summary>
    public static double Concord(Town town)
    {
        var r = Rules(town);
        int bridges = HedgeMages.All(town).Count(a => Joined(town, a));
        return r.Concord + Math.Min(r.BridgeMax, r.Bridge * bridges);
    }

    /// <summary>What pulls the bar toward the mages, and toward the guilds, with the reasons, for the card.</summary>
    public static List<(string Why, double Pull)> Pulls(Town town)
    {
        var r = Rules(town);
        var list = new List<(string, double)>();
        if (town.Research.Funding > 0) list.Add(($"research funded ({town.Content.Research.Funding[town.Research.Funding].Name})", r.Funding * town.Research.Funding));
        int hedge = HedgeMages.All(town).Count(a => !Joined(town, a));
        if (hedge > 0) list.Add(($"{hedge} hedge {(hedge == 1 ? "mage" : "mages")} outside the movement", r.Hedge * hedge));
        if (Institutions.Find(town, "wizards-library") is { Shut: false } lib && lib.Patronage > 0) list.Add(("the wizards' library's patronage", r.Library * lib.Patronage));
        if (Factions.Find(town, "unshut-book") is { Active: true } book) list.Add(($"{book.Name}'s chapter", r.Order));
        int wizards = Research.Wizards(town).Count;
        if (wizards > 0) list.Add(($"{wizards} {(wizards == 1 ? "wizard" : "wizards")} in town", r.Wizard * wizards));
        int members = Members(town).Count;
        if (members > 0) list.Add(($"{members} {(members == 1 ? "member" : "members")} of {r.Name}", -r.PerMember * members));
        if (town.Guilds.Count > 0)
        {
            list.Add(($"{town.Guilds.Count} {(town.Guilds.Count == 1 ? "guild" : "guilds")}", -r.PerGuild * town.Guilds.Count));
            double mean = Js.Sum(town.Guilds, g => g.Standing) / town.Guilds.Count;
            if (mean > 50) list.Add(("the guilds' good temper", -r.Standing * (mean - 50)));
        }
        if (Math.Abs(town.Union.Heard) >= 0.5) list.Add((town.Union.Heard > 0 ? "the mages heard out" : "the movement heard out", town.Union.Heard));
        if (Math.Abs(town.Union.Grudge) >= 0.5) list.Add((town.Union.Grudge > 0 ? "the movement's grudge" : "the mages' grudge", town.Union.Grudge));
        return list;
    }

    /// <summary>Where the town's doings pull the bar.</summary>
    public static double Target(Town town) => Math.Clamp(Js.Sum(Pulls(town), p => p.Pull), -100, 100);

    /// <summary>"Concord", "The mages lean ahead", ...: how it stands, in words.</summary>
    public static string Mood(Town town)
    {
        var r = Rules(town);
        double b = town.Union.Bar;
        if (Sided(town)) return town.Union.Side == "union" ? "Sided with the movement" : "Sided with the mages";
        if (Math.Abs(b) <= Concord(town)) return "Concord";
        if (b >= r.Breaking) return "The movement is ready to march";
        if (b <= -r.Breaking) return "The mages are ready to walk out";
        if (b >= r.Lean) return "The movement grumbles";
        if (b <= -r.Lean) return "The mages sulk";
        return b > 0 ? "Leaning to the mages" : "Leaning to the movement";
    }

    /// <summary>The library's lore, as the town stands with its mages.</summary>
    public static double LoreFactor(Town town)
    {
        var u = town.Union;
        if (!u.Formed) return 1;
        var r = Rules(town);
        if (Sided(town)) return u.Side == "mages" ? r.MagesLore : r.GuildsLore;
        if (Math.Abs(u.Bar) <= Concord(town)) return 1 + r.ConcordLore;
        if (u.Bar <= -r.Breaking) return Math.Max(0, 1 - 2 * r.Sulk);
        if (u.Bar <= -r.Lean) return 1 - r.Sulk;
        return 1;
    }

    // ---------- the mayor's levers ----------

    public static double HearCost(Town town) => Js.Round(Rules(town).HearCost + Rules(town).HearPerResident * town.Population);

    public static string? HearBlocked(Town town)
    {
        if (!town.Union.Formed) return "no movement yet";
        if (Sided(town)) return "the town has taken a side";
        double cost = HearCost(town);
        return town.Treasury < cost ? $"needs {Js.Str(cost)} gold" : null;
    }

    /// <summary>The player's lever: hear one side out ("union" or "mages"), with gold for their grievances.</summary>
    public static bool Hear(Town town, string side)
    {
        if (side is not ("union" or "mages") || HearBlocked(town) != null) return false;
        var r = Rules(town);
        double cost = HearCost(town);
        town.Book(TreasuryAccount.Guilds, -cost);
        town.Union.Heard += side == "mages" ? r.HearShift : -r.HearShift;
        Log(town, side == "mages" ? r.Lines.HearMages : r.Lines.HearGuilds, Importance.Notable, ("cost", Js.Str(cost)));
        return true;
    }

    public static string? SideBlocked(Town town) => !town.Union.Formed ? "no movement yet" : Sided(town) ? "the town has taken a side" : null;

    /// <summary>The player's lever: take a side outright for a season, and face what the other side does about it.</summary>
    public static bool Side(Town town, string side)
    {
        if (side is not ("union" or "mages") || SideBlocked(town) != null) return false;
        var r = Rules(town);
        var u = town.Union;
        u.Side = side;
        u.SideUntil = town.Day + r.SideDays;
        u.Bar = side == "mages" ? 100 : -100;
        u.Heard = 0;
        if (side == "union")
        {
            foreach (var g in town.Guilds) g.Standing = Math.Min(100, g.Standing + r.SideStanding);
            int forgot = HedgeMages.Forget(town);
            if (Factions.Find(town, "unshut-book") is { Active: true } book) book.Standing = Math.Max(-100, book.Standing - 2 * r.SideStanding);
            u.Grudge = -r.Grudge;
            Log(town, r.Lines.SideGuilds, Importance.Major, ("hedge", forgot.ToString()));
            Walkout(town);
        }
        else
        {
            foreach (var g in town.Guilds) g.Standing = Math.Max(0, g.Standing - r.SideStanding);
            town.Mandate = Math.Clamp(town.Mandate - r.SideMandate, 0, 100);
            u.Grudge = r.Grudge;
            Log(town, r.Lines.SideMages, Importance.Major);
        }
        u.NextBreak = town.Day + r.BreakEvery;
        return true;
    }

    // ---------- the day ----------

    /// <summary>Daily: the union forms, the bar drifts toward where the town's doings pull it, and each side reacts to where it stands.</summary>
    public static void Daily(Town town, bool auto)
    {
        if (auto) return;
        var r = Rules(town);
        var u = town.Union;
        if (!u.Formed)
        {
            if (Members(town).Count < r.Founding) return;
            u.Formed = true;
            u.Bar = Target(town);
            u.NextBreak = town.Day + r.BreakEvery;
            Log(town, r.Lines.Formed, Importance.Major);
            return;
        }
        u.Heard *= 1 - r.Forget;
        u.Grudge = u.Grudge > 0 ? Math.Max(0, u.Grudge - r.GrudgeFades) : Math.Min(0, u.Grudge + r.GrudgeFades);
        if (u.Side != "")
        {
            if (Sided(town)) return;
            u.Side = "";
            Log(town, r.Lines.Lapsed, Importance.Notable);
        }
        u.Bar = Math.Clamp(u.Bar + (Target(town) - u.Bar) * r.Drift, -100, 100);
        double b = u.Bar;
        if (Math.Abs(b) <= Concord(town))
        {
            foreach (var g in town.Guilds) g.Standing = Math.Min(100, g.Standing + r.ConcordStanding);
            if (u.Leaning != 0) Log(town, r.Lines.Concord, Importance.Notable);
            u.Leaning = 0;
        }
        else if (b >= r.Lean)
        {
            double drain = b >= r.Breaking ? 2 * r.Drain : r.Drain;
            foreach (var g in town.Guilds) g.Standing = Math.Max(0, g.Standing - drain);
            if (u.Leaning != 1) Log(town, r.Lines.Grumble, Importance.Notable);
            u.Leaning = 1;
        }
        else if (b <= -r.Lean)
        {
            if (u.Leaning != -1) Log(town, r.Lines.Sulk, Importance.Notable);
            u.Leaning = -1;
        }
        if (Math.Abs(b) < r.Breaking || town.Day < u.NextBreak) return;
        u.NextBreak = town.Day + r.BreakEvery;
        if (b > 0)
        {
            // The movement marches on the town hall, and every guild downs tools together.
            foreach (var g in town.Guilds.Where(g => !Guilds.OnStrike(town, g)).ToList())
                if (Guilds.Members(town, g).Count > 0) Guilds.Strike(town, g);
            town.Mandate = Math.Clamp(town.Mandate - r.MarchMandate, 0, 100);
            Log(town, r.Lines.March, Importance.Major);
        }
        else Walkout(town);
    }

    /// <summary>The mages walk out: the least of the town's wizards leaves, and the library's studies lose ground.</summary>
    private static void Walkout(Town town)
    {
        var r = Rules(town);
        var wizard = town.Heroes.Where(a => a.Hero!.Cls.Id == "wizard" && !a.IsAway).OrderBy(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault();
        if (town.Research.Study is string id) town.Research.Progress[id] = town.Research.Progress.Get(id, 0) * 0.75;
        Log(town, r.Lines.Walkout, Importance.Major, ("wizard", wizard != null ? $"{wizard.Name} packs their books and leaves {town.Name}." : "The library's lamps go dark for a week."));
        if (wizard != null) Loyalty.Dismiss(town, wizard);
    }

    private static void Log(Town town, string line, Importance importance, params (string Key, string Value)[] extra)
    {
        if (line == "") return;
        string guilds = town.Guilds.Count switch
        {
            0 => "the guilds",
            _ => Expedition.ListNames(town.Guilds.Select(g => Guilds.Name(town, g)).ToList()),
        };
        string text = line.Replace("{town}", town.Name).Replace("{guilds}", guilds).Replace("{union}", Rules(town).Name).Replace("{members}", Members(town).Count.ToString());
        foreach (var (k, v) in extra) text = text.Replace("{" + k + "}", v);
        town.Log(LedgerCategory.Council, importance, Saga.Sentences(text));
    }
}
