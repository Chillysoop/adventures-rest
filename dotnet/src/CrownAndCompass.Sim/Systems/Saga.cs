namespace CrownAndCompass.Sim.Systems;

/// <summary>One act of the campaign, opened when the town's heroes reach its tier (or time runs out).</summary>
public sealed record ActDef
{
    public string Name { get; init; } = "";

    /// <summary>Party levels the act's threats are built for.</summary>
    public int[] Levels { get; init; } = [1, 4];

    /// <summary>Average level of the town's four best heroes the act waits for.</summary>
    public int OpensAtLevel { get; init; }

    /// <summary>The act never opens before this day, and always opens by LatestDay.</summary>
    public int EarliestDay { get; init; }
    public int LatestDay { get; init; }

    /// <summary>Levels added to every hero who arrives: the town's name carries farther each act.</summary>
    public int ArrivalBonus { get; init; }

    /// <summary>How fast menaces fester in this act.</summary>
    public double Pace { get; init; } = 1;

    /// <summary>Level bands of the menaces the act brings with it.</summary>
    public IReadOnlyList<int[]> Seeds { get; init; } = [];
    public bool Finale { get; init; }

    /// <summary>The Ledger's opening line, and its darker telling for a town already in trouble: {town}, {bigbad}.</summary>
    public string Intro { get; init; } = "";
    public string IntroDark { get; init; } = "";
}

public sealed record DreadRules
{
    /// <summary>Dread at which the town's mood turns uneasy, grim and dark.</summary>
    public int[] Tones { get; init; } = [25, 50, 75];

    /// <summary>Dread a known menace adds at each stage (lair to overlord).</summary>
    public double[] StageWeights { get; init; } = [0, 3, 9, 18, 28];
    public double Yoke { get; init; } = 20;
    public double Razed { get; init; } = 6;
    public double MaxRazed { get; init; } = 24;
    public double WarbandRaids { get; init; } = 6;
    public double WarbandMarching { get; init; } = 12;

    /// <summary>Share of the gap to its target that dread closes each day.</summary>
    public double Follow { get; init; } = 0.1;

    /// <summary>Scars are dread that outlasts its cause (a sack, a razed village); they fade this much a day.</summary>
    public double ScarDecay { get; init; } = 0.25;
    public double Sack { get; init; } = 35;
    public double MenaceDestroyed { get; init; } = 10;
    public double YokeBroken { get; init; } = 20;

    /// <summary>A menace frightens in proportion to its power against the town's defence, twice over, within these bounds.</summary>
    public double RelativeFloor { get; init; } = 0.25;

    /// <summary>The mood lifts only once dread is this far below a line, and changes at most once in this many days.</summary>
    public double ToneMargin { get; init; } = 10;
    public int ToneDays { get; init; } = 20;
    public double RelativeCap { get; init; } = 1.5;

    /// <summary>Favour evil gods draw each day from a grim town, per point of dread.</summary>
    public double EvilFavor { get; init; } = 0.03;
    /// <summary>Days between families giving up on the town: often when the mood is dark, now and then when grim.</summary>
    public int[] FleeEveryDays { get; init; } = [8, 14];
    public int[] GrimFleeEveryDays { get; init; } = [20, 30];
    public int[] Flee { get; init; } = [1, 3];
    public double DesertChance { get; init; } = 0.25;
    public int DesertRenown { get; init; } = 10;
    public int[] Refugees { get; init; } = [2, 4];
    public double AbandonAt { get; init; } = 95;
    public int AbandonDays { get; init; } = 30;
    public string Flight { get; init; } = "";
    public string Desert { get; init; } = "";
    public string RefugeesText { get; init; } = "";
    public string Abandoned { get; init; } = "";

    /// <summary>Ledger lines as the mood worsens into a tone (index), and as it lifts back to one.</summary>
    public IReadOnlyList<string> ToneUp { get; init; } = [];
    public IReadOnlyList<string> ToneDown { get; init; } = [];
}

public sealed record YokeRules
{
    public int EveryDays { get; init; } = 10;
    public double Share { get; init; } = 0.3;
    public double Minimum { get; init; } = 20;

    /// <summary>Tributes missed before the overlord stops waiting and marches.</summary>
    public int MissedToMarch { get; init; } = 2;

