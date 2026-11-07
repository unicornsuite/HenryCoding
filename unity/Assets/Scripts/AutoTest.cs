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
            yield return Shot("01_lobby"); yield return new WaitForSeconds(7); yield return Shot("01b_lobby_late");
            yield return new WaitUntil(() => g.DebugConnected); yield return new WaitForSeconds(0.5f);
            if (!string.IsNullOrEmpty(mapOverride)) { g.DebugMap(mapOverride); yield return new WaitForSeconds(0.5f); }
            g.DebugStart();
            yield return new WaitUntil(() => g.DebugInGame); yield return new WaitForSeconds(2.5f);
            yield return Shot("02_spawn");
            g.DebugLook(0, -0.05f); yield return new WaitForSeconds(0.3f); yield return Shot("03_lookgen");
            g.DebugLook(Mathf.PI, -0.1f); yield return new WaitForSeconds(0.3f); yield return Shot("04_lookaway");
            // Kalibrierungsbilder für das echte Waffenmodell
            foreach (var (yaw, sc) in new[] { (180f, 0.22f), (180f, 0.15f), (180f, 0.3f), (0f, 0.22f) }) { g.DebugVm(yaw, sc); yield return new WaitForSeconds(0.3f); yield return Shot($"04c_vm_{yaw:0}_{sc:0.00}"); }
            g.DebugVm(180f, 0.22f);
            g.DebugPreviewZombies(true); yield return new WaitForSeconds(1.2f); yield return Shot("04b_zombies"); yield return new WaitForSeconds(0.8f);
            // Zombie-Schau: nähere Gruppen
            g.DebugPreviewZombies(true, new[] { "walker", "runner", "crawler" }, 3.2f, 1.5f, -0.12f); yield return new WaitForSeconds(1.0f); yield return Shot("04d_zombies_close1"); yield return new WaitForSeconds(0.7f); yield return Shot("04d_zombies_close1b");
            // Todes-Schau: links normal, Mitte Kopfschuss, rechts Explosion
            g.DebugKillPreview(); yield return new WaitForSeconds(0.25f); yield return Shot("04k_deaths_0"); yield return new WaitForSeconds(0.6f); yield return Shot("04k_deaths_1"); yield return new WaitForSeconds(1.2f); yield return Shot("04k_deaths_2"); yield return new WaitForSeconds(2.5f); yield return Shot("04k_deaths_3");
            g.DebugLook(Mathf.PI, -0.45f); yield return new WaitForSeconds(0.3f); yield return Shot("04k_deaths_ground");
            g.DebugPreviewZombies(true, new[] { "spitter", "exploder", "brute" }, 3.6f, 1.7f, -0.1f); yield return new WaitForSeconds(1.0f); yield return Shot("04d_zombies_close2");
            g.DebugPreviewZombies(true, new[] { "boss" }, 4.5f, 1.7f, 0.05f); yield return new WaitForSeconds(1.0f); yield return Shot("04d_zombies_boss");
            // Waffen-Schau: jede Waffe einmal vor Zombie-Kulisse
            g.DebugPreviewZombies(true, new[] { "walker", "runner", "brute", "walker" }, 7f, 2.2f, -0.06f);
            foreach (var w in new[] { "smg", "shotgun", "rifle", "sniper", "minigun", "launcher", "pistol" })
            {
                g.DebugWeapon(w);
                if (w == "rifle") yield return Sheet("sheet_rifle", 60); else yield return new WaitForSeconds(0.9f);
                yield return Shot("04e_weapon_" + w); yield return new WaitForSeconds(0.6f); yield return Shot("04e_weapon_" + w + "_b");
            }
            g.DebugPreviewZombies(false); g.DebugLook(Mathf.PI, -0.1f); yield return new WaitForSeconds(0.3f);
            g.DebugOpenShop(); yield return new WaitForSeconds(0.5f); yield return Shot("05_shop"); g.DebugShopTab("perks"); yield return new WaitForSeconds(0.3f); yield return Shot("06_shop_perks"); g.CloseShop(false);
            g.DebugReady();
            yield return new WaitForSeconds(6);
            // Zombies kommen: in Richtung des nächsten Zombies schauen
            for (int i = 0; i < 6; i++) { g.DebugFaceNearestZombie(); g.DebugFire(1.0f); yield return new WaitForSeconds(1.4f); yield return Shot("07_fight_" + i); }
            g.DebugThrowGrenade(); yield return new WaitForSeconds(1.2f); yield return Shot("08_grenade");
            g.DebugMelee(); yield return new WaitForSeconds(0.2f); yield return Shot("09_melee"); DumpNear();
            g.DebugThirdPerson(true); yield return new WaitForSeconds(0.8f); yield return Shot("10_thirdperson"); g.DebugFire(0.4f); yield return new WaitForSeconds(0.5f); yield return Shot("11_thirdperson_fire");
            File.WriteAllText(Path.Combine(dir, "state.txt"), g.DebugState());
            File.WriteAllText(Path.Combine(dir, "walls.txt"), g.DebugWallTest());
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }
        /// <summary>Kontaktbogen: n aufeinanderfolgende Frames verkleinert in ein Bild, dazu pro Frame eine Logzeile</summary>
        IEnumerator Sheet(string name, int frames, int cols = 10)
        {
            int step = 8, w = Screen.width / step, h = Screen.height / step, rows = (frames + cols - 1) / cols;
            var sheet = new Texture2D(cols * w, rows * h, TextureFormat.RGB24, false); var full = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            var sb = new System.Text.StringBuilder();
            for (int f = 0; f < frames; f++)
            {
                yield return new WaitForEndOfFrame();
                full.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false); var px = full.GetPixels32();
                int col = f % cols, row = f / cols;
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) sheet.SetPixel(col * w + x, (rows - 1 - row) * h + y, px[(y * step) * Screen.width + x * step]);
                sb.AppendLine($"{name} f={f} t={Time.time:0.000} {Game.I.DebugAnimState()}");
            }
            sheet.Apply(); File.WriteAllBytes(Path.Combine(dir, name + ".png"), sheet.EncodeToPNG()); File.AppendAllText(Path.Combine(dir, "sheets.txt"), sb.ToString());
            Object.Destroy(sheet); Object.Destroy(full);
        }
        /// <summary>Screenshot aus dem echten Bildspeicher am Frame-Ende (ScreenCapture.CaptureScreenshot rendert in HDRP erneut und verliert dabei das Waffenmodell)</summary>
        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0, false); tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG()); Object.Destroy(tex);
        }
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
