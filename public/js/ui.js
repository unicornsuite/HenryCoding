// DOM-Oberfläche: Lobby, HUD, Shop, Minikarte, Meldungen
import {
  WEAPONS, WEAPON_ORDER, TURRETS, TURRET_ORDER, ITEMS, ITEM_ORDER, PLAYER_COLORS, GENERATOR_HP,
  DIFFS, DIFF_ORDER, UPGRADE, weaponStats, PERKS, PERK_ORDER, MAX_PERKS, POWERUPS, CRATE, GRENADE, BARRICADE,
} from '/shared/config.js';
import { MAPS, MAP_ORDER } from '/shared/maps.js';

const $ = id => document.getElementById(id);
const esc = s => String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));

const WEAPON_ICON = { pistol: '🔫', smg: '💨', shotgun: '💥', rifle: '🎖', sniper: '🎯', launcher: '💣', minigun: '⚙' };
const TURRET_ICON = { mg: '🗼', cannon: '💣', tesla: '⚡' };
const ITEM_ICON = { ammo: '📦', nades: '💣', heal: '❤', armor: '🛡', repair: '🔧', fixturrets: '🛠', fixbarr: '🪵' };
const MAP_STYLE = {
  ruins: ['linear-gradient(#5c6b74 0 55%, #3a3a33 55%)', '🏚'],
  suburb: ['linear-gradient(#8fd3ff 0 55%, #6abf4b 55%)', '🏘'],
  farm: ['linear-gradient(#ffb074 0 55%, #8bc34a 55%)', '🌽'],
  base: ['linear-gradient(#a7b4c2 0 55%, #9a9a7a 55%)', '🪖'],
  graveyard: ['linear-gradient(#0b1026 0 55%, #34502f 55%)', '🪦'],
};

// Bestleistungen im Browser speichern
export const records = {
  data: { best: {}, kills: 0, games: 0 },
  load() { try { Object.assign(this.data, JSON.parse(localStorage.getItem('za-records') || '{}')); } catch { /* ohne Speicher */ } },
  save() { try { localStorage.setItem('za-records', JSON.stringify(this.data)); } catch { /* egal */ } },
  key(map, diff) { return `${map}:${diff}`; },
  best(map, diff) { return this.data.best[this.key(map, diff)] || 0; },
  /** Trägt ein Ergebnis ein; gibt true zurück, wenn es ein neuer Rekord ist */
  report(map, diff, wave, kills) {
    this.data.games++; this.data.kills += kills || 0;
    const k = this.key(map, diff), isNew = wave > (this.data.best[k] || 0);
    if (isNew) this.data.best[k] = wave;
    this.save();
    return isNew;
  },
};
records.load();