    /// <summary>Days after the sack while the town rebuilds: an empty treasury then is not defiance.</summary>
    public int GraceDays { get; init; } = 60;
    public string Sack { get; init; } = "";
    public string Tribute { get; init; } = "";
    public string Missed { get; init; } = "";
    public string Broken { get; init; } = "";
}

public sealed record FallbackFoe
{
    public string Kind { get; init; } = "beasts";
    public string Leader { get; init; } = "";
}

public sealed record FinaleRules
{
    /// <summary>The big bad's level when crowned: the level it grew to, within these bounds.</summary>
    public int[] OverlordLevel { get; init; } = [9, 13];

    /// <summary>
    /// The doom clock: the big bad's first host stands between these shares of the town's defence, however
    /// much or little it has grown, and each march after brings MarchGrowth more.
    /// </summary>
    public double[] MarchArmy { get; init; } = [0.7, 0.9];
    public double MarchGrowth { get; init; } = 0.15;

    /// <summary>Days after it is crowned before the big bad marches whether it is confident or not.</summary>
    public int FirstMarchDays { get; init; } = 60;
    public string Crown { get; init; } = "";
    public IReadOnlyList<FallbackFoe> Fallback { get; init; } = [];
    public string FallbackText { get; init; } = "";
    public string Victory { get; init; } = "";
    public string VictoryDark { get; init; } = "";
    public string Defeat { get; init; } = "";
    public string March { get; init; } = "";

    /// <summary>The Last Ride: the town's champions go for the big bad's seat themselves, this long after the crowning and then again after each try, once they are within RideUnder levels of it.</summary>
    public int RideAfterDays { get; init; } = 20;
    public int RideEveryDays { get; init; } = 30;
    public int RideUnder { get; init; } = 3;

    /// <summary>Levels under the yoke-holder's seat the champions may be and still ride to break the yoke.</summary>
    public int YokeRideUnder { get; init; }
    public string Ride { get; init; } = "";
    public string Repelled { get; init; } = "";

    /// <summary>
    /// The big bad does not die the first time its seat falls: it escapes this many times, and always while
    /// the Long Night is younger than MinDays, falling back to a holding or regrouping at its seat after
    /// RegroupDays. Strong heroes win the finale sooner, but never in a single blow.
    /// </summary>
    public int Escapes { get; init; } = 2;
    public int MinDays { get; init; } = 120;
    public int RegroupDays { get; init; } = 30;
    public double EscapeLoss { get; init; } = 0.8;
    public string Escape { get; init; } = "";
    public string Regroup { get; init; } = "";
}

/// <summary>The campaign arc (content/game/saga.json).</summary>
public sealed record SagaRules
{
    /// <summary>Day from which the Director drives the warband toward Act I's Last Stand.</summary>
    public int ClimaxDay { get; init; } = 120;
    public IReadOnlyList<ActDef> Acts { get; init; } = [];
    public MenaceRules Menaces { get; init; } = new();
    public DreadRules Dread { get; init; } = new();
    public YokeRules Yoke { get; init; } = new();
    public FinaleRules Finale { get; init; } = new();
}

/// <summary>How the town feels about its chances: the campaign's tone.</summary>
public enum Tone
{
    Hopeful,
    Uneasy,
    Grim,
    Dark,
}

public sealed class SagaState
{
    /// <summary>Off in sandboxes and some tests: the warband's Last Stand then ends the game as it used to.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Index of the current act.</summary>
    public int Act { get; set; }

    /// <summary>Day each act opened, in order.</summary>
    public List<int> ActDays { get; } = [1];

    /// <summary>Every menace there has been, in the order they arose; ended ones keep EndedDay.</summary>
    public List<Menace> Menaces { get; } = [];

    /// <summary>The town's fear, 0 to 100, and the part of it that outlasts its causes.</summary>
    public double Dread { get; set; }
    public double Scars { get; set; }
    public double PeakDread { get; set; }
    public Tone Tone { get; set; }
    public int ToneDay { get; set; }

    public int? BigBadId { get; set; }
    public int CrownDay { get; set; }

    /// <summary>Times the big bad has been driven from its seat and got away.</summary>
    public int BigBadEscapes { get; set; }

    /// <summary>The menace whose yoke the town is under after a sack, and whether it has stopped waiting for tribute.</summary>
    public int? YokeId { get; set; }
    public bool Defied { get; set; }
    public int NextTributeTick { get; set; }
    public int YokeTick { get; set; }
    public int MissedTribute { get; set; }
    public int Tributes { get; set; }
    public int Sacks { get; set; }

