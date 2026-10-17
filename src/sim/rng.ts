/**
 * Deterministic random source (xoshiro128**). Every random decision in the simulation goes
 * through one instance so a seed replays a campaign exactly, in Node and in the browser.
 */
export class Rng {
  private s0: number;
  private s1: number;
  private s2: number;
  private s3: number;

  constructor(seed: number) {
    // SplitMix32 to spread the seed over the state.
    let x = seed >>> 0;
    const next = (): number => {
      x = (x + 0x9e3779b9) >>> 0;
      let z = x;
      z = Math.imul(z ^ (z >>> 16), 0x85ebca6b) >>> 0;
      z = Math.imul(z ^ (z >>> 13), 0xc2b2ae35) >>> 0;
      return (z ^ (z >>> 16)) >>> 0;
    };
    this.s0 = next();
    this.s1 = next();
    this.s2 = next();
    this.s3 = next();
    if ((this.s0 | this.s1 | this.s2 | this.s3) === 0) this.s0 = 1;
  }

  nextUint(): number {
    const result = Math.imul(rotl(Math.imul(this.s1, 5) >>> 0, 7), 9) >>> 0;
    const t = (this.s1 << 9) >>> 0;
    this.s2 = (this.s2 ^ this.s0) >>> 0;
    this.s3 = (this.s3 ^ this.s1) >>> 0;
    this.s1 = (this.s1 ^ this.s2) >>> 0;
    this.s0 = (this.s0 ^ this.s3) >>> 0;
    this.s2 = (this.s2 ^ t) >>> 0;
    this.s3 = rotl(this.s3, 11);
    return result;
  }

  /** Integer in [min, max). */
  range(min: number, max: number): number {
    if (max <= min) return min;
    return min + (this.nextUint() % (max - min));
  }

  /** Float in [0, 1). */
  next(): number {
    return (this.nextUint() >>> 8) / 16777216;
  }

  chance(p: number): boolean {
    return this.next() < p;
  }

  pick<T>(items: readonly T[]): T {
    return items[this.range(0, items.length)];
  }

  /** Roll NdS, e.g. roll(3, 6) for 3d6. */
  roll(count: number, sides: number): number {
    let total = 0;
    for (let i = 0; i < count; i++) total += this.range(1, sides + 1);
    return total;
  }

  d20(): number {
    return this.range(1, 21);
  }

  /** Classic 4d6-drop-lowest ability score. */
  abilityScore(): number {
    const r = [this.range(1, 7), this.range(1, 7), this.range(1, 7), this.range(1, 7)];
    return r[0] + r[1] + r[2] + r[3] - Math.min(...r);
  }
}

function rotl(x: number, k: number): number {
  return ((x << k) | (x >>> (32 - k))) >>> 0;
}

export function modifier(score: number): number {
  return Math.floor((score - 10) / 2);
}
