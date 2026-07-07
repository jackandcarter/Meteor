#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

usage() {
  cat <<'EOF'
Usage: tools/configure-public-vps.sh --public-host HOST [--world-port PORT]

Updates the database world route that the lobby gives to game clients.

For a single-host VPS, keep the map server internal and bind only web/lobby/world
publicly:

  WEB_BIND=0.0.0.0
  LOBBY_IP=0.0.0.0
  WORLD_IP=0.0.0.0
  MAP_IP=127.0.0.1

The public host should be the DNS name or public IP that remote clients can
reach, for example:

  tools/configure-public-vps.sh --public-host play.example.com
EOF
}

PUBLIC_HOST=""
WORLD_PORT="${WORLD_PORT:-54992}"

while [[ "$#" -gt 0 ]]; do
  case "$1" in
    --public-host)
      PUBLIC_HOST="${2:-}"
      shift 2
      ;;
    --world-port)
      WORLD_PORT="${2:-}"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "unknown argument: $1" >&2
      usage >&2
      exit 2
      ;;
  esac
done

if [[ -z "$PUBLIC_HOST" ]]; then
  echo "--public-host is required" >&2
  usage >&2
  exit 2
fi

if [[ -z "${MYSQL_BIN:-}" ]]; then
  if command -v mariadb >/dev/null 2>&1; then
    MYSQL_BIN="mariadb"
  else
    MYSQL_BIN="mysql"
  fi
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "MariaDB/MySQL client not found. Install mariadb-client or mysql-client." >&2
  exit 2
fi

DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_PORT="${DB_APP_PORT:-${AETHER_DB_PORT:-${METEOR_DB_PORT:-3306}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

sql_escape() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\'/\\\'}"
  printf '%s' "$value"
}

mysql_args=(-h "$DB_APP_HOST" -P "$DB_APP_PORT" -u "$DB_APP_USER")
if [[ -n "$DB_APP_PASS" ]]; then
  mysql_args+=("-p$DB_APP_PASS")
fi

public_host_sql="$(sql_escape "$PUBLIC_HOST")"

"$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" <<SQL
UPDATE servers
SET address = '$public_host_sql',
    port = $WORLD_PORT,
    isActive = 1
WHERE id = 1;

SELECT id, name, address, port, isActive
FROM servers
ORDER BY listPosition, id;
SQL

cat <<EOF

Public route updated.

Suggested .env.local service binds for a single-host VPS:
WEB_BIND=0.0.0.0
LOBBY_IP=0.0.0.0
WORLD_IP=0.0.0.0
MAP_IP=127.0.0.1

Keep server_zones.serverIp as 127.0.0.1 when the map server is on this same VPS;
the world server uses that route internally.
EOF
