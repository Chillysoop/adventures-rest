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
  ["fiends", "imp-laugh", "#e0553a"],
  // What a story choice puts at stake (content/game/assess.json icons) beside the goods and town icons above
  ["dread", "terror", "#b28ad8"],
  ["unrest", "fist", "#e07a5f"],
  ["standing", "shaking-hands", "#7fb0d4"],
  ["threat", "crossed-swords", "#ef7f6f"],
  ["foe-unseen", "fog", "#a9a294"],
  ["lore", "scroll-unfurled", "#d8c08a"],
  ["decree", "scroll-quill", "#cfc6b0"],
  // The UI kit's glyphs (godot/src/UiKit.cs, docs/ui-text.md): coloured so a line reads at a glance, before any word
  ["ui-info", "info", "#7fb0d4"],
  ["ui-time", "hourglass", "#d8c08a"],
  ["ui-day", "sundial", "#cfc6b0"],
  ["ui-yes", "check-mark", "#8cc084"],
  ["ui-no", "cross-mark", "#ef7f6f"],
  ["ui-warn", "hazard-sign", "#e8b93a"],
  ["ui-road", "walking-boot", "#c4955f"],
  ["ui-heal", "health-normal", "#8cc084"],
  ["ui-wound", "sword-wound", "#ef7f6f"],
  ["ui-up", "upgrade", "#8cc084"],
  ["ui-lock", "padlock", "#b3a68f"],
  ["ui-star", "round-star", "#e8b93a"],
  ["ui-renown", "laurel-crown", "#e8b93a"],
  ["ui-person", "person", "#cfc6b0"],
  ["ui-house", "family-house", "#c08550"],
  ["ui-trade", "trade", "#d9b25a"],
  ["ui-shrine", "church", "#c9a0e8"],
  ["ui-holy", "holy-symbol", "#f2d27a"],
  ["ui-follower", "prayer", "#c9a0e8"],
  ["ui-miracle", "stars-stack", "#f2d27a"],
  ["ui-eye", "eye-target", "#7fb0d4"],
  ["ui-skull", "skull-crossed-bones", "#cfcab4"],
  ["ui-shield", "checked-shield", "#7fb0d4"],
  ["ui-search", "magnifying-glass", "#cfc6b0"],
  ["ui-faction", "tattered-banner", "#d9b25a"],
  ["ui-bag", "swap-bag", "#c08550"],
  ["ui-heart", "hearts", "#e07aa0"],
  // The gods (content/original/deities.json): god-<id>, one glyph after each holy symbol
  ["god-anvil-father", "anvil-impact", "#d9a05a"],
  ["god-aurel", "open-palm", "#e8d27a"],
  ["god-even-scale", "scales", "#cfd6e0"],
  ["god-ferryman", "canoe", "#9fb0c8"],
  ["god-greenhush", "oak-leaf", "#8cc084"],
  ["god-hollow-king", "crowned-skull", "#a8b090"],
  ["god-iron-crown", "mailed-fist", "#8a8f9a"],
  ["god-lantern-mother", "lantern-flame", "#f2c45a"],
  ["god-one-eye", "bleeding-eye", "#d8443c"],
  ["god-red-banner", "tattered-banner", "#d0463c"],
  ["god-stormmother", "trident", "#6fb3e0"],
  ["god-veiled-coin", "two-coins", "#a8a0b8"],
  ["god-ysolde", "book-aura", "#9c88e0"],
  // Character sheet scores (content/game/character.json) beside the icons above
  ["health", "health-normal", "#e07070"],
  ["faith", "prayer", "#f0d890"],
  ["wits", "brain", "#c9a0dc"],
  ["will", "muscle-up", "#d89a5c"],
  // Institutions (content/game/institutions.json "icon")
  ["inst-temple-district", "church", "#e8d08a"],
  ["inst-adventurers-hall", "swords-emblem", "#d9b25a"],
  ["inst-grand-bazaar", "medieval-pavilion", "#e0a43c"],
  ["inst-druid-grove", "oak", "#8cc084"],
  ["inst-famous-tavern", "beer-stein", "#e6b85c"],
  ["inst-wizards-library", "spell-book", "#a68ad8"],
  ["inst-paladin-chapterhouse", "templar-shield", "#e8e0c8"],
  ["inst-alchemists-quarter", "cauldron", "#9fd46a"],
  ["inst-artificers-foundry", "gears", "#c8a060"],
  ["inst-grand-arena", "arena", "#e07a5f"],
  ["inst-bards-college", "lyre", "#d4a0e0"],
  ["inst-undercity-gate", "dungeon-gate", "#a8a294"],
  ["inst-griffon-roost", "griffin-symbol", "#e3b04a"],
  ["inst-sky-dock", "air-balloon", "#7fb0d4"],
  ["inst-monster-sanctuary", "paw", "#c4955f"],
  ["inst-cursed-auction-house", "cursed-star", "#d0566a"],
  ["inst-fey-market", "fairy-wand", "#e89ad8"],
  ["inst-oracle-observatory", "crystal-ball", "#7fc8e0"],
  ["inst-dungeon-entrance", "cave-entrance", "#b3ada4"],
  ["inst-embassy-quarter", "flying-flag", "#7f9fd4"],
  ["inst-planar-gatehouse", "magic-portal", "#b48ae8"],
  ["inst-hall-of-heroes", "trophy", "#e8b93a"],
  ["inst-resident-dragon", "dragon-spiral", "#e8673f"],
  // Dock windows, the build bar and the menu
  ["tab-city", "castle", mono],
  ["tab-ledger", "notebook", mono],
  ["tab-war", "crossed-swords", mono],
  ["tab-defence", "guarded-tower", mono],
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
  // The council chamber (godot/src/Chamber.cs): votes over heads, seats, loyalty, what the mayor holds over them
  ["vote-aye", "open-palm", "#8cc084"],
  ["vote-nay", "thumb-down", "#ef7f6f"],
  ["seat-locked", "padlock", "#b3a68f"],
  ["loyal", "hearts", "#8cc084"],
  ["wary", "sly", "#e8b93a"],
  ["disloyal", "broken-heart", "#ef7f6f"],
  ["secret", "secret-book", "#e8b93a"],
  ["traitor", "backstab", "#d0566a"],
  ["dig", "magnifying-glass", "#d8c08a"],
  ["expose", "shouting", "#e07a5f"],
  ["press", "chained-heart", "#d0566a"],
  ["skill", "upgrade", "#7fb0d4"],
  ["deadline", "hourglass", "#d8c08a"],
  // Armory items by kind (godot/src/Armory.cs), white so the window tints them by rarity
  ["item-weapon", "broadsword", mono],
  ["item-armor", "breastplate", mono],
  ["item-shield", "round-shield", mono],
  ["item-trinket", "gem-pendant", mono],
  ["item-ring", "ring", mono],
  ["item-wand", "orb-wand", mono],
  ["item-stone", "rune-stone", mono],
  // The bounty board's buttons: the terms of a job, then what else a row can do
  ["act-slay", "broadsword", mono],
  ["act-drive", "run", mono],
  ["act-parley", "conversation", mono],
  ["act-rescue", "breaking-chain", mono],
  ["act-fetch", "knapsack", mono],
  ["act-recover", "tombstone", mono],
  ["act-loot", "open-treasure-chest", mono],
  ["act-scry", "crystal-ball", mono],
  ["act-scout", "spyglass", mono],
  ["act-word", "envelope", mono],
  ["act-recall", "return-arrow", mono],
  ["act-offer", "shaking-hands", mono],
  ["act-explore", "treasure-map", mono],
  ["act-build", "claw-hammer", mono],
  // Enemy portraits (godot/src/Portrait.cs draws them on a medallion tinted by the enemy's kind)
  ...[
    "orc-head", "ogre", "troll", "goblin-head", "brutal-helm", "horned-helm", "giant",
    "shambling-zombie", "death-skull", "vampire-dracula", "floating-ghost", "crowned-skull", "dread-skull",
    "hooded-assassin", "hooded-figure", "spy", "domino-mask", "iron-mask", "witch-face", "frog",
    "wolf-head", "bear-head", "direwolf", "spider-face", "boar", "harpy", "rat", "evil-bat", "snake", "scorpion", "werewolf",
    "dragon-head", "spiked-dragon-head", "wyvern", "sea-dragon", "hydra",
    "cultist", "warlock-hood", "tentacles-skull", "devil-mask", "all-seeing-eye", "imp",
    "black-knight-helm", "visored-helm", "bandit", "overlord-helm", "crested-helmet",
    "minotaur", "gargoyle", "golem-head", "cyclops", "kraken-tentacle", "goo-skull", "mimic-chest", "help", "crown",
    "barn-owl", "eagle-head", "raven", "amber-mosquito", "crab", "shark-jaws", "fish-monster", "lizardman", "evil-tree", "fairy",
    "dwarf-face", "woman-elf-face", "mermaid", "centaur", "worm-mouth", "flying-beetle", "wasp-sting", "vulture", "horse-head",
    "unicorn", "greek-sphinx", "medusa-head", "hyena-head", "lion", "tiger-head", "saber-toothed-cat-head", "fire",
  ].map((g): [string, string, string] => [`face-${g}`, g, mono]),
];

for (const [name, glyph, tint] of icons) {
  const body = pack.icons[glyph]?.body;
  if (!body) throw new Error(`game-icons has no ${glyph}`);
  const paths = body.replaceAll(`fill="currentColor"`, "");
  const svg =
    tint === mono
      ? // Portraits are drawn large, so they are imported at twice the size.
        `<svg xmlns="http://www.w3.org/2000/svg" width="${name.startsWith("face-") ? 128 : 64}" height="${name.startsWith("face-") ? 128 : 64}" viewBox="0 0 512 512"><g fill="#ffffff">${paths}</g></svg>\n`
      : // The outline first, then the glyph over it: a dark rim like the drawn icons, without paint-order.
        `<svg xmlns="http://www.w3.org/2000/svg" width="64" height="64" viewBox="-16 -16 544 544"><g fill="#1d1712" stroke="#1d1712" stroke-width="34" stroke-linejoin="round">${paths}</g><g fill="${tint}">${paths}</g></svg>\n`;
  writeFileSync(join(out, `${name}.svg`), svg);
}
console.log(`wrote ${icons.length} icons to ${out}`);
