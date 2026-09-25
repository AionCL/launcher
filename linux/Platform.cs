using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace AionCL {
public sealed class LinuxUiProgress<T> : IProgress<T>, IDisposable {
    readonly object gate = new object();
    readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
    readonly System.Windows.Forms.Control owner;
    readonly Action<T> callback;
    T latest; bool pending, disposed;
    public LinuxUiProgress(System.Windows.Forms.Control owner, Action<T> callback) {
        this.owner=owner; this.callback=callback;
        owner.Disposed += OwnerDisposed;
        timer.Tick += delegate {
            T value;
            lock(gate) { if(disposed || !pending)return; value=latest; pending=false; }
            callback(value);
        };
        timer.Start();
    }
    public void Report(T value) { lock(gate) { if(disposed)return; latest=value; pending=true; } }
    void OwnerDisposed(object sender, EventArgs args) { Dispose(); }
    public void Dispose() {
        lock(gate) { if(disposed)return; disposed=true; pending=false; latest=default(T); }
        timer.Stop(); timer.Dispose(); owner.Disposed -= OwnerDisposed;
    }
}
public static class LinuxPlatform {
    public static System.Security.Cryptography.SHA256 CreateSha256() {
        try { return new OpenSslSha256(); }
        catch(DllNotFoundException) { return System.Security.Cryptography.SHA256.Create(); }
        catch(EntryPointNotFoundException) { return System.Security.Cryptography.SHA256.Create(); }
    }
    public static string ResolveClientPath(string root,string relative) {
        string current=Path.GetFullPath(root);
        Safety.NoLinks(current);
        foreach(string part in relative.Split('/')) {
            string next=Path.Combine(current,part);
            if(!File.Exists(next)&&!Directory.Exists(next)&&Directory.Exists(current)) {
                string match=null;
                foreach(string entry in Directory.EnumerateFileSystemEntries(current)) {
                    if(!String.Equals(Path.GetFileName(entry),part,StringComparison.OrdinalIgnoreCase))continue;
                    if(match!=null)throw new IOException("Ambiguous filename casing: "+next);
                    match=entry;
                }
                if(match!=null)next=match;
            }
            Safety.NoLinks(next); current=next;
        }
        return current;
    }
    public static async System.Threading.Tasks.Task ExtractPackages(InstallPlan plan,string cache,string stage,
        System.Threading.CancellationToken token,IProgress<ExtractionProgress> progress,Action<string> log) {
        // Create common directories serially, including their canonical casing.
        foreach(var package in plan.Packages)foreach(var file in package.files)
            Directory.CreateDirectory(Path.GetDirectoryName(Safety.Under(stage,file.path)));
        var wave=new System.Collections.Generic.List<System.Threading.Tasks.Task>();
        var paths=new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var limit=new System.Threading.SemaphoreSlim(Math.Min(4,Math.Max(1,Environment.ProcessorCount)))) {
            for(int index=0;index<plan.Packages.Length;index++) {
                var package=plan.Packages[index]; bool overlaps=false;
                foreach(var file in package.files)if(paths.Contains(file.path)){overlaps=true;break;}
                if(overlaps) { await System.Threading.Tasks.Task.WhenAll(wave).ConfigureAwait(false); wave.Clear();paths.Clear(); }
                foreach(var file in package.files)paths.Add(file.path);
                int number=index+1;
                wave.Add(System.Threading.Tasks.Task.Run(async delegate {
                    await limit.WaitAsync(token).ConfigureAwait(false);
                    try {
                        log("Extraction : "+package.name);
                        await Installation.Extract(Safety.Under(cache,package.name),package,stage,token,progress,number,plan.Packages.Length).ConfigureAwait(false);
                    } finally {limit.Release();}
                }));
            }
            await System.Threading.Tasks.Task.WhenAll(wave).ConfigureAwait(false);
        }
    }
    public static long DownloadBytesNeeded(Package[] packages, string cache) {
        long remaining=0;
        foreach(var package in packages) {
            string archive=Safety.Under(cache,package.name), part=Safety.Under(cache,package.name+".part");
            long occupied=Math.Max(File.Exists(archive)?new FileInfo(archive).Length:0,
                                   File.Exists(part)?new FileInfo(part).Length:0);
            remaining=checked(remaining+Math.Max(0,package.size-occupied));
        }
        return remaining;
    }
    public static DriveInfo InstallationDrive(string path) {
        string full=Path.GetFullPath(path).TrimEnd('/') + "/";
        DriveInfo best=null;
        foreach(var drive in DriveInfo.GetDrives()) {
            string prefix=drive.Name.TrimEnd('/') + "/";
            if(full.StartsWith(prefix,StringComparison.Ordinal) && (best==null || drive.Name.Length>best.Name.Length)) best=drive;
        }
        if(best==null)throw new IOException("Installation filesystem not found.");
        return best;
    }
    public static string Quote(string value) {
        if (value == null || value.IndexOf('\0') >= 0 || value.IndexOf('\n') >= 0 || value.IndexOf('\r') >= 0)
            throw new ArgumentException("Invalid process argument.");
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
    public static ProcessStartInfo WineCommand(ProcessStartInfo game) {
        string runner = Environment.GetEnvironmentVariable("AIONCL_WINE") ?? "wine";
        string prefix = Environment.GetEnvironmentVariable("AIONCL_WINEPREFIX") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AionCL", "wine");
        if (!Path.IsPathRooted(prefix)) throw new ArgumentException("AIONCL_WINEPREFIX must be an absolute path.");
        var result = new ProcessStartInfo {
            FileName = runner, Arguments = Quote(game.FileName) + " " + game.Arguments,
            WorkingDirectory = game.WorkingDirectory, UseShellExecute = false
        };
        result.EnvironmentVariables["WINEPREFIX"] = prefix;
        // The shipped No-IP patch is a version.dll proxy. Wine otherwise loads
        // its builtin DLL and Aion rejects the game server locally with error 6.
        string overrides = result.EnvironmentVariables["WINEDLLOVERRIDES"];
        result.EnvironmentVariables["WINEDLLOVERRIDES"] =
            (String.IsNullOrWhiteSpace(overrides) ? "" : overrides.TrimEnd(';') + ";") + "version=n,b";
        return result;
    }
    public static void CreateShortcut() {
        string data = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (String.IsNullOrEmpty(data) || !Path.IsPathRooted(data))
            data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".local", "share");
        string target = Path.Combine(data, "applications", "aioncl-launcher.desktop");
        string script = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "aioncl-launcher");
        // Desktop Exec has its own escaping rules, independent of shell quoting.
        string exec = Quote(script).Replace("`", "\\`").Replace("$", "\\$").Replace("%", "%%");
        Directory.CreateDirectory(Path.GetDirectoryName(target));
        File.WriteAllText(target, "[Desktop Entry]\nType=Application\nName=AionCL Launcher (Linux preview)\nExec=" + exec + "\nTerminal=false\nCategories=Game;\n", new UTF8Encoding(false));
    }
}
}
