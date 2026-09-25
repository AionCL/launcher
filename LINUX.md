# Launcher Linux — 2.5.42-linux-preview.5

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
`out/AionCL-Launcher-2.5.42-linux-preview.5.tar.gz` et son SHA-256.
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

Preview.5 corrige les chemins Windows sur un disque Linux sensible à la casse
(`L10N`/`l10n`, y compris les voix et les dégâts). Les contrôles SHA-256 utilisent
OpenSSL 3 lorsque disponible, avec repli géré si absent. L'extraction emploie
jusqu'à quatre workers ; les packages qui remplacent les mêmes chemins restent
ordonnés. Aucun contrôle d'intégrité n'est supprimé.
Référence EVP : https://docs.openssl.org/3.0/man3/EVP_DigestInit/
