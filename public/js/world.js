// Baut die 3D-Welt einer Karte (Boden, Gebäude, Deko, Himmel, Licht)
import * as THREE from 'three';
import { mergeGeometries } from 'three/addons/utils/BufferGeometryUtils.js';
import { getSolids } from '/shared/geom.js';
import { boxGeo, cylGeo, mat, buildGenerator, buildShop, buildCrate } from './models.js';

function rng(seed) {
  return () => { seed |= 0; seed = (seed + 0x6D2B79F5) | 0; let t = Math.imul(seed ^ (seed >>> 15), 1 | seed); t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t; return ((t ^ (t >>> 14)) >>> 0) / 4294967296; };
}

// ------------------------------------------------------------------ Texturen
const texCache = new Map();
function canvasTex(key, size, draw, repeat = null) {
  if (texCache.has(key)) return texCache.get(key);
  const c = document.createElement('canvas'); c.width = c.height = size;
  draw(c.getContext('2d'), size);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace; t.wrapS = t.wrapT = THREE.RepeatWrapping;
  t.anisotropy = 4;
  if (repeat) t.repeat.set(repeat, repeat);
  texCache.set(key, t);
  return t;
}
function shade(hex, f) { const c = new THREE.Color(hex); c.multiplyScalar(f); return '#' + c.getHexString(); }

function groundTex(color) {
  return canvasTex('ground' + color, 256, (g, s) => {
    g.fillStyle = color; g.fillRect(0, 0, s, s);
    const r = rng(7);
    for (let i = 0; i < 900; i++) { g.fillStyle = shade(color, 0.85 + r() * 0.3); g.fillRect(r() * s, r() * s, 3, 3); }
    // Noppen (Studs) im Baustein-Look
    for (let y = 0; y < 4; y++) for (let x = 0; x < 4; x++) {
      const cx = x * 64 + 32, cy = y * 64 + 32;
      g.fillStyle = shade(color, 0.86); g.beginPath(); g.arc(cx + 2, cy + 2, 14, 0, Math.PI * 2); g.fill();
      g.fillStyle = shade(color, 1.08); g.beginPath(); g.arc(cx, cy, 13, 0, Math.PI * 2); g.fill();
    }
  });
}
const plankTex = () => canvasTex('plank', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  g.fillStyle = '#d7c3a5'; for (let i = 0; i < 4; i++) g.fillRect(0, i * 16 + 14, s, 2);
  g.strokeStyle = '#a88a62'; g.lineWidth = 5; g.strokeRect(3, 3, s - 6, s - 6);
  g.beginPath(); g.moveTo(4, 4); g.lineTo(s - 4, s - 4); g.stroke();
});
const ribTex = () => canvasTex('rib', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  for (let i = 0; i < 8; i++) { g.fillStyle = i % 2 ? '#d0d0d0' : '#ffffff'; g.fillRect(i * 8, 0, 8, s); }
});
const hayTex = () => canvasTex('hay', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  const r = rng(3); g.strokeStyle = '#d9c27a';
  for (let i = 0; i < 70; i++) { g.beginPath(); const x = r() * s, y = r() * s; g.moveTo(x, y); g.lineTo(x + r() * 10 - 5, y + 8); g.stroke(); }
  g.fillStyle = '#a1793d'; g.fillRect(0, 20, s, 3); g.fillRect(0, 42, s, 3);
});
const bagTex = () => canvasTex('bag', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  g.strokeStyle = '#b5a06a'; g.lineWidth = 3;
  for (let y = 0; y < 2; y++) for (let x = 0; x < 2; x++) { g.beginPath(); g.roundRect ? g.roundRect(x * 32 + (y ? 16 : 0) - 14, y * 32 + 2, 30, 28, 10) : g.rect(x * 32, y * 32, 30, 28); g.stroke(); }
});
const brickTex = () => canvasTex('brick', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  g.fillStyle = '#d9d9d9';
  for (let y = 0; y < 8; y++) { g.fillRect(0, y * 8, s, 1); for (let x = 0; x < 4; x++) g.fillRect(x * 16 + (y % 2 ? 8 : 0), y * 8, 1, 8); }
});
const leafTex = () => canvasTex('leaf', 64, (g, s) => {
  g.fillStyle = '#ffffff'; g.fillRect(0, 0, s, s);
  const r = rng(11); for (let i = 0; i < 160; i++) { g.fillStyle = r() > 0.5 ? '#d8ecd8' : '#f2fff2'; g.fillRect(r() * s, r() * s, 4, 4); }
});

