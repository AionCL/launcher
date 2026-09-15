# AionCL Launcher

Windows avec .NET Framework 4.8. Extraire tout le ZIP dans un dossier accessible en écriture, puis ouvrir AionCL.Launcher.exe. Choisir un dossier client, INSTALLER, puis JOUER. VÉRIFIER / RÉPARER contrôle les fichiers et réinstalle les packages endommagés. ANNULER permet de reprendre avec INSTALLER ; conserver .aioncl/cache pour réutiliser les téléchargements.

La première utilisation exige Internet et la publication de la configuration serveur. Ne pas distribuer tant que la recette décrite dans RECETTE.md n’est pas validée. Le launcher est portable et non signé ; la signature de distribution reste à organiser.

La configuration serveur provient de https://raw.githubusercontent.com/AionCL/launcher/main/config/server-config.json. Le cache .aioncl/server-config.json est créé automatiquement au premier clic JOUER réussi. En cas de panne distante, un cache valide est réutilisé avec un avertissement. Une maintenance déjà en cache reste bloquante ; une nouvelle maintenance distante ne peut pas être connue hors ligne. Le serveur de jeu reste responsable du contrôle d’accès. Sans cache et sans source accessible, réessayer après rétablissement de la connexion ; aucun fichier manuel n’est demandé.

Les paramètres personnels et fichiers générés absents du manifest ne sont pas supprimés. Conserver les fichiers du launcher ensemble, hors des sous-dossiers du jeu. Une connexion au manifest distant est actuellement nécessaire à chaque ouverture du launcher.

## Release v1.9.1 — interface à onglets

La version `launcher-v1.9.1-tabs` est la version de recette actuelle. Elle désactive le défilement de la fenêtre fixe 1100×640 et sépare Accueil / Paramètres / Journal. Le login reste natif et direct ; l’authentification navigateur a été retirée.

Asset : `AionCL-Launcher-20260914-060714-173.zip`
SHA-256 : `dfc461307fd30769b305794b27f47bb60d742fe0515ac71e9c30f4b81febf75f`

## Release v1.9.2 — barre d’authentification sans chevauchement

La version `launcher-v1.9.2-ergonomic` conserve la fenêtre fixe 1100×640, mais réserve un bandeau inférieur à la connexion et aux états. Le bouton `Interrompre` reste masqué au repos et remplace temporairement `JOUER` uniquement pendant une opération. Les champs d’identifiants restent visibles sur Accueil et Paramètres sans défilement.

## Release v1.9.3 — fenêtre responsive

La version `launcher-v1.9.3-responsive` adapte la fenêtre à la zone de travail Windows, autorise le redimensionnement et réduit automatiquement les champs d’authentification lorsque la largeur disponible est plus faible. La page reste sans défilement et le bandeau d’action reste séparé de l’authentification.

Asset : `AionCL-Launcher-20260914-165837-378.zip`  
SHA-256 : `412d0f9651d66897d0cafd10d924d204efa901083952bc462e0b2e7b2ded5e5`

## Release v2.0.0 — rework visuel complet

Cette version remplace la composition à positions fixes par un shell structuré
avec navigation latérale, zone d’actualités, panneau de configuration du jeu
et bandeau d’authentification permanent. La fenêtre reste sans défilement et
sa taille minimale est calculée pour conserver tous les contrôles visibles.
Le mode compact conserve les boutons communautaires, les champs d’identifiants
et l’action JOUER ; aucun contrôle fonctionnel n’est masqué selon la largeur.

Pre-release : https://github.com/AionCL/launcher/releases/tag/launcher-v2.0.0-redesign  
Asset : `AionCL-Launcher-20260915-205622-099.zip`  
SHA-256 : `2893850b081d87e3c8623af04356fe974d301b22d64f612ab67e064a9c04dd35`

## Release v2.0.1 — journal intégré

Le journal est désormais intégré dans la fenêtre principale via l’onglet
Journal. Il ne s’ouvre plus dans une fenêtre séparée et conserve la même barre
d’authentification et le même bouton JOUER.

## Release v2.0.2 — logos intégrés

Ajout d’une marque AionCL dans la navigation et de pictogrammes vectoriels
Discord/YouTube dans les boutons communautaires. Aucun asset externe ni appel
réseau supplémentaire n’est nécessaire.

## Release v2.0.3 — icône Windows et boutons sociaux

Les boutons Discord et YouTube affichent uniquement leurs pictogrammes, avec
un nom accessible pour les lecteurs d’écran. L’exécutable utilise désormais
`assets/aioncl-icon.ico` comme icône Windows au lieu de l’icône générique du
compilateur.

La version `2.0.4` ajoute la carte d’annonce générique du lancement du serveur.

Le flux `config/updates.json` accepte aussi une section `notice` optionnelle
(`title`, `message`, `actionLabel`, `actionUrl`). Elle affiche une carte
d’information fermable dans le launcher, sans modifier le client de jeu.

## Release v2.0.5 — diffusion fiable du flux

Le launcher consulte désormais le flux via l’URL GitHub `raw` afin d’éviter
les anciennes réponses mises en cache par `raw.githubusercontent.com`. La
carte d’annonce est donc visible dès que le flux publié est rafraîchi.

Pre-release : https://github.com/AionCL/launcher/releases/tag/launcher-v2.0.5-feed


## Release v2.1.0 — diagnostic intégré

Ajoute un test de connexion depuis Paramètres pour vérifier le flux de mises à jour, le manifest et la configuration serveur. La page permet aussi d’effacer les identifiants mémorisés dans Windows.
