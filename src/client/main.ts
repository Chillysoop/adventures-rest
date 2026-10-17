// Browser client: runs the simulation in real time with pause, draws the town, and shows the
// Ledger, the bounty board, Chronicles and an inspector beside the map.

import { HOURS_PER_DAY, Importance, Simulation, competence, formatAbilities, pendingMotion, type Agent, type LedgerEntry } from '../sim/index.ts';
import { suggestedReward } from '../sim/systems/quests.ts';
import { MapView, type Selection } from './map.ts';

/** In-game hours per real second for each speed button; index 0 is paused. */
const SPEEDS = [0, 1, 4, 12, 36];
const LEDGER_LIMIT = 400;

const $ = <T extends HTMLElement>(sel: string): T => document.querySelector(sel) as T;
const esc = (s: string): string => s.replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[c]!);

let sim!: Simulation;
let map!: MapView;
let speed = 1;
let lastSpeed = 1;
let acc = 0;
let lastFrame = performance.now();
let tab: 'ledger' | 'council' | 'bounties' | 'chronicles' | 'inspect' = 'ledger';
let ledgerFilter: Importance = Importance.Notable;
let dirty = true;
let shownChronicles = -1;
const openChronicles = new Set<number>();

function newTown(seed: number): void {
  sim = new Simulation({ seed, autoBounties: false, autoCouncil: false });
  const canvas = $<HTMLCanvasElement>('#map');
  map = new MapView(canvas, sim);
  map.resize();
  $('#ledger-list').innerHTML = '';
  for (const e of sim.town.ledger.entries) addEntry(e);
  sim.town.ledger.onWrite((e) => {
    addEntry(e);
    if (e.importance === Importance.Major || (e.category === 'Council' && e.text.includes(' moves that '))) toast(e);
  });
  $('#town-name').textContent = sim.town.name;
  $('#seed').textContent = `seed ${seed}`;
  openChronicles.clear();
  shownChronicles = -1;
  dirty = true;
  setTab('ledger');
}

// ---------- Ledger ----------

function addEntry(e: LedgerEntry): void {
  const list = $('#ledger-list');
  const li = document.createElement('li');
  li.className = `entry imp-${e.importance} cat-${e.category.toLowerCase()}`;
  li.dataset.imp = String(e.importance);
  const day = Math.floor(e.tick / HOURS_PER_DAY) + 1;
  const hour = String(e.tick % HOURS_PER_DAY).padStart(2, '0');
  li.innerHTML = `<span class="when">Day ${day} · ${hour}:00</span><span class="cat">${e.category}</span><p>${linkNames(e)}</p>`;
  li.hidden = e.importance < ledgerFilter;
  list.prepend(li);
  while (list.children.length > LEDGER_LIMIT) list.lastElementChild!.remove();
}

/** Turn the names of agents an entry mentions into links that open the inspector. */
function linkNames(e: LedgerEntry): string {
  let html = esc(e.text);
  for (const id of e.agents) {
    const a = sim.town.agents[id - 1] ?? sim.town.agents.find((x) => x.id === id);
    if (!a) continue;
    for (const n of [a.name, a.givenName]) {
      const i = html.indexOf(esc(n));
      if (i >= 0) {
        html = `${html.slice(0, i)}<a href="#" data-agent="${a.id}">${esc(n)}</a>${html.slice(i + esc(n).length)}`;
        break;
      }
    }
  }
  return html;
}

function applyLedgerFilter(): void {
  for (const li of $('#ledger-list').children) (li as HTMLElement).hidden = Number((li as HTMLElement).dataset.imp) < ledgerFilter;
  for (const b of document.querySelectorAll<HTMLButtonElement>('[data-filter]')) b.classList.toggle('on', Number(b.dataset.filter) === ledgerFilter);
}

let toastTimer = 0;
function toast(e: LedgerEntry): void {
  const el = $('#toast');
  el.innerHTML = `<span class="cat">${e.category}</span> ${linkNames(e)}`;
  el.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = window.setTimeout(() => el.classList.remove('show'), 6000);
}

// ---------- Panels ----------

function setTab(t: typeof tab): void {
  tab = t;
  for (const b of document.querySelectorAll<HTMLButtonElement>('[data-tab]')) b.classList.toggle('on', b.dataset.tab === t);
  for (const p of document.querySelectorAll<HTMLElement>('.pane')) p.hidden = p.id !== `pane-${t}`;
  dirty = true;
}

