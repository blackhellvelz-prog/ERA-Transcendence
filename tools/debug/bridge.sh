#!/bin/bash
# WoG Debug command bridge client: runs one command in the running game and prints the answer.
# Needs [Debug] Enabled = true in BepInEx/config/wog.oldenera.cfg (deploy.ps1 -DebugMode sets it).
# usage: OLDEN_ERA_DIR="/path/to/Olden Era" tools/debug/bridge.sh "erm !!OW:R-1/6/?x1;"
#        tools/debug/bridge.sh help
# The command file is written as .tmp and renamed, so the plugin never reads half a file.
G="${OLDEN_ERA_DIR:?set OLDEN_ERA_DIR to the Olden Era folder}/BepInEx/config/WoG/debug"
mkdir -p "$G/in" "$G/out"
n="q$(date +%s%N)"
printf "%s" "$1" > "$G/$n.tmp" && mv "$G/$n.tmp" "$G/in/$n.txt"
for i in $(seq 1 60); do
  if [ -f "$G/out/$n.txt" ]; then cat "$G/out/$n.txt"; exit 0; fi
  sleep 0.5
done
echo "timeout: is the game running with WoG Debug on, and a map loaded?" >&2
exit 1
