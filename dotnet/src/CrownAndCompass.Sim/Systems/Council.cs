namespace CrownAndCompass.Sim.Systems;

public sealed class Seat(SeatDef def)
{
    public SeatDef Def { get; } = def;
    public int? HolderId { get; set; }

    /// <summary>Loyalty to the mayor, 0 to 100.</summary>
    public double Loyalty { get; set; } = 50;

    public int LowLoyaltyDays { get; set; }
}

public enum MotionStatus
{
    Pending,
    Passed,
    Failed,
}

public enum DecidedBy
{
    Mayor,
    Council,
}

public sealed record Vote(string Seat, bool Yes);

public sealed class Motion
{
    public int Id { get; init; }
    public required string Kind { get; init; }
    public required string Proposer { get; init; }
    public int ProposerId { get; init; }
    public required string Text { get; init; }

    /// <summary>Gold the treasury pays if it passes.</summary>
    public double Cost { get; init; }

    /// <summary>The agent the motion is about (the hero to knight), if any.</summary>
    public int? TargetId { get; init; }

    public int ProposedTick { get; init; }
    public int DeadlineTick { get; init; }
    public List<Vote> Votes { get; set; } = [];
    public MotionStatus Status { get; set; } = MotionStatus.Pending;

    /// <summary>Whether the outcome overruled the council's majority.</summary>
    public bool Decree { get; set; }

    public DecidedBy? DecidedBy { get; set; }
}

/// <summary>
/// The town council: townsfolk who hold seats, each with a portfolio, an agenda and a loyalty to
/// the mayor. Councillors propose motions, the council votes, and the mayor signs or vetoes.
/// Overruling the council is a decree: it works, but it costs Mandate and loyalty. A councillor
/// whose loyalty sinks starts working against the mayor in their own portfolio.
/// </summary>
public static class Council
{
    private const double LowLoyalty = 25;
    private const double ResignLoyalty = 10;

    public static void InitCouncil(Town town)
    {
        town.Mandate = town.Content.Council.StartingMandate;
        foreach (var def in town.Content.Council.Seats) town.Seats.Add(new Seat(def));
    }

    public static Seat? FindSeat(Town town, string id) => town.Seats.FirstOrDefault(s => s.Def.Id == id);

    public static Agent? Holder(Town town, string id)
    {
        var s = FindSeat(town, id);
        return s != null && s.HolderId != null ? town.Agent(s.HolderId.Value) : null;
    }

    /// <summary>Ability modifier of the seat's key ability: how good the holder is at the job.</summary>
    public static int Competence(Town town, Seat s) =>
        s.HolderId == null ? 0 : Rng.Modifier(town.Agent(s.HolderId.Value).Abilities[s.Def.Ability]);

    public static Motion? PendingMotion(Town town) => town.Motions.FirstOrDefault(m => m.Status == MotionStatus.Pending);

    /// <summary>
    /// A motion the council settles within the day if the mayor leaves it. The clock no longer stops for motions: its
    /// alert waits on the strip and flashes red over its last day instead.
    /// </summary>
    public static bool Closing(Town town, Motion m) => m.DeadlineTick - town.Tick <= SimTime.HoursPerDay;

    /// <summary>Whole days, at least one, until the council settles a motion the mayor leaves alone.</summary>
    public static int DaysLeft(Town town, Motion m) => Math.Max(1, (int)Math.Ceiling((m.DeadlineTick - town.Tick) / (double)SimTime.HoursPerDay));

    /// <summary>How well the watch keeps the peace, 0 to ~0.85: more watchmen, a capable and loyal captain.</summary>
    public static double WatchStrength(Town town)
    {
        var cap = FindSeat(town, SeatId.Captain);
        double effect = 1;
        if (cap != null && cap.HolderId != null)
        {
            effect += 0.1 * Competence(town, cap);
            if (cap.Loyalty < LowLoyalty) effect *= 0.5;
        }
        return Math.Min(0.85, town.Watch * 0.12 * effect * Factions.WatchFactor(town) * Planning.WatchFactor(town) + Clockwork.Catch(town));
    }

    /// <summary>Bonus mood while a festival runs.</summary>
    /// <summary>What a festival costs the treasury: lanterns, ale and a feast for every household.</summary>
    public static double FestivalCost(Town town) => Js.Round(town.Content.Council.FestivalBase + town.Content.Council.FestivalPerResident * town.Population);