function renderHeader(): void {
  const t = sim.town;
  const s = sim.snapshot();
  const hour = t.tick % HOURS_PER_DAY;
  $('#motion-dot').hidden = !pendingMotion(t);
  $('#clock').textContent = `Day ${t.day} · ${String(hour).padStart(2, '0')}:00`;
  setHTML($('#stats'), [
    stat('Population', String(s.population)),
    stat('Heroes', String(t.heroes.length)),
    stat('Treasury', `${Math.floor(t.treasury)}g`),
    stat('Bread', `${s.foodPrice.toFixed(2)}g`),
    stat('Mandate', `${Math.round(t.mandate)}`),
    stat('Homeless', String(s.homeless)),
  ].join(''));
}
const stat = (k: string, v: string): string => `<div class="stat"><span>${k}</span><b>${v}</b></div>`;

function renderBounties(): void {
  const t = sim.town;
  const rows = t.sites.map((s) => {
    const live = t.bounties.find((b) => b.siteId === s.def.id && (b.status === 'open' || b.status === 'taken'));
    const party = live?.partyId != null ? t.parties.find((p) => p.id === live.partyId) : undefined;
    const reward = suggestedReward(s.def.tier);
    let status: string;
    let action = '';
    if (s.cleared) status = '<span class="tag good">Cleared</span>';
    else if (party && !party.returned) status = `<span class="tag gold">${esc(party.name)} on the road</span>`;
    else if (live) status = `<span class="tag gold">Bounty posted: ${live.reward}g</span>`;
    else {
      status = s.attempts ? `<span class="tag bad">${s.attempts} failed ${s.attempts === 1 ? 'attempt' : 'attempts'}</span>` : '<span class="tag">Unexplored</span>';
      const afford = t.treasury >= reward;
      action = `<button class="post" data-site="${s.def.id}" data-reward="${reward}" ${afford ? '' : 'disabled'}>Post ${reward}g</button>`;
    }
    const tier = '◆'.repeat(s.def.tier) + '◇'.repeat(3 - s.def.tier);
    return `<li class="site"><div><b>${esc(cap(s.def.name))}</b> <span class="tier" title="Tier ${s.def.tier}">${tier}</span><div>${status}</div></div>${action}</li>`;
  });
  const books = Object.entries(t.accountsYesterday)
    .filter(([, v]) => Math.abs(v) >= 0.5)
    .sort((x, y) => y[1] - x[1] || (x[0] < y[0] ? -1 : 1));
  const net = books.reduce((s, [, v]) => s + v, 0);
  setHTML(
    $('#books'),
    books.length
      ? `<h4>Yesterday's books <b class="${net >= 0 ? 'good' : 'bad'}">${net >= 0 ? '+' : '−'}${Math.abs(Math.round(net))}g</b></h4><ul>${books
          .map(([k, v]) => `<li><span>${esc(k)}</span><b class="${v >= 0 ? 'good' : 'bad'}">${v >= 0 ? '+' : '−'}${Math.abs(Math.round(v))}g</b></li>`)
          .join('')}</ul>`
      : '',
  );
  const idle = t.heroes.filter((a) => !a.isAway).length;
  setHTML($('#bounty-list'), rows.join(''));
  $('#bounty-note').textContent =
    t.taverns.length === 0
      ? 'Heroes need a tavern before they will come to town.'
      : `${idle} ${idle === 1 ? 'hero is' : 'heroes are'} in town. Parties need at least three, and tougher sites want higher levels.`;
}

function meter(value: number, cls = ''): string {
  return `<span class="meter ${cls}"><i style="width:${Math.round(Math.max(0, Math.min(100, value)))}%"></i></span>`;
}

