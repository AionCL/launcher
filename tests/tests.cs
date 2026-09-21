using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AionCL
{
    public static class Tests
    {
        static int passed = 0;
        static int failed = 0;

        public static void Main()
        {
            Run("Safety.Relative valid path", TestRelativeValid);
            Run("Safety.Relative traversal rejected", TestRelativeTraversal);
            Run("Safety.Relative absolute rejected", TestRelativeAbsolute);
            Run("SHA-256", TestSha256);
            Run("Detect absent", TestDetectAbsent);
            Run("Detect potential", TestDetectPotential);
            Run("Launch command dry-run", TestLaunchCommand);
            Run("Camera preference bounds and persistence", TestCameraSettings);
            RunAsync("Camera helper integrity gate", TestCameraHelper).GetAwaiter().GetResult();
            RunAsync("ZIP extraction", TestZipExtraction).GetAwaiter().GetResult();
            RunAsync("Server configuration scenarios", RegressionTests.ServerScenarios).GetAwaiter().GetResult();
            RunAsync("Korean pack install remove cache and cancellation", RegressionTests.KoreanPackScenarios).GetAwaiter().GetResult();
            RunAsync("Client and launcher update discovery", RegressionTests.UpdateScenarios).GetAwaiter().GetResult();
            RunAsync("Install resume repair and generated files", RegressionTests.InstallScenarios).GetAwaiter().GetResult();

            Console.WriteLine();
            Console.WriteLine("Passed: " + passed);
            Console.WriteLine("Failed: " + failed);

            Environment.Exit(failed == 0 ? 0 : 1);
        }

        static void TestCameraSettings() {
            var settings = new CameraSettings { enabled = true, fov = 170, distance = 100 };
            settings.Validate();
            string directory = Path.Combine(Path.GetTempPath(), "aioncl-camera-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "camera.json");
            try {
                settings.Save(path);
                var loaded = CameraSettings.Load(path);
                if (!loaded.enabled || loaded.fov != 170 || loaded.distance != 100) throw new Exception("Camera roundtrip failed");
                settings.distance = 5; settings.fov = 60; settings.Save(path);
                if (CameraSettings.Load(path).distance != 5) throw new Exception("Camera replacement failed");
                foreach (int invalid in new[] { 4, 101 }) {
                    settings.distance = invalid;
                    bool rejected = false;
                    try { settings.Validate(); } catch (InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("Camera distance bound not enforced");
                }
                settings.distance = 32;
                foreach (int invalid in new[] { 59, 171 }) {
                    settings.fov = invalid;
                    bool rejected = false;
                    try { settings.Validate(); } catch (InvalidDataException) { rejected = true; }
                    if (!rejected) throw new Exception("Camera FOV bound not enforced");
                }
            } finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        static async Task TestCameraHelper() {
            string root = Path.Combine(Path.GetTempPath(), "aioncl-helper-" + Guid.NewGuid().ToString("N"));
            try {
                Directory.CreateDirectory(Path.Combine(root, "tools"));
                string path = Path.Combine(root, "tools", "AionCL.Camera.exe");
                byte[] bytes = Encoding.ASCII.GetBytes("fixture-not-executable");
                File.WriteAllBytes(path, bytes);
                var manifest = new Manifest { packages = new[] { new Package { files = new[] {
                    new ClientFile { path = "tools/AionCL.Camera.exe", size = bytes.Length, sha256 = Safety.Sha(bytes) }
                } } } };
                if (await CameraSettings.VerifyHelper(root, manifest, CancellationToken.None) != path)
                    throw new Exception("Wrong helper path");
                bytes[0]++; File.WriteAllBytes(path, bytes);
                bool rejected = false;
                try { await CameraSettings.VerifyHelper(root, manifest, CancellationToken.None); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Corrupt helper accepted");
                File.Delete(path); rejected = false;
                try { await CameraSettings.VerifyHelper(root, manifest, CancellationToken.None); }
                catch (InvalidDataException) { rejected = true; }
                if (!rejected) throw new Exception("Missing helper accepted");
            } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
        }

        static void Run(string name, Action test)
        {
            try
            {
                test();
                Console.WriteLine("[OK]   " + name);
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + ex.Message);
                failed++;
            }
        }

        static async Task RunAsync(string name, Func<Task> test)
        {
            try
            {
                await test();
                Console.WriteLine("[OK]   " + name);
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + ex.Message);
                failed++;
            }
        }

        static void ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new Exception("Expected exception: " + typeof(T).Name);
        }

        static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }

        static void TestRelativeValid()
        {
            Safety.Relative("bin64/aionclassic.bin");
            Safety.Relative("Data/items.pak");
        }

        static void TestRelativeTraversal()
        {
            ExpectThrows<InvalidDataException>(
                delegate { Safety.Relative("../evil.txt"); }
            );

            ExpectThrows<InvalidDataException>(
                delegate { Safety.Relative("foo/../../evil.txt"); }
            );
        }

        static void TestRelativeAbsolute()
        {
            ExpectThrows<InvalidDataException>(
                delegate { Safety.Relative("C:/Windows/system32/calc.exe"); }
            );

            ExpectThrows<InvalidDataException>(
                delegate { Safety.Relative("/etc/passwd"); }
            );
        }

        static void TestSha256()
        {
            byte[] bytes = Encoding.UTF8.GetBytes("AionCL");

            string hash = Safety.Sha(bytes);

            Assert(
                hash == "da0438d0e5511de6a9a51fd0ef8e5d4143cbedd7c9a2a28ccfaac2327d298b4b",
                "Unexpected SHA-256: " + hash
            );
        }

        static void TestDetectAbsent()
        {
            string root = NewTemp();

            try
            {
                Directory.CreateDirectory(root);

                var manifest = MinimalManifest();

                ClientState state = Installation.Detect(root, manifest);

                Assert(
                    state == ClientState.Absent,
                    "Expected Absent, got " + state
                );
            }
            finally
            {
                DeleteTree(root);
            }
        }

        static void TestDetectPotential()
        {
            string root = NewTemp();

            try
            {
                Directory.CreateDirectory(
                    Path.Combine(root, "bin64")
                );

                File.WriteAllBytes(
                    Path.Combine(root, "bin64", "aionclassic.bin"),
                    new byte[] { 1, 2, 3 }
                );

                ClientState state =
                    Installation.Detect(
                        root,
                        MinimalManifest()
                    );

                Assert(
                    state == ClientState.Potential,
                    "Expected Potential, got " + state
                );
            }
            finally
            {
                DeleteTree(root);
            }
        }

        static void TestLaunchCommand()
        {
            string root = NewTemp();

            try
            {
                Directory.CreateDirectory(
                    Path.Combine(root, "bin64")
                );

                string exe =
                    Path.Combine(
                        root,
                        "bin64",
                        "aionclassic.bin"
                    );

                File.WriteAllBytes(
                    exe,
                    new byte[] { 1 }
                );

                var config = new LauncherConfig
                {
                    product = "AionCL",
                    launcherVersion = "1.0.0",
                    clientBaseVersion = "2.4.0",
                    manifestUrl =
                        "https://example.com/install-manifest.json",
                    serverConfigUrl = "",
                    maxParallelDownloads = 2,
                    requestTimeoutSeconds = 30,
                    gameExecutable =
                        "bin64/aionclassic.bin",
                    launchArguments =
                        "-ip:{ip} -port:{port} -cc:2 -lang:FRA"
                };

                config.Validate();

                var server = new ServerConfig
                {
                    version = 1,
                    serverName = "AionCL",
                    loginHost = "localhost",
                    loginPort = 2106,
                    gamePort = 7777,
                    maintenance = false
                };

                var launcher =
                    new GameLauncher(config);

                var command =
                    launcher.BuildLaunchCommand(
                        root,
                        server,
                        IPAddress.Parse("127.0.0.1")
                    );

                Assert(
                    command.Arguments.Contains("-ip:127.0.0.1"),
                    "IP argument missing."
                );

                Assert(
                    command.Arguments.Contains("-port:2106"),
                    "Port argument missing."
                );

                Assert(
                    command.FileName ==
                    Path.GetFullPath(exe),
                    "Executable path mismatch."
                );
                Assert(command.WorkingDirectory == Path.GetFullPath(root), "Working directory mismatch.");
            }
            finally
            {
                DeleteTree(root);
            }
        }

        static async Task TestZipExtraction()
        {
            string root = NewTemp();
            string stage = Path.Combine(root, "stage");
            string zipPath = Path.Combine(root, "fixture.zip");

            try
            {
                Directory.CreateDirectory(root);

                byte[] payload =
                    Encoding.UTF8.GetBytes(
                        "AionCL fixture"
                    );

                using (var zip =
                    ZipFile.Open(
                        zipPath,
                        ZipArchiveMode.Create
                    ))
                {
                    var entry =
                        zip.CreateEntry(
                            "Data/test.txt"
                        );

                    using (var stream = entry.Open())
                    {
                        stream.Write(
                            payload,
                            0,
                            payload.Length
                        );
                    }
                }

                string fileHash =
                    Safety.Sha(payload);

                string zipHash =
                    await Safety.ShaAsync(
                        zipPath,
                        CancellationToken.None
                    );

                long zipSize =
                    new FileInfo(zipPath).Length;

                var package = new Package
                {
                    name =
                        "aioncl-client-2.4.0-001.zip",
                    size = zipSize,
                    uncompressedSize =
                        payload.Length,
                    fileCount = 1,
                    sha256 = zipHash,
                    mirrors = new[]
                    {
                        "https://example.com/test.zip"
                    },
                    files = new[]
                    {
                        new ClientFile
                        {
                            path = "Data/test.txt",
                            size = payload.Length,
                            sha256 = fileHash
                        }
                    }
                };

                await Installation.Extract(
                    zipPath,
                    package,
                    stage,
                    CancellationToken.None
                );

                string extracted =
                    Path.Combine(
                        stage,
                        "Data",
                        "test.txt"
                    );

                Assert(
                    File.Exists(extracted),
                    "Extracted file missing."
                );

                Assert(
                    File.ReadAllText(extracted) ==
                    "AionCL fixture",
                    "Extracted file content mismatch."
                );
            }
            finally
            {
                DeleteTree(root);
            }
        }

        static ManifestResult MinimalManifest()
        {
            return new ManifestResult
            {
                Hash =
                    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",

                Manifest = new Manifest
                {
                    formatVersion = 1,
                    product = "AionCL",
                    gameVersion = "2.4",
                    clientVersion = "2.4.0",
                    archiveFormat = "zip",
                    sourceBytes = 1,
                    compressedBytes = 1,
                    sourceFileCount = 1,

                    packages = new[]
                    {
                        new Package
                        {
                            name =
                                "aioncl-client-2.4.0-001.zip",
                            size = 1,
                            uncompressedSize = 1,
                            fileCount = 1,
                            sha256 =
                                "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                            mirrors = new[]
                            {
                                "https://example.com/test.zip"
                            },

                            files = new[]
                            {
                                new ClientFile
                                {
                                    path =
                                        "Data/test.dat",
                                    size = 1,
                                    sha256 =
                                        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                                }
                            }
                        }
                    }
                }
            };
        }

        static string NewTemp()
        {
            return Path.Combine(
                Path.GetTempPath(),
                "AionCL-Tests-" +
                Guid.NewGuid().ToString("N")
            );
        }

        static void DeleteTree(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch
            {
            }
        }
    }
}
