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

## Comment le dépôt gère les assets de la ROM

Unity identifie chaque asset par un **GUID** stocké dans son fichier `.meta`, et les scènes référencent les
assets par ce GUID. Le dépôt sépare donc les deux :

| Versionné (Git) | Ignoré (`.gitignore`), régénéré par `Tools/setup.py` |
|---|---|
| tous les `.meta` | `Assets/Sprites/Pokemon/*/*.png`, `AnimData.xml` |
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
