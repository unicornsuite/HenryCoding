// Klötzchen-Modelle: Figuren, Zombies, Waffen, Türme, Generator, Shop
import * as THREE from 'three';
import { BODY } from '/shared/config.js';

const geoCache = new Map();
export function boxGeo(w, h, d) {
  const k = `${w}|${h}|${d}`;
  if (!geoCache.has(k)) geoCache.set(k, new THREE.BoxGeometry(w, h, d));
  return geoCache.get(k);
}
const cylCache = new Map();
export function cylGeo(rt, rb, h, seg = 12) {
  const k = `${rt}|${rb}|${h}|${seg}`;
  if (!cylCache.has(k)) cylCache.set(k, new THREE.CylinderGeometry(rt, rb, h, seg));
  return cylCache.get(k);
}
const matCache = new Map();
export function mat(color, o = {}) {
  const k = color + JSON.stringify(o);
  if (!matCache.has(k)) matCache.set(k, new THREE.MeshStandardMaterial({ color, roughness: 0.62, metalness: 0.05, ...o }));
  return matCache.get(k);
}

function mesh(geo, material, x = 0, y = 0, z = 0, shadow = true) {
  const m = new THREE.Mesh(geo, material);
  m.position.set(x, y, z);
  m.castShadow = shadow; m.receiveShadow = shadow;
  return m;
}

// ------------------------------------------------------------------ Gesichter
const faceCache = new Map();
function faceTexture(kind, skin, eyes) {
  const k = kind + skin + eyes;
  if (faceCache.has(k)) return faceCache.get(k);
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d');
  g.fillStyle = skin; g.fillRect(0, 0, 64, 64);
  if (kind === 'player') {
    g.fillStyle = '#1a1a1a';
    g.beginPath(); g.ellipse(22, 26, 4, 6, 0, 0, Math.PI * 2); g.fill();
    g.beginPath(); g.ellipse(42, 26, 4, 6, 0, 0, Math.PI * 2); g.fill();
    g.lineWidth = 3.5; g.strokeStyle = '#1a1a1a'; g.lineCap = 'round';
    g.beginPath(); g.arc(32, 36, 11, 0.2 * Math.PI, 0.8 * Math.PI); g.stroke();
  } else {
    // Zombie: leuchtende Augen, zackiger Mund, Wunden
    g.fillStyle = 'rgba(0,0,0,0.25)'; g.fillRect(14, 18, 16, 14); g.fillRect(34, 18, 16, 14);
    g.fillStyle = eyes; g.fillRect(17, 22, 10, 7); g.fillRect(37, 22, 10, 7);
    g.fillStyle = '#ffffff'; g.fillRect(19, 23, 3, 3); g.fillRect(39, 23, 3, 3);
    g.fillStyle = '#2b0f0f'; g.fillRect(18, 40, 28, 10);
    g.fillStyle = '#f5f0d0';
    for (let i = 0; i < 5; i++) { g.fillRect(19 + i * 5.5, 40, 3, 4); g.fillRect(21 + i * 5.5, 46, 3, 4); }
    g.fillStyle = 'rgba(120,0,0,0.6)'; g.fillRect(48, 8, 6, 12); g.fillRect(8, 44, 5, 8);
  }
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace; t.magFilter = THREE.NearestFilter;
  faceCache.set(k, t);
  return t;
}

// ------------------------------------------------------------------ Figur
/**
 * Baut eine Roblox-artige Figur. Vorderseite zeigt nach +Z.
 * o: { skin, shirt, pants, kind:'player'|'zombie', eyes, scale, wide, extras }
 */
