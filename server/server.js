// Zombie-Ansturm – autoritativer Spielserver (HTTP + WebSocket)
import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { WebSocketServer } from 'ws';
import {
  TICK_RATE, START_MONEY, PREP_TIME_FIRST, PREP_TIME, WAVE_BONUS, HIT_REWARD, PLAYER_MAX_HP, PLAYER_MAX_ARMOR,
  REVIVE_TIME, BLEEDOUT_TIME, SHOP_RADIUS, MAX_TURRETS, PLAYER_COLORS, WEAPONS, WEAPON_ORDER, TURRETS, TURRET_ORDER,
  ITEMS, ZOMBIES, ZOMBIE_TYPES, GENERATOR_HP, waveComposition, waveHpMult, waveSpeedMult, spawnInterval, maxAlive,
  DIFFS, UPGRADE, weaponStats, GRENADE, MELEE, PERKS, MAX_PERKS, POWERUPS, POWERUP_ORDER, POWERUP_DROP, POWERUP_LIFE, CRATE, waveKind, BARRICADE,
} from '../shared/config.js';
import { MAPS, MAP_ORDER } from '../shared/maps.js';
import { getSolids, buildGrid, flowField, gridLOS, pushCircleOutOfBox, dist2, GEN_SIZE, barricadeBox } from '../shared/geom.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(__dirname, '..');
const PORT = Number(process.env.PORT) || 3000;
const START_CASH = Number(process.env.START_MONEY) || START_MONEY; // zum Testen überschreibbar

// ------------------------------------------------------------------ HTTP (statische Dateien)
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.css': 'text/css; charset=utf-8', '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.webmanifest': 'application/manifest+json' };
const ROUTES = [
  ['/shared/', path.join(ROOT, 'shared')],
  ['/vendor/three/', path.join(ROOT, 'node_modules', 'three')],
  ['/', path.join(ROOT, 'public')],
];

function lanUrls() {
  const out = [];
  for (const list of Object.values(os.networkInterfaces())) {
    for (const a of list || []) if (a.family === 'IPv4' && !a.internal) out.push(`http://${a.address}:${PORT}`);
  }
  return out;
}

const server = http.createServer((req, res) => {
  const url = decodeURIComponent((req.url || '/').split('?')[0]);
  if (url === '/info') {
    res.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
    return res.end(JSON.stringify({ urls: lanUrls(), port: PORT }));
  }
  for (const [prefix, dir] of ROUTES) {
    if (!url.startsWith(prefix)) continue;
    let rel = url.slice(prefix.length) || 'index.html';
    const file = path.resolve(dir, rel);
    if (!file.startsWith(dir)) { res.writeHead(403); return res.end(); }
    fs.stat(file, (err, st) => {
      if (err || !st.isFile()) { res.writeHead(404); return res.end('Nicht gefunden'); }
      res.writeHead(200, { 'Content-Type': MIME[path.extname(file)] || 'application/octet-stream', 'Cache-Control': 'no-cache' });
      fs.createReadStream(file).pipe(res);
    });
    return;
  }
  res.writeHead(404); res.end();
});

// ------------------------------------------------------------------ Spielzustand
let nextId = 1;
const players = new Map();          // id -> Spieler
const lobby = { map: 'suburb', diff: 'normal', phase: 'lobby' };
let G = null;                        // laufendes Spiel

const STATE_IDX = { alive: 0, down: 1, dead: 2 };
const r2 = v => Math.round(v * 100) / 100;

function send(p, msg) { if (p.ws.readyState === 1) p.ws.send(JSON.stringify(msg)); }
function broadcast(msg, except) { const s = JSON.stringify(msg); for (const p of players.values()) if (p !== except && p.ws.readyState === 1) p.ws.send(s); }
function lobbyMsg() {
  return { t: 'lobby', map: lobby.map, diff: lobby.diff, phase: lobby.phase, players: [...players.values()].map(p => ({ id: p.id, name: p.name, color: p.color, inGame: p.inGame })) };
}
function sendInv(p) { send(p, { t: 'inv', owned: [...p.owned], money: p.money, upg: p.upg, perks: [...p.perks], nades: p.nades, maxHp: p.maxHp }); }
function event(e) { if (G) G.events.push(e); }
function sysChat(text) { broadcast({ t: 'chat', sys: true, text }); }

function resetPlayerForGame(p, i) {
  const m = G.map;
  const ang = (i / Math.max(1, players.size)) * Math.PI * 2;
  p.x = m.spawn.x + Math.sin(ang) * 1.5; p.z = m.spawn.z + Math.cos(ang) * 1.5; p.y = 0;
  p.yaw = 0; p.pitch = 0; p.weapon = 'pistol'; p.moving = 0;
  p.maxHp = PLAYER_MAX_HP; p.hp = PLAYER_MAX_HP; p.armor = 0; p.money = START_CASH; p.kills = 0; p.damage = 0; p.revives = 0; p.downs = 0;
  p.state = 'alive'; p.downT = 0; p.reviveProg = 0; p.reviver = null; p.revivePing = 0; p.ready = false;
  p.owned = new Set(['pistol']); p.upg = {}; p.perks = new Set(); p.nades = 0; p.inGame = true; p.lives = 2; p.selfRev = 0;
  p.waveKills = 0; p.streak = 0; p.lastKillT = -99; p.crateT = 0; p.crateItem = null;
}

function startGame(mapId) {
  const map = MAPS[mapId] || MAPS.suburb;
  const solids = getSolids(map);
  const grid = buildGrid(map, solids);
  // Generator-Flussfeld: Quellen = freie Zellen um den Generator
  const genSrc = [];
  for (let gz = 0; gz < grid.n; gz++) for (let gx = 0; gx < grid.n; gx++) {
    const [x, z] = grid.center(gx, gz);
    if (!grid.blocked[gz * grid.n + gx] && dist2(x, z, map.gen.x, map.gen.z) < 3.4 * 3.4) genSrc.push([gx, gz]);
  }
  G = {
    map, solids, grid, genField: flowField(grid, genSrc), playerField: null, pfTimer: 0,
    diff: DIFFS[lobby.diff] || DIFFS.normal,
    zombies: [], turrets: [], genHp: GENERATOR_HP, wave: Number(process.env.START_WAVE) || 0, kind: 'normal', phase: 'prep', timer: PREP_TIME_FIRST, // START_WAVE nur zum Testen
    queue: [], spawnT: 0, events: [], time: 0, totalKills: 0,
    pickups: [], pu: { instakill: 0, double: 0, freeze: 0 }, spits: [],
    barricades: [], barrBoxes: [], genSrc,
  };
  lobby.phase = 'game'; lobby.map = map.id;
  let i = 0;
  for (const p of players.values()) { resetPlayerForGame(p, i++); }
  for (const p of players.values()) { send(p, startMsg(p)); sendInv(p); }
  broadcast(lobbyMsg());
  console.log(`Spiel gestartet: ${map.name} (${G.diff.name}) mit ${players.size} Spieler(n)`);
}
/** Barrikaden haben sich geändert: Kollisionskästen und Wegkosten neu berechnen */
function rebuildBarricades() {
  G.barrBoxes = G.barricades.map(barricadeBox);
  G.grid.setCosts(G.barrBoxes, 40);
  G.genField = flowField(G.grid, G.genSrc);
  G.pfTimer = 0;
}
function startMsg(p) { return { t: 'start', map: G.map.id, diff: lobby.diff, you: { x: p.x, y: p.y, z: p.z }, wave: G.wave }; }

