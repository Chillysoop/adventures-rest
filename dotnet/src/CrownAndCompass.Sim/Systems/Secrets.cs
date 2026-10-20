namespace CrownAndCompass.Sim.Systems;

/// <summary>One kind of thing a councillor hides, and what the town makes of it when it comes out.</summary>
public sealed record SecretKind
{
    public string Id { get; init; } = "";

    /// <summary>"{name} ..." once the mayor knows it.</summary>
    public string Text { get; init; } = "";
    public double Weight { get; init; } = 1;

    /// <summary>Mandate when the mayor makes it public: a crook unmasked pleases the town, a private sin aired doesn't.</summary>
    public double Mandate { get; init; }
}

/// <summary>Councillors' secrets and what they buy the mayor (council.json "secrets").</summary>
public sealed record SecretRules
{
    /// <summary>The share of councillors who hide something worth knowing.</summary>
    public double Chance { get; init; } = 0.6;
    public IReadOnlyList<SecretKind> Kinds { get; init; } = [];

    /// <summary>Gold to set the mayor's people asking; the check is the "dig" decision, easier by one per WatchPerPoint watchmen up to WatchMax.</summary>
    public double DigCost { get; init; } = 25;
    public int WatchPerPoint { get; init; } = 4;
    public int WatchMax { get; init; } = 3;

    /// <summary>Days before the mayor can have the same councillor looked into again.</summary>
    public int DigGapDays { get; init; } = 5;

    /// <summary>Loyalty a councillor loses when they hear the mayor's people were asking, or when pressed with what they hide.</summary>
    public double Noticed { get; init; } = 10;
    public double Pressed { get; init; } = 15;

    /// <summary>Chance pressing a councillor gets out, and the Mandate it costs then.</summary>
    public double Scandal { get; init; } = 0.15;
    public double ScandalMandate { get; init; } = 4;

    /// <summary>Loyalty the other councillors lose when the mayor exposes one of them.</summary>
    public double Fear { get; init; } = 5;

    /// <summary>Chance a day a councillor's secret reaches the mayor on its own, through the taverns.</summary>
    public double Rumour { get; init; } = 0.01;

    /// <summary>Ledger lines: {name}, {seat}, {secret}, {by}.</summary>
    public string Found { get; init; } = "";
    public string Clean { get; init; } = "";
    public string Caught { get; init; } = "";
    public string Heard { get; init; } = "";
    public string Press { get; init; } = "";
    public string Leaked { get; init; } = "";
    public string Expose { get; init; } = "";
}

/// <summary>What the mayor has learned, or failed to learn, about one councillor. A blank kind: nothing to find.</summary>
public sealed class Secret
{
    public int AgentId { get; init; }
    public string Kind { get; set; } = "";
    public bool Known { get; set; }
    public bool Spent { get; set; }
    public int DugDay { get; set; } = -1000;
}

/// <summary>
/// Councillors' secrets, after Crusader Kings' hooks: many councillors hide something (debts, a lover, wagons that
/// pass the gate unsearched, a forbidden shrine, another name in another town). The mayor's people can dig for it, a
/// d20 check helped by the watch; a botched dig gets back to the councillor. A secret in hand is spent once: press the
/// councillor to vote the mayor's way on the motion before the council (no decree needed, but they resent it and it may
/// leak), or expose them and they lose the seat. A councillor in a foe's pay hides that, and exposing it unmasks them.
/// Whether a councillor hides anything is settled only when someone looks, so the stand-in mayor's towns, which never
/// look, roll no dice for it.
/// </summary>
public static class Secrets
{
    private static SecretRules Rules(Town town) => town.Content.Council.Secrets;

    public const string Foe = "foe";

    public static Secret? Of(Town town, int agentId) => town.Secrets.FirstOrDefault(s => s.AgentId == agentId);

    /// <summary>The secret the mayor holds over a councillor and hasn't spent, or null.</summary>
    public static Secret? Held(Town town, int agentId) => Of(town, agentId) is { Known: true, Spent: false } s && s.Kind != "" ? s : null;

    private static Seat? SeatOf(Town town, int agentId) => town.Seats.FirstOrDefault(s => s.HolderId == agentId);

    public static string Describe(Town town, Secret s)
    {
        var a = town.Agent(s.AgentId);
        if (s.Kind == Foe) return $"{a.Name} takes orders from {Infiltration.Of(town, a.Id)?.By ?? "a foe of the town"}";
        var k = Rules(town).Kinds.FirstOrDefault(x => x.Id == s.Kind);
        return k == null ? a.Name : k.Text.Replace("{name}", a.Name);
    }

    /// <summary>Why the mayor can't have this councillor looked into now, or null.</summary>
    public static string? DigBlocked(Town town, int agentId)
    {
        var r = Rules(town);
        if (SeatOf(town, agentId) == null) return "only a councillor";
        var s = Of(town, agentId);
        if (s is { Known: true }) return s.Kind == "" ? "nothing to find" : "already found";
        if (s != null && town.Day - s.DugDay < r.DigGapDays) return $"wait {r.DigGapDays - (town.Day - s.DugDay)} days";
        if (town.Treasury < r.DigCost) return $"needs {Js.Str(r.DigCost)} gold";
        return null;
    }

