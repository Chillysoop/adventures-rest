import type { Agent } from '../agent.ts';
import type { SiteDef } from '../content.ts';
import { Importance } from '../ledger.ts';
import { HOURS_PER_DAY, dayOf } from '../time.ts';
import type { Town } from '../town.ts';
import { listNames, runExpedition } from './expedition.ts';
import { depart } from './population.ts';
import type { Chronicle } from './quests.ts';

/**
 * The slice's Tier 2 enemy, a warband that grows off-screen on the daily tick. Uncleared sites
 * feed it; heroes clearing them, and its war camp, starve it. Pressure on the town climbs through
 * visible stages (omens, probing, raids, the march) and ends in the Last Stand: a siege at the
 * walls, and the warlord in person. Kill the warlord and the town wins; let the walls break twice
 * and it falls.
 */

export type ThreatStage = 'quiet' | 'omens' | 'probing' | 'raids' | 'marching' | 'defeated' | 'victorious';

export interface EnemyState {
  strength: number;
  stage: ThreatStage;
  /** When the marching warband reaches the walls. */
  arrivalTick: number | null;
  nextRaidTick: number | null;
  outpost: boolean;
  sieges: number;
  raids: number;
  lastSignTick: number;
  /** Signs shown lately, so the same rumour doesn't repeat. */
  recentSigns: string[];
}

export interface Defenses {
  palisade: 'none' | 'building' | 'built';
  palisadeReadyTick: number | null;
  towers: number;
  /** Ready ticks of towers under construction. */
  towersBuilding: number[];
}

export type Outcome = 'victory' | 'defeat' | null;

export function initThreat(town: Town): void {
  town.enemy.strength = town.content.enemy.startStrength;
}

const STAGE_ORDER: ThreatStage[] = ['quiet', 'omens', 'probing', 'raids', 'marching'];

export function isActive(town: Town): boolean {
  return town.enemy.stage !== 'defeated' && town.enemy.stage !== 'victorious';
}

/** Daily growth before the Director's hand: uncleared sites feed the warband, the war camp most of all. */
export function growth(town: Town): number {
  const e = town.content.enemy;
  let g = e.baseGrowth + e.growthPerDay * town.day + e.growthPerResident * town.population;
  for (const s of town.sites) {
    if (s.cleared) continue;
    if (s.def.id === e.outpost.id) g += e.outpostGrowth;
    else g += s.def.tier >= 3 ? e.growthPerTier3Site : e.growthPerSite;
  }
  return g;
}

/** The town's strength at the walls: watch, heroes at home, a militia of townsfolk, and fortifications. */
export function defense(town: Town): number {
  const d = town.content.enemy.defense;
  const heroLevels = town.heroes.filter((a) => !a.isAway).reduce((s, a) => s + a.hero!.level, 0);
  const base = town.watch * d.watchman + heroLevels * d.heroLevel + town.workers.length * d.militiaPerWorker;
  return base * fortification(town);
}

export function fortification(town: Town): number {
  const d = town.content.enemy.defense;
  return (town.defenses.palisade === 'built' ? d.palisade : 1) * (1 + d.perTower * town.defenses.towers);
}

/** What the town's scouts believe the warband numbers, rounded the way rumours are. */
export function estimatedStrength(town: Town): number | null {
  if (STAGE_ORDER.indexOf(town.enemy.stage) < STAGE_ORDER.indexOf('probing')) return null;
  return Math.max(10, Math.round(town.enemy.strength / 10) * 10);
}

export function daily(town: Town, director: number, autoMayor: boolean, enemy = true): void {
  finishDefenses(town);
  const en = town.enemy;
  if (!enemy || !isActive(town)) return;

  if (en.stage !== 'marching') en.strength += growth(town) * director;
  outpostFalls(town);
  advance(town);
  if (en.stage === 'raids' && en.nextRaidTick !== null && town.tick >= en.nextRaidTick) raid(town);
  if (en.stage === 'marching' && en.arrivalTick !== null && town.tick >= en.arrivalTick) siege(town);
  else if (en.stage !== 'marching' && town.tick - en.lastSignTick >= 4 * HOURS_PER_DAY && town.rng.chance(0.35)) sign(town);
  if (autoMayor) fortify(town);
}

