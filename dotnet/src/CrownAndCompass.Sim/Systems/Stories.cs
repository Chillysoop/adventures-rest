namespace CrownAndCompass.Sim.Systems;

/// <summary>Story events and the mayor's character sheet (content/game/stories.json).</summary>
public sealed record StoryRules
{
    /// <summary>No story opens before this day, nor sooner than EveryDays after the last one.</summary>
    public int FirstDay { get; init; } = 6;
    public int EveryDays { get; init; } = 5;

    /// <summary>Chance a day, once the gap has passed, that a new story opens.</summary>
    public double Chance { get; init; } = 0.3;

    /// <summary>Stories waiting on the mayor at once, at most.</summary>
    public int MaxOpen { get; init; } = 2;

    /// <summary>A failed check this close to the DC is a success at a cost, where the option has one.</summary>
    public int PartialMargin { get; init; } = 3;

    /// <summary>Low, medium and high stakes: days to answer and the mayor's XP for seeing one through.</summary>
    public List<StakesDef> Stakes { get; init; } = [];

    /// <summary>XP to reach each level, from level 1.</summary>
    public List<int> Levels { get; init; } = [];

    /// <summary>What the mayor is called at each level.</summary>
    public List<string> Titles { get; init; } = [];

    /// <summary>Levels that bring an ability score improvement (+2 to the background's first ability below 20).</summary>
    public List<int> Improvements { get; init; } = [];

    /// <summary>The standard array, dealt out in the background's ability order.</summary>
    public List<int> Array { get; init; } = [];
    public List<SkillDef> Skills { get; init; } = [];
    public List<MayorBackground> Backgrounds { get; init; } = [];
    public List<StoryDef> Stories { get; init; } = [];

    public StakesDef StakesOf(string id) => Stakes.FirstOrDefault(s => s.Id == id) ?? Stakes[0];
    public SkillDef? Skill(string id) => Skills.FirstOrDefault(s => s.Id == id);
    public MayorBackground Background(string id) => Backgrounds.FirstOrDefault(b => b.Ref == id) ?? Backgrounds[0];
    public StoryDef? Story(string id) => Stories.FirstOrDefault(s => s.Id == id);
}

public sealed record StakesDef
{
    public string Id { get; init; } = "low";
    public string Name { get; init; } = "";
    public int DueDays { get; init; } = 3;
    public int Xp { get; init; } = 20;
}

/// <summary>One of the eighteen skills, and the ability it rolls with.</summary>
public sealed record SkillDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Ability { get; init; } = "";
}

/// <summary>Who the mayor was before the charter: a library background, its skills, its abilities in order of strength, and a story source it gives advantage in.</summary>
public sealed record MayorBackground
{
    public string Ref { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> Abilities { get; init; } = [];
    public List<string> Skills { get; init; } = [];
    public string Favoured { get; init; } = "";
    public string Feature { get; init; } = "";
    public string Text { get; init; } = "";
}

/// <summary>
/// A story event: something going on in or around the town that the mayor can step into. It opens when
/// its needs are met, binding the hero, menace, faction, seat, building, site or party it is about,
/// and runs through stages of choices until an outcome ends it. Petitions and other systems add their
/// own stories here with a <see cref="Source"/> of their own.
/// </summary>
public sealed record StoryDef
{
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Stakes { get; init; } = "low";

    /// <summary>Where it comes from (tavern, heroes, council, faction, war, road, faith, history, town): backgrounds favour some.</summary>
    public string Source { get; init; } = "town";

    /// <summary>What must be true for it to open, and what it binds (<see cref="Stories.Needs"/>).</summary>
    public List<string> Needs { get; init; } = [];
    public int MinDay { get; init; }
    public int MinPopulation { get; init; }
    public double Weight { get; init; } = 1;

    /// <summary>Days before the same story can open again.</summary>
    public int RepeatDays { get; init; } = 90;
    public List<StoryStage> Stages { get; init; } = [];

    /// <summary>What happens if the mayor lets it run its course, unless the stage says otherwise.</summary>
    public StoryOutcome? Ignored { get; init; }
}

public sealed record StoryStage
{
    public string Id { get; init; } = "";
    public string Text { get; init; } = "";
    public List<StoryOption> Options { get; init; } = [];
    public StoryOutcome? Ignored { get; init; }
}

/// <summary>A choice: a skill (or bare ability) check against a DC, or, with neither, a sure thing.</summary>
public sealed record StoryOption
{
    public string Label { get; init; } = "";
    public string Skill { get; init; } = "";
    public string Ability { get; init; } = "";
    public int Dc { get; init; }

    /// <summary>Gold from the treasury to try it at all.</summary>
    public double Cost { get; init; }

    /// <summary>Needs for the option itself (a temple, the watch...).</summary>
    public List<string> Needs { get; init; } = [];

