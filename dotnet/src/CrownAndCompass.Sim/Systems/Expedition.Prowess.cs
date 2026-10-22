namespace CrownAndCompass.Sim.Systems;

public static partial class Expedition
{
    // How heroes fight at their level, and how foes pick whom to hit (Prowess, content/game/prowess.json).
    public sealed partial class Venture
    {
        /// <summary>The damage type that seared each regenerating foe this round, for the line that says so.</summary>
        private readonly Dictionary<Foe, string> _searedBy = [];

        /// <summary>Spells that keep hurting foes round after round: whose, the dice and type, the save, until when, and how many foes.</summary>
        private readonly List<(Combatant By, Dice Dice, string Type, string Save, int Dc, int Until, int Targets, string Name)> _auras = [];

        /// <summary>Party spells on the party this fight (bless), so two casters don't spend slots on the same.</summary>
        private readonly HashSet<string> _buffs = [];

        private ProwessRules Rules => _content.Prowess;

        /// <summary>Back to full after a long rest (slots, every use, the healing pool), or what a short rest brings back.</summary>
        private void Refresh(Combatant c, bool longRest)
        {
            var s = c.Sheet;
            if (longRest || s.Cls.Magic == "pact") c.Slots = Prowess.Casts(_content, s.Cls, s.Level);
            foreach (var p in Prowess.Powers(_content, s))
            {
                bool back = longRest || p.Rest == "short" || (p.ShortFrom > 0 && s.Level >= p.ShortFrom);
                if (!back) continue;
                if (p.Uses.Count > 0) c.Uses[p.Id] = p.Id == "inspiration" ? Math.Max(1, Math.Max(Prowess.At(p.Uses, s.Level), Rng.Modifier(c.Agent.Abilities.Cha))) : Prowess.At(p.Uses, s.Level);
                if (p.Pool > 0 && longRest) c.Pool = p.Pool * s.Level;
            }
            if (longRest) c.Recovered = false;
            // A wizard's first short rest of the day: an hour over the spellbook brings back a few slots.
            else if (Prowess.Power(_content, s, "arcane-recovery") is { } ar && !c.Recovered && !c.Dead)
            {
                int most = Prowess.Casts(_content, s.Cls, s.Level);
                int n = Math.Min(most - c.Slots, Math.Max(1, s.Level / 4));
                if (n > 0)
                {
                    c.Slots += n;
                    c.Recovered = true;
                    Log(Prowess.Fill(ar.Text, ("hero", c.Agent.GivenName), ("n", n.ToString()), ("s", n == 1 ? "" : "s")));
                }
            }
        }

        private int Use(Combatant c, string id) => c.Uses.GetValueOrDefault(id, 0);

        private void StartFight()
        {
            foreach (var c in Party)
            {
                c.Raging = false;
                c.Rose = 0;
                c.Inspired = c.HitBuff = c.AcBuff = c.Haste = c.Focus = c.Temp = 0;
                c.MarkOn = null;
                c.Buffed = false;
                c.Dodged = 0;
            }
            _auras.Clear();
            _buffs.Clear();
            _searedBy.Clear();
        }

        /// <summary>A beast shape lasts the fight.</summary>
        private void EndFight()
        {
            foreach (var c in Party)
                if (c.Form != null)
                {
                    c.Form = null;
                    c.Shape = 0;
                }
        }

        private void EndShape(Combatant c)
        {
            if (Prowess.Power(_content, c.Sheet, "wild-shape") is { Ended.Length: > 0 } ws)
                Log(Prowess.Fill(ws.Ended, ("hero", c.Agent.GivenName), ("shape", Library.Prose(c.Form!.Name))));
            c.Form = null;
            c.Shape = 0;
        }

        /// <summary>A fight worth a power: a boss, a pack, or foes with more blood in them than half the party.</summary>
        private bool Worth(List<Foe> living) =>
            living.Any(f => f.Def.Boss) || living.Count >= 2 || living.Sum(f => f.Hp) * 2 >= Standing.Sum(c => c.Hp);

        private int Dc(Combatant c) => 8 + Proficiency(c.Sheet.Level) + Rng.Modifier(c.Agent.Abilities[c.Sheet.Cls.Casting is { Length: > 0 } k ? k : c.Sheet.Cls.Primary]) + (c.Focus > 0 ? 1 : 0);

        private int SpellAttack(Combatant c) => (c.Sheet.Cls.Spell ? c.Attack : Proficiency(c.Sheet.Level) + Rng.Modifier(c.Agent.Abilities[c.Sheet.Cls.Casting is { Length: > 0 } k ? k : c.Sheet.Cls.Primary]) + c.Edge) + c.Focus + c.HitBuff;

