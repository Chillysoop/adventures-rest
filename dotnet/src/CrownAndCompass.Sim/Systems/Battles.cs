namespace CrownAndCompass.Sim.Systems;

/// <summary>How a siege is played back on the map after it is fought (garrison.json <c>battle</c>).</summary>
public sealed record BattleRules
{
    /// <summary>
    /// The hour the assault goes in. A host reaches the walls in the night (when the siege is settled) and camps before them
    /// until then, so the map plays the fight by daylight.
    /// </summary>
    public int Dawn { get; init; } = 6;

    /// <summary>In-game hours the assault takes to play back, from dawn.</summary>
    public double Hours { get; init; } = 6;

    /// <summary>Shares of the assault: its waves, then the champions' sally; the rest is the ending.</summary>
    public double Waves { get; init; } = 0.62;
    public double Sally { get; init; } = 0.24;

    /// <summary>Sieges kept for the map to play back, newest last.</summary>
    public int Keep { get; init; } = 4;
}

/// <summary>One wave of a siege: the section of wall it hit, about how many came, and whether they got in.</summary>
public sealed class BattleWave
{
    public int Section { get; init; }
    public int Size { get; init; }
    public bool Breached { get; init; }
}

/// <summary>How a siege ended.</summary>
public enum BattleEnd
{
    /// <summary>The walls held and no hero was home to meet the leader: the host drew off.</summary>
    Held,

    /// <summary>The heroes met the leader and it got away from them: the host drew off with it.</summary>
    Repelled,

    /// <summary>The heroes cut the leader down and the host broke.</summary>
    Slain,

    /// <summary>The heroes struck the leader down but the host carried it off the field.</summary>
    Carried,

    /// <summary>The walls fell and the town was sacked and put under the yoke.</summary>
    Sacked,

    /// <summary>The walls fell and the town with them.</summary>
    Fell,
}

/// <summary>Where a played-back siege is at a moment.</summary>
public enum BattlePhase
{
    March,
    Waves,
    Sally,
    End,
}

/// <summary>
/// A siege as it was fought, recorded for the map to play back over the hours after it (<see cref="Battles"/>).
/// The siege itself is settled in one stroke by <see cref="Threat"/> or <see cref="Menaces"/>; this is only what happened.
/// </summary>
public sealed class Battle
{
    public int Id { get; init; }
    public int Tick { get; init; }

    /// <summary>Who came: "warband" for the warband's Last Stand, else "menace:id".</summary>
    public string Foe { get; init; } = "";

    /// <summary>The menace kind it was ("warband", "tribe", "undead"...), for how the host looks.</summary>
    public string Kind { get; init; } = "";
    public string Name { get; init; } = "";
    public string People { get; init; } = "";
    public string Leader { get; init; } = "";

    /// <summary>The section of wall the host came at, and the strength on each side at the start.</summary>
    public int Approach { get; init; }
    public int Strength { get; init; }
    public int Defenders { get; init; }
    public List<BattleWave> Waves { get; } = [];

    /// <summary>War-wagons that rolled at the gate (<see cref="Cannons.Wagons"/>), and how many reached it.</summary>
    public int Wagons { get; set; }
    public int WagonsThrough { get; set; }

    /// <summary>Townsfolk who fell on the walls; the heroes who went out to meet the leader, and those of them who fell.</summary>
    public List<int> Fallen { get; } = [];
    public List<int> Champions { get; } = [];
    public List<int> ChampionsFell { get; } = [];

    /// <summary>Traps that went off before the wall, by id (<see cref="Traps"/>).</summary>
    public List<int> Traps { get; } = [];
    public BattleEnd End { get; set; }

    public bool Breached => Waves.Any(w => w.Breached);
    public bool Lost => End is BattleEnd.Sacked or BattleEnd.Fell;
}

/// <summary>
/// The siege on the map: both siege paths record what happened as a <see cref="Battle"/>, and the client plays it back over
/// a few hours from the hour it was fought: the host marching up, each wave at its stretch of wall, the heroes going out to
/// meet the leader, and how it ended. Recording rolls no dice and changes nothing, so it moves no outcome.
/// </summary>
public static class Battles
{
    public static BattleRules Rules(Town town) => town.Content.Garrison.Battle;

