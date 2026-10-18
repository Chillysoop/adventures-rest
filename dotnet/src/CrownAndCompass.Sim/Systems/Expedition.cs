namespace CrownAndCompass.Sim.Systems;

public enum ExpeditionOutcome
{
    Cleared,
    Retreated,
    Wiped,
}

/// <summary>A magic item from the hoard: claimed by a hero, or (HeroId null) carried home for the town.</summary>
public sealed record ExpeditionItem(int? HeroId, LibItem Item);

public sealed record ExpeditionResult(
    ExpeditionOutcome Outcome,
    IReadOnlyList<string> Lines,
    IReadOnlyList<int> Dead,
    int Gold,
    int Xp,
    IReadOnlyList<ExpeditionItem> Items,
    OrderedMap<int, int> Kills)
{
    /// <summary>Each hero's hit points and potions when it was over; they go onto the sheets when the party gets home.</summary>
    public OrderedMap<int, int> Hp { get; init; } = new();
    public OrderedMap<int, int> Potions { get; init; } = new();

    /// <summary>The bounty's terms were met short of slaying: the occupants talked down, driven out, the captives cut loose, the hoard robbed.</summary>
    public bool Talked { get; init; }
    public bool Driven { get; init; }
    public bool GoalMet { get; init; }

    /// <summary>The site fell, though nobody lived to tell the town.</summary>
    public bool SiteCleared { get; init; }

    /// <summary>Hours from the start of the delve to the moment the last of them died, for a wiped party.</summary>
    public double? LostAt { get; init; }

    /// <summary>Put the heroes' hit points and potions where they ended up.</summary>
    public void Apply(IEnumerable<Agent> heroes)
    {
        foreach (var a in heroes)
        {
            if (Hp.ContainsKey(a.Id)) a.Hero!.Hp = Hp.Get(a.Id, a.Hero.Hp);
            if (Potions.ContainsKey(a.Id)) a.Hero!.Potions = Potions.Get(a.Id, a.Hero.Potions);
        }
    }
}

/// <summary>A stretch of time a party spent stopped near its site: camped outside, delving, or fallen back to rest.</summary>
public sealed record Halt(double From, double To, string Kind);

/// <summary>
/// The expedition resolver: a deterministic, seedable dice simulation of a party going into a
/// site, encounter by encounter. It never touches the town; it returns what happened and a
/// Chronicle written from the logged rolls, and the caller applies the results.
/// </summary>
public static class Expedition
{
    internal sealed class Combatant(Agent agent)
    {
        public Agent Agent { get; } = agent;
        public HeroSheet Sheet { get; } = agent.Hero!;
        public bool Down { get; set; }
        public bool Dead { get; set; }
        public int Heals { get; set; } = agent.Hero!.Cls.HealsAt(agent.Hero.Level);

        /// <summary>Kept apart from the sheet until the party is home, so the town learns nothing early.</summary>
        public int Hp { get; set; } = agent.Hero!.Hp;
        public int Potions { get; set; } = agent.Hero!.Potions;

        /// <summary>Hit dice left to spend on short rests.</summary>
        public int HitDice { get; set; } = agent.Hero!.Level;

        /// <summary>From the hero's gear, worked out once for the delve.</summary>
        public int Ac { get; init; }
        public int Attack { get; set; }
        public int Skill { get; set; }

        /// <summary>Been out often enough to keep their head when a friend falls.</summary>
        public bool Veteran { get; init; }
    }

    private sealed class Foe(MonsterDef def)
    {
        public MonsterDef Def { get; } = def;
        public int Hp { get; set; } = def.Hp;
    }

    private sealed record Check(Combatant Who, int Roll, int Total, string With = "")
    {
        /// <summary>The hero's name, with the friend or rival who swayed the roll.</summary>
        public string Name => With == "" ? Who.Agent.GivenName : $"{Who.Agent.GivenName}, {With},";
    }

    private static readonly Dictionary<string, string> SkillAbility = new()
    {
        [SkillKind.Trap] = Ability.Dex,
        [SkillKind.Hazard] = Ability.Con,
        [SkillKind.Puzzle] = Ability.Int,
        [SkillKind.Parley] = Ability.Cha,
        [SkillKind.Perception] = Ability.Wis,
    };

    /// <summary>'the goblin', but just 'Grukk Bloodtusk' for a named foe.</summary>
    private static string The(MonsterDef d) => d.Named ? d.Name : $"the {d.Name}";

    public static int Proficiency(int level) => 2 + (level - 1) / 4;

    /// <summary>
    /// A hero's bonus on a check of an ability away from a delve (over a sending stone): its modifier,
    /// plus proficiency and class training in the matching delving skill, plus the gear's.
    /// </summary>
    public static int HeroBonus(Content content, Agent a, string ability, string kind)
    {
        var sheet = a.Hero!;
        int cls = kind == "" ? 0 : sheet.Cls.Skills.GetValueOrDefault(kind, 0);
        return (ability == "" ? 0 : Rng.Modifier(a.Abilities[ability])) + cls + (cls > 0 ? Proficiency(sheet.Level) : 0) + Gear.Skill(content, sheet);
    }

    /// <remarks>Integer division matches the TS sim because hit dice are even (d6 to d12).</remarks>
    public static int HeroMaxHp(HeroSheet sheet, int con)
    {
        int die = sheet.Cls.HitDie;
        return Math.Max(1, die + Rng.Modifier(con) + (sheet.Level - 1) * Math.Max(1, die / 2 + 1 + Rng.Modifier(con)));
    }


