// Kartendefinitionen – gemeinsam für Server (Kollision / Wegfindung) und Client (Darstellung).
// Ein Objekt: { x, z, w, d, h, y?, color, kind, solid? }  (x/z = Mittelpunkt, w = Breite X, d = Tiefe Z)

function B(x, z, w, d, h, color, kind = 'block', extra = {}) {
  return { x, z, w, d, h, y: 0, color, kind, solid: true, ...extra };
}
const tree = (x, z, s = 1) => B(x, z, 0.8 * s, 0.8 * s, 6 * s, '#6d4c41', 'tree', { leaf: '#43a047' });
const pine = (x, z, s = 1) => B(x, z, 0.7 * s, 0.7 * s, 7 * s, '#5d4037', 'pine', { leaf: '#2e7d32' });
const crate = (x, z, s = 1.2) => B(x, z, s, s, s, '#a1887f', 'crate');
const barrel = (x, z, c = '#c62828') => B(x, z, 0.8, 0.8, 1.1, c, 'barrel');

function fenceLine(x1, z1, x2, z2, color = '#d7ccc8', h = 1.1, kind = 'fence') {
  // Nur achsparallele Zäune
  if (z1 === z2) return B((x1 + x2) / 2, z1, Math.abs(x2 - x1), 0.2, h, color, kind);
  return B(x1, (z1 + z2) / 2, 0.2, Math.abs(z2 - z1), h, color, kind);
}

// ---------------------------------------------------------------- Vorstadt
function suburb() {
  const o = [];
  // Häuser
  o.push(B(-18, -18, 11, 9, 5.5, '#ffcc80', 'house', { roof: '#c62828' }));
  o.push(B(18, -19, 9, 11, 5.5, '#90caf9', 'house', { roof: '#37474f' }));
  o.push(B(-19, 18, 10, 10, 6.5, '#e0e0e0', 'house', { roof: '#1565c0' }));
  o.push(B(18, 18, 12, 9, 5.5, '#c5e1a5', 'house', { roof: '#6d4c41' }));
  o.push(B(-31, -31, 7, 7, 5, '#f8bbd0', 'house', { roof: '#4e342e' }));
  o.push(B(31, 30, 7, 7, 5, '#fff59d', 'house', { roof: '#b71c1c' }));
  // Autos auf der Straße
  o.push(B(2, -15, 2.1, 4.4, 1.5, '#1e88e5', 'car'));
  o.push(B(-2, 24, 2.1, 4.4, 1.5, '#e53935', 'car'));
  o.push(B(15, 2, 4.4, 2.1, 1.5, '#fdd835', 'car'));
  o.push(B(-26, -2, 4.4, 2.1, 1.5, '#ffffff', 'car'));
  o.push(B(29, -1.8, 4.4, 2.1, 1.5, '#424242', 'car'));
  // Gartenzäune
  o.push(fenceLine(-24, -11, -12, -11));
  o.push(fenceLine(12, -11, 22, -11));
  o.push(fenceLine(-24, 11, -13, 11));
  o.push(fenceLine(13, 11, 24, 11));
  o.push(fenceLine(-11, -24, -11, -14));
  o.push(fenceLine(11, 13, 11, 24));
  // Bäume
  for (const [x, z] of [[-9, -30], [9, -32], [-32, 9], [-35, 22], [32, -12], [35, 14], [-9, 33], [10, 34], [26, -32], [-28, 30], [25, 8], [-8, -9], [8, 9]]) o.push(tree(x, z, 0.9 + Math.abs(x * z) % 3 * 0.12));
  // Kisten & Barrikaden am Platz
  o.push(crate(6, -6)); o.push(crate(-6, 6)); o.push(crate(6.6, 6.4, 1)); o.push(crate(-6.5, -6.2, 1));
  o.push(B(-7.5, -6.5, 0.3, 0.3, 4, '#616161', 'lamp')); o.push(B(7.5, 6.8, 0.3, 0.3, 4, '#616161', 'lamp'));
  o.push(B(-28, 12, 3, 1.4, 1.6, '#43a047', 'dumpster'));
  o.push(B(26, -10, 3, 1.4, 1.6, '#1565c0', 'dumpster'));
  return {
    id: 'suburb', name: 'Vorstadt', desc: 'Ruhige Wohnsiedlung – Straßen aus allen Richtungen.',
    size: 84, ground: '#6abf4b', border: 'hedge',
    sky: '#8fd3ff', fog: ['#bfe6ff', 45, 120], sun: { color: '#fff4e0', intensity: 2.2, pos: [30, 50, 20] }, ambient: 1.1, hemiGround: '#5a8f3a',
    decals: [
      { x: 0, z: 0, w: 8, d: 84, color: '#4a4a4a', kind: 'road' },
      { x: 0, z: 0, w: 84, d: 8, color: '#4a4a4a', kind: 'road' },
      { x: 0, z: 0, w: 18, d: 18, color: '#bdbdbd', kind: 'plaza' },
    ],
    gen: { x: 0, z: 0 }, shop: { x: 3.2, z: -3.6 }, crate: { x: -4.2, z: -3.2 }, spawn: { x: 0, z: 4.5 },
    zspawns: [[0, -40], [0, 40], [-40, 0], [40, 0], [-38, -38], [38, -38], [-38, 38], [38, 38], [-40, 20], [40, -22]],
    objects: o,
  };
}

