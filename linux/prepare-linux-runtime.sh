#!/usr/bin/env bash
# Prepare the Linux-only graphics and Wine compatibility layer before Aion starts.
set -euo pipefail
if [[ $# != 3 || $1 != /* || $2 != /* || ! -d $2/bin64 ]]; then
    echo 'Usage: prepare-linux-runtime.sh /absolute/path/to/wine-prefix /absolute/path/to/aion-client native|wine' >&2
    exit 1
fi
if (( EUID == 0 )); then
    echo 'Run as the owner of the Wine prefix and Aion client, not root.' >&2
    exit 1
fi
mode=$3
[[ $mode == native || $mode == wine ]] || { echo 'Invalid D3DX mode.' >&2; exit 1; }
base=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
prefix=$(realpath -- "$1")
client=$(realpath -- "$2")
export WINEPREFIX="$prefix"
wine_runner=${AIONCL_WINE:-wine}
command -v "$wine_runner" >/dev/null || { echo "Missing Wine runner: $wine_runner" >&2; exit 1; }
# Reuse is safe with a running client; mode changes invalidate shared caches.
state="$client/.aioncl/d3dx9-mode"
previous=$(cat "$state" 2>/dev/null || true)
if [[ $previous != "$mode" ]] && pgrep -x aionclassic.bin >/dev/null; then
    echo 'Close Aion before changing its graphics mode.' >&2
    exit 1
fi
"$wine_runner" reg add 'HKCU\Software\Wine\AppDefaults\aionclassic.bin' /v Version /t REG_SZ /d win7 /f
"$base/install-dxvk.sh" "$client"
"$base/install-d3dcompiler.sh" "$prefix" "$client"
if [[ $mode == native ]]; then
    "$base/install-d3dx9.sh" "$prefix" "$client"
else
    "$wine_runner" reg add 'HKCU\Software\Wine\DllOverrides' /v d3dx9_38 /t REG_SZ /d builtin /f
fi
# Rebuild caches only when the mode changes, including the first migration.
if [[ $previous != "$mode" ]]; then
    backup="$client/.aioncl/shader-cache-before-d3dx9-$(date +%Y%m%d-%H%M%S)-$$"
    mkdir -p "$backup"
    [[ ! -d $client/Shaders/Cache ]] || mv -- "$client/Shaders/Cache" "$backup/Shaders-Cache"
    [[ ! -f $client/aionclassic.bin.dxvk-cache ]] || mv -- "$client/aionclassic.bin.dxvk-cache" "$backup/"
    printf '%s\n' "$mode" > "$state.new"
    mv -- "$state.new" "$state"
    printf 'D3DX9_MODE_CHANGED mode=%s backup=%s\n' "$mode" "$backup"
fi
printf 'LINUX_RUNTIME_READY prefix=%s client=%s\n' "$prefix" "$client"
