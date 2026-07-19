#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="/opt/aetherxiv"

usage() {
  echo "Usage: $0 <lobby|world|map>" >&2
}

SERVICE="${1:-}"
case "$SERVICE" in
  lobby|world|map)
    ;;
  *)
    usage
    exit 64
    ;;
esac

cd "$ROOT_DIR"

# tools/load-local-env.sh sources .env.defaults AFTER the process
# environment, which would clobber compose-injected values (AETHER_DB_HOST,
# LOBBY_IP/WORLD_IP/MAP_IP) back to 127.0.0.1. Inside a container, compose
# env plus the run scripts' inline fallbacks are the only config sources.
rm -f "$ROOT_DIR/.env.defaults"

export AUTO_PREPARE_STATICACTORS=0
export REFRESH_RUNTIME_DATA=0

# tools/copy-runtime-data.sh only returns non-zero when a required
# Data/*_config.ini or Data/scripts source is missing (copy_file/copy_dir
# return 1 in that case). A missing Data/staticactors.bin is logged as a
# warning and does not itself fail the script, but that warning is
# tolerated explicitly here (grep on its known message) in case the
# script's exit behavior changes later - any other failure still
# propagates loudly instead of being swallowed.
set +e
copy_output="$(tools/copy-runtime-data.sh 2>&1)"
copy_status=$?
set -e
printf '%s\n' "$copy_output"

if [[ $copy_status -ne 0 ]]; then
  if printf '%s\n' "$copy_output" | grep -q "Data/staticactors.bin is missing"; then
    echo "warning: continuing without Data/staticactors.bin ($SERVICE Server will still start)"
  else
    echo "tools/copy-runtime-data.sh failed with status $copy_status" >&2
    exit "$copy_status"
  fi
fi

READY_FILE="${AETHER_READY_FILE:-/tmp/${SERVICE}.ready}"
rm -f "$READY_FILE"

exec "tools/run-${SERVICE}.sh"