function endGame(reason) {
  if (!G) return;
  const stats = [...players.values()].filter(p => p.inGame).map(p => ({ name: p.name, color: p.color, kills: p.kills, damage: Math.round(p.damage), revives: p.revives, downs: p.downs }))
    .sort((a, b) => b.kills - a.kills);
  broadcast({ t: 'over', reason, wave: G.wave, stats, map: G.map.name, mapId: G.map.id, diff: lobby.diff, kills: G.totalKills });
  console.log(`Spiel vorbei (${reason}) in Welle ${G.wave}`);
  G = null;
  lobby.phase = 'lobby';
  for (const p of players.values()) p.inGame = false;
  broadcast(lobbyMsg());
}

// ------------------------------------------------------------------ Zombies
function spawnZombie(type) {
  const m = G.map, cfg = ZOMBIES[type];
  for (let tries = 0; tries < 12; tries++) {
    const [sx, sz] = m.zspawns[Math.floor(Math.random() * m.zspawns.length)];
    const x = sx + (Math.random() - 0.5) * 4, z = sz + (Math.random() - 0.5) * 4;
    const [gx, gz] = G.grid.toCell(x, z);
    if (G.grid.blocked[gz * G.grid.n + gx] || !isFinite(G.genField[gz * G.grid.n + gx])) continue;
    const half = m.size / 2 - 0.6;
    const hp = Math.round(cfg.hp * waveHpMult(G.wave) * G.diff.hp * (type === 'boss' ? 1 + 0.35 * (activeCount() - 1) : 1));
    G.zombies.push({
      id: nextId++, type, x: Math.max(-half, Math.min(half, x)), z: Math.max(-half, Math.min(half, z)), vx: 0, vz: 0,
      rot: Math.atan2(-x, -z), hp, maxHp: hp, speed: cfg.speed * waveSpeedMult(G.wave) * (0.9 + Math.random() * 0.2),
      atkCd: 0.5, atk: 0, stuckT: 0, lastX: x, lastZ: z, wander: 0, wx: 0, wz: 0, spawnT: 1.0,
    });
    return true;
  }
  return false;
}

function activeCount() { let n = 0; for (const p of players.values()) if (p.inGame) n++; return Math.max(1, n); }

function damagePlayer(p, dmg) {
  if (p.state !== 'alive') return;
  if (p.armor > 0) { const a = Math.min(p.armor, dmg * 0.6); p.armor -= a; dmg -= a; }
  p.hp -= dmg;
  event({ e: 'hurt', id: p.id, d: Math.round(dmg) });
  if (p.hp <= 0) {
    p.hp = 0; p.state = 'down'; p.downT = BLEEDOUT_TIME; p.reviveProg = 0; p.downs++; p.streak = 0;
    event({ e: 'down', id: p.id });
    // Allein unterwegs? -> Selbstwiederbelebung, solange Leben übrig sind
    if (activeCount() === 1 && p.lives > 0) {
      p.selfRev = 4;
      sysChat(`Selbstwiederbelebung in 4s … (noch ${p.lives} Extra-Leben)`);
    } else sysChat(`${p.name} ist am Boden! Halte E zum Wiederbeleben.`);
  }
}

function damageZombie(z, dmg, byId, head = false) {
  if (z.hp <= 0) return false;
  const p = byId != null ? players.get(byId) : null;
  // Insta-Kill: Spieler-Treffer töten sofort (Bosse verlieren 15% pro Treffer)
  if (p && G.pu.instakill > 0) dmg = z.type === 'boss' ? Math.max(dmg, z.maxHp * 0.15) : z.hp + 1;
  z.hp -= dmg;
  if (p) p.damage += Math.min(dmg, z.hp + dmg);
  if (z.hp <= 0) {
    killZombie(z, byId, head);
    return true;
  }
  return false;
}

function killZombie(z, byId, head = false, silent = false) {
  z.hp = 0; z.dead = true;
  const p = byId != null ? players.get(byId) : null;
  const cfg = ZOMBIES[z.type];
  let streak = 0, reward = 0;
  if (p && !silent) {
    // Kill-Serie: Kills kurz hintereinander bringen Bonusgeld
    if (G.time - p.lastKillT < 3) p.streak++; else p.streak = 1;
    p.lastKillT = G.time; streak = p.streak;
    reward = cfg.reward + (head ? 25 : 0) + Math.min(10, streak - 1) * 8;
    reward *= G.diff.money;
    if (p.perks.has('lucky')) reward *= 1.25;
    if (G.pu.double > 0) reward *= 2;
    reward = Math.round(reward);
    p.money += reward; p.kills++; p.waveKills++;
    // Power-up fallen lassen?
    let chance = POWERUP_DROP * (p.perks.has('lucky') ? 2 : 1);
    if (z.type === 'boss') chance = 1; else if (z.type === 'brute') chance = 0.25;
    if (Math.random() < chance && G.pickups.length < 4) dropPowerup(z.x, z.z);
  }
  G.totalKills++;
  event({ e: 'zd', id: z.id, by: byId ?? -1, h: head ? 1 : 0, x: r2(z.x), z: r2(z.z), s: streak, m: reward });
  // Explodierer platzen auch, wenn sie erschossen werden (schadet dann nur Zombies)
  if (cfg.explode && !silent) explode(z.x, z.z, cfg.explode, cfg.damage * 1.5, byId, false);
}

function dropPowerup(x, z) {
  // Nuke und Munition seltener, wenn kaum Zombies da sind, damit es sich lohnt
  let pool = POWERUP_ORDER.slice();
  if (G.zombies.length < 6) pool = pool.filter(k => k !== 'nuke');
  const kind = pool[Math.floor(Math.random() * pool.length)];
  G.pickups.push({ id: nextId++, kind, x, z, t: POWERUP_LIFE });
}

function applyPowerup(kind, p) {
  const cfg = POWERUPS[kind];
  event({ e: 'pu', k: kind, by: p ? p.id : -1 });
  if (cfg.dur > 0) G.pu[kind] = Math.max(G.pu[kind] || 0, cfg.dur);
  if (kind === 'nuke') {
    let n = 0;
    for (const z of G.zombies) { if (!z.dead && z.spawnT <= 0) { killZombie(z, null, false, true); n++; } }
    G.zombies = G.zombies.filter(z => !z.dead);
    for (const q of players.values()) if (q.inGame) q.money += Math.round(400 * (G.pu.double > 0 ? 2 : 1));
    sysChat(`☢ NUKE von ${p ? p.name : '?'} – ${n} Zombies vernichtet!`);
  } else if (kind === 'ammo') {
    for (const q of players.values()) if (q.inGame) q.nades = Math.max(q.nades, GRENADE.max);
    for (const q of players.values()) if (q.inGame) sendInv(q);
  } else sysChat(`${cfg.icon} ${cfg.name} von ${p ? p.name : '?'} aufgehoben!`);
}

