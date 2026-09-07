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
                Check((await Installation.Verify(root, m.Manifest, CancellationToken.None)).Count == 1, "Corruption missed");
                h.Reply = r => { throw new Exception("Valid archive cache not reused"); };
                await Installation.Install(root, plan, cfg, network, null, delegate {}, CancellationToken.None);
                Check((await Installation.Verify(root, m.Manifest, CancellationToken.None)).Count == 0, "Repair failed");
                Check(File.ReadAllText(Path.Combine(root, "system.cfg")) == "personal" && File.Exists(Path.Combine(root, "Shaders", "Cache", "generated")), "Generated files changed");
            }
        } finally { Cleanup(root); }
    }
}
}
