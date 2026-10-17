import { Agent, TRAITS, type Trait, rollAbilities } from '../agent.ts';
import type { RaceDef } from '../content.ts';
import { Importance } from '../ledger.ts';
import type { Rng } from '../rng.ts';
import type { Town } from '../town.ts';
import { putUpForSale } from './economy.ts';

/** Who comes to town, who settles, and who gives up and leaves. */

const PROFESSIONS = ['farmer', 'farmer', 'woodcutter', 'laborer', 'laborer', 'smith', 'brewer', 'barkeep', 'merchant'];

const DREAM_FOR: Record<string, string> = {
  farmer: 'farm',
  woodcutter: 'lumber_camp',
  smith: 'forge',
  brewer: 'brewery',
  barkeep: 'tavern',
};

const OPPOSITES: [Trait, Trait][] = [
  ['Gregarious', 'Shy'],
  ['Greedy', 'Generous'],
  ['Ambitious', 'Lazy'],
];

export interface NewAgentOptions {
  profession?: string;
  race?: string;
  familyName?: string;
  gold?: number;
}

export function createAgent(town: Town, opts: NewAgentOptions = {}): Agent {
  const rng = town.rng;
  const raceDef = opts.race ? town.content.races.find((r) => r.id === opts.race)! : pickRace(town);
  const profession = opts.profession ?? rng.pick(PROFESSIONS);
  const traits = rollTraits(rng);
  const entrepreneur = profession === 'merchant' || traits.has('Ambitious');

  const a = new Agent();
  a.id = town.newAgentId();
  a.givenName = rng.pick(raceDef.given);
  a.familyName = opts.familyName ?? rng.pick(raceDef.family);
  a.race = raceDef.name;
  a.age = rng.range(18, 56);
  a.abilities = rollAbilities(rng);
  a.traits = traits;
  a.profession = profession;
  a.gold = opts.gold ?? (entrepreneur ? rng.range(60, 170) : rng.range(5, 35));
  a.hunger = rng.range(10, 40);
  a.fatigue = rng.range(10, 40);
  a.loneliness = rng.range(10, 50);
  a.arrivedTick = town.tick;
  a.ambition = entrepreneur ? 'OpenBusiness' : 'OwnHome';
  if (entrepreneur && DREAM_FOR[profession]) a.dreamBusiness = DREAM_FOR[profession];
  town.agents.push(a);
  return a;
}

function pickRace(town: Town): RaceDef {
  const races = town.content.races;
  const total = races.reduce((s, r) => s + r.weight, 0);
  let roll = town.rng.range(0, total);
  for (const r of races) {
    if (roll < r.weight) return r;
    roll -= r.weight;
  }
  return races[0];
}

function rollTraits(rng: Rng): Set<Trait> {
  const traits = new Set<Trait>();
  const wanted = rng.range(1, 4);
  for (let guard = 0; guard < 20 && traits.size < wanted; guard++) {
    const t = rng.pick(TRAITS);
    if (OPPOSITES.some(([a, b]) => (a === t && traits.has(b)) || (b === t && traits.has(a)))) continue;
    traits.add(t);
  }
  return traits;
}

/** Daily: newcomers arrive if the town looks like a place worth living. */
export function dailyArrivals(town: Town): void {
  const rng = town.rng;
  let openJobs = 0;
  let openBeds = 0;
  let sites = 0;
  for (const b of town.buildings) {
    openJobs += b.openJobs;
    openBeds += b.openBeds;
    if (b.isSite) sites++;
  }
  const foodPrice = town.market.price('Food');
  const res = town.workers;
  const homeless = res.filter((x) => x.homeId === null).length;
  const idle = res.filter((x) => x.jobId === null).length;

  let attraction = 0.3 + 0.06 * openJobs + 0.04 * openBeds + 0.05 * sites - (foodPrice > 1.8 ? 0.4 : 0) - 0.04 * homeless - 0.06 * idle;
  // Word travels: a popular mayor draws settlers, high taxes turn them away.
  attraction += (town.mandate - 50) / 200 - (town.taxRate - town.content.rules.taxRate) * 5;
  attraction = Math.max(0.05, Math.min(2.5, attraction));

  let groups = 0;
  for (let i = 0; i < 4; i++) if (rng.chance(attraction / 4)) groups++;

  for (let g = 0; g < groups; g++) {
    if (rng.chance(0.3)) arriveCouple(town);
    else arriveSingle(town);
  }
}

