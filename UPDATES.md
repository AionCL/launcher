# Publier les mises à jour AionCL

Le nouveau launcher consulte une adresse stable : https://raw.githubusercontent.com/AionCL/launcher/main/config/updates.json.
Détection au démarrage, toutes les 5 minutes lorsque le launcher est inactif, et sur demande. Ce fichier est préparé localement mais son URL répondait 404 à l’inspection : le publier est nécessaire pour activer la découverte distante. Aucune publication faite par l’agent.

## Client : prochain patch 2.4.1

1. Préparer un manifest COMPLET de la version cible, avec tous les fichiers, tailles et SHA256 et tous les packages nécessaires à une installation neuve. Garder gameVersion="2.4", formatVersion=1, clientVersion="2.4.1". Le launcher accepte les versions 2.4.x du client Classic 2.4.
2. Réutiliser les ZIP inchangés de 2.4.0 et leurs URL. Un package modifié doit avoir une nouvelle URL/nom, par exemple aioncl-client-2.4.1-003.zip. Ne pas remplacer silencieusement un asset publié. Recalculer tailles/hashes/totaux avec les outils de packaging. Cette étape lourde reste réservée à l’utilisateur.
3. Publier les nouveaux ZIP et le manifest sur une release client-2.4 ; vérifier leur accessibilité avant d’annoncer le patch.
4. Dans config/updates.json, modifier client.version et client.manifestUrl vers le nouveau manifest. Publier ce fichier EN DERNIER.

Le bouton du client devient METTRE À JOUR LE JEU. Lors du clic, le launcher vérifie les fichiers locaux et télécharge les packages contenant les différences. La vérification complète peut être longue ; aucun hash complet n’est fait pendant la simple recherche des mises à jour. L’installation neuve utilise toujours le manifest complet. Les fichiers absents du nouveau manifest ne sont pas supprimés automatiquement : pour un patch exigeant des suppressions, un format de migration reste à concevoir. Les mods coréen/hit font sont des ajouts distincts ; ne pas intégrer leurs chemins dans le manifest officiel sans prévoir leur migration.

## Launcher : prochaine version 1.1.1

1. Modifier Updates.LauncherVersion dans src/Updates.cs et launcherVersion dans config/launcher.json (même version, format trois nombres).
2. Compiler et tester, produire le ZIP avec package-launcher.ps1, valider le lancement. Le ZIP doit conserver EXE, EXE.config et launcher.json ensemble.
3. Publier le ZIP dans une release launcher.
4. Modifier launcher.version et launcher.downloadPage dans config/updates.json. downloadPage doit être l’URL HTTPS de la release contenant le ZIP. Publier le flux EN DERNIER.

Une notification propose Télécharger le launcher. Cette version ouvre la page de release ; elle NE remplace PAS automatiquement l’exécutable en cours. L’utilisateur extrait la nouvelle version et la lance. Préférences et packs client restent séparés du launcher. Un installateur automatique avec sauvegarde/rollback reste une amélioration distincte.

## Première activation du flux préparé

Après revue des changements et publication de la version 1.1.0, depuis C:\Users\aymen\git\github.com\launcher :

```powershell
git --no-pager diff -- config/updates.json config/launcher.json
# Ajouter explicitement les nouveaux sources/assets et les modifications à publier.
git add -- config/updates.json config/launcher.json src/Updates.cs src/UpdateUi.cs
# Compléter la sélection avec les autres fichiers de cette version avant commit/build final.
git --no-pager diff --cached --stat
```

Ne pas publier uniquement ces quatre fichiers dans un dépôt dont les autres changements n’ont pas encore été commités. Inclure les modules/packs décrits dans RECETTE.md, et conserver src/Core.cs.bak, .local et out hors Git. Aucun push automatique. Après publication choisie :

```powershell
Invoke-RestMethod 'https://raw.githubusercontent.com/AionCL/launcher/main/config/updates.json' -TimeoutSec 30
```

## Recette

- Sur version actuelle : flux valide sans nouveauté = état à jour.
- Version launcher supérieure dans un flux de test = notification et lien release, sans remplacer l’EXE.
- Version client supérieure avec manifest valide = notification, bouton mise à jour, validation finale marqueur nouvelle version.
- 404 / JSON invalide / réseau interrompu = avertissement, ancienne configuration déjà chargée conservée ; pas de faux succès de recherche.
- Version inférieure = refus. Si le client local est plus récent que le manifest disponible, installer/vérifier ne doit pas le rétrograder.
- Annulation pendant patch = reprise au prochain clic. Ne pas faire de fausse annonce dans le flux public pour tester : utiliser une copie locale de la config pointant sur un flux HTTPS de test.

Tests automatisés : comparaison numérique, versions égales, rejet HTTP/JSON vide/rétrogradation, isolation de configuration avant validation, montée 2.4.0 -> 2.4.1 réutilisant les packages 2.4.0 sans téléchargement. Scénarios réels distants restent à valider après publication par l’utilisateur.
