#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
payload=${1:-out/linux}
destination=${2:-out}
[[ -f $payload/AionCL.Launcher.Linux.exe && -x $payload/aioncl-launcher && -f $payload/assets/aioncl-icon.png ]]
command -v dpkg-deb >/dev/null || { echo 'dpkg-deb is required.' >&2; exit 1; }
mkdir -p "$destination"
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
install -d "$stage/opt/aioncl/launcher" "$stage/usr/share/applications"
cp -a "$payload/." "$stage/opt/aioncl/launcher/"
install -m 644 linux/aioncl-launcher.desktop "$stage/usr/share/applications/aioncl-launcher.desktop"
install -d "$stage/DEBIAN"
cat >"$stage/DEBIAN/control" <<'EOF'
Package: aioncl-launcher
Version: 2.5.42~preview12
Section: games
Priority: optional
Architecture: amd64
Depends: ca-certificates, fonts-dejavu-core, libgdiplus, libmono-system-io-compression-filesystem4.0-cil, libmono-system-net-http4.0-cil, libmono-system-web-extensions4.0-cil, libmono-system-windows-forms4.0-cil, libssl3t64 | libssl3, mono-runtime, wine, xdg-utils
Maintainer: AionCL Project
Description: AionCL launcher for Linux
 Native Linux launcher for installing and updating Aion Classic, with Wine
 integration for running the game client.
EOF
dpkg-deb --root-owner-group --build "$stage" "$destination/aioncl-launcher_2.5.42~preview12_amd64.deb" >/dev/null
echo "Created $destination/aioncl-launcher_2.5.42~preview12_amd64.deb"
