namespace CrownAndCompass.Sim.Systems;

/// <summary>One road to victory (content/game/victory.json): its terms' numbers and the ending it writes.</summary>
public sealed record RoadDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Icon { get; init; } = "";

    /// <summary>The kind of win it is, for the window ("Diplomatic").</summary>
    public string Kind { get; init; } = "";
    public string Blurb { get; init; } = "";

    /// <summary>The end screen's title and text ({name}, {leader}, {seat}, {town}, {allies}).</summary>
    public string Title { get; init; } = "";
    public string Ending { get; init; } = "";

    /// <summary>Days in a row every term must hold.</summary>
    public int Hold { get; init; }

    // Grand Alliance: how many allies, with minor powers among them (and nobody backing the big bad).
    public int Allies { get; init; }

    // Dominion: standing that counts a settlement sworn, the share of settlements sworn; minor powers allied for both.
    public double Sworn { get; init; }
    public double Share { get; init; }
    public int Powers { get; init; }

    // Age of Legends: heroes at a fame tier, and the town's most famous company.
    public int FameTier { get; init; }
    public int Heroes { get; init; }
    public int CompanyTier { get; init; }

    // Golden Age: charter, Mandate and unrest.
    public string Tier { get; init; } = "";
    public double Mandate { get; init; }
    public double Unrest { get; init; }

    // Capital of Adventurers: share of known sites cleared, companies at home, bastions.
    public double Cleared { get; init; }
    public int Companies { get; init; }
    public int Bastions { get; init; }

    // Merchant Crown: open trade routes and a month's trade gold.
    public int Routes { get; init; }
    public double Income { get; init; }
}

public sealed record VictoryRules
{
    public List<RoadDef> Roads { get; init; } = [];
}

/// <summary>One thing a road needs, for its bar: what the town has and what it needs ("Allies 2/4").</summary>
public sealed record RoadTerm(string Label, double Have, double Need, bool AtMost = false)
{
    public bool Met => AtMost ? Have <= Need : Have >= Need;

    /// <summary>How far along it is, 0 to 1.</summary>
    public double Share => Met ? 1 : AtMost ? Math.Clamp(Need / Math.Max(Have, 0.001), 0, 1) : Need <= 0 ? 1 : Math.Clamp(Have / Need, 0, 1);
}

public sealed class VictoryState
{
    /// <summary>Roads switched off for this campaign (victory.json ids); empty for all on.</summary>
    public List<string> Off { get; } = [];

    /// <summary>The road the mayor has claimed, and the day it was claimed: its terms must hold its days from then.</summary>
    public string Claimed { get; set; } = "";
    public int ClaimDay { get; set; }

    /// <summary>The road the campaign was won by; empty until then, or when it was lost.</summary>
    public string Road { get; set; } = "";
}

/// <summary>
/// Roads to victory. Slaying the big bad is the Saga's own finale; the other roads end its threat in other ways:
/// allies who outmatch it, every hall under the town's banner, heroes too famous to stand against, a golden age
/// that drains its people, a map with nowhere left to hide, or trade that starves its host. Each road's terms are
/// read from the town every day. Once they all hold and the big bad has shown itself, the mayor may claim the road:
/// the big bad marches at once to stop it, and if the terms still hold after the road's days the campaign is won.
/// Only the mayor claims (the stand-in never does), and nothing here rolls dice.
/// </summary>
public static class Victory
{
    public const string Slay = "slay";

    public static VictoryRules Rules(Town town) => town.Content.Victory;

    public static RoadDef? Def(Town town, string id) => Rules(town).Roads.FirstOrDefault(r => r.Id == id);

    /// <summary>The roads open in this campaign, in victory.json order.</summary>
    public static IEnumerable<RoadDef> Roads(Town town) => Rules(town).Roads.Where(r => !town.Victory.Off.Contains(r.Id));