function texMat(color, tex) { return mat(color, { map: tex }); }

// ------------------------------------------------------------------ Bausteine
function M(geo, material, x, y, z, rx = 0, ry = 0, rz = 0) {
  const m = new THREE.Mesh(geo, material);
  m.position.set(x, y, z); m.rotation.set(rx, ry, rz);
  m.castShadow = true; m.receiveShadow = true;
  return m;
}
function gableGeo(w, rh, d) {
  const sh = new THREE.Shape();
  sh.moveTo(-w / 2, 0); sh.lineTo(w / 2, 0); sh.lineTo(0, rh); sh.closePath();
  const g = new THREE.ExtrudeGeometry(sh, { depth: d, bevelEnabled: false });
  g.translate(0, 0, -d / 2);
  return g;
}

function addObject(o, G, ctx) {
  const { x, z, w, d, h } = o; const y0 = o.y || 0;
  const add = m => { G.add(m); return m; };
  const c = o.color;
  switch (o.kind) {
    case 'house': {
      add(M(boxGeo(w, h, d), texMat(c, brickTex()), x, h / 2, z));
      const rh = Math.min(w, d) * 0.42;
      const roof = M(new THREE.ConeGeometry(1, rh, 4), mat(o.roof), x, h + rh / 2, z, 0, Math.PI / 4);
      roof.scale.set((w + 1) / Math.SQRT2, 1, (d + 1) / Math.SQRT2); add(roof);
      add(M(boxGeo(0.8, 1.6, 0.8), mat('#8d6e63'), x + w * 0.25, h + rh * 0.5, z - d * 0.15));
      // Tür zur Kartenmitte
      const toX = Math.abs(x) > Math.abs(z);
      const sx = -Math.sign(x) || 1, sz = -Math.sign(z) || 1;
      if (toX) add(M(boxGeo(0.12, 2.2, 1.3), mat('#5d4037'), x + sx * (w / 2 + 0.03), 1.1, z));
      else add(M(boxGeo(1.3, 2.2, 0.12), mat('#5d4037'), x, 1.1, z + sz * (d / 2 + 0.03)));
      const glass = mat('#90caf9', { emissive: ctx.night ? '#ffd54f' : '#000000', emissiveIntensity: ctx.night ? 0.6 : 0, roughness: 0.2 });
      for (const f of [-1, 1]) for (const k of [-1, 1]) {
        add(M(boxGeo(1.2, 1.1, 0.1), glass, x + k * w * 0.27, h * 0.62, z + f * (d / 2 + 0.03)));
        add(M(boxGeo(0.1, 1.1, 1.2), glass, x + f * (w / 2 + 0.03), h * 0.62, z + k * d * 0.27));
      }
      break;
    }
    case 'barn': {
      add(M(boxGeo(w, h, d), texMat(c, plankTex()), x, h / 2, z));
      const rh = w * 0.38;
      const roofG = gableGeo(w + 0.8, rh, d + 0.8);
      add(M(roofG, mat(o.roof), x, h, z));
      for (const f of [-1, 1]) {
        add(M(boxGeo(4, 4.5, 0.12), mat('#ffffff'), x, 2.25, z + f * (d / 2 + 0.03)));
        add(M(boxGeo(3.6, 4.1, 0.14), mat('#8e2020'), x, 2.25, z + f * (d / 2 + 0.04)));
        const b1 = M(boxGeo(5.2, 0.25, 0.16), mat('#ffffff'), x, 2.25, z + f * (d / 2 + 0.06), 0, 0, 0.83); add(b1);
        const b2 = M(boxGeo(5.2, 0.25, 0.16), mat('#ffffff'), x, 2.25, z + f * (d / 2 + 0.06), 0, 0, -0.83); add(b2);
      }
      break;
    }
    case 'silo': {
      add(M(cylGeo(w / 2, w / 2, h, 18), texMat(c, ribTex()), x, h / 2, z));
      add(M(new THREE.SphereGeometry(w / 2, 18, 8, 0, Math.PI * 2, 0, Math.PI / 2), mat('#78909c', { metalness: 0.4 }), x, h, z));
      for (let i = 1; i < 4; i++) add(M(cylGeo(w / 2 + 0.05, w / 2 + 0.05, 0.15, 18), mat('#607d8b'), x, (h / 4) * i, z));
      break;
    }
    case 'church': {
      add(M(boxGeo(w, h, d), texMat(c, brickTex()), x, h / 2, z));
      const rh = w * 0.5;
      add(M(gableGeo(w + 0.6, rh, d + 0.6), mat(o.roof), x, h, z));
      const tz = z + d / 2 - 1.6;
      add(M(boxGeo(3.2, h + 5, 3.2), texMat(c, brickTex()), x, (h + 5) / 2, tz));
      add(M(new THREE.ConeGeometry(2.4, 4.5, 4), mat(o.roof), x, h + 5 + 2.25, tz, 0, Math.PI / 4));
      add(M(boxGeo(0.25, 1.6, 0.25), mat('#ffd54f'), x, h + 5 + 5.2, tz));
      add(M(boxGeo(0.9, 0.25, 0.25), mat('#ffd54f'), x, h + 5 + 5.4, tz));
      const stained = mat('#7e57c2', { emissive: '#7e57c2', emissiveIntensity: ctx.night ? 1.2 : 0.2 });
      for (const f of [-1, 1]) for (let i = -1; i <= 1; i++) add(M(boxGeo(0.12, 2.2, 1.0), stained, x + f * (w / 2 + 0.04), h * 0.55, z + i * 3.2));
      break;
    }
    case 'crypt': {
      add(M(boxGeo(w, h, d), mat(c), x, h / 2, z));
      add(M(boxGeo(w + 0.6, 0.4, d + 0.6), mat('#78909c'), x, h + 0.2, z));
      add(M(gableGeo(w + 0.4, 1.4, 1.2), mat('#b0bec5'), x, h + 0.4, z + d / 2 - 0.2));
      for (let i = -1.5; i <= 1.5; i += 1) add(M(cylGeo(0.22, 0.25, h, 10), mat('#cfd8dc'), x + i * (w / 4), h / 2, z + d / 2 + 0.5));
      add(M(boxGeo(1.6, 2.6, 0.1), mat('#212121'), x, 1.3, z + d / 2 + 0.03));
      break;
    }
    case 'car': case 'truck': {
      const along = d > w; const L = along ? d : w, W = along ? w : d;
      const grp = new THREE.Group(); grp.position.set(x, 0, z); if (!along) grp.rotation.y = Math.PI / 2;
      const isTruck = o.kind === 'truck';
      grp.add(M(boxGeo(W, isTruck ? 1.0 : 0.7, L), mat(c, { metalness: 0.4, roughness: 0.35 }), 0, isTruck ? 0.95 : 0.6, 0));
      if (isTruck) {
        grp.add(M(boxGeo(W * 0.95, 1.1, L * 0.3), mat(c), 0, 1.95, L * 0.33));
        grp.add(M(boxGeo(W * 0.9, 0.5, 0.05), mat('#263238', { roughness: 0.1 }), 0, 2.0, L * 0.48 + 0.02));
        grp.add(M(boxGeo(W * 1.02, 1.3, L * 0.62), mat('#3b4220'), 0, 2.1, -L * 0.18));
      } else {
        grp.add(M(boxGeo(W * 0.88, 0.6, L * 0.5), mat('#263238', { metalness: 0.2, roughness: 0.1 }), 0, 1.25, -L * 0.05));
        grp.add(M(boxGeo(W * 0.9, 0.08, L * 0.52), mat(c), 0, 1.58, -L * 0.05));
        for (const s of [-1, 1]) grp.add(M(boxGeo(0.35, 0.18, 0.05), mat('#fff9c4', { emissive: '#fff59d', emissiveIntensity: ctx.night ? 1.5 : 0.3 }), s * W * 0.32, 0.7, L / 2 + 0.02));
      }
      for (const sx of [-1, 1]) for (const sz of [-1, 1]) grp.add(M(cylGeo(0.36, 0.36, 0.3, 12), mat('#212121'), sx * (W / 2 - 0.05), 0.36, sz * L * 0.32, 0, 0, Math.PI / 2));
      add(grp); break;
    }
    case 'tractor': {
      const grp = new THREE.Group(); grp.position.set(x, 0, z);
      grp.add(M(boxGeo(1.4, 0.9, 3.2), mat(c), 0, 1.2, 0.2));
      grp.add(M(boxGeo(1.6, 1.3, 1.4), mat('#263238', { roughness: 0.1 }), 0, 2.0, -0.8));
      grp.add(M(boxGeo(1.8, 0.12, 1.6), mat(c), 0, 2.7, -0.8));
      grp.add(M(cylGeo(0.08, 0.08, 1.2, 8), mat('#424242'), 0.45, 2.3, 0.9));
      for (const s of [-1, 1]) {
        grp.add(M(cylGeo(0.85, 0.85, 0.5, 14), mat('#212121'), s * 1.0, 0.85, -0.9, 0, 0, Math.PI / 2));
        grp.add(M(cylGeo(0.5, 0.5, 0.5, 14), mat('#fdd835'), s * 1.01, 0.85, -0.9, 0, 0, Math.PI / 2));
        grp.add(M(cylGeo(0.48, 0.48, 0.35, 12), mat('#212121'), s * 0.85, 0.48, 1.4, 0, 0, Math.PI / 2));
      }
      add(grp); break;
    }
    case 'tree': case 'pine': {
      const s = h / 6;
      add(M(boxGeo(w, h * 0.55, d), mat(c), x, h * 0.275, z));
      const leaf = texMat(o.leaf, leafTex());
      if (o.kind === 'tree') {
        add(M(boxGeo(3.2 * s, 2.4 * s, 3.2 * s), leaf, x, h * 0.62, z, 0, 0.3));
        add(M(boxGeo(2.2 * s, 1.8 * s, 2.2 * s), mat(shade(o.leaf, 1.12), { map: leafTex() }), x + 0.3 * s, h * 0.86, z - 0.2 * s, 0, 0.8));
      } else {
        for (let i = 0; i < 3; i++) add(M(new THREE.ConeGeometry((2.2 - i * 0.55) * s, 2.2 * s, 6), leaf, x, h * 0.4 + i * 1.3 * s + 1, z));
      }
      break;
    }
    case 'deadtree': {
      add(M(boxGeo(w, h, d), mat(c), x, h / 2, z));
      const r = rng(Math.round(x * 13 + z * 7));
      for (let i = 0; i < 4; i++) {
        const len = 1.2 + r() * 1.2, a = r() * Math.PI * 2;
        const br = M(boxGeo(0.2, len, 0.2), mat(c), x + Math.cos(a) * len * 0.35, h * (0.55 + i * 0.12), z + Math.sin(a) * len * 0.35);
        br.rotation.set(Math.sin(a) * 0.9, 0, -Math.cos(a) * 0.9); add(br);
      }
      break;
    }
    case 'crate': add(M(boxGeo(w, h, d), texMat(c, plankTex()), x, y0 + h / 2, z)); break;
    case 'hay': add(M(boxGeo(w, h, d), texMat(c, hayTex()), x, h / 2, z)); break;
    case 'container': add(M(boxGeo(w, h, d), texMat(c, ribTex()), x, h / 2, z)); add(M(boxGeo(w + 0.05, 0.12, d + 0.05), mat(shade(c, 0.7)), x, h, z)); break;
    case 'sandbag': add(M(boxGeo(w, h, d), texMat(c, bagTex()), x, h / 2, z)); break;
    case 'concrete': add(M(boxGeo(w, h, d), texMat(c, brickTex()), x, h / 2, z)); add(M(boxGeo(w + 0.1, 0.2, d + 0.1), mat('#757575'), x, h + 0.1, z)); break;
    case 'barrel': {
      add(M(cylGeo(w / 2, w / 2, h, 12), mat(c, { metalness: 0.3 }), x, h / 2, z));
      for (const yy of [0.25, 0.75]) add(M(cylGeo(w / 2 + 0.03, w / 2 + 0.03, 0.06, 12), mat(shade(c, 0.6)), x, h * yy, z));
      break;
    }
    case 'fence': case 'ironfence': {
      const along = w > d; const L = along ? w : d;
      const iron = o.kind === 'ironfence';
      const step = iron ? 0.35 : 2;
      const n = Math.max(1, Math.round(L / step));
      for (let i = 0; i <= n; i++) {
        const t = -L / 2 + (i / n) * L;
        const px = along ? x + t : x, pz = along ? z : z + t;
        add(M(boxGeo(iron ? 0.06 : 0.16, h, iron ? 0.06 : 0.16), mat(c), px, h / 2, pz));
        if (iron && i % 6 === 0) add(M(boxGeo(0.14, h + 0.3, 0.14), mat(c), px, (h + 0.3) / 2, pz));
      }
      for (const yy of iron ? [0.25, h - 0.15] : [h * 0.4, h * 0.85]) add(M(boxGeo(along ? L : 0.08, iron ? 0.06 : 0.12, along ? 0.08 : L), mat(c), x, yy, z));
      break;
    }
    case 'tent': {
      add(M(gableGeo(w, h, d), mat(c), x, 0, z));
      add(M(boxGeo(1.0, 1.4, 0.05), mat('#1b1b1b'), x, 0.7, z + d / 2 + 0.01));
      break;
    }
    case 'post': add(M(boxGeo(w, h, d), mat(c), x, h / 2, z)); break;
    case 'platform': {
      add(M(boxGeo(w, h, d), mat(c), x, y0 + h / 2, z));
      for (const s of [-1, 1]) { add(M(boxGeo(w, 0.6, 0.08), mat(c), x, y0 + 0.6, z + s * d / 2)); add(M(boxGeo(0.08, 0.6, d), mat(c), x + s * w / 2, y0 + 0.6, z)); }
      for (const [dx, dz] of [[-1, -1], [1, -1], [-1, 1], [1, 1]]) add(M(boxGeo(0.12, 1.6, 0.12), mat('#5d4037'), x + dx * (w / 2 - 0.1), y0 + 1.1, z + dz * (d / 2 - 0.1)));
      add(M(new THREE.ConeGeometry(2.4, 1.0, 4), mat('#4e342e'), x, y0 + 2.4, z, 0, Math.PI / 4));
      break;
    }
    case 'hedgehog': {
      for (const [rx, rz] of [[0.6, 0], [-0.6, 0], [0, 0.6]]) add(M(boxGeo(0.15, 1.6, 0.15), mat(c, { metalness: 0.5 }), x, 0.55, z, rx, rz * 2, rz));
      break;
    }
    case 'grave': {
      add(M(boxGeo(w, h, d), mat(c), x, h / 2, z));
      add(M(boxGeo(w * 0.8, 0.15, 1.6), mat('#4e3b2a'), x, 0.07, z + 0.95));
      break;
    }
    case 'cross': {
      add(M(boxGeo(0.25, h, 0.25), mat(c), x, h / 2, z));
      add(M(boxGeo(w, 0.22, 0.24), mat(c), x, h * 0.72, z));
      add(M(boxGeo(0.8, 0.15, 1.6), mat('#4e3b2a'), x, 0.07, z + 0.95));
      break;
    }
    case 'lantern': case 'lamp': {
      add(M(boxGeo(0.18, h, 0.18), mat(c, { metalness: 0.5 }), x, h / 2, z));
      const glow = mat('#fff59d', { emissive: '#ffd54f', emissiveIntensity: ctx.night ? 2.5 : 0.4 });
      if (o.kind === 'lamp') { add(M(boxGeo(0.9, 0.12, 0.15), mat(c), x + 0.4, h, z)); add(M(boxGeo(0.35, 0.15, 0.3), glow, x + 0.75, h - 0.1, z)); }
      else { add(M(boxGeo(0.4, 0.45, 0.4), glow, x, h + 0.2, z)); add(M(boxGeo(0.5, 0.08, 0.5), mat(c), x, h + 0.45, z)); }
      if (ctx.night && ctx.lights < 6) {
        ctx.lights++;
        const pl = new THREE.PointLight('#ffcc66', 18, 14, 1.6); pl.position.set(x + (o.kind === 'lamp' ? 0.75 : 0), h + 0.1, z); G.add(pl);
      }
      break;
    }
    case 'dumpster': add(M(boxGeo(w, h, d), mat(c, { metalness: 0.4 }), x, h / 2, z)); add(M(boxGeo(w + 0.1, 0.1, d + 0.1), mat(shade(c, 0.6)), x, h + 0.05, z)); break;
    case 'trough': add(M(boxGeo(w, h, d), mat(c), x, h / 2, z)); add(M(boxGeo(w - 0.2, 0.02, d - 0.2), mat('#4fc3f7', { roughness: 0.1 }), x, h - 0.1, z)); break;
    case 'woodpile': {
      for (let i = 0; i < 6; i++) {
        const row = i < 3 ? 0 : 1, col = i % 3;
        add(M(cylGeo(0.22, 0.22, d + 0.1, 8), mat(i % 2 ? '#8d6e63' : '#795548'), x - w / 3 + col * (w / 3) + row * 0.25, 0.22 + row * 0.42, z, Math.PI / 2));
      }
      break;
    }
    case 'corn': {
      const n = Math.round(w * d * 2.2);
      const geo = new THREE.BoxGeometry(0.1, 1, 0.1); geo.translate(0, 0.5, 0);
      const inst = new THREE.InstancedMesh(geo, new THREE.MeshStandardMaterial({ color: '#ffffff', roughness: 0.8 }), n);
      const r = rng(Math.round(x * 31 + z));
      const m4 = new THREE.Matrix4(), q = new THREE.Quaternion(), e = new THREE.Euler(), col = new THREE.Color();
      for (let i = 0; i < n; i++) {
        e.set((r() - 0.5) * 0.3, r() * 3, (r() - 0.5) * 0.3); q.setFromEuler(e);
        const hh = h * (0.7 + r() * 0.4);
        m4.compose(new THREE.Vector3(x + (r() - 0.5) * w, 0, z + (r() - 0.5) * d), q, new THREE.Vector3(1 + r(), hh, 1 + r()));
        inst.setMatrixAt(i, m4);
        col.set(r() > 0.3 ? '#9e9d24' : '#c0ca33').multiplyScalar(0.8 + r() * 0.3); inst.setColorAt(i, col);
      }
      inst.castShadow = true; inst.receiveShadow = true; inst.userData.noMerge = true;
      G.add(inst); break;
    }
    default: add(M(boxGeo(w, h, d), mat(c), x, y0 + h / 2, z));
  }
}

