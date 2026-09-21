param(
    [Parameter(Mandatory=$true)][string]$Launcher,
    [string]$Client = 'D:\games\aioncl-recette'
)
$ErrorActionPreference = 'Stop'
# Runs without credentials and does not enter a character. No client file patch.
if (Get-Process aionclassic* -ErrorAction SilentlyContinue) { throw 'Aion already running; test refused' }
$gameDll = Join-Path $Client 'bin64\Game.dll'
if ((Get-FileHash $gameDll).Hash.ToLowerInvariant() -ne 'f259b60f74768c226eafff085551700bcaf3aa43a20ede7fe3925e0e31daaf0f') {
    throw 'Unrecognized Game.dll; refusing version-specific memory read'
}
Add-Type -AssemblyName System.Web.Extensions
[void][Reflection.Assembly]::LoadFrom($Launcher)
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ShopFlagsReader {
    [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
    [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr process, IntPtr address, byte[] data, UIntPtr length, out UIntPtr read);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    public static byte[] Read(int id, long address) {
        IntPtr handle = OpenProcess(0x410, false, id);
        if (handle == IntPtr.Zero) throw new Exception("OpenProcess failed");
        try {
            byte[] data = new byte[16]; UIntPtr read;
            if (!ReadProcessMemory(handle, new IntPtr(address), data, new UIntPtr(16), out read) || read.ToUInt64() != 16)
                throw new Exception("Flag read failed");
            return data;
        } finally { CloseHandle(handle); }
    }
}
'@
$serializer = New-Object Web.Script.Serialization.JavaScriptSerializer
$config = $serializer.Deserialize((Get-Content (Join-Path (Split-Path $Launcher) 'launcher.json') -Raw), [AionCL.LauncherConfig])
# Also exercise a stale external configuration, using the released launch builder.
$config.launchArguments += ' -shop -ingameshop -ingamewebshop'
$server = New-Object AionCL.ServerConfig
$server.version = 1; $server.serverName = 'AionCL'; $server.loginHost = 'aioncl.freeddns.org'
$server.loginPort = 2106; $server.gamePort = 7777; $server.maintenance = $false
$ip = [Net.Dns]::GetHostAddresses($server.loginHost) | Where-Object { $_.AddressFamily -eq 'InterNetwork' } | Select-Object -First 1
if (!$ip) { throw 'No server IPv4' }
$builder = New-Object AionCL.GameLauncher($config)
$command = $builder.BuildLaunchCommand($Client, $server, $ip)
if ($command.Arguments -match '(?i)(?<!\S)-(shop|ingameshop|ingamewebshop)(?!\S)') { throw 'Shop activator remains' }
$process = $null
try {
    $process = [Diagnostics.Process]::Start($command)
    $started = $process.StartTime
    $deadline = [datetime]::UtcNow.AddSeconds(90)
    $validSince = $null
    do {
        Start-Sleep -Seconds 2
        $process.Refresh()
        if ($process.HasExited) { throw "Client exited: $($process.ExitCode)" }
        $module = $process.Modules | Where-Object { $_.ModuleName -ieq 'Game.dll' } | Select-Object -First 1
        if (!$module) { continue }
        if ($module.FileName -ine $gameDll) { throw 'Unexpected game module path' }
        # CGame singleton RVA 0xe42670 + native flags offset 0xfa8 (two 64-bit words).
        $flags = [ShopFlagsReader]::Read($process.Id, $module.BaseAddress.ToInt64() + 0xe43618)
        $low = [BitConverter]::ToUInt64($flags, 0)
        $high = [BitConverter]::ToUInt64($flags, 8)
        if (($high -band 0x4800) -eq 0x4800 -and ($low -band 0x400000000000) -eq 0) {
            if (!$validSince) { $validSince = [datetime]::UtcNow }
            if (([datetime]::UtcNow - $validSince).TotalSeconds -ge 20) {
                "NATIVE_SHOPS_PASS disabled75=True disabled78=True web46=False alive=True pid=$($process.Id)"
                return
            }
        } else { $validSince = $null }
    } while ([datetime]::UtcNow -lt $deadline)
    throw ('Native shop flags not confirmed within 90 seconds: low={0:X16} high={1:X16}' -f $low,$high)
} finally {
    if ($process) {
        $process.Refresh()
        if (!$process.HasExited -and $process.StartTime -eq $started -and $process.Path -ieq $command.FileName) {
            [void]$process.CloseMainWindow()
            if (!$process.WaitForExit(5000)) { $process.Kill(); $process.WaitForExit() }
        }
        $process.Dispose()
        'TEST_PROCESS_CLOSED'
    }
}
