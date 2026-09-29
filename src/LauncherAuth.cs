using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace AionCL {
public sealed class SavedAccount { public string username { get; set; } public string password { get; set; } }
public sealed class LauncherAuth {
    readonly string credentials;
    public LauncherAuth() {
        var directory=Environment.GetEnvironmentVariable("AIONCL_CONFIG_DIR");
        if(String.IsNullOrWhiteSpace(directory))directory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL");
        credentials=Path.Combine(Path.GetFullPath(directory),"launcher-credentials.bin");
    }
    public List<SavedAccount> Accounts { get { try { if(!File.Exists(credentials))return new List<SavedAccount>(); var clear=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(credentials),null,DataProtectionScope.CurrentUser)); try { var accounts=Json.Parse<List<SavedAccount>>(clear); if(accounts!=null)return accounts.FindAll(a=>a!=null&&!String.IsNullOrWhiteSpace(a.username)&&a.password!=null); } catch { } var legacy=clear.Split(new[]{'\n'},2); if(legacy.Length==2&&!String.IsNullOrWhiteSpace(legacy[0]))return new List<SavedAccount> { new SavedAccount { username=legacy[0],password=legacy[1] } }; } catch { } return new List<SavedAccount>(); } }
    string SelectionPath { get { return Path.Combine(Path.GetDirectoryName(credentials),"selected-account.txt"); } }
    public string SelectedAccount {
        get {
            var accounts=Accounts;
            try {
                var selected=File.ReadAllText(SelectionPath,Encoding.UTF8).Trim();
                foreach(var account in accounts)
                    if(String.Equals(account.username,selected,StringComparison.OrdinalIgnoreCase))return account.username;
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
            return accounts.Count==0 ? null : accounts[0].username;
        }
    }
    void ClearSelection() {
        if(File.Exists(SelectionPath)) {

            File.Delete(SelectionPath);
        }
    }
    public void SelectAccount(string username) {
        var account=Find(username);
        if(account==null)return;
        Directory.CreateDirectory(Path.GetDirectoryName(credentials));
        File.WriteAllText(SelectionPath,account.username,new UTF8Encoding(false));

    }
    public SavedAccount Find(string username) { foreach(var account in Accounts) if(String.Equals(account.username,username,StringComparison.OrdinalIgnoreCase))return account; return null; }
    public void ForgetCredentials(){ClearSelection();if(File.Exists(credentials))File.Delete(credentials);}
    public void ForgetCredential(string username){var accounts=Accounts;if(String.Equals(SelectedAccount,username,StringComparison.OrdinalIgnoreCase))ClearSelection();accounts.RemoveAll(a=>String.Equals(a.username,username,StringComparison.OrdinalIgnoreCase));Write(accounts);}
    public void SaveCredentials(string username,string password,bool remember){var accounts=Accounts;accounts.RemoveAll(a=>String.Equals(a.username,username,StringComparison.OrdinalIgnoreCase));if(remember)accounts.Insert(0,new SavedAccount { username=username,password=password });Write(accounts);}
    void Write(List<SavedAccount> accounts){if(accounts.Count==0){ForgetCredentials();return;}Directory.CreateDirectory(Path.GetDirectoryName(credentials));string temp=credentials+"."+Guid.NewGuid().ToString("N")+".tmp";try { File.WriteAllBytes(temp,ProtectedData.Protect(Encoding.UTF8.GetBytes(Json.Serialize(accounts)),null,DataProtectionScope.CurrentUser));if(File.Exists(credentials))File.Replace(temp,credentials,null);else File.Move(temp,credentials); } finally { if(File.Exists(temp))File.Delete(temp); }}
}
}
