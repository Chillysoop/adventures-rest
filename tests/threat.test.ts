import { describe, expect, it } from 'vitest';
import { Simulation, defense } from '../src/sim/index.ts';

function runUntilOutcome(sim: Simulation, maxDays: number): void {
  for (let d = 0; d < maxDays && !sim.town.outcome; d++) sim.runDays(1);
}

describe('the warband and the Last Stand', () => {
  it('escalates through foreshadowed stages and raises a war camp heroes can raid', () => {
    const sim = new Simulation({ seed: 3 });
    const seen = new Set<string>();
    for (let d = 0; d < 90; d++) {
      sim.runDays(1);
      seen.add(sim.town.enemy.stage);
    }
    expect(seen.has('omens')).toBe(true);
    expect(seen.has('probing')).toBe(true);
    expect(sim.town.sites.some((s) => s.def.id === 'bloodtusk_camp')).toBe(true);
    const war = sim.town.ledger.entries.filter((e) => e.category === 'War');
    expect(war.length).toBeGreaterThan(3);
  });

  it('announces the march days before the siege', () => {
    const sim = new Simulation({ seed: 5, autoBounties: false });
    runUntilOutcome(sim, 250);
    const march = sim.town.ledger.entries.find((e) => e.text.includes('is on the march'));
    const siege = sim.town.chronicles.find((c) => c.partyName.startsWith('Defenders'));
    expect(march).toBeDefined();
    expect(siege).toBeDefined();
    expect(siege!.returnDay * 24 - march!.tick).toBeGreaterThanOrEqual(2 * 24);
  });

  it('punishes a town that never fortifies and never fights back', () => {
    let fallen = 0;
    for (const seed of [1, 3, 4]) {
      const sim = new Simulation({ seed, autoBounties: false });
      sim.town.defenses.palisade = 'building';
      sim.town.defenses.palisadeReadyTick = Number.MAX_SAFE_INTEGER;
      runUntilOutcome(sim, 250);
      if (sim.town.outcome === 'defeat') fallen++;
    }
    expect(fallen).toBeGreaterThanOrEqual(2);
  });

  it('can be won by a town that fortifies and sends heroes out', () => {
    let won = 0;
    for (const seed of [1, 2, 3]) {
      const sim = new Simulation({ seed });
      runUntilOutcome(sim, 260);
      if (sim.town.outcome === 'victory') won++;
      expect(sim.town.enemy.strength).toBeGreaterThanOrEqual(0);
    }
    expect(won).toBeGreaterThanOrEqual(2);
  });

  it('only builds a palisade the treasury can pay for', () => {
    const sim = new Simulation({ seed: 2, startingTreasury: 0, enemy: false });
    expect(sim.buildDefense('palisade')).toBe(false);
    sim.town.treasury = 2000;
    expect(sim.buildDefense('palisade')).toBe(true);
    expect(sim.buildDefense('tower')).toBe(false);
    const before = defense(sim.town);
    sim.runDays(7);
    expect(sim.town.defenses.palisade).toBe('built');
    expect(defense(sim.town)).toBeGreaterThan(before);
  });
});
