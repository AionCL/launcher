using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
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
        private readonly Button playButton = new LauncherButton();
        private readonly Button cancelButton = new LauncherButton();

        private readonly Label statusLabel = new Label();
        private readonly Label versionLabel = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly TextBox logBox = new TextBox();

        public MainForm() : this(false) {}

        public MainForm(bool preview)
        {
            Text = "AionCL - Classic 2.4";
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(1100, 680);
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
                await CheckUpdates(true);

                Log("Chargement du manifest distant...");

                using (var network =
                    new Network(config.requestTimeoutSeconds))
                {
                    cts = new CancellationTokenSource();

                    if (manifest == null) manifest = await network.ManifestAsync(
                        config,
                        cts.Token
                    );
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
                playButton.Enabled = false;
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

                playButton.Enabled =
                    state == ClientState.Valid;
            }
            catch (Exception ex)
            {
                playButton.Enabled = false;
                statusLabel.Text =
                    "État du client : erreur";
                Log(ex.Message);
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
                            if (p.Total > 0)
                            {
                                int percent =
                                    (int)Math.Min(
                                        100,
                                        p.Bytes * 100L /
                                        p.Total
                                    );

                                progressBar.Value =
                                    Math.Max(
                                        0,
                                        Math.Min(
                                            100,
                                            percent
                                        )
                                    );
                            }

                            statusLabel.Text =
                                p.Package +
                                " - " +
                                FormatBytes(p.Bytes) +
                                " / " +
                                FormatBytes(p.Total) +
                                " - " +
                                FormatBytes(
                                    (long)p.BytesPerSecond
                                ) +
                                "/s";
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
                        cts.Token
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

                    if (!File.Exists(Safety.Under(pathBox.Text, "L10N/" + selectedLanguage + "/" + selectedLanguage + ".pak")))
                        throw new InvalidOperationException(L("Les fichiers de cette langue sont absents. Vérifiez / réparez le client.", "Files for this language are missing. Verify / repair the game.", "Die Sprachdateien fehlen. Bitte das Spiel prüfen / reparieren."));
                    command.Arguments = GameLanguage.Apply(command.Arguments, selectedLanguage);

                    Log(
                        "Lancement : " +
                        command.FileName +
                        " " +
                        command.Arguments
                    );

                    VoiceMode.Apply(pathBox.Text, selectedLanguage, false);
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
            hitFontButton.Enabled = !busy;
            RefreshUpdateUi();
            pathBox.Enabled = !busy;
            browseButton.Enabled = !busy;
            installButton.Enabled = !busy &&
                                    manifest != null;
            verifyButton.Enabled = !busy &&
                                   manifest != null;
            playButton.Enabled = !busy &&
                                 manifest != null &&
                                 !String.IsNullOrWhiteSpace(
                                     pathBox.Text
                                 );
            cancelButton.Enabled = busy;
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
    }
}
