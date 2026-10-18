// Frees space in the project's shared folder: stale staging files, old build zips, exact duplicates and screenshots a
// newer shot of the same screen has replaced. Prints what it would delete; with --apply it first appends that list to
// cleanup-log.md in the folder, then deletes.
//
//   node scripts/cleanup.ts [--apply] [--root /mnt/project-files]
//
// Never touched: local-build-sync/ (the auto-update thread's), uploads/ (people's files), .notes/, the three current
// game zips, BUILD.md and the log itself.

import { createHash } from "node:crypto";
import { appendFileSync, existsSync, readdirSync, readFileSync, rmSync, statSync } from "node:fs";
import { basename, extname, join, relative } from "node:path";

const args = process.argv.slice(2);
const apply = args.includes("--apply");
const rootArg = args.indexOf("--root");
const root = rootArg >= 0 ? args[rootArg + 1] : "/mnt/project-files";

const protectedDirs = ["local-build-sync", "uploads", ".notes"];
const keep = new Set(["cleanup-log.md", "godot/BUILD.md", "godot/CrownAndCompass-linux.zip", "godot/CrownAndCompass-windows.zip", "godot/CrownAndCompass-mac.zip"]);
const shots = new Set([".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4", ".webm"]);
/** Left behind by an interrupted copy, export or publish. */
const staging = (rel: string) => /(^|\/)\.[^/]*\.part$|\.part$|\.tmp$|\.partial$|(^|\/)\.staging[^/]*\//.test(rel) || rel.startsWith("godot/build/");

type File = { rel: string; size: number; mtime: number };

function walk(dir: string, out: File[] = []): File[] {
  for (const name of readdirSync(dir)) {
    const path = join(dir, name);
    const rel = relative(root, path);
    if (protectedDirs.some((p) => rel === p || rel.startsWith(p + "/"))) continue;
    const st = statSync(path);
    if (st.isDirectory()) walk(path, out);
    else if (st.isFile()) out.push({ rel, size: st.size, mtime: st.mtimeMs });
  }
  return out;
}

const files = walk(root).filter((f) => !keep.has(f.rel));
const doomed = new Map<string, string>();
const mark = (f: File, why: string) => doomed.has(f.rel) || doomed.set(f.rel, why);

for (const f of files) {
  if (staging(f.rel)) mark(f, "stale staging file");
  else if (f.rel.startsWith("godot/") && extname(f.rel) === ".zip") mark(f, "old build zip (the three current zips are kept)");
}

// Exact duplicates: same size, then same contents. The newest copy stays.
const bySize = Map.groupBy(files.filter((f) => f.size > 0 && !doomed.has(f.rel)), (f) => f.size);
for (const group of bySize.values()) {
  if (group.length < 2) continue;
  const byHash = Map.groupBy(group, (f) => createHash("sha256").update(readFileSync(join(root, f.rel))).digest("hex"));
  for (const same of byHash.values()) {
    const [newest, ...older] = same.sort((a, b) => b.mtime - a.mtime || a.rel.localeCompare(b.rel));
    for (const f of older) mark(f, `duplicate of ${newest.rel}`);
  }
}

// A screenshot or clip with the same name in more than one feature pass shows the same screen: the newest stays.
const byName = Map.groupBy(files.filter((f) => shots.has(extname(f.rel).toLowerCase()) && !doomed.has(f.rel)), (f) => basename(f.rel));
for (const group of byName.values()) {
  if (group.length < 2) continue;
  const [newest, ...older] = group.sort((a, b) => b.mtime - a.mtime || a.rel.localeCompare(b.rel));
  for (const f of older) mark(f, `older shot of the same screen as ${newest.rel}`);
}

const list = files.filter((f) => doomed.has(f.rel)).sort((a, b) => a.rel.localeCompare(b.rel));
const mb = (n: number) => `${(n / 1048576).toFixed(1)} MB`;
const freed = list.reduce((n, f) => n + f.size, 0);
const lines = list.map((f) => `- ${f.rel} (${mb(f.size)}): ${doomed.get(f.rel)}`);
console.log(lines.length ? lines.join("\n") : "Nothing to delete.");
console.log(`${list.length} files, ${mb(freed)}${apply ? "" : " (dry run; --apply deletes)"}`);

if (apply && list.length) {
  const log = join(root, "cleanup-log.md");
  if (!existsSync(log)) appendFileSync(log, "# Shared folder cleanup log\n\nWritten by scripts/cleanup.ts before it deletes anything.\n");
  appendFileSync(log, `\n## ${new Date().toISOString()}: ${list.length} files, ${mb(freed)}\n\n${lines.join("\n")}\n`);
  for (const f of list) rmSync(join(root, f.rel), { force: true });
  // Folders the deletions emptied go too, outside the protected ones.
  for (const dir of new Set(list.map((f) => join(root, f.rel, "..")))) {
    try {
      if (dir !== root && readdirSync(dir).length === 0) rmSync(dir, { recursive: true });
    } catch {}
  }
}
