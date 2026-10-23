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
    public OrderedMap<int, int> Scrolls { get; init; } = new();
    public OrderedMap<int, int> Copied { get; init; } = new();

    /// <summary>Heroes who drank their draughts on this delve.</summary>
    public List<int> Drunk { get; init; } = [];

    /// <summary>The bounty's terms were met short of slaying: the occupants talked down, driven out, the captives cut loose, the hoard robbed.</summary>
    public bool Talked { get; init; }
    public bool Driven { get; init; }
    public bool GoalMet { get; init; }

    /// <summary>The site fell, though nobody lived to tell the town.</summary>
    public bool SiteCleared { get; init; }

    /// <summary>Hours from the start of the delve to the moment the last of them died, for a wiped party.</summary>
    public double? LostAt { get; init; }

    /// <summary>The moments worth telling at the tavern, pointing into <see cref="Lines"/>.</summary>
    public IReadOnlyList<TaleBeat> Beats { get; init; } = [];

    /// <summary>Mishaps and feats that may grow into running jokes (<see cref="Voices"/>).</summary>
    public IReadOnlyList<Gag> Gags { get; init; } = [];

    /// <summary>Every kind of creature they fought, in the order they met them (game or library ids), for the map to draw the fight.</summary>
    public IReadOnlyList<string> Fought { get; init; } = [];

    /// <summary>Heroes bought back from the veiled room, and the patron each now owes.</summary>
    public OrderedMap<int, string> Bargains { get; init; } = new();

    /// <summary>Put the heroes' hit points and potions where they ended up.</summary>
    public void Apply(IEnumerable<Agent> heroes)
    {
        foreach (var a in heroes)
        {
            if (Hp.ContainsKey(a.Id)) a.Hero!.Hp = Hp.Get(a.Id, a.Hero.Hp);
            if (Potions.ContainsKey(a.Id)) a.Hero!.Potions = Potions.Get(a.Id, a.Hero.Potions);
            if (Scrolls.ContainsKey(a.Id)) a.Hero!.Scrolls = Scrolls.Get(a.Id, a.Hero.Scrolls);
            if (Copied.ContainsKey(a.Id)) a.Hero!.Copied = Copied.Get(a.Id, a.Hero.Copied);
            if (Drunk.Contains(a.Id)) a.Hero!.Draughts.Clear();
        }
    }
}

/// <summary>
/// One moment of a chronicle worth telling at the tavern: what kind of moment, the line it is, the hero it is
/// about (0 for the party), and the d20 behind it (0 when nothing was rolled).
/// </summary>
public sealed record TaleBeat(int Line, string Kind, int HeroId = 0, int Roll = 0, int Total = 0, int Dc = 0);

/// <summary>A stretch of time a party spent stopped near its site: camped outside, delving, or fallen back to rest.</summary>
public sealed record Halt(double From, double To, string Kind);

/// <summary>
/// The expedition resolver: a deterministic, seedable dice simulation of a party going into a
/// site, encounter by encounter. It never touches the town; it returns what happened and a
/// Chronicle written from the logged rolls, and the caller applies the results.
/// </summary>
public static partial class Expedition
{
    internal sealed partial class Combatant(Agent agent)
    {
        public Agent Agent { get; } = agent;
        public HeroSheet Sheet { get; } = agent.Hero!;
        public bool Down { get; set; }
        public bool Dead { get; set; }
        public int Heals { get; set; } = agent.Hero!.Cls.HealsAt(agent.Hero.Level);

        /// <summary>Kept apart from the sheet until the party is home, so the town learns nothing early.</summary>
        public int Hp { get; set; } = agent.Hero!.Hp;
        public int Potions { get; set; } = agent.Hero!.Potions;
        public int Scrolls { get; set; } = agent.Hero!.Scrolls;
        public int Copied { get; set; } = agent.Hero!.Copied;

        /// <summary>Hit dice left to spend on short rests.</summary>
        public int HitDice { get; set; } = agent.Hero!.Level;

        /// <summary>From the hero's gear, worked out once for the delve.</summary>
        public int Ac { get; init; }
        public int Attack { get; set; }
        public int Skill { get; set; }

        /// <summary>Been out often enough to keep their head when a friend falls.</summary>
        public bool Veteran { get; init; }

        /// <summary>Hit points of the beast they ride that fights beside them (a snow bear), taken before their own.</summary>
        public int Beast { get; set; }
    }

    private sealed partial class Foe(MonsterDef def)
    {
        public MonsterDef Def { get; } = def;
        public int Hp { get; set; } = def.Hp;

        /// <summary>How its kind fights (<see cref="Tactics"/>).</summary>
        public TacticDef? Tactic { get; init; }

        /// <summary>The round it spent on a signature move or calling help, instead of attacking.</summary>
        public int Spent { get; set; }

        /// <summary>What its stat block lets it do (<see cref="Knacks"/>).</summary>
        public KnackSet Ways { get; init; } = KnackSet.None;

        /// <summary>Spent its power; got back up once; burst; said its piece about a knack.</summary>
        public bool Powered, Rose, Burst;
        public readonly HashSet<string> Told = [];
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
    public sealed partial class Venture
    {
        public readonly List<string> Lines = [];
        public readonly List<TaleBeat> Beats = [];
        public readonly List<string> Fought = [];
        public readonly List<Gag> Gags = [];
        internal readonly List<Combatant> Party;
        public readonly OrderedMap<int, int> Kills = new();

        /// <summary>Heroes bought back from the veiled room, and the patron they owe (<see cref="Patrons"/>).</summary>
        public readonly OrderedMap<int, string> Bargains = new();
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

        /// <summary>How far the alarm has risen on a heist or a rescue (<see cref="HeistRules"/>).</summary>
        public int Alarm;
        public readonly List<ExpeditionItem> Items = [];

        public Venture(Rng rng, Content content, IReadOnlyList<Agent> heroes, SiteDef? site = null)
        {
            _rng = rng;
            _content = content;
            _site = site;
            Party = heroes.Select(agent => new Combatant(agent) { Ac = Gear.Ac(content, agent) + Alchemy.WardAc(content, agent.Hero!, site) + Draughts.Edge(content, agent.Hero!).Ac, Attack = Gear.Attack(content, agent) + Draughts.Edge(content, agent.Hero!).Attack, Skill = Gear.Skill(content, agent.Hero!), Veteran = agent.Hero!.Expeditions >= content.World.Companies.Veteran, Beast = content.Passage.Mount(agent.Hero.Mount)?.Fights ?? 0, Powers = [.. Enchant.Worn(content, agent.Hero), .. Draughts.Powers(content, agent.Hero)] }).ToList();
            // Greenhorns with nobody seasoned among them lose their nerve sooner.
            if (Party.Any(c => c.Sheet.Expeditions == 0) && !Party.Any(c => c.Veteran)) _caution = content.Roads.Retreat.GreenCaution;
        }

        /// <summary>Traps and hazards hit harder in sites built for higher levels: one more set of dice every four levels.</summary>
        private int Scale => _site == null ? 1 : 1 + (Math.Max(1, _site.Tier) - 1) / 4;

        internal List<Combatant> Standing => Party.Where(c => !c.Down && !c.Dead).ToList();

        // A fight's holds on the party (<see cref="Knacks"/>): swallowed whole, snared (a penalty to hit), dazed (no turn).
        private readonly Dictionary<Combatant, (Foe By, int Until, int Dmg)> _held = [];
        private readonly Dictionary<Combatant, (int Until, int Hit)> _snared = [];
        private readonly Dictionary<Combatant, int> _dazed = [];

