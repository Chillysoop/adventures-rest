namespace CrownAndCompass.Sim.Systems;

/// <summary>Magic items' powers in a delve's fights (<see cref="Enchant"/>): what each hero's gear does beyond its +N.</summary>
public static partial class Expedition
{
    internal sealed partial class Combatant
    {
        /// <summary>Every power of every item the hero wears, with the item, read once for the delve.</summary>
        public List<(ItemPowerDef Power, LibItem Item)> Powers { get; init; } = [];

        /// <summary>Uses spent since the last long rest, and powers spent this fight, by their place in <see cref="Powers"/>.</summary>
        public readonly Dictionary<int, int> Used = [];
        public readonly HashSet<int> FightUsed = [];

        public IEnumerable<(int I, ItemPowerDef Power, LibItem Item)> Of(string kind)
        {
            for (int i = 0; i < Powers.Count; i++)
                if (Powers[i].Power.Kind == kind) yield return (i, Powers[i].Power, Powers[i].Item);
        }

        public bool Has(string kind) => Powers.Any(p => p.Power.Kind == kind);

        /// <summary>A power with a use left: per delve (blast, chain, heal...) or, for <paramref name="perFight"/>, per fight.</summary>
        public (int I, ItemPowerDef Power, LibItem Item)? Ready(string kind, bool perFight = false)
        {
            foreach (var x in Of(kind))
            {
                if (perFight ? FightUsed.Contains(x.I) && x.Power.Uses <= 1 : x.Power.Uses > 0 && Used.GetValueOrDefault(x.I) >= x.Power.Uses) continue;
                return x;
            }
            return null;
        }

        public void Spend(int i, bool perFight = false)
        {
            if (perFight) FightUsed.Add(i);
            else Used[i] = Used.GetValueOrDefault(i) + 1;
        }
    }

    private sealed partial class Foe
    {
        /// <summary>Pinned by a hero's magic (a web, a freezing blow) until the end of this round.</summary>
        public int HeldUntil;

        private bool _named;
        private string _element = "";

        /// <summary>The element its name says it is made of ("frost giant": cold), which the same element doesn't hurt.</summary>
        public string Element(Content c)
        {
            if (!_named)
            {
                _named = true;
                _element = Enchant.ElementOf(c, Def.Name)?.Id ?? "";
            }
            return _element;
        }
    }

    public sealed partial class Venture
    {
        /// <summary>
        /// A hoard's piece: plain +N arms rarer the higher the bonus, and pieces that belong in the site's country (cold in
        /// the frozen north, a boat on the coast, a grave-blade in a tomb: enchantments.json found) likelier than the rest.
        /// </summary>
        private LibItem Hoarded(List<LibItem> pool, SiteDef site)
        {
            var r = Enchant.Rules(_content);
            var favoured = Enchant.Favoured(_content, site.Habitats);
            double Weight(LibItem it)
            {
                if (Enchant.Basic(_content, it)) return r.Basic.Weight[Math.Clamp(Gear.BonusOf(_content, it), 0, r.Basic.Weight.Length - 1)];
                bool fits = (favoured.Count > 0 && Enchant.Elements(_content, it).Any(favoured.Contains))
                    || (r.Found.TryGetValue(it.Id, out var at) && at.Any(site.Habitats.Contains));
                return fits ? r.Affinity : _content.Loot.OffFit;
            }
            double total = Js.Sum(pool, Weight), x = _rng.Next() * total;
            foreach (var it in pool)
            {
                x -= Weight(it);
                if (x < 0) return it;
            }
            return pool[^1];
        }

        private static string Its(Combatant c, LibItem it) => $"{c.Agent.GivenName}'s {Library.Prose(it.Name)}";

        /// <summary>Damage of an element on a foe: nothing if the foe is of that element.</summary>
        private int Burn(Foe f, string element, int dmg) => element != "" && f.Element(_content) == element ? 0 : dmg;

        private string ElementName(string id) => Enchant.Rules(_content).Element(id)?.Name ?? id;

        /// <summary>Whether a foe is of a kind a bane bites: its library type, or a word in its name.</summary>
        private static bool Bitten(Foe f, ItemPowerDef p)
        {
            string type = f.Def.Type.ToLowerInvariant(), name = Spoils.Plain(f.Def.Name);
            return p.Vs.Any(v => type.StartsWith(v, StringComparison.Ordinal) || Spoils.Named(name, v));
        }

