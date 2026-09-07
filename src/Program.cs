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

    public sealed class MainForm : Form
    {
        private LauncherConfig config;
        private ManifestResult manifest;
        private CancellationTokenSource cts;

        private readonly TextBox pathBox = new TextBox();
        private readonly Button browseButton = new Button();
        private readonly Button installButton = new Button();
        private readonly Button verifyButton = new Button();
        private readonly Button playButton = new Button();
        private readonly Button cancelButton = new Button();

        private readonly Label statusLabel = new Label();
        private readonly Label versionLabel = new Label();
        private readonly ProgressBar progressBar = new ProgressBar();
        private readonly TextBox logBox = new TextBox();

        public MainForm()
        {
            Text = "AionCL - Classic 2.4";
            Width = 900;
            Height = 620;
            StartPosition = FormStartPosition.CenterScreen;

            BuildUi();

            Shown += async delegate
            {
                await InitializeAsync();
            };

            FormClosing += delegate
            {
                if (cts != null)
                    cts.Cancel();
            };
        }

        private void BuildUi()
        {
            var pathLabel = new Label
            {
                Left = 20,
                Top = 25,
                Width = 150,
                Text = "Dossier du client"
            };

            pathBox.Left = 20;
            pathBox.Top = 50;
            pathBox.Width = 690;

            browseButton.Left = 720;
            browseButton.Top = 48;
            browseButton.Width = 130;
            browseButton.Text = "Parcourir";
            browseButton.Click += Browse;

            versionLabel.Left = 20;
            versionLabel.Top = 95;
            versionLabel.Width = 500;
            versionLabel.Text = "Version : inconnue";

            statusLabel.Left = 20;
            statusLabel.Top = 125;
            statusLabel.Width = 820;
            statusLabel.Text = "Initialisation...";

            progressBar.Left = 20;
            progressBar.Top = 160;
            progressBar.Width = 830;
            progressBar.Height = 25;

            installButton.Left = 20;
            installButton.Top = 205;
            installButton.Width = 180;
            installButton.Height = 40;
            installButton.Text = "INSTALLER";
            installButton.Enabled = false;
            installButton.Click += async delegate
            {
                await InstallAsync();
            };

            verifyButton.Left = 215;
            verifyButton.Top = 205;
            verifyButton.Width = 180;
            verifyButton.Height = 40;
            verifyButton.Text = "VÉRIFIER / RÉPARER";
            verifyButton.Enabled = false;
            verifyButton.Click += async delegate
            {
                await VerifyAsync();
            };

            playButton.Left = 410;
            playButton.Top = 205;
            playButton.Width = 180;
            playButton.Height = 40;
            playButton.Text = "JOUER";
            playButton.Enabled = false;
            playButton.Click += async delegate
            {
                await PlayAsync();
            };

            cancelButton.Left = 605;
            cancelButton.Top = 205;
            cancelButton.Width = 180;
            cancelButton.Height = 40;
            cancelButton.Text = "ANNULER";
            cancelButton.Enabled = false;
            cancelButton.Click += delegate
            {
                if (cts != null)
                    cts.Cancel();
            };

            logBox.Left = 20;
            logBox.Top = 270;
            logBox.Width = 830;
            logBox.Height = 280;
            logBox.Multiline = true;
            logBox.ReadOnly = true;
            logBox.ScrollBars = ScrollBars.Vertical;

            Controls.Add(pathLabel);
            Controls.Add(pathBox);
            Controls.Add(browseButton);
            Controls.Add(versionLabel);
            Controls.Add(statusLabel);
            Controls.Add(progressBar);
            Controls.Add(installButton);
            Controls.Add(verifyButton);
            Controls.Add(playButton);
            Controls.Add(cancelButton);
            Controls.Add(logBox);
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

                Log("Chargement du manifest distant...");

                using (var network =
                    new Network(config.requestTimeoutSeconds))
                {
                    cts = new CancellationTokenSource();

                    manifest = await network.ManifestAsync(
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
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Erreur d'initialisation";
                Log(ex.Message);
                MessageBox.Show(
                    ex.Message,
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
                    "Choisir le dossier d'installation AionCL";

                if (dialog.ShowDialog(this) ==
                    DialogResult.OK)
                {
                    pathBox.Text = dialog.SelectedPath;
                    RefreshClientState();
                }
            }
        }

        private void RefreshClientState()
        {
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

                statusLabel.Text =
                    "État du client : " + state;

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

                    InstallPlan plan =
                        planner.Plan(null, manifest);

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
                    ex.Message,
                    "Échec de l'installation",
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

            SetBusy(true);

            cts = new CancellationTokenSource();

            try
            {
                Log("Vérification complète...");

                var issues =
                    await Installation.Verify(
                        pathBox.Text,
                        manifest.Manifest,
                        cts.Token
                    );

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
                        await Installation.Install(pathBox.Text, plan, config, network, null, Log, cts.Token);
                    }
                }

                RefreshClientState();
            }
            catch (OperationCanceledException)
            {
                Log("Vérification annulée.");
            }
            catch (Exception ex)
            {
                Log(ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "Erreur de vérification",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                SetBusy(false);
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

                    Log(
                        "Lancement : " +
                        command.FileName +
                        " " +
                        command.Arguments
                    );

                    game.StartGame(command);
                }
            }
            catch (Exception ex)
            {
                Log(ex.ToString());

                MessageBox.Show(
                    ex.Message,
                    "Impossible de lancer Aion",
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
                    "Choisis d'abord un dossier client.",
                    "AionCL",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );

                return false;
            }

            Directory.CreateDirectory(pathBox.Text);
            return true;
        }

        private void SetBusy(bool busy)
        {
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
