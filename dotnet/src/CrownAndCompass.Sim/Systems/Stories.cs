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

    /// <summary>Standing orders a story can put in force for a while (<see cref="Decrees"/>).</summary>
    public List<DecreeDef> Decrees { get; init; } = [];

    /// <summary>What the town remembers the mayor deciding, which later stories turn on.</summary>
    public List<PrecedentDef> Precedents { get; init; } = [];

    /// <summary>Lasting parts heroes take up in the town through the stories of their class (<see cref="Callings"/>).</summary>
    public List<CallingDef> Callings { get; init; } = [];

    /// <summary>Inspiration the mayor can hold at once, before feats.</summary>
    public int InspirationMax { get; init; } = 3;

    /// <summary>Ways of running the town well that earn the mayor inspiration.</summary>
    public List<DeedDef> Deeds { get; init; } = [];

    /// <summary>Feats the mayor can take at an improvement level instead of raising abilities.</summary>
    public List<FeatDef> Feats { get; init; } = [];

    /// <summary>The first level at which a skill-level advance can be expertise.</summary>
    public int ExpertiseFrom { get; init; } = 3;

    /// <summary>The checks the mayor rolls on everyday decisions (council motions, demands, envoys...), by decision.</summary>
    public Dictionary<string, DecisionCheck> Decisions { get; init; } = [];

    public FeatDef? Feat(string id) => Feats.FirstOrDefault(f => f.Id == id);

    public StakesDef StakesOf(string id) => Stakes.FirstOrDefault(s => s.Id == id) ?? Stakes[0];
    public SkillDef? Skill(string id) => Skills.FirstOrDefault(s => s.Id == id);
    public MayorBackground Background(string id) => Backgrounds.FirstOrDefault(b => b.Ref == id) ?? Backgrounds[0];
    public StoryDef? Story(string id) => Stories.FirstOrDefault(s => s.Id == id);
    public DecreeDef? Decree(string id) => Decrees.FirstOrDefault(d => d.Id == id);
    public PrecedentDef? Precedent(string id) => Precedents.FirstOrDefault(p => p.Id == id);
    public CallingDef? Calling(string id) => Callings.FirstOrDefault(c => c.Id == id);
}

/// <summary>Something the town remembers: a gate opened or closed, a pact made, a witch burned or sheltered.</summary>
public sealed record PrecedentDef
{
    public string Id { get; init; } = "";

    /// <summary>How the Mayor tab tells it ("The gates were opened to the refugees of {town}").</summary>
    public string Text { get; init; } = "";
}

public sealed record StakesDef
{
    public string Id { get; init; } = "low";
    public string Name { get; init; } = "";
    public int DueDays { get; init; } = 3;
    public int Xp { get; init; } = 20;
}

/// <summary>A way of running the town well that earns inspiration: every <see cref="Every"/> of its kind (charters, bounties, raids thrown back, menaces ended, days at or above <see cref="Threshold"/> Mandate).</summary>
public sealed record DeedDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public int Every { get; init; } = 1;
    public double Threshold { get; init; }
}

/// <summary>A feat: a bonus on some skills, more inspiration or a steady way of regaining it, a floor under the die, a wider margin for a success at a cost.</summary>
public sealed record FeatDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public Dictionary<string, int> Skills { get; init; } = [];
    public int Inspiration { get; init; }
    public int RegainDays { get; init; }
    public int Floor { get; init; }
    public int PartialMargin { get; init; }
}

/// <summary>A d20 check on an everyday decision: its skill and DC, and how much a success adds (what it adds depends on the decision).</summary>
public sealed record DecisionCheck
{
    public string Skill { get; init; } = "";
    public int Dc { get; init; } = 12;
    public double Gain { get; init; }
    public string Success { get; init; } = "";
    public string Failure { get; init; } = "";
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

    /// <summary>Weight added for every hundred people: petitions come more often as the town grows.</summary>
    public double PerHundred { get; init; }

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

    /// <summary>How much the stand-in mayor leans to this option over its odds (a hard choice's sensible default).</summary>
    public double Prefer { get; init; }
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

