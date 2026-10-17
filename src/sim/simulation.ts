import { Content } from './content.ts';
import { Importance } from './ledger.ts';
import { HOURS_PER_DAY } from './time.ts';
import { type Lot, Town } from './town.ts';
import type { Zone } from './content.ts';
import * as brain from './systems/brain.ts';
import * as economy from './systems/economy.ts';
import * as mayor from './systems/mayor.ts';
import * as population from './systems/population.ts';
import * as property from './systems/property.ts';
import * as quests from './systems/quests.ts';
import * as social from './systems/social.ts';

export interface DayStats {
  day: number;
  population: number;
  homeless: number;
  unemployed: number;
  buildings: number;
  sites: number;
  treasury: number;
  foodPrice: number;
  averageMood: number;
  averageGold: number;
}

export interface SimulationOptions {
  seed?: number;
  settlers?: number;
  startingTreasury?: number;
  autoMayor?: boolean;
  /** Let the stand-in mayor post bounties; off when a player runs the bounty board. */
  autoBounties?: boolean;
}

/**
 * Entry point for clients. The game client calls step() once per in-game hour at the speed the
 * player picks (real time with pause); tests and the CLI call runDays() as fast as possible.
 */
export class Simulation {
  readonly town: Town;
  readonly options: Required<SimulationOptions>;
  readonly history: DayStats[] = [];

  constructor(options: SimulationOptions = {}, content: Content = Content.default()) {
    this.options = { seed: 1, settlers: 14, startingTreasury: 250, autoMayor: true, autoBounties: true, ...options };
    this.town = new Town(content, this.options.seed);
    this.charter();
  }

  step(): void {
    const t = this.town;
    brain.hourly(t);
    economy.hourly(t);
    property.hourly(t);
    social.hourly(t);
    t.tick++;
    if (t.hour === 0) this.dailyTick();
  }

  runDays(days: number): void {
    for (let i = 0; i < days * HOURS_PER_DAY; i++) this.step();
  }

  /** Strategic layer, once per in-game day. */
  private dailyTick(): void {
    const t = this.town;
    brain.daily(t);
    economy.daily(t);
    property.daily(t);
    population.dailyDepartures(t);
    population.dailyArrivals(t);
    if (this.options.autoMayor) mayor.daily(t);
    social.daily(t);
    quests.daily(t, this.options.autoMayor && this.options.autoBounties);
    economy.daily(t, true);
    this.history.push(this.snapshot());
  }

  /** The player's lever: post a bounty on a site, paid from the treasury into escrow. */
  postBounty(siteId: string, reward: number): quests.Bounty | null {
    return quests.postBounty(this.town, siteId, reward);
  }

  snapshot(): DayStats {
    const t = this.town;
    const res = t.residents;
    const workers = t.workers;
    const avg = (f: (a: (typeof res)[number]) => number): number => (res.length ? res.reduce((s, a) => s + f(a), 0) / res.length : 0);
    return {
      day: t.day - 1,
      population: res.length,
      homeless: workers.filter((a) => a.homeId === null).length,
      unemployed: workers.filter((a) => a.jobId === null).length,
      buildings: t.buildings.filter((b) => b.complete).length,
      sites: t.buildings.filter((b) => b.isSite).length,
      treasury: t.treasury,
      foodPrice: t.market.price('Food'),
      averageMood: avg((a) => a.lastDayMood),
      averageGold: avg((a) => a.gold),
    };
  }

  /** Day one: the charter, a wagon of settlers, two charter houses, a farm and a lumber camp. */
  private charter(): void {
    const t = this.town;
    const c = t.content;
    t.treasury = this.options.startingTreasury;
    const center = t.lotAt(Town.GRID / 2 - 1, Town.GRID / 2 - 1);
    center.zone = 'Civic';
    t.addBuilding(c.building('town_hall'), center, null, 'the Town Hall', true);
    quests.initSites(t);

    if (this.options.autoMayor) mayor.daily(t);
    else {
      const zones: Zone[] = ['Residential', 'Residential', 'Residential', 'Commercial', 'Farmland', 'Farmland'];
      for (const z of zones) {
        const lot = t.lots
          .filter((l) => l.zone === 'None')
          .sort((x, y) => Town.distanceFromCenter(x) - Town.distanceFromCenter(y) || x.id - y.id)[0];
        lot.zone = z;
      }
    }

    const house = c.building('house');
    for (let i = 0; i < 2; i++) t.addBuilding(house, this.freeLot('Residential'), null, property.nameFor(t, house, null), true);

    const farmer = population.createAgent(t, { profession: 'farmer', gold: t.rng.range(10, 30) });
    const woodcutter = population.createAgent(t, { profession: 'woodcutter', gold: t.rng.range(10, 30) });
    for (const owner of [farmer, woodcutter]) {
      owner.ambition = 'OwnHome';
      owner.dreamBusiness = null;
    }

    const farmDef = c.building('farm');
    const campDef = c.building('lumber_camp');
    const farm = t.addBuilding(farmDef, this.freeLot('Farmland'), farmer.id, property.nameFor(t, farmDef, farmer), true);
    const camp = t.addBuilding(campDef, this.freeLot('Farmland'), woodcutter.id, property.nameFor(t, campDef, woodcutter), true);
    farm.cash = 40;
    camp.cash = 40;

    for (let i = 2; i < this.options.settlers; i++) population.createAgent(t);

    t.market.seed('Food', 150);
    t.market.seed('Timber', 60);
    t.market.seed('Tools', 8);
    t.market.seed('Ale', 15);

    economy.daily(t, true);
    if (this.options.autoMayor) mayor.daily(t);

    t.log(
      'Town',
      Importance.Major,
      `The charter is signed. ${this.options.settlers} settlers and a wagon arrive at the river ford that will become ${t.name}.`,
      t.residents.map((a) => a.id),
    );
  }

  private freeLot(zone: Zone): Lot {
    return property.findLot(this.town, zone)!;
  }
}
