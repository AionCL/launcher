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
    [STAThread]
    static void Main() {
        string root = Path.Combine(Path.GetTempPath(), "aioncl-linux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            Environment.SetEnvironmentVariable("AIONCL_WINE", "/bin/echo");
            Environment.SetEnvironmentVariable("AIONCL_WINEPREFIX", Path.Combine(root, "prefix with spaces"));
            var command = LinuxPlatform.WineCommand(new ProcessStartInfo {
                FileName="/tmp/client space/quote\"/aionclassic.bin", WorkingDirectory=root,
                Arguments="-lang:FRA -dnpshop -dingameshop"
            });
            Check(command.FileName=="/bin/echo" && !command.UseShellExecute, "Runner and shell policy");
            Check(command.EnvironmentVariables["WINEPREFIX"]==Path.Combine(root,"prefix with spaces"), "Dedicated prefix");
            command.RedirectStandardOutput=true;
            using(var process=Process.Start(command)) {
                string output=process.StandardOutput.ReadToEnd(); process.WaitForExit();
                Check(process.ExitCode==0 && output=="/tmp/client space/quote\"/aionclassic.bin -lang:FRA -dnpshop -dingameshop\n", "Wine argv quoting");
            }
            Environment.SetEnvironmentVariable("AIONCL_WINEPREFIX", "relative");
            bool rejected=false; try { LinuxPlatform.WineCommand(command); } catch(ArgumentException) { rejected=true; }
            Check(rejected, "Reject relative prefix");
            Environment.SetEnvironmentVariable("XDG_DATA_HOME", root);
            LinuxPlatform.CreateShortcut();
            Check(File.ReadAllText(Path.Combine(root,"applications","aioncl-launcher.desktop")).Contains("Terminal=false"), "Desktop entry");
            var auth=new LauncherAuth(); auth.SaveCredentials("fixture", "not-a-real-password", false);
            Check(auth.Accounts.Count==0, "No credential persistence");
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
                var camera=(Button)typeof(MainForm).GetField("cameraButton",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                var remember=(CheckBox)typeof(MainForm).GetField("authRemember",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
                Check(!camera.Enabled && !remember.Enabled && !remember.Checked, "Linux UI capability gates");
                using(var bitmap=new Bitmap(form.Width,form.Height)) {
                    using(var graphics=Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.Location,Point.Empty,bitmap.Size);
                    bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"linux-preview.png"));
                }
                form.Close();
            }
            Console.WriteLine("PASS Linux: Wine command, prefix, shortcut, credentials, UI navigation and rendering");
        } finally { Directory.Delete(root,true); }
    }
}
