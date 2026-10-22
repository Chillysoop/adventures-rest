namespace CrownAndCompass.Sim;

/// <summary>Personality traits, in the canonical order trait lists are printed in.</summary>
public enum Trait
{
    Ambitious,
    Lazy,
    Gregarious,
    Shy,
    HotTempered,
    Greedy,
    Generous,
    Romantic,
}

public enum Ambition
{
    None,
    OwnHome,
    OpenBusiness,
    Landlord,
}

public enum Activity
{
    Idle,
    Sleeping,
    Eating,
    Working,
    Building,
    Drinking,
    Relaxing,
    Wandering,
    Praying,

    /// <summary>Carrying timber or stone onto a building site (<see cref="Systems.Construction"/>).</summary>
    Hauling,

    /// <summary>Out at a bathhouse, theatre, market hall, library or the arena (<see cref="Systems.Crowds"/>).</summary>
    Visiting,

    /// <summary>A soldier at drill in their hall (<see cref="Systems.Army"/>).</summary>
    Drilling,

    /// <summary>A soldier walking the town guard's round.</summary>
    Patrolling,

    /// <summary>A soldier on the walls or standing guard at the hall.</summary>
    Guarding,

    /// <summary>Out on the road with the host (<see cref="Systems.Field"/>).</summary>
    Marching,
}

public static class TraitOrder
{
    public static readonly IReadOnlyList<Trait> All = Enum.GetValues<Trait>();
}

public sealed class Abilities
{
    public int Str { get; set; } = 10;
    public int Dex { get; set; } = 10;
    public int Con { get; set; } = 10;
    public int Int { get; set; } = 10;
    public int Wis { get; set; } = 10;
    public int Cha { get; set; } = 10;

    /// <summary>By content key: str, dex, con, int, wis, cha.</summary>
    public int this[string ability]
    {
        get => ability switch
        {
            Ability.Str => Str,
            Ability.Dex => Dex,
            Ability.Con => Con,
            Ability.Int => Int,
            Ability.Wis => Wis,
            Ability.Cha => Cha,
            _ => throw new ArgumentOutOfRangeException(nameof(ability), ability, null),
        };
        set
        {
            switch (ability)
            {
                case Ability.Str: Str = value; break;
                case Ability.Dex: Dex = value; break;
                case Ability.Con: Con = value; break;
                case Ability.Int: Int = value; break;
                case Ability.Wis: Wis = value; break;
                case Ability.Cha: Cha = value; break;
                default: throw new ArgumentOutOfRangeException(nameof(ability), ability, null);
            }
        }
    }

    /// <summary>Six 4d6-drop-lowest scores, rolled in STR, DEX, CON, INT, WIS, CHA order.</summary>
    public static Abilities Roll(Rng rng) => new()
    {
        Str = rng.AbilityScore(),
        Dex = rng.AbilityScore(),
        Con = rng.AbilityScore(),
        Int = rng.AbilityScore(),
        Wis = rng.AbilityScore(),
        Cha = rng.AbilityScore(),
    };

    public override string ToString() => $"STR {Str} DEX {Dex} CON {Con} INT {Int} WIS {Wis} CHA {Cha}";
}

/// <summary>The adventuring half of a hero: class, level, hit points and gear.</summary>
public sealed class HeroSheet
{
    public required ClassDef Cls { get; init; }

    /// <summary>The library background they came up through (a lineage in content/game/history.json); "" for none.</summary>
    public string Background { get; set; } = "";

    /// <summary>Who they were before, when it isn't a background: a menace's leader who turned, a legend of another campaign.</summary>
    public string Past { get; set; } = "";
    public int Level { get; set; }
    public int Xp { get; set; }
    public int MaxHp { get; set; }
    public int Hp { get; set; }

    /// <summary>Passing weapon bonus (attack and damage), such as a god's blessing; gear adds its own.</summary>
    public int WeaponBonus { get; set; }

    /// <summary>Passing armour or ward bonus (AC); gear adds its own.</summary>
    public int ArmorBonus { get; set; }

