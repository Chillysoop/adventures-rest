import buildingsJson from '../../content/game/buildings.json' with { type: 'json' };
import namesJson from '../../content/game/names.json' with { type: 'json' };
import classesJson from '../../content/game/classes.json' with { type: 'json' };
import monstersJson from '../../content/game/monsters.json' with { type: 'json' };
import sitesJson from '../../content/game/sites.json' with { type: 'json' };
import townJson from '../../content/game/town.json' with { type: 'json' };
import councilJson from '../../content/game/council.json' with { type: 'json' };
import enemyJson from '../../content/game/enemy.json' with { type: 'json' };

export const GOODS = ['Food', 'Timber', 'Tools', 'Ale'] as const;
export type Good = (typeof GOODS)[number];

export type Zone = 'None' | 'Civic' | 'Residential' | 'Farmland' | 'Commercial';

export interface BuildingDef {
  id: string;
  name: string;
  zone: Zone;
  buildable: boolean;
  cost: number;
  timber: number;
  buildHours: number;
  jobs: number;
  wage: number;
  housing: number;
  rent: number;
  venue: boolean;
  capacity: number;
  profession?: string;
  produces: Partial<Record<Good, number>>;
  consumes: Partial<Record<Good, number>>;
}

export interface RaceDef {
  id: string;
  name: string;
  weight: number;
  given: string[];
  family: string[];
}

export type Ability = 'str' | 'dex' | 'con' | 'int' | 'wis' | 'cha';
export type SkillKind = 'trap' | 'hazard' | 'puzzle' | 'parley' | 'perception';

export interface ClassDef {
  id: string;
  name: string;
  primary: Ability;
  hitDie: number;
  armor: number;
  damageDie: number;
  skills: Record<SkillKind, number>;
  weight: number;
  sneak?: boolean;
  area?: boolean;
  healer?: boolean;
}

export interface MonsterDef {
  id: string;
  name: string;
  plural: string;
  hp: number;
  ac: number;
  attack: number;
  damageDie: number;
  damageBonus: number;
  xp: number;
  boss?: boolean;
  undead?: boolean;
  /** A named individual: no article before the name. */
  named?: boolean;
}

export type EncounterDef =
  | { kind: 'combat'; monster: string; count: [number, number]; escort?: string; escortCount?: [number, number]; text: string }
  | { kind: 'trap' | 'hazard'; dc: number; damageDie: number; text: string }
  | { kind: 'puzzle' | 'parley'; dc: number; gold: [number, number]; text: string }
  | { kind: 'treasure'; gold: [number, number]; mimicChance: number; text: string };

export interface SiteDef {
  id: string;
  name: string;
  tier: number;
  distance: number;
  encounters: EncounterDef[];
  boss: EncounterDef;
  loot: [number, number];
}

/** Town-wide economic rules. */
export interface TownRules {
  /** Share of every sale and every drink that goes to the treasury. */
  taxRate: number;
  /** Daily cost per resident of the watch, roads and wells. */
  upkeepPerResident: number;
  /** Per-resident upkeep doubles at this many residents: bigger towns cost more per head. */
  upkeepGrowth: number;
  /** Daily cost of keeping the town hall and council running. */
  civicUpkeep: number;
}

export type SeatId = 'steward' | 'speaker' | 'captain';
export type MotionKind = 'raise_tax' | 'lower_tax' | 'hire_watch' | 'dismiss_watch' | 'festival' | 'build_homes';

export interface SeatDef {
  id: SeatId;
  title: string;
  ability: Ability;
  unlockPopulation: number;
  portfolio: string;
  agenda: string;
}

/** The council: its seats, how each seat feels about each kind of motion, and the motions' numbers. */
export interface CouncilRules {
  seats: SeatDef[];
  /** -1 (against) to 1 (for), by motion kind and seat. */
  alignment: Record<MotionKind, Record<SeatId, number>>;
  taxStep: number;
  minTax: number;
  maxTax: number;
  watchPerMotion: number;
  watchWage: number;
  festivalBase: number;
  festivalPerResident: number;
  festivalDays: number;
  festivalMood: number;
  motionDays: number;
  motionGapDays: number;
  decreeMandate: number;
  startingMandate: number;
}

