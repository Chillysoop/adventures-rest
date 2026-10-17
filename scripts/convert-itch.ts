// Converts three CC-BY-4.0 packs from itch.io into Open5e-shaped fixtures under content/vendor, so the main
// importer (scripts/import-content.ts) treats them like any other third-party book. Needs `pdftotext` (poppler).
//
//   node scripts/convert-itch.ts --dir <folder with the downloads>
//
// Expected files in that folder (the free downloads from each itch.io page):
//   khyberia-srd.pdf               https://tzarfenix.itch.io/khyberia-srd (Khyberia SRD 2023-10.pdf)
//   midnight-spell-compendium.pdf  https://nocturnal-designs.itch.io/spell-compendium (V2.0 pdf, CC-BY since v3.0)
//   little-book-of-beasts.txt      https://taichara.itch.io/kristin-saint-clairs-little-book-of-beasts ([critter stats].txt)
//
// Only the CC-BY parts are taken: stat blocks and spell text. The Little Book of Beasts' art and the letter from
// "Kristin Saint-Clair" are not CC-BY and are not used; the Midnight Spell Compendium's Artificer references are
// under WotC's Fan Content Policy, not CC-BY, so Artificer is dropped from its class lists.

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

type Fields = Record<string, any>;
type Fixture = { model: string; pk: string; fields: Fields };

const args = process.argv.slice(2);
const dir = args[args.indexOf('--dir') + 1];
if (!args.includes('--dir') || !dir) {
  console.error('usage: node scripts/convert-itch.ts --dir <folder with the downloads>');
  process.exit(1);
}
const OUT = join('content', 'vendor');

