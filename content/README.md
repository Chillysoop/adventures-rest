# Content library

Engine-neutral JSON that both the TypeScript sim and the Godot/C# port can load. It is a pool of
inspiration for heroes, spells, gear and monsters, not a rules engine: the game is real time, so
each entry also carries an `rt` block of real-time hints that systems can use or ignore.

## Layout

| Folder | What | Licence |
| --- | --- | --- |
| `srd/` | SRD 5.2 (2024) merged with SRD 5.1 (2014). 2024 wins; 2014 entries are kept only when 2024 has nothing equivalent, and fill gaps 2024 left blank (monster habitats, saving throws). | CC-BY-4.0 |
| `open/<book>/` | Open-licensed third-party books (Kobold Press, EN Publishing's Level Up, and others). Anything that duplicates an SRD entry, or an earlier book, is dropped. | CC-BY-4.0 or OGL-1.0a, per book |
| `original/` | Crown & Compass originals written for this game (fog of war, town, sieges, alchemy). | ours |

Each folder holds up to one file per kind: `classes`, `subclasses`, `species`, `backgrounds`, `feats`,
`spells`, `equipment`, `magic-items`, `monsters`, `conditions`. Every entry has `id`, `name` and
`source`; ids are unique within a kind across all folders, so a loader can merge them into one map.

- `sources.json`: every source id with title, publisher, licence and entry count.
- `ATTRIBUTION.md`: the credits the licences require; show it in the game's credits. Generated.
- `licenses/OGL-1.0a.txt`: the OGL text, which must ship with OGL content. Generated.
- `catalog.json`: counts per kind and source, including how many duplicates were folded. Generated.
- `conversion.json`: the knobs that turn tabletop numbers into `rt` hints, plus name aliases used
  for de-duplication (for example, the 2014 Goblin becomes the 2024 Goblin Warrior).

To avoid OGL content entirely, skip entries whose source has `"license": "OGL-1.0a"` in `sources.json`.

## Real-time hints (`rt`)

- Spells: `castSeconds`, `cooldownSeconds` (grows with level), `durationSeconds` (rounds are
  `secondsPerRound`; long durations are compressed, -1 means lasts until dispelled), `range`
  band (`self`, `touch`, `short`, `medium`, `far`), `targets`, `roles` (damage, heal, control, buff,
  summon, movement, scouting, ward, utility), `sustained` (concentration), `downtime` (cast in
  town, not in a fight).
- Monsters: `tier` and `band` (minion, local, regional, realm, legend) from challenge rating,
  `threat` (XP), `role` (solo, artillery, controller, skirmisher, brute, soldier), `dps` and
  `burst` estimates, `moves`, `fast`, `thinks` (smart enough to join factions or plan raids).
- Items: `slot`, `tier` from rarity, `valueGp`, `consumable`. Magic weapons and armour that come in
  many forms are one entry with a `variants` list.
- Classes and subclasses: `role` (frontline, skirmisher, caster, support, specialist, scout).

Originals may add game-specific hints such as `fog`, `town`, `siege` or `faction`.

## Regenerating

```
git clone --depth 1 https://github.com/open5e/open5e-api /tmp/open5e
npm run content:import -- --from /tmp/open5e/data/v2
```

The importer rewrites `srd/`, `open/` and the generated files; `original/` and `conversion.json`
are hand-edited and never touched. `npm test` checks ids, sources, links and attribution.