    /// <summary>The road the campaign was won by, if any.</summary>
    public static RoadDef? Won(Town town) => town.Outcome == Outcome.Victory && town.Victory.Road != "" ? Def(town, town.Victory.Road) : null;

    /// <summary>The claimed road, while its days run.</summary>
    public static RoadDef? Claimed(Town town) => town.Victory.Claimed != "" ? Def(town, town.Victory.Claimed) : null;

    /// <summary>Days the claimed road has held, and the days it still needs.</summary>
    public static int Held(Town town) => Claimed(town) == null ? 0 : town.Day - town.Victory.ClaimDay;
    public static int DaysLeft(Town town) => Claimed(town) is { } d ? Math.Max(0, d.Hold - Held(town)) : 0;

    /// <summary>No road can be claimed until the big bad has been crowned.</summary>
    public static bool Ready(Town town) => town.Saga.Enabled && town.Outcome == null && Saga.BigBad(town) is { Alive: true };

    /// <summary>What the road needs today.</summary>
    public static List<RoadTerm> Terms(Town town, RoadDef d) => d.Id switch
    {
        Slay => SlayTerms(town),
        "alliance" =>
        [
            new("Allies", AllyCount(town), d.Allies),
            new("Powers allied", Systems.Powers.Allies(town).Count(), Math.Min(d.Powers, town.Powers.Count)),
            new("Backing the big bad", Backers(town), 0, AtMost: true),
        ],
        "dominion" =>
        [
            new("Settlements sworn", Sworn(town, d), Math.Ceiling(Standing(town).Count() * d.Share)),
            new("Powers allied", Systems.Powers.Allies(town).Count(), Math.Min(d.Powers, town.Powers.Count)),
        ],
        "legends" =>
        [
            new("Legendary heroes", town.Heroes.Count(a => Notoriety.FameTier(town, a.Hero!) >= d.FameTier), d.Heroes),
            new("Most famous company", Notoriety.TownFame(town), d.CompanyTier),
        ],
        "golden" =>
        [
            new("Charter", town.City.Tier, City.TierIndex(town, d.Tier)),
            new("Mandate", Js.Round(town.Mandate), d.Mandate),
            new("Unrest", Js.Round(town.Taxes.Unrest), d.Unrest, AtMost: true),
            new("Free of any yoke", town.Saga.YokeId == null ? 1 : 0, 1),
        ],
        "adventurers" =>
        [
            new("Sites cleared %", Js.Round(ClearedShare(town) * 100), Js.Round(d.Cleared * 100)),
            new("Companies at home", CompaniesHome(town), d.Companies),
            new("Bastions", town.Bastions.Count, d.Bastions),
        ],
        "merchant" =>
        [
            new("Trade routes", Trade.Open(town).Count(), d.Routes),
            new("Trade gold this month", Js.Round(TradeIncome(town)), d.Income),
        ],
        _ => [],
    };

    /// <summary>How far along the road is, 0 to 1: its furthest-behind term.</summary>
    public static double Progress(Town town, RoadDef d)
    {
        var terms = Terms(town, d);
        return terms.Count == 0 ? 0 : terms.Min(t => t.Share);
    }

    public static bool Met(Town town, RoadDef d) => d.Id != Slay && Terms(town, d) is { Count: > 0 } terms && terms.All(t => t.Met);

    /// <summary>Why the road can't be claimed now, or null when it can.</summary>
    public static string? ClaimBlocked(Town town, string id)
    {
        if (Def(town, id) is not { } d || d.Id == Slay || town.Victory.Off.Contains(id)) return "That road is not open.";
        if (town.Outcome != null) return "The campaign is over.";
        if (!Ready(town)) return "The big bad has not shown itself.";
        if (Claimed(town) != null) return $"{Claimed(town)!.Name} is already claimed.";
        if (!Met(town, d)) return "Not every term holds yet.";
        return null;
    }

