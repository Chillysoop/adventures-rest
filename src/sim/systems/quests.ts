import { type Agent, type HeroSheet, rollAbilities } from '../agent.ts';
import type { ClassDef, SiteDef } from '../content.ts';
import { Importance } from '../ledger.ts';
import { HOURS_PER_DAY, dayOf } from '../time.ts';
import type { Town } from '../town.ts';
import { heroMaxHp, listNames, runExpedition, type ExpeditionOutcome } from './expedition.ts';
import { createAgent, depart } from './population.ts';

/**
 * Heroes, the bounty board and expeditions. The mayor never sends a hero anywhere: the town
 * posts bounties with gold held in escrow, and heroes decide for themselves whether the odds and
 * the pay are worth it, forming parties with people they like.
 */

export interface SiteState {
  def: SiteDef;
  cleared: boolean;
  attempts: number;
}

export type BountyStatus = 'open' | 'taken' | 'completed' | 'failed' | 'expired';

export interface Bounty {
  id: number;
  siteId: string;
  reward: number;
  postedTick: number;
  expiresTick: number;
  status: BountyStatus;
  partyId: number | null;
}

export interface Party {
  id: number;
  name: string;
  members: number[];
  bountyId: number;
  siteId: string;
  departTick: number;
  returnTick: number;
  returned: boolean;
}

export interface Chronicle {
  id: number;
  partyName: string;
  siteName: string;
  members: string[];
  departDay: number;
  returnDay: number;
  outcome: ExpeditionOutcome;
  lines: string[];
}

const MAX_HEROES = 12;
const BOUNTY_DAYS = 10;
const LODGING = 1;
const XP_LEVELS = [0, 300, 900, 2700, 6500, 14000];

const PARTY_ADJECTIVES = ['Ashen', 'Iron', 'Crimson', 'Silver', 'Gallows', 'Lucky', 'Broken', 'Midnight', 'Copper', 'Wandering'];
const PARTY_NOUNS = ['Blades', 'Lanterns', 'Owls', 'Shields', 'Hounds', 'Crows', 'Compasses', 'Wolves', 'Pilgrims', 'Daggers'];

export function initSites(town: Town): void {
  for (const def of town.content.sites) town.sites.push({ def, cleared: false, attempts: 0 });
}

export function site(town: Town, id: string): SiteState {
  return town.sites.find((s) => s.def.id === id)!;
}

/** Post a bounty, holding the reward in escrow from the treasury. Returns null if the town can't pay. */
export function postBounty(town: Town, siteId: string, reward: number): Bounty | null {
  const s = site(town, siteId);
  if (!s || s.cleared || reward <= 0 || town.treasury < reward) return null;
  if (town.bounties.some((b) => b.siteId === siteId && (b.status === 'open' || b.status === 'taken'))) return null;
  town.treasury -= reward;
  const b: Bounty = {
    id: town.bounties.length + 1,
    siteId,
    reward,
    postedTick: town.tick,
    expiresTick: town.tick + BOUNTY_DAYS * HOURS_PER_DAY,
    status: 'open',
    partyId: null,
  };
  town.bounties.push(b);
  town.log('Town', Importance.Notable, `The council posts a bounty of ${reward} gold on ${s.def.name}.`);
  return b;
}

export function suggestedReward(tier: number): number {
  return 50 * tier + 30 * tier * tier;
}

export function createHero(town: Town, cls: ClassDef, level: number): Agent {
  const a = createAgent(town, { profession: cls.name, gold: town.rng.range(10, 40) });
  a.ambition = 'None';
  a.dreamBusiness = null;
  // Heroes favor their class's key ability: reroll it once and keep the better.
  const reroll = rollAbilities(town.rng)[cls.primary];
  a.abilities[cls.primary] = Math.max(a.abilities[cls.primary], reroll);
  const sheet: HeroSheet = {
    cls,
    level,
    xp: XP_LEVELS[level - 1],
    maxHp: 1,
    hp: 1,
    weaponBonus: 0,
    armorBonus: 0,
    items: [],
    renown: 0,
    kills: 0,
    expeditions: 0,
    partyName: null,
    awayUntil: null,
    lodging: false,
  };
  sheet.maxHp = heroMaxHp(sheet, a.abilities.con);
  sheet.hp = sheet.maxHp;
  a.hero = sheet;
  return a;
}

function pickClass(town: Town): ClassDef {
  const classes = town.content.classes;
  const total = classes.reduce((s, c) => s + c.weight, 0);
  let roll = town.rng.range(0, total);
  for (const c of classes) {
    if (roll < c.weight) return c;
    roll -= c.weight;
  }
  return classes[0];
}

export function daily(town: Town, autoMayor: boolean): void {
  returnParties(town);
  heroArrivals(town);
  lodgingAndRest(town);
  expireBounties(town);
  if (autoMayor) autoPost(town);
  formParties(town);
}

