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

## Interface AionCL inspirée d’Utopia — 7 septembre 2026

La recette jeu précédente est validée par l’utilisateur (installation D:\games\tmp et maps problématiques OK). Une nouvelle présentation est maintenant préparée : fond portail intégré à l’exécutable, menu latéral, panneau client, bouton JOUER, aide et journal séparé. Le dossier choisi est mémorisé dans %LOCALAPPDATA%\AionCL\launcher-path.txt. Aucune URL de compte ou règle non fournie n’a été inventée.

Pour cette recette UI, ouvrir out\AionCL.Launcher.exe et choisir D:\games\tmp : aucune nouvelle installation nécessaire. Vérifier lisibilité, navigation AIDE / ACTUALITÉS / JOURNAL, fermeture et réouverture (dossier mémorisé). Tester JOUER lorsque le jeu déjà ouvert a été fermé. Le cœur et le profil de lancement sont conservés ; le nouveau rendu est vérifié hors écran à deux tailles, le lancement réel avec cette interface reste à confirmer.

Avant une éventuelle publication du nouveau code, inclure aussi src/LauncherUi.cs et assets/portal.png dans la sélection Git explicite. Utiliser le nouveau ZIP produit par package-launcher.ps1 ; le ZIP de 11:52 contient l’ancienne interface. Rien n’a été publié par l’agent.

## Habillage Classic fourni par l’utilisateur — 7 septembre 2026

Nouvelle version dans out\classic-preview\AionCL.Launcher.exe. L’exécutable out principal était ouvert : il est conservé, aucun processus utilisateur fermé. Image originale assets/classic-battle.jpg intégrée sans retouche, palette violette, carte d’accueil compacte, navigation sélectionnée et survol. Source 640 × 300 : agrandissement visible. Ancien asset portal.png conservé mais non embarqué.

Build isolé reproductible :

```powershell
.\build.ps1 -Tests -OutputDirectory .\out\classic-preview
.\out\classic-preview\AionCL.Tests.exe
```

L’utilisateur a confirmé la recette bout en bout de l’interface précédente. Incident de sélection serveur résolu séparément côté serveur (member status). Cette nouvelle variante est une modification visuelle ; réutiliser le dossier client mémorisé, sans réinstallation. À inclure au prochain commit choisi : assets/classic-battle.jpg en plus de src/LauncherUi.cs et des fichiers modifiés. Aucune publication effectuée.

## Nouvelle composition et langues

Version à essayer : out\redesign\AionCL.Launcher.exe. Navigation horizontale et image dans son cadre, non étirée. Sélecteur Français / English / Deutsch pour le launcher ET le jeu, mémorisé dans %LOCALAPPDATA%\AionCL\language.txt. Ces trois dossiers L10N et ENG.pak sont présents dans le client inspecté ; le launcher vérifie le PAK de la langue choisie avant lancement. Les autres langues du jeu nécessitent des ressources supplémentaires non présentes dans cette installation.

Les textes usuels du launcher sont traduits ; les diagnostics techniques bruts peuvent conserver leur langue d’origine. Le profil de lancement conserve ses paramètres, seul -lang change selon le choix. Français reste la valeur par défaut. Les tests valident FRA/ENG/DEU et le refus de valeurs injectées ou de plusieurs arguments langue. Le rendu est inspecté hors ligne ; lancement du jeu en anglais/allemand à confirmer par l’utilisateur.

Build : .\build.ps1 -Tests -OutputDirectory .\out\redesign. Ajouter aussi src/Localization.cs à la sélection de fichiers du prochain commit, si publication décidée. Rien publié par l’agent.

## Retour visuel vérification
Version corrigée : out/verify-progress/AionCL.Launcher.exe. Affiche fichier courant, nombre de fichiers vérifiés, progression et résultat final ; Interrompre annule le contrôle. Réparation signalée séparément. Progression par fichier (un gros fichier peut prendre du temps). Tests synthétiques progression/annulation réussis ; vérification intégrale réelle réservée à l’utilisateur.

## Test coréen — voix du personnage uniquement

Ouvrir out\korean-preview\AionCL.Launcher.exe, jeu fermé. Garder Français pour les textes, cocher « Voix personnage coréennes (test) », puis JOUER. Écouter les cris de combat, sorts et emotes. Le client utilise ses voix de base : langue coréenne et couverture restent à confirmer à l’écoute. Aucun pack téléchargé. PNJ, cinématiques et musique ne sont pas modifiés. Japonais exclu de cette étape.

Activation uniquement au clic JOUER, après les contrôles réseau : L10N/<langue>/sounds/voice est déplacé, sans suppression, vers .aioncl/voice-original/<langue>. L’option est mémorisée dans %LOCALAPPDATA%\AionCL\language.txt.voices. Le changement est refusé si Aion tourne. Pour restaurer : fermer le jeu, décocher l’option, cliquer JOUER dans la même langue de texte. Si plusieurs langues ont été testées, restaurer chacune de la même façon.

