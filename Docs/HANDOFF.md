# Pokescrap — Passation de session

Dernière mise à jour : 15/10/2026 (jalon 4 minimal et système d'attaques génériques faits ; **prochaine session : travail détaillé des attaques, au cas par cas, avec Owen**). À lire en début de session avec `CLAUDE.md` (détails techniques), `Docs/GDD.md` (décisions de conception) et `Docs/ROADMAP.md` (jalons).

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
| 5 Tous les Pokémon jouables et ennemis (génération automatique des attaques) | en cours : attaques génériques, statuts, évolution en partie faits |
| 6 Coopération, son, finition | à faire, en dernier (décidé) |

## Contenu actuel

- **Jouables** : 6 débloqués au départ (`meta.json`) : Bulbizarre, Salamèche, Carapuce, Pikachu, Goinfrex, Riolu. Les 6 ennemis sont recrutables (taux de la ROM de la première forme jouable de la lignée) et deviennent jouables. 27 Pokémon ont des sprites, dont 14 formes évoluées importées pour l'évolution en partie (Herbizarre, Florizarre, Reptincel, Dracaufeu, Carabaffe, Tortank, Rattatac, Machopeur, Smogogo, Floravol, Cotovol, Kirlia, Gardevoir, Oniglali).
- **Menu** (scène `Menu`, lancée par Play) : choix de 3 Pokémon, Triangle pour choisir leur attaque de départ (en plus de l'attaque de base, sur R1) dans leur liste par niveau ; sauvegarde des débloqués, de la dernière équipe et des attaques choisies.
- **Attaques** : 343 attaques génériques (`generate_moves.py`, ROM + PokéAPI, règles dans `Tools/pmd/move_rules.json`) + 28 faites à la main (prioritaires) ; 108 écartées, listées dans `Tools/pmd/generated_moves_report.txt`. Recharge croissante avec la puissance, annonce plus longue pour les attaques à charge, recharge ×1,5 pour celles à recharge, recul.
- **Statuts** (`statuses.json`) : poison, paralysie, brûlure, sommeil, gel, confusion, piège, bâillement, vampigraine ; durées réduites pour l'équipe du joueur (sommeil, gel) et pour les boss.
- **En partie** : tous les 10 niveaux, une des 3 cartes est une nouvelle attaque (toujours sous 4 attaques, puis 1 fois sur 2, en remplacement) ; évolution au niveau de la ROM (seulement par niveau et si les sprites existent) ; rôle d'IA déduit des attaques équipées. Ennemis : 2 attaques tirées au hasard dans leur liste (dont une offensive). Boss Groudon inchangé (4 attaques fixes, bulle numérotée, résistances).
- **Étapes** : décor 1 (cristaux roses, ennemis niv. 1), décor 20 (sable, niv. 5), décor 12 (volcan, boss niv. 10).
- **Commandes manette** : stick gauche déplacement, stick droit visée, R1/R2/L1/L2 attaques, croix gauche/droite changer de Pokémon ; écran d'amélioration : croix ou stick + ✕, ○ pour revenir de l'écran de remplacement ; fin de partie : ✕ rejouer, ○ menu.

## Prochaine session : attaques au cas par cas

Owen veut travailler les attaques en détail. Fait : les 17 **attaques de base** à distance (une par type, `basic: true`, `basicByType` dans `realtime_moves.json`), avec trajectoires et paramètres propres (`Projectile`). Prompt Claude Design envoyé ou à envoyer : `Design/prompt_attaques_de_base.md` (planches à brancher à leur arrivée). À faire ensuite : les attaques de base au corps à corps (chaque Pokémon aura l'une ou l'autre, via `basicByPokemon`), régler les dégâts (`damageScale`) en jeu. Points de départ :
- `Tools/pmd/generated_moves_report.txt` : 108 attaques écartées (75 de statut sans effet transposable comme Abri, Repos, Morphing, Métronome ; terrain, météo, K.O. en un coup, expulsion ; statuts non gérés comme Attraction, Requiem, Entrave).
- Les attaques génériques n'ont pas d'effet visuel propre (disque coloré du type) : prévoir des prompts Claude Design par famille ou par attaque.
- Une attaque se reprend à la main en l'ajoutant à `realtime_moves.json` (même `id`), qui l'emporte sur la version générée.
- Notes du générateur sur chaque attaque (`notes` : « plusieurs coups », « charge », « recharge », « une seule statistique gardée ») : pistes de reprise.
- Champs ignorés pour l'instant : chance de recul critique, coups multiples en mêlée (puissance ×2 à la place), bonus sur plusieurs statistiques.

## En attente

- **Icônes de statut** (`status_burn`, `sleep`, `freeze`, `confusion`, `trap`, `yawn`, `leech-seed`, 12 × 12 px) : prompt Claude Design donné le 15/10/2026, archive pas encore reçue ; gouttes colorées provisoires.
- **Icônes de bonus/malus** (`status_attack_up`… six fichiers) : prompt donné plus tôt, archive pas encore reçue ; flèches colorées provisoires.
- **Évolutions sans niveau** (pierre, échange, bonheur, bébés comme Pichu) : pas décidé par Owen. En attendant, on remonte la lignée jusqu'à la première forme jouable (Raichu → Pikachu).
- Archives à la racine qui ne servent plus (Owen peut les supprimer) : `Jeu d'action 2D pixel art*.zip`, `Groudon.zip`. Garder l'archive de la ROM.

## Problèmes connus et points à surveiller

- **Équilibrage entièrement provisoire**, encore plus depuis le système d'attaques : 2 attaques au départ, ennemis à attaques tirées au hasard, recharges génériques (`move_rules.json`). Le soutien devient rare : il faut un soin ou un bonus aux alliés (Bulbizarre n'est soutien qu'avec Synthèse). L'équipe progresse trop vite (niveau 23-25 à la fin de l'arène 2 dans mes tests, mais ces tests tuaient les vagues par code) et écrase Groudon niveau 10. Rythme d'expérience (20 + 4 × niveau) sans lien avec l'objectif « niveau 100 en 30 min ».
- Les alliés n'esquivent pas les attaques annoncées. Un soutien ennemi peut être rattrapé pendant sa pause après attaque.
- Jamais testés à la manette : navigation du menu (grille et écran des attaques), écran de remplacement d'attaque, Croix/Rond des écrans de fin. Le symbole ✕ de la ligne d'aide de l'écran d'amélioration semble mal rendu par la police.
- Pas testés en situation réelle : recul, bonus au lanceur à l'impact, soin de Vampigraine générique, comportement des IA sous statut.
- Jamais observés en jeu : clignotement des icônes en fin d'effet, navigation manette dans l'écran d'amélioration, effets animés de Groudon (seulement les fichiers), alliés au repos qui se répartissent.
- La traînée de Vive-Attaque et l'onde de Séisme sont fines : à vérifier pour la lisibilité.
- Unity s'est bloqué une fois pendant un rechargement de scripts (« The file 'MemoryStream' is corrupted ») ; Owen a dû le relancer. Après une recompilation, la liaison unityMCP peut être absente une dizaine de secondes : vérifier `Logs/Editor.log` (ciblé) avant de conclure à un blocage.
- La manette est une Battletron compatible PS4 en Bluetooth (le câble USB coupait). Si Linux la refuse (« Rejected connection from !bonded device » dans `journalctl -u bluetooth`), refaire l'appairage avec `bluetoothctl` jusqu'à « Bonded: yes ». Ne pas toucher à `ClassicBondedOnly` sans accord explicite.

## Comment je teste (rappels utiles)

- Play lance la scène `Menu` ; en test, `Run.Start(new[]{4,1,25})` lance une partie avec cette équipe, `Run.ToMenu()` revient au menu. Pour tester l'écran d'amélioration : `Progression.AddXp(5000)` ; les champs privés `options`, `pending`, `replacing` se lisent par réflexion, `Choose(i)` et `Replace(slot)` sont publics.
- Statuts : `combatant.AddStatus("sleep", 3f)` ; la confusion se vérifie par `Steer(Vector2.right)`.
- Captures : `manage_camera` avec `output_folder` = `Temp/Captures` (pas de dossier `Assets/Screenshots`).
- Sauvegarde : `~/.config/unity3d/DefaultCompany/Pokescrap/save.json` ; menu **Pokescrap > Effacer la sauvegarde** (la remettre à zéro après les tests). Pour tester le recrutement : `SaveData.Settings.recruitMultiplier = 100` en mode Play. Lancer `SampleScene` directement donne une équipe au hasard parmi les débloqués ; `Run.ToMenu()` / `Run.Start(ids)` changent de scène.
- `execute_code` en mode Play ; neutraliser l'écran d'amélioration avec `Progression.choiceInterval = 0` et les vagues avec `EnemySpawner.delayBetweenWaves = 600`.
- Aller directement au boss : invoquer deux fois la méthode privée `EnemySpawner.NextStage` par réflexion après avoir détruit les ennemis.
- Pour mesurer une mécanique, figer les autres acteurs (désactiver `AllyAI`, `EnemyAI`, `BossAI`) : les alliés tuent sinon les cibles de test, et le poison fausse les PV.
- Les captures complètes sont en 1920×879 : recadrer avec PIL dans le scratchpad plutôt que d'agrandir la capture.
- Le temps de jeu est figé quand l'écran d'amélioration est ouvert (`Time.timeScale = 0`) : vérifier `Time.time` entre deux relevés.
