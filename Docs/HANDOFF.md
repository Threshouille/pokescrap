# Pokescrap — Passation de session

Dernière mise à jour : 25/10/2026 (Robin, branche `refacto/socle`). À lire en début de session avec `CLAUDE.md` (détails
techniques), `Docs/GDD.md` (décisions de conception) et `Docs/ROADMAP.md` (jalons). Bilan complet du 25/10 : avancement,
branches, axes de refonte et questions de conception (page partagée par Robin).

## Méthode de travail

- Owen et Robin écrivent en français ; réponses courtes, économie de tokens (voir `CLAUDE.md`).
- Avant toute nouvelle tâche, présenter un plan et attendre l'accord (valeurs, comportements, ce qui est provisoire,
  questions ouvertes). Signaler tout écart au plan validé. Donner un avis argumenté, pas seulement exécuter.
- Une branche par tâche **depuis `dev`** (`feature/…`, `fix/…`, `refacto/…`), poussée sur `origin`, fusionnée dans `dev`
  une fois testée en jeu et le banc vert. `main` n'est plus mis à jour depuis le 19/10 (à rattraper depuis `dev`).
- Commits non signés, seulement avec l'accord de la personne ; jamais de force push ; ne pas toucher aux branches de
  l'autre sans lui. Dépôt public : rien de la ROM (vérification avant chaque push, voir `CLAUDE.md`).
- Après chaque tâche : tests (EditMode et banc), mise à jour de GDD / ROADMAP / CLAUDE.md / README, commit.
- Effets visuels : prompt pour Claude Design (pixel art strict, 24 px = 1 case, une ligne d'images orientée vers la
  droite, `effects.json`) ; archive déposée à la racine, extraite dans `Design/effects_vN/`, contrôlée, fusionnée dans
  `Assets/Resources/Effects/`. Effets Z et Téra : ressources existantes en ligne plutôt que Claude Design.

## Où en est le projet (25/10/2026)

| Domaine | État |
|---|---|
| Combat (familles d'attaques, types, statuts, esquive, visée) | solide |
| Environnement (10 terrains, 20 obstacles, pylônes, 4 météos, 2 sports) | solide |
| IA (rôles, cible par score, dangers, réparation de chemin, secours, soins sûrs) | solide |
| Rencontres (lots, rangs, combinaisons, élites, seconde phase à mi-PV) | fait, à régler en vraie partie |
| Objets (méga, Z, Téra ; sac partagé ; objets tenus à tiers ; butin ; interface lisible) | fait |
| Progression en partie (expérience, améliorations, attaques, évolutions) | provisoire |
| Structure de partie | provisoire sur `dev` (Arène 1 → Arène 2 → Boss) ; île, salles et niveaux continus sur les branches d'Owen |
| Entre les parties (menu, recrutement, sauvegarde) | minimal ; ni monnaie ni boutique |
| Son, coopération | pas commencés (jalon 6) |
| Équilibrage | sans cible chiffrée |

## Branches pas encore dans `dev` (25/10/2026)

Toutes ont des conflits avec `dev` (fusion à blanc par `git merge-tree`) ; intégration prévue avec Owen, dans l'ordre :

1. `feature/attaques-type-lot2` (Owen) : 1 conflit (`PlayerController`).
2. `feature/refonte-visuelle` (Owen) : 3 conflits (`PmdSpriteImport`, `TerrainSandbox`, `CLAUDE.md`).
3. `feature/salles` (Owen) : 5 conflits (`AiScenarios`, `TerrainSandbox`, `PlayerController`, `HANDOFF`, `README`).
4. `feature/quart-ile` (Owen, avec une fusion de `dev` par Robin) : 7 conflits (`AiScenarios`, `AllyAI`, `Obstacles`,
   `TerrainSandbox`, `PlayerController`, `CLAUDE.md`, `README`).

Conflits récurrents : numéros des salles du terrain de test, liste des scénarios du banc, `PlayerController`,
journaux de modifications. Après l'intégration : registre des salles par nom, banc découpé par domaine, outils de dev
dans un assembly à part, puis découpage de `Combatant` et `Obstacles`.

## En attente

- Icônes de bonus et malus (`status_attack_up`… six fichiers) : flèches colorées provisoires.
- Planches des 14 attaques à trace qui n'ont qu'un disque coloré (prompt prêt : `Design/prompt_attaques_terrain.md`) ;
  les attribuer via `move_rules.json`.
- Attaques génériques sans effet visuel propre ; 108 attaques écartées (`Tools/pmd/generated_moves_report.txt`).
- Évolution manuelle : pas testés à la manette (Triangle), échange, Babimanta, Barpau, Debugant.
- Archives qui ne servent plus à la racine et dans `Design/` (Owen peut les supprimer) ; garder l'archive de la ROM.

## Problèmes connus et points à surveiller

- Équilibrage entièrement provisoire : rythme d'expérience (20 + 4 × niveau) sans lien avec l'objectif « niveau 100 en
  30 min », soutien rare (il faut un soin ou un bonus aux alliés), équipe qui progresse vite.
- L'annonce des attaques au corps à corps ennemies reste un disque rouge, qui ne correspond pas aux arcs ni aux estocs.
- Mineurs : les zones ne déclenchent ni drain ni recul ; Attaque / Attaque Spé proposées même sans attaque de cette
  catégorie ; animation de marche pendant un lancement ou sous sommeil (IA).
- Jamais testés à la manette : navigation du menu, écran de remplacement d'attaque, Croix / Rond des écrans de fin.
- Observé sur le banc : une attaque de projectile touchait un ennemi immobile de nombreuses fois (à vérifier en jeu).
- Réglages de test : dans `meta_dev.json` (éditeur et builds de développement), plus dans `meta.json`.
- Unity s'est déjà bloqué pendant un rechargement de scripts (« The file 'MemoryStream' is corrupted ») : relancer.
  Après une recompilation, la liaison unityMCP peut manquer une dizaine de secondes : vérifier `Logs/Editor.log`.
- Version d'Unity : `ProjectSettings/ProjectVersion.txt` diffère selon les machines ; ne pas le committer.
- Machine d'Owen (Linux) : manette Battletron compatible PS4 en Bluetooth ; si Linux la refuse (« Rejected connection
  from !bonded device »), refaire l'appairage avec `bluetoothctl` jusqu'à « Bonded: yes ». Ne pas toucher à
  `ClassicBondedOnly` sans accord explicite.

