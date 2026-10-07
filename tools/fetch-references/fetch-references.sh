#!/usr/bin/env bash
# Скачивает исходники для справки в ../research (рядом с репозиторием). Только чтение.
# Использование: tools/fetch-references/fetch-references.sh [папка] [--rus] [--full]
set -euo pipefail
here="$(cd "$(dirname "$0")/../.." && pwd)"
target="$here/../research"; rus=0; full=0
for a in "$@"; do case "$a" in --rus) rus=1;; --full) full=1;; *) target="$a";; esac; done
mkdir -p "$target"; target="$(cd "$target" && pwd)"; echo "Папка: $target"

clone() { [ -d "$target/$2/.git" ] && { echo "есть: $2"; return; }; echo "клонирую: $1"; git clone --depth 1 "$1" "$target/$2"; }
clone_era() {
  [ -d "$target/$2/.git" ] && { echo "есть: $2"; return; }
  if [ "$full" = 1 ]; then git clone --depth 1 "$1" "$target/$2"; return; fi
  echo "клонирую (только тексты): $1"
  git clone --filter=blob:none --sparse --depth 1 "$1" "$target/$2"
  git -C "$target/$2" sparse-checkout set --no-cone "/Help/" "/Mods/*/Data/s/" "/Mods/*/Lang/" "/Mods/*/lang/" "/Mods/*/mod.json" "/default heroes3.ini" "/LICENSE" "/README.md"
}

clone https://github.com/ethernidee/era era
clone https://github.com/ethernidee/b2 b2
clone_era https://github.com/ERA-Projects/era-project-eng era-eng
[ "$rus" = 1 ] && clone_era https://github.com/ERA-Projects/era-project-rus era-rus
clone https://github.com/GrayFace/wog wog
clone https://github.com/alexandersorokin/heroes3-era-wogify wogify
clone https://github.com/rehan-remade/universal-modder universal-modder
clone https://github.com/Weolcan/homm-olden-era-community-mods homm-olden-era-community-mods
clone https://github.com/vcmi/vcmi vcmi

echo
echo "Готово. Для тестов на корпусах:"
echo "  export ERA_MODS_DIR=\"$target/era-eng/Mods\""
echo "  export WOG_SCRIPTS_DIR=\"$target/wogify/Mods/WoG Wogify Scripts 3.58f/Data/s\""
echo "  dotnet test tests/WoG.Tests"