    /// <summary>The DC the mayor's people roll against, the watch making it easier.</summary>
    public static int Dc(Town town)
    {
        var r = Rules(town);
        int dc = town.Content.Stories.Decisions.TryGetValue("dig", out var d) ? d.Dc : 13;
        return dc - Math.Min(r.WatchMax, town.Watch / Math.Max(1, r.WatchPerPoint));
    }

    /// <summary>Settle once whether a councillor hides anything, and what: a foe's servant always does.</summary>
    private static Secret Decide(Town town, int agentId)
    {
        if (Of(town, agentId) is { } s) return s;
        var r = Rules(town);
        string kind = "";
        if (Infiltration.Of(town, agentId) != null) kind = Foe;
        else if (r.Kinds.Count > 0 && town.Rng.Chance(r.Chance))
        {
            double total = r.Kinds.Sum(k => k.Weight), roll = town.Rng.Next() * total;
            kind = r.Kinds[^1].Id;
            foreach (var k in r.Kinds)
            {
                if (roll < k.Weight)
                {
                    kind = k.Id;
                    break;
                }
                roll -= k.Weight;
            }
        }
        s = new Secret { AgentId = agentId, Kind = kind };
        town.Secrets.Add(s);
        return s;
    }

    private static string Fill(string text, Town town, Secret s, Seat seat) => Saga.Sentences(text
        .Replace("{secret}", Describe(town, s))
        .Replace("{name}", town.Agent(s.AgentId).Name)
        .Replace("{seat}", seat.Def.Title));

    /// <summary>The mayor pays to have a councillor looked into. Returns whether the digging went ahead.</summary>
    public static bool Dig(Town town, int agentId)
    {
        var r = Rules(town);
        if (DigBlocked(town, agentId) != null) return false;
        var seat = SeatOf(town, agentId)!;
        town.Book(TreasuryAccount.Watch, -r.DigCost);
        var s = Decide(town, agentId);
        s.DugDay = town.Day;
        var beat = Sheet.Decide(town, "dig", Dc(town));
        bool ok = beat == null || Sheet.Succeeded(beat);
        if (ok)
        {
            s.Known = true;
            string text = s.Kind == "" ? r.Clean : r.Found;
            if (text != "") town.Log(LedgerCategory.Council, Importance.Notable, Fill(text, town, s, seat), [agentId]);
            return true;
        }
        seat.Loyalty = Math.Max(0, seat.Loyalty - r.Noticed);
        if (r.Caught != "") town.Log(LedgerCategory.Council, Importance.Notable, Fill(r.Caught, town, s, seat), [agentId]);
        return true;
    }

    /// <summary>Spend a secret to have a councillor vote the mayor's way on the motion before the council.</summary>
    public static bool Press(Town town, int agentId, bool yes)
    {
        var r = Rules(town);
        var s = Held(town, agentId);
        var seat = SeatOf(town, agentId);
        var m = Council.PendingMotion(town);
        if (s == null || seat == null || m == null) return false;
        int i = m.Votes.FindIndex(v => v.Seat == seat.Def.Id);
        if (i < 0 || m.Votes[i].Yes == yes) return false;
        m.Votes[i] = m.Votes[i] with { Yes = yes };
        s.Spent = true;
        seat.Loyalty = Math.Max(0, seat.Loyalty - r.Pressed);
        if (r.Press != "") town.Log(LedgerCategory.Council, Importance.Notable, Fill(r.Press.Replace("{vote}", yes ? "aye" : "nay"), town, s, seat), [agentId]);
        if (town.Rng.Chance(r.Scandal))
        {
            town.Mandate = Math.Max(0, town.Mandate - r.ScandalMandate);
            if (r.Leaked != "") town.Log(LedgerCategory.Council, Importance.Major, Fill(r.Leaked, town, s, seat), [agentId]);
        }
        return true;
    }

    /// <summary>Make a councillor's secret public: they lose their seat (a foe's servant is unmasked), and the rest of the council takes note.</summary>
    public static bool Expose(Town town, int agentId)
    {
        var r = Rules(town);
        var s = Held(town, agentId);
        var seat = SeatOf(town, agentId);
        if (s == null || seat == null) return false;
        s.Spent = true;
        if (s.Kind == Foe && Infiltration.Of(town, agentId) != null) return Infiltration.Accuse(town, agentId);
        var k = r.Kinds.FirstOrDefault(x => x.Id == s.Kind);
        if (r.Expose != "") town.Log(LedgerCategory.Council, Importance.Major, Fill(r.Expose, town, s, seat), [agentId]);
        town.Mandate = Math.Clamp(town.Mandate + (k?.Mandate ?? 0), 0, 100);
        seat.HolderId = null;
        foreach (var other in town.Seats) if (other.HolderId != null) other.Loyalty = Math.Max(0, other.Loyalty - r.Fear);
        return true;
    }

    /// <summary>Old secrets go with the councillors who kept them, and now and then the taverns hand the mayor one for free.</summary>
    public static void Daily(Town town, bool auto)
    {
        town.Secrets.RemoveAll(s => SeatOf(town, s.AgentId) == null);
        if (auto) return;
        var r = Rules(town);
        foreach (var seat in town.Seats)
        {
            if (seat.HolderId is not int h || Of(town, h) is { Known: true }) continue;
            if (!town.Rng.Chance(r.Rumour)) continue;
            var s = Decide(town, h);
            if (s.Kind == "") continue;
            s.Known = true;
            if (r.Heard != "") town.Log(LedgerCategory.Council, Importance.Notable, Fill(r.Heard, town, s, seat), [h]);
        }
    }
}
