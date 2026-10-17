import type { Agent } from './agent.ts';
import type { Bounty, Chronicle, Party, SiteState } from './systems/quests.ts';
import { type BuildingDef, type Content, GOODS, type Good, type Zone, isBusiness } from './content.ts';
import { type Importance, Ledger, type LedgerCategory, type LedgerEntry } from './ledger.ts';
import { Rng } from './rng.ts';
import { dayOf, hourOf } from './time.ts';

export class Lot {
  zone: Zone = 'None';
  /** Agent id, or null when the town (the mayor's charter) owns it. */
  ownerId: number | null = null;
  buildingId: number | null = null;

  readonly id: number;
  readonly x: number;
  readonly y: number;

  constructor(id: number, x: number, y: number) {
    this.id = id;
    this.x = x;
    this.y = y;
  }

  get isVacant(): boolean {
    return this.buildingId === null;
  }
}

export class Building {
  name = '';
  ownerId: number | null = null;
  buildProgress = 0;
  /** Set once the completion has been handled (workers released, owner moved in). */
  opened = false;
  laborBudget = 0;

  /** Business account: revenue in, wages and inputs out. */
  cash = 0;
  revenueToday = 0;
  daysInDebt = 0;
  forSale = false;
  closed = false;

  readonly stock = new Map<Good, number>();
  readonly workers: number[] = [];
  readonly residents: number[] = [];

  readonly id: number;
  readonly def: BuildingDef;
  readonly lotId: number;

  constructor(id: number, def: BuildingDef, lotId: number) {
    this.id = id;
    this.def = def;
    this.lotId = lotId;
  }

  get complete(): boolean {
    return this.buildProgress >= this.def.buildHours;
  }
  get isSite(): boolean {
    return !this.complete;
  }
  get openJobs(): number {
    return this.complete && !this.closed && !this.forSale ? Math.max(0, this.def.jobs - this.workers.length) : 0;
  }
  get openBeds(): number {
    return this.complete ? Math.max(0, this.def.housing - this.residents.length) : 0;
  }

  stockOf(g: Good): number {
    return this.stock.get(g) ?? 0;
  }
  addStock(g: Good, qty: number): void {
    this.stock.set(g, this.stockOf(g) + qty);
  }
}

/**
 * The town exchange. Producers sell into it and consumers buy from it; prices float with stock.
 * Caravans cover shortfalls at a steep import price and export surplus, so money flows in and
 * out of the frontier town rather than being conserved.
 */
export class Market {
  static readonly basePrice: Readonly<Record<Good, number>> = { Food: 1.0, Timber: 1.5, Tools: 4.0, Ale: 1.2 };

  private readonly stockBy = new Map<Good, number>();
  private readonly targetBy = new Map<Good, number>();
  importedToday = 0;

  constructor() {
    for (const g of GOODS) {
      this.stockBy.set(g, 0);
      this.targetBy.set(g, 20);
    }
  }

  stock(g: Good): number {
    return this.stockBy.get(g)!;
  }
  setTarget(g: Good, target: number): void {
    this.targetBy.set(g, Math.max(5, target));
  }
  seed(g: Good, qty: number): void {
    this.stockBy.set(g, this.stock(g) + qty);
  }

  price(g: Good): number {
    const ratio = Math.sqrt(this.targetBy.get(g)! / (this.stock(g) + 1));
    return Market.basePrice[g] * Math.max(0.4, Math.min(3.0, ratio));
  }

  /** Cost of buying qty now, importing the shortfall by caravan at a premium. */
  quote(g: Good, qty: number): number {
    const fromStock = Math.min(qty, this.stock(g));
    return fromStock * this.price(g) + (qty - fromStock) * Market.basePrice[g] * 3.0;
  }

  buy(g: Good, qty: number): number {
    const cost = this.quote(g, qty);
    const fromStock = Math.min(qty, this.stock(g));
    this.stockBy.set(g, this.stock(g) - fromStock);
    this.importedToday += qty - fromStock;
    return cost;
  }

  sell(g: Good, qty: number): number {
    const revenue = qty * this.price(g) * 0.9;
    this.stockBy.set(g, this.stock(g) + qty);
    return revenue;
  }

  /** Daily caravan: surplus above target is exported, so prices hover near base when supply is healthy. */
  dailyCaravan(): void {
    for (const g of GOODS) {
      const cap = this.targetBy.get(g)! * 1.2;
      if (this.stock(g) > cap) this.stockBy.set(g, cap);
    }
    this.importedToday = 0;
  }
}