    /// <summary>Tick the town first heard of each uncleared site; a lair known and left alone long enough festers.</summary>
    public Dictionary<string, int> SeenSince { get; } = new();
    public int NextFesterDay { get; set; }
    public int NextFleeDay { get; set; }
    public int NextRideDay { get; set; }
    public int AbandonDays { get; set; }

    /// <summary>How the campaign ended, in words, for the end screen.</summary>
    public string EndText { get; set; } = "";
}

/// <summary>
/// The campaign arc, a ten-hour story told in four acts whose threats match the heroes' tiers: Points
/// of Light (the Bloodtusk Warband and the lairs around town), the Wider Wilds, Lords of the Land, and
/// the Long Night. An act opens when the town's best heroes reach its tier, and always by its latest
/// day. Lairs the town knows of and leaves alone fester into menaces (<see cref="Menaces"/>); the
/// strongest of them at the last act becomes the campaign's big bad, so each playthrough's villain
/// comes out of play. The tone is heroic, and turns dark as the town spirals: dread rises with every
/// menace in power, every sack and every village razed, until families flee, heroes desert, and evil
/// gods feed. A town can be sacked once and live under the yoke; the second time it falls.
/// </summary>
public static class Saga
{
    private static SagaRules Rules(Town town) => town.Content.Saga;

    public static ActDef CurrentAct(Town town) => Rules(town).Acts.Count == 0 ? new ActDef() : Rules(town).Acts[town.Saga.Act];

    public static bool Finale(Town town) => town.Saga.Enabled && CurrentAct(town).Finale;

    /// <summary>"Act II: The Wider Wilds".</summary>
    public static string ActTitle(Town town, int act) => $"Act {Roman(act + 1)}: {Rules(town).Acts[act].Name}";

    /// <summary>Average level of the town's four best living heroes, rounded down.</summary>
    public static int ChampionLevel(Town town)
    {
        var best = town.Heroes.Select(h => h.Hero!.Level).OrderByDescending(l => l).Take(4).ToList();
        return best.Count == 0 ? 0 : best.Sum() / best.Count;
    }

    public static int ArrivalBonus(Town town) => town.Saga.Enabled ? CurrentAct(town).ArrivalBonus : 0;

    public static double Pace(Town town) => CurrentAct(town).Pace;

    public static Menace? BigBad(Town town) => town.Saga.BigBadId is int id ? town.Saga.Menaces[id - 1] : null;

    public static Tone ToneOf(Town town) => town.Saga.Tone;

    public static bool Grim(Town town) => town.Saga.Tone >= Tone.Grim;

    /// <summary>
    /// The Director's push on the warband once Act I nears its climax. Past Act I the warband (if it
    /// lives) festers at a capped pace; once it is the big bad, the push starts over from its crowning.
    /// </summary>
    public static double WarbandPressure(Town town)
    {
        var s = town.Saga;
        int climax = s.Enabled ? Rules(town).ClimaxDay : 120;
        if (!s.Enabled) return town.Day > climax ? 1 + (town.Day - climax) / 30.0 : 1;
        if (BigBad(town)?.Origin == Menaces.FromWarband) return 1 + Math.Max(0, town.Day - s.CrownDay) / 30.0;
        if (town.Day <= climax) return 1;
        double p = 1 + (town.Day - climax) / 30.0;
        return s.Act == 0 ? p : Math.Min(2, p);
    }

    /// <summary>The warband holds back while the town pays its tribute.</summary>
    public static bool Restrained(Town town)
    {
        var s = town.Saga;
        return s.YokeId is int id && !s.Defied && s.Menaces[id - 1].Origin == Menaces.FromWarband;
    }

    public static void Daily(Town town, bool autoMayor)
    {
        var s = town.Saga;
        if (!s.Enabled || town.Outcome != null || Rules(town).Acts.Count == 0) return;
        Menaces.Daily(town, autoMayor);
        if (town.Outcome != null) return;
        AdvanceAct(town);
        Tribute(town);
        LastRide(town);
        Dread(town);
    }

