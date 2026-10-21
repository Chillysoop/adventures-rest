namespace CrownAndCompass.Sim.Systems;

/// <summary>How the town's allies answer a call to arms (army.json "allies").</summary>
public sealed record AllyRules
{
    /// <summary>Hours before an ally's answer comes back, low and high.</summary>
    public int[] AnswerHours { get; init; } = [4, 16];

    /// <summary>A host's strength: a base, more for its prosperity, times its people's kind of place (hold, enclave, village).</summary>
    public double HostBase { get; init; } = 30;
    public double HostPerProsperity { get; init; } = 0.8;
    public IReadOnlyDictionary<string, double> Kinds { get; init; } = new Dictionary<string, double>();

    /// <summary>Days a host marches for each day its region lies from town, and the least.</summary>
    public double MarchPerDay { get; init; } = 1;
    public int MarchLeast { get; init; } = 1;

    /// <summary>Days a host stays once there, with no foe at the gates, before it goes home.</summary>
    public int Stay { get; init; } = 10;

    /// <summary>The chance an ally comes: a base, more for its standing (per point over 0), the ruler's readiness to help, less when its own lands are unsafe.</summary>
    public double Come { get; init; } = 0.5;
    public double ComePerStanding { get; init; } = 0.006;
    public double ComeRelief { get; init; } = 0.15;
    public double ComeUnsafe { get; init; } = 0.004;

    /// <summary>Of those that don't come, the share that hold out for a price rather than refuse.</summary>
    public double Holdout { get; init; } = 0.7;

    /// <summary>Gold a holdout asks for each point of its host.</summary>
    public double BribePerStrength { get; init; } = 4;

    /// <summary>The Intimidation DC to threaten a holdout into marching, and more for its standing below 50; standing lost either way, more on a failure.</summary>
    public int ThreatenDc { get; init; } = 13;
    public double ThreatenPerStanding { get; init; } = 0.1;
    public double ThreatenCost { get; init; } = 12;
    public double ThreatenFailCost { get; init; } = 30;

    /// <summary>Standing each call costs (asking is a favour), and what a host that fought and won brings back.</summary>
    public double CallCost { get; init; } = 3;
    public double Fought { get; init; } = 6;

    /// <summary>Shadow depth past which an ally may already be the enemy's, and how likely per point past it; a puppet ruler always is.</summary>
    public double TurnFrom { get; init; } = 40;
    public double TurnPerDepth { get; init; } = 0.012;

    /// <summary>Days before the town may call the same ally again.</summary>
    public int Again { get; init; } = 20;
}

public enum CallState
{
    /// <summary>The rider is out; the answer comes back within the day.</summary>
    Asked,

    /// <summary>They'll come for a price, or not at all: bribe, threaten or let it go.</summary>
    Holdout,
    Refused,
    Marching,

    /// <summary>Camped outside the gate, on the walls in a siege.</summary>
    Arrived,
    Home,

    /// <summary>They were the enemy's all along, and went over at the walls.</summary>
    Turned,
}

/// <summary>A call to arms sent to an ally, and the host it brings.</summary>
public sealed class AllyCall
{
    public int Id { get; init; }
    public int SettlementId { get; init; }
    public CallState State { get; set; }
    public int AskedTick { get; init; }
    public int AnswerTick { get; set; }
    public double Strength { get; set; }

    /// <summary>When the host reaches the town, and the day it leaves for home.</summary>
    public int ArriveTick { get; set; }
    public int LeaveDay { get; set; }

    /// <summary>Gold a holdout asks.</summary>
    public double Price { get; set; }

    /// <summary>Already in the enemy's pocket: they march, and go over to the foe at the walls. Hidden from the client.</summary>
    public bool Turncoat { get; set; }
}

/// <summary>
/// Calling the town's allies to arms. The mayor sends riders to one ally, some or all; each answers within the day.
/// Most friends come (more the better they stand, the readier their ruler, the safer their own lands); others hold out
/// for gold, which the mayor can pay, threaten them over (an Intimidation check that costs standing either way), or let
/// go; a few refuse. A host that comes must march the days its region lies from town, so a call sent late arrives late.
/// And a people under a foe's shadow, or a puppet ruler, may be the foe's already: they march, camp at the gate, and go
/// over to the enemy when the walls are hit.
/// </summary>
public static class AllyCalls
{
    public static AllyRules Rules(Town town) => town.Content.Army.Allies;

