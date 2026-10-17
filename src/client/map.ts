// Canvas renderer for the town map: lots, buildings, townsfolk, the wilderness ring and its sites.
// Pure drawing; it reads the simulation and never changes it.

import type { Agent, Building, Simulation, Town } from '../sim/index.ts';
import { HOURS_PER_DAY } from '../sim/index.ts';

/** Wilderness tiles around the 16x16 town grid, where the adventure sites sit. */
export const MARGIN = 6;
const GRID = 16;
export const WORLD = GRID + MARGIN * 2;

export interface Selection {
  kind: 'agent' | 'building' | 'site';
  id: number | string;
}

export interface SitePin {
  id: string;
  x: number;
  y: number;
}

interface Sprite {
  x: number;
  y: number;
  visible: boolean;
}

/** Small deterministic hash so decoration never touches the simulation's Rng. */
function hash(n: number): number {
  let x = (n | 0) * 0x45d9f3b;
  x = ((x >>> 16) ^ x) * 0x45d9f3b;
  x = (x >>> 16) ^ x;
  return (x >>> 0) / 4294967296;
}

const PALETTE = {
  grass: '#8fae6b',
  grassDark: '#7b9a5a',
  forest: '#4f7543',
  forestDark: '#3c5c34',
  river: '#6d9fbf',
  riverLight: '#9cc3da',
  dirt: '#c8ab7e',
  dirtDark: '#b0915f',
  plaza: '#d8cbb0',
  residential: '#a8bf84',
  commercial: '#c9b98f',
  farmland: '#b9a76a',
  furrow: '#9d8a4f',
  wheat: '#d9bf62',
  wall: '#efe2c6',
  wallShade: '#d4c3a0',
  roofHouse: '#b5583f',
  roofTavern: '#8e3b2e',
  roofHall: '#4f5f8a',
  roofForge: '#5b5550',
  roofBrewery: '#7a5a34',
  stone: '#9a9389',
  timber: '#8a6239',
  ink: '#2c2620',
  gold: '#e8b93a',
  window: '#ffd27a',
};

const CLASS_COLORS: Record<string, string> = {
  fighter: '#c0392b',
  barbarian: '#d35400',
  rogue: '#5b4a6e',
  ranger: '#2e7d32',
  wizard: '#3949ab',
  cleric: '#f5f0e1',
};

export class MapView {
  readonly canvas: HTMLCanvasElement;
  private ctx: CanvasRenderingContext2D;
  private scale = 20;
  private ox = 0;
  private oy = 0;
  /** Camera in world tiles: centre and visible span, eased toward the settled area. */
  private cam = { x: WORLD / 2, y: WORLD / 2, span: 12 };
  private cssW = 1;
  private cssH = 1;
  private dpr = 1;
  private townRadius = 3;
  private firstFrame = true;
  private sprites = new Map<number, Sprite>();
  private trees: { x: number; y: number; r: number }[] = [];
  private river: { x: number; y: number }[] = [];
  sitePins: SitePin[] = [];
  selection: Selection | null = null;

  constructor(canvas: HTMLCanvasElement, sim: Simulation) {
    this.canvas = canvas;
    this.ctx = canvas.getContext('2d')!;
    this.layoutWilderness(sim.town);
  }

  /** Sites ring the town by distance; the river and forest are decoration seeded by the town seed. */
  private layoutWilderness(town: Town): void {
    const seed = 1.3;
    const c = WORLD / 2;
    this.sitePins = town.sites.map((s) => ({ id: s.def.id, x: c, y: c }));
    this.placeSites(town, 1);
    // A river runs down the eastern wilds and bends south, past the ford the town was founded on.
    this.river = [];
    for (let i = 0; i <= 40; i++) {
      const t = i / 40;
      const x = WORLD - 2.2 + Math.sin(t * 5 + seed) * 0.6 - t * t * 3;
      const y = -8 + t * (WORLD + 16);
      this.river.push({ x, y });
    }
    this.trees = [];
    for (let i = 0; i < 1400; i++) {
      const x = -6 + hash(i * 3 + 1) * (WORLD + 12);
      const y = -6 + hash(i * 3 + 2) * (WORLD + 12);
      const inTown = x > MARGIN - 0.3 && x < MARGIN + GRID + 0.3 && y > MARGIN - 0.3 && y < MARGIN + GRID + 0.3;
      if (inTown) continue;
      if (this.river.some((p) => Math.hypot(p.x - x, p.y - y) < 0.9)) continue;
      // Denser forest farther from the ford.
      const d = Math.hypot(x - WORLD / 2, y - WORLD / 2);
      if (hash(i * 3 + 3) > (d - 7) / 9) continue;
      this.trees.push({ x, y, r: 0.28 + hash(i * 7) * 0.22 });
    }
    this.trees.sort((a, b) => a.y - b.y);
  }

