// Nachbearbeitung der exportierten Stadtszene:
// 1) Schwebende Großobjekte auf den Boden setzen
// 2) Mesh-Kollider doppelseitig machen (einseitige Gebäudemeshes blockieren sonst nur von außen -> man läuft durch Wände)
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ZA
{
    public static class CityFix
    {
        const string ColDir = "Assets/Scenes/CityColliders";

        [MenuItem("Zombie-Ansturm/Ruinenstadt nachbearbeiten")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(CityExport.CityScenePath, OpenSceneMode.Single);
            Physics.SyncTransforms();
            int settled = Settle();
            int doubled = DoubleSideColliders();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log($"CITYFIX fertig: {settled} Objekte abgesetzt, {doubled} Kollider doppelseitig");
        }

        /// <summary>Große Objekte (Gebäude), deren Unterkante über dem höchsten Bodenpunkt ihrer Grundfläche schwebt, absenken</summary>
        public static int Settle()
        {
            int groundMask = 1 << Layers.Ground, solidMask = 1 << Layers.Solid, count = 0;
            var rends = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer) && !r.GetComponentInParent<Terrain>()).ToList();
            foreach (var r in rends.OrderByDescending(r => r.bounds.size.magnitude))
            {
                var b = r.bounds;
                if (b.size.y < 6 || b.size.x < 8 || b.size.z < 8) continue; // nur Gebäude-Größe
                if (r.transform.parent != null && r.transform.parent.name != "City") continue; // nur Wurzelobjekte der Stadt (keine Anbauten wie Feuerleitern)
                float highest = float.MinValue; int found = 0;
                foreach (var p in new[] { b.center, new Vector3(b.min.x + 0.5f, 0, b.min.z + 0.5f), new Vector3(b.max.x - 0.5f, 0, b.min.z + 0.5f), new Vector3(b.min.x + 0.5f, 0, b.max.z - 0.5f), new Vector3(b.max.x - 0.5f, 0, b.max.z - 0.5f), new Vector3(b.center.x, 0, b.min.z + 0.5f), new Vector3(b.center.x, 0, b.max.z - 0.5f), new Vector3(b.min.x + 0.5f, 0, b.center.z), new Vector3(b.max.x - 0.5f, 0, b.center.z) })
                {
                    var hits = Physics.RaycastAll(new Vector3(p.x, b.min.y + 1.5f, p.z), Vector3.down, 60, groundMask | solidMask).Where(h => h.collider.transform != r.transform && !h.collider.transform.IsChildOf(r.transform)).OrderBy(h => h.distance).ToList();
                    if (hits.Count == 0) continue; found++; highest = Mathf.Max(highest, hits[0].point.y);
                }
                if (found == 0) continue;
                float gap = b.min.y - highest;
                if (gap > 0.3f)
                {
                    r.transform.position += Vector3.down * (gap - 0.05f); count++;
                    Debug.Log($"CITYFIX abgesetzt: {r.name} um {gap:F2} m (center {b.center:F0})");
                    Physics.SyncTransforms();
                }
            }
            return count;
        }

        /// <summary>Ersetzt die Meshes nicht-konvexer Mesh-Kollider durch Kopien mit beidseitigen Dreiecken</summary>
        public static int DoubleSideColliders()
        {
            Directory.CreateDirectory(ColDir);
            var cache = new Dictionary<Mesh, Mesh>(); int count = 0;
            foreach (var mc in Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
            {
                var src = mc.sharedMesh; if (src == null || mc.convex) continue;
                if (src.name.EndsWith("_ds")) continue;
                if (!cache.TryGetValue(src, out var ds))
                {
                    string path = $"{ColDir}/{Sanitize(src.name)}_{src.vertexCount}_{src.triangles.Length}_ds.asset"; // stabiler Name, damit ein erneuter Export dieselben Dateien wiederverwendet
                    ds = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (ds == null)
                    {
                        ds = new Mesh { name = src.name + "_ds", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        ds.vertices = src.vertices;
                        var tris = new List<int>();
                        for (int s = 0; s < src.subMeshCount; s++) { var t = src.GetTriangles(s); tris.AddRange(t); for (int i = 0; i < t.Length; i += 3) { tris.Add(t[i]); tris.Add(t[i + 2]); tris.Add(t[i + 1]); } }
                        ds.triangles = tris.ToArray(); ds.RecalculateBounds();
                        AssetDatabase.CreateAsset(ds, path);
                    }
                    cache[src] = ds;
                }
                mc.sharedMesh = ds; count++;
            }
            return count;
        }
        static string Sanitize(string s) { foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace(' ', '_'); }
    }
}
