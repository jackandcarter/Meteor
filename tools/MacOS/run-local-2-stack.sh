#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
TRACE_DIR="${AETHERXIV_TRACE_DIR:-$HOME/Library/Application Support/AetherXIV/Diagnostics/manual}"
DB_HOST="${AETHERXIV_DB_HOST:-127.0.0.1}"
DB_PORT="${AETHERXIV_DB_PORT:-3306}"
DB_NAME="${AETHERXIV_DB_NAME:-ffxiv_server}"
DB_USER="${AETHERXIV_DB_USER:-aetherxiv}"
DB_PASSWORD="${AETHERXIV_DB_PASSWORD:-aether_dev}"
SCRIPTS_ROOT="${AETHERXIV_SCRIPTS_ROOT:-$ROOT_DIR/Data/scripts}"
if [[ ! -f "$SCRIPTS_ROOT/player.lua" ]]; then
  echo "AetherXIV scripts root is missing player.lua: $SCRIPTS_ROOT" >&2
  echo "Copy scripts into Data/scripts or set AETHERXIV_SCRIPTS_ROOT explicitly." >&2
  exit 1
fi

mkdir -p "$TRACE_DIR"

echo "Building AetherXIV hosts once before parallel startup..."
"$DOTNET_BIN" build "$ROOT_DIR/AetherXIV.sln" \
  --no-restore \
  -m:1 \
  /nodeReuse:false

pids=()
cleanup() {
  for pid in "${pids[@]}"; do
    if kill -0 "$pid" >/dev/null 2>&1; then
      kill "$pid" >/dev/null 2>&1 || true
    fi
  done
}
trap cleanup EXIT INT TERM

run_direct_core() {
  local project="$1"
  local bind="$2"
  local host="${bind%:*}"
  local port="${bind##*:}"
  shift 2
  AETHERXIV_DEV_DIAGNOSTICS=1 AETHERXIV_DEV_DIAGNOSTICS_DIR="$TRACE_DIR" \
  "$DOTNET_BIN" run --project "$ROOT_DIR/$project" --no-build --no-restore -- \
    --ip "$host" \
    --port "$port" \
    --host "$DB_HOST" \
    --db-port "$DB_PORT" \
    --db "$DB_NAME" \
    --user "$DB_USER" \
    --p "$DB_PASSWORD" \
    "$@" &
  pids+=("$!")
}

run_direct_core "src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "127.0.0.1:1989" --no-console
run_direct_core "src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "127.0.0.1:54992" --no-console
run_direct_core "src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "127.0.0.1:54994"

"$DOTNET_BIN" run --project "$ROOT_DIR/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" --no-build --no-restore -- \
  --bind 127.0.0.1:8080 \
  --db-host "$DB_HOST" \
  --db-port "$DB_PORT" \
  --db-name "$DB_NAME" \
  --db-user "$DB_USER" \
  --db-password "$DB_PASSWORD" &
pids+=("$!")

echo "AetherXIV 2.0 local stack started. Trace dir: $TRACE_DIR"
echo "Launcher service: http://127.0.0.1:8080/launcher"
echo "Press Ctrl-C to stop."
wait