        /// <summary>A foe's saving throw against a hero's spell or power; a legend shrugs off the first it fails.</summary>
        private bool Saves(Foe f, string ability, int dc)
        {
            int bonus = Prowess.SaveBonus(f.Lib, f.Def, ability == "" ? "wis" : ability) + (f.Ways.Has("magic") ? 4 : 0);
            if (_rng.D20() + bonus >= dc) return true;
            if (f.Ways["legendary"] is { } lr && !f.Told.Contains(lr.Id))
            {
                Tell(f, lr);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Damage onto a foe through its defences (resistances, immunities, weaknesses, a hide that turns plain blades),
        /// saying so the first time each matters; fire on a troll stops it knitting, and a blow breaks a charm or a sleep.
        /// Returns what got through.
        /// </summary>
        private int Hit(Foe f, int dmg, string type, bool magic, bool silver, List<Foe> foes, int round)
        {
            var (dealt, how) = Prowess.Harm(_content, f.Lib, dmg, type, magic, silver);
            if (how != "" && f.Told.Add($"harm:{how}:{type}"))
                Log(Cap(Prowess.Line(_content, how, ("foe", The(f.Def)), ("type", type))));
            if (f.Lib?.RegenStops.Contains(type) == true)
            {
                f.Seared = round;
                _searedBy[f] = type;
            }
            if (dealt <= 0) return 0;
            f.Hp -= dealt;
            if (f.Held(round) && f.HeldBy is "unconscious" or "charmed" or "turned") f.HeldUntil = 0;
            Struck(f, foes);
            return dealt;
        }

        /// <summary>A regenerating creature that went down to anything it doesn't fear will get up on its turn.</summary>
        private bool Rising(Foe f, int round) =>
            f.Hp <= 0 && f.Lib is { RegenRises: true } && f.Ways["regen"] != null && f.Rises < Rules.RegenRises && f.Seared != round;

        private void Rise(List<Foe> foes, int round)
        {
            foreach (var f in foes)
            {
                if (!Rising(f, round)) continue;
                f.Rises++;
                f.Hp = Math.Max(1, (int)Math.Round(f.Def.Hp * f.Ways["regen"]!.Share));
                Log(Cap(Prowess.Line(_content, "rises", ("foe", The(f.Def)))));
            }
        }

        /// <summary>Spirit guardians, a moonbeam: the foes near the caster take its dice each round, a save for half.</summary>
        private void Auras(List<Foe> foes, int round)
        {
            foreach (var a in _auras.ToList())
            {
                if (round > a.Until || a.By.Down || a.By.Dead) continue;
                foreach (var f in foes.Where(f => f.Hp > 0).Take(a.Targets).ToList())
                {
                    int dmg = a.Dice.Roll(_rng);
                    if (a.Save != "" && Saves(f, a.Save, a.Dc)) dmg /= 2;
                    Hit(f, Spell(f, dmg), a.Type, true, false, foes, round);
                    if (f.Hp <= 0) Credit(a.By, 1);
                }
            }
        }

        // ---- Bonus actions: rage, a second wind, a fighting song, a beast's shape, the hunter's mark ----

        private void BonusAction(Combatant c, List<Foe> foes, List<Foe> living, int round)
        {
            var s = c.Sheet;
            string name = c.Agent.GivenName;
            bool opening = round <= 1;
            if (Prowess.Power(_content, s, "rage") is { } rage && opening && !c.Raging && Use(c, rage.Id) > 0 && Worth(living))
            {
                c.Uses[rage.Id]--;
                c.Raging = true;
                Log(Prowess.Fill(rage.Text, ("hero", name)));
                return;
            }
            if (Prowess.Power(_content, s, "second-wind") is { } wind && Use(c, wind.Id) > 0 && c.Hp < s.MaxHp / 2)
            {
                c.Uses[wind.Id]--;
                int hp = Math.Max(1, (Sim.Dice.Parse(wind.Dice) ?? new Dice(1, 10, 0)).Roll(_rng) + s.Level);
                c.Hp = Math.Min(s.MaxHp, c.Hp + hp);
                Log(Prowess.Fill(wind.Text, ("hero", name), ("hp", hp.ToString())));
                return;
            }
            foreach (var id in new[] { "lay-on-hands", "poultice" })
            {
                if (Prowess.Power(_content, s, id) is not { } hands || c.Pool <= 0 || _hushed) continue;
                var patient = Party.Where(p => !p.Dead && (p.Down || p.Hp < p.Sheet.MaxHp * 0.35)).OrderByDescending(p => p.Down).ThenBy(p => p.Hp).ThenBy(p => p.Agent.Id).FirstOrDefault();
                if (patient == null) continue;
                int hp = Math.Min(c.Pool, Math.Max(1, patient.Sheet.MaxHp / 2 - patient.Hp));
                c.Pool -= hp;
                patient.Hp = Math.Min(patient.Sheet.MaxHp, patient.Hp + hp);
                patient.Down = false;
                Log(Prowess.Fill(hands.Text, ("hero", name), ("ally", patient == c ? "themself" : patient.Agent.GivenName), ("hp", hp.ToString())));
                return;
            }
            if (Prowess.Power(_content, s, "inspiration") is { } song && opening && Use(c, song.Id) > 0 && Worth(living))
            {
                var ally = Standing.Where(p => p != c && p.Inspired == 0).OrderByDescending(p => Prowess.Attacks(_content, p.Sheet)).ThenByDescending(p => p.Attack).ThenBy(p => p.Agent.Id).FirstOrDefault();
                if (ally != null)
                {
                    c.Uses[song.Id]--;
                    ally.Inspired = Prowess.At(song.Die, s.Level);
                    Log(Prowess.Fill(song.Text, ("hero", name), ("ally", ally.Agent.GivenName)));
                    return;
                }
            }
            if (Prowess.Power(_content, s, "wild-shape") is { } wild && opening && c.Form == null && Use(c, wild.Id) > 0 && Worth(living)
                && (Party.Any(p => p != c && !p.Dead && p.Sheet.Cls.Healer) || !Party.Any(p => !p.Dead && p.Hp < p.Sheet.MaxHp / 2))
                && wild.Shapes.Where(x => x.Level <= s.Level).LastOrDefault() is { } shape && _content.Library.Monster(shape.Ref) is { Damage: not null } beast)
            {
                c.Uses[wild.Id]--;
                c.Form = beast;
                c.Shape = beast.Hp;
                Log(Prowess.Fill(wild.Text, ("hero", name), ("shape", $"{Article(Library.Prose(beast.Name))} {Library.Prose(beast.Name)}")));
                return;
            }
            if (Prowess.Power(_content, s, "innate-sorcery") is { } innate && opening && c.Focus == 0 && Use(c, innate.Id) > 0 && (living.Any(f => f.Def.Boss) || living.Count >= Rules.SpendFoes))
            {
                c.Uses[innate.Id]--;
                c.Focus = innate.Hit;
                Log(Prowess.Fill(innate.Text, ("hero", name)));
                return;
            }
            Mark(c, living, round);
        }

        /// <summary>The ranger's favoured quarry (free uses of hunter's mark) or a warlock's hex on the master of the place.</summary>
        private void Mark(Combatant c, List<Foe> living, int round)
        {
            var s = c.Sheet;
            if (c.MarkOn is { Hp: > 0 }) return;
            bool had = c.MarkOn != null;
            var boss = living.FirstOrDefault(f => f.Def.Boss);
            LibSpell? spell = null;
            if (Prowess.Power(_content, s, "favored-enemy") is { } fe && _content.Library.Spell(fe.Spell) is { } hm && (had || Use(c, fe.Id) > 0) && Worth(living))
            {
                if (!had) c.Uses[fe.Id]--;
                spell = hm;
                if (!had) Log(Prowess.Fill(fe.Text, ("hero", c.Agent.GivenName), ("foe", The((boss ?? Aim(c, living, hm.DamageType, true, false, 6)).Def))));
            }
            else if (!_hushed && (had || (boss != null && c.Slots > 0)) && Prowess.Castable(_content, s).FirstOrDefault(k => k.Play.Kind == "mark").Spell is { } hex && c.Form == null)
            {
                if (!had) c.Slots--;
                spell = hex;
                if (!had) Log($"{c.Agent.GivenName} lays {Library.Prose(hex.Name)} on {The(boss!.Def)}.");
            }
            if (spell == null) return;
            c.MarkOn = had ? Aim(c, living, spell.DamageType, true, false, 6) : boss ?? Aim(c, living, spell.DamageType, true, false, 6);
            c.MarkDice = spell.Dice ?? new Dice(1, 6, 0);
            c.MarkType = spell.DamageType;
        }

        // ---- Actions: heals, the spell the fight needs, turning the dead ----

        /// <summary>A healer with a slot heals the worst hurt: one friend, or several at once when more than one is bleeding.</summary>
        private bool HealTurn(Combatant c)
        {
            var s = c.Sheet;
            if (!s.Cls.Healer || c.Slots <= 0 || _hushed || c.Form != null) return false;
            var hurt = Party.Where(p => !p.Dead && p.Hp < p.Sheet.MaxHp * 0.4).OrderBy(p => p.Hp).ThenBy(p => p.Agent.Id).ToList();
            if (hurt.Count == 0) return false;
            int top = Prowess.SlotTop(s.Cls, s.Level);
            var heals = Prowess.Castable(_content, s).Where(k => k.Play.Kind == "heal").ToList();
            var pick = heals
                .Where(k => hurt.Count >= 2 || k.Play.Targets <= 1)
                .OrderByDescending(k => Prowess.Avg(Prowess.DiceAt(k.Play, k.Spell, Prowess.CastAt(k.Spell, top)) ?? new Dice(1, 8, 0)) * Math.Min(hurt.Count, k.Play.Targets))
                .ThenBy(k => k.Play.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            var spell = pick.Spell ?? s.Cls.HealSpell!;
            var dice = pick.Spell != null ? Prowess.DiceAt(pick.Play, pick.Spell, Prowess.CastAt(pick.Spell, top)) ?? new Dice(1, 8, 0) : spell.Dice ?? new Dice(1, 8, 0);
            int healed = Math.Max(1, dice.Roll(_rng) + Rng.Modifier(c.Agent.Abilities[s.Cls.Casting]));
            var patients = hurt.Take(pick.Spell != null ? pick.Play.Targets : 1).ToList();
            var raised = new List<string>();
            foreach (var p in patients)
            {
                if (p.Down) raised.Add(p.Agent.GivenName);
                p.Hp = Math.Min(p.Sheet.MaxHp, Math.Max(0, p.Hp) + healed);
                p.Down = false;
            }
            c.Slots--;
            if (patients.Count > 1)
            {
                Log($"{c.Agent.GivenName} casts {Library.Prose(spell.Name)} over {ListNames(patients.Select(p => p.Agent.GivenName).ToList())} (+{healed} hp each).");
                Mark("heal", c);
            }
            else if (raised.Count > 0)
            {
                Log($"{c.Agent.GivenName} drags {raised[0]} back from the brink with {Library.Prose(spell.Name)} (+{healed} hp).");
                Mark("heal", c);
            }
            return true;
        }

        /// <summary>A cleric raises the holy symbol against the dead: those who fail flee until struck, seared as they go.</summary>
        private bool TurnUndead(Combatant c, List<Foe> foes, int round)
        {
            var s = c.Sheet;
            if (Prowess.Power(_content, s, "turn-undead") is not { } turn || Use(c, turn.Id) <= 0 || _hushed || c.Form != null) return false;
            var dead = foes.Where(f => f.Hp > 0 && !f.Held(round) && (f.Def.Undead || f.Type == "undead")).ToList();
            if (dead.Count < 2 && !dead.Any(f => f.Def.Boss)) return false;
            c.Uses[turn.Id]--;
            int dc = Dc(c);
            var fled = new List<string>();
            int felled = 0;
            foreach (var f in dead)
            {
                if (Saves(f, turn.Save, dc)) continue;
                if (s.Level >= turn.Sear)
                {
                    Hit(f, _rng.Roll(Math.Max(1, Rng.Modifier(c.Agent.Abilities.Wis)), 8), "radiant", true, false, foes, round);
                    if (f.Hp <= 0)
                    {
                        felled++;
                        continue;
                    }
                }
                f.HeldUntil = round + turn.Rounds;
                f.HeldBy = "turned";
                fled.Add(f.Def.Name);
            }
            Credit(c, felled);
            string line = Prowess.Fill(turn.Text, ("hero", c.Agent.GivenName));
            Log(fled.Count + felled == 0 ? $"{line} {Prowess.Line(_content, "missed", ("foes", "They"))}" : $"{line} {Cap(Count(fled, felled))}.");
            Mark("spell", c);
            return true;
        }

        private static string Count(List<string> fled, int felled)
        {
            var parts = new List<string>();
            if (fled.Count > 0) parts.Add($"{fled.Count} flee{(fled.Count == 1 ? "s" : "")}");
            if (felled > 0) parts.Add($"{felled} burn{(felled == 1 ? "s" : "")} away");
            return string.Join(" and ", parts);
        }

        /// <summary>A reliquarian's binding rite on the master of the place: it loses its next turns if it fails.</summary>
        private bool Rite(Combatant c, List<Foe> foes, int round)
        {
            if (Prowess.Power(_content, c.Sheet, "binding-rite") is not { } rite || Use(c, rite.Id) <= 0 || _hushed || round > 2) return false;
            var boss = foes.FirstOrDefault(f => f.Def.Boss && f.Hp > 0 && !f.Held(round));
            if (boss == null) return false;
            c.Uses[rite.Id]--;
            bool saved = Saves(boss, rite.Save, Dc(c));
            if (!saved)
            {
                boss.HeldUntil = round + rite.Rounds;
                boss.HeldBy = "bound";
            }
            Log($"{Prowess.Fill(rite.Text, ("hero", c.Agent.GivenName), ("foe", The(boss.Def)))} {(saved ? Prowess.Line(_content, "missed", ("foes", Cap(The(boss.Def)))).Replace("shake", "shakes") : Prowess.Line(_content, "held", ("foes", Cap(The(boss.Def))), ("are", "is")))}");
            Mark("spell", c);
            return true;
        }

        /// <summary>
        /// A caster spends a slot when the fight calls for it: a blessing or haste before a boss or a crowd, a blast
        /// into a crowd (or a spell that stops them), a hold on the master of the place, then the heaviest spell at it.
        /// Healers keep a slot back for heals. Returns whether they cast.
        /// </summary>
        private bool CastTurn(Combatant c, List<Foe> foes, List<Foe> living, int round)
        {
            var s = c.Sheet;
            if (c.Form != null || _hushed || c.Slots <= (s.Cls.Healer ? Rules.KeepForHeals : 0)) return false;
            var known = Prowess.Castable(_content, s).Where(k => k.Spell.Level > 0).ToList();
            if (known.Count == 0) return false;
            var boss = living.FirstOrDefault(f => f.Def.Boss);
            bool crowd = living.Count >= Rules.SpendFoes;
            int top = Prowess.SlotTop(s.Cls, s.Level);

            // Steadying the party is for the master of the place or a fight with more blood in it than the party has.
            bool hard = boss != null || living.Sum(f => f.Hp) >= Standing.Sum(x => x.Hp);
            if (round <= 1 && !c.Buffed && hard && _buffs.Count < Rules.Buffs && Standing.Count >= 2)
            {
                var buff = known.Where(k => k.Play.Kind == "buff" && !_buffs.Contains(k.Play.Id)).OrderByDescending(k => k.Spell.Level).ThenBy(k => k.Play.Id, StringComparer.Ordinal).FirstOrDefault();
                if (buff.Spell != null)
                {
                    CastBuff(c, buff.Play, buff.Spell, top);
                    return true;
                }
            }
            if (crowd)
            {
                var blast = known.Where(k => k.Play.Kind is "blast" or "aura")
                    .Select(k => (k.Play, k.Spell, Value: Blast(k.Play, k.Spell, top, living)))
                    .Where(x => x.Value > 0)
                    .OrderByDescending(x => x.Value).ThenBy(x => x.Play.Id, StringComparer.Ordinal).FirstOrDefault();
                if (blast.Spell != null)
                {
                    if (blast.Play.Kind == "aura") CastAura(c, blast.Play, blast.Spell, top, round);
                    else CastBlast(c, blast.Play, blast.Spell, top, living, foes, round);
                    return true;
                }
                var hold = known.Where(k => k.Play.Kind == "hold" && k.Play.Targets >= 2 && Holdable(k.Play, k.Spell, top, living, round).Count >= 2)
                    .OrderByDescending(k => k.Spell.Level).ThenBy(k => k.Play.Id, StringComparer.Ordinal).FirstOrDefault();
                if (hold.Spell != null && round <= 2)
                {
                    CastHold(c, hold.Play, hold.Spell, top, living, round);
                    return true;
                }
            }
            if (boss != null)
            {
                if (round <= 2 && !boss.Held(round))
                {
                    var hold = known.Where(k => k.Play.Kind == "hold" && Holdable(k.Play, k.Spell, top, [boss], round).Count > 0 && k.Play.Effect == "held")
                        .OrderByDescending(k => k.Spell.Level).ThenBy(k => k.Play.Id, StringComparer.Ordinal).FirstOrDefault();
                    if (hold.Spell != null)
                    {
                        CastHold(c, hold.Play, hold.Spell, top, [boss], round);
                        return true;
                    }
                }
                var bolt = known.Where(k => k.Play.Kind is "bolt" or "aura" or "blast")
                    .Select(k => (k.Play, k.Spell, Value: k.Play.Kind == "bolt" ? Bolt(c, k.Play, k.Spell, top, boss) : Blast(k.Play, k.Spell, top, living)))
                    .OrderByDescending(x => x.Value).ThenBy(x => x.Play.Id, StringComparer.Ordinal).FirstOrDefault();
                if (bolt.Spell != null && bolt.Value > 1.5 * Everyday(c, boss))
                {
                    if (bolt.Play.Kind == "bolt") CastBolt(c, bolt.Play, bolt.Spell, top, boss, foes, round);
                    else if (bolt.Play.Kind == "aura") CastAura(c, bolt.Play, bolt.Spell, top, round);
                    else CastBlast(c, bolt.Play, bolt.Spell, top, living, foes, round);
                    return true;
                }
            }
            return false;
        }

        /// <summary>What the hero does to a foe on an ordinary turn, for weighing a slot against it.</summary>
        private double Everyday(Combatant c, Foe f)
        {
            var s = c.Sheet;
            if (s.Cls.Spell && Prowess.Castable(_content, s).Where(k => k.Play.Kind == "cantrip").Select(k => Cantrip(c, k.Play, k.Spell, f)).DefaultIfEmpty(0).Max() is double best && best > 0) return best;
            double avg = Prowess.Avg(s.Weapon?.Damage ?? new Dice(1, Math.Max(4, s.Cls.DamageDie), 0)) + Rng.Modifier(c.Agent.Abilities[s.Cls.Primary]);
            return avg * Prowess.Attacks(_content, s) * 0.6;
        }

        /// <summary>A blast's worth: its dice on as many foes as it reaches, through their defences, half on the ones that save.</summary>
        private double Blast(SpellPlay p, LibSpell sp, int top, List<Foe> living)
        {
            var d = Prowess.DiceAt(p, sp, Prowess.CastAt(sp, top));
            double each = Prowess.Avg(d) * 0.75 * (p.Kind == "aura" ? Math.Max(1, p.Rounds) * 0.6 : 1);
            return living.Take(Math.Max(1, p.Targets)).Sum(f => each * Prowess.Share(_content, f.Lib, sp.DamageType, true, false));
        }

        private double Bolt(Combatant c, SpellPlay p, LibSpell sp, int top, Foe f)
        {
            int slot = Prowess.CastAt(sp, top);
            int beams = p.Beams > 0 ? p.Beams + Math.Max(0, slot - sp.Level) : 1;
            var d = p.Beams > 0 ? sp.Dice : Prowess.DiceAt(p, sp, slot);
            return Prowess.Avg(d) * beams * (p.Auto ? 1 : 0.65) * Prowess.Share(_content, f.Lib, sp.DamageType, true, false);
        }

        private double Cantrip(Combatant c, SpellPlay p, LibSpell sp, Foe f)
        {
            int n = Prowess.CantripDice(_content, c.Sheet.Level);
            double avg = Prowess.Avg(sp.Dice) * n + (p.Beams > 0 && Prowess.Power(_content, c.Sheet, "agonizing-blast") != null ? n * Rng.Modifier(c.Agent.Abilities.Cha) : 0);
            return avg * (sp.Attack || sp.Save == "" ? 0.65 : 0.55) * Math.Min(Math.Max(1, p.Targets), 2) * Prowess.Share(_content, f.Lib, sp.DamageType, true, false);
        }

        private void CastBuff(Combatant c, SpellPlay p, LibSpell sp, int top)
        {
            int slot = Prowess.CastAt(sp, top);
            int extra = Math.Max(0, slot - sp.Level) * Math.Max(0, p.Upcast);
            c.Slots--;
            c.Buffed = true;
            _buffs.Add(p.Id);
            var who = Standing.ToList();
            List<Combatant> on;
            if (p.Attacks > 0)
                on = who.OrderByDescending(x => Prowess.Attacks(_content, x.Sheet)).ThenByDescending(x => x.Attack).ThenBy(x => x.Agent.Id).Take(p.Targets).ToList();
            else if (p.Ac > 0)
                on = who.OrderByDescending(x => x.Sheet.Cls.Role == "frontline").ThenBy(x => x.ArmorClass).ThenBy(x => x.Agent.Id).Take(p.Targets).ToList();
            else if (p.Hp > 0)
                on = who.OrderBy(x => x.Sheet.MaxHp).ThenBy(x => x.Agent.Id).Take(p.Targets + (p.Upcast == 1 ? extra : 0)).ToList();
            else
                on = who.OrderByDescending(x => x.Attack).ThenBy(x => x.Agent.Id).Take(p.Targets + extra).ToList();
            foreach (var x in on)
            {
                x.HitBuff = Math.Max(x.HitBuff, p.Hit);
                x.AcBuff += p.Ac;
                x.Haste += p.Attacks;
                if (p.Hp > 0) x.Temp = Math.Max(x.Temp, p.Hp + (p.Upcast > 1 ? extra : 0));
            }
            Log($"{c.Agent.GivenName} casts {Library.Prose(sp.Name)} on {(on.Count == who.Count && on.Count > 1 ? "the party" : ListNames(on.Select(x => x == c ? "themself" : x.Agent.GivenName).ToList()))}.");
            Mark("spell", c);
        }

        private void CastBlast(Combatant c, SpellPlay p, LibSpell sp, int top, List<Foe> living, List<Foe> foes, int round)
        {
            int slot = Prowess.CastAt(sp, top);
            var d = Prowess.DiceAt(p, sp, slot) ?? new Dice(2, 6, 0);
            var targets = living.OrderByDescending(f => Prowess.Share(_content, f.Lib, sp.DamageType, true, false) > 0).Take(Math.Max(1, p.Targets)).ToList();
            int dmg = d.Roll(_rng) + c.Copied * _content.Shrines.Scroll.CopyBonus;
            int felled = 0;
            foreach (var f in targets)
            {
                int each = dmg;
                if (sp.Attack)
                {
                    int roll = _rng.D20();
                    if (roll != 20 && (roll == 1 || roll + SpellAttack(c) < f.Def.Ac + Cover(f, round) + f.Ways.Ac)) continue;
                }
                else if (sp.Save != "" && Saves(f, sp.Save, Dc(c))) each /= 2;
                Hit(f, Spell(f, each), sp.DamageType, true, false, foes, round);
                if (f.Hp <= 0) felled++;
            }
            c.Slots--;
            Credit(c, felled);
            Log($"{c.Agent.GivenName} looses {Library.Prose(sp.Name)} into {targets.Count} of them for {dmg} damage{(felled > 0 ? $", dropping {felled}" : "")}.");
            Mark("spell", c);
        }

        private void CastBolt(Combatant c, SpellPlay p, LibSpell sp, int top, Foe target, List<Foe> foes, int round)
        {
            int slot = Prowess.CastAt(sp, top);
            int beams = p.Beams > 0 ? p.Beams + Math.Max(0, slot - sp.Level) : 1;
            var d = (p.Beams > 0 ? sp.Dice : Prowess.DiceAt(p, sp, slot)) ?? new Dice(3, 8, 0);
            int total = 0;
            for (int i = 0; i < beams && target.Hp > 0; i++)
            {
                bool crit = false;
                int dmg;
                if (p.Auto) dmg = d.Roll(_rng);
                else if (sp.Attack)
                {
                    int roll = _rng.D20();
                    if (roll != 20 && (roll == 1 || roll + SpellAttack(c) < target.Def.Ac + Cover(target, round) + target.Ways.Ac + (target.Held(round) ? -5 : 0))) continue;
                    crit = roll == 20;
                    dmg = d.Roll(_rng, crit);
                }
                else
                {
                    dmg = d.Roll(_rng);
                    if (sp.Save != "" && Saves(target, sp.Save, Dc(c))) dmg /= 2;
                }
                total += Hit(target, Spell(target, dmg + (i == 0 ? c.Copied * _content.Shrines.Scroll.CopyBonus : 0)), sp.DamageType, true, false, foes, round);
            }
            c.Slots--;
            if (target.Hp <= 0) Credit(c, 1);
            Log($"{c.Agent.GivenName} casts {Library.Prose(sp.Name)} at {The(target.Def)}{(total > 0 ? $" for {total}" : ", to no effect")}{(target.Hp <= 0 ? " and it goes down" : "")}.");
            Mark(target.Hp <= 0 && target.Def.Boss ? "kill" : "spell", c);
        }

        private void CastAura(Combatant c, SpellPlay p, LibSpell sp, int top, int round)
        {
            int slot = Prowess.CastAt(sp, top);
            c.Slots--;
            _auras.Add((c, Prowess.DiceAt(p, sp, slot) ?? new Dice(2, 8, 0), sp.DamageType, sp.Save, Dc(c), round + Math.Max(1, p.Rounds), Math.Max(1, p.Targets), sp.Name));
            Log($"{c.Agent.GivenName} calls up {Library.Prose(sp.Name)}.");
            Mark("spell", c);
        }

        /// <summary>The foes a hold can take: the right kind, not immune to what it lays, not too strong for it, not already held.</summary>
        private List<Foe> Holdable(SpellPlay p, LibSpell sp, int top, List<Foe> living, int round)
        {
            int slot = Prowess.CastAt(sp, top);
            int most = p.Most > 0 ? p.Most + Math.Max(0, slot - sp.Level) * p.Most / 3 : int.MaxValue;
            return living.Where(f => f.Hp > 0 && !f.Held(round) && f.HinderUntil < round
                    && (p.Types.Count == 0 || p.Types.Contains(f.Type))
                    && (p.Condition == "" || f.Lib?.ConditionImmune.Contains(p.Condition) != true)
                    && f.Hp <= most)
                .OrderByDescending(f => f.Def.Boss).ThenByDescending(Threat).ThenBy(f => living.IndexOf(f))
                .ToList();
        }

        private void CastHold(Combatant c, SpellPlay p, LibSpell sp, int top, List<Foe> living, int round)
        {
            int slot = Prowess.CastAt(sp, top);
            int n = p.Targets + (p.Targets == 1 ? Math.Max(0, slot - sp.Level) * Math.Max(0, p.Upcast) : 0);
            var targets = Holdable(p, sp, top, living, round).Take(n).ToList();
            int dc = Dc(c);
            List<string> caught = [], free = [];
            foreach (var f in targets)
            {
                if (sp.Save != "" && Saves(f, sp.Save, dc))
                {
                    free.Add(f.Def.Name);
                    continue;
                }
                if (p.Effect == "hindered")
                {
                    f.HinderUntil = round + p.Rounds;
                    f.Hinder = p.Hit;
                }
                else
                {
                    f.HeldUntil = round + p.Rounds;
                    f.HeldBy = p.Condition;
                }
                caught.Add(f.Def.Name);
            }
            c.Slots--;
            string line = $"{Prowess.Line(_content, "cast", ("hero", c.Agent.GivenName), ("spell", Library.Prose(sp.Name)))}.";
            if (caught.Count > 0) line += " " + Prowess.Line(_content, "held", ("foes", Cap(Names(caught))), ("are", caught.Count == 1 ? "is" : "are"));
            if (free.Count > 0) line += " " + Prowess.Line(_content, "missed", ("foes", Cap(Names(free)))).Replace(" shake ", free.Count == 1 ? " shakes " : " shake ");
            Log(line);
            Mark("spell", c);
        }

        /// <summary>"the goblin", "3 goblins", "the ogre and 2 goblins".</summary>
        private static string Names(List<string> names) =>
            ListNames(names.GroupBy(n => n).Select(g => g.Count() == 1 ? $"the {g.Key}" : $"{g.Count()} {Library.Plural(g.Key)}").ToList());

        // ---- Blows: weapons, beasts' jaws and cantrips, as many as their level gives them ----

        private void StrikeTurn(Combatant c, List<Foe> foes, int round)
        {
            var s = c.Sheet;
            bool caster = s.Cls.Spell && c.Form == null && !_hushed;
            int n = c.Form != null ? Math.Max(1, c.Form.Attacks) : caster ? 1 : Prowess.Attacks(_content, s);
            n += c.Haste;
            if (c.Form == null && Prowess.Power(_content, s, "martial-arts") != null) n++;
            var living = foes.Where(f => f.Hp > 0).ToList();
            // A fighter's action surge: twice the blows when the master of the place shows itself or the fight turns bad.
            if (Prowess.Power(_content, s, "action-surge") is { } surge && Use(c, surge.Id) > 0 && (living.Any(f => f.Def.Boss) || living.Count >= Rules.SpendFoes || HpFraction() < 0.5))
            {
                c.Uses[surge.Id]--;
                n *= 2;
                Log(Prowess.Fill(surge.Text, ("hero", c.Agent.GivenName)));
            }
            if (Prowess.Power(_content, s, "sworn-to-the-walls") is { Text.Length: > 0 } sworn && round == 1 && living.Any(f => f.Def.Boss) && !c.Struck)
                Log(Prowess.Fill(sworn.Text, ("hero", c.Agent.GivenName)));
            for (int i = 0; i < n; i++)
            {
                if (!foes.Any(f => f.Hp > 0)) return;
                if (caster && i < 1 && CantripAttack(c, foes, round)) continue;
                WeaponAttack(c, foes, round);
            }
        }

        /// <summary>
        /// Whom a hero swings at: a foe one blow would finish, else the one doing most harm for the blood left in it,
        /// skipping what their weapon can't hurt and what a spell already holds.
        /// </summary>
        private Foe Aim(Combatant c, List<Foe> living, string type, bool magic, bool silver, double avg)
        {
            Foe? best = null;
            double score = double.MinValue;
            int round = _round;
            foreach (var f in living)
            {
                double share = Prowess.Share(_content, f.Lib, type, magic, silver);
                double v = share <= 0 ? -1000 : f.Hp <= avg * share ? 1000 + Threat(f) : Threat(f) * share / Math.Max(1, f.Hp) * 100;
                if (f.Held(round) && f.Hp > avg * share) v *= 0.3;
                if (c.MarkOn == f) v *= 1.5;
                if (v > score)
                {
                    best = f;
                    score = v;
                }
            }
            return best ?? living[0];
        }

        /// <summary>The harm a foe does in a round, for whom to hit first.</summary>
        private static double Threat(Foe f) => Prowess.Avg(f.Def.Dice) * Math.Max(1, f.Def.Attacks) + (f.Ways.Power != null && !f.Powered ? f.Ways.Power.Avg / 2 : 0) + (f.Def.Boss ? 5 : 0);

        private int _round;

        private void WeaponAttack(Combatant c, List<Foe> foes, int round)
        {
            var s = c.Sheet;
            var rng = _rng;
            string name = c.Agent.GivenName;
            _round = round;
            var living = foes.Where(f => f.Hp > 0).ToList();
            var form = c.Form;
            string type = form != null ? (form.DamageType is { Length: > 0 } ft ? ft : "piercing") : s.Weapon?.DamageType is { Length: > 0 } wt ? wt : "bludgeoning";
            bool magic = form == null && Prowess.Magic(s);
            bool silver = form == null && s.Silvered;
            double avg = form != null ? Prowess.Avg(form.Damage) : Prowess.Avg(s.Weapon?.Damage ?? new Dice(1, Math.Max(4, s.Cls.DamageDie), 0)) + Rng.Modifier(c.Agent.Abilities[s.Cls.Primary]);
            var target = Aim(c, living, type, magic, silver, avg);
            int roll = rng.D20();
            int atk = (form != null ? form.ToHit + c.Edge : c.Attack) + c.HitBuff;
            if (roll == 1)
            {
                if (form == null && rng.Chance(0.3))
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
                return;
            }
            int snare = _snared.TryGetValue(c, out var sn) && round <= sn.Until ? sn.Hit : 0;
            int edge = target.Held(round) ? 5 : target.HinderUntil >= round ? target.Hinder : 0;
            int need = target.Def.Ac + Cover(target, round) + target.Ways.Ac;
            int got = roll + atk - snare - (target.Ways["aura"]?.Hit ?? 0) + edge;
            if (roll != 20 && got < need && c.Inspired > 0 && got + c.Inspired >= need)
            {
                int die = rng.Roll(1, c.Inspired);
                c.Inspired = 0;
                if (got + die >= need)
                {
                    got += die;
                    if (Rules.Powers.FirstOrDefault(p => p.Id == "inspiration") is { Used.Length: > 0 } song) Log(Prowess.Fill(song.Used, ("ally", name), ("roll", die.ToString())));
                }
            }
            if (roll != 20 && got < need) return;
            bool crit = roll == 20;
            int dmg = form != null ? form.Damage!.Value.Roll(rng, crit) : Gear.Damage(_content, rng, c.Agent, crit);
            if (c.Raging && form == null && Prowess.Power(_content, s, "rage") is { } rage) dmg += Prowess.At(rage.Bonus, s.Level);
            if (s.Cls.Sneak && Standing.Count > 1 && !c.Sneaked)
            {
                dmg += rng.Roll((int)Math.Ceiling(s.Level / 2.0), 6);
                c.Sneaked = true;
            }
            // A blade passes half through a ghost (unless the library already says what blades do to it).
            if (target.Ways["incorporeal"] is { } ghost && !(target.Lib is { } gl && (gl.Resist.Any(Prowess.IsPhysical) || gl.Immune.Any(Prowess.IsPhysical))))
            {
                dmg = (int)Math.Round(dmg * ghost.Share);
                Tell(target, ghost);
            }
            dmg = Hit(target, Math.Max(1, dmg), type, magic, silver, foes, round);
            dmg += Extras(c, target, foes, round, crit, false);
            if (target.Hp <= 0) Credit(c, 1);
            Told(c, target, roll, atk, dmg);
            Stun(c, target, round);
        }

        /// <summary>A spell class's everyday attack: the cantrip that suits the foe best, more dice (or rays) as they level.</summary>
        private bool CantripAttack(Combatant c, List<Foe> foes, int round)
        {
            var s = c.Sheet;
            var rng = _rng;
            _round = round;
            var living = foes.Where(f => f.Hp > 0).ToList();
            var cantrips = Prowess.Castable(_content, s).Where(k => k.Play.Kind == "cantrip" && k.Spell.Dice != null).ToList();
            if (cantrips.Count == 0) return false;
            var pick = cantrips
                .Select(k => (k.Play, k.Spell, Target: Aim(c, living, k.Spell.DamageType, true, false, Prowess.Avg(k.Spell.Dice) * Prowess.CantripDice(_content, s.Level))))
                .Select(x => (x.Play, x.Spell, x.Target, Value: Cantrip(c, x.Play, x.Spell, x.Target)))
                .OrderByDescending(x => x.Value).ThenBy(x => x.Play.Id, StringComparer.Ordinal).First();
            var (play, sp, target) = (pick.Play, pick.Spell, pick.Target);
            int n = Prowess.CantripDice(_content, s.Level);
            int rays = play.Beams > 0 ? n : 1;
            var d = sp.Dice!.Value with { Count = play.Beams > 0 ? sp.Dice.Value.Count : sp.Dice.Value.Count * n };
            int agonize = play.Beams > 0 && Prowess.Power(_content, s, "agonizing-blast") != null ? Math.Max(0, Rng.Modifier(c.Agent.Abilities.Cha)) : 0;
            int total = 0;
            int lastRoll = 0;
            var hit = new List<Foe>();
            for (int r = 0; r < rays; r++)
            {
                if (target.Hp <= 0)
                {
                    living = foes.Where(f => f.Hp > 0).ToList();
                    if (living.Count == 0) break;
                    target = Aim(c, living, sp.DamageType, true, false, Prowess.Avg(d));
                }
                foreach (var f in new[] { target }.Concat(living.Where(x => x != target && x.Hp > 0).Take(Math.Max(1, play.Targets) - 1)).ToList())
                {
                    bool crit = false;
                    if (sp.Attack || sp.Save == "")
                    {
                        int roll = rng.D20();
                        lastRoll = roll;
                        if (roll == 1)
                        {
                            if (rng.Chance(0.3))
                            {
                                Log($"{c.Agent.GivenName} rolls a natural 1 and nearly loses {s.Cls.Fumble}.");
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
                        int edge = f.Held(round) ? 5 : f.HinderUntil >= round ? f.Hinder : 0;
                        if (roll != 20 && roll + SpellAttack(c) - snare - (f.Ways["aura"]?.Hit ?? 0) + edge < f.Def.Ac + Cover(f, round) + f.Ways.Ac) continue;
                        crit = roll == 20;
                    }
                    else if (Saves(f, sp.Save, Dc(c))) continue;
                    int dmg = Math.Max(1, d.Roll(rng, crit) + agonize + Gear.Flat(_content, c.Agent));
                    int dealt = Hit(f, Spell(f, dmg), sp.DamageType, true, false, foes, round);
                    dealt += Extras(c, f, foes, round, crit, true);
                    if (f.Hp <= 0) Credit(c, 1);
                    if (f == target) total += dealt;
                    if (crit) Told(c, f, 20, SpellAttack(c), dealt);
                    else if (f.Hp <= 0 && f.Def.Boss) Told(c, f, lastRoll, SpellAttack(c), dealt);
                }
            }
            return true;
        }

        /// <summary>The tale's line for a natural 20 or the blow that fells the master of the place.</summary>
        private void Told(Combatant c, Foe target, int roll, int atk, int dmg)
        {
            string name = c.Agent.GivenName;
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

        /// <summary>What rides on a blow that lands: a paladin's smite, a cleric's blessed strike, the hunter's mark.</summary>
        private int Extras(Combatant c, Foe f, List<Foe> foes, int round, bool crit, bool spell)
        {
            var s = c.Sheet;
            int more = 0;
            if (f.Hp <= 0) return 0;
            if (!spell && !c.Smote && Prowess.Power(_content, s, "smite") is { } smite && c.Slots > (s.Cls.Healer ? Rules.KeepForHeals : 0) && !_hushed
                && (f.Def.Boss || f.Type is "undead" or "fiend" || f.Hp > Everyday(c, f)))
            {
                int top = Prowess.SlotTop(s.Cls, s.Level);
                var d = Sim.Dice.Parse(smite.Dice) ?? new Dice(2, 8, 0);
                d = d with { Count = d.Count + Math.Max(0, (top + 1) / 2 - 1) + (f.Type is "undead" or "fiend" ? Prowess.At(smite.Bonus, s.Level) : 0) };
                c.Slots--;
                c.Smote = true;
                int dealt = Hit(f, d.Roll(_rng, crit), "radiant", true, false, foes, round);
                more += dealt;
                Log(Prowess.Fill(smite.Text, ("hero", c.Agent.GivenName), ("foe", The(f.Def)), ("dmg", dealt.ToString())));
            }
            if (f.Hp > 0 && !c.Struck && Prowess.Powers(_content, s).FirstOrDefault(p => p.Type != "" && p.Dice != "") is { } strike)
            {
                c.Struck = true;
                more += Hit(f, (Sim.Dice.Parse(strike.Dice) ?? new Dice(1, 8, 0)).Roll(_rng, crit), strike.Type, true, false, foes, round);
            }
            if (f.Hp > 0 && c.MarkOn == f && c.MarkType != "")
                more += Hit(f, c.MarkDice.Roll(_rng, crit), c.MarkType, true, false, foes, round);
            return more;
        }

        /// <summary>A monk's stunning strike on something worth stopping: it saves with Constitution or loses its next turn.</summary>
        private void Stun(Combatant c, Foe f, int round)
        {
            if (f.Hp <= 0 || f.Held(round + 1) || Prowess.Power(_content, c.Sheet, "stunning-strike") is not { } stun || Use(c, stun.Id) <= 0) return;
            if (!f.Def.Boss && f.Hp < 20) return;
            if (f.Lib?.ConditionImmune.Contains("stunned") == true) return;
            c.Uses[stun.Id]--;
            int dc = 8 + Proficiency(c.Sheet.Level) + Rng.Modifier(c.Agent.Abilities.Wis);
            if (Saves(f, stun.Save, dc)) return;
            f.HeldUntil = round + 1;
            f.HeldBy = "stunned";
            if (f.Told.Add("stunned")) Log(Prowess.Fill(stun.Text, ("hero", c.Agent.GivenName), ("foe", The(f.Def))));
        }

        // ---- Foes: whom they go for, and what the heroes' powers take off their blows ----

        /// <summary>
        /// Whom a foe hits, by its wits: a beast goes for whoever stands in front (a warden who dares it most), a clever
        /// foe for the casters, the healers, the bloodied and the lightly armoured. One draw on the dice either way.
        /// </summary>
        private Combatant Victim(Foe f, List<Combatant> targets)
        {
            var r = Rules.Targeting;
            int wits = f.Lib?.Int ?? (f.Def.Int > 0 ? f.Def.Int : 10);
            var weights = new int[targets.Count];
            int total = 0;
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                bool front = t.Form != null || t.Sheet.Cls.Role == "frontline";
                int w;
                if (wits >= r.Clever)
                {
                    bool soft = t.Form == null && (t.Sheet.Cls.Spell || t.Sheet.Cls.Healer || t.Sheet.Cls.Magic == "full");
                    w = (soft ? r.CleverSoft : 1) * (t.Hp < t.Sheet.MaxHp / 2 ? r.CleverBloodied : 1) * (t.ArmorClass <= r.CleverAc ? 2 : 1);
                }
                else
                {
                    w = front ? (wits <= r.Dim ? r.BeastFront : r.DimFront) : 1;
                    if (Prowess.Power(_content, t.Sheet, "sworn-to-the-walls") is { Weight: > 0 } sworn) w *= sworn.Weight;
                }
                weights[i] = Math.Max(1, w);
                total += weights[i];
            }
            int pick = _rng.Range(0, total);
            for (int i = 0; i < targets.Count; i++)
            {
                pick -= weights[i];
                if (pick < 0) return targets[i];
            }
            return targets[^1];
        }

        /// <summary>A blow on a hero, less what their powers take off it: a rage turns steel, a rogue rolls with the first blow a round.</summary>
        private int Taken(Combatant t, Foe f, int dmg, int round)
        {
            string type = f.Lib?.DamageType is { Length: > 0 } dt ? dt : "bludgeoning";
            if (t.Raging && t.Form == null && Prowess.IsPhysical(type)) dmg = Math.Max(1, dmg / 2);
            if (t.Dodged != round && t.Form == null && Prowess.Power(_content, t.Sheet, "uncanny-dodge") is { } dodge)
            {
                t.Dodged = round;
                dmg = Math.Max(1, dmg / 2);
                if (!_said.Contains(t.Agent.Id))
                {
                    _said.Add(t.Agent.Id);
                    Log(Prowess.Fill(dodge.Text, ("hero", t.Agent.GivenName)));
                }
            }
            return dmg;
        }

        /// <summary>Heroes who have said their piece about a power this delve.</summary>
        private readonly HashSet<int> _said = [];

        /// <summary>A raging barbarian of high enough level stays on their feet: a Constitution save, harder each time.</summary>
        private void Relentless(Combatant t)
        {
            if (!t.Down || !t.Raging || Prowess.Power(_content, t.Sheet, "rage") is not { Rises: > 0 } rage || t.Sheet.Level < rage.Rises) return;
            int dc = 10 + 5 * t.Rose;
            t.Rose++;
            if (_rng.D20() + Rng.Modifier(t.Agent.Abilities.Con) + Proficiency(t.Sheet.Level) < dc) return;
            t.Down = false;
            t.Hp = 2 * t.Sheet.Level;
            Log(Prowess.Fill(rage.Rose, ("hero", t.Agent.GivenName)));
        }
    }
}
