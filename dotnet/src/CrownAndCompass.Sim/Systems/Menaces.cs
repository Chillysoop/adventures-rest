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
    public int SeenAttempts { get; set; }

    /// <summary>Times its host has marched on the town.</summary>
    public int Marches { get; set; }
    public bool BigBad { get; set; }
    public int? EndedDay { get; set; }
    public string? FactionId { get; init; }

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
            if (m.Stage >= MenaceStage.Rising && town.Tick >= m.NextStrikeTick) Strike(town, m, seat);
            if (m.Stage >= MenaceStage.Dominion) Assaults(town, m);
            if (town.Outcome != null) return;
        }
        Watch(town);
        Fester(town);
        Cult(town);
        if (autoMayor) AutoBounty(town);
    }

    // Birth --------------------------------------------------------------------------------------

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

    private static Menace Create(Town town, SiteState seat, string kind, string origin, double power, string? leader = null, string? faction = null, string? fixedName = null)
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
        m.BaseLevel = World.LevelOf(seat);
        m.Level = m.BaseLevel;
        m.Stage = StageFor(town, m);
        m.Peak = m.Stage;
        m.NextStrikeTick = town.Tick + town.Rng.Range(Rules(town).StrikeEveryDays[0], Rules(town).StrikeEveryDays[1] + 1) * SimTime.HoursPerDay;
        town.Saga.Menaces.Add(m);
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
    private static bool Free(Town town, SiteState s) =>
        !s.Cleared && !s.Held && s.Def.Id != town.Content.Enemy.Outpost.Id && s.Def.Id != town.Content.Factions.CryptSite?.Id && At(town, s.Def.Id) == null;

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
        Announce(town, m, seat);
        return m;
    }

    /// <summary>The town hears the menace's name: its seat and region go on the map.</summary>
    private static void Announce(Town town, Menace m, SiteState seat)
    {
        m.Named = true;
        var region = World.RegionOf(town, seat);
        if (region != null) region.Known = true;
        Spread(town, m, seat, 60);
        string text = Saga.Grim(town) && m.Def.RiseDark != "" ? m.Def.RiseDark : m.Def.Rise;
        town.Log(LedgerCategory.War, Importance.Major, Saga.Fill(town, text, m));
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
        };
        town.Saga.Menaces.Add(m);
        SyncWarband(town, m);
        return m;
    }

    private static void SyncWarband(Town town, Menace m)
    {
        m.Power = town.Enemy.Strength;
        m.Level = Math.Max(m.Level, Math.Min(Rules(town).MaxLevel, m.BaseLevel + (int)(m.Power / Rules(town).PerLevel)));
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
        var m = Create(town, far, foe.Kind, "awakened", Rules(town).Stages.Overlord, foe.Leader != "" ? foe.Leader : null);
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

    /// <summary>The big bad stands a couple of levels over the town's champions, and never sinks below where it has grown to.</summary>
    private static int OverlordLevel(Town town, Menace m)
    {
        var f = town.Content.Saga.Finale;
        int crowned = Math.Clamp(Saga.ChampionLevel(town) + f.OverChampion, f.OverlordLevel[0], f.OverlordLevel[^1]);
        return Math.Min(Math.Max(m.Level, crowned), f.OverlordLevel[^1]);
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
        double g = r.Growth * (seat.Known != SiteKnowledge.Unheard ? 1 : r.UnknownGrowth) * (1 + m.Holdings.Count * r.HoldingGrowth) * Saga.Pace(town);
        if (m.FactionId != null && Factions.Find(town, m.FactionId) is FactionState f && f.Active) g += f.Members.Count * 0.1;
        // Every party that went for it and came back without finishing it makes it bolder.
        if (seat.Attempts > m.SeenAttempts)
        {
            g += (seat.Attempts - m.SeenAttempts) * r.FailedDelve;
            m.SeenAttempts = seat.Attempts;
        }
        m.Power += g;

        // A menace outgrows its act by a few levels at most; the big bad keeps the level it was crowned at.
        int level = Math.Min(Math.Min(r.MaxLevel, Saga.CurrentAct(town).Levels[^1] + 3), m.BaseLevel + (int)(m.Power / r.PerLevel));
        if (m.BigBad) level = m.Level;
        if (level > seat.Def.Tier)
        {
            m.Level = level;
            Empower(town, m, seat, level);
            if (m.Named) Spread(town, m, seat, 50);
        }
        m.Stage = StageFor(town, m);
        var before = m.Peak;
        if (m.Stage <= before) return;
        m.Peak = m.Stage;
        if (m.Stage >= MenaceStage.Power) Recruit(town, m, seat);
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
    private static void Recruit(Town town, Menace m, SiteState seat)
    {
        var lair = town.Sites
            .Where(s => Free(town, s))
            .OrderBy(s => s.RegionId == seat.RegionId ? 0 : 1)
            .ThenBy(s => Math.Abs(s.Def.Distance - seat.Def.Distance))
            .ThenBy(s => s.Def.Id, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lair == null) return;
        m.Holdings.Add(lair.Def.Id);
        lair.Strength = town.Content.Lairs.MaxStrength;
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
        m.NextStrikeTick = town.Tick + (int)Math.Round(rng.Range(r.StrikeEveryDays[0], r.StrikeEveryDays[1] + 1) * far) * SimTime.HoursPerDay;
        // Past its first rise it turns on the peoples of the wilds as often as on the town.
        var victim = m.Stage >= MenaceStage.Power ? Neighbour(town, seat) : null;
        if (victim != null && rng.Chance(0.5))
        {
            HitSettlement(town, m, victim);
            return;
        }
        HitTown(town, m);
    }

    private static Settlement? Neighbour(Town town, SiteState seat)
    {
        var region = World.RegionOf(town, seat);
        if (region == null) return null;
        return town.Settlements
            .Where(s => !s.Razed)
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
    }

    private static void HitTown(Town town, Menace m)
    {
        var r = Rules(town);
        var rng = town.Rng;
        var target = town.Buildings
            .Where(b => b.Complete && (b.Def.Zone == Zone.Farmland || b.Def.Id == "lumber_camp" || b.Def.Zone == Zone.Commercial))
            .OrderByDescending(b => Threat.Distance(town, b.LotId))
            .ThenBy(b => b.Id)
            .Take(4)
            .ToList();
        var where = target.Count > 0 ? target[rng.Range(0, target.Count)] : null;
        string place = where?.Name ?? "the outlying fields";
        double raiders = m.Power * r.StrikeShare * (0.7 + rng.Next() * 0.6);
        if (Threat.LocalDefense(town) >= raiders)
        {
            m.Power = Math.Max(0, m.Power - raiders * r.RepelLoss);
            var champion = town.Heroes.Where(a => !a.IsAway).OrderByDescending(x => x.Hero!.Level).ThenBy(x => x.Id).FirstOrDefault();
            string by = champion != null ? $"{champion.Name} and the watch" : "the watch";
            town.Log(LedgerCategory.War, Importance.Notable, Saga.Cap(Saga.Fill(town, r.Repelled, m).Replace("{where}", place).Replace("{by}", by)), champion != null ? [champion.Id] : []);
            town.Tension += 6;
            return;
        }
        bool dark = Saga.Grim(town) || rng.Chance(0.25);
        var victims = new List<int>();
        if (where != null)
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
        }
        var lines = victims.Count > 0 && m.Def.StrikeDark.Count > 0 ? m.Def.StrikeDark : m.Def.Strike;
        string text = lines.Count > 0 ? lines[rng.Range(0, lines.Count)] : "{name} strikes at {where}.";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, text, m).Replace("{where}", place)), victims);
        town.Tension += 15 + victims.Count * 10;
        town.Mandate = Math.Max(0, town.Mandate - 3);
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
        if (!due && !provoked && m.Power < Threat.Defense(town, allies: false) * r.AssaultConfidence)
        {
            m.NextAssaultTick = town.Tick + 10 * SimTime.HoursPerDay;
            return;
        }
        m.ArrivalTick = town.Tick + r.MarchDays * SimTime.HoursPerDay;
        // The big bad gathers a greater host for every march: the finale's doom clock.
        if (m.BigBad)
        {
            double d = Threat.Defense(town, allies: false), more = f.MarchGrowth * m.Marches;
            m.Power = Math.Clamp(m.Power, d * (f.MarchArmy[0] + more), d * (f.MarchArmy[^1] + more));
        }
        m.Marches++;
        string army = $"{Js.Str(Js.Round(m.Power / 10) * 10)} {m.Def.People}";
        town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, f.March, m).Replace("{army}", army).Replace("{days}", r.MarchDays.ToString())));
    }

    private static string Wall(int w) => w == 1 ? "gate" : w == 2 ? "east wall" : "river side";

    /// <summary>The menace's host at the walls: three waves, then its leader against the heroes at home, as at the Last Stand.</summary>
    private static void Assault(Town town, Menace m)
    {
        var r = Rules(town);
        var f = town.Content.Saga.Finale;
        var rng = town.Rng;
        m.ArrivalTick = null;
        m.NextAssaultTick = town.Tick + rng.Range(r.AssaultEveryDays[0], r.AssaultEveryDays[1] + 1) * SimTime.HoursPerDay;
        var lines = new List<string>();
        double def = Threat.Defense(town);
        double startDef = def;
        double army = m.Power;
        int breaches = 0;
        lines.Add($"{Saga.Cap(m.Name)}, some {Js.Str(Js.Round(army))} strong, comes against {town.Name}. The defenders muster {Js.Str(Js.Round(startDef))} strong.");
        for (int w = 1; w <= 3 && breaches < 2; w++)
        {
            double wave = (army / (3 - w + 1)) * (0.8 + rng.Next() * 0.4);
            double hold = (def / (3 - w + 1)) * (0.8 + rng.Next() * 0.4);
            if (hold >= wave)
            {
                def -= wave * 0.15;
                army -= wave;
                lines.Add($"Wave {w}: the {m.Def.People} of {m.Leader} hit the {Wall(w)} and break against it.");
            }
            else
            {
                def -= wave * 0.35;
                army -= wave * 0.5;
                breaches++;
                lines.Add($"Wave {w}: the {m.Def.People} of {m.Leader} breach the {Wall(w)}. Fighting spills into the streets.");
            }
        }
        m.Power = Math.Max(0, army);
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
        town.Watch = Math.Max(0, town.Watch - breaches * 2);
        town.Tension += 60 + dead.Count * 5;

        if (breaches >= 2)
        {
            if (Saga.Sack(town, m, lines))
            {
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
                c.Hero!.Renown += 10;
                c.Hero.Kills += result.Kills.Get(c.Id, 0);
            }
            slain = result.Outcome == ExpeditionOutcome.Cleared;
        }
        else lines.Add($"The walls hold, but no hero is in town to face {m.Leader}.");

        Chronicle(town, lines, slain ? ExpeditionOutcome.Cleared : ExpeditionOutcome.Retreated, champions);
        if (slain)
        {
            town.Log(LedgerCategory.War, Importance.Major, $"{m.Leader} falls before the walls of {town.Name}! (Chronicle {town.Chronicles.Count})", champions.Select(a => a.Id).ToList());
            End(town, m);
            return;
        }
        m.Power *= r.AssaultRetreat;
        town.Log(LedgerCategory.War, Importance.Major, $"{Saga.Cap(Saga.Fill(town, f.Repelled, m))} (Chronicle {town.Chronicles.Count})", champions.Select(a => a.Id).ToList());
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
            m.Holdings.Remove(id);
            m.Power = Math.Max(0, m.Power - r.HoldingCleared);
        }
    }

    /// <summary>Heroes cleared the seat: the big bad dies; with holdings left the menace is broken and moves; otherwise it ends.</summary>
    private static void SeatFalls(Town town, Menace m, SiteState seat)
    {
        var r = Rules(town);
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
            End(town, m);
            return;
        }
        string fled = seat.Def.Name;
        string old = m.Name;
        string oldLeader = m.Leader;
        bool escaped = town.Rng.Chance(0.5);
        if (!escaped) m.Leader = NewLeader(town, m.Def);
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
    }

    /// <summary>The menace is finished. Its holdings go back to being ordinary lairs.</summary>
    public static void End(Town town, Menace m)
    {
        if (!m.Alive) return;
        m.EndedDay = town.Day;
        m.Holdings.Clear();
        Saga.Relief(town);
        // The warlord's own death is told by Threat.
        if (m.Named && !m.BigBad && m.Origin != FromWarband && m.Def.Slain != "") town.Log(LedgerCategory.War, Importance.Major, Saga.Cap(Saga.Fill(town, m.Def.Slain, m)));
        Saga.Unyoke(town, m);
        if (m.BigBad) Saga.Win(town, m);
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
            .ThenByDescending(x => x.Menace.Stage)
            .ThenByDescending(x => x.Menace.Power)
            .ThenBy(x => x.Menace.Id)
            .FirstOrDefault();
        if (target.Seat == null) return;
        if (town.Bounties.Any(b => b.SiteId == target.Seat.Def.Id && b.Status is BountyStatus.Open or BountyStatus.Taken)) return;
        int level = Math.Max(1, target.Seat.Def.Tier);
        double reward = Math.Min(Quests.SuggestedReward(level) + r.BountyBonus, Math.Floor(town.Treasury * 0.6));
        if (reward < 40 * level) return;
        if (Quests.PostBounty(town, target.Seat.Def.Id, reward) != null) target.Menace.NextBountyDay = town.Day + r.BountyEveryDays;
    }
}