    /// <summary>Why the mayor can't call a festival now, or null.</summary>
    public static string? FestivalBlocked(Town town)
    {
        var c = town.Content.Council;
        if (town.FestivalDays > 0) return "a festival is on";
        if (town.FestivalTick is int at && town.Tick - at < c.FestivalGapDays * SimTime.HoursPerDay)
            return $"the last was {(town.Tick - at) / SimTime.HoursPerDay} days ago; the town tires of them under {c.FestivalGapDays}";
        if (town.Treasury < FestivalCost(town)) return "not enough gold";
        return null;
    }

    /// <summary>The mayor calls a festival on their own word and coin: no motion, a smaller Mandate than the council's blessing gives.</summary>
    public static bool HoldFestival(Town town)
    {
        if (FestivalBlocked(town) != null) return false;
        var c = town.Content.Council;
        double cost = FestivalCost(town);
        town.Book(TreasuryAccount.CouncilMotions, -cost);
        town.FestivalDays = c.FestivalDays;
        town.FestivalTick = town.Tick;
        town.Mandate = Clamp(town.Mandate + c.FestivalMandate, 0, 100);
        town.Log(LedgerCategory.Town, Importance.Major, $"Lanterns go up across {town.Name}: the mayor calls a festival, {Js.Str(cost)} gold of ale and bunting from the treasury.");
        return true;
    }

    public static double FestivalMood(Town town) => town.FestivalDays > 0 ? town.Content.Council.FestivalMood : 0;

    public static void Daily(Town town, bool autoDecide)
    {
        if (town.FestivalDays > 0) town.FestivalDays--;
        PayWatch(town);
        FillSeats(town);
        DriftMandate(town);
        foreach (var s in town.Seats) if (s.HolderId != null) LoyaltyDay(town, s);

        var pending = PendingMotion(town);
        if (pending != null && (autoDecide || town.Tick >= pending.DeadlineTick)) Resolve(town, pending, null, !autoDecide);
        if (PendingMotion(town) == null) Propose(town, autoDecide);
    }

    /// <summary>The player's lever: sign or veto the motion before the council.</summary>
    public static bool Decide(Town town, int motionId, bool sign)
    {
        var m = town.Motions.FirstOrDefault(x => x.Id == motionId);
        if (m == null || m.Status != MotionStatus.Pending) return false;
        Resolve(town, m, sign);
        // Carrying the hall (or fumbling it) moves the town's trust a little.
        var b = Sheet.Decide(town, "motion");
        town.Mandate = Math.Clamp(town.Mandate + Sheet.Gain(town, "motion", b) - (b?.Result == StoryResult.Fumble ? town.Content.Stories.Decisions["motion"].Gain : 0), 0, 100);
        return true;
    }

    private static bool? Majority(Motion m)
    {
        int yes = m.Votes.Count(v => v.Yes);
        int no = m.Votes.Count - yes;
        return yes == no ? null : yes > no;
    }

    private static void PayWatch(Town town)
    {
        if (town.Watch == 0) return;
        double wage = Garrison.Wages(town);
        if (town.Treasury >= wage) town.Book(TreasuryAccount.Watch, -wage);
        else
        {
            int left = (int)Math.Max(0, Math.Floor(town.Treasury / town.Content.Council.WatchWage));
            town.Book(TreasuryAccount.Watch, -left * town.Content.Council.WatchWage);
            town.Log(LedgerCategory.Council, Importance.Notable, $"With no gold for their wages, {town.Watch - left} of the watch hand in their badges.");
            town.Watch = left;
        }
    }