function addBorder(map, G) {
  const half = map.size / 2 + 0.6, L = map.size + 2.4;
  const kind = map.border;
  const sides = [[0, -half, L, true], [0, half, L, true], [-half, 0, L, false], [half, 0, L, false]];
  for (const [x, z, len, along] of sides) {
    if (kind === 'hedge') {
      G.add(M(boxGeo(along ? len : 1.4, 2.8, along ? 1.4 : len), texMat('#2e7d32', leafTex()), x, 1.4, z));
    } else if (kind === 'wall') {
      G.add(M(boxGeo(along ? len : 0.8, 3.6, along ? 0.8 : len), texMat('#9e9e9e', brickTex()), x, 1.8, z));
      G.add(M(boxGeo(along ? len : 1.0, 0.25, along ? 1.0 : len), mat('#616161'), x, 3.7, z));
    } else {
      const o = { x, z, w: along ? len : 0.2, d: along ? 0.2 : len, h: kind === 'ironfence' ? 2.2 : 1.8, color: kind === 'ironfence' ? '#1f2a30' : '#795548', kind: kind === 'ironfence' ? 'ironfence' : 'fence' };
      addObject(o, G, {});
    }
  }
}

function addScenery(map, G, ctx) {
  // Bäume außerhalb der Grenze (reine Deko)
  const r = rng(map.size * 17 + map.id.length);
  const half = map.size / 2;
  for (let i = 0; i < 70; i++) {
    const a = r() * Math.PI * 2, dist = half + 4 + r() * 26;
    let x = Math.cos(a) * dist, z = Math.sin(a) * dist;
    x = Math.max(-half - 30, Math.min(half + 30, x)); z = Math.max(-half - 30, Math.min(half + 30, z));
    if (Math.abs(x) < half + 3 && Math.abs(z) < half + 3) continue;
    const s = 0.9 + r() * 0.8;
    const kind = map.night ? 'deadtree' : (r() > 0.5 ? 'pine' : 'tree');
    addObject({ x, z, w: 0.8 * s, d: 0.8 * s, h: 6 * s, color: map.night ? '#3e2723' : '#6d4c41', leaf: r() > 0.5 ? '#43a047' : '#388e3c', kind }, G, ctx);
  }
}

