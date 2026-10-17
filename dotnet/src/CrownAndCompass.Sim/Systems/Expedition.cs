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
    OrderedMap<int, int> Kills);

/// <summary>
/// The expedition resolver: a deterministic, seedable dice simulation of a party going into a
/// site, encounter by encounter. It never touches the town; it returns what happened and a
/// Chronicle written from the logged rolls, and the caller applies the results.
/// </summary>
public static class Expedition
{
    private sealed class Combatant(Agent agent)
    {
        public Agent Agent { get; } = agent;
        public HeroSheet Sheet { get; } = agent.Hero!;
        public bool Down { get; set; }
        public bool Dead { get; set; }
        public int Heals { get; set; } = agent.Hero!.Cls.HealsAt(agent.Hero.Level);
    }

    private sealed class Foe(MonsterDef def)
    {
        public MonsterDef Def { get; } = def;
        public int Hp { get; set; } = def.Hp;
    }

    private sealed record Check(Combatant Who, int Roll, int Total);

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

    /// <remarks>Integer division matches the TS sim because hit dice are even (d6 to d12).</remarks>
    public static int HeroMaxHp(HeroSheet sheet, int con)
    {
        int die = sheet.Cls.HitDie;
        return Math.Max(1, die + Rng.Modifier(con) + (sheet.Level - 1) * Math.Max(1, die / 2 + 1 + Rng.Modifier(con)));
    }

    public static int HeroAc(Agent a)
    {
        var s = a.Hero!;
        return 10 + s.Cls.Armor + Math.Min(2, Math.Max(0, Rng.Modifier(a.Abilities.Dex))) + s.ArmorBonus;
    }

    private static int AttackBonus(Agent a)
    {
        var s = a.Hero!;
        return Proficiency(s.Level) + Rng.Modifier(a.Abilities[s.Cls.Primary]) + s.WeaponBonus;
    }

    private static string Sign(int n) => n >= 0 ? $"+{n}" : $"-{-n}";

    private sealed class Delve
    {
        public readonly List<string> Lines = [];
        public readonly List<Combatant> Party;
        public readonly OrderedMap<int, int> Kills = new();
        public int Gold;
        public int Xp;
        private bool _skipNextCombat;
        private readonly Rng _rng;
        private readonly Content _content;
        private readonly SiteDef? _site;

        public Delve(Rng rng, Content content, IReadOnlyList<Agent> heroes, SiteDef? site = null)
        {
            _rng = rng;
            _content = content;
            _site = site;
            Party = heroes.Select(agent => new Combatant(agent)).ToList();
        }

        /// <summary>Traps and hazards hit harder in sites built for higher levels: one more set of dice every four levels.</summary>
        private int Scale => _site == null ? 1 : 1 + (Math.Max(1, _site.Tier) - 1) / 4;

        public List<Combatant> Standing => Party.Where(c => !c.Down && !c.Dead).ToList();

        public void Log(string s) => Lines.Add(s);

