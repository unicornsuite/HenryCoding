// Zombie-Ansturm – Client-Hauptprogramm
import * as THREE from 'three';
import {
  WEAPONS, WEAPON_ORDER, TURRETS, TURRET_ORDER, ZOMBIES, ZOMBIE_TYPES, BODY, SHOP_RADIUS, MAX_TURRETS, PLAYER_COLORS, GENERATOR_HP,
  weaponStats, GRENADE, MELEE, PERKS, POWERUPS, POWERUP_ORDER, CRATE, WAVE_KINDS, BARRICADE,
} from '/shared/config.js';
import { MAPS } from '/shared/maps.js';
import { pushCircleOutOfBox, barricadeBox } from '/shared/geom.js';
import { buildWorld } from './world.js';
import { buildCharacter, animateCharacter, buildGun, buildTurret, buildPickup, buildBarricade, makeLabel, makeHealthBar, boxGeo, mat } from './models.js';
import { Effects } from './effects.js';
import { initAudio, sfx, setListener, setVolume } from './audio.js';
import { ui, esc } from './ui.js';
import { touch } from './touch.js';

const V3 = THREE.Vector3;
const $ = id => document.getElementById(id);
const clamp = (v, a, b) => Math.max(a, Math.min(b, v));
const lerpAngle = (a, b, k) => { let d = b - a; while (d > Math.PI) d -= 2 * Math.PI; while (d < -Math.PI) d += 2 * Math.PI; return a + d * k; };

touch.init();

// ------------------------------------------------------------------ Einstellungen
const settings = { name: '', color: null, sens: 1, vol: 0.7, fov: 78, quality: touch.enabled ? 'low' : 'medium', autofire: true };
try { Object.assign(settings, JSON.parse(localStorage.getItem('za-settings') || '{}')); } catch { /* ohne Speicher */ }
const saveSettings = () => { try { localStorage.setItem('za-settings', JSON.stringify(settings)); } catch { /* egal */ } };
setVolume(settings.vol);

// ------------------------------------------------------------------ Renderer & Szenen
const canvas = $('c');
const renderer = new THREE.WebGLRenderer({ canvas, antialias: settings.quality !== 'low', powerPreference: 'high-performance' });
renderer.outputColorSpace = THREE.SRGBColorSpace;
renderer.shadowMap.enabled = true;
renderer.shadowMap.type = THREE.PCFSoftShadowMap;
renderer.autoClear = false;
const pixelRatio = () => settings.quality === 'low' ? Math.min(devicePixelRatio, 1) : settings.quality === 'high' ? Math.min(devicePixelRatio, 2) : Math.min(devicePixelRatio, 1.5);
renderer.setPixelRatio(pixelRatio());
renderer.setSize(innerWidth, innerHeight, false);

const scene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(settings.fov, innerWidth / innerHeight, 0.05, 700);
camera.rotation.order = 'YXZ';
scene.add(camera);

const vmScene = new THREE.Scene();
const vmCam = new THREE.PerspectiveCamera(60, innerWidth / innerHeight, 0.01, 10);
const vmHemi = new THREE.HemisphereLight('#ffffff', '#555555', 1.6); vmScene.add(vmHemi);
const vmSun = new THREE.DirectionalLight('#ffffff', 1.6); vmSun.position.set(0.5, 1, 0.6); vmScene.add(vmSun);

const fx = new Effects(scene, camera);

addEventListener('resize', () => {
  renderer.setSize(innerWidth, innerHeight, false);
  camera.aspect = vmCam.aspect = innerWidth / innerHeight;
  camera.updateProjectionMatrix(); vmCam.updateProjectionMatrix();
});

// ------------------------------------------------------------------ Zustand
let ws = null, myId = null, myColor = null, lanUrls = [], lobby = null;
let inGame = false, world = null, mapDef = null, gameOver = false;
const me = {
  pos: new V3(), vel: new V3(), yaw: 0, pitch: 0, onGround: true, hp: 100, armor: 0, money: 0, state: 'alive', kills: 0, downT: 0, ready: false,
  owned: ['pistol'], weapon: 'pistol', ammo: {}, fireCd: 0, reloadT: 0, switchT: 0, spin: 0, aim: 0, recoilAcc: 0, bob: 0, stepT: 0, stepOff: 0, sprintAmt: 0, lastFire: 0,
  upg: {}, perks: [], nades: 0, maxHp: 100, meleeT: 0, meleeCd: 0, nadeT: 0, streak: 0, lastKill: -9,
};
const remotes = new Map(), zombies = new Map(), turrets = new Map(), pickups = new Map(), barricades = new Map();
let barrBoxes = [];
const wstat = (id = me.weapon) => weaponStats(id, me.upg[id] || 0, me.perks);
let crateSpinT = 0;
const dbg = { pauseSnap: false };
const hitboxes = [];
let snap = { wave: 0, phase: 'prep', timer: 0, left: 0, genHp: GENERATOR_HP, pu: [0, 0, 0] };
let placing = null, flashlight = null, flashOn = true;
const keys = {};
let mouseDown = false, mouseClicked = false, rightDown = false, locked = false;
let shopOpen = false, chatOpen = false;
let lookDX = 0, lookDY = 0, frameLookDX = 0;
let T = 0;
let prevTouchFire = false;
let NOLOCK = new URLSearchParams(location.search).has('nolock'); // Modus ohne Mauszeiger-Sperre (Testmodus oder automatischer Fallback)
let lockTimer = 0, noLockHinted = false;
function enableNoLock() {
  if (NOLOCK) return;
  NOLOCK = true; clearTimeout(lockTimer);
  if (inGame && !shopOpen) ui.show('pause', false);
  if (!noLockHinted) {
    noLockHinted = true;
    ui.toast('Maus-Sperre nicht verfügbar – Steuerung ohne Sperre aktiv. Maus bewegen = umsehen, Esc = Pause. Für echte Maussteuerung das Spiel in Chrome/Safari/Firefox öffnen.', '', 10);
  }
}

// ------------------------------------------------------------------ Netzwerk
function connect() {
  ui.setConn('Verbinde mit Server…');
  ws = new WebSocket(`${location.protocol === 'https:' ? 'wss' : 'ws'}://${location.host}/ws`);
  ws.onopen = () => ui.setConn('✔ Mit Server verbunden', 'ok');
  ws.onmessage = e => { let m; try { m = JSON.parse(e.data); } catch { return; } onMsg(m); };
  ws.onclose = () => {
    ui.setConn('✖ Verbindung verloren – neuer Versuch…', 'bad');
    if (inGame || gameOver) { cleanupGame(); ui.show('over', false); ui.show('pause', false); ui.show('menu'); }
    $('startbtn').disabled = true;
    setTimeout(connect, 2000);
  };
}
const send = o => { if (ws && ws.readyState === 1) ws.send(JSON.stringify(o)); };
const myName = () => (settings.name || '').trim() || `Spieler ${myId ?? ''}`.trim();

function onMsg(m) {
  switch (m.t) {
    case 'welcome':
      myId = m.id; lanUrls = m.urls || [];
      myColor = PLAYER_COLORS.includes(settings.color) ? settings.color : m.color;
      send({ t: 'hello', name: myName(), color: myColor });
      break;
    case 'lobby': {
      lobby = m;
      const mine = m.players.find(p => p.id === myId); if (mine) myColor = mine.color;
      ui.renderLobby(m, myId, myColor, lanUrls);
      for (const r of remotes.values()) { const p = m.players.find(q => q.id === r.id); if (p && p.name !== r.name) relabel(r, p.name); }
      break;
    }
    case 'start': startGame(m); break;
    case 'snap': if (inGame && !dbg.pauseSnap) applySnap(m); break;
    case 'inv': {
      const prevTier = me.upg[me.weapon] || 0, prevPerks = me.perks.join();
      me.owned = WEAPON_ORDER.filter(id => m.owned.includes(id)); me.money = m.money;
      me.upg = m.upg || {}; me.perks = m.perks || []; me.nades = m.nades || 0; me.maxHp = m.maxHp || 100;
      for (const id of me.owned) if (!me.ammo[id]) initAmmo(id);
      if ((me.upg[me.weapon] || 0) !== prevTier || prevPerks !== me.perks.join()) { const a = me.ammo[me.weapon]; if (a) a.mag = Math.max(a.mag, Math.min(wstat().mag, a.mag + 10)); buildViewmodel(); }
      refreshShop();
      break;
    }
    case 'crate': onCrateResult(m.item); break;
    case 'bought': onBought(m); break;
    case 'respawn':
      me.pos.set(m.x, m.y, m.z); me.vel.set(0, 0, 0);
      ui.toast('Du bist zurück im Kampf!', 'good');
      break;
    case 'over': onGameOver(m); break;
    case 'chat': ui.chat(m); break;
  }
}

// ------------------------------------------------------------------ Spielstart / Ende
function startGame(m) {
  cleanupGame();
  gameOver = false;
  mapDef = MAPS[m.map];
  renderer.shadowMap.enabled = settings.quality !== 'low';
  renderer.setPixelRatio(pixelRatio());
  world = buildWorld(scene, mapDef, settings.quality);
  ui.buildMinimapBase(mapDef);
  me.pos.set(m.you.x, m.you.y, m.you.z); me.vel.set(0, 0, 0);
  const dx = me.pos.x - mapDef.gen.x, dz = me.pos.z - mapDef.gen.z;
  me.yaw = Math.atan2(-dx, -dz); me.pitch = -0.05;
  Object.assign(me, { state: 'alive', hp: 100, maxHp: 100, armor: 0, weapon: 'pistol', owned: ['pistol'], ammo: {}, upg: {}, perks: [], nades: 0, fireCd: 0, reloadT: 0, switchT: 0, spin: 0, aim: 0, recoilAcc: 0, meleeT: 0, meleeCd: 0, nadeT: 0, streak: 0, lastKill: -9 });
  crateSpinT = 0; ui.crateSpin(false); ui.show('wavesum', false);
  initAmmo('pistol');
  // Taschenlampe in der Nacht
  if (mapDef.night) {
    flashlight = new THREE.SpotLight('#fff3d6', 90, 40, 0.5, 0.55, 1.3);
    flashlight.position.set(0.25, -0.25, 0); camera.add(flashlight);
    flashlight.target.position.set(0, -0.5, -6); camera.add(flashlight.target);
    flashOn = true;
  }
  vmHemi.intensity = mapDef.night ? 0.7 : 1.6; vmSun.intensity = mapDef.night ? 0.9 : 1.6;
  inGame = true;
  buildViewmodel();
  ui.show('menu', false); ui.show('over', false); ui.show('hud');
  touch.show(true);
  initAudio();
  ui.announce(mapDef.name.toUpperCase(), 'Verteidige den Generator! &nbsp;<kbd>B</kbd> Shop', 4);
  if (!touch.enabled && !NOLOCK && document.pointerLockElement !== canvas) ui.show('pause');
}

function cleanupGame() {
  inGame = false;
  cancelPlacing();
  if (world) { world.dispose(); world = null; }
  for (const id of [...remotes.keys()]) removeRemote(id);
  for (const z of [...zombies.values()]) removeZombie(z);
  for (const t of [...turrets.values()]) removeTurret(t);
  for (const pk of [...pickups.values()]) removePickup(pk);
  for (const b of [...barricades.values()]) removeBarricade(b);
  hitboxes.length = 0;
  fx.clear();
  if (flashlight) { camera.remove(flashlight); camera.remove(flashlight.target); flashlight.dispose(); flashlight = null; }
  ui.show('hud', false); ui.show('shop', false); ui.show('pause', false); shopOpen = false;
  ui.downed(null); ui.scope(false); ui.prompt(null); ui.progress(null); ui.placeHint(null);
  touch.show(false);
  if (chatOpen) closeChat();
}

