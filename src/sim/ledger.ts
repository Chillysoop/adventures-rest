import { dayOf, hourOf } from './time.ts';

export const Importance = { Routine: 1, Notable: 2, Major: 3 } as const;
export type Importance = (typeof Importance)[keyof typeof Importance];

export type LedgerCategory =
  | 'Arrival'
  | 'Departure'
  | 'Property'
  | 'Construction'
  | 'Business'
  | 'Social'
  | 'Brawl'
  | 'Romance'
  | 'Hardship'
  | 'Quest'
  | 'Town';

export interface LedgerEntry {
  id: number;
  tick: number;
  category: LedgerCategory;
  importance: Importance;
  text: string;
  agents: readonly number[];
}

export function formatEntry(e: LedgerEntry): string {
  const day = String(dayOf(e.tick)).padStart(3);
  const hour = String(hourOf(e.tick)).padStart(2, '0');
  return `Day ${day} ${hour}:00  [${e.category}] ${e.text}`;
}

/**
 * The town chronicle: everything notable that happens, in order. This is what the player reads
 * to follow the living city, and what we read to judge whether the sim produces stories.
 */
export class Ledger {
  readonly entries: LedgerEntry[] = [];
  private listeners: ((e: LedgerEntry) => void)[] = [];

  onWrite(fn: (e: LedgerEntry) => void): void {
    this.listeners.push(fn);
  }

  write(tick: number, category: LedgerCategory, importance: Importance, text: string, agents: number[] = []): LedgerEntry {
    const entry: LedgerEntry = { id: this.entries.length + 1, tick, category, importance, text, agents };
    this.entries.push(entry);
    for (const fn of this.listeners) fn(entry);
    return entry;
  }

  about(agentId: number): LedgerEntry[] {
    return this.entries.filter((e) => e.agents.includes(agentId));
  }
}