    private static string Sign(int n) => n >= 0 ? $"+{n}" : $"-{-n}";

    /// <summary>
    /// One party's venture: the road, its camps and rests, the site itself. It keeps its own clock (in
    /// ticks) and its own record of hit points, and hands back what happened as a result.
    /// </summary>
    public sealed class Venture
    {
        public readonly List<string> Lines = [];
        internal readonly List<Combatant> Party;
        public readonly OrderedMap<int, int> Kills = new();
        public int Gold;
        public int Swell;
        public int Xp;
        private bool _skipNextCombat;
        private readonly Rng _rng;
        private readonly Content _content;
        private readonly SiteDef? _site;

        /// <summary>The tick it is for the party: time passes in fights, rests and camps.</summary>
        public double Clock;

        /// <summary>Times stopped near the site (fallen back to rest), for the map.</summary>
        public readonly List<Halt> Halts = [];

        /// <summary>How much sooner than usual they break off a fight: on the road, and on a second try.</summary>
        private double _caution;

        /// <summary>Share of the last fight's foes' hit points still standing when it ended.</summary>
        public double FoeShare;

        /// <summary>Short rests taken since the last long one.</summary>
        private int _shortRests;

        /// <summary>When the last of the party died, if it did.</summary>
        public double? LostAt;

        /// <summary>The boss of the site is dead, whatever happens to them after.</summary>
        public bool SiteCleared;

        /// <summary>The bounty's terms (<see cref="Systems.Terms"/>): null or slay fights to the heart of the place.</summary>
        public TermDef? Terms;
        public bool Talked;
        public bool Driven;
        public bool GoalMet;
        public readonly List<ExpeditionItem> Items = [];

        public Venture(Rng rng, Content content, IReadOnlyList<Agent> heroes, SiteDef? site = null)
        {
            _rng = rng;
            _content = content;
            _site = site;
            Party = heroes.Select(agent => new Combatant(agent) { Ac = Gear.Ac(content, agent), Attack = Gear.Attack(content, agent), Skill = Gear.Skill(content, agent.Hero!), Veteran = agent.Hero!.Expeditions >= content.World.Companies.Veteran }).ToList();
            // Greenhorns with nobody seasoned among them lose their nerve sooner.
            if (Party.Any(c => c.Sheet.Expeditions == 0) && !Party.Any(c => c.Veteran)) _caution = content.Roads.Retreat.GreenCaution;
        }

        /// <summary>Traps and hazards hit harder in sites built for higher levels: one more set of dice every four levels.</summary>
        private int Scale => _site == null ? 1 : 1 + (Math.Max(1, _site.Tier) - 1) / 4;

        internal List<Combatant> Standing => Party.Where(c => !c.Down && !c.Dead).ToList();

        public void Log(string s) => Lines.Add(s);

        public double HpFraction()
        {
            int hp = 0;
            int max = 0;
            foreach (var c in Party)
            {
                if (c.Dead) continue;
                hp += Math.Max(0, c.Hp);
                max += c.Sheet.MaxHp;
            }
            return max == 0 ? 0 : (double)hp / max;
        }

        /// <summary>Best member makes the check for the party.</summary>
        private Check BestCheck(string kind)
        {
            Combatant? who = null;
            int best = 0;
            foreach (var c in Standing)
            {
                int bonus = SkillBonus(c, kind);
                if (who == null || bonus > best)
                {
                    who = c;
                    best = bonus;
                }
            }
            int roll = _rng.D20();
            // A friend beside them steadies the hand; a rival in the party spoils it.
            var (chem, with) = Regard.Chemistry(_content.Regard, who!.Agent, Standing.Select(c => c.Agent));
            return new Check(who, roll, roll + best + chem, with);
        }

        /// <summary>The mayor's word over the sending stone: a bonus (or a penalty) on every check and blow at the site.</summary>
        public void Hearten(int edge)
        {
            foreach (var c in Party)
            {
                c.Attack += edge;
                c.Skill += edge;
            }
        }

        private static int SkillBonus(Combatant c, string kind)
        {
            int ab = kind == SkillKind.Hazard ? Math.Max(c.Agent.Abilities.Con, c.Agent.Abilities.Wis) : c.Agent.Abilities[SkillAbility[kind]];
            int cls = c.Sheet.Cls.Skills.GetValueOrDefault(kind, 0);
            return Rng.Modifier(ab) + cls + (cls > 0 ? Proficiency(c.Sheet.Level) : 0) + c.Skill;
        }

        private static void Hurt(Combatant c, int dmg)
        {
            c.Hp -= dmg;
            if (c.Hp <= 0)
            {
                c.Hp = 0;
                c.Down = true;
            }
        }