export function buildCharacter(o) {
  const s = o.scale || 1, wide = o.wide || 1;
  const B = BODY;
  const root = new THREE.Group();
  const body = new THREE.Group(); body.scale.setScalar(s); root.add(body);

  const own = o.unique !== false; // eigene Materialien (für Treffer-Blinken)
  const M = c => own ? new THREE.MeshStandardMaterial({ color: c, roughness: 0.6, metalness: 0.05 }) : mat(c);
  const skin = M(o.skin), shirt = M(o.shirt), pants = M(o.pants), shoe = mat('#2b2b2b');
  const mats = [skin, shirt, pants];

  const torsoW = B.torsoW * wide;
  // Beine
  const legs = [];
  for (const side of [-1, 1]) {
    const pivot = new THREE.Group(); pivot.position.set(side * B.legW * 0.55 * wide, B.legH, 0);
    pivot.add(mesh(boxGeo(B.legW, B.legH * 0.82, B.legW * 1.05), pants, 0, -B.legH * 0.41, 0));
    pivot.add(mesh(boxGeo(B.legW * 1.02, B.legH * 0.18, B.legW * 1.15), shoe, 0, -B.legH * 0.91, 0.02));
    body.add(pivot); legs.push(pivot);
  }
  // Rumpf
  const torso = new THREE.Group(); torso.position.y = B.legH; body.add(torso);
  torso.add(mesh(boxGeo(torsoW, B.torsoH, B.torsoD), shirt, 0, B.torsoH / 2, 0));
  if (o.kind === 'player') {
    // Gürtel
    torso.add(mesh(boxGeo(torsoW * 1.01, 0.08, B.torsoD * 1.02), mat('#3e2723'), 0, 0.05, 0));
  }
  // Arme
  const arms = [];
  for (const side of [-1, 1]) {
    const pivot = new THREE.Group(); pivot.position.set(side * (torsoW / 2 + B.armW / 2), B.torsoH - 0.1, 0);
    pivot.add(mesh(boxGeo(B.armW, B.armH * 0.45, B.armW), shirt, 0, -B.armH * 0.225 + 0.1, 0));
    pivot.add(mesh(boxGeo(B.armW * 0.98, B.armH * 0.55, B.armW * 0.98), skin, 0, -B.armH * 0.725 + 0.1, 0));
    torso.add(pivot); arms.push(pivot);
  }
  // Kopf
  const head = new THREE.Group(); head.position.y = B.torsoH; torso.add(head);
  const faceMat = new THREE.MeshStandardMaterial({ map: faceTexture(o.kind || 'player', o.skin, o.eyes || '#000'), roughness: 0.6 });
  const headMats = [skin, skin, skin, skin, faceMat, skin];
  const headMesh = mesh(boxGeo(B.headS * 1.1, B.headS, B.headS), headMats, 0, B.headS / 2 + 0.02, 0);
  head.add(headMesh);
  if (o.kind === 'player' && o.hat) {
    head.add(mesh(boxGeo(B.headS * 1.16, 0.14, B.headS * 1.06), mat(o.hat), 0, B.headS + 0.05, 0));
    head.add(mesh(boxGeo(B.headS * 1.0, 0.04, 0.22), mat(o.hat), 0, B.headS - 0.0, 0.32));
  }

  // Extras für Zombies
  if (o.kind === 'zombie') {
    // zerrissenes Hemd: Haut-Flecken
    torso.add(mesh(boxGeo(0.22, 0.2, 0.02), skin, -0.18 * wide, 0.25, B.torsoD / 2 + 0.005, false));
    torso.add(mesh(boxGeo(0.16, 0.14, 0.02), mat('#7a1010'), 0.2 * wide, 0.55, B.torsoD / 2 + 0.006, false));
    if (o.extras === 'exploder') {
      const glow = new THREE.MeshStandardMaterial({ color: '#ffab00', emissive: '#ff6d00', emissiveIntensity: 1.5, roughness: 0.4 });
      mats.push(glow);
      for (const [x, y, z, sz] of [[0, 0.4, 0.23, 0.32], [-0.25, 0.6, 0.2, 0.16], [0.28, 0.2, 0.2, 0.18], [0.1, 0.65, -0.22, 0.2]]) torso.add(mesh(boxGeo(sz, sz, sz * 0.6), glow, x, y, z, false));
      root.userData.glow = glow;
    }
    if (o.extras === 'boss' || o.extras === 'brute') {
      const pad = mat(o.extras === 'boss' ? '#4a148c' : '#5d4037');
      for (const side of [-1, 1]) arms[side < 0 ? 0 : 1].add(mesh(boxGeo(B.armW * 1.3, 0.2, B.armW * 1.3), pad, 0, 0.08, 0));
    }
    if (o.extras === 'spitter') {
      const sac = new THREE.MeshStandardMaterial({ color: '#aeea00', emissive: '#64dd17', emissiveIntensity: 0.9, roughness: 0.35 });
      mats.push(sac);
      torso.add(mesh(boxGeo(0.5, 0.42, 0.3), sac, 0, 0.3, B.torsoD / 2 + 0.1, false));
      head.add(mesh(boxGeo(0.3, 0.12, 0.1), sac, 0, 0.12, B.headS / 2 + 0.04, false));
      root.userData.glow = sac;
    }
    if (o.extras === 'crawler') {
      // fehlender Unterkörper-Look: zerfetzte Hose
      torso.add(mesh(boxGeo(torsoW * 1.02, 0.1, B.torsoD * 1.02), mat('#3e2723'), 0, 0.02, 0, false));
    }
    if (o.extras === 'boss') {
      const horn = mat('#e0e0e0');
      for (const side of [-1, 1]) { const h = mesh(boxGeo(0.08, 0.3, 0.08), horn, side * 0.2, B.headS + 0.15, 0); h.rotation.z = -side * 0.4; head.add(h); }
    }
  }

  root.userData.char = { body, torso, head, headMesh, arms, legs, mats, scale: s };
  return root;
}

