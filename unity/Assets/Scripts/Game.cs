// Spielsteuerung: Verbindung, Lobby, Spielzustand, Schießen, Shop, Platzieren, HUD
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace ZA
{
    public class Game : MonoBehaviour
    {
        public static Game I;
        Net net; UI ui; Effects fx; Sfx sfx; Entities ents; World world; Player player; Camera cam;
        static GameData D => GameData.I;

        // Einstellungen
        float sens = 1, vol = 0.7f, fov = 78; string quality = "medium", myName = "", myColorPref = "", serverUrl = "localhost:3000";
        // Lobby / Verbindung
        int myId = -1; string myColor = "#1e88e5"; List<string> lanUrls = new List<string>();
        string lobbyMap = "suburb", lobbyDiff = "normal", lobbyPhase = "lobby"; List<(int id, string name, string color, bool inGame)> lobbyPlayers = new List<(int, string, string, bool)>();
        bool wantJoin;
        // Spiel
        bool inGame, gameOver, shopOpen, chatOpen, paused;
        MapDef mapDef; string diffId = "normal";
        float T;
        // Ich
        int hp = 100, maxHp = 100, kills, money; float armor, downT, rev; string state = "alive"; bool ready;
        List<string> owned = new List<string> { "pistol" }; string weapon = "pistol"; readonly Dictionary<string, (int mag, int res)> ammo = new Dictionary<string, (int, int)>();
        Dictionary<string, int> upg = new Dictionary<string, int>(); List<string> perks = new List<string>(); int nades;
        float fireCd, meleeCd; int streak; float lastKill = -9;
        // Snapshot
        int wave, left, genHp = 2500; string phase = "prep"; float timer; float[] pu = new float[3];
        // Platzieren
        class Placing { public string type; public GameObject ghost; public List<Material> mats = new List<Material>(); public bool valid, pending; public float x, z; public int rot; public string reason = ""; public Transform ring; }
        Placing placing;
        float crateSpinT; Light flashlight; bool flashOn = true;
        float hudT, sendT, zucT; bool zucHit;
        // Lobby-Hintergrund: Karte mit Kamerafahrt und herumlaufenden Zombies
        World lobbyWorld; string lobbyWorldMap = ""; float lobbyCamT; readonly List<Zombie> lobbyZombies = new List<Zombie>();
        // Eingabe-Merker
        bool mouseDown, mouseClicked, rightDown;
        float reviveSendT;

        // ------------------------------------------------------------------ Start
        void Awake()
        {
            I = this;
            Application.targetFrameRate = 120;
            GameData.Load();
            cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Main Camera"); cg.tag = "MainCamera"; cam = cg.AddComponent<Camera>(); cg.AddComponent<AudioListener>(); }
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 700; cam.fieldOfView = fov;
            LoadSettings();
            ui = new UI(); fx = new Effects(cam); sfx = new Sfx { volume = vol, listener = cam.transform }; ents = new Entities(fx, sfx, cam) { playerInfo = id => { var p = lobbyPlayers.Find(q => q.id == id); return p.id == id ? (p.name, p.color) : ("Spieler", "#9e9e9e"); } };
            player = new Player(cam);
            ApplyQuality();
            SetupPostProcessing();
            Physics.IgnoreLayerCollision(Layers.World, Layers.Hitbox, true);
            net = new Net();
            ui.BuildLobbyChoices(c => { myColorPref = c; myColor = c; SaveSettings(); SendHello(); }, m => net.Send("t", "map", "map", m), d => net.Send("t", "diff", "diff", d));
            ui.serverField.text = serverUrl; ui.nameField.text = myName;
            ui.serverField.onEndEdit.AddListener(v => { serverUrl = v.Trim(); SaveSettings(); Connect(); });
            ui.nameField.onEndEdit.AddListener(v => { myName = v.Trim(); SaveSettings(); SendHello(); });
            ui.startBtn.onClick.AddListener(OnStartClicked);
            ui.onBuy = item => net.Send("t", "buy", "item", item);
            ui.onUpgrade = w => net.Send("t", "buy", "item", "upgrade", "w", w);
            ui.onEquip = id => { Equip(id); RefreshShop(); };
            ui.onTurret = type => { CloseShop(false); StartPlacing(type); };
            ui.onTab = _ => RefreshShop();
            ui.chatInput.onEndEdit.AddListener(OnChatEnd);
            ui.SetSettingsText(sens, vol, fov, quality);
            ui.startBtn.interactable = false;
            AutoTest.Start(gameObject);
            if (!string.IsNullOrEmpty(AutoTest.serverOverride)) { serverUrl = AutoTest.serverOverride; ui.serverField.text = serverUrl; }
            Connect();
        }

        // ------------------------------------------------------------------ Selbsttest-Hilfen
        public bool DebugConnected => myId > 0;
        public bool DebugInGame => inGame && world != null;
        public void DebugStart() { OnStartClicked(); }
        public void DebugMap(string id) { net.Send("t", "map", "map", id); }
        bool debugPause;
        /// <summary>Stellt lokal alle Zombietypen vor die Kamera (nur Optik)</summary>
        public void DebugPreviewZombies(bool on)
        {
            debugPause = on;
            if (!on) { foreach (var z in ents.zombies.Values.ToList()) if (z.id >= 9000) ents.RemoveZombie(z); return; }
            var p = player.Pos; player.yaw = 0; player.pitch = -0.08f;
            string[] types = { "walker", "runner", "crawler", "spitter", "exploder", "brute", "boss" };
            for (int i = 0; i < types.Length; i++) { var z = ents.CreateZombie(9000 + i, types[i], p.x - 5.4f + i * 1.8f, p.z - 6.5f - (i == 6 ? 2.5f : 0), 0, false); z.walk = 0.6f; z.atkT = i == 1 ? 0.5f : 0; }
        }
        public void DebugLook(float yaw, float pitch) { player.yaw = yaw; player.pitch = pitch; }
        public void DebugOpenShop() { OpenShop(); }
        public void DebugShopTab(string tab) { ui.shopTab = tab; RefreshShop(); }
        public void DebugReady() { net.Send("t", "ready"); }
        public void DebugFaceNearestZombie()
        {
            Zombie best = null; float bd = 1e9f; var me = player.Pos;
            foreach (var z in ents.zombies.Values) { if (z.dying) continue; float d = (new Vector2(z.x, z.z) - new Vector2(me.x, me.z)).sqrMagnitude; if (d < bd) { bd = d; best = z; } }
            if (best != null) { player.yaw = Mathf.Atan2(-(best.x - me.x), -(best.z - me.z)); player.pitch = -0.03f; }
        }
        float debugFireT;
        public void DebugFire(float secs) { debugFireT = secs; }
        public void DebugThrowGrenade() { nades = Mathf.Max(nades, 1); ThrowGrenade(); }
        public void DebugMelee() { MeleeAttack(); }
        public void DebugThirdPerson(bool on) { player.SetThirdPerson(on); }
        public string DebugState() => $"wave={wave} phase={phase} hp={hp} money={money} kills={kills} zombies={ents.zombies.Count} remotes={ents.remotes.Count} weapon={weapon} mag={ammo[weapon].mag} pos={player.Pos} fps={1f / Time.smoothDeltaTime:0}";

        void SetupPostProcessing()
        {
            var go = new GameObject("PostFX"); var vol = go.AddComponent<UnityEngine.Rendering.Volume>(); vol.isGlobal = true; vol.priority = 1;
            var prof = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>(); vol.profile = prof;
            var ex = prof.Add<UnityEngine.Rendering.HighDefinition.Exposure>(true); ex.mode.Override(UnityEngine.Rendering.HighDefinition.ExposureMode.Automatic); ex.limitMin.Override(-2f); ex.limitMax.Override(14f); ex.compensation.Override(0.3f); ex.adaptationSpeedDarkToLight.Override(4f); ex.adaptationSpeedLightToDark.Override(2f);
            var bloom = prof.Add<UnityEngine.Rendering.HighDefinition.Bloom>(true); bloom.intensity.Override(0.35f); bloom.scatter.Override(0.65f); bloom.threshold.Override(0.9f);
            var vig = prof.Add<UnityEngine.Rendering.HighDefinition.Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.4f);
            var ca = prof.Add<UnityEngine.Rendering.HighDefinition.ColorAdjustments>(true); ca.contrast.Override(12f); ca.saturation.Override(-5f);
            var tm = prof.Add<UnityEngine.Rendering.HighDefinition.Tonemapping>(true); tm.mode.Override(UnityEngine.Rendering.HighDefinition.TonemappingMode.ACES);
            var ao = prof.Add<UnityEngine.Rendering.HighDefinition.ScreenSpaceAmbientOcclusion>(true); ao.intensity.Override(1.2f); ao.radius.Override(1.5f);
            var ssr = prof.Add<UnityEngine.Rendering.HighDefinition.ScreenSpaceReflection>(true); ssr.enabled.Override(true);
            var cs = prof.Add<UnityEngine.Rendering.HighDefinition.ContactShadows>(true); cs.enable.Override(true);
            var mb = prof.Add<UnityEngine.Rendering.HighDefinition.MotionBlur>(true); mb.intensity.Override(0.3f);
            var fg = prof.Add<UnityEngine.Rendering.HighDefinition.FilmGrain>(true); fg.intensity.Override(0.2f);
            var ca2 = prof.Add<UnityEngine.Rendering.HighDefinition.ChromaticAberration>(true); ca2.intensity.Override(0.08f);
        }

        void LoadSettings()
        {
            sens = PlayerPrefs.GetFloat("sens", 1); vol = PlayerPrefs.GetFloat("vol", 0.7f); fov = PlayerPrefs.GetFloat("fov", 78); quality = PlayerPrefs.GetString("quality", "medium");
            myName = PlayerPrefs.GetString("name", ""); myColorPref = PlayerPrefs.GetString("color", ""); serverUrl = PlayerPrefs.GetString("server", "localhost:3000");
            if (!string.IsNullOrEmpty(myColorPref)) myColor = myColorPref;
        }
        void SaveSettings() { PlayerPrefs.SetFloat("sens", sens); PlayerPrefs.SetFloat("vol", vol); PlayerPrefs.SetFloat("fov", fov); PlayerPrefs.SetString("quality", quality); PlayerPrefs.SetString("name", myName); PlayerPrefs.SetString("color", myColorPref); PlayerPrefs.SetString("server", serverUrl); PlayerPrefs.Save(); }
        void ApplyQuality()
        {
            QualitySettings.shadows = quality == "low" ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowDistance = quality == "high" ? 150 : 90;
            ents.highQuality = quality == "high";
        }
        public void AdjustSetting(string key, int dir)
        {
            switch (key)
            {
                case "sens": sens = Mathf.Clamp(sens + dir * 0.1f, 0.2f, 3f); break;
                case "vol": vol = Mathf.Clamp01(vol + dir * 0.1f); sfx.volume = vol; break;
                case "fov": fov = Mathf.Clamp(fov + dir * 2, 60, 100); cam.fieldOfView = fov; break;
                case "quality": var qs = new[] { "low", "medium", "high" }; int i = Mathf.Clamp(Array.IndexOf(qs, quality) + dir, 0, 2); quality = qs[i]; if (ents != null) ApplyQuality(); break;
            }
            ui.SetSettingsText(sens, vol, fov, quality); SaveSettings();
        }

        string MyName => string.IsNullOrEmpty(myName) ? ("Spieler " + (myId > 0 ? myId.ToString() : "")).Trim() : myName;
        void Connect() { net.Connect(serverUrl); ui.connText.text = "Verbinde mit " + serverUrl + " …"; ui.connText.color = Color.white; }
        void SendHello() { if (myId > 0) net.Send("t", "hello", "name", MyName, "color", myColor); }
        void OnStartClicked()
        {
            myName = ui.nameField.text.Trim(); SaveSettings(); SendHello();
            if (lobbyPhase == "game") net.Send("t", "join"); else net.Send("t", "start");
        }

        // ------------------------------------------------------------------ Hauptschleife
        float reconnectT;
        void Update()
        {
            float dt = Mathf.Min(0.05f, Time.deltaTime); T += dt;
            // Netzwerk
            while (net.TryReceive(out var m)) OnMsg(m);
            if (net.state == Net.State.Disconnected)
            {
                if (inGame || gameOver) { CleanupGame(); gameOver = false; UI.Show(ui.over, false); UI.Show(ui.lobby, true); }
                ui.connText.text = "✖ Nicht verbunden" + (string.IsNullOrEmpty(net.lastError) ? "" : " – " + net.lastError) + " (neuer Versuch…)"; ui.connText.color = U.C("#ff8a80"); ui.startBtn.interactable = false;
                reconnectT -= dt; if (reconnectT <= 0) { reconnectT = 3; Connect(); }
            }
            else if (net.state == Net.State.Connected && myId < 0) { ui.connText.text = "Verbunden – warte auf Server …"; }
            ui.Tick(dt); ui.TickLobby(dt);
            if (!inGame && !gameOver) { if (Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; } UpdateLobbyScene(dt); return; }
            if (lobbyWorld != null) DisposeLobbyScene();
            if (world == null) return;
            ents.T = T; ents.frozen = pu[2] > 0;
            if (inGame)
            {
                HandleKeys();
                UpdateLocal(dt);
                UpdatePlacing();
                SendState(dt);
                UpdateHud(dt);
            }
            else
            { // Game Over: Kamerafahrt
                cam.transform.position = new Vector3(mapDef.gen.x + Mathf.Sin(T * 0.15f) * 22, 12, mapDef.gen.y + Mathf.Cos(T * 0.15f) * 22);
                cam.transform.LookAt(new Vector3(mapDef.gen.x, 1, mapDef.gen.y)); player.SetVmVisible(false);
            }
            ents.UpdateRemotes(dt); ents.UpdateZombies(dt); ents.UpdateTurrets(dt); ents.UpdatePickups(dt); ents.UpdateBarricades(dt);
            world.Update(dt); fx.Update(dt);
        }

        void UpdateLobbyScene(float dt)
        {
            if (!ui.lobby.activeSelf) return;
            if (lobbyWorld == null || lobbyWorldMap != lobbyMap)
            {
                DisposeLobbyScene();
                if (!D.map.ContainsKey(lobbyMap)) return;
                lobbyWorld = World.Build(D.map[lobbyMap], quality); lobbyWorldMap = lobbyMap; lobbyCamT = 0;
                ents.frozen = false;
                string[] types = { "walker", "runner", "walker", "crawler", "spitter", "walker", "brute", "exploder", "walker", "runner" };
                for (int i = 0; i < types.Length; i++) { var z = ents.CreateZombie(8000 + i, types[i], 0, 0, 0, false); z.seed = i * 1.7f; lobbyZombies.Add(z); }
            }
            var m = lobbyWorld.map; float t = lobbyCamT += dt;
            // Zombies wandern auf einem Kreis um den Generator, Kamera umrundet langsam
            for (int i = 0; i < lobbyZombies.Count; i++)
            {
                var z = lobbyZombies[i]; float a = t * 0.25f * (z.cfg.speed / 2.3f) * 0.6f + i * 0.63f; float rad = 9 + (i % 3) * 2.5f;
                z.tx = m.gen.x + Mathf.Cos(a) * rad; z.tz = m.gen.y + Mathf.Sin(a) * rad; z.trot = Mathf.Atan2(-Mathf.Sin(a), Mathf.Cos(a)); z.hp = 100;
                if (i % 4 == 1 && Mathf.Sin(t * 1.3f + i) > 0.9f) z.atkT = 0.5f;
            }
            ents.T = T; ents.UpdateZombies(dt); lobbyWorld.Update(dt); fx.Update(dt);
            float ca = t * 0.08f; float camR = 16;
            cam.transform.position = new Vector3(m.gen.x + Mathf.Cos(ca) * camR, 5.5f + Mathf.Sin(t * 0.3f) * 0.6f, m.gen.y + Mathf.Sin(ca) * camR);
            cam.transform.LookAt(new Vector3(m.gen.x, 1.8f, m.gen.y));
            cam.fieldOfView = 60;
        }
        void DisposeLobbyScene()
        {
            foreach (var z in lobbyZombies) if (ents.zombies.ContainsKey(z.id)) ents.RemoveZombie(z);
            lobbyZombies.Clear();
            if (lobbyWorld != null) { lobbyWorld.Dispose(); lobbyWorld = null; lobbyWorldMap = ""; }
            cam.fieldOfView = fov;
        }

        bool ControlsActive => inGame && !shopOpen && !chatOpen && !paused && (Cursor.lockState == CursorLockMode.Locked || AutoTest.Active);
        void Lock(bool on) { Cursor.lockState = on ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !on; }
        public void Resume() { paused = false; UI.Show(ui.pause, false); Lock(true); }
        public void LeaveGame() { net.Send("t", "leave"); CleanupGame(); gameOver = false; ui.ResetLobbyAnim(); UI.Show(ui.lobby, true); }
        public void BackToLobby() { CleanupGame(); gameOver = false; UI.Show(ui.over, false); ui.ResetLobbyAnim(); UI.Show(ui.lobby, true); }

        // ------------------------------------------------------------------ Nachrichten
        void OnMsg(Dictionary<string, object> m)
        {
            switch (J.S(m, "t"))
            {
                case "welcome":
                    myId = J.I(m, "id"); lanUrls.Clear(); foreach (var u in J.A(m, "urls")) lanUrls.Add(u.ToString());
                    if (string.IsNullOrEmpty(myColorPref) || !D.playerColors.Contains(myColorPref)) myColor = J.S(m, "color", myColor);
                    ui.connText.text = "✔ Mit Server verbunden"; ui.connText.color = U.C("#b9f6ca");
                    SendHello(); break;
                case "lobby":
                    lobbyMap = J.S(m, "map", lobbyMap); lobbyDiff = J.S(m, "diff", "normal"); lobbyPhase = J.S(m, "phase", "lobby");
                    lobbyPlayers.Clear(); foreach (var p in J.A(m, "players")) lobbyPlayers.Add((J.I(p, "id"), J.S(p, "name"), J.S(p, "color"), J.B(p, "inGame")));
                    var mine = lobbyPlayers.Find(p => p.id == myId); if (mine.id == myId) myColor = mine.color;
                    ui.RenderLobby(lobbyMap, lobbyDiff, myColor, lobbyPlayers, myId, lanUrls, lobbyPhase == "game", BestWave);
                    foreach (var r in ents.remotes.Values) { var p = lobbyPlayers.Find(q => q.id == r.id); if (p.id == r.id && p.name != r.name) ents.Relabel(r, p.name); }
                    break;
                case "start": StartGame(m); break;
                case "snap": if (inGame && !debugPause) ApplySnap(m); break;
                case "inv":
                    {
                        int prevTier = upg.TryGetValue(weapon, out var pt) ? pt : 0; string prevPerks = string.Join(",", perks);
                        var own = J.A(m, "owned").Select(o => o.ToString()).ToList(); owned = D.weapons.Where(w => own.Contains(w.id)).Select(w => w.id).ToList();
                        money = J.I(m, "money"); upg = new Dictionary<string, int>(); var uo = J.O(m, "upg"); if (uo != null) foreach (var kv in uo) upg[kv.Key] = (int)J.Num(kv.Value);
                        perks = J.A(m, "perks").Select(o => o.ToString()).ToList(); nades = J.I(m, "nades"); maxHp = J.I(m, "maxHp", 100);
                        foreach (var id in owned) if (!ammo.ContainsKey(id)) InitAmmo(id);
                        if ((upg.TryGetValue(weapon, out var nt) ? nt : 0) != prevTier || prevPerks != string.Join(",", perks)) { var a = ammo[weapon]; ammo[weapon] = (Mathf.Max(a.mag, Mathf.Min(WStat().mag, a.mag + 10)), a.res); player.BuildViewmodel(weapon, upg.TryGetValue(weapon, out var t2) ? t2 : 0, myColor); }
                        RefreshShop(); break;
                    }
                case "bought": OnBought(m); break;
                case "crate": OnCrateResult(J.O(m, "item")); break;
                case "respawn": player.Pos = new Vector3(J.F(m, "x"), J.F(m, "y"), J.F(m, "z")); player.vel = Vector3.zero; ui.Toast("Du bist zurück im Kampf!", "good"); break;
                case "over": OnGameOver(m); break;
                case "chat": if (J.B(m, "sys")) ui.Chat($"<color=#ffca28>{J.S(m, "text")}</color>"); else ui.Chat($"<color={J.S(m, "color", "#fff")}>{J.S(m, "name")}:</color> {J.S(m, "text")}"); break;
            }
        }

        // ------------------------------------------------------------------ Spielstart / Ende
        void StartGame(Dictionary<string, object> m)
        {
            CleanupGame(); gameOver = false; DisposeLobbyScene();
            mapDef = D.map[J.S(m, "map", "suburb")]; diffId = J.S(m, "diff", "normal");
            world = World.Build(mapDef, quality);
            ui.BuildMinimapBase(mapDef);
            var you = J.O(m, "you"); player.Pos = new Vector3(J.F(you, "x"), J.F(you, "y"), J.F(you, "z")); player.vel = Vector3.zero;
            float dx = player.Pos.x - mapDef.gen.x, dz = player.Pos.z - mapDef.gen.y; player.yaw = Mathf.Atan2(-dx, -dz); player.pitch = -0.05f;
            hp = 100; maxHp = 100; armor = 0; state = "alive"; weapon = "pistol"; owned = new List<string> { "pistol" }; ammo.Clear(); upg.Clear(); perks.Clear(); nades = 0; fireCd = 0; player.reloadT = 0; player.switchT = 0; player.spin = 0; player.aim = 0; player.recoilAcc = 0; player.meleeT = 0; player.nadeT = 0; streak = 0; lastKill = -9; crateSpinT = 0; UI.Show(ui.crateUi, false); UI.Show(ui.wavesum, false);
            InitAmmo("pistol");
            if (mapDef.night)
            {
                var fl = new GameObject("flashlight"); fl.transform.SetParent(cam.transform, false); fl.transform.localPosition = new Vector3(0.25f, -0.25f, 0); fl.transform.localRotation = Quaternion.Euler(4, 0, 0);
                flashlight = fl.AddComponent<Light>(); flashlight.type = LightType.Spot; flashlight.color = U.C("#fff3d6"); flashlight.intensity = 60; flashlight.range = 40; flashlight.spotAngle = 55; flashlight.innerSpotAngle = 30; flashlight.shadows = quality == "high" ? LightShadows.Soft : LightShadows.None; flashlight.cullingMask = ~(1 << Layers.ViewModel); flashOn = true;
            }
            inGame = true; paused = false;
            player.BuildViewmodel("pistol", 0, myColor); player.SetVmVisible(true);
            player.BuildSelf(myColor); player.SetThirdPerson(PlayerPrefs.GetInt("thirdperson", 0) == 1);
            UI.Show(ui.lobby, false); UI.Show(ui.over, false); UI.Show(ui.hud, true);
            ui.Announce(mapDef.name.ToUpper(), "Verteidige den Generator!  [B] Shop", 4);
            Lock(true);
        }
        void CleanupGame()
        {
            inGame = false; CancelPlacing();
            if (world != null) { world.Dispose(); world = null; }
            ents.Clear(); fx.Clear();
            if (flashlight) { Destroy(flashlight.gameObject); flashlight = null; }
            UI.Show(ui.hud, false); UI.Show(ui.shop, false); UI.Show(ui.pause, false); shopOpen = false; paused = false;
            ui.Downed(null); UI.Show(ui.scope, false); ui.Prompt(null); ui.Progress(null); ui.placeHint.text = "";
            if (chatOpen) CloseChat();
            Lock(false);
        }
        void OnGameOver(Dictionary<string, object> m)
        {
            gameOver = true; inGame = false; Lock(false);
            UI.Show(ui.shop, false); UI.Show(ui.pause, false); UI.Show(ui.hud, false); shopOpen = false; CancelPlacing(); ui.Downed(null);
            int w = J.I(m, "wave"); string mapId = J.S(m, "mapId", mapDef?.id ?? "suburb"), diff = J.S(m, "diff", "normal");
            var stats = J.A(m, "stats"); int totalKills = 0; foreach (var s in stats) totalKills += J.I(s, "kills");
            bool newRec = ReportRecord(mapId, diff, w, totalKills);
            ui.overReason.text = J.S(m, "reason"); ui.overWave.text = $"Erreichte Welle: {w}  ·  {J.S(m, "map")} · {D.diffs.Find(d => d.id == diff)?.name}";
            UI.Show(ui.overRecord.gameObject, newRec && w > 0);
            var sb = new System.Text.StringBuilder("<color=#ffca28>SPIELER            KILLS   SCHADEN   WIEDERBELEBT</color>\n");
            foreach (var s in stats) sb.Append($"<color={J.S(s, "color")}>■</color> {J.S(s, "name"),-16} {J.I(s, "kills"),5}   {J.I(s, "damage"),7}   {J.I(s, "revives"),5}\n");
            ui.overStats.text = sb.ToString();
            UI.Show(ui.over, true);
        }
        int BestWave(string map, string diff) => PlayerPrefs.GetInt($"rec_{map}:{diff}", 0);
        bool ReportRecord(string map, string diff, int w, int k)
        {
            PlayerPrefs.SetInt("games", PlayerPrefs.GetInt("games", 0) + 1); PlayerPrefs.SetInt("kills", PlayerPrefs.GetInt("kills", 0) + k);
            bool isNew = w > BestWave(map, diff); if (isNew) PlayerPrefs.SetInt($"rec_{map}:{diff}", w); PlayerPrefs.Save(); return isNew;
        }

        // ------------------------------------------------------------------ Waffen
        WeaponDef WStat(string id = null) { id ??= weapon; return D.weapon[id].Upgraded(upg.TryGetValue(id, out var t) ? t : 0, new HashSet<string>(perks)); }
        void InitAmmo(string id) { var w = WStat(id); ammo[id] = (w.mag, w.reserve); }
        void Equip(string id)
        {
            if (!owned.Contains(id) || (weapon == id && player.switchT <= 0)) return;
            weapon = id; player.switchT = 0.35f; player.reloadT = 0; player.spin = 0;
            player.BuildViewmodel(id, upg.TryGetValue(id, out var t) ? t : 0, myColor);
        }
        void CycleWeapon(int dir) { int i = owned.IndexOf(weapon); Equip(owned[(i + dir + owned.Count) % owned.Count]); }
        void StartReload()
        {
            var w = WStat(); var a = ammo[weapon];
            if (player.reloadT > 0 || a.mag >= w.mag || a.res <= 0 || player.switchT > 0) return;
            player.reloadT = w.reload; player.reloadDur = w.reload; sfx.Play("reload");
        }
        void FinishReload() { var w = WStat(); var a = ammo[weapon]; int take = Mathf.Min(w.mag - a.mag, a.res); ammo[weapon] = (a.mag + take, weapon == "pistol" ? a.res : a.res - take); }

        float CurrentSpread()
        {
            var w = WStat(); float hs = new Vector2(player.vel.x, player.vel.z).magnitude;
            float s = w.spread * (1 + hs / 5.4f * 0.8f) * (player.onGround ? 1 : 2.2f);
            if (weapon == "sniper") s = player.aim > 0.9f ? w.spread : 0.05f; else if (weapon == "shotgun") s *= 1 - player.aim * 0.25f; else s *= 1 - player.aim * 0.6f;
            if (weapon == "minigun") s *= 1 + (1 - player.spin) * 0.5f;
            return s;
        }

        void Shoot()
        {
            var w = WStat(); var a = ammo[weapon]; ammo[weapon] = (a.mag - 1, a.res); fireCd = w.rate; player.lastFire = Time.time;
            var origin = cam.transform.position; var f = cam.transform.forward; var r = cam.transform.right; var u = cam.transform.up;
            float spread = CurrentSpread(); var mz = player.thirdPerson ? player.SelfMuzzle() : player.MuzzleWorld();
            player.kick = 1; player.flashT = 0.05f; player.slideT = 1;
            fx.Muzzle(mz, weapon == "shotgun" ? 1.2f : 0.8f, true); sfx.Shoot(weapon, null);
            fx.Smoke(mz, f, weapon == "shotgun" || weapon == "sniper" ? 2 : 0.7f);
            if (weapon != "launcher") fx.Shell(player.thirdPerson ? mz - f * 0.3f : cam.transform.position + cam.transform.rotation * new Vector3(0.12f, -0.1f, 0.4f), r, u);
            float kick = w.recoil * (0.7f + UnityEngine.Random.value * 0.5f) * (1 - player.aim * 0.4f);
            player.pitch += kick; player.recoilAcc += kick; player.yaw += (UnityEngine.Random.value - 0.5f) * w.recoil * 0.5f;
            if (weapon == "launcher") { FireLauncher(mz, f); net.Send("t", "shot", "w", weapon, "o", V(mz), "ends", new List<object>()); return; }
            var hits = new Dictionary<int, (float dmg, bool head, Zombie z)>(); var ends = new List<object>();
            bool anyHit = false, anyHead = false;
            for (int p = 0; p < w.pellets; p++)
            {
                float ang = UnityEngine.Random.value * Mathf.PI * 2, rr = Mathf.Sqrt(UnityEngine.Random.value) * spread;
                var dir = (f + r * (Mathf.Cos(ang) * rr) + u * (Mathf.Sin(ang) * rr)).normalized;
                var list = Physics.RaycastAll(origin, dir, 150, Layers.ShootMask, QueryTriggerInteraction.Collide); Array.Sort(list, (x, y) => x.distance.CompareTo(y.distance));
                int pierce = w.pierce; var end = origin + dir * 150; var done = new HashSet<Zombie>();
                foreach (var it in list)
                {
                    var hb = it.collider.GetComponent<ZombieHitbox>();
                    if (hb != null)
                    {
                        var zz = hb.z; if (done.Contains(zz) || zz.dying) continue; done.Add(zz);
                        float dmg = w.damage * (hb.head ? (weapon == "sniper" ? 2.5f : 2f) : 1f);
                        if (weapon == "shotgun" && it.distance > 8) dmg *= Mathf.Max(0.35f, 1 - (it.distance - 8) / 20);
                        var h = hits.TryGetValue(zz.id, out var hv) ? hv : (0, false, zz); hits[zz.id] = (h.dmg + dmg, h.head || hb.head, zz);
                        zz.flash = 1; anyHit = true; anyHead |= hb.head;
                        fx.Blood(it.point, dir); fx.DamageNumber(it.point, dmg, hb.head);
                        if (--pierce <= 0) { end = it.point; break; }
                    }
                    else { end = it.point; fx.Sparks(it.point, it.normal); break; }
                }
                ends.Add(V(end));
                fx.Tracer(mz, end, U.C("#fff59d"), weapon == "sniper" ? 0.05f : 0.022f, weapon == "sniper" ? 0.15f : 0.06f);
            }
            if (anyHit) { ui.Hitmarker(false); sfx.Play(anyHead ? "headshot" : "hit"); }
            if (hits.Count > 0) net.Send("t", "hit", "w", weapon, "hits", hits.Select(kv => (object)new List<object> { kv.Key, Mathf.RoundToInt(kv.Value.dmg), kv.Value.head ? 1 : 0 }).ToList());
            if (ends.Count > 10) ends = ends.Take(10).ToList();
            net.Send("t", "shot", "w", weapon, "o", V(mz), "ends", ends);
        }
        static List<object> V(Vector3 v) => new List<object> { Math.Round(v.x, 2), Math.Round(v.y, 2), Math.Round(v.z, 2) };

        void SplashHits(Vector3 p, float damage, float splash, string label)
        {
            var hits = new List<object>();
            foreach (var zz in ents.zombies.Values)
            {
                if (zz.dying || zz.rise > 0) continue;
                float d = new Vector3(zz.x - p.x, (zz.cfg.scale - p.y) * 0.5f, zz.z - p.z).magnitude;
                if (d < splash + zz.cfg.radius) { float dmg = damage * (1 - 0.5f * Mathf.Min(1, d / splash)); hits.Add(new List<object> { zz.id, Mathf.RoundToInt(dmg), 0 }); zz.flash = 1; zz.bigDeath = T; fx.DamageNumber(new Vector3(zz.x, 2 * zz.cfg.scale, zz.z), dmg); }
            }
            if (hits.Count > 0) { net.Send("t", "hit", "w", label, "hits", hits); ui.Hitmarker(false); sfx.Play("hit"); }
        }
        void FireLauncher(Vector3 from, Vector3 dir)
        {
            var w = WStat("launcher"); var vel = dir * 42 + Vector3.up * 2;
            fx.Launch(from, vel, (a, b, pr) =>
            {
                var seg = b - a; float len = seg.magnitude; Vector3? p = null;
                if (len > 1e-4f && Physics.Raycast(a, seg / len, out var hit, len, Layers.ShootMask, QueryTriggerInteraction.Collide)) p = hit.point; else if (b.y <= 0.05f) p = new Vector3(b.x, 0.05f, b.z);
                if (!p.HasValue) return false;
                fx.Explosion(p.Value, w.splash); sfx.Play("explosion", p.Value);
                net.Send("t", "boom", "p", V(p.Value)); SplashHits(p.Value, w.damage, w.splash, "launcher"); return true;
            }, 4, 0, U.C("#33691e"), U.C("#76ff03"), 0.09f, false, 6, U.C("#bdbdbd"));
        }
        void LaunchGrenade(Vector3 from, Vector3 vel, bool mine)
        {
            float splash = D.grenadeSplash, dmg = D.grenadeDamage;
            fx.Launch(from, vel, (a, b, pr) =>
            {
                if (pr.life > 0.02f)
                {
                    var seg = b - a; float len = seg.magnitude;
                    if (len > 1e-4f && Physics.Raycast(a, seg / len, out var hit, len + 0.1f, Layers.PlaceMask ^ (1 << Layers.Ground), QueryTriggerInteraction.Ignore)) { pr.v = Vector3.Reflect(pr.v, hit.normal) * 0.45f; pr.o.position = hit.point + hit.normal * 0.1f; }
                    return false;
                }
                var p = b; p.y = Mathf.Max(0.1f, p.y);
                fx.Explosion(p, splash); sfx.Play("explosion", p);
                if (mine) { net.Send("t", "boom", "w", "grenade", "p", V(p)); SplashHits(p, dmg, splash, "grenade"); }
                return true;
            }, D.grenadeFuse, 0.45f, U.C("#37474f"), U.C("#ff5252"), 0.16f, true, 14, null);
        }
        void ThrowGrenade()
        {
            if (nades <= 0) { ui.Toast("Keine Granaten – im Shop kaufen (Ausrüstung)", "bad"); sfx.Play("deny"); return; }
            if (player.nadeT > 0 || player.reloadT > 0 || state != "alive") return;
            nades--; player.nadeT = 0.35f;
            var o = cam.transform.position + cam.transform.rotation * new Vector3(0.25f, -0.15f, 0.3f);
            var v = cam.transform.forward * 14 + Vector3.up * 4;
            LaunchGrenade(o, v, true);
            net.Send("t", "nade", "o", V(o), "v", V(v)); sfx.Play("throw");
        }
        void MeleeAttack()
        {
            if (meleeCd > 0 || state != "alive" || player.switchT > 0) return;
            meleeCd = D.meleeRate; player.meleeT = 0.3f; player.reloadT = 0; sfx.Play("swing");
            var f = cam.transform.forward; var hits = new List<object>(); bool any = false, head = false; var me = player.Pos;
            foreach (var zz in ents.zombies.Values)
            {
                if (zz.dying || zz.rise > 0) continue;
                float dx = zz.x - me.x, dz = zz.z - me.z, d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d > D.meleeRange + zz.cfg.radius) continue;
                float dot = (dx * f.x + dz * f.z) / Mathf.Max(d, 1e-3f); if (dot < 0.5f) continue;
                bool isHead = player.pitch > -0.15f && zz.cfg.scale <= 1.2f && !zz.cfg.crawl;
                float dmg = D.meleeDamage * (isHead ? 1.5f : 1);
                hits.Add(new List<object> { zz.id, Mathf.RoundToInt(dmg), isHead ? 1 : 0 }); zz.flash = 1; any = true; head |= isHead;
                var hp0 = new Vector3(zz.x, 1.3f * zz.cfg.scale, zz.z); fx.Blood(hp0, f); fx.DamageNumber(hp0, dmg, false); fx.TextNumber(hp0 + Vector3.up * 0.4f, "🔪", U.C("#ff8a65"), 0.5f);
            }
            if (any) { net.Send("t", "hit", "w", "melee", "hits", hits); ui.Hitmarker(false); sfx.Play(head ? "headshot" : "hit"); fx.shake = Mathf.Max(fx.shake, 0.15f); }
        }

        // ------------------------------------------------------------------ Snapshot / Ereignisse
        void ApplySnap(Dictionary<string, object> m)
        {
            wave = J.I(m, "wv"); phase = J.S(m, "ph"); timer = J.F(m, "tm"); left = J.I(m, "zl"); genHp = J.I(m, "gh");
            var pua = J.A(m, "pu"); for (int i = 0; i < 3; i++) pu[i] = J.F(pua, i);
            foreach (var e in J.A(m, "ev")) HandleEvent(J.Obj(e));
            var seen = new HashSet<int>();
            foreach (var ao in J.A(m, "p"))
            {
                var a = J.Arr(ao); int id = J.I(a, 0); float x = J.F(a, 1), y = J.F(a, 2), z = J.F(a, 3), yaw = J.F(a, 4), pitch = J.F(a, 5); int wi = J.I(a, 6), hp0 = J.I(a, 7), armor0 = J.I(a, 8), st = J.I(a, 9), money0 = J.I(a, 10), kills0 = J.I(a, 11), mv = J.I(a, 12); float rev0 = J.F(a, 13), downT0 = J.F(a, 14); int ready0 = J.I(a, 15), maxHp0 = J.I(a, 17, 100), tier = J.I(a, 18), melee = J.I(a, 19);
                string state0 = st == 0 ? "alive" : st == 1 ? "down" : "dead";
                if (id == myId)
                {
                    string prev = state; hp = hp0; armor = armor0; money = money0; kills = kills0; state = state0; downT = downT0; ready = ready0 == 1; rev = rev0; maxHp = maxHp0;
                    if (prev != state) OnMyState(prev, state);
                    continue;
                }
                seen.Add(id);
                var r = ents.remotes.TryGetValue(id, out var r0) ? r0 : ents.CreateRemote(id, x, y, z);
                r.tx = x; r.ty = y; r.tz = z; r.tyaw = yaw; r.pitch = pitch; r.weapon = D.WeaponAt(wi); r.hp = hp0; r.maxHp = maxHp0; r.state = state0; r.money = money0; r.kills = kills0; r.moving = mv; r.rev = rev0; r.ready = ready0 == 1; r.tier = tier; r.melee = melee;
            }
            foreach (var id in ents.remotes.Keys.ToList()) if (!seen.Contains(id)) ents.RemoveRemote(id);
            var zs = new HashSet<int>();
            foreach (var ao in J.A(m, "z"))
            {
                var a = J.Arr(ao); int id = J.I(a, 0); zs.Add(id);
                var zz = ents.zombies.TryGetValue(id, out var z0) ? z0 : ents.CreateZombie(id, D.zombies[J.I(a, 1)].id, J.F(a, 2), J.F(a, 3), J.F(a, 4), J.I(a, 7) == 1);
                if (zz.dying) continue;
                zz.tx = J.F(a, 2); zz.tz = J.F(a, 3); zz.trot = J.F(a, 4); zz.hp = J.I(a, 5);
                if (J.I(a, 7) == 0 && zz.rise > 0.3f) zz.rise = Mathf.Min(zz.rise, 0.3f);
            }
            foreach (var zz in ents.zombies.Values.ToList()) if (!zs.Contains(zz.id) && !zz.dying) ents.KillZombieLocal(zz, false, false);
            var ts = new HashSet<int>();
            foreach (var ao in J.A(m, "tu")) { var a = J.Arr(ao); int id = J.I(a, 0); ts.Add(id); var t = ents.turrets.TryGetValue(id, out var t0) ? t0 : ents.CreateTurret(id, D.turrets[J.I(a, 1)].id, J.F(a, 2), J.F(a, 3), J.F(a, 4), J.I(a, 6)); t.tyaw = J.F(a, 4); t.hp = J.I(a, 5); }
            foreach (var t in ents.turrets.Values.ToList()) if (!ts.Contains(t.id)) ents.RemoveTurret(t);
            var bs = new HashSet<int>();
            foreach (var ao in J.A(m, "ba")) { var a = J.Arr(ao); int id = J.I(a, 0); bs.Add(id); var b = ents.barricades.TryGetValue(id, out var b0) ? b0 : ents.CreateBarricade(id, J.F(a, 1), J.F(a, 2), J.I(a, 3)); b.hp = J.I(a, 4); }
            foreach (var b in ents.barricades.Values.ToList()) if (!bs.Contains(b.id)) ents.RemoveBarricade(b);
            var ps = new HashSet<int>();
            foreach (var ao in J.A(m, "pk")) { var a = J.Arr(ao); int id = J.I(a, 0); ps.Add(id); var p = ents.pickups.TryGetValue(id, out var p0) ? p0 : ents.CreatePickup(id, D.powerups[J.I(a, 1)].id, J.F(a, 2), J.F(a, 3)); p.t = J.F(a, 4); }
            foreach (var p in ents.pickups.Values.ToList()) if (!ps.Contains(p.id)) ents.RemovePickup(p);
            if (shopOpen && (money != lastShopMoney || CanShopLocal() != lastShopCan)) RefreshShop();
        }
        void OnMyState(string prev, string st)
        {
            if (st == "down") { ui.Announce("AM BODEN!", "Ein Mitspieler kann dich mit [E] wiederbeleben", 2.5f); CancelPlacing(); if (shopOpen) CloseShop(); }
            if (st == "alive" && prev != "alive") { ui.Downed(null); player.vel = Vector3.zero; }
        }
        string PlayerName(int id) { if (id == myId) return MyName; if (ents.remotes.TryGetValue(id, out var r)) return r.name; var p = lobbyPlayers.Find(q => q.id == id); return p.id == id ? p.name : "Spieler"; }
        string PlayerColor(int id) { if (id == myId) return myColor; if (ents.remotes.TryGetValue(id, out var r)) return r.color; var p = lobbyPlayers.Find(q => q.id == id); return p.id == id ? p.color : "#9e9e9e"; }

        void HandleEvent(Dictionary<string, object> e)
        {
            switch (J.S(e, "e"))
            {
                case "hurt": if (J.I(e, "id") == myId) { ui.Hurt(J.I(e, "d")); sfx.Play("hurt"); fx.shake = Mathf.Max(fx.shake, 0.2f); } break;
                case "zd":
                    {
                        ents.zombies.TryGetValue(J.I(e, "id"), out var zz); string name = zz != null ? zz.cfg.name : "Zombie"; int by = J.I(e, "by", -1), s = J.I(e, "s"), mny = J.I(e, "m");
                        if (zz != null) ents.KillZombieLocal(zz, J.I(e, "h") == 1, false);
                        if (by == myId)
                        {
                            sfx.Play("kill"); ui.Hitmarker(true);
                            if (mny > 0) fx.TextNumber(new Vector3(J.F(e, "x"), zz != null ? 2.2f * zz.cfg.scale : 2, J.F(e, "z")), "+$" + mny, U.C("#69f0ae"));
                            if (s >= 3) { string txt = s >= 15 ? "UNAUFHALTSAM!" : s >= 10 ? "MASSAKER!" : s >= 7 ? "MEGA-SERIE!" : s >= 5 ? "KILL-SERIE ×5!" : s + "er-SERIE!"; ui.Streak(txt, s >= 10 ? U.C("#ff1744") : s >= 5 ? U.C("#ff9100") : U.C("#ffd600")); if (s == 5 || s == 10 || s == 15) sfx.Play("streak"); }
                        }
                        if (by >= 0) ui.Feed($"<color={PlayerColor(by)}>{PlayerName(by)}</color> ☠ {name}{(J.I(e, "h") == 1 ? " 🎯" : "")}{(s >= 3 ? $" <color=#ffd600>×{s}</color>" : "")}");
                        break;
                    }
                case "pu":
                    {
                        var cfg = D.powerup[J.S(e, "k")]; ui.Announce($"{cfg.icon} {cfg.name}", $"{cfg.desc} ({PlayerName(J.I(e, "by", -1))})", 2.5f); sfx.Play("powerup");
                        if (cfg.id == "nuke") { ui.NukeFlash(); sfx.Play("explosion"); fx.shake = 1; int n = 0; foreach (var zz in ents.zombies.Values) if (!zz.dying) { zz.bigDeath = T; if (n++ < 10) fx.Explosion(new Vector3(zz.x, 1, zz.z), 2); } }
                        if (cfg.id == "ammo") foreach (var id in owned) InitAmmo(id);
                        break;
                    }
                case "zs": { if (ents.zombies.TryGetValue(J.I(e, "id"), out var zz)) { zz.atkT = 0.5f; zz.spitTrig = 1; fx.Spit(new Vector3(zz.x, 1.6f * zz.cfg.scale, zz.z), new Vector3(J.F(e, "x"), 0.1f, J.F(e, "z")), 0.7f); sfx.Play("spit", zz.go.transform.position); } break; }
                case "zsl": fx.AcidPool(new Vector3(J.F(e, "x"), 0, J.F(e, "z")), 2); break;
                case "nd": if (J.I(e, "by") != myId) { var o = J.A(e, "o"); var v = J.A(e, "v"); LaunchGrenade(new Vector3(J.F(o, 0), J.F(o, 1), J.F(o, 2)), new Vector3(J.F(v, 0), J.F(v, 1), J.F(v, 2)), false); } break;
                case "cs": if (world?.crate != null) { world.crateSpin = D.crateSpin; sfx.Play("crate", world.crate.root.transform.position); } break;
                case "crate": if (J.I(e, "by") != myId) { string k = J.S(e, "kind"); ui.Feed($"<color={PlayerColor(J.I(e, "by"))}>{PlayerName(J.I(e, "by"))}</color> 🎁 {(k == "weapon" ? D.weapon[J.S(e, "id")].name : k == "money" ? "Geld" : "Niete")}"); } break;
                case "za": { if (ents.zombies.TryGetValue(J.I(e, "id"), out var zz)) { zz.atkT = 0.5f; if (UnityEngine.Random.value < 0.25f) sfx.Groan(zz.go.transform.position, 1.1f); } break; }
                case "bm":
                    {
                        if (J.I(e, "by", -1) == myId) break; var p = new Vector3(J.F(e, "x"), J.F(e, "y", 0.6f), J.F(e, "z")); float r = J.F(e, "r", 4);
                        fx.Explosion(p, r); sfx.Play("explosion", p);
                        foreach (var zz in ents.zombies.Values) if (!zz.dying && Vector2.Distance(new Vector2(zz.x, zz.z), new Vector2(p.x, p.z)) < r * 0.7f) zz.bigDeath = T;
                        break;
                    }
                case "sh": if (J.I(e, "by") != myId) RemoteShot(e); break;
                case "ts": { if (ents.turrets.TryGetValue(J.I(e, "id"), out var t)) { var from = ents.TurretMuzzle(t); var to = new Vector3(J.F(e, "x"), 1.1f * J.F(e, "s", 1), J.F(e, "z")); fx.Tracer(from, to, U.C("#ffecb3"), 0.03f); fx.Muzzle(from, 0.5f, UnityEngine.Random.value < 0.3f); if (UnityEngine.Random.value < 0.5f) fx.Blood(to, null); sfx.Shoot("mg", from); } break; }
                case "tc": { if (ents.turrets.TryGetValue(J.I(e, "id"), out var t)) { var from = ents.TurretMuzzle(t); fx.Tracer(from, new Vector3(J.F(e, "x"), 0.8f, J.F(e, "z")), U.C("#ffab40"), 0.12f, 0.12f); fx.Muzzle(from, 1.4f, true); sfx.Shoot("cannon", from); } break; }
                case "tt":
                    {
                        if (!ents.turrets.TryGetValue(J.I(e, "id"), out var t)) break;
                        var pts = new List<Vector3> { ents.TurretMuzzle(t) }; foreach (var po in J.A(e, "pts")) { var p = J.Arr(po); pts.Add(new Vector3(J.F(p, 0), 1.2f * J.F(p, 2, 1), J.F(p, 1))); }
                        fx.Lightning(pts); sfx.Play("zap", pts[0]); for (int i = 1; i < pts.Count; i++) fx.Blood(pts[i], null);
                        break;
                    }
                case "td": { var p = new Vector3(J.F(e, "x"), 1, J.F(e, "z")); fx.Explosion(p, 2.5f); sfx.Play("explosion", p); if (ents.turrets.TryGetValue(J.I(e, "id"), out var t)) { if (t.owner == myId) ui.Toast("Einer deiner Türme wurde zerstört!", "bad"); ents.RemoveTurret(t); } break; }
                case "tp": case "bp": sfx.Play("place"); break;
                case "bh": { if (ents.barricades.TryGetValue(J.I(e, "id"), out var b)) { b.shakeT = 0.3f; fx.Burst(new Vector3(J.F(e, "x"), 0.8f, J.F(e, "z")), U.C("#8d6e63"), 6, 3, 0.08f, 0.6f); } sfx.Play("woodhit", new Vector3(J.F(e, "x"), 1, J.F(e, "z"))); break; }
                case "bd": { var p = new Vector3(J.F(e, "x"), 0.6f, J.F(e, "z")); fx.Gibs(p, new[] { U.C("#8d6e63"), U.C("#5d4037"), U.C("#a1887f") }, 16, 6); sfx.Play("woodbreak", p); if (ents.barricades.TryGetValue(J.I(e, "id"), out var b)) ents.RemoveBarricade(b); ui.Toast("Eine Barrikade wurde zerstört!", "bad"); break; }
                case "down": if (J.I(e, "id") != myId) sfx.Play("hurt"); break;
                case "ws":
                    {
                        string kind = J.S(e, "kind", "normal"); (string name, string sub) k = D.waveKinds.TryGetValue(kind, out var wk) ? wk : ("", "Die Zombies kommen!");
                        ui.Announce(string.IsNullOrEmpty(k.name) ? $"WELLE {J.I(e, "w")}" : $"WELLE {J.I(e, "w")} · {k.name}", StripTags(k.sub), 3.2f); sfx.Play(kind == "boss" ? "waveboss" : "wave"); UI.Show(ui.wavesum, false);
                        break;
                    }
                case "wc":
                    {
                        var sb = new System.Text.StringBuilder($"<size=26><color=#66bb6a><b>WELLE {J.I(e, "w")} GESCHAFFT!</b></color></size>\n");
                        var mvp = J.O(e, "mvp"); var kills0 = J.O(e, "kills");
                        if (mvp != null && J.I(mvp, "kills") > 0) sb.Append($"<color=#ffca28>🏆 MVP: <color={J.S(mvp, "color")}>{J.S(mvp, "name")}</color> ({J.I(mvp, "kills")} Kills{(kills0 != null && kills0.Count > 1 ? ", +$100" : "")})</color>\n");
                        if (kills0 != null) foreach (var kv in kills0.OrderByDescending(kv => J.Num(kv.Value))) { int id = int.Parse(kv.Key); sb.Append($"<color={PlayerColor(id)}>■</color> {PlayerName(id)}: {J.Num(kv.Value)} Kills{(id == myId ? " (du)" : "")}\n"); }
                        sb.Append($"<color=#b0bec5>+${J.I(e, "bonus")} Bonus für alle · Shop überall geöffnet [B]</color>");
                        ui.WaveSummary(sb.ToString()); sfx.Play("waveclear");
                        break;
                    }
            }
        }
        static string StripTags(string s) => System.Text.RegularExpressions.Regex.Replace(s ?? "", "<[^>]+>", "").Replace("&nbsp;", " ");
        void RemoteShot(Dictionary<string, object> e)
        {
            ents.remotes.TryGetValue(J.I(e, "by"), out var r); Vector3? o = null; var oa = J.A(e, "o"); if (oa.Count >= 3) o = new Vector3(J.F(oa, 0), J.F(oa, 1), J.F(oa, 2));
            if (!o.HasValue && r != null) o = new Vector3(r.x, r.y + 1.5f, r.z); if (!o.HasValue) return;
            string w = J.S(e, "w", "pistol");
            foreach (var en in J.A(e, "ends")) { var a = J.Arr(en); fx.Tracer(o.Value, new Vector3(J.F(a, 0), J.F(a, 1), J.F(a, 2)), U.C("#fff59d"), w == "sniper" ? 0.05f : 0.025f); }
            fx.Muzzle(o.Value, 0.8f, true); sfx.Shoot(w, o.Value);
        }

        // ------------------------------------------------------------------ Platzieren
        bool CanShopLocal() { if (state != "alive") return false; if (phase == "prep") return true; return mapDef != null && Vector2.Distance(new Vector2(player.Pos.x, player.Pos.z), mapDef.shop) < D.shopRadius; }
        void StartPlacing(string type)
        {
            CancelPlacing();
            bool isBarr = type == "barricade";
            var ghost = isBarr ? Models.BuildBarricade() : Models.BuildTurret(type, D.turret[type].color).root;
            var gm = U.Transparent(new Color(0.3f, 0.69f, 0.31f, 0.5f));
            foreach (var r in ghost.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = gm; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
            foreach (var c in ghost.GetComponentsInChildren<Collider>()) Destroy(c);
            placing = new Placing { type = type, ghost = ghost }; placing.mats.Add(gm);
            if (!isBarr)
            {
                float range = D.turret[type].range; var ring = new GameObject("ring").transform; ring.SetParent(ghost.transform, false);
                int n = 64; var lr = ring.gameObject.AddComponent<LineRenderer>(); lr.positionCount = n + 1; lr.loop = true; lr.useWorldSpace = false; lr.startWidth = lr.endWidth = 0.15f; var rm = U.UnlitMat(new Color(0.3f, 0.69f, 0.31f, 0.5f), true); lr.material = rm; placing.mats.Add(rm);
                for (int i = 0; i <= n; i++) { float a = i / (float)n * Mathf.PI * 2; lr.SetPosition(i, new Vector3(Mathf.Cos(a) * range, 0.06f, Mathf.Sin(a) * range)); }
                placing.ring = ring;
            }
            string name = isBarr ? D.barricadeName : D.turret[type].name; int price = isBarr ? D.barricadePrice : D.turret[type].price;
            ui.placeHint.text = $"<b>{name}</b> platzieren (${price})  ·  Linksklick setzen  ·  Q / Rechtsklick abbrechen{(isBarr ? "  ·  steht quer zu deiner Blickrichtung" : "")}";
        }
        void CancelPlacing() { if (placing == null) return; Destroy(placing.ghost); placing = null; ui.placeHint.text = ""; }
        bool SpotValid(float x, float z)
        {
            float half = mapDef.size / 2 - 1.5f; if (Mathf.Abs(x) > half || Mathf.Abs(z) > half) return false;
            if (Physics.CheckSphere(new Vector3(x, 0.6f, z), 0.7f, (1 << Layers.Solid) | (1 << Layers.Barricade), QueryTriggerInteraction.Ignore)) return false;
            foreach (var t in ents.turrets.Values) if (Vector2.Distance(new Vector2(x, z), new Vector2(t.x, t.z)) < 1.6f) return false;
            if (Vector2.Distance(new Vector2(x, z), new Vector2(player.Pos.x, player.Pos.z)) < 0.9f) return false;
            return true;
        }
        bool BarricadeSpotValid(float x, float z, int rot)
        {
            float half = mapDef.size / 2 - 1.5f; if (Mathf.Abs(x) > half || Mathf.Abs(z) > half) return false;
            var center = new Vector3(x, 0.6f, z); var q = Quaternion.Euler(0, rot == 1 ? 90 : 0, 0);
            if (Physics.CheckBox(center, new Vector3(1.3f + 0.25f, 0.55f, 0.25f + 0.25f), q, 1 << Layers.Solid, QueryTriggerInteraction.Ignore)) return false;
            if (Physics.CheckBox(center, new Vector3(1.3f - 0.35f, 0.5f, 0.25f - 0.1f), q, 1 << Layers.Barricade, QueryTriggerInteraction.Ignore)) return false;
            var p2 = new Vector2(x, z);
            if (Vector2.Distance(p2, new Vector2(player.Pos.x, player.Pos.z)) < 1.6f) return false;
            foreach (var r in ents.remotes.Values) if (r.state == "alive" && Vector2.Distance(p2, new Vector2(r.x, r.z)) < 1.6f) return false;
            if (Vector2.Distance(p2, mapDef.gen) < 3.2f || Vector2.Distance(p2, mapDef.shop) < 2.2f || (mapDef.crate.HasValue && Vector2.Distance(p2, mapDef.crate.Value) < 2.2f)) return false;
            return true;
        }
        void UpdatePlacing()
        {
            if (placing == null) return;
            bool isBarr = placing.type == "barricade"; int price = isBarr ? D.barricadePrice : D.turret[placing.type].price;
            float x, z; bool blocked = false;
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 9, Layers.PlaceMask, QueryTriggerInteraction.Ignore)) { x = hit.point.x; z = hit.point.z; blocked = hit.collider.gameObject.layer != Layers.Ground; }
            else { var p = cam.transform.position + cam.transform.forward * 5; x = p.x; z = p.z; }
            placing.x = x; placing.z = z; placing.ghost.transform.position = new Vector3(x, 0, z);
            if (isBarr) { placing.rot = Mathf.Abs(Mathf.Sin(player.yaw)) > 0.707f ? 1 : 0; placing.ghost.transform.rotation = Quaternion.Euler(0, placing.rot == 1 ? 90 : 0, 0); }
            else placing.ghost.transform.rotation = Quaternion.Euler(0, (player.yaw + Mathf.PI) * Mathf.Rad2Deg, 0);
            string reason = "";
            if (blocked || (isBarr ? !BarricadeSpotValid(x, z, placing.rot) : !SpotValid(x, z))) reason = "Kein Platz";
            else if (money < price) reason = "Zu wenig Geld";
            else if (isBarr && ents.barricades.Count >= D.barricadeMax) reason = "Barrikaden-Limit erreicht";
            else if (!isBarr && ents.turrets.Count >= D.maxTurrets) reason = "Turm-Limit erreicht";
            else if (!CanShopLocal()) reason = "Nur nahe am Shop während Wellen";
            placing.valid = reason == ""; placing.reason = reason;
            var col = placing.valid ? new Color(0.3f, 0.69f, 0.31f, 0.5f) : new Color(0.96f, 0.26f, 0.21f, 0.5f);
            foreach (var m in placing.mats) U.SetColor(m, col);
        }
        void TryPlace()
        {
            if (placing == null || placing.pending) return;
            if (!placing.valid) { ui.Toast(placing.reason, "bad"); sfx.Play("deny"); return; }
            placing.pending = true;
            net.Send("t", "place", "type", placing.type, "x", Math.Round(placing.x, 2), "z", Math.Round(placing.z, 2), "yaw", player.yaw + Mathf.PI, "rot", placing.rot);
            Invoke(nameof(UnpendPlacing), 0.8f);
        }
        void UnpendPlacing() { if (placing != null) placing.pending = false; }

        // ------------------------------------------------------------------ Shop / Kiste
        int lastShopMoney; bool lastShopCan;
        void RefreshShop()
        {
            if (!shopOpen) return; lastShopMoney = money; lastShopCan = CanShopLocal();
            ui.RenderShop(money, owned, weapon, upg, perks, nades, ents.barricades.Count, CanShopLocal(), state != "alive" ? "Du kannst gerade nichts kaufen." : "Während einer Welle kannst du nur am Shop-Terminal neben dem Generator einkaufen!");
        }
        void OpenShop() { if (!inGame || state != "alive") return; shopOpen = true; UI.Show(ui.shop, true); RefreshShop(); Lock(false); }
        public void CloseShop(bool relock = true) { shopOpen = false; UI.Show(ui.shop, false); if (relock && !paused) Lock(true); }
        void OnBought(Dictionary<string, object> m)
        {
            if (!J.B(m, "ok")) { ui.Toast(J.S(m, "reason", "Nicht möglich"), "bad"); sfx.Play("deny"); if (placing != null) placing.pending = false; return; }
            sfx.Play("buy"); string item = J.S(m, "item");
            if (D.weapon.ContainsKey(item)) { InitAmmo(item); if (!owned.Contains(item)) owned = D.weapons.Where(w => w.id == item || owned.Contains(w.id)).Select(w => w.id).ToList(); Equip(item); ui.Toast($"{D.weapon[item].name} gekauft!", "good"); }
            else if (item == "upgrade") { string w = J.S(m, "w"); upg[w] = J.I(m, "tier"); ui.Toast($"{D.weapon[w].name} auf Stufe {J.I(m, "tier")} aufgewertet!", "good"); if (w == weapon) player.BuildViewmodel(weapon, upg[w], myColor); }
            else if (D.perk.ContainsKey(item)) ui.Toast($"{D.perk[item].icon} {D.perk[item].name} aktiv!", "good");
            else if (item == "crate") { crateSpinT = D.crateSpin; if (world?.crate != null) world.crateSpin = D.crateSpin; }
            else if (D.turret.ContainsKey(item)) { CancelPlacing(); ui.Toast($"{D.turret[item].name} platziert!", "good"); }
            else if (item == "barricade") { bool again = placing != null && placing.type == "barricade"; CancelPlacing(); ui.Toast("Barrikade gebaut!", "good"); if (again && money - D.barricadePrice >= D.barricadePrice) StartPlacing("barricade"); }
            else if (item == "ammo") { foreach (var id in owned) InitAmmo(id); ui.Toast("Munition aufgefüllt!", "good"); }
            else if (item == "nades") ui.Toast("Granaten gekauft – werfen mit G", "good");
            else ui.Toast(item switch { "heal" => "Geheilt!", "armor" => "Schutzweste angelegt!", "repair" => "Generator repariert!", "fixturrets" => "Türme repariert!", "fixbarr" => "Barrikaden repariert!", _ => "Gekauft!" }, "good");
            RefreshShop();
        }
        void OnCrateResult(Dictionary<string, object> item)
        {
            crateSpinT = 0; UI.Show(ui.crateUi, false); string kind = J.S(item, "kind");
            if (kind == "weapon") { string id = J.S(item, "id"); int tier = J.I(item, "tier"); InitAmmo(id); ui.Announce("🎁 " + D.weapon[id].Upgraded(tier, null).name.ToUpper(), tier > 0 ? "Aufgewertet! Was für ein Glück." : "Aus der Glückskiste!", 3); Invoke(nameof(EquipCrateWeapon), 0.1f); crateWeapon = id; sfx.Play("waveclear"); }
            else if (kind == "money") { ui.Announce($"🎁 +${J.I(item, "amount")}", "Geld aus der Glückskiste", 2.5f); sfx.Play("buy"); }
            else { ui.Announce("🎁 NIETE", "Immerhin: volle Munition", 2.5f); foreach (var id in owned) InitAmmo(id); sfx.Play("deny"); }
        }
        string crateWeapon; void EquipCrateWeapon() { if (owned.Contains(crateWeapon)) Equip(crateWeapon); }
        bool NearCrate() => mapDef != null && mapDef.crate.HasValue && Vector2.Distance(new Vector2(player.Pos.x, player.Pos.z), mapDef.crate.Value) < 3;
        void UseCrate() { if (!NearCrate() || crateSpinT > 0 || state != "alive") return; net.Send("t", "buy", "item", "crate"); }

        // ------------------------------------------------------------------ Chat
        void OpenChat() { chatOpen = true; UI.Show(ui.chatInput.gameObject, true); ui.chatInput.text = ""; ui.chatInput.ActivateInputField(); Lock(false); }
        void CloseChat() { chatOpen = false; UI.Show(ui.chatInput.gameObject, false); ui.chatInput.DeactivateInputField(); if (inGame && !shopOpen && !paused) Lock(true); }
        void OnChatEnd(string v) { if (!chatOpen) return; var kb = Keyboard.current; if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) { v = v.Trim(); if (v.Length > 0) net.Send("t", "chat", "text", v); } CloseChat(); }

        // ------------------------------------------------------------------ Eingabe
        void HandleKeys()
        {
            var kb = Keyboard.current; var ms = Mouse.current; if (kb == null) return;
            if (chatOpen) { if (kb.escapeKey.wasPressedThisFrame) CloseChat(); return; }
            if (kb.escapeKey.wasPressedThisFrame)
            {
                if (shopOpen) CloseShop(false);
                else if (paused) Resume();
                else { paused = true; UI.Show(ui.pause, true); Lock(false); }
                return;
            }
            if (kb.bKey.wasPressedThisFrame) { if (shopOpen) CloseShop(); else if (!paused) OpenShop(); return; }
            if (shopOpen || paused) return;
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (ms != null && ms.leftButton.wasPressedThisFrame) Lock(true);
                return;
            }
            if (kb.tKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame) { OpenChat(); return; }
            if (kb.rKey.wasPressedThisFrame) StartReload();
            if (kb.nKey.wasPressedThisFrame) net.Send("t", "ready");
            if (kb.qKey.wasPressedThisFrame) CancelPlacing();
            if (kb.gKey.wasPressedThisFrame && placing == null) ThrowGrenade();
            if (kb.vKey.wasPressedThisFrame) { player.SetThirdPerson(!player.thirdPerson); PlayerPrefs.SetInt("thirdperson", player.thirdPerson ? 1 : 0); ui.Toast(player.thirdPerson ? "Third-Person-Ansicht (V wechselt zurück)" : "Egoperspektive", "good", 1.5f); }
            if ((kb.cKey.wasPressedThisFrame || (ms != null && ms.middleButton.wasPressedThisFrame)) && placing == null) MeleeAttack();
            if (kb.eKey.wasPressedThisFrame && NearCrate() && !ents.remotes.Values.Any(r => r.state == "down" && Vector2.Distance(new Vector2(r.x, r.z), new Vector2(player.Pos.x, player.Pos.z)) < 2.6f)) UseCrate();
            if (kb.fKey.wasPressedThisFrame && flashlight) { flashOn = !flashOn; flashlight.enabled = flashOn; }
            for (int d = 1; d <= 7; d++) if (kb[(Key)((int)Key.Digit1 + d - 1)].wasPressedThisFrame) { var w = D.weapons.Find(x => x.slot == d); if (w != null) Equip(w.id); }
            if (ms != null)
            {
                if (ms.leftButton.wasPressedThisFrame) mouseClicked = true; mouseDown = ms.leftButton.isPressed;
                if (ms.rightButton.wasPressedThisFrame && placing != null) CancelPlacing();
                rightDown = ms.rightButton.isPressed;
                float scroll = ms.scroll.ReadValue().y; if (Mathf.Abs(scroll) > 0.01f && placing == null) CycleWeapon(scroll < 0 ? 1 : -1);
            }
        }

        // ------------------------------------------------------------------ Lokaler Spieler
        void UpdateLocal(float dt)
        {
            bool active = ControlsActive, alive = state == "alive";
            float shake = fx.shake;
            float moveFrac = player.Update(dt, active, alive, state == "down", sens, fov, perks.Contains("sprint"), weapon == "minigun", sfx, shake);
            if (alive) UpdateWeapon(dt, active, player.sprinting); else { mouseDown = false; mouseClicked = false; }
            if (state == "dead")
            {
                var tgt = ents.remotes.Values.FirstOrDefault(r => r.state == "alive");
                if (tgt != null) { var back = new Vector3(Mathf.Sin(tgt.yaw), 0, Mathf.Cos(tgt.yaw)) * 4.5f; cam.transform.position = new Vector3(tgt.x + back.x, tgt.y + 3, tgt.z + back.z); cam.transform.LookAt(new Vector3(tgt.x, tgt.y + 1.5f, tgt.z)); }
                else { cam.transform.position = new Vector3(mapDef.gen.x + Mathf.Sin(T * 0.2f) * 14, 9, mapDef.gen.y + Mathf.Cos(T * 0.2f) * 14); cam.transform.LookAt(new Vector3(mapDef.gen.x, 1, mapDef.gen.y)); }
                ui.Downed("Du bist gefallen.\nDu kehrst zurück, sobald die Welle geschafft ist.");
            }
            else if (state == "down") ui.Downed($"{(ents.remotes.Count > 0 ? $"Ausbluten in <b>{Mathf.CeilToInt(downT)}s</b> – warte auf einen Mitspieler!" : "Selbstwiederbelebung läuft…")}{(rev > 0 ? $"\nWiederbelebung: {Mathf.RoundToInt(rev * 100)}%" : "")}");
            else ui.Downed(null);
            bool scoped = weapon == "sniper" && player.aim > 0.9f && alive;
            UI.Show(ui.scope, scoped); player.SetVmVisible(alive && !scoped && !player.thirdPerson);
            sfx.listener = cam.transform;
            // Interaktionen
            string promptTxt = null; float? prog = null;
            if (alive)
            {
                Remote near = null; float nd = 2.6f;
                foreach (var r in ents.remotes.Values) if (r.state == "down") { float d = Vector2.Distance(new Vector2(r.x, r.z), new Vector2(player.Pos.x, player.Pos.z)); if (d < nd) { nd = d; near = r; } }
                if (near != null)
                {
                    bool holding = Keyboard.current != null && Keyboard.current.eKey.isPressed && active;
                    promptTxt = $"Halte [E] um <b>{near.name}</b> wiederzubeleben";
                    if (holding) { if (T - reviveSendT > 0.1f) { net.Send("t", "revive", "id", near.id); reviveSendT = T; } prog = near.rev; }
                }
                else if (Vector2.Distance(new Vector2(player.Pos.x, player.Pos.z), mapDef.shop) < 3) promptTxt = "[B] Shop öffnen";
                else if (NearCrate()) promptTxt = crateSpinT > 0 ? "🎁 Die Kiste dreht sich …" : $"[E] Glückskiste öffnen <color=#69f0ae>${D.cratePrice}</color>{(money < D.cratePrice ? " <color=#ff5252>(zu wenig Geld)</color>" : "")}";
            }
            ui.Prompt(placing != null ? null : promptTxt); ui.Progress(prog);
            if (crateSpinT > 0)
            {
                crateSpinT -= dt; var icons = new[] { "🔫", "💨", "💥", "🎖", "🎯", "💣", "⚙", "💰", "❌" };
                UI.Show(ui.crateUi, true); ui.crateText.text = icons[(int)(T * (crateSpinT < 0.8f ? 6 : 16)) % icons.Length];
                if (crateSpinT <= 0) UI.Show(ui.crateUi, false);
            }
            meleeCd -= dt;
            if (player.meleeT > 0) player.meleeT -= dt; if (player.nadeT > 0) player.nadeT -= dt;
        }

        void UpdateWeapon(float dt, bool active, bool sprinting)
        {
            var w = WStat(); var a = ammo[weapon];
            fireCd -= dt; if (player.switchT > 0) player.switchT -= dt;
            if (player.reloadT > 0) { player.reloadT -= dt; if (player.reloadT <= 0) FinishReload(); }
            if (debugFireT > 0) { debugFireT -= dt; mouseDown = true; if (!AutoTest.Active) mouseDown = false; }
            bool firing = (active || AutoTest.Active) && mouseDown; bool clicked = (active || AutoTest.Active) && (mouseClicked || (debugFireT > 0 && fireCd <= 0)); mouseClicked = false;
            bool wantAim = active && rightDown && player.reloadT <= 0 && !sprinting && placing == null;
            player.aim += ((wantAim ? 1 : 0) - player.aim) * Mathf.Min(1, dt * 12);
            if (placing != null) { if (clicked) TryPlace(); return; }
            if (weapon == "minigun") { float prev = player.spin; player.spin = firing && player.reloadT <= 0 && player.switchT <= 0 ? Mathf.Min(1, player.spin + dt / w.spinup) : Mathf.Max(0, player.spin - dt * 1.2f); if (prev == 0 && player.spin > 0) sfx.Play("spin"); }
            bool want = w.auto ? firing : clicked;
            if (want && fireCd <= 0 && player.switchT <= 0 && player.reloadT <= 0 && player.meleeT <= 0 && player.nadeT <= 0)
            {
                if (weapon == "minigun" && player.spin < 1) { }
                else if (a.mag <= 0) { sfx.Play("empty"); fireCd = 0.3f; StartReload(); }
                else Shoot();
            }
            a = ammo[weapon];
            if (a.mag <= 0 && a.res > 0 && player.reloadT <= 0 && !firing && player.switchT <= 0) StartReload();
        }

        // ------------------------------------------------------------------ HUD
        void UpdateHud(float dt)
        {
            var w = WStat();
            float spreadPx = CurrentSpread() * Screen.height / (2 * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad));
            ui.Crosshair(Mathf.Min(80, spreadPx), state == "alive" && player.aim < 0.5f && !shopOpen);
            hudT -= dt; if (hudT > 0) return; hudT = 0.1f;
            var alivePlayers = ents.remotes.Values.Where(r => r.state == "alive").Count() + (state == "alive" ? 1 : 0);
            int readyCount = ents.remotes.Values.Count(r => r.state == "alive" && r.ready) + (state == "alive" && ready ? 1 : 0);
            ui.waveText.text = wave > 0 ? $"WELLE {wave}" : "VORBEREITUNG";
            ui.statusText.text = phase == "prep" ? $"Nächste Welle in <b>{Mathf.CeilToInt(timer)}s</b>  ·  [N] Bereit ({readyCount}/{alivePlayers})" : $"🧟 Zombies übrig: <b>{left}</b>";
            float gf = Mathf.Max(0, genHp / (float)D.generatorHp); ui.genBar.fillAmount = gf; ui.genText.text = $"⚡ GENERATOR {Mathf.RoundToInt(gf * 100)}%";
            ui.hpBar.fillAmount = Mathf.Max(0, hp / (float)maxHp); ui.hpText.text = Mathf.Max(0, hp).ToString(); ui.armorBar.fillAmount = armor / 100f; ui.armorText.text = Mathf.CeilToInt(armor).ToString();
            ui.moneyText.text = "$ " + money.ToString("N0");
            ui.lowHp.color = new Color(0.55f, 0, 0, hp < 35 && state == "alive" ? (1 - hp / 35f) * 0.5f : 0);
            ui.freezeOverlay.color = new Color(0.4f, 0.78f, 1, pu[2] > 0 ? 0.25f : 0);
            var a = ammo[weapon]; ui.wname.text = w.name; ui.wname.color = w.tier > 0 ? U.C(D.upgrade.colors[w.tier]) : UI.Accent;
            ui.ammoText.text = a.mag.ToString(); ui.ammoText.color = a.mag <= Mathf.CeilToInt(w.mag * 0.25f) ? UI.Red : Color.white; ui.reserveText.text = weapon == "pistol" ? "/ ∞" : "/ " + a.res;
            UI.Show(ui.reloadText.gameObject, player.reloadT > 0);
            ui.slotsText.text = string.Join("  ", owned.Select(id => { var wd = D.weapon[id]; int t = upg.TryGetValue(id, out var tt) ? tt : 0; string s = $"{wd.slot} {(id == weapon ? "<color=#ffca28><b>" : "")}{wd.name}{(t > 0 ? " " + D.upgrade.names[t] : "")}{(id == weapon ? "</b></color>" : "")}"; return s; }));
            ui.gearText.text = $"💣 {nades}   {string.Join(" ", perks.Select(p => D.perk[p].icon))}";
            var pus = new List<string>(); string[] puIds = { "instakill", "double", "freeze" };
            for (int i = 0; i < 3; i++) if (pu[i] > 0) { var c = D.powerup[puIds[i]]; pus.Add($"<color={c.color}><b>{c.icon} {c.name} {Mathf.CeilToInt(pu[i])}s</b></color>"); }
            ui.powerupText.text = string.Join("   ", pus);
            var boss = ents.zombies.Values.FirstOrDefault(z => z.type == "boss" && !z.dying);
            UI.Show(ui.bossBarGo, boss != null); if (boss != null) ui.bossBar.fillAmount = boss.hp / 100f;
            var sb = new System.Text.StringBuilder();
            var all = new List<(string name, string color, string st, float hp, int maxHp, int money, int kills, bool ready)> { (MyName, myColor, state, hp, maxHp, money, kills, ready) };
            foreach (var r in ents.remotes.Values) all.Add((r.name, r.color, r.state, r.hp, r.maxHp, r.money, r.kills, r.ready));
            foreach (var p in all) { int bars = p.st == "alive" ? Mathf.RoundToInt(Mathf.Clamp01(p.hp / Mathf.Max(1, p.maxHp)) * 10) : 0; sb.Append($"<color={p.color}>■</color> {p.name}{(phase == "prep" && p.ready ? " ✔" : "")} <color=#ffca28>${p.money}</color>\n<color=#66bb6a>{new string('█', bars)}</color><color=#333333>{new string('█', 10 - bars)}</color>{(p.st == "down" ? " <color=#ff5252>AM BODEN</color>" : p.st == "dead" ? " <color=#888>TOT</color>" : "")}\n"); }
            ui.teamText.text = sb.ToString();
            bool tab = Keyboard.current != null && Keyboard.current.tabKey.isPressed; UI.Show(ui.scoreboard, tab);
            if (tab) { var s2 = new System.Text.StringBuilder("<size=24><b>Punktestand</b></size>\n\n<color=#ffca28>SPIELER                 KILLS     GELD    STATUS</color>\n"); foreach (var p in all.OrderByDescending(p => p.kills)) s2.Append($"<color={p.color}>■</color> {p.name,-20} {p.kills,5}  ${p.money,7}   {(p.st == "alive" ? "aktiv" : p.st == "down" ? "am Boden" : "tot")}\n"); ui.scoreText.text = s2.ToString(); }
            ui.DrawMinimap(new Vector2(player.Pos.x, player.Pos.z), player.yaw, ents.remotes.Values, ents.zombies.Values, ents.turrets.Values, ents.pickups.Values, ents.barricades.Values);
        }

        void SendState(float dt)
        {
            sendT -= dt; if (sendT > 0) return; sendT = 0.05f;
            var p = player.Pos; int moving = new Vector2(player.vel.x, player.vel.z).magnitude > 0.6f ? 1 : 0;
            net.Send("t", "s", "p", V(p), "yaw", Math.Round(player.yaw, 3), "pitch", Math.Round(player.pitch, 3), "w", weapon, "mv", moving, "f", Time.time - player.lastFire < 0.15f ? 1 : 0, "ml", player.meleeT > 0 ? 1 : 0);
        }

        void OnApplicationQuit() { net?.Close(); }
    }
}
