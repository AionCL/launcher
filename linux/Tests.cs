using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using AionCL;

class LinuxTests {
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
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