    /// <summary>The town's four best heroes ride for the big bad's seat, or to break the yoke the town is under.</summary>
    private static void LastRide(Town town)
    {
        var s = town.Saga;
        var f = Rules(town).Finale;
        var b = BigBad(town) ?? (s.YokeId is int y ? s.Menaces[y - 1] : null);
        if (b == null || !b.Alive || town.Day < s.NextRideDay) return;
        var seat = Quests.Site(town, b.SeatId);
        if (seat == null || seat.Cleared || town.Parties.Any(p => !p.Returned && p.SiteId == seat.Def.Id)) return;
        var champions = town.Heroes.Where(Quests.Available).OrderByDescending(h => h.Hero!.Level).ThenBy(h => h.Id).Take(4).ToList();
        int under = b.BigBad ? f.RideUnder : f.YokeRideUnder;
        if (champions.Count < 3 || champions.Average(h => h.Hero!.Level) < Math.Max(1, seat.Def.Tier) - under) return;
        s.NextRideDay = town.Day + f.RideEveryDays;
        var bounty = town.Bounties.FirstOrDefault(x => x.SiteId == seat.Def.Id && x.Status == BountyStatus.Open);
        Quests.Launch(town, champions, seat, bounty);
        string names = Expedition.ListNames(champions.Select(h => $"{h.GivenName} the {h.Hero!.Cls.Name} {h.Hero.Level}").ToList());
        town.Log(LedgerCategory.Quest, Importance.Major, Fill(town, f.Ride, b).Replace("{heroes}", names), champions.Select(h => h.Id).ToList());
    }

    // Acts ---------------------------------------------------------------------------------------

    private static void AdvanceAct(Town town)
    {
        var s = town.Saga;
        var acts = Rules(town).Acts;
        if (s.Act + 1 >= acts.Count) return;
        var next = acts[s.Act + 1];
        // Act I is the warband's: it ends with the Last Stand (won, or the town sacked), or when time runs out.
        bool spineDone = s.Act > 0 || !Threat.IsActive(town) || s.Sacks > 0;
        bool ready = spineDone && town.Day >= next.EarliestDay && ChampionLevel(town) >= next.OpensAtLevel;
        if (ready || town.Day >= next.LatestDay) OpenAct(town, s.Act + 1);
    }

    private static void OpenAct(Town town, int act)
    {
        var s = town.Saga;
        var def = Rules(town).Acts[act];
        // A warband that outlived its act goes on festering with the rest.
        if (s.Act == 0 && Threat.IsActive(town)) Menaces.Warband(town);
        s.Act = act;
        s.ActDays.Add(town.Day);
        if (def.Finale) Crown(town);
        string intro = (Grim(town) && def.IntroDark != "" ? def.IntroDark : def.Intro)
            .Replace("{town}", town.Name)
            .Replace("{bigbad}", BigBad(town) is Menace b ? b.Name : "");
        town.Log(LedgerCategory.Town, Importance.Major, Sentences($"{ActTitle(town, act)}. {intro}"));
        foreach (var band in def.Seeds) Menaces.Seed(town, band);
    }

    /// <summary>The finale: the strongest menace in the land becomes the big bad, or something old wakes if the town has beaten them all.</summary>
    private static void Crown(Town town)
    {
        var s = town.Saga;
        var f = Rules(town).Finale;
        // Only a mind that can rule is crowned: a beast or a kobold band may be the worst thing abroad, but it is no big bad.
        var top = s.Menaces.Where(m => m.Alive && Menaces.CanCrown(town, m)).OrderByDescending(m => m.Power * Menaces.CunningOf(town, m).CrownWeight).ThenBy(m => m.Id).FirstOrDefault();
        string text;
        if (top == null)
        {
            top = Menaces.Awaken(town, f.Fallback.Count > 0 ? town.Rng.Pick(f.Fallback) : new FallbackFoe { Leader = "the Old Wyrm" });
            if (top == null) return;
            text = Fill(town, f.FallbackText, top);
        }
        else text = Fill(town, f.Crown, top);
        s.BigBadId = top.Id;
        s.CrownDay = town.Day;
        s.NextRideDay = town.Day + f.RideAfterDays;
        top.BigBad = true;
        Menaces.Ascend(town, top);
        town.Log(LedgerCategory.War, Importance.Major, text);
    }

    // The yoke -----------------------------------------------------------------------------------