    /// <summary>D&amp;D inspiration: spend one to reroll a failed check. Running the town well and natural 20s earn it.</summary>
    public int Inspiration { get; set; }

    /// <summary>What each level after the first was spent on: "ability:str,cha", "skill:arcana", "expertise:persuasion" or "feat:lucky".</summary>
    public List<string> Advances { get; } = [];
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

    /// <summary>The die the mayor spent inspiration to reroll, if they did.</summary>
    public int? Rerolled { get; set; }
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

    /// <summary>The townsperson who brought a petition, and the ward it is about.</summary>
    public int? PetitionerId { get; set; }
    public string? Ward { get; set; }

    /// <summary>The settlement out in the wilds a story is about (its people, a trade route there).</summary>
    public int? SettlementId { get; set; }

    /// <summary>The fallen hero in the hall of heroes the story is about.</summary>
    public int? FallenId { get; set; }
    public List<StoryBeat> Beats { get; } = [];
}

public sealed class StoryState
{
    public MayorSheet Mayor { get; set; } = new();
    public List<Story> All { get; } = [];
    public int NextDay { get; set; }

    /// <summary>The day each story last opened, so it does not repeat too soon.</summary>
    public OrderedMap<string, int> LastDay { get; } = new();

    /// <summary>Decrees in force, and the day each lapses.</summary>
    public OrderedMap<string, int> Decrees { get; } = new();

    /// <summary>Precedents set, and the day each was.</summary>
    public OrderedMap<string, int> Precedents { get; } = new();

    /// <summary>Callings held, by the hero holding each.</summary>
    public OrderedMap<int, string> Callings { get; } = new();

    public IEnumerable<Story> Open => All.Where(s => !s.Done);

    /// <summary>Each deed's count already rewarded with inspiration.</summary>
    public OrderedMap<string, int> Deeds { get; } = new();

    /// <summary>Raids and assaults thrown back from the town.</summary>
    public int Repelled { get; set; }

    /// <summary>Days in a row at or above the contented-town Mandate.</summary>
    public int ContentDays { get; set; }

    /// <summary>The last check rolled on an everyday decision, and how many have been rolled (so the client shows each once).</summary>
    public StoryBeat? LastCheck { get; set; }
    public int Checks { get; set; }
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

    /// <summary>
    /// Every effect an outcome can have, in the order they apply; then decree:id (days, or -1 to repeal),
    /// precedent:id (1 sets it, -1 forgets it) and calling:id (1: the story's hero takes it up, -1: it ends).
    /// </summary>
    public static readonly string[] EffectKeys =
        ["gold", "mandate", "dread", "tension", "food", "timber", "loyalty", "wound", "favour", "standing", "seat", "foePower", "delay", "festival", "reveal", "inspiration", "people", "leaves", "renown", "heat", "expose", "regard", "retire", "memorial", "stir"];

    /// <summary>
    /// Every need a story or option can name; besides these, precedent:id, decree:id, season:id and
    /// calling:id (held; binds its hero), each negated with a leading !, and class:id (binds a hero of the
    /// class in town with no calling yet) and faction:id (binds that faction while it has a chapter).
    /// </summary>
    public static readonly string[] Needs =
        ["hero", "restless", "party", "menace", "march", "warband", "faction", "seat", "tavern", "temple", "site", "unheard", "power", "hungry", "homeless", "rich", "watch", "poor", "petitioner", "ward", "settlement", "route", "courted", "veteran", "fallen", "below"];

    private static readonly string[] Prefixed = ["precedent:", "decree:", "season:"];

    /// <summary>Whether content may name this need: a plain one, or a precedent, decree or season the content defines.</summary>
    public static bool KnownNeed(Content c, string need)
    {
        if (Needs.Contains(need)) return true;
        string n = need.TrimStart('!');
        if (n.StartsWith("precedent:", StringComparison.Ordinal)) return c.Stories.Precedent(n[10..]) != null;
        if (n.StartsWith("decree:", StringComparison.Ordinal)) return c.Stories.Decree(n[7..]) != null;
        if (n.StartsWith("season:", StringComparison.Ordinal)) return c.Seasons.Seasons.Any(x => x.Id == n[7..]);
        if (n.StartsWith("calling:", StringComparison.Ordinal)) return c.Stories.Calling(n[8..]) != null;
        if (need.StartsWith("class:", StringComparison.Ordinal)) return c.Classes.Any(x => x.Id == need[6..]);
        if (need.StartsWith("faction:", StringComparison.Ordinal)) return c.Factions.Factions.Any(x => x.Ref == need[8..]);
        return false;
    }