  /**
   * The wilds keep their distance from the frontier: sites sit just beyond the edge of the
   * settled area, farther for longer journeys, and drift outward as the town grows.
   */
  private placeSites(town: Town, ease: number): void {
    const c = WORLD / 2;
    let r = 3;
    for (const l of town.lots) if (l.zone !== 'None') r = Math.max(r, Math.hypot(MARGIN + l.x + 0.5 - c, MARGIN + l.y + 0.5 - c) + 0.7);
    this.townRadius += (r - this.townRadius) * ease;
    town.sites.forEach((s, i) => {
      const a = -Math.PI / 2 + (i / town.sites.length) * Math.PI * 2 + 0.35;
      const rr = this.townRadius + 1.3 + s.def.distance * 1.1;
      const pin = this.sitePins[i];
      pin.x = c + Math.cos(a) * rr;
      pin.y = c + Math.sin(a) * rr;
    });
  }

  resize(): void {
    this.dpr = Math.min(window.devicePixelRatio || 1, 2);
    const rect = this.canvas.getBoundingClientRect();
    this.cssW = Math.max(1, rect.width);
    this.cssH = Math.max(1, rect.height);
    this.canvas.width = Math.round(this.cssW * this.dpr);
    this.canvas.height = Math.round(this.cssH * this.dpr);
  }

  /** Frame the town and its sites, easing so growth reads as the map opening up. */
  private updateCamera(ease: number): void {
    let x0 = Infinity;
    let y0 = Infinity;
    let x1 = -Infinity;
    let y1 = -Infinity;
    for (const p of this.sitePins) {
      x0 = Math.min(x0, p.x - 2.2);
      x1 = Math.max(x1, p.x + 2.2);
      y0 = Math.min(y0, p.y - 1.1);
      y1 = Math.max(y1, p.y + 1.7);
    }
    const tx = (x0 + x1) / 2;
    const ty = (y0 + y1) / 2;
    const aspect = this.cssW / this.cssH;
    const span = Math.max(x1 - x0, (y1 - y0) * aspect);
    this.cam.x += (tx - this.cam.x) * ease;
    this.cam.y += (ty - this.cam.y) * ease;
    this.cam.span += (span - this.cam.span) * ease;
    this.scale = (this.canvas.width / this.cam.span);
    this.ox = this.canvas.width / 2 - this.cam.x * this.scale;
    this.oy = this.canvas.height / 2 - this.cam.y * this.scale;
  }

  /** Screen (CSS px) to world tiles. */
  toWorld(px: number, py: number): { x: number; y: number } {
    return { x: (px * this.dpr - this.ox) / this.scale, y: (py * this.dpr - this.oy) / this.scale };
  }

  pick(town: Town, px: number, py: number): Selection | null {
    const w = this.toWorld(px, py);
    let best: Agent | null = null;
    let bestD = 0.28;
    for (const a of town.agents) {
      const s = this.sprites.get(a.id);
      if (!s || !s.visible) continue;
      const d = Math.hypot(s.x - w.x, s.y - w.y);
      if (d < bestD) {
        bestD = d;
        best = a;
      }
    }
    if (best) return { kind: 'agent', id: best.id };
    for (const p of this.sitePins) if (Math.hypot(p.x - w.x, p.y - w.y) < 0.8) return { kind: 'site', id: p.id };
    const gx = Math.floor(w.x - MARGIN);
    const gy = Math.floor(w.y - MARGIN);
    if (gx >= 0 && gy >= 0 && gx < GRID && gy < GRID) {
      const lot = town.lotAt(gx, gy);
      if (lot.buildingId !== null) return { kind: 'building', id: lot.buildingId };
    }
    return null;
  }