    private static void FillSeats(Town town)
    {
        int pop = town.Population;
        foreach (var s in town.Seats)
        {
            if (s.HolderId != null)
            {
                var h = town.Agent(s.HolderId.Value);
                if (!h.Departed) continue;
                s.HolderId = null;
                town.Log(LedgerCategory.Council, Importance.Notable, $"{h.Name} is gone from {town.Name}, and the council has no {s.Def.Title}.", [h.Id]);
            }
            if (pop < s.Def.UnlockPopulation) continue;
            var seated = new HashSet<int?>(town.Seats.Select(x => x.HolderId));
            string ab = s.Def.Ability;
            var candidate = town.Workers
                .Where(a => !seated.Contains(a.Id) && town.Tick - a.ArrivedTick >= 2 * SimTime.HoursPerDay)
                .OrderByDescending(a => Score(a, ab))
                .ThenBy(a => a.Id)
                .FirstOrDefault();
            if (candidate == null) continue;
            s.HolderId = candidate.Id;
            s.Loyalty = 45 + town.Rng.Range(0, 20);
            s.LowLoyaltyDays = 0;
            town.Log(
                LedgerCategory.Council,
                Importance.Notable,
                $"{candidate.Name}, {candidate.Describe()[(candidate.Name.Length + 2)..]}, joins the council as {s.Def.Title}, " +
                $"minding {s.Def.Portfolio}. They want {s.Def.Agenda}.",
                [candidate.Id]);
        }
    }

    private static double Score(Agent a, string ab) =>
        a.Abilities[ab] + (a.Has(Trait.Ambitious) ? 2 : 0) + (a.Has(Trait.Lazy) ? -2 : 0) + Math.Min(3, a.Gold / 100);

    private static void DriftMandate(Town town)
    {
        var res = town.Residents;
        double mood = res.Count > 0 ? Js.Sum(res, a => a.LastDayMood) / res.Count : 65;
        var speaker = FindSeat(town, SeatId.Speaker);
        double drift = (mood - 70) * 0.04 + Planning.Mandate(town) * 0.05 + Decor.Mandate(town);
        if (speaker != null && speaker.HolderId != null) drift += speaker.Loyalty >= LowLoyalty ? 0.15 * Competence(town, speaker) : -0.5;
        // Goodwill fades: a mandate above its rest has to be kept earned.
        var rules = town.Content.Council;
        drift += (rules.MandateRest - town.Mandate) * rules.MandateEase;
        town.Mandate = Clamp(town.Mandate + drift, 0, 100);
    }

    private static void LoyaltyDay(Town town, Seat s)
    {
        var h = town.Agent(s.HolderId!.Value);
        // Loyalty eases back toward neutral; a popular mayor wins people over.
        s.Loyalty = Clamp(s.Loyalty + (50 - s.Loyalty) * 0.02 + (town.Mandate - 50) * 0.01, 0, 100);
        if (s.Loyalty >= LowLoyalty)
        {
            s.LowLoyaltyDays = 0;
            return;
        }
        s.LowLoyaltyDays++;
        if (s.Loyalty < ResignLoyalty && s.LowLoyaltyDays >= 5)
        {
            town.Log(LedgerCategory.Council, Importance.Major, $"{h.Name} resigns as {s.Def.Title}, denouncing the mayor before the whole council.", [h.Id]);
            town.Mandate = Clamp(town.Mandate - 5, 0, 100);
            s.HolderId = null;
            return;
        }
        if (!town.Rng.Chance(0.15)) return;
        // A disloyal councillor turns their portfolio against the mayor, deniably.
        if (s.Def.Id == SeatId.Steward)
        {
            double take = Math.Min(40, Math.Floor(town.Treasury * 0.04));
            if (take <= 0) return;
            town.Book(TreasuryAccount.Discrepancies, -take);
            h.Gold += take;
            town.Log(LedgerCategory.Council, Importance.Routine, $"The treasury ledgers don't quite add up this week. {h.GivenName} the Steward has a new silk coat.", [h.Id]);
        }
        else if (s.Def.Id == SeatId.Speaker)
        {
            town.Mandate = Clamp(town.Mandate - 3, 0, 100);
            town.Log(LedgerCategory.Council, Importance.Routine, $"{h.Name} tells anyone who will listen that the mayor ignores the common folk.", [h.Id]);
        }
        else
        {
            town.Log(LedgerCategory.Council, Importance.Routine, $"The night watch is seen dicing in the guardhouse. Captain {h.GivenName} doesn't seem to mind.", [h.Id]);
        }
    }

    private sealed record Proposal(string Kind, double Cost, string Why, int? TargetId = null);

