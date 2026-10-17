import type { Agent } from '../agent.ts';
import { type BuildingDef, type Zone, isBusiness } from '../content.ts';
import { Importance } from '../ledger.ts';
import { type Building, type Lot, Market, Town } from '../town.ts';
import { CONSTRUCTION_WAGE } from './brain.ts';
import { moveIn, releaseWorkers, salePrice } from './economy.ts';

/**
 * Settling, buying and building: agents with ambitions and savings buy lots from the town,
 * break ground, and staff what they build. The mayor only decides which lots are zoned.
 */

const TAVERN_ADJECTIVES = ['Prancing', 'Drunken', 'Gilded', 'Rusty', 'Sleeping', 'Laughing', 'Crooked', 'Wandering', 'Golden', 'Howling'];
const TAVERN_NOUNS = ['Griffon', 'Mimic', 'Owlbear', 'Flagon', 'Dragon', 'Lantern', 'Kobold', 'Boar', 'Wyvern', 'Compass'];

export function daily(town: Town): void {
  for (const a of town.workers) {
    switch (a.ambition) {
      case 'OpenBusiness':
        tryOpenBusiness(town, a);
        break;
      case 'OwnHome':
        tryBuildHome(town, a);
        break;
      case 'Landlord':
        tryBuildRental(town, a);
        break;
      case 'None':
        if (a.has('Ambitious') && a.gold > 180) a.ambition = 'OpenBusiness';
        else if (a.has('Greedy') && a.gold > 120) a.ambition = 'Landlord';
        break;
    }
  }
}

/** How much the town wants another building of this kind (1.0 = balanced). */
export function demand(town: Town, buildingId: string): number {
  const m = town.market;
  // Sites already rising will add supply soon; each one dampens the signal.
  const pending = 1 - 0.15 * town.buildings.filter((b) => b.isSite && b.def.id === buildingId).length;
  // Idle hands are cheap labor: a pool of unemployed makes any workshop look attractive.
  const idle = town.workers.filter((a) => a.jobId === null).length;
  const labor = Math.min(0.4, idle * 0.025);
  const signal = (() => {
    switch (buildingId) {
      case 'farm':
        return m.price('Food') / Market.basePrice.Food + labor;
      case 'lumber_camp':
        return m.price('Timber') / Market.basePrice.Timber + labor;
      case 'forge':
        return m.price('Tools') / Market.basePrice.Tools + labor;
      case 'brewery':
        return m.price('Ale') / Market.basePrice.Ale + labor;
      case 'tavern':
        return tavernDemand(town);
      default:
        return 0;
    }
  })();
  return pending * signal;
}

function tavernDemand(town: Town): number {
  const taverns = town.buildings.filter((b) => b.def.venue && !b.forSale).length;
  if (taverns === 0) return town.population >= 12 ? 3.0 : 0.5;
  return town.population / (taverns * 40.0);
}

function tryOpenBusiness(town: Town, a: Agent): void {
  // A bargain first: a failed business going cheap.
  const forSale = town.buildings
    .filter((b) => b.forSale && b.complete && demand(town, b.def.id) > 0.8)
    .sort((x, y) => salePrice(town, x) - salePrice(town, y))[0];
  if (forSale && a.gold >= salePrice(town, forSale) + 20) {
    const price = salePrice(town, forSale);
    a.gold -= price;
    town.book('Land sales', price);
    const old = forSale.name;
    forSale.ownerId = a.id;
    forSale.forSale = false;
    forSale.closed = false;
    forSale.cash = 20;
    a.gold -= 20;
    town.lot(forSale.lotId).ownerId = a.id;
    forSale.name = nameFor(town, forSale.def, a);
    town.log('Property', Importance.Notable, `${a.name} buys the shuttered ${old} for ${price.toFixed(0)} gold and reopens it as ${forSale.name}.`, [a.id]);
    afterOpening(a);
    return;
  }

  const candidates = [...town.content.buildings.values()].filter((d) => d.buildable && isBusiness(d));
  let pick: BuildingDef | null = null;
  if (a.dreamBusiness !== null && demand(town, a.dreamBusiness) >= 0.85) {
    pick = town.content.building(a.dreamBusiness);
  } else {
    const scored = candidates.map((d) => ({ d, s: demand(town, d.id) }));
    scored.sort((x, y) => y.s - x.s || (x.d.id < y.d.id ? -1 : x.d.id > y.d.id ? 1 : 0));
    if (scored[0].s >= 1.05) pick = scored[0].d;
  }
  if (pick === null) return;

  if (tryBuild(town, a, pick, false)) afterOpening(a);
}

function afterOpening(a: Agent): void {
  a.ambition = a.has('Greedy') ? 'Landlord' : 'OwnHome';
  a.dreamBusiness = null;
}

