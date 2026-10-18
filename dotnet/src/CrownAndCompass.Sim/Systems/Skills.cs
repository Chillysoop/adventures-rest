namespace CrownAndCompass.Sim.Systems;

/// <summary>Townsfolk and heroes' skills, and the everyday checks they roll (content/game/skills.json).</summary>
public sealed record SkillRules
{
    public TownsfolkSkills Townsfolk { get; init; } = new();
    public HeroSkills Heroes { get; init; } = new();

    /// <summary>The skills each trade is trained in.</summary>
    public Dictionary<string, List<string>> Trades { get; init; } = [];

    /// <summary>The skills each hero class is trained in, and any it is expert in.</summary>
    public Dictionary<string, ClassSkills> Classes { get; init; } = [];

    /// <summary>The skills a hero background trains, where the library background doesn't say.</summary>
    public Dictionary<string, List<string>> Backgrounds { get; init; } = [];

    /// <summary>The watch's bonus at the skills it rolls (the watch are a count, not residents).</summary>
    public Dictionary<string, int> Watchman { get; init; } = [];

    /// <summary>How many recent checks the town keeps for the client's dice.</summary>
    public int Ring { get; init; } = 64;

    public Dictionary<string, CheckDef> Checks { get; init; } = [];

    public CheckDef Check(string kind) => Checks.TryGetValue(kind, out var c) ? c : new CheckDef();
}

public sealed record TownsfolkSkills
{
    /// <summary>Proficiency at their trade's skills (and a knack), before practice.</summary>
    public int Proficiency { get; init; } = 2;

    /// <summary>Successful checks at their trade for each +1 practice adds, up to the max.</summary>
    public int PracticeStep { get; init; } = 40;
    public int PracticeMax { get; init; } = 2;

    /// <summary>One townsperson in this many has a knack: one more skill, outside their trade.</summary>
    public int KnackEvery { get; init; } = 4;

    /// <summary>The kinds of check that count as practice at a trade (idle talk doesn't).</summary>
    public List<string> Practice { get; init; } = [];
}

public sealed record HeroSkills
{
    /// <summary>Share of their proficiency heroes add to skills they aren't trained in: they have seen more of the world.</summary>
    public double Untrained { get; init; } = 0.5;
}

public sealed record ClassSkills
{
    public List<string> Skills { get; init; } = [];
    public List<string> Expertise { get; init; } = [];
}

/// <summary>One everyday check's numbers; each kind reads the ones it needs.</summary>
public sealed record CheckDef
{
    public string Skill { get; init; } = "";
    public int Dc { get; init; } = 12;
    public string Text { get; init; } = "";
    public double Chance { get; init; }
    public double Success { get; init; }
    public double Failure { get; init; }
    public double Critical { get; init; }
    public double Fumble { get; init; }
    public int Audience { get; init; }
    public double Mood { get; init; }
    public double FumbleMood { get; init; }
    public double Tip { get; init; }
    public double Poor { get; init; }
    public double Rest { get; init; }
    public List<int> Take { get; init; } = [];
    public int Caught { get; init; }
}

/// <summary>
/// A d20 someone just rolled, kept for the client to show over their head (and in their sheet).
/// AgentId is null when the watch rolled; BuildingId is where it happened, if anywhere.
/// </summary>
public sealed class SkillCheck
{
    public int Serial { get; init; }
    public int Tick { get; init; }
    public int? AgentId { get; init; }
    public int? TargetId { get; init; }
    public int? BuildingId { get; init; }
    public string Kind { get; init; } = "";
    public string Skill { get; init; } = "";
    public int Die { get; init; }
    public int Bonus { get; init; }
    public int Total => Die + Bonus;
    public int Dc { get; init; }
    public bool Success { get; init; }
    public bool Critical => Die == 20;
    public bool Fumble => Die == 1;
}

