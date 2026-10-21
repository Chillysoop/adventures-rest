namespace CrownAndCompass.Sim.Systems;

/// <summary>How a foe's servant on the council reads in the Ledger, by the foe's kind.</summary>
public sealed record InfiltrationKindDef
{
    /// <summary>A councillor is won over (a hint, not a proof): {victim}.</summary>
    public string Turn { get; init; } = "";

    /// <summary>The watch stops the approach: {victim}.</summary>
    public string Caught { get; init; } = "";
}

/// <summary>What the town gets or loses when a councillor is named a traitor.</summary>
public sealed record InfiltrationOutcome
{
    public double Mandate { get; init; }
    public double Heat { get; init; }
    public double Loyalty { get; init; }
}

/// <summary>Revenge by politics (crises.json "infiltration").</summary>
public sealed record InfiltrationRules
{
    /// <summary>The dullest wits that try it, and the share of a grudge's strikes spent on it while no servant sits.</summary>
    public string MinCunning { get; init; } = "cunning";
    public double Chance { get; init; } = 0.5;

    /// <summary>The watch stops an approach with this share of its usual chance against agents.</summary>
    public double Caught { get; init; } = 0.5;
    public int[] EveryDays { get; init; } = [5, 9];

    /// <summary>Suspicion a servant gathers each day (times the watch, plus a half) and with each act.</summary>
    public double Daily { get; init; } = 0.8;
    public double PerAct { get; init; } = 6;

    /// <summary>Suspicion at which the taverns name two councillors, and at which the watch has proof.</summary>
    public double Whisper { get; init; } = 35;
    public double Proof { get; init; } = 100;

    /// <summary>A steward's skim: this share of the treasury, at most SkimCap, half of it feeding the foe's host.</summary>
    public double Skim { get; init; } = 0.05;
    public double SkimCap { get; init; } = 60;
    public double PowerPerGold { get; init; } = 0.5;

    /// <summary>Watchmen a captain sends home, and Mandate a speaker's slander costs.</summary>
    public int Dismiss { get; init; } = 2;
    public double Mandate { get; init; } = 3;
    public InfiltrationOutcome Unmasked { get; init; } = new();
    public InfiltrationOutcome Wrong { get; init; } = new();

    /// <summary>Share of its suspicion a servant keeps when the mayor names the wrong one: it lies low.</summary>
    public double LieLow { get; init; } = 0.5;

    public string Whispers { get; init; } = "";
    public string Proven { get; init; } = "";
    public string Accused { get; init; } = "";
    public string Innocent { get; init; } = "";
    public IReadOnlyDictionary<string, string> Acts { get; init; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, InfiltrationKindDef> Kinds { get; init; } = new Dictionary<string, InfiltrationKindDef>();

    /// <summary>Foes that win a councillor by enchantment, not coin: a charmed councillor is a victim, freed when found out.</summary>
    public IReadOnlyList<string> CharmKinds { get; init; } = [];

    /// <summary>Decrees or callings that ward the chamber, and how much likelier they make a charm's failure.</summary>
    public IReadOnlyList<string> Wards { get; init; } = [];
    public double Warded { get; init; } = 3;

    /// <summary>Callings that hear lies, and how much faster a servant's suspicion grows while one is held.</summary>
    public IReadOnlyList<string> Seers { get; init; } = [];
    public double Seen { get; init; } = 2;

    /// <summary>A charm caught at the warded door: {victim}, {by}.</summary>
    public string WardFlared { get; init; } = "";

    /// <summary>A charmed councillor found out and freed, keeping the seat: {victim}, {by}, {seat}.</summary>
    public string Freed { get; init; } = "";
}

/// <summary>A councillor secretly serving a foe.</summary>
public sealed class Mole
{
    public int Id { get; init; }
    public int AgentId { get; init; }

    /// <summary>The grudge it serves (<see cref="Grudge.Foe"/>).</summary>
    public string Foe { get; init; } = "";
    public string By { get; init; } = "";
    public int Since { get; init; }
    public double Suspicion { get; set; }
    public int NextDay { get; set; }

