using System;
using System.IO;
namespace AionCL {
public sealed class HitFont {
    readonly byte[] bytes;
    readonly string hash;
    public HitFont(byte[] data) { bytes=(byte[])data.Clone(); hash=Safety.Sha(bytes); }
    public static HitFont Load() { using(var stream=typeof(HitFont).Assembly.GetManifestResourceStream("AionCL.hit-font.pak")) using(var memory=new MemoryStream()) { stream.CopyTo(memory);return new HitFont(memory.ToArray()); } }
    string Target(string root,string language) { if(language!="FRA"&&language!="ENG"&&language!="DEU")throw new InvalidDataException("Unsupported language.");return Safety.Under(root,"L10N/"+language+"/textures/ui/hit_number.pak"); }
    public bool Installed(string root,string language) { return File.Exists(Target(root,language)); }
    public bool Valid(string root,string language) { string p=Target(root,language);return File.Exists(p)&&new FileInfo(p).Length==bytes.Length&&Safety.Sha(File.ReadAllBytes(p))==hash; }
    public void Change(string root,string language,bool install) {
        VoiceMode.RequireGameClosed();Directory.CreateDirectory(Safety.Under(root,".aioncl"));
        using(var gate=new FileStream(Safety.Under(root,".aioncl/install.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)) ChangeFiles(root,language,install);
    }
    internal void ChangeFiles(string root,string language,bool install) {
        string target=Target(root,language);
        if(File.Exists(target)&&!Valid(root,language))throw new IOException("A different hit font file exists. It was preserved.");
        if(install&&!File.Exists(target)) {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temp=Safety.Under(root,".aioncl/hit-font-"+Guid.NewGuid().ToString("N")+".tmp");Directory.CreateDirectory(Path.GetDirectoryName(temp));
            try { File.WriteAllBytes(temp,bytes);File.Move(temp,target); }finally{if(File.Exists(temp))File.Delete(temp);}
        } else if(!install&&File.Exists(target)) {
            string backup=Safety.Under(root,".aioncl/hit-font-removed/"+Guid.NewGuid().ToString("N")+".pak");Directory.CreateDirectory(Path.GetDirectoryName(backup));File.Move(target,backup);
        }
    }
}
}