/** Animiert eine Figur. st: { t, walk (0..1), phase, attack (0..1), kind, aimPitch, downed } */
export function animateCharacter(root, st) {
  const c = root.userData.char;
  const sw = Math.sin(st.phase) * 0.85 * st.walk;
  c.legs[0].rotation.x = sw; c.legs[1].rotation.x = -sw;
  c.torso.position.y = BODY.legH + Math.abs(Math.cos(st.phase)) * 0.04 * st.walk;
  if (st.kind === 'zombie' && st.crawl) {
    // Kriecher: fast waagerecht, zieht sich mit den Armen vorwärts
    const ph = st.phase;
    c.torso.rotation.x = 1.25;
    c.torso.position.y = BODY.legH * 0.35 + Math.abs(Math.sin(ph)) * 0.04 * st.walk;
    c.legs[0].rotation.x = 1.1 + Math.sin(ph) * 0.3 * st.walk; c.legs[1].rotation.x = 1.1 - Math.sin(ph) * 0.3 * st.walk;
    c.legs[0].position.y = c.legs[1].position.y = BODY.legH * 0.35;
    const reach = st.attack > 0 ? -2.6 : -2.2;
    c.arms[0].rotation.x = reach + Math.sin(ph) * 0.6 * st.walk; c.arms[1].rotation.x = reach - Math.sin(ph) * 0.6 * st.walk;
    c.arms[0].rotation.z = -0.25; c.arms[1].rotation.z = 0.25;
    c.head.rotation.x = -1.1; c.head.rotation.z = Math.sin(st.t * 3 + st.seed) * 0.15;
    return;
  }
  if (st.kind === 'zombie') {
    const sway = Math.sin(st.t * 2.2 + st.seed) * 0.12;
    let ax = -Math.PI / 2 + 0.15 + sway;
    if (st.attack > 0) ax = -Math.PI / 2 - 0.9 * Math.sin(st.attack * Math.PI) + 0.9 * (1 - st.attack);
    c.arms[0].rotation.x = ax; c.arms[1].rotation.x = ax + (st.attack > 0 ? 0 : -sway * 2);
    c.arms[0].rotation.z = -0.05; c.arms[1].rotation.z = 0.05;
    c.torso.rotation.x = 0.18 + (st.attack > 0 ? Math.sin(st.attack * Math.PI) * 0.25 : 0);
    c.head.rotation.z = Math.sin(st.t * 1.3 + st.seed) * 0.18;
    c.head.rotation.x = -0.1;
  } else {
    // Spieler hält eine Waffe
    const p = st.aimPitch || 0;
    c.arms[1].rotation.x = -Math.PI / 2 + p;
    c.arms[0].rotation.x = -Math.PI / 2 + p + 0.1;
    c.arms[0].rotation.y = 0; c.arms[1].rotation.y = 0;
    c.arms[0].rotation.z = 0.5; c.arms[1].rotation.z = 0;
    c.head.rotation.x = -p * 0.6;
    c.torso.rotation.x = 0;
  }
}

