namespace CrownAndCompass.Sim.Systems;

/// <summary>How one kind of menace looks and talks as it festers.</summary>
public sealed record MenaceKindDef
{
    /// <summary>Library creature types its leader can be.</summary>
    public IReadOnlyList<string> Types { get; init; } = [];
    public IReadOnlyList<string> Leaders { get; init; } = [];

    /// <summary>What it is called at each stage, lair to overlord: {leader}, {seat}, {region}.</summary>
    public IReadOnlyList<string> Titles { get; init; } = [];

    /// <summary>What a razed settlement's people are to it ("the dwarves of Ironhold are gone").</summary>
    public string People { get; init; } = "";
    public string Rise { get; init; } = "";
    public string RiseDark { get; init; } = "";
    /// <summary>What a strike on the town reads like: {name}, {leader}, {where}, {seat}, {region}; the dark lines when someone dies.</summary>
    public IReadOnlyList<string> Strike { get; init; } = [];
    public IReadOnlyList<string> StrikeDark { get; init; } = [];
    public string Raze { get; init; } = "";
    public string Slain { get; init; } = "";
}

/// <summary>
/// How far a menace can scheme, set by its founding leader's Intelligence: a mindless beast hunts and
/// never rules, a kobold band takes a valley and squabbles, a vampire lord builds a domain and can be crowned.
/// </summary>
public sealed record MenaceCunningDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The highest leader Intelligence this tier covers; tiers are listed lowest first.</summary>
    public int MaxInt { get; init; } = 99;
    public MenaceStage MaxStage { get; init; } = MenaceStage.Dominion;

    /// <summary>Levels its seat can rise above where it started.</summary>
    public int LevelGain { get; init; } = 20;
    public double Growth { get; init; } = 1;
    public bool Holdings { get; init; } = true;

    /// <summary>Whether the finale can crown it the big bad, and how much its power counts toward it.</summary>
    public bool Crown { get; init; }
    public double CrownWeight { get; init; } = 1;

    /// <summary>A patient menace brings another settlement of the wilds under its sway every so many days once it has risen, up to a cap.</summary>
    public int DomainEveryDays { get; init; }
    public int MaxDomain { get; init; }
    public string Domain { get; init; } = "";

    /// <summary>Told once, when it has grown as far as its wits allow.</summary>
    public string Plateau { get; init; } = "";
}

/// <summary>What a menace wants, which decides how it treats the town: hunt, hold, conquer, rule, infect or scheme.</summary>
public sealed record MenaceAmbitionDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>One line for the Campaign window: {town}.</summary>
    public string What { get; init; } = "";

    /// <summary>Cunning tiers it needs (by id, inclusive); empty means any.</summary>
    public string MinCunning { get; init; } = "";
    public string MaxCunning { get; init; } = "";

    /// <summary>Menace kinds it suits; empty means any.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public double Weight { get; init; } = 1;
    public bool Marches { get; init; }
    public double Growth { get; init; } = 1;

    // territory
    public string Provoked { get; init; } = "";
    public int ProvokeDays { get; init; } = 40;

    // vassal
    public string Demand { get; init; } = "";
    public string Submitted { get; init; } = "";
    public string Refused { get; init; } = "";
    public int AnswerDays { get; init; } = 10;

    // plague
    public IReadOnlyList<string> Infect { get; init; } = [];
    public string Warded { get; init; } = "";

    // intrigue
    public IReadOnlyList<string> Schemes { get; init; } = [];
    public string Caught { get; init; } = "";
}

/// <summary>Menaces that share a region fight over it: the stronger takes a lair, or swallows the weaker whole.</summary>
public sealed record MenaceFeudRules
{
    public int[] EveryDays { get; init; } = [30, 50];
    public double Take { get; init; } = 0.25;

    /// <summary>How many times stronger it must be to swallow the other.</summary>
    public double Absorb { get; init; } = 2.5;
    public string Taken { get; init; } = "";
    public string Absorbed { get; init; } = "";
}

public sealed record MenaceStageRules
{
    public double Rising { get; init; } = 80;
    public double Power { get; init; } = 200;
    public double Dominion { get; init; } = 400;
    public double Overlord { get; init; } = 450;
}

public sealed record MenaceRules
{
    /// <summary>Menaces abroad at once in each act, not counting the warband; lairs fester only below the cap.</summary>
    public int[] MaxByAct { get; init; } = [2, 3, 4, 5];

    /// <summary>Days a full lair must be known and left alone before it festers, and days between new ones.</summary>
    public int FesterDays { get; init; } = 45;
    public int FesterEveryDays { get; init; } = 12;
    public double Growth { get; init; } = 0.8;
    public double UnknownGrowth { get; init; } = 0.5;
    public double HoldingGrowth { get; init; } = 0.3;

    /// <summary>Power a menace gains from every party that went for it and did not finish it.</summary>
    public double FailedDelve { get; init; } = 25;

    /// <summary>Power per level the menace's seat rises above where it started.</summary>
    public double PerLevel { get; init; } = 70;
    public int MaxLevel { get; init; } = 14;
    public MenaceStageRules Stages { get; init; } = new();
    public int[] StrikeEveryDays { get; init; } = [10, 16];
    public double StrikeShare { get; init; } = 0.15;
    public double RepelLoss { get; init; } = 0.3;
    public double HoldingCleared { get; init; } = 40;
    public double SeatBroken { get; init; } = 0.5;
    public int BrokenLevels { get; init; } = 2;
    public int[] AssaultEveryDays { get; init; } = [35, 50];
    public int MarchDays { get; init; } = 4;
    public int BountyEveryDays { get; init; } = 20;
    public double AssaultConfidence { get; init; } = 0.7;
    public double AssaultRetreat { get; init; } = 0.6;
    public double SettlementHit { get; init; } = 25;
    public double BountyBonus { get; init; } = 60;
    public string PowerText { get; init; } = "";
    public string DominionText { get; init; } = "";
    public string Repelled { get; init; } = "";
    public string Broken { get; init; } = "";
    public string Succeeded { get; init; } = "";
    public string Rootless { get; init; } = "";
    public IReadOnlyDictionary<string, MenaceKindDef> Kinds { get; init; } = new Dictionary<string, MenaceKindDef>();
    public IReadOnlyList<MenaceCunningDef> Cunning { get; init; } = [];
    public IReadOnlyList<MenaceAmbitionDef> Ambitions { get; init; } = [];
    public MenaceFeudRules Feud { get; init; } = new();

    /// <summary>Villains' plans and the assets their strength rests on (<see cref="Plots"/>).</summary>
    public PlotRules Plots { get; init; } = new();
}

public enum MenaceStage
{
    Lair,
    Rising,
    Power,
    Dominion,
    Overlord,
}

/// <summary>A threat with a name: a lair that festered, a power an act brought, the warband, or a cult.</summary>
public sealed class Menace
{
    public int Id { get; init; }
    public required string Kind { get; init; }
    public required MenaceKindDef Def { get; init; }
    public required string Leader { get; set; }

    /// <summary>Where it came from: lair, act, warband, cult, remnant or awakened.</summary>
    public required string Origin { get; init; }

    /// <summary>The site it rules from; clearing it breaks the menace. Holdings are lairs it has taken in.</summary>
    public string SeatId { get; set; } = "";
    public string SeatName { get; set; } = "";
    public List<string> Holdings { get; } = [];
    public string Region { get; set; } = "the hills";

    /// <summary>The level of the seat it was born at; BaseLevel drops when it is broken.</summary>
    public int BornLevel { get; set; }

    /// <summary>The level the seat started at; Level is that plus what Power has added.</summary>
    public int BaseLevel { get; set; } = 1;
    public int Level { get; set; } = 1;
    public double Power { get; set; }
    public MenaceStage Stage { get; set; }

    /// <summary>The highest stage it has reached: only a new height is news.</summary>
    public MenaceStage Peak { get; set; }
    public int NextBountyDay { get; set; }
    public int BornDay { get; init; }

    /// <summary>The town has heard its leader's name.</summary>
    public bool Named { get; set; }
    public int NextStrikeTick { get; set; }
    public int? NextAssaultTick { get; set; }

    /// <summary>When its marching host reaches the walls.</summary>
    public int? ArrivalTick { get; set; }

    /// <summary>The wall section its host comes at.</summary>
    public int Approach { get; set; }
    public int SeenAttempts { get; set; }

    /// <summary>Times its host has marched on the town.</summary>
    public int Marches { get; set; }
    public bool BigBad { get; set; }
    public int? EndedDay { get; set; }
    public string? FactionId { get; init; }

    /// <summary>How far it can scheme (a cunning tier id, from its founder's Intelligence) and what it wants (an ambition id).</summary>
    public string Cunning { get; set; } = "cunning";
    public string Ambition { get; set; } = "conquest";

    /// <summary>Whose heir it is in the world's history (a lineage id, content/game/history.json): what it wants and why.</summary>
    public string Lineage { get; set; } = "";

    /// <summary>Its leader turned and joined the town (as this hero), against its own history.</summary>
    public bool Redeemed { get; set; }
    public int? TurnedHeroId { get; set; }

    /// <summary>The town's own hero who fell and leads it.</summary>
    public int? FallenHeroId { get; set; }

    /// <summary>Who its leader was in another campaign, told when it rises.</summary>
    public string Legend { get; set; } = "";

    /// <summary>It has grown as far as its wits allow, and the town has been told.</summary>
    public bool Plateaued { get; set; }
    public int NextDomainDay { get; set; }

    /// <summary>Settlements of the wilds it has brought under its sway.</summary>
    public int Domain { get; set; }
    public int NextFeudDay { get; set; }

    /// <summary>A territorial menace strikes at the town only while provoked.</summary>
    public int ProvokedUntilTick { get; set; }

    /// <summary>A would-be overlord's demand for fealty: when its answer is due, and whether the town refused.</summary>
    public int? DemandDueTick { get; set; }
    public bool Refused { get; set; }