    /// <summary>"hero": the bound hero lends a hand, which is advantage.</summary>
    public string Aid { get; init; } = "";
    public StoryOutcome Success { get; init; } = new();
    public StoryOutcome Failure { get; init; } = new();

    /// <summary>A natural 20, a natural 1, and a near miss; each falls back to success or failure.</summary>
    public StoryOutcome? Critical { get; init; }
    public StoryOutcome? Fumble { get; init; }
    public StoryOutcome? Partial { get; init; }

    public bool Rolls => Skill != "" || Ability != "";
}

/// <summary>
/// What a choice leads to: its text, its effects on the town (<see cref="Stories.EffectKeys"/>), and the
/// next stage, if the story goes on.
/// </summary>
public sealed record StoryOutcome
{
    public string Text { get; init; } = "";
    public Dictionary<string, double> Effects { get; init; } = [];
    public string Next { get; init; } = "";
}

/// <summary>The mayor, as a D&amp;D character: background, level and XP, inspiration. Abilities and skills follow from them.</summary>
public sealed class MayorSheet
{
    public string Name { get; set; } = "";
    public string Background { get; set; } = "";

    /// <summary>Whether the player has picked the background (it can be changed until then).</summary>
    public bool Chosen { get; set; }
    public int Level { get; set; } = 1;
    public int Xp { get; set; }

    /// <summary>D&amp;D inspiration: spend it for advantage on a check. A natural 20 earns it back.</summary>
    public int Inspiration { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }
}

public enum StoryResult
{
    Certain,
    Critical,
    Success,
    Partial,
    Failure,
    Fumble,
    Ignored,
}

/// <summary>One choice made in a story, the dice it took and what came of it.</summary>
public sealed class StoryBeat
{
    public int Tick { get; set; }
    public string Stage { get; set; } = "";
    public string Option { get; set; } = "";
    public string Skill { get; set; } = "";
    public int Die { get; set; }

    /// <summary>The other d20 when the check had advantage.</summary>
    public int? Other { get; set; }
    public int Modifier { get; set; }
    public int Proficiency { get; set; }
    public int Total { get; set; }
    public int Dc { get; set; }
    public StoryResult Result { get; set; }
    public string Text { get; set; } = "";

    /// <summary>Why it had advantage, if it did.</summary>
    public string Advantage { get; set; } = "";
}

public sealed class Story
{
    public int Id { get; set; }
    public string Def { get; set; } = "";
    public string Stage { get; set; } = "";
    public int OpenedTick { get; set; }
    public int DueTick { get; set; }
    public bool Done { get; set; }
    public int? HeroId { get; set; }
    public int? MenaceId { get; set; }
    public bool Warband { get; set; }
    public string? FactionId { get; set; }
    public string? SeatId { get; set; }
    public int? BuildingId { get; set; }
    public string? SiteId { get; set; }
    public int? PartyId { get; set; }
    public string? Power { get; set; }
    public List<StoryBeat> Beats { get; } = [];
}

public sealed class StoryState
{
    public MayorSheet Mayor { get; set; } = new();
    public List<Story> All { get; } = [];
    public int NextDay { get; set; }

    /// <summary>The day each story last opened, so it does not repeat too soon.</summary>
    public OrderedMap<string, int> LastDay { get; } = new();

    public IEnumerable<Story> Open => All.Where(s => !s.Done);
}

/// <summary>
/// Story events, in the manner of CK and Paradox events with a d20 under them. Things going on in and
/// around the town (a brawl, a restless hero, a menace's envoy, a host on the march) open as stories the
/// mayor can step into. Each choice is a check of the mayor's own abilities and skills against a DC,
/// rolled on the town's rng; its outcome moves the town (gold, Mandate, dread, a hero's loyalty, a
/// faction's standing, a foe's strength...). A story left alone runs its course when it falls due.
/// The stand-in mayor answers them headless. New sources (petitions, seasons) add stories to the
/// content and, where they need to, a need below.
/// </summary>
public static class Stories
{
    private static StoryRules Rules(Town town) => town.Content.Stories;

    /// <summary>Every effect an outcome can have, in the order they apply.</summary>
    public static readonly string[] EffectKeys =
        ["gold", "mandate", "dread", "tension", "food", "timber", "loyalty", "wound", "favour", "standing", "seat", "foePower", "delay", "festival", "reveal", "inspiration"];

    /// <summary>Every need a story or option can name.</summary>
    public static readonly string[] Needs =
        ["hero", "restless", "party", "menace", "march", "warband", "faction", "seat", "tavern", "temple", "site", "unheard", "power", "hungry", "homeless", "rich", "watch", "poor"];

    // ---------- The mayor ----------

