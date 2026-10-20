// Writes the game-icons.net glyphs the client uses into godot/assets/icons/ as small SVGs: resources and enemy kinds
// tinted with a dark outline like the hand-drawn icons beside them, dock and menu icons as plain white glyphs the
// theme tints. game-icons.net is CC BY 3.0 (credited in godot/assets/CREDITS.md); the glyphs come from the
// @iconify-json/game-icons package.
//
//   node scripts/icons.ts
//
// To add one: put it in the table below (our name, the game-icons.net name, a tint or "mono"), run this, then let
// Godot import it (`godot --headless --path godot --import`) and commit the .svg and its .svg.import.

import { readFileSync, writeFileSync } from "node:fs";
import { createRequire } from "node:module";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const out = join(root, "godot/assets/icons");
const pack = JSON.parse(readFileSync(createRequire(import.meta.url).resolve("@iconify-json/game-icons/icons.json"), "utf8")) as {
  icons: Record<string, { body: string }>;
};

const mono = "mono";
const icons: [name: string, glyph: string, tint: string][] = [
  // Goods (Good in the sim)
  ["food", "wheat", "#e2c25c"],
  ["timber", "wood-pile", "#b07a44"],
  ["tools", "hammer-nails", "#a7b0b8"],
  ["ale", "beer-stein", "#e0a43c"],
  ["stone", "stone-block", "#b3ada4"],
  ["wool", "wool", "#ece5d3"],
  ["cloth", "rolled-cloth", "#7f9fd4"],
  ["furniture", "wooden-chair", "#c08550"],
  ["herbs", "herbs-bundle", "#8cc084"],
  ["potions", "potion-ball", "#c784d8"],
  ["wine", "wine-bottle", "#b8475e"],
  ["books", "book-cover", "#c8694a"],
  // Menace kinds (saga.json menaces.kinds)
  ["tribe", "orc-head", "#9cb85a"],
  ["undead", "raise-zombie", "#cfcab4"],
  ["schemers", "hooded-figure", "#a68ad8"],
  ["beasts", "wolf-head", "#c4955f"],
  ["dragon", "dragon-head", "#e8673f"],
  ["cult", "cultist", "#d0566a"],
  ["rivals", "duel", "#d9b25a"],
  // Dock windows, the build bar and the menu
  ["tab-city", "castle", mono],
  ["tab-ledger", "notebook", mono],
  ["tab-war", "crossed-swords", mono],
  ["tab-council", "gavel", mono],
  ["tab-bounties", "scroll-unfurled", mono],
  ["tab-companies", "knight-banner", mono],
  ["tab-armory", "anvil", mono],
  ["tab-diplomacy", "shaking-hands", mono],
  ["tab-institutions", "greek-temple", mono],
  ["tab-faith", "prayer", mono],
  ["tab-campaign", "treasure-map", mono],
  ["tab-chronicles", "scroll-quill", mono],
  ["tab-mayor", "throne-king", mono],
  ["tab-library", "spell-book", mono],
  ["tab-build", "claw-hammer", mono],
  ["glossary", "open-book", mono],
  // Quick orders over the corner map
  ["order-defend", "guarded-tower", mono],
  ["order-guard", "flying-flag", mono],
  ["order-strike", "sword-brandish", mono],
  ["order-scout", "spyglass", mono],
];

for (const [name, glyph, tint] of icons) {
  const body = pack.icons[glyph]?.body;
  if (!body) throw new Error(`game-icons has no ${glyph}`);
  const paths = body.replaceAll(`fill="currentColor"`, "");
  const svg =
    tint === mono
      ? `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="0 0 512 512"><g fill="#ffffff">${paths}</g></svg>\n`
      : // The outline first, then the glyph over it: a dark rim like the drawn icons, without paint-order.
        `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="-16 -16 544 544"><g fill="#1d1712" stroke="#1d1712" stroke-width="34" stroke-linejoin="round">${paths}</g><g fill="${tint}">${paths}</g></svg>\n`;
  writeFileSync(join(out, `${name}.svg`), svg);
}
console.log(`wrote ${icons.length} icons to ${out}`);