        public void Encounter(EncounterDef e)
        {
            switch (e.Kind)
            {
                case "combat":
                    if (_skipNextCombat && !IsBoss(e))
                    {
                        _skipNextCombat = false;
                        Log($"Thanks to the help they found, the party slips past {e.Text}.");
                        return;
                    }
                    Combat(e, false);
                    return;
                case "wander":
                {
                    // Whatever the library says lives in this kind of country, met on the way in.
                    List<MonsterDef> pool = _site == null ? [] : _content.Wanderers(_site, e.Cr);
                    if (pool.Count == 0) return;
                    var def = _rng.Pick(pool);
                    int n = Math.Clamp(e.Budget / Math.Max(1, def.Xp), 1, 6);
                    Combat(new EncounterDef { Kind = "combat", Monster = def.Id, Count = [n, n], Text = e.Text }, false);
                    return;
                }
                case "trap":
                {
                    var check = BestCheck(SkillKind.Trap);
                    string who = check.Name;
                    if (check.Total >= e.Dc)
                    {
                        Log($"{Cap(e.Text)}: {who} spots it and disarms it (d20 {check.Roll}, {check.Total} vs DC {e.Dc}).");
                        Xp += 25;
                    }
                    else
                    {
                        var victim = _rng.Pick(Standing);
                        int dmg = 0;
                        for (int k = 0; k < Scale; k++) dmg += _rng.Roll(1, e.DamageDie) + _rng.Roll(1, e.DamageDie);
                        Hurt(victim, dmg);
                        Log($"{Cap(e.Text)}: {who} misses it (d20 {check.Roll}, {check.Total} vs DC {e.Dc}) and {victim.Agent.GivenName} takes {dmg} damage" +
                            (victim.Down ? " and drops." : "."));
                        if (victim.Down) AfterCombat();
                    }
                    return;
                }
                case "hazard":
                {
                    var hurt = new List<string>();
                    foreach (var c in Standing)
                    {
                        int roll = _rng.D20();
                        if (roll + SkillBonus(c, SkillKind.Hazard) < e.Dc)
                        {
                            int dmg = 0;
                            for (int k = 0; k < Scale; k++) dmg += _rng.Roll(1, e.DamageDie);
                            Hurt(c, dmg);
                            hurt.Add($"{c.Agent.GivenName} ({dmg})");
                        }
                    }
                    Log(hurt.Count == 0 ? $"{Cap(e.Text)}, but the party pushes through unscathed." : $"{Cap(e.Text)}. It costs them: {string.Join(", ", hurt)}.");
                    if (Party.Any(c => c.Down && !c.Dead)) AfterCombat();
                    return;
                }
                case "puzzle":
                case "parley":
                {
                    var check = BestCheck(e.Kind == "puzzle" ? SkillKind.Puzzle : SkillKind.Parley);
                    string who = check.Name;
                    if (check.Total >= e.Dc)
                    {
                        int gold = _rng.Range(e.Gold[0], e.Gold[1] + 1);
                        Gold += gold;
                        Xp += 50;
                        if (e.Kind == "parley") _skipNextCombat = true;
                        Log($"{Cap(e.Text)}. {who} handles it (d20 {check.Roll}, {check.Total} vs DC {e.Dc}): {gold} gold{(e.Kind == "parley" ? " and a safer path" : "")}.");
                    }
                    else
                    {
                        Log($"{Cap(e.Text)}. {who} gets nowhere (d20 {check.Roll}, {check.Total} vs DC {e.Dc}).");
                    }
                    return;
                }
                case "treasure":
                {
                    bool isMimic = _rng.Chance(e.MimicChance);
                    if (isMimic)
                    {
                        var check = BestCheck(SkillKind.Perception);
                        bool spotted = check.Total >= 13;
                        Log(spotted
                            ? $"{Cap(e.Text)}. {check.Name} notices the lid breathing (d20 {check.Roll}, {check.Total} vs DC 13). It's a mimic."
                            : $"{Cap(e.Text)}. Nobody notices the lid breathing (d20 {check.Roll}, {check.Total} vs DC 13) until it bites. It's a mimic!");
                        Combat(new EncounterDef { Kind = "combat", Monster = "mimic", Count = [1, 1], Text = "the mimic" }, !spotted);
                        if (Standing.Count == 0) return;
                    }
                    int gold = _rng.Range(e.Gold[0], e.Gold[1] + 1);
                    Gold += gold;
                    Log(isMimic ? $"Inside the mimic's gullet: {gold} gold." : $"{Cap(e.Text)}: {gold} gold.");
                    return;
                }
            }
        }

        private bool IsBoss(EncounterDef e) => e.Kind == "combat" && _content.Monster(e.Monster).Boss;

