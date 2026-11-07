// Partikel, Leuchtspuren, Mündungsfeuer, Explosionen, Blitze, Schadenszahlen
import * as THREE from 'three';

const MAX_PARTS = 900;

function glowTexture() {
  const c = document.createElement('canvas'); c.width = c.height = 64;
  const g = c.getContext('2d');
  const grd = g.createRadialGradient(32, 32, 0, 32, 32, 32);
  grd.addColorStop(0, 'rgba(255,255,255,1)'); grd.addColorStop(0.25, 'rgba(255,230,150,0.9)'); grd.addColorStop(1, 'rgba(255,120,0,0)');
  g.fillStyle = grd; g.fillRect(0, 0, 64, 64);
  const t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
}

export class Effects {
  constructor(scene, camera) {
    this.scene = scene; this.camera = camera;
    // Partikel als InstancedMesh (Würfel)
    this.parts = [];
    const geo = new THREE.BoxGeometry(1, 1, 1);
    this.inst = new THREE.InstancedMesh(geo, new THREE.MeshLambertMaterial({ color: '#ffffff' }), MAX_PARTS);
    this.inst.instanceMatrix.setUsage(THREE.DynamicDrawUsage);
    this.inst.count = 0; this.inst.frustumCulled = false;
    this.inst.setColorAt(0, new THREE.Color());
    scene.add(this.inst);
    this.glowTex = glowTexture();
    // Leuchtspuren
    this.tracers = [];
    this.tracerGeo = new THREE.BoxGeometry(1, 1, 1); this.tracerGeo.translate(0, 0, 0.5);
    // Licht-Pool für Mündungsfeuer/Explosionen (feste Anzahl -> keine Shader-Neukompilierung)
    this.lights = [];
    for (let i = 0; i < 3; i++) { const l = new THREE.PointLight('#ffb74d', 0, 10, 2); scene.add(l); this.lights.push({ l, t: 0, max: 0 }); }
    this.lightIdx = 0;
    this.flashes = []; this.rings = []; this.bolts = []; this.projectiles = []; this.spits = []; this.pools = [];
    this.shake = 0;
    this.numLayer = document.getElementById('dmgnums');
    this.nums = [];
    this._m = new THREE.Matrix4(); this._q = new THREE.Quaternion(); this._v = new THREE.Vector3(); this._s = new THREE.Vector3(); this._c = new THREE.Color();
  }