    /// <summary>
    /// The walls are broken. Before the finale, and the first time, the town is sacked and lives under
    /// the victor's yoke; returns false if it falls for good instead.
    /// </summary>
    public static bool Sack(Town town, Menace by, List<string> lines)
    {
        var s = town.Saga;
        var y = Rules(town).Yoke;
        if (!s.Enabled || Finale(town) || s.YokeId != null) return false;
        s.Sacks++;
        s.Scars += Rules(town).Dread.Sack;
        s.YokeId = by.Id;
        s.Defied = false;
        s.MissedTribute = 0;
        s.Tributes = 0;
        s.NextRideDay = town.Day + Rules(town).Finale.RideAfterDays;
        s.NextTributeTick = town.Tick + y.EveryDays * SimTime.HoursPerDay;
        s.YokeTick = town.Tick;
        town.Book(TreasuryAccount.Tribute, -Math.Max(0, town.Treasury));
        town.Mandate = 0;
        lines.Add(Fill(town, y.Sack, by));
        return true;
    }

    /// <summary>The town bends the knee to a would-be overlord without a fight: the yoke, with no sack.</summary>
    public static void Swear(Town town, Menace to)
    {
        var s = town.Saga;
        var y = Rules(town).Yoke;
        s.YokeId = to.Id;
        s.Defied = false;
        s.MissedTribute = 0;
        s.Tributes = 0;
        s.NextRideDay = town.Day + Rules(town).Finale.RideAfterDays;
        s.NextTributeTick = town.Tick + y.EveryDays * SimTime.HoursPerDay;
        s.YokeTick = town.Tick;
        s.Scars += Rules(town).Dread.Sack * 0.5;
        town.Mandate = Math.Max(0, town.Mandate - 15);
    }

    /// <summary>The town falls for good.</summary>
    public static void Fall(Town town, string text)
    {
        town.Outcome = Outcome.Defeat;
        town.Saga.EndText = text;
    }

    private static void Tribute(Town town)
    {
        var s = town.Saga;
        var y = Rules(town).Yoke;
        if (s.YokeId is not int id || town.Tick < s.NextTributeTick) return;
        var m = s.Menaces[id - 1];
        s.NextTributeTick = town.Tick + y.EveryDays * SimTime.HoursPerDay;
        double take = Math.Floor(Math.Max(0, town.Treasury) * y.Share);
        if (take >= y.Minimum)
        {
            town.Book(TreasuryAccount.Tribute, -take);
            Menaces.Feed(town, m, take * 0.1);
            s.MissedTribute = 0;
            s.Tributes++;
            town.Log(LedgerCategory.War, s.Tributes == 1 ? Importance.Notable : Importance.Routine, Fill(town, y.Tribute, m).Replace("{gold}", Js.Str(take)));
            return;
        }
        town.Log(LedgerCategory.War, Importance.Notable, Fill(town, y.Missed, m));
        if (town.Tick - s.YokeTick < y.GraceDays * SimTime.HoursPerDay) return;
        s.MissedTribute++;
        if (s.MissedTribute >= y.MissedToMarch && !s.Defied)
        {
            s.Defied = true;
            Menaces.Provoke(town, m);
        }
    }

    /// <summary>The menace whose yoke the town wore is gone.</summary>
    internal static void Unyoke(Town town, Menace m)
    {
        var s = town.Saga;
        if (s.YokeId != m.Id) return;
        s.YokeId = null;
        s.Defied = false;
        s.Scars = Math.Max(0, s.Scars - Rules(town).Dread.YokeBroken);
        town.Log(LedgerCategory.War, Importance.Major, Fill(town, Rules(town).Yoke.Broken, m));
    }

    /// <summary>The big bad is dead: the campaign is won.</summary>
    internal static void Win(Town town, Menace m)
    {
        var f = Rules(town).Finale;
        town.Outcome = Outcome.Victory;
        town.Mandate = Math.Min(100, town.Mandate + 25);
        town.Saga.EndText = Fill(town, Grim(town) ? f.VictoryDark : f.Victory, m);
        town.Log(LedgerCategory.War, Importance.Major, town.Saga.EndText);
    }

    /// <summary>The warlord died at the walls (or at his camp): Act I's spine is done, or the big bad is dead.</summary>
    public static void WarlordSlain(Town town)
    {
        var s = town.Saga;
        if (!s.Enabled)
        {
            town.Outcome = Outcome.Victory;
            return;
        }
        var band = s.Menaces.FirstOrDefault(m => m.Alive && m.Origin == Menaces.FromWarband);
        if (band != null) Menaces.End(town, band);
        // The warband scatters; one of its captains keeps a band together in the hills.
        if (band?.BigBad != true) Menaces.Remnant(town);
    }