        /// <summary>
        /// A hero's turn spent on an item: a blast or a chain into a crowd (or at a boss), a hold on the strongest foe,
        /// the weak sent running, or a hurt friend mended. Returns whether it took the turn.
        /// </summary>
        private bool ItemAction(Combatant c, List<Foe> foes, List<Foe> living, int round)
        {
            if (c.Powers.Count == 0) return false;
            string name = c.Agent.GivenName;
            bool crowd = living.Count >= 2 || living.Any(f => f.Def.Boss);

            // Mend a friend near the brink first.
            if (c.Ready("heal") is { } heal && Party.Where(p => !p.Dead && p.Hp < p.Sheet.MaxHp * 0.35).OrderBy(p => p.Hp).ThenBy(p => p.Agent.Id).FirstOrDefault() is { } patient)
            {
                c.Spend(heal.I);
                int hp = Math.Max(1, (heal.Power.Roll ?? new Dice(2, 8, 0)).Roll(_rng));
                bool wasDown = patient.Down;
                patient.Hp = Math.Min(patient.Sheet.MaxHp, patient.Hp + hp);
                patient.Down = false;
                Log($"{Its(c, heal.Item)} glows, and {(patient == c ? "mends its bearer" : wasDown ? $"pulls {patient.Agent.GivenName} back from the brink" : $"mends {patient.Agent.GivenName}")} (+{hp} hp).");
                Mark("heal", c);
                return true;
            }

            if (crowd && (c.Ready("blast") ?? c.Ready("chain")) is { } fire)
            {
                c.Spend(fire.I);
                var p = fire.Power;
                var dice = p.Roll ?? new Dice(3, 6, 0);
                int felled = 0, shrugged = 0;
                if (p.Kind == "blast")
                {
                    var targets = living.OrderByDescending(f => f.Def.Boss).ThenByDescending(f => f.Hp).Take(Math.Max(1, p.Targets)).ToList();
                    int dmg = Math.Max(1, dice.Roll(_rng));
                    foreach (var f in targets)
                    {
                        int hit = Burn(f, p.Element, Spell(f, dmg));
                        if (hit == 0) shrugged++;
                        f.Hp -= hit;
                        Struck(f, foes);
                        if (f.Hp <= 0) felled++;
                    }
                    Credit(c, felled);
                    Log($"{Its(c, fire.Item)} looses {(p.Element != "" ? ElementName(p.Element) : "raw force")} into {targets.Count} of them for {dmg} damage{(felled > 0 ? $", dropping {felled}" : "")}{(shrugged > 0 ? $"; {shrugged} shrug it off" : "")}.");
                }
                else
                {
                    // The bolt leaps from foe to foe, weaker at each.
                    var chain = living.OrderByDescending(f => f.Def.Boss).ThenByDescending(f => f.Hp).Take(Math.Max(1, p.Targets)).ToList();
                    int dmg = Math.Max(1, dice.Roll(_rng)), total = 0;
                    for (int i = 0; i < chain.Count; i++)
                    {
                        var f = chain[i];
                        int hit = Burn(f, p.Element, Spell(f, Math.Max(1, dmg >> i)));
                        total += hit;
                        f.Hp -= hit;
                        Struck(f, foes);
                        if (f.Hp <= 0) felled++;
                    }
                    Credit(c, felled);
                    Log($"{ElementName(p.Element) switch { "lightning" => "Lightning", var e => Stories.Cap(e) }} leaps from {Its(c, fire.Item)} through {chain.Count} of them, {total} damage in all{(felled > 0 ? $", dropping {felled}" : "")}.");
                }
                Mark("spell", c);
                return true;
            }

            if (c.Ready("hold") is { } hold && living.OrderByDescending(f => f.Def.Boss).ThenByDescending(f => f.Hp).FirstOrDefault(f => f.HeldUntil < round) is { } big && (big.Def.Boss || living.Count >= 2))
            {
                c.Spend(hold.I);
                // A master of its place shrugs off half of what would pin it.
                if (big.Def.Boss && !_rng.Chance(0.5))
                {
                    Log($"{Its(c, hold.Item)} reaches for {The(big.Def)}, and it tears free.");
                    return true;
                }
                big.HeldUntil = round + Math.Max(1, hold.Power.Rounds);
                Log($"{Its(c, hold.Item)} pins {The(big.Def)} where it stands for {(hold.Power.Rounds <= 1 ? "a round" : $"{hold.Power.Rounds} rounds")}.");
                Mark("spell", c);
                return true;
            }

            if (c.Ready("rout") is { } rout && living.Count(f => !f.Def.Boss) >= 2)
            {
                c.Spend(rout.I);
                var weak = living.Where(f => !f.Def.Boss && f.Hp <= f.Def.Hp).OrderBy(f => f.Hp).ThenBy(f => foes.IndexOf(f)).Take(Math.Max(1, rout.Power.Targets)).ToList();
                foreach (var f in weak) f.Hp = 0;
                Credit(c, weak.Count);
                Log($"{Its(c, rout.Item)} fills {weak.Count} of them with terror, and they break and run.");
                Mark("spell", c);
                return true;
            }
            return false;
        }