// ---------------------------------------------------------------- Bauernhof
function farm() {
  const o = [];
  o.push(B(-17, -15, 14, 10, 7, '#c62828', 'barn', { roof: '#5d4037' }));
  o.push(B(-28, -4, 5, 5, 11, '#b0bec5', 'silo'));
  o.push(B(-28, -10.5, 4, 4, 9, '#b0bec5', 'silo'));
  o.push(B(18, -17, 10, 9, 5.5, '#fff3e0', 'house', { roof: '#6d4c41' }));
  o.push(B(9, 12, 2.6, 4.2, 2.4, '#43a047', 'tractor'));
  o.push(B(-10, 14, 4, 2, 0.9, '#78909c', 'trough'));
  // Heuballen
  for (const [x, z] of [[-6, -4], [-7.2, -4], [5, 6], [6.2, 6], [5.6, 7.2], [-12, 5], [14, -4], [-3, 20], [22, 2], [-22, 8], [-24, 9.3]]) o.push(B(x, z, 1.2, 1.2, 1.2, '#fbc02d', 'hay'));
  // Weidezäune mit Lücken
  o.push(fenceLine(-36, 22, -14, 22, '#8d6e63'));
  o.push(fenceLine(-10, 22, 6, 22, '#8d6e63'));
  o.push(fenceLine(-36, 22, -36, 36, '#8d6e63'));
  o.push(fenceLine(26, -6, 26, 8, '#8d6e63'));
  o.push(fenceLine(26, 12, 26, 30, '#8d6e63'));
  o.push(fenceLine(-6, -36, -6, -24, '#8d6e63'));
  o.push(fenceLine(6, -36, 6, -26, '#8d6e63'));
  // Maisfeld (nicht solide – Zombies schleichen hindurch)
  o.push({ x: 18, z: 20, w: 13, d: 13, h: 2.4, y: 0, color: '#c0ca33', kind: 'corn', solid: false });
  o.push({ x: -22, z: 29, w: 10, d: 8, h: 2.4, y: 0, color: '#c0ca33', kind: 'corn', solid: false });
  for (const [x, z] of [[-36, -34], [-30, 34], [34, -34], [36, -10], [33, 36], [-12, -32], [12, -34], [-38, 12], [-4, 33], [30, 6]]) o.push(tree(x, z, 1 + (Math.abs(x + z) % 4) * 0.1));
  o.push(crate(4, -6)); o.push(crate(-5, 7)); o.push(barrel(6.5, -5.5, '#1565c0')); o.push(barrel(-7, 6.8, '#2e7d32'));
  o.push(B(-16, -8.4, 3.2, 0.6, 1.3, '#6d4c41', 'woodpile'));
  return {
    id: 'farm', name: 'Bauernhof', desc: 'Scheune, Silos und ein unheimliches Maisfeld bei Sonnenuntergang.',
    size: 84, ground: '#8bc34a', border: 'woodfence',
    sky: '#ffb074', fog: ['#ffcc99', 40, 115], sun: { color: '#ffcf8a', intensity: 2.0, pos: [-50, 25, 30] }, ambient: 1.0, hemiGround: '#7a5a3a',
    decals: [
      { x: 0, z: 0, w: 20, d: 18, color: '#a1887f', kind: 'dirt' },
      { x: 0, z: -24, w: 6, d: 36, color: '#a1887f', kind: 'dirt' },
      { x: 24, z: 0, w: 40, d: 5, color: '#a1887f', kind: 'dirt' },
    ],
    gen: { x: 0, z: 0 }, shop: { x: -3.6, z: 3.4 }, crate: { x: 4.2, z: -3.2 }, spawn: { x: 1, z: 4.5 },
    zspawns: [[0, -40], [0, 40], [-40, 0], [40, 0], [-38, -38], [38, -38], [-38, 30], [38, 38], [20, 40], [40, 20]],
    objects: o,
  };
}