    // Dread --------------------------------------------------------------------------------------

    private static void Dread(Town town)
    {
        var s = town.Saga;
        var d = Rules(town).Dread;
        var rng = town.Rng;
        double target = s.Scars;
        double defense = Math.Max(50, Threat.Defense(town));
        foreach (var m in s.Menaces)
        {
            if (!m.Alive || (!m.Named && m.Stage < MenaceStage.Dominion)) continue;
            double scale = Math.Clamp(2 * m.Power / defense, d.RelativeFloor, d.RelativeCap);
            target += d.StageWeights[Math.Min(d.StageWeights.Length - 1, (int)m.Stage)] * scale;
        }
        if (s.YokeId != null) target += d.Yoke;
        target += Math.Min(d.MaxRazed, town.Settlements.Count(x => x.Razed) * d.Razed);
        if (Threat.IsActive(town) && !Restrained(town))
            target += town.Enemy.Stage == ThreatStage.Marching ? d.WarbandMarching : town.Enemy.Stage == ThreatStage.Raids ? d.WarbandRaids : 0;
        target = Math.Clamp(target, 0, 100);
        s.Dread += (target - s.Dread) * d.Follow;
        s.Scars = Math.Max(0, s.Scars - d.ScarDecay);
        s.PeakDread = Math.Max(s.PeakDread, s.Dread);
        Retone(town);

        if (s.Tone >= Tone.Grim)
        {
            // Fear is food for the dark gods, through the cults that sing to them.
            foreach (var god in Faith.Gods(town))
                if (god.Evil && town.Factions.Any(f => f.Active && f.Lore.Patron == god.Id))
                    town.Faiths.Favor[god.Id] = town.Faiths.Favor.Get(god.Id, 0) + s.Dread * d.EvilFavor;
            var every = s.Tone == Tone.Dark ? d.FleeEveryDays : d.GrimFleeEveryDays;
            if (s.NextFleeDay == 0) s.NextFleeDay = town.Day + rng.Range(every[0], every[1] + 1);
            if (town.Day >= s.NextFleeDay)
            {
                s.NextFleeDay = town.Day + rng.Range(every[0], every[1] + 1);
                Flight(town, s.Tone == Tone.Dark ? rng.Range(d.Flee[0], d.Flee[1] + 1) : 1);
                if (s.Tone == Tone.Dark && rng.Chance(d.DesertChance)) Desertion(town);
            }
        }
        else s.NextFleeDay = 0;

        s.AbandonDays = s.Dread >= d.AbandonAt ? s.AbandonDays + 1 : 0;
        if (s.AbandonDays >= d.AbandonDays)
        {
            string text = d.Abandoned.Replace("{town}", town.Name);
            Fall(town, text);
            town.Log(LedgerCategory.Town, Importance.Major, text);
        }
    }

    /// <summary>The mood shifts a tone at a time, and only lifts once dread is clearly below the line.</summary>
    private static void Retone(Town town)
    {
        var s = town.Saga;
        var d = Rules(town).Dread;
        int raw = d.Tones.Count(t => s.Dread >= t);
        int cur = (int)s.Tone;
        if (raw == cur || town.Day < s.ToneDay + d.ToneDays) return;
        if (raw > cur)
        {
            s.ToneDay = town.Day;
            s.Tone = (Tone)(cur + 1);
            if (cur + 1 < d.ToneUp.Count) town.Log(LedgerCategory.Town, Importance.Major, d.ToneUp[cur + 1].Replace("{town}", town.Name));
        }
        else if (s.Dread < d.Tones[cur - 1] - d.ToneMargin)
        {
            s.ToneDay = town.Day;
            s.Tone = (Tone)(cur - 1);
            if (cur - 1 < d.ToneDown.Count) town.Log(LedgerCategory.Town, Importance.Major, d.ToneDown[cur - 1].Replace("{town}", town.Name));
        }
    }

