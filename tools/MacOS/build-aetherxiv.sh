#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
if [[ ! -x "${DOTNET_BIN}" ]]; then
  DOTNET_BIN="$(command -v dotnet 2>/dev/null || true)"
fi

CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
BUILD_NUMBER="$(tr -d '[:space:]' < "${ROOT_DIR}/build-number.txt")"
if (($# > 0)); then
  CONFIGURATION="$1"
  shift
fi
case "${CONFIGURATION}" in Debug|Release) ;; *) echo "Configuration must be Debug or Release." >&2; exit 2 ;; esac
OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/MacOS"
STAGING_ROOT="${OUTPUT_ROOT}/.staging"
SERVER_RID="${AETHERXIV_SERVER_RID:-osx-arm64}"
LAUNCHER_RID="${AETHERXIV_LAUNCHER_RID:-${SERVER_RID}}"
UMBRA_RID="${AETHERXIV_UMBRA_RID:-win-x86}"
UMBRA_VERSION="${AETHERXIV_UMBRA_VERSION:-2.0.0}"
LAUNCHER_ROOT="${ROOT_DIR}/AetherXIV Launcher"
RELEASE_WORK_ROOT="${ROOT_DIR}/bin/build/.work/${CONFIGURATION}/MacOS"
export AetherXivWorkRoot="${RELEASE_WORK_ROOT}"

cleanup_release_work() {
  rm -rf "${RELEASE_WORK_ROOT}"
  rmdir "${ROOT_DIR}/bin/build/.work/${CONFIGURATION}" 2>/dev/null || true
  rmdir "${ROOT_DIR}/bin/build/.work" 2>/dev/null || true
}
trap cleanup_release_work EXIT
if [[ -n "${AETHERXIV_HELPER_RIDS:-}" ]]; then
  HELPER_RIDS="${AETHERXIV_HELPER_RIDS}"
elif [[ -n "${AETHERXIV_HELPER_RID:-}" ]]; then
  HELPER_RIDS="${AETHERXIV_HELPER_RID}"
else
  HELPER_RIDS="win-x64"
fi

reset_output_root() {
  mkdir -p "${ROOT_DIR}/bin/build"
  find "${ROOT_DIR}/bin" -mindepth 1 -maxdepth 1 ! -name build -exec rm -rf {} +
  rm -rf "${ROOT_DIR}/bin/build/.work"
  find "${ROOT_DIR}/bin/build" -mindepth 1 -maxdepth 1 \
    ! -name Debug ! -name Release -exec rm -rf {} +
  # A platform package is a complete release image. Recreate it instead of
  # preserving unknown files from an earlier build or a local smoke run.
  rm -rf "${OUTPUT_ROOT}"
  mkdir -p "${STAGING_ROOT}"
}

publish_project() {
  local project_path="$1"
  local output_path="$2"
  shift 2

  publish_project_common "${project_path}" "${output_path}" false "$@"
}

publish_self_contained_project() {
  local project_path="$1"
  local output_path="$2"
  shift 2

  publish_project_common "${project_path}" "${output_path}" true "$@"
}

publish_project_common() {
  local project_path="$1"
  local output_path="$2"
  local self_contained="$3"
  shift 3

  mkdir -p "${output_path}"
  "${DOTNET_BIN}" publish "${project_path}" \
    --configuration "${CONFIGURATION}" \
    --self-contained "${self_contained}" \
    --output "${output_path}" \
    -m:1 \
    /nodeReuse:false \
    /p:NuGetAudit=false \
    /p:PublishSingleFile=false \
    /p:UseAppHost=true \
    "$@"
}

require_mingw_x86() {
  if ! command -v i686-w64-mingw32-g++ >/dev/null 2>&1; then
    echo "i686-w64-mingw32-g++ is required to build the Windows x86 launcher/Umbra native payload." >&2
    exit 41
  fi
}