    private static void Propose(Town town, bool autoDecide)
    {
        var c = town.Content.Council;
        var last = town.Motions.Count > 0 ? town.Motions[^1] : null;
        if (last != null && town.Tick - last.ProposedTick < c.MotionGapDays * SimTime.HoursPerDay) return;
        foreach (var s in town.Seats)
        {
            if (s.HolderId == null) continue;
            // A steward in a foe's pay keeps moving to send the watch home.
            var p = Infiltration.Steers(town, s) ? new Proposal(MotionKind.DismissWatch, 0, "the watch costs more than the town can bear") : ProposalFor(town, s.Def.Id, autoDecide);
            if (p == null || RecentlyFailed(town, p.Kind) || !town.Rng.Chance(0.5)) continue;
            var h = town.Agent(s.HolderId.Value);
            var m = new Motion
            {
                Id = town.Motions.Count + 1,
                Kind = p.Kind,
                Proposer = s.Def.Id,
                ProposerId = h.Id,
                Text = MotionText(town, p),
                Cost = p.Cost,
                TargetId = p.TargetId,
                ProposedTick = town.Tick,
                DeadlineTick = town.Tick + c.MotionDays * SimTime.HoursPerDay,
            };
            m.Votes = CastVotes(town, m);
            town.Motions.Add(m);
            int yes = m.Votes.Count(v => v.Yes);
            town.Log(
                LedgerCategory.Council,
                autoDecide ? Importance.Routine : Importance.Notable,
                $"{h.Name} the {s.Def.Title} moves that {m.Text} ({p.Why}). The council votes {yes} to {m.Votes.Count - yes}.",
                [h.Id]);
            return;
        }
    }

    /// <summary>A motion that just failed isn't brought back for a while.</summary>
    private static bool RecentlyFailed(Town town, string kind) =>
        town.Motions.Any(m => m.Kind == kind && m.Status == MotionStatus.Failed && town.Tick - m.ProposedTick < 12 * SimTime.HoursPerDay);

    /// <summary>What a seat moves. Tax motions only come before a council that settles itself: a mayor at the table sets taxes by hand (<see cref="Taxes"/>).</summary>
    private static Proposal? ProposalFor(Town town, string id, bool taxes)
    {
        var c = town.Content.Council;
        // A week's average, so one big land sale or charter house doesn't swing the council.
        double books = town.RecentNet.Count > 0 ? Js.Sum(town.RecentNet, v => v) / town.RecentNet.Count : 0;
        double upkeep = -town.AccountsYesterday.Get(TreasuryAccount.Upkeep, 0) - town.AccountsYesterday.Get(TreasuryAccount.CountingHouse, 0) - town.AccountsYesterday.Get(TreasuryAccount.Watch, 0);
        var workers = town.Workers;
        int homeless = workers.Count(a => a.HomeId == null);
        double mood = workers.Count > 0 ? Js.Sum(workers, a => a.LastDayMood) / workers.Count : 65;
        double festival = FestivalCost(town);
        int brawls = town.Ledger.Entries.Count(e => e.Category == LedgerCategory.Brawl && town.Tick - e.Tick < 7 * SimTime.HoursPerDay);

        switch (id)
        {
            case SeatId.Steward:
            {
                // A red week only matters while the coffers couldn't ride it out: the Speaker calls the same
                // reserve full and moves taxes down again, so acting on a red week above it only ping-pongs the town.
                bool red = books < 0 && town.Treasury < upkeep * 20, underfunded = Taxes.Funding(town) < 1;
                if (taxes && (red || underfunded || town.Treasury < upkeep * 5) && town.TaxRate < c.MaxTax - 1e-9)
                    return new Proposal(MotionKind.RaiseTax, 0, red ? "the books have run red this week" : underfunded ? "the town's services cost more than it takes in" : "the treasury is thin");
                // Not below the Captain's own floor, or the two seats hire and dismiss the same watchmen by turns
                // (a watch the town truly can't pay hands in its badges by itself).
                if (red && town.Watch > 0 && town.Watch > town.Population / 30) return new Proposal(MotionKind.DismissWatch, 0, "the watch costs more than the town can bear");
                return null;
            }
            case SeatId.Captain:
                if (brawls >= 2 || town.Watch < town.Population / 30)
                    return new Proposal(MotionKind.HireWatch, 0, brawls >= 2 ? $"{brawls} brawls this week" : "the streets go unwatched");
                return KnightProposal(town);
            case SeatId.Speaker:
            {
                // One charter house at a time: moving the same motion every few days while the last one goes up
                // only interrupts the mayor.
                bool housing = town.Buildings.Any(b => b.IsSite && b.OwnerId == null && b.Def.Housing > 0);
                if (homeless >= 3 && !housing) return new Proposal(MotionKind.BuildHomes, HouseCost(town), $"{homeless} people sleep rough");
                var lastFestival = town.Motions.LastOrDefault(m => m.Kind == MotionKind.Festival && m.Status == MotionStatus.Passed);
                bool festivalDue = (lastFestival == null || town.Tick - lastFestival.ProposedTick > c.FestivalGapDays * SimTime.HoursPerDay)
                    && (town.FestivalTick == null || town.Tick - town.FestivalTick > c.FestivalGapDays * SimTime.HoursPerDay);
                if (mood < 70 && festivalDue && town.FestivalDays == 0 && town.Treasury > festival * 4)
                    return new Proposal(MotionKind.Festival, festival, "spirits are low");
                if (taxes && town.TaxRate > c.MinTax + 1e-9 && town.Treasury > upkeep * 20 && books > 0 && Taxes.Funding(town) > 1.2)
                    return new Proposal(MotionKind.LowerTax, 0, "the coffers are full while the people pay");
                return KnightProposal(town);
            }
            default:
                return null;
        }
    }

