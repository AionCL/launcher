#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
payload=${1:-out/linux}
destination=${2:-out}
version=${AIONCL_DEB_VERSION:-2.5.42~preview13}
preview=${AIONCL_PREVIEW_NUMBER:-13}
[[ $version =~ ^[0-9][A-Za-z0-9.+:~-]*$ ]] || { echo 'Invalid Debian package version.' >&2; exit 1; }
[[ $preview =~ ^[0-9]+$ ]] || { echo 'Invalid Linux preview number.' >&2; exit 1; }
[[ -f $payload/AionCL.Launcher.Linux.exe && -x $payload/aioncl-launcher && -f $payload/assets/aioncl-icon.png ]]
command -v dpkg-deb >/dev/null || { echo 'dpkg-deb is required.' >&2; exit 1; }
mkdir -p "$destination"
stage=$(mktemp -d)
trap 'rm -rf "$stage"' EXIT
install -d -m 0755 "$stage/opt/aioncl/launcher" "$stage/usr/share/applications"
cp -a "$payload/." "$stage/opt/aioncl/launcher/"
chmod 0755 "$stage/opt/aioncl" "$stage/opt/aioncl/launcher"
sed "s/@PREVIEW@/$preview/g" linux/aioncl-launcher.desktop > "$stage/usr/share/applications/aioncl-launcher.desktop"
install -d "$stage/DEBIAN"
cat > "$stage/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
root=${AIONCL_PACKAGE_ROOT:-}
chmod 0755 "${root}/opt/aioncl/launcher" "${root}/opt/aioncl/launcher/aioncl-launcher"
EOF
chmod 0755 "$stage/DEBIAN/postinst"
cat >"$stage/DEBIAN/control" <<EOF
Package: aioncl-launcher
Version: $version
Section: games
Priority: optional
Architecture: amd64
Depends: ca-certificates, fonts-dejavu-core, libgdiplus, libmono-system-io-compression-filesystem4.0-cil, libmono-system-net-http4.0-cil, libmono-system-web-extensions4.0-cil, libmono-system-windows-forms4.0-cil, libssl3t64 | libssl3, mono-runtime, wine, xdg-utils
Maintainer: AionCL Project
Description: AionCL launcher for Linux
 Native Linux launcher for installing and updating Aion Classic, with Wine
 integration for running the game client.
EOF
dpkg-deb --root-owner-group --build "$stage" "$destination/aioncl-launcher_${version}_amd64.deb" >/dev/null
echo "Created $destination/aioncl-launcher_${version}_amd64.deb"
