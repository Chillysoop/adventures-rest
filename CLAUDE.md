# Adventures Rest

- Design doc (Claude Doc): https://claude.ai/code/artifact/dff8e88a-ef18-4a41-8715-186a982a81c3
- Stack: TypeScript, web-first. Simulation in `src/sim` (no DOM), browser client to come; builds are published
  so they can be played from the Claude project. The owner never opens GitHub, an engine or any other tool:
  show progress as summaries, Ledgers, screenshots and playable builds.
- Decided: real time with pause on top of a daily strategic tick.
- Priorities: low-cost scalability, efficiency, speed, quality, style.

## Working here

- Node 22.18+ runs `.ts` directly (type stripping), so only erasable TypeScript: no `enum`, no
  `namespace`, no constructor parameter properties. Import with explicit `.ts` extensions.
- `npm run typecheck` and `npm test` must pass.
- Determinism matters: every random call goes through `town.rng`, and decisions iterate agents and
  buildings in id order. Sorts must break ties by id.
- Judge sim changes by reading the Ledger across several seeds (`npm run sim -- --days 120 --seed N`), not one run.
- Gameplay numbers live in `src/sim/content/*.json` where possible.