        public bool Combat(EncounterDef e, bool surprised)
        {
            var rng = _rng;
            var def = _content.Monster(e.Monster);
            var foes = new List<Foe>();
            int n = rng.Range(e.Count[0], e.Count[1] + 1);
            // Fewer foes than drawn never empties a fight that had any.
            if (!def.Boss) n = Math.Max(Math.Min(n, 1), n + Swell);
            for (int i = 0; i < n; i++) foes.Add(new Foe(def));
            if (!string.IsNullOrEmpty(e.Escort) && e.EscortCount != null)
            {
                var ed = _content.Monster(e.Escort);
                int en = rng.Range(e.EscortCount[0], e.EscortCount[1] + 1);
                for (int i = 0; i < en; i++) foes.Add(new Foe(ed));
            }
            string label = n == 1 ? (def.Named ? def.Name : $"{Article(def.Name)} {def.Name}") : $"{n} {def.Plural}";
            if (def.Id != "mimic") Log($"{Cap(e.Text)}: {label}{(foes.Count > n ? $", with {foes.Count - n} more at its side" : "")}.");

            int totalXp = foes.Sum(f => f.Def.Xp);
            int round = 0;
            bool monstersFirst = surprised;
            while (round < 20)
            {
                round++;
                if (!monstersFirst) HeroesAct(foes, round);
                monstersFirst = false;
                if (foes.All(f => f.Hp <= 0)) break;
                MonstersAct(foes);
                if (Standing.Count == 0) break;
                if (foes.Any(f => f.Hp > 0) && BreakOff(def.Boss))
                {
                    int left = foes.Sum(f => f.Def.Hp);
                    FoeShare = left == 0 ? 0 : foes.Sum(f => Math.Max(0, f.Hp)) / (double)left;
                    var fallen = Party.Where(c => c.Down && !c.Dead).Select(c => c.Agent.GivenName).ToList();
                    Log(fallen.Count == 0
                        ? "Bloodied and outmatched, the party breaks off and runs."
                        : $"Bloodied and outmatched, the party breaks off and runs, dragging {ListNames(fallen)} with them.");
                    AfterCombat();
                    return false;
                }
            }

            bool won = foes.All(f => f.Hp <= 0);
            int foeMax = foes.Sum(f => f.Def.Hp);
            FoeShare = foeMax == 0 ? 0 : foes.Sum(f => Math.Max(0, f.Hp)) / (double)foeMax;
            if (won)
            {
                Xp += totalXp;
                var hurt = Standing.Where(c => c.Hp < c.Sheet.MaxHp / 3.0).Select(c => $"{c.Agent.GivenName} ({c.Hp}/{c.Sheet.MaxHp} hp)").ToList();
                Log($"The fight is over in {round} round{(round == 1 ? "" : "s")}.{(hurt.Count > 0 ? $" Badly hurt: {string.Join(", ", hurt)}." : "")}");
            }
            AfterCombat();
            return won;
        }

        /// <summary>Too hurt, or too many friends down, to win this one.</summary>
        private bool BreakOff(bool boss)
        {
            var rules = _content.World.Delve;
            int living = Party.Count(c => !c.Dead);
            int down = Party.Count(c => c.Down && !c.Dead);
            return HpFraction() < (boss ? rules.BossBreakOff : rules.BreakOff) + _caution || (down > 0 && down >= rules.DownedShare * living);
        }

        /// <summary>A veteran's Wisdom (Medicine) check to stop a fallen friend's bleeding.</summary>
        private bool FirstAid(Combatant c)
        {
            int dc = _content.World.Delve.FirstAidDc;
            var medic = Standing.Where(h => h.Veteran).OrderByDescending(h => Rng.Modifier(h.Agent.Abilities.Wis)).ThenBy(h => h.Agent.Id).FirstOrDefault();
            if (dc <= 0 || medic == null) return false;
            int roll = _rng.D20();
            int total = roll + Rng.Modifier(medic.Agent.Abilities.Wis);
            if (total < dc) return false;
            c.Down = false;
            c.Hp = 1;
            Log($"{medic.Agent.GivenName} binds {c.Agent.GivenName}'s wounds and stops the bleeding (d20 {roll}, {total} vs DC {dc}).");
            return true;
        }

        private void HeroesAct(List<Foe> foes, int round)
        {
            var rng = _rng;
            foreach (var c in Standing)
            {
                var living = foes.Where(f => f.Hp > 0).ToList();
                if (living.Count == 0) return;
                var s = c.Sheet;
                string name = c.Agent.GivenName;

                // A badly hurt hero downs a potion (a bonus action) and fights on.
                if (c.Potions > 0 && c.Hp < s.MaxHp * 0.35)
                {
                    c.Potions--;
                    int drunk = Potion();
                    c.Hp = Math.Min(s.MaxHp, c.Hp + drunk);
                    Log($"{name} gulps a {PotionName()} (+{drunk} hp).");
                }

                // Clerics keep the party alive before they swing.
                if (s.Cls.Healer && c.Heals > 0)
                {
                    var patient = Party.Where(p => !p.Dead && p.Hp < p.Sheet.MaxHp * 0.4).OrderBy(p => p.Hp).FirstOrDefault();
                    if (patient != null)
                    {
                        var spell = s.Cls.HealSpell!;
                        int healed = (spell.Dice ?? new Dice(1, 8, 0)).Roll(rng) + Rng.Modifier(c.Agent.Abilities[s.Cls.Casting]);
                        bool wasDown = patient.Down;
                        patient.Hp = Math.Min(patient.Sheet.MaxHp, patient.Hp + Math.Max(1, healed));
                        patient.Down = false;
                        c.Heals--;
                        if (wasDown) Log($"{name} drags {patient.Agent.GivenName} back from the brink with {Library.Prose(spell.Name)} (+{Math.Max(1, healed)} hp).");
                        continue;
                    }
                }

                // Casters with an area spell open with it, cast at a higher slot as they level.
                if (s.Cls.Area && round == 1 && living.Count >= 2)
                {
                    var spell = s.Cls.AreaSpell!;
                    var targets = living.Take(3).ToList();
                    int dmg = spell.Dice!.Value.Roll(rng) + (s.Level - 1) * spell.Dice.Value.Die / 2;
                    int felled = 0;
                    foreach (var f in targets)
                    {
                        f.Hp -= dmg;
                        if (f.Hp <= 0) felled++;
                    }
                    Credit(c, felled);
                    Log($"{name} looses {Library.Prose(spell.Name)} into {targets.Count} of them for {dmg} damage{(felled > 0 ? $", dropping {felled}" : "")}.");
                    continue;
                }

                var target = living[0];
                int roll = rng.D20();
                int atk = c.Attack;
                if (roll == 1)
                {
                    if (rng.Chance(0.3)) Log($"{name} rolls a natural 1 and nearly loses {s.Cls.Fumble}.");
                    continue;
                }
                if (roll == 20 || roll + atk >= target.Def.Ac)
                {
                    int dmg = Gear.Damage(_content, rng, c.Agent, roll == 20);
                    if (s.Cls.Sneak && Standing.Count > 1) dmg += rng.Roll((int)Math.Ceiling(s.Level / 2.0), 6);
                    dmg = Math.Max(1, dmg);
                    target.Hp -= dmg;
                    if (target.Hp <= 0) Credit(c, 1);
                    if (roll == 20) Log($"Natural 20! {name} hits {The(target.Def)} for {dmg}{(target.Hp <= 0 ? " and it goes down" : "")}.");
                    else if (target.Hp <= 0 && target.Def.Boss) Log($"{name} lands the killing blow on {The(target.Def)} (d20 {roll}{Sign(atk)}, {dmg} damage).");
                }
            }
        }