    /// <summary>A hero of renown the town has not yet honoured, put forward for knighthood.</summary>
    private static Proposal? KnightProposal(Town town)
    {
        var k = town.Content.Loyalty.Knight;
        if (Loyalty.LastKnighting(town) is int last && town.Day - last < k.GapDays) return null;
        if (town.Treasury < k.Cost * 4) return null;
        var hero = Loyalty.Candidate(town);
        if (hero == null) return null;
        return new Proposal(MotionKind.Knight, k.Cost, $"{hero.GivenName} has won {hero.Hero!.Renown} renown in the town's name", hero.Id);
    }

    private static double HouseCost(Town town)
    {
        var house = town.Content.Building("house");
        return Js.Round(house.Cost + town.Market.Quote(Good.Timber, house.Timber));
    }

    private static string Pct(double r) => $"{Js.Str(Js.Round(r * 100))}%";

    private static string MotionText(Town town, Proposal p)
    {
        var c = town.Content.Council;
        return p.Kind switch
        {
            MotionKind.RaiseTax => $"the tax rise from {Pct(town.TaxRate)} to {Pct(town.TaxRate + c.TaxStep)}",
            MotionKind.LowerTax => $"the tax fall from {Pct(town.TaxRate)} to {Pct(town.TaxRate - c.TaxStep)}",
            MotionKind.HireWatch => $"the town hire {c.WatchPerMotion} more watchmen at {Js.Str(c.WatchWage)} gold a day each",
            MotionKind.DismissWatch => $"{c.WatchPerMotion} watchmen be let go",
            MotionKind.Festival => $"the town hold a festival, at a cost of {Js.Str(p.Cost)} gold",
            MotionKind.BuildHomes => $"the town build a charter house for the homeless, at a cost of {Js.Str(p.Cost)} gold",
            MotionKind.Knight => $"{town.Agent(p.TargetId!.Value).Name} be knighted, at a cost of {Js.Str(p.Cost)} gold",
            _ => throw new ArgumentOutOfRangeException(nameof(p), p.Kind, null),
        };
    }

    private static List<Vote> CastVotes(Town town, Motion m)
    {
        var votes = new List<Vote>();
        foreach (var s in town.Seats)
        {
            if (s.HolderId == null) continue;
            if (s.Def.Id == m.Proposer)
            {
                votes.Add(new Vote(s.Def.Id, true));
                continue;
            }
            var voter = town.Agent(s.HolderId.Value);
            if (Infiltration.Vote(town, voter.Id, m) is bool bought)
            {
                votes.Add(new Vote(s.Def.Id, bought));
                continue;
            }
            double lean = town.Content.Council.Alignment[m.Kind][s.Def.Id] * 50 + voter.OpinionOf(m.ProposerId) * 0.3 + Guilds.Lean(town, s.Def.Id, m.Kind) + town.Rng.Range(-25, 25);
            votes.Add(new Vote(s.Def.Id, lean > 0));
        }
        return votes;
    }

