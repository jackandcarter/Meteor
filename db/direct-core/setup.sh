#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BASELINE_FILE="${SCRIPT_DIR}/ffxiv_server.sql"
MIGRATIONS_DIR="${SCRIPT_DIR}/migrations"
BACKUP_DIR="${AETHERXIV_CORE_BACKUP_DIR:-${HOME}/.aetherxiv/backups/database}"

DB_HOST="${AETHERXIV_DB_HOST:-127.0.0.1}"
DB_PORT="${AETHERXIV_DB_PORT:-3306}"
DB_NAME="${AETHERXIV_DB_NAME:-ffxiv_server}"
DB_APP_USER="${AETHERXIV_DB_USER:-aetherxiv}"
DB_APP_PASS="${AETHERXIV_DB_PASSWORD:-aether_dev}"
DB_ADMIN_USER="${AETHERXIV_DB_ADMIN_USER:-root}"
DB_ADMIN_PASS="${AETHERXIV_DB_ADMIN_PASSWORD:-}"
MODE=setup
DROP_DATABASE=0
CLEAN_MIGRATE=0
MIGRATE_ONLY=0

while (($#)); do
  case "$1" in
    --check) MODE=check ;;
    --drop) DROP_DATABASE=1 ;;
    --clean-migrate) CLEAN_MIGRATE=1 ;;
    --migrate-only) MIGRATE_ONLY=1 ;;
    -h|--help)
      echo "Usage: setup.sh [--check] [--drop] [--clean-migrate] [--migrate-only]"
      exit 0
      ;;
    *) echo "Unknown argument: $1" >&2; exit 2 ;;
  esac
  shift
done

((MIGRATE_ONLY == 0 || (DROP_DATABASE == 0 && CLEAN_MIGRATE == 0))) || {
  echo "--migrate-only cannot be combined with --drop or --clean-migrate." >&2
  exit 2
}

