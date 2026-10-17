import type { Agent } from '../agent.ts';
import type { MotionKind, SeatDef, SeatId } from '../content.ts';
import { Importance } from '../ledger.ts';
import { modifier } from '../rng.ts';
import { HOURS_PER_DAY } from '../time.ts';
import type { Town } from '../town.ts';
import { commissionHousing } from './mayor.ts';

/**
 * The town council: townsfolk who hold seats, each with a portfolio, an agenda and a loyalty to
 * the mayor. Councillors propose motions, the council votes, and the mayor signs or vetoes.
 * Overruling the council is a decree: it works, but it costs Mandate and loyalty. A councillor
 * whose loyalty sinks starts working against the mayor in their own portfolio.
 */

export interface Seat {
  def: SeatDef;
  holderId: number | null;
  /** Loyalty to the mayor, 0 to 100. */
  loyalty: number;
  lowLoyaltyDays: number;
}

export type MotionStatus = 'pending' | 'passed' | 'failed';

export interface Vote {
  seat: SeatId;
  yes: boolean;
}

export interface Motion {
  id: number;
  kind: MotionKind;
  proposer: SeatId;
  proposerId: number;
  text: string;
  /** Gold the treasury pays if it passes. */
  cost: number;
  proposedTick: number;
  deadlineTick: number;
  votes: Vote[];
  status: MotionStatus;
  /** Whether the outcome overruled the council's majority. */
  decree: boolean;
  decidedBy: 'mayor' | 'council' | null;
}

const LOW_LOYALTY = 25;
const RESIGN_LOYALTY = 10;

export function initCouncil(town: Town): void {
  town.mandate = town.content.council.startingMandate;
  for (const def of town.content.council.seats) town.seats.push({ def, holderId: null, loyalty: 50, lowLoyaltyDays: 0 });
}

export function seat(town: Town, id: SeatId): Seat | undefined {
  return town.seats.find((s) => s.def.id === id);
}

export function holder(town: Town, id: SeatId): Agent | null {
  const s = seat(town, id);
  return s && s.holderId !== null ? town.agent(s.holderId) : null;
}

/** Ability modifier of the seat's key ability: how good the holder is at the job. */
export function competence(town: Town, s: Seat): number {
  return s.holderId === null ? 0 : modifier(town.agent(s.holderId).abilities[s.def.ability]);
}

export function pendingMotion(town: Town): Motion | undefined {
  return town.motions.find((m) => m.status === 'pending');
}

/** How well the watch keeps the peace, 0 to ~0.85: more watchmen, a capable and loyal captain. */
export function watchStrength(town: Town): number {
  const cap = seat(town, 'captain');
  let effect = 1;
  if (cap && cap.holderId !== null) {
    effect += 0.1 * competence(town, cap);
    if (cap.loyalty < LOW_LOYALTY) effect *= 0.5;
  }
  return Math.min(0.85, town.watch * 0.12 * effect);
}

/** Bonus mood while a festival runs. */
export function festivalMood(town: Town): number {
  return town.festivalDays > 0 ? town.content.council.festivalMood : 0;
}

export function daily(town: Town, autoDecide: boolean): void {
  if (town.festivalDays > 0) town.festivalDays--;
  payWatch(town);
  fillSeats(town);
  driftMandate(town);
  for (const s of town.seats) if (s.holderId !== null) loyaltyDay(town, s);

  const pending = pendingMotion(town);
  if (pending && (autoDecide || town.tick >= pending.deadlineTick)) resolve(town, pending, null, !autoDecide);
  if (!pendingMotion(town)) propose(town, autoDecide);
}

/** The player's lever: sign or veto the motion before the council. */
export function decide(town: Town, motionId: number, sign: boolean): boolean {
  const m = town.motions.find((x) => x.id === motionId);
  if (!m || m.status !== 'pending') return false;
  resolve(town, m, sign);
  return true;
}

function majority(m: Motion): boolean | null {
  const yes = m.votes.filter((v) => v.yes).length;
  const no = m.votes.length - yes;
  return yes === no ? null : yes > no;
}

