using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace AionCLUpdater {
    static class Program {
        static int Main(string[] args) {
            try {
                string source=Value(args,"--source"), target=Value(args,"--target"), exe=Value(args,"--exe");
                int pid; if(!Int32.TryParse(Value(args,"--pid"),out pid)||String.IsNullOrWhiteSpace(source)||String.IsNullOrWhiteSpace(target)||String.IsNullOrWhiteSpace(exe)) throw new InvalidDataException("Paramètres de mise à jour invalides.");
                try { using(var process=Process.GetProcessById(pid)) process.WaitForExit(120000); } catch(ArgumentException) { }
                string backup=Path.Combine(Path.GetTempPath(),"AionCL-backup-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(backup);
                var replaced=new System.Collections.Generic.List<string>();
                try {
                    foreach(string file in Directory.GetFiles(source,"*",SearchOption.AllDirectories)) {
                        string relative=file.Substring(source.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Length).TrimStart(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
                        string destination=Path.Combine(target,relative); string old=Path.Combine(backup,relative);
                        string parent=Path.GetDirectoryName(destination); if(!String.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        if(File.Exists(destination)) { Directory.CreateDirectory(Path.GetDirectoryName(old)); File.Copy(destination,old,true); }
                        if(File.Exists(destination)) File.Delete(destination); File.Copy(file,destination,true); replaced.Add(relative);
                    }
                    Process.Start(new ProcessStartInfo { FileName=Path.Combine(target,exe),WorkingDirectory=target,UseShellExecute=true });
                } catch {
                    foreach(string relative in replaced) { string destination=Path.Combine(target,relative), old=Path.Combine(backup,relative); if(File.Exists(destination))File.Delete(destination); if(File.Exists(old)) { Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(old,destination,true); } }
                    throw;
                }
                TryDelete(source); TryDelete(backup); return 0;
            } catch(Exception ex) { try { File.WriteAllText(Path.Combine(Path.GetTempPath(),"AionCL-update-error.log"),ex.ToString()); } catch {} return 1; }
        }
        static string Value(string[] args,string name) { for(int i=0;i<args.Length-1;i++) if(String.Equals(args[i],name,StringComparison.OrdinalIgnoreCase)) return args[i+1]; return null; }
        static void TryDelete(string path) { try { if(Directory.Exists(path))Directory.Delete(path,true); } catch {} }
    }
}