    /// <summary>What its strength rests on, each kept at one of its sites, and what it is working toward (<see cref="Plots"/>).</summary>
    public List<MenaceAsset> Assets { get; } = [];
    public MenacePlan? Plan { get; set; }
    public int NextPlanDay { get; set; }

    /// <summary>Its leader's right hand, who may get away when the leader does not.</summary>
    public string Lieutenant { get; set; } = "";

    /// <summary>The last the town heard of it (a Ledger line) and when, for its leader card.</summary>
    public string LastHeard { get; set; } = "";
    public int LastHeardDay { get; set; }

    /// <summary>What it holds against the town, newest last (a few at most), for its leader card.</summary>
    public List<string> Memories { get; } = [];

    /// <summary>Its raids the town has driven off in a row (each comes bigger), and the side of town the last was turned back at.</summary>
    public int Repulsed { get; set; }
    public int? RepulsedAt { get; set; }

    /// <summary>The settlement whose road it holds and tolls, if any.</summary>
    public int? TollOn { get; set; }

    /// <summary>The warband keeps its own name until it is crowned.</summary>
    public string? FixedName { get; init; }
    public bool Alive => EndedDay == null;

    public string Name
    {
        get
        {
            if (FixedName != null && Stage != MenaceStage.Overlord) return FixedName;
            string t = Def.Titles.Count > (int)Stage ? Def.Titles[(int)Stage] : "{leader}";
            return t.Replace("{leader}", Leader).Replace("{seat}", SeatName).Replace("{region}", Region);
        }
    }
}

/// <summary>
/// Menaces: threats that fester. A lair the town knows of and leaves alone at full strength, an act's
/// new power, a warband that outlived its act, or a cult left to sing under the hill grows on the daily
/// tick: its seat rises in level (a stronger library creature takes the throne), it takes in nearby lairs
/// as holdings, and it climbs through stages. Rising, it strikes at the town and at the peoples of the
/// wilds; in power, it hammers them; in dominion, it razes settlements and marches on the walls. Every
/// party that goes for it and fails feeds it. Clearing its seat breaks it: with holdings left, its leader
/// flees (or an heir takes over) and it descends two levels; with none, it ends.
/// </summary>
public static class Menaces
{
    public const string FromWarband = "warband";

    private static MenaceRules Rules(Town town) => town.Content.Saga.Menaces;

    private static MenaceKindDef KindDef(Town town, string kind) =>
        Rules(town).Kinds.GetValueOrDefault(kind) ?? Rules(town).Kinds.GetValueOrDefault("beasts") ?? new MenaceKindDef();

    public static List<Menace> Alive(Town town) => town.Saga.Menaces.Where(m => m.Alive).ToList();

    /// <summary>The menace whose seat or holding a site is, if any.</summary>
    public static Menace? At(Town town, string siteId) =>
        town.Saga.Menaces.FirstOrDefault(m => m.Alive && (m.SeatId == siteId || m.Holdings.Contains(siteId)));

    public static void Daily(Town town, bool autoMayor)
    {
        foreach (var m in Alive(town))
        {
            if (m.Origin == FromWarband)
            {
                SyncWarband(town, m);
                continue;
            }
            var seat = Quests.Site(town, m.SeatId);
            if (seat == null) continue;
            Holdings(town, m);
            if (seat.Cleared)
            {
                SeatFalls(town, m, seat);
                if (!m.Alive || town.Outcome != null) continue;
                seat = Quests.Site(town, m.SeatId)!;
                if (seat.Cleared) continue;
            }
            Grow(town, m, seat);
            if (m.Stage >= MenaceStage.Power) Demand(town, m);
            if (m.Stage >= MenaceStage.Rising && town.Tick >= m.NextStrikeTick) Strike(town, m, seat);
            if (m.Stage >= MenaceStage.Dominion || m.ArrivalTick != null) Assaults(town, m);
            if (town.Outcome != null) return;
        }
        Feuds(town);
        Plots.Daily(town);
        Watch(town);
        Fester(town);
        Cult(town);
        if (autoMayor)
        {
            AutoAnswer(town);
            AutoBounty(town);
        }
    }

    // Birth --------------------------------------------------------------------------------------

    internal static string NewLeaderName(Town town, MenaceKindDef def) => NewLeader(town, def);

    private static string NewLeader(Town town, MenaceKindDef def)
    {
        var taken = town.Saga.Menaces.Select(m => m.Leader).ToHashSet();
        var free = def.Leaders.Where(l => !taken.Contains(l)).ToList();
        if (free.Count > 0) return free[town.Rng.Range(0, free.Count)];
        // Every name is spent: an heir to one of them.
        var heirs = def.Leaders.Select(l => $"{l} the Younger").Where(l => !taken.Contains(l)).ToList();
        return heirs.Count > 0 ? heirs[town.Rng.Range(0, heirs.Count)] : "a nameless thing";
    }

    private static string KindOf(SiteState s) => s.Def.Kind is "" or null ? "beasts" : s.Def.Kind;

    // Wits and wants -----------------------------------------------------------------------------

    private static readonly MenaceCunningDef DefaultCunning = new() { Id = "cunning", Name = "cunning", Crown = true };
    private static readonly MenaceAmbitionDef DefaultAmbition = new() { Id = "conquest", Name = "conquers", Marches = true };

    public static MenaceCunningDef CunningOf(Town town, Menace m) => Rules(town).Cunning.FirstOrDefault(c => c.Id == m.Cunning) ?? DefaultCunning;

    public static MenaceAmbitionDef AmbitionOf(Town town, Menace m) => Rules(town).Ambitions.FirstOrDefault(a => a.Id == m.Ambition) ?? DefaultAmbition;

    /// <summary>Whether the finale may crown it: only a mind that can rule becomes the big bad.</summary>
    public static bool CanCrown(Town town, Menace m) => CunningOf(town, m).Crown;

    internal static int CunningRank(Town town, string id)
    {
        var tiers = Rules(town).Cunning;
        for (int i = 0; i < tiers.Count; i++)
            if (tiers[i].Id == id) return i;
        return -1;
    }

    /// <summary>The tier a leader of this Intelligence schemes at.</summary>
    private static string CunningFor(Town town, int intelligence)
    {
        var tier = Rules(town).Cunning.FirstOrDefault(c => intelligence <= c.MaxInt) ?? Rules(town).Cunning.LastOrDefault();
        return tier?.Id ?? DefaultCunning.Id;
    }

    private static int IntOf(Town town, string monsterId)
    {
        if (monsterId == "") return 10;
        int i = town.Content.Monster(monsterId).Int;
        return i > 0 ? i : 10;
    }

    /// <summary>What it wants, weighted by what suits its kind and its wits.</summary>
    private static string PickAmbition(Town town, string kind, string cunning, string lineage = "")
    {
        var heirs = Annals.Lineage(town.Content.History, lineage)?.Ambitions;
        double Weight(MenaceAmbitionDef a) => a.Weight * (heirs != null && heirs.TryGetValue(a.Id, out double x) ? x : 1);
        int rank = CunningRank(town, cunning);
        var fit = Rules(town).Ambitions
            .Where(a => a.Kinds.Count == 0 || a.Kinds.Contains(kind))
            .Where(a => a.MinCunning == "" || rank >= CunningRank(town, a.MinCunning))
            .Where(a => a.MaxCunning == "" || rank <= CunningRank(town, a.MaxCunning))
            .ToList();
        if (fit.Count == 0) return Rules(town).Ambitions.Count > 0 ? "territory" : DefaultAmbition.Id;
        double roll = town.Rng.Next() * fit.Sum(Weight);
        foreach (var a in fit)
        {
            roll -= Weight(a);
            if (roll < 0) return a.Id;
        }
        return fit[^1].Id;
    }

    internal static Menace Create(Town town, SiteState seat, string kind, string origin, double power, string? leader = null, string? faction = null, string? fixedName = null, string? cunning = null, string? lineage = null)
    {
        var def = KindDef(town, kind);
        var m = new Menace
        {
            Id = town.Saga.Menaces.Count + 1,
            Kind = kind,
            Def = def,
            Leader = leader ?? NewLeader(town, def),
            Origin = origin,
            BornDay = town.Day,
            FactionId = faction,
            FixedName = fixedName,
            Power = power,
            SeenAttempts = seat.Attempts,
        };
        Seat(town, m, seat);
        m.Cunning = cunning ?? CunningFor(town, IntOf(town, seat.Def.Boss.Monster));
        m.Lineage = lineage ?? Annals.ForMenace(town.Content.History, kind, World.RegionOf(town, seat)?.Theme.Id, seat.Def.Foe)?.Id ?? "";
        // A signature foe's master goes by its lineage's names (picked by the seat, not dice, so the stream holds).
        if (leader == null && Annals.Lineage(town.Content.History, m.Lineage) is { Leaders.Count: > 0 } heirs)
            m.Leader = heirs.Leaders[(int)(Voices.Hash(seat.Def.Id) % (uint)heirs.Leaders.Count)];
        m.Ambition = PickAmbition(town, kind, m.Cunning, m.Lineage);
        m.BaseLevel = World.LevelOf(seat);
        m.BornLevel = m.BaseLevel;
        m.Level = m.BaseLevel;
        m.Stage = StageFor(town, m);
        m.Peak = m.Stage;
        m.NextStrikeTick = town.Tick + town.Rng.Range(Rules(town).StrikeEveryDays[0], Rules(town).StrikeEveryDays[1] + 1) * SimTime.HoursPerDay;
        town.Saga.Menaces.Add(m);
        Plots.Arm(town, m);
        return m;
    }

    private static void Seat(Town town, Menace m, SiteState seat)
    {
        m.SeatId = seat.Def.Id;
        m.SeatName = seat.Def.Name;
        m.Region = World.RegionOf(town, seat)?.Name ?? "the hills";
        m.Holdings.Remove(seat.Def.Id);
    }

