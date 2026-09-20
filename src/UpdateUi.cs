using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace AionCL {
public sealed partial class MainForm {
    Panel updatePanel;
    Panel noticeCard;
    Label noticeTitle, noticeMessage;
    Button noticeClose, noticeAction;
    Label updateNotice;
    Button checkUpdatesButton,launcherUpdateButton;
    readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer { Interval=300000 };
    bool checkingUpdates;
    UpdateFeed releases;
    string updateError;
    private void BuildUpdateUi() {
        updatePanel=new Panel { BackColor=Color.FromArgb(178,23,39,51) };(workspace ?? (Control)this).Controls.Add(updatePanel);
        updateNotice=new Label { Bounds=new Rectangle(16,12,610,42),ForeColor=Color.FromArgb(176,218,233),Text="AionCL · Updates",Font=new Font("Segoe UI",9) };updatePanel.Controls.Add(updateNotice);
        checkUpdatesButton=ButtonAt(updatePanel,"",650,12,165,34,false);checkUpdatesButton.Click += async delegate { await CheckUpdates(false); };
        launcherUpdateButton=ButtonAt(updatePanel,"",825,12,200,34,true);launcherUpdateButton.Visible=false;
        launcherUpdateButton.Click += async delegate { await UpdateLauncherAsync(); };
        noticeCard=new SurfacePanel { BackColor=Color.FromArgb(185,20,40,63), Visible=false, Padding=new Padding(18) };
        noticeTitle=new Label { ForeColor=Color.White, Font=new Font("Segoe UI",12,FontStyle.Bold), AutoEllipsis=true };
        noticeMessage=new Label { ForeColor=Color.FromArgb(207,226,239), Font=new Font("Segoe UI",9), AutoEllipsis=true };
        noticeClose=ButtonAt(noticeCard,"×",0,0,30,28,false); noticeClose.Click += delegate { noticeCard.Visible=false; };
        noticeAction=ButtonAt(noticeCard,"",0,0,150,32,true); noticeAction.Visible=false; noticeAction.Click += delegate { if(releases!=null&&releases.notice!=null&&!String.IsNullOrWhiteSpace(releases.notice.actionUrl)) Process.Start(new ProcessStartInfo { FileName=Safety.Https(releases.notice.actionUrl).AbsoluteUri,UseShellExecute=true }); };
        noticeCard.Controls.Add(noticeTitle); noticeCard.Controls.Add(noticeMessage); (workspace ?? (Control)this).Controls.Add(noticeCard); noticeCard.BringToFront();
        updateTimer.Tick += async delegate { if(!busy&&!checkingUpdates)await CheckUpdates(false); };
        FormClosed += delegate {updateTimer.Stop();updateTimer.Dispose();};
    }
    private bool ClientUpdateAvailable() {
        if(manifest==null||String.IsNullOrWhiteSpace(pathBox.Text))return false;
        try {var p=Safety.Under(pathBox.Text,".aioncl/version.json");if(!File.Exists(p))return false;var local=Json.Parse<LocalVersion>(File.ReadAllText(p));return local!=null&&Updates.Newer(manifest.Manifest.clientVersion,local.version);}
        catch(Exception ex){if(ex is IOException||ex is ArgumentException||ex is InvalidDataException)return false;throw;}
    }
    private void RefreshUpdateUi() {
        if(updatePanel==null)return;
        checkUpdatesButton.Text=L("Rechercher les mises à jour","Check for updates","Updates suchen");
        launcherUpdateButton.Text=L("Mettre à jour le launcher","Update launcher","Launcher aktualisieren");
        checkUpdatesButton.Enabled=!busy&&!checkingUpdates&&config!=null;
        bool launcherAvailable=releases!=null&&Updates.Newer(releases.launcher.version,Updates.LauncherVersion);
        bool directLauncherUpdate=launcherAvailable&&releases.launcher!=null&&!String.IsNullOrWhiteSpace(releases.launcher.assetUrl)&&!String.IsNullOrWhiteSpace(releases.launcher.sha256);
        launcherUpdateButton.Visible=directLauncherUpdate&&!busy&&!checkingUpdates;
        string available=launcherAvailable?L(directLauncherUpdate?"Nouveau launcher disponible. ":"Nouveau launcher disponible dans le journal. ",directLauncherUpdate?"New launcher available. ":"A new launcher is available in the log. ",directLauncherUpdate?"Neuer Launcher verfügbar. ":"Ein neuer Launcher ist im Journal verfügbar. "):"";
        if(ClientUpdateAvailable()) { playButton.Text=L("↻   METTRE À JOUR LE JEU","↻   UPDATE GAME","↻   SPIEL AKTUALISIEREN"); ApplyActionStyle(playButton,Color.FromArgb(205,125,32),Color.FromArgb(230,151,48),15F); }
        if(ClientUpdateAvailable()){available+=L("Mise à jour du jeu : ","Game update: ","Spielupdate: ")+manifest.Manifest.clientVersion;installButton.Text=L("METTRE À JOUR LE JEU","UPDATE GAME","SPIEL AKTUALISIEREN");ApplyActionStyle(installButton,Color.FromArgb(205,125,32),Color.FromArgb(230,151,48),10F);}
        else if(!busy){installButton.Text=L("Installer / reprendre","Install / resume","Installieren / fortsetzen");ApplyActionStyle(installButton,Color.FromArgb(31,145,123),Color.FromArgb(43,170,145),10F);}
        ApplyActionStyle(verifyButton,Color.FromArgb(123,83,166),Color.FromArgb(148,106,193),10F);
        updateNotice.ForeColor=updateError!=null?Color.FromArgb(255,202,125):Color.FromArgb(176,218,233);
        updateNotice.Text=checkingUpdates?L("Recherche des mises à jour…","Checking for updates…","Suche nach Updates…"):available.Length>0?available:updateError!=null?updateError:releases==null?L("Mises à jour du jeu et du launcher","Game and launcher updates","Spiel- und Launcherupdates"):L("Vous êtes à jour","You are up to date","Du bist auf dem neuesten Stand")+" · Launcher "+Updates.LauncherVersion;
        RefreshNoticeUi();
        LayoutLauncher();
    }
    private async Task UpdateLauncherAsync() {
        if(releases==null||releases.launcher==null||String.IsNullOrWhiteSpace(releases.launcher.assetUrl)||String.IsNullOrWhiteSpace(releases.launcher.sha256)) { if(releases!=null) Process.Start(new ProcessStartInfo { FileName=Safety.Https(releases.launcher.downloadPage).AbsoluteUri,UseShellExecute=true }); return; }
        string root=AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        string staging=Path.Combine(Path.GetTempPath(),"AionCL-launcher-update-"+Guid.NewGuid().ToString("N")); string zip=Path.Combine(staging,"launcher.zip"); string extract=Path.Combine(staging,"new"); string helperSource=Path.Combine(root,"AionCL.Updater.exe");
        if(!File.Exists(helperSource)) { MessageBox.Show(this,L("Le module de mise à jour est absent de cette installation.","The update module is missing from this installation.","Das Update-Modul fehlt in dieser Installation."),"AionCL",MessageBoxButtons.OK,MessageBoxIcon.Error); return; }
        try {
            Directory.CreateDirectory(extract); SetBusy(true); cts=new CancellationTokenSource(); progressBar.Style=ProgressBarStyle.Marquee; statusLabel.Text=L("Téléchargement du nouveau launcher…","Downloading the new launcher…","Neuen Launcher wird heruntergeladen…");
            using(var network=new Network(config.requestTimeoutSeconds)) await network.Download(releases.launcher.assetUrl,zip,null,cts.Token);
            if(!String.Equals(await Safety.ShaAsync(zip,cts.Token),releases.launcher.sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("SHA-256 du launcher invalide.");
            using(var archive=ZipFile.OpenRead(zip)) {
                string prefix=Path.GetFullPath(extract).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                foreach(var entry in archive.Entries) { string candidate=Path.GetFullPath(Path.Combine(extract,entry.FullName.Replace('/',Path.DirectorySeparatorChar))); if(!candidate.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Archive launcher avec chemin interdit."); }
            }
            ZipFile.ExtractToDirectory(zip,extract);
            string stagedExe=Path.Combine(extract,"AionCL.Launcher.exe"); if(!File.Exists(stagedExe)) throw new InvalidDataException("Archive launcher invalide.");
            string helper=Path.Combine(Path.GetTempPath(),"AionCL-updater-"+Guid.NewGuid().ToString("N")+".exe"); File.Copy(helperSource,helper);
            var start=new ProcessStartInfo { FileName=helper,UseShellExecute=true,Verb="runas",Arguments="--pid "+Process.GetCurrentProcess().Id.ToString()+" --source \""+extract.Replace("\"","\\\"")+"\" --target \""+root.Replace("\"","\\\"")+"\" --exe AionCL.Launcher.exe" };
            Process.Start(start); Application.Exit();
        } catch(OperationCanceledException) { statusLabel.Text=L("Mise à jour interrompue.","Update interrupted.","Update unterbrochen."); }
        catch(Exception ex) { statusLabel.Text=L("Échec de la mise à jour du launcher.","Launcher update failed.","Launcher-Update fehlgeschlagen."); Log(ex.Message); MessageBox.Show(this,ex.Message,"AionCL",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { if(!IsDisposed){ progressBar.Style=ProgressBarStyle.Continuous; SetBusy(false); } }
    }
    private void RefreshNoticeUi() {
        if(noticeCard==null) return;
        var n=releases==null?null:releases.notice;
        if(n==null||String.IsNullOrWhiteSpace(n.title)||String.IsNullOrWhiteSpace(n.message)) { noticeCard.Visible=false; return; }
        noticeTitle.Text=n.title; noticeMessage.Text=n.message; noticeAction.Text=n.actionLabel??""; noticeAction.Visible=!String.IsNullOrWhiteSpace(n.actionUrl)&&!String.IsNullOrWhiteSpace(n.actionLabel); noticeCard.Visible=storyPage=="home"; if(noticeCard.Visible)noticeCard.BringToFront();
    }
    private async Task CheckUpdates(bool startup) {
        if(config==null||checkingUpdates||(!startup&&busy))return;
        checkingUpdates=true;updateError=null;
        cts=new CancellationTokenSource();
        if(!startup)SetBusy(true);RefreshUpdateUi();
        try {
            if(String.IsNullOrWhiteSpace(config.updateFeedUrl))throw new InvalidDataException("Update feed URL missing.");
            using(var network=new Network(config.requestTimeoutSeconds)) {
                var feed=await Updates.Fetch(network,config.updateFeedUrl,cts.Token);
                var target=Updates.Target(config,feed);
                ManifestResult next=manifest;
                if(manifest==null||manifest.Manifest.clientVersion!=feed.client.version||config.manifestUrl!=feed.client.manifestUrl)next=await network.ManifestAsync(target,cts.Token);
                config=target;manifest=next;releases=feed;
            }
            Log(L("Recherche des mises à jour terminée.","Update check complete.","Updatesuche abgeschlossen."));
        }catch(OperationCanceledException){updateError=L("Recherche interrompue. Réessayez.","Update check interrupted. Retry.","Updatesuche unterbrochen. Erneut versuchen.");}
        catch(Exception ex){updateError=DescribeUpdateFailure(ex);Log(L("Recherche des mises à jour : ","Update check: ","Updatesuche: ")+ex.Message);}
        finally {checkingUpdates=false;if(!IsDisposed){if(!startup)SetBusy(false);UpdateVersionText();RefreshUpdateUi();}}
    }
    private string DescribeUpdateFailure(Exception error) {
        string details=error==null?"":error.Message??"";
        int http=details.IndexOf("HTTP ",StringComparison.OrdinalIgnoreCase);
        if(http>=0) {
            int end=details.IndexOf(' ',http+5);
            string code=end>http?details.Substring(http+5,end-http-5):details.Substring(http+5);
            return L("⚠ Service de mise à jour inaccessible (HTTP "+code+"). Réessayez plus tard.","⚠ Update service unavailable (HTTP "+code+"). Try again later.","⚠ Update-Dienst nicht erreichbar (HTTP "+code+"). Später erneut versuchen.");
        }
        if(!NetworkInterface.GetIsNetworkAvailable() || IsNetworkException(error))
            return L("⚠ Connexion Internet indisponible. Vérifiez le réseau puis réessayez.","⚠ No Internet connection. Check the network and retry.","⚠ Keine Internetverbindung. Netzwerk prüfen und erneut versuchen.");
        return L("⚠ Service de mise à jour indisponible. Réessayez plus tard.","⚠ Update service unavailable. Try again later.","⚠ Update-Dienst nicht verfügbar. Später erneut versuchen.");
    }
    private bool IsNetworkException(Exception error) {
        for(Exception current=error;current!=null;current=current.InnerException)
            if(current is HttpRequestException||current is WebException||current is SocketException||current is TimeoutException)return true;
        return false;
    }
}
}