        private Foe NewFoe(MonsterDef d) => new(d) { Tactic = Tactics.Of(_content, d), Ways = Knacks.Of(_content, d) };

        /// <summary>Whoever carries draughts drinks them at the door; they last this delve.</summary>
        public void Drink()
        {
            foreach (var c in Party)
                if (c.Sheet.Draughts.Count > 0)
                    Log($"{c.Agent.GivenName} drinks {ListNames(c.Sheet.Draughts.Select(d => $"{Agent.Article(Library.Prose(d.Name))} {Library.Prose(d.Name)}").ToList())}.");
        }

        public void Log(string s) => Lines.Add(s.Length > 0 ? char.ToUpperInvariant(s[0]) + s[1..] : s);

        /// <summary>Mark the line just logged as a moment for the tavern tale.</summary>
        internal void Mark(string kind, Combatant? c = null, int roll = 0, int total = 0, int dc = 0) =>
            Beats.Add(new TaleBeat(Lines.Count - 1, kind, c?.Agent.Id ?? 0, roll, total, dc));

        private void Mark(string kind, Check check, int dc) => Mark(kind, check.Who, check.Roll, check.Total, dc);

        /// <summary>A stable number for where they are in the tale, for picking words without the town's dice.</summary>
        private uint Seed => Voices.Hash("venture", Lines.Count, Party[0].Agent.Id, (int)Clock);

        /// <summary>Somebody in the party says something, if the moment moves them to.</summary>
        internal void Speak(string moment, Combatant? about = null)
        {
            if (Voices.Say(_content, moment, Standing.Select(c => c.Agent).ToList(), about?.Agent, _site?.Name ?? "", Seed) is not { } said) return;
            Log(said.Text);
            Beats.Add(new TaleBeat(Lines.Count - 1, "say", said.Speaker));
        }

        /// <summary>A mishap or a feat that may become a running joke; the party teases a hero already named for it.</summary>
        private void Gag(Combatant c, string kind)
        {
            Gags.Add(new Gag(c.Agent.Id, kind));
            if (Voices.Tease(_content, kind, Standing.Select(x => x.Agent).ToList(), c.Agent, Seed) is { } tease)
            {
                Log(tease.Text);
                Beats.Add(new TaleBeat(Lines.Count - 1, "say", tease.Speaker));
            }
        }

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
            return Rng.Modifier(ab) + cls + (cls > 0 ? Proficiency(c.Sheet.Level) : 0) + c.Skill + Knack(c, kind);
        }

        private static void Hurt(Combatant c, int dmg)
        {
            int soak = Math.Min(c.Beast, dmg);
            c.Beast -= soak;
            c.Hp -= dmg - soak;
            if (c.Hp <= 0)
            {
                c.Hp = 0;
                c.Down = true;
            }
        }

        /// <summary>Kinds of trouble a caster has already solved their own way on this delve.</summary>
        private readonly HashSet<string> _waysUsed = [];

        /// <summary>
        /// A strong caster in the party gets them past this their own way, without a roll (once a delve for each kind):
        /// flies them over the gorge, sends an unseen servant at the trap, divines the riddle, charms the guards.
        /// </summary>
        private bool Way(EncounterDef e)
        {
            if (_waysUsed.Contains(e.Kind)) return false;
            foreach (var w in _content.Bounties.Ways)
            {
                if (w.Kind != e.Kind) continue;
                var who = Standing.Where(c => w.Classes.Contains(c.Sheet.Cls.Id) && c.Sheet.Level >= w.MinLevel).OrderByDescending(c => c.Sheet.Level).ThenBy(c => c.Agent.Id).FirstOrDefault();
                if (who == null) continue;
                _waysUsed.Add(e.Kind);
                Log(Cap(w.Line.Replace("{name}", who.Agent.GivenName).Replace("{class}", who.Sheet.Cls.Name.ToLowerInvariant()).Replace("{text}", e.Text)));
                Mark("way");
                Xp += e.Kind == "combat" ? 50 : 25;
                if (e.Kind == "parley") _skipNextCombat = true;
                if (e.Kind is "puzzle" or "parley" && e.Gold.Length > 1) Gold += _rng.Range(e.Gold[0], e.Gold[1] + 1);
                return true;
            }
            return false;
        }

