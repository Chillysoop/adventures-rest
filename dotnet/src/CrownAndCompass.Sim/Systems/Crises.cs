namespace CrownAndCompass.Sim.Systems;

/// <summary>How bad a crisis is: how fast it comes, how often and how hard it strikes, and the host it ends with.</summary>
public sealed record CrisisSeverityDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The least score (<see cref="Crises.Score"/>) this severity covers; listed mildest first.</summary>
    public double Min { get; init; }

    /// <summary>Days of the gathering, then of the onslaught, before it culminates.</summary>
    public int[] PhaseDays { get; init; } = [40, 35];

    /// <summary>Days between blows in the onslaught (half as often while it gathers), and blows each time.</summary>
    public int[] BlowEvery { get; init; } = [15, 21];
    public int Blows { get; init; } = 1;

    /// <summary>How hard its blows are: raiders' share of its power, and how much harder its agents are to catch.</summary>
    public double Strength { get; init; } = 1;

    /// <summary>The host it marches with at the end, as a share of the town's defence.</summary>
    public double Army { get; init; } = 0.6;

    /// <summary>Dread it adds while it runs, and scars it leaves when it starts.</summary>
    public double Dread { get; init; }
    public double Scar { get; init; }

    /// <summary>Blows struck all at once the night it culminates.</summary>
    public int CulminationBlows { get; init; } = 2;
}

/// <summary>A menace kind's crisis: its name and what it says at each turn, and the blows it favours (weights by blow kind).</summary>
public sealed record CrisisKindDef
{
    public string Name { get; init; } = "";
    public string What { get; init; } = "";
    public string Start { get; init; } = "";
    public string Phase { get; init; } = "";
    public string Culminate { get; init; } = "";
    public string Averted { get; init; } = "";
    public IReadOnlyDictionary<string, double> Blows { get; init; } = new Dictionary<string, double>();
}

/// <summary>How a crisis's score is reckoned: the enemy's wits, its level and its power.</summary>
public sealed record CrisisScoreRules
{
    /// <summary>Weight by cunning tier id; a tier at 0 never has a crisis.</summary>
    public IReadOnlyDictionary<string, double> Cunning { get; init; } = new Dictionary<string, double>();
    public double PerLevel { get; init; } = 0.08;

    /// <summary>Power over the dominion threshold, clamped to this range.</summary>
    public double PowerFloor { get; init; } = 0.5;
    public double PowerCap { get; init; } = 2;

    /// <summary>The big bad's crisis is worse by this much.</summary>
    public double BigBad { get; init; } = 1.5;
}

/// <summary>Two crises' hosts meeting in the field, and what the town can do about it.</summary>
public sealed record CrisisClashRules
{
    public int[] EveryDays { get; init; } = [25, 40];
    public int NoticeDays { get; init; } = 5;

    /// <summary>How many rings apart their seats can be and still meet.</summary>
    public int Reach { get; init; } = 1;
    public double LoserLoss { get; init; } = 0.6;
    public double WinnerLoss { get; init; } = 0.25;
    public double Absorb { get; init; } = 2.5;

    /// <summary>The share of the town's defence that rides out when it joins, and townsfolk lost per so much of it.</summary>
    public double TownShare { get; init; } = 0.3;
    public double MilitiaPerDead { get; init; } = 60;

    /// <summary>Days the side the town fought for stays its hand.</summary>
    public int TruceDays { get; init; } = 20;
    public string Announce { get; init; } = "";
    public string Fought { get; init; } = "";
    public string Swallowed { get; init; } = "";
    public string Joined { get; init; } = "";
    public string Pact { get; init; } = "";
    public string Melee { get; init; } = "";
}

public sealed record CrisisEndText
{
    public string Held { get; init; } = "";
    public string Sacked { get; init; } = "";
    public string Routed { get; init; } = "";
}

public sealed record CrisisRules
{
    /// <summary>The act (0-based) from which crises can begin, and days a power must be left alone to start one.</summary>
    public int MinAct { get; init; } = 2;
    public int RipenDays { get; init; } = 30;

    /// <summary>Days of ripening each party sent against it, or each holding cleared, sets it back.</summary>
    public int DisturbDays { get; init; } = 12;

    /// <summary>Days after a crisis ends before the same enemy can begin another.</summary>
    public int CooldownDays { get; init; } = 150;
    public CrisisScoreRules Score { get; init; } = new();
    public IReadOnlyList<CrisisSeverityDef> Severities { get; init; } = [];

    /// <summary>Each other crisis under way shortens a crisis's phases by this share, and swells its host by this share.</summary>
    public double Cascade { get; init; } = 0.15;
    public double OverlapArmy { get; init; } = 0.08;

