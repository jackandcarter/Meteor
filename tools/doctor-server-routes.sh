#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

status() {
  printf '%-24s %s\n' "$1" "$2"
}

warn() {
  status "$1" "WARN: $2"
}

ok() {
  status "$1" "ok: $2"
}

fail() {
  status "$1" "FAIL: $2"
}

if [[ -z "${MYSQL_BIN:-}" ]]; then
  if command -v mariadb >/dev/null 2>&1; then
    MYSQL_BIN="mariadb"
  else
    MYSQL_BIN="mysql"
  fi
fi

DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_PORT="${DB_APP_PORT:-${AETHER_DB_PORT:-${METEOR_DB_PORT:-3306}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"

WEB_BIND="${WEB_BIND:-127.0.0.1}"
WEB_PORT="${WEB_PORT:-8080}"
LOBBY_IP="${LOBBY_IP:-${SERVER_IP:-127.0.0.1}}"
LOBBY_PORT="${LOBBY_PORT:-54994}"
WORLD_IP="${WORLD_IP:-${SERVER_IP:-127.0.0.1}}"
WORLD_PORT="${WORLD_PORT:-54992}"
MAP_IP="${MAP_IP:-${SERVER_IP:-127.0.0.1}}"
MAP_PORT="${MAP_PORT:-1989}"

echo "AetherXIV route doctor"
echo "Project root: $ROOT_DIR"
echo

echo "Configured binds"
status "web" "$WEB_BIND:$WEB_PORT"
status "lobby" "$LOBBY_IP:$LOBBY_PORT"
status "world" "$WORLD_IP:$WORLD_PORT"
status "map" "$MAP_IP:$MAP_PORT"
echo

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  fail "mysql client" "$MYSQL_BIN not found"
  exit 2
fi

mysql_args=(-h "$DB_APP_HOST" -P "$DB_APP_PORT" -u "$DB_APP_USER")
if [[ -n "$DB_APP_PASS" ]]; then
  mysql_args+=("-p$DB_APP_PASS")
fi

if "$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" -e "SELECT 1;" >/dev/null 2>&1; then
  ok "database" "$DB_APP_USER@$DB_APP_HOST:$DB_APP_PORT/$DB_NAME"
else
  fail "database" "$DB_APP_USER@$DB_APP_HOST:$DB_APP_PORT/$DB_NAME is not reachable"
  exit 20
fi
echo

echo "World advertised to clients"
"$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" -e "SELECT id, name, address, port, isActive FROM servers ORDER BY listPosition, id;"
advertised_loopback="$("$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" -N -B -e "SELECT COUNT(*) FROM servers WHERE isActive = 1 AND address IN ('127.0.0.1', 'localhost', '0.0.0.0');")"
if [[ "${advertised_loopback:-0}" != "0" ]]; then
  warn "client world route" "active servers table advertises a loopback/bind-only address"
else
  ok "client world route" "active servers table is not advertising loopback"
fi
echo

echo "Map routes used internally by World"
"$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" -e "SELECT serverIp, serverPort, COUNT(*) AS zone_count FROM server_zones WHERE zoneName IS NOT NULL GROUP BY serverIp, serverPort ORDER BY zone_count DESC;"
matching_zone_count="$("$MYSQL_BIN" "${mysql_args[@]}" "$DB_NAME" -N -B -e "SELECT COUNT(*) FROM server_zones WHERE zoneName IS NOT NULL AND serverIp = '$MAP_IP' AND serverPort = $MAP_PORT;")"
if [[ "${matching_zone_count:-0}" == "0" ]]; then
  warn "map zone load" "no server_zones rows match MAP_IP=$MAP_IP MAP_PORT=$MAP_PORT; Map will load 0 zones with this bind"
else
  ok "map zone load" "$matching_zone_count zones match MAP_IP=$MAP_IP MAP_PORT=$MAP_PORT"
fi
echo

if command -v ss >/dev/null 2>&1; then
  echo "Listening sockets"
  ss -ltnp 2>/dev/null | awk 'NR == 1 || /:8080|:54994|:54992|:1989/'
  echo
else
  warn "socket list" "ss command not found"
fi

tcp_probe() {
  local label="$1"
  local host="$2"
  local port="$3"

  if timeout 2 bash -c "</dev/tcp/$host/$port" >/dev/null 2>&1; then
    ok "$label" "$host:$port accepts TCP"
  else
    warn "$label" "$host:$port did not accept TCP from this host"
  fi
}

probe_world_host="127.0.0.1"
probe_map_host="$MAP_IP"
if [[ "$probe_map_host" == "0.0.0.0" ]]; then
  probe_map_host="127.0.0.1"
fi

echo "Local TCP probes"
tcp_probe "web" "127.0.0.1" "$WEB_PORT"
tcp_probe "lobby" "127.0.0.1" "$LOBBY_PORT"
tcp_probe "world" "$probe_world_host" "$WORLD_PORT"
tcp_probe "map" "$probe_map_host" "$MAP_PORT"

cat <<'EOF'

Expected single-host VPS shape:
  servers.address        = public DNS/IP that players can reach
  servers.port           = 54992
  server_zones.serverIp  = 127.0.0.1
  server_zones.serverPort= 1989
  WEB_BIND/LOBBY_IP/WORLD_IP = 0.0.0.0
  MAP_IP = 127.0.0.1
EOF