        private int Potion() => Math.Max(1, (Sim.Dice.Parse(_content.Shrines.Potion.Dice) ?? new Dice(2, 4, 2)).Roll(_rng));

        private string PotionName() => Library.Prose(_content.Library.Item(_content.Shrines.Potion.Item)?.Name ?? "potion of healing");

        private void Credit(Combatant c, int n)
        {
            if (n > 0) Kills[c.Agent.Id] = Kills.Get(c.Agent.Id, 0) + n;
        }

        private void MonstersAct(List<Foe> foes)
        {
            var rng = _rng;
            foreach (var f in foes)
            {
                for (int k = 0; k < Math.Max(1, f.Def.Attacks); k++)
                {
                    if (f.Hp <= 0) break;
                    var targets = Standing;
                    if (targets.Count == 0) return;
                    var t = rng.Pick(targets);
                    int roll = rng.D20();
                    if (roll == 1) continue;
                    if (roll == 20 || roll + f.Def.Attack >= t.Ac)
                    {
                        int dmg = Math.Max(1, f.Def.Dice.Roll(rng, roll == 20));
                        Hurt(t, dmg);
                        if (t.Down) Log($"{Cap(The(f.Def))} {(roll == 20 ? "crits" : "hits")} {t.Agent.GivenName} for {dmg}. {t.Agent.GivenName} goes down.");
                    }
                }
            }
        }

        /// <summary>Anyone left at 0 hp makes death saves unless a cleric can reach them.</summary>
        public void AfterCombat()
        {
            foreach (var c in Party)
            {
                if (!c.Down || c.Dead) continue;
                var healer = Standing.FirstOrDefault(h => h.Sheet.Cls.Healer && h.Heals > 0);
                if (healer != null)
                {
                    healer.Heals--;
                    c.Down = false;
                    c.Hp = Math.Max(1, _rng.Roll(1, 8) + healer.Sheet.Level);
                    Log($"{healer.Agent.GivenName} stabilizes {c.Agent.GivenName}.");
                    continue;
                }
                if (Standing.Count == 0)
                {
                    c.Dead = true;
                    continue;
                }
                // No cleric to hand: a friend pours a potion down their throat.
                var donor = Standing.FirstOrDefault(h => h.Potions > 0);
                if (donor != null)
                {
                    donor.Potions--;
                    c.Down = false;
                    c.Hp = Potion();
                    Log($"{donor.Agent.GivenName} pours a {PotionName()} down {c.Agent.GivenName}'s throat.");
                    continue;
                }
                if (FirstAid(c)) continue;
                int ok = 0;
                int fail = 0;
                var rolls = new List<int>();
                while (ok < 3 && fail < 3)
                {
                    int r = _rng.D20();
                    rolls.Add(r);
                    if (r == 20) ok = 3;
                    else if (r == 1) fail += 2;
                    else if (r >= 10) ok++;
                    else fail++;
                }
                if (fail >= 3)
                {
                    c.Dead = true;
                    Log($"{c.Agent.GivenName} fails their death saves ({string.Join(", ", rolls)}) and dies.");
                }
                else
                {
                    c.Down = false;
                    c.Hp = 1;
                    Log($"{c.Agent.GivenName} pulls through on death saves ({string.Join(", ", rolls)}).");
                }
            }
        }

        public int Living => Party.Count(c => !c.Dead);
        public int Fallen => Party.Count(c => c.Dead);
        public bool Wiped => Standing.Count == 0;

        private RoadRules Roads => _content.Roads;

        /// <summary>Note when the party died out, the first time it does.</summary>
        private void MarkLost()
        {
            if (Wiped && LostAt == null) LostAt = Clock;
        }

        /// <summary>A long rest: a night's sleep heals them, gives back their spells and half their hit dice.</summary>
        public void LongRest()
        {
            foreach (var c in Party)
            {
                if (c.Dead) continue;
                c.Down = false;
                c.Hp = c.Sheet.MaxHp;
                c.Heals = c.Sheet.Cls.HealsAt(c.Sheet.Level);
                c.HitDice = Math.Min(c.Sheet.Level, c.HitDice + Math.Max(1, (int)(c.Sheet.Level * Roads.LongRest.HitDice)));
            }
            _shortRests = 0;
        }