    /// <summary>A mayor for a new town: named after the seed, with the background it points to, until the player picks one.</summary>
    public static MayorSheet NewMayor(Town town)
    {
        var r = Rules(town);
        var names = town.Content.Races.First(x => x.Id == "human");
        int seed = Math.Abs(town.Seed);
        return new MayorSheet
        {
            Name = $"{names.Given[seed * 7 % names.Given.Count]} {names.Family[seed * 13 % names.Family.Count]}",
            Background = r.Backgrounds.Count > 0 ? r.Backgrounds[seed % r.Backgrounds.Count].Ref : "",
        };
    }

    /// <summary>The mayor's ability scores: the standard array in the background's order, +2 and +1 from it, then each improvement reached.</summary>
    public static Abilities Scores(Town town)
    {
        var r = Rules(town);
        var m = town.Stories.Mayor;
        var bg = r.Background(m.Background);
        var a = new Abilities();
        for (int i = 0; i < bg.Abilities.Count && i < r.Array.Count; i++) a[bg.Abilities[i]] = r.Array[i];
        if (bg.Abilities.Count > 1)
        {
            a[bg.Abilities[0]] += 2;
            a[bg.Abilities[1]] += 1;
        }
        foreach (int level in r.Improvements.Where(l => l <= m.Level))
        {
            int points = 2;
            foreach (var ab in bg.Abilities)
            {
                while (points > 0 && a[ab] < 20)
                {
                    a[ab]++;
                    points--;
                }
            }
        }
        return a;
    }

    public static int ProficiencyBonus(int level) => 2 + (level - 1) / 4;

    public static bool Proficient(Town town, string skill) => Rules(town).Background(town.Stories.Mayor.Background).Skills.Contains(skill);

    /// <summary>The mayor's bonus on a check: the ability's modifier, plus proficiency in the skill.</summary>
    public static (int Modifier, int Proficiency, string Ability) Bonus(Town town, string skill, string ability = "")
    {
        var sk = skill == "" ? null : Rules(town).Skill(skill);
        string ab = sk?.Ability ?? ability;
        if (ab == "") return (0, 0, "");
        int mod = Rng.Modifier(Scores(town)[ab]);
        int prof = sk != null && Proficient(town, skill) ? ProficiencyBonus(town.Stories.Mayor.Level) : 0;
        return (mod, prof, ab);
    }

    public static string Title(Town town)
    {
        var titles = Rules(town).Titles;
        return titles.Count == 0 ? "Mayor" : titles[Math.Clamp(town.Stories.Mayor.Level - 1, 0, titles.Count - 1)];
    }

    /// <summary>XP for the next level, or null at the top.</summary>
    public static int? NextLevelXp(Town town)
    {
        var levels = Rules(town).Levels;
        int lv = town.Stories.Mayor.Level;
        return lv < levels.Count ? levels[lv] : null;
    }

    /// <summary>The background can be chosen until the mayor first steps into a story.</summary>
    public static bool CanChooseBackground(Town town) => town.Stories.All.All(s => s.Beats.All(b => b.Result == StoryResult.Ignored));

    /// <summary>The player picks who they were before the charter, until they first step into a story.</summary>
    public static bool ChooseBackground(Town town, string id)
    {
        var r = Rules(town);
        var m = town.Stories.Mayor;
        if (!CanChooseBackground(town) || r.Backgrounds.All(b => b.Ref != id)) return false;
        m.Background = id;
        m.Chosen = true;
        var bg = r.Background(id);
        town.Log(LedgerCategory.Story, Importance.Routine, $"Before the charter, Mayor {m.Name} was {Article(bg.Name)}.");
        return true;
    }

    private static string Article(string s) => (s.Length > 0 && "aeiou".Contains(char.ToLowerInvariant(s[0])) ? "an " : "a ") + s.ToLowerInvariant();

    private static void GainXp(Town town, int xp)
    {
        var m = town.Stories.Mayor;
        m.Xp += xp;
        var levels = Rules(town).Levels;
        while (m.Level < levels.Count && m.Xp >= levels[m.Level])
        {
            m.Level++;
            bool improves = Rules(town).Improvements.Contains(m.Level);
            town.Log(LedgerCategory.Story, Importance.Notable,
                $"Mayor {m.Name} is now a level {m.Level} {Title(town).ToLowerInvariant()}.{(ProficiencyBonus(m.Level) > ProficiencyBonus(m.Level - 1) ? $" Their proficiency bonus rises to +{ProficiencyBonus(m.Level)}." : "")}{(improves ? " Their best abilities improve." : "")}");
        }
    }

    // ---------- Stories ----------

    public static Story? Find(Town town, int id) => town.Stories.All.FirstOrDefault(s => s.Id == id);

    public static StoryDef DefOf(Town town, Story s) => Rules(town).Story(s.Def) ?? throw new KeyNotFoundException($"Unknown story {s.Def}");

