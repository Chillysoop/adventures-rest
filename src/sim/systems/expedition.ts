import type { Agent, HeroSheet } from '../agent.ts';
import type { Ability, Content, EncounterDef, MonsterDef, SiteDef, SkillKind } from '../content.ts';
import { type Rng, modifier } from '../rng.ts';

/** 'the goblin', but just 'Grukk Bloodtusk' for a named foe. */
const the = (d: MonsterDef): string => (d.named ? d.name : `the ${d.name}`);

/**
 * The expedition resolver: a deterministic, seedable dice simulation of a party going into a
 * site, encounter by encounter. It never touches the town; it returns what happened and a
 * Chronicle written from the logged rolls, and the caller applies the results.
 */

export type ExpeditionOutcome = 'cleared' | 'retreated' | 'wiped';

export interface ExpeditionResult {
  outcome: ExpeditionOutcome;
  lines: string[];
  dead: number[];
  gold: number;
  xp: number;
  items: { heroId: number; item: string }[];
  kills: Map<number, number>;
}

interface Combatant {
  agent: Agent;
  sheet: HeroSheet;
  down: boolean;
  dead: boolean;
  heals: number;
}

interface Foe {
  def: MonsterDef;
  hp: number;
}

const SKILL_ABILITY: Record<SkillKind, Ability> = {
  trap: 'dex',
  hazard: 'con',
  puzzle: 'int',
  parley: 'cha',
  perception: 'wis',
};

const ITEMS = [
  { name: '+1 longsword', weapon: 1, armor: 0 },
  { name: 'a cloak of protection', weapon: 0, armor: 1 },
  { name: '+1 shield', weapon: 0, armor: 1 },
  { name: 'a flame tongue blade', weapon: 2, armor: 0 },
  { name: '+1 chain shirt', weapon: 0, armor: 1 },
  { name: 'a wand of magic missiles', weapon: 1, armor: 0 },
];

export const proficiency = (level: number): number => 2 + Math.floor((level - 1) / 4);

export function heroMaxHp(sheet: HeroSheet, con: number): number {
  const die = sheet.cls.hitDie;
  return Math.max(1, die + modifier(con) + (sheet.level - 1) * Math.max(1, die / 2 + 1 + modifier(con)));
}

export function heroAc(a: Agent): number {
  const s = a.hero!;
  return 10 + s.cls.armor + Math.min(2, Math.max(0, modifier(a.abilities.dex))) + s.armorBonus;
}

function attackBonus(a: Agent): number {
  const s = a.hero!;
  return proficiency(s.level) + modifier(a.abilities[s.cls.primary]) + s.weaponBonus;
}

const sign = (n: number): string => (n >= 0 ? `+${n}` : `${n}`);

class Run {
  readonly lines: string[] = [];
  readonly party: Combatant[];
  readonly kills = new Map<number, number>();
  gold = 0;
  xp = 0;
  skipNextCombat = false;
  readonly rng: Rng;
  readonly content: Content;

  constructor(rng: Rng, content: Content, heroes: Agent[]) {
    this.rng = rng;
    this.content = content;
    this.party = heroes.map((agent) => ({
      agent,
      sheet: agent.hero!,
      down: false,
      dead: false,
      heals: agent.hero!.cls.healer ? 1 + Math.floor(agent.hero!.level / 2) : 0,
    }));
  }

  get standing(): Combatant[] {
    return this.party.filter((c) => !c.down && !c.dead);
  }

  log(s: string): void {
    this.lines.push(s);
  }

  hpFraction(): number {
    let hp = 0;
    let max = 0;
    for (const c of this.party) {
      if (c.dead) continue;
      hp += Math.max(0, c.sheet.hp);
      max += c.sheet.maxHp;
    }
    return max === 0 ? 0 : hp / max;
  }

