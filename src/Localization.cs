using System;
namespace AionCL {
public sealed partial class MainForm {
    private string TranslateMessage(string text) {
        if (String.IsNullOrEmpty(text) || selectedLanguage == "FRA") return text;
        string[,] terms = {
            {"Close Aion before changing voices or repairing files.","Close Aion before changing voices or repairing files.","Aion vor dem Ändern der Stimmen oder Reparieren schließen."},
            {"Configuration serveur indisponible. Verifiez la connexion Internet puis reessayez JOUER. Si le probleme persiste, contactez AionCL (configuration publique indisponible).", "Server settings unavailable. Check your Internet connection and try PLAY again. Contact AionCL if the problem persists.", "Serverkonfiguration nicht verfügbar. Prüfe deine Internetverbindung und versuche SPIELEN erneut. Wende dich bei weiteren Problemen an AionCL."},
            {"Serveur en maintenance.","Server under maintenance.","Server wird gewartet."},
            {"Client incomplet : installer ou reparer avant de jouer.","Incomplete game: install or repair before playing.","Spiel unvollständig: Vor dem Spielen installieren oder reparieren."},
            {"Espace disque insuffisant pour cache et preparation.","Not enough disk space for download and installation.","Nicht genügend Speicherplatz für Download und Installation."},
            {"Choisis d'abord un dossier client.","Choose a game folder first.","Wähle zuerst einen Spielordner."},
            {"Choisir le dossier d'installation AionCL","Choose the AionCL installation folder","AionCL-Installationsordner auswählen"},
            {"Impossible de lancer Aion","Unable to launch Aion","Aion konnte nicht gestartet werden"},
            {"Échec de l'installation","Installation failed","Installation fehlgeschlagen"},
            {"Erreur de vérification","Verification error","Prüffehler"},
            {"Erreur d'initialisation","Initialization error","Initialisierungsfehler"},
            {"État du client : erreur","Game status: error","Spielstatus: Fehler"},
            {"Installation terminée.","Installation complete.","Installation abgeschlossen."},
            {"Installation interrompue.","Installation interrupted.","Installation unterbrochen."},
            {"Installation annulée.","Installation cancelled.","Installation abgebrochen."},
            {"Vérification annulée.","Verification cancelled.","Prüfung abgebrochen."},
            {"Vérification complète...","Verifying game files...","Spieldateien werden geprüft..."},
            {"Tous les fichiers sont valides.","All files are valid.","Alle Dateien sind gültig."},
            {"Client valide.","Game files verified.","Spieldateien geprüft."},
            {"Manifest AionCL chargé.","AionCL manifest loaded.","AionCL-Manifest geladen."},
            {"Chargement du manifest distant...","Loading game manifest...","Spielmanifest wird geladen..."},
            {"Reparation des packages concernes...","Repairing affected packages...","Betroffene Pakete werden repariert..."},
            {"Validation finale des fichiers...","Final file verification...","Abschließende Dateiprüfung..."},
            {"Installation validee.","Installation verified.","Installation geprüft."},
            {"Telechargement / validation : ","Download / verification: ","Download / Prüfung: "},
            {"Serveur résolu : ","Server resolved: ","Server aufgelöst: "},
            {"Lancement : ","Launching: ","Start: "},
            {"Extraction : ","Extracting: ","Entpacken: "},
            {" fichier(s) à réparer."," file(s) to repair."," Datei(en) zu reparieren."},
            {" fichier(s) invalide(s)."," invalid file(s)."," ungültige Datei(en)."},
            {"Configuration distante indisponible ou invalide : ","Remote settings unavailable or invalid: ","Remote-Konfiguration ungültig oder nicht verfügbar: "},
            {" Lecture du cache local."," Reading local cache."," Lokaler Cache wird gelesen."},
            {"Configuration serveur en cache utilisee ; la maintenance distante ne peut pas etre actualisee.","Using cached server settings; maintenance status may be outdated.","Gespeicherte Serverkonfiguration verwendet; Wartungsstatus eventuell veraltet."}
        };
        for(int i=0;i<terms.GetLength(0);i++) text=text.Replace(terms[i,0],terms[i,selectedLanguage=="ENG"?1:2]);
        return text;
    }
}
}
