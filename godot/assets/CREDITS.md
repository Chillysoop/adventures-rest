# Art credits

Every asset in the game is either made here or licensed CC0 / CC-BY. Add a line here for anything new.

## Third party (CC0)

- **Kenney, Fantasy Town Kit 2.0** — houses, walls, roofs, market stalls, banners, fountains, hedges, benches. https://kenney.nl/assets/fantasy-town-kit
- **Kenney, Castle Kit** — towers, gates, flags. https://kenney.nl/assets/castle-kit
- **Kenney, Nature Kit** — trees, rocks, bushes, tents, logs, flowers, statues and fences for decorations. https://kenney.nl/assets/nature-kit

All three are CC0 (licence files in `models/LICENSE-kenney-*.txt`). Credit is not required but given:
"Kenney (www.kenney.nl)". `tools/kenney_bake.py` downloads them and bakes the models used into `models/kenney.bin`
(one colour per triangle, recoloured to our palette in `src/Map/TownView.cs`).

## Made for this game

- Terrain, water, foliage, fog of war and the strategic map: shaders in `shaders/`, generated from the seed at load.
- Townsfolk, heroes, orcs, banners, far trees, roofs, grass, flowers and site landmarks: generated in `src/Map/MeshKit.cs`
  and its callers.
- Resource and alert icons in `icons/`: drawn for this game (SVG).
- Fonts: see `fonts/` (SIL Open Font License).