  particle(pos, vel, color, size = 0.1, life = 0.8, gravity = 14) {
    if (this.parts.length >= MAX_PARTS) this.parts.shift();
    this.parts.push({ p: pos.clone(), v: vel.clone(), c: new THREE.Color(color), s: size, life, max: life, g: gravity, rot: Math.random() * 6 });
  }
  burst(pos, color, n = 8, speed = 3, size = 0.1, life = 0.7, gravity = 14, up = 1.5) {
    for (let i = 0; i < n; i++) {
      const v = new THREE.Vector3((Math.random() - 0.5) * speed, Math.random() * speed * 0.6 + up, (Math.random() - 0.5) * speed);
      this.particle(pos, v, color, size * (0.6 + Math.random() * 0.8), life * (0.6 + Math.random() * 0.6), gravity);
    }
  }
  blood(pos, dir, big = false) {
    const col = ['#4caf50', '#2e7d32', '#7cb342', '#8b0000'];
    for (let i = 0; i < (big ? 18 : 7); i++) {
      const v = new THREE.Vector3((Math.random() - 0.5) * 3, Math.random() * 3 + 0.5, (Math.random() - 0.5) * 3);
      if (dir) v.addScaledVector(dir, 2.5);
      this.particle(pos, v, col[(Math.random() * col.length) | 0], 0.07 + Math.random() * 0.08, 0.7, 12);
    }
  }
  sparks(pos, normal) {
    for (let i = 0; i < 6; i++) {
      const v = new THREE.Vector3((Math.random() - 0.5) * 4, Math.random() * 3, (Math.random() - 0.5) * 4);
      if (normal) v.addScaledVector(normal, 3);
      this.particle(pos, v, Math.random() > 0.5 ? '#ffe082' : '#9e9e9e', 0.05, 0.35, 10);
    }
  }
  flashLight(pos, color = '#ffb74d', intensity = 30, dur = 0.06, dist = 10) {
    const L = this.lights[this.lightIdx++ % this.lights.length];
    L.l.position.copy(pos); L.l.color.set(color); L.l.distance = dist; L.t = dur; L.max = dur; L.peak = intensity; L.l.intensity = intensity;
  }
  muzzle(pos, scale = 1, withLight = true) {
    const sp = new THREE.Sprite(new THREE.SpriteMaterial({ map: this.glowTex, color: '#ffd180', blending: THREE.AdditiveBlending, transparent: true, depthWrite: false }));
    sp.position.copy(pos); sp.scale.setScalar(0.6 * scale); sp.material.rotation = Math.random() * 6;
    this.scene.add(sp); this.flashes.push({ o: sp, t: 0.05, max: 0.05 });
    if (withLight) this.flashLight(pos, '#ffb74d', 14 * scale, 0.06, 8);
  }
  tracer(from, to, color = '#fff59d', width = 0.03, life = 0.07) {
    const m = new THREE.Mesh(this.tracerGeo, new THREE.MeshBasicMaterial({ color, transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }));
    const len = from.distanceTo(to);
    m.position.copy(from); m.lookAt(to); m.scale.set(width, width, len);
    this.scene.add(m); this.tracers.push({ o: m, t: life, max: life });
  }
  explosion(pos, radius = 4) {
    const sphere = new THREE.Mesh(new THREE.IcosahedronGeometry(1, 1), new THREE.MeshBasicMaterial({ color: '#ffab40', transparent: true, opacity: 0.9, blending: THREE.AdditiveBlending, depthWrite: false }));
    sphere.position.copy(pos); this.scene.add(sphere);
    this.rings.push({ o: sphere, t: 0.45, max: 0.45, r: radius });
    this.flashLight(pos, '#ff9100', 120, 0.35, radius * 5);
    this.burst(pos, '#ff6d00', 18, 9, 0.25, 0.8, 8, 3);
    this.burst(pos, '#ffd600', 10, 7, 0.2, 0.5, 6, 2);
    this.burst(pos, '#424242', 14, 4, 0.45, 1.6, -1.5, 1);
    const d = this.camera.position.distanceTo(pos);
    this.shake = Math.max(this.shake, Math.max(0, 1 - d / 25) * 0.6);
  }
  lightning(points, color = '#80d8ff') {
    const verts = [];
    for (let i = 0; i < points.length - 1; i++) {
      const a = points[i], b = points[i + 1];
      const segs = 6;
      let prev = a.clone();
      for (let s = 1; s <= segs; s++) {
        const p = a.clone().lerp(b, s / segs);
        if (s < segs) p.add(new THREE.Vector3((Math.random() - 0.5) * 0.6, (Math.random() - 0.5) * 0.6, (Math.random() - 0.5) * 0.6));
        verts.push(prev.x, prev.y, prev.z, p.x, p.y, p.z); prev = p;
      }
    }
    const g = new THREE.BufferGeometry(); g.setAttribute('position', new THREE.Float32BufferAttribute(verts, 3));
    const l = new THREE.LineSegments(g, new THREE.LineBasicMaterial({ color, transparent: true, blending: THREE.AdditiveBlending }));
    this.scene.add(l); this.bolts.push({ o: l, t: 0.18, max: 0.18 });
    for (const p of points.slice(1)) this.burst(p, '#b3e5fc', 4, 3, 0.06, 0.3, 4, 1);
    this.flashLight(points[0], '#40c4ff', 30, 0.15, 12);
  }
  /** Fliegendes Projektil, ruft step(from, to, pr) auf; opts: { fuse, bounce, color, size, gravity, trail } */
  projectile(pos, vel, step, opts = {}) {
    const m = new THREE.Mesh(opts.box ? new THREE.BoxGeometry(opts.size || 0.18, opts.size || 0.18, opts.size || 0.18) : new THREE.SphereGeometry(opts.size || 0.09, 8, 6),
      new THREE.MeshStandardMaterial({ color: opts.color || '#33691e', emissive: opts.emissive || '#76ff03', emissiveIntensity: 0.5 }));
    m.position.copy(pos); this.scene.add(m);
    this.projectiles.push({ o: m, v: vel.clone(), step, life: opts.fuse || 4, bounce: opts.bounce || 0, g: opts.gravity ?? 6, trail: opts.trail || '#bdbdbd', fuse: !!opts.fuse, rot: new THREE.Vector3(Math.random(), Math.random(), Math.random()) });
  }
  /** Säure-Spucke: fliegt im Bogen von a nach b in `dur` Sekunden */
  spit(a, b, dur = 0.7) {
    const m = new THREE.Mesh(new THREE.SphereGeometry(0.16, 8, 6), new THREE.MeshStandardMaterial({ color: '#aeea00', emissive: '#64dd17', emissiveIntensity: 1.2 }));
    this.scene.add(m);
    this.spits.push({ o: m, a: a.clone(), b: b.clone(), t: 0, dur });
  }
  acidPool(pos, r = 2) {
    const ring = new THREE.Mesh(new THREE.CircleGeometry(r, 20), new THREE.MeshBasicMaterial({ color: '#76ff03', transparent: true, opacity: 0.45, depthWrite: false }));
    ring.rotation.x = -Math.PI / 2; ring.position.set(pos.x, 0.04, pos.z); this.scene.add(ring);
    this.pools.push({ o: ring, t: 1.2, max: 1.2 });
    this.burst(new THREE.Vector3(pos.x, 0.2, pos.z), '#aeea00', 14, 4, 0.1, 0.6, 10, 2);
  }
  /** Körperteile beim Tod (Klötzchen in Figurfarben) */
  gibs(pos, colors, n = 10, speed = 5) {
    for (let i = 0; i < n; i++) {
      const v = new THREE.Vector3((Math.random() - 0.5) * speed, Math.random() * speed * 0.8 + 2, (Math.random() - 0.5) * speed);
      this.particle(pos, v, colors[i % colors.length], 0.16 + Math.random() * 0.14, 1.4 + Math.random() * 0.6, 16);
    }
  }
  textNumber(pos, text, cls = 'money', life = 1.1) { this._num(pos, text, cls, life); }
  damageNumber(pos, amount, head = false) { this._num(pos, String(Math.round(amount)), head ? 'head' : '', 0.8); }
  _num(pos, text, cls, life) {
    let el = this.nums.find(n => !n.active);
    if (!el) {
      if (this.nums.length > 40) return;
      const d = document.createElement('div'); d.className = 'dmgnum'; this.numLayer.appendChild(d);
      el = { el: d }; this.nums.push(el);
    }
    el.active = true; el.t = life; el.pos = pos.clone().add(new THREE.Vector3((Math.random() - 0.5) * 0.4, 0, (Math.random() - 0.5) * 0.4));
    el.el.textContent = text;
    el.el.className = 'dmgnum' + (cls ? ' ' + cls : '');
    el.el.style.display = 'block';
  }