const slug = (s: string) =>
  s.toLowerCase().normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/['’]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const ABILITIES = ['strength', 'dexterity', 'constitution', 'intelligence', 'wisdom', 'charisma'];
const DAMAGE = /\b(acid|bludgeoning|cold|fire|force|lightning|necrotic|piercing|poison|psychic|radiant|slashing|thunder)\b/i;

/** Reads a two-column PDF page by page, left column then right, so text comes out in reading order. */
function pdfColumns(file: string): string[] {
  const info = execFileSync('pdfinfo', [file], { encoding: 'utf8' });
  const [, w, h] = info.match(/Page size:\s+([\d.]+) x ([\d.]+)/)!.map(Number);
  const pages = Number(info.match(/Pages:\s+(\d+)/)![1]);
  const half = Math.floor(w / 2);
  const crop = (p: number, x: number, width: number) =>
    execFileSync('pdftotext', ['-f', `${p}`, '-l', `${p}`, '-x', `${x}`, '-y', '0', '-W', `${width}`, '-H', `${Math.ceil(h)}`, '-r', '72', file, '-'], { encoding: 'utf8' });
  const lines: string[] = [];
  for (let p = 1; p <= pages; p++) lines.push(...`${crop(p, 0, half)}\n${crop(p, half, Math.ceil(w) - half)}`.split('\n'));
  // Page numbers and form feeds are layout, not text.
  return lines.map((l) => l.replace(/\f/g, '').trimEnd()).filter((l) => !/^\d{1,2}$/.test(l.trim()));
}

/** Joins wrapped lines into paragraphs: blank lines and new "Name." entries start a new paragraph. */
function paragraphs(lines: string[]): string {
  const out: string[] = [];
  let cur = '';
  for (const raw of lines) {
    const l = raw.trim();
    if (!l) {
      if (cur) out.push(cur);
      cur = '';
    } else cur = !cur ? l : cur.endsWith('-') && /^[a-z]/.test(l) ? cur + l : `${cur} ${l}`;
  }
  if (cur) out.push(cur);
  return out.join('\n\n');
}

function document(pk: string, f: Fields): Fixture {
  return { model: 'api_v2.document', pk, fields: { licenses: ['cc-by-40'], gamesystem: '5e-2024', ...f } };
}

function write(publisher: string, doc: string, models: Record<string, Fixture[]>) {
  const d = join(OUT, publisher, doc);
  rmSync(d, { recursive: true, force: true });
  mkdirSync(d, { recursive: true });
  for (const [model, rows] of Object.entries(models)) {
    rows.sort((a, b) => (a.pk < b.pk ? -1 : a.pk > b.pk ? 1 : 0));
    writeFileSync(join(d, `${model}.json`), `[\n${rows.map((r) => JSON.stringify(r)).join(',\n')}\n]\n`);
  }
  console.log(`${doc}: ${Object.entries(models).map(([m, r]) => `${r.length} ${m}`).join(', ')}`);
}

// ---- creatures (shared by both bestiaries) ----

type Attack = { toHit?: number; reach?: number; range?: number; longRange?: number; count?: number; die?: number; bonus?: number; type?: string; xCount?: number; xDie?: number; xType?: string };
type Action = { name: string; kind: string; desc: string; attacks: Attack[]; usesType?: string; usesParam?: number };
type Creature = { fields: Fields; traits: { name: string; desc: string }[]; actions: Action[] };

const KIND: Record<string, string> = { action: 'ACTION', bonus: 'BONUS_ACTION', reaction: 'REACTION', legendary: 'LEGENDARY_ACTION' };

function creatureFixtures(doc: string, cs: Creature[]): Record<string, Fixture[]> {
  const models: Record<string, Fixture[]> = { Creature: [], CreatureTrait: [], CreatureAction: [], CreatureActionAttack: [] };
  for (const c of cs) {
    const pk = `${doc}_${slug(c.fields.name)}`;
    models.Creature.push({ model: 'api_v2.creature', pk, fields: { document: doc, ...c.fields } });
    c.traits.forEach((t, i) => models.CreatureTrait.push({ model: 'api_v2.creaturetrait', pk: `${pk}_${slug(t.name)}-${i}`, fields: { name: t.name, desc: t.desc, parent: pk } }));
    c.actions.forEach((a, i) => {
      const apk = `${pk}_${slug(a.name)}-${i}`;
      models.CreatureAction.push({
        model: 'api_v2.creatureaction', pk: apk,
        fields: { name: a.name, desc: a.desc, parent: pk, action_type: KIND[a.kind], order_in_statblock: i, uses_type: a.usesType ?? null, uses_param: a.usesParam ?? null },
      });
      a.attacks.forEach((t, j) => models.CreatureActionAttack.push({
        model: 'api_v2.creatureactionattack', pk: `${apk}_attack-${j}`,
        fields: {
          parent: apk, name: `${a.name} attack`, to_hit_mod: t.toHit ?? null, reach: t.reach ?? null, range: t.range ?? null, long_range: t.longRange ?? null,
          damage_die_count: t.count ?? null, damage_die_type: t.die ? `D${t.die}` : null, damage_bonus: t.bonus ?? null, damage_type: t.type ?? null,
          extra_damage_die_count: t.xCount ?? null, extra_damage_die_type: t.xDie ? `D${t.xDie}` : null, extra_damage_type: t.xType ?? null,
        },
      }));
    });
  }
  return models;
}

/** "Melee Weapon Attack: +3 to hit, reach 5 ft., one target. Hit: 4 (1d6 + 1) piercing damage plus 3 (1d6) fire damage." */
function parseAttack(desc: string): Attack | null {
  const m = desc.match(/(Melee|Ranged|Melee or Ranged) (?:Weapon|Spell) Attack:\s*\+(\d+) to hit/);
  if (!m) return null;
  const a: Attack = { toHit: Number(m[2]) };
  const reach = desc.match(/reach (\d+) ft/);
  if (reach) a.reach = Number(reach[1]);
  const range = desc.match(/range (\d+)(?:\/(\d+))? ft/);
  if (range) {
    a.range = Number(range[1]);
    if (range[2]) a.longRange = Number(range[2]);
  }
  const hit = desc.match(/Hit:\s*\d+ \((\d+)d(\d+)(?:\s*([+-])\s*(\d+))?\)\s*(\w+) damage/);
  if (hit) {
    a.count = Number(hit[1]);
    a.die = Number(hit[2]);
    if (hit[3]) a.bonus = Number(hit[4]) * (hit[3] === '-' ? -1 : 1);
    a.type = hit[5].toLowerCase();
  }
  const extra = desc.match(/plus \d+ \((\d+)d(\d+)\)\s*(\w+) damage/);
  if (extra && DAMAGE.test(extra[3])) {
    a.xCount = Number(extra[1]);
    a.xDie = Number(extra[2]);
    a.xType = extra[3].toLowerCase();
  }
  return a;
}

function usesOf(name: string): Pick<Action, 'usesType' | 'usesParam'> {
  const r = name.match(/\(Recharge (\d)/);
  if (r) return { usesType: 'RECHARGE_ON_ROLL', usesParam: Number(r[1]) };
  const d = name.match(/\((\d+)\/Day\)/i);
  if (d) return { usesType: 'PER_DAY', usesParam: Number(d[1]) };
  if (/\(Recharges after/i.test(name)) return { usesType: 'RECHARGE_AFTER_REST' };
  return {};
}

// ---- Khyberia SRD (5e stat blocks) ----

const SIZE = /^(Tiny|Small|Medium|Large|Huge|Gargantuan) /;
const HEADER = /^(Armor Class|Hit Points|Speed|Saving Throws|Skills|Damage Vulnerabilities|Damage Resistances|Damage Immunities|Condition Immunities|Senses|Languages|Challenge)\b/;
/** Lines in the Khyberia PDF that start a family's lore or a table, not part of the stat block before them. */
const KHY_BREAKS = new Set(['Frogoblins', 'Hedrons', 'Koaxians', 'Chaos Swell', 'Other Monsters']);
/** An entry starts with a short capitalised name and a full stop, e.g. "Standing Leap." or "Bite (B’lot Form Only)." */
const ENTRY = /^(?!(?:The|Her|His|Its|If|When|On|Until|Each|A|An|In|This|That|While)\b)([A-Z][A-Za-z’'-]*(?: [A-Za-z’'-]+){0,5}(?: \([^)]*\))?)\.\s/;

function khyberia(file: string): Creature[] {
  const lines = pdfColumns(file);
  const starts: number[] = [];
  for (let i = 0; i < lines.length - 1; i++) if (SIZE.test(lines[i + 1]) && lines[i].trim() && !SIZE.test(lines[i])) starts.push(i);
  const chaosSwell = khyberiaChaosSwell(lines);
  return starts.map((s, k) => {
    let end = k + 1 < starts.length ? starts[k + 1] : lines.length;
    for (let i = s + 2; i < end; i++) if (KHY_BREAKS.has(lines[i].trim())) { end = i; break; }
    const block = lines.slice(s, end);
    const name = block[0].trim();
    let i = 1;
    let typeLine = '';
    while (!HEADER.test(block[i])) typeLine += ` ${block[i++].trim()}`;
    const head: Record<string, string> = {};
    let abilities: number[] = [];
    let last = '';
    for (; i < block.length; i++) {
      const l = block[i].trim();
      if (/^(Actions|Bonus Actions|Reactions|Legendary Actions)$/.test(l) || (last === 'Challenge' && ENTRY.test(l))) break;
      const h = l.match(HEADER);
      if (h) {
        last = h[1];
        head[last] = l.slice(h[1].length).trim();
      } else if (/^(STR|DEX|CON|INT|WIS|CHA)( |$)/.test(l) || !l) continue;
      else if (/^\d+ \([+-]\d+\)/.test(l)) abilities.push(...[...l.matchAll(/(\d+) \([+-]\d+\)/g)].map((m) => Number(m[1])));
      else if (last) head[last] += ` ${l}`;
    }
    // The rest: traits, then the action sections.
    const entries: { name: string; kind: string; lines: string[] }[] = [];
    let kind = 'trait';
    let prevEnds = true;
    for (; i < block.length; i++) {
      const l = block[i].trim();
      const sec = l.match(/^(Actions|Bonus Actions|Reactions|Legendary Actions)$/);
      if (sec) {
        kind = { Actions: 'action', 'Bonus Actions': 'bonus', Reactions: 'reaction', 'Legendary Actions': 'legendary' }[sec[1]]!;
        prevEnds = true;
        continue;
      }
      if (!l) continue;
      // A name can wrap: "Primordial Croak (Recharges after a Short or Long" + "Rest). The frogoblin ..."
      const joined = /\((?:Recharge|\d+\/Day)[^)]*$/.test(l) && block[i + 1] ? `${l} ${block[i + 1].trim()}` : l;
      const e = prevEnds ? joined.match(ENTRY) : null;
      if (e) {
        if (joined !== l) i++;
        entries.push({ name: e[1], kind, lines: [joined.slice(e[0].length)] });
      } else if (entries.length) entries[entries.length - 1].lines.push(l);
      prevEnds = /[.!:)”]$/.test(l);
    }
    const [size, ...rest] = typeLine.trim().split(' ');
    const [typePart, ...align] = rest.join(' ').split(',');
    const sub = typePart.match(/\(([^)]+)\)/);
    const ac = head['Armor Class'].match(/(\d+)(?:\s*\(([^)]+)\))?/)!;
    const hp = head['Hit Points'].match(/(\d+)(?:\s*\(([^)]+)\))?/)!;
    const speed = (k: string) => {
      const m = head.Speed.match(k === 'walk' ? /^(\d+) ft/ : new RegExp(`${k} (\\d+) ft`));
      return m ? Number(m[1]) : null;
    };
    const sense = (k: string) => {
      const m = (head.Senses ?? '').match(new RegExp(`${k} (\\d+)`, 'i'));
      return m ? Number(m[1]) : null;
    };
    const list = (k: string) => (head[k] ? head[k].split(/,\s*|;\s*/).map((s) => s.trim().toLowerCase()).filter(Boolean) : []);
    const cr = head.Challenge.match(/^([\d/]+)/)![1];
    const traits = entries.filter((e) => e.kind === 'trait').map((e) => ({ name: e.name, desc: paragraphs(e.lines) }));
    if (/b’lot/i.test(name)) {
      const own = traits.find((t) => t.name === 'Chaos Swell');
      if (own) own.desc += `\n\n${chaosSwell.desc}`;
      else traits.push(chaosSwell);
    }
    return {
      fields: {
        name, size: size.toLowerCase(), type: slug(typePart.replace(/\(.*\)/, '')), subcategory: sub ? sub[1].toLowerCase() : null,
        alignment: align.join(',').trim().toLowerCase(),
        challenge_rating: cr.includes('/') ? Number(cr.split('/')[0]) / Number(cr.split('/')[1]) : Number(cr),
        armor_class: Number(ac[1]), armor_detail: ac[2] ?? null, hit_points: Number(hp[1]), hit_dice: hp[2]?.replace(/\s+/g, '') ?? null,
        walk: speed('walk'), fly: speed('fly'), swim: speed('swim'), climb: speed('climb'), burrow: speed('burrow'), hover: /hover/.test(head.Speed),
        ...Object.fromEntries(ABILITIES.map((a, j) => [`ability_score_${a}`, abilities[j] ?? null])),
        darkvision_range: sense('darkvision'), blindsight_range: sense('blindsight'), tremorsense_range: sense('tremorsense'), truesight_range: sense('truesight'),
        passive_perception: sense('passive Perception'), languages_desc: head.Languages && head.Languages !== '—' ? head.Languages : null,
        damage_vulnerabilities: list('Damage Vulnerabilities'), damage_resistances: list('Damage Resistances'), damage_immunities: list('Damage Immunities'),
        condition_immunities: list('Condition Immunities'), environments: [],
      },
      traits,
      actions: entries.filter((e) => e.kind !== 'trait').map((e) => {
        const desc = paragraphs(e.lines);
        const atk = parseAttack(desc);
        return { name: e.name, kind: e.kind, desc, attacks: atk ? [atk] : [], ...usesOf(e.name) };
      }),
    };
  });
}