function onGameOver(m) {
  gameOver = true; inGame = false;
  if (document.pointerLockElement) document.exitPointerLock();
  touch.show(false);
  ui.show('shop', false); ui.show('pause', false); ui.show('hud', false); shopOpen = false;
  cancelPlacing(); ui.downed(null);
  ui.showOver(m);
}

// ------------------------------------------------------------------ Munition & Waffen
function initAmmo(id) { const w = wstat(id); me.ammo[id] = { mag: w.mag, res: w.reserve }; }
function equip(id) {
  if (!me.owned.includes(id) || (me.weapon === id && me.switchT <= 0)) return;
  me.weapon = id; me.switchT = 0.35; me.reloadT = 0; me.spin = 0;
  buildViewmodel();
}
function cycleWeapon(dir) {
  const i = me.owned.indexOf(me.weapon);
  equip(me.owned[(i + dir + me.owned.length) % me.owned.length]);
}
function startReload() {
  const w = wstat(), a = me.ammo[me.weapon];
  if (me.reloadT > 0 || a.mag >= w.mag || a.res <= 0 || me.switchT > 0) return;
  me.reloadT = w.reload; sfx.reload();
}
function finishReload() {
  const w = wstat(), a = me.ammo[me.weapon];
  const take = Math.min(w.mag - a.mag, a.res);
  a.mag += take; if (me.weapon !== 'pistol') a.res -= take;
}

// ------------------------------------------------------------------ Viewmodel (Waffe in der Hand)
const vm = { root: new THREE.Group(), gun: null, flash: null, flashT: 0, kick: 0, swayX: 0, swayY: 0 };
vmScene.add(vm.root);
const AIM_POS = { pistol: [0, -0.085, -0.34], smg: [0, -0.09, -0.3], shotgun: [0, -0.1, -0.32], rifle: [0, -0.135, -0.27], sniper: [0, -0.13, -0.25], launcher: [-0.09, -0.16, -0.3], minigun: [0.16, -0.22, -0.42] };
const FORE = { pistol: [-0.03, -0.09, 0.02], smg: [0, -0.04, -0.28], shotgun: [0, -0.04, -0.36], rifle: [0, -0.04, -0.4], sniper: [0, -0.05, -0.42], launcher: [0, -0.04, -0.34], minigun: [0.14, 0.05, 0.06] };
let glowTexCache = null;
const VM_SCALE = 0.75;
function buildViewmodel() {
  if (vm.gun) vm.root.remove(vm.gun);
  const g = buildGun(me.weapon, me.upg[me.weapon] || 0);
  const shirt = mat(myColor || '#1e88e5'), skin = mat('#f2c94c');
  // rechter Arm + Hand
  const rs = new THREE.Mesh(boxGeo(0.11, 0.11, 0.5), shirt); rs.position.set(0.05, -0.13, 0.3); rs.rotation.set(0.28, 0.12, 0); g.add(rs);
  const rh = new THREE.Mesh(boxGeo(0.095, 0.11, 0.11), skin); rh.position.set(0.0, -0.06, 0.05); g.add(rh);
  // linker Arm zum Vordergriff
  const f = FORE[me.weapon];
  const lh = new THREE.Mesh(boxGeo(0.1, 0.1, 0.11), skin); lh.position.set(f[0], f[1], f[2]); g.add(lh);
  const ls = new THREE.Mesh(boxGeo(0.11, 0.11, 0.55), shirt); ls.position.set(f[0] - 0.13, f[1] - 0.1, f[2] + 0.25); ls.rotation.set(0.25, -0.45, 0); g.add(ls);
  // Mündungsfeuer
  if (!glowTexCache) glowTexCache = fx.glowTex;
  const fl = new THREE.Sprite(new THREE.SpriteMaterial({ map: glowTexCache, color: '#ffd180', blending: THREE.AdditiveBlending, transparent: true, depthWrite: false }));
  fl.scale.setScalar(me.weapon === 'shotgun' || me.weapon === 'launcher' ? 0.35 : 0.24);
  fl.position.copy(g.userData.muzzle.position); fl.visible = false; g.add(fl);
  vm.flash = fl;
  g.traverse(o => { if (o.isMesh) { o.castShadow = false; o.receiveShadow = false; } });
  g.scale.setScalar(VM_SCALE);
  vm.gun = g; vm.root.add(g);
}

function updateViewmodel(dt, moveFrac) {
  const g = vm.gun; if (!g) return;
  const hip = [0.17, -0.17, -0.42], ap = AIM_POS[me.weapon], aimP = [ap[0] * VM_SCALE, ap[1] * VM_SCALE, ap[2] - 0.06];
  const a = me.aim;
  let x = hip[0] + (aimP[0] - hip[0]) * a, y = hip[1] + (aimP[1] - hip[1]) * a, z = hip[2] + (aimP[2] - hip[2]) * a;
  let rx = 0, ry = 0, rz = 0;
  const bobAmt = moveFrac * (1 - a * 0.85) * (me.onGround ? 1 : 0.3);
  x += Math.sin(me.bob) * 0.016 * bobAmt; y -= Math.abs(Math.cos(me.bob)) * 0.014 * bobAmt;
  vm.swayX += (clamp(-frameLookDX * 0.0006, -0.06, 0.06) - vm.swayX) * Math.min(1, dt * 10);
  x += vm.swayX * (1 - a * 0.7); ry += vm.swayX * 1.5;
  vm.kick *= Math.exp(-dt * 14);
  z += vm.kick * 0.07; rx += vm.kick * (me.weapon === 'minigun' ? 0.02 : 0.14);
  if (me.reloadT > 0) {
    const p = 1 - me.reloadT / wstat().reload, d = Math.sin(p * Math.PI);
    rx += d * 0.55; rz += d * 0.5; y -= d * 0.12;
  }
  if (me.meleeT > 0) { // Messerhieb: Waffe schwingt von rechts oben nach links unten
    const p = 1 - me.meleeT / 0.3, d = Math.sin(p * Math.PI);
    x -= d * 0.25; y += d * 0.05 - p * 0.1; z -= d * 0.25; ry += d * 1.1; rz -= d * 0.8; rx -= d * 0.3;
  }
  if (me.nadeT > 0) { const p = 1 - me.nadeT / 0.35, d = Math.sin(p * Math.PI); y -= d * 0.25; rx -= d * 0.5; x += d * 0.1; }
  if (me.switchT > 0) { const s = me.switchT / 0.35; y -= s * 0.4; rx -= s * 0.6; }
  x -= me.sprintAmt * 0.06; ry += me.sprintAmt * 0.6; rx -= me.sprintAmt * 0.2;
  g.position.set(x, y, z); g.rotation.set(rx, ry, rz);
  if (g.userData.spin) g.userData.spin.rotation.z += dt * me.spin * 35;
  vm.flashT -= dt; vm.flash.visible = vm.flashT > 0;
  if (vm.flash.visible) vm.flash.material.rotation = Math.random() * 6;
}

// ------------------------------------------------------------------ Andere Spieler
function lobbyInfo(id) { return lobby?.players.find(p => p.id === id) || { name: 'Spieler', color: '#9e9e9e' }; }
function createRemote(id, x, y, z) {
  const info = lobbyInfo(id);
  const model = buildCharacter({ kind: 'player', skin: '#f2c94c', shirt: info.color, pants: '#2f3b52', hat: info.color, unique: false });
  model.traverse(o => { if (o.isMesh) { o.castShadow = settings.quality !== 'low'; } });
  const label = makeLabel(info.name, '#ffffff'); label.position.y = 2.6; model.add(label);
  const help = makeLabel('✚ HILFE!', '#ff5252', 'rgba(0,0,0,0.6)'); help.position.y = 1.3; help.visible = false; model.add(help);
  model.position.set(x, y, z);
  scene.add(model);
  const r = { id, name: info.name, color: info.color, model, label, help, gun: null, gunType: null, x, y, z, yaw: 0, tx: x, ty: y, tz: z, tyaw: 0, pitch: 0, phase: 0, state: 'alive', hp: 100, maxHp: 100, money: 0, kills: 0, moving: 0, rev: 0, weapon: 'pistol', tier: 0, melee: 0, meleeAnim: 0 };
  remotes.set(id, r);
  return r;
}
function relabel(r, name) {
  r.model.remove(r.label); r.label.material.map.dispose(); r.label.material.dispose();
  r.label = makeLabel(name, '#ffffff'); r.label.position.y = 2.6; r.model.add(r.label); r.name = name;
}
function removeRemote(id) {
  const r = remotes.get(id); if (!r) return;
  scene.remove(r.model);
  for (const s of [r.label, r.help]) { s.material.map.dispose(); s.material.dispose(); }
  remotes.delete(id);
}
function updateRemotes(dt) {
  const k = 1 - Math.exp(-dt * 14);
  for (const r of remotes.values()) {
    r.x += (r.tx - r.x) * k; r.y += (r.ty - r.y) * k; r.z += (r.tz - r.z) * k;
    r.yaw = lerpAngle(r.yaw, r.tyaw, k);
    r.model.visible = r.state !== 'dead';
    r.model.position.set(r.x, r.y, r.z);
    r.model.rotation.y = r.yaw + Math.PI;
    if (r.gunType !== r.weapon + r.tier) {
      const c = r.model.userData.char;
      if (r.gun) c.arms[1].remove(r.gun);
      r.gun = buildGun(r.weapon, r.tier); r.gun.scale.setScalar(0.95);
      r.gun.position.set(0, -BODY.armH + 0.12, 0.02); r.gun.rotation.set(Math.PI / 2, Math.PI, 0);
      r.gun.traverse(o => { if (o.isMesh) o.castShadow = settings.quality !== 'low'; });
      c.arms[1].add(r.gun); r.gunType = r.weapon + r.tier;
    }
    r.phase += dt * 9 * r.moving;
    const c = r.model.userData.char;
    const down = r.state === 'down';
    animateCharacter(r.model, { t: T, walk: down ? 0 : r.moving, phase: r.phase, kind: 'player', aimPitch: down ? 0 : r.pitch });
    if (r.melee) r.meleeAnim = Math.min(1, r.meleeAnim + dt * 8); else r.meleeAnim = Math.max(0, r.meleeAnim - dt * 6);
    if (r.meleeAnim > 0) { c.arms[1].rotation.x -= Math.sin(r.meleeAnim * Math.PI) * 1.2; c.arms[1].rotation.z = -Math.sin(r.meleeAnim * Math.PI) * 0.6; }
    c.body.rotation.x = down ? -Math.PI / 2 : 0;
    c.body.position.y = down ? 0.25 : 0;
    r.help.visible = down;
    if (down) r.help.material.opacity = 0.6 + Math.sin(T * 6) * 0.4;
  }
}
function remoteShot(e) {
  const r = remotes.get(e.by);
  let o = Array.isArray(e.o) ? new V3(e.o[0], e.o[1], e.o[2]) : null;
  if (!o && r) o = new V3(r.x, r.y + 1.5, r.z);
  if (!o) return;
  for (const end of e.ends || []) fx.tracer(o, new V3(end[0], end[1], end[2]), '#fff59d', e.w === 'sniper' ? 0.05 : 0.025);
  fx.muzzle(o, 0.8, true);
  sfx.shoot(e.w, o);
}