        public void Encounter(EncounterDef e)
        {
            if (e.Kind is "hazard" or "trap" or "puzzle" or "parley" && Way(e)) return;
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
                        Mark("trap", check, e.Dc);
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
                        Mark("trap", check, e.Dc);
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
                    Mark("hazard");
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
                        Mark(e.Kind, check, e.Dc);
                    }
                    else
                    {
                        Log($"{Cap(e.Text)}. {who} gets nowhere (d20 {check.Roll}, {check.Total} vs DC {e.Dc}).");
                        Mark(e.Kind, check, e.Dc);
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
                    Mark("treasure");
                    Speak("treasure");
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
            for (int i = 0; i < n; i++) foes.Add(NewFoe(def));
            if (!Fought.Contains(def.Id)) Fought.Add(def.Id);
            if (!string.IsNullOrEmpty(e.Escort) && e.EscortCount != null)
            {
                var ed = _content.Monster(e.Escort);
                if (!Fought.Contains(ed.Id)) Fought.Add(ed.Id);
                int en = rng.Range(e.EscortCount[0], e.EscortCount[1] + 1);
                for (int i = 0; i < en; i++) foes.Add(NewFoe(ed));
            }
            string label = n == 1 ? (def.Named ? def.Name : $"{Article(def.Name)} {def.Name}") : $"{n} {def.Plural}";
            if (def.Id != "mimic") Log($"{Cap(e.Text)}: {label}{(foes.Count > n ? $", with {foes.Count - n} more at its side" : "")}.");
            Mark(def.Boss ? "boss" : "fight");
            if (def.Boss) Speak("boss");

            int totalXp = foes.Sum(f => f.Def.Xp);
            int round = 0;
            _held.Clear();
            _snared.Clear();
            _dazed.Clear();
            foreach (var c in Party) c.FightUsed.Clear();
            bool monstersFirst = surprised || Ambush(foes);
            if (def.Boss && Goad(foes[0])) HeroesAct(foes, 0);
            while (round < 20)
            {
                round++;
                _round = round;
                if (!monstersFirst) HeroesAct(foes, round);
                monstersFirst = false;
                if (foes.All(f => f.Hp <= 0)) break;
                if (Scatter(foes)) break;
                if (def.Boss) BossMoves(foes, round);
                MonstersAct(foes, round);
                if (Standing.Count == 0) break;
                if (foes.Any(f => f.Hp > 0) && BreakOff(def.Boss))
                {
                    int left = foes.Sum(f => f.Def.Hp);
                    FoeShare = left == 0 ? 0 : foes.Sum(f => Math.Max(0, f.Hp)) / (double)left;
                    var fallen = Party.Where(c => c.Down && !c.Dead).Select(c => c.Agent.GivenName).ToList();
                    Log(fallen.Count == 0
                        ? "Bloodied and outmatched, the party breaks off and runs."
                        : $"Bloodied and outmatched, the party breaks off and runs, dragging {ListNames(fallen)} with them.");
                    Mark("retreat");
                    Speak("retreat");
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
                string rounds = $"{round} round{(round == 1 ? "" : "s")}";
                Log($"{Voices.Variant(_content, "over", "The fight is over in {rounds}.", Seed, ("rounds", rounds))}{(hurt.Count > 0 ? $" Badly hurt: {string.Join(", ", hurt)}." : "")}");
                Mark("won");
                Speak("victory");
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
                // Swallowed whole, or frozen by a gaze or a roar: no turn.
                if (_held.ContainsKey(c) || (_dazed.TryGetValue(c, out int dazed) && round <= dazed)) continue;

                // A badly hurt hero downs a potion (a bonus action) and fights on.
                if (c.Potions > 0 && c.Hp < s.MaxHp * 0.35)
                {
                    c.Potions--;
                    int drunk = Potion();
                    c.Hp = Math.Min(s.MaxHp, c.Hp + drunk);
                    Log(Voices.Variant(_content, "potion", "{name} gulps a {potion} (+{hp} hp).", Seed, ("name", name), ("potion", PotionName()), ("hp", drunk.ToString())));
                    if (c.Potions == 0 && c.Sheet.Potions >= 3) Gag(c, "potion");
                }

                Regen(c);
                if (ItemAction(c, foes, living, round)) continue;

                // Clerics keep the party alive before they swing.
                if (s.Cls.Healer && c.Heals > 0 && !_hushed)
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
                        if (wasDown)
                        {
                            Log($"{name} drags {patient.Agent.GivenName} back from the brink with {Library.Prose(spell.Name)} (+{Math.Max(1, healed)} hp).");
                            Mark("heal", c);
                        }
                        continue;
                    }
                }

                // Casters with an area spell open with it, cast at a higher slot as they level.
                if (s.Cls.Area && round == 1 && living.Count >= 2 && !_hushed)
                {
                    var spell = s.Cls.AreaSpell!;
                    var targets = living.Take(3).ToList();
                    int dmg = spell.Dice!.Value.Roll(rng) + (s.Level - 1) * spell.Dice.Value.Die / 2 + c.Copied * _content.Shrines.Scroll.CopyBonus;
                    int felled = 0;
                    foreach (var f in targets)
                    {
                        f.Hp -= Spell(f, dmg);
                        Struck(f, foes);
                        if (f.Hp <= 0) felled++;
                    }
                    Credit(c, felled);
                    Log($"{name} looses {Library.Prose(spell.Name)} into {targets.Count} of them for {dmg} damage{(felled > 0 ? $", dropping {felled}" : "")}.");
                    Mark("spell", c);
                    continue;
                }

                // A caster with a scroll reads it when the foes crowd in or the master of the place shows itself.
                if (c.Scrolls > 0 && Scrolls.Reads(s.Cls) && !_hushed && (living.Count >= 3 || living.Any(f => f.Def.Boss)))
                {
                    var sr = _content.Shrines.Scroll;
                    c.Scrolls--;
                    var targets = living.OrderByDescending(f => f.Def.Boss).Take(sr.Targets).ToList();
                    int dmg = Math.Max(1, (Sim.Dice.Parse(sr.Dice) ?? new Dice(3, 6, 0)).Roll(rng));
                    int felled = 0;
                    foreach (var f in targets)
                    {
                        f.Hp -= Spell(f, dmg);
                        Struck(f, foes);
                        if (f.Hp <= 0) felled++;
                    }
                    Credit(c, felled);
                    Log($"{name} reads a spell scroll aloud and it burns away, striking {targets.Count} of them for {dmg} damage{(felled > 0 ? $", dropping {felled}" : "")}.");
                    continue;
                }

                var target = living[0];
                int roll = rng.D20();
                int atk = c.Attack;
                if (roll == 1)
                {
                    if (rng.Chance(0.3))
                    {
                        Log($"{name} rolls a natural 1 and nearly loses {s.Cls.Fumble}.");
                        Mark("fumble", c, 1);
                        if (c.Sheet.NicknameFor == "fumble") Gag(c, "fumble");
                        else
                        {
                            Gags.Add(new Gag(c.Agent.Id, "fumble"));
                            Speak("fumble", c);
                        }
                    }
                    continue;
                }
                int snare = _snared.TryGetValue(c, out var sn) && round <= sn.Until ? sn.Hit : 0;
                bool sure = false;
                if (roll == 20 || roll + atk - snare - (target.Ways["aura"]?.Hit ?? 0) >= target.Def.Ac + Cover(target, round) + target.Ways.Ac || (sure = Sure(c, target)))
                {
                    int dmg = Gear.Damage(_content, rng, c.Agent, roll == 20) + OnHit(c, target, roll == 20, foes);
                    if (s.Cls.Sneak && Standing.Count > 1) dmg += rng.Roll((int)Math.Ceiling(s.Level / 2.0), 6);
                    // A blade passes half through a ghost.
                    if (target.Ways["incorporeal"] is { } ghost)
                    {
                        dmg = (int)Math.Round(dmg * ghost.Share);
                        Tell(target, ghost);
                    }
                    dmg = Math.Max(1, dmg);
                    target.Hp -= dmg;
                    Drain(c, dmg);
                    Struck(target, foes);
                    if (target.Hp <= 0) Credit(c, 1);
                    if (!sure && roll != 20 && target.Hp > 0) Haste(c, target, foes, atk);
                    if (roll == 20)
                    {
                        Log($"Natural 20! {name} hits {The(target.Def)} for {dmg}{(target.Hp <= 0 ? " and it goes down" : "")}.");
                        Mark(target.Hp <= 0 && target.Def.Boss ? "kill" : "crit", c, 20, 20 + atk, target.Def.Ac);
                        if (c.Sheet.NicknameFor == "crit") Gag(c, "crit");
                        else
                        {
                            Gags.Add(new Gag(c.Agent.Id, "crit"));
                            Speak("crit", c);
                        }
                    }
                    else if (target.Hp <= 0 && target.Def.Boss)
                    {
                        Log($"{name} lands the killing blow on {The(target.Def)} (d20 {roll}{Sign(atk)}, {dmg} damage).");
                        Mark("kill", c, roll, roll + atk, target.Def.Ac);
                    }
                }
            }
        }

        private int Potion() => Math.Max(1, (Sim.Dice.Parse(_content.Shrines.Potion.Dice) ?? new Dice(2, 4, 2)).Roll(_rng));

        private string PotionName() => Library.Prose(_content.Library.Item(_content.Shrines.Potion.Item)?.Name ?? "potion of healing");

        private void Credit(Combatant c, int n)
        {
            if (n > 0) Kills[c.Agent.Id] = Kills.Get(c.Agent.Id, 0) + n;
        }

        /// <summary>Cover a foe's kind fights from in the first rounds.</summary>
        private static int Cover(Foe f, int round) => f.Tactic is { Cover: > 0 } t && round <= t.CoverRounds ? t.Cover : 0;

        /// <summary>A pack hunter hits harder with its packmates still standing.</summary>
        private static int Pack(Foe f, List<Foe> foes) =>
            f.Tactic is { Pack: > 0 } t ? Math.Min(t.Pack, foes.Count(o => o != f && o.Hp > 0 && o.Tactic == t)) : 0;