function renderCouncil(): void {
  const t = sim.town;
  const pending = pendingMotion(t);
  const sign = (n: number): string => (n >= 0 ? `+${n}` : `${n}`);
  const seats = t.seats
    .map((s) => {
      if (s.holderId === null)
        return `<li class="seat vacant"><b>${esc(s.def.title)}</b><small>${
          t.population < s.def.unlockPopulation ? `Opens when ${s.def.unlockPopulation} people live here` : 'Vacant, to be filled tomorrow'
        }</small></li>`;
      const a = t.agent(s.holderId);
      const mood = s.loyalty >= 60 ? 'Loyal' : s.loyalty >= 25 ? 'Wary' : 'Disloyal';
      return `<li class="seat"><div class="seat-top"><b>${esc(s.def.title)}</b><span class="tag ${s.loyalty < 25 ? 'bad' : s.loyalty >= 60 ? 'good' : ''}">${mood}</span></div>
        ${agentLink(a)} <small>· ${esc(a.race)} ${esc(a.profession)} · skill ${sign(competence(t, s))}</small>
        <div class="row"><span>Loyalty</span>${meter(s.loyalty, s.loyalty < 25 ? 'bad' : '')}</div>
        <small>Minds ${esc(s.def.portfolio)}. Wants ${esc(s.def.agenda)}.</small></li>`;
    })
    .join('');
  let motion = '<p class="empty">No motion before the council. Councillors bring one when their portfolio needs it.</p>';
  if (pending) {
    const yes = pending.votes.filter((v) => v.yes).length;
    const no = pending.votes.length - yes;
    const majority = yes === no ? null : yes > no;
    const hoursLeft = pending.deadlineTick - t.tick;
    const votes = pending.votes
      .map((v) => {
        const s = t.seats.find((x) => x.def.id === v.seat)!;
        return `<li><span>${esc(s.def.title)}</span><b class="${v.yes ? 'good' : 'bad'}">${v.yes ? 'Aye' : 'Nay'}</b></li>`;
      })
      .join('');
    const decree = (signIt: boolean): string => (majority !== null && majority !== signIt ? ' (decree)' : '');
    motion = `<div class="motion"><p><b>${esc(cap(pending.text))}.</b></p>
      <p class="sub">Moved by ${agentLink(t.agent(pending.proposerId))}. ${
        pending.cost ? `Costs ${pending.cost}g.` : ''
      } If you say nothing in ${Math.ceil(hoursLeft / HOURS_PER_DAY)} ${hoursLeft > HOURS_PER_DAY ? 'days' : 'day'}, the council's vote stands.</p>
      <ul class="votes">${votes}</ul>
      <div class="actions"><button class="post" data-motion="${pending.id}" data-sign="1">Sign${decree(true)}</button>
      <button class="ghost" data-motion="${pending.id}" data-sign="0">Veto${decree(false)}</button></div>
      <p class="note">Going against the council's majority is a decree: it costs Mandate and angers the councillors you overrule.</p></div>`;
  }
  const history = t.motions
    .filter((m) => m.status !== 'pending')
    .slice(-8)
    .reverse()
    .map(
      (m) =>
        `<li><span class="when">Day ${Math.floor(m.proposedTick / HOURS_PER_DAY) + 1}</span> ${esc(cap(m.text))} <span class="tag ${
          m.status === 'passed' ? 'good' : 'bad'
        }">${m.status === 'passed' ? 'passed' : 'failed'}${m.decree ? ' by decree' : ''}</span></li>`,
    )
    .join('');
  setHTML(
    $('#pane-council'),
    `<div class="mandate"><div class="row"><span>Mandate</span>${meter(t.mandate, t.mandate < 30 ? 'bad' : '')}<b>${Math.round(t.mandate)}</b></div>
      <small>The townsfolk's approval of you. It follows their mood and falls when you rule by decree. A popular mayor draws settlers.</small>
      <div class="facts"><span>Tax ${Math.round(t.taxRate * 100)}%</span><span>Watch ${t.watch}</span>${t.festivalDays ? '<span>Festival!</span>' : ''}</div></div>
    <h4>Before the council</h4>${motion}
    <h4>Seats</h4><ul class="seats">${seats}</ul>
    ${history ? `<h4>Past motions</h4><ul class="history">${history}</ul>` : ''}`,
  );
}

function renderChronicles(): void {
  shownChronicles = sim.town.chronicles.length;
  const list = [...sim.town.chronicles].reverse();
  $('#chronicle-list').innerHTML = list.length
    ? list
        .map((c) => {
          const open = openChronicles.has(c.id);
          return `<li class="chron ${c.outcome}"><button class="chron-head" data-chron="${c.id}">
            <b>${esc(c.partyName)}</b> at ${esc(c.siteName)}<span class="tag ${c.outcome === 'cleared' ? 'good' : c.outcome === 'wiped' ? 'bad' : ''}">${c.outcome}</span>
            <small>Day ${c.departDay}–${c.returnDay} · ${c.members.map(esc).join(', ')}</small></button>
            ${open ? `<ol>${c.lines.map((l) => `<li>${esc(l)}</li>`).join('')}</ol>` : ''}</li>`;
        })
        .join('')
    : '<li class="empty">No expeditions yet. Post a bounty and wait for a party to form at the tavern.</li>';
}

function councilSeat(a: Agent): string {
  const s = sim.town.seats.find((x) => x.holderId === a.id);
  return s ? `<p><span class="tag gold">${esc(s.def.title)}</span> loyalty ${Math.round(s.loyalty)}</p>` : '';
}

