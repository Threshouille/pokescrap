# Pokescrap — Passation de session

Dernière mise à jour : 15/10/2026 (jalon 4 minimal fait ; prochaine étape : approfondissement ou jalon 5, à voir avec Owen). À lire en début de session avec `CLAUDE.md` (détails techniques), `Docs/GDD.md` (décisions de conception) et `Docs/ROADMAP.md` (jalons).

## Méthode de travail avec Owen

- Owen écrit en français ; réponses courtes, économie de tokens (voir `CLAUDE.md`).
- **Avant toute nouvelle tâche, présenter un plan détaillé et attendre son accord** (Pokémon, attaques, valeurs, comportements, ce qui est provisoire, questions ouvertes). Ne pas coder dans le même tour. Signaler explicitement tout écart au plan validé.
- Il aime être contredit quand c'est justifié : donner un avis argumenté, pas seulement exécuter.
- Après chaque tâche : tester dans Unity (unityMCP), mettre à jour GDD / ROADMAP / CLAUDE.md, committer (Git local, **jamais de dépôt distant public** : des assets tirés de la ROM sont versionnés).
- Effets visuels : je rédige un prompt pour Claude Design (contraintes : pixel art strict, 24 px = 1 case, une ligne d'images orientée vers la droite, `effects.json`), Owen dépose l'archive `.zip` à la racine, je l'extrais dans `Design/effects_vN/`, je contrôle (tailles, alpha 0/255, ≤ 6 couleurs), je fusionne dans `Assets/Resources/Effects/`.

## Où en est le projet

Feuille de route « squelette d'abord » : jalons 1 à 4 en version minimale, puis approfondissement.

| Jalon | État |
|---|---|
| 0 Git local | fait |
| 1 Arène avec vagues et victoire | fait (minimal) |
| 2 Expérience, niveaux, choix d'améliorations | fait (minimal) |
| 3 Partie complète : Arène 1 → Arène 2 → Boss, écrans de fin | fait (minimal), boss Groudon complet |
| 4 Entre les parties : choix de l'équipe, recrutement, sauvegarde | fait (minimal) |
| 5 Tous les Pokémon jouables et ennemis (génération automatique des attaques) | à faire |
| 6 Coopération, son, finition | à faire, en dernier (décidé) |

## Contenu actuel

- **Jouables (6 au départ)**, équipe de 3 choisie dans la scène `Menu` : Bulbizarre (soutien), Salamèche, Carapuce, Pikachu (distance), Goinfrex, Riolu (corps à corps). Les 6 ennemis sont recrutables et deviennent jouables.
- **Ennemis (6)** : Rattata, Machoc (corps à corps), Smogo, Stalgamin (distance), Granivol (soigneur), Tarsal (affaiblisseur).
- **Boss** : Groudon, 4 attaques fixes annoncées uniquement par une bulle numérotée 1-4, tirées au hasard, annonces plus courtes sous 50 % PV. Résiste au poison (¼) et à la paralysie (½).
- **Étapes** : décor 1 (cristaux roses, ennemis niv. 1), décor 20 (sable, niv. 5), décor 12 (volcan, boss niv. 10).
- **Mécaniques** : 5 familles d'attaques + `burst` pour les boss, poison, paralysie, bonus/malus temporaires, soin, drain, défense dans les dégâts, réanimation (3 s sur un allié K.O.), expérience commune, 8 améliorations, rôles d'IA (corps à corps / distance 4-6 cases / soutien 5-7 cases), espacement entre IA, contournement des murs et des nuages.
- **Commandes manette** : stick gauche déplacement, stick droit visée (flèche aux pieds), R1/R2/L1/L2 attaques, croix gauche/droite changer de Pokémon, Options pour recommencer ; écran d'amélioration : croix ou stick + ✕.

## En attente

- **Icônes de bonus/malus** (`status_attack_up`… six fichiers) : prompt Claude Design donné, archive pas encore reçue ; flèches colorées provisoires en place.
- Archives à la racine qui ne servent plus (Owen peut les supprimer) : `Jeu d'action 2D pixel art*.zip`, `Groudon.zip`. Garder l'archive de la ROM.

## Problèmes connus et points à surveiller

- **Équilibrage entièrement provisoire.** L'équipe progresse trop vite (niveau 23-25 à la fin de l'arène 2 dans mes tests, mais ces tests tuaient les vagues par code) et écrase Groudon niveau 10. Rythme d'expérience (20 + 4 × niveau) sans lien avec l'objectif « niveau 100 en 30 min ».
- Les alliés n'esquivent pas les attaques annoncées. Un soutien ennemi peut être rattrapé pendant sa pause après attaque.
- Jamais observés en jeu : écran de défaite, clignotement des icônes en fin d'effet, navigation manette dans l'écran d'amélioration, effets animés de Groudon (seulement les fichiers), alliés au repos qui se répartissent.
- La traînée de Vive-Attaque et l'onde de Séisme sont fines : à vérifier pour la lisibilité.
- Unity s'est bloqué une fois pendant un rechargement de scripts (« The file 'MemoryStream' is corrupted ») ; Owen a dû le relancer. Après une recompilation, la liaison unityMCP peut être absente une dizaine de secondes : vérifier `Logs/Editor.log` (ciblé) avant de conclure à un blocage.
- La manette est une Battletron compatible PS4 en Bluetooth (le câble USB coupait). Si Linux la refuse (« Rejected connection from !bonded device » dans `journalctl -u bluetooth`), refaire l'appairage avec `bluetoothctl` jusqu'à « Bonded: yes ». Ne pas toucher à `ClassicBondedOnly` sans accord explicite.

## Comment je teste (rappels utiles)

- Système d'attaques (décidé le 15/10/2026, voir GDD) : étapes 1 (générateur), effets et 2 (attaques de départ au menu) faites ; restent l'étape 3 (nouvelle attaque tous les 10 niveaux, plus de chances pour celles de la forme évoluée), l'étape 4 (rôle de l'IA déduit des attaques), les ennemis à 2 attaques tirées au hasard, et l'intégration des icônes de statut demandées à Claude Design.
- Sauvegarde : `~/.config/unity3d/DefaultCompany/Pokescrap/save.json` ; menu **Pokescrap > Effacer la sauvegarde** (la remettre à zéro après les tests). Pour tester le recrutement : `SaveData.Settings.recruitMultiplier = 100` en mode Play. Lancer `SampleScene` directement donne une équipe au hasard parmi les débloqués ; `Run.ToMenu()` / `Run.Start(ids)` changent de scène.
- `execute_code` en mode Play ; neutraliser l'écran d'amélioration avec `Progression.choiceInterval = 0` et les vagues avec `EnemySpawner.delayBetweenWaves = 600`.
- Aller directement au boss : invoquer deux fois la méthode privée `EnemySpawner.NextStage` par réflexion après avoir détruit les ennemis.
- Pour mesurer une mécanique, figer les autres acteurs (désactiver `AllyAI`, `EnemyAI`, `BossAI`) : les alliés tuent sinon les cibles de test, et le poison fausse les PV.
- Les captures complètes sont en 1920×879 : recadrer avec PIL dans le scratchpad plutôt que d'agrandir la capture.
- Le temps de jeu est figé quand l'écran d'amélioration est ouvert (`Time.timeScale = 0`) : vérifier `Time.time` entre deux relevés.
