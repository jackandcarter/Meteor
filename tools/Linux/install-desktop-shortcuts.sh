#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_ROOT="${AETHERXIV_RELEASE_ROOT:-${SCRIPT_DIR}}"

CORE_APP="${RELEASE_ROOT}/core/app/AetherXIV.Core.App"
LAUNCHER_APP="${RELEASE_ROOT}/launcher/app/AetherXIV.Launcher.App"
CORE_ICON="${RELEASE_ROOT}/share/icons/hicolor/512x512/apps/org.aetherxiv.core.png"
LAUNCHER_ICON="${RELEASE_ROOT}/share/icons/hicolor/512x512/apps/org.aetherxiv.launcher.png"

for required in "${CORE_APP}" "${LAUNCHER_APP}" "${CORE_ICON}" "${LAUNCHER_ICON}"; do
  [[ -f "${required}" ]] || {
    echo "This installer must be run from the root of an extracted AetherXIV Linux or SteamOS release: missing ${required}" >&2
    exit 2
  }
done
chmod u+x "${CORE_APP}" "${LAUNCHER_APP}"

desktop_quote() {
  local value="$1"
  value="${value//\\/\\\\}"
  value="${value//\"/\\\"}"
  value="${value//\`/\\\`}"
  value="${value//\$/\\\$}"
  value="${value//%/%%}"
  printf '"%s"' "${value}"
}

DATA_HOME="${XDG_DATA_HOME:-${HOME}/.local/share}"
APPLICATIONS_DIR="${DATA_HOME}/applications"
ICON_DIR="${DATA_HOME}/icons/hicolor/512x512/apps"
mkdir -p "${APPLICATIONS_DIR}" "${ICON_DIR}"
install -m 0644 "${CORE_ICON}" "${ICON_DIR}/org.aetherxiv.core.png"
install -m 0644 "${LAUNCHER_ICON}" "${ICON_DIR}/org.aetherxiv.launcher.png"

write_entry() {
  local destination="$1" name="$2" comment="$3" executable="$4" icon_name="$5" categories="$6"
  {
    printf '%s\n' '[Desktop Entry]'
    printf '%s\n' 'Type=Application' 'Version=1.0'
    printf 'Name=%s\nComment=%s\n' "${name}" "${comment}"
    printf 'Exec=%s\nIcon=%s\n' "$(desktop_quote "${executable}")" "${icon_name}"
    printf '%s\n' 'Terminal=false' "Categories=${categories}" 'StartupNotify=true'
  } > "${destination}"
  chmod u+x "${destination}"
}

CORE_ENTRY="${APPLICATIONS_DIR}/org.aetherxiv.core.desktop"
LAUNCHER_ENTRY="${APPLICATIONS_DIR}/org.aetherxiv.launcher.desktop"
write_entry "${CORE_ENTRY}" "AetherXIV Core" "Manage the local AetherXIV server" "${CORE_APP}" 'org.aetherxiv.core' 'Game;Utility;'
write_entry "${LAUNCHER_ENTRY}" "AetherXIV Launcher" "Configure and launch AetherXIV" "${LAUNCHER_APP}" 'org.aetherxiv.launcher' 'Game;'

if command -v update-desktop-database >/dev/null 2>&1; then
  update-desktop-database "${APPLICATIONS_DIR}" >/dev/null 2>&1 || true
fi

DESKTOP_DIR=""
if command -v xdg-user-dir >/dev/null 2>&1; then
  DESKTOP_DIR="$(xdg-user-dir DESKTOP 2>/dev/null || true)"
elif [[ -d "${HOME}/Desktop" ]]; then
  DESKTOP_DIR="${HOME}/Desktop"
fi
if [[ -n "${DESKTOP_DIR}" && -d "${DESKTOP_DIR}" ]]; then
  install -m 0755 "${CORE_ENTRY}" "${DESKTOP_DIR}/AetherXIV Core.desktop"
  install -m 0755 "${LAUNCHER_ENTRY}" "${DESKTOP_DIR}/AetherXIV Launcher.desktop"
  if command -v gio >/dev/null 2>&1; then
    gio set "${DESKTOP_DIR}/AetherXIV Core.desktop" metadata::trusted true >/dev/null 2>&1 || true
    gio set "${DESKTOP_DIR}/AetherXIV Launcher.desktop" metadata::trusted true >/dev/null 2>&1 || true
  fi
  echo "Installed AetherXIV application-menu and desktop shortcuts for ${RELEASE_ROOT}"
else
  echo "Installed AetherXIV application-menu shortcuts for ${RELEASE_ROOT}"
fi
