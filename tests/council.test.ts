import { describe, expect, it } from 'vitest';
import { Simulation, pendingMotion } from '../src/sim/index.ts';

function untilMotion(sim: Simulation, maxDays = 120) {
  for (let d = 0; d < maxDays; d++) {
    const m = pendingMotion(sim.town);
    if (m) return m;
    sim.runDays(1);
  }
  throw new Error('no motion was proposed');
}

describe('council', () => {
  it('seats a Steward early and opens more seats as the town grows', () => {
    const sim = new Simulation({ seed: 2 });
    sim.runDays(5);
    const steward = sim.town.seats.find((s) => s.def.id === 'steward')!;
    expect(steward.holderId).not.toBeNull();
    sim.runDays(115);
    for (const s of sim.town.seats) if (sim.town.population >= s.def.unlockPopulation + 10) expect(s.holderId).not.toBeNull();
    const ids = sim.town.seats.map((s) => s.holderId).filter((x) => x !== null);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('lets the mayor sign or veto, and charges Mandate for overruling the majority', () => {
    for (const seed of [1, 2, 3]) {
      const sim = new Simulation({ seed, autoCouncil: false });
      const m = untilMotion(sim);
      const yes = m.votes.filter((v) => v.yes).length;
      const majority = yes * 2 === m.votes.length ? null : yes * 2 > m.votes.length;
      const before = sim.town.mandate;
      const sign = majority === null ? true : !majority;
      expect(sim.decideMotion(m.id, sign)).toBe(true);
      expect(m.decidedBy).toBe('mayor');
      expect(m.decree).toBe(majority !== null);
      if (m.decree) expect(sim.town.mandate).toBeLessThan(before);
      expect(sim.decideMotion(m.id, true)).toBe(false);
    }
  });

  it('lets the council vote stand when the mayor says nothing', () => {
    const sim = new Simulation({ seed: 3, autoCouncil: false });
    const m = untilMotion(sim);
    sim.runDays(sim.town.content.council.motionDays + 1);
    expect(m.status).not.toBe('pending');
    expect(m.decidedBy).toBe('council');
  });

  it('replays identically from the same seed', () => {
    const run = () => {
      const sim = new Simulation({ seed: 7 });
      sim.runDays(90);
      return sim.town.motions.map((m) => `${m.kind}:${m.status}`).join(',') + `|${sim.town.mandate.toFixed(3)}`;
    };
    expect(run()).toBe(run());
  });
});
