// Headless runner: simulate a town and print its Ledger, so we can judge whether the city feels alive.
// Usage: npm run sim -- [--seed N] [--days N] [--all] [--stats] [--people N] [--chronicles]

import { Importance, Simulation, formatAbilities, formatChronicle, formatEntry } from './sim/index.ts';

let seed = 1;
let days = 60;
let all = false;
let stats = false;
let people = 3;
let chronicles = false;

const args = process.argv.slice(2);
for (let i = 0; i < args.length; i++) {
  switch (args[i]) {
    case '--seed':
      seed = Number(args[++i]);
      break;
    case '--days':
      days = Number(args[++i]);
      break;
    case '--all':
      all = true;
      break;
    case '--stats':
      stats = true;
      break;
    case '--chronicles':
      chronicles = true;
      break;
    case '--people':
      people = Number(args[++i]);
      break;
    default:
      console.error(`Unknown argument ${args[i]}`);
      process.exit(1);
  }
}

const sim = new Simulation({ seed });
const town = sim.town;
const minImportance = all ? Importance.Routine : Importance.Notable;
const out: string[] = [];
const print = (s = ''): void => {
  out.push(s);
};

for (const e of town.ledger.entries) if (e.importance >= minImportance) print(formatEntry(e));
town.ledger.onWrite((e) => {
  if (e.importance >= minImportance) print(formatEntry(e));
});

for (let d = 0; d < days; d++) {
  sim.runDays(1);
  const s = sim.history[sim.history.length - 1];
  if (stats || s.day % 10 === 0) {
    print(
      `  -- end of day ${s.day}: pop ${s.population}, homeless ${s.homeless}, idle ${s.unemployed}, ` +
        `buildings ${s.buildings} (+${s.sites} rising), treasury ${s.treasury.toFixed(0)}, food ${s.foodPrice.toFixed(2)}g, ` +
        `mood ${s.averageMood.toFixed(0)}, avg purse ${s.averageGold.toFixed(0)}g`,
    );
  }
}

print();
print(`=== ${town.name} after ${days} days (seed ${seed}) ===`);
const final = sim.snapshot();
print(`Population ${final.population} (${town.agents.filter((a) => a.departed).length} have left), treasury ${final.treasury.toFixed(0)} gold.`);
print(
  town.outcome === 'victory'
    ? 'The Last Stand is won: the warlord is dead.'
    : town.outcome === 'defeat'
      ? 'The town has fallen to the warband.'
      : `The warband stands at ${Math.round(town.enemy.strength)} (${town.enemy.stage}); ${town.enemy.raids} raids, ${town.enemy.sieges} sieges so far.`,
);
const counts = new Map<string, number>();
for (const b of town.buildings) if (b.complete) counts.set(b.def.name, (counts.get(b.def.name) ?? 0) + 1);
for (const [name, n] of [...counts].sort(([a], [b]) => (a < b ? -1 : 1))) print(`  ${name}: ${n}`);
const entries = town.ledger.entries;
print(
  `Ledger entries: ${entries.length} (${entries.filter((e) => e.importance === Importance.Major).length} major, ` +
    `${entries.filter((e) => e.importance === Importance.Notable).length} notable)`,
);

print();
print('=== People worth knowing ===');
const notableCount = (id: number): number => town.ledger.about(id).filter((e) => e.importance >= Importance.Notable && e.id > 1).length;
const notable = town.residents.sort((a, b) => notableCount(b.id) - notableCount(a.id) || a.id - b.id).slice(0, people);
for (const a of notable) {
  const home = a.homeId !== null ? town.building(a.homeId).name : 'nowhere (sleeps rough)';
  const job = a.jobId !== null ? town.building(a.jobId).name : 'no work';
  const spouse = a.spouseId !== null ? `, married to ${town.agent(a.spouseId).givenName}` : '';
  const friends = [...a.opinions].filter(([, v]) => v >= 40).map(([k]) => town.agent(k).givenName);
  const enemies = [...a.opinions].filter(([, v]) => v <= -30).map(([k]) => town.agent(k).givenName);
  print(`${a.describe()} (${a.traitList()})${spouse}`);
  print(`  ${formatAbilities(a.abilities)}; ${a.gold.toFixed(0)} gold; lives at ${home}; works at ${job}.`);
  if (friends.length) print(`  Friends: ${friends.join(', ')}`);
  if (enemies.length) print(`  Grudges: ${enemies.join(', ')}`);
  for (const e of town.ledger.about(a.id).filter((e) => e.id > 1)) print(`    ${formatEntry(e)}`);
  print();
}

if (chronicles) {
  print('=== Chronicles ===');
  for (const c of town.chronicles) {
    print(formatChronicle(c));
    print();
  }
}

process.stdout.write(out.join('\n') + '\n');
