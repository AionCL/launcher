using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
namespace AionCL {
public sealed class SavedAccount { public string username { get; set; } public string password { get; set; } }
public sealed class LauncherAuth {
    [DllImport("libc.so.6", SetLastError=true, EntryPoint="chmod")]
    static extern int Chmod(string path, uint mode);
    readonly string directory;
    readonly string credentials;
    public LauncherAuth() {
        string configured=Environment.GetEnvironmentVariable("AIONCL_CONFIG_DIR");
        if(String.IsNullOrWhiteSpace(configured))
            configured=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal),".aioncl");
        directory=Path.GetFullPath(configured);
        credentials=Path.Combine(directory,"credentials.json");
    }
    public List<SavedAccount> Accounts {
        get {
            try {
                if(!File.Exists(credentials))return new List<SavedAccount>();
                RejectLink(credentials);
                var parsed=Json.Parse<List<SavedAccount>>(File.ReadAllText(credentials,Encoding.UTF8));
                return parsed==null ? new List<SavedAccount>() : parsed.FindAll(a=>a!=null&&!String.IsNullOrWhiteSpace(a.username)&&a.password!=null);
            } catch { return new List<SavedAccount>(); }
        }
    }
    string SelectionPath { get { return Path.Combine(directory,"selected-account.txt"); } }
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
            RejectLink(SelectionPath);
            File.Delete(SelectionPath);
        }
    }
    public void SelectAccount(string username) {
        var account=Find(username);
        if(account==null)return;
        SecureDirectory();
        if(File.Exists(SelectionPath))RejectLink(SelectionPath);
        File.WriteAllText(SelectionPath,account.username,new UTF8Encoding(false));
        if(Chmod(SelectionPath,Convert.ToUInt32("600",8))!=0)throw new IOException("Cannot restrict account selection permissions.");
    }
    public SavedAccount Find(string username) {
        foreach(var account in Accounts)
            if(String.Equals(account.username,username,StringComparison.OrdinalIgnoreCase))
                return new SavedAccount { username=account.username,password=account.password };
        return null;
    }
    public void ForgetCredentials() { ClearSelection(); if(File.Exists(credentials)){RejectLink(credentials);File.Delete(credentials);} }
    public void ForgetCredential(string username) {
        var accounts=Accounts;
        if(String.Equals(SelectedAccount,username,StringComparison.OrdinalIgnoreCase))ClearSelection();
        accounts.RemoveAll(account=>String.Equals(account.username,username,StringComparison.OrdinalIgnoreCase));
        Write(accounts);
    }
    public void SaveCredentials(string username, string password, bool remember) {
        var accounts=Accounts;
        accounts.RemoveAll(account=>String.Equals(account.username,username,StringComparison.OrdinalIgnoreCase));
        if(remember)accounts.Insert(0,new SavedAccount { username=username,password=password });
        Write(accounts);
    }
    void RejectLink(string path) {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing a symbolic-link credential file.");
    }
    void SecureDirectory() {
        if(!Directory.Exists(directory))Directory.CreateDirectory(directory);
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Refusing a symbolic-link credential directory.");
        if(Chmod(directory,Convert.ToUInt32("700",8))!=0)throw new IOException("Cannot restrict credential directory permissions.");
    }
    void Write(List<SavedAccount> accounts) {
        if(accounts.Count==0){ForgetCredentials();return;}
        SecureDirectory();
        string temp=Path.Combine(directory,".credentials-"+Guid.NewGuid().ToString("N")+".tmp");
        try {
            File.WriteAllText(temp,Json.Serialize(accounts),new UTF8Encoding(false));
            if(Chmod(temp,Convert.ToUInt32("600",8))!=0)throw new IOException("Cannot restrict credential file permissions.");
            if(File.Exists(credentials)) {
                RejectLink(credentials);
                File.Replace(temp,credentials,null);
            } else File.Move(temp,credentials);
        } finally { if(File.Exists(temp))File.Delete(temp); }
    }
}
}
