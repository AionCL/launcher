#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
payload=${1:-out/linux}
destination=${2:-out}
release=${AIONCL_RPM_RELEASE:-0.preview26}
preview=${AIONCL_PREVIEW_NUMBER:-26}
[[ $release =~ ^[A-Za-z0-9._+-]+$ ]] || { echo 'Invalid RPM package release.' >&2; exit 1; }
[[ $preview =~ ^[0-9]+$ ]] || { echo 'Invalid Linux preview number.' >&2; exit 1; }
[[ -f $payload/AionCL.Launcher.Linux.exe && -x $payload/aioncl-launcher && -f $payload/assets/aioncl-icon.png ]]
command -v rpmbuild >/dev/null || { echo 'rpmbuild is required.' >&2; exit 1; }
mkdir -p "$destination"
top=$(mktemp -d)
trap 'rm -rf "$top"' EXIT
mkdir -p "$top"/{BUILD,BUILDROOT,RPMS,SOURCES,SPECS,SRPMS,rpmdb}
tar -czf "$top/SOURCES/aioncl-payload.tar.gz" -C "$payload" .
sed "s/@PREVIEW@/$preview/g" linux/aioncl-launcher.desktop > "$top/SOURCES/aioncl-launcher.desktop"
cat >"$top/SPECS/aioncl-launcher.spec" <<EOF
Name:           aioncl-launcher
Version:        2.5.42
Release:        $release%{?dist}
Summary:        AionCL launcher for Linux
License:        Proprietary
BuildArch:      x86_64
Requires:       mono-core, mono-winforms, mono-mvc
Requires:       libgdiplus, dejavu-sans-fonts, xdg-utils, ca-certificates
Requires:       openssl-libs, wine
Requires:       cabextract, curl, procps-ng, tar, polkit, dnf
Source0:        aioncl-payload.tar.gz
Source1:        aioncl-launcher.desktop

%description
Native Linux launcher for installing and updating Aion Classic, with Wine
integration for running the game client.

%prep
mkdir -p aioncl-payload
tar -xzf %{SOURCE0} -C aioncl-payload

%build

%install
mkdir -p %{buildroot}/opt/aioncl/launcher
cp -a aioncl-payload/. %{buildroot}/opt/aioncl/launcher/
chmod 0755 %{buildroot}/opt/aioncl %{buildroot}/opt/aioncl/launcher
install -D -m 0644 %{SOURCE1} %{buildroot}/usr/share/applications/aioncl-launcher.desktop

%files
/opt/aioncl/launcher
/usr/share/applications/aioncl-launcher.desktop

%post
chmod 0755 /opt/aioncl/launcher /opt/aioncl/launcher/aioncl-launcher

%changelog
* Sun Sep 27 2026 AionCL Project <support@aioncl.example> - 2.5.42-$release
- Add persistent Linux credentials, reduced-priority camera helper and native packages.
EOF
rpmbuild --dbpath "$top/rpmdb" --define "_topdir $top" --define '_build_id_links none' -bb "$top/SPECS/aioncl-launcher.spec" >/dev/null
find "$top/RPMS" -type f -name '*.rpm' -exec cp {} "$destination/" \;
echo "Created $destination/aioncl-launcher-2.5.42-${release}.x86_64.rpm"
