# Zombie-Ansturm auf einem anderen Mac einrichten

Es gibt zwei Spielvarianten, die denselben Server nutzen:

| Variante | Branch | Was man braucht | Aufwand |
|---|---|---|---|
| **Browser-Version** (Klötzchen-Stil) | `main` (Tag `v1-kloetzchen`) | nur Node.js | 2 Minuten |
| **Unity-Version „Ruinenstadt“** (realistisch, HDRP) | `realistisch` | Node.js, Unity 6000.3.28f1, die vier gekauften Store-Pakete | ca. 1 Stunde (davon ~10 Min. Build) |

Mitspieler im selben WLAN brauchen für die Browser-Version gar nichts zu installieren. Für die Unity-Version braucht jeder Mitspieler die gebaute App (`Zombie-Ansturm.app`), die man einmal baut und dann weitergeben kann (ca. 2 GB).

---

## 1. Grundlage: Repository und Server (für beide Varianten)

1. **Node.js** installieren (LTS-Version): <https://nodejs.org>
2. Repository holen:
   ```bash
   git clone https://github.com/HenryCoding/zombie-ansturm.git
   cd zombie-ansturm
   npm install
   ```
3. Server starten:
   ```bash
   npm start
   ```
   Der Server zeigt Adressen wie `http://192.168.1.201:3000`. Diese Adresse öffnen die Mitspieler im Browser bzw. tragen sie in der Unity-App in der Lobby ein. Alle Geräte müssen im selben WLAN sein.

   Alternativ per Doppelklick: `Spiel starten.command` (startet Server und öffnet den Browser).

Damit läuft die Browser-Version bereits. Für die Unity-Version weiter mit Schritt 2.

---

## 2. Unity-Version „Ruinenstadt“

### 2.1 Unity installieren
1. **Unity Hub** installieren: <https://unity.com/download>
2. Im Hub unter *Installs → Install Editor* genau die Version **6000.3.28f1** wählen (andere Versionen können Projekt und Pakete anders importieren). Beim Installieren das Modul **Mac Build Support (Mono)** anhaken.
3. Mit der Unity-ID anmelden, mit der die Store-Pakete gekauft wurden.

### 2.2 Branch wechseln und Projekt öffnen
```bash
git checkout realistisch
```
Im Unity Hub *Add → Add project from disk* und den Ordner `zombie-ansturm/unity` wählen. Der erste Import dauert einige Minuten.

### 2.3 Die vier Store-Pakete importieren
Die Pakete sind lizenzgebunden und **nicht im Repository**. Sie müssen aus dem eigenen Asset-Store-Konto importiert werden (siehe auch `EINKAUFSLISTE.md`):

| Paket | Anbieter | Landet unter |
|---|---|---|
| Dead District – Post Apocalyptic City Environment | Neo Star | `Assets/DeadDistrict/` |
| Realistic Zombie Character | Snake3D | `Assets/Snake3D/` |
| Zombie Animation Set | Nephilite Studios | `Assets/Nephilite Studios/` |
| Animated FPS Weapons Pack (Part 1) | BSP | `Assets/Animated FPS Weapons Part 1/` |

Vorgehen in Unity: *Window → Package Manager*, oben links auf **My Assets** umstellen, jedes Paket **Download** und dann **Import** (alle Dateien angehakt lassen). Beim Waffenpaket die Rückfrage zur Render-Pipeline mit HDRP beantworten, falls sie erscheint.

Abkürzung: Wenn die Pakete im Hub bereits heruntergeladen wurden, importiert der Menüpunkt **Zombie-Ansturm → Store-Pakete importieren** alle `.unitypackage`-Dateien aus dem lokalen Asset-Store-Cache auf einmal.

### 2.4 Projekt einrichten (einmalig, Reihenfolge einhalten)
Alle Schritte sind Menüpunkte im Unity-Menü **Zombie-Ansturm**:

1. **Projekt einrichten** – HDRP-Einstellungen, Shader-Varianten, Layer.
2. **Store-Assets aufbereiten** – Materialien der Pakete auf HDRP, Zombie-Rigs auf Humanoid, Animator, Prefab-Kopien unter `Resources/Generated`. Dauert einige Minuten.
3. **Ruinenstadt exportieren** – baut aus der Demo-Szene des Stadtpakets die Spielkarte: eigene Szene `Assets/Scenes/City.unity`, doppelseitige Kollider, abgesetzte Gebäude, Begehbarkeitsmaske und Platzwahl (Generator, Shop, Spawns) in `shared/maps-ruins.js`. Dauert 5–10 Minuten.
4. Im Terminal die Kartendaten für den Client erneuern:
   ```bash
   node tools/export-unity.mjs
   ```
5. **Mac-App bauen** – erzeugt `unity/Build/Zombie-Ansturm.app` (ca. 10 Minuten, ca. 2 GB).

Dasselbe ohne Unity-Oberfläche, aus dem Terminal (Pfad zur Unity-App ggf. anpassen):
```bash
U="/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity"
P="$PWD/unity"
"$U" -batchmode -nographics -quit -projectPath "$P" -executeMethod ZA.Setup.Configure     -logFile setup.log
"$U" -batchmode -nographics -quit -projectPath "$P" -executeMethod ZA.StoreAssets.Prepare -logFile prepare.log
"$U" -batchmode -nographics -quit -projectPath "$P" -executeMethod ZA.CityExport.Export   -logFile export.log
node tools/export-unity.mjs
"$U" -batchmode -nographics -quit -projectPath "$P" -executeMethod ZA.Setup.BuildMac      -logFile build.log
```
Erfolg steht am Ende von `build.log` als `Build Finished, Result: Success.`

### 2.5 Spielen
- Server starten (`npm start`), dann `unity/Build/Zombie-Ansturm.app` öffnen. Oder beides per Doppelklick auf `Unity-Version starten.command`.
- In der Lobby die Serveradresse eintragen (bei Spiel auf demselben Mac: `localhost:3000`), Karte **Ruinenstadt** wählen, Start.
- Mitspieler: die App kopieren (sie ist eigenständig, braucht kein Unity) und in der Lobby die WLAN-Adresse des Servers eintragen, z. B. `192.168.1.201:3000`.

### 2.6 Selbsttest (optional)
Die App kann sich selbst prüfen: verbindet sich, spielt eine Runde an und speichert Screenshots.
```bash
caffeinate -u -d -t 300 &   # Bildschirm wach halten, sonst blockiert Unity beim Rendern
./unity/Build/Zombie-Ansturm.app/Contents/MacOS/Zombie-Ansturm -autotest ~/za-test -server localhost:3000 -map ruins -screen-width 1280 -screen-height 800
```
Ergebnis in `~/za-test`: Screenshots, `state.txt`, `errors.txt` (leer = gut). Für einen Mehrspielertest eine zweite Kopie der App mit `-secondary -delay 14` starten.

---

## Bekannte Stolpersteine
- **App startet nicht / bleibt schwarz**: Der Bildschirm darf nicht im Ruhezustand sein; beim Selbsttest `caffeinate` benutzen.
- **„Hier ist kein Platz“ überall oder Server reagiert nicht**: alten Server beenden (`pkill -f "node server/server.js"`) und neu starten, damit die aktuelle `shared/maps-ruins.js` geladen ist.
- **Generator steht woanders als der Spawn**: `node tools/export-unity.mjs` vergessen und App neu bauen.
- **Store-Pakete erscheinen nicht unter „My Assets“**: in Unity mit derselben Unity-ID angemeldet wie beim Kauf?
- Die Store-Ordner und `unity/Build/` sind in `.gitignore`; sie gehören nicht ins Repository.
