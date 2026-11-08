// Exportiert Karten und Spielwerte aus shared/ als JSON für den Unity-Client
import fs from 'node:fs';
import * as C from '../shared/config.js';
import { MAPS, MAP_ORDER } from '../shared/maps.js';
import { getSolids } from '../shared/geom.js';
const pick = (o, keys) => Object.fromEntries(keys.map(k => [k, o[k]]));
const cfg = pick(C, ['TICK_RATE', 'START_MONEY', 'PREP_TIME_FIRST', 'PREP_TIME', 'WAVE_BONUS', 'HIT_REWARD', 'PLAYER_MAX_HP', 'PLAYER_MAX_ARMOR', 'REVIVE_TIME', 'BLEEDOUT_TIME', 'SHOP_RADIUS', 'MAX_TURRETS', 'PLAYER_COLORS',
  'DIFFS', 'DIFF_ORDER', 'WEAPONS', 'WEAPON_ORDER', 'UPGRADE', 'TURRETS', 'TURRET_ORDER', 'BARRICADE', 'SHOVEL', 'BOARDS', 'GRENADE', 'MELEE', 'ITEMS', 'ITEM_ORDER', 'PERKS', 'PERK_ORDER', 'MAX_PERKS',
  'POWERUPS', 'POWERUP_ORDER', 'CRATE', 'ZOMBIES', 'ZOMBIE_TYPES', 'GENERATOR_HP', 'BODY', 'WAVE_KINDS']);
// Listen in Objekt-Arrays umwandeln (JsonUtility kann keine Dictionaries)
const asList = (obj, order) => order.map(id => ({ id, ...obj[id] }));
const out = {
  ...cfg,
  weapons: asList(C.WEAPONS, C.WEAPON_ORDER), turrets: asList(C.TURRETS, C.TURRET_ORDER), items: asList(C.ITEMS, C.ITEM_ORDER),
  perks: asList(C.PERKS, C.PERK_ORDER), powerups: asList(C.POWERUPS, C.POWERUP_ORDER), zombies: asList(C.ZOMBIES, C.ZOMBIE_TYPES), diffs: asList(C.DIFFS, C.DIFF_ORDER),
  maps: MAP_ORDER.map(id => { const m = MAPS[id]; return { ...m, solids: getSolids(m), night: !!m.night }; }),
};
fs.mkdirSync(new URL('../unity/Assets/Resources/', import.meta.url), { recursive: true });
fs.writeFileSync(new URL('../unity/Assets/Resources/gamedata.json', import.meta.url), JSON.stringify(out));
console.log('gamedata.json geschrieben:', Object.keys(out).length, 'Schlüssel,', out.maps.length, 'Karten');
