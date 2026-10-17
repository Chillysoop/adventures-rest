import type { Activity, Agent } from '../agent.ts';
import { modifier } from '../rng.ts';
import { isEvening, isNight, isWorkHours } from '../time.ts';
import type { Building, Town } from '../town.ts';
import { festivalMood } from './council.ts';

/**
 * Hourly utility AI: every agent scores what it could do this hour from its needs, traits,
 * money and the time of day, then does the best-scoring thing.
 */

export const CONSTRUCTION_WAGE = 0.6;
export const ALE_MARKUP = 2.0;

export function hourly(town: Town): void {
  for (const a of town.agents) {
    if (a.departed || a.isAway) continue;
    updateNeeds(a);
    execute(town, a, choose(town, a));
    sampleMood(a, festivalMood(town));
  }
}

function updateNeeds(a: Agent): void {
  const asleep = a.activity === 'Sleeping';
  a.hunger = Math.min(100, a.hunger + (asleep ? 1.0 : 3.0));
  if (!asleep) a.fatigue = Math.min(100, a.fatigue + Math.max(2.5, 4.0 - 0.3 * modifier(a.abilities.con)));
  const lonely = a.has('Gregarious') ? 2.5 : a.has('Shy') ? 0.8 : 1.5;
  a.loneliness = Math.min(100, a.loneliness + (asleep ? 0.3 : lonely));
}

function choose(town: Town, a: Agent): Activity {
  const hour = town.hour;
  const night = isNight(hour);
  const rng = town.rng;

  // Sleep is sticky: once down for the night, stay down until rested or morning.
  if (a.activity === 'Sleeping' && a.fatigue > 5 && (night || a.fatigue > 40)) return 'Sleeping';

  const sleep = (a.fatigue / 100) * (night ? 2.0 : hour >= 21 ? 1.0 : 0.3) + (a.fatigue > 90 ? 1 : 0);

  const canEat = a.gold >= town.market.quote('Food', 1);
  const eat = a.hunger > 35 ? (canEat ? (a.hunger / 100) * 1.4 + (hour === 7 || hour === 12 || hour === 18 ? 0.2 : 0) : 0.05) : 0;

  let work = 0;
  if (a.jobId !== null && isWorkHours(hour))
    work = (a.has('Lazy') ? 0.55 : 0.85) + (a.has('Greedy') ? 0.1 : 0) + (a.has('Ambitious') ? 0.05 : 0);

  let drink = 0;
  if (isEvening(hour) && a.gold >= 8 && town.taverns.length > 0) {
    drink =
      0.25 +
      (a.loneliness / 100) * 0.8 +
      (a.has('Gregarious') ? 0.25 : 0) -
      (a.has('Shy') ? 0.2 : 0) -
      (a.drinks >= 2 ? 0.4 : 0) -
      (a.has('Greedy') ? 0.1 : 0);
  }

  const relax = 0.3;
  const wander = night ? 0 : 0.15;

  const options: [Activity, number][] = [
    ['Sleeping', sleep],
    ['Eating', eat],
    ['Working', work],
    ['Drinking', drink],
    ['Relaxing', relax],
    ['Wandering', wander],
  ];
  let best: Activity = 'Relaxing';
  let bestScore = -Infinity;
  for (const [act, score] of options) {
    const s = score + rng.next() * 0.1;
    if (s > bestScore) {
      bestScore = s;
      best = act;
    }
  }
  return best;
}

