# Adventures Rest

A D&D-themed city builder where you govern a frontier town as its mayor while a living campaign
plays out around you. (The design doc's working title is *Crown & Compass*; the code keeps that
namespace for now.)

## What's here

The **simulation core**: a plain C# library that runs the town headless, with no engine attached.
The Godot client will sit on top of it and only draw what it does.

- `src/CrownAndCompass.Sim`: the simulation
  - `Core/`: seeded RNG and dice, time, the Ledger (town chronicle), data-driven content
  - `Agents/`: townsfolk with D&D ability scores, traits, needs, money, ambitions and opinions of each other
  - `World/`: lots, buildings and the market
  - `Systems/`: hourly utility AI (`Brain`), production and wages (`Economy`), buying land and
    building (`Property`), tavern life (`Social`), arrivals and departures (`Population`), and a
    stand-in `Mayor` that zones land while there is no player
  - `Content/*.json`: buildings, races and names (data, so it can be modded)
- `src/CrownAndCompass.Cli`: headless runner that prints the Ledger
- `tests/`: xUnit tests (determinism, growth, invariants, dice, market)

## Time model

Real time with pause. A client calls `Simulation.Step()` once per in-game hour at whatever speed
the player picks; strategic systems (accounts, property, arrivals) run on the daily tick.
The same seed always replays the same town.

## Run it

```bash
./scripts/setup.sh                      # installs the .NET 8 SDK if missing (Ubuntu)
dotnet test
dotnet run --project src/CrownAndCompass.Cli -- --days 120 --seed 1          # notable events
dotnet run --project src/CrownAndCompass.Cli -- --days 30 --all --stats      # everything, daily stats
```

`docs/sample-ledger.txt` is a 120-day run you can read without building anything.