    /// <summary>Settle a motion. <paramref name="sign"/> is the mayor's call, or null when the council's majority stands.</summary>
    private static void Resolve(Town town, Motion m, bool? sign, bool lapsed = false)
    {
        bool? council = Majority(m);
        bool passed = sign ?? council == true;
        m.Status = passed ? MotionStatus.Passed : MotionStatus.Failed;
        m.DecidedBy = sign == null ? DecidedBy.Council : DecidedBy.Mayor;
        m.Decree = sign != null && council != null && sign != council;

        var proposer = FindSeat(town, m.Proposer);
        if (sign != null)
        {
            if (proposer != null && proposer.HolderId == m.ProposerId) proposer.Loyalty = Clamp(proposer.Loyalty + (sign.Value ? 12 : -12), 0, 100);
            foreach (var v in m.Votes)
            {
                var s = FindSeat(town, v.Seat);
                if (s == null || s.HolderId == null || v.Seat == m.Proposer) continue;
                s.Loyalty = Clamp(s.Loyalty + (v.Yes == passed ? 3 : -3) - (m.Decree && v.Yes != passed ? 8 : 0), 0, 100);
            }
            if (m.Decree) town.Mandate = Clamp(town.Mandate - town.Content.Council.DecreeMandate, 0, 100);
            Guilds.Ruled(town, m.Kind, passed, $"{(passed ? "signed" : "vetoed")} the motion that {m.Text}");
        }

        string who = sign == null
            ? (lapsed ? "With the mayor silent, the council" : "The council")
            : m.Decree ? "By decree, overruling the council, the mayor" : "The mayor";
        if (!passed)
        {
            town.Log(LedgerCategory.Council, Importance.Notable, $"{who} {(sign == null ? "lets the motion fall" : "vetoes the motion")}: {m.Text}.", [m.ProposerId]);
            if (m.Kind == MotionKind.Knight && town.Agent(m.TargetId!.Value) is { Departed: false } snubbed) Loyalty.Slighted(town, snubbed);
            return;
        }
        if (m.Cost > town.Treasury)
        {
            m.Status = MotionStatus.Failed;
            town.Log(LedgerCategory.Council, Importance.Notable, $"The motion carries, but the treasury cannot pay for it: {m.Text}.", [m.ProposerId]);
            return;
        }
        town.Log(LedgerCategory.Council, Importance.Notable, $"{who} {(sign == null ? "carries the motion" : "signs the motion")}: {m.Text}.", [m.ProposerId]);
        Enact(town, m);
    }

    private static void Enact(Town town, Motion m)
    {
        var c = town.Content.Council;
        switch (m.Kind)
        {
            case MotionKind.RaiseTax:
                town.TaxRate = Math.Min(c.MaxTax, town.TaxRate + c.TaxStep);
                town.Mandate = Clamp(town.Mandate - 4, 0, 100);
                break;
            case MotionKind.LowerTax:
                town.TaxRate = Math.Max(c.MinTax, town.TaxRate - c.TaxStep);
                town.Mandate = Clamp(town.Mandate + 4, 0, 100);
                break;
            case MotionKind.HireWatch:
                town.Watch += c.WatchPerMotion;
                break;
            case MotionKind.DismissWatch:
                town.Watch = Math.Max(0, town.Watch - c.WatchPerMotion);
                break;
            case MotionKind.Festival:
                town.Book(TreasuryAccount.CouncilMotions, -m.Cost);
                town.FestivalDays = c.FestivalDays;
                town.FestivalTick = town.Tick;
                town.Mandate = Clamp(town.Mandate + 5, 0, 100);
                town.Log(LedgerCategory.Town, Importance.Major, $"Lanterns go up across {town.Name}: the town holds a festival, and the ale flows on the council's coin.");
                break;
            case MotionKind.Knight:
                if (!Loyalty.Knight(town, town.Agent(m.TargetId!.Value))) town.Log(LedgerCategory.Council, Importance.Routine, "The knighting cannot go ahead after all.");
                break;
            case MotionKind.BuildHomes:
                if (!Mayor.CommissionHousing(town, 1)) town.Log(LedgerCategory.Council, Importance.Routine, "There is nowhere to build the charter house yet.");
                break;
        }
    }

    private static double Clamp(double v, double lo, double hi) => Math.Max(lo, Math.Min(hi, v));
}
