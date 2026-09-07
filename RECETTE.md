# État et préparation

Configuration HTTPS préparée localement, URL constatée HTTP 404 le 7 septembre 2026. Aucune publication effectuée. Source retenue : fichier JSON versionné du dépôt public AionCL/launcher, branche main, modifiable indépendamment des archives client. Ne pas distribuer avant disponibilité publique et recette réelle. Aucun repli embarqué : il risquerait de masquer une maintenance publiée.

## Build et petit paquet launcher

PowerShell, dossier C:\Users\aymen\git\github.com\launcher :

```powershell
Set-Location C:\Users\aymen\git\github.com\launcher
Start-Transcript -Path ('.local\distribution-' + (Get-Date -Format yyyyMMdd-HHmmss) + '.log')
.\package-launcher.ps1
Stop-Transcript
```

Attendu : 10 groupes réussis, 0 échec, ZIP out\AionCL-Launcher-<date>.zip et SHA-256 affiché. Ce script ne contient pas le client, ne publie rien et sauvegarde les configurations out existantes dans .local/backups/build-*.

## Publication manuelle

Le dépôt local n’a encore aucun commit. Vérifier les fichiers ci-dessous avant de créer le premier commit ; ne pas ajouter globalement le dépôt (src/Core.cs.bak préexiste et doit rester local).

```powershell
git status --short
git add -- .gitignore AGENTS.md build.ps1 package-launcher.ps1 DISTRIBUTION.md RECETTE.md config/launcher.json config/server-config.json config/app.config src/Core.cs src/Program.cs tests/tests.cs tests/RegressionTests.cs
git diff --cached --stat
git diff --cached
git commit -m "Prepare launcher server configuration and validated distribution"
git push -u origin main
```

À exécuter uniquement lorsque tu décides de publier ; aucun push effectué par l’agent. Si le push est rejeté, ne pas forcer : reprendre l’inspection de l’historique distant. Le dépôt doit être public pour que les joueurs accèdent au JSON sans authentification.

```powershell
$url = 'https://raw.githubusercontent.com/AionCL/launcher/main/config/server-config.json'
$server = Invoke-RestMethod -Uri $url -TimeoutSec 30
if ($server.version -ne 1 -or $server.loginHost -ne 'aioncl.freeddns.org' -or $server.loginPort -ne 2106 -or $server.gamePort -ne 7777 -or $server.maintenance -isnot [bool]) { throw 'Configuration publique inattendue' }
$server | ConvertTo-Json
```

Attendu : HTTP réussi et paramètres conformes. Publier ensuite le ZIP launcher via une nouvelle release GitHub du dépôt launcher, après la recette. Ne pas remplacer les assets client-2.4. La commande facultative suivante exige GitHub CLI (non trouvé dans PATH lors de l’inspection) :

```powershell
$zip = Get-ChildItem .\out\AionCL-Launcher-*.zip | Sort-Object LastWriteTime -Descending | Select-Object -First 1
gh release create launcher-v1.0.0 $zip.FullName --repo AionCL/launcher --title 'AionCL Launcher 1.0.0' --notes-file DISTRIBUTION.md
```

## Recette réelle à exécuter par l’utilisateur

1. Depuis le ZIP extrait, choisir un nouveau dossier vide, différent des clients validés. INSTALLER est une opération lourde (environ 18,9 Go téléchargés) réservée à l’utilisateur. Ne créer aucun JSON manuellement.
2. Interrompre un téléchargement avec ANNULER, puis reprendre INSTALLER. Attendre « Installation validée ». Les fichiers .part se trouvent dans <client>/.aioncl/cache ; le marqueur final est <client>/.aioncl/version.json.
3. Cliquer JOUER. Vérifier que <client>/.aioncl/server-config.json apparaît automatiquement et que le jeu démarre. Reconfirmer Gelkmaros, Inggison, Silentera et Beshmundir sur cette installation si possible.
4. Après fermeture du jeu, VÉRIFIER / RÉPARER : aucun fichier personnel ou cache généré ne doit être supprimé. La vérification complète est longue et reste à exécuter par l’utilisateur. La corruption et la réparation sont déjà testées sur petites fixtures, ne pas endommager le client validé pour ce test.
5. Noter les résultats et copier le journal affiché par le launcher dans .local/recette-reelle.txt. Le journal UI n’est pas encore écrit automatiquement sur disque. Ne pas activer une maintenance publique pour un simple test ; cette logique est couverte localement.

Limites : tests automatisés du cœur, pas de validation visuelle UI ni de nouveau lancement réel par l’agent ; reprise testée à partir d’un .part synthétique, pas après arrêt forcé Windows. Pas de signature ni de mise à jour automatique du launcher. Manifest nécessaire au démarrage. Ces points et la recette distante restent ouverts avant qualification finale.
