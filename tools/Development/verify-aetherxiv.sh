#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
DEV_WORK_ROOT="${AETHERXIV_DEV_WORK_ROOT:-${ROOT_DIR}/bin/build/.work/Verification}"
export AetherXivWorkRoot="${DEV_WORK_ROOT}"

cleanup() {
  rm -rf "${DEV_WORK_ROOT}"
  rmdir "${ROOT_DIR}/bin/build/.work" 2>/dev/null || true
}
trap cleanup EXIT

cleanup
"${DOTNET_BIN}" build "${ROOT_DIR}/AetherXIV.sln" \
  --configuration Release -m:1 /nodeReuse:false /p:NuGetAudit=false
# Several parity tests intentionally execute the authoritative source Lua. Make
# that source visible inside disposable test work without copying it into a
# release package or changing runtime lookup behavior.
ln -s "${ROOT_DIR}/Data" "${DEV_WORK_ROOT}/out/Data"
ln -s "${ROOT_DIR}/db" "${DEV_WORK_ROOT}/out/db"
ln -s "${ROOT_DIR}/tests" "${DEV_WORK_ROOT}/out/tests"
ln -s "${ROOT_DIR}/AetherXIV.sln" "${DEV_WORK_ROOT}/out/AetherXIV.sln"
"${DOTNET_BIN}" test "${ROOT_DIR}/AetherXIV.sln" \
  --configuration Release --no-build --nologo /p:NuGetAudit=false

"${DOTNET_BIN}" build "${ROOT_DIR}/AetherXIV Launcher/AetherXIV.Launcher.sln" \
  --configuration Release -m:1 /nodeReuse:false /p:NuGetAudit=false
"${DOTNET_BIN}" test "${ROOT_DIR}/AetherXIV Launcher/AetherXIV.Launcher.sln" \
  --configuration Release --no-build --nologo /p:NuGetAudit=false

echo "Development verification passed; no development artifacts were added to bin/build."
