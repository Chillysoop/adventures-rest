import { Importance } from '../ledger.ts';
import { HOURS_PER_DAY } from '../time.ts';
import type { Town } from '../town.ts';
import { postBounty, createHero, suggestedReward } from './quests.ts';
import { isActive } from './threat.ts';

/**
 * The Director, a pacing hand modelled on a good Dungeon Master. It never decides a roll. It reads
 * recent drama (tension) and sets the tempo: after a hard blow it slows the warband and sends
 * help; after a long quiet stretch it lets the warband gather faster and sends a stranger with a
 * commission. It also makes sure the campaign reaches its climax in a reasonable time.
 */

/** The Director's multiplier on the warband's growth today. */
export function daily(town: Town): number {
  town.tension *= 0.9;
  for (const e of town.ledger.entries.slice(-80)) {
    if (town.tick - e.tick >= HOURS_PER_DAY) continue;
    if (e.category === 'Brawl') town.tension += 2;
    if (e.category === 'Quest' && e.importance === 3) town.tension += 4;
  }
  if (!isActive(town)) return 0;

  town.quietDays = town.tension < 10 ? town.quietDays + 1 : 0;
  let pace = 1;
  if (town.tension > 40) pace = 0.5;
  else if (town.quietDays > 10) pace = 1.5;
  // Every campaign reaches its climax: past day 120 the warband gathers in earnest.
  if (town.day > 120) pace = Math.max(pace, 1 + (town.day - 120) / 30);

  if (town.tension > 45 && town.rng.chance(0.25)) sendHelp(town);
  else if (town.quietDays > 12 && town.rng.chance(0.2)) sendStranger(town);
  return pace;
}

function sendHelp(town: Town): void {
  if (town.taverns.length === 0) return;
  const cls = town.content.classes[town.rng.range(0, town.content.classes.length)];
  const a = createHero(town, cls, town.rng.chance(0.5) ? 3 : 2);
  town.tension -= 15;
  town.log(
    'Arrival',
    Importance.Notable,
    `Word of ${town.name}'s troubles has travelled. ${a.describe()} rides in, saying they owe the old mayor a debt.`,
    [a.id],
  );
}

/** A hooded stranger pays for a bounty on the warband's camp, or on whatever threat is nearest. */
function sendStranger(town: Town): void {
  const camp = town.sites.find((s) => s.def.id === town.content.enemy.outpost.id && !s.cleared);
  const target = camp ?? town.sites.filter((s) => !s.cleared).sort((x, y) => x.def.tier - y.def.tier || (x.def.id < y.def.id ? -1 : 1))[0];
  if (!target) return;
  if (town.bounties.some((b) => b.siteId === target.def.id && (b.status === 'open' || b.status === 'taken'))) return;
  const reward = suggestedReward(target.def.tier) + 40;
  town.book('Patron', reward);
  if (!postBounty(town, target.def.id, reward)) {
    town.book('Patron', -reward);
    return;
  }
  town.quietDays = 0;
  town.log('Town', Importance.Major, `A hooded stranger leaves a purse of ${reward} gold at the town hall, for whoever clears ${target.def.name}. No name is given.`);
}
