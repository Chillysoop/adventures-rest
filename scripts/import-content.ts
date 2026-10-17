// Builds the content library under content/srd and content/open from a checkout of the Open5e
// data (https://github.com/open5e/open5e-api, folder data/v2), which carries both SRDs and the
// open-licensed third-party books in one schema.
//
//   git clone --depth 1 https://github.com/open5e/open5e-api /tmp/open5e
//   node scripts/import-content.ts --from /tmp/open5e/data/v2
//
// SRD 5.2 (2024) wins over SRD 5.1 (2014) when both have the same thing; 2014-only entries are kept.
// Third-party entries that duplicate anything earlier in the order are dropped. Every entry carries
// its `source`, and an `rt` block of real-time hints derived with the knobs in content/conversion.json.

import { existsSync, mkdirSync, readFileSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

type Row = Record<string, any> & { pk: string };
type Entry = Record<string, any> & { id: string; name: string; source: string };

const args = process.argv.slice(2);
const from = args[args.indexOf('--from') + 1];
if (!from || !existsSync(join(from, 'License.json'))) {
  console.error('usage: node scripts/import-content.ts --from <open5e-api>/data/v2');
  process.exit(1);
}
const OUT = 'content';
const conv = JSON.parse(readFileSync(join(OUT, 'conversion.json'), 'utf8'));
const SRD = ['srd-2024', 'srd-2014'];
const ORDER: string[] = [...SRD, ...conv.thirdPartyOrder];

// ---- reading Open5e fixtures ----

const docDirs = new Map<string, string>();
for (const pub of readdirSync(from, { withFileTypes: true })) {
  if (!pub.isDirectory()) continue;
  for (const doc of readdirSync(join(from, pub.name), { withFileTypes: true })) {
    if (doc.isDirectory()) docDirs.set(doc.name, join(from, pub.name, doc.name));
  }
}

function load(doc: string, model: string): Row[] {
  const file = join(docDirs.get(doc)!, `${model}.json`);
  if (!existsSync(file)) return [];
  const rows = JSON.parse(readFileSync(file, 'utf8')) as { pk: string; fields: Record<string, any> }[];
  return rows.map((r) => ({ ...r.fields, pk: String(r.pk) })).sort((a, b) => (a.pk < b.pk ? -1 : a.pk > b.pk ? 1 : 0));
}

function groupBy(rows: Row[], key: string): Map<string, Row[]> {
  const m = new Map<string, Row[]>();
  for (const r of rows) {
    const k = r[key];
    if (!m.has(k)) m.set(k, []);
    m.get(k)!.push(r);
  }
  return m;
}

const slug = (s: string) =>
  s.toLowerCase().normalize('NFKD').replace(/[̀-ͯ]/g, '').replace(/['’]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
const ref = (pk: string | null | undefined) => (pk ? pk.slice(pk.indexOf('_') + 1) : null);
const text = (s: string | null | undefined) => (s ?? '').replace(/\r\n/g, '\n').trim();
const num = (s: string | number | null | undefined) => (s === null || s === undefined || s === '' ? null : Number(s));
const titleCase = (s: string) => s.replace(/(^|[\s-])\w/g, (c) => c.toUpperCase());

/** Drops null, empty-string, empty-array and false fields so files stay small. */
function compact<T extends Record<string, any>>(o: T): T {
  const out: Record<string, any> = {};
  for (const [k, v] of Object.entries(o)) {
    if (v === null || v === undefined || v === '' || v === false) continue;
    if (Array.isArray(v) && v.length === 0) continue;
    if (typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === 0) continue;
    out[k] = v;
  }
  return out as T;
}

// ---- real-time hints ----

function rangeBand(rangeText: string, range: number | null): string {
  const t = (rangeText ?? '').toLowerCase();
  if (t.startsWith('self')) return 'self';
  if (t === 'touch') return 'touch';
  if (t === 'sight' || t === 'unlimited' || t.includes('mile') || t === 'special') return 'far';
  const r = range ?? 0;
  if (r <= conv.rangeBands.touch) return 'touch';
  if (r <= conv.rangeBands.short) return 'short';
  if (r <= conv.rangeBands.medium) return 'medium';
  return 'far';
}

/** Tabletop duration -> seconds of real-time play. Long effects are compressed on a square-root curve. */
function rtDuration(d: string): number | null {
  const t = (d ?? '').toLowerCase();
  if (!t || t.startsWith('instant')) return 0;
  if (t.includes('until dispelled') || t.includes('permanent')) return -1;
  const m = t.match(/(\d+)\s*(round|minute|hour|day)/);
  if (!m) return null;
  const n = Number(m[1]);
  if (m[2] === 'round') return n * conv.secondsPerRound;
  const minutes = n * ({ minute: 1, hour: 60, day: 1440 } as Record<string, number>)[m[2]];
  return Math.min(conv.duration.maxSeconds, Math.round(conv.duration.secondsPerSqrtMinute * Math.sqrt(minutes)));
}

const CONDITIONS = /\b(blinded|charmed|deafened|frightened|grappled|incapacitated|invisible|paralyzed|petrified|poisoned|prone|restrained|stunned|unconscious|exhaustion|slowed)\b/i;

function spellRoles(s: Row): string[] {
  const d = `${s.desc ?? ''} ${s.higher_level ?? ''}`;
  const roles = new Set<string>();
  if ((s.damage_types ?? []).length || /\d+d\d+\s+[\w ]{0,20}damage/i.test(d)) roles.add('damage');
  if (/regains?\s+(a number of\s+)?(\w+\s+)?hit points|restores?\s+\w*\s*hit points|temporary hit points/i.test(d)) roles.add('heal');
  if (CONDITIONS.test(d) && s.saving_throw_ability && !s.school?.endsWith('divination')) roles.add('control');
  if (/\b(summon|conjure|animate|appears? in an unoccupied)\b/i.test(d)) roles.add('summon');
  if (/\b(teleport|fly speed|flying speed|speed increases|plane shift|travel)\b/i.test(d)) roles.add('movement');
  if (s.school?.endsWith('divination') || /\b(scry|clairvoyance|you learn|you sense|detect|reveal)\b/i.test(d)) roles.add('scouting');
  if (/\b(resistance to|bonus to (ac|armor class|attack|saving)|advantage on|you and each|willing creature)\b/i.test(d) && !roles.has('damage')) roles.add('buff');
  if (s.school?.endsWith('abjuration') && !roles.size) roles.add('ward');
  if (!roles.size) roles.add('utility');
  return [...roles].sort();
}

function spellRt(s: Row) {
  const ct = s.casting_time;
  const lvl = s.level ?? 0;
  return compact({
    castSeconds: conv.castSeconds[ct] ?? conv.castSeconds.action,
    cooldownSeconds: lvl === 0 ? 0 : conv.spellCooldown.perLevel * lvl + conv.spellCooldown.perLevelSquared * lvl * lvl,
    durationSeconds: rtDuration(s.duration),
    sustained: !!s.concentration,
    downtime: conv.downtimeCastTimes.includes(ct) || !!s.ritual,
    range: rangeBand(s.range_text, s.range),
    targets: s.shape_type ? 'area' : (s.target_count ?? 1) > 1 ? 'multi' : 'single',
    roles: spellRoles(s),
  });
}

const XP: Record<string, number> = {
  '0': 10, '0.125': 25, '0.25': 50, '0.5': 100, '1': 200, '2': 450, '3': 700, '4': 1100, '5': 1800, '6': 2300, '7': 2900,
  '8': 3900, '9': 5000, '10': 5900, '11': 7200, '12': 8400, '13': 10000, '14': 11500, '15': 13000, '16': 15000, '17': 18000,
  '18': 20000, '19': 22000, '20': 25000, '21': 33000, '22': 41000, '23': 50000, '24': 62000, '25': 75000, '26': 90000,
  '27': 105000, '28': 120000, '29': 135000, '30': 155000,
};
const WORDS: Record<string, number> = { one: 1, two: 2, three: 3, four: 4, five: 5, six: 6, seven: 7, eight: 8 };

function monsterRt(c: Row, actions: Entry[], cr: number) {
  const tier = conv.monsterTiers.find((t: any) => cr <= t.maxCr);
  const attacks = actions.filter((a) => a.kind === 'action').flatMap((a) => a.attacks ?? []);
  const best = attacks.reduce((m: number, a: any) => Math.max(m, a.avg ?? 0), 0);
  const multi = actions.find((a) => /multiattack/i.test(a.name));
  let swings = 1;
  if (multi) {
    const words = [...String(multi.text).toLowerCase().matchAll(/\b(one|two|three|four|five|six|seven|eight)\b/g)].map((m) => WORDS[m[1]]);
    if (words.length) swings = Math.max(...words);
  }
  const burst = actions.reduce((m, a) => {
    if (a.attacks) return m;
    const hit = String(a.text).match(/(\d+)\s*\(\d+d\d+/);
    return Math.max(m, hit ? Number(hit[1]) : 0);
  }, 0);
  const speeds = [c.walk, c.fly, c.swim, c.climb, c.burrow].map((v) => Number(v ?? 0));
  const speed = Math.max(...speeds);
  const ranged = attacks.some((a: any) => (a.range ?? 0) >= 30) && !attacks.some((a: any) => a.reach && (a.avg ?? 0) >= best);
  const caster = actions.some((a) => /spellcasting/i.test(a.name)) || /spellcasting/i.test(JSON.stringify(c._traits ?? ''));
  const controls = actions.some((a) => !a.attacks && CONDITIONS.test(String(a.text)) && /saving throw/i.test(String(a.text)));
  const legendary = actions.some((a) => a.kind === 'legendary');
  const role = legendary ? 'solo'
    : ranged || caster ? 'artillery'
    : controls ? 'controller'
    : (c.fly ?? 0) > 0 || speed >= 50 ? 'skirmisher'
    : 'soldier'; // some soldiers become brutes once every book's hit points are known (see below)
  return compact({
    tier: tier.tier,
    band: tier.label,
    threat: XP[String(cr)] ?? Math.round(200 * cr * cr),
    role,
    dps: Math.round(((swings * best) / conv.secondsPerRound) * 10) / 10,
    burst,
    moves: (c.fly ?? 0) > 0 ? 'flies' : (c.burrow ?? 0) > 0 ? 'burrows' : (c.swim ?? 0) > (c.walk ?? 0) ? 'swims' : 'walks',
    fast: speed >= 50,
    thinks: (c.ability_score_intelligence ?? 0) >= 6,
  });
}

// ---- per-model builders (one document at a time) ----

function spells(doc: string): Entry[] {
  return load(doc, 'Spell').map((s) =>
    compact({
      id: slug(s.name), name: s.name.trim(), source: doc,
      level: s.level ?? 0, school: ref(s.school) ?? s.school,
      classes: (s.classes ?? []).map((c: string) => slug(ref(c)!)).sort(),
      castingTime: s.casting_time, range: s.range_text, duration: s.duration,
      concentration: !!s.concentration, ritual: !!s.ritual,
      components: `${s.verbal ? 'V' : ''}${s.somatic ? 'S' : ''}${s.material ? 'M' : ''}`,
      material: s.material_specified,
      save: s.saving_throw_ability, attack: !!s.attack_roll,
      damage: s.damage_roll, damageTypes: s.damage_types,
      area: s.shape_type ? compact({ shape: s.shape_type, size: s.shape_size }) : null,
      text: text(s.desc), higherLevel: text(s.higher_level),
      rt: spellRt(s),
    }),
  );
}

function monsters(doc: string): Entry[] {
  const attacks = groupBy(load(doc, 'CreatureActionAttack'), 'parent');
  const actions = groupBy(load(doc, 'CreatureAction'), 'parent');
  const traits = groupBy(load(doc, 'CreatureTrait'), 'parent');
  const kinds: Record<string, string> = { ACTION: 'action', BONUS_ACTION: 'bonus', REACTION: 'reaction', LEGENDARY_ACTION: 'legendary' };
  return load(doc, 'Creature').map((c) => {
    const acts: Entry[] = (actions.get(c.pk) ?? [])
      .sort((a, b) => (a.order_in_statblock ?? 0) - (b.order_in_statblock ?? 0) || (a.pk < b.pk ? -1 : 1))
      .map((a) =>
        compact({
          name: a.name, kind: kinds[a.action_type] ?? 'action', text: text(a.desc),
          uses: a.uses_type ? `${a.uses_type.toLowerCase()}${a.uses_param ? ` ${a.uses_param}` : ''}` : null,
          cost: a.legendary_action_cost,
          attacks: (attacks.get(a.pk) ?? []).map((t) => {
            const die = Number(String(t.damage_die_type ?? 'D0').slice(1));
            const xdie = Number(String(t.extra_damage_die_type ?? 'D0').slice(1));
            const avg = Math.floor(((t.damage_die_count ?? 0) * (die + 1)) / 2) + (t.damage_bonus ?? 0)
              + Math.floor(((t.extra_damage_die_count ?? 0) * (xdie + 1)) / 2) + (t.extra_damage_bonus ?? 0);
            return compact({
              toHit: t.to_hit_mod, avg,
              dice: t.damage_die_count ? `${t.damage_die_count}d${die}${t.damage_bonus ? (t.damage_bonus > 0 ? '+' : '') + t.damage_bonus : ''}` : null,
              type: t.damage_type, extra: t.extra_damage_type && t.extra_damage_die_count ? `${t.extra_damage_die_count}d${xdie} ${t.extra_damage_type}` : null,
              reach: t.reach, range: t.range, longRange: t.long_range,
            });
          }),
        }) as Entry,
      );
    const tr = (traits.get(c.pk) ?? []).map((t) => ({ name: t.name, text: text(t.desc) }));
    const cr = Number(c.challenge_rating ?? 0);
    return compact({
      id: slug(c.name), name: c.name.trim(), source: doc,
      size: ref(c.size) ?? c.size, type: ref(c.type) ?? c.type, subtype: c.subcategory, alignment: c.alignment,
      cr, ac: c.armor_class, armorNote: c.armor_detail, hp: c.hit_points, hitDice: c.hit_dice,
      speed: compact({ walk: c.walk, fly: c.fly, swim: c.swim, climb: c.climb, burrow: c.burrow, hover: !!c.hover }),
      abilities: {
        str: c.ability_score_strength, dex: c.ability_score_dexterity, con: c.ability_score_constitution,
        int: c.ability_score_intelligence, wis: c.ability_score_wisdom, cha: c.ability_score_charisma,
      },
      senses: compact({
        darkvision: c.darkvision_range, blindsight: c.blindsight_range, tremorsense: c.tremorsense_range,
        truesight: c.truesight_range, passivePerception: c.passive_perception,
      }),
      languages: c.languages_desc,
      resist: c.damage_resistances, immune: c.damage_immunities, vulnerable: c.damage_vulnerabilities,
      conditionImmune: (c.condition_immunities ?? []).map((x: string) => ref(x) ?? x),
      habitats: (c.environments ?? []).map((e: string) => ref(e) ?? e).sort(),
      traits: tr, actions: acts,
      rt: monsterRt({ ...c, _traits: tr }, acts, cr),
    });
  });
}

function itemRt(i: Row, rarity: string | null) {
  const cat = i.category;
  const slot = i.weapon || cat === 'weapon' ? 'weapon'
    : /shield/i.test(i.name) ? 'shield'
    : i.armor || cat === 'armor' ? 'armor'
    : ['potion', 'scroll', 'ammunition'].includes(cat) ? 'consumable'
    : ['ring', 'rod', 'staff', 'wand', 'wondrous-item'].includes(cat) ? 'trinket'
    : ['mount', 'land-vehicle', 'waterborne-vehicle'].includes(cat) ? 'transport'
    : cat === 'tools' ? 'tool'
    : 'gear';
  const cost = num(i.cost) ?? 0;
  return compact({
    slot,
    tier: rarity ? conv.rarityTiers[rarity] ?? 1 : 0,
    valueGp: cost > 0 ? cost : rarity ? conv.rarityValueGp[rarity] ?? 0 : 0,
    consumable: slot === 'consumable',
  });
}

function weaponsAndArmor(doc: string) {
  const props = new Map(load(doc, 'WeaponProperty').map((p) => [p.pk, p]));
  const assigned = groupBy(load(doc, 'WeaponPropertyAssignment'), 'weapon');
  const weapons = new Map(
    load(doc, 'Weapon').map((w) => {
      const ps = (assigned.get(w.pk) ?? []).map((a) => ({ p: props.get(a.property), detail: a.detail }));
      return [w.pk, compact({
        damage: w.damage_dice, type: w.damage_type, simple: !!w.is_simple,
        range: w.range || null, longRange: w.long_range || null,
        properties: ps.filter((x) => x.p?.type !== 'Mastery')
          .map((x) => (x.p ? x.p.name.replace(/\s*\(.*\)$/, '') : '') + (x.detail ? ` (${x.detail})` : '')).filter(Boolean).sort(),
        mastery: ps.find((x) => x.p?.type === 'Mastery')?.p?.name,
      })];
    }),
  );
  const armor = new Map(
    load(doc, 'Armor').map((a) => [a.pk, compact({
      ac: a.ac_base, addDex: !!a.ac_add_dexmod, dexCap: a.ac_cap_dexmod,
      stealthDisadvantage: !!a.grants_stealth_disadvantage, strength: a.strength_score_required,
    })]),
  );
  return { weapons, armor };
}

function items(doc: string, magic: boolean): Entry[] {
  const { weapons, armor } = weaponsAndArmor(doc);
  return load(doc, magic ? 'MagicItem' : 'Item').map((i) =>
    compact({
      id: slug(i.name), name: i.name.trim(), source: doc,
      category: ref(i.category) ?? i.category,
      rarity: magic ? ref(i.rarity) ?? i.rarity : null,
      attunement: !!i.requires_attunement, attunementNote: i.attunement_detail,
      costGp: num(i.cost) || null, weight: num(i.weight) || null,
      weapon: i.weapon ? weapons.get(i.weapon) ?? ref(i.weapon) : null,
      armor: i.armor ? armor.get(i.armor) ?? ref(i.armor) : null,
      text: text(i.desc),
      rt: itemRt(i, magic ? ref(i.rarity) ?? i.rarity ?? 'common' : null),
    }),
  );
}

function classesAndSubclasses(doc: string): { classes: Entry[]; subclasses: Entry[] } {
  const items = groupBy(load(doc, 'ClassFeatureItem'), 'parent');
  const feats = groupBy(load(doc, 'ClassFeature'), 'parent');
  const all = load(doc, 'CharacterClass');
  const byPk = new Map(all.map((c) => [c.pk, c]));
  const build = (c: Row) => {
    const features: any[] = [];
    const tables: Record<string, Record<string, string>> = {};
    for (const f of feats.get(c.pk) ?? []) {
      const its = items.get(f.pk) ?? [];
      const cols = its.filter((x) => x.column_value !== null && x.column_value !== undefined);
      if (cols.length && cols.length === its.length) {
        tables[f.name] = Object.fromEntries(cols.sort((a, b) => a.level - b.level).map((x) => [String(x.level), String(x.column_value)]));
        continue;
      }
      const levels = its.map((x) => x.level).filter((l) => l !== null && l !== undefined).sort((a, b) => a - b);
      features.push(compact({ level: levels[0] ?? 1, name: f.name, text: text(f.desc), alsoAt: levels.slice(1) }));
    }
    features.sort((a, b) => a.level - b.level || (a.name < b.name ? -1 : 1));
    return { features, tables };
  };
  const classes: Entry[] = [];
  const subclasses: Entry[] = [];
  for (const c of all) {
    const { features, tables } = build(c);
    const id = slug(c.name);
    if (c.subclass_of) {
      const parent = byPk.get(c.subclass_of);
      subclasses.push(compact({
        id, name: c.name.trim(), source: doc,
        classId: slug(parent?.name ?? ref(c.subclass_of)!),
        text: text(c.desc), features,
        rt: { role: conv.classRoles[slug(parent?.name ?? ref(c.subclass_of)!)] ?? null },
      }));
    } else {
      const hitDie = Number(String(c.hit_dice ?? 'D8').replace(/\D/g, '')) || 8;
      const caster = String(c.caster_type ?? 'NONE').toLowerCase();
      classes.push(compact({
        id, name: c.name.trim(), source: doc, hitDie,
        primaryAbilities: (c.primary_abilities ?? []).length ? c.primary_abilities.map((a: string) => ref(a) ?? a) : conv.classPrimary[id],
        savingThrows: (c.saving_throws ?? []).map((a: string) => ref(a) ?? a),
        caster: caster === 'none' ? null : caster,
        text: text(c.desc), features, tables,
        rt: compact({
          role: conv.classRoles[id] ?? (caster === 'full' ? 'caster' : hitDie >= 10 ? 'frontline' : 'skirmisher'),
          toughness: hitDie,
          magic: caster === 'none' ? null : caster,
        }),
      }));
    }
  }
  return { classes, subclasses };
}

function species(doc: string): Entry[] {
  const traits = groupBy(load(doc, 'SpeciesTrait'), 'parent');
  const all = load(doc, 'Species');
  const byPk = new Map(all.map((s) => [s.pk, s]));
  return all.map((s) =>
    compact({
      id: slug(s.name), name: s.name.trim(), source: doc,
      parent: s.subspecies_of ? slug(byPk.get(s.subspecies_of)?.name ?? ref(s.subspecies_of)!) : null,
      text: text(s.desc),
      traits: (traits.get(s.pk) ?? []).sort((a, b) => (a.order ?? 0) - (b.order ?? 0)).map((t) => ({ name: t.name, text: text(t.desc) })),
    }),
  );
}

function backgrounds(doc: string): Entry[] {
  const ben = groupBy(load(doc, 'BackgroundBenefit'), 'parent');
  return load(doc, 'Background').map((b) =>
    compact({
      id: slug(b.name), name: b.name.trim(), source: doc, text: text(b.desc),
      benefits: (ben.get(b.pk) ?? []).map((x) => compact({ name: x.name, type: x.type, text: text(x.desc) })),
    }),
  );
}

function feats(doc: string): Entry[] {
  const ben = groupBy(load(doc, 'FeatBenefit'), 'parent');
  return load(doc, 'Feat').map((f) =>
    compact({
      id: slug(f.name), name: f.name.trim(), source: doc,
      type: f.type ? slug(f.type) : null, prerequisite: text(f.prerequisite).replace(/^\*|\*$/g, ''),
      text: text(f.desc),
      benefits: (ben.get(f.pk) ?? []).map((x) => compact({ name: x.name, text: text(x.desc) })),
    }),
  );
}

function conditions(doc: string): Entry[] {
  return load(doc, 'ConditionDescription').map((c) => {
    const id = slug(ref(c.describes) ?? c.describes);
    return { id, name: titleCase(id), source: doc, text: text(c.desc) };
  });
}

// ---- merge ----

const KINDS: Record<string, (doc: string) => Entry[]> = {
  classes: (d) => classesAndSubclasses(d).classes,
  subclasses: (d) => classesAndSubclasses(d).subclasses,
  species, backgrounds, feats, spells, monsters, conditions,
  equipment: (d) => items(d, false).map(cleanEquipment),
  'magic-items': (d) => collapseVariants(items(d, true)),
};

// ---- matching the same thing across editions and books ----

const STOP = new Set(['of', 'the', 'a', 'an', 'or', 'and']);
const singular = (w: string) => (w.length > 3 && w.endsWith('s') && !w.endsWith('ss') ? w.slice(0, -1) : w);
const tokens = (id: string) => id.split('-').filter((w) => w && !STOP.has(w)).map(singular);
const tokenKey = (id: string) => [...new Set(tokens(id))].sort().join('-');

/** Strips the edition-specific noise from gear names ("(50 GP)", "Musical Instrument,", "bag of 20", "vial"). */
const GEAR_NOISE: [RegExp, string][] = [
  [/\s*\(\d+ GP\)$/i, ''],
  [/^(Musical Instrument|Gaming Set|Holy Symbol|Druidic Focus|Arcane Focus),\s*/i, ''],
];
function cleanEquipment(e: Entry): Entry {
  let name = e.name;
  for (const [re, by] of GEAR_NOISE) name = name.replace(re, by);
  return name === e.name ? e : { ...e, id: slug(name), name };
}
const GEAR_KEY_NOISE = /-(vial|flask|bag-of-\d+|\d+-(feet|foot)|one-sheet|1-piece|1-day|1-ounce-bottle|set|armor)$/;

/** Order-free, plural-free key, so "Crossbow, hand" meets "Hand Crossbow" and the 2014 belts meet the 2024 ones. */
function matchKey(kind: string, id: string): string {
  id = conv.aliases[kind]?.[id] ?? id;
  if (kind === 'equipment') id = id.replace(GEAR_KEY_NOISE, '').replace(GEAR_KEY_NOISE, '');
  let key = tokenKey(id);
  if (kind === 'magic-items') {
    // "Flame Tongue", "Flame Tongue Weapon" and "Sword of Sharpness", "Weapon of Sharpness" are one item each.
    const ws = [...new Set(key.split('-').map((w) => (w === 'sword' || w === 'axe' ? 'weapon' : w)))];
    const named = ws.filter((w) => w !== 'weapon' && !/^\d+$/.test(w));
    key = (named.length ? ws.filter((w) => w !== 'weapon') : ws).sort().join('-');
  }
  return key;
}

// Weapon and armour names, as token keys, for spotting "Flame Tongue Longsword" as a variant of "Flame Tongue".
const gearKeys = { weapon: new Set<string>(), armor: new Set<string>() };
for (const doc of SRD) {
  for (const e of items(doc, false)) {
    if (e.weapon) gearKeys.weapon.add(tokenKey(e.id));
    if (e.armor && !/shield/i.test(e.name)) {
      gearKeys.armor.add(tokenKey(e.id));
      gearKeys.armor.add(tokenKey(e.id.replace(/-armor$/, '')));
    }
  }
}
gearKeys.weapon.add('net');
const SWORDS = new Set(['greatsword', 'longsword', 'rapier', 'scimitar', 'shortsword']);
const AXES = new Set(['battleaxe', 'greataxe', 'handaxe', 'halberd']);

function gearKind(words: string[]): 'weapon' | 'armor' | null {
  const k = [...new Set(words.map((w) => singular(slug(w))).filter(Boolean))].sort().join('-');
  return gearKeys.weapon.has(k) ? 'weapon' : gearKeys.armor.has(k) ? 'armor' : null;
}

/** Splits a magic item name into a template with a § hole and the weapon or armour that fills it. */
function variantOf(name: string): { template: string; variant: string; gear: 'weapon' | 'armor' | null } | null {
  const paren = name.match(/^(.*?)\s*\(([^)]+)\)$/);
  if (paren) {
    const [, base, inner] = paren;
    const g = gearKind(base.split(/[\s,-]+/));
    if (g && /^\+\d$/.test(inner)) return { template: `§ ${inner}`, variant: base, gear: g };
    return { template: base, variant: inner, gear: gearKind(inner.split(/[\s,-]+/)) };
  }
  const words = name.split(/\s+/);
  for (let len = 3; len >= 1; len--) {
    for (let i = 0; i + len <= words.length; i++) {
      const g = gearKind(words.slice(i, i + len));
      if (g) return { template: [...words.slice(0, i), '§', ...words.slice(i + len)].join(' '), variant: words.slice(i, i + len).join(' '), gear: g };
    }
  }
  return null;
}

/**
 * The 2024 SRD lists every weapon a magic weapon can be ("Vicious Battleaxe", "Vicious Blowgun", ...).
 * Folds each such family with one shared text into a single entry with a `variants` list.
 */
function collapseVariants(entries: Entry[]): Entry[] {
  const families = new Map<string, { e: Entry; v: NonNullable<ReturnType<typeof variantOf>> }[]>();
  for (const e of entries) {
    const v = variantOf(e.name);
    if (!v) continue;
    const fam = `${v.template}|${v.template.includes('§') ? v.gear : ''}`;
    if (!families.has(fam)) families.set(fam, []);
    families.get(fam)!.push({ e, v });
  }
  const folded = new Set<Entry>();
  const out: Entry[] = [];
  for (const [fkey, fam] of families) {
    const template = fkey.split('|')[0];
    if (fam.length < 2 || new Set(fam.map((f) => f.e.text)).size > 1 || new Set(fam.map((f) => f.e.rarity)).size > 1) continue;
    const vs = fam.map((f) => slug(f.v.variant));
    const word = fam.every((f) => f.v.gear === 'armor') ? 'Armor' : vs.every((v) => SWORDS.has(v)) ? 'Sword' : vs.every((v) => AXES.has(v)) ? 'Axe' : 'Weapon';
    const name = template.includes('§') ? template.replace('§', word) : template;
    const first = fam[0].e;
    const { weapon, armor, ...rest } = first;
    out.push(compact({ ...rest, id: slug(name), name, variants: fam.map((f) => f.v.variant).sort() }));
    for (const f of fam) folded.add(f.e);
  }
  return [...entries.filter((e) => !folded.has(e)), ...out];
}

const alias = (kind: string, id: string) => conv.aliases[kind]?.[id] ?? id;
const report: Record<string, any> = {};
const srdOut: Record<string, Entry[]> = {};
const openOut: Record<string, Record<string, Entry[]>> = {};

for (const [kind, build] of Object.entries(KINDS)) {
  const seen = new Map<string, Entry>();
  const r = { srd2024: 0, srd2014Only: 0, srd2014Merged: 0, open: {} as Record<string, number>, openDuplicatesDropped: 0 };
  for (const doc of ORDER) {
    if (!docDirs.has(doc)) continue;
    const isSrd = SRD.includes(doc);
    for (const e of build(doc)) {
      const key = matchKey(kind, e.id);
      const prior = seen.get(key);
      if (prior) {
        if (prior.source === doc) {
          // The same thing twice in one book (often a variant list split in two): keep one, pool the variants.
          if (e.variants) prior.variants = [...new Set([...(prior.variants ?? []), ...e.variants])].sort();
        } else if (doc === 'srd-2014' && prior.source === 'srd-2024') {
          prior.alsoIn = [...(prior.alsoIn ?? []), 'srd-2014'];
          // 2024 wins, but its gaps (monster habitats, class saving throws) are filled from 2014.
          for (const [k, v] of Object.entries(e)) if (prior[k] === undefined && k !== 'variants') prior[k] = v;
          r.srd2014Merged++;
        } else if (!isSrd) r.openDuplicatesDropped++;
        continue;
      }
      if (kind === 'subclasses' || (kind === 'species' && e.parent)) {
        const parentKind = kind === 'subclasses' ? 'classes' : 'species';
        const field = kind === 'subclasses' ? 'classId' : 'parent';
        e[field] = alias(parentKind, e[field]);
      }
      seen.set(key, e);
      if (isSrd) {
        (srdOut[kind] ??= []).push(e);
        if (doc === 'srd-2024') r.srd2024++;
        else r.srd2014Only++;
      } else {
        ((openOut[doc] ??= {})[kind] ??= []).push(e);
        r.open[doc] = (r.open[doc] ?? 0) + 1;
      }
    }
  }
  report[kind] = r;
}

// Brutes: soldiers with well above the usual hit points for their challenge rating, across every book.
{
  const all = [...(srdOut.monsters ?? []), ...Object.values(openOut).flatMap((k) => k.monsters ?? [])];
  const byCr = groupBy(all as any, 'cr');
  const median = new Map([...byCr].map(([cr, ms]) => {
    const hps = ms.map((m) => m.hp ?? 0).sort((a, b) => a - b);
    return [cr, hps[Math.floor(hps.length / 2)]];
  }));
  for (const m of all) if (m.rt.role === 'soldier' && (m.hp ?? 0) >= conv.bruteHpOverMedian * median.get(m.cr)!) m.rt.role = 'brute';
}

// Subclass lists on each class, so a reader sees what is available without a join.
const allSubclasses = [...(srdOut.subclasses ?? []), ...Object.values(openOut).flatMap((k) => k.subclasses ?? [])];
for (const c of [...srdOut.classes, ...Object.values(openOut).flatMap((k) => k.classes ?? [])]) c.subclassIds = allSubclasses.filter((s) => s.classId === c.id).map((s) => s.id).sort();

function writeEntries(path: string, entries: Entry[]) {
  entries.sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  writeFileSync(path, `[\n${entries.map((e) => JSON.stringify(e)).join(',\n')}\n]\n`);
}

rmSync(join(OUT, 'srd'), { recursive: true, force: true });
rmSync(join(OUT, 'open'), { recursive: true, force: true });
mkdirSync(join(OUT, 'srd'), { recursive: true });
for (const [kind, entries] of Object.entries(srdOut)) writeEntries(join(OUT, 'srd', `${kind}.json`), entries);
for (const [doc, kinds] of Object.entries(openOut)) {
  mkdirSync(join(OUT, 'open', doc), { recursive: true });
  for (const [kind, entries] of Object.entries(kinds)) writeEntries(join(OUT, 'open', doc, `${kind}.json`), entries);
}

// ---- sources and attribution ----

const licenses: Record<string, string> = Object.fromEntries(
  JSON.parse(readFileSync(join(from, 'License.json'), 'utf8')).map((l: any) => [l.pk, l.fields.name]),
);
const PUBLISHER_NAMES: Record<string, string> = {
  'wizards-of-the-coast': 'Wizards of the Coast LLC', 'kobold-press': 'Open Design LLC d/b/a Kobold Press',
  'en-publishing': 'EN Publishing', somanyrobots: 'somanyrobots',
};
const sources: Record<string, any> = {};
for (const doc of ORDER) {
  if (!docDirs.has(doc)) continue;
  const d = load(doc, 'Document')[0];
  const year = String(d.publication_date ?? '').slice(0, 4) || null;
  const publisher = PUBLISHER_NAMES[d.publisher] ?? d.publisher;
  const lic: string[] = d.licenses ?? [];
  sources[doc] = compact({
    title: d.name, publisher, author: d.author, year, url: d.permalink,
    // Prefer CC-BY-4.0 when a book offers it; OGL only when that is the only open licence.
    license: lic.includes('cc-by-40') ? 'CC-BY-4.0' : lic.includes('ogl-10a') ? 'OGL-1.0a' : lic[0],
    alsoOffered: lic.filter((l) => !(lic.includes('cc-by-40') ? l === 'cc-by-40' : l === 'ogl-10a')).map((l) => licenses[l] ?? l),
    entries: Object.values(report).reduce((n: number, r: any) => n + (r.open[doc] ?? 0), 0) || null,
  });
}
sources['srd-2024'].entries = Object.values(report).reduce((n: number, r: any) => n + r.srd2024, 0);
sources['srd-2014'].entries = Object.values(report).reduce((n: number, r: any) => n + r.srd2014Only, 0);
sources['crown-and-compass'] = { title: 'Crown & Compass originals', publisher: 'Adventures Rest', license: 'proprietary', note: 'Hand-written for this game; lives in content/original.' };
writeFileSync(join(OUT, 'sources.json'), `${JSON.stringify(sources, null, 2)}\n`);

const SRD_ATTRIB: Record<string, string> = {
  'srd-2024': 'This work includes material from the System Reference Document 5.2 ("SRD 5.2") by Wizards of the Coast LLC, available at https://www.dndbeyond.com/srd. The SRD 5.2 is licensed under the Creative Commons Attribution 4.0 International License, available at https://creativecommons.org/licenses/by/4.0/legalcode.',
  'srd-2014': 'This work includes material taken from the System Reference Document 5.1 ("SRD 5.1") by Wizards of the Coast LLC and available at https://dnd.wizards.com/resources/systems-reference-document. The SRD 5.1 is licensed under the Creative Commons Attribution 4.0 International License available at https://creativecommons.org/licenses/by/4.0/legalcode.',
};
const ccby = Object.entries(sources).filter(([k, s]) => s.license === 'CC-BY-4.0' && !SRD.includes(k));
const ogl = Object.entries(sources).filter(([, s]) => s.license === 'OGL-1.0a');
const md = [
  '# Attribution',
  '',
  'Generated by `scripts/import-content.ts`; edit that script, not this file. Every entry in `content/` names its source;',
  'the source ids below are the values of that field. Show these notices in the game credits.',
  '',
  '## Creative Commons Attribution 4.0',
  '',
  ...SRD.map((k) => `- **${k}** (${sources[k].title}). ${SRD_ATTRIB[k]}`),
  ...ccby.map(([k, s]) => `- **${k}**: This work includes material from *${s.title}*${s.year ? ` (${s.year})` : ''} by ${s.author ? `${s.author}, ` : ''}${s.publisher}${s.url ? `, ${s.url}` : ''}, licensed under the Creative Commons Attribution 4.0 International License, https://creativecommons.org/licenses/by/4.0/legalcode. Converted and adapted for real-time play.`),
  '',
  '## Open Game License 1.0a',
  '',
  'Entries whose source is listed here are Open Game Content under the Open Game License v1.0a, reproduced in',
  '`content/licenses/OGL-1.0a.txt`. Only their game mechanics and rules text are used; no Product Identity (logos,',
  'trade dress, proprietary setting names) is claimed. Section 15 of that license is:',
  '',
  '> Open Game License v 1.0a Copyright 2000, Wizards of the Coast, LLC.',
  '>',
  '> System Reference Document 5.1 Copyright 2016, Wizards of the Coast, Inc.; Authors Mike Mearls, Jeremy Crawford, Chris Perkins, Rodney Thompson, Peter Lee, James Wyatt, Robert J. Schwalb, Bruce R. Cordell, Chris Sims, and Steve Townshend, based on original material by E. Gary Gygax and Dave Arneson.',
  ...ogl.flatMap(([k, s]) => ['>', `> ${s.title} (\`${k}\`). Copyright ${s.year ?? ''}, ${s.publisher}${s.author ? `; Authors ${s.author}` : ''}.`]),
  '',
  '## Data',
  '',
  'Converted from the Open5e data set (https://github.com/open5e/open5e-api), whose code is MIT licensed and whose',
  'content keeps the licenses above.',
  '',
];
writeFileSync(join(OUT, 'ATTRIBUTION.md'), md.join('\n'));

// The OGL must travel with OGL content; copy its text from the data set rather than retyping it.
mkdirSync(join(OUT, 'licenses'), { recursive: true });
const oglText = JSON.parse(readFileSync(join(from, 'License.json'), 'utf8')).find((l: any) => l.pk === 'ogl-10a').fields.desc;
writeFileSync(join(OUT, 'licenses', 'OGL-1.0a.txt'), `${text(oglText)}\n`);

writeFileSync(join(OUT, 'catalog.json'), `${JSON.stringify(report, null, 2)}\n`);
for (const [kind, r] of Object.entries(report)) {
  const open = Object.values(r.open as Record<string, number>).reduce((a, b) => a + b, 0);
  console.log(`${kind.padEnd(12)} srd ${String(r.srd2024 + r.srd2014Only).padStart(4)} (2024 ${r.srd2024}, 2014-only ${r.srd2014Only}, merged ${r.srd2014Merged})  open ${String(open).padStart(5)}  dropped ${r.openDuplicatesDropped}`);
}