  /** Best member makes the check for the party. */
  bestCheck(kind: SkillKind): { who: Combatant; roll: number; total: number } {
    let best: { who: Combatant; roll: number; total: number } | null = null;
    for (const c of this.standing) {
      const bonus = this.skillBonus(c, kind);
      if (!best || bonus > best.total) best = { who: c, roll: 0, total: bonus };
    }
    const roll = this.rng.d20();
    return { who: best!.who, roll, total: roll + best!.total };
  }

  skillBonus(c: Combatant, kind: SkillKind): number {
    const ab = kind === 'hazard' ? Math.max(c.agent.abilities.con, c.agent.abilities.wis) : c.agent.abilities[SKILL_ABILITY[kind]];
    const cls = c.sheet.cls.skills[kind] ?? 0;
    return modifier(ab) + cls + (cls > 0 ? proficiency(c.sheet.level) : 0);
  }

  hurt(c: Combatant, dmg: number): void {
    c.sheet.hp -= dmg;
    if (c.sheet.hp <= 0) {
      c.sheet.hp = 0;
      c.down = true;
    }
  }

  encounter(e: EncounterDef): void {
    switch (e.kind) {
      case 'combat':
        if (this.skipNextCombat && !this.isBoss(e)) {
          this.skipNextCombat = false;
          this.log(`Thanks to the help they found, the party slips past ${e.text}.`);
          return;
        }
        this.combat(e, false);
        return;
      case 'trap': {
        const check = this.bestCheck('trap');
        const who = check.who.agent.givenName;
        if (check.total >= e.dc) {
          this.log(`${cap(e.text)}: ${who} spots it and disarms it (d20 ${check.roll}, ${check.total} vs DC ${e.dc}).`);
          this.xp += 25;
        } else {
          const victim = this.rng.pick(this.standing);
          const dmg = this.rng.roll(1, e.damageDie) + this.rng.roll(1, e.damageDie);
          this.hurt(victim, dmg);
          this.log(
            `${cap(e.text)}: ${who} misses it (d20 ${check.roll}, ${check.total} vs DC ${e.dc}) and ${victim.agent.givenName} takes ${dmg} damage` +
              (victim.down ? ' and drops.' : '.'),
          );
          if (victim.down) this.afterCombat();
        }
        return;
      }
      case 'hazard': {
        const hurt: string[] = [];
        for (const c of this.standing) {
          const roll = this.rng.d20();
          if (roll + this.skillBonus(c, 'hazard') < e.dc) {
            const dmg = this.rng.roll(1, e.damageDie);
            this.hurt(c, dmg);
            hurt.push(`${c.agent.givenName} (${dmg})`);
          }
        }
        this.log(hurt.length === 0 ? `${cap(e.text)}, but the party pushes through unscathed.` : `${cap(e.text)}. It costs them: ${hurt.join(', ')}.`);
        if (this.party.some((c) => c.down && !c.dead)) this.afterCombat();
        return;
      }
      case 'puzzle':
      case 'parley': {
        const check = this.bestCheck(e.kind === 'puzzle' ? 'puzzle' : 'parley');
        const who = check.who.agent.givenName;
        if (check.total >= e.dc) {
          const gold = this.rng.range(e.gold[0], e.gold[1] + 1);
          this.gold += gold;
          this.xp += 50;
          if (e.kind === 'parley') this.skipNextCombat = true;
          this.log(`${cap(e.text)}. ${who} handles it (d20 ${check.roll}, ${check.total} vs DC ${e.dc}): ${gold} gold${e.kind === 'parley' ? ' and a safer path' : ''}.`);
        } else {
          this.log(`${cap(e.text)}. ${who} gets nowhere (d20 ${check.roll}, ${check.total} vs DC ${e.dc}).`);
        }
        return;
      }
      case 'treasure': {
        const isMimic = this.rng.chance(e.mimicChance);
        if (isMimic) {
          const check = this.bestCheck('perception');
          const spotted = check.total >= 13;
          this.log(
            spotted
              ? `${cap(e.text)}. ${check.who.agent.givenName} notices the lid breathing (d20 ${check.roll}, ${check.total} vs DC 13). It's a mimic.`
              : `${cap(e.text)}. Nobody notices the lid breathing (d20 ${check.roll}, ${check.total} vs DC 13) until it bites. It's a mimic!`,
          );
          this.combat({ kind: 'combat', monster: 'mimic', count: [1, 1], text: 'the mimic' }, !spotted);
          if (this.standing.length === 0) return;
        }
        const gold = this.rng.range(e.gold[0], e.gold[1] + 1);
        this.gold += gold;
        this.log(isMimic ? `Inside the mimic's gullet: ${gold} gold.` : `${cap(e.text)}: ${gold} gold.`);
        return;
      }
    }
  }