    public static StoryStage StageOf(Town town, Story s)
    {
        var d = DefOf(town, s);
        return d.Stages.FirstOrDefault(x => x.Id == s.Stage) ?? d.Stages[0];
    }

    public static StakesDef StakesOf(Town town, Story s) => Rules(town).StakesOf(DefOf(town, s).Stakes);

    /// <summary>Open stories, oldest first.</summary>
    public static List<Story> Pending(Town town) => town.Stories.Open.OrderBy(s => s.Id).ToList();

    public static void Daily(Town town, bool autoMayor)
    {
        var r = Rules(town);
        var st = town.Stories;
        foreach (var s in Pending(town))
            if (town.Tick >= s.DueTick) Lapse(town, s);
        if (autoMayor)
            foreach (var s in Pending(town)) StandIn(town, s);
        if (r.Stories.Count == 0 || town.Day < Math.Max(r.FirstDay, st.NextDay) || st.Open.Count() >= r.MaxOpen || town.Outcome != null) return;
        if (!town.Rng.Chance(r.Chance)) return;
        var open = st.Open.Select(s => s.Def).ToHashSet();
        var fits = new List<(StoryDef Def, Story Bound)>();
        foreach (var d in r.Stories)
        {
            if (open.Contains(d.Id) || town.Day < d.MinDay || town.Population < d.MinPopulation) continue;
            if (st.LastDay.Get(d.Id, -9999) + d.RepeatDays > town.Day) continue;
            if (Bind(town, d) is { } bound) fits.Add((d, bound));
        }
        if (fits.Count == 0) return;
        double total = Js.Sum(fits, f => f.Def.Weight);
        double roll = town.Rng.Next() * total;
        var pick = fits[^1];
        foreach (var f in fits)
        {
            roll -= f.Def.Weight;
            if (roll < 0)
            {
                pick = f;
                break;
            }
        }
        Begin(town, pick.Def, pick.Bound);
    }

    /// <summary>Open a story now, binding what it is about; null when its needs are not met. Tests and scripted shots call it directly.</summary>
    public static Story? Open(Town town, string defId)
    {
        var d = Rules(town).Story(defId);
        if (d == null || Bind(town, d) is not { } bound) return null;
        return Begin(town, d, bound);
    }

    private static Story Begin(Town town, StoryDef d, Story s)
    {
        var st = town.Stories;
        var stakes = Rules(town).StakesOf(d.Stakes);
        s.Id = st.All.Count + 1;
        s.Def = d.Id;
        s.Stage = d.Stages[0].Id;
        s.OpenedTick = town.Tick;
        s.DueTick = town.Tick + stakes.DueDays * SimTime.HoursPerDay;
        st.All.Add(s);
        st.LastDay[d.Id] = town.Day;
        st.NextDay = town.Day + Rules(town).EveryDays;
        town.Log(LedgerCategory.Story, d.Stakes == "high" ? Importance.Major : Importance.Notable, $"{Fill(town, s, d.Title)}. {Fill(town, s, d.Stages[0].Text)}", Agents(s));
        return s;
    }

    private static List<int> Agents(Story s) => s.HeroId is int h ? [h] : [];

    /// <summary>Bind what a story is about from the town as it stands, or null if one of its needs is unmet.</summary>
    private static Story? Bind(Town town, StoryDef d)
    {
        var s = new Story();
        foreach (var need in d.Needs)
            if (!Meet(town, need, s, bind: true)) return null;
        // Every stage must leave the mayor at least one option.
        return s;
    }

