// Gemeinsame Spielwerte – werden von Server UND Client importiert.

export const TICK_RATE = 20;            // Server-Ticks pro Sekunde
export const START_MONEY = 500;
export const PREP_TIME_FIRST = 20;      // Sekunden bis Welle 1
export const PREP_TIME = 25;            // Pause zwischen Wellen
export const WAVE_BONUS = 250;          // Geld für jeden Spieler nach einer Welle
export const HIT_REWARD = 5;            // Geld pro Treffer
export const PLAYER_MAX_HP = 100;
export const PLAYER_MAX_ARMOR = 100;
export const REVIVE_TIME = 3;           // Sekunden E halten
export const BLEEDOUT_TIME = 30;        // Sekunden bis ein Verletzter stirbt
export const SHOP_RADIUS = 9;           // Abstand zum Shop-Terminal während einer Welle
export const MAX_TURRETS = 0; // 0 = kein Turmlimit
export const PLAYER_COLORS = ['#e53935', '#1e88e5', '#fdd835', '#8e24aa', '#fb8c00', '#00acc1', '#ec407a', '#7cb342'];

// Schwierigkeit (Lobby-Auswahl)
export const DIFFS = {
  easy:   { name: 'Entspannt', hp: 0.75, count: 0.8,  money: 1.25, desc: 'Weniger und schwächere Zombies, mehr Geld.' },
  normal: { name: 'Normal',    hp: 1.0,  count: 1.0,  money: 1.0,  desc: 'Die ausgewogene Erfahrung.' },
  hard:   { name: 'Albtraum',  hp: 1.35, count: 1.25, money: 0.9,  desc: 'Für Profis: zähe Horden, knappes Geld.' },
};
export const DIFF_ORDER = ['easy', 'normal', 'hard'];

// rate = Sekunden zwischen Schüssen
export const WEAPONS = {
  pistol:   { name: 'Pistole',            slot: 1, price: 0,    damage: 28,  rate: 0.22, mag: 12,  reserve: 9999, reload: 1.2, spread: 0.008, pellets: 1, auto: false, recoil: 0.025, pierce: 1, desc: 'Zuverlässig. Unendlich Reservemunition.' },
  smg:      { name: 'Maschinenpistole',   slot: 2, price: 750,  damage: 20,  rate: 0.07, mag: 32,  reserve: 192,  reload: 1.6, spread: 0.03,  pellets: 1, auto: true,  recoil: 0.012, pierce: 1, desc: 'Hohe Feuerrate, wenig Schaden pro Kugel.' },
  shotgun:  { name: 'Schrotflinte',       slot: 3, price: 1200, damage: 18,  rate: 0.75, mag: 6,   reserve: 42,   reload: 2.1, spread: 0.085, pellets: 9, auto: false, recoil: 0.09,  pierce: 1, desc: 'Verheerend auf kurze Distanz.' },
  rifle:    { name: 'Sturmgewehr',        slot: 4, price: 2000, damage: 38,  rate: 0.1,  mag: 30,  reserve: 240,  reload: 1.9, spread: 0.016, pellets: 1, auto: true,  recoil: 0.018, pierce: 2, desc: 'Allrounder mit Durchschlag.' },
  sniper:   { name: 'Scharfschützengewehr', slot: 5, price: 2800, damage: 220, rate: 1.1, mag: 5,   reserve: 35,   reload: 2.5, spread: 0.001, pellets: 1, auto: false, recoil: 0.12,  pierce: 5, zoom: true, desc: 'Durchschlägt 5 Zombies. Rechtsklick = Zielfernrohr.' },
  launcher: { name: 'Granatwerfer',       slot: 6, price: 3800, damage: 190, rate: 0.9,  mag: 4,   reserve: 24,   reload: 2.6, spread: 0.004, pellets: 1, auto: false, recoil: 0.14,  pierce: 1, splash: 4.5, desc: 'Explodiert beim Aufprall (Flächenschaden).' },
  minigun:  { name: 'Minigun',            slot: 7, price: 6000, damage: 26,  rate: 0.045, mag: 200, reserve: 800, reload: 4.0, spread: 0.04,  pellets: 1, auto: true,  recoil: 0.006, pierce: 2, spinup: 0.5, desc: 'Muss erst hochdrehen – dann gibt es kein Halten.' },
};
export const WEAPON_ORDER = ['pistol', 'smg', 'shotgun', 'rifle', 'sniper', 'launcher', 'minigun'];