export const ui = {
  h: null,
  shopTab: 'weapons',
  diff: 'normal',

  init(h) {
    this.h = h;
    // Farben
    const cw = $('colors');
    for (const c of PLAYER_COLORS) {
      const b = document.createElement('button'); b.style.background = c; b.dataset.c = c;
      b.onclick = () => h.onColor(c); cw.appendChild(b);
    }
    // Karten
    const mw = $('maps');
    for (const id of MAP_ORDER) {
      const m = MAPS[id], [bg, emo] = MAP_STYLE[id];
      const b = document.createElement('button'); b.className = 'mapcard'; b.dataset.map = id;
      b.innerHTML = `<div class="pic" style="background:${bg}">${emo}</div><div class="txt"><b>${esc(m.name)}</b><p>${esc(m.desc)}</p><span class="rec"></span></div>`;
      if (m.unityOnly) { b.classList.add('unityonly'); b.title = 'Diese Karte gibt es nur in der Unity-App'; }
      b.onclick = () => h.onMap(id);
      mw.appendChild(b);
    }
    // Schwierigkeit
    const dw = $('diffs');
    for (const id of DIFF_ORDER) {
      const d = DIFFS[id];
      const b = document.createElement('button'); b.dataset.diff = id;
      b.innerHTML = `${esc(d.name)}<small>${esc(d.desc)}</small>`;
      b.onclick = () => h.onDiff(id);
      dw.appendChild(b);
    }
    $('name').addEventListener('change', e => h.onName(e.target.value));
    $('name').addEventListener('keydown', e => { if (e.key === 'Enter') e.target.blur(); e.stopPropagation(); });
    $('startbtn').onclick = () => h.onStart();
    $('resume').onclick = () => h.onResume();
    $('leave').onclick = () => h.onLeave();
    $('tolobby').onclick = () => h.onBackToLobby();
    $('shopclose').onclick = () => h.onCloseShop();
    for (const b of document.querySelectorAll('.tabs button')) b.onclick = () => { this.shopTab = b.dataset.tab; this.renderShop(this._shopState); };
    for (const id of ['sens', 'vol', 'fov', 'quality', 'autofire']) $(id).addEventListener('input', () => h.onSettings(this.readSettings()));
  },

  readSettings() { return { sens: +$('sens').value, vol: +$('vol').value, fov: +$('fov').value, quality: $('quality').value, autofire: $('autofire').checked }; },
  writeSettings(s) { $('sens').value = s.sens; $('vol').value = s.vol; $('fov').value = s.fov; $('quality').value = s.quality; $('autofire').checked = !!s.autofire; $('name').value = s.name || ''; },

  show(id, on = true) { $(id).classList.toggle('hidden', !on); },
  isShown(id) { return !$(id).classList.contains('hidden'); },

  setConn(text, cls) { const c = $('conn'); c.textContent = text; c.className = 'conn ' + (cls || ''); },

  renderLobby(lobby, myId, myColor, urls) {
    this.diff = lobby.diff || 'normal';
    for (const b of $('colors').children) b.classList.toggle('on', b.dataset.c === myColor);
    for (const b of $('maps').children) {
      b.classList.toggle('on', b.dataset.map === lobby.map); b.disabled = lobby.phase === 'game';
      const best = records.best(b.dataset.map, this.diff);
      b.querySelector('.rec').textContent = best ? `🏆 Rekord: Welle ${best}` : 'Noch kein Rekord';
    }
    for (const b of $('diffs').children) { b.classList.toggle('on', b.dataset.diff === this.diff); b.disabled = lobby.phase === 'game'; }
    $('plist').innerHTML = lobby.players.map(p => `<li><i style="background:${p.color}"></i>${esc(p.name)}${p.id === myId ? ' (du)' : ''}<small>${p.inGame ? 'im Spiel' : 'Lobby'}</small></li>`).join('');
    const sb = $('startbtn');
    sb.disabled = false;
    sb.textContent = lobby.phase === 'game' ? '▶ LAUFENDEM SPIEL BEITRETEN' : `▶ SPIEL STARTEN (${MAPS[lobby.map].name} · ${DIFFS[this.diff].name})`;
    if (urls) {
      const list = urls.length ? urls.map(u => `<code>${esc(u)}</code>`).join('<br>') : `<code>${esc(location.origin)}</code>`;
      const stats = records.data.games ? `<br><br>📊 Gespielt: ${records.data.games} Runden · ${records.data.kills.toLocaleString('de-DE')} Zombies erledigt` : '';
      $('share').innerHTML = `👥 <b>Freunde einladen:</b> Sie öffnen im selben WLAN diese Adresse im Browser (PC, Tablet oder Handy):<br>${list}${stats}`;
    }
  },

  // ---------------------------------------------------------- HUD
  updateHUD(d) {
    $('wave').textContent = d.wave > 0 ? `WELLE ${d.wave}` : 'VORBEREITUNG';
    $('status').innerHTML = d.status;
    const gf = Math.max(0, d.genHp / GENERATOR_HP);
    $('genbar').style.width = (gf * 100) + '%';
    $('gentext').textContent = Math.round(gf * 100) + '%';
    $('hpbar').style.width = Math.max(0, d.hp / d.maxHp * 100) + '%';
    $('hptext').textContent = Math.max(0, Math.ceil(d.hp));
    $('armorbar').style.width = d.armor + '%';
    $('armortext').textContent = Math.ceil(d.armor);
    const mEl = $('money');
    const mt = '$ ' + d.money.toLocaleString('de-DE');
    if (mEl.textContent !== mt) { mEl.textContent = mt; mEl.classList.remove('flash'); void mEl.offsetWidth; mEl.classList.add('flash'); }
    $('lowhp').style.opacity = d.hp < 35 && d.state === 'alive' ? (1 - d.hp / 35) * 0.9 : 0;
    // Waffe
    const w = d.wstat;
    $('wname').textContent = w.name;
    $('wname').style.color = UPGRADE.colors[w.tier] === '#ffffff' ? '' : UPGRADE.colors[w.tier];
    $('mag').textContent = d.mag;
    $('reserve').textContent = d.weapon === 'pistol' ? '/ ∞' : '/ ' + d.reserve;
    $('ammo').classList.toggle('low', d.mag <= Math.ceil(w.mag * 0.25));
    this.show('reloadtxt', d.reloading);
    const sl = d.owned.map(id => `<div data-slot="${id}" class="${id === d.weapon ? 'on' : ''}">${WEAPONS[id].slot} ${WEAPON_ICON[id]}${d.upg[id] ? `<sup>${'★'.repeat(d.upg[id])}</sup>` : ''}</div>`).join('');
    if ($('slots').innerHTML !== sl) $('slots').innerHTML = sl;
    const nd = $('nades'); const nt = `💣 ${d.nades}`;
    if (nd.textContent !== nt) nd.textContent = nt; nd.classList.toggle('none', d.nades <= 0);
    const pi = d.perks.map(k => `<span title="${esc(PERKS[k].name)}">${PERKS[k].icon}</span>`).join('');
    if ($('perkicons').innerHTML !== pi) $('perkicons').innerHTML = pi;
    // Power-ups
    const pu = d.powerups.map(([k, t]) => `<div class="pu" style="color:${POWERUPS[k].color};border-color:${POWERUPS[k].color}">${POWERUPS[k].icon} ${POWERUPS[k].name} ${Math.ceil(t)}s</div>`).join('');
    if ($('powerups').innerHTML !== pu) $('powerups').innerHTML = pu;
    $('freeze').style.opacity = d.powerups.some(([k]) => k === 'freeze') ? 1 : 0;
    // Boss
    if (d.boss) { this.show('bossbar'); $('bossbar').firstElementChild.style.width = d.boss * 100 + '%'; } else this.show('bossbar', false);
    // Team
    const th = d.team.map(p => `<div class="tm ${p.state}" style="border-color:${p.color}">${esc(p.name)}${p.ready ? ' ✔' : ''}<small>$${p.money}</small><div class="bar"><div style="width:${p.state === 'alive' ? Math.min(100, p.hp / (p.maxHp || 100) * 100) : 0}%"></div></div></div>`).join('');
    if ($('team').innerHTML !== th) $('team').innerHTML = th;
  },

  crosshair(spreadPx, visible) {
    const c = $('crosshair'); c.style.display = visible ? 'block' : 'none';
    const s = 5 + spreadPx;
    c.children[0].style.top = (-s - 9) + 'px'; c.children[1].style.top = s + 'px';
    c.children[2].style.left = (-s - 9) + 'px'; c.children[3].style.left = s + 'px';
  },
  hitmarker(kill) {
    const h = $('hitmarker'); h.classList.toggle('kill', !!kill);
    h.style.transition = 'none'; h.style.opacity = 1; void h.offsetWidth;
    h.style.transition = 'opacity .3s'; h.style.opacity = 0;
  },
  hurt(amount) {
    const f = $('hurtflash'); f.style.transition = 'none'; f.style.opacity = Math.min(1, 0.4 + amount / 30); void f.offsetWidth;
    f.style.transition = 'opacity .5s'; f.style.opacity = 0;
  },
  nukeFlash() {
    const f = $('nukeflash'); f.style.transition = 'none'; f.style.opacity = 1; void f.offsetWidth;
    f.style.transition = 'opacity 1.4s'; f.style.opacity = 0;
  },
  prompt(text) { const p = $('prompt'); if (text) { p.innerHTML = text; p.style.display = 'block'; } else p.style.display = 'none'; },
  progress(frac) { if (frac == null) this.show('progress', false); else { this.show('progress'); $('progress').firstElementChild.style.width = frac * 100 + '%'; } },
  placeHint(text) { const p = $('placehint'); if (text) { p.innerHTML = text; p.classList.remove('hidden'); } else p.classList.add('hidden'); },
  downed(text) { if (text == null) this.show('downed', false); else { this.show('downed'); $('downedtxt').innerHTML = text; } },
  scope(on) { this.show('scope', on); },

  announce(big, small = '', dur = 2.6) {
    const a = $('announce');
    a.querySelector('.big').textContent = big; a.querySelector('.small').innerHTML = small;
    a.classList.add('show'); this.show('wavesum', false);
    clearTimeout(this._annT); this._annT = setTimeout(() => a.classList.remove('show'), dur * 1000);
  },
  streak(text, color = '#ffd600') {
    const s = $('streak'); s.textContent = text; s.style.color = color;
    s.classList.remove('show'); void s.offsetWidth; s.classList.add('show');
    clearTimeout(this._stT); this._stT = setTimeout(() => s.classList.remove('show'), 1300);
  },
  /** Zusammenfassung nach einer Welle */
  waveSummary(e, myId, names) {
    const rows = Object.entries(e.kills || {}).sort((a, b) => b[1] - a[1]).map(([id, k]) => {
      const n = names(+id);
      return `<div class="row ${+id === myId ? 'me' : ''}"><span><i style="display:inline-block;width:10px;height:10px;border-radius:3px;background:${n.color};margin-right:6px"></i>${esc(n.name)}</span><span>${k} Kills</span></div>`;
    }).join('');
    const mvp = e.mvp && e.mvp.kills > 0 ? `<div class="mvp">🏆 MVP: <span style="color:${e.mvp.color}">${esc(e.mvp.name)}</span> (${e.mvp.kills} Kills${Object.keys(e.kills || {}).length > 1 ? ', +$100' : ''})</div>` : '';
    $('wavesum').innerHTML = `<h3>WELLE ${e.w} GESCHAFFT!</h3>${mvp}${rows}<div class="hint">+$${e.bonus} Bonus für alle · Shop überall geöffnet <kbd>B</kbd></div>`;
    this.show('wavesum'); $('announce').classList.remove('show');
    clearTimeout(this._wsT); this._wsT = setTimeout(() => this.show('wavesum', false), 6000);
  },
  crateSpin(active, icon) {
    const c = $('crateui');
    c.style.display = active ? 'block' : 'none';
    if (active) c.textContent = icon;
  },
  feed(html) {
    const f = $('feed'); const d = document.createElement('div'); d.innerHTML = html; f.prepend(d);
    while (f.children.length > 6) f.lastChild.remove();
    setTimeout(() => d.remove(), 5200);
  },
  toast(text, kind = '', secs = 2.6) {
    const t = $('toast'); const d = document.createElement('div'); d.className = kind; d.textContent = text; t.appendChild(d);
    setTimeout(() => d.remove(), secs * 1000);
  },
  chat(m) {
    const log = $('chatlog'); const d = document.createElement('div');
    if (m.sys) { d.className = 'sys'; d.textContent = m.text; }
    else d.innerHTML = `<span style="color:${m.color}">${esc(m.name)}:</span> ${esc(m.text)}`;
    log.appendChild(d);
    while (log.children.length > 8) log.firstChild.remove();
  },
  openChat(on) {
    const i = $('chatinput');
    this.show('chatinput', on); $('hud').classList.toggle('chatting', on);
    if (on) { i.value = ''; setTimeout(() => i.focus(), 0); } else i.blur();
  },

  scoreboard(on, players) {
    this.show('scoreboard', on);
    if (!on) return;
    $('scoreboard').innerHTML = `<h2>Punktestand</h2><table class="stats"><tr><th>Spieler</th><th>Kills</th><th>Geld</th><th>Status</th></tr>` +
      players.sort((a, b) => b.kills - a.kills).map(p => `<tr><td><i style="background:${p.color}"></i>${esc(p.name)}</td><td>${p.kills}</td><td>$${p.money}</td><td>${{ alive: '🟢 aktiv', down: '🔴 am Boden', dead: '💀 tot' }[p.state]}</td></tr>`).join('') + '</table>';
  },

  showOver(m) {
    $('overreason').textContent = m.reason;
    $('overwave').textContent = `Erreichte Welle: ${m.wave}  ·  ${m.map} · ${DIFFS[m.diff]?.name || ''}`;
    const isNew = m.mapId ? records.report(m.mapId, m.diff || 'normal', m.wave, m.stats.reduce((a, s) => a + s.kills, 0)) : false;
    const r = $('overrecord');
    if (isNew && m.wave > 0) { r.textContent = '🏆 NEUER REKORD!'; r.classList.remove('hidden'); } else r.classList.add('hidden');
    $('overstats').innerHTML = '<tr><th>Spieler</th><th>Kills</th><th>Schaden</th><th>Wiederbelebt</th></tr>' +
      m.stats.map(s => `<tr><td><i style="background:${s.color}"></i>${esc(s.name)}</td><td>${s.kills}</td><td>${s.damage}</td><td>${s.revives}</td></tr>`).join('');
    this.show('over');
  },

  // ---------------------------------------------------------- Shop
  renderShop(st) {
    this._shopState = st;
    if (!st) return;
    $('shopmoney').textContent = '$ ' + st.money.toLocaleString('de-DE');
    for (const b of document.querySelectorAll('.tabs button')) b.classList.toggle('on', b.dataset.tab === this.shopTab);
    const warn = $('shopwarn');
    if (st.canShop) warn.classList.add('hidden'); else { warn.classList.remove('hidden'); warn.textContent = st.warn; }
    const grid = $('shopgrid');
    const bar = (label, frac) => `<div><span>${label}</span><em><i style="width:${Math.max(4, Math.min(100, frac * 100))}%"></i></em></div>`;
    const money = v => '$ ' + v.toLocaleString('de-DE');
    const dis = price => !st.canShop || st.money < price ? 'disabled' : '';
    let html = '';
    if (this.shopTab === 'weapons') {
      for (const id of WEAPON_ORDER) {
        const w = weaponStats(id, st.upg[id] || 0, st.perks), owned = st.owned.includes(id);
        const dps = w.damage * w.pellets / w.rate;
        let btn;
        if (owned) btn = st.weapon === id ? `<button class="owned" disabled>✔ Ausgerüstet</button>` : `<button class="equip" data-equip="${id}">Ausrüsten [${w.slot}]</button>`;
        else btn = `<button data-buy="${id}" ${dis(w.price)}>${money(w.price)}</button>`;
        html += `<div class="card ${!owned && st.money < w.price ? 'cant' : ''}"><div class="icon">${WEAPON_ICON[id]}</div><b>${w.name}</b><p>${w.desc}</p>
          <div class="stats">${bar('Schaden', w.damage * w.pellets / 200)}${bar('Feuerrate', 0.08 / w.rate + 0.05)}${bar('DPS', dps / 650)}${bar('Magazin', w.mag / 60)}</div>${btn}</div>`;
      }
      html += `<div class="card"><div class="icon">🎁</div><b>Glückskiste</b><p>${CRATE.desc} Steht neben dem Generator – hingehen und <kbd>E</kbd> drücken.</p><button class="owned" disabled>${money(CRATE.price)} an der Kiste</button></div>`;
    } else if (this.shopTab === 'upgrades') {
      for (const id of WEAPON_ORDER) {
        if (!st.owned.includes(id)) continue;
        const tier = st.upg[id] || 0, cur = weaponStats(id, tier, st.perks);
        const next = tier < UPGRADE.max ? weaponStats(id, tier + 1, st.perks) : null;
        const cost = next ? UPGRADE.cost[tier + 1] : 0;
        html += `<div class="card ${next && st.money < cost ? 'cant' : ''}"><div class="icon">${WEAPON_ICON[id]}</div><b>${WEAPONS[id].name} <span class="tier">${UPGRADE.names[tier]}</span></b>
          <p>${next ? `Stufe ${tier + 1}: Schaden ${Math.round(cur.damage)} → <b>${Math.round(next.damage)}</b>, Magazin ${cur.mag} → <b>${next.mag}</b>${tier + 1 === 3 ? ', +1 Durchschlag' : ''}` : 'Voll aufgewertet – ein Prachtstück.'}</p>
          <div class="stats">${bar('Stufe', tier / UPGRADE.max)}</div>
          ${next ? `<button data-upgrade="${id}" ${dis(cost)}>⬆ ${money(cost)}</button>` : '<button class="max" disabled>★★★ MAX</button>'}</div>`;
      }
      html += `<div class="card"><div class="icon">ℹ</div><b>Aufwerten</b><p>Jede Stufe macht eine Waffe deutlich stärker und vergrößert das Magazin. Stufe 3 durchschlägt zusätzlich einen Zombie mehr. Aufgewertete Waffen leuchten farbig.</p></div>`;
    } else if (this.shopTab === 'perks') {
      for (const id of PERK_ORDER) {
        const pk = PERKS[id], has = st.perks.includes(id);
        const full = !has && st.perks.length >= MAX_PERKS;
        html += `<div class="card ${!has && st.money < pk.price ? 'cant' : ''}"><div class="perkicon">${pk.icon}</div><b>${pk.name}</b><p>${pk.desc}</p>
          ${has ? '<button class="owned" disabled>✔ Aktiv</button>' : full ? `<button disabled>Max. ${MAX_PERKS} Perks</button>` : `<button data-buy="${id}" ${dis(pk.price)}>${money(pk.price)}</button>`}</div>`;
      }
      html += `<div class="card"><div class="icon">ℹ</div><b>Perks</b><p>Dauerhafte Vorteile für diese Runde (max. ${MAX_PERKS}). Du hast ${st.perks.length}/${MAX_PERKS}.</p></div>`;
    } else if (this.shopTab === 'turrets') {
      for (const id of TURRET_ORDER) {
        const t = TURRETS[id];
        html += `<div class="card ${st.money < t.price ? 'cant' : ''}"><div class="icon">${TURRET_ICON[id]}</div><b>${t.name}</b><p>${t.desc}</p>
          <div class="stats">${bar('Schaden', t.damage / 120)}${bar('Reichweite', t.range / 22)}${bar('Feuerrate', 0.15 / t.rate)}${bar('Haltbarkeit', t.hp / 600)}</div>
          <button data-turret="${id}" ${dis(t.price)}>${money(t.price)} · Platzieren</button></div>`;
      }
      html += `<div class="card ${st.money < BARRICADE.price ? 'cant' : ''}"><div class="icon">🪵</div><b>${BARRICADE.name}</b><p>${BARRICADE.desc}</p>
          <div class="stats">${bar('Haltbarkeit', BARRICADE.hp / 600)}${bar('Breite', 2.6 / 3)}</div>
          <button data-turret="barricade" ${dis(BARRICADE.price)}>${money(BARRICADE.price)} · Platzieren (${st.barricades}/${BARRICADE.max})</button></div>`;
      html += `<div class="card"><div class="icon">ℹ</div><b>So funktioniert's</b><p>Kaufen → Turm erscheint als Vorschau vor dir → mit Linksklick auf freien Boden setzen (grün = ok). Türme schießen automatisch, blockieren Zombies und können zerstört werden. Max. 16 Türme im Team.</p></div>`;
    } else {
      for (const id of ITEM_ORDER) {
        const it = ITEMS[id];
        if (it.unityOnly) continue; // Schaufel/Bretter gibt es nur in der Unity-Version
        const extra = id === 'nades' ? ` (${st.nades}/${GRENADE.max})` : '';
        html += `<div class="card ${st.money < it.price ? 'cant' : ''}"><div class="icon">${ITEM_ICON[id]}</div><b>${it.name}${extra}</b><p>${it.desc}</p>
          <button data-buy="${id}" ${dis(it.price)}>${money(it.price)}</button></div>`;
      }
    }
    grid.innerHTML = html;
    for (const b of grid.querySelectorAll('[data-buy]')) b.onclick = () => this.h.onBuy(b.dataset.buy);
    for (const b of grid.querySelectorAll('[data-upgrade]')) b.onclick = () => this.h.onUpgrade(b.dataset.upgrade);
    for (const b of grid.querySelectorAll('[data-equip]')) b.onclick = () => this.h.onEquip(b.dataset.equip);
    for (const b of grid.querySelectorAll('[data-turret]')) b.onclick = () => this.h.onTurret(b.dataset.turret);
  },

  // ---------------------------------------------------------- Minikarte
  buildMinimapBase(map) {
    const S = 190, c = document.createElement('canvas'); c.width = c.height = S;
    const g = c.getContext('2d');
    const k = S / map.size, half = map.size / 2;
    const tx = x => (x + half) * k;
    g.fillStyle = map.ground; g.fillRect(0, 0, S, S);
    g.fillStyle = 'rgba(0,0,0,0.25)'; g.fillRect(0, 0, S, S);
    for (const d of map.decals) { g.fillStyle = d.color; g.fillRect(tx(d.x - d.w / 2), tx(d.z - d.d / 2), d.w * k, d.d * k); }
    for (const o of map.objects) {
      if (o.kind === 'platform') continue;
      g.fillStyle = o.kind === 'corn' ? 'rgba(200,200,50,0.5)' : (o.kind === 'tree' || o.kind === 'pine') ? '#2e7d32' : o.color;
      g.fillRect(tx(o.x - o.w / 2), tx(o.z - o.d / 2), Math.max(1.5, o.w * k), Math.max(1.5, o.d * k));
    }
    g.strokeStyle = '#000'; g.lineWidth = 3; g.strokeRect(0, 0, S, S);
    this._mmBase = c; this._mmMap = map;
  },
  drawMinimap(me, players, zombies, turrets, pickups = [], barricades = []) {
    const map = this._mmMap; if (!map) return;
    const cv = $('minimap'), g = cv.getContext('2d'), S = 190, k = S / map.size, half = map.size / 2;
    const tx = x => (x + half) * k;
    g.drawImage(this._mmBase, 0, 0);
    // Generator & Shop & Kiste
    g.fillStyle = '#00e5ff'; g.fillRect(tx(map.gen.x) - 5, tx(map.gen.z) - 5, 10, 10);
    g.strokeStyle = '#000'; g.lineWidth = 1.5; g.strokeRect(tx(map.gen.x) - 5, tx(map.gen.z) - 5, 10, 10);
    g.fillStyle = '#ffca28'; g.fillRect(tx(map.shop.x) - 3, tx(map.shop.z) - 3, 6, 6);
    if (map.crate) { g.fillStyle = '#ff9100'; g.fillRect(tx(map.crate.x) - 3, tx(map.crate.z) - 3, 6, 6); }
    for (const b of barricades) { g.fillStyle = '#a1887f'; const w = b.rot ? 1.5 : 2.6 * k, d = b.rot ? 2.6 * k : 1.5; g.fillRect(tx(b.x) - w / 2, tx(b.z) - d / 2, w, d); }
    for (const t of turrets) { g.fillStyle = '#90a4ae'; g.fillRect(tx(t.x) - 3, tx(t.z) - 3, 6, 6); g.strokeRect(tx(t.x) - 3, tx(t.z) - 3, 6, 6); }
    for (const p of pickups) { g.fillStyle = POWERUPS[p.kind].color; g.beginPath(); g.arc(tx(p.x), tx(p.z), 4, 0, Math.PI * 2); g.fill(); g.stroke(); }
    for (const z of zombies) {
      g.fillStyle = z.type === 'boss' ? '#d500f9' : z.type === 'exploder' ? '#ff9100' : z.type === 'spitter' ? '#aeea00' : '#ff1744';
      const r = z.type === 'boss' ? 4.5 : z.type === 'brute' ? 3.2 : z.type === 'crawler' ? 1.7 : 2.2;
      g.beginPath(); g.arc(tx(z.x), tx(z.z), r, 0, Math.PI * 2); g.fill();
    }
    const arrow = (x, z, yaw, color, size) => {
      g.save(); g.translate(tx(x), tx(z)); g.rotate(-yaw);
      g.fillStyle = color; g.strokeStyle = '#000'; g.lineWidth = 1.5;
      g.beginPath(); g.moveTo(0, -size); g.lineTo(size * 0.7, size * 0.7); g.lineTo(0, size * 0.3); g.lineTo(-size * 0.7, size * 0.7); g.closePath(); g.fill(); g.stroke();
      g.restore();
    };
    for (const p of players) if (p.state !== 'dead') arrow(p.x, p.z, p.yaw, p.state === 'down' ? '#ff5252' : p.color, 5);
    if (me) arrow(me.x, me.z, me.yaw, '#ffffff', 7);
  },
};
export { esc };