function heroArrivals(town: Town): void {
  const taverns = town.taverns;
  if (taverns.length === 0) return;
  const heroes = town.heroes.length;
  if (heroes >= MAX_HEROES) return;
  const open = town.bounties.filter((b) => b.status === 'open').length;
  const chance = Math.min(0.9, 0.12 + 0.25 * open + (heroes < 4 ? 0.2 : 0));
  if (!town.rng.chance(chance)) return;

  const level = town.rng.chance(0.7) ? 1 : town.rng.chance(0.8) ? 2 : 3;
  const a = createHero(town, pickClass(town), level);
  const where = taverns[town.rng.range(0, taverns.length)].name;
  town.log('Arrival', Importance.Notable, `${a.describe()}, walks into ${where} looking for paid work. (${a.traitList()})`, [a.id]);
}

function lodgingAndRest(town: Town): void {
  const taverns = town.taverns;
  for (const a of town.heroes) {
    const s = a.hero!;
    if (s.awayUntil !== null) continue;
    // A bed above the tavern.
    s.lodging = taverns.length > 0 && a.gold >= LODGING;
    if (s.lodging) {
      const t = taverns[a.id % taverns.length];
      a.gold -= LODGING;
      t.cash += LODGING;
    }
    // A long rest heals; a bed heals faster.
    s.hp = Math.min(s.maxHp, s.hp + Math.ceil(s.maxHp * (s.lodging ? 0.5 : 0.25)));
  }
}

function expireBounties(town: Town): void {
  for (const b of town.bounties) {
    if (b.status !== 'open' || town.tick < b.expiresTick) continue;
    b.status = 'expired';
    town.treasury += b.reward;
    town.log('Town', Importance.Routine, `No one takes the bounty on ${site(town, b.siteId).def.name}; the council withdraws it.`);
  }
}

/** Stand-in mayor: put idle treasury gold to work on the easiest uncleared site. */
function autoPost(town: Town): void {
  if (town.bounties.some((b) => b.status === 'open' || b.status === 'taken')) return;
  if (town.heroes.length === 0 && town.taverns.length === 0) return;
  const target = town.sites.filter((s) => !s.cleared).sort((a, b) => a.def.tier - b.def.tier || a.attempts - b.attempts)[0];
  if (!target) return;
  const reward = suggestedReward(target.def.tier) + 20 * target.attempts;
  if (town.treasury < reward + 100) return;
  postBounty(town, target.def.id, reward);
}

function interest(town: Town, a: Agent, bounty: Bounty, partySize: number): number {
  const tier = site(town, bounty.siteId).def.tier;
  const share = bounty.reward / Math.max(1, partySize);
  return (
    share / (tier * 40) +
    (a.has('Greedy') ? 0.2 : 0) +
    (a.has('HotTempered') ? 0.15 : 0) +
    (a.has('Ambitious') ? 0.1 : 0) -
    (a.has('Lazy') ? 0.3 : 0) +
    town.rng.next() * 0.3
  );
}

function available(a: Agent): boolean {
  const s = a.hero!;
  return s.awayUntil === null && s.hp >= s.maxHp * 0.7 && !a.departed;
}

function formParties(town: Town): void {
  for (const bounty of town.bounties) {
    if (bounty.status !== 'open') continue;
    const s = site(town, bounty.siteId);
    const tier = s.def.tier;
    const pool = town.heroes.filter(available).sort((a, b) => b.hero!.level - a.hero!.level || a.id - b.id);
    if (pool.length === 0) continue;

    const leader = pool.find((h) => interest(town, h, bounty, 3) >= 0.6);
    if (!leader) continue;

    const members = [leader];
    let levels = leader.hero!.level;
    const recruits = pool
      .filter((h) => h.id !== leader.id)
      .sort((a, b) => leader.opinionOf(b.id) - leader.opinionOf(a.id) || b.hero!.level - a.hero!.level || a.id - b.id);
    for (const r of recruits) {
      if (members.length >= 4 || (members.length >= 3 && levels >= 3 * tier + 2)) break;
      if (leader.opinionOf(r.id) < -10 || r.opinionOf(leader.id) < -10) continue;
      if (interest(town, r, bounty, members.length + 1) < 0.5) continue;
      members.push(r);
      levels += r.hero!.level;
    }
    // Heroes won't walk into a site they can't handle; the bounty waits for a stronger company.
    if (levels < 3 * tier || members.length < 3) continue;

    const name = leader.hero!.partyName ?? partyName(town);
    const travel = s.def.distance * 2 + 1;
    const party: Party = {
      id: town.parties.length + 1,
      name,
      members: members.map((m) => m.id),
      bountyId: bounty.id,
      siteId: s.def.id,
      departTick: town.tick,
      returnTick: town.tick + travel * HOURS_PER_DAY,
      returned: false,
    };
    town.parties.push(party);
    bounty.status = 'taken';
    bounty.partyId = party.id;
    s.attempts++;
    for (const m of members) {
      m.hero!.partyName = name;
      m.hero!.awayUntil = party.returnTick;
      m.activity = 'Idle';
      m.locationId = null;
    }
    town.log(
      'Quest',
      Importance.Notable,
      `The ${name} (${members.map((m) => `${m.givenName} the ${m.hero!.cls.name}`).join(', ')}) take the bounty on ${s.def.name} and march out at dawn.`,
      members.map((m) => m.id),
    );
  }
}

