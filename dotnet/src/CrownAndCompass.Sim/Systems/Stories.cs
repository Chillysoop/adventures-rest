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

    /// <summary>Added to every check's DC in each act of the campaign: the stakes rise as the mayor grows.</summary>
    public int[] ActDc { get; init; } = [];

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

    /// <summary>Things a campaign rolls once from its seed (what lies under the town, if anything), which stories need as variant:id:option.</summary>
    public List<StoryVariantDef> Variants { get; init; } = [];

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

    /// <summary>The most the town's readings can multiply one story's chance by (<see cref="Systems.Odds.Factor"/>).</summary>
    public double OddsCap { get; init; } = 4;

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
/// <summary>One thing a campaign rolls from its seed: its options and how likely each is.</summary>
public sealed record StoryVariantDef
{
    public string Id { get; init; } = "";
    public List<StoryVariantOption> Options { get; init; } = [];
}

public sealed record StoryVariantOption
{
    public string Id { get; init; } = "";
    public double Weight { get; init; } = 1;
}

public sealed record PrecedentDef
{
    public string Id { get; init; } = "";

    /// <summary>How the Mayor tab tells it ("The gates were opened to the refugees of {town}").</summary>
    public string Text { get; init; } = "";

    /// <summary>Days after it is set before stories that call back to it can come up (a debt takes time to fall due).</summary>
    public int After { get; init; }
}

public sealed record StakesDef
{
    public string Id { get; init; } = "low";
    public string Name { get; init; } = "";
    public int DueDays { get; init; } = 3;
    public int Xp { get; init; } = 20;
    /// <summary>
    /// The least a check at these stakes needs on the die after every bonus (0 for none): a high-stakes DC rises with
    /// the mayor's own bonus and helpers, so a seasoned mayor with good friends still fails a third of the time.
    /// </summary>
    public int Floor { get; init; }
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

    /// <summary>What in the town makes it likelier, as a weight per unit of each reading (<see cref="Systems.Odds"/>); negative makes it rarer.</summary>
    public Dictionary<string, double> Odds { get; init; } = [];

    /// <summary>The charter tier it first comes up at, and the tier the town outgrows it at (empty for none).</summary>
    public string FromTier { get; init; } = "";
    public string UntilTier { get; init; } = "";

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

    /// <summary>What the choice stands for (mercy, a hard hand...), which heroes judge by their temper (<see cref="Regard"/>).</summary>
    public List<string> Tags { get; init; } = [];

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

    /// <summary>Hours until the next stage comes (a party's next sending); 0 for at once.</summary>
    public int After { get; init; }
}

/// <summary>A mayor rolled on New game: a name, the background picked and six scores (4d6, lowest dropped), highest first.</summary>
public sealed record MayorRoll
{
    public string Name { get; init; } = "";
    public string Background { get; init; } = "";
    public IReadOnlyList<int> Scores { get; init; } = [];
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

    /// <summary>The six scores rolled on New game, highest first, dealt out in the background's ability order; empty for the standard array.</summary>
    public List<int> Rolled { get; } = [];

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

    /// <summary>What others added to the mayor's roll (a party over the sending stone, the Adventurers' Hall), and who.</summary>
    public int Help { get; set; }
    public string Helpers { get; set; } = "";

    /// <summary>How the people the choice touched took the roll ("Nell and Delia cheer."), for the card.</summary>
    public string Table { get; set; } = "";
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

    /// <summary>The patron the story is about (<see cref="Patrons"/>).</summary>
    public string? Patron { get; set; }
    /// <summary>The minor power (<see cref="Powers"/>) a first-contact scene is about.</summary>
    public string? Minor { get; set; }

    /// <summary>The townsperson who brought a petition, and the neighbourhood it is about.</summary>
    public int? PetitionerId { get; set; }
    public int? QuarterId { get; set; }

    /// <summary>The settlement out in the wilds a story is about (its people, a trade route there).</summary>
    public int? SettlementId { get; set; }

    /// <summary>The fallen hero in the hall of heroes the story is about.</summary>
    public int? FallenId { get; set; }

    /// <summary>The person out in the wilds the story is about (<see cref="Neighbours"/>).</summary>
    public int? ContactId { get; set; }

    /// <summary>A first meeting's earlier steps, held so the whole meeting goes in the Ledger as one line.</summary>
    public string Told { get; set; } = "";

    /// <summary>An enemy agent in town (<see cref="Lurker.Id"/>), a servant on the council (<see cref="Mole.Id"/>) and a foe's grudge (<see cref="Grudge.Foe"/>).</summary>
    public int? LurkerId { get; set; }
    public int? MoleId { get; set; }
    public string? Foe { get; set; }

    /// <summary>A trade guild, and the notables a feud is between.</summary>
    public string? GuildId { get; set; }
    public int? NotableId { get; set; }
    public int? Notable2Id { get; set; }
    /// <summary>The institutions the story is about, in the order its needs name them (a request, or two of them at odds).</summary>
    public List<string> Institutions { get; } = [];
    /// <summary>When the next stage comes, if the story waits on it (a party's next sending).</summary>
    public int? WaitTick { get; set; }
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

    /// <summary>Open, and not waiting on a later stage.</summary>
    public IEnumerable<Story> Waiting(int tick) => All.Where(s => !s.Done && (s.WaitTick ?? 0) <= tick);

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
    /// What this campaign rolled for a variant ("horror": none, choir, hoax...): a pure function of the seed, so
    /// the town's dice never move and a new game may hold something different, or nothing at all.
    /// </summary>
    public static string Variant(Town town, string id)
    {
        var def = Rules(town).Variants.FirstOrDefault(v => v.Id == id);
        if (def == null || def.Options.Count == 0) return "";
        int salt = 17;
        foreach (char ch in id) salt = unchecked(salt * 31 + ch);
        double roll = new Rng(unchecked(town.Seed * 389 + salt)).Next() * def.Options.Sum(o => o.Weight);
        foreach (var o in def.Options)
        {
            roll -= o.Weight;
            if (roll < 0) return o.Id;
        }
        return def.Options[^1].Id;
    }

    /// <summary>
    /// Every effect an outcome can have, in the order they apply; then decree:id (days, or -1 to repeal),
    /// precedent:id (1 sets it, -1 forgets it) and calling:id (1: the story's hero takes it up, -1: it ends).
    /// </summary>
    public static readonly string[] EffectKeys =
        ["gold", "mandate", "dread", "tension", "food", "timber", "loyalty", "wound", "favour", "standing", "seat", "foePower", "grudge", "delay", "festival", "reveal", "inspiration", "people", "leaves", "renown", "heat", "expose", "regard", "retire", "memorial", "esteem", "heroXp", "personal", "dismiss", "edge", "swell", "partyHurt", "rest", "slots", "loot", "partyLoyalty", "turnBack", "fine", "jail", "taint", "infamy", "exile", "goodwill", "goodwill2", "prosperity", "security", "contact", "band", "moves", "stir", "book", "lore", "recover", "minor", "egg", "ranger", "shelter", "settle", "pact", "pactFavour", "weregild", "owed", "curse", "magicItem", "seize", "suspicion", "foeHeat", "guild", "notable", "notable2", "raiseFallen", "bigBad"];

    /// <summary>
    /// Every need a story or option can name; besides these, precedent:id, decree:id, season:id and
    /// calling:id (held; binds its hero), building:id, known:id (a discovery made) and institution:id or
    /// institution:id:choice (open, and its choice made so; binds it), each negated with a leading !, and class:id
    /// (binds a hero of the class in town with no calling yet), faction:id (binds that faction while it has a
    /// chapter) and below:level (binds that undercity level while it is open and not cleared).
    /// </summary>
    public static readonly string[] Needs =
        ["hero", "restless", "party", "menace", "march", "warband", "faction", "seat", "tavern", "temple", "site", "unheard", "power", "hungry", "homeless", "rich", "watch", "poor", "petitioner", "neighbourhood", "settlement", "route", "courted", "veteran", "fallen", "admirer", "aggrieved", "abroad", "stormbound", "homeward", "victorious", "lawbreaker", "famous", "infamous", "trouble", "contact", "band", "forge", "below", "library", "scholars", "remains", "minor", "egg", "eggHeld", "ranger", "column", "sworn", "pactless", "bloodied", "owed", "cursed", "lurker", "mole", "grudge", "guild", "rivals", "refugee", "festival"];

    private static readonly string[] Prefixed = ["precedent:", "decree:", "season:"];