Utiliser cette nouvelle version pour vérifier/réparer une installation avec l’option active : elle contrôle les originaux sauvegardés et répare à cet emplacement, sans annuler le choix. Un ancien launcher ignore ce mécanisme et pourrait recréer les fichiers localisés ; la nouvelle version refuserait alors le conflit sans rien écraser. Ne pas effacer .aioncl/voice-original.

Tests locaux : activation idempotente, sauvegarde intacte, vérification en mode actif, détection corruption sauvegarde, restauration et refus de conflit. Rendu checkbox inspecté. Aucune modification audio réelle par l’agent ; recette à l’écoute encore requise.

## Bouton natif du pack coréen

Nouvelle version : out\voice-manager\AionCL.Launcher.exe. L’ancienne case de repli audio est supprimée. Le bouton détecte les 84 fichiers du pack pour la langue de texte sélectionnée : « Voix coréennes : retirer » ou « Installer les voix coréennes ». Le pack validé déjà présent par script est reconnu.

Jeu fermé : retirer déplace les fichiers connus vers .aioncl/korean-pack-cache ; réinstaller les recopie depuis ce cache après contrôle SHA256. Aucun original n’est remplacé. Sans cache, le launcher retrouve le dossier extrait du précédent essai ou demande ce dossier (contenant voice, npc, system…). Le téléchargement/extraction automatique pour un nouveau joueur reste à préparer ; le pack n’est pas embarqué dans l’EXE.

Le catalogue config/korean-pack.json est intégré au binaire. Le journal .aioncl/korean-pack-<langue>.json marque les opérations, permettant de retirer une installation interrompue. Les fichiers présents mais modifiés sont préservés et signalés, jamais écrasés silencieusement. Vérifier / réparer contrôle également les SHA du pack actif ; en cas de corruption du pack, cette version signale l’anomalie sans réparation automatique des voix. Le client de base conserve sa réparation existante.

Validation : 11 groupes réussis, dont cycle installation/retrait/réinstallation depuis cache, annulation, corruption et refus d’effacement d’un fichier modifié. Rendu inspecté et pack réel reconnu sans le modifier. Tester maintenant le bouton sur l’installation existante, puis relancer le jeu pour comparer les voix. Répéter par langue de texte si plusieurs langues sont utilisées. Ancien script Disable à éviter après usage du bouton (cache géré par le nouveau launcher).

Build : .\build.ps1 -Tests -OutputDirectory .\out\voice-manager. Nouveaux fichiers à inclure au prochain commit choisi : src/KoreanPack.cs et config/korean-pack.json, ainsi que src/Localization.cs, src/LauncherUi.cs et les modifications déjà consignées. Aucun push/release effectué.

## Japan hit font

Version : out\hit-font\AionCL.Launcher.exe. Jeu fermé, cliquer « Installer Japan hit font », puis JOUER. Tester dégâts, critiques, esquive et résistance. Le bouton devient « Japan hit font : retirer » pour retrouver le rendu original, après fermeture du jeu. Installation pour la langue de texte sélectionnée, sans modification voix/textes.

Pack trouvé sur https://guillef.weebly.com/japanese-hit-font.html (lien MediaFire wg1a45qc46cjw72). Archive 44092 octets, un fichier PAK 47270 octets, embarqué dans assets/japanese-hit-font.pak. SHA256 PAK : 8F2B0683B9ABAD09337066C2A99ED30AD26043FCEF2E7410F35AB788C467AAFF. Chemin d’origine ENU adapté à FRA/ENG/DEU : L10N/<lang>/textures/ui/hit_number.pak. Le fichier global Textures/ui/hit_number/hit_number.pak n’est pas remplacé.

Tout fichier différent déjà présent à destination est préservé et bloque l’opération. Retrait par déplacement dans .aioncl/hit-font-removed. SHA contrôlé lors de Vérifier ; modification inattendue signalée. 11 groupes de tests OK avec assertions installation/retrait/conflit du hit font. Rendu UI inspecté. Compatibilité visuelle Classic 2.4 à confirmer par utilisateur ; aucun fichier client modifié par l’agent. Aucun droit de redistribution supplémentaire établi ; source conservée pour revue avant publication publique.

## Présentation et découverte des mises à jour

Version locale à essayer : out\updates-preview\AionCL.Launcher.exe, launcher 1.1.0. Encadré de notification et recherche manuelle, automatique au démarrage puis toutes les cinq minutes hors opération. Guide de publication : UPDATES.md. Flux public config/updates.json encore à publier (404 constaté). Une erreur de recherche n’annonce pas un succès ; le manifest déjà chargé reste utilisable.

Patch client détecté : bouton METTRE À JOUR LE JEU, contrôle des fichiers au clic puis téléchargement des seuls packages nécessaires. Nouveau launcher : bouton Télécharger le launcher vers la release ; extraction/remplacement manuels, pas d’auto-update binaire dans cette étape. Aucun patch réel publié ou téléchargé par l’agent. 12 groupes tests OK ; notification launcher simulée vérifiée hors réseau ; recette publique à réaliser après publication.
