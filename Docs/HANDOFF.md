# Pokescrap — Passation de session

Dernière mise à jour : 24/10/2026, 2e session (**composition du quart d'île : falaises, hauteur, végétation ; voir la section ci-dessous**). À lire en début de session avec `CLAUDE.md` (détails techniques), `Docs/GDD.md` (décisions de conception) et `Docs/ROADMAP.md` (jalons).

## Méthode de travail avec Owen

- Owen écrit en français ; réponses courtes, économie de tokens (voir `CLAUDE.md`).
- **Avant toute nouvelle tâche, présenter un plan détaillé et attendre son accord** (Pokémon, attaques, valeurs, comportements, ce qui est provisoire, questions ouvertes). Ne pas coder dans le même tour. Signaler explicitement tout écart au plan validé.
- Il aime être contredit quand c'est justifié : donner un avis argumenté, pas seulement exécuter.
- Après chaque tâche : tester dans Unity (unityMCP), mettre à jour GDD / ROADMAP / CLAUDE.md, committer ; dépôt public GitHub sans contenu de la ROM (voir `README.md` et `.gitignore`). Une branche `feature/<nom>` par tâche, depuis `dev` à jour (convention de Robin, l'autre développeur ; pseudo GitHub WinstonBlewUp), poussée sur `origin`, fusionnée dans `dev` une fois validée en jeu par Owen. Ne pas toucher à `main`.
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

- **Jouables** : 6 débloqués au départ (`meta.json`) : Bulbizarre, Salamèche, Carapuce, Pichu (à la place de Pikachu, qui est une évolution), Goinfrex, Riolu. Les 6 ennemis sont recrutables (taux de la ROM de la première forme jouable de la lignée) et deviennent jouables. Les 519 Pokémon de la ROM ont des sprites (492 espèces + 28 Zarbi, formes de base seulement ; exceptions à revoir : `Docs/POKEMON_EXCEPTIONS.md`). Seuls Abra, Métamorph et Queulorior n'ont aucune attaque au choix (Téléport, Morphing, Gribouille) et ne sont pas jouables. Les ennemis restent ceux des `stages` (tirage parmi tous les Pokémon à décider plus tard).
- **Menu** (scène `Menu`, lancée par Play) : choix de 3 Pokémon, Triangle pour choisir leurs 2 attaques de départ (attaques de base des types du Pokémon comprises) dans leur liste par niveau ; sauvegarde des débloqués, de la dernière équipe et des attaques choisies.
- **Attaques** : 343 attaques génériques (`generate_moves.py`, ROM + PokéAPI, règles dans `Tools/pmd/move_rules.json`) + 28 faites à la main (prioritaires) ; 108 écartées, listées dans `Tools/pmd/generated_moves_report.txt`. Recharge croissante avec la puissance, annonce plus longue pour les attaques à charge, recharge ×1,5 pour celles à recharge, recul.
- **Statuts** (`statuses.json`) : poison, paralysie, brûlure, sommeil, gel, confusion, piège, bâillement, vampigraine ; durées réduites pour l'équipe du joueur (sommeil, gel) et pour les boss.
- **En partie** : tous les 10 niveaux, une des 3 cartes est une nouvelle attaque (toujours sous 4 attaques, puis 1 fois sur 2, en remplacement) ; évolution au niveau de la ROM (seulement par niveau et si les sprites existent) ; rôle d'IA déduit des attaques équipées. Ennemis : une attaque de base de leur style (Attaque / Attaque Spé) plus une attaque de leur liste. Boss Groudon inchangé (4 attaques fixes, bulle numérotée, résistances).
- **Étapes** : décor 1 (cristaux roses, ennemis niv. 1), décor 20 (sable, niv. 5), décor 12 (volcan, boss niv. 10).
- **Commandes clavier** (19/10/2026) : ZQSD / flèches déplacement, U / I / O / P attaques 1 à 4 (mêmes touches en AZERTY et QWERTY ; un bouton de souris maintenu vise le curseur), Ctrl gauche esquive, Tab changer de Pokémon ; au menu des attaques, ← / → changent le filtre de type. Le HUD affiche U I O P sans manette branchée.
- **Commandes manette** : stick gauche déplacement, stick droit visée, R1/R2/L1/L2 attaques, croix gauche/droite changer de Pokémon ; écran d'amélioration : croix ou stick + ✕, ○ pour revenir de l'écran de remplacement ; fin de partie : ✕ rejouer, ○ menu.

## Session en cours : composition du quart d'île (branche `feature/quart-ile`, 23-24/10/2026)

Branche partie de `feature/salles` (Robin : salles, île flottante ; Owen : zones séparées), **ni l'une ni l'autre
fusionnées dans `dev`**. But : composer un quartier de l'île principale avec les assets de DM. Salle de test : escalier
turquoise du terrain de test (salle 5) ; R / Select : nouveau tirage. Code : `Assets/Scripts/Dungeon/IslandQuarterTest.cs`
(génération), `IslandPlan.cs` (données par case), `DungeonGenerator.GenerateIsland` / `Draw` (rendu), `IslandRun.cs`
(décor, marqueurs, mini-carte) ; réglages `Assets/Resources/Data/quarter_test.json`. Détails techniques dans `CLAUDE.md`
(paragraphes « Quart d'île de test », « Cartes de l'histoire », « Éléments de décor »).

Fait et validé par Owen : quart en éventail sans murs, rivière large et sinueuse (pointe = centre de l'île → côte),
jungle (décor 44) et prairie fleurie (décor 5) de part et d'autre, 3 ponts ; route principale (terre du décor 35 +
pierres clairsemées du décor 30) entre deux portails (marqueurs inertes) par le pont du milieu, colonnes brisées
(obstacle `column`, rocher du décor 46) ; relief vu de 3/4 : paroi sud de 2 cases par palier, rebord fin ailleurs,
escaliers de 3 colonnes, montagne en lacets, rebords à sens unique (validés), grotte au sommet sous un toit qui
s'efface (validée, sol du décor 9).

Repris et validé par Owen le 24/10 (2e session) :
1. **Falaises** : plateaux aux contours droits (rectangles sur une grille de 3 cases, encoches bouchées) ; morceaux
   refaits dans `cliff_styles.json` : prairie = rebord de la carte 224 + bas de paroi pris dans la carte 226 (zone orange
   sans pli rose : les plis sont des angles, les enchaîner donnait « une suite de coins »), roche = carte 23 (paroi
   régulière, entrée de grotte) au lieu de la 26 (gros blocs qui ne se découpent pas) ; colonnes posées dans l'ordre puis
   en miroir (sans couture) ; sol de la carte effacé en haut des morceaux, bas de paroi en vague, ombre portée au pied
   (remplace les cases assombries `footShadow`) ; dos, côtés et coins fabriqués à partir de la bande du rebord, extrémités
   de paroi avec rebord et coin du bas arrondi seulement contre un terrain plus bas ; plus de bordure du décor autour des
   falaises ; escaliers sans bande de terre ; toit de la grotte jusqu'à la roche.
2. **Collisions et hauteur** : collisions à l'épaisseur dessinée des rebords ; les tirs d'un palier passent les
   falaises de ce palier ou plus basses, s'écrasent sur les plus hautes ; IA idem (`ShotClear`) ; scénario de banc
   « Hauteur : … » (passe).
3. **Décor** : cases sombres sous le décor corrigées (`blocker` caché n'est plus peint) ; retirés : buissons 877, 879,
   905, haie `grass_909` ; décor épars réduit (fleurs, buissons, rochers) ; taches par sous-biome : champs de fleurs,
   fougeraies, éboulis, forêts en massif plein avec sentiers et clairière (`IslandPlan.clearings` pour le level design).

Reste à faire :
- Arbres transparents quand un Pokémon passe derrière (on marche sous les couronnes en lisière) ; plus de variété
  d'arbres (couche du dessous des cartes, à détourer).
- La roche n'a pas d'escaliers à elle (elle emprunte ceux de la prairie, roses) : en chercher dans la carte 24.
- Banc d'essai lancé dans l'éditeur (24/10) : 23/25, les deux échecs sont des pires images à ~100 ms (« Niveau : pas de
  gel… », « Île : … ») alors que l'éditeur tournait sans le focus ; à relancer en batch (`setup.py --ai-scenarios`,
  Unity fermé) pour confirmer que ce n'est pas une régression.
- Les IA ne cherchent pas encore les hauteurs ; idées de level design autour des clairières.
- Attention : un rendu de contrôle par `execute_code` dans l'éditeur laisse des objets dans la scène ouverte (`Obstacles`,
  « Tuiles d'obstacles » à la racine) qui reviennent en jeu : recharger `SampleScene` sans enregistrer après coup, et
  jamais pendant que Owen est en Play.

Abandonné : la refonte visuelle organique (branche `feature/refonte-visuelle`, non fusionnée, gardée pour mémoire :
shader de sol, vitrine, assets Claude Design) — Owen préfère les assets de DM en cases carrées.

## Prochaine session : attaques au cas par cas

Fait (15-17/10/2026) : les 34 **attaques de base** (17 à distance, 17 au corps à corps), réglées une par une avec Owen (détail dans le GDD, paramètres dans `CLAUDE.md`), avec leurs planches Claude Design (`Design/effects_v6_attaques_de_base/`, `Design/effects_v7_attaques_de_base_cac/`, prompts `Design/prompt_attaques_de_base*.md`) et les icônes des 9 statuts (`Design/effects_v5_statuts/`). Owen n'a pas encore tout testé à la manette. Ensuite : les 18 attaques de type (liste et lots dans le GDD) ; lot 1 fait le 19/10/2026 (Vibraqua, Lame d'Air, Plaie-Croix, Onde Folie, Telluriforce), planches demandées (`Design/prompt_attaques_type_lot1.md`). Points de départ :
- `Tools/pmd/generated_moves_report.txt` : 108 attaques écartées (75 de statut sans effet transposable comme Abri, Repos, Morphing, Métronome ; terrain, météo, K.O. en un coup, expulsion ; statuts non gérés comme Attraction, Requiem, Entrave).
- Les attaques génériques n'ont pas d'effet visuel propre (disque coloré du type) : prévoir des prompts Claude Design par famille ou par attaque.
- Une attaque se reprend à la main en l'ajoutant à `realtime_moves.json` (même `id`), qui l'emporte sur la version générée.
- Notes du générateur sur chaque attaque (`notes` : « plusieurs coups », « charge », « recharge », « une seule statistique gardée ») : pistes de reprise.
- Champs ignorés pour l'instant : chance de recul critique, coups multiples en mêlée (puissance ×2 à la place), bonus sur plusieurs statistiques.

## Effets de terrain (19/10/2026)

Faits et testés en Play par Robin : 7 terrains, 14 attaques, persos de dev, commandes clavier U I O P. Suite décidée : affiner la logique et le fonctionnement, puis les affinités entre terrains (table `reactions`). Tuiles animées des 8 terrains livrées (19/10/2026, `Design/effects_v11_terrains/`, prompt `Design/prompt_terrains.md`). Reste, reporté par Robin (19/10/2026) : planches des 14 attaques à trace qui n'ont qu'un disque coloré (Lance-Flamme, Déflagration, Hydrocanon, Hydroblast, Canon Graine, Mégafouet, Végé-Attak, Tonnerre, Fatal-Foudre, Laser Glace, Blizzard, Détritus, Bomb-Beurk, Boue-Bombe) ; prompt prêt : `Design/prompt_attaques_terrain.md`. Ces attaques sont générées : leur attribuer les planches via `move_rules.json` plutôt qu'en les recopiant dans `realtime_moves.json`.

## Méga-évolutions (20/10/2026, Robin) : en pause

Étape 1 faite (branche `feature/mega-evolutions`) : sprites de 27 mégas (SpriteCollab), galerie du terrain de test,
jauge commune, L3 + R3 / F, stats et types, onde de choc, retour au bout de 50 s ; testée en jeu par Robin, réservée
au `devMode`. Mise en pause par Robin pour passer aux IA. Fusionnée dans `dev` le 22/10/2026. Suite (GDD) : 2) méga-gemmes (sauvegarde, emplacement au menu
d'équipe, obtention en partie, sans doublon) ; 3) les 27 passifs. Plus tard : boss qui méga-évoluent.

## Correction des IA (branche `feature/ia`, fusionnée dans `dev` le 22/10/2026, Robin)

- Alliés qui suivaient le Pokémon joué jusque dans une zone nocive : ils visent la case sûre la plus proche de lui
  (`AllyAI.SafeSpotNear`) par un chemin qui contourne les dangers (`Navigator.Direction(..., avoid: true)`), et
  n'attendent au bord que s'il n'en existe aucun.
- Lot A (GDD « IA ») fait et testé : `TargetChoice` (cible par score, alliés et groupes ennemis),
  alliés qui suivent le Pokémon joué quand il recule, plus offensifs, prudents quand ils sont blessés. Suite : lot B
  (placement et combos selon les types), lot C (couper et réparer les chemins).
- Banc d'essai des IA (`AiScenarios`, touche B dans le terrain de test, `python3 Tools/setup.py --ai-scenarios`) :
  8/8 en batch le 20/10/2026. Il a révélé que l'allié restait coincé au coin d'une bande de danger (coin coupé en
  diagonale, ligne droite autorisée depuis le danger) : corrigé dans `Navigator` (mode `avoid`).
- Lot C commencé (20/10/2026) : réparer un chemin (geler, nénuphars, lianes, lave refroidie), brûler un pont sous un
  ennemi au corps à corps ; pylônes vus avec 60 % de chances par cycle ; métal ignoré par les IA. `Navigator` ne
  relance plus l'A* à chaque pas vers une cible inaccessible (problème connu, corrigé). Banc : 14 scénarios.

## Salles (branche `feature/salles`, 22/10/2026, Robin)

Étape 1 faite : catalogue `rooms.json` (types, thèmes, variantes, tailles), générateur (plans de murs, décors de la ROM,
liquide du décor), galerie des salles dans le terrain de test, `RoomCheck` (84 salles, 0 problème). Le point de départ
est encore le centre (spawnClear réduit à 2 dans les salles) : les portes le remplaceront à l'étape 2.
Retour de Robin (22/10/2026) : les obstacles des salles « ne marchaient pas ». Diagnostic (banc + captures `RoomShots`) :
la mécanique marchait, mais le liquide de certains décors trompait (eau violette, sol d'eau, glace = sol) et des salles
sortaient vides ; corrigé (aspect standard, décors écartés, nouvel essai). Banc : 22/22.
Étage façon Isaac fait puis remplacé (22/10/2026, demande de Robin) par des **niveaux continus** façon Minecraft
Dungeons : phase 1 faite (`LevelPlan`, `LevelRun`, zones naturelles, chemins sinueux et renfoncements, branches,
boucles, mini-carte avec brouillard ; l'escalier orange mène désormais à l'île) ; `LevelCheck` 20 niveaux sans problème,
exploration ≈ 2 à 4,5 min sans combat ; banc 22/22. Suite (GDD) : 2) sous-biomes (`biomes.json`, décor par case,
transitions sur frontières naturelles), 3) passages secrets, 4) rencontres. Puis brancher les niveaux sur la vraie partie.
**Nouvelle direction (22/10/2026)** : une run = une île flottante (GDD « L'île flottante ») ; les niveaux continus
serviront aux chaînes d'îlots. Étapes 1 et 2 faites (données seulement) : `IslandPlan`, `IslandFeatures`, `island.json`,
`biomes.json`, aperçu `IslandCheck`. Étape 3 faite : île jouable (escalier orange du terrain de test), décor par case,
haies aux changements de décor, ciel autour ; pire image 24 ms en courant avec les alliés (les pics de 108 ms venaient de
la réparation de chemin, rendue locale). Banc 24/24. Suite : 4) chaînes d'îlots, 5) sceaux et engrenages, 6) centre,
7) rencontres.
**Composition d'un quartier (23/10/2026, Owen, `feature/quart-ile`)** : salle de test d'un quart d'île « nature
luxuriante » (escalier turquoise) : grande zone sans murs, rivière large et sinueuse de la pointe (centre de l'île) à
la côte, jungle (décor 44) d'un côté, prairie fleurie (décor 5) de l'autre, 3 ponts. Point de départ pour composer les
quartiers de l'île principale.
**Zones séparées (22/10/2026, Owen)** : chaque quartier et le centre sont des cartes à part, murées, avec écran de
chargement aux passages (dans les deux sens ; dernier quartier → centre). `IslandCheck.Run` : 4 îles, zones toutes
accessibles depuis leur arrivée ; construction d'un quartier ≈ 0,9 s. Limite : une zone revisitée est reconstruite
telle qu'à l'origine (pont brûlé, rivière gelée non gardés).
Gels des grands niveaux corrigés par une session parallèle (pire image 167 → 31 ms ; banc 23/23), à confirmer en jeu.
Retour de Robin sur l'étage de test : plus d'obstacles (pylônes dans les labyrinthes…) et obstacles fondus dans le décor
(capture : vide standard bordé de bleu sur la Grotte Abîme aux murs noirs). Fait : liquide du décor par thème
(`liquidDecors`), rochers découpés dans chaque décor, décor 26 écarté, plus d'éléments par variante.
Décors de la ROM utiles, par donjon d'origine : lave 57/58 (Cratère Obscur), 110 (Caverne Brasier), 123 (Volcan Géant) ;
glace 88, 111, 137, 139, 140 ; eau 4, 39, 52, 96, 113, 126 ; forêt 5, 10, 35, 44, 128 ; caverne 1, 9, 22, 49, 103, 130 ;
désert 17, 19, 20, 85, 133 ; électrique 14 (Plaines Elek), 127 (Registeel) ; néant 26, 55 (Faille Spatiale, liquide
noir), 59, 99 ; ruines 28, 29, 30, 45/46 (Tour du Temps) ; labyrinthe 102 (Grotte Labyrinthe).

## Objets spéciaux (branche `feature/objets`, 23/10/2026, Robin)

Faits : `items.json`, collection et équipement dans la sauvegarde, encoches et écran des objets au menu, ultimes (méga
avec gemme, capacité Z, Téracristal) sur la jauge commune, butin (boss, coffre de fin d'étape, élites). Banc 23/23 (5
scénarios d'objets, 4 d'élites et d'ultimes ennemis). Ultimes ennemis (`EnemyUltimate`) et élites qui modifient le terrain : faits. À faire : tester en jeu ; icônes d'objets faites (`import_items.py`, PokeAPI + Nikostera) ; effets Z et Téra à chercher en ligne
plutôt que chez Claude Design (demande de Robin) ; vérifier les noms français des objets ; observé sur le banc : une
attaque de projectile touche un ennemi immobile du banc à de nombreuses reprises (1 035 dégâts pour ≈ 33 estimés par
coup), à vérifier en jeu.

## En attente

- **Icônes de bonus/malus** (`status_attack_up`… six fichiers) : prompt donné plus tôt, archive pas encore reçue ; flèches colorées provisoires.
- **Évolution manuelle** (17/10/2026) : faite et testée par code (niveau, carte Pierre Givre d'Évoli, Ningale → Ninjask + Munja, onde de choc, nuit à l'étape 2). Pas testés : Triangle à la manette, échange, Babimanta, Barpau, Debugant, menu avec Rond et équipe de 1 ou 2. Le voile de nuit est discret (alpha 0,3) ; le gris « sans effet » des chiffres de dégâts se lit mal.
- Archives qui ne servent plus (Owen peut les supprimer) : `Jeu d'action 2D pixel art*.zip`, `Groudon.zip`, `Statuts.zip` à la racine ; `Design/Attaquesbasedistance.zip`, `Design/CaC.zip`, `Design/Evolution.zip`, `Design/Jeu d'action 2D pixel art.zip`. Garder l'archive de la ROM.

## Problèmes connus et points à surveiller

- Corrigé le 22/10/2026 : gels sur les grands niveaux quand le Pokémon joué survolait l'eau, la lave ou le vide (Ptéra) :
  les alliés, sans chemin, lançaient l'A* jusqu'à sa limite puis la réparation de chemin (Dijkstra quadratique sur
  toute la carte, toutes les 0,5 s). Correctifs : cible de suivi praticable, chemin partiel à la limite de l'A*,
  réparation à tas binaire bornée, redessin incrémental des obstacles et du terrain. Sonde au banc (« Niveau : pas de
  gel… ») : pire image 167 ms → 31 ms sur un niveau 360×268. Reste à confirmer en jeu par Robin.

- Analyse du code (17/10/2026), corrigé : gardes du boss qui rapportaient expérience et recrues, ✕ / Espace qui esquivaient ou attaquaient en fermant un menu (entrées ignorées 0,25 s), ennemi seul forcé en soutien, écran de fin qui se fermait aussitôt (1 s avant d'accepter une touche), recul ignoré pendant l'annonce d'un ennemi, attaque possible en pleine ruée ou esquive. Restent, mineurs : les zones ne déclenchent ni drain ni recul ; tuiles non détruites à chaque `Generate()` ; Attaque / Attaque Spé proposées même sans attaque de cette catégorie ; animation de marche pendant un lancement ou sous sommeil (IA).

- **Équilibrage entièrement provisoire**, encore plus depuis le système d'attaques : 2 attaques au départ, ennemis à attaques tirées au hasard, recharges génériques (`move_rules.json`). Le soutien devient rare : il faut un soin ou un bonus aux alliés (Bulbizarre n'est soutien qu'avec Synthèse). L'équipe progresse trop vite (niveau 23-25 à la fin de l'arène 2 dans mes tests, mais ces tests tuaient les vagues par code) et écrase Groudon niveau 10. Rythme d'expérience (20 + 4 × niveau) sans lien avec l'objectif « niveau 100 en 30 min ».
- Un soutien ennemi peut être rattrapé pendant sa pause après attaque.
- **IA à corriger (prochaine branche, Robin, 20/10/2026)** : un allié qui suit le Pokémon joué ne recalcule pas de
  chemin sûr pour le rejoindre ; il s'arrête au bord du danger (`AllyAI.SafeSpotNear`, en cours sur la branche des IA).
- Corrigé le 20/10/2026 : le terrain se dessinait au même ordre que les Pokémon (ils passaient dessous et clignotaient) ;
  sol descendu à -6 (`DungeonGenerator.FloorOrder`).
- `meta.json` a `"devMode": true` (persos de dev en tête du menu, 19/10/2026) : le repasser à `false` pour une version de jeu.
- `meta.json` a `"unlockAll": true` pour les tests d'Owen : le repasser à `false` pour retrouver les vrais déblocages.
- L'annonce des attaques au corps à corps ennemies reste un disque rouge, qui ne correspond pas aux arcs ni aux estocs.
- Jamais testés à la manette : navigation du menu (grille et écran des attaques), écran de remplacement d'attaque, Croix/Rond des écrans de fin. Le symbole ✕ de la ligne d'aide de l'écran d'amélioration semble mal rendu par la police.
- Pas testés en situation réelle : recul, bonus au lanceur à l'impact, soin de Vampigraine générique, comportement des IA sous statut.
- Jamais observés en jeu : clignotement des icônes en fin d'effet, navigation manette dans l'écran d'amélioration, effets animés de Groudon (seulement les fichiers), alliés au repos qui se répartissent.
- La traînée de Vive-Attaque et l'onde de Séisme sont fines : à vérifier pour la lisibilité.
- Unity s'est bloqué une fois pendant un rechargement de scripts (« The file 'MemoryStream' is corrupted ») ; Owen a dû le relancer. Après une recompilation, la liaison unityMCP peut être absente une dizaine de secondes : vérifier `Logs/Editor.log` (ciblé) avant de conclure à un blocage.
- Machine d'Owen (Linux) : la manette est une Battletron compatible PS4 en Bluetooth (le câble USB coupait). Si Linux la refuse (« Rejected connection from !bonded device » dans `journalctl -u bluetooth`), refaire l'appairage avec `bluetoothctl` jusqu'à « Bonded: yes ». Ne pas toucher à `ClassicBondedOnly` sans accord explicite.

## Comment je teste (rappels utiles)

- Play lance la scène `Menu` ; en test, `Run.Start(new[]{4,1,25})` lance une partie avec cette équipe, `Run.ToMenu()` revient au menu. Pour tester l'écran d'amélioration : `Progression.AddXp(5000)` ; les champs privés `options`, `pending`, `replacing` se lisent par réflexion, `Choose(i)` et `Replace(slot)` sont publics.
- Statuts : `combatant.AddStatus("sleep", 3f)` ; la confusion se vérifie par `Steer(Vector2.right)`.
- Captures : `manage_camera` avec `output_folder` = `Temp/Captures` (pas de dossier `Assets/Screenshots`).
- Sauvegarde : `save.json` dans `Application.persistentDataPath` (Linux `~/.config/unity3d/DefaultCompany/Pokescrap/`, macOS `~/Library/Application Support/DefaultCompany/Pokescrap/`, Windows `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Pokescrap\`) ; menu **Pokescrap > Effacer la sauvegarde** (la remettre à zéro après les tests). Pour tester le recrutement : `SaveData.Settings.recruitMultiplier = 100` en mode Play. Lancer `SampleScene` directement donne une équipe au hasard parmi les débloqués ; `Run.ToMenu()` / `Run.Start(ids)` changent de scène.
- Tester une attaque par code : désactiver `EnemyAI` et `AllyAI`, et ne pas utiliser le Pokémon contrôlé (sa `LiveAim` vient de la manette et remplace la direction donnée) ; la fenêtre en arrière-plan est lente, prévoir plusieurs lectures espacées.
- `execute_code` en mode Play ; neutraliser l'écran d'amélioration avec `Progression.choiceInterval = 0` et les vagues avec `EnemySpawner.delayBetweenWaves = 600`.
- Aller directement au boss : invoquer deux fois la méthode privée `EnemySpawner.NextStage` par réflexion après avoir détruit les ennemis.
- Pour mesurer une mécanique, figer les autres acteurs (désactiver `AllyAI`, `EnemyAI`, `BossAI`) : les alliés tuent sinon les cibles de test, et le poison fausse les PV.
- Les captures complètes sont en 1920×879 : recadrer avec PIL dans le scratchpad plutôt que d'agrandir la capture.
- Le temps de jeu est figé quand l'écran d'amélioration est ouvert (`Time.timeScale = 0`) : vérifier `Time.time` entre deux relevés.
