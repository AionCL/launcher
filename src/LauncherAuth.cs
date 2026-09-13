using System;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
namespace AionCL {
public sealed class LauncherAuth {
    readonly string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","launcher-token.bin");
    readonly string credentials=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AionCL","launcher-credentials.bin");
    public string Token { get { try { if(!File.Exists(path))return null; return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)); } catch { return null; } } }
    public void Clear(){try{if(File.Exists(path))File.Delete(path);}catch{}}
    public Tuple<string,string> SavedCredentials { get { try { if(!File.Exists(credentials))return null; var value=Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(credentials),null,DataProtectionScope.CurrentUser)).Split(new[]{'\n'},2); return value.Length==2?Tuple.Create(value[0],value[1]):null; } catch { return null; } } }
    public void ForgetCredentials(){try{if(File.Exists(credentials))File.Delete(credentials);}catch{}}
    public async Task<bool> BrowserLogin(string portal, CancellationToken token) {
        using(var client=new HttpClient()) {
            client.Timeout=TimeSpan.FromSeconds(15); client.DefaultRequestHeaders.TryAddWithoutValidation("Origin","https://aioncl.com");
            var state=(Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+','-').Replace('/','_')+Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace('+','-').Replace('/','_')).Substring(0,43);
            var start=await client.PostAsync(portal.TrimEnd('/')+"/api/launcher/authorize/start",new StringContent(Json.Serialize(new {state=state}),Encoding.UTF8,"application/json"),token).ConfigureAwait(false);
            if(!start.IsSuccessStatusCode)return false;
            Process.Start(new ProcessStartInfo { FileName=portal.TrimEnd('/')+"/account.html?launcher_state="+Uri.EscapeDataString(state),UseShellExecute=true });
            for(int i=0;i<150;i++) { await Task.Delay(2000,token).ConfigureAwait(false); var response=await client.GetAsync(portal.TrimEnd('/')+"/api/launcher/authorize/poll?state="+Uri.EscapeDataString(state),token).ConfigureAwait(false); if((int)response.StatusCode==202)continue; if(!response.IsSuccessStatusCode)return false; var data=Json.Parse<LauncherAuthResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false)); if(String.IsNullOrEmpty(data.token))return false; Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(data.token),null,DataProtectionScope.CurrentUser)); return true; }
            return false;
        }
    }
    public async Task<bool> Login(string endpoint,string username,string password,string code,bool remember,CancellationToken token){using(var client=new HttpClient()){client.Timeout=TimeSpan.FromSeconds(15);client.DefaultRequestHeaders.TryAddWithoutValidation("Origin","https://aioncl.com");var body=Json.Serialize(new {username=username,password=password,code=code??""});using(var content=new StringContent(body,Encoding.UTF8,"application/json")){var response=await client.PostAsync(endpoint,content,token).ConfigureAwait(false);if(!response.IsSuccessStatusCode)return false;var data=Json.Parse<LauncherAuthResponse>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));if(String.IsNullOrEmpty(data.token))return false;Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(data.token),null,DataProtectionScope.CurrentUser));if(remember)File.WriteAllBytes(credentials,ProtectedData.Protect(Encoding.UTF8.GetBytes(username+"\n"+password),null,DataProtectionScope.CurrentUser));else ForgetCredentials();return true;}}}
}
public sealed class LauncherAuthResponse { public string token { get; set; } }
}
