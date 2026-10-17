# Adventures Rest

- Design doc (Claude Doc): https://claude.ai/code/artifact/dff8e88a-ef18-4a41-8715-186a982a81c3
- Plan: simulation core first (this repo, C#/.NET 8, headless), then a Godot 4 (.NET) client that renders it.
- Decided: real time with pause on top of a daily strategic tick.

## Working here

- Run `./scripts/setup.sh` first in a fresh container (installs .NET 8 via apt; Microsoft's download host may be blocked).
- `dotnet test` must pass. Determinism matters: every random call goes through `Town.Rng`, and iteration
  over agents/buildings is in id order. Never iterate a `Dictionary` to make decisions.
- Judge sim changes by reading the Ledger across several seeds
  (`dotnet run --project src/CrownAndCompass.Cli -- --days 120 --seed N`), not one run.
- Gameplay numbers live in `src/CrownAndCompass.Sim/Content/*.json` where possible.