function explode(x, z, radius, damage, byId, hurtsPlayers) {
  event({ e: 'bm', x: r2(x), y: 0.6, z: r2(z), r: radius });
  for (const zz of G.zombies) {
    if (zz.dead) continue;
    const d = Math.sqrt(dist2(x, z, zz.x, zz.z));
    if (d < radius + ZOMBIES[zz.type].radius) damageZombie(zz, damage * (1 - 0.5 * d / radius), byId);
  }
  if (hurtsPlayers) {
    for (const p of players.values()) {
      if (!p.inGame || p.state !== 'alive') continue;
      const d = Math.sqrt(dist2(x, z, p.x, p.z));
      if (d < radius && p.y < 3) damagePlayer(p, damage * (1 - 0.6 * d / radius));
    }
    for (const t of G.turrets) {
      const d = Math.sqrt(dist2(x, z, t.x, t.z));
      if (d < radius) t.hp -= damage * 1.5;
    }
    for (const b of G.barricades) {
      const d = Math.sqrt(dist2(x, z, b.x, b.z));
      if (d < radius + 1) b.hp -= damage * 1.2;
    }
    const gd = distToGen(x, z);
    if (gd < radius) G.genHp -= damage * 1.5;
  }
}

function distToGen(x, z) {
  const m = G.map;
  const dx = Math.max(Math.abs(x - m.gen.x) - GEN_SIZE.w / 2, 0), dz = Math.max(Math.abs(z - m.gen.z) - GEN_SIZE.d / 2, 0);
  return Math.hypot(dx, dz);
}

function updateZombies(dt) {
  const grid = G.grid, n = grid.n, half = G.map.size / 2 - 0.5;
  const alivePlayers = [...players.values()].filter(p => p.inGame && p.state === 'alive');
  const frozen = G.pu.freeze > 0 ? 0.4 : 1;

  G.pfTimer -= dt;
  if (G.pfTimer <= 0) {
    G.pfTimer = 0.25;
    G.playerField = alivePlayers.length ? flowField(grid, alivePlayers.map(p => grid.toCell(p.x, p.z))) : null;
  }

  for (const z of G.zombies) {
    if (z.dead) continue;
    const cfg = ZOMBIES[z.type];
    if (z.spawnT > 0) { z.spawnT -= dt; continue; } // steigt aus dem Boden
    z.atkCd -= dt * frozen; if (z.atk > 0) z.atk -= dt;

    // --- Ziel wählen
    let target = null, tx, tz, field = G.genField, kind = 'gen', bestD = 22 * 22;
    for (const p of alivePlayers) {
      const d = dist2(z.x, z.z, p.x, p.z);
      if (d < bestD) { bestD = d; target = p; }
    }
    if (target) { tx = target.x; tz = target.z; field = G.playerField || G.genField; kind = 'player'; }
    else { tx = G.map.gen.x; tz = G.map.gen.z; }

    // Turm im Weg? -> angreifen
    let blockTurret = null;
    for (const t of G.turrets) {
      if (dist2(z.x, z.z, t.x, t.z) < (cfg.radius + 0.95) ** 2) { blockTurret = t; break; }
    }

    // Barrikade direkt vor der Nase (in Zielrichtung)? -> einschlagen
    let blockBarr = null;
    if (!blockTurret && G.barrBoxes.length) {
      const r = cfg.radius + 0.5;
      const ddx = tx - z.x, ddz = tz - z.z;
      for (const b of G.barrBoxes) {
        if (z.x < b.minX - r || z.x > b.maxX + r || z.z < b.minZ - r || z.z > b.maxZ + r) continue;
        const cx = Math.max(b.minX, Math.min(z.x, b.maxX)), cz = Math.max(b.minZ, Math.min(z.z, b.maxZ));
        const ox = cx - z.x, oz = cz - z.z;
        if (ox * ox + oz * oz > r * r) continue;
        // nur angreifen, wenn die Barrikade zwischen Zombie und Ziel liegt (oder er feststeckt)
        if (ox * ddx + oz * ddz > 0 || z.stuckT > 1) { blockBarr = G.barricades.find(bb => bb.id === b.id); if (blockBarr) { tx = cx; tz = cz; } break; }
      }
    }

    // --- Reichweite / Angriff
    let inRange = false, spitting = false;
    if (blockTurret) { inRange = true; tx = blockTurret.x; tz = blockTurret.z; }
    else if (blockBarr) inRange = true;
    else if (kind === 'player') {
      const d = Math.sqrt(bestD);
      inRange = d < cfg.radius + 0.95 && target.y < 1.7 * cfg.scale;
      // Spucker: aus der Distanz spucken, wenn Sichtlinie
      if (!inRange && cfg.spit && d > cfg.spit.min && d < cfg.spit.max && gridLOS(grid, z.x, z.z, tx, tz)) spitting = true;
    } else inRange = distToGen(z.x, z.z) < cfg.radius + 0.45;

    if (inRange || spitting) {
      z.vx *= 0.6; z.vz *= 0.6;
      z.rot = Math.atan2(tx - z.x, tz - z.z);
      if (cfg.explode && inRange) {
        killZombie(z, null, false, true);
        explode(z.x, z.z, cfg.explode, cfg.damage, null, true);
        continue;
      }
      if (z.atkCd <= 0) {
        if (spitting) {
          z.atkCd = cfg.spit.cd; z.atk = 0.5;
          // Zielpunkt leicht vorhalten
          const lead = target.moving ? 0.5 : 0;
          const px = target.x - Math.sin(target.yaw) * lead, pz = target.z - Math.cos(target.yaw) * lead;
          event({ e: 'zs', id: z.id, x: r2(px), z: r2(pz) });
          G.spits.push({ t: G.time + cfg.spit.travel, x: px, z: pz, dmg: cfg.damage, r: cfg.spit.radius });
        } else {
          z.atkCd = z.type === 'boss' ? 1.6 : 1.15; z.atk = 0.5;
          event({ e: 'za', id: z.id });
          if (blockTurret) blockTurret.hp -= cfg.damage * 1.4;
          else if (blockBarr) { blockBarr.hp -= cfg.damage * 1.6; event({ e: 'bh', id: blockBarr.id, x: r2(tx), z: r2(tz) }); }
          else if (kind === 'player') damagePlayer(target, cfg.damage);
          else G.genHp -= cfg.damage;
        }
      }
    } else {
      // --- Bewegungsrichtung bestimmen
      let dx = tx - z.x, dz = tz - z.z;
      const dToT = Math.hypot(dx, dz);
      const [gx, gz] = grid.toCell(z.x, z.z);
      if (!(dToT < 6 && gridLOS(grid, z.x, z.z, tx, tz))) {
        let best = Infinity, bx = 0, bz = 0;
        for (let oz = -1; oz <= 1; oz++) for (let ox = -1; ox <= 1; ox++) {
          if (!ox && !oz) continue;
          const nx = gx + ox, nz = gz + oz;
          if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
          if (grid.blocked[nz * n + nx]) continue;
          if (ox && oz && (grid.blocked[gz * n + nx] || grid.blocked[nz * n + gx])) continue;
          const v = field[nz * n + nx];
          if (v < best) { best = v; bx = nx; bz = nz; }
        }
        if (best < Infinity) { const [cx, cz] = grid.center(bx, bz); dx = cx - z.x; dz = cz - z.z; }
      }
      if (z.wander > 0) { z.wander -= dt; dx = z.wx; dz = z.wz; }
      const len = Math.hypot(dx, dz) || 1;
      const sp = z.speed * frozen;
      const k = Math.min(1, dt * 7);
      z.vx += ((dx / len) * sp - z.vx) * k;
      z.vz += ((dz / len) * sp - z.vz) * k;
    }

    z.x += z.vx * dt; z.z += z.vz * dt;
    const sp2 = z.vx * z.vx + z.vz * z.vz;
    if (sp2 > 0.05 && !inRange && !spitting) {
      const target = Math.atan2(z.vx, z.vz);
      let d = target - z.rot; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
      z.rot += d * Math.min(1, dt * 10);
    }

    // Festgefahren? -> kurz zufällig ausweichen
    z.stuckT += dt;
    if (z.stuckT > 1.5) {
      if (!inRange && !spitting && dist2(z.x, z.z, z.lastX, z.lastZ) < 0.3 * 0.3) {
        const a = Math.random() * Math.PI * 2; z.wander = 0.6; z.wx = Math.sin(a); z.wz = Math.cos(a);
      }
      z.stuckT = 0; z.lastX = z.x; z.lastZ = z.z;
    }
  }

  // --- Spucke landet
  if (G.spits.length) {
    const keep = [];
    for (const s of G.spits) {
      if (G.time < s.t) { keep.push(s); continue; }
      event({ e: 'zsl', x: r2(s.x), z: r2(s.z) });
      for (const p of alivePlayers) if (p.state === 'alive' && dist2(p.x, p.z, s.x, s.z) < s.r * s.r && p.y < 2) damagePlayer(p, s.dmg);
    }
    G.spits = keep;
  }

  // --- Trennung zwischen Zombies
  const zs = G.zombies;
  for (let i = 0; i < zs.length; i++) {
    const a = zs[i]; if (a.dead || a.spawnT > 0) continue;
    const ra = ZOMBIES[a.type].radius;
    for (let j = i + 1; j < zs.length; j++) {
      const b = zs[j]; if (b.dead || b.spawnT > 0) continue;
      const rb = ZOMBIES[b.type].radius;
      const dx = b.x - a.x, dz = b.z - a.z, rr = ra + rb;
      const d2 = dx * dx + dz * dz;
      if (d2 < rr * rr && d2 > 1e-6) {
        const d = Math.sqrt(d2), push = (rr - d) / 2;
        const wa = rb / (ra + rb), wb = ra / (ra + rb); // große Zombies schieben kleine
        a.x -= (dx / d) * push * wa * 2; a.z -= (dz / d) * push * wa * 2;
        b.x += (dx / d) * push * wb * 2; b.z += (dz / d) * push * wb * 2;
      }
    }
  }
  // --- Kollision mit Welt, Türmen, Grenzen
  for (const z of zs) {
    if (z.dead) continue;
    const r = ZOMBIES[z.type].radius * 0.85;
    for (const b of G.solids) {
      if (b.minY > 1.5) continue;
      if (z.x < b.minX - r || z.x > b.maxX + r || z.z < b.minZ - r || z.z > b.maxZ + r) continue;
      const res = pushCircleOutOfBox(z.x, z.z, r, b);
      if (res) { z.x = res[0]; z.z = res[1]; }
    }
    for (const t of G.turrets) {
      const dx = z.x - t.x, dz = z.z - t.z, rr = r + 0.6, d2 = dx * dx + dz * dz;
      if (d2 < rr * rr && d2 > 1e-6) { const d = Math.sqrt(d2); z.x = t.x + dx / d * rr; z.z = t.z + dz / d * rr; }
    }
    for (const b of G.barrBoxes) {
      if (z.x < b.minX - r || z.x > b.maxX + r || z.z < b.minZ - r || z.z > b.maxZ + r) continue;
      const res = pushCircleOutOfBox(z.x, z.z, r, b);
      if (res) { z.x = res[0]; z.z = res[1]; }
    }
    z.x = Math.max(-half, Math.min(half, z.x)); z.z = Math.max(-half, Math.min(half, z.z));
  }
  G.zombies = zs.filter(z => !z.dead);
}

