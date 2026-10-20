namespace CrownAndCompass.Sim.Systems;

public sealed record DragonKindDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Hex { get; init; } = "";
    public string Gems { get; init; } = "";
    public IReadOnlyList<string> Names { get; init; } = [];
}

/// <summary>What a dragon of the town does for it while it is small, and once it is grown.</summary>
public sealed record DragonAidDef
{
    public double Defence { get; init; }
    public double Dread { get; init; }
    public double Mood { get; init; }
}

public sealed record EggRules
{
    /// <summary>Chance a cleared dragon's lair holds an egg, and a deep hoard (of at least this tier) does.</summary>
    public double FromDragon { get; init; } = 0.5;
    public double FromHoard { get; init; } = 0.02;
    public int HoardTier { get; init; } = 5;

    /// <summary>Days kept warm before it hatches, and from hatching to grown.</summary>
    public int HatchDays { get; init; } = 60;
    public int GrowDays { get; init; } = 300;

    /// <summary>Daily chance the mother comes for it while the town holds the egg or the young one, after this many days.</summary>
    public double Mother { get; init; } = 0.006;
    public int MotherAfter { get; init; } = 10;

    /// <summary>Gold the egg fetches sold.</summary>
    public double Sell { get; init; } = 1500;
    public DragonAidDef Hatchling { get; init; } = new();
    public DragonAidDef Grown { get; init; } = new();
    public IReadOnlyList<DragonKindDef> Kinds { get; init; } = [];
    public IReadOnlyDictionary<string, string> Texts { get; init; } = new Dictionary<string, string>();
}

/// <summary>The egg the town holds, or the dragon it raised from one.</summary>
public sealed class DragonEgg
{
    public string Kind { get; set; } = "";
    public int FoundDay { get; set; }

    /// <summary>The mayor chose to keep it warm and hatch it, on this day.</summary>
    public bool Kept { get; set; }
    public int KeptDay { get; set; }

    /// <summary>Hatched (and named), and grown into the town's dragon.</summary>
    public int? HatchedDay { get; set; }
    public string Name { get; set; } = "";
    public bool Grown { get; set; }

    /// <summary>The mother is coming, or came and was answered.</summary>
    public bool MotherCame { get; set; }
}

/// <summary>
/// Dragon eggs. A company that clears a dragon's lair may carry home an egg (a deep hoard holds one now and then).
/// The mayor decides on a story card: keep it warm in the keep to hatch, sell it, or give it back. Kept, it hatches
/// into a wyrmling the town names, which grows over a year into the town's own dragon, guarding the walls. While the
/// town holds the egg or the young one, its mother may come for it: another card, to hand it back, hide it or fight.
/// </summary>
public static class Eggs
{
    public const string Source = "egg";

    private static EggRules Rules(Town town) => town.Content.Eggs;

    public static DragonKindDef? Kind(Town town, string id) => Rules(town).Kinds.FirstOrDefault(k => k.Id == id);

    /// <summary>The egg is in town waiting for the mayor's word.</summary>
    public static bool Waiting(Town town) => town.Egg is { Kept: false, HatchedDay: null };

    /// <summary>The town holds an egg or a dragon not yet grown, which a mother would come for.</summary>
    public static bool Held(Town town) => town.Egg is { Grown: false } e && (e.Kept || e.HatchedDay != null);

    /// <summary>A company home from a cleared lair: is there an egg in the hoard? Rolled from the lair and the day, not the town's dice.</summary>
    public static void Found(Town town, Party party, SiteState site)
    {
        var r = Rules(town);
        if (town.Egg != null || r.Kinds.Count == 0) return;
        string boss = site.Def.Boss.Monster ?? "";
        bool dragon = boss.Contains("dragon", StringComparison.Ordinal);
        double chance = dragon ? r.FromDragon : site.Def.Tier >= r.HoardTier ? r.FromHoard : 0;
        if (chance <= 0 || Voices.Hash("egg:" + site.Def.Id, town.Day) % 10000 >= chance * 10000) return;
        var kind = r.Kinds.FirstOrDefault(k => dragon && boss.Contains(k.Id + "-dragon", StringComparison.Ordinal))
            ?? r.Kinds[(int)(Voices.Hash("eggkind:" + site.Def.Id) % (uint)r.Kinds.Count)];
        town.Egg = new DragonEgg { Kind = kind.Id, FoundDay = town.Day };
        town.Log(LedgerCategory.Quest, Importance.Major, Fill(town, r.Texts.GetValueOrDefault("found", ""), kind, site.Def.Name, party.Name), party.Members.ToList());
        Stories.Open(town, "dragon-egg");
    }

