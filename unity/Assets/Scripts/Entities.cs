// Spielobjekte, die vom Server kommen: Mitspieler, Zombies, Türme, Barrikaden, Power-ups
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class ZombieHitbox : MonoBehaviour { public Zombie z; public bool head; }

    public class Zombie
    {
        public int id; public string type; public ZombieDef cfg; public CharRig rig; public GameObject go;
        public float x, z, rot, tx, tz, trot, hp = 100, phase, seed, atkT, flash, rise, groanT, walk, deathT, bigDeath = -99, flinch, lastHp = 100;
        public bool dying, wasFrozen, headless; public float spurtT, poolT = -1, poolSize;
        public HealthBar bar; public List<Collider> boxes = new List<Collider>();
        // realistischer Zombie (gekauftes Modell + Animator)
        public bool real; public Animator anim; public List<Material> mats = new List<Material>(); public Material glowMat; public float lastAtk, spitTrig, hitCd; public Transform headBone;
    }
    public class Remote
    {
        public int id; public string name, color; public CharRig rig; public GameObject go; public TextMesh label, help;
        public float x, y, z, yaw, tx, ty, tz, tyaw, pitch, phase, meleeAnim; public string state = "alive", weapon = "pistol"; public int hp = 100, maxHp = 100, money, kills, moving, tier, melee; public float rev; public bool ready;
        public Models.GunParts gun; public string gunType = "";
    }
    public class Turret { public int id; public string type; public Models.TurretParts parts; public float x, z, yaw, tyaw, hp = 100; public int owner, muzzleIdx; public HealthBar bar; public Collider col; }
    public class Barricade { public int id; public GameObject go; public float x, z, hp = 100, shakeT; public int rot, kind, pit; public Quaternion baseRot = Quaternion.identity; public HealthBar bar; public BoxCollider col; }
    public class Pit { public int id; public GameObject go; public float x, z; public bool covered; }
    public class Pickup { public int id; public string kind; public PowerupDef cfg; public Models.PickupParts parts; public float x, z, t = 99, seed; }

    /// <summary>Verwaltet alle Server-Entitäten im Client</summary>
    public class Entities
    {
        public readonly Dictionary<int, Zombie> zombies = new Dictionary<int, Zombie>();
        public readonly Dictionary<int, Remote> remotes = new Dictionary<int, Remote>();
        public readonly Dictionary<int, Turret> turrets = new Dictionary<int, Turret>();
        public readonly Dictionary<int, Barricade> barricades = new Dictionary<int, Barricade>();
        public readonly Dictionary<int, Pickup> pickups = new Dictionary<int, Pickup>();
        public Transform root;
        readonly Effects fx; readonly Sfx sfx; readonly Camera cam;
        public bool frozen; public float T; public bool highQuality;
        public System.Func<int, (string name, string color)> playerInfo;
        static GameData D => GameData.I;

        public Entities(Effects fx, Sfx sfx, Camera cam) { this.fx = fx; this.sfx = sfx; this.cam = cam; root = new GameObject("Entities").transform; }

        // ------------------------------------------------------------------ Zombies
        static readonly Dictionary<string, string> Variant = new Dictionary<string, string> { ["walker"] = "Default", ["runner"] = "Yellow", ["crawler"] = "Green", ["spitter"] = "Green", ["exploder"] = "Yellow", ["brute"] = "Blue", ["boss"] = "Blue" };
        static readonly Dictionary<string, Color> Tint = new Dictionary<string, Color> { ["walker"] = Color.white, ["runner"] = new Color(1f, 0.95f, 0.85f), ["crawler"] = new Color(0.85f, 1f, 0.85f), ["spitter"] = new Color(0.8f, 1f, 0.6f), ["exploder"] = new Color(1f, 0.75f, 0.5f), ["brute"] = new Color(0.75f, 0.75f, 0.8f), ["boss"] = new Color(0.6f, 0.5f, 0.75f) };
        static bool? realAvailable;
        public static bool RealZombies { get { if (realAvailable == null) realAvailable = Resources.Load<GameObject>("Generated/Zombie_Default") != null; return realAvailable.Value; } }

        /// <summary>Zombie aus dem gekauften Modell (Humanoid + Animator)</summary>
        Zombie CreateRealZombie(int id, string type, float x, float z, float rot, bool spawning)
        {
            var cfg = D.zombie[type];
            var prefab = Resources.Load<GameObject>("Generated/Zombie_" + (Variant.TryGetValue(type, out var v) ? v : "Default")) ?? Resources.Load<GameObject>("Generated/Zombie_Default");
            var go = Object.Instantiate(prefab, root); go.name = "zombie_" + type;
            float s = cfg.scale * (type == "boss" ? 0.95f : type == "brute" ? 1.0f : 1.0f);
            go.transform.localScale = Vector3.one * s;
            go.transform.position = new Vector3(x, spawning ? -2.2f * s : 0, z); go.transform.rotation = Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0);
            var z0 = new Zombie { id = id, type = type, cfg = cfg, go = go, real = true, x = x, z = z, rot = rot, tx = x, tz = z, trot = rot, phase = Random.value * 6, seed = Random.value * 10, rise = spawning ? 1 : 0, groanT = 1 + Random.value * 8 };
            z0.anim = go.GetComponent<Animator>(); if (z0.anim) { z0.anim.applyRootMotion = false; z0.anim.SetBool("Crawl", cfg.crawl); z0.anim.speed = 0.9f + Random.value * 0.25f; }
            var tint = Tint.TryGetValue(type, out var tc) ? tc : Color.white;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = highQuality || type == "boss" ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                var ms = r.materials; // Instanzen, damit Tönung/Blinken pro Zombie geht
                foreach (var m in ms) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.GetColor("_BaseColor") * tint); z0.mats.Add(m); }
            }
            if (cfg.explode > 0 && z0.mats.Count > 0) z0.glowMat = z0.mats[0];
            // Trefferboxen an Knochen
            Transform head = z0.anim ? z0.anim.GetBoneTransform(HumanBodyBones.Head) : null, chest = z0.anim ? (z0.anim.GetBoneTransform(HumanBodyBones.Chest) ?? z0.anim.GetBoneTransform(HumanBodyBones.Spine)) : null, hips = z0.anim ? z0.anim.GetBoneTransform(HumanBodyBones.Hips) : null;
            z0.headBone = head;
            Collider Box(Transform parent, Vector3 size, Vector3 center, bool isHead)
            {
                var hb = new GameObject(isHead ? "hb_head" : "hb_body"); hb.transform.SetParent(parent ?? go.transform, false); hb.layer = Layers.Hitbox;
                var c = hb.AddComponent<BoxCollider>(); c.isTrigger = true; c.size = size; c.center = center; var zh = hb.AddComponent<ZombieHitbox>(); zh.z = z0; zh.head = isHead; z0.boxes.Add(c); return c;
            }
            if (head) Box(head, new Vector3(0.26f, 0.3f, 0.28f), new Vector3(0, 0.1f, 0.02f), true); else Box(null, Vector3.one * 0.3f, new Vector3(0, 1.65f, 0), true);
            if (chest) Box(chest, new Vector3(0.45f, 0.6f, 0.35f), new Vector3(0, 0.1f, 0), false);
            if (hips) Box(hips, new Vector3(0.4f, 0.5f, 0.3f), new Vector3(0, 0.15f, 0), false);
            if (!chest && !hips) Box(null, new Vector3(0.5f, 1.3f, 0.4f), new Vector3(0, 0.8f, 0), false);
            if (cfg.crawl) foreach (var b in z0.boxes) { b.transform.localScale = Vector3.one; }
            z0.bar = HealthBar.Create(go.transform, type == "boss" ? 2.2f : type == "brute" ? 1.4f : 0.9f, 2.05f); z0.bar.gameObject.SetActive(false);
            zombies[id] = z0;
            if (spawning) fx.Burst(new Vector3(x, 0.1f, z), U.C("#5d4037"), 12, 3, 0.15f, 1, 2);
            return z0;
        }

        public Zombie CreateZombie(int id, string type, float x, float z, float rot, bool spawning)
        {
            if (RealZombies) return CreateRealZombie(id, type, x, z, rot, spawning);
            var cfg = D.zombie[type];
            float wide = type == "brute" ? 1.3f : type == "boss" ? 1.35f : 1;
            var rig = Models.BuildCharacter(true, cfg.skin, cfg.shirt, cfg.pants, cfg.eyes, cfg.scale, wide, type);
            rig.root.transform.SetParent(root, false);
            bool shadows = highQuality || type == "boss";
            foreach (var r in rig.root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var z0 = new Zombie { id = id, type = type, cfg = cfg, rig = rig, go = rig.root, x = x, z = z, rot = rot, tx = x, tz = z, trot = rot, phase = Random.value * 6, seed = Random.value * 10, rise = spawning ? 1 : 0, groanT = 1 + Random.value * 8 };
            float s = cfg.scale, bodyH = (D.bodyLegH + D.bodyTorsoH) * s;
            // Trefferboxen (Körper + Kopf)
            var hb1 = new GameObject("hb_body"); hb1.transform.SetParent(rig.root.transform, false); hb1.layer = Layers.Hitbox;
            var c1 = hb1.AddComponent<BoxCollider>(); c1.size = new Vector3(D.bodyTorsoW * wide * s * 1.15f, bodyH, D.bodyTorsoD * s * 1.8f); c1.center = new Vector3(0, bodyH / 2, 0);
            var hb2 = new GameObject("hb_head"); hb2.transform.SetParent(rig.root.transform, false); hb2.layer = Layers.Hitbox;
            var c2 = hb2.AddComponent<BoxCollider>(); c2.size = Vector3.one * D.bodyHeadS * s * 1.3f; c2.center = new Vector3(0, bodyH + D.bodyHeadS * s * 0.5f, 0.14f * s);
            if (cfg.crawl) { bodyH = 0.75f * s; c1.size = new Vector3(D.bodyTorsoW * s * 1.2f, bodyH, 1.3f * s); c1.center = new Vector3(0, bodyH / 2, 0.2f * s); c2.center = new Vector3(0, 0.55f * s, 0.95f * s); }
            hb1.AddComponent<ZombieHitbox>().z = z0; var h2 = hb2.AddComponent<ZombieHitbox>(); h2.z = z0; h2.head = true;
            z0.boxes.Add(c1); z0.boxes.Add(c2);
            rig.root.transform.position = new Vector3(x, spawning ? -2.2f * s : 0, z); rig.root.transform.rotation = Quaternion.Euler(0, rot * Mathf.Rad2Deg, 0);
            z0.bar = HealthBar.Create(rig.root.transform, type == "boss" ? 2.2f : type == "brute" ? 1.4f : 0.9f, (D.bodyLegH + D.bodyTorsoH + D.bodyHeadS) * s + 0.45f); z0.bar.gameObject.SetActive(false);
            zombies[id] = z0;
            if (spawning) fx.Burst(new Vector3(x, 0.1f, z), U.C("#5d4037"), 12, 3, 0.15f, 1, 2);
            return z0;
        }
        public void RemoveZombie(Zombie z) { Object.Destroy(z.go); zombies.Remove(z.id); }
        public void KillZombieLocal(Zombie z, bool head, bool big)
        {
            if (z.dying) return;
            z.dying = true; z.deathT = 0; z.bar.gameObject.SetActive(false);
            foreach (var b in z.boxes) b.enabled = false;
            var p = new Vector3(z.x, 1.2f * z.cfg.scale, z.z);
            if (z.real)
            {
                bool gore = big || (T - z.bigDeath < 0.4f);
                z.headless = head && z.headBone;
                if (z.headless) { fx.Blood(z.headBone.position, Vector3.up, true); fx.Splat(z.headBone.position, Vector3.up, 1.2f, 1f); z.headBone.localScale = Vector3.zero; z.spurtT = 1.3f; }
                else fx.Blood(p, null, false);
                if (gore) { fx.Gore(p, 16, 7); z.go.SetActive(false); z.deathT = 3; }
                else { if (z.anim) z.anim.SetTrigger("Die"); z.poolT = 0.9f + Random.value * 0.6f; }
                if (Random.value < 0.5f) sfx.Groan(p, z.type == "boss" ? 0.5f : 1.3f);
                return;
            }
            fx.Blood(p, null, true);
            var cols = new[] { U.C(z.cfg.skin), U.C(z.cfg.shirt), U.C(z.cfg.pants) };
            if (head) { z.rig.head.gameObject.SetActive(false); var hp = z.rig.headMesh.position; fx.Gibs(hp, new[] { U.C(z.cfg.skin), U.C(z.cfg.skin), U.C("#8b0000") }, 9, 4); fx.Burst(hp, U.C(z.cfg.eyes), 5, 3, 0.07f, 0.5f); }
            if (big || (T - z.bigDeath < 0.4f)) { fx.Gibs(p, cols, 14, 7); z.go.SetActive(false); z.deathT = 3; }
            else if (Random.value < 0.35f) fx.Gibs(p, cols, 4, 3);
            if (Random.value < 0.5f) sfx.Groan(p, z.type == "boss" ? 0.5f : 1.3f);
        }
        public void UpdateZombies(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 12);
            var list = new List<Zombie>(zombies.Values);
            foreach (var z in list)
            {
                var c = z.rig; float s = z.cfg.scale;
                if (z.real)
                {
                    UpdateRealZombie(z, dt, k); continue;
                }
                if (z.dying)
                {
                    z.deathT += dt; float p = Mathf.Min(1, z.deathT / 0.45f);
                    c.body.localEulerAngles = new Vector3(-90 * p * p, 0, 0); c.body.localPosition = new Vector3(0, 0.2f * s * p, 0);
                    // Glieder sacken zusammen
                    float fl = Mathf.Sin(Mathf.Min(1, z.deathT / 0.6f) * Mathf.PI * 0.5f);
                    c.arms[0].localEulerAngles = new Vector3(-40 + 60 * fl, 0, -30 * fl); c.arms[1].localEulerAngles = new Vector3(-40 + 20 * fl, 0, 50 * fl);
                    c.lowerArms[0].localEulerAngles = new Vector3(-60 * fl, 0, 0); c.lowerArms[1].localEulerAngles = new Vector3(-20 * fl, 0, 0);
                    c.legs[0].localEulerAngles = new Vector3(20 * fl, 0, -15 * fl); c.legs[1].localEulerAngles = new Vector3(-10 * fl, 0, 15 * fl); c.lowerLegs[0].localEulerAngles = new Vector3(40 * fl, 0, 0);
                    if (c.jaw) c.jaw.localEulerAngles = new Vector3(35, 0, 0);
                    if (z.deathT > 2.2f) z.go.transform.position += Vector3.down * dt * 0.9f;
                    if (z.deathT > 3.6f) RemoveZombie(z);
                    continue;
                }
                float ox = z.x, oz = z.z;
                z.x += (z.tx - z.x) * k; z.z += (z.tz - z.z) * k; z.rot = Mathf.LerpAngle(z.rot * Mathf.Rad2Deg, z.trot * Mathf.Rad2Deg, k) * Mathf.Deg2Rad;
                float spd = Mathf.Sqrt((z.x - ox) * (z.x - ox) + (z.z - oz) * (z.z - oz)) / Mathf.Max(dt, 1e-3f);
                z.walk += (Mathf.Clamp01(spd / (z.cfg.speed * 0.8f)) - z.walk) * Mathf.Min(1, dt * 8);
                float y = 0;
                if (z.rise > 0) { z.rise -= dt; y = -2.2f * s * Mathf.Max(0, z.rise); if (Random.value < 0.3f) fx.Particle(new Vector3(z.x + Random.value - 0.5f, 0.05f, z.z + Random.value - 0.5f), new Vector3(0, 2, 0), U.C("#4e342e"), 0.12f, 0.6f); }
                z.go.transform.position = new Vector3(z.x, y + World.GroundY(z.x, z.z), z.z); z.go.transform.rotation = Quaternion.Euler(0, z.rot * Mathf.Rad2Deg, 0);
                z.phase += dt * z.walk * (z.type == "runner" ? 15 : 8) / Mathf.Sqrt(s);
                float attack = 0; if (z.atkT > 0) { z.atkT -= dt; attack = 1 - Mathf.Max(0, z.atkT) / 0.5f; }
                if (z.hp < z.lastHp - 3) z.flinch = 1; z.lastHp = z.hp; z.flinch = Mathf.Max(0, z.flinch - dt * 4);
                Models.Animate(c, new Models.AnimState { t = T, walk = z.walk, phase = z.phase, zombie = true, attack = attack, seed = z.seed, crawl = z.cfg.crawl, flinch = Mathf.Sin(z.flinch * Mathf.PI) });
                if (z.flash > 0 || frozen || z.wasFrozen)
                {
                    z.flash = Mathf.Max(0, z.flash - dt * 6);
                    foreach (var m in c.mats) if (m != c.glow) { if (frozen) U.SetEmission(m, U.C("#4fc3f7"), 0.55f + z.flash * 0.6f); else U.SetEmission(m, Color.white, z.flash * 0.7f); }
                    z.wasFrozen = frozen;
                }
                if (c.glow != null) U.SetEmission(c.glow, U.GetColor(c.glow), 1.2f + Mathf.Sin(T * 9 + z.seed) * 0.9f);
                bool showBar = (z.hp < 100 || z.type == "boss" || z.type == "brute") && z.rise <= 0;
                z.bar.gameObject.SetActive(showBar); if (showBar) z.bar.Set(z.hp / 100f);
                z.groanT -= dt; if (z.groanT <= 0) { z.groanT = 4 + Random.value * 9; sfx.Groan(z.go.transform.position, z.type == "boss" ? 0.5f : z.type == "brute" ? 0.7f : z.type == "runner" ? 1.35f : 1); }
            }
        }

        void UpdateRealZombie(Zombie z, float dt, float k)
        {
            float s = z.cfg.scale;
            if (z.dying)
            {
                z.deathT += dt;
                if (z.headless && z.headBone) { z.headBone.localScale = Vector3.zero; if (z.spurtT > 0) { z.spurtT -= dt; fx.BloodSpurt(z.headBone.position, (Vector3.up + z.go.transform.forward * 0.3f).normalized, Mathf.Clamp01(z.spurtT) * 1.2f); } }
                if (z.poolT > 0) { z.poolT -= dt; if (z.poolT <= 0) fx.GroundSplat(z.go.transform.position + z.go.transform.forward * 0.4f * s, (1.3f + Random.value * 0.7f) * s, 2.5f); }
                if (z.deathT > 6f) z.go.transform.position += Vector3.down * dt * 0.5f;
                if (z.deathT > 8f) RemoveZombie(z);
                return;
            }
            float ox = z.x, oz = z.z;
            z.x += (z.tx - z.x) * k; z.z += (z.tz - z.z) * k; z.rot = Mathf.LerpAngle(z.rot * Mathf.Rad2Deg, z.trot * Mathf.Rad2Deg, k) * Mathf.Deg2Rad;
            float spd = Mathf.Sqrt((z.x - ox) * (z.x - ox) + (z.z - oz) * (z.z - oz)) / Mathf.Max(dt, 1e-3f);
            z.walk += (Mathf.Clamp01(spd / (z.cfg.speed * 0.8f)) - z.walk) * Mathf.Min(1, dt * 8);
            float y = 0;
            if (z.rise > 0) { z.rise -= dt; y = -2.2f * s * Mathf.Max(0, z.rise); if (Random.value < 0.3f) fx.Particle(new Vector3(z.x + Random.value - 0.5f, 0.05f, z.z + Random.value - 0.5f), new Vector3(0, 2, 0), U.C("#4e342e"), 0.12f, 0.6f); }
            z.go.transform.position = new Vector3(z.x, y + World.GroundY(z.x, z.z), z.z); z.go.transform.rotation = Quaternion.Euler(0, z.rot * Mathf.Rad2Deg, 0);
            if (z.anim)
            {
                z.anim.SetFloat("Speed", Mathf.Min(4.5f, spd / Mathf.Max(0.5f, s)));
                if (z.atkT > 0 && T - z.lastAtk > 0.6f) { z.lastAtk = T; if (z.spitTrig > 0) { z.anim.SetTrigger("Spit"); z.spitTrig = 0; } else z.anim.SetTrigger("Attack"); }
                z.hitCd -= dt;
                if (z.hp < z.lastHp - 3 && z.atkT <= 0 && z.hitCd <= 0 && z.type != "boss") { z.anim.SetTrigger("Hit"); z.hitCd = 0.8f; }
                z.lastHp = z.hp;
            }
            if (z.atkT > 0) z.atkT -= dt;
            if (z.flash > 0 || frozen || z.wasFrozen)
            {
                z.flash = Mathf.Max(0, z.flash - dt * 6);
                foreach (var m in z.mats) { if (frozen) U.SetEmission(m, U.C("#4fc3f7"), 0.3f + z.flash * 0.4f); else U.SetEmission(m, Color.white, z.flash * 0.5f); }
                z.wasFrozen = frozen;
            }
            if (z.glowMat != null && !frozen && z.flash <= 0) U.SetEmission(z.glowMat, U.C("#ff6d00"), 0.25f + Mathf.Max(0, Mathf.Sin(T * 9 + z.seed)) * 0.5f);
            bool showBar = (z.hp < 100 || z.type == "boss" || z.type == "brute") && z.rise <= 0;
            z.bar.gameObject.SetActive(showBar); if (showBar) z.bar.Set(z.hp / 100f);
            z.groanT -= dt; if (z.groanT <= 0) { z.groanT = 4 + Random.value * 9; sfx.Groan(z.go.transform.position, z.type == "boss" ? 0.5f : z.type == "brute" ? 0.7f : z.type == "runner" ? 1.35f : 1); }
        }

        // ------------------------------------------------------------------ Mitspieler
        public Remote CreateRemote(int id, float x, float y, float z)
        {
            var (name, color) = playerInfo(id);
            var rig = Models.BuildCharacter(false, "#f2c94c", color, "#2f3b52", null, 1, 1, null, color, false);
            rig.root.transform.SetParent(root, false); rig.root.transform.position = new Vector3(x, y, z);
            var r = new Remote { id = id, name = name, color = color, rig = rig, go = rig.root, x = x, y = y, z = z, tx = x, ty = y, tz = z };
            r.label = U.Label(rig.root.transform, name, Color.white, 2.6f, 0.32f);
            r.help = U.Label(rig.root.transform, "✚ HILFE!", U.C("#ff5252"), 1.3f, 0.32f); r.help.gameObject.SetActive(false);
            remotes[id] = r; return r;
        }
        public void Relabel(Remote r, string name) { r.name = name; r.label.text = name; }
        public void RemoveRemote(int id) { if (remotes.TryGetValue(id, out var r)) { Object.Destroy(r.go); remotes.Remove(id); } }
        public void UpdateRemotes(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 14);
            foreach (var r in remotes.Values)
            {
                r.x += (r.tx - r.x) * k; r.y += (r.ty - r.y) * k; r.z += (r.tz - r.z) * k; r.yaw = Mathf.LerpAngle(r.yaw * Mathf.Rad2Deg, r.tyaw * Mathf.Rad2Deg, k) * Mathf.Deg2Rad;
                r.go.SetActive(r.state != "dead");
                r.go.transform.position = new Vector3(r.x, r.y, r.z); r.go.transform.rotation = Quaternion.Euler(0, (r.yaw + Mathf.PI) * Mathf.Rad2Deg, 0);
                string gt = r.weapon + r.tier;
                if (r.gunType != gt)
                {
                    if (r.gun != null) Object.Destroy(r.gun.root);
                    r.gun = Models.BuildGun(r.weapon, r.tier); var g = r.gun.root.transform; g.SetParent(r.rig.arms[1], false); g.localScale = Vector3.one * 0.95f;
                    g.localPosition = new Vector3(0, -D.bodyArmH + 0.12f, 0.02f); g.localRotation = Quaternion.Euler(90, 180, 0); r.gunType = gt;
                }
                r.phase += dt * 9 * r.moving;
                bool down = r.state == "down";
                Models.Animate(r.rig, new Models.AnimState { t = T, walk = down ? 0 : r.moving, phase = r.phase, zombie = false, aimPitch = down ? 0 : r.pitch });
                if (r.melee > 0) r.meleeAnim = Mathf.Min(1, r.meleeAnim + dt * 8); else r.meleeAnim = Mathf.Max(0, r.meleeAnim - dt * 6);
                if (r.meleeAnim > 0) { var e = r.rig.arms[1].localEulerAngles; e.x -= Mathf.Sin(r.meleeAnim * Mathf.PI) * 1.2f * Mathf.Rad2Deg; e.z = -Mathf.Sin(r.meleeAnim * Mathf.PI) * 0.6f * Mathf.Rad2Deg; r.rig.arms[1].localEulerAngles = e; }
                r.rig.body.localEulerAngles = new Vector3(down ? -90 : 0, 0, 0); r.rig.body.localPosition = new Vector3(0, down ? 0.25f : 0, 0);
                r.help.gameObject.SetActive(down); if (down) { var c = r.help.color; c.a = 0.6f + Mathf.Sin(T * 6) * 0.4f; r.help.color = c; }
            }
        }

        // ------------------------------------------------------------------ Türme
        public Turret CreateTurret(int id, string type, float x, float z, float yaw, int owner)
        {
            var cfg = D.turret[type]; var parts = Models.BuildTurret(type, cfg.color);
            parts.root.transform.SetParent(root, false); parts.root.transform.position = new Vector3(x, World.GroundY(x, z), z); parts.head.localRotation = Quaternion.Euler(0, yaw * Mathf.Rad2Deg, 0);
            var col = parts.root.AddComponent<CapsuleCollider>(); col.radius = 0.55f; col.height = 2; col.center = new Vector3(0, 1, 0); parts.root.layer = Layers.Solid;
            var t = new Turret { id = id, type = type, parts = parts, x = x, z = z, yaw = yaw, tyaw = yaw, owner = owner, col = col };
            t.bar = HealthBar.Create(parts.root.transform, 1.0f, type == "tesla" ? 2.7f : 2.0f); t.bar.gameObject.SetActive(false);
            turrets[id] = t; fx.Burst(new Vector3(x, 0.3f, z), U.C("#bdbdbd"), 10, 3, 0.15f, 0.8f, 1); return t;
        }
        public void RemoveTurret(Turret t) { Object.Destroy(t.parts.root); turrets.Remove(t.id); }
        public void UpdateTurrets(float dt)
        {
            float k = 1 - Mathf.Exp(-dt * 14);
            foreach (var t in turrets.Values)
            {
                var e = t.parts.head.localEulerAngles; e.y = Mathf.LerpAngle(e.y, t.tyaw * Mathf.Rad2Deg, k); t.parts.head.localEulerAngles = e;
                if (t.parts.orb) { t.parts.orb.localPosition = new Vector3(0, 1.0f + Mathf.Sin(T * 3 + t.id) * 0.08f, 0); t.parts.orb.Rotate(0, dt * 120, 0); }
                t.bar.gameObject.SetActive(t.hp < 100); if (t.hp < 100) t.bar.Set(t.hp / 100f);
            }
        }
        public Vector3 TurretMuzzle(Turret t) { var ms = t.parts.muzzles; return ms[t.muzzleIdx++ % ms.Count].position; }

        // ------------------------------------------------------------------ Barrikaden
        /// <summary>kind 0 = Barrikade, 1 = Brett (Schaufel); pit != 0: Brett liegt flach über dieser Grube (Falle, kein Hindernis)</summary>
        public Barricade CreateBarricade(int id, float x, float z, int rot, int kind = 0, int pit = 0)
        {
            bool cover = pit != 0, board = kind == 1;
            var go = cover ? Models.BuildPitCover(D.pitRadius) : board ? Models.BuildBoard() : Models.BuildBarricade();
            go.transform.SetParent(root, false); go.transform.position = new Vector3(x, World.GroundY(x, z), z);
            var baseRot = Quaternion.Euler(0, cover ? (id * 37) % 180 : rot == 1 ? 90 : 0, 0); go.transform.rotation = baseRot;
            BoxCollider col = null;
            if (!cover) { go.layer = Layers.Barricade; col = go.AddComponent<BoxCollider>(); col.size = board ? new Vector3(1.6f, 0.95f, 0.4f) : new Vector3(2.6f, 1.2f, 0.5f); col.center = new Vector3(0, col.size.y / 2, 0); }
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = highQuality ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = new Barricade { id = id, go = go, x = x, z = z, rot = rot, kind = kind, pit = pit, baseRot = baseRot, col = col };
            b.bar = HealthBar.Create(go.transform, board ? 1.0f : 1.4f, cover ? 0.6f : board ? 1.25f : 1.7f); b.bar.gameObject.SetActive(false);
            barricades[id] = b; fx.Burst(new Vector3(x, 0.4f, z), U.C("#5c4c3a"), 10, 3, 0.1f, 0.8f, 1); return b;
        }
        // ------------------------------------------------------------------ Gruben (Schaufel)
        public readonly Dictionary<int, Pit> pits = new Dictionary<int, Pit>();
        public Pit CreatePit(int id, float x, float z)
        {
            var go = Models.BuildPit(D.pitRadius); go.transform.SetParent(root, false); go.transform.position = new Vector3(x, World.GroundY(x, z) + 0.01f, z);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = highQuality ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = new Pit { id = id, go = go, x = x, z = z }; pits[id] = p; return p;
        }
        public void RemovePit(Pit p) { Object.Destroy(p.go); pits.Remove(p.id); }
        public void RemoveBarricade(Barricade b) { Object.Destroy(b.go); barricades.Remove(b.id); }
        public void UpdateBarricades(float dt)
        {
            foreach (var b in barricades.Values)
            {
                if (b.shakeT > 0) { b.shakeT -= dt; b.go.transform.rotation = b.baseRot * Quaternion.Euler(0, 0, Mathf.Sin(T * 40) * 0.03f * b.shakeT * Mathf.Rad2Deg); } else b.go.transform.rotation = b.baseRot;
                b.bar.gameObject.SetActive(b.hp < 100); if (b.hp < 100) b.bar.Set(b.hp / 100f);
            }
        }

        // ------------------------------------------------------------------ Power-ups
        public Pickup CreatePickup(int id, string kind, float x, float z)
        {
            var cfg = D.powerup[kind]; var parts = Models.BuildPickup(cfg); parts.root.transform.SetParent(root, false); parts.root.transform.position = new Vector3(x, World.GroundY(x, z), z);
            var p = new Pickup { id = id, kind = kind, cfg = cfg, parts = parts, x = x, z = z, seed = Random.value * 6 };
            pickups[id] = p; fx.Burst(new Vector3(x, 1, z), U.C(cfg.color), 12, 3, 0.1f, 0.8f, 2); return p;
        }
        public void RemovePickup(Pickup p) { Object.Destroy(p.parts.root); pickups.Remove(p.id); }
        public void UpdatePickups(float dt)
        {
            foreach (var p in pickups.Values)
            {
                var u = p.parts; u.core.Rotate(0, dt * 140, 0, Space.World); u.core.localPosition = new Vector3(0, 1.0f + Mathf.Sin(T * 3 + p.seed) * 0.15f, 0);
                u.ring.localRotation = Quaternion.Euler(Mathf.Sin(T * 1.5f + p.seed) * 23f, T * 60, 0);
                float blink = p.t < 5 ? (Mathf.Sin(T * 12) > 0 ? 1 : 0.15f) : 1;
                U.SetEmission(u.mat, U.C(p.cfg.color), 1.4f * blink); var c = u.label.color; c.a = blink; u.label.color = c;
                if (Random.value < 0.15f) fx.Particle(new Vector3(p.x + (Random.value - 0.5f) * 0.6f, 0.3f, p.z + (Random.value - 0.5f) * 0.6f), new Vector3(0, 1.5f, 0), U.C(p.cfg.color), 0.06f, 0.8f, -0.3f);
            }
        }

        public void Clear()
        {
            foreach (var z in new List<Zombie>(zombies.Values)) RemoveZombie(z);
            foreach (var id in new List<int>(remotes.Keys)) RemoveRemote(id);
            foreach (var t in new List<Turret>(turrets.Values)) RemoveTurret(t);
            foreach (var p in new List<Pit>(pits.Values)) RemovePit(p);
            foreach (var b in new List<Barricade>(barricades.Values)) RemoveBarricade(b);
            foreach (var p in new List<Pickup>(pickups.Values)) RemovePickup(p);
        }
    }
}