    /// <summary>Start the record of a siege, as the host comes up to the walls.</summary>
    public static Battle Begin(Town town, string foe, string kind, string name, string people, string leader, int approach, double strength, double defenders)
    {
        var b = new Battle
        {
            Id = town.Battles.Count > 0 ? town.Battles[^1].Id + 1 : 1,
            Tick = town.Tick,
            Foe = foe,
            Kind = kind,
            Name = name,
            People = people,
            Leader = leader,
            Approach = approach,
            Strength = (int)Js.Round(strength),
            Defenders = (int)Js.Round(defenders),
        };
        town.Battles.Add(b);
        Records.Pin(town, "siege", name);
        while (town.Battles.Count > Math.Max(1, Rules(town).Keep)) town.Battles.RemoveAt(0);
        return b;
    }

    public static void Wave(Battle b, int section, double size, bool breached) =>
        b.Waves.Add(new BattleWave { Section = section, Size = (int)Js.Round(size), Breached = breached });

    /// <summary>When the assault goes in: the next dawn after the host reaches the walls (or at once, if it came by day).</summary>
    public static int Assault(Town town, Battle b)
    {
        int hour = SimTime.HourOf(b.Tick);
        return hour < Rules(town).Dawn ? b.Tick - hour + Rules(town).Dawn : b.Tick;
    }

    /// <summary>The siege being played back at a moment (a tick with the hour's fraction), the newest if two overlap.</summary>
    public static Battle? Playing(Town town, double at)
    {
        double hours = Rules(town).Hours;
        for (int i = town.Battles.Count - 1; i >= 0; i--)
        {
            var b = town.Battles[i];
            if (at >= b.Tick && at < Assault(town, b) + hours) return b;
        }
        return null;
    }

    /// <summary>
    /// Where a siege's playback is at a moment: its phase (the host coming up and camping until dawn, its waves, the sally,
    /// the end), the wave (0-based) while the waves come on, and how far through that stretch.
    /// </summary>
    public static (BattlePhase Phase, int Wave, double U) At(Town town, Battle b, double at)
    {
        var r = Rules(town);
        int dawn = Assault(town, b);
        if (at < dawn) return (BattlePhase.March, 0, Math.Clamp((at - b.Tick) / Math.Max(1, dawn - b.Tick), 0, 1));
        double f = Math.Clamp((at - dawn) / r.Hours, 0, 1);
        if (f < r.Waves && b.Waves.Count > 0)
        {
            double each = r.Waves / b.Waves.Count;
            int w = Math.Min(b.Waves.Count - 1, (int)(f / each));
            return (BattlePhase.Waves, w, (f - w * each) / each);
        }
        f -= r.Waves;
        if (f < r.Sally) return (BattlePhase.Sally, Math.Max(0, b.Waves.Count - 1), f / r.Sally);
        f -= r.Sally;
        return (BattlePhase.End, Math.Max(0, b.Waves.Count - 1), Math.Clamp(f / Math.Max(1e-6, 1 - r.Waves - r.Sally), 0, 1));
    }

    /// <summary>What the map's banner says while a siege plays back: "Siege at the north wall · wave 2 of 3".</summary>
    public static string Headline(Town town, Battle b, double at)
    {
        var (phase, wave, _) = At(town, b, at);
        string where = Garrison.Place(town, b.Approach);
        return phase switch
        {
            BattlePhase.March => $"{Saga.Cap(b.Name)} camps before the {where} · the assault comes at dawn",
            BattlePhase.Waves => $"Siege at the {Garrison.Place(town, b.Waves[wave].Section)} · wave {wave + 1} of {b.Waves.Count}",
            BattlePhase.Sally when b.Lost => $"The walls are breached · {b.Leader} is in the streets",
            BattlePhase.Sally when b.Champions.Count > 0 => $"The heroes sally out to meet {b.Leader}",
            BattlePhase.Sally => $"The walls hold · {b.Leader} draws off",
            _ => b.End switch
            {
                BattleEnd.Slain => $"{b.Leader} is slain · the host breaks",
                BattleEnd.Carried => $"{b.Leader} is struck down and carried off",
                BattleEnd.Sacked => $"{town.Name} is sacked",
                BattleEnd.Fell => $"{town.Name} falls",
                _ => $"{Saga.Cap(b.Name)} draws off from the walls",
            },
        };
    }
}
