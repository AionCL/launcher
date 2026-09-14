using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace AionCL {
public sealed class LauncherAuth {
    readonly string credentials=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","launcher-credentials.bin");
    public Tuple<string,string> SavedCredentials { get { try { if(!File.Exists(credentials))return null; var value=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(credentials),null,DataProtectionScope.CurrentUser)).Split(new[]{'\n'},2); return value.Length==2?Tuple.Create(value[0],value[1]):null; } catch { return null; } } }
    public void ForgetCredentials(){try{if(File.Exists(credentials))File.Delete(credentials);}catch{}}
    public void SaveCredentials(string username,string password,bool remember){if(!remember){ForgetCredentials();return;}Directory.CreateDirectory(Path.GetDirectoryName(credentials));File.WriteAllBytes(credentials,ProtectedData.Protect(Encoding.UTF8.GetBytes(username+"\n"+password),null,DataProtectionScope.CurrentUser));}
}
}
