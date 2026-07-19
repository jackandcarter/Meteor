#!/usr/bin/env bash
set -euo pipefail

DATABASE_PACKAGE=/opt/aetherxiv/database
SERVER_ROOT=/opt/aetherxiv/servers

MAP_PORT="${AETHERXIV_MAP_PORT:-1989}"
WORLD_PORT="${AETHERXIV_WORLD_PORT:-54992}"
LOBBY_PORT="${AETHERXIV_LOBBY_PORT:-54994}"
LAUNCHER_SERVICES_PORT="${AETHERXIV_LAUNCHER_SERVICES_PORT:-8080}"
PUBLIC_HOST="${AETHERXIV_PUBLIC_HOST:-127.0.0.1}"
ENABLE_LAUNCHER_SERVICES="${AETHERXIV_ENABLE_LAUNCHER_SERVICES:-true}"

validate_port() {
  local name="$1"
  local value="$2"
  if ! [[ "${value}" =~ ^[0-9]+$ ]] || ((value < 1 || value > 65535)); then
    echo "${name} must be a TCP port from 1 through 65535; received '${value}'." >&2
    exit 2
  fi
}

validate_port AETHERXIV_MAP_PORT "${MAP_PORT}"
validate_port AETHERXIV_WORLD_PORT "${WORLD_PORT}"
validate_port AETHERXIV_LOBBY_PORT "${LOBBY_PORT}"
validate_port AETHERXIV_LAUNCHER_SERVICES_PORT "${LAUNCHER_SERVICES_PORT}"
[[ "${PUBLIC_HOST}" =~ ^[A-Za-z0-9._:-]+$ ]] || {
  echo "AETHERXIV_PUBLIC_HOST must be an IP address or DNS name without a URL scheme." >&2
  exit 2
}
[[ "${ENABLE_LAUNCHER_SERVICES}" == "true" || "${ENABLE_LAUNCHER_SERVICES}" == "false" ]] || {
  echo "AETHERXIV_ENABLE_LAUNCHER_SERVICES must be true or false." >&2
  exit 2
}

echo "Checking the AetherXIV database package..."
if "${DATABASE_PACKAGE}/setup.sh" --check >/tmp/aetherxiv-database-check.log 2>&1; then
  cat /tmp/aetherxiv-database-check.log
else
  echo "The database needs installation or migration; applying the packaged baseline and pending migrations."
  "${DATABASE_PACKAGE}/setup.sh"
fi

database=(
  mariadb
  --host="${AETHERXIV_DB_HOST:-mariadb}"
  --port="${AETHERXIV_DB_PORT:-3306}"
  --user="${AETHERXIV_DB_USER:-aetherxiv}"
  "--password=${AETHERXIV_DB_PASSWORD:?AETHERXIV_DB_PASSWORD is required}"
  "${AETHERXIV_DB_NAME:-ffxiv_server}"
)

"${database[@]}" --execute="
  UPDATE servers
  SET address='${PUBLIC_HOST}', port=${WORLD_PORT}, isActive=1
  WHERE id=1;
  UPDATE server_zones
  SET serverIp='127.0.0.1', serverPort=${MAP_PORT}
  WHERE serverIp IS NOT NULL;
"

common_database_args=(
  --host "${AETHERXIV_DB_HOST:-mariadb}"
  --db-port "${AETHERXIV_DB_PORT:-3306}"
  --db "${AETHERXIV_DB_NAME:-ffxiv_server}"
  --user "${AETHERXIV_DB_USER:-aetherxiv}"
  --p "${AETHERXIV_DB_PASSWORD}"
)

wait_for_port() {
  local label="$1"
  local port="$2"
  local pid="$3"
  local deadline=$((SECONDS + 45))
  until bash -c "exec 9<>/dev/tcp/127.0.0.1/${port}; exec 9>&-; exec 9<&-" >/dev/null 2>&1; do
    if ! kill -0 "${pid}" >/dev/null 2>&1; then
      wait "${pid}" || true
      echo "${label} exited before opening port ${port}." >&2
      exit 1
    fi
    if ((SECONDS >= deadline)); then
      echo "Timed out waiting for ${label} on port ${port}." >&2
      exit 1
    fi
    sleep 0.25
  done
  echo "${label} is accepting connections on port ${port}."
}

