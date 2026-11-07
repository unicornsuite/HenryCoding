// Prozedurale Soundeffekte mit der WebAudio-API (keine Audiodateien nötig)

let ctx = null, master = null, noiseBuf = null;
let volume = 0.7;
const listener = { x: 0, z: 0 };

export function initAudio() {
  if (ctx) { if (ctx.state === 'suspended') ctx.resume(); return; }
  try {
    ctx = new (window.AudioContext || window.webkitAudioContext)();
    master = ctx.createGain(); master.gain.value = volume;
    const comp = ctx.createDynamicsCompressor();
    comp.threshold.value = -14; comp.ratio.value = 4;
    master.connect(comp); comp.connect(ctx.destination);
    noiseBuf = ctx.createBuffer(1, ctx.sampleRate * 1.5, ctx.sampleRate);
    const d = noiseBuf.getChannelData(0);
    for (let i = 0; i < d.length; i++) d[i] = Math.random() * 2 - 1;
  } catch { ctx = null; }
}
export function setVolume(v) { volume = v; if (master) master.gain.value = v; }
export function setListener(x, z) { listener.x = x; listener.z = z; }

function att(pos, range = 45) {
  if (!pos) return 1;
  const d = Math.hypot(pos.x - listener.x, pos.z - listener.z);
  return Math.max(0, 1 - d / range) ** 1.5;
}

function noise(t, dur, { freq = 1000, q = 0.7, type = 'lowpass', gain = 0.5, attack = 0.002, sweepTo = null } = {}) {
  const src = ctx.createBufferSource(); src.buffer = noiseBuf;
  const f = ctx.createBiquadFilter(); f.type = type; f.frequency.setValueAtTime(freq, t); f.Q.value = q;
  if (sweepTo) f.frequency.exponentialRampToValueAtTime(sweepTo, t + dur);
  const g = ctx.createGain();
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(gain, t + attack);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  src.connect(f); f.connect(g); g.connect(master);
  src.start(t, Math.random() * 0.5); src.stop(t + dur + 0.05);
}
function tone(t, dur, { freq = 440, to = null, type = 'sine', gain = 0.3, attack = 0.005 } = {}) {
  const o = ctx.createOscillator(); o.type = type; o.frequency.setValueAtTime(freq, t);
  if (to) o.frequency.exponentialRampToValueAtTime(to, t + dur);
  const g = ctx.createGain();
  g.gain.setValueAtTime(0.0001, t);
  g.gain.exponentialRampToValueAtTime(gain, t + attack);
  g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
  o.connect(g); g.connect(master);
  o.start(t); o.stop(t + dur + 0.05);
}

const GUN = {
  pistol:   { f: 2600, d: 0.16, g: 0.55, thump: 140 },
  smg:      { f: 3200, d: 0.1,  g: 0.4,  thump: 170 },
  shotgun:  { f: 1500, d: 0.38, g: 0.9,  thump: 80 },
  rifle:    { f: 2200, d: 0.18, g: 0.6,  thump: 110 },
  sniper:   { f: 1800, d: 0.55, g: 1.0,  thump: 70 },
  launcher: { f: 600,  d: 0.3,  g: 0.6,  thump: 60 },
  minigun:  { f: 3000, d: 0.08, g: 0.38, thump: 150 },
  mg:       { f: 2800, d: 0.09, g: 0.35, thump: 160 },
  cannon:   { f: 700,  d: 0.45, g: 0.9,  thump: 55 },
};