    /// <summary>The innocent councillor the taverns name alongside it, once they talk.</summary>
    public int? DecoyId { get; set; }

    /// <summary>Won by enchantment, not coin: freed rather than driven out when found.</summary>
    public bool Charmed { get; init; }
}

/// <summary>
/// Revenge by politics: a cunning foe with a grudge (a vampire, a coven, a wyrm in a man's shape, a rival's purse, a
/// cult) wins over a councillor instead of sending a knife. The servant steers the town from inside: votes against
/// the watch and the knights, skims the treasury into its master's war chest, sends the watch home, slanders the
/// mayor. Every act raises suspicion; past a line the taverns name two councillors and the mayor can name one
/// before the council (right, and the servant flees; wrong, and an honest councillor resigns). Left long enough,
/// the watch finds the proof itself.
/// </summary>
public static class Infiltration
{
    private static InfiltrationRules Rules(Town town) => town.Content.Crises.Infiltration;

    public static Mole? Of(Town town, int agentId) => town.Moles.FirstOrDefault(m => m.AgentId == agentId);

    /// <summary>A servant the taverns are talking about, with the two names they give, in order.</summary>
    public static IEnumerable<(Mole Mole, Agent A, Agent B)> Whispered(Town town)
    {
        foreach (var m in town.Moles.OrderBy(m => m.Id))
        {
            if (m.DecoyId is not int decoy) continue;
            var pair = new[] { town.Agent(m.AgentId), town.Agent(decoy) }.OrderBy(a => a.Name, StringComparer.Ordinal).ThenBy(a => a.Id).ToList();
            yield return (m, pair[0], pair[1]);
        }
    }

    private static Seat? SeatOf(Town town, int agentId) => town.Seats.FirstOrDefault(s => s.HolderId == agentId);

    /// <summary>The council chamber is warded against charms (a decree in force or a warder in the calling).</summary>
    public static bool Warded(Town town) =>
        Rules(town).Wards.Any(w => Decrees.InForce(town, w) || Callings.Holder(town, w) != null);

    /// <summary>Someone sits by the council door who hears lies.</summary>
    public static bool Seen(Town town) => Rules(town).Seers.Any(w => Callings.Holder(town, w) != null);

    /// <summary>A grudge's strike spent winning over a councillor. Returns whether it tried.</summary>
    public static bool Try(Town town, Grudge g)
    {
        var r = Rules(town);
        if (!r.Kinds.TryGetValue(g.Kind, out var k)) return false;
        if (Menaces.CunningRank(town, g.Wits) < Menaces.CunningRank(town, r.MinCunning)) return false;
        if (town.Moles.Any(m => m.Foe == g.Foe) || !town.Rng.Chance(r.Chance)) return false;
        var marks = town.Seats
            .Where(s => s.HolderId is int h && Of(town, h) == null && !town.Agent(h).Dead)
            .OrderBy(s => s.Loyalty)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .ToList();
        if (marks.Count == 0) return false;
        // The least loyal is the likeliest, but not always.
        var seat = marks[town.Rng.Chance(0.6) ? 0 : town.Rng.Range(0, marks.Count)];
        var victim = town.Agent(seat.HolderId!.Value);
        string name = $"{victim.Name}, the town's {seat.Def.Title.ToLowerInvariant()}";
        bool charm = r.CharmKinds.Contains(g.Kind);
        bool warded = charm && Warded(town);
        double caught = r.Caught * Council.WatchStrength(town) / Math.Max(0.5, g.Cunning);
        if (warded) caught = Math.Max(caught, 0.3) * r.Warded;
        if (town.Rng.Chance(Math.Min(warded ? 0.95 : 0.9, caught)))
        {
            string text = warded && r.WardFlared != "" ? r.WardFlared.Replace("{victim}", name).Replace("{by}", g.Name) : k.Caught.Replace("{victim}", name);
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Sentences(text), [victim.Id]);
            return true;
        }
        town.Moles.Add(new Mole
        {
            Id = town.Moles.Count == 0 ? 1 : town.Moles.Max(m => m.Id) + 1,
            AgentId = victim.Id,
            Foe = g.Foe,
            By = g.Name,
            Since = town.Day,
            NextDay = town.Day + town.Rng.Range(r.EveryDays[0], r.EveryDays[^1] + 1),
            Charmed = charm,
        });
        // A hint, not a proof: the Ledger doesn't say whose.
        town.Log(LedgerCategory.Council, Importance.Notable, Saga.Sentences(k.Turn.Replace("{victim}", name)), [victim.Id]);
        return true;
    }