    /// <summary>Sites free to become a menace's seat or holding.</summary>
    internal static bool Free(Town town, SiteState s) =>
        !s.Cleared && !s.Held && s.Def.Id != town.Content.Enemy.Outpost.Id && s.Def.Id != town.Content.Factions.CryptSite?.Id && !Undercity.IsUnder(town, s) && At(town, s.Def.Id) == null;

    /// <summary>An act brings a new power at its level, and announces it.</summary>
    public static Menace? Seed(Town town, int[] band)
    {
        var pool = town.Sites
            .Where(s => s.Generated && Free(town, s) && World.LevelOf(s) >= band[0] && World.LevelOf(s) <= band[^1])
            .OrderBy(s => s.Def.Id, StringComparer.Ordinal)
            .ToList();
        if (pool.Count == 0)
            pool = town.Sites.Where(s => s.Generated && Free(town, s)).OrderBy(s => Math.Abs(World.LevelOf(s) - band[0])).ThenBy(s => s.Def.Id, StringComparer.Ordinal).Take(1).ToList();
        if (pool.Count == 0) return null;
        var seat = pool[town.Rng.Range(0, pool.Count)];
        var m = Create(town, seat, KindOf(seat), "act", Rules(town).Stages.Rising);
        Turning.Haunt(town, m);
        Announce(town, m, seat);
        return m;
    }

    /// <summary>A new menace led by someone in particular (a fallen hero), seated at the free lair nearest their level. Not yet announced.</summary>
    public static Menace? Rise(Town town, string kind, string leader, int level, string origin)
    {
        var seat = town.Sites
            .Where(s => s.Generated && Free(town, s))
            .OrderBy(s => Math.Abs(World.LevelOf(s) - level))
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        return seat == null ? null : Create(town, seat, kind, origin, Rules(town).Stages.Rising, leader);
    }

