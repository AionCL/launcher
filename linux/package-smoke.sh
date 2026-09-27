#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
destination=${1:-out}
version=${AIONCL_DEB_VERSION:-2.5.42~preview13}
rpm_release=${AIONCL_RPM_RELEASE:-0.preview13}
deb="$destination/aioncl-launcher_${version}_amd64.deb"
rpm="$destination/aioncl-launcher-2.5.42-${rpm_release}.x86_64.rpm"
[[ -f $deb && -f $rpm ]]
depends=$(dpkg-deb -f "$deb" Depends)
depends=${depends//, /,}
for package in mono-runtime libmono-system-windows-forms4.0-cil libgdiplus wine xdg-utils; do
    [[ ,$depends, == *,$package,* ]] || { echo "Missing Debian dependency: $package" >&2; exit 1; }
done
[[ $depends == *libssl3t64* || $depends == *libssl3* ]]
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
mkdir -p "$tmp/rpmdb"
dpkg-deb -x "$deb" "$tmp/deb"
[[ -x $tmp/deb/opt/aioncl/launcher/aioncl-launcher ]]
[[ -s $tmp/deb/opt/aioncl/launcher/assets/aioncl-icon.png ]]
[[ -s $tmp/deb/usr/share/applications/aioncl-launcher.desktop ]]
[[ $(dpkg-deb -f "$deb" Version) == "$version" ]]
rpm --dbpath "$tmp/rpmdb" -qp --qf '%{NAME} %{VERSION}-%{RELEASE} %{ARCH}\n' "$rpm" | grep -Fx "aioncl-launcher 2.5.42-${rpm_release} x86_64"
rpm --dbpath "$tmp/rpmdb" -qp --requires "$rpm" | grep -Fx mono-core >/dev/null
rpm --dbpath "$tmp/rpmdb" -qp --requires "$rpm" | grep -Fx mono-winforms >/dev/null
rpm --dbpath "$tmp/rpmdb" -qp --requires "$rpm" | grep -Fx mono-mvc >/dev/null
rpm --dbpath "$tmp/rpmdb" -qp --requires "$rpm" | grep -Fx wine >/dev/null
echo PACKAGE_SMOKE_PASS