    public static void Daily(Town town)
    {
        var r = Rules(town);
        foreach (var m in town.Moles.OrderBy(m => m.Id).ToList())
        {
            var a = town.Agent(m.AgentId);
            var seat = SeatOf(town, m.AgentId);
            // Dead, gone or out of office: of no more use to its master.
            if (a.Dead || a.Departed || seat == null || Gone(town, m.Foe))
            {
                town.Moles.Remove(m);
                continue;
            }
            m.Suspicion += r.Daily * (0.5 + Council.WatchStrength(town)) * (Seen(town) ? r.Seen : 1);
            if (m.DecoyId is int d && (SeatOf(town, d) == null || town.Agent(d).Dead || town.Agent(d).Departed)) m.DecoyId = null;
            if (m.DecoyId == null && m.Suspicion >= r.Whisper) Whisper(town, m);
            if (m.Suspicion >= r.Proof)
            {
                Expose(town, m, seat, r.Proven);
                continue;
            }
            if (town.Day < m.NextDay) continue;
            m.NextDay = town.Day + town.Rng.Range(r.EveryDays[0], r.EveryDays[^1] + 1);
            Act(town, m, a, seat);
        }
    }

    /// <summary>Whether the foe a servant answers to is gone (a dead menace).</summary>
    private static bool Gone(Town town, string foe) =>
        foe.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(foe[7..], out int id) && id >= 1 && id <= town.Saga.Menaces.Count && !town.Saga.Menaces[id - 1].Alive;

    private static Menace? Master(Town town, Mole m) =>
        m.Foe.StartsWith("menace:", StringComparison.Ordinal) && int.TryParse(m.Foe[7..], out int id) && id >= 1 && id <= town.Saga.Menaces.Count ? town.Saga.Menaces[id - 1] : null;

    private static void Whisper(Town town, Mole m)
    {
        var others = town.Seats.Where(s => s.HolderId is int h && h != m.AgentId && !town.Agent(h).Dead).OrderBy(s => s.Def.Id, StringComparer.Ordinal).ToList();
        if (others.Count == 0) return;
        m.DecoyId = others[town.Rng.Range(0, others.Count)].HolderId;
        var pair = Whispered(town).First(x => x.Mole == m);
        town.Log(LedgerCategory.Council, Importance.Major, Saga.Sentences(Rules(town).Whispers.Replace("{by}", m.By).Replace("{a}", pair.A.Name).Replace("{b}", pair.B.Name)), [pair.A.Id, pair.B.Id]);
    }

    /// <summary>The servant steers its portfolio toward its master's ends.</summary>
    private static void Act(Town town, Mole m, Agent a, Seat seat)
    {
        var r = Rules(town);
        string text;
        if (seat.Def.Id == SeatId.Steward)
        {
            double gold = Math.Floor(Math.Min(r.SkimCap, Math.Max(0, town.Treasury) * r.Skim));
            if (gold <= 0) return;
            town.Book(TreasuryAccount.Discrepancies, -gold);
            if (Master(town, m) is { Alive: true } master) master.Power += gold * r.PowerPerGold;
            text = r.Acts.GetValueOrDefault("skim", "").Replace("{gold}", Js.Str(gold));
        }
        else if (seat.Def.Id == SeatId.Captain && town.Watch > 0)
        {
            int n = Math.Min(town.Watch, r.Dismiss);
            town.Watch -= n;
            text = r.Acts.GetValueOrDefault("dismiss", "").Replace("{n}", Js.Str(n));
        }
        else
        {
            town.Mandate = Math.Max(0, town.Mandate - r.Mandate);
            town.Tension += 5;
            text = r.Acts.GetValueOrDefault("slander", "");
        }
        m.Suspicion += r.PerAct;
        if (text != "")
            town.Log(LedgerCategory.Council, Importance.Routine, Saga.Sentences(text.Replace("{victim}", a.Name).Replace("{seat}", seat.Def.Title.ToLowerInvariant())), [a.Id]);
    }