    /// <summary>Whether a need holds; with bind, the first time a kind of thing is needed it is picked (by rng, in id order).</summary>
    private static bool Meet(Town town, string need, Story s, bool bind)
    {
        T? Pick<T>(List<T> items) where T : class => items.Count == 0 ? null : bind ? items[town.Rng.Range(0, items.Count)] : items[0];
        switch (need)
        {
            case "hero":
            case "restless":
            {
                if (s.HeroId is int h) return Hero(town, h) is { } a && (need == "hero" || a.Hero!.Restless);
                if (!bind) return false;
                var a2 = Pick(town.Heroes.Where(a => !a.IsAway && (need == "hero" || a.Hero!.Restless)).OrderBy(a => a.Id).ToList());
                s.HeroId = a2?.Id;
                return a2 != null;
            }
            case "party":
            {
                if (s.PartyId is int pid) return town.Parties.FirstOrDefault(p => p.Id == pid) is { Returned: false };
                if (!bind) return false;
                var p = Pick(town.Parties.Where(p => !p.Returned && p.Members.Count > 0).OrderBy(p => p.Id).ToList());
                if (p == null) return false;
                s.PartyId = p.Id;
                s.HeroId = p.Members[0];
                return true;
            }
            case "menace":
            {
                if (s.MenaceId is int mid) return town.Saga.Menaces[mid - 1].Alive;
                if (!bind) return false;
                var m = Pick(town.Saga.Menaces.Where(m => m.Alive && m.Named && m.ArrivalTick == null).OrderBy(m => m.Id).ToList());
                s.MenaceId = m?.Id;
                return m != null;
            }
            case "march":
            {
                if (s.MenaceId is int mid) return town.Saga.Menaces[mid - 1] is { Alive: true, ArrivalTick: int };
                if (s.Warband) return town.Enemy.Stage == ThreatStage.Marching;
                if (!bind) return false;
                var m = Pick(town.Saga.Menaces.Where(m => m.Alive && m.ArrivalTick is int at && at - town.Tick >= 36).OrderBy(m => m.Id).ToList());
                if (m != null)
                {
                    s.MenaceId = m.Id;
                    return true;
                }
                if (town.Enemy.Stage == ThreatStage.Marching && town.Enemy.ArrivalTick is int wat && wat - town.Tick >= 36)
                {
                    s.Warband = true;
                    return true;
                }
                return false;
            }
            case "warband":
                if (town.Enemy.Stage is not (ThreatStage.Probing or ThreatStage.Raids)) return false;
                s.Warband = true;
                return true;
            case "faction":
            {
                if (s.FactionId is string fid) return Factions.Find(town, fid) is { Active: true };
                if (!bind) return false;
                var f = Pick(town.Factions.Where(f => f.Active && f.Known).OrderBy(f => f.Id, StringComparer.Ordinal).ToList());
                s.FactionId = f?.Id;
                return f != null;
            }
            case "seat":
            {
                if (s.SeatId is string sid) return town.Seats.FirstOrDefault(x => x.Def.Id == sid)?.HolderId != null;
                if (!bind) return false;
                var seat = Pick(town.Seats.Where(x => x.HolderId != null).ToList());
                s.SeatId = seat?.Def.Id;
                return seat != null;
            }
            case "tavern":
            case "temple":
            {
                if (s.BuildingId is int bid) return town.Building(bid) is { Complete: true, Closed: false };
                if (!bind) return false;
                var list = need == "tavern" ? town.Taverns : town.Buildings.Where(b => b.Complete && b.Def.Id == town.Content.Shrines.Temple).ToList();
                var b = Pick(list.OrderBy(b => b.Id).ToList());
                s.BuildingId = b?.Id;
                return b != null;
            }
            case "site":
            case "unheard":
            {
                if (s.SiteId is string sid) return Quests.Site(town, sid) is { Cleared: false };
                if (!bind) return false;
                var known = need == "site";
                var site = Pick(town.Sites
                    .Where(x => !x.Cleared && (known ? x.Known != SiteKnowledge.Unheard && !x.ThoughtCleared : x.Known == SiteKnowledge.Unheard && x.Def.Tier <= 3))
                    .OrderBy(x => x.Def.Distance).ThenBy(x => x.Def.Id, StringComparer.Ordinal).Take(6).ToList());
                s.SiteId = site?.Def.Id;
                return site != null;
            }
            case "power":
            {
                if (s.Power != null) return true;
                var h = town.Content.History;
                var fallen = h.Powers.Where(p => p.To < h.Year).OrderBy(p => p.Id, StringComparer.Ordinal).ToList();
                if (fallen.Count == 0) return false;
                if (bind) s.Power = fallen[town.Rng.Range(0, fallen.Count)].Id;
                return true;
            }
            case "hungry":
                return town.Market.Price(Good.Food) >= town.Market.BasePrice(Good.Food) * 1.5;
            case "homeless":
                return town.Workers.Count(a => a.HomeId == null) >= 3;
            case "rich":
                return town.Treasury >= 400;
            case "poor":
                return town.Treasury < 150;
            case "watch":
                return town.Watch >= 1;
            default:
                throw new InvalidDataException($"Unknown story need {need}");
        }
    }

    private static Agent? Hero(Town town, int id) => town.Agent(id) is { Departed: false, Hero: not null } a ? a : null;

    /// <summary>Whether an option can be taken now: its own needs, its cost, and the story's bindings still holding.</summary>
    public static string? Blocked(Town town, Story s, StoryOption o)
    {
        if (o.Cost > 0 && town.Treasury < o.Cost) return $"needs {Js.Str(o.Cost)} gold";
        foreach (var need in o.Needs)
            if (!Meet(town, need, s, bind: false) && !MeetFresh(town, need)) return need switch
            {
                "temple" => "needs a temple",
                "watch" => "needs the watch",
                "tavern" => "needs a tavern",
                "rich" => "needs a full treasury",
                "hero" => "needs a hero in town",
                _ => $"needs {need}",
            };
        return null;
    }

