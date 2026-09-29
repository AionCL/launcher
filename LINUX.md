# Launcher Linux — 2.5.42 Linux preview

Première adaptation expérimentale du launcher Windows 2.5.42. Même interface,
assets, moteur de téléchargement/reprise, SHA-256, réparation, flux client et
langues. Le launcher tourne sous Mono/X11 ; seul le jeu est lancé via Wine.
Wayland nécessite XWayland. La compatibilité du jeu et le rendu sur GPU restent
à qualifier sur le poste de recette ; ce paquet n'est pas une version stable.

## Construire et tester sur la VM

```sh
docker build -t aioncl-linux-build -f linux/Dockerfile linux
docker run --rm --user "$(id -u):$(id -g)" -e HOME=/tmp -v "$PWD:/src" aioncl-linux-build
```

Ou installer Mono (compilateur `mcs`), libgdiplus, Xvfb, xauth et DejaVu puis
exécuter `./linux/build.sh`. Le script compile, exécute les régressions du cœur
et les tests Linux, rend l'interface sous Xvfb puis produit
`out/AionCL-Launcher-2.5.42-linux-preview.26.tar.gz` et son SHA-256 par défaut. Il produit
aussi des paquets `.deb` et `.rpm`, avec contrôle de leur contenu et dépendances.
Le rendu de contrôle reste dans `out/linux/linux-preview.png`.

## Lancer sur un bureau Linux

