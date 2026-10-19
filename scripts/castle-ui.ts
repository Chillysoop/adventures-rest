// Draws the castle look's nine-patch art into godot/assets/ui/ as SVGs: oak panels with iron corner brackets for
// windows, a carved beam for title bars, a crenellated stone parapet for the top bar, a stone ledge for the dock, oak
// plank buttons with iron studs and a red banner for the main action. Made for this game (see CREDITS.md).
//
//   node scripts/castle-ui.ts
//
// The client cuts each into a StyleBoxTexture with the margins in godot/src/Castle.cs; keep the two in step.

import { writeFileSync, mkdirSync } from "node:fs";
import { join, resolve } from "node:path";

const out = join(resolve(import.meta.dirname, ".."), "godot/assets/ui");
mkdirSync(out, { recursive: true });

// A small deterministic random, so the grain and the stones come out the same every run.
let seed = 7;
const rnd = () => ((seed = (seed * 1103515245 + 12345) % 2147483648) / 2147483648);

function svg(w: number, h: number, body: string) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${body}</svg>\n`;
}

/** Wood grain: long faint lines across a board. */
function grain(x: number, y: number, w: number, h: number, color: string, n: number, vertical = false) {
  let s = "";
  for (let i = 0; i < n; i++) {
    const o = (vertical ? x : y) + rnd() * (vertical ? w : h);
    const a = (0.08 + rnd() * 0.12).toFixed(2);
    const wob = (rnd() - 0.5) * 3;
    s += vertical
      ? `<path d="M${o.toFixed(1)} ${y} q${wob.toFixed(1)} ${h / 2} 0 ${h}" stroke="${color}" stroke-opacity="${a}" stroke-width="${(0.6 + rnd()).toFixed(1)}" fill="none"/>`
      : `<path d="M${x} ${o.toFixed(1)} q${w / 2} ${wob.toFixed(1)} ${w} 0" stroke="${color}" stroke-opacity="${a}" stroke-width="${(0.6 + rnd()).toFixed(1)}" fill="none"/>`;
  }
  return s;
}

const stud = (x: number, y: number, r = 2.2) =>
  `<circle cx="${x}" cy="${y}" r="${r}" fill="#2a2b2e"/><circle cx="${x - r * 0.3}" cy="${y - r * 0.3}" r="${r * 0.45}" fill="#9aa0a6" fill-opacity="0.8"/>`;

/** An iron corner bracket, drawn for the top-left corner and turned for the others. */
function bracket(w: number, h: number, size: number, inset: number) {
  const one = `<path d="M${inset} ${inset + size}V${inset}h${size}" stroke="#1b1c1f" stroke-width="7" fill="none" stroke-linecap="square"/>` +
    `<path d="M${inset} ${inset + size}V${inset}h${size}" stroke="#55595f" stroke-width="4" fill="none" stroke-linecap="square"/>` +
    stud(inset + 1, inset + 1, 2.4) + stud(inset + size - 3, inset + 0.5, 1.6) + stud(inset + 0.5, inset + size - 3, 1.6);
  return [`<g>${one}</g>`, `<g transform="translate(${w} 0) scale(-1 1)">${one}</g>`, `<g transform="translate(0 ${h}) scale(1 -1)">${one}</g>`, `<g transform="translate(${w} ${h}) scale(-1 -1)">${one}</g>`].join("");
}

// Windows and dialogs: dark oak boards inside a darker frame, iron brackets on the corners.
{
  const w = 72, h = 72;
  let b = `<rect x="0.5" y="0.5" width="${w - 1}" height="${h - 1}" rx="5" fill="#120d09"/>`;
  b += `<rect x="3" y="3" width="${w - 6}" height="${h - 6}" rx="3" fill="#1e1610"/>`;
  b += grain(3, 3, w - 6, h - 6, "#6b4e33", 12);
  b += `<rect x="3.5" y="3.5" width="${w - 7}" height="${h - 7}" rx="3" fill="none" stroke="#5a4129" stroke-opacity="0.5"/>`;
  b += bracket(w, h, 16, 2);
  writeFileSync(join(out, "oak-panel.svg"), svg(w, h, b));
}

// A window's title bar: a carved beam, lighter oak, with a highlight above and a shadow below.
{
  const w = 64, h = 40;
  let b = `<rect width="${w}" height="${h}" fill="#3a2a1b"/>`;
  b += grain(0, 0, w, h, "#9a7a52", 10);
  b += `<rect y="0" width="${w}" height="2" fill="#6e5236" fill-opacity="0.8"/><rect y="${h - 3}" width="${w}" height="3" fill="#0e0a07" fill-opacity="0.9"/>`;
  b += stud(8, h / 2, 2.4) + stud(w - 8, h / 2, 2.4);
  writeFileSync(join(out, "beam.svg"), svg(w, h, b));
}

/** Rows of stone blocks with mortar between. */
function stones(x0: number, y0: number, w: number, h: number, rowH: number, base = 40) {
  let s = `<rect x="${x0}" y="${y0}" width="${w}" height="${h}" fill="#1b1814"/>`;
  for (let r = 0, y = y0; y < y0 + h; r++, y += rowH) {
    let x = x0 - (r % 2 ? 12 : 0);
    while (x < x0 + w) {
      const bw = 20 + Math.floor(rnd() * 3) * 2;
      const shade = base + Math.floor(rnd() * 12);
      const c = `rgb(${shade + 6},${shade + 1},${shade - 6})`;
      s += `<rect x="${x + 1}" y="${y + 1}" width="${bw - 2}" height="${rowH - 2}" rx="1.5" fill="${c}"/>`;
      s += `<rect x="${x + 1}" y="${y + 1}" width="${bw - 2}" height="1.2" fill="#ffffff" fill-opacity="0.07"/>`;
      x += bw;
    }
  }
  return s;
}

// The top bar: dressed stone with merlons hanging below its lower edge. 48 wide so it tiles: two merlons a tile.
{
  const w = 48, body = 58, merlon = 12, h = body + merlon;
  let b = `<defs><clipPath id="c"><path d="M0 0H${w}V${body}h-6v${merlon}h-14v-${merlon}h-4v${merlon}h-14v-${merlon}h-10z"/></clipPath></defs>`;
  // A darker wash over the face of the wall, where the figures sit, so they read over the stone.
  b += `<defs><linearGradient id="g" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#0d0b09" stop-opacity="0.15"/><stop offset="0.5" stop-color="#0d0b09" stop-opacity="0.5"/><stop offset="1" stop-color="#0d0b09" stop-opacity="0.2"/></linearGradient></defs>`;
  b += `<g clip-path="url(#c)">${stones(0, 0, w, h, 14)}<rect width="${w}" height="${body}" fill="url(#g)"/><rect y="${body - 2}" width="${w}" height="2" fill="#0d0b09" fill-opacity="0.6"/></g>`;
  b += `<rect y="${body - 1}" width="${w}" height="1" fill="#0d0b09"/>`;
  writeFileSync(join(out, "parapet.svg"), svg(w, h, b));
}