// ------------------------------------------------------------------ Power-ups am Boden
function updatePickups(dt) {
  for (const k of Object.keys(G.pu)) if (G.pu[k] > 0) G.pu[k] = Math.max(0, G.pu[k] - dt);
  if (!G.pickups.length) return;
  const keep = [];
  for (const pk of G.pickups) {
    pk.t -= dt;
    if (pk.t <= 0) continue;
    let taker = null;
    for (const p of players.values()) {
      if (!p.inGame || p.state !== 'alive') continue;
      if (dist2(p.x, p.z, pk.x, pk.z) < 1.4 * 1.4 && p.y < 2) { taker = p; break; }
    }
    if (taker) applyPowerup(pk.kind, taker); else keep.push(pk);
  }
  G.pickups = keep;
}

// ------------------------------------------------------------------ Türme
function updateTurrets(dt) {
  for (const t of G.turrets) {
    const cfg = TURRETS[t.type];
    t.cd -= dt;
    // Ziel suchen (nächster Zombie mit Sichtlinie)
    let best = null, bd = cfg.range * cfg.range;
    for (const z of G.zombies) {
      if (z.spawnT > 0) continue;
      const d = dist2(t.x, t.z, z.x, z.z);
      if (d < bd && gridLOS(G.grid, t.x, t.z, z.x, z.z)) { bd = d; best = z; }
    }
    if (!best) continue;
    const want = Math.atan2(best.x - t.x, best.z - t.z);
    let d = want - t.yaw; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI;
    t.yaw += d * Math.min(1, dt * 12);
    if (t.cd > 0 || Math.abs(d) > 0.5) continue;
    t.cd = cfg.rate;
    if (t.type === 'mg') {
      event({ e: 'ts', id: t.id, x: r2(best.x), z: r2(best.z), s: ZOMBIES[best.type].scale });
      damageZombie(best, cfg.damage, t.owner);
    } else if (t.type === 'cannon') {
      event({ e: 'tc', id: t.id, x: r2(best.x), z: r2(best.z) });
      explode(best.x, best.z, cfg.splash, cfg.damage, t.owner, false);
    } else if (t.type === 'tesla') {
      const pts = [[r2(best.x), r2(best.z), ZOMBIES[best.type].scale]];
      const hit = new Set([best]);
      let cur = best;
      damageZombie(best, cfg.damage, t.owner);
      for (let i = 1; i < cfg.chain; i++) {
        let nb = null, nd = 6 * 6;
        for (const z of G.zombies) {
          if (hit.has(z) || z.dead || z.spawnT > 0) continue;
          const dd = dist2(cur.x, cur.z, z.x, z.z);
          if (dd < nd) { nd = dd; nb = z; }
        }
        if (!nb) break;
        hit.add(nb); pts.push([r2(nb.x), r2(nb.z), ZOMBIES[nb.type].scale]);
        damageZombie(nb, cfg.damage * 0.85, t.owner);
        cur = nb;
      }
      event({ e: 'tt', id: t.id, pts });
    }
  }
  // zerstörte Türme
  for (const t of G.turrets) if (t.hp <= 0) { event({ e: 'td', id: t.id, x: t.x, z: t.z }); }
  G.turrets = G.turrets.filter(t => t.hp > 0);
  G.zombies = G.zombies.filter(z => !z.dead);
  // zerstörte Barrikaden
  if (G.barricades.some(b => b.hp <= 0)) {
    for (const b of G.barricades) if (b.hp <= 0) event({ e: 'bd', id: b.id, x: b.x, z: b.z });
    G.barricades = G.barricades.filter(b => b.hp > 0);
    rebuildBarricades();
  }
}

