# Art credits

Every asset in the game is either made here or licensed CC0 / CC-BY. Add a line here for anything new.

## Third party (CC0)

- **Kenney, Fantasy Town Kit 2.0** — houses, walls, roofs, market stalls, banners, fountains, hedges, benches. https://kenney.nl/assets/fantasy-town-kit
- **Kenney, Castle Kit** — towers, gates, flags. https://kenney.nl/assets/castle-kit
- **Kenney, Nature Kit** — trees, rocks, bushes, tents, logs, flowers, statues and fences for decorations. https://kenney.nl/assets/nature-kit

All three are CC0 (licence files in `models/LICENSE-kenney-*.txt`). Credit is not required but given:
"Kenney (www.kenney.nl)". `tools/kenney_bake.py` downloads them and bakes the models used into `models/kenney.bin`
(one colour per triangle, recoloured to our palette in `src/Map/TownView.cs`).

## Third party (CC BY 3.0)

- **game-icons.net** (Lorc, Delapouite and contributors) — the goods, enemy-kind, stakes, dock and glossary icons and the
  enemy portrait faces in `icons/` written by `scripts/icons.ts` (food, timber, tools, ale, stone, wool, cloth, furniture,
  herbs, potions, wine, books, tribe, undead, schemers, beasts, dragon, cult, rivals, dread, unrest, standing, threat,
  lore, decree, regalia-*, sheet, reroll, face-*, tab-*, ui-*, god-*, glossary). https://game-icons.net, licence
  https://creativecommons.org/licenses/by/3.0/. Recoloured and outlined for the game.

## Made for this game

- Terrain, water, foliage, fog of war and the strategic map: shaders in `shaders/`, generated from the seed at load.
- Homes (cottage, timber house, townhouse, tenement, tall house, manor): modelled for this game in Blender from code by
  `tools/blender_houses.py`, baked to `models/crafted.bin`.
- Cliff faces round the plateaus: generated in `src/Map/WorldView.cs` (BuildCliffs).
- Townsfolk, heroes, orcs, banners, far trees, roofs, grass, flowers and site landmarks: generated in `src/Map/MeshKit.cs`
  and its callers.
- Gold, bread, people, heroes, homeless, mandate, favour, suspicion, defence, warband, d20, compass and alert icons in
  `icons/`: drawn for this game (SVG).
- The castle look in `ui/` (oak panels, iron brackets, beams, stone parapet and ledge, plank and banner buttons):
  drawn for this game by `scripts/castle-ui.ts` (SVG).
- Fonts: see `fonts/` (SIL Open Font License).