function agentLink(a: Agent): string {
  return `<a href="#" data-agent="${a.id}">${esc(a.name)}</a>`;
}

function renderInspect(): void {
  const t = sim.town;
  const sel = map.selection;
  const el = $('#pane-inspect');
  let html = '';
  if (!sel) {
    html = '<p class="empty">Tap a building, a person or a site on the map.</p>';
  } else if (sel.kind === 'agent') {
    const a = t.agent(sel.id as number);
    const home = a.homeId !== null ? t.building(a.homeId).name : a.hero?.lodging ? 'a room at the tavern' : 'nowhere';
    const job = a.hero ? 'adventuring' : a.jobId !== null ? t.building(a.jobId).name : 'out of work';
    const spouse = a.spouseId !== null ? `<p>Married to ${agentLink(t.agent(a.spouseId))}</p>` : '';
    const friends = [...a.opinions].filter(([, v]) => v >= 40).sort((x, y) => x[0] - y[0]).map(([k]) => agentLink(t.agent(k)));
    const foes = [...a.opinions].filter(([, v]) => v <= -30).sort((x, y) => x[0] - y[0]).map(([k]) => agentLink(t.agent(k)));
    const h = a.hero;
    const heroInfo = h
      ? `<p>HP ${h.hp}/${h.maxHp} · ${h.xp} XP · ${h.kills} kills · ${h.expeditions} expeditions${h.items.length ? ` · ${h.items.map(esc).join(', ')}` : ''}</p>`
      : '';
    const status = a.departed ? (a.dead ? 'Dead' : 'Left town') : a.isAway ? 'Away on an expedition' : a.activity;
    const history = t.ledger
      .about(a.id)
      .filter((e) => e.id > 1)
      .slice(-12)
      .reverse()
      .map((e) => `<li><span class="when">Day ${Math.floor(e.tick / HOURS_PER_DAY) + 1}</span> ${linkNames(e)}</li>`)
      .join('');
    html = `<h3>${esc(a.name)}</h3><p class="sub">${esc(cap(a.describe().slice(a.name.length + 2)))} · ${esc(a.traitList())}</p>
      ${councilSeat(a)}${heroInfo}<p>${esc(status)} · ${Math.floor(a.gold)} gold · mood ${Math.round(a.lastDayMood)}</p>
      <p>Lives at ${esc(home)}; ${a.hero ? 'lives by bounties' : `works at ${esc(job)}`}.</p>${spouse}
      <p class="abil">${esc(formatAbilities(a.abilities))}</p>
      ${friends.length ? `<p>Friends: ${friends.join(', ')}</p>` : ''}${foes.length ? `<p>Grudges: ${foes.join(', ')}</p>` : ''}
      ${history ? `<h4>Story so far</h4><ul class="history">${history}</ul>` : ''}`;
  } else if (sel.kind === 'building') {
    const b = t.building(sel.id as number);
    const owner = b.ownerId !== null ? agentLink(t.agent(b.ownerId)) : 'the town';
    const people = (ids: number[]): string => ids.map((id) => agentLink(t.agent(id))).join(', ') || 'none';
    const here = t.agents.filter((a) => !a.departed && a.locationId === b.id && a.activity !== 'Sleeping');
    const state = b.isSite
      ? `Under construction, ${Math.floor((100 * b.buildProgress) / b.def.buildHours)}% done.`
      : b.closed
        ? 'Closed.'
        : b.forSale
          ? 'For sale.'
          : 'Open.';
    html = `<h3>${esc(cap(b.name))}</h3><p class="sub">${esc(cap(b.def.name))} · owned by ${owner}</p><p>${state}</p>
      ${b.def.jobs ? `<p>Workers (${b.workers.length}/${b.def.jobs}): ${people(b.workers)}</p><p>Cash ${Math.floor(b.cash)}g</p>` : ''}
      ${b.def.housing ? `<p>Residents (${b.residents.length}/${b.def.housing}): ${people(b.residents)}</p>` : ''}
      ${here.length ? `<p>Here now: ${here.map(agentLink).join(', ')}</p>` : ''}`;
  } else {
    const s = t.sites.find((x) => x.def.id === sel.id)!;
    const chrons = t.chronicles.filter((c) => c.siteName === s.def.name);
    html = `<h3>${esc(cap(s.def.name))}</h3><p class="sub">Tier ${s.def.tier} · ${s.def.distance} ${s.def.distance === 1 ? 'day' : 'days'} away</p>
      <p>${s.cleared ? 'Cleared. The road is safe.' : `Still dangerous. ${s.attempts} ${s.attempts === 1 ? 'party has' : 'parties have'} tried.`}</p>
      <p>Its master: ${esc(s.def.boss.kind === 'combat' ? s.def.boss.text : '')}.</p>
      ${chrons.length ? `<p>${chrons.length} ${chrons.length === 1 ? 'chronicle' : 'chronicles'} written here. See the Chronicles tab.</p>` : ''}`;
  }
  setHTML(el, html);
}