    /// <summary>The mayor claims a road: the big bad marches to stop it, and the terms must hold for the road's days.</summary>
    public static bool Claim(Town town, string id)
    {
        if (ClaimBlocked(town, id) != null) return false;
        var d = Def(town, id)!;
        var m = Saga.BigBad(town)!;
        town.Victory.Claimed = d.Id;
        town.Victory.ClaimDay = town.Day;
        Menaces.Provoke(town, m);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, $"{town.Name} claims the road of {d.Name}. Word reaches {{leader}}, and {{name}} gathers everything it has to stop it. Hold for {d.Hold} days.", m));
        return true;
    }

    /// <summary>End of day: a claimed road whose terms broke lapses; one that held its days wins the campaign.</summary>
    public static void Daily(Town town)
    {
        if (!town.Saga.Enabled || town.Outcome != null || Claimed(town) is not { } d) return;
        if (!Ready(town) || !Met(town, d))
        {
            town.Victory.Claimed = "";
            if (town.Outcome == null) town.Log(LedgerCategory.War, Importance.Major, $"The road of {d.Name} slips away: its terms no longer hold. It may be claimed again once they do.");
            return;
        }
        if (Held(town) >= d.Hold) Win(town, d);
    }

    /// <summary>The campaign is won by this road: the big bad's threat ends its way.</summary>
    public static void Win(Town town, RoadDef d)
    {
        town.Victory.Road = d.Id;
        town.Victory.Claimed = "";
        if (d.Id == Slay) return;
        var m = Saga.BigBad(town)!;
        if (Endless.On(town))
        {
            town.Victory.Road = "";
            Endless.EraWon(town, d.Id, m);
            return;
        }
        town.Outcome = Outcome.Victory;
        town.Mandate = Math.Min(100, town.Mandate + 25);
        town.Saga.EndText = Saga.Fill(town, d.Ending, m).Replace("{allies}", AllyCount(town).ToString());
        town.Log(LedgerCategory.War, Importance.Major, town.Saga.EndText);
    }

    /// <summary>The end screen's title for a road other than slaying, or null.</summary>
    public static string? Title(Town town) => Won(town) is { Id: not Slay } d && d.Title != "" ? d.Title : null;

    // Measures ------------------------------------------------------------------------------------

    private static List<RoadTerm> SlayTerms(Town town)
    {
        var m = Saga.BigBad(town);
        return
        [
            new("The big bad has shown itself", m == null ? 0 : 1, 1),
            new("Champions' level", Saga.ChampionLevel(town), m?.Level ?? town.Content.Saga.Finale.OverlordLevel[0]),
        ];
    }

    /// <summary>Allied settlements and minor powers.</summary>
    public static int AllyCount(Town town) => town.Settlements.Count(s => s.Allied && !s.Razed) + Systems.Powers.Allies(town).Count();

    /// <summary>Who stands with the big bad: the minor powers it has bought and the settlements under its sway.</summary>
    public static int Backers(Town town) =>
        Saga.BigBad(town) is { Alive: true } m ? Systems.Powers.Backing(town, m).Count() + m.Domain : 0;

    private static IEnumerable<Settlement> Standing(Town town) => town.Settlements.Where(s => !s.Razed);

    private static int Sworn(Town town, RoadDef d) => Standing(town).Count(s => s.Allied && s.Standing >= d.Sworn && !Shadows.Shadowed(s));

    private static double ClearedShare(Town town)
    {
        var known = town.Sites.Where(s => s.Found).ToList();
        return known.Count == 0 ? 0 : (double)known.Count(s => s.Cleared) / known.Count;
    }

    private static int CompaniesHome(Town town) =>
        town.Companies.Count(c => town.Heroes.Any(h => h.Hero!.PartyName == c.Name));

    private static double TradeIncome(Town town) =>
        Js.Sum(town.Trends.Days, day => Math.Max(0, day.Accounts.Get(TreasuryAccount.Trade, 0)));
}
