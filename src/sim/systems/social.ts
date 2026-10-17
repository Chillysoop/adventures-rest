import type { Agent } from '../agent.ts';
import { Importance } from '../ledger.ts';
import { modifier } from '../rng.ts';
import { isEvening } from '../time.ts';
import type { Building, Town } from '../town.ts';
import { moveIn } from './economy.ts';
import { watchStrength } from './council.ts';

/**
 * Interactions outside the mayor's view: tavern talk, friendships, grudges, brawls, courtship
 * and marriage. Every contested moment is a visible d20 roll so the Ledger reads like a table.
 */

export const FRIEND_THRESHOLD = 40;
export const RIVAL_THRESHOLD = -30;
export const COURT_THRESHOLD = 60;
export const MARRY_THRESHOLD = 85;

const TOPICS = [
  'the price of timber',
  'dwarven beards',
  'elven manners',
  "the mayor's taxes",
  'their cooking',
  'who really killed the wolf last winter',
  'the smell of the tannery',
  'a game of dice',
  'their mother',
  'the proper way to pour ale',
  'orcs',
  'a debt from three weeks ago',
];

export function hourly(town: Town): void {
  if (!isEvening(town.hour)) return;
  for (const tavern of town.taverns) {
    const patrons = town.agents.filter((a) => !a.departed && a.activity === 'Drinking' && a.locationId === tavern.id);
    if (patrons.length < 2) continue;

    for (const p of patrons) {
      if (!town.rng.chance(0.5)) continue;
      const courting = p.courtingId;
      const partner =
        courting !== null && patrons.some((x) => x.id === courting) && town.rng.chance(0.6)
          ? town.agent(courting)
          : town.rng.pick(patrons.filter((x) => x.id !== p.id));
      interact(town, tavern, p, partner);
    }
  }
}

function interact(town: Town, tavern: Building, p: Agent, q: Agent): void {
  const rng = town.rng;
  const before = q.opinionOf(p.id);
  const beforeMutual = p.opinionOf(q.id);

  const roll = rng.d20();
  const total =
    roll + modifier(p.abilities.cha) + (p.has('Gregarious') ? 2 : 0) - (p.has('Shy') ? 2 : 0) - (p.drinks >= 3 ? 2 : 0) + Math.trunc(before / 25);

  const delta = roll === 20 ? 15 : total >= 15 ? 8 : total >= 9 ? 3 : total >= 5 ? -3 : q.has('HotTempered') ? -16 : -10;
  const after = q.changeOpinion(p.id, delta);
  const mutual = p.changeOpinion(q.id, delta > 0 ? delta : Math.trunc(delta / 2));

  // Friendship.
  if (after >= FRIEND_THRESHOLD && mutual >= FRIEND_THRESHOLD && (before < FRIEND_THRESHOLD || beforeMutual < FRIEND_THRESHOLD))
    town.log('Social', Importance.Routine, `${p.name} and ${q.name} have become fast friends over ale at ${tavern.name}.`, [p.id, q.id]);

  // A grudge begins.
  if (after <= RIVAL_THRESHOLD && before > RIVAL_THRESHOLD)
    town.log(
      'Social',
      Importance.Notable,
      `${p.name} says something about ${rng.pick(TOPICS)} that ${q.name} will not forgive. A grudge begins at ${tavern.name}.`,
      [p.id, q.id],
    );

  // Brawl.
  if (after <= RIVAL_THRESHOLD && (q.has('HotTempered') || q.drinks >= 3) && rng.chance(0.35)) {
    if (town.watch > 0 && rng.chance(watchStrength(town)))
      town.log('Brawl', Importance.Routine, `The watch steps in at ${tavern.name} before ${q.name} can swing at ${p.name}.`, [q.id, p.id]);
    else brawl(town, tavern, q, p);
    return;
  }

  // Courtship and marriage.
  if (p.spouseId === null && q.spouseId === null) {
    if (p.courtingId === null && q.courtingId === null && after >= COURT_THRESHOLD && mutual >= COURT_THRESHOLD) {
      const chance = 0.08 * (p.has('Romantic') ? 2 : 1) * (q.has('Romantic') ? 2 : 1);
      if (rng.chance(chance)) {
        p.courtingId = q.id;
        q.courtingId = p.id;
        town.log('Romance', Importance.Notable, `Tongues wag at ${tavern.name}: ${p.name} and ${q.name} have been seen walking out together.`, [
          p.id,
          q.id,
        ]);
      }
    } else if (p.courtingId === q.id && after >= MARRY_THRESHOLD && mutual >= MARRY_THRESHOLD && rng.chance(0.25)) {
      marry(town, p, q);
    }
  }
}