    /// <summary>A need an option names that the story did not bind: checked against the town without binding anything.</summary>
    private static bool MeetFresh(Town town, string need) => need switch
    {
        "temple" => town.Buildings.Any(b => b.Complete && b.Def.Id == town.Content.Shrines.Temple),
        "tavern" => town.Taverns.Count > 0,
        "hero" => town.Heroes.Any(a => !a.IsAway),
        _ => Meet(town, need, new Story(), bind: false),
    };

    /// <summary>Why a check would have advantage now, or "" if it would not.</summary>
    public static string Advantage(Town town, Story s, StoryOption o, bool inspired)
    {
        if (!o.Rolls) return "";
        if (inspired && town.Stories.Mayor.Inspiration > 0) return "inspiration";
        if (o.Aid == "hero" && s.HeroId is int h && Hero(town, h) is { IsAway: false } a) return $"{a.GivenName} lends a hand";
        var bg = Rules(town).Background(town.Stories.Mayor.Background);
        if (bg.Favoured != "" && bg.Favoured == DefOf(town, s).Source) return bg.Feature;
        return "";
    }

    /// <summary>The chance the check succeeds (a natural 20 always does, a natural 1 never), with advantage if it has it.</summary>
    public static double Chance(Town town, Story s, StoryOption o, bool inspired = false)
    {
        if (!o.Rolls) return 1;
        var (mod, prof, _) = Bonus(town, o.Skill, o.Ability);
        int faces = 0;
        for (int f = 1; f <= 20; f++)
            if (f == 20 || (f != 1 && f + mod + prof >= o.Dc)) faces++;
        double p = faces / 20.0;
        return Advantage(town, s, o, inspired) != "" ? 1 - (1 - p) * (1 - p) : p;
    }

    /// <summary>
    /// The mayor's answer to a story's current stage: option by index, with inspiration spent for
    /// advantage if asked. Rolls the check, applies the outcome, and moves the story on or ends it.
    /// </summary>
    public static StoryBeat? Resolve(Town town, int storyId, int option, bool inspired = false)
    {
        if (Find(town, storyId) is not { Done: false } s) return null;
        var stage = StageOf(town, s);
        if (option < 0 || option >= stage.Options.Count) return null;
        var o = stage.Options[option];
        if (Blocked(town, s, o) != null) return null;
        var m = town.Stories.Mayor;
        if (o.Cost > 0) town.Book(TreasuryAccount.Stories, -o.Cost);
        var beat = new StoryBeat { Tick = town.Tick, Stage = stage.Id, Option = o.Label, Skill = o.Skill != "" ? o.Skill : o.Ability, Dc = o.Dc };
        StoryOutcome outcome;
        if (!o.Rolls)
        {
            beat.Result = StoryResult.Certain;
            outcome = o.Success;
        }
        else
        {
            string adv = Advantage(town, s, o, inspired);
            if (adv == "inspiration") m.Inspiration--;
            var (mod, prof, _) = Bonus(town, o.Skill, o.Ability);
            int die = town.Rng.D20();
            if (adv != "")
            {
                int other = town.Rng.D20();
                beat.Other = Math.Min(die, other);
                die = Math.Max(die, other);
            }
            beat.Advantage = adv;
            beat.Die = die;
            beat.Modifier = mod;
            beat.Proficiency = prof;
            beat.Total = die + mod + prof;
            beat.Result = die == 20 ? StoryResult.Critical
                : die == 1 ? StoryResult.Fumble
                : beat.Total >= o.Dc ? StoryResult.Success
                : o.Partial != null && o.Dc - beat.Total <= Rules(town).PartialMargin ? StoryResult.Partial
                : StoryResult.Failure;
            outcome = beat.Result switch
            {
                StoryResult.Critical => o.Critical ?? o.Success,
                StoryResult.Success => o.Success,
                StoryResult.Partial => o.Partial!,
                StoryResult.Fumble => o.Fumble ?? o.Failure,
                _ => o.Failure,
            };
            if (beat.Result is StoryResult.Critical or StoryResult.Success) m.Successes++;
            else m.Failures++;
            if (die == 20 && m.Inspiration == 0) m.Inspiration = 1;
        }
        Conclude(town, s, beat, outcome);
        return beat;
    }

    /// <summary>A story nobody stepped into runs its course.</summary>
    private static void Lapse(Town town, Story s)
    {
        var outcome = StageOf(town, s).Ignored ?? DefOf(town, s).Ignored ?? new StoryOutcome { Text = "In the end it comes to nothing." };
        var beat = new StoryBeat { Tick = town.Tick, Stage = s.Stage, Option = "Let it run its course", Result = StoryResult.Ignored };
        // A story left alone ends there, whatever its outcome would lead to.
        Conclude(town, s, beat, outcome with { Next = "" });
    }

