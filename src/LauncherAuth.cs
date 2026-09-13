using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace AionCL {
public sealed class LauncherAuth {
    readonly string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","launcher-token.bin");
    public string Token { get { try { if(!File.Exists(path))return null; return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)); } catch { return null; } } }
    public void Clear(){try{if(File.Exists(path))File.Delete(path);}catch{}}
    public async Task<bool> Login(string endpoint,string username,string password,CancellationToken token){using(var client=new HttpClient()){client.Timeout=TimeSpan.FromSeconds(15);var body=Json.Serialize(new {username=username,password=password});using(var content=new StringContent(body,Encoding.UTF8,"application/json")){var response=await client.PostAsync(endpoint,content,token).ConfigureAwait(false);if(!response.IsSuccessStatusCode)return false;var data=Json.Parse<LauncherAuthResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));if(String.IsNullOrEmpty(data.token))return false;Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(data.token),null,DataProtectionScope.CurrentUser));return true;}}}
}
public sealed class LauncherAuthResponse { public string token { get; set; } }
}
