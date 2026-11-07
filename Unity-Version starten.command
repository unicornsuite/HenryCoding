#!/bin/bash
# Startet den Server (falls nicht schon aktiv) und die Unity-App
cd "$(dirname "$0")"
if ! curl -s -o /dev/null http://localhost:3000/; then
  [ -d node_modules ] || npm install
  (npm start > /dev/null 2>&1 &)
  sleep 1
fi
open "unity/Build/Zombie-Ansturm.app"
