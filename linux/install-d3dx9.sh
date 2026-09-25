#!/usr/bin/env bash
# Install only the 64-bit D3DX library used by Aion Classic into its Wine prefix.
set -euo pipefail
if [[ $# != 1 || $1 != /* || ! -d $1/drive_c/windows/system32 ]]; then
    echo 'Usage: install-d3dx9.sh /absolute/path/to/initialized/wine-prefix' >&2
    exit 1
fi
if (( EUID == 0 )); then
    echo 'Run as the owner of the Wine prefix, not root.' >&2
    exit 1
fi
for command in curl sha256sum cabextract wine pgrep; do
    command -v "$command" >/dev/null || { echo "Missing dependency: $command" >&2; exit 1; }
done
if pgrep -x 'aionclassic.bin' >/dev/null; then
    echo 'Close Aion before installing its graphics runtime.' >&2
    exit 1
fi
export WINEPREFIX="$1"
cache="${XDG_CACHE_HOME:-$HOME/.cache}/aioncl/directx"
mkdir -p "$cache"
archive="$cache/directx_Jun2010_redist.exe"
digest=053f76dcbb28802e23341b6a787e3b0791c0fa5c8d4d011b1044172dbf89c73b
valid_archive() { [[ -f $archive ]] && echo "$digest  $archive" | sha256sum --check --status; }
if ! valid_archive; then
    curl --fail --location --retry 2 --output "$archive.part" \
        https://download.microsoft.com/download/8/4/A/84A35BF1-DAFE-4AE8-82AF-AD2AE20B6B14/directx_Jun2010_redist.exe
    echo "$digest  $archive.part" | sha256sum --check --status
    mv "$archive.part" "$archive"
fi
scratch=$(mktemp -d "$cache/extract.XXXXXXXX")
trap 'rm -rf -- "$scratch"' EXIT
cabextract -q -d "$scratch" -F JUN2008_d3dx9_38_x64.cab "$archive"
cabextract -q -d "$scratch" -F d3dx9_38.dll "$scratch/JUN2008_d3dx9_38_x64.cab"
# Recheck after the download: the user may have started the game meanwhile.
if pgrep -x 'aionclassic.bin' >/dev/null; then
    echo 'Aion started during preparation; close it and run this command again.' >&2
    exit 1
fi
target="$WINEPREFIX/drive_c/windows/system32/d3dx9_38.dll"
backup="$WINEPREFIX/aioncl-runtime-backup"
mkdir -p "$backup"
if [[ ! -e $backup/d3dx9_38.dll && ! -L $backup/d3dx9_38.dll ]]; then
    cp -a -- "$target" "$backup/d3dx9_38.dll"
fi
# Atomic replacement avoids writing through a Wine symlink into system files.
install -m 644 "$scratch/d3dx9_38.dll" "$target.aioncl-new"
mv -f -- "$target.aioncl-new" "$target"
wine reg add 'HKCU\Software\Wine\DllOverrides' /v d3dx9_38 /t REG_SZ /d native,builtin /f
echo 'D3DX9_38 installed in the Aion Wine prefix. Original library saved in aioncl-runtime-backup.'
