using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace AionCL {
public sealed class LauncherConfig {
    public string product { get; set; }
    public string launcherVersion { get; set; }
    public string clientBaseVersion { get; set; }
    public string manifestUrl { get; set; }
    public string serverConfigUrl { get; set; }
    public string updateFeedUrl { get; set; }
    public int maxParallelDownloads { get; set; }
    public int requestTimeoutSeconds { get; set; }
    public string gameExecutable { get; set; }
    public string launchArguments { get; set; }
    public void Validate() {
        if (product != "AionCL" || !Regex.IsMatch(clientBaseVersion ?? "", "^2\\.4\\.[0-9]+$") || maxParallelDownloads < 1 || maxParallelDownloads > 3 || requestTimeoutSeconds < 5 || requestTimeoutSeconds > 120) throw new InvalidDataException("Configuration launcher invalide.");
        if (!String.IsNullOrEmpty(updateFeedUrl)) Safety.Https(updateFeedUrl);
        Safety.Https(manifestUrl); if (!String.IsNullOrEmpty(serverConfigUrl)) Safety.Https(serverConfigUrl);
        Safety.Relative(gameExecutable);
        if (String.IsNullOrEmpty(launchArguments) || !launchArguments.Contains("{ip}") || !launchArguments.Contains("{port}") || launchArguments.IndexOfAny(new[] {'\r','\n','\0'}) >= 0) throw new InvalidDataException("Profil de lancement invalide.");
    }
}
public sealed class ClientFile { public string path { get; set; } public long size { get; set; } public string sha256 { get; set; } }
public sealed class Package {
    public string name { get; set; } public long size { get; set; } public long uncompressedSize { get; set; }
    public int fileCount { get; set; } public string sha256 { get; set; } public string[] mirrors { get; set; } public ClientFile[] files { get; set; }
}
public sealed class Manifest {
    public int formatVersion { get; set; } public string product { get; set; } public string gameVersion { get; set; }
    public string clientVersion { get; set; } public string archiveFormat { get; set; }
    public long sourceBytes { get; set; } public long compressedBytes { get; set; } public int sourceFileCount { get; set; }
    public Package[] packages { get; set; }
    public void Validate(string version) {
        if (formatVersion != 1 || product != "AionCL" || gameVersion != "2.4" || clientVersion != version || archiveFormat != "zip" || packages == null || packages.Length == 0 || packages.Length > 998) throw new InvalidDataException("Manifest non pris en charge.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long bytes = 0, compressed = 0; int count = 0;
        checked { foreach (var p in packages) {
            if (p == null || !Regex.IsMatch(p.name ?? "", "^aioncl-client-2\\.4\\.[0-9]+-[0-9]{3}\\.zip$") || !names.Add(p.name) || p.size <= 0 || p.size >= 2147483648L || p.files == null || p.files.Length == 0 || p.files.Length != p.fileCount || p.mirrors == null || p.mirrors.Length == 0) throw new InvalidDataException("Package invalide.");
            Safety.Hash(p.sha256); foreach (var url in p.mirrors) Safety.Https(url);
            long packageBytes = 0;
            foreach (var f in p.files) {
                if (f == null) throw new InvalidDataException("Fichier invalide."); Safety.Relative(f.path); Safety.Hash(f.sha256);
                if (f.path.Split('/')[0].Equals(".aioncl", StringComparison.OrdinalIgnoreCase) || !paths.Add(f.path) || f.size < 0) throw new InvalidDataException("Chemin reserve, duplique ou taille invalide.");
                packageBytes += f.size; count++;
            }
            if (packageBytes != p.uncompressedSize) throw new InvalidDataException("Total package incoherent."); bytes += packageBytes; compressed += p.size;
        }}
        foreach (var path in paths) { var parent = path; while (parent.Contains("/")) { parent = parent.Substring(0, parent.LastIndexOf('/')); if (paths.Contains(parent)) throw new InvalidDataException("Collision fichier/repertoire."); } }
        if (bytes != sourceBytes || compressed != compressedBytes || count != sourceFileCount) throw new InvalidDataException("Totaux manifest incoherents.");
    }
}
public sealed class ManifestResult { public Manifest Manifest; public string Hash; }
public sealed class LocalVersion { public string version { get; set; } public string installedAt { get; set; } public string manifestHash { get; set; } }
public sealed class ServerConfig {
    public int version { get; set; } public string serverName { get; set; } public string loginHost { get; set; }
    public int loginPort { get; set; } public int gamePort { get; set; } public bool maintenance { get; set; }
    public void Validate() {
        if (version != 1 || String.IsNullOrWhiteSpace(serverName) || Uri.CheckHostName(loginHost ?? "") == UriHostNameType.Unknown || loginPort < 1 || loginPort > 65535 || gamePort < 1 || gamePort > 65535) throw new InvalidDataException("Configuration serveur invalide.");
    }
    public static ServerConfig Parse(string text) {
        var fields = Json.Parse<Dictionary<string, object>>(text);
        if (fields == null || !fields.ContainsKey("maintenance") || !(fields["maintenance"] is bool)) throw new InvalidDataException("Champ maintenance booleen requis.");
        var server = Json.Parse<ServerConfig>(text); server.Validate(); return server;
    }
}

// Exact, reviewed local shop patch variants. Never trust a local allowlist.
// Bound to the original manifest hash: future client versions use their own files.
public static class ShopPatchIntegrity {
    static readonly string[][] Variants = new string[][] {
        new string[] { "l10n/FRA/FRA.pak", "986cc2759fa226c11241520da71fd5adff066a541fcb808dc90b426dc55b0e50", "69d6e7c7348ba3cd5e27b6402aea2d4175cadb51db2889aa09065395395941df", "673" },
        new string[] { "l10n/ENG/ENG.pak", "1e6caa894d0460f08de6af777e170c84ff49be5689ac1eabf79f81706bd26b07", "43c96391822b34bd5b3d1d7c65dc2d9724a7ec4f11b070e0f8eea2b2cdf46d1d", "672" },
        new string[] { "l10n/DEU/DEU.pak", "0a4ff72890de41d45cbff051d04da0e530be96c581d42a35be9547c19726e03d", "037593bcd90b17d858f39ded24584096cb6c5b81d532e8db15e27e97d0403242", "672" },
        new string[] { "L10N/FRA/data/data.pak", "b508fab73d5fc16de32cf1e64242e63a42c2b4e60b5eb603a37ef84cbdbeda12", "6d956bd7dd062ce533bab510f82da498b9d946c734a5c0600a2673c0c8457962", "54314257" },
        new string[] { "L10N/ENG/data/data.pak", "0b3050ee0d28d90f81e300a6145dbdd467b0327477d69c589384acc575adf220", "e91c76afee98397438115c7568907be4c11597e6f9b3fb45133d68c37509bb3f", "50881368" },
        new string[] { "L10N/DEU/data/data.pak", "d53264795c17094bdae218e38a8aa24ee57d7a890f878d89898071b9b2b01630", "647198922a02147a30fbdc319cbf968d11be6585dc0de6fbd457cc01c0896fd9", "53771604" },
    };
    public static bool Matches(string path, ClientFile original) {
        var variant = Variants.FirstOrDefault(v => String.Equals(v[0], original.path, StringComparison.OrdinalIgnoreCase) && String.Equals(v[1], original.sha256, StringComparison.OrdinalIgnoreCase));
        if (variant == null) return false;
        return Safety.Matches(path, Int64.Parse(variant[3], CultureInfo.InvariantCulture), variant[2], CancellationToken.None).GetAwaiter().GetResult();
    }
}

public enum ClientState { Absent, Potential, Valid, Incomplete }
public sealed class FileIssue { public string Path; public string Package; public string Reason; }
public sealed class VerificationProgress { public string Path; public int Completed; public int Total; }
public sealed class TransferProgress { public string Package; public long Bytes; public long Total; public double BytesPerSecond; }
// Future patch planning and launcher self-update remain independent contracts.
public sealed class InstallPlan { public ManifestResult Target; public Package[] Packages; }
public interface IClientUpdatePlanner { InstallPlan Plan(string installedVersion, ManifestResult target); }
public interface ILauncherUpdateSource { Task<string> GetAvailableLauncherVersionAsync(CancellationToken token); }
public sealed class FullInstallPlanner : IClientUpdatePlanner {
    public InstallPlan Plan(string installedVersion, ManifestResult target) { return new InstallPlan { Target = target, Packages = target.Manifest.packages }; }
}
public static class Json {
    public static T Parse<T>(string text) { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 32 }.Deserialize<T>(text); }
    public static string Serialize(object value) { return new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 }.Serialize(value); }
    public static void Atomic(string path, object value) {
        Safety.NoLinks(path); Directory.CreateDirectory(Path.GetDirectoryName(path)); string temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(temp, Serialize(value), new UTF8Encoding(false));
        if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
    }
}
public static class Safety {
    public static Uri Https(string value) {
        Uri uri; if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "https" || !String.IsNullOrEmpty(uri.UserInfo) || !String.IsNullOrEmpty(uri.Fragment) || !String.IsNullOrEmpty(uri.Query)) throw new InvalidDataException("URL HTTPS invalide."); return uri;
    }
    public static void Hash(string hash) { if (!Regex.IsMatch(hash ?? "", "^[a-fA-F0-9]{64}$")) throw new InvalidDataException("SHA-256 invalide."); }
    public static void Relative(string path) {
        if (String.IsNullOrWhiteSpace(path) || path.StartsWith("/") || path.IndexOfAny(new[] {'\\', ':', '\0'}) >= 0) throw new InvalidDataException("Chemin interdit.");
        foreach (var part in path.Split('/')) if (part.Length == 0 || part == "." || part == ".." || part.EndsWith(".") || part.EndsWith(" ") || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(part, "^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])($|\\.)", RegexOptions.IgnoreCase)) throw new InvalidDataException("Chemin interdit.");
    }
    public static void NoLinks(string path) {
        string current = Path.GetFullPath(path);
        while (!String.IsNullOrEmpty(current)) {
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Liens et jonctions interdits dans le chemin cible.");
            current = Path.GetDirectoryName(current);
        }
    }
    public static string Under(string root, string relative) {
        Relative(relative); string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string result = Path.GetFullPath(Path.Combine(prefix, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Chemin hors installation."); NoLinks(result); return result;
    }
    public static async Task<string> ShaAsync(string path, CancellationToken token) {
        using (var sha = SHA256.Create()) using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true)) {
            var buffer = new byte[131072]; int n;
            while ((n = await stream.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0) sha.TransformBlock(buffer, 0, n, buffer, 0);
            sha.TransformFinalBlock(new byte[0], 0, 0); return BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }
    }
    public static string Sha(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    public static async Task<bool> Matches(string path, long size, string hash, CancellationToken token) {
        NoLinks(path); return File.Exists(path) && new FileInfo(path).Length == size && String.Equals(await ShaAsync(path, token).ConfigureAwait(false), hash, StringComparison.OrdinalIgnoreCase);
    }
}
public sealed class Network : IDisposable {
    readonly HttpClient client; readonly int timeout;
    public Network(int timeoutSeconds, HttpMessageHandler handler = null) {
        timeout = timeoutSeconds; ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        client = handler == null ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, UseCookies = false }) : new HttpClient(handler);
        client.Timeout = Timeout.InfiniteTimeSpan; client.DefaultRequestHeaders.UserAgent.ParseAdd("AionCL-Launcher/1.0.0");
    }
    public async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken token) {
        Safety.Https(request.RequestUri.AbsoluteUri);
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token)) {
            cts.CancelAfter(TimeSpan.FromSeconds(timeout)); var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token).ConfigureAwait(false);
            if (response.RequestMessage != null && response.RequestMessage.RequestUri.Scheme != "https") { response.Dispose(); throw new IOException("Redirection non HTTPS refusee."); } return response;
        }
    }
    public async Task<int> Read(Stream stream, byte[] buffer, CancellationToken token) {
        using (var cts = CancellationTokenSource.CreateLinkedTokenSource(token)) {
            cts.CancelAfter(TimeSpan.FromSeconds(timeout));
            using (cts.Token.Register(() => stream.Dispose())) return await stream.ReadAsync(buffer, 0, buffer.Length, cts.Token).ConfigureAwait(false);
        }
    }
    public async Task<byte[]> Small(string url, CancellationToken token) {
        using (var request = new HttpRequestMessage(HttpMethod.Get, Safety.Https(url))) using (var response = await Send(request, token).ConfigureAwait(false)) {
            if (!response.IsSuccessStatusCode) throw new IOException("HTTP " + (int)response.StatusCode + " lors de la lecture des metadonnees.");
            if (response.Content.Headers.ContentLength > 4 * 1024 * 1024) throw new InvalidDataException("Metadonnees trop grandes.");
            using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using (var memory = new MemoryStream()) {
                var buffer = new byte[65536]; int n; while ((n = await Read(stream, buffer, token).ConfigureAwait(false)) > 0) { if (memory.Length + n > 4 * 1024 * 1024) throw new InvalidDataException("Metadonnees trop grandes."); memory.Write(buffer, 0, n); } return memory.ToArray();
            }
        }
    }
    public async Task<ManifestResult> ManifestAsync(LauncherConfig config, CancellationToken token) {
        byte[] data = await Small(config.manifestUrl, token).ConfigureAwait(false); var m = Json.Parse<Manifest>(Encoding.UTF8.GetString(data));
        if (m == null) throw new InvalidDataException("Manifest vide."); m.Validate(config.clientBaseVersion); return new ManifestResult { Manifest = m, Hash = Safety.Sha(data) };
    }
    public void Dispose() { client.Dispose(); }
}
public sealed class Downloader {
    readonly Network network; public Downloader(Network network) { this.network = network; }
    public async Task<string> Get(Package p, string cache, IProgress<TransferProgress> progress, CancellationToken token) {
        string target = Safety.Under(cache, p.name); Directory.CreateDirectory(cache);
        if (await Safety.Matches(target, p.size, p.sha256, token).ConfigureAwait(false)) return target;
        if (File.Exists(target)) File.Delete(target); // Only a corrupt cache entry, never a client file.
        string part = Safety.Under(cache, p.name + ".part");
        foreach (string url in p.mirrors) {
            try {
                for (int attempt = 0; attempt < 2; attempt++) {
                    token.ThrowIfCancellationRequested(); long offset = File.Exists(part) ? new FileInfo(part).Length : 0;
                    if (offset == p.size && await Safety.Matches(part, p.size, p.sha256, token).ConfigureAwait(false)) { File.Move(part, target); return target; }
                    if (offset >= p.size) { File.Delete(part); offset = 0; }
                    var clock = Stopwatch.StartNew(); long transferred = 0;
                    using (var request = new HttpRequestMessage(HttpMethod.Get, Safety.Https(url))) {
                        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
                        using (var response = await network.Send(request, token).ConfigureAwait(false)) {
                            if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && offset > 0) { File.Delete(part); continue; }
                            if (response.StatusCode != HttpStatusCode.OK && response.StatusCode != HttpStatusCode.PartialContent) throw new IOException("HTTP " + (int)response.StatusCode + " pour " + p.name);
                            if (response.StatusCode == HttpStatusCode.PartialContent) {
                                var range = response.Content.Headers.ContentRange;
                                if (range == null || range.Unit != "bytes" || range.From != offset || range.Length != p.size || range.To != p.size - 1) throw new InvalidDataException("Reponse Range incoherente.");
                            } else offset = 0;
                            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value != p.size - offset) throw new InvalidDataException("Taille HTTP incoherente.");
                            using (var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false)) using (var output = new FileStream(part, offset == 0 ? FileMode.Create : FileMode.Append, FileAccess.Write, FileShare.None, 131072, true)) {
                                var buffer = new byte[131072]; int n;
                                while ((n = await network.Read(input, buffer, token).ConfigureAwait(false)) > 0) {
                                    if (output.Length + n > p.size) throw new InvalidDataException("Archive plus grande que prevu.");
                                    await output.WriteAsync(buffer, 0, n, token).ConfigureAwait(false); transferred += n;
                                    if (progress != null) progress.Report(new TransferProgress { Package = p.name, Bytes = output.Length, Total = p.size, BytesPerSecond = transferred / Math.Max(clock.Elapsed.TotalSeconds, .001) });
                                }
                            }
                        }
                    }
                    if (!await Safety.Matches(part, p.size, p.sha256, token).ConfigureAwait(false)) { File.Delete(part); throw new InvalidDataException("Taille ou SHA-256 incorrect : " + p.name); }
                    File.Move(part, target); return target;
                }
            } catch (OperationCanceledException) { throw; }
            catch (Exception ex) { token.ThrowIfCancellationRequested(); if (!(ex is InvalidDataException || ex is IOException || ex is HttpRequestException)) throw; if (url == p.mirrors.Last()) throw; }
        }
        throw new IOException("Impossible de telecharger " + p.name);
    }
}
public static class Installation {
    public static bool IsMutable(string path)
    {
        if (String.IsNullOrEmpty(path))
            return false;

        path = path.Replace('\\', '/');

        return path.StartsWith(
            "Shaders/Cache/",
            StringComparison.OrdinalIgnoreCase
        );
    }
    public static ClientState Detect(string root, ManifestResult manifest)
    {
        if (!Directory.Exists(root))
            return ClientState.Absent;

        Safety.NoLinks(root);

        string version = Safety.Under(root, ".aioncl/version.json");

        if (File.Exists(version))
        {
            try
            {
                var v = Json.Parse<LocalVersion>(File.ReadAllText(version));

                if (v == null ||
                    v.version != manifest.Manifest.clientVersion ||
                    v.manifestHash != manifest.Hash)
                {
                    return ClientState.Incomplete;
                }

                foreach (var f in manifest.Manifest.packages.SelectMany(p => p.files))
                {
                    // Runtime cache files can be generated, changed or removed by Aion.
                    if (IsMutable(f.path))
                        continue;

                    var path = VoiceMode.FilePath(root, f.path);
                    if (ShopPatchIntegrity.Matches(path, f)) continue;

                    if (!File.Exists(path) ||
                        new FileInfo(path).Length != f.size)
                    {
                        return ClientState.Incomplete;
                    }
                }

                // Fast startup check: presence and size only.
                // Full SHA-256 checks are handled by Verify/Repair.
                return ClientState.Valid;
            }
            catch (Exception ex)
            {
                if (ex is IOException ||
                    ex is ArgumentException ||
                    ex is InvalidOperationException)
                {
                    return ClientState.Incomplete;
                }

                throw;
            }
        }

        if (Directory.Exists(Safety.Under(root, ".aioncl")))
            return ClientState.Incomplete;

        return File.Exists(Safety.Under(root, "bin64/aionclassic.bin")) ||
               File.Exists(Safety.Under(root, "bin32/aionclassic.bin"))
            ? ClientState.Potential
            : ClientState.Absent;
    }
    public static async Task<List<FileIssue>> Verify(
        string root,
        Manifest manifest,
        CancellationToken token,
        IProgress<VerificationProgress> progress = null)
    {
        var result = new List<FileIssue>();
        int total = manifest.packages.SelectMany(p => p.files).Count(f => !IsMutable(f.path));
        int completed = 0;

        foreach (var p in manifest.packages)
        {
            foreach (var f in p.files)
            {
                token.ThrowIfCancellationRequested();

                // Runtime cache files are intentionally excluded from integrity checks.
                if (IsMutable(f.path))
                    continue;

                string path = VoiceMode.FilePath(root, f.path);
                if (progress != null) progress.Report(new VerificationProgress { Path = f.path, Completed = completed, Total = total });

                string reason =
                    ShopPatchIntegrity.Matches(path, f) ? null :
                    !File.Exists(path)
                        ? "absent"
                        : new FileInfo(path).Length != f.size
                            ? "taille"
                            : !await Safety.Matches(
                                path,
                                f.size,
                                f.sha256,
                                token
                            ).ConfigureAwait(false)
                                ? "SHA-256"
                                : null;

                if (reason != null)
                {
                    result.Add(new FileIssue
                    {
                        Path = f.path,
                        Package = p.name,
                        Reason = reason
                    });
                }
                completed++;
            }
        }

        if (progress != null) progress.Report(new VerificationProgress { Path = "", Completed = completed, Total = total });

        return result;
    }
    public static async Task Extract(string archive, Package package, string stage, CancellationToken token) {
        if (!await Safety.Matches(archive, package.size, package.sha256, token).ConfigureAwait(false)) throw new InvalidDataException("Archive non validee.");
        using (var zip = ZipFile.OpenRead(archive)) {
            var expected = package.files.ToDictionary(f => f.path, StringComparer.Ordinal); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (zip.Entries.Count != expected.Count) throw new InvalidDataException("Inventaire ZIP incorrect.");
            foreach (var entry in zip.Entries) {
                token.ThrowIfCancellationRequested(); string path = Safety.Under(stage, entry.FullName); ClientFile file;
                if (!expected.TryGetValue(entry.FullName, out file) || !seen.Add(entry.FullName) || entry.Length != file.size || ((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Entree ZIP interdite.");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var input = entry.Open()) using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true)) {
                    var buffer = new byte[131072]; long total = 0; int n;
                    while ((n = await input.ReadAsync(buffer, 0, buffer.Length, token).ConfigureAwait(false)) > 0) { total += n; if (total > file.size) throw new InvalidDataException("Entree ZIP trop grande."); await output.WriteAsync(buffer, 0, n, token).ConfigureAwait(false); }
                }
                if (!await Safety.Matches(path, file.size, file.sha256, token).ConfigureAwait(false)) throw new InvalidDataException("Fichier extrait corrompu.");
            }
        }
    }
    public static async Task Install(string root, InstallPlan plan, LauncherConfig config, Network network, IProgress<TransferProgress> progress, Action<string> log, CancellationToken token) {
        if (Directory.Exists(Safety.Under(root, ".aioncl/voice-original"))) VoiceMode.RequireGameClosed();
        plan.Target.Manifest.Validate(config.clientBaseVersion);
        string metadata = Safety.Under(root, ".aioncl"); Directory.CreateDirectory(metadata);
        using (var installLock = new FileStream(Safety.Under(metadata, "install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
            string version = Safety.Under(metadata, "version.json"); if (File.Exists(version)) File.Delete(version);
            string cache = Safety.Under(metadata, "cache"); string stage = Safety.Under(metadata, "staging");
            long needed = plan.Target.Manifest.sourceBytes + plan.Packages.Sum(p => p.size);
            if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath(root))).AvailableFreeSpace < needed) throw new IOException("Espace disque insuffisant pour cache et preparation.");
            var downloader = new Downloader(network);
            using (var semaphore = new SemaphoreSlim(config.maxParallelDownloads)) {
                await Task.WhenAll(plan.Packages.Select(async p => { await semaphore.WaitAsync(token).ConfigureAwait(false); try { log("Telechargement / validation : " + p.name); await downloader.Get(p, cache, progress, token).ConfigureAwait(false); } finally { semaphore.Release(); } })).ConfigureAwait(false);
            }
            foreach (var p in plan.Packages) { log("Extraction : " + p.name); await Extract(Safety.Under(cache, p.name), p, stage, token).ConfigureAwait(false); }
            // Commit is recoverable, not an atomic directory swap. No version marker until final verification.
            foreach (var f in plan.Packages.SelectMany(p => p.files)) {
                token.ThrowIfCancellationRequested(); string destination = VoiceMode.FilePath(root, f.path); string staged = Safety.Under(stage, f.path);
                // Preserve a verified shop variant when another file in its package is repaired.
                if (ShopPatchIntegrity.Matches(destination, f)) continue;
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                if (File.Exists(destination)) File.Replace(staged, destination, null); else File.Move(staged, destination);
            }
            log("Validation finale des fichiers..."); var issues = await Verify(root, plan.Target.Manifest, token).ConfigureAwait(false);
            if (issues.Count != 0) throw new InvalidDataException("Installation incomplete : " + issues.Count + " fichiers.");
            token.ThrowIfCancellationRequested();
            Json.Atomic(version, new LocalVersion { version = plan.Target.Manifest.clientVersion, installedAt = DateTime.UtcNow.ToString("o"), manifestHash = plan.Target.Hash });
            log("Installation validee.");
        }
    }
}
public sealed class ServerService {
    readonly Network network; public ServerService(Network network) { this.network = network; }
    public async Task<ServerConfig> Load(string url, string root, Action<string> log, CancellationToken token) {
        string cache = Safety.Under(root, ".aioncl/server-config.json");
        if (!String.IsNullOrEmpty(url)) {
            try {
                var bytes = await network.Small(url, token).ConfigureAwait(false);
                var server = ServerConfig.Parse(Encoding.UTF8.GetString(bytes));
                token.ThrowIfCancellationRequested();
                try {
                    if (!File.Exists(cache) || File.ReadAllText(cache) != Json.Serialize(server)) {
                        if (File.Exists(cache)) File.Copy(cache, cache + ".bak-" + Guid.NewGuid().ToString("N"));
                        Json.Atomic(cache, server);
                    }
                } catch (Exception ex) {
                    if (!(ex is IOException || ex is UnauthorizedAccessException)) throw;
                    log("Configuration serveur chargee ; cache non enregistrable : " + ex.Message);
                }
                return server;
            }
            catch (Exception ex) { token.ThrowIfCancellationRequested(); if (!(ex is InvalidDataException || ex is IOException || ex is HttpRequestException || ex is ArgumentException || ex is InvalidOperationException || ex is OperationCanceledException)) throw; log("Configuration distante indisponible ou invalide : " + ex.Message + " Lecture du cache local."); }
        }
        token.ThrowIfCancellationRequested();
        if (!File.Exists(cache)) throw new InvalidOperationException("Configuration serveur indisponible. Verifiez la connexion Internet puis reessayez JOUER. Si le probleme persiste, contactez AionCL (configuration publique indisponible).");
        var fallback = ServerConfig.Parse(File.ReadAllText(cache));
        log("Configuration serveur en cache utilisee ; la maintenance distante ne peut pas etre actualisee."); return fallback;
    }
    public static async Task<IPAddress> Resolve(string host, CancellationToken token) {
        var task = Dns.GetHostAddressesAsync(host);
        if (await Task.WhenAny(task, Task.Delay(10000, token)).ConfigureAwait(false) != task) { token.ThrowIfCancellationRequested(); throw new IOException("Delai DNS depasse."); }
        var address = (await task.ConfigureAwait(false)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        if (address == null) throw new IOException("Aucune adresse IPv4 pour le serveur."); return address;
    }
    public static async Task Tcp(IPAddress address, int port, CancellationToken token) {
        using (var client = new TcpClient(address.AddressFamily)) {
            var task = client.ConnectAsync(address, port);
            if (await Task.WhenAny(task, Task.Delay(5000, token)).ConfigureAwait(false) != task) { token.ThrowIfCancellationRequested(); throw new IOException("Connexion au serveur : delai depasse."); } await task.ConfigureAwait(false);
        }
    }
}
public static class VoiceMode {
    static readonly string[] Languages = { "FRA", "ENG", "DEU" };
    static string Original(string language) { return "L10N/" + language + "/sounds/voice"; }
    static string Backup(string language) { return ".aioncl/voice-original/" + language; }
    public static void RequireGameClosed() {
        foreach (var p in Process.GetProcesses()) using (p) {
            if (p.ProcessName.Equals("aionclassic", StringComparison.OrdinalIgnoreCase) || p.ProcessName.Equals("aion", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Close Aion before changing voices or repairing files.");
        }
    }
    public static string FilePath(string root, string relative) {
        foreach (string language in Languages) {
            string prefix = Original(language) + "/";
            if (relative.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && Directory.Exists(Safety.Under(root, Backup(language)))) {
                if (Directory.Exists(Safety.Under(root, Original(language)))) throw new IOException("Voice folders conflict. Original files preserved; see .aioncl/voice-original.");
                return Safety.Under(root, Backup(language) + "/" + relative.Substring(prefix.Length));
            }
        }
        return Safety.Under(root, relative);
    }
    public static void Apply(string root, string language, bool korean) {
        RequireGameClosed(); ApplyFiles(root, language, korean);
    }
    internal static void ApplyFiles(string root, string language, bool korean) {
        if (!Languages.Contains(language)) throw new InvalidDataException("Unsupported voice language.");
        string metadata = Safety.Under(root, ".aioncl"); Directory.CreateDirectory(metadata);
        using (var gate = new FileStream(Safety.Under(root, ".aioncl/install.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)) {
            string original = Safety.Under(root, Original(language)), backup = Safety.Under(root, Backup(language));
            if (korean) {
                if (!Directory.Exists(Safety.Under(root, "Sounds/voice"))) throw new IOException("Base voice files are missing.");
                if (Directory.Exists(backup)) { if (Directory.Exists(original)) throw new IOException("Voice folders conflict; no files overwritten."); return; }
                if (!Directory.Exists(original)) throw new IOException("Localized voice files are missing. Repair the game first.");
                CheckTree(original); Directory.CreateDirectory(Path.GetDirectoryName(backup)); Directory.Move(original, backup);
            } else if (Directory.Exists(backup)) {
                if (Directory.Exists(original)) throw new IOException("Voice folders conflict; no files overwritten.");
                CheckTree(backup); Directory.CreateDirectory(Path.GetDirectoryName(original)); Directory.Move(backup, original);
            }
        }
    }
    static void CheckTree(string root) {
        Safety.NoLinks(root);
        foreach (string entry in Directory.EnumerateFileSystemEntries(root)) { Safety.NoLinks(entry); if (Directory.Exists(entry)) CheckTree(entry); }
    }
}
public static class GameLanguage {
    public static string Apply(string arguments, string language) {
        if (language != "FRA" && language != "ENG" && language != "DEU") throw new InvalidDataException("Unsupported game language.");
        var pattern = new Regex(@"(?<!\S)-lang:[A-Za-z]{3}(?!\S)");
        if (pattern.Matches(arguments ?? "").Count != 1) throw new InvalidDataException("Expected one game language argument.");
        return pattern.Replace(arguments, "-lang:" + language);
    }
}
public sealed class GameLauncher {
    readonly LauncherConfig config; public GameLauncher(LauncherConfig config) { this.config = config; }
    public void ValidateInstallation(string root, ManifestResult manifest) { if (Installation.Detect(root, manifest) != ClientState.Valid || !File.Exists(Safety.Under(root, config.gameExecutable))) throw new InvalidOperationException("Client incomplet : installer ou reparer avant de jouer."); }
    public Task<IPAddress> ResolveServer(ServerConfig server, CancellationToken token) { server.Validate(); if (server.maintenance) throw new InvalidOperationException("Serveur en maintenance."); return ServerService.Resolve(server.loginHost, token); }
    public ProcessStartInfo BuildLaunchCommand(string root, ServerConfig server, IPAddress address) {
        server.Validate(); if (server.maintenance) throw new InvalidOperationException("Serveur en maintenance."); if (address.AddressFamily != AddressFamily.InterNetwork) throw new InvalidOperationException("IPv4 requise par ce profil.");
        return new ProcessStartInfo { FileName = Safety.Under(root, config.gameExecutable), WorkingDirectory = Path.GetFullPath(root), Arguments = config.launchArguments.Replace("{ip}", address.ToString()).Replace("{port}", server.loginPort.ToString(CultureInfo.InvariantCulture)), UseShellExecute = false };
    }
    public Process StartGame(ProcessStartInfo command) { return Process.Start(command); }
}
}