/** The koaxians' d12 Chaos Swell table, folded into one trait on each b'lot. */
function khyberiaChaosSwell(lines: string[]): { name: string; desc: string } {
  const at = lines.findIndex((l) => l.trim() === 'Chaos Swell Effect');
  const rows: string[] = [];
  let cur = '';
  for (let i = at + 1; i < lines.length; i++) {
    const l = lines[i].trim();
    if (l === 'Other Monsters') break;
    if (/^(1[0-2]|[1-9])$/.test(l)) continue;
    if (!l) {
      if (cur) rows.push(cur);
      cur = '';
    } else cur = cur ? `${cur} ${l}` : l;
  }
  if (cur) rows.push(cur);
  return { name: 'Chaos Swell', desc: `Roll a d12 for the effect. ${rows.slice(0, 12).map((r, i) => `${i + 1}: ${r}`).join(' ')}` };
}

// ---- Little Book of Beasts (rules-light old-school stats, converted to 5e numbers) ----

const ARMOUR: Record<string, number> = { unarmoured: 10, leather: 12, hide: 13, scalemail: 14, chainmail: 16, 'plate mail': 18, fullplate: 19, variable: 13 };

/** Old-school hit dice to a 5e challenge rating: roughly two-thirds of the HD. */
function crFromHd(hd: number): number {
  if (hd < 1) return 0.125;
  if (hd <= 1) return 0.25;
  if (hd <= 2) return 1;
  return Math.round((hd * 2) / 3);
}
const PROF = (cr: number) => (cr < 5 ? 2 : cr < 9 ? 3 : cr < 13 ? 4 : cr < 17 ? 5 : 6);