Les paquets `.deb` installent automatiquement les prérequis Debian/Ubuntu :
`mono-runtime`, `libmono-system-windows-forms4.0-cil`,
`libmono-system-web-extensions4.0-cil`, `libmono-system-net-http4.0-cil`,
`libmono-system-io-compression-filesystem4.0-cil`, `libgdiplus`, `fonts-dejavu-core`,
`libssl3t64` (ou `libssl3`), `xdg-utils`, `curl`, `cabextract`, `procps`, `tar`,
certificats et Wine. Le RPM déclare les équivalents Mono, OpenSSL, DejaVu, curl,
cabextract, procps-ng, tar et Wine dans ses dépendances. XWayland est requis
sous Wayland. Le fichier de bureau et son icône sont installés dans le menu système.
Installation du `.deb` de préversion : `sudo apt install
./aioncl-launcher_..._amd64.deb`. Pour le RPM : `sudo dnf install
./aioncl-launcher-...x86_64.rpm`. Les paquets Linux `.deb`, `.rpm` et l'archive
portable sont publiés dans les [GitHub Pre-releases](https://github.com/AionCL/launcher/releases).
Le launcher met à jour le client du jeu ; le paquet du launcher lui-même se met à
jour en installant la nouvelle préversion publiée.

Pour créer les paquets, le build demande `dpkg-deb`, `rpmbuild` et les outils déjà
présents dans le conteneur Linux fourni.

Extraire l'archive dans un dossier permanent puis exécuter `./aioncl-launcher`.
Choisir un dossier de client Linux dédié dans l'interface ; le launcher installe
et met à jour les mêmes packages publiés que sous Windows. Aucun nouveau package
client n'est créé pour ce portage. Au premier lancement du jeu, puis si un
composant manque ou a changé, le launcher prépare automatiquement la couche Linux
dans le client/préfixe sélectionné : DXVK 2.6.2, D3DCompiler47 Microsoft natif,
options DXVK et profil Wine Windows 7 propre à Aion (correctif du gel au clic
droit). Les téléchargements sont vérifiés par SHA-256 et les installateurs
conservent les fichiers remplacés. Les lancements suivants vérifient les
composants sans invalider à nouveau les caches de shaders.

Variables facultatives avant lancement :

```sh
export AIONCL_WINE=/chemin/vers/wine
export AIONCL_WINEPREFIX="$HOME/.local/share/AionCL/wine"
./aioncl-launcher
```

`AIONCL_WINE` désigne un exécutable (pas une commande avec arguments).
Le préfixe par défaut est `AionCL/wine` sous LocalApplicationData de Mono.
Wine initialise ce préfixe au premier lancement du jeu. Un wrapper Wine peut
être utilisé ; Proton n'est pas encore intégré. Ne pas partager le dossier de
client avec un jeu en cours d'exécution sur une autre machine.

## Données utilisateur et caméra

- Les comptes sauvegardés sont conservés en clair dans
  `~/.aioncl/credentials.json`. Le dossier est limité au propriétaire (`0700`)
  et le fichier à `0600`. Ils survivent à la fermeture du launcher et restent
  présents après désinstallation du paquet ; les supprimer depuis Paramètres
  ou effacer ce fichier.
- Caméra/FOV utilise un pont Wine qui attend 15 secondes après avoir détecté le
  client, puis lance le helper vérifié à priorité CPU réduite (`nice +10`) pour
  réduire la contention pendant le chargement. Les paramètres s'appliquent au
  lancement suivant du jeu.
- Mises à jour du jeu actives. L'auto-update du launcher Windows est désactivé
  pour éviter de remplacer le paquet Linux par un EXE Windows.
- Le paquet natif installe un raccourci système et l'icône. L'archive `.tar.gz`
  reste portable et peut créer un raccourci utilisateur depuis Paramètres.
- La suite synthétique ne qualifie pas les packs de voix, le système de fichiers
  sensible à la casse du client réel, DirectX, Wine ou la connexion ingame.

## Recette GPU à venir

Vérifier navigation, textes, langues et rendu à l'échelle du bureau ; installer
le client dans un dossier dédié, interrompre/reprendre, vérifier/réparer ;
lancer via Wine, contrôler connexion, sélection personnage et entrée en jeu.
Valider ensuite audio, boutiques et stabilité. Conserver la version de Wine,
la distribution, le GPU et le pilote avec les résultats.

Référence technique : https://www.mono-project.com/docs/gui/winforms/

## Test graphique borné sans identifiants

Le build produit aussi `out/linux/AionCL.GameSmoke.exe` (outil de recette,
non inclus dans l'archive utilisateur). Depuis une session graphique et avec
les mêmes variables Wine que le launcher :

```sh
mono out/linux/AionCL.GameSmoke.exe /chemin/du/client 120
```

L'outil exige une installation validée et la configuration serveur hors
maintenance. Il lance le client avec le moteur du launcher, sans identifiants,
contrôle sa présence pendant 120 secondes puis ferme uniquement le processus
qu'il a créé. Un processus vivant ne prouve pas un rendu correct : inspecter
la fenêtre et le GPU séparément. L'entrée en jeu reste une recette distincte.

Preview.6 corrige les chemins Windows sur un disque Linux sensible à la casse
(`L10N`/`l10n`, y compris les voix et les dégâts). Les contrôles SHA-256 utilisent
OpenSSL 3 lorsque disponible, avec repli géré si absent. L'extraction emploie
jusqu'à quatre workers ; les packages qui remplacent les mêmes chemins restent
ordonnés. Aucun contrôle d'intégrité n'est supprimé.
Référence EVP : https://docs.openssl.org/3.0/man3/EVP_DigestInit/

## Connexion Wine et bibliothèques graphiques

Preview.7 impose `version=n,b` pour le processus du jeu, tout en conservant les
surcharges des autres DLL. Sans cela, Wine ignore `bin64/version.dll`, le correctif
No-IP livré et vérifié avec le client : l'identification et la liste des serveurs
fonctionnent, mais la sélection affiche l'erreur (6) sans connexion au port7777.
Le rôle de cette DLL est documenté par son auteur :
https://github.com/beyond-aion/aion-version-dll

Le moteur utilise aussi `d3dx9_38.dll`. Sous Wine10, son implémentation intégrée
peut échouer à compiler les shaders (D3DCompile2 E5017) et produire un fond vert.
Le script suivant installe uniquement la bibliothèque Microsoft x64 dans le
préfixe dédié, à partir du redistribuable officiel DirectX Juin2010 dont le
SHA-256 est contrôlé. Dépendances : curl, cabextract, Wine. Fermer le jeu et
exécuter avec le compte propriétaire du préfixe, sans sudo :

```sh
./install-d3dx9.sh /chemin/absolu/du/prefixe-wine
```

Il conserve la DLL précédente dans `aioncl-runtime-backup`, puis configure la
surcharge `d3dx9_38=native,builtin` dans ce préfixe. Les fichiers du client et
les installations Wine système ne sont pas remplacés. Pour revenir au moteur
intégré : `WINEPREFIX=/chemin/du/prefixe wine reg add
'HKCU\Software\Wine\DllOverrides' /v d3dx9_38 /d builtin /f`.
Méthode d'extraction et source Microsoft :
https://github.com/Winetricks/winetricks/blob/master/src/winetricks

Mesures ZBook Debian13/Wine10 : vérification complète client2.4.8 en43,6s ;
installation/retrait des voix KR/JP et police de dégâts en15,3s ; extraction des
deux premiers packages, contrôles SHA inclus, en16,5s. Ces mesures ne constituent
pas une validation de connexion ou de rendu ingame.

## Exécution manuelle et journaux

Pour lancer le prochain build sur la VM sans surveillance interactive :
`./linux/build-logged.sh`. Il regroupe compilation et tests automatisés, conserve
la sortie dans `out/logs/build-*.log` et affiche le code de sortie final. Il ne
déploie rien et ne lance pas le jeu.

Sur le poste de recette, jeu et launcher fermés, appliquer une archive et son
fichier `.sha256` adjacent avec :
`bash apply-preview.sh /chemin/archive.tar.gz /chemin/launcher`.
Le script contrôle le SHA, sauvegarde l'installation précédente et conserve un
journal `apply-preview-*.log` à côté du dossier launcher. Il ne lance pas le jeu.
Preview.8 ajoute `install-dxvk.sh`. Le rendu Direct3D9 intégré à Wine peut laisser
de larges zones noires dans le terrain malgré une interface correcte. Jeu fermé,
installer DXVK dans le client dédié avec :
`./install-dxvk.sh /chemin/absolu/du/client`. Le script utilise DXVK 2.6.2,
contrôle le SHA-256 de l'archive officielle, sauvegarde les éventuelles DLL
précédentes sous `.aioncl/dxvk-backup` et écrit la configuration recommandée
pour Aion. Il déplace aussi le cache de shaders compilé par l'ancien renderer
dans cette sauvegarde afin que le jeu le reconstruise avec DXVK. Le launcher
force ensuite `d3d9=n,b` uniquement dans le processus jeu.
Source : https://github.com/doitsujin/dxvk/releases/tag/v2.6.2

Preview.9 ajoute `install-d3dcompiler.sh` pour les terrains absents accompagnés
de `D3DCompile2 ... E5017` dans le terminal. Jeu fermé, exécuter :
`./install-d3dcompiler.sh /chemin/absolu/du/prefixe-wine /chemin/absolu/du/client`.
Le script installe le D3DCompiler 47 x64 officiel de Microsoft, vérifie son
SHA-256, sauvegarde la DLL Wine et déplace les caches de shaders existants.
Le launcher force ensuite son chargement natif uniquement pour le jeu.

## Compatibilité graphique des personnages (preview26)

La préparation Linux installe aussi Microsoft D3DX9_38 x64, indépendamment de
la marque du GPU. Dans **Paramètres → Compatibilité graphique Linux**, le mode
**DLL Microsoft — recommandé** est activé par défaut. **DLL Wine — dépannage**
force l'implémentation builtin sans désactiver DXVK ou D3DCompiler47. Le choix
est conservé dans `AionCL/linux-graphics.json` sous LocalApplicationData, puis
appliqué au préfixe sélectionné au prochain clic Jouer. Aucun rebuild nécessaire.
Les caches sont sauvegardés/recréés uniquement lors d'un changement de mode ou
de la réparation d'une DLL. Le jeu doit être fermé. Le correctif est confirmé
sur le ZBook NVIDIA ; AMD/Intel et autres distributions restent à qualifier.

## Mise à jour du launcher Linux (à partir de preview26)

Le bouton **Mettre à jour le launcher** apparaît lorsqu'une version Linux plus
récente est publiée. Le flux Linux est distinct du flux Windows et les fichiers
sont vérifiés par SHA-256 avant installation. Le flux Linux est publié après la
release, avec les empreintes des paquets construits par la CI.

- `.deb` installé dans `/opt/aioncl/launcher` : APT via `pkexec`.
- `.rpm` installé dans le même dossier : DNF via `pkexec` (DNF requis).
- Installation portable dans un dossier accessible en écriture : ZIP vérifié,
  extraction strictement contrôlée, remplacement avec sauvegarde du dossier
  précédent ; `launcher.json` local est conservé.

Le launcher et le jeu continuent de tourner sous l'utilisateur normal. Seule
l'installation du paquet système demande l'autorisation administrateur via
Polkit. Une fois l'installation commencée, l'annulation/la fermeture sont
bloquées pour laisser le gestionnaire de paquets terminer. Le launcher redémarre
automatiquement après réussite. En cas d'échec ou de refus d'autorisation, le
launcher affiche l'erreur dans le journal et reste ouvert.

Preview25 et les versions antérieures n'ont pas ce mécanisme : il faut installer
preview26 manuellement une fois. Les mises à jour suivantes sont proposées dans
le launcher ; la construction de chaque nouvelle version reste nécessaire côté
développeur, mais le joueur n'a plus à télécharger/installer manuellement.