function payWatch(town: Town): void {
  if (town.watch === 0) return;
  const wage = town.watch * town.content.council.watchWage;
  if (town.treasury >= wage) town.book('Watch', -wage);
  else {
    const left = Math.max(0, Math.floor(town.treasury / town.content.council.watchWage));
    town.book('Watch', -left * town.content.council.watchWage);
    town.log('Council', Importance.Notable, `With no gold for their wages, ${town.watch - left} of the watch hand in their badges.`);
    town.watch = left;
  }
}

function fillSeats(town: Town): void {
  const pop = town.population;
  for (const s of town.seats) {
    if (s.holderId !== null) {
      const h = town.agent(s.holderId);
      if (!h.departed) continue;
      s.holderId = null;
      town.log('Council', Importance.Notable, `${h.name} is gone from ${town.name}, and the council has no ${s.def.title}.`, [h.id]);
    }
    if (pop < s.def.unlockPopulation) continue;
    const seated = new Set(town.seats.map((x) => x.holderId));
    const ab = s.def.ability;
    const candidate = town.workers
      .filter((a) => !seated.has(a.id) && town.tick - a.arrivedTick >= 2 * HOURS_PER_DAY)
      .sort((x, y) => score(y, ab) - score(x, ab) || x.id - y.id)[0];
    if (!candidate) continue;
    s.holderId = candidate.id;
    s.loyalty = 45 + town.rng.range(0, 20);
    s.lowLoyaltyDays = 0;
    town.log(
      'Council',
      Importance.Notable,
      `${candidate.name}, ${candidate.describe().slice(candidate.name.length + 2)}, joins the council as ${s.def.title}, ` +
        `minding ${s.def.portfolio}. They want ${s.def.agenda}.`,
      [candidate.id],
    );
  }
}

function score(a: Agent, ab: keyof Agent['abilities']): number {
  return a.abilities[ab] + (a.has('Ambitious') ? 2 : 0) + (a.has('Lazy') ? -2 : 0) + Math.min(3, a.gold / 100);
}

function driftMandate(town: Town): void {
  const res = town.residents;
  const mood = res.length ? res.reduce((s, a) => s + a.lastDayMood, 0) / res.length : 65;
  const speaker = seat(town, 'speaker');
  let drift = (mood - 70) * 0.04;
  if (speaker && speaker.holderId !== null) drift += speaker.loyalty >= LOW_LOYALTY ? 0.15 * competence(town, speaker) : -0.5;
  town.mandate = clamp(town.mandate + drift, 0, 100);
}

function loyaltyDay(town: Town, s: Seat): void {
  const h = town.agent(s.holderId!);
  // Loyalty eases back toward neutral; a popular mayor wins people over.
  s.loyalty = clamp(s.loyalty + (50 - s.loyalty) * 0.02 + (town.mandate - 50) * 0.01, 0, 100);
  if (s.loyalty >= LOW_LOYALTY) {
    s.lowLoyaltyDays = 0;
    return;
  }
  s.lowLoyaltyDays++;
  if (s.loyalty < RESIGN_LOYALTY && s.lowLoyaltyDays >= 5) {
    town.log('Council', Importance.Major, `${h.name} resigns as ${s.def.title}, denouncing the mayor before the whole council.`, [h.id]);
    town.mandate = clamp(town.mandate - 5, 0, 100);
    s.holderId = null;
    return;
  }
  if (!town.rng.chance(0.15)) return;
  // A disloyal councillor turns their portfolio against the mayor, deniably.
  if (s.def.id === 'steward') {
    const take = Math.min(40, Math.floor(town.treasury * 0.04));
    if (take <= 0) return;
    town.book('Discrepancies', -take);
    h.gold += take;
    town.log('Council', Importance.Routine, `The treasury ledgers don't quite add up this week. ${h.givenName} the Steward has a new silk coat.`, [h.id]);
  } else if (s.def.id === 'speaker') {
    town.mandate = clamp(town.mandate - 3, 0, 100);
    town.log('Council', Importance.Routine, `${h.name} tells anyone who will listen that the mayor ignores the common folk.`, [h.id]);
  } else {
    town.log('Council', Importance.Routine, `The night watch is seen dicing in the guardhouse. Captain ${h.givenName} doesn't seem to mind.`, [h.id]);
  }
}

interface Proposal {
  kind: MotionKind;
  cost: number;
  why: string;
}