function littleBookOfBeasts(file: string): Creature[] {
  const raw = readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  const body = raw.slice(0, raw.indexOf("Kristin Saint-Clair's Little Book Of Beasts [critter stats]"));
  const blocks = body.split(/\n\s*\n/).map((b) => b.trim()).filter(Boolean);
  const out: Creature[] = [];
  for (let k = 0; k < blocks.length; k++) {
    const ls = blocks[k].split('\n').map((l) => l.trim());
    const statAt = ls.findIndex((l) => /HD/.test(l) && /armou?r|unarmoured/.test(l));
    // The book's "critters" (die to any hit) have a name, a line of description and bullets, but no stat line.
    const critter = statAt < 0 && ls.length >= 2 && ls[0].length < 40 && !ls[1].startsWith('*') && ls.slice(2).some((l) => l.startsWith('*'));
    if (statAt !== 1 && !critter) continue;
    const name = ls[0];
    const stat = critter ? 'HD: 1 hp, unarmoured' : ls[1];
    const from = critter ? 1 : 2;
    // "3 HD", "2-8+ HD", "4/8/12 HD" (growth stages: the first is used), "1/2 HD", "HD: 1 hp".
    const hd = /^1\/2 HD/.test(stat) ? 0.5 : /^HD: 1 hp/.test(stat) ? 0.25 : Number(stat.match(/^(\d+)/)?.[1] ?? 1);
    const armour = Object.keys(ARMOUR).find((a) => stat.includes(a)) ?? 'unarmoured';
    const cr = crFromHd(hd);
    const toHit = PROF(cr) + 2 + Math.min(3, Math.floor(hd / 3));
    const bullets = ls.slice(from).filter((l) => l.startsWith('*')).map((l) => l.replace(/^\*\s*/, '').trim());
    const desc = ls.slice(from).filter((l) => !l.startsWith('*')).join(' ');
    const traits: { name: string; desc: string }[] = [];
    const actions: Action[] = [];
    let fly = false;
    let swim = false;
    let burrow = false;
    for (const b of bullets) {
      if (/^(flight|flies|winged)/i.test(b)) fly = true;
      if (/amphibious|aquatic|swim/i.test(b)) swim = true;
      if (/burrow/i.test(b)) burrow = true;
      const colon = b.indexOf(':');
      const head = colon > 0 ? b.slice(0, colon).trim() : b;
      const rest = colon > 0 ? b.slice(colon + 1).trim() : '';
      // "Claws 1d4 / Bite 1d8", "Nightblades x2, 2d8 + paralysis", "Lash 2d4 + constrict, x2"
      if (!rest && /^[A-Z][A-Za-z’' -]*?(?:\s*x\d+,?)?\s+\d+d\d+/.test(b)) {
        for (const part of b.split(/\s+\/\s+/)) {
          const m = part.match(/^([A-Z][A-Za-z’' -]*?)\s*(?:x(\d+),?\s*)?(\d+)d(\d+)(.*)$/);
          if (!m) continue;
          const times = Number(m[2] ?? (m[5].match(/x(\d+)/)?.[1] ?? 1));
          const extra = m[5].match(/\+\s*(\d+)d(\d+)\s+(\w+)/);
          const effect = m[5].replace(/,?\s*x\d+/, '').replace(/^\s*\+\s*/, '').trim();
          const atk: Attack = { toHit, reach: 5, count: Number(m[3]), die: Number(m[4]), type: damageFor(m[1]) };
          if (extra && DAMAGE.test(extra[3])) Object.assign(atk, { xCount: Number(extra[1]), xDie: Number(extra[2]), xType: extra[3].toLowerCase() });
          const avg = Math.floor((Number(m[3]) * (Number(m[4]) + 1)) / 2);
          for (let t = 0; t < times; t++) {
            actions.push({
              name: m[1].trim(), kind: 'action', attacks: [atk],
              desc: `Melee Attack: +${toHit} to hit, reach 5 ft. Hit: ${avg} (${m[3]}d${m[4]}) ${atk.type} damage${effect ? `, ${effect}` : ''}.`,
            });
          }
        }
      } else if (/\d+d\d+/.test(rest) && /breath|weapon|wave|blast|spit|spray|glyph|bite|lash|claw|burn|sting/i.test(head)) {
        // An area or special attack with damage dice: written the 5e way so its burst damage is readable.
        const withAvg = rest.replace(/(\d+)d(\d+)/, (_, n, d) => `${Math.floor((Number(n) * (Number(d) + 1)) / 2)} (${n}d${d})`);
        actions.push({ name: head, kind: 'action', attacks: [], desc: withAvg });
      } else traits.push({ name: head, desc: rest || head });
    }
    // Repeated identical attacks become a multiattack.
    const merged: Action[] = [];
    for (const a of actions) if (!merged.some((m) => m.name === a.name)) merged.push(a);
    const swings = actions.filter((a) => a.attacks.length).length;
    if (swings > 1) merged.unshift({ name: 'Multiattack', kind: 'action', attacks: [], desc: `The creature makes ${['', 'one', 'two', 'three', 'four', 'five', 'six'][Math.min(6, swings)]} attacks.` });
    const hp = Math.max(1, Math.round(hd * 4.5));
    out.push({
      fields: {
        name, size: hd >= 8 ? 'large' : 'medium', type: guessType(`${name} ${bullets.join(' ')} ${desc}`), alignment: 'unaligned',
        challenge_rating: cr, armor_class: ARMOUR[armour] + (/shield/.test(stat) ? 2 : 0), armor_detail: `as ${armour}`,
        hit_points: critter ? 1 : hp, hit_dice: hd >= 1 ? `${hd}d8` : null, walk: 30, fly: fly ? 60 : null, swim: swim ? 30 : null, burrow: burrow ? 20 : null,
        ability_score_intelligence: /telepath|speaks|spells|cunning|clever/i.test(bullets.join(' ')) ? 10 : null,
        languages_desc: /telepath/i.test(bullets.join(' ')) ? 'telepathy' : null, desc, environments: [],
      },
      traits, actions: merged,
    });
  }
  return out;
}

function damageFor(attack: string): string {
  const a = attack.toLowerCase();
  if (/bite|fang|sting|horn|gore|spine|quill|beak|blade/.test(a)) return 'piercing';
  if (/claw|talon|lash|whip|tentacle|slash|rake/.test(a)) return 'slashing';
  if (/burn|flame|fire/.test(a)) return 'fire';
  return 'bludgeoning';
}
function guessType(t: string): string {
  const s = t.toLowerCase();
  if (/construct|clockwork|golem|automaton|iron/.test(s)) return 'construct';
  if (/drake|wyrm|dragon/.test(s)) return 'dragon';
  if (/undead|ghost|corpse|bones/.test(s)) return 'undead';
  if (/fiend|hell|damnation|pact/.test(s)) return 'fiend';
  if (/fey|faerie|dream/.test(s)) return 'fey';
  if (/flame|fire|storm|elemental/.test(s)) return 'elemental';
  if (/spells|sorcer|rune|glyph|sigil/.test(s)) return 'aberration';
  return 'monstrosity';
}

// ---- The Midnight Spell Compendium ----

const SCHOOLS = 'Abjuration|Conjuration|Divination|Enchantment|Evocation|Illusion|Necromancy|Transmutation';
const SPELL_HEAD = new RegExp(`^(?:(${SCHOOLS}) Cantrip|Level (\\d) (${SCHOOLS})) \\(`);
const CLASS_MAP: Record<string, string | null> = {
  bard: 'bard', cleric: 'cleric', druid: 'druid', paladin: 'paladin', ranger: 'ranger', sorcerer: 'sorcerer', warlock: 'warlock', wizard: 'wizard',
  // The book's own (unreleased) priest class is a divine caster; the cleric is the nearest SRD class.
  priest: 'cleric',
  // Artificer references are under the Fan Content Policy, not CC-BY.
  artificer: null,
};
/** Full-width headings the column split cuts in two; they are layout, not spell text. */
const MIDNIGHT_JUNK = /^(criptions|d alphabetically within their spell level\.|Spell Des|Spells are presente)$/;

function castingTime(t: string): { ct: string; ritual: boolean } {
  const s = t.toLowerCase();
  const ritual = /ritual/.test(s);
  if (s.startsWith('bonus action')) return { ct: 'bonus-action', ritual };
  if (s.startsWith('reaction')) return { ct: 'reaction', ritual };
  if (s.startsWith('attack')) return { ct: 'attack', ritual };
  const m = s.match(/^(\d+) (minute|hour)/);
  if (m) return { ct: `${m[1]}${m[2]}${m[1] === '1' ? '' : 's'}`, ritual };
  return { ct: 'action', ritual };
}

function midnight(file: string, doc: string): Fixture[] {
  const lines = pdfColumns(file).filter((l) => !MIDNIGHT_JUNK.test(l.trim()));
  const first = lines.findIndex((l) => l.trim() === 'Cantrips (Level 0 Spells)');
  const heads: number[] = [];
  for (let i = first; i < lines.length; i++) if (SPELL_HEAD.test(lines[i].trim())) heads.push(i);
  // The spell name is the last non-blank line before its school line.
  const nameAt = heads.map((h) => {
    let j = h - 1;
    while (!lines[j].trim()) j--;
    return j;
  });
  return heads.map((h, k) => {
    const name = lines[nameAt[k]].trim();
    let i = h;
    let classLine = lines[i].trim();
    while (!classLine.includes(')')) classLine += ` ${lines[++i].trim()}`;
    const hm = classLine.match(SPELL_HEAD)!;
    const level = hm[1] ? 0 : Number(hm[2]);
    const school = (hm[1] ?? hm[3]).toLowerCase();
    const classes = [...new Set(classLine.slice(classLine.indexOf('(') + 1, classLine.indexOf(')')).split(',').map((c) => CLASS_MAP[c.trim().toLowerCase()]).filter((c): c is string => !!c))].sort();
    const field: Record<string, string> = {};
    let lastField = '';
    const body: string[] = [];
    // Each spell runs to the next spell's name; drop the section headings ("Level 3 Spells") between them.
    const end = k + 1 < heads.length ? nameAt[k + 1] : lines.findIndex((l, j) => j > h && /^Appendix/.test(l.trim()));
    for (i++; i < end; i++) {
      const l = lines[i].trim();
      if (/^(Cantrips \(Level 0 Spells\)|Level \d Spells)$/.test(l)) continue;
      const f = l.match(/^(Casting Time|Range|Components|Duration):\s*(.*)$/);
      if (f && !body.length) {
        lastField = f[1];
        field[lastField] = f[2];
      } else if (lastField && !field.Duration && l && !body.length) field[lastField] += ` ${l}`;
      else if (field.Duration || body.length) body.push(lines[i]);
    }
    const all = paragraphs(body);
    // Summoned creatures' stat tables break into tiny fragments ("MOD SAVE", "+3"); they carry nothing readable.
    const paras = all.split('\n\n').filter((p) => p.length >= 14 || /^[A-Z][a-z]+ [a-z]/.test(p));
    const upAt = paras.findIndex((p) => /^(Using a Higher-Level Spell Slot|Cantrip Upgrade)\./.test(p));
    const desc = (upAt >= 0 ? paras.slice(0, upAt) : paras).join('\n\n');
    const higher = upAt >= 0 ? paras.slice(upAt).join('\n\n').replace(/^(Using a Higher-Level Spell Slot|Cantrip Upgrade)\.\s*/, '') : '';
    const { ct, ritual } = castingTime(field['Casting Time'] ?? 'Action');
    const comps = field.Components ?? '';
    const material = comps.match(/M \((.*)\)/);
    const rangeText = (field.Range ?? '').replace(/Feet/, 'feet');
    const duration = (field.Duration ?? '').replace(/Minute\b/, 'minute').replace(/minutes$/, (m) => (/up to 1 minutes/.test(field.Duration ?? '') ? 'minute' : m));
    const save = desc.match(/\b(Strength|Dexterity|Constitution|Intelligence|Wisdom|Charisma) saving throw/);
    const dmg = [...desc.matchAll(/(\d+d\d+) (\w+) damage/g)].filter((m) => DAMAGE.test(m[2]));
    const shape = desc.match(/(\d+)-foot(?:-radius)?[ -](cone|cube|sphere|line|cylinder|emanation|radius|square)/i);
    return {
      model: 'api_v2.spell', pk: `${doc}_${slug(name)}`,
      fields: {
        name, document: doc, level, school, classes,
        casting_time: ct, ritual, range_text: rangeText, range: Number(rangeText.match(/(\d+)/)?.[1] ?? 0) || null,
        verbal: /\bV\b/.test(comps), somatic: /\bS\b/.test(comps), material: !!material, material_specified: material?.[1] ?? null,
        duration: duration.replace(/^Concentration, up to /i, '').toLowerCase(), concentration: /concentration/i.test(duration),
        saving_throw_ability: save ? save[1].toLowerCase() : '', attack_roll: /spell attack/i.test(desc),
        damage_roll: dmg[0]?.[1] ?? null, damage_types: [...new Set(dmg.map((m) => m[2].toLowerCase()))],
        shape_type: shape ? shape[2].toLowerCase() : null, shape_size: shape ? Number(shape[1]) : null, target_count: 1,
        desc, higher_level: higher,
      },
    };
  });
}

// ---- run ----

rmSync(OUT, { recursive: true, force: true });
write('nick-stefanski', 'khyberia-srd', {
  Document: [document('khyberia-srd', {
    name: 'Khyberia SRD', author: 'Nick Stefanski', publisher: 'nick-stefanski', publication_date: '2023-10-01T00:00:00',
    permalink: 'https://tzarfenix.itch.io/khyberia-srd', gamesystem: '5e-2014',
  })],
  ...creatureFixtures('khyberia-srd', khyberia(join(dir, 'khyberia-srd.pdf'))),
});
write('nocturnal-designs', 'midnight-spells', {
  Document: [document('midnight-spells', {
    name: 'The Midnight Spell Compendium', author: 'Eve Parker', publisher: 'nocturnal-designs', publication_date: '2024-02-09T00:00:00',
    permalink: 'https://nocturnal-designs.itch.io/spell-compendium',
  })],
  Spell: midnight(join(dir, 'midnight-spell-compendium.pdf'), 'midnight-spells'),
});
write('fanged-moon-games', 'little-book-of-beasts', {
  Document: [document('little-book-of-beasts', {
    name: "Kristin Saint-Clair's Little Book of Beasts [critter stats]", author: 'E. A. "taichara" Bisson', publisher: 'fanged-moon-games',
    publication_date: '2023-03-21T00:00:00', permalink: 'https://taichara.itch.io/kristin-saint-clairs-little-book-of-beasts', gamesystem: 'old-school',
  })],
  ...creatureFixtures('little-book-of-beasts', littleBookOfBeasts(join(dir, 'little-book-of-beasts.txt'))),
});