    public static Settlement Of(Town town, AllyCall c) => town.Settlements.First(s => s.Id == c.SettlementId);

    /// <summary>The allies the town could call: allied, standing and not paying a foe's tribute.</summary>
    public static List<Settlement> Callable(Town town) =>
        town.Settlements.Where(s => s.Allied && !s.Razed).OrderBy(s => town.Regions[s.RegionId].Days).ThenBy(s => s.Id).ToList();

    /// <summary>The live call to an ally (asked, holding out, marching or arrived), if any.</summary>
    public static AllyCall? Live(Town town, Settlement s) =>
        town.Army.Calls.LastOrDefault(c => c.SettlementId == s.Id && c.State is CallState.Asked or CallState.Holdout or CallState.Marching or CallState.Arrived);

    /// <summary>The host an ally could send.</summary>
    public static double Host(Town town, Settlement s)
    {
        var r = Rules(town);
        return Js.Round((r.HostBase + s.Prosperity * r.HostPerProsperity) * r.Kinds.GetValueOrDefault(s.Kin.Kind, 1));
    }

    /// <summary>Days a host from the settlement takes to reach the town.</summary>
    public static int MarchDays(Town town, Settlement s) =>
        Math.Max(Rules(town).MarchLeast, (int)Math.Ceiling(town.Regions[s.RegionId].Days * Rules(town).MarchPerDay) + Seasons.MarchDays(town));

    /// <summary>Why the town can't call this ally now, or null.</summary>
    public static string? CallBlocked(Town town, Settlement s)
    {
        if (!s.Allied || s.Razed) return "not an ally";
        if (Live(town, s) is { } c) return c.State switch { CallState.Asked => "answer on the way", CallState.Holdout => "holding out", CallState.Marching => "marching", _ => "already here" };
        var last = town.Army.Calls.LastOrDefault(c => c.SettlementId == s.Id);
        if (last != null && town.Day - last.AskedTick / SimTime.HoursPerDay < Rules(town).Again) return "called too lately";
        return null;
    }

    /// <summary>Send riders to the allies named: each answers within the day.</summary>
    public static List<AllyCall> Call(Town town, IEnumerable<int> settlementIds)
    {
        var r = Rules(town);
        var sent = new List<AllyCall>();
        foreach (int id in settlementIds.Distinct().Order())
        {
            var s = town.Settlements.FirstOrDefault(x => x.Id == id);
            if (s == null || CallBlocked(town, s) != null) continue;
            var c = new AllyCall { Id = town.Army.NextCall++, SettlementId = s.Id, State = CallState.Asked, AskedTick = town.Tick, AnswerTick = town.Tick + town.Rng.Range(r.AnswerHours[0], r.AnswerHours[1] + 1), Strength = Host(town, s) };
            town.Army.Calls.Add(c);
            s.Standing -= r.CallCost;
            sent.Add(c);
        }
        if (sent.Count > 0)
            town.Log(LedgerCategory.War, Importance.Notable, $"Riders go out to {Expedition.ListNames(sent.Select(c => Of(town, c).People).ToList())}: {town.Name} calls its allies to arms.");
        return sent;
    }

    /// <summary>The chance an ally answers yes.</summary>
    public static double ComeChance(Town town, Settlement s)
    {
        var r = Rules(town);
        return Math.Clamp(r.Come + Math.Max(0, s.Standing) * r.ComePerStanding + (Reigns.Relief(town, s) - 1) * r.ComeRelief - Math.Max(0, 50 - s.Security) * r.ComeUnsafe, 0.05, 0.95);
    }

