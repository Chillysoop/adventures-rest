namespace CrownAndCompass.Sim.Systems;

/// <summary>A curse on a weapon: what it gives and what it takes (curses.json).</summary>
public sealed record CurseDef
{
    public string Id { get; init; } = "";

    /// <summary>"bloodthirsty", and what it does in a line.</summary>
    public string Name { get; init; } = "";
    public string Text { get; init; } = "";
    public int Attack { get; init; }
    public int Ac { get; init; }
    public int Damage { get; init; }
    public int Skill { get; init; }

    /// <summary>Times the bearer's chance of starting a tavern brawl, loyalty added a day, times their rest in town.</summary>
    public double Brawl { get; init; } = 1;
    public double Loyalty { get; init; }
    public double Heal { get; init; } = 1;

    /// <summary>The bearer can wear no other magic; the bearer sells nothing to the town.</summary>
    public bool Jealous { get; init; }
    public bool Greedy { get; init; }
}

public sealed record CurseLines
{
    public string Cursed { get; init; } = "";
    public string Lifted { get; init; } = "";
    public string Broken { get; init; } = "";
}

public sealed record CurseRules
{
    /// <summary>Per mille of magic weapons taken up from a hoard that are cursed.</summary>
    public int Chance { get; init; } = 150;
    public CurseLines Lines { get; init; } = new();
    public List<CurseDef> Curses { get; init; } = [];

    public CurseDef? Curse(string id) => Curses.FirstOrDefault(c => c.Id == id);
}

/// <summary>
/// Cursed weapons. Now and then a magic weapon a hero takes up from a hoard is cursed: it gives (a deeper bite, a truer
/// strike, a parry, whispered secrets) and takes (brawls, a jealous hold on its bearer, a flinch, a pull away from the
/// town, slow healing, greed), and it will not be put down, sold or passed on until the temple lifts the curse or breaks
/// the blade (stories with need cursed). Which weapons are cursed comes from a stable hash, so it never moves the dice.
/// </summary>
public static class Curses
{
    public static CurseRules Rules(Content c) => c.Curses;

    /// <summary>The curse on the hero's weapon, or null.</summary>
    public static CurseDef? Of(Content c, HeroSheet s) => s.Curse == "" ? null : Rules(c).Curse(s.Curse);

    /// <summary>What the curse adds: attack, ac, damage or skill.</summary>
    public static int Mod(Content c, HeroSheet s, string kind) => Of(c, s) is not { } d ? 0 : kind switch
    {
        "attack" => d.Attack,
        "ac" => d.Ac,
        "damage" => d.Damage,
        "skill" => d.Skill,
        _ => 0,
    };

    /// <summary>Whether the hero's cursed weapon (or its jealousy) keeps them from taking up an item for this slot.</summary>
    public static bool Holds(Content c, HeroSheet s, string slot) => Of(c, s) is { } d && (slot == "weapon" || d.Jealous);

    public static double Brawl(Content c, Agent a) => a.Hero != null && Of(c, a.Hero) is { } d ? d.Brawl : 1;

    public static double Heal(Content c, HeroSheet s) => Of(c, s)?.Heal ?? 1;

    public static bool Greedy(Content c, HeroSheet s) => Of(c, s)?.Greedy == true;

    /// <summary>A hero has just taken up a magic weapon from a hoard: it may be cursed.</summary>
    public static void Taken(Town town, Agent a, LibItem it)
    {
        var r = Rules(town.Content);
        var s = a.Hero!;
        if (r.Curses.Count == 0 || s.Curse != "" || Gear.SlotOf(it) != "weapon") return;
        if (Voices.Hash("curse:" + it.Id, town.Tick, a.Id) % 1000 >= (uint)r.Chance) return;
        var d = r.Curses[(int)(Voices.Hash("curse-kind:" + it.Id, a.Id) % (uint)r.Curses.Count)];
        s.Curse = d.Id;
        // A jealous blade will not share its bearer: every other piece of magic goes.
        if (d.Jealous)
            foreach (var slot in s.Magic.Keys.Where(k => k != "weapon").ToList())
            {
                var other = s.Magic[slot];
                s.Magic.Remove(slot);
                s.Named.Remove(slot);
                Gear.Relist(s);
                Gear.SellOnRoad(town, a, other);
            }
        Log(town, a, r.Lines.Cursed, it, d, Importance.Notable);
    }

    /// <summary>Daily: a whispering blade pulls its bearer away from the town.</summary>
    public static void Daily(Town town)
    {
        foreach (var a in town.Heroes)
            if (Of(town.Content, a.Hero!) is { Loyalty: not 0 } d) a.Hero!.Loyalty = Math.Clamp(a.Hero.Loyalty + d.Loyalty, 0, 100);
    }

    /// <summary>The curse is lifted (the blade stays, only a blade now), or the blade is broken with it.</summary>
    public static void Lift(Town town, Agent a, bool breakIt)
    {
        var s = a.Hero!;
        if (Of(town.Content, s) is not { } d) return;
        var it = s.Magic.TryGetValue("weapon", out var w) ? w : null;
        s.Curse = "";
        if (breakIt && it != null)
        {
            s.Magic.Remove("weapon");
            s.Named.Remove("weapon");
            Gear.Relist(s);
        }
        Log(town, a, breakIt ? Rules(town.Content).Lines.Broken : Rules(town.Content).Lines.Lifted, it, d, Importance.Notable);
    }

    private static void Log(Town town, Agent a, string line, LibItem? it, CurseDef d, Importance importance)
    {
        if (line == "") return;
        town.Log(LedgerCategory.Quest, importance, Stories.Cap(line
            .Replace("{hero}", a.Name)
            .Replace("{item}", it != null ? Gear.Label(a.Hero!, it) : "blade")
            .Replace("{curse}", d.Name)
            .Replace("{text}", d.Text)), [a.Id]);
    }
}