  draw(town: Town, dt: number, snap = false): void {
    const { ctx } = this;
    const ease = snap || this.firstFrame ? 1 : 1 - Math.exp(-dt * 1.5);
    this.firstFrame = false;
    this.placeSites(town, ease);
    this.updateCamera(ease);
    const s = this.scale;
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = PALETTE.grass;
    ctx.fillRect(0, 0, this.canvas.width, this.canvas.height);
    ctx.setTransform(s, 0, 0, s, this.ox, this.oy);

    this.drawGround(town);
    this.drawRiver();
    this.drawLots(town);
    const night = nightness(town);
    this.drawBuildings(town, night);
    this.drawAgents(town, dt);
    this.drawTrees();
    this.drawSites(town);
    this.drawParties(town);
    this.drawSelection(town);

    // Day and night: a cool wash after dusk, warm light spilling from windows drawn on top.
    if (night > 0) {
      ctx.fillStyle = `rgba(20, 28, 64, ${0.5 * night})`;
      ctx.fillRect(-WORLD, -WORLD, WORLD * 3, WORLD * 3);
      this.drawWindows(town, night);
    }
  }

  private drawGround(town: Town): void {
    const { ctx } = this;
    ctx.fillStyle = PALETTE.grass;
    ctx.fillRect(-WORLD, -WORLD, WORLD * 3, WORLD * 3);
    // Mottled grass so the field does not read as a flat fill.
    ctx.fillStyle = PALETTE.grassDark;
    for (let i = 0; i < 260; i++) {
      const x = hash(i * 11 + 5) * WORLD;
      const y = hash(i * 11 + 6) * WORLD;
      ctx.globalAlpha = 0.35;
      ctx.beginPath();
      ctx.ellipse(x, y, 0.5 + hash(i) * 0.6, 0.3 + hash(i + 1) * 0.3, 0, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
    void town;
  }

  private drawRiver(): void {
    const { ctx } = this;
    const path = (w: number, color: string): void => {
      ctx.strokeStyle = color;
      ctx.lineWidth = w;
      ctx.lineCap = 'round';
      ctx.lineJoin = 'round';
      ctx.beginPath();
      this.river.forEach((p, i) => (i ? ctx.lineTo(p.x, p.y) : ctx.moveTo(p.x, p.y)));
      ctx.stroke();
    };
    path(1.25, PALETTE.dirtDark);
    path(1.0, PALETTE.river);
    path(0.35, PALETTE.riverLight);
  }

  private drawLots(town: Town): void {
    const { ctx } = this;
    // Zoned lots sit on packed dirt; neighbouring lots merge into streets.
    ctx.fillStyle = PALETTE.dirt;
    for (const l of town.lots) {
      if (l.zone === 'None' || l.zone === 'Farmland') continue;
      ctx.fillRect(MARGIN + l.x - 0.04, MARGIN + l.y - 0.04, 1.08, 1.08);
    }
    for (const l of town.lots) {
      if (l.zone === 'None') continue;
      const x = MARGIN + l.x;
      const y = MARGIN + l.y;
      const inset = l.zone === 'Farmland' ? 0.04 : 0.13;
      ctx.fillStyle =
        l.zone === 'Civic' ? PALETTE.plaza : l.zone === 'Commercial' ? PALETTE.commercial : l.zone === 'Farmland' ? PALETTE.farmland : PALETTE.residential;
      ctx.beginPath();
      ctx.roundRect(x + inset, y + inset, 1 - inset * 2, 1 - inset * 2, 0.08);
      ctx.fill();
      if (l.zone === 'Farmland') {
        ctx.strokeStyle = PALETTE.furrow;
        ctx.lineWidth = 0.035;
        ctx.beginPath();
        for (let i = 1; i < 6; i++) {
          ctx.moveTo(x + 0.1, y + i / 6);
          ctx.lineTo(x + 0.9, y + i / 6);
        }
        ctx.stroke();
      }
    }
  }

  private drawBuildings(town: Town, night: number): void {
    const sorted = [...town.buildings].sort((a, b) => a.lotId - b.lotId);
    for (const b of sorted) {
      const lot = town.lots[b.lotId];
      const x = MARGIN + lot.x;
      const y = MARGIN + lot.y;
      if (b.isSite) this.drawSite(b, x, y);
      else this.drawBuilding(b, x, y, night);
    }
  }

  private drawSite(b: Building, x: number, y: number): void {
    const { ctx } = this;
    const p = Math.min(1, b.buildProgress / b.def.buildHours);
    // Foundation, rising walls, and scaffolding.
    ctx.fillStyle = PALETTE.stone;
    ctx.fillRect(x + 0.18, y + 0.3, 0.64, 0.52);
    const h = 0.52 * p;
    ctx.fillStyle = PALETTE.timber;
    ctx.fillRect(x + 0.22, y + 0.82 - h, 0.56, h);
    ctx.strokeStyle = '#6b4a2a';
    ctx.lineWidth = 0.035;
    ctx.beginPath();
    for (const sx of [0.18, 0.5, 0.82]) {
      ctx.moveTo(x + sx, y + 0.86);
      ctx.lineTo(x + sx, y + 0.2);
    }
    ctx.moveTo(x + 0.14, y + 0.42);
    ctx.lineTo(x + 0.86, y + 0.42);
    ctx.moveTo(x + 0.14, y + 0.62);
    ctx.lineTo(x + 0.86, y + 0.62);
    ctx.moveTo(x + 0.18, y + 0.86);
    ctx.lineTo(x + 0.5, y + 0.42);
    ctx.stroke();
    // Progress bar.
    ctx.fillStyle = 'rgba(0,0,0,0.35)';
    ctx.fillRect(x + 0.15, y + 0.06, 0.7, 0.08);
    ctx.fillStyle = PALETTE.gold;
    ctx.fillRect(x + 0.15, y + 0.06, 0.7 * p, 0.08);
  }

  private drawBuilding(b: Building, x: number, y: number, night: number): void {
    const { ctx } = this;
    const id = b.def.id;
    const shadow = (bx: number, by: number, w: number, h: number): void => {
      ctx.fillStyle = 'rgba(40, 30, 20, 0.22)';
      ctx.fillRect(bx + 0.06, by + 0.06, w, h);
    };
    const house = (bx: number, by: number, w: number, h: number, roof: string, roofH = 0.55): void => {
      shadow(bx, by, w, h);
      const rh = h * roofH;
      ctx.fillStyle = PALETTE.wall;
      ctx.fillRect(bx, by + rh, w, h - rh);
      ctx.fillStyle = PALETTE.wallShade;
      ctx.fillRect(bx, by + h - 0.05, w, 0.05);
      ctx.fillStyle = roof;
      ctx.beginPath();
      ctx.moveTo(bx - 0.04, by + rh + 0.02);
      ctx.lineTo(bx + 0.1, by);
      ctx.lineTo(bx + w - 0.1, by);
      ctx.lineTo(bx + w + 0.04, by + rh + 0.02);
      ctx.closePath();
      ctx.fill();
      ctx.strokeStyle = 'rgba(0,0,0,0.25)';
      ctx.lineWidth = 0.025;
      ctx.beginPath();
      ctx.moveTo(bx + 0.1, by + rh * 0.45);
      ctx.lineTo(bx + w - 0.1, by + rh * 0.45);
      ctx.stroke();
      // Door.
      ctx.fillStyle = '#6b4a2a';
      ctx.fillRect(bx + w / 2 - 0.05, by + h - 0.18, 0.1, 0.16);
    };

    switch (id) {
      case 'house':
        house(x + 0.2, y + 0.22, 0.6, 0.58, PALETTE.roofHouse);
        break;
      case 'tavern': {
        house(x + 0.1, y + 0.16, 0.8, 0.68, PALETTE.roofTavern, 0.5);
        // Hanging sign.
        ctx.fillStyle = PALETTE.gold;
        ctx.beginPath();
        ctx.arc(x + 0.82, y + 0.62, 0.07, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      case 'town_hall': {
        house(x + 0.08, y + 0.24, 0.84, 0.62, PALETTE.roofHall, 0.5);
        // Bell tower and banner.
        ctx.fillStyle = PALETTE.wall;
        ctx.fillRect(x + 0.4, y + 0.04, 0.2, 0.3);
        ctx.fillStyle = PALETTE.roofHall;
        ctx.beginPath();
        ctx.moveTo(x + 0.36, y + 0.08);
        ctx.lineTo(x + 0.5, y - 0.1);
        ctx.lineTo(x + 0.64, y + 0.08);
        ctx.fill();
        ctx.strokeStyle = PALETTE.ink;
        ctx.lineWidth = 0.025;
        ctx.beginPath();
        ctx.moveTo(x + 0.5, y - 0.1);
        ctx.lineTo(x + 0.5, y - 0.3);
        ctx.stroke();
        ctx.fillStyle = '#c0392b';
        ctx.fillRect(x + 0.5, y - 0.3, 0.16, 0.09);
        break;
      }
      case 'farm': {
        ctx.fillStyle = PALETTE.wheat;
        for (let i = 0; i < 4; i++) ctx.fillRect(x + 0.08, y + 0.12 + i * 0.13, 0.5, 0.07);
        house(x + 0.6, y + 0.4, 0.32, 0.42, '#9b3d2a', 0.55);
        break;
      }
      case 'lumber_camp': {
        // Stumps, a log pile and a lean-to.
        ctx.fillStyle = PALETTE.timber;
        for (const [sx, sy] of [
          [0.2, 0.2],
          [0.35, 0.12],
          [0.15, 0.4],
        ])
          ctx.fillRect(x + sx, y + sy, 0.08, 0.08);
        for (let i = 0; i < 3; i++) {
          ctx.fillStyle = i % 2 ? '#a5784a' : PALETTE.timber;
          ctx.beginPath();
          ctx.roundRect(x + 0.12, y + 0.58 + i * 0.08, 0.4, 0.07, 0.03);
          ctx.fill();
        }
        house(x + 0.58, y + 0.3, 0.34, 0.5, PALETTE.forest, 0.6);
        break;
      }
      case 'forge': {
        house(x + 0.16, y + 0.24, 0.68, 0.58, PALETTE.roofForge, 0.5);
        ctx.fillStyle = PALETTE.stone;
        ctx.fillRect(x + 0.62, y + 0.06, 0.12, 0.26);
        ctx.fillStyle = `rgba(255, 120, 40, ${0.55 + 0.45 * night})`;
        ctx.beginPath();
        ctx.arc(x + 0.68, y + 0.06, 0.06, 0, Math.PI * 2);
        ctx.fill();
        break;
      }
      case 'brewery': {
        house(x + 0.14, y + 0.2, 0.62, 0.62, PALETTE.roofBrewery, 0.5);
        ctx.fillStyle = '#8a5a2b';
        for (const by of [0.42, 0.62]) {
          ctx.beginPath();
          ctx.ellipse(x + 0.84, y + by, 0.08, 0.09, 0, 0, Math.PI * 2);
          ctx.fill();
        }
        break;
      }
      default:
        house(x + 0.2, y + 0.22, 0.6, 0.58, PALETTE.roofHouse);
    }

    if (b.closed || b.forSale) {
      ctx.fillStyle = 'rgba(80, 80, 80, 0.35)';
      ctx.fillRect(x + 0.08, y + 0.08, 0.84, 0.84);
      ctx.fillStyle = '#f2e6cc';
      ctx.fillRect(x + 0.06, y + 0.72, 0.3, 0.16);
      ctx.fillStyle = PALETTE.ink;
      ctx.font = '0.12px sans-serif';
      ctx.fillText('SALE', x + 0.08, y + 0.84);
    }
  }

  private drawWindows(town: Town, night: number): void {
    const { ctx } = this;
    const occupied = new Set<number>();
    for (const a of town.agents) if (!a.departed && a.locationId !== null && a.activity !== 'Sleeping') occupied.add(a.locationId);
    for (const b of town.buildings) {
      if (!b.complete) continue;
      const lit = occupied.has(b.id) || b.def.venue || b.def.id === 'forge';
      if (!lit) continue;
      const lot = town.lots[b.lotId];
      const x = MARGIN + lot.x + 0.5;
      const y = MARGIN + lot.y + 0.6;
      const g = ctx.createRadialGradient(x, y, 0.02, x, y, b.def.venue ? 0.9 : 0.55);
      g.addColorStop(0, `rgba(255, 200, 110, ${0.55 * night})`);
      g.addColorStop(1, 'rgba(255, 200, 110, 0)');
      ctx.fillStyle = g;
      ctx.fillRect(x - 1, y - 1, 2, 2);
      ctx.fillStyle = PALETTE.window;
      ctx.fillRect(x - 0.2, y - 0.02, 0.08, 0.08);
      ctx.fillRect(x + 0.12, y - 0.02, 0.08, 0.08);
    }
  }

  private drawSites(town: Town): void {
    const { ctx } = this;
    for (const pin of this.sitePins) {
      const site = town.sites.find((s) => s.def.id === pin.id)!;
      const bounty = town.bounties.find((b) => b.siteId === pin.id && (b.status === 'open' || b.status === 'taken'));
      const r = 0.42 + site.def.tier * 0.06;
      ctx.fillStyle = 'rgba(0,0,0,0.3)';
      ctx.beginPath();
      ctx.ellipse(pin.x + 0.05, pin.y + 0.1, r, r * 0.8, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = site.cleared ? '#6f8f5b' : site.def.tier >= 3 ? '#4a2c3a' : site.def.tier === 2 ? '#5a3d2e' : '#5e5236';
      ctx.strokeStyle = site.cleared ? '#e8f0d8' : bounty ? PALETTE.gold : '#1d1712';
      ctx.lineWidth = bounty ? 0.09 : 0.05;
      ctx.beginPath();
      ctx.arc(pin.x, pin.y, r, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
      // Glyph: skull for a live threat, banner for a cleared site.
      ctx.fillStyle = site.cleared ? '#e8f0d8' : '#efe6d2';
      if (site.cleared) {
        ctx.fillRect(pin.x - 0.03, pin.y - 0.26, 0.05, 0.5);
        ctx.beginPath();
        ctx.moveTo(pin.x + 0.02, pin.y - 0.26);
        ctx.lineTo(pin.x + 0.26, pin.y - 0.16);
        ctx.lineTo(pin.x + 0.02, pin.y - 0.06);
        ctx.fill();
      } else {
        ctx.beginPath();
        ctx.arc(pin.x, pin.y - 0.04, 0.17, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillRect(pin.x - 0.1, pin.y + 0.06, 0.2, 0.12);
        ctx.fillStyle = '#1d1712';
        ctx.beginPath();
        ctx.arc(pin.x - 0.065, pin.y - 0.04, 0.045, 0, Math.PI * 2);
        ctx.arc(pin.x + 0.065, pin.y - 0.04, 0.045, 0, Math.PI * 2);
        ctx.fill();
      }
      // Tier pips.
      for (let i = 0; i < site.def.tier; i++) {
        ctx.fillStyle = PALETTE.gold;
        ctx.beginPath();
        ctx.arc(pin.x - (site.def.tier - 1) * 0.08 + i * 0.16, pin.y + r + 0.12, 0.055, 0, Math.PI * 2);
        ctx.fill();
      }
      // Keep labels legible on small screens.
      const fs = Math.max(0.34, (10 * this.dpr) / this.scale);
      ctx.font = `600 ${fs}px "Cinzel", Georgia, serif`;
      ctx.textAlign = 'center';
      const label = site.def.name.replace(/^the /, '');
      ctx.lineWidth = fs * 0.3;
      ctx.strokeStyle = 'rgba(20,16,10,0.75)';
      ctx.strokeText(label, pin.x, pin.y + r + 0.2 + fs);
      ctx.fillStyle = '#f6eedb';
      ctx.fillText(label, pin.x, pin.y + r + 0.2 + fs);
      if (bounty) {
        ctx.font = '700 0.3px system-ui, sans-serif';
        ctx.strokeText(`${bounty.reward}g`, pin.x, pin.y - r - 0.12);
        ctx.fillStyle = PALETTE.gold;
        ctx.fillText(`${bounty.reward}g`, pin.x, pin.y - r - 0.12);
      }
      ctx.textAlign = 'start';
    }
  }

  /** Parties on the road: a banner marching out, waiting at the site, then marching home. */
  private drawParties(town: Town): void {
    const { ctx } = this;
    const home = { x: WORLD / 2, y: WORLD / 2 };
    for (const p of town.parties) {
      if (p.returned) continue;
      const pin = this.sitePins.find((s) => s.id === p.siteId);
      if (!pin) continue;
      const span = Math.max(1, p.returnTick - p.departTick);
      const t = (town.tick - p.departTick) / span;
      const leg = t < 0.35 ? t / 0.35 : t > 0.65 ? 1 - (t - 0.65) / 0.35 : 1;
      const x = home.x + (pin.x - home.x) * leg;
      const y = home.y + (pin.y - home.y) * leg;
      ctx.setLineDash([0.12, 0.12]);
      ctx.strokeStyle = 'rgba(232, 185, 58, 0.7)';
      ctx.lineWidth = 0.05;
      ctx.beginPath();
      ctx.moveTo(home.x, home.y);
      ctx.lineTo(pin.x, pin.y);
      ctx.stroke();
      ctx.setLineDash([]);
      ctx.fillStyle = PALETTE.ink;
      ctx.fillRect(x - 0.02, y - 0.5, 0.04, 0.55);
      ctx.fillStyle = PALETTE.gold;
      ctx.beginPath();
      ctx.moveTo(x + 0.02, y - 0.5);
      ctx.lineTo(x + 0.34, y - 0.4);
      ctx.lineTo(x + 0.02, y - 0.28);
      ctx.fill();
      p.members.forEach((id, i) => {
        const a = town.agent(id);
        ctx.fillStyle = CLASS_COLORS[a.hero?.cls.id ?? ''] ?? '#ddd';
        ctx.beginPath();
        ctx.arc(x - 0.2 + (i % 3) * 0.16, y + 0.12 + Math.floor(i / 3) * 0.14, 0.07, 0, Math.PI * 2);
        ctx.fill();
      });
    }
  }

  private target(town: Town, a: Agent): { x: number; y: number } | null {
    if (a.departed || a.dead || a.isAway) return null;
    if (a.activity === 'Sleeping') return null;
    const j1 = hash(a.id * 13 + 1);
    const j2 = hash(a.id * 13 + 2);
    if (a.locationId !== null) {
      const b = town.building(a.locationId);
      const lot = town.lots[b.lotId];
      // Gather along the street in front of the building.
      return { x: MARGIN + lot.x + 0.15 + j1 * 0.7, y: MARGIN + lot.y + 0.86 + (j2 - 0.5) * 0.12 };
    }
    // Wanderers drift around the square, picking a new spot every few hours.
    const k = Math.floor(town.tick / 3);
    const ang = hash(a.id * 31 + k) * Math.PI * 2;
    const r = 1 + hash(a.id * 17 + k) * 3.2;
    return { x: WORLD / 2 + Math.cos(ang) * r, y: WORLD / 2 + Math.sin(ang) * r };
  }

  private drawAgents(town: Town, dt: number): void {
    const { ctx } = this;
    const k = 1 - Math.exp(-dt * 4);
    for (const a of town.agents) {
      const tgt = this.target(town, a);
      let s = this.sprites.get(a.id);
      if (!tgt) {
        if (s) s.visible = false;
        continue;
      }
      if (!s) {
        s = { x: WORLD / 2, y: WORLD / 2, visible: true };
        this.sprites.set(a.id, s);
      }
      s.visible = true;
      s.x += (tgt.x - s.x) * k;
      s.y += (tgt.y - s.y) * k;
    }
    // Draw in y order so nearer figures overlap farther ones.
    const order = town.agents.filter((a) => this.sprites.get(a.id)?.visible).sort((a, b) => this.sprites.get(a.id)!.y - this.sprites.get(b.id)!.y);
    for (const a of order) {
      const s = this.sprites.get(a.id)!;
      ctx.fillStyle = 'rgba(0,0,0,0.25)';
      ctx.beginPath();
      ctx.ellipse(s.x, s.y + 0.06, 0.09, 0.04, 0, 0, Math.PI * 2);
      ctx.fill();
      if (a.hero) {
        ctx.fillStyle = CLASS_COLORS[a.hero.cls.id] ?? '#ddd';
        ctx.strokeStyle = PALETTE.gold;
        ctx.lineWidth = 0.035;
      } else {
        ctx.fillStyle = raceColor(a.race);
        ctx.strokeStyle = a.activity === 'Drinking' ? '#e0a04a' : 'rgba(30,20,10,0.6)';
        ctx.lineWidth = 0.025;
      }
      ctx.beginPath();
      ctx.arc(s.x, s.y - 0.04, a.hero ? 0.085 : 0.07, 0, Math.PI * 2);
      ctx.fill();
      ctx.stroke();
    }
  }

  private drawTrees(): void {
    const { ctx } = this;
    for (const t of this.trees) {
      if (this.sitePins.some((p) => Math.abs(p.x - t.x) < 1.5 && Math.abs(p.y - t.y) < 1.3)) continue;
      ctx.fillStyle = 'rgba(0,0,0,0.2)';
      ctx.beginPath();
      ctx.ellipse(t.x + 0.06, t.y + t.r * 0.7, t.r * 0.9, t.r * 0.4, 0, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = PALETTE.forestDark;
      ctx.beginPath();
      ctx.arc(t.x, t.y, t.r, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = PALETTE.forest;
      ctx.beginPath();
      ctx.arc(t.x - t.r * 0.25, t.y - t.r * 0.25, t.r * 0.65, 0, Math.PI * 2);
      ctx.fill();
    }
  }

  private drawSelection(town: Town): void {
    const sel = this.selection;
    if (!sel) return;
    const { ctx } = this;
    ctx.strokeStyle = '#fff6d8';
    ctx.lineWidth = 0.06;
    ctx.setLineDash([0.15, 0.1]);
    if (sel.kind === 'building') {
      const b = town.building(sel.id as number);
      const lot = town.lots[b.lotId];
      ctx.strokeRect(MARGIN + lot.x + 0.03, MARGIN + lot.y + 0.03, 0.94, 0.94);
    } else if (sel.kind === 'agent') {
      const s = this.sprites.get(sel.id as number);
      if (s?.visible) {
        ctx.beginPath();
        ctx.arc(s.x, s.y - 0.04, 0.2, 0, Math.PI * 2);
        ctx.stroke();
      }
    } else {
      const p = this.sitePins.find((q) => q.id === sel.id);
      if (p) {
        ctx.beginPath();
        ctx.arc(p.x, p.y, 0.85, 0, Math.PI * 2);
        ctx.stroke();
      }
    }
    ctx.setLineDash([]);
  }
}

/** 0 by day, 1 at deep night, easing through dusk and dawn. */
export function nightness(town: Town): number {
  const h = (town.tick % HOURS_PER_DAY) + 0.5;
  if (h >= 7 && h <= 19) return 0;
  if (h > 19 && h < 22) return (h - 19) / 3;
  if (h >= 22 || h < 4) return 1;
  return 1 - (h - 4) / 3;
}

function raceColor(race: string): string {
  switch (race) {
    case 'human':
      return '#e9c9a0';
    case 'dwarf':
      return '#c98b5e';
    case 'elf':
      return '#d9e8b8';
    case 'halfling':
      return '#f0b98a';
    case 'gnome':
      return '#c4b0e0';
    case 'half-orc':
      return '#9fbf7a';
    default:
      return '#b9d0d8';
  }
}
