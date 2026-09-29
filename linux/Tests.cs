using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AionCL;

class LinuxTests {
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    static Package MakePackage(string cache,string name,string path,string value) {
        byte[] bytes=System.Text.Encoding.UTF8.GetBytes(value);string archive=Path.Combine(cache,name);
        using(var zip=System.IO.Compression.ZipFile.Open(archive,System.IO.Compression.ZipArchiveMode.Create))
            using(var stream=zip.CreateEntry(path).Open())stream.Write(bytes,0,bytes.Length);
        return new Package {name=name,size=new FileInfo(archive).Length,sha256=Safety.ShaAsync(archive,System.Threading.CancellationToken.None).GetAwaiter().GetResult(),files=new[]{new ClientFile {path=path,size=bytes.Length,sha256=Safety.Sha(bytes)}}};
    }
    static void TestLauncherUpdater(string root) {
        string prefix="https://github.com/AionCL/launcher/releases/download/test/";
        var feed=new LinuxLauncherRelease {schemaVersion=1,version="2.5.42",preview=100,
            deb=new LinuxUpdateAsset {url=prefix+"launcher.deb",sha256=new string('a',64)},
            rpm=new LinuxUpdateAsset {url=prefix+"launcher.rpm",sha256=new string('b',64)},
            portable=new LinuxUpdateAsset {url=prefix+"launcher.zip",sha256=new string('c',64)}};
        feed.Validate();Check(feed.IsNewer("2.5.42",99)&&!feed.IsNewer("2.5.42",100),"Linux preview comparison");
        feed.deb.url="https://example.com/launcher.deb";bool rejected=false;try{feed.Validate();}catch(System.IO.InvalidDataException){rejected=true;}
        Check(rejected,"Reject foreign launcher asset URL");
        string package=Path.Combine(root,"package with spaces.deb");
        var installer=LinuxLauncherUpdater.InstallCommand("deb",package);
        Check(installer.FileName=="pkexec"&&installer.Arguments.Contains("/usr/bin/apt-get install --yes"),"System package update uses authorization and APT");
        var fake=new ProcessStartInfo {FileName="/bin/sh",Arguments="-c "+LinuxPlatform.Quote("exit 7"),UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};
        rejected=false;try{LinuxLauncherUpdater.RunInstaller(fake,delegate{}).GetAwaiter().GetResult();}catch(IOException){rejected=true;}
        Check(rejected,"Installer failures preserve running launcher");
        string zip=Path.Combine(root,"bad-launcher.zip");
        using(var archive=System.IO.Compression.ZipFile.Open(zip,System.IO.Compression.ZipArchiveMode.Create)) {
            using(var stream=new StreamWriter(archive.CreateEntry("../escape").Open()))stream.Write("bad");
        }
        rejected=false;try{LinuxLauncherUpdater.ExtractPortable(zip,Path.Combine(root,"rejected"));}catch(InvalidDataException){rejected=true;}
        Check(rejected&&!File.Exists(Path.Combine(root,"escape")),"Reject traversal before extracting launcher");
        string portableRoot=Path.Combine(root,"portable launcher");Directory.CreateDirectory(portableRoot);
        File.WriteAllText(Path.Combine(portableRoot,"launcher.json"),"local-config");File.WriteAllText(Path.Combine(portableRoot,"old"),"preserve-in-backup");
        zip=Path.Combine(root,"valid-launcher.zip");
        using(var archive=System.IO.Compression.ZipFile.Open(zip,System.IO.Compression.ZipArchiveMode.Create)) {
            foreach(string name in new[]{"AionCL.Launcher.Linux.exe","launcher.json","assets/aioncl-icon.png","aioncl-launcher","aioncl-camera","install-d3dx9.sh","install-dxvk.sh","install-d3dcompiler.sh","prepare-linux-runtime.sh","LINUX.md"})
                using(var stream=new StreamWriter(archive.CreateEntry(name).Open()))stream.Write("fixture");
        }
        string backup=LinuxLauncherUpdater.ApplyPortable(zip,portableRoot);
        Check(File.ReadAllText(Path.Combine(portableRoot,"launcher.json"))=="local-config"&&File.Exists(Path.Combine(backup,"old")),"Portable update preserves configuration and backup");
    }
    [STAThread]
    static void Main() {
        string root = Path.Combine(Path.GetTempPath(), "aioncl-linux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var buildInfo=typeof(LinuxPlatform).Assembly.GetType("AionCL.LinuxBuildInfo");
            string expectedPreview=Environment.GetEnvironmentVariable("AIONCL_PREVIEW_NUMBER")??"28";
            string settingsPath=LinuxGraphicsSettings.PreferencePath;
            string originalSettings=File.Exists(settingsPath)?File.ReadAllText(settingsPath):null;
            new LinuxGraphicsSettings().Save(settingsPath);
            Check(LinuxGraphicsSettings.Load(Path.Combine(root,"absent.json")).nativeD3dx,"Native D3DX enabled by default");
            string settingsFixture=Path.Combine(root,"settings.json");
            new LinuxGraphicsSettings {nativeD3dx=false}.Save(settingsFixture);
            Check(!LinuxGraphicsSettings.Load(settingsFixture).nativeD3dx,"Wine fallback preference persists");
            Check(buildInfo!=null && (string)buildInfo.GetField("PreviewNumber",BindingFlags.Static|BindingFlags.NonPublic).GetRawConstantValue()==expectedPreview, "Launcher title preview matches package build number");
            Environment.SetEnvironmentVariable("AIONCL_WINE", "/bin/echo");
            Environment.SetEnvironmentVariable("AIONCL_WINEPREFIX", Path.Combine(root, "prefix with spaces"));
            Environment.SetEnvironmentVariable("WINEDLLOVERRIDES", "d3d9=n;version=b");
            var command = LinuxPlatform.WineCommand(new ProcessStartInfo {
                FileName="/tmp/client space/quote\"/aionclassic.bin", WorkingDirectory=root,
                Arguments="-lang:FRA -dnpshop -dingameshop"
            });
            Check(command.FileName=="/bin/echo" && !command.UseShellExecute, "Runner and shell policy");
            Check(command.EnvironmentVariables["WINEPREFIX"]==Path.Combine(root,"prefix with spaces"), "Dedicated prefix");
            Check(command.EnvironmentVariables["WINEDLLOVERRIDES"]=="d3d9=n;version=b;version=n,b;d3d9=n,b;d3dcompiler_47=n,b;d3dx9_38=n,b", "Load client proxies and native shader compiler while preserving other Wine overrides");
            Environment.SetEnvironmentVariable("WINEDLLOVERRIDES", null);
            new LinuxGraphicsSettings {nativeD3dx=false}.Save(settingsPath);
            Check(LinuxPlatform.WineCommand(command).EnvironmentVariables["WINEDLLOVERRIDES"].EndsWith(";d3dx9_38=b"),"Wine mode overrides existing native registry settings");
            new LinuxGraphicsSettings().Save(settingsPath);
            Check(LinuxPlatform.WineCommand(command).EnvironmentVariables["WINEDLLOVERRIDES"]=="version=n,b;d3d9=n,b;d3dcompiler_47=n,b;d3dx9_38=n,b", "No-IP, DXVK and native shader compiler enabled without existing overrides");
            string wineSetup=Path.Combine(root,"wine-setup");
            string wineCapture=Path.Combine(root,"wine-setup-args");
            File.WriteAllText(wineSetup,"#!/bin/sh\nprintf '%s\\n' \"$*\" > \"$AIONCL_TEST_CAPTURE\"\n");
            Process.Start("chmod","+x \""+wineSetup+"\"").WaitForExit();
            Environment.SetEnvironmentVariable("AIONCL_WINE",wineSetup);
            Environment.SetEnvironmentVariable("AIONCL_TEST_CAPTURE",wineCapture);
            LinuxPlatform.ConfigureGameWindowsVersion(new ProcessStartInfo { FileName="/tmp/client/aionclassic.bin", WorkingDirectory=root });
            Check(File.ReadAllText(wineCapture).Trim()=="reg add HKCU\\Software\\Wine\\AppDefaults\\aionclassic.bin /v Version /t REG_SZ /d win7 /f", "Set per-game Wine compatibility to Windows 7");
            Environment.SetEnvironmentVariable("AIONCL_TEST_CAPTURE",null);
            Environment.SetEnvironmentVariable("AIONCL_WINE", "/bin/echo");
            string runtimeBase=Path.Combine(root,"runtime helpers");Directory.CreateDirectory(runtimeBase);
            string runtimeClient=Path.Combine(root,"client path");Directory.CreateDirectory(runtimeClient);
            string runtimeScript=Path.Combine(runtimeBase,"prepare-linux-runtime.sh");
            string runtimeCapture=Path.Combine(root,"runtime-args");
            File.WriteAllText(runtimeScript,"printf '%s|%s|%s|%s\\n' \"$WINEPREFIX\" \"$1\" \"$2\" \"$3\" > \"$AIONCL_TEST_CAPTURE\"\nprintf 'runtime-helper-pass\\n'\n");
            Environment.SetEnvironmentVariable("AIONCL_TEST_CAPTURE",runtimeCapture);
            var runtimeLogs=new System.Collections.Generic.List<string>();
            LinuxPlatform.PrepareRuntime(runtimeClient,runtimeBase,System.Threading.CancellationToken.None,runtimeLogs.Add).GetAwaiter().GetResult();
            Check(File.ReadAllText(runtimeCapture).Trim()==Path.Combine(root,"prefix with spaces")+"|"+Path.Combine(root,"prefix with spaces")+"|"+runtimeClient+"|native", "Runtime preparation receives Wine prefix and client paths safely");
            new LinuxGraphicsSettings {nativeD3dx=false}.Save(settingsPath);
            LinuxPlatform.PrepareRuntime(runtimeClient,runtimeBase,System.Threading.CancellationToken.None,runtimeLogs.Add).GetAwaiter().GetResult();
            Check(File.ReadAllText(runtimeCapture).Trim().EndsWith("|wine"),"Runtime receives selected Wine fallback mode");
            new LinuxGraphicsSettings().Save(settingsPath);
            Check(runtimeLogs.Contains("runtime-helper-pass"), "Runtime preparation output reaches launcher log");
            Environment.SetEnvironmentVariable("AIONCL_TEST_CAPTURE",null);
            command.RedirectStandardOutput=true;
            using(var process=Process.Start(command)) {
                string output=process.StandardOutput.ReadToEnd(); process.WaitForExit();
                Check(process.ExitCode==0 && output=="/tmp/client space/quote\"/aionclassic.bin -lang:FRA -dnpshop -dingameshop\n", "Wine argv quoting");
            }
            Environment.SetEnvironmentVariable("AIONCL_WINEPREFIX", "relative");
            bool rejected=false; try { LinuxPlatform.WineCommand(command); } catch(ArgumentException) { rejected=true; }
            Check(rejected, "Reject relative prefix");
            rejected=false; try { LinuxPlatform.ConfigureGameWindowsVersion(command); } catch(ArgumentException) { rejected=true; }
            Check(rejected, "Reject relative prefix for compatibility configuration");
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", root);
            LinuxPlatform.CreateShortcut();
            Check(File.ReadAllText(Path.Combine(root,"applications","aioncl-launcher.desktop")).Contains("Terminal=false"), "Desktop entry");
            Environment.SetEnvironmentVariable("AIONCL_CONFIG_DIR",Path.Combine(root,"credentials"));
            var auth=new LauncherAuth(); auth.SaveCredentials("first", "one", true); auth.SaveCredentials("second", "two", true);
            Check(auth.Accounts.Count==2 && auth.Find("FIRST").password=="one", "Session account selection");
            auth=new LauncherAuth(); Check(auth.Accounts.Count==2 && auth.Find("first").password=="one", "Accounts persist across launcher instances");
            Check(File.Exists(Path.Combine(root,"credentials","credentials.json")), "Plain JSON credentials file exists in user config directory");
            auth.SaveCredentials("first", "changed", true);
            Check(auth.Accounts.Count==2 && auth.Accounts[0].username=="first" && auth.Find("first").password=="changed", "Session account replacement");
            auth.SaveCredentials("first", "changed", false);
            Check(auth.Accounts.Count==1 && auth.Find("first")==null, "Unchecked account is forgotten");
            auth.ForgetCredentials(); Check(auth.Accounts.Count==0 && !File.Exists(Path.Combine(root,"credentials","credentials.json")), "Clear persisted accounts");
            Environment.SetEnvironmentVariable("AIONCL_CONFIG_DIR",null);
            var package=new Package { name="fixture.zip",size=100 };
            Check(LinuxPlatform.DownloadBytesNeeded(new[]{package},root)==100,"Fresh download capacity");
            File.WriteAllBytes(Path.Combine(root,"fixture.zip.part"),new byte[60]);
            Check(LinuxPlatform.DownloadBytesNeeded(new[]{package},root)==40,"Resume capacity excludes existing bytes");
            File.WriteAllBytes(Path.Combine(root,"fixture.zip"),new byte[100]);
            Check(LinuxPlatform.DownloadBytesNeeded(new[]{package},root)==0,"Complete cache capacity");
            string locale=Path.Combine(root,"l10n","FRA"); Directory.CreateDirectory(locale);
            File.WriteAllText(Path.Combine(locale,"FRA.pak"),"fixture");
            Check(Safety.Under(root,"L10N/fra/fra.pak")==Path.Combine(locale,"FRA.pak"),"Windows casing resolves existing Linux language files");
            Check(Safety.Under(root,"L10N/FRA/sounds/new.pak")==Path.Combine(locale,"sounds","new.pak"),"New files stay under existing lowercase directory");
            Directory.CreateDirectory(Path.Combine(root,"ambiguous"));
            File.WriteAllText(Path.Combine(root,"ambiguous","NAME"),"a");File.WriteAllText(Path.Combine(root,"ambiguous","name"),"b");
            bool ambiguous=false; try { Safety.Under(root,"ambiguous/Name"); } catch(IOException){ambiguous=true;}
            Check(ambiguous,"Reject ambiguous case matches");
            using(var hash=new OpenSslSha256()) {
                var input=System.Text.Encoding.ASCII.GetBytes("xabcx");
                hash.TransformBlock(input,1,1,input,1);hash.TransformFinalBlock(input,2,2);
                Check(BitConverter.ToString(hash.Hash).Replace("-","").ToLowerInvariant()=="ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad","Native SHA-256 incremental offsets");
                Check(BitConverter.ToString(hash.ComputeHash(new byte[0])).Replace("-","").ToLowerInvariant()=="e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","Native SHA-256 reset and empty input");
            }
            var cache=Path.Combine(root,"parallel-cache");var stage=Path.Combine(root,"parallel-stage");Directory.CreateDirectory(cache);
            var first=MakePackage(cache,"first.zip","l10n/FRA/test.pak","original");
            var other=MakePackage(cache,"other.zip","data/other.pak","independent");
            var patch=MakePackage(cache,"patch.zip","L10N/FRA/test.pak","patched");
            LinuxPlatform.ExtractPackages(new InstallPlan { Packages=new[]{first,other,patch} },cache,stage,System.Threading.CancellationToken.None,null,delegate{}).GetAwaiter().GetResult();
            Check(File.ReadAllText(Safety.Under(stage,"L10N/FRA/test.pak"))=="patched","Parallel extraction preserves patch order and casing");
            Check(!Directory.Exists(Path.Combine(stage,"L10N")),"No duplicate case directory from patches");
            File.AppendAllText(Path.Combine(cache,"first.zip"),"corrupt");
            bool corrupt=false;try { LinuxPlatform.ExtractPackages(new InstallPlan {Packages=new[]{first,other}},cache,stage,System.Threading.CancellationToken.None,null,delegate{}).GetAwaiter().GetResult(); }catch(InvalidDataException){corrupt=true;}
            Check(corrupt,"Concurrent extraction rejects corrupt archives");
            TestLauncherUpdater(root);
            if(originalSettings==null)File.Delete(settingsPath);else File.WriteAllText(settingsPath,originalSettings);
            Application.EnableVisualStyles();
            using(var form=new MainForm(true)) {
                form.Show(); Application.DoEvents();
                int deliveries=0, last=-1;
                using(var progress=new LinuxUiProgress<int>(form, value=>{deliveries++;last=value;})) {
                    System.Threading.Tasks.Task.Run(()=>{for(int i=0;i<100000;i++)progress.Report(i);}).Wait();
                    var deadline=DateTime.UtcNow.AddSeconds(2);
                    while(last<0 && DateTime.UtcNow<deadline) { Application.DoEvents(); System.Threading.Thread.Sleep(10); }
                    Check(deliveries==1 && last==99999,"Coalesce high-rate progress without flooding X11");
                }
                Check(LinuxPlatform.InstallationDrive(root).AvailableFreeSpace>0,"Filesystem capacity");
                foreach(string field in new[]{"helpButton", "journalButton", "homeButton"}) {
                    var button=(Button)typeof(MainForm).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                    button.PerformClick(); Application.DoEvents();
                }
                typeof(MainForm).GetField("releases",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,
                    new UpdateFeed { launcher=new LauncherRelease { version="99.0.0", assetUrl="https://example.com/windows.zip", sha256=new string('a',64) } });
                typeof(MainForm).GetMethod("RefreshUpdateUi",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                var update=(Button)typeof(MainForm).GetField("launcherUpdateButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                Check(!update.Visible, "Never offer a Windows launcher update on Linux");
                var linuxFeed=new LinuxLauncherRelease {schemaVersion=1,version=Updates.LauncherVersion,preview=Int32.Parse(expectedPreview)+1};
                typeof(MainForm).GetField("linuxRelease",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(form,linuxFeed);
                typeof(MainForm).GetMethod("RefreshUpdateUi",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                Check(update.Visible,"Offer a newer Linux launcher preview");
                linuxFeed.preview=Int32.Parse(expectedPreview);
                typeof(MainForm).GetMethod("RefreshUpdateUi",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(form,null);
                Check(!update.Visible,"Do not offer same Linux preview");
                var camera=(Button)typeof(MainForm).GetField("cameraButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                var remember=(CheckBox)typeof(MainForm).GetField("authRemember",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                Check(camera.Enabled && !remember.Visible, "Linux credential action and camera enabled");
                using(var bitmap=new Bitmap(form.Width,form.Height)) {
                    using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.Location,Point.Empty,bitmap.Size);
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"linux-preview.png"));
                }
                var settingsButton=(Button)typeof(MainForm).GetField("helpButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                settingsButton.PerformClick();Application.DoEvents();
                var graphicsButton=(Button)typeof(MainForm).GetField("linuxGraphicsButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                Check(graphicsButton.Visible&&graphicsButton.Bottom<=graphicsButton.Parent.Height,"Linux graphics settings visible inside settings panel");
                using(var bitmap=new Bitmap(form.Width,form.Height)) {
                    using(var graphics=Graphics.FromImage(bitmap))graphics.CopyFromScreen(form.Location,Point.Empty,bitmap.Size);
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"linux-settings.png"));
                }
                form.Close();
            }
            Console.WriteLine("PASS Linux: Wine command, prefix, shortcut, credentials, UI navigation and rendering");
        } finally { Directory.Delete(root,true); }
    }
}
