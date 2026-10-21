# Map layers: the underdark and the planes (future expansion)

Decided by Conner on 2026-10-21. Not built yet: this is expansion/DLC scope. Current work should leave room for it.

## The plan

- Each new realm is its own separate, giant map layer, as big as the surface world, with its own threats, regions,
  sites, settlements and factions. First candidates: the underdark, the fey realm, the hells, and space ("all the way
  through space and hell").
- The player can found and grow settlements in several layers at once (the surface city, expansion cities, then towns
  in the fey, the underdark and so on), and quest across all of them.
- Travel between layers is through portals. Late-game research lets the player build their own portals in the starting
  city and in expansion cities. A portal is a way in for enemies too, so it has to be guarded by might (garrison,
  walls) and/or magic (wards).
- This greatly widens the end game: new giant maps to quest and expand into after the surface is settled.

## What exists today (and is not the underdark)

- The undercity (`content/game/undercity.json`, `Systems/Undercity.cs`): five levels under the town (the Old
  Cellars, the Sewers, the Catacombs, the Drowned Vaults, the Deep Ways), each a site at distance 0 that opens with a
  charter tier. They are single sites, not a map.
- Institutions that point the way: the Undercity Gate, the Delvers, and the Gatehouse of Ways (`planar-gatehouse`),
  which trades with "places no road reaches" but has no map behind it.
- Far-land themes on the surface (e.g. `hellbreach`, `spellscar`) are surface regions touched by other planes, not the
  planes themselves.

## Leaving room for layers

The sim and client assume one world today. When the expansion starts, these are the places that need a layer key:

- Sites and regions: `Town.Sites`, `World` generation (rings, budgets, themes in `content/game/world.json`), `Roads`
  (terrain features, ranges) and `Passage` all build one world around one town. A layer would be a second world
  generated with its own rules file (rings, themes, terrain kinds) and its own seed stream.
- Distances are days from the founding town (`Passage.TilesPerDay`, `Reach0`); a layer needs its own origin (its
  portal or settlement) instead of the town centre.
- Client: `Geo.World` (2048 tiles), `WorldGen`, `WorldView`, fog (`Fog.cs`) and the strategic view are one map. A layer
  is a second map the camera switches to, with its own look (ground palette, sky, haze).
- Settlements: the town is a single `Town`. Several settlements at once (also needed for expansion cities) would share
  one treasury and research but keep their own plan, residents and buildings.
- Portals: a portal building (research-gated, like the Sky Dock lending an airship) would be the opening between two
  layers in `Passage`, and raids could come through it (`Threat`), so its defence is a real cost.

Keep new world code free of hidden "there is only one map" shortcuts where it's cheap to do so (pass the world or
layer in, don't reach for statics), so this stays an extension rather than a rewrite.