/** Fasst statische Meshes mit gleichem Material zusammen (viel weniger Draw-Calls). */
function mergeStatic(group) {
  group.updateMatrixWorld(true);
  const buckets = new Map(), remove = [];
  group.traverse(o => {
    if (!o.isMesh || o.isInstancedMesh || Array.isArray(o.material) || o.userData.noMerge) return;
    let g = o.geometry.index ? o.geometry.toNonIndexed() : o.geometry.clone();
    for (const k of Object.keys(g.attributes)) if (!['position', 'normal', 'uv'].includes(k)) g.deleteAttribute(k);
    g.applyMatrix4(o.matrixWorld);
    const key = o.material.uuid;
    if (!buckets.has(key)) buckets.set(key, { mat: o.material, geos: [] });
    buckets.get(key).geos.push(g);
    remove.push(o);
  });
  for (const o of remove) o.parent.remove(o);
  for (const { mat: m, geos } of buckets.values()) {
    const merged = mergeGeometries(geos, false);
    if (!merged) continue;
    const mesh = new THREE.Mesh(merged, m);
    mesh.castShadow = true; mesh.receiveShadow = true;
    group.add(mesh);
  }
}

// ------------------------------------------------------------------ Himmel
function buildSky(map, scene) {
  const top = new THREE.Color(map.sky).multiplyScalar(map.night ? 0.6 : 0.75);
  const geo = new THREE.SphereGeometry(450, 24, 12);
  const sky = new THREE.Mesh(geo, new THREE.ShaderMaterial({
    side: THREE.BackSide, depthWrite: false, fog: false,
    uniforms: { top: { value: top }, bottom: { value: new THREE.Color(map.fog[0]) } },
    vertexShader: 'varying vec3 vP; void main(){ vP = normalize(position); gl_Position = projectionMatrix * modelViewMatrix * vec4(position,1.0); }',
    fragmentShader: 'uniform vec3 top; uniform vec3 bottom; varying vec3 vP; void main(){ float h = clamp(vP.y*1.6+0.05,0.0,1.0); gl_FragColor = vec4(mix(bottom, top, h),1.0); }',
  }));
  sky.renderOrder = -10;
  scene.add(sky);
  const extras = new THREE.Group(); scene.add(extras);
  // Sonne / Mond
  const sp = new THREE.Vector3(...map.sun.pos).normalize().multiplyScalar(380);
  const disc = new THREE.Mesh(new THREE.CircleGeometry(map.night ? 14 : 20, 24), new THREE.MeshBasicMaterial({ color: map.night ? '#e8eaf6' : '#fff8e1', fog: false }));
  disc.position.copy(sp); disc.lookAt(0, 0, 0); extras.add(disc);
  // Wolken (Klötzchen)
  const clouds = [];
  const r = rng(99);
  const cm = new THREE.MeshBasicMaterial({ color: map.night ? '#2a2f4a' : '#ffffff', fog: false, transparent: true, opacity: map.night ? 0.6 : 0.92 });
  for (let i = 0; i < 18; i++) {
    const c = new THREE.Group();
    const n = 2 + Math.floor(r() * 3);
    for (let j = 0; j < n; j++) { const b = new THREE.Mesh(boxGeo(8 + r() * 10, 3 + r() * 2, 6 + r() * 6), cm); b.position.set(j * 6 - n * 3, r() * 2, (r() - 0.5) * 5); c.add(b); }
    c.position.set((r() - 0.5) * 400, 60 + r() * 30, (r() - 0.5) * 400);
    extras.add(c); clouds.push(c);
  }
  if (map.night) {
    const pts = [];
    for (let i = 0; i < 600; i++) { const v = new THREE.Vector3(r() - 0.5, r() * 0.9 + 0.1, r() - 0.5).normalize().multiplyScalar(420); pts.push(v.x, v.y, v.z); }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(pts, 3));
    extras.add(new THREE.Points(g, new THREE.PointsMaterial({ color: '#ffffff', size: 1.6, fog: false, sizeAttenuation: false })));
  }
  return { sky, extras, clouds };
}

