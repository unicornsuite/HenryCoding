// Hilfsfunktionen: Materialien, Grundkörper, prozedurale Texturen, Beschriftungen
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public static class Layers
    {
        public const int World = 0, ViewModel = 8, Hitbox = 9, Solid = 10, Barricade = 11, Ground = 12, SelfFP = 13; // SelfFP: eigene Figur, in der Egoperspektive von der Kamera ausgeblendet (wirft aber Schatten)
        public const int ShootMask = (1 << Solid) | (1 << Hitbox) | (1 << Barricade) | (1 << Ground);
        public const int PlaceMask = (1 << Solid) | (1 << Barricade) | (1 << Ground);
    }

    public static class U
    {
        static readonly Dictionary<string, Material> matCache = new Dictionary<string, Material>();
        static Shader litShader, unlitShader, particleShader, particleLitShader;
        public static Shader FindShader(string name, string resource) { var s = Shader.Find(name); if (s == null) { var m = Resources.Load<Material>("Shaders/" + resource); if (m) s = m.shader; } if (s == null) Debug.LogError("Shader nicht gefunden: " + name); return s; }
        public static Shader Lit => litShader ??= FindShader("HDRP/Lit", "Lit");
        public static Shader Unlit => unlitShader ??= FindShader("HDRP/Unlit", "Unlit");
        public static Shader Particle => particleShader ??= (Resources.Load<Material>("Shaders/ParticleUnlit")?.shader ?? Unlit);
        public static Shader ParticleLit => particleLitShader ??= (Resources.Load<Material>("Shaders/ParticleLit")?.shader ?? Particle);
        /// <summary>Emission in HDRP ist HDR und wird an die (automatische) Belichtung angepasst</summary>
        public const float EmissiveScale = 6f;

        public static Color C(string hex, float a = 1f) => GameData.Col(hex, a);
        public static Color Shade(Color c, float f) => new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);

        /// <summary>Gecachtes, undurchsichtiges Lit-Material.</summary>
        public static Material Mat(string hex, float metallic = 0.05f, float smooth = 0.38f, Texture2D tex = null, string emissive = null, float emissiveI = 0f)
        {
            string key = $"{hex}|{metallic}|{smooth}|{(tex ? tex.GetInstanceID() : 0)}|{emissive}|{emissiveI}";
            if (matCache.TryGetValue(key, out var m)) return m;
            m = NewMat(C(hex), metallic, smooth, tex, emissive != null ? C(emissive) : (Color?)null, emissiveI);
            matCache[key] = m;
            return m;
        }
        public static Material NewMat(Color col, float metallic = 0.05f, float smooth = 0.38f, Texture2D tex = null, Color? emissive = null, float emissiveI = 0f, Texture2D normal = null, float normalScale = 1f)
        {
            var m = new Material(Lit);
            m.SetColor("_BaseColor", col); m.color = col;
            m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", smooth);
            if (tex) { m.SetTexture("_BaseColorMap", tex); m.mainTexture = tex; }
            if (normal) { m.SetTexture("_NormalMap", normal); m.SetFloat("_NormalScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
            if (emissive.HasValue) SetEmission(m, emissive.Value, emissiveI);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            return m;
        }
        public static void SetEmission(Material m, Color col, float intensity)
        {
            UnityEngine.Rendering.HighDefinition.HDMaterial.SetUseEmissiveIntensity(m, false);
            UnityEngine.Rendering.HighDefinition.HDMaterial.SetEmissiveColor(m, col * intensity * EmissiveScale);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        public static Material MatN(string hex, Texture2D tex, Texture2D normal, float smooth = 0.3f, float normalScale = 1f)
        {
            string key = $"N|{hex}|{(tex ? tex.GetInstanceID() : 0)}|{(normal ? normal.GetInstanceID() : 0)}|{smooth}|{normalScale}";
            if (matCache.TryGetValue(key, out var m)) return m;
            m = NewMat(C(hex), 0.05f, smooth, tex, null, 0, normal, normalScale); matCache[key] = m; return m;
        }
        /// <summary>Alpha-Cutout-Material (Gras, Blätter), beidseitig</summary>
        public static Material Cutout(Texture2D tex, Color tint)
        {
            var m = new Material(Lit);
            m.SetColor("_BaseColor", tint); m.SetTexture("_BaseColorMap", tex); m.mainTexture = tex; m.SetFloat("_Smoothness", 0.1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.SetAlphaClipping(m, true); UnityEngine.Rendering.HighDefinition.HDMaterial.SetAlphaCutoff(m, 0.45f);
            m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.EnableKeyword("_DOUBLESIDED_ON");
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            return m;
        }
        public static void DoubleSided(Material m) { m.SetFloat("_DoubleSidedEnable", 1); m.SetFloat("_CullMode", 0); m.EnableKeyword("_DOUBLESIDED_ON"); UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m); }
        /// <summary>Transparentes Lit-Material</summary>
        public static Material Transparent(Color col, float smooth = 0.3f)
        {
            var m = new Material(Lit);
            m.SetColor("_BaseColor", col); m.color = col; m.SetFloat("_Smoothness", smooth);
            UnityEngine.Rendering.HighDefinition.HDMaterial.SetSurfaceType(m, true);
            m.SetFloat("_BlendMode", 0); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_BLENDMODE_ALPHA");
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            return m;
        }
        public static Material UnlitMat(Color col, bool transparent = false, bool additive = false)
        {
            var m = new Material(Unlit);
            m.SetColor("_UnlitColor", col); m.color = col;
            if (transparent || additive)
            {
                UnityEngine.Rendering.HighDefinition.HDMaterial.SetSurfaceType(m, true);
                m.SetFloat("_BlendMode", additive ? 1 : 0); m.SetFloat("_ZWrite", 0);
                m.EnableKeyword(additive ? "_BLENDMODE_ADD" : "_BLENDMODE_ALPHA");
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            }
            return m;
        }
        /// <summary>Farbe eines Materials setzen (Lit oder Unlit)</summary>
        public static void SetColor(Material m, Color c) { if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); m.color = c; }
        public static Color GetColor(Material m) => m.HasProperty("_UnlitColor") ? m.GetColor("_UnlitColor") : m.GetColor("_BaseColor");
        public static void SetMainTex(Material m, Texture t) { if (m.HasProperty("_UnlitColorMap")) m.SetTexture("_UnlitColorMap", t); if (m.HasProperty("_BaseColorMap")) m.SetTexture("_BaseColorMap", t); m.mainTexture = t; }

        // ------------------------------------------------------------------ Grundkörper
        public static GameObject Prim(PrimitiveType t, Transform parent, Material m, Vector3 pos, Vector3 scale, Vector3? euler = null, bool collider = false, bool shadows = true, int layer = -1)
        {
            var g = GameObject.CreatePrimitive(t);
            if (!collider) Object.Destroy(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos; g.transform.localScale = scale;
            if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            var r = g.GetComponent<Renderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadows;
            if (layer >= 0) g.layer = layer;
            return g;
        }
        public static GameObject Box(Transform p, Material m, float x, float y, float z, float w, float h, float d, float rx = 0, float ry = 0, float rz = 0, bool shadows = true)
            => Prim(PrimitiveType.Cube, p, m, new Vector3(x, y, z), new Vector3(w, h, d), new Vector3(rx, ry, rz) * Mathf.Rad2Deg, false, shadows);
        public static GameObject Cyl(Transform p, Material m, float x, float y, float z, float r, float h, float rx = 0, float ry = 0, float rz = 0, bool shadows = true)
            => Prim(PrimitiveType.Cylinder, p, m, new Vector3(x, y, z), new Vector3(r * 2, h / 2, r * 2), new Vector3(rx, ry, rz) * Mathf.Rad2Deg, false, shadows);
        public static GameObject Sphere(Transform p, Material m, float x, float y, float z, float r, bool shadows = true)
            => Prim(PrimitiveType.Sphere, p, m, new Vector3(x, y, z), Vector3.one * r * 2, null, false, shadows);

        static Mesh coneMesh, gableMesh, quadMesh;
        public static Mesh ConeMesh(int seg = 8)
        {
            if (coneMesh && seg == 8) return coneMesh;
            var m = new Mesh(); var v = new List<Vector3>(); var tri = new List<int>();
            for (int i = 0; i < seg; i++)
            {
                float a0 = i / (float)seg * Mathf.PI * 2, a1 = (i + 1) / (float)seg * Mathf.PI * 2;
                var b0 = new Vector3(Mathf.Cos(a0) * 0.5f, -0.5f, Mathf.Sin(a0) * 0.5f); var b1 = new Vector3(Mathf.Cos(a1) * 0.5f, -0.5f, Mathf.Sin(a1) * 0.5f);
                int n = v.Count; v.Add(new Vector3(0, 0.5f, 0)); v.Add(b1); v.Add(b0); tri.Add(n); tri.Add(n + 1); tri.Add(n + 2);
                n = v.Count; v.Add(new Vector3(0, -0.5f, 0)); v.Add(b0); v.Add(b1); tri.Add(n); tri.Add(n + 1); tri.Add(n + 2);
            }
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            if (seg == 8) coneMesh = m;
            return m;
        }
        /// <summary>Dreiecksprisma (Giebeldach), Breite/Höhe/Tiefe 1, Basis bei y=0</summary>
        public static Mesh GableMesh()
        {
            if (gableMesh) return gableMesh;
            var m = new Mesh();
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0, 1, -0.5f);
            Vector3 d = new Vector3(-0.5f, 0, 0.5f), e = new Vector3(0.5f, 0, 0.5f), f = new Vector3(0, 1, 0.5f);
            var v = new List<Vector3> { a, c, b, d, e, f, a, d, f, a, f, c, b, c, f, b, f, e, a, b, e, a, e, d };
            var tri = new List<int>(); for (int i = 0; i < v.Count; i++) tri.Add(i);
            m.SetVertices(v); m.SetTriangles(tri, 0); m.RecalculateNormals(); m.RecalculateBounds();
            gableMesh = m; return m;
        }
        public static GameObject MeshObj(Mesh mesh, Transform p, Material m, Vector3 pos, Vector3 scale, Vector3? euler = null, bool shadows = true)
        {
            var g = new GameObject("mesh"); g.transform.SetParent(p, false);
            g.transform.localPosition = pos; g.transform.localScale = scale; if (euler.HasValue) g.transform.localEulerAngles = euler.Value;
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }
        public static GameObject Cone(Transform p, Material m, float x, float y, float z, float r, float h, int seg = 8, float ry = 0)
            => MeshObj(ConeMesh(seg), p, m, new Vector3(x, y, z), new Vector3(r * 2, h, r * 2), new Vector3(0, ry * Mathf.Rad2Deg, 0));
        public static GameObject Gable(Transform p, Material m, float x, float y, float z, float w, float h, float d)
            => MeshObj(GableMesh(), p, m, new Vector3(x, y, z), new Vector3(w, h, d));

        // ------------------------------------------------------------------ Texturen
        static readonly Dictionary<string, Texture2D> texCache = new Dictionary<string, Texture2D>();
        public static System.Func<float> Rng(int seed) { var r = new System.Random(seed); return () => (float)r.NextDouble(); }
        static Texture2D MakeTex(string key, int size, System.Action<Color[], int> draw)
        {
            if (texCache.TryGetValue(key, out var t)) return t;
            var px = new Color[size * size];
            draw(px, size);
            t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, anisoLevel = 4 };
            t.SetPixels(px); t.Apply(true);
            texCache[key] = t; return t;
        }
        static void Fill(Color[] px, int s, Color c) { for (int i = 0; i < px.Length; i++) px[i] = c; }
        static void Rect(Color[] px, int s, int x, int y, int w, int h, Color c) { for (int yy = Mathf.Max(0, y); yy < Mathf.Min(s, y + h); yy++) for (int xx = Mathf.Max(0, x); xx < Mathf.Min(s, x + w); xx++) px[yy * s + xx] = c; }
        static void Disc(Color[] px, int s, float cx, float cy, float r, Color c) { for (int yy = (int)(cy - r); yy <= cy + r; yy++) for (int xx = (int)(cx - r); xx <= cx + r; xx++) if (xx >= 0 && yy >= 0 && xx < s && yy < s && (xx - cx) * (xx - cx) + (yy - cy) * (yy - cy) <= r * r) px[yy * s + xx] = c; }

        public static Texture2D GroundTex(string hex) => MakeTex("ground" + hex, 256, (px, s) =>
        {
            var c = C(hex); Fill(px, s, c); var r = Rng(7);
            for (int i = 0; i < 900; i++) Rect(px, s, (int)(r() * s), (int)(r() * s), 3, 3, Shade(c, 0.85f + r() * 0.3f));
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++) { Disc(px, s, x * 64 + 34, y * 64 + 30, 14, Shade(c, 0.86f)); Disc(px, s, x * 64 + 32, y * 64 + 32, 13, Shade(c, 1.08f)); }
        });
        public static Texture2D PlankTex() => MakeTex("plank", 64, (px, s) =>
        {
            Fill(px, s, Color.white); Color line = C("#d7c3a5"), edge = C("#a88a62");
            for (int i = 0; i < 4; i++) Rect(px, s, 0, i * 16 + 14, s, 2, line);
            Rect(px, s, 3, 3, s - 6, 4, edge); Rect(px, s, 3, s - 7, s - 6, 4, edge); Rect(px, s, 3, 3, 4, s - 6, edge); Rect(px, s, s - 7, 3, 4, s - 6, edge);
            for (int i = 4; i < s - 4; i++) Rect(px, s, i, i, 3, 3, edge);
        });
        public static Texture2D RibTex() => MakeTex("rib", 64, (px, s) => { Fill(px, s, Color.white); var g = C("#d0d0d0"); for (int i = 0; i < 8; i += 2) Rect(px, s, i * 8, 0, 8, s, g); });
        public static Texture2D HayTex() => MakeTex("hay", 64, (px, s) =>
        {
            Fill(px, s, Color.white); var r = Rng(3); var st = C("#d9c27a");
            for (int i = 0; i < 70; i++) { int x = (int)(r() * s), y = (int)(r() * s), dx = (int)(r() * 10 - 5); for (int k = 0; k < 8; k++) Rect(px, s, x + dx * k / 8, y + k, 1, 1, st); }
            var band = C("#a1793d"); Rect(px, s, 0, 20, s, 3, band); Rect(px, s, 0, 42, s, 3, band);
        });
        public static Texture2D BagTex() => MakeTex("bag", 64, (px, s) =>
        {
            Fill(px, s, Color.white); var l = C("#b5a06a");
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) { int ox = x * 32 + (y == 1 ? 16 : 0) - 14, oy = y * 32 + 2; Rect(px, s, ox, oy, 30, 3, l); Rect(px, s, ox, oy + 25, 30, 3, l); Rect(px, s, ox, oy, 3, 28, l); Rect(px, s, ox + 27, oy, 3, 28, l); }
        });
        public static Texture2D BrickTex() => MakeTex("brick", 64, (px, s) =>
        {
            Fill(px, s, Color.white); var g = C("#d9d9d9");
            for (int y = 0; y < 8; y++) { Rect(px, s, 0, y * 8, s, 1, g); for (int x = 0; x < 4; x++) Rect(px, s, x * 16 + (y % 2 == 1 ? 8 : 0), y * 8, 1, 8, g); }
        });
        public static Texture2D LeafTex() => MakeTex("leaf", 64, (px, s) =>
        {
            Fill(px, s, Color.white); var r = Rng(11); var a = C("#d8ecd8"); var b = C("#f2fff2");
            for (int i = 0; i < 160; i++) Rect(px, s, (int)(r() * s), (int)(r() * s), 4, 4, r() > 0.5f ? a : b);
        });
        public static Texture2D StripeTex() => MakeTex("stripe", 64, (px, s) =>
        {
            Fill(px, s, C("#ffca28")); var d = C("#212121");
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { int v = ((x + y) / 11) % 2; if (v == 0) px[y * s + x] = d; }
        });
        /// <summary>Blutspritzer: unregelmäßiger Fleck mit Tropfen (Alpha), Farbe im Material</summary>
        public static Texture2D BloodTex(int variant) => MakeTex("blood" + variant, 128, (px, s) =>
        {
            var r = Rng(101 + variant * 7); Fill(px, s, new Color(1, 1, 1, 0));
            float cx = s / 2f, cy = s / 2f;
            void Blob(float bx, float by, float rad, float a) { for (int y = (int)(by - rad - 1); y <= by + rad + 1; y++) for (int x = (int)(bx - rad - 1); x <= bx + rad + 1; x++) { if (x < 0 || y < 0 || x >= s || y >= s) continue; float d = Mathf.Sqrt((x - bx) * (x - bx) + (y - by) * (y - by)) / rad; float n = Mathf.PerlinNoise(x * 0.11f + variant, y * 0.11f) * 0.5f; float v = Mathf.Clamp01((1.15f - d - n * 0.6f) * 3f) * a; int i = y * s + x; px[i].a = Mathf.Max(px[i].a, v); px[i].r = px[i].g = px[i].b = 1 - Mathf.Clamp01(d) * 0.35f; } }
            Blob(cx, cy, s * 0.30f, 1f);
            for (int k = 0; k < 7; k++) { float a = r() * Mathf.PI * 2, dd = s * (0.15f + r() * 0.2f); Blob(cx + Mathf.Cos(a) * dd, cy + Mathf.Sin(a) * dd, s * (0.06f + r() * 0.13f), 0.9f); }
            for (int k = 0; k < 18; k++) { float a = r() * Mathf.PI * 2, dd = s * (0.25f + r() * 0.22f); Blob(cx + Mathf.Cos(a) * dd, cy + Mathf.Sin(a) * dd, s * (0.015f + r() * 0.035f), 0.85f); }
        });
        /// <summary>Weicher Tropfen (rund, weicher Rand) für Blutpartikel</summary>
        public static Texture2D DropTex() => MakeTex("drop", 32, (px, s) => { for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { float d = Mathf.Sqrt((x - 15.5f) * (x - 15.5f) + (y - 15.5f) * (y - 15.5f)) / 15.5f; float a = Mathf.Clamp01((1 - d) * 2.2f); px[y * s + x] = new Color(1, 1, 1, a); } });
        static Material decalBase;
        /// <summary>HDRP-Decal-Grundmaterial (aus Resources, damit der Shader im Build enthalten ist)</summary>
        public static Material DecalBase => decalBase ??= Resources.Load<Material>("Shaders/Decal");
        public static Texture2D GlowTex() => MakeTex("glow", 64, (px, s) =>
        {
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++) { float d = Mathf.Sqrt((x - 32) * (x - 32) + (y - 32) * (y - 32)) / 32f; float a = Mathf.Clamp01(1 - d); a = a * a; px[y * s + x] = new Color(1, 0.9f - d * 0.5f, 0.6f - d * 0.6f, a); }
        });
        /// <summary>Gesicht: Spieler (Augen + Lächeln) oder Zombie (leuchtende Augen, Zähne)</summary>
        public static Texture2D FaceTex(bool zombie, string skinHex, string eyesHex) => MakeTex("face" + zombie + skinHex + eyesHex, 64, (px, s) =>
        {
            var skin = C(skinHex); Fill(px, s, skin);
            // Unity-Texturen: y=0 unten. Zeichenkoordinaten wie im Browser (y nach unten) -> spiegeln
            void R(int x, int y, int w, int h, Color c) => Rect(px, s, x, s - y - h, w, h, c);
            if (!zombie)
            {
                var ink = C("#1a1a1a");
                Disc(px, s, 22, s - 26, 5, ink); Disc(px, s, 42, s - 26, 5, ink);
                for (int x = 21; x <= 43; x++) { int y = 36 + (int)(Mathf.Sqrt(Mathf.Max(0, 121 - (x - 32) * (x - 32)))) ; R(x, y - 2, 1, 3, ink); }
            }
            else
            {
                var eyes = C(eyesHex); var dark = new Color(0, 0, 0, 0.25f);
                R(14, 18, 16, 14, Shade(skin, 0.6f)); R(34, 18, 16, 14, Shade(skin, 0.6f));
                R(17, 22, 10, 7, eyes); R(37, 22, 10, 7, eyes);
                R(19, 23, 3, 3, Color.white); R(39, 23, 3, 3, Color.white);
                R(18, 40, 28, 10, C("#2b0f0f"));
                var tooth = C("#f5f0d0"); for (int i = 0; i < 5; i++) { R(19 + (int)(i * 5.5f), 40, 3, 4, tooth); R(21 + (int)(i * 5.5f), 46, 3, 4, tooth); }
                R(48, 8, 6, 12, C("#7a1010")); R(8, 44, 5, 8, C("#7a1010"));
            }
        });

        // ------------------------------------------------------------------ Normal Maps aus Höhenfeldern
        static Texture2D NormalFromHeight(string key, int size, System.Func<int, int, float> height, float strength)
        {
            if (texCache.TryGetValue(key, out var t)) return t;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float hl = height((x - 1 + size) % size, y), hr = height((x + 1) % size, y), hd = height(x, (y - 1 + size) % size), hu = height(x, (y + 1) % size);
                var n = new Vector3(-(hr - hl) * strength, -(hu - hd) * strength, 1).normalized;
                px[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1);
            }
            t = new Texture2D(size, size, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat, anisoLevel = 4 };
            t.SetPixels(px); t.Apply(true); texCache[key] = t; return t;
        }
        public static Texture2D BrickNormal() => NormalFromHeight("n_brick", 64, (x, y) => { int row = y / 8; int xx = (x + (row % 2 == 1 ? 8 : 0)) % 16; return (y % 8 == 0 || xx == 0) ? 0f : 1f; }, 1.2f);
        public static Texture2D PlankNormal() => NormalFromHeight("n_plank", 64, (x, y) => { float v = 1f; for (int i = 0; i < 4; i++) if (y >= i * 16 + 14 && y < i * 16 + 16) v = 0.3f; if (x < 3 || x > 60 || y < 3 || y > 60) v = 0.4f; return v; }, 1.0f);
        public static Texture2D StudNormal() => NormalFromHeight("n_stud", 256, (x, y) => { float cx = (x % 64) - 32, cy = (y % 64) - 32; float d = Mathf.Sqrt(cx * cx + cy * cy); return d < 13 ? 1f : d < 15 ? (15 - d) / 2f : 0f; }, 2.5f);
        public static Texture2D RibNormal() => NormalFromHeight("n_rib", 64, (x, y) => (x / 8) % 2 == 0 ? 1f : 0f, 1.0f);
        public static Texture2D NoiseNormal() => NormalFromHeight("n_noise", 128, (x, y) => Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 3 + Mathf.PerlinNoise(x * 0.3f, y * 0.3f), 1.2f);
        /// <summary>Grasbüschel (Alpha)</summary>
        public static Texture2D GrassTex() => MakeTex("grass", 64, (px, s) =>
        {
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0, 0, 0, 0);
            var r = Rng(5);
            for (int b = 0; b < 9; b++)
            {
                float x0 = 6 + b * 6 + r() * 3, lean = (r() - 0.5f) * 14, h = 36 + r() * 26; var c = Color.Lerp(C("#7cb342"), C("#c5e1a5"), r());
                for (int y = 0; y < h; y++) { float t = y / h; int x = (int)(x0 + lean * t * t); int w = Mathf.Max(1, (int)(4 * (1 - t))); for (int k = 0; k < w; k++) if (x + k >= 0 && x + k < s) px[y * s + x + k] = Color.Lerp(c * 0.6f, c, t) ; }
            }
            for (int i = 0; i < px.Length; i++) if (px[i].a > 0) px[i].a = 1;
        });
        public static Texture2D BarkTex() => MakeTex("bark", 64, (px, s) =>
        {
            Fill(px, s, Color.white); var r = Rng(21);
            for (int i = 0; i < 40; i++) { int x = (int)(r() * s), y = (int)(r() * s), h = 8 + (int)(r() * 30); Rect(px, s, x, y, 2, h, new Color(0.7f, 0.65f, 0.6f)); }
        });
        public static Texture2D StoneTex() => MakeTex("stone", 64, (px, s) => { var r = Rng(8); for (int i = 0; i < px.Length; i++) { float v = 0.8f + r() * 0.2f; px[i] = new Color(v, v, v, 1); } for (int i = 0; i < 30; i++) Rect(px, s, (int)(r() * s), (int)(r() * s), 2 + (int)(r() * 5), 2 + (int)(r() * 5), new Color(0.6f, 0.6f, 0.62f)); });

        // ------------------------------------------------------------------ Zusätzliche Meshes
        static Mesh grassMesh, rockMesh;
        /// <summary>Grasbüschel aus 8 Halmen (Dreiecke, beidseitig), Fußpunkt bei y=0, Höhe 1</summary>
        public static Mesh GrassMesh()
        {
            if (grassMesh) return grassMesh;
            var v = new List<Vector3>(); var col = new List<Color>(); var tri = new List<int>();
            var r = Rng(4);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2 + r() * 0.5f, rad = 0.05f + r() * 0.12f, h = 0.6f + r() * 0.5f, lean = 0.15f + r() * 0.25f, w = 0.04f + r() * 0.03f;
                var basePt = new Vector3(Mathf.Cos(a) * rad, 0, Mathf.Sin(a) * rad); var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                var side = new Vector3(-dir.z, 0, dir.x) * w;
                var mid = basePt + Vector3.up * h * 0.55f + dir * lean * 0.4f; var tip = basePt + Vector3.up * h + dir * lean;
                int n = v.Count;
                v.Add(basePt - side); v.Add(basePt + side); v.Add(mid + side * 0.6f); v.Add(mid - side * 0.6f); v.Add(tip);
                col.Add(new Color(0.55f, 0.55f, 0.55f)); col.Add(new Color(0.55f, 0.55f, 0.55f)); col.Add(Color.white); col.Add(Color.white); col.Add(new Color(1.2f, 1.2f, 1.0f));
                tri.AddRange(new[] { n, n + 2, n + 1, n, n + 3, n + 2, n + 3, n + 4, n + 2 });
                tri.AddRange(new[] { n, n + 1, n + 2, n, n + 2, n + 3, n + 3, n + 2, n + 4 }); // Rückseite
            }
            var m = new Mesh(); m.SetVertices(v); m.SetColors(col); m.SetTriangles(tri, 0); var nr = new Vector3[v.Count]; for (int i = 0; i < nr.Length; i++) nr[i] = Vector3.up; m.SetNormals(nr); m.RecalculateBounds();
            grassMesh = m; return m;
        }
        /// <summary>Unregelmäßiger Stein (verformte Kugel)</summary>
        public static Mesh RockMesh(int seed = 3)
        {
            if (rockMesh && seed == 3) return rockMesh;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere); var src = tmp.GetComponent<MeshFilter>().sharedMesh; Object.Destroy(tmp);
            var m = new Mesh(); var verts = src.vertices; var r = new System.Random(seed);
            for (int i = 0; i < verts.Length; i++) { var p = verts[i]; float n = Mathf.PerlinNoise(p.x * 3 + seed, p.z * 3 + p.y * 2) * 0.5f + 0.75f; verts[i] = new Vector3(p.x * n * 1.3f, Mathf.Max(-0.2f, p.y * n * 0.8f), p.z * n); }
            m.vertices = verts; m.triangles = src.triangles; m.uv = src.uv; m.RecalculateNormals(); m.RecalculateBounds();
            if (seed == 3) rockMesh = m; return m;
        }

        // ------------------------------------------------------------------ Beschriftungen (World-Space-Canvas, funktioniert in HDRP)
        static Font font, headFont; static bool fontIsOs;
        /// <summary>Fließtext: schmale System-Schrift (macOS), sonst Unity-Standard</summary>
        public static Font Font { get { if (font == null) { font = OsFont(new[] { "Avenir Next Condensed Demi Bold", "Avenir Next Condensed Medium", "Helvetica Neue Condensed Bold", "Arial Narrow" }, 18); fontIsOs = font != null; if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } return font; } }
        public static bool FontIsOs { get { var _ = Font; return fontIsOs; } }
        /// <summary>Überschriften: harte, schmale Schrift im Militär-/Schablonenstil</summary>
        public static Font HeadFont => headFont ??= OsFont(new[] { "DIN Condensed Bold", "DIN Condensed", "Impact", "Arial Black" }, 48) ?? Font;
        static Font OsFont(string[] names, int size)
        {
            try { var avail = new HashSet<string>(Font.GetOSInstalledFontNames()); foreach (var n in names) if (avail.Contains(n)) { var f = Font.CreateDynamicFontFromOSFont(n, size); if (f) return f; } }
            catch (System.Exception e) { Debug.LogWarning("Systemschrift nicht verfügbar: " + e.Message); }
            return null;
        }
        public static TextMesh Label(Transform parent, string text, Color color, float y, float size = 0.3f)
        {
            var g = new GameObject("label"); g.transform.SetParent(parent, false); g.transform.localPosition = new Vector3(0, y, 0);
            var canvas = g.AddComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.sortingOrder = 5;
            var rt = g.GetComponent<RectTransform>(); rt.sizeDelta = new Vector2(400, 60); rt.localScale = Vector3.one * (size * 0.0125f);
            var tg = new GameObject("text", typeof(RectTransform)); tg.transform.SetParent(g.transform, false);
            var trt = tg.GetComponent<RectTransform>(); trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var t = tg.AddComponent<UnityEngine.UI.Text>(); t.font = Font; t.fontSize = 40; t.color = color; t.alignment = TextAnchor.MiddleCenter; t.text = text; t.fontStyle = FontStyle.Bold; t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = tg.AddComponent<UnityEngine.UI.Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(2, -2);
            g.AddComponent<Billboard>();
            var tm = g.AddComponent<TextMesh>(); tm.hideFlags = HideFlags.HideInInspector; // Kompatibilitäts-Hülle: text/color werden weitergereicht
            var link = g.AddComponent<LabelLink>(); link.ui = t; link.tm = tm;
            tm.text = text; tm.color = color; tm.characterSize = size * 0.1f;
            var mr = g.GetComponent<MeshRenderer>(); if (mr) mr.enabled = false;
            return tm;
        }
    }

    /// <summary>Spiegelt TextMesh-Eigenschaften (text, color, characterSize) in das UI-Text des World-Space-Canvas</summary>
    public class LabelLink : MonoBehaviour
    {
        public UnityEngine.UI.Text ui; public TextMesh tm; string lastText; Color lastColor; float lastSize;
        void LateUpdate()
        {
            if (!ui || !tm) return;
            if (tm.text != lastText) { ui.text = tm.text; lastText = tm.text; }
            if (tm.color != lastColor) { ui.color = tm.color; lastColor = tm.color; }
            if (tm.characterSize != lastSize) { transform.localScale = Vector3.one * (tm.characterSize * 0.125f); lastSize = tm.characterSize; }
        }
    }

    /// <summary>Dreht sich immer zur Kamera</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate() { var c = Camera.main; if (c) transform.rotation = c.transform.rotation; }
    }

    /// <summary>Kleiner Lebensbalken über Zombies/Türmen</summary>
    public class HealthBar : MonoBehaviour
    {
        Transform fg; float width;
        public static HealthBar Create(Transform parent, float width, float y)
        {
            var g = new GameObject("hpbar"); g.transform.SetParent(parent, false); g.transform.localPosition = new Vector3(0, y, 0);
            var hb = g.AddComponent<HealthBar>(); hb.width = width;
            var bg = U.Prim(PrimitiveType.Quad, g.transform, U.UnlitMat(new Color(0, 0, 0, 0.6f), true), Vector3.zero, new Vector3(width, 0.1f, 1), null, false, false);
            var fgo = U.Prim(PrimitiveType.Quad, g.transform, U.UnlitMat(U.C("#76ff03")), new Vector3(0, 0, -0.001f), new Vector3(width - 0.04f, 0.06f, 1), null, false, false);
            hb.fg = fgo.transform;
            g.AddComponent<Billboard>();
            return hb;
        }
        public void Set(float frac)
        {
            frac = Mathf.Clamp01(frac);
            float w = (width - 0.04f) * Mathf.Max(0.001f, frac);
            fg.localScale = new Vector3(w, 0.06f, 1); fg.localPosition = new Vector3(-(width - 0.04f) / 2 + w / 2, 0, -0.001f);
            var col = frac > 0.5f ? U.C("#76ff03") : frac > 0.25f ? U.C("#ffc400") : U.C("#ff1744");
            fg.GetComponent<Renderer>().material.SetColor("_BaseColor", col);
        }
    }
}