    /// <summary>How a servant votes on a motion, or null to vote its own mind: against the watch and the knights, for sending the watch home.</summary>
    public static bool? Vote(Town town, int agentId, Motion motion)
    {
        if (Of(town, agentId) == null) return null;
        return motion.Kind switch
        {
            MotionKind.HireWatch or MotionKind.Knight => false,
            MotionKind.DismissWatch => true,
            _ => null,
        };
    }

    /// <summary>A steward in a foe's pay moves to send the watch home whenever there is one.</summary>
    internal static bool Steers(Town town, Seat s) =>
        s.HolderId is int h && Of(town, h) != null && s.Def.Id == SeatId.Steward && town.Watch > 0;

    /// <summary>The mayor names a councillor before the council as a foe's servant.</summary>
    public static bool Accuse(Town town, int agentId)
    {
        var r = Rules(town);
        var seat = SeatOf(town, agentId);
        if (seat == null) return false;
        var named = town.Moles.Where(m => m.AgentId == agentId || m.DecoyId == agentId).OrderBy(m => m.Id).ToList();
        if (named.Count == 0) return false;
        var mole = named.FirstOrDefault(m => m.AgentId == agentId);
        if (mole != null)
        {
            town.Mandate = Math.Min(100, town.Mandate + r.Unmasked.Mandate);
            Expose(town, mole, seat, r.Accused);
            return true;
        }
        // An honest councillor named: they go, the council resents it, and the real servant lies low.
        var a = town.Agent(agentId);
        var real = named[0];
        town.Log(LedgerCategory.Council, Importance.Major, Saga.Sentences(r.Innocent.Replace("{victim}", a.Name).Replace("{by}", real.By)), [a.Id]);
        town.Mandate = Math.Max(0, town.Mandate - r.Wrong.Mandate);
        foreach (var s in town.Seats) if (s.HolderId != null && s != seat) s.Loyalty = Math.Max(0, s.Loyalty - r.Wrong.Loyalty);
        seat.HolderId = null;
        foreach (var m in named)
        {
            m.DecoyId = null;
            m.Suspicion *= r.LieLow;
        }
        return true;
    }

    private static void Expose(Town town, Mole m, Seat seat, string text)
    {
        var r = Rules(town);
        var a = town.Agent(m.AgentId);
        if (m.Charmed && r.Freed != "")
        {
            // A charm broken: the councillor was a victim, and keeps the seat.
            town.Log(LedgerCategory.Council, Importance.Major, Saga.Sentences(r.Freed.Replace("{victim}", a.Name).Replace("{by}", m.By).Replace("{seat}", seat.Def.Title.ToLowerInvariant())), [a.Id]);
            town.Moles.Remove(m);
            if (Vengeance.Of(town, m.Foe) is { } cg) cg.Heat += r.Unmasked.Heat;
            return;
        }
        town.Log(LedgerCategory.Council, Importance.Major, Saga.Sentences(text.Replace("{victim}", a.Name).Replace("{by}", m.By).Replace("{seat}", seat.Def.Title.ToLowerInvariant())), [a.Id]);
        seat.HolderId = null;
        town.Moles.Remove(m);
        Population.Depart(town, a);
        // Caught out, the master's grudge burns hotter.
        if (Vengeance.Of(town, m.Foe) is { } g) g.Heat += r.Unmasked.Heat;
    }
}