resolve_tool() {
  local configured="$1"
  shift
  local candidate directory
  if [[ -n "${configured}" ]]; then
    if [[ "${configured}" == */* && -x "${configured}" ]]; then
      printf '%s\n' "${configured}"
      return 0
    fi
    if candidate="$(command -v "${configured}" 2>/dev/null)"; then
      printf '%s\n' "${candidate}"
      return 0
    fi
    return 1
  fi

  for candidate in "$@"; do
    if directory="$(command -v "${candidate}" 2>/dev/null)"; then
      printf '%s\n' "${directory}"
      return 0
    fi
    for directory in \
      /usr/local/bin \
      /usr/local/opt/mariadb/bin \
      /opt/homebrew/bin \
      /opt/homebrew/opt/mariadb/bin \
      /opt/local/bin; do
      if [[ -x "${directory}/${candidate}" ]]; then
        printf '%s\n' "${directory}/${candidate}"
        return 0
      fi
    done
  done
  return 1
}

if ! MYSQL_BIN="$(resolve_tool "${MYSQL_BIN:-}" mariadb mysql)"; then
  echo "MariaDB/MySQL client is required. Checked PATH and standard Homebrew/MacPorts locations." >&2
  exit 2
fi
if ! MYSQLDUMP_BIN="$(resolve_tool "${MYSQLDUMP_BIN:-}" mariadb-dump mysqldump)"; then
  echo "MariaDB/MySQL dump client is required. Checked PATH and standard Homebrew/MacPorts locations." >&2
  exit 2
fi

admin=("${MYSQL_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_ADMIN_USER}")
app=("${MYSQL_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_APP_USER}")
admin_dump=("${MYSQLDUMP_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_ADMIN_USER}")
app_dump=("${MYSQLDUMP_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_APP_USER}")
[[ -z "${DB_ADMIN_PASS}" ]] || admin+=("-p${DB_ADMIN_PASS}")
[[ -z "${DB_ADMIN_PASS}" ]] || admin_dump+=("-p${DB_ADMIN_PASS}")
[[ -z "${DB_APP_PASS}" ]] || app+=("-p${DB_APP_PASS}")
[[ -z "${DB_APP_PASS}" ]] || app_dump+=("-p${DB_APP_PASS}")
if ((MIGRATE_ONLY == 1)); then
  admin=("${app[@]}")
  dump=("${app_dump[@]}")
else
  dump=("${admin_dump[@]}")
fi

literal() { local value="$1"; value="${value//\\/\\\\}"; value="${value//\'/\'\'}"; printf '%s' "$value"; }
identifier() { local value="$1"; value="${value//\`/\`\`}"; printf '%s' "$value"; }

verify_database() {
  local database_literal table missing=()
  database_literal="$(literal "${DB_NAME}")"
  "${app[@]}" "${DB_NAME}" -e "SELECT 1" >/dev/null
  local required=(users sessions servers characters characters_appearance characters_quest_scenario
    characters_quest_completed characters_hotbar server_sessions server_zones server_zones_privateareas
    server_battlenpc_spawn_locations server_battlenpc_spawn_audit_pins server_battle_commands
    server_player_base_stats characters_class_attributes server_spawn_locations gamedata_actor_class
    gamedata_actor_appearance server_items_modifiers characters_inventory characters_chocobo
    server_npc_spawn_evidence server_npc_spawn_evidence_catalog launcher_config aether_database_compatibility
    launcher_status launcher_news launcher_patch_files launcher_presentation launcher_reel_text)
  for table in "${required[@]}"; do
    [[ "$("${app[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${database_literal}' AND table_name='${table}'")" == 1 ]] || missing+=("${table}")
  done
  ((${#missing[@]} == 0)) || { echo "Database schema is incomplete: ${missing[*]}" >&2; return 21; }
  local zones commands stats launcher_columns
  zones="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM server_zones")"
  commands="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM server_battle_commands")"
  stats="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM server_player_base_stats")"
  launcher_columns="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM information_schema.columns WHERE table_schema=DATABASE() AND table_name='launcher_config' AND column_name IN ('service_version','server_name','is_active')")"
  [[ "${zones}" != 0 && "${commands}" != 0 && "${stats}" != 0 && "${launcher_columns}" == 3 ]] || {
    echo "Database seed/launcher verification failed: zones=${zones} commands=${commands} stats=${stats} launcherColumns=${launcher_columns}" >&2
    return 22
  }
  local npc_service_contract
  npc_service_contract="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT CONCAT(COUNT(*),':',COALESCE(MAX(version),''),':',COALESCE(MAX(contentHashSha256),''),':',COALESCE(MAX(recordCount),0)) FROM server_npc_spawn_evidence_catalog WHERE catalogId='zone-service-npcs-1.23b'")"
  [[ "${npc_service_contract}" == "1:2026.07.19.1:f40276dea0ce6739b40d0dca3dc44f665ee525646851592a9439d5013f97b8de:23" ]] || {
    echo "NPC service seed contract mismatch: ${npc_service_contract:-missing}" >&2
    return 28
  }
  local central_shroud_pinspawn_contract
  central_shroud_pinspawn_contract="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT CONCAT((SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND createdByCharacterName='Akhebica Loha' AND promotionNote LIKE 'Source dump pin #%'),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND createdByCharacterName='Akhebica Loha' AND isPromoted=1 AND enemyName='Star Marmot' AND promotionMigration='20260718_000013_central_shroud_pinspawn_restore'),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_locations s JOIN server_battlenpc_groups g ON g.groupId=s.groupId JOIN server_battlenpc_pools p ON p.poolId=g.poolId WHERE s.bnpcId IN (1500001,1500002,1500034,1500035,1500039,1500041,1500042,1500051,1500055,1500056,1500057,1500058,1500060) AND g.zoneId=150 AND g.minLevel=3 AND g.maxLevel=4 AND g.hp=99 AND g.mp=130 AND p.actorClassId IN (2104009,2104028) AND p.genusId=12),':',(SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE zoneId=150 AND promotionNote LIKE 'Source dump pin #%' AND isPromoted=1 AND enemyName<>'Star Marmot'))")"
  [[ "${central_shroud_pinspawn_contract}" == "60:11:13:0" ]] || {
    echo "Central Shroud pinspawn contract mismatch: ${central_shroud_pinspawn_contract:-missing}" >&2
    return 29
  }
  local contract
  contract="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT CONCAT(schema_generation,':',schema_version,':',compatibility_id,':',baseline_id) FROM aether_database_compatibility WHERE compatibility_key='direct-core' LIMIT 1")"
  [[ "${contract}" == "2:1:aetherxiv-direct-core-v2:20260716_000001_ffxiv_server_v2_baseline" ]] || {
    echo "Database compatibility mismatch: ${contract:-missing}" >&2
    return 24
  }
  echo "Direct-core database verified: ${DB_NAME} (zones=${zones} commands=${commands} baseStats=${stats})"
}

verify_migration_candidate() {
  local database_literal table missing=()
  database_literal="$(literal "${DB_NAME}")"
  local required=(users characters server_zones server_battle_commands server_player_base_stats)
  for table in "${required[@]}"; do
    [[ "$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${database_literal}' AND table_name='${table}'")" == 1 ]] || missing+=("${table}")
  done
  ((${#missing[@]} == 0)) || {
    echo "The configured database is not a recognizable AetherXIV direct-core database: missing ${missing[*]}" >&2
    return 25
  }
}

if [[ "${MODE}" == check ]]; then verify_database; exit $?; fi
[[ -f "${BASELINE_FILE}" && -d "${MIGRATIONS_DIR}" ]] || { echo "Database package is incomplete." >&2; exit 2; }
"${admin[@]}" -e "SELECT 1" >/dev/null

db_id="$(identifier "${DB_NAME}")"
db_literal="$(literal "${DB_NAME}")"
user_literal="$(literal "${DB_APP_USER}")"
pass_literal="$(literal "${DB_APP_PASS}")"
exists="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.schemata WHERE schema_name='${db_literal}'")"

LAST_BACKUP_PATH=""
backup_database() {
  command -v "${MYSQLDUMP_BIN}" >/dev/null 2>&1 || { echo "Database dump client is required before modifying an existing database." >&2; exit 2; }
  mkdir -p "${BACKUP_DIR}"
  local path="${BACKUP_DIR}/${DB_NAME}-$(date -u +'%Y%m%dT%H%M%SZ').sql"
  "${dump[@]}" --routines --triggers --single-transaction "${DB_NAME}" > "${path}"
  shasum -a 256 "${path}" > "${path}.sha256"
  LAST_BACKUP_PATH="${path}"
  echo "Backed up ${DB_NAME} to ${path}"
}

BASELINE_IMPORTED=0
clean_migrate_database() {
  [[ "${exists}" == 1 ]] || { echo "Clean migration requires an existing database." >&2; exit 2; }
  local user_table_count character_table_count
  user_table_count="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='users'")"
  character_table_count="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='characters'")"
  [[ "${user_table_count}" == 1 && "${character_table_count}" == 1 ]] || {
    echo "The configured database is not a recognizable AetherXIV direct-core database; automatic player migration was refused." >&2
    exit 25
  }

  backup_database
  local migration_data="${LAST_BACKUP_PATH%.sql}-player-data.sql"
  local users_before characters_before users_after characters_after table
  users_before="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM users")"
  characters_before="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM characters")"
  local candidates=(
    users characters reserved_names server_linkshells server_retainers
    supportdesk_issues supportdesk_tickets launcher_config launcher_status launcher_news
    launcher_presentation launcher_reel_text
  )
  while IFS= read -r table; do candidates+=("${table}"); done < <(
    "${admin[@]}" -N -B -e "SELECT table_name FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name LIKE 'characters\\_%' ESCAPE '\\\\' ORDER BY table_name"
  )
  local preserved=()
  for table in "${candidates[@]}"; do
    [[ "$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='$(literal "${table}")'")" == 1 ]] || continue
    [[ " ${preserved[*]} " == *" ${table} "* ]] || preserved+=("${table}")
  done
  ((${#preserved[@]} > 0)) || { echo "No player tables were available to migrate." >&2; exit 25; }
  "${dump[@]}" --single-transaction --no-create-info --complete-insert --replace --hex-blob --skip-triggers "${DB_NAME}" "${preserved[@]}" > "${migration_data}"
  shasum -a 256 "${migration_data}" > "${migration_data}.sha256"

  set +e
  "${admin[@]}" -e "DROP DATABASE \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci" \
    && "${admin[@]}" "${DB_NAME}" < "${BASELINE_FILE}" \
    && "${admin[@]}" "${DB_NAME}" < "${migration_data}"
  local migration_status=$?
  set -e
  if ((migration_status != 0)); then
    echo "Player migration failed; restoring the untouched full backup." >&2
    "${admin[@]}" -e "DROP DATABASE IF EXISTS \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci"
    "${admin[@]}" "${DB_NAME}" < "${LAST_BACKUP_PATH}"
    exit 26
  fi

  users_after="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM users")"
  characters_after="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM characters")"
  if [[ "${users_before}" != "${users_after}" || "${characters_before}" != "${characters_after}" ]]; then
    echo "Player migration count mismatch; restoring the untouched full backup." >&2
    "${admin[@]}" -e "DROP DATABASE IF EXISTS \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci"
    "${admin[@]}" "${DB_NAME}" < "${LAST_BACKUP_PATH}"
    exit 27
  fi
  BASELINE_IMPORTED=1
  echo "Migrated ${users_after} accounts and ${characters_after} characters into the AetherXIV 2 baseline. Player-data copy: ${migration_data}"
}

if ((MIGRATE_ONLY == 1)); then
  [[ "${exists}" == 1 ]] || {
    echo "The configured database does not exist; administrative setup is required." >&2
    exit 25
  }
  verify_migration_candidate
  backup_database
  echo "Applying pending migrations with configured account ${DB_APP_USER}."
else
  if [[ "${CLEAN_MIGRATE}" == 1 ]]; then
    clean_migrate_database
  fi

  if [[ "${exists}" == 1 && "${DROP_DATABASE}" == 1 ]]; then
    backup_database
    "${admin[@]}" -e "DROP DATABASE \`${db_id}\`"
    exists=0
  fi

  "${admin[@]}" -e "CREATE DATABASE IF NOT EXISTS \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci"
  for host in localhost 127.0.0.1; do
    host_literal="$(literal "${host}")"
    "${admin[@]}" -e "CREATE USER IF NOT EXISTS '${user_literal}'@'${host_literal}' IDENTIFIED BY '${pass_literal}'; ALTER USER '${user_literal}'@'${host_literal}' IDENTIFIED BY '${pass_literal}'; GRANT ALL PRIVILEGES ON \`${db_id}\`.* TO '${user_literal}'@'${host_literal}'"
  done
  "${admin[@]}" -e "FLUSH PRIVILEGES"

  if [[ "${BASELINE_IMPORTED}" == 1 ]]; then
    echo "Canonical baseline and preserved player data are installed in ${DB_NAME}"
  elif [[ "${exists}" == 0 ]]; then
    echo "Importing canonical direct-core baseline into ${DB_NAME}"
    "${admin[@]}" "${DB_NAME}" < "${BASELINE_FILE}"
  else
    verify_migration_candidate
    backup_database
  fi
fi

"${admin[@]}" "${DB_NAME}" -e "CREATE TABLE IF NOT EXISTS aether_schema_migrations (migration_name varchar(255) NOT NULL, checksum_sha256 char(64) NOT NULL, applied_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (migration_name)) ENGINE=InnoDB DEFAULT CHARSET=utf8"
baseline_checksum="$(shasum -a 256 "${BASELINE_FILE}" | awk '{print tolower($1)}')"
recorded_baseline="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='baseline/20260716_000001_ffxiv_server_v2' LIMIT 1")"
if ((MIGRATE_ONLY == 1)) && [[ -z "${recorded_baseline}" ]]; then
  echo "The existing database has no recorded direct-core baseline; administrative repair is required." >&2
  exit 23
fi
if ((MIGRATE_ONLY == 0)) && [[ -n "${recorded_baseline}" && "${recorded_baseline}" != "${baseline_checksum}" ]]; then
  echo "Baseline checksum mismatch." >&2; exit 23
fi
if [[ -z "${recorded_baseline}" ]]; then
  "${admin[@]}" "${DB_NAME}" -e "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('baseline/20260716_000001_ffxiv_server_v2','${baseline_checksum}')"
fi

for migration in "${MIGRATIONS_DIR}"/*.sql; do
  [[ -e "${migration}" ]] || continue
  name="$(basename "${migration}")"
  checksum="$(shasum -a 256 "${migration}" | awk '{print tolower($1)}')"
  recorded="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='${name}' LIMIT 1")"
  if [[ -n "${recorded}" ]]; then
    if [[ "${recorded}" != "${checksum}" ]]; then
      if ((MIGRATE_ONLY == 1)); then
        echo "Warning: already-applied migration file differs from its recorded checksum and will not be reapplied: ${name}" >&2
      else
        echo "Migration checksum mismatch: ${name}" >&2
        exit 23
      fi
    fi
    continue
  fi
  echo "Applying ${name}"
  "${admin[@]}" "${DB_NAME}" < "${migration}"
  "${admin[@]}" "${DB_NAME}" -e "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('${name}','${checksum}')"
done

verify_database
