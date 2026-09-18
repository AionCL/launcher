using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        launcherUpdateButton.Click += delegate { if(releases!=null)Process.Start(new ProcessStartInfo { FileName=Safety.Https(releases.launcher.downloadPage).AbsoluteUri,UseShellExecute=true }); };
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
        launcherUpdateButton.Text=L("Télécharger le launcher","Download launcher","Launcher herunterladen");
        checkUpdatesButton.Enabled=!busy&&!checkingUpdates&&config!=null;
        bool launcherAvailable=releases!=null&&Updates.Newer(releases.launcher.version,Updates.LauncherVersion);
        // The home page must keep one clear primary action. Launcher packages
        // are handled separately; do not display a second download button here.
        launcherUpdateButton.Visible=false;
        string available=launcherAvailable?L("Nouveau launcher disponible dans le journal. ","A new launcher is available in the log. ","Ein neuer Launcher ist im Journal verfügbar. "):"";
        if(ClientUpdateAvailable()){available+=L("Mise à jour du jeu : ","Game update: ","Spielupdate: ")+manifest.Manifest.clientVersion;installButton.Text=L("METTRE À JOUR LE JEU","UPDATE GAME","SPIEL AKTUALISIEREN");}
        else if(!busy)installButton.Text=L("Installer / reprendre","Install / resume","Installieren / fortsetzen");
        updateNotice.Text=checkingUpdates?L("Recherche des mises à jour…","Checking for updates…","Suche nach Updates…"):available.Length>0?available:updateError!=null?L("Recherche indisponible. Réessayez avec le bouton.","Update check unavailable. Retry with the button.","Updatesuche nicht verfügbar. Erneut versuchen."):releases==null?L("Mises à jour du jeu et du launcher","Game and launcher updates","Spiel- und Launcherupdates"):L("Vous êtes à jour","You are up to date","Du bist auf dem neuesten Stand")+" · Launcher "+Updates.LauncherVersion;
        RefreshNoticeUi();
        LayoutLauncher();
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
        }catch(Exception ex){updateError=ex.Message;Log(L("Recherche des mises à jour : ","Update check: ","Updatesuche: ")+ex.Message);}
        finally {checkingUpdates=false;if(!IsDisposed){if(!startup)SetBusy(false);UpdateVersionText();RefreshUpdateUi();}}
    }
}
}
