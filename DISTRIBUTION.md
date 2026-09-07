# AionCL Launcher

Windows avec .NET Framework 4.8. Extraire tout le ZIP dans un dossier accessible en écriture, puis ouvrir AionCL.Launcher.exe. Choisir un dossier client, INSTALLER, puis JOUER. VÉRIFIER / RÉPARER contrôle les fichiers et réinstalle les packages endommagés. ANNULER permet de reprendre avec INSTALLER ; conserver .aioncl/cache pour réutiliser les téléchargements.

La première utilisation exige Internet et la publication de la configuration serveur. Ne pas distribuer tant que la recette décrite dans RECETTE.md n’est pas validée. Le launcher est portable et non signé ; la signature de distribution reste à organiser.

La configuration serveur provient de https://raw.githubusercontent.com/AionCL/launcher/main/config/server-config.json. Le cache .aioncl/server-config.json est créé automatiquement au premier clic JOUER réussi. En cas de panne distante, un cache valide est réutilisé avec un avertissement. Une maintenance déjà en cache reste bloquante ; une nouvelle maintenance distante ne peut pas être connue hors ligne. Le serveur de jeu reste responsable du contrôle d’accès. Sans cache et sans source accessible, réessayer après rétablissement de la connexion ; aucun fichier manuel n’est demandé.

Les paramètres personnels et fichiers générés absents du manifest ne sont pas supprimés. Conserver les fichiers du launcher ensemble, hors des sous-dossiers du jeu. Une connexion au manifest distant est actuellement nécessaire à chaque ouverture du launcher.
