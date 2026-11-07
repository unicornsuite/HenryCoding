// Partikel, Leuchtspuren, Mündungsfeuer, Explosionen, Blitze, Projektile, Schadenszahlen, Kamerawackeln
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ZA
{
    public class Effects
    {
        public float shake;
        readonly Transform root;
        readonly Camera cam;
        ParticleSystem ps;
        readonly List<(LineRenderer lr, float t, float max)> lines = new List<(LineRenderer, float, float)>();
        readonly Stack<LineRenderer> linePool = new Stack<LineRenderer>();
        readonly List<(GameObject go, float t, float max, Action<GameObject, float> fn)> fades = new List<(GameObject, float, float, Action<GameObject, float>)>();
        readonly List<(Light l, float t, float max, float peak)> lights = new List<(Light, float, float, float)>();
        readonly Stack<Light> lightPool = new Stack<Light>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<(Transform o, Vector3 a, Vector3 b, float t, float dur)> spits = new List<(Transform, Vector3, Vector3, float, float)>();
        readonly List<(TextMesh tm, Vector3 pos, float t, float life)> nums = new List<(TextMesh, Vector3, float, float)>();
        readonly Stack<TextMesh> numPool = new Stack<TextMesh>();
        Material tracerMat, flashMat, sphereMat, boltMat, poolMat;
        Mesh cube;

        public class Projectile
        {
            public Transform o; public Vector3 v; public float life, bounce, g; public bool fuse; public Color trail; public bool hasTrail;
            public Func<Vector3, Vector3, Projectile, bool> step; public Material mat; public Vector3 rot;
        }

        public Effects(Camera camera)
        {
            cam = camera;
            root = new GameObject("Effects").transform;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cube); cube = tmp.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.Destroy(tmp);
            // Partikelsystem (Würfel)
            var go = new GameObject("particles"); go.transform.SetParent(root, false);
            ps = go.AddComponent<ParticleSystem>();
            var main = ps.main; main.playOnAwake = false; main.loop = false; main.maxParticles = 2000; main.simulationSpace = ParticleSystemSimulationSpace.World; main.startSize = 0.1f; main.gravityModifier = 1.4f; main.startRotation3D = true;
            var em = ps.emission; em.enabled = false;
            var sh = ps.shape; sh.enabled = false;
            var col = ps.collision; col.enabled = true; col.type = ParticleSystemCollisionType.Planes; col.bounce = 0.2f; col.dampen = 0.5f; col.lifetimeLoss = 0;
            var plane = new GameObject("groundplane").transform; plane.SetParent(root, false); plane.position = new Vector3(0, 0.03f, 0); col.SetPlane(0, plane);
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0, 1), new Keyframe(0.6f, 1), new Keyframe(1, 0)));
            var rot = ps.rotationOverLifetime; rot.enabled = true; rot.separateAxes = true; rot.x = 3; rot.y = 5; rot.z = 2;
            var r = ps.GetComponent<ParticleSystemRenderer>(); r.renderMode = ParticleSystemRenderMode.Mesh; r.mesh = cube; r.alignment = ParticleSystemRenderSpace.World;
            var pm = new Material(U.ParticleLit); pm.SetColor("_BaseColor", Color.white); r.sharedMaterial = pm;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            tracerMat = U.UnlitMat(new Color(1, 0.96f, 0.6f, 0.9f), false, true);
            flashMat = U.UnlitMat(new Color(1, 0.82f, 0.5f, 1), false, true); flashMat.SetTexture("_BaseMap", U.GlowTex()); flashMat.mainTexture = U.GlowTex();
            sphereMat = U.UnlitMat(new Color(1, 0.67f, 0.25f, 0.9f), false, true);
            boltMat = U.UnlitMat(new Color(0.5f, 0.85f, 1f, 1), false, true);
            poolMat = U.UnlitMat(new Color(0.46f, 1, 0.01f, 0.45f), true);
        }

        // ------------------------------------------------------------------ Partikel
        public void Particle(Vector3 pos, Vector3 vel, Color color, float size = 0.1f, float life = 0.8f, float gravity = 1f)
        {
            var ep = new ParticleSystem.EmitParams { position = pos, velocity = vel, startColor = color, startSize = size, startLifetime = life, rotation3D = new Vector3(UnityEngine.Random.value * 360, UnityEngine.Random.value * 360, 0) };
            ps.Emit(ep, 1);
        }
        public void Burst(Vector3 pos, Color color, int n = 8, float speed = 3, float size = 0.1f, float life = 0.7f, float up = 1.5f)
        {
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3((UnityEngine.Random.value - 0.5f) * speed, UnityEngine.Random.value * speed * 0.6f + up, (UnityEngine.Random.value - 0.5f) * speed);
                Particle(pos, v, color, size * (0.6f + UnityEngine.Random.value * 0.8f), life * (0.6f + UnityEngine.Random.value * 0.6f));
            }
        }
        static readonly Color[] bloodCols = { U.C("#4caf50"), U.C("#2e7d32"), U.C("#7cb342"), U.C("#8b0000") };
        public void Blood(Vector3 pos, Vector3? dir, bool big = false)
        {
            for (int i = 0; i < (big ? 18 : 7); i++)
            {
                var v = new Vector3((UnityEngine.Random.value - 0.5f) * 3, UnityEngine.Random.value * 3 + 0.5f, (UnityEngine.Random.value - 0.5f) * 3);
                if (dir.HasValue) v += dir.Value * 2.5f;
                Particle(pos, v, bloodCols[UnityEngine.Random.Range(0, bloodCols.Length)], 0.07f + UnityEngine.Random.value * 0.08f, 0.7f);
            }
        }
        /// <summary>Patronenhülse fliegt nach rechts weg</summary>
        public void Shell(Vector3 pos, Vector3 right, Vector3 up)
        {
            var v = right * (2 + UnityEngine.Random.value * 1.5f) + up * (2.5f + UnityEngine.Random.value) + new Vector3((UnityEngine.Random.value - 0.5f) * 0.5f, 0, (UnityEngine.Random.value - 0.5f) * 0.5f);
            Particle(pos, v, U.C("#ffca28"), 0.035f, 1.2f);
        }
        public void Smoke(Vector3 pos, Vector3 dir, float amount = 1)
        {
            for (int i = 0; i < 3 * amount; i++)
            {
                var ep = new ParticleSystem.EmitParams { position = pos + dir * 0.1f * i, velocity = dir * (1.5f + UnityEngine.Random.value) + Vector3.up * 0.6f, startColor = new Color(0.85f, 0.85f, 0.85f, 0.35f), startSize = 0.08f + UnityEngine.Random.value * 0.08f, startLifetime = 0.5f + UnityEngine.Random.value * 0.4f };
                ps.Emit(ep, 1);
            }
        }
        public void Sparks(Vector3 pos, Vector3? normal)
        {
            for (int i = 0; i < 6; i++)
            {
                var v = new Vector3((UnityEngine.Random.value - 0.5f) * 4, UnityEngine.Random.value * 3, (UnityEngine.Random.value - 0.5f) * 4);
                if (normal.HasValue) v += normal.Value * 3;
                Particle(pos, v, UnityEngine.Random.value > 0.5f ? U.C("#ffe082") : U.C("#9e9e9e"), 0.05f, 0.35f);
            }
        }
        public void Gibs(Vector3 pos, Color[] colors, int n = 10, float speed = 5)
        {
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3((UnityEngine.Random.value - 0.5f) * speed, UnityEngine.Random.value * speed * 0.8f + 2, (UnityEngine.Random.value - 0.5f) * speed);
                Particle(pos, v, colors[i % colors.Length], 0.16f + UnityEngine.Random.value * 0.14f, 1.4f + UnityEngine.Random.value * 0.6f);
            }
        }

        // ------------------------------------------------------------------ Lichter
        Light GetLight()
        {
            if (lightPool.Count > 0) { var l = lightPool.Pop(); l.gameObject.SetActive(true); return l; }
            var go = new GameObject("fxlight"); go.transform.SetParent(root, false);
            var li = go.AddComponent<Light>(); li.type = LightType.Point; li.shadows = LightShadows.None; return li;
        }
        public void FlashLight(Vector3 pos, Color color, float intensity = 8, float dur = 0.06f, float range = 10)
        {
            if (lights.Count > 6) return;
            var l = GetLight(); l.transform.position = pos; l.color = color; l.range = range; l.intensity = intensity;
            lights.Add((l, dur, dur, intensity));
        }
        public void Muzzle(Vector3 pos, float scale = 1, bool withLight = true)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(root, false); q.transform.position = pos; q.transform.localScale = Vector3.one * 0.6f * scale; q.transform.rotation = cam.transform.rotation * Quaternion.Euler(0, 0, UnityEngine.Random.value * 360);
            var r = q.GetComponent<Renderer>(); r.sharedMaterial = flashMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fades.Add((q, 0.05f, 0.05f, null));
            if (withLight) FlashLight(pos, U.C("#ffb74d"), 6 * scale, 0.06f, 8);
        }
        LineRenderer GetLine(Material m)
        {
            LineRenderer lr;
            if (linePool.Count > 0) { lr = linePool.Pop(); lr.gameObject.SetActive(true); }
            else { var go = new GameObject("line"); go.transform.SetParent(root, false); lr = go.AddComponent<LineRenderer>(); lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; lr.useWorldSpace = true; lr.textureMode = LineTextureMode.Stretch; lr.numCapVertices = 2; }
            lr.material = m; return lr;
        }
        public void Tracer(Vector3 from, Vector3 to, Color color, float width = 0.03f, float life = 0.07f)
        {
            var lr = GetLine(tracerMat); lr.positionCount = 2; lr.SetPosition(0, from); lr.SetPosition(1, to); lr.startWidth = lr.endWidth = width; lr.startColor = lr.endColor = color;
            lines.Add((lr, life, life));
        }
        public void Lightning(List<Vector3> points, Color? color = null)
        {
            var pts = new List<Vector3>();
            for (int i = 0; i < points.Count - 1; i++)
            {
                for (int s = 0; s <= 6; s++) { var p = Vector3.Lerp(points[i], points[i + 1], s / 6f); if (s > 0 && s < 6) p += new Vector3((UnityEngine.Random.value - 0.5f) * 0.6f, (UnityEngine.Random.value - 0.5f) * 0.6f, (UnityEngine.Random.value - 0.5f) * 0.6f); pts.Add(p); }
            }
            var lr = GetLine(boltMat); lr.positionCount = pts.Count; lr.SetPositions(pts.ToArray()); lr.startWidth = lr.endWidth = 0.08f; var c = color ?? U.C("#80d8ff"); lr.startColor = lr.endColor = c;
            lines.Add((lr, 0.18f, 0.18f));
            for (int i = 1; i < points.Count; i++) Burst(points[i], U.C("#b3e5fc"), 4, 3, 0.06f, 0.3f, 1);
            FlashLight(points[0], U.C("#40c4ff"), 10, 0.15f, 12);
        }
        public void Explosion(Vector3 pos, float radius = 4)
        {
            var s = GameObject.CreatePrimitive(PrimitiveType.Sphere); UnityEngine.Object.Destroy(s.GetComponent<Collider>()); s.transform.SetParent(root, false); s.transform.position = pos;
            var r = s.GetComponent<Renderer>(); r.material = new Material(sphereMat); r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fades.Add((s, 0.45f, 0.45f, (go, k) => { go.transform.localScale = Vector3.one * (radius * (1 - k * k) + 0.3f) * 2; var m = go.GetComponent<Renderer>().material; var c = Color.HSVToRGB(0.08f * k, 1, 0.5f); c.a = k * 0.9f; m.SetColor("_BaseColor", c); }));
            FlashLight(pos, U.C("#ff9100"), 40, 0.35f, radius * 5);
            Burst(pos, U.C("#ff6d00"), 18, 9, 0.25f, 0.8f, 3); Burst(pos, U.C("#ffd600"), 10, 7, 0.2f, 0.5f, 2); Burst(pos, U.C("#424242"), 14, 4, 0.45f, 1.6f, 1);
            float d = Vector3.Distance(cam.transform.position, pos);
            shake = Mathf.Max(shake, Mathf.Max(0, 1 - d / 25) * 0.6f);
        }
        public void AcidPool(Vector3 pos, float r = 2)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad); UnityEngine.Object.Destroy(q.GetComponent<Collider>()); q.transform.SetParent(root, false);
            q.transform.position = new Vector3(pos.x, 0.04f, pos.z); q.transform.rotation = Quaternion.Euler(90, 0, 0); q.transform.localScale = Vector3.one * r * 2;
            var rr = q.GetComponent<Renderer>(); rr.material = new Material(poolMat); rr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fades.Add((q, 1.2f, 1.2f, (go, k) => { var m = go.GetComponent<Renderer>().material; var c = m.GetColor("_BaseColor"); c.a = k * 0.45f; m.SetColor("_BaseColor", c); }));
            Burst(new Vector3(pos.x, 0.2f, pos.z), U.C("#aeea00"), 14, 4, 0.1f, 0.6f, 2);
        }
        public void Spit(Vector3 a, Vector3 b, float dur = 0.7f)
        {
            var s = U.Sphere(root, U.NewMat(U.C("#aeea00"), 0, 0.6f, null, U.C("#64dd17"), 1.2f), 0, 0, 0, 0.16f, false);
            spits.Add((s.transform, a, b, 0, dur));
        }
        /// <summary>Projektil mit Rückruf step(from, to, pr) – true = fertig</summary>
        public Projectile Launch(Vector3 pos, Vector3 vel, Func<Vector3, Vector3, Projectile, bool> step, float fuse = 4, float bounce = 0, Color? color = null, Color? emissive = null, float size = 0.09f, bool box = false, float gravity = 6, Color? trail = null)
        {
            var mat = U.NewMat(color ?? U.C("#33691e"), 0, 0.5f, null, emissive ?? U.C("#76ff03"), 0.5f);
            var go = box ? U.Prim(PrimitiveType.Cube, root, mat, pos, Vector3.one * size) : U.Sphere(root, mat, 0, 0, 0, size);
            go.transform.position = pos;
            var pr = new Projectile { o = go.transform, v = vel, life = fuse, bounce = bounce, g = gravity, fuse = fuse < 4, step = step, mat = mat, trail = trail ?? U.C("#bdbdbd"), hasTrail = trail.HasValue, rot = new Vector3(UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value) };
            projectiles.Add(pr); return pr;
        }
        public void DamageNumber(Vector3 pos, float amount, bool head = false) => Num(pos, Mathf.RoundToInt(amount).ToString(), head ? U.C("#ffd600") : Color.white, head ? 0.42f : 0.32f, 0.8f);
        public void TextNumber(Vector3 pos, string text, Color color, float life = 1.1f) => Num(pos, text, color, 0.3f, life);
        void Num(Vector3 pos, string text, Color color, float size, float life)
        {
            TextMesh tm;
            if (numPool.Count > 0) { tm = numPool.Pop(); tm.gameObject.SetActive(true); }
            else { if (nums.Count > 40) return; tm = U.Label(root, text, color, 0, size); }
            tm.text = text; tm.color = color; tm.characterSize = size * 0.1f;
            nums.Add((tm, pos + new Vector3((UnityEngine.Random.value - 0.5f) * 0.4f, 0, (UnityEngine.Random.value - 0.5f) * 0.4f), life, life));
        }

        public void Update(float dt)
        {
            for (int i = lines.Count - 1; i >= 0; i--)
            {
                var (lr, t, max) = lines[i]; t -= dt;
                if (t <= 0) { lr.gameObject.SetActive(false); linePool.Push(lr); lines.RemoveAt(i); continue; }
                var c = lr.startColor; c.a = t / max; lr.startColor = lr.endColor = c; lines[i] = (lr, t, max);
            }
            for (int i = fades.Count - 1; i >= 0; i--)
            {
                var (go, t, max, fn) = fades[i]; t -= dt;
                if (t <= 0) { UnityEngine.Object.Destroy(go); fades.RemoveAt(i); continue; }
                fn?.Invoke(go, t / max); fades[i] = (go, t, max, fn);
            }
            for (int i = lights.Count - 1; i >= 0; i--)
            {
                var (l, t, max, peak) = lights[i]; t -= dt;
                if (t <= 0) { l.gameObject.SetActive(false); lightPool.Push(l); lights.RemoveAt(i); continue; }
                l.intensity = peak * (t / max); lights[i] = (l, t, max, peak);
            }
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var pr = projectiles[i];
                pr.life -= dt; pr.v.y -= pr.g * dt;
                var from = pr.o.position; var to = from + pr.v * dt;
                bool done = pr.step(from, to, pr) || pr.life <= 0;
                if (!done && pr.bounce > 0 && to.y < 0.1f) { to.y = 0.1f; pr.v.y = Mathf.Abs(pr.v.y) * pr.bounce; pr.v.x *= 0.6f; pr.v.z *= 0.6f; if (Mathf.Abs(pr.v.y) < 0.5f) pr.v.y = 0; }
                pr.o.position = to; pr.o.Rotate(pr.rot.x * dt * 400, 0, pr.rot.z * dt * 400);
                if (pr.hasTrail && UnityEngine.Random.value < 0.7f) Particle(from, new Vector3(0, 0.5f, 0), pr.trail, 0.1f, 0.5f);
                if (pr.fuse && pr.life < 0.6f) U.SetEmission(pr.mat, U.C("#ff5252"), Mathf.Sin(pr.life * 40) > 0 ? 3 : 0.3f);
                if (done) { UnityEngine.Object.Destroy(pr.o.gameObject); projectiles.RemoveAt(i); }
            }
            for (int i = spits.Count - 1; i >= 0; i--)
            {
                var (o, a, b, t, dur) = spits[i]; t += dt; float k = Mathf.Min(1, t / dur);
                o.position = Vector3.Lerp(a, b, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 2.5f;
                if (UnityEngine.Random.value < 0.5f) Particle(o.position, new Vector3(0, 0.3f, 0), U.C("#aeea00"), 0.06f, 0.3f);
                if (k >= 1) { UnityEngine.Object.Destroy(o.gameObject); spits.RemoveAt(i); } else spits[i] = (o, a, b, t, dur);
            }
            for (int i = nums.Count - 1; i >= 0; i--)
            {
                var (tm, pos, t, life) = nums[i]; t -= dt; pos.y += dt * 1.2f;
                if (t <= 0) { tm.gameObject.SetActive(false); numPool.Push(tm); nums.RemoveAt(i); continue; }
                tm.transform.position = pos; float s = 0.7f + t * 0.5f; tm.transform.localScale = Vector3.one * s;
                var c = tm.color; c.a = Mathf.Min(1, t * 2.5f); tm.color = c; nums[i] = (tm, pos, t, life);
            }
            shake = Mathf.Max(0, shake - dt * 1.5f);
        }

        public void Clear()
        {
            foreach (var (lr, _, _) in lines) { lr.gameObject.SetActive(false); linePool.Push(lr); } lines.Clear();
            foreach (var (go, _, _, _) in fades) UnityEngine.Object.Destroy(go); fades.Clear();
            foreach (var pr in projectiles) UnityEngine.Object.Destroy(pr.o.gameObject); projectiles.Clear();
            foreach (var (o, _, _, _, _) in spits) UnityEngine.Object.Destroy(o.gameObject); spits.Clear();
            foreach (var (tm, _, _, _) in nums) { tm.gameObject.SetActive(false); numPool.Push(tm); } nums.Clear();
            ps.Clear();
        }
    }
}