        /// <summary>Spell slots spent on sendings since their last sleep come out of the healers' heals.</summary>
        public void SpendSlots(int slots)
        {
            foreach (var c in Party)
            {
                int spent = Math.Min(slots, c.Heals);
                c.Heals -= spent;
                slots -= spent;
            }
        }

        /// <summary>
        /// An hour's short rest, if they need one and have one left: each hurt hero spends hit dice
        /// (a roll of the hit die plus Constitution each) until back near full. Inside a site, the rest
        /// can draw a wandering band.
        /// </summary>
        public bool ShortRest(bool inSite)
        {
            var r = Roads.ShortRest;
            if (Wiped || _shortRests >= r.PerLongRest || HpFraction() >= r.Below) return false;
            _shortRests++;
            Clock += 1;
            Log(Text("shortRest"));
            foreach (var c in Standing)
            {
                while (c.HitDice > 0 && c.Hp < c.Sheet.MaxHp * 0.8)
                {
                    c.HitDice--;
                    c.Hp = Math.Min(c.Sheet.MaxHp, c.Hp + Math.Max(1, _rng.Roll(1, c.Sheet.Cls.HitDie) + Rng.Modifier(c.Agent.Abilities.Con)));
                }
            }
            if (inSite && _site != null && _rng.Chance(r.WanderChance))
            {
                var wander = _site.Encounters.FirstOrDefault(e => e.Kind == "wander");
                if (wander != null)
                {
                    Log(Text("shortRestInterrupted"));
                    Encounter(wander);
                    MarkLost();
                }
            }
            return true;
        }

        /// <summary>A night in camp on the road or outside a site: maybe trouble, then sleep.</summary>
        public void Camp(int tier, IReadOnlyList<string> habitats, string? text = null)
        {
            if (Wiped) return;
            var rules = Roads.Camp;
            Log(text ?? Text("camp"));
            if (_rng.Chance(rules.Ambush + rules.PerTier * tier))
            {
                var check = BestCheck(SkillKind.Perception);
                bool seen = check.Total >= rules.WatchDc + tier / 2;
                string watcher = check.Who.Agent.GivenName;
                Log(Text(seen ? "campAmbush" : "campSurprised").Replace("{watcher}", watcher, StringComparison.Ordinal));
                Wilds(tier, habitats, "they fight in the firelight", !seen);
                MarkLost();
            }
            if (!Wiped) LongRest();
        }

        /// <summary>Crossing hard country: each of them makes the check, and something may be waiting there.</summary>
        public void Cross(TerrainKindDef kind, string name, int tier)
        {
            if (Wiped) return;
            var hz = kind.Hazard;
            int dc = hz.Dc + tier / 2;
            int scale = 1 + (Math.Max(1, tier) - 1) / 4;
            var hurt = new List<string>();
            foreach (var c in Standing)
            {
                int roll = _rng.D20();
                if (roll + SkillBonus(c, hz.Skill) >= dc) continue;
                int dmg = 0;
                for (int k = 0; k < scale; k++) dmg += _rng.Roll(1, hz.Die);
                Hurt(c, dmg);
                hurt.Add($"{c.Agent.GivenName} ({dmg})");
            }
            string what = hz.Text.Count > 0 ? _rng.Pick(hz.Text) : $"the going in {name}";
            Log(hurt.Count == 0 ? $"In {name}, {what}, but they come through unhurt." : $"In {name}, {what}. It costs them: {string.Join(", ", hurt)}.");
            if (Party.Any(c => c.Down && !c.Dead)) AfterCombat();
            MarkLost();
            if (!Wiped && _rng.Chance(kind.Ambush))
            {
                Wilds(tier, kind.Habitats, kind.AmbushText.Count > 0 ? _rng.Pick(kind.AmbushText) : $"trouble in {name}", false);
                MarkLost();
            }
        }

        /// <summary>A fight with whatever roams this country, sized for its level.</summary>
        public void Wilds(int tier, IReadOnlyList<string> habitats, string text, bool surprised)
        {
            tier = Math.Max(1, tier);
            var probe = new SiteDef { Id = "wilds", Name = "the wilds", Tier = tier, Boss = new EncounterDef { Kind = "combat" }, Habitats = habitats };
            double[] cr = [Math.Max(0.125, tier / 8.0), Math.Max(0.5, tier / 2.0)];
            // Only what would attack travellers: nothing of good heart.
            bool Hostile(MonsterDef m) => !(_content.Library.Monster(m.Id)?.Alignment.Contains("good") ?? false);
            var pool = _content.Wanderers(probe, cr).Where(Hostile).ToList();
            if (pool.Count == 0) pool = _content.Wanderers(probe with { Habitats = ["grassland", "forest", "hills"] }, cr).Where(Hostile).ToList();
            if (pool.Count == 0) return;
            var def = _rng.Pick(pool);
            var budget = _content.World.Budget;
            int xp = budget.Length == 0 ? 50 * tier : budget[Math.Min(tier, budget.Length) - 1] / 2;
            int n = Math.Clamp(xp / Math.Max(1, def.Xp), 1, 5);
            int swell = Swell;
            double caution = _caution;
            // Nothing to win out here: they run sooner than they would in a lair.
            Swell = 0;
            _caution = Math.Max(_caution, Roads.Retreat.RoadCaution);
            Combat(new EncounterDef { Kind = "combat", Monster = def.Id, Count = [n, n], Text = text }, surprised);
            Swell = swell;
            _caution = caution;
        }

