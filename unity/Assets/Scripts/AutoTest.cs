// Selbsttest: `Zombie-Ansturm.app/Contents/MacOS/Zombie-Ansturm -autotest <ordner> -server localhost:3000`
// Verbindet, startet ein Spiel, läuft ein Skript aus Aktionen ab und speichert Screenshots in den Ordner.
using System.Collections;
using System.IO;
using UnityEngine;

namespace ZA
{
    public class AutoTest : MonoBehaviour
    {
        string dir; public static string serverOverride, mapOverride;
        public static bool Active;

        public static void Start(GameObject host)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autotest" && i + 1 < args.Length) { var at = host.AddComponent<AutoTest>(); at.dir = args[i + 1]; Active = true; }
                if (args[i] == "-server" && i + 1 < args.Length) serverOverride = args[i + 1];
                if (args[i] == "-map" && i + 1 < args.Length) mapOverride = args[i + 1];
            }
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(dir);
            Application.logMessageReceived += (c, s, t) => { if (t == LogType.Error || t == LogType.Exception) File.AppendAllText(Path.Combine(dir, "errors.txt"), c + "\n" + s + "\n"); };
            var g = Game.I;
            yield return new WaitForSeconds(2);
            Shot("01_lobby");
            yield return new WaitUntil(() => g.DebugConnected); yield return new WaitForSeconds(0.5f);
            if (!string.IsNullOrEmpty(mapOverride)) { g.DebugMap(mapOverride); yield return new WaitForSeconds(0.5f); }
            g.DebugStart();
            yield return new WaitUntil(() => g.DebugInGame); yield return new WaitForSeconds(2.5f);
            Shot("02_spawn");
            g.DebugLook(0, -0.05f); yield return new WaitForSeconds(0.3f); Shot("03_lookgen");
            g.DebugLook(Mathf.PI, -0.1f); yield return new WaitForSeconds(0.3f); Shot("04_lookaway");
            // Kalibrierungsbilder für das echte Waffenmodell
            foreach (var (yaw, sc) in new[] { (180f, 0.22f), (180f, 0.15f), (180f, 0.3f), (0f, 0.22f) }) { g.DebugVm(yaw, sc); yield return new WaitForSeconds(0.3f); Shot($"04c_vm_{yaw:0}_{sc:0.00}"); }
            g.DebugVm(180f, 0.22f);
            g.DebugPreviewZombies(true); yield return new WaitForSeconds(1.2f); Shot("04b_zombies"); yield return new WaitForSeconds(0.8f); g.DebugPreviewZombies(false); g.DebugLook(Mathf.PI, -0.1f); yield return new WaitForSeconds(0.3f);
            g.DebugOpenShop(); yield return new WaitForSeconds(0.5f); Shot("05_shop"); g.DebugShopTab("perks"); yield return new WaitForSeconds(0.3f); Shot("06_shop_perks"); g.CloseShop(false);
            g.DebugReady();
            yield return new WaitForSeconds(6);
            // Zombies kommen: in Richtung des nächsten Zombies schauen
            for (int i = 0; i < 4; i++) { g.DebugFaceNearestZombie(); g.DebugFire(0.6f); yield return new WaitForSeconds(1.5f); Shot("07_fight_" + i); }
            g.DebugThrowGrenade(); yield return new WaitForSeconds(1.2f); Shot("08_grenade");
            g.DebugMelee(); yield return new WaitForSeconds(0.2f); Shot("09_melee"); DumpNear();
            g.DebugThirdPerson(true); yield return new WaitForSeconds(0.8f); Shot("10_thirdperson"); g.DebugFire(0.4f); yield return new WaitForSeconds(0.5f); Shot("11_thirdperson_fire");
            File.WriteAllText(Path.Combine(dir, "state.txt"), g.DebugState());
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
        void Shot(string name) { ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png")); }
        /// <summary>Diagnose: alle sichtbaren Renderer nahe der Kamera (Name, Pfad, Layer, Bounds)</summary>
        void DumpNear()
        {
            var cam = Camera.main; var sb = new System.Text.StringBuilder(); sb.AppendLine("cullingMask=" + cam.cullingMask);
            foreach (var c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) sb.AppendLine($"camera {c.name} enabled={c.enabled} active={c.gameObject.activeInHierarchy} mask={c.cullingMask} depth={c.depth} pos={c.transform.position:F2}");
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)) if (r.isVisible && r.gameObject.layer == Layers.SelfFP) sb.AppendLine("SICHTBAR trotz SelfFP: " + r.name);
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (Vector3.Distance(r.bounds.ClosestPoint(cam.transform.position), cam.transform.position) > 1.2f) continue;
                string path = r.name; for (var t = r.transform.parent; t; t = t.parent) path = t.name + "/" + path;
                sb.AppendLine($"{path} layer={r.gameObject.layer} visible={r.isVisible} shadows={r.shadowCastingMode} center={r.bounds.center:F2} size={r.bounds.size:F2}");
            }
            File.WriteAllText(Path.Combine(dir, "near.txt"), sb.ToString());
        }
    }
}