function advance(town: Town): void {
  const e = town.content.enemy;
  const en = town.enemy;
  const next = (s: ThreatStage): void => {
    en.stage = s;
    en.lastSignTick = town.tick;
  };
  // Every stage is announced before it bites, so nothing arrives without a tell.
  if (en.stage === 'quiet' && en.strength >= e.stages.omens) {
    next('omens');
    town.log('War', Importance.Major, e.omens[0]);
    en.recentSigns.push(e.omens[0]);
  } else if (en.stage === 'omens' && en.strength >= e.stages.probing) {
    next('probing');
    spawnOutpost(town);
  } else if (en.stage === 'probing' && en.strength >= e.stages.raids) {
    next('raids');
    en.nextRaidTick = town.tick + town.rng.range(2, 4) * HOURS_PER_DAY;
    town.log('War', Importance.Major, `${cap(e.name)} has grown bold. Its raiders will come for the outlying farms next.`);
  } else if (en.stage === 'raids' && en.strength >= e.stages.march && en.strength >= defense(town) * e.marchConfidence) {
    next('marching');
    en.arrivalTick = town.tick + e.marchDays * HOURS_PER_DAY;
    town.log(
      'War',
      Importance.Major,
      `${cap(e.name)} is on the march! ${e.leader} leads some ${Math.round(en.strength / 10) * 10} warriors toward ${town.name}. ` +
        `They will reach the walls in ${e.marchDays} days.`,
    );
  }
}

function sign(town: Town): void {
  const e = town.content.enemy;
  const en = town.enemy;
  const pool = en.stage === 'omens' ? e.omens : en.stage === 'probing' || en.stage === 'raids' ? e.probing : [];
  const fresh = pool.filter((p) => !en.recentSigns.includes(p));
  if (fresh.length === 0) return;
  en.lastSignTick = town.tick;
  const text = fresh[town.rng.range(0, fresh.length)];
  en.recentSigns.push(text);
  if (en.recentSigns.length > 3) en.recentSigns.shift();
  town.log('War', Importance.Notable, text);
  // A caravan that never arrives leaves the market short.
  if (text.includes('caravan')) town.market.seed('Food', -Math.floor(town.market.stock('Food') * 0.3));
}

function spawnOutpost(town: Town): void {
  const e = town.content.enemy;
  town.enemy.outpost = true;
  town.sites.push({ def: e.outpost as SiteDef, cleared: false, attempts: 0 });
  town.log(
    'War',
    Importance.Major,
    `Scouts bring word: ${e.name} has raised ${e.outpost.name} two days from town. While it stands, the warband grows faster. Heroes could burn it.`,
  );
}

function outpostFalls(town: Town): void {
  const e = town.content.enemy;
  const camp = town.sites.find((s) => s.def.id === e.outpost.id);
  if (!camp || !camp.cleared || !town.enemy.outpost) return;
  town.enemy.outpost = false;
  const lost = Math.round(town.enemy.strength * e.outpostClearedLoss);
  town.enemy.strength -= lost;
  town.log('War', Importance.Major, `With ${e.outpost.name} in ashes, ${e.name} loses some ${lost} warriors to desertion and the sword.`);
  // The warband falls back a stage if it has shrunk below it.
  if (town.enemy.stage === 'raids' && town.enemy.strength < e.stages.raids) town.enemy.stage = 'probing';
}

function raid(town: Town): void {
  const e = town.content.enemy;
  const en = town.enemy;
  const rng = town.rng;
  en.raids++;
  en.nextRaidTick = town.tick + rng.range(e.raidEveryDays[0], e.raidEveryDays[1] + 1) * HOURS_PER_DAY;
  const raiders = Math.max(4, Math.round(en.strength * e.raidShare * (0.7 + rng.next() * 0.6)));
  const d = e.defense;
  const home = town.heroes.filter((a) => !a.isAway);
  const local =
    (town.watch * d.watchman + home.reduce((s, a) => s + a.hero!.level, 0) * d.heroLevel) *
    fortification(town) *
    (0.7 + rng.next() * 0.6);

  // They strike the farm or camp farthest from the town hall.
  const target = town.buildings
    .filter((b) => b.complete && (b.def.zone === 'Farmland' || b.def.id === 'lumber_camp'))
    .sort((x, y) => distance(town, y.lotId) - distance(town, x.lotId) || x.id - y.id)[0];
  const where = target ? target.name : 'the outlying fields';

  if (local >= raiders) {
    en.strength = Math.max(0, en.strength - raiders * 0.5);
    const champion = home.sort((x, y) => y.hero!.level - x.hero!.level || x.id - y.id)[0];
    const by = champion ? `${champion.name} and the watch` : 'the watch';
    town.log('War', Importance.Notable, `Some ${raiders} raiders fall on ${where} in the night, and ${by} drive them off.`, champion ? [champion.id] : []);
    town.tension += 8;
    return;
  }
  let text = `Some ${raiders} raiders fall on ${where} in the night`;
  const victims: number[] = [];
  if (target) {
    const loot = Math.floor(target.cash * 0.6);
    target.cash -= loot;
    for (const [g, q] of target.stock) target.stock.set(g, Math.floor(q * 0.3));
    text += `, carrying off the stores and ${loot} gold`;
    const workers = target.workers.map((id) => town.agent(id)).filter((a) => !a.departed);
    if (workers.length && rng.chance(0.45)) {
      const v = workers[rng.range(0, workers.length)];
      kill(town, v);
      victims.push(v.id);
      text += `. ${v.name} dies defending it`;
    }
  }
  town.log('War', Importance.Major, `${text}. The watch arrives too late.`, victims);
  town.tension += 25 + victims.length * 10;
  town.mandate = Math.max(0, town.mandate - 4);
}

