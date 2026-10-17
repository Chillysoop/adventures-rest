# Adventures Rest

A D&D-themed city builder where you govern a frontier town as its mayor while a living campaign
plays out around you. (The design doc's working title is *Crown & Compass*.)

The game is built for the web: every build is a page you can open and play, and the simulation
runs in the browser.

## What's here

- `src/sim`: the simulation, plain TypeScript with no rendering
  - `rng.ts`, `time.ts`, `ledger.ts`, `content.ts`: seeded dice, time, the Ledger (town chronicle), data-driven content
  - `agent.ts`: townsfolk with D&D ability scores, traits, needs, money, ambitions and opinions of each other
  - `town.ts`: lots, buildings and the market
  - `systems/`: hourly utility AI (`brain`), production and wages (`economy`), buying land and
    building (`property`), tavern life (`social`), arrivals and departures (`population`), and a
    stand-in `mayor` that zones land while there is no player
  - `systems/quests.ts`, `systems/expedition.ts`: heroes, the bounty board, adventuring parties, and a
    seeded d20 expedition resolver that writes a Chronicle of every delve
  - `systems/council.ts`: three council seats (Steward, Speaker, Captain) with loyalty and agendas,
    motions the mayor signs or vetoes, decrees, and Mandate
  - `systems/threat.ts`, `systems/director.ts`: the Bloodtusk warband growing off-screen, its
    escalation (omens, war camp, raids, the march), the Last Stand siege, and the Director that paces it
- `src/sim/content/*.json`: the TypeScript sim's own frozen copy of the game data (see below)
- `src/client`: the browser game. A Canvas 2D map (`map.ts`) and the HTML panels (`main.ts`,
  `index.html`): time controls, the Ledger, the bounty board, Chronicles and an inspector
- `scripts/build-client.ts`: bundles the client into one self-contained HTML file
- `src/cli.ts`: headless runner that prints the Ledger
- `tests/`: determinism, growth, invariants, dice and market

## The C# sim (`dotnet/`)

The game is moving to Godot 4 with C#. `dotnet/` holds a .NET 8 port of the simulation with no Godot
dependency, so a Godot client can reference it directly:

- `dotnet/src/CrownAndCompass.Sim`: the sim, system by system the same shape as `src/sim` (`Systems/Brain.cs`,
  `Economy.cs`, `Quests.cs`, `Threat.cs`, ...). It embeds `content/game/*.json` and the content library
  (`content/srd`, `content/open`, `content/original`: monsters, classes, spells, magic items) at build time.
- `dotnet/src/CrownAndCompass.Cli`: the headless runner, same flags and output as `npm run sim`
- `dotnet/tests/CrownAndCompass.Sim.Tests`: xUnit, every TypeScript test ported, plus checks of the JS
  number semantics the port relies on

The C# sim is now the game. The TypeScript sim and its web build are frozen as the legacy web build: they read their
own copy of the old game data (`src/sim/content/`) and no longer track the C# sim, so the two Ledgers differ. (They
matched byte for byte until the C# sim started drawing on the content library.)

```bash
dotnet test dotnet/CrownAndCompass.sln
dotnet run --project dotnet/src/CrownAndCompass.Cli -c Release -- --days 120 --seed 1
```

What comes from the content library (`Library.cs`, resolved in `Content.cs`):

- Hero classes: all 12 SRD classes plus the four originals (cartographer, hedge witch, reliquarian, warden). Name, hit
  die, key ability, role and magic come from the library; `content/game/classes.json` adds armour, weapon, skills and
  how often each turns up. Healers heal with their class's first healing spell (cure wounds, for clerics), area
  casters open with their first area spell (burning hands, thunderwave, frost snare).
- Monsters: `content/game/monsters.json` names a library creature with `ref` and overrides only what the game needs
  (the Last Stand's Grukk is scaled down). AC, HP, best attack, multiattack and XP come from the library.
- Wanderers: each site lists library habitats, and one encounter in each deck draws any library creature of those
  habitats within a challenge-rating range, so a delve can meet any of ~470 creatures.
- Loot: a cleared hoard can hold any library magic item of the site's tier (`content/game/loot.json`). Weapons and
  armour help in later fights; town-sized relics are carried home for the hall.

Live sight (`Systems/Knowledge.cs`, `Systems/Watch.cs`, numbers in `content/game/knowledge.json`):

- Scrying: pay a caster in town (any class with the library's Commissioned Scrying) to show a site live for 12 hours.
- Sending stones: a party's casters spend spell slots to report or turn back.
- Scouts: a ranger, rogue, monk or cartographer walks out and watches a site from cover for up to 8 days, paid daily.
  Harder sites spot them more often; a spotted scout runs for home or never comes back.
- Mirrors: a level 3 caster with the library's Mirror Network binds a pair; a scout hides one at a site, which then
  shows live until the site's monsters find and smash it.
- Works: the Scrying Pool (a free scrying a day, houses a Far-Seeing Basin) and the Sleepless Spire (every site within
  two days shows live, always, houses an Eye of the Sleepless Spire). Either relic carried home by a party does the same.

## The Godot client (`godot/`)

Godot 4.3 (.NET) with the C# sim as a project reference. Everything is built in code (`src/Main.cs`, `src/Panels.cs`)
and drawn procedurally (`src/Map/`), so there are no art assets beyond two OFL fonts.

- The map: the town, the wilds and the sites, with day and night. Scroll to zoom, drag to pan, double right-click to recenter.
- Fog of war (`src/Map/Fog.cs`), derived from the sim each frame: the town's own light (wider with watchtowers) is live;
  roads and sites that a party came home to describe are *scouted* as of that day and go stale after 30 days; everything
  else is *rumoured*. Parties vanish into the fog when they leave the light. A scrying shows a site live for a few hours.
  While a party is away the only lever is a sending stone (ask for word, or call them home), which costs a spell slot.
- Time: pause and 1×, 4×, 12×, 36× (space and 1 to 4), one sim step per in-game hour.
- Live sight on the map: violet rings for scryings, green for a watching scout, silver for a hidden mirror, and a golden
  boundary for the Spire's reach. Scouts are drawn on the road like parties and vanish into the fog the same way.
- Panes: Ledger, War, Council, Bounties (parties on the road, eyes in the wilds, scrying and scouting), Chronicles,
  Inspect (a site's page offers a scrying, a scout or a mirror; a hero's shows their class, spells and items).

```bash
godot --path godot                         # play (Godot 4.3 .NET, .NET 8 SDK)
godot --path godot -- --seed 5 --day 40 --autoplay --tab bounties --screenshot shot.png
godot --headless --path godot --export-release Windows build/windows/CrownAndCompass.exe
```

Options after `--`: `--seed`, `--day`, `--hour`, `--speed 0-4`, `--tab`, `--site <id>`, `--autoplay` (the stand-in mayor
plays), `--bounty <site>`, `--scry <site>`, `--scout <site>`, `--mirror <site>`, `--send`, `--recall`,
`--build palisade|tower|pool|spire`, `--relic <item-id>`, `--hero` (inspect the most storied hero), `--no-pause` (keep
time running through war news), `--screenshot <png>`, `--frames N`. For a clip, add Godot's own `--write-movie clip.avi --fixed-fps 30 --quit-after 900` before the `--`.

## Time model

Real time with pause. A client calls `Simulation.step()` once per in-game hour at whatever speed
the player picks; strategic systems (accounts, property, arrivals) run on the daily tick.
The same seed always replays the same town.

## Run it

```bash
npm ci
npm test
npm run sim -- --days 120 --seed 1          # notable events
npm run sim -- --days 30 --all --stats      # everything, with daily stats
npm run sim -- --days 120 --chronicles      # plus every expedition's Chronicle
npm run build                               # dist/crown-and-compass.html, the playable build
```

The build is one HTML file with no server. Open it directly; `?seed=N` picks a town and `?day=N`
fast-forwards. Space pauses, 1 to 4 set the speed.

`docs/sample-ledger.txt` is a 200-day run, through the Last Stand, that you can read without building anything.
