namespace CrownAndCompass.Sim.Systems;

/// <summary>A feat a hero may take in place of raising a score (content/game/advances.json).</summary>
public sealed record HeroFeatDef
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> Classes { get; init; } = [];
    public List<string> Cards { get; init; } = [];

    /// <summary>Only for classes that cast.</summary>
    public bool Spell { get; init; }
    public int HpPerLevel { get; init; }
    public int Attack { get; init; }
    public int Ac { get; init; }
    public int Skill { get; init; }
    public int Initiative { get; init; }
    public int Concentration { get; init; }
}

public sealed record AdvanceRules
{
    public List<int> Levels { get; init; } = [];
    public Dictionary<string, List<int>> Extra { get; init; } = [];
    public int Cap { get; init; } = 20;
    public int KeyBelow { get; init; } = 20;
    public List<HeroFeatDef> Feats { get; init; } = [];

    public HeroFeatDef? Feat(string id) => Feats.FirstOrDefault(f => f.Id == id);
}

/// <summary>
/// Heroes grow as they level: at 4, 8, 12, 16 and 19 (fighters and rogues more often) they raise their key ability
/// by two until it is high, then take a feat. No dice: the choice follows the rules, so a sheet always reads the same.
/// </summary>
public static class Advances
{
    private static AdvanceRules Rules(Town town) => town.Content.Advances;

    /// <summary>The ability a hero of the class leans on: the casting one for casters and healers, else its key ability.</summary>
    public static string Key(ClassDef c) => (c.Spell || c.Card == "healer") && c.Casting != "" ? c.Casting : c.Primary;

    /// <summary>How many advances a hero of this class has earned by this level.</summary>
    public static int Due(Town town, ClassDef c, int level)
    {
        var r = Rules(town);
        return r.Levels.Concat(r.Extra.GetValueOrDefault(c.Id, [])).Count(l => level >= l);
    }

    public static IEnumerable<HeroFeatDef> Feats(Content content, HeroSheet s) => s.Feats.Select(content.Advances.Feat).OfType<HeroFeatDef>();

    public static int Attack(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.Attack);
    public static int Ac(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.Ac);
    public static int Skill(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.Skill);
    public static int Initiative(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.Initiative);
    public static int Concentration(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.Concentration);
    public static int HpPerLevel(Content content, HeroSheet s) => Feats(content, s).Sum(f => f.HpPerLevel);

    /// <summary>Spend every advance the hero has earned and not yet taken; returns what they took, for the Ledger.</summary>
    public static List<string> Catch(Town town, Agent a)
    {
        var s = a.Hero!;
        var r = Rules(town);
        var took = new List<string>();
        while (s.Advances < Due(town, s.Cls, s.Level))
        {
            s.Advances++;
            string key = Key(s.Cls);
            if (a.Abilities[key] < Math.Min(r.KeyBelow, r.Cap))
            {
                Raise(a, key, 2, r.Cap);
                took.Add($"{Ability.Name(key)} +2");
                continue;
            }
            if (r.Feats.FirstOrDefault(f => Fits(s, f) && !s.Feats.Contains(f.Id)) is { } feat)
            {
                s.Feats.Add(feat.Id);
                took.Add(feat.Name);
                continue;
            }
            Raise(a, Ability.Con, 2, r.Cap);
            took.Add($"{Ability.Name(Ability.Con)} +2");
        }
        if (took.Count > 0)
        {
            int before = s.MaxHp;
            s.MaxHp = Expedition.HeroMaxHp(town.Content, s, a.Abilities.Con);
            s.Hp = Math.Max(1, s.Hp + s.MaxHp - before);
        }
        return took;
    }

    private static void Raise(Agent a, string ability, int by, int cap) => a.Abilities[ability] = Math.Min(cap, a.Abilities[ability] + by);

    public static bool Fits(HeroSheet s, HeroFeatDef f) =>
        (f.Classes.Count == 0 || f.Classes.Contains(s.Cls.Id)) && (f.Cards.Count == 0 || f.Cards.Contains(s.Cls.Card)) && (!f.Spell || s.Cls.Spell || s.Cls.Card == "healer");

    /// <summary>Every hero catches up on advances they are owed (arrivals above level 1, saves from before advances).</summary>
    public static void Daily(Town town)
    {
        foreach (var a in town.Heroes.OrderBy(h => h.Id))
            if (a.Hero!.Advances < Due(town, a.Hero.Cls, a.Hero.Level)) Catch(town, a);
    }
}