// Waffen-Aufwertung (3 Stufen): mehr Schaden, größeres Magazin, Stufe 3 durchschlägt mehr
export const UPGRADE = {
  max: 3,
  cost: [0, 1200, 2500, 4500],
  dmg: [1, 1.3, 1.65, 2.1],
  mag: [1, 1.15, 1.3, 1.5],
  names: ['', '★', '★★', '★★★'],
  colors: ['#ffffff', '#40c4ff', '#d500f9', '#ffd600'],
};
/** Effektive Waffenwerte für Stufe `tier` und Perks (Set oder Array). */
export function weaponStats(id, tier = 0, perks = null) {
  const w = WEAPONS[id];
  const t = Math.max(0, Math.min(UPGRADE.max, tier | 0));
  const has = k => perks ? (perks.has ? perks.has(k) : perks.includes(k)) : false;
  return {
    ...w,
    damage: w.damage * UPGRADE.dmg[t],
    mag: Math.round(w.mag * UPGRADE.mag[t]),
    reserve: Math.round(w.reserve * (has('belt') ? 1.5 : 1)),
    pierce: w.pierce + (t >= 3 ? 1 : 0),
    reload: w.reload * (has('fast') ? 0.55 : 1),
    tier: t,
    name: w.name + (t ? ' ' + UPGRADE.names[t] : ''),
  };
}

export const TURRETS = {
  mg:     { name: 'MG-Turm',       price: 900,  damage: 14, rate: 0.13, range: 17, hp: 350, color: '#546e7a', desc: 'Schnelles Dauerfeuer auf einzelne Ziele.' },
  cannon: { name: 'Kanonenturm',   price: 2000, damage: 110, rate: 1.4, range: 21, hp: 550, splash: 3.2, color: '#6d4c41', desc: 'Langsame Granaten mit Flächenschaden.' },
  tesla:  { name: 'Tesla-Turm',    price: 2800, damage: 45, rate: 0.85, range: 12, hp: 450, chain: 5, color: '#3949ab', desc: 'Blitz springt auf bis zu 5 Zombies über.' },
};
export const TURRET_ORDER = ['mg', 'cannon', 'tesla'];
export const BARRICADE = { name: 'Barrikade', price: 350, hp: 500, max: 32, desc: 'Holzwall, den Zombies erst zerlegen müssen. Spieler springen drüber. Zombies suchen lieber einen Umweg, wenn es einen gibt.' };

// Schaufel: Werkzeug zum Graben von Gruben und Setzen von Brettern (Taste X in der Unity-Version)
export const SHOVEL = { name: 'Schaufel', price: 350, digTime: 2.2, reach: 3.2, pitRadius: 1.05, pitMax: 24, slow: 0.3, trapStun: 2.2, trapDamage: 45, hitDamage: 110, hitRange: 2.6, hitRate: 0.8, hitStun: 0.45,
  desc: 'Werkzeug (Taste X). Linksklick auf einen Zombie: kräftiger Schlag mit dem Blatt (wirft ihn kurz zurück). Linksklick auf den Boden: Grube graben – Zombies kommen darin kaum voran und meiden sie. Mit Brettern abgedeckt wird eine Grube zur Falle: Zombies brechen ein, bleiben stecken und nehmen Schaden.' };
export const BOARDS = { name: 'Bretter ×4', price: 140, pack: 4, max: 20, hp: 160, boardMax: 40,
  desc: '4 Bretter für die Schaufel (Rechtsklick): leichter Holzzaun, den Zombies einschlagen müssen – oder über eine Grube gelegt als Falle.' };

export const GRENADE = { name: 'Handgranaten', price: 300, pack: 3, max: 6, damage: 240, splash: 5, fuse: 1.8, desc: '3 Stück. Werfen mit G, hüpfen und explodieren nach kurzer Zeit.' };
export const MELEE = { name: 'Messer', damage: 70, range: 2.3, rate: 0.55, desc: 'Nahkampf mit V – spart Munition.' };

