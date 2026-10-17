import { describe, expect, it } from 'vitest';
import { Market, Rng, Simulation, formatEntry, modifier } from '../src/sim/index.ts';

const run = (seed: number, days: number): Simulation => {
  const sim = new Simulation({ seed });
  sim.runDays(days);
  return sim;
};

describe('simulation', () => {
  it('replays identically from the same seed', () => {
    const a = run(42, 40).town.ledger.entries.map(formatEntry);
    const b = run(42, 40).town.ledger.entries.map(formatEntry);
    expect(a).toEqual(b);
  });

  it('tells different stories from different seeds', () => {
    const a = run(1, 20).town.ledger.entries.map((e) => e.text);
    const b = run(2, 20).town.ledger.entries.map((e) => e.text);
    expect(a).not.toEqual(b);
  });

  it.each([1, 7])('grows and builds (seed %i)', (seed) => {
    const sim = run(seed, 60);
    const town = sim.town;
    expect(town.population).toBeGreaterThan(sim.options.settlers);
    expect(town.buildings.filter((b) => b.complete).length).toBeGreaterThan(5);
    expect(town.buildings.some((b) => b.def.venue && b.complete)).toBe(true);
    expect(town.lots.some((l) => l.ownerId !== null)).toBe(true);
  });

  it('keeps townsfolk fed and mostly housed', () => {
    const res = run(3, 60).town.residents;
    expect(res.reduce((s, a) => s + a.hunger, 0) / res.length).toBeLessThan(60);
    expect(res.filter((a) => a.homeId === null).length).toBeLessThanOrEqual(Math.floor(res.length / 4) + 1);
  });

  it('writes social stories into the Ledger', () => {
    const cats = new Set(run(1, 120).town.ledger.entries.map((e) => e.category));
    for (const c of ['Social', 'Property', 'Arrival'] as const) expect(cats.has(c)).toBe(true);
    expect(cats.has('Brawl') || cats.has('Romance')).toBe(true);
  });

  it.each([1, 2, 5])('keeps homes, jobs and ownership consistent (seed %i)', (seed) => {
    const town = run(seed, 90).town;
    for (const a of town.agents) {
      if (a.departed) {
        expect(a.jobId).toBeNull();
        expect(a.homeId).toBeNull();
        expect(town.buildings.some((b) => b.ownerId === a.id)).toBe(false);
        continue;
      }
      if (a.jobId !== null) expect(town.building(a.jobId).workers).toContain(a.id);
      if (a.homeId !== null) expect(town.building(a.homeId).residents).toContain(a.id);
      if (a.spouseId !== null) expect(town.agent(a.spouseId).spouseId).toBe(a.id);
    }
    for (const b of town.buildings) {
      expect(b.residents.length).toBeLessThanOrEqual(b.def.housing);
      for (const w of b.workers) expect(town.agent(w).jobId).toBe(b.id);
      expect(town.lot(b.lotId).buildingId).toBe(b.id);
    }
  });

  it('runs a 120-day town quickly', () => {
    const start = performance.now();
    run(1, 120);
    expect(performance.now() - start).toBeLessThan(5000);
  });
});

describe('dice and market', () => {
  it('keeps dice in range', () => {
    const rng = new Rng(9);
    for (let i = 0; i < 10_000; i++) {
      const d = rng.d20();
      expect(d >= 1 && d <= 20).toBe(true);
      const s = rng.abilityScore();
      expect(s >= 3 && s <= 18).toBe(true);
    }
  });

  it('rolls a roughly uniform d20', () => {
    const rng = new Rng(11);
    const counts = new Array<number>(21).fill(0);
    for (let i = 0; i < 40_000; i++) counts[rng.d20()]++;
    for (let face = 1; face <= 20; face++) {
      expect(counts[face]).toBeGreaterThan(1700);
      expect(counts[face]).toBeLessThan(2300);
    }
  });

  it('follows the modifier table', () => {
    expect([8, 10, 11, 16, 3].map(modifier)).toEqual([-1, 0, 0, 3, -4]);
  });

  it('raises prices on scarcity and imports shortfalls dearly', () => {
    const m = new Market();
    m.setTarget('Food', 100);
    m.seed('Food', 100);
    const healthy = m.price('Food');
    m.buy('Food', 90);
    expect(m.price('Food')).toBeGreaterThan(healthy);
    expect(m.quote('Food', 20)).toBeGreaterThan(20 * Market.basePrice.Food * 2);
  });
});