  update(dt) {
    // Partikel
    const m = this._m, q = this._q, s = this._s;
    let n = 0;
    for (let i = this.parts.length - 1; i >= 0; i--) {
      const p = this.parts[i];
      p.life -= dt;
      if (p.life <= 0) { this.parts.splice(i, 1); continue; }
      p.v.y -= p.g * dt;
      p.p.addScaledVector(p.v, dt);
      if (p.p.y < 0.03) { p.p.y = 0.03; p.v.set(p.v.x * 0.5, 0, p.v.z * 0.5); }
      p.rot += dt * 5;
      const k = Math.min(1, p.life / p.max * 2);
      q.setFromAxisAngle(this._v.set(0.3, 1, 0.2).normalize(), p.rot);
      m.compose(p.p, q, s.setScalar(p.s * k));
      this.inst.setMatrixAt(n, m); this.inst.setColorAt(n, p.c);
      n++;
    }
    this.inst.count = n;
    this.inst.instanceMatrix.needsUpdate = true;
    if (this.inst.instanceColor) this.inst.instanceColor.needsUpdate = true;

    const fade = (list, fn) => {
      for (let i = list.length - 1; i >= 0; i--) {
        const f = list[i]; f.t -= dt;
        if (f.t <= 0) { this.scene.remove(f.o); f.o.material.dispose(); if (f.o.geometry !== this.tracerGeo && !f.o.isSprite) f.o.geometry.dispose(); list.splice(i, 1); continue; }
        fn(f, f.t / f.max);
      }
    };
    fade(this.flashes, (f, k) => { f.o.material.opacity = k; });
    fade(this.tracers, (f, k) => { f.o.material.opacity = k * 0.9; });
    fade(this.bolts, (f, k) => { f.o.material.opacity = k; });
    fade(this.rings, (f, k) => { f.o.scale.setScalar(f.r * (1 - k * k) + 0.3); f.o.material.opacity = k * 0.9; f.o.material.color.setHSL(0.08 * k, 1, 0.5); });
    for (const L of this.lights) { if (L.t > 0) { L.t -= dt; L.l.intensity = Math.max(0, L.t / L.max) * L.peak; } else L.l.intensity = 0; }

    for (let i = this.projectiles.length - 1; i >= 0; i--) {
      const pr = this.projectiles[i];
      pr.life -= dt;
      pr.v.y -= pr.g * dt;
      const from = pr.o.position.clone();
      const to = from.clone().addScaledVector(pr.v, dt);
      let done = pr.step(from, to, pr) || pr.life <= 0;
      if (!done && pr.bounce && to.y < 0.1) { to.y = 0.1; pr.v.y = Math.abs(pr.v.y) * pr.bounce; pr.v.x *= 0.6; pr.v.z *= 0.6; if (Math.abs(pr.v.y) < 0.5) pr.v.y = 0; }
      pr.o.position.copy(to);
      pr.o.rotation.x += pr.rot.x * dt * 8; pr.o.rotation.z += pr.rot.z * dt * 8;
      if (pr.trail && Math.random() < 0.7) this.particle(from, new THREE.Vector3(0, 0.5, 0), pr.trail, 0.1, 0.5, -1);
      if (pr.fuse && pr.life < 0.6) pr.o.material.emissiveIntensity = (Math.sin(pr.life * 40) > 0) ? 3 : 0.3;
      if (done) { this.scene.remove(pr.o); pr.o.geometry.dispose(); pr.o.material.dispose(); this.projectiles.splice(i, 1); }
    }
    for (let i = this.spits.length - 1; i >= 0; i--) {
      const sp = this.spits[i]; sp.t += dt;
      const k = Math.min(1, sp.t / sp.dur);
      sp.o.position.lerpVectors(sp.a, sp.b, k); sp.o.position.y += Math.sin(k * Math.PI) * 2.5;
      if (Math.random() < 0.5) this.particle(sp.o.position, new THREE.Vector3(0, 0.3, 0), '#aeea00', 0.06, 0.3, 2);
      if (k >= 1) { this.scene.remove(sp.o); sp.o.geometry.dispose(); sp.o.material.dispose(); this.spits.splice(i, 1); }
    }
    fade(this.pools, (f, k) => { f.o.material.opacity = k * 0.45; });

    // Schadenszahlen
    const w = innerWidth, h = innerHeight;
    for (const nn of this.nums) {
      if (!nn.active) continue;
      nn.t -= dt; nn.pos.y += dt * 1.2;
      if (nn.t <= 0) { nn.active = false; nn.el.style.display = 'none'; continue; }
      const v = this._v.copy(nn.pos).project(this.camera);
      if (v.z > 1) { nn.el.style.display = 'none'; continue; }
      nn.el.style.display = 'block';
      nn.el.style.transform = `translate(${(v.x * 0.5 + 0.5) * w}px, ${(-v.y * 0.5 + 0.5) * h}px) translate(-50%,-50%) scale(${0.7 + nn.t * 0.5})`;
      nn.el.style.opacity = Math.min(1, nn.t * 2.5);
    }
    this.shake = Math.max(0, this.shake - dt * 1.5);
  }

  clear() {
    for (const list of [this.flashes, this.tracers, this.bolts, this.rings, this.projectiles, this.spits, this.pools]) { for (const f of list) this.scene.remove(f.o); list.length = 0; }
    this.parts.length = 0; this.inst.count = 0;
    for (const n of this.nums) { n.active = false; n.el.style.display = 'none'; }
  }
}