        private string Text(string key) =>
            Roads.Texts.TryGetValue(key, out var lines) && lines.Count > 0 ? _rng.Pick(lines) : "";

        private bool CanRetry(int retries)
        {
            var r = Roads.Retreat;
            bool green = !r.GreenRetry && Party.Any(c => c.Sheet.Expeditions == 0) && !Party.Any(c => c.Veteran);
            return retries > 0 && !green && Standing.Count >= r.MinStanding && Fallen * 2 < Party.Count;
        }

        /// <summary>Fall back out of the site, sleep until dawn, and go back in against defenders who are ready for them.</summary>
        private void FallBack()
        {
            string where = _site?.Name ?? "the place";
            Log(Text("fallBack").Replace("{site}", where, StringComparison.Ordinal));
            double from = Clock;
            Clock = Systems.Roads.WakeAfterRest(_content, Clock);
            Halts.Add(new Halt(from, Clock, "fallback"));
            LongRest();
            Swell += Roads.Retreat.Alerted;
            _caution += Roads.Retreat.Caution;
            Log(Text("tryAgain").Replace("{site}", Cap(where), StringComparison.Ordinal));
        }

        /// <summary>
        /// Into the site, encounter by encounter, with short rests between when they need them. A beaten
        /// party with a retry left falls back, sleeps and goes in again; the boss's death clears it.
        /// </summary>
        public ExpeditionOutcome Delve(SiteDef site, int retries)
        {
            // Draw this delve's encounters from the site's deck.
            var deck = site.Encounters.ToList();
            int count = Math.Min(deck.Count, 2 + site.Tier);
            var plan = new List<EncounterDef>();
            for (int i = 0; i < count; i++)
            {
                int k = _rng.Range(0, deck.Count);
                plan.Add(deck[k]);
                deck.RemoveAt(k);
            }

            double start = Clock;
            var outcome = ExpeditionOutcome.Retreated;
            int at = 0;
            bool beaten = false;
            // Talking them down: the party's best voice against the master of the place.
            if (Terms is { Dc: > 0 } talk)
            {
                var boss = _content.Monster(site.Boss.Monster);
                var check = BestCheck(SkillKind.Parley);
                int dc = talk.Dc + talk.DcPerTier * Math.Max(1, site.Tier);
                if (check.Total >= dc)
                {
                    Log($"{check.Name} goes in under a branch of truce and talks {The(boss)} round (d20 {check.Roll}, {check.Total} vs DC {dc}): they will quit {site.Name} by the next moon.");
                    Talked = true;
                    SiteCleared = true;
                    Gold += (int)(_rng.Range(site.Loot[0], site.Loot[1] + 1) * talk.Loot);
                    Halts.Insert(0, new Halt(start, Clock + 2, "delve"));
                    Clock += 2;
                    return ExpeditionOutcome.Cleared;
                }
                Log($"{check.Name} tries to talk {The(boss)} round (d20 {check.Roll}, {check.Total} vs DC {dc}), and it comes to blades.");
            }
            // A drive, a rescue or a raid on the hoard ends once the party is far enough in.
            int goal = Terms is { Rooms: > 0 } part ? Math.Max(1, (int)Math.Ceiling(plan.Count * part.Rooms)) : int.MaxValue;
            while (true)
            {
                bool boss = at == plan.Count;
                var e = boss ? site.Boss : plan[at];
                if (boss) Log("At last, the heart of the place.");
                Clock += 1;
                bool won = true;
                if (e.Kind == "combat") won = Combat(e, false);
                else Encounter(e);
                MarkLost();
                if (Wiped)
                {
                    outcome = ExpeditionOutcome.Wiped;
                    break;
                }
                if (boss && won)
                {
                    outcome = ExpeditionOutcome.Cleared;
                    break;
                }
                if (!won)
                {
                    // Driven off: rest and try this fight again if it was close, or go home.
                    if (CanRetry(retries) && FoeShare <= Roads.Retreat.FoeShare)
                    {
                        retries--;
                        beaten = true;
                        FallBack();
                        continue;
                    }
                    if (beaten) Log(Text("giveUp"));
                    break;
                }
                at++;
                if (at >= goal && at < plan.Count + 1)
                {
                    outcome = Reach(site);
                    break;
                }
                // Catch their breath before the next room.
                ShortRest(true);
                if (Wiped)
                {
                    outcome = ExpeditionOutcome.Wiped;
                    break;
                }
                // Know when to quit, or when to fall back and come again.
                var rules = _content.World.Delve;
                double hp = HpFraction();
                bool battered = hp < rules.TurnBack || Fallen * 2 >= Party.Count;
                if (!battered && hp < Roads.Retreat.TurnBack && CanRetry(retries) && _shortRests >= Roads.ShortRest.PerLongRest)
                {
                    retries--;
                    beaten = true;
                    FallBack();
                    continue;
                }
                if (battered)
                {
                    if (CanRetry(retries))
                    {
                        retries--;
                        beaten = true;
                        FallBack();
                        continue;
                    }
                    Log("Too battered to go on, the party turns back.");
                    break;
                }
            }
            Halts.Insert(0, new Halt(start, Clock, "delve"));

            if (outcome == ExpeditionOutcome.Cleared && !Driven)
            {
                SiteCleared = true;
                Hoard(site);
            }
            return outcome;
        }