    /// <summary>The town hears the menace's name: its seat and region go on the map.</summary>
    internal static void Announce(Town town, Menace m, SiteState seat)
    {
        m.Named = true;
        var region = World.RegionOf(town, seat);
        if (region != null) region.Known = true;
        Spread(town, m, seat, 60);
        string text = Saga.Grim(town) && m.Def.RiseDark != "" ? m.Def.RiseDark : m.Def.Rise;
        string motive = Annals.Motive(town, m);
        string legend = m.Legend == "" ? "" : " " + m.Legend;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, text, m) + legend + (motive == "" ? "" : " " + motive));
    }

    /// <summary>Word of what holds the seat now: the town's belief catches up with the menace's level.</summary>
    private static void Spread(Town town, Menace m, SiteState seat, int trust)
    {
        string who = Rumors.Occupants(town.Content, seat.Def);
        if (seat.Known == SiteKnowledge.Confirmed)
        {
            seat.Believed = who;
            seat.BelievedTier = seat.Def.Tier;
        }
        else Rumors.Hear(town, seat, who, seat.Def.Tier, Math.Max(trust, seat.Trust));
        seat.ThoughtCleared = false;
    }

    /// <summary>Remember when the town first heard of each standing lair; forget it once cleared.</summary>
    private static void Watch(Town town)
    {
        var seen = town.Saga.SeenSince;
        foreach (var s in town.Sites)
        {
            if (s.Cleared) seen.Remove(s.Def.Id);
            else if (s.Known != SiteKnowledge.Unheard && !seen.ContainsKey(s.Def.Id)) seen[s.Def.Id] = town.Tick;
        }
    }

    /// <summary>A full lair the town has known of for long enough, and left alone, festers into a menace.</summary>
    private static void Fester(Town town)
    {
        var r = Rules(town);
        var sg = town.Saga;
        if (town.Day < sg.NextFesterDay) return;
        if (Alive(town).Count(m => m.Origin != FromWarband) >= r.MaxByAct[Math.Min(sg.Act, r.MaxByAct.Length - 1)]) return;
        int since = town.Tick - r.FesterDays * SimTime.HoursPerDay;
        var lairs = town.Content.Lairs;
        // Only trouble within the act's reach festers: the lairs the heroes could have dealt with, and didn't.
        int reach = Saga.CurrentAct(town).Levels[^1];
        var ripe = town.Sites
            .Where(s => Free(town, s) && World.LevelOf(s) <= reach && s.Strength >= lairs.MaxStrength * 0.9 && sg.SeenSince.GetValueOrDefault(s.Def.Id, int.MaxValue) <= since)
            .OrderByDescending(s => s.Attempts)
            .ThenBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (ripe == null) return;
        sg.NextFesterDay = town.Day + r.FesterEveryDays;
        var m = Create(town, ripe, KindOf(ripe), "lair", r.Stages.Rising * 0.5);
        town.Log(LedgerCategory.Rumor, Importance.Routine, $"Travellers say {ripe.Def.Name} has a new master, and that the other lairs of {m.Region} are taking notice.");
    }

    /// <summary>A cult left to sing long enough calls up a crypt, and a menace with it.</summary>
    private static void Cult(Town town)
    {
        var r = Rules(town);
        int since = town.Tick - r.FesterDays * SimTime.HoursPerDay;
        foreach (var f in town.Factions)
        {
            if (!f.Active || f.Lore.Kind != "cult" || f.FoundedTick is not int founded || founded > since) continue;
            if (town.Saga.Menaces.Any(m => m.Alive && m.FactionId == f.Id)) continue;
            var crypt = f.CryptId != null ? Quests.Site(town, f.CryptId) : null;
            if (crypt == null || crypt.Cleared)
            {
                string? woke = Factions.Waken(town);
                if (woke == null) continue;
                town.Log(LedgerCategory.Faith, Importance.Major, woke);
                crypt = Quests.Site(town, f.CryptId!);
            }
            if (crypt == null) continue;
            var m = Create(town, crypt, "cult", "cult", r.Stages.Rising * 0.6 + f.Members.Count * 10, faction: f.Id);
            m.Named = f.Known;
        }
    }

    /// <summary>The warband outlived its act: it festers as a menace, though the war system still drives it.</summary>
    public static Menace Warband(Town town)
    {
        var e = town.Content.Enemy;
        var m = town.Saga.Menaces.FirstOrDefault(x => x.Alive && x.Origin == FromWarband);
        if (m != null) return m;
        var camp = Quests.Site(town, e.Outpost.Id) ?? new SiteState(e.Outpost);
        m = new Menace
        {
            Id = town.Saga.Menaces.Count + 1,
            Kind = "tribe",
            Def = KindDef(town, "tribe"),
            Leader = e.Leader,
            Origin = FromWarband,
            BornDay = town.Day,
            FixedName = e.Name,
            Named = true,
            SeatId = e.Outpost.Id,
            SeatName = e.Outpost.Name,
            BaseLevel = 4,
            Cunning = CunningFor(town, IntOf(town, e.WarlordBattle.Monster)),
            Ambition = "conquest",
            Lineage = Annals.ForMenace(town.Content.History, "tribe", null)?.Id ?? "",
        };
        town.Saga.Menaces.Add(m);
        SyncWarband(town, m);
        return m;
    }

    private static void SyncWarband(Town town, Menace m)
    {
        m.Power = town.Enemy.Strength;
        // The big bad keeps the level it was crowned at, the warband's host as much as any other.
        if (!m.BigBad) m.Level = Math.Max(m.Level, Math.Min(Rules(town).MaxLevel, m.BaseLevel + (int)(m.Power / Rules(town).PerLevel)));
        if (!m.BigBad) m.Stage = StageFor(town, m);
        // Under the yoke, the warlord waits in his camp: burning it is how the town gets at him.
        if ((town.Saga.YokeId == m.Id || m.BigBad) && Quests.Site(town, m.SeatId) is SiteState camp && camp.Cleared && camp.Def.Boss.Monster == town.Content.Enemy.WarlordBattle.Monster)
            Threat.WarlordFalls(town, camp);
    }

    /// <summary>After the Last Stand, a captain keeps a band of the warband together at a lair near town.</summary>
    public static Menace? Remnant(Town town)
    {
        var lair = town.Sites
            .Where(s => Free(town, s) && s.Def.Kind == "tribe")
            .OrderBy(s => s.Def.Distance)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lair == null) return null;
        var m = Create(town, lair, "tribe", "remnant", Rules(town).Stages.Rising * 0.6);
        m.Named = true;
        Spread(town, m, lair, 50);
        town.Log(LedgerCategory.War, Importance.Notable, $"Not every orc fled far. {m.Leader}, one of the warlord's captains, has gathered the survivors at {lair.Def.Name}.");
        return m;
    }

    /// <summary>The finale when the town has beaten every menace: something old wakes in the farthest wilds.</summary>
    public static Menace? Awaken(Town town, FallbackFoe foe)
    {
        var far = town.Sites
            .Where(s => s.Generated && Free(town, s))
            .OrderByDescending(s => s.Def.Distance)
            .ThenByDescending(s => s.Def.Tier)
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (far == null) return null;
        var mind = Rules(town).Cunning.LastOrDefault(c => c.Crown)?.Id;
        var m = Create(town, far, foe.Kind, "awakened", Rules(town).Stages.Overlord, foe.Leader != "" ? foe.Leader : null, cunning: mind, lineage: foe.Lineage != "" ? foe.Lineage : null);
        m.Named = true;
        return m;
    }

    /// <summary>The big bad is crowned: it rises to the finale's level and readies its first march.</summary>
    public static void Ascend(Town town, Menace m)
    {
        var r = Rules(town);
        var f = town.Content.Saga.Finale;
        m.Named = true;
        m.Power = Math.Max(m.Power, r.Stages.Overlord);
        m.Stage = MenaceStage.Overlord;
        m.Peak = MenaceStage.Overlord;
        m.Level = OverlordLevel(town, m);
        m.NextAssaultTick = town.Tick + f.FirstMarchDays * SimTime.HoursPerDay;
        if (m.Origin == FromWarband)
        {
            var en = town.Enemy;
            en.Strength = Math.Max(en.Strength, town.Content.Enemy.Stages.March * 1.2);
            // The warlord waits for the heroes in his war camp.
            Threat.WarlordInCamp(town);
            en.Strength = Math.Max(en.Strength, town.Content.Enemy.Stages.March * 1.2);
            return;
        }
        var seat = Quests.Site(town, m.SeatId);
        if (seat == null) return;
        Empower(town, m, seat, m.Level);
        if (seat.Def.Distance > 1)
        {
            var region = World.RegionOf(town, seat);
            if (region != null) region.Known = true;
        }
        Spread(town, m, seat, 70);
    }

    /// <summary>
    /// The big bad is as strong as it grew while the town let it be, within the finale's band. It does not
    /// rise to meet the town's champions: heroes the town has raised well are what wins the Long Night.
    /// </summary>
    private static int OverlordLevel(Town town, Menace m)
    {
        var f = town.Content.Saga.Finale;
        return Math.Clamp(m.Level, f.OverlordLevel[0], f.OverlordLevel[^1]);
    }

    // Growth -------------------------------------------------------------------------------------

    private static MenaceStage StageFor(Town town, Menace m)
    {
        if (m.BigBad) return MenaceStage.Overlord;
        var st = Rules(town).Stages;
        return m.Power >= st.Dominion ? MenaceStage.Dominion : m.Power >= st.Power ? MenaceStage.Power : m.Power >= st.Rising ? MenaceStage.Rising : MenaceStage.Lair;
    }

    /// <summary>Gold, tribute or souls flow to the menace.</summary>
    public static void Feed(Town town, Menace m, double power)
    {
        if (m.Origin == FromWarband) town.Enemy.Strength += power;
        else m.Power += power;
    }

    private static void Grow(Town town, Menace m, SiteState seat)
    {
        var r = Rules(town);
        var cu = CunningOf(town, m);
        var am = AmbitionOf(town, m);
        double g = r.Growth * cu.Growth * am.Growth * (seat.Known != SiteKnowledge.Unheard ? 1 : r.UnknownGrowth) * (1 + m.Holdings.Count * r.HoldingGrowth) * Plots.Growth(town, m) * Saga.Pace(town) * Storyteller.MenaceGrowth(town) * Seasons.Activity(town, m.Kind);
        if (m.FactionId != null && Factions.Find(town, m.FactionId) is FactionState f && f.Active) g += f.Members.Count * 0.1;
        // Every party that went for it and came back without finishing it makes it bolder, and a proud one angry.
        if (seat.Attempts > m.SeenAttempts)
        {
            g += (seat.Attempts - m.SeenAttempts) * r.FailedDelve;
            m.SeenAttempts = seat.Attempts;
            Trespass(town, m);
            Vengeance.Wrong(town, m, "attempt");
        }
        m.Power += g;
        Ceiling(town, m, cu);

        // A menace outgrows its act by a few levels at most, and a dull one hardly at all; the big bad keeps the level it was crowned at.
        int level = Math.Min(Math.Min(Math.Min(r.MaxLevel, Saga.CurrentAct(town).Levels[^1] + 3), m.BaseLevel + cu.LevelGain), m.BaseLevel + (int)(m.Power / r.PerLevel));
        if (m.BigBad) level = m.Level;
        if (level > seat.Def.Tier)
        {
            m.Level = level;
            Empower(town, m, seat, level);
            if (m.Named) Spread(town, m, seat, 50);
        }
        m.Stage = StageFor(town, m);
        Domain(town, m, seat, cu);
        var before = m.Peak;
        if (m.Stage <= before) return;
        m.Peak = m.Stage;
        if (m.Stage >= MenaceStage.Power && cu.Holdings) Recruit(town, m, seat);
        if (m.Stage == MenaceStage.Rising)
        {
            if (m.FactionId != null && seat.Cleared) Factions.Waken(town);
            Announce(town, m, seat);
        }
        else if (m.Stage == MenaceStage.Power) PowerGrows(town, m, before);
        else if (m.Stage == MenaceStage.Dominion)
        {
            m.Named = true;
            m.NextAssaultTick ??= town.Tick + town.Rng.Range(r.AssaultEveryDays[0], r.AssaultEveryDays[1] + 1) * SimTime.HoursPerDay;
            town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, r.DominionText, m));
        }
    }

    private static void PowerGrows(Town town, Menace m, MenaceStage before)
    {
        var r = Rules(town);
        string old = m.Def.Titles.Count > (int)before ? m.Def.Titles[(int)before].Replace("{leader}", m.Leader).Replace("{seat}", m.SeatName).Replace("{region}", m.Region) : m.Name;
        string holdings = m.Holdings.Count == 0 ? "nothing else" : Expedition.ListNames(m.Holdings.Select(id => Quests.Site(town, id)!.Def.Name).ToList());
        m.Named = true;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, r.PowerText, m).Replace("{oldname}", old).Replace("{holdings}", holdings)));
    }

    /// <summary>The menace takes in the nearest free lair, preferring its own region: a holding, at full strength.</summary>
    private static SiteState? Recruit(Town town, Menace m, SiteState seat)
    {
        var lair = town.Sites
            .Where(s => Free(town, s))
            .OrderBy(s => s.RegionId == seat.RegionId ? 0 : 1)
            .ThenBy(s => Math.Abs(s.Def.Distance - seat.Def.Distance))
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lair == null) return null;
        m.Holdings.Add(lair.Def.Id);
        lair.Strength = town.Content.Lairs.MaxStrength;
        Plots.Stock(town, m, lair);
        return lair;
    }

    /// <summary>The menace takes in another lair as a holding (a plan's supply camp), if one is free.</summary>
    internal static SiteState? TakeHolding(Town town, Menace m) => Quests.Site(town, m.SeatId) is { } seat ? Recruit(town, m, seat) : null;

    /// <summary>The power a stage starts at.</summary>
    private static double Threshold(Town town, MenaceStage stage)
    {
        var st = Rules(town).Stages;
        return stage switch
        {
            MenaceStage.Rising => st.Rising,
            MenaceStage.Power => st.Power,
            MenaceStage.Dominion => st.Dominion,
            MenaceStage.Overlord => st.Overlord,
            _ => 0,
        };
    }

    /// <summary>A dull menace grows only as far as its wits: its power stops short of the next stage.</summary>
    private static void Ceiling(Town town, Menace m, MenaceCunningDef cu)
    {
        if (m.BigBad || cu.MaxStage >= MenaceStage.Dominion) return;
        double cap = Threshold(town, cu.MaxStage + 1) - 1;
        if (m.Power <= cap) return;
        m.Power = cap;
        if (m.Plateaued) return;
        m.Plateaued = true;
        if (m.Named && cu.Plateau != "") town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Saga.Fill(town, cu.Plateau, m)));
    }

    /// <summary>
    /// A patient mind builds a domain: once risen, it brings the nearest free settlement of the wilds under its
    /// sway every so often, with tithes and fear rather than raids. The settlement turns from the town, and the menace grows.
    /// </summary>
    private static void Domain(Town town, Menace m, SiteState seat, MenaceCunningDef cu)
    {
        // Crowned, it stops building and comes for the town.
        if (cu.DomainEveryDays <= 0 || m.Stage < MenaceStage.Rising || m.BigBad) return;
        if (m.NextDomainDay == 0)
        {
            m.NextDomainDay = town.Day + cu.DomainEveryDays;
            return;
        }
        if (town.Day < m.NextDomainDay || m.Domain >= cu.MaxDomain) return;
        m.NextDomainDay = town.Day + cu.DomainEveryDays;
        var s = Neighbour(town, seat, swayed: true);
        if (s == null) return;
        m.Domain++;
        s.Standing = Math.Max(-100, Math.Min(s.Standing, 0) - Rules(town).SettlementHit);
        s.Allied = false;
        m.Power += Rules(town).FailedDelve;
        Shadows.Sway(town, m, s);
        if (cu.Domain != "" && (m.Named || town.Regions[s.RegionId].Known))
            town.Log(LedgerCategory.Rumor, Importance.Notable, Saga.Cap(Saga.Fill(town, cu.Domain, m).Replace("{settlement}", s.Name)));
    }

    /// <summary>The town's heroes came for it, or for its lairs: a menace that only holds its land now wants blood.</summary>
    private static void Trespass(Town town, Menace m)
    {
        var am = AmbitionOf(town, m);
        if (am.Id != "territory") return;
        if (town.Tick >= m.ProvokedUntilTick && m.Named && am.Provoked != "") town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Saga.Fill(town, am.Provoked, m)));
        m.ProvokedUntilTick = town.Tick + am.ProvokeDays * SimTime.HoursPerDay;
        m.NextStrikeTick = Math.Min(m.NextStrikeTick, town.Tick + 2 * SimTime.HoursPerDay);
    }

    /// <summary>A stronger library creature of the menace's kind takes the seat at its new level.</summary>
    private static void Empower(Town town, Menace m, SiteState seat, int level)
    {
        var c = town.Content;
        var lr = c.Lairs;
        double[] cr = lr.BossCr.Length > level ? lr.BossCr[level] : [Math.Max(0.5, level * 0.75), Math.Max(1, level)];
        var types = m.Def.Types;
        var fit = c.Library.Monsters
            .Where(lm => lm.Damage != null && types.Contains(lm.Type) && lm.Cr >= cr[0] * 0.8 && lm.Cr <= cr[^1] * 1.25)
            .OrderBy(lm => lm.Id, StringComparer.Ordinal)
            .ToList();
        var home = fit.Where(lm => lm.Habitats.Any(seat.Def.Habitats.Contains)).ToList();
        if (home.Count > 0) fit = home;
        if (fit.Count == 0)
        {
            seat.Def = seat.Def with { Tier = level };
            return;
        }
        var lead = fit[town.Rng.Range(0, fit.Count)];
        var escorts = c.Wanderers(seat.Def, [0.125, Math.Max(0.25, lead.Cr / 2)]).Where(x => x.Id != lead.Id).ToList();
        var escort = escorts.Count > 0 ? escorts[town.Rng.Range(0, escorts.Count)] : null;
        int gain = Math.Max(0, level - seat.Def.Tier);
        seat.Def = seat.Def with
        {
            Tier = level,
            Kind = m.Kind == "cult" ? "undead" : m.Kind,
            Boss = seat.Def.Boss with
            {
                Monster = c.BossOf(lead.Id).Id,
                Count = [1, 1],
                Escort = escort?.Id,
                EscortCount = escort != null ? [2, 2 + level / 6] : null,
                Text = $"{m.Leader}, a {lead.Name.ToLowerInvariant()}, in the heart of {seat.Def.Name}",
            },
            Loot = [seat.Def.Loot[0] + 40 * gain, seat.Def.Loot[1] + 80 * gain],
        };
        seat.Strength = c.Lairs.MaxStrength;
    }

    // Acting on the world ------------------------------------------------------------------------

    private static void Strike(Town town, Menace m, SiteState seat)
    {
        var r = Rules(town);
        var rng = town.Rng;
        double far = 1 + 0.25 * Math.Max(0, seat.Def.Distance - 1);
        // A menace in its season strikes more often; one out of it keeps to its lair.
        m.NextStrikeTick = town.Tick + (int)Math.Round(rng.Range(r.StrikeEveryDays[0], r.StrikeEveryDays[1] + 1) * far * Storyteller.StrikeEvery(town) / Seasons.Activity(town, m.Kind)) * SimTime.HoursPerDay;
        var am = AmbitionOf(town, m);
        // Left alone, a menace that only holds its land keeps to it.
        if (am.Id == "territory" && town.Tick >= m.ProvokedUntilTick) return;
        // Past its first rise it turns on the peoples of the wilds as often as on the town.
        var victim = m.Stage >= MenaceStage.Power ? Neighbour(town, seat) : null;
        // Its vassal pays to be spared; the peoples of the wilds are not.
        bool sworn = town.Saga.YokeId == m.Id && !town.Saga.Defied;
        if (victim != null && (sworn || rng.Chance(0.5)))
        {
            HitSettlement(town, m, victim);
            return;
        }
        if (sworn) return;
        if (am.Id == "plague" && am.Infect.Count > 0) Infect(town, m, am);
        else if (am.Id == "intrigue" && am.Schemes.Count > 0) Scheme(town, m, am);
        else HitTown(town, m);
    }

    /// <summary>A night visit: a townsperson taken, and the menace grows on what it takes. Defenders at home can keep the door.</summary>
    private static void Infect(Town town, Menace m, MenaceAmbitionDef am)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var people = town.Workers.OrderBy(a => a.Id).ToList();
        if (people.Count == 0) return;
        var v = people[rng.Range(0, people.Count)];
        double hunger = m.Power * r.StrikeShare * (0.7 + rng.Next() * 0.6);
        if (Threat.LocalDefense(town) * (0.5 + Council.WatchStrength(town)) >= hunger)
        {
            var champion = town.Heroes.Where(a => !a.IsAway).OrderByDescending(x => x.Hero!.Level).ThenBy(x => x.Id).FirstOrDefault();
            string by = champion != null ? champion.Name : "the watch";
            m.Power = Math.Max(0, m.Power - hunger * r.RepelLoss);
            town.Log(LedgerCategory.War, Importance.Routine, Saga.Cap(Saga.Fill(town, am.Warded, m).Replace("{victim}", v.Name).Replace("{by}", by)), champion != null ? [champion.Id, v.Id] : [v.Id]);
            town.Tension += 6;
            return;
        }
        string where = v.JobId is int w ? town.Building(w).Name : town.Name;
        // A fever-breaker from the alchemists' shelf pulls them back by morning.
        if (Alchemy.Cure(town) is string cure)
        {
            m.Power = Math.Max(0, m.Power - hunger * r.RepelLoss);
            town.Log(LedgerCategory.War, Importance.Notable, $"{v.Name} of {where} is found at dawn pale and fevered, marked by {m.Leader}. A dose of {cure} breaks the fever by noon.", [v.Id]);
            town.Tension += 6;
            return;
        }
        string text = am.Infect[rng.Range(0, am.Infect.Count)];
        Threat.Kill(town, v);
        m.Power += r.FailedDelve;
        Saga.Scar(town, 1);
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, text, m).Replace("{victim}", v.Name).Replace("{where}", where)), [v.Id]);
        town.Tension += 20;
    }

    /// <summary>Agents, bribes and whispers instead of raiders. A strong watch catches them.</summary>
    private static void Scheme(Town town, Menace m, MenaceAmbitionDef am)
    {
        var r = Rules(town);
        var rng = town.Rng;
        if (rng.Chance(Council.WatchStrength(town)))
        {
            m.Power = Math.Max(0, m.Power - r.FailedDelve);
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Saga.Fill(town, am.Caught, m)));
            return;
        }
        int pick = rng.Range(0, am.Schemes.Count);
        double gold = 0;
        switch (pick % 3)
        {
            case 0:
                gold = Math.Floor(Math.Min(Math.Max(0, town.Treasury) * 0.15, 30 + m.Level * 15));
                if (gold > 0) town.Book(TreasuryAccount.Discrepancies, -gold);
                m.Power += gold * 0.1;
                break;
            case 1:
                town.Mandate = Math.Max(0, town.Mandate - 6);
                break;
            default:
                town.Tension += 25;
                break;
        }
        m.Power += r.FailedDelve * 0.5;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, am.Schemes[pick], m).Replace("{gold}", Js.Str(gold))));
    }

    private static Settlement? Neighbour(Town town, SiteState seat, bool swayed = false)
    {
        var region = World.RegionOf(town, seat);
        if (region == null) return null;
        // A domain takes in only settlements not already cowed.
        return town.Settlements
            .Where(s => !s.Razed && (!swayed || s.Standing > -25))
            .OrderBy(s => s.RegionId == region.Id ? 0 : 1)
            .ThenBy(s => Math.Abs(town.Regions[s.RegionId].Ring - region.Ring))
            .ThenBy(s => World.AngleBetween(town.Regions[s.RegionId].Bearing, region.Bearing))
            .ThenBy(s => s.Id)
            .FirstOrDefault(s => Math.Abs(town.Regions[s.RegionId].Ring - region.Ring) <= 1);
    }

    private static void HitSettlement(Town town, Menace m, Settlement s)
    {
        var r = Rules(town);
        bool known = town.Regions[s.RegionId].Known;
        if (m.Stage >= MenaceStage.Dominion && (s.Standing <= -40 || town.Rng.Chance(0.4)))
        {
            s.Razed = true;
            s.Allied = false;
            s.Standing = -100;
            Saga.Scar(town, 5);
            town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, m.Def.Raze, m).Replace("{people}", s.People).Replace("{settlement}", s.Name)));
            Saga.Refugees(town, s);
            return;
        }
        s.Standing = Math.Max(-100, s.Standing - r.SettlementHit);
        if (s.Allied && s.Standing < 0) s.Allied = false;
        if (known) town.Log(LedgerCategory.War, Importance.Notable, $"Runners from {s.Name} say {m.Name} has fallen on {s.People}. They ask why {town.Name}'s heroes did not come.");
        Reigns.Struck(town, s, m);
    }

    /// <summary>Very hostile: the big bad, an enemy in crisis, or one that wants the town's walls or its flesh. Only these burn what they raid.</summary>
    public static bool Hostile(Town town, Menace m) => m.BigBad || Crises.Of(town, m) != null || AmbitionOf(town, m).Id is "conquest" or "hunger";

    /// <summary>The side of town a building stands on (a wall section).</summary>
    private static int Side(Town town, Building b) => Garrison.SectionAt(town, town.Lot(b.LotId).X, town.Lot(b.LotId).Y);

    /// <summary>Something the menace will hold against the town; its card shows the last few.</summary>
    public static void Remember(Menace m, string what)
    {
        if (what == "" || m.Memories.LastOrDefault() == what) return;
        m.Memories.Add(what);
        if (m.Memories.Count > 4) m.Memories.RemoveAt(0);
    }

    /// <summary>Daily: the latest Ledger news naming each known menace becomes the last the town heard of it.</summary>
    public static void Track(Town town)
    {
        var entries = town.Ledger.Entries;
        var known = town.Saga.Menaces.Where(m => m.Alive && (m.Named || m.Origin == FromWarband)).OrderBy(m => m.Id).ToList();
        for (int i = Math.Max(0, town.Saga.HeardUpTo); i < entries.Count; i++)
        {
            var e = entries[i];
            if (e.Category is not (LedgerCategory.War or LedgerCategory.Rumor or LedgerCategory.Faith)) continue;
            foreach (var m in known)
                if (e.Text.Contains(m.Name, StringComparison.Ordinal) || (m.Leader != "" && e.Text.Contains(m.Leader, StringComparison.Ordinal)))
                {
                    m.LastHeard = e.Text;
                    m.LastHeardDay = SimTime.DayOf(e.Tick);
                }
        }
        town.Saga.HeardUpTo = entries.Count;
    }

    /// <summary>A raid of a crisis (<see cref="Crises"/>), this many strong. Returns whether it got through.</summary>
    internal static bool Raid(Town town, Menace m, double raiders) => HitTown(town, m, raiders);

    private static bool HitTown(Town town, Menace m, double? strength = null)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var target = town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" || b.Def.Zone == Zone.Commercial))
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .Take(4)
            .ToList();
        target = Garrison.Unguarded(town, target).Where(b => Threat.Exposed(town, b)).ToList();
        // Turned back on one side last time, it tries another.
        if (m.RepulsedAt is int side && target.Any(b => Side(town, b) != side)) target = target.Where(b => Side(town, b) != side).ToList();
        var where = target.Count > 0 ? target[rng.Range(0, target.Count)] : null;
        double raiders = (strength ?? m.Power * r.StrikeShare) * (0.7 + rng.Next() * 0.6) * (1 + town.Content.Lairs.RebuffBand * Math.Min(town.Content.Lairs.MaxRebuffs, m.Repulsed));
        // Nothing beyond the walls it could take tonight: it does not waste its warriors.
        if (!Threat.WorthRaiding(town, where, raiders)) return false;
        string place = where!.Name;
        Passage.Alarm(town, where);
        // A sky watch sees them come over the hills in time for the watch to turn out: they meet them at the fence.
        double warned = 1 + Institutions.SkyWatch(town) / 48.0 * town.Content.Enemy.Defense.SkyWatch;
        if (warned > 1) town.Log(LedgerCategory.War, Importance.Routine, $"{Saga.Cap(Sightings.SkyRider(town))} sees {m.Name}'s raiders coming for {place} and rings the alarm.");
        if (Threat.LocalDefense(town) * Garrison.FlagFactor(town, where) * warned >= raiders)
        {
            m.Power = Math.Max(0, m.Power - raiders * r.RepelLoss);
            var champion = town.Heroes.Where(a => !a.IsAway).OrderByDescending(x => x.Hero!.Level).ThenBy(x => x.Id).FirstOrDefault();
            string by = champion != null ? $"{champion.Name} and the watch" : "the watch";
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Saga.Fill(town, r.Repelled, m).Replace("{where}", place).Replace("{by}", by)), champion != null ? [champion.Id] : []);
            m.Repulsed++;
            m.RepulsedAt = Side(town, where);
            Remember(m, $"driven off at {place} by {by}");
            Sheet.Repelled(town);
            town.Tension += 6;
            return false;
        }
        m.Repulsed = 0;
        m.RepulsedAt = null;
        var d = town.Content.Enemy.Defense;
        bool dark = rng.Chance(Saga.Grim(town) ? d.RaidKill * 2 : d.RaidKill), burned = false;
        var victims = new List<int>();
        {
            double loot = Math.Floor(where.Cash * 0.5);
            where.Cash -= loot;
            where.Stock[Good.Food] = Math.Floor(where.Stock.Get(Good.Food, 0) * 0.4);
            var workers = where.Workers.Select(town.Agent).Where(a => !a.Departed).OrderBy(a => a.Id).ToList();
            if (dark && workers.Count > 0)
            {
                var v = workers[rng.Range(0, workers.Count)];
                Threat.Kill(town, v);
                victims.Add(v.Id);
            }
            // Only a foe that means the town real harm puts it to the torch.
            if (Hostile(town, m) && rng.Chance(d.RaidBurn) && Blows.Burns(where))
            {
                Blows.Wreck(town, where, 0.4 + rng.Next() * 0.3);
                burned = true;
            }
        }
        var lines = victims.Count > 0 && m.Def.StrikeDark.Count > 0 ? m.Def.StrikeDark : m.Def.Strike;
        string text = lines.Count > 0 ? lines[rng.Range(0, lines.Count)] : "{name} strikes at {where}.";
        string fire = burned ? $" They leave {place} burning behind them." : "";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, text, m).Replace("{where}", place)) + fire, victims);
        if (burned) Fires.Spread(town, where);
        Sightings.Report(town, $"raid:{m.Id}", Sightings.Flight, m.Name, (int)raiders, m.SeatId, where.Id, null, 0.25, SimTime.HoursPerDay, "");
        town.Tension += 15 + victims.Count * 10;
        town.Mandate = Math.Max(0, town.Mandate - 3);
        return true;
    }

    /// <summary>A crisis culminates (<see cref="Crises"/>): its host sets out for the walls today, whatever its wants or its odds.</summary>
    internal static void MarchNow(Town town, Menace m)
    {
        var f = town.Content.Saga.Finale;
        int march = Rules(town).MarchDays + Seasons.MarchDays(town);
        m.ArrivalTick = town.Tick + march * SimTime.HoursPerDay;
        m.Approach = Garrison.Approach(town, 1000 + m.Id * 31 + m.Marches);
        m.Marches++;
        string army = $"{Js.Str(Js.Round(m.Power / 10) * 10)} {m.Def.People}";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, f.March, m).Replace("{army}", army).Replace("{days}", march.ToString())) + $" Scouts say it will come at the {Garrison.Place(town, m.Approach)}.");
    }

    /// <summary>Its host turns back before the walls (joined another, or driven off by one): it marches again in its own time.</summary>
    internal static void Recall(Town town, Menace m)
    {
        var r = Rules(town);
        m.ArrivalTick = null;
        m.NextAssaultTick = town.Tick + town.Rng.Range(r.AssaultEveryDays[0], r.AssaultEveryDays[1] + 1) * SimTime.HoursPerDay;
    }

    /// <summary>A rival broke it in the field and took its warriors (<see cref="Crises"/>).</summary>
    internal static void Swallowed(Town town, Menace m)
    {
        if (!m.Alive) return;
        m.EndedDay = town.Day;
        m.Holdings.Clear();
        m.Assets.Clear();
        m.Plan = null;
        m.ArrivalTick = null;
        Saga.Relief(town);
        Saga.Unyoke(town, m);
    }

    /// <summary>A town that stopped paying its tribute: the overlord marches at once.</summary>
    public static void Provoke(Town town, Menace m)
    {
        if (m.Origin == FromWarband) return;
        m.NextAssaultTick = town.Tick;
    }

    private static void Assaults(Town town, Menace m)
    {
        var r = Rules(town);
        var f = town.Content.Saga.Finale;
        var s = town.Saga;
        bool restrained = s.YokeId == m.Id && !s.Defied;
        if (m.ArrivalTick is int arrival)
        {
            if (town.Tick >= arrival) Assault(town, m);
            return;
        }
        if (restrained || m.NextAssaultTick is not int next || town.Tick < next) return;
        bool due = m.BigBad && town.Day >= s.CrownDay + f.FirstMarchDays;
        bool provoked = s.YokeId == m.Id && s.Defied;
        var am = AmbitionOf(town, m);
        // Only a menace that wants the town's walls comes for them, until it is crowned or defied.
        if (!due && !provoked && !m.BigBad && !am.Marches) return;
        // A would-be overlord asks before it marches.
        if (m.DemandDueTick != null && !m.Refused) return;
        if (!due && !provoked && m.Power < Threat.Defense(town, allies: false) * r.AssaultConfidence)
        {
            m.NextAssaultTick = town.Tick + 10 * SimTime.HoursPerDay;
            return;
        }
        int march = r.MarchDays + Seasons.MarchDays(town);
        m.ArrivalTick = town.Tick + march * SimTime.HoursPerDay;
        m.Approach = Garrison.Approach(town, 1000 + m.Id * 31 + m.Marches);
        // The big bad gathers a greater host for every march: the finale's doom clock.
        if (m.BigBad)
        {
            double d = Threat.Defense(town, allies: false), more = f.MarchGrowth * m.Marches;
            m.Power = Math.Clamp(m.Power, d * (f.MarchArmy[0] + more), d * (f.MarchArmy[^1] + more));
        }
        m.Marches++;
        string army = $"{Js.Str(Js.Round(m.Power / 10) * 10)} {m.Def.People}";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, f.March, m).Replace("{army}", army).Replace("{days}", march.ToString())) + $" Scouts say it will come at the {Garrison.Place(town, m.Approach)}.");
    }

    /// <summary>The menace's host at the walls: three waves, then its leader against the heroes at home, as at the Last Stand.</summary>
    private static void Assault(Town town, Menace m)
    {
        var r = Rules(town);
        var f = town.Content.Saga.Finale;
        var rng = town.Rng;
        m.ArrivalTick = null;
        m.NextAssaultTick = town.Tick + rng.Range(r.AssaultEveryDays[0], r.AssaultEveryDays[1] + 1) * SimTime.HoursPerDay;
        var lines = new List<string>();
        Garrison.Sync(town);
        double def = Threat.Defense(town);
        double startDef = def;
        double army = m.Power;
        int breaches = 0;
        army = Crises.Converge(town, m, army, lines);
        if (army > 0) army += Powers.JoinHost(town, m, lines);
        if (army <= 0) return;
        lines.Add($"{Saga.Cap(m.Name)}, some {Js.Str(Js.Round(army))} strong, comes against {town.Name}. The defenders muster {Js.Str(Js.Round(startDef))} strong.");
        var record = Battles.Begin(town, $"menace:{m.Id}", m.Kind, m.Name, m.Def.People, m.Leader, m.Approach, army, startDef);
        def = Math.Max(0, def - Loyalty.Betray(town, m.Name, m, lines));
        string foe = Quests.Site(town, m.SeatId)?.Def.Foe ?? "";
        double gate = Cannons.Wagons(town, m.Kind, m.Marches - 1, Garrison.WaveSection(town, m.Approach, 1), lines, record);
        for (int w = 1; w <= 3 && breaches < 2; w++)
        {
            double wave = (army / (3 - w + 1)) * (0.8 + rng.Next() * 0.4);
            int at = Garrison.WaveSection(town, m.Approach, w);
            double hold = (def / (3 - w + 1)) * (0.8 + rng.Next() * 0.4) * Garrison.SectionFactor(town, at) * gate;
            double shot = Cannons.Volley(town, m.Kind, foe, wave, w, lines);
            army -= wave - shot;
            wave = shot;
            double wading = Garrison.Wade(town, wave, Passage.Fliers(town, m.Kind));
            if (hold >= wading)
            {
                def -= wading * 0.15;
                army -= wave;
                Battles.Wave(record, at, wave, false);
                lines.Add($"Wave {w}: the {m.Def.People} of {m.Leader} hit the {Garrison.Place(town, at)} and break against it.");
            }
            else
            {
                def -= wading * 0.35;
                army -= wave * 0.5;
                breaches++;
                Battles.Wave(record, at, wave, true);
                lines.Add($"Wave {w}: the {m.Def.People} of {m.Leader} breach the {Garrison.Place(town, at)}. Fighting spills into the streets.");
            }
        }
        m.Power = Math.Max(0, army);
        Crises.Assaulted(town, m, breaches >= 2);
        int fallen = (int)Math.Min(town.Workers.Count, Js.Round(((startDef - def) / Math.Max(1, Threat.Fortification(town))) / 12));
        var dead = new List<Agent>();
        var workers = town.Workers;
        for (int i = 0; i < fallen; i++)
        {
            int k = rng.Range(0, workers.Count);
            var v = workers[k];
            workers.RemoveAt(k);
            Threat.Kill(town, v);
            dead.Add(v);
        }
        if (dead.Count > 0) lines.Add($"{Expedition.ListNames(dead.Select(a => a.Name).ToList())} {(dead.Count == 1 ? "falls" : "fall")} on the walls.");
        record.Fallen.AddRange(dead.Select(a => a.Id));
        town.Watch = Math.Max(0, town.Watch - breaches * 2);
        town.Tension += 60 + dead.Count * 5;

        if (breaches >= 2)
        {
            record.End = BattleEnd.Fell;
            if (Saga.Sack(town, m, lines))
            {
                record.End = BattleEnd.Sacked;
                Chronicle(town, lines, ExpeditionOutcome.Wiped, []);
                town.Log(LedgerCategory.War, Importance.Major, $"{town.Name} is sacked by {m.Name}. (Chronicle {town.Chronicles.Count})", dead.Select(a => a.Id).ToList());
                return;
            }
            string fall = Saga.Fill(town, f.Defeat, m);
            lines.Add(fall);
            Chronicle(town, lines, ExpeditionOutcome.Wiped, []);
            town.Book(TreasuryAccount.Discrepancies, -town.Treasury);
            town.Mandate = 0;
            Saga.Fall(town, fall);
            town.Log(LedgerCategory.War, Importance.Major, $"{fall} (Chronicle {town.Chronicles.Count})", dead.Select(a => a.Id).ToList());
            return;
        }

        var champions = town.Heroes.Where(a => !a.IsAway && a.Hero!.Hp > 0).OrderByDescending(x => x.Hero!.Level).ThenBy(x => x.Id).Take(4).ToList();
        var seat = Quests.Site(town, m.SeatId);
        bool slain = false;
        if (champions.Count > 0 && seat != null)
        {
            lines.Add($"The walls hold. {m.Leader} comes to the breach, and {Expedition.ListNames(champions.Select(a => a.GivenName).ToList())} go out to meet them.");
            var battle = new SiteDef { Id = "assault", Name = "the walls", Tier = 0, Distance = 0, Encounters = [], Boss = seat.Def.Boss, Loot = [0, 0] };
            var result = Expedition.Run(rng, town.Content, battle, champions);
            record.Champions.AddRange(champions.Select(c => c.Id));
            record.ChampionsFell.AddRange(result.Dead.Order());
            lines.AddRange(result.Lines.Skip(1).Where(l =>
                !l.StartsWith("At last", StringComparison.Ordinal) &&
                !l.Contains("is cleared. The hoard", StringComparison.Ordinal) &&
                !l.StartsWith("No one comes back", StringComparison.Ordinal)));
            foreach (int id in result.Dead)
            {
                var h = town.Agent(id);
                h.Dead = true;
                Population.Depart(town, h);
            }
            foreach (var c in champions)
            {
                if (result.Dead.Contains(c.Id)) continue;
                Notoriety.Deed(town, c, "slay", 10);
                c.Hero!.Kills += result.Kills.Get(c.Id, 0);
            }
            slain = result.Outcome == ExpeditionOutcome.Cleared;
        }
        else lines.Add($"The walls hold, but no hero is in town to face {m.Leader}.");

        Chronicle(town, lines, slain ? ExpeditionOutcome.Cleared : ExpeditionOutcome.Retreated, champions);
        record.End = !slain ? (record.Champions.Count > 0 ? BattleEnd.Repelled : BattleEnd.Held) : m.BigBad && seat != null && MayEscape(town) ? BattleEnd.Carried : BattleEnd.Slain;
        if (slain && m.BigBad && seat != null && MayEscape(town))
        {
            // Beaten at the walls, the big bad is carried off the field and back to its seat to heal.
            town.Saga.BigBadEscapes++;
            m.Power *= r.AssaultRetreat * f.EscapeLoss;
            town.Log(LedgerCategory.War, Importance.Major, $"{m.Leader} is struck down before the walls of {town.Name}, but the host carries its master off the field, back to {seat.Def.Name}. (Chronicle {town.Chronicles.Count})", champions.Select(a => a.Id).ToList());
            return;
        }
        if (slain)
        {
            town.Log(LedgerCategory.War, Importance.Major, $"{m.Leader} falls before the walls of {town.Name}! (Chronicle {town.Chronicles.Count})", champions.Select(a => a.Id).ToList());
            End(town, m);
            return;
        }
        m.Power *= r.AssaultRetreat;
        town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(Saga.Fill(town, f.Repelled, m))} (Chronicle {town.Chronicles.Count})", champions.Select(a => a.Id).ToList());
        Sheet.Repelled(town);
    }

    private static void Chronicle(Town town, List<string> lines, ExpeditionOutcome outcome, List<Agent> champions)
    {
        int day = SimTime.DayOf(town.Tick);
        town.Chronicles.Add(new Chronicle(town.Chronicles.Count + 1, $"Defenders of {town.Name}", $"the walls of {town.Name}", champions.Select(a => a.Describe()).ToList(), day, day, outcome, lines));
    }

    // Losses -------------------------------------------------------------------------------------

    /// <summary>Holdings the heroes have cleared fall away, and take power with them.</summary>
    private static void Holdings(Town town, Menace m)
    {
        var r = Rules(town);
        foreach (var id in m.Holdings.ToList())
        {
            if (!Quests.Site(town, id)!.Cleared) continue;
            Plots.Seize(town, m, Quests.Site(town, id)!);
            var raiders = town.Parties.Where(p => p.SiteId == id).OrderByDescending(p => p.DepartTick).ThenByDescending(p => p.Id).FirstOrDefault()?.Members;
            m.Holdings.Remove(id);
            m.Power = Math.Max(0, m.Power - r.HoldingCleared);
            Trespass(town, m);
            Vengeance.Wrong(town, m, "holding", heroes: raiders ?? []);
        }
    }

    /// <summary>Heroes cleared the seat: the big bad dies; with holdings left the menace is broken and moves; otherwise it ends.</summary>
    private static void SeatFalls(Town town, Menace m, SiteState seat)
    {
        var r = Rules(town);
        if (m.BigBad && Escapes(town, m, seat)) return;
        if (Turning.Spared(town, m)) return;
        var refuge = m.BigBad ? null : m.Holdings.Select(id => Quests.Site(town, id)!).Where(s => !s.Cleared).OrderByDescending(s => s.Def.Tier).ThenBy(s => s.Def.Id, StringComparer.Ordinal).FirstOrDefault();
        if (refuge == null && !m.BigBad && m.FactionId != null && Factions.Find(town, m.FactionId) is FactionState f && f.Active)
        {
            // The choir in town lives: it will call up another hall.
            m.Power *= r.SeatBroken;
            m.Stage = StageFor(town, m);
            if (m.Named) town.Log(LedgerCategory.War, Importance.Notable, Saga.Fill(town, r.Rootless, m));
            m.SeenAttempts = seat.Attempts;
            seat.Cleared = false;
            seat.Def = seat.Def with { Tier = Math.Max(1, seat.Def.Tier - r.BrokenLevels) };
            seat.QuietUntil = town.Tick + r.FesterDays * SimTime.HoursPerDay;
            return;
        }
        if (refuge == null)
        {
            Plots.LastStand(town, m, seat);
            End(town, m);
            return;
        }
        string fled = seat.Def.Name;
        string old = m.Name;
        string oldLeader = m.Leader;
        var breakers = town.Parties.Where(p => p.SiteId == seat.Def.Id).OrderByDescending(p => p.DepartTick).ThenByDescending(p => p.Id).FirstOrDefault()?.Members ?? [];
        bool escaped = town.Rng.Chance(0.5);
        if (!escaped) m.Leader = NewLeader(town, m.Def);
        Plots.SeatFell(town, m, seat, refuge, escaped);
        Seat(town, m, refuge);
        m.Power *= r.SeatBroken;
        m.BaseLevel = Math.Max(1, m.BaseLevel - r.BrokenLevels);
        m.Level = Math.Max(m.BaseLevel, m.Level - r.BrokenLevels);
        m.Stage = StageFor(town, m);
        m.Peak = m.Stage;
        m.SeenAttempts = refuge.Attempts;
        Empower(town, m, refuge, Math.Max(World.LevelOf(refuge), m.Level));
        Spread(town, m, refuge, 50);
        Saga.Relief(town);
        string text = escaped ? r.Broken : r.Succeeded;
        town.Log(
            LedgerCategory.War,
            Importance.Major,
            Saga.Cap(text.Replace("{leader}", oldLeader).Replace("{heir}", m.Leader).Replace("{seat}", fled).Replace("{refuge}", refuge.Def.Name).Replace("{name}", old)));
        Vengeance.Wrong(town, m, "broken", heroes: breakers);
    }

    /// <summary>
    /// The big bad driven from its seat gets away while it has escapes left or the Long Night is young: to its
    /// strongest holding, or back to the same seat once it has regrouped. Returns false when this was the end.
    /// </summary>
    private static bool Escapes(Town town, Menace m, SiteState seat)
    {
        var s = town.Saga;
        var f = town.Content.Saga.Finale;
        if (!MayEscape(town)) return false;
        s.BigBadEscapes++;
        string fled = seat.Def.Name;
        var refuge = m.Holdings.Select(id => Quests.Site(town, id)!).Where(x => !x.Cleared).OrderByDescending(x => x.Def.Tier).ThenBy(x => x.Def.Id, StringComparer.Ordinal).FirstOrDefault();
        if (refuge != null)
        {
            Plots.SeatFell(town, m, seat, refuge, true);
            Seat(town, m, refuge);
        }
        else
        {
            refuge = seat;
            seat.Cleared = false;
            seat.QuietUntil = town.Tick + f.RegroupDays * SimTime.HoursPerDay;
        }
        m.SeenAttempts = refuge.Attempts;
        m.Power *= f.EscapeLoss;
        Empower(town, m, refuge, m.Level);
        string text = refuge == seat ? f.Regroup : f.Escape;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, text, m).Replace("{fled}", fled).Replace("{refuge}", refuge.Def.Name)));
        return true;
    }

    /// <summary>
    /// The warlord crowned big bad gets out of his burning war camp as any big bad would: the warband
    /// thins, regroups at the camp, and comes again. Returns false when this was the end.
    /// </summary>
    public static bool WarlordEscapes(Town town, SiteState camp)
    {
        var m = town.Saga.Menaces.FirstOrDefault(x => x.Alive && x.BigBad && x.Origin == FromWarband);
        if (m == null || !MayEscape(town)) return false;
        var f = town.Content.Saga.Finale;
        town.Saga.BigBadEscapes++;
        camp.Cleared = false;
        camp.QuietUntil = town.Tick + f.RegroupDays * SimTime.HoursPerDay;
        town.Enemy.Strength *= f.EscapeLoss;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, f.Regroup, m).Replace("{fled}", camp.Def.Name).Replace("{refuge}", camp.Def.Name)));
        return true;
    }

    /// <summary>Whether a beaten big bad still gets away: it has escapes left, or the Long Night is too young to end.</summary>
    private static bool MayEscape(Town town)
    {
        var f = town.Content.Saga.Finale;
        return town.Saga.BigBadEscapes < f.Escapes || town.Day < town.Saga.CrownDay + f.MinDays;
    }

    /// <summary>The menace is finished. Its holdings go back to being ordinary lairs.</summary>
    public static void End(Town town, Menace m)
    {
        if (!m.Alive) return;
        m.EndedDay = town.Day;
        m.Holdings.Clear();
        m.Assets.Clear();
        m.Plan = null;
        Saga.Relief(town);
        // The warlord's own death is told by Threat.
        if (m.Named && !m.BigBad && !m.Redeemed && m.Origin != FromWarband && m.Def.Slain != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, m.Def.Slain, m)));
        Saga.Unyoke(town, m);
        if (m.BigBad) Saga.Win(town, m);
    }

    // Demands -----------------------------------------------------------------------------------

    /// <summary>Once it is a power, a would-be overlord sends heralds: kneel and pay, or face it. No answer is an answer.</summary>
    private static void Demand(Town town, Menace m)
    {
        var am = AmbitionOf(town, m);
        if (am.Id != "vassal" || m.BigBad || m.Refused || town.Saga.YokeId != null) return;
        if (m.DemandDueTick is not int answer)
        {
            if (PendingDemand(town) != null) return;
            m.DemandDueTick = town.Tick + am.AnswerDays * SimTime.HoursPerDay;
            m.Named = true;
            town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, am.Demand, m)));
            return;
        }
        if (town.Tick >= answer) Answer(town, m.Id, false, answered: false);
    }

    /// <summary>A would-be overlord whose demand for fealty waits on the town's answer.</summary>
    public static Menace? PendingDemand(Town town) =>
        town.Saga.Menaces.Where(m => m.Alive && m.DemandDueTick != null && !m.Refused && town.Saga.YokeId != m.Id).OrderBy(m => m.Id).FirstOrDefault();

    /// <summary>The mayor answers a demand for fealty: bend the knee and pay tribute, or refuse and face its host.</summary>
    /// <summary>Answer a menace's demand; answered (not left to lapse), the mayor rolls to kneel with dignity or to refuse so it gives them pause.</summary>
    public static bool Answer(Town town, int menaceId, bool submit, bool answered = true)
    {
        if (menaceId < 1 || menaceId > town.Saga.Menaces.Count) return false;
        var m = town.Saga.Menaces[menaceId - 1];
        if (PendingDemand(town) != m) return false;
        var am = AmbitionOf(town, m);
        m.DemandDueTick = null;
        if (submit && town.Saga.YokeId == null)
        {
            Saga.Swear(town, m);
            town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, am.Submitted, m)));
            // Kneeling with dignity keeps the town's respect.
            if (answered) town.Mandate = Math.Min(100, town.Mandate + Sheet.Gain(town, "submit", Sheet.Decide(town, "submit")));
            return true;
        }
        m.Refused = true;
        Vengeance.Wrong(town, m, "refused", heroes: []);
        // Its raiders come at once; its host, once it can field one.
        m.NextStrikeTick = Math.Min(m.NextStrikeTick, town.Tick + 2 * SimTime.HoursPerDay);
        if (m.Stage >= MenaceStage.Dominion) m.NextAssaultTick = town.Tick;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, am.Refused, m)));
        if (answered) m.Power *= 1 - Sheet.Gain(town, "refuse", Sheet.Decide(town, "refuse"));
        return true;
    }

    /// <summary>The stand-in mayor kneels only to a host the walls could not hold.</summary>
    private static void AutoAnswer(Town town)
    {
        if (PendingDemand(town) is not Menace m) return;
        Answer(town, m.Id, m.Power > Threat.Defense(town, allies: false) * 1.2);
    }

    // Feuds --------------------------------------------------------------------------------------

    /// <summary>Menaces with a reach fight their neighbours: the stronger takes a lair, or swallows a weak one whole.</summary>
    private static void Feuds(Town town)
    {
        var fr = Rules(town).Feud;
        foreach (var m in Alive(town))
        {
            if (!m.Alive || m.Origin == FromWarband || m.Stage < MenaceStage.Power || !AmbitionOf(town, m).Marches) continue;
            if (m.NextFeudDay == 0) m.NextFeudDay = town.Day + town.Rng.Range(fr.EveryDays[0], fr.EveryDays[1] + 1);
            if (town.Day < m.NextFeudDay) continue;
            m.NextFeudDay = town.Day + town.Rng.Range(fr.EveryDays[0], fr.EveryDays[1] + 1);
            var seat = Quests.Site(town, m.SeatId);
            if (seat == null) continue;
            var region = World.RegionOf(town, seat);
            if (region == null) continue;
            var rival = Alive(town)
                .Where(o => o != m && !o.BigBad && o.Origin != FromWarband && town.Saga.YokeId != o.Id && o.Power < m.Power)
                .Select(o => (Menace: o, Region: Quests.Site(town, o.SeatId) is SiteState os ? World.RegionOf(town, os) : null))
                .Where(x => x.Region != null && Math.Abs(x.Region.Ring - region.Ring) <= 1 && World.AngleBetween(x.Region.Bearing, region.Bearing) <= 45)
                .OrderBy(x => x.Region!.Id == region.Id ? 0 : 1)
                .ThenBy(x => x.Menace.Id)
                .Select(x => x.Menace)
                .FirstOrDefault();
            if (rival == null) continue;
            bool heard = region.Known;
            if (rival.Holdings.Count == 0 && m.Power >= rival.Power * fr.Absorb)
            {
                m.Power += rival.Power * 0.5;
                string other = rival.Name;
                rival.EndedDay = town.Day;
                Saga.Relief(town);
                if (heard && fr.Absorbed != "") town.Log(LedgerCategory.Rumor, Importance.Notable, Saga.Cap(Saga.Fill(town, fr.Absorbed, m).Replace("{other}", other)));
                continue;
            }
            if (rival.Holdings.Count == 0) continue;
            var cu = CunningOf(town, m);
            if (!cu.Holdings) continue;
            string lair = rival.Holdings.OrderBy(id => id, StringComparer.Ordinal).First();
            rival.Holdings.Remove(lair);
            rival.Power = Math.Max(0, rival.Power - Rules(town).HoldingCleared);
            m.Holdings.Add(lair);
            m.Power += rival.Power * fr.Take * 0.2;
            if (heard && fr.Taken != "")
                town.Log(LedgerCategory.Rumor, Importance.Notable, Saga.Cap(Saga.Fill(town, fr.Taken, m).Replace("{other}", rival.Name).Replace("{lair}", Quests.Site(town, lair)!.Def.Name)));
        }
    }

    // The stand-in mayor -------------------------------------------------------------------------

    /// <summary>Put a price on the worst menace the town's heroes could face: the yoke first, then the big bad.</summary>
    private static void AutoBounty(Town town)
    {
        var r = Rules(town);
        int champ = Saga.ChampionLevel(town);
        int? yoke = town.Saga.YokeId;
        var target = Alive(town)
            .Where(m => (m.Origin != FromWarband || m.Id == yoke) && (m.Stage >= MenaceStage.Rising || m.Id == yoke) && town.Day >= m.NextBountyDay)
            .Select(m => (Menace: m, Seat: Quests.Site(town, m.SeatId)))
            .Where(x => x.Seat != null && x.Seat.Known != SiteKnowledge.Unheard && !x.Seat.Cleared && champ >= x.Seat.Def.Tier - 1)
            .OrderByDescending(x => x.Menace.Id == yoke)
            .ThenByDescending(x => x.Menace.BigBad)
            .ThenByDescending(x => Crises.Of(town, x.Menace) != null)
            .ThenByDescending(x => x.Menace.Stage)
            .ThenByDescending(x => x.Menace.Power)
            .ThenBy(x => x.Menace.Id)
            .FirstOrDefault();
        if (target.Seat == null) return;
        if (town.Bounties.Any(b => b.SiteId == target.Seat.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) return;
        int level = Math.Max(1, target.Seat.Def.Tier);
        double reward = Math.Min(Quests.Asking(town, level) + r.BountyBonus, Math.Floor(town.Treasury * 0.6));
        if (reward < 40 * level) return;
        if (Quests.PostBounty(town, target.Seat.Def.Id, reward) != null) target.Menace.NextBountyDay = town.Day + r.BountyEveryDays;
    }
}