// ------------------------------------------------------------------ Zombies
const hitMat = new THREE.MeshBasicMaterial({ visible: false });
function createZombie(id, type, x, z, rot, spawning) {
  const cfg = ZOMBIES[type];
  const wide = type === 'brute' ? 1.3 : type === 'boss' ? 1.35 : 1;
  const model = buildCharacter({ kind: 'zombie', skin: cfg.skin, shirt: cfg.shirt, pants: cfg.pants, eyes: cfg.eyes, scale: cfg.scale, wide, extras: type });
  const shadows = settings.quality === 'high' || type === 'boss';
  model.traverse(o => { if (o.isMesh) { o.castShadow = shadows; o.receiveShadow = false; } });
  const s = cfg.scale;
  let bodyH = (BODY.legH + BODY.torsoH) * s;
  const hb1 = new THREE.Mesh(boxGeo(1, 1, 1), hitMat); hb1.scale.set(BODY.torsoW * wide * s * 1.15, bodyH, BODY.torsoD * s * 1.8); hb1.position.y = bodyH / 2;
  const hb2 = new THREE.Mesh(boxGeo(1, 1, 1), hitMat); hb2.scale.setScalar(BODY.headS * s * 1.3); hb2.position.set(0, bodyH + BODY.headS * s * 0.5, 0.14 * s);
  if (cfg.crawl) { // Kriecher: flacher, Kopf vorne
    bodyH = 0.75 * s;
    hb1.scale.set(BODY.torsoW * s * 1.2, bodyH, 1.3 * s); hb1.position.set(0, bodyH / 2, 0.2 * s);
    hb2.scale.setScalar(BODY.headS * s * 1.3); hb2.position.set(0, 0.55 * s, 0.95 * s);
  }
  hb1.visible = hb2.visible = false;
  model.add(hb1, hb2);
  for (const m of model.userData.char.mats) { if (m.emissive && m !== model.userData.glow) { m.emissive.set('#ffffff'); m.emissiveIntensity = 0; } }
  model.position.set(x, spawning ? -2.2 * s : 0, z);
  model.rotation.y = rot;
  scene.add(model);
  const bar = makeHealthBar(type === 'boss' ? 2.2 : type === 'brute' ? 1.4 : 0.9);
  bar.visible = false; scene.add(bar);
  const zz = { id, type, cfg, model, x, z, rot, tx: x, tz: z, trot: rot, hp: 100, phase: Math.random() * 6, seed: Math.random() * 10, atkT: 0, flash: 0, rise: spawning ? 1 : 0, groanT: 1 + Math.random() * 8, bar, boxes: [hb1, hb2], dying: false, deathT: 0, walk: 0 };
  hb1.userData = { z: zz, head: false }; hb2.userData = { z: zz, head: true };
  hitboxes.push(hb1, hb2);
  zombies.set(id, zz);
  if (spawning) fx.burst(new V3(x, 0.1, z), '#5d4037', 12, 3, 0.15, 1, 12, 2);
  return zz;
}
function removeHitboxes(zz) {
  for (const b of zz.boxes) { const i = hitboxes.indexOf(b); if (i >= 0) hitboxes.splice(i, 1); }
}
function removeZombie(zz) {
  removeHitboxes(zz);
  scene.remove(zz.model); scene.remove(zz.bar);
  for (const m of zz.model.userData.char.mats) m.dispose();
  zz.bar.children.forEach(c => c.material.dispose());
  zombies.delete(zz.id);
}
function killZombieLocal(zz, e) {
  if (zz.dying) return;
  zz.dying = true; zz.deathT = 0; zz.bar.visible = false;
  removeHitboxes(zz);
  const p = new V3(zz.x, 1.2 * zz.cfg.scale, zz.z);
  fx.blood(p, null, true);
  const c = zz.model.userData.char, cols = [zz.cfg.skin, zz.cfg.shirt, zz.cfg.pants];
  if (e && e.h) { // Kopfschuss: Kopf platzt
    c.head.visible = false;
    const hp = new V3(); c.headMesh.getWorldPosition(hp);
    fx.gibs(hp, [zz.cfg.skin, zz.cfg.skin, '#8b0000'], 9, 4);
    fx.burst(hp, zz.cfg.eyes, 5, 3, 0.07, 0.5);
  }
  if ((e && e.big) || (zz.bigDeath && T - zz.bigDeath < 0.4)) { // Explosion: ganze Figur zerlegt sich
    fx.gibs(p, cols, 14, 7); zz.model.visible = false; zz.deathT = 3;
  } else if (Math.random() < 0.35) fx.gibs(p, cols, 4, 3);
  if (Math.random() < 0.5) sfx.groan(p, zz.type === 'boss' ? 0.5 : 1.3);
}
function updateZombies(dt) {
  const k = 1 - Math.exp(-dt * 12);
  for (const zz of [...zombies.values()]) {
    const c = zz.model.userData.char, s = zz.cfg.scale;
    if (zz.dying) {
      zz.deathT += dt;
      const p = Math.min(1, zz.deathT / 0.45);
      c.body.rotation.x = -(Math.PI / 2) * p * p;
      c.body.position.y = 0.2 * s * p;
      if (zz.deathT > 2.2) zz.model.position.y -= dt * 0.9;
      if (zz.deathT > 3.6) removeZombie(zz);
      continue;
    }
    const ox = zz.x, oz = zz.z;
    zz.x += (zz.tx - zz.x) * k; zz.z += (zz.tz - zz.z) * k;
    zz.rot = lerpAngle(zz.rot, zz.trot, k);
    const spd = Math.hypot(zz.x - ox, zz.z - oz) / Math.max(dt, 1e-3);
    zz.walk += (clamp(spd / (zz.cfg.speed * 0.8), 0, 1) - zz.walk) * Math.min(1, dt * 8);
    let y = 0;
    if (zz.rise > 0) {
      zz.rise -= dt; y = -2.2 * s * Math.max(0, zz.rise);
      if (Math.random() < 0.3) fx.particle(new V3(zz.x + (Math.random() - 0.5), 0.05, zz.z + (Math.random() - 0.5)), new V3(0, 2, 0), '#4e342e', 0.12, 0.6);
    }
    zz.model.position.set(zz.x, y, zz.z);
    zz.model.rotation.y = zz.rot;
    zz.phase += dt * zz.walk * (zz.type === 'runner' ? 15 : 8) / Math.sqrt(s);
    let attack = 0;
    if (zz.atkT > 0) { zz.atkT -= dt; attack = 1 - Math.max(0, zz.atkT) / 0.5; }
    animateCharacter(zz.model, { t: T, walk: zz.walk, phase: zz.phase, kind: 'zombie', attack, seed: zz.seed, crawl: zz.cfg.crawl });
    const frozen = snap.pu[2] > 0;
    if (zz.flash > 0 || frozen || zz.wasFrozen) {
      zz.flash = Math.max(0, zz.flash - dt * 6);
      for (const m of c.mats) if (m !== zz.model.userData.glow) {
        if (frozen) { m.emissive.set('#4fc3f7'); m.emissiveIntensity = 0.55 + zz.flash * 0.6; }
        else { m.emissive.set('#ffffff'); m.emissiveIntensity = zz.flash * 0.7; }
      }
      zz.wasFrozen = frozen;
    }
    if (zz.model.userData.glow) zz.model.userData.glow.emissiveIntensity = 1.2 + Math.sin(T * 9 + zz.seed) * 0.9;
    // Lebensbalken
    const showBar = zz.hp < 100 || zz.type === 'boss' || zz.type === 'brute';
    zz.bar.visible = showBar && zz.rise <= 0;
    if (zz.bar.visible) {
      zz.bar.position.set(zz.x, (BODY.legH + BODY.torsoH + BODY.headS) * s + 0.45, zz.z);
      zz.bar.quaternion.copy(camera.quaternion);
      zz.bar.userData.set(zz.hp / 100);
    }
    zz.groanT -= dt;
    if (zz.groanT <= 0) { zz.groanT = 4 + Math.random() * 9; sfx.groan(zz.model.position, zz.type === 'boss' ? 0.5 : zz.type === 'brute' ? 0.7 : zz.type === 'runner' ? 1.35 : 1); }
  }
}

// ------------------------------------------------------------------ Power-ups am Boden
function createPickup(id, kind, x, z) {
  const cfg = POWERUPS[kind];
  const model = buildPickup(cfg); model.position.set(x, 0, z); scene.add(model);
  const pk = { id, kind, cfg, model, x, z, t: 99, seed: Math.random() * 6 };
  pickups.set(id, pk);
  fx.burst(new V3(x, 1, z), cfg.color, 12, 3, 0.1, 0.8, 4, 2);
  return pk;
}
function removePickup(pk) {
  scene.remove(pk.model); pk.model.userData.mat.dispose(); pk.model.userData.label.material.map.dispose(); pk.model.userData.label.material.dispose();
  pickups.delete(pk.id);
}
function updatePickups(dt) {
  for (const pk of pickups.values()) {
    const u = pk.model.userData;
    u.core.rotation.y += dt * 2.5; u.core.position.y = 1.0 + Math.sin(T * 3 + pk.seed) * 0.15;
    u.ring.rotation.x = Math.PI / 2 + Math.sin(T * 1.5 + pk.seed) * 0.4; u.ring.rotation.z += dt;
    const blink = pk.t < 5 ? (Math.sin(T * 12) > 0 ? 1 : 0.15) : 1;
    u.mat.emissiveIntensity = 1.4 * blink; u.label.material.opacity = blink;
    if (Math.random() < 0.15) fx.particle(new V3(pk.x + (Math.random() - 0.5) * 0.6, 0.3, pk.z + (Math.random() - 0.5) * 0.6), new V3(0, 1.5, 0), pk.cfg.color, 0.06, 0.8, -1);
  }
}

// ------------------------------------------------------------------ Barrikaden
function createBarricade(id, x, z, rot) {
  const model = buildBarricade(); model.position.set(x, 0, z); model.rotation.y = rot ? Math.PI / 2 : 0;
  model.traverse(o => { if (o.isMesh) o.castShadow = settings.quality !== 'low'; });
  scene.add(model);
  const bar = makeHealthBar(1.4); bar.visible = false; scene.add(bar);
  const b = { id, x, z, rot, hp: 100, model, bar, shakeT: 0 };
  barricades.set(id, b);
  fx.burst(new V3(x, 0.4, z), '#8d6e63', 12, 3, 0.12, 0.8, 8, 1);
  rebuildBarrBoxes();
  return b;
}
function removeBarricade(b) {
  scene.remove(b.model); scene.remove(b.bar); b.bar.children.forEach(c => c.material.dispose());
  barricades.delete(b.id); rebuildBarrBoxes();
}
function rebuildBarrBoxes() { barrBoxes = [...barricades.values()].map(barricadeBox); }
function updateBarricades(dt) {
  for (const b of barricades.values()) {
    if (b.shakeT > 0) { b.shakeT -= dt; b.model.rotation.z = Math.sin(T * 40) * 0.03 * b.shakeT; } else b.model.rotation.z = 0;
    b.bar.visible = b.hp < 100;
    if (b.bar.visible) { b.bar.position.set(b.x, 1.7, b.z); b.bar.quaternion.copy(camera.quaternion); b.bar.userData.set(b.hp / 100); }
  }
}

