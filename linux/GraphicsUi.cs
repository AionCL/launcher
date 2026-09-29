using System;
using System.Drawing;
using System.Windows.Forms;
namespace AionCL {
public sealed partial class MainForm {
    Button linuxGraphicsButton;
    private void BuildLinuxGraphicsUi() {
        linuxGraphicsButton=ButtonAt(installPanel,"",24,378,282,34,false);
        linuxGraphicsButton.Click+=delegate { OpenLinuxGraphicsSettings(); };
    }
    private void OpenLinuxGraphicsSettings() {
        try {
            var settings=LinuxGraphicsSettings.Load();
            using(var dialog=new Form {Text=L("Compatibilité graphique Linux","Linux graphics compatibility","Linux-Grafikkompatibilität"),ClientSize=new Size(600,270),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,StartPosition=FormStartPosition.CenterParent,BackColor=BackColor,ForeColor=ForeColor,Font=Font}) {
                var enabled=new RadioButton {Text=L("DLL Microsoft — recommandé","Microsoft DLL — recommended","Microsoft-DLL — empfohlen"),Bounds=new Rectangle(24,24,552,32),Checked=settings.nativeD3dx};
                var builtin=new RadioButton {Text=L("DLL Wine — dépannage","Wine DLL — troubleshooting","Wine-DLL — Fehlerbehebung"),Bounds=new Rectangle(24,65,552,32),Checked=!settings.nativeD3dx};
                dialog.Controls.AddRange(new Control[]{enabled,builtin});
                LabelAt(dialog,L("Le choix s’applique au prochain lancement du jeu.\nLes caches graphiques sont reconstruits lors d’un changement.\nDXVK et D3DCompiler restent configurés séparément.","Applied on the next game launch.\nGraphics caches are rebuilt when the mode changes.\nDXVK and D3DCompiler remain configured separately.","Wird beim nächsten Spielstart angewendet.\nBeim Wechsel werden die Grafik-Caches neu aufgebaut.\nDXVK und D3DCompiler bleiben separat konfiguriert."),24,112,552,84,10,ForeColor);
                var cancel=ButtonAt(dialog,L("Annuler","Cancel","Abbrechen"),266,218,130,34,false);cancel.DialogResult=DialogResult.Cancel;dialog.CancelButton=cancel;
                var save=ButtonAt(dialog,L("Enregistrer","Save","Speichern"),410,218,166,34,true);
                save.Click+=delegate {new LinuxGraphicsSettings {nativeD3dx=enabled.Checked}.Save(LinuxGraphicsSettings.PreferencePath);dialog.DialogResult=DialogResult.OK;};
                if(dialog.ShowDialog(this)==DialogResult.OK)statusLabel.Text=L("Compatibilité graphique enregistrée pour le prochain lancement.","Graphics compatibility saved for the next launch.","Grafikkompatibilität für den nächsten Start gespeichert.");
            }
        } catch(Exception ex) {MessageBox.Show(this,ex.Message,"AionCL",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
}
}
