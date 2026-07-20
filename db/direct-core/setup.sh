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
DB_ALLOWED_HOSTS="${AETHERXIV_DB_ALLOWED_HOSTS:-localhost,127.0.0.1}"
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
MYSQLDUMP_BIN="$(resolve_tool "${MYSQLDUMP_BIN:-}" mariadb-dump mysqldump || true)"

admin=("${MYSQL_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_ADMIN_USER}")
app=("${MYSQL_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_APP_USER}")
admin_dump=()
app_dump=()
if [[ -n "${MYSQLDUMP_BIN}" ]]; then
  admin_dump=("${MYSQLDUMP_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_ADMIN_USER}")
  app_dump=("${MYSQLDUMP_BIN}" -h "${DB_HOST}" -P "${DB_PORT}" -u "${DB_APP_USER}")
  [[ -z "${DB_ADMIN_PASS}" ]] || admin_dump+=("-p${DB_ADMIN_PASS}")
  [[ -z "${DB_APP_PASS}" ]] || app_dump+=("-p${DB_APP_PASS}")
fi
[[ -z "${DB_ADMIN_PASS}" ]] || admin+=("-p${DB_ADMIN_PASS}")
[[ -z "${DB_APP_PASS}" ]] || app+=("-p${DB_APP_PASS}")
dump=()
if ((MIGRATE_ONLY == 1)); then
  admin=("${app[@]}")
  [[ -z "${MYSQLDUMP_BIN}" ]] || dump=("${app_dump[@]}")
else
  [[ -z "${MYSQLDUMP_BIN}" ]] || dump=("${admin_dump[@]}")
fi

literal() { local value="$1"; value="${value//\\/\\\\}"; value="${value//\'/\'\'}"; printf '%s' "$value"; }
identifier() { local value="$1"; value="${value//\`/\`\`}"; printf '%s' "$value"; }
sha256_file() {
  if command -v shasum >/dev/null 2>&1; then
    shasum -a 256 "$1" | awk '{print tolower($1)}'
  elif command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | awk '{print tolower($1)}'
  else
    echo "A SHA-256 tool is required (shasum or sha256sum)." >&2
    return 2
  fi
}
write_sha256_sidecar() {
  local path="$1"
  printf '%s  %s\n' "$(sha256_file "${path}")" "$(basename "${path}")" > "${path}.sha256"
}

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
  local gridania_tutorial_actors
  gridania_tutorial_actors="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT CONCAT((SELECT COUNT(*) FROM server_battlenpc_pools WHERE poolId=3 AND name='yda' AND actorClassId=2290006),':',(SELECT COUNT(*) FROM server_battlenpc_pools WHERE poolId=4 AND name='papalymo' AND actorClassId=2290005))")"
  [[ "${gridania_tutorial_actors}" == "1:1" ]] || {
    echo "Gridania tutorial actor-role contract mismatch: ${gridania_tutorial_actors:-missing}" >&2
    return 30
  }
  local contract
  contract="$("${app[@]}" -N -B "${DB_NAME}" -e "SELECT CONCAT(schema_generation,':',schema_version,':',compatibility_id,':',baseline_id) FROM aether_database_compatibility WHERE compatibility_key='direct-core' LIMIT 1")"
  [[ "${contract}" == "2:1:aetherxiv-direct-core-v2:20260716_000001_ffxiv_server_v2_baseline" ]] || {
    echo "Database compatibility mismatch: ${contract:-missing}" >&2
    return 24
  }
  echo "Direct-core database verified: ${DB_NAME} (zones=${zones} commands=${commands} baseStats=${stats})"
}