// ------------------------------------------------------------------ Türme
function createTurret(id, type, x, z, yaw, owner) {
  const cfg = TURRETS[type];
  const model = buildTurret(type, cfg.color);
  model.position.set(x, 0, z); model.userData.head.rotation.y = yaw;
  scene.add(model);
  const bar = makeHealthBar(1.0); bar.visible = false; scene.add(bar);
  const t = { id, type, model, x, z, yaw, tyaw: yaw, hp: 100, owner, bar, muzzleIdx: 0 };
  turrets.set(id, t);
  fx.burst(new V3(x, 0.3, z), '#bdbdbd', 10, 3, 0.15, 0.8, 8, 1);
  return t;
}
function removeTurret(t) {
  scene.remove(t.model); scene.remove(t.bar);
  t.bar.children.forEach(c => c.material.dispose());
  turrets.delete(t.id);
}
function updateTurrets(dt) {
  const k = 1 - Math.exp(-dt * 14);
  for (const t of turrets.values()) {
    const head = t.model.userData.head;
    head.rotation.y = lerpAngle(head.rotation.y, t.tyaw, k);
    if (t.model.userData.orb) { t.model.userData.orb.position.y = 1.0 + Math.sin(T * 3 + t.id) * 0.08; t.model.userData.orb.rotation.y += dt * 2; }
    t.bar.visible = t.hp < 100;
    if (t.bar.visible) { t.bar.position.set(t.x, t.type === 'tesla' ? 2.7 : 2.0, t.z); t.bar.quaternion.copy(camera.quaternion); t.bar.userData.set(t.hp / 100); }
  }
}
function turretMuzzle(t) {
  const ms = t.model.userData.muzzles; const m = ms[t.muzzleIdx++ % ms.length];
  t.model.updateMatrixWorld(true);
  return m.getWorldPosition(new V3());
}

// ------------------------------------------------------------------ Snapshot anwenden
function applySnap(m) {
  snap = { wave: m.wv, phase: m.ph, timer: m.tm, left: m.zl, genHp: m.gh, pu: m.pu || [0, 0, 0] };
  for (const e of m.ev) handleEvent(e);
  // Spieler
  const seen = new Set();
  for (const a of m.p) {
    const [id, x, y, z, yaw, pitch, wi, hp, armor, st, money, kills, mv, rev, downT, ready, , maxHp, tier, melee] = a;
    const state = ['alive', 'down', 'dead'][st];
    if (id === myId) {
      const prev = me.state;
      Object.assign(me, { hp, armor, money, kills, state, downT, ready: !!ready, rev, maxHp: maxHp || 100 });
      if (prev !== state) onMyState(prev, state);
      continue;
    }
    seen.add(id);
    const r = remotes.get(id) || createRemote(id, x, y, z);
    Object.assign(r, { tx: x, ty: y, tz: z, tyaw: yaw, pitch, weapon: WEAPON_ORDER[wi] || 'pistol', hp, maxHp: maxHp || 100, state, money, kills, moving: mv, rev, ready: !!ready, tier: tier || 0, melee: melee || 0 });
  }
  for (const id of [...remotes.keys()]) if (!seen.has(id)) removeRemote(id);
  // Zombies
  const zs = new Set();
  for (const a of m.z) {
    const [id, ti, x, z, rot, hp, , spawning] = a;
    zs.add(id);
    let zz = zombies.get(id);
    if (!zz) zz = createZombie(id, ZOMBIE_TYPES[ti], x, z, rot, spawning);
    if (zz.dying) continue;
    zz.tx = x; zz.tz = z; zz.trot = rot; zz.hp = hp;
    if (!spawning && zz.rise > 0.3) zz.rise = Math.min(zz.rise, 0.3);
  }
  for (const zz of [...zombies.values()]) if (!zs.has(zz.id) && !zz.dying) killZombieLocal(zz, null);
  // Türme
  const ts = new Set();
  for (const a of m.tu) {
    const [id, ti, x, z, yaw, hp, owner] = a;
    ts.add(id);
    const t = turrets.get(id) || createTurret(id, TURRET_ORDER[ti], x, z, yaw, owner);
    t.tyaw = yaw; t.hp = hp;
  }
  for (const t of [...turrets.values()]) if (!ts.has(t.id)) removeTurret(t);
  // Barrikaden
  const bs = new Set();
  for (const a of m.ba || []) {
    const [id, x, z, rot, hp] = a; bs.add(id);
    const b = barricades.get(id) || createBarricade(id, x, z, rot);
    b.hp = hp;
  }
  for (const b of [...barricades.values()]) if (!bs.has(b.id)) removeBarricade(b);
  // Power-ups
  const ps = new Set();
  for (const a of m.pk || []) {
    const [id, ki, x, z, t] = a; ps.add(id);
    const pk = pickups.get(id) || createPickup(id, POWERUP_ORDER[ki], x, z);
    pk.t = t;
  }
  for (const pk of [...pickups.values()]) if (!ps.has(pk.id)) removePickup(pk);
  if (shopOpen && (me.money !== refreshShop.lastMoney || canShopLocal() !== refreshShop.lastCan)) refreshShop();
}

function onMyState(prev, state) {
  if (state === 'down') { ui.announce('AM BODEN!', 'Ein Mitspieler kann dich mit <kbd>E</kbd> wiederbeleben', 2.5); cancelPlacing(); if (shopOpen) closeShop(); }
  if (state === 'alive' && prev !== 'alive') { ui.downed(null); me.vel.set(0, 0, 0); }
}

function playerName(id) { if (id === myId) return myName(); return remotes.get(id)?.name || lobbyInfo(id).name; }
function playerColor(id) { if (id === myId) return myColor; return remotes.get(id)?.color || lobbyInfo(id).color; }

function handleEvent(e) {
  switch (e.e) {
    case 'hurt':
      if (e.id === myId) { ui.hurt(e.d); sfx.hurt(); fx.shake = Math.max(fx.shake, 0.2); touch.vibrate(45); }
      break;
    case 'zd': {
      const zz = zombies.get(e.id);
      const name = zz ? zz.cfg.name : 'Zombie';
      if (zz) killZombieLocal(zz, e);
      if (e.by === myId) {
        sfx.kill(); ui.hitmarker(true); touch.vibrate(25);
        if (e.m) fx.textNumber(new V3(e.x, (zz ? 2.2 * zz.cfg.scale : 2), e.z), `+$${e.m}`, 'money');
        if (e.s >= 3) {
          const txt = e.s >= 15 ? 'UNAUFHALTSAM!' : e.s >= 10 ? 'MASSAKER!' : e.s >= 7 ? 'MEGA-SERIE!' : e.s >= 5 ? 'KILL-SERIE ×5!' : `${e.s}er-SERIE!`;
          ui.streak(txt, e.s >= 10 ? '#ff1744' : e.s >= 5 ? '#ff9100' : '#ffd600');
          if (e.s === 5 || e.s === 10 || e.s === 15) sfx.streak();
        }
      }
      if (e.by >= 0) ui.feed(`<span style="color:${playerColor(e.by)}">${esc(playerName(e.by))}</span> ☠ ${name}${e.h ? ' 🎯' : ''}${e.s >= 3 ? ` <b style="color:#ffd600">×${e.s}</b>` : ''}`);
      break;
    }
    case 'pu': {
      const cfg = POWERUPS[e.k];
      ui.announce(`${cfg.icon} ${cfg.name}`, `${cfg.desc} <span style="opacity:.7">(${esc(playerName(e.by))})</span>`, 2.5);
      sfx.powerup(); touch.vibrate([30, 40, 30]);
      if (e.k === 'nuke') { ui.nukeFlash(); sfx.explosion(camera.position); fx.shake = 1; let n = 0; for (const zz of zombies.values()) if (!zz.dying) { zz.bigDeath = T; if (n++ < 10) fx.explosion(new V3(zz.x, 1, zz.z), 2); } }
      if (e.k === 'ammo') { for (const id of me.owned) initAmmo(id); }
      break;
    }
    case 'zs': { // Spucker spuckt
      const zz = zombies.get(e.id); if (!zz) break;
      zz.atkT = 0.5;
      fx.spit(new V3(zz.x, 1.6 * zz.cfg.scale, zz.z), new V3(e.x, 0.1, e.z), zz.cfg.spit.travel);
      sfx.spit(zz.model.position);
      break;
    }
    case 'zsl': fx.acidPool(new V3(e.x, 0, e.z), 2); break;
    case 'nd': if (e.by !== myId && Array.isArray(e.o)) launchGrenade(new V3(e.o[0], e.o[1], e.o[2]), new V3(e.v[0], e.v[1], e.v[2]), false); break;
    case 'cs': if (world && world.crate) { world.crateSpin = CRATE.spin; sfx.crate(world.crate.position); } break;
    case 'crate': if (e.by !== myId) ui.feed(`<span style="color:${playerColor(e.by)}">${esc(playerName(e.by))}</span> 🎁 ${e.kind === 'weapon' ? WEAPONS[e.id].name : e.kind === 'money' ? 'Geld' : 'Niete'}`); break;
    case 'za': { const zz = zombies.get(e.id); if (zz) { zz.atkT = 0.5; if (Math.random() < 0.25) sfx.groan(zz.model.position, 1.1); } break; }
    case 'bm': {
      if (e.by === myId) break;
      const p = new V3(e.x, e.y || 0.6, e.z);
      fx.explosion(p, e.r || 4); sfx.explosion(p);
      for (const zz of zombies.values()) if (!zz.dying && Math.hypot(zz.x - p.x, zz.z - p.z) < (e.r || 4) * 0.7) zz.bigDeath = T;
      break;
    }
    case 'sh': if (e.by !== myId) remoteShot(e); break;
    case 'ts': {
      const t = turrets.get(e.id); if (!t) break;
      const from = turretMuzzle(t), to = new V3(e.x, 1.1 * (e.s || 1), e.z);
      fx.tracer(from, to, '#ffecb3', 0.03); fx.muzzle(from, 0.5, Math.random() < 0.3);
      if (Math.random() < 0.5) fx.blood(to, null);
      sfx.shoot('mg', from);
      break;
    }
    case 'tc': {
      const t = turrets.get(e.id); if (!t) break;
      const from = turretMuzzle(t);
      fx.tracer(from, new V3(e.x, 0.8, e.z), '#ffab40', 0.12, 0.12); fx.muzzle(from, 1.4, true);
      sfx.shoot('cannon', from);
      break;
    }
    case 'tt': {
      const t = turrets.get(e.id); if (!t) break;
      const pts = [turretMuzzle(t), ...e.pts.map(p => new V3(p[0], 1.2 * (p[2] || 1), p[1]))];
      fx.lightning(pts); sfx.zap(pts[0]);
      for (const p of pts.slice(1)) fx.blood(p, null);
      break;
    }
    case 'td': {
      const t = turrets.get(e.id);
      const p = new V3(e.x, 1, e.z);
      fx.explosion(p, 2.5); sfx.explosion(p);
      if (t && t.owner === myId) ui.toast('Einer deiner Türme wurde zerstört!', 'bad');
      if (t) removeTurret(t);
      break;
    }
    case 'tp': sfx.place(); break;
    case 'bp': sfx.place(); break;
    case 'bh': { const b = barricades.get(e.id); if (b) { b.shakeT = 0.3; fx.burst(new V3(e.x, 0.8, e.z), '#8d6e63', 6, 3, 0.08, 0.6); } sfx.woodHit(new V3(e.x, 1, e.z)); break; }
    case 'bd': {
      const b = barricades.get(e.id);
      const p = new V3(e.x, 0.6, e.z);
      fx.gibs(p, ['#8d6e63', '#5d4037', '#a1887f'], 16, 6); sfx.woodBreak(p);
      if (b) removeBarricade(b);
      ui.toast('Eine Barrikade wurde zerstört!', 'bad');
      break;
    }
    case 'down': if (e.id !== myId) sfx.hurt(); break;
    case 'ws': {
      const k = WAVE_KINDS[e.kind] || WAVE_KINDS.normal;
      ui.announce(k.name ? `WELLE ${e.w} · ${k.name}` : `WELLE ${e.w}`, k.sub, 3.2);
      sfx.waveStart(e.kind);
      ui.show('wavesum', false);
      break;
    }
    case 'wc':
      ui.waveSummary(e, myId, id => ({ name: playerName(id), color: playerColor(id) }));
      sfx.waveClear();
      break;
  }
}

