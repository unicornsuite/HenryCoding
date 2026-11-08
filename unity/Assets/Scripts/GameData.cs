// Lädt gamedata.json (aus shared/ exportiert) und bietet typisierte Zugriffe
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class WeaponDef
    {
        public string id, name, desc; public int slot, price, mag, reserve, pellets, pierce, tier; public float damage, rate, reload, spread, recoil, splash, spinup; public bool auto, zoom;
        public WeaponDef Upgraded(int tier, HashSet<string> perks)
        {
            var u = GameData.I.upgrade; tier = Mathf.Clamp(tier, 0, GameData.I.upgradeMax);
            var w = (WeaponDef)MemberwiseClone();
            w.damage = damage * u.dmg[tier]; w.mag = Mathf.RoundToInt(mag * u.mag[tier]);
            w.reserve = Mathf.RoundToInt(reserve * (perks != null && perks.Contains("belt") ? 1.5f : 1f));
            w.pierce = pierce + (tier >= 3 ? 1 : 0);
            w.reload = reload * (perks != null && perks.Contains("fast") ? 0.55f : 1f);
            w.name = name + (tier > 0 ? " " + u.names[tier] : ""); w.tier = tier;
            return w;
        }
    }
    public class UpgradeDef { public int[] cost; public float[] dmg, mag; public string[] names, colors; }
    public class TurretDef { public string id, name, desc, color; public int price; public float damage, rate, range, hp, splash; public int chain; }
    public class ZombieDef { public string id, name, skin, shirt, pants, eyes; public float hp, speed, damage, scale, radius, explode; public int reward; public bool crawl, spit; }
    public class PowerupDef { public string id, name, icon, color, desc; public float dur; }
    public class PerkDef { public string id, name, icon, desc; public int price; }
    public class ItemDef { public string id, name, desc; public int price; }
    public class DiffDef { public string id, name, desc; }
    public class MapObj { public float x, z, w, d, h, y; public string color, kind, roof; public bool solid; public Dictionary<string, object> raw; }
    public class Solid { public float minX, maxX, minZ, maxZ, minY, maxY; public string kind; }
    public class MapDef
    {
        public string id, name, desc, ground, border, sky, hemiGround; public float size, ambient; public bool night;
        public Color fogColor; public float fogNear, fogFar; public Color sunColor; public float sunIntensity; public Vector3 sunPos;
        public Vector2 gen, shop, spawn; public Vector2? crate; public List<Vector2> zspawns = new List<Vector2>();
        public List<MapObj> objects = new List<MapObj>(); public List<MapObj> decals = new List<MapObj>(); public List<Solid> solids = new List<Solid>();
    }

    public class GameData
    {
        public static GameData I;
        public Dictionary<string, object> raw;
        public int tickRate, startMoney, playerMaxHp, playerMaxArmor, maxTurrets, maxPerks, generatorHp;
        public float reviveTime, bleedoutTime, shopRadius;
        public List<string> playerColors = new List<string>();
        public List<WeaponDef> weapons = new List<WeaponDef>(); public Dictionary<string, WeaponDef> weapon = new Dictionary<string, WeaponDef>();
        public List<TurretDef> turrets = new List<TurretDef>(); public Dictionary<string, TurretDef> turret = new Dictionary<string, TurretDef>();
        public List<ZombieDef> zombies = new List<ZombieDef>(); public Dictionary<string, ZombieDef> zombie = new Dictionary<string, ZombieDef>();
        public List<PowerupDef> powerups = new List<PowerupDef>(); public Dictionary<string, PowerupDef> powerup = new Dictionary<string, PowerupDef>();
        public List<PerkDef> perks = new List<PerkDef>(); public Dictionary<string, PerkDef> perk = new Dictionary<string, PerkDef>();
        public List<ItemDef> items = new List<ItemDef>();
        public List<DiffDef> diffs = new List<DiffDef>();
        public List<MapDef> maps = new List<MapDef>(); public Dictionary<string, MapDef> map = new Dictionary<string, MapDef>();
        public UpgradeDef upgrade; public int upgradeMax;
        public int barricadePrice, barricadeMax; public float barricadeHp; public string barricadeName, barricadeDesc;
        public int shovelPrice, pitMax; public float digTime, shovelReach, pitRadius, shovelHitDamage, shovelHitRange, shovelHitRate; public string shovelName, shovelDesc;
        public int boardsPrice, boardsPack, boardsMax, boardMax; public float boardHp; public string boardsName, boardsDesc;
        public int grenadePrice, grenadeMax, grenadePack; public float grenadeDamage, grenadeSplash, grenadeFuse;
        public float meleeDamage, meleeRange, meleeRate;
        public int cratePrice; public float crateSpin; public string crateDesc;
        public Dictionary<string, (string name, string sub)> waveKinds = new Dictionary<string, (string, string)>();
        public float bodyLegH, bodyLegW, bodyTorsoH, bodyTorsoW, bodyTorsoD, bodyArmH, bodyArmW, bodyHeadS, bodyEye;

        public static void Load()
        {
            var ta = Resources.Load<TextAsset>("gamedata");
            if (ta == null) { Debug.LogError("gamedata.json fehlt in Assets/Resources – bitte `node tools/export-unity.mjs` ausführen."); return; }
            I = new GameData(); I.Parse(J.Obj(MiniJson.Parse(ta.text)));
        }

        void Parse(Dictionary<string, object> r)
        {
            raw = r;
            tickRate = J.I(r, "TICK_RATE", 20); startMoney = J.I(r, "START_MONEY", 500); playerMaxHp = J.I(r, "PLAYER_MAX_HP", 100); playerMaxArmor = J.I(r, "PLAYER_MAX_ARMOR", 100);
            maxTurrets = J.I(r, "MAX_TURRETS", 16); maxPerks = J.I(r, "MAX_PERKS", 4); generatorHp = J.I(r, "GENERATOR_HP", 2500);
            reviveTime = J.F(r, "REVIVE_TIME", 3); bleedoutTime = J.F(r, "BLEEDOUT_TIME", 30); shopRadius = J.F(r, "SHOP_RADIUS", 9);
            foreach (var c in J.A(r, "PLAYER_COLORS")) playerColors.Add(c.ToString());
            var up = J.O(r, "UPGRADE");
            upgrade = new UpgradeDef { cost = ToInts(J.A(up, "cost")), dmg = ToFloats(J.A(up, "dmg")), mag = ToFloats(J.A(up, "mag")), names = ToStrings(J.A(up, "names")), colors = ToStrings(J.A(up, "colors")) };
            upgradeMax = J.I(up, "max", 3);
            foreach (var o in J.A(r, "weapons"))
            {
                var w = new WeaponDef { id = J.S(o, "id"), name = J.S(o, "name"), desc = J.S(o, "desc"), slot = J.I(o, "slot"), price = J.I(o, "price"), mag = J.I(o, "mag"), reserve = J.I(o, "reserve"), pellets = J.I(o, "pellets", 1), pierce = J.I(o, "pierce", 1), damage = J.F(o, "damage"), rate = J.F(o, "rate"), reload = J.F(o, "reload"), spread = J.F(o, "spread"), recoil = J.F(o, "recoil"), splash = J.F(o, "splash"), spinup = J.F(o, "spinup"), auto = J.B(o, "auto"), zoom = J.B(o, "zoom") };
                weapons.Add(w); weapon[w.id] = w;
            }
            foreach (var o in J.A(r, "turrets"))
            {
                var t = new TurretDef { id = J.S(o, "id"), name = J.S(o, "name"), desc = J.S(o, "desc"), color = J.S(o, "color"), price = J.I(o, "price"), damage = J.F(o, "damage"), rate = J.F(o, "rate"), range = J.F(o, "range"), hp = J.F(o, "hp"), splash = J.F(o, "splash"), chain = J.I(o, "chain") };
                turrets.Add(t); turret[t.id] = t;
            }
            foreach (var o in J.A(r, "zombies"))
            {
                var z = new ZombieDef { id = J.S(o, "id"), name = J.S(o, "name"), skin = J.S(o, "skin"), shirt = J.S(o, "shirt"), pants = J.S(o, "pants"), eyes = J.S(o, "eyes"), hp = J.F(o, "hp"), speed = J.F(o, "speed"), damage = J.F(o, "damage"), scale = J.F(o, "scale", 1), radius = J.F(o, "radius", 0.45f), explode = J.F(o, "explode"), reward = J.I(o, "reward"), crawl = J.B(o, "crawl"), spit = J.Has(o, "spit") };
                zombies.Add(z); zombie[z.id] = z;
            }
            foreach (var o in J.A(r, "powerups")) { var p = new PowerupDef { id = J.S(o, "id"), name = J.S(o, "name"), icon = J.S(o, "icon"), color = J.S(o, "color"), desc = J.S(o, "desc"), dur = J.F(o, "dur") }; powerups.Add(p); powerup[p.id] = p; }
            foreach (var o in J.A(r, "perks")) { var p = new PerkDef { id = J.S(o, "id"), name = J.S(o, "name"), icon = J.S(o, "icon"), desc = J.S(o, "desc"), price = J.I(o, "price") }; perks.Add(p); perk[p.id] = p; }
            foreach (var o in J.A(r, "items")) items.Add(new ItemDef { id = J.S(o, "id"), name = J.S(o, "name"), desc = J.S(o, "desc"), price = J.I(o, "price") });
            foreach (var o in J.A(r, "diffs")) diffs.Add(new DiffDef { id = J.S(o, "id"), name = J.S(o, "name"), desc = J.S(o, "desc") });
            var b = J.O(r, "BARRICADE"); barricadePrice = J.I(b, "price"); barricadeMax = J.I(b, "max"); barricadeHp = J.F(b, "hp"); barricadeName = J.S(b, "name"); barricadeDesc = J.S(b, "desc");
            var sh = J.O(r, "SHOVEL"); if (sh != null) { shovelPrice = J.I(sh, "price", 350); digTime = J.F(sh, "digTime", 2.2f); shovelReach = J.F(sh, "reach", 3.2f); pitRadius = J.F(sh, "pitRadius", 1.05f); pitMax = J.I(sh, "pitMax", 24); shovelName = J.S(sh, "name", "Schaufel"); shovelDesc = J.S(sh, "desc"); shovelHitDamage = J.F(sh, "hitDamage", 110); shovelHitRange = J.F(sh, "hitRange", 2.6f); shovelHitRate = J.F(sh, "hitRate", 0.8f); }
            var bo = J.O(r, "BOARDS"); if (bo != null) { boardsPrice = J.I(bo, "price", 160); boardsPack = J.I(bo, "pack", 4); boardsMax = J.I(bo, "max", 20); boardMax = J.I(bo, "boardMax", 40); boardHp = J.F(bo, "hp", 160); boardsName = J.S(bo, "name", "Bretter"); boardsDesc = J.S(bo, "desc"); }
            var g = J.O(r, "GRENADE"); grenadePrice = J.I(g, "price"); grenadeMax = J.I(g, "max"); grenadePack = J.I(g, "pack"); grenadeDamage = J.F(g, "damage"); grenadeSplash = J.F(g, "splash"); grenadeFuse = J.F(g, "fuse");
            var m = J.O(r, "MELEE"); meleeDamage = J.F(m, "damage"); meleeRange = J.F(m, "range"); meleeRate = J.F(m, "rate");
            var cr = J.O(r, "CRATE"); cratePrice = J.I(cr, "price"); crateSpin = J.F(cr, "spin"); crateDesc = J.S(cr, "desc");
            var wk = J.O(r, "WAVE_KINDS"); if (wk != null) foreach (var kv in wk) waveKinds[kv.Key] = (J.S(kv.Value, "name"), J.S(kv.Value, "sub"));
            var body = J.O(r, "BODY"); bodyLegH = J.F(body, "legH"); bodyLegW = J.F(body, "legW"); bodyTorsoH = J.F(body, "torsoH"); bodyTorsoW = J.F(body, "torsoW"); bodyTorsoD = J.F(body, "torsoD"); bodyArmH = J.F(body, "armH"); bodyArmW = J.F(body, "armW"); bodyHeadS = J.F(body, "headS"); bodyEye = J.F(body, "eye", 1.75f);
            foreach (var o in J.A(r, "maps"))
            {
                var md = new MapDef { id = J.S(o, "id"), name = J.S(o, "name"), desc = J.S(o, "desc"), ground = J.S(o, "ground"), border = J.S(o, "border"), sky = J.S(o, "sky"), hemiGround = J.S(o, "hemiGround"), size = J.F(o, "size"), ambient = J.F(o, "ambient", 1), night = J.B(o, "night") };
                var fog = J.A(o, "fog"); md.fogColor = Col(fog[0].ToString()); md.fogNear = J.F(fog, 1); md.fogFar = J.F(fog, 2);
                var sun = J.O(o, "sun"); md.sunColor = Col(J.S(sun, "color", "#ffffff")); md.sunIntensity = J.F(sun, "intensity", 1); var sp = J.A(sun, "pos"); md.sunPos = new Vector3(J.F(sp, 0), J.F(sp, 1), J.F(sp, 2));
                md.gen = V2(J.O(o, "gen")); md.shop = V2(J.O(o, "shop")); md.spawn = V2(J.O(o, "spawn")); if (J.Has(o, "crate")) md.crate = V2(J.O(o, "crate"));
                foreach (var zs in J.A(o, "zspawns")) { var a = J.Arr(zs); md.zspawns.Add(new Vector2(J.F(a, 0), J.F(a, 1))); }
                foreach (var ob in J.A(o, "objects")) md.objects.Add(Obj(ob));
                foreach (var ob in J.A(o, "decals")) md.decals.Add(Obj(ob));
                foreach (var s in J.A(o, "solids")) md.solids.Add(new Solid { minX = J.F(s, "minX"), maxX = J.F(s, "maxX"), minZ = J.F(s, "minZ"), maxZ = J.F(s, "maxZ"), minY = J.F(s, "minY"), maxY = J.F(s, "maxY"), kind = J.S(s, "kind") });
                maps.Add(md); map[md.id] = md;
            }
        }

        static MapObj Obj(object o) => new MapObj { x = J.F(o, "x"), z = J.F(o, "z"), w = J.F(o, "w"), d = J.F(o, "d"), h = J.F(o, "h"), y = J.F(o, "y"), color = J.S(o, "color", "#888888"), kind = J.S(o, "kind"), roof = J.S(o, "roof", null), solid = J.B(o, "solid", true), raw = J.Obj(o) };
        static Vector2 V2(object o) => new Vector2(J.F(o, "x"), J.F(o, "z"));
        static int[] ToInts(List<object> a) { var r = new int[a.Count]; for (int i = 0; i < a.Count; i++) r[i] = J.I(a, i); return r; }
        static float[] ToFloats(List<object> a) { var r = new float[a.Count]; for (int i = 0; i < a.Count; i++) r[i] = J.F(a, i); return r; }
        static string[] ToStrings(List<object> a) { var r = new string[a.Count]; for (int i = 0; i < a.Count; i++) r[i] = a[i]?.ToString() ?? ""; return r; }

        public static Color Col(string hex, float alpha = 1f)
        {
            if (string.IsNullOrEmpty(hex)) return Color.magenta;
            if (ColorUtility.TryParseHtmlString(hex, out var c)) { c.a = alpha; return c; }
            return Color.magenta;
        }

        public string WeaponAt(int index) => index >= 0 && index < weapons.Count ? weapons[index].id : "pistol";
        public int WeaponIndex(string id) => weapons.FindIndex(w => w.id == id);
    }
}