    /// <summary>Whether content may name this need: a plain one, or a precedent, decree or season the content defines.</summary>
    public static bool KnownNeed(Content c, string need)
    {
        if (Needs.Contains(need)) return true;
        string n = need.TrimStart('!');
        if (n.StartsWith("precedent:", StringComparison.Ordinal)) return c.Stories.Precedent(n[10..]) != null;
        if (n.StartsWith(Manners.Need, StringComparison.Ordinal)) return c.Manners.Manner(n[Manners.Need.Length..]) != null;
        if (n.StartsWith("decree:", StringComparison.Ordinal)) return c.Stories.Decree(n[7..]) != null;
        if (n.StartsWith("season:", StringComparison.Ordinal)) return c.Seasons.Seasons.Any(x => x.Id == n[7..]);
        if (n.StartsWith("calling:", StringComparison.Ordinal)) return c.Stories.Calling(n[8..]) != null;
        if (n.StartsWith("institution:", StringComparison.Ordinal))
        {
            var parts = n[12..].Split(':');
            var inst = c.Institutions.Def(parts[0]);
            return inst != null && (parts.Length == 1 || parts.Length == 2 && inst.Choice?.Options.Any(o => o.Id == parts[1]) == true);
        }
        if (need.StartsWith("class:", StringComparison.Ordinal)) return c.Classes.Any(x => x.Id == need[6..]);
        if (need.StartsWith("faction:", StringComparison.Ordinal)) return c.Factions.Factions.Any(x => x.Ref == need[8..]);
        if (n.StartsWith("building:", StringComparison.Ordinal)) return c.Buildings.Any(x => x.Id == n[9..]);
        if (n.StartsWith("known:", StringComparison.Ordinal)) return c.Research.Discovery(n[6..]) != null;
        if (need.StartsWith("below:", StringComparison.Ordinal)) return c.Undercity.Level(need[6..]) != null;
        if (need.StartsWith("zone:", StringComparison.Ordinal)) return Enum.TryParse<Zone>(need[5..], out var z) && z != Zone.None;
        if (need.StartsWith("neighbourhood:", StringComparison.Ordinal)) return c.City.Neighbourhood.Quarters.Names.ContainsKey(need[14..]);
        if (need.StartsWith("patron:", StringComparison.Ordinal)) return c.Patrons.Patron(need[7..]) != null;
        if (need.StartsWith("keeper:", StringComparison.Ordinal)) return c.Taverns.Past(need[7..]) != null;
        if (n.StartsWith("variant:", StringComparison.Ordinal))
        {
            var parts = n[8..].Split(':');
            return parts.Length == 2 && c.Stories.Variants.FirstOrDefault(v => v.Id == parts[0]) is { } vd && vd.Options.Any(o => o.Id == parts[1]);
        }
        return false;
    }

    /// <summary>Whether content may name this effect: a plain one, or a decree or precedent the content defines.</summary>
    public static bool KnownEffect(Content c, string key) =>
        EffectKeys.Contains(key)
        || key.StartsWith("decree:", StringComparison.Ordinal) && c.Stories.Decree(key[7..]) != null
        || key.StartsWith("precedent:", StringComparison.Ordinal) && c.Stories.Precedent(key[10..]) != null
        || key.StartsWith("calling:", StringComparison.Ordinal) && c.Stories.Calling(key[8..]) != null
        || key.StartsWith("project:", StringComparison.Ordinal) && c.Neighbours.Project(key[8..]) != null
        || key.StartsWith("fixture:", StringComparison.Ordinal) && c.Institutions.Fixture(key[8..]) != null
        || key.StartsWith("promise:", StringComparison.Ordinal) && c.Accords.Promise(key[8..]) != null;

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

    /// <summary>
    /// Roll a mayor for New game from its own seed (never the town's rng): a name from the human names and six
    /// scores of 4d6 with the lowest die dropped, highest first. Rerolling is rolling again with another seed.
    /// </summary>
    public static MayorRoll RollMayor(Content c, int seed, string background = "")
    {
        var rng = new Rng(seed);
        var names = c.Races.First(x => x.Id == "human");
        string name = $"{rng.Pick(names.Given)} {rng.Pick(names.Family)}";
        var scores = new List<int>();
        for (int i = 0; i < 6; i++)
        {
            var dice = new[] { rng.Range(1, 7), rng.Range(1, 7), rng.Range(1, 7), rng.Range(1, 7) };
            scores.Add(dice.Sum() - dice.Min());
        }
        var bgs = c.Stories.Backgrounds;
        string bg = bgs.Any(b => b.Ref == background) ? background : bgs.Count > 0 ? bgs[rng.Range(0, bgs.Count)].Ref : "";
        return new MayorRoll { Name = name, Background = bg, Scores = scores.OrderByDescending(x => x).ToList() };
    }

    /// <summary>Make the rolled mayor the town's: name, background (chosen) and scores.</summary>
    public static void Adopt(Town town, MayorRoll roll)
    {
        var m = town.Stories.Mayor;
        if (roll.Name != "") m.Name = roll.Name;
        if (Rules(town).Backgrounds.Any(b => b.Ref == roll.Background))
        {
            m.Background = roll.Background;
            m.Chosen = true;
        }
        m.Rolled.Clear();
        if (roll.Scores.Count == 6) m.Rolled.AddRange(roll.Scores.Select(s => Math.Clamp(s, 3, 18)).OrderByDescending(s => s));
    }

    /// <summary>Scores dealt out in a background's ability order, with its +2 and +1: a level 1 mayor's abilities (New game shows them as rolled).</summary>
    public static Abilities Deal(StoryRules r, string background, IReadOnlyList<int> array)
    {
        var bg = r.Background(background);
        var a = new Abilities();
        for (int i = 0; i < bg.Abilities.Count && i < array.Count; i++) a[bg.Abilities[i]] = array[i];
        if (bg.Abilities.Count > 1)
        {
            a[bg.Abilities[0]] += 2;
            a[bg.Abilities[1]] += 1;
        }
        return a;
    }

    /// <summary>The scores dealt out before the background's bonus: the rolled ones, or the standard array.</summary>
    public static IReadOnlyList<int> Array(Town town) => town.Stories.Mayor.Rolled.Count == 6 ? town.Stories.Mayor.Rolled : Rules(town).Array;