// ------------------------------------------------------------------ Schießen
const ray = new THREE.Raycaster();
function camBasis() {
  const q = camera.quaternion;
  return { f: new V3(0, 0, -1).applyQuaternion(q), r: new V3(1, 0, 0).applyQuaternion(q), u: new V3(0, 1, 0).applyQuaternion(q) };
}
function muzzleWorld() {
  const a = me.aim;
  const off = new V3(0.2 * (1 - a), -0.16 + 0.08 * a, -0.85).applyQuaternion(camera.quaternion);
  return camera.position.clone().add(off);
}
function currentSpread() {
  const w = WEAPONS[me.weapon];
  const hs = Math.hypot(me.vel.x, me.vel.z);
  let s = w.spread * (1 + hs / 5.4 * 0.8) * (me.onGround ? 1 : 2.2);
  if (me.weapon === 'sniper') s = me.aim > 0.9 ? w.spread : 0.05;
  else if (me.weapon === 'shotgun') s *= 1 - me.aim * 0.25;
  else s *= 1 - me.aim * 0.6;
  if (me.weapon === 'minigun') s *= 1 + (1 - me.spin) * 0.5;
  return s;
}

function shoot() {
  const w = wstat(), a = me.ammo[me.weapon];
  a.mag--; me.fireCd = w.rate; me.lastFire = T;
  camera.updateMatrixWorld();
  const origin = camera.position.clone();
  const { f, r, u } = camBasis();
  const spread = currentSpread();
  const mz = muzzleWorld();
  vm.kick = 1; vm.flashT = 0.05;
  fx.muzzle(mz, me.weapon === 'shotgun' ? 1.2 : 0.8, true);
  sfx.shoot(me.weapon, null);
  // Rückstoß
  const kick = w.recoil * (0.7 + Math.random() * 0.5) * (1 - me.aim * 0.4);
  me.pitch += kick; me.recoilAcc += kick; me.yaw += (Math.random() - 0.5) * w.recoil * 0.5;

  if (me.weapon === 'launcher') { fireGrenade(mz, f); send({ t: 'shot', w: me.weapon, o: [mz.x, mz.y, mz.z].map(v => +v.toFixed(2)), ends: [] }); return; }

  const targets = world.rayTargets.concat(hitboxes);
  const hits = new Map();
  const ends = [];
  let anyHit = false, anyHead = false;
  for (let p = 0; p < w.pellets; p++) {
    const ang = Math.random() * Math.PI * 2, rr = Math.sqrt(Math.random()) * spread;
    const dir = f.clone().addScaledVector(r, Math.cos(ang) * rr).addScaledVector(u, Math.sin(ang) * rr).normalize();
    ray.set(origin, dir); ray.far = 150;
    const list = ray.intersectObjects(targets, false);
    let pierce = w.pierce, end = origin.clone().addScaledVector(dir, 150);
    const done = new Set();
    for (const it of list) {
      const ud = it.object.userData;
      if (ud.z) {
        const zz = ud.z;
        if (done.has(zz) || zz.dying) continue;
        done.add(zz);
        let dmg = w.damage * (ud.head ? (me.weapon === 'sniper' ? 2.5 : 2) : 1);
        if (me.weapon === 'shotgun' && it.distance > 8) dmg *= Math.max(0.35, 1 - (it.distance - 8) / 20);
        const h = hits.get(zz.id) || { dmg: 0, head: false };
        h.dmg += dmg; h.head = h.head || ud.head; hits.set(zz.id, h);
        zz.flash = 1; anyHit = true; anyHead = anyHead || ud.head;
        fx.blood(it.point, dir);
        fx.damageNumber(it.point, dmg, ud.head);
        if (--pierce <= 0) { end = it.point; break; }
      } else {
        end = it.point;
        fx.sparks(it.point, it.face ? it.face.normal.clone().transformDirection(it.object.matrixWorld) : null);
        break;
      }
    }
    ends.push(end);
    fx.tracer(mz, end, '#fff59d', me.weapon === 'sniper' ? 0.05 : 0.022, me.weapon === 'sniper' ? 0.15 : 0.06);
  }
  if (anyHit) { ui.hitmarker(false); anyHead ? sfx.headshot() : sfx.hit(); touch.vibrate(anyHead ? 20 : 10); }
  if (hits.size) send({ t: 'hit', w: me.weapon, hits: [...hits].map(([id, h]) => [id, Math.round(h.dmg), h.head ? 1 : 0]) });
  send({ t: 'shot', w: me.weapon, o: [mz.x, mz.y, mz.z].map(v => +v.toFixed(2)), ends: ends.slice(0, 10).map(e => [+e.x.toFixed(2), +e.y.toFixed(2), +e.z.toFixed(2)]) });
}

/** Flächenschaden an Zombies melden (Granate/Werfer) */
function splashHits(p, w, label) {
  const hits = [];
  for (const zz of zombies.values()) {
    if (zz.dying || zz.rise > 0) continue;
    const d = Math.hypot(zz.x - p.x, (zz.cfg.scale - p.y) * 0.5, zz.z - p.z);
    if (d < w.splash + zz.cfg.radius) {
      const dmg = w.damage * (1 - 0.5 * Math.min(1, d / w.splash));
      hits.push([zz.id, Math.round(dmg), 0]); zz.flash = 1; zz.bigDeath = T;
      fx.damageNumber(new V3(zz.x, 2 * zz.cfg.scale, zz.z), dmg);
    }
  }
  if (hits.length) { send({ t: 'hit', w: label, hits }); ui.hitmarker(false); sfx.hit(); }
}
/** Handgranate: hüpft, explodiert nach Zündzeit. mine=false für Granaten anderer Spieler (nur Optik). */
function launchGrenade(from, vel, mine) {
  const w = GRENADE;
  fx.projectile(from, vel, (a, b, pr) => {
    if (pr.life > 0.02) {
      // an Wänden abprallen
      const seg = b.clone().sub(a); const len = seg.length();
      if (len > 1e-4) {
        ray.set(a, seg.clone().normalize()); ray.far = len + 0.1;
        const hit = ray.intersectObjects(world.rayTargets, false)[0];
        if (hit && !hit.object.userData.ground) {
          const nrm = hit.face.normal.clone().transformDirection(hit.object.matrixWorld);
          pr.v.reflect(nrm).multiplyScalar(0.45); b.copy(hit.point).addScaledVector(nrm, 0.1);
        }
      }
      return false;
    }
    const p = b.clone(); p.y = Math.max(0.1, p.y);
    fx.explosion(p, w.splash); sfx.explosion(p);
    if (mine) { send({ t: 'boom', w: 'grenade', p: [p.x, p.y, p.z].map(v => +v.toFixed(2)) }); splashHits(p, w, 'grenade'); }
    return true;
  }, { fuse: w.fuse, bounce: 0.45, color: '#37474f', emissive: '#ff5252', size: 0.16, box: true, gravity: 14, trail: null });
}
function throwGrenade() {
  if (me.nades <= 0) { ui.toast('Keine Granaten – im Shop kaufen (Ausrüstung)', 'bad'); sfx.deny(); return; }
  if (me.nadeT > 0 || me.reloadT > 0 || me.state !== 'alive') return;
  me.nades--; me.nadeT = 0.35;
  camera.updateMatrixWorld();
  const { f } = camBasis();
  const o = camera.position.clone().add(new V3(0.25, -0.15, -0.3).applyQuaternion(camera.quaternion));
  const v = f.clone().multiplyScalar(14); v.y += 4;
  launchGrenade(o, v, true);
  send({ t: 'nade', o: [o.x, o.y, o.z].map(n => +n.toFixed(2)), v: [v.x, v.y, v.z].map(n => +n.toFixed(2)) });
  sfx.throw();
}
function meleeAttack() {
  if (me.meleeCd > 0 || me.state !== 'alive' || me.switchT > 0) return;
  me.meleeCd = MELEE.rate; me.meleeT = 0.3; me.reloadT = 0;
  sfx.swing();
  camera.updateMatrixWorld();
  const { f } = camBasis();
  const hits = [];
  let any = false, head = false;
  for (const zz of zombies.values()) {
    if (zz.dying || zz.rise > 0) continue;
    const dx = zz.x - me.pos.x, dz = zz.z - me.pos.z, d = Math.hypot(dx, dz);
    if (d > MELEE.range + zz.cfg.radius) continue;
    const dot = (dx * f.x + dz * f.z) / (d || 1);
    if (dot < 0.5) continue;
    const isHead = me.pitch > -0.15 && zz.cfg.scale <= 1.2 && !zz.cfg.crawl;
    const dmg = MELEE.damage * (isHead ? 1.5 : 1);
    hits.push([zz.id, Math.round(dmg), isHead ? 1 : 0]); zz.flash = 1; any = true; head = head || isHead;
    const hp = new V3(zz.x, 1.3 * zz.cfg.scale, zz.z);
    fx.blood(hp, f); fx.damageNumber(hp, dmg, false);
    fx.textNumber(hp.clone().add(new V3(0, 0.4, 0)), '🔪', 'melee', 0.5);
  }
  if (any) { send({ t: 'hit', w: 'melee', hits }); ui.hitmarker(false); head ? sfx.headshot() : sfx.hit(); fx.shake = Math.max(fx.shake, 0.15); }
}

function fireGrenade(from, dir) {
  const w = WEAPONS.launcher;
  const vel = dir.clone().multiplyScalar(42); vel.y += 2;
  fx.projectile(from, vel, (a, b) => {
    const seg = b.clone().sub(a); const len = seg.length();
    ray.set(a, seg.normalize()); ray.far = len;
    const hit = ray.intersectObjects(world.rayTargets.concat(hitboxes), false)[0];
    let p = null;
    if (hit) p = hit.point; else if (b.y <= 0.05) p = new V3(b.x, 0.05, b.z);
    if (!p) return false;
    fx.explosion(p, w.splash); sfx.explosion(p);
    send({ t: 'boom', p: [p.x, p.y, p.z].map(v => +v.toFixed(2)) });
    splashHits(p, { damage: wstat('launcher').damage, splash: w.splash }, 'launcher');
    return true;
  });
}

