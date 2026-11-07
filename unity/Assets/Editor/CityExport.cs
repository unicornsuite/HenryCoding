// Macht aus der HDRP-Demoszene des Stadtpakets (Dead District) eine spielbare Karte:
// 1) Eigene Szene Assets/Scenes/City.unity (Client lädt sie additiv)
// 2) Kollisionskästen + Spawnpunkte als shared/maps-ruins.js (Server, Browser, Unity gemeinsam)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZA
{
    public static class CityExport
    {
        const string DemoScene = "Assets/DeadDistrict/HDRP/demo.unity";
        const string Gen = "Assets/Resources/Generated";
        public const string CityScenePath = "Assets/Scenes/City.unity";

        [MenuItem("Zombie-Ansturm/Ruinenstadt exportieren")]
        public static void Export()
        {
            if (!File.Exists(DemoScene)) { Debug.LogWarning("Demoszene fehlt: " + DemoScene); return; }
            var scene = EditorSceneManager.OpenScene(DemoScene, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            // Szene aufräumen: Kameras, Volumes, Spielerobjekte raus
            foreach (var c in roots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToList()) if (c) Object.DestroyImmediate(c.gameObject);
            foreach (var v in roots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<UnityEngine.Rendering.Volume>(true)).ToList()) if (v) Object.DestroyImmediate(v.gameObject);
            foreach (var a in roots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<AudioListener>(true)).ToList()) if (a) Object.DestroyImmediate(a);
            foreach (var mb in roots.Where(r => r).SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToList()) if (mb != null && !(mb is UnityEngine.Rendering.HighDefinition.HDAdditionalLightData) && !(mb is UnityEngine.Rendering.HighDefinition.HDAdditionalReflectionData)) Object.DestroyImmediate(mb);
            roots = scene.GetRootGameObjects().Where(r => r).ToArray();
            // Gesamtausdehnung
            var all = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToList();
            var terrains = roots.SelectMany(r => r.GetComponentsInChildren<Terrain>(true)).ToList();
            var b = new Bounds(); bool first = true;
            foreach (var r in all) { if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds); }
            foreach (var t in terrains) { var tb = new Bounds(t.transform.position + t.terrainData.size / 2, t.terrainData.size); if (first) { b = tb; first = false; } else b.Encapsulate(tb); }
            Vector3 center = new Vector3(b.center.x, 0, b.center.z);
            float size = Mathf.Ceil(Mathf.Min(140f, Mathf.Max(b.size.x, b.size.z)));
            Debug.Log($"Stadt-Bounds: center={center} size={b.size} -> Kartengröße {size}");
            // Alles so verschieben, dass die Kartenmitte im Ursprung liegt
            var holder = new GameObject("City");
            foreach (var r in roots) r.transform.SetParent(holder.transform, true);
            holder.transform.position = -center;
            var hroot = new GameObject("CityRoot"); holder.transform.SetParent(hroot.transform, true);
            // Terrainhöhe am Ursprung -> Boden auf y=0 legen
            float groundY = SampleGround(hroot, Vector3.zero);
            hroot.transform.position = new Vector3(0, -groundY, 0);
            // Kollider sicherstellen (Gebäude: MeshCollider, Terrain: TerrainCollider) und Layer setzen
            foreach (var mf in hroot.GetComponentsInChildren<MeshFilter>(true))
            {
                var go = mf.gameObject; var r = go.GetComponent<Renderer>(); if (!r || !r.enabled || !mf.sharedMesh) continue;
                if (!go.GetComponent<Collider>()) { var mc = go.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; }
                go.layer = Layers.Solid;
            }
            foreach (var t in hroot.GetComponentsInChildren<Terrain>(true)) { t.gameObject.layer = Layers.Ground; if (!t.GetComponent<TerrainCollider>()) t.gameObject.AddComponent<TerrainCollider>().terrainData = t.terrainData; }
            Physics.SyncTransforms();
            // Nachbearbeitung vor der Analyse: schwebende Gebäude absetzen, Kollider doppelseitig (sonst zählt das Innere von Gebäuden als frei)
            int settled = CityFix.Settle(), doubled = CityFix.DoubleSideColliders(); Physics.SyncTransforms();
            Debug.Log($"Nachbearbeitung: {settled} abgesetzt, {doubled} Kollider doppelseitig");
            // Grundflächen gebäudegroßer Objekte (auch dachlose Ruinen) sind gesperrt: kein Spawn/Generator im Hausinneren
            var footprints = hroot.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer) && !r.GetComponentInParent<Terrain>() && r.bounds.size.x >= 10 && r.bounds.size.z >= 10 && r.bounds.size.y >= 8 && !IsVegetation(r.transform)).Select(r => r.bounds).ToList();
            Debug.Log($"Gebäude-Grundflächen gesperrt: {footprints.Count}");
            bool InBuilding(float x, float z) { foreach (var fb in footprints) if (x > fb.min.x && x < fb.max.x && z > fb.min.z && z < fb.max.z) return true; return false; }
            // Begehbarkeit pro Meter abtasten
            int n = (int)size; float half = size / 2;
            var walk = new bool[n * n]; var gy = new float[n * n];
            int solidMask = 1 << Layers.Solid, groundMask = 1 << Layers.Ground;
            for (int gz = 0; gz < n; gz++) for (int gx = 0; gx < n; gx++)
            {
                float x = -half + gx + 0.5f, z = -half + gz + 0.5f;
                if (!Physics.Raycast(new Vector3(x, 80, z), Vector3.down, out var hit, 200, solidMask | groundMask)) continue;
                bool onGround = ((1 << hit.collider.gameObject.layer) & groundMask) != 0 || hit.point.y < 1.0f;
                if (!onGround || hit.point.y > 2.5f) continue;
                float y = hit.point.y;
                if (Physics.CheckBox(new Vector3(x, y + 1.1f, z), new Vector3(0.42f, 0.75f, 0.42f), Quaternion.identity, solidMask)) continue;
                // Freier Himmel: nichts Festes über der Zelle (außer Vegetation) -> keine Gebäudeinnenräume, auch nicht offene Gerüste mit Dach
                bool roofed = false; foreach (var h in Physics.RaycastAll(new Vector3(x, y + 0.3f, z), Vector3.up, 80, solidMask)) if (!IsVegetation(h.collider)) { roofed = true; break; }
                if (roofed || InBuilding(x, z)) continue;
                walk[gz * n + gx] = true; gy[gz * n + gx] = y;
            }
            // größte zusammenhängende Fläche = Spielfeld
            var comp = new int[n * n]; int best = 0, bestSize = 0, cid = 0;
            var stack = new Stack<int>();
            for (int i = 0; i < n * n; i++)
            {
                if (!walk[i] || comp[i] != 0) continue; cid++; int cnt = 0; stack.Push(i); comp[i] = cid;
                while (stack.Count > 0) { int c = stack.Pop(); cnt++; int cx = c % n, cz = c / n; foreach (var (dx, dz) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) }) { int nx = cx + dx, nz = cz + dz; if (nx < 0 || nz < 0 || nx >= n || nz >= n) continue; int ni = nz * n + nx; if (walk[ni] && comp[ni] == 0) { comp[ni] = cid; stack.Push(ni); } } }
                if (cnt > bestSize) { bestSize = cnt; best = cid; }
            }
            for (int i = 0; i < n * n; i++) if (comp[i] != best) walk[i] = false;
            Debug.Log($"Begehbar: {bestSize} von {n * n} Zellen");
            // Abstand zum nächsten Hindernis (für Platzwahl)
            bool Clear(int gx, int gz, int r) { for (int dz = -r; dz <= r; dz++) for (int dx = -r; dx <= r; dx++) { int x = gx + dx, z = gz + dz; if (x < 0 || z < 0 || x >= n || z >= n || !walk[z * n + x]) return false; } return true; }
            Vector2 Cell(int gx, int gz) => new Vector2(-half + gx + 0.5f, -half + gz + 0.5f);
            // Generator: freie Stelle mit viel Platz, möglichst nah an der Mitte
            // Umschlossenheit: in wie vielen von 8 Richtungen steht binnen 45 m ein Gebäude? (Platz mitten in der Stadt statt am Feldrand)
            int Enclosed(Vector2 p) { int cnt = 0; for (int k = 0; k < 8; k++) { float a = k * Mathf.PI / 4; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); bool hit = false; for (float s = 3; s <= 45 && !hit; s += 3) { var q = p + dir * s; foreach (var fb in footprints) if (q.x > fb.min.x && q.x < fb.max.x && q.y > fb.min.z && q.y < fb.max.z) { hit = true; break; } } if (hit) cnt++; } return cnt; }
            int bgx = -1, bgz = -1; float bscore = float.MaxValue; int bestEnc = 0;
            for (int gz = 4; gz < n - 4; gz++) for (int gx = 4; gx < n - 4; gx++) { if (!Clear(gx, gz, 4)) continue; var cp = Cell(gx, gz); int enc = Enclosed(cp); float d = Vector2.Distance(cp, Vector2.zero) + 25f * (8 - enc); if (d < bscore) { bscore = d; bgx = gx; bgz = gz; bestEnc = enc; } }
            Debug.Log($"Generatorplatz: Zelle ({bgx},{bgz}) Umschlossenheit {bestEnc}/8, Score {bscore:F1}");
            if (bgx < 0) for (int gz = 2; gz < n - 2; gz++) for (int gx = 2; gx < n - 2; gx++) { if (!Clear(gx, gz, 2)) continue; float d = Vector2.Distance(Cell(gx, gz), Vector2.zero); if (d < bscore) { bscore = d; bgx = gx; bgz = gz; } }
            if (bgx < 0) { Debug.LogError("Keine freie Fläche für den Generator gefunden"); return; }
            Vector2 gen = Cell(bgx, bgz);
            Vector2 Near(Vector2 from, Vector2 dir, int clearance) { for (int step = 3; step < 30; step++) { var p = from + dir * step; int gx = Mathf.FloorToInt(p.x + half), gz = Mathf.FloorToInt(p.y + half); if (gx < clearance || gz < clearance || gx >= n - clearance || gz >= n - clearance) break; if (Clear(gx, gz, clearance)) return Cell(gx, gz); } for (int gz = 2; gz < n - 2; gz++) for (int gx = 2; gx < n - 2; gx++) if (Clear(gx, gz, clearance) && Vector2.Distance(Cell(gx, gz), from) > 3) return Cell(gx, gz); return from + dir * 4; }
            Vector2 shop = Near(gen, new Vector2(1, -0.7f).normalized, 1), crate = Near(gen, new Vector2(-1, -0.7f).normalized, 1), spawn = Near(gen, new Vector2(0, 1), 1);
            // Zombie-Spawns: pro Richtung die am weitesten entfernte begehbare Zelle
            var zs = new List<Vector2>();
            for (int k = 0; k < 8; k++)
            {
                float a = k * Mathf.PI / 4; var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a)); Vector2 found = Vector2.zero; float fd = -1;
                for (int gz = 1; gz < n - 1; gz++) for (int gx = 1; gx < n - 1; gx++) { if (!Clear(gx, gz, 1)) continue; var c = Cell(gx, gz) - gen; float along = Vector2.Dot(c, dir); if (along < 20) continue; float side = Mathf.Abs(c.x * dir.y - c.y * dir.x); if (side > along * 0.45f) continue; if (along > fd) { fd = along; found = Cell(gx, gz); } }
                if (fd > 0 && !zs.Any(q => Vector2.Distance(q, found) < 6)) zs.Add(found);
            }
            if (zs.Count < 3) { Debug.LogWarning("Wenige Zombie-Spawns gefunden: " + zs.Count); }
            // Maske als Text (Zeilen = z, Zeichen = x)
            var sb = new System.Text.StringBuilder();
            for (int gz = 0; gz < n; gz++) { for (int gx = 0; gx < n; gx++) sb.Append(walk[gz * n + gx] ? '.' : '#'); if (gz < n - 1) sb.Append('\n'); }
            // Als eigene Szene speichern (nicht in Resources: 1,8 GB in resources.assets blockierten den App-Start)
            Directory.CreateDirectory("Assets/Scenes");
            if (File.Exists(Gen + "/City.prefab")) AssetDatabase.DeleteAsset(Gen + "/City.prefab");
            var cityScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(hroot, cityScene);
            EditorSceneManager.SaveScene(cityScene, CityScenePath);
            // Karten-Datei für den Server (Begehbarkeitsmaske statt Kästen; Kollision im Client über die Mesh-Kollider)
            string js = "// Automatisch erzeugt (CityExport) – Begehbarkeitsmaske der Ruinenstadt aus dem Dead-District-Paket\n" +
                "export const RUINS = {\n" +
                "  id: 'ruins', name: 'Ruinenstadt', desc: 'Verfallene Großstadt – eng, dunkel, überwuchert. Nur in der Unity-App.', unityOnly: true,\n" +
                $"  size: {F(size)}, ground: '#4a4a44', border: 'wall',\n" +
                "  sky: '#6b7a86', fog: ['#7d8791', 18, 95], sun: { color: '#ffd9b3', intensity: 1.6, pos: [-30, 28, 22] }, ambient: 0.8, hemiGround: '#3a3a33',\n" +
                "  decals: [], objects: [],\n" +
                $"  gen: {{ x: {F(gen.x)}, z: {F(gen.y)} }}, shop: {{ x: {F(shop.x)}, z: {F(shop.y)} }}, crate: {{ x: {F(crate.x)}, z: {F(crate.y)} }}, spawn: {{ x: {F(spawn.x)}, z: {F(spawn.y)} }},\n" +
                $"  zspawns: [{string.Join(", ", zs.Select(q => $"[{F(q.x)}, {F(q.y)}]"))}],\n" +
                "  mask: `" + sb + "`,\n};\n";
            string mapsPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../shared/maps-ruins.js"));
            File.WriteAllText(mapsPath, js);
            Debug.Log($"Ruinenstadt exportiert: {bestSize} begehbare Zellen, Generator bei {gen}, {zs.Count} Zombie-Spawns -> {mapsPath}");
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static bool IsVegetation(Collider col) => IsVegetation(col.transform);
        static bool IsVegetation(Transform tr) { for (var t = tr; t; t = t.parent) { string nm = t.name.ToLowerInvariant(); if (nm.Contains("tree") || nm.Contains("plant") || nm.Contains("bush") || nm.Contains("grass") || nm.Contains("ivy") || nm.Contains("leaf") || nm.Contains("branch")) return true; if (t.name == "City") break; } return false; }
        static string F(float v) => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

        static float SampleGround(GameObject root, Vector3 at)
        {
            float best = 0; bool found = false;
            foreach (var t in root.GetComponentsInChildren<Terrain>(true)) { var local = at - t.transform.position; if (local.x >= 0 && local.z >= 0 && local.x <= t.terrainData.size.x && local.z <= t.terrainData.size.z) { best = t.SampleHeight(at) + t.transform.position.y; found = true; } }
            if (!found) { var hits = Physics.RaycastAll(at + Vector3.up * 200, Vector3.down, 400); foreach (var h in hits) if (h.collider.GetComponentInParent<Terrain>() || h.collider.bounds.size.x > 20) { best = h.point.y; found = true; break; } }
            return best;
        }

        /// <summary>Sucht ab `start` spiralförmig eine Stelle, an der kein Renderer-Kasten im Radius liegt</summary>
        static Vector2 FindFree(GameObject root, Vector2 start, float radius, int maxRing)
        {
            var boxes = root.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && !(r is ParticleSystemRenderer) && r.bounds.size.y > 0.4f && r.bounds.min.y < 2f && !r.GetComponentInParent<Terrain>()).Select(r => r.bounds).ToList();
            bool Free(Vector2 p) { foreach (var bb in boxes) { if (p.x > bb.min.x - radius && p.x < bb.max.x + radius && p.y > bb.min.z - radius && p.y < bb.max.z + radius) return false; } return true; }
            if (Free(start)) return start;
            for (int ring = 1; ring <= maxRing; ring++)
                for (int i = 0; i < ring * 8; i++) { float a = i / (float)(ring * 8) * Mathf.PI * 2; var p = start + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * ring * 1.5f; if (Free(p)) return p; }
            return start;
        }
    }
}