/// <summary>
/// The skills of a character sheet for everyone in town. Townsfolk are trained only in their trade's skills
/// (and one in four has a knack for one more), at a small proficiency that grows slowly with practice; heroes
/// are trained in their class's and background's skills at proficiency by level, expert in a few, and add
/// half their proficiency to everything else, so they outclass townsfolk at most things. Nothing is stored
/// per resident but a practice count: the rest comes from their trade, their id and their ability scores.
/// Everyday moments roll real checks: tavern talk, brawls, the barkeep and the watch stopping them, a healer
/// tending the loser, performers, pickpockets, thralls stealing past the watch, haggling over a business's
/// supplies and a craftsman's day at work. Each check is kept a while in <see cref="Town.Checks"/> for the dice.
/// </summary>
public static class Skills
{
    private static SkillRules Rules(Town town) => town.Content.Skills;

    public static IReadOnlyList<SkillDef> All(Town town) => town.Content.Stories.Skills;

    public static SkillDef? Def(Town town, string skill) => town.Content.Stories.Skill(skill);

    public static string Name(Town town, string skill) => Def(town, skill)?.Name ?? skill;

    // ---------- The sheet ----------

    public static int HeroProficiency(int level) => 2 + (Math.Max(1, level) - 1) / 4;

    /// <summary>The skills a townsperson's trade trains.</summary>
    public static IReadOnlyList<string> Trade(Town town, Agent a) =>
        a.Hero == null && Rules(town).Trades.TryGetValue(a.Profession, out var s) ? s : [];

    /// <summary>A townsperson's knack, picked by their id so it costs no dice and no storage; "" for none.</summary>
    public static string Knack(Town town, Agent a)
    {
        if (a.Hero != null) return "";
        var all = All(town);
        uint h = unchecked((uint)a.Id * 2654435761u);
        if (all.Count == 0 || h % (uint)Math.Max(1, Rules(town).Townsfolk.KnackEvery) != 0) return "";
        string k = all[(int)((h >> 8) % (uint)all.Count)].Id;
        return Trade(town, a).Contains(k) ? "" : k;
    }

    /// <summary>Practice a townsperson has put into their trade, as a bonus.</summary>
    public static int Practised(Town town, Agent a)
    {
        var t = Rules(town).Townsfolk;
        return Math.Min(t.PracticeMax, a.Practice / Math.Max(1, t.PracticeStep));
    }

    /// <summary>The skills a hero's class and background train.</summary>
    public static IEnumerable<string> HeroTrained(Town town, HeroSheet hs)
    {
        var r = Rules(town);
        if (r.Classes.TryGetValue(hs.Cls.Id, out var c))
            foreach (var s in c.Skills) yield return s;
        if (hs.Background == "") yield break;
        if (r.Backgrounds.TryGetValue(hs.Background, out var bg))
            foreach (var s in bg) yield return s;
        else if (town.Content.Library.Background(hs.Background) is { } lib)
            foreach (var s in lib.Skills) yield return s;
    }

    public static bool Trained(Town town, Agent a, string skill) =>
        a.Hero is HeroSheet hs ? HeroTrained(town, hs).Contains(skill) : Trade(town, a).Contains(skill) || Knack(town, a) == skill;

    public static bool Expert(Town town, Agent a, string skill) =>
        a.Hero is HeroSheet hs && Rules(town).Classes.TryGetValue(hs.Cls.Id, out var c) && c.Expertise.Contains(skill);

    /// <summary>What proficiency adds to a skill for them: a hero's by level (doubled where expert, half where untrained), a townsperson's at their trade.</summary>
    public static int Proficiency(Town town, Agent a, string skill)
    {
        if (a.Hero is HeroSheet hs)
        {
            int p = HeroProficiency(hs.Level);
            if (Expert(town, a, skill)) return 2 * p;
            return Trained(town, a, skill) ? p : (int)Math.Floor(p * Rules(town).Heroes.Untrained);
        }
        var t = Rules(town).Townsfolk;
        if (Trade(town, a).Contains(skill)) return t.Proficiency + Practised(town, a);
        return Knack(town, a) == skill ? t.Proficiency : 0;
    }

