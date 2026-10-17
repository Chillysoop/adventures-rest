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

Rumours (`Systems/Rumors.cs`, `content/game/rumors.json`), GDD rules W3 and C9:

- The town starts knowing only the local menace. Each evening's talk at each tavern can turn up a rumour: a new place,
  who holds it, how dangerous it is, or that a cleared ruin has been taken again. Every rumour names its teller (a
  traveller, a townsperson or a hero) and how far they can be trusted, and some are false.
- The town acts on what it believes (bounties, heroes sizing up a job) until a party reports or the site is seen live;
  then the Ledger says which tale was wrong. `Rumors.Plant` lets other systems (factions) plant rumours of their own.

Shrines, the store and the temple (`Systems/Shrines.cs`, `content/game/shrines.json`), GDD rules W8, C1, C2, W9:

- The charter brings a shrine and a general store. Townsfolk pray at the bells, leave offerings and take vows; heroes
  rest better near the shrine and buy healing potions at the store, which they drink in the delve.
- At 40 residents the council can endow a temple: it tithes the devout, keeps a holy day, posts bounties from its own
  coffers on undead and schemer dens, heals heroes fully for a fee, and raises a fallen hero for 250 gold.

The pantheon (`Systems/Faith.cs`, `content/original/deities.json`, `content/game/faith.json`), GDD rule W8:

- Thirteen original gods on the D&D domain archetypes (light, forge, nature, war, law, death, trickery, trade, storm,
  knowledge, and three evil gods only cults serve). Newcomers lean to a god by race, trade, class and temperament;
  clerics and paladins arrive sworn. The charter shrine is the Lantern Mother's, the temple goes to the largest
  congregation, and any other god with six followers pools coin for a shrine of its own (up to four).
- Each god keeps its holy day at its own house: the faithful gather, offer, take vows and quarrel with rival faiths; a
  lawful or good temple preaches against the secret faction its god hates; temples crusade against the site kinds their
  god hates. Heroes kneel at their god's altar before they march (+1 to hit for the expedition).
- Devotion builds divine favour, and a god acts when it brims over (rarer each time): healing light, a ripened harvest,
  ringing forges, a war banner that steadies the walls, a law god's scales that expose the guilty, the Ferryman's
  lanterns that put the dead back to sleep, a free scrying, a lucky purse, a masterless caravan, a storm on the warband.

Factions (`Systems/Factions.cs`, `content/original/factions.json`, `content/game/factions.json`), GDD rules C10, C11:

- Seven factions take root as the town grows and enough townsfolk fit them: the Quiet Hand (thieves' guild), the Gilded
  Scale (merchants), the Compass Lodge (adventurers), the Order of the Unshut Book (arcane), the Wardens of the Dawn
  Gate (knights), the Thornward Circle (druids) and the Hollow Choir (death cult). Five more are written for later.
- Members pay dues and recruit; open factions build a hall and fund contracts from it. Each day a faction may act on
  its agenda: burgle, run a racket, smuggle in what the market is short of, sell a true tip about a site, send a
  caravan, post a reward for thieves, fund a bounty, send for a hero, sponsor a scrying, patrol, hunt cults, bless the
  fields, block the lumber road, hold a rite or rob graves. Left alone, the Hollow Choir wakes a crypt of the dead.
- Secret factions gather heat from their crimes, sermons and the knights' hunting until the watch exposes them. The
  mayor's levers: `CrackDown` an exposed faction (leader run out, members fined, coffers seized) or `SupportFaction`
  with treasury gold. A thieves' guild member in the captain's seat weakens the watch; knights strengthen it and the walls.

Lairs (`Systems/Lairs.cs`, `content/game/lairs.json`), GDD rules E4, E8, E9:

- Every site grows off-screen at its kind's pace; a strong lair puts more creatures in every fight but the boss's, and
  what a party kills thins it. Once bold, it acts by its kind: beasts hunt the farms, a tribe raids or demands tribute,
  the dead walk into the fields (and kill, unless the shrine or temple turns them), schemers take a resident in thrall
  who quietly steals from the treasury, rivals toll the merchants' carts. Anything that comes out of an unheard-of place
  puts it on the map.
- Raiders, the warband's and the tribes', pick their night: they hang back a few days while the heroes are home and
  come early once most of them have marched out.
- A cleared site is resettled after 25 to 45 days by a library creature of its country, sometimes a tier worse. The
  town only learns of it from rumour or sight, unless it holds the site with an outpost (150 gold, 2 a day).

## The Godot client (`godot/`)

Godot 4.3 (.NET, Compatibility renderer) with the C# sim as a project reference. The UI is built in code
(`src/Main.cs`, `src/Panels.cs`); the map (`src/Map/`) is a 3D world in the spirit of Northgard, scaled for a map that
takes many hours to explore. Art credits are in [`godot/assets/CREDITS.md`](godot/assets/CREDITS.md).

- The map: one continuous 1024×1024-tile world (1 tile = 1 lot, about 44 tiles per day of march) generated from the
  seed (`WorldGen.cs`): hills, mountain chains, a river past the town, biomes and forests, cut into Northgard-style
  territories. Terrain is 16×16 chunks with three levels of detail; trees and ground clutter are instanced per chunk and
  give way to painted canopy from far off. Buildings mix Kenney's CC0 kits (baked into `assets/models/kenney.bin` by
  `tools/kenney_bake.py`) with generated parts (`MeshKit.cs`), all in one vertex-coloured batch per kind.
- Camera: scroll to zoom from street level to the whole map, drag to pan, Q/E to rotate, double right-click to recenter.
  M switches to the strategic view: the same land as an inked campaign map, where markers can be placed.
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
time running through war news), `--screenshot <png>`, `--frames N`, `--cam <distance>` (with `--look x,y` or `--site`
to aim it), `--flyout` (for clips: pull back from the town to the whole map), `--yaw <quarter turns>`, `--strategic`, `--trees <density>` (stress test) and `--bench` (prints frame time,
map CPU time, draw calls and triangles, then quits). For a clip, add Godot's own `--write-movie clip.avi --fixed-fps 30 --quit-after 900` before the `--`.

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