    /// <summary>A crisis that sacks the town hastens every other one: ripening days added to each power still gathering.</summary>
    public double AfterSack { get; init; } = 1.2;

    /// <summary>From this act (0-based) the worst enemy abroad always has a crisis under way, the next one beginning this many days after the last ends.</summary>
    public int PressureAct { get; init; } = 1;
    public int[] PressureGap { get; init; } = [25, 45];

    /// <summary>Power an enemy in crisis gathers each day, times its severity's strength.</summary>
    public double Gather { get; init; } = 1.5;

    /// <summary>When a crisis culminates its master calls on other enemies: the chance each answers, by its wits; kin always come. An ally sends this share of its power.</summary>
    public IReadOnlyDictionary<string, double> CallAllies { get; init; } = new Dictionary<string, double>();
    public double AllyShare { get; init; } = 0.6;
    public string Ally { get; init; } = "";

    /// <summary>Kinds that swallow a weaker neighbour's host whole as their crisis turns (the dead raise the fallen), and how much stronger they must be.</summary>
    public IReadOnlyList<string> Absorbers { get; init; } = [];
    public double AbsorbOver { get; init; } = 1.5;
    public IReadOnlyDictionary<string, string> AbsorbText { get; init; } = new Dictionary<string, string>();

    /// <summary>Raiders of a blow, as a share of the enemy's power.</summary>
    public double StrikeShare { get; init; } = 0.2;

    /// <summary>The walls' strength while saboteurs' damage stands, and for how long.</summary>
    public double WallsWeak { get; init; } = 0.8;
    public int WallsWeakDays { get; init; } = 20;
    public CrisisClashRules Clash { get; init; } = new();
    public VengeanceRules Vengeance { get; init; } = new();
    public InfiltrationRules Infiltration { get; init; } = new();
    public IReadOnlyDictionary<string, BlowText> Blows { get; init; } = new Dictionary<string, BlowText>();
    public IReadOnlyDictionary<string, CrisisKindDef> Kinds { get; init; } = new Dictionary<string, CrisisKindDef>();
    public CrisisEndText Ended { get; init; } = new();
}

public enum CrisisPhase
{
    Gathering,
    Onslaught,
    Marching,
}

/// <summary>One enemy's crisis, from its first omen to its host at the walls.</summary>
public sealed class Crisis
{
    public int Id { get; init; }
    public int MenaceId { get; init; }
    public string Severity { get; set; } = "";
    public CrisisPhase Phase { get; set; }
    public int StartDay { get; init; }

    /// <summary>When the current phase ends: the onslaught begins, or the host marches.</summary>
    public int NextPhaseDay { get; set; }
    public int NextBlowDay { get; set; }
    public int BlowsLanded { get; set; }
    public int BlowsStopped { get; set; }

    /// <summary>Its power before it raised its host, which it falls back to once the host is spent.</summary>
    public double PowerBefore { get; set; }
    public int? EndedDay { get; set; }

    /// <summary>How it ended: averted (the enemy broken first), held, sacked, swallowed (by a rival).</summary>
    public string Outcome { get; set; } = "";
    public bool Active => EndedDay == null;
}

/// <summary>Two enemies' hosts about to meet in the field, waiting on whether the town joins in.</summary>
public sealed class Clash
{
    public int Id { get; init; }
    public int A { get; init; }
    public int B { get; init; }
    public string Where { get; init; } = "";
    public int Day { get; init; }

    /// <summary>The menace the town rides out against, if it does.</summary>
    public int? Against { get; set; }
    public bool Answered { get; set; }
    public bool Done { get; set; }
}

/// <summary>Every crisis and clash of the campaign, and how ripe each enemy power is.</summary>
public sealed class CrisisBook
{
    public List<Crisis> All { get; } = [];
    public List<Clash> Clashes { get; } = [];

    /// <summary>Days each menace (by id) has been a power left alone, toward its crisis.</summary>
    public Dictionary<int, int> Ripeness { get; } = new();

    /// <summary>Attempts on its seat and holdings already counted against its ripening.</summary>
    public Dictionary<int, int> Disturbed { get; } = new();
    public int NextClashDay { get; set; }

    /// <summary>The day the next crisis begins if none is under way (<see cref="CrisisRules.PressureAct"/>).</summary>
    public int NextPressureDay { get; set; }
    public int WallsWeakUntil { get; set; }
}