export const sfx = {
  shoot(type, pos) {
    if (!ctx) return; const a = att(pos); if (a <= 0.01) return;
    const c = GUN[type] || GUN.pistol, t = ctx.currentTime;
    noise(t, c.d, { freq: c.f, gain: c.g * a, sweepTo: 300 });
    tone(t, c.d * 0.8, { freq: c.thump, to: 40, type: 'triangle', gain: 0.5 * a });
    if (type === 'sniper') noise(t + 0.02, 0.8, { freq: 800, gain: 0.15 * a, sweepTo: 100 });
  },
  explosion(pos) {
    if (!ctx) return; const a = att(pos, 70); if (a <= 0.01) return; const t = ctx.currentTime;
    noise(t, 1.2, { freq: 900, gain: 1.0 * a, sweepTo: 60, attack: 0.005 });
    tone(t, 0.8, { freq: 90, to: 25, type: 'sine', gain: 0.9 * a });
  },
  hit() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.05, { freq: 1800, type: 'square', gain: 0.08 }); },
  headshot() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.18, { freq: 2400, to: 2200, type: 'sine', gain: 0.18 }); tone(t, 0.05, { freq: 1200, type: 'square', gain: 0.06 }); },
  kill() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.12, { freq: 900, to: 1500, type: 'triangle', gain: 0.15 }); },
  groan(pos, pitch = 1) {
    if (!ctx) return; const a = att(pos, 30); if (a <= 0.02) return; const t = ctx.currentTime;
    const o = ctx.createOscillator(); o.type = 'sawtooth';
    const base = (70 + Math.random() * 40) * pitch;
    o.frequency.setValueAtTime(base, t); o.frequency.linearRampToValueAtTime(base * 0.7, t + 0.9);
    const lfo = ctx.createOscillator(); lfo.frequency.value = 6 + Math.random() * 4;
    const lg = ctx.createGain(); lg.gain.value = 8; lfo.connect(lg); lg.connect(o.frequency);
    const f = ctx.createBiquadFilter(); f.type = 'bandpass'; f.frequency.value = 500 * pitch; f.Q.value = 2;
    const g = ctx.createGain(); g.gain.setValueAtTime(0.0001, t); g.gain.exponentialRampToValueAtTime(0.25 * a, t + 0.15); g.gain.exponentialRampToValueAtTime(0.0001, t + 1);
    o.connect(f); f.connect(g); g.connect(master);
    o.start(t); lfo.start(t); o.stop(t + 1.05); lfo.stop(t + 1.05);
  },
  hurt() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.25, { freq: 160, to: 60, type: 'square', gain: 0.25 }); noise(t, 0.15, { freq: 500, gain: 0.3 }); },
  reload() { if (!ctx) return; const t = ctx.currentTime; noise(t, 0.05, { freq: 3000, type: 'bandpass', q: 4, gain: 0.3 }); noise(t + 0.25, 0.06, { freq: 2200, type: 'bandpass', q: 4, gain: 0.35 }); },
  empty() { if (!ctx) return; const t = ctx.currentTime; noise(t, 0.04, { freq: 4000, type: 'bandpass', q: 6, gain: 0.25 }); },
  buy() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.12, { freq: 1046, type: 'square', gain: 0.1 }); tone(t + 0.1, 0.25, { freq: 1568, type: 'square', gain: 0.1 }); },
  deny() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.25, { freq: 180, type: 'square', gain: 0.12 }); },
  waveStart(kind = 'normal') {
    if (!ctx) return; const t = ctx.currentTime;
    const chord = kind === 'boss' ? [82.4, 98, 123.5] : kind === 'rush' ? [146.8, 174.6, 220] : [110, 138.6, 164.8];
    for (const f of chord) tone(t, kind === 'boss' ? 2.4 : 1.6, { freq: f, type: 'sawtooth', gain: 0.09, attack: 0.15 });
    if (kind === 'boss') tone(t + 0.3, 1.2, { freq: 55, to: 40, type: 'square', gain: 0.12 });
  },
  streak() { if (!ctx) return; const t = ctx.currentTime; [660, 880, 1320].forEach((f, i) => tone(t + i * 0.07, 0.2, { freq: f, type: 'square', gain: 0.1 })); },
  powerup() { if (!ctx) return; const t = ctx.currentTime; [523, 659, 784, 1046, 1318].forEach((f, i) => tone(t + i * 0.06, 0.3, { freq: f, type: 'triangle', gain: 0.14 })); noise(t, 0.4, { freq: 3000, type: 'bandpass', q: 2, gain: 0.1, sweepTo: 8000 }); },
  spit(pos) { if (!ctx) return; const a = att(pos, 30); if (a <= 0.02) return; const t = ctx.currentTime; noise(t, 0.3, { freq: 600, type: 'bandpass', q: 3, gain: 0.3 * a, sweepTo: 200 }); tone(t, 0.25, { freq: 300, to: 120, type: 'sawtooth', gain: 0.1 * a }); },
  crate(pos) { if (!ctx) return; const a = att(pos, 25); const t = ctx.currentTime; for (let i = 0; i < 10; i++) tone(t + i * 0.22, 0.12, { freq: 700 + (i % 3) * 200, type: 'square', gain: 0.07 * a }); },
  woodHit(pos) { if (!ctx) return; const a = att(pos, 30); if (a <= 0.02) return; const t = ctx.currentTime; noise(t, 0.08, { freq: 900, type: 'bandpass', q: 2, gain: 0.35 * a }); tone(t, 0.1, { freq: 180, to: 90, type: 'triangle', gain: 0.2 * a }); },
  woodBreak(pos) { if (!ctx) return; const a = att(pos, 40); if (a <= 0.02) return; const t = ctx.currentTime; for (let i = 0; i < 4; i++) noise(t + i * 0.05, 0.15, { freq: 600 + Math.random() * 800, type: 'bandpass', q: 1.5, gain: 0.35 * a }); tone(t, 0.3, { freq: 120, to: 50, type: 'triangle', gain: 0.3 * a }); },
  throw() { if (!ctx) return; const t = ctx.currentTime; noise(t, 0.15, { freq: 1200, type: 'bandpass', q: 1, gain: 0.2, sweepTo: 400 }); },
  swing() { if (!ctx) return; const t = ctx.currentTime; noise(t, 0.18, { freq: 2500, type: 'bandpass', q: 1.5, gain: 0.25, sweepTo: 600 }); },
  waveClear() { if (!ctx) return; const t = ctx.currentTime; [523, 659, 784, 1046].forEach((f, i) => tone(t + i * 0.1, 0.35, { freq: f, type: 'triangle', gain: 0.15 })); },
  zap(pos) { if (!ctx) return; const a = att(pos); if (a <= 0.01) return; const t = ctx.currentTime; tone(t, 0.25, { freq: 900 + Math.random() * 600, to: 120, type: 'sawtooth', gain: 0.2 * a }); noise(t, 0.2, { freq: 5000, type: 'highpass', gain: 0.25 * a }); },
  step() { if (!ctx) return; const t = ctx.currentTime; noise(t, 0.07, { freq: 400 + Math.random() * 200, gain: 0.08 }); },
  place() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.1, { freq: 300, type: 'square', gain: 0.15 }); noise(t, 0.2, { freq: 1200, gain: 0.3 }); tone(t + 0.12, 0.2, { freq: 600, type: 'triangle', gain: 0.12 }); },
  spin() { if (!ctx) return; const t = ctx.currentTime; tone(t, 0.5, { freq: 80, to: 400, type: 'sawtooth', gain: 0.06 }); },
};
