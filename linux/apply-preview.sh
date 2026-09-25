#!/usr/bin/env bash
# Run on the recipe desktop as the owner of the launcher installation.
# Usage: apply-preview.sh archive.tar.gz /path/to/launcher
set -euo pipefail
if [[ $# != 2 || ! -f $1 || ! -d $2 ]]; then
    echo 'Usage: apply-preview.sh archive.tar.gz /path/to/existing/launcher' >&2
    exit 1
fi
if pgrep -x aionclassic.bin >/dev/null || pgrep -f '[m]ono .*AionCL.Launcher.Linux.exe' >/dev/null; then
    echo 'Close Aion and the launcher first.' >&2
    exit 1
fi
archive=$(realpath -- "$1")
target=$(realpath -- "$2")
log="$(dirname -- "$target")/apply-preview-$(date +%Y%m%d-%H%M%S).log"
exec > >(tee "$log") 2>&1
echo "Applying $archive to $target"
(cd -- "$(dirname -- "$archive")" && sha256sum --check "$(basename -- "$archive").sha256")
# Accept only the files produced by the launcher build, without path components.
while IFS= read -r entry; do
    case "$entry" in
        AionCL.Launcher.Linux.exe|launcher.json|aioncl-launcher|install-d3dx9.sh|install-dxvk.sh|LINUX.md) ;;
        *) echo "Unexpected archive entry: $entry" >&2; exit 1 ;;
    esac
done < <(tar -tzf "$archive")
backup="$(dirname -- "$target")/launcher-backup-$(date +%Y%m%d-%H%M%S)"
mkdir "$backup"
cp -a "$target/." "$backup/"
tar -xzf "$archive" --no-same-owner -C "$target"
echo "APPLY_PASS; backup=$backup; log=$log"
echo 'You can now open the launcher and test the connection.'