has_current_v2_contract() {
  [[ "$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='aether_database_compatibility'")" == 1 ]] || return 1
  [[ "$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM aether_database_compatibility WHERE compatibility_key='direct-core' AND schema_generation=2 AND schema_version=1 AND compatibility_id='aetherxiv-direct-core-v2' AND baseline_id='20260716_000001_ffxiv_server_v2_baseline'")" == 1 ]]
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
ORIGINAL_BACKUP_PATH="${AETHERXIV_ORIGINAL_BACKUP_PATH:-}"
backup_database() {
  mkdir -p "${BACKUP_DIR}"
  local base_path path suffix=0 table_count
  base_path="${BACKUP_DIR}/${DB_NAME}-$(date -u +'%Y%m%dT%H%M%SZ')"
  path="${base_path}.sql"
  while [[ -e "${path}" || -e "${path}.sha256" ]]; do
    suffix=$((suffix + 1))
    path="${base_path}-${suffix}.sql"
  done
  table_count="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}'")"
  if [[ "${table_count}" == 0 ]]; then
    printf '%s\n' "-- AetherXIV backup: ${DB_NAME} existed and contained no tables." > "${path}"
  else
    [[ -n "${MYSQLDUMP_BIN}" && -x "${MYSQLDUMP_BIN}" ]] || {
      echo "MariaDB/MySQL dump client is required to preserve the existing non-empty database before rebuilding it." >&2
      exit 2
    }
    "${dump[@]}" --routines --triggers --single-transaction "${DB_NAME}" > "${path}"
  fi
  write_sha256_sidecar "${path}"
  LAST_BACKUP_PATH="${path}"
  if [[ -z "${ORIGINAL_BACKUP_PATH}" ]]; then
    ORIGINAL_BACKUP_PATH="${path}"
  fi
  echo "Backed up ${DB_NAME} to ${path}"
}

restore_original_database() {
  if [[ -n "${ORIGINAL_BACKUP_PATH}" && -f "${ORIGINAL_BACKUP_PATH}" ]]; then
    echo "Restoring the untouched database backup: ${ORIGINAL_BACKUP_PATH}" >&2
    "${admin[@]}" -e "DROP DATABASE IF EXISTS \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci"
    "${admin[@]}" "${DB_NAME}" < "${ORIGINAL_BACKUP_PATH}"
  else
    echo "No original database existed; removing the incomplete new database so setup can be retried." >&2
    "${admin[@]}" -e "DROP DATABASE IF EXISTS \`${db_id}\`"
  fi
}

BASELINE_IMPORTED=0
clean_migrate_database() {
  [[ "${exists}" == 1 ]] || { echo "Clean migration requires an existing database." >&2; exit 2; }
  local user_table_count character_table_count
  user_table_count="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='users'")"
  character_table_count="$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='characters'")"
  backup_database
  local migration_data="" users_before=0 characters_before=0 table
  local can_restore_players=0
  if [[ "${user_table_count}" == 1 && "${character_table_count}" == 1 ]]; then
    can_restore_players=1
    migration_data="${LAST_BACKUP_PATH%.sql}-player-data.sql"
    users_before="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM users")"
    characters_before="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM characters")"
    local candidates=(users characters)
    while IFS= read -r table; do candidates+=("${table}"); done < <(
      "${admin[@]}" -N -B -e "SELECT table_name FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name LIKE 'characters\\_%' ESCAPE '\\\\' ORDER BY table_name"
    )
    local preserved=() existing_table already_preserved
    for table in "${candidates[@]}"; do
      [[ "$("${admin[@]}" -N -B -e "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='${db_literal}' AND table_name='$(literal "${table}")'")" == 1 ]] || continue
      already_preserved=0
      for existing_table in "${preserved[@]-}"; do
        [[ "${existing_table}" == "${table}" ]] && already_preserved=1 && break
      done
      ((already_preserved == 1)) || preserved+=("${table}")
    done
    "${dump[@]}" --single-transaction --no-create-info --complete-insert --replace --hex-blob --skip-triggers "${DB_NAME}" "${preserved[@]}" > "${migration_data}"
    write_sha256_sidecar "${migration_data}"
  else
    echo "No complete users/characters pair was found; installing a fresh canonical database. The full backup is retained for manual recovery."
  fi

  set +e
  "${admin[@]}" -e "DROP DATABASE \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci" \
    && "${admin[@]}" "${DB_NAME}" < "${BASELINE_FILE}"
  local baseline_status=$?
  set -e
  if ((baseline_status != 0)); then
    echo "Canonical database installation failed; restoring the untouched full backup." >&2
    restore_original_database
    exit 26
  fi

  if ((can_restore_players == 1)); then
    set +e
    "${admin[@]}" "${DB_NAME}" < "${migration_data}"
    local migration_status=$?
    local users_after=0 characters_after=0
    if ((migration_status == 0)); then
      users_after="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM users")"
      characters_after="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT COUNT(*) FROM characters")"
      [[ "${users_before}" == "${users_after}" && "${characters_before}" == "${characters_after}" ]] || migration_status=27
    fi
    set -e
    if ((migration_status == 0)); then
      echo "Migrated ${users_after} accounts and ${characters_after} characters into the AetherXIV 2 baseline. Player-data copy: ${migration_data}"
    else
      echo "Player data was incompatible with the canonical schema; keeping the fresh database. The full backup and player-data copy are retained." >&2
      set +e
      "${admin[@]}" -e "DROP DATABASE IF EXISTS \`${db_id}\`; CREATE DATABASE \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci" \
        && "${admin[@]}" "${DB_NAME}" < "${BASELINE_FILE}"
      local rebuild_status=$?
      set -e
      if ((rebuild_status != 0)); then
        echo "Fresh database recovery failed; restoring the untouched full backup." >&2
        restore_original_database
        exit 27
      fi
    fi
  fi
  BASELINE_IMPORTED=1
  echo "Canonical AetherXIV 2 database installed. Full backup: ${LAST_BACKUP_PATH}"
}