        /// <summary>What a hero's gear adds to a blow that lands: an element on every hit, a bane's bite, a critical's power.</summary>
        private int OnHit(Combatant c, Foe target, bool crit, List<Foe> foes)
        {
            if (c.Powers.Count == 0) return 0;
            int more = 0;
            foreach (var (i, p, it) in c.Of("strike"))
                more += Burn(target, p.Element, (p.Roll ?? new Dice(1, 6, 0)).Roll(_rng, crit));
            foreach (var (i, p, it) in c.Of("bane"))
            {
                if (!Bitten(target, p) || p.Uses > 0 && c.FightUsed.Contains(i)) continue;
                if (p.Uses > 0) c.FightUsed.Add(i);
                int bite = (p.Roll ?? new Dice(2, 6, 0)).Roll(_rng, crit);
                more += bite;
                if (!target.Told.Contains("bane:" + it.Id))
                {
                    target.Told.Add("bane:" + it.Id);
                    Log($"{Its(c, it)} flares at the touch of {The(target.Def)} (+{bite}).");
                }
            }
            if (crit)
                foreach (var (i, p, it) in c.Of("crit"))
                {
                    switch (p.Effect)
                    {
                        case "slay" when !target.Def.Boss:
                            more += target.Hp;
                            Log($"{Its(c, it)} takes {The(target.Def)} clean off its feet, and it does not get up.");
                            break;
                        case "hold":
                            target.HeldUntil = Math.Max(target.HeldUntil, Rounds() + Math.Max(1, p.Rounds));
                            Log($"{Its(c, it)} {(p.Element == "cold" ? "freezes" : p.Element == "radiant" || p.Element == "fire" ? "blinds" : "stuns")} {The(target.Def)} where it stands.");
                            break;
                        default:
                            more += (p.Roll ?? c.Sheet.Weapon?.Damage ?? new Dice(1, 8, 0)).Roll(_rng, true);
                            break;
                    }
                    break;
                }
            return more;
        }

        private int _round;

        /// <summary>The round the fight is in, for holds set from a blow.</summary>
        private int Rounds() => _round;

        /// <summary>A blade that drinks: the hero heals by a share of the blow.</summary>
        private void Drain(Combatant c, int dmg)
        {
            foreach (var (i, p, it) in c.Of("drain"))
            {
                int hp = (int)Math.Round(dmg * (p.Amount > 0 ? p.Amount : 0.5));
                if (hp <= 0 || c.Hp >= c.Sheet.MaxHp) continue;
                c.Hp = Math.Min(c.Sheet.MaxHp, c.Hp + hp);
                break;
            }
        }

        /// <summary>A heart that knits: the hurt hero heals a little at the start of each turn.</summary>
        private void Regen(Combatant c)
        {
            if (c.Hp >= c.Sheet.MaxHp) return;
            foreach (var (i, p, it) in c.Of("regen"))
            {
                c.Hp = Math.Min(c.Sheet.MaxHp, c.Hp + Math.Max(1, (p.Roll ?? new Dice(1, 6, 0)).Roll(_rng)));
                break;
            }
        }

        /// <summary>A shot that cannot miss, once a delve: it finds its mark anyway.</summary>
        private bool Sure(Combatant c, Foe target)
        {
            if (c.Ready("sure") is not { } sure) return false;
            c.Spend(sure.I);
            Log($"{Its(c, sure.Item)} bends the shot round to find {The(target.Def)}.");
            return true;
        }

