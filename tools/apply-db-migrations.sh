#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$ROOT_DIR/tools/load-local-env.sh"

MIGRATIONS_DIR="${MIGRATIONS_DIR:-$ROOT_DIR/Data/sql/migrations}"

if [[ -z "${MYSQL_BIN:-}" ]]; then
  if command -v mariadb >/dev/null 2>&1; then
    MYSQL_BIN="mariadb"
  else
    MYSQL_BIN="mysql"
  fi
fi

DB_HOST="${DB_HOST:-localhost}"
DB_PORT="${DB_PORT:-3306}"
DB_NAME="${DB_NAME:-${AETHER_DB_NAME:-${METEOR_DB_NAME:-ffxiv_server}}}"
DB_APP_HOST="${DB_APP_HOST:-${AETHER_DB_HOST:-${METEOR_DB_HOST:-127.0.0.1}}}"
DB_APP_PORT="${DB_APP_PORT:-${AETHER_DB_PORT:-${METEOR_DB_PORT:-3306}}}"
DB_APP_USER="${DB_APP_USER:-${AETHER_DB_USER:-${METEOR_DB_USER:-aetherxiv}}}"
DB_APP_PASS="${DB_APP_PASS:-${AETHER_DB_PASS:-${METEOR_DB_PASS:-aether_dev}}}"
DB_ADMIN_USER="${DB_ADMIN_USER:-${DB_USER:-}}"
DB_ADMIN_PASS="${DB_ADMIN_PASS:-${DB_PASS:-}}"
DB_ADMIN_SUDO="${DB_ADMIN_SUDO:-0}"

if [[ -z "$DB_ADMIN_USER" && "$DB_ADMIN_SUDO" != "1" ]]; then
  DB_HOST="$DB_APP_HOST"
  DB_PORT="$DB_APP_PORT"
  DB_ADMIN_USER="$DB_APP_USER"
  DB_ADMIN_PASS="$DB_APP_PASS"
fi

if [[ ! -d "$MIGRATIONS_DIR" ]]; then
  echo "Migration directory not found: $MIGRATIONS_DIR" >&2
  exit 2
fi

if ! command -v "$MYSQL_BIN" >/dev/null 2>&1; then
  echo "MariaDB/MySQL client not found: $MYSQL_BIN" >&2
  exit 2
fi

mysql_args=(-h "$DB_HOST" -u "$DB_ADMIN_USER")
if [[ "$DB_HOST" != "localhost" ]]; then
  mysql_args+=(-P "$DB_PORT")
fi
if [[ -n "$DB_ADMIN_PASS" ]]; then
  mysql_args+=("-p$DB_ADMIN_PASS")
fi

mysql_cmd=()
if [[ "$DB_ADMIN_SUDO" == "1" ]]; then
  mysql_cmd+=(sudo)
fi
mysql_cmd+=("$MYSQL_BIN")

mysql_exec() {
  "${mysql_cmd[@]}" "${mysql_args[@]}" "$DB_NAME" "$@"
}

mysql_scalar() {
  "${mysql_cmd[@]}" "${mysql_args[@]}" "$DB_NAME" -N -B -e "$1"
}

sql_escape_literal() {
  printf "%s" "$1" | sed "s/'/''/g"
}

sha256_file() {
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print tolower($1)}'
  elif command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print tolower($1)}'
  else
    echo "Neither shasum nor sha256sum was found; cannot checksum migrations." >&2
    exit 2
  fi
}

ensure_migration_ledger() {
  mysql_exec <<'SQL'
CREATE TABLE IF NOT EXISTS `aether_schema_migrations` (
  `migration_name` varchar(255) NOT NULL,
  `checksum_sha256` char(64) NOT NULL,
  `applied_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`migration_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;
SQL
}

get_applied_migration_checksum() {
  local migration_name_sql
  migration_name_sql="$(sql_escape_literal "$1")"
  mysql_scalar "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name = '$migration_name_sql' LIMIT 1;"
}

record_applied_migration() {
  local migration_name_sql checksum_sql
  migration_name_sql="$(sql_escape_literal "$1")"
  checksum_sql="$(sql_escape_literal "$2")"
  mysql_scalar "INSERT INTO aether_schema_migrations (migration_name, checksum_sha256) VALUES ('$migration_name_sql', '$checksum_sql');" >/dev/null
}

echo "Applying AetherXIV DB migrations to $DB_NAME from $MIGRATIONS_DIR"
ensure_migration_ledger
applied=0
skipped=0
for sql_file in "$MIGRATIONS_DIR"/*.sql; do
  [[ -e "$sql_file" ]] || continue
  migration_name="$(basename "$sql_file")"
  checksum="$(sha256_file "$sql_file")"
  recorded_checksum="$(get_applied_migration_checksum "$migration_name")"

  if [[ -n "$recorded_checksum" ]]; then
    if [[ "$recorded_checksum" != "$checksum" ]]; then
      echo "Warning: skipping $migration_name: already applied with checksum $recorded_checksum, but current checksum is $checksum." >&2
    else
      echo "Skipping $migration_name: already applied."
    fi
    skipped=$((skipped + 1))
    continue
  fi

  echo "Applying $migration_name"
  mysql_exec < "$sql_file"
  record_applied_migration "$migration_name" "$checksum"
  applied=$((applied + 1))
done
echo "Database migrations complete: $applied applied, $skipped skipped."