    /// <summary>Whether an ally is the foe's already: a puppet ruler always, a people deep under a shadow maybe.</summary>
    private static bool Bought(Town town, Settlement s)
    {
        if (Reigns.Puppet(s)) return true;
        double depth = s.Shadow?.Depth ?? 0;
        var r = Rules(town);
        return depth > r.TurnFrom && town.Rng.Chance((depth - r.TurnFrom) * r.TurnPerDepth);
    }

    private static void Answer(Town town, AllyCall c)
    {
        var s = Of(town, c);
        var r = Rules(town);
        var rng = town.Rng;
        if (!s.Allied || s.Razed)
        {
            c.State = CallState.Refused;
            return;
        }
        // One that has gone over says yes most gladly of all.
        if (Bought(town, s))
        {
            c.Turncoat = true;
            March(town, c, $"{Saga.Cap(s.People)} answer at once: their host is on the road");
            return;
        }
        if (rng.Chance(ComeChance(town, s)))
        {
            March(town, c, $"{Saga.Cap(s.People)} answer the call: their host is on the road");
            return;
        }
        if (rng.Chance(r.Holdout))
        {
            c.State = CallState.Holdout;
            c.Price = Js.Round(c.Strength * r.BribePerStrength / 10) * 10;
            town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(s.People)} hold back. {Saga.Cap(s.Leader != "" ? s.Leader : "Their ruler")} will send {Js.Str(c.Strength)} only for {Js.Str(c.Price)} gold, or under a harder word.");
            return;
        }
        c.State = CallState.Refused;
        town.Log(LedgerCategory.War, Importance.Notable, $"{Saga.Cap(s.People)} refuse the call: they have their own walls to man.");
    }

    private static void March(Town town, AllyCall c, string said)
    {
        var s = Of(town, c);
        int days = MarchDays(town, s);
        c.State = CallState.Marching;
        c.ArriveTick = town.Tick + days * SimTime.HoursPerDay;
        town.Log(LedgerCategory.War, Importance.Major, $"{said}, some {Js.Str(c.Strength)} strong. They will reach {town.Name} in {days} {(days == 1 ? "day" : "days")}.");
    }

    public static string? BribeBlocked(Town town, AllyCall c) =>
        c.State != CallState.Holdout ? "not holding out" : town.Treasury < c.Price ? "not enough gold" : null;

    /// <summary>Pay a holdout's price: they march.</summary>
    public static bool Bribe(Town town, int callId)
    {
        var c = town.Army.Calls.FirstOrDefault(x => x.Id == callId);
        if (c == null || BribeBlocked(town, c) != null) return false;
        town.Book(TreasuryAccount.Army, -c.Price);
        c.Turncoat = Bought(town, Of(town, c));
        March(town, c, $"The gold is counted out. {Saga.Cap(Of(town, c).People)} march");
        return true;
    }

    /// <summary>The DC to threaten a holdout.</summary>
    public static int ThreatenDc(Town town, AllyCall c) =>
        Rules(town).ThreatenDc + (int)Math.Round(Math.Max(0, 50 - Of(town, c).Standing) * Rules(town).ThreatenPerStanding);

    /// <summary>
    /// Threaten a holdout: the mayor's Intimidation against them. It costs standing either way; on a success they march,
    /// on a failure they keep their host and like the town much less (and may break the alliance).
    /// </summary>
    public static bool Threaten(Town town, int callId)
    {
        var c = town.Army.Calls.FirstOrDefault(x => x.Id == callId);
        if (c == null || c.State != CallState.Holdout) return false;
        var s = Of(town, c);
        var r = Rules(town);
        var beat = Sheet.Roll(town, "intimidation", "", ThreatenDc(town, c), "", false, false);
        beat.Option = "threaten";
        town.Stories.LastCheck = beat;
        town.Stories.Checks++;
        string roll = $"(Intimidation {beat.Total} against DC {beat.Dc}, {Stories.Word(beat.Result)})";
        if (Sheet.Succeeded(beat))
        {
            s.Standing -= r.ThreatenCost;
            c.Turncoat = Bought(town, s);
            March(town, c, $"The mayor's word is hard and plain {roll}. {Saga.Cap(s.People)} march, sullen");
            return true;
        }
        s.Standing -= r.ThreatenFailCost;
        c.State = CallState.Refused;
        if (s.Standing < 0)
        {
            s.Allied = false;
            town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(s.People)} will not be threatened {roll}. They send the rider back and break the alliance.");
        }
        else town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(s.People)} will not be threatened {roll}. They keep their host at home and remember it.");
        return true;
    }

    /// <summary>Let a holdout keep its host.</summary>
    public static bool LetGo(Town town, int callId)
    {
        var c = town.Army.Calls.FirstOrDefault(x => x.Id == callId);
        if (c == null || c.State != CallState.Holdout) return false;
        c.State = CallState.Refused;
        return true;
    }

    /// <summary>Strength the hosts camped at the town add at the walls (turncoats included, until the walls are hit).</summary>
    public static double Defense(Town town)
    {
        double d = 0;
        foreach (var c in town.Army.Calls) if (c.State == CallState.Arrived) d += c.Strength;
        return d;
    }

    /// <summary>
    /// At the walls, the turncoats go over: their strength comes off the defence and joins the foe. Returns what they took
    /// to the other side.
    /// </summary>
    public static double Betray(Town town, string foe, List<string> lines)
    {
        double went = 0;
        foreach (var c in town.Army.Calls.Where(c => c.State == CallState.Arrived && c.Turncoat).OrderBy(c => c.Id))
        {
            var s = Of(town, c);
            went += c.Strength;
            c.State = CallState.Turned;
            s.Allied = false;
            s.Standing = Math.Min(s.Standing, -60);
            string line = $"{Saga.Cap(s.People)} camped at the gate turn their spears on the town and go over to {foe}!";
            lines.Add(line);
            town.Log(LedgerCategory.War, Importance.Major, $"Treachery: {line}");
        }
        return went;
    }

    /// <summary>A siege fought with allies at the walls: they share the glory and the losses.</summary>
    public static void Fought(Town town, bool held, double share)
    {
        foreach (var c in town.Army.Calls.Where(c => c.State == CallState.Arrived).OrderBy(c => c.Id))
        {
            c.Strength = Math.Max(0, Js.Round(c.Strength * (1 - Math.Min(0.9, share))));
            if (held) Of(town, c).Standing += Rules(town).Fought;
        }
    }

    /// <summary>Answers come back, hosts arrive, and hosts with no foe in sight go home.</summary>
    public static void Hourly(Town town)
    {
        if (town.Army.Calls.Count == 0) return;
        foreach (var c in town.Army.Calls.OrderBy(c => c.Id))
        {
            if (c.State == CallState.Asked && town.Tick >= c.AnswerTick) Answer(town, c);
            else if (c.State == CallState.Marching && town.Tick >= c.ArriveTick)
            {
                var s = Of(town, c);
                c.State = CallState.Arrived;
                c.LeaveDay = town.Day + Rules(town).Stay;
                town.Log(LedgerCategory.War, Importance.Major, $"The host of {s.People} reaches {town.Name}, {Js.Str(c.Strength)} strong, and camps before the gate.");
            }
        }
    }

    /// <summary>Hosts stay while a foe marches on the town; with none, they go home when their days are up.</summary>
    public static void Daily(Town town)
    {
        if (town.Army.Calls.Count == 0) return;
        bool danger = town.Enemy.Stage == ThreatStage.Marching || town.Saga.Menaces.Any(m => m.Alive && m.ArrivalTick != null);
        foreach (var c in town.Army.Calls.Where(c => c.State == CallState.Arrived).OrderBy(c => c.Id))
        {
            if (danger) c.LeaveDay = Math.Max(c.LeaveDay, town.Day + 1);
            if (town.Day < c.LeaveDay && c.Strength > 0) continue;
            c.State = CallState.Home;
            town.Log(LedgerCategory.War, Importance.Notable, $"The host of {Of(town, c).People} strikes its camp and marches home.");
        }
        // Old answers are forgotten once they are long done.
        town.Army.Calls.RemoveAll(c => c.State is CallState.Refused or CallState.Home or CallState.Turned && town.Tick - c.AskedTick > 60 * SimTime.HoursPerDay);
    }
}
