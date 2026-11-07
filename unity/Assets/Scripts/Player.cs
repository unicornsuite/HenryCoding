using System.Collections.Generic;
// Lokaler Spieler: Bewegung (CharacterController), Kamera, Waffe in der Hand (Viewmodel)
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZA
{
    public class Player
    {
        public GameObject go; public CharacterController cc; public Camera cam, vmCam;
        public Vector3 vel; public float yaw, pitch, bob, stepT, stepOff, sprintAmt, recoilAcc, lastFire = -9, aim;
        public bool onGround = true, sprinting;
        // Viewmodel
        public Transform vmRoot; public Models.GunParts gun; public Transform flash; public float flashT, kick, swayX, meleeT, nadeT, reloadT, reloadDur = 1, switchT, spin, slideT;
        public string weapon = "pistol"; public int tier;
        // Third-Person: eigene Figur + Schulterkamera
        public bool thirdPerson; public CharRig self; Models.GunParts selfGun; string selfGunType = ""; float selfPhase, selfMelee; string selfColor = "#1e88e5";
        public float camDist = 4.2f;
        // gekauftes Waffenmodell mit Händen und Animator
        GameObject realGun; Animator realAnim; Transform realMuzzle; string realState = ""; public bool realActive; public bool moving;
        static readonly string[] RealStates = { "Idle", "Move", "Reload", "Fire", "EmptyReload", "FastMove", "ZoomIdle", "ZoomMove", "ZoomFire", "MeleeAttack", "Crouch", "ZoomCrouch", "Jump", "Select", "PutAway", "DryFire", "AltToAndFrom", "AltFire", "AltZoomFire", "ReloadLoop", "EndReload", "AlternateTo", "AlternateFrom", "GrenadeThrow" };
        // Die Store-Waffen zeigen in Modellkoordinaten nach -Z und sind ca. 4-5x zu groß (Pistolenkörper 1 m):
        // also um 180° drehen und um den Kamerapunkt herunterskalieren (gleiche Bildgröße, weniger Durchdringung von Wänden).
        public Vector3 realOffset = Vector3.zero; public float realScale = 0.22f, realYaw = 180f;
        static readonly Dictionary<string, Vector3> RealOffsets = new Dictionary<string, Vector3> { { "launcher", new Vector3(0, 0.05f, 0) } };
        Quaternion RealRot => Quaternion.Euler(0, realYaw, 0);
        /// <summary>Kalibrierung per Selbsttest: Drehung/Skalierung des echten Waffenmodells live ändern</summary>
        public void DebugVm(float yaw, float scale)
        {
            realYaw = yaw; realScale = scale; if (!realGun) return;
            realOffset = (RealOffsets.TryGetValue(weapon, out var ro) ? ro : Vector3.zero) * realScale;
            realGun.transform.localPosition = realOffset; realGun.transform.localRotation = RealRot; realGun.transform.localScale = Vector3.one * realScale;
        }
        const float VmShrink = 0.5f; // Viewmodel näher an die Kamera skaliert
        static GameData D => GameData.I;
        public float frameLookDX;
        readonly Material flashMat;

        public Player(Camera mainCam)
        {
            cam = mainCam;
            go = new GameObject("Player");
            cc = go.AddComponent<CharacterController>(); cc.radius = 0.38f; cc.height = 1.85f; cc.center = new Vector3(0, 0.925f, 0); cc.stepOffset = 0.5f; cc.slopeLimit = 50; cc.skinWidth = 0.03f; cc.minMoveDistance = 0;
            go.layer = Layers.World;
            // HDRP kennt keine Overlay-Kameras: Waffe hängt direkt an der Hauptkamera, verkleinert und nah dran (kaum Durchdringen von Wänden)
            vmCam = cam; cam.nearClipPlane = 0.02f;
            vmRoot = new GameObject("vm").transform; vmRoot.SetParent(cam.transform, false); vmRoot.localRotation = Quaternion.Euler(0, 180, 0); vmRoot.localScale = Vector3.one * VmShrink; // Waffen zeigen nach -Z
            flashMat = U.UnlitMat(new Color(1, 0.82f, 0.5f, 1), false, true); U.SetMainTex(flashMat, U.GlowTex());
        }

        public Vector3 Pos { get => go.transform.position; set { cc.enabled = false; go.transform.position = value; cc.enabled = true; } }

        /// <summary>Eigene Figur bauen (in der Egoperspektive nur als Schatten sichtbar)</summary>
        public void BuildSelf(string colorHex)
        {
            selfColor = colorHex;
            if (self != null) Object.Destroy(self.root);
            self = Models.BuildCharacter(false, "#f2c94c", colorHex, "#2f3b52", null, 1, 1, null, colorHex, false);
            self.root.transform.SetParent(go.transform, false); self.root.transform.localRotation = Quaternion.Euler(0, 180, 0);
            selfGunType = ""; selfGun = null;
            ApplySelfVisibility();
        }
        void ApplySelfVisibility()
        {
            if (self == null) return;
            // Eigene Layer statt "ShadowsOnly": HDRP zeichnete die Figur beim Nahkampfschwung trotzdem ins Bild
            foreach (var t in self.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.SelfFP;
            foreach (var r in self.root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            if (thirdPerson) cam.cullingMask |= 1 << Layers.SelfFP; else cam.cullingMask &= ~(1 << Layers.SelfFP);
        }
        public void SetThirdPerson(bool on) { thirdPerson = on; ApplySelfVisibility(); SetVmVisible(!on); }
        /// <summary>Mündung der Waffe der eigenen Figur (für Leuchtspuren in Third-Person)</summary>
        public Vector3 SelfMuzzle() => selfGun != null ? selfGun.muzzle.position : cam.transform.position + cam.transform.forward * 0.8f;

        public void BuildViewmodel(string weaponId, int tier, string shirtHex)
        {
            weapon = weaponId; this.tier = tier;
            if (gun != null) Object.Destroy(gun.root);
            gun = Models.BuildGun(weaponId, tier);
            var g = gun.root.transform; g.SetParent(vmRoot, false);
            Material shirt = U.Mat(shirtHex), skin = U.Mat("#f2c94c");
            U.Box(g, shirt, 0.05f, -0.13f, 0.22f, 0.11f, 0.11f, 0.32f, 0.28f, 0.12f, 0, false);
            U.Box(g, skin, 0, -0.06f, 0.05f, 0.095f, 0.11f, 0.11f, 0, 0, 0, false);
            var f = Fore(weaponId);
            U.Box(g, skin, f.x, f.y, f.z, 0.1f, 0.1f, 0.11f, 0, 0, 0, false);
            U.Box(g, shirt, f.x - 0.12f, f.y - 0.09f, f.z + 0.18f, 0.11f, 0.11f, 0.34f, 0.25f, -0.45f, 0, false);
            var fl = GameObject.CreatePrimitive(PrimitiveType.Quad); Object.Destroy(fl.GetComponent<Collider>()); fl.transform.SetParent(g, false); fl.transform.localPosition = gun.muzzle.localPosition;
            fl.transform.localRotation = Quaternion.Euler(0, 180, 0); fl.transform.localScale = Vector3.one * (weaponId == "shotgun" || weaponId == "launcher" ? 0.35f : 0.24f); fl.GetComponent<Renderer>().sharedMaterial = flashMat; fl.SetActive(false); flash = fl.transform;
            foreach (var t in gun.root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.ViewModel;
            foreach (var r in gun.root.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            g.localScale = Vector3.one * 0.55f;
            // Echtes Waffenmodell aus dem Store-Paket (falls vorhanden) statt Klötzchenwaffe
            if (realGun) Object.Destroy(realGun); realGun = null; realAnim = null; realMuzzle = null; realActive = false;
            var prefab = Resources.Load<GameObject>("Generated/Weapons/" + weaponId);
            if (prefab)
            {
                realGun = Object.Instantiate(prefab, cam.transform); realGun.name = "realgun_" + weaponId;
                realOffset = (RealOffsets.TryGetValue(weaponId, out var ro) ? ro : new Vector3(0, -0.1f, -1f)) * realScale;
                realGun.transform.localPosition = realOffset; realGun.transform.localRotation = RealRot; realGun.transform.localScale = Vector3.one * realScale;
                realAnim = realGun.GetComponent<Animator>() ?? realGun.GetComponentInChildren<Animator>();
                if (realAnim) { realAnim.cullingMode = AnimatorCullingMode.AlwaysAnimate; realAnim.applyRootMotion = false; }
                foreach (var t in realGun.GetComponentsInChildren<Transform>(true)) { string n = t.name.ToLowerInvariant(); if (realMuzzle == null && (n == "firespot" || n.Contains("muzzle") || n.Contains("flash") || n.Contains("barrel end"))) realMuzzle = t; }
                foreach (var t in realGun.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.ViewModel;
                realActive = true; gun.root.SetActive(false); realState = "";
            }
        }
        /// <summary>Sichtbarkeit der Waffe in der Hand (Klötzchen- oder echtes Modell)</summary>
        public void SetVmVisible(bool on) { vmRoot.gameObject.SetActive(on && !realActive); if (realGun) realGun.SetActive(on); }
        void UpdateRealGun(float dt, float moveFrac)
        {
            if (!realAnim) return;
            string st;
            if (switchT > 0.1f) st = "Select";
            else if (reloadT > 0) st = "Reload";
            else if (meleeT > 0) st = "MeleeAttack";
            else if (nadeT > 0) st = "GrenadeThrow";
            else if (slideT > 0.35f) st = aim > 0.5f ? "ZoomFire" : "Fire";
            else if (aim > 0.5f) st = moveFrac > 0.15f ? "ZoomMove" : "ZoomIdle";
            else if (sprinting && moveFrac > 0.3f) st = "FastMove";
            else if (moveFrac > 0.15f) st = "Move";
            else st = "Idle";
            if (st != realState)
            {
                foreach (var n in RealStates) realAnim.SetBool(n, false);
                realAnim.SetBool(st, true); realState = st;
            }
            // leichtes Schwanken wie beim Klötzchenmodell
            swayX += (Mathf.Clamp(-frameLookDX * 0.0006f, -0.06f, 0.06f) - swayX) * Mathf.Min(1, dt * 10);
            realGun.transform.localPosition = realOffset + new Vector3(swayX * 0.5f, -Mathf.Abs(Mathf.Cos(bob)) * 0.006f * moveFrac, 0);
            realGun.transform.localRotation = Quaternion.Euler(kick * -2f, swayX * 40f, 0) * RealRot;
        }
        static Vector3 AimPos(string w) => w switch { "pistol" => new Vector3(0, -0.085f, -0.34f), "smg" => new Vector3(0, -0.09f, -0.3f), "shotgun" => new Vector3(0, -0.1f, -0.32f), "rifle" => new Vector3(0, -0.135f, -0.27f), "sniper" => new Vector3(0, -0.13f, -0.25f), "launcher" => new Vector3(-0.09f, -0.16f, -0.3f), _ => new Vector3(0.16f, -0.22f, -0.42f) };
        static Vector3 Fore(string w) => w switch { "pistol" => new Vector3(-0.03f, -0.09f, 0.02f), "smg" => new Vector3(0, -0.04f, -0.28f), "shotgun" => new Vector3(0, -0.04f, -0.36f), "rifle" => new Vector3(0, -0.04f, -0.4f), "sniper" => new Vector3(0, -0.05f, -0.42f), "launcher" => new Vector3(0, -0.04f, -0.34f), _ => new Vector3(0.14f, 0.05f, 0.06f) };

        /// <summary>Bewegung & Kamera. active = Eingaben erlaubt; alive = lebt. Gibt moveFrac zurück.</summary>
        public float Update(float dt, bool active, bool alive, bool down, float sens, float fov, bool sprintPerk, bool minigun, Sfx sfx, float shake)
        {
            var kb = Keyboard.current; var ms = Mouse.current;
            float lookDX = 0, lookDY = 0;
            if (active && ms != null && Cursor.lockState == CursorLockMode.Locked) { var d = ms.delta.ReadValue(); lookDX = d.x; lookDY = -d.y; }
            float zoom = weapon == "sniper" && aim > 0.9f ? 0.3f : 1 - aim * 0.3f;
            frameLookDX = lookDX;
            // Unity ist linkshändig: Drehsinn gegenüber der Browser-Version gespiegelt
            if (active) { yaw += lookDX * 0.0022f * sens * zoom; pitch -= lookDY * 0.0022f * sens * zoom; }
            pitch = Mathf.Clamp(pitch, -1.5f, 1.5f);
            if (recoilAcc > 0 && Time.time - lastFire > 0.08f) { float r = Mathf.Min(recoilAcc, dt * (0.15f + recoilAcc * 5)); pitch -= r; recoilAcc -= r; }

            float moveFrac = 0;
            if (alive)
            {
                float f = 0, s = 0; bool jump = false;
                if (active && kb != null)
                {
                    f = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1 : 0) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1 : 0);
                    s = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1 : 0) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1 : 0);
                    jump = kb.spaceKey.isPressed;
                }
                float len = Mathf.Sqrt(f * f + s * s); if (len > 1) { f /= len; s /= len; }
                float sin = Mathf.Sin(yaw), cos = Mathf.Cos(yaw);
                float wx = -sin * f - cos * s, wz = -cos * f + sin * s; // rechts = Kamera-Rechtsvektor (linkshändig)
                sprinting = active && kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) && f > 0.3f && aim < 0.3f && reloadT <= 0;
                sprintAmt += ((sprinting ? 1 : 0) - sprintAmt) * Mathf.Min(1, dt * 10);
                float speed = sprinting ? 8.4f : 5.4f; if (sprintPerk) speed *= 1.15f; speed *= 1 - aim * 0.4f; if (minigun) speed *= 0.78f;
                float acc = onGround ? 14 : 3.5f;
                vel.x += (wx * speed - vel.x) * Mathf.Min(1, acc * dt); vel.z += (wz * speed - vel.z) * Mathf.Min(1, acc * dt);
                if (jump && onGround) { vel.y = 6.8f; onGround = false; }
                vel.y -= 20 * dt;
                float oldY = go.transform.position.y;
                var flags = cc.Move(vel * dt);
                onGround = (flags & CollisionFlags.Below) != 0;
                if (onGround && vel.y < 0) vel.y = -1f;
                if ((flags & CollisionFlags.Above) != 0 && vel.y > 0) vel.y = 0;
                float dy = go.transform.position.y - oldY; if (dy > 0.05f && onGround && vel.y <= 0) stepOff -= dy;
                float hs = Mathf.Sqrt(vel.x * vel.x + vel.z * vel.z);
                moveFrac = Mathf.Clamp(hs / 5.4f, 0, 1.5f);
                if (onGround && hs > 0.5f) { bob += dt * hs * 1.7f; stepT -= dt * hs; if (stepT <= 0) { stepT = 2.4f; sfx.Play("step", null, 0.5f); } }
            }
            else { vel = Vector3.zero; aim = 0; spin = 0; sprintAmt = 0; }
            stepOff *= Mathf.Exp(-dt * 14);

            // Kamera
            float eye = down ? 0.45f : D.bodyEye;
            var cp = go.transform.position + new Vector3(0, eye + stepOff, 0);
            if (alive && onGround && moveFrac > 0.1f) cp.y += Mathf.Sin(bob * 2) * 0.035f * moveFrac * (1 - aim);
            cam.transform.position = cp;
            // JS-Konvention: yaw 0 = Blick nach -Z, pitch > 0 = nach oben
            cam.transform.rotation = Quaternion.Euler(-(pitch + (Random.value - 0.5f) * shake * 0.08f) * Mathf.Rad2Deg, (yaw + (Random.value - 0.5f) * shake * 0.08f) * Mathf.Rad2Deg + 180f, down ? 20 : 0);
            if (thirdPerson && !down)
            {
                // Schulterkamera: hinter und rechts über der Figur, bei Hindernissen näher heran
                var pivot = go.transform.position + new Vector3(0, 1.7f, 0);
                float dist = Mathf.Lerp(camDist, 1.6f, aim);
                var want = pivot - cam.transform.forward * dist + cam.transform.right * Mathf.Lerp(0.7f, 0.5f, aim) + cam.transform.up * 0.35f;
                var dir = want - pivot; float len = dir.magnitude;
                if (Physics.SphereCast(pivot, 0.25f, dir / len, out var hit, len, (1 << Layers.Solid) | (1 << Layers.Barricade) | (1 << Layers.Ground), QueryTriggerInteraction.Ignore)) want = pivot + dir / len * Mathf.Max(0.4f, hit.distance - 0.1f);
                cam.transform.position = want;
            }
            UpdateSelf(dt, alive, down, moveFrac);
            float wantFov = fov * (weapon == "sniper" ? 1 - aim * 0.72f : 1 - aim * 0.2f) * (1 + sprintAmt * 0.06f);
            if (Mathf.Abs(cam.fieldOfView - wantFov) > 0.01f) cam.fieldOfView = wantFov;
            UpdateViewmodel(dt, moveFrac);
            return moveFrac;
        }

        void UpdateSelf(float dt, bool alive, bool down, float moveFrac)
        {
            if (self == null) return;
            self.root.SetActive((alive || down) && thirdPerson); // in der Egoperspektive ganz aus (Layer-Culling reichte in HDRP nicht)
            self.root.transform.localRotation = Quaternion.Euler(0, (yaw + Mathf.PI) * Mathf.Rad2Deg, 0) * Quaternion.Euler(0, 0, 0);
            self.root.transform.rotation = Quaternion.Euler(0, (yaw + Mathf.PI) * Mathf.Rad2Deg, 0);
            string gt = weapon + tier;
            if (selfGunType != gt)
            {
                if (selfGun != null) Object.Destroy(selfGun.root);
                selfGun = Models.BuildGun(weapon, tier); var g = selfGun.root.transform; g.SetParent(self.arms[1], false); g.localScale = Vector3.one * 0.95f;
                g.localPosition = new Vector3(0, -D.bodyArmH + 0.12f, 0.02f); g.localRotation = Quaternion.Euler(90, 180, 0); selfGunType = gt;
                ApplySelfVisibility();
            }
            float hs = new Vector2(vel.x, vel.z).magnitude; float walk = Mathf.Clamp01(hs / 5.4f);
            selfPhase += dt * 9 * walk * (sprinting ? 1.4f : 1);
            Models.Animate(self, new Models.AnimState { t = Time.time, walk = down ? 0 : walk, phase = selfPhase, zombie = false, aimPitch = down ? 0 : pitch });
            if (meleeT > 0) selfMelee = Mathf.Min(1, selfMelee + dt * 8); else selfMelee = Mathf.Max(0, selfMelee - dt * 6);
            if (selfMelee > 0) { var e = self.arms[1].localEulerAngles; e.x -= Mathf.Sin(selfMelee * Mathf.PI) * 1.2f * Mathf.Rad2Deg; e.z = -Mathf.Sin(selfMelee * Mathf.PI) * 0.6f * Mathf.Rad2Deg; self.arms[1].localEulerAngles = e; }
            if (reloadT > 0 && selfGun != null && selfGun.mag) { float pr = 1 - reloadT / reloadDur, d = pr < 0.5f ? pr * 2 : (1 - pr) * 2; selfGun.mag.localPosition = selfGun.magHome + new Vector3(0, -0.18f * d, 0.04f * d); } else if (selfGun != null && selfGun.mag) selfGun.mag.localPosition = selfGun.magHome;
            self.body.localEulerAngles = new Vector3(down ? -90 : 0, 0, 0); self.body.localPosition = new Vector3(0, down ? 0.25f : 0, 0);
        }

        /// <summary>Blickrichtung (Einheitsvektor)</summary>
        public Vector3 Forward => cam.transform.forward;

        void UpdateViewmodel(float dt, float moveFrac)
        {
            if (realActive) { slideT = Mathf.Max(0, slideT - dt * 9); kick *= Mathf.Exp(-dt * 14); UpdateRealGun(dt, moveFrac); return; }
            if (gun == null) return;
            var g = gun.root.transform;
            var hip = new Vector3(0.2f, -0.2f, -0.55f); var ap = AimPos(weapon); var aimP = new Vector3(ap.x * 0.55f, ap.y * 0.55f - 0.02f, ap.z - 0.12f);
            float a = aim;
            var p = Vector3.Lerp(hip, aimP, a);
            float rx = 0, ry = 0, rz = 0;
            float bobAmt = moveFrac * (1 - a * 0.85f) * (onGround ? 1 : 0.3f);
            p.x += Mathf.Sin(bob) * 0.016f * bobAmt; p.y -= Mathf.Abs(Mathf.Cos(bob)) * 0.014f * bobAmt;
            swayX += (Mathf.Clamp(-frameLookDX * 0.0006f, -0.06f, 0.06f) - swayX) * Mathf.Min(1, dt * 10);
            p.x += swayX * (1 - a * 0.7f); ry += swayX * 1.5f;
            kick *= Mathf.Exp(-dt * 14);
            p.z += kick * 0.07f; rx += kick * (weapon == "minigun" ? 0.02f : 0.14f);
            if (reloadT > 0) { float pr = 1 - reloadT / reloadDur, d = Mathf.Sin(pr * Mathf.PI); rx += d * 0.55f; rz += d * 0.5f; p.y -= d * 0.12f; }
            if (meleeT > 0) { float pr = 1 - meleeT / 0.3f, d = Mathf.Sin(pr * Mathf.PI); p.x -= d * 0.25f; p.y += d * 0.05f - pr * 0.1f; p.z -= d * 0.25f; ry += d * 1.1f; rz -= d * 0.8f; rx -= d * 0.3f; }
            if (nadeT > 0) { float pr = 1 - nadeT / 0.35f, d = Mathf.Sin(pr * Mathf.PI); p.y -= d * 0.25f; rx -= d * 0.5f; p.x += d * 0.1f; }
            if (switchT > 0) { float s = switchT / 0.35f; p.y -= s * 0.4f; rx -= s * 0.6f; }
            p.x -= sprintAmt * 0.06f; ry += sprintAmt * 0.6f; rx -= sprintAmt * 0.2f;
            p.x = -p.x; // vmRoot ist um 180° gedreht
            g.localPosition = p; g.localRotation = Quaternion.Euler(rx * Mathf.Rad2Deg, -ry * Mathf.Rad2Deg, -rz * Mathf.Rad2Deg);
            if (gun.spin) gun.spin.Rotate(0, 0, dt * spin * 35 * Mathf.Rad2Deg);
            // Verschluss / Schlitten fährt beim Schuss zurück
            slideT = Mathf.Max(0, slideT - dt * 9);
            if (gun.slide) gun.slide.localPosition = gun.slideHome + new Vector3(0, 0, Mathf.Sin(Mathf.Min(1, slideT) * Mathf.PI) * (weapon == "shotgun" ? 0.09f : 0.045f));
            // Magazin: beim Nachladen raus und wieder rein
            if (gun.mag)
            {
                if (reloadT > 0) { float pr = 1 - reloadT / reloadDur; float d = pr < 0.5f ? pr * 2 : (1 - pr) * 2; gun.mag.localPosition = gun.magHome + new Vector3(0, -0.18f * d, 0.04f * d); gun.mag.localRotation = Quaternion.Euler(d * 25, 0, 0); }
                else { gun.mag.localPosition = gun.magHome; gun.mag.localRotation = Quaternion.identity; }
            }
            flashT -= dt; if (flash) { flash.gameObject.SetActive(flashT > 0); if (flashT > 0) flash.localRotation = Quaternion.Euler(0, 0, Random.value * 360); }
        }

        public Vector3 MuzzleWorld()
        {
            if (realActive && realMuzzle) return realMuzzle.position;
            var off = new Vector3(0.2f * (1 - aim), -0.16f + 0.08f * aim, 0.85f);
            return cam.transform.position + cam.transform.rotation * off;
        }
    }
}
