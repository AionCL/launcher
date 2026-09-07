using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AionCL {
public sealed class KoreanPack {
    readonly ClientFile[] files;
    public KoreanPack(ClientFile[] catalog) {
        if (catalog == null || catalog.Length == 0) throw new InvalidDataException("Empty voice catalogue.");
        foreach(var f in catalog) { Safety.Relative(f.path); Safety.Hash(f.sha256); if(f.size<0 || !f.path.EndsWith(".pak",StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(f.path).StartsWith("z",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid voice catalogue."); }
        if(catalog.Select(f=>f.path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=catalog.Length) throw new InvalidDataException("Duplicate voice path.");
        files=catalog;
    }
    public static KoreanPack Load() { using(var s=typeof(KoreanPack).Assembly.GetManifestResourceStream("AionCL.korean-pack.json")) using(var r=new StreamReader(s)) return new KoreanPack(Json.Parse<ClientFile[]>(r.ReadToEnd())); }
    static string Prefix(string language) { if(language!="FRA"&&language!="ENG"&&language!="DEU")throw new InvalidDataException("Unsupported language.");return "L10N/"+language+"/sounds/"; }
    string Active(string root,string language,ClientFile f){return Safety.Under(root,Prefix(language)+f.path);}
    string Cached(string root,ClientFile f){return Safety.Under(root,".aioncl/korean-pack-cache/"+f.path);}
    string Marker(string root,string language){Prefix(language);return Safety.Under(root,".aioncl/korean-pack-"+language+".json");}
    public int Present(string root,string language){return files.Count(f=>File.Exists(Active(root,language,f)));}
    public bool HasPack(string root,string language){return Present(root,language)>0 || File.Exists(Marker(root,language));}
    public bool HasCache(string root){return files.All(f=>File.Exists(Cached(root,f)));}
    public string PreviousSource(string root,string language){
        var path=Safety.Under(root,".aioncl/korean-trial-"+language+".json");
        if(!File.Exists(path))return null;
        try {var record=Json.Parse<Trial>(File.ReadAllText(path));var first=record.files[0];var relative=first.path.Substring(Prefix(language).Length).Replace('/',Path.DirectorySeparatorChar); if(!first.source.EndsWith(relative,StringComparison.OrdinalIgnoreCase))return null;var folder=first.source.Substring(0,first.source.Length-relative.Length).TrimEnd(Path.DirectorySeparatorChar);return Directory.Exists(folder)?folder:null;}catch(Exception ex){if(ex is IOException||ex is ArgumentException||ex is NullReferenceException||ex is IndexOutOfRangeException)return null;throw;}
    }
    public sealed class Trial {public TrialFile[] files{get;set;}}
    public sealed class TrialFile {public string path{get;set;} public string source{get;set;}}
    public async Task<bool> Valid(string root,string language,CancellationToken token) {foreach(var f in files)if(!await Safety.Matches(Active(root,language,f),f.size,f.sha256,token).ConfigureAwait(false))return false;return true;}
    public async Task Change(string root,string language,bool install,string source,IProgress<VerificationProgress> progress,CancellationToken token) {
        VoiceMode.RequireGameClosed();
        string metadata=Safety.Under(root,".aioncl");Directory.CreateDirectory(metadata);
        using(var gate=new FileStream(Safety.Under(root,".aioncl/install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) {
            await ChangeFiles(root,language,install,source,progress,token).ConfigureAwait(false);
        }
    }
    internal async Task ChangeFiles(string root,string language,bool install,string source,IProgress<VerificationProgress> progress,CancellationToken token) {
        Prefix(language);
        // Preflight the entire set before changing any active file.
        foreach(var f in files) {
            token.ThrowIfCancellationRequested();var active=Active(root,language,f);
            if(File.Exists(active) && !await Safety.Matches(active,f.size,f.sha256,token).ConfigureAwait(false)) throw new IOException("Modified Korean pack file preserved: "+f.path);
            if(install && !File.Exists(active)) {
                var candidate=Cached(root,f);
                if(!await Safety.Matches(candidate,f.size,f.sha256,token).ConfigureAwait(false)) {
                    if(String.IsNullOrEmpty(source)||!await Safety.Matches(Safety.Under(source,f.path),f.size,f.sha256,token).ConfigureAwait(false)) throw new IOException("Korean source missing or invalid: "+f.path);
                }
            }
            if(!install && File.Exists(active) && File.Exists(Cached(root,f)) && !await Safety.Matches(Cached(root,f),f.size,f.sha256,token).ConfigureAwait(false)) throw new IOException("Modified Korean cache preserved: "+f.path);
        }
        var marker=Marker(root,language);
        if(File.Exists(marker))File.Copy(marker,marker+".bak-"+Guid.NewGuid().ToString("N"));
        Json.Atomic(marker,new {version=1,operation=install?"install":"remove"});
        int completed=0;
        foreach(var f in files) {
            token.ThrowIfCancellationRequested();var active=Active(root,language,f);var cache=Cached(root,f);
            if(install && !File.Exists(active)) {
                string candidate=await Safety.Matches(cache,f.size,f.sha256,token).ConfigureAwait(false)?cache:Safety.Under(source,f.path);
                Directory.CreateDirectory(Path.GetDirectoryName(active));
                var temp=Safety.Under(root,".aioncl/voice-copy-"+Guid.NewGuid().ToString("N")+".tmp");
                try {
                    using(var input=new FileStream(candidate,FileMode.Open,FileAccess.Read,FileShare.Read,131072,true))using(var output=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072,true))await input.CopyToAsync(output,131072,token).ConfigureAwait(false);
                    if(!await Safety.Matches(temp,f.size,f.sha256,token).ConfigureAwait(false))throw new IOException("Voice copy verification failed.");
                    File.Move(temp,active);
                } finally {if(File.Exists(temp))File.Delete(temp);}
            } else if(!install && File.Exists(active)) {
                Directory.CreateDirectory(Path.GetDirectoryName(cache));
                if(File.Exists(cache))File.Delete(active);else File.Move(active,cache);
            }
            completed++;if(progress!=null)progress.Report(new VerificationProgress{Path=f.path,Completed=completed,Total=files.Length});
        }
        if(!install)File.Delete(marker);
    }
}
}