function propose(town: Town, autoDecide: boolean): void {
  const c = town.content.council;
  const last = town.motions[town.motions.length - 1];
  if (last && town.tick - last.proposedTick < c.motionGapDays * HOURS_PER_DAY) return;
  for (const s of town.seats) {
    if (s.holderId === null) continue;
    const p = proposalFor(town, s.def.id);
    if (!p || recentlyFailed(town, p.kind) || !town.rng.chance(0.5)) continue;
    const h = town.agent(s.holderId);
    const m: Motion = {
      id: town.motions.length + 1,
      kind: p.kind,
      proposer: s.def.id,
      proposerId: h.id,
      text: motionText(town, p),
      cost: p.cost,
      proposedTick: town.tick,
      deadlineTick: town.tick + c.motionDays * HOURS_PER_DAY,
      votes: [],
      status: 'pending',
      decree: false,
      decidedBy: null,
    };
    m.votes = castVotes(town, m);
    town.motions.push(m);
    const yes = m.votes.filter((v) => v.yes).length;
    town.log(
      'Council',
      autoDecide ? Importance.Routine : Importance.Notable,
      `${h.name} the ${s.def.title} moves that ${m.text} (${p.why}). The council votes ${yes} to ${m.votes.length - yes}.`,
      [h.id],
    );
    return;
  }
}

/** A motion that just failed isn't brought back for a while. */
function recentlyFailed(town: Town, kind: MotionKind): boolean {
  return town.motions.some((m) => m.kind === kind && m.status === 'failed' && town.tick - m.proposedTick < 12 * HOURS_PER_DAY);
}

function proposalFor(town: Town, id: SeatId): Proposal | null {
  const c = town.content.council;
  // A week's average, so one big land sale or charter house doesn't swing the council.
  const books = town.recentNet.length ? town.recentNet.reduce((s, v) => s + v, 0) / town.recentNet.length : 0;
  const upkeep = -(town.accountsYesterday['Upkeep'] ?? 0) - (town.accountsYesterday['Watch'] ?? 0);
  const workers = town.workers;
  const homeless = workers.filter((a) => a.homeId === null).length;
  const mood = workers.length ? workers.reduce((s, a) => s + a.lastDayMood, 0) / workers.length : 65;
  const festival = Math.round(c.festivalBase + c.festivalPerResident * town.population);
  const brawls = town.ledger.entries.filter((e) => e.category === 'Brawl' && town.tick - e.tick < 7 * HOURS_PER_DAY).length;

  switch (id) {
    case 'steward':
      if ((books < 0 || town.treasury < upkeep * 5) && town.taxRate < c.maxTax - 1e-9)
        return { kind: 'raise_tax', cost: 0, why: books < 0 ? 'the books have run red this week' : 'the treasury is thin' };
      if (books < 0 && town.watch > 0) return { kind: 'dismiss_watch', cost: 0, why: 'the watch costs more than the town can bear' };
      return null;
    case 'captain':
      if (brawls >= 2 || town.watch < Math.floor(town.population / 30))
        return { kind: 'hire_watch', cost: 0, why: brawls >= 2 ? `${brawls} brawls this week` : 'the streets go unwatched' };
      return null;
    case 'speaker':
      if (homeless >= 3) return { kind: 'build_homes', cost: houseCost(town), why: `${homeless} people sleep rough` };
      const lastFestival = town.motions.filter((m) => m.kind === 'festival' && m.status === 'passed').pop();
      const festivalDue = !lastFestival || town.tick - lastFestival.proposedTick > 20 * HOURS_PER_DAY;
      if (mood < 70 && festivalDue && town.festivalDays === 0 && town.treasury > festival * 4)
        return { kind: 'festival', cost: festival, why: 'spirits are low' };
      if (town.taxRate > c.minTax + 1e-9 && town.treasury > upkeep * 20 && books > 0)
        return { kind: 'lower_tax', cost: 0, why: 'the coffers are full while the people pay' };
      return null;
  }
}

function houseCost(town: Town): number {
  const house = town.content.building('house');
  return Math.round(house.cost + town.market.quote('Timber', house.timber));
}

const pct = (r: number): string => `${Math.round(r * 100)}%`;