        /// <summary>
        /// Foes that run when hurt break once the fight has turned: what is left of them is under the share
        /// of their strength their kind will stand, and no boss holds them. Returns whether the field is clear.
        /// </summary>
        private bool Scatter(List<Foe> foes)
        {
            var living = foes.Where(f => f.Hp > 0).ToList();
            if (living.Count == 0 || living.Any(f => f.Def.Boss)) return false;
            var t = living[0].Tactic;
            if (t is not { Flee: > 0 } || living.Any(f => f.Tactic != t)) return false;
            var kin = foes.Where(f => f.Tactic == t).ToList();
            double left = kin.Sum(f => Math.Max(0, f.Hp)) / (double)Math.Max(1, kin.Sum(f => f.Def.Hp));
            if (left >= t.Flee) return false;
            foreach (var f in living) f.Hp = 0;
            if (t.Fled != "") Log(Cap(t.Fled.Replace("{foes}", living[0].Def.Plural)));
            return true;
        }

        /// <summary>A proud boss can be goaded into talking by the party's best talker, and the party closes in for free.</summary>
        private bool Goad(Foe boss)
        {
            if (boss.Tactic is not { Pride: > 0 } t) return false;
            var face = Standing.OrderByDescending(c => c.Agent.Abilities.Cha).ThenBy(c => c.Agent.Id).FirstOrDefault();
            if (face == null || face.Agent.Abilities.Cha < t.Pride) return false;
            if (_rng.D20() + Rng.Modifier(face.Agent.Abilities.Cha) < t.Pride) return false;
            Log(t.Goaded.Replace("{hero}", face.Agent.GivenName).Replace("{boss}", The(boss.Def)));
            return true;
        }

        /// <summary>A boss's signature attack on the whole party in round 2, and its minions called in round 3.</summary>
        private void BossMoves(List<Foe> foes, int round)
        {
            var boss = foes.FirstOrDefault(f => f.Def.Boss && f.Hp > 0);
            if (boss?.Tactic is not { } t) return;
            if (round == 2 && t.Signature != "")
            {
                // It spends its round on it: half a blow to everyone, and a quick hero dodges it.
                boss.Spent = round;
                var downed = new List<string>();
                foreach (var c in Standing)
                {
                    int dmg = Math.Max(1, boss.Def.Dice.Roll(_rng, false) / 2);
                    if (_rng.D20() + Rng.Modifier(c.Agent.Abilities.Dex) >= 10 + boss.Def.Attack / 2) continue;
                    Hurt(c, dmg);
                    if (c.Down) downed.Add(c.Agent.GivenName);
                }
                Log($"{Cap(t.Signature.Replace("{boss}", The(boss.Def)))}{(downed.Count > 0 ? $". {ListNames(downed)} {(downed.Count == 1 ? "goes" : "go")} down" : "")}.");
            }
            if (round == 3 && t.Minions != "")
            {
                boss.Spent = round;
                var m = _content.Monster(t.Minions);
                int n = _rng.Range(t.MinionCount[0], t.MinionCount[1] + 1);
                for (int i = 0; i < n; i++) foes.Add(NewFoe(m));
                if (t.Called != "") Log(Cap(t.Called.Replace("{boss}", The(boss.Def)).Replace("{n}", n.ToString())));
            }
        }

        /// <summary>A knack's line, said once a fight for each foe that has it.</summary>
        private void Tell(Foe f, KnackDef k, string hero = "")
        {
            if (k.Text == "" || !f.Told.Add(k.Id)) return;
            Log(Cap(Knacks.Fill(k.Text, The(f.Def), hero)));
        }

        /// <summary>What a spell or scroll does to a foe: halved by magic resistance, more on a swarm, nothing the first time a legend shrugs it off.</summary>
        private int Spell(Foe f, int dmg)
        {
            if (f.Ways["legendary"] is { } lr && !f.Told.Contains(lr.Id))
            {
                Tell(f, lr);
                return 0;
            }
            if (f.Ways["magic"] is { } mr) dmg = (int)Math.Round(dmg * mr.Share);
            if (f.Ways["swarm"] is { } sw) dmg = (int)Math.Round(dmg * sw.Share);
            return Math.Max(1, dmg);
        }

        /// <summary>After a blow lands on a foe: the relentless get up once; the bursting burst as they die.</summary>
        private void Struck(Foe f, List<Foe> foes)
        {
            if (f.Hp > 0) return;
            if (f.Ways["fortitude"] is { } fort && !f.Rose)
            {
                f.Rose = true;
                if (_rng.D20() >= fort.Dc)
                {
                    f.Hp = 1;
                    Tell(f, fort);
                    return;
                }
            }
            if (f.Ways["deathburst"] is { } burst && !f.Burst)
            {
                f.Burst = true;
                var laid = new List<string>();
                foreach (var c in Standing)
                {
                    if (_rng.D20() + Rng.Modifier(c.Agent.Abilities.Dex) >= burst.Dc) continue;
                    Hurt(c, Math.Max(1, (int)Math.Round(f.Def.Dice.Roll(_rng, false) * burst.Share)));
                    if (c.Down) laid.Add(c.Agent.GivenName);
                }
                Log(Cap(Knacks.Fill(burst.Text, The(f.Def))) + (laid.Count > 0 ? " " + Laid(burst.Laid, laid) : ""));
            }
        }

        private static string Laid(string text, List<string> names) =>
            text.Replace("{heroes}", ListNames(names)).Replace("{go}", names.Count == 1 ? "goes" : "go").Replace("{is}", names.Count == 1 ? "is" : "are");

        /// <summary>A foe that lies in wait strikes first unless the party's sharpest eyes see it.</summary>
        private bool Ambush(List<Foe> foes)
        {
            var lurker = foes.FirstOrDefault(f => f.Ways.Has("ambush"));
            if (lurker == null) return false;
            if (Warned(lurker)) return false;
            var k = lurker.Ways["ambush"]!;
            var check = BestCheck(SkillKind.Perception);
            if (check.Total >= k.Dc)
            {
                Log(Cap(Knacks.Fill(k.Spotted, The(lurker.Def), check.Name)));
                return false;
            }
            Tell(lurker, k);
            return true;
        }

        /// <summary>
        /// A foe's power (a breath, a gaze, a roar), once a fight: everyone in it, or one hero, saves with the ability it
        /// names against its DC. A failure takes its damage and its hold (frozen a turn, or shaken and hard put to hit).
        /// </summary>
        private void UsePower(Foe f, int round)
        {
            var k = f.Ways["power"]!;
            var p = f.Ways.Power!;
            f.Powered = true;
            f.Spent = round;
            var targets = p.Area ? Standing : [_rng.Pick(Standing)];
            if (Reflect(f, targets, p.Name, p.Dice)) return;
            List<string> laid = [], held = [], shaken = [];
            foreach (var c in targets)
            {
                bool saved = _rng.D20() + Rng.Modifier(c.Agent.Abilities[p.Save]) >= p.Dc;
                if (p.Dice is { } d)
                {
                    int dmg = Math.Max(1, (int)Math.Round(d.Roll(_rng) * k.Share * (saved ? 0.5 : 1) * Resist(c, p.Name)));
                    Hurt(c, dmg);
                    if (c.Down)
                    {
                        laid.Add(c.Agent.GivenName);
                        continue;
                    }
                }
                if (saved || p.Condition == "" || Free(c)) continue;
                if (p.Condition is "paralyzed" or "stunned" or "incapacitated" or "petrified" or "unconscious")
                {
                    _dazed[c] = round + k.Rounds;
                    held.Add(c.Agent.GivenName);
                }
                else
                {
                    _snared[c] = (round + k.Rounds, k.Hit);
                    shaken.Add(c.Agent.GivenName);
                }
            }
            string line = Cap(Knacks.Fill(k.Text, The(f.Def), "", $"its {p.Name.ToLowerInvariant()}"));
            if (laid.Count > 0) line += " " + Laid(k.Laid, laid);
            if (held.Count > 0) line += " " + Laid(k.Held, held);
            if (shaken.Count > 0) line += " " + Laid(k.Shaken, shaken);
            if (laid.Count + held.Count + shaken.Count == 0 && k.Freed != "") line += " " + k.Freed;
            Log(line);
            Mark("power");
        }

