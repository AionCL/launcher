param()
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
& (Join-Path $root 'build.ps1') -Tests
& (Join-Path $root 'out\AionCL.Tests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; distribution cancelled.' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss-fff'
$stage = Join-Path $root "out\distribution-$stamp"
New-Item -ItemType Directory -Path $stage | Out-Null
foreach ($name in @('AionCL.Launcher.exe', 'AionCL.Launcher.exe.config', 'launcher.json')) {
    Copy-Item -LiteralPath (Join-Path $root "out\$name") -Destination $stage
}
Copy-Item -LiteralPath (Join-Path $root 'DISTRIBUTION.md') -Destination $stage
$zip = Join-Path $root "out\AionCL-Launcher-$stamp.zip"
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Output "Launcher package: $zip"