/** The slice's Tier 2 enemy: how it grows, escalates, raids and besieges the town. */
export interface EnemyDef {
  name: string;
  leader: string;
  leaderMonster: string;
  startStrength: number;
  baseGrowth: number;
  growthPerDay: number;
  growthPerSite: number;
  growthPerTier3Site: number;
  /** A richer town draws a bigger warband. */
  growthPerResident: number;
  /** The warlord marches only once the warband is at least this share of the town's defence. */
  marchConfidence: number;
  /** Warband strength at which each escalation stage begins. */
  stages: { omens: number; probing: number; raids: number; march: number };
  marchDays: number;
  raidEveryDays: [number, number];
  raidShare: number;
  outpostGrowth: number;
  outpostClearedLoss: number;
  siegeWaves: number;
  breachesToFall: number;
  defense: { watchman: number; heroLevel: number; militiaPerWorker: number; palisade: number; perTower: number };
  palisade: { cost: number; timber: number; days: number };
  tower: { cost: number; timber: number; days: number; max: number };
  outpost: SiteDef;
  warlordBattle: EncounterDef;
  omens: string[];
  probing: string[];
}

export const isBusiness = (d: BuildingDef): boolean => d.jobs > 0;

/**
 * Data-driven content (buildings, races, names). Bundled JSON by default so the simulation
 * runs anywhere; a mod can pass its own.
 */
export class Content {
  readonly buildings: ReadonlyMap<string, BuildingDef>;
  readonly races: readonly RaceDef[];
  readonly classes: readonly ClassDef[];
  readonly monsters: ReadonlyMap<string, MonsterDef>;
  readonly sites: readonly SiteDef[];
  readonly rules: TownRules;
  readonly council: CouncilRules;
  readonly enemy: EnemyDef;

  constructor(
    buildings: BuildingDef[],
    races: RaceDef[],
    classes: ClassDef[],
    monsters: MonsterDef[],
    sites: SiteDef[],
    rules: TownRules = townJson,
    council: CouncilRules = councilJson as CouncilRules,
    enemy: EnemyDef = enemyJson as unknown as EnemyDef,
  ) {
    this.buildings = new Map(buildings.map((b) => [b.id, b]));
    this.races = races;
    this.classes = classes;
    this.monsters = new Map(monsters.map((m) => [m.id, m]));
    this.sites = sites;
    this.rules = rules;
    this.council = council;
    this.enemy = enemy;
  }

  monster(id: string): MonsterDef {
    const m = this.monsters.get(id);
    if (!m) throw new Error(`Unknown monster ${id}`);
    return m;
  }

  building(id: string): BuildingDef {
    const b = this.buildings.get(id);
    if (!b) throw new Error(`Unknown building ${id}`);
    return b;
  }

  static default(): Content {
    const buildings = (buildingsJson as Partial<BuildingDef>[]).map(
      (b): BuildingDef => ({
        id: b.id!,
        name: b.name!,
        zone: b.zone ?? 'None',
        buildable: b.buildable ?? true,
        cost: b.cost ?? 0,
        timber: b.timber ?? 0,
        buildHours: b.buildHours ?? 0,
        jobs: b.jobs ?? 0,
        wage: b.wage ?? 0,
        housing: b.housing ?? 0,
        rent: b.rent ?? 0,
        venue: b.venue ?? false,
        capacity: b.capacity ?? 0,
        profession: b.profession,
        produces: b.produces ?? {},
        consumes: b.consumes ?? {},
      }),
    );
    return new Content(
      buildings,
      namesJson.races,
      classesJson as ClassDef[],
      monstersJson as MonsterDef[],
      sitesJson as unknown as SiteDef[],
    );
  }
}
