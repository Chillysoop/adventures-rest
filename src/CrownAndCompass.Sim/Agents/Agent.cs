using CrownAndCompass.Sim.Core;

namespace CrownAndCompass.Sim.Agents;

[Flags]
public enum Trait
{
    None = 0,
    Ambitious = 1 << 0,
    Lazy = 1 << 1,
    Gregarious = 1 << 2,
    Shy = 1 << 3,
    HotTempered = 1 << 4,
    Greedy = 1 << 5,
    Generous = 1 << 6,
    Romantic = 1 << 7,
}

public enum Ambition { None, OwnHome, OpenBusiness, Landlord }

public enum Activity { Idle, Sleeping, Eating, Working, Building, Drinking, Relaxing, Wandering }

public sealed class Abilities
{
    public int Str, Dex, Con, Int, Wis, Cha;

    public static Abilities Roll(Rng rng) => new()
    {
        Str = rng.AbilityScore(), Dex = rng.AbilityScore(), Con = rng.AbilityScore(),
        Int = rng.AbilityScore(), Wis = rng.AbilityScore(), Cha = rng.AbilityScore(),
    };

    public override string ToString() => $"STR {Str} DEX {Dex} CON {Con} INT {Int} WIS {Wis} CHA {Cha}";
}

/// <summary>
/// One autonomous townsperson. The mayor never controls agents directly; they act on needs,
/// traits, money and relationships.
/// </summary>
public sealed class Agent
{
    public int Id { get; init; }
    public string GivenName { get; init; } = "";
    public string FamilyName { get; set; } = "";
    public string Race { get; init; } = "";
    public int Age { get; init; }
    public Abilities Abilities { get; init; } = new();
    public Trait Traits { get; init; }
    public string Profession { get; init; } = "";
    public Ambition Ambition { get; set; }
    public string? DreamBusiness { get; set; }

    public double Gold { get; set; }

    // Needs, 0 = satisfied, 100 = desperate.
    public double Hunger { get; set; }
    public double Fatigue { get; set; }
    public double Loneliness { get; set; }

    public int? HomeId { get; set; }
    public int? JobId { get; set; }
    public int? SpouseId { get; set; }
    public int? CourtingId { get; set; }

    public Activity Activity { get; set; }
    public int? LocationId { get; set; }
    public int Drinks { get; set; }

    public long ArrivedTick { get; init; }
    public bool Departed { get; set; }

    public double MoodToday { get; set; }
    public int MoodSamples { get; set; }
    public double LastDayMood { get; set; } = 70;
    public int MiserableDays { get; set; }
    public int DaysUnemployed { get; set; }
    public int DaysHomeless { get; set; }
    public int RentArrears { get; set; }

    /// <summary>Opinion of other agents, -100..100.</summary>
    public Dictionary<int, int> Opinions { get; } = new();

    public string Name => $"{GivenName} {FamilyName}";
    public bool Has(Trait t) => (Traits & t) != 0;

    public int OpinionOf(int other) => Opinions.TryGetValue(other, out var v) ? v : 0;

    public int ChangeOpinion(int other, int delta)
    {
        int v = Math.Clamp(OpinionOf(other) + delta, -100, 100);
        Opinions[other] = v;
        return v;
    }

    public string Describe() => $"{Name}, {Article(Race)} {Race} {Profession}";

    public static string Article(string word) => "aeiouAEIOU".Contains(word[0]) ? "an" : "a";

    public string TraitList() =>
        Traits == Trait.None ? "unremarkable" :
        string.Join(", ", Enum.GetValues<Trait>().Where(t => t != Trait.None && Has(t)).Select(t => t.ToString().ToLowerInvariant()));
}
