// Baut die 3D-Welt einer Karte: Boden, Gebäude, Deko, Grenze, Licht, Himmel, Kollisionskörper
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class World
    {
        public GameObject root;
        public MapDef map;
        public Models.GenParts gen;
        public Models.CrateParts crate;
        public GameObject shop;
        public Light sun;
        public List<Transform> clouds = new List<Transform>();
        public float crateSpin;
        GameObject staticRoot;
        bool night; int lights;
        float t;

        static Material TexMat(string hex, Texture2D tex, float smooth = 0.3f)
        {
            Texture2D n = tex == U.BrickTex() ? U.BrickNormal() : tex == U.PlankTex() ? U.PlankNormal() : tex == U.RibTex() ? U.RibNormal() : null;
            return n ? U.MatN(hex, tex, n, smooth) : U.Mat(hex, 0.05f, smooth, tex);
        }

        public static World Build(MapDef map, string quality)
        {
            var w = new World { map = map, night = map.night };
            w.root = new GameObject("World");
            w.staticRoot = new GameObject("Static"); w.staticRoot.transform.SetParent(w.root.transform, false);
            var S = w.staticRoot.transform;

            // Umgebung
            w.BuildEnvironmentVolume();

            // Boden
            float gsize = map.size + 90;
            var gt = U.GroundTex(map.ground);
            var gm = U.NewMat(Color.white, 0, 0.12f, gt, null, 0, U.StudNormal(), 0.6f); gm.SetTextureScale("_BaseColorMap", new Vector2(gsize / 4, gsize / 4)); gm.SetTextureScale("_NormalMap", new Vector2(gsize / 4, gsize / 4));
            var groundGo = U.Prim(PrimitiveType.Plane, S, gm, Vector3.zero, new Vector3(gsize / 10, 1, gsize / 10), null, true, true, Layers.Ground);
            groundGo.name = "ground"; groundGo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // Bodenmarkierungen
            int di = 0;
            foreach (var dc in map.decals)
            {
                var m = U.Prim(PrimitiveType.Quad, S, U.Mat(dc.color, 0, 0.1f), new Vector3(dc.x, 0.012f + di * 0.004f, dc.z), new Vector3(dc.w, dc.d, 1), new Vector3(90, 0, 0), false, false);
                m.GetComponent<Renderer>().receiveShadows = true; di++;
                if (dc.kind == "road")
                {
                    bool along = dc.d > dc.w; float L = along ? dc.d : dc.w;
                    var white = U.Mat("#ffffff", 0, 0.1f);
                    for (float tt = -L / 2 + 2; tt < L / 2; tt += 4)
                    {
                        if (Mathf.Abs(tt) < 6) continue;
                        U.Prim(PrimitiveType.Quad, S, white, new Vector3(along ? dc.x : tt, 0.05f, along ? tt : dc.z), new Vector3(along ? 0.2f : 1.8f, along ? 1.8f : 0.2f, 1), new Vector3(90, 0, 0), false, false);
                    }
                }
            }

            foreach (var o in map.objects) w.AddObject(o, S);
            w.AddBorder(S);
            w.AddScenery(S);
            w.AddVegetation(S);

            // Generator, Shop, Kiste
            w.gen = Models.BuildGenerator(); w.gen.root.transform.SetParent(w.root.transform, false); w.gen.root.transform.position = new Vector3(map.gen.x, 0, map.gen.y);
            var gl = new GameObject("genlight").AddComponent<Light>(); gl.type = LightType.Point; gl.color = U.C("#00e5ff"); gl.range = 12; gl.transform.SetParent(w.root.transform, false); gl.transform.position = new Vector3(map.gen.x, 2.6f, map.gen.y); HdLight(gl, map.night ? 2500 : 1200);
            w.shop = Models.BuildShop(); w.shop.transform.SetParent(w.root.transform, false); w.shop.transform.position = new Vector3(map.shop.x, 0, map.shop.y);
            w.shop.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(map.shop.x - map.gen.x, map.shop.y - map.gen.y) * Mathf.Rad2Deg, 0);
            if (map.crate.HasValue)
            {
                var c = map.crate.Value;
                w.crate = Models.BuildCrate(); w.crate.root.transform.SetParent(w.root.transform, false); w.crate.root.transform.position = new Vector3(c.x, 0, c.y);
                w.crate.root.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(map.gen.x - c.x, map.gen.y - c.y) * Mathf.Rad2Deg, 0);
            }

            // Sonne
            var sunGo = new GameObject("sun"); sunGo.transform.SetParent(w.root.transform, false);
            w.sun = sunGo.AddComponent<Light>(); w.sun.type = LightType.Directional; w.sun.color = map.sunColor;
            w.sun.shadows = quality == "low" ? LightShadows.None : LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.LookRotation(-map.sunPos.normalized);
            var sunHd = sunGo.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            sunHd.SetIntensity(map.night ? 0.6f * map.sunIntensity : 25000f * map.sunIntensity / 2.2f, UnityEngine.Rendering.LightUnit.Lux);
            sunHd.angularDiameter = 1.2f; sunHd.EnableShadows(quality != "low"); sunHd.SetShadowResolution(quality == "high" ? 4096 : 2048); sunHd.shadowDimmer = 0.9f; sunHd.volumetricDimmer = 1f; sunHd.affectsVolumetric = true;
            if (map.night) w.sun.color = new Color(0.55f, 0.65f, 1f);

            // Kollisionskörper aus den gemeinsamen Daten
            var col = new GameObject("Colliders"); col.transform.SetParent(w.root.transform, false);
            foreach (var s in map.solids)
            {
                var g = new GameObject("solid_" + s.kind); g.transform.SetParent(col.transform, false); g.layer = Layers.Solid;
                g.transform.position = new Vector3((s.minX + s.maxX) / 2, (s.minY + s.maxY) / 2, (s.minZ + s.maxZ) / 2);
                var bc = g.AddComponent<BoxCollider>(); bc.size = new Vector3(s.maxX - s.minX, s.maxY - s.minY, s.maxZ - s.minZ);
            }
            // Kartenrand (unsichtbare Wände)
            float half = map.size / 2;
            foreach (var (x, z, sx, sz) in new[] { (0f, -half - 1, map.size + 4, 2f), (0f, half + 1, map.size + 4, 2f), (-half - 1, 0f, 2f, map.size + 4), (half + 1, 0f, 2f, map.size + 4) })
            {
                var g = new GameObject("edge"); g.transform.SetParent(col.transform, false); g.layer = Layers.Solid; g.transform.position = new Vector3(x, 5, z);
                g.AddComponent<BoxCollider>().size = new Vector3(sx, 10, sz);
            }

            w.BuildSky();
            StaticBatchingUtility.Combine(w.staticRoot);
            return w;
        }

        void AddObject(MapObj o, Transform S)
        {
            float x = o.x, z = o.z, w = o.w, d = o.d, h = o.h, y0 = o.y; string c = o.color;
            string roof = o.roof ?? "#8d6e63"; string leaf = o.raw != null ? J.S(o.raw, "leaf", "#43a047") : "#43a047";
            switch (o.kind)
            {
                case "house":
                    {
                        U.Box(S, TexMat(c, U.BrickTex()), x, h / 2, z, w, h, d);
                        float rh = Mathf.Min(w, d) * 0.42f;
                        var r = U.Cone(S, U.Mat(roof), x, h + rh / 2, z, 0.5f, rh, 4, Mathf.PI / 4); r.transform.localScale = new Vector3((w + 1), rh, (d + 1));
                        U.Box(S, U.Mat("#8d6e63"), x + w * 0.25f, h + rh * 0.5f, z - d * 0.15f, 0.8f, 1.6f, 0.8f);
                        bool toX = Mathf.Abs(x) > Mathf.Abs(z); float sx = x == 0 ? 1 : -Mathf.Sign(x), sz = z == 0 ? 1 : -Mathf.Sign(z);
                        if (toX) U.Box(S, U.Mat("#5d4037"), x + sx * (w / 2 + 0.03f), 1.1f, z, 0.12f, 2.2f, 1.3f); else U.Box(S, U.Mat("#5d4037"), x, 1.1f, z + sz * (d / 2 + 0.03f), 1.3f, 2.2f, 0.12f);
                        var glass = night ? U.Mat("#90caf9", 0, 0.8f, null, "#ffd54f", 0.6f) : U.Mat("#90caf9", 0, 0.8f);
                        foreach (var f in new[] { -1, 1 }) foreach (var k in new[] { -1, 1 }) { U.Box(S, glass, x + k * w * 0.27f, h * 0.62f, z + f * (d / 2 + 0.03f), 1.2f, 1.1f, 0.1f); U.Box(S, glass, x + f * (w / 2 + 0.03f), h * 0.62f, z + k * d * 0.27f, 0.1f, 1.1f, 1.2f); }
                        break;
                    }
                case "barn":
                    {
                        U.Box(S, TexMat(c, U.PlankTex()), x, h / 2, z, w, h, d);
                        float rh = w * 0.38f; U.Gable(S, U.Mat(roof), x, h, z, w + 0.8f, rh, d + 0.8f);
                        foreach (var f in new[] { -1, 1 })
                        {
                            U.Box(S, U.Mat("#ffffff"), x, 2.25f, z + f * (d / 2 + 0.03f), 4, 4.5f, 0.12f); U.Box(S, U.Mat("#8e2020"), x, 2.25f, z + f * (d / 2 + 0.04f), 3.6f, 4.1f, 0.14f);
                            U.Box(S, U.Mat("#ffffff"), x, 2.25f, z + f * (d / 2 + 0.06f), 5.2f, 0.25f, 0.16f, 0, 0, 0.83f); U.Box(S, U.Mat("#ffffff"), x, 2.25f, z + f * (d / 2 + 0.06f), 5.2f, 0.25f, 0.16f, 0, 0, -0.83f);
                        }
                        break;
                    }
                case "silo":
                    U.Cyl(S, TexMat(c, U.RibTex(), 0.5f), x, h / 2, z, w / 2, h);
                    { var s = U.Sphere(S, U.Mat("#78909c", 0.4f, 0.5f), x, h, z, w / 2); s.transform.localScale = new Vector3(w, w * 0.6f, w); }
                    for (int i = 1; i < 4; i++) U.Cyl(S, U.Mat("#607d8b"), x, (h / 4) * i, z, w / 2 + 0.05f, 0.15f);
                    break;
                case "church":
                    {
                        U.Box(S, TexMat(c, U.BrickTex()), x, h / 2, z, w, h, d);
                        float rh = w * 0.5f; U.Gable(S, U.Mat(roof), x, h, z, w + 0.6f, rh, d + 0.6f);
                        float tz = z + d / 2 - 1.6f;
                        U.Box(S, TexMat(c, U.BrickTex()), x, (h + 5) / 2, tz, 3.2f, h + 5, 3.2f);
                        var sp = U.Cone(S, U.Mat(roof), x, h + 5 + 2.25f, tz, 2.4f, 4.5f, 4, Mathf.PI / 4);
                        U.Box(S, U.Mat("#ffd54f"), x, h + 5 + 5.2f, tz, 0.25f, 1.6f, 0.25f); U.Box(S, U.Mat("#ffd54f"), x, h + 5 + 5.4f, tz, 0.9f, 0.25f, 0.25f);
                        var stained = U.Mat("#7e57c2", 0, 0.5f, null, "#7e57c2", night ? 1.2f : 0.2f);
                        foreach (var f in new[] { -1, 1 }) for (int i = -1; i <= 1; i++) U.Box(S, stained, x + f * (w / 2 + 0.04f), h * 0.55f, z + i * 3.2f, 0.12f, 2.2f, 1.0f);
                        break;
                    }
                case "crypt":
                    U.Box(S, U.Mat(c), x, h / 2, z, w, h, d); U.Box(S, U.Mat("#78909c"), x, h + 0.2f, z, w + 0.6f, 0.4f, d + 0.6f);
                    U.Gable(S, U.Mat("#b0bec5"), x, h + 0.4f, z + d / 2 - 0.2f, w + 0.4f, 1.4f, 1.2f);
                    for (float i = -1.5f; i <= 1.5f; i += 1) U.Cyl(S, U.Mat("#cfd8dc"), x + i * (w / 4), h / 2, z + d / 2 + 0.5f, 0.23f, h);
                    U.Box(S, U.Mat("#212121"), x, 1.3f, z + d / 2 + 0.03f, 1.6f, 2.6f, 0.1f);
                    break;
                case "car": case "truck":
                    {
                        bool along = d > w; float L = along ? d : w, W = along ? w : d; bool isTruck = o.kind == "truck";
                        var grp = new GameObject(o.kind).transform; grp.SetParent(S, false); grp.position = new Vector3(x, 0, z); if (!along) grp.rotation = Quaternion.Euler(0, 90, 0);
                        U.Box(grp, U.Mat(c, 0.4f, 0.65f), 0, isTruck ? 0.95f : 0.6f, 0, W, isTruck ? 1.0f : 0.7f, L);
                        if (isTruck)
                        {
                            U.Box(grp, U.Mat(c), 0, 1.95f, L * 0.33f, W * 0.95f, 1.1f, L * 0.3f); U.Box(grp, U.Mat("#263238", 0, 0.9f), 0, 2.0f, L * 0.48f + 0.02f, W * 0.9f, 0.5f, 0.05f); U.Box(grp, U.Mat("#3b4220"), 0, 2.1f, -L * 0.18f, W * 1.02f, 1.3f, L * 0.62f);
                        }
                        else
                        {
                            U.Box(grp, U.Mat("#263238", 0.2f, 0.9f), 0, 1.25f, -L * 0.05f, W * 0.88f, 0.6f, L * 0.5f); U.Box(grp, U.Mat(c), 0, 1.58f, -L * 0.05f, W * 0.9f, 0.08f, L * 0.52f);
                            foreach (var s in new[] { -1, 1 }) U.Box(grp, U.Mat("#fff9c4", 0, 0.5f, null, "#fff59d", night ? 1.5f : 0.3f), s * W * 0.32f, 0.7f, L / 2 + 0.02f, 0.35f, 0.18f, 0.05f);
                        }
                        foreach (var sx in new[] { -1, 1 }) foreach (var sz in new[] { -1, 1 }) U.Cyl(grp, U.Mat("#212121"), sx * (W / 2 - 0.05f), 0.36f, sz * L * 0.32f, 0.36f, 0.3f, 0, 0, Mathf.PI / 2);
                        break;
                    }
                case "tractor":
                    {
                        var grp = new GameObject("tractor").transform; grp.SetParent(S, false); grp.position = new Vector3(x, 0, z);
                        U.Box(grp, U.Mat(c), 0, 1.2f, 0.2f, 1.4f, 0.9f, 3.2f); U.Box(grp, U.Mat("#263238", 0, 0.9f), 0, 2.0f, -0.8f, 1.6f, 1.3f, 1.4f); U.Box(grp, U.Mat(c), 0, 2.7f, -0.8f, 1.8f, 0.12f, 1.6f); U.Cyl(grp, U.Mat("#424242"), 0.45f, 2.3f, 0.9f, 0.08f, 1.2f);
                        foreach (var s in new[] { -1, 1 }) { U.Cyl(grp, U.Mat("#212121"), s * 1.0f, 0.85f, -0.9f, 0.85f, 0.5f, 0, 0, Mathf.PI / 2); U.Cyl(grp, U.Mat("#fdd835"), s * 1.01f, 0.85f, -0.9f, 0.5f, 0.52f, 0, 0, Mathf.PI / 2); U.Cyl(grp, U.Mat("#212121"), s * 0.85f, 0.48f, 1.4f, 0.48f, 0.35f, 0, 0, Mathf.PI / 2); }
                        break;
                    }
                case "tree": case "pine":
                    {
                        float s = h / 6; var bark = U.MatN(c, U.BarkTex(), U.NoiseNormal(), 0.2f, 0.8f);
                        U.Cyl(S, bark, x, h * 0.3f, z, w * 0.55f, h * 0.6f); U.Cyl(S, bark, x, 0.15f, z, w * 0.8f, 0.3f);
                        var rr = U.Rng(Mathf.RoundToInt(x * 7 + z * 13));
                        var lc = U.C(leaf);
                        if (o.kind == "tree")
                        {
                            // Äste + mehrere Laubballen in zwei Grüntönen
                            for (int b = 0; b < 3; b++) { float a = rr() * Mathf.PI * 2; U.Box(S, bark, x + Mathf.Cos(a) * 0.6f * s, h * (0.5f + b * 0.08f), z + Mathf.Sin(a) * 0.6f * s, 0.18f, 1.4f * s, 0.18f, Mathf.Sin(a) * 0.9f, 0, -Mathf.Cos(a) * 0.9f); }
                            for (int b = 0; b < 5; b++)
                            {
                                float a = rr() * Mathf.PI * 2, rad = (b == 0 ? 0 : 0.9f + rr() * 0.5f) * s, sz = (b == 0 ? 2.6f : 1.6f + rr() * 0.8f) * s;
                                var lm = U.NewMat(U.Shade(lc, 0.85f + rr() * 0.35f), 0.05f, 0.25f, U.LeafTex());
                                U.Box(S, lm, x + Mathf.Cos(a) * rad, h * (0.6f + (b == 0 ? 0.1f : rr() * 0.3f)), z + Mathf.Sin(a) * rad, sz, sz * 0.8f, sz, 0, rr() * 1.5f, 0);
                            }
                        }
                        else for (int i = 0; i < 4; i++) { var lm = U.NewMat(U.Shade(lc, 0.8f + i * 0.1f), 0.05f, 0.25f, U.LeafTex()); U.Cone(S, lm, x, h * 0.35f + i * 1.1f * s + 1, z, (2.4f - i * 0.5f) * s, 2.0f * s, 7, i * 0.4f); }
                        break;
                    }
                case "deadtree":
                    {
                        U.Box(S, U.Mat(c), x, h / 2, z, w, h, d); var r = U.Rng(Mathf.RoundToInt(x * 13 + z * 7));
                        for (int i = 0; i < 4; i++) { float len = 1.2f + r() * 1.2f, a = r() * Mathf.PI * 2; U.Box(S, U.Mat(c), x + Mathf.Cos(a) * len * 0.35f, h * (0.55f + i * 0.12f), z + Mathf.Sin(a) * len * 0.35f, 0.2f, len, 0.2f, Mathf.Sin(a) * 0.9f, 0, -Mathf.Cos(a) * 0.9f); }
                        break;
                    }
                case "crate": U.Box(S, TexMat(c, U.PlankTex()), x, y0 + h / 2, z, w, h, d); break;
                case "hay": U.Box(S, TexMat(c, U.HayTex()), x, h / 2, z, w, h, d); break;
                case "container": U.Box(S, TexMat(c, U.RibTex(), 0.5f), x, h / 2, z, w, h, d); U.Box(S, U.NewMat(U.Shade(U.C(c), 0.7f)), x, h, z, w + 0.05f, 0.12f, d + 0.05f); break;
                case "sandbag": U.Box(S, TexMat(c, U.BagTex()), x, h / 2, z, w, h, d); break;
                case "concrete": U.Box(S, TexMat(c, U.BrickTex()), x, h / 2, z, w, h, d); U.Box(S, U.Mat("#757575"), x, h + 0.1f, z, w + 0.1f, 0.2f, d + 0.1f); break;
                case "barrel": U.Cyl(S, U.Mat(c, 0.3f, 0.5f), x, h / 2, z, w / 2, h); foreach (var yy in new[] { 0.25f, 0.75f }) U.Cyl(S, U.NewMat(U.Shade(U.C(c), 0.6f)), x, h * yy, z, w / 2 + 0.03f, 0.06f); break;
                case "fence": case "ironfence":
                    {
                        bool along = w > d; float L = along ? w : d; bool iron = o.kind == "ironfence"; float step = iron ? 0.35f : 2; int n = Mathf.Max(1, Mathf.RoundToInt(L / step));
                        var m = U.Mat(c);
                        for (int i = 0; i <= n; i++)
                        {
                            float tt = -L / 2 + (i / (float)n) * L; float px = along ? x + tt : x, pz = along ? z : z + tt;
                            U.Box(S, m, px, h / 2, pz, iron ? 0.06f : 0.16f, h, iron ? 0.06f : 0.16f);
                            if (iron && i % 6 == 0) U.Box(S, m, px, (h + 0.3f) / 2, pz, 0.14f, h + 0.3f, 0.14f);
                        }
                        foreach (var yy in iron ? new[] { 0.25f, h - 0.15f } : new[] { h * 0.4f, h * 0.85f }) U.Box(S, m, x, yy, z, along ? L : 0.08f, iron ? 0.06f : 0.12f, along ? 0.08f : L);
                        break;
                    }
                case "tent": U.Gable(S, U.Mat(c), x, 0, z, w, h, d); U.Box(S, U.Mat("#1b1b1b"), x, 0.7f, z + d / 2 + 0.01f, 1.0f, 1.4f, 0.05f); break;
                case "platform":
                    U.Box(S, U.Mat(c), x, y0 + h / 2, z, w, h, d);
                    foreach (var s in new[] { -1, 1 }) { U.Box(S, U.Mat(c), x, y0 + 0.6f, z + s * d / 2, w, 0.6f, 0.08f); U.Box(S, U.Mat(c), x + s * w / 2, y0 + 0.6f, z, 0.08f, 0.6f, d); }
                    foreach (var (dx, dz) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) }) U.Box(S, U.Mat("#5d4037"), x + dx * (w / 2 - 0.1f), y0 + 1.1f, z + dz * (d / 2 - 0.1f), 0.12f, 1.6f, 0.12f);
                    U.Cone(S, U.Mat("#4e342e"), x, y0 + 2.4f, z, 2.4f, 1.0f, 4, Mathf.PI / 4);
                    break;
                case "hedgehog": foreach (var (rx, rz) in new[] { (0.6f, 0f), (-0.6f, 0f), (0f, 0.6f) }) U.Box(S, U.Mat(c, 0.5f, 0.5f), x, 0.55f, z, 0.15f, 1.6f, 0.15f, rx, rz * 2, rz); break;
                case "grave": U.Box(S, U.Mat(c), x, h / 2, z, w, h, d); U.Box(S, U.Mat("#4e3b2a"), x, 0.07f, z + 0.95f, w * 0.8f, 0.15f, 1.6f); break;
                case "cross": U.Box(S, U.Mat(c), x, h / 2, z, 0.25f, h, 0.25f); U.Box(S, U.Mat(c), x, h * 0.72f, z, w, 0.22f, 0.24f); U.Box(S, U.Mat("#4e3b2a"), x, 0.07f, z + 0.95f, 0.8f, 0.15f, 1.6f); break;
                case "lantern": case "lamp":
                    {
                        U.Box(S, U.Mat(c, 0.5f, 0.5f), x, h / 2, z, 0.18f, h, 0.18f);
                        var glow = U.Mat("#fff59d", 0, 0.5f, null, "#ffd54f", night ? 2.5f : 0.4f);
                        if (o.kind == "lamp") { U.Box(S, U.Mat(c), x + 0.4f, h, z, 0.9f, 0.12f, 0.15f); U.Box(S, glow, x + 0.75f, h - 0.1f, z, 0.35f, 0.15f, 0.3f); }
                        else { U.Box(S, glow, x, h + 0.2f, z, 0.4f, 0.45f, 0.4f); U.Box(S, U.Mat(c), x, h + 0.45f, z, 0.5f, 0.08f, 0.5f); }
                        if (night && lights < 8) { lights++; var pl = new GameObject("lamplight").AddComponent<Light>(); pl.type = LightType.Point; pl.color = U.C("#ffcc66"); pl.range = 14; pl.transform.SetParent(root.transform, false); pl.transform.position = new Vector3(x + (o.kind == "lamp" ? 0.75f : 0), h + 0.1f, z); HdLight(pl, 1800); lampLights.Add(pl); }
                        break;
                    }
                case "dumpster": U.Box(S, U.Mat(c, 0.4f, 0.5f), x, h / 2, z, w, h, d); U.Box(S, U.NewMat(U.Shade(U.C(c), 0.6f)), x, h + 0.05f, z, w + 0.1f, 0.1f, d + 0.1f); break;
                case "trough": U.Box(S, U.Mat(c), x, h / 2, z, w, h, d); U.Box(S, U.Mat("#4fc3f7", 0, 0.9f), x, h - 0.1f, z, w - 0.2f, 0.02f, d - 0.2f); break;
                case "woodpile": for (int i = 0; i < 6; i++) { int row = i < 3 ? 0 : 1, col = i % 3; U.Cyl(S, U.Mat(i % 2 == 1 ? "#8d6e63" : "#795548"), x - w / 3 + col * (w / 3) + row * 0.25f, 0.22f + row * 0.42f, z, 0.22f, d + 0.1f, Mathf.PI / 2); } break;
                case "corn":
                    {
                        int n = Mathf.RoundToInt(w * d * 1.2f); var r = U.Rng(Mathf.RoundToInt(x * 31 + z));
                        Material a = U.Mat("#9e9d24"), b = U.Mat("#c0ca33");
                        for (int i = 0; i < n; i++) { float hh = h * (0.7f + r() * 0.4f); U.Box(S, r() > 0.3f ? a : b, x + (r() - 0.5f) * w, hh / 2, z + (r() - 0.5f) * d, 0.1f + r() * 0.1f, hh, 0.1f + r() * 0.1f, 0, r() * 3, 0, false); }
                        break;
                    }
                default: U.Box(S, U.Mat(c), x, y0 + h / 2, z, w, h, d); break;
            }
        }

        void AddBorder(Transform S)
        {
            float half = map.size / 2 + 0.6f, L = map.size + 2.4f; string kind = map.border;
            foreach (var (x, z, along) in new[] { (0f, -half, true), (0f, half, true), (-half, 0f, false), (half, 0f, false) })
            {
                if (kind == "hedge") U.Box(S, TexMat("#2e7d32", U.LeafTex()), x, 1.4f, z, along ? L : 1.4f, 2.8f, along ? 1.4f : L);
                else if (kind == "wall") { U.Box(S, TexMat("#9e9e9e", U.BrickTex()), x, 1.8f, z, along ? L : 0.8f, 3.6f, along ? 0.8f : L); U.Box(S, U.Mat("#616161"), x, 3.7f, z, along ? L : 1.0f, 0.25f, along ? 1.0f : L); }
                else AddObject(new MapObj { x = x, z = z, w = along ? L : 0.2f, d = along ? 0.2f : L, h = kind == "ironfence" ? 2.2f : 1.8f, color = kind == "ironfence" ? "#1f2a30" : "#795548", kind = kind == "ironfence" ? "ironfence" : "fence" }, S);
            }
        }

        void AddScenery(Transform S)
        {
            var r = U.Rng((int)(map.size * 17 + map.id.Length)); float half = map.size / 2;
            for (int i = 0; i < 60; i++)
            {
                float a = r() * Mathf.PI * 2, dist = half + 4 + r() * 26;
                float x = Mathf.Clamp(Mathf.Cos(a) * dist, -half - 30, half + 30), z = Mathf.Clamp(Mathf.Sin(a) * dist, -half - 30, half + 30);
                if (Mathf.Abs(x) < half + 3 && Mathf.Abs(z) < half + 3) continue;
                float s = 0.9f + r() * 0.8f; string kind = night ? "deadtree" : (r() > 0.5f ? "pine" : "tree");
                var raw = new Dictionary<string, object> { ["leaf"] = r() > 0.5f ? "#43a047" : "#388e3c" };
                AddObject(new MapObj { x = x, z = z, w = 0.8f * s, d = 0.8f * s, h = 6 * s, color = night ? "#3e2723" : "#6d4c41", kind = kind, raw = raw }, S);
            }
        }

        readonly List<Light> lampLights = new List<Light>();
        static void HdLight(Light l, float lumen) { var hd = l.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>(); hd.SetIntensity(lumen, UnityEngine.Rendering.LightUnit.Lumen); hd.affectsVolumetric = true; }
        /// <summary>Himmel, Nebel und Lichtstimmung der Karte als HDRP-Volume</summary>
        void BuildEnvironmentVolume()
        {
            var go = new GameObject("Environment"); go.transform.SetParent(root.transform, false);
            var vol = go.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true; vol.priority = 5;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); vol.profile = prof;
            var env = prof.Add<UnityEngine.Rendering.HighDefinition.VisualEnvironment>(true); env.skyType.Override((int)UnityEngine.Rendering.HighDefinition.SkyType.PhysicallyBased); env.skyAmbientMode.Override(UnityEngine.Rendering.HighDefinition.SkyAmbientMode.Dynamic);
            var sky = prof.Add<UnityEngine.Rendering.HighDefinition.PhysicallyBasedSky>(true); sky.exposure.Override(0f); sky.multiplier.Override(1f);
            var fog = prof.Add<UnityEngine.Rendering.HighDefinition.Fog>(true);
            fog.enabled.Override(true); fog.albedo.Override(map.fogColor); fog.meanFreePath.Override(Mathf.Max(20f, map.fogFar * 0.55f)); fog.baseHeight.Override(0f); fog.maximumHeight.Override(map.night ? 25f : 60f);
            fog.enableVolumetricFog.Override(true); fog.anisotropy.Override(0.35f); fog.globalLightProbeDimmer.Override(0.8f); fog.depthExtent.Override(80f);
            fog.tint.Override(map.night ? new Color(0.5f, 0.6f, 0.9f) : Color.white);
            var clouds = prof.Add<UnityEngine.Rendering.HighDefinition.VolumetricClouds>(true); clouds.enable.Override(true); clouds.cloudPreset = map.night ? UnityEngine.Rendering.HighDefinition.VolumetricClouds.CloudPresets.Overcast : UnityEngine.Rendering.HighDefinition.VolumetricClouds.CloudPresets.Cloudy; clouds.shadows.Override(true);
            var shadows = prof.Add<UnityEngine.Rendering.HighDefinition.HDShadowSettings>(true); shadows.maxShadowDistance.Override(140f); shadows.cascadeShadowSplitCount.Override(4);
            if (map.night) { var ca = prof.Add<UnityEngine.Rendering.HighDefinition.ColorAdjustments>(true); ca.colorFilter.Override(new Color(0.75f, 0.82f, 1f)); ca.postExposure.Override(-0.5f); }
        }
        /// <summary>Gras, Blumen, Büsche und Steine auf freien Flächen</summary>
        void AddVegetation(Transform S)
        {
            var r = U.Rng(Mathf.RoundToInt(map.size * 3 + map.id.Length * 11));
            float half = map.size / 2;
            bool Free(float x, float z, float m)
            {
                foreach (var dc in map.decals) if (x > dc.x - dc.w / 2 - m && x < dc.x + dc.w / 2 + m && z > dc.z - dc.d / 2 - m && z < dc.z + dc.d / 2 + m) return false;
                foreach (var s in map.solids) if (x > s.minX - m && x < s.maxX + m && z > s.minZ - m && z < s.maxZ + m) return false;
                if (Vector2.Distance(new Vector2(x, z), map.spawn) < 4) return false;
                foreach (var zs in map.zspawns) if (Vector2.Distance(new Vector2(x, z), zs) < 3) return false;
                return true;
            }
            var grassMat = U.NewMat(U.C(night ? "#4a6b45" : "#7cb342"), 0.0f, 0.15f); grassMat.SetFloat("_Cull", 0);
            var gmesh = U.GrassMesh();
            int tufts = night ? 900 : 1600;
            for (int i = 0; i < tufts; i++)
            {
                float x = (r() - 0.5f) * map.size, z = (r() - 0.5f) * map.size;
                if (!Free(x, z, 0.3f)) continue;
                float sc = 0.6f + r() * 0.5f;
                U.MeshObj(gmesh, S, grassMat, new Vector3(x, 0, z), new Vector3(sc, sc * (0.7f + r() * 0.6f), sc), new Vector3(0, r() * 360, 0), false);
            }
            if (!night)
            {
                Material[] flowers = { U.Mat("#f44336"), U.Mat("#ffeb3b"), U.Mat("#e91e63"), U.Mat("#ffffff") };
                for (int i = 0; i < 160; i++) { float x = (r() - 0.5f) * map.size, z = (r() - 0.5f) * map.size; if (!Free(x, z, 0.5f)) continue; U.Box(S, U.Mat("#4caf50"), x, 0.12f, z, 0.03f, 0.24f, 0.03f, 0, 0, 0, false); U.Box(S, flowers[i % 4], x, 0.26f, z, 0.1f, 0.06f, 0.1f, 0, r() * 1.5f, 0, false); }
            }
            // Büsche: Haufen aus 3–5 Kugeln
            var bushMat = U.NewMat(U.C(night ? "#2e4a2e" : "#388e3c"), 0.05f, 0.25f, U.LeafTex()); var bushMat2 = U.NewMat(U.C(night ? "#3a5a35" : "#43a047"), 0.05f, 0.25f, U.LeafTex());
            for (int i = 0; i < 36; i++)
            {
                float x = (r() - 0.5f) * (map.size - 6), z = (r() - 0.5f) * (map.size - 6);
                if (!Free(x, z, 1.4f)) continue;
                int n = 3 + (int)(r() * 3);
                for (int k = 0; k < n; k++) { float rad = 0.4f + r() * 0.35f; U.Sphere(S, k % 2 == 0 ? bushMat : bushMat2, x + (r() - 0.5f) * 0.9f, rad * 0.75f, z + (r() - 0.5f) * 0.9f, rad); }
            }
            // Steine
            var rockMat = U.MatN("#8d8d8d", U.StoneTex(), U.NoiseNormal(), 0.15f, 1f); var rm = U.RockMesh();
            for (int i = 0; i < 26; i++)
            {
                float x = (r() - 0.5f) * (map.size - 4), z = (r() - 0.5f) * (map.size - 4);
                if (!Free(x, z, 1.2f)) continue;
                float sc = 0.3f + r() * 0.7f;
                U.MeshObj(rm, S, rockMat, new Vector3(x, 0, z), new Vector3(sc * (1 + r() * 0.6f), sc * 0.8f, sc * (1 + r() * 0.6f)), new Vector3(0, r() * 360, 0));
            }
        }

        void BuildSky()
        {
            var cam = Camera.main; if (cam) { cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = map.fogColor; }
            var extras = new GameObject("sky"); extras.transform.SetParent(root.transform, false);
            // Himmelskuppel (innen sichtbar): große Kugel mit Verlauf über Scheitelfarben nicht nötig – zwei Farben über Nebel
            // Sonne / Mond
            var sp = map.sunPos.normalized * 380;
            var disc = U.Prim(PrimitiveType.Quad, extras.transform, U.UnlitMat(U.C(night ? "#e8eaf6" : "#fff8e1")), sp, Vector3.one * (night ? 28 : 40), null, false, false);
            disc.transform.LookAt(Vector3.zero); disc.transform.Rotate(0, 180, 0);
            // Wolken
            var r = U.Rng(99); var cm = U.UnlitMat(night ? new Color(0.16f, 0.18f, 0.29f, 0.6f) : new Color(1, 1, 1, 0.92f), true);
            for (int i = 0; i < 16; i++)
            {
                var c = new GameObject("cloud").transform; c.SetParent(extras.transform, false);
                int n = 2 + (int)(r() * 3);
                for (int j = 0; j < n; j++) U.Prim(PrimitiveType.Cube, c, cm, new Vector3(j * 6 - n * 3, r() * 2, (r() - 0.5f) * 5), new Vector3(8 + r() * 10, 3 + r() * 2, 6 + r() * 6), null, false, false);
                c.position = new Vector3((r() - 0.5f) * 400, 60 + r() * 30, (r() - 0.5f) * 400);
                clouds.Add(c);
            }
            if (night)
            {
                // Bodennebel und Glühwürmchen
                var fogGo = new GameObject("groundfog"); fogGo.transform.SetParent(root.transform, false); fogGo.transform.position = new Vector3(0, 0.6f, 0);
                var fps = fogGo.AddComponent<ParticleSystem>(); var fm = fps.main; fm.startLifetime = 14; fm.startSpeed = 0.3f; fm.startSize = new ParticleSystem.MinMaxCurve(10, 18); fm.startColor = new Color(0.7f, 0.75f, 0.9f, 0.08f); fm.maxParticles = 160; fm.simulationSpace = ParticleSystemSimulationSpace.World; fm.startRotation = new ParticleSystem.MinMaxCurve(0, 6.28f);
                var fe = fps.emission; fe.rateOverTime = 10; var fs = fps.shape; fs.shapeType = ParticleSystemShapeType.Box; fs.scale = new Vector3(map.size, 0.5f, map.size);
                var fcol = fps.colorOverLifetime; fcol.enabled = true; var grad = new Gradient(); grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, 0.2f), new GradientAlphaKey(1, 0.8f), new GradientAlphaKey(0, 1) }); fcol.color = grad;
                var frot = fps.rotationOverLifetime; frot.enabled = true; frot.z = 0.05f;
                var fr = fps.GetComponent<ParticleSystemRenderer>(); var fogMat = U.UnlitMat(new Color(1, 1, 1, 0.08f), true); U.SetMainTex(fogMat, U.GlowTex()); fr.material = fogMat; fr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; fr.sortingFudge = 10;
                var ffGo = new GameObject("fireflies"); ffGo.transform.SetParent(root.transform, false); ffGo.transform.position = new Vector3(0, 1.5f, 0);
                var ff = ffGo.AddComponent<ParticleSystem>(); var ffm = ff.main; ffm.startLifetime = 6; ffm.startSpeed = 0.4f; ffm.startSize = 0.12f; ffm.startColor = new Color(0.8f, 1f, 0.4f, 1); ffm.maxParticles = 120; ffm.simulationSpace = ParticleSystemSimulationSpace.World;
                var ffe = ff.emission; ffe.rateOverTime = 15; var ffs = ff.shape; ffs.shapeType = ParticleSystemShapeType.Box; ffs.scale = new Vector3(map.size, 2f, map.size);
                var ffn = ff.noise; ffn.enabled = true; ffn.strength = 0.6f; ffn.frequency = 0.4f;
                var ffsz = ff.sizeOverLifetime; ffsz.enabled = true; ffsz.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.2f, 1), new Keyframe(0.5f, 0.3f), new Keyframe(0.7f, 1), new Keyframe(1, 0)));
                var ffr = ff.GetComponent<ParticleSystemRenderer>(); var ffMat = U.UnlitMat(new Color(0.8f, 1f, 0.4f, 1), false, true); U.SetMainTex(ffMat, U.GlowTex()); ffr.material = ffMat; ffr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var sm = U.UnlitMat(Color.white);
                for (int i = 0; i < 300; i++) { var v = new Vector3(r() - 0.5f, r() * 0.9f + 0.1f, r() - 0.5f).normalized * 420; var st = U.Prim(PrimitiveType.Quad, extras.transform, sm, v, Vector3.one * (1.2f + r() * 1.5f), null, false, false).transform; st.LookAt(Vector3.zero); st.Rotate(0, 180, 0); }
            }
        }

        public void Update(float dt)
        {
            t += dt;
            gen.fan.Rotate(0, dt * 9 * Mathf.Rad2Deg, 0);
            U.SetEmission(gen.core, U.C("#00e5ff"), 1.0f + Mathf.Sin(t * 3) * 0.4f);
            for (int i = 0; i < gen.lamps.Count; i++) U.SetEmission(gen.lamps[i], U.C("#ff1744"), Mathf.Sin(t * 5 + i * 1.57f) > 0 ? 2 : 0.2f);
            foreach (var c in clouds) { var p = c.position; p.x += dt * 2; if (p.x > 220) p.x = -220; c.position = p; }
            for (int i = 0; i < lampLights.Count; i++) lampLights[i].GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>().SetIntensity(1800 + (Mathf.PerlinNoise(t * 6 + i * 3.1f, i) - 0.5f) * 900, UnityEngine.Rendering.LightUnit.Lumen); // Flackern
            if (crate != null)
            {
                crateSpin = Mathf.Max(0, crateSpin - dt); float open = crateSpin > 0 ? 1 : 0;
                var e = crate.lid.localEulerAngles; float cur = e.x > 180 ? e.x - 360 : e.x; cur = Mathf.Lerp(cur, open * -109f, Mathf.Min(1, dt * 6)); crate.lid.localEulerAngles = new Vector3(cur, 0, 0);
                var col = U.GetColor(crate.glow); col.a = Mathf.Lerp(col.a, open * 0.55f, Mathf.Min(1, dt * 6)); U.SetColor(crate.glow, col);
                crate.sign.localPosition = new Vector3(0, 1.7f + Mathf.Sin(t * 2.5f) * 0.08f + open * 0.6f, 0); crate.sign.Rotate(0, dt * (open > 0 ? 9 : 1.2f) * Mathf.Rad2Deg, 0);
            }
        }

        public void Dispose() { if (root) Object.Destroy(root); }
    }
}
