import type { ClassDef } from './content.ts';
import { Rng } from './rng.ts';

export const TRAITS = ['Ambitious', 'Lazy', 'Gregarious', 'Shy', 'HotTempered', 'Greedy', 'Generous', 'Romantic'] as const;
export type Trait = (typeof TRAITS)[number];

export type Ambition = 'None' | 'OwnHome' | 'OpenBusiness' | 'Landlord';

export type Activity = 'Idle' | 'Sleeping' | 'Eating' | 'Working' | 'Building' | 'Drinking' | 'Relaxing' | 'Wandering';

export interface Abilities {
  str: number;
  dex: number;
  con: number;
  int: number;
  wis: number;
  cha: number;
}

export function rollAbilities(rng: Rng): Abilities {
  return {
    str: rng.abilityScore(),
    dex: rng.abilityScore(),
    con: rng.abilityScore(),
    int: rng.abilityScore(),
    wis: rng.abilityScore(),
    cha: rng.abilityScore(),
  };
}

export function formatAbilities(a: Abilities): string {
  return `STR ${a.str} DEX ${a.dex} CON ${a.con} INT ${a.int} WIS ${a.wis} CHA ${a.cha}`;
}

/** The adventuring half of a hero: class, level, hit points and gear. */
export interface HeroSheet {
  cls: ClassDef;
  level: number;
  xp: number;
  maxHp: number;
  hp: number;
  /** Magic weapon bonus (attack and damage). */
  weaponBonus: number;
  /** Magic armor or ward bonus (AC). */
  armorBonus: number;
  items: string[];
  renown: number;
  kills: number;
  expeditions: number;
  partyName: string | null;
  /** Tick the hero is due back from an expedition, or null when in town. */
  awayUntil: number | null;
  lodging: boolean;
}

export const article = (word: string): string => ('aeiouAEIOU'.includes(word[0]) ? 'an' : 'a');

/**
 * One autonomous townsperson. The mayor never controls agents directly; they act on needs,
 * traits, money and relationships.
 */
export class Agent {
  id = 0;
  givenName = '';
  familyName = '';
  race = '';
  age = 0;
  abilities: Abilities = { str: 10, dex: 10, con: 10, int: 10, wis: 10, cha: 10 };
  traits: ReadonlySet<Trait> = new Set();
  profession = '';
  ambition: Ambition = 'None';
  dreamBusiness: string | null = null;
  hero: HeroSheet | null = null;
  dead = false;

  gold = 0;

  // Needs, 0 = satisfied, 100 = desperate.
  hunger = 0;
  fatigue = 0;
  loneliness = 0;

  homeId: number | null = null;
  jobId: number | null = null;
  spouseId: number | null = null;
  courtingId: number | null = null;

  activity: Activity = 'Idle';
  locationId: number | null = null;
  drinks = 0;

  arrivedTick = 0;
  departed = false;

  moodToday = 0;
  moodSamples = 0;
  lastDayMood = 70;
  miserableDays = 0;
  daysUnemployed = 0;
  daysHomeless = 0;
  rentArrears = 0;

  /** Opinion of other agents, -100..100. */
  readonly opinions = new Map<number, number>();

  get name(): string {
    return `${this.givenName} ${this.familyName}`;
  }

  has(t: Trait): boolean {
    return this.traits.has(t);
  }

  opinionOf(other: number): number {
    return this.opinions.get(other) ?? 0;
  }

  changeOpinion(other: number, delta: number): number {
    const v = Math.max(-100, Math.min(100, this.opinionOf(other) + delta));
    this.opinions.set(other, v);
    return v;
  }

  get isHero(): boolean {
    return this.hero !== null;
  }

  get isAway(): boolean {
    return this.hero?.awayUntil != null;
  }

  describe(): string {
    if (this.hero) return `${this.name}, ${article(this.race)} ${this.race} ${this.hero.cls.name} (level ${this.hero.level})`;
    return `${this.name}, ${article(this.race)} ${this.race} ${this.profession}`;
  }

  traitList(): string {
    return this.traits.size === 0 ? 'unremarkable' : TRAITS.filter((t) => this.has(t)).map((t) => t.toLowerCase()).join(', ');
  }
}