// ---------------------------------------------------------------- Militärbasis
function base() {
  const o = [];
  const C = '#9e9e9e';
  // Innenmauer mit 4 Toren
  for (const s of [-1, 1]) {
    o.push(B(-10.5, s * 16, 11, 0.8, 3, C, 'concrete'));
    o.push(B(10.5, s * 16, 11, 0.8, 3, C, 'concrete'));
    o.push(B(s * 16, -10.5, 0.8, 11, 3, C, 'concrete'));
    o.push(B(s * 16, 10.5, 0.8, 11, 3, C, 'concrete'));
  }
  // Wachtürme (Beine solide, Plattform nur Deko)
  for (const [x, z] of [[-14, -14], [14, -14], [-14, 14], [14, 14]]) {
    for (const [dx, dz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) o.push(B(x + dx, z + dz, 0.3, 0.3, 4.5, '#5d4037', 'post'));
    o.push({ x, z, w: 3, d: 3, h: 0.3, y: 4.5, color: '#795548', kind: 'platform', solid: false });
  }
  // Sandsäcke an den Toren
  for (const s of [-1, 1]) {
    o.push(B(-3.2, s * 12.5, 2.4, 1, 0.9, '#c8b273', 'sandbag'));
    o.push(B(3.2, s * 12.5, 2.4, 1, 0.9, '#c8b273', 'sandbag'));
    o.push(B(s * 12.5, -3.2, 1, 2.4, 0.9, '#c8b273', 'sandbag'));
    o.push(B(s * 12.5, 3.2, 1, 2.4, 0.9, '#c8b273', 'sandbag'));
  }
  // Container außerhalb
  const cc = ['#c62828', '#1565c0', '#2e7d32', '#ef6c00', '#6a1b9a'];
  [[-28, -24, 6, 2.5], [-28, -20.5, 6, 2.5], [27, -26, 2.5, 6], [30.5, -26, 2.5, 6], [-26, 26, 6, 2.5], [28, 24, 2.5, 6], [-34, 6, 2.5, 6], [34, -4, 2.5, 6]].forEach(([x, z, w, d], i) => o.push(B(x, z, w, d, 2.6, cc[i % cc.length], 'container')));
  // Zelte & Kisten innen
  o.push(B(-8, -8, 5, 4, 2.6, '#556b2f', 'tent'));
  o.push(B(8.5, 8, 5, 4, 2.6, '#556b2f', 'tent'));
  o.push(crate(7, -7)); o.push(crate(8.3, -7, 1)); o.push(crate(-7, 8)); o.push(crate(-8.2, 8.4, 0.9));
  for (const [x, z] of [[-20, 0], [20, 10], [0, -24], [0, 26], [-22, -14], [22, 18]]) o.push(barrel(x, z, '#33691e'));
  // Panzersperren
  for (const [x, z] of [[-6, -24], [6, -24], [-6, 24], [6, 24], [-24, -6], [-24, 6], [24, -6], [24, 6]]) o.push(B(x, z, 1.2, 1.2, 1, '#757575', 'hedgehog'));
  o.push(B(-24, 32, 3.4, 6, 2.6, '#4b5320', 'truck'));
  o.push(B(22, -36, 6, 3.4, 2.6, '#4b5320', 'truck'));
  return {
    id: 'base', name: 'Militärbasis', desc: 'Befestigtes Lager mit vier Toren – ideal für Türme.',
    size: 84, ground: '#9a9a7a', border: 'wall',
    sky: '#a7b4c2', fog: ['#b5bfc9', 35, 105], sun: { color: '#ffffff', intensity: 1.6, pos: [20, 50, -30] }, ambient: 1.3, hemiGround: '#6b6b55',
    decals: [
      { x: 0, z: 0, w: 32, d: 32, color: '#8d8d8d', kind: 'concrete' },
      { x: 0, z: 0, w: 4, d: 84, color: '#7a7a6a', kind: 'dirt' },
      { x: 0, z: 0, w: 84, d: 4, color: '#7a7a6a', kind: 'dirt' },
    ],
    gen: { x: 0, z: 0 }, shop: { x: 3.4, z: 3.4 }, crate: { x: -4.2, z: -3.2 }, spawn: { x: -1, z: 4.5 },
    zspawns: [[0, -40], [0, 40], [-40, 0], [40, 0], [-38, -38], [38, -38], [-38, 38], [38, 38], [-40, -20], [40, 26]],
    objects: o,
  };
}

// ---------------------------------------------------------------- Friedhof (Nacht)
function graveyard() {
  const o = [];
  o.push(B(0, -25, 8, 6, 5, '#90a4ae', 'crypt'));
  o.push(B(24, -20, 9, 13, 8, '#795548', 'church', { roof: '#37474f' }));
  // Grabreihen
  const plots = [[-26, -26, 4, 4], [-26, 12, 4, 5], [10, 14, 5, 4], [-14, -12, 3, 3], [12, -6, 3, 3], [-12, 26, 5, 2], [20, 30, 4, 2]];
  for (const [px, pz, nx, nz] of plots) {
    for (let i = 0; i < nx; i++) for (let j = 0; j < nz; j++) {
      const big = (i * 7 + j * 3) % 5 === 0;
      o.push(B(px + i * 2.6, pz + j * 3, big ? 0.9 : 0.75, 0.3, big ? 1.5 : 1.0, big ? '#b0bec5' : '#9e9e9e', big ? 'cross' : 'grave'));
    }
  }
  // Eisenzaun-Segmente
  o.push(fenceLine(-34, -34, -10, -34, '#263238', 1.8, 'ironfence'));
  o.push(fenceLine(10, -34, 34, -34, '#263238', 1.8, 'ironfence'));
  o.push(fenceLine(-34, -34, -34, -8, '#263238', 1.8, 'ironfence'));
  o.push(fenceLine(-34, 8, -34, 34, '#263238', 1.8, 'ironfence'));
  o.push(fenceLine(34, 4, 34, 34, '#263238', 1.8, 'ironfence'));
  o.push(fenceLine(-30, 34, -6, 34, '#263238', 1.8, 'ironfence'));
  // Tote Bäume
  for (const [x, z] of [[-6, -14], [16, 4], [-20, -2], [4, 20], [-30, -12], [30, -4], [-8, 38], [35, -28], [-27, 38], [26, 10]]) o.push(B(x, z, 0.7, 0.7, 5.5, '#3e2723', 'deadtree'));
  // Laternen
  for (const [x, z] of [[-5, -5], [5, 5], [-5, 5], [5, -5], [0, -20], [18, -10]]) o.push(B(x, z, 0.3, 0.3, 3.2, '#212121', 'lantern'));
  o.push(crate(3, -7, 1)); o.push(crate(-7, -2.5, 1));
  return {
    id: 'graveyard', name: 'Friedhof', desc: 'Nachts, Nebel, Taschenlampe. Viel Glück.',
    size: 84, ground: '#34502f', border: 'ironfence', night: true,
    sky: '#0b1026', fog: ['#0f1530', 14, 60], sun: { color: '#8fa8ff', intensity: 0.55, pos: [-30, 60, -20] }, ambient: 0.45, hemiGround: '#111',
    decals: [
      { x: 0, z: 0, w: 14, d: 14, color: '#5d5548', kind: 'dirt' },
      { x: 0, z: -10, w: 3, d: 30, color: '#5d5548', kind: 'dirt' },
      { x: 10, z: -10, w: 22, d: 3, color: '#5d5548', kind: 'dirt' },
    ],
    gen: { x: 0, z: 0 }, shop: { x: -3.4, z: -3.4 }, crate: { x: 4.2, z: 3.2 }, spawn: { x: 0, z: 4.5 },
    zspawns: [[0, -40], [0, 40], [-40, 0], [40, 0], [-38, -38], [38, -38], [-38, 38], [38, 38], [40, -20], [-20, 40]],
    objects: o,
  };
}

import { RUINS } from './maps-ruins.js';
export const MAPS = { suburb: suburb(), farm: farm(), base: base(), graveyard: graveyard(), ruins: RUINS };
export const MAP_ORDER = ['suburb', 'farm', 'base', 'graveyard', 'ruins'];