const sign = (m: number): string => (m >= 0 ? `+${m}` : `${m}`);

function brawl(town: Town, tavern: Building, attacker: Agent, target: Agent): void {
  const rng = town.rng;
  const ar = rng.d20();
  const tr = rng.d20();
  const am = modifier(attacker.abilities.str);
  const tm = modifier(target.abilities.str);
  const attackerWins = ar + am >= tr + tm;
  const winner = attackerWins ? attacker : target;
  const loser = attackerWins ? target : attacker;
  const [wr, wm, lr, lm] = attackerWins ? [ar, am, tr, tm] : [tr, tm, ar, am];

  const damages = rng.range(3, 11);
  const paid = Math.min(loser.gold, damages);
  loser.gold -= paid;
  tavern.cash += paid;
  loser.fatigue = 100;
  loser.activity = 'Sleeping';
  loser.locationId = loser.homeId;
  attacker.changeOpinion(target.id, -20);
  target.changeOpinion(attacker.id, -20);

  const crit = wr === 20 ? ' A natural 20!' : lr === 1 ? ' A natural 1.' : '';
  town.log(
    'Brawl',
    Importance.Notable,
    `A brawl breaks out at ${tavern.name}: ${attacker.name} swings at ${target.name}. ` +
      `${winner.givenName} (d20 ${wr}${sign(wm)}) lays ${loser.givenName} (d20 ${lr}${sign(lm)}) out cold.${crit} ` +
      `${loser.givenName} pays ${paid.toFixed(0)} gold for the broken chairs.`,
    [attacker.id, target.id],
  );
}

function marry(town: Town, p: Agent, q: Agent): void {
  p.spouseId = q.id;
  q.spouseId = p.id;
  p.courtingId = null;
  q.courtingId = null;

  // The couple settles in whichever home one of them owns, else wherever has room.
  let home = ownedHome(town, p) ?? ownedHome(town, q) ?? null;
  const oldName = q.familyName;
  if (!home && p.homeId !== null && town.building(p.homeId).openBeds > 0) home = town.building(p.homeId);
  if (!home && q.homeId !== null && town.building(q.homeId).openBeds > 0) home = town.building(q.homeId);
  if (home) {
    if (p.homeId !== home.id) moveIn(town, p, home);
    if (q.homeId !== home.id) moveIn(town, q, home);
  }
  q.familyName = p.familyName;

  town.log(
    'Romance',
    Importance.Major,
    `${p.givenName} ${p.familyName} and ${q.givenName} (once ${oldName}) are wed at the town hall. Half the town turns out to cheer.`,
    [p.id, q.id],
  );
}

function ownedHome(town: Town, a: Agent): Building | undefined {
  return town.buildings.find(
    (b) => b.complete && b.def.housing > 0 && b.ownerId === a.id && b.openBeds + (b.residents.includes(a.id) ? 1 : 0) >= 1,
  );
}

/** Daily acts of charity from generous townsfolk. */
export function daily(town: Town): void {
  for (const giver of town.residents.filter((a) => a.has('Generous') && a.gold > 40)) {
    const needy = town.residents
      .filter((a) => a.id !== giver.id && a.hunger > 60 && a.gold < 2)
      .sort((x, y) => giver.opinionOf(y.id) - giver.opinionOf(x.id) || x.id - y.id)[0];
    if (!needy) continue;
    giver.gold -= 6;
    needy.gold += 6;
    needy.changeOpinion(giver.id, 25);
    giver.changeOpinion(needy.id, 5);
    town.log('Social', Importance.Routine, `${giver.name} quietly presses a few coins into the hand of ${needy.name}, who has not eaten in a day.`, [
      giver.id,
      needy.id,
    ]);
  }
}