// ------------------------------------------------------------------ Spieler (Wiederbeleben / Ausbluten / Glückskiste)
function updatePlayers(dt) {
  const now = Date.now();
  for (const p of players.values()) {
    if (!p.inGame) continue;
    // Glückskiste dreht sich
    if (p.crateT > 0) {
      p.crateT -= dt;
      if (p.crateT <= 0) { p.crateT = 0; finishCrate(p); }
    }
    if (p.state === 'down' && p.selfRev > 0) {
      p.selfRev -= dt; p.reviveProg = Math.min(REVIVE_TIME, (1 - p.selfRev / 4) * REVIVE_TIME);
      if (p.selfRev <= 0) {
        p.selfRev = 0; p.lives--; p.state = 'alive'; p.hp = p.perks.has('medic') ? 80 : 60; p.reviveProg = 0;
        event({ e: 'rev', id: p.id, by: p.id });
      }
      continue;
    }
    if (p.state === 'down') {
      const reviver = p.reviver != null ? players.get(p.reviver) : null;
      const active = reviver && reviver.state === 'alive' && now - p.revivePing < 300 && dist2(reviver.x, reviver.z, p.x, p.z) < 3 * 3;
      if (active) {
        p.reviveProg += dt * (reviver.perks.has('medic') ? 2 : 1);
        if (p.reviveProg >= REVIVE_TIME) {
          p.state = 'alive'; p.hp = reviver.perks.has('medic') ? 80 : 40; p.reviveProg = 0; p.reviver = null; reviver.revives++; reviver.money += 100;
          event({ e: 'rev', id: p.id, by: reviver.id });
          sysChat(`${reviver.name} hat ${p.name} wiederbelebt (+100$)`);
        }
      } else {
        p.reviveProg = Math.max(0, p.reviveProg - dt * 2);
        p.downT -= dt;
        if (p.downT <= 0) { p.state = 'dead'; event({ e: 'dead', id: p.id }); sysChat(`${p.name} ist gestorben – Rückkehr nach der Welle.`); }
      }
    }
  }
}

// ------------------------------------------------------------------ Wellen
function updateWaves(dt) {
  if (G.phase === 'prep') {
    G.timer -= dt;
    const alive = [...players.values()].filter(p => p.inGame && p.state === 'alive');
    if (alive.length && alive.every(p => p.ready) && G.timer > 3) G.timer = 3;
    if (G.timer <= 0) {
      G.wave++; G.phase = 'wave'; G.kind = waveKind(G.wave);
      G.queue = waveComposition(G.wave, activeCount(), G.diff.count);
      G.spawnT = 0;
      for (const p of players.values()) { p.ready = false; p.waveKills = 0; }
      event({ e: 'ws', w: G.wave, kind: G.kind });
    }
  } else {
    G.spawnT -= dt;
    if (G.spawnT <= 0 && G.queue.length && G.zombies.length < maxAlive(activeCount())) {
      G.spawnT = spawnInterval(G.wave);
      const burst = G.kind === 'horde' ? 3 : G.wave >= 6 ? 2 : 1;
      for (let i = 0; i < burst && G.queue.length; i++) { if (spawnZombie(G.queue[G.queue.length - 1])) G.queue.pop(); }
    }
    if (!G.queue.length && !G.zombies.length) {
      G.phase = 'prep'; G.timer = PREP_TIME; G.spits = [];
      const bonus = Math.round(WAVE_BONUS * G.diff.money * (G.pu.double > 0 ? 2 : 1));
      let mvp = null;
      const kills = {};
      for (const p of players.values()) {
        if (!p.inGame) continue;
        p.money += bonus;
        kills[p.id] = p.waveKills;
        if (!mvp || p.waveKills > mvp.waveKills) mvp = p;
        if (p.state !== 'alive') { p.state = 'alive'; p.hp = p.maxHp; p.reviveProg = 0; send(p, { t: 'respawn', x: G.map.spawn.x, y: 0, z: G.map.spawn.z }); }
      }
      if (mvp && mvp.waveKills > 0 && activeCount() > 1) mvp.money += 100;
      event({ e: 'wc', w: G.wave, bonus, mvp: mvp ? { id: mvp.id, name: mvp.name, color: mvp.color, kills: mvp.waveKills } : null, kills });
    }
  }
}

// ------------------------------------------------------------------ Snapshot
function snapshot() {
  const snap = {
    t: 'snap', wv: G.wave, ph: G.phase, tm: r2(Math.max(0, G.timer)), zl: G.queue.length + G.zombies.length,
    gh: Math.max(0, Math.round(G.genHp)),
    pu: [r2(G.pu.instakill), r2(G.pu.double), r2(G.pu.freeze)],
    p: [], z: [], tu: [], pk: [], ba: [], ev: G.events,
  };
  for (const p of players.values()) {
    if (!p.inGame) continue;
    snap.p.push([p.id, r2(p.x), r2(p.y), r2(p.z), r2(p.yaw), r2(p.pitch), WEAPON_ORDER.indexOf(p.weapon), Math.ceil(p.hp), Math.ceil(p.armor),
      STATE_IDX[p.state], p.money, p.kills, p.moving ? 1 : 0, r2(p.reviveProg / REVIVE_TIME), r2(p.downT), p.ready ? 1 : 0, p.fire ? 1 : 0, p.maxHp, p.upg[p.weapon] || 0, p.melee ? 1 : 0]);
  }
  for (const z of G.zombies) snap.z.push([z.id, ZOMBIE_TYPES.indexOf(z.type), r2(z.x), r2(z.z), r2(z.rot), Math.max(0, Math.ceil(z.hp / z.maxHp * 100)), z.atk > 0 ? 1 : 0, z.spawnT > 0 ? 1 : 0]);
  for (const t of G.turrets) snap.tu.push([t.id, TURRET_ORDER.indexOf(t.type), r2(t.x), r2(t.z), r2(t.yaw), Math.max(0, Math.ceil(t.hp / t.maxHp * 100)), t.owner]);
  for (const b of G.barricades) snap.ba.push([b.id, r2(b.x), r2(b.z), b.rot, Math.max(0, Math.ceil(b.hp / b.maxHp * 100))]);
  for (const pk of G.pickups) snap.pk.push([pk.id, POWERUP_ORDER.indexOf(pk.kind), r2(pk.x), r2(pk.z), r2(pk.t)]);
  G.events = [];
  broadcast(snap);
}