export type TreasuryAccount = 'Sales tax' | 'Ale tax' | 'Rent' | 'Land sales' | 'Town businesses' | 'Bailouts' | 'Charter houses' | 'Bounties' | 'Upkeep';

export class Town {
  static readonly GRID = 16;

  readonly name = 'Briarford';
  readonly rng: Rng;
  readonly ledger = new Ledger();
  readonly market = new Market();
  tick = 0;
  treasury = 0;
  taxRate: number;
  /** The town's books: gold in (positive) and out (negative) by account, all time and today. */
  readonly accounts: Record<string, number> = {};
  accountsToday: Record<string, number> = {};
  /** The last full day's books, for the player to read. */
  accountsYesterday: Record<string, number> = {};
  /** Consecutive days the treasury could not cover upkeep. */
  unpaidDays = 0;

  readonly lots: Lot[] = [];
  readonly buildings: Building[] = [];
  readonly agents: Agent[] = [];
  readonly sites: SiteState[] = [];
  readonly bounties: Bounty[] = [];
  readonly parties: Party[] = [];
  readonly chronicles: Chronicle[] = [];
  private nextAgentId = 1;

  readonly content: Content;

  constructor(content: Content, seed: number) {
    this.content = content;
    this.rng = new Rng(seed);
    this.taxRate = content.rules.taxRate;
    for (let y = 0; y < Town.GRID; y++) for (let x = 0; x < Town.GRID; x++) this.lots.push(new Lot(this.lots.length, x, y));
  }

  /** Move gold into (positive) or out of (negative) the treasury, booked under an account. */
  book(account: TreasuryAccount, amount: number): void {
    this.treasury += amount;
    this.accounts[account] = (this.accounts[account] ?? 0) + amount;
    this.accountsToday[account] = (this.accountsToday[account] ?? 0) + amount;
  }

  get day(): number {
    return dayOf(this.tick);
  }
  get hour(): number {
    return hourOf(this.tick);
  }

  get residents(): Agent[] {
    return this.agents.filter((a) => !a.departed);
  }
  /** Townsfolk who work and rent (heroes live by bounties instead). */
  get workers(): Agent[] {
    return this.agents.filter((a) => !a.departed && a.hero === null);
  }
  get heroes(): Agent[] {
    return this.agents.filter((a) => !a.departed && a.hero !== null);
  }

  get population(): number {
    let n = 0;
    for (const a of this.agents) if (!a.departed) n++;
    return n;
  }

  agent(id: number): Agent {
    return this.agents[id - 1];
  }
  building(id: number): Building {
    return this.buildings[id];
  }
  lot(id: number): Lot {
    return this.lots[id];
  }
  lotAt(x: number, y: number): Lot {
    return this.lots[y * Town.GRID + x];
  }

  newAgentId(): number {
    return this.nextAgentId++;
  }

  static distanceFromCenter(l: Lot): number {
    const c = (Town.GRID - 1) / 2;
    return Math.sqrt((l.x - c) * (l.x - c) + (l.y - c) * (l.y - c));
  }

  lotPrice(lot: Lot): number {
    return 15 + this.population * 0.4 + Math.max(0, 8 - Town.distanceFromCenter(lot)) * 2;
  }

  addBuilding(def: BuildingDef, lot: Lot, ownerId: number | null, name: string, complete: boolean): Building {
    const b = new Building(this.buildings.length, def, lot.id);
    b.ownerId = ownerId;
    b.name = name;
    b.buildProgress = complete ? def.buildHours : 0;
    b.opened = complete;
    this.buildings.push(b);
    lot.buildingId = b.id;
    lot.ownerId = ownerId;
    return b;
  }

  get taverns(): Building[] {
    return this.buildings.filter((b) => b.complete && b.def.venue && !b.closed && !b.forSale);
  }

  businessCount(): number {
    return this.buildings.filter((b) => isBusiness(b.def)).length;
  }

  ownerName(ownerId: number | null): string {
    return ownerId === null ? 'the town' : this.agent(ownerId).name;
  }

  log(cat: LedgerCategory, imp: Importance, text: string, agents: number[] = []): LedgerEntry {
    return this.ledger.write(this.tick, cat, imp, text, agents);
  }
}

/** Stable ordering helper: sort by key(s) ascending, ties broken by id. */
export function byId<T extends { id: number }>(a: T, b: T): number {
  return a.id - b.id;
}
