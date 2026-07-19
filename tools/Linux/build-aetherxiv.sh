#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-dotnet}"
CONFIGURATION="${AETHERXIV_BUILD_CONFIGURATION:-Release}"
if (($# > 0)); then
  CONFIGURATION="$1"
  shift
fi
case "${CONFIGURATION}" in Debug|Release) ;; *) echo "Configuration must be Debug or Release." >&2; exit 2 ;; esac
PLATFORM_NAME="${AETHERXIV_PLATFORM_NAME:-Linux}"
OUTPUT_ROOT="${ROOT_DIR}/bin/build/${CONFIGURATION}/${PLATFORM_NAME}"
SERVER_RID="${AETHERXIV_SERVER_RID:-linux-x64}"
LAUNCHER_RID="${AETHERXIV_LAUNCHER_RID:-${SERVER_RID}}"
UMBRA_RID="${AETHERXIV_UMBRA_RID:-win-x86}"
UMBRA_VERSION="${AETHERXIV_UMBRA_VERSION:-2.0.0}"
LAUNCHER_ROOT="${ROOT_DIR}/AetherXIV Launcher"
RELEASE_WORK_ROOT="${ROOT_DIR}/bin/build/.work/${CONFIGURATION}/${PLATFORM_NAME}"
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

reset_output_root() {
  mkdir -p "${ROOT_DIR}/bin/build"
  find "${ROOT_DIR}/bin" -mindepth 1 -maxdepth 1 ! -name build -exec rm -rf {} +
  rm -rf "${ROOT_DIR}/bin/build/.work"
  find "${ROOT_DIR}/bin/build" -mindepth 1 -maxdepth 1 \
    ! -name Debug ! -name Release -exec rm -rf {} +
  # Linux and SteamOS packages are complete release images. Recreate the
  # selected platform root so stale diagnostics or superseded payloads cannot
  # survive a later build.
  rm -rf "${OUTPUT_ROOT}"
  mkdir -p "${OUTPUT_ROOT}"
}

require_mingw_x86() {
  if ! command -v i686-w64-mingw32-g++ >/dev/null 2>&1; then
    echo "i686-w64-mingw32-g++ is required to build the Windows x86 launcher/Umbra native payload." >&2
    exit 41
  fi
}

