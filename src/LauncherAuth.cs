using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace AionCL {
public sealed class SavedAccount { public string username { get; set; } public string password { get; set; } }
public sealed class LauncherAuth {
    readonly string credentials=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","launcher-credentials.bin");
    public List<SavedAccount> Accounts { get { try { if(!File.Exists(credentials))return new List<SavedAccount>(); var clear=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(credentials),null,DataProtectionScope.CurrentUser)); try { var accounts=Json.Parse<List<SavedAccount>>(clear); if(accounts!=null)return accounts; } catch { } var legacy=clear.Split(new[]{'\n'},2); if(legacy.Length==2&&!String.IsNullOrWhiteSpace(legacy[0]))return new List<SavedAccount> { new SavedAccount { username=legacy[0],password=legacy[1] } }; } catch { } return new List<SavedAccount>(); } }
    public SavedAccount Find(string username) { foreach(var account in Accounts) if(String.Equals(account.username,username,StringComparison.OrdinalIgnoreCase))return account; return null; }
    public void ForgetCredentials(){try{if(File.Exists(credentials))File.Delete(credentials);}catch{}}
    public void ForgetCredential(string username){var accounts=Accounts;accounts.RemoveAll(a=>String.Equals(a.username,username,StringComparison.OrdinalIgnoreCase));Write(accounts);}
    public void SaveCredentials(string username,string password,bool remember){var accounts=Accounts;accounts.RemoveAll(a=>String.Equals(a.username,username,StringComparison.OrdinalIgnoreCase));if(remember)accounts.Insert(0,new SavedAccount { username=username,password=password });Write(accounts);}
    void Write(List<SavedAccount> accounts){if(accounts.Count==0){ForgetCredentials();return;}Directory.CreateDirectory(Path.GetDirectoryName(credentials));File.WriteAllBytes(credentials,ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Serialize(accounts)),null,DataProtectionScope.CurrentUser));}
}
}
