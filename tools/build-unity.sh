#!/bin/bash
# Baut die Unity-Version (Mac-App) komplett per Kommandozeile.
# Voraussetzung: Unity 6000.3.28f1 über Unity Hub installiert (Pfad unten ggf. anpassen).
set -e
cd "$(dirname "$0")/.."
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.3.28f1/Unity.app/Contents/MacOS/Unity}"
if [ ! -x "$UNITY" ]; then echo "Unity nicht gefunden: $UNITY (UNITY=… setzen)"; exit 1; fi
echo "1/3 Spieldaten exportieren …"; node tools/export-unity.mjs
echo "2/3 Projekt einrichten und Mac-App bauen (dauert einige Minuten) …"
"$UNITY" -batchmode -nographics -quit -projectPath "$PWD/unity" -executeMethod ZA.Setup.BuildMac -logFile unity/Build/build.log
echo "3/3 Fertig: unity/Build/Zombie-Ansturm.app"
