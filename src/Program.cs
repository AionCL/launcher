using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AionCL
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try
            {
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.ToString(),
                    "AionCL Launcher - erreur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }

    public sealed partial class MainForm : Form
    {
        private LauncherConfig config;
        private ManifestResult manifest;
        private CancellationTokenSource cts;
        private bool busy;

        private readonly TextBox pathBox = new TextBox();
        private readonly Button browseButton = new LauncherButton();
        private readonly Button installButton = new LauncherButton();
        private readonly Button verifyButton = new LauncherButton();
        private readonly Button diagnosticsButton = new LauncherButton();
        private readonly Button forgetCredentialsButton = new LauncherButton();
        private readonly Button playButton = new LauncherButton();
        private readonly Button cancelButton = new LauncherButton();
        private readonly TextBox authUser = new TextBox();
        private readonly TextBox authPassword = new TextBox();
        private readonly ComboBox accountBox = new ComboBox();
        private readonly CheckBox authRemember = new CheckBox();
        private readonly Button authManualButton = new LauncherButton();
        private readonly Label authStatus = new Label();
        private readonly LauncherAuth launcherAuth = new LauncherAuth();
        private bool loadingAccounts;

        private readonly Label statusLabel = new Label();
        private readonly Label diagnosticsStatus = new Label();
        private readonly Label versionLabel = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly TextBox logBox = new TextBox();

        public MainForm() : this(false) {}

        public MainForm(bool preview)
        {
            Text = "AionCL - Classic 2.4";
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
            ClientSize = new Size(1100, 700);
            MinimumSize = ClientSize;
            MaximumSize = ClientSize;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();

            if (!preview) Shown += async delegate
            {
                await InitializeAsync();
            };

            FormClosing += delegate
            {
                if (cts != null)
                    cts.Cancel();
            };
        }

        private async Task InitializeAsync()
        {
            try
            {
                string configPath = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "launcher.json"
                );

                if (!File.Exists(configPath))
                    throw new FileNotFoundException(
                        "launcher.json introuvable.",
                        configPath
                    );

                config = Json.Parse<LauncherConfig>(
                    File.ReadAllText(configPath)
                );

                if (config == null)
                    throw new InvalidDataException(
                        "Configuration launcher vide."
                    );

                config.Validate();
                Text = "AionCL - Classic 2.4 · Launcher " + config.launcherVersion;
                RefreshAccountBox(null);
                authStatus.Text = launcherAuth.Accounts.Count == 0 ? "Saisis tes identifiants pour lancer le client." : "Identifiants mémorisés disponibles.";
                await CheckUpdates(true);

                Log("Chargement du manifest distant...");

                using (var network =
                    new Network(config.requestTimeoutSeconds))
                {
                    cts = new CancellationTokenSource();

                    if (manifest == null) {
                        try {
                            manifest = await network.ManifestAsync(config, cts.Token);
                        }
                        catch (Exception ex) {
                            // A stale launcher may still carry an obsolete manifest
                            // URL while the update feed is temporarily unavailable.
                            // Keep the UI usable and let the explicit retry refresh it;
                            // do not show a blocking 404 dialog during startup.
                            Log("Manifest indisponible au démarrage : " + ex.Message);
                            statusLabel.Text = L("Mises à jour indisponibles. Cliquez sur Rechercher les mises à jour.", "Updates unavailable. Click Check for updates to retry.", "Updates nicht verfügbar. Klicke auf Updates suchen, um es erneut zu versuchen.");
                            RefreshUpdateUi();
                            updateTimer.Start();
                            return;
                        }
                    }
                }

                Log(
                    "Manifest OK : " +
                    manifest.Manifest.packages.Length +
                    " packages, " +
                    manifest.Manifest.sourceFileCount +
                    " fichiers."
                );

                versionLabel.Text =
                    "Version disponible : " +
                    manifest.Manifest.clientVersion;

                statusLabel.Text =
                    "Manifest AionCL chargé.";

                installButton.Enabled = true;
                verifyButton.Enabled = true;

                RefreshClientState();
                updateTimer.Start();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erreur d'initialisation";
                Log(ex.Message);
                MessageBox.Show(
                    TranslateMessage(ex.Message),
                    "AionCL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private async Task ManualAuthenticateAsync() {
            if (String.IsNullOrWhiteSpace(authUser.Text) || String.IsNullOrWhiteSpace(authPassword.Text)) {
                authStatus.Text = TranslateMessage("Identifiant et mot de passe requis.");
                return;
            }
            var validation = ValidateNativeCredentials(authUser.Text.Trim(), authPassword.Text);
            if (validation != null) { authStatus.Text = TranslateMessage(validation); return; }
            launcherAuth.SaveCredentials(authUser.Text.Trim(), authPassword.Text, authRemember.Checked);
            RefreshAccountBox(authUser.Text.Trim());
            authStatus.Text = TranslateMessage(authRemember.Checked ? "Identifiants enregistrés pour le client." : "Identifiants prêts pour le client.");
            await Task.Yield();
        }

        private void Browse(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description =
                    TranslateMessage("Choisir le dossier d'installation AionCL");

                if (dialog.ShowDialog(this) ==
                    DialogResult.OK)
                {
                    pathBox.Text = dialog.SelectedPath;
                    SaveClientPath();
                    RefreshClientState();
                }
            }
        }

        private void RefreshClientState()
        {
            if (busy) return;
            RefreshKoreanButton();
            RefreshUpdateUi();
            if (manifest == null ||
                String.IsNullOrWhiteSpace(pathBox.Text))
            {
                playButton.Enabled = manifest != null;
                ApplyPlayActionStyle(manifest==null?ClientState.Absent:ClientState.Absent);
                return;
            }

            try
            {
                ClientState state =
                    Installation.Detect(
                        pathBox.Text,
                        manifest
                    );

                statusLabel.Text = ClientStateText(state);

                playButton.Enabled = true;
                ApplyPlayActionStyle(state);
            }
            catch (Exception ex)
            {
                playButton.Enabled = manifest != null;
                ApplyPlayActionStyle(ClientState.Incomplete);
                statusLabel.Text =
                    "État du client : erreur";
                Log(ex.Message);
            }
        }

        private void ApplyPlayActionStyle(ClientState state) {
            if (state == ClientState.Valid) {
                playButton.Text=L("▶   JOUER","▶   PLAY","▶   SPIELEN");
                ApplyActionStyle(playButton,Color.FromArgb(25,135,190),Color.FromArgb(41,163,215),20F);
            } else if (state == ClientState.Absent) {
                playButton.Text=L("⬇   TÉLÉCHARGER LE JEU","⬇   DOWNLOAD GAME","⬇   SPIEL HERUNTERLADEN");
                ApplyActionStyle(playButton,Color.FromArgb(31,145,123),Color.FromArgb(43,170,145),15F);
            } else {
                playButton.Text=L("⚙   INSTALLER / RÉPARER","⚙   INSTALL / REPAIR","⚙   INSTALLIEREN / REPARIEREN");
                ApplyActionStyle(playButton,Color.FromArgb(123,83,166),Color.FromArgb(148,106,193),15F);
            }
        }

        private async Task InstallAsync()
        {
            if (!EnsurePath())
                return;

            SetBusy(true);

            cts = new CancellationTokenSource();

            try
            {
                var progress =
                    new Progress<TransferProgress>(
                        delegate(TransferProgress p)
                        {
                            int percent = p.Total > 0 ? (int)Math.Min(100, p.Bytes * 100L / p.Total) : 0;
                            if (p.Total > 0)
                            {
                                progressBar.Value =
                                    Math.Max(
                                        0,
                                        Math.Min(
                                            100,
                                            percent
                                        )
                                    );
                            }

                            string eta = p.BytesPerSecond > 0 && p.Total > p.Bytes ? " · reste " + FormatDuration((p.Total-p.Bytes) / p.BytesPerSecond) : "";
                            statusLabel.Text =
                                L("Téléchargement ","Downloading ","Download ") + percent + "% · " + p.Package +
                                " - " +
                                FormatBytes(p.Bytes) +
                                " / " +
                                FormatBytes(p.Total) +
                                " - " +
                                FormatBytes(
                                    (long)p.BytesPerSecond
                                ) +
                                "/s" + eta;
                        });
                var extractionProgress = new Progress<ExtractionProgress>(delegate(ExtractionProgress p) {
                    progressBar.Style = ProgressBarStyle.Continuous;
                    int packagePercent = p.PackageTotal == 0 ? 100 : (int)(100L * (p.PackageIndex - 1) / p.PackageTotal);
                    int filePercent = p.FileTotal == 0 ? 100 : (int)(100L * p.FileIndex / p.FileTotal);
                    progressBar.Value = Math.Max(0, Math.Min(100, packagePercent + filePercent / Math.Max(1, p.PackageTotal)));
                    statusLabel.Text = L("Extraction du client…", "Extracting client…", "Client wird entpackt…") + Environment.NewLine + p.Package + " (" + p.PackageIndex + " / " + p.PackageTotal + ")" + Environment.NewLine + p.FileIndex + " / " + p.FileTotal;
                });

                using (var network =
                    new Network(config.requestTimeoutSeconds))
                {
                    var planner =
                        new FullInstallPlanner();

                    InstallPlan plan = planner.Plan(null, manifest);
                    string versionPath = Safety.Under(pathBox.Text, ".aioncl/version.json");
                    if(File.Exists(versionPath)) {
                        var local=Json.Parse<LocalVersion>(File.ReadAllText(versionPath));
                        if(local!=null&&Updates.Newer(local.version,manifest.Manifest.clientVersion))throw new InvalidOperationException("A newer client is installed. Check for updates before installing.");
                        statusLabel.Text=L("Recherche des fichiers à mettre à jour…","Checking files for updates…","Dateien für das Update werden geprüft…");
                        progressBar.Style=ProgressBarStyle.Marquee;
                        var issues=await Installation.Verify(pathBox.Text,manifest.Manifest,cts.Token);
                        var names=issues.Select(issue=>issue.Package).ToArray();
                        plan.Packages=manifest.Manifest.packages.Where(p=>names.Contains(p.name)).ToArray();
                        progressBar.Style=ProgressBarStyle.Continuous;
                    }

                    await Installation.Install(
                        pathBox.Text,
                        plan,
                        config,
                        network,
                        progress,
                        Log,
                        cts.Token,
                        extractionProgress
                    );
                }

                progressBar.Value = 100;
                statusLabel.Text =
                    "Installation terminée.";

                RefreshClientState();
            }
            catch (OperationCanceledException)
            {
                Log("Installation annulée.");
                statusLabel.Text =
                    "Installation interrompue.";
            }
            catch (Exception ex)
            {
                Log(ex.ToString());

                MessageBox.Show(
                    TranslateMessage(ex.Message),
                    TranslateMessage("Échec de l'installation"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task VerifyAsync()
        {
            if (!EnsurePath())
                return;
            var localMarker=Safety.Under(pathBox.Text,".aioncl/version.json");
            if(File.Exists(localMarker)) {
                var localVersion=Json.Parse<LocalVersion>(File.ReadAllText(localMarker));
                if(localVersion!=null&&Updates.Newer(localVersion.version,manifest.Manifest.clientVersion)) {
                    MessageBox.Show(this,L("Client plus récent : recherchez les mises à jour avant de vérifier.","Newer client installed: check for updates before verifying.","Neuerer Client installiert: vor der Prüfung nach Updates suchen."),"AionCL");return;
                }
            }
            else {
                // No marker means the previous installation was interrupted.
                // Reuse the package cache and resume directly instead of hashing
                // the entire client before starting the repair.
                statusLabel.Text=L("Installation partielle : reprise du client…","Partial installation: resuming the client…","Teilinstallation: Client wird fortgesetzt…");
                await InstallAsync();
                return;
            }

            SetBusy(true);

            cts = new CancellationTokenSource();
            string outcome = null;
            bool reporting = true;

            try
            {
                Log("Vérification complète...");
                statusLabel.Text = L("Vérification des fichiers en cours…", "Verifying game files…", "Spieldateien werden geprüft…");
                progressBar.Value = 0;
                progressBar.Style = ProgressBarStyle.Marquee;
                var progress = new Progress<VerificationProgress>(delegate(VerificationProgress p) {
                    if (!reporting || IsDisposed) return;
                    statusLabel.Text = L("Vérification", "Verifying", "Prüfung") + " : " + p.Completed + " / " + p.Total + Environment.NewLine + p.Path;
                    progressBar.Style = ProgressBarStyle.Continuous;
                    progressBar.Value = p.Total == 0 ? 100 : (int)(100L * p.Completed / p.Total);
                });

                var issues =
                    await Installation.Verify(
                        pathBox.Text,
                        manifest.Manifest,
                        cts.Token,
                        progress
                    );
                reporting = false;
                if(hitFont.Installed(pathBox.Text,selectedLanguage)&&!hitFont.Valid(pathBox.Text,selectedLanguage))throw new InvalidDataException("Japan hit font modified: existing file preserved.");
                if (koreanPack.HasPack(pathBox.Text, selectedLanguage)) {
                    statusLabel.Text = L("Vérification du pack coréen…", "Verifying Korean voice pack…", "Koreanisches Stimmenpaket wird geprüft…");
                    progressBar.Style = ProgressBarStyle.Marquee;
                    if (!await koreanPack.Valid(pathBox.Text, selectedLanguage, cts.Token))
                        throw new InvalidDataException(L("Le pack coréen est incomplet ou modifié. Consultez le journal avant de le réinstaller.", "The Korean pack is incomplete or modified. Check the log before reinstalling.", "Das koreanische Paket ist unvollständig oder verändert. Vor Neuinstallation das Protokoll prüfen."));
                }
                if (japanesePack.HasPack(pathBox.Text, selectedLanguage)) {
                    statusLabel.Text = L("Vérification du pack japonais…", "Verifying Japanese voice pack…", "Japanisches Stimmenpaket wird geprüft…");
                    progressBar.Style = ProgressBarStyle.Marquee;
                    if (!await japanesePack.Valid(pathBox.Text, selectedLanguage, cts.Token))
                        throw new InvalidDataException(L("Le pack japonais est incomplet ou modifié. Consultez le journal avant de le réinstaller.", "The Japanese pack is incomplete or modified. Check the log before reinstalling.", "Das japanische Paket ist unvollständig oder verändert. Vor Neuinstallation das Protokoll prüfen."));
                }
                progressBar.Style = ProgressBarStyle.Continuous;
                progressBar.Value = 100;
                outcome = L("Vérification terminée : tous les fichiers sont valides.", "Verification complete: all files are valid.", "Prüfung abgeschlossen: Alle Dateien sind gültig.");

                if (issues.Count == 0)
                {
                    Json.Atomic(Safety.Under(pathBox.Text, ".aioncl/version.json"), new LocalVersion { version = manifest.Manifest.clientVersion, manifestHash = manifest.Hash, installedAt = DateTime.UtcNow.ToString("o") });
                    Log("Tous les fichiers sont valides.");
                    statusLabel.Text =
                        "Client valide.";
                }
                else
                {
                    Log(
                        issues.Count +
                        " fichier(s) à réparer."
                    );

                    foreach (var issue in issues)
                    {
                        Log(
                            issue.Reason +
                            " : " +
                            issue.Path +
                            " [" +
                            issue.Package +
                            "]"
                        );
                    }

                    statusLabel.Text =
                        issues.Count +
                        " fichier(s) invalide(s).";
                    using (var network = new Network(config.requestTimeoutSeconds))
                    {
                        var names = issues.Select(issue => issue.Package).ToArray();
                        var plan = new InstallPlan { Target = manifest, Packages = manifest.Manifest.packages.Where(p => names.Contains(p.name)).ToArray() };
                        Log("Reparation des packages concernes...");
                        progressBar.Style = ProgressBarStyle.Marquee;
                        statusLabel.Text = L("Réparation des fichiers en cours…", "Repairing game files…", "Spieldateien werden repariert…");
                        await Installation.Install(pathBox.Text, plan, config, network, null, Log, cts.Token);
                        outcome = L("Vérification et réparation terminées.", "Verification and repair complete.", "Prüfung und Reparatur abgeschlossen.");
                    }
                }

                RefreshClientState();
            }
            catch (OperationCanceledException)
            {
                Log("Vérification annulée.");
                outcome = L("Vérification interrompue. Le contrôle complet reste à faire.", "Verification interrupted. A full check is still required.", "Prüfung unterbrochen. Eine vollständige Prüfung steht noch aus.");
            }
            catch (Exception ex)
            {
                outcome = L("Échec de la vérification. Consultez le journal.", "Verification failed. See the log.", "Prüfung fehlgeschlagen. Siehe Protokoll.");
                Log(ex.ToString());

                MessageBox.Show(
                    TranslateMessage(ex.Message),
                    TranslateMessage("Erreur de vérification"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                reporting = false;
                progressBar.Style = ProgressBarStyle.Continuous;
                SetBusy(false);
                if (outcome != null) statusLabel.Text = outcome;
            }
        }

        private async Task PlayAsync()
        {
            if (!EnsurePath())
                return;

            var directAuthUser = authUser.Text.Trim();
            var directAuthPassword = authPassword.Text;
            if (String.IsNullOrWhiteSpace(directAuthUser) || String.IsNullOrEmpty(directAuthPassword)) {
                authStatus.Text = TranslateMessage("Identifiant et mot de passe requis avant JOUER.");
                return;
            }
            var credentialError = ValidateNativeCredentials(directAuthUser, directAuthPassword);
            if (credentialError != null) { authStatus.Text = TranslateMessage(credentialError); return; }
            launcherAuth.SaveCredentials(directAuthUser, directAuthPassword, authRemember.Checked);
            RefreshAccountBox(directAuthUser);
            authStatus.Text = TranslateMessage("Connexion directe en préparation…");

            SetBusy(true);

            cts = new CancellationTokenSource();

            try
            {
                using (var network =
                    new Network(config.requestTimeoutSeconds))
                {
                    var service =
                        new ServerService(network);

                    ServerConfig server =
                        await service.Load(
                            config.serverConfigUrl,
                            pathBox.Text,
                            Log,
                            cts.Token
                        );

                    var game =
                        new GameLauncher(config);

                    game.ValidateInstallation(
                        pathBox.Text,
                        manifest
                    );

                    IPAddress ip =
                        await game.ResolveServer(
                            server,
                            cts.Token
                        );

                    Log(
                        "Serveur résolu : " +
                        server.loginHost +
                        " -> " +
                        ip
                    );

                    await ServerService.Tcp(
                        ip,
                        server.loginPort,
                        cts.Token
                    );

                    var command =
                        game.BuildLaunchCommand(
                            pathBox.Text,
                            server,
                            ip
                        );

                    string runtimeLanguage = GameLanguage.Runtime(selectedLanguage);
                    if (!File.Exists(Safety.Under(pathBox.Text, "L10N/" + runtimeLanguage + "/" + runtimeLanguage + ".pak")))
                        throw new InvalidOperationException(L("Les fichiers de cette langue sont absents. Vérifiez / réparez le client.", "Files for this language are missing. Verify / repair the game.", "Die Sprachdateien fehlen. Bitte das Spiel prüfen / reparieren."));
                    command.Arguments = GameLanguage.Apply(command.Arguments, selectedLanguage);

                    // Explicit direct mode follows the native Aion launcher contract.
                    // Credentials are held only in memory and are never persisted by
                    // this mode unless the user separately checks Windows storage.
                    command.Arguments += " -account:" + NativeArgument(directAuthUser) + " -password:" + NativeArgument(directAuthPassword);

                    Log(
                        "Lancement : " +
                        command.FileName +
                        " " +
                        RedactSessionKey(command.Arguments)
                    );

                    game.StartGame(command);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());

                MessageBox.Show(
                    TranslateMessage(ex.Message),
                    TranslateMessage("Impossible de lancer Aion"),
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                SetBusy(false);
            }
        }

        private static string RedactSessionKey(string arguments) {
            if (String.IsNullOrEmpty(arguments)) return arguments;
            const string marker = "/SessKey:\"";
            var start = arguments.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            var redacted = arguments;
            if (start >= 0) {
                var valueStart = start + marker.Length;
                var end = arguments.IndexOf('"', valueStart);
                redacted = end < 0 ? arguments.Substring(0, valueStart) + "[redacted]" : arguments.Substring(0, valueStart) + "[redacted]" + arguments.Substring(end);
            }
            var password = redacted.IndexOf(" -password:", StringComparison.OrdinalIgnoreCase);
            if (password < 0) return redacted;
            var startPassword = password + 11;
            var endPassword = redacted.IndexOf(' ', startPassword);
            return endPassword < 0 ? redacted.Substring(0, startPassword) + "[redacted]" : redacted.Substring(0, startPassword) + "[redacted]" + redacted.Substring(endPassword);
        }

        private static string NativeArgument(string value) {
            if (String.IsNullOrEmpty(value)) return "\"\"";
            // Aion's command-line parser reads the value after the colon
            // literally. Avoid quotes for the normal ASCII credential case;
            // quoted values are otherwise sent with the quote characters.
            if (value.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '\"' }) < 0) return value;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static string ValidateNativeCredentials(string username, string password) {
            if (String.IsNullOrWhiteSpace(username)) return "Identifiant requis.";
            if (username.IndexOfAny(new[] { ' ', '\t', '\r', '\n', '\"' }) >= 0) return "L'identifiant ne doit pas contenir d'espace ou de guillemet.";
            if (password.IndexOfAny(new[] { '\r', '\n', '\"' }) >= 0) return "Le mot de passe contient un caractère non pris en charge par le client.";
            if (password.Trim() != password) return "Le mot de passe ne doit pas commencer ou finir par un espace.";
            if (Encoding.UTF8.GetByteCount(password) > 32) return "Le mot de passe du client Aion doit contenir au maximum 32 octets.";
            return null;
        }

        private bool EnsurePath()
        {
            if (String.IsNullOrWhiteSpace(pathBox.Text))
            {
                MessageBox.Show(
                    TranslateMessage("Choisis d'abord un dossier client."),
                    "AionCL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return false;
            }

            Directory.CreateDirectory(pathBox.Text);
            SaveClientPath();
            return true;
        }

        private void SetBusy(bool busy)
        {
            this.busy = busy;
            languageBox.Enabled = !busy;
            koreanVoices.Enabled = !busy;
            japaneseVoices.Enabled = !busy;
            hitFontButton.Enabled = !busy;
            RefreshUpdateUi();
            pathBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            installButton.Enabled = !busy &&
                                    manifest != null;
            verifyButton.Enabled = !busy &&
                                   manifest != null;
            playButton.Enabled = !busy && manifest != null;
            cancelButton.Enabled = busy;
            cancelButton.Visible = busy;
            playButton.Visible = !busy;
            if (!busy) RefreshClientState();
        }

        private void Log(string message)
        {
            if (InvokeRequired)
            {
                BeginInvoke(
                    new Action<string>(Log),
                    message
                );
                return;
            }

            message = TranslateMessage(message);
            logBox.AppendText(
                "[" +
                DateTime.Now.ToString("HH:mm:ss") +
                "] " +
                message +
                Environment.NewLine
            );
        }

        private static string FormatBytes(long bytes)
        {
            double value = bytes;

            string[] units =
            {
                "B", "KB", "MB", "GB", "TB"
            };

            int unit = 0;

            while (value >= 1024 &&
                   unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return value.ToString("0.00") +
                   " " +
                   units[unit];
        }

        private static string FormatDuration(double seconds)
        {
            if (seconds < 60) return Math.Max(1, (int)Math.Ceiling(seconds)) + " s";
            var span = TimeSpan.FromSeconds(seconds);
            return span.TotalHours >= 1 ? ((int)span.TotalHours) + " h " + span.Minutes.ToString("00") + " min" : ((int)span.TotalMinutes) + " min " + span.Seconds.ToString("00") + " s";
        }
    }
}
