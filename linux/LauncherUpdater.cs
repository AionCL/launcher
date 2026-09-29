using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace AionCL {
public sealed class LinuxUpdateAsset {
    public string url {get;set;}
    public string sha256 {get;set;}
    public void Validate(string extension) {
        var uri=Safety.Https(url);Safety.Hash(sha256);
        if(uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/AionCL/launcher/releases/download/",StringComparison.Ordinal) || !uri.AbsolutePath.EndsWith(extension,StringComparison.Ordinal))
            throw new InvalidDataException("Invalid Linux launcher package URL.");
    }
}
public sealed class LinuxLauncherRelease {
    public int schemaVersion {get;set;}
    public string version {get;set;}
    public int preview {get;set;}
    public LinuxUpdateAsset deb {get;set;}
    public LinuxUpdateAsset rpm {get;set;}
    public LinuxUpdateAsset portable {get;set;}
    public void Validate() {
        if(schemaVersion!=1||preview<1||deb==null||rpm==null||portable==null)throw new InvalidDataException("Invalid Linux update feed.");
        Updates.Version(version);deb.Validate(".deb");rpm.Validate(".rpm");portable.Validate(".zip");
    }
    public bool IsNewer(string version,int preview) { return Updates.Newer(this.version,version)||(this.version==version&&this.preview>preview); }
}
public static class LinuxLauncherUpdater {
    public const string FeedUrl="https://raw.githubusercontent.com/AionCL/launcher/feat/linux-launcher/config/linux-updates.json";
    public static async Task<LinuxLauncherRelease> Fetch(Network network,CancellationToken token) {
        var result=Json.Parse<LinuxLauncherRelease>(Encoding.UTF8.GetString(await network.Small(FeedUrl,token).ConfigureAwait(false)));
        if(result==null)throw new InvalidDataException("Empty Linux update feed.");result.Validate();return result;
    }
    public static string PackageKind(string root) {
        if(Path.GetFullPath(root).TrimEnd('/')!="/opt/aioncl/launcher")return "portable";
        if(File.Exists("/usr/bin/dpkg-query") && Installed("/usr/bin/dpkg-query","-W -f=${Status} aioncl-launcher","install ok installed"))return "deb";
        if(File.Exists("/usr/bin/rpm") && Installed("/usr/bin/rpm","-q aioncl-launcher",null))return "rpm";
        throw new InvalidOperationException("This system installation is not managed by a supported package manager.");
    }
    static bool Installed(string tool,string args,string expected) {
        using(var p=Process.Start(new ProcessStartInfo {FileName=tool,Arguments=args,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true})) {
            if(p==null)return false;
            var error=p.StandardError.ReadToEndAsync();string output=p.StandardOutput.ReadToEnd();p.WaitForExit();error.GetAwaiter().GetResult();
            return p.ExitCode==0 && (expected==null||output.Trim()==expected);
        }
    }
    public static ProcessStartInfo InstallCommand(string kind,string package) {
        if(kind!="deb"&&kind!="rpm")throw new ArgumentException("Unsupported package kind.");
        if(!Path.IsPathRooted(package))throw new ArgumentException("Absolute package path required.");
        return new ProcessStartInfo {FileName="pkexec",Arguments=(kind=="deb"?"/usr/bin/apt-get install --yes ":"/usr/bin/dnf install --assumeyes ")+LinuxPlatform.Quote(package),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
    }
    public static async Task RunInstaller(ProcessStartInfo start,Action<string> log) {
        using(var p=Process.Start(start)) {
            if(p==null)throw new IOException("Could not start the package installer.");
            p.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)log(e.Data);};
            p.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)log(e.Data);};
            p.BeginOutputReadLine();p.BeginErrorReadLine();
            // Do not kill a package manager midway through installation.
            while(!p.HasExited)await Task.Delay(150).ConfigureAwait(false);
            p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Launcher package installation failed or authorization was cancelled (exit "+p.ExitCode+").");
        }
    }
    static readonly HashSet<string> PayloadFiles=new HashSet<string>(new[]{"AionCL.Launcher.Linux.exe","launcher.json","assets/aioncl-icon.png","aioncl-launcher","aioncl-camera","install-d3dx9.sh","install-dxvk.sh","install-d3dcompiler.sh","prepare-linux-runtime.sh","LINUX.md"},StringComparer.Ordinal);
    public static void ExtractPortable(string zip,string destination) {
        Safety.NoLinks(destination);Directory.CreateDirectory(destination);
        using(var archive=ZipFile.OpenRead(zip)) {
            var seen=new HashSet<string>(StringComparer.Ordinal);long total=0;
            foreach(var e in archive.Entries) {
                int type=(e.ExternalAttributes>>16)&0xf000;
                if(!PayloadFiles.Contains(e.FullName)||!seen.Add(e.FullName)||(type!=0&&type!=0x8000)||e.Length<=0)
                    throw new InvalidDataException("Unexpected portable launcher archive entry: "+e.FullName);
                total=checked(total+e.Length);if(total>50*1024*1024)throw new InvalidDataException("Portable launcher archive too large.");
            }
            if(seen.Count!=PayloadFiles.Count)throw new InvalidDataException("Incomplete portable launcher archive.");
            foreach(var e in archive.Entries) {
                string path=Safety.Under(destination,e.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path));e.ExtractToFile(path);
            }
        }
        foreach(string file in PayloadFiles)if(file.EndsWith(".sh",StringComparison.Ordinal)||file=="aioncl-launcher"||file=="aioncl-camera") {
            using(var p=Process.Start("chmod","0755 "+LinuxPlatform.Quote(Path.Combine(destination,file)))) {p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Could not set launcher permissions.");}
        }
    }
    public static string ApplyPortable(string zip,string root) {
        root=Path.GetFullPath(root).TrimEnd('/');Safety.NoLinks(root);
        string next=root+"-update-"+Guid.NewGuid().ToString("N"),backup=root+"-backup-"+Guid.NewGuid().ToString("N");
        try {
            ExtractPortable(zip,next);
            // Preserve installation-specific server/client configuration.
            if(File.Exists(Path.Combine(root,"launcher.json")))File.Copy(Path.Combine(root,"launcher.json"),Path.Combine(next,"launcher.json"),true);
            Directory.Move(root,backup);
            try {Directory.Move(next,root);}catch {Directory.Move(backup,root);throw;}
            return backup;
        } finally {if(Directory.Exists(next))Directory.Delete(next,true);}
    }
    public static void RestartAfterExit(string root) {
        string helper=Path.Combine(Path.GetTempPath(),"aioncl-restart-"+Guid.NewGuid().ToString("N")+".sh");
        File.WriteAllText(helper,"#!/bin/bash\nfor ((i=0;i<120;i++)); do\n  if ! kill -0 \"$1\" 2>/dev/null; then exec bash \"$2/aioncl-launcher\"; fi\n  sleep 1\ndone\n",new UTF8Encoding(false));
        var start=new ProcessStartInfo {FileName="/bin/bash",Arguments=LinuxPlatform.Quote(helper)+" "+Process.GetCurrentProcess().Id+" "+LinuxPlatform.Quote(root),UseShellExecute=false};
        if(Process.Start(start)==null)throw new IOException("Could not schedule launcher restart.");
    }
}
}