export const ITEMS = {
  ammo:   { name: 'Munitionskiste',   price: 250,  desc: 'Füllt die Munition aller Waffen auf.' },
  nades:  { name: 'Handgranaten ×3',  price: GRENADE.price, desc: GRENADE.desc },
  shovel: { name: SHOVEL.name,        price: SHOVEL.price, desc: SHOVEL.desc, unityOnly: true },
  boards: { name: BOARDS.name,        price: BOARDS.price, desc: BOARDS.desc, unityOnly: true },
  heal:   { name: 'Medikit',          price: 200,  desc: 'Heilt dich vollständig.' },
  armor:  { name: 'Schutzweste',      price: 450,  desc: '+50 Panzerung (max. 100). Fängt 60% Schaden ab.' },
  repair: { name: 'Generator reparieren', price: 600, desc: 'Repariert den Generator um 30%.' },
  fixturrets: { name: 'Türme reparieren', price: 400, desc: 'Repariert alle deine Türme vollständig.' },
  fixbarr: { name: 'Barrikaden reparieren', price: 300, desc: 'Repariert alle Barrikaden des Teams vollständig.' },
};
export const ITEM_ORDER = ['ammo', 'nades', 'shovel', 'boards', 'heal', 'armor', 'repair', 'fixturrets', 'fixbarr'];

// Perks: einmal pro Spiel kaufen, bleiben bis zum Ende
export const PERKS = {
  tough:  { name: 'Dickhäuter',   price: 2500, icon: '🛡', desc: '+50 maximale Lebenspunkte (150 statt 100).' },
  fast:   { name: 'Turbo-Lader',  price: 2000, icon: '⚡', desc: 'Nachladen fast doppelt so schnell.' },
  sprint: { name: 'Sprinter',     price: 1500, icon: '👟', desc: '+15% Lauf- und Sprinttempo.' },
  medic:  { name: 'Sanitäter',    price: 1500, icon: '➕', desc: 'Wiederbeleben doppelt so schnell, Wiederbelebte starten mit 80 LP. Allein: +1 Extra-Leben.' },
  lucky:  { name: 'Glückspilz',   price: 3000, icon: '🍀', desc: '+25% Geld pro Kill, Power-ups fallen für dich doppelt so oft.' },
  belt:   { name: 'Munitionsgurt', price: 1200, icon: '🎒', desc: '+50% Reservemunition für alle Waffen.' },
};
export const PERK_ORDER = ['tough', 'fast', 'sprint', 'medic', 'lucky', 'belt'];
export const MAX_PERKS = 4;

// Power-ups, die Zombies fallen lassen (gelten für das ganze Team)
export const POWERUPS = {
  instakill: { name: 'INSTA-KILL',       icon: '☠', color: '#ff1744', dur: 18, desc: 'Jeder Treffer tötet sofort!' },
  double:    { name: 'DOPPELTE PUNKTE',  icon: '✖2', color: '#ffd600', dur: 30, desc: 'Doppeltes Geld für alles.' },
  nuke:      { name: 'NUKE',             icon: '☢', color: '#76ff03', dur: 0,  desc: 'Alle Zombies vernichtet! +$400 für alle.' },
  ammo:      { name: 'MAX-MUNITION',     icon: '📦', color: '#40c4ff', dur: 0,  desc: 'Munition und Granaten aufgefüllt.' },
  freeze:    { name: 'EISZEIT',          icon: '❄', color: '#80d8ff', dur: 12, desc: 'Zombies sind eingefroren und langsam.' },
};
export const POWERUP_ORDER = ['instakill', 'double', 'nuke', 'ammo', 'freeze'];
export const POWERUP_DROP = 0.035;     // Chance pro Kill
export const POWERUP_LIFE = 22;        // Sekunden am Boden

// Glückskiste: Zufallswaffe
export const CRATE = { price: 950, spin: 2.6, desc: 'Zufällige Waffe – mit Glück sogar aufgewertet. Niete gibt Munition.' };

export const ZOMBIES = {
  walker:   { name: 'Zombie',       hp: 110,  speed: 2.3, damage: 10, reward: 60,   scale: 1.0, radius: 0.45, skin: '#7cb342', shirt: '#5d4037', pants: '#37474f', eyes: '#ff1744' },
  runner:   { name: 'Renner',       hp: 70,   speed: 4.6, damage: 7,  reward: 75,   scale: 0.92, radius: 0.4, skin: '#9ccc65', shirt: '#b71c1c', pants: '#263238', eyes: '#ffea00' },
  brute:    { name: 'Brocken',      hp: 750,  speed: 1.7, damage: 28, reward: 260,  scale: 1.55, radius: 0.75, skin: '#558b2f', shirt: '#455a64', pants: '#3e2723', eyes: '#ff1744' },
  exploder: { name: 'Explodierer',  hp: 90,   speed: 3.3, damage: 45, reward: 90,   scale: 1.05, radius: 0.5, skin: '#c0ca33', shirt: '#ff6f00', pants: '#bf360c', eyes: '#ffffff', explode: 3.8 },
  boss:     { name: 'Koloss',       hp: 5000, speed: 2.1, damage: 45, reward: 1500, scale: 2.6, radius: 1.15, skin: '#33691e', shirt: '#212121', pants: '#1b1b1b', eyes: '#d500f9' },
  crawler:  { name: 'Kriecher',     hp: 55,   speed: 5.4, damage: 8,  reward: 70,   scale: 0.8, radius: 0.38, skin: '#a5d6a7', shirt: '#4e342e', pants: '#4e342e', eyes: '#ff9100', crawl: true },
  spitter:  { name: 'Spucker',      hp: 160,  speed: 2.0, damage: 14, reward: 120,  scale: 1.1, radius: 0.48, skin: '#9e9d24', shirt: '#33691e', pants: '#1b5e20', eyes: '#76ff03', spit: { min: 6, max: 15, cd: 2.6, travel: 0.7, radius: 2.0 } },
};
export const ZOMBIE_TYPES = ['walker', 'runner', 'brute', 'exploder', 'boss', 'crawler', 'spitter'];