    /// <summary>Arms and armour from the library's equipment lists; null for none (<see cref="Systems.Gear"/>).</summary>
    public LibGear? Weapon { get; set; }
    public LibGear? Armor { get; set; }
    public LibGear? Shield { get; set; }

    /// <summary>Magic gear worn, by slot (weapon, armor, shield, trinket).</summary>
    public OrderedMap<string, LibItem> Magic { get; } = new();

    /// <summary>Names townsfolk gave the gear they forged, by the slot it is worn in (<see cref="Systems.Moods"/>).</summary>
    public OrderedMap<string, string> Named { get; } = new();

    /// <summary>Names of the magic gear worn, in slot order.</summary>
    public List<string> Items { get; } = [];

    /// <summary>Carries one of the town's sending stones: their company can send word with no caster's slot (<see cref="Systems.Armory"/>).</summary>
    public bool Stone { get; set; }

    /// <summary>The site kind of the ward draught they drank before setting out, harder to hit there (<see cref="Systems.Alchemy"/>).</summary>
    public string? Ward { get; set; }

    /// <summary>The day the mayor last gave them something from the armory.</summary>
    public int LastGiftDay { get; set; } = -99;

    /// <summary>How far the hero will stand by the town, 0 to 100 (<see cref="Systems.Loyalty"/>).</summary>
    public double Loyalty { get; set; } = 55;

    /// <summary>Days in a row the hero has been ready to leave.</summary>
    public int DisloyalDays { get; set; }

    /// <summary>Has let the taverns know they are unhappy (said once until they are content again).</summary>
    public bool Restless { get; set; }

    /// <summary>Gold the town paid them lately (bounties, rewards, stipends), fading day by day.</summary>
    public double Pay { get; set; }

    /// <summary>Knighted by the town: Ser, with a stipend and an oath.</summary>
    public bool Knight { get; set; }
    public int? KnightedDay { get; set; }

    /// <summary>A cleared site the town granted the knight as their land.</summary>
    public string? FiefId { get; set; }

    /// <summary>How the knight runs their land, their own choice (<see cref="Systems.Loyalty.Choose"/>): "tower", "ride" or "rent".</summary>
    public string FiefWay { get; set; } = "";

    /// <summary>The town's sworn champion (one at a time).</summary>
    public bool Champion { get; set; }

    /// <summary>Posted to guard the town on a wage, and taking no bounties (<see cref="Systems.Garrison.SetDuty"/>).</summary>
    public Systems.HeroDuty Duty { get; set; }
    public int LastRewardDay { get; set; } = -99;

    /// <summary>The menace they went over to, or the faction they ran to.</summary>
    public int? TurnedTo { get; set; }
    public string? DefectedTo { get; set; }

    /// <summary>The bastion the hero lives in and rides for (<see cref="Systems.Bastions"/>), by id.</summary>
    public int? BastionId { get; set; }

    /// <summary>Fame from deeds (<see cref="Systems.Notoriety"/>).</summary>
    public int Renown { get; set; }

    /// <summary>Ill fame from brawls, theft, desertion and treachery; it fades slowly.</summary>
    public int Infamy { get; set; }

    /// <summary>What the bards (or the gossips) call them: "the Delver", "the Turncloak"; "" for none.</summary>
    public string Epithet { get; set; } = "";

    /// <summary>The epithet is an ill name, earned by infamy.</summary>
    public bool EpithetIll { get; set; }
    public int Kills { get; set; }
    public int Expeditions { get; set; }
    public string? PartyName { get; set; }

    /// <summary>The mount the hero rides (<see cref="Systems.MountDef"/> id), or "" on foot.</summary>
    public string Mount { get; set; } = "";

    /// <summary>Tick the hero is due back from an expedition, or null when in town.</summary>
    public int? AwayUntil { get; set; }

    /// <summary>A caster resting after a scrying casts no other until this tick (<see cref="Systems.Knowledge"/>).</summary>
    public int? ScryRestUntil { get; set; }

    public bool Lodging { get; set; }

    /// <summary>Sworn to the charter: the town hall keeps a bed for them, tavern or none.</summary>
    public bool Sworn { get; set; }

    /// <summary>Healing potions from the general store, carried into the next delve.</summary>
    public int Potions { get; set; }