function siege(town: Town): void {
  const e = town.content.enemy;
  const en = town.enemy;
  const rng = town.rng;
  en.sieges++;
  const lines: string[] = [];
  let def = defense(town);
  const startDef = def;
  let breaches = 0;
  lines.push(
    `${cap(e.name)}, some ${Math.round(en.strength)} strong, surrounds ${town.name}. ` +
      `The defenders muster ${Math.round(startDef)} strong` +
      (town.defenses.palisade === 'built' ? ' behind the palisade' : ' with no wall to hide behind') +
      (town.defenses.towers ? ` and ${town.defenses.towers} ${town.defenses.towers === 1 ? 'tower' : 'towers'}.` : '.'),
  );
  for (let w = 1; w <= e.siegeWaves && breaches < e.breachesToFall; w++) {
    const wave = (en.strength / (e.siegeWaves - w + 1)) * (0.8 + rng.next() * 0.4);
    const hold = (def / (e.siegeWaves - w + 1)) * (0.8 + rng.next() * 0.4);
    if (hold >= wave) {
      def -= wave * 0.15;
      en.strength -= wave;
      lines.push(`Wave ${w}: ${Math.round(wave)} orcs hit the ${w === 1 ? 'gate' : w === 2 ? 'east wall' : 'river side'} and break against it.`);
    } else {
      def -= wave * 0.35;
      en.strength -= wave * 0.5;
      breaches++;
      lines.push(`Wave ${w}: ${Math.round(wave)} orcs breach the ${w === 1 ? 'gate' : w === 2 ? 'east wall' : 'river side'}. Fighting spills into the streets.`);
    }
  }

  // Townsfolk who stood in the militia pay for it.
  const fallen = Math.min(town.workers.length, Math.round(((startDef - def) / Math.max(1, fortification(town))) / 12));
  const dead: Agent[] = [];
  const workers = [...town.workers];
  for (let i = 0; i < fallen; i++) {
    const v = workers.splice(rng.range(0, workers.length), 1)[0];
    kill(town, v);
    dead.push(v);
  }
  if (dead.length) lines.push(`${listNames(dead.map((a) => a.name))} ${dead.length === 1 ? 'falls' : 'fall'} on the walls.`);
  town.watch = Math.max(0, town.watch - breaches * 2);

  if (breaches >= e.breachesToFall) {
    lines.push(`The defence collapses. ${e.leader} raises the red banner over the town hall.`);
    en.stage = 'victorious';
    town.outcome = 'defeat';
    town.mandate = 0;
    town.book('Discrepancies', -town.treasury);
    chronicle(town, lines, 'wiped', [], startDef);
    town.log('War', Importance.Major, `${town.name} falls. ${e.leader}'s warband sacks the town and empties the treasury. (Chronicle ${town.chronicles.length})`, dead.map((a) => a.id));
    town.tension += 100;
    return;
  }

  // The walls held; now the warlord comes in person, and only heroes can meet him.
  const champions = town.heroes
    .filter((a) => !a.isAway && a.hero!.hp > 0)
    .sort((x, y) => y.hero!.level - x.hero!.level || x.id - y.id)
    .slice(0, 4);
  let slain = false;
  if (champions.length > 0) {
    lines.push(`The walls hold. ${e.leader} comes to the breach himself, and ${listNames(champions.map((a) => a.givenName))} go out to meet him.`);
    const battle: SiteDef = { id: 'last_stand', name: 'the walls', tier: 0, distance: 0, encounters: [], boss: e.warlordBattle, loot: [0, 0] };
    const result = runExpedition(rng, town.content, battle, champions);
    lines.push(...result.lines.slice(1).filter((l) => !l.startsWith('At last') && !l.includes('is cleared. The hoard') && !l.startsWith('No one comes back')));
    for (const id of result.dead) {
      const h = town.agent(id);
      h.dead = true;
      depart(town, h);
      dead.push(h);
    }
    for (const c of champions) {
      if (result.dead.includes(c.id)) continue;
      c.hero!.renown += 10;
      c.hero!.kills += result.kills.get(c.id) ?? 0;
    }
    slain = result.outcome === 'cleared';
  } else {
    lines.push(`The walls hold, but no hero is in town to face ${e.leader}.`);
  }

  if (slain) {
    lines.push(`${e.leader} is dead. The warband breaks and scatters into the hills.`);
    en.stage = 'defeated';
    en.strength = 0;
    town.outcome = 'victory';
    town.mandate = Math.min(100, town.mandate + 25);
    chronicle(town, lines, 'cleared', champions, startDef);
    town.log(
      'War',
      Importance.Major,
      `The Last Stand is won! ${e.leader} falls before the walls of ${town.name}, and the warband is broken. (Chronicle ${town.chronicles.length})`,
      champions.map((a) => a.id),
    );
  } else {
    lines.push(`${e.leader} sounds the retreat. The warband limps back to the hills to lick its wounds.`);
    en.stage = 'raids';
    en.arrivalTick = null;
    en.strength = Math.max(town.content.enemy.stages.probing, en.strength * 0.6);
    en.nextRaidTick = town.tick + 6 * HOURS_PER_DAY;
    chronicle(town, lines, 'retreated', champions, startDef);
    town.log(
      'War',
      Importance.Major,
      `${town.name} survives the siege, but ${e.leader} escapes. He will be back. (Chronicle ${town.chronicles.length})`,
      champions.map((a) => a.id),
    );
  }
  town.tension += 60 + dead.length * 5;
}