check_build_prerequisites() {
  local missing=()
  if [[ -z "${DOTNET_BIN}" ]] \
      || ! command -v "${DOTNET_BIN}" >/dev/null 2>&1 \
      || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fxq '10.0.203'; then
    missing+=(".NET SDK 10.0.203 (dotnet)")
  fi
  command -v python3 >/dev/null 2>&1 || missing+=("Python 3 (python3)")
  command -v i686-w64-mingw32-g++ >/dev/null 2>&1 || missing+=("MinGW-w64 (i686-w64-mingw32-g++)")

  if ((${#missing[@]} > 0)); then
    echo "AetherXIV macOS build prerequisites are missing:" >&2
    printf '  - %s\n' "${missing[@]}" >&2
    echo "See docs/build/MACOS.md before running this build again." >&2
    exit 40
  fi
}

build_umbra_native_injector() {
  require_mingw_x86

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.NativeInjector/umbra_native_injector.cpp"
  local output_path="${STAGING_ROOT}/native/Umbra.NativeInjector.x86.exe"
  mkdir -p "$(dirname "${output_path}")"

  echo "Building Umbra native x86 injector..."
  i686-w64-mingw32-g++ \
    -std=c++20 \
    -O2 \
    -municode \
    -static \
    "${source_path}" \
    -o "${output_path}"

  for helper_rid in ${HELPER_RIDS}; do
    if [[ -d "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}/Umbra.NativeInjector.x86.exe"
    fi
  done
}

build_umbra_bootstrap() {
  require_mingw_x86

  local bootstrap_dir="${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Bootstrap"
  local imgui_dir="${LAUNCHER_ROOT}/Umbra/vendor/imgui"
  local framework_dir="${STAGING_ROOT}/umbra/Framework"
  mkdir -p "${framework_dir}"

  echo "Building Umbra native x86 bootstrap..."
  i686-w64-mingw32-g++ \
    -std=c++20 \
    -O2 \
    -fno-builtin \
    -fno-tree-loop-distribute-patterns \
    -fno-exceptions \
    -fno-rtti \
    -DIMGUI_IMPL_WIN32_DISABLE_GAMEPAD \
    -I"${imgui_dir}" \
    -I"${imgui_dir}/backends" \
    -shared \
    -static \
    -static-libgcc \
    -static-libstdc++ \
    -Wl,--kill-at \
    -o "${framework_dir}/Aether.Umbra.Bootstrap.x86.dll" \
    "${bootstrap_dir}/dllmain.cpp" \
    "${imgui_dir}/imgui.cpp" \
    "${imgui_dir}/imgui_draw.cpp" \
    "${imgui_dir}/imgui_tables.cpp" \
    "${imgui_dir}/imgui_widgets.cpp" \
    "${imgui_dir}/backends/imgui_impl_dx9.cpp" \
    "${imgui_dir}/backends/imgui_impl_win32.cpp" \
    -lgdi32 \
    -ldwmapi \
    -lws2_32

  printf '%s\n' "${UMBRA_VERSION}" > "${framework_dir}/version.txt"
  rm -rf "${framework_dir}/Assets"
  cp -R "${LAUNCHER_ROOT}/Umbra/assets" "${framework_dir}/Assets"
}

write_info_plist() {
  local plist_path="$1"
  local bundle_name="$2"
  local bundle_identifier="$3"
  local executable_name="$4"
  local icon_file="$5"

  cat > "${plist_path}" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>${bundle_name}</string>
  <key>CFBundleExecutable</key>
  <string>${executable_name}</string>
  <key>CFBundleIdentifier</key>
  <string>${bundle_identifier}</string>
  <key>CFBundleIconFile</key>
  <string>${icon_file}</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>${bundle_name}</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>2.0.0</string>
  <key>CFBundleVersion</key>
  <string>${BUILD_NUMBER}</string>
  <key>LSMinimumSystemVersion</key>
  <string>14.0</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
EOF
}

create_app_bundle() {
  local bundle_name="$1"
  local bundle_identifier="$2"
  local executable_name="$3"
  local publish_dir="$4"
  local icon_source="$5"
  local bundle_dir="${OUTPUT_ROOT}/${bundle_name}.app"
  local contents_dir="${bundle_dir}/Contents"
  local macos_dir="${contents_dir}/MacOS"
  local resources_dir="${contents_dir}/Resources"

  echo "Creating ${bundle_name}.app..."
  rm -rf "${bundle_dir}"
  mkdir -p "${macos_dir}" "${resources_dir}"
  cp -R "${publish_dir}/." "${macos_dir}/"
  cp "${icon_source}" "${resources_dir}/AppIcon.icns"
  chmod +x "${macos_dir}/${executable_name}" || true
  write_info_plist "${contents_dir}/Info.plist" "${bundle_name}" "${bundle_identifier}" "${executable_name}" "AppIcon.icns"
}

create_core_app_bundle() {
  create_app_bundle "AetherXIV Core" "org.aetherxiv.core" "AetherXIV.Core.App" "${STAGING_ROOT}/core/app" "${ROOT_DIR}/assets/icons/aetherxiv-core.icns"
  local resources_dir="${OUTPUT_ROOT}/AetherXIV Core.app/Contents/Resources"
  cp -R "${STAGING_ROOT}/servers" "${resources_dir}/servers"
  mkdir -p "${resources_dir}/AetherXIV Launcher/Image"
  cp -R "${LAUNCHER_ROOT}/Image/Reels" "${resources_dir}/AetherXIV Launcher/Image/Reels"
}

copy_umbra_payload_to_launcher_bundle() {
  local umbra_root="${OUTPUT_ROOT}/AetherXIV Launcher.app/Contents/MacOS/Umbra/Framework"
  rm -rf "${umbra_root}"
  mkdir -p "$(dirname "${umbra_root}")"
  cp -R "${STAGING_ROOT}/umbra/Framework" "${umbra_root}"
}

cleanup_staging() {
  rm -rf "${STAGING_ROOT}"
  if [[ "${CONFIGURATION}" == Release ]]; then
    find "${OUTPUT_ROOT}" -type f -name '*.pdb' -delete
  fi
  find "${ROOT_DIR}/bin/build" -type f -name '.DS_Store' -delete
}

write_build_manifest() {
  local manifest_path="${OUTPUT_ROOT}/build-manifest.txt"
  local map_core_path="${OUTPUT_ROOT}/AetherXIV Core.app/Contents/Resources/servers/map/AetherXIV.Core.Map.dll"
  {
    printf 'schema=aetherxiv.build.manifest.v1\n'
    printf 'built_at_utc=%s\n' "$(date -u +'%Y-%m-%dT%H:%M:%SZ')"
    printf 'configuration=%s\n' "${CONFIGURATION}"
    printf 'product_version=2.0\n'
    printf 'build_number=%s\n' "${BUILD_NUMBER}"
    printf 'server_rid=%s\n' "${SERVER_RID}"
    printf 'map_core_sha256=%s\n' "$(shasum -a 256 "${map_core_path}" | awk '{print $1}')"
    printf 'map_core_path=%s\n' "AetherXIV Core.app/Contents/Resources/servers/map/AetherXIV.Core.Map.dll"
  } > "${manifest_path}"
}

check_build_prerequisites
reset_output_root

echo "Publishing server hosts..."
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${STAGING_ROOT}/servers/map" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${STAGING_ROOT}/servers/world" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${STAGING_ROOT}/servers/lobby" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${STAGING_ROOT}/servers/launcher-services" --runtime "${SERVER_RID}"
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
  --scripts-root "${STAGING_ROOT}/servers/map/scripts" \
  --manifest "${STAGING_ROOT}/servers/map/scripts.manifest.json" \
  --write

echo "Publishing launcher app and Windows helper payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/AetherXIV.Launcher.App/AetherXIV.Launcher.App.csproj" "${STAGING_ROOT}/launcher/app" --runtime "${LAUNCHER_RID}"
for helper_rid in ${HELPER_RIDS}; do
  publish_self_contained_project \
    "${LAUNCHER_ROOT}/AetherXIV.Launcher.ClientLauncher/AetherXIV.Launcher.ClientLauncher.csproj" \
    "${STAGING_ROOT}/launcher/app/Helpers/${helper_rid}" \
    --runtime "${helper_rid}" \
    /p:PublishSingleFile=true \
    /p:IncludeNativeLibrariesForSelfExtract=true
done
build_umbra_native_injector

echo "Publishing AetherXIV Core app..."
publish_self_contained_project "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" "${STAGING_ROOT}/core/app" --runtime "${LAUNCHER_RID}"

echo "Publishing managed Umbra payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj" "${STAGING_ROOT}/umbra/Framework/Managed" --runtime "${UMBRA_RID}"
build_umbra_bootstrap

create_app_bundle "AetherXIV Launcher" "org.aetherxiv.launcher" "AetherXIV.Launcher.App" "${STAGING_ROOT}/launcher/app" "${ROOT_DIR}/assets/icons/aetherxiv-launcher.icns"
copy_umbra_payload_to_launcher_bundle
create_core_app_bundle
python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${OUTPUT_ROOT}/Database"
write_build_manifest
cleanup_staging
cleanup_release_work
"${ROOT_DIR}/tools/Universal/verify-bin-only-build.sh" "${OUTPUT_ROOT}"

cat <<EOF
AetherXIV macOS build complete.
Output: ${OUTPUT_ROOT}
Apps: ${OUTPUT_ROOT}/AetherXIV Launcher.app
      ${OUTPUT_ROOT}/AetherXIV Core.app

This script publishes the macOS launcher/server bundle plus the self-contained Windows helper and Umbra payloads used by Wine.
EOF
