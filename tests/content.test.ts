import { describe, expect, it } from 'vitest';
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

type Entry = Record<string, any> & { id: string; name: string; source: string };

const ROOT = 'content';
const sources: Record<string, { license: string }> = JSON.parse(readFileSync(join(ROOT, 'sources.json'), 'utf8'));

/** Every entry file in the library, by kind (the file name without .json). */
function library(): Map<string, { file: string; entries: Entry[] }[]> {
  const dirs = [join(ROOT, 'srd'), join(ROOT, 'original'), ...readdirSync(join(ROOT, 'open')).map((d) => join(ROOT, 'open', d))];
  const kinds = new Map<string, { file: string; entries: Entry[] }[]>();
  for (const dir of dirs) {
    for (const f of readdirSync(dir).filter((f) => f.endsWith('.json')).sort()) {
      const kind = f.replace(/\.json$/, '');
      if (!kinds.has(kind)) kinds.set(kind, []);
      kinds.get(kind)!.push({ file: join(dir, f), entries: JSON.parse(readFileSync(join(dir, f), 'utf8')) });
    }
  }
  return kinds;
}

const lib = library();
const all = (kind: string) => (lib.get(kind) ?? []).flatMap((f) => f.entries);

describe('content library', () => {
  it('has the SRD core', () => {
    expect(all('classes').filter((c) => c.source === 'srd-2024')).toHaveLength(12);
    expect(all('spells').filter((s) => s.source.startsWith('srd')).length).toBeGreaterThan(300);
    expect(all('monsters').filter((m) => m.source.startsWith('srd')).length).toBeGreaterThan(300);
  });

  it('gives every entry an id, a name and a known, licensed source', () => {
    for (const [, files] of lib) {
      for (const { file, entries } of files) {
        for (const e of entries) {
          expect(e.id, file).toMatch(/^[a-z0-9]+(-[a-z0-9]+)*$/);
          expect(e.name, `${file} ${e.id}`).toBeTruthy();
          expect(sources[e.source], `${file} ${e.id} source ${e.source}`).toBeDefined();
          expect(sources[e.source].license).toBeTruthy();
        }
      }
    }
  });

  it('keeps ids unique within a kind across every book, and files sorted by id', () => {
    for (const [kind, files] of lib) {
      const seen = new Map<string, string>();
      for (const { file, entries } of files) {
        const ids = entries.map((e) => e.id);
        expect(ids, file).toEqual([...ids].sort());
        for (const id of ids) {
          expect(seen.get(id), `${kind} ${id} in ${file}`).toBeUndefined();
          seen.set(id, file);
        }
      }
    }
  });

  it('favours SRD 5.2 and keeps 5.1 only for what 5.2 lacks', () => {
    const fireball = all('spells').find((s) => s.id === 'fireball')!;
    expect(fireball.source).toBe('srd-2024');
    expect(fireball.alsoIn).toContain('srd-2014');
    expect(all('species').find((s) => s.id === 'half-orc')?.source).toBe('srd-2014');
    expect(all('monsters').find((m) => m.id === 'goblin')).toBeUndefined(); // folded into Goblin Warrior
  });

  it('links subclasses and subspecies to something that exists', () => {
    const classes = new Set(all('classes').map((c) => c.id));
    for (const s of all('subclasses')) expect(classes.has(s.classId), `${s.id} -> ${s.classId}`).toBe(true);
    const species = new Set(all('species').map((s) => s.id));
    for (const s of all('species').filter((s) => s.parent)) expect(species.has(s.parent), `${s.id} -> ${s.parent}`).toBe(true);
  });

  it('gives spells, monsters, items and classes real-time hints', () => {
    for (const s of all('spells')) {
      expect(s.rt.castSeconds, s.id).toBeGreaterThanOrEqual(0);
      expect(s.rt.roles.length, s.id).toBeGreaterThan(0);
    }
    for (const m of all('monsters')) {
      expect(m.rt.tier, m.id).toBeGreaterThanOrEqual(0);
      expect(['solo', 'artillery', 'controller', 'skirmisher', 'brute', 'soldier']).toContain(m.rt.role);
    }
    for (const i of [...all('equipment'), ...all('magic-items')]) expect(i.rt.slot, i.id).toBeTruthy();
    for (const c of all('classes')) expect(c.rt.role, c.id).toBeTruthy();
  });

  it('credits every source it uses and holds nothing under the OGL', () => {
    const attribution = readFileSync(join(ROOT, 'ATTRIBUTION.md'), 'utf8');
    const used = new Set([...lib.values()].flat().flatMap((f) => f.entries.map((e) => e.source)));
    for (const s of used) if (s !== 'crown-and-compass') expect(attribution, s).toContain(s);
    for (const s of used) expect(['CC-BY-4.0', 'proprietary'], s).toContain(sources[s].license);
    expect(attribution).not.toMatch(/Open Game License/);
  });
});