    public static string AbilityOf(Town town, string skill) => Def(town, skill)?.Ability is { Length: > 0 } ab ? ab : Ability.Cha;

    /// <summary>Their whole bonus at a skill: the ability's modifier and their proficiency.</summary>
    public static int Bonus(Town town, Agent a, string skill) =>
        Rng.Modifier(a.Abilities[AbilityOf(town, skill)]) + Proficiency(town, a, skill);

    /// <summary>What they notice (or see through) without trying: 10 + their bonus.</summary>
    public static int Passive(Town town, Agent a, string skill) => 10 + Bonus(town, a, skill);

    public static int WatchBonus(Town town, string skill) => Rules(town).Watchman.GetValueOrDefault(skill);

    // ---------- Checks ----------

    /// <summary>
    /// Roll a check: a d20 on the town's dice plus their bonus (and any extra) against a DC. A natural 20
    /// always succeeds and a natural 1 always fails. A townsperson's success at their trade is practice.
    /// A null roller is the watch.
    /// </summary>
    public static SkillCheck Roll(Town town, Agent? a, string skill, int dc, string kind, int? target = null, int? building = null, int extra = 0)
    {
        int die = town.Rng.D20();
        int bonus = (a != null ? Bonus(town, a, skill) : WatchBonus(town, skill)) + extra;
        bool ok = die == 20 || (die != 1 && die + bonus >= dc);
        if (ok && a != null) Practise(town, a, skill, kind);
        return Record(town, new SkillCheck
        {
            Serial = ++town.CheckSerial, Tick = town.Tick, AgentId = a?.Id, TargetId = target, BuildingId = building,
            Kind = kind, Skill = skill, Die = die, Bonus = bonus, Dc = dc, Success = ok,
        });
    }

    /// <summary>Two roll against each other; the first wins ties. Returns both checks, each against the other's total.</summary>
    public static (SkillCheck First, SkillCheck Second) Contest(Town town, Agent a, string skillA, Agent b, string skillB, string kind, int? building = null)
    {
        int da = town.Rng.D20(), db = town.Rng.D20();
        int ba = Bonus(town, a, skillA), bb = Bonus(town, b, skillB);
        bool first = da + ba >= db + bb;
        if (first) Practise(town, a, skillA, kind);
        else Practise(town, b, skillB, kind);
        var x = Record(town, new SkillCheck
        {
            Serial = ++town.CheckSerial, Tick = town.Tick, AgentId = a.Id, TargetId = b.Id, BuildingId = building,
            Kind = kind, Skill = skillA, Die = da, Bonus = ba, Dc = db + bb, Success = first,
        });
        var y = Record(town, new SkillCheck
        {
            Serial = ++town.CheckSerial, Tick = town.Tick, AgentId = b.Id, TargetId = a.Id, BuildingId = building,
            Kind = kind, Skill = skillB, Die = db, Bonus = bb, Dc = da + ba + 1, Success = !first,
        });
        return (x, y);
    }

    private static SkillCheck Record(Town town, SkillCheck c)
    {
        var ring = town.Checks;
        if (ring.Count >= Math.Max(1, Rules(town).Ring)) ring.RemoveAt(0);
        ring.Add(c);
        return c;
    }

    /// <summary>A success at their trade counts toward the next step of practice; the step is news.</summary>
    private static void Practise(Town town, Agent a, string skill, string kind)
    {
        if (a.Hero != null || !Rules(town).Townsfolk.Practice.Contains(kind) || !Trade(town, a).Contains(skill)) return;
        int before = Practised(town, a);
        a.Practice++;
        int after = Practised(town, a);
        if (after > before)
            town.Log(LedgerCategory.Social, Importance.Routine,
                $"{a.Name} has grown {(after >= Rules(town).Townsfolk.PracticeMax ? "masterful" : "practised")} as {Agent.Article(a.Profession)} {a.Profession}: {Name(town, skill)} {Sign(Bonus(town, a, skill))}.", [a.Id]);
    }

    public static string Sign(int m) => m >= 0 ? $"+{m}" : $"{m}";