    private static void Conclude(Town town, Story s, StoryBeat beat, StoryOutcome outcome)
    {
        var d = DefOf(town, s);
        string effects = Apply(town, s, outcome.Effects);
        beat.Text = Fill(town, s, outcome.Text);
        s.Beats.Add(beat);
        string roll = beat.Result is StoryResult.Certain or StoryResult.Ignored ? "" : $" ({Rules(town).Skill(beat.Skill)?.Name ?? Cap(beat.Skill)} {beat.Total} against DC {beat.Dc}: {Word(beat.Result)})";
        bool goesOn = outcome.Next != "" && d.Stages.Any(x => x.Id == outcome.Next);
        town.Log(LedgerCategory.Story, d.Stakes == "high" && !goesOn ? Importance.Major : Importance.Notable,
            $"{Fill(town, s, d.Title)}: {(beat.Result == StoryResult.Ignored ? "left to run its course" : $"Mayor {town.Stories.Mayor.Name} chose to {Lower(beat.Option)}")}{roll}. {beat.Text}{effects}", Agents(s));
        if (goesOn)
        {
            s.Stage = outcome.Next;
            s.DueTick = Math.Max(s.DueTick, town.Tick + 2 * SimTime.HoursPerDay);
            return;
        }
        s.Done = true;
        if (beat.Result != StoryResult.Ignored) GainXp(town, Rules(town).StakesOf(d.Stakes).Xp);
    }

    public static string Word(StoryResult r) => r switch
    {
        StoryResult.Critical => "a natural 20",
        StoryResult.Success => "success",
        StoryResult.Partial => "a success at a cost",
        StoryResult.Fumble => "a natural 1",
        StoryResult.Failure => "failure",
        _ => "",
    };

