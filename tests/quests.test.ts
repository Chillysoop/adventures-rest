import { describe, expect, it } from 'vitest';
import { Rng, Simulation } from '../src/sim/index.ts';
import { runExpedition } from '../src/sim/systems/expedition.ts';
import { createHero } from '../src/sim/systems/quests.ts';

describe('quests', () => {
  it('holds bounty rewards in escrow and refunds them on expiry', () => {
    const sim = new Simulation({ seed: 4, autoMayor: false });
    const town = sim.town;
    const before = town.treasury;
    const bounty = sim.postBounty('wolfwood', 100)!;
    expect(bounty).not.toBeNull();
    expect(town.treasury).toBe(before - 100);
    expect(sim.postBounty('wolfwood', 50)).toBeNull();
    sim.runDays(11);
    expect(bounty.status).not.toBe('open');
    if (bounty.status === 'expired') {
      // The refund came back; everything else the treasury did is ordinary income.
      const refunds = town.ledger.entries.filter((e) => e.text.includes('withdraws it')).length;
      expect(refunds).toBe(1);
    }
  });

  it('refuses bounties the town cannot pay for', () => {
    const sim = new Simulation({ seed: 4, autoMayor: false });
    expect(sim.postBounty('drowned_temple', sim.town.treasury + 1)).toBeNull();
  });

  it('resolves the same expedition identically from the same seed', () => {
    const run = () => {
      const sim = new Simulation({ seed: 9, autoMayor: false });
      const t = sim.town;
      const party = t.content.classes.slice(0, 4).map((c) => createHero(t, c, 2));
      return runExpedition(new Rng(77), t.content, t.content.sites[0], party);
    };
    const a = run();
    const b = run();
    expect(a.lines).toEqual(b.lines);
    expect(a.outcome).toBe(b.outcome);
  });

  it('sends parties out and writes Chronicles over a campaign', () => {
    const sim = new Simulation({ seed: 1 });
    sim.runDays(120);
    const town = sim.town;
    expect(town.heroes.length + town.agents.filter((a) => a.hero && a.departed).length).toBeGreaterThan(0);
    expect(town.chronicles.length).toBeGreaterThan(0);
    for (const c of town.chronicles) expect(c.lines.length).toBeGreaterThan(1);
    expect(town.ledger.entries.some((e) => e.category === 'Quest')).toBe(true);
  });

  it.each([1, 2, 3])('keeps heroes out of the job and housing markets, and the dead out of town (seed %i)', (seed) => {
    const sim = new Simulation({ seed });
    sim.runDays(100);
    for (const a of sim.town.agents) {
      if (a.hero) {
        expect(a.jobId).toBeNull();
        expect(a.homeId).toBeNull();
      }
      if (a.dead) expect(a.departed).toBe(true);
    }
    for (const b of sim.town.bounties) {
      if (b.status === 'taken') expect(sim.town.parties.some((p) => p.bountyId === b.id && !p.returned)).toBe(true);
    }
  });
});
