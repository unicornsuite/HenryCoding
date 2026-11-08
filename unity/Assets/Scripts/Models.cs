// Klötzchen-Modelle: Figuren, Zombies, Waffen, Türme, Generator, Shop, Kiste, Barrikade, Power-ups
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class CharRig
    {
        public GameObject root; public Transform body, torso, head, headMesh, jaw; public Transform[] arms = new Transform[2], legs = new Transform[2], lowerArms = new Transform[2], lowerLegs = new Transform[2], hands = new Transform[2];
        public List<Material> mats = new List<Material>(); public Material glow; public float scale = 1;
    }

    public static class Models
    {
        static GameData D => GameData.I;
        const float R2D = Mathf.Rad2Deg;

        static Transform Pivot(Transform parent, string name, Vector3 pos) { var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = pos; return g.transform; }

        /// <summary>Roblox-artige Figur mit Ellbogen- und Kniegelenken, Vorderseite +Z. extras: Zombietyp-ID oder null</summary>
        public static CharRig BuildCharacter(bool zombie, string skin, string shirt, string pants, string eyes, float scale = 1, float wide = 1, string extras = null, string hat = null, bool uniqueMats = true)
        {
            var rig = new CharRig { scale = scale };
            rig.root = new GameObject(zombie ? "zombie" : "player");
            rig.body = Pivot(rig.root.transform, "body", Vector3.zero); rig.body.localScale = Vector3.one * scale;
            Material M(string c) => uniqueMats ? U.NewMat(U.C(c)) : U.Mat(c);
            Material skinM = M(skin), shirtM = M(shirt), pantsM = M(pants), shoe = U.Mat("#2b2b2b"), dark = U.Mat("#1a1a1a");
            rig.mats.AddRange(new[] { skinM, shirtM, pantsM });
            float legH = D.bodyLegH, legW = D.bodyLegW, torsoH = D.bodyTorsoH, torsoW = D.bodyTorsoW * wide, torsoD = D.bodyTorsoD, armH = D.bodyArmH, armW = D.bodyArmW, headS = D.bodyHeadS;
            var r = U.Rng((skin + shirt).GetHashCode());
            // Beine: Hüfte -> Oberschenkel -> Knie -> Schienbein + Schuh
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var hip = Pivot(rig.body, "leg", new Vector3(side * legW * 0.55f * wide, legH, 0));
                U.Box(hip, pantsM, 0, -legH * 0.25f, 0, legW, legH * 0.5f, legW * 1.05f);
                var knee = Pivot(hip, "knee", new Vector3(0, -legH * 0.5f, 0));
                U.Box(knee, pantsM, 0, -legH * 0.2f, 0, legW * 0.92f, legH * 0.4f, legW * 0.98f);
                if (!zombie) U.Box(knee, dark, 0, -legH * 0.02f, legW * 0.5f, legW * 0.7f, 0.12f, 0.08f, 0, 0, 0, false); // Knieschoner
                U.Box(knee, shoe, 0, -legH * 0.46f, 0.06f, legW * 1.05f, legH * 0.14f, legW * 1.3f);
                rig.legs[i] = hip; rig.lowerLegs[i] = knee;
            }
            // Rumpf
            rig.torso = Pivot(rig.body, "torso", new Vector3(0, legH, 0));
            U.Box(rig.torso, shirtM, 0, torsoH / 2, 0, torsoW, torsoH, torsoD);
            if (!zombie)
            {
                U.Box(rig.torso, U.Mat("#3e2723"), 0, 0.06f, 0, torsoW * 1.02f, 0.1f, torsoD * 1.03f);           // Gürtel
                U.Box(rig.torso, U.Mat("#5d4037"), 0, 0.06f, torsoD / 2 + 0.01f, 0.12f, 0.12f, 0.03f, 0, 0, 0, false); // Schnalle
                U.Box(rig.torso, U.Mat("#455a64"), 0, torsoH * 0.55f, -torsoD / 2 - 0.1f, torsoW * 0.7f, torsoH * 0.6f, 0.2f); // Rucksack
                foreach (var sd in new[] { -1, 1 }) U.Box(rig.torso, U.Mat("#263238"), sd * torsoW * 0.3f, torsoH * 0.6f, torsoD / 2 + 0.01f, 0.14f, torsoH * 0.5f, 0.03f, 0, 0, 0, false); // Träger
                U.Box(rig.torso, U.Mat("#37474f"), 0, 0.22f, torsoD / 2 + 0.03f, 0.3f, 0.16f, 0.06f, 0, 0, 0, false); // Tasche
            }
            else
            {
                U.Box(rig.torso, skinM, -0.18f * wide, 0.25f, torsoD / 2 + 0.005f, 0.22f, 0.2f, 0.02f, 0, 0, 0, false);   // zerrissen
                U.Box(rig.torso, U.Mat("#7a1010"), 0.2f * wide, 0.55f, torsoD / 2 + 0.006f, 0.16f, 0.14f, 0.02f, 0, 0, 0, false);
                for (int k = 0; k < 3; k++) U.Box(rig.torso, U.Mat("#e0d8c0"), -0.16f * wide, 0.22f + k * 0.07f, torsoD / 2 + 0.01f, 0.18f, 0.025f, 0.02f, 0, 0, 0, false); // Rippen
            }
            // Arme: Schulter -> Oberarm -> Ellbogen -> Unterarm + Hand
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var sh = Pivot(rig.torso, "arm", new Vector3(side * (torsoW / 2 + armW / 2), torsoH - 0.1f, 0));
                U.Box(sh, shirtM, 0, -armH * 0.22f + 0.1f, 0, armW, armH * 0.45f, armW);
                if (!zombie) U.Box(sh, U.Mat("#263238"), 0, 0.02f, 0, armW * 1.15f, 0.12f, armW * 1.15f); // Schulterpolster
                var el = Pivot(sh, "elbow", new Vector3(0, -armH * 0.35f, 0));
                U.Box(el, skinM, 0, -armH * 0.25f, 0, armW * 0.95f, armH * 0.45f, armW * 0.95f);
                var hand = Pivot(el, "hand", new Vector3(0, -armH * 0.5f, 0));
                U.Box(hand, skinM, 0, -0.07f, 0.02f, armW * 0.9f, 0.14f, armW * 0.75f);
                U.Box(hand, skinM, -side * armW * 0.5f, -0.05f, 0.06f, 0.08f, 0.1f, 0.08f); // Daumen
                if (zombie && i == 0) U.Box(el, U.Mat("#e0d8c0"), 0, -armH * 0.2f, armW * 0.45f, 0.06f, armH * 0.25f, 0.04f, 0, 0, 0, false); // Knochen
                rig.arms[i] = sh; rig.lowerArms[i] = el; rig.hands[i] = hand;
            }
            // Kopf
            rig.head = Pivot(rig.torso, "head", new Vector3(0, torsoH, 0));
            var hm = U.Box(rig.head, skinM, 0, headS / 2 + 0.02f, 0, headS * 1.1f, headS, headS); rig.headMesh = hm.transform;
            var faceMat = U.NewMat(Color.white, 0, 0.3f, U.FaceTex(zombie, skin, eyes ?? "#000000"));
            U.Prim(PrimitiveType.Quad, rig.head, faceMat, new Vector3(0, headS / 2 + 0.02f, headS / 2 + 0.003f), new Vector3(headS * 1.1f, headS, 1), new Vector3(0, 180, 0), false, false);
            foreach (var sd in new[] { -1, 1 }) U.Box(rig.head, skinM, sd * headS * 0.57f, headS / 2 + 0.05f, 0, 0.06f, 0.12f, 0.1f, 0, 0, 0, false); // Ohren
            if (!zombie)
            {
                var hairM = U.Mat(hat ?? "#3e2723");
                U.Box(rig.head, hairM, 0, headS + 0.04f, -0.02f, headS * 1.16f, 0.1f, headS * 1.08f);                 // Haar/Mütze oben
                U.Box(rig.head, hairM, 0, headS * 0.8f, -headS / 2 - 0.02f, headS * 1.12f, headS * 0.45f, 0.06f, 0, 0, 0, false); // Hinterkopf
                U.Box(rig.head, hairM, 0, headS + 0.02f, 0.3f, headS * 0.9f, 0.04f, 0.2f);                            // Schirm
                U.Box(rig.head, U.Mat("#ffffff"), 0, headS + 0.09f, 0, 0.16f, 0.06f, 0.16f, 0, 0, 0, false);           // Logo
            }
            else
            {
                rig.jaw = Pivot(rig.head, "jaw", new Vector3(0, 0.1f, headS / 2 - 0.08f));
                U.Box(rig.jaw, skinM, 0, -0.03f, 0.08f, headS * 0.7f, 0.1f, 0.16f);
                for (int k = 0; k < 4; k++) U.Box(rig.jaw, U.Mat("#f5f0d0"), -0.12f + k * 0.08f, 0.03f, 0.15f, 0.03f, 0.05f, 0.02f, 0, 0, 0, false);
                if (r() > 0.4f) U.Box(rig.head, U.Mat("#2b1b10"), 0.1f, headS + 0.03f, -0.05f, headS * 0.6f, 0.06f, headS * 0.7f, 0, 0.3f, 0.1f); // Haarrest
            }
            if (zombie)
            {
                if (extras == "exploder")
                {
                    var glow = U.NewMat(U.C("#ffab00"), 0, 0.5f, null, U.C("#ff6d00"), 1.5f); rig.mats.Add(glow); rig.glow = glow;
                    foreach (var (x, y, z, sz) in new[] { (0f, 0.4f, 0.23f, 0.32f), (-0.25f, 0.6f, 0.2f, 0.16f), (0.28f, 0.2f, 0.2f, 0.18f), (0.1f, 0.65f, -0.22f, 0.2f) }) U.Box(rig.torso, glow, x, y, z, sz, sz, sz * 0.6f, 0, 0, 0, false);
                }
                if (extras == "spitter")
                {
                    var sac = U.NewMat(U.C("#aeea00"), 0, 0.6f, null, U.C("#64dd17"), 0.9f); rig.mats.Add(sac); rig.glow = sac;
                    U.Box(rig.torso, sac, 0, 0.3f, torsoD / 2 + 0.1f, 0.5f, 0.42f, 0.3f, 0, 0, 0, false);
                    U.Sphere(rig.head, sac, 0, 0.12f, headS / 2 + 0.02f, 0.14f, false);
                }
                if (extras == "crawler") U.Box(rig.torso, U.Mat("#3e2723"), 0, 0.02f, 0, torsoW * 1.02f, 0.1f, torsoD * 1.02f, 0, 0, 0, false);
                if (extras == "boss" || extras == "brute")
                {
                    var pad = U.Mat(extras == "boss" ? "#4a148c" : "#5d4037", 0.3f, 0.5f);
                    foreach (var a in rig.arms) { U.Box(a, pad, 0, 0.08f, 0, armW * 1.4f, 0.22f, armW * 1.4f); U.Box(a, pad, 0, -0.05f, 0, armW * 1.25f, 0.1f, armW * 1.25f); }
                    U.Box(rig.torso, pad, 0, torsoH * 0.78f, 0, torsoW * 1.08f, 0.14f, torsoD * 1.1f); // Schultergurt
                }
                if (extras == "boss")
                {
                    var horn = U.Mat("#e0e0e0");
                    for (int i = 0; i < 2; i++) { int side = i == 0 ? -1 : 1; U.Box(rig.head, horn, side * 0.2f, headS + 0.15f, 0, 0.1f, 0.3f, 0.1f, 0, 0, -side * 0.4f); U.Box(rig.head, horn, side * 0.28f, headS + 0.32f, 0, 0.06f, 0.16f, 0.06f, 0, 0, -side * 0.9f); }
                    foreach (var sd in new[] { -1, 1 }) U.Box(rig.torso, U.Mat("#7a1010"), sd * torsoW * 0.25f, torsoH * 0.5f, torsoD / 2 + 0.01f, 0.08f, torsoH * 0.6f, 0.02f, 0, 0, sd * 0.3f, false);
                }
            }
            return rig;
        }

        public struct AnimState { public float t, walk, phase, attack, aimPitch, seed, flinch; public bool zombie, crawl; }

        static void E(Transform tr, float x, float y, float z) => tr.localEulerAngles = new Vector3(x * R2D, y * R2D, z * R2D);

        public static void Animate(CharRig c, AnimState st)
        {
            float legH = D.bodyLegH, ph = st.phase;
            if (st.zombie && st.crawl)
            {
                E(c.torso, 1.25f + st.flinch * 0.3f, 0, 0);
                c.torso.localPosition = new Vector3(0, legH * 0.35f + Mathf.Abs(Mathf.Sin(ph)) * 0.04f * st.walk, 0);
                E(c.legs[0], 1.1f + Mathf.Sin(ph) * 0.3f * st.walk, 0, 0); E(c.legs[1], 1.1f - Mathf.Sin(ph) * 0.3f * st.walk, 0, 0);
                E(c.lowerLegs[0], -1.0f, 0, 0); E(c.lowerLegs[1], -1.0f, 0, 0);
                c.legs[0].localPosition = new Vector3(c.legs[0].localPosition.x, legH * 0.35f, 0); c.legs[1].localPosition = new Vector3(c.legs[1].localPosition.x, legH * 0.35f, 0);
                float reach = st.attack > 0 ? -2.6f : -2.2f;
                E(c.arms[0], reach + Mathf.Sin(ph) * 0.6f * st.walk, 0, -0.25f); E(c.arms[1], reach - Mathf.Sin(ph) * 0.6f * st.walk, 0, 0.25f);
                E(c.lowerArms[0], -0.4f - Mathf.Max(0, Mathf.Sin(ph)) * 0.5f * st.walk, 0, 0); E(c.lowerArms[1], -0.4f - Mathf.Max(0, -Mathf.Sin(ph)) * 0.5f * st.walk, 0, 0);
                E(c.head, -1.1f, 0, Mathf.Sin(st.t * 3 + st.seed) * 0.15f);
                if (c.jaw) E(c.jaw, st.attack > 0 ? 0.5f : 0.15f + Mathf.Sin(st.t * 4 + st.seed) * 0.1f, 0, 0);
                return;
            }
            // Beine mit Knie: Schwung vorn gestreckt, hinten angewinkelt
            float sw = Mathf.Sin(ph) * 0.8f * st.walk;
            E(c.legs[0], sw, 0, 0); E(c.legs[1], -sw, 0, 0);
            float k0 = Mathf.Max(0, -Mathf.Cos(ph + 0.6f)) * 1.1f * st.walk, k1 = Mathf.Max(0, Mathf.Cos(ph + 0.6f)) * 1.1f * st.walk;
            E(c.lowerLegs[0], k0, 0, 0); E(c.lowerLegs[1], k1, 0, 0);
            c.torso.localPosition = new Vector3(0, legH + Mathf.Abs(Mathf.Cos(ph)) * 0.05f * st.walk, 0);
            if (st.zombie)
            {
                float sway = Mathf.Sin(st.t * 2.2f + st.seed) * 0.12f;
                float ax = -Mathf.PI / 2 + 0.15f + sway;
                if (st.attack > 0) ax = -Mathf.PI / 2 - 0.9f * Mathf.Sin(st.attack * Mathf.PI) + 0.9f * (1 - st.attack);
                E(c.arms[0], ax + Mathf.Sin(ph) * 0.15f * st.walk, 0, -0.08f); E(c.arms[1], ax + (st.attack > 0 ? 0 : -sway * 2) - Mathf.Sin(ph) * 0.15f * st.walk, 0, 0.08f);
                float elb = -0.5f + (st.attack > 0 ? -0.6f * Mathf.Sin(st.attack * Mathf.PI) : 0);
                E(c.lowerArms[0], elb + Mathf.Sin(st.t * 3 + st.seed) * 0.1f, 0, 0); E(c.lowerArms[1], elb - Mathf.Sin(st.t * 2.5f + st.seed) * 0.1f, 0, 0);
                E(c.hands[0], -0.3f, 0, 0); E(c.hands[1], -0.3f, 0, 0);
                float lean = 0.18f + (st.attack > 0 ? Mathf.Sin(st.attack * Mathf.PI) * 0.3f : 0) - st.flinch * 0.35f;
                E(c.torso, lean, Mathf.Sin(ph) * 0.08f * st.walk, Mathf.Sin(ph * 0.5f) * 0.05f * st.walk);
                E(c.head, -0.1f + st.flinch * 0.3f, Mathf.Sin(st.t * 0.7f + st.seed) * 0.4f, Mathf.Sin(st.t * 1.3f + st.seed) * 0.18f);
                if (c.jaw) E(c.jaw, st.attack > 0 ? 0.6f * Mathf.Sin(st.attack * Mathf.PI) + 0.2f : 0.1f + Mathf.Max(0, Mathf.Sin(st.t * 1.7f + st.seed)) * 0.25f, 0, 0);
            }
            else
            {
                float p = st.aimPitch;
                E(c.arms[1], -Mathf.PI / 2 + p, 0, 0); E(c.lowerArms[1], -0.15f, 0, 0); E(c.hands[1], 0, 0, 0);
                E(c.arms[0], -Mathf.PI / 2 + p + 0.25f, 0, 0.35f); E(c.lowerArms[0], -0.9f, 0.6f, 0); E(c.hands[0], 0, 0, 0);
                E(c.head, -p * 0.6f, 0, 0);
                E(c.torso, 0.05f * st.walk, Mathf.Sin(ph) * 0.06f * st.walk, 0);
            }
        }

        // ------------------------------------------------------------------ Waffen (zeigen nach -Z, Ursprung = Griff)
        public class GunParts { public GameObject root; public Transform muzzle, spin, slide, mag; public Vector3 slideHome, magHome; public Material glow; }
        public static GunParts BuildGun(string type, int tier = 0)
        {
            var gp = new GunParts { root = new GameObject("gun_" + type) };
            var g = gp.root.transform;
            Material black = U.Mat("#222428", 0.5f, 0.6f), grey = U.Mat("#5f646b", 0.6f, 0.65f), wood = U.Mat("#8d5a2b", 0.05f, 0.45f), olive = U.Mat("#4b5320", 0.1f, 0.4f), green = U.Mat("#2e7d32"), tan = U.Mat("#a1887f"), lens = U.NewMat(U.C("#4fc3f7"), 0.2f, 0.95f, null, U.C("#29b6f6"), 0.6f);
            GameObject Add(Material m, float x, float y, float z, float w, float h, float d, float rx = 0, Transform parent = null) => U.Box(parent ?? g, m, x, y, z, w, h, d, rx, 0, 0, false);
            void Barrel(float r, float len, float z, float y = 0.04f, Material m = null) => U.Cyl(g, m ?? black, 0, y, z, r, len, Mathf.PI / 2, 0, 0, false);
            void Sights(float y, float zFront, float zRear) { Add(black, 0, y, zFront, 0.015f, 0.03f, 0.015f); Add(black, -0.02f, y, zRear, 0.012f, 0.03f, 0.015f); Add(black, 0.02f, y, zRear, 0.012f, 0.03f, 0.015f); }
            Transform Part(string n, Vector3 pos) { var t = Pivot(g, n, pos); return t; }
            float muzzleZ = -0.3f, muzzleY = 0.04f;
            switch (type)
            {
                case "pistol":
                    gp.slide = Part("slide", new Vector3(0, 0.055f, -0.1f)); Add(grey, 0, 0, 0, 0.07f, 0.05f, 0.3f, 0, gp.slide); Sights(0.04f, -0.12f, 0.12f);
                    Add(black, 0, 0.02f, -0.08f, 0.075f, 0.04f, 0.26f);               // Rahmen
                    Add(black, 0, -0.06f, 0.02f, 0.065f, 0.16f, 0.08f, 0.2f);          // Griff
                    Add(black, 0, -0.03f, -0.06f, 0.02f, 0.05f, 0.08f);                // Abzugbügel
                    gp.mag = Part("mag", new Vector3(0, -0.15f, 0.03f)); Add(grey, 0, 0, 0, 0.05f, 0.04f, 0.07f, 0, gp.mag);
                    muzzleZ = -0.26f; break;
                case "smg":
                    Add(black, 0, 0.03f, -0.12f, 0.08f, 0.11f, 0.42f); Add(grey, 0, 0.1f, -0.15f, 0.05f, 0.03f, 0.3f); Sights(0.13f, -0.3f, 0.02f);
                    gp.mag = Part("mag", new Vector3(0, -0.12f, -0.14f)); Add(grey, 0, 0, 0, 0.05f, 0.22f, 0.06f, 0, gp.mag);
                    Add(black, 0, -0.06f, 0.06f, 0.06f, 0.14f, 0.07f, 0.2f); Barrel(0.02f, 0.14f, -0.39f); Add(grey, 0, 0.02f, 0.18f, 0.04f, 0.06f, 0.22f);
                    gp.slide = Part("slide", new Vector3(0.045f, 0.05f, -0.05f)); Add(grey, 0, 0, 0, 0.02f, 0.02f, 0.06f, 0, gp.slide); // Spannhebel
                    Add(tan, 0, -0.04f, -0.3f, 0.06f, 0.08f, 0.12f); // Vordergriff
                    muzzleZ = -0.47f; break;
                case "shotgun":
                    Barrel(0.028f, 0.72f, -0.38f, 0.06f); Barrel(0.024f, 0.6f, -0.3f, 0.01f, grey);
                    gp.slide = Part("slide", new Vector3(0, 0, -0.36f)); Add(wood, 0, 0, 0, 0.075f, 0.065f, 0.22f, 0, gp.slide); for (int k = 0; k < 4; k++) Add(black, 0, -0.034f, -0.08f + k * 0.05f, 0.08f, 0.004f, 0.01f, 0, gp.slide);
                    Add(black, 0, 0.04f, -0.02f, 0.085f, 0.11f, 0.26f); Add(wood, 0, -0.02f, 0.26f, 0.065f, 0.11f, 0.34f, 0.12f); Sights(0.1f, -0.72f, -0.1f);
                    Add(black, 0, -0.03f, 0.0f, 0.02f, 0.05f, 0.08f);
                    muzzleZ = -0.75f; muzzleY = 0.06f; break;
                case "rifle":
                    Add(olive, 0, 0.03f, -0.1f, 0.08f, 0.12f, 0.46f); Add(black, 0, 0.03f, -0.42f, 0.095f, 0.095f, 0.26f);
                    for (int k = 0; k < 6; k++) Add(black, 0, 0.095f, -0.3f + k * 0.04f, 0.03f, 0.012f, 0.02f); // Schiene
                    Barrel(0.018f, 0.24f, -0.62f, 0.04f); Add(black, 0, 0.04f, -0.73f, 0.03f, 0.03f, 0.05f);
                    gp.mag = Part("mag", new Vector3(0, -0.12f, -0.14f)); Add(black, 0, 0, 0, 0.06f, 0.2f, 0.08f, 0.25f, gp.mag);
                    Add(black, 0, -0.07f, 0.07f, 0.06f, 0.13f, 0.08f, 0.2f); Add(olive, 0, 0, 0.24f, 0.07f, 0.12f, 0.26f); Add(black, 0, -0.02f, 0.38f, 0.075f, 0.14f, 0.04f);
                    Add(black, 0, 0.12f, -0.08f, 0.03f, 0.05f, 0.1f); Sights(0.14f, -0.5f, -0.06f);
                    gp.slide = Part("slide", new Vector3(0.05f, 0.06f, -0.02f)); Add(grey, 0, 0, 0, 0.025f, 0.015f, 0.05f, 0, gp.slide);
                    Add(tan, 0, -0.04f, -0.36f, 0.05f, 0.08f, 0.1f);
                    muzzleZ = -0.75f; break;
                case "sniper":
                    Add(black, 0, 0.03f, -0.1f, 0.08f, 0.1f, 0.5f); Barrel(0.022f, 0.75f, -0.7f); Add(black, 0, 0.04f, -1.0f, 0.04f, 0.04f, 0.12f);
                    U.Cyl(g, black, 0, 0.13f, -0.1f, 0.04f, 0.34f, Mathf.PI / 2, 0, 0, false); U.Cyl(g, black, 0, 0.13f, -0.28f, 0.05f, 0.05f, Mathf.PI / 2, 0, 0, false); U.Cyl(g, lens, 0, 0.13f, -0.305f, 0.045f, 0.01f, Mathf.PI / 2, 0, 0, false);
                    U.Cyl(g, black, 0, 0.13f, 0.08f, 0.045f, 0.05f, Mathf.PI / 2, 0, 0, false); U.Cyl(g, lens, 0, 0.13f, 0.105f, 0.04f, 0.01f, Mathf.PI / 2, 0, 0, false);
                    Add(black, 0, 0.09f, -0.2f, 0.02f, 0.04f, 0.03f); Add(black, 0, 0.09f, 0.0f, 0.02f, 0.04f, 0.03f);
                    Add(wood, 0, -0.03f, 0.3f, 0.07f, 0.15f, 0.34f, 0.1f); Add(black, 0, -0.07f, 0.04f, 0.06f, 0.13f, 0.07f, 0.2f);
                    gp.mag = Part("mag", new Vector3(0, -0.1f, -0.14f)); Add(black, 0, 0, 0, 0.05f, 0.1f, 0.1f, 0, gp.mag);
                    gp.slide = Part("slide", new Vector3(0.06f, 0.06f, 0.0f)); Add(grey, 0, 0, 0, 0.05f, 0.02f, 0.02f, 0, gp.slide); // Kammerstengel
                    foreach (var sd in new[] { -1, 1 }) Add(black, sd * 0.06f, -0.12f, -0.55f, 0.015f, 0.2f, 0.015f, sd * 0.3f); // Zweibein
                    muzzleZ = -1.08f; break;
                case "launcher":
                    U.Cyl(g, green, 0, 0.06f, -0.22f, 0.075f, 0.62f, Mathf.PI / 2, 0, 0, false); U.Cyl(g, black, 0, 0.06f, -0.53f, 0.09f, 0.06f, Mathf.PI / 2, 0, 0, false);
                    Add(black, 0, -0.06f, 0.02f, 0.06f, 0.15f, 0.07f, 0.2f); U.Cyl(g, grey, 0, -0.04f, -0.12f, 0.11f, 0.12f, 0, 0, 0, false); Add(wood, 0, 0, 0.22f, 0.06f, 0.1f, 0.26f);
                    for (int k = 0; k < 4; k++) U.Cyl(g, U.Mat("#ffa000"), Mathf.Cos(k * 1.57f) * 0.065f, -0.04f + Mathf.Sin(k * 1.57f) * 0.065f, -0.18f, 0.025f, 0.04f, Mathf.PI / 2, 0, 0, false); // Granaten in der Trommel
                    Add(black, 0, 0.16f, -0.3f, 0.02f, 0.06f, 0.03f); Add(black, 0, 0.17f, 0.0f, 0.04f, 0.05f, 0.03f);
                    gp.slide = Part("slide", new Vector3(0, 0.06f, -0.53f)); gp.mag = null;
                    muzzleZ = -0.56f; muzzleY = 0.06f; break;
                case "minigun":
                    {
                        Add(grey, 0, 0, 0.02f, 0.17f, 0.17f, 0.36f); Add(black, 0, 0.14f, 0, 0.05f, 0.12f, 0.2f); Add(black, 0, -0.14f, 0.1f, 0.12f, 0.1f, 0.2f);
                        gp.spin = Pivot(g, "spin", new Vector3(0, 0, -0.2f));
                        for (int i = 0; i < 6; i++) { float a = i / 6f * Mathf.PI * 2; U.Cyl(gp.spin, black, Mathf.Cos(a) * 0.05f, Mathf.Sin(a) * 0.05f, -0.29f, 0.017f, 0.58f, Mathf.PI / 2, 0, 0, false); }
                        U.Cyl(gp.spin, grey, 0, 0, -0.5f, 0.085f, 0.04f, Mathf.PI / 2, 0, 0, false); U.Cyl(gp.spin, grey, 0, 0, -0.2f, 0.085f, 0.04f, Mathf.PI / 2, 0, 0, false);
                        Add(U.Mat("#795548"), 0.13f, -0.05f, 0.06f, 0.1f, 0.16f, 0.14f); Add(U.Mat("#795548"), -0.1f, -0.1f, -0.12f, 0.08f, 0.14f, 0.1f);
                        Add(black, 0.12f, -0.02f, 0.25f, 0.06f, 0.06f, 0.3f); // Munitionsgurt
                        muzzleZ = -0.82f; muzzleY = 0; break;
                    }
            }
            gp.muzzle = Pivot(g, "muzzle", new Vector3(0, muzzleY, muzzleZ));
            if (gp.slide) gp.slideHome = gp.slide.localPosition; if (gp.mag) gp.magHome = gp.mag.localPosition;
            if (tier > 0)
            {
                var col = U.C(D.upgrade.colors[Mathf.Min(tier, D.upgrade.colors.Length - 1)]);
                var glow = U.NewMat(col, 0, 0.7f, null, col, 1.6f); gp.glow = glow;
                float len = Mathf.Max(0.12f, -muzzleZ * 0.55f);
                for (int s = -1; s <= 1; s += 2) U.Box(g, glow, s * 0.045f, muzzleY + 0.02f, muzzleZ * 0.45f, 0.012f, 0.03f, len, 0, 0, 0, false);
                for (int i = 0; i < tier; i++) U.Cyl(g, glow, 0, muzzleY, muzzleZ + 0.06f + i * 0.05f, 0.035f, 0.015f, Mathf.PI / 2, 0, 0, false);
            }
            return gp;
        }

        // ------------------------------------------------------------------ Türme (zeigen nach +Z)
        public class TurretParts { public GameObject root; public Transform head, orb; public List<Transform> muzzles = new List<Transform>(); }
        public static TurretParts BuildTurret(string type, string color)
        {
            var tp = new TurretParts { root = new GameObject("turret_" + type) };
            var root = tp.root.transform;
            Material dark = U.Mat("#37474f", 0.4f, 0.5f), body = U.Mat(color, 0.3f, 0.5f), black = U.Mat("#1c1c1c", 0.5f, 0.5f);
            U.Box(root, dark, 0, 0.125f, 0, 1.1f, 0.25f, 1.1f); U.Box(root, U.Mat("#fbc02d"), 0, 0.31f, 0, 0.9f, 0.12f, 0.9f); U.Cyl(root, dark, 0, 0.65f, 0, 0.16f, 0.6f);
            tp.head = Pivot(root, "head", new Vector3(0, 1, 0));
            void Muzzle(float x, float y, float z) => tp.muzzles.Add(Pivot(tp.head, "muzzle", new Vector3(x, y, z)));
            if (type == "mg")
            {
                U.Box(tp.head, body, 0, 0.05f, 0, 0.55f, 0.36f, 0.62f); U.Box(tp.head, U.Mat("#8d6e63"), 0.38f, 0, -0.05f, 0.3f, 0.25f, 0.3f);
                foreach (var x in new[] { -0.12f, 0.12f }) { U.Cyl(tp.head, black, x, 0.08f, 0.6f, 0.045f, 0.75f, Mathf.PI / 2); Muzzle(x, 0.08f, 0.98f); }
                U.Box(tp.head, U.NewMat(U.C("#e53935"), 0, 0.5f, null, U.C("#e53935"), 0.8f), 0, 0.28f, 0.2f, 0.12f, 0.1f, 0.12f);
            }
            else if (type == "cannon")
            {
                U.Box(tp.head, body, 0, 0.12f, -0.05f, 0.8f, 0.6f, 0.8f); U.Cyl(tp.head, black, 0, 0.15f, 0.7f, 0.155f, 1.0f, Mathf.PI / 2); U.Cyl(tp.head, dark, 0, 0.15f, 1.18f, 0.2f, 0.12f, Mathf.PI / 2); Muzzle(0, 0.15f, 1.25f);
            }
            else
            {
                U.Cyl(tp.head, body, 0, 0.3f, 0, 0.26f, 0.8f);
                var ring = U.Mat("#ffb300", 0.8f, 0.7f);
                for (int i = 0; i < 4; i++) U.Cyl(tp.head, ring, 0, i * 0.22f, 0, 0.3f - i * 0.03f, 0.05f);
                var orbMat = U.NewMat(U.C("#b3e5fc"), 0, 0.6f, null, U.C("#40c4ff"), 2.2f);
                tp.orb = U.Sphere(tp.head, orbMat, 0, 1.0f, 0, 0.28f, false).transform;
                Muzzle(0, 1.0f, 0);
            }
            return tp;
        }

        // ------------------------------------------------------------------ Generator
        public class GenParts { public GameObject root; public Transform fan; public Material core; public List<Material> lamps = new List<Material>(); }
        public static GenParts BuildGenerator()
        {
            // Verwittertes Diesel-Stromaggregat: Betonsockel, Stahlrahmen, Motorgehäuse mit Kühlergrill und Lüfter, Auspuff, Tank, Schaltkasten, Kabel, Sandsäcke
            var gp = new GenParts { root = new GameObject("generator") }; var r = gp.root.transform;
            var nrm = U.NoiseNormal();
            Material concrete = U.MatN("#6e6a62", U.StoneTex(), nrm, 0.12f, 1f);
            Material olive = U.NewMat(U.C("#4a4d3f"), 0.55f, 0.32f, null, null, 0, nrm, 0.9f);
            Material oliveDark = U.NewMat(U.C("#363a30"), 0.55f, 0.3f, null, null, 0, nrm, 0.9f);
            Material rust = U.NewMat(U.C("#5a3a22"), 0.35f, 0.22f, null, null, 0, nrm, 1.2f);
            Material steel = U.NewMat(U.C("#3a3d40"), 0.8f, 0.45f, null, null, 0, nrm, 0.6f);
            Material black = U.NewMat(U.C("#1b1c1a"), 0.3f, 0.25f);
            Material grille = U.NewMat(U.C("#202221"), 0.7f, 0.3f);
            Material sand = U.NewMat(U.C("#5e5442"), 0, 0.12f, null, null, 0, nrm, 1.4f), sand2 = U.NewMat(U.C("#4f4738"), 0, 0.12f, null, null, 0, nrm, 1.4f);
            // Sockel + Rahmen
            U.Box(r, concrete, 0, 0.11f, 0, 3.2f, 0.22f, 2.4f);
            foreach (var z in new[] { -0.8f, 0.8f }) U.Box(r, rust, 0, 0.3f, z, 2.6f, 0.16f, 0.16f);
            foreach (var x in new[] { -1.2f, 1.2f }) U.Box(r, rust, x, 0.3f, 0, 0.16f, 0.16f, 1.76f);
            // Motorgehäuse (Kanzel) mit Blechstößen und Türgriffen
            U.Box(r, olive, 0.1f, 1.05f, 0, 2.0f, 1.35f, 1.3f);
            foreach (var x in new[] { -0.55f, 0.1f, 0.75f }) U.Box(r, oliveDark, x, 1.05f, 0, 0.03f, 1.37f, 1.32f, 0, 0, 0, false);
            foreach (var z in new[] { 0.66f, -0.66f }) { U.Box(r, black, -0.25f, 1.0f, z, 0.05f, 0.14f, 0.03f, 0, 0, 0, false); U.Box(r, black, 0.4f, 1.0f, z, 0.05f, 0.14f, 0.03f, 0, 0, 0, false); }
            U.Box(r, oliveDark, 0.1f, 1.76f, 0, 2.06f, 0.08f, 1.36f);
            // Kühlergrill vorne (-X) mit Lamellen, dahinter der Lüfter
            U.Box(r, grille, -0.93f, 1.05f, 0, 0.1f, 1.1f, 1.1f);
            for (int i = 0; i < 7; i++) U.Box(r, steel, -0.985f, 0.6f + i * 0.15f, 0, 0.02f, 0.03f, 1.05f, 0, 0, 0, false);
            gp.fan = Pivot(r, "fan", new Vector3(-0.9f, 1.05f, 0)); gp.fan.localEulerAngles = new Vector3(0, 0, 90);
            U.Cyl(gp.fan, black, 0, 0, 0, 0.12f, 0.1f);
            for (int i = 0; i < 5; i++) U.Box(gp.fan, steel, 0, 0, 0, 0.9f, 0.02f, 0.16f, 0.35f, i * Mathf.PI / 5, 0, false);
            // Auspuff mit Regenklappe, Tank, Einfüllstutzen
            U.Cyl(r, rust, 0.75f, 2.25f, -0.35f, 0.07f, 1.0f); U.Cyl(r, black, 0.75f, 2.78f, -0.35f, 0.1f, 0.06f, 0.35f);
            U.Cyl(r, rust, 0.1f, 0.62f, 0.85f, 0.22f, 1.6f, 0, 0, Mathf.PI / 2); U.Cyl(r, black, 0.4f, 0.88f, 0.85f, 0.06f, 0.08f);
            foreach (var x in new[] { -0.6f, 0.8f }) U.Box(r, steel, x, 0.48f, 0.85f, 0.06f, 0.5f, 0.5f);
            // Schaltkasten mit Anzeige (core = Anzeigeleuchte) und Statuslampen
            U.Box(r, oliveDark, 1.3f, 1.0f, 0.2f, 0.4f, 0.7f, 0.5f);
            gp.core = U.NewMat(U.C("#3a2a10"), 0, 0.6f, null, U.C("#ffb74d"), 1.0f);
            U.Box(r, gp.core, 1.505f, 1.12f, 0.2f, 0.01f, 0.18f, 0.28f, 0, 0, 0, false);
            foreach (var (z, c) in new[] { (0.05f, "#ff1744"), (0.2f, "#ff1744"), (0.35f, "#76ff03") }) { var lm = U.NewMat(U.C(c), 0, 0.4f, null, U.C(c), 1f); if (c != "#76ff03") gp.lamps.Add(lm); U.Box(r, lm, 1.505f, 0.88f, z, 0.01f, 0.05f, 0.05f, 0, 0, 0, false); }
            // Rundumleuchte oben
            U.Cyl(r, black, -0.5f, 1.84f, 0.3f, 0.08f, 0.08f);
            var beacon = U.NewMat(new Color(1f, 0.55f, 0.1f, 0.9f), 0, 0.7f, null, U.C("#ff9100"), 1.5f); gp.lamps.Add(beacon);
            U.Cyl(r, beacon, -0.5f, 1.95f, 0.3f, 0.07f, 0.16f);
            // Kabel vom Schaltkasten zum Boden und zur Kanzel
            U.Cyl(r, black, 1.6f, 0.45f, 0.1f, 0.025f, 1.0f, 0.5f, 0, 0.35f); U.Cyl(r, black, 1.65f, 0.35f, 0.35f, 0.025f, 0.9f, 0.9f, 0, 0.2f);
            U.Cyl(r, black, 2.0f, 0.12f, -0.4f, 0.03f, 1.4f, 0, 0.6f, Mathf.PI / 2);
            // Kanister und Sandsäcke
            U.Box(r, U.NewMat(U.C("#3d4a2e"), 0.3f, 0.3f, null, null, 0, nrm, 0.8f), -1.1f, 0.45f, 0.95f, 0.32f, 0.46f, 0.18f, 0, 0.3f, 0);
            U.Box(r, U.NewMat(U.C("#5a3a22"), 0.3f, 0.3f, null, null, 0, nrm, 0.8f), -1.35f, 0.45f, 0.8f, 0.32f, 0.46f, 0.18f, 0, -0.2f, 0);
            var rnd = U.Rng(5);
            // Sandsäcke: flache, leicht gestauchte Kapseln in zwei Lagen, versetzt wie gemauert
            for (int i = 0; i < 16; i++) { float a = -0.7f + i * (Mathf.PI * 1.3f / 16f); float rad = 2.05f + (rnd() - 0.5f) * 0.12f; var mat = rnd() > 0.5f ? sand : sand2; U.Prim(PrimitiveType.Capsule, r, mat, new Vector3(Mathf.Cos(a) * rad, 0.13f, Mathf.Sin(a) * rad), new Vector3(0.36f, 0.3f, 0.26f), new Vector3(0, -a * Mathf.Rad2Deg + (rnd() - 0.5f) * 20f, 90f)); if (i % 2 == 1 && i < 15) U.Prim(PrimitiveType.Capsule, r, mat, new Vector3(Mathf.Cos(a + 0.12f) * rad, 0.35f, Mathf.Sin(a + 0.12f) * rad), new Vector3(0.36f, 0.3f, 0.26f), new Vector3(0, -(a + 0.12f) * Mathf.Rad2Deg + (rnd() - 0.5f) * 20f, 90f)); }
            // Arbeitsleuchte am Mast (warmes Licht)
            U.Cyl(r, steel, -1.4f, 1.6f, -1.0f, 0.03f, 3.0f); U.Box(r, black, -1.3f, 3.05f, -0.95f, 0.3f, 0.18f, 0.22f, 0.5f, 0.6f, 0);
            var lgo = new GameObject("worklight"); lgo.transform.SetParent(r, false); lgo.transform.localPosition = new Vector3(-1.2f, 2.95f, -0.85f); lgo.transform.localRotation = Quaternion.Euler(60, 50, 0);
            var li = lgo.AddComponent<Light>(); li.type = LightType.Spot; li.color = U.C("#ffd9a0"); li.range = 14; li.spotAngle = 80; li.innerSpotAngle = 40; li.shadows = LightShadows.None;
            var hd = lgo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(); hd.SetIntensity(2500, UnityEngine.Rendering.LightUnit.Lumen);
            return gp;
        }

        public static GameObject BuildShop()
        {
            var root = new GameObject("shop"); var r = root.transform;
            U.Box(r, U.Mat("#1a237e", 0.3f, 0.4f), 0, 0.85f, 0, 1.4f, 1.7f, 0.9f);
            var screen = U.NewMat(U.C("#1565c0"), 0, 0.6f, null, U.C("#64b5f6"), 0.8f);
            foreach (var z in new[] { 0.452f, -0.452f }) U.Box(r, screen, 0, 1.25f, z, 1.1f, 0.6f, 0.01f, 0, 0, 0, false);
            U.Box(r, U.Mat("#ffca28"), 0, 1.76f, 0, 1.5f, 0.12f, 1.0f);
            U.Box(r, U.NewMat(U.C("#ffca28"), 0, 0.4f, null, U.C("#ffca28"), 0.4f), 0, 2.25f, 0, 2.0f, 0.38f, 0.08f, 0, 0, 0, false);
            U.Box(r, U.Mat("#424242"), -0.6f, 1.95f, 0, 0.08f, 0.5f, 0.08f); U.Box(r, U.Mat("#424242"), 0.6f, 1.95f, 0, 0.08f, 0.5f, 0.08f);
            U.Label(r, "SHOP  [B]", U.C("#ffeb3b"), 2.7f, 0.4f);
            return root;
        }

        public class CrateParts { public GameObject root; public Transform lid, sign; public Material glow; }
        public static CrateParts BuildCrate()
        {
            var cp = new CrateParts { root = new GameObject("crate") }; var r = cp.root.transform;
            Material wood = U.Mat("#6d4c41"), gold = U.Mat("#ffca28", 0.6f, 0.7f), dark = U.Mat("#3e2723");
            U.Box(r, wood, 0, 0.35f, 0, 1.3f, 0.7f, 0.9f);
            foreach (var x in new[] { -0.5f, 0f, 0.5f }) U.Box(r, gold, x, 0.36f, 0, 0.08f, 0.72f, 0.92f, 0, 0, 0, false);
            cp.lid = Pivot(r, "lid", new Vector3(0, 0.7f, -0.45f));
            U.Box(cp.lid, dark, 0, 0.1f, 0.45f, 1.32f, 0.2f, 0.92f);
            foreach (var x in new[] { -0.5f, 0f, 0.5f }) U.Box(cp.lid, gold, x, 0.1f, 0.45f, 0.08f, 0.22f, 0.94f, 0, 0, 0, false);
            cp.glow = U.Transparent(new Color(1, 0.84f, 0, 0)); U.SetEmission(cp.glow, U.C("#ffd600"), 1.2f);
            U.Prim(PrimitiveType.Cube, r, cp.glow, new Vector3(0, 0.9f, 0), new Vector3(1.1f, 0.6f, 0.7f), null, false, false);
            cp.sign = Pivot(r, "sign", new Vector3(0, 1.7f, 0));
            U.Box(cp.sign, U.Mat("#1a237e"), 0, 0, 0, 0.5f, 0.5f, 0.06f, 0, 0, 0, false);
            var q = U.Label(cp.sign, "?", U.C("#ffd600"), 0, 0.5f); q.transform.localPosition = new Vector3(0, 0, 0.04f); Object.Destroy(q.GetComponent<Billboard>());
            return cp;
        }

        /// <summary>Verwittertes Holz (Bretterstruktur + Maserungs-Normalmap), gedeckte Farben passend zur Ruinenstadt</summary>
        public static Material Wood(string hex, float smooth = 0.16f) => U.MatN(hex, U.PlankTex(), U.PlankNormal(), smooth, 1.1f);
        static Material RustMetal => U.NewMat(U.C("#4a4441"), 0.75f, 0.32f, null, null, 0, U.NoiseNormal(), 0.9f);
        public static GameObject BuildBarricade()
        {
            var root = new GameObject("barricade"); var r = root.transform;
            Material wood = Wood("#6a5a46"), dark = Wood("#45382c"), metal = RustMetal;
            foreach (var x in new[] { -1.05f, 1.05f }) U.Box(r, dark, x, 0.6f, 0, 0.18f, 1.2f, 0.18f);
            foreach (var (y, rz) in new[] { (0.3f, 0.03f), (0.65f, -0.02f), (1.0f, 0.04f) }) U.Box(r, wood, 0, y, 0.1f, 2.6f, 0.22f, 0.08f, 0, 0, rz);
            foreach (var x in new[] { -0.8f, 0.8f }) U.Box(r, dark, x, 0.55f, -0.35f, 0.12f, 1.3f, 0.12f, -0.5f);
            for (int i = 0; i < 5; i++) U.Box(r, metal, -1 + i * 0.5f, 1.22f, 0, 0.06f, 0.06f, 0.3f, 0, 0.6f, 0, false);
            U.Box(r, metal, 0, 1.25f, 0, 2.4f, 0.03f, 0.03f, 0, 0, 0, false);
            return root;
        }

        /// <summary>Brett (Schaufel): zwei Pflöcke, drei angenagelte, verwitterte Bretter, 1,6 m breit</summary>
        public static GameObject BuildBoard()
        {
            var root = new GameObject("board"); var r = root.transform;
            var rnd = U.Rng(root.GetInstanceID() & 0xffff);
            Material[] woods = { Wood("#6b5b47"), Wood("#5c4c3a"), Wood("#766551"), Wood("#4f4234") };
            foreach (var x in new[] { -0.62f, 0.62f }) U.Box(r, woods[3], x, 0.5f, -0.04f, 0.1f, 1.0f + rnd() * 0.08f, 0.1f, 0, 0, (rnd() - 0.5f) * 0.05f);
            foreach (var (y, rz) in new[] { (0.28f, 0.03f), (0.55f, -0.025f), (0.82f, 0.04f) })
            {
                var pl = U.Box(r, woods[(int)(rnd() * 3)], (rnd() - 0.5f) * 0.1f, y, 0.03f, 1.6f + rnd() * 0.12f, 0.17f + rnd() * 0.04f, 0.045f, 0, 0, rz + (rnd() - 0.5f) * 0.02f);
                foreach (var x in new[] { -0.62f, 0.62f }) U.Cyl(r, RustMetal, x + (rnd() - 0.5f) * 0.03f, y + (rnd() - 0.5f) * 0.04f, 0.06f, 0.012f, 0.02f, Mathf.PI / 2, 0, 0, false);
            }
            return root;
        }
        /// <summary>Bretter flach über eine Grube gelegt (Falle): fünf lose Planken quer</summary>
        public static GameObject BuildPitCover(float radius)
        {
            var root = new GameObject("pitcover"); var r = root.transform;
            var rnd = U.Rng(root.GetInstanceID() & 0xffff);
            Material[] woods = { Wood("#6b5b47"), Wood("#5c4c3a"), Wood("#766551") };
            int n = 5; float span = radius * 2.1f;
            for (int i = 0; i < n; i++)
            {
                float z = -radius * 0.8f + i * (radius * 1.6f / (n - 1)) + (rnd() - 0.5f) * 0.08f;
                float len = Mathf.Sqrt(Mathf.Max(0.2f, radius * radius * 1.25f - z * z)) * 2 + 0.5f;
                U.Box(r, woods[i % 3], (rnd() - 0.5f) * 0.25f, 0.035f + (i % 2) * 0.02f, z, Mathf.Min(span, len), 0.05f, 0.2f + rnd() * 0.05f, 0, (rnd() - 0.5f) * 0.12f, (rnd() - 0.5f) * 0.03f);
            }
            return root;
        }
        /// <summary>Grube: dunkler Erd-Decal auf dem Boden, Aushub als Erdwall ringsum</summary>
        public static GameObject BuildPit(float radius)
        {
            var root = new GameObject("pit"); var r = root.transform;
            var rnd = U.Rng(root.GetInstanceID() & 0xffff);
            if (U.DecalBase != null)
            {
                var dgo = new GameObject("hole"); dgo.transform.SetParent(r, false); dgo.transform.localPosition = new Vector3(0, 0.6f, 0); dgo.transform.localRotation = Quaternion.Euler(90, rnd() * 360, 0);
                var dp = dgo.AddComponent<UnityEngine.Rendering.HighDefinition.DecalProjector>();
                var dm = new Material(U.DecalBase); dm.SetTexture("_BaseColorMap", U.PitTex()); dm.EnableKeyword("_COLORMAP"); dm.SetColor("_BaseColor", new Color(0.09f, 0.06f, 0.04f, 1f)); dm.SetFloat("_Smoothness", 0.05f); dm.SetFloat("_DecalBlend", 1f);
                dp.material = dm; dp.pivot = new Vector3(0, 0, 0.6f); dp.size = new Vector3(radius * 2.3f, radius * 2.3f, 1.2f); dp.drawDistance = 150;
            }
            var soil = U.NewMat(U.C("#3f3024"), 0, 0.1f, U.StoneTex(), null, 0, U.NoiseNormal(), 1.4f);
            var soil2 = U.NewMat(U.C("#4a3a2c"), 0, 0.1f, U.StoneTex(), null, 0, U.NoiseNormal(), 1.4f);
            int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n + (rnd() - 0.5f) * 0.3f, rad = radius * (1.02f + rnd() * 0.18f);
                float sx = 0.3f + rnd() * 0.3f, sy = 0.12f + rnd() * 0.12f, sz = 0.25f + rnd() * 0.25f;
                var clump = U.Prim(PrimitiveType.Sphere, r, rnd() > 0.5f ? soil : soil2, new Vector3(Mathf.Cos(a) * rad, sy * 0.25f, Mathf.Sin(a) * rad), new Vector3(sx, sy, sz), new Vector3(0, -a * Mathf.Rad2Deg + rnd() * 40, 0));
            }
            for (int i = 0; i < 10; i++) { float a = rnd() * Mathf.PI * 2, rad = radius * (1.25f + rnd() * 0.5f); U.Prim(PrimitiveType.Sphere, r, soil, new Vector3(Mathf.Cos(a) * rad, 0.02f, Mathf.Sin(a) * rad), new Vector3(0.08f + rnd() * 0.1f, 0.05f, 0.08f + rnd() * 0.1f), null, false, false); }
            return root;
        }
        /// <summary>Schaufel (Waffenmodell): Holzstiel, rostiges Stahlblatt, D-Griff. Ursprung am unteren Stielende, Blatt zeigt nach -Z (vorn)</summary>
        public static GameObject BuildShovel()
        {
            var root = new GameObject("shovel"); var r = root.transform;
            var wood = U.MatN("#5a4632", U.BarkTex(), U.NoiseNormal(), 0.2f, 0.7f);
            var steel = U.NewMat(U.C("#55585a"), 0.9f, 0.42f, null, null, 0, U.NoiseNormal(), 0.5f);
            var rust = U.NewMat(U.C("#5a3b26"), 0.5f, 0.25f, null, null, 0, U.NoiseNormal(), 1.1f);
            // Stiel entlang -Z (nach vorn), 1,05 m
            U.Cyl(r, wood, 0, 0, -0.5f, 0.021f, 1.0f, Mathf.PI / 2);
            // D-Griff hinten
            U.Box(r, steel, 0, 0, 0.03f, 0.03f, 0.03f, 0.08f); U.Box(r, wood, 0, 0.065f, 0.12f, 0.028f, 0.13f, 0.028f); U.Box(r, wood, 0, -0.065f, 0.12f, 0.028f, 0.13f, 0.028f); U.Cyl(r, wood, 0, 0, 0.18f, 0.016f, 0.14f);
            // Zwinge und Blatt vorn: leicht geknickt, Rost an den Kanten
            U.Cyl(r, rust, 0, 0, -1.0f, 0.03f, 0.1f, Mathf.PI / 2);
            var blade = new GameObject("blade").transform; blade.SetParent(r, false); blade.localPosition = new Vector3(0, -0.02f, -1.06f); blade.localRotation = Quaternion.Euler(12, 0, 0);
            U.Box(blade, steel, 0, 0, -0.14f, 0.22f, 0.012f, 0.28f);
            U.Box(blade, rust, 0, 0.004f, -0.27f, 0.2f, 0.01f, 0.05f); U.Box(blade, rust, -0.105f, 0.004f, -0.14f, 0.02f, 0.01f, 0.28f); U.Box(blade, rust, 0.105f, 0.004f, -0.14f, 0.02f, 0.01f, 0.28f);
            U.Box(blade, steel, 0, 0.02f, -0.03f, 0.12f, 0.03f, 0.08f); // Tritt
            foreach (var t in root.GetComponentsInChildren<Renderer>()) { t.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; t.receiveShadows = false; }
            return root;
        }

        public class PickupParts { public GameObject root; public Transform core, ring; public Material mat; public TextMesh label; }
        public static PickupParts BuildPickup(PowerupDef cfg)
        {
            var pp = new PickupParts { root = new GameObject("pickup_" + cfg.id) }; var r = pp.root.transform;
            var col = U.C(cfg.color);
            pp.mat = U.NewMat(col, 0, 0.7f, null, col, 1.4f);
            pp.core = U.Prim(PrimitiveType.Cube, r, pp.mat, new Vector3(0, 1, 0), Vector3.one * 0.45f, new Vector3(45, 0, 45), false, false).transform;
            pp.ring = Pivot(r, "ring", new Vector3(0, 1, 0));
            for (int i = 0; i < 12; i++) { float a = i / 12f * Mathf.PI * 2; U.Box(pp.ring, pp.mat, Mathf.Cos(a) * 0.5f, 0, Mathf.Sin(a) * 0.5f, 0.1f, 0.06f, 0.22f, 0, -a, 0, false); }
            pp.label = U.Label(r, cfg.icon + " " + cfg.name, col, 1.8f, 0.3f);
            var beam = U.Prim(PrimitiveType.Cylinder, r, U.UnlitMat(new Color(col.r, col.g, col.b, 0.18f), true), new Vector3(0, 3, 0), new Vector3(0.4f, 3, 0.4f), null, false, false);
            return pp;
        }
    }
}
