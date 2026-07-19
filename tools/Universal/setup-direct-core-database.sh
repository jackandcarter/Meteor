#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
PACKAGE_DIR="${AETHERXIV_DATABASE_PACKAGE_DIR:-${ROOT_DIR}/bin/build/${CONFIGURATION}/Database}"

python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${PACKAGE_DIR}"

exec "${PACKAGE_DIR}/setup.sh" "$@"