// ------------------------------------------------------------------ Geschütz platzieren
function canShopLocal() {
  if (me.state !== 'alive') return false;
  if (snap.phase === 'prep') return true;
  return mapDef && Math.hypot(me.pos.x - mapDef.shop.x, me.pos.z - mapDef.shop.z) < SHOP_RADIUS;
}
function startPlacing(type) {
  cancelPlacing();
  const isBarr = type === 'barricade';
  const cfg = isBarr ? BARRICADE : TURRETS[type];
  const ghost = isBarr ? buildBarricade() : buildTurret(type, cfg.color);
  const gm = new THREE.MeshBasicMaterial({ color: '#4caf50', transparent: true, opacity: 0.5, depthWrite: false });
  ghost.traverse(o => { if (o.isMesh) { o.material = gm; o.castShadow = false; } });
  const ring = new THREE.Mesh(new THREE.RingGeometry(isBarr ? 0.01 : cfg.range - 0.15, isBarr ? 0.02 : cfg.range, 64), new THREE.MeshBasicMaterial({ color: '#4caf50', transparent: true, opacity: 0.35, side: THREE.DoubleSide, depthWrite: false }));
  ring.rotation.x = -Math.PI / 2; ring.position.y = 0.06; ghost.add(ring); ring.visible = !isBarr;
  scene.add(ghost);
  placing = { type, ghost, gm, ring, valid: false, x: 0, z: 0, rot: 0, pending: false };
  ui.placeHint(`<b>${cfg.name}</b> platzieren ($${cfg.price}) &nbsp;·&nbsp; <kbd>Linksklick</kbd> setzen &nbsp;·&nbsp; <kbd>Q</kbd> / <kbd>Rechtsklick</kbd> abbrechen${isBarr ? ' &nbsp;·&nbsp; steht quer zu deiner Blickrichtung' : ''}`);
  touch.setPlacing(true);
}
function boxesOverlap(a, b, m = 0) { return a.minX < b.maxX + m && a.maxX > b.minX - m && a.minZ < b.maxZ + m && a.maxZ > b.minZ - m; }
function barricadeSpotValid(x, z, rot) {
  const box = barricadeBox({ x, z, rot });
  const half = mapDef.size / 2 - 1.5;
  if (Math.abs(x) > half || Math.abs(z) > half) return false;
  for (const b of world.solids) if (b.minY < 1.2 && boxesOverlap(box, b, 0.25)) return false;
  for (const b of barrBoxes) if (boxesOverlap(box, b, -0.35)) return false;
  for (const t of turrets.values()) if (pushCircleOutOfBox(t.x, t.z, 0.8, box)) return false;
  if (pushCircleOutOfBox(me.pos.x, me.pos.z, 0.5, box)) return false;
  for (const r of remotes.values()) if (r.state === 'alive' && pushCircleOutOfBox(r.x, r.z, 0.5, box)) return false;
  if (Math.hypot(x - mapDef.gen.x, z - mapDef.gen.z) < 3.2) return false;
  if (Math.hypot(x - mapDef.shop.x, z - mapDef.shop.z) < 2.2) return false;
  if (mapDef.crate && Math.hypot(x - mapDef.crate.x, z - mapDef.crate.z) < 2.2) return false;
  return true;
}
function cancelPlacing() {
  if (!placing) return;
  scene.remove(placing.ghost); placing.gm.dispose(); placing.ring.material.dispose(); placing.ring.geometry.dispose();
  placing = null; ui.placeHint(null); touch.setPlacing(false);
}
function spotValid(x, z) {
  const half = mapDef.size / 2 - 1.5;
  if (Math.abs(x) > half || Math.abs(z) > half) return false;
  for (const b of world.solids) if (pushCircleOutOfBox(x, z, 0.7, b)) return false;
  for (const b of barrBoxes) if (pushCircleOutOfBox(x, z, 0.7, b)) return false;
  for (const t of turrets.values()) if (Math.hypot(x - t.x, z - t.z) < 1.6) return false;
  if (Math.hypot(x - me.pos.x, z - me.pos.z) < 0.9) return false;
  return true;
}
function updatePlacing() {
  if (!placing) return;
  const isBarr = placing.type === 'barricade';
  const cfg = isBarr ? BARRICADE : TURRETS[placing.type];
  const { f } = camBasis();
  ray.set(camera.position, f); ray.far = 9;
  const hit = ray.intersectObjects(world.rayTargets, false)[0];
  let x, z, blocked = false;
  if (hit) { x = hit.point.x; z = hit.point.z; blocked = !hit.object.userData.ground; }
  else { const p = camera.position.clone().addScaledVector(f, 5); x = p.x; z = p.z; }
  placing.x = x; placing.z = z;
  placing.ghost.position.set(x, 0, z);
  if (isBarr) {
    // Barrikade steht quer zur Blickrichtung (achsenparallel)
    placing.rot = Math.abs(Math.sin(me.yaw)) > 0.707 ? 1 : 0;
    placing.ghost.rotation.y = placing.rot ? Math.PI / 2 : 0;
  } else placing.ghost.rotation.y = me.yaw + Math.PI;
  let reason = '';
  if (blocked || (isBarr ? !barricadeSpotValid(x, z, placing.rot) : !spotValid(x, z))) reason = 'Kein Platz';
  else if (me.money < cfg.price) reason = 'Zu wenig Geld';
  else if (isBarr && barricades.size >= BARRICADE.max) reason = 'Barrikaden-Limit erreicht';
  else if (!isBarr && turrets.size >= MAX_TURRETS) reason = 'Turm-Limit erreicht';
  else if (!canShopLocal()) reason = 'Nur nahe am Shop während Wellen';
  placing.valid = !reason;
  const col = placing.valid ? '#4caf50' : '#f44336';
  placing.gm.color.set(col); placing.ring.material.color.set(col);
  placing.reason = reason;
}
function tryPlace() {
  if (!placing || placing.pending) return;
  if (!placing.valid) { ui.toast(placing.reason || 'Hier nicht möglich', 'bad'); sfx.deny(); return; }
  placing.pending = true;
  send({ t: 'place', type: placing.type, x: +placing.x.toFixed(2), z: +placing.z.toFixed(2), yaw: me.yaw + Math.PI, rot: placing.rot });
  setTimeout(() => { if (placing) placing.pending = false; }, 800);
}

// ------------------------------------------------------------------ Shop
function refreshShop() {
  if (!shopOpen) return;
  refreshShop.lastMoney = me.money; refreshShop.lastCan = canShopLocal();
  ui.renderShop({ money: me.money, owned: me.owned, weapon: me.weapon, upg: me.upg, perks: me.perks, nades: me.nades, barricades: barricades.size, canShop: canShopLocal(), warn: me.state !== 'alive' ? 'Du kannst gerade nichts kaufen.' : 'Während einer Welle kannst du nur am Shop-Terminal neben dem Generator einkaufen!' });
}
function openShop() {
  if (!inGame || me.state !== 'alive') return;
  shopOpen = true; ui.show('shop'); refreshShop();
  if (document.pointerLockElement) document.exitPointerLock();
  touch.show(false);
}
function closeShop(relock = true) {
  shopOpen = false; ui.show('shop', false);
  touch.show(true);
  if (relock) requestLock();
}
function onBought(m) {
  if (!m.ok) { ui.toast(m.reason || 'Nicht möglich', 'bad'); sfx.deny(); if (placing) placing.pending = false; return; }
  sfx.buy();
  if (WEAPONS[m.item]) { initAmmo(m.item); if (!me.owned.includes(m.item)) me.owned = WEAPON_ORDER.filter(id => id === m.item || me.owned.includes(id)); equip(m.item); ui.toast(`${WEAPONS[m.item].name} gekauft!`, 'good'); }
  else if (m.item === 'upgrade') { me.upg[m.w] = m.tier; ui.toast(`${WEAPONS[m.w].name} auf Stufe ${m.tier} aufgewertet!`, 'good'); if (m.w === me.weapon) buildViewmodel(); }
  else if (PERKS[m.item]) ui.toast(`${PERKS[m.item].icon} ${PERKS[m.item].name} aktiv!`, 'good');
  else if (m.item === 'crate') { crateSpinT = CRATE.spin; if (world && world.crate) world.crateSpin = CRATE.spin; }
  else if (TURRETS[m.item]) { cancelPlacing(); ui.toast(`${TURRETS[m.item].name} platziert!`, 'good'); }
  else if (m.item === 'barricade') { const again = placing && placing.type === 'barricade'; cancelPlacing(); ui.toast('Barrikade gebaut!', 'good'); if (again && me.money - BARRICADE.price >= BARRICADE.price) startPlacing('barricade'); }
  else if (m.item === 'ammo') { for (const id of me.owned) initAmmo(id); ui.toast('Munition aufgefüllt!', 'good'); }
  else if (m.item === 'nades') ui.toast('Granaten gekauft – werfen mit G', 'good');
  else ui.toast({ heal: 'Geheilt!', armor: 'Schutzweste angelegt!', repair: 'Generator repariert!', fixturrets: 'Türme repariert!', fixbarr: 'Barrikaden repariert!' }[m.item] || 'Gekauft!', 'good');
  refreshShop();
}

function onCrateResult(item) {
  crateSpinT = 0; ui.crateSpin(false);
  if (item.kind === 'weapon') {
    const name = weaponStats(item.id, item.tier).name;
    initAmmo(item.id);
    ui.announce('🎁 ' + name.toUpperCase(), item.tier ? 'Aufgewertet! Was für ein Glück.' : 'Aus der Glückskiste!', 3);
    setTimeout(() => equip(item.id), 100);
    sfx.waveClear();
  } else if (item.kind === 'money') { ui.announce(`🎁 +$${item.amount}`, 'Geld aus der Glückskiste', 2.5); sfx.buy(); }
  else { ui.announce('🎁 NIETE', 'Immerhin: volle Munition', 2.5); for (const id of me.owned) initAmmo(id); sfx.deny(); }
}
function nearCrate() { return mapDef && mapDef.crate && Math.hypot(me.pos.x - mapDef.crate.x, me.pos.z - mapDef.crate.z) < 3; }
function useCrate() {
  if (!nearCrate() || crateSpinT > 0 || me.state !== 'alive') return;
  send({ t: 'buy', item: 'crate' });
}

// ------------------------------------------------------------------ Chat
function openChat() { chatOpen = true; ui.openChat(true); }
function closeChat() { chatOpen = false; ui.openChat(false); }
$('chatinput').addEventListener('keydown', e => {
  e.stopPropagation();
  if (e.key === 'Enter') { const v = e.target.value.trim(); if (v) send({ t: 'chat', text: v }); closeChat(); }
  else if (e.key === 'Escape') closeChat();
});

// ------------------------------------------------------------------ Eingabe
function requestLock() {
  if (touch.enabled || !inGame) return;
  if (NOLOCK) { ui.show('pause', false); return; }
  if (!canvas.requestPointerLock) { enableNoLock(); return; }
  clearTimeout(lockTimer);
  lockTimer = setTimeout(() => { if (!locked) enableNoLock(); }, 700); // keine Sperre nach 0,7 s → Fallback
  const fallback = () => { try { const q = canvas.requestPointerLock(); if (q && q.catch) q.catch(enableNoLock); } catch { enableNoLock(); } };
  try { const p = canvas.requestPointerLock({ unadjustedMovement: true }); if (p && p.catch) p.catch(fallback); } catch { fallback(); }
}
document.addEventListener('pointerlockchange', () => {
  locked = document.pointerLockElement === canvas;
  if (locked) { clearTimeout(lockTimer); ui.show('pause', false); initAudio(); }
  else { mouseDown = false; rightDown = false; if (inGame && !shopOpen && !chatOpen) ui.show('pause'); }
});
document.addEventListener('pointerlockerror', () => enableNoLock());