function motionText(town: Town, p: Proposal): string {
  const c = town.content.council;
  switch (p.kind) {
    case 'raise_tax':
      return `the tax rise from ${pct(town.taxRate)} to ${pct(town.taxRate + c.taxStep)}`;
    case 'lower_tax':
      return `the tax fall from ${pct(town.taxRate)} to ${pct(town.taxRate - c.taxStep)}`;
    case 'hire_watch':
      return `the town hire ${c.watchPerMotion} more watchmen at ${c.watchWage} gold a day each`;
    case 'dismiss_watch':
      return `${c.watchPerMotion} watchmen be let go`;
    case 'festival':
      return `the town hold a festival, at a cost of ${p.cost} gold`;
    case 'build_homes':
      return `the town build a charter house for the homeless, at a cost of ${p.cost} gold`;
  }
}

function castVotes(town: Town, m: Motion): Vote[] {
  const votes: Vote[] = [];
  for (const s of town.seats) {
    if (s.holderId === null) continue;
    if (s.def.id === m.proposer) {
      votes.push({ seat: s.def.id, yes: true });
      continue;
    }
    const voter = town.agent(s.holderId);
    const lean = town.content.council.alignment[m.kind][s.def.id] * 50 + voter.opinionOf(m.proposerId) * 0.3 + town.rng.range(-25, 25);
    votes.push({ seat: s.def.id, yes: lean > 0 });
  }
  return votes;
}

/** Settle a motion. `sign` is the mayor's call, or null when the council's majority stands. */
function resolve(town: Town, m: Motion, sign: boolean | null, lapsed = false): void {
  const council = majority(m);
  const passed = sign ?? council === true;
  m.status = passed ? 'passed' : 'failed';
  m.decidedBy = sign === null ? 'council' : 'mayor';
  m.decree = sign !== null && council !== null && sign !== council;

  const proposer = seat(town, m.proposer);
  if (sign !== null) {
    if (proposer && proposer.holderId === m.proposerId) proposer.loyalty = clamp(proposer.loyalty + (sign ? 12 : -12), 0, 100);
    for (const v of m.votes) {
      const s = seat(town, v.seat);
      if (!s || s.holderId === null || v.seat === m.proposer) continue;
      s.loyalty = clamp(s.loyalty + (v.yes === passed ? 3 : -3) - (m.decree && v.yes !== passed ? 8 : 0), 0, 100);
    }
    if (m.decree) town.mandate = clamp(town.mandate - town.content.council.decreeMandate, 0, 100);
  }

  const who = sign === null ? (lapsed ? 'With the mayor silent, the council' : 'The council') : m.decree ? 'By decree, overruling the council, the mayor' : 'The mayor';
  if (!passed) {
    town.log('Council', Importance.Notable, `${who} ${sign === null ? 'lets the motion fall' : 'vetoes the motion'}: ${m.text}.`, [m.proposerId]);
    return;
  }
  if (m.cost > town.treasury) {
    m.status = 'failed';
    town.log('Council', Importance.Notable, `The motion carries, but the treasury cannot pay for it: ${m.text}.`, [m.proposerId]);
    return;
  }
  town.log('Council', Importance.Notable, `${who} ${sign === null ? 'carries the motion' : 'signs the motion'}: ${m.text}.`, [m.proposerId]);
  enact(town, m);
}

function enact(town: Town, m: Motion): void {
  const c = town.content.council;
  switch (m.kind) {
    case 'raise_tax':
      town.taxRate = Math.min(c.maxTax, town.taxRate + c.taxStep);
      town.mandate = clamp(town.mandate - 4, 0, 100);
      break;
    case 'lower_tax':
      town.taxRate = Math.max(c.minTax, town.taxRate - c.taxStep);
      town.mandate = clamp(town.mandate + 4, 0, 100);
      break;
    case 'hire_watch':
      town.watch += c.watchPerMotion;
      break;
    case 'dismiss_watch':
      town.watch = Math.max(0, town.watch - c.watchPerMotion);
      break;
    case 'festival':
      town.book('Council motions', -m.cost);
      town.festivalDays = c.festivalDays;
      town.mandate = clamp(town.mandate + 5, 0, 100);
      town.log('Town', Importance.Major, `Lanterns go up across ${town.name}: the town holds a festival, and the ale flows on the council's coin.`);
      break;
    case 'build_homes':
      if (!commissionHousing(town, 1)) town.log('Council', Importance.Routine, `There is nowhere to build the charter house yet.`);
      break;
  }
}

const clamp = (v: number, lo: number, hi: number): number => Math.max(lo, Math.min(hi, v));
