using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AionCL {
public static class RegressionTests {
    public static async Task UpdateScenarios() {
        Check(Updates.Newer("2.4.10","2.4.9"),"Lexical version comparison");
        Check(!Updates.Newer("1.1.0","1.1.0"),"Equal release flagged");
        var feed=new UpdateFeed { schemaVersion=1,product="AionCL",client=new ClientRelease{version="2.4.1",manifestUrl="https://example.com/patch.json"},launcher=new LauncherRelease{version="1.2.0",downloadPage="https://example.com/release"} };
        feed.Validate();
        var original=new LauncherConfig{product="AionCL",clientBaseVersion="2.4.0",manifestUrl="https://example.com/base.json",gameExecutable="bin64/aionclassic.bin",launchArguments="-ip:{ip} -port:{port} -lang:FRA",maxParallelDownloads=2,requestTimeoutSeconds=30};
        var target=Updates.Target(original,feed);Check(target.clientBaseVersion=="2.4.1"&&original.clientBaseVersion=="2.4.0","Update mutated configuration too soon");
        feed.client.version="2.4.0";
        try{Updates.Target(target,feed);throw new Exception("Downgrade accepted");}catch(InvalidDataException){}
        feed.client.manifestUrl="http://example.com/plain";
        try{feed.Validate();throw new Exception("HTTP accepted");}catch(InvalidDataException){}
        using(var network=new Network(5,new Handler{Reply=r=>new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("null")}})) {
            try{await Updates.Fetch(network,"https://example.com/feed",CancellationToken.None);throw new Exception("Null feed accepted");}catch(InvalidDataException){}
        }
    }
    public static async Task KoreanPackScenarios() {
        string root=Temp(),source=Temp();
        try {
            var font=new HitFont(Encoding.UTF8.GetBytes("font fixture"));
            font.ChangeFiles(root,"FRA",true);
            Check(font.Valid(root,"FRA"),"Font installation failed");
            font.ChangeFiles(root,"FRA",false);
            Check(!font.Installed(root,"FRA"),"Font removal failed");
            font.ChangeFiles(root,"FRA",true);
            File.WriteAllText(Path.Combine(root,"L10N","FRA","textures","ui","hit_number.pak"),"custom");
            try{font.ChangeFiles(root,"FRA",false);throw new Exception("Custom font erased");}catch(IOException){}
            Directory.CreateDirectory(Path.Combine(source,"voice","attack"));
            var bytes=Encoding.UTF8.GetBytes("korean fixture");File.WriteAllBytes(Path.Combine(source,"voice","attack","zattack.pak"),bytes);
            var pack=new KoreanPack(new[]{new ClientFile{path="voice/attack/zattack.pak",size=bytes.Length,sha256=Safety.Sha(bytes)}});
            string localized=Path.Combine(root,"L10N","FRA","sounds","voice","attack");Directory.CreateDirectory(localized);File.WriteAllText(Path.Combine(localized,"attack.pak"),"original");
            Check(!pack.HasPack(root,"FRA"),"Fresh client falsely detected");
            await pack.ChangeFiles(root,"FRA",true,source,null,CancellationToken.None);
            Check(await pack.Valid(root,"FRA",CancellationToken.None),"Installed pack invalid");
            Check(File.ReadAllText(Path.Combine(localized,"attack.pak"))=="original","Original overwritten");
            await pack.ChangeFiles(root,"FRA",false,null,null,CancellationToken.None);
            Check(!pack.HasPack(root,"FRA")&&pack.HasCache(root),"Removal/cache failed");
            await pack.ChangeFiles(root,"FRA",true,null,null,CancellationToken.None);
            Check(await pack.Valid(root,"FRA",CancellationToken.None),"Cache reinstall failed");
            var cts=new CancellationTokenSource();cts.Cancel();
            try{await pack.ChangeFiles(root,"FRA",false,null,null,cts.Token);throw new Exception("Cancellation ignored");}catch(OperationCanceledException){}
            Check(pack.HasPack(root,"FRA"),"Cancelled removal lost pack");
            File.WriteAllText(Path.Combine(localized,"zattack.pak"),"modified");
            Check(!await pack.Valid(root,"FRA",CancellationToken.None),"Corrupt pack accepted");
            try{await pack.ChangeFiles(root,"FRA",false,null,null,CancellationToken.None);throw new Exception("Modified file erased");}catch(IOException){}
            Check(File.ReadAllText(Path.Combine(localized,"zattack.pak"))=="modified","Conflict not preserved");
        }finally{Cleanup(root);Cleanup(source);}
    }
    sealed class CheckProgress : IProgress<VerificationProgress> {
        public int Calls; public VerificationProgress Last;
        public void Report(VerificationProgress value) { Calls++; Last = value; }
    }
    sealed class Handler : HttpMessageHandler {
        public Func<HttpRequestMessage, HttpResponseMessage> Reply;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) {
            token.ThrowIfCancellationRequested(); return Task.FromResult(Reply(request));
        }
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    static string Temp() { var p = Path.Combine(Path.GetTempPath(), "AionCL-Regression-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    static void Cleanup(string p) { if (!Path.GetFullPath(p).StartsWith(Path.Combine(Path.GetTempPath(), "AionCL-Regression-"), StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe cleanup"); Directory.Delete(p, true); }
    static ServerConfig Server(bool maintenance) { return new ServerConfig { version = 1, serverName = "AionCL", loginHost = "localhost", loginPort = 2106, gamePort = 7777, maintenance = maintenance }; }
    public static async Task ServerScenarios() {
        string voices = Temp();
        try {
            Directory.CreateDirectory(Path.Combine(voices, "Sounds", "voice"));
            string original = Path.Combine(voices, "L10N", "FRA", "sounds", "voice");
            Directory.CreateDirectory(original); File.WriteAllText(Path.Combine(original, "test.pak"), "original");
            VoiceMode.ApplyFiles(voices, "FRA", true);
            string mapped = VoiceMode.FilePath(voices, "L10N/FRA/sounds/voice/test.pak");
            Check(!Directory.Exists(original) && File.ReadAllText(mapped) == "original", "Voice backup failed");
            VoiceMode.ApplyFiles(voices, "FRA", true);
            var audioManifest = new Manifest { packages = new[] { new Package { name = "audio", files = new[] { new ClientFile { path = "L10N/FRA/sounds/voice/test.pak", size = 8, sha256 = Safety.Sha(Encoding.UTF8.GetBytes("original")) } } } } };
            Check((await Installation.Verify(voices, audioManifest, CancellationToken.None)).Count == 0, "Enabled voices fail verification");
            File.WriteAllText(mapped, "repaired");
            Check((await Installation.Verify(voices, audioManifest, CancellationToken.None)).Count == 1, "Corrupt backup not detected");
            Directory.CreateDirectory(original);
            try { VoiceMode.ApplyFiles(voices, "FRA", false); throw new Exception("Voice conflict accepted"); } catch (IOException) {}
            Directory.Delete(original);
            VoiceMode.ApplyFiles(voices, "FRA", false);
            Check(File.ReadAllText(Path.Combine(original, "test.pak")) == "repaired", "Voice restoration failed");
            Check(VoiceMode.FilePath(voices, "Sounds/voice/test.pak") == Path.Combine(voices, "Sounds", "voice", "test.pak"), "Base voice redirected");
        } finally { Cleanup(voices); }
        foreach (var code in new[] { "FRA", "ENG", "DEU" }) {
            Check(GameLanguage.Apply("-ip:127.0.0.1 -lang:FRA -port:2106", code) == "-ip:127.0.0.1 -lang:" + code + " -port:2106", "Language changes other arguments");
        }
        try { GameLanguage.Apply("-lang:FRA", "ENG -bad"); throw new Exception("Invalid language accepted"); } catch (InvalidDataException) {}
        try { GameLanguage.Apply("-lang:FRA -lang:ENG", "DEU"); throw new Exception("Duplicate language accepted"); } catch (InvalidDataException) {}
        string root = Temp(); var h = new Handler();
        try { using (var n = new Network(5, h)) {
            var s = new ServerService(n); string url = "https://example.com/server.json";
            h.Reply = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Serialize(Server(false))) };
            Check(!(await s.Load(url, root, delegate {}, CancellationToken.None)).maintenance, "Fresh load failed");
            string cache = Path.Combine(root, ".aioncl", "server-config.json"); Check(File.Exists(cache), "Cache not created");
            h.Reply = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Json.Serialize(Server(true))) };
            var maintenance = await s.Load(url, root, delegate {}, CancellationToken.None);
            Check(maintenance.maintenance, "Maintenance lost");
            Check(Directory.GetFiles(Path.GetDirectoryName(cache), "*.bak-*").Length == 1, "Previous config not backed up");
            try { new GameLauncher(new LauncherConfig()).BuildLaunchCommand(root, maintenance, IPAddress.Loopback); throw new Exception("Maintenance bypass"); } catch (InvalidOperationException) {}
            h.Reply = r => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
            Check((await s.Load(url, root, delegate {}, CancellationToken.None)).maintenance, "Offline maintenance lost");
            h.Reply = r => { throw new HttpRequestException("offline"); };
            Check((await s.Load(url, root, delegate {}, CancellationToken.None)).maintenance, "Network fallback failed");
            h.Reply = r => { throw new OperationCanceledException("timeout"); };
            Check((await s.Load(url, root, delegate {}, CancellationToken.None)).maintenance, "Timeout fallback failed");
            foreach (string bad in new[] { "null", "{bad", "{}", Json.Serialize(Server(false)).Replace("\"version\":1", "\"version\":2"), Json.Serialize(Server(false)).Replace("\"maintenance\":false", "\"maintenance\":null") }) {
                h.Reply = r => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(bad) };
                Check((await s.Load(url, root, delegate {}, CancellationToken.None)).maintenance, "Invalid data replaced cache");
            }
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await s.Load(url, root, delegate {}, cancelled.Token); throw new Exception("Cancellation ignored"); } catch (OperationCanceledException) {}
            File.Delete(cache);
            try { await s.Load(url, root, delegate {}, CancellationToken.None); throw new Exception("Missing configuration accepted"); } catch (InvalidOperationException ex) { Check(ex.Message.Contains("connexion"), "Unhelpful error"); }
        }} finally { Cleanup(root); }
    }
    public static async Task InstallScenarios() {
        string root = Temp();
        try {
            byte[] payload = Encoding.UTF8.GetBytes("fixture"); byte[] archive;
            using (var memory = new MemoryStream()) {
                using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true)) { using (var stream = zip.CreateEntry("Data/test.dat").Open()) stream.Write(payload, 0, payload.Length); }
                archive = memory.ToArray();
            }
            var p = new Package { name = "aioncl-client-2.4.0-001.zip", size = archive.Length, uncompressedSize = payload.Length, fileCount = 1, sha256 = Safety.Sha(archive), mirrors = new[] { "https://example.com/client.zip" }, files = new[] { new ClientFile { path = "Data/test.dat", size = payload.Length, sha256 = Safety.Sha(payload) } } };
            var m = new ManifestResult { Hash = Safety.Sha(payload), Manifest = new Manifest { formatVersion = 1, product = "AionCL", gameVersion = "2.4", clientVersion = "2.4.0", archiveFormat = "zip", sourceBytes = payload.Length, compressedBytes = archive.Length, sourceFileCount = 1, packages = new[] { p } } };
            var cfg = new LauncherConfig { clientBaseVersion = "2.4.0", maxParallelDownloads = 1 };
            bool ranged = false; var h = new Handler { Reply = r => {
                long offset = r.Headers.Range == null ? 0 : 7; ranged = r.Headers.Range != null;
                if (ranged) Check(r.Headers.Range.ToString() == "bytes=7-", "Resume offset");
                var content = new ByteArrayContent(archive, (int)offset, archive.Length - (int)offset);
                if (ranged) content.Headers.ContentRange = new ContentRangeHeaderValue(offset, archive.Length - 1, archive.Length);
                return new HttpResponseMessage(ranged ? HttpStatusCode.PartialContent : HttpStatusCode.OK) { Content = content };
            }};
            using (var network = new Network(5, h)) {
                var plan = new InstallPlan { Target = m, Packages = new[] { p } };
                await Installation.Install(root, plan, cfg, network, null, delegate {}, CancellationToken.None);
                Check(Installation.Detect(root, m) == ClientState.Valid, "Fresh install invalid");
                string cache = Path.Combine(root, ".aioncl", "cache", p.name);
                File.Delete(cache); File.WriteAllBytes(cache + ".part", new ArraySegment<byte>(archive, 0, 7).ToArray());
                await Installation.Install(root, plan, cfg, network, null, delegate {}, CancellationToken.None);
                Check(ranged, "Interrupted download not resumed");
                File.WriteAllText(Path.Combine(root, "system.cfg"), "personal");
                Directory.CreateDirectory(Path.Combine(root, "Shaders", "Cache")); File.WriteAllText(Path.Combine(root, "Shaders", "Cache", "generated"), "runtime");
                File.WriteAllText(Path.Combine(root, "Data", "test.dat"), "damaged");
                var progress = new CheckProgress();
                Check((await Installation.Verify(root, m.Manifest, CancellationToken.None, progress)).Count == 1, "Corruption missed");
                Check(progress.Calls == 2 && progress.Last.Completed == 1 && progress.Last.Total == 1, "Verification progress incomplete");
                var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                try { await Installation.Verify(root, m.Manifest, cancelled.Token, new CheckProgress()); throw new Exception("Verification cancellation ignored"); } catch (OperationCanceledException) {}
                h.Reply = r => { throw new Exception("Valid archive cache not reused"); };
                await Installation.Install(root, plan, cfg, network, null, delegate {}, CancellationToken.None);
                Check((await Installation.Verify(root, m.Manifest, CancellationToken.None)).Count == 0, "Repair failed");
                Check(File.ReadAllText(Path.Combine(root, "system.cfg")) == "personal" && File.Exists(Path.Combine(root, "Shaders", "Cache", "generated")), "Generated files changed");
                m.Manifest.clientVersion="2.4.1";cfg.clientBaseVersion="2.4.1";
                await Installation.Install(root,new InstallPlan{Target=m,Packages=new Package[0]},cfg,network,null,delegate{},CancellationToken.None);
                Check(Installation.Detect(root,m)==ClientState.Valid,"Upgrade reusing older unchanged packages failed");
            }
        } finally { Cleanup(root); }
    }
}
}
