using System.Collections.Generic;
namespace AionCL {
// Linux credentials live only for the launcher process lifetime. Never fall
// back to plaintext storage when no qualified desktop keyring is available.
public sealed class SavedAccount { public string username { get; set; } public string password { get; set; } }
public sealed class LauncherAuth {
    readonly List<SavedAccount> accounts = new List<SavedAccount>();
    public List<SavedAccount> Accounts { get { return new List<SavedAccount>(accounts); } }
    public SavedAccount Find(string username) {
        foreach(var account in accounts)
            if(System.String.Equals(account.username,username,System.StringComparison.OrdinalIgnoreCase))
                return new SavedAccount { username=account.username,password=account.password };
        return null;
    }
    public void ForgetCredentials() { accounts.Clear(); }
    public void ForgetCredential(string username) {
        accounts.RemoveAll(account=>System.String.Equals(account.username,username,System.StringComparison.OrdinalIgnoreCase));
    }
    public void SaveCredentials(string username, string password, bool remember) {
        ForgetCredential(username);
        if(remember) accounts.Insert(0,new SavedAccount { username=username,password=password });
    }
}
}