// ------------------------------------------------------------------ Hauptfunktion
export function buildWorld(scene, map, quality) {
  const root = new THREE.Group(); scene.add(root);
  const staticG = new THREE.Group(); root.add(staticG);
  const ctx = { night: !!map.night, lights: 0 };

  scene.background = new THREE.Color(map.fog[0]);
  scene.fog = new THREE.Fog(map.fog[0], map.fog[1], map.fog[2]);

  // Boden
  const gsize = map.size + 90;
  const gt = groundTex(map.ground).clone(); gt.needsUpdate = true; gt.repeat.set(gsize / 4, gsize / 4);
  const ground = new THREE.Mesh(new THREE.PlaneGeometry(gsize, gsize), new THREE.MeshStandardMaterial({ map: gt, roughness: 0.95 }));
  ground.rotation.x = -Math.PI / 2; ground.receiveShadow = true; root.add(ground);

  // Bodenmarkierungen
  map.decals.forEach((dc, i) => {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(dc.w, dc.d), mat(dc.color, { roughness: 0.95, polygonOffset: true, polygonOffsetFactor: -1 - i, polygonOffsetUnits: -1 - i }));
    m.rotation.x = -Math.PI / 2; m.position.set(dc.x, 0.01 + i * 0.004, dc.z); m.receiveShadow = true; staticG.add(m);
    if (dc.kind === 'road') {
      const along = dc.d > dc.w; const L = along ? dc.d : dc.w;
      for (let t = -L / 2 + 2; t < L / 2; t += 4) {
        if (Math.abs(t) < 6) continue;
        const line = new THREE.Mesh(new THREE.PlaneGeometry(along ? 0.2 : 1.8, along ? 1.8 : 0.2), mat('#ffffff', { polygonOffset: true, polygonOffsetFactor: -8, polygonOffsetUnits: -8 }));
        line.rotation.x = -Math.PI / 2; line.position.set(along ? dc.x : t, 0.05, along ? t : dc.z); line.receiveShadow = true; staticG.add(line);
      }
    }
  });

  for (const o of map.objects) addObject(o, staticG, ctx);
  addBorder(map, staticG);
  addScenery(map, staticG, ctx);
  mergeStatic(staticG);

  // Generator & Shop (animiert, nicht zusammengeführt)
  const gen = buildGenerator(); gen.position.set(map.gen.x, 0, map.gen.z);
  gen.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  root.add(gen);
  const genLight = new THREE.PointLight('#00e5ff', map.night ? 25 : 6, 12, 1.5); genLight.position.set(map.gen.x, 2.6, map.gen.z); root.add(genLight);
  const shop = buildShop(); shop.position.set(map.shop.x, 0, map.shop.z);
  shop.rotation.y = Math.atan2(map.shop.x - map.gen.x, map.shop.z - map.gen.z);
  shop.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
  root.add(shop);
  let crate = null;
  if (map.crate) {
    crate = buildCrate(); crate.position.set(map.crate.x, 0, map.crate.z);
    crate.rotation.y = Math.atan2(map.gen.x - map.crate.x, map.gen.z - map.crate.z);
    crate.traverse(o => { if (o.isMesh) { o.castShadow = true; o.receiveShadow = true; } });
    root.add(crate);
  }

  // Licht
  const hemi = new THREE.HemisphereLight(map.sky, map.hemiGround, map.ambient);
  root.add(hemi);
  const sun = new THREE.DirectionalLight(map.sun.color, map.sun.intensity);
  sun.position.set(...map.sun.pos);
  sun.castShadow = quality !== 'low';
  const sc = sun.shadow.camera; const ext = map.size / 2 + 8;
  sc.left = -ext; sc.right = ext; sc.top = ext; sc.bottom = -ext; sc.near = 1; sc.far = 220;
  sun.shadow.mapSize.set(quality === 'high' ? 4096 : 2048, quality === 'high' ? 4096 : 2048);
  sun.shadow.bias = -0.0004; sun.shadow.normalBias = 0.03;
  root.add(sun); root.add(sun.target);
  if (!map.night) root.add(new THREE.AmbientLight('#ffffff', 0.25));

  const skyObj = buildSky(map, root);

  // Kollision & Raycast-Ziele
  const solids = getSolids(map);
  const rayTargets = [];
  const invis = new THREE.MeshBasicMaterial({ visible: false });
  for (const b of solids) {
    const m = new THREE.Mesh(boxGeo(1, 1, 1), invis);
    m.scale.set(b.maxX - b.minX, b.maxY - b.minY, b.maxZ - b.minZ);
    m.position.set((b.minX + b.maxX) / 2, (b.minY + b.maxY) / 2, (b.minZ + b.maxZ) / 2);
    m.updateMatrixWorld(true); m.userData.solid = true;
    rayTargets.push(m);
  }
  ground.userData.ground = true; rayTargets.push(ground);

  let t = 0;
  return {
    root, solids, rayTargets, ground, gen, shop, crate, sun, map,
    crateSpin: 0,
    update(dt) {
      t += dt;
      if (crate) {
        const u = crate.userData;
        this.crateSpin = Math.max(0, this.crateSpin - dt);
        const open = this.crateSpin > 0 ? 1 : 0;
        u.lid.rotation.x += ((open ? -1.9 : 0) - u.lid.rotation.x) * Math.min(1, dt * 6);
        u.glowMat.opacity += ((open ? 0.55 : 0) - u.glowMat.opacity) * Math.min(1, dt * 6);
        u.q.position.y = 1.7 + Math.sin(t * 2.5) * 0.08 + open * 0.6; u.q.rotation.y += dt * (open ? 9 : 1.2);
      }
      gen.userData.fan.rotation.y += dt * 9;
      gen.userData.coreMat.emissiveIntensity = 1.6 + Math.sin(t * 3) * 0.6;
      gen.userData.lamps.forEach((l, i) => { l.emissiveIntensity = (Math.sin(t * 5 + i * 1.57) > 0) ? 2 : 0.2; });
      for (const c of skyObj.clouds) { c.position.x += dt * 2; if (c.position.x > 220) c.position.x = -220; }
    },
    dispose() {
      scene.remove(root);
      root.traverse(o => { if (o.isMesh && o.geometry && !o.geometry.parameters) o.geometry.dispose(); });
    },
  };
}