let lastTick = Date.now();
setInterval(() => {
  const now = Date.now();
  const dt = Math.min(0.1, (now - lastTick) / 1000);
  lastTick = now;
  if (!G) return;
  G.time += dt;
  updateWaves(dt);
  updateZombies(dt);
  updateTurrets(dt);
  updatePickups(dt);
  updatePlayers(dt);
  if (!G) return;
  const inGame = [...players.values()].filter(p => p.inGame);
  if (G.genHp <= 0) { snapshot(); return endGame('Der Generator wurde zerstört!'); }
  if (inGame.length && inGame.every(p => p.state !== 'alive' && !(p.selfRev > 0))) { snapshot(); return endGame('Alle Überlebenden sind gefallen!'); }
  snapshot();
}, 1000 / TICK_RATE);

// ------------------------------------------------------------------ Nachrichten
function canShop(p) {
  if (!G || p.state !== 'alive') return false;
  if (G.phase === 'prep') return true;
  return dist2(p.x, p.z, G.map.shop.x, G.map.shop.z) < SHOP_RADIUS * SHOP_RADIUS;
}

function finishCrate(p) {
  if (!G || !p.inGame) return;
  const item = p.crateItem; p.crateItem = null;
  if (!item) return;
  let text;
  if (item.kind === 'weapon') {
    p.owned.add(item.id);
    if (item.tier > (p.upg[item.id] || 0)) p.upg[item.id] = item.tier;
    text = `${p.name} zieht ${weaponStats(item.id, p.upg[item.id] || 0).name} aus der Glückskiste!`;
  } else if (item.kind === 'money') {
    p.money += item.amount; text = `${p.name} findet $${item.amount} in der Glückskiste.`;
  } else text = `${p.name} zieht eine Niete – immerhin volle Munition.`;
  sysChat(text);
  send(p, { t: 'crate', item }); sendInv(p);
  event({ e: 'crate', by: p.id, kind: item.kind, id: item.id || null });
}

