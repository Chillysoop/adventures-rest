// The merge gate: proves the game still works with a change merged in, before it merges and before a build is
// published. CI runs it on every pull request (merged onto the latest main) and on main; threads run it with
// --publish to put new builds in the project's game folder.
//
//   node scripts/gate.ts [--sim] [--client] [--old-save REF] [--rebaseline] [--publish DIR]
//
//   --sim         build, unit tests, a campaign sweep against dotnet/gate-baseline.json, save/load replay
//   --client      export the Linux build and play it through from the main menu (Godot's --smoke)
//   --old-save R  a save written by commit R (usually the latest main) must still load and play on
//   --rebaseline  write the sweep's results as the new baseline (for a PR that changes balance on purpose)
//   --publish D   everything above against origin/main, then export all three builds into D as the game zips
//
// With neither --sim nor --client, both run. Godot comes from $GODOT or `godot` on the PATH.

import { execFileSync, spawn } from "node:child_process";
import { cpus, tmpdir } from "node:os";
import { appendFileSync, copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { join, resolve } from "node:path";

const root = resolve(import.meta.dirname, "..");
const baselinePath = join(root, "dotnet/gate-baseline.json");
const cliDll = (dir: string) => join(dir, "dotnet/src/CrownAndCompass.Cli/bin/Release/net8.0/CrownAndCompass.Cli.dll");
const godot = process.env.GODOT ?? "godot";

const args = process.argv.slice(2);
const flag = (name: string) => args.includes(name);
const value = (name: string) => {
  const i = args.indexOf(name);
  return i >= 0 && i + 1 < args.length ? args[i + 1] : undefined;
};
const publishTo = value("--publish");
let runSim = flag("--sim") || !!publishTo;
let runClient = flag("--client") || !!publishTo;
if (!runSim && !runClient) runSim = runClient = true;
// Publishing checks saves from the build players have now, named in the game folder's BUILD.md.
const published = publishTo && existsSync(join(publishTo, "BUILD.md")) ? /Commit ([0-9a-f]{40})/.exec(readFileSync(join(publishTo, "BUILD.md"), "utf8"))?.[1] : undefined;
const oldSaveRef = value("--old-save") ?? (publishTo ? published ?? "HEAD~1" : undefined);

type Summary = { seed: number; outcome: "won" | "lost" | "unfinished"; day: number; population: number; treasury: number; ledger: number; chronicles: number };
type Baseline = { days: number; seeds: number[]; runs: Summary[] };

const report: string[] = [];
let failed = false;

function note(line: string) {
  console.log(line);
  report.push(line);
}

function fail(step: string, why: string) {
  failed = true;
  note(`FAIL ${step}: ${why}`);
}

function sh(cmd: string, argv: string[], opts: { cwd?: string; quiet?: boolean } = {}) {
  console.log(`$ ${cmd} ${argv.join(" ")}`);
  return execFileSync(cmd, argv, { cwd: opts.cwd ?? root, stdio: opts.quiet ? "pipe" : "inherit", encoding: "utf8", maxBuffer: 1 << 28 });
}

/** Run a command, never throwing: its exit code and everything it printed. */
function run(cmd: string, argv: string[], opts: { cwd?: string; timeoutMs?: number } = {}): Promise<{ code: number; out: string }> {
  return new Promise((done) => {
    const child = spawn(cmd, argv, { cwd: opts.cwd ?? root });
    let out = "";
    child.stdout.on("data", (d) => (out += d));
    child.stderr.on("data", (d) => (out += d));
    const timer = opts.timeoutMs ? setTimeout(() => {
      out += `\n[gate] timed out after ${opts.timeoutMs! / 1000}s\n`;
      child.kill("SIGKILL");
    }, opts.timeoutMs) : undefined;
    child.on("error", (e) => (out += `\n[gate] ${e.message}\n`));
    child.on("close", (code) => {
      clearTimeout(timer);
      done({ code: code ?? 1, out });
    });
  });
}

/** Run jobs with at most `width` at once, keeping their order. */
async function pool<T, R>(items: T[], width: number, job: (item: T) => Promise<R>): Promise<R[]> {
  const results: R[] = new Array(items.length);
  let next = 0;
  const lanes = Array.from({ length: Math.min(width, items.length) }, async () => {
    while (next < items.length) {
      const i = next++;
      results[i] = await job(items[i]);
    }
  });
  await Promise.all(lanes);
  return results;
}

async function campaign(dll: string, argv: string[], cwd = root): Promise<Summary | string> {
  const r = await run("dotnet", [dll, ...argv, "--summary"], { cwd, timeoutMs: 10 * 60_000 });
  const line = r.out.trim().split("\n").at(-1) ?? "";
  if (r.code !== 0 || !line.startsWith("{")) return `exit ${r.code}: ${r.out.trim().split("\n").slice(-12).join("\n")}`;
  return JSON.parse(line) as Summary;
}

const median = (xs: number[]) => {
  const s = [...xs].sort((a, b) => a - b);
  return s.length === 0 ? 0 : s.length % 2 ? s[(s.length - 1) / 2] : (s[s.length / 2 - 1] + s[s.length / 2]) / 2;
};

function spread(runs: Summary[]) {
  const ended = runs.filter((r) => r.outcome !== "unfinished");
  return {
    won: runs.filter((r) => r.outcome === "won").length,
    lost: runs.filter((r) => r.outcome === "lost").length,
    unfinished: runs.length - ended.length,
    medianEnd: median(ended.map((r) => r.day)),
    earliestEnd: ended.length ? Math.min(...ended.map((r) => r.day)) : 0,
  };
}

/** The sweep: every baseline seed played to the end (or the day limit), compared with the baseline's spread. */
async function sweep() {
  const baseline = JSON.parse(readFileSync(baselinePath, "utf8")) as Baseline;
  const width = Math.max(1, cpus().length);
  const results = await pool(baseline.seeds, width, (seed) => campaign(cliDll(root), ["--days", String(baseline.days), "--seed", String(seed)]));
  const runs: Summary[] = [];
  results.forEach((r, i) => (typeof r === "string" ? fail("sweep", `seed ${baseline.seeds[i]} crashed (${r})`) : runs.push(r)));
  if (runs.length < results.length) return;

  if (flag("--rebaseline")) {
    writeFileSync(baselinePath, JSON.stringify({ ...baseline, runs }, null, 2) + "\n");
    note(`Rebaselined ${baselinePath}: ${JSON.stringify(spread(runs))}`);
    return;
  }
  const now = spread(runs), was = spread(baseline.runs);
  note(`Sweep of ${runs.length} campaigns, ${baseline.days} days: won ${now.won} (baseline ${was.won}), lost ${now.lost} (${was.lost}), unfinished ${now.unfinished} (${was.unfinished}), median end day ${now.medianEnd} (${was.medianEnd}), earliest end ${now.earliestEnd} (${was.earliestEnd})`);
  for (const r of runs) note(`  seed ${r.seed}: ${r.outcome} on day ${r.day}, ${r.population} people, ${r.ledger} ledger entries`);
  // Limits wide enough for a change that shifts single seeds, narrow enough to catch a campaign that broke.
  const allowed = Math.max(2, Math.round(runs.length / 4));
  if (now.won < was.won - allowed) fail("sweep", `wins fell from ${was.won} to ${now.won} of ${runs.length}`);
  if (now.lost > was.lost + allowed) fail("sweep", `defeats rose from ${was.lost} to ${now.lost} of ${runs.length}`);
  if (was.medianEnd && now.medianEnd && Math.abs(now.medianEnd - was.medianEnd) > 0.2 * was.medianEnd)
    fail("sweep", `median campaign length moved from day ${was.medianEnd} to day ${now.medianEnd} (more than 20%)`);
  if (was.earliestEnd && now.earliestEnd && now.earliestEnd < 0.6 * was.earliestEnd)
    fail("sweep", `a campaign ended on day ${now.earliestEnd}, far sooner than the baseline's earliest (day ${was.earliestEnd})`);
  if (now.unfinished === runs.length) fail("sweep", "no campaign reached an ending");
}

/** A campaign saved part way and loaded again must end exactly as the one played straight through. */
async function replay() {
  const dir = mkdtempSync(join(tmpdir(), "gate-replay-"));
  for (const seed of [2, 5]) {
    const save = join(dir, `seed${seed}.save`);
    const [straight, first] = await Promise.all([
      campaign(cliDll(root), ["--days", "400", "--seed", String(seed)]),
      campaign(cliDll(root), ["--days", "250", "--seed", String(seed), "--save", save]),
    ]);
    if (typeof straight === "string" || typeof first === "string") {
      fail("replay", `seed ${seed} crashed (${typeof straight === "string" ? straight : first})`);
      continue;
    }
    const reloaded = await campaign(cliDll(root), ["--load", save, "--days", "150"]);
    if (typeof reloaded === "string") fail("replay", `seed ${seed} save from day 250 did not load (${reloaded})`);
    else if (JSON.stringify(reloaded) !== JSON.stringify(straight)) fail("replay", `seed ${seed} played on from a save ends differently: ${JSON.stringify(reloaded)} vs ${JSON.stringify(straight)}`);
    else note(`Save at day 250 and reload replays seed ${seed} exactly (day ${straight.day}, ${straight.outcome})`);
  }
  rmSync(dir, { recursive: true, force: true });
}

const saveVersion = (dir: string) => Number(/const int Version = (\d+);/.exec(readFileSync(join(dir, "dotnet/src/CrownAndCompass.Sim/Save.cs"), "utf8"))?.[1]);

/** A save written by an earlier commit (the build players have) must load and play on, unless the save version was bumped. */
async function oldSave(ref: string) {
  const dir = mkdtempSync(join(tmpdir(), "gate-old-"));
  try {
    sh("git", ["worktree", "add", "--detach", dir, ref], { quiet: true });
    sh("dotnet", ["build", "dotnet/src/CrownAndCompass.Cli", "-c", "Release", "--nologo", "-v", "q"], { cwd: dir, quiet: true });
    const save = join(dir, "old.save");
    // Older commits' CLIs know no --summary, so this one only has to write the save.
    const written = await run("dotnet", [cliDll(dir), "--days", "200", "--seed", "7", "--save", save], { cwd: dir, timeoutMs: 10 * 60_000 });
    if (written.code !== 0 || !existsSync(save)) {
      note(`Skipped the old-save check: ${ref} could not write a save (${written.out.trim().split("\n").at(-1)})`);
      return;
    }
    const loaded = await campaign(cliDll(root), ["--load", save, "--days", "120"]);
    const was = saveVersion(dir), now = saveVersion(root);
    if (typeof loaded !== "string") note(`A day-200 save from ${ref} loads and plays on to day ${loaded.day}`);
    else if (now > was && loaded.includes("older version")) note(`Saves from ${ref} are refused on purpose (save version ${was} → ${now})`);
    else fail("old save", `a day-200 save from ${ref} (save version ${was}) does not load or play on: ${loaded}. Fix the break, or bump SaveGame.Version if old saves cannot be kept.`);
  } finally {
    sh("git", ["worktree", "remove", "--force", dir], { quiet: true });
  }
}

/** Engine lines that mean the build is broken (shader, script or resource errors); audio and vsync noise is not. */
const brokenLine = /^(ERROR|SCRIPT ERROR|USER ERROR):|Unhandled exception|Cannot instantiate C# script/m;

function exportBuild(preset: string, out: string) {
  mkdirSync(join(out, ".."), { recursive: true });
  sh(godot, ["--headless", "--path", "godot", "--export-release", preset, out]);
  if (!existsSync(out)) fail("export", `Godot did not write the ${preset} build`);
}

/** Export the Linux build and play it from the main menu: new town, every window, save and load, tutorial. */
async function client(outDir: string) {
  sh("dotnet", ["build", "godot/CrownAndCompass.csproj", "--nologo", "-v", "q"]);
  // The first import of a fresh checkout reports missing imports while it makes them; that pass is not judged.
  await run(godot, ["--headless", "--path", "godot", "--import"], { timeoutMs: 10 * 60_000 });
  const bin = join(outDir, "linux/CrownAndCompass.x86_64");
  exportBuild("Linux", bin);
  if (failed) return;
  // A software-rendered smoke run on a small container can take longer than CI's: GATE_SMOKE_MINUTES raises the limit.
  const smokeMs = Number(process.env.GATE_SMOKE_MINUTES ?? 15) * 60_000;
  const game = ["--rendering-driver", "opengl3", "--audio-driver", "Dummy", "--resolution", "960x540", "--", "--smoke", "--seed", "3"];
  const hasX = await run("sh", ["-c", "command -v xvfb-run"]);
  // The play-through grows with every window and system; on a software-rendered runner it takes 13 minutes or more.
  const smokeMs = 25 * 60_000;
  const r = hasX.code === 0
    ? await run("xvfb-run", ["-a", bin, ...game], { timeoutMs: smokeMs })
    : await run(bin, ["--headless", ...game.slice(4)], { timeoutMs: smokeMs });
  const ok = /^SMOKE OK.*$/m.exec(r.out);
  const bad = r.out.split("\n").filter((l) => brokenLine.test(l) && !/audio|ALSA|PulseAudio/i.test(l));
  if (r.code !== 0 || !ok || bad.length) {
    console.log(r.out);
    fail("client", `the exported game ${r.code !== 0 ? `quit with ${r.code}` : !ok ? "never finished its play-through" : "logged errors"}: ${bad.slice(0, 5).join(" | ") || r.out.trim().split("\n").slice(-5).join(" | ")}`);
  } else note(`Exported Linux build plays through from the main menu: ${ok[0]}`);
}

/** Zip a build folder into the game folder through a staging name, so a half-written zip is never in place. */
function publish(outDir: string, dest: string, sha: string) {
  exportBuild("Windows", join(outDir, "windows/CrownAndCompass.exe"));
  exportBuild("macOS", join(outDir, "macos/CrownAndCompass.zip"));
  if (failed) return;
  mkdirSync(dest, { recursive: true });
  const staged: [string, string][] = [];
  for (const [name, folder] of [["linux", "linux"], ["windows", "windows"]] as const) {
    const zip = join(outDir, `CrownAndCompass-${name}.zip`);
    rmSync(zip, { force: true });
    sh("zip", ["-qr", zip, "."], { cwd: join(outDir, folder) });
    staged.push([zip, `CrownAndCompass-${name}.zip`]);
  }
  staged.push([join(outDir, "macos/CrownAndCompass.zip"), "CrownAndCompass-mac.zip"]);
  for (const [from, name] of staged) {
    const tmp = join(dest, `.${name}.part`);
    copyFileSync(from, tmp);
    renameSync(tmp, join(dest, name));
  }
  writeFileSync(join(dest, "BUILD.md"), `# Current game build\n\nCommit ${sha} of main, published ${new Date().toISOString()} after the merge gate passed:\n\n${report.map((l) => `- ${l.trim()}`).join("\n")}\n`);
  note(`Published the three builds of ${sha.slice(0, 7)} to ${dest}`);
}

async function main() {
  const started = Date.now();
  let sha = sh("git", ["rev-parse", "HEAD"], { quiet: true }).trim();
  if (publishTo) {
    // Only main, as it stands on GitHub, gets published.
    sh("git", ["fetch", "origin", "main"], { quiet: true });
    const main = sh("git", ["rev-parse", "origin/main"], { quiet: true }).trim();
    const dirty = sh("git", ["status", "--porcelain", "--untracked-files=no"], { quiet: true }).trim();
    if (sha !== main || dirty) {
      console.error(`--publish needs a clean checkout of origin/main (${main.slice(0, 7)}); this is ${sha.slice(0, 7)}${dirty ? " with changes" : ""}.`);
      process.exit(2);
    }
    sha = main;
  }
  const outDir = join(root, "godot/build/gate");
  rmSync(outDir, { recursive: true, force: true });
  if (runSim) {
    sh("dotnet", ["build", "dotnet/CrownAndCompass.sln", "-c", "Release", "--nologo", "-v", "q"]);
    try {
      sh("dotnet", ["test", "dotnet/CrownAndCompass.sln", "-c", "Release", "--no-build", "--nologo", "-v", "q"]);
      note("Unit tests pass");
    } catch {
      fail("tests", "dotnet test failed (see the log above)");
    }
    await Promise.all([sweep(), replay()]);
    if (oldSaveRef) await oldSave(oldSaveRef);
  }
  if (runClient && !failed) await client(outDir);
  if (publishTo && !failed) publish(outDir, publishTo, sha);

  note(failed ? `GATE FAILED (${Math.round((Date.now() - started) / 1000)}s)` : `GATE PASSED (${Math.round((Date.now() - started) / 1000)}s)`);
  if (process.env.GITHUB_STEP_SUMMARY) appendFileSync(process.env.GITHUB_STEP_SUMMARY, `## Merge gate\n\n${report.map((l) => `    ${l}`).join("\n")}\n`);
  process.exit(failed ? 1 : 0);
}

await main();