/** Re-render only when content changed, so clicks and text selection survive a running clock. */
function setHTML(el: HTMLElement, html: string): void {
  if (el.dataset.html !== html) {
    el.dataset.html = html;
    el.innerHTML = html;
  }
}

const cap = (s: string): string => s.charAt(0).toUpperCase() + s.slice(1);

function select(sel: Selection | null): void {
  map.selection = sel;
  if (sel) setTab('inspect');
  dirty = true;
}

// ---------- Time ----------

function setSpeed(i: number): void {
  if (i > 0) lastSpeed = i;
  speed = i;
  for (const b of document.querySelectorAll<HTMLButtonElement>('[data-speed]')) b.classList.toggle('on', Number(b.dataset.speed) === i);
  document.body.classList.toggle('paused', i === 0);
}

function frame(now: number): void {
  const dt = Math.min(0.1, (now - lastFrame) / 1000);
  lastFrame = now;
  acc += dt * SPEEDS[speed];
  let steps = 0;
  while (acc >= 1 && steps < 8) {
    sim.step();
    acc -= 1;
    steps++;
    dirty = true;
  }
  if (steps === 8) acc = 0;
  map.draw(sim.town, dt);
  if (dirty) {
    renderHeader();
    if (tab === 'bounties') renderBounties();
    else if (tab === 'council') renderCouncil();
    else if (tab === 'inspect') renderInspect();
    else if (tab === 'chronicles' && sim.town.chronicles.length !== shownChronicles) renderChronicles();
    dirty = false;
  }
  requestAnimationFrame(frame);
}

// ---------- Wiring ----------

function wire(): void {
  document.addEventListener('click', (ev) => {
    const el = (ev.target as HTMLElement).closest<HTMLElement>('[data-agent],[data-tab],[data-speed],[data-filter],[data-site],[data-chron],[data-motion]');
    if (!el) return;
    ev.preventDefault();
    if (el.dataset.agent) select({ kind: 'agent', id: Number(el.dataset.agent) });
    else if (el.dataset.tab) setTab(el.dataset.tab as typeof tab);
    else if (el.dataset.speed) setSpeed(Number(el.dataset.speed));
    else if (el.dataset.filter) {
      ledgerFilter = Number(el.dataset.filter) as Importance;
      applyLedgerFilter();
    } else if (el.dataset.motion) {
      sim.decideMotion(Number(el.dataset.motion), el.dataset.sign === '1');
      dirty = true;
    } else if (el.dataset.site) {
      sim.postBounty(el.dataset.site, Number(el.dataset.reward));
      dirty = true;
    } else if (el.dataset.chron) {
      const id = Number(el.dataset.chron);
      if (openChronicles.has(id)) openChronicles.delete(id);
      else openChronicles.add(id);
      renderChronicles();
    }
  });
  map.canvas.addEventListener('click', (ev) => {
    const r = map.canvas.getBoundingClientRect();
    select(map.pick(sim.town, ev.clientX - r.left, ev.clientY - r.top));
  });
  $('#toast').addEventListener('click', (ev) => {
    if (!(ev.target as HTMLElement).closest('a')) $('#toast').classList.remove('show');
  });
  $('#new-town').addEventListener('click', () => newTown(1 + Math.floor(Math.random() * 9999)));
  document.addEventListener('keydown', (ev) => {
    if ((ev.target as HTMLElement).tagName === 'INPUT') return;
    if (ev.code === 'Space') {
      ev.preventDefault();
      setSpeed(speed === 0 ? lastSpeed : 0);
    } else if (ev.key >= '1' && ev.key <= '4') setSpeed(Number(ev.key));
  });
  new ResizeObserver(() => map.resize()).observe(map.canvas);
}

// ?seed=N picks the town; ?day=N fast-forwards (handy for sharing a moment or checking a build).
const params = new URLSearchParams(location.search);
newTown(Number(params.get('seed')) || 1);
const skip = Number(params.get('day')) || 0;
if (skip > 0) sim.runDays(skip);
wire();
setSpeed(1);
applyLedgerFilter();
requestAnimationFrame(frame);
