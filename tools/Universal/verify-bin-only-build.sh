#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "${ROOT_DIR}"

TARGET_ROOT="${1:-}"
TARGET_CONFIGURATION=""
TARGET_PLATFORM=""
if [[ -n "${TARGET_ROOT}" ]]; then
  TARGET_ROOT="$(cd "${TARGET_ROOT}" && pwd)"
  case "${TARGET_ROOT}" in
    "${ROOT_DIR}/bin/build/Debug/"*|"${ROOT_DIR}/bin/build/Release/"*) ;;
    *) echo "Verification target is outside bin/build/{Debug,Release}: ${TARGET_ROOT}" >&2; exit 2 ;;
  esac
  TARGET_PLATFORM="$(basename "${TARGET_ROOT}")"
  TARGET_CONFIGURATION="$(basename "$(dirname "${TARGET_ROOT}")")"
fi

generated_dirs=()
while IFS= read -r path; do generated_dirs+=("${path}"); done < <(
  find src tests tools "AetherXIV Launcher" -type d \( -name bin -o -name obj \) -prune -print | sort
)
if ((${#generated_dirs[@]})); then
  echo "Project-local build directories were found; build artifacts belong under ${ROOT_DIR}/bin/build:" >&2
  printf '  %s\n' "${generated_dirs[@]}" >&2
  exit 1
fi

[[ -d "${ROOT_DIR}/bin/build" ]] || { echo "Build output is missing." >&2; exit 2; }
unexpected=()
while IFS= read -r path; do unexpected+=("${path}"); done < <(
  find "${ROOT_DIR}/bin" -mindepth 1 -maxdepth 1 ! -name build -print | sort
)
while IFS= read -r path; do unexpected+=("${path}"); done < <(
  find "${ROOT_DIR}/bin/build" -mindepth 1 -maxdepth 1 ! -name Debug ! -name Release -print | sort
)
if ((${#unexpected[@]})); then
  echo "Only bin/build/{Debug,Release} may remain after a build:" >&2
  printf '  %s\n' "${unexpected[@]}" >&2
  exit 3
fi

verify_file() { [[ -f "$1" ]] || { echo "Build is missing required file: $1" >&2; exit 6; }; }

configurations=(Debug Release)
[[ -z "${TARGET_CONFIGURATION}" ]] || configurations=("${TARGET_CONFIGURATION}")
for configuration in "${configurations[@]}"; do
  configuration_root="${ROOT_DIR}/bin/build/${configuration}"
  [[ -d "${configuration_root}" ]] || continue
  invalid_platforms=()
  while IFS= read -r path; do invalid_platforms+=("${path}"); done < <(
    find "${configuration_root}" -mindepth 1 -maxdepth 1 \
      ! -name MacOS ! -name Windows ! -name Linux ! -name SteamOS -print | sort
  )
  if ((${#invalid_platforms[@]})); then
    echo "Unexpected ${configuration} build entries:" >&2
    printf '  %s\n' "${invalid_platforms[@]}" >&2
    exit 4
  fi

  forbidden=()
  while IFS= read -r path; do forbidden+=("${path}"); done < <(
    find "${configuration_root}" -type f \( \
      -name '.DS_Store' \
      -o -iname '*Tests*.dll' -o -iname '*Tests*.exe' \
      -o -iname 'AetherXIV.Map' -o -iname 'AetherXIV.Map.dll' -o -iname 'AetherXIV.Map.exe' \
      -o -iname 'AetherXIV.World' -o -iname 'AetherXIV.World.dll' -o -iname 'AetherXIV.World.exe' \
      -o -iname 'AetherXIV.Lobby' -o -iname 'AetherXIV.Lobby.dll' -o -iname 'AetherXIV.Lobby.exe' \
      -o -iname 'AetherXIV.Scripting.dll' -o -iname 'AetherXIV.Compatibility.dll' \
      -o -iname 'AetherXIV.Map.Host*' -o -iname 'AetherXIV.World.Host*' \
      -o -iname 'AetherXIV.Lobby.Host*' \) -print | sort
  )
  if [[ "${configuration}" == Release ]]; then
    while IFS= read -r path; do forbidden+=("${path}"); done < <(
      find "${configuration_root}" -type f -iname '*.pdb' -print | sort
    )
  fi
  if ((${#forbidden[@]})); then
    echo "${configuration} contains test, symbol, or superseded server files:" >&2
    printf '  %s\n' "${forbidden[@]}" >&2
    exit 5
  fi

  for platform in MacOS Windows Linux SteamOS; do
    [[ -z "${TARGET_PLATFORM}" || "${platform}" == "${TARGET_PLATFORM}" ]] || continue
    platform_root="${configuration_root}/${platform}"
    [[ -d "${platform_root}" ]] || continue
    verify_file "${platform_root}/build-manifest.txt"
    grep -Fxq 'product_version=2.0' "${platform_root}/build-manifest.txt" || {
      echo "Build manifest has the wrong product version: ${platform_root}" >&2
      exit 9
    }
    expected_build_number="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
    grep -Fxq "build_number=${expected_build_number}" "${platform_root}/build-manifest.txt" || {
      echo "Build manifest has the wrong build number: ${platform_root}" >&2
      exit 9
    }
    verify_file "${platform_root}/Database/ffxiv_server.sql"
    verify_file "${platform_root}/Database/ffxiv_server.sql.sha256"
    verify_file "${platform_root}/Database/baseline-history.sha256"
    verify_file "${platform_root}/Database/baseline-manifest.json"
    verify_file "${platform_root}/Database/setup.sh"
    verify_file "${platform_root}/Database/setup.ps1"
    verify_file "${platform_root}/Database/migrations/20260627_battlenpc_spawn_audit_pins.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000003_launcher_local_identity.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000004_database_compatibility.sql"
    verify_file "${platform_root}/Database/migrations/20260716_000005_guildleve_content_contract.sql"
    verify_file "${platform_root}/Database/migrations/20260717_000006_central_shroud_enemy_restore.sql"
    verify_file "${platform_root}/Database/migrations/20260717_000007_character_attribute_allocations.sql"
    verify_file "${platform_root}/Database/migrations/20260718_000013_central_shroud_pinspawn_restore.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000016_gridania_tutorial_actor_roles.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000017_gridania_tutorial_spawn_contract.sql"
    verify_file "${platform_root}/Database/migrations/20260720_000018_gridania_tutorial_nameplates.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000019_gridania_man0g1_guilds.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000020_gridania_man0g1_growery.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000021_gridania_man0g1_escort_and_completion.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000022_gridania_man0g1_escort_balance.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000023_gridania_man0g1_escort_boundary.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000024_gridania_man0g1_escort_boundary_polarity.sql"
    verify_file "${platform_root}/Database/migrations/20260722_000025_gridania_man0g1_escort_actor_presentation.sql"
    grep -q 'CREATE TABLE IF NOT EXISTS server_battlenpc_spawn_audit_pins' \
      "${platform_root}/Database/ffxiv_server.sql" || {
        echo "Database baseline omits pinspawn persistence: ${platform_root}" >&2
        exit 7
      }
    grep -q 'aetherxiv-direct-core-v2' "${platform_root}/Database/ffxiv_server.sql" || {
      echo "Database baseline omits the AetherXIV 2 compatibility contract: ${platform_root}" >&2
      exit 7
    }

    if [[ "${platform}" == MacOS ]]; then
      map_root="${platform_root}/AetherXIV Core.app/Contents/Resources/servers/map"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/MacOS/AetherXIV.Core.App"
      verify_file "${platform_root}/AetherXIV Launcher.app/Contents/MacOS/AetherXIV.Launcher.App"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/AppIcon.icns"
      verify_file "${platform_root}/AetherXIV Launcher.app/Contents/Resources/AppIcon.icns"
      grep -q '<key>CFBundleIconFile</key>' "${platform_root}/AetherXIV Core.app/Contents/Info.plist" || {
        echo "Core macOS bundle does not declare its icon: ${platform_root}" >&2
        exit 8
      }
      grep -q '<key>CFBundleIconFile</key>' "${platform_root}/AetherXIV Launcher.app/Contents/Info.plist" || {
        echo "Launcher macOS bundle does not declare its icon: ${platform_root}" >&2
        exit 8
      }
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/world/AetherXIV.Core.World"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/lobby/AetherXIV.Core.Lobby"
      verify_file "${platform_root}/AetherXIV Core.app/Contents/Resources/servers/launcher-services/AetherXIV.Launcher.Host"
    else
      map_root="${platform_root}/servers/map"
      executable_suffix=""; [[ "${platform}" == Windows ]] && executable_suffix=".exe"
      verify_file "${platform_root}/core/app/AetherXIV.Core.App${executable_suffix}"
      verify_file "${platform_root}/launcher/app/AetherXIV.Launcher.App${executable_suffix}"
      verify_file "${platform_root}/servers/world/AetherXIV.Core.World${executable_suffix}"
      verify_file "${platform_root}/servers/lobby/AetherXIV.Core.Lobby${executable_suffix}"
      verify_file "${platform_root}/servers/launcher-services/AetherXIV.Launcher.Host${executable_suffix}"
    fi

    map_suffix=""; [[ "${platform}" == Windows ]] && map_suffix=".exe"
    verify_file "${map_root}/AetherXIV.Core.Map${map_suffix}"
    verify_file "${map_root}/scripts/player.lua"
    verify_file "${map_root}/scripts/commands/ArrowReloadCommand.lua"
    verify_file "${map_root}/scripts/directors/AfterQuestWarpDirector.lua"
    verify_file "${map_root}/staticactors.bin"
    verify_file "${map_root}/scripts.manifest.json"
    verify_file "${map_root}/navmesh/wil0Field01.snb"
    verify_file "${map_root}/navmesh/SHARPNAV_LICENSE"
  done
done

echo "repository-owned Debug/Release build layout verified: ${ROOT_DIR}/bin/build"