  isBoss(e: EncounterDef): boolean {
    return e.kind === 'combat' && this.content.monster(e.monster).boss === true;
  }

  combat(e: Extract<EncounterDef, { kind: 'combat' }>, surprised: boolean): boolean {
    const rng = this.rng;
    const def = this.content.monster(e.monster);
    const foes: Foe[] = [];
    const n = rng.range(e.count[0], e.count[1] + 1);
    for (let i = 0; i < n; i++) foes.push({ def, hp: def.hp });
    if (e.escort && e.escortCount) {
      const ed = this.content.monster(e.escort);
      const en = rng.range(e.escortCount[0], e.escortCount[1] + 1);
      for (let i = 0; i < en; i++) foes.push({ def: ed, hp: ed.hp });
    }
    const label = n === 1 ? (def.named ? def.name : `${article(def.name)} ${def.name}`) : `${n} ${def.plural}`;
    if (def.id !== 'mimic') this.log(`${cap(e.text)}: ${label}${foes.length > n ? `, with ${foes.length - n} more at its side` : ''}.`);

    const totalXp = foes.reduce((s, f) => s + f.def.xp, 0);
    let round = 0;
    let monstersFirst = surprised;
    while (round < 20) {
      round++;
      if (!monstersFirst) this.heroesAct(foes, round);
      monstersFirst = false;
      if (foes.every((f) => f.hp <= 0)) break;
      this.monstersAct(foes);
      if (this.standing.length === 0) break;
      if (this.hpFraction() < (def.boss ? 0.15 : 0.2) && foes.some((f) => f.hp > 0)) {
        this.log(`Bloodied and outmatched, the party breaks off and runs.`);
        this.afterCombat();
        return false;
      }
    }

    const won = foes.every((f) => f.hp <= 0);
    if (won) {
      this.xp += totalXp;
      const hurt = this.standing.filter((c) => c.sheet.hp < c.sheet.maxHp / 3).map((c) => `${c.agent.givenName} (${c.sheet.hp}/${c.sheet.maxHp} hp)`);
      this.log(`The fight is over in ${round} round${round === 1 ? '' : 's'}.${hurt.length ? ` Badly hurt: ${hurt.join(', ')}.` : ''}`);
    }
    this.afterCombat();
    return won;
  }

