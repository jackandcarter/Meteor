#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "${script_dir}/../../.." && pwd)"
project_path="${script_dir}/AetherXIV.ClientData.Miner/AetherXIV.ClientData.Miner.csproj"
output_path="${repo_root}/bin/build/Debug/MacOS/DeveloperTools/client-data-miner"
work_path="${repo_root}/bin/build/.work/Debug/MacOSClientDataMiner"
export AetherXivWorkRoot="${work_path}"

cleanup() {
  find "${work_path}" -depth -type f -delete 2>/dev/null || true
  find "${work_path}" -depth -type d -empty -delete 2>/dev/null || true
  rmdir "${repo_root}/bin/build/.work/Debug" 2>/dev/null || true
  rmdir "${repo_root}/bin/build/.work" 2>/dev/null || true
}
trap cleanup EXIT

dotnet_bin="${DOTNET_BIN:-/usr/local/share/dotnet/dotnet}"
if [[ ! -x "${dotnet_bin}" ]]; then
  dotnet_bin="$(command -v dotnet)"
fi

mkdir -p "${output_path}"

"${dotnet_bin}" publish "${project_path}" \
  --configuration Debug \
  --framework net10.0 \
  --self-contained false \
  --output "${output_path}"

chmod +x "${output_path}/AetherXIV.ClientData.Miner"

printf 'Built client data miner:\n  %s\n' "${output_path}/AetherXIV.ClientData.Miner"
printf 'Run from repo root:\n  bin/build/Debug/MacOS/DeveloperTools/client-data-miner/AetherXIV.ClientData.Miner --client-root <path> --output <path>\n'