// ------------------------------------------------------------------ Waffen (zeigen nach -Z, Ursprung = Griff)
const TIER_COLORS = ['#ffffff', '#40c4ff', '#d500f9', '#ffd600'];
export function buildGun(type, tier = 0) {
  const g = new THREE.Group();
  const black = mat('#222428', { metalness: 0.4, roughness: 0.45 }), grey = mat('#5f646b', { metalness: 0.5, roughness: 0.4 });
  const wood = mat('#8d5a2b'), olive = mat('#4b5320'), green = mat('#2e7d32');
  const add = (geo, m, x, y, z, rx = 0) => { const o = mesh(geo, m, x, y, z, false); o.rotation.x = rx; g.add(o); return o; };
  const barrel = (r, len, z, y = 0.04, m = black) => add(cylGeo(r, r, len, 10), m, 0, y, z, Math.PI / 2);
  let muzzleZ = -0.3, muzzleY = 0.04, spin = null;
  switch (type) {
    case 'pistol':
      add(boxGeo(0.07, 0.08, 0.3), grey, 0, 0.04, -0.1);
      add(boxGeo(0.065, 0.16, 0.08), black, 0, -0.06, 0.02, 0.2);
      add(boxGeo(0.02, 0.03, 0.02), black, 0, 0.09, -0.22);
      muzzleZ = -0.26; break;
    case 'smg':
      add(boxGeo(0.08, 0.11, 0.42), black, 0, 0.03, -0.12);
      add(boxGeo(0.05, 0.22, 0.06), grey, 0, -0.12, -0.14);
      add(boxGeo(0.06, 0.14, 0.07), black, 0, -0.06, 0.06, 0.2);
      barrel(0.02, 0.14, -0.39);
      add(boxGeo(0.04, 0.06, 0.22), grey, 0, 0.02, 0.18);
      muzzleZ = -0.47; break;
    case 'shotgun':
      barrel(0.028, 0.72, -0.38, 0.06);
      add(boxGeo(0.075, 0.065, 0.22), wood, 0, 0.0, -0.36);
      add(boxGeo(0.085, 0.11, 0.26), black, 0, 0.04, -0.02);
      add(boxGeo(0.065, 0.11, 0.34), wood, 0, -0.02, 0.26, 0.12);
      muzzleZ = -0.75; muzzleY = 0.06; break;
    case 'rifle':
      add(boxGeo(0.08, 0.12, 0.46), olive, 0, 0.03, -0.1);
      add(boxGeo(0.095, 0.095, 0.26), black, 0, 0.03, -0.42);
      barrel(0.018, 0.24, -0.62, 0.04);
      add(boxGeo(0.06, 0.2, 0.08), black, 0, -0.12, -0.14, 0.25);
      add(boxGeo(0.06, 0.13, 0.08), black, 0, -0.07, 0.07, 0.2);
      add(boxGeo(0.07, 0.12, 0.26), olive, 0, 0.0, 0.24);
      add(boxGeo(0.03, 0.05, 0.1), black, 0, 0.12, -0.08);
      muzzleZ = -0.75; break;
    case 'sniper':
      add(boxGeo(0.08, 0.1, 0.5), black, 0, 0.03, -0.1);
      barrel(0.022, 0.75, -0.7, 0.04);
      add(cylGeo(0.04, 0.04, 0.34, 12), black, 0, 0.13, -0.1, Math.PI / 2);
      add(cylGeo(0.05, 0.05, 0.05, 12), black, 0, 0.13, -0.28, Math.PI / 2);
      add(boxGeo(0.07, 0.15, 0.34), wood, 0, -0.03, 0.3, 0.1);
      add(boxGeo(0.06, 0.13, 0.07), black, 0, -0.07, 0.04, 0.2);
      muzzleZ = -1.08; break;
    case 'launcher':
      add(cylGeo(0.075, 0.075, 0.62, 14), green, 0, 0.06, -0.22, Math.PI / 2);
      add(cylGeo(0.09, 0.09, 0.06, 14), black, 0, 0.06, -0.53, Math.PI / 2);
      add(boxGeo(0.06, 0.15, 0.07), black, 0, -0.06, 0.02, 0.2);
      add(cylGeo(0.11, 0.11, 0.12, 8), grey, 0, -0.04, -0.12);
      add(boxGeo(0.06, 0.1, 0.26), wood, 0, 0.0, 0.22);
      muzzleZ = -0.56; muzzleY = 0.06; break;
    case 'minigun': {
      add(boxGeo(0.17, 0.17, 0.36), grey, 0, 0.0, 0.02);
      add(boxGeo(0.05, 0.12, 0.2), black, 0, 0.14, 0.0);
      spin = new THREE.Group(); spin.position.set(0, 0.0, -0.2); g.add(spin);
      for (let i = 0; i < 6; i++) {
        const a = (i / 6) * Math.PI * 2;
        const b = mesh(cylGeo(0.017, 0.017, 0.58, 8), black, Math.cos(a) * 0.05, Math.sin(a) * 0.05, -0.29, false);
        b.rotation.x = Math.PI / 2; spin.add(b);
      }
      const ring = mesh(cylGeo(0.085, 0.085, 0.04, 14), grey, 0, 0, -0.5, false); ring.rotation.x = Math.PI / 2; spin.add(ring);
      add(boxGeo(0.1, 0.16, 0.14), mat('#795548'), 0.13, -0.05, 0.06);
      muzzleZ = -0.82; muzzleY = 0.0; break;
    }
  }
  const muzzle = new THREE.Object3D(); muzzle.position.set(0, muzzleY, muzzleZ); g.add(muzzle);
  if (tier > 0) {
    // Aufwertungs-Leuchtstreifen entlang des Laufs
    const glow = new THREE.MeshStandardMaterial({ color: TIER_COLORS[tier], emissive: TIER_COLORS[tier], emissiveIntensity: 1.6, roughness: 0.3 });
    const len = Math.max(0.12, -muzzleZ * 0.55);
    for (const side of [-1, 1]) { const b = mesh(boxGeo(0.012, 0.03, len), glow, side * 0.045, muzzleY + 0.02, muzzleZ * 0.45, false); g.add(b); }
    for (let i = 0; i < tier; i++) { const r = mesh(cylGeo(0.035, 0.035, 0.015, 10), glow, 0, muzzleY, muzzleZ + 0.06 + i * 0.05, false); r.rotation.x = Math.PI / 2; g.add(r); }
    g.userData.glow = glow;
  }
  g.userData.muzzle = muzzle; g.userData.spin = spin;
  return g;
}