const controlsActive = () => inGame && !shopOpen && !chatOpen && (locked || ((touch.enabled || NOLOCK) && !ui.isShown('pause')));

addEventListener('keydown', e => {
  if (e.target && e.target.tagName === 'INPUT') return;
  if (!inGame) return;
  if (e.code === 'Tab') { e.preventDefault(); keys.Tab = true; return; }
  if (e.code === 'KeyB') { shopOpen ? closeShop() : openShop(); return; }
  if (shopOpen) { if (e.code === 'Escape') closeShop(false); return; }
  if (NOLOCK && !touch.enabled && e.code === 'Escape') { if (ui.isShown('pause')) ui.show('pause', false); else ui.show('pause'); return; }
  if (!controlsActive()) return;
  keys[e.code] = true;
  if (e.code === 'Space') e.preventDefault();
  if (e.code === 'KeyT' || e.code === 'Enter') { e.preventDefault(); openChat(); return; }
  if (e.code === 'KeyR') startReload();
  if (e.code === 'KeyN') send({ t: 'ready' });
  if (e.code === 'KeyQ') cancelPlacing();
  if (e.code === 'KeyG' && !placing) throwGrenade();
  if (e.code === 'KeyV' && !placing) meleeAttack();
  if (e.code === 'KeyE' && !e.repeat && nearCrate() && ![...remotes.values()].some(r => r.state === 'down' && Math.hypot(r.x - me.pos.x, r.z - me.pos.z) < 2.6)) useCrate();
  if (e.code === 'KeyF' && flashlight) { flashOn = !flashOn; flashlight.visible = flashOn; }
  const dm = e.code.match(/^Digit([1-7])$/);
  if (dm) { const id = WEAPON_ORDER.find(k => WEAPONS[k].slot === +dm[1]); if (id) equip(id); }
});
addEventListener('keyup', e => { keys[e.code] = false; });
addEventListener('blur', () => { for (const k in keys) keys[k] = false; mouseDown = rightDown = false; });
canvas.addEventListener('mousedown', e => {
  if (!inGame) return;
  if (!locked && !NOLOCK) { if (!touch.enabled && !shopOpen) requestLock(); return; }
  if (NOLOCK && ui.isShown('pause')) return;
  if (NOLOCK) initAudio();
  if (e.button === 0) { mouseDown = true; mouseClicked = true; }
  if (e.button === 2) { rightDown = true; if (placing) cancelPlacing(); }
});
addEventListener('mouseup', e => { if (e.button === 0) mouseDown = false; if (e.button === 2) rightDown = false; });
addEventListener('mousemove', e => { if (locked || (NOLOCK && !touch.enabled && controlsActive())) { lookDX += e.movementX; lookDY += e.movementY; } });
addEventListener('wheel', e => { if ((locked || NOLOCK) && !placing) cycleWeapon(e.deltaY > 0 ? 1 : -1); }, { passive: true });
addEventListener('contextmenu', e => e.preventDefault());

// ------------------------------------------------------------------ Lokaler Spieler
const R = 0.38, H = 1.85, STEP = 0.5;
function collide(oldY) {
  const p = me.pos;
  for (const b of barrBoxes) {
    if (b.maxY <= p.y + STEP || b.minY >= p.y + H) continue;
    if (p.x < b.minX - R || p.x > b.maxX + R || p.z < b.minZ - R || p.z > b.maxZ + R) continue;
    const res = pushCircleOutOfBox(p.x, p.z, R, b);
    if (res) { p.x = res[0]; p.z = res[1]; }
  }
  for (const b of world.solids) {
    if (b.maxY <= p.y + STEP || b.minY >= p.y + H) continue;
    if (p.x < b.minX - R || p.x > b.maxX + R || p.z < b.minZ - R || p.z > b.maxZ + R) continue;
    const res = pushCircleOutOfBox(p.x, p.z, R, b);
    if (res) { p.x = res[0]; p.z = res[1]; }
  }
  for (const t of turrets.values()) {
    const dx = p.x - t.x, dz = p.z - t.z, rr = R + 0.55, d = Math.hypot(dx, dz);
    if (d < rr && d > 1e-4 && p.y < 1.4) { p.x = t.x + dx / d * rr; p.z = t.z + dz / d * rr; }
  }
  let gy = 0;
  for (const b of barrBoxes.length ? world.solids.concat(barrBoxes) : world.solids) {
    if (b.maxY > p.y + STEP) continue;
    const cx = clamp(p.x, b.minX, b.maxX), cz = clamp(p.z, b.minZ, b.maxZ);
    if ((p.x - cx) ** 2 + (p.z - cz) ** 2 < (R * 0.8) ** 2) gy = Math.max(gy, b.maxY);
  }
  if (p.y <= gy) {
    if (gy - oldY > 0.05 && me.onGround) me.stepOff -= (gy - p.y);
    p.y = gy; if (me.vel.y < 0) me.vel.y = 0; me.onGround = true;
  } else me.onGround = false;
  const half = mapDef.size / 2 - 0.5;
  p.x = clamp(p.x, -half, half); p.z = clamp(p.z, -half, half);
}

function updateLocal(dt) {
  const active = controlsActive();
  // Umsehen
  const [tdx, tdy] = touch.enabled ? touch.consumeLook() : [0, 0];
  const zoom = me.weapon === 'sniper' && me.aim > 0.9 ? 0.3 : 1 - me.aim * 0.3;
  frameLookDX = lookDX + tdx * 2.2;
  if (active) {
    me.yaw -= (lookDX * 0.0022 + tdx * 0.0048) * settings.sens * zoom;
    me.pitch -= (lookDY * 0.0022 + tdy * 0.0048) * settings.sens * zoom;
  }
  lookDX = lookDY = 0;
  me.pitch = clamp(me.pitch, -1.5, 1.5);
  // Rückstoß erholen
  if (me.recoilAcc > 0 && T - me.lastFire > 0.08) { const r = Math.min(me.recoilAcc, dt * (0.15 + me.recoilAcc * 5)); me.pitch -= r; me.recoilAcc -= r; }

  const alive = me.state === 'alive';
  let moveFrac = 0;
  if (alive) {
    let f = 0, s = 0;
    if (active) {
      f = (keys.KeyW || keys.ArrowUp ? 1 : 0) - (keys.KeyS || keys.ArrowDown ? 1 : 0) - touch.moveY;
      s = (keys.KeyD || keys.ArrowRight ? 1 : 0) - (keys.KeyA || keys.ArrowLeft ? 1 : 0) + touch.moveX;
    }
    const len = Math.hypot(f, s); if (len > 1) { f /= len; s /= len; }
    const sin = Math.sin(me.yaw), cos = Math.cos(me.yaw);
    const wx = -sin * f + cos * s, wz = -cos * f - sin * s;
    const sprinting = active && (keys.ShiftLeft || keys.ShiftRight || (touch.enabled && touch.moveY < -0.9)) && f > 0.3 && me.aim < 0.3 && me.reloadT <= 0;
    me.sprintAmt += ((sprinting ? 1 : 0) - me.sprintAmt) * Math.min(1, dt * 10);
    let speed = sprinting ? 8.4 : 5.4;
    if (me.perks.includes('sprint')) speed *= 1.15;
    speed *= 1 - me.aim * 0.4;
    if (me.weapon === 'minigun') speed *= 0.78;
    const acc = me.onGround ? 14 : 3.5;
    me.vel.x += (wx * speed - me.vel.x) * Math.min(1, acc * dt);
    me.vel.z += (wz * speed - me.vel.z) * Math.min(1, acc * dt);
    const jump = active && (keys.Space || touch.take('jump'));
    if (jump && me.onGround) { me.vel.y = 6.8; me.onGround = false; }
    me.vel.y -= 20 * dt;
    const oldY = me.pos.y;
    me.pos.addScaledVector(me.vel, dt);
    collide(oldY);
    const hs = Math.hypot(me.vel.x, me.vel.z);
    moveFrac = clamp(hs / 5.4, 0, 1.5);
    if (me.onGround && hs > 0.5) {
      me.bob += dt * hs * 1.7;
      me.stepT -= dt * hs;
      if (me.stepT <= 0) { me.stepT = 2.4; sfx.step(); }
    }
    updateWeapon(dt, active, sprinting);
  } else {
    me.vel.set(0, 0, 0); me.aim = 0; me.spin = 0; me.sprintAmt = 0;
  }
  me.stepOff *= Math.exp(-dt * 14);

  // Kamera
  if (me.state === 'dead') {
    const tgt = [...remotes.values()].find(r => r.state === 'alive');
    if (tgt) {
      const back = new V3(Math.sin(tgt.yaw), 0, Math.cos(tgt.yaw)).multiplyScalar(4.5);
      camera.position.set(tgt.x + back.x, tgt.y + 3, tgt.z + back.z);
      camera.lookAt(tgt.x, tgt.y + 1.5, tgt.z);
    } else {
      camera.position.set(mapDef.gen.x + Math.sin(T * 0.2) * 14, 9, mapDef.gen.z + Math.cos(T * 0.2) * 14);
      camera.lookAt(mapDef.gen.x, 1, mapDef.gen.z);
    }
    ui.downed('Du bist gefallen.<br>Du kehrst zurück, sobald die Welle geschafft ist.');
  } else {
    const eye = me.state === 'down' ? 0.45 : BODY.eye;
    camera.position.set(me.pos.x, me.pos.y + eye + me.stepOff, me.pos.z);
    if (alive && me.onGround && moveFrac > 0.1) camera.position.y += Math.sin(me.bob * 2) * 0.035 * moveFrac * (1 - me.aim);
    const sh = fx.shake;
    camera.rotation.set(me.pitch + (Math.random() - 0.5) * sh * 0.08, me.yaw + (Math.random() - 0.5) * sh * 0.08, me.state === 'down' ? 0.35 : 0);
    if (me.state === 'down') ui.downed(`${remotes.size ? `Ausbluten in <b>${Math.ceil(me.downT)}s</b> – warte auf einen Mitspieler!` : 'Selbstwiederbelebung läuft…'}${me.rev > 0 ? `<br>Wiederbelebung: ${Math.round(me.rev * 100)}%` : ''}`);
    else ui.downed(null);
  }
  // Sichtfeld
  const wantFov = settings.fov * (me.weapon === 'sniper' ? 1 - me.aim * 0.72 : 1 - me.aim * 0.2) * (1 + me.sprintAmt * 0.06);
  if (Math.abs(camera.fov - wantFov) > 0.01) { camera.fov = wantFov; camera.updateProjectionMatrix(); }
  const scoped = me.weapon === 'sniper' && me.aim > 0.9 && alive;
  ui.scope(scoped);
  vm.root.visible = alive && !scoped;
  updateViewmodel(dt, moveFrac);
  setListener(camera.position.x, camera.position.z);

  // Interaktionen
  let promptTxt = null, prog = null;
  if (alive && active !== false) {
    let near = null, nd = 2.6;
    for (const r of remotes.values()) if (r.state === 'down') { const d = Math.hypot(r.x - me.pos.x, r.z - me.pos.z); if (d < nd) { nd = d; near = r; } }
    if (near) {
      const holding = keys.KeyE || touch.use;
      promptTxt = `Halte <kbd>E</kbd> um <b>${esc(near.name)}</b> wiederzubeleben`;
      if (holding) { if (!updateLocal.revT || T - updateLocal.revT > 0.1) { send({ t: 'revive', id: near.id }); updateLocal.revT = T; } prog = near.rev; }
    } else if (Math.hypot(me.pos.x - mapDef.shop.x, me.pos.z - mapDef.shop.z) < 3) promptTxt = '<kbd>B</kbd> Shop öffnen';
    else if (nearCrate()) {
      if (crateSpinT > 0) promptTxt = '🎁 Die Kiste dreht sich …';
      else promptTxt = `<kbd>E</kbd> Glückskiste öffnen <b style="color:#69f0ae">$${CRATE.price}</b>${me.money < CRATE.price ? ' <span style="color:#ff5252">(zu wenig Geld)</span>' : ''}`;
      if (touch.enabled && touch.take('use') && crateSpinT <= 0) useCrate();
    }
  }
  // Glückskisten-Animation (Symbole rotieren)
  if (crateSpinT > 0) {
    crateSpinT -= dt;
    const icons = ['🔫', '💨', '💥', '🎖', '🎯', '💣', '⚙', '💰', '❌'];
    ui.crateSpin(true, icons[Math.floor(T * (crateSpinT < 0.8 ? 6 : 16)) % icons.length]);
    if (crateSpinT <= 0) ui.crateSpin(false);
  }
  me.meleeCd -= dt; if (me.meleeT > 0) me.meleeT -= dt; if (me.nadeT > 0) me.nadeT -= dt;
  ui.prompt(placing ? null : promptTxt); ui.progress(prog);

  // Touch-Aktionen
  if (touch.enabled) {
    if (touch.take('shop')) shopOpen ? closeShop() : openShop();
    if (touch.take('ready')) send({ t: 'ready' });
    if (touch.take('pause')) ui.show('pause');
    if (touch.take('cancel')) cancelPlacing();
    if (touch.take('reload')) startReload();
    if (touch.take('swap')) cycleWeapon(1);
    if (touch.take('nade')) throwGrenade();
    if (touch.take('melee')) meleeAttack();
    const slot = touch.takeSlot(); if (slot) equip(slot);
  }
}