    /// <summary>Spell scrolls from the scriptorium or a hoard, each read once in a fight (<see cref="Systems.Scrolls"/>).</summary>
    public int Scrolls { get; set; }

    /// <summary>Scrolls a wizard copied into their spellbook; each adds to their area spell.</summary>
    public int Copied { get; set; }

    /// <summary>What the hero cares about in a mayor (a temper in content/game/regard.json); "" until settled.</summary>
    public string Temper { get; set; } = "";

    /// <summary>What the hero thinks of the mayor's choices, -100 to 100 (<see cref="Systems.Regard"/>).</summary>
    public double Regard { get; set; }

    /// <summary>The choice that last moved their regard most ("chose to keep the gates shut"), and which way.</summary>
    public string RegardWhy { get; set; } = "";
    public bool RegardPleased { get; set; }

    /// <summary>Their personal quest: "" not yet asked, "done" seen through, "failed" let down.</summary>
    public string Personal { get; set; } = "";

    /// <summary>The tick they walk out of the town cells, while the mayor has them locked up.</summary>
    public int? JailedUntil { get; set; }

    /// <summary>Dark gifts taken: each one makes the hero likelier to fall (<see cref="Systems.Turning"/>).</summary>
    public int Taint { get; set; }

    /// <summary>Mishaps and feats on delves, by kind, that grow into running jokes (<see cref="Systems.Voices"/>).</summary>
    public OrderedMap<string, int> Gags { get; } = new();

    /// <summary>What their friends call them after a running joke ("Butterfingers"), and which joke; "" for none.</summary>
    public string Nickname { get; set; } = "";
    public string NicknameFor { get; set; } = "";

    /// <summary>The site they swore over a friend's grave to see cleared, and the friend.</summary>
    public string? Vow { get; set; }
    public string VowFor { get; set; } = "";

    /// <summary>The patron they are sworn to (a patrons.json id), "" for none, and its favour, -5 to +5 (<see cref="Systems.Patrons"/>).</summary>
    public string Patron { get; set; } = "";
    public int Favour { get; set; }

    /// <summary>The day the patron last judged them, and what its deeds stood at then.</summary>
    public int PactJudged { get; set; }
    public OrderedMap<string, double> PactMarks { get; } = new();

    /// <summary>Broke a pact once: no patron claims them again unasked.</summary>
    public bool PactRefused { get; set; }

    /// <summary>Favours the town owes this caster for scryings they cast on credit (<see cref="Systems.Knowledge.ScryOffer"/>).</summary>
    public int Owed { get; set; }

    /// <summary>The curse on their magic weapon, "" for none (<see cref="Systems.Curses"/>).</summary>
    public string Curse { get; set; } = "";

    /// <summary>Their own house and land in town, "" until they buy one; a business they hold a stake in, and what they put in (<see cref="Systems.Downtime"/>).</summary>
    public string Holding { get; set; } = "";
    public int? StakeId { get; set; }
    public double Stake { get; set; }

    /// <summary>The familiar their patron sent ("a cinder imp"), "" for none.</summary>
    public string Familiar { get; set; } = "";

    /// <summary>Where each magic item they carry came from, by item id ("Taken from King-under-Turf on day 51...").</summary>
    public OrderedMap<string, string> ItemTales { get; } = new();

    /// <summary>Lasting wounds they carry, oldest first (<see cref="Systems.Wounds"/>).</summary>
    public List<Systems.Wound> Wounds { get; } = [];
}

/// <summary>
/// One autonomous townsperson. The mayor never controls agents directly; they act on needs,
/// traits, money and relationships.
/// </summary>
public sealed class Agent
{
    public int Id { get; set; }
    public string GivenName { get; set; } = "";
    public string FamilyName { get; set; } = "";
    public string Race { get; set; } = "";
    public int Age { get; set; }
    public Abilities Abilities { get; set; } = new();
    public IReadOnlySet<Trait> Traits { get; set; } = new HashSet<Trait>();
    public string Profession { get; set; } = "";
    public Ambition Ambition { get; set; } = Ambition.None;
    public string? DreamBusiness { get; set; }
    public HeroSheet? Hero { get; set; }
    public bool Dead { get; set; }

