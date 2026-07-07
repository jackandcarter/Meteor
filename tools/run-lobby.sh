#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"
CONFIGURATION="${CONFIGURATION:-Release}"
LOBBY_IP="${LOBBY_IP:-${SERVER_IP:-127.0.0.1}}"
LOBBY_PORT="${LOBBY_PORT:-54994}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

SERVER_EXE="$(resolve_server_executable "Lobby Server" "$CONFIGURATION")"
cd "$(dirname "$SERVER_EXE")"
exec mono "$(basename "$SERVER_EXE")" --ip "$LOBBY_IP" --port "$LOBBY_PORT" --host "$DB_APP_HOST" --db "$DB_NAME" --user "$DB_APP_USER" --p "$DB_APP_PASS" "$@"