        /// <summary>Far enough in for the bounty's terms: drive the rest out, cut the captives loose, or rob the hoard.</summary>
        private ExpeditionOutcome Reach(SiteDef site)
        {
            var t = Terms!;
            int loot = (int)(_rng.Range(site.Loot[0], site.Loot[1] + 1) * t.Loot);
            Gold += loot;
            if (t.Captives)
            {
                Log($"In the pens at the back they find the captives, cut them loose and slip out of {site.Name} with them before the alarm.");
                GoalMet = true;
                return ExpeditionOutcome.Retreated;
            }
            if (t.Item)
            {
                Log($"They find the hoard, take what they can carry{(loot > 0 ? $" ({loot} gold)" : "")} and get out of {site.Name} before the rest of it wakes.");
                Treasure(site, true);
                GoalMet = true;
                return ExpeditionOutcome.Retreated;
            }
            Log($"With half of {site.Name} taken, they set fire to the rest. What is left of its folk breaks and runs for the wilds{(loot > 0 ? $", leaving {loot} gold behind" : "")}.");
            Driven = true;
            SiteCleared = true;
            return ExpeditionOutcome.Cleared;
        }

        private void Hoard(SiteDef site)
        {
            int loot = _rng.Range(site.Loot[0], site.Loot[1] + 1);
            Gold += loot;
            string line = $"{Cap(site.Name)} is cleared. The hoard holds {loot} gold";
            line += Treasure(site, false);
            Log($"{line}.");
        }

        /// <summary>The magic item in a hoard, if there is one (always, when sure), and who takes it. Returns the words for the Ledger line.</summary>
        private string Treasure(SiteDef site, bool sure)
        {
            string line = "";
            // Item tiers stop at 5 (legendary); sites for higher levels draw from the top of the list.
            int itemTier = Math.Min(site.Tier, 5);
            var pool = _content.LootItems.Where(it => it.Tier >= itemTier && it.Tier <= itemTier + 1).ToList();
            if (pool.Count > 0 && (sure || _rng.Chance(_content.Loot.ItemChancePerTier * site.Tier)))
            {
                var item = _rng.Pick(pool);
                if (item.Slot == "town")
                {
                    // Too big or too strange for one hero: it goes to the town.
                    Items.Add(new ExpeditionItem(null, item));
                    line += $" and the {item.Name}, which they haul home for the town";
                }
                else
                {
                    // Whoever gains most from it claims it; if nobody does, the one with least takes it to sell.
                    var who = Party
                        .Where(c => !c.Dead)
                        .OrderByDescending(c => Math.Max(0, Gear.Gain(_content, c.Agent, item)))
                        .ThenBy(c => c.Sheet.Items.Count)
                        .ThenBy(c => c.Agent.Id)
                        .First();
                    Items.Add(new ExpeditionItem(who.Agent.Id, item));
                    line += $" and the {item.Name}, which {who.Agent.GivenName} claims";
                }
            }
            if (sure && line != "") Log($"They bring back{line[4..]}.");
            return line;
        }

        /// <summary>What happened, for the town to learn when (if) they come home.</summary>
        public ExpeditionResult Result(ExpeditionOutcome outcome, string siteName)
        {
            if (outcome == ExpeditionOutcome.Wiped) Log($"No one comes back from {siteName}.");
            var hp = new OrderedMap<int, int>();
            var potions = new OrderedMap<int, int>();
            foreach (var c in Party)
            {
                hp[c.Agent.Id] = c.Dead ? 0 : Math.Max(1, c.Hp);
                potions[c.Agent.Id] = c.Potions;
            }
            return new ExpeditionResult(
                outcome,
                Lines,
                Party.Where(c => c.Dead).Select(c => c.Agent.Id).ToList(),
                Gold,
                Xp,
                Items,
                Kills)
            {
                Hp = hp,
                Potions = potions,
                SiteCleared = SiteCleared,
                LostAt = LostAt,
                Talked = Talked,
                Driven = Driven,
                GoalMet = GoalMet,
            };
        }
    }

    private static string Article(string w) => "aeiou".Contains(w[0]) ? "an" : "a";

    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <summary>
    /// A fight or a delve settled on the spot (the battle at the walls, tests): the party goes in as it
    /// is, with no road and no second try, and its wounds go straight onto the sheets.
    /// </summary>
    /// <param name="slotsSpent">Spell slots the party spent on sending-stone messages; they come out of the healers' heals.</param>
    /// <param name="swell">Extra creatures in every fight but the boss's, from how strong the lair has grown.</param>
    /// <param name="terms">The bounty's terms, when they are not to slay (<see cref="Terms"/>).</param>
    public static ExpeditionResult Run(Rng rng, Content content, SiteDef site, IReadOnlyList<Agent> heroes, int slotsSpent = 0, int swell = 0, TermDef? terms = null)
    {
        var run = new Venture(rng, content, heroes, site) { Swell = swell, Terms = terms };
        run.SpendSlots(slotsSpent);
        run.Log($"{ListNames(heroes.Select(h => h.GivenName).ToList())} set out for {site.Name}.");
        var outcome = run.Delve(site, 0);
        var result = run.Result(outcome, site.Name);
        result.Apply(heroes);
        return result;
    }

    public static string ListNames(IReadOnlyList<string> names)
    {
        if (names.Count <= 1) return string.Concat(names);
        return $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }
}
