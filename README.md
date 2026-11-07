# 🧟 Zombie-Ansturm

Koop-Zombie-Verteidigung in der Egoperspektive – im Browser, in Echtzeit, mit Freunden auf mehreren Geräten (PC, Mac, Tablet, Handy).

## Starten

**Mac:** Doppelklick auf `Spiel starten.command`

**Oder im Terminal:**
```bash
npm install
npm start
```

Der Server zeigt danach Adressen wie `http://192.168.1.201:3000`.
Deine Freunde öffnen genau diese Adresse im Browser – **alle müssen im selben WLAN sein**.
(Die Adresse steht auch in der Lobby unter „Freunde einladen“.)

## Spielprinzip
- Verteidige den **Generator** in der Kartenmitte. Ist er zerstört oder sind alle Spieler gefallen → Game Over.
- Zombies kommen in **Wellen**, jede Welle wird stärker. Alle 5 Wellen kommt ein **Koloss** (Boss).
- Geld gibt es für Treffer, Kills, Kopfschüsse, Wiederbeleben und jede überstandene Welle.
- Im **Shop** (Taste B) gibt es 7 Waffen, 3 Geschütztürme und Ausrüstung.
  Zwischen den Wellen überall, während einer Welle nur am Shop-Terminal neben dem Generator.
- Fällt ein Spieler, kann ein Mitspieler ihn mit **E (halten)** wiederbeleben. Allein hast du 2 Extra-Leben.

## Karten
Vorstadt · Bauernhof · Militärbasis · Friedhof (Nacht mit Taschenlampe)

## Zombies
Zombie · Renner (schnell) · Brocken (zäh) · Explodierer (platzt) · Kriecher (flach, flink) · Spucker (Säure) · Koloss (Boss)

## Was das Spiel bietet
- **Power-ups** fallen von Zombies: ☠ Insta-Kill, ✖2 Doppelte Punkte, ☢ Nuke, 📦 Max-Munition, ❄ Eiszeit – gelten fürs ganze Team, einfach drüberlaufen.
- **Waffen aufwerten** (Shop → Aufwerten): 3 Stufen, mehr Schaden, größeres Magazin, Stufe 3 durchschlägt mehr. Aufgewertete Waffen leuchten blau/lila/gold.
- **Perks** (Shop → Perks, max. 4 pro Runde): Dickhäuter, Turbo-Lader, Sprinter, Sanitäter, Glückspilz, Munitionsgurt.
- **Glückskiste** neben dem Generator: $950, E drücken, Zufallswaffe (mit Glück aufgewertet).
- **Barrikaden** (Shop → Geschütztürme): Holzwälle, die Zombies erst einschlagen müssen. Sie stehen quer zu deiner Blickrichtung, lassen sich lückenlos aneinanderreihen, und du kannst drüberspringen. Zombies nehmen lieber einen Umweg, wenn es einen gibt – also baut Engstellen und stellt Türme dahinter.
- **Handgranaten** (G) und **Messer** (V).
- **Kill-Serien** bringen Bonusgeld und Ansagen; **Kopfschüsse** lassen Köpfe platzen.
- **7 Zombie-Arten**: Zombie, Renner, Brocken, Explodierer, Kriecher (schnell, flach), Spucker (Säure aus der Distanz) und der Koloss.
- **Spezialwellen**: alle 3 Wellen Blitzwelle (nur Renner/Kriecher), alle 5 Wellen Boss, ab Welle 8 alle 4 Wellen Horde.
- **Schwierigkeit** (Entspannt / Normal / Albtraum), **Wellen-Zusammenfassung** mit MVP und gespeicherte **Rekorde** pro Karte.

## Steuerung
| Taste | Aktion |
|---|---|
| W A S D | Laufen |
| Maus | Umsehen |
| Linksklick / Rechtsklick | Schießen / Zielen |
| Leertaste / Shift | Springen / Sprinten |
| R | Nachladen |
| 1–7, Mausrad | Waffe wechseln |
| B | Shop |
| E (halten) | Wiederbeleben / Glückskiste öffnen |
| G | Granate werfen |
| V | Messer |
| N | Bereit (nächste Welle früher starten, wenn alle bereit sind) |
| T | Chat |
| Tab | Punktestand |
| F | Taschenlampe (Friedhof) |
| Q | Turm-Platzierung abbrechen |

Auf Handy/Tablet erscheinen automatisch Touch-Steuerelemente (Gerät quer halten): Der Joystick erscheint dort, wo der linke Daumen den Bildschirm berührt (ganz nach vorn = Sprint); Umsehen durch Ziehen auf der rechten Hälfte; **Auto-Feuer** schießt von selbst, sobald ein Zombie im Fadenkreuz ist (in den Einstellungen abschaltbar); Waffenwechsel durch Antippen der Waffen-Slots unten; Treffer und Schaden vibrieren (Android). Über „Zum Home-Bildschirm hinzufügen“ bekommt das Spiel ein eigenes Icon und läuft im Vollbild.

