#!/usr/bin/env bash
# Prepare the Linux-only graphics and Wine compatibility layer before Aion starts.
set -euo pipefail
if [[ $# != 2 || $1 != /* || $2 != /* || ! -d $2/bin64 ]]; then
    echo 'Usage: prepare-linux-runtime.sh /absolute/path/to/wine-prefix /absolute/path/to/aion-client' >&2
    exit 1
fi
if (( EUID == 0 )); then
    echo 'Run as the owner of the Wine prefix and Aion client, not root.' >&2
    exit 1
fi
base=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
prefix=$(realpath -- "$1")
client=$(realpath -- "$2")
export WINEPREFIX="$prefix"
wine_runner=${AIONCL_WINE:-wine}
command -v "$wine_runner" >/dev/null || { echo "Missing Wine runner: $wine_runner" >&2; exit 1; }
"$wine_runner" reg add 'HKCU\Software\Wine\AppDefaults\aionclassic.bin' /v Version /t REG_SZ /d win7 /f
"$base/install-dxvk.sh" "$client"
"$base/install-d3dcompiler.sh" "$prefix" "$client"
printf 'LINUX_RUNTIME_READY prefix=%s client=%s\n' "$prefix" "$client"