function tryBuildHome(town: Town, a: Agent): void {
  if (a.homeId !== null) {
    const home = town.building(a.homeId);
    if (home.ownerId === a.id || (a.spouseId !== null && home.ownerId === a.spouseId)) {
      a.ambition = 'None';
      return;
    }
  }
  if (tryBuild(town, a, town.content.building('house'), false)) a.ambition = 'None';
}

function tryBuildRental(town: Town, a: Agent): void {
  const homeless = town.workers.filter((x) => x.homeId === null).length;
  let openBeds = 0;
  for (const b of town.buildings) openBeds += b.isSite ? b.def.housing : b.openBeds;
  if (homeless <= openBeds) return;
  const house = town.content.building('house');
  if (a.gold < totalCost(town, house, town.lots[0]) * 1.5) return;
  if (tryBuild(town, a, house, true) && !a.has('Greedy')) a.ambition = 'None';
}

export function totalCost(town: Town, def: BuildingDef, lot: Lot): number {
  return town.lotPrice(lot) + def.cost + town.market.quote('Timber', def.timber);
}

function tryBuild(town: Town, a: Agent, def: BuildingDef, rental: boolean): boolean {
  const lot = findLot(town, def.zone);
  if (!lot) return false;
  const lotPrice = town.lotPrice(lot);
  if (a.gold < totalCost(town, def, lot) + 5) return false;

  a.gold -= lotPrice;
  town.book('Land sales', lotPrice);
  a.gold -= town.market.buy('Timber', def.timber);
  a.gold -= def.cost;

  const site = town.addBuilding(def, lot, a.id, nameFor(town, def, a), false);
  site.laborBudget = def.buildHours * CONSTRUCTION_WAGE;

  const what = def.id === 'house' ? (rental ? 'a house to let' : 'a home for the family') : site.name;
  town.log(
    'Property',
    isBusiness(def) ? Importance.Notable : Importance.Routine,
    `${a.name} buys a ${def.zone.toLowerCase()} lot for ${lotPrice.toFixed(0)} gold and breaks ground on ${what}.`,
    [a.id],
  );
  return true;
}

export function findLot(town: Town, zone: Zone): Lot | undefined {
  return town.lots
    .filter((l) => l.zone === zone && l.isVacant && l.ownerId === null)
    .sort((x, y) => Town.distanceFromCenter(x) - Town.distanceFromCenter(y) || x.id - y.id)[0];
}

export function nameFor(town: Town, def: BuildingDef, owner: Agent | null): string {
  const family = owner?.familyName ?? 'Charter';
  let name: string;
  switch (def.id) {
    case 'house':
      name = owner === null ? `Charter House ${town.buildings.filter((b) => b.def.id === 'house').length + 1}` : `the ${family} house`;
      break;
    case 'farm':
      name = `${family} Farm`;
      break;
    case 'lumber_camp':
      name = `${family} Lumber Camp`;
      break;
    case 'forge':
      name = `${family}'s Forge`;
      break;
    case 'brewery':
      name = `${owner?.givenName ?? 'Charter'}'s Brewery`;
      break;
    case 'tavern':
      return uniqueTavernName(town);
    default:
      name = def.name;
  }
  // A tycoon's second forge is "Nailo's Forge No. 2", not a twin.
  const same = town.buildings.filter((b) => b.name === name || b.name.startsWith(`${name} No. `)).length;
  return same === 0 ? name : `${name} No. ${same + 1}`;
}

function uniqueTavernName(town: Town): string {
  for (let i = 0; i < 20; i++) {
    const name = `The ${town.rng.pick(TAVERN_ADJECTIVES)} ${town.rng.pick(TAVERN_NOUNS)}`;
    if (town.buildings.every((b) => b.name !== name)) return name;
  }
  return `Tavern No. ${town.buildings.length}`;
}

/** Hourly: finish construction sites whose labor is done. */
export function hourly(town: Town): void {
  for (const b of town.buildings) {
    if (b.opened || !b.complete) continue;
    b.opened = true;
    complete(town, b);
  }
}

function complete(town: Town, b: Building): void {
  releaseWorkers(town, b);
  if (b.ownerId !== null) {
    const owner = town.agent(b.ownerId);
    owner.gold += b.laborBudget;
    if (b.def.housing > 0 && (owner.homeId === null || town.building(owner.homeId).ownerId !== owner.id)) {
      moveIn(town, owner, b);
      if (owner.spouseId !== null) moveIn(town, town.agent(owner.spouseId), b);
    }
  }
  b.laborBudget = 0;
  town.log(
    'Construction',
    isBusiness(b.def) ? Importance.Notable : Importance.Routine,
    `${capitalize(b.name)} is finished${b.ownerId !== null ? ` for ${town.ownerName(b.ownerId)}` : ''}.`,
    b.ownerId !== null ? [b.ownerId] : [],
  );
}

const capitalize = (s: string): string => (s.length === 0 ? s : s[0].toUpperCase() + s.slice(1));