mkdir -p /tmp/aetherxiv-input
mkfifo \
  /tmp/aetherxiv-input/map \
  /tmp/aetherxiv-input/world \
  /tmp/aetherxiv-input/lobby \
  /tmp/aetherxiv-input/launcher-services
exec 3<>/tmp/aetherxiv-input/map
exec 4<>/tmp/aetherxiv-input/world
exec 5<>/tmp/aetherxiv-input/lobby
exec 6<>/tmp/aetherxiv-input/launcher-services

dotnet "${SERVER_ROOT}/map/AetherXIV.Core.Map.dll" \
  --ip 127.0.0.1 \
  --port "${MAP_PORT}" \
  "${common_database_args[@]}" \
  --no-console <&3 &
map_pid=$!
wait_for_port Map "${MAP_PORT}" "${map_pid}"

dotnet "${SERVER_ROOT}/world/AetherXIV.Core.World.dll" \
  --ip 0.0.0.0 \
  --port "${WORLD_PORT}" \
  "${common_database_args[@]}" \
  --no-console <&4 &
world_pid=$!
wait_for_port World "${WORLD_PORT}" "${world_pid}"

dotnet "${SERVER_ROOT}/lobby/AetherXIV.Core.Lobby.dll" \
  --ip 0.0.0.0 \
  --port "${LOBBY_PORT}" \
  "${common_database_args[@]}" <&5 &
lobby_pid=$!
wait_for_port Lobby "${LOBBY_PORT}" "${lobby_pid}"

pids=("${map_pid}" "${world_pid}" "${lobby_pid}")
if [[ "${ENABLE_LAUNCHER_SERVICES}" == "true" ]]; then
  dotnet "${SERVER_ROOT}/launcher-services/AetherXIV.Launcher.Host.dll" \
    --bind "0.0.0.0:${LAUNCHER_SERVICES_PORT}" \
    --db-host "${AETHERXIV_DB_HOST:-mariadb}" \
    --db-port "${AETHERXIV_DB_PORT:-3306}" \
    --db-name "${AETHERXIV_DB_NAME:-ffxiv_server}" \
    --db-user "${AETHERXIV_DB_USER:-aetherxiv}" \
    --db-password "${AETHERXIV_DB_PASSWORD}" \
    --allow-account-create "${AETHERXIV_LAUNCHER_ALLOW_ACCOUNT_CREATE:-true}" <&6 &
  launcher_services_pid=$!
  pids+=("${launcher_services_pid}")
  wait_for_port "Launcher Services" "${LAUNCHER_SERVICES_PORT}" "${launcher_services_pid}"
fi

stopping=0
shutdown_children() {
  if ((stopping == 1)); then
    return
  fi
  stopping=1
  echo "Stopping the AetherXIV server stack..."
  printf 'shutdown\n' >&6 || true
  printf 'shutdown\n' >&5 || true
  printf 'shutdown\n' >&4 || true
  printf 'shutdown\n' >&3 || true

  local deadline=$((SECONDS + 20))
  local running
  while ((SECONDS < deadline)); do
    running=0
    for pid in "${pids[@]}"; do
      kill -0 "${pid}" >/dev/null 2>&1 && running=1
    done
    ((running == 0)) && break
    sleep 0.25
  done

  for pid in "${pids[@]}"; do
    kill -TERM "${pid}" >/dev/null 2>&1 || true
  done
  wait "${pids[@]}" 2>/dev/null || true
}

trap 'shutdown_children; exit 0' INT TERM

echo "AetherXIV server stack is ready. Public World endpoint: ${PUBLIC_HOST}:${WORLD_PORT}"
set +e
wait -n "${pids[@]}"
status=$?
set -e
echo "An AetherXIV service exited with status ${status}; stopping the remaining services." >&2
shutdown_children
exit "${status}"