function partyName(town: Town): string {
  for (let i = 0; i < 20; i++) {
    const name = `${town.rng.pick(PARTY_ADJECTIVES)} ${town.rng.pick(PARTY_NOUNS)}`;
    if (town.parties.every((p) => p.name !== name)) return name;
  }
  return `Company No. ${town.parties.length + 1}`;
}

function returnParties(town: Town): void {
  for (const party of town.parties) {
    if (party.returned || town.tick < party.returnTick) continue;
    party.returned = true;
    const s = site(town, party.siteId);
    const bounty = town.bounties[party.bountyId - 1];
    const members = party.members.map((id) => town.agent(id));
    const result = runExpedition(town.rng, town.content, s.def, members);

    const chronicle: Chronicle = {
      id: town.chronicles.length + 1,
      partyName: party.name,
      siteName: s.def.name,
      members: members.map((m) => m.describe()),
      departDay: dayOf(party.departTick),
      returnDay: town.day,
      outcome: result.outcome,
      lines: result.lines,
    };
    town.chronicles.push(chronicle);

    const survivors = members.filter((m) => !result.dead.includes(m.id));
    for (const m of members) {
      const sheet = m.hero!;
      sheet.awayUntil = null;
      sheet.expeditions++;
      sheet.kills += result.kills.get(m.id) ?? 0;
    }

    // The dead are mourned; the living split the take.
    for (const id of result.dead) {
      const d = town.agent(id);
      d.dead = true;
      depart(town, d);
      for (const other of town.residents) if (other.opinionOf(id) >= 40) other.loneliness = Math.min(100, other.loneliness + 30);
    }

    let pay = result.gold;
    if (result.outcome === 'cleared') {
      s.cleared = true;
      bounty.status = 'completed';
      pay += bounty.reward;
    } else {
      bounty.status = 'failed';
      town.treasury += bounty.reward;
    }
    if (survivors.length > 0) {
      const share = pay / survivors.length;
      const xpShare = Math.floor(result.xp / survivors.length);
      for (const m of survivors) {
        m.gold += share;
        gainXp(town, m, xpShare);
        m.hero!.renown += result.outcome === 'cleared' ? 2 * s.def.tier : 0;
        for (const o of survivors) if (o.id !== m.id) m.changeOpinion(o.id, 15);
      }
    }

    const names = listNames(members.map((m) => m.givenName));
    const deadNames = listNames(result.dead.map((id) => town.agent(id).givenName));
    let text: string;
    if (result.outcome === 'cleared') {
      text = `The ${party.name} return from ${s.def.name} victorious with ${Math.round(pay)} gold to split` + (result.dead.length ? `, but ${deadNames} did not come home.` : '.');
    } else if (result.outcome === 'wiped') {
      text = `The ${party.name} never return from ${s.def.name}. ${names} ${members.length === 1 ? 'is' : 'are'} presumed dead.`;
    } else {
      text = `The ${party.name} limp back from ${s.def.name} empty-handed` + (result.dead.length ? `, carrying ${result.dead.length === 1 ? `${deadNames}'s body` : `the bodies of ${deadNames}`}.` : '.');
    }
    town.log('Quest', Importance.Major, `${text} (Chronicle ${chronicle.id})`, members.map((m) => m.id));
  }
}

function gainXp(town: Town, a: Agent, xp: number): void {
  const s = a.hero!;
  s.xp += xp;
  while (s.level < XP_LEVELS.length && s.xp >= XP_LEVELS[s.level]) {
    s.level++;
    const before = s.maxHp;
    s.maxHp = heroMaxHp(s, a.abilities.con);
    s.hp += s.maxHp - before;
    town.log('Quest', Importance.Notable, `${a.name} reaches level ${s.level}.`, [a.id]);
  }
}

export function formatChronicle(c: Chronicle): string {
  const head = `Chronicle ${c.id}: the ${c.partyName} at ${c.siteName} (days ${c.departDay} to ${c.returnDay}, ${c.outcome})`;
  return [head, ...c.members.map((m) => `  ${m}`), ...c.lines.map((l) => `  - ${l}`)].join('\n');
}
