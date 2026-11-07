// Misst die echten Waffen-Prefabs (Ausdehnung relativ zur Wurzel, Pose nach Animator-Update), um das Viewmodel zu kalibrieren
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ZA
{
    public static class WeaponProbe
    {
        public static void Run()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Single);
            foreach (var id in new[] { "pistol", "smg", "shotgun", "rifle", "sniper", "launcher", "minigun" })
            {
                var prefab = Resources.Load<GameObject>("Generated/Weapons/" + id);
                if (!prefab) { Debug.Log($"PROBE {id}: kein Prefab"); continue; }
                var go = Object.Instantiate(prefab); go.transform.position = Vector3.zero; go.transform.rotation = Quaternion.identity;
                var anim = go.GetComponent<Animator>() ?? go.GetComponentInChildren<Animator>();
                string animInfo = anim ? $"animator={anim.name} ctrl={(anim.runtimeAnimatorController ? anim.runtimeAnimatorController.name : "-")} rootMotion={anim.applyRootMotion}" : "kein Animator";
                foreach (string state in new[] { "none", "Idle", "Fire" })
                {
                    if (anim && state != "none") { foreach (var p in anim.parameters) if (p.type == AnimatorControllerParameterType.Bool) anim.SetBool(p.name, p.name == state); anim.Update(0f); for (int i = 0; i < 10; i++) anim.Update(0.05f); }
                    var rends = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !(r is ParticleSystemRenderer)).ToList();
                    foreach (var smr in rends.OfType<SkinnedMeshRenderer>()) smr.updateWhenOffscreen = true;
                    Bounds b = new Bounds(); bool first = true; var per = new System.Collections.Generic.List<(string n, Bounds bb)>();
                    foreach (var r in rends)
                    {
                        Bounds rb;
                        if (r is SkinnedMeshRenderer smr) { var m = new Mesh(); smr.BakeMesh(m, true); var v = m.vertices; if (v.Length == 0) continue; rb = new Bounds(smr.transform.TransformPoint(v[0]), Vector3.zero); foreach (var q in v) rb.Encapsulate(smr.transform.TransformPoint(q)); Object.DestroyImmediate(m); }
                        else rb = r.bounds;
                        per.Add((r.name, rb));
                        if (first) { b = rb; first = false; } else b.Encapsulate(rb);
                    }
                    var big = per.OrderByDescending(x => x.bb.size.magnitude).Take(5).Select(x => $"{x.n}:{x.bb.center:F2}/{x.bb.size:F2}");
                    var holder = go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.Contains("CameraHolder"));
                    Debug.Log($"PROBE {id} [{state}] root={go.transform.position:F3} rot={go.transform.eulerAngles:F1} scale={go.transform.localScale:F2} bounds.min={b.min:F2} bounds.max={b.max:F2} renderers={rends.Count} holder={(holder ? holder.position.ToString("F3") + "/" + holder.eulerAngles.ToString("F1") : "-")} {animInfo}\n   big: {string.Join(" | ", big)}");
                }
                Object.DestroyImmediate(go);
            }
        }
    }
}
