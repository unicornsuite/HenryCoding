// Gemeinsame Geometrie-Helfer (Kollision, Raster für Wegfindung)

export const GEN_SIZE = { w: 2.6, d: 2.6, h: 2.4 };
export const SHOP_SIZE = { w: 1.4, d: 0.9, h: 1.7 };
export const CRATE_SIZE = { w: 1.3, d: 0.9, h: 0.9 };

/** Alle soliden AABBs einer Karte inkl. Generator und Shop-Terminal. */
export function getSolids(map) {
  const list = map.objects.filter(o => o.solid !== false).map(o => ({
    minX: o.x - o.w / 2, maxX: o.x + o.w / 2,
    minZ: o.z - o.d / 2, maxZ: o.z + o.d / 2,
    minY: o.y || 0, maxY: (o.y || 0) + o.h, kind: o.kind,
  }));
  list.push({ minX: map.gen.x - GEN_SIZE.w / 2, maxX: map.gen.x + GEN_SIZE.w / 2, minZ: map.gen.z - GEN_SIZE.d / 2, maxZ: map.gen.z + GEN_SIZE.d / 2, minY: 0, maxY: GEN_SIZE.h, kind: 'generator' });
  list.push({ minX: map.shop.x - SHOP_SIZE.w / 2, maxX: map.shop.x + SHOP_SIZE.w / 2, minZ: map.shop.z - SHOP_SIZE.d / 2, maxZ: map.shop.z + SHOP_SIZE.d / 2, minY: 0, maxY: SHOP_SIZE.h, kind: 'shop' });
  if (map.crate) list.push({ minX: map.crate.x - CRATE_SIZE.w / 2, maxX: map.crate.x + CRATE_SIZE.w / 2, minZ: map.crate.z - CRATE_SIZE.d / 2, maxZ: map.crate.z + CRATE_SIZE.d / 2, minY: 0, maxY: CRATE_SIZE.h, kind: 'crate' });
  return list;
}

/** Schiebt einen Kreis (x,z,r) aus einer AABB heraus. Gibt neue Position zurück oder null. */
export function pushCircleOutOfBox(x, z, r, b) {
  const cx = Math.max(b.minX, Math.min(x, b.maxX));
  const cz = Math.max(b.minZ, Math.min(z, b.maxZ));
  let dx = x - cx, dz = z - cz;
  const d2 = dx * dx + dz * dz;
  if (d2 >= r * r) return null;
  if (d2 > 1e-8) {
    const d = Math.sqrt(d2);
    return [cx + (dx / d) * r, cz + (dz / d) * r];
  }
  // Mittelpunkt im Kasten: entlang der kürzesten Achse hinaus
  const l = x - b.minX, rr = b.maxX - x, t = z - b.minZ, bb = b.maxZ - z;
  const m = Math.min(l, rr, t, bb);
  if (m === l) return [b.minX - r, z];
  if (m === rr) return [b.maxX + r, z];
  if (m === t) return [x, b.minZ - r];
  return [x, b.maxZ + r];
}

/** Raster für Wegfindung. cell = Zellgröße in Metern. */
export function buildGrid(map, solids, cell = 1, margin = 0.35) {
  const half = map.size / 2;
  const n = Math.ceil(map.size / cell);
  const blocked = new Uint8Array(n * n);
  if (map.mask) {
    // Vorberechnete Begehbarkeitsmaske (z. B. aus einer Unity-Szene exportiert): Zeilen = z, Zeichen = x, '#' = gesperrt
    const rows = map.mask.split('\n');
    for (let gz = 0; gz < n; gz++) { const row = rows[gz] || ''; for (let gx = 0; gx < n; gx++) blocked[gz * n + gx] = row[gx] === '#' ? 1 : 0; }
  } else for (let gz = 0; gz < n; gz++) {
    for (let gx = 0; gx < n; gx++) {
      const x = -half + (gx + 0.5) * cell, z = -half + (gz + 0.5) * cell;
      for (const b of solids) {
        if (b.minY > 1.5) continue;
        if (x > b.minX - margin && x < b.maxX + margin && z > b.minZ - margin && z < b.maxZ + margin) { blocked[gz * n + gx] = 1; break; }
      }
    }
  }
  const cost = new Float32Array(n * n); // Zusatzkosten (z. B. Barrikaden: teuer, aber durchbrechbar)
  return {
    n, cell, half, blocked, cost,
    /** Setzt Zusatzkosten für alle Zellen, die eine der AABBs berühren. */
    setCosts(boxes, value, m = 0.3) {
      cost.fill(0);
      for (const b of boxes) {
        const gx0 = Math.max(0, Math.floor((b.minX - m + half) / cell)), gx1 = Math.min(n - 1, Math.floor((b.maxX + m + half) / cell));
        const gz0 = Math.max(0, Math.floor((b.minZ - m + half) / cell)), gz1 = Math.min(n - 1, Math.floor((b.maxZ + m + half) / cell));
        for (let gz = gz0; gz <= gz1; gz++) for (let gx = gx0; gx <= gx1; gx++) cost[gz * n + gx] = value;
      }
    },
    toCell(x, z) {
      const gx = Math.max(0, Math.min(n - 1, Math.floor((x + half) / cell)));
      const gz = Math.max(0, Math.min(n - 1, Math.floor((z + half) / cell)));
      return [gx, gz];
    },
    center(gx, gz) { return [-half + (gx + 0.5) * cell, -half + (gz + 0.5) * cell]; },
  };
}