    /// <summary>"Persuasion 17 vs DC 13", for the Ledger.</summary>
    public static string Said(Town town, SkillCheck c) =>
        $"{Name(town, c.Skill)} {(c.Critical ? "natural 20" : c.Fumble ? "natural 1" : c.Total.ToString())} vs {c.Dc}";

    /// <summary>The checks someone rolled lately, newest first.</summary>
    public static IEnumerable<SkillCheck> Recent(Town town, int agentId) => town.Checks.Where(c => c.AgentId == agentId).Reverse();

    // ---------- Everyday checks ----------

    /// <summary>A business's day at its craft: the best hand there rolls their trade, and the day's work comes out a little better or worse.</summary>
    public static void Daily(Town town)
    {
        var c = Rules(town).Check("craft");
        foreach (var b in town.Buildings)
        {
            b.KnackPct = 0;
            if (!b.Complete || b.Closed || b.ForSale || b.IsSite || b.Def.Produces.Count == 0 || b.Workers.Count == 0) continue;
            Agent? best = null;
            string skill = "";
            int bonus = int.MinValue;
            foreach (int wid in b.Workers)
            {
                var w = town.Agent(wid);
                if (w.Departed || w.IsAway) continue;
                var trade = Trade(town, w);
                string s = trade.Count > 0 ? trade[0] : "athletics";
                int bb = Bonus(town, w, s);
                if (bb > bonus) (best, skill, bonus) = (w, s, bb);
            }
            if (best == null) continue;
            var chk = Roll(town, best, skill, c.Dc, "craft", null, b.Id);
            double f = chk.Critical ? c.Critical : chk.Fumble ? c.Fumble : chk.Success ? c.Success : c.Failure;
            b.KnackPct = (int)Math.Round((f - 1) * 100);
        }
    }

    /// <summary>A business's yield for the day from its craft check.</summary>
    public static double Yield(Building b) => 1 + b.KnackPct / 100.0;

    /// <summary>Haggling over a business's supplies: what share of the price the buyer saves (less than none when the carter sees them coming).</summary>
    public static double Haggle(Town town, Agent buyer, Building b)
    {
        var c = Rules(town).Check("haggle");
        var chk = Roll(town, buyer, c.Skill, c.Dc, "haggle", null, b.Id);
        return chk.Critical ? c.Critical : chk.Fumble ? c.Fumble : chk.Success ? c.Success : c.Failure;
    }

    /// <summary>Someone takes up a song at the tavern: the best performer there tries, and the room warms to it, tips, or groans.</summary>
    public static void Perform(Town town, Building tavern, List<Agent> patrons)
    {
        var c = Rules(town).Check("perform");
        if (patrons.Count < Math.Max(2, c.Audience) || !town.Rng.Chance(c.Chance)) return;
        Agent? best = null;
        int bonus = int.MinValue;
        foreach (var p in patrons)
        {
            int bb = Bonus(town, p, c.Skill);
            if (bb > bonus) (best, bonus) = (p, bb);
        }
        var chk = Roll(town, best!, c.Skill, c.Dc, "perform", null, tavern.Id);
        if (!chk.Success && !chk.Fumble) return;
        double mood = chk.Fumble ? c.FumbleMood : chk.Critical ? 2 * c.Mood : c.Mood;
        double tips = 0;
        foreach (var p in patrons)
        {
            if (p.Id == best!.Id) continue;
            p.MoodToday += mood;
            if (!chk.Success) continue;
            double tip = Math.Min(Math.Max(0, p.Gold), c.Tip);
            p.Gold -= tip;
            tips += tip;
        }
        best!.Gold += tips;
        if (chk.Critical)
            town.Log(LedgerCategory.Social, Importance.Routine,
                $"{best.Name} brings the house down at {tavern.Name} ({Said(town, chk)}); the hat comes back with {Js.ToFixed(tips, 0)} gold in it.", [best.Id]);
    }

