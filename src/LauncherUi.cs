using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AionCL {
public sealed partial class MainForm {
    readonly Color accent = Color.FromArgb(124, 181, 205);
    readonly Color muted = Color.FromArgb(153, 164, 181);
    Panel footer, installPanel, storyPanel;
    Panel shell, navigation, workspace, contentArea, journalPanel;
    PictureBox artwork;
    Label storyTitle, storyBody, pathLabel, languageLabel, clientTitle, heading, edition, brand;
    Label authSectionLabel, authUserLabel, authPasswordLabel;
    Button homeButton, helpButton, journalButton, discordButton, youtubeButton;
    ComboBox languageBox;
    Button koreanVoices;
    readonly KoreanPack koreanPack = KoreanPack.Load();
    readonly HitFont hitFont = HitFont.Load();
    Button hitFontButton;
    Form journal;
    Image portal;
    bool changingLanguage;
    string selectedLanguage = "FRA";
    string storyPage = "home";
    readonly string preferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AionCL", "launcher-path.txt");
    readonly string languagePreference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AionCL", "language.txt");

    private void BuildUi() {
        DoubleBuffered = true; AutoScaleMode = AutoScaleMode.None;
        var workArea = Screen.PrimaryScreen.WorkingArea;
        int initialWidth = Math.Min(1180, Math.Max(1000, workArea.Width - 40));
        int initialHeight = Math.Min(780, Math.Max(700, workArea.Height - 40));
        ClientSize = new Size(initialWidth, initialHeight); MinimumSize = new Size(1000, 700); MaximumSize = Size.Empty; AutoScroll = false;
        Font = new Font("Segoe UI", 10F); ForeColor = Color.FromArgb(231,235,241); BackColor = Color.FromArgb(12,16,23);
        using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("AionCL.classic-wings.jpg")) {
            if (stream != null) using (var source = Image.FromStream(stream)) portal = new Bitmap(source);
        }
        BackgroundImage = portal; BackgroundImageLayout = ImageLayout.Stretch;
        brand = LabelAt(this,"AIONCL",28,20,185,48,28,ForeColor); brand.Font = new Font("Georgia",28);
        edition = LabelAt(this,"CLASSIC  /  2.4",226,37,140,22,9,muted);
        homeButton = ButtonAt(this,"",370,26,120,42,false); homeButton.Click += delegate { storyPage="home"; ApplyLanguage(); };
        helpButton = ButtonAt(this,"",498,26,100,42,false); helpButton.Click += delegate { storyPage="settings"; ApplyLanguage(); };
        journalButton = ButtonAt(this,"",606,26,110,42,false); journalButton.Click += delegate { storyPage="journal"; ApplyLanguage(); };
        artwork = new PictureBox { Image = portal, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(10,12,17) }; Controls.Add(artwork);
        storyPanel = new Panel { BackColor = BackColor }; Controls.Add(storyPanel);
        heading = LabelAt(storyPanel,"AION CLASSIC",0,0,550,22,9,accent);
        storyTitle = LabelAt(storyPanel,"",0,29,580,36,20,ForeColor);
        storyBody = LabelAt(storyPanel,"",0,78,590,126,10,muted);
        installPanel = new SurfacePanel { BackColor = Color.FromArgb(20,27,37), AutoScroll = false }; Controls.Add(installPanel);
        clientTitle = LabelAt(installPanel,"",24,24,280,32,17,ForeColor);
        versionLabel.SetBounds(24,66,282,25); versionLabel.ForeColor=muted; installPanel.Controls.Add(versionLabel);
        pathLabel = LabelAt(installPanel,"",24,104,280,25,9,muted);
        pathBox.SetBounds(24,132,234,27); pathBox.BackColor=BackColor; pathBox.ForeColor=ForeColor; pathBox.BorderStyle=BorderStyle.FixedSingle; installPanel.Controls.Add(pathBox);
        StyleButton(browseButton,"…",installPanel,266,129,40,32,false); browseButton.Click += Browse;
        languageLabel = LabelAt(installPanel,"",24,171,282,24,9,muted);
        languageBox = new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList, FlatStyle=FlatStyle.Flat, BackColor=Color.FromArgb(35,43,56), ForeColor=ForeColor, Bounds=new Rectangle(24,199,282,30) };
        languageBox.Items.AddRange(new object[] { "Français", "English", "Deutsch" }); installPanel.Controls.Add(languageBox);
        try { if (File.Exists(languagePreference)) { var saved=File.ReadAllText(languagePreference).Trim(); if (saved=="ENG" || saved=="DEU") selectedLanguage=saved; } } catch (IOException) {} catch (UnauthorizedAccessException) {}
        languageBox.SelectedIndex=selectedLanguage=="ENG" ? 1 : selectedLanguage=="DEU" ? 2 : 0;
        languageBox.SelectedIndexChanged += delegate { if (changingLanguage) return; selectedLanguage=new[] { "FRA","ENG","DEU" }[languageBox.SelectedIndex]; SavePreference(languagePreference,selectedLanguage); ApplyLanguage(); };
        koreanVoices = ButtonAt(installPanel,"",24,239,282,30,false);
        koreanVoices.Click += async delegate { await ToggleKoreanPack(); };
        hitFontButton = ButtonAt(installPanel,"",24,274,282,30,false);
        hitFontButton.Click += delegate {
            if(!EnsurePath())return;
            try {
                hitFont.Change(pathBox.Text,selectedLanguage,!hitFont.Installed(pathBox.Text,selectedLanguage));
                RefreshKoreanButton();
                statusLabel.Text=L("Style des dégâts mis à jour. Relancez le jeu pour le voir.","Damage style updated. Restart the game to see it.","Schadensanzeige geändert. Spiel neu starten.");
            }catch(Exception ex){MessageBox.Show(this,ex.Message,"AionCL",MessageBoxButtons.OK,MessageBoxIcon.Error);}
        };
        StyleButton(installButton,"",installPanel,24,319,282,32,false); installButton.Enabled=false; installButton.Click += async delegate { await InstallAsync(); };
        StyleButton(verifyButton,"",installPanel,24,360,282,30,false); verifyButton.Enabled=false; verifyButton.Click += async delegate { await VerifyAsync(); };
        discordButton=ButtonAt(this,"Discord",370,26,100,40,false); discordButton.Click += delegate { OpenCommunity(config==null?null:config.discordUrl); };
        youtubeButton=ButtonAt(this,"YouTube",478,26,100,40,false); youtubeButton.Click += delegate { OpenCommunity(config==null?null:config.youtubeUrl); };
        footer=new Panel { BackColor=Color.FromArgb(12,15,21) }; Controls.Add(footer);
        authSectionLabel=LabelAt(footer,"Connexion au jeu",28,3,130,18,9,accent);
        authUserLabel=LabelAt(footer,"Identifiant",28,20,150,16,8,muted);
        authPasswordLabel=LabelAt(footer,"Mot de passe",206,20,150,16,8,muted);
        authUser.SetBounds(28,36,170,30); authUser.BackColor=BackColor; authUser.ForeColor=ForeColor; authUser.BorderStyle=BorderStyle.FixedSingle; footer.Controls.Add(authUser);
        authPassword.SetBounds(206,36,170,30); authPassword.UseSystemPasswordChar=true; authPassword.BackColor=BackColor; authPassword.ForeColor=ForeColor; authPassword.BorderStyle=BorderStyle.FixedSingle; footer.Controls.Add(authPassword);
        authRemember.SetBounds(386,38,130,24); authRemember.Text="Mémoriser"; authRemember.ForeColor=muted; authRemember.BackColor=Color.Transparent; footer.Controls.Add(authRemember);
        StyleButton(authManualButton,"",footer,520,34,158,34,true); authManualButton.Click += async delegate { await ManualAuthenticateAsync(); };
        authStatus.SetBounds(28,70,650,24); authStatus.ForeColor=muted; footer.Controls.Add(authStatus);
        progressBar.SetBounds(28,98,650,5); footer.Controls.Add(progressBar);
        statusLabel.SetBounds(28,104,430,18); statusLabel.ForeColor=muted; footer.Controls.Add(statusLabel);
        StyleButton(cancelButton,"",footer,500,85,178,28,false); cancelButton.Enabled=false; cancelButton.Visible=false; cancelButton.Click += delegate { if(cts!=null) cts.Cancel(); };
        StyleButton(playButton,"",footer,720,24,330,76,true); playButton.Font=new Font("Segoe UI",20,FontStyle.Bold); playButton.Enabled=false; playButton.Click += async delegate { await PlayAsync(); };
        logBox.Multiline=true; logBox.ReadOnly=true; logBox.ScrollBars=ScrollBars.Vertical; logBox.BackColor=BackColor; logBox.ForeColor=ForeColor; logBox.BorderStyle=BorderStyle.None;
        journalPanel = new SurfacePanel { BackColor=Color.FromArgb(14,21,30), Visible=false, Padding=new Padding(18) };
        journalPanel.Controls.Add(logBox); logBox.Dock=DockStyle.Fill;
        try { if(File.Exists(preferences)) pathBox.Text=File.ReadAllText(preferences); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        pathBox.Leave += delegate { SaveClientPath(); RefreshClientState(); };
        statusLabel.TextChanged += delegate { LocalizeStatus(); };
        versionLabel.TextChanged += delegate { if(!changingLanguage) UpdateVersionText(); };
        Resize += delegate { LayoutLauncher(); };
        FormClosed += delegate { if(journal!=null) journal.Dispose(); if(portal!=null) portal.Dispose(); };
        BuildShell();
        BuildUpdateUi(); LayoutLauncher(); ApplyLanguage();
    }
    private void BuildShell() {
        shell = new Panel { BackColor=Color.FromArgb(11,16,24), BackgroundImage=portal, BackgroundImageLayout=ImageLayout.Stretch, Dock=DockStyle.Fill };
        Controls.Add(shell);
        navigation = new Panel { BackColor=Color.FromArgb(13,20,30) };
        workspace = new Panel { BackColor=Color.FromArgb(10,15,23) };
        contentArea = new Panel { BackColor=Color.Transparent };
        shell.Controls.Add(workspace); shell.Controls.Add(navigation);
        navigation.Controls.Add(brand); navigation.Controls.Add(edition);
        navigation.Controls.Add(homeButton); navigation.Controls.Add(helpButton); navigation.Controls.Add(journalButton);
        navigation.Controls.Add(discordButton); navigation.Controls.Add(youtubeButton);
        workspace.Controls.Add(contentArea);
        contentArea.Controls.Add(artwork); contentArea.Controls.Add(storyPanel); contentArea.Controls.Add(installPanel); contentArea.Controls.Add(journalPanel);
        workspace.Controls.Add(footer);
        brand.Font=new Font("Georgia",25,FontStyle.Bold); brand.ForeColor=Color.White;
        edition.ForeColor=accent;
        navigation.BringToFront(); workspace.BringToFront(); navigation.BringToFront();
    }
    private void LayoutLauncher() {
        if(shell==null||footer==null) return;
        int w=Math.Max(980,ClientSize.Width), h=Math.Max(640,ClientSize.Height);
        const int navWidth=190, outer=22, gap=18, footerHeight=142;
        shell.SetBounds(0,0,ClientSize.Width,ClientSize.Height);
        navigation.SetBounds(0,0,navWidth,ClientSize.Height);
        workspace.SetBounds(navWidth,0,Math.Max(1,ClientSize.Width-navWidth),ClientSize.Height);
        brand.SetBounds(20,22,navWidth-40,42); edition.SetBounds(22,68,navWidth-44,20);
        homeButton.SetBounds(16,120,navWidth-32,42); helpButton.SetBounds(16,170,navWidth-32,42); journalButton.SetBounds(16,220,navWidth-32,42);
        discordButton.SetBounds(16,ClientSize.Height-104,(navWidth-42)/2,34); youtubeButton.SetBounds(22+(navWidth-42)/2,ClientSize.Height-104,(navWidth-42)/2,34);
        bool compact=ClientSize.Width<1080;
        int workW=workspace.Width, contentTop=updatePanel==null?18:94;
        footer.SetBounds(0,ClientSize.Height-footerHeight,workW,footerHeight);
        contentArea.SetBounds(outer,contentTop,Math.Max(1,workW-outer*2),Math.Max(1,footer.Top-contentTop-14));
        int contentW=contentArea.Width, contentH=contentArea.Height;
        if(storyPage=="journal") {
            artwork.Visible=false; storyPanel.Visible=false; installPanel.Visible=false; journalPanel.Visible=true;
            journalPanel.SetBounds(0,0,contentW,contentH); logBox.Dock=DockStyle.Fill;
        } else if(storyPage=="settings") {
            artwork.Visible=false; storyPanel.Visible=false; installPanel.Visible=true; installPanel.SetBounds(0,0,contentW,contentH);
            journalPanel.Visible=false;
            LayoutSettingsPanel(contentW,contentH);
        } else {
            artwork.Visible=true; storyPanel.Visible=true; installPanel.Visible=false; journalPanel.Visible=false;
            int left=contentW;
            artwork.SetBounds(0,0,left,Math.Min(contentH,Math.Max(220,left*9/16)));
            storyPanel.SetBounds(20,Math.Min(contentH-120,artwork.Bottom+12),left-40,Math.Max(100,contentH-artwork.Bottom-24));
            storyTitle.SetBounds(0,12,storyPanel.Width,38); storyBody.SetBounds(0,55,storyPanel.Width,Math.Max(50,storyPanel.Height-55));
        }
        LayoutFooter(workW,ClientSize.Height);
        AutoScrollMinSize=Size.Empty;
        if(updatePanel!=null) {
            updatePanel.SetBounds(outer,18,Math.Max(1,workW-outer*2),64);
            int downloadWidth=Math.Max(190,TextRenderer.MeasureText(launcherUpdateButton.Text,launcherUpdateButton.Font).Width+30);
            int checkWidth=Math.Max(210,TextRenderer.MeasureText(checkUpdatesButton.Text,checkUpdatesButton.Font).Width+30);
            bool showDownload=releases!=null&&Updates.Newer(releases.launcher.version,Updates.LauncherVersion);
            launcherUpdateButton.SetBounds(updatePanel.Width-downloadWidth-10,12,downloadWidth,40);
            launcherUpdateButton.Visible=showDownload;
            checkUpdatesButton.SetBounds(updatePanel.Width-checkWidth-(showDownload?downloadWidth+20:10),12,checkWidth,40);
            updateNotice.SetBounds(16,10,Math.Max(180,checkUpdatesButton.Left-24),42); updateNotice.TextAlign=ContentAlignment.MiddleLeft;
        }
    }
    private void LayoutInstallPanel(int width,int height) {
        int inner=Math.Max(220,width-44), y=18;
        clientTitle.SetBounds(22,y,inner,32); y+=40; versionLabel.SetBounds(22,y,inner,24); y+=34;
        pathLabel.SetBounds(22,y,inner,20); y+=24; pathBox.SetBounds(22,y,inner-52,30); browseButton.SetBounds(width-66,y-3,42,36); y+=48;
        languageLabel.SetBounds(22,y,inner,20); y+=24; languageBox.SetBounds(22,y,inner,32); y+=44;
        koreanVoices.SetBounds(22,y,inner,34); y+=42; hitFontButton.SetBounds(22,y,inner,34); y+=42;
        installButton.SetBounds(22,y,inner,38); y+=46; verifyButton.SetBounds(22,y,inner,38);
    }
    private void LayoutSettingsPanel(int width,int height) { LayoutInstallPanel(width,height); }
    private void LayoutFooter(int w, int h) {
        if(w<930) {
            int playX=Math.Max(430,w-300), playW=Math.Max(250,w-playX-28);
            authSectionLabel.SetBounds(28,8,190,18);
            authUserLabel.SetBounds(28,27,135,16); authUser.SetBounds(28,43,135,28);
            authPasswordLabel.SetBounds(173,27,135,16); authPassword.SetBounds(173,43,135,28);
            authRemember.SetBounds(318,46,112,24);
            authManualButton.SetBounds(28,78,190,32);
            authStatus.SetBounds(228,80,Math.Max(180,playX-246),20);
            statusLabel.SetBounds(28,111,playX-46,18); progressBar.SetBounds(28,132,Math.Max(180,playX-46),5);
            playButton.SetBounds(playX,24,playW,68); cancelButton.SetBounds(playX,99,playW,30);
            return;
        }
        LayoutAuthRow(w-396, 28);
        playButton.SetBounds(w-380,22,330,78); cancelButton.SetBounds(w-380,108,330,30); progressBar.SetBounds(28,136,w-458,6); statusLabel.SetBounds(28,105,w-458,24);
    }
    private void LayoutAuthRow(int rightEdge, int left) {
        int available = Math.Max(520, rightEdge-left);
        int userWidth, rememberWidth, manualWidth;
        if (available >= 710) { userWidth=170; rememberWidth=150; manualWidth=170; }
        else if (available >= 640) { userWidth=150; rememberWidth=120; manualWidth=155; }
        else { userWidth=130; rememberWidth=100; manualWidth=145; }
        int gap=10, x=left;
        authSectionLabel.SetBounds(left,8,180,18);
        authUserLabel.SetBounds(x,27,userWidth,16); authUser.SetBounds(x,43,userWidth,30); x+=userWidth+gap;
        authPasswordLabel.SetBounds(x,27,userWidth,16); authPassword.SetBounds(x,43,userWidth,30); x+=userWidth+gap;
        authRemember.SetBounds(x,46,rememberWidth,24); x+=rememberWidth+gap;
        authManualButton.SetBounds(x,41,manualWidth,34);
        authStatus.SetBounds(left,80,Math.Max(300,available),20);
    }
    private Label LabelAt(Control p,string t,int x,int y,int w,int h,float size,Color c) {
        var l=new Label { Text=t,Bounds=new Rectangle(x,y,w,h),ForeColor=c,BackColor=Color.Transparent,Font=new Font("Segoe UI",size) }; p.Controls.Add(l); return l;
    }
    private void OpenCommunity(string url) { if(String.IsNullOrWhiteSpace(url)) return; try { Process.Start(new ProcessStartInfo { FileName=Safety.Https(url).AbsoluteUri, UseShellExecute=true }); } catch(Exception ex) { Log(ex.Message); } }
    private Button ButtonAt(Control p,string t,int x,int y,int w,int h,bool primary) { var b=new LauncherButton(); StyleButton(b,t,p,x,y,w,h,primary); return b; }
    private void StyleButton(Button b,string text,Control p,int x,int y,int w,int h,bool primary) {
        b.Font=new Font("Segoe UI",10F);b.Text=text;b.SetBounds(x,y,w,h);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(62,76,93);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(46,62,78);
        b.BackColor=primary?Color.FromArgb(37,125,147):Color.FromArgb(27,37,49);b.ForeColor=Color.White;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;p.Controls.Add(b);
    }
    private void ApplyLanguage() {
        changingLanguage=true;
        languageBox.SelectedIndex=selectedLanguage=="ENG"?1:selectedLanguage=="DEU"?2:0;
        homeButton.Text=L("Accueil","Home","Start"); helpButton.Text=L("Paramètres","Settings","Einstellungen"); journalButton.Text=L("Journal","Log","Protokoll");
        discordButton.Text="Discord"; youtubeButton.Text="YouTube";
        authSectionLabel.Text=L("Connexion au jeu","Game login","Spielanmeldung"); authUserLabel.Text=L("Identifiant","Username","Benutzername"); authPasswordLabel.Text=L("Mot de passe","Password","Passwort"); authRemember.Text=L("Mémoriser dans Windows","Remember in Windows","In Windows speichern");
        authManualButton.Text=L("Utiliser ces identifiants","Use these credentials","Diese Zugangsdaten verwenden");
        homeButton.BackColor=storyPage=="home"?Color.FromArgb(44,61,75):BackColor; helpButton.BackColor=storyPage=="settings"?Color.FromArgb(44,61,75):BackColor; journalButton.BackColor=storyPage=="journal"?Color.FromArgb(44,61,75):BackColor;
        clientTitle.Text=L("Votre jeu","Your game","Dein Spiel");pathLabel.Text=L("DOSSIER DU CLIENT","GAME FOLDER","SPIELORDNER");languageLabel.Text=L("LANGUE DU JEU ET DU LAUNCHER","GAME & LAUNCHER LANGUAGE","SPIEL- UND LAUNCHERSPRACHE");
        installButton.Text=L("Installer / reprendre","Install / resume","Installieren / fortsetzen");verifyButton.Text=L("Vérifier / réparer","Verify / repair","Prüfen / reparieren");cancelButton.Text=L("Interrompre","Interrupt","Unterbrechen"); playButton.Text=L("▶   JOUER","▶   PLAY","▶   SPIELEN");
        browseButton.AccessibleName=L("Choisir le dossier client","Choose game folder","Spielordner auswählen");
        storyTitle.Text=storyPage=="home"?L("Votre aventure reprend ici.","Your adventure continues here.","Dein Abenteuer geht weiter."):storyPage=="journal"?L("Journal du launcher","Launcher log","Launcher-Protokoll"):L("Paramètres du launcher","Launcher settings","Launcher-Einstellungen");
        storyBody.Text=storyPage=="home"?L("Retrouvez Atréia dans Aion Classic 2.4.\nChoisissez votre langue, puis entrez en jeu.","Return to Atreia in Aion Classic 2.4.\nChoose your language, then enter the game.","Kehre in Aion Classic 2.4 nach Atreia zurück.\nWähle deine Sprache und starte das Spiel."):storyPage=="journal"?L("Les opérations et diagnostics apparaissent ici.","Operations and diagnostics appear here.","Vorgänge und Diagnosen erscheinen hier."):L("Dossier du client, langue et outils facultatifs.\nLes modifications sont appliquées avant le prochain lancement.","Game folder, language and optional tools.\nChanges are applied before the next launch.","Spielordner, Sprache et optionale Werkzeuge.\nÄnderungen werden vor dem nächsten Start angewendet.");
        UpdateVersionText(); changingLanguage=false;
        if(manifest!=null) RefreshClientState(); else statusLabel.Text=L("Connexion au service de mise à jour…","Connecting to update service…","Verbindung zum Update-Dienst…");
        RefreshKoreanButton();
        RefreshUpdateUi();
        if(journal!=null) journal.Text="AionCL · "+journalButton.Text;
    }
    private string L(string fr,string en,string de) { return selectedLanguage=="ENG"?en:selectedLanguage=="DEU"?de:fr; }
    private void RefreshKoreanButton() {
        if(koreanVoices==null || busy)return;
        bool present=false;
        try { if(!String.IsNullOrWhiteSpace(pathBox.Text))present=koreanPack.HasPack(pathBox.Text,selectedLanguage); }
        catch(IOException){koreanVoices.Enabled=false;return;} catch(ArgumentException){koreanVoices.Enabled=false;return;}
        koreanVoices.Text=present?L("Voix coréennes : retirer","Korean voices: remove","Koreanische Stimmen: entfernen"):L("Installer les voix coréennes","Install Korean voices","Koreanische Stimmen installieren");
        koreanVoices.Enabled=manifest!=null && !String.IsNullOrWhiteSpace(pathBox.Text);
        if(hitFontButton!=null) {
            bool installed=!String.IsNullOrWhiteSpace(pathBox.Text)&&hitFont.Installed(pathBox.Text,selectedLanguage);
            hitFontButton.Text=installed?L("Japan hit font : retirer","Japan hit font: remove","Japan Hit Font: entfernen"):L("Installer Japan hit font","Install Japan hit font","Japan Hit Font installieren");
            hitFontButton.Enabled=koreanVoices.Enabled;
        }
    }
    private async System.Threading.Tasks.Task ToggleKoreanPack() {
        if(!EnsurePath())return;
        string root=pathBox.Text,language=selectedLanguage;
        bool reporting=true;
        string outcome=null;
        try {
            VoiceMode.RequireGameClosed();
            bool install=!koreanPack.HasPack(root,language);
            string source=null;
            if(install && !koreanPack.HasCache(root)) {
                source=koreanPack.PreviousSource(root,language);
                if(source==null)using(var dialog=new FolderBrowserDialog { Description=L("Choisir le dossier extrait du pack coréen (voice, npc, system…)","Select extracted Korean pack folder (voice, npc, system…)","Entpackten koreanischen Stimmenordner wählen (voice, npc, system…)") }) {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return;source=dialog.SelectedPath;
                }
            }
            SetBusy(true);cts=new System.Threading.CancellationTokenSource();
            progressBar.Value=0;progressBar.Style=ProgressBarStyle.Marquee;
            statusLabel.Text=L("Contrôle du pack coréen…","Checking Korean voice pack…","Koreanisches Stimmenpaket wird geprüft…");
            var progress=new Progress<VerificationProgress>(delegate(VerificationProgress p) {
                if(!reporting||IsDisposed)return;
                progressBar.Style=ProgressBarStyle.Continuous;progressBar.Value=(int)(100L*p.Completed/p.Total);
                statusLabel.Text=(install?L("Installation des voix","Installing voices","Stimmen installieren"):L("Retrait des voix","Removing voices","Stimmen entfernen"))+" : "+p.Completed+" / "+p.Total;
            });
            await koreanPack.Change(root,language,install,source,progress,cts.Token);
            progressBar.Value=100;
            outcome=install?L("Voix coréennes installées. Textes conservés.","Korean voices installed. Text unchanged.","Koreanische Stimmen installiert. Texte unverändert."):L("Pack coréen retiré. Voix d’origine restaurées.","Korean pack removed. Original voices restored.","Koreanisches Paket entfernt. Originalstimmen wiederhergestellt.");
        } catch(OperationCanceledException) { outcome=L("Opération interrompue. Le bouton permet de retirer les fichiers ajoutés.","Operation interrupted. Use the button to remove added files.","Vorgang unterbrochen. Hinzugefügte Dateien über die Schaltfläche entfernen."); }
        catch(Exception ex) { outcome=TranslateMessage(ex.Message);MessageBox.Show(this,outcome,"AionCL",MessageBoxButtons.OK,MessageBoxIcon.Error); }
        finally { reporting=false;progressBar.Style=ProgressBarStyle.Continuous;SetBusy(false);if(outcome!=null){statusLabel.Text=outcome;Log(outcome);} }
    }
    private void UpdateVersionText() {
        bool prior=changingLanguage;changingLanguage=true;
        versionLabel.Text=manifest==null?L("Version : chargement…","Version: loading…","Version: wird geladen…"):L("Version disponible : ","Available version: ","Verfügbare Version: ")+manifest.Manifest.clientVersion;
        changingLanguage=prior;
    }
    private void LocalizeStatus() { if(changingLanguage)return; changingLanguage=true; statusLabel.Text=TranslateMessage(statusLabel.Text);changingLanguage=false; }
    private void ShowJournal() {
        storyPage="journal";
        ApplyLanguage();
    }
    private void SaveClientPath() { if(!String.IsNullOrWhiteSpace(pathBox.Text)) SavePreference(preferences,pathBox.Text); }
    private void SavePreference(string path,string value) {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)); if(!File.Exists(path)||File.ReadAllText(path)!=value){ if(File.Exists(path))File.Copy(path,path+".bak-"+Guid.NewGuid().ToString("N")); File.WriteAllText(path,value); } }
        catch(IOException ex){ Log(ex.Message); } catch(UnauthorizedAccessException ex){ Log(ex.Message); }
    }
    private string ClientStateText(ClientState state) {
        switch(state) {
            case ClientState.Valid:return L("Votre client est prêt. Bon retour sur Atréia !","Your game is ready. Welcome back to Atreia!","Dein Spiel ist bereit. Willkommen zurück in Atreia!");
            case ClientState.Absent:return L("Choisissez INSTALLER pour télécharger le jeu.","Choose INSTALL to download the game.","Wähle INSTALLIEREN, um das Spiel herunterzuladen.");
            case ClientState.Potential:return L("Client détecté. Vérifiez ses fichiers pour jouer.","Game detected. Verify its files to play.","Spiel erkannt. Prüfe die Dateien, um zu spielen.");
            default:return L("Installation incomplète. Reprenez ou réparez le client.","Incomplete installation. Resume or repair the game.","Installation unvollständig. Fortsetzen oder reparieren.");
        }
    }
}
public sealed class SurfacePanel:Panel {
    public SurfacePanel(){DoubleBuffered=true;}
    internal static System.Drawing.Drawing2D.GraphicsPath Outline(Rectangle r,int radius) {
        var path=new System.Drawing.Drawing2D.GraphicsPath();int d=radius*2;
        path.AddArc(r.Left,r.Top,d,d,180,90);path.AddArc(r.Right-d,r.Top,d,d,270,90);
        path.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);path.AddArc(r.Left,r.Bottom-d,d,d,90,90);path.CloseFigure();return path;
    }
    protected override void OnPaintBackground(PaintEventArgs e) {
        e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using(var shape=Outline(new Rectangle(0,0,Width-1,Height-1),12))
        using(var fill=new SolidBrush(BackColor))
        using(var edge=new Pen(Color.FromArgb(38,49,63))) { e.Graphics.FillPath(fill,shape);e.Graphics.DrawPath(edge,shape); }
    }
}
public sealed class LauncherButton:Button {
    bool hovered;
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){
        e.Graphics.Clear(Parent==null?BackColor:Parent.BackColor);
        e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using(var shape=SurfacePanel.Outline(new Rectangle(1,1,Width-3,Height-3),7)) {
            using(var b=new SolidBrush(Enabled?(hovered?FlatAppearance.MouseOverBackColor:BackColor):Color.FromArgb(25,30,39)))e.Graphics.FillPath(b,shape);
            using(var p=new Pen(Enabled?FlatAppearance.BorderColor:Color.FromArgb(44,51,64)))e.Graphics.DrawPath(p,shape);
        }
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(5,0,Width-10,Height),Enabled?ForeColor:Color.FromArgb(124,137,155),TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
    }
}
}