// ------------------------------------------------------------------ Barrikade (entlang X, 2.6 m breit)
export function buildBarricade() {
  const root = new THREE.Group();
  const wood = mat('#8d6e63'), dark = mat('#5d4037'), metal = mat('#9e9e9e', { metalness: 0.6, roughness: 0.4 });
  for (const x of [-1.05, 1.05]) { const p = mesh(boxGeo(0.18, 1.2, 0.18), dark, x, 0.6, 0); root.add(p); }
  for (const [y, rz] of [[0.3, 0.03], [0.65, -0.02], [1.0, 0.04]]) { const pl = mesh(boxGeo(2.6, 0.22, 0.08), wood, 0, y, 0.1); pl.rotation.z = rz; root.add(pl); }
  // Stützen hinten
  for (const x of [-0.8, 0.8]) { const st = mesh(boxGeo(0.12, 1.3, 0.12), dark, x, 0.55, -0.35); st.rotation.x = -0.5; root.add(st); }
  // Stacheldraht oben
  for (let i = 0; i < 5; i++) { const w = mesh(boxGeo(0.06, 0.06, 0.3), metal, -1 + i * 0.5, 1.22, 0, false); w.rotation.y = 0.6; root.add(w); }
  root.add(mesh(boxGeo(2.4, 0.03, 0.03), metal, 0, 1.25, 0, false));
  return root;
}

// ------------------------------------------------------------------ Glückskiste
export function buildCrate() {
  const root = new THREE.Group();
  const wood = mat('#6d4c41'), gold = mat('#ffca28', { metalness: 0.6, roughness: 0.3 }), dark = mat('#3e2723');
  root.add(mesh(boxGeo(1.3, 0.7, 0.9), wood, 0, 0.35, 0));
  for (const x of [-0.5, 0, 0.5]) root.add(mesh(boxGeo(0.08, 0.72, 0.92), gold, x, 0.36, 0, false));
  const lid = new THREE.Group(); lid.position.set(0, 0.7, -0.45); root.add(lid);
  lid.add(mesh(boxGeo(1.32, 0.2, 0.92), dark, 0, 0.1, 0.45));
  for (const x of [-0.5, 0, 0.5]) lid.add(mesh(boxGeo(0.08, 0.22, 0.94), gold, x, 0.1, 0.45, false));
  const glowMat = new THREE.MeshStandardMaterial({ color: '#fff59d', emissive: '#ffd600', emissiveIntensity: 1.2, transparent: true, opacity: 0.0, depthWrite: false });
  const glow = mesh(boxGeo(1.1, 0.6, 0.7), glowMat, 0, 0.9, 0, false); root.add(glow);
  const sign = new THREE.MeshStandardMaterial({ map: textCanvasTexture([{ text: '?', size: 90, color: '#ffd600' }], 128, 128, '#1a237e'), emissive: '#ffffff', emissiveIntensity: 0.6 });
  sign.emissiveMap = sign.map;
  const q = mesh(boxGeo(0.5, 0.5, 0.06), [mat('#1a237e'), mat('#1a237e'), mat('#1a237e'), mat('#1a237e'), sign, sign], 0, 1.7, 0, false); root.add(q);
  root.userData = { lid, glowMat, q };
  return root;
}

