// Oberfläche (uGUI, komplett per Code): Lobby, HUD, Shop, Pause, Game Over
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ZA
{
    public class UI
    {
        public Canvas canvas; Transform root;
        static GameData D => GameData.I;
        // Düstere, entsättigte Palette (Messing, getrocknetes Blut, Olivgrün, Stahl, Knochenweiß) statt der Comic-Farben
        public static readonly Color Accent = U.C("#c8963e"), Red = U.C("#b4332a"), Green = U.C("#7d8f4f"), Blue = U.C("#6a8ba3"), Panel = new Color(0.07f, 0.065f, 0.06f, 0.94f), Txt = U.C("#e6dfd0"), Grey = U.C("#9a948a"), Bone = U.C("#e6dfd0");
        static readonly Dictionary<string, string> WeaponIcon = new Dictionary<string, string> { ["pistol"] = "🔫", ["smg"] = "💨", ["shotgun"] = "💥", ["rifle"] = "🎖", ["sniper"] = "🎯", ["launcher"] = "💣", ["minigun"] = "⚙" };

        // Lobby
        public GameObject lobby; public InputField serverField, nameField; public Text connText, playerList, shareText, startBtnText; public Button startBtn;
        public Transform colorRow, mapRow, diffRow; public Text sensText, volText, fovText, qualText, autoText;
        // HUD
        public GameObject hud, pause, over, shop, scope, downedPanel, scoreboard, wavesum, crateUi;
        public Text waveText, statusText, genText, moneyText, hpText, armorText, wname, ammoText, reserveText, reloadText, slotsText, gearText, teamText, feedText, announceBig, announceSmall, promptText, placeHint, downedText, scoreText, powerupText, streakText, wavesumText, toastText, crateText, overReason, overWave, overRecord, overStats, chatLog, pauseInfo;
        public Image genBar, bossBar, hpBar, armorBar, progressBar, hurtFlash, lowHp, nukeFlash, freezeOverlay, hitmarker; public GameObject bossBarGo, progressGo, crosshair; public RectTransform[] crossLines;
        public RawImage minimap; Texture2D mmTex, mmBase; Color32[] mmPx; MapDef mmMap;
        // Shop
        public Text shopMoney, shopWarn; public Transform shopGrid; public string shopTab = "weapons"; public Transform tabRow;
        public InputField chatInput;
        float annT, streakT, wsT, toastT;
        public Action<string> onBuy, onEquip, onTurret, onUpgrade, onTab;

        public UI()
        {
            var cgo = new GameObject("Canvas"); canvas = cgo.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
            var sc = cgo.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1600, 900); sc.matchWidthOrHeight = 0.5f;
            cgo.AddComponent<GraphicRaycaster>();
            root = cgo.transform;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null) { var es = new GameObject("EventSystem"); es.AddComponent<EventSystem>(); es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>(); }
            BuildHud(); BuildShop(); BuildPause(); BuildOver(); BuildLobby();
            Show(hud, false); Show(shop, false); Show(pause, false); Show(over, false);
        }

        // ------------------------------------------------------------------ Bausteine
        public static RectTransform Rect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax)
        {
            var go = new GameObject(name, typeof(RectTransform)); var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offMin; rt.offsetMax = offMax; return rt;
        }
        public static Image Img(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool raycast = false)
        { var rt = Rect(parent, name, aMin, aMax, oMin, oMax); var i = rt.gameObject.AddComponent<Image>(); i.color = c; i.raycastTarget = raycast; return i; }
        public static Text Txt2(Transform parent, string name, string text, int size, Color c, TextAnchor anchor, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(parent, name, aMin, aMax, oMin, oMax); var t = rt.gameObject.AddComponent<Text>(); t.font = U.Font; t.fontSize = size; t.color = c; t.alignment = anchor; t.text = text; t.fontStyle = U.FontIsOs && style == FontStyle.Bold ? FontStyle.Normal : style; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.9f); sh.effectDistance = new Vector2(1.5f, -1.5f);
            return t;
        }
        /// <summary>Überschriften-Schrift (Schablonenstil) mit dunkler Kontur</summary>
        public static Text Head(Text t)
        {
            t.font = U.HeadFont; t.fontStyle = FontStyle.Normal; t.fontSize = Mathf.RoundToInt(t.fontSize * (U.HeadFont == U.Font ? 1f : 1.18f));
            if (!t.GetComponent<Outline>()) { var ol = t.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.85f); ol.effectDistance = new Vector2(1.5f, -1.5f); }
            return t;
        }
        public static Button Btn(Transform parent, string label, Color bg, Action onClick, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int size = 18, Color? fg = null)
        {
            var img = Img(parent, "btn", bg, aMin, aMax, oMin, oMax, true);
            var o = img.gameObject.AddComponent<Outline>(); o.effectColor = Color.black; o.effectDistance = new Vector2(2, -2);
            var b = img.gameObject.AddComponent<Button>(); b.targetGraphic = img; var cb = b.colors; cb.highlightedColor = new Color(1.1f, 1.1f, 1.1f); cb.pressedColor = new Color(0.8f, 0.8f, 0.8f); cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.8f); b.colors = cb;
            b.onClick.AddListener(() => onClick?.Invoke());
            Txt2(img.transform, "label", label, size, fg ?? Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -2));
            return b;
        }
        static InputField Field(Transform parent, string placeholder, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int size = 20)
        {
            var img = Img(parent, "field", U.C("#d9d2c3"), aMin, aMax, oMin, oMax, true);
            var f = img.gameObject.AddComponent<InputField>(); f.targetGraphic = img;
            var t = Txt2(img.transform, "text", "", size, Color.black, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(10, 2), new Vector2(-10, -2), FontStyle.Normal); t.supportRichText = false;
            var p = Txt2(img.transform, "ph", placeholder, size, new Color(0, 0, 0, 0.4f), TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(10, 2), new Vector2(-10, -2), FontStyle.Italic);
            f.textComponent = t; f.placeholder = p; return f;
        }
        public static void Show(GameObject g, bool on = true) { if (g && g.activeSelf != on) g.SetActive(on); }
        static Image Bar(Transform parent, Color fill, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, out Image fillImg)
        {
            var bg = Img(parent, "bar", new Color(0, 0, 0, 0.6f), aMin, aMax, oMin, oMax); bg.gameObject.AddComponent<Outline>().effectColor = Color.black;
            fillImg = Img(bg.transform, "fill", fill, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); fillImg.sprite = WhiteSprite; fillImg.type = Image.Type.Filled; fillImg.fillMethod = Image.FillMethod.Horizontal; fillImg.fillOrigin = 0; fillImg.fillAmount = 1; // ohne Sprite ignoriert uGUI fillAmount
            return bg;
        }
        static Sprite whiteSprite;
        static Sprite WhiteSprite => whiteSprite ??= Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), new Vector2(0.5f, 0.5f));
        static void Label(Transform parent, string text, float y) => Txt2(parent, "lbl", text.ToUpper(), 13, Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, y - 20), new Vector2(0, y));

        // ------------------------------------------------------------------ Lobby
        static Sprite roundedSprite, roundedSmall;
        /// <summary>9-Slice-Sprite mit runden Ecken (für Panels und Buttons)</summary>
        public static Sprite Rounded(bool small = false)
        {
            if (small ? roundedSmall : roundedSprite) return small ? roundedSmall : roundedSprite;
            int S = 64, r = small ? 12 : 22; var tex = new Texture2D(S, S, TextureFormat.RGBA32, false); var px = new Color[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float dx = Mathf.Max(0, Mathf.Max(r - x, x - (S - 1 - r))), dy = Mathf.Max(0, Mathf.Max(r - y, y - (S - 1 - r)));
                float d = Mathf.Sqrt(dx * dx + dy * dy); float a = Mathf.Clamp01(r + 0.5f - d);
                px[y * S + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels(px); tex.Apply(); tex.filterMode = FilterMode.Bilinear;
            var sp = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            if (small) roundedSmall = sp; else roundedSprite = sp; return sp;
        }
        static Image RImg(Transform parent, string name, Color c, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, bool small = false, bool raycast = false)
        { var i = Img(parent, name, c, aMin, aMax, oMin, oMax, raycast); i.sprite = Rounded(small); i.type = Image.Type.Sliced; return i; }
        public static Button RBtn(Transform parent, string label, Color bg, Action onClick, Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax, int size = 18, Color? fg = null)
        {
            var b = Btn(parent, label, bg, onClick, aMin, aMax, oMin, oMax, size, fg);
            var img = b.GetComponent<Image>(); img.sprite = Rounded(true); img.type = Image.Type.Sliced; UnityEngine.Object.Destroy(b.GetComponent<Outline>());
            var sh = b.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.6f); sh.effectDistance = new Vector2(0, -4);
            return b;
        }
        public CanvasGroup lobbyGroup; public Image lobbyDim; float lobbyT;
        void BuildLobby()
        {
            lobby = Rect(root, "Lobby", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            lobbyGroup = lobby.AddComponent<CanvasGroup>();
            // dunkler Verlauf über der 3D-Szene (links kräftiger, damit Text lesbar bleibt)
            lobbyDim = Img(lobby.transform, "dim", new Color(0.02f, 0.02f, 0.02f, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
            Img(lobby.transform, "dim2", new Color(0.02f, 0.02f, 0.02f, 0.35f), new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
            // Logo-Bereich oben
            var logoBg = RImg(lobby.transform, "logobg", new Color(0, 0, 0, 0.35f), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-470, -150), new Vector2(470, -26));
            var logo = Txt2(logoBg.transform, "logo", "<color=#d8d0bf>ZOMBIE</color> <color=#a8362b>ANSTURM</color>", 76, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 26), new Vector2(0, -4));
            Head(logo);
            var lsh = logo.GetComponent<Shadow>(); lsh.effectDistance = new Vector2(4, -5); lsh.effectColor = new Color(0, 0, 0, 1);
            var ol = logo.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0, 0, 0, 0.9f); ol.effectDistance = new Vector2(2, -2);
            Txt2(logoBg.transform, "tag", "KOOP-ZOMBIE-VERTEIDIGUNG  ·  BIS ZU 8 SPIELER  ·  BROWSER & UNITY ZUSAMMEN", 13, U.C("#b8b1a4"), TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 8), new Vector2(0, 30));
            // linke Karte: Spieler & Verbindung
            var L = RImg(lobby.transform, "left", new Color(0.075f, 0.07f, 0.065f, 0.88f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560, -300), new Vector2(-180, 120), false, true);
            var Lt = L.transform;
            SectionTitle(Lt, "👤  DU", -14);
            connText = Txt2(Lt, "conn", "Nicht verbunden", 13, Color.white, TextAnchor.MiddleRight, new Vector2(0.4f, 1), new Vector2(1, 1), new Vector2(0, -40), new Vector2(-16, -14));
            Label(Lt, "Server-Adresse", -48); serverField = Field(Lt, "localhost:3000", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -104), new Vector2(-16, -70), 18); RoundField(serverField);
            Label(Lt, "Dein Name", -112); nameField = Field(Lt, "Spieler", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -168), new Vector2(-16, -134), 18); RoundField(nameField);
            Label(Lt, "Farbe", -176); colorRow = Rect(Lt, "colors", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -240), new Vector2(-16, -198));
            SectionTitle(Lt, "👥  IN DER LOBBY", -252);
            playerList = Txt2(Lt, "plist", "", 15, Color.white, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(16, 100), new Vector2(-16, -282), FontStyle.Normal);
            var shareBg = RImg(Lt, "sharebg", new Color(0, 0, 0, 0.35f), new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 96), true);
            shareText = Txt2(shareBg.transform, "share", "", 11, U.C("#9a948a"), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(10, 6), new Vector2(-10, -6), FontStyle.Normal);
            // rechte Karte: Spiel
            var R = RImg(lobby.transform, "right", new Color(0.075f, 0.07f, 0.065f, 0.88f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-160, -300), new Vector2(560, 120), false, true);
            var Rt = R.transform;
            SectionTitle(Rt, "🗺  KARTE", -14);
            mapRow = Rect(Rt, "maps", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -192), new Vector2(-16, -44));
            SectionTitle(Rt, "⚔  SCHWIERIGKEIT", -202);
            diffRow = Rect(Rt, "diffs", new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -290), new Vector2(-16, -232));
            startBtn = RBtn(Rt, "▶  SPIEL STARTEN", U.C("#5a6b2e"), null, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 24), new Vector2(-16, 90), 26); startBtnText = startBtn.GetComponentInChildren<Text>();
            var so = startBtn.gameObject.AddComponent<Outline>(); so.effectColor = new Color(0.12f, 0.14f, 0.06f, 1); so.effectDistance = new Vector2(2, -2);
            // Einstellungen unten
            var S = RImg(lobby.transform, "settings", new Color(0.075f, 0.07f, 0.065f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-560, -430), new Vector2(560, -312), false, true);
            var St = S.transform;
            SectionTitle(St, "⚙  EINSTELLUNGEN", -10);
            sensText = SettingRow(St, "Maus", 0, -42, () => Game.I.AdjustSetting("sens", -1), () => Game.I.AdjustSetting("sens", 1));
            volText = SettingRow(St, "Lautstärke", 1, -42, () => Game.I.AdjustSetting("vol", -1), () => Game.I.AdjustSetting("vol", 1));
            fovText = SettingRow(St, "Sichtfeld", 2, -42, () => Game.I.AdjustSetting("fov", -1), () => Game.I.AdjustSetting("fov", 1));
            qualText = SettingRow(St, "Grafik", 3, -42, () => Game.I.AdjustSetting("quality", -1), () => Game.I.AdjustSetting("quality", 1));
            Txt2(St, "keys", "WASD Laufen · Maus Umsehen · LMB Schießen · RMB Zielen · Leertaste Springen · Shift Sprint · R Nachladen · 1-7/Mausrad Waffe · B Shop · E Wiederbeleben/Kiste · G Granate · C Messer · V Ego-/Third-Person · N Bereit · T Chat · Tab Punkte · F Lampe · Q Abbrechen · Esc Pause", 11, U.C("#90a4ae"), TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 6), new Vector2(-16, 30), FontStyle.Normal);
        }
        static void SectionTitle(Transform p, string text, float y) => Txt2(p, "sec", text, 15, Accent, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, y - 22), new Vector2(-16, y));
        static void RoundField(InputField f) { var img = f.GetComponent<Image>(); img.sprite = Rounded(true); img.type = Image.Type.Sliced; img.color = new Color(0.85f, 0.82f, 0.76f, 1); }
        Text SettingRow(Transform p, string label, int col, float y, Action minus, Action plus)
        {
            float w = 0.25f, x0 = col * w;
            Txt2(p, "l", label, 13, Color.white, TextAnchor.MiddleLeft, new Vector2(x0, 1), new Vector2(x0 + w, 1), new Vector2(16, y - 28), new Vector2(-16, y), FontStyle.Normal);
            RBtn(p, "–", U.C("#2e2b28"), minus, new Vector2(x0 + w * 0.45f, 1), new Vector2(x0 + w * 0.45f, 1), new Vector2(0, y - 28), new Vector2(30, y), 16);
            var v = Txt2(p, "v", "", 14, Accent, TextAnchor.MiddleCenter, new Vector2(x0 + w * 0.45f, 1), new Vector2(x0 + w, 1), new Vector2(32, y - 28), new Vector2(-48, y));
            RBtn(p, "+", U.C("#2e2b28"), plus, new Vector2(x0 + w, 1), new Vector2(x0 + w, 1), new Vector2(-46, y - 28), new Vector2(-16, y), 16);
            return v;
        }
        static readonly Dictionary<string, (string icon, string color)> MapStyle = new Dictionary<string, (string, string)> { ["suburb"] = ("🏘", "#8fd3ff"), ["farm"] = ("🌽", "#ffb074"), ["base"] = ("🪖", "#a7b4c2"), ["graveyard"] = ("🪦", "#5c6bc0"), ["ruins"] = ("🏚", "#78909c") };
        public void BuildLobbyChoices(Action<string> onColor, Action<string> onMap, Action<string> onDiff)
        {
            foreach (Transform c in colorRow) UnityEngine.Object.Destroy(c.gameObject);
            for (int i = 0; i < D.playerColors.Count; i++)
            {
                string col = D.playerColors[i];
                var b = RBtn(colorRow, "", U.C(col), () => onColor(col), new Vector2(0, 0), new Vector2(0, 1), new Vector2(i * 42, 2), new Vector2(i * 42 + 36, -2), 16);
                b.name = "col_" + i; Txt2(b.transform, "chk", "✔", 18, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            foreach (Transform c in mapRow) UnityEngine.Object.Destroy(c.gameObject);
            for (int i = 0; i < D.maps.Count; i++)
            {
                int cols = D.maps.Count > 4 ? 3 : 2; var m = D.maps[i]; int col = i % cols, row = i / cols; float cw = 1f / cols; (string icon, string color) st = MapStyle.TryGetValue(m.id, out var ms) ? ms : ("🗺", "#90a4ae");
                var b = RBtn(mapRow, "", new Color(0.16f, 0.15f, 0.14f, 1), () => onMap(m.id), new Vector2(col * cw, 1), new Vector2(col * cw + cw, 1), new Vector2(col == 0 ? 0 : 4, -row * 74 - 68), new Vector2(col == cols - 1 ? 0 : -4, -row * 74), 14);
                b.name = "map_" + m.id;
                var stripe = RImg(b.transform, "stripe", U.C(st.color), new Vector2(0, 0), new Vector2(0, 1), new Vector2(0, 0), new Vector2(54, 0), true);
                Txt2(stripe.transform, "ico", st.icon, 26, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Txt2(b.transform, "n", m.name, 17, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(64, 4), new Vector2(-8, -6));
                Txt2(b.transform, "d", m.desc, 11, U.C("#b8b1a4"), TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(64, 18), new Vector2(-8, -28), FontStyle.Normal);
                Txt2(b.transform, "rec", "", 11, Accent, TextAnchor.LowerLeft, Vector2.zero, Vector2.one, new Vector2(64, 5), new Vector2(-8, -40));
            }
            foreach (Transform c in diffRow) UnityEngine.Object.Destroy(c.gameObject);
            string[] dIcons = { "🙂", "😐", "💀" };
            for (int i = 0; i < D.diffs.Count; i++)
            {
                var d = D.diffs[i]; float w = 1f / D.diffs.Count;
                var b = RBtn(diffRow, "", U.C("#2e2b28"), () => onDiff(d.id), new Vector2(i * w, 0), new Vector2(i * w + w, 1), new Vector2(i == 0 ? 0 : 5, 0), new Vector2(i == D.diffs.Count - 1 ? 0 : -5, 0), 14);
                b.name = "diff_" + d.id;
                Txt2(b.transform, "n", $"{(i < dIcons.Length ? dIcons[i] + " " : "")}{d.name}", 16, Color.white, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-4, -6));
                Txt2(b.transform, "d", d.desc, 10, U.C("#b8b1a4"), TextAnchor.LowerCenter, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, -26), FontStyle.Normal);
            }
        }
        public void RenderLobby(string mapId, string diffId, string myColor, List<(int id, string name, string color, bool inGame)> players, int myId, List<string> urls, bool gameRunning, Func<string, string, int> best)
        {
            foreach (Transform c in colorRow) { int i = int.Parse(c.name.Substring(4)); bool on = D.playerColors[i] == myColor; c.Find("chk").gameObject.SetActive(on); c.localScale = Vector3.one * (on ? 1.12f : 1f); }
            foreach (Transform c in mapRow) { bool on = c.name == "map_" + mapId; c.GetComponent<Image>().color = on ? new Color(0.36f, 0.3f, 0.2f, 1) : new Color(0.16f, 0.15f, 0.14f, 1); var o = c.GetComponent<Outline>() ?? c.gameObject.AddComponent<Outline>(); o.effectColor = on ? Accent : new Color(0, 0, 0, 0); o.effectDistance = new Vector2(3, -3); c.GetComponent<Button>().interactable = !gameRunning; var id = c.name.Substring(4); int b = best(id, diffId); c.Find("rec").GetComponent<Text>().text = b > 0 ? "🏆 Rekord: Welle " + b : "Noch kein Rekord"; }
            foreach (Transform c in diffRow) { bool on = c.name == "diff_" + diffId; c.GetComponent<Image>().color = on ? Accent : U.C("#2e2b28"); c.Find("n").GetComponent<Text>().color = on ? Color.black : Color.white; c.Find("d").GetComponent<Text>().color = on ? new Color(0.2f, 0.2f, 0.2f) : U.C("#b8b1a4"); c.GetComponent<Button>().interactable = !gameRunning; }
            var sb = new System.Text.StringBuilder();
            foreach (var p in players) sb.Append($"<color={p.color}>●</color> {p.name}{(p.id == myId ? " <color=#90a4ae>(du)</color>" : "")}  <color=#90a4ae>{(p.inGame ? "· im Spiel" : "· Lobby")}</color>\n");
            if (players.Count == 0) sb.Append("<color=#90a4ae>Noch niemand da …</color>");
            playerList.text = sb.ToString();
            string mapName = D.map.TryGetValue(mapId, out var md) ? md.name : mapId; string diffName = D.diffs.Find(d => d.id == diffId)?.name ?? diffId;
            startBtnText.text = gameRunning ? "▶  LAUFENDEM SPIEL BEITRETEN" : $"▶  SPIEL STARTEN  ·  {mapName} · {diffName}";
            startBtn.interactable = true;
            if (urls != null && urls.Count > 0) shareText.text = "👥 Freunde einladen – im selben WLAN diese Adresse öffnen:\n<color=#ffffff>" + string.Join("   ", urls) + "</color>\nBrowser (PC/Handy) und Unity-App spielen zusammen.";
        }
        public void TickLobby(float dt)
        {
            if (!lobby.activeSelf) return;
            lobbyT += dt; lobbyGroup.alpha = Mathf.Min(1, lobbyT * 1.5f);
            if (startBtn.interactable) startBtn.transform.localScale = Vector3.one * (1 + Mathf.Sin(lobbyT * 3) * 0.015f);
        }
        public void ResetLobbyAnim() { lobbyT = 0; }
        public void SetSettingsText(float sens, float vol, float fov, string quality) { sensText.text = sens.ToString("0.00"); volText.text = Mathf.RoundToInt(vol * 100) + "%"; fovText.text = fov.ToString("0") + "°"; qualText.text = quality == "low" ? "Niedrig" : quality == "high" ? "Hoch" : "Mittel"; }

        // ------------------------------------------------------------------ HUD
        void BuildHud()
        {
            hud = Rect(root, "HUD", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            var H = hud.transform;
            hurtFlash = Img(H, "hurt", new Color(1, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            lowHp = Img(H, "lowhp", new Color(0.55f, 0, 0, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            freezeOverlay = Img(H, "freeze", new Color(0.4f, 0.78f, 1, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            nukeFlash = Img(H, "nuke", new Color(1, 1, 1, 0), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            // Zielfernrohr
            scope = Rect(H, "scope", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Img(scope.transform, "l", Color.black, new Vector2(0, 0), new Vector2(0.5f, 1), Vector2.zero, new Vector2(-300, 0));
            Img(scope.transform, "r", Color.black, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(300, 0), Vector2.zero);
            Img(scope.transform, "t", Color.black, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(0, 300), Vector2.zero);
            Img(scope.transform, "b", Color.black, new Vector2(0, 0), new Vector2(1, 0.5f), Vector2.zero, new Vector2(0, -300));
            Img(scope.transform, "h", Color.black, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-300, -1), new Vector2(300, 1));
            Img(scope.transform, "v", Color.black, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-1, -300), new Vector2(1, 300));
            Show(scope, false);
            // Fadenkreuz
            crosshair = Rect(H, "crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero).gameObject;
            crossLines = new RectTransform[4];
            for (int i = 0; i < 4; i++) { var im = Img(crosshair.transform, "l" + i, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero); im.gameObject.AddComponent<Outline>().effectColor = Color.black; crossLines[i] = im.rectTransform; }
            Img(crosshair.transform, "dot", Color.white, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-2, -2), new Vector2(2, 2));
            hitmarker = Img(H, "hitmarker", new Color(1, 1, 1, 0), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-16, -16), new Vector2(16, 16)); hitmarker.rectTransform.localEulerAngles = new Vector3(0, 0, 45);
            var hmt = Txt2(hitmarker.transform, "x", "✕", 30, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); hitmarker.color = new Color(0, 0, 0, 0);
            // Oben
            waveText = Head(Txt2(H, "wave", "VORBEREITUNG", 36, Bone, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-240, -54), new Vector2(240, -8)));
            statusText = Txt2(H, "status", "", 16, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-240, -80), new Vector2(240, -54));
            Bar(H, U.C("#6e7a3c"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-230, -100), new Vector2(230, -84), out genBar);
            genText = Txt2(genBar.transform.parent, "gt", "⚡ GENERATOR 100%", 11, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bossBarGo = Bar(H, U.C("#7a1f1f"), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-230, -126), new Vector2(230, -106), out bossBar).gameObject;
            Txt2(bossBarGo.transform, "bt", "☠ KOLOSS", 13, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); Show(bossBarGo, false);
            powerupText = Txt2(H, "pu", "", 14, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-300, -158), new Vector2(300, -128));
            // Team links oben
            teamText = Txt2(H, "team", "", 13, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -260), new Vector2(240, -12));
            // Minikarte rechts oben
            var mmBg = Img(H, "mm", Color.black, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-186, -186), new Vector2(-12, -12)); mmBg.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            var mmRt = Rect(mmBg.transform, "img", Vector2.zero, Vector2.one, new Vector2(2, 2), new Vector2(-2, -2)); minimap = mmRt.gameObject.AddComponent<RawImage>(); minimap.raycastTarget = false;
            feedText = Txt2(H, "feed", "", 13, Color.white, TextAnchor.UpperRight, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-330, -360), new Vector2(-12, -196));
            // Mitte
            announceBig = Head(Txt2(H, "annB", "", 64, Bone, TextAnchor.MiddleCenter, new Vector2(0, 0.62f), new Vector2(1, 0.62f), new Vector2(0, -40), new Vector2(0, 50)));
            announceSmall = Txt2(H, "annS", "", 22, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 0.62f), new Vector2(1, 0.62f), new Vector2(0, -80), new Vector2(0, -40));
            streakText = Head(Txt2(H, "streak", "", 40, Accent, TextAnchor.MiddleCenter, new Vector2(0, 0.64f), new Vector2(1, 0.64f), new Vector2(0, 50), new Vector2(0, 110)));
            promptText = Txt2(H, "prompt", "", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(-300, -18), new Vector2(300, 18));
            progressGo = Bar(H, Green, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.38f), new Vector2(-120, -46), new Vector2(120, -34), out progressBar).gameObject; Show(progressGo, false);
            placeHint = Txt2(H, "place", "", 16, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.22f), new Vector2(-400, -20), new Vector2(400, 20));
            crateUi = Rect(H, "crate", new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(-60, -40), new Vector2(60, 40)).gameObject;
            var cbg = Img(crateUi.transform, "bg", new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); cbg.gameObject.AddComponent<Outline>().effectColor = Accent; cbg.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            crateText = Txt2(crateUi.transform, "t", "🔫", 50, Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); Show(crateUi, false);
            // Unten links
            moneyText = Head(Txt2(H, "money", "$ 0", 34, U.C("#c9b26a"), TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 72), new Vector2(300, 116)));
            Txt2(H, "hpi", "❤", 16, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 42), new Vector2(40, 64));
            Bar(H, U.C("#9e2b22"), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 44), new Vector2(240, 62), out hpBar); hpText = Txt2(H, "hpt", "100", 16, Color.white, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(0, 0), new Vector2(244, 42), new Vector2(290, 64));
            Txt2(H, "ari", "🛡", 16, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 16), new Vector2(40, 38));
            Bar(H, U.C("#5f7c93"), new Vector2(0, 0), new Vector2(0, 0), new Vector2(40, 18), new Vector2(240, 36), out armorBar); armorBar.fillAmount = 0; armorText = Txt2(H, "art", "0", 16, Color.white, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(0, 0), new Vector2(244, 16), new Vector2(290, 38));
            chatLog = Txt2(H, "chat", "", 14, Color.white, TextAnchor.LowerLeft, new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 126), new Vector2(420, 300), FontStyle.Normal);
            chatInput = Field(H, "Nachricht… (Enter senden, Esc abbrechen)", new Vector2(0, 0), new Vector2(0, 0), new Vector2(14, 300), new Vector2(420, 330), 15); Show(chatInput.gameObject, false);
            // Unten rechts
            slotsText = Txt2(H, "slots", "", 13, U.C("#b8b1a4"), TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 118), new Vector2(-14, 140));
            wname = Txt2(H, "wname", "Pistole", 18, Accent, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 94), new Vector2(-14, 118));
            ammoText = Head(Txt2(H, "ammo", "12", 50, Bone, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 38), new Vector2(-70, 94)));
            reserveText = Txt2(H, "res", "/ ∞", 22, U.C("#b8b1a4"), TextAnchor.LowerLeft, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-66, 44), new Vector2(-6, 80));
            reloadText = Txt2(H, "rel", "NACHLADEN…", 16, Accent, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 16), new Vector2(-150, 38));
            gearText = Txt2(H, "gear", "💣 0", 15, Color.white, TextAnchor.LowerRight, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-400, 16), new Vector2(-14, 38));
            // Am Boden
            downedPanel = Img(H, "downed", new Color(0.3f, 0, 0, 0.35f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
            Head(Txt2(downedPanel.transform, "b", "DU BIST AM BODEN", 44, Red, TextAnchor.MiddleCenter, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, 0), new Vector2(0, 70)));
            downedText = Txt2(downedPanel.transform, "s", "", 20, Color.white, TextAnchor.UpperCenter, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0, -80), new Vector2(0, 0)); Show(downedPanel, false);
            // Punktestand
            scoreboard = Img(H, "score", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, -160), new Vector2(260, 160)).gameObject; scoreboard.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            scoreText = Txt2(scoreboard.transform, "t", "", 16, Color.white, TextAnchor.UpperLeft, Vector2.zero, Vector2.one, new Vector2(20, 14), new Vector2(-20, -14), FontStyle.Normal); Show(scoreboard, false);
            // Wellen-Zusammenfassung
            wavesum = Img(H, "wavesum", Panel, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.78f), new Vector2(-200, -110), new Vector2(200, 60)).gameObject; wavesum.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            wavesumText = Txt2(wavesum.transform, "t", "", 15, Color.white, TextAnchor.UpperCenter, Vector2.zero, Vector2.one, new Vector2(16, 10), new Vector2(-16, -10), FontStyle.Normal); Show(wavesum, false);
            toastText = Txt2(H, "toast", "", 17, Color.white, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), new Vector2(-400, -30), new Vector2(400, 30));
        }

        // ------------------------------------------------------------------ Shop
        void BuildShop()
        {
            shop = Img(root, "Shop", new Color(0, 0, 0, 0.55f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true).gameObject;
            var panel = Img(shop.transform, "panel", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-520, -380), new Vector2(520, 380), true); panel.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            var P = panel.transform;
            Txt2(P, "h", "🛒 SHOP", 32, Color.white, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0.5f, 1), new Vector2(24, -60), new Vector2(0, -14));
            shopMoney = Txt2(P, "m", "$ 0", 28, U.C("#c9b26a"), TextAnchor.MiddleRight, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(-80, -14));
            Btn(P, "✕", U.C("#7a1f1f"), () => Game.I.CloseShop(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-66, -60), new Vector2(-20, -16), 22);
            var warnBg = Img(P, "warnbg", U.C("#8a4a12"), new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -92), new Vector2(-24, -66));
            shopWarn = Txt2(warnBg.transform, "warn", "", 14, Color.white, TextAnchor.MiddleLeft, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-8, 0));
            tabRow = Rect(P, "tabs", new Vector2(0, 1), new Vector2(1, 1), new Vector2(24, -140), new Vector2(-24, -98));
            var tabs = new (string id, string label)[] { ("weapons", "🔫 Waffen"), ("upgrades", "⬆ Aufwerten"), ("perks", "⭐ Perks"), ("turrets", "🗼 Türme & Barrikaden"), ("items", "🎒 Ausrüstung") };
            for (int i = 0; i < tabs.Length; i++) { var tb = tabs[i]; float w = 1f / tabs.Length; var b = Btn(tabRow, tb.label, U.C("#2e2b28"), () => { shopTab = tb.id; onTab?.Invoke(tb.id); }, new Vector2(i * w, 0), new Vector2(i * w + w, 1), new Vector2(i == 0 ? 0 : 4, 0), new Vector2(i == tabs.Length - 1 ? 0 : -4, 0), 15); b.name = "tab_" + tb.id; }
            // Scrollbereich
            var sv = Rect(P, "scroll", Vector2.zero, Vector2.one, new Vector2(24, 20), new Vector2(-24, -148));
            var scroll = sv.gameObject.AddComponent<ScrollRect>(); sv.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); sv.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var content = Rect(sv, "content", new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(232, 236); grid.spacing = new Vector2(12, 12); grid.padding = new RectOffset(2, 2, 4, 8); grid.childAlignment = TextAnchor.UpperLeft;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30;
            shopGrid = content;
        }
        public void RenderShop(int money, List<string> owned, string weapon, Dictionary<string, int> upg, List<string> perks, int nades, int barricades, bool canShop, string warn)
        {
            shopMoney.text = "$ " + money.ToString("N0");
            foreach (Transform t in tabRow) { bool on = t.name == "tab_" + shopTab; t.GetComponent<Image>().color = on ? Accent : U.C("#2e2b28"); t.GetComponentInChildren<Text>().color = on ? Color.black : Color.white; }
            Show(shopWarn.transform.parent.gameObject, !canShop); shopWarn.text = warn;
            foreach (Transform c in shopGrid) UnityEngine.Object.Destroy(c.gameObject);
            string M(int v) => "$ " + v.ToString("N0");
            bool Dis(int price) => !canShop || money < price;
            var perkSet = new HashSet<string>(perks);
            if (shopTab == "weapons")
            {
                foreach (var w0 in D.weapons)
                {
                    var w = w0.Upgraded(upg.TryGetValue(w0.id, out var tier) ? tier : 0, perkSet); bool has = owned.Contains(w0.id);
                    string stats = $"Schaden {Mathf.Round(w.damage * w.pellets)} · Feuerrate {(1 / w.rate):0.#}/s\nMagazin {w.mag} · Durchschlag {w.pierce}";
                    if (has) Card(WeaponIcon[w0.id], w.name, w.desc + "\n" + stats, weapon == w0.id ? "✔ Ausgerüstet" : $"Ausrüsten [{w.slot}]", weapon == w0.id ? U.C("#546e7a") : Blue, weapon == w0.id, () => onEquip(w0.id));
                    else Card(WeaponIcon[w0.id], w.name, w.desc + "\n" + stats, M(w.price), Green, Dis(w.price), () => onBuy(w0.id));
                }
                Card("🎁", "Glückskiste", D.crateDesc + " Steht neben dem Generator – hingehen und E drücken.", M(D.cratePrice) + " an der Kiste", U.C("#546e7a"), true, null);
            }
            else if (shopTab == "upgrades")
            {
                foreach (var w0 in D.weapons)
                {
                    if (!owned.Contains(w0.id)) continue;
                    int tier = upg.TryGetValue(w0.id, out var t0) ? t0 : 0; var cur = w0.Upgraded(tier, perkSet);
                    if (tier < D.upgradeMax) { var nx = w0.Upgraded(tier + 1, perkSet); int cost = D.upgrade.cost[tier + 1]; Card(WeaponIcon[w0.id], w0.name + " " + D.upgrade.names[tier], $"Stufe {tier + 1}: Schaden {Mathf.Round(cur.damage)} → {Mathf.Round(nx.damage)}, Magazin {cur.mag} → {nx.mag}{(tier + 1 == 3 ? ", +1 Durchschlag" : "")}", "⬆ " + M(cost), Green, Dis(cost), () => onUpgrade(w0.id)); }
                    else Card(WeaponIcon[w0.id], w0.name + " ★★★", "Voll aufgewertet – ein Prachtstück.", "★★★ MAX", U.C("#546e7a"), true, null);
                }
                Card("ℹ", "Aufwerten", "Jede Stufe macht eine Waffe deutlich stärker und vergrößert das Magazin. Stufe 3 durchschlägt einen Zombie mehr. Aufgewertete Waffen leuchten farbig.", null, Green, true, null);
            }
            else if (shopTab == "perks")
            {
                foreach (var pk in D.perks)
                {
                    bool has = perkSet.Contains(pk.id), full = !has && perks.Count >= D.maxPerks;
                    Card(pk.icon, pk.name, pk.desc, has ? "✔ Aktiv" : full ? $"Max. {D.maxPerks} Perks" : M(pk.price), has ? U.C("#546e7a") : Green, has || full || Dis(pk.price), () => onBuy(pk.id));
                }
                Card("ℹ", "Perks", $"Dauerhafte Vorteile für diese Runde (max. {D.maxPerks}). Du hast {perks.Count}/{D.maxPerks}.", null, Green, true, null);
            }
            else if (shopTab == "turrets")
            {
                foreach (var t in D.turrets) Card(t.id == "mg" ? "🗼" : t.id == "cannon" ? "💣" : "⚡", t.name, t.desc + $"\nSchaden {t.damage} · Reichweite {t.range} m · HP {t.hp}", M(t.price) + " · Platzieren", Green, Dis(t.price), () => onTurret(t.id));
                Card("🪵", D.barricadeName, D.barricadeDesc, $"{M(D.barricadePrice)} · Platzieren ({barricades}/{D.barricadeMax})", Green, Dis(D.barricadePrice), () => onTurret("barricade"));
                Card("ℹ", "So funktioniert's", "Kaufen → Vorschau erscheint vor dir → Linksklick setzt (grün = ok). Türme schießen automatisch; Barrikaden stehen quer zur Blickrichtung, Zombies müssen sie einschlagen.", null, Green, true, null);
            }
            else
            {
                foreach (var it in D.items)
                {
                    string extra = it.id == "nades" ? $" ({nades}/{D.grenadeMax})" : "";
                    string icon = it.id switch { "ammo" => "📦", "nades" => "💣", "heal" => "❤", "armor" => "🛡", "repair" => "🔧", "fixturrets" => "🛠", _ => "🪵" };
                    Card(icon, it.name + extra, it.desc, M(it.price), Green, Dis(it.price), () => onBuy(it.id));
                }
            }
        }
        void Card(string icon, string title, string desc, string btn, Color btnColor, bool disabled, Action onClick)
        {
            var c = Img(shopGrid, "card", U.C("#263238"), Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, true); c.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            var T = c.transform;
            Txt2(T, "i", icon, 30, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -50), new Vector2(-12, -8));
            Txt2(T, "t", title, 17, Color.white, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -76), new Vector2(-12, -52));
            Txt2(T, "d", desc, 12, U.C("#9a948a"), TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(12, 54), new Vector2(-12, -78), FontStyle.Normal);
            if (btn != null) { var b = Btn(T, btn, btnColor, onClick, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 10), new Vector2(-12, 48), 15); b.interactable = !disabled; }
        }

        // ------------------------------------------------------------------ Pause / Game Over
        void BuildPause()
        {
            pause = Img(root, "Pause", new Color(0, 0, 0, 0.6f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true).gameObject;
            var p = Img(pause.transform, "panel", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, -150), new Vector2(230, 150), true); p.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            Txt2(p.transform, "h", "PAUSE", 34, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -16));
            pauseInfo = Txt2(p.transform, "i", "Das Spiel läuft für deine Freunde weiter!\nKlick ins Spiel, um die Maus zu fangen.", 15, Grey, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -120), new Vector2(-10, -72), FontStyle.Normal);
            Btn(p.transform, "▶ Weiterspielen", U.C("#558b2f"), () => Game.I.Resume(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 90), new Vector2(-30, 146), 22);
            Btn(p.transform, "Spiel verlassen", U.C("#2e2b28"), () => Game.I.LeaveGame(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 30), new Vector2(-30, 76), 16);
        }
        void BuildOver()
        {
            over = Img(root, "Over", new Color(0, 0, 0, 0.7f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true).gameObject;
            var p = Img(over.transform, "panel", Panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-260, -230), new Vector2(260, 230), true); p.gameObject.AddComponent<Outline>().effectDistance = new Vector2(3, -3);
            Txt2(p.transform, "h", "GAME OVER", 44, Red, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -74), new Vector2(0, -14));
            overReason = Txt2(p.transform, "r", "", 16, Color.white, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -104), new Vector2(-10, -76), FontStyle.Normal);
            overWave = Txt2(p.transform, "w", "", 24, Accent, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -140), new Vector2(0, -106));
            overRecord = Txt2(p.transform, "rec", "🏆 NEUER REKORD!", 20, U.C("#ffd600"), TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -168), new Vector2(0, -142));
            overStats = Txt2(p.transform, "s", "", 15, Color.white, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(24, 80), new Vector2(-24, -176), FontStyle.Normal);
            Btn(p.transform, "Zurück zur Lobby", U.C("#558b2f"), () => Game.I.BackToLobby(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 20), new Vector2(-30, 70), 20);
        }

        // ------------------------------------------------------------------ Laufende Anzeigen
        public void Announce(string big, string small = "", float dur = 2.6f) { announceBig.text = big; announceSmall.text = small; annT = dur; Show(wavesum, false); }
        public void Streak(string text, Color color) { streakText.text = text; streakText.color = color; streakT = 1.3f; }
        public void Toast(string text, string kind = "", float secs = 2.6f) { toastText.text = text; toastText.color = kind == "bad" ? U.C("#ff8a80") : kind == "good" ? U.C("#b9f6ca") : Color.white; toastT = secs; }
        public void WaveSummary(string html, float dur = 6) { wavesumText.text = html; Show(wavesum, true); wsT = dur; announceBig.text = ""; announceSmall.text = ""; }
        public void Hurt(float amount) { hurtFlash.color = new Color(1, 0, 0, Mathf.Min(0.7f, 0.3f + amount / 40f)); }
        public void Hitmarker(bool kill) { hitmarker.GetComponentInChildren<Text>().color = kill ? U.C("#ff1744") : Color.white; hmT = 0.3f; }
        float hmT;
        public void NukeFlash() { nukeFlash.color = new Color(1, 1, 1, 1); }
        public void Prompt(string t) { promptText.text = t ?? ""; }
        public void Progress(float? f) { Show(progressGo, f.HasValue); if (f.HasValue) progressBar.fillAmount = f.Value; }
        public void Downed(string t) { Show(downedPanel, t != null); if (t != null) downedText.text = t; }
        public void Crosshair(float spreadPx, bool visible)
        {
            Show(crosshair, visible); float s = 5 + spreadPx;
            crossLines[0].offsetMin = new Vector2(-1, s); crossLines[0].offsetMax = new Vector2(1, s + 9);
            crossLines[1].offsetMin = new Vector2(-1, -s - 9); crossLines[1].offsetMax = new Vector2(1, -s);
            crossLines[2].offsetMin = new Vector2(-s - 9, -1); crossLines[2].offsetMax = new Vector2(-s, 1);
            crossLines[3].offsetMin = new Vector2(s, -1); crossLines[3].offsetMax = new Vector2(s + 9, 1);
        }
        readonly List<string> feed = new List<string>(); readonly List<float> feedT = new List<float>();
        public void Feed(string line) { feed.Insert(0, line); feedT.Insert(0, 5.2f); if (feed.Count > 6) { feed.RemoveAt(6); feedT.RemoveAt(6); } feedText.text = string.Join("\n", feed); }
        readonly List<string> chat = new List<string>();
        public void Chat(string line) { chat.Add(line); if (chat.Count > 8) chat.RemoveAt(0); chatLog.text = string.Join("\n", chat); }

        public void Tick(float dt)
        {
            annT -= dt; var c = announceBig.color; c.a = Mathf.Clamp01(annT * 2.5f); announceBig.color = c; var c2 = announceSmall.color; c2.a = c.a; announceSmall.color = c2;
            streakT -= dt; var sc = streakText.color; sc.a = Mathf.Clamp01(streakT * 3); streakText.color = sc; streakText.transform.localScale = Vector3.one * (1 + Mathf.Max(0, streakT - 1.1f) * 2);
            if (wsT > 0) { wsT -= dt; if (wsT <= 0) Show(wavesum, false); }
            toastT -= dt; var tc = toastText.color; tc.a = Mathf.Clamp01(toastT * 2); toastText.color = tc;
            hmT -= dt; var hc = hitmarker.GetComponentInChildren<Text>().color; hc.a = Mathf.Clamp01(hmT * 3.3f); hitmarker.GetComponentInChildren<Text>().color = hc;
            var hf = hurtFlash.color; hf.a = Mathf.Max(0, hf.a - dt * 1.4f); hurtFlash.color = hf;
            var nf = nukeFlash.color; nf.a = Mathf.Max(0, nf.a - dt * 0.8f); nukeFlash.color = nf;
            for (int i = feedT.Count - 1; i >= 0; i--) { feedT[i] -= dt; if (feedT[i] <= 0) { feedT.RemoveAt(i); feed.RemoveAt(i); feedText.text = string.Join("\n", feed); } }
        }

        // ------------------------------------------------------------------ Minikarte
        public void BuildMinimapBase(MapDef map)
        {
            const int S = 190; mmMap = map;
            mmTex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point }; minimap.texture = mmTex;
            mmBase = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S]; var g = U.C(map.ground) * 0.75f; g.a = 1; for (int i = 0; i < px.Length; i++) px[i] = g;
            float k = S / map.size, half = map.size / 2;
            void R(float x, float z, float w, float d, Color c) { int x0 = Mathf.Clamp((int)((x - w / 2 + half) * k), 0, S - 1), x1 = Mathf.Clamp((int)((x + w / 2 + half) * k), 0, S - 1), z0 = Mathf.Clamp((int)((z - d / 2 + half) * k), 0, S - 1), z1 = Mathf.Clamp((int)((z + d / 2 + half) * k), 0, S - 1); if (x1 <= x0) x1 = x0 + 1; if (z1 <= z0) z1 = z0 + 1; for (int zz = z0; zz < z1 && zz < S; zz++) for (int xx = x0; xx < x1 && xx < S; xx++) px[(S - 1 - zz) * S + (S - 1 - xx)] = c; } // x gespiegelt (linkshändiges Unity)
            foreach (var d in map.decals) R(d.x, d.z, d.w, d.d, U.C(d.color));
            foreach (var o in map.objects) { if (o.kind == "platform") continue; R(o.x, o.z, Mathf.Max(1.5f / k, o.w), Mathf.Max(1.5f / k, o.d), o.kind == "corn" ? new Color(0.78f, 0.78f, 0.2f) : (o.kind == "tree" || o.kind == "pine") ? U.C("#2e7d32") : U.C(o.color)); }
            mmBase.SetPixels32(px); mmBase.Apply(); mmPx = new Color32[S * S];
        }
        public void DrawMinimap(Vector2 me, float myYaw, IEnumerable<Remote> players, IEnumerable<Zombie> zombies, IEnumerable<Turret> turrets, IEnumerable<Pickup> pickups, IEnumerable<Barricade> barricades)
        {
            if (mmMap == null) return;
            const int S = 190; float k = S / mmMap.size, half = mmMap.size / 2;
            Array.Copy(mmBase.GetPixels32(), mmPx, mmPx.Length);
            void Dot(float x, float z, float r, Color32 c) { int cx = (int)((x + half) * k), cz = (int)((z + half) * k); for (int dz = -(int)r; dz <= r; dz++) for (int dx = -(int)r; dx <= r; dx++) { if (dx * dx + dz * dz > r * r) continue; int px = cx + dx, pz = cz + dz; if (px < 0 || pz < 0 || px >= S || pz >= S) continue; mmPx[(S - 1 - pz) * S + (S - 1 - px)] = c; } }
            void Sq(float x, float z, float s, Color32 c) { int cx = (int)((x + half) * k), cz = (int)((z + half) * k); for (int dz = -(int)s; dz <= s; dz++) for (int dx = -(int)s; dx <= s; dx++) { int px = cx + dx, pz = cz + dz; if (px < 0 || pz < 0 || px >= S || pz >= S) continue; mmPx[(S - 1 - pz) * S + (S - 1 - px)] = c; } }
            Sq(mmMap.gen.x, mmMap.gen.y, 5, U.C("#00e5ff")); Sq(mmMap.shop.x, mmMap.shop.y, 3, U.C("#ffca28")); if (mmMap.crate.HasValue) Sq(mmMap.crate.Value.x, mmMap.crate.Value.y, 3, U.C("#ff9100"));
            foreach (var b in barricades) Sq(b.x, b.z, 2, U.C("#a1887f"));
            foreach (var t in turrets) Sq(t.x, t.z, 3, U.C("#90a4ae"));
            foreach (var p in pickups) Dot(p.x, p.z, 4, U.C(p.cfg.color));
            foreach (var z in zombies) { if (z.dying) continue; Dot(z.x, z.z, z.type == "boss" ? 4.5f : z.type == "brute" ? 3.2f : z.type == "crawler" ? 1.7f : 2.2f, z.type == "boss" ? U.C("#d500f9") : z.type == "exploder" ? U.C("#ff9100") : z.type == "spitter" ? U.C("#aeea00") : U.C("#ff1744")); }
            foreach (var p in players) if (p.state != "dead") Dot(p.x, p.z, 4, p.state == "down" ? U.C("#ff5252") : U.C(p.color));
            Dot(me.x, me.y, 5, Color.white);
            // Blickrichtung
            float fx = me.x - Mathf.Sin(myYaw) * 4, fz = me.y - Mathf.Cos(myYaw) * 4; Dot(fx, fz, 2, Color.white);
            mmTex.SetPixels32(mmPx); mmTex.Apply(false);
        }
    }
}