    /// <summary>The mayor's ability scores: the standard array in the background's order, +2 and +1 from it, then each ability advance taken (at most 20).</summary>
    public static Abilities Scores(Town town)
    {
        var m = town.Stories.Mayor;
        var a = Deal(Rules(town), m.Background, Array(town));
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
        if (sk != null) prof += Sheet.Feats(town).Sum(f => f.Skills.GetValueOrDefault(skill)) + Regalia.Skill(town, skill);
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

    /// <summary>Set a precedent the town will remember, unless it already does.</summary>
    internal static void Remember(Town town, string id)
    {
        if (!town.Stories.Precedents.ContainsKey(id)) town.Stories.Precedents[id] = town.Day;
    }

    public static Story? Find(Town town, int id) => town.Stories.All.FirstOrDefault(s => s.Id == id);

    public static StoryDef DefOf(Town town, Story s) => Rules(town).Story(s.Def) ?? throw new KeyNotFoundException($"Unknown story {s.Def}");

    public static StoryStage StageOf(Town town, Story s)
    {
        var d = DefOf(town, s);
        return d.Stages.FirstOrDefault(x => x.Id == s.Stage) ?? d.Stages[0];
    }

    public static StakesDef StakesOf(Town town, Story s) => Rules(town).StakesOf(DefOf(town, s).Stakes);

    /// <summary>Open stories waiting on the mayor now, oldest first (not those waiting on a later sending).</summary>
    public static List<Story> Pending(Town town) => town.Stories.Waiting(town.Tick).OrderBy(s => s.Id).ToList();

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
        if (r.Stories.Count == 0 || town.Day < Math.Max(r.FirstDay, st.NextDay) || st.Open.Count(s => !Sendings.Is(town, s) && DefOf(town, s).Source != Powers.ContactSource) >= r.MaxOpen || town.Outcome != null) return;
        if (!town.Rng.Chance(r.Chance * Storyteller.Of(town).Stories)) return;
        var open = st.Open.Select(s => s.Def).ToHashSet();
        var fits = new List<(StoryDef Def, Story Bound)>();
        foreach (var d in r.Stories)
        {
            // Sendings come from parties on the road (Sendings.Hourly), and word of a lost company from its wipe (Lost), not the town.
            if (Sendings.Is(d) || d.Source == LostSource || d.Source == FoundSource || d.Source == Powers.ContactSource || d.Source == Eggs.Source || d.Source == Poaching.Source || d.Source == Refugees.Source) continue;
            if (open.Contains(d.Id) || town.Day < d.MinDay || town.Population < d.MinPopulation || !Odds.InTier(town, d)) continue;
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

    public static double WeightOf(Town town, StoryDef d) => (d.Weight + d.PerHundred * town.Population / 100.0) * Odds.Factor(town, d);

    /// <summary>
    /// An enemy agent come to town may leave a sign before its night: one of the stories that need a lurker, bound to it,
    /// picked by the town's odds. It comes on its own day, like a first meeting, and holds back none of the town's stories.
    /// </summary>
    public static Story? Sign(Town town, Lurker l)
    {
        var st = town.Stories;
        var open = st.Open.Select(s => s.Def).ToHashSet();
        var fits = new List<(StoryDef Def, Story Bound)>();
        foreach (var d in Rules(town).Stories)
        {
            if (!d.Needs.Contains("lurker") || open.Contains(d.Id) || !Odds.InTier(town, d)) continue;
            if (st.LastDay.Get(d.Id, -9999) + d.RepeatDays > town.Day) continue;
            if (BindFrom(town, d, new Story { LurkerId = l.Id }) is { } bound) fits.Add((d, bound));
        }
        if (fits.Count == 0) return null;
        double roll = town.Rng.Next() * Js.Sum(fits, f => WeightOf(town, f.Def));
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
        int next = st.NextDay;
        var story = Begin(town, pick.Def, pick.Bound);
        st.NextDay = next;
        return story;
    }

    /// <summary>Open a story now, binding what it is about; null when its needs are not met. Tests and scripted shots call it directly.</summary>
    public static Story? Open(Town town, string defId)
    {
        var d = Rules(town).Story(defId);
        if (d == null || Bind(town, d) is not { } bound) return null;
        return Begin(town, d, bound);
    }

    /// <summary>A power met for the first time: its first-contact scene, bound to it (<see cref="Powers.Introduce"/>).</summary>
    public static Story? Contact(Town town, StoryDef d, Story seed)
    {
        if (BindFrom(town, d, seed) is not { } bound) return null;
        // A meeting comes on its own day: it holds back none of the town's own stories.
        int next = town.Stories.NextDay;
        var story = Begin(town, d, bound);
        town.Stories.NextDay = next;
        return story;
    }

    /// <summary>A card about a place: opens with the site bound, so its effects (reveal) land there.</summary>
    public static Story? OpenAt(Town town, string defId, string siteId)
    {
        var d = Rules(town).Story(defId);
        if (d == null || BindFrom(town, d, new Story { SiteId = siteId }) is not { } bound) return null;
        return Begin(town, d, bound);
    }

    /// <summary>The source of cards a find opens (enchantments.json events): never drawn at random.</summary>
    public const string FoundSource = "found";

    /// <summary>A hero brings home something that opens a card of its own (a deck of many things): the card binds them.</summary>
    public static Story? Found(Town town, string defId, Agent hero)
    {
        var d = Rules(town).Story(defId);
        if (d == null || town.Outcome != null || BindFrom(town, d, new Story { HeroId = hero.Id }) is not { } bound) return null;
        return Begin(town, d, bound);
    }

    /// <summary>The source of the card that tells of a company wiped out (<see cref="Remains"/>).</summary>
    public const string LostSource = "lost";

    /// <summary>
    /// A company is wiped out: a card tells the mayor and offers to send another party after their gear. One card per
    /// place at a time; a second company lost there is told on the card already open.
    /// </summary>
    public static Story? Lost(Town town, FallenCompany f, Party party)
    {
        if (town.Outcome != null || town.Stories.Open.Any(o => o.SiteId == f.SiteId && DefOf(town, o).Source == LostSource)) return null;
        var d = Rules(town).Stories.FirstOrDefault(x => x.Source == LostSource);
        if (d == null || BindFrom(town, d, new Story { SiteId = f.SiteId, PartyId = party.Id }) is not { } bound) return null;
        return Begin(town, d, bound);
    }

    /// <summary>A sending opens with the party already bound, and falls due in hours rather than days.</summary>
    internal static Story BeginSending(Town town, StoryDef d, Story s, int dueHours)
    {
        var story = Begin(town, d, s);
        story.DueTick = town.Tick + dueHours;
        return story;
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

    /// <summary>Bind a story on top of what is already bound (a sending's party, hero and site).</summary>
    internal static Story? BindFrom(Town town, StoryDef d, Story seed)
    {
        foreach (var need in d.Needs)
            if (!Meet(town, need, seed, bind: true)) return null;
        return seed;
    }

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
        bool Bind(string patron)
        {
            if (bind) s.Patron ??= patron;
            return true;
        }
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
            case "famous":
            case "infamous":
            {
                var nr = town.Content.Notoriety;
                bool Fits(Agent a) => need == "famous"
                    ? Notoriety.FameTier(town, a.Hero!) >= nr.StoryFame
                    : Notoriety.InfamyTier(town, a.Hero!) >= nr.StoryInfamy;
                if (s.HeroId is int h) return Hero(town, h) is { } a && Fits(a);
                if (!bind) return false;
                var a2 = Pick(town.Heroes.Where(a => !a.IsAway && Fits(a)).OrderBy(a => a.Id).ToList());
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
            case "remains":
                return s.SiteId is string rid && Remains.CanPost(town, rid);
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
                return town.Workers.Count(a => a.Homeless) >= 3;
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
            case "neighbourhood":
            {
                if (s.QuarterId != null) return true;
                if (!bind) return town.City.Quarters.Count > 0;
                var all = town.City.Quarters;
                // A petition comes from the newer neighbourhoods when there are any: the old town has had its say.
                var from = all.Count > 1 ? all.Skip(1).ToList() : all.ToList();
                s.QuarterId = from.Count == 0 ? null : from[town.Rng.Range(0, from.Count)].Id;
                return s.QuarterId != null;
            }
            case "lurker":
            {
                // An enemy agent loitering in town, before its night comes.
                if (s.LurkerId is int lid) return Lurkers.Of(town, lid) is { } bl && town.Tick < bl.StrikeTick;
                if (!bind) return false;
                var l = Pick(town.Lurkers.Where(l => town.Tick < l.StrikeTick).OrderBy(l => l.Id).ToList());
                s.LurkerId = l?.Id;
                return l != null;
            }
            case "mole":
            {
                // A foe's servant on the council, not yet named; binds the seat they hold.
                if (s.MoleId is int mid) return town.Moles.Any(m => m.Id == mid);
                if (!bind) return false;
                var m = Pick(town.Moles.Where(m => town.Seats.Any(x => x.HolderId == m.AgentId)).OrderBy(m => m.Id).ToList());
                if (m == null) return false;
                s.MoleId = m.Id;
                s.SeatId ??= town.Seats.First(x => x.HolderId == m.AgentId).Def.Id;
                return true;
            }
            case "grudge":
            {
                // A foe whose grudge is at least a third of the way to striking.
                double half = town.Content.Crises.Vengeance.StrikeAt / 3;
                if (s.Foe is string bf) return Vengeance.Of(town, bf) != null;
                if (!bind) return false;
                var g = Pick(town.Grudges.Where(g => g.Heat >= half).OrderBy(g => g.Foe, StringComparer.Ordinal).ToList());
                s.Foe = g?.Foe;
                return g != null;
            }
            case "guild":
            {
                if (s.GuildId is string bg) return Guilds.Find(town, bg) != null;
                if (!bind) return false;
                var g = Pick(town.Guilds.OrderBy(g => g.Id, StringComparer.Ordinal).ToList());
                s.GuildId = g?.Id;
                return g != null;
            }
            case "rivals":
            {
                // Two notables of the town at odds: the mayor mediates.
                var live = Notables.Active(town).OrderBy(n => n.Id).ToList();
                if (s.NotableId is int n1 && s.Notable2Id is int n2) return live.Any(n => n.Id == n1) && live.Any(n => n.Id == n2);
                if (!bind || live.Count < 2) return false;
                int i = town.Rng.Range(0, live.Count), j = town.Rng.Range(0, live.Count - 1);
                if (j >= i) j++;
                s.NotableId = live[i].Id;
                s.Notable2Id = live[j].Id;
                return true;
            }
            case "refugee":
            {
                // Someone who came in fleeing a blow, camped or settled; bound as the petitioner.
                if (s.PetitionerId is int rp) return town.Agent(rp) is { Departed: false, Refugee: true };
                if (!bind) return false;
                var a = Pick(town.Agents.Where(a => !a.Departed && a.Refugee && a.Hero == null && a.Age >= 16).OrderBy(a => a.Id).ToList());
                s.PetitionerId = a?.Id;
                return a != null;
            }
            case "festival":
                return town.FestivalDays > 0;
            case "minor":
                // Only ever bound by the first meeting that opens the scene (Powers.Introduce).
                return s.Minor is string minorId && Powers.Find(town, minorId) != null;
            case "settlement":
            case "route":
            case "courted":
            {
                bool Fits(Settlement x) => !x.Razed && Neighbours.Known(town, x) && need switch
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
            case "trouble":
            case "band":
            case "forge":
            {
                bool Fits(Settlement x) => !x.Razed && Neighbours.Known(town, x) && x.Standing > -20 && need switch
                {
                    "trouble" => x.Trouble != null,
                    "band" => x.Band is { Home: true, Members: > 0 },
                    _ => !x.Projects.Contains("forge") && x.Works == null && Neighbours.ProjectsFor(town, x).Any(p => p.Id == "forge"),
                };
                if (s.SettlementId is int sid) return Fits(town.Settlements[sid]);
                if (!bind) return false;
                var found = Pick(town.Settlements.Where(Fits).OrderBy(x => x.Id).ToList());
                s.SettlementId = found?.Id;
                return found != null;
            }
            case "contact":
            {
                bool Fits(Contact c) => !c.Gone && c.Memories.Count > 0 && c.Regard >= 40 && town.Settlements[c.SettlementId] is { Razed: false } cs && Neighbours.Known(town, cs);
                if (s.ContactId is int cid) return Neighbours.ContactById(town, cid) is { } bound && Fits(bound);
                if (!bind) return false;
                var c = Pick(town.Neighbours.Cast.Where(Fits).OrderBy(x => x.Id).ToList());
                s.ContactId = c?.Id;
                if (c != null) s.SettlementId ??= c.SettlementId;
                return c != null;
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
            case "lawbreaker":
            {
                if (s.HeroId is int lh) return Hero(town, lh) is { } la && !la.IsAway && la.Hero!.JailedUntil == null;
                if (!bind) return false;
                var lb = Pick(town.Heroes.Where(a => Regard.Lawless(town, a)).OrderBy(a => a.Id).ToList());
                s.HeroId = lb?.Id;
                return lb != null;
            }
            case "admirer":
            case "aggrieved":
            {
                bool Fits(Agent a) => need == "admirer" ? Regard.Admirer(town, a) : Regard.Aggrieved(town, a);
                if (s.HeroId is int h) return Hero(town, h) is { } a && (need == "admirer" ? !a.IsAway && a.Hero!.Personal == "" : !a.IsAway);
                if (!bind) return false;
                var a2 = Pick(town.Heroes.Where(Fits).OrderBy(a => a.Id).ToList());
                s.HeroId = a2?.Id;
                return a2 != null;
            }
            case "abroad":
            case "stormbound":
            case "homeward":
            case "victorious":
            {
                // Bound by the sending: where the party is now.
                if (s.PartyId is not int sp || town.Parties.FirstOrDefault(p => p.Id == sp) is not { } party) return false;
                if (need == "stormbound")
                    return Sendings.Abroad(town, party) && Roads.Where(town, party, town.Tick) is { } at && Weather.At(town, at.X, at.Y, town.Tick).Step >= Weather.Storm;
                return need == "abroad" ? Sendings.Abroad(town, party)
                    : Sendings.Homeward(town, party) && (need == "homeward" || party.Result!.Outcome == ExpeditionOutcome.Cleared);
            }
            case "sworn":
            {
                // A hero in town sworn to a patron; binds the hero and their patron.
                if (s.HeroId is int h) return Hero(town, h) is { } a && a.Hero!.Patron != "" && (s.Patron == null || a.Hero.Patron == s.Patron) && Bind(a.Hero.Patron);
                if (!bind) return false;
                var a2 = Pick(town.Heroes.Where(a => !a.IsAway && a.Hero!.Patron != "" && (s.Patron == null || a.Hero.Patron == s.Patron)).OrderBy(a => a.Id).ToList());
                s.HeroId = a2?.Id;
                return a2 != null && Bind(a2.Hero!.Patron);
            }
            case "pactless":
            {
                // A hero in town no patron holds, and a patron to court them (by rng, in content order).
                bool Free(Agent a) => a.Hero!.Patron == "" && !a.Hero.PactRefused && a.Hero.Cls.Id != "warlock";
                var patrons = town.Content.Patrons.Patrons;
                if (patrons.Count == 0) return false;
                if (s.HeroId is int h && !(Hero(town, h) is { } a && Free(a))) return false;
                if (s.HeroId == null)
                {
                    if (!bind) return town.Heroes.Any(a => !a.IsAway && Free(a));
                    var a2 = Pick(town.Heroes.Where(a => !a.IsAway && Free(a)).OrderBy(a => a.Id).ToList());
                    if (a2 == null) return false;
                    s.HeroId = a2.Id;
                }
                if (s.Patron == null && bind) s.Patron = Pick(patrons.ToList())!.Id;
                return true;
            }
            case "bloodied":
            case "owed":
            case "cursed":
            {
                // A hero in town who killed a townsperson in a brawl and has not settled it; or one the town owes favours.
                bool Fits(Agent a) => !a.IsAway && need switch
                {
                    "bloodied" => Taverns.Owing(town, a) != null,
                    "cursed" => a.Hero!.Curse != "",
                    _ => a.Hero!.Owed > 0,
                };
                if (s.HeroId is int h) return Hero(town, h) is { } a && Fits(a);
                if (!bind) return false;
                var a2 = Pick(town.Heroes.Where(Fits).OrderBy(a => a.Id).ToList());
                s.HeroId = a2?.Id;
                return a2 != null;
            }
            case "library":
                return town.Research.Books.Count > 0;
            case "egg":
                return Eggs.Waiting(town);
            case "eggHeld":
                return Eggs.Held(town);
            case "ranger":
                return Poaching.Waiting(town);
            case "column":
                return Refugees.Waiting(town);
            case "scholars":
                return town.Research.Study != null;
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
                if (need.StartsWith("neighbourhood:", StringComparison.Ordinal))
                {
                    // A neighbourhood that is now what the need names (market, workshops, poor...); binds it as the place.
                    string kind = need[14..];
                    bool Is(Quarter q) => Neighbourhood.Character(town, Neighbourhood.Buildings(town, q)) == kind;
                    if (s.QuarterId is int bound) return Neighbourhood.Find(town, bound) is { } bq && Is(bq);
                    if (!bind) return town.City.Quarters.Any(Is);
                    var fit = town.City.Quarters.Where(Is).ToList();
                    s.QuarterId = fit.Count == 0 ? null : fit[town.Rng.Range(0, fit.Count)].Id;
                    return s.QuarterId != null;
                }
                if (need.StartsWith("zone:", StringComparison.Ordinal))
                {
                    // A finished building on land painted for the zone; binds it as the place.
                    var zone = Enum.Parse<Zone>(need[5..]);
                    bool On(Building b) => b.Complete && !b.Closed && town.Lots[b.LotId].Zone == zone;
                    if (s.BuildingId is int zb) return On(town.Building(zb));
                    if (!bind) return town.Buildings.Any(On);
                    var zp = Pick(town.Buildings.Where(On).OrderBy(b => b.Id).ToList());
                    s.BuildingId = zp?.Id;
                    return zp != null;
                }
                if (need.StartsWith("below:", StringComparison.Ordinal))
                {
                    // One level of the undercity, open and not cleared.
                    var lv = Undercity.Of(town, need[6..]);
                    if (lv == null || Undercity.Site(town, lv).Cleared) return false;
                    string site = Undercity.Def(town, lv).Site.Id;
                    if (s.SiteId is string bs) return bs == site;
                    if (bind) s.SiteId = site;
                    return true;
                }
                if (need.StartsWith("keeper:", StringComparison.Ordinal))
                {
                    // A tavern whose keeper has this past; binds the tavern.
                    string past = need[7..];
                    bool Kept(Building b) => b.Complete && !b.Closed && Taverns.Past(town, b)?.Id == past;
                    if (s.BuildingId is int kb) return Kept(town.Building(kb));
                    if (!bind) return town.Taverns.Any(Kept);
                    var tb = Pick(town.Taverns.Where(Kept).OrderBy(b => b.Id).ToList());
                    s.BuildingId = tb?.Id;
                    return tb != null;
                }
                if (need.StartsWith("patron:", StringComparison.Ordinal))
                {
                    // A patron whose sworn heroes have pleased it enough to press its own design; binds it and its best-favoured hero.
                    string pid = need[7..];
                    if (s.Patron is string bp && bp != pid) return false;
                    if (Patrons.Pull(town, pid) < town.Content.Patrons.DesignAt) return false;
                    if (bind)
                    {
                        s.Patron = pid;
                        s.HeroId ??= Patrons.Sworn(town, pid).Where(a => !a.IsAway).OrderByDescending(a => a.Hero!.Favour).ThenBy(a => a.Id).FirstOrDefault()?.Id;
                    }
                    return true;
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
                if (n.StartsWith("precedent:", StringComparison.Ordinal))
                    holds = town.Stories.Precedents.TryGetValue(n[10..], out int set) && (not || town.Day - set >= (town.Content.Stories.Precedent(n[10..])?.After ?? 0));
                else if (n.StartsWith("decree:", StringComparison.Ordinal)) holds = Decrees.InForce(town, n[7..]);
                else if (n.StartsWith("season:", StringComparison.Ordinal)) holds = Seasons.Current(town).Id == n[7..];
                else if (n.StartsWith("institution:", StringComparison.Ordinal))
                {
                    // An institution open (with its choice made so, if named) binds it, after any bound already.
                    var parts = n[12..].Split(':');
                    holds = Institutions.Find(town, parts[0]) is { Shut: false } inst && (parts.Length == 1 || inst.Choice == parts[1]);
                    if (holds && !not && bind && !s.Institutions.Contains(parts[0])) s.Institutions.Add(parts[0]);
                }
                else if (n.StartsWith("building:", StringComparison.Ordinal)) holds = town.Buildings.Any(b => b.Def.Id == n[9..] && b.Complete && !b.Closed);
                else if (n.StartsWith("known:", StringComparison.Ordinal)) holds = Research.IsKnown(town, n[6..]);
                else if (n.StartsWith("variant:", StringComparison.Ordinal))
                {
                    var parts = n[8..].Split(':');
                    holds = Variant(town, parts[0]) == parts[1];
                }
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
        if (Hidden(town, s, o)) return "not for them";
        if (o.Cost > 0 && town.Treasury < o.Cost) return $"needs {Js.Str(o.Cost)} gold";
        foreach (var need in o.Needs.Where(n => !n.StartsWith(Manners.Need, StringComparison.Ordinal)))
            if (!Meet(town, need, s, bind: false) && !MeetFresh(town, need)) return need switch
            {
                "temple" => "needs a temple",
                "watch" => "needs the watch",
                "tavern" => "needs a tavern",
                "rich" => "needs a full treasury",
                "hero" => "needs a hero in town",
                "remains" => "the treasury can't fund the bounty",
                _ when need.StartsWith("building:", StringComparison.Ordinal) => $"needs {Article(town.Content.Building(need[9..]).Name)}",
                _ => $"needs {need}",
            };
        return null;
    }

    /// <summary>An option only a faction with a manner is offered (a need "manner:id"): the card leaves it out for the rest.</summary>
    public static bool Hidden(Town town, Story s, StoryOption o) =>
        o.Needs.Any(n => n.StartsWith(Manners.Need, StringComparison.Ordinal) && !Manners.Has(town, s, n[Manners.Need.Length..]));

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
        return Sheet.HelpText(town, o.Skill);
    }

    /// <summary>The chance the check succeeds, with advantage if it has it and an inspired reroll if one is asked for and in hand.</summary>
    /// <summary>An option's DC in the town's act: harder each act (stories.json actDc), so a seasoned mayor still sweats.</summary>
    public static int DcOf(Town town, StoryOption o)
    {
        var a = Rules(town).ActDc;
        return o.Dc + (a.Length == 0 ? 0 : a[Math.Clamp(town.Saga.Act, 0, a.Length - 1)]);
    }

    /// <summary>An option's DC on this story: a first meeting is harder or easier by the manners of whoever it is with.</summary>
    public static int DcOf(Town town, Story s, StoryOption o)
    {
        int dc = DcOf(town, o) + (DefOf(town, s).Source == Powers.ContactSource ? Manners.Lean(town, Manners.KeyOf(s), o.Skill) : 0);
        int floor = StakesOf(town, s).Floor;
        if (floor <= 0 || !o.Rolls) return dc;
        var (mod, prof, _) = Bonus(town, o.Skill, o.Ability);
        return Math.Max(dc, mod + prof + Sendings.HelpTotal(town, s, o) + floor);
    }

    public static double Chance(Town town, Story s, StoryOption o, bool inspired = false) =>
        !o.Rolls ? 1 : Sheet.Chance(town, o.Skill, o.Ability, DcOf(town, s, o), Advantage(town, s, o) != "", inspired, Sendings.HelpTotal(town, s, o));

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
            beat = new StoryBeat { Tick = town.Tick, Skill = o.Skill != "" ? o.Skill : o.Ability, Dc = DcOf(town, s, o), Result = StoryResult.Certain };
            outcome = o.Success;
        }
        else
        {
            var help = Sendings.Helpers(town, s, o);
            beat = Sheet.Roll(town, o.Skill, o.Ability, DcOf(town, s, o), Advantage(town, s, o), inspired, o.Partial != null, help.Sum(h => h.Bonus));
            beat.Helpers = string.Join(", ", help.Select(h => $"{h.Name} {(h.Bonus >= 0 ? "+" : "−")}{Math.Abs(h.Bonus)}"));
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
        beat.Table = Voices.Table(town, o.Tags, beat.Result);
        string chose = Chose(o.Label, beat.Option);
        Conclude(town, s, beat, outcome, chose);
        // The heroes hear what the mayor chose, and judge it by their tempers.
        if (o.Tags.Count > 0) Regard.Judge(town, o.Tags, chose, Fill(town, s, DefOf(town, s).Title));
        return beat;
    }

    /// <summary>A story nobody stepped into runs its course.</summary>
    private static void Lapse(Town town, Story s)
    {
        var outcome = StageOf(town, s).Ignored ?? DefOf(town, s).Ignored
            ?? new StoryOutcome { Text = Sendings.Is(town, s) ? "No answer comes, and they do as they think best." : "In the end it comes to nothing." };
        var beat = new StoryBeat { Tick = town.Tick, Stage = s.Stage, Option = "Let it run its course", Result = StoryResult.Ignored };
        // A story left alone ends there, whatever its outcome would lead to.
        Conclude(town, s, beat, outcome with { Next = "" }, "");
    }

    private static void Conclude(Town town, Story s, StoryBeat beat, StoryOutcome outcome, string chose)
    {
        var d = DefOf(town, s);
        string effects = Apply(town, s, outcome.Effects);
        beat.Text = Fill(town, s, outcome.Text);
        s.Beats.Add(beat);
        string roll = beat.Result is StoryResult.Certain or StoryResult.Ignored ? "" : $" ({Rules(town).Skill(beat.Skill)?.Name ?? Cap(beat.Skill)} {beat.Total} against DC {beat.Dc}{(beat.Helpers != "" ? $", with {beat.Helpers}" : "")}: {Word(beat.Result)})";
        bool goesOn = outcome.Next != "" && d.Stages.Any(x => x.Id == outcome.Next);
        string said = $"{(beat.Result == StoryResult.Ignored ? (s.Told != "" ? "The rest is left to run its course" : "left to run its course") : $"{(s.Told != "" ? "Then the mayor" : $"Mayor {town.Stories.Mayor.Name}")} {chose}")}{roll}. {beat.Text}{effects}";
        // A first meeting is one exchange: its steps are told together when it ends, so a later step never reads as a second answer.
        if (goesOn && d.Source == Powers.ContactSource) s.Told = $"{s.Told}{(s.Told != "" ? " " : "")}{said}";
        else
        {
            town.Log(LedgerCategory.Story, d.Stakes == "high" && !goesOn ? Importance.Major : Importance.Notable,
                $"{Fill(town, s, d.Title)}: {s.Told}{(s.Told != "" ? " " : "")}{said}", Agents(s));
            s.Told = "";
        }
        if (goesOn)
        {
            s.Stage = outcome.Next;
            if (Sendings.Is(d))
            {
                // The party sends again when it can; the mayor has the usual hours to answer then.
                int after = outcome.After;
                // It always comes before they reach the site (or home) again.
                if (Sendings.PartyOf(town, s) is { } p) after = Math.Min(after, Math.Max(0, (p.Resolved ? p.ReturnTick : p.ArriveTick) - town.Tick - 2));
                s.WaitTick = after > 0 ? town.Tick + after : null;
                s.DueTick = town.Tick + after + town.Content.Sendings.DueHours;
                if (after > 0 && Sendings.PartyOf(town, s) is { } sp)
                {
                    sp.Sendings++;
                    Knowledge.Heard(town, sp);
                }
            }
            else s.DueTick = Math.Max(s.DueTick, town.Tick + 2 * SimTime.HoursPerDay);
            return;
        }
        s.Done = true;
        if (s.Institutions.Count > 0 && Institutions.Find(town, s.Institutions[0]) is { } asked) Institutions.Heard(town, asked, beat.Result == StoryResult.Ignored);
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
    internal static string Apply(Town town, Story s, Dictionary<string, double> effects)
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
                case "grudge":
                    // The foe will remember this: its grudge heats (crises.json vengeance.heat "envoy"), scaled by the value.
                    if (v > 0 && s.MenaceId is int gm && town.Saga.Menaces[gm - 1] is { Alive: true } gmm)
                    {
                        Menaces.Remember(gmm, town.Content.Crises.Vengeance.Deeds.GetValueOrDefault("envoy", ""));
                        Vengeance.Wrong(town, gmm, "envoy");
                    }
                    break;
                case "delay":
                    // Hours added to (or taken from) the foe's march.
                    if (s.MenaceId is int dm && town.Saga.Menaces[dm - 1] is { Alive: true, ArrivalTick: int at }) town.Saga.Menaces[dm - 1].ArrivalTick = Math.Max(town.Tick + 1, at + (int)v);
                    else if (s.Warband && town.Enemy.ArrivalTick is int wat) town.Enemy.ArrivalTick = Math.Max(town.Tick + 1, wat + (int)v);
                    break;
                case "festival":
                    town.FestivalDays += (int)v;
                    break;
                case "minor":
                    if (s.Minor is string minor && Powers.Find(town, minor) is { } mp)
                    {
                        Powers.Sway(town, mp, v);
                        if (v != 0) said.Add($"{Saga.Sentences(Powers.Name(town, Powers.Def(town, minor)))}'s standing {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
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
                case "book":
                    if (v > 0) said.Add($"{Research.Add(town, (int)v, "from a story").Name} for the library");
                    break;
                case "lore":
                    if (town.Research.Study is string studyId)
                    {
                        town.Research.Progress[studyId] = Math.Max(0, town.Research.Progress.Get(studyId, 0) + v);
                        said.Add($"{(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))} lore to {Research.Def(town, studyId).Name.ToLowerInvariant()}");
                    }
                    break;
                case "egg":
                    said.Add(Eggs.Decide(town, (int)v));
                    break;
                case "ranger":
                    said.Add(Poaching.Decide(town, (int)v));
                    break;
                case "shelter":
                    said.Add(Refugees.Decide(town, (int)v));
                    break;
                case "recover":
                    if (v > 0 && s.SiteId is string lostAt && Remains.Post(town, lostAt) is { } rb)
                        said.Add($"a bounty of {Js.Str(rb.Reward)} gold to bring the fallen home");
                    break;
                case "settle":
                    // Whatever was at the site is gone without a fight: moved on, taken in or driven off.
                    if (v > 0 && s.SiteId is string settleSite && Quests.Site(town, settleSite) is { Cleared: false } calm && Menaces.At(town, settleSite) == null)
                        Neighbours.Clear(town, calm);
                    break;
                case "memorial":
                    if (v > 0 && s.FallenId is int mf && Legacies.Of(town, mf) is { Memorial: false } ml)
                    {
                        Legacies.Honor(town, ml);
                        said.Add($"a memorial to {ml.Name}");
                    }
                    break;
                case "goodwill":
                case "goodwill2":
                {
                    int i = key == "goodwill" ? 0 : 1;
                    if (i < s.Institutions.Count && Institutions.Find(town, s.Institutions[i]) is { } inst)
                    {
                        Institutions.Esteem(town, inst, v);
                        said.Add($"{Institutions.KeeperGiven(town, inst)}'s regard {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                }
                case "renown":
                case "infamy":
                    // The bound hero's name, and their company's with it.
                    if (s.HeroId is int nh && Hero(town, nh) is { } na)
                    {
                        var ns = na.Hero!;
                        if (key == "renown")
                        {
                            if (v > 0) Notoriety.Deed(town, na, "story", (int)v);
                            else ns.Renown = Math.Max(0, ns.Renown + (int)v);
                            if (ns.PartyName != null) Notoriety.CompanyDeed(town, ns.PartyName, (int)Math.Max(0, v / 2));
                        }
                        else if (v > 0) Notoriety.Deed(town, na, "brawl", 0, (int)v);
                        else ns.Infamy = Math.Max(0, ns.Infamy + (int)v);
                        said.Add($"{na.GivenName}'s {(key == "renown" ? "renown" : "infamy")} {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "exile":
                    // The bound hero is handed over or sent away.
                    if (s.HeroId is int gone && Hero(town, gone) is { } exiled)
                    {
                        Loyalty.Strip(exiled);
                        Population.Depart(town, exiled);
                        said.Add($"{exiled.GivenName} leaves {town.Name}");
                    }
                    break;
                case "heat":
                    if (s.FactionId is string hf && Factions.Find(town, hf) is { } hfs) hfs.Heat = Math.Clamp(hfs.Heat + v, 0, 100);
                    break;
                case "prosperity":
                case "security":
                    if (s.SettlementId is int ps && town.Settlements[ps] is { Razed: false } pst)
                    {
                        if (key == "prosperity") pst.Prosperity = Math.Clamp(pst.Prosperity + v, 0, 100);
                        else pst.Security = Math.Clamp(pst.Security + v, 0, 100);
                        said.Add($"{pst.Name}'s {key} {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "contact":
                    if (s.ContactId is int rc && Neighbours.ContactById(town, rc) is { Gone: false } rcc)
                    {
                        rcc.Regard = Math.Clamp(rcc.Regard + v, 0, 100);
                        said.Add($"{rcc.Name}'s regard {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "band":
                    if (s.SettlementId is int bs && town.Settlements[bs].Band is { Members: > 0 } band)
                    {
                        band.Members = Math.Max(1, band.Members + (int)v);
                        said.Add($"{band.Name} {(v > 0 ? "+" : "−")}{Math.Abs((int)v)}");
                    }
                    break;
                case "moves":
                    // The person the story is about leaves the wilds to live in the town.
                    if (v > 0 && s.ContactId is int mc && Neighbours.ContactById(town, mc) is { Gone: false } mcc)
                    {
                        mcc.Gone = true;
                        said.Add($"{mcc.Name} moves to {town.Name}");
                    }
                    break;
                case "esteem":
                    if (s.HeroId is int eh && Hero(town, eh) is { } ea)
                    {
                        Regard.Esteem(ea, v);
                        said.Add($"{ea.GivenName} thinks {(v > 0 ? "better" : "worse")} of the mayor");
                    }
                    break;
                case "raiseFallen":
                {
                    // A wish: the heroes the town lost most lately breathe again, as many as it names.
                    var back = new List<string>();
                    foreach (int id in town.Chronicles.AsEnumerable().Reverse().SelectMany(c => c.Dead).Distinct())
                    {
                        if (back.Count >= (int)v) break;
                        var d = town.Agent(id);
                        if (!d.Dead || d.Hero == null || d.Hero.DefectedTo != null) continue;
                        d.Dead = false;
                        d.Departed = false;
                        d.Hero.Hp = Math.Max(1, d.Hero.MaxHp / 2);
                        d.Hero.PartyName = null;
                        d.Hero.AwayUntil = null;
                        back.Add(d.GivenName);
                    }
                    if (back.Count > 0) said.Add($"{Expedition.ListNames(back)} {(back.Count == 1 ? "breathes" : "breathe")} again");
                    break;
                }
                case "bigBad":
                    // A fraction off the greatest living menace (the big bad while it lives).
                    if ((Saga.BigBad(town) is { Alive: true } bb ? bb : town.Saga.Menaces.Where(x => x.Alive).OrderByDescending(x => x.Power).ThenBy(x => x.Id).FirstOrDefault()) is { } foe)
                    {
                        foe.Power = Math.Max(1, foe.Power * (1 + v));
                        said.Add($"{foe.Leader} is struck down to {Js.Str(Math.Round((1 + v) * 100))}% of its strength");
                    }
                    break;
                case "heroXp":
                    // A share of what the hero needs for their next level.
                    if (s.HeroId is int xh && Hero(town, xh) is { } xa)
                    {
                        int next = Quests.XpToNext(xa.Hero!.Level);
                        if (next > 0) Quests.GainXp(town, xa, (int)(next * v));
                    }
                    break;
                case "personal":
                    if (s.HeroId is int ph && Hero(town, ph) is { } pa2) pa2.Hero!.Personal = v > 0 ? "done" : "failed";
                    break;
                case "dismiss":
                    if (v > 0 && s.HeroId is int dh && Hero(town, dh) is { } da)
                    {
                        Loyalty.Dismiss(town, da);
                        said.Add($"{da.GivenName} leaves {town.Name}");
                    }
                    break;
                case "edge":
                case "swell":
                case "partyHurt":
                case "rest":
                case "slots":
                case "loot":
                case "partyLoyalty":
                case "turnBack":
                {
                    string said1 = Sendings.Apply(town, s, key, v);
                    if (said1 != "") said.Add(said1);
                    break;
                }
                case "fine":
                    if (s.HeroId is int fh && Hero(town, fh) is { } fa && v > 0)
                    {
                        double paid = Math.Min(v, Math.Floor(Math.Max(0, fa.Gold)));
                        fa.Gold -= paid;
                        town.Book(TreasuryAccount.Fines, paid);
                        said.Add($"{fa.GivenName} pays a fine of {Js.Str(paid)} gold");
                    }
                    break;
                case "taint":
                    if (s.HeroId is int tnh && Hero(town, tnh) is { } tna && v > 0)
                    {
                        tna.Hero!.Taint += (int)v;
                        said.Add($"{tna.GivenName} carries a dark gift");
                    }
                    break;
                case "pact":
                    // 1: the story's hero swears to the story's patron; -1: they break the pact they hold.
                    if (s.HeroId is int pah && Hero(town, pah) is { } paa)
                    {
                        if (v > 0 && s.Patron is string sp && paa.Hero!.Patron != sp)
                        {
                            Patrons.Swear(town, paa, sp);
                            said.Add($"{paa.GivenName} is sworn to {town.Content.Patrons.Patron(sp)!.Name}");
                        }
                        else if (v < 0 && paa.Hero!.Patron != "") Patrons.Break(town, paa);
                    }
                    break;
                case "weregild":
                    // 1: the kin of the townsperson the story's hero killed are paid (more than 1: that much out of the hero's own purse); -1: they swear a feud.
                    if (s.HeroId is int wgh && Hero(town, wgh) is { } wga && Taverns.Owing(town, wga) is { } debt)
                    {
                        if (v > 1)
                        {
                            double paid = Math.Min(v, Math.Floor(Math.Max(0, wga.Gold)));
                            wga.Gold -= paid;
                            if (debt.KinId is int ki && town.Agent(ki) is { Departed: false } kin) kin.Gold += paid;
                            said.Add($"{wga.GivenName} pays {Js.Str(paid)} gold");
                        }
                        Taverns.Settle(town, wga, v > 0);
                        said.Add(v > 0 ? "the kin are paid" : "the kin swear a feud");
                    }
                    break;
                case "magicItem":
                    // The story's hero gains a piece of this tier from the hoard pool, worn if it suits them.
                    if (s.HeroId is int mh && Hero(town, mh) is { } ma)
                    {
                        var pool = town.Content.LootItems.Where(i => i.Tier == (int)v && Gear.Slots.Contains(Gear.SlotOf(i)) && !Enchant.Rules(town.Content).Events.ContainsKey(i.Id)).OrderBy(i => i.Id, StringComparer.Ordinal).ToList();
                        if (pool.Count > 0)
                        {
                            var won = pool[town.Rng.Range(0, pool.Count)];
                            Spoils.Found(town, won, ma.GivenName, false, ma.Id, null, [ma]);
                            Gear.Claim(town, ma, won);
                            said.Add($"{ma.GivenName} gains the {won.Name}");
                        }
                    }
                    break;
                case "curse":
                    // 1: the curse on the story's hero's weapon is lifted; -1: the blade is broken with it.
                    if (s.HeroId is int ch && Hero(town, ch) is { } ca && ca.Hero!.Curse != "")
                    {
                        Curses.Lift(town, ca, v < 0);
                        said.Add(v < 0 ? "the cursed blade is broken" : "the curse is lifted");
                    }
                    break;
                case "owed":
                    // The story's hero is owed this many favours fewer (all of them when it is 0 or less).
                    if (s.HeroId is int oh && Hero(town, oh) is { } oa)
                        oa.Hero!.Owed = v > 0 ? Math.Max(0, oa.Hero.Owed - (int)v) : 0;
                    break;
                case "pactFavour":
                    // Every hero sworn to the story's patron (or the story's hero's) gains or loses favour with it.
                    if ((s.Patron ?? (s.HeroId is int pfh && Hero(town, pfh) is { } pfa ? pfa.Hero!.Patron : null)) is { Length: > 0 } fp)
                    {
                        foreach (var sw in Patrons.Sworn(town, fp)) Patrons.Shift(town, sw, (int)v);
                        said.Add($"favour with {town.Content.Patrons.Patron(fp)?.Name ?? fp} {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "jail":
                    if (s.HeroId is int jh && Hero(town, jh) is { } ja && v > 0)
                    {
                        ja.Hero!.JailedUntil = town.Tick + (int)v * SimTime.HoursPerDay;
                        said.Add($"{ja.GivenName} spends {(int)v} days in the cells");
                    }
                    break;
                case "seize":
                    // The watch takes the agent before its night comes.
                    if (v > 0 && s.LurkerId is int seizeId && Lurkers.Seize(town, seizeId)) said.Add("the agent is taken");
                    break;
                case "suspicion":
                    if (s.MoleId is int sm && town.Moles.FirstOrDefault(m => m.Id == sm) is { } smm) smm.Suspicion = Math.Max(0, smm.Suspicion + v);
                    break;
                case "foeHeat":
                    if (s.Foe is string heatFoe && Vengeance.Of(town, heatFoe) is { } fhg)
                    {
                        fhg.Heat = Math.Max(0, fhg.Heat + v);
                        said.Add($"{fhg.Name}'s grudge {(v > 0 ? "grows" : "cools")}");
                    }
                    break;
                case "guild":
                    if (s.GuildId is string gs && Guilds.Find(town, gs) is { } gsg)
                    {
                        gsg.Standing = Math.Clamp(gsg.Standing + v, 0, 100);
                        said.Add($"the {Guilds.Name(town, gsg).ToLowerInvariant()} {(v > 0 ? "+" : "−")}{Js.Str(Math.Abs(v))}");
                    }
                    break;
                case "notable":
                case "notable2":
                    if ((key == "notable" ? s.NotableId : s.Notable2Id) is int nid && town.Notables.FirstOrDefault(n => n.Id == nid) is { Gone: false } nn)
                    {
                        Notables.Sway(nn, v);
                        said.Add($"{nn.Name} {(v > 0 ? "warms" : "cools")}");
                    }
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
            else if (key.StartsWith("project:", StringComparison.Ordinal))
            {
                if (v > 0 && s.SettlementId is int gs && Neighbours.Give(town, town.Settlements[gs], key[8..]) is { Length: > 0 } built) said.Add(built);
            }
            else if (key.StartsWith("promise:", StringComparison.Ordinal))
            {
                if (v > 0 && s.SettlementId is int ps && Accords.Give(town, town.Settlements[ps], key[8..]) is { Length: > 0 } word) said.Add(word);
            }
            else if (key.StartsWith("fixture:", StringComparison.Ordinal))
            {
                if (v > 0 && Institutions.Fix(town, key[8..]) is { Length: > 0 } fixture) said.Add(fixture);
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
    /// <summary>The stand-in answers a story through to its end, or as far as it can now.</summary>
    internal static void Answer(Town town, Story s) => StandIn(town, s);

    private static void StandIn(Town town, Story s)
    {
        for (int guard = 0; guard < 8 && !s.Done && (s.WaitTick ?? 0) <= town.Tick; guard++)
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
                double score = c - o.Cost / 200 + o.Prefer + Regard.Sway(town, o.Tags);
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
        Put("{minor}", () => s.Minor is string mn ? Powers.Name(town, Powers.Def(town, mn)) : "a power");
        Put("{minorLeader}", () => s.Minor is string ml ? Powers.Def(town, ml).Leader : "its leader");
        Put("{minorTitle}", () => s.Minor is string mt ? Powers.Def(town, mt).Title : "its leader");
        Put("{god}", () => Faith.Prose(Faith.TempleGod(town) ?? Faith.CharterGod(town)));
        Put("{petitioner}", () => s.PetitionerId is int p ? town.Agent(p).Name : "a townsman");
        Put("{petitionerName}", () => s.PetitionerId is int p ? town.Agent(p).GivenName : "the petitioner");
        Put("{trade}", () => s.PetitionerId is int p ? town.Agent(p).Profession.ToLowerInvariant() : "townsman");
        Put("{neighbourhood}", () => s.QuarterId is int q && Neighbourhood.Find(town, q) is { } n ? n.Name : "the old town");
        Put("{settlement}", () => s.SettlementId is int st ? town.Settlements[st].Name : "a far town");
        Put("{people}", () => s.SettlementId is int sp ? town.Settlements[sp].People : "the folk of a far town");
        Put("{leader}", () => s.SettlementId is int sl ? town.Settlements[sl].Leader : "their leader");
        Put("{foeLeader}", () => s.MenaceId is int fl ? town.Saga.Menaces[fl - 1].Leader : "their master");
        Put("{motive}", () => Motive(town, s));
        Put("{band}", () => s.SettlementId is int sb ? town.Settlements[sb].Band?.Name ?? "their band" : "their band");
        Put("{trouble}", () => s.SettlementId is int stt && Neighbours.TroubleOf(town, town.Settlements[stt]) is { } td ? td.Name : "their trouble");
        Put("{contact}", () => s.ContactId is int ci && Neighbours.ContactById(town, ci) is { } cc ? cc.Name : "an old friend");
        Put("{memory}", () => s.ContactId is int cm && Neighbours.ContactById(town, cm) is { Memories.Count: > 0 } cmm ? cmm.Memories[^1] : "old kindnesses");
        Put("{refugees}", () => Refugees.ColumnText(town));
        Put("{season}", () => Seasons.Current(town).Name.ToLowerInvariant());
        Put("{fallen}", () => s.FallenId is int fh ? town.Agent(fh).Name : "a fallen hero");
        Put("{fallenName}", () => s.FallenId is int fh ? town.Agent(fh).GivenName : "the fallen");
        Put("{fallenClass}", () => s.FallenId is int fh ? town.Agent(fh).Hero?.Cls.Name.ToLowerInvariant() ?? "hero" : "hero");
        Put("{institution}", () => InstitutionName(town, s, 0));
        Put("{institution2}", () => InstitutionName(town, s, 1));
        Put("{keeper}", () => Bound(town, s, 0) is { } i ? Institutions.KeeperName(town, i) : "the keeper");
        Put("{keeperName}", () => Bound(town, s, 0) is { } i ? Institutions.KeeperGiven(town, i) : "the keeper");
        Put("{keeper2}", () => Bound(town, s, 1) is { } i ? Institutions.KeeperName(town, i) : "the other keeper");
        Put("{keeper2Name}", () => Bound(town, s, 1) is { } i ? Institutions.KeeperGiven(town, i) : "the other keeper");
        Put("{choice}", () => Bound(town, s, 0) is { } i && (Institutions.Chosen(town, i) is { } o) ? o.Name : "its calling");
        Put("{grievance}", () => s.HeroId is int g && town.Agent(g).Hero is { RegardWhy: not "" } gs ? gs.RegardWhy : "made the choices they did");
        Put("{patron}", () => (s.Patron ?? (s.HeroId is int ph ? town.Agent(ph).Hero?.Patron : null)) is { Length: > 0 } pid && town.Content.Patrons.Patron(pid) is { } pd ? pd.Name : "the patron");
        Put("{patronTitle}", () => (s.Patron ?? (s.HeroId is int ph ? town.Agent(ph).Hero?.Patron : null)) is { Length: > 0 } pid && town.Content.Patrons.Patron(pid) is { } pd ? pd.Title : "a power beyond the world");
        Put("{familiar}", () => s.HeroId is int fh && town.Agent(fh).Hero is { Familiar: not "" } fs ? fs.Familiar : "a familiar");
        Put("{curse}", () => s.HeroId is int cuh && town.Agent(cuh).Hero is { } cus && Curses.Of(town.Content, cus) is { } cud ? cud.Name : "cursed");
        Put("{curseText}", () => s.HeroId is int cuh && town.Agent(cuh).Hero is { } cus && Curses.Of(town.Content, cus) is { } cud ? cud.Text : "");
        Put("{tavernKeeper}", () => s.BuildingId is int tk && Taverns.Keeper(town, town.Building(tk)) is { } kp ? kp.Name : "the keeper");
        Put("{tavernKeeperName}", () => s.BuildingId is int tk && Taverns.Keeper(town, town.Building(tk)) is { } kp ? kp.GivenName : "the keeper");
        Put("{keeperPast}", () => s.BuildingId is int tk && Taverns.Past(town, town.Building(tk)) is { } kp ? kp.Name : "a stranger");
        Put("{keeperStory}", () => s.BuildingId is int tk && Taverns.Past(town, town.Building(tk)) is { } kp ? kp.Story : "came from somewhere else");
        Put("{victim}", () => s.HeroId is int vh && town.BloodDebts.LastOrDefault(d => d.HeroId == vh) is { } vd ? vd.Victim : "the dead");
        Put("{kin}", () => s.HeroId is int vh && town.BloodDebts.LastOrDefault(d => d.HeroId == vh) is { } vd ? vd.Kin : "the family");
        Put("{disguise}", () => s.LurkerId is int dl && Lurkers.Of(town, dl) is { } dll ? dll.Disguise : "a stranger");
        Put("{agentBy}", () => s.LurkerId is int bl && Lurkers.Of(town, bl) is { } bll ? bll.By : s.MoleId is int bm && town.Moles.FirstOrDefault(m => m.Id == bm) is { } bmm ? bmm.By : "the enemy");
        Put("{lurks}", () => s.LurkerId is int wl && Lurkers.Of(town, wl) is { } wll ? Lurkers.Where(town, wll) : "the gate");
        Put("{grudge}", () => s.Foe is string gf && Vengeance.Of(town, gf) is { } gg ? gg.Name : "the enemy");
        Put("{grudgeLeader}", () => s.Foe is string gl && Vengeance.Of(town, gl) is { Leader: not "" } gll ? gll.Leader : "their master");
        Put("{guild}", () => s.GuildId is string gi && Guilds.Find(town, gi) is { } gu ? Lower(Guilds.Name(town, gu)) : "the guild");
        Put("{guildMaster}", () => s.GuildId is string gm && Guilds.Find(town, gm)?.MasterId is int gmi ? town.Agent(gmi).Name : "the guild master");
        Put("{notable}", () => s.NotableId is int n1 && town.Notables.FirstOrDefault(n => n.Id == n1) is { } nn1 ? nn1.Name : "a notable");
        Put("{notable2}", () => s.Notable2Id is int n2 && town.Notables.FirstOrDefault(n => n.Id == n2) is { } nn2 ? nn2.Name : "another notable");
        Put("{place}", () => s.BuildingId is int pb ? town.Building(pb).Name : "the street");
        Put("{class}", () => s.HeroId is int h ? town.Agent(h).Hero?.Cls.Name.ToLowerInvariant() ?? "hero" : "hero");
        // A name that opens a sentence ("the Ashen Abbot") is capitalised there.
        return Saga.Sentences(sb.ToString());
    }

    /// <summary>What whoever the story is about wants, from their lineage: a menace's in its leader's words.</summary>
    private static string Motive(Town town, Story s)
    {
        var h = town.Content.History;
        if (s.MenaceId is int m && Annals.Motive(town, town.Saga.Menaces[m - 1]) is { Length: > 0 } said) return said;
        var l = s.SettlementId is int st ? Annals.LineageOf(h, "people:" + town.Settlements[st].Kin.Id)
            : s.Minor is string mn ? Annals.LineageOf(h, "faction:" + Powers.Def(town, mn).Ref)
            : null;
        return l is { Motive: not "" } ? l.Motive.Replace("{town}", town.Name) : "to be left alone.";
    }

    private static InstitutionState? Bound(Town town, Story s, int i) => i < s.Institutions.Count ? Institutions.Find(town, s.Institutions[i]) : null;

    private static string InstitutionName(Town town, Story s, int i) => i < s.Institutions.Count && town.Content.Institutions.Def(s.Institutions[i]) is { } d ? d.Name : "the institution";

    internal static string Cap(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string Lower(string s) => s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    /// <summary>Option labels that don't start with a verb ("Friendship", "They knew the risks") read as a quote.</summary>
    private static readonly HashSet<string> NotVerbs = ["a", "an", "the", "they", "it's", "one", "only", "half", "no", "not", "nothing", "friendship", "gold", "quietly", "what", "which", "who"];

    /// <summary>"chose to make it talk", or "chose “Friendship”" when the label (before it is filled) isn't a deed.</summary>
    public static string Chose(string label, string filled)
    {
        string first = label.Split(' ', 2)[0].TrimEnd(',', '.').ToLowerInvariant();
        return NotVerbs.Contains(first) || !char.IsLetter(label.FirstOrDefault()) ? $"chose “{filled}”" : $"chose to {Lower(filled)}";
    }
}