/// <summary>
/// End-game crises: an enemy power left alone long enough in the later acts begins its crisis, a campaign of
/// its own against the town with a name (the Great Horde, the Long Dark, Wyrmfire, the Night of Knives...). How
/// bad it is follows the enemy's wits, level and power: a low-cunning warband's horde is lesser, an ancient
/// vampire's Long Dark is dire. A crisis gathers, then strikes blows (<see cref="Blows"/>) ever more often,
/// then culminates: blows all at once, and a host sized against the town's defence marches on the walls.
/// Breaking the enemy's seat first averts it. Crises overlap and cascade (each one under way speeds and swells
/// the others; a sack hastens the rest), and enemies in crisis meet in the field or at the walls, fighting each
/// other or making common cause; the town can ride out and fall on one of them.
/// </summary>
public static class Crises
{
    private static CrisisRules Rules(Town town) => town.Content.Crises;

    public static IEnumerable<Crisis> Active(Town town) => town.Crises.All.Where(c => c.Active);

    public static Crisis? Of(Town town, Menace m) => town.Crises.All.FirstOrDefault(c => c.Active && c.MenaceId == m.Id);

    public static Menace MenaceOf(Town town, Crisis c) => town.Saga.Menaces[c.MenaceId - 1];

    public static CrisisKindDef KindOf(Town town, Menace m) =>
        Rules(town).Kinds.GetValueOrDefault(m.Kind) ?? Rules(town).Kinds.GetValueOrDefault("tribe") ?? new CrisisKindDef();

    public static CrisisSeverityDef SeverityOf(Town town, Crisis c) =>
        Rules(town).Severities.FirstOrDefault(s => s.Id == c.Severity) ?? Rules(town).Severities.FirstOrDefault() ?? new CrisisSeverityDef();

    /// <summary>The crisis's name, as the town calls it: "Wyrmfire", "the Long Dark".</summary>
    public static string Title(Town town, Crisis c) => KindOf(town, MenaceOf(town, c)).Name;

    /// <summary>How bad this enemy's crisis would be: its wits, its level and its power (0 means it never has one).</summary>
    public static double Score(Town town, Menace m)
    {
        var s = Rules(town).Score;
        double wits = s.Cunning.GetValueOrDefault(m.Cunning, 1);
        if (wits <= 0) return 0;
        double power = Math.Clamp(m.Power / Math.Max(1, town.Content.Saga.Menaces.Stages.Dominion), s.PowerFloor, s.PowerCap);
        return wits * (1 + s.PerLevel * Math.Max(0, m.Level - 1)) * power * (m.BigBad ? s.BigBad : 1);
    }

    private static CrisisSeverityDef SeverityFor(Town town, double score) =>
        Rules(town).Severities.LastOrDefault(s => score >= s.Min) ?? Rules(town).Severities.FirstOrDefault() ?? new CrisisSeverityDef();

    /// <summary>Days until it culminates, counting the march.</summary>
    public static int DaysLeft(Town town, Crisis c)
    {
        if (c.Phase == CrisisPhase.Marching)
            return MenaceOf(town, c).ArrivalTick is int at ? Math.Max(0, (int)Math.Ceiling((at - town.Tick) / 24.0)) : 0;
        var sv = SeverityOf(town, c);
        int rest = c.Phase == CrisisPhase.Gathering ? Phase(town, sv, 1) : 0;
        return Math.Max(0, c.NextPhaseDay - town.Day) + rest + town.Content.Saga.Menaces.MarchDays;
    }

    /// <summary>Dread the crises under way add (<see cref="Saga"/>).</summary>
    public static double Dread(Town town) => Active(town).Sum(c => SeverityOf(town, c).Dread * (c.Phase == CrisisPhase.Gathering ? 0.5 : 1));

    public static void Daily(Town town, bool autoMayor)
    {
        if (!town.Saga.Enabled || town.Outcome != null) return;
        foreach (var c in Active(town).ToList())
        {
            var m = MenaceOf(town, c);
            if (!m.Alive)
            {
                End(town, c, "averted");
                continue;
            }
            Run(town, c, m);
            if (town.Outcome != null) return;
        }
        Ripen(town);
        Clashes(town, autoMayor);
    }

    // Beginning ----------------------------------------------------------------------------------

