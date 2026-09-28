#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
out=out/linux
mkdir -p "$out"
preview=${AIONCL_PREVIEW_NUMBER:-25}
[[ $preview =~ ^[0-9]+$ ]] || { echo 'Invalid Linux preview number.' >&2; exit 1; }
cat >"$out/LinuxBuildInfo.cs" <<EOF
namespace AionCL
{
    internal static class LinuxBuildInfo
    {
        internal const string PreviewNumber = "$preview";
    }
}
EOF
refs=(-r:System.Net.Http -r:System.IO.Compression -r:System.IO.Compression.FileSystem -r:System.Web.Extensions -r:System.Windows.Forms -r:System.Drawing -r:System.Security)
core=(src/Core.cs src/CameraSettings.cs src/Updates.cs src/KoreanPack.cs src/HitFont.cs linux/Platform.cs linux/OpenSslSha256.cs)
resources=(-resource:assets/portal.png,AionCL.portal.png -resource:assets/classic-wings.jpg,AionCL.classic-wings.jpg -resource:config/korean-pack.json,AionCL.korean-pack.json -resource:config/japanese-pack.json,AionCL.japanese-pack.json -resource:assets/japanese-hit-font.pak,AionCL.hit-font.pak)
mcs -define:LINUX,CAMERA_UI -target:exe -out:"$out/AionCL.Launcher.Linux.exe" "${refs[@]}" "${resources[@]}" "${core[@]}" "$out/LinuxBuildInfo.cs" src/Program.cs src/LauncherUi.cs src/Localization.cs src/UpdateUi.cs src/JapanesePack.cs linux/LauncherAuth.cs
mcs -define:LINUX -out:"$out/AionCL.Tests.exe" "${refs[@]}" "${core[@]}" tests/tests.cs tests/RegressionTests.cs
mono "$out/AionCL.Tests.exe"
mcs -out:"$out/AionCL.LinuxTests.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/Tests.cs
xvfb-run -a mono "$out/AionCL.LinuxTests.exe"
mcs -out:"$out/AionCL.GameSmoke.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/GameSmoke.cs
mcs -out:"$out/AionCL.RecipeCheck.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/RecipeCheck.cs
cp config/launcher.json "$out/launcher.json"
mkdir -p "$out/assets"
cp assets/aioncl-icon.png "$out/assets/aioncl-icon.png"
cp linux/aioncl-launcher linux/aioncl-camera linux/install-d3dx9.sh linux/install-dxvk.sh linux/install-d3dcompiler.sh linux/prepare-linux-runtime.sh "$out/"
cp LINUX.md "$out/"
chmod +x "$out/aioncl-launcher" "$out/aioncl-camera" "$out/install-d3dx9.sh" "$out/install-dxvk.sh" "$out/install-d3dcompiler.sh" "$out/prepare-linux-runtime.sh"
package=$(mktemp -d "$out/package-preview${preview}.XXXXXX")
trap 'rm -rf "$package"' EXIT
mkdir -p "$package/assets"
cp "$out/AionCL.Launcher.Linux.exe" "$out/launcher.json" "$out/aioncl-launcher" "$out/aioncl-camera" \
   "$out/install-d3dx9.sh" "$out/install-dxvk.sh" "$out/install-d3dcompiler.sh" "$out/LINUX.md" "$package/"
cp "$out/prepare-linux-runtime.sh" "$package/"
cp "$out/assets/aioncl-icon.png" "$package/assets/"
chmod +x "$package/aioncl-launcher" "$package/aioncl-camera" "$package/install-d3dx9.sh" "$package/install-dxvk.sh" "$package/install-d3dcompiler.sh" "$package/prepare-linux-runtime.sh"
linux/camera-bridge-smoke.sh
# Test binaries and screenshots stay in out/linux, outside the distributable.
tar -czf "out/AionCL-Launcher-2.5.42-linux-preview.${preview}.tar.gz" -C "$out" AionCL.Launcher.Linux.exe launcher.json assets/aioncl-icon.png aioncl-launcher aioncl-camera install-d3dx9.sh install-dxvk.sh install-d3dcompiler.sh prepare-linux-runtime.sh LINUX.md
(cd out && sha256sum "AionCL-Launcher-2.5.42-linux-preview.${preview}.tar.gz" > "AionCL-Launcher-2.5.42-linux-preview.${preview}.tar.gz.sha256")
cat "out/AionCL-Launcher-2.5.42-linux-preview.${preview}.tar.gz.sha256"
linux/package-deb.sh "$package" out
linux/package-rpm.sh "$package" out
linux/package-smoke.sh out