  heroesAct(foes: Foe[], round: number): void {
    const rng = this.rng;
    for (const c of this.standing) {
      const living = foes.filter((f) => f.hp > 0);
      if (living.length === 0) return;
      const s = c.sheet;
      const name = c.agent.givenName;

      // Clerics keep the party alive before they swing.
      if (s.cls.healer && c.heals > 0) {
        const patient = this.party.filter((p) => !p.dead && p.sheet.hp < p.sheet.maxHp * 0.4).sort((a, b) => a.sheet.hp - b.sheet.hp)[0];
        if (patient) {
          const healed = rng.roll(1, 8) + modifier(c.agent.abilities.wis) + s.level;
          const wasDown = patient.down;
          patient.sheet.hp = Math.min(patient.sheet.maxHp, patient.sheet.hp + Math.max(1, healed));
          patient.down = false;
          c.heals--;
          if (wasDown) this.log(`${name} drags ${patient.agent.givenName} back from the brink with a prayer (+${healed} hp).`);
          continue;
        }
      }

      // Wizards open with an area spell.
      if (s.cls.area && round === 1 && living.length >= 2) {
        const targets = living.slice(0, 3);
        const dmg = rng.roll(2, 6) + s.level + modifier(c.agent.abilities.int);
        let felled = 0;
        for (const f of targets) {
          f.hp -= dmg;
          if (f.hp <= 0) felled++;
        }
        this.credit(c, felled);
        this.log(`${name} looses a burning hands spell into ${targets.length} of them for ${dmg} damage${felled ? `, dropping ${felled}` : ''}.`);
        continue;
      }

      const target = living[0];
      const roll = rng.d20();
      const atk = attackBonus(c.agent);
      if (roll === 1) {
        if (rng.chance(0.3)) this.log(`${name} rolls a natural 1 and nearly loses ${pronounWeapon(s.cls.id)}.`);
        continue;
      }
      if (roll === 20 || roll + atk >= target.def.ac) {
        let dmg = rng.roll(roll === 20 ? 2 : 1, s.cls.damageDie) + modifier(c.agent.abilities[s.cls.primary]) + s.weaponBonus;
        if (s.cls.sneak && this.standing.length > 1) dmg += rng.roll(Math.ceil(s.level / 2), 6);
        dmg = Math.max(1, dmg);
        target.hp -= dmg;
        if (target.hp <= 0) this.credit(c, 1);
        if (roll === 20) this.log(`Natural 20! ${name} hits ${the(target.def)} for ${dmg}${target.hp <= 0 ? ' and it goes down' : ''}.`);
        else if (target.hp <= 0 && target.def.boss) this.log(`${name} lands the killing blow on ${the(target.def)} (d20 ${roll}${sign(atk)}, ${dmg} damage).`);
      }
    }
  }

  credit(c: Combatant, n: number): void {
    if (n > 0) this.kills.set(c.agent.id, (this.kills.get(c.agent.id) ?? 0) + n);
  }

  monstersAct(foes: Foe[]): void {
    const rng = this.rng;
    for (const f of foes) {
      if (f.hp <= 0) continue;
      const targets = this.standing;
      if (targets.length === 0) return;
      const t = rng.pick(targets);
      const roll = rng.d20();
      if (roll === 1) continue;
      if (roll === 20 || roll + f.def.attack >= heroAc(t.agent)) {
        const dmg = rng.roll(roll === 20 ? 2 : 1, f.def.damageDie) + f.def.damageBonus;
        this.hurt(t, dmg);
        if (t.down) this.log(`${cap(the(f.def))} ${roll === 20 ? 'crits' : 'hits'} ${t.agent.givenName} for ${dmg}. ${t.agent.givenName} goes down.`);
      }
    }
  }

  /** Anyone left at 0 hp makes death saves unless a cleric can reach them. */
  afterCombat(): void {
    for (const c of this.party) {
      if (!c.down || c.dead) continue;
      const healer = this.standing.find((h) => h.sheet.cls.healer && h.heals > 0);
      if (healer) {
        healer.heals--;
        c.down = false;
        c.sheet.hp = Math.max(1, this.rng.roll(1, 8) + healer.sheet.level);
        this.log(`${healer.agent.givenName} stabilizes ${c.agent.givenName}.`);
        continue;
      }
      if (this.standing.length === 0) {
        c.dead = true;
        continue;
      }
      let ok = 0;
      let fail = 0;
      const rolls: number[] = [];
      while (ok < 3 && fail < 3) {
        const r = this.rng.d20();
        rolls.push(r);
        if (r === 20) {
          ok = 3;
        } else if (r === 1) fail += 2;
        else if (r >= 10) ok++;
        else fail++;
      }
      if (fail >= 3) {
        c.dead = true;
        this.log(`${c.agent.givenName} fails their death saves (${rolls.join(', ')}) and dies.`);
      } else {
        c.down = false;
        c.sheet.hp = 1;
        this.log(`${c.agent.givenName} pulls through on death saves (${rolls.join(', ')}).`);
      }
    }
  }
}