let zucT = 0, zucHit = false;
function zombieUnderCrosshair(dt) {
  zucT -= dt; if (zucT > 0) return zucHit; zucT = 0.08;
  camera.updateMatrixWorld();
  const { f } = camBasis();
  ray.set(camera.position, f); ray.far = me.weapon === 'shotgun' ? 16 : 45;
  const it = ray.intersectObjects(world.rayTargets.concat(hitboxes), false)[0];
  zucHit = !!(it && it.object.userData.z && !it.object.userData.z.dying);
  return zucHit;
}
function updateWeapon(dt, active, sprinting) {
  const w = wstat(), a = me.ammo[me.weapon];
  me.fireCd -= dt;
  if (me.switchT > 0) me.switchT -= dt;
  if (me.reloadT > 0) { me.reloadT -= dt; if (me.reloadT <= 0) finishReload(); }
  // Auto-Feuer auf Touch-Geräten: schießt, sobald ein Zombie im Fadenkreuz ist
  const auto = touch.enabled && settings.autofire && active && !placing && me.reloadT <= 0 && a.mag > 0 && zombieUnderCrosshair(dt);
  const firing = active && (mouseDown || touch.fire || auto);
  const touchEdge = (touch.fire && !prevTouchFire) || (auto && me.fireCd <= 0); prevTouchFire = touch.fire;
  const clicked = mouseClicked || touchEdge; mouseClicked = false;
  const wantAim = active && (rightDown || touch.aim) && me.reloadT <= 0 && !sprinting && !placing;
  me.aim += ((wantAim ? 1 : 0) - me.aim) * Math.min(1, dt * 12);
  if (placing) { if (clicked) tryPlace(); return; }
  if (me.weapon === 'minigun') {
    const prev = me.spin;
    me.spin = firing && me.reloadT <= 0 && me.switchT <= 0 ? Math.min(1, me.spin + dt / w.spinup) : Math.max(0, me.spin - dt * 1.2);
    if (prev === 0 && me.spin > 0) sfx.spin();
  }
  const want = w.auto ? firing : clicked;
  if (want && me.fireCd <= 0 && me.switchT <= 0 && me.reloadT <= 0 && me.meleeT <= 0 && me.nadeT <= 0) {
    if (me.weapon === 'minigun' && me.spin < 1) { /* dreht noch hoch */ }
    else if (a.mag <= 0) { sfx.empty(); me.fireCd = 0.3; startReload(); }
    else shoot();
  }
  if (a.mag <= 0 && a.res > 0 && me.reloadT <= 0 && !firing && me.switchT <= 0) startReload();
}

// ------------------------------------------------------------------ HUD
let hudT = 0;
function updateHUD(dt) {
  const w = wstat();
  const spreadPx = currentSpread() * innerHeight / (2 * Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)));
  ui.crosshair(Math.min(80, spreadPx), me.state === 'alive' && me.aim < 0.5 && !shopOpen);
  hudT -= dt; if (hudT > 0) return; hudT = 0.1;
  const allP = [{ id: myId, name: myName(), color: myColor, state: me.state, hp: me.hp, maxHp: me.maxHp, money: me.money, kills: me.kills, ready: me.ready }, ...[...remotes.values()]];
  const alive = allP.filter(p => p.state === 'alive');
  let status;
  if (snap.phase === 'prep') status = `Nächste Welle in <b>${Math.ceil(snap.timer)}s</b> &nbsp;·&nbsp; <kbd>N</kbd> Bereit (${alive.filter(p => p.ready).length}/${alive.length})`;
  else status = `🧟 Zombies übrig: <b>${snap.left}</b>`;
  const boss = [...zombies.values()].find(z => z.type === 'boss' && !z.dying);
  const a = me.ammo[me.weapon] || { mag: 0, res: 0 };
  ui.updateHUD({
    wave: snap.wave, status, genHp: snap.genHp, hp: me.hp, maxHp: me.maxHp, armor: me.armor, money: me.money, state: me.state,
    weapon: me.weapon, wstat: w, upg: me.upg, perks: me.perks, nades: me.nades, mag: a.mag, reserve: a.res, reloading: me.reloadT > 0, owned: me.owned,
    boss: boss ? boss.hp / 100 : 0,
    powerups: [['instakill', snap.pu[0]], ['double', snap.pu[1]], ['freeze', snap.pu[2]]].filter(([, t]) => t > 0),
    team: allP.map(p => ({ name: p.name, color: p.color, state: p.state, hp: p.hp, maxHp: p.maxHp, money: p.money, ready: snap.phase === 'prep' && p.ready })),
  });
  ui.scoreboard(!!keys.Tab, allP.map(p => ({ ...p })));
  ui.drawMinimap({ x: me.pos.x, z: me.pos.z, yaw: me.yaw }, [...remotes.values()].map(r => ({ x: r.x, z: r.z, yaw: r.yaw, color: r.color, state: r.state })), [...zombies.values()].filter(z => !z.dying), [...turrets.values()], [...pickups.values()], [...barricades.values()]);
}

let sendT = 0;
function sendState(dt) {
  sendT -= dt; if (sendT > 0) return; sendT = 0.05;
  const moving = Math.hypot(me.vel.x, me.vel.z) > 0.6 ? 1 : 0;
  send({ t: 's', p: [+me.pos.x.toFixed(2), +me.pos.y.toFixed(2), +me.pos.z.toFixed(2)], yaw: +me.yaw.toFixed(3), pitch: +me.pitch.toFixed(3), w: me.weapon, mv: moving, f: T - me.lastFire < 0.15 ? 1 : 0, ml: me.meleeT > 0 ? 1 : 0 });
}

// ------------------------------------------------------------------ Hauptschleife
let last = performance.now();
function frame(now) {
  requestAnimationFrame(frame);
  const dt = Math.min(0.05, (now - last) / 1000); last = now; T += dt;
  if (world && (inGame || gameOver)) {
    if (inGame) { updateLocal(dt); updatePlacing(); sendState(dt); updateHUD(dt); }
    else { // Game Over: langsame Kamerafahrt
      camera.position.set(mapDef.gen.x + Math.sin(T * 0.15) * 22, 12, mapDef.gen.z + Math.cos(T * 0.15) * 22);
      camera.lookAt(mapDef.gen.x, 1, mapDef.gen.z); vm.root.visible = false;
    }
    updateRemotes(dt); updateZombies(dt); updateTurrets(dt); updatePickups(dt); updateBarricades(dt);
    world.update(dt); fx.update(dt);
    renderer.clear();
    renderer.render(scene, camera);
    if (inGame && vm.root.visible) { renderer.clearDepth(); renderer.render(vmScene, vmCam); }
  } else {
    renderer.clear();
  }
}
requestAnimationFrame(frame);

// ------------------------------------------------------------------ UI-Verknüpfung
ui.init({
  onColor(c) { settings.color = c; myColor = c; saveSettings(); send({ t: 'hello', name: myName(), color: c }); },
  onName(n) { settings.name = n.slice(0, 16); saveSettings(); send({ t: 'hello', name: myName(), color: myColor }); },
  onMap(id) { send({ t: 'map', map: id }); },
  onDiff(id) { send({ t: 'diff', diff: id }); },
  onUpgrade(w) { send({ t: 'buy', item: 'upgrade', w }); },
  onStart() {
    initAudio();
    settings.name = $('name').value.slice(0, 16); saveSettings();
    send({ t: 'hello', name: myName(), color: myColor });
    if (lobby && lobby.phase === 'game') send({ t: 'join' }); else send({ t: 'start' });
    if (!touch.enabled) { inGame = true; requestLock(); inGame = false; }
    if (touch.enabled && document.documentElement.requestFullscreen) document.documentElement.requestFullscreen().catch(() => {});
  },
  onResume() { ui.show('pause', false); if (touch.enabled) touch.show(true); else requestLock(); initAudio(); },
  onLeave() { send({ t: 'leave' }); cleanupGame(); gameOver = false; ui.show('menu'); },
  onBackToLobby() { cleanupGame(); gameOver = false; ui.show('over', false); ui.show('menu'); },
  onCloseShop() { closeShop(); },
  onBuy(item) { send({ t: 'buy', item }); },
  onEquip(id) { equip(id); refreshShop(); },
  onTurret(type) { closeShop(); startPlacing(type); },
  onSettings(s) {
    Object.assign(settings, s); saveSettings(); setVolume(s.vol);
    camera.fov = settings.fov; camera.updateProjectionMatrix();
    renderer.setPixelRatio(pixelRatio());
  },
});
ui.writeSettings(settings);
connect();

// Für Tests/Debugging
Object.assign(dbg, { createZombie, createPickup, handleEvent, ui });
window.__za = { me, zombies, remotes, turrets, pickups, snap: () => snap, scene, camera, get placing() { return placing; }, throwGrenade, meleeAttack, useCrate, dbg };