// ------------------------------------------------------------------ Power-up am Boden
export function buildPickup(cfg) {
  const root = new THREE.Group();
  const m = new THREE.MeshStandardMaterial({ color: cfg.color, emissive: cfg.color, emissiveIntensity: 1.4, roughness: 0.3 });
  const core = mesh(new THREE.OctahedronGeometry(0.32, 0), m, 0, 1.0, 0, false); root.add(core);
  const ring = mesh(new THREE.TorusGeometry(0.5, 0.04, 6, 20), m, 0, 1.0, 0, false); root.add(ring);
  const label = makeLabel(`${cfg.icon} ${cfg.name}`, cfg.color, 'rgba(0,0,0,0.6)'); label.position.y = 1.8; label.scale.set(2.2, 0.55, 1); root.add(label);
  const beam = mesh(cylGeo(0.12, 0.25, 6, 8), new THREE.MeshBasicMaterial({ color: cfg.color, transparent: true, opacity: 0.18, depthWrite: false }), 0, 3, 0, false); root.add(beam);
  root.userData = { core, ring, label, mat: m };
  return root;
}

// ------------------------------------------------------------------ Türme (zeigen nach +Z)
export function buildTurret(type, color) {
  const root = new THREE.Group();
  const dark = mat('#37474f', { metalness: 0.4 }), body = mat(color, { metalness: 0.3 }), black = mat('#1c1c1c', { metalness: 0.5 });
  root.add(mesh(boxGeo(1.1, 0.25, 1.1), dark, 0, 0.125, 0));
  root.add(mesh(boxGeo(0.9, 0.12, 0.9), mat('#fbc02d'), 0, 0.31, 0));
  root.add(mesh(cylGeo(0.14, 0.18, 0.6, 10), dark, 0, 0.65, 0));
  const head = new THREE.Group(); head.position.y = 1.0; root.add(head);
  const muzzles = [];
  const addMuzzle = (x, y, z) => { const m = new THREE.Object3D(); m.position.set(x, y, z); head.add(m); muzzles.push(m); };
  if (type === 'mg') {
    head.add(mesh(boxGeo(0.55, 0.36, 0.62), body, 0, 0.05, 0));
    head.add(mesh(boxGeo(0.3, 0.25, 0.3), mat('#8d6e63'), 0.38, 0, -0.05));
    for (const x of [-0.12, 0.12]) { const b = mesh(cylGeo(0.045, 0.045, 0.75, 8), black, x, 0.08, 0.6); b.rotation.x = Math.PI / 2; head.add(b); addMuzzle(x, 0.08, 0.98); }
    head.add(mesh(boxGeo(0.12, 0.1, 0.12), mat('#e53935', { emissive: '#e53935', emissiveIntensity: 0.8 }), 0, 0.28, 0.2));
  } else if (type === 'cannon') {
    head.add(mesh(boxGeo(0.8, 0.6, 0.8), body, 0, 0.12, -0.05));
    const b = mesh(cylGeo(0.14, 0.17, 1.0, 12), black, 0, 0.15, 0.7); b.rotation.x = Math.PI / 2; head.add(b);
    const r = mesh(cylGeo(0.2, 0.2, 0.12, 12), dark, 0, 0.15, 1.18); r.rotation.x = Math.PI / 2; head.add(r);
    addMuzzle(0, 0.15, 1.25);
  } else if (type === 'tesla') {
    head.add(mesh(cylGeo(0.22, 0.3, 0.8, 10), body, 0, 0.3, 0));
    const ringMat = mat('#ffb300', { metalness: 0.8, roughness: 0.3 });
    for (let i = 0; i < 4; i++) { const t = mesh(new THREE.TorusGeometry(0.3 - i * 0.03, 0.04, 6, 16), ringMat, 0, 0.0 + i * 0.22, 0); t.rotation.x = Math.PI / 2; head.add(t); }
    const orbMat = new THREE.MeshStandardMaterial({ color: '#b3e5fc', emissive: '#40c4ff', emissiveIntensity: 2.2 });
    const orb = mesh(new THREE.IcosahedronGeometry(0.28, 1), orbMat, 0, 1.0, 0, false); head.add(orb);
    root.userData.orb = orb;
    addMuzzle(0, 1.0, 0);
  }
  root.userData.head = head; root.userData.muzzles = muzzles;
  root.traverse(o => { if (o.isMesh) { o.castShadow = true; } });
  return root;
}

