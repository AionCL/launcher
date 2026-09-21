using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AionCL {
public sealed class CameraSettings {
    public bool enabled { get; set; }
    public int fov { get; set; }
    public int distance { get; set; }
    public CameraSettings() { fov = 60; distance = 32; }
    public void Validate() {
        if (fov < 60 || fov > 170 || distance < 5 || distance > 100)
            throw new InvalidDataException("Camera settings outside supported range.");
    }
    public static CameraSettings Load(string path) {
        if (!File.Exists(path)) return new CameraSettings();
        var result = Json.Parse<CameraSettings>(File.ReadAllText(path));
        if (result == null) throw new InvalidDataException("Invalid camera settings.");
        result.Validate(); return result;
    }
    public void Save(string path) {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            File.WriteAllText(temp, Json.Serialize(this));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static async Task<string> VerifyHelper(string root, Manifest manifest, CancellationToken token) {
        const string relative = "tools/AionCL.Camera.exe";
        ClientFile expected = null;
        foreach (var package in manifest.packages) foreach (var file in package.files)
            if (String.Equals(file.path, relative, StringComparison.OrdinalIgnoreCase)) expected = file;
        string path = Safety.Under(root, relative);
        if (expected == null || !await Safety.Matches(path, expected.size, expected.sha256, token))
            throw new InvalidDataException("Camera package missing or damaged. Verify / repair the client.");
        return path;
    }
    public Process Start(string helper, Process game) {
        Validate();
        return Process.Start(new ProcessStartInfo {
            FileName = helper, WorkingDirectory = Path.GetDirectoryName(helper),
            Arguments = game.Id.ToString(CultureInfo.InvariantCulture) + " " + fov.ToString(CultureInfo.InvariantCulture) + " " + distance.ToString(CultureInfo.InvariantCulture),
            UseShellExecute = false, CreateNoWindow = true
        });
    }
}

#if CAMERA_UI
public sealed partial class MainForm {
    readonly string cameraPreference = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AionCL", "camera.json");
    Button cameraButton;
    private void OpenCameraSettings() {
        try {
            CameraSettings saved = CameraSettings.Load(cameraPreference);
            using (var dialog = new Form()) {
                dialog.Text = L("Camera", "Camera", "Kamera");
                dialog.ClientSize = new Size(580, 355); dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false; dialog.MinimizeBox = false; dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.BackColor = BackColor; dialog.ForeColor = ForeColor; dialog.Font = Font;
                var enabled = new CheckBox { Text = L("Activer les reglages camera", "Enable camera settings", "Kameraeinstellungen aktivieren"), Checked = saved.enabled, Bounds = new Rectangle(24, 22, 530, 30) };
                dialog.Controls.Add(enabled);
                LabelAt(dialog, L("Distance maximale", "Maximum distance", "Maximaler Abstand"), 24, 74, 420, 24, 10, ForeColor);
                var distance = new TrackBar { Minimum = 5, Maximum = 100, Value = saved.distance, TickStyle = TickStyle.None, Bounds = new Rectangle(20, 106, 438, 40), AccessibleName = "Camera distance" };
                var distanceValue = new NumericUpDown { Minimum = 5, Maximum = 100, Value = saved.distance, Bounds = new Rectangle(470, 108, 80, 28) };
                LabelAt(dialog, L("Champ de vision (FOV)", "Field of view (FOV)", "Sichtfeld (FOV)"), 24, 160, 420, 24, 10, ForeColor);
                var fov = new TrackBar { Minimum = 60, Maximum = 170, Value = saved.fov, TickStyle = TickStyle.None, Bounds = new Rectangle(20, 192, 438, 40), AccessibleName = "Field of view" };
                var fovValue = new NumericUpDown { Minimum = 60, Maximum = 170, Value = saved.fov, Bounds = new Rectangle(470, 194, 80, 28) };
                dialog.Controls.AddRange(new Control[] { distance, distanceValue, fov, fovValue });
                distance.ValueChanged += delegate { distanceValue.Value = distance.Value; };
                distanceValue.ValueChanged += delegate { distance.Value = (int)distanceValue.Value; };
                fov.ValueChanged += delegate { fovValue.Value = fov.Value; };
                fovValue.ValueChanged += delegate { fov.Value = (int)fovValue.Value; };
                var reset = ButtonAt(dialog, L("Valeurs par defaut", "Reset defaults", "Standardwerte"), 24, 249, 210, 34, false);
                reset.Click += delegate { distance.Value = 32; fov.Value = 60; enabled.Checked = false; };
                var cancel = ButtonAt(dialog, L("Annuler", "Cancel", "Abbrechen"), 270, 305, 130, 34, false);
                cancel.DialogResult = DialogResult.Cancel; dialog.CancelButton = cancel;
                var save = ButtonAt(dialog, L("Enregistrer", "Save", "Speichern"), 412, 305, 140, 34, true);
                save.Click += delegate {
                    new CameraSettings { enabled = enabled.Checked, fov = fov.Value, distance = distance.Value }.Save(cameraPreference);
                    dialog.DialogResult = DialogResult.OK;
                };
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    statusLabel.Text = L("Camera enregistree pour le prochain lancement.", "Camera saved for the next launch.", "Kamera fur den nachsten Start gespeichert.");
            }
        } catch (Exception ex) { MessageBox.Show(this, ex.Message, "AionCL", MessageBoxButtons.OK, MessageBoxIcon.Error); }
    }
}
#endif
}
