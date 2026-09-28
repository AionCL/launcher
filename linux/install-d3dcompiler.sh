#!/usr/bin/env bash
# Install Microsoft's 64-bit D3DCompiler 47 for Aion's Wine prefix.
set -euo pipefail
if [[ $# != 2 || $1 != /* || ! -d $1/drive_c/windows/system32 || $2 != /* || ! -d $2/bin64 ]]; then
    echo 'Usage: install-d3dcompiler.sh /absolute/path/to/wine-prefix /absolute/path/to/aion-client' >&2
    exit 1
fi
if (( EUID == 0 )); then
    echo 'Run as the owner of the Wine prefix and Aion client, not root.' >&2
    exit 1
fi
wine_runner=${AIONCL_WINE:-wine}
for command in curl sha256sum cabextract pgrep; do
    command -v "$command" >/dev/null || { echo "Missing dependency: $command" >&2; exit 1; }
done
command -v "$wine_runner" >/dev/null || { echo "Missing Wine runner: $wine_runner" >&2; exit 1; }
if pgrep -x aionclassic.bin >/dev/null; then
    echo 'Close Aion before installing its shader compiler.' >&2
    exit 1
fi

export WINEPREFIX="$(realpath -- "$1")"
client=$(realpath -- "$2")
cache="${XDG_CACHE_HOME:-$HOME/.cache}/aioncl/d3dcompiler"
archive="$cache/d3dcompiler-47-x64.cab"
digest=f736e161547095bb8d98c636b85fdfeb4070fefeee3c3745db3ce88f6eb1d9de
url=https://download.microsoft.com/download/B/0/C/B0C80BA3-8AD6-4958-810B-6882485230B5/standalonesdk/Installers/61d57a7a82309cd161a854a6f4619e52.cab
mkdir -p "$cache"
valid_archive() { [[ -f $archive ]] && echo "$digest  $archive" | sha256sum --check --status; }
if ! valid_archive; then
    curl --fail --location --retry 2 --output "$archive.part" "$url"
    echo "$digest  $archive.part" | sha256sum --check --status
    mv "$archive.part" "$archive"
fi

scratch=$(mktemp -d "$cache/extract.XXXXXXXX")
trap 'rm -rf -- "$scratch"' EXIT
# Internal filename documented by the upstream Winetricks d3dcompiler_47 verb.
cabextract -q -d "$scratch" -F fil3585cb2ea5db13cc0838f8d06b5c9679 "$archive"
source="$scratch/fil3585cb2ea5db13cc0838f8d06b5c9679"
[[ -s $source ]] || { echo 'Microsoft D3DCompiler extraction failed.' >&2; exit 1; }
target="$WINEPREFIX/drive_c/windows/system32/d3dcompiler_47.dll"
if cmp -s "$source" "$target"; then
    "$wine_runner" reg add 'HKCU\Software\Wine\DllOverrides' /v d3dcompiler_47 /t REG_SZ /d native,builtin /f
    printf 'D3DCOMPILER_ALREADY_READY version=47 prefix=%s\n' "$WINEPREFIX"
    exit 0
fi
if pgrep -x aionclassic.bin >/dev/null; then
    echo 'Aion started during preparation; close it and run this command again.' >&2
    exit 1
fi

backup="$WINEPREFIX/aioncl-runtime-backup"
mkdir -p "$backup"
if [[ ! -e $backup/d3dcompiler_47.dll && ! -L $backup/d3dcompiler_47.dll ]]; then
    cp -a -- "$target" "$backup/d3dcompiler_47.dll"
fi
install -m 644 "$source" "$target.aioncl-new"
mv -f -- "$target.aioncl-new" "$target"
"$wine_runner" reg add 'HKCU\Software\Wine\DllOverrides' /v d3dcompiler_47 /t REG_SZ /d native,builtin /f

# Previously compiled bytecode can retain the broken Wine compiler output.
stamp=$(date +%Y%m%d-%H%M%S)
cache_backup="$client/.aioncl/shader-cache-before-native-compiler-$stamp"
mkdir -p "$cache_backup"
if [[ -d $client/Shaders/Cache ]]; then
    mv -- "$client/Shaders/Cache" "$cache_backup/Shaders-Cache"
fi
if [[ -f $client/aionclassic.bin.dxvk-cache ]]; then
    mv -- "$client/aionclassic.bin.dxvk-cache" "$cache_backup/"
fi
printf 'D3DCOMPILER_INSTALL_PASS version=47 prefix=%s cache_backup=%s\n' "$WINEPREFIX" "$cache_backup"