    /// <summary>The mayor's word on the egg: keep it to hatch (1), sell it (2), give it back (3), or it is lost (4).</summary>
    public static string Decide(Town town, int what)
    {
        var r = Rules(town);
        if (town.Egg is not { } e) return "";
        var kind = Kind(town, e.Kind);
        switch (what)
        {
            case 1:
                e.Kept = true;
                e.KeptDay = town.Day;
                return r.Texts.GetValueOrDefault("kept", "");
            case 2:
                town.Book(TreasuryAccount.Stories, r.Sell);
                town.Egg = null;
                return $"{r.Texts.GetValueOrDefault("sold", "")} (+{Js.Str(r.Sell)} gold)";
            case 3:
                town.Egg = null;
                return r.Texts.GetValueOrDefault("returned", "");
            default:
                town.Egg = null;
                return r.Texts.GetValueOrDefault("lost", "");
        }
    }

    /// <summary>Daily: a kept egg hatches, a young dragon grows, and a mother may come looking.</summary>
    public static void Daily(Town town)
    {
        var r = Rules(town);
        if (town.Egg is not { } e || Kind(town, e.Kind) is not { } kind) return;
        if (e.Kept && e.HatchedDay == null && town.Day - e.KeptDay >= r.HatchDays)
        {
            e.HatchedDay = town.Day;
            e.Name = kind.Names.Count > 0 ? kind.Names[(int)(Voices.Hash("wyrmling", town.Seed, e.FoundDay) % (uint)kind.Names.Count)] : "the wyrmling";
            town.Log(LedgerCategory.Town, Importance.Major, Fill(town, r.Texts.GetValueOrDefault("hatched", ""), kind, "", "", e.Name));
        }
        else if (e.HatchedDay is int hd && !e.Grown && town.Day - hd >= r.GrowDays)
        {
            e.Grown = true;
            town.Log(LedgerCategory.Town, Importance.Major, Fill(town, r.Texts.GetValueOrDefault("grown", ""), kind, "", "", e.Name));
        }
        if (Aid(town) is { } aid && aid.Dread != 0) town.Saga.Dread = Math.Clamp(town.Saga.Dread + aid.Dread, 0, 100);
        // The mother comes once, for an egg or a young one, never a grown dragon that could answer her itself.
        if (Held(town) && !e.MotherCame && town.Day - e.FoundDay >= r.MotherAfter
            && Voices.Hash("mother", town.Seed, town.Day) % 10000 < r.Mother * 10000
            && Stories.Open(town, "dragon-mother") != null)
            e.MotherCame = true;
    }

    /// <summary>What the town's young or grown dragon does for it, or null with none.</summary>
    public static DragonAidDef? Aid(Town town) => town.Egg switch
    {
        { Grown: true } => Rules(town).Grown,
        { HatchedDay: not null } => Rules(town).Hatchling,
        _ => null,
    };

    public static double Defence(Town town) => Aid(town)?.Defence ?? 0;

    public static double Mood(Town town) => Aid(town)?.Mood ?? 0;

    /// <summary>The town's dragon in a phrase, for cards and the inspector: "Cinder, the town's red dragon".</summary>
    public static string Describe(Town town)
    {
        if (town.Egg is not { } e || Kind(town, e.Kind) is not { } k) return "";
        if (e.Grown) return $"{e.Name}, {town.Name}'s {k.Name}";
        if (e.HatchedDay != null) return $"{e.Name}, a {k.Name} wyrmling";
        return e.Kept ? $"a {k.Name}'s egg, kept warm in the keep" : $"a {k.Name}'s egg";
    }

    private static string Fill(Town town, string text, DragonKindDef kind, string site, string party, string name = "") => Saga.Cap(text
        .Replace("{kind}", kind.Name, StringComparison.Ordinal)
        .Replace("{gems}", kind.Gems, StringComparison.Ordinal)
        .Replace("{site}", site, StringComparison.Ordinal)
        .Replace("{party}", party, StringComparison.Ordinal)
        .Replace("{name}", name, StringComparison.Ordinal)
        .Replace("{town}", town.Name, StringComparison.Ordinal));
}
