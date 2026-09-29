using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AionCL {
public static class VoicePackSources {
    // Resolve a whole catalogue once. A root can contain several languages or
    // versions: the expected hash, not enumeration order, selects each source.
    public static Task<Dictionary<string,string>> Resolve(string root,ClientFile[] files,CancellationToken token) {
        return Task.Run(()=>ResolveFiles(root,files,token),token);
    }
    static async Task<Dictionary<string,string>> ResolveFiles(string root,ClientFile[] files,CancellationToken token) {
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        if(String.IsNullOrWhiteSpace(root)||!Directory.Exists(root))return result;
        root=Path.GetFullPath(root);
        var wanted=files.GroupBy(f=>Path.GetFileName(f.path),StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.OrdinalIgnoreCase);
        var pending=new Stack<string>();pending.Push(root);
        while(pending.Count>0&&result.Count<files.Length) {
            token.ThrowIfCancellationRequested();string directory=pending.Pop();
            string[] entries;
            try {Safety.NoLinks(directory);entries=Directory.GetFileSystemEntries(directory);}
            catch(UnauthorizedAccessException){continue;}
            catch(IOException){continue;}
            catch(InvalidDataException){continue;}
            Array.Sort(entries,StringComparer.Ordinal);
            foreach(string path in entries) {
                token.ThrowIfCancellationRequested();
                try {
                    Safety.NoLinks(path);
                    if(Directory.Exists(path)){pending.Push(path);continue;}
                    ClientFile[] matches;
                    if(!wanted.TryGetValue(Path.GetFileName(path),out matches))continue;
                    string normalized=path.Replace('\\','/');
                    foreach(var file in matches) {
                        if(result.ContainsKey(file.path)||!normalized.EndsWith("/"+file.path,StringComparison.OrdinalIgnoreCase))continue;
                        if(await Safety.Matches(path,file.size,file.sha256,token).ConfigureAwait(false))result[file.path]=path;
                    }
                } catch(UnauthorizedAccessException) {}catch(IOException) {}catch(InvalidDataException) {}
            }
        }
        return result;
    }
    public static IEnumerable<string> Candidates(string client,string language,string kind,string preference) {
        if(kind!="korean"&&kind!="japanese")throw new ArgumentException("Unknown voice pack.");
        var clients=new List<string>{client};var sources=new List<string>();
        string data=Path.GetDirectoryName(preference),remembered=Path.Combine(data,kind+"-pack-source.txt");
        if(File.Exists(remembered)) {try{sources.Add(File.ReadAllText(remembered).Trim());}catch(IOException) {}catch(UnauthorizedAccessException) {}}
        if(Directory.Exists(data)) {
            foreach(string file in Directory.GetFiles(data,"launcher-path.txt*",SearchOption.TopDirectoryOnly).OrderByDescending(File.GetLastWriteTimeUtc)) {
                try {clients.Add(File.ReadAllText(file).Trim());}catch(IOException) {}catch(UnauthorizedAccessException) {}
            }
        }
        foreach(string root in clients.Where(p=>!String.IsNullOrWhiteSpace(p)&&Path.IsPathRooted(p)).Distinct(StringComparer.Ordinal)) {
            foreach(string installation in new[]{root,Path.Combine(root,"client")}) {
                sources.Add(Path.Combine(installation,".aioncl",kind+"-pack-cache"));
                sources.Add(Path.Combine(installation,"L10N",GameLanguage.Runtime(language),"sounds"));
                sources.Add(Path.Combine(installation,"l10n",GameLanguage.Runtime(language),"sounds"));
            }
        }
        // Selected/remembered game roots may also contain extracted pack folders.
        sources.AddRange(clients);
        return sources.Where(p=>!String.IsNullOrWhiteSpace(p)&&Path.IsPathRooted(p)&&Directory.Exists(p)).Distinct(StringComparer.Ordinal);
    }
    public static async Task<string> FindAutomatic(string client,string language,string kind,string preference,ClientFile[] files,CancellationToken token) {
        foreach(string root in Candidates(client,language,kind,preference)) {
            token.ThrowIfCancellationRequested();
            if((await Resolve(root,files,token).ConfigureAwait(false)).Count==files.Length)return root;
        }
        return null;
    }
}
}
