using System.Collections.Generic;
namespace AionCL {
// Linux preview deliberately keeps credentials in memory only until a keyring
// integration is qualified. Never fall back to plaintext credential storage.
public sealed class SavedAccount { public string username { get; set; } public string password { get; set; } }
public sealed class LauncherAuth {
    public List<SavedAccount> Accounts { get { return new List<SavedAccount>(); } }
    public SavedAccount Find(string username) { return null; }
    public void ForgetCredentials() {}
    public void ForgetCredential(string username) {}
    public void SaveCredentials(string username, string password, bool remember) {
        if (remember) throw new System.NotSupportedException("Credential storage is not available in the Linux preview.");
    }
}
}