    /// <summary>Has taken the town god's vows: prays, tithes, and may be raised at the temple's cost.</summary>
    public bool Devout { get; set; }

    public bool PrayedToday { get; set; }

    /// <summary>The site whose schemers have this resident in their thrall; they steal for it until it falls.</summary>
    public string? ThrallOf { get; set; }

    /// <summary>The god the agent keeps (a library deity id), "" for the faithless, null until settled on arrival.</summary>
    public string? Faith { get; set; }

    public double Gold { get; set; }

    // Needs, 0 = satisfied, 100 = desperate.
    public double Hunger { get; set; }
    public double Fatigue { get; set; }
    public double Loneliness { get; set; }

    public int? HomeId { get; set; }
    public int? JobId { get; set; }

    /// <summary>The town regiment they serve in (<see cref="Systems.Army"/>); a standing soldier's job is their hall.</summary>
    public int? RegimentId { get; set; }
    public int? SpouseId { get; set; }
    public int? CourtingId { get; set; }

    /// <summary>The workplace whose trade spell they taught themselves (<see cref="Systems.HedgeMages"/>), "" for none.</summary>
    public string HedgeSpell { get; set; } = "";

    /// <summary>Successes at their trade's skills, which slowly make them better at it (<see cref="Systems.Skills"/>).</summary>
    public int Practice { get; set; }

    public Activity Activity { get; set; } = Activity.Idle;
    public int? LocationId { get; set; }
    public int Drinks { get; set; }

    /// <summary>The full building they walked to this hour before thinking again (<see cref="Systems.Crowds"/>).</summary>
    public int? TurnedFrom { get; set; }

    /// <summary>Been out to a bathhouse, theatre or the like today.</summary>
    public bool VisitedToday { get; set; }

    public int ArrivedTick { get; set; }
    public bool Departed { get; set; }

    public double MoodToday { get; set; }
    public int MoodSamples { get; set; }
    public double LastDayMood { get; set; } = 70;
    public int MiserableDays { get; set; }

    /// <summary>Days in a row their mood has been high, or low, enough to be struck by an inspiration or a break (<see cref="Systems.Moods"/>).</summary>
    public int HighDays { get; set; }
    public int LowDays { get; set; }

    /// <summary>The inspiration or break they are in the grip of (a moods.json id), "" for none, and the tick it passes.</summary>
    public string Episode { get; set; } = "";
    public int EpisodeUntil { get; set; }

    /// <summary>The day their last inspiration or break came; none comes again too soon.</summary>
    public int EpisodeDay { get; set; } = -999;

    /// <summary>What their inspirations and breaks came to, oldest first, for the inspector ("day 34: forged Emberfang").</summary>
    public List<string> Moments { get; } = [];
    public int DaysUnemployed { get; set; }
    public int DaysHomeless { get; set; }
    public int RentArrears { get; set; }

    /// <summary>Opinion of other agents, -100..100, in the order they were first met.</summary>
    public OrderedMap<int, int> Opinions { get; } = new();

    public string Name => FamilyName == "" ? GivenName : $"{GivenName} {FamilyName}";

    public bool Has(Trait t) => Traits.Contains(t);

    public int OpinionOf(int other) => Opinions.Get(other, 0);

    public int ChangeOpinion(int other, int delta)
    {
        int v = Math.Max(-100, Math.Min(100, OpinionOf(other) + delta));
        Opinions[other] = v;
        return v;
    }

    public bool IsHero => Hero != null;

    public bool IsAway => Hero?.AwayUntil != null;

    public string Describe() =>
        Hero != null
            ? $"{Name}, {Article(Race)} {Race} {Hero.Cls.Name} (level {Hero.Level})"
            : $"{Name}, {Article(Race)} {Race} {Profession}";

    public string TraitList() =>
        Traits.Count == 0 ? "unremarkable" : string.Join(", ", TraitOrder.All.Where(Has).Select(t => t.ToString().ToLowerInvariant()));

    public static string Article(string word) => "aeiouAEIOU".Contains(word[0]) ? "an" : "a";
}