    /// <summary>A family gives up on the town.</summary>
    private static void Flight(Town town, int n)
    {
        var pool = town.Workers.Where(a => town.Seats.All(x => x.HolderId != a.Id)).OrderBy(a => a.Id).ToList();
        if (pool.Count == 0) return;
        var first = pool[town.Rng.Range(0, pool.Count)];
        string family = first.Name.Split(' ')[^1];
        var leaving = pool.Where(a => a.Name.EndsWith(" " + family, StringComparison.Ordinal) && a.Id != first.Id).Take(n - 1).Prepend(first).ToList();
        foreach (var a in leaving) Population.Depart(town, a);
        town.Log(LedgerCategory.Departure, Importance.Notable, Rules(town).Dread.Flight.Replace("{family}", family), leaving.Select(a => a.Id).ToList());
    }

    private static void Desertion(Town town)
    {
        var d = Rules(town).Dread;
        var h = town.Heroes.Where(a => !a.IsAway && a.Hero!.Renown < d.DesertRenown).OrderBy(a => a.Hero!.Renown).ThenBy(a => a.Id).FirstOrDefault();
        if (h == null) return;
        Population.Depart(town, h);
        town.Log(LedgerCategory.Departure, Importance.Notable, d.Desert.Replace("{hero}", h.Name), [h.Id]);
    }

    /// <summary>Survivors of a razed settlement come to town with nothing.</summary>
    internal static void Refugees(Town town, Settlement from)
    {
        var d = Rules(town).Dread;
        int n = town.Rng.Range(d.Refugees[0], d.Refugees[1] + 1);
        var come = new List<int>();
        for (int i = 0; i < n; i++) come.Add(Population.CreateAgent(town, new NewAgentOptions { Gold = town.Rng.Range(0, 5) }).Id);
        town.Log(LedgerCategory.Arrival, Importance.Notable, d.RefugeesText.Replace("{n}", n.ToString()).Replace("{settlement}", from.Name).Replace("{town}", town.Name), come);
    }

    /// <summary>A menace is gone: hope returns a little.</summary>
    internal static void Relief(Town town) => town.Saga.Scars = Math.Max(0, town.Saga.Scars - Rules(town).Dread.MenaceDestroyed);

    internal static void Scar(Town town, double amount) => town.Saga.Scars += amount;

    // Words --------------------------------------------------------------------------------------

    internal static string Fill(Town town, string text, Menace m) => Sentences(text
        .Replace("{name}", m.Name)
        .Replace("{Name}", Cap(m.Name))
        .Replace("{leader}", m.Leader)
        .Replace("{region}", m.Region)
        .Replace("{seat}", Quests.Site(town, m.SeatId)?.Def.Name ?? m.Region)
        .Replace("{town}", town.Name));

    /// <summary>The end screen's title.</summary>
    public static string EndTitle(Town town) =>
        town.Outcome == Outcome.Victory ? (town.Saga.Enabled ? "The Long Night is over" : "The Last Stand is won")
        : town.Outcome == Outcome.Defeat ? $"{town.Name} has fallen"
        : "";

    /// <summary>One line on where the campaign stands, for the CLI and the client.</summary>
    public static string Summary(Town town)
    {
        var s = town.Saga;
        if (!s.Enabled || Rules(town).Acts.Count == 0) return "";
        var alive = s.Menaces.Where(m => m.Alive).ToList();
        string foes = alive.Count == 0 ? "no menaces abroad" : string.Join("; ", alive.Select(m => $"{m.Name} (level {m.Level}, {m.Stage.ToString().ToLowerInvariant()}{(m.BigBad ? ", the big bad" : "")})"));
        return $"{ActTitle(town, s.Act)} (from day {s.ActDays[^1]}). Dread {Js.Str(Js.Round(s.Dread))} ({s.Tone.ToString().ToLowerInvariant()}, peak {Js.Str(Js.Round(s.PeakDread))}), sacked {s.Sacks}x, champions level {ChampionLevel(town)}. {Cap(foes)}.";
    }

    private static string Roman(int n) => n switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        4 => "IV",
        5 => "V",
        _ => n.ToString(),
    };

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>Capitalize every sentence: names like "the Grey Widow" can open one.</summary>
    internal static string Sentences(string s)
    {
        var chars = Cap(s).ToCharArray();
        for (int i = 2; i < chars.Length; i++)
            if (chars[i - 1] == ' ' && chars[i - 2] is '.' or '!' or '?') chars[i] = char.ToUpperInvariant(chars[i]);
        return new string(chars);
    }
}
