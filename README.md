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
- `content/game/*.json`: buildings, races, names, hero classes, monsters, sites, the council and the enemy
  (data, so it can be modded; shared by the TypeScript and C# sims)
- `src/client`: the browser game. A Canvas 2D map (`map.ts`) and the HTML panels (`main.ts`,
  `index.html`): time controls, the Ledger, the bounty board, Chronicles and an inspector
- `scripts/build-client.ts`: bundles the client into one self-contained HTML file
- `src/cli.ts`: headless runner that prints the Ledger
- `tests/`: determinism, growth, invariants, dice and market

## The C# sim (`dotnet/`)

The game is moving to Godot 4 with C#. `dotnet/` holds a .NET 8 port of the simulation with no Godot
dependency, so a Godot client can reference it directly:

- `dotnet/src/CrownAndCompass.Sim`: the sim, system by system the same shape as `src/sim` (`Systems/Brain.cs`,
  `Economy.cs`, `Quests.cs`, `Threat.cs`, ...). It embeds `content/game/*.json` at build time.
- `dotnet/src/CrownAndCompass.Cli`: the headless runner, same flags and output as `npm run sim`
- `dotnet/tests/CrownAndCompass.Sim.Tests`: xUnit, every TypeScript test ported, plus checks of the JS
  number semantics the port relies on

The TypeScript sim stays as the reference until the Godot client replaces the web build. For any seed and
day count both CLIs print a byte-identical Ledger; `scripts/parity.sh` diffs them (seeds 1 to 8, 200 days, in
default and full `--all --stats --chronicles` output) and runs in CI.

```bash
dotnet test dotnet/CrownAndCompass.sln
dotnet run --project dotnet/src/CrownAndCompass.Cli -c Release -- --days 120 --seed 1
scripts/parity.sh                      # or: scripts/parity.sh 300 9 10 11
```

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