        public double HpFraction()
        {
            int hp = 0;
            int max = 0;
            foreach (var c in Party)
            {
                if (c.Dead) continue;
                hp += Math.Max(0, c.Sheet.Hp);
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
            return new Check(who!, roll, roll + best);
        }

        private static int SkillBonus(Combatant c, string kind)
        {
            int ab = kind == SkillKind.Hazard ? Math.Max(c.Agent.Abilities.Con, c.Agent.Abilities.Wis) : c.Agent.Abilities[SkillAbility[kind]];
            int cls = c.Sheet.Cls.Skills.GetValueOrDefault(kind, 0);
            return Rng.Modifier(ab) + cls + (cls > 0 ? Proficiency(c.Sheet.Level) : 0);
        }

        private static void Hurt(Combatant c, int dmg)
        {
            c.Sheet.Hp -= dmg;
            if (c.Sheet.Hp <= 0)
            {
                c.Sheet.Hp = 0;
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
                    string who = check.Who.Agent.GivenName;
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
                    string who = check.Who.Agent.GivenName;
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
                            ? $"{Cap(e.Text)}. {check.Who.Agent.GivenName} notices the lid breathing (d20 {check.Roll}, {check.Total} vs DC 13). It's a mimic."
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
                if (HpFraction() < (def.Boss ? 0.15 : 0.2) && foes.Any(f => f.Hp > 0))
                {
                    Log("Bloodied and outmatched, the party breaks off and runs.");
                    AfterCombat();
                    return false;
                }
            }

            bool won = foes.All(f => f.Hp <= 0);
            if (won)
            {
                Xp += totalXp;
                var hurt = Standing.Where(c => c.Sheet.Hp < c.Sheet.MaxHp / 3.0).Select(c => $"{c.Agent.GivenName} ({c.Sheet.Hp}/{c.Sheet.MaxHp} hp)").ToList();
                Log($"The fight is over in {round} round{(round == 1 ? "" : "s")}.{(hurt.Count > 0 ? $" Badly hurt: {string.Join(", ", hurt)}." : "")}");
            }
            AfterCombat();
            return won;
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
                if (s.Potions > 0 && s.Hp < s.MaxHp * 0.35)
                {
                    s.Potions--;
                    int drunk = Potion();
                    s.Hp = Math.Min(s.MaxHp, s.Hp + drunk);
                    Log($"{name} gulps a {PotionName()} (+{drunk} hp).");
                }

                // Clerics keep the party alive before they swing.
                if (s.Cls.Healer && c.Heals > 0)
                {
                    var patient = Party.Where(p => !p.Dead && p.Sheet.Hp < p.Sheet.MaxHp * 0.4).OrderBy(p => p.Sheet.Hp).FirstOrDefault();
                    if (patient != null)
                    {
                        var spell = s.Cls.HealSpell!;
                        int healed = (spell.Dice ?? new Dice(1, 8, 0)).Roll(rng) + Rng.Modifier(c.Agent.Abilities[s.Cls.Casting]);
                        bool wasDown = patient.Down;
                        patient.Sheet.Hp = Math.Min(patient.Sheet.MaxHp, patient.Sheet.Hp + Math.Max(1, healed));
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
                int atk = AttackBonus(c.Agent);
                if (roll == 1)
                {
                    if (rng.Chance(0.3)) Log($"{name} rolls a natural 1 and nearly loses {s.Cls.Fumble}.");
                    continue;
                }
                if (roll == 20 || roll + atk >= target.Def.Ac)
                {
                    int dmg = rng.Roll(roll == 20 ? 2 : 1, s.Cls.DamageDie) + Rng.Modifier(c.Agent.Abilities[s.Cls.Primary]) + s.WeaponBonus;
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
                    if (roll == 20 || roll + f.Def.Attack >= HeroAc(t.Agent))
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
                    c.Sheet.Hp = Math.Max(1, _rng.Roll(1, 8) + healer.Sheet.Level);
                    Log($"{healer.Agent.GivenName} stabilizes {c.Agent.GivenName}.");
                    continue;
                }
                if (Standing.Count == 0)
                {
                    c.Dead = true;
                    continue;
                }
                // No cleric to hand: a friend pours a potion down their throat.
                var donor = Standing.FirstOrDefault(h => h.Sheet.Potions > 0);
                if (donor != null)
                {
                    donor.Sheet.Potions--;
                    c.Down = false;
                    c.Sheet.Hp = Potion();
                    Log($"{donor.Agent.GivenName} pours a {PotionName()} down {c.Agent.GivenName}'s throat.");
                    continue;
                }
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
                    c.Sheet.Hp = 1;
                    Log($"{c.Agent.GivenName} pulls through on death saves ({string.Join(", ", rolls)}).");
                }
            }
        }
    }

    private static string Article(string w) => "aeiou".Contains(w[0]) ? "an" : "a";

