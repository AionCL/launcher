#!/usr/bin/env bash
# Install a pinned DXVK D3D9 runtime in an existing Aion client.
set -euo pipefail
if [[ $# != 1 || $1 != /* || ! -d $1/bin64 ]]; then
    echo 'Usage: install-dxvk.sh /absolute/path/to/aion-client' >&2
    exit 1
fi
if (( EUID == 0 )); then
    echo 'Run as the owner of the Aion client, not root.' >&2
    exit 1
fi
for command in curl sha256sum tar pgrep; do
    command -v "$command" >/dev/null || { echo "Missing dependency: $command" >&2; exit 1; }
done
if pgrep -x aionclassic.bin >/dev/null; then
    echo 'Close Aion before installing DXVK.' >&2
    exit 1
fi

client=$(realpath -- "$1")
version=2.6.2
digest=17761876556afd55736cb895d184f5a1c55d43350f1b1e3b129f8d28706d7992
cache="${XDG_CACHE_HOME:-$HOME/.cache}/aioncl/dxvk"
archive="$cache/dxvk-$version.tar.gz"
mkdir -p "$cache"
valid_archive() { [[ -f $archive ]] && echo "$digest  $archive" | sha256sum --check --status; }
if ! valid_archive; then
    curl --fail --location --retry 2 --output "$archive.part" \
        "https://github.com/doitsujin/dxvk/releases/download/v$version/dxvk-$version.tar.gz"
    echo "$digest  $archive.part" | sha256sum --check --status
    mv "$archive.part" "$archive"
fi

scratch=$(mktemp -d "$cache/extract.XXXXXXXX")
trap 'rm -rf -- "$scratch"' EXIT
tar -xzf "$archive" -C "$scratch" \
    "dxvk-$version/x64/d3d9.dll" "dxvk-$version/x32/d3d9.dll"
if pgrep -x aionclassic.bin >/dev/null; then
    echo 'Aion started during preparation; close it and run this command again.' >&2
    exit 1
fi

backup="$client/.aioncl/dxvk-backup"
mkdir -p "$backup/bin64"
if [[ -e $client/bin64/d3d9.dll && ! -e $backup/bin64/d3d9.dll ]]; then
    cp -a -- "$client/bin64/d3d9.dll" "$backup/bin64/d3d9.dll"
fi
install -m 644 "$scratch/dxvk-$version/x64/d3d9.dll" "$client/bin64/d3d9.dll.aioncl-new"
mv -f -- "$client/bin64/d3d9.dll.aioncl-new" "$client/bin64/d3d9.dll"

if [[ -d $client/bin32 ]]; then
    mkdir -p "$backup/bin32"
    if [[ -e $client/bin32/d3d9.dll && ! -e $backup/bin32/d3d9.dll ]]; then
        cp -a -- "$client/bin32/d3d9.dll" "$backup/bin32/d3d9.dll"
    fi
    install -m 644 "$scratch/dxvk-$version/x32/d3d9.dll" "$client/bin32/d3d9.dll.aioncl-new"
    mv -f -- "$client/bin32/d3d9.dll.aioncl-new" "$client/bin32/d3d9.dll"
fi

config="$client/dxvk.conf"
if [[ -e $config && ! -e $backup/dxvk.conf ]]; then
    cp -a -- "$config" "$backup/dxvk.conf"
fi
cat >"$config.aioncl-new" <<'EOF'
dxvk.allowFse = False
d3d9.cachedDynamicBuffers = True
d3d9.deferSurfaceCreation = True
d3d9.forceSamplerTypeSpecConstants = True
EOF
mv -f -- "$config.aioncl-new" "$config"
# Shader bytecode is renderer-specific. The No-IP DLL marker only tracks its own
# source patch, so a cache built by WineD3D would otherwise survive the switch
# to DXVK and cause angle-dependent black lighting/terrain passes.
shader_cache="$client/Shaders/Cache"
if [[ -d $shader_cache && ! -e $backup/Shaders-Cache-before-DXVK ]]; then
    mv -- "$shader_cache" "$backup/Shaders-Cache-before-DXVK"
fi
printf 'DXVK_INSTALL_PASS version=%s client=%s backup=%s\n' "$version" "$client" "$backup"