        /// <summary>Boots or a blade of speed: another blow at the next foe standing.</summary>
        private void Haste(Combatant c, Foe was, List<Foe> foes, int atk)
        {
            if (!c.Has("haste")) return;
            var target = was.Hp > 0 ? was : foes.FirstOrDefault(f => f.Hp > 0);
            if (target == null) return;
            int roll = _rng.D20();
            if (roll == 1 || roll != 20 && roll + atk < target.Def.Ac + target.Ways.Ac) return;
            int dmg = Math.Max(1, Gear.Damage(_content, _rng, c.Agent, roll == 20));
            target.Hp -= dmg;
            Struck(target, foes);
            if (target.Hp <= 0) Credit(c, 1);
        }

        /// <summary>A shield that throws itself in the way: one blow a fight is halved.</summary>
        private int Guard(Combatant t, Foe f, int dmg)
        {
            if (dmg < 4 || t.Ready("guard", true) is not { } g) return dmg;
            t.Spend(g.I, true);
            Log($"{Its(t, g.Item)} throws itself between {t.Agent.GivenName} and {The(f.Def)}'s blow.");
            return Math.Max(1, dmg / 2);
        }

        /// <summary>Armour that bites back: the first foe to land a blow on its wearer each fight is frozen or burned in place.</summary>
        private void Rebuke(Combatant t, Foe f, int round)
        {
            if (t.Down || f.Def.Boss && !_rng.Chance(0.5)) return;
            if (t.Ready("rebuke", true) is not { } r) return;
            t.Spend(r.I, true);
            f.HeldUntil = Math.Max(f.HeldUntil, round + Math.Max(1, r.Power.Rounds));
            Log($"{Its(t, r.Item)} answers the blow: {The(f.Def)} is {(r.Power.Element == "cold" ? "frozen" : "held")} fast.");
        }

        /// <summary>Gear against a foe's power of its own element: half the harm.</summary>
        private double Resist(Combatant c, string power)
        {
            if (!c.Has("resist")) return 1;
            string el = Enchant.ElementOf(_content, power)?.Id ?? "";
            return el != "" && c.Of("resist").Any(x => x.Power.Element == el || x.Power.Element == "") ? 0.5 : 1;
        }

        /// <summary>No snare, hold or swallow takes a hero whose gear keeps them free.</summary>
        private static bool Free(Combatant c) => c.Has("free");

        /// <summary>What a hero's items add to a check of this kind: eyes for traps, a silver tongue for parley.</summary>
        private static int Knack(Combatant c, string kind)
        {
            int n = 0;
            foreach (var (_, p, _) in c.Of("knack"))
                if (p.Vs.Contains(kind)) n = Math.Max(n, (int)p.Amount);
            return n;
        }

        /// <summary>A bag that holds more than it should: the best of them carries home a share more of the gold.</summary>
        private void Haul()
        {
            if (Gold <= 0) return;
            var best = Party.Where(c => !c.Dead).SelectMany(c => c.Of("haul").Select(x => (C: c, x.Power, x.Item)))
                .OrderByDescending(x => x.Power.Amount).ThenBy(x => x.C.Agent.Id).FirstOrDefault();
            if (best.C == null) return;
            int more = (int)Math.Floor(Gold * best.Power.Amount);
            if (more <= 0) return;
            Gold += more;
            Log($"{Its(best.C, best.Item)} swallows {more} more gold than they could have carried.");
        }

        /// <summary>A ward that turns a foe's power back on it, once a fight: nobody in its path is touched.</summary>
        private bool Reflect(Foe f, List<Combatant> targets, string power, Dice? dice)
        {
            foreach (var c in targets)
            {
                if (c.Ready("reflect", true) is not { } r) continue;
                c.Spend(r.I, true);
                int back = dice is { } d ? Math.Max(1, d.Roll(_rng)) : 0;
                f.Hp -= back;
                Log($"{Its(c, r.Item)} catches {The(f.Def)}'s {power.ToLowerInvariant()} and throws it back{(back > 0 ? $" for {back} damage" : "")}.");
                Mark("power");
                return true;
            }
            return false;
        }

        /// <summary>Gear that warns of an ambush: the party is ready for the lurker.</summary>
        private bool Warned(Foe lurker)
        {
            var c = Standing.FirstOrDefault(h => h.Has("first"));
            if (c == null) return false;
            var it = c.Of("first").First().Item;
            Log($"{Its(c, it)} stirs before {The(lurker.Def)} can spring its ambush.");
            return true;
        }
    }
}