function handle(p, m) {
  switch (m.t) {
    case 'hello': {
      p.name = String(m.name || 'Spieler').slice(0, 16).trim() || 'Spieler';
      if (PLAYER_COLORS.includes(m.color)) p.color = m.color;
      broadcast(lobbyMsg());
      // Laufendes Spiel? -> direkt einsteigen
      if (G && m.join) joinRunning(p);
      break;
    }
    case 'map': if (lobby.phase === 'lobby' && MAPS[m.map]) { lobby.map = m.map; broadcast(lobbyMsg()); } break;
    case 'diff': if (lobby.phase === 'lobby' && DIFFS[m.diff]) { lobby.diff = m.diff; broadcast(lobbyMsg()); } break;
    case 'start': if (!G) startGame(lobby.map); else joinRunning(p); break;
    case 'join': if (G && !p.inGame) joinRunning(p); break;
    case 's': {
      if (!G || !p.inGame || !Array.isArray(m.p)) break;
      if (p.state === 'alive') {
        const half = G.map.size / 2;
        p.x = Math.max(-half, Math.min(half, +m.p[0] || 0)); p.y = Math.max(0, Math.min(20, +m.p[1] || 0)); p.z = Math.max(-half, Math.min(half, +m.p[2] || 0));
      }
      p.yaw = +m.yaw || 0; p.pitch = +m.pitch || 0; p.moving = !!m.mv; p.fire = !!m.f; p.melee = !!m.ml;
      if (WEAPONS[m.w] && p.owned.has(m.w)) p.weapon = m.w;
      break;
    }
    case 'shot': {
      if (!G || p.state !== 'alive') break;
      event({ e: 'sh', by: p.id, w: m.w, o: m.o, ends: Array.isArray(m.ends) ? m.ends.slice(0, 10) : [] });
      break;
    }
    case 'boom': {
      if (!G || p.state !== 'alive' || !Array.isArray(m.p)) break;
      const r = m.w === 'grenade' ? GRENADE.splash : WEAPONS.launcher.splash;
      if (m.w !== 'grenade' && !p.owned.has('launcher')) break;
      event({ e: 'bm', x: r2(+m.p[0]), y: r2(+m.p[1]), z: r2(+m.p[2]), r, by: p.id });
      break;
    }
    case 'nade': {
      if (!G || p.state !== 'alive' || p.nades <= 0 || !Array.isArray(m.o) || !Array.isArray(m.v)) break;
      p.nades--;
      event({ e: 'nd', by: p.id, o: m.o.slice(0, 3).map(Number), v: m.v.slice(0, 3).map(Number) });
      sendInv(p);
      break;
    }
    case 'hit': {
      if (!G || p.state !== 'alive' || !Array.isArray(m.hits)) break;
      let cap;
      if (m.w === 'grenade') cap = GRENADE.damage * 1.2;
      else if (m.w === 'melee') cap = MELEE.damage * 2.2;
      else {
        const w = WEAPONS[m.w];
        if (!w || !p.owned.has(m.w)) break;
        const ws = weaponStats(m.w, p.upg[m.w] || 0, p.perks);
        cap = ws.damage * ws.pellets * 2.6;
      }
      const seen = new Set();
      for (const h of m.hits.slice(0, 40)) {
        const [zid, dmgRaw, head] = h;
        const z = G.zombies.find(zz => zz.id === zid);
        if (!z || z.spawnT > 0) continue;
        if (m.w === 'melee' && dist2(p.x, p.z, z.x, z.z) > (MELEE.range + ZOMBIES[z.type].radius + 1) ** 2) continue;
        const dmg = Math.max(0, Math.min(+dmgRaw || 0, cap));
        if (!seen.has(zid)) { seen.add(zid); p.money += Math.round(HIT_REWARD * (G.pu.double > 0 ? 2 : 1)); }
        damageZombie(z, dmg, p.id, !!head);
      }
      G.zombies = G.zombies.filter(z => !z.dead);
      break;
    }
    case 'buy': {
      if (!G) break;
      const fail = reason => send(p, { t: 'bought', ok: false, item: m.item, reason });
      if (!canShop(p)) { fail('Während einer Welle nur am Shop-Terminal (beim Generator)!'); break; }
      if (WEAPONS[m.item]) {
        const w = WEAPONS[m.item];
        if (p.owned.has(m.item)) { fail('Schon gekauft'); break; }
        if (p.money < w.price) { fail('Nicht genug Geld'); break; }
        p.money -= w.price; p.owned.add(m.item);
        send(p, { t: 'bought', ok: true, item: m.item }); sendInv(p);
      } else if (m.item === 'upgrade') {
        const id = m.w;
        if (!WEAPONS[id] || !p.owned.has(id)) { fail('Waffe nicht im Besitz'); break; }
        const tier = (p.upg[id] || 0) + 1;
        if (tier > UPGRADE.max) { fail('Schon voll aufgewertet'); break; }
        const cost = UPGRADE.cost[tier];
        if (p.money < cost) { fail('Nicht genug Geld'); break; }
        p.money -= cost; p.upg[id] = tier;
        send(p, { t: 'bought', ok: true, item: 'upgrade', w: id, tier }); sendInv(p);
      } else if (PERKS[m.item]) {
        const pk = PERKS[m.item];
        if (p.perks.has(m.item)) { fail('Perk schon aktiv'); break; }
        if (p.perks.size >= MAX_PERKS) { fail(`Maximal ${MAX_PERKS} Perks`); break; }
        if (p.money < pk.price) { fail('Nicht genug Geld'); break; }
        p.money -= pk.price; p.perks.add(m.item);
        if (m.item === 'tough') { p.maxHp = PLAYER_MAX_HP + 50; p.hp += 50; }
        if (m.item === 'medic' && activeCount() === 1) p.lives++;
        send(p, { t: 'bought', ok: true, item: m.item }); sendInv(p);
      } else if (m.item === 'crate') {
        if (!G.map.crate) { fail('Keine Glückskiste auf dieser Karte'); break; }
        if (dist2(p.x, p.z, G.map.crate.x, G.map.crate.z) > 3.2 * 3.2) { fail('Geh näher an die Glückskiste'); break; }
        if (p.crateT > 0) { fail('Die Kiste dreht sich noch'); break; }
        if (p.money < CRATE.price) { fail('Nicht genug Geld'); break; }
        p.money -= CRATE.price;
        // Auslosung: lieber Waffen, die man noch nicht hat; sonst Aufwertung, Geld oder Niete
        const notOwned = WEAPON_ORDER.filter(id => id !== 'pistol' && !p.owned.has(id));
        const r = Math.random();
        if (notOwned.length && r < 0.7) {
          const weighted = notOwned.flatMap(id => Array(Math.max(1, 7 - WEAPONS[id].slot)).fill(id)); // billige häufiger
          const id = weighted[Math.floor(Math.random() * weighted.length)];
          p.crateItem = { kind: 'weapon', id, tier: Math.random() < 0.2 ? 1 : 0 };
        } else if (r < 0.85) {
          const owned = WEAPON_ORDER.filter(id => p.owned.has(id) && (p.upg[id] || 0) < UPGRADE.max);
          if (owned.length) { const id = owned[Math.floor(Math.random() * owned.length)]; p.crateItem = { kind: 'weapon', id, tier: (p.upg[id] || 0) + 1 }; }
          else p.crateItem = { kind: 'money', amount: 1500 };
        } else if (r < 0.93) p.crateItem = { kind: 'money', amount: 1000 + Math.floor(Math.random() * 6) * 100 };
        else p.crateItem = { kind: 'dud' };
        p.crateT = CRATE.spin;
        event({ e: 'cs', by: p.id });
        send(p, { t: 'bought', ok: true, item: 'crate' }); sendInv(p);
      } else if (ITEMS[m.item]) {
        const it = ITEMS[m.item];
        if (p.money < it.price) { fail('Nicht genug Geld'); break; }
        if (m.item === 'heal' && p.hp >= p.maxHp) { fail('Du bist schon gesund'); break; }
        if (m.item === 'armor' && p.armor >= PLAYER_MAX_ARMOR) { fail('Panzerung ist voll'); break; }
        if (m.item === 'repair' && G.genHp >= GENERATOR_HP) { fail('Generator ist unbeschädigt'); break; }
        if (m.item === 'nades' && p.nades >= GRENADE.max) { fail(`Maximal ${GRENADE.max} Granaten`); break; }
        const mine = G.turrets.filter(t => t.owner === p.id);
        if (m.item === 'fixturrets' && !mine.some(t => t.hp < t.maxHp)) { fail('Keine beschädigten Türme'); break; }
        if (m.item === 'fixbarr' && !G.barricades.some(b => b.hp < b.maxHp)) { fail('Keine beschädigten Barrikaden'); break; }
        p.money -= it.price;
        if (m.item === 'heal') p.hp = p.maxHp;
        if (m.item === 'armor') p.armor = Math.min(PLAYER_MAX_ARMOR, p.armor + 50);
        if (m.item === 'repair') G.genHp = Math.min(GENERATOR_HP, G.genHp + GENERATOR_HP * 0.3);
        if (m.item === 'fixturrets') for (const t of mine) t.hp = t.maxHp;
        if (m.item === 'fixbarr') for (const b of G.barricades) b.hp = b.maxHp;
        if (m.item === 'nades') p.nades = Math.min(GRENADE.max, p.nades + GRENADE.pack);
        send(p, { t: 'bought', ok: true, item: m.item }); sendInv(p);
      }
      break;
    }
    case 'place': {
      if (!G) break;
      const fail = reason => send(p, { t: 'bought', ok: false, item: m.type, reason });
      if (m.type === 'barricade') {
        if (!canShop(p)) { fail('Während einer Welle nur in der Nähe des Shop-Terminals!'); break; }
        if (p.money < BARRICADE.price) { fail('Nicht genug Geld'); break; }
        if (G.barricades.length >= BARRICADE.max) { fail(`Maximal ${BARRICADE.max} Barrikaden`); break; }
        const x = +m.x, z = +m.z, rot = m.rot ? 1 : 0;
        if (!isFinite(x) || !isFinite(z)) break;
        if (dist2(x, z, p.x, p.z) > 9 * 9) { fail('Zu weit weg'); break; }
        const b = { id: nextId++, x, z, rot, hp: BARRICADE.hp, maxHp: BARRICADE.hp, owner: p.id };
        if (!validBarricadeSpot(b)) { fail('Hier ist kein Platz'); break; }
        p.money -= BARRICADE.price;
        G.barricades.push(b); rebuildBarricades();
        event({ e: 'bp', id: b.id });
        send(p, { t: 'bought', ok: true, item: 'barricade' }); sendInv(p);
        break;
      }
      const cfg = TURRETS[m.type];
      if (!cfg) break;
      if (!canShop(p)) { fail('Während einer Welle nur in der Nähe des Shop-Terminals!'); break; }
      if (p.money < cfg.price) { fail('Nicht genug Geld'); break; }
      if (G.turrets.length >= MAX_TURRETS) { fail(`Maximal ${MAX_TURRETS} Türme`); break; }
      const x = +m.x, z = +m.z;
      if (!isFinite(x) || !isFinite(z)) break;
      if (!validTurretSpot(x, z)) { fail('Hier ist kein Platz'); break; }
      if (dist2(x, z, p.x, p.z) > 9 * 9) { fail('Zu weit weg'); break; }
      p.money -= cfg.price;
      const t = { id: nextId++, type: m.type, owner: p.id, x, z, yaw: +m.yaw || 0, hp: cfg.hp, maxHp: cfg.hp, cd: 0.5 };
      G.turrets.push(t);
      event({ e: 'tp', id: t.id });
      send(p, { t: 'bought', ok: true, item: m.type }); sendInv(p);
      break;
    }
    case 'revive': {
      if (!G || p.state !== 'alive') break;
      const tgt = players.get(m.id);
      if (!tgt || tgt.state !== 'down') break;
      if (tgt.reviver != null && tgt.reviver !== p.id && Date.now() - tgt.revivePing < 300) break;
      tgt.reviver = p.id; tgt.revivePing = Date.now();
      break;
    }
    case 'leave': {
      if (!G || !p.inGame) break;
      p.inGame = false; p.state = 'dead';
      sysChat(`${p.name} hat das Spiel verlassen.`);
      if (![...players.values()].some(q => q.inGame)) { G = null; lobby.phase = 'lobby'; }
      broadcast(lobbyMsg());
      break;
    }
    case 'ready': if (G && G.phase === 'prep') { p.ready = !p.ready; } break;
    case 'chat': {
      const text = String(m.text || '').slice(0, 120).trim();
      if (text) broadcast({ t: 'chat', name: p.name, color: p.color, text });
      break;
    }
  }
}