    /// <summary>A hard-up patron tries a purse at the tavern: Sleight of Hand against the mark's eye.</summary>
    public static void Pickpocket(Town town, Building tavern, List<Agent> patrons)
    {
        var c = Rules(town).Check("pickpocket");
        if (patrons.Count < 2) return;
        foreach (var p in patrons)
        {
            if (p.Hero != null || p.Gold >= c.Poor || !(p.Has(Trait.Greedy) || p.ThrallOf != null)) continue;
            if (!town.Rng.Chance(c.Chance)) continue;
            var marks = patrons.Where(x => x.Id != p.Id && x.Gold > c.Poor).ToList();
            if (marks.Count == 0) continue;
            var mark = town.Rng.Pick(marks);
            var chk = Roll(town, p, c.Skill, Passive(town, mark, "perception"), "pickpocket", mark.Id, tavern.Id);
            if (chk.Success)
            {
                double take = Math.Min(mark.Gold, town.Rng.Range(c.Take[0], c.Take[1] + 1));
                mark.Gold -= take;
                p.Gold += take;
                continue;
            }
            mark.ChangeOpinion(p.Id, c.Caught);
            town.Log(LedgerCategory.Brawl, Importance.Routine,
                $"{mark.Name} catches {p.Name}'s hand in their purse at {tavern.Name} ({Said(town, chk)}).", [mark.Id, p.Id]);
        }
    }

    /// <summary>Before a brawl: the barkeep or the owner, if there, may read it coming and show the troublemaker out. True if they did.</summary>
    public static bool Calm(Town town, Building tavern, Agent brawler)
    {
        var c = Rules(town).Check("calm");
        var staff = tavern.OwnerId is int o ? tavern.Workers.Append(o) : tavern.Workers;
        var keep = staff.Distinct().Select(town.Agent).Where(w => !w.Departed && w.Id != brawler.Id && w.LocationId == tavern.Id).OrderBy(w => w.Id).FirstOrDefault();
        if (keep == null) return false;
        var chk = Roll(town, keep, c.Skill, c.Dc, "calm", brawler.Id, tavern.Id);
        if (!chk.Success) return false;
        brawler.Activity = Activity.Relaxing;
        brawler.LocationId = brawler.HomeId;
        town.Log(LedgerCategory.Brawl, Importance.Routine,
            $"{keep.Name} {c.Text.Replace("{who}", brawler.Name)} at {tavern.Name} ({Said(town, chk)}).", [keep.Id, brawler.Id]);
        return true;
    }

    /// <summary>The watch, on hand, faces a brawler down: Intimidation against the DC. True if they back off.</summary>
    public static SkillCheck Intimidate(Town town, Building tavern, Agent brawler)
    {
        var c = Rules(town).Check("intimidate");
        return Roll(town, null, c.Skill, c.Dc, "intimidate", brawler.Id, tavern.Id);
    }

    /// <summary>A healer among the patrons sees to a brawl's loser, who comes round sooner. Returns the healer, or null.</summary>
    public static Agent? Tend(Town town, Building tavern, Agent hurt, IEnumerable<Agent> around)
    {
        var c = Rules(town).Check("tend");
        var healer = around.Where(x => x.Id != hurt.Id && !x.Departed && Trained(town, x, c.Skill)).OrderByDescending(x => Bonus(town, x, c.Skill)).ThenBy(x => x.Id).FirstOrDefault();
        if (healer == null) return null;
        var chk = Roll(town, healer, c.Skill, c.Dc, "tend", hurt.Id, tavern.Id);
        if (!chk.Success) return null;
        hurt.Fatigue = Math.Min(hurt.Fatigue, c.Rest);
        return healer;
    }

    /// <summary>A thrall slips coin from the strongbox past the watch: Stealth against the watch's eye. False if caught.</summary>
    public static bool SlipPast(Town town, Agent thrall)
    {
        if (town.Watch <= 0) return true;
        var c = Rules(town).Check("steal");
        var chk = Roll(town, thrall, c.Skill, 10 + WatchBonus(town, "perception"), "steal", null, Economy.Hall(town)?.Id);
        return chk.Success;
    }
}
