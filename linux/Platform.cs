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