export const GENERATOR_HP = 2500;
export const GENERATOR_RADIUS = 1.6;

// Charakter-Proportionen (Klötzchen-Stil), alles in Metern bei Skala 1
export const BODY = {
  legH: 0.85, legW: 0.38,
  torsoH: 0.8, torsoW: 0.8, torsoD: 0.42,
  armH: 0.8, armW: 0.36,
  headS: 0.5,
  eye: 1.75, // Augenhöhe der Kamera
};

// Wellen-Art: normal, Boss (alle 5), Blitzwelle (alle 3), Horde (ab 8, alle 4)
export function waveKind(wave) {
  if (wave % 5 === 0) return 'boss';
  if (wave % 3 === 0) return 'rush';
  if (wave >= 8 && wave % 4 === 0) return 'horde';
  return 'normal';
}
export const WAVE_KINDS = {
  normal: { name: '', sub: 'Die Zombies kommen!' },
  boss:   { name: 'BOSSWELLE', sub: '⚠ Ein <b style="color:#d500f9">KOLOSS</b> nähert sich!' },
  rush:   { name: 'BLITZWELLE', sub: '⚡ Nur Renner und Kriecher – bleibt zusammen!' },
  horde:  { name: 'HORDE', sub: '🧟 Doppelt so viele Zombies. Spart Munition nicht!' },
};

// Wellen-Zusammensetzung
export function waveComposition(wave, players, diff = 1) {
  const p = Math.max(1, players);
  const kind = waveKind(wave);
  let total = Math.round((6 + wave * 3.5) * (1 + 0.55 * (p - 1)) * diff);
  if (kind === 'horde') total = Math.round(total * 1.8);
  if (kind === 'rush') total = Math.round(total * 1.3);
  const list = [];
  const bosses = kind === 'boss' ? Math.floor(wave / 5) : 0;
  for (let i = 0; i < bosses; i++) list.push('boss');
  for (let i = 0; i < total; i++) {
    const r = Math.random();
    let t = 'walker';
    if (kind === 'rush') t = r < 0.45 ? 'crawler' : 'runner';
    else if (kind === 'horde') t = r < 0.1 && wave >= 6 ? 'spitter' : r < 0.2 ? 'runner' : 'walker';
    else if (wave >= 5 && r < Math.min(0.14, 0.02 * (wave - 3))) t = 'brute';
    else if (wave >= 6 && r < 0.14 + Math.min(0.1, 0.02 * (wave - 5))) t = 'spitter';
    else if (wave >= 4 && r < 0.3) t = 'exploder';
    else if (wave >= 3 && r < 0.3 + Math.min(0.12, 0.03 * wave)) t = 'crawler';
    else if (wave >= 2 && r < 0.42 + Math.min(0.3, 0.06 * wave)) t = 'runner';
    list.push(t);
  }
  // mischen (Bosse bleiben ungefähr in der Mitte)
  for (let i = list.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [list[i], list[j]] = [list[j], list[i]];
  }
  return list;
}
export function waveHpMult(wave) { return 1 + (wave - 1) * 0.14 + Math.max(0, wave - 10) * 0.08; }
export function waveSpeedMult(wave) { return Math.min(1.35, 1 + (wave - 1) * 0.02); }
export function spawnInterval(wave) { return Math.max(0.3, 1.6 - wave * 0.09) * (waveKind(wave) === 'rush' ? 0.6 : 1); }
export function maxAlive(players) { return Math.min(80, 30 + players * 10); }