    /// <summary>Powers left alone in the later acts ripen toward their crisis; every party sent against them sets it back.</summary>
    private static void Ripen(Town town)
    {
        var r = Rules(town);
        var book = town.Crises;
        foreach (var m in Menaces.Alive(town))
        {
            if (m.Origin == Menaces.FromWarband || Of(town, m) != null || (town.Saga.YokeId == m.Id && !town.Saga.Defied)) continue;
            if (m.Stage < MenaceStage.Power && !m.BigBad) continue;
            if (Score(town, m) <= 0) continue;
            int tried = Attempts(town, m);
            int seen = book.Disturbed.GetValueOrDefault(m.Id, tried);
            int ripe = book.Ripeness.GetValueOrDefault(m.Id) + 1 - Math.Max(0, tried - seen) * r.DisturbDays;
            book.Disturbed[m.Id] = tried;
            book.Ripeness[m.Id] = Math.Max(0, ripe);
            if (town.Saga.Act < r.MinAct && !m.BigBad) continue;
            var last = book.All.Where(c => c.MenaceId == m.Id).Select(c => c.EndedDay ?? town.Day).DefaultIfEmpty(-r.CooldownDays).Max();
            if (town.Day - last < r.CooldownDays) continue;
            if (book.Ripeness[m.Id] >= r.RipenDays || m.BigBad) Begin(town, m);
        }
        Pressure(town);
    }

    /// <summary>
    /// A well-run town is never left in peace: from the second act, when no crisis is under way, the worst enemy
    /// abroad that has risen begins one a few weeks after the last ended.
    /// </summary>
    private static void Pressure(Town town)
    {
        var r = Rules(town);
        var book = town.Crises;
        if (town.Saga.Act < r.PressureAct || Active(town).Any()) return;
        if (book.NextPressureDay == 0) book.NextPressureDay = town.Day + town.Rng.Range(r.PressureGap[0], r.PressureGap[^1] + 1);
        if (town.Day < book.NextPressureDay) return;
        var worst = Menaces.Alive(town)
            .Where(m => m.Origin != Menaces.FromWarband && (m.Stage >= MenaceStage.Rising || m.BigBad) && !(town.Saga.YokeId == m.Id && !town.Saga.Defied) && Score(town, m) > 0)
            .Where(m => town.Day - book.All.Where(c => c.MenaceId == m.Id).Select(c => c.EndedDay ?? town.Day).DefaultIfEmpty(-r.CooldownDays).Max() >= r.CooldownDays / 3)
            .OrderByDescending(m => Score(town, m))
            .ThenBy(m => m.Id)
            .FirstOrDefault();
        if (worst != null) Begin(town, worst);
    }

    /// <summary>Parties that have gone for its seat and holdings.</summary>
    private static int Attempts(Town town, Menace m) =>
        m.Holdings.Prepend(m.SeatId).Select(id => Quests.Site(town, id)?.Attempts ?? 0).Sum();

    private static int Phase(Town town, CrisisSeverityDef sv, int phase)
    {
        int others = Math.Max(0, Active(town).Count() - 1);
        return Math.Max(8, (int)Math.Round(sv.PhaseDays[Math.Min(phase, sv.PhaseDays.Length - 1)] * Math.Pow(1 - Rules(town).Cascade, others)));
    }

    private static void Begin(Town town, Menace m)
    {
        var sv = SeverityFor(town, Score(town, m));
        var c = new Crisis { Id = town.Crises.All.Count + 1, MenaceId = m.Id, Severity = sv.Id, StartDay = town.Day };
        town.Crises.All.Add(c);
        town.Crises.Ripeness[m.Id] = 0;
        c.NextPhaseDay = town.Day + Phase(town, sv, 0);
        c.NextBlowDay = town.Day + Every(town, sv, true);
        m.Named = true;
        Saga.Scar(town, sv.Scar);
        string text = Saga.Fill(town, KindOf(town, m).Start, m);
        town.Log(LedgerCategory.War, Importance.Major, $"{text} {Saga.Cap(Article(sv.Name))} crisis: it culminates in some {DaysLeft(town, c)} days unless {m.Leader} is broken first.");
    }

    private static string Article(string severity) => severity is "" ? "a" : "aeiou".Contains(severity[0]) ? $"an {severity}" : $"a {severity}";

    private static int Every(Town town, CrisisSeverityDef sv, bool gathering)
    {
        int days = town.Rng.Range(sv.BlowEvery[0], sv.BlowEvery[^1] + 1);
        return gathering ? days * 2 : days;
    }

    // Running ------------------------------------------------------------------------------------