    private static string Cap(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    /// <param name="slotsSpent">Spell slots the party spent on sending-stone messages; they come out of the healers' heals.</param>
    public static ExpeditionResult Run(Rng rng, Content content, SiteDef site, IReadOnlyList<Agent> heroes, int slotsSpent = 0)
    {
        var run = new Delve(rng, content, heroes, site);
        foreach (var c in run.Party)
        {
            int spent = Math.Min(slotsSpent, c.Heals);
            c.Heals -= spent;
            slotsSpent -= spent;
        }
        var names = heroes.Select(h => h.GivenName).ToList();
        run.Log($"{ListNames(names)} set out for {site.Name}.");

        // Draw this delve's encounters from the site's deck.
        var deck = site.Encounters.ToList();
        int count = Math.Min(deck.Count, 2 + site.Tier);
        var plan = new List<EncounterDef>();
        for (int i = 0; i < count; i++)
        {
            int k = rng.Range(0, deck.Count);
            plan.Add(deck[k]);
            deck.RemoveAt(k);
        }

        var outcome = ExpeditionOutcome.Retreated;
        bool rested = false;
        for (int i = 0; i <= plan.Count; i++)
        {
            bool boss = i == plan.Count;
            var e = boss ? site.Boss : plan[i];
            if (boss) run.Log("At last, the heart of the place.");
            if (e.Kind == "combat")
            {
                bool won = run.Combat(e, false);
                if (run.Standing.Count == 0)
                {
                    outcome = ExpeditionOutcome.Wiped;
                    break;
                }
                if (boss && won)
                {
                    outcome = ExpeditionOutcome.Cleared;
                    break;
                }
                if (!won) break;
            }
            else
            {
                run.Encounter(e);
                if (run.Standing.Count == 0)
                {
                    outcome = ExpeditionOutcome.Wiped;
                    break;
                }
            }

            // A short rest halfway in.
            if (!rested && i >= plan.Count / 2)
            {
                rested = true;
                foreach (var c in run.Standing) c.Sheet.Hp = Math.Min(c.Sheet.MaxHp, c.Sheet.Hp + (int)Math.Ceiling(c.Sheet.MaxHp / 4.0));
            }
            // Know when to quit.
            if (!boss && (run.HpFraction() < 0.3 || run.Party.Count(c => c.Dead) * 2 >= run.Party.Count))
            {
                run.Log("Too battered to go on, the party turns back.");
                break;
            }
        }

        var items = new List<ExpeditionItem>();
        if (outcome == ExpeditionOutcome.Cleared)
        {
            int loot = rng.Range(site.Loot[0], site.Loot[1] + 1);
            run.Gold += loot;
            string line = $"{Cap(site.Name)} is cleared. The hoard holds {loot} gold";
            // Item tiers stop at 5 (legendary); sites for higher levels draw from the top of the list.
            int itemTier = Math.Min(site.Tier, 5);
            var pool = content.LootItems.Where(it => it.Tier >= itemTier && it.Tier <= itemTier + 1).ToList();
            if (pool.Count > 0 && rng.Chance(content.Loot.ItemChancePerTier * site.Tier))
            {
                var item = rng.Pick(pool);
                if (item.Slot == "town")
                {
                    // Too big or too strange for one hero: it goes to the town.
                    items.Add(new ExpeditionItem(null, item));
                    line += $" and the {item.Name}, which they haul home for the town";
                }
                else
                {
                    var who = run.Party.Where(c => !c.Dead).OrderBy(c => c.Sheet.Items.Count).ThenBy(c => c.Agent.Id).First();
                    items.Add(new ExpeditionItem(who.Agent.Id, item));
                    if (item.Slot == "weapon") who.Sheet.WeaponBonus += content.Loot.WeaponBonus[Math.Min(item.Tier, content.Loot.WeaponBonus.Length - 1)];
                    else if (item.Slot is "armor" or "shield") who.Sheet.ArmorBonus += 1;
                    who.Sheet.Items.Add(item.Name);
                    line += $" and the {item.Name}, which {who.Agent.GivenName} claims";
                }
            }
            run.Log($"{line}.");
        }
        else if (outcome == ExpeditionOutcome.Wiped)
        {
            run.Log($"No one comes back from {site.Name}.");
        }

        return new ExpeditionResult(
            outcome,
            run.Lines,
            run.Party.Where(c => c.Dead).Select(c => c.Agent.Id).ToList(),
            run.Gold,
            run.Xp,
            items,
            run.Kills);
    }

    public static string ListNames(IReadOnlyList<string> names)
    {
        if (names.Count <= 1) return string.Concat(names);
        return $"{string.Join(", ", names.Take(names.Count - 1))} and {names[^1]}";
    }
}