ensure_application_account() {
  IFS=',' read -r -a allowed_hosts <<< "${DB_ALLOWED_HOSTS}"
  ((${#allowed_hosts[@]} > 0)) || { echo "At least one database application host is required." >&2; return 2; }
  local host host_literal
  for host in "${allowed_hosts[@]}"; do
    host="${host#"${host%%[![:space:]]*}"}"
    host="${host%"${host##*[![:space:]]}"}"
    [[ -n "${host}" ]] || { echo "Database application hosts cannot be empty." >&2; return 2; }
    host_literal="$(literal "${host}")"
    "${admin[@]}" -e "CREATE USER IF NOT EXISTS '${user_literal}'@'${host_literal}' IDENTIFIED BY '${pass_literal}'; ALTER USER '${user_literal}'@'${host_literal}' IDENTIFIED BY '${pass_literal}'; GRANT ALL PRIVILEGES ON \`${db_id}\`.* TO '${user_literal}'@'${host_literal}'"
  done
  "${admin[@]}" -e "FLUSH PRIVILEGES"
}

if ((MIGRATE_ONLY == 1)); then
  [[ "${exists}" == 1 ]] || {
    echo "The configured database does not exist; administrative setup is required." >&2
    exit 25
  }
  has_current_v2_contract || {
    echo "The configured database is not an AetherXIV 2 database; administrator-assisted setup is required." >&2
    exit 25
  }
  backup_database
  echo "Applying pending migrations with configured account ${DB_APP_USER}."
else
  if [[ "${CLEAN_MIGRATE}" == 1 && "${exists}" == 1 ]]; then
    clean_migrate_database
  fi

  if [[ "${exists}" == 1 && "${DROP_DATABASE}" == 1 ]]; then
    backup_database
    "${admin[@]}" -e "DROP DATABASE \`${db_id}\`"
    exists=0
  fi

  "${admin[@]}" -e "CREATE DATABASE IF NOT EXISTS \`${db_id}\` CHARACTER SET utf8 COLLATE utf8_general_ci"
  ensure_application_account

  if [[ "${BASELINE_IMPORTED}" == 1 ]]; then
    echo "Canonical baseline is installed in ${DB_NAME}"
  elif [[ "${exists}" == 0 ]]; then
    echo "Importing canonical direct-core baseline into ${DB_NAME}"
    if ! "${admin[@]}" "${DB_NAME}" < "${BASELINE_FILE}"; then
      echo "Canonical database installation failed." >&2
      restore_original_database
      exit 26
    fi
    BASELINE_IMPORTED=1
  elif has_current_v2_contract; then
    echo "Existing AetherXIV 2 database detected; checking its migration ledger and canonical schema."
    backup_database
  else
    echo "Existing database is empty or predates AetherXIV 2; preserving a full backup before installing the canonical database."
    clean_migrate_database
    ensure_application_account
  fi
fi

apply_migrations() {
  "${admin[@]}" "${DB_NAME}" -e "CREATE TABLE IF NOT EXISTS aether_schema_migrations (migration_name varchar(255) NOT NULL, checksum_sha256 char(64) NOT NULL, applied_at timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP, PRIMARY KEY (migration_name)) ENGINE=InnoDB DEFAULT CHARSET=utf8"
  local baseline_checksum recorded_baseline migration name checksum recorded
  baseline_checksum="$(sha256_file "${BASELINE_FILE}")"
  recorded_baseline="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='baseline/20260716_000001_ffxiv_server_v2' LIMIT 1")"
  if ((MIGRATE_ONLY == 1)) && [[ -z "${recorded_baseline}" ]]; then
    echo "The existing database has no recorded AetherXIV 2 baseline; an administrator-assisted repair is required." >&2
    return 23
  fi
  if ((MIGRATE_ONLY == 0)) && [[ -n "${recorded_baseline}" && "${recorded_baseline}" != "${baseline_checksum}" ]]; then
    echo "The recorded baseline checksum is stale; rebuilding from the packaged canonical database." >&2
    return 23
  fi
  if [[ -z "${recorded_baseline}" ]]; then
    "${admin[@]}" "${DB_NAME}" -e "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('baseline/20260716_000001_ffxiv_server_v2','${baseline_checksum}')"
  fi

  for migration in "${MIGRATIONS_DIR}"/*.sql; do
    [[ -e "${migration}" ]] || continue
    name="$(basename "${migration}")"
    checksum="$(sha256_file "${migration}")"
    recorded="$("${admin[@]}" -N -B "${DB_NAME}" -e "SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='${name}' LIMIT 1")"
    if [[ -n "${recorded}" ]]; then
      if [[ "${recorded}" != "${checksum}" ]]; then
        if ((MIGRATE_ONLY == 1)); then
          echo "Warning: already-applied migration file differs from its recorded checksum and will not be reapplied: ${name}" >&2
        else
          echo "Migration checksum mismatch: ${name}" >&2
          return 23
        fi
      fi
      continue
    fi
    echo "Applying ${name}"
    "${admin[@]}" "${DB_NAME}" < "${migration}"
    "${admin[@]}" "${DB_NAME}" -e "INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('${name}','${checksum}')"
  done
}

migration_status=0
apply_migrations || migration_status=$?
if ((migration_status == 0)) && verify_database; then
  exit 0
fi

if ((MIGRATE_ONLY == 1)); then
  echo "The AetherXIV 2 database needs an administrator-assisted canonical repair." >&2
  ((migration_status != 0)) || migration_status=30
  exit "${migration_status}"
fi
if ((BASELINE_IMPORTED == 1)); then
  echo "The freshly installed canonical database did not pass verification; the packaged database may be incomplete." >&2
  restore_original_database
  ((migration_status != 0)) || migration_status=30
  exit "${migration_status}"
fi

echo "The existing AetherXIV 2 schema is incomplete or stale; preserving it and rebuilding the canonical schema."
clean_migrate_database
ensure_application_account
apply_migrations
verify_database
