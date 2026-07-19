#!/usr/bin/env bash
set -euo pipefail

check_port() {
  local port="$1"
  bash -c "exec 9<>/dev/tcp/127.0.0.1/${port}" >/dev/null 2>&1
}

check_port "${AETHERXIV_MAP_PORT:-1989}"
check_port "${AETHERXIV_WORLD_PORT:-54992}"
check_port "${AETHERXIV_LOBBY_PORT:-54994}"

if [[ "${AETHERXIV_ENABLE_LAUNCHER_SERVICES:-true}" == "true" ]]; then
  check_port "${AETHERXIV_LAUNCHER_SERVICES_PORT:-8080}"
fi
