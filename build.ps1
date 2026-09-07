param(
    [switch]$Tests,
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (!(Test-Path $compiler)) {
    throw '.NET Framework C# compiler is required.'
}

$out = Join-Path $root 'out'
if ($OutputDirectory) { $out = [IO.Path]::GetFullPath($OutputDirectory) }
New-Item -ItemType Directory -Force -Path $out | Out-Null

$coreFile    = Join-Path $root 'src\Core.cs'
$programFile = Join-Path $root 'src\Program.cs'
$testsFile   = Join-Path $root 'tests\Tests.cs'

$launcherConfig = Join-Path $root 'config\launcher.json'
$appConfig      = Join-Path $root 'config\app.config'

$launcherExe = Join-Path $out 'AionCL.Launcher.exe'
$testsExe    = Join-Path $out 'AionCL.Tests.exe'

if (!(Test-Path $coreFile)) {
    throw "Missing source file: $coreFile"
}

if (!(Test-Path $programFile)) {
    throw "Missing source file: $programFile"
}

$references = @(
    '/r:System.Net.Http.dll'
    '/r:System.IO.Compression.dll'
    '/r:System.IO.Compression.FileSystem.dll'
    '/r:System.Web.Extensions.dll'
    '/r:System.Windows.Forms.dll'
    '/r:System.Drawing.dll'
)

Write-Host "Building AionCL Launcher..."
Write-Host "Core    : $coreFile"
Write-Host "Program : $programFile"

& $compiler `
    /nologo `
    /target:winexe `
    "/out:$launcherExe" `
    @references `
    $coreFile `
    (Join-Path $root 'src\Updates.cs') `
    (Join-Path $root 'src\UpdateUi.cs') `
    (Join-Path $root 'src\KoreanPack.cs') `
    (Join-Path $root 'src\HitFont.cs') `
    $programFile `
    (Join-Path $root 'src\LauncherUi.cs') `
    (Join-Path $root 'src\Localization.cs') `
    "/resource:$(Join-Path $root 'assets\classic-battle.jpg'),AionCL.classic-battle.jpg" `
    "/resource:$(Join-Path $root 'config\korean-pack.json'),AionCL.korean-pack.json" `
    "/resource:$(Join-Path $root 'assets\japanese-hit-font.pak'),AionCL.hit-font.pak"

if ($LASTEXITCODE -ne 0) {
    throw 'Launcher compilation failed.'
}

$configBackup = Join-Path $root ('.local\backups\build-' + [Guid]::NewGuid().ToString('N'))
foreach ($existing in @((Join-Path $out 'launcher.json'), "$launcherExe.config", "$testsExe.config")) {
    if (Test-Path -LiteralPath $existing) {
        New-Item -ItemType Directory -Force -Path $configBackup | Out-Null
        Copy-Item -LiteralPath $existing -Destination $configBackup
    }
}
Copy-Item $launcherConfig (Join-Path $out 'launcher.json') -Force
Copy-Item $appConfig "$launcherExe.config" -Force

if ($Tests) {

    if (!(Test-Path $testsFile)) {
        throw "Missing test source file: $testsFile"
    }

    Write-Host "Building AionCL tests..."

    & $compiler `
        /nologo `
        /target:exe `
        "/out:$testsExe" `
        @references `
        $coreFile `
        (Join-Path $root 'src\Updates.cs') `
        (Join-Path $root 'src\KoreanPack.cs') `
        (Join-Path $root 'src\HitFont.cs') `
        $testsFile `
        (Join-Path $root 'tests\RegressionTests.cs')

    if ($LASTEXITCODE -ne 0) {
        throw 'Test compilation failed.'
    }

    Copy-Item $appConfig "$testsExe.config" -Force
}

Write-Host ""
Write-Host "Build successful."
Write-Host "Output: $out"
