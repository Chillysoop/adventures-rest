import type { Agent } from '../agent.ts';
import { isBusiness } from '../content.ts';
import { Importance } from '../ledger.ts';
import type { Building, Town } from '../town.ts';
import { CONSTRUCTION_WAGE } from './brain.ts';
import { remove } from './population.ts';

/** Production, wages, rent, the housing and job markets, and bankruptcy. */

const TOOL_USE_PER_HOUR = 0.02;
const TOOL_BONUS = 0.3;
const CASH_RESERVE = 40;

export function hourly(town: Town): void {
  for (const b of town.buildings) {
    if (!b.complete || b.closed || b.forSale || Object.keys(b.def.produces).length === 0) continue;
    for (const wid of b.workers) {
      const w = town.agent(wid);
      if (w.activity === 'Working' && w.locationId === b.id) produce(town, b);
    }
  }
}

function produce(town: Town, b: Building): void {
  let rate = 1.0;
  if (b.stockOf('Tools') >= TOOL_USE_PER_HOUR) {
    b.addStock('Tools', -TOOL_USE_PER_HOUR);
    rate += TOOL_BONUS;
  }

  for (const [good, qty] of entries(b.def.consumes)) {
    const need = qty * rate;
    if (b.stockOf(good) < need) {
      const buy = Math.ceil(need * 10);
      b.cash -= town.market.buy(good, buy);
      b.addStock(good, buy);
    }
    b.addStock(good, -need);
  }

  for (const [good, qty] of entries(b.def.produces)) {
    const revenue = town.market.sell(good, qty * rate);
    const tax = revenue * town.taxRate;
    town.treasury += tax;
    b.cash += revenue - tax;
    b.revenueToday += revenue;
  }
}

function entries<K extends string>(r: Partial<Record<K, number>>): [K, number][] {
  return Object.entries(r) as [K, number][];
}

/** Daily accounts; with jobsAndHousingOnly, just re-runs matching (e.g. after arrivals). */
export function daily(town: Town, jobsAndHousingOnly = false): void {
  if (!jobsAndHousingOnly) {
    updateMarket(town);
    accounts(town);
    rent(town);
  }
  housingMarket(town);
  jobMarket(town);
}

function updateMarket(town: Town): void {
  const pop = town.population;
  const m = town.market;
  m.setTarget('Food', pop * 3 + 40);
  m.setTarget('Timber', 40 + town.buildings.filter((b) => b.isSite).length * 10);
  m.setTarget('Tools', 10 + town.businessCount());
  m.setTarget('Ale', pop * 0.6 + 10);
  m.dailyCaravan();
}

function ownerIds(b: Building): number[] {
  return b.ownerId !== null ? [b.ownerId] : [];
}

function accounts(town: Town): void {
  for (const b of town.buildings) {
    if (!b.complete || !isBusiness(b.def) || b.forSale) {
      b.revenueToday = 0;
      continue;
    }

    // Keep a working stock of tools.
    if (Object.keys(b.def.produces).length > 0 && b.stockOf('Tools') < 0.5 && b.cash > 30) {
      b.cash -= town.market.buy('Tools', 1);
      b.addStock('Tools', 1);
    }

    if (b.cash < 0 && b.ownerId !== null) {
      const owner = town.agent(b.ownerId);
      const cover = Math.min(owner.gold, -b.cash);
      owner.gold -= cover;
      b.cash += cover;
    } else if (b.cash < 0 && b.ownerId === null) {
      const cover = Math.min(town.treasury, -b.cash);
      town.treasury -= cover;
      b.cash += cover;
    }

    if (b.cash < -0.01) {
      b.daysInDebt++;
      if (b.daysInDebt === 2)
        town.log(
          'Business',
          Importance.Notable,
          `${b.name} cannot meet its wages; ${town.ownerName(b.ownerId)} is borrowing against next week.`,
          ownerIds(b),
        );
      if (b.daysInDebt >= 4) bankrupt(town, b);
    } else {
      b.daysInDebt = 0;
      if (b.cash > CASH_RESERVE) {
        const profit = b.cash - CASH_RESERVE;
        b.cash = CASH_RESERVE;
        if (b.ownerId !== null) town.agent(b.ownerId).gold += profit;
        else town.treasury += profit;
      }
    }
    b.revenueToday = 0;
  }
}

function bankrupt(town: Town, b: Building): void {
  const owner = town.ownerName(b.ownerId);
  const ids = ownerIds(b);
  if (b.ownerId !== null) {
    b.ownerId = null;
    town.lot(b.lotId).ownerId = null;
  }
  b.cash = 0;
  b.daysInDebt = 0;
  putUpForSale(town, b);
  town.log('Business', Importance.Major, `${b.name} goes bankrupt. ${owner} loses it to creditors and the town puts it up for sale.`, ids);
}

