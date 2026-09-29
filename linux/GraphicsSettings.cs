using System;
using System.IO;
namespace AionCL {
public sealed class LinuxGraphicsSettings {
    public bool nativeD3dx { get; set; }
    public LinuxGraphicsSettings() { nativeD3dx=true; }
    public static string PreferencePath {
        get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","linux-graphics.json"); }
    }
    public static LinuxGraphicsSettings Load() { return Load(PreferencePath); }
    public static LinuxGraphicsSettings Load(string path) {
        if(!File.Exists(path))return new LinuxGraphicsSettings();
        var settings=Json.Parse<LinuxGraphicsSettings>(File.ReadAllText(path));
        if(settings==null)throw new InvalidDataException("Invalid Linux graphics settings.");
        return settings;
    }
    public void Save(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".new";File.WriteAllText(temp,Json.Serialize(this));
        if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
    }
}
}