function pronounWeapon(cls: string): string {
  return cls === 'wizard' ? 'the spellbook' : cls === 'cleric' ? 'the holy symbol' : 'the weapon';
}

const article = (w: string): string => ('aeiou'.includes(w[0]) ? 'an' : 'a');
const cap = (s: string): string => s[0].toUpperCase() + s.slice(1);

export function runExpedition(rng: Rng, content: Content, site: SiteDef, heroes: Agent[]): ExpeditionResult {
  const run = new Run(rng, content, heroes);
  const names = heroes.map((h) => h.givenName);
  run.log(`${listNames(names)} set out for ${site.name}.`);

  // Draw this delve's encounters from the site's deck.
  const deck = [...site.encounters];
  const count = Math.min(deck.length, 2 + site.tier);
  const plan: EncounterDef[] = [];
  for (let i = 0; i < count; i++) plan.push(deck.splice(rng.range(0, deck.length), 1)[0]);

  let outcome: ExpeditionOutcome = 'retreated';
  let rested = false;
  for (let i = 0; i <= plan.length; i++) {
    const boss = i === plan.length;
    const e = boss ? site.boss : plan[i];
    if (boss) run.log('At last, the heart of the place.');
    if (e.kind === 'combat') {
      const won = run.combat(e, false);
      if (run.standing.length === 0) {
        outcome = 'wiped';
        break;
      }
      if (boss && won) {
        outcome = 'cleared';
        break;
      }
      if (!won) break;
    } else {
      run.encounter(e);
      if (run.standing.length === 0) {
        outcome = 'wiped';
        break;
      }
    }

    // A short rest halfway in.
    if (!rested && i >= Math.floor(plan.length / 2)) {
      rested = true;
      for (const c of run.standing) c.sheet.hp = Math.min(c.sheet.maxHp, c.sheet.hp + Math.ceil(c.sheet.maxHp / 4));
    }
    // Know when to quit.
    if (!boss && (run.hpFraction() < 0.3 || run.party.filter((c) => c.dead).length * 2 >= run.party.length)) {
      run.log('Too battered to go on, the party turns back.');
      break;
    }
  }

  const items: { heroId: number; item: string }[] = [];
  if (outcome === 'cleared') {
    const loot = rng.range(site.loot[0], site.loot[1] + 1);
    run.gold += loot;
    let line = `${site.name[0].toUpperCase() + site.name.slice(1)} is cleared. The hoard holds ${loot} gold`;
    if (rng.chance(0.15 * site.tier)) {
      const item = rng.pick(ITEMS);
      const survivors = run.party.filter((c) => !c.dead).sort((a, b) => a.sheet.items.length - b.sheet.items.length || a.agent.id - b.agent.id);
      const who = survivors[0];
      items.push({ heroId: who.agent.id, item: item.name });
      who.sheet.weaponBonus += item.weapon;
      who.sheet.armorBonus += item.armor;
      who.sheet.items.push(item.name);
      line += ` and ${item.name}, which ${who.agent.givenName} claims`;
    }
    run.log(`${line}.`);
  } else if (outcome === 'wiped') {
    run.log(`No one comes back from ${site.name}.`);
  }

  return {
    outcome,
    lines: run.lines,
    dead: run.party.filter((c) => c.dead).map((c) => c.agent.id),
    gold: run.gold,
    xp: run.xp,
    items,
    kills: run.kills,
  };
}

export function listNames(names: string[]): string {
  if (names.length <= 1) return names.join('');
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}
