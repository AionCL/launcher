using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AionCL {
public sealed partial class MainForm {
    readonly Color accent = Color.FromArgb(124, 181, 205);
    readonly Color muted = Color.FromArgb(153, 164, 181);
    Panel footer, installPanel, storyPanel;
    PictureBox artwork;
    Label storyTitle, storyBody, pathLabel, languageLabel, clientTitle, heading, edition;
    Button homeButton, helpButton, journalButton;
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
        ClientSize = new Size(1180, 720); MinimumSize = new Size(1120, 759);
        Font = new Font("Segoe UI", 10F); ForeColor = Color.FromArgb(231,235,241); BackColor = Color.FromArgb(16,19,26);
        using (var stream = typeof(MainForm).Assembly.GetManifestResourceStream("AionCL.classic-battle.jpg")) {
            if (stream != null) using (var source = Image.FromStream(stream)) portal = new Bitmap(source);
        }
        var brand = LabelAt(this,"AION",28,20,150,48,28,ForeColor); brand.Font = new Font("Georgia",28);
        edition = LabelAt(this,"CLASSIC  /  2.4",180,37,140,22,9,muted);
        homeButton = ButtonAt(this,"",370,26,120,42,false); homeButton.Click += delegate { storyPage="home"; ApplyLanguage(); };
        helpButton = ButtonAt(this,"",498,26,100,42,false); helpButton.Click += delegate { storyPage="help"; ApplyLanguage(); };
        journalButton = ButtonAt(this,"",606,26,110,42,false); journalButton.Click += delegate { ShowJournal(); };
        artwork = new PictureBox { Image = portal, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(10,12,17) }; Controls.Add(artwork);
        storyPanel = new Panel { BackColor = BackColor }; Controls.Add(storyPanel);
        heading = LabelAt(storyPanel,"AION CLASSIC",0,0,550,22,9,accent);
        storyTitle = LabelAt(storyPanel,"",0,29,580,36,20,ForeColor);
        storyBody = LabelAt(storyPanel,"",0,78,590,126,10,muted);
        installPanel = new Panel { BackColor = Color.FromArgb(23,28,38) }; Controls.Add(installPanel);
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
        footer=new Panel { BackColor=Color.FromArgb(12,15,21) }; Controls.Add(footer);
        progressBar.SetBounds(28,23,650,5); footer.Controls.Add(progressBar);
        statusLabel.SetBounds(28,44,650,46); statusLabel.ForeColor=muted; footer.Controls.Add(statusLabel);
        StyleButton(cancelButton,"",footer,500,85,178,28,false); cancelButton.Enabled=false; cancelButton.Click += delegate { if(cts!=null) cts.Cancel(); };
        StyleButton(playButton,"",footer,720,24,330,76,true); playButton.Font=new Font("Segoe UI",20,FontStyle.Bold); playButton.Enabled=false; playButton.Click += async delegate { await PlayAsync(); };
        logBox.Multiline=true; logBox.ReadOnly=true; logBox.ScrollBars=ScrollBars.Vertical; logBox.BackColor=BackColor; logBox.ForeColor=ForeColor; logBox.BorderStyle=BorderStyle.None;
        try { if(File.Exists(preferences)) pathBox.Text=File.ReadAllText(preferences); } catch(IOException) {} catch(UnauthorizedAccessException) {}
        pathBox.Leave += delegate { SaveClientPath(); RefreshClientState(); };
        statusLabel.TextChanged += delegate { LocalizeStatus(); };
        versionLabel.TextChanged += delegate { if(!changingLanguage) UpdateVersionText(); };
        Resize += delegate { LayoutLauncher(); };
        FormClosed += delegate { if(journal!=null) journal.Dispose(); if(portal!=null) portal.Dispose(); };
        BuildUpdateUi(); LayoutLauncher(); ApplyLanguage();
    }
    private void LayoutLauncher() {
        if(footer==null) return;
        int w=ClientSize.Width,h=ClientSize.Height;
        footer.SetBounds(0,h-128,w,128);
        installPanel.SetBounds(w-358,170,330,h-322);
        int imageWidth = Math.Min(512, w - 438);
        int imageHeight = portal == null ? imageWidth * 300 / 640 : imageWidth * portal.Height / portal.Width;
        artwork.SetBounds(28 + (w - 414 - imageWidth) / 2, 170, imageWidth, imageHeight);
        storyPanel.SetBounds(28,artwork.Bottom+24,w-414,footer.Top-artwork.Bottom-30);
        storyTitle.Width=storyPanel.Width; storyBody.Width=storyPanel.Width;
        playButton.SetBounds(w-358,24,330,76);
        progressBar.Width=w-414; statusLabel.Width=progressBar.Width;
        cancelButton.Left=progressBar.Right-cancelButton.Width;
        if(updatePanel!=null){updatePanel.SetBounds(28,90,w-56,60);checkUpdatesButton.Left=updatePanel.Width-390;launcherUpdateButton.Left=updatePanel.Width-212;updateNotice.Width=updatePanel.Width-420;}
    }
    private Label LabelAt(Control p,string t,int x,int y,int w,int h,float size,Color c) {
        var l=new Label { Text=t,Bounds=new Rectangle(x,y,w,h),ForeColor=c,BackColor=Color.Transparent,Font=new Font("Segoe UI",size) }; p.Controls.Add(l); return l;
    }
    private Button ButtonAt(Control p,string t,int x,int y,int w,int h,bool primary) { var b=new LauncherButton(); StyleButton(b,t,p,x,y,w,h,primary); return b; }
    private void StyleButton(Button b,string text,Control p,int x,int y,int w,int h,bool primary) {
        b.Text=text;b.SetBounds(x,y,w,h);b.FlatStyle=FlatStyle.Flat;b.FlatAppearance.BorderColor=Color.FromArgb(62,76,93);b.FlatAppearance.MouseOverBackColor=Color.FromArgb(46,62,78);
        b.BackColor=primary?Color.FromArgb(51,108,131):Color.FromArgb(29,37,49);b.ForeColor=Color.White;b.Cursor=Cursors.Hand;b.UseVisualStyleBackColor=false;p.Controls.Add(b);
    }
    private void ApplyLanguage() {
        changingLanguage=true;
        languageBox.SelectedIndex=selectedLanguage=="ENG"?1:selectedLanguage=="DEU"?2:0;
        homeButton.Text=L("Accueil","Home","Start"); helpButton.Text=L("Aide","Help","Hilfe"); journalButton.Text=L("Journal","Log","Protokoll");
        homeButton.BackColor=storyPage=="home"?Color.FromArgb(44,61,75):BackColor; helpButton.BackColor=storyPage=="help"?Color.FromArgb(44,61,75):BackColor;
        clientTitle.Text=L("Votre jeu","Your game","Dein Spiel");pathLabel.Text=L("DOSSIER DU CLIENT","GAME FOLDER","SPIELORDNER");languageLabel.Text=L("LANGUE DU JEU ET DU LAUNCHER","GAME & LAUNCHER LANGUAGE","SPIEL- UND LAUNCHERSPRACHE");
        installButton.Text=L("Installer / reprendre","Install / resume","Installieren / fortsetzen");verifyButton.Text=L("Vérifier / réparer","Verify / repair","Prüfen / reparieren");cancelButton.Text=L("Interrompre","Interrupt","Unterbrechen"); playButton.Text=L("▶   JOUER","▶   PLAY","▶   SPIELEN");
        browseButton.AccessibleName=L("Choisir le dossier client","Choose game folder","Spielordner auswählen");
        storyTitle.Text=storyPage=="home"?L("Votre aventure reprend ici.","Your adventure continues here.","Dein Abenteuer geht weiter."):L("Prêt à rejoindre Atréia ?","Ready to enter Atreia?","Bereit für Atreia?");
        storyBody.Text=storyPage=="home"?L("Retrouvez Atréia dans Aion Classic 2.4.\nChoisissez votre langue, puis entrez en jeu.","Return to Atreia in Aion Classic 2.4.\nChoose your language, then enter the game.","Kehre in Aion Classic 2.4 nach Atreia zurück.\nWähle deine Sprache und starte das Spiel."):L("Choisissez un dossier, puis installez le client.\nDéjà installé ? Vérifiez ses fichiers et cliquez sur JOUER.\nUn téléchargement interrompu peut être repris.","Choose a folder, then install the game.\nAlready installed? Verify its files and click PLAY.\nInterrupted downloads can be resumed.","Wähle einen Ordner und installiere das Spiel.\nSchon installiert? Prüfe die Dateien und klicke auf SPIELEN.\nUnterbrochene Downloads lassen sich fortsetzen.");
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
        if(journal==null||journal.IsDisposed) { journal=new Form { Text="AionCL · "+journalButton.Text,Size=new Size(850,450),StartPosition=FormStartPosition.CenterParent,BackColor=BackColor };logBox.Dock=DockStyle.Fill;journal.Controls.Add(logBox);journal.FormClosing += delegate(object sender,FormClosingEventArgs e) { if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;journal.Hide();} }; }
        journal.Show(this);journal.BringToFront();
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
public sealed class LauncherButton:Button {
    bool hovered;
    protected override void OnMouseEnter(EventArgs e){hovered=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hovered=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnPaint(PaintEventArgs e){
        using(var b=new SolidBrush(Enabled?(hovered?FlatAppearance.MouseOverBackColor:BackColor):Color.FromArgb(25,30,39)))e.Graphics.FillRectangle(b,ClientRectangle);
        using(var p=new Pen(Enabled?FlatAppearance.BorderColor:Color.FromArgb(44,51,64)))e.Graphics.DrawRectangle(p,0,0,Width-1,Height-1);
        TextRenderer.DrawText(e.Graphics,Text,Font,new Rectangle(5,0,Width-10,Height),Enabled?ForeColor:Color.FromArgb(124,137,155),TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
    }
}
}