function execute(town: Town, a: Agent, act: Activity): void {
  a.activity = act;
  switch (act) {
    case 'Sleeping':
      a.locationId = a.homeId;
      a.fatigue = Math.max(0, a.fatigue - (a.homeId !== null ? 14 : 8));
      a.drinks = 0;
      break;
    case 'Eating':
      a.gold -= town.market.buy('Food', 1);
      a.hunger = Math.max(0, a.hunger - 55);
      a.locationId = a.homeId;
      break;
    case 'Working':
      doWork(town, a);
      break;
    case 'Drinking':
      doDrink(town, a);
      break;
    case 'Relaxing':
      a.locationId = a.homeId;
      a.fatigue = Math.max(0, a.fatigue - 1.5);
      if (a.homeId !== null && town.building(a.homeId).residents.length > 1)
        a.loneliness = Math.max(0, a.loneliness - (a.spouseId !== null ? 4 : 2));
      break;
    case 'Wandering':
      a.locationId = null;
      a.loneliness = Math.max(0, a.loneliness - 1);
      break;
  }
}

function doWork(town: Town, a: Agent): void {
  const b = town.building(a.jobId!);
  a.locationId = b.id;
  if (b.isSite) {
    a.activity = 'Building';
    b.buildProgress += Math.max(0.5, 1 + 0.1 * modifier(a.abilities.str));
    if (b.ownerId !== a.id) {
      const pay = Math.min(CONSTRUCTION_WAGE, b.laborBudget);
      b.laborBudget -= pay;
      a.gold += pay;
    }
  } else if (b.ownerId !== a.id) {
    // Wages are paid by the hour out of the business account, which may go into debt.
    b.cash -= b.def.wage;
    a.gold += b.def.wage;
  }
}

function doDrink(town: Town, a: Agent): void {
  const tavern = town.taverns
    .filter((t) => patronsAt(town, t) < t.def.capacity)
    .sort((x, y) => opinionOfOwner(a, y) - opinionOfOwner(a, x) || x.id - y.id)[0];
  if (!tavern) {
    a.activity = 'Relaxing';
    a.locationId = a.homeId;
    return;
  }

  a.locationId = tavern.id;
  if (tavern.stockOf('Ale') < 1) {
    const restock = Math.min(10, Math.max(0, tavern.cash) / Math.max(0.1, town.market.price('Ale')));
    if (restock >= 1) {
      tavern.cash -= town.market.buy('Ale', Math.floor(restock));
      tavern.addStock('Ale', Math.floor(restock));
    }
  }
  const price = Math.round(town.market.price('Ale') * ALE_MARKUP * 10) / 10;
  if (tavern.stockOf('Ale') >= 1 && a.gold >= price) {
    tavern.addStock('Ale', -1);
    a.gold -= price;
    const tax = price * town.taxRate;
    town.book('Ale tax', tax);
    tavern.cash += price - tax;
    tavern.revenueToday += price;
    a.drinks++;
  }
  a.loneliness = Math.max(0, a.loneliness - 8);
}

function opinionOfOwner(a: Agent, b: Building): number {
  return b.ownerId !== null ? a.opinionOf(b.ownerId) : 0;
}

export function patronsAt(town: Town, b: Building): number {
  let n = 0;
  for (const x of town.agents) if (!x.departed && x.activity === 'Drinking' && x.locationId === b.id) n++;
  return n;
}

function sampleMood(a: Agent, bonus: number): void {
  const mood =
    100 +
    bonus -
    0.45 * a.hunger -
    0.25 * a.fatigue -
    0.25 * a.loneliness -
    (a.homeId === null && !a.hero?.lodging ? 20 : 0) -
    (a.jobId === null && a.ambition !== 'OpenBusiness' && !a.hero ? 10 : 0);
  a.moodToday += Math.max(0, Math.min(100, mood));
  a.moodSamples++;
}

export function daily(town: Town): void {
  for (const a of town.residents) {
    if (a.isAway) continue;
    a.lastDayMood = a.moodSamples > 0 ? a.moodToday / a.moodSamples : a.lastDayMood;
    a.moodToday = 0;
    a.moodSamples = 0;
    a.miserableDays = a.lastDayMood < 35 ? a.miserableDays + 1 : 0;
    a.daysHomeless = a.homeId === null ? a.daysHomeless + 1 : 0;
    a.daysUnemployed = a.jobId === null ? a.daysUnemployed + 1 : 0;
  }
}
