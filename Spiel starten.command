#!/bin/bash
# Doppelklick startet den Zombie-Ansturm-Server (macOS)
cd "$(dirname "$0")"
if ! command -v node >/dev/null 2>&1; then
  echo "Node.js fehlt. Bitte von https://nodejs.org installieren."; read -r; exit 1
fi
[ -d node_modules ] || npm install
(sleep 1.5 && open "http://localhost:3000") &
npm start