function arriveSingle(town: Town): void {
  const a = createAgent(town);
  if (a.ambition === 'OpenBusiness') {
    const dream = a.dreamBusiness
      ? `dreaming of opening a ${town.content.building(a.dreamBusiness).name.toLowerCase()}`
      : 'looking for a business to buy into';
    town.log('Arrival', Importance.Notable, `${a.describe()}, rides in with ${a.gold.toFixed(0)} gold, ${dream}.`, [a.id]);
  } else {
    town.log('Arrival', Importance.Routine, `${a.describe()}, arrives looking for work. (${a.traitList()})`, [a.id]);
  }
}

function arriveCouple(town: Town): void {
  const a = createAgent(town);
  const raceId = town.content.races.find((r) => r.name === a.race)!.id;
  const b = createAgent(town, { race: raceId, familyName: a.familyName });
  a.spouseId = b.id;
  b.spouseId = a.id;
  a.changeOpinion(b.id, 70);
  b.changeOpinion(a.id, 70);
  const pooled = a.gold + b.gold;
  town.log(
    'Arrival',
    Importance.Routine,
    `The ${a.familyName} family arrives by cart: ${a.givenName} the ${a.profession} and ${b.givenName} the ${b.profession}, with ${pooled.toFixed(0)} gold between them.`,
    [a.id, b.id],
  );
}

/** Daily: agents who have been miserable too long leave town, taking their spouse. */
export function dailyDepartures(town: Town): void {
  for (const a of town.residents) {
    if (a.departed || a.miserableDays < 4) continue;
    const reason = worstComplaint(a);
    depart(town, a);
    let withName = '';
    if (a.spouseId !== null && !town.agent(a.spouseId).departed) {
      const s = town.agent(a.spouseId);
      depart(town, s);
      withName = ` and ${s.givenName}`;
    }
    const verb = withName === '' ? 'packs up and leaves' : 'pack up and leave';
    town.log('Departure', Importance.Notable, `${a.name}${withName} ${verb} ${town.name}, ${reason}.`, [a.id]);
  }
}

function worstComplaint(a: Agent): string {
  if (a.hunger > 70) return 'unable to afford a decent meal';
  if (a.daysHomeless >= 3) return 'tired of sleeping in the mud behind the stables';
  if (a.daysUnemployed >= 4) return 'having found no work worth the name';
  if (a.loneliness > 70) return 'without a single friend in town';
  return 'worn down by frontier life';
}

export function depart(town: Town, a: Agent): void {
  a.departed = true;
  if (a.jobId !== null) remove(town.building(a.jobId).workers, a.id);
  if (a.homeId !== null) remove(town.building(a.homeId).residents, a.id);
  a.jobId = null;
  a.homeId = null;
  a.locationId = null;
  if (a.courtingId !== null) {
    town.agent(a.courtingId).courtingId = null;
    a.courtingId = null;
  }

  // Property left behind reverts to the town; businesses go up for sale.
  for (const b of town.buildings) {
    if (b.ownerId !== a.id) continue;
    b.ownerId = null;
    town.lot(b.lotId).ownerId = null;
    if (b.def.jobs > 0) putUpForSale(town, b);
  }
  for (const lot of town.lots) if (lot.ownerId === a.id) lot.ownerId = null;
}

export function remove(list: number[], id: number): void {
  const i = list.indexOf(id);
  if (i >= 0) list.splice(i, 1);
}
