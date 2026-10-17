import buildingsJson from './content/buildings.json' with { type: 'json' };
import namesJson from './content/names.json' with { type: 'json' };
import classesJson from './content/classes.json' with { type: 'json' };
import monstersJson from './content/monsters.json' with { type: 'json' };
import sitesJson from './content/sites.json' with { type: 'json' };

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

  constructor(buildings: BuildingDef[], races: RaceDef[], classes: ClassDef[], monsters: MonsterDef[], sites: SiteDef[]) {
    this.buildings = new Map(buildings.map((b) => [b.id, b]));
    this.races = races;
    this.classes = classes;
    this.monsters = new Map(monsters.map((m) => [m.id, m]));
    this.sites = sites;
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