// ------------------------------------------------------------------ Generator
function stripeTexture() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d');
  g.fillStyle = '#ffca28'; g.fillRect(0, 0, 64, 64);
  g.fillStyle = '#212121';
  for (let i = -64; i < 128; i += 22) { g.beginPath(); g.moveTo(i, 0); g.lineTo(i + 11, 0); g.lineTo(i + 11 - 64, 64); g.lineTo(i - 64, 64); g.fill(); }
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping; t.magFilter = THREE.NearestFilter;
  return t;
}
export function buildGenerator() {
  const root = new THREE.Group();
  const stripes = new THREE.MeshStandardMaterial({ map: stripeTexture(), roughness: 0.6 });
  const grey = mat('#607d8b', { metalness: 0.5 }), dark = mat('#263238', { metalness: 0.5 });
  root.add(mesh(boxGeo(2.6, 0.5, 2.6), stripes, 0, 0.25, 0));
  root.add(mesh(boxGeo(2.3, 1.3, 2.3), grey, 0, 1.15, 0));
  const coreMat = new THREE.MeshStandardMaterial({ color: '#80deea', emissive: '#00e5ff', emissiveIntensity: 2, roughness: 0.3 });
  for (const [x, z, ry] of [[0, 1.16, 0], [0, -1.16, 0], [1.16, 0, Math.PI / 2], [-1.16, 0, Math.PI / 2]]) {
    const w = mesh(boxGeo(1.2, 0.7, 0.04), coreMat, x, 1.15, z, false); w.rotation.y = ry; root.add(w);
  }
  root.add(mesh(boxGeo(2.4, 0.15, 2.4), dark, 0, 1.87, 0));
  const fanHousing = mesh(cylGeo(0.7, 0.7, 0.3, 16), dark, 0, 2.05, 0); root.add(fanHousing);
  const fan = new THREE.Group(); fan.position.y = 2.22; root.add(fan);
  for (let i = 0; i < 4; i++) { const b = mesh(boxGeo(1.1, 0.04, 0.22), mat('#b0bec5', { metalness: 0.6 }), 0, 0, 0, false); b.rotation.y = i * Math.PI / 4; fan.add(b); }
  const lamps = [];
  for (const [x, z] of [[1.1, 1.1], [-1.1, 1.1], [1.1, -1.1], [-1.1, -1.1]]) {
    const lm = new THREE.MeshStandardMaterial({ color: '#ff5252', emissive: '#ff1744', emissiveIntensity: 1 });
    root.add(mesh(boxGeo(0.18, 0.18, 0.18), lm, x, 2.03, z, false)); lamps.push(lm);
  }
  // Kabel
  for (const [x, z, w, d] of [[0, 2.2, 0.25, 2.2], [2.2, 0, 2.2, 0.25]]) root.add(mesh(boxGeo(w, 0.12, d), mat('#212121'), x * 0.75, 0.06, z * 0.75));
  root.userData = { fan, coreMat, lamps };
  return root;
}