function validTurretSpot(x, z) {
  const half = G.map.size / 2 - 1.5;
  if (Math.abs(x) > half || Math.abs(z) > half) return false;
  for (const b of G.solids) if (pushCircleOutOfBox(x, z, 0.7, b)) return false;
  for (const b of G.barrBoxes) if (pushCircleOutOfBox(x, z, 0.7, b)) return false;
  for (const t of G.turrets) if (dist2(x, z, t.x, t.z) < 1.6 * 1.6) return false;
  if (G.map.mask && !cellFree(x, z, 0.7)) return false;
  return true;
}
/** Prüft die Begehbarkeitsmaske im Umkreis (für Karten mit vorberechneter Maske) */
function cellFree(x, z, r) {
  const g = G.grid;
  const offs = r > 0 ? [-r, 0, r] : [0]; // r = 0: nur die Zelle selbst (Schrittweite 0 würde sonst endlos schleifen)
  for (const dz of offs) for (const dx of offs) { const [gx, gz] = g.toCell(x + dx, z + dz); if (g.blocked[gz * g.n + gx]) return false; }
  return true;
}
function boxesOverlap(a, b, m = 0) { return a.minX < b.maxX + m && a.maxX > b.minX - m && a.minZ < b.maxZ + m && a.maxZ > b.minZ - m; }
function validBarricadeSpot(bar) {
  const box = barricadeBox(bar);
  const half = G.map.size / 2 - 1.5;
  if (Math.abs(bar.x) > half || Math.abs(bar.z) > half) return false;
  for (const b of G.solids) if (b.minY < 1.2 && boxesOverlap(box, b, 0.25)) return false;
  if (G.map.mask) { for (const [cx, cz] of [[box.minX, box.minZ], [box.maxX, box.minZ], [box.minX, box.maxZ], [box.maxX, box.maxZ], [bar.x, bar.z]]) if (!cellFree(cx, cz, 0)) return false; }
  for (const b of G.barrBoxes) if (boxesOverlap(box, b, -0.35)) return false; // leichtes Überlappen erlaubt (lückenlose Wälle)
  for (const t of G.turrets) if (pushCircleOutOfBox(t.x, t.z, 0.8, box)) return false;
  for (const p of players.values()) if (p.inGame && p.state === 'alive' && pushCircleOutOfBox(p.x, p.z, 0.5, box)) return false;
  // Generator und Shop müssen erreichbar bleiben: nicht direkt davor bauen
  if (dist2(bar.x, bar.z, G.map.gen.x, G.map.gen.z) < 3.2 * 3.2) return false;
  if (dist2(bar.x, bar.z, G.map.shop.x, G.map.shop.z) < 2.2 * 2.2) return false;
  if (G.map.crate && dist2(bar.x, bar.z, G.map.crate.x, G.map.crate.z) < 2.2 * 2.2) return false;
  return true;
}

function joinRunning(p) {
  if (!G || p.inGame) return;
  resetPlayerForGame(p, players.size);
  // Nachzügler bekommen etwas Startgeld je nach Welle
  p.money = START_CASH + G.wave * 300;
  send(p, startMsg(p)); sendInv(p);
  sysChat(`${p.name} ist dem Spiel beigetreten.`);
  broadcast(lobbyMsg());
}

// ------------------------------------------------------------------ WebSocket
const wss = new WebSocketServer({ server, path: '/ws' });
wss.on('connection', ws => {
  const used = new Set([...players.values()].map(p => p.color));
  const p = {
    id: nextId++, ws, name: 'Spieler', color: PLAYER_COLORS.find(c => !used.has(c)) || PLAYER_COLORS[0],
    inGame: false, x: 0, y: 0, z: 0, yaw: 0, pitch: 0, weapon: 'pistol', owned: new Set(['pistol']), upg: {}, perks: new Set(), nades: 0, maxHp: PLAYER_MAX_HP,
    money: 0, hp: 100, armor: 0, state: 'alive', kills: 0, crateT: 0,
  };
  players.set(p.id, p);
  send(p, { t: 'welcome', id: p.id, color: p.color, urls: lanUrls(), maps: MAP_ORDER });
  send(p, lobbyMsg());
  ws.on('message', data => {
    let m; try { m = JSON.parse(data); } catch { return; }
    try { handle(p, m); } catch (err) { console.error('Fehler bei Nachricht', m?.t, err); }
  });
  ws.on('close', () => {
    players.delete(p.id);
    if (G) {
      sysChat(`${p.name} hat das Spiel verlassen.`);
      if (![...players.values()].some(q => q.inGame)) { G = null; lobby.phase = 'lobby'; console.log('Alle weg – zurück zur Lobby'); }
    }
    broadcast(lobbyMsg());
  });
});

server.listen(PORT, () => {
  console.log('\n🧟  ZOMBIE-ANSTURM Server läuft!\n');
  console.log(`   Auf diesem Gerät:     http://localhost:${PORT}`);
  for (const u of lanUrls()) console.log(`   Für Freunde im WLAN:  ${u}`);
  console.log('\n   (Alle Geräte müssen im selben Netzwerk sein.)\n');
});
