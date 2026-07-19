#!/bin/bash
# First-boot seed script run by the mariadb image's entrypoint from
# /docker-entrypoint-initdb.d. Imports only the TOP-LEVEL *.sql files from
# the read-only /seed mount (Data/sql), in the same alphabetical order as
# tools/setup-local-db.sh, deliberately skipping Data/sql's helper scripts
# (import.sh/export.sh) and the migrations/ subdir (applied by db-init).
set -euo pipefail

database="${MARIADB_DATABASE:-ffxiv_server}"

for f in /seed/*.sql; do
  echo "db-seed: importing ${f}"
  mariadb --protocol=socket -uroot -p"${MARIADB_ROOT_PASSWORD:?}" "${database}" < "${f}"
done

echo "db-seed: base seed complete"