function chronicle(town: Town, lines: string[], outcome: Chronicle['outcome'], champions: Agent[], _def: number): void {
  town.chronicles.push({
    id: town.chronicles.length + 1,
    partyName: `Defenders of ${town.name}`,
    siteName: `the walls of ${town.name}`,
    members: champions.map((a) => a.describe()),
    departDay: dayOf(town.tick),
    returnDay: dayOf(town.tick),
    outcome,
    lines,
  });
}

/** The player's lever: order a palisade or a watchtower, paid from the treasury. */
export function build(town: Town, what: 'palisade' | 'tower'): boolean {
  const e = town.content.enemy;
  const d = town.defenses;
  const spec = what === 'palisade' ? e.palisade : e.tower;
  if (what === 'palisade' && d.palisade !== 'none') return false;
  if (what === 'tower' && (d.palisade !== 'built' || d.towers + d.towersBuilding.length >= e.tower.max)) return false;
  const cost = spec.cost + town.market.quote('Timber', spec.timber);
  if (town.treasury < cost) return false;
  town.book('Defenses', -cost);
  town.market.buy('Timber', spec.timber);
  const ready = town.tick + spec.days * HOURS_PER_DAY;
  if (what === 'palisade') {
    d.palisade = 'building';
    d.palisadeReadyTick = ready;
    town.log('War', Importance.Notable, `Work begins on a palisade around ${town.name}. It will take ${spec.days} days.`);
  } else {
    d.towersBuilding.push(ready);
    town.log('War', Importance.Notable, `Carpenters start on a watchtower on the palisade.`);
  }
  return true;
}

export function buildCost(town: Town, what: 'palisade' | 'tower'): number {
  const spec = what === 'palisade' ? town.content.enemy.palisade : town.content.enemy.tower;
  return Math.round(spec.cost + town.market.quote('Timber', spec.timber));
}

function finishDefenses(town: Town): void {
  const d = town.defenses;
  if (d.palisade === 'building' && d.palisadeReadyTick !== null && town.tick >= d.palisadeReadyTick) {
    d.palisade = 'built';
    town.log('War', Importance.Notable, `The palisade is finished. ${town.name} sleeps a little easier.`);
  }
  const done = d.towersBuilding.filter((t) => town.tick >= t);
  if (done.length) {
    d.towers += done.length;
    d.towersBuilding = d.towersBuilding.filter((t) => town.tick < t);
    town.log('War', Importance.Routine, `A new watchtower stands on the palisade.`);
  }
}

/** The stand-in mayor fortifies once raids begin. */
function fortify(town: Town): void {
  const stage = STAGE_ORDER.indexOf(town.enemy.stage);
  if (stage < STAGE_ORDER.indexOf('probing')) return;
  if (town.defenses.palisade === 'none' && town.treasury > buildCost(town, 'palisade') + 150) build(town, 'palisade');
  else if (stage >= STAGE_ORDER.indexOf('raids') && town.treasury > buildCost(town, 'tower') + 200) build(town, 'tower');
}

function kill(town: Town, a: Agent): void {
  a.dead = true;
  depart(town, a);
  for (const other of town.residents) if (other.opinionOf(a.id) >= 40) other.loneliness = Math.min(100, other.loneliness + 30);
  const s = town.seats.find((x) => x.holderId === a.id);
  if (s) s.holderId = null;
}

function distance(town: Town, lotId: number): number {
  const l = town.lots[lotId];
  const c = 7.5;
  return Math.hypot(l.x - c, l.y - c);
}

const cap = (s: string): string => s.charAt(0).toUpperCase() + s.slice(1);

