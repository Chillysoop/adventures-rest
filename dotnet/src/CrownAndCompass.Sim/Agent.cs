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
    public int Level { get; set; }
    public int Xp { get; set; }
    public int MaxHp { get; set; }
    public int Hp { get; set; }

    /// <summary>Magic weapon bonus (attack and damage).</summary>
    public int WeaponBonus { get; set; }

    /// <summary>Magic armor or ward bonus (AC).</summary>
    public int ArmorBonus { get; set; }

    public List<string> Items { get; } = [];
    public int Renown { get; set; }
    public int Kills { get; set; }
    public int Expeditions { get; set; }
    public string? PartyName { get; set; }

    /// <summary>Tick the hero is due back from an expedition, or null when in town.</summary>
    public int? AwayUntil { get; set; }

    public bool Lodging { get; set; }

    /// <summary>Healing potions from the general store, carried into the next delve.</summary>
    public int Potions { get; set; }
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

    public double Gold { get; set; }

    // Needs, 0 = satisfied, 100 = desperate.
    public double Hunger { get; set; }
    public double Fatigue { get; set; }
    public double Loneliness { get; set; }

    public int? HomeId { get; set; }
    public int? JobId { get; set; }
    public int? SpouseId { get; set; }
    public int? CourtingId { get; set; }

    public Activity Activity { get; set; } = Activity.Idle;
    public int? LocationId { get; set; }
    public int Drinks { get; set; }

    public int ArrivedTick { get; set; }
    public bool Departed { get; set; }

    public double MoodToday { get; set; }
    public int MoodSamples { get; set; }
    public double LastDayMood { get; set; } = 70;
    public int MiserableDays { get; set; }
    public int DaysUnemployed { get; set; }
    public int DaysHomeless { get; set; }
    public int RentArrears { get; set; }

    /// <summary>Opinion of other agents, -100..100, in the order they were first met.</summary>
    public OrderedMap<int, int> Opinions { get; } = new();

    public string Name => $"{GivenName} {FamilyName}";

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