const DIRS = [[1, 0, 1], [-1, 0, 1], [0, 1, 1], [0, -1, 1], [1, 1, 1.414], [1, -1, 1.414], [-1, 1, 1.414], [-1, -1, 1.414]];

/** Dijkstra-ähnliches Flussfeld (Bucket-Queue) von mehreren Quellen. Gibt Float64Array Distanzen zurück. */
export function flowField(grid, sources) {
  const { n, blocked, cost } = grid;
  const dist = new Float64Array(n * n).fill(Infinity);
  // einfache Binär-Heap-Prioritätswarteschlange
  const heap = [];
  const push = (d, i) => {
    heap.push([d, i]);
    let k = heap.length - 1;
    while (k > 0) { const p = (k - 1) >> 1; if (heap[p][0] <= heap[k][0]) break; [heap[p], heap[k]] = [heap[k], heap[p]]; k = p; }
  };
  const pop = () => {
    const top = heap[0], last = heap.pop();
    if (heap.length) {
      heap[0] = last; let k = 0;
      for (;;) {
        const l = 2 * k + 1, r = l + 1; let m = k;
        if (l < heap.length && heap[l][0] < heap[m][0]) m = l;
        if (r < heap.length && heap[r][0] < heap[m][0]) m = r;
        if (m === k) break;
        [heap[m], heap[k]] = [heap[k], heap[m]]; k = m;
      }
    }
    return top;
  };
  for (const [gx, gz] of sources) {
    const i = gz * n + gx;
    if (dist[i] > 0) { dist[i] = 0; push(0, i); }
  }
  while (heap.length) {
    const [d, i] = pop();
    if (d > dist[i]) continue;
    const gx = i % n, gz = (i / n) | 0;
    for (const [dx, dz, c] of DIRS) {
      const nx = gx + dx, nz = gz + dz;
      if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue;
      const ni = nz * n + nx;
      if (blocked[ni]) continue;
      if (dx && dz && (blocked[gz * n + nx] || blocked[nz * n + gx])) continue; // keine Ecken schneiden
      const nd = d + c + (cost ? cost[ni] : 0);
      if (nd < dist[ni]) { dist[ni] = nd; push(nd, ni); }
    }
  }
  return dist;
}

/** Sichtlinie im Raster (für Türme / direkte Verfolgung). */
export function gridLOS(grid, x1, z1, x2, z2) {
  const dx = x2 - x1, dz = z2 - z1;
  const len = Math.hypot(dx, dz);
  const steps = Math.ceil(len / (grid.cell * 0.5));
  for (let s = 1; s < steps; s++) {
    const t = s / steps;
    const [gx, gz] = grid.toCell(x1 + dx * t, z1 + dz * t);
    if (grid.blocked[gz * grid.n + gx]) return false;
  }
  return true;
}

export function dist2(ax, az, bx, bz) { const dx = ax - bx, dz = az - bz; return dx * dx + dz * dz; }

export const BARRICADE_SIZE = { w: 2.6, d: 0.5, h: 1.2 };
/** AABB einer Barrikade (rot 0 = entlang X, 1 = entlang Z) */
export function barricadeBox(b) {
  const w = b.rot ? BARRICADE_SIZE.d : BARRICADE_SIZE.w, d = b.rot ? BARRICADE_SIZE.w : BARRICADE_SIZE.d;
  return { minX: b.x - w / 2, maxX: b.x + w / 2, minZ: b.z - d / 2, maxZ: b.z + d / 2, minY: 0, maxY: BARRICADE_SIZE.h, kind: 'barricade', id: b.id };
}
