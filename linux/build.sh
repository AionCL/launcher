#!/usr/bin/env bash
set -euo pipefail
cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.."
out=out/linux
mkdir -p "$out"
refs=(-r:System.Net.Http -r:System.IO.Compression -r:System.IO.Compression.FileSystem -r:System.Web.Extensions -r:System.Windows.Forms -r:System.Drawing -r:System.Security)
core=(src/Core.cs src/CameraSettings.cs src/Updates.cs src/KoreanPack.cs src/HitFont.cs linux/Platform.cs linux/OpenSslSha256.cs)
resources=(-resource:assets/portal.png,AionCL.portal.png -resource:assets/classic-wings.jpg,AionCL.classic-wings.jpg -resource:config/korean-pack.json,AionCL.korean-pack.json -resource:config/japanese-pack.json,AionCL.japanese-pack.json -resource:assets/japanese-hit-font.pak,AionCL.hit-font.pak)
mcs -define:LINUX,CAMERA_UI -target:exe -out:"$out/AionCL.Launcher.Linux.exe" "${refs[@]}" "${resources[@]}" "${core[@]}" src/Program.cs src/LauncherUi.cs src/Localization.cs src/UpdateUi.cs src/JapanesePack.cs linux/LauncherAuth.cs
mcs -define:LINUX -out:"$out/AionCL.Tests.exe" "${refs[@]}" "${core[@]}" tests/tests.cs tests/RegressionTests.cs
mono "$out/AionCL.Tests.exe"
mcs -out:"$out/AionCL.LinuxTests.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/Tests.cs
xvfb-run -a mono "$out/AionCL.LinuxTests.exe"
mcs -out:"$out/AionCL.GameSmoke.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/GameSmoke.cs
mcs -out:"$out/AionCL.RecipeCheck.exe" "${refs[@]}" -r:"$out/AionCL.Launcher.Linux.exe" linux/RecipeCheck.cs
cp config/launcher.json "$out/launcher.json"
cp linux/aioncl-launcher "$out/"
cp LINUX.md "$out/"
chmod +x "$out/aioncl-launcher"
# Test binaries and screenshots stay in out/linux, outside the distributable.
tar -czf out/AionCL-Launcher-2.5.42-linux-preview.5.tar.gz -C "$out" AionCL.Launcher.Linux.exe launcher.json aioncl-launcher LINUX.md
(cd out && sha256sum AionCL-Launcher-2.5.42-linux-preview.5.tar.gz > AionCL-Launcher-2.5.42-linux-preview.5.tar.gz.sha256)
cat out/AionCL-Launcher-2.5.42-linux-preview.5.tar.gz.sha256