function textCanvasTexture(lines, w = 256, h = 128, bg = '#0d47a1', fg = '#ffffff') {
  const c = document.createElement('canvas'); c.width = w; c.height = h;
  const g = c.getContext('2d');
  g.fillStyle = bg; g.fillRect(0, 0, w, h);
  g.fillStyle = fg; g.textAlign = 'center'; g.textBaseline = 'middle';
  lines.forEach((l, i) => { g.font = `bold ${l.size}px system-ui, sans-serif`; g.fillStyle = l.color || fg; g.fillText(l.text, w / 2, h * (i + 1) / (lines.length + 1)); });
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  return t;
}
export function buildShop() {
  const root = new THREE.Group();
  root.add(mesh(boxGeo(1.4, 1.7, 0.9), mat('#1a237e', { metalness: 0.3 }), 0, 0.85, 0));
  const screen = new THREE.MeshStandardMaterial({ map: textCanvasTexture([{ text: 'SHOP', size: 54, color: '#ffeb3b' }, { text: 'Taste [B]', size: 30 }]), emissive: '#ffffff', emissiveMap: null, emissiveIntensity: 0.0 });
  screen.emissiveMap = screen.map; screen.emissiveIntensity = 0.9;
  for (const z of [0.452, -0.452]) { const s = mesh(new THREE.PlaneGeometry(1.1, 0.6), screen, 0, 1.25, z, false); if (z < 0) s.rotation.y = Math.PI; root.add(s); }
  root.add(mesh(boxGeo(1.5, 0.12, 1.0), mat('#ffca28'), 0, 1.76, 0));
  const sign = new THREE.MeshStandardMaterial({ map: textCanvasTexture([{ text: '$ WAFFEN & TÜRME $', size: 30, color: '#212121' }], 512, 96, '#ffca28'), emissive: '#ffffff', emissiveIntensity: 0.5 });
  sign.emissiveMap = sign.map;
  for (const z of [0.0]) { const s = mesh(boxGeo(2.0, 0.38, 0.08), [mat('#ffca28'), mat('#ffca28'), mat('#ffca28'), mat('#ffca28'), sign, sign], 0, 2.25, z, false); root.add(s); }
  root.add(mesh(boxGeo(0.08, 0.5, 0.08), mat('#424242'), -0.6, 1.95, 0));
  root.add(mesh(boxGeo(0.08, 0.5, 0.08), mat('#424242'), 0.6, 1.95, 0));
  return root;
}

// ------------------------------------------------------------------ Beschriftung
export function makeLabel(text, color = '#ffffff', bg = 'rgba(0,0,0,0.45)') {
  const c = document.createElement('canvas'); c.width = 256; c.height = 64;
  const g = c.getContext('2d');
  g.font = 'bold 30px system-ui, sans-serif';
  const w = Math.min(250, g.measureText(text).width + 24);
  g.fillStyle = bg; roundRect(g, (256 - w) / 2, 10, w, 44, 12); g.fill();
  g.fillStyle = color; g.textAlign = 'center'; g.textBaseline = 'middle'; g.fillText(text, 128, 33);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
  const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, depthTest: false, transparent: true }));
  sp.scale.set(1.6, 0.4, 1); sp.renderOrder = 10;
  return sp;
}
function roundRect(g, x, y, w, h, r) {
  g.beginPath(); g.moveTo(x + r, y); g.arcTo(x + w, y, x + w, y + h, r); g.arcTo(x + w, y + h, x, y + h, r); g.arcTo(x, y + h, x, y, r); g.arcTo(x, y, x + w, y, r); g.closePath();
}

/** Kleiner Lebensbalken (immer zur Kamera gedreht) */
export function makeHealthBar(width = 0.9) {
  const grp = new THREE.Group();
  const bg = new THREE.Mesh(new THREE.PlaneGeometry(width, 0.1), new THREE.MeshBasicMaterial({ color: '#000000', transparent: true, opacity: 0.55, depthTest: false }));
  const fgGeo = new THREE.PlaneGeometry(width - 0.04, 0.06); fgGeo.translate((width - 0.04) / 2, 0, 0);
  const fg = new THREE.Mesh(fgGeo, new THREE.MeshBasicMaterial({ color: '#76ff03', depthTest: false }));
  fg.position.set(-(width - 0.04) / 2, 0, 0.001);
  bg.renderOrder = 11; fg.renderOrder = 12;
  grp.add(bg, fg);
  grp.userData.set = frac => {
    fg.scale.x = Math.max(0.001, frac);
    fg.material.color.set(frac > 0.5 ? '#76ff03' : frac > 0.25 ? '#ffc400' : '#ff1744');
  };
  return grp;
}