Hinweis: Zeigt der Browser für `localhost:3000` noch ein altes Icon einer anderen App, hilft einmal neu laden (Cmd+Shift+R) oder das Löschen der Website-Daten für localhost.

**Maus-Sperre:** Beim Klick ins Spiel wird der Mauszeiger normalerweise eingefangen (Esc gibt ihn frei und öffnet die Pause). Erlaubt der Browser das nicht (z. B. eingebettete Browser-Fenster), schaltet das Spiel automatisch auf einen Modus ohne Sperre um: Maus bewegen = umsehen, Esc = Pause an/aus. Am besten spielt es sich in einem normalen Browser (Chrome, Safari, Firefox, Edge).

## Über das Internet spielen
Standardmäßig nur im lokalen Netz. Für Freunde außerhalb kannst du z. B. Tailscale nutzen
oder einen Tunnel wie `npx cloudflared tunnel --url http://localhost:3000` starten und ihnen die angezeigte Adresse schicken.

## Technik
- `server/server.js` – autoritativer Spielserver (Node.js + WebSocket, 20 Ticks/s): Wellen, Zombie-KI mit Wegfindung (Flussfelder), Türme, Shop, Wiederbeleben
- `shared/` – Spielwerte und Karten (von Server und Client genutzt)
- `public/` – Three.js-Client: Klötzchen-Grafik, Animationen, Effekte, HUD, Touch-Steuerung, prozedurale Sounds
- Port ändern: `PORT=8080 npm start`

## Unity-Version (Mac-App)
**Einrichtung auf einem neuen Mac: siehe [INSTALL.md](INSTALL.md).** Auf GitHub (`unicornsuite/HenryCoding`) liegt die realistische HDRP-Fassung „Ruinenstadt“ im Branch `zombie-ansturm` (gekaufte Store-Pakete nötig, siehe `EINKAUFSLISTE.md`); der Branch `zombie-ansturm-kloetzchen` bzw. Tag `v1-kloetzchen` ist der Klötzchen-Stand.

Im Ordner `unity/` liegt ein vollständiger Unity-Client (Unity 6, HDRP) mit derselben Spiellogik: Er verbindet sich per WebSocket mit dem Node-Server, **Browser- und Unity-Spieler spielen zusammen**.

- Fertige App: `unity/Build/Zombie-Ansturm.app` (doppelklicken). In der Lobby oben links die Server-Adresse eintragen (z. B. `localhost:3000` oder die WLAN-Adresse des Rechners, auf dem `npm start` läuft).
- Neu bauen: `tools/build-unity.sh` (braucht Unity Hub + Unity 6000.3 mit Mac Build Support). Das Skript exportiert zuerst `shared/` nach `unity/Assets/Resources/gamedata.json`, damit Karten und Werte identisch bleiben.
- Im Unity-Editor öffnen: Unity Hub → Add → Ordner `unity/` wählen. Menü „Zombie-Ansturm → Projekt einrichten“ legt Szene und Render-Pipeline an; „Mac-App bauen“ erzeugt die App.
- Steuerung wie im Browser; zusätzlich **V** = Wechsel zwischen Ego- und Third-Person-Ansicht (Schulterkamera), Messer liegt in der Unity-Version auf **C** oder der mittleren Maustaste.
- Selbsttest ohne Spieler: `Zombie-Ansturm.app/Contents/MacOS/Zombie-Ansturm -autotest /tmp/za-test -server localhost:3000` spielt ein kurzes Skript durch und legt Screenshots ab.

Grafik der Unity-Version: Echtzeit-Schatten, SSAO, Bloom/Vignette/ACES, Normal Maps auf Ziegeln, Brettern und Boden, Gras aus echten Halmen, Blumen, Büsche, Steine, detaillierte Bäume, Figuren mit Ellbogen- und Kniegelenken, Kiefer-Animation und Trefferzucken bei Zombies, Waffen mit Schlitten-/Verschluss- und Magazin-Animation, Hülsenauswurf und Mündungsrauch; nachts Bodennebel, Glühwürmchen und flackernde Laternen.

Der Code liegt in `unity/Assets/Scripts/`: `Net.cs` (Verbindung), `GameData.cs` (Spieldaten), `World.cs` (Karte), `Models.cs` (Klötzchen-Modelle), `Entities.cs` (Zombies, Mitspieler, Türme, …), `Player.cs` (Bewegung, Kamera, Waffe), `Effects.cs`, `Sfx.cs`, `UI.cs`, `Game.cs` (Spielsteuerung).