        private void MonstersAct(List<Foe> foes, int round)
        {
            var rng = _rng;
            // The swallowed are digested, until their swallower dies or they cut their way out.
            foreach (var c in Party.Where(_held.ContainsKey).ToList())
            {
                var (by, until, chew) = _held[c];
                if (by.Hp <= 0 || round > until || c.Down || c.Dead)
                {
                    _held.Remove(c);
                    if (by.Hp > 0 && !c.Down && by.Ways["swallow"] is { Freed.Length: > 0 } sw) Log(Cap(Knacks.Fill(sw.Freed, The(by.Def), c.Agent.GivenName)));
                    continue;
                }
                Hurt(c, chew);
            }
            int powers = 0;
            foreach (var f in foes.ToList())
            {
                if (f.Hp <= 0 || f.Spent == round || f.HeldUntil >= round) continue;
                if (f.Ways["regen"] is { } regen && f.Hp < f.Def.Hp)
                {
                    f.Hp = Math.Min(f.Def.Hp, f.Hp + Math.Max(1, (int)Math.Round(f.Def.Hp * regen.Share)));
                    Tell(f, regen);
                }
                // The power on its first turn, or its second for every other one of a crowd, a few of them a fight.
                if (f.Ways.Power != null && !f.Powered && Standing.Count > 0 && powers < f.Ways["power"]!.Most && round == (f.Def.Boss ? 1 : 1 + foes.IndexOf(f) % 2))
                {
                    powers++;
                    UsePower(f, round);
                    continue;
                }
            }
            foreach (var f in foes.ToList())
            {
                int pack = Pack(f, foes);
                if (f.Ways["pack"] is { } pk && foes.Any(o => o != f && o.Hp > 0)) pack = Math.Max(pack, pk.Hit);
                for (int k = 0; k < Math.Max(1, f.Def.Attacks); k++)
                {
                    if (f.Hp <= 0 || f.Spent == round || f.HeldUntil >= round) break;
                    var targets = Standing;
                    if (targets.Count == 0) return;
                    var t = rng.Pick(targets);
                    int frenzy = 0;
                    // A frenzied beast goes for whoever is bleeding most.
                    if (f.Ways["frenzy"] is { } fz && targets.Where(c => c.Hp < c.Sheet.MaxHp / 2).OrderBy(c => c.Hp).ThenBy(c => c.Agent.Id).FirstOrDefault() is { } bleeding)
                    {
                        t = bleeding;
                        frenzy = fz.Hit;
                        Tell(f, fz, t.Agent.GivenName);
                    }
                    int roll = rng.D20();
                    if (roll == 1) continue;
                    int caught = _snared.TryGetValue(t, out var sn) && round <= sn.Until ? 2 : 0;
                    if (roll == 20 || roll + f.Def.Attack + pack + frenzy + caught >= t.Ac)
                    {
                        int dmg = Math.Max(1, f.Def.Dice.Roll(rng, roll == 20));
                        if (round == 1 && f.Ways["charge"] is { } charge)
                        {
                            dmg += (int)Math.Round(dmg * charge.Share);
                            Tell(f, charge, t.Agent.GivenName);
                        }
                        dmg = Guard(t, f, dmg);
                        Hurt(t, dmg);
                        Rebuke(t, f, round);
                        if (f.Ways["drain"] is { } drain)
                        {
                            f.Hp = Math.Min(f.Def.Hp, f.Hp + (int)Math.Round(dmg * drain.Share));
                            Tell(f, drain, t.Agent.GivenName);
                        }
                        if (!t.Down && !Free(t) && f.Ways["swallow"] is { } sw && !_held.ContainsKey(t) && !_held.Values.Any(h => h.By == f))
                        {
                            _held[t] = (f, round + sw.Rounds, Math.Max(1, dmg / 2));
                            Log(Cap(Knacks.Fill(sw.Text, The(f.Def), t.Agent.GivenName)));
                        }
                        else if (!t.Down && !Free(t) && f.Ways["grapple"] is { } gr)
                        {
                            _snared[t] = (round + gr.Rounds, gr.Hit);
                            Tell(f, gr, t.Agent.GivenName);
                        }
                        if (t.Down)
                        {
                            Log($"{Cap(The(f.Def))} {(roll == 20 ? "crits" : "hits")} {t.Agent.GivenName} for {dmg}. {t.Agent.GivenName} goes down.");
                            Mark("down", t);
                            if (t.Sheet.NicknameFor == "down") Gag(t, "down");
                            else
                            {
                                Gags.Add(new Gag(t.Agent.Id, "down"));
                                Speak("down", t);
                            }
                        }
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
                var healer = _hushed ? null : Standing.FirstOrDefault(h => h.Sheet.Cls.Healer && h.Heals > 0);
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
                // Nobody could reach them: they wake in the veiled room and answer its three questions (death saves).
                var veil = _content.Patrons.Veil;
                string name = c.Agent.GivenName;
                if (veil.Wake != "") Log(veil.Wake.Replace("{name}", name));
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
                string said = string.Join(", ", rolls);
                if (fail >= 3)
                {
                    // Before the door closes, a patron may offer a costly way back.
                    if (Patrons.Offer(_content, _rng, c.Sheet) is { } patron)
                    {
                        Log(veil.Fail.Replace("{name}", name).Replace("{rolls}", said) + " " + veil.Offer.Replace("{name}", name).Replace("{patron}", patron.Name));
                        if (!c.Sheet.Cls.Healer || c.Sheet.Patron == patron.Id)
                        {
                            c.Down = false;
                            c.Hp = 1;
                            Bargains[c.Agent.Id] = patron.Id;
                            Log(veil.Take.Replace("{name}", name).Replace("{patron}", patron.Name));
                            Mark("bargain", c, rolls[^1]);
                            continue;
                        }
                        Log(veil.Refuse.Replace("{name}", name).Replace("{patron}", patron.Name));
                    }
                    c.Dead = true;
                    Log($"{name} fails their death saves ({said}) and dies.");
                    Mark("death", c, rolls[^1]);
                    Speak("death", c);
                }
                else
                {
                    c.Down = false;
                    c.Hp = 1;
                    Log(veil.Pass != "" ? veil.Pass.Replace("{name}", name).Replace("{rolls}", said) : $"{name} pulls through on death saves ({said}).");
                    Mark("saved", c, rolls[^1]);
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
                c.Used.Clear();
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
            Treat();
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
            Forage(habitats);
            if (_rng.Chance(rules.Ambush + rules.PerTier * tier))
            {
                var check = WatchCheck();
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
            if (kind.Wonder != null)
            {
                Wonder(kind, name, tier);
                return;
            }
            if (kind.Peril is { } peril && _way != null && _way.Chance(peril.Chance))
            {
                Peril(peril, name, tier);
                if (!Wiped && _rng.Chance(kind.Ambush))
                {
                    Wilds(tier, kind.Habitats, kind.AmbushText.Count > 0 ? _rng.Pick(kind.AmbushText) : $"trouble in {name}", false);
                    MarkLost();
                }
                return;
            }
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

        // ---- The road's jobs, perils and strange places (Wayfaring) ----

        /// <summary>Dice for the road's own rolls, off the town's stream; null when the venture has no road.</summary>
        private Rng? _way;
        private List<RoadJob> _jobs = [];
        private Dictionary<int, Dictionary<string, int>> _skills = [];
        private int _food = int.MaxValue;
        private int _hunger;

        /// <summary>No spell works here (dead-magic ground).</summary>
        private bool _hushed;

        /// <summary>The tracker found a back way in: one room fewer.</summary>
        private bool _tracked;

        private WayRules Roadways => _content.Roads.Wayfaring;

        /// <summary>Who does what on the road, their bonus at every skill the road asks for, and the road's dice.</summary>
        public void TakeJobs(List<RoadJob> jobs, Dictionary<int, Dictionary<string, int>> skills, Rng dice)
        {
            _jobs = jobs;
            _skills = skills;
            _way = dice;
            _food = Roadways.Rations;
        }

        public IReadOnlyList<RoadJob> Jobs => _jobs;

        public void TellJobs()
        {
            if (_jobs.Count == 0) return;
            var list = Expedition.ListNames(_jobs.Select(j => $"{j.Name} {Roadways.Job(j.Job).Verb}").ToList());
            string line = Wayfaring.Pick(_way!, Roadways.Texts, "jobs");
            if (line != "") Log(line.Replace("{list}", list, StringComparison.Ordinal));
        }

        private Combatant? Doing(string job) =>
            _jobs.FirstOrDefault(j => j.Job == job) is { } j ? Standing.FirstOrDefault(c => c.Agent.Id == j.AgentId) : null;

        /// <summary>A hero's bonus at a skill on the road: the sheet's, their gear's, and anything weighing on them (hunger).</summary>
        private int Bonus(Combatant c, string skill) =>
            (_skills.TryGetValue(c.Agent.Id, out var m) && m.TryGetValue(skill, out var b) ? b : 0) + c.Skill;

        /// <summary>A job's check: routine (no roll) when ten would do, else a d20 with natural 1s and 20s.</summary>
        private (bool Ok, int Roll, int Total) Try(Combatant c, string skill, int dc, bool pressed = false)
        {
            int bonus = Bonus(c, skill);
            if (!pressed && Wayfaring.Routine(bonus, dc)) return (true, 0, 10 + bonus);
            int roll = (_way ?? _rng).D20();
            return (roll == 20 || (roll != 1 && roll + bonus >= dc), roll, roll + bonus);
        }

        private static string Rolled((bool Ok, int Roll, int Total) t, int dc) => t.Roll == 0 ? "routine" : $"d20 {t.Roll}, {t.Total} vs DC {dc}";

        private string WayText(string key, Combatant? who = null) =>
            Wayfaring.Pick(_way ?? _rng, Roadways.Texts, key).Replace("{name}", who?.Agent.GivenName ?? "", StringComparison.Ordinal);

        /// <summary>The night's watch: the hero whose job it is, or the sharpest eyes if nobody took it.</summary>
        private Check WatchCheck()
        {
            if (Doing("watch") is not { } w) return BestCheck(SkillKind.Perception);
            int roll = _rng.D20();
            return new Check(w, roll, roll + Bonus(w, "perception"));
        }

        private void SetHunger(int n)
        {
            n = Math.Clamp(n, 0, Roadways.HungerMax);
            int delta = n - _hunger;
            if (delta == 0) return;
            foreach (var c in Party)
            {
                c.Attack -= delta;
                c.Skill -= delta;
            }
            _hunger = n;
        }

        /// <summary>A night's food: the rations first, then whatever the forager finds.</summary>
        private void Forage(IReadOnlyList<string> habitats)
        {
            if (_way == null) return;
            if (_food > 0)
            {
                _food--;
                return;
            }
            var job = Roadways.Job("forage");
            if (Doing("forage") is not { } f)
            {
                Log(WayText("hungry"));
                SetHunger(_hunger + 1);
                return;
            }
            int dc = job.Dc + (habitats.Any(job.HarshHabitats.Contains) ? job.Harsh : 0);
            var t = Try(f, job.Skill, dc);
            if (!t.Ok)
            {
                Log(WayText("hungryForager", f) + $" ({Rolled(t, dc)})");
                SetHunger(_hunger + 1);
                return;
            }
            Log(WayText("fed", f) + (t.Roll == 0 ? "" : $" ({Rolled(t, dc)})"));
            SetHunger(_hunger - 1);
            if (t.Roll == 20 && Doing("treat") is { } medic)
            {
                medic.Potions++;
                Log(WayText("herbs", f));
            }
        }

        /// <summary>The medic on a short rest: cleaned and stitched wounds give back a little more.</summary>
        private void Treat()
        {
            if (Doing("treat") is not { } medic) return;
            var hurt = Standing.Where(c => c.Hp < c.Sheet.MaxHp).ToList();
            if (hurt.Count == 0) return;
            var job = Roadways.Job("treat");
            var t = Try(medic, job.Skill, job.Dc);
            if (!t.Ok) return;
            int each = Math.Max(1, job.Heal + Rng.Modifier(medic.Agent.Abilities.Wis));
            int total = 0;
            foreach (var c in hurt)
            {
                int gain = Math.Min(each, c.Sheet.MaxHp - c.Hp);
                c.Hp += gain;
                total += gain;
            }
            Log(WayText("treated", medic).Replace("{hp}", total.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        /// <summary>At the site: the tracker looks for a way in that nobody guards.</summary>
        public void Track(SiteDef site)
        {
            if (_way == null || Doing("track") is not { } tracker) return;
            var job = Roadways.Job("track");
            int dc = job.Dc + (int)(Math.Max(1, site.Tier) * job.PerTier);
            var t = Try(tracker, job.Skill, dc);
            _tracked = t.Ok;
            Log(WayText(t.Ok ? "tracked" : "untracked", tracker) + (t.Roll == 0 ? "" : $" ({Rolled(t, dc)})"));
        }

        /// <summary>The bone ferryman takes them over his water, a toll a head.</summary>
        public void Ferry(TerrainKindDef kind, string name)
        {
            if (Wiped || kind.Wonder is not { } w) return;
            int toll = w.Toll * Standing.Count;
            Gold -= toll;
            Log(Pick(w.Text).Replace("{name}", name, StringComparison.Ordinal).Replace("{toll}", w.Toll.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal));
        }

        private string Pick(IReadOnlyList<string> lines) => lines.Count > 0 ? (_way ?? _rng).Pick(lines) : "";

        private Combatant? Best(string skill) => Standing.OrderByDescending(c => Bonus(c, skill)).ThenBy(c => c.Agent.Id).FirstOrDefault();

        /// <summary>A strange place on the road, and the rule it bends.</summary>
        private void Wonder(TerrainKindDef kind, string name, int tier)
        {
            var w = kind.Wonder!;
            var dice = _way ?? _rng;
            int gold = w.Gold * Math.Max(1, tier);
            string Say(string line, Combatant? who) => line
                .Replace("{name}", name, StringComparison.Ordinal)
                .Replace("{name2}", who?.Agent.GivenName ?? "", StringComparison.Ordinal)
                .Replace("{gold}", gold.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
            switch (w.Kind)
            {
                case "hush":
                    Log(Say(Pick(w.Text), null));
                    _hushed = true;
                    if (_rng.Chance(kind.Ambush)) Wilds(tier, kind.Habitats, kind.AmbushText.Count > 0 ? Pick(kind.AmbushText) : $"trouble in {name}", false);
                    _hushed = false;
                    MarkLost();
                    break;
                case "well":
                {
                    var hero = dice.Pick(Standing);
                    Log(Say(Pick(w.Text), hero));
                    int roll = dice.D20();
                    if (roll == 20)
                    {
                        foreach (var c in Standing) c.Hp = c.Sheet.MaxHp;
                        Gold += gold;
                        Log(Say(Pick(w.Wish), hero));
                    }
                    else if (roll >= 15)
                    {
                        hero.Potions++;
                        Log(Say(Pick(w.Boon), hero));
                    }
                    else if (roll == 1)
                    {
                        hero.Skill--;
                        Log(Say(Pick(w.Curse), hero));
                    }
                    else Log(Say(Pick(w.Nothing), hero));
                    break;
                }
                case "bones":
                {
                    var who = Best(w.Skill)!;
                    var t = Try(who, w.Skill, w.Dc + tier / 2);
                    if (t.Ok) Gold += gold;
                    Log(Say(t.Ok ? Pick(w.Text) : Pick(w.Missed), who));
                    if (_rng.Chance(kind.Ambush))
                    {
                        Wilds(tier, ["tomb", "badlands", "desert"], kind.AmbushText.Count > 0 ? Pick(kind.AmbushText) : $"trouble in {name}", false);
                        MarkLost();
                    }
                    break;
                }
                case "ring":
                {
                    var who = Best(w.Skill)!;
                    var t = Try(who, w.Skill, w.Dc + tier / 2);
                    if (t.Ok)
                    {
                        Log(Say(Pick(w.Text), who));
                        LongRest();
                    }
                    else
                    {
                        Log(Say(Pick(w.Missed), who));
                        foreach (var c in Party) c.Potions = 0;
                        Gold -= gold / 2;
                    }
                    break;
                }
                case "oasis":
                    Log(Say(Pick(w.Text), null));
                    _food = Math.Max(_food, Roadways.Rations);
                    SetHunger(0);
                    foreach (var c in Standing) c.Hp = Math.Min(c.Sheet.MaxHp, c.Hp + c.Sheet.MaxHp / 4);
                    break;
                case "mirage":
                {
                    var who = Best(w.Skill)!;
                    var t = Try(who, w.Skill, w.Dc + tier / 2);
                    if (t.Ok)
                    {
                        Log(Say(Pick(w.Text), who) + $" ({Rolled(t, w.Dc + tier / 2)})");
                        break;
                    }
                    Log(Say(Pick(w.Missed), who));
                    if (w.Monster != "" && _content.Library.Monster(w.Monster) != null)
                    {
                        RoadFight(new EncounterDef { Kind = "combat", Monster = w.Monster, Count = [1, 1], Text = $"the false oasis of {name}" }, true);
                        MarkLost();
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Nature as a monster: the avalanche, the sandstorm, the cliff. Each hero in turn rolls their best skill
        /// against it (no taking ten with the mountain coming down); they need so many successes before so many
        /// failures, every failure hurts whoever rolled it, and losing costs them their packs or more blood.
        /// </summary>
        private void Peril(PerilDef p, string name, int tier)
        {
            var dice = _way!;
            Log($"In {name}: {Pick(p.Text)}");
            int dc = p.Dc + tier / 2;
            int scale = 1 + (Math.Max(1, tier) - 1) / 4;
            int ok = 0, bad = 0, turn = 0;
            var held = new List<string>();
            var hurt = new List<string>();
            while (ok < p.Need && bad < p.Fail && !Wiped)
            {
                var order = Standing.OrderBy(c => c.Agent.Id).ToList();
                var c = order[turn++ % order.Count];
                string skill = p.Skills.OrderByDescending(k => Bonus(c, k)).ThenBy(k => k, StringComparer.Ordinal).FirstOrDefault() ?? "athletics";
                var t = Try(c, skill, dc, pressed: true);
                if (t.Ok)
                {
                    ok++;
                    held.Add($"{c.Agent.GivenName} ({t.Roll}+{t.Total - t.Roll})");
                    continue;
                }
                bad++;
                int dmg = 0;
                for (int k = 0; k < scale; k++) dmg += dice.Roll(1, p.Die);
                Hurt(c, dmg);
                hurt.Add($"{c.Agent.GivenName} ({dmg})");
            }
            string tally = (held.Count > 0 ? $" Holding fast: {string.Join(", ", held)}." : "") + (hurt.Count > 0 ? $" Hurt: {string.Join(", ", hurt)}." : "");
            if (ok >= p.Need)
                Log($"{p.Win}{tally}");
            else
            {
                Log($"{p.Lose}{tally}");
                if (p.Cost == "packs")
                {
                    _food = 0;
                    foreach (var c in Party) c.Potions /= 2;
                }
                else
                    foreach (var c in Standing)
                    {
                        int dmg = 0;
                        for (int k = 0; k < scale; k++) dmg += dice.Roll(1, p.Die);
                        Hurt(c, dmg);
                    }
            }
            if (Party.Any(c => c.Down && !c.Dead)) AfterCombat();
            MarkLost();
        }

        /// <summary>A fight on the road: nothing to win, so they run sooner than in a lair.</summary>
        private void RoadFight(EncounterDef e, bool surprised)
        {
            int swell = Swell;
            double caution = _caution;
            Swell = 0;
            _caution = Math.Max(_caution, Roads.Retreat.RoadCaution);
            Combat(e, surprised);
            Swell = swell;
            _caution = caution;
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
            int count = Math.Min(deck.Count, 2 + site.Tier - (_tracked ? 1 : 0));
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
                    Mark("parley", check, dc);
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
                if (!boss && e.Kind == "combat" && Terms is { Heist: true } && Sneak(site)) won = true;
                else if (!boss && e.Kind == "combat" && Way(e)) won = true;
                else if (e.Kind == "combat") won = Combat(e, false);
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

        /// <summary>Far enough in for the bounty's terms: drive the rest out, cut the captives loose, bring the fallen home, or rob the hoard.</summary>
        private ExpeditionOutcome Reach(SiteDef site)
        {
            var t = Terms!;
            int loot = (int)(_rng.Range(site.Loot[0], site.Loot[1] + 1) * t.Loot);
            Gold += loot;
            if (t.Captives)
            {
                Log($"In the pens at the back they find the captives, cut them loose and slip out of {site.Name} with them before the alarm.");
                Mark("goal");
                GoalMet = true;
                return ExpeditionOutcome.Retreated;
            }
            if (t.Fallen)
            {
                Log($"Where the fighting was worst they find what is left of the town's fallen, and carry them and their gear out of {site.Name}{(loot > 0 ? $" with {loot} gold besides" : "")}.");
                Mark("goal");
                GoalMet = true;
                return ExpeditionOutcome.Retreated;
            }
            if (t.Item)
            {
                if (t.Heist && !Vault(site))
                {
                    Gold -= loot - (int)(loot * _content.Bounties.Heist.Bolted);
                    Log($"The vault door slams behind them and every bell in {site.Name} rings. They run with what is in their hands{(loot > 0 ? $" ({(int)(loot * _content.Bounties.Heist.Bolted)} gold)" : "")}, and the treasure stays behind.");
                    Mark("goal");
                    return ExpeditionOutcome.Retreated;
                }
                Log($"They find the hoard, take what they can carry{(loot > 0 ? $" ({loot} gold)" : "")} and get out of {site.Name} before the rest of it wakes.");
                Mark("goal");
                Treasure(site, true);
                GoalMet = true;
                return ExpeditionOutcome.Retreated;
            }
            Log($"With half of {site.Name} taken, they set fire to the rest. What is left of its folk breaks and runs for the wilds{(loot > 0 ? $", leaving {loot} gold behind" : "")}.");
            Mark("goal");
            Driven = true;
            SiteCleared = true;
            return ExpeditionOutcome.Cleared;
        }

        /// <summary>
        /// Slip past a room on a quiet job: the party's best at it against the place, or fight it and raise the alarm.
        /// Once the alarm is up the whole place is awake and nobody slips past anything. Returns whether they got by.
        /// </summary>
        private bool Sneak(SiteDef site)
        {
            var h = _content.Bounties.Heist;
            if (Alarm >= h.AlarmMax) return false;
            var check = BestCheck(SkillKind.Trap);
            int dc = h.SneakDc + h.PerTier * Math.Max(1, site.Tier);
            if (check.Total >= dc)
            {
                Log($"{check.Name} leads them past unseen (d20 {check.Roll}, {check.Total} vs DC {dc}); the alarm stays at {Alarm} of {h.AlarmMax}.");
                Mark("sneak", check, dc);
                return true;
            }
            Raise(site, $"{check.Name} kicks a pail in the dark (d20 {check.Roll}, {check.Total} vs DC {dc})");
            return false;
        }

        /// <summary>The alarm goes up a step; at the top the whole place wakes, and more foes come to every room after.</summary>
        private void Raise(SiteDef site, string why)
        {
            var h = _content.Bounties.Heist;
            Alarm = Math.Min(h.AlarmMax, Alarm + 1);
            if (Alarm >= h.AlarmMax)
            {
                Swell += h.Wake;
                Log($"{why}, and the alarm goes up all through {site.Name}: every room ahead is awake now.");
            }
            else Log($"{why}. The alarm rises to {Alarm} of {h.AlarmMax}.");
        }

        /// <summary>The vault round a hoard: its locks, wards and hidden catches one after another, sharing the job's alarm. Returns whether they got in.</summary>
        private bool Vault(SiteDef site)
        {
            var h = _content.Bounties.Heist;
            int layers = h.Layers.Count == 0 ? 0 : h.Layers[Math.Clamp(site.Tier - 1, 0, h.Layers.Count - 1)];
            int dc = h.LayerDc + h.PerTier * Math.Max(1, site.Tier);
            for (int i = 0; i < layers; i++)
            {
                string kind = h.Kinds.Count == 0 ? SkillKind.Trap : h.Kinds[i % h.Kinds.Count];
                string what = kind switch { SkillKind.Puzzle => "a ward", SkillKind.Perception => "a hidden catch", _ => "a lock" };
                var check = BestCheck(kind);
                if (check.Total >= dc)
                {
                    Log($"{check.Name} gets past {what} on the vault (d20 {check.Roll}, {check.Total} vs DC {dc}), {i + 1} of {layers}.");
                    Mark("vault", check, dc);
                    continue;
                }
                Raise(site, $"{check.Name} fumbles {what} on the vault (d20 {check.Roll}, {check.Total} vs DC {dc})");
                if (Alarm >= h.AlarmMax) return false;
            }
            return true;
        }

        private void Hoard(SiteDef site)
        {
            int loot = _rng.Range(site.Loot[0], site.Loot[1] + 1);
            Gold += loot;
            string line = $"{Cap(site.Name)} is cleared. The hoard holds {loot} gold";
            line += Treasure(site, false);
            Log($"{line}.");
            Mark("cleared");
            Speak("treasure");
        }

        /// <summary>The magic item in a hoard, if there is one (always, when sure), and who takes it. Returns the words for the Ledger line.</summary>
        private string Treasure(SiteDef site, bool sure)
        {
            string line = "";
            // Item tiers stop at 5 (legendary); sites for higher levels draw from the top of the list.
            int itemTier = Math.Min(site.Tier, 5);
            var pool = _content.HoardItems.Where(it => it.Tier >= itemTier && it.Tier <= itemTier + 1).ToList();
            if (pool.Count > 0 && (sure || _rng.Chance(_content.Loot.ItemChancePerTier * site.Tier)))
            {
                var item = Hoarded(pool, site);
                if (item.Slot == "consumable" && _content.Regalia.Item(item.Id) is not { Slot: Regalia.Hall })
                {
                    // A draught goes to whoever carries fewest.
                    var who = Party.Where(c => !c.Dead).OrderBy(c => c.Sheet.Draughts.Count).ThenBy(c => c.Agent.Id).First();
                    Items.Add(new ExpeditionItem(who.Agent.Id, item));
                    line += $" and {Agent.Article(Library.Prose(item.Name))} {Library.Prose(item.Name)}, which {who.Agent.GivenName} tucks away";
                }
                else if (item.Slot is "town" or "siege" or "consumable")
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
            string scroll = FindScroll();
            if (scroll != "") line += line == "" ? $" and {scroll}" : $", and {scroll}";
            if (sure && line != "") Log($"They bring back{line[4..]}.");
            return line;
        }

        /// <summary>A hoard may hold a spell scroll: a wizard copies it into the spellbook, another caster carries it.</summary>
        private string FindScroll()
        {
            var sr = _content.Shrines.Scroll;
            var reader = Party.Where(c => !c.Dead && Scrolls.Reads(c.Sheet.Cls)).OrderBy(c => c.Agent.Id).FirstOrDefault();
            if (reader == null || !_rng.Chance(sr.Found)) return "";
            if (reader.Sheet.Cls.Id == sr.Copier && reader.Copied < sr.MaxCopied)
            {
                reader.Copied++;
                return $"a spell scroll {reader.Agent.GivenName} copies into the spellbook by firelight";
            }
            reader.Scrolls++;
            return $"a spell scroll {reader.Agent.GivenName} tucks away";
        }

        /// <summary>What happened, for the town to learn when (if) they come home.</summary>
        public ExpeditionResult Result(ExpeditionOutcome outcome, string siteName)
        {
            if (outcome == ExpeditionOutcome.Wiped)
            {
                Log($"No one comes back from {siteName}.");
                Mark("lost");
            }
            if (outcome != ExpeditionOutcome.Wiped) Haul();
            var hp = new OrderedMap<int, int>();
            var potions = new OrderedMap<int, int>();
            var scrolls = new OrderedMap<int, int>();
            var copied = new OrderedMap<int, int>();
            foreach (var c in Party)
            {
                hp[c.Agent.Id] = c.Dead ? 0 : Math.Max(1, c.Hp);
                potions[c.Agent.Id] = c.Potions;
                scrolls[c.Agent.Id] = c.Scrolls;
                copied[c.Agent.Id] = c.Copied;
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
                Scrolls = scrolls,
                Copied = copied,
                Drunk = Party.Where(c => c.Sheet.Draughts.Count > 0).Select(c => c.Agent.Id).ToList(),
                SiteCleared = SiteCleared,
                LostAt = LostAt,
                Talked = Talked,
                Driven = Driven,
                GoalMet = GoalMet,
                Beats = Beats.ToList(),
                Gags = Gags.ToList(),
                Fought = Fought.ToList(),
                Bargains = Bargains,
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
        run.Drink();
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
