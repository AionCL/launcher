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
    Label updateNotice;
    Button checkUpdatesButton,launcherUpdateButton;
    readonly System.Windows.Forms.Timer updateTimer=new System.Windows.Forms.Timer { Interval=300000 };
    bool checkingUpdates;
    UpdateFeed releases;
    string updateError;
    private void BuildUpdateUi() {
        updatePanel=new Panel { BackColor=Color.FromArgb(23,39,51) };Controls.Add(updatePanel);
        updateNotice=new Label { Bounds=new Rectangle(16,12,610,42),ForeColor=Color.FromArgb(176,218,233),Text="AionCL · Updates",Font=new Font("Segoe UI",9) };updatePanel.Controls.Add(updateNotice);
        checkUpdatesButton=ButtonAt(updatePanel,"",650,12,165,34,false);checkUpdatesButton.Click += async delegate { await CheckUpdates(false); };
        launcherUpdateButton=ButtonAt(updatePanel,"",825,12,200,34,true);launcherUpdateButton.Visible=false;
        launcherUpdateButton.Click += delegate { if(releases!=null)Process.Start(new ProcessStartInfo { FileName=Safety.Https(releases.launcher.downloadPage).AbsoluteUri,UseShellExecute=true }); };
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
        launcherUpdateButton.Visible=launcherAvailable;
        string available=launcherAvailable?L("Nouveau launcher : ","New launcher: ","Neuer Launcher: ")+releases.launcher.version+". ":"";
        if(ClientUpdateAvailable()){available+=L("Mise à jour du jeu : ","Game update: ","Spielupdate: ")+manifest.Manifest.clientVersion;installButton.Text=L("METTRE À JOUR LE JEU","UPDATE GAME","SPIEL AKTUALISIEREN");}
        else if(!busy)installButton.Text=L("Installer / reprendre","Install / resume","Installieren / fortsetzen");
        updateNotice.Text=checkingUpdates?L("Recherche des mises à jour…","Checking for updates…","Suche nach Updates…"):available.Length>0?available:updateError!=null?L("Recherche indisponible. Réessayez avec le bouton.","Update check unavailable. Retry with the button.","Updatesuche nicht verfügbar. Erneut versuchen."):releases==null?L("Mises à jour du jeu et du launcher","Game and launcher updates","Spiel- und Launcherupdates"):L("Vous êtes à jour","You are up to date","Du bist auf dem neuesten Stand")+" · Launcher "+Updates.LauncherVersion;
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