## Comment je teste

- **Tests EditMode** (logique pure et données, quelques secondes) : `python3 Tools/setup.py --edit-tests` (Unity fermé ;
  sinon Window > General > Test Runner, EditMode). Fichiers dans `Assets/Editor/Tests/`.
- **Banc d'essai** (58 scénarios en mode Play, environ 10 min) : `python3 Tools/setup.py --ai-scenarios`, Unity fermé,
  lignes `AISCENARIO` dans `Logs/ai_scenarios.log` ; touche B dans le terrain de test.
- **Compilation rapide hors Unity** : reprendre le fichier `.rsp` d'Unity (`Library/Bee/artifacts/*/Assembly-CSharp.rsp`)
  et le passer au `csc` du SDK .NET livré avec Unity.
- **Terrain de test** (menu, Select / T en `devMode`) : salle 0 (émetteurs de terrain, présentoir des objets), 1
  obstacles et cibles, 2 galerie des mégas, 3 démo des capacités Z, 4 élite, 5 boss, 6 démos de secours, 20
  observation des rencontres (V / ○ : pilote automatique).
- Play lance la scène `Menu` ; `Run.Start(new[]{4,1,25})` lance une partie avec cette équipe, `Run.ToMenu()` revient au
  menu. Écran d'amélioration : `Progression.AddXp(5000)`. Statuts : `combatant.AddStatus("sleep", 3f)`.
- Sauvegarde : `save.json` dans `Application.persistentDataPath` (macOS `~/Library/Application Support/DefaultCompany/
  Pokescrap/`, Linux `~/.config/unity3d/DefaultCompany/Pokescrap/`, Windows `%USERPROFILE%\AppData\LocalLow\
  DefaultCompany\Pokescrap\`) ; menu **Pokescrap > Effacer la sauvegarde**.
- Mesurer une mécanique : figer les autres acteurs (désactiver `AllyAI`, `EnemyAI`, `BossAI`). Le temps est figé
  pendant l'écran d'amélioration (`Time.timeScale = 0`). La fenêtre Unity en arrière-plan est lente.