    private static void Run(Town town, Crisis c, Menace m)
    {
        var sv = SeverityOf(town, c);
        // A vassal is spared its overlord's crisis while it pays.
        if (town.Saga.YokeId == m.Id && !town.Saga.Defied) return;
        if (c.Phase == CrisisPhase.Marching)
        {
            // Its host was turned back or broken on the way, or never reached the walls: the crisis is spent.
            if (m.ArrivalTick == null) End(town, c, "held");
            return;
        }
        m.Power += Rules(town).Gather * sv.Strength;
        if (town.Day >= c.NextBlowDay)
        {
            c.NextBlowDay = town.Day + Every(town, sv, c.Phase == CrisisPhase.Gathering);
            for (int i = 0; i < sv.Blows; i++) Strike(town, c, m, sv);
            if (town.Outcome != null) return;
        }
        if (town.Day < c.NextPhaseDay) return;
        if (c.Phase == CrisisPhase.Gathering)
        {
            // It may have grown worse while it gathered.
            var worse = SeverityFor(town, Score(town, m));
            if (worse.Min > sv.Min) c.Severity = worse.Id;
            c.Phase = CrisisPhase.Onslaught;
            c.NextPhaseDay = town.Day + Phase(town, SeverityOf(town, c), 1);
            c.NextBlowDay = Math.Min(c.NextBlowDay, town.Day + 2);
            town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, KindOf(town, m).Phase, m));
            Absorb(town, m);
            return;
        }
        Culminate(town, c, m);
    }

    private static Culprit By(Town town, Menace m) => new(m.Name, m.Leader, Quests.Site(town, m.SeatId)?.Def.Name ?? m.Region);

    /// <summary>One blow, of a kind the enemy favours.</summary>
    private static void Strike(Town town, Crisis c, Menace m, CrisisSeverityDef sv)
    {
        var weights = KindOf(town, m).Blows.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToList();
        if (weights.Count == 0) return;
        double roll = town.Rng.Next() * weights.Sum(kv => kv.Value);
        string kind = weights[^1].Key;
        foreach (var (k, w) in weights)
        {
            roll -= w;
            if (roll < 0)
            {
                kind = k;
                break;
            }
        }
        double raiders = m.Power * Rules(town).StrikeShare * sv.Strength;
        bool landed = kind == Blows.Raid ? Menaces.Raid(town, m, raiders) : Blows.Strike(town, kind, By(town, m), raiders, sv.Strength);
        if (landed) c.BlowsLanded++;
        else c.BlowsStopped++;
    }

    /// <summary>The crisis comes to a head: its blows fall all at once and its host, swollen by every other crisis under way, marches.</summary>
    private static void Culminate(Town town, Crisis c, Menace m)
    {
        var r = Rules(town);
        var sv = SeverityOf(town, c);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, KindOf(town, m).Culminate, m));
        for (int i = 0; i < sv.CulminationBlows && town.Outcome == null; i++) Strike(town, c, m, sv);
        if (town.Outcome != null) return;
        Absorb(town, m);
        int others = Active(town).Count(x => x != c);
        double host = Threat.Defense(town) * sv.Army * (1 + r.OverlapArmy * others) * (0.9 + town.Rng.Next() * 0.2);
        // The big bad's doom clock: every march it has made swells the host its crisis brings, so a finale the heroes
        // cannot end at the seat or the breach ends at the walls instead.
        if (m.BigBad) host = Math.Max(host, Threat.Defense(town) * town.Content.Saga.Finale.MarchGrowth * m.Marches);
        c.PowerBefore = m.Power;
        m.Power = Math.Max(m.Power, host) + Allies(town, m);
        c.Phase = CrisisPhase.Marching;
        // A big bad already on the road gathers its crisis into the host that is marching.
        if (m.ArrivalTick == null) Menaces.MarchNow(town, m);
    }

    /// <summary>Its master calls on the other enemies abroad: kin come, others as its wits persuade them. Returns the warriors they send.</summary>
    private static double Allies(Town town, Menace m)
    {
        var r = Rules(town);
        double chance = r.CallAllies.GetValueOrDefault(m.Cunning);
        double sent = 0;
        foreach (var o in Menaces.Alive(town).Where(o => o != m && o.Origin != Menaces.FromWarband && !o.BigBad && o.ArrivalTick == null && o.Stage >= MenaceStage.Rising).OrderBy(o => o.Id).ToList())
        {
            if (town.Saga.YokeId == o.Id && !town.Saga.Defied) continue;
            if (Of(town, o) is { Phase: CrisisPhase.Marching }) continue;
            if (!Pact(town, m, o) && !town.Rng.Chance(chance)) continue;
            double share = o.Power * r.AllyShare;
            o.Power -= share * 0.5;
            sent += share;
            if (r.Ally != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(r.Ally.Replace("{ally}", o.Name).Replace("{name}", m.Name).Replace("{leader}", m.Leader).Replace("{town}", town.Name).Replace("{n}", Js.Str(Js.Round(share / 10) * 10))));
        }
        return sent;
    }

    /// <summary>The dead and their like raise a weaker neighbour's fallen into their own host as their crisis turns.</summary>
    private static void Absorb(Town town, Menace m)
    {
        var r = Rules(town);
        if (!r.Absorbers.Contains(m.Kind)) return;
        var seat = Quests.Site(town, m.SeatId);
        var region = seat != null ? World.RegionOf(town, seat) : null;
        if (region == null) return;
        var prey = Menaces.Alive(town)
            .Where(o => o != m && !o.BigBad && o.Origin != Menaces.FromWarband && o.ArrivalTick == null && o.Kind != m.Kind && m.Power >= o.Power * r.AbsorbOver)
            .Select(o => (Menace: o, Region: Quests.Site(town, o.SeatId) is { } os ? World.RegionOf(town, os) : null))
            .Where(x => x.Region != null && Math.Abs(x.Region.Ring - region.Ring) <= r.Clash.Reach)
            .OrderByDescending(x => x.Menace.Power)
            .ThenBy(x => x.Menace.Id)
            .Select(x => x.Menace)
            .FirstOrDefault();
        if (prey == null) return;
        string name = prey.Name;
        m.Power += prey.Power * 0.7;
        if (Of(town, prey) is { } pc) End(town, pc, "swallowed");
        Menaces.Swallowed(town, prey);
        string text = r.AbsorbText.GetValueOrDefault(m.Kind) ?? "";
        if (text != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(Saga.Fill(town, text, m).Replace("{other}", name)));
    }

    // Ending -------------------------------------------------------------------------------------

    /// <summary>
    /// The host of an enemy in crisis has been at the walls (<see cref="Menaces"/>): the crisis is spent, the host
    /// it raised melts away, and a sack hastens every other power toward its own crisis.
    /// </summary>
    internal static void Assaulted(Town town, Menace m, bool sacked)
    {
        // Hosts that joined it at the walls share its fate.
        var spent = Active(town).Where(c => c.Phase == CrisisPhase.Marching && (c.MenaceId == m.Id || MenaceOf(town, c).ArrivalTick == null)).ToList();
        if (spent.Count == 0) return;
        foreach (var c in spent)
        {
            var host = MenaceOf(town, c);
            host.Power = Math.Min(host.Power, sacked ? c.PowerBefore * 1.2 : c.PowerBefore);
            End(town, c, sacked ? "sacked" : "held");
        }
        if (!sacked) return;
        var r = Rules(town);
        foreach (var o in Menaces.Alive(town).Where(o => o != m).OrderBy(o => o.Id))
            if (town.Crises.Ripeness.ContainsKey(o.Id)) town.Crises.Ripeness[o.Id] = (int)(town.Crises.Ripeness[o.Id] + r.RipenDays * (r.AfterSack - 1) + r.DisturbDays);
    }

    private static void End(Town town, Crisis c, string outcome)
    {
        c.EndedDay = town.Day;
        c.Outcome = outcome;
        var gap = Rules(town).PressureGap;
        town.Crises.NextPressureDay = town.Day + town.Rng.Range(gap[0], gap[^1] + 1);
        var m = MenaceOf(town, c);
        var e = Rules(town).Ended;
        string title = Title(town, c);
        string text = outcome switch
        {
            "averted" => Saga.Fill(town, KindOf(town, m).Averted, m),
            "held" => Saga.Fill(town, e.Held, m),
            "sacked" => Saga.Fill(town, e.Sacked, m),
            "routed" => Saga.Fill(town, e.Routed, m),
            _ => "",
        };
        if (text != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(text.Replace("{crisis}", title)));
        if (outcome is "averted" or "held") Saga.Relief(town);
    }

    // Clashes ------------------------------------------------------------------------------------

    /// <summary>Whether two enemies would make common cause rather than fight: the same kind of foe, or two masterminds.</summary>
    public static bool Pact(Town town, Menace a, Menace b) =>
        a.Kind == b.Kind || (a.Cunning == "mastermind" && b.Cunning == "mastermind");

    /// <summary>A clash the town has not yet answered.</summary>
    public static IEnumerable<Clash> Pending(Town town) => town.Crises.Clashes.Where(x => !x.Done && !x.Answered);

    private static void Clashes(Town town, bool autoMayor)
    {
        var cr = Rules(town).Clash;
        var book = town.Crises;
        foreach (var x in book.Clashes.Where(x => !x.Done).ToList())
        {
            if (autoMayor && !x.Answered) AutoJoin(town, x);
            if (town.Day >= x.Day) Fight(town, x);
        }
        if (town.Day < book.NextClashDay) return;
        book.NextClashDay = town.Day + town.Rng.Range(cr.EveryDays[0], cr.EveryDays[^1] + 1);
        // An enemy in crisis runs into any other power on the march: they settle who rules the land.
        var crisis = Active(town).Where(c => c.Phase != CrisisPhase.Marching).Select(c => MenaceOf(town, c)).Where(m => m.Alive && m.ArrivalTick == null).OrderBy(m => m.Id).ToList();
        var hosts = Menaces.Alive(town).Where(m => m.Origin != Menaces.FromWarband && m.ArrivalTick == null && (m.Stage >= MenaceStage.Rising || m.BigBad)).OrderBy(m => m.Id).ToList();
        foreach (var a in crisis)
            foreach (var b in hosts.Where(b => b != a && !(crisis.Contains(b) && b.Id < a.Id)))
            {
                if (Pact(town, a, b) || book.Clashes.Any(x => !x.Done && (x.A == a.Id || x.B == a.Id || x.A == b.Id || x.B == b.Id))) continue;
                var sa = Quests.Site(town, a.SeatId);
                var sb = Quests.Site(town, b.SeatId);
                var ra = sa != null ? World.RegionOf(town, sa) : null;
                var rb = sb != null ? World.RegionOf(town, sb) : null;
                if (ra == null || rb == null || Math.Abs(ra.Ring - rb.Ring) > cr.Reach) continue;
                // They meet on the nearer one's ground.
                var where = ra.Ring <= rb.Ring ? ra : rb;
                var x = new Clash { Id = book.Clashes.Count + 1, A = a.Id, B = b.Id, Where = where.Name, Day = town.Day + cr.NoticeDays };
                book.Clashes.Add(x);
                where.Known = true;
                town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(cr.Announce.Replace("{a}", a.Name).Replace("{b}", b.Name).Replace("{where}", x.Where).Replace("{days}", cr.NoticeDays.ToString()).Replace("{town}", town.Name)));
                return;
            }
    }

    /// <summary>The town's answer to a clash: ride out against one of the two hosts (by menace id), or 0 to keep behind the walls.</summary>
    public static bool Join(Town town, int clashId, int against)
    {
        var x = town.Crises.Clashes.FirstOrDefault(c => c.Id == clashId);
        if (x == null || x.Done || x.Answered) return false;
        if (against != 0 && against != x.A && against != x.B) return false;
        x.Answered = true;
        x.Against = against == 0 ? null : against;
        return true;
    }

    /// <summary>The stand-in mayor falls on the worse of the two when the town can spare the strength, and otherwise keeps its walls.</summary>
    private static void AutoJoin(Town town, Clash x)
    {
        var a = town.Saga.Menaces[x.A - 1];
        var b = town.Saga.Menaces[x.B - 1];
        var worse = Score(town, a) >= Score(town, b) ? a : b;
        double share = Threat.Defense(town) * Rules(town).Clash.TownShare;
        Join(town, x.Id, share >= worse.Power * 0.3 ? worse.Id : 0);
    }

    private static void Fight(Town town, Clash x)
    {
        var cr = Rules(town).Clash;
        var rng = town.Rng;
        x.Done = true;
        var a = town.Saga.Menaces[x.A - 1];
        var b = town.Saga.Menaces[x.B - 1];
        if (!a.Alive || !b.Alive) return;
        double pa = a.Power * (0.8 + rng.Next() * 0.4);
        double pb = b.Power * (0.8 + rng.Next() * 0.4);
        if (x.Against is int target)
        {
            var foe = target == a.Id ? a : b;
            var other = foe == a ? b : a;
            double share = Threat.Defense(town) * cr.TownShare;
            if (foe == a) pa = Math.Max(0, pa - share);
            else pb = Math.Max(0, pb - share);
            foe.Power = Math.Max(0, foe.Power - share * 0.5);
            var riders = town.Heroes.Where(h => !h.IsAway && h.Hero!.Hp > 0).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).Take(4).ToList();
            foreach (var h in riders) Notoriety.Deed(town, h, "slay", 6);
            int lost = Math.Min(town.Workers.Count, (int)(share / cr.MilitiaPerDead));
            var dead = new List<Agent>();
            var pool = town.Workers.OrderBy(w => w.Id).ToList();
            for (int i = 0; i < lost && pool.Count > 0; i++)
            {
                int k = rng.Range(0, pool.Count);
                dead.Add(pool[k]);
                pool.RemoveAt(k);
            }
            foreach (var d in dead) Threat.Kill(town, d);
            Vengeance.Wrong(town, foe, "clash", x.Where, riders.Select(h => h.Id));
            if (Of(town, other) is { } truce) truce.NextBlowDay = Math.Max(truce.NextBlowDay, town.Day + cr.TruceDays);
            string fallen = dead.Count > 0 ? $" {Expedition.ListNames(dead.Select(d => d.Name).ToList())} {(dead.Count == 1 ? "does" : "do")} not come home." : "";
            town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(cr.Joined.Replace("{town}", town.Name).Replace("{where}", x.Where).Replace("{target}", foe.Name).Replace("{other}", other.Name)) + fallen, riders.Concat(dead).Select(h => h.Id).ToList());
        }
        var (winner, loser, pw, pl) = pa >= pb ? (a, b, pa, pb) : (b, a, pb, pa);
        if (pw >= pl * cr.Absorb && !loser.BigBad)
        {
            // The winner swallows the loser and takes up its crisis: the two become one, worse.
            winner.Power += loser.Power * 0.5;
            var lc = Of(town, loser);
            string lname = loser.Name;
            Menaces.Swallowed(town, loser);
            if (lc != null) End(town, lc, "swallowed");
            if (Of(town, winner) is { } wc && lc != null)
            {
                int i = Rules(town).Severities.ToList().FindIndex(s => s.Id == wc.Severity);
                if (i >= 0 && i + 1 < Rules(town).Severities.Count) wc.Severity = Rules(town).Severities[i + 1].Id;
            }
            town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(cr.Swallowed.Replace("{winner}", winner.Name).Replace("{loser}", lname).Replace("{where}", x.Where)));
            return;
        }
        winner.Power = Math.Max(0, winner.Power - loser.Power * cr.WinnerLoss);
        loser.Power = Math.Max(0, loser.Power * (1 - cr.LoserLoss));
        // A beaten host's crisis falls back to gathering.
        if (Of(town, loser) is { } back && back.Phase == CrisisPhase.Onslaught)
        {
            back.Phase = CrisisPhase.Gathering;
            back.NextPhaseDay = town.Day + Phase(town, SeverityOf(town, back), 0);
        }
        town.Log(LedgerCategory.War, Importance.Major, Saga.Sentences(cr.Fought.Replace("{winner}", winner.Name).Replace("{loser}", loser.Name).Replace("{where}", x.Where)));
    }

    /// <summary>
    /// Hosts reaching the walls within days of each other (<see cref="Menaces"/>): kin and masterminds join
    /// forces, the rest turn on each other first. Returns the attacking host, or 0 if this one was driven off.
    /// </summary>
    internal static double Converge(Town town, Menace m, double army, List<string> lines)
    {
        var cr = Rules(town).Clash;
        var rng = town.Rng;
        int window = 3 * SimTime.HoursPerDay;
        foreach (var o in Menaces.Alive(town).Where(o => o != m && o.ArrivalTick is int at && Math.Abs(at - town.Tick) <= window).OrderBy(o => o.Id).ToList())
        {
            if (Pact(town, m, o))
            {
                army += o.Power;
                Menaces.Recall(town, o);
                lines.Add(Saga.Sentences(cr.Pact.Replace("{a}", m.Name).Replace("{b}", o.Name).Replace("{town}", town.Name)));
                continue;
            }
            double mine = army * (0.8 + rng.Next() * 0.4), theirs = o.Power * (0.8 + rng.Next() * 0.4);
            var (winner, loser) = mine >= theirs ? (m, o) : (o, m);
            string text = Saga.Sentences(cr.Melee.Replace("{a}", m.Name).Replace("{b}", o.Name).Replace("{town}", town.Name).Replace("{winner}", winner.Name).Replace("{loser}", loser.Name));
            if (winner == m)
            {
                army = Math.Max(0, army - o.Power * cr.WinnerLoss);
                o.Power *= 1 - cr.LoserLoss;
                Menaces.Recall(town, o);
                lines.Add(text);
                if (Of(town, o) is { } beaten && beaten.Phase == CrisisPhase.Marching)
                {
                    o.Power = Math.Min(o.Power, beaten.PowerBefore);
                    End(town, beaten, "routed");
                }
                continue;
            }
            m.Power = army * (1 - cr.LoserLoss);
            o.Power = Math.Max(0, o.Power - army * cr.WinnerLoss);
            town.Log(LedgerCategory.War, Importance.Major, text);
            if (Of(town, m) is { } routed)
            {
                m.Power = Math.Min(m.Power, routed.PowerBefore);
                End(town, routed, "routed");
            }
            return 0;
        }
        return army;
    }
}
