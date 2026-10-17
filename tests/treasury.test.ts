import { describe, expect, it } from 'vitest';
import { Simulation } from '../src/sim/index.ts';

describe('treasury', () => {
  it('books every gold piece in or out of the treasury under an account', () => {
    for (const seed of [1, 2, 3]) {
      const sim = new Simulation({ seed, startingTreasury: 250 });
      sim.runDays(60);
      const t = sim.town;
      const booked = Object.values(t.accounts).reduce((s, v) => s + v, 0);
      expect(t.treasury).toBeCloseTo(250 + booked, 6);
      expect(t.accounts['Upkeep']).toBeLessThan(0);
    }
  });

  it('never lets upkeep drive the treasury below zero', () => {
    const sim = new Simulation({ seed: 5, startingTreasury: 0, autoBounties: false });
    for (let d = 0; d < 40; d++) {
      sim.runDays(1);
      expect(sim.town.treasury).toBeGreaterThanOrEqual(0);
    }
  });
});