// The dock: a stone ledge with merlons standing up from its top edge, the dock buttons sitting in the wall.
{
  const w = 48, merlon = 10, body = 66, h = merlon + body;
  let b = `<defs><clipPath id="c"><path d="M0 ${merlon}h4v-${merlon}h14v${merlon}h6v-${merlon}h14v${merlon}h10V${h}H0z"/></clipPath></defs>`;
  b += `<g clip-path="url(#c)">${stones(0, 0, w, h, 13)}<rect y="${merlon}" width="${w}" height="${body}" fill="#120f0c" fill-opacity="0.55"/></g>`;
  writeFileSync(join(out, "ledge.svg"), svg(w, h, b));
}

/** A plank button: boards, a dark rim, an iron stud at each end. */
function plank(file: string, fill: string, rim: string, grainColor: string, edge: string, studs: boolean, inset = false) {
  const w = 40, h = 30;
  let b = `<rect x="0.5" y="0.5" width="${w - 1}" height="${h - 1}" rx="4" fill="${rim}"/>`;
  b += `<rect x="2" y="2" width="${w - 4}" height="${h - 4}" rx="3" fill="${fill}"/>`;
  b += grain(2, 2, w - 4, h - 4, grainColor, 6);
  b += inset
    ? `<rect x="2" y="2" width="${w - 4}" height="3" rx="2" fill="#000" fill-opacity="0.35"/>`
    : `<rect x="2" y="2" width="${w - 4}" height="1.5" rx="1" fill="#fff" fill-opacity="0.12"/><rect x="2" y="${h - 4}" width="${w - 4}" height="2" fill="#000" fill-opacity="0.3"/>`;
  if (edge) b += `<rect x="1.5" y="1.5" width="${w - 3}" height="${h - 3}" rx="3.5" fill="none" stroke="${edge}" stroke-width="1.2"/>`;
  if (studs) b += stud(6, 6, 1.6) + stud(w - 6, 6, 1.6) + stud(6, h - 6, 1.6) + stud(w - 6, h - 6, 1.6);
  writeFileSync(join(out, file), svg(w, h, b));
}

plank("button.svg", "#3b2a1b", "#0f0b08", "#a07c52", "", true);
plank("button-hover.svg", "#4a3522", "#0f0b08", "#b48c5e", "#c9a24a", true);
plank("button-pressed.svg", "#2e2015", "#0f0b08", "#8a6a46", "#c9a24a", true, true);
plank("button-disabled.svg", "#251c14", "#0f0b08", "#6a5440", "", false);
// The main action: a red banner bound in gold.
plank("banner.svg", "#8a2a20", "#2a0b07", "#e07a5f", "#c9a24a", true);
plank("banner-hover.svg", "#a1342a", "#2a0b07", "#f09a7f", "#e8c463", true);
plank("banner-pressed.svg", "#6e2018", "#2a0b07", "#c0604a", "#c9a24a", true, true);
plank("banner-disabled.svg", "#4a2420", "#1a0907", "#80504a", "#6a5a3a", false);

console.log(`wrote the castle UI art to ${out}`);
