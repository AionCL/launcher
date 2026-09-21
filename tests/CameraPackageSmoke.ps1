param([Parameter(Mandatory=$true)][string]$Launcher, [Parameter(Mandatory=$true)][string]$ManifestUrl)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom($Launcher)
$root = Join-Path ([IO.Path]::GetTempPath()) ('aioncl-package-' + [guid]::NewGuid().ToString('N'))
$network = New-Object AionCL.Network(30, $null)
try {
    $web = New-Object Net.WebClient
    try { $json = $web.DownloadString($ManifestUrl) } finally { $web.Dispose() }
    $serializer = New-Object Web.Script.Serialization.JavaScriptSerializer
    $manifest = $serializer.Deserialize($json, [AionCL.Manifest])
    $manifest.Validate('2.4.5')
    $package = $manifest.packages[-1]
    if ($package.files.Count -ne 1 -or $package.files[0].path -ne 'tools/AionCL.Camera.exe') {
        throw 'Expected appended camera-only package'
    }
    $downloader = New-Object AionCL.Downloader($network)
    $token = [Threading.CancellationToken]::None
    $archive = $downloader.Get($package, (Join-Path $root 'cache'), $null, $token).GetAwaiter().GetResult()
    [AionCL.Installation]::Extract($archive, $package, $root, $token, $null, 1, 1).GetAwaiter().GetResult()
    $helper = [AionCL.CameraSettings]::VerifyHelper($root, $manifest, $token).GetAwaiter().GetResult()
    $process = Start-Process $helper -ArgumentList '0 80 101' -Wait -PassThru -WindowStyle Hidden
    if ($process.ExitCode -ne 2) { throw 'Helper failed to reject invalid parameters' }
    'CAMERA_PACKAGE_PASS'
} finally {
    $network.Dispose()
    if (Test-Path $root) { Remove-Item -LiteralPath $root -Recurse -Force }
}