    /// <summary>Apply an outcome's effects in a fixed order; returns a short account of the plain numbers for the Ledger.</summary>
    private static string Apply(Town town, Story s, Dictionary<string, double> effects)
    {
        var said = new List<string>();
        foreach (var key in effects.Keys)
            if (!EffectKeys.Contains(key)) throw new InvalidDataException($"Unknown story effect {key} in {s.Def}");
        foreach (var key in EffectKeys)
        {
            if (!effects.TryGetValue(key, out double v) || v == 0) continue;
            switch (key)
            {
                case "gold":
                    town.Book(TreasuryAccount.Stories, v);
                    said.Add($"{(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} gold");
                    break;
                case "mandate":
                    town.Mandate = Math.Clamp(town.Mandate + v, 0, 100);
                    said.Add($"Mandate {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    break;
                case "dread":
                    town.Saga.Dread = Math.Clamp(town.Saga.Dread + v, 0, 100);
                    if (town.Saga.Enabled) said.Add($"dread {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    break;
                case "tension":
                    town.Tension = Math.Max(0, town.Tension + v);
                    break;
                case "food":
                case "timber":
                {
                    var g = key == "food" ? Good.Food : Good.Timber;
                    if (v > 0) town.Market.Seed(g, v);
                    else town.Market.Seed(g, -Math.Min(town.Market.Stock(g), -v));
                    said.Add($"{(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} {key}");
                    break;
                }
                case "loyalty":
                    if (s.HeroId is int h && Hero(town, h) is { } a)
                    {
                        a.Hero!.Loyalty = Math.Clamp(a.Hero.Loyalty + v, 0, 100);
                        said.Add($"{a.GivenName}'s loyalty {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "wound":
                    if (s.HeroId is int w && Hero(town, w) is { } wa)
                        wa.Hero!.Hp = Math.Max(1, wa.Hero.Hp - (int)v);
                    break;
                case "favour":
                {
                    var god = Faith.TempleGod(town) ?? Faith.CharterGod(town);
                    town.Faiths.Favor[god.Id] = Math.Max(0, town.Faiths.Favor.Get(god.Id, 0) + v);
                    said.Add($"{Faith.Prose(god)}'s favour {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    break;
                }
                case "standing":
                    if (s.FactionId is string fid && Factions.Find(town, fid) is { } f)
                    {
                        f.Standing = Math.Clamp(f.Standing + v, -100, 100);
                        said.Add($"{f.Name}'s standing {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "seat":
                    if (s.SeatId is string sid && town.Seats.FirstOrDefault(x => x.Def.Id == sid) is { } seat)
                        seat.Loyalty = Math.Clamp(seat.Loyalty + v, 0, 100);
                    break;
                case "foePower":
                    // A fraction: -0.1 takes a tenth of the foe's strength.
                    if (s.MenaceId is int mid && town.Saga.Menaces[mid - 1] is { Alive: true } m) m.Power = Math.Max(1, m.Power * (1 + v));
                    else if (s.Warband) town.Enemy.Strength = Math.Max(1, town.Enemy.Strength * (1 + v));
                    break;
                case "delay":
                    // Hours added to (or taken from) the foe's march.
                    if (s.MenaceId is int dm && town.Saga.Menaces[dm - 1] is { Alive: true, ArrivalTick: int at }) town.Saga.Menaces[dm - 1].ArrivalTick = Math.Max(town.Tick + 1, at + (int)v);
                    else if (s.Warband && town.Enemy.ArrivalTick is int wat) town.Enemy.ArrivalTick = Math.Max(town.Tick + 1, wat + (int)v);
                    break;
                case "festival":
                    town.FestivalDays += (int)v;
                    break;
                case "reveal":
                    if (s.SiteId is string site && Quests.Site(town, site) is { } ss)
                        Rumors.Hear(town, ss, Rumors.Occupants(town.Content, ss.Def), ss.Def.Tier, 70);
                    break;
                case "inspiration":
                    town.Stories.Mayor.Inspiration = Math.Clamp(town.Stories.Mayor.Inspiration + (int)v, 0, 1);
                    break;
            }
        }
        return said.Count == 0 ? "" : $" ({string.Join(", ", said)})";
    }

    /// <summary>The stand-in mayor's answer: the likeliest option it can afford, spending inspiration when the stakes are high and the odds poor.</summary>
    private static void StandIn(Town town, Story s)
    {
        for (int guard = 0; guard < 8 && !s.Done; guard++)
        {
            var stage = StageOf(town, s);
            // Gold counts against an option: a sure thing that costs 100 gold weighs like an even chance.
            int best = -1;
            double bestScore = double.MinValue, bestChance = 0;
            for (int i = 0; i < stage.Options.Count; i++)
            {
                var o = stage.Options[i];
                if (Blocked(town, s, o) != null || (o.Cost > 0 && town.Treasury < o.Cost + 100)) continue;
                double c = Chance(town, s, o);
                double score = c - o.Cost / 200;
                if (score > bestScore + 1e-9)
                {
                    best = i;
                    bestScore = score;
                    bestChance = c;
                }
            }
            if (best < 0)
            {
                Lapse(town, s);
                return;
            }
            bool inspire = DefOf(town, s).Stakes == "high" && bestChance < 0.6 && town.Stories.Mayor.Inspiration > 0;
            Resolve(town, s.Id, best, inspire);
        }
    }

    // ---------- Text ----------

    /// <summary>Fill a story's text with what it is about: {hero}, {menace}, {faction}, {seat}, {councillor}, {tavern}, {temple}, {site}, {party}, {power}, {god}, {town}, {mayor}.</summary>
    public static string Fill(Town town, Story s, string text)
    {
        if (!text.Contains('{')) return text;
        var sb = new System.Text.StringBuilder(text);
        void Put(string key, Func<string> value)
        {
            if (text.Contains(key)) sb.Replace(key, value());
        }
        Put("{town}", () => town.Name);
        Put("{mayor}", () => town.Stories.Mayor.Name);
        Put("{hero}", () => s.HeroId is int h ? town.Agent(h).Name : "a hero");
        Put("{heroName}", () => s.HeroId is int h ? town.Agent(h).GivenName : "the hero");
        Put("{menace}", () => s.MenaceId is int m ? town.Saga.Menaces[m - 1].Name : s.Warband ? town.Content.Enemy.Name : "the enemy");
        Put("{faction}", () => s.FactionId is string f && Factions.Find(town, f) is { } fs ? fs.Name : "a faction");
        Put("{seat}", () => s.SeatId is string id && town.Seats.FirstOrDefault(x => x.Def.Id == id) is { } seat ? seat.Def.Title : "a councillor");
        Put("{councillor}", () => s.SeatId is string id && town.Seats.FirstOrDefault(x => x.Def.Id == id)?.HolderId is int holder ? town.Agent(holder).Name : "a councillor");
        Put("{tavern}", () => s.BuildingId is int b ? town.Building(b).Name : "the tavern");
        Put("{temple}", () => s.BuildingId is int b ? town.Building(b).Name : "the temple");
        Put("{site}", () => s.SiteId is string id && Quests.Site(town, id) is { } site ? site.Def.Name : "the ruin");
        Put("{party}", () => s.PartyId is int p && town.Parties.FirstOrDefault(x => x.Id == p) is { } party ? party.Name : "the party");
        Put("{power}", () => s.Power is string pid && town.Content.History.Powers.FirstOrDefault(p => p.Id == pid) is { } pw ? pw.Name : "an old power");
        Put("{god}", () => Faith.Prose(Faith.TempleGod(town) ?? Faith.CharterGod(town)));
        return Cap(sb.ToString());
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
