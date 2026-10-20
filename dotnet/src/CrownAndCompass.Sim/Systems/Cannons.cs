namespace CrownAndCompass.Sim.Systems;

public sealed record CannonModeDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";

    /// <summary>The foes it is loaded for: by kind (battle kind or menace kind), or by signature foe.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public IReadOnlyList<string> Foes { get; init; } = [];
    public double Factor { get; init; } = 1;
    public string Text { get; init; } = "";
}

public sealed record WagonRules
{
    /// <summary>Who builds war-wagons, from which siege or march of theirs, and how many at most.</summary>
    public IReadOnlyList<string> Kinds { get; init; } = [];
    public int After { get; init; } = 1;
    public int Most { get; init; } = 3;

    /// <summary>The wall's hold lost for each wagon that reaches the gate.</summary>
    public double Ram { get; init; } = 0.2;

    /// <summary>Chance each gun's shot smashes a wagon (basic, piercing), and a sally's base chance per wagon and per hero level.</summary>
    public double Shot { get; init; } = 0.25;
    public double PiercingShot { get; init; } = 0.7;
    public double Sally { get; init; } = 0.3;
    public double SallyPerLevel { get; init; } = 0.05;
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

public sealed record CannonRules
{
    /// <summary>Guns over the main gate, besides one per tower, and the strength each gun takes off a wave.</summary>
    public int GateGuns { get; init; } = 1;
    public double Basic { get; init; } = 3;
    public double Arcane { get; init; } = 7;
    public IReadOnlyList<CannonModeDef> Modes { get; init; } = [];
    public string BasicText { get; init; } = "";
    public WagonRules Wagons { get; init; } = new();
}

/// <summary>
/// Tower cannons and the war-wagons sent against them. Research "bombards" mounts a gun on every wall tower and over
/// the main gate; "arcane-batteries" (after it, with the Artificers' Foundry's crystals) makes them arcane guns that
/// load piercing bolts against giants and dragons, blast shells against packed hosts and flame against swarms. Every
/// wave of a siege or assault takes their fire before it reaches the wall. Hosts that have seen the walls bring
/// war-wagons with rams for the gate: the guns can smash them, heroes at home sally out against them, and each that
/// reaches the gate weakens the wall's hold.
/// </summary>
public static class Cannons
{
    private static CannonRules Rules(Town town) => town.Content.Cannons;

    /// <summary>0 no guns, 1 bombards, 2 arcane guns.</summary>
    public static int Rank(Town town) => Research.Known(town).Select(d => d.Cannons).DefaultIfEmpty(0).Max();

    /// <summary>Guns on the walls: one per tower and over the gate, once there is a wall to put them on.</summary>
    public static int Guns(Town town)
    {
        var d = town.Defenses;
        bool walled = d.Palisade == PalisadeState.Built || d.StoneWall == PalisadeState.Built || d.CityWall == PalisadeState.Built;
        return Rank(town) == 0 || !walled ? 0 : d.Towers + Rules(town).GateGuns;
    }

    /// <summary>The arcane guns' mode for this foe: by signature foe first, then by kind.</summary>
    public static CannonModeDef? Mode(Town town, string kind, string foe = "") =>
        Rules(town).Modes.FirstOrDefault(m => foe != "" && m.Foes.Contains(foe)) ?? Rules(town).Modes.FirstOrDefault(m => m.Kinds.Contains(kind));

    /// <summary>What the guns take off one wave; the first wave's line tells of them.</summary>
    public static double Volley(Town town, string kind, string foe, double wave, int w, List<string> lines)
    {
        int guns = Guns(town);
        if (guns == 0) return wave;
        var r = Rules(town);
        var mode = Rank(town) >= 2 ? Mode(town, kind, foe) : null;
        double fire = guns * (Rank(town) >= 2 ? r.Arcane * (mode?.Factor ?? 1) : r.Basic);
        if (w == 1) lines.Add(mode != null ? mode.Text : r.BasicText.Replace("{guns}", guns.ToString()));
        return Math.Max(0, wave - fire);
    }

    /// <summary>
    /// War-wagons roll at the gate before the first wave: the guns and a sally of the heroes at home try to stop them.
    /// Returns the share of the wall's hold left at the gate (1 when none got through).
    /// </summary>
    public static double Wagons(Town town, string kind, int attempts, int gate, List<string> lines, Battle? record = null)
    {
        var w = Rules(town).Wagons;
        var d = town.Defenses;
        bool gated = d.Palisade == PalisadeState.Built || d.StoneWall == PalisadeState.Built || d.CityWall == PalisadeState.Built;
        if (!gated || !w.Kinds.Contains(kind) || attempts < w.After) return 1;
        int n = Math.Min(w.Most, 1 + attempts - w.After);
        if (record != null) record.Wagons = n;
        var rng = town.Rng;
        string place = Garrison.Place(town, gate);
        lines.Add(Text(w, "come", n).Replace("{gate}", place));
        int guns = Guns(town);
        double shot = Rank(town) >= 2 && Mode(town, kind)?.Id == "piercing" ? w.PiercingShot : Rank(town) >= 2 ? (w.Shot + w.PiercingShot) / 2 : w.Shot;
        int smashed = 0;
        for (int g = 0; g < guns && smashed < n; g++)
            if (rng.Chance(shot)) smashed++;
        if (smashed > 0) lines.Add(Text(w, "shot", smashed));
        n -= smashed;
        var hero = town.Heroes.Where(a => !a.IsAway && a.Hero!.Hp > 0).OrderByDescending(a => a.Hero!.Level).ThenBy(a => a.Id).FirstOrDefault();
        if (n > 0 && hero != null)
        {
            int fired = 0;
            for (int i = 0; i < n; i++)
                if (rng.Chance(Math.Min(0.9, w.Sally + w.SallyPerLevel * hero.Hero!.Level))) fired++;
            if (fired > 0)
            {
                lines.Add(Text(w, "sally", fired).Replace("{hero}", hero.Name));
                Notoriety.Deed(town, hero, "slay", 2 * fired);
            }
            n -= fired;
        }
        if (n <= 0) return 1;
        if (record != null) record.WagonsThrough = n;
        lines.Add(Text(w, "ram", n).Replace("{gate}", place));
        return Math.Max(0.2, 1 - w.Ram * n);
    }

    private static string Text(WagonRules w, string key, int n) => w.Texts.GetValueOrDefault(key, "")
        .Replace("{wagons}", n == 1 ? "A war-wagon rolls" : $"{n} war-wagons roll")
        .Replace("{reach}", n == 1 ? "One reaches" : $"{n} reach")
        .Replace("{n}", n == 1 ? "one" : Js.Str(n));
}