export function putUpForSale(town: Town, b: Building): void {
  if (!b.complete) return;
  b.forSale = true;
  releaseWorkers(town, b);
}

export function salePrice(town: Town, b: Building): number {
  return Math.round(town.lotPrice(town.lot(b.lotId)) + b.def.cost * 0.6);
}

function rent(town: Town): void {
  for (const house of town.buildings) {
    if (!house.complete || house.def.housing === 0) continue;
    for (const rid of [...house.residents]) {
      const r = town.agent(rid);
      if (house.ownerId === r.id || (house.ownerId !== null && r.spouseId === house.ownerId)) continue;
      const landlord = house.ownerId !== null ? town.agent(house.ownerId) : null;
      if (r.gold >= house.def.rent) {
        r.rentArrears = 0;
        r.gold -= house.def.rent;
        if (landlord) landlord.gold += house.def.rent;
        else town.treasury += house.def.rent;
      } else if (landlord?.has('Generous')) {
        r.changeOpinion(landlord.id, 3);
      } else if (++r.rentArrears < 3) {
        // A few days' grace before the bailiff comes.
      } else {
        r.rentArrears = 0;
        remove(house.residents, r.id);
        r.homeId = null;
        if (landlord) r.changeOpinion(landlord.id, -25);
        town.log(
          'Hardship',
          Importance.Notable,
          `${r.name} cannot pay rent and is turned out of ${house.name} by ${town.ownerName(house.ownerId)}.`,
          landlord ? [r.id, landlord.id] : [r.id],
        );
      }
    }
  }
}

export function moveIn(town: Town, a: Agent, home: Building): void {
  if (a.homeId !== null) remove(town.building(a.homeId).residents, a.id);
  a.homeId = home.id;
  home.residents.push(a.id);
}

function housingMarket(town: Town): void {
  for (const a of town.residents) {
    if (a.homeId !== null) continue;
    const spouse = a.spouseId !== null ? town.agent(a.spouseId) : null;
    const beds = spouse && spouse.homeId === null ? 2 : 1;
    if (spouse?.homeId != null && town.building(spouse.homeId).openBeds > 0) {
      moveIn(town, a, town.building(spouse.homeId));
      continue;
    }

    const purse = a.gold + (spouse?.gold ?? 0);
    const score = (b: Building): number => (b.ownerId === a.id ? 1000 : b.ownerId !== null ? a.opinionOf(b.ownerId) : 0);
    const home = town.buildings
      .filter((b) => b.complete && b.openBeds >= beds && (b.ownerId === a.id || purse >= b.def.rent * beds * 3))
      .sort((x, y) => score(y) - score(x) || x.id - y.id)[0];
    if (!home) continue;
    moveIn(town, a, home);
    if (beds === 2) moveIn(town, spouse!, home);
  }
}

function jobMarket(town: Town): void {
  // Laborers walk off sites that can no longer pay them.
  for (const site of town.buildings) {
    if (!site.isSite || site.laborBudget >= CONSTRUCTION_WAGE) continue;
    for (const wid of site.workers.filter((w) => w !== site.ownerId)) {
      town.agent(wid).jobId = null;
      remove(site.workers, wid);
    }
  }

  for (const a of town.residents) {
    if (a.jobId !== null) continue;
    // Owners staff their own businesses first.
    let job = town.buildings.find((b) => b.ownerId === a.id && b.complete && isBusiness(b.def) && b.openJobs > 0);
    job ??= town.buildings
      .filter((b) => b.openJobs > 0)
      .sort(
        (x, y) =>
          (y.def.profession === a.profession ? 1 : 0) - (x.def.profession === a.profession ? 1 : 0) ||
          y.def.wage - x.def.wage ||
          x.id - y.id,
      )[0];
    job ??= town.buildings.find(
      (b) => b.isSite && b.workers.length < 3 && (b.laborBudget >= CONSTRUCTION_WAGE || b.ownerId === a.id),
    );
    if (!job) continue;

    a.jobId = job.id;
    job.workers.push(a.id);
    if (a.daysUnemployed >= 3)
      town.log('Business', Importance.Routine, `After ${a.daysUnemployed} days idle, ${a.name} finds work at ${job.name}.`, [a.id]);
  }
}

export function releaseWorkers(town: Town, b: Building): void {
  for (const wid of b.workers) town.agent(wid).jobId = null;
  b.workers.length = 0;
}
