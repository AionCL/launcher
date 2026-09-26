# Launcher Linux — 2.5.42-linux-preview.8

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
`out/AionCL-Launcher-2.5.42-linux-preview.8.tar.gz` et son SHA-256.
Le rendu de contrôle reste dans `out/linux/linux-preview.png`.

## Lancer sur un bureau Linux

Dépendances Debian/Ubuntu : `mono-runtime`, `libmono-system-windows-forms4.0-cil`,
`libmono-system-web-extensions4.0-cil`, `libmono-system-net-http4.0-cil`,
`libmono-system-io-compression-filesystem4.0-cil`, `libgdiplus`, `fonts-dejavu-core`,
`xdg-utils` et Wine adapté à la distribution. XWayland est requis sous Wayland.

Extraire l'archive dans un dossier permanent puis exécuter `./aioncl-launcher`.
Choisir un dossier de client Linux dédié dans l'interface ; le launcher installe
et met à jour les mêmes packages publiés que sous Windows. Aucun nouveau package
client n'est créé pour ce portage.

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

## Limites de cette première recette

- Identifiants uniquement en mémoire ; mémorisation désactivée en attendant
  l'intégration d'un trousseau Linux.
- Caméra/FOV désactivé : le helper Windows attend un PID Windows, différent du
  PID du processus Wine observé sous Linux.
- Mises à jour du jeu actives. L'auto-update du launcher Windows est désactivé
  pour éviter de remplacer le paquet Linux par un EXE Windows. Les prochains
  paquets Linux s'installent manuellement pour le moment.
- Raccourci dans le menu des applications, pointant sur le dossier extrait.
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
