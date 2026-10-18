namespace CrownAndCompass.Sim.Systems;

/// <summary>
/// Someone remembered from an earlier campaign: a great foe, or a hero of the town. The client keeps them
/// in the player's profile and hands them to the next campaign (<see cref="SimulationOptions.Legends"/>),
/// where now and then they come back on the other side (<see cref="Turning"/>).
/// </summary>
public sealed class Legend
{
    public const string Foe = "foe";
    public const string Hero = "hero";

    public string Kind { get; set; } = Foe;
    public string Name { get; set; } = "";

    /// <summary>What the town called them: "Vermithrax, the Ruin of the Ash Fields", "champion of Briarford".</summary>
    public string Title { get; set; } = "";

    /// <summary>Heroes: race and class ids. Foes: menace kind and lineage.</summary>
    public string Race { get; set; } = "";
    public string Cls { get; set; } = "";
    public string MenaceKind { get; set; } = "";
    public string Lineage { get; set; } = "";
    public int Level { get; set; }
    public string Town { get; set; } = "";
    public int Seed { get; set; }
    public int Day { get; set; }

    /// <summary>What they did, told as "{name} {deed}": "burned Briarford to the ground".</summary>
    public string Deed { get; set; } = "";
}

public static class Legends
{
    /// <summary>How many of a campaign's heroes are remembered.</summary>
    public const int HeroesKept = 3;

    /// <summary>How many legends a profile keeps, newest last.</summary>
    public const int Kept = 40;

    private static readonly System.Text.Json.JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static List<Legend> Read(string json) =>
        string.IsNullOrWhiteSpace(json) ? [] : System.Text.Json.JsonSerializer.Deserialize<List<Legend>>(json, Json) ?? [];

    public static string Write(IEnumerable<Legend> legends) => System.Text.Json.JsonSerializer.Serialize(legends.ToList(), Json);

    /// <summary>A campaign's legends added to what the profile remembers: the same campaign's replace its older ones.</summary>
    public static List<Legend> Merge(IEnumerable<Legend> kept, IReadOnlyList<Legend> fresh)
    {
        var seeds = fresh.Select(l => (l.Seed, l.Town)).ToHashSet();
        var list = kept.Where(l => !seeds.Contains((l.Seed, l.Town))).Concat(fresh).ToList();
        return list.Skip(Math.Max(0, list.Count - Kept)).ToList();
    }

    /// <summary>The campaign's legends: its big bad and risen powers, its champion and best heroes, its turncoats.</summary>
    public static List<Legend> Record(Town town, int seed)
    {
        var list = new List<Legend>();
        bool lost = town.Outcome == Outcome.Defeat;
        foreach (var m in town.Saga.Menaces.Where(m => m.Named && !m.Redeemed && m.Origin != Menaces.FromWarband && (m.BigBad || m.Peak >= MenaceStage.Power)).OrderBy(m => m.Id))
        {
            string deed = m.BigBad && lost ? $"burned {town.Name} to the ground"
                : m.Alive ? $"held {m.Region} against {town.Name} and was never beaten"
                : m.BigBad ? $"nearly ended {town.Name}, and fell to its heroes at the last"
                : $"terrorised {town.Name} from {m.SeatName}, until its heroes struck it down";
            list.Add(new Legend
            {
                Kind = Legend.Foe,
                Name = m.Leader,
                Title = Saga.Cap(m.Name),
                MenaceKind = m.Kind,
                Lineage = m.Lineage,
                Level = m.Level,
                Town = town.Name,
                Seed = seed,
                Day = town.Day,
                Deed = deed,
            });
        }
        var heroes = town.Agents
            .Where(a => a.Hero != null && !a.Dead && (a.Hero.TurnedTo != null || !a.Departed))
            .OrderByDescending(a => a.Hero!.Champion)
            .ThenByDescending(a => a.Hero!.TurnedTo != null)
            // The names the bards sang, for good or ill, outlast the merely strong.
            .ThenByDescending(a => a.Hero!.Renown + a.Hero.Infamy)
            .ThenByDescending(a => a.Hero!.Level)
            .ThenBy(a => a.Id)
            .Take(HeroesKept);
        foreach (var a in heroes)
        {
            var s = a.Hero!;
            string deed = s.TurnedTo != null ? $"turned on {town.Name} and led its enemies"
                : s.Champion ? $"was champion of {town.Name}"
                : $"fought for {town.Name} as a level {s.Level} {s.Cls.Name.ToLowerInvariant()}";
            list.Add(new Legend
            {
                Kind = Legend.Hero,
                Name = a.Name,
                Title = s.Champion ? $"champion of {town.Name}" : s.Epithet != "" ? Saga.Cap(s.Epithet) : s.Cls.Name,
                Race = town.Content.Races.FirstOrDefault(r => r.Name == a.Race)?.Id ?? "",
                Cls = s.Cls.Id,
                Level = s.Level,
                Town = town.Name,
                Seed = seed,
                Day = town.Day,
                Deed = deed,
            });
        }
        return list;
    }
}
