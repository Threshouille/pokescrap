# Pokescrap

Roguelike en temps réel, jouable à la manette, fait sous Unity (URP). Il réutilise les sprites et les données
de *Pokémon Donjon Mystère : Explorateurs du Ciel*, sans chercher à reproduire le jeu d'origine.

> Projet de fan, non commercial, sans lien avec Nintendo, The Pokémon Company, Spike Chunsoft ni Game Freak.
> **Ce dépôt ne contient aucun asset du jeu** : chacun les extrait de sa propre cartouche.

Conception : [`Docs/GDD.md`](Docs/GDD.md). État du projet et méthode de travail : [`Docs/HANDOFF.md`](Docs/HANDOFF.md),
[`Docs/ROADMAP.md`](Docs/ROADMAP.md). Notes techniques détaillées : [`CLAUDE.md`](CLAUDE.md).

## Prérequis

- **Unity 6000.6.x** via Unity Hub (version de référence : `ProjectSettings/ProjectVersion.txt`). Un autre correctif
  de la même branche 6000.6 convient ; ne pas committer `ProjectVersion.txt` s'il change seulement parce que ton
  éditeur est plus récent (changer de version se décide à l'équipe).
- **Python 3.10+** (avec `venv` ; sous Ubuntu/Debian : `sudo apt install python3-venv`).
- Git.
- Pour travailler avec Claude Code et unityMCP (facultatif) : [`uv`](https://docs.astral.sh/uv/) (`uvx` doit être dans
  le `PATH` ; macOS : `brew install uv`).
- La ROM européenne de *Pokémon Donjon Mystère : Explorateurs du Ciel* (code `C2SP`), **dumpée depuis ta
  propre cartouche**. Ne jamais la partager ni la committer.

## Installation

```bash
git clone https://github.com/Threshouille/pokescrap.git
cd pokescrap
mkdir Rom
# copier la ROM dans Rom/eos_eu.nds
python3 Tools/setup.py
```

**N'ouvre pas le projet dans Unity avant la fin du script** (voir plus bas pourquoi).

Le script :
1. vérifie la ROM (somme SHA-1) ;
2. crée l'environnement Python `Tools/.venv` (`Tools/requirements.txt`, dont `skytemple-files`) ;
3. dépaquette la ROM dans `Rom/files/` et extrait les données (Pokémon, attaques, niveaux) ;
4. exporte sprites et portraits dans `Rom/export/` (≈ 8 min la première fois) puis copie dans `Assets/`
   ceux du projet ;
5. extrait les décors de donjon ;
6. lance Unity en mode batch (`PmdSpriteImport.BuildEverything` : **Build Tilesets** + **Build Anim Sets**).
   La première importation du projet prend plusieurs minutes. Le script cherche Unity dans les dossiers du Hub
   (Linux, macOS, Windows, et le dossier d'installation choisi dans le Hub) : la version exacte du projet, sinon
   la plus récente de la même branche (6000.6.x). Sinon, passe `--unity <chemin de l'exécutable Unity>` ou
   définis la variable d'environnement `UNITY_PATH`.

Options : `--no-unity` (s'arrêter avant Unity), `--force` (tout refaire), `--unity-only [Classe.Méthode]` (relancer
seulement l'étape Unity, par défaut `PmdSpriteImport.BuildEverything`). Chaque étape déjà faite est sautée,
on peut donc relancer le script sans risque (par exemple après un `git pull` qui ajoute des Pokémon).

Ensuite : ouvrir le projet dans Unity Hub, puis Play (le jeu démarre toujours sur la scène `Menu`).

## Commandes

| Action | Manette (PS4) | Clavier |
|---|---|---|
| Se déplacer | stick gauche | ZQSD (WASD en QWERTY) ou flèches |
| Viser | stick droit | bouton de souris maintenu (sinon cible automatique) |
| Attaques 1 à 4 | R1 / R2 / L1 / L2 | U / I / O / P |
| Esquive | ✕ | Ctrl gauche |
| Changer de Pokémon | croix gauche / droite | Tab |
| Pause | Options | Échap |
| Menu : ajouter / retirer un Pokémon | ✕ / ○ | Espace / Échap |
| Menu : attaques de départ | △ | E |
| Menu des attaques : filtre par type | L1 / R1 ou croix gauche / droite | ← / → |
| Lancer la partie | Options | Entrée |

Le HUD affiche les touches du clavier quand aucune manette n'est branchée.

## Outils de test

Réglages de test dans `Assets/Resources/Data/meta.json` (à repasser à `false` pour une version de jeu) :

- `unlockAll` : tous les Pokémon jouables sont débloqués.
- `devMode` : trois **persos de dev** (Dev Mew, Dev Métamorph, Dev Porygon) apparaissent en tête du menu. Ils
  peuvent prendre n'importe quelle attaque du jeu (`devStartingMoveCount` au départ, 4 par défaut), n'ont pas de
  type et ne sont jamais recrutés, sauvegardés ni ennemis. Définis dans `Assets/Resources/Data/dev_characters.json`.
- `devMode` donne aussi accès au **terrain de test** : au menu, une fois l'équipe choisie, **Select** (manette) ou
  **T** (clavier). Arène sans obstacles où des Pokémon émetteurs (décor, intouchables) entretiennent une zone de chaque
  terrain en anneau (une case neutralisée, par exemple du poison brûlé, le reste jusqu'à la relance de la zone, toutes
  les 15 s), plus deux démonstrations au centre : une flaque frappée par la foudre toutes les 6 s et un champ
  de ronces incendié toutes les 7 s. Pas d'ennemis par défaut (`"enemyWaves": true` pour faire tourner en boucle les
  vagues de la première étape et voir l'IA réagir). Rien n'est recruté ni compté. Disposition et réglages : `Assets/Resources/Data/terrain_sandbox.json`.
- **Banc d'essai des IA** : dans le terrain de test, **B** retire les émetteurs puis joue 14 scénarios (ne pas suivre
  le Pokémon joué dans un danger, contourner une bande de danger, attendre au bord sans chemin sûr, sortir d'un
  terrain nocif, choisir la cible selon les types, finir un K.O., suivre le Pokémon joué qui recule, rester près de
  lui quand on est blessé, geler une rivière, faire pousser des nénuphars, refaire un pont brûlé, refroidir la lave,
  brûler un pont sous un ennemi, marcher sur du métal chargé) et affiche réussi / échec avec les mesures. Ne pas toucher aux commandes pendant le banc.
  Sans ouvrir Unity : `python3 Tools/setup.py --ai-scenarios` (Unity fermé ; résultats dans `Logs/ai_scenarios.log`,
  code de sortie = nombre d'échecs). Scénarios : `Assets/Scripts/Combat/AiScenarios.cs`.

## Obstacles

**En partie**, chaque arène reçoit 1 ou 2 obstacles selon son décor (`Assets/Resources/Data/arena_obstacles.json`) :
cristaux (arène 1) : eau, vide, rochers ; sable (arène 2) : ravins, gouffres, sable mouvant, rochers ; volcan (boss) : lave, rochers, arène
dégagée. Une partie des blocs de mur est remplacée par des rochers cassables ; pylônes et métal y restent rares (ils
sont faits pour des salles à thème). Le point d'arrivée reste dégagé et les ennemis n'apparaissent que sur des cases
sûres reliées à l'équipe. `obstaclesInRuns` dans `meta.json` les coupe. Après une mise à jour du dépôt, relancer
`python3 Tools/setup.py` (ou le menu **Pokescrap > Build Tilesets**) : les décors reçoivent leur numéro de la ROM.

### Salle de test

Depuis le terrain de test, l'**escalier en haut à droite** mène à la **salle des obstacles** (l'escalier en bas à
gauche ramène). Chaque visite tire 2 ou 3 obstacles au hasard (**R** ou **Select** : nouveau tirage) : vide avec
ponts (d'un mur à l'autre), ravin (sur une partie de la salle), gouffre, rivière, rivière glacée, lac, lac de lave,
lac de glace, île entourée de vide ou d'eau, murs de glace, rochers, pylônes électriques, plaques de métal avec bornes
aimantées. Les rivières vont d'un mur à l'autre, les ponts sont
droits ; l'eau et la lave ne se touchent jamais. L'eau, la lave, le vide et la glace
reprennent les décors de la ROM (extraits par `Tools/setup.py` dans `Assets/Resources/ObstacleTiles`, non versionnés) ;
ponts (intact, en feu, brûlé, de lianes), végétation, roche refroidie et murs de glace ont leurs planches Claude Design
(`Design/effects_v12_obstacles/`, prompt `Design/prompt_obstacles.md`).

- On ne marche jamais dedans par erreur : le déplacement s'arrête au bord. Mais une poussée, une glissade ou une ruée
  peuvent y faire **tomber** : retour sur la terre ferme la plus proche et dégâts (eau 10 %, vide 20 %, lave 25 % +
  brûlure), puis 1 s d'invincibilité ; si la chute prend ses derniers PV, le Pokémon est K.O. sur la rive.
- Franchissement : le type de déplacement de la ROM (nage, lave, lévitation, traverse-murs) **ou** le type du Pokémon
  suffit. Eau : nage dans l'eau profonde ; Feu : marche sur la lave ; Vol : survole l'eau, le vide et la lave (3 % des
  PV par seconde et risque de brûlure au-dessus de la lave) ; Spectre : traverse tout, rochers et murs de glace compris,
  seul l'arc des pylônes le touche ; Glace ne glisse pas sur la glace, Plante n'est pas ralenti par la végétation, Sol et
  Roche par les gravats. Ennemis comme alliés. Les tirs passent au-dessus de l'eau, de la lave et du vide, pas des
  solides.
- Interactions, par le type de l'attaque **ou par une trace de terrain posée sur l'obstacle ou juste à côté** :
  Glace gèle l'eau 8 s (un rayon de glace trace un pont) et refroidit la lave ; Plante couvre l'eau de végétation 10 s
  et reconstruit un pont brûlé en lianes ; Électrik électrifie toute l'eau ; Poison la contamine ; Feu fait fondre la
  glace (ceux qui sont dessus tombent), brûle la végétation, fait fondre les murs de glace et **met le feu aux ponts** :
  le feu gagne tout le pont (rouge, bloqué), qui finit brûlé (gris, bloqué) ; Eau refroidit la lave en roche.

- **Rochers** (décors 9 et 18 de la ROM) : solides, brisés par une attaque **Combat** (les gros en 2 coups), qui laisse
  des gravats ; le Combat brise aussi les murs de glace en éclats glissants. Un Pokémon **poussé** par un coup ou qui
  **glisse** sur une surface glissante contre un rocher, un mur de glace, un pylône ou une borne est étourdi et perd
  5 % de ses PV ; marcher dedans, esquiver ou foncer volontairement ne fait rien. Des plaques de glace sont souvent
  posées contre le vide, l'eau ou les rochers pour profiter des glissades.
- **Sable mouvant** : ralentit fortement (×0,4) ; rester plus de 2 s dedans enlise (pris au piège 1,5 s, petits dégâts).
  Vol, Sol, Spectre et les Pokémon qui lévitent ne sont pas concernés ; l'IA l'évite.
- **Pylônes électriques** : par paires, un arc les relie toutes les 4,8 s (avertissement qui crépite, puis arc
  d'1 s) : dégâts, paralysie, étourdissement ; Électrik et Sol immunisés, Vol ×1,5, Spectre touchés. Électrik
  surcharge un pylône (arc continu 4 s), Sol le met à la terre (coupé 6 s). Un arc au-dessus de l'eau ou du métal
  l'électrifie en entier.
- **Métal** : les plaques conduisent l'électricité (toutes les plaques reliées), chauffent au Feu, gèlent à la Glace ;
  un Acier dessus ne peut pas être poussé, résiste mieux (+20 %) et n'est pas électrocuté. Les effets se combinent :
  de l'eau sur du métal ou un sol chargé devient de l'eau électrifiée (et des plaques qu'on électrifie électrifient
  la flaque posée dessus) ; l'eau s'évapore sur le métal brûlant, la glace y fond. Les **bornes aimantées**
  attirent les Acier et happent les tirs Acier et Électrik ; chargées, elles électrifient les plaques qu'elles
  touchent.

Réglages : `Assets/Resources/Data/obstacles.json` (obstacles, chutes, interactions, arc des pylônes) et
`obstacle_room.json` (tirage). Planches des pylônes, de l'arc, du métal et des bornes : `Design/effects_v13_electrique_metal/`
(prompt `Design/prompt_electrique_metal.md`).

## Météos et sports

Six attaques de la ROM jusque-là écartées deviennent jouables (`Assets/Resources/Data/climates.json`) :

| Attaque | Effet (toute l'arène, 20 s, pour les deux camps) |
|---|---|
| Zénith | braises plus grandes et plus longues, flaques et verglas raccourcis ; Feu ×1,3, Eau ×0,7 ; quelques braises éparses |
| Danse Pluie | flaques plus grandes, eau électrifiée plus longue, braises vite éteintes ; Eau ×1,3, Feu ×0,7 ; flaques éparses |
| Grêle | verglas plus grand et plus long ; 0,4 % des PV par seconde sauf Glace ; verglas épars |
| Tempêtesable | boue plus grande et plus longue ; 0,4 % des PV par seconde sauf Roche, Sol, Acier ; boue éparse |

Une seule météo à la fois (la dernière lancée remplace l'autre), signalée par un voile coloré et son nom en haut à
droite ; elle agit aussi sur les traces déjà au sol. Elle change aussi la vitesse et la portée des tirs : sous la pluie,
les tirs Feu vont 30 % moins loin et les tirs Eau un peu plus ; au soleil, l'inverse ; la grêle porte les tirs Glace.
La grêle et la tempête de sable ont un **vent** (direction fixe, flèche à l'écran) : un tir dans son sens gagne jusqu'à
+15 / +25 % de vitesse et de portée, contre lui il en perd autant ; Vol y est deux fois plus sensible, Roche et Sol
pas du tout. Le vent déporte aussi les Pokémon **Vol** (0,8 case/s sous la grêle, 1,5 dans la tempête de sable),
même à l'arrêt. Textures (pluie, grêle, sable, soleil, icônes, zones de Tourniquet et Lance-Boue, sable mouvant) :
`Design/effects_v14_meteo/`, prompt `Design/prompt_meteo.md`. **Tourniquet** et **Lance-Boue** ouvrent une zone de 4 cases
autour du lanceur pendant 15 s : plus de braises (Tourniquet) ou d'électricité (Lance-Boue) dedans, et les attaques
Feu (ou Électrik) y font moitié moins de dégâts. L'IA lance une météo quand elle favorise son équipe, un sport
quand des adversaires du type visé sont proches.

## Effets de terrain

Certaines attaques laissent une trace au sol, case par case, qui agit sur **tous** les Pokémon qui marchent dessus
(les deux camps), sauf ceux du type correspondant, ceux qui lévitent et **le lanceur lui-même** (sa propre trace ne
le gêne pas, même s'il n'est pas du type de l'attaque ; celui qui déclenche une affinité en devient le lanceur). Exceptions :
le verglas et la flaque agissent aussi sur leur lanceur (il glisse sur sa glace, il est ralenti dans son eau).
Les **alliés du lanceur** subissent le sol (glissade, ralentissements) mais pas l'attaque : ni brûlure, ni paralysie,
ni poison, ni dégâts. Une affinité appartient à l'équipe qui la déclenche (électrifier la flaque d'un ennemi frappe
ses alliés à lui). Les IA les contournent et en sortent.

| Terrain | Effet | Attaques |
|---|---|---|
| Verglas | forte glissade, poussées bien plus longues | Poudreuse, Vent Glace, Laser Glace, Blizzard |
| Braises | brûlure quasi immédiate qui dure 4 s après en être sorti, dégâts | Flammèche, Lance-Flamme, Déflagration, Éruption |
| Flaque | ralentit, soigne la brûlure ; Pokémon Eau plus rapides et soignés | Pistolet à O, Hydrocanon, Hydroblast |
| Sol chargé | paralysie fréquente, petits étourdissements à chaque seconde | Étincelle, Tonnerre, Fatal-Foudre |
| Eau électrifiée | paralysie, étourdissements plus longs, dégâts (Électrik sur une flaque) | — |
| Ronces | ralentit, petits dégâts | Fouet Lianes, Canon Graine, Mégafouet, Végé-Attak |
| Boue | ralentit fortement | Coud'Boue, Tir de Boue, Boue-Bombe |
| Flaque toxique | poison | Acide, Détritus, Bomb-Beurk |

La taille et la durée d'une trace suivent la puissance de l'attaque (bloc `sizing`).

**Affinités** : le feu brûle les ronces de proche en proche, fait fondre le verglas, sèche la boue, consume le
poison et s'éteint dans l'eau ; l'eau éteint les braises, dilue le poison et nettoie la boue ; la glace fond sur les
braises. Une flaque transmet : la glace la gèle, les plantes y poussent (plus longtemps), l'électricité l'électrifie,
le poison la contamine, chaque fois sur **toute la flaque reliée**, en vague ; la boue ne la détrempe que sur 2 cases
autour du point touché.

**IA** : les Pokémon contournent les terrains qui leur nuisent et en sortent ; ils préfèrent une attaque dont la
trace réagit contre les ennemis (Électrik sur des ennemis dans l'eau) et renoncent à celle qui toucherait un allié.
Blessé (sous 35 % des PV), un Pokémon peut se replier vers un terrain qui l'aide : une flaque s'il est Eau ou brûlé,
un terrain nocif auquel il est immunisé (les ennemis l'évitent).

Tout se règle dans `Assets/Resources/Data/terrains.json` : `terrains` (effets, immunités, soin, coût pour l'IA),
`moves` (attaques qui laissent une trace et sa forme), `sizing` et `reactions`.

## Journal des changements

### Après la mise à jour (à faire une fois)
Fermer Unity, puis `python3 Tools/setup.py` : il extrait les nouveaux assets tirés de la ROM (eau, lave, vide et glace
des obstacles dans `Assets/Resources/ObstacleTiles`, rochers `rock_9` / `rock_18`) et relance **Build Tilesets**, qui
donne à chaque décor son numéro de la ROM (`romId`, utilisé pour choisir les obstacles d'une arène). Sans cela, les
obstacles retombent sur des cases de couleur.

### 20/10/2026 — IA, lot A (Robin, branche `feature/ia`)
- **Choix de la cible** (alliés et groupes d'ennemis) : plus seulement le plus proche, mais un score (`TargetChoice`) :
  K.O. rapide possible, efficacité des types, menace sur un coéquipier faible face à lui, coéquipiers déjà sur lui,
  distance. Poids `aiTarget*` dans `meta.json`.
- **Alliés** : ils suivent le Pokémon joué quand il recule, contournent les dangers pour le rejoindre (sinon attendent
  au bord), engagent plus loin (9 cases) ; blessés (moins de 30 % de PV), ils restent près de lui. Réglages `ally*`.
- **Obstacles (lot C)** : un allié coupé du Pokémon joué par de l'eau, de la lave, du vide ou un pont brûlé les change
  avec ses attaques pour passer (gèle la rivière, nénuphars, pont de lianes, lave refroidie) ; il brûle le pont (ou
  fait fondre la glace) sous un ennemi au corps à corps, jamais sous un coéquipier.
- **Pylônes** : les IA ne voient venir un arc qu'avec 60 % de chances par cycle (`arc.aiAwareness` dans
  `obstacles.json`) et se font donc surprendre de temps en temps. **Métal** chargé ou brûlant : les IA ne s'en
  soucient pas (`aiBlind` dans `terrains.json`).
- **Banc d'essai des IA** (touche B dans le terrain de test, ou `python3 Tools/setup.py --ai-scenarios`) : 14 scénarios.
- Correctif : le terrain se dessinait au même niveau que les Pokémon (ils semblaient passer dessous et clignotaient).

### 20/10/2026 — Obstacles, météos (Robin, branche `feature/obstacles`)
- **Obstacles** : couche durable sous le terrain (eau profonde, lave, vide, ponts, glace, végétation, roche,
  rochers, murs de glace, pylônes électriques, plaques de métal et bornes aimantées, sable mouvant), avec chutes,
  chocs, interactions par type d'attaque et par contact du terrain, franchissement selon le type de Pokémon ou de
  déplacement de la ROM. Visuels : décors de la ROM + planches Claude Design (`Design/effects_v12_obstacles`,
  `effects_v13_electrique_metal`, `effects_v14_meteo`).
- **En partie** : 1 ou 2 obstacles par arène selon le décor (`arena_obstacles.json`), blocs de mur réduits, ennemis
  qui apparaissent seulement sur des cases sûres et atteignables. **Salle de test** des obstacles depuis le terrain de
  test (escalier, tirage, R pour retirer).
- **Météos et sports** : Zénith, Danse Pluie, Grêle, Tempêtesable, Tourniquet, Lance-Boue deviennent jouables
  (`climates.json`) : traces de terrain modifiées, dégâts par type, portée des tirs, vent des tempêtes qui porte les
  tirs et déporte les Pokémon Vol.
- Effets de terrain combinés (eau sur métal chargé : eau électrifiée…), apparition en fondu des traces.

### 19-20/10/2026 — Attaques de type, lot 1 (Owen, `feature/attaques-type-lot1`, fusionné dans `dev`)
- Vibraqua, Lame d'Air, Plaie-Croix, Onde Folie, Telluriforce, avec leurs planches ; spores et poudres (nuages qui se
  déploient) ; Pistolet à O en rafale très courte. Le lot 2 (Fatal-Foudre, Blizzard, Draco Météor, Pouv. Antique)
  est en cours sur `feature/attaques-type-lot2`.

### 19/10/2026 — Effets de terrain (Robin, `feature/effets-terrain`, fusionné dans `dev`)
- Traces au sol laissées par 24 attaques (verglas, braises, flaque, sol chargé, eau électrifiée, ronces, boue, flaque
  toxique), affinités entre terrains, tuiles animées aux bords découpés par élément, IA qui évite, se replie et
  profite du terrain ; terrain de test (Select / T au menu en `devMode`).

### 19/10/2026 — Persos de dev, portabilité (Robin)
- Persos de dev qui apprennent toutes les attaques (`devMode`), filtre par type au menu, commandes clavier U I O P.
- Portabilité multi-OS : `setup.py` trouve Unity sur Linux, macOS et Windows (`--unity`, `UNITY_PATH`, version la plus
  proche de la même branche), `uvx` pris dans le `PATH` pour unityMCP.

## Comment le dépôt gère les assets de la ROM

Unity identifie chaque asset par un **GUID** stocké dans son fichier `.meta`, et les scènes référencent les
assets par ce GUID. Le dépôt sépare donc les deux :

| Versionné (Git) | Ignoré (`.gitignore`), régénéré par `Tools/setup.py` |
|---|---|
| tous les `.meta` | `Assets/Sprites/Pokemon/*/*.png`, `AnimData.xml` |
| | `Assets/Resources/ObstacleTiles/*/chunks.png`, `tileset.json` (eau, lave, vide des obstacles) |
| `Assets/Sprites/Pokemon/<Nom>/id.txt` (liste des Pokémon du projet) | `Assets/Resources/Portraits/*.png` |
| code, scènes, réglages, `Tools/` | `Assets/Resources/AnimSets/*.asset` |
| données écrites à la main (`realtime_moves.json`, `meta.json`, `statuses.json`, `upgrades.json`, `type_chart.json`) | `Assets/Tilesets/*/chunks.png`, `tileset.json`, `Tileset.asset` |
| effets d'attaque originaux (`Assets/Resources/Effects`, `Design/`) | `Assets/Data/*.json`, `pokemon_rt.json`, `levels_rt.bytes`, `generated_moves.json` |

Le script remet chaque asset au chemin de son `.meta` : Unity retrouve les mêmes GUID et aucune référence ne
casse. Pour les ScriptableObjects reconstruits par Unity (`AnimSets`, `Tileset.asset`), il écrit d'abord un
fichier vide qui garde le GUID, puis les menus Build le remplissent.

**Si Unity est ouvert alors que des assets manquent**, il supprime leurs `.meta` orphelins (avertissement
« A meta data file (.meta) exists but its asset can't be found »). Pour réparer : fermer Unity, puis

```bash
git checkout -- .
```

puis relancer `python3 Tools/setup.py`.

## Règles pour les contributeurs

- **Ne jamais committer la ROM ni un fichier qui en est extrait.** Avant de pousser, vérifier que
  `git status` ne montre que du code, des `.meta`, des `id.txt` ou des données écrites à la main.
- **Toujours committer les `.meta`**, y compris ceux des assets ignorés.
- **Ajouter un Pokémon** : `Tools/.venv/bin/python -I Tools/pmd/import_pokemon.py . Nom=id`, puis le menu
  **Pokescrap > Build Anim Sets** ; committer `id.txt` et les `.meta` du dossier (les PNG sont ignorés tout seuls).
- **Ajouter un décor** : `Tools/.venv/bin/python -I Tools/pmd/extract_tileset.py Rom/eos_eu.nds Assets/Tilesets <id>`,
  puis **Pokescrap > Build Tilesets** ; committer les `.meta` du dossier.
- **Nouveau type d'asset tiré de la ROM** : l'ajouter au `.gitignore` et à `Tools/setup.py`.
- Travailler sur une branche par fonctionnalité et éviter de modifier la même scène (`.unity`) à deux en
  même temps : les conflits de scène se fusionnent mal. Une grande partie du jeu est construite à
  l'exécution par code, ce qui limite ces conflits.
- Après un `git pull` qui touche aux assets de la ROM (nouveaux Pokémon, décors), fermer Unity et relancer
  `python3 Tools/setup.py`.