    /// <summary>Whether content may name this effect: a plain one, or a decree or precedent the content defines.</summary>
    public static bool KnownEffect(Content c, string key) =>
        EffectKeys.Contains(key)
        || key.StartsWith("decree:", StringComparison.Ordinal) && c.Stories.Decree(key[7..]) != null
        || key.StartsWith("precedent:", StringComparison.Ordinal) && c.Stories.Precedent(key[10..]) != null
        || key.StartsWith("calling:", StringComparison.Ordinal) && c.Stories.Calling(key[8..]) != null;

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

    /// <summary>The mayor's ability scores: the standard array in the background's order, +2 and +1 from it, then each ability advance taken (at most 20).</summary>
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
        foreach (var adv in m.Advances)
            if (adv.StartsWith("ability:", StringComparison.Ordinal))
                foreach (var ab in adv[8..].Split(','))
                    a[ab] = Math.Min(20, a[ab] + 1);
        return a;
    }

    public static int ProficiencyBonus(int level) => 2 + (level - 1) / 4;

    public static bool Proficient(Town town, string skill) =>
        Rules(town).Background(town.Stories.Mayor.Background).Skills.Contains(skill) || town.Stories.Mayor.Advances.Contains("skill:" + skill);

    public static bool Expert(Town town, string skill) => town.Stories.Mayor.Advances.Contains("expertise:" + skill);

    /// <summary>
    /// The mayor's bonus on a check: the ability's modifier, plus proficiency in the skill (doubled with
    /// expertise) and any feat's bonus on it.
    /// </summary>
    public static (int Modifier, int Proficiency, string Ability) Bonus(Town town, string skill, string ability = "")
    {
        var sk = skill == "" ? null : Rules(town).Skill(skill);
        string ab = sk?.Ability ?? ability;
        if (ab == "") return (0, 0, "");
        int mod = Rng.Modifier(Scores(town)[ab]);
        int prof = sk != null && Proficient(town, skill) ? ProficiencyBonus(town.Stories.Mayor.Level) * (Expert(town, skill) ? 2 : 1) : 0;
        if (sk != null) prof += Sheet.Feats(town).Sum(f => f.Skills.GetValueOrDefault(skill));
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

    internal static void GainXp(Town town, int xp)
    {
        var m = town.Stories.Mayor;
        m.Xp += xp;
        var levels = Rules(town).Levels;
        while (m.Level < levels.Count && m.Xp >= levels[m.Level])
        {
            m.Level++;
            bool improves = Rules(town).Improvements.Contains(m.Level);
            town.Log(LedgerCategory.Story, Importance.Notable,
                $"Mayor {m.Name} is now a level {m.Level} {Title(town).ToLowerInvariant()}.{(ProficiencyBonus(m.Level) > ProficiencyBonus(m.Level - 1) ? $" Their proficiency bonus rises to +{ProficiencyBonus(m.Level)}." : "")} They can {(improves ? "raise their abilities or take a feat" : "learn a skill or sharpen one")}.");
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
        Decrees.Daily(town);
        Callings.Daily(town);
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
        double total = Js.Sum(fits, f => WeightOf(town, f.Def));
        double roll = town.Rng.Next() * total;
        var pick = fits[^1];
        foreach (var f in fits)
        {
            roll -= WeightOf(town, f.Def);
            if (roll < 0)
            {
                pick = f;
                break;
            }
        }
        Begin(town, pick.Def, pick.Bound);
    }

    private static double WeightOf(Town town, StoryDef d) => d.Weight + d.PerHundred * town.Population / 100.0;

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

    private static List<int> Agents(Story s) => s.HeroId is int h ? [h] : s.PetitionerId is int p ? [p] : [];

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
            case "petitioner":
            {
                if (s.PetitionerId is int pid) return town.Agent(pid) is { Departed: false };
                if (!bind) return false;
                var a = Pick(town.Workers.Where(a => a.HomeId != null && a.Age >= 18).OrderBy(a => a.Id).ToList());
                s.PetitionerId = a?.Id;
                return a != null;
            }
            case "ward":
            {
                if (s.Ward != null) return true;
                if (!bind) return false;
                var open = town.City.OpenWards;
                // A petition comes from the newer wards when there are any: the old town has had its say.
                var wards = open.Count > 1 ? open.Where(w => w != open[0]).ToList() : open.ToList();
                s.Ward = wards.Count == 0 ? null : wards[town.Rng.Range(0, wards.Count)];
                return s.Ward != null;
            }
            case "settlement":
            case "route":
            case "courted":
            {
                bool Fits(Settlement x) => !x.Razed && town.Regions[x.RegionId].Known && need switch
                {
                    "route" => Trade.Open(town).Any(r => r.SettlementId == x.Id),
                    "courted" => !x.Allied && x.Standing >= 25,
                    _ => x.Standing > -20,
                };
                if (s.SettlementId is int sid) return Fits(town.Settlements[sid]);
                if (!bind) return false;
                var found = Pick(town.Settlements.Where(Fits).OrderBy(x => x.Id).ToList());
                s.SettlementId = found?.Id;
                return found != null;
            }
            case "veteran":
            {
                if (s.HeroId is int vh) return Hero(town, vh) is { } va && Legacies.Veteran(town, va);
                if (!bind) return false;
                var v = Pick(town.Heroes.Where(a => Legacies.Veteran(town, a)).OrderBy(a => a.Id).ToList());
                s.HeroId = v?.Id;
                return v != null;
            }
            case "below":
            {
                // A level of the undercity, open and not cleared: its site is the story's.
                bool Live(UnderLevel l) => !Undercity.Site(town, l).Cleared;
                if (s.SiteId is string us) return Undercity.LevelOf(town, us) is { } ul && Live(ul);
                if (!bind) return false;
                var lv = Pick(town.Undercity.Where(Live).ToList());
                s.SiteId = lv == null ? null : Undercity.Def(town, lv).Site.Id;
                return lv != null;
            }
            case "fallen":
            {
                // A hero fallen lately, with no memorial yet.
                bool Recent(Legacy l) => !l.Retired && !l.Memorial && town.Day - l.Day <= 30;
                if (s.FallenId is int fid) return Legacies.Of(town, fid) is { } fl && !fl.Retired && !fl.Memorial;
                if (!bind) return false;
                var f = Pick(town.Legacies.Where(Recent).OrderBy(l => l.HeroId).ToList());
                s.FallenId = f?.HeroId;
                return f != null;
            }
            default:
            {
                if (need.StartsWith("class:", StringComparison.Ordinal))
                {
                    string cls = need[6..];
                    if (s.HeroId is int h) return Hero(town, h) is { } a && a.Hero!.Cls.Id == cls;
                    if (!bind) return false;
                    var a2 = Pick(town.Heroes.Where(a => !a.IsAway && a.Hero!.Cls.Id == cls && Callings.Of(town, a) == null).OrderBy(a => a.Id).ToList());
                    s.HeroId = a2?.Id;
                    return a2 != null;
                }
                if (need.StartsWith("faction:", StringComparison.Ordinal))
                {
                    string fid = need[8..];
                    if (s.FactionId is string bound) return bound == fid && Factions.Find(town, fid) is { Active: true };
                    if (Factions.Find(town, fid) is not { Active: true }) return false;
                    if (bind) s.FactionId = fid;
                    return true;
                }
                bool not = need.StartsWith('!');
                string n = not ? need[1..] : need;
                bool holds;
                if (n.StartsWith("precedent:", StringComparison.Ordinal)) holds = town.Stories.Precedents.ContainsKey(n[10..]);
                else if (n.StartsWith("decree:", StringComparison.Ordinal)) holds = Decrees.InForce(town, n[7..]);
                else if (n.StartsWith("season:", StringComparison.Ordinal)) holds = Seasons.Current(town).Id == n[7..];
                else if (n.StartsWith("calling:", StringComparison.Ordinal))
                {
                    // A calling held binds the hero who holds it.
                    string id = n[8..];
                    if (s.HeroId is int bound) holds = Hero(town, bound) is { } a && Callings.Of(town, a)?.Id == id;
                    else
                    {
                        var holder = Callings.Holder(town, id);
                        holds = holder != null;
                        if (holds && !not && bind) s.HeroId = holder!.Id;
                    }
                }
                else throw new InvalidDataException($"Unknown story need {need}");
                return holds != not;
            }
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
    public static string Advantage(Town town, Story s, StoryOption o)
    {
        if (!o.Rolls) return "";
        if (o.Aid == "hero" && s.HeroId is int h && Hero(town, h) is { IsAway: false } a) return $"{a.GivenName} lends a hand";
        var bg = Rules(town).Background(town.Stories.Mayor.Background);
        if (bg.Favoured != "" && bg.Favoured == DefOf(town, s).Source) return bg.Feature;
        return "";
    }

    /// <summary>The chance the check succeeds, with advantage if it has it and an inspired reroll if one is asked for and in hand.</summary>
    public static double Chance(Town town, Story s, StoryOption o, bool inspired = false) =>
        !o.Rolls ? 1 : Sheet.Chance(town, o.Skill, o.Ability, o.Dc, Advantage(town, s, o) != "", inspired);

    /// <summary>
    /// The mayor's answer to a story's current stage: option by index, with inspiration spent to reroll
    /// a failed check if asked. Rolls the check, applies the outcome, and moves the story on or ends it.
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
        StoryBeat beat;
        StoryOutcome outcome;
        if (!o.Rolls)
        {
            beat = new StoryBeat { Tick = town.Tick, Skill = o.Skill != "" ? o.Skill : o.Ability, Dc = o.Dc, Result = StoryResult.Certain };
            outcome = o.Success;
        }
        else
        {
            beat = Sheet.Roll(town, o.Skill, o.Ability, o.Dc, Advantage(town, s, o), inspired, o.Partial != null);
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
        }
        beat.Stage = stage.Id;
        beat.Option = Fill(town, s, o.Label);
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
            if (!KnownEffect(town.Content, key)) throw new InvalidDataException($"Unknown story effect {key} in {s.Def}");
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
                    Sheet.Inspire(town, (int)v, "");
                    break;
                case "people":
                {
                    int n = Population.Settle(town, (int)v);
                    if (n != 0) said.Add($"{(n > 0 ? "+" : "−")}{Math.Abs(n)} {(Math.Abs(n) == 1 ? "settler" : "settlers")}");
                    break;
                }
                case "regard":
                    if (s.SettlementId is int rs && town.Settlements[rs] is { Razed: false } st)
                    {
                        World.Shift(town, st, v);
                        said.Add($"{st.Name}'s regard {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "leaves":
                    // The petitioner, and their household, give up on the town.
                    if (s.PetitionerId is int pid && town.Agent(pid) is { Departed: false } pa)
                    {
                        Population.Depart(town, pa);
                        if (pa.SpouseId is int sp && town.Agent(sp) is { Departed: false } spouse) Population.Depart(town, spouse);
                        said.Add($"{pa.GivenName} leaves {town.Name}");
                    }
                    break;
                case "retire":
                    if (v > 0 && s.HeroId is int th && Hero(town, th) is { } ta) said.Add(Legacies.Retire(town, ta));
                    break;
                case "stir":
                    if (s.SiteId is string usid && Undercity.LevelOf(town, usid) is { } sl)
                    {
                        sl.Stir = Math.Max(0, sl.Stir + v);
                        said.Add($"{Undercity.Def(town, sl).Name} {(v > 0 ? "stir" : "quiet")} ({(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))})");
                    }
                    break;
                case "memorial":
                    if (v > 0 && s.FallenId is int mf && Legacies.Of(town, mf) is { Memorial: false } ml)
                    {
                        Legacies.Honor(town, ml);
                        said.Add($"a memorial to {ml.Name}");
                    }
                    break;
                case "renown":
                    if (s.HeroId is int rh && Hero(town, rh) is { } ra) ra.Hero!.Renown = Math.Max(0, ra.Hero.Renown + (int)v);
                    break;
                case "heat":
                    if (s.FactionId is string hf && Factions.Find(town, hf) is { } hfs) hfs.Heat = Math.Clamp(hfs.Heat + v, 0, 100);
                    break;
                case "expose":
                    // The hero names the faction's members to the watch.
                    if (v > 0 && s.FactionId is string ef && Factions.Find(town, ef) is { Active: true, Known: false } efs)
                    {
                        efs.Known = true;
                        said.Add($"{efs.Name} {efs.Verb("is", "are")} known");
                    }
                    break;
            }
        }
        // Decrees and precedents follow, in the order the outcome names them.
        foreach (var (key, v) in effects.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (key.StartsWith("decree:", StringComparison.Ordinal))
            {
                string id = key[7..];
                if (v < 0) Decrees.Repeal(town, id);
                else said.Add(Decrees.Enact(town, id, (int)v));
            }
            else if (key.StartsWith("precedent:", StringComparison.Ordinal))
            {
                string id = key[10..];
                if (v < 0) town.Stories.Precedents.Remove(id);
                else if (!town.Stories.Precedents.ContainsKey(id)) town.Stories.Precedents[id] = town.Day;
            }
            else if (key.StartsWith("calling:", StringComparison.Ordinal))
            {
                string id = key[8..];
                if (s.HeroId is not int ch || Hero(town, ch) is not { } ca) continue;
                if (v > 0) said.Add(Callings.Take(town, ca, id));
                else if (Callings.Of(town, ca)?.Id == id) Callings.End(town, ca, "");
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
            double bestScore = double.MinValue;
            for (int i = 0; i < stage.Options.Count; i++)
            {
                var o = stage.Options[i];
                if (Blocked(town, s, o) != null || (o.Cost > 0 && town.Treasury < o.Cost + 100)) continue;
                double c = Chance(town, s, o);
                double score = c - o.Cost / 200 + o.Prefer;
                if (score > bestScore + 1e-9)
                {
                    best = i;
                    bestScore = score;
                }
            }
            if (best < 0)
            {
                Lapse(town, s);
                return;
            }
            // Inspiration is only spent if the roll fails, so the stand-in keeps it for anything above low stakes.
            bool inspire = DefOf(town, s).Stakes != "low";
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
        Put("{petitioner}", () => s.PetitionerId is int p ? town.Agent(p).Name : "a townsman");
        Put("{petitionerName}", () => s.PetitionerId is int p ? town.Agent(p).GivenName : "the petitioner");
        Put("{trade}", () => s.PetitionerId is int p ? town.Agent(p).Profession.ToLowerInvariant() : "townsman");
        Put("{ward}", () => s.Ward is string w ? City.Ward(town, w).Name : "the old town");
        Put("{settlement}", () => s.SettlementId is int st ? town.Settlements[st].Name : "a far town");
        Put("{people}", () => s.SettlementId is int sp ? town.Settlements[sp].People : "the folk of a far town");
        Put("{season}", () => Seasons.Current(town).Name.ToLowerInvariant());
        Put("{fallen}", () => s.FallenId is int fh ? town.Agent(fh).Name : "a fallen hero");
        Put("{fallenName}", () => s.FallenId is int fh ? town.Agent(fh).GivenName : "the fallen");
        Put("{fallenClass}", () => s.FallenId is int fh ? town.Agent(fh).Hero?.Cls.Name.ToLowerInvariant() ?? "hero" : "hero");
        Put("{class}", () => s.HeroId is int h ? town.Agent(h).Hero?.Cls.Name.ToLowerInvariant() ?? "hero" : "hero");
        return Cap(sb.ToString());
    }

    private static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];
}