check_build_prerequisites() {
  local missing=()
  if ! command -v "${DOTNET_BIN}" >/dev/null 2>&1 \
      || ! "${DOTNET_BIN}" --list-sdks 2>/dev/null | awk '{print $1}' | grep -Fxq '10.0.203'; then
    missing+=(".NET SDK 10.0.203 (dotnet)")
  fi
  command -v python3 >/dev/null 2>&1 || missing+=("Python 3 (python3)")
  command -v i686-w64-mingw32-g++ >/dev/null 2>&1 || missing+=("MinGW-w64 (i686-w64-mingw32-g++)")

  if ((${#missing[@]} > 0)); then
    echo "AetherXIV Linux build prerequisites are missing:" >&2
    printf '  - %s\n' "${missing[@]}" >&2
    echo "See docs/build/${PLATFORM_NAME^^}.md before running this build again." >&2
    exit 40
  fi
}

build_umbra_native_injector() {
  require_mingw_x86

  local source_path="${LAUNCHER_ROOT}/AetherXIV.Launcher.NativeInjector/umbra_native_injector.cpp"
  local output_path="${OUTPUT_ROOT}/native/Umbra.NativeInjector.x86.exe"
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
    if [[ -d "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}" ]]; then
      cp "${output_path}" "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}/Umbra.NativeInjector.x86.exe"
    fi
  done

  rm -rf "${OUTPUT_ROOT}/native"
}

build_umbra_bootstrap() {
  require_mingw_x86

  local bootstrap_dir="${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Bootstrap"
  local imgui_dir="${LAUNCHER_ROOT}/Umbra/vendor/imgui"
  local framework_dir="${OUTPUT_ROOT}/launcher/app/Umbra/Framework"
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
    -ldwmapi

  printf '%s\n' "${UMBRA_VERSION}" > "${framework_dir}/version.txt"
  rm -rf "${framework_dir}/Assets"
  cp -R "${LAUNCHER_ROOT}/Umbra/assets" "${framework_dir}/Assets"
}

check_build_prerequisites
reset_output_root

echo "Publishing server hosts..."
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj" "${OUTPUT_ROOT}/servers/map" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.World/AetherXIV.Core.World.csproj" "${OUTPUT_ROOT}/servers/world" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Core.Lobby/AetherXIV.Core.Lobby.csproj" "${OUTPUT_ROOT}/servers/lobby" --runtime "${SERVER_RID}"
publish_project "${ROOT_DIR}/src/AetherXIV.Launcher.Host/AetherXIV.Launcher.Host.csproj" "${OUTPUT_ROOT}/servers/launcher-services" --runtime "${SERVER_RID}"
python3 "${ROOT_DIR}/tools/Universal/lua-tree-manifest.py" \
  --scripts-root "${OUTPUT_ROOT}/servers/map/scripts" \
  --manifest "${OUTPUT_ROOT}/servers/map/scripts.manifest.json" \
  --write

echo "Publishing launcher app and Windows helper payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/AetherXIV.Launcher.App/AetherXIV.Launcher.App.csproj" "${OUTPUT_ROOT}/launcher/app" --runtime "${LAUNCHER_RID}"
for helper_rid in ${HELPER_RIDS}; do
  publish_self_contained_project "${LAUNCHER_ROOT}/AetherXIV.Launcher.ClientLauncher/AetherXIV.Launcher.ClientLauncher.csproj" "${OUTPUT_ROOT}/launcher/app/Helpers/${helper_rid}" --runtime "${helper_rid}"
done
build_umbra_native_injector

echo "Publishing AetherXIV Core app..."
publish_self_contained_project "${ROOT_DIR}/src/AetherXIV.UI.App/AetherXIV.UI.App.csproj" "${OUTPUT_ROOT}/core/app" --runtime "${LAUNCHER_RID}"

echo "Publishing managed Umbra payload..."
publish_self_contained_project "${LAUNCHER_ROOT}/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj" "${OUTPUT_ROOT}/launcher/app/Umbra/Framework/Managed" --runtime "${UMBRA_RID}"
build_umbra_bootstrap

python3 "${ROOT_DIR}/tools/Universal/create-direct-core-database-package.py" \
  --repo-root "${ROOT_DIR}" \
  --output-dir "${OUTPUT_ROOT}/Database"

# Desktop entries target the documented /opt/aetherxiv installation layout and
# explicitly prevent desktop environments from opening a terminal.
mkdir -p "${OUTPUT_ROOT}/desktop"
install -m 0644 "${ROOT_DIR}/tools/Linux/desktop/org.aetherxiv.core.desktop" "${OUTPUT_ROOT}/desktop/"
install -m 0644 "${ROOT_DIR}/tools/Linux/desktop/org.aetherxiv.launcher.desktop" "${OUTPUT_ROOT}/desktop/"
icon_dir="${OUTPUT_ROOT}/share/icons/hicolor/512x512/apps"
mkdir -p "${icon_dir}"
install -m 0644 "${ROOT_DIR}/assets/icons/aetherxiv-core.png" "${icon_dir}/org.aetherxiv.core.png"
install -m 0644 "${ROOT_DIR}/assets/icons/aetherxiv-launcher.png" "${icon_dir}/org.aetherxiv.launcher.png"

if [[ "${CONFIGURATION}" == Release ]]; then
  find "${OUTPUT_ROOT}" -type f -name '*.pdb' -delete
fi
find "${OUTPUT_ROOT}" -type f -name '.DS_Store' -delete
cleanup_release_work
"${ROOT_DIR}/tools/Universal/verify-bin-only-build.sh" "${OUTPUT_ROOT}"

cat <<EOF
AetherXIV ${PLATFORM_NAME} build complete.
Output: ${OUTPUT_ROOT}

AetherXIV Launcher includes bundled Windows helper and Umbra payloads for Wine.
EOF
